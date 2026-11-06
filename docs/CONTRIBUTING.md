# CONTRIBUTING — 贡献与评审指南

1. 开发前先查阅 `README.md`、`docs/INDEX.md`、`docs/SRS.md` 与 `docs/ENGINEERING_STANDARDS.md`。
2. 每项改动必须对应明确的 `FR-*` 或 `NFR-*` 需求编号；新功能或架构调整须先更新 `docs/SRS.md` 或新增 `docs/adr/ADR-*.md`。
3. 严禁破坏模块依赖方向（`Web -> Application -> Domain`；`Domain` 零外部 UI/IO 依赖）。
4. 提交前必须通过以下质量门禁：
   - `dotnet format MotionStudio.slnx --verify-no-changes --no-restore`
   - `dotnet build MotionStudio.slnx -c Release --no-restore`
   - `dotnet test MotionStudio.slnx -c Release --no-build`
   - `npm --prefix src/MotionStudio.Renderer run typecheck`
   - `npm --prefix src/MotionStudio.Renderer run lint`
   - `npm --prefix src/MotionStudio.Renderer run test`
   - `npm --prefix src/MotionStudio.Renderer run build`
5. 使用 Conventional Commits 提交并在正文附上 `Refs: FR-xxx`，将真实命令输出更新至 `docs/VERIFICATION.md`。
