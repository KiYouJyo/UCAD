using System.Xml.Linq;
using Xunit;

namespace UCAD.Core.Tests;

public sealed class LocalizationResourceParityTests
{
    private static readonly string[] Languages = ["zh-CN", "ja-JP", "en-US"];
    private static readonly string[] ResourceMaps = ["Resources.resw", "UcadV039.resw", "ShellLive.resw", "AuthoringLive.resw", "StartLive.resw", "UpdateLive.resw"];

    [Fact]
    public void EveryUserInterfaceResourceMapHasExactTriLanguageKeyParity()
    {
        var stringsRoot = LocateStringsRoot();
        foreach (var map in ResourceMaps)
        {
            var baseline = ReadKeys(Path.Combine(stringsRoot, Languages[0], map));
            Assert.NotEmpty(baseline);
            foreach (var language in Languages.Skip(1))
            {
                var keys = ReadKeys(Path.Combine(stringsRoot, language, map));
                var missing = baseline.Except(keys, StringComparer.Ordinal).OrderBy(value => value).ToArray();
                var extra = keys.Except(baseline, StringComparer.Ordinal).OrderBy(value => value).ToArray();
                Assert.True(
                    missing.Length == 0 && extra.Length == 0,
                    $"{map} {language} key mismatch. Missing=[{string.Join(", ", missing)}] Extra=[{string.Join(", ", extra)}]");
            }
        }
    }

    [Fact]
    public void StartGeneratedToolsAndUpdateCriticalStringsAreActuallyTranslated()
    {
        var stringsRoot = LocateStringsRoot();
        var expected = new Dictionary<string, (string Start, string Move, string Erase, string Download)>(StringComparer.Ordinal)
        {
            ["zh-CN"] = ("开始使用 UCAD", "移动", "删除", "下载并安装"),
            ["ja-JP"] = ("UCAD を開始", "移動", "削除", "ダウンロードしてインストール"),
            ["en-US"] = ("Start with UCAD", "Move", "Erase", "Download & install")
        };

        foreach (var language in Languages)
        {
            var start = ReadValues(Path.Combine(stringsRoot, language, "StartLive.resw"));
            var shell = ReadValues(Path.Combine(stringsRoot, language, "ShellLive.resw"));
            var update = ReadValues(Path.Combine(stringsRoot, language, "UpdateLive.resw"));
            Assert.Equal(expected[language].Start, start["Title"]);
            Assert.Equal(expected[language].Move, shell["CommandLabel_MOVE"]);
            Assert.Equal(expected[language].Erase, shell["CommandLabel_ERASE"]);
            Assert.Equal(expected[language].Download, update["DownloadInstall"]);
        }
    }

    private static HashSet<string> ReadKeys(string path) =>
        ReadValues(path).Keys.ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ReadValues(string path)
    {
        Assert.True(File.Exists(path), $"Localization resource file was not found: {path}");
        var document = XDocument.Load(path);
        return document.Root!
            .Elements("data")
            .Where(element => element.Attribute("name") is not null)
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static string LocateStringsRoot()
    {
        foreach (var seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(seed);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "src", "UCAD.App", "Strings");
                if (Directory.Exists(candidate)) return candidate;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate src/UCAD.App/Strings from the test host.");
    }
}
