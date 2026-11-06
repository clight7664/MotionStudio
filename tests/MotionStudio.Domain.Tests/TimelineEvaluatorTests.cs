using MotionStudio.Domain.Models;
using MotionStudio.Domain.Scene;
using MotionStudio.Domain.Services;

namespace MotionStudio.Domain.Tests;

public sealed class TimelineEvaluatorTests
{
    [Fact]
    public void TC001_Composition_ValidatesBoundariesAndRejectsInvalidInputs()
    {
        var comp = new Composition(1920, 1080, 30, 90);
        Assert.Equal(1920, comp.Width);
        Assert.Equal(1080, comp.Height);
        Assert.Equal(30, comp.Fps);
        Assert.Equal(90, comp.DurationFrames);
        Assert.Equal(3.0, comp.DurationSeconds, precision: 5);

        Assert.Throws<DomainValidationException>(() => new Composition(0, 1080, 30, 90));
        Assert.Throws<DomainValidationException>(() => new Composition(-1920, 1080, 30, 90));
        Assert.Throws<DomainValidationException>(() => new Composition(1920, 20000, 30, 90));
        Assert.Throws<DomainValidationException>(() => new Composition(1920, 1080, 0, 90));
        Assert.Throws<DomainValidationException>(() => new Composition(1920, 1080, 45, 90));
        Assert.Throws<DomainValidationException>(() => new Composition(1920, 1080, 30, 0));
    }

    [Fact]
    public void TC002_LinearInterpolation_AtFrame15_Between0And300_Equals150()
    {
        var doc = ProjectDocument.CreateEmpty("Test", 1920, 1080, 30, 90);
        var rect = new LayerNode(
            "layer-1",
            LayerKind.Rectangle,
            "Shape 1",
            visible: true,
            locked: false,
            zIndex: 0,
            new ShapeGeometry(200, 100),
            new Transform(0, 50, 0, 1, 1),
            opacity: 1.0,
            fill: "#6B9CFF");

        var track = new AnimationTrack(
            "track-1",
            "layer-1",
            AnimatableProperty.TransformX,
            [
                new Keyframe(0, 0.0, EasingType.Linear),
                new Keyframe(30, 300.0, EasingType.Linear),
            ]);

        var animatedDoc = new ProjectDocument(
            doc.ProjectId,
            doc.Name,
            doc.Composition,
            [rect],
            [track]);

        EvaluatedLayerState state0 = TimelineEvaluator.EvaluateLayerAtFrame(animatedDoc, rect, 0);
        EvaluatedLayerState state15 = TimelineEvaluator.EvaluateLayerAtFrame(animatedDoc, rect, 15);
        EvaluatedLayerState state30 = TimelineEvaluator.EvaluateLayerAtFrame(animatedDoc, rect, 30);
        EvaluatedLayerState state60 = TimelineEvaluator.EvaluateLayerAtFrame(animatedDoc, rect, 60);

        Assert.Equal(0.0, state0.X, precision: 6);
        Assert.Equal(150.0, state15.X, precision: 6);
        Assert.Equal(300.0, state30.X, precision: 6);
        Assert.Equal(300.0, state60.X, precision: 6);
    }

    [Fact]
    public void KeyframeTrack_EnforcesUniqueFrameIndexAndSortedOrder()
    {
        var track = new AnimationTrack(
            "track-uniq",
            "layer-1",
            AnimatableProperty.TransformX,
            [
                new Keyframe(30, 300.0, EasingType.Linear),
                new Keyframe(0, 10.0, EasingType.Linear),
                new Keyframe(0, 50.0, EasingType.EaseOut),
            ]);

        Assert.Equal(2, track.Keyframes.Count);
        Assert.Equal(0, track.Keyframes[0].FrameIndex);
        Assert.Equal(50.0, track.Keyframes[0].Value, precision: 6);
        Assert.Equal(EasingType.EaseOut, track.Keyframes[0].Easing);
        Assert.Equal(30, track.Keyframes[1].FrameIndex);
    }

    [Fact]
    public void EasingAndColorInterpolation_ProduceDeterministicGoldenVectors()
    {
        Assert.Equal(0.25, TimelineEvaluator.EvaluateEasing(EasingType.EaseIn, 0.5), precision: 6);
        Assert.Equal(0.75, TimelineEvaluator.EvaluateEasing(EasingType.EaseOut, 0.5), precision: 6);
        Assert.Equal(0.5, TimelineEvaluator.EvaluateEasing(EasingType.EaseInOut, 0.5), precision: 6);

        var bezier = new CubicBezierPoints(0.25, 0.1, 0.25, 1.0);
        double bezierMid = TimelineEvaluator.EvaluateEasing(EasingType.CubicBezier, 0.5, bezier);
        Assert.InRange(bezierMid, 0.75, 0.85);

        string midColor = TimelineEvaluator.InterpolateHexColor("#000000", "#FFFFFF", 0.5);
        Assert.Equal("#808080", midColor);
    }

    [Fact]
    public void DomainPrimitives_RejectNaNAndInfinityAndInvalidHex()
    {
        Assert.Throws<DomainValidationException>(() => new Transform(double.NaN, 0));
        Assert.Throws<DomainValidationException>(() => new Transform(0, double.PositiveInfinity));
        Assert.Throws<DomainValidationException>(() => new ShapeGeometry(double.NaN, 100));
        Assert.Throws<DomainValidationException>(() => new CubicBezierPoints(-0.1, 0, 0.5, 1));
        Assert.Throws<DomainValidationException>(() => DomainGuard.NormalizeHexColor("not-a-color"));
        Assert.Equal(0.0, TimelineEvaluator.EvaluateEasing(EasingType.Step, 0.5), precision: 6);
        Assert.Equal(1.0, TimelineEvaluator.EvaluateEasing(EasingType.Step, 1.0), precision: 6);
    }

    [Fact]
    public void SceneGraph_SupportsHierarchyWorldMatricesAndHitTesting()
    {
        var group = new GroupNode("grp-1", "Parent Group", x: 100, y: 200);
        var rect = new RectangleNode("rect-1", "Child Rect", x: 50, y: 30, width: 120, height: 80);
        var ellipse = new EllipseNode("ell-1", "Orb", x: 400, y: 300, width: 100, height: 100);
        var path = new PathNode("path-1", "Curve", "M 0 0 L 50 50", 10, 10, 50, 50);
        var img = new ImageNode("img-1", "Poster", "asset-1", 0, 0, 64, 64);

        group.AddChild(rect);
        SceneBounds worldBounds = rect.GetWorldBounds();
        Assert.Equal(150.0, worldBounds.Left, precision: 5);
        Assert.Equal(230.0, worldBounds.Top, precision: 5);
        Assert.True(group.HitTestWorld(180, 260));
        Assert.False(group.HitTestWorld(20, 20));
        Assert.True(ellipse.HitTestWorld(450, 350));
        Assert.False(ellipse.HitTestWorld(402, 302));
        Assert.Equal("M 0 0 L 50 50", path.SvgPathData);
        Assert.Equal("asset-1", img.AssetId);
    }
}
