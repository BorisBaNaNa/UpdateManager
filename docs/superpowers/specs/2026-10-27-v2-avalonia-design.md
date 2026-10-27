# UpdateManager 2.0 — Avalonia / .NET 10 — дизайн

Статус: дизайн согласован 2026-10-27. Реализация: шаги 1, 3, 4 выполнены (см. «Порядок»).

## Цель

2.0 — новый продукт (отдельная установка, не апгрейд 1.x): паритет функций с 1.x на новой
платформе, новый удобный дизайн, юнит-тесты и самообновление с первого релиза.

Успех: 2.0 умеет всё, что 1.x (проекты, сборка, доставка папка/FTP, «Проверить», RSA,
пресет «узнать себя»), выглядит нормально в обеих темах и обновляет сама себя.

## Решения

| Вопрос | Решение |
|---|---|
| Платформа | .NET 10 (`net10.0-windows`), `win-x64`, self-contained, **без** single-file (движок подменяет файлы по одному). Было .NET 8 — заменено: поддержка .NET 8 заканчивается 10.11.2026, .NET 10 — LTS до 11.2028 |
| UI | Avalonia 12.1.2 + Fluent, MVVM на CommunityToolkit.Mvvm 8.4.1 (версии — как в соседних проектах пользователя) |
| Репозиторий | 2.0 на `main`; 1.x — ветка `v1` + тег `v1.0.3` от `bff6795` (по команде пользователя) |
| Самообновление в 1.x | Нет и не будет. Незакоммиченная реализация самообновления (WinForms) — исходник для переноса в 2.0 |
| Сервер | `http://updates.borealis-soft-home/UpdateManager/VersionInfo.info` (каналы не делим — у 1.x обновлений нет) |
| Объём | Паритет + xUnit-тесты + новый дизайн отдельным этапом через дизайн-скилл |
| Движок | SimplePatchTool остаётся (DLL под CLR 2.0, `lib/`, HintPath) |

## Структура

```
UpdateManager.sln
src/UpdateManager.Core/    net10.0-windows classlib: Project, Delivery, Operations, Versioning,
                           Security, SelfUpdate, Common — перенос из 1.x
src/UpdateManager.App/     net10.0-windows, Avalonia: Views/ (axaml), ViewModels/, Services/;
                           сборка UpdateManager.exe, publish — Properties/PublishProfiles/win-x64.pubxml
tests/UpdateManager.Tests/ xUnit v3: Core + ViewModel'и + сквозной прогон движка
lib/                       DLL движка (HintPath). FluentFTP — из NuGet 52.1.0 (lib/FluentFTP.dll — net472 для 1.x)
Directory.Build.props      версия 2.0.0, Product/Company (по ним SelfIdentity узнаёт себя)
```

## Слои / MVVM

- Core не знает про UI. Интерфейсы (`IProjectService`, `IFtpConnectionStore`, …) — только там,
  где VM нужен мок.
- VM: `MainWindowViewModel`, `ProjectSettingsViewModel`, `DeliveryViewModel`,
  `FtpConnectionViewModel`, `FtpBrowserViewModel`, `OperationViewModel` (прогресс + лог),
  `SelfUpdateViewModel`. Логика `MainPresenter`/`SelfUpdatePresenter` → команды
  (`AsyncRelayCommand` + `CanExecute`).
- `IDialogService`: `ConfirmAsync`, `ShowErrorAsync`, `ShowInfoAsync`, `PickFolderAsync`,
  `PickFileAsync`, `ShowOperationAsync`, `EditSettingsAsync`, … Реализация — Avalonia
  `StorageProvider` + своё окно сообщений; в тестах — фейк.
- Композиция — `Microsoft.Extensions.DependencyInjection`.

## Темы и дизайн

- Fluent, `RequestedThemeVariant` (Light/Dark/Default), режим — `%AppData%\UpdateManager\theme.txt`.
  Ручная темизация 1.x (`Forms/Theming/`) не переносится.
- Дизайн-этап: макеты через дизайн-скилл → согласование → финальный XAML. До него экраны —
  функциональная вёрстка.

## Совместимость с 1.x

- Проекты 1.x открываются как есть (`Settings.xml`, `updatemanager.project.xml`).
- `%AppData%\UpdateManager` от 1.x читается: недавние, `ftp.xml` (DPAPI через пакет
  `System.Security.Cryptography.ProtectedData`), тема.

## Поставка и самообновление

- `dotnet publish -r win-x64 --self-contained`: `UpdateManager.exe` (apphost) + DLL + рантайм + `SelfPatcher\`.
- Самообновление — перенос реализации 1.x-ветки разработки (`Core/SelfUpdate`, логика презентера):
  тихая проверка при старте + меню, `Run(true)` → `ApplySelfPatch` → выход, RSA при заданном ключе.
  Конфиг — `appsettings.json` (`SelfUpdate:VersionInfoUrl`, `SelfUpdate:PublicKey`) вместо App.config.
- Пресет «узнать себя» сохраняется; `IgnoredPaths` дополнить под .NET 8 (уточнить на спайке).

## Риски движка под .NET 8 (разведка 2026-10-27)

- DLL движка и SelfPatcher — CLR 2.0 (v2.0.50727); `Thread.Abort`/`BinaryFormatter` не найдены.
- `SimplePatchToolSecurity` использует `SignedXml` → пакет `System.Security.Cryptography.Xml`.
- Сеть — `WebClient`/`HttpWebRequest` (obsolete, но работают).
- SelfPatcher.exe — .NET Framework exe (WinForms), запускается на Win10/11 штатно.

## Тесты

- Core: `VersionRules`, `DownloadUrl`, `SelfUpdateConfig`, `SelfProjectPreset`, отпечаток настроек, `FileUtils`.
- VM с фейками: защиты чек-листа (нет `BaseDownloadURL`, понижение версии, ни одного типа патча,
  кириллица + installer, сборка «себя» без self-patching и т.д.).
- Интеграционный: temp-проект → сборка патча → «Проверить» через `file:///`.

## Порядок

1. ✅ Спайк: движок + Security под .NET 10 — `EnginePipelineTests` (создание → сборка 1.0.0 →
   RSA-подпись → «Проверить» через `file:///` → 1.0.1 инкрементальным патчем). Не проверено:
   self-patch (`Run(true)` → SelfPatcher.exe → перезапуск) — нужен ручной прогон на собранном exe.
2. git: ветка `v1` + тег `v1.0.3` от `bff6795` (по команде пользователя). Пока 1.x (`UpdateManager/`)
   лежит рядом в том же `UpdateManager.sln`; `dotnet build` всего .sln на нём падает (resx) — из VS собирается.
3. ✅ Каркас солюшена, перенос Core, тесты Core.
4. ✅ ViewModel'и + сервисы + функциональные экраны (раскладка как в 1.x). Отличия от 1.x в Core:
   `appsettings.json` вместо App.config; `SelfIdentity` — entry assembly и `Environment.ProcessPath`
   (в .NET `Assembly.Location` — это .dll); хранилища получили конструктор с путём (тесты);
   `RemotePathsOverlap` переехал из презентера в `DownloadUrl`.
5. Дизайн-этап → финальная вёрстка.
6. Публикация, версия 2.0.0, первая доставка.
