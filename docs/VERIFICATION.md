# VERIFICATION — 执行证据台账

> 本文件记录真实执行的构建、静态检查、单元测试、集成测试、E2E 与桌面宿主验证命令及结果。P0/P1 代码提交后将写入实际执行结果。

| Date | Environment | Branch / Commit | Command / Test | Expected | Actual | Status | Evidence |
|---|---|---|---|---|---|---|---|
| 2026-11-06 | Windows 10 x64 / .NET 10.0.303 / Node v22.23.4 | `chore/governance-baseline` (`48e57d3`) | `git --version; dotnet --version; node --version; npm --version` | Tools available | `git 2.46.0`, `dotnet 10.0.303`, `node v22.23.4`, `npm 10.9.9` | **PASS** | G0 Environment Discovery |
| TBD | Windows 10 x64 | `feat/p0-editor-shell` | `dotnet format MotionStudio.slnx --verify-no-changes --no-restore` | Exit 0 | Pending P0 build | NOT RUN | TBD |
| TBD | Windows 10 x64 | `feat/p0-editor-shell` | `dotnet build MotionStudio.slnx -c Release --no-restore` | Exit 0, 0 Warnings | Pending P0 build | NOT RUN | TBD |
| TBD | Windows 10 x64 | `feat/p0-editor-shell` | `dotnet test MotionStudio.slnx -c Release --no-build` | All xUnit tests pass | Pending P0 build | NOT RUN | TBD |
| TBD | Windows 10 x64 | `feat/p0-editor-shell` | `npm --prefix src/MotionStudio.Renderer run typecheck && lint && test && build` | Exit 0 | Pending P0 build | NOT RUN | TBD |
| TBD | Windows 10 x64 | `feat/p0-editor-shell` | Electron Desktop `--self-test` & `--screenshot` (`rebuild-check`) | All checks pass | Pending P0 build | NOT RUN | TBD |
