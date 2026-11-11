# 界面结构

界面用 **UI Toolkit**（UXML + USS）实现，每个界面都是一个 `UIDocument`，脚本通过 `rootVisualElement.Q<T>("名字")` 拿控件。顶部栏是例外，它用的是旧的 UGUI Canvas。

## 文件一览

| 界面 | UXML | USS | 控制脚本 | 面板设置 |
| --- | --- | --- | --- | --- |
| 主菜单 | `control/StartScene/StartView.uxml` | `StartView.uss` | `StartViewManager` + `StartSceneControler` | `StartView.asset` |
| 客户端连接 | `control/ClientConnection/ClientView.uxml` | `ClientView.uss` | `ClientViewManager` | `ClientView.asset` |
| 房间大厅 | `control/HostConnectionView/ConnectionView.uxml` | `ConnectionView.uss` | `ConnectionViewManager` | `ConnectionPanelSetting.asset` |
| 大厅玩家行 | `ConnectionItem.uxml` | `ConnectionView.uss` | `ClientItemManager` | — |
| 计分板 | `control/ScoreTable/ScoreView.uxml` | `ScoreView.uss` | `ScoreViewManager` | `ScorePanelSetting.asset` |
| 计分行 | `ScoreItem.uxml` | `ScoreView.uss` | `ScoreItemManager` | — |
| 公共主题 | — | `control/style/TankTheme.uss` | — | — |
| 顶部栏 | UGUI（场景里搭的 Canvas） | — | `HostTopCanvasManager` | — |

所有 UXML 都会先引用 `TankTheme.uss`，再引用自己的 USS。公共类名以 `tk-` 开头（`tk-root`、`tk-card`、`tk-title`、`tk-btn`、`tk-btn--primary`、`tk-btn--ghost`、`tk-btn--danger`），界面自己的类名用语义化命名。

## 主菜单

```text
┌──────────────────────────┐
│        坦克动荡          │
│   和朋友来一场坦克对战   │
│       [ 开房间 ]         │
│       [ 加入房间 ]       │
│       [ 退出 ]           │
└──────────────────────────┘
```

- `StartViewManager.Start()` 把按钮接到 `StartSceneControler`：
  - `newRoom` → `SceneManager.LoadScene("Host")`
  - `joinRoom` → `SceneManager.LoadScene("Client")`
  - `quit` → `Application.Quit()`
- 界面脚本只负责绑定，场景跳转集中在 `StartSceneControler`。

### 封面背景

- `StartView.uxml` 里 `startBg` / `startScrim` 是两个 `position: absolute` 的全屏层，排在 `card` 前面，所以卡片始终压在最上面。
- `startBg` 的 `background-image` 指向 `Assets/control/StartScene/start_bg.png`，`-unity-background-scale-mode: scale-and-crop` 让任意 16:9 / 16:10 / 21:9 分辨率都铺满且不变形（多余部分裁掉）。
- 背景图由 `Tools/tank_art/gen_menu_bg.py` 生成：俯视迷宫 + 两辆坦克 + 琥珀色灯，配色和坦克贴图、UI 主按钮一致。
- 想换图时保持文件名不变、直接覆盖 PNG 即可（`.meta` 里的 GUID 不能变，USS 是按 GUID 引用的）。建议尺寸 2048×1280（16:10），更宽的比例也能用。
- `startScrim` 是背景与卡片之间的冷色遮罩（`rgba(8, 14, 24, 0.30)`）；觉得背景太抢眼就调高不透明度，觉得太闷就调低。

## 客户端连接

字段：名字、地址、端口；上方是“附近的房间”列表，底部是提示文字和“确定 / 退出”。

`ClientViewManager` 的行为：

- 端口占位符：Unity 2022.3 的 `TextField` 没有 placeholder，所以 UXML 里在端口输入框内叠了一个 `Label`（`portPlaceholder`），由脚本控制显隐。默认值来自 `UnityTransport.ConnectionData.Port`。
- 实时校验：
  - 地址用 `ServerAddress.TryParse` 校验；
  - 端口用 `ServerAddress.TryParsePort` 校验；
  - 地址为空或端口为空时不报错，只保持默认描边；
  - 填错时输入框四边变红（`ErrorColor ≈ #ED6959`），提示文字显示具体原因。
- 只有名字非空、地址和端口都合法、且不在连接中时，“确定”才可点；名字只有空白也算空，并提示“请先填写你的名字”。连接时会去掉名字首尾空白。
- 地址里自带端口（如 `http://example.com:7788`）时，会自动把端口同步到端口栏，让玩家看到实际使用的端口。
- 点“确定”后：
  1. 解析地址和端口，端口优先级：端口栏 > 地址里的端口 > 默认端口；
  2. 如果不是 IP，后台线程解析 DNS；
  3. 写入 `UnityTransport.ConnectionData`；
  4. `StartClient()`，失败则提示“连接失败”。
- 点“退出”：`Shutdown()` → 销毁 `NetworkManager` → 回 `start` 场景。
- “附近的房间”由 `RoomBrowser` 填充（数据来自局域网组播 / 广播），每 1 秒刷新一次，超过 4 秒没再收到公告的房间会自动消失。点一行会把地址和端口回填到下面的输入框；搜索失败时只显示提示，不影响手动填写。房间行没有单独的 UXML，由脚本在 `makeItem` 里搭出来。

## 房间大厅

```text
┌────────────────────────────────────┐
│ 房间大厅                  3 名玩家 │
│ 把地址分享给朋友，人到齐后开始对局 │
│ 玩家        ID        击杀   踢出  │
│ ────────────────────────────────── │
│ 房主  你    0         0     [踢出] │
│ 电脑 1      电脑       0     [踢出] │
│ 玩家 2      ID: 3      2     [踢出] │
│ 人都到齐了，可以开始               │
│ [添加电脑]  [退出]  [开始]         │
└────────────────────────────────────┘
```

`ConnectionViewManager` 同时是 `NetworkBehaviour`，挂在 `Host` 场景的 `ConnectionView` 上（带 `NetworkObject`），所以它本身会随场景同步到客户端。

- 数据源：`PlayerListManager.PlayerList`。列表变化时 `RefreshPlayerList()` 重建本地副本并 `list.RefreshItems()`。
- 玩家行模板是 `ConnectionItem.uxml`，对应的 `VisualTreeAsset` 在 Inspector 的 `item` 字段里指定。
- “开始”按钮：
  - 只有服务器可点（客户端按钮一直禁用）；
  - 至少 2 名玩家时才启用；
  - 点击后广播 `startGame_ClientRpc`：标记 `gameStarted`、隐藏大厅、服务器清分并重置地图。
- “添加电脑”：
  - 只在“自己是服务器 + 还没开局 + PlayerListManager 已生成”时显示；
  - 点击调用 `PlayerListManager.AddBot()`。
- “踢出”：
  - 只在服务器显示；自己的那一行禁用；
  - 真人：`NetworkManager.DisconnectClient(clientId)`；
  - 电脑：先 Despawn 坦克，再从列表删除。
- 提示文字：
  - 房主：人数不足时“等待其他玩家加入，或点‘添加电脑’开始”；够人时“人都到齐了，可以开始”。
  - 客户端：始终“等待房主开始游戏”。
  - 够人时给 `hint` 加 `lobby-hint--ready` 类。
- 开局前会把主相机移到 `(10000, 0, -10)`，避免玩家透过大厅看到空场景；开局后 `MapGenerator` 的 `ClientRpc` 再把相机放回来。

### ListView 的复用约定

大厅和计分板都用了 `ListView`，元素会被回收复用，所以：

- **点击回调在 `makeItem` 里只注册一次**；当前行的数据在 `bindItem` 里写进 `button.userData`，回调里从 `userData` 取，避免重复绑定导致一次点击触发多次。
- **样式类必须显式开和关**。例如名次高亮不能只 `AddToClassList("rank--1")`，否则复用到第 4 名时还会留着第 1 名的样式。计分板用的是 `EnableInClassList("rank--1", rank == 1)` 这种写法。

## 计分板

```text
┌───────────────────────────────────┐
│ 计分板                            │
│ 先比击杀，再比存活；死亡越少越靠前 │
│ 排名   玩家      击杀  死亡  存活 │
│ ───────────────────────────────── │
│ 1      玩家 2     5     1     2   │
│ 2      房主       3     2     1   │
│ 3      电脑 1     1     3     0   │
│          [ 返回 ]                 │
└───────────────────────────────────┘
```

- 数据来自 `PlayerListManager.PlayerList`；每行展示击杀、死亡、最终存活三项战绩。
- 排序规则：击杀降序 → 最终存活降序 → 死亡升序 → `clientId` 升序。
- 大厅列表只展示击杀数；完整战绩在计分板里看。
- 前三名分别加 `rank--1` / `rank--2` / `rank--3`；自己那一行加 `score-row--local`。
- 打开方式：顶部栏的计分板按钮；点“返回”或按 `Esc` 关闭。
- 打开时刷新排序；关闭时恢复顶部栏。

## 顶部栏（UGUI）

`Host` 场景的 `Canvas` 上有两个按钮（用户列表 / 分数线），在 Inspector 里通过 `OnClick` 绑定到 `HostTopCanvasManager`：

- `onUserButtonClick()`：隐藏顶部栏，调用 `ConnectionViewManager.Show(true)`。
- `onScoreButtonClick()`：隐藏顶部栏，调用 `ScoreViewManager.Show(true)`。

顶部栏和两个面板是互斥的：面板打开时 `TopCanvas.SetActive(false)`，面板关闭时恢复。

## Esc 键归谁管

`Esc` **统一由 `ConnectionViewManager.Update()` 处理**，避免和计分板抢按键：

| 当前状态 | 按 Esc 的结果 |
| --- | --- |
| 计分板打开 | 关闭计分板，恢复顶部栏 |
| 计分板关闭 | 切换大厅的显示 / 隐藏 |
| 大厅打开且已开局 | 关闭大厅（背景压暗，保留战局） |
| 大厅打开且未开局 | 关闭大厅（背景不透明蓝底） |

`ScoreViewManager` 自己不处理 `Esc`，只提供 `Show(bool)`。

## 面板设置

每个 `UIDocument` 在 Inspector 里绑定一个 `PanelSettings` 资源：

- `StartView.asset`
- `ClientView.asset`
- `ConnectionPanelSetting.asset`
- `ScorePanelSetting.asset`

它们决定分辨率、缩放模式和主题。修改 UI 时如果要统一调整缩放，改对应的 PanelSettings，而不是每个 UXML 单独调。

## 改 UI 的常见步骤

1. 新建或修改 UXML / USS（可以用 UI Builder 打开）。
2. 给要操作的控件起唯一的 `name`，脚本里用 `root.Q<Button>("名字")` 取。
3. 如果是列表项，复制一份 item UXML，并在 Inspector 的 `item` 字段里指定。
4. 新加 `UIDocument` 时记得绑定 `PanelSettings`，并把脚本挂在同一个 GameObject 上。
5. 保持 `TankTheme.uss` 在前、界面 USS 在后，公共样式不要复制到各个界面里。
6. 列表项脚本里同时处理“添加样式”和“移除样式”，不要假设元素是新建的。

## 已知注意点

- `ClientViewManager.clientName` 是静态字段，默认值写死为“房主”；客户端不填名字时会显示成“房主”。
- 场景里的按钮事件（顶部栏）是 Inspector 绑定的，重命名方法或挪动脚本会断开绑定，需要重新拖。
- `ConnectionView` 同时带 `NetworkObject` 和 `UIDocument`，增删组件时不要破坏这个组合。
