using SimplePatchToolCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpdateManager.Core.Common;
using UpdateManager.Core.Delivery;
using UpdateManager.Core.Operations;
using UpdateManager.Core.Project;
using UpdateManager.Core.Security;
using UpdateManager.Core.Versioning;

namespace UpdateManager.App.ViewModels;

/// <summary>
/// Платформа Android главного окна: без патчей — папка билда (собирается вне UpdateManager) →
/// подпись файлов по маске RSA-ключом проекта (выключаемая, как SignPatch у Windows) → выкладка в папку канала →
/// проверка канала.
/// Версию UpdateManager из файлов не читает — спрашивает при подписи.
/// </summary>
public sealed partial class MainWindowViewModel
{
    [ObservableProperty] private string _androidBuildSource = "";
    [ObservableProperty] private bool _isWindowsEnabled = true;
    [ObservableProperty] private bool _isAndroidEnabled;

    /// <summary>Подпись Android включена в настройках (выключена — этап только записывает версию).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AndroidSignMenuText))]
    private bool _isAndroidSigned = true;

    public string AndroidSignMenuText => IsAndroidSigned ? "Подписать файлы Android" : "Записать версию Android";

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task BrowseAndroidBuild()
    {
        if (_project == null)
            return;

        var folder = await _dialogs.PickFolderAsync("Папка билда Android (apk, манифест)");
        if (folder == null)
            return;

        if (FileUtils.PathsOverlap(folder, _project.RootPath))
        {
            await _dialogs.ShowErrorAsync(BuildSourceOverlapMessage);
            return;
        }

        _project.Meta.Android.BuildSource = folder;
        _project.Meta.Save(_project.RootPath);
        ShowProject();
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task SignAndroid()
    {
        if (_project == null)
            return;

        var meta = _project.Meta;
        var folder = meta.Android.BuildSource;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            await _dialogs.ShowErrorAsync("Папка билда Android не выбрана или не существует.");
            return;
        }

        // Подпись выключена — как SignPatch у Windows: ни ключ, ни маски не нужны, этап только записывает версию.
        var masks = meta.Android.ActiveSignMasks;
        if (meta.Android.Sign && masks.Count == 0)
        {
            await _dialogs.ShowErrorAsync("Подпись Android включена, но маски не заданы.\n" +
                "Укажите, какие файлы подписывать (настройки проекта → Android → Подписывать), " +
                "или снимите «Подписывать файлы RSA-ключом» — тогда выпуск уйдёт без .sig.");
            return;
        }
        if (masks.Count > 0 && !_keys.HasKeys(_project.RootPath))
        {
            await _dialogs.ShowErrorAsync("Для подписи нужны RSA-ключи проекта (" + RsaKeyManager.PrivateKeyFileName +
                " и " + RsaKeyManager.PublicKeyFileName + " в папке проекта) — те же, что подписывают патчи Windows.\n" +
                "Положите их в папку проекта или сгенерируйте («Безопасность → Сгенерировать RSA-ключи…»; " +
                "новые ключи не примут клиенты со старым публичным ключом), либо выключите подпись в настройках проекта.");
            return;
        }

        var version = await _dialogs.PromptAsync("Версия Android-выпуска",
            "Под какой версией записать выпуск в каталог? (UpdateManager не читает её из файлов.)",
            SuggestAndroidVersion());
        if (version == null)
            return; // отмена

        if (!VersionRules.IsValid(version))
        {
            await _dialogs.ShowErrorAsync("Версия \"" + version + "\" недопустима: только числа через точку, например 0.9.24.");
            return;
        }

        var latest = ReleaseCatalog.LatestAndroidVersion(meta);
        if (latest != null && VersionRules.IsValid(latest) && new VersionCode(version).CompareTo(new VersionCode(latest)) < 0)
        {
            await _dialogs.ShowErrorAsync("Версия " + version + " ниже последнего Android-выпуска (" + latest + ").\n" +
                "Подпишите версию не ниже " + latest + " (ровно " + latest + " — подписать тот же выпуск заново).");
            return;
        }

        // Номер откатанной или удалённой версии не повторяется: клиент с ней не увидел бы обновления.
        if (meta.RemovedAndroidVersions.Contains(version))
        {
            await _dialogs.ShowErrorAsync("Версия Android " + version + " уже выпускалась и была откатана или удалена.\n" +
                "Клиенты, успевшие её получить, не увидят обновления с тем же номером. Запишите следующую версию.");
            return;
        }

        ResetAndroidFrom(version, StageKind.Files);
        AndroidRecord record;
        try
        {
            if (masks.Count > 0)
            {
                var root = _project.RootPath;
                var privateKey = _keys.ReadPrivateKey(root);
                var publicKey = File.ReadAllText(_keys.PublicKeyPath(root));
                await Task.Run(() => new FileSigner().Sign(folder, masks, privateKey, publicKey));
            }

            // Как записана версия и отпечатки файлов: каталог и доставка опираются на них, а не на текущие настройки.
            var files = AndroidPackage.Files(folder, masks, meta.Android.ExcludeMasks);
            if (files.Count == 0)
                throw new InvalidOperationException("в папке билда нечего выкладывать: " + folder);
            record = new AndroidRecord
            {
                Signed = masks.Count > 0,
                Masks = new List<string>(masks),
                Files = await Task.Run(() => AndroidPackage.Hashes(folder, files))
            };

            // Выкладка версии сохраняется в проект (Versions/Android/<версия>): доставка и откат берут её
            // оттуда, а правки папки билда после записи — уже следующий, «новый» билд.
            var stored = ProjectService.AndroidVersionPath(_project.RootPath, version);
            await Task.Run(() => AndroidPackage.CopyTo(folder, files, stored));
        }
        catch (Exception ex)
        {
            ShowProject();
            await _dialogs.ShowErrorAsync((masks.Count > 0 ? "Подпись не выполнена:\n" : "Версия не записана:\n") + ex.Message);
            return;
        }

        meta.AndroidRecords[version] = record;
        meta.AndroidSignedVersions[version] = DateTime.Now;
        meta.Save(_project.RootPath);
        ShowProject();
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task DeliverAndroid()
    {
        if (_project == null)
            return;

        var meta = _project.Meta;
        var version = ReleaseCatalog.LatestAndroidVersion(meta);
        if (version == null || !meta.AndroidSignedVersions.ContainsKey(version))
        {
            await _dialogs.ShowErrorAsync(meta.Android.Sign
                ? "Сначала подпишите файлы Android-выпуска."
                : "Сначала запишите версию Android-выпуска.");
            return;
        }

        var package = await PrepareAndroidPackage(version, checkSignatures: true);
        if (package == null)
            return;
        var (folder, files) = package.Value;

        // Выкладывается ровно записанное: файл правили, добавили или удалили после записи — сначала записать заново.
        if (meta.AndroidRecords.TryGetValue(version, out var record) && record.Files.Count > 0)
        {
            var changes = AndroidPackage.Changes(record.Files, await Task.Run(() => AndroidPackage.Hashes(folder, files)));
            if (changes.Count > 0)
            {
                ShowProject();
                await _dialogs.ShowErrorAsync("Файлы Android изменились после того, как версия " + version + " была " +
                    (record.Signed ? "подписана" : "записана") + ":\n" + string.Join("\n", changes.Take(10)) +
                    (changes.Count > 10 ? "\n… и ещё " + (changes.Count - 10) : "") + "\n" +
                    (record.Signed ? "Подпишите файлы заново." : "Запишите версию заново."));
                return;
            }
        }

        var config = meta.Delivery;
        var channel = meta.Android.Channel;
        if (config.Method != DeliveryMethods.Ftp && string.IsNullOrWhiteSpace(config.Path))
        {
            await _dialogs.ShowErrorAsync("Не настроена доставка: строка «Доставка → Настроить…».");
            return;
        }

        FtpConnection? conn = null;
        if (config.Method == DeliveryMethods.Ftp)
        {
            conn = _ftpStore.Load(_project.RootPath);
            await WarnIfPasswordUndecryptable(conn);
            if (conn == null || !conn.IsComplete() || conn.PasswordDecryptFailed)
            {
                conn = await _dialogs.ConfigureFtpAsync(conn ?? new FtpConnection(), GetUploadDirectory());
                if (conn == null)
                    return; // отмена
                _ftpStore.Save(_project.RootPath, conn);
            }
        }

        var staging = AndroidPackage.Stage(folder, files);
        try
        {
            ResetAndroidFrom(version, StageKind.Delivery);
            if (conn == null)
            {
                var target = Path.Combine(config.Path, channel);
                try
                {
                    new FolderDeliveryTarget(target).Deliver(staging, Array.Empty<string>());
                }
                catch (Exception ex)
                {
                    ShowProject();
                    await _dialogs.ShowErrorAsync("Доставка Android не удалась:\n" + ex.Message);
                    return;
                }
                MarkAndroidDelivered(version);
                await _dialogs.ShowInfoAsync("Android-выпуск " + version + " выложен в:\n" + target);
            }
            else
            {
                var remote = DownloadUrl.CombineRemote(conn.RemotePath, GetUploadDirectory()).TrimEnd('/') + "/" + channel;
                var op = new FtpUploadOperation(conn, staging, remote, null, "Выкладка Android на FTP");
                await _dialogs.ShowOperationAsync(op);
                if (!op.Succeeded)
                {
                    ShowProject();
                    return;
                }
                MarkAndroidDelivered(version);
                await _dialogs.ShowInfoAsync("Android-выпуск " + version + " залит на FTP:\n" + remote);
            }
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); } catch { /* временная копия */ }
        }
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    private async Task VerifyAndroid()
    {
        if (_project == null)
            return;

        var meta = _project.Meta;
        var version = ReleaseCatalog.LatestAndroidVersion(meta);
        if (version == null)
        {
            await _dialogs.ShowErrorAsync(meta.Android.Sign
                ? "Android ещё не выпускался — сначала подпишите и доставьте файлы."
                : "Android ещё не выпускался — сначала запишите версию и доставьте файлы.");
            return;
        }

        var baseUrl = (_service.LoadSettings(_project.RootPath).BaseDownloadURL ?? "").Trim();
        if (baseUrl.Length == 0)
        {
            await _dialogs.ShowErrorAsync("Не задан адрес сервера (BaseDownloadURL). Укажите его в настройках проекта.");
            return;
        }

        var package = await PrepareAndroidPackage(version, checkSignatures: false);
        if (package == null)
            return;
        var (folder, files) = package.Value;

        var signed = AndroidPackage.SignedFiles(files, AndroidMasks(version));
        var publicKey = signed.Count > 0 && _keys.HasPublicKey(_project.RootPath)
            ? File.ReadAllText(_keys.PublicKeyPath(_project.RootPath)) : "";
        if (signed.Count > 0 && publicKey.Length == 0)
        {
            await _dialogs.ShowErrorAsync("Нет публичного ключа проекта (" + RsaKeyManager.PublicKeyFileName + ") — подпись нечем сверить.");
            return;
        }

        var channelUrl = baseUrl.TrimEnd('/') + "/" + meta.Android.Channel.Trim('/') + "/";
        var verifier = new AndroidChannelVerifier(channelUrl, folder, files, signed, publicKey);
        await _dialogs.ShowOperationAsync(verifier);

        var details = verifier.Succeeded ? "" : verifier.ResultDetails;
        if (!verifier.Succeeded && string.IsNullOrWhiteSpace(details))
            details = "проверка прервана";
        meta.AndroidVerifiedVersions[version] = new VerificationResult(DateTime.Now, verifier.Succeeded, details);
        meta.Save(_project.RootPath);
        ShowProject();
    }

    // Откуда выкладывается версия и её файлы: сохранённая при записи копия (Versions/Android/<версия>), у записей
    // до 2.2 без копии — папка билда. checkSignatures — у подписываемых файлов .sig на месте и сходится
    // (файл не правили после подписи). null — ошибка уже показана.
    private async Task<(string Folder, List<string> Files)?> PrepareAndroidPackage(string version, bool checkSignatures)
    {
        var meta = _project!.Meta;
        var stored = ProjectService.AndroidVersionPath(_project.RootPath, version);
        var folder = Directory.Exists(stored) ? stored : meta.Android.BuildSource;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            await _dialogs.ShowErrorAsync("Выкладка Android " + version + " не сохранена в проекте, а папки билда нет.\n" +
                "Выберите папку билда и запишите версию заново.");
            return null;
        }

        var masks = AndroidMasks(version);
        var files = AndroidPackage.Files(folder, masks, meta.Android.ExcludeMasks);
        if (files.Count == 0)
        {
            await _dialogs.ShowErrorAsync("В папке билда Android нечего выкладывать: " + folder);
            return null;
        }

        if (checkSignatures)
        {
            var signed = AndroidPackage.SignedFiles(files, masks);
            if (signed.Count > 0)
            {
                var publicKey = File.ReadAllText(_keys.PublicKeyPath(_project.RootPath));
                foreach (var file in signed)
                {
                    var path = Path.Combine(folder, file);
                    var sig = path + FileSigner.SignatureExtension;
                    if (!File.Exists(sig) || !FileSigner.Verify(File.ReadAllBytes(path), File.ReadAllText(sig), publicKey))
                    {
                        ShowProject(); // билд изменился — каталог покажет его как новый
                        await _dialogs.ShowErrorAsync("Файл " + file + " изменился после подписи (или нет его .sig).\n" +
                            "Подпишите файлы заново — иначе клиент не примет обновление.");
                        return null;
                    }
                }
            }
        }
        return (folder, files);
    }

    // Маски подписи версии — какими её записали (настройки могли поменяться после); у записей 2.1.0 — текущие.
    private List<string> AndroidMasks(string version) =>
        _project!.Meta.AndroidRecords.TryGetValue(version, out var record) ? record.Masks : _project.Meta.Android.ActiveSignMasks;

    // Подсказка версии при подписи: выбранный выпуск, иначе самый новый с известной версией.
    private string SuggestAndroidVersion()
    {
        if (SelectedRelease != null && SelectedRelease.Version != ReleaseItem.UnknownVersion)
            return SelectedRelease.Version;
        return Releases.FirstOrDefault(r => r.Version != ReleaseItem.UnknownVersion)?.Version ?? "";
    }

    // Этап Android выполняется (заново) — его запись и записи следующих этапов этой версии больше не верны.
    private void ResetAndroidFrom(string version, StageKind from)
    {
        var meta = _project!.Meta;
        if (from <= StageKind.Files)
        {
            meta.AndroidSignedVersions.Remove(version);
            meta.AndroidRecords.Remove(version);
        }
        if (from <= StageKind.Delivery)
            meta.AndroidDeliveredVersions.Remove(version);
        meta.AndroidVerifiedVersions.Remove(version);
        meta.Save(_project.RootPath);
    }

    private void MarkAndroidDelivered(string version)
    {
        _project!.Meta.AndroidDeliveredVersions[version] = DateTime.Now;
        _project.Meta.Save(_project.RootPath);
        ShowProject();
    }

    // Сведения Android для строки в главном окне (вызывается из ShowProject).
    private void ShowAndroid(ProjectMeta meta)
    {
        IsWindowsEnabled = meta.WindowsEnabled;
        IsAndroidEnabled = meta.AndroidEnabled;
        IsAndroidSigned = meta.Android.Sign;
        var folder = meta.Android.BuildSource ?? "";
        AndroidBuildSource = folder.Length > 0 ? folder : "не выбрана";
    }
}
