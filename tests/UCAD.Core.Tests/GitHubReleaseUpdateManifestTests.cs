using UCAD.Core.Updates;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class GitHubReleaseUpdateManifestTests
{
    [Fact]
    public void StableReleaseSelectsArchitectureBundleAndChecksums()
    {
        var manifest = GitHubReleaseUpdateManifest.Parse(ReleaseJson("v0.9.4", draft: false, prerelease: false));

        Assert.Equal(new Version(0, 9, 4), manifest.Version);
        Assert.Equal("UCAD_0.9.4.0_x64.msixbundle", manifest.Bundle.Name);
        Assert.NotNull(manifest.Checksums);
        Assert.Equal("SHA256SUMS.txt", manifest.Checksums!.Name);
        Assert.True(manifest.IsNewerThan("0.9.3"));
        Assert.False(manifest.IsNewerThan("0.9.4"));
    }

    [Theory]
    [InlineData("0.9.3", 0, 9, 3)]
    [InlineData("v1.2.0", 1, 2, 0)]
    [InlineData("V12.34.56", 12, 34, 56)]
    public void VersionParserAcceptsThreePartProductVersions(string value, int major, int minor, int build)
    {
        Assert.True(GitHubReleaseUpdateManifest.TryParseVersion(value, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Fact]
    public void DraftAndPrereleaseAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => GitHubReleaseUpdateManifest.Parse(ReleaseJson("v0.9.4", draft: true, prerelease: false)));
        Assert.Throws<InvalidDataException>(() => GitHubReleaseUpdateManifest.Parse(ReleaseJson("v0.9.4", draft: false, prerelease: true)));
    }

    [Fact]
    public void MissingArchitectureBundleIsRejectedWhenAmbiguous()
    {
        const string json = """
        {
          "tag_name":"v0.9.4",
          "name":"UCAD v0.9.4",
          "html_url":"https://github.com/KiYouJyo/UCAD/releases/tag/v0.9.4",
          "draft":false,
          "prerelease":false,
          "assets":[
            {"name":"UCAD_0.9.4.0_arm64.msixbundle","browser_download_url":"https://example.test/arm64.msixbundle","size":100,"content_type":"application/octet-stream"},
            {"name":"UCAD_0.9.4.0_x86.msixbundle","browser_download_url":"https://example.test/x86.msixbundle","size":100,"content_type":"application/octet-stream"}
          ]
        }
        """;

        Assert.Throws<InvalidDataException>(() => GitHubReleaseUpdateManifest.Parse(json, "x64"));
    }

    [Fact]
    public void InvalidCurrentVersionIsRejectedInsteadOfSilentlyUpdating()
    {
        var manifest = GitHubReleaseUpdateManifest.Parse(ReleaseJson("v0.9.4", draft: false, prerelease: false));
        Assert.Throws<FormatException>(() => manifest.IsNewerThan("preview"));
    }

    private static string ReleaseJson(string tag, bool draft, bool prerelease) => $$"""
    {
      "tag_name":"{{tag}}",
      "name":"UCAD {{tag}} — Test Release",
      "body":"Release notes",
      "html_url":"https://github.com/KiYouJyo/UCAD/releases/tag/{{tag}}",
      "published_at":"2026-08-19T00:00:00Z",
      "draft":{{draft.ToString().ToLowerInvariant()}},
      "prerelease":{{prerelease.ToString().ToLowerInvariant()}},
      "assets":[
        {"name":"UCAD_0.9.4.0_x64.msixbundle","browser_download_url":"https://github.com/KiYouJyo/UCAD/releases/download/v0.9.4/UCAD_0.9.4.0_x64.msixbundle","size":123456,"content_type":"application/octet-stream"},
        {"name":"UCAD-v0.9.4-x64-one-click.zip","browser_download_url":"https://github.com/KiYouJyo/UCAD/releases/download/v0.9.4/UCAD-v0.9.4-x64-one-click.zip","size":234567,"content_type":"application/zip"},
        {"name":"SHA256SUMS.txt","browser_download_url":"https://github.com/KiYouJyo/UCAD/releases/download/v0.9.4/SHA256SUMS.txt","size":256,"content_type":"text/plain"}
      ]
    }
    """;
}
