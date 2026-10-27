# UpdateManager 2.0 — Avalonia / .NET 8 — дизайн

Статус: дизайн согласован 2026-10-27. Следующий шаг — план реализации (writing-plans).

## Цель

2.0 — новый продукт (отдельная установка, не апгрейд 1.x): паритет функций с 1.x на новой
платформе, новый удобный дизайн, юнит-тесты и самообновление с первого релиза.

Успех: 2.0 умеет всё, что 1.x (проекты, сборка, доставка папка/FTP, «Проверить», RSA,
пресет «узнать себя»), выглядит нормально в обеих темах и обновляет сама себя.

## Решения

| Вопрос | Решение |
|---|---|
| Платформа | .NET 8, `win-x64`, self-contained, **без** single-file (движок подменяет файлы по одному) |
| UI | Avalonia 11 + Fluent, MVVM на CommunityToolkit.Mvvm |
| Репозиторий | 2.0 на `main`; 1.x — ветка `v1` + тег `v1.0.3` от `bff6795` (по команде пользователя) |
| Самообновление в 1.x | Нет и не будет. Незакоммиченная реализация самообновления (WinForms) — исходник для переноса в 2.0 |
| Сервер | `http://updates.borealis-soft-home/UpdateManager/VersionInfo.info` (каналы не делим — у 1.x обновлений нет) |
| Объём | Паритет + xUnit-тесты + новый дизайн отдельным этапом через дизайн-скилл |
| Движок | SimplePatchTool остаётся (DLL под CLR 2.0, `lib/`, HintPath) |

## Структура

```
UpdateManager.sln
src/UpdateManager.Core/    net8.0 classlib: Project, Delivery, Operations, Versioning,
                           Security, SelfUpdate, Common — перенос из 1.x
src/UpdateManager.App/     net8.0-windows, Avalonia: Views/ (axaml), ViewModels/, Services/
tests/UpdateManager.Tests/ xUnit: Core + ViewModel'и
lib/                       DLL движка, FluentFTP (или FluentFTP из NuGet — решить в плане)
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

1. Спайк: движок + Security + SelfPatcher под .NET 8, включая self-patch через `file:///` в temp.
2. git: ветка `v1` + тег `v1.0.3` от `bff6795` (по команде пользователя).
3. Каркас солюшена, перенос Core, тесты Core.
4. ViewModel'и + сервисы + функциональные экраны.
5. Дизайн-этап → финальная вёрстка.
6. Публикация, версия 2.0.0, первая доставка.
