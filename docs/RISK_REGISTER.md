# RISK_REGISTER — 项目风险登记册

- 维护角色：Project Manager / System Architect / Security Engineer

| ID | 风险描述 | 等级 | 责任角色 | 缓解与验证策略 | 当前状态与实测结论 |
|---|---|---|---|---|---|
| **R-001** | `ElectronNET.Core` / `ElectronNET.Core.AspNet` 与 .NET 10 Interactive Server 离线环境兼容性未知 | High | Architect / Desktop | 在 G0/P0 检查本地 NuGet 缓存与离线构建能力；采用标准 `IDesktopHostBridge` 抽象 + 本地 Electron 33 宿主进程启动 `.NET 10` Loopback Kestrel (`127.0.0.1`) 进行实测（见 `ADR-0001`） | **Mitigated (Spike Verified)**：本地离线 NuGet 缓存未预置 `ElectronNET.Core` 包；已通过 Electron 33.4.11 + `.NET 10` Blazor Interactive Server Loopback Host 实现真实桌面封装与离线运行 |
| **R-002** | 画布拖拽与播放期间高频调用 Blazor/SignalR 导致延迟与卡顿 | High | TS Renderer / Architect | 严格隔离高频交互：Renderer 在本地维护 `requestAnimationFrame` 播放采样与拖拽预览状态，仅在 `pointerup` 时向 C# 提交单条语义命令（含 `baseRevision`） | **Mitigated (Verified)**：`TC-006` 实测 100 次 `pointermove` + 1 次 `pointerup` 仅产生 1 次命令提交 |
| **R-003** | 工程文件保存中断或加载损坏 JSON 导致当前工程丢失 | High | Infrastructure / QA | 采用 Schema v1 校验、同目录 `.tmp` 临时文件写入 + 原子替换 + `.bak` 备份；解析失败时拒绝替换内存中的有效工程 | **Mitigated (Verified)**：`TC-003` 集成测试覆盖原子保存、备份恢复与损坏文件防护 |
| **R-004** | 复杂工作台退化为无功能的静态原型或空壳按钮 | High | PO / UX / QA | 所有按钮关联真实命令或明确标识状态；通过 xUnit + E2E 自检验证新建、关键帧编辑、插值、撤销重做、保存再打开全链路 | **Mitigated (Verified)**：全链路自动化测试与 `--self-test` 验证通过 |
| **R-005** | 桌面模式本地端口暴露、外部链接跳转或 Node 权限泄露 | High | Security / Desktop | Kestrel 仅绑定 `127.0.0.1` 随机可用端口；Electron 启用 `contextIsolation: true`、`nodeIntegration: false`；拦截非 loopback 导航与新窗口 | **Mitigated (Verified)**：见 `electron-main.cjs`、`electron-preload.cjs` 与 `docs/SECURITY.md` |
| **R-006** | 竞品参考引入版权、商标或离线未缓存 npm 依赖风险 | Medium | PM / UX / Renderer | 严格遵循 Clean-room 原则，全部 UI 样式、图标与示例工程均为原创；针对离线环境未缓存外部 `konva` npm 包，提供类型严格兼容的内置 Canvas 2D Konva 场景图模块（见 `ADR-0002`） | **Mitigated (Verified)**：无第三方品牌资产；离线构建与测试 100% 可重复 |
