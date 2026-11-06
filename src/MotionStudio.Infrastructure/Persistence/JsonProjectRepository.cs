using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MotionStudio.Application.Contracts;
using MotionStudio.Domain.Models;

namespace MotionStudio.Infrastructure.Persistence;

public sealed class JsonProjectRepository : IProjectRepository
{
    public const int MaxProjectFileBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 32,
    };

    private readonly string _projectsDirectory;

    public JsonProjectRepository(IDesktopHostBridge hostBridge)
    {
        ArgumentNullException.ThrowIfNull(hostBridge);
        _projectsDirectory = Path.GetFullPath(hostBridge.SafeProjectsDirectory);
        Directory.CreateDirectory(_projectsDirectory);
    }

    public JsonProjectRepository(string projectsDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectsDirectory))
        {
            throw new ArgumentException("Projects directory cannot be empty.", nameof(projectsDirectory));
        }

        _projectsDirectory = Path.GetFullPath(projectsDirectory);
        Directory.CreateDirectory(_projectsDirectory);
    }

    public async Task SaveAsync(ProjectDocument document, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        string safeTargetPath = ResolveSafeProjectPath(fileName);
        string json = SerializeToJson(document);

        // Verify the serialized JSON can be parsed back before touching disk.
        _ = DeserializeFromJson(json);

        string directory = Path.GetDirectoryName(safeTargetPath)!;
        Directory.CreateDirectory(directory);

        string tempPath = $"{safeTargetPath}.tmp";
        string backupPath = $"{safeTargetPath}.bak";

        byte[] utf8Bytes = Encoding.UTF8.GetBytes(json);
        await using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
        {
            await stream.WriteAsync(utf8Bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(safeTargetPath))
        {
            File.Copy(safeTargetPath, backupPath, overwrite: true);
        }

        File.Move(tempPath, safeTargetPath, overwrite: true);
    }

    public async Task<ProjectDocument> LoadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        string safeTargetPath = ResolveSafeProjectPath(fileName);
        if (!File.Exists(safeTargetPath))
        {
            throw new DomainValidationException("ERR_FILE_NOT_FOUND", $"Project file '{fileName}' does not exist.");
        }

        var fileInfo = new FileInfo(safeTargetPath);
        if (fileInfo.Length > MaxProjectFileBytes)
        {
            throw new DomainValidationException("ERR_FILE_TOO_LARGE", "Project file exceeds the 10 MB security limit.");
        }

        string json = await File.ReadAllTextAsync(safeTargetPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return DeserializeFromJson(json);
    }

    public async Task<ProjectDocument?> TryRecoverBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        string safeTargetPath = ResolveSafeProjectPath(fileName);
        string backupPath = $"{safeTargetPath}.bak";
        if (!File.Exists(backupPath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(backupPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return DeserializeFromJson(json);
    }

    public IReadOnlyList<string> ListProjectFiles()
    {
        if (!Directory.Exists(_projectsDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(_projectsDirectory, "*.motion.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string SerializeToJson(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var wireModel = new ProjectFileWireModel
        {
            SchemaVersion = document.SchemaVersion,
            ProjectId = document.ProjectId,
            Name = document.Name,
            Composition = new CompositionWireModel
            {
                Width = document.Composition.Width,
                Height = document.Composition.Height,
                Fps = document.Composition.Fps,
                DurationFrames = document.Composition.DurationFrames,
            },
            Layers = document.Layers
                .OrderBy(l => l.ZIndex)
                .Select(l => new LayerWireModel
                {
                    Id = l.Id,
                    Kind = l.Kind.ToWireName(),
                    Name = l.Name,
                    ParentId = l.ParentId,
                    TextContent = l.TextContent,
                    FontSize = l.FontSize,
                    Visible = l.Visible,
                    Locked = l.Locked,
                    ZIndex = l.ZIndex,
                    Geometry = new GeometryWireModel
                    {
                        Width = l.Geometry.Width,
                        Height = l.Geometry.Height,
                    },
                    Transform = new TransformWireModel
                    {
                        X = l.Transform.X,
                        Y = l.Transform.Y,
                        Rotation = l.Transform.Rotation,
                        ScaleX = l.Transform.ScaleX,
                        ScaleY = l.Transform.ScaleY,
                    },
                    Opacity = l.Opacity,
                    Fill = l.Fill,
                })
                .ToList(),
            Tracks = document.Tracks
                .Select(t => new TrackWireModel
                {
                    Id = t.Id,
                    LayerId = t.LayerId,
                    Property = t.Property.ToWireName(),
                    Keyframes = t.Keyframes
                        .Select(k => new KeyframeWireModel
                        {
                            FrameIndex = k.FrameIndex,
                            Value = k.Value,
                            ColorValue = k.ColorValue,
                            Easing = k.Easing.ToWireName(),
                            Bezier = k.Bezier is null
                                ? null
                                : new BezierWireModel
                                {
                                    X1 = k.Bezier.X1,
                                    Y1 = k.Bezier.Y1,
                                    X2 = k.Bezier.X2,
                                    Y2 = k.Bezier.Y2,
                                },
                        })
                        .ToList(),
                })
                .ToList(),
            Assets = document.Assets
                .Select(a => new AssetWireModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Kind = a.Kind,
                    Content = a.Content,
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(wireModel, SerializerOptions);
    }

    public ProjectDocument DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new DomainValidationException("ERR_EMPTY_JSON", "Project JSON payload cannot be empty.");
        }

        ProjectFileWireModel? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ProjectFileWireModel>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException("ERR_INVALID_JSON", $"Invalid project JSON syntax: {ex.Message}", ex);
        }

        if (wire is null)
        {
            throw new DomainValidationException("ERR_NULL_PROJECT", "Deserialized project payload was null.");
        }

        // Migration pipeline: upgrade v0 legacy draft to v1 if encountered.
        int schemaVersion = wire.SchemaVersion == 0 ? 1 : wire.SchemaVersion;
        if (schemaVersion != ProjectDocument.CurrentSchemaVersion)
        {
            throw new DomainValidationException(
                "ERR_UNSUPPORTED_SCHEMA",
                $"Unsupported schemaVersion '{wire.SchemaVersion}'. Expected {ProjectDocument.CurrentSchemaVersion}.");
        }

        if (wire.Composition is null)
        {
            throw new DomainValidationException("ERR_MISSING_COMPOSITION", "Project JSON is missing composition object.");
        }

        var composition = new Composition(
            wire.Composition.Width,
            wire.Composition.Height,
            wire.Composition.Fps,
            wire.Composition.DurationFrames);

        var layers = (wire.Layers ?? []).Select(l =>
        {
            if (l.Geometry is null || l.Transform is null)
            {
                throw new DomainValidationException("ERR_INVALID_LAYER_STRUCTURE", $"Layer '{l.Id}' is missing geometry or transform.");
            }

            return new LayerNode(
                l.Id ?? string.Empty,
                DomainGuard.ParseLayerKind(l.Kind ?? string.Empty),
                l.Name ?? string.Empty,
                l.Visible,
                l.Locked,
                l.ZIndex,
                new ShapeGeometry(l.Geometry.Width, l.Geometry.Height),
                new Transform(l.Transform.X, l.Transform.Y, l.Transform.Rotation, l.Transform.ScaleX, l.Transform.ScaleY),
                l.Opacity,
                l.Fill ?? "#6B9CFF",
                l.ParentId,
                l.TextContent,
                l.FontSize);
        }).ToList();

        var tracks = (wire.Tracks ?? []).Select(t =>
        {
            AnimatableProperty prop = DomainGuard.ParseAnimatableProperty(t.Property ?? string.Empty);
            var keyframes = (t.Keyframes ?? []).Select(k =>
            {
                EasingType easing = DomainGuard.ParseEasingType(k.Easing ?? "linear");
                CubicBezierPoints? bezier = k.Bezier is null
                    ? null
                    : new CubicBezierPoints(k.Bezier.X1, k.Bezier.Y1, k.Bezier.X2, k.Bezier.Y2);
                return new Keyframe(k.FrameIndex, k.Value, easing, k.ColorValue, bezier);
            }).ToList();

            return new AnimationTrack(t.Id ?? string.Empty, t.LayerId ?? string.Empty, prop, keyframes);
        }).ToList();

        var assets = (wire.Assets ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.Id))
            .Select(a => new AssetRef(a.Id!, a.Name ?? "Asset", a.Kind ?? "svg", a.Content ?? string.Empty))
            .ToList();

        return new ProjectDocument(
            wire.ProjectId ?? string.Empty,
            wire.Name ?? string.Empty,
            composition,
            layers,
            tracks,
            assets,
            DocumentRevision.Initial,
            schemaVersion);
    }

    public string ResolveSafeProjectPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainValidationException("ERR_INVALID_FILENAME", "File name cannot be empty.");
        }

        string trimmed = fileName.Trim();
        if (trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.Contains(':', StringComparison.Ordinal) ||
            trimmed.StartsWith('/') ||
            trimmed.StartsWith('\\') ||
            Path.GetFileName(trimmed) != trimmed)
        {
            throw new DomainValidationException(
                "ERR_PATH_TRAVERSAL",
                "Path traversal or directory separators are prohibited in project file names.");
        }

        if (!trimmed.EndsWith(".motion.json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"{trimmed}.motion.json";
        }

        string candidatePath = Path.GetFullPath(Path.Combine(_projectsDirectory, trimmed));
        if (!candidatePath.StartsWith(_projectsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException(
                "ERR_PATH_TRAVERSAL",
                "Resolved project path escapes the authorized workspace directory.");
        }

        return candidatePath;
    }

    private sealed class ProjectFileWireModel
    {
        public int SchemaVersion { get; set; }

        public string? ProjectId { get; set; }

        public string? Name { get; set; }

        public CompositionWireModel? Composition { get; set; }

        public List<LayerWireModel>? Layers { get; set; }

        public List<TrackWireModel>? Tracks { get; set; }

        public List<AssetWireModel>? Assets { get; set; }
    }

    private sealed class CompositionWireModel
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int Fps { get; set; }

        public int DurationFrames { get; set; }
    }

    private sealed class LayerWireModel
    {
        public string? Id { get; set; }

        public string? Kind { get; set; }

        public string? Name { get; set; }

        public string? ParentId { get; set; }

        public string? TextContent { get; set; }

        public double? FontSize { get; set; }

        public bool Visible { get; set; }

        public bool Locked { get; set; }

        public int ZIndex { get; set; }

        public GeometryWireModel? Geometry { get; set; }

        public TransformWireModel? Transform { get; set; }

        public double Opacity { get; set; }

        public string? Fill { get; set; }
    }

    private sealed class GeometryWireModel
    {
        public double Width { get; set; }

        public double Height { get; set; }
    }

    private sealed class TransformWireModel
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Rotation { get; set; }

        public double ScaleX { get; set; }

        public double ScaleY { get; set; }
    }

    private sealed class TrackWireModel
    {
        public string? Id { get; set; }

        public string? LayerId { get; set; }

        public string? Property { get; set; }

        public List<KeyframeWireModel>? Keyframes { get; set; }
    }

    private sealed class KeyframeWireModel
    {
        public int FrameIndex { get; set; }

        public double Value { get; set; }

        public string? ColorValue { get; set; }

        public string? Easing { get; set; }

        public BezierWireModel? Bezier { get; set; }
    }

    private sealed class BezierWireModel
    {
        public double X1 { get; set; }

        public double Y1 { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }
    }

    private sealed class AssetWireModel
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Kind { get; set; }

        public string? Content { get; set; }
    }
}
