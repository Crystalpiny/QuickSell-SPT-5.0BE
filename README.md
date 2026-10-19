# QuickSell for SPT 5.0BE

An **unofficial community port** of [QuickSell 4.3.0](https://github.com/TadMaj/Tarkov-QuickSell) to **SPT 5.0 Bleeding Edge** (EFT `1.2.0.0.47965`, BepInEx 6 IL2CPP, SPTushonka 5.0).

> **Original mod:** [TadMaj / BlackHawk — Tarkov-QuickSell](https://github.com/TadMaj/Tarkov-QuickSell)
> All original design, code and credit belong to the original author. This repository only contains the SPT 5.0BE compatibility port. The original MIT license is preserved in [LICENSE](LICENSE).

---

## Features

- **Right-click quick sell**
  - `QuickSell (Flea)` — list the item on the flea market using the current market price.
  - `QuickSell (Trader)` — sell to the trader paying the most.
- **Price tooltips** — flea and best-trader prices appended to item tooltips, with configurable colors and a tooltip delay.
- **Keyboard shortcuts** — sell the hovered item to the flea market or to traders without opening the context menu.
- **Bulk flea price table** — one request at startup, cached client-side, so tooltips work in raid and do not hit the network on every hover.
- **F12 configuration** — prices, colors, keybinds, blacklist, sell-from-containers and more.

## Requirements

| Component | Version |
|---|---|
| SPT | 5.0 Bleeding Edge (`SPTarkov.Server.Core 5.0.0`) |
| EFT | `1.2.0.0.47965` |
| BepInEx | 6.x IL2CPP (`BepInEx.Unity.IL2CPP`) |
| Client patching | SPTushonka 5.0 (`SPTushonka.Common`, `SPTushonka.Reflection`) |
| .NET SDK (to build) | 10.0 |

The game must have been launched at least once so that `BepInEx\interop` exists.

## Installation

1. Copy `QuickSell.dll` to:
   ```
   <SPT>\BepInEx\plugins\QuickSell\QuickSell.dll
   ```
2. Copy `BlackHawk.QuickSell.Server.dll` to:
   ```
   <SPT>\SPT_Runtime\user\mods\QuickSell\BlackHawk.QuickSell.Server.dll
   ```
3. Start the server and the game.

The server mod exposes `GET /quicksell/getFleaPrices`; the client fetches it once at startup.

## Build

Set `SPT_ROOT` to the folder containing `EscapeFromTarkov.exe`, `BepInEx\` and `SPT_Runtime\`.

```powershell
# Client
dotnet build .\QuickSell.Client\QuickSell.Client.csproj -c Release -p:SPT_ROOT="C:\Path\To\SPT"

# Server
dotnet build .\QuickSell.Server\QuickSell.Server.csproj -c Release -p:SPT_ROOT="C:\Path\To\SPT"
```

The projects reference the game's BepInEx/interop assemblies and SPT server assemblies from that root; nothing is bundled.

## 5.0BE-specific changes

This port keeps the original behavior but adapts the parts that changed between SPT 4.1 and 5.0BE:

- **BepInEx 6 / SPTushonka 5.0** — `BasePlugin`, `ModulePatch`, IL2CPP interop references, `net10.0`.
- **Context menu targets** — `SimpleContextMenu.Show/ShowMenu` are still generic IL2CPP methods on 5.0; the patches resolve them and close over `EItemInfoButton`.
- **Renamed game members** — 5.0 interop exposes EFT fields as properties:
  - `mainMenuControllerClass` → `_menuOperation`
  - `iEftSession` → `_session`
  - `_dynamicInteractions` is resolved as a property and written through its indexer.
- **Separate price transport** — SPTushonka's `RequestHandler` expects encrypted/shuffled responses. The custom `/quicksell/getFleaPrices` endpoint returns plain JSON, so the client uses a local `HttpClient` + `System.Text.Json` instead.
- **Weapon pricing** — 5.0's `GetStaticPriceForItem` is far below the flea market for weapons (e.g. M9A3: 4,800 vs ~115,000). The server now prices weapons from live flea weapon offers, with a dynamic-price and static-price fallback.
- **Trader price lookup** — 5.0's IL2CPP interop throws `InvalidOperationException: Nullable object must have a value` from `Trader.GetUserItemPrice` when a trader does not buy the item. Each trader is now queried in its own `try/catch`; non-buyers are skipped and the next trader is asked.
- **UI Fixes multi-select** — there is no 5.0 build of UI Fixes, so multi-select is disabled (single-item selling only).

## Known limitations

- **Multi-select is unavailable** on 5.0BE — the UI Fixes interop stub returns an empty selection, so QuickSell falls back to single-item selling.
- `DynamicContextInteraction`'s `buttonType` is currently `null`; the menu entries work, but a future game update may require a proper `Il2CppSystem.Enum` wrapper.
- Trader assortments are refreshed fire-and-forget in 5.0 (the API returns an Il2Cpp `Task`), so the first trader-price tooltip after login may lag briefly.
- Tooltip placement/clipping is inherited from the game's tooltip layout.
- Tested on a modded SPT 5.0BE install; other 5.0 builds may rename internals again. Failures are logged with `QuickSell:` prefixes in `BepInEx\LogOutput.log`.

## Credits

- **BlackHawk / Tadas Majorovas ([TadMaj](https://github.com/TadMaj))** — author of the original [QuickSell](https://github.com/TadMaj/Tarkov-QuickSell). This port would not exist without their work.
- **Tyfon** — UI Fixes / multi-select interop used by the 4.x version.
- **925316** — flea duplicate-offer fix in the original mod.
- **SwiftXP** — "Show Me The Money" inspired the original price-tooltip approach.
- **Crystalpiny** — SPT 5.0BE port.

## License

MIT — see [LICENSE](LICENSE). The original copyright notice is preserved.

This is an unofficial fan project, not affiliated with Battlestate Games, SPT, or the original author. If the original author requests that this port be taken down, it will be removed.
