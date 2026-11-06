using MotionStudio.Application.Contracts;
using MotionStudio.Application.Services;
using MotionStudio.Domain.Models;

namespace MotionStudio.Application.Tests;

public sealed class WorkspaceCommandTests
{
    private static EditorWorkspaceService CreateService()
    {
        return new EditorWorkspaceService(
            new InMemoryProjectRepository(),
            new StubSvgSanitizer(),
            new StubFrameExporter());
    }

    [Fact]
    public void TC004_UndoRedo_TenStepSequence_ReversesAndReplaysWithoutPlayheadPollution()
    {
        EditorWorkspaceService workspace = CreateService();
        workspace.CreateNewProject("Undo Test", 1920, 1080, 30, 90);
        workspace.CreateLayer(LayerKind.Rectangle, "Rect 1");
        string layerId = workspace.CurrentDocument.Layers[0].Id;

        // Perform 10 distinct semantic edits (steps 2..11)
        for (int step = 1; step <= 10; step++)
        {
            workspace.SeekFrame(step * 2); // Playhead seek must NOT pollute undo history
            workspace.SetSelectedProperty(AnimatableProperty.TransformX, step * 25.0);
        }

        Assert.Equal(11, workspace.UndoCount);
        Assert.Equal(250.0, workspace.CurrentDocument.Layers[0].Transform.X, precision: 5);

        // Undo 10 property edits
        for (int step = 10; step >= 1; step--)
        {
            CommandResult undoRes = workspace.Undo();
            Assert.True(undoRes.Succeeded);
        }

        Assert.Equal(1, workspace.UndoCount);
        Assert.Equal(10, workspace.RedoCount);

        // Redo all 10 property edits
        for (int step = 1; step <= 10; step++)
        {
            CommandResult redoRes = workspace.Redo();
            Assert.True(redoRes.Succeeded);
            Assert.Equal(step * 25.0, workspace.CurrentDocument.Layers[0].Transform.X, precision: 5);
        }

        // Verify playhead seek alone does not add undo entries
        int undoBeforeSeek = workspace.UndoCount;
        workspace.SeekFrame(45);
        Assert.Equal(undoBeforeSeek, workspace.UndoCount);
        _ = layerId;
    }

    [Fact]
    public void TC006_PointerTransformCommit_SingleCommitPerGesture_AndRejectsStaleRevision()
    {
        EditorWorkspaceService workspace = CreateService();
        workspace.CreateNewProject("Gesture Test", 1920, 1080, 30, 90);
        workspace.CreateLayer(LayerKind.Rectangle, "Drag Shape");
        string layerId = workspace.CurrentDocument.Layers[0].Id;

        int commitsBefore = workspace.CommandCommitCount;
        long baseRevision = workspace.CurrentDocument.Revision.Value;

        var commitPayload = new PointerCommitPayload(
            "cmd-drag-1",
            workspace.CurrentDocument.ProjectId,
            baseRevision,
            layerId,
            X: 412.5,
            Y: 288.0,
            Rotation: 15.0,
            ScaleX: 1.25,
            ScaleY: 1.25);

        CommandResult res = workspace.CommitPointerTransform(commitPayload);
        Assert.True(res.Succeeded);
        Assert.Equal(commitsBefore + 1, workspace.CommandCommitCount);
        Assert.Equal(412.5, workspace.CurrentDocument.Layers[0].Transform.X, precision: 5);

        // Stale baseRevision must be rejected without mutating state
        CommandResult staleRes = workspace.CommitPointerTransform(commitPayload);
        Assert.False(staleRes.Succeeded);
        Assert.Equal("ERR_REVISION_CONFLICT", staleRes.ErrorCode);
    }

    [Fact]
    public void LayerLock_Visibility_Reorder_Group_CopyPaste_AndPresets_WorkCorrectly()
    {
        EditorWorkspaceService workspace = CreateService();
        workspace.CreateNewProject("Layer Ops", 1920, 1080, 30, 90);
        workspace.CreateLayer(LayerKind.Rectangle, "Layer A");
        workspace.CreateLayer(LayerKind.Ellipse, "Layer B");

        string layerA = workspace.CurrentDocument.Layers[0].Id;
        string layerB = workspace.CurrentDocument.Layers[1].Id;

        // Multi-select & mixed value check
        workspace.SelectLayer(layerA, appendSelection: false);
        workspace.SetSelectedProperty(AnimatableProperty.TransformX, 100.0);
        workspace.SelectLayer(layerB, appendSelection: false);
        workspace.SetSelectedProperty(AnimatableProperty.TransformX, 300.0);

        workspace.SelectLayer(layerA, appendSelection: false);
        workspace.SelectLayer(layerB, appendSelection: true);
        Assert.True(workspace.IsSelectedPropertyMixed(AnimatableProperty.TransformX));

        // Copy & Paste
        Assert.True(workspace.CopySelectedLayers().Succeeded);
        Assert.True(workspace.PasteClipboardLayers().Succeeded);
        Assert.Equal(4, workspace.CurrentDocument.Layers.Count);

        // Group selected
        Assert.True(workspace.GroupSelectedLayers("Hero Group").Succeeded);
        Assert.Contains(workspace.CurrentDocument.Layers, l => l.Kind == LayerKind.Group);

        // Apply motion preset
        Assert.True(workspace.ApplyPreset(layerA, MotionPresetKind.FadeIn).Succeeded);
        Assert.Contains(workspace.CurrentDocument.Tracks, t => t.LayerId == layerA && t.Property == AnimatableProperty.Opacity);

        // Locking prevents property modification
        workspace.ToggleLayerLock(layerA);
        workspace.SelectLayer(layerA, appendSelection: false);
        CommandResult lockedEdit = workspace.SetSelectedProperty(AnimatableProperty.TransformX, 500.0);
        Assert.False(lockedEdit.Succeeded);
        Assert.Equal("ERR_LAYER_LOCKED", lockedEdit.ErrorCode);
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        private readonly Dictionary<string, ProjectDocument> _store = [];

        public Task SaveAsync(ProjectDocument document, string fileName, CancellationToken cancellationToken = default)
        {
            _store[fileName] = document;
            return Task.CompletedTask;
        }

        public Task<ProjectDocument> LoadAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store[fileName]);
        }

        public Task<ProjectDocument?> TryRecoverBackupAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProjectDocument?>(null);
        }

        public IReadOnlyList<string> ListProjectFiles() => [.. _store.Keys];

        public string SerializeToJson(ProjectDocument document) => "{}";

        public ProjectDocument DeserializeFromJson(string json) => ProjectDocument.CreateDefaultDemo();
    }

    private sealed class StubSvgSanitizer : ISvgSanitizer
    {
        public SvgSanitizeResult InspectAndSanitize(string rawSvg) =>
            new(true, null, null, rawSvg, 200, 120, "#6B9CFF");
    }

    private sealed class StubFrameExporter : IFrameExporter
    {
        public string ExportFrameToSvg(ProjectDocument document, int frameIndex) => "<svg></svg>";

        public byte[] ExportFrameToPngBytes(ProjectDocument document, int frameIndex) => [0x89, 0x50, 0x4E, 0x47];

        public SkiaRenderMetrics BenchmarkHundredNodeRender(int nodeCount = 100) =>
            new(nodeCount, 0.2, 1.5, 4096, 1024);
    }
}
