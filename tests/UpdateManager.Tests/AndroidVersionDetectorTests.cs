using UpdateManager.Core.Versioning;

namespace UpdateManager.Tests;

public sealed class AndroidVersionDetectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("AndroidVersion_").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Detect_FindsVersionNameInNestedConfig_UsesNameInsteadOfCode()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "metadata")).FullName;
        File.WriteAllText(Path.Combine(folder, "channel.json"),
            "{\"versionCode\":911,\"versionName\":\"0.9.11\",\"apkUrl\":\"https://example.com/App.apk\"}",
            System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "broken.json"), "{");
        Assert.Equal("0.9.11", AndroidVersionDetector.Detect(_root));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"versionCode\":911}")]
    [InlineData("{\"versionName\":911}")]
    [InlineData("{\"versionName\":\"1.0-beta\"}")]
    public void Detect_WithoutValidVersion_ReturnsNull(string json)
    {
        File.WriteAllText(Path.Combine(_root, "latest.json"), json);
        Assert.Null(AndroidVersionDetector.Detect(_root));
    }

    [Fact]
    public void Detect_ConflictingManifests_RequiresManualVersion()
    {
        File.WriteAllText(Path.Combine(_root, "latest.json"), "{\"versionName\":\"1.0.0\"}");
        File.WriteAllText(Path.Combine(_root, "other.json"), "{\"versionName\":\"2.0.0\"}");
        Assert.Null(AndroidVersionDetector.Detect(_root));
        Assert.Null(AndroidVersionDetector.Detect(Path.Combine(_root, "missing")));
    }
}
