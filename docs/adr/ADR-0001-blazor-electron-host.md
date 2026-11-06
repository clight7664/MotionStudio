# ADR-0001 — Blazor Interactive Server 与 Electron 桌面宿主适配策略

- **Status**: `Accepted (Verified via P0 Spike)`
- **Date**: `2026-11-06`
- **Related Requirements**: `FR-016`, `NFR-001`, `NFR-006`
- **Related Risks**: `R-001`, `R-005`

## 1. Context

项目要求同时支持：
1. 普通浏览器直接访问的 `.NET 10` ASP.NET Core Blazor Web App (`Interactive Server` 模式)；
2. Windows 桌面离线封装，本地启动 ASP.NET Core Loopback 服务，禁用不必要的 Node 集成并启用 `contextIsolation`。

在 G0 环境探查中，我们检验了当前离线沙箱构建环境：
- 已安装 `.NET SDK 10.0.303` 与 `Microsoft.AspNetCore.App 10.0.11`；
- 已安装 `Node.js v22.23.4` 与 `Electron 33.4.11`；
- 本地 NuGet 缓存 (`C:\Users\17289\.nuget\packages`) 未预缓存外部 `ElectronNET.Core` / `ElectronNET.Core.AspNet` NuGet 包，且沙箱构建环境禁止访问公网 NuGet 源。

## 2. Decision

1. **抽象桌面桥接契约 (`IDesktopHostBridge`)**：在 `MotionStudio.Application` 定义 `IDesktopHostBridge`，在 `MotionStudio.Desktop` 提供 `ElectronDesktopHostBridge` 实现，负责管理 Loopback 绑定 (`127.0.0.1`)、一次性会话令牌 (`MOTIONSTUDIO_DESKTOP_TOKEN`)、安全路径约束与桌面环境检测，使 `MotionStudio.Web` 与 `MotionStudio.Domain` 不直接耦合任何特定 Electron NuGet 包版本。
2. **原生 Electron 33 + .NET 10 Loopback 宿主 (`electron-main.cjs` + `electron-preload.cjs`)**：
   - Electron 主进程在启动时自动探测空闲的 `127.0.0.1` 端口，生成随机 `MOTIONSTUDIO_DESKTOP_TOKEN`，拉起编译后的 `MotionStudio.Web.dll` 子进程；
   - 轮询 `/api/health` 就绪后，创建 `BrowserWindow`（配置 `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`, `backgroundThrottling: false`）加载本地 Blazor Interactive Server 工作台；
   - 拦截所有非 `http://127.0.0.1:<port>` 的 `will-navigate` 与 `setWindowOpenHandler` 请求；
   - 在窗口关闭、`before-quit` 或自动化 `--screenshot` / `--self-test` 结束时，干净终止 `.NET` 子进程，杜绝孤儿进程残留。

## 3. Consequences

- **优势**：100% 满足 `.NET 10` Blazor Interactive Server + SignalR + Electron 33 离线桌面运行、Loopback 安全隔离、退出清理及自动化截图/自检要求，且不依赖外网 NuGet 下载。
- **可演进性**：当在具备外网或私有制品库的环境中需要切换至 `ElectronNET.Core.AspNet` 打包流水线时，仅需在 `MotionStudio.Desktop` 内部替换启动器，无需修改 Domain/Application/Web 业务代码。
