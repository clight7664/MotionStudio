using System.Text.Json;
using MotionStudio.Application.Contracts;
using MotionStudio.Application.Services;
using MotionStudio.Desktop;
using MotionStudio.Domain.Models;
using MotionStudio.Domain.Services;
using MotionStudio.Infrastructure.Export;
using MotionStudio.Infrastructure.Persistence;
using MotionStudio.Infrastructure.Security;
using MotionStudio.Web.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IDesktopHostBridge>(sp =>
{
    IWebHostEnvironment env = sp.GetRequiredService<IWebHostEnvironment>();
    return new ElectronDesktopHostBridge(env.ContentRootPath);
});

builder.Services.AddSingleton<IProjectRepository, JsonProjectRepository>();
builder.Services.AddSingleton<ISvgSanitizer, SvgSanitizerService>();
builder.Services.AddSingleton<IFrameExporter, StaticExporterService>();
builder.Services.AddSingleton<EditorWorkspaceService>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/health", (IDesktopHostBridge bridge) => Results.Ok(new
{
    status = "ok",
    hostMode = bridge.HostModeLabel,
    isDesktop = bridge.IsDesktopMode,
    schemaVersion = ProjectDocument.CurrentSchemaVersion,
}));

app.MapGet("/api/workspace/snapshot", (EditorWorkspaceService workspace) =>
    Results.Ok(workspace.GetSnapshot()));

app.MapPost("/api/workspace/invoke", async (HttpRequest request, EditorWorkspaceService workspace, CancellationToken ct) =>
{
    using JsonDocument doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false);
    JsonElement root = doc.RootElement;
    string method = root.TryGetProperty("method", out JsonElement mEl) ? (mEl.GetString() ?? string.Empty) : string.Empty;
    JsonElement args = root.TryGetProperty("args", out JsonElement aEl) ? aEl : default;

    switch (method)
    {
        case "OnRendererPointerCommitAsync":
            if (args.ValueKind == JsonValueKind.Array && args.GetArrayLength() > 0)
            {
                JsonElement p = args[0];
                string cmdId = p.TryGetProperty("commandId", out JsonElement cid) ? (cid.GetString() ?? Guid.NewGuid().ToString("N")) : Guid.NewGuid().ToString("N");
                string docId = p.TryGetProperty("documentId", out JsonElement did) ? (did.GetString() ?? workspace.CurrentDocument.ProjectId) : workspace.CurrentDocument.ProjectId;
                long baseRev = p.TryGetProperty("baseRevision", out JsonElement br) ? br.GetInt64() : workspace.CurrentDocument.Revision.Value;
                string layerId = p.TryGetProperty("layerId", out JsonElement lid) ? (lid.GetString() ?? string.Empty) : string.Empty;
                double x = p.TryGetProperty("x", out JsonElement xEl) ? xEl.GetDouble() : 0.0;
                double y = p.TryGetProperty("y", out JsonElement yEl) ? yEl.GetDouble() : 0.0;
                double rot = p.TryGetProperty("rotation", out JsonElement rEl) ? rEl.GetDouble() : 0.0;
                double sx = p.TryGetProperty("scaleX", out JsonElement sxEl) ? sxEl.GetDouble() : 1.0;
                double sy = p.TryGetProperty("scaleY", out JsonElement syEl) ? syEl.GetDouble() : 1.0;

                CommandResult commitRes = workspace.CommitPointerTransform(
                    new PointerCommitPayload(cmdId, docId, baseRev, layerId, x, y, rot, sx, sy));
                return Results.Ok(new { result = commitRes, snapshot = workspace.GetSnapshot() });
            }

            break;

        case "OnRendererLayerSelectedAsync":
            if (args.ValueKind == JsonValueKind.Array && args.GetArrayLength() >= 1)
            {
                string layerId = args[0].GetString() ?? string.Empty;
                bool append = args.GetArrayLength() >= 2 && args[1].ValueKind == JsonValueKind.True;
                workspace.SelectLayer(layerId, append);
            }

            break;

        case "OnRendererPlaybackFrameSyncAsync":
            if (args.ValueKind == JsonValueKind.Array && args.GetArrayLength() >= 2)
            {
                int frame = args[0].GetInt32();
                bool playing = args[1].ValueKind == JsonValueKind.True;
                workspace.SeekFrame(frame);
                workspace.SetPlaybackState(playing);
            }

            break;
    }

    return Results.Ok(new { snapshot = workspace.GetSnapshot() });
});

app.MapPost("/api/workspace/action", async (HttpRequest request, EditorWorkspaceService workspace, CancellationToken ct) =>
{
    using JsonDocument doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false);
    JsonElement root = doc.RootElement;
    string action = root.TryGetProperty("action", out JsonElement actEl) ? (actEl.GetString() ?? string.Empty) : string.Empty;

    switch (action)
    {
        case "select-layer":
            if (root.TryGetProperty("layerId", out JsonElement selId))
            {
                bool multi = root.TryGetProperty("multi", out JsonElement mEl) && mEl.ValueKind == JsonValueKind.True;
                workspace.SelectLayer(selId.GetString() ?? string.Empty, multi);
            }

            break;

        case "add-shape":
            if (root.TryGetProperty("kind", out JsonElement kindEl))
            {
                LayerKind parsedKind = DomainGuard.ParseLayerKind(kindEl.GetString() ?? "rectangle");
                workspace.CreateLayer(parsedKind);
            }

            break;

        case "toggle-vis":
            if (root.TryGetProperty("layerId", out JsonElement visId))
            {
                workspace.ToggleLayerVisibility(visId.GetString() ?? string.Empty);
            }

            break;

        case "toggle-lock":
            if (root.TryGetProperty("layerId", out JsonElement lockId))
            {
                workspace.ToggleLayerLock(lockId.GetString() ?? string.Empty);
            }

            break;

        case "seek":
            if (root.TryGetProperty("frame", out JsonElement fEl))
            {
                workspace.SeekFrame(fEl.GetInt32());
            }

            break;

        case "set-prop":
            if (root.TryGetProperty("property", out JsonElement propEl))
            {
                AnimatableProperty prop = DomainGuard.ParseAnimatableProperty(propEl.GetString() ?? "transform.x");
                double num = root.TryGetProperty("value", out JsonElement vEl) && vEl.ValueKind == JsonValueKind.Number ? vEl.GetDouble() : 0.0;
                string? color = root.TryGetProperty("color", out JsonElement cEl) ? cEl.GetString() : null;
                workspace.SetSelectedProperty(prop, num, color);
            }

            break;

        case "set-geom":
            if (root.TryGetProperty("width", out JsonElement wEl) && root.TryGetProperty("height", out JsonElement hEl))
            {
                workspace.SetSelectedGeometry(wEl.GetDouble(), hEl.GetDouble());
            }

            break;

        case "undo":
            workspace.Undo();
            break;

        case "redo":
            workspace.Redo();
            break;

        case "save":
            await workspace.SaveProjectAsync(workspace.LastSavedFileName, ct).ConfigureAwait(false);
            break;
    }

    return Results.Ok(new
    {
        isDirty = workspace.IsDirty,
        canUndo = workspace.CanUndo,
        canRedo = workspace.CanRedo,
        snapshot = workspace.GetSnapshot(),
    });
});

app.MapGet("/api/export/png", (HttpRequest req, EditorWorkspaceService workspace, IFrameExporter exporter) =>
{
    int frame = workspace.CurrentFrameIndex;
    if (req.Query.TryGetValue("frame", out var rawFrame) && int.TryParse(rawFrame, out int parsedFrame))
    {
        frame = parsedFrame;
    }

    byte[] png = exporter.ExportFrameToPngBytes(workspace.CurrentDocument, frame);
    return Results.File(png, "image/png", $"motionstudio-frame-{frame}.png");
});

app.MapPost("/api/self-test", async (
    IProjectRepository repo,
    ISvgSanitizer sanitizer,
    IFrameExporter exporter,
    CancellationToken ct) =>
{
    var workspace = new EditorWorkspaceService(repo, sanitizer, exporter);

    // Step 1 ("move"): Create a project, add rectangle, and commit a single pointer transform gesture.
    workspace.CreateNewProject("SelfTest Motion Project", 1920, 1080, 30, 90);
    CommandResult createLayerRes = workspace.CreateLayer(LayerKind.Rectangle, "Shape 1");
    string layerId = workspace.CurrentDocument.Layers[0].Id;
    long revBeforeDrag = workspace.CurrentDocument.Revision.Value;

    var dragCommit = new PointerCommitPayload(
        Guid.NewGuid().ToString("N"),
        workspace.CurrentDocument.ProjectId,
        revBeforeDrag,
        layerId,
        X: 180.0,
        Y: 220.0,
        Rotation: 12.0,
        ScaleX: 1.2,
        ScaleY: 1.2);
    CommandResult moveRes = workspace.CommitPointerTransform(dragCommit);
    LayerNode movedLayer = workspace.CurrentDocument.Layers[0];
    bool movePassed = createLayerRes.Succeeded &&
                      moveRes.Succeeded &&
                      Math.Abs(movedLayer.Transform.X - 180.0) < 1e-6 &&
                      Math.Abs(movedLayer.Transform.Y - 220.0) < 1e-6;

    // Step 2 ("act"): Add keyframes at 0s (frame 0, X=0) and 1s (frame 30, X=300), sample at frame 15 (X=150),
    // verify Save/Open .motion.json persistence round-trip.
    workspace.UpsertKeyframe(layerId, AnimatableProperty.TransformX, 0, 0.0, EasingType.Linear);
    workspace.UpsertKeyframe(layerId, AnimatableProperty.TransformX, 30, 300.0, EasingType.Linear);
    workspace.SeekFrame(15);
    IReadOnlyList<EvaluatedLayerState> evaluated15 = workspace.GetEvaluatedLayersAtCurrentFrame();
    double sampledX15 = evaluated15[0].X;

    CommandResult saveRes = await workspace.SaveProjectAsync("selftest-roundtrip.motion.json", ct).ConfigureAwait(false);
    workspace.CreateNewProject("Temp Blank", 1920, 1080, 30, 90);
    CommandResult openRes = await workspace.OpenProjectAsync("selftest-roundtrip.motion.json", ct).ConfigureAwait(false);
    workspace.SeekFrame(15);
    double reopenedX15 = workspace.GetEvaluatedLayersAtCurrentFrame()[0].X;

    bool actPassed = saveRes.Succeeded &&
                     openRes.Succeeded &&
                     Math.Abs(sampledX15 - 150.0) < 1e-6 &&
                     Math.Abs(reopenedX15 - 150.0) < 1e-6;

    // Step 3 ("hud"): Verify snapshot, SVG export, and SkiaSharp 100-node headless PNG rendering.
    DocumentSnapshotDto snapshot = workspace.GetSnapshot();
    string exportedSvg = workspace.ExportCurrentFrameSvg();
    byte[] exportedPng = exporter.ExportFrameToPngBytes(workspace.CurrentDocument, 15);
    SkiaRenderMetrics skiaBench = exporter.BenchmarkHundredNodeRender(100);
    bool pngSignatureValid = exportedPng.Length > 64 &&
                             exportedPng[0] == 0x89 &&
                             exportedPng[1] == 0x50 &&
                             exportedPng[2] == 0x4E &&
                             exportedPng[3] == 0x47;
    bool hudPassed = snapshot.Composition.Width == 1920 &&
                     snapshot.Composition.Height == 1080 &&
                     snapshot.Composition.Fps == 30 &&
                     snapshot.Layers.Count == 1 &&
                     snapshot.Tracks.Count == 1 &&
                     exportedSvg.Contains("<svg", StringComparison.Ordinal) &&
                     pngSignatureValid &&
                     skiaBench.NodeCount == 100 &&
                     skiaBench.PngByteLength > 100;

    return Results.Ok(new
    {
        version = 1,
        steps = new object[]
        {
            new
            {
                step = "move",
                passed = movePassed,
                detail = $"Created 'Shape 1' and committed single pointer transform to X={movedLayer.Transform.X}, Y={movedLayer.Transform.Y} (rev {workspace.CurrentDocument.Revision.Value})",
            },
            new
            {
                step = "act",
                passed = actPassed,
                detail = $"Added keyframes X=0@0f and X=300@30f, interpolated X={sampledX15}@15f, saved and reopened selftest-roundtrip.motion.json with X={reopenedX15}@15f",
            },
            new
            {
                step = "hud",
                passed = hudPassed,
                detail = $"Workbench HUD active: {snapshot.Composition.Width}x{snapshot.Composition.Height}@{snapshot.Composition.Fps}fps, SVG+SkiaSharp PNG ({exportedPng.Length}B) & 100-node benchmark (eval={skiaBench.SceneEvaluationMs:F2}ms, render={skiaBench.RenderMs:F2}ms) verified",
            },
        },
    });
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
