using System.Diagnostics;
using SimplePatchToolSecurity;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;
using UpdateManager.Core.Security;

namespace UpdateManager.Tests;

/// <summary>
/// Сквозной прогон движка SimplePatchTool (CLR 2.0) под .NET 10: создать проект → собрать патч →
/// подписать → «Проверить» (клиент реально качает через file:///) → вторая версия и обновление клиента.
/// Это и есть спайк из спека 2.0: если он зелёный, движок на новой платформе работает.
/// </summary>
public sealed class EnginePipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UpdateManagerTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* временная папка — не критично */ }
    }

    [Fact]
    public void BuildSignVerifyAndUpdate()
    {
        var service = new ProjectService();
        var projectDir = Path.Combine(_root, "project");
        var buildDir = Path.Combine(_root, "build");
        var clientDir = Path.Combine(_root, "client");
        Directory.CreateDirectory(clientDir);

        // 1. Проект: движок создаёт структуру, мы кладём SelfPatcher и мету.
        var project = service.Create(projectDir, "TestApp");
        Assert.Equal("TestApp", project.Name);
        Assert.True(File.Exists(Path.Combine(projectDir, ProjectMeta.FileName)));
        Assert.True(File.Exists(Path.Combine(projectDir, ProjectService.SelfPatcherFolder, "SelfPatcher.exe")));

        // Сервер = сама папка Output, клиент качает её через file:///.
        var output = service.GetOutputPath(projectDir);
        var settings = service.LoadSettings(projectDir);
        settings.BaseDownloadURL = new Uri(output).AbsoluteUri; // без «/» — SaveSettings добавит сам
        settings.CreateRepairPatch = true;
        settings.CreateInstallerPatch = true;
        settings.CreateIncrementalPatch = true;
        settings.SignPatch = true;
        service.SaveSettings(projectDir, settings);
        Assert.EndsWith("/", service.LoadSettings(projectDir).BaseDownloadURL);

        // 2. Версия 1.0.0.
        WriteBuild(buildDir, "first");
        service.StageBuild(projectDir, buildDir, "1.0.0");
        RunToEnd(new PatchBuilder(projectDir));

        // 3. Подпись RSA (SignedXml из System.Security.Cryptography.Xml).
        var keys = new RsaKeyManager();
        keys.Generate(projectDir);
        var signed = new PatchSigner().Sign(output, keys.ReadPrivateKey(projectDir));
        Assert.Contains("VersionInfo.info", signed);
        var publicKey = File.ReadAllText(keys.PublicKeyPath(projectDir));
        Assert.True(XMLSigner.VerifyXMLContents(File.ReadAllText(Path.Combine(output, "VersionInfo.info")), publicKey));

        // 4. «Проверить»: клиент с пустой папкой ставит 1.0.0.
        var versionInfoUrl = service.BuildVersionInfoUrl(service.LoadSettings(projectDir).BaseDownloadURL);
        RunToEnd(new PatchVerifier(clientDir, versionInfoUrl));
        Assert.Equal("first", File.ReadAllText(Path.Combine(clientDir, "app.txt")));
        Assert.True(File.Exists(Path.Combine(clientDir, "data", "payload.bin")));

        // 5. Версия 1.0.1 → инкрементальный патч (бинарный дифф), клиент обновляется.
        WriteBuild(buildDir, "second");
        service.StageBuild(projectDir, buildDir, "1.0.1");
        RunToEnd(new PatchBuilder(projectDir));
        Assert.True(Directory.EnumerateFiles(Path.Combine(output, "IncrementalPatch")).Any());

        RunToEnd(new PatchVerifier(clientDir, versionInfoUrl));
        Assert.Equal("second", File.ReadAllText(Path.Combine(clientDir, "app.txt")));

        var reopened = service.Open(projectDir);
        Assert.Equal(new[] { "1.0.0", "1.0.1" }, reopened.Versions.Select(v => v.Version));
        Assert.True(reopened.OutputReady);
    }

    // release.ps1 кладёт self-patcher в Release/SPPatcher — туда же его кладёт движок для self-patching
    // приложения. Билд с такой папкой собирается, а клиент получает self-patcher проекта.
    [Fact]
    public void SelfPatchingBuildWithSelfPatcherFolder_BuildsAndDelivers()
    {
        var service = new ProjectService();
        var projectDir = Path.Combine(_root, "project");
        var buildDir = Path.Combine(_root, "build");
        var clientDir = Path.Combine(_root, "client");
        Directory.CreateDirectory(clientDir);

        service.Create(projectDir, "TestApp");
        var output = service.GetOutputPath(projectDir);
        var settings = service.LoadSettings(projectDir);
        settings.BaseDownloadURL = new Uri(output).AbsoluteUri;
        settings.IsSelfPatchingApp = true;
        service.SaveSettings(projectDir, settings);

        var projectSelfPatcher = Path.Combine(projectDir, ProjectService.SelfPatcherFolder);
        WriteBuild(buildDir, "first");
        var buildSelfPatcher = Path.Combine(buildDir, "SPPatcher");
        Directory.CreateDirectory(buildSelfPatcher);
        foreach (var file in Directory.GetFiles(projectSelfPatcher))
            File.Copy(file, Path.Combine(buildSelfPatcher, Path.GetFileName(file)));
        // Даже если в билде self-patcher другой, движок кладёт в версию self-patcher проекта.
        File.WriteAllText(Path.Combine(buildSelfPatcher, "SelfPatcher.exe"), "stale");

        service.StageBuild(projectDir, buildDir, "1.0.0");
        RunToEnd(new PatchBuilder(projectDir));

        var versionInfoUrl = service.BuildVersionInfoUrl(service.LoadSettings(projectDir).BaseDownloadURL);
        RunToEnd(new PatchVerifier(clientDir, versionInfoUrl));
        foreach (var file in Directory.GetFiles(projectSelfPatcher))
            Assert.Equal(File.ReadAllBytes(file),
                File.ReadAllBytes(Path.Combine(clientDir, "SPPatcher", Path.GetFileName(file))));
    }

    private static void WriteBuild(string dir, string marker)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(Path.Combine(dir, "data"));
        File.WriteAllText(Path.Combine(dir, "app.txt"), marker);

        // Бинарник побольше, чтобы сработали сжатие и дифф; между версиями меняется хвост.
        var bytes = new byte[64 * 1024];
        new Random(42).NextBytes(bytes);
        for (int i = 0; i < marker.Length; i++)
            bytes[^(i + 1)] = (byte)marker[i];
        File.WriteAllBytes(Path.Combine(dir, "data", "payload.bin"), bytes);
    }

    // Крутим операцию как окно прогресса: поллим до конца, лог копим для сообщения об ошибке.
    private static void RunToEnd(IEngineOperation op)
    {
        Assert.True(op.Start(), op.Title + ": не запустилась");
        var log = new List<string>();
        var timer = Stopwatch.StartNew();
        while (op.IsRunning)
        {
            Assert.True(timer.Elapsed < TimeSpan.FromMinutes(2), op.Title + ": таймаут");
            Drain(op, log);
            Thread.Sleep(50);
        }
        Drain(op, log);
        Assert.True(op.Succeeded, op.Title + " failed: " + op.ResultDetails + "\n" + string.Join("\n", log));
    }

    private static void Drain(IEngineOperation op, List<string> log)
    {
        for (var line = op.FetchLog(); line != null; line = op.FetchLog())
            log.Add(line);
    }
}
