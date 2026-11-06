# ADR-0004: SkiaSharp Authoritative Renderer & Native HTML5 Canvas 2D Client Bridge

- **Status**: Accepted
- **Date**: 2026-11-06

## Context

MotionStudio requires:
1. Deterministic C# Scene Graph (`SceneNode`, `Matrix2D`) and `SkiaSharp` rendering (`3.119.0`) for frame accurate rasterization, PNG export, and 100-node performance benchmarking.
2. Zero per-frame SignalR round-trips during 60 FPS interactive canvas dragging and timeline playback inside Blazor Interactive Server, without relying on third-party JS canvas frameworks in runtime bundles.

## Decision

1. **Authoritative C# Scene Graph & SkiaSharp Renderer**:
   - `MotionStudio.Domain.Scene` defines `Matrix2D`, `SceneBounds`, `SceneNode`, `GroupNode`, `RectangleNode`, `EllipseNode`, `PathNode`, `TextNode`, `ImageNode`, and `SceneGraphBuilder`.
   - `MotionStudio.Infrastructure.Export.StaticExporterService` traverses the evaluated `GroupNode` hierarchy into `SkiaSharp` (`SKSurface`, `SKCanvas`, `SKPaint`) for lossless PNG export (`/api/export/png`) and 100-node headless render benchmarking.
2. **Zero-Dependency Browser HTML5 Canvas 2D Bridge**:
   - `MotionStudio.Renderer` compiles pure TypeScript into `wwwroot/renderer/motionstudio-renderer.js` with zero third-party runtime dependencies.
   - Pointer `down/move` gestures and `requestAnimationFrame` playback evaluate locally on HTML5 Canvas 2D and commit a single atomic `OnRendererPointerCommitAsync` command on `pointerup` with optimistic revision checking (`BaseRevision`).
