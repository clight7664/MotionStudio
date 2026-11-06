using MotionStudio.Domain.Models;
using MotionStudio.Domain.Services;

namespace MotionStudio.Domain.Scene;

/// <summary>
/// Immutable 2D affine transform matrix [M11 M12 OffsetX; M21 M22 OffsetY; 0 0 1].
/// Zero UI, SkiaSharp, Razor, or Electron dependencies (FR-003).
/// </summary>
public readonly record struct Matrix2D(
    double M11,
    double M12,
    double M21,
    double M22,
    double OffsetX,
    double OffsetY)
{
    public static Matrix2D Identity => new(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);

    public static Matrix2D FromTransform(double x, double y, double rotationDegrees, double scaleX, double scaleY)
    {
        double rad = rotationDegrees * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);
        return new Matrix2D(
            cos * scaleX,
            sin * scaleX,
            -sin * scaleY,
            cos * scaleY,
            x,
            y);
    }

    public Matrix2D Multiply(Matrix2D child)
    {
        return new Matrix2D(
            (M11 * child.M11) + (M12 * child.M21),
            (M11 * child.M12) + (M12 * child.M22),
            (M21 * child.M11) + (M22 * child.M21),
            (M21 * child.M12) + (M22 * child.M22),
            (M11 * child.OffsetX) + (M21 * child.OffsetY) + OffsetX,
            (M12 * child.OffsetX) + (M22 * child.OffsetY) + OffsetY);
    }

    public (double X, double Y) TransformPoint(double localX, double localY)
    {
        return (
            (M11 * localX) + (M21 * localY) + OffsetX,
            (M12 * localX) + (M22 * localY) + OffsetY);
    }

    public bool TryInvert(out Matrix2D inverse)
    {
        double det = (M11 * M22) - (M12 * M21);
        if (Math.Abs(det) < 1e-9)
        {
            inverse = Identity;
            return false;
        }

        double invDet = 1.0 / det;
        double m11 = M22 * invDet;
        double m12 = -M12 * invDet;
        double m21 = -M21 * invDet;
        double m22 = M11 * invDet;
        double ox = -((m11 * OffsetX) + (m21 * OffsetY));
        double oy = -((m12 * OffsetX) + (m22 * OffsetY));
        inverse = new Matrix2D(m11, m12, m21, m22, ox, oy);
        return true;
    }
}

public readonly record struct SceneBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public bool Contains(double x, double y) =>
        x >= Left && x <= Right && y >= Top && y <= Bottom;
}

/// <summary>
/// Pure C# Scene Graph base node (FR-003).
/// </summary>
public abstract class SceneNode
{
    private readonly List<SceneNode> _children = [];

    protected SceneNode(
        string id,
        string name,
        double x,
        double y,
        double width,
        double height,
        double rotation = 0.0,
        double scaleX = 1.0,
        double scaleY = 1.0,
        double opacity = 1.0,
        bool visible = true,
        bool locked = false,
        int zOrder = 0,
        string fill = "#5148EC",
        string stroke = "#FFFFFF",
        double strokeWidth = 0.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        X = x;
        Y = y;
        Width = Math.Max(0.0, width);
        Height = Math.Max(0.0, height);
        Rotation = rotation;
        ScaleX = scaleX;
        ScaleY = scaleY;
        Opacity = Math.Clamp(opacity, 0.0, 1.0);
        Visible = visible;
        Locked = locked;
        ZOrder = zOrder;
        Fill = fill;
        Stroke = stroke;
        StrokeWidth = Math.Max(0.0, strokeWidth);
    }

    public string Id { get; }

    public string Name { get; set; }

    public SceneNode? Parent { get; private set; }

    public IReadOnlyList<SceneNode> Children => _children;

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public double Rotation { get; set; }

    public double ScaleX { get; set; }

    public double ScaleY { get; set; }

    public double Opacity { get; set; }

    public bool Visible { get; set; }

    public bool Locked { get; set; }

    public int ZOrder { get; set; }

    public string Fill { get; set; }

    public string Stroke { get; set; }

    public double StrokeWidth { get; set; }

    public Matrix2D LocalMatrix => Matrix2D.FromTransform(X, Y, Rotation, ScaleX, ScaleY);

    public Matrix2D WorldMatrix => Parent is null ? LocalMatrix : Parent.WorldMatrix.Multiply(LocalMatrix);

    public void AddChild(SceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (ReferenceEquals(child, this))
        {
            throw new DomainValidationException("ERR_SCENE_CYCLE", "Cannot attach a node to itself.");
        }

        child.Parent?._children.Remove(child);
        child.Parent = this;
        _children.Add(child);
    }

    public SceneBounds GetWorldBounds()
    {
        Matrix2D world = WorldMatrix;
        (double x0, double y0) = world.TransformPoint(0, 0);
        (double x1, double y1) = world.TransformPoint(Width, 0);
        (double x2, double y2) = world.TransformPoint(Width, Height);
        (double x3, double y3) = world.TransformPoint(0, Height);

        double minX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3));
        double maxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
        double minY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
        double maxY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));
        return new SceneBounds(minX, minY, maxX - minX, maxY - minY);
    }

    public virtual bool HitTestWorld(double worldX, double worldY)
    {
        if (!Visible || Locked)
        {
            return false;
        }

        if (!WorldMatrix.TryInvert(out Matrix2D inv))
        {
            return false;
        }

        (double lx, double ly) = inv.TransformPoint(worldX, worldY);
        return lx >= 0.0 && lx <= Width && ly >= 0.0 && ly <= Height;
    }
}

public sealed class GroupNode : SceneNode
{
    public GroupNode(string id, string name, double x = 0, double y = 0, int zOrder = 0)
        : base(id, name, x, y, 0, 0, zOrder: zOrder)
    {
    }

    public override bool HitTestWorld(double worldX, double worldY)
    {
        if (!Visible || Locked)
        {
            return false;
        }

        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].HitTestWorld(worldX, worldY))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class RectangleNode : SceneNode
{
    public RectangleNode(
        string id,
        string name,
        double x,
        double y,
        double width,
        double height,
        double cornerRadius = 12.0,
        string fill = "#5148EC",
        int zOrder = 0)
        : base(id, name, x, y, width, height, fill: fill, zOrder: zOrder)
    {
        CornerRadius = Math.Max(0.0, cornerRadius);
    }

    public double CornerRadius { get; set; }
}

public sealed class EllipseNode : SceneNode
{
    public EllipseNode(
        string id,
        string name,
        double x,
        double y,
        double width,
        double height,
        string fill = "#5148EC",
        int zOrder = 0)
        : base(id, name, x, y, width, height, fill: fill, zOrder: zOrder)
    {
    }

    public override bool HitTestWorld(double worldX, double worldY)
    {
        if (!Visible || Locked || Width <= 0 || Height <= 0)
        {
            return false;
        }

        if (!WorldMatrix.TryInvert(out Matrix2D inv))
        {
            return false;
        }

        (double lx, double ly) = inv.TransformPoint(worldX, worldY);
        double rx = Width * 0.5;
        double ry = Height * 0.5;
        double dx = (lx - rx) / rx;
        double dy = (ly - ry) / ry;
        return (dx * dx) + (dy * dy) <= 1.0;
    }
}

public sealed class PathNode : SceneNode
{
    public PathNode(
        string id,
        string name,
        string svgPathData,
        double x,
        double y,
        double width,
        double height,
        string fill = "#5148EC",
        int zOrder = 0)
        : base(id, name, x, y, width, height, fill: fill, zOrder: zOrder)
    {
        SvgPathData = svgPathData ?? string.Empty;
    }

    public string SvgPathData { get; set; }
}

public sealed class TextNode : SceneNode
{
    public TextNode(
        string id,
        string name,
        string textContent,
        double fontSize,
        double x,
        double y,
        double width,
        double height,
        string fill = "#1B2130",
        int zOrder = 0)
        : base(id, name, x, y, width, height, fill: fill, zOrder: zOrder)
    {
        TextContent = textContent ?? string.Empty;
        FontSize = Math.Max(8.0, fontSize);
    }

    public string TextContent { get; set; }

    public double FontSize { get; set; }
}

public sealed class ImageNode : SceneNode
{
    public ImageNode(
        string id,
        string name,
        string assetId,
        double x,
        double y,
        double width,
        double height,
        int zOrder = 0)
        : base(id, name, x, y, width, height, zOrder: zOrder)
    {
        AssetId = assetId ?? string.Empty;
    }

    public string AssetId { get; set; }
}

public static class SceneGraphBuilder
{
    public static GroupNode BuildEvaluatedSceneTree(ProjectDocument document, double frameIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<EvaluatedLayerState> states = TimelineEvaluator.EvaluateDocumentAtFrame(document, frameIndex);

        var root = new GroupNode("scene-root", document.Name);
        var byId = new Dictionary<string, SceneNode>(states.Count, StringComparer.Ordinal);

        foreach (EvaluatedLayerState s in states)
        {
            SceneNode node = s.Kind switch
            {
                LayerKind.Group => new GroupNode(s.LayerId, s.Name, s.X, s.Y, s.ZIndex),
                LayerKind.Ellipse => new EllipseNode(s.LayerId, s.Name, s.X, s.Y, s.Width, s.Height, s.Fill, s.ZIndex),
                LayerKind.Text => new TextNode(s.LayerId, s.Name, s.TextContent ?? s.Name, s.FontSize ?? 32.0, s.X, s.Y, s.Width, s.Height, s.Fill, s.ZIndex),
                _ => new RectangleNode(s.LayerId, s.Name, s.X, s.Y, s.Width, s.Height, 12.0, s.Fill, s.ZIndex),
            };
            node.Rotation = s.Rotation;
            node.ScaleX = s.ScaleX;
            node.ScaleY = s.ScaleY;
            node.Opacity = s.Opacity;
            node.Visible = s.Visible;
            node.Locked = s.Locked;
            byId[s.LayerId] = node;
        }

        foreach (EvaluatedLayerState s in states)
        {
            SceneNode node = byId[s.LayerId];
            if (!string.IsNullOrWhiteSpace(s.ParentId) && byId.TryGetValue(s.ParentId, out SceneNode? parentNode))
            {
                parentNode.AddChild(node);
            }
            else
            {
                root.AddChild(node);
            }
        }

        return root;
    }
}
