# 发帖文案（sns.oddba.cn）—— QuickSell rev2 更新日志

标题（推荐第一个，备选两条）：

【QuickSell 更新】v4.3.0 rev2（SPT 5.0BE）—— 修复愿望单/弹匣预设右键菜单异常
备选1：【更新】QuickSell SPT 5.0BE rev2 —— 右键菜单修复，覆盖安装即可
备选2：【QuickSell】rev2 小更新：菜单项不再互相顶掉（愿望单/弹匣预设/快速出售共存）

—————— 以下为正文 ——————

QuickSell 的 SPT 5.0BE 移植版更新 rev2，一个小修复更新，直接覆盖安装。

【这次修了什么】
- 修复右键菜单异常：之前版本的菜单补丁会把愿望单、弹匣预设的右键菜单项顶掉或者点了没反应。rev2 改掉了补丁挂载方式，现在愿望单、弹匣预设、QuickSell 的菜单项（跳蚤出售/商人出售）可以正常共存。

【问题说明：点击物品没反应 / 快捷键失效】
之前有朋友反馈过"鼠标左右键点物品没反应""N 键快速出售失效"——排查下来**不是 QuickSell 的锅**，是另一个 mod 的补丁污染了共享代码导致连坐。那个 mod 已经移除，rev2 也把菜单补丁的挂载面收窄了，这类冲突以后更难发生。如果你还遇到，删掉可疑 mod 对照测试，或带 BepInEx\LogOutput.log 回帖。

【安装】
1. 下载附件 zip（或 GitHub Release 的 QuickSell-4.3.0-SPT-5.0BE-r2.zip）；
2. 解压到 SPT 5.0BE 游戏根目录（BepInEx\plugins\QuickSell 和 SPT_Runtime\user\mods\QuickSell 自动就位），覆盖旧版即可；
3. 重启服务端和游戏，日志里看到 "QuickSell: Keybinds enabled." 就是装好了。

【下载与源码】
- 直接可用的 zip：https://github.com/Crystalpiny/QuickSell-SPT-5.0BE/releases/tag/v4.3.0-spt5.0be-r2
- 源码（MIT）：https://github.com/Crystalpiny/QuickSell-SPT-5.0BE

【致谢】
原作 QuickSell © 2024 Tadas Majorovas（TadMaj / BlackHawk），MIT 开源，原仓库：https://github.com/TadMaj/Tarkov-QuickSell 。快速出售这个设计、跳蚤/商人一键出的全部玩法都出自原作者，本仓库只做 SPT 5.0BE 的兼容移植。觉得好用欢迎去原仓库点个 star。
