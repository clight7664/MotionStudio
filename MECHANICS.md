# MotionStudio — Numerical Specifications & Mechanics Sheet

All parameters governing the MotionStudio 2D animation workbench layout, composition boundaries, timeline evaluation, SkiaSharp rendering, interaction thresholds, and persistence rules are documented below with their authoritative source.

## 1. Composition & Timeline Parameters (`composition`, `timeline`)

| Parameter | Value | Unit | Source |
|---|---|---|---|
| `default composition width` | `1920` | `px` | `UI_IMPLEMENTATION_SPEC.md` / `docs/SRS.md` FR-001 |
| `default composition height` | `1080` | `px` | `UI_IMPLEMENTATION_SPEC.md` / `docs/SRS.md` FR-001 |
| `scene 01 fps` | `60` | `fps` | `UI_IMPLEMENTATION_SPEC.md` Brand Intro / Scene 01 |
| `scene 01 duration frames` | `240` | `frames` | `ProjectDocument.CreateDefaultDemo()` |
| `empty project default fps` | `30` | `fps` | `docs/SRS.md` FR-001 |
| `empty project duration frames` | `90` | `frames` | `docs/SRS.md` FR-001 |
| `min composition dimension` | `1` | `px` | `schemas/motion-project.v1.schema.json` |
| `max composition dimension` | `16384` | `px` | `schemas/motion-project.v1.schema.json` |
| `max duration frames` | `216000` | `frames` | `schemas/motion-project.v1.schema.json` |

## 2. Workbench Layout & Design Tokens (`layout`, `theme`)

| Parameter | Value | Unit | Source |
|---|---|---|---|
| `desktop window width` | `1536` | `px` | `UI_IMPLEMENTATION_SPEC.md` |
| `desktop window height` | `1024` | `px` | `UI_IMPLEMENTATION_SPEC.md` |
| `min desktop width` | `1280` | `px` | `UI_IMPLEMENTATION_SPEC.md` |
| `min desktop height` | `800` | `px` | `UI_IMPLEMENTATION_SPEC.md` |
| `header height` | `44` | `px` | `MotionStudio_design_tokens.txt` |
| `toolbar height` | `50` | `px` | `MotionStudio_design_tokens.txt` |
| `tool rail width` | `60` | `px` | `MotionStudio_design_tokens.txt` |
| `left panel width` | `270` | `px` | `MotionStudio_design_tokens.txt` |
| `inspector width` | `320` | `px` | `MotionStudio_design_tokens.txt` |
| `timeline height` | `330` | `px` | `MotionStudio_design_tokens.txt` |
| `status height` | `28` | `px` | `MotionStudio_design_tokens.txt` |
| `layer row height` | `36` | `px` | `UI_IMPLEMENTATION_SPEC.md` |
| `control border radius` | `4` | `px` | `UI_IMPLEMENTATION_SPEC.md` |
| `color background` | `#F9FAFC` | `hex` | `MotionStudio_design_tokens.txt` |
| `color surface` | `#FFFFFF` | `hex` | `MotionStudio_design_tokens.txt` |
| `color canvas` | `#F3F4F8` | `hex` | `MotionStudio_design_tokens.txt` |
| `color border` | `#E4E7EE` | `hex` | `MotionStudio_design_tokens.txt` |
| `color text` | `#1B2130` | `hex` | `MotionStudio_design_tokens.txt` |
| `color muted` | `#677084` | `hex` | `MotionStudio_design_tokens.txt` |
| `color accent` | `#5148EC` | `hex` | `MotionStudio_design_tokens.txt` |
| `color selected` | `#EEECFF` | `hex` | `MotionStudio_design_tokens.txt` |

## 3. Canvas Viewport, SkiaSharp Benchmark, History & Persistence (`interaction`, `persistence`)

| Parameter | Value | Unit | Source |
|---|---|---|---|
| `default viewport zoom` | `0.75` | `ratio` | `EditorWorkspaceService` (75% fit) |
| `min canvas zoom` | `0.1` | `ratio` | `docs/SRS.md` FR-002 |
| `max canvas zoom` | `8.0` | `ratio` | `docs/SRS.md` FR-002 |
| `max undo history depth` | `100` | `steps` | `docs/SRS.md` FR-011 |
| `skia benchmark node count` | `100` | `nodes` | `StaticExporterService.BenchmarkHundredNodeRender` |
| `paste offset delta` | `20` | `px` | `docs/INTERACTION_SPEC.md` FR-012 |
| `schema version` | `1` | `version` | `schemas/motion-project.v1.schema.json` |
| `commits per drag gesture` | `1` | `count` | `docs/SRS.md` NFR-005 / TC-006 |
