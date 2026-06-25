# Справочник по движку SimplePatchTool

Извлечено из эталонного редакторного окна Unity `PatcherEditorLegacy.cs` (проект Interactive Book).
DLL движка лежат в `lib/`:

- `SimplePatchTool.dll` — namespace `SimplePatchToolCore` (`PatchCreator`, `PatchUpdater`,
  `PatchUtils`, enum `CompressionFormat`, `PatchResult`).
- `SimplePatchToolSecurity.dll` — namespace `SimplePatchToolSecurity` (`XMLSigner`, `SecurityUtils`).

> Полный API см. в исходниках/wiki: https://github.com/yasirkula/SimplePatchTool/wiki

## 1. Создание патча — `PatchCreator`

```csharp
using SimplePatchToolCore;

var patchCreator = new PatchCreator( rootPath, outputPath, projectName, version );

patchCreator
    .CreateIncrementalPatch( prevRoot.Length > 0, prevRoot )   // инкрементальный (нужна папка пред. версии)
    .CreateRepairPatch( createRepair )                          // repair-патч (пофайловое восстановление)
    .CreateInstallerPatch( createInstaller )                    // installer-патч (полная установка)
    .SetCompressionFormat( repairComp, installerComp, incrementalComp )
    .SetPreviousPatchFilesRoot( prevOutputPath, skipUnchangedPatchFiles );

if( ignoredPaths.Length > 0 )
    patchCreator.AddIgnoredPaths( ignoredPaths );  // string[] — по одному пути на элемент

// Запуск (асинхронный, движок крутится в своём потоке):
if( patchCreator.Run() )
{
    // Тянуть лог в цикле:
    string log = patchCreator.FetchLog();   // null когда логов нет
    while( log != null ) { /* вывести log */ log = patchCreator.FetchLog(); }

    // Признак завершения и результат:
    bool running = patchCreator.IsRunning;
    PatchResult result = patchCreator.Result; // PatchResult.Failed / .Success
}
```

**Важно по `BaseDownloadURL`:** в legacy-окне он НЕ задавался прямо (задаётся отдельно при
обновлении ссылок, вкладка UPDATE, либо методом на манифесте). В новом инструменте это должно
быть явное поле. В заметках упоминается метод `SetBaseDownloadURL` — проверить наличие в этой
версии DLL (через рефлексию/декомпиляцию `lib/SimplePatchTool.dll`); если его нет — выставлять
ссылки через `PatchUpdater` (см. п.2).

### Параметры (из legacy-окна)

| Параметр | Назначение |
|---|---|
| `rootPath` | Папка актуальной (свежей) версии/билда |
| `prevRoot` | (опц.) Папка предыдущей версии → включает инкрементальный патч |
| `prevOutputPath` | (опц.) Папка с патч-файлами пред. версии → repair-файлы для неизменённых файлов копируются, а не пересчитываются |
| `outputPath` | Куда генерировать патч (должна быть пустой) |
| `projectName`, `version` | Имя/версия проекта |
| `createRepair`, `createInstaller` | Какие типы патчей создавать |
| `skipUnchangedPatchFiles` | Не генерировать патч-файлы для неизменённых файлов (экономия трафика; работает только если задан `prevOutputPath`) |
| `CompressionFormat` | LZMA (дефолт) и др. — отдельно для repair/incremental/installer |
| `ignoredPaths` | Игнор-список, по одному на строку; дефолт включал `*output_log.txt` |

## 2. Обновление ссылок на скачивание — `PatchUpdater`

```csharp
var patchUpdater = new PatchUpdater( versionInfoPath, log => Debug.Log(log) );

// Вариант А — из файла со ссылками:
bool ok = patchUpdater.UpdateDownloadLinks( downloadLinksFilePath );

// Вариант Б — из словаря (relativePath -> url):
var links = new Dictionary<string,string> { ... };
bool ok = patchUpdater.UpdateDownloadLinks( links );

if( ok ) patchUpdater.SaveChanges();
```

Формат строки ссылок: `<относительный_путь> <url>` (разделитель — последний пробел в строке).

## 3. Безопасность (RSA) — `SimplePatchToolSecurity`

```csharp
using SimplePatchToolSecurity;

// Генерация пары ключей:
SecurityUtils.CreateRSAKeyPair( out string publicKey, out string privateKey );
// сохраняются как public.key / private.key

// Подпись и проверка XML (манифеста):
XMLSigner.SignXMLFile( xmlPath, File.ReadAllText( privateKeyPath ) );
bool genuine = XMLSigner.VerifyXMLFile( xmlPath, File.ReadAllText( publicKeyPath ) );
```

## 4. Консольная команда (альтернатива API)

Legacy-окно умело генерировать готовую команду CLI-патчера:

```
Patcher create -root="<root>" -out="<out>" -name="<name>" -version="<ver>" \
  -compressionRepair="LZMA" -compressionIncremental="LZMA" -compressionInstaller="LZMA" \
  [-prevRoot="<prevRoot>"] [-prevPatchRoot="<prevOutput>"] \
  [-ignoredPaths="PATH/TO/ignoredPaths.txt"] \
  [-dontCreateRepairPatch] [-dontCreateInstallerPatch] [-skipUnchangedPatchFiles]
```

`ignoredPaths` в консольном режиме передаётся файлом, а не списком.

## 5. Раскладка на сервере (формат выдачи)

Сервер — расшаренная по HTTP папка. `GetURL()` донора собирает version-info URL как
`{RemoteUrl}/{ProjectName}/{VersionFolder}/VersionInfo.info`. В этой же папке лежат:

- `VersionInfo.info` — манифест (тянется по version-info URL).
- `RepairPatch/` — файлы repair-патча; лежат сжатыми с расширением `.lzdat`.
- `InstallerPatch/` — файлы installer-патча.
- Файлы патча качаются по `BaseDownloadURL` из манифеста (НЕ по version-info URL — отсюда
  класс ошибок с забытым `BaseDownloadURL`).
