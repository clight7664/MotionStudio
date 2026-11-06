using System.Globalization;
using System.Text.RegularExpressions;

namespace MotionStudio.Domain.Models;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainValidationException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public readonly record struct DocumentRevision(long Value)
{
    public static DocumentRevision Initial => new(1);

    public DocumentRevision Next()
    {
        return new DocumentRevision(Value + 1);
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}

public enum LayerKind
{
    Rectangle,
    Ellipse,
    Text,
    Group,
}

public enum AnimatableProperty
{
    TransformX,
    TransformY,
    TransformRotation,
    TransformScaleX,
    TransformScaleY,
    Opacity,
    Fill,
}

public enum EasingType
{
    Linear,
    Step,
    EaseIn,
    EaseOut,
    EaseInOut,
    CubicBezier,
}

public sealed record CubicBezierPoints
{
    public CubicBezierPoints(double x1, double y1, double x2, double y2)
    {
        DomainGuard.EnsureFinite(x1, nameof(x1));
        DomainGuard.EnsureFinite(y1, nameof(y1));
        DomainGuard.EnsureFinite(x2, nameof(x2));
        DomainGuard.EnsureFinite(y2, nameof(y2));

        if (x1 < 0.0 || x1 > 1.0 || x2 < 0.0 || x2 > 1.0)
        {
            throw new DomainValidationException(
                "ERR_INVALID_BEZIER_X",
                "Cubic Bezier X control points must be within [0, 1].");
        }

        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public double X1 { get; }

    public double Y1 { get; }

    public double X2 { get; }

    public double Y2 { get; }
}

public sealed record Composition
{
    public const int MinDimension = 1;
    public const int MaxDimension = 16384;
    public const int MinDurationFrames = 1;
    public const int MaxDurationFrames = 216000;

    public Composition(int width, int height, int fps, int durationFrames)
    {
        if (width < MinDimension || width > MaxDimension)
        {
            throw new DomainValidationException(
                "ERR_INVALID_WIDTH",
                $"Composition width must be between {MinDimension} and {MaxDimension}.");
        }

        if (height < MinDimension || height > MaxDimension)
        {
            throw new DomainValidationException(
                "ERR_INVALID_HEIGHT",
                $"Composition height must be between {MinDimension} and {MaxDimension}.");
        }

        if (fps is not (24 or 30 or 60))
        {
            throw new DomainValidationException(
                "ERR_INVALID_FPS",
                "Composition FPS must be 24, 30, or 60.");
        }

        if (durationFrames < MinDurationFrames || durationFrames > MaxDurationFrames)
        {
            throw new DomainValidationException(
                "ERR_INVALID_DURATION",
                $"Composition durationFrames must be between {MinDurationFrames} and {MaxDurationFrames}.");
        }

        Width = width;
        Height = height;
        Fps = fps;
        DurationFrames = durationFrames;
    }

    public int Width { get; }

    public int Height { get; }

    public int Fps { get; }

    public int DurationFrames { get; }

    public double DurationSeconds => (double)DurationFrames / Fps;

    public int ClampFrame(int frameIndex)
    {
        if (frameIndex < 0)
        {
            return 0;
        }

        if (frameIndex > DurationFrames)
        {
            return DurationFrames;
        }

        return frameIndex;
    }
}

public sealed record ShapeGeometry
{
    public ShapeGeometry(double width, double height)
    {
        DomainGuard.EnsureFinite(width, nameof(width));
        DomainGuard.EnsureFinite(height, nameof(height));

        if (width <= 0.0 || height <= 0.0)
        {
            throw new DomainValidationException(
                "ERR_INVALID_GEOMETRY",
                "Geometry width and height must be positive numbers.");
        }

        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}

public sealed record Transform
{
    public Transform(double x, double y, double rotation = 0.0, double scaleX = 1.0, double scaleY = 1.0)
    {
        DomainGuard.EnsureFinite(x, nameof(x));
        DomainGuard.EnsureFinite(y, nameof(y));
        DomainGuard.EnsureFinite(rotation, nameof(rotation));
        DomainGuard.EnsureFinite(scaleX, nameof(scaleX));
        DomainGuard.EnsureFinite(scaleY, nameof(scaleY));

        if (Math.Abs(scaleX) < 0.001 || Math.Abs(scaleY) < 0.001)
        {
            throw new DomainValidationException(
                "ERR_INVALID_SCALE",
                "Transform scale factors must have magnitude >= 0.001.");
        }

        X = x;
        Y = y;
        Rotation = rotation;
        ScaleX = scaleX;
        ScaleY = scaleY;
    }

    public double X { get; }

    public double Y { get; }

    public double Rotation { get; }

    public double ScaleX { get; }

    public double ScaleY { get; }
}

public sealed record AssetRef(string Id, string Name, string Kind, string Content);

public static partial class DomainGuard
{
    public static void EnsureFinite(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new DomainValidationException(
                "ERR_NON_FINITE_NUMBER",
                $"Parameter '{paramName}' must be a finite number.");
        }
    }

    public static string NormalizeHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            throw new DomainValidationException(
                "ERR_INVALID_COLOR",
                "Fill color cannot be empty.");
        }

        string trimmed = color.Trim();
        if (!HexColorRegex().IsMatch(trimmed))
        {
            throw new DomainValidationException(
                "ERR_INVALID_COLOR",
                $"Color '{color}' must match #RRGGBB hex format.");
        }

        return trimmed.ToUpperInvariant();
    }

    public static string ToWireName(this LayerKind kind)
    {
        return kind switch
        {
            LayerKind.Rectangle => "rectangle",
            LayerKind.Ellipse => "ellipse",
            LayerKind.Text => "text",
            LayerKind.Group => "group",
            _ => "rectangle",
        };
    }

    public static LayerKind ParseLayerKind(string value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "rectangle" => LayerKind.Rectangle,
            "ellipse" => LayerKind.Ellipse,
            "text" => LayerKind.Text,
            "group" => LayerKind.Group,
            _ => throw new DomainValidationException("ERR_INVALID_LAYER_KIND", $"Unsupported layer kind '{value}'."),
        };
    }

    public static string ToWireName(this AnimatableProperty property)
    {
        return property switch
        {
            AnimatableProperty.TransformX => "transform.x",
            AnimatableProperty.TransformY => "transform.y",
            AnimatableProperty.TransformRotation => "transform.rotation",
            AnimatableProperty.TransformScaleX => "transform.scaleX",
            AnimatableProperty.TransformScaleY => "transform.scaleY",
            AnimatableProperty.Opacity => "opacity",
            AnimatableProperty.Fill => "fill",
            _ => "transform.x",
        };
    }

    public static AnimatableProperty ParseAnimatableProperty(string value)
    {
        return value?.Trim() switch
        {
            "transform.x" => AnimatableProperty.TransformX,
            "transform.y" => AnimatableProperty.TransformY,
            "transform.rotation" => AnimatableProperty.TransformRotation,
            "transform.scaleX" => AnimatableProperty.TransformScaleX,
            "transform.scaleY" => AnimatableProperty.TransformScaleY,
            "opacity" => AnimatableProperty.Opacity,
            "fill" => AnimatableProperty.Fill,
            _ => throw new DomainValidationException("ERR_INVALID_PROPERTY", $"Unsupported animatable property '{value}'."),
        };
    }

    public static string ToWireName(this EasingType easing)
    {
        return easing switch
        {
            EasingType.Linear => "linear",
            EasingType.Step => "step",
            EasingType.EaseIn => "ease-in",
            EasingType.EaseOut => "ease-out",
            EasingType.EaseInOut => "ease-in-out",
            EasingType.CubicBezier => "cubic-bezier",
            _ => "linear",
        };
    }

    public static EasingType ParseEasingType(string value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "linear" => EasingType.Linear,
            "step" => EasingType.Step,
            "ease-in" => EasingType.EaseIn,
            "ease-out" => EasingType.EaseOut,
            "ease-in-out" => EasingType.EaseInOut,
            "cubic-bezier" => EasingType.CubicBezier,
            _ => throw new DomainValidationException("ERR_INVALID_EASING", $"Unsupported easing '{value}'."),
        };
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
