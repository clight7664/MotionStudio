# TEST_PLAN — 验证与质量保证计划

- **文档版本**：`0.2.0`
- **维护角色**：QA Engineer

## 1. 分层测试矩阵

| 测试层次 | 测试工程 / 目录 | 覆盖范围 | 质量门禁要求 |
|---|---|---|---|
| **静态与格式检查** | Solution & `MotionStudio.Renderer` | `dotnet format --verify-no-changes`, C# `TreatWarningsAsErrors`, TS `tsc --noEmit`, `eslint` | Exit Code 0，0 Warning，0 Error |
| **领域单元测试** | `tests/MotionStudio.Domain.Tests` | `TC-001` 工程参数边界校验、`TC-002` 关键帧插值 Golden Vectors、同帧唯一覆盖、Easing 曲线、NaN/Infinity 防护 | 全部通过 |
| **应用层单元测试** | `tests/MotionStudio.Application.Tests` | `TC-004` 10 步 Undo/Redo 可逆性、播放头不污染 Undo 栈、`BaseRevision` 冲突拦截、`TC-006` 拖拽单次提交、复制粘贴、编组与预设 | 全部通过 |
| **基础设施集成测试** | `tests/MotionStudio.Integration.Tests` | `TC-003` `.motion.json` Save/Open round-trip、损坏文件拒绝且保留原工程、`.bak` 备份恢复、`FR-017` SVG 净化拦截、静态 SVG/PNG 导出 | 全部通过 |
| **前端渲染器单测** | `src/MotionStudio.Renderer/tests` | TS 插值引擎与 C# Golden Vector 对齐、Konva 场景图创建与命中、`InteractionController` 100 次 `pointermove` 仅 1 次 `pointerup` 提交 | `vitest run` 全部通过 |
| **端到端与桌面验证** | `tests/MotionStudio.E2E` & Electron `--self-test` / `--screenshot` | `TC-005` 真实启动 `.NET 10` Blazor Host + Electron 33 窗口、执行完整编辑闭环、生成真实窗口截图与自检报告 | 全部通过 |

## 2. 核心验收测试用例定义

- **TC-001 (`FR-001`)**：新建 `1920×1080, 30fps, 90 frames` 工程成功；输入 `0 fps`、`-100 width`、`NaN` 时抛出验证失败并保留原状态。
- **TC-002 (`FR-008`, `FR-009`)**：图层 `Shape 1` 在帧 0 设 `X=0`，在帧 30 设 `X=300` (`linear`)，在帧 15 采样精确等于 `150`；C# 与 TS 双端结果完全一致。
- **TC-003 (`FR-013`, `NFR-003`)**：保存工程至 `.motion.json` 后重新打开，`projectId`, `composition`, `layers`, `tracks` 完全一致；加载非法 JSON 时不覆盖当前工程。
- **TC-004 (`FR-011`)**：连续执行 10 步编辑命令后执行 10 次 Undo 再执行 10 次 Redo，中间穿插播放头 Seek，状态与修订号完全吻合。
- **TC-005 (`FR-016`, `NFR-001`)**：在离线环境通过 Electron 启动本地 `.NET 10` Host，完成页面加载、核心交互自检 (`--self-test`)、窗口截图 (`--screenshot`) 并在退出时回收子进程。
- **TC-006 (`NFR-005`)**：模拟 100 次连续 `pointermove` + 1 次 `pointerup`，断言服务端命令提交次数严格等于 `1`。
