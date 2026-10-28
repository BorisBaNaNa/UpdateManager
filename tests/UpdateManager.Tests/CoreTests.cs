using UpdateManager.Core.Common;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Project;
using UpdateManager.Core.SelfUpdate;
using UpdateManager.Core.Versioning;

namespace UpdateManager.Tests;

public class VersionRulesTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("1.0", true)]
    [InlineData("1.0.0.0", true)]
    [InlineData("2.10.3", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("1.2-b1", false)]
    [InlineData("v1.0", false)]
    [InlineData("1.0.0a", false)]
    public void IsValid(string version, bool expected) => Assert.Equal(expected, VersionRules.IsValid(version));
}

public sealed class VersionDetectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("UpdateManagerTests_").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void PrefersVersionHolderOverExe()
    {
        // Как в Release/ после scripts/release.ps1: exe + UpdateManager_vers.sptv с версией патча.
        File.Copy(Path.Combine(AppContext.BaseDirectory, "UpdateManager.exe"), Path.Combine(_root, "UpdateManager.exe"));
        File.WriteAllText(Path.Combine(_root, "UpdateManager_vers.sptv"), "2.3.4.0");

        var build = new VersionDetector().Detect(_root, "UpdateManager", "");

        Assert.Equal("UpdateManager.exe", build.ExecutableName);
        Assert.Equal("2.3.4.0", build.Version);
    }
}

public class DownloadUrlTests
{
    [Fact]
    public void Split_SeparatesServerAndDirectory()
    {
        DownloadUrl.Split("http://host:8080/a/b/", out var server, out var directory);
        Assert.Equal("http://host:8080/", server);
        Assert.Equal("a/b", directory);
    }

    [Fact]
    public void Split_Empty()
    {
        DownloadUrl.Split("  ", out var server, out var directory);
        Assert.Equal("", server);
        Assert.Equal("", directory);
    }

    [Theory]
    [InlineData("http://host", "a/b", "http://host/a/b/")]
    [InlineData("http://host/", "/a/b/", "http://host/a/b/")]
    [InlineData("http://host", "", "http://host/")]
    [InlineData("", "a", "")]
    public void Join_AddsTrailingSlash(string server, string directory, string expected) =>
        Assert.Equal(expected, DownloadUrl.Join(server, directory));

    [Theory]
    [InlineData("patches", "UpdaterTest/Output", "/patches/UpdaterTest/Output")]
    [InlineData("", "a", "/a")]
    [InlineData("/root/", "", "/root")]
    [InlineData("", "", "/")]
    public void CombineRemote(string root, string directory, string expected) =>
        Assert.Equal(expected, DownloadUrl.CombineRemote(root, directory));

    [Theory]
    [InlineData("/keys", "/patches/app", false)]
    [InlineData("/patches", "/patches/app", true)]
    [InlineData("patches/app/", "/patches/app", true)]
    [InlineData("\\patches\\app\\keys", "/patches/app", true)]
    [InlineData("/patches/app2", "/patches/app", false)]
    [InlineData("/", "/patches/app", false)]
    public void RemotePathsOverlap(string a, string b, bool expected) =>
        Assert.Equal(expected, DownloadUrl.RemotePathsOverlap(a, b));
}

public sealed class FileUtilsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("UpdateManagerTests_").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void PathsOverlap()
    {
        var project = Path.Combine(_root, "project");
        Assert.True(FileUtils.PathsOverlap(project, project + "\\"));
        Assert.True(FileUtils.PathsOverlap(project, Path.Combine(project, "build")));
        Assert.True(FileUtils.PathsOverlap(Path.Combine(project, "build"), project));
        Assert.False(FileUtils.PathsOverlap(project, Path.Combine(_root, "project2")));
    }

    [Fact]
    public void FindNonAsciiNames_StopsAtMax()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Данные"));
        File.WriteAllText(Path.Combine(_root, "ok.txt"), "");
        File.WriteAllText(Path.Combine(_root, "Данные", "файл.txt"), "");
        File.WriteAllText(Path.Combine(_root, "ещё.txt"), "");

        Assert.Equal(3, FileUtils.FindNonAsciiNames(_root, 10).Count);
        Assert.Equal(2, FileUtils.FindNonAsciiNames(_root, 2).Count);
    }

    [Fact]
    public void CopyDirectory_Recursive()
    {
        var src = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(src, "sub"));
        File.WriteAllText(Path.Combine(src, "sub", "a.txt"), "a");

        var dst = Path.Combine(_root, "dst");
        FileUtils.CopyDirectory(src, dst);
        Assert.Equal("a", File.ReadAllText(Path.Combine(dst, "sub", "a.txt")));
    }
}

public sealed class SelfUpdateConfigTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("UpdateManagerTests_").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SelfUpdateConfig LoadJson(string json)
    {
        var path = Path.Combine(_root, "appsettings.json");
        File.WriteAllText(path, json);
        return SelfUpdateConfig.Load(path);
    }

    [Fact]
    public void ReadsSectionWithComments()
    {
        var config = LoadJson("""
            {
              // комментарий
              "SelfUpdate": { "VersionInfoUrl": " http://h/UpdateManager/VersionInfo.info ", "PublicKey": "<RSAKeyValue/>" },
            }
            """);
        Assert.True(config.IsEnabled);
        Assert.True(config.HasPublicKey);
        Assert.Equal("http://h/UpdateManager/VersionInfo.info", config.VersionInfoUrl);
        Assert.Equal("http://h/UpdateManager/", config.BaseDownloadUrl);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"SelfUpdate\": { \"VersionInfoUrl\": \"\" } }")]
    [InlineData("not json")]
    public void DisabledWhenMissingOrBroken(string json) => Assert.False(LoadJson(json).IsEnabled);

    [Fact]
    public void DisabledWhenNoFile() => Assert.False(SelfUpdateConfig.Load(Path.Combine(_root, "none.json")).IsEnabled);
}

public class SelfProjectPresetTests
{
    [Fact]
    public void Apply_EnablesSelfPatchingAndMergesIgnoredPaths()
    {
        var settings = new ProjectSettings { IgnoredPaths = { "*.pdb", "custom" } };
        var config = new SelfUpdateConfig("http://h/UpdateManager/VersionInfo.info", "");

        Assert.False(SelfProjectPreset.IsApplied(settings));
        SelfProjectPreset.Apply(settings, config);

        Assert.True(SelfProjectPreset.IsApplied(settings));
        Assert.Equal("http://h/UpdateManager/", settings.BaseDownloadURL);
        Assert.Equal(1, settings.IgnoredPaths.Count(p => p == "*.pdb"));
        Assert.Contains("custom", settings.IgnoredPaths);
        Assert.Contains("*spt_logs.txt", settings.IgnoredPaths);
    }

    [Fact]
    public void IsApplied_NotWithEngineDefaults()
    {
        // Новый проект движка: IsSelfPatchingApp уже true, но URL пуст и масок нет — пресет нужен.
        var fresh = new ProjectSettings { IsSelfPatchingApp = true, IgnoredPaths = { "*spt_logs.txt" } };
        Assert.False(SelfProjectPreset.IsApplied(fresh));

        // Свой URL + все маски — считаем применённым, не пристаём.
        var custom = new ProjectSettings
        {
            IsSelfPatchingApp = true,
            BaseDownloadURL = "http://test-server/UpdateManager/",
            IgnoredPaths = { "*.pdb", "*.vshost.*", "*spt_logs.txt" }
        };
        Assert.True(SelfProjectPreset.IsApplied(custom));
    }

    [Fact]
    public void Apply_KeepsBaseUrlWhenConfigEmpty()
    {
        var settings = new ProjectSettings { BaseDownloadURL = "http://mine/" };
        SelfProjectPreset.Apply(settings, new SelfUpdateConfig("", ""));
        Assert.Equal("http://mine/", settings.BaseDownloadURL);
    }
}

public sealed class StoresTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("UpdateManagerTests_").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Recent_MostRecentFirstWithoutDuplicates()
    {
        var store = new RecentProjectsStore(Path.Combine(_root, "recent.xml"));
        store.Add(@"C:\a");
        store.Add(@"C:\b");
        store.Add(@"c:\A");
        Assert.Equal(new[] { @"c:\A", @"C:\b" }, store.Load());

        store.Remove(@"C:\B");
        Assert.Equal(new[] { @"c:\A" }, store.Load());
    }

    [Fact]
    public void Recent_OldLongFileIsTrimmedToThree()
    {
        var path = Path.Combine(_root, "recent-old.xml");
        File.WriteAllText(path, "<RecentProjects>" +
            string.Concat(Enumerable.Range(1, 10).Select(i => "<Project>C:\\p" + i + "</Project>")) +
            "</RecentProjects>");

        Assert.Equal(new[] { "C:\\p1", "C:\\p2", "C:\\p3" }, new RecentProjectsStore(path).Load());
    }

    [Fact]
    public void Recent_KeepsMaxItems()
    {
        var store = new RecentProjectsStore(Path.Combine(_root, "recent.xml"));
        for (int i = 0; i < RecentProjectsStore.MaxItems + 3; i++)
            store.Add(@"C:\p" + i);
        Assert.Equal(RecentProjectsStore.MaxItems, store.Load().Count);
    }

    [Fact]
    public void Ftp_RoundTripsWithEncryptedPassword()
    {
        var path = Path.Combine(_root, "ftp.xml");
        var store = new FtpConnectionStore(path);
        store.Save(@"C:\proj", new FtpConnection
        {
            Host = "ftp.example.com", Port = 2121, Username = "u", Password = "секрет",
            RemotePath = "patches", PrivateKeyRemotePath = "keys"
        });

        Assert.DoesNotContain("секрет", File.ReadAllText(path));

        var loaded = store.Load(@"c:\PROJ")!;
        Assert.Equal("ftp.example.com", loaded.Host);
        Assert.Equal(2121, loaded.Port);
        Assert.Equal("секрет", loaded.Password);
        Assert.False(loaded.PasswordDecryptFailed);
        Assert.Equal("keys", loaded.PrivateKeyRemotePath);
        Assert.Null(store.Load(@"C:\other"));
    }

    [Fact]
    public void Ftp_UndecryptablePasswordIsFlagged()
    {
        var path = Path.Combine(_root, "ftp.xml");
        File.WriteAllText(path, """
            <FtpConnections><Connection project="C:\p"><Host>h</Host><Port>21</Port>
            <Password>AAAA</Password></Connection></FtpConnections>
            """);

        var loaded = new FtpConnectionStore(path).Load(@"C:\p")!;
        Assert.True(loaded.PasswordDecryptFailed);
        Assert.Equal("", loaded.Password);
    }

    [Fact]
    public void ProjectMeta_RoundTrip()
    {
        var meta = new ProjectMeta
        {
            MainExecutable = @"bin\App.exe",
            LastBuildSource = @"C:\build",
            Delivery = new DeliveryConfig { Method = DeliveryMethods.Ftp, Path = "" },
            LastDeliveredAt = new DateTime(2026, 10, 27, 12, 30, 0),
            LastBuiltSettings = "abc",
            SignPatch = true
        };
        meta.DeliveredVersions["1.0.0"] = new DateTime(2026, 10, 27, 10, 0, 0);
        meta.DeliveredVersions["1.0.1"] = new DateTime(2026, 10, 27, 11, 0, 0);
        meta.PatchedVersions["1.0.1"] = new DateTime(2026, 10, 27, 10, 50, 0);
        meta.VerifiedVersions["1.0.0"] = new VerificationResult(new DateTime(2026, 10, 27, 10, 5, 0), true, "");
        meta.VerifiedVersions["1.0.1"] = new VerificationResult(new DateTime(2026, 10, 27, 11, 5, 0), false, "timeout \"30 s\"");
        meta.Save(_root);

        var loaded = ProjectMeta.Load(_root);
        Assert.Equal(meta.MainExecutable, loaded.MainExecutable);
        Assert.Equal(meta.LastBuildSource, loaded.LastBuildSource);
        Assert.Equal(DeliveryMethods.Ftp, loaded.Delivery.Method);
        Assert.Equal(meta.LastDeliveredAt, loaded.LastDeliveredAt);
        Assert.Equal("abc", loaded.LastBuiltSettings);
        Assert.True(loaded.SignPatch);
        Assert.Equal(new DateTime(2026, 10, 27, 10, 0, 0), loaded.DeliveredVersions["1.0.0"]);
        Assert.Equal(new DateTime(2026, 10, 27, 11, 0, 0), loaded.DeliveredVersions["1.0.1"]);
        Assert.Equal(new DateTime(2026, 10, 27, 10, 50, 0), loaded.PatchedVersions["1.0.1"]);
        Assert.True(loaded.VerifiedVersions["1.0.0"].Succeeded);
        var failed = loaded.VerifiedVersions["1.0.1"];
        Assert.False(failed.Succeeded);
        Assert.Equal(new DateTime(2026, 10, 27, 11, 5, 0), failed.At);
        Assert.Equal("timeout \"30 s\"", failed.Details);
        Assert.True(loaded.WindowsEnabled);
        Assert.False(loaded.AndroidEnabled);
    }

    [Fact]
    public void ProjectMeta_AndroidRoundTrip()
    {
        var meta = new ProjectMeta { WindowsEnabled = false, AndroidEnabled = true };
        meta.Android.BuildSource = @"D:\AlmaProfile\build\android";
        meta.Android.Channel = "AndroidPatch";
        meta.Android.SignMasks.Add("latest.json");
        meta.Android.SignMasks.Add("manifests/*.json");
        meta.Android.ExcludeMasks.Add("AlmaProfile-x86_64.apk");
        meta.AndroidSignedVersions["0.9.24"] = new DateTime(2026, 10, 28, 9, 0, 0);
        meta.AndroidDeliveredVersions["0.9.24"] = new DateTime(2026, 10, 28, 9, 5, 0);
        meta.AndroidVerifiedVersions["0.9.24"] = new VerificationResult(new DateTime(2026, 10, 28, 9, 6, 0), true, "");
        meta.Save(_root);

        var loaded = ProjectMeta.Load(_root);
        Assert.False(loaded.WindowsEnabled);
        Assert.True(loaded.AndroidEnabled);
        Assert.Equal(@"D:\AlmaProfile\build\android", loaded.Android.BuildSource);
        Assert.Equal("AndroidPatch", loaded.Android.Channel);
        Assert.Equal(new[] { "latest.json", "manifests/*.json" }, loaded.Android.SignMasks);
        Assert.Equal(new[] { "AlmaProfile-x86_64.apk" }, loaded.Android.ExcludeMasks);
        Assert.Equal(new DateTime(2026, 10, 28, 9, 0, 0), loaded.AndroidSignedVersions["0.9.24"]);
        Assert.Equal(new DateTime(2026, 10, 28, 9, 5, 0), loaded.AndroidDeliveredVersions["0.9.24"]);
        Assert.True(loaded.AndroidVerifiedVersions["0.9.24"].Succeeded);
        Assert.True(loaded.Android.Sign);
    }

    [Fact]
    public void ProjectMeta_AndroidSignOff_RoundTripAndNoActiveMasks()
    {
        var meta = new ProjectMeta { AndroidEnabled = true };
        meta.Android.Sign = false;
        meta.Android.SignMasks.Add("latest.json");
        meta.Save(_root);

        var loaded = ProjectMeta.Load(_root);
        Assert.False(loaded.Android.Sign);
        Assert.Equal(new[] { "latest.json" }, loaded.Android.SignMasks); // маски помнятся до включения подписи
        Assert.Empty(loaded.Android.ActiveSignMasks);
    }

    [Fact]
    public void ProjectMeta_ForgetVersion_RemovedVersionsRoundTrip()
    {
        var meta = new ProjectMeta();
        meta.PatchedVersions["1.0.0"] = DateTime.Now;
        meta.AndroidSignedVersions["1.0.0"] = DateTime.Now;
        meta.AndroidRecords["1.0.0"] = new AndroidRecord();
        meta.ForgetVersion("1.0.0");
        meta.Save(_root);

        var loaded = ProjectMeta.Load(_root);
        Assert.Empty(loaded.PatchedVersions);
        Assert.Empty(loaded.AndroidSignedVersions);
        Assert.Empty(loaded.AndroidRecords);
        Assert.Contains("1.0.0", loaded.RemovedVersions);
    }

    [Fact]
    public void ProjectMeta_AndroidRecordsRoundTrip()
    {
        var meta = new ProjectMeta();
        meta.AndroidRecords["1.0.0"] = new AndroidRecord
        {
            Signed = true,
            Masks = { "latest.json" },
            Files = { ["App.apk"] = "aa", ["sub/latest.json"] = "bb" }
        };
        meta.AndroidRecords["1.0.1"] = new AndroidRecord { Signed = false };
        meta.Save(_root);

        var loaded = ProjectMeta.Load(_root);
        Assert.True(loaded.AndroidRecords["1.0.0"].Signed);
        Assert.Equal(new[] { "latest.json" }, loaded.AndroidRecords["1.0.0"].Masks);
        Assert.Equal("bb", loaded.AndroidRecords["1.0.0"].Files["sub/latest.json"]);
        Assert.False(loaded.AndroidRecords["1.0.1"].Signed);
        Assert.Empty(loaded.AndroidRecords["1.0.1"].Files);
    }

    [Fact]
    public void AndroidPackage_HashesSkipSig_ChangesListed()
    {
        var folder = Path.Combine(_root, "android");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "App.apk"), "apk");
        File.WriteAllText(Path.Combine(folder, "sub", "latest.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "sub", "latest.json.sig"), "sig");
        var recorded = AndroidPackage.Hashes(folder, new[] { "App.apk", "sub/latest.json", "sub/latest.json.sig" });
        Assert.Equal(new[] { "App.apk", "sub/latest.json" }, recorded.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(AndroidPackage.Changes(recorded, AndroidPackage.Hashes(folder, new[] { "App.apk", "sub/latest.json" })));

        File.WriteAllText(Path.Combine(folder, "App.apk"), "apk2");
        File.WriteAllText(Path.Combine(folder, "extra.txt"), "x");
        var current = AndroidPackage.Hashes(folder, new[] { "App.apk", "extra.txt" });
        Assert.Equal(new[] { "изменён: App.apk", "новый: extra.txt", "пропал: sub/latest.json" },
            AndroidPackage.Changes(recorded, current));
    }

    [Theory]
    [InlineData("<Sign>latest.json</Sign>", true)]
    [InlineData("", false)]
    public void ProjectMeta_AndroidWithoutSignAttribute_SignFollowsMasks(string masks, bool sign)
    {
        // Мета 2.1.0: флажка нет, подписью управляли маски (пусто — выпуск без подписи).
        File.WriteAllText(Path.Combine(_root, ProjectMeta.FileName),
            "<UpdateManagerProject schemaVersion=\"1\"><Android buildSource=\"\" channel=\"AndroidPatch\">" + masks +
            "</Android></UpdateManagerProject>");

        Assert.Equal(sign, ProjectMeta.Load(_root).Android.Sign);
    }
}

public class ExceptionTextTests
{
    [Fact]
    public void Full_IncludesInnerExceptions()
    {
        var ex = new Exception("Error while uploading the file to the server. See InnerException for more info.",
            new IOException("550 Permission denied", new IOException("550 Permission denied")));

        Assert.Equal("Error while uploading the file to the server. See InnerException for more info. → 550 Permission denied",
            ExceptionText.Full(ex));
    }
}

public class FtpErrorTextTests
{
    [Fact]
    public void Describe_ServerCodeInHumanWords()
    {
        // Как FluentFTP: обёртка "See InnerException", настоящая причина — ответ сервера внутри.
        var ex = new Exception("Error while uploading the file to the server. See InnerException for more info.",
            new FluentFTP.Exceptions.FtpCommandException("553", "Could not create file."));

        Assert.Equal("Сервер не разрешил создать файл — у пользователя нет прав на запись в эту папку " +
            "(553 Could not create file.).", FtpErrorText.Describe(ex));
    }

    [Fact]
    public void Describe_WrongPassword() =>
        Assert.StartsWith("Неверный логин или пароль",
            FtpErrorText.Describe(new FluentFTP.Exceptions.FtpAuthenticationException("530", "Login incorrect.")));

    [Fact]
    public void Describe_ConnectionRefused() =>
        Assert.StartsWith("Сервер отказал в подключении",
            FtpErrorText.Describe(new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused)));

    [Fact]
    public void Describe_KeepsOwnMessages() =>
        Assert.Equal("Не удалось создать временную папку /a_new.",
            FtpErrorText.Describe(new FtpDeliveryException("Не удалось создать временную папку /a_new.",
                new FluentFTP.Exceptions.FtpCommandException("550", "Permission denied"))));
}
