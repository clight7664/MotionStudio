# VERIFICATION — 执行证据台账

> 本文件记录真实执行的构建、静态检查、单元测试、集成测试、E2E 与桌面宿主验证命令及结果。

| Date | Environment | Branch / Commit | Command / Test | Expected | Actual | Status | Evidence |
|---|---|---|---|---|---|---|---|
| 2026-11-06 | Windows 10 x64 / .NET 10.0.303 | `chore/governance-baseline` (`48e57d3`) | `git --version; dotnet --version` | Tools available | `git 2.46.0`, `dotnet 10.0.303`, Electron `33.4.11` | **PASS** | G0 Environment Discovery |
| 2026-11-06 | Windows 10 x64 / .NET 10.0.303 | `feat/p0-editor-shell` (`ddb54d0`) | `dotnet format MotionStudio.slnx --no-restore` | Exit 0 | Formatted cleanly, Exit 0 | **PASS** | Format Gate |
| 2026-11-06 | Windows 10 x64 / .NET 10.0.303 | `feat/p0-editor-shell` (`ddb54d0`) | `dotnet build MotionStudio.slnx -c Release --no-restore` | Exit 0, 0 Warnings | `0 个警告，0 个错误` | **PASS** | Release Build Gate |
| 2026-11-06 | Windows 10 x64 / .NET 10.0.303 | `feat/p0-editor-shell` (`ddb54d0`) | `dotnet test MotionStudio.slnx -c Release --no-build` | All xUnit tests pass | `17/17` passed (`Domain`: 6, `Application`: 3, `Integration`: 6, `E2E`: 2) | **PASS** | Automated Test Gate |
| 2026-11-06 | Windows 10 x64 / TypeScript 5.8.2 | `feat/p0-editor-shell` (`ddb54d0`) | `lint-check.mjs && tsc && build-bundle.mjs && renderer.test.js` | Exit 0 | `8` TS files 0 warnings/errors, `4/4` renderer tests pass, bundle `61274` bytes | **PASS** | Renderer Gate |
| 2026-11-06 | Windows 10 x64 / Electron 33.4.11 | `feat/p0-editor-shell` (`ebab6ca`) | `craft-anything-cli.exe rebuild-check` (`--self-test` & `--screenshot`) | All 4 gates pass | `build: passed`, `app-screenshot: passed`, `game-self-test: passed` (`move`, `act`, `hud`, SkiaSharp PNG `15764B`, 100-node benchmark `eval=0.36ms, render=73.64ms`), `mechanics: passed` (`38/38`) | **PASS** | Desktop & E2E Gate |
