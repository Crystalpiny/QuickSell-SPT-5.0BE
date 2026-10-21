# QuickSell 4.3.0 → SPT 5.0BE 移植进度

## 目标
客户端 `BepInEx\plugins\QuickSell\QuickSell.dll` + 服务端 `SPT_Runtime\user\mods\QuickSell\`，功能对齐 4.3.0（快速出售跳蚤/商人、tooltip 价格、颜色、音效、磨损折价；UIFixes 多选在 5.0 无对应版本，降级为单选）。

## 已完成（M0 脚手架）
- 源码复制到 `_src\QuickSell-Port\Tarkov-QuickSell`（20 个客户端文件、1 个服务端文件）。
- 新建 `QuickSell.Client.csproj`：net10.0，引用 5.0BE 的 BepInEx 6 / Il2CppInterop / interop Assembly-CSharp / Comfort / Diz.Binding / SemanticVersioning / SPTushonka.Common+Reflection。
- 批量替换 SPT 4 命名空间：
  - `SPT.Reflection.Patching` → `SPTushonka.Reflection.Patching`
  - `SPT.Reflection.Utils` → `SPTushonka.Reflection.Utils`
  - `SPT.Common.Http` → `SPTushonka.Common.Http`
- `Plugin : BaseUnityPlugin` → `BasePlugin`，`Awake()` → `Load()`；`Logger` → `Log`。
- 移除 BepInEx 5 `Chainloader` 依赖（headless 检测先固定 false）。
- UIFixes 多选：`MultiSelectInterop.cs` 停用，新增空实现 `UIFixesInteropStub.cs`（Count=0，单选回退）。
- 补 `Diz.Binding` / `SemanticVersioning` 引用。

## 当前编译错误（约 10 类）
1. `DynamicContextInteraction` 构造多了 `buttonType`（Enum）参数。
2. `RagfairNewOfferContext` 构造多了 `RagFair` 参数。
3. `Il2CppReferenceArray<T>` / `Il2CppStringArray` 不能无参构造/集合初始化。
4. IL2CPP 委托：`Il2CppSystem.Action`、`Il2CppSystem.Action<T>`、`Callback` 需要显式转换（DelegateSupport）。
5. `trader.RefreshAssortment` 返回 `Il2CppSystem.Threading.Tasks.Task<IResult>`，不能直接当 `System.Threading.Tasks.Task`。
6. 少量 `Task`/`??` 类型不匹配。

## 剩余里程碑
- **M1 核心可用**：右键出售到跳蚤/商人 + 确认框 + 服务端 `/quicksell/getFleaPrices` endpoint。
- **M2 价格显示**：tooltip 价格、缓存、颜色分级、延迟。
- **M3 对齐 4.3.0**：音效、磨损药品/食物折价、弹药等级、调试日志。
- **M4 游戏内验证**：逐个补丁看 `BepInEx\LogOutput.log` 的启用日志，修正 5.0 字段名/方法名。

## 已知风险
- 4.3.0 依赖 4.1.6 EFT UI 内部字段（`_dynamicInteractions`、`mainMenuControllerClass`、`iEftSession` 等），5.0BE 需逐个重新定位。
- 服务端 4.1 `IModMetadata` 版本范围会拒绝 5.0，需要改成 `~5.0.0` 并用 5.0 的 `IHttpListener` 签名。
- 多选功能在 5.0 没有 UIFixes，先降级单选。

## 说明
当前尚未向游戏目录安装任何文件；等 M1 编译通过后再放 DLL 并让游戏日志验证。

## M1 进展（2026-10-19 20:10）
- 客户端 `QuickSell.Client.csproj` 编译通过：0 error / 0 warning，产物 `QuickSell.dll`（82,944 B）。
- 服务端 `QuickSell.Server.csproj` 编译通过：0 error，产物 `BlackHawk.QuickSell.Server.dll`（20,480 B），`SptVersion` 已改为 `~5.0.0`。
- 已安装：
  - `BepInEx\plugins\QuickSell\QuickSell.dll`
  - `SPT_Runtime\user\mods\QuickSell\BlackHawk.QuickSell.Server.dll`
- 当前游戏/服务端进程在 19:43 启动，早于安装时间；**需要完整重启服务端和游戏才会加载**。
- 待验证：SimpleContextMenu 目标方法、`_dynamicInteractions` 字段、`mainMenuControllerClass`/`iEftSession` 反射、tooltip 目标、服务端 endpoint 返回。

## 测试步骤
1. 退出游戏和 SPT 服务端，重新启动。
2. 看 `BepInEx\LogOutput.log` 是否有 `BlackHawk-QuickSell` / `QuickSell` 加载与补丁启用/失败日志。
3. 仓库里右键物品，应出现 `QuickSell (Flea)` / `QuickSell (Trader)`。
4. 服务端日志看 `/quicksell/getFleaPrices` 请求与 `[QuickSell] Built flea price table`。
5. 把两份日志发回，按失败项修 5.0 字段/方法。

## 已知差异
- UI Fixes 多选在 5.0 不可用，已降级为单选（`UIFixesInteropStub`）。
- 工具提示/价格缓存依赖 4.1.6 UI 内部结构，可能需要在 5.0 重新定位。
- 服务端 assort 刷新改为 fire-and-forget（5.0 返回 Il2Cpp Task，暂不 await）。

## 静态适配（第二轮）
- SimpleContextMenu 5.0 的 Show/ShowMenu 不再是泛型方法，`FindMenuMethod` 已改为按名称 + Item 参数定位。
- Compat 改为同时查属性/字段：5.0 interop 把 EFT 私有字段暴露成属性。
- 字段映射：`mainMenuControllerClass`→`_menuOperation`，`iEftSession`→`_session`，`_dynamicInteractions` 走属性。
- Plugin.Load 增加总 try/catch 和逐补丁 Safe() 包装，单个补丁目标找不到不会拖垮插件；日志会打印每个补丁 enabled/failed。
- 工具提示目标已验证：`SimpleTooltip.Show(string text, ..., float delay, ...)`、`ItemAddress.RaiseAddEvent(item,status,controller)`、`Item.RaiseRefreshEvent(bool,bool)` 参数名匹配。
- 已重新安装最新客户端/服务端 DLL。

## 服务端验证（2026-10-19 20:10）
- 启动 SPT 5.0 服务端：ModValidator 日志 `模组：QuickSell 版本4.3.0 (GUID: com.blackhawk.quicksell | targets SPT: ~5.0.0) 已加载`。
- GET `https://127.0.0.1:6969/quicksell/getFleaPrices` 返回 **200**，171,177 字节，5,183 个物品价格。
- 服务端日志：`[QuickSell] Built flea price table: 5183 items from 53577 live offers (3508 live averages, 174 weapon bases, 1501 fallbacks) in 60ms.`
- 验证完已停止测试用服务端进程。

## 客户端启动验证（2026-10-19 20:14）
实际启动 5.0BE 客户端 + 服务端，BepInEx 日志：
- `Loading [BlackHawk-QuickSell 4.3.0]`
- `SimpleContextMenu candidates: Show<T>[pub](..., EFT.InventoryLogic.Item) | ShowMenu<T>[pub](..., Item)` —— 5.0 仍是泛型方法，已恢复 `MakeGenericMethod(EItemInfoButton)`。
- `ContextMenu.Show -> SimpleContextMenu.Show`、`ContextMenu.ShowMenu -> SimpleContextMenu.ShowMenu` 均启用。
- `Tooltip -> SimpleTooltip.Show`、`TooltipCacheInvalidation`、`MenuPrecompute -> MenuScreen.Awake`、`InventoryPrecompute -> InventoryScreen.Show`、`TooltipDelay`、`Keybinds` 全部 enabled。
- `FleaPriceClient.Fetch` 成功：`loaded 5183 flea prices; cache now holds 5183.`（改用本机明文 HttpClient + System.Text.Json，绕开 SPTushonka 加密响应的 deshuffle）。
- 验证完已停止测试用游戏/服务端进程。

## 待用户验证
- 仓库右键是否出现 `QuickSell (Flea)` / `QuickSell (Trader)`；`DynamicContextInteraction` 的 buttonType 目前传 null，若菜单空引用需改成具体枚举。
- tooltip 价格是否显示、颜色/延迟是否正常。
- 实际出售/上架是否成功（服务端 endpoint 已通）。

## 启动复测（2026-10-19 20:18）
- 再次完整启动：所有补丁 enabled，`loaded 5183 flea prices`，日志无异常。
- `DynamicContextInteraction` 的 buttonType：`EItemInfoButton` 不能直接转 `Il2CppSystem.Enum`，暂传 null；`AddQuickSellEntries` 已包 try/catch，右键菜单即使构造失败也只记日志、不破坏原菜单。
- 当前安装：`BepInEx\plugins\QuickSell\QuickSell.dll` 88,064 B。
