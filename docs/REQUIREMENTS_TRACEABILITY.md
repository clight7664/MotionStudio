# REQUIREMENTS_TRACEABILITY — 双向需求追溯矩阵

- **文档版本**：`0.2.0`
- **维护角色**：QA Engineer / Project Manager

| 需求 ID | 阶段 | 设计 / ADR 引用 | 代码模块实现 | 验证测试用例 | 状态 |
|---|---|---|---|---|---|
| `FR-001` | P0 | `ARCHITECTURE.md` §2 | `MotionStudio.Domain.Composition`, `EditorWorkspaceService` | `TC-001` (`DomainValidationTests`) | Approved for Implementation |
| `FR-002` | P0 | `ADR-0002` | `MotionStudio.Renderer/src/workbench.ts`, `Workbench.razor` | `RendererTests`, `E2ETests` | Approved for Implementation |
| `FR-003` | P0/P1 | `ARCHITECTURE.md` §2 | `LayerNode`, `EditorWorkspaceService.CreateLayer` | `WorkspaceCommandTests` | Approved for Implementation |
| `FR-004` | P0/P1 | `ARCHITECTURE.md` §3, `ADR-0002` | `InteractionController`, `MoveLayerTransformCommand` | `TC-006` (`InteractionTests`) | Approved for Implementation |
| `FR-005` | P0/P1 | `ARCHITECTURE.md` §2 | `EditorWorkspaceService` (Visibility, Lock, Reorder, Group) | `WorkspaceCommandTests` | Approved for Implementation |
| `FR-006` | P0 | `DESIGN_SYSTEM.md` §2 | `Workbench.razor` (Inspector + `<Mixed>` state) | `WorkspaceCommandTests`, `E2ETests` | Approved for Implementation |
| `FR-007` | P0 | `ARCHITECTURE.md` §4 | `EditorWorkspaceService.SeekFrame`, `Workbench.razor` | `TC-002` (`TimelineEvaluatorTests`) | Approved for Implementation |
| `FR-008` | P0/P1 | `ARCHITECTURE.md` §4 | `AnimationTrack`, `EditorWorkspaceService.UpsertKeyframe` | `TC-002` (`TimelineEvaluatorTests`) | Approved for Implementation |
| `FR-009` | P0/P1 | `ADR-0002` | `TimelineEvaluator.cs`, `interpolation.ts` | `TC-002` (`GoldenVectorTests`, `interpolation.test.ts`) | Approved for Implementation |
| `FR-010` | P0 | `ADR-0002` | `PlaybackController` (`playback.ts`), `Workbench.razor` | `playback.test.ts`, `E2ETests` | Approved for Implementation |
| `FR-011` | P1 | `ADR-0003` | `EditorWorkspaceService.Undo` / `Redo` | `TC-004` (`UndoRedoTests`) | Approved for Implementation |
| `FR-012` | P1 | `INTERACTION_SPEC.md` | `EditorWorkspaceService.CopySelected` / `PasteClipboard` | `WorkspaceCommandTests` | Approved for Implementation |
| `FR-013` | P0/P1 | `ADR-0003` | `JsonProjectRepository.cs` | `TC-003` (`ProjectPersistenceTests`) | Approved for Implementation |
| `FR-014` | P1 | `INTERACTION_SPEC.md` | `Workbench.razor` (`HandleGlobalKeyDown`), `workbench.ts` | `E2ETests` | Approved for Implementation |
| `FR-015` | P0 | `DESIGN_SYSTEM.md` §3 | `Workbench.razor` (Banner, Empty & Dirty state) | `E2ETests` | Approved for Implementation |
| `FR-016` | P0/P1 | `ADR-0001` | `MotionStudio.Desktop`, `electron-main.cjs` | `TC-005` (`--self-test`, `--screenshot`) | Approved for Implementation |
| `FR-017` | P2 | `SECURITY.md` §3 | `SvgSanitizerService.cs` | `SvgSanitizerTests` | Approved for Implementation |
| `FR-018` | P1/P2 | `ARCHITECTURE.md` §2 | `StaticExporterService.cs` | `ExporterTests` | Approved for Implementation |
| `FR-019` | P2 | `PRD.md` §3 | `EditorWorkspaceService.ApplyPreset` | `WorkspaceCommandTests` | Approved for Implementation |
| `FR-020` | P1/P2 | `ADR-0002` | `CubicBezierPoints`, `TimelineEvaluator`, `interpolation.ts` | `TimelineEvaluatorTests`, `interpolation.test.ts` | Approved for Implementation |
| `NFR-001`..`NFR-009` | G0..P3 | `ADR-0001..0003`, `SECURITY.md` | All Modules | Full Test Suite & CI Gates | Approved for Implementation |
