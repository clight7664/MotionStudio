# Changelog

All notable changes to MotionStudio are documented in this file. Format follows Keep a Changelog and Semantic Versioning.

## [Unreleased]

### Added
- **G0 Governance Baseline**: `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.gitattributes`, `.gitignore`, `.githooks/commit-msg`, `.github/workflows/ci.yml`, PR template, and role governance documentation (`NFR-004`, `NFR-006`, `NFR-007`, `NFR-009`).
- **G1 Requirements & Architecture Baseline**: Formal `PRD.md`, `SRS.md`, `ARCHITECTURE.md`, `DESIGN_SYSTEM.md`, `INTERACTION_SPEC.md`, `TEST_PLAN.md`, `REQUIREMENTS_TRACEABILITY.md`, `ADR-0001` through `ADR-0003`, `.motion.json` Schema v1 (`schemas/motion-project.v1.schema.json`), and sample project (`examples/basic-project.motion.json`).
- **P0/P1 Executable Workbench**: `.NET 10` Blazor Workbench (`MotionStudio.Web`), pure C# Scene Graph (`SceneNode`, `Matrix2D`) & deterministic keyframe evaluator (`MotionStudio.Domain`), versioned Command/Revision & Undo/Redo application layer (`MotionStudio.Application`), SkiaSharp `3.119.0` headless PNG rasterizer/benchmark + atomic `.motion.json` persistence & SVG sanitizer (`MotionStudio.Infrastructure`), zero-dependency TypeScript Native HTML5 Canvas 2D renderer (`MotionStudio.Renderer`), Electron 33 desktop loopback host (`MotionStudio.Desktop` / `electron-main.cjs`), design token workbench UI (`#F9FAFC` / `#5148EC`), and automated test suites (`Domain.Tests`, `Application.Tests`, `Integration.Tests`, `E2E`).
