using System.Reflection;
using MotionStudio.Application.Contracts;
using MotionStudio.Application.Services;
using MotionStudio.Domain.Models;
using MotionStudio.Infrastructure.Export;
using MotionStudio.Infrastructure.Persistence;
using MotionStudio.Infrastructure.Security;

namespace MotionStudio.E2E;

public sealed class EndToEndWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public EndToEndWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MotionStudioE2E-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task P0_CompleteClosedLoop_Create_AddRect_EditXY_AddKeyframes_PlaySample_Save_Reopen()
    {
        var repo = new JsonProjectRepository(_tempDir);
        var sanitizer = new SvgSanitizerService();
        var exporter = new StaticExporterService();
        var workspace = new EditorWorkspaceService(repo, sanitizer, exporter);

        // 1. New 1920x1080 @ 30fps, 90 frames project
        CommandResult createProj = workspace.CreateNewProject("P0 Closed Loop Project", 1920, 1080, 30, 90);
        Assert.True(createProj.Succeeded);

        // 2. Add Rectangle
        CommandResult addRect = workspace.CreateLayer(LayerKind.Rectangle, "Hero Rectangle");
        Assert.True(addRect.Succeeded);
        string layerId = workspace.CurrentDocument.Layers[0].Id;

        // 3. Change X/Y and add keyframes at 0s (frame 0) and 1s (frame 30)
        workspace.SeekFrame(0);
        Assert.True(workspace.UpsertKeyframe(layerId, AnimatableProperty.TransformX, 0, 0.0, EasingType.Linear).Succeeded);
        Assert.True(workspace.UpsertKeyframe(layerId, AnimatableProperty.TransformY, 0, 100.0, EasingType.Linear).Succeeded);

        workspace.SeekFrame(30);
        Assert.True(workspace.UpsertKeyframe(layerId, AnimatableProperty.TransformX, 30, 300.0, EasingType.Linear).Succeeded);
        Assert.True(workspace.UpsertKeyframe(layerId, AnimatableProperty.TransformY, 30, 400.0, EasingType.Linear).Succeeded);

        // 4. Play -> Pause at frame 15 -> Stop
        workspace.SetPlaybackState(isPlaying: true);
        Assert.True(workspace.IsPlaying);
        workspace.SeekFrame(15);
        workspace.SetPlaybackState(isPlaying: false);

        var evaluatedAt15 = workspace.GetEvaluatedLayersAtCurrentFrame();
        Assert.Single(evaluatedAt15);
        Assert.Equal(150.0, evaluatedAt15[0].X, precision: 5);
        Assert.Equal(250.0, evaluatedAt15[0].Y, precision: 5);

        workspace.StopPlayback(resetToStart: true);
        Assert.False(workspace.IsPlaying);
        Assert.Equal(0, workspace.CurrentFrameIndex);

        // 5. Save -> Open into fresh workspace -> Verify identical keyframes and sampled values
        CommandResult saveRes = await workspace.SaveProjectAsync("p0-closed-loop.motion.json");
        Assert.True(saveRes.Succeeded);
        Assert.False(workspace.IsDirty);

        var reopenedWorkspace = new EditorWorkspaceService(repo, sanitizer, exporter);
        CommandResult openRes = await reopenedWorkspace.OpenProjectAsync("p0-closed-loop.motion.json");
        Assert.True(openRes.Succeeded);

        reopenedWorkspace.SeekFrame(15);
        var reopenedAt15 = reopenedWorkspace.GetEvaluatedLayersAtCurrentFrame();
        Assert.Single(reopenedAt15);
        Assert.Equal("Hero Rectangle", reopenedAt15[0].Name);
        Assert.Equal(150.0, reopenedAt15[0].X, precision: 5);
        Assert.Equal(250.0, reopenedAt15[0].Y, precision: 5);
        Assert.Equal(2, reopenedWorkspace.CurrentDocument.Tracks.Count);
    }

    [Fact]
    public void NFR009_ArchitectureDependencyDirection_DomainHasZeroUiOrIoProjectReferences()
    {
        Assembly domainAssembly = typeof(ProjectDocument).Assembly;
        string[] referencedNames = domainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(referencedNames, n => n.StartsWith("MotionStudio.", StringComparison.Ordinal));
        Assert.DoesNotContain(referencedNames, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
