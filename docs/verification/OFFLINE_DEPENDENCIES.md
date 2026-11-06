# MotionStudio — Offline Dependency & Runtime Verification Matrix

## 1. Environment Audit

- **OS**: Windows (x64)
- **SDK**: `.NET SDK 10.0.105` (`global.json` pinned with `rollForward: latestFeature`)
- **Offline NuGet Source**: `C:\Users\17289\.nuget\packages` (`nuget.config` clears external network feeds)
- **Desktop Runtime**: Electron `33.4.11` (`node_modules/electron/dist/electron-real.exe`, Chromium + Node `v20.18.3`)

## 2. Verification Results

| Component / Package | Version | Offline Cache Status | Verification Verdict | Notes |
|---|---|---|---|---|
| `Microsoft.NET.Sdk.Web` (Blazor SSR + Loopback JSON Host) | `10.0.105` | Present (`Internal.Assets` absent) | **PASS** | Compiles and runs cleanly with `<RequiresAspNetWebAssets>false</RequiresAspNetWebAssets>`; initial UI renders via Blazor SSR and bridges interactive state over `/api/workspace/*` + native HTML5 Canvas 2D. |
| `SkiaSharp` | `3.119.0` | Present | **PASS** | Renders `SceneNode` hierarchy (`GroupNode`, `RectangleNode`, `EllipseNode`, `PathNode`, `TextNode`, `ImageNode`) via `SKSurface` / `SKCanvas` and encodes PNG snapshots; 100-node headless benchmark verified. |
| `SkiaSharp.NativeAssets.Win32` | `3.119.0` | Present | **PASS** | Native `libSkiaSharp.dll` loads cleanly on Windows x64 in both xUnit and Electron loopback server. |
| `xunit` + `Microsoft.NET.Test.Sdk` | `2.9.2` / `17.11.1` | Present | **PASS** | All Domain, Application, Integration, and E2E suites execute offline. |
| Native Electron `33.4.11` Loopback Host | `33.4.11` | Present | **PASS** | Spawns `MotionStudio.Web.dll` on an ephemeral `127.0.0.1` port with `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`, and `1536×1024` viewport. |
| `Microsoft.AspNetCore.Components.WebAssembly` (`.NET 10`) | `10.0.x` | Missing (only `6.0.36` cached) | **BLOCKED (Offline)** | Documented per clean-room offline policy; interactive 60 FPS canvas and timeline evaluation run in TypeScript over native Browser HTML5 Canvas 2D without SignalR per-frame round-trips. |
| `ElectronNET.Core` NuGet wrapper | `0.6.0` | Missing in offline cache | **BLOCKED (Offline)** | Replaced cleanly by direct `electron-main.cjs` + `ElectronDesktopHostBridge` loopback process supervision (`ADR-0003`). |
