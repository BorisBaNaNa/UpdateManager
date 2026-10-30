using System.Security.Cryptography;
using System.Text;
using UpdateManager.Core.Common;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Security;

namespace UpdateManager.Tests;

/// <summary>Платформа Android: маски файлов и подпись по маске.</summary>
public sealed class AndroidTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("UpdateManagerAndroid_").FullName;
    private readonly string _keys;
    private readonly string _build;

    public AndroidTests()
    {
        _keys = Path.Combine(_root, "project");
        _build = Path.Combine(_root, "build");
        Directory.CreateDirectory(_keys);
        Directory.CreateDirectory(Path.Combine(_build, "manifests"));
        new RsaKeyManager().Generate(_keys);
        File.WriteAllBytes(Path.Combine(_build, "App.apk"), new byte[] { 0x50, 0x4B, 3, 4, 0, 1, 2 });
        File.WriteAllBytes(Path.Combine(_build, "App-x86_64.apk"), new byte[] { 0x50, 0x4B, 9 });
        // Манифест с BOM и CRLF — подпись обязана быть по сырым байтам, без «нормализации» текста.
        File.WriteAllBytes(Path.Combine(_build, "latest.json"),
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\r\n  \"version\": \"0.9.24\"\r\n}")).ToArray());
        File.WriteAllText(Path.Combine(_build, "manifests", "extra.json"), "{}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* временная папка */ }
    }

    private string PrivateKey => File.ReadAllText(Path.Combine(_keys, RsaKeyManager.PrivateKeyFileName));
    private string PublicKey => File.ReadAllText(Path.Combine(_keys, RsaKeyManager.PublicKeyFileName));

    [Theory]
    [InlineData("latest.json", "latest.json", true)]
    [InlineData("LATEST.JSON", "latest.json", true)]
    [InlineData("App.apk", "*.apk", true)]
    [InlineData("manifests/extra.json", "*.json", false)]
    [InlineData("manifests/extra.json", "manifests/*.json", true)]
    [InlineData(@"manifests\extra.json", "manifests/*.json", true)]
    [InlineData("manifests/extra.json", "**/*.json", true)]
    [InlineData("latest.json", "**/*.json", true)]
    [InlineData("a.apk", "?.apk", true)]
    [InlineData("ab.apk", "?.apk", false)]
    public void FileMask_Glob(string path, string mask, bool expected)
    {
        Assert.Equal(expected, FileMask.IsMatch(path, mask));
    }

    [Fact]
    public void Sign_WritesBase64SigOverRawBytes_AndVerifies()
    {
        var signed = new FileSigner().Sign(_build, new[] { "latest.json" }, PrivateKey, PublicKey);

        Assert.Equal(new[] { "latest.json" }, signed);
        var sigPath = Path.Combine(_build, "latest.json.sig");
        var sigBytes = File.ReadAllBytes(sigPath);
        Assert.False(sigBytes.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble())); // без BOM
        Assert.NotEqual((byte)'\n', sigBytes[^1]); // без перевода строки

        // Ровно то, что ждёт клиент: RSA SHA-256 PKCS#1 v1.5 по сырым байтам файла.
        using var rsa = RSA.Create();
        rsa.FromXmlString(PublicKey);
        Assert.True(rsa.VerifyData(File.ReadAllBytes(Path.Combine(_build, "latest.json")),
            Convert.FromBase64String(File.ReadAllText(sigPath)), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Sign_IsDeterministic()
    {
        var signer = new FileSigner();
        signer.Sign(_build, new[] { "latest.json" }, PrivateKey, PublicKey);
        var first = File.ReadAllText(Path.Combine(_build, "latest.json.sig"));
        signer.Sign(_build, new[] { "latest.json" }, PrivateKey, PublicKey);

        Assert.Equal(first, File.ReadAllText(Path.Combine(_build, "latest.json.sig")));
    }

    [Fact]
    public void Sign_MaskWithoutMatches_IsError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new FileSigner().Sign(_build, new[] { "latest.json", "missing/*.json" }, PrivateKey, PublicKey));

        Assert.Contains("missing/*.json", ex.Message);
    }

    [Fact]
    public void Package_ExcludesMasksAndForeignSignatures()
    {
        File.WriteAllText(Path.Combine(_build, "App.apk.sig"), "старый чужой"); // apk не подписывается
        new FileSigner().Sign(_build, new[] { "latest.json" }, PrivateKey, PublicKey);

        var files = AndroidPackage.Files(_build, new[] { "latest.json" }, new[] { "App-x86_64.apk" });

        Assert.Equal(new[] { "App.apk", "latest.json", "latest.json.sig", "manifests/extra.json" }, files);
        Assert.Equal(new[] { "latest.json" }, AndroidPackage.SignedFiles(files, new[] { "latest.json" }));
    }

    [Fact]
    public void FolderDelivery_MirrorsAndKeepsChannel()
    {
        var server = Path.Combine(_root, "server");
        Directory.CreateDirectory(Path.Combine(server, "AndroidPatch"));
        File.WriteAllText(Path.Combine(server, "AndroidPatch", "latest.json"), "android");
        File.WriteAllText(Path.Combine(server, "old.lzdat"), "старый патч");
        Directory.CreateDirectory(Path.Combine(server, "OldFolder"));
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "VersionInfo.info"), "new");

        new FolderDeliveryTarget(server).Deliver(output, new[] { "AndroidPatch" });

        Assert.True(File.Exists(Path.Combine(server, "VersionInfo.info")));
        Assert.False(File.Exists(Path.Combine(server, "old.lzdat")));
        Assert.False(Directory.Exists(Path.Combine(server, "OldFolder")));
        Assert.Equal("android", File.ReadAllText(Path.Combine(server, "AndroidPatch", "latest.json")));
    }

    [Fact]
    public void FolderDelivery_RefusesDriveRoot()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new FolderDeliveryTarget(Path.GetPathRoot(_root)!).Deliver(_build, Array.Empty<string>()));
    }

    [Fact]
    public void ChannelVerifier_PassesOnPublishedChannel_FailsOnForeignSig()
    {
        var signMasks = new[] { "latest.json" };
        new FileSigner().Sign(_build, signMasks, PrivateKey, PublicKey);
        var files = AndroidPackage.Files(_build, signMasks, new[] { "App-x86_64.apk" });
        var channel = Path.Combine(_root, "server", "AndroidPatch");
        new FolderDeliveryTarget(channel).Deliver(AndroidPackage.Stage(_build, files), Array.Empty<string>());
        var url = new Uri(channel + Path.DirectorySeparatorChar).AbsoluteUri;

        var ok = new AndroidChannelVerifier(url, _build, files, AndroidPackage.SignedFiles(files, signMasks), PublicKey);
        RunToEnd(ok);
        Assert.True(ok.Succeeded, ok.ResultDetails);

        // На сервере остался .sig от другого ключа — клиент счёл бы это подделкой.
        var other = Path.Combine(_root, "other");
        Directory.CreateDirectory(other);
        new RsaKeyManager().Generate(other);
        var bad = new AndroidChannelVerifier(url, _build, files, AndroidPackage.SignedFiles(files, signMasks),
            File.ReadAllText(Path.Combine(other, RsaKeyManager.PublicKeyFileName)));
        RunToEnd(bad);
        Assert.False(bad.Succeeded);
        Assert.Contains("не сходится", bad.ResultDetails);
    }

    private static void RunToEnd(UpdateManager.Core.Operations.IEngineOperation op)
    {
        Assert.True(op.Start());
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (op.IsRunning)
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(30));
            Thread.Sleep(20);
        }
    }

    [Fact]
    public void Sign_WrongKeyPair_FailsSelfCheck()
    {
        var other = Path.Combine(_root, "other");
        Directory.CreateDirectory(other);
        new RsaKeyManager().Generate(other);

        Assert.Throws<InvalidOperationException>(() => new FileSigner().Sign(_build, new[] { "latest.json" },
            PrivateKey, File.ReadAllText(Path.Combine(other, RsaKeyManager.PublicKeyFileName))));
    }
}
