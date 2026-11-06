using System.Globalization;
using MotionStudio.Application.Contracts;
using MotionStudio.Domain.Models;
using MotionStudio.Domain.Services;

namespace MotionStudio.Application.Services;

public sealed class EditorWorkspaceService
{
    public const int DesktopWindowWidth = 1536;
    public const int DesktopWindowHeight = 1024;
    public const int MinDesktopWindowWidth = 1280;
    public const int MinDesktopWindowHeight = 800;
    public const int DefaultHeaderHeight = 44;
    public const int DefaultToolbarHeight = 50;
    public const int DefaultToolRailWidth = 60;
    public const int DefaultLeftPanelWidth = 270;
    public const int DefaultInspectorWidth = 320;
    public const int DefaultTimelineHeight = 330;
    public const int DefaultStatusHeight = 28;
    public const int DefaultLayerRowHeight = 36;
    public const int DefaultControlBorderRadius = 4;
    public const int MaxHistoryDepth = 100;
    public const double MinCanvasZoom = 0.1;
    public const double MaxCanvasZoom = 8.0;
    public const double PasteOffsetDelta = 20.0;

    private readonly IProjectRepository _repository;
    private readonly ISvgSanitizer _svgSanitizer;
    private readonly IFrameExporter _frameExporter;
    private readonly LinkedList<HistoryEntry> _undoStack = new();
    private readonly Stack<HistoryEntry> _redoStack = new();
    private readonly List<string> _selectedLayerIds = [];
    private List<ClipboardItem> _clipboard = [];
    private int _shapeCounter = 3;

    public EditorWorkspaceService(
        IProjectRepository repository,
        ISvgSanitizer svgSanitizer,
        IFrameExporter frameExporter)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(svgSanitizer);
        ArgumentNullException.ThrowIfNull(frameExporter);

        _repository = repository;
        _svgSanitizer = svgSanitizer;
        _frameExporter = frameExporter;

        CurrentDocument = ProjectDocument.CreateDefaultDemo();
        CurrentFrameIndex = 60;
        IsLooping = true;
        CanvasZoom = 0.75;
        ShowGrid = true;
        ShowCheckerboard = false;
        LastSavedFileName = "basic-project.motion.json";
        LayerNode? defaultSelected = CurrentDocument.Layers.LastOrDefault(l => !l.Locked) ?? CurrentDocument.Layers.FirstOrDefault();
        if (defaultSelected is not null)
        {
            _selectedLayerIds.Add(defaultSelected.Id);
        }
    }

    public event Action? StateChanged;

    public ProjectDocument CurrentDocument { get; private set; }

    public int CurrentFrameIndex { get; private set; }

    public bool IsPlaying { get; private set; }

    public bool IsLooping { get; private set; }

    public double CanvasZoom { get; private set; }

    public double CanvasPanX { get; private set; }

    public double CanvasPanY { get; private set; }

    public bool ShowGrid { get; private set; }

    public bool ShowCheckerboard { get; private set; }

    public bool IsDirty { get; private set; }

    public string LastSavedFileName { get; private set; }

    public string? StatusBannerMessage { get; private set; }

    public string? StatusBannerErrorCode { get; private set; }

    public bool StatusBannerIsError { get; private set; }

    public int CommandCommitCount { get; private set; }

    public int UndoCount => _undoStack.Count;

    public int RedoCount => _redoStack.Count;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public bool HasClipboard => _clipboard.Count > 0;

    public IReadOnlyList<string> SelectedLayerIds => _selectedLayerIds.AsReadOnly();

    public DocumentSnapshotDto GetSnapshot()
    {
        return SnapshotMapper.ToDto(
            CurrentDocument,
            CurrentFrameIndex,
            IsPlaying,
            IsLooping,
            CanvasZoom,
            CanvasPanX,
            CanvasPanY,
            ShowGrid,
            ShowCheckerboard,
            _selectedLayerIds);
    }

    public IReadOnlyList<EvaluatedLayerState> GetEvaluatedLayersAtCurrentFrame()
    {
        return TimelineEvaluator.EvaluateDocumentAtFrame(CurrentDocument, CurrentFrameIndex);
    }

    public LayerNode? GetPrimarySelectedLayer()
    {
        if (_selectedLayerIds.Count == 0)
        {
            return null;
        }

        string primaryId = _selectedLayerIds[0];
        return CurrentDocument.Layers.FirstOrDefault(l => string.Equals(l.Id, primaryId, StringComparison.Ordinal));
    }

    public bool IsSelectedPropertyMixed(AnimatableProperty property)
    {
        if (_selectedLayerIds.Count <= 1)
        {
            return false;
        }

        var selectedLayers = CurrentDocument.Layers
            .Where(l => _selectedLayerIds.Contains(l.Id, StringComparer.Ordinal))
            .ToList();

        if (selectedLayers.Count <= 1)
        {
            return false;
        }

        if (property == AnimatableProperty.Fill)
        {
            string firstFill = selectedLayers[0].Fill;
            return selectedLayers.Skip(1).Any(l => !string.Equals(l.Fill, firstFill, StringComparison.OrdinalIgnoreCase));
        }

        double firstVal = selectedLayers[0].GetPropertyValue(property);
        return selectedLayers.Skip(1).Any(l => Math.Abs(l.GetPropertyValue(property) - firstVal) > 1e-4);
    }

    public void DismissBanner()
    {
        StatusBannerMessage = null;
        StatusBannerErrorCode = null;
        StatusBannerIsError = false;
        NotifyStateChanged();
    }

    public CommandResult CreateNewProject(string name, int width, int height, int fps, int durationFrames)
    {
        try
        {
            var nextDoc = ProjectDocument.CreateEmpty(name, width, height, fps, durationFrames);
            _undoStack.Clear();
            _redoStack.Clear();
            _selectedLayerIds.Clear();
            CurrentDocument = nextDoc;
            CurrentFrameIndex = 0;
            IsPlaying = false;
            IsDirty = false;
            LastSavedFileName = "untitled.motion.json";
            SetInfoBanner($"Created new project '{nextDoc.Name}' ({width}x{height} @ {fps}fps, {durationFrames}f).");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult LoadDemoProject()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _selectedLayerIds.Clear();
        CurrentDocument = ProjectDocument.CreateDefaultDemo();
        CurrentFrameIndex = 60;
        IsPlaying = false;
        IsDirty = false;
        CanvasZoom = 0.75;
        LastSavedFileName = "basic-project.motion.json";
        LayerNode? defaultSelected = CurrentDocument.Layers.LastOrDefault(l => !l.Locked) ?? CurrentDocument.Layers.FirstOrDefault();
        if (defaultSelected is not null)
        {
            _selectedLayerIds.Add(defaultSelected.Id);
        }

        SetInfoBanner("Loaded Brand Intro / Scene 01 demo project.");
        return CommandResult.Success(GetSnapshot());
    }

    public void SeekFrame(int frameIndex)
    {
        CurrentFrameIndex = CurrentDocument.Composition.ClampFrame(frameIndex);
        NotifyStateChanged();
    }

    public void SetPlaybackState(bool isPlaying, bool? isLooping = null)
    {
        IsPlaying = isPlaying;
        if (isLooping.HasValue)
        {
            IsLooping = isLooping.Value;
        }

        NotifyStateChanged();
    }

    public void StopPlayback(bool resetToStart = true)
    {
        IsPlaying = false;
        if (resetToStart)
        {
            CurrentFrameIndex = 0;
        }

        NotifyStateChanged();
    }

    public CommandResult SetViewportZoom(double zoom, double? panX = null, double? panY = null)
    {
        try
        {
            DomainGuard.EnsureFinite(zoom, nameof(zoom));
            CanvasZoom = Math.Clamp(zoom, MinCanvasZoom, MaxCanvasZoom);
            if (panX.HasValue)
            {
                DomainGuard.EnsureFinite(panX.Value, nameof(panX));
                CanvasPanX = panX.Value;
            }

            if (panY.HasValue)
            {
                DomainGuard.EnsureFinite(panY.Value, nameof(panY));
                CanvasPanY = panY.Value;
            }

            NotifyStateChanged();
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
        NotifyStateChanged();
    }

    public void ToggleCheckerboard()
    {
        ShowCheckerboard = !ShowCheckerboard;
        NotifyStateChanged();
    }

    public void SelectLayer(string layerId, bool appendSelection = false)
    {
        if (string.IsNullOrWhiteSpace(layerId))
        {
            return;
        }

        if (!CurrentDocument.Layers.Any(l => string.Equals(l.Id, layerId, StringComparison.Ordinal)))
        {
            return;
        }

        if (!appendSelection)
        {
            _selectedLayerIds.Clear();
            _selectedLayerIds.Add(layerId);
        }
        else if (_selectedLayerIds.Contains(layerId, StringComparer.Ordinal))
        {
            _selectedLayerIds.Remove(layerId);
        }
        else
        {
            _selectedLayerIds.Add(layerId);
        }

        NotifyStateChanged();
    }

    public void ClearSelection()
    {
        _selectedLayerIds.Clear();
        NotifyStateChanged();
    }

    public CommandResult CreateLayer(LayerKind kind, string? customName = null)
    {
        try
        {
            _shapeCounter++;
            string id = $"layer-{kind.ToWireName()}-{_shapeCounter}-{Guid.NewGuid().ToString("N")[..6]}";
            string name = string.IsNullOrWhiteSpace(customName)
                ? $"{kind} {_shapeCounter}"
                : customName.Trim();

            var geometry = kind switch
            {
                LayerKind.Ellipse => new ShapeGeometry(160, 160),
                LayerKind.Text => new ShapeGeometry(360, 56),
                _ => new ShapeGeometry(240, 140),
            };

            string fill = kind switch
            {
                LayerKind.Ellipse => "#48D597",
                LayerKind.Text => "#E7E9ED",
                _ => "#6B9CFF",
            };

            double offsetX = 180 + ((_shapeCounter % 5) * 48);
            double offsetY = 160 + ((_shapeCounter % 5) * 36);

            var newLayer = new LayerNode(
                id,
                kind,
                name,
                visible: true,
                locked: false,
                zIndex: CurrentDocument.Layers.Count,
                geometry,
                new Transform(offsetX, offsetY, 0, 1, 1),
                opacity: 1.0,
                fill: fill,
                textContent: kind == LayerKind.Text ? name : null,
                fontSize: kind == LayerKind.Text ? 36.0 : null);

            var nextLayers = CurrentDocument.Layers.ToList();
            nextLayers.Add(newLayer);

            ApplyDocumentMutation($"CreateLayer:{name}", nextLayers, CurrentDocument.Tracks);
            _selectedLayerIds.Clear();
            _selectedLayerIds.Add(newLayer.Id);
            SetInfoBanner($"Added {kind.ToWireName()} layer '{newLayer.Name}'.");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult DeleteSelectedLayers(long? baseRevision = null)
    {
        if (!ValidateRevision(baseRevision, out CommandResult? conflict))
        {
            return conflict!;
        }

        if (_selectedLayerIds.Count == 0)
        {
            return CommandResult.Failure("ERR_NO_SELECTION", "No layer selected to delete.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        var toDelete = new HashSet<string>(_selectedLayerIds, StringComparer.Ordinal);
        var remainingLayers = CurrentDocument.Layers
            .Where(l => !toDelete.Contains(l.Id))
            .ToList();
        var remainingTracks = CurrentDocument.Tracks
            .Where(t => !toDelete.Contains(t.LayerId))
            .ToList();

        _selectedLayerIds.Clear();
        if (remainingLayers.Count > 0)
        {
            _selectedLayerIds.Add(remainingLayers[0].Id);
        }

        ApplyDocumentMutation("DeleteSelectedLayers", remainingLayers, remainingTracks);
        SetInfoBanner($"Deleted {toDelete.Count} layer(s).");
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult ToggleLayerVisibility(string layerId)
    {
        LayerNode? target = CurrentDocument.Layers.FirstOrDefault(l => string.Equals(l.Id, layerId, StringComparison.Ordinal));
        if (target is null)
        {
            return CommandResult.Failure("ERR_LAYER_NOT_FOUND", $"Layer '{layerId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        var nextLayers = CurrentDocument.Layers
            .Select(l => string.Equals(l.Id, layerId, StringComparison.Ordinal) ? l.WithVisibility(!l.Visible) : l)
            .ToList();

        ApplyDocumentMutation($"ToggleVisibility:{target.Name}", nextLayers, CurrentDocument.Tracks);
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult ToggleLayerLock(string layerId)
    {
        LayerNode? target = CurrentDocument.Layers.FirstOrDefault(l => string.Equals(l.Id, layerId, StringComparison.Ordinal));
        if (target is null)
        {
            return CommandResult.Failure("ERR_LAYER_NOT_FOUND", $"Layer '{layerId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        var nextLayers = CurrentDocument.Layers
            .Select(l => string.Equals(l.Id, layerId, StringComparison.Ordinal) ? l.WithLocked(!l.Locked) : l)
            .ToList();

        ApplyDocumentMutation($"ToggleLock:{target.Name}", nextLayers, CurrentDocument.Tracks);
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult MoveLayerOrder(string layerId, int delta)
    {
        var ordered = CurrentDocument.Layers.OrderBy(l => l.ZIndex).ToList();
        int index = ordered.FindIndex(l => string.Equals(l.Id, layerId, StringComparison.Ordinal));
        if (index < 0)
        {
            return CommandResult.Failure("ERR_LAYER_NOT_FOUND", $"Layer '{layerId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        int targetIndex = Math.Clamp(index + delta, 0, ordered.Count - 1);
        if (targetIndex == index)
        {
            return CommandResult.Success(GetSnapshot());
        }

        LayerNode item = ordered[index];
        ordered.RemoveAt(index);
        ordered.Insert(targetIndex, item);

        var reindexed = ordered.Select((l, idx) => l.WithZIndex(idx)).ToList();
        ApplyDocumentMutation($"ReorderLayer:{item.Name}", reindexed, CurrentDocument.Tracks);
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult GroupSelectedLayers(string groupName = "Layer Group")
    {
        if (_selectedLayerIds.Count == 0)
        {
            return CommandResult.Failure("ERR_NO_SELECTION", "Select at least one layer to group.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            _shapeCounter++;
            string groupId = $"layer-group-{_shapeCounter}-{Guid.NewGuid().ToString("N")[..4]}";
            var groupLayer = new LayerNode(
                groupId,
                LayerKind.Group,
                string.IsNullOrWhiteSpace(groupName) ? $"Group {_shapeCounter}" : groupName.Trim(),
                visible: true,
                locked: false,
                zIndex: CurrentDocument.Layers.Count,
                new ShapeGeometry(400, 300),
                new Transform(0, 0, 0, 1, 1),
                opacity: 1.0,
                fill: "#6B9CFF");

            var selectedSet = new HashSet<string>(_selectedLayerIds, StringComparer.Ordinal);
            var nextLayers = CurrentDocument.Layers
                .Select(l => selectedSet.Contains(l.Id) ? l.WithParent(groupId) : l)
                .ToList();
            nextLayers.Add(groupLayer);

            ApplyDocumentMutation($"GroupLayers:{groupLayer.Name}", nextLayers, CurrentDocument.Tracks);
            SetInfoBanner($"Grouped {selectedSet.Count} layer(s) into '{groupLayer.Name}'.");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult RenameLayer(string layerId, string newName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new DomainValidationException("ERR_INVALID_LAYER_NAME", "Layer name cannot be empty.");
            }

            var nextLayers = CurrentDocument.Layers
                .Select(l => string.Equals(l.Id, layerId, StringComparison.Ordinal) ? l.WithName(newName) : l)
                .ToList();

            ApplyDocumentMutation($"RenameLayer:{newName}", nextLayers, CurrentDocument.Tracks);
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult CommitPointerTransform(PointerCommitPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!ValidateRevision(payload.BaseRevision, out CommandResult? conflict))
        {
            return conflict!;
        }

        LayerNode? target = CurrentDocument.Layers.FirstOrDefault(l => string.Equals(l.Id, payload.LayerId, StringComparison.Ordinal));
        if (target is null)
        {
            return CommandResult.Failure("ERR_LAYER_NOT_FOUND", $"Layer '{payload.LayerId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        if (target.Locked || !target.Visible)
        {
            return CommandResult.Failure("ERR_LAYER_LOCKED", $"Layer '{target.Name}' is locked or hidden and cannot be transformed.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            IsPlaying = false;
            var nextTransform = new Transform(payload.X, payload.Y, payload.Rotation, payload.ScaleX, payload.ScaleY);
            var nextLayers = CurrentDocument.Layers
                .Select(l => string.Equals(l.Id, payload.LayerId, StringComparison.Ordinal) ? l.WithTransform(nextTransform) : l)
                .ToList();

            var nextTracks = UpdateTracksIfAnimatedAtCurrentFrame(
                CurrentDocument.Tracks,
                payload.LayerId,
                nextTransform,
                CurrentFrameIndex);

            ApplyDocumentMutation($"MoveLayerTransform:{target.Name}", nextLayers, nextTracks);
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult SetSelectedProperty(
        AnimatableProperty property,
        double numericValue,
        string? colorValue = null,
        long? baseRevision = null)
    {
        if (!ValidateRevision(baseRevision, out CommandResult? conflict))
        {
            return conflict!;
        }

        if (_selectedLayerIds.Count == 0)
        {
            return CommandResult.Failure("ERR_NO_SELECTION", "Select a layer before editing properties.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            IsPlaying = false;
            var selectedSet = new HashSet<string>(_selectedLayerIds, StringComparer.Ordinal);
            if (CurrentDocument.Layers.Any(l => selectedSet.Contains(l.Id) && l.Locked))
            {
                return CommandResult.Failure("ERR_LAYER_LOCKED", "Cannot edit properties of a locked layer.", CurrentDocument.Revision.Value, GetSnapshot());
            }

            var nextLayers = CurrentDocument.Layers
                .Select(l => selectedSet.Contains(l.Id) ? l.WithPropertyValue(property, numericValue, colorValue) : l)
                .ToList();

            var nextTracks = CurrentDocument.Tracks.ToList();
            foreach (string layerId in selectedSet)
            {
                int trackIdx = nextTracks.FindIndex(t =>
                    string.Equals(t.LayerId, layerId, StringComparison.Ordinal) &&
                    t.Property == property);

                if (trackIdx >= 0)
                {
                    AnimationTrack track = nextTracks[trackIdx];
                    Keyframe? existingAtFrame = track.Keyframes.FirstOrDefault(k => k.FrameIndex == CurrentFrameIndex);
                    EasingType easing = existingAtFrame?.Easing ?? EasingType.Linear;
                    var updatedKeyframe = new Keyframe(
                        CurrentFrameIndex,
                        property == AnimatableProperty.Opacity ? Math.Clamp(numericValue, 0.0, 1.0) : numericValue,
                        easing,
                        property == AnimatableProperty.Fill ? colorValue : null,
                        existingAtFrame?.Bezier);
                    nextTracks[trackIdx] = track.UpsertKeyframe(updatedKeyframe);
                }
            }

            ApplyDocumentMutation($"SetProperty:{property.ToWireName()}", nextLayers, nextTracks);
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult SetSelectedGeometry(double width, double height)
    {
        if (_selectedLayerIds.Count == 0)
        {
            return CommandResult.Failure("ERR_NO_SELECTION", "No layer selected.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            var nextGeo = new ShapeGeometry(width, height);
            var selectedSet = new HashSet<string>(_selectedLayerIds, StringComparer.Ordinal);
            if (CurrentDocument.Layers.Any(l => selectedSet.Contains(l.Id) && l.Locked))
            {
                return CommandResult.Failure("ERR_LAYER_LOCKED", "Selected layer is locked.", CurrentDocument.Revision.Value, GetSnapshot());
            }

            var nextLayers = CurrentDocument.Layers
                .Select(l => selectedSet.Contains(l.Id) ? l.WithGeometry(nextGeo) : l)
                .ToList();

            ApplyDocumentMutation("SetGeometry", nextLayers, CurrentDocument.Tracks);
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult SetSelectedText(string textContent, double fontSize)
    {
        if (_selectedLayerIds.Count == 0)
        {
            return CommandResult.Failure("ERR_NO_SELECTION", "No layer selected.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            DomainGuard.EnsureFinite(fontSize, nameof(fontSize));
            if (fontSize < 6 || fontSize > 400)
            {
                throw new DomainValidationException("ERR_INVALID_FONT_SIZE", "Font size must be between 6 and 400.");
            }

            var selectedSet = new HashSet<string>(_selectedLayerIds, StringComparer.Ordinal);
            var nextLayers = CurrentDocument.Layers
                .Select(l => selectedSet.Contains(l.Id) && l.Kind == LayerKind.Text ? l.WithText(textContent, fontSize) : l)
                .ToList();

            ApplyDocumentMutation("SetText", nextLayers, CurrentDocument.Tracks);
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult UpsertKeyframe(
        string layerId,
        AnimatableProperty property,
        int frameIndex,
        double value,
        EasingType easing = EasingType.Linear,
        string? colorValue = null,
        CubicBezierPoints? bezier = null,
        long? baseRevision = null)
    {
        if (!ValidateRevision(baseRevision, out CommandResult? conflict))
        {
            return conflict!;
        }

        LayerNode? layer = CurrentDocument.Layers.FirstOrDefault(l => string.Equals(l.Id, layerId, StringComparison.Ordinal));
        if (layer is null)
        {
            return CommandResult.Failure("ERR_LAYER_NOT_FOUND", $"Layer '{layerId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        if (layer.Locked)
        {
            return CommandResult.Failure("ERR_LAYER_LOCKED", $"Layer '{layer.Name}' is locked.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            if (frameIndex < 0 || frameIndex > CurrentDocument.Composition.DurationFrames)
            {
                throw new DomainValidationException(
                    "ERR_KEYFRAME_OUT_OF_RANGE",
                    $"Keyframe frame {frameIndex} must be within [0, {CurrentDocument.Composition.DurationFrames}].");
            }

            var keyframe = new Keyframe(
                frameIndex,
                property == AnimatableProperty.Opacity ? Math.Clamp(value, 0.0, 1.0) : value,
                easing,
                property == AnimatableProperty.Fill ? (colorValue ?? layer.Fill) : null,
                bezier);

            var nextTracks = CurrentDocument.Tracks.ToList();
            int existingIdx = nextTracks.FindIndex(t =>
                string.Equals(t.LayerId, layerId, StringComparison.Ordinal) &&
                t.Property == property);

            if (existingIdx >= 0)
            {
                nextTracks[existingIdx] = nextTracks[existingIdx].UpsertKeyframe(keyframe);
            }
            else
            {
                string trackId = $"track-{property.ToWireName().Replace('.', '-')}-{Guid.NewGuid().ToString("N")[..6]}";
                nextTracks.Add(new AnimationTrack(trackId, layerId, property, [keyframe]));
            }

            var nextLayers = CurrentDocument.Layers
                .Select(l => string.Equals(l.Id, layerId, StringComparison.Ordinal) && frameIndex == CurrentFrameIndex
                    ? l.WithPropertyValue(property, keyframe.Value, keyframe.ColorValue)
                    : l)
                .ToList();

            ApplyDocumentMutation($"UpsertKeyframe:{layer.Name}.{property.ToWireName()}@{frameIndex}f", nextLayers, nextTracks);
            SetInfoBanner($"Keyframe set on '{layer.Name}' ({property.ToWireName()}) at frame {frameIndex}.");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult MoveKeyframe(string trackId, int fromFrameIndex, int toFrameIndex)
    {
        AnimationTrack? track = CurrentDocument.Tracks.FirstOrDefault(t => string.Equals(t.Id, trackId, StringComparison.Ordinal));
        if (track is null)
        {
            return CommandResult.Failure("ERR_TRACK_NOT_FOUND", $"Track '{trackId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        Keyframe? source = track.Keyframes.FirstOrDefault(k => k.FrameIndex == fromFrameIndex);
        if (source is null)
        {
            return CommandResult.Failure("ERR_KEYFRAME_NOT_FOUND", $"No keyframe at frame {fromFrameIndex}.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            int clampedTarget = CurrentDocument.Composition.ClampFrame(toFrameIndex);
            var moved = new Keyframe(clampedTarget, source.Value, source.Easing, source.ColorValue, source.Bezier);
            AnimationTrack updatedTrack = track.RemoveKeyframeAt(fromFrameIndex).UpsertKeyframe(moved);

            var nextTracks = CurrentDocument.Tracks
                .Select(t => string.Equals(t.Id, trackId, StringComparison.Ordinal) ? updatedTrack : t)
                .ToList();

            ApplyDocumentMutation($"MoveKeyframe:{track.Property.ToWireName()}:{fromFrameIndex}->{clampedTarget}", CurrentDocument.Layers, nextTracks);
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult DeleteKeyframe(string trackId, int frameIndex)
    {
        AnimationTrack? track = CurrentDocument.Tracks.FirstOrDefault(t => string.Equals(t.Id, trackId, StringComparison.Ordinal));
        if (track is null)
        {
            return CommandResult.Failure("ERR_TRACK_NOT_FOUND", $"Track '{trackId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        AnimationTrack updatedTrack = track.RemoveKeyframeAt(frameIndex);
        var nextTracks = CurrentDocument.Tracks
            .Select(t => string.Equals(t.Id, trackId, StringComparison.Ordinal) ? updatedTrack : t)
            .Where(t => t.Keyframes.Count > 0)
            .ToList();

        ApplyDocumentMutation($"DeleteKeyframe:{track.Property.ToWireName()}@{frameIndex}f", CurrentDocument.Layers, nextTracks);
        SetInfoBanner($"Deleted keyframe at frame {frameIndex}.");
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult CopySelectedLayers()
    {
        if (_selectedLayerIds.Count == 0)
        {
            return CommandResult.Failure("ERR_NO_SELECTION", "Select at least one layer to copy.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        var selectedSet = new HashSet<string>(_selectedLayerIds, StringComparer.Ordinal);
        _clipboard = CurrentDocument.Layers
            .Where(l => selectedSet.Contains(l.Id))
            .Select(layer => new ClipboardItem(
                layer,
                CurrentDocument.Tracks.Where(t => string.Equals(t.LayerId, layer.Id, StringComparison.Ordinal)).ToList()))
            .ToList();

        SetInfoBanner($"Copied {_clipboard.Count} layer(s) to clipboard.");
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult PasteClipboardLayers()
    {
        if (_clipboard.Count == 0)
        {
            return CommandResult.Failure("ERR_EMPTY_CLIPBOARD", "Clipboard is empty.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            var nextLayers = CurrentDocument.Layers.ToList();
            var nextTracks = CurrentDocument.Tracks.ToList();
            var newSelectedIds = new List<string>();

            foreach (ClipboardItem item in _clipboard)
            {
                _shapeCounter++;
                string newLayerId = $"layer-copy-{_shapeCounter}-{Guid.NewGuid().ToString("N")[..5]}";
                LayerNode src = item.Layer;
                var shiftedTransform = new Transform(
                    src.Transform.X + PasteOffsetDelta,
                    src.Transform.Y + PasteOffsetDelta,
                    src.Transform.Rotation,
                    src.Transform.ScaleX,
                    src.Transform.ScaleY);

                var clonedLayer = new LayerNode(
                    newLayerId,
                    src.Kind,
                    $"{src.Name} Copy",
                    src.Visible,
                    src.Locked,
                    nextLayers.Count,
                    src.Geometry,
                    shiftedTransform,
                    src.Opacity,
                    src.Fill,
                    src.ParentId,
                    src.TextContent,
                    src.FontSize);

                nextLayers.Add(clonedLayer);
                newSelectedIds.Add(newLayerId);

                foreach (AnimationTrack track in item.Tracks)
                {
                    string newTrackId = $"track-copy-{Guid.NewGuid().ToString("N")[..6]}";
                    var shiftedKeyframes = track.Keyframes
                        .Select(k => track.Property is AnimatableProperty.TransformX or AnimatableProperty.TransformY
                            ? new Keyframe(k.FrameIndex, k.Value + PasteOffsetDelta, k.Easing, k.ColorValue, k.Bezier)
                            : k)
                        .ToList();
                    nextTracks.Add(new AnimationTrack(newTrackId, newLayerId, track.Property, shiftedKeyframes));
                }
            }

            _selectedLayerIds.Clear();
            _selectedLayerIds.AddRange(newSelectedIds);
            ApplyDocumentMutation("PasteLayers", nextLayers, nextTracks);
            SetInfoBanner($"Pasted {newSelectedIds.Count} layer(s).");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public CommandResult ApplyPreset(string layerId, MotionPresetKind preset)
    {
        LayerNode? layer = CurrentDocument.Layers.FirstOrDefault(l => string.Equals(l.Id, layerId, StringComparison.Ordinal));
        if (layer is null)
        {
            return CommandResult.Failure("ERR_LAYER_NOT_FOUND", $"Layer '{layerId}' not found.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        if (layer.Locked)
        {
            return CommandResult.Failure("ERR_LAYER_LOCKED", $"Layer '{layer.Name}' is locked.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        int endFrame = Math.Min(30, CurrentDocument.Composition.DurationFrames);
        int midFrame = Math.Max(1, endFrame / 2);

        var nextTracks = CurrentDocument.Tracks.ToList();

        void UpsertTrack(AnimatableProperty prop, IReadOnlyList<Keyframe> kfs)
        {
            int idx = nextTracks.FindIndex(t => string.Equals(t.LayerId, layerId, StringComparison.Ordinal) && t.Property == prop);
            string id = idx >= 0 ? nextTracks[idx].Id : $"track-preset-{Guid.NewGuid().ToString("N")[..6]}";
            var merged = idx >= 0 ? nextTracks[idx].Keyframes.ToList() : [];
            foreach (Keyframe kf in kfs)
            {
                merged.RemoveAll(existing => existing.FrameIndex == kf.FrameIndex);
                merged.Add(kf);
            }

            var updated = new AnimationTrack(id, layerId, prop, merged);
            if (idx >= 0)
            {
                nextTracks[idx] = updated;
            }
            else
            {
                nextTracks.Add(updated);
            }
        }

        switch (preset)
        {
            case MotionPresetKind.FadeIn:
                UpsertTrack(AnimatableProperty.Opacity, [
                    new Keyframe(0, 0.0, EasingType.EaseOut),
                    new Keyframe(endFrame, 1.0, EasingType.Linear),
                ]);
                break;
            case MotionPresetKind.SlideInRight:
                UpsertTrack(AnimatableProperty.TransformX, [
                    new Keyframe(0, layer.Transform.X - 240.0, EasingType.EaseOut),
                    new Keyframe(endFrame, layer.Transform.X, EasingType.Linear),
                ]);
                break;
            case MotionPresetKind.PopScale:
                UpsertTrack(AnimatableProperty.TransformScaleX, [
                    new Keyframe(0, 0.4, EasingType.EaseOut),
                    new Keyframe(midFrame, 1.15, EasingType.EaseInOut),
                    new Keyframe(endFrame, 1.0, EasingType.Linear),
                ]);
                UpsertTrack(AnimatableProperty.TransformScaleY, [
                    new Keyframe(0, 0.4, EasingType.EaseOut),
                    new Keyframe(midFrame, 1.15, EasingType.EaseInOut),
                    new Keyframe(endFrame, 1.0, EasingType.Linear),
                ]);
                break;
            case MotionPresetKind.Pulse:
                UpsertTrack(AnimatableProperty.Opacity, [
                    new Keyframe(0, 1.0, EasingType.EaseInOut),
                    new Keyframe(midFrame, 0.35, EasingType.EaseInOut),
                    new Keyframe(endFrame, 1.0, EasingType.Linear),
                ]);
                break;
        }

        ApplyDocumentMutation($"ApplyPreset:{preset}", CurrentDocument.Layers, nextTracks);
        SetInfoBanner($"Applied preset '{preset}' to '{layer.Name}'.");
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult ImportSvgAsset(string layerName, string rawSvg)
    {
        SvgSanitizeResult result = _svgSanitizer.InspectAndSanitize(rawSvg);
        if (!result.IsSafe)
        {
            StatusBannerErrorCode = result.ErrorCode ?? "ERR_UNSAFE_SVG";
            StatusBannerMessage = result.ErrorMessage ?? "SVG rejected by security sanitizer.";
            StatusBannerIsError = true;
            NotifyStateChanged();
            return CommandResult.Failure(StatusBannerErrorCode, StatusBannerMessage, CurrentDocument.Revision.Value, GetSnapshot());
        }

        try
        {
            _shapeCounter++;
            string assetId = $"asset-svg-{_shapeCounter}";
            string layerId = $"layer-svg-{_shapeCounter}-{Guid.NewGuid().ToString("N")[..4]}";
            var newAsset = new AssetRef(assetId, layerName, "svg", result.SanitizedSvg ?? string.Empty);
            var newLayer = new LayerNode(
                layerId,
                LayerKind.Rectangle,
                string.IsNullOrWhiteSpace(layerName) ? $"SVG Layer {_shapeCounter}" : layerName.Trim(),
                visible: true,
                locked: false,
                zIndex: CurrentDocument.Layers.Count,
                new ShapeGeometry(result.Width, result.Height),
                new Transform(300, 260, 0, 1, 1),
                opacity: 1.0,
                fill: result.FillColor);

            var nextLayers = CurrentDocument.Layers.ToList();
            nextLayers.Add(newLayer);
            var nextAssets = CurrentDocument.Assets.ToList();
            nextAssets.Add(newAsset);

            ApplyDocumentMutation($"ImportSvg:{newLayer.Name}", nextLayers, CurrentDocument.Tracks, nextAssets);
            _selectedLayerIds.Clear();
            _selectedLayerIds.Add(newLayer.Id);
            SetInfoBanner($"Sanitized and imported SVG layer '{newLayer.Name}'.");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
    }

    public string ExportCurrentFrameSvg()
    {
        string svg = _frameExporter.ExportFrameToSvg(CurrentDocument, CurrentFrameIndex);
        SetInfoBanner($"Exported clean SVG snapshot at frame {CurrentFrameIndex}.");
        return svg;
    }

    public byte[] ExportCurrentFramePngBytes()
    {
        byte[] png = _frameExporter.ExportFrameToPngBytes(CurrentDocument, CurrentFrameIndex);
        SetInfoBanner($"Exported SkiaSharp PNG snapshot ({png.Length:N0} bytes) at frame {CurrentFrameIndex}.");
        return png;
    }

    public SkiaRenderMetrics RunSkiaBenchmark(int nodeCount = 100)
    {
        SkiaRenderMetrics metrics = _frameExporter.BenchmarkHundredNodeRender(nodeCount);
        SetInfoBanner($"SkiaSharp {metrics.NodeCount}-node render: eval={metrics.SceneEvaluationMs:F2}ms, draw+encode={metrics.RenderMs:F2}ms.");
        return metrics;
    }

    public CommandResult Undo()
    {
        if (_undoStack.Count == 0)
        {
            return CommandResult.Failure("ERR_NOTHING_TO_UNDO", "Undo stack is empty.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        IsPlaying = false;
        HistoryEntry entry = _undoStack.Last!.Value;
        _undoStack.RemoveLast();

        _redoStack.Push(new HistoryEntry(entry.Label, CurrentDocument));
        CurrentDocument = new ProjectDocument(
            entry.Document.ProjectId,
            entry.Document.Name,
            entry.Document.Composition,
            entry.Document.Layers,
            entry.Document.Tracks,
            entry.Document.Assets,
            CurrentDocument.Revision.Next(),
            entry.Document.SchemaVersion);

        IsDirty = _undoStack.Count > 0;
        SetInfoBanner($"Undid '{entry.Label}'.");
        return CommandResult.Success(GetSnapshot());
    }

    public CommandResult Redo()
    {
        if (_redoStack.Count == 0)
        {
            return CommandResult.Failure("ERR_NOTHING_TO_REDO", "Redo stack is empty.", CurrentDocument.Revision.Value, GetSnapshot());
        }

        IsPlaying = false;
        HistoryEntry entry = _redoStack.Pop();
        PushUndoEntry(new HistoryEntry(entry.Label, CurrentDocument));

        CurrentDocument = new ProjectDocument(
            entry.Document.ProjectId,
            entry.Document.Name,
            entry.Document.Composition,
            entry.Document.Layers,
            entry.Document.Tracks,
            entry.Document.Assets,
            CurrentDocument.Revision.Next(),
            entry.Document.SchemaVersion);

        IsDirty = true;
        SetInfoBanner($"Redid '{entry.Label}'.");
        return CommandResult.Success(GetSnapshot());
    }

    public async Task<CommandResult> SaveProjectAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            string targetFile = string.IsNullOrWhiteSpace(fileName) ? LastSavedFileName : fileName.Trim();
            await _repository.SaveAsync(CurrentDocument, targetFile, cancellationToken).ConfigureAwait(false);
            LastSavedFileName = targetFile;
            IsDirty = false;
            SetInfoBanner($"Saved project to '{LastSavedFileName}' (Revision {CurrentDocument.Revision.Value}).");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusBannerErrorCode = "ERR_SAVE_FAILED";
            StatusBannerMessage = $"Failed to save project: {ex.Message}";
            StatusBannerIsError = true;
            NotifyStateChanged();
            return CommandResult.Failure("ERR_SAVE_FAILED", StatusBannerMessage, CurrentDocument.Revision.Value, GetSnapshot());
        }
    }

    public async Task<CommandResult> OpenProjectAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            ProjectDocument loaded = await _repository.LoadAsync(fileName, cancellationToken).ConfigureAwait(false);
            _undoStack.Clear();
            _redoStack.Clear();
            _selectedLayerIds.Clear();

            CurrentDocument = loaded;
            CurrentFrameIndex = Math.Min(CurrentFrameIndex, loaded.Composition.DurationFrames);
            IsPlaying = false;
            IsDirty = false;
            LastSavedFileName = fileName.Trim();

            if (loaded.Layers.Count > 0)
            {
                _selectedLayerIds.Add(loaded.Layers[0].Id);
            }

            SetInfoBanner($"Opened project '{loaded.Name}' from '{LastSavedFileName}'.");
            return CommandResult.Success(GetSnapshot());
        }
        catch (DomainValidationException ex)
        {
            return RecordValidationFailure(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusBannerErrorCode = "ERR_OPEN_FAILED";
            StatusBannerMessage = $"Could not open '{fileName}': {ex.Message}. Current project kept intact.";
            StatusBannerIsError = true;
            NotifyStateChanged();
            return CommandResult.Failure("ERR_OPEN_FAILED", StatusBannerMessage, CurrentDocument.Revision.Value, GetSnapshot());
        }
    }

    private static List<AnimationTrack> UpdateTracksIfAnimatedAtCurrentFrame(
        IReadOnlyList<AnimationTrack> tracks,
        string layerId,
        Transform nextTransform,
        int frameIndex)
    {
        var updated = new List<AnimationTrack>(tracks.Count);
        foreach (AnimationTrack track in tracks)
        {
            if (!string.Equals(track.LayerId, layerId, StringComparison.Ordinal))
            {
                updated.Add(track);
                continue;
            }

            Keyframe? existing = track.Keyframes.FirstOrDefault(k => k.FrameIndex == frameIndex);
            EasingType easing = existing?.Easing ?? EasingType.Linear;
            CubicBezierPoints? bezier = existing?.Bezier;

            AnimationTrack nextTrack = track.Property switch
            {
                AnimatableProperty.TransformX => track.UpsertKeyframe(new Keyframe(frameIndex, nextTransform.X, easing, null, bezier)),
                AnimatableProperty.TransformY => track.UpsertKeyframe(new Keyframe(frameIndex, nextTransform.Y, easing, null, bezier)),
                AnimatableProperty.TransformRotation => track.UpsertKeyframe(new Keyframe(frameIndex, nextTransform.Rotation, easing, null, bezier)),
                AnimatableProperty.TransformScaleX => track.UpsertKeyframe(new Keyframe(frameIndex, nextTransform.ScaleX, easing, null, bezier)),
                AnimatableProperty.TransformScaleY => track.UpsertKeyframe(new Keyframe(frameIndex, nextTransform.ScaleY, easing, null, bezier)),
                _ => track,
            };
            updated.Add(nextTrack);
        }

        return updated;
    }

    private bool ValidateRevision(long? baseRevision, out CommandResult? conflictResult)
    {
        if (baseRevision.HasValue && baseRevision.Value != CurrentDocument.Revision.Value)
        {
            string msg = string.Create(
                CultureInfo.InvariantCulture,
                $"Revision conflict: command based on r{baseRevision.Value}, but authoritative document is at r{CurrentDocument.Revision.Value}.");
            StatusBannerErrorCode = "ERR_REVISION_CONFLICT";
            StatusBannerMessage = msg;
            StatusBannerIsError = true;
            NotifyStateChanged();
            conflictResult = CommandResult.Failure("ERR_REVISION_CONFLICT", msg, CurrentDocument.Revision.Value, GetSnapshot());
            return false;
        }

        conflictResult = null;
        return true;
    }

    private void ApplyDocumentMutation(
        string commandLabel,
        IReadOnlyList<LayerNode> nextLayers,
        IReadOnlyList<AnimationTrack> nextTracks,
        IReadOnlyList<AssetRef>? nextAssets = null)
    {
        var updatedDoc = new ProjectDocument(
            CurrentDocument.ProjectId,
            CurrentDocument.Name,
            CurrentDocument.Composition,
            nextLayers,
            nextTracks,
            nextAssets ?? CurrentDocument.Assets,
            CurrentDocument.Revision.Next(),
            CurrentDocument.SchemaVersion);

        PushUndoEntry(new HistoryEntry(commandLabel, CurrentDocument));
        _redoStack.Clear();
        CurrentDocument = updatedDoc;
        IsDirty = true;
        CommandCommitCount++;
        NotifyStateChanged();
    }

    private void PushUndoEntry(HistoryEntry entry)
    {
        _undoStack.AddLast(entry);
        while (_undoStack.Count > MaxHistoryDepth)
        {
            _undoStack.RemoveFirst();
        }
    }

    private CommandResult RecordValidationFailure(DomainValidationException ex)
    {
        StatusBannerErrorCode = ex.ErrorCode;
        StatusBannerMessage = ex.Message;
        StatusBannerIsError = true;
        NotifyStateChanged();
        return CommandResult.Failure(ex.ErrorCode, ex.Message, CurrentDocument.Revision.Value, GetSnapshot());
    }

    private void SetInfoBanner(string message)
    {
        StatusBannerErrorCode = null;
        StatusBannerMessage = message;
        StatusBannerIsError = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private sealed record HistoryEntry(string Label, ProjectDocument Document);

    private sealed record ClipboardItem(LayerNode Layer, IReadOnlyList<AnimationTrack> Tracks);
}
