using MotionStudio.Domain.Models;
using MotionStudio.Domain.Services;

namespace MotionStudio.Application.Contracts;

public enum MotionPresetKind
{
    FadeIn,
    SlideInRight,
    PopScale,
    Pulse,
}

public sealed record EditorCommandEnvelope<TPayload>(
    string CommandId,
    int ContractVersion,
    string DocumentId,
    long BaseRevision,
    DateTimeOffset Timestamp,
    string CommandType,
    TPayload Payload);

public sealed record PointerCommitPayload(
    string CommandId,
    string DocumentId,
    long BaseRevision,
    string LayerId,
    double X,
    double Y,
    double Rotation,
    double ScaleX,
    double ScaleY);

public sealed record BezierDto(double X1, double Y1, double X2, double Y2);

public sealed record KeyframeDto(
    int FrameIndex,
    double Value,
    string Easing,
    string? ColorValue = null,
    BezierDto? Bezier = null);

public sealed record TrackDto(
    string Id,
    string LayerId,
    string Property,
    IReadOnlyList<KeyframeDto> Keyframes);

public sealed record GeometryDto(double Width, double Height);

public sealed record TransformDto(double X, double Y, double Rotation, double ScaleX, double ScaleY);

public sealed record LayerDto(
    string Id,
    string Kind,
    string Name,
    bool Visible,
    bool Locked,
    int ZIndex,
    GeometryDto Geometry,
    TransformDto Transform,
    double Opacity,
    string Fill,
    string? ParentId = null,
    string? TextContent = null,
    double? FontSize = null);

public sealed record CompositionDto(int Width, int Height, int Fps, int DurationFrames);

public sealed record DocumentSnapshotDto(
    int SchemaVersion,
    string ProjectId,
    string Name,
    long Revision,
    int CurrentFrameIndex,
    bool IsPlaying,
    bool IsLooping,
    double CanvasZoom,
    double CanvasPanX,
    double CanvasPanY,
    bool ShowGrid,
    bool ShowCheckerboard,
    IReadOnlyList<string> SelectedLayerIds,
    CompositionDto Composition,
    IReadOnlyList<LayerDto> Layers,
    IReadOnlyList<TrackDto> Tracks,
    IReadOnlyList<EvaluatedLayerState> EvaluatedLayers);

public sealed record CommandResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage,
    long Revision,
    DocumentSnapshotDto? Snapshot)
{
    public static CommandResult Success(DocumentSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new CommandResult(true, null, null, snapshot.Revision, snapshot);
    }

    public static CommandResult Failure(string errorCode, string message, long currentRevision, DocumentSnapshotDto? snapshot = null)
    {
        return new CommandResult(false, errorCode, message, currentRevision, snapshot);
    }
}

public sealed record SvgSanitizeResult(
    bool IsSafe,
    string? ErrorCode,
    string? ErrorMessage,
    string? SanitizedSvg,
    double Width,
    double Height,
    string FillColor);

public interface IProjectRepository
{
    Task SaveAsync(ProjectDocument document, string fileName, CancellationToken cancellationToken = default);

    Task<ProjectDocument> LoadAsync(string fileName, CancellationToken cancellationToken = default);

    Task<ProjectDocument?> TryRecoverBackupAsync(string fileName, CancellationToken cancellationToken = default);

    IReadOnlyList<string> ListProjectFiles();

    string SerializeToJson(ProjectDocument document);

    ProjectDocument DeserializeFromJson(string json);
}

public interface ISvgSanitizer
{
    SvgSanitizeResult InspectAndSanitize(string rawSvg);
}

public sealed record SkiaRenderMetrics(
    int NodeCount,
    double SceneEvaluationMs,
    double RenderMs,
    long PngByteLength,
    long AllocatedBytes);

public interface IFrameExporter
{
    string ExportFrameToSvg(ProjectDocument document, int frameIndex);

    byte[] ExportFrameToPngBytes(ProjectDocument document, int frameIndex);

    SkiaRenderMetrics BenchmarkHundredNodeRender(int nodeCount = 100);
}

public interface IDesktopHostBridge
{
    bool IsDesktopMode { get; }

    string HostModeLabel { get; }

    string SafeProjectsDirectory { get; }

    bool ValidateDesktopToken(string? providedToken);
}

public static class SnapshotMapper
{
    public static DocumentSnapshotDto ToDto(
        ProjectDocument document,
        int currentFrameIndex,
        bool isPlaying,
        bool isLooping,
        double canvasZoom,
        double canvasPanX,
        double canvasPanY,
        bool showGrid,
        bool showCheckerboard,
        IReadOnlyList<string> selectedLayerIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(selectedLayerIds);

        var compositionDto = new CompositionDto(
            document.Composition.Width,
            document.Composition.Height,
            document.Composition.Fps,
            document.Composition.DurationFrames);

        var layerDtos = document.Layers
            .OrderBy(l => l.ZIndex)
            .Select(l => new LayerDto(
                l.Id,
                l.Kind.ToWireName(),
                l.Name,
                l.Visible,
                l.Locked,
                l.ZIndex,
                new GeometryDto(l.Geometry.Width, l.Geometry.Height),
                new TransformDto(l.Transform.X, l.Transform.Y, l.Transform.Rotation, l.Transform.ScaleX, l.Transform.ScaleY),
                l.Opacity,
                l.Fill,
                l.ParentId,
                l.TextContent,
                l.FontSize))
            .ToList();

        var trackDtos = document.Tracks
            .Select(t => new TrackDto(
                t.Id,
                t.LayerId,
                t.Property.ToWireName(),
                t.Keyframes.Select(k => new KeyframeDto(
                    k.FrameIndex,
                    k.Value,
                    k.Easing.ToWireName(),
                    k.ColorValue,
                    k.Bezier is null ? null : new BezierDto(k.Bezier.X1, k.Bezier.Y1, k.Bezier.X2, k.Bezier.Y2))).ToList()))
            .ToList();

        IReadOnlyList<EvaluatedLayerState> evaluated = TimelineEvaluator.EvaluateDocumentAtFrame(document, currentFrameIndex);

        return new DocumentSnapshotDto(
            document.SchemaVersion,
            document.ProjectId,
            document.Name,
            document.Revision.Value,
            currentFrameIndex,
            isPlaying,
            isLooping,
            canvasZoom,
            canvasPanX,
            canvasPanY,
            showGrid,
            showCheckerboard,
            selectedLayerIds.ToList(),
            compositionDto,
            layerDtos,
            trackDtos,
            evaluated);
    }
}
