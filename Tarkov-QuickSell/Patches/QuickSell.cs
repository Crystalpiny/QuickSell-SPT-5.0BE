using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Ragfair;
using EFT.Utilities;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using SPTushonka.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UIFixesInterop;
using UnityEngine;

namespace QuickSell.Patches
{
    /// <summary>
    /// SimpleContextMenu has two entry points that build a context menu for an item:
    /// Show&lt;T&gt;(position, contextInteractions, names, item) and
    /// ShowMenu&lt;T&gt;(position, contextInteractions, names, icons, item).
    /// In 4.0 this was a single obfuscated method_0.
    ///
    /// Both are patched. If Show delegates to ShowMenu the prefix runs twice, which is harmless -
    /// adding the entries is idempotent (same keys, same values). Patching only one risks missing
    /// whichever path the game actually takes, with no error to explain the absence.
    /// </summary>
    internal class ContextMenuShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => ContextMenuPatch.FindMenuMethod("Show");

        [PatchPrefix]
        private static void Prefix(object contextInteractions, Item item)
        {
            // 5.0 的 SimpleContextMenu.Show<T>/ShowMenu<T> 由所有 T 共用同一段 native body，
            // Harmony 补丁会对 Wishlist/MagPreset 等其它 T 的调用一起生效。这里必须用 object
            // 接收并按类型过滤，否则 IL2CPP 会在参数绑定阶段抛 MethodAccessException，
            // 直接把愿望单/弹夹快速装弹等原版菜单弄坏。
            if (contextInteractions is ContextInteractions<EItemInfoButton> typed)
                ContextMenuPatch.AddQuickSellEntries(typed, item);
        }
    }

    internal class ContextMenuShowMenuPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => ContextMenuPatch.FindMenuMethod("ShowMenu");

        [PatchPrefix]
        private static void Prefix(object contextInteractions, Item item)
        {
            // 5.0 的 SimpleContextMenu.Show<T>/ShowMenu<T> 由所有 T 共用同一段 native body，
            // Harmony 补丁会对 Wishlist/MagPreset 等其它 T 的调用一起生效。这里必须用 object
            // 接收并按类型过滤，否则 IL2CPP 会在参数绑定阶段抛 MethodAccessException，
            // 直接把愿望单/弹夹快速装弹等原版菜单弄坏。
            if (contextInteractions is ContextInteractions<EItemInfoButton> typed)
                ContextMenuPatch.AddQuickSellEntries(typed, item);
        }
    }

    /// <summary>
    /// 5.0 的 SimpleContextMenu.Show&lt;T&gt;/ShowMenu&lt;T&gt; 所有泛型实例共用同一段 native body，
    /// 给 EItemInfoButton 打 Harmony detour 会连带破坏 Wishlist/MagPreset 等其它 T 的菜单
    /// （MethodAccessException）。改为在 InventoryItemContextInteractions 构造完成后
    /// 直接把 QuickSell 条目写进它的 dynamic interactions，不再碰泛型 Show。
    /// </summary>
    internal class InventoryItemContextCtorPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.GetDeclaredConstructors(typeof(InventoryItemContextInteractions))
                .FirstOrDefault(ctor => !ctor.IsStatic);

        [PatchPostfix]
        private static void Postfix(InventoryItemContextInteractions __instance)
        {
            if (__instance == null) return;
            var item = __instance.Item;
            if (item == null) return;
            ContextMenuPatch.AddQuickSellEntries(__instance, item);
        }
    }

    internal class InventoryItemContextInteractionsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(InventoryItemContextInteractions), "get_AvailableInteractions");

        [PatchPrefix]
        private static void Prefix(InventoryItemContextInteractions __instance)
        {
            if (__instance == null) return;
            var item = __instance.Item;
            if (item == null) return;
            ContextMenuPatch.AddQuickSellEntries(__instance, item);
        }
    }

    /// <summary>Selling logic, shared by the context menu entries and the keybinds.</summary>
    internal static class ContextMenuPatch
    {
        // The "unload ammo" sprite is reused for both menu entries. Fetching it per right-click was
        // wasteful; it never changes, so it is fetched once.
        private static Sprite _menuSprite;
        private static bool _menuSpriteFetched;

        private static Sprite MenuSprite
        {
            get
            {
                if (_menuSpriteFetched) return _menuSprite;
                _menuSpriteFetched = true;
                _menuSprite = ResourcesCache.Pop<Sprite>("Characteristics/Icons/UnloadAmmo");
                return _menuSprite;
            }
        }

        internal static MethodBase FindMenuMethod(string name)
        {
            // Requiring an Item parameter disambiguates the two Show overloads: only one takes an
            // Item, the other takes an icons dictionary.
            var all = typeof(SimpleContextMenu)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                var dump = string.Join(" | ", all
                    .Where(m => m.Name == "Show" || m.Name == "ShowMenu")
                    .Select(m => $"{m.Name}{(m.IsGenericMethodDefinition ? "<T>" : "")}" +
                                 (m.IsPublic ? "[pub]" : "[nonpub]") +
                                 "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.FullName)) + ")"));
                Plugin.LogSource?.LogInfo($"QuickSell: SimpleContextMenu candidates: {dump}");
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning($"QuickSell: candidate dump failed: {ex.Message}");
            }

            var method = all.FirstOrDefault(m =>
                    m.Name == name
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Any(p => p.ParameterType.FullName == "EFT.InventoryLogic.Item"
                                                  || typeof(Item).IsAssignableFrom(p.ParameterType)));

            if (method == null)
            {
                Plugin.LogSource?.LogError(
                    $"QuickSell: SimpleContextMenu.{name}<T>(... Item ...) not found. " +
                    "That context menu path will not get QuickSell entries.");
                return null;
            }

            return method.MakeGenericMethod(typeof(EItemInfoButton));
        }

        internal static IEftSession GetSession()
        {
            var fromUi = ItemUiContext.Instance?.Session;
            if (fromUi != null) return fromUi;

            var app = ClientAppUtils.GetMainApp();
            if (app == null) return null;

            var mainMenu = Compat.Get<MainMenuShowOperation>(app, "_menuOperation");
            return mainMenu == null ? null : Compat.Get<IEftSession>(mainMenu, "_session");
        }

        private static MainMenuShowOperation GetMainMenu()
        {
            var app = ClientAppUtils.GetMainApp();
            if (app == null)
            {
                Utils.SendError("TarkovApplication instance is null");
                return null;
            }

            var mainMenu = Compat.Get<MainMenuShowOperation>(app, "_menuOperation");
            if (mainMenu == null) Utils.SendError("MainMenuShowOperation is null");
            return mainMenu;
        }

        internal static void AddQuickSellEntries(
            ContextInteractions<EItemInfoButton> contextInteractions,
            Item item)
        {
            try
            {
                AddQuickSellEntriesCore(contextInteractions, item);
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning($"QuickSell: adding context menu entries failed: {ex}");
            }
        }

        private static object GetDynamicInteractions(ContextInteractions<EItemInfoButton> contextInteractions)
        {
            var prop = typeof(ContextInteractions<EItemInfoButton>).GetProperty(
                "_dynamicInteractions",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop?.GetValue(contextInteractions);
        }

        private static void SetDynamicInteraction(object dict, string key, DynamicContextInteraction value)
        {
            var indexer = dict.GetType().GetProperty("Item", new[] { typeof(string) });
            if (indexer == null) throw new MissingMemberException(dict.GetType().Name, "Item");
            indexer.SetValue(dict, value, new object[] { key });
        }

        private static void AddQuickSellEntriesCore(
            ContextInteractions<EItemInfoButton> contextInteractions,
            Item item)
        {
            if (contextInteractions is not InventoryItemContextInteractions) return;
            if (item == null) return;
            if (ModEnvironment.IsInRaid) return;

            var itemContext = Compat.Get<ItemContext>(contextInteractions as BaseItemContextInteractions, "ItemContext");
            if (itemContext == null || itemContext.ViewType != EItemViewType.Inventory) return;

            if (Singleton<MenuUI>.Instantiated &&
                Singleton<MenuUI>.Instance.HideoutAreaTransferItemsScreen != null &&
                Singleton<MenuUI>.Instance.HideoutAreaTransferItemsScreen.isActiveAndEnabled) return;

            // Where an item may be sold from - stash, and optionally containers you are carrying.
            // See SellLocation.
            if (!SellLocation.IsSellable(item)) return;

            // Was .Dictionary_0 in 4.0. The public DynamicInteractions property is a read-only
            // IEnumerable, so the backing dictionary is needed in order to add entries.
            var dynamicInteractions = GetDynamicInteractions(contextInteractions);
            if (dynamicInteractions is null)
            {
                Plugin.LogSource?.LogError("QuickSell: ContextInteractions._dynamicInteractions not found.");
                return;
            }

            if (Plugin.EnableQuickSellFlea)
            {
                SetDynamicInteraction(dynamicInteractions, "QuickSell (Flea)", new DynamicContextInteraction(
                    "QuickSell (Flea)", "QuickSell (Flea)", Il2CppConv.Act(() => SellToFlea(item)), MenuSprite, null, null));
            }

            if (Plugin.EnableQuickSellTraders)
            {
                SetDynamicInteraction(dynamicInteractions, "QuickSell (Trader)", new DynamicContextInteraction(
                    "QuickSell (Trader)", "QuickSell (Trader)", Il2CppConv.Act(() => SellToTraders(item)), MenuSprite, null, null));
            }

            Plugin.LogSource?.LogInfo($"QuickSell: menu entries added for {item.Id} (flea={Plugin.EnableQuickSellFlea}, trader={Plugin.EnableQuickSellTraders})");
        }

        // ------------------------------------------------------------------ traders

        public static void SellToTraders(Item item)
        {
            try
            {
                var session = GetSession();
                if (session == null)
                {
                    Utils.SendError("Session is not available");
                    return;
                }

                // Assortments are loaded ONCE for the whole operation. The old code ran a trader
                // health check inside the per-item loop, so a 30-item sale repeated it 30 times.
                TraderService.EnsureAllAssortments(session);

                var items = GetItemsToSell(item);

                // Each item is priced ONCE and the winning trader kept. Previously SelectTrader ran
                // here to build the total and then again inside the sell, doubling a full trader
                // sweep per item.
                var offers = new List<(Item Item, Trader Trader, int Price)>(items.Count);
                var total = 0;

                foreach (var candidate in items)
                {
                    if (TraderService.TryGetBestOffer(candidate, session, out var trader, out var price))
                    {
                        offers.Add((candidate, trader, price));
                        total += price;
                    }
                }

                if (offers.Count == 0)
                {
                    Utils.SendError("No items can be sold to traders");
                    return;
                }

                var byId = offers.ToDictionary(o => o.Item.Id);

                ConfirmWindow(
                    () =>
                    {
                        var soundGate = new SellSoundGate(Plugin.TraderSellSound);

                        if (IsMultiSelectActive())
                        {
                            MultiSelect.Apply(selected =>
                            {
                                if (selected != null && byId.TryGetValue(selected.Id, out var offer))
                                    ExecuteTraderSale(offer.Item, offer.Trader, offer.Price, soundGate.OnResult);
                            }, ItemUiContext.Instance);
                            return;
                        }

                        foreach (var offer in offers)
                            ExecuteTraderSale(offer.Item, offer.Trader, offer.Price, soundGate.OnResult);
                    },
                    "to the traders",
                    offers.Count,
                    total);
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        /// <summary>Sells one item to an already-chosen trader at an already-known price.</summary>
        private static void ExecuteTraderSale(Item item, Trader trader, int price, Action<IResult> onResult)
        {
            try
            {
                // Was trader.ITraderInteractions in 4.0; now a private field.
                var interactions = Compat.Get<ITradingSession>(trader, "_trading");
                if (interactions is null)
                {
                    Utils.SendError("Failed to get trader interactions");
                    return;
                }

                interactions.ConfirmSell(
                    trader.Id,
                    new Il2CppReferenceArray<TradingItemReference>(new[] { new TradingItemReference { Item = item, Count = item.StackObjectsCount } }),
                    price,
                    Il2CppConv.Cb(onResult));
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        // ------------------------------------------------------------------ flea

        public static void SellToFlea(Item item)
        {
            try
            {
                if (!Singleton<MenuUI>.Instantiated || Singleton<MenuUI>.Instance?.TradingScreen == null)
                {
                    Utils.SendError("MenuUI is not available");
                    return;
                }

                var session = GetSession();
                if (session == null) return;

                var mainMenu = GetMainMenu();
                if (mainMenu == null) return;

                var inventoryController = mainMenu.InventoryController;
                if (inventoryController == null || inventoryController is not ItemController traderController)
                {
                    Utils.SendError("Could not load inventory");
                    return;
                }

                var ragFair = session.RagFair;
                if (!ragFair.Available)
                {
                    Utils.SendError("Flea market is not available");
                    return;
                }

                var helper = new RagfairNewOfferContext(inventoryController.Inventory.Stash.Grids[0], traderController, ragFair);
                var items = GetItemsToSell(item);
                var validItems = items.Where(i => helper.HighlightedAtRagfair(i)).ToList();

                if (validItems.Count == 0)
                {
                    Utils.SendError("No items can be sold on the flea");
                    return;
                }

                if (!Plugin.IgnoreFleaCapacity &&
                    ragFair.MyOffersCount + validItems.Count > ragFair.GetMaxOffersCount(ragFair.MyRating))
                {
                    Utils.SendError("Not enough flea offer slots");
                    return;
                }

                // Flea price is per template; one lookup covers every copy of the same template.
                var templates = new HashSet<string>(validItems.Select(i => i.TemplateId.ToString()));

                // Cache hits fire the RequestIfMissing callback synchronously, so including cached
                // templates could double-fire the confirmation when everything is already cached.
                var uncached = templates.Where(t => !FleaPriceCache.TryGet(t, out _)).ToList();

                if (uncached.Count == 0)
                {
                    ShowFleaConfirmation(validItems, session);
                    return;
                }

                var outstanding = uncached.Count;
                var gate = new object();

                foreach (var templateId in uncached)
                {
                    FleaPriceCache.RequestIfMissing(ragFair, templateId, _ =>
                    {
                        bool ready;
                        lock (gate) { ready = --outstanding == 0; }
                        if (ready) ShowFleaConfirmation(validItems, session);
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static void ShowFleaConfirmation(List<Item> validItems, IEftSession session)
        {
            try
            {
                var prices = new Dictionary<string, int>();
                var total = 0;
                var totalFee = 0;

                foreach (var candidate in validItems)
                {
                    if (!FleaPriceCache.TryGet(candidate.TemplateId, out var avg)) continue;

                    // Use the same quality-aware formula as the tooltip. Partially used meds,
                    // food/drinks and other resource items are scaled by what remains; stackable
                    // items such as ammo still use a per-unit listing price here.
                    var price = FleaListingPrice.GetListingUnitPrice(candidate, avg);
                    if (price <= 0) continue;

                    prices[candidate.Id] = price;
                    total += price * candidate.StackObjectsCount;

                    // Tax stays PER ITEM even though price is per template: the fee depends on the
                    // individual item's stack size and condition, not just its type.
                    totalFee += (int)Math.Ceiling(
                        PriceCalculator.CalculateTaxPrice(candidate, candidate.StackObjectsCount, price, false));
                }

                if (prices.Count == 0)
                {
                    Utils.SendError("Could not determine flea prices");
                    return;
                }

                ConfirmWindow(
                    () =>
                    {
                        var soundGate = new SellSoundGate(Plugin.FleaListingSound);

                        if (IsMultiSelectActive())
                        {
                            MultiSelect.Apply(selected =>
                            {
                                if (selected != null && prices.TryGetValue(selected.Id, out var price))
                                    DoFleaOffer(selected, session, price, soundGate.OnResult);
                            }, ItemUiContext.Instance);
                            return;
                        }

                        foreach (var candidate in validItems)
                        {
                            if (prices.TryGetValue(candidate.Id, out var price))
                                DoFleaOffer(candidate, session, price, soundGate.OnResult);
                        }
                    },
                    "on the flea",
                    prices.Count,
                    total,
                    totalFee);
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        private static void DoFleaOffer(Item item, IEftSession session, int price, Action<IResult> onResult)
        {
            try
            {
                var requirements = new List<BarterTemplate>
                {
                    new()
                    {
                        _tpl = (string)CurrencyUtil.GetCurrencyId(ECurrencyType.RUB),
                        count = price,
                        onlyFunctional = true
                    }
                };

                session.RagfairAddOffer(false, new Il2CppStringArray(new[] { item.Id }), new Il2CppReferenceArray<BarterTemplate>(requirements.ToArray()), Il2CppConv.Cb(onResult));
            }
            catch (Exception ex)
            {
                Utils.SendError(ex.ToString());
                Plugin.LogSource.LogWarning(ex.ToString());
            }
        }

        // ------------------------------------------------------------------ shared

        /// <summary>
        /// Handles the optional QuickSell completion sound without changing the sale callback flow.
        /// Trader and flea sounds can be enabled separately, and multi-item operations can play
        /// either once for the whole batch or once for every successful item.
        /// </summary>
        private sealed class SellSoundGate
        {
            private readonly bool _enabled;
            private readonly bool _playPerItem;
            private bool _played;

            public SellSoundGate(bool enabled)
            {
                _enabled = enabled;
                _playPerItem = Plugin.PlaySellSoundPerItem;
            }

            public void OnResult(IResult result)
            {
                if (!_enabled || !result.Succeed) return;

                if (!_playPerItem)
                {
                    if (_played) return;
                    _played = true;
                }

                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
            }
        }

        private static bool IsMultiSelectActive()
        {
            return Plugin.EnableUIFixesIntegration && MultiSelect.Count > 0;
        }

        internal static List<Item> GetItemsToSell(Item item)
        {
            if (item == null) return new List<Item>();
            if (!Plugin.EnableUIFixesIntegration) return new List<Item> { item };
            if (MultiSelect.Count > 0) return MultiSelect.Items.ToList();
            return new List<Item> { item };
        }

        public static void ConfirmWindow(Action callback, string source, int count, int? totalPrice = null, int? fee = null)
        {
            if (!Plugin.ShowConfirmationDialog) { callback(); return; }

            var itemUiContext = ItemUiContext.Instance;
            if (itemUiContext == null) { callback(); return; }

            string baseMsg = count == 1
                ? $"Are you sure you want to sell this item {source}?".Localized()
                : $"Are you sure you want to sell {count} items {source}?".Localized();

            string message;
            if (totalPrice.HasValue && fee.HasValue && fee.Value > 0)
            {
                message = $"{baseMsg}\n\nListing Fee: {PriceFormat.Format(fee.Value)}\nNet Profit: {PriceFormat.Format(totalPrice.Value - fee.Value)}";
            }
            else if (totalPrice.HasValue)
            {
                message = $"{baseMsg}\n\n{PriceFormat.Format(totalPrice.Value)}";
            }
            else
            {
                message = baseMsg;
            }

            itemUiContext.ShowMessageWindow(message, callback, Il2CppConv.Act(() => { }), null, 0f, false, TextAlignmentOptions.Center);
        }
    }
}
