using System.Text;
using System.Text.Json;
using UCAD.Core.IO;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class AutoCadFixtureCorpusTests
{
    private const string ManifestFileName = "manifest.json";
    private static readonly Lazy<FixtureManifest> Manifest = new(LoadManifest);

    public static IEnumerable<object[]> GetDwgFixtures() =>
        Manifest.Value.Fixtures
            .Where(fixture => string.Equals(fixture.Kind, "dwg", StringComparison.OrdinalIgnoreCase))
            .Select(fixture => new object[] { fixture });

    public static IEnumerable<object[]> GetDxfFixtures() =>
        Manifest.Value.Fixtures
            .Where(fixture => string.Equals(fixture.Kind, "dxf", StringComparison.OrdinalIgnoreCase))
            .Select(fixture => new object[] { fixture });

    [Fact]
    public void ManifestPinsMultipleAutoCadGenerationsAndContainerKinds()
    {
        var manifest = Manifest.Value;

        Assert.Equal("ucad-autocad-fixtures-v1", manifest.Schema);
        Assert.Equal("DomCR/ACadSharp", manifest.Source.Repository);
        Assert.Equal("d7dc111023477d8a9fffc2153139459c95b4f345", manifest.Source.Commit);
        Assert.True(manifest.Fixtures.Count >= 10);
        Assert.True(manifest.Fixtures.Count(fixture => fixture.Kind == "dwg") >= 7);
        Assert.True(manifest.Fixtures.Count(fixture => fixture.Kind == "dxf") >= 3);
        Assert.Contains(manifest.Fixtures, fixture => fixture.AcadVersion == "AC1014");
        Assert.Contains(manifest.Fixtures, fixture => fixture.AcadVersion == "AC1032");
    }

    [Theory]
    [MemberData(nameof(GetDwgFixtures))]
    public void RealDwgFixtureImportsAcrossVersionGenerations(FixtureDefinition fixture)
    {
        var path = ResolveFixturePath(fixture);
        if (path is null) return;

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 6, $"Fixture {fixture.Id} is too small to contain a DWG signature.");
        Assert.Equal(fixture.AcadVersion, Encoding.ASCII.GetString(bytes.AsSpan(0, 6)));

        var imported = CadAcadInteropCodec.ImportDwg(bytes);

        Assert.Equal(".dwg", imported.SourceExtension);
        Assert.Equal(fixture.AcadVersion, imported.SourceCadVersion);
        Assert.NotEmpty(imported.Document.Entities);
    }

    [Theory]
    [MemberData(nameof(GetDxfFixtures))]
    public void RealDxfFixtureImportsThroughNormalizedEntityBridge(FixtureDefinition fixture)
    {
        var path = ResolveFixturePath(fixture);
        if (path is null) return;

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(fixture.Size, bytes.LongLength);

        var imported = CadAcadInteropCodec.ImportDxf(bytes);

        Assert.Equal(".dxf", imported.SourceExtension);
        Assert.False(string.IsNullOrWhiteSpace(imported.SourceCadVersion));
        Assert.NotEmpty(imported.Document.Entities);
    }

    private static string? ResolveFixturePath(FixtureDefinition fixture)
    {
        var root = Environment.GetEnvironmentVariable("UCAD_AUTOCAD_FIXTURE_DIR");
        var required = string.Equals(
            Environment.GetEnvironmentVariable("UCAD_REQUIRE_AUTOCAD_FIXTURES"),
            "1",
            StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(root))
        {
            Assert.False(required, "UCAD_AUTOCAD_FIXTURE_DIR is required by this validation run but was not set.");
            return null;
        }

        var path = Path.Combine(root, fixture.FileName);
        if (!File.Exists(path))
        {
            Assert.False(required, $"Required AutoCAD fixture is missing: {path}");
            return null;
        }

        var length = new FileInfo(path).Length;
        Assert.Equal(fixture.Size, length);
        return path;
    }

    private static FixtureManifest LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "AutoCad", ManifestFileName);
        if (!File.Exists(path))
            throw new InvalidOperationException($"AutoCAD fixture manifest was not copied to the test output: {path}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<FixtureManifest>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException("AutoCAD fixture manifest is empty.");
    }

    public sealed class FixtureManifest
    {
        public string Schema { get; set; } = string.Empty;
        public FixtureSource Source { get; set; } = new();
        public List<FixtureDefinition> Fixtures { get; set; } = [];
    }

    public sealed class FixtureSource
    {
        public string Repository { get; set; } = string.Empty;
        public string Commit { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
    }

    public sealed class FixtureDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string AcadVersion { get; set; } = string.Empty;
        public string Generation { get; set; } = string.Empty;
        public long Size { get; set; }
        public string GitBlobSha1 { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        public override string ToString() => $"{Id} ({Generation})";
    }
}
