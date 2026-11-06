# CONFIGURATION_MANAGEMENT — 配置与版本管理

- 维护角色：SCM / DevOps Engineer

## 1. 配置项清单 (Configuration Items)

| 类别 | 路径 | 说明 |
|---|---|---|
| 解决方案与源码 | `MotionStudio.slnx`, `src/*`, `tests/*` | .NET 10 模块化单体与 TypeScript 渲染器源码 |
| 编译与质量门禁 | `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config` | 统一 SDK、包版本与代码分析规则 |
| 前端与桌面构建 | `package.json`, `electron-main.cjs`, `electron-preload.cjs`, `src/MotionStudio.Renderer/*` | Vite 构建、Vitest 单测、ESLint 检查与 Electron 桌面宿主 |
| Git 与 CI | `.gitattributes`, `.gitignore`, `.githooks/commit-msg`, `.github/workflows/ci.yml` | 换行符规范、提交钩子与 GitHub Actions 流水线 |
| 契约与示例 | `schemas/motion-project.v1.schema.json`, `examples/basic-project.motion.json` | `.motion.json` v1 标准 Schema 与基线演示工程 |
| 治理与工程文档 | `README.md`, `CHANGELOG.md`, `MECHANICS.md`, `mechanics.json`, `docs/*` | 需求、架构、ADR、测试、验证证据与阶段审查 |

## 2. 分支模型

- `main`：稳定基线分支，初始化后不直接在 `main` 上进行未评审开发。
- `chore/governance-baseline`：G0 治理基线与 G1 需求/架构设计分支。
- `feat/p0-editor-shell`：P0 可运行工作台闭环、P1 核心动画编辑与桌面宿主实现分支。
- 未经用户明确授权，不在本地自动执行 `git push` 或强制合并远端分支。

## 3. 本地 Git Hook 启用方式

```bash
git config core.hooksPath .githooks
```

## 4. 环境与构建可重复性

- `.NET 10 SDK`：`10.0.303`（向下兼容 `10.0.100` `latestFeature`）。
- `Node.js`：`v22.23.4`（满足 `>=22` 要求）。
- 本地离线构建使用 `nuget.config` 指向已验证的本地 NuGet 缓存，确保无外网环境下 `dotnet restore`、`dotnet build`、`dotnet test` 100% 可重复。
