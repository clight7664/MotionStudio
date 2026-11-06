using MotionStudio.Application.Contracts;
using MotionStudio.Desktop;
using MotionStudio.Domain.Models;
using MotionStudio.Domain.Services;
using MotionStudio.Infrastructure.Export;
using MotionStudio.Infrastructure.Persistence;
using MotionStudio.Infrastructure.Security;

namespace MotionStudio.Integration.Tests;

public sealed class PersistenceAndSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonProjectRepository _repository;

    public PersistenceAndSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MotionStudioTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _repository = new JsonProjectRepository(_tempDir);
    }

    [Fact]
    public async Task TC003_SaveAndOpenRoundTrip_PreservesFullSemanticModel_AndCreatesBackup()
    {
        ProjectDocument original = ProjectDocument.CreateDefaultDemo();
        await _repository.SaveAsync(original, "demo-roundtrip.motion.json");

        // Save a second time to verify .bak backup generation
        var updated = new ProjectDocument(
            original.ProjectId,
            "Updated Demo Title",
            original.Composition,
            original.Layers,
            original.Tracks,
            original.Assets,
            original.Revision.Next());
        await _repository.SaveAsync(updated, "demo-roundtrip.motion.json");

        ProjectDocument loaded = await _repository.LoadAsync("demo-roundtrip.motion.json");
        Assert.Equal(updated.ProjectId, loaded.ProjectId);
        Assert.Equal("Updated Demo Title", loaded.Name);
        Assert.Equal(updated.Composition.Width, loaded.Composition.Width);
        Assert.Equal(updated.Layers.Count, loaded.Layers.Count);
        Assert.Equal(updated.Tracks.Count, loaded.Tracks.Count);

        EvaluatedLayerState origRing = TimelineEvaluator.EvaluateDocumentAtFrame(updated, 15).Single(l => l.LayerId == "layer-orbital-ring");
        EvaluatedLayerState loadRing = TimelineEvaluator.EvaluateDocumentAtFrame(loaded, 15).Single(l => l.LayerId == "layer-orbital-ring");
        Assert.Equal(origRing.X, loadRing.X, precision: 6);
        Assert.Equal(960.0, loadRing.X, precision: 6);

        // Backup recovery should return the first saved version
        ProjectDocument? recovered = await _repository.TryRecoverBackupAsync("demo-roundtrip.motion.json");
        Assert.NotNull(recovered);
        Assert.Equal("Brand Intro / Scene 01", recovered.Name);
    }

    [Fact]
    public async Task Repository_RejectsCorruptedJsonAndPathTraversal()
    {
        string badFile = Path.Combine(_tempDir, "corrupted.motion.json");
        await File.WriteAllTextAsync(badFile, "{ invalid json");

        await Assert.ThrowsAsync<DomainValidationException>(() => _repository.LoadAsync("corrupted.motion.json"));
        Assert.Throws<DomainValidationException>(() => _repository.ResolveSafeProjectPath("../outside.motion.json"));
        Assert.Throws<DomainValidationException>(() => _repository.ResolveSafeProjectPath("C:\\Windows\\System32\\cmd.exe"));
    }

    [Fact]
    public void SvgSanitizer_RejectsMaliciousPayloads_AndAcceptsSafeVector()
    {
        var sanitizer = new SvgSanitizerService();

        SvgSanitizeResult scriptAttack = sanitizer.InspectAndSanitize("<svg><script>alert(1)</script></svg>");
        Assert.False(scriptAttack.IsSafe);
        Assert.Equal("ERR_FORBIDDEN_SVG_ELEMENT", scriptAttack.ErrorCode);

        SvgSanitizeResult eventAttack = sanitizer.InspectAndSanitize("<svg onload=\"alert(1)\"><rect width=\"10\" height=\"10\"/></svg>");
        Assert.False(eventAttack.IsSafe);
        Assert.Equal("ERR_FORBIDDEN_SVG_EVENT", eventAttack.ErrorCode);

        SvgSanitizeResult xxeAttack = sanitizer.InspectAndSanitize("<!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><svg></svg>");
        Assert.False(xxeAttack.IsSafe);
        Assert.Equal("ERR_SVG_XXE", xxeAttack.ErrorCode);

        SvgSanitizeResult externalLink = sanitizer.InspectAndSanitize("<svg><image href=\"https://evil.example/payload.svg\"/></svg>");
        Assert.False(externalLink.IsSafe);
        Assert.Equal("ERR_EXTERNAL_SVG_RESOURCE", externalLink.ErrorCode);

        SvgSanitizeResult cleanSvg = sanitizer.InspectAndSanitize(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"240\" height=\"120\"><rect width=\"240\" height=\"120\" fill=\"#48D597\"/></svg>");
        Assert.True(cleanSvg.IsSafe);
        Assert.Equal(240.0, cleanSvg.Width, precision: 4);
        Assert.Equal(120.0, cleanSvg.Height, precision: 4);
        Assert.Equal("#48D597", cleanSvg.FillColor);
    }

    [Fact]
    public void StaticExporter_GeneratesValidSelfContainedSvg()
    {
        var exporter = new StaticExporterService();
        ProjectDocument demo = ProjectDocument.CreateDefaultDemo();
        string svg = exporter.ExportFrameToSvg(demo, 60);

        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("DESIGN IN MOTION", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SkiaSharp_RendersDefaultSceneAndHundredNodesToValidPng()
    {
        var exporter = new StaticExporterService();
        ProjectDocument demo = ProjectDocument.CreateDefaultDemo();
        byte[] png = exporter.ExportFrameToPngBytes(demo, 60);

        Assert.True(png.Length > 256);
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]);
        Assert.Equal(0x4E, png[2]);
        Assert.Equal(0x47, png[3]);

        SkiaRenderMetrics metrics = exporter.BenchmarkHundredNodeRender(100);
        Assert.Equal(100, metrics.NodeCount);
        Assert.True(metrics.PngByteLength > 256);
    }

    [Fact]
    public void DesktopHostBridge_ResolvesSafeDirectory()
    {
        var bridge = new ElectronDesktopHostBridge(_tempDir);
        Assert.True(Directory.Exists(bridge.SafeProjectsDirectory));
        Assert.True(bridge.ValidateDesktopToken(null));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
