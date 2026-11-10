# 发布新版本

推一个 `v*` 标签，就会自动打包 Windows x64 并创建 GitHub Release。

构建在**本机的 self-hosted runner** 上执行：工程使用 Unity 中国版 `2022.3.34f1c1`，GameCI 官方镜像只有国际版 `2022.3.34f1`，而且中国版许可证无法在 GitHub 托管 runner 上激活。

## 流程

```text
git tag v1.0.0
git push origin v1.0.0
  → GitHub Actions 把任务派发到本机 runner
  → 用本机 Unity 2022.3.34f1c1 批处理构建 Windows x64
  → 压缩成 tank-1.0.0-win64.zip
  → 创建 GitHub Release 并上传附件
```

- 标签名去掉开头的 `v` 后写入 `PlayerSettings.bundleVersion`，例如 `v1.0.0` → `1.0.0`。
- 带 `-` 的标签（如 `v1.0.0-beta.1`）会自动标记为预发布。
- 工作流文件：[`.github/workflows/release.yml`](../.github/workflows/release.yml)；打包入口：[`Assets/Editor/BuildScript.cs`](../Assets/Editor/BuildScript.cs)。

## 一次性准备

### 1. 登记 self-hosted runner

仓库 → **Settings → Actions → Runners → New self-hosted runner** → 选 **Windows x64**，然后按页面给出的命令操作，例如：

```powershell
mkdir actions-runner; cd actions-runner
# 下载并解压页面给出的 runner 压缩包
.\config.cmd --url https://github.com/3297207993/tank --token <页面给出的 token>
```

先用前台 `.\run.cmd` 跑通（页面会显示 Idle），再装成 Windows 服务（管理员 PowerShell）：

```powershell
.\svc.cmd install
.\svc.cmd start
```

### 2. 确认 Unity 路径

工作流默认使用：

```text
C:\soft\unity\save\2022.3.34f1c1\Editor\Unity.exe
```

如果本机路径不同，在 **Settings → Secrets and variables → Actions → Variables** 新建变量 `UNITY_PATH`，填实际的 `Unity.exe` 路径。

### 3. 确认许可证和 Windows 构建模块

- 许可证文件默认是 `C:\ProgramData\Unity\Unity_lic.ulf`（机器级）。工作流会把它复制到工程内的 `Build/`，先启动一次 Unity 用 `-manualLicenseFile` 激活（这一步加载完许可证就会退出），再单独启动一次 Unity 执行构建。这样不依赖 Unity Hub 的登录令牌，也能避开旧版 Hub 授权客户端和编辑器的协议冲突。路径不同时，在 **Settings → Secrets and variables → Actions → Variables** 新建 `UNITY_LICENSE_FILE`。
- 许可证本身要先在 Unity Hub 里激活/续期过（中国版个人版需要定期联网续期）。如果日志出现 `Token not found in cache`、`grace period` 或 `No license activation found`，先用 **unity.cn 账号**打开 Hub 登录并续期。
- Unity 安装时勾选 **Windows Build Support (Mono)**。
- 电脑需要开机且 runner 在线。首次构建会重建 `Library/`，比较慢；之后会复用 runner 工作目录里的 `Library/`，速度正常。

## 发布

```bash
git tag v1.0.0
git push origin v1.0.0
```

也可以打附注标签：

```bash
git tag -a v1.0.0 -m "坦克动荡 v1.0.0"
git push origin v1.0.0
```

然后在仓库的 **Releases** 页面就能看到自动生成的版本、Release Notes 和 `tank-1.0.0-win64.zip`。

## 手动测试

**Actions → 发布新版本 → Run workflow**，可以填一个测试版本号（留空则自动生成 `v0.0.0-manual.<run_number>`）。手动运行只上传 Actions artifact，不创建 Release，适合验证 runner、Unity 路径和构建脚本。

## 排错

| 现象 | 检查 |
| --- | --- |
| 任务一直排队 | runner 是否在线（Settings → Actions → Runners） |
| 找不到 Unity | 仓库变量 `UNITY_PATH` 或默认路径是否正确 |
| 没有许可证 / `Token not found in cache` | 检查 `UNITY_LICENSE_FILE` 是否存在；在 Hub 里用 unity.cn 账号登录并续期 |
| `Unsupported protocol version`（505） | 旧版 Hub 授权客户端与编辑器不兼容；工作流已用 `-manualLicenseFile` 绕开，仍失败可更新 Unity Hub |
| 构建失败 | 看日志里 `[Build]` 开头的行；确认 Build Settings 里的三个场景都启用 |
| 产物缺失 | `Build/Windows/tank.exe` 是否生成；日志末尾的 `[Build]` 结果 |
| 启动阶段就退出、授权没加载完 | 不要加 `-nographics`；本工程在无图形模式下会在启动阶段退出 |
| 版本号报错 | 标签只用 `v1.2.3` / `v1.2.3-beta.1` 这类字符（字母、数字、`.`、`_`、`-`） |

## 安全注意

self-hosted runner 会执行仓库 workflow 里的命令，因此：

- 只把这个 runner 给当前仓库使用；
- 工作流只保留 `push` 标签和手动触发，不要添加 `pull_request` 触发（尤其是 fork 的 PR）；
- 不要把 runner 的访问权限分享给不受信任的贡献者。
