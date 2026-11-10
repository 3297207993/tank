# 坦克动荡

一个基于 Unity 的 2D 多人坦克对战游戏。房主开房间，朋友填入地址和端口加入；房主也可以添加由服务器控制的电脑玩家。坦克在随机生成的回廊地图里行驶、旋转炮塔、发射会反弹的炮弹。

## 技术栈

| 项 | 版本 / 说明 |
| --- | --- |
| Unity | 2022.3.34f1c1 |
| 网络 | Netcode for GameObjects 1.15.0 + UnityTransport（UTP 1.x） |
| 网络拓扑 | 客户端-服务器；房主同时是服务器和本地客户端 |
| UI | UI Toolkit（UXML / USS）：主菜单、大厅、计分板 |
| 物理 | 2D 物理：Rigidbody2D、Collider2D、反弹物理材质 |
| 多开调试 | ParrelSync（已放在 `Assets/ParrelSync`） |
| 美术工具 | Python + Pillow（`Tools/tank_art`） |

## 快速开始

1. 用 Unity Hub 打开本目录，编辑器版本选 **2022.3.34f1c1**。
2. 打开 `Assets/Scenes/start.unity`，点击 Play。
3. 点 **开房间**：进入 `Host` 场景，自动成为房主。
4. 再开一个编辑器实例。推荐用 ParrelSync：`Tools > ParrelSync > Clones Manager > Create New Clone`，在克隆出来的项目里也点 Play。
5. 第二个实例点 **加入房间**，地址填 `127.0.0.1`，端口留空（默认 7777），填名字，点 **确定**。
6. 房主在大厅里可以点 **添加电脑**；至少 2 名玩家后点 **开始**。

> 局域网联机时，客户端把 `127.0.0.1` 换成房主的内网 IP，并确保防火墙放行 UDP 7777。主机的监听地址在 `Host` 场景的 `NetworkManager > UnityTransport` 里配置。

## 操作

| 按键 | 作用 |
| --- | --- |
| `W` / `↑` | 前进 |
| `S` / `↓` | 后退 |
| `A` / `←` | 车身左转 |
| `D` / `→` | 车身右转 |
| `,` / `.` | 炮塔左转 / 右转 |
| `空格` | 开火（初始 5 发弹药） |
| `Esc` | 打开 / 关闭大厅或计分板 |

## 目录结构

```text
Assets/
  Scenes/                  start、Host、Client 三个场景
  control/                 入口、连接、大厅、计分板（UI Toolkit + 控制逻辑）
    Network/               房主启动、服务器地址解析
    StartScene/            主菜单
    HostConnectionView/    大厅：玩家列表、踢人、添加电脑、开始
    ClientConnection/      客户端连接表单
    ScoreTable/            计分板
    HostViewManager/       游戏内顶部按钮（UGUI）
    style/                 公共 USS 主题
  game_obj/
    tank/                  坦克控制、预制体、贴图
    bullet/                子弹控制器与预制体
    map/                   地图生成、路障预制体
    ai/                    电脑玩家决策与 A* 导航
    CustomComponents/      FixedNetworkTransform（自研同步组件）
    common/                通用小脚本
  ParrelSync/              多开调试工具
Tools/tank_art/            坦克贴图生成脚本（Python）
docs/                      项目文档
```

## 玩法一句话

击中别人记 1 次击杀，被击毁记 1 次死亡，每局活到切图前一刻的坦克记 1 次最终存活；子弹碰到墙会反弹，30 秒后消失并返还 1 发弹药；场上只剩 1 辆坦克时，2 秒后重开一局并随机生成新地图。

## 文档索引

- [架构总览](docs/architecture.md)
- [网络与同步](docs/network.md)
- [玩法与规则](docs/gameplay.md)
- [地图生成](docs/map-generation.md)
- [电脑玩家 AI](docs/ai.md)
- [界面结构](docs/ui.md)
- [开发与调试](docs/development.md)
- [坦克贴图工具](Tools/tank_art/README.md)

## 已知限制

- 没有房间列表或自动发现：客户端必须知道房主的地址和端口。
- 没有断线重连；房主退出会直接结束对局。
- 没有自动化测试（Test Framework 已安装，但项目里还没有测试用例）。
- 移动采用“客户端本地先应用、再上报服务器转发”的方式，不是严格服务器权威的反作弊移动。
