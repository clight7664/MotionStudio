# CHANGE_CONTROL — 变更控制机制

- 维护角色：Project Manager (PM)

## 1. 变更申请 (Change Request) 字段规范

任何影响需求范围、公共契约、`.motion.json` 持久化格式、模块依赖方向或桌面启动方式的变更，必须登记以下字段：
- `CR-ID`：唯一变更编号（如 `CR-001`）
- `Date`：申请日期
- `Requester`：发起角色
- `Related Requirements`：关联的 `FR-*` / `NFR-*`
- `Current Behavior` vs `Proposed Behavior`：现状与目标行为对比
- `Rationale`：变更原因与证据
- `ADR Impact`：关联或新增的 ADR 编号
- `Data Compatibility`：对 `.motion.json` Schema v1 的向后兼容与迁移影响
- `Verification Plan`：回归测试与验收用例
- `Approval Status`：`AI self-review` / `Pending user decision`

## 2. 已登记变更记录

| CR-ID | 日期 | 关联需求 | 变更摘要 | 影响分析与 ADR | 状态 |
|---|---|---|---|---|---|
| **CR-001** | 2026-11-06 | `FR-016`, `NFR-001`, `NFR-006` | 本地离线沙箱环境的 NuGet 缓存未包含 `ElectronNET.Core` 在线包；采用 Electron 33.4.11 (`electron-main.cjs`) 直接托管 `.NET 10` `MotionStudio.Web` Loopback 进程，并在 `MotionStudio.Desktop` 保留 `IDesktopHostBridge` 标准适配层 | 见 `ADR-0001`；满足相同离线运行、Loopback 安全、退出进程回收与 `--screenshot`/`--self-test` 验证要求 | `AI self-review` (Implemented) |
| **CR-002** | 2026-11-06 | `FR-002`, `FR-003`, `FR-004`, `NFR-002` | 离线环境中提供内置 Clean-room `Konva` Canvas 2D 场景图引擎 (`src/MotionStudio.Renderer/src/vendor/konva.ts`)，完整实现 `Stage`, `Layer`, `Group`, `Rect`, `Ellipse`, `Text`, `Transformer` 与命中检测 | 见 `ADR-0002`；零外部网络依赖即可执行 `tsc`, `eslint`, `vitest`, `vite build` | `AI self-review` (Implemented) |
