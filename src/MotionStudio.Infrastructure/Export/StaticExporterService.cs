using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text;
using MotionStudio.Application.Contracts;
using MotionStudio.Domain.Models;
using MotionStudio.Domain.Scene;
using MotionStudio.Domain.Services;
using SkiaSharp;

namespace MotionStudio.Infrastructure.Export;

/// <summary>
/// Headless Vector (SVG) and Raster (SkiaSharp PNG) Frame Renderer &amp; Exporter (FR-012, FR-018, ADR-0004).
/// Reuses the shared C# SceneGraphBuilder + TimelineEvaluator across headless tests, PNG export, and benchmarks.
/// </summary>
public sealed class StaticExporterService : IFrameExporter
{
    public string ExportFrameToSvg(ProjectDocument document, int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<EvaluatedLayerState> states = TimelineEvaluator.EvaluateDocumentAtFrame(document, frameIndex);

        int width = document.Composition.Width;
        int height = document.Composition.Height;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"  <rect width=\"{width}\" height=\"{height}\" fill=\"#F7F8FE\" />");
        sb.AppendLine();

        foreach (EvaluatedLayerState layer in states.Where(s => s.Visible && s.Kind != LayerKind.Group))
        {
            string transformAttr = string.Create(
                CultureInfo.InvariantCulture,
                $"translate({layer.X:F2} {layer.Y:F2}) rotate({layer.Rotation:F2}) scale({layer.ScaleX:F3} {layer.ScaleY:F3})");

            string opacityAttr = layer.Opacity.ToString("F3", CultureInfo.InvariantCulture);
            string fillAttr = SecurityElement.Escape(layer.Fill) ?? "#5148EC";

            switch (layer.Kind)
            {
                case LayerKind.Rectangle:
                    sb.Append(
                        CultureInfo.InvariantCulture,
                        $"  <rect id=\"{SecurityElement.Escape(layer.LayerId)}\" width=\"{layer.Width:F2}\" height=\"{layer.Height:F2}\" rx=\"8\" fill=\"{fillAttr}\" opacity=\"{opacityAttr}\" transform=\"{transformAttr}\" />");
                    sb.AppendLine();
                    break;
                case LayerKind.Ellipse:
                    double rx = layer.Width / 2.0;
                    double ry = layer.Height / 2.0;
                    sb.Append(
                        CultureInfo.InvariantCulture,
                        $"  <ellipse id=\"{SecurityElement.Escape(layer.LayerId)}\" cx=\"{rx:F2}\" cy=\"{ry:F2}\" rx=\"{rx:F2}\" ry=\"{ry:F2}\" fill=\"{fillAttr}\" opacity=\"{opacityAttr}\" transform=\"{transformAttr}\" />");
                    sb.AppendLine();
                    break;
                case LayerKind.Text:
                    string safeText = SecurityElement.Escape(layer.TextContent ?? layer.Name) ?? string.Empty;
                    double fontSize = layer.FontSize ?? 36.0;
                    sb.Append(
                        CultureInfo.InvariantCulture,
                        $"  <text id=\"{SecurityElement.Escape(layer.LayerId)}\" x=\"0\" y=\"{fontSize:F2}\" font-family=\"Inter, Segoe UI, sans-serif\" font-size=\"{fontSize:F2}\" font-weight=\"700\" fill=\"{fillAttr}\" opacity=\"{opacityAttr}\" transform=\"{transformAttr}\">{safeText}</text>");
                    sb.AppendLine();
                    break;
            }
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    public byte[] ExportFrameToPngBytes(ProjectDocument document, int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        GroupNode root = SceneGraphBuilder.BuildEvaluatedSceneTree(document, frameIndex);
        return RenderSceneTreeToPng(root, document.Composition.Width, document.Composition.Height);
    }

    public SkiaRenderMetrics BenchmarkHundredNodeRender(int nodeCount = 100)
    {
        int count = Math.Clamp(nodeCount, 1, 2000);
        var layers = new List<LayerNode>(count);
        for (int i = 0; i < count; i++)
        {
            LayerKind kind = (i % 3) switch
            {
                0 => LayerKind.Rectangle,
                1 => LayerKind.Ellipse,
                _ => LayerKind.Text,
            };
            layers.Add(new LayerNode(
                $"bench-node-{i}",
                kind,
                $"Node {i}",
                visible: true,
                locked: false,
                zIndex: i,
                new ShapeGeometry(80 + (i % 40), 60 + (i % 30)),
                new Transform((i * 17) % 1760, (i * 23) % 960, (i * 7) % 360, 1.0, 1.0),
                opacity: 0.9,
                fill: (i % 2 == 0) ? "#5148EC" : "#6B9CFF",
                textContent: kind == LayerKind.Text ? $"Shape {i}" : null,
                fontSize: kind == LayerKind.Text ? 24.0 : null));
        }

        var doc = new ProjectDocument(
            "bench-100-nodes",
            "100-Node SkiaSharp Benchmark",
            new Composition(1920, 1080, 60, 120),
            layers,
            []);

        long memBefore = GC.GetAllocatedBytesForCurrentThread();
        var swEval = Stopwatch.StartNew();
        GroupNode sceneRoot = SceneGraphBuilder.BuildEvaluatedSceneTree(doc, 30);
        swEval.Stop();

        var swRender = Stopwatch.StartNew();
        byte[] pngBytes = RenderSceneTreeToPng(sceneRoot, 1920, 1080);
        swRender.Stop();
        long memAfter = GC.GetAllocatedBytesForCurrentThread();

        return new SkiaRenderMetrics(
            count,
            swEval.Elapsed.TotalMilliseconds,
            swRender.Elapsed.TotalMilliseconds,
            pngBytes.LongLength,
            Math.Max(0L, memAfter - memBefore));
    }

    private static byte[] RenderSceneTreeToPng(GroupNode root, int width, int height)
    {
        var imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(imageInfo)
            ?? throw new InvalidOperationException("Failed to initialize SkiaSharp raster surface.");

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(new SKColor(247, 248, 254));

        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, 32f);

        RenderNodeRecursive(canvas, root, fillPaint, font);

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 95);
        return encoded.ToArray();
    }

    private static void RenderNodeRecursive(SKCanvas canvas, SceneNode node, SKPaint paint, SKFont font)
    {
        if (!node.Visible)
        {
            return;
        }

        canvas.Save();
        canvas.Translate((float)node.X, (float)node.Y);
        if (Math.Abs(node.Rotation) > 0.001)
        {
            canvas.RotateDegrees((float)node.Rotation, (float)(node.Width * 0.5), (float)(node.Height * 0.5));
        }

        if (Math.Abs(node.ScaleX - 1.0) > 0.001 || Math.Abs(node.ScaleY - 1.0) > 0.001)
        {
            canvas.Scale((float)node.ScaleX, (float)node.ScaleY, (float)(node.Width * 0.5), (float)(node.Height * 0.5));
        }

        if (SKColor.TryParse(node.Fill, out SKColor parsedColor))
        {
            byte alpha = (byte)Math.Clamp((int)Math.Round(node.Opacity * 255.0), 0, 255);
            paint.Color = parsedColor.WithAlpha(alpha);
        }
        else
        {
            paint.Color = new SKColor(81, 72, 236);
        }

        switch (node)
        {
            case RectangleNode rectNode:
                canvas.DrawRoundRect(
                    new SKRect(0, 0, (float)rectNode.Width, (float)rectNode.Height),
                    (float)rectNode.CornerRadius,
                    (float)rectNode.CornerRadius,
                    paint);
                break;
            case EllipseNode ellipseNode:
                canvas.DrawOval(new SKRect(0, 0, (float)ellipseNode.Width, (float)ellipseNode.Height), paint);
                break;
            case TextNode textNode:
                font.Size = (float)textNode.FontSize;
                canvas.DrawText(textNode.TextContent, 0f, (float)textNode.FontSize, SKTextAlign.Left, font, paint);
                break;
        }

        foreach (SceneNode child in node.Children)
        {
            RenderNodeRecursive(canvas, child, paint, font);
        }

        canvas.Restore();
    }
}
