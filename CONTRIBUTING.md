# Contributing to MotionStudio

## Branching & Commit Policy

- All development work happens on feature or spike branches (`feat/*`, `fix/*`, `chore/*`, `spike/*`, `docs/*`). Never commit directly to `main`.
- Commit messages follow **Conventional Commits** (`type(scope): summary`, enforced by `.githooks/commit-msg`), linked to requirement IDs (`FR-*`, `NFR-*`).

## Architecture Boundaries

- `MotionStudio.Domain` — Pure C# Scene Graph (`SceneNode`, `Matrix2D`), keyframe interpolation (`Linear`, `Step`, `EaseIn`, `EaseOut`, `EaseInOut`, `CubicBezier`), and `.motion.json` document primitives. Zero UI/Web/OS dependencies.
- `MotionStudio.Application` — Command history (`100`-step undo/redo), revision-tracked `EditorWorkspaceService`, and layout design token constants.
- `MotionStudio.Infrastructure` — Atomic `.motion.json` persistence, secure SVG sanitization, and `SkiaSharp` (`3.119.0`) headless PNG export & 100-node render benchmark.
- `MotionStudio.Renderer` — Zero-runtime-dependency TypeScript Native HTML5 Canvas 2D interactive viewport (`requestAnimationFrame` + single-commit pointer gesture bridge).
- `MotionStudio.Web` & `MotionStudio.Desktop` — `.NET 10` loopback Blazor/JSON host (`127.0.0.1`) and Electron `33` desktop shell (`1536×1024`).

## Build & Verification

```powershell
dotnet format MotionStudio.slnx --no-restore
dotnet build MotionStudio.slnx -c Release --no-restore
dotnet test MotionStudio.slnx -c Release --no-build
```
