# 开发与调试

## 环境

| 项 | 要求 |
| --- | --- |
| Unity | 2022.3.34f1c1（`ProjectSettings/ProjectVersion.txt`） |
| 平台 | Windows（当前工程按 Windows 配置；其他平台未验证） |
| Python（仅美术工具） | Python 3 + Pillow |
| 多开 | ParrelSync（已随工程提供） |

首次打开工程时 Unity 会重建 `Library/`，耗时较长。`Library`、`Temp`、`Logs`、`UserSettings` 都已被 `.gitignore` 忽略。

## 运行

1. 用 Unity Hub 打开工程根目录。
2. 打开 `Assets/Scenes/start.unity`。
3. 点击 Play。
4. 房主：点“开房间” → 进入 `Host` 场景。
5. 客户端：另开一个实例 → 点“加入房间” → 地址 `127.0.0.1`、端口留空、填名字 → “确定”。

三个场景都必须保留在 Build Settings 里（`start`、`Host`、`Client`），且顺序不要随意调整——客户端连接后要靠 Netcode 的场景同步加载 `Host` 场景。

## 一机多开

### 方式 A：ParrelSync（推荐）

1. 菜单 `Tools > ParrelSync > Clones Manager`。
2. `Create New Clone`，会在工程旁边生成一个克隆目录。
3. 用 `Open in New Editor` 打开克隆项目。
4. 两个编辑器都点 Play。
5. 一个开房间，一个加入 `127.0.0.1`。

克隆项目和原项目共享 `Assets` / `ProjectSettings` 的源文件（符号链接或同步），但各自有独立的 `Library` 和进程，所以能同时运行两个客户端。

### 方式 B：编辑器 + 构建包

1. 在编辑器里开房间。
2. `File > Build And Run` 出 Windows 包。
3. 运行构建包，加入 `127.0.0.1`。

## 局域网联机

1. 房主侧确认 `Host` 场景的 `UnityTransport` 监听 `0.0.0.0:7777`（默认已配好）。
2. Windows 防火墙放行 Unity 编辑器/构建程序的 **UDP 7777** 入站。
3. 客户端地址填房主的内网 IPv4（`ipconfig` 查看），端口 7777。
4. 如果填域名，客户端会先做 DNS 解析；解析失败会提示“无法解析地址”。

## 构建

1. `File > Build Settings`，确认场景列表：`start`、`Host`、`Client`。
2. 目标平台选 Windows，`Build`。
3. `NetworkManager` 已勾选 `RunInBackground`，窗口失焦后仍会继续联网。
4. 客户端和房主使用**同一份构建**即可，不需要区分的服务器包。

## 调试

### 日志

- 网络日志：`NetworkManager` 的 `EnableNetworkLogs` 已开启，日志级别 Normal。
- AI：开局时服务器会打印 `[AI] 电脑玩家 N 已开始行动`。
- 连接失败：`ClientViewManager` 会在 `tip` 标签和 Console 里给出原因。

### AI Gizmos

运行时在 Scene 视图选中任意电脑坦克：

- 黄色折线：当前 A* 路径。
- 红色线：当前目标。

Gizmos 只在服务器进程里有意义。

### 常用断点位置

| 现象 | 先看 |
| --- | --- |
| 客户端连不上 | `ClientViewManager.Connect()`、`NetworkManager.StartClient()` 返回值 |
| 连上了但大厅没人 | `PlayerListManager.OnNetworkSpawn()`、`SetName_ServerRpc` |
| 开局没地图 | `ConnectionViewManager.startGame_ClientRpc` → `MapGenerator.ResetAndGenerate` |
| 坦克不动 | `TankController.FixedUpdate`、`FixedNetworkTransform` 是否挂上 |
| 炮塔不同步 | `TankController.SyncTurretAngle`、`turretAngle` 的写权限 |
| 电脑不动 | `AiTankController.Initialize` 是否在服务器、`CurrentMap` 是否为空 |
| 击杀/死亡不对 | `TankController.OnCollisionEnter2D`、`PlayerListManager.AddKill` / `AddDeath` |
| 最终存活不对 | `MapGenerator.EndRound` / `AwardSurvivals`、`ResetAndGenerateLater` 的 2 秒延迟 |

## 代码约定

- **编码统一 UTF-8**。之前 `HostController.cs` 是 GBK，注释会显示成乱码；已转成 UTF-8。新增/修改文件请保持 UTF-8。
- **注释用中文**，和现有代码保持一致；公共方法用 `/// <summary>`。
- 脚本没有命名空间，类名要全局唯一。
- **服务器专属逻辑显式判断 `IsServer`**，尤其是生成/销毁 `NetworkObject`、改 `NetworkVariable`、加分。
- RPC 方法名必须以 `_ServerRpc` / `_ClientRpc` 结尾（NGO 的约定）。
- 默认 `[ServerRpc]` 只允许 Owner 调用；非玩家对象（例如 `PlayerListManager`）要加 `RequireOwnership = false`。
- 新增网络预制体时：
  1. 预制体加 `NetworkObject`；
  2. 加入 `DefaultNetworkPrefabs.asset`；
  3. 所有端使用同一份预制体列表（`ForceSamePrefabs` 已开启）。
- `.meta` 文件必须一起提交，不要删除或手工改 GUID；否则场景/预制体引用会丢。
- UI 元素 `name` 用 lowerCamelCase，USS 类名用语义化命名；公共样式放 `TankTheme.uss`。
- `ListView` 的 `makeItem` 只注册一次回调，`bindItem` 负责数据；样式类要显式开关（见 [ui.md](ui.md#listview-的复用约定)）。

## 目录与脚本速查

| 路径 | 内容 |
| --- | --- |
| `Assets/Scenes/start.unity` | 主菜单 |
| `Assets/Scenes/Host.unity` | 房主 / 服务器 / 通用游戏场景 |
| `Assets/Scenes/Client.unity` | 连接表单 |
| `Assets/control/StartScene/` | 主菜单按钮与场景跳转 |
| `Assets/control/ClientConnection/` | 地址表单、DNS、连接 |
| `Assets/control/HostConnectionView/` | 大厅、玩家列表、踢人、加电脑 |
| `Assets/control/ScoreTable/` | 计分板 |
| `Assets/control/Network/` | `HostController`、`ServerAddress` |
| `Assets/control/HostViewManager/` | 顶部栏按钮 |
| `Assets/control/style/` | 公共 USS |
| `Assets/game_obj/tank/` | 坦克控制、预制体、贴图 |
| `Assets/game_obj/bullet/` | 子弹控制器与预制体 |
| `Assets/game_obj/map/` | 地图生成、路障 |
| `Assets/game_obj/ai/` | AI 决策与 A* |
| `Assets/game_obj/CustomComponents/` | `FixedNetworkTransform` |
| `Tools/tank_art/` | 贴图生成脚本 |

更细的模块关系见 [architecture.md](architecture.md)。

## 美术资源

坦克车身和炮塔的贴图由 `Tools/tank_art/gen_tank_art.py` 生成。画布尺寸、朝向、缩放、碰撞体对应的约定都写在 [Tools/tank_art/README.md](../Tools/tank_art/README.md)，改图前先读那份文档。

## 测试

目前没有自动化测试（`com.unity.test-framework` 已安装，但工程里没有测试程序集或用例）。改动后建议按下面的清单手动回归：

- [ ] 主菜单三个按钮都能用。
- [ ] 开房间后，大厅显示 1 名玩家。
- [ ] 第二个实例能连上，双方大厅都出现对方。
- [ ] 添加电脑后，双方列表都出现“电脑 N”。
- [ ] 点“开始”后，双方都看到同一张地图，相机取景正确。
- [ ] 驾驶、炮塔旋转、开火正常。
- [ ] 子弹能反弹，命中后击杀/死亡各 +1、爆炸、坦克消失。
- [ ] 只剩 1 辆坦克时，2 秒后自动重开新地图，幸存者的最终存活 +1。
- [ ] `Esc` 能开关大厅和计分板，且两者不抢按键。
- [ ] 踢真人和踢电脑都生效。
- [ ] 客户端中途退出，服务器列表移除该玩家。
- [ ] 构建包能作为客户端加入编辑器里的房主。

## 常见问题

| 问题 | 排查 |
| --- | --- |
| 客户端点“确定”没反应 | 地址/端口校验是否通过；`StartClient()` 是否返回 false |
| 一直卡在连接界面 | 房主是否已 `StartHost`；防火墙是否放行 UDP 7777；是否填错端口 |
| 连上但看不到大厅 | `EnableSceneManagement` 是否被关掉；`Host` 场景是否在 Build Settings 里 |
| 客户端名字都是“房主” | 客户端没填名字，用了静态默认值 |
| 坦克不动 | 是否是电脑且 AI 未初始化；`FixedNetworkTransform` 是否被删 |
| 炮塔朝向不对 | `turretAngle` 初值应为 270；炮塔预制体旋转是否为 -90° |
| 电脑玩家不动 | 是否在服务器进程；`CurrentMap` 是否已生成；Console 是否有 `[AI]` 日志 |
| 场景引用丢失（Missing Script） | `.meta` 是否被删/改 GUID；脚本类名是否改过 |
| 中文注释乱码 | 文件是否被保存成 GBK；统一用 UTF-8 |
| 构建后连不上 | 防火墙；构建包是否包含三个场景；两端预制体列表是否一致 |

## 仓库约定

- 主分支 `main`；功能开发建议走分支 / worktree。
- 提交信息沿用现有的 Conventional Commits 风格，例如：
  - `feat(lobby): 房主可按需添加电脑玩家`
  - `fix(network): 修复炮塔角度不同步`
  - `docs: 补充网络同步说明`
- 不提交 `Library/`、`Temp/`、`Logs/`、`*.csproj`、`*.sln`、`.mcp.json`、`Assets/Plugins/NuGet/`。
- 大改动前先看 [architecture.md](architecture.md) 和 [network.md](network.md)，避免破坏场景同步和预制体注册。
