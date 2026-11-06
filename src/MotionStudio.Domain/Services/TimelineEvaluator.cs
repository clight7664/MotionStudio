using System.Globalization;
using MotionStudio.Domain.Models;

namespace MotionStudio.Domain.Services;

public sealed record EvaluatedLayerState(
    string LayerId,
    LayerKind Kind,
    string Name,
    bool Visible,
    bool Locked,
    int ZIndex,
    double Width,
    double Height,
    double X,
    double Y,
    double Rotation,
    double ScaleX,
    double ScaleY,
    double Opacity,
    string Fill,
    string? ParentId,
    string? TextContent,
    double? FontSize);

public static class TimelineEvaluator
{
    public static IReadOnlyList<EvaluatedLayerState> EvaluateDocumentAtFrame(ProjectDocument document, double frameIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        DomainGuard.EnsureFinite(frameIndex, nameof(frameIndex));

        double clampedFrame = Math.Clamp(frameIndex, 0.0, document.Composition.DurationFrames);
        var states = new List<EvaluatedLayerState>(document.Layers.Count);

        foreach (LayerNode layer in document.Layers.OrderBy(l => l.ZIndex))
        {
            states.Add(EvaluateLayerAtFrame(document, layer, clampedFrame));
        }

        return states;
    }

    public static EvaluatedLayerState EvaluateLayerAtFrame(ProjectDocument document, LayerNode layer, double frameIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layer);
        DomainGuard.EnsureFinite(frameIndex, nameof(frameIndex));

        double clampedFrame = Math.Clamp(frameIndex, 0.0, document.Composition.DurationFrames);

        double x = EvaluateProperty(document, layer.Id, AnimatableProperty.TransformX, clampedFrame, layer.Transform.X);
        double y = EvaluateProperty(document, layer.Id, AnimatableProperty.TransformY, clampedFrame, layer.Transform.Y);
        double rotation = EvaluateProperty(document, layer.Id, AnimatableProperty.TransformRotation, clampedFrame, layer.Transform.Rotation);
        double scaleX = EvaluateProperty(document, layer.Id, AnimatableProperty.TransformScaleX, clampedFrame, layer.Transform.ScaleX);
        double scaleY = EvaluateProperty(document, layer.Id, AnimatableProperty.TransformScaleY, clampedFrame, layer.Transform.ScaleY);
        double opacity = Math.Clamp(
            EvaluateProperty(document, layer.Id, AnimatableProperty.Opacity, clampedFrame, layer.Opacity),
            0.0,
            1.0);
        string fill = EvaluateColorProperty(document, layer.Id, clampedFrame, layer.Fill);

        return new EvaluatedLayerState(
            layer.Id,
            layer.Kind,
            layer.Name,
            layer.Visible,
            layer.Locked,
            layer.ZIndex,
            layer.Geometry.Width,
            layer.Geometry.Height,
            x,
            y,
            rotation,
            scaleX,
            scaleY,
            opacity,
            fill,
            layer.ParentId,
            layer.TextContent,
            layer.FontSize);
    }

    public static double EvaluateProperty(
        ProjectDocument document,
        string layerId,
        AnimatableProperty property,
        double frameIndex,
        double defaultValue)
    {
        ArgumentNullException.ThrowIfNull(document);
        AnimationTrack? track = document.Tracks.FirstOrDefault(t =>
            string.Equals(t.LayerId, layerId, StringComparison.Ordinal) &&
            t.Property == property);

        if (track is null || track.Keyframes.Count == 0)
        {
            return defaultValue;
        }

        return EvaluateNumericTrack(track.Keyframes, frameIndex, defaultValue);
    }

    public static string EvaluateColorProperty(
        ProjectDocument document,
        string layerId,
        double frameIndex,
        string defaultHex)
    {
        ArgumentNullException.ThrowIfNull(document);
        AnimationTrack? track = document.Tracks.FirstOrDefault(t =>
            string.Equals(t.LayerId, layerId, StringComparison.Ordinal) &&
            t.Property == AnimatableProperty.Fill);

        if (track is null || track.Keyframes.Count == 0)
        {
            return DomainGuard.NormalizeHexColor(defaultHex);
        }

        return EvaluateColorTrack(track.Keyframes, frameIndex, defaultHex);
    }

    public static double EvaluateNumericTrack(
        IReadOnlyList<Keyframe> keyframes,
        double frameIndex,
        double defaultValue)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        DomainGuard.EnsureFinite(frameIndex, nameof(frameIndex));

        if (keyframes.Count == 0)
        {
            return defaultValue;
        }

        if (keyframes.Count == 1 || frameIndex <= keyframes[0].FrameIndex)
        {
            return keyframes[0].Value;
        }

        Keyframe last = keyframes[^1];
        if (frameIndex >= last.FrameIndex)
        {
            return last.Value;
        }

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            Keyframe start = keyframes[i];
            Keyframe end = keyframes[i + 1];

            if (Math.Abs(frameIndex - start.FrameIndex) < 1e-9)
            {
                return start.Value;
            }

            if (frameIndex > start.FrameIndex && frameIndex <= end.FrameIndex)
            {
                int span = end.FrameIndex - start.FrameIndex;
                if (span <= 0)
                {
                    return end.Value;
                }

                double rawT = (frameIndex - start.FrameIndex) / span;
                double easedT = EvaluateEasing(start.Easing, rawT, start.Bezier);
                return start.Value + ((end.Value - start.Value) * easedT);
            }
        }

        return last.Value;
    }

    public static string EvaluateColorTrack(
        IReadOnlyList<Keyframe> keyframes,
        double frameIndex,
        string defaultHex)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        string fallback = DomainGuard.NormalizeHexColor(defaultHex);

        if (keyframes.Count == 0)
        {
            return fallback;
        }

        if (keyframes.Count == 1 || frameIndex <= keyframes[0].FrameIndex)
        {
            return keyframes[0].ColorValue ?? fallback;
        }

        Keyframe last = keyframes[^1];
        if (frameIndex >= last.FrameIndex)
        {
            return last.ColorValue ?? fallback;
        }

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            Keyframe start = keyframes[i];
            Keyframe end = keyframes[i + 1];

            if (frameIndex >= start.FrameIndex && frameIndex <= end.FrameIndex)
            {
                int span = end.FrameIndex - start.FrameIndex;
                if (span <= 0)
                {
                    return end.ColorValue ?? fallback;
                }

                double rawT = (frameIndex - start.FrameIndex) / span;
                double easedT = EvaluateEasing(start.Easing, rawT, start.Bezier);
                return InterpolateHexColor(
                    start.ColorValue ?? fallback,
                    end.ColorValue ?? fallback,
                    easedT);
            }
        }

        return last.ColorValue ?? fallback;
    }

    public static double EvaluateEasing(EasingType easing, double t, CubicBezierPoints? bezier = null)
    {
        double clamped = Math.Clamp(t, 0.0, 1.0);
        return easing switch
        {
            EasingType.Linear => clamped,
            EasingType.Step => clamped >= 1.0 ? 1.0 : 0.0,
            EasingType.EaseIn => clamped * clamped,
            EasingType.EaseOut => 1.0 - ((1.0 - clamped) * (1.0 - clamped)),
            EasingType.EaseInOut => clamped < 0.5
                ? 2.0 * clamped * clamped
                : 1.0 - (Math.Pow((-2.0 * clamped) + 2.0, 2.0) / 2.0),
            EasingType.CubicBezier when bezier is not null => SolveCubicBezier(bezier.X1, bezier.Y1, bezier.X2, bezier.Y2, clamped),
            EasingType.CubicBezier => SolveCubicBezier(0.42, 0.0, 0.58, 1.0, clamped),
            _ => clamped,
        };
    }

    public static string InterpolateHexColor(string startHex, string endHex, double easedT)
    {
        string s = DomainGuard.NormalizeHexColor(startHex);
        string e = DomainGuard.NormalizeHexColor(endHex);
        double t = Math.Clamp(easedT, 0.0, 1.0);

        int sr = int.Parse(s.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int sg = int.Parse(s.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int sb = int.Parse(s.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        int er = int.Parse(e.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int eg = int.Parse(e.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int eb = int.Parse(e.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        int r = (int)Math.Round(sr + ((er - sr) * t));
        int g = (int)Math.Round(sg + ((eg - sg) * t));
        int b = (int)Math.Round(sb + ((eb - sb) * t));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}");
    }

    private static double SolveCubicBezier(double x1, double y1, double x2, double y2, double x)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        if (x >= 1.0)
        {
            return 1.0;
        }

        double u = x;
        for (int i = 0; i < 8; i++)
        {
            double currentX = SampleCurve(x1, x2, u) - x;
            if (Math.Abs(currentX) < 1e-6)
            {
                break;
            }

            double derivative = SampleCurveDerivative(x1, x2, u);
            if (Math.Abs(derivative) < 1e-6)
            {
                break;
            }

            u = Math.Clamp(u - (currentX / derivative), 0.0, 1.0);
        }

        return SampleCurve(y1, y2, u);
    }

    private static double SampleCurve(double p1, double p2, double t)
    {
        double inv = 1.0 - t;
        return (3.0 * inv * inv * t * p1) + (3.0 * inv * t * t * p2) + (t * t * t);
    }

    private static double SampleCurveDerivative(double p1, double p2, double t)
    {
        double inv = 1.0 - t;
        return (3.0 * inv * inv * p1) + (6.0 * inv * t * (p2 - p1)) + (3.0 * t * t * (1.0 - p2));
    }
}
