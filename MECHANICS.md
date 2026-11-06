# MotionStudio — Numerical Specifications & Mechanics Sheet

All parameters governing the MotionStudio 2D animation workbench layout, composition boundaries, timeline evaluation, interaction thresholds, and persistence rules are documented below with their authoritative source.

## 1. Composition & Timeline Parameters (`composition`, `timeline`)

| Parameter | Value | Unit | Source |
|---|---|---|---|
| `default composition width` | `1920` | `px` | `docs/SRS.md` FR-001 / `examples/basic-project.motion.json` |
| `default composition height` | `1080` | `px` | `docs/SRS.md` FR-001 / `examples/basic-project.motion.json` |
| `default fps` | `30` | `fps` | `docs/SRS.md` FR-001 / `docs/PRD.md` §5 |
| `default duration frames` | `90` | `frames` | `docs/SRS.md` FR-001 (3.0s at 30fps) |
| `min composition dimension` | `1` | `px` | `schemas/motion-project.v1.schema.json` |
| `max composition dimension` | `16384` | `px` | `schemas/motion-project.v1.schema.json` |
| `max duration frames` | `216000` | `frames` | `schemas/motion-project.v1.schema.json` |
| `demo frame 0 x` | `0` | `px` | `docs/SRS.md` §4 Gherkin Scenario |
| `demo frame 30 x` | `300` | `px` | `docs/SRS.md` §4 Gherkin Scenario |
| `demo frame 15 interpolated x` | `150` | `px` | `docs/SRS.md` §4 Gherkin Scenario (Linear midpoint) |

## 2. Workbench Layout & Design Tokens (`layout`, `theme`)

| Parameter | Value | Unit | Source |
|---|---|---|---|
| `design viewport width` | `1440` | `px` | `docs/DESIGN_SYSTEM.md` |
| `design viewport height` | `900` | `px` | `docs/DESIGN_SYSTEM.md` |
| `topbar height` | `48` | `px` | `docs/DESIGN_SYSTEM.md` `--topbar-height` |
| `left layer panel width` | `240` | `px` | `docs/DESIGN_SYSTEM.md` `--left-panel-width` |
| `right inspector panel width` | `280` | `px` | `docs/DESIGN_SYSTEM.md` `--right-panel-width` |
| `bottom timeline height` | `240` | `px` | `docs/DESIGN_SYSTEM.md` `--timeline-height` |
| `control border radius` | `4` | `px` | `docs/DESIGN_SYSTEM.md` `--radius-control` |
| `color bg` | `#17191D` | `hex` | `docs/DESIGN_SYSTEM.md` `--color-bg` |
| `color panel` | `#202329` | `hex` | `docs/DESIGN_SYSTEM.md` `--color-panel` |
| `color canvas` | `#2B2E34` | `hex` | `docs/DESIGN_SYSTEM.md` `--color-canvas` |
| `color accent` | `#6B9CFF` | `hex` | `docs/DESIGN_SYSTEM.md` `--color-accent` |

## 3. Canvas Viewport, History & Persistence (`interaction`, `persistence`)

| Parameter | Value | Unit | Source |
|---|---|---|---|
| `min canvas zoom` | `0.1` | `ratio` | `docs/SRS.md` FR-002 (10%) |
| `max canvas zoom` | `8.0` | `ratio` | `docs/SRS.md` FR-002 (800%) |
| `max undo history depth` | `100` | `steps` | `docs/SRS.md` FR-011 (supports >= 10 steps) |
| `paste offset delta` | `20` | `px` | `docs/INTERACTION_SPEC.md` FR-012 |
| `schema version` | `1` | `version` | `schemas/motion-project.v1.schema.json` |
| `commits per drag gesture` | `1` | `count` | `docs/SRS.md` NFR-005 / TC-006 |
