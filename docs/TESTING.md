# QuickSell 4.3.0 → SPT 5.0BE 移植测试说明

## 产物与安装

| 部分 | 路径 | 大小 |
|---|---|---|
| 客户端 | `BepInEx\plugins\QuickSell\QuickSell.dll` | 89,088 B |
| 服务端 | `SPT_Runtime\user\mods\QuickSell\BlackHawk.QuickSell.Server.dll` | 20,480 B |
| 源码工程 | `_src\QuickSell-Port\`（`QuickSell.Client` + `QuickSell.Server`） | — |

GUID 沿用上游：`com.blackhawk.quicksell`，版本 `4.3.0`，`SptVersion = ~5.0.0`。

## 已通过验证

### 1. 编译
- 客户端：`dotnet build` 0 error / 0 warning（net10.0，BepInEx 6 IL2CPP + interop）。
- 服务端：0 error（net10.0，SPT 5.0 `SPTarkov.Server.Core`）。

### 2. 服务端
启动 SPT 5.0 服务端：

```
[ModValidator] 模组：QuickSell 版本4.3.0
(GUID: com.blackhawk.quicksell | targets SPT: ~5.0.0) 已加载
```

`GET https://127.0.0.1:6969/quicksell/getFleaPrices` → **200**，171,177 字节，**5,183 个物品价格**：

```
[QuickSell] Built flea price table: 5183 items from 53577 live offers
(3508 live averages, 174 weapon bases, 1501 fallbacks) in 60ms.
```

### 3. 客户端启动
真实启动游戏后 BepInEx 日志：

```
Loading [BlackHawk-QuickSell 4.3.0]
ContextMenu.Show     -> SimpleContextMenu.Show
ContextMenu.ShowMenu -> SimpleContextMenu.ShowMenu
Tooltip              -> SimpleTooltip.Show
TooltipCacheInvalidation enabled
MenuPrecompute       -> MenuScreen.Awake
InventoryPrecompute  -> InventoryScreen.Show
TooltipDelay enabled
Keybinds enabled
FleaPriceClient.Fetch enabled
loaded 5183 flea prices; cache now holds 5183
```

### 4. 游戏内交互
- 仓库右键物品，QuickSell 前缀成功执行，日志：`menu entries added for <itemId> (flea=True, trader=True)`。
- 出现确认框：`Are you sure you want to sell this item on the flea? Listing Fee: ₽1,744 Net Profit: ₽10.2k`（截图 `screenshots/01-confirmation-dialog.png`）。
- 点「是」后服务端收到 `RagFairAddOffer` 事件；因角色等级 <25 被游戏规则拒绝：
  `Unable to trade this item on the flea market before level 25`。
  这是正常游戏限制，不是移植问题；说明「客户端 → 服务端」整条链路已打通。

## 已知差异 / 限制

1. **多选出售不可用**：5.0 没有 UI Fixes，`UIFixesInterop` 为空实现（`Count=0`），QuickSell 会退化为单选。
2. **`DynamicContextInteraction` 的 `buttonType`**：5.0 构造要求 `Il2CppSystem.Enum`，`EItemInfoButton` 无法直接转换，当前传 `null`。菜单已能显示/执行；若后续出现空引用，需要补正确的 IL2CPP 枚举包装。
3. **服务端 assort 刷新**：5.0 的 `RefreshAssortment` 返回 `Il2CppSystem.Threading.Tasks.Task<IResult>`，目前触发后不 await（fire-and-forget），首次 tooltip 商人价可能略滞后。
4. **工具提示**：补丁已启用，价格文本会追加到 `SimpleTooltip.Show` 的 `text` 参数；实际 tooltip 在屏幕右缘可能被裁切（截图可见价格条），如需调整位置属于 UI 调优。
5. 日志里偶见游戏自身的 `MethodAccessException`（MagPreset/Wishlist 上下文的枚举迭代），与 QuickSell 的出售链路无直接关系，未在本移植中处理。
6. 音效、磨损药品/食物折价、弹药穿透颜色等逻辑已随源码编译进来，但只做了启动/出售链路验证，未逐项人工确认。

## 卸载 / 回滚

删除以下两个目录即可：

```
BepInEx\plugins\QuickSell
SPT_Runtime\user\mods\QuickSell
```

配置在 `BepInEx\config\com.blackhawk.quicksell.cfg`，可一并删除。

## 复测步骤

1. 完整重启 SPT 服务端和游戏。
2. 进仓库右键物品 → 应出现 `QuickSell (Flea)` / `QuickSell (Trader)`。
3. 悬浮物品 → tooltip 追加跳蚤价/商人价。
4. 若右键无条目或出售报错，查看 `BepInEx\LogOutput.log` 中 `QuickSell:` 行（每个补丁的 `enabled`/`failed` 目标都有记录），以及 `SPT_Runtime\user\logs\spt\spt*.log`。

## 价格修正（2026-10-19 20:26）

**问题**：M9A3 跳蚤市场价约 10w–12w，QuickSell 只按 4,800 上架。

**原因**：上游 4.3.0 服务端对 `BaseClasses.WEAPON` 直接使用 `GetStaticPriceForItem`（手册/基础价），4.1 时代接近市场，5.0BE 里 M9A3 只有 4,800；而跳蚤实际成交价 11w+。

**修复**：`FleaPriceEndpoint` 对武器改为按跳蚤现有武器报价的平均价（`weaponAverages`），没有报价时退回动态价，最后才用静态基础价。

**验证**：服务端重启后 `GET /quicksell/getFleaPrices` 中 M9A3（`5cadc190ae921500103bb3b6`）价格 = **115,282**（修复前 4,800）。客户端下次启动会重新拉取价格表。

**注意**：目前武器均价包含带配件的整枪报价，重度改装枪可能被估高；如需要可以改成中位数或裸枪+配件拆分估价。

## 卖给商人报错修复（2026-10-19 20:34）

**现象**：卖 MK47 给商人时弹错误提示。

**日志**：
```
QuickSell.Patches.TraderService.TryGetBestOffer(...)
System.InvalidOperationException: Nullable object must have a value.
```

**原因**：5.0BE 的 IL2CPP interop 在 `Trader.GetUserItemPrice` 返回「无报价」的
`Nullable<ItemPrice>` 时，会直接抛 `InvalidOperationException`，而不是返回 `null`。
Therapist / Ragman / Jaeger 这类不收该物品的商人先被问到，异常直接中断了整个询价循环。

**修复**：`TryGetBestOffer` 对每个商人单独 try/catch；不收该物品的商人记录一条
`price lookup failed for trader ...` 后跳过，继续问下一个，直到找到最高报价。

**验证**：
- 重新启动客户端后，商人确认框正常出现（M855A1 54 发，₽40.5k）。
- 点「是」后服务端日志：
  `event: TradingConfirm`、`Selling: id ... tpl: 606587252535c57a13424cfd`，成交成功。
- 日志中仅剩 Therapist/Ragman/Jaeger 的 `price lookup failed` 警告（预期，它们不收该物品），不再有弹窗。

当前客户端 DLL：89,600 B（已安装并在运行中的游戏里加载）。
