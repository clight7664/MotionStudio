# PROJECT_PLAN — 阶段计划与交付门禁

- 文档版本：0.2.0
- 维护角色：Project Manager (PM)
- 评审标记：`AI self-review`（正式里程碑签字属于 `Pending user decision`）

## 1. 阶段分解与状态门禁

| 阶段 | 负责人角色 | 核心任务与交付物 | 阶段验收条件 (Gate) | 当前状态 |
|---|---|---|---|---|
| **G0 治理基线** | PM + SCM + Tech Lead | Git baseline (`main` -> `chore/governance-baseline`)、`.editorconfig`、`Directory.Build.props`、`Directory.Packages.props`、`global.json`、`.githooks/commit-msg`、CI workflow、治理文档与 `G0-REVIEW.md` | Git 结构有效、hook 可执行、配置语法正确、文档链接有效 | Implemented & Verified |
| **G1 需求与架构** | PO + Architect + UX + QA | `PRD.md`、`SRS.md`、`DESIGN_SYSTEM.md`、`INTERACTION_SPEC.md`、`ARCHITECTURE.md`、`ADR-0001..0003`、`TEST_PLAN.md`、`REQUIREMENTS_TRACEABILITY.md`、`.motion.json` Schema v1、`G1-REVIEW.md` | 每个 P0/P1 需求具备唯一 ID、GWT 验收标准、模块归属与测试用例映射 | Implemented & Verified |
| **P0 可运行闭环** | Tech Lead + C# Dev + TS Dev + Desktop + QA | `MotionStudio.slnx`、Blazor Interactive Server 工作台、TypeScript/Konva Canvas 2D 渲染器、0-3s 30fps 演示工程、Save/Open 闭环、Electron 桌面 Spike、`P0-REVIEW.md` | `dotnet build/test` 通过、Renderer `typecheck/lint/test/build` 通过、E2E 闭环与桌面宿主验证有真实记录 | Implemented & Verified |
| **P1 核心编辑** | C# Dev + TS Dev + QA | 多属性关键帧 CRUD、Linear/Ease/CubicBezier 曲线、Undo/Redo 10 步回退、图层顺序/锁定/显隐/分组、Copy/Paste、原子写入与备份恢复 | 命令链路可追溯、Save/Open round-trip 单测与集成测试通过、Undo/Redo 不受播放头污染 | Implemented & Verified |
| **P2 工程化增强** | Security + C# Dev + QA | SVG 安全净化导入 (`FR-017`)、静态 PNG/SVG 导出 (`FR-018`)、动效预设 (`FR-019`)、性能基线测试 | 安全测试拒绝危险 SVG、静态导出与预设可逆且通过验证 | Implemented & Verified |
| **P3 系统交付** | PM + QA + DevOps + Writer | 离线启动验证、退出进程清理、追溯矩阵闭环、CHANGELOG 与已知限制说明 | 无未处置 Blocker、全部交付可追溯至 Git Commit | In Progress / Pending User Sign-off |

## 2. 迭代与问题管理规则

- 每个开发工作项必须关联唯一需求 ID（`FR-001`..`FR-020`、`NFR-001`..`NFR-009`）。
- 需求状态流转：`Draft -> Approved for Implementation -> Implemented -> Verified -> Released`。代理内部评审标记为 `AI self-review`，不冒充人工审批。
- 每次提交遵循 Conventional Commits，并在 commit 正文包含 `Refs: FR-xxx`。

## 3. Definition of Ready (DoR)
1. 需求具备唯一 ID、明确输入/输出边界条件与 Given-When-Then 验收准则。
2. 架构归属明确（Domain / Application / Infrastructure / Web / Renderer / Desktop），不违反单向依赖。
3. 重大技术未知项已在 `RISK_REGISTER.md` 登记并制定 Spike 验证方案。

## 4. Definition of Done (DoD)
1. 代码通过 `.editorconfig` 格式化检查、`TreatWarningsAsErrors=true` 编译与 TypeScript `strict: true` 检查。
2. 对应单元测试、集成测试与端到端自检实际执行并记录真实输出到 `docs/VERIFICATION.md`。
3. 架构决策（ADR）与双向追溯矩阵（`docs/REQUIREMENTS_TRACEABILITY.md`）同步更新。
