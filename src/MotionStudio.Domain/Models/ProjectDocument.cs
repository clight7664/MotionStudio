namespace MotionStudio.Domain.Models;

public sealed record LayerNode
{
    public LayerNode(
        string id,
        LayerKind kind,
        string name,
        bool visible,
        bool locked,
        int zIndex,
        ShapeGeometry geometry,
        Transform transform,
        double opacity,
        string fill,
        string? parentId = null,
        string? textContent = null,
        double? fontSize = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("ERR_INVALID_LAYER_ID", "Layer ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("ERR_INVALID_LAYER_NAME", "Layer name cannot be empty.");
        }

        if (zIndex < 0)
        {
            throw new DomainValidationException("ERR_INVALID_ZINDEX", "Layer zIndex must be >= 0.");
        }

        DomainGuard.EnsureFinite(opacity, nameof(opacity));
        if (opacity < 0.0 || opacity > 1.0)
        {
            throw new DomainValidationException("ERR_INVALID_OPACITY", "Layer opacity must be within [0, 1].");
        }

        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(transform);

        Id = id.Trim();
        Kind = kind;
        Name = name.Trim();
        Visible = visible;
        Locked = locked;
        ZIndex = zIndex;
        Geometry = geometry;
        Transform = transform;
        Opacity = opacity;
        Fill = DomainGuard.NormalizeHexColor(fill);
        ParentId = string.IsNullOrWhiteSpace(parentId) ? null : parentId.Trim();
        TextContent = kind == LayerKind.Text
            ? (string.IsNullOrWhiteSpace(textContent) ? "Text Layer" : textContent)
            : textContent;
        FontSize = fontSize.HasValue && fontSize.Value > 0 ? fontSize.Value : (kind == LayerKind.Text ? 36.0 : null);
    }

    public string Id { get; }

    public LayerKind Kind { get; }

    public string Name { get; }

    public bool Visible { get; }

    public bool Locked { get; }

    public int ZIndex { get; }

    public ShapeGeometry Geometry { get; }

    public Transform Transform { get; }

    public double Opacity { get; }

    public string Fill { get; }

    public string? ParentId { get; }

    public string? TextContent { get; }

    public double? FontSize { get; }

    public double GetPropertyValue(AnimatableProperty property)
    {
        return property switch
        {
            AnimatableProperty.TransformX => Transform.X,
            AnimatableProperty.TransformY => Transform.Y,
            AnimatableProperty.TransformRotation => Transform.Rotation,
            AnimatableProperty.TransformScaleX => Transform.ScaleX,
            AnimatableProperty.TransformScaleY => Transform.ScaleY,
            AnimatableProperty.Opacity => Opacity,
            _ => 0.0,
        };
    }

    public LayerNode WithPropertyValue(AnimatableProperty property, double numericValue, string? colorValue = null)
    {
        return property switch
        {
            AnimatableProperty.TransformX => WithTransform(new Transform(numericValue, Transform.Y, Transform.Rotation, Transform.ScaleX, Transform.ScaleY)),
            AnimatableProperty.TransformY => WithTransform(new Transform(Transform.X, numericValue, Transform.Rotation, Transform.ScaleX, Transform.ScaleY)),
            AnimatableProperty.TransformRotation => WithTransform(new Transform(Transform.X, Transform.Y, numericValue, Transform.ScaleX, Transform.ScaleY)),
            AnimatableProperty.TransformScaleX => WithTransform(new Transform(Transform.X, Transform.Y, Transform.Rotation, numericValue, Transform.ScaleY)),
            AnimatableProperty.TransformScaleY => WithTransform(new Transform(Transform.X, Transform.Y, Transform.Rotation, Transform.ScaleX, numericValue)),
            AnimatableProperty.Opacity => WithOpacity(Math.Clamp(numericValue, 0.0, 1.0)),
            AnimatableProperty.Fill when !string.IsNullOrWhiteSpace(colorValue) => WithFill(colorValue),
            _ => this,
        };
    }

    public LayerNode WithTransform(Transform nextTransform)
    {
        return new LayerNode(Id, Kind, Name, Visible, Locked, ZIndex, Geometry, nextTransform, Opacity, Fill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithGeometry(ShapeGeometry nextGeometry)
    {
        return new LayerNode(Id, Kind, Name, Visible, Locked, ZIndex, nextGeometry, Transform, Opacity, Fill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithOpacity(double nextOpacity)
    {
        return new LayerNode(Id, Kind, Name, Visible, Locked, ZIndex, Geometry, Transform, nextOpacity, Fill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithFill(string nextFill)
    {
        return new LayerNode(Id, Kind, Name, Visible, Locked, ZIndex, Geometry, Transform, Opacity, nextFill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithVisibility(bool nextVisible)
    {
        return new LayerNode(Id, Kind, Name, nextVisible, Locked, ZIndex, Geometry, Transform, Opacity, Fill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithLocked(bool nextLocked)
    {
        return new LayerNode(Id, Kind, Name, Visible, nextLocked, ZIndex, Geometry, Transform, Opacity, Fill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithZIndex(int nextZIndex)
    {
        return new LayerNode(Id, Kind, Name, Visible, Locked, nextZIndex, Geometry, Transform, Opacity, Fill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithName(string nextName)
    {
        return new LayerNode(Id, Kind, nextName, Visible, Locked, ZIndex, Geometry, Transform, Opacity, Fill, ParentId, TextContent, FontSize);
    }

    public LayerNode WithParent(string? nextParentId)
    {
        return new LayerNode(Id, Kind, Name, Visible, Locked, ZIndex, Geometry, Transform, Opacity, Fill, nextParentId, TextContent, FontSize);
    }

    public LayerNode WithText(string nextText, double? nextFontSize = null)
    {
        return new LayerNode(Id, Kind, Name, Visible, Locked, ZIndex, Geometry, Transform, Opacity, Fill, ParentId, nextText, nextFontSize ?? FontSize);
    }
}

public sealed record Keyframe
{
    public Keyframe(
        int frameIndex,
        double value,
        EasingType easing = EasingType.Linear,
        string? colorValue = null,
        CubicBezierPoints? bezier = null)
    {
        if (frameIndex < 0)
        {
            throw new DomainValidationException("ERR_INVALID_FRAME", "Keyframe frameIndex must be >= 0.");
        }

        DomainGuard.EnsureFinite(value, nameof(value));

        FrameIndex = frameIndex;
        Value = value;
        Easing = easing;
        ColorValue = string.IsNullOrWhiteSpace(colorValue) ? null : DomainGuard.NormalizeHexColor(colorValue);
        Bezier = bezier;
    }

    public int FrameIndex { get; }

    public double Value { get; }

    public EasingType Easing { get; }

    public string? ColorValue { get; }

    public CubicBezierPoints? Bezier { get; }
}

public sealed record AnimationTrack
{
    public AnimationTrack(
        string id,
        string layerId,
        AnimatableProperty property,
        IEnumerable<Keyframe> keyframes)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("ERR_INVALID_TRACK_ID", "Track ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(layerId))
        {
            throw new DomainValidationException("ERR_INVALID_TRACK_LAYER", "Track layerId cannot be empty.");
        }

        ArgumentNullException.ThrowIfNull(keyframes);

        // Enforce uniqueness per frameIndex (last write wins) and sorted frame order.
        var byFrame = new SortedDictionary<int, Keyframe>();
        foreach (Keyframe kf in keyframes)
        {
            byFrame[kf.FrameIndex] = kf;
        }

        Id = id.Trim();
        LayerId = layerId.Trim();
        Property = property;
        Keyframes = byFrame.Values.ToList().AsReadOnly();
    }

    public string Id { get; }

    public string LayerId { get; }

    public AnimatableProperty Property { get; }

    public IReadOnlyList<Keyframe> Keyframes { get; }

    public AnimationTrack UpsertKeyframe(Keyframe keyframe)
    {
        ArgumentNullException.ThrowIfNull(keyframe);
        var updated = Keyframes.Where(k => k.FrameIndex != keyframe.FrameIndex).ToList();
        updated.Add(keyframe);
        return new AnimationTrack(Id, LayerId, Property, updated);
    }

    public AnimationTrack RemoveKeyframeAt(int frameIndex)
    {
        var updated = Keyframes.Where(k => k.FrameIndex != frameIndex).ToList();
        return new AnimationTrack(Id, LayerId, Property, updated);
    }
}

public sealed record ProjectDocument
{
    public const int CurrentSchemaVersion = 1;

    public ProjectDocument(
        string projectId,
        string name,
        Composition composition,
        IEnumerable<LayerNode> layers,
        IEnumerable<AnimationTrack> tracks,
        IEnumerable<AssetRef>? assets = null,
        DocumentRevision? revision = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new DomainValidationException(
                "ERR_UNSUPPORTED_SCHEMA",
                $"Unsupported project schema version '{schemaVersion}'. Expected {CurrentSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new DomainValidationException("ERR_INVALID_PROJECT_ID", "Project ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 256)
        {
            throw new DomainValidationException("ERR_INVALID_PROJECT_NAME", "Project name must be 1..256 characters.");
        }

        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(tracks);

        var layerList = layers
            .OrderBy(l => l.ZIndex)
            .Select((layer, idx) => layer.WithZIndex(idx))
            .ToList();

        var layerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LayerNode layer in layerList)
        {
            if (!layerIds.Add(layer.Id))
            {
                throw new DomainValidationException("ERR_DUPLICATE_LAYER_ID", $"Duplicate layer ID '{layer.Id}'.");
            }
        }

        var trackList = new List<AnimationTrack>();
        foreach (AnimationTrack track in tracks)
        {
            if (!layerIds.Contains(track.LayerId))
            {
                continue;
            }

            foreach (Keyframe kf in track.Keyframes)
            {
                if (kf.FrameIndex < 0 || kf.FrameIndex > composition.DurationFrames)
                {
                    throw new DomainValidationException(
                        "ERR_KEYFRAME_OUT_OF_RANGE",
                        $"Keyframe frameIndex {kf.FrameIndex} exceeds composition duration {composition.DurationFrames}.");
                }

                if (track.Property == AnimatableProperty.Opacity && (kf.Value < 0.0 || kf.Value > 1.0))
                {
                    throw new DomainValidationException(
                        "ERR_INVALID_OPACITY_KEYFRAME",
                        "Opacity keyframe value must be within [0, 1].");
                }
            }

            trackList.Add(track);
        }

        SchemaVersion = schemaVersion;
        ProjectId = projectId.Trim();
        Name = name.Trim();
        Composition = composition;
        Layers = layerList.AsReadOnly();
        Tracks = trackList.AsReadOnly();
        Assets = (assets ?? []).ToList().AsReadOnly();
        Revision = revision ?? DocumentRevision.Initial;
    }

    public int SchemaVersion { get; }

    public string ProjectId { get; }

    public string Name { get; }

    public Composition Composition { get; }

    public IReadOnlyList<LayerNode> Layers { get; }

    public IReadOnlyList<AnimationTrack> Tracks { get; }

    public IReadOnlyList<AssetRef> Assets { get; }

    public DocumentRevision Revision { get; }

    public static ProjectDocument CreateEmpty(
        string name = "Untitled Project",
        int width = 1920,
        int height = 1080,
        int fps = 30,
        int durationFrames = 90)
    {
        return new ProjectDocument(
            Guid.NewGuid().ToString("D"),
            name,
            new Composition(width, height, fps, durationFrames),
            [],
            [],
            [],
            DocumentRevision.Initial);
    }

    public static ProjectDocument CreateDefaultDemo()
    {
        var composition = new Composition(1920, 1080, 60, 240);

        var bgLayer = new LayerNode(
            "layer-bg",
            LayerKind.Rectangle,
            "Background",
            visible: true,
            locked: true,
            zIndex: 0,
            new ShapeGeometry(1920, 1080),
            new Transform(0, 0, 0, 1, 1),
            opacity: 1.0,
            fill: "#F7F8FE");

        var gridLayer = new LayerNode(
            "layer-grid",
            LayerKind.Rectangle,
            "Grid Lines",
            visible: true,
            locked: true,
            zIndex: 1,
            new ShapeGeometry(1840, 1000),
            new Transform(40, 40, 0, 1, 1),
            opacity: 0.45,
            fill: "#EEF0FA");

        var accentShapes = new LayerNode(
            "layer-accent-shapes",
            LayerKind.Ellipse,
            "Accent Shapes",
            visible: true,
            locked: false,
            zIndex: 2,
            new ShapeGeometry(156, 156),
            new Transform(710, 720, 0, 1, 1),
            opacity: 0.96,
            fill: "#DCE0F7");

        var headline = new LayerNode(
            "layer-headline",
            LayerKind.Text,
            "Headline",
            visible: true,
            locked: false,
            zIndex: 3,
            new ShapeGeometry(760, 210),
            new Transform(132, 290, 0, 1, 1),
            opacity: 1.0,
            fill: "#1B2130",
            textContent: "DESIGN IN MOTION",
            fontSize: 84);

        var subtitle = new LayerNode(
            "layer-subtitle",
            LayerKind.Text,
            "Subtitle",
            visible: true,
            locked: false,
            zIndex: 4,
            new ShapeGeometry(560, 60),
            new Transform(136, 560, 0, 1, 1),
            opacity: 1.0,
            fill: "#4B5568",
            textContent: "Make every frame matter.",
            fontSize: 33);

        var orbitalRing = new LayerNode(
            "layer-orbital-ring",
            LayerKind.Ellipse,
            "Orbital Ring",
            visible: true,
            locked: false,
            zIndex: 5,
            new ShapeGeometry(580, 560),
            new Transform(1040, 240, -15, 1, 1),
            opacity: 1.0,
            fill: "#5148EC");

        var trackPosX = new AnimationTrack(
            "track-ring-x",
            "layer-orbital-ring",
            AnimatableProperty.TransformX,
            [
                new Keyframe(15, 960, EasingType.CubicBezier, bezier: new CubicBezierPoints(0.4, 0.0, 0.2, 1.0)),
                new Keyframe(120, 1120, EasingType.CubicBezier, bezier: new CubicBezierPoints(0.4, 0.0, 0.2, 1.0)),
                new Keyframe(220, 1040, EasingType.EaseInOut),
            ]);

        var trackScaleX = new AnimationTrack(
            "track-ring-scale",
            "layer-orbital-ring",
            AnimatableProperty.TransformScaleX,
            [
                new Keyframe(60, 1.0, EasingType.EaseOut),
                new Keyframe(105, 1.08, EasingType.EaseInOut),
                new Keyframe(180, 1.0, EasingType.EaseInOut),
            ]);

        var trackRot = new AnimationTrack(
            "track-ring-rot",
            "layer-orbital-ring",
            AnimatableProperty.TransformRotation,
            [
                new Keyframe(40, -18, EasingType.CubicBezier, bezier: new CubicBezierPoints(0.4, 0.0, 0.2, 1.0)),
                new Keyframe(150, -8, EasingType.EaseInOut),
            ]);

        var trackOp = new AnimationTrack(
            "track-ring-op",
            "layer-orbital-ring",
            AnimatableProperty.Opacity,
            [
                new Keyframe(18, 0.92, EasingType.EaseOut),
                new Keyframe(198, 1.0, EasingType.Linear),
            ]);

        return new ProjectDocument(
            "f4829005-63e3-4f2f-a49a-babce0e29ec1",
            "Brand Intro / Scene 01",
            composition,
            [bgLayer, gridLayer, accentShapes, headline, subtitle, orbitalRing],
            [trackPosX, trackScaleX, trackRot, trackOp],
            [],
            DocumentRevision.Initial);
    }
}
