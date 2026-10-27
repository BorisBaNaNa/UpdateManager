# CLAUDE.md

Инструкции Claude Code (claude.ai/code) для работы с репозиторием **UpdateManager**.

## Что это

**UpdateManager** — самостоятельное Windows-приложение (Avalonia, .NET 10) для
**сборки патчей и заливки их на сервер обновлений**. Инструмент **универсальный**: он не привязан
к конкретному приложению и должен уметь собирать патчи для любого проекта (указал папку билда →
собрал патч → залил на сервер).

Под капотом — движок **SimplePatchTool** (`lib/SimplePatchTool.dll`, `lib/SimplePatchToolSecurity.dll`):
`ProjectManager` (создание патча + настройки проекта), клиентский класс `SimplePatchTool` (проверка —
реальная закачка), `PatchCreator`/`PatchUpdater`, `VersionCode`. Цель — заменить ручную работу в
редакторном окне SimplePatchTool (которое жило внутри Unity-проекта) на отдельную программу.

## Зачем (мотивация)

- Убрать ручную работу в редакторном окне SimplePatchTool внутри Unity.
- Автоматизировать заливку патча на сервер (FTP).
- Закрыть класс ошибок с забытым/неверным `BaseDownloadURL`: при нём проверка обновлений
  проходит успешно, а **скачивание файлов патча падает** (`DownloadError: ... could not be
  downloaded`). Поэтому в инструменте нужна фича «Проверить» — HTTP-проба `VersionInfo.info`
  и файлов патча.

## Текущее состояние

**Версия 2.0** (Avalonia / .NET 10) — функциональный паритет с 1.x; экраны пока повторяют раскладку
1.x, новый дизайн — отдельным этапом. 1.x (WinForms, .NET Framework 4.8) живёт в ветке `v1`
(тег `v1.0.3`), в `main` её нет. Спек 2.0 и статус по шагам:
`docs/superpowers/specs/2026-10-27-v2-avalonia-design.md`.

Реализовано (перенесено из 1.x):

- **Создать / открыть / недавние проекты** (как IDE). Проект = папка движка (`Settings.xml`,
  `Versions/ Output/ SelfPatcher/ Other/`) + наш `updatemanager.project.xml` (мета: главный exe,
  последний источник билда, конфиг доставки, `LastDeliveredAt`).
- **Настройки проекта** — окно правки `Settings.xml` (Name, `BaseDownloadURL`, `MaintenanceCheckURL`,
  `IsSelfPatchingApp`, типы патчей, `IgnoredPaths`, `CreateAllIncrementalPatches`) + раздел
  «Дополнительно» (форматы сжатия Repair/Installer/Incremental, `BinaryDiffQuality`,
  `DontCreatePatchFilesForUnchangedFiles`); прочие продвинутые поля движка сохраняются как есть.
- **Создать патч** — папка билда → версия из `FileVersion` главного exe (валидация через `VersionCode`)
  → копия в `Versions/<версия>` → `ProjectManager.GeneratePatch()` (окно лога). Инкрементальные патчи
  движок делает сам из накопленных версий.
- **Доставить патч** — два метода: **папка** (стратегия `IDeliveryTarget`, копия `Output/`) и **FTP**
  (`FtpUploadOperation` на FluentFTP, Binary-режим, прогресс через `IEngineOperation`). Реквизиты FTP
  (host/port/user/path + пароль) хранятся в профиле пользователя (`%AppData%\UpdateManager\ftp.xml`),
  а НЕ в файле проекта; пароль шифруется через DPAPI (`SecretProtector`, `CurrentUser`). Отдельная
  кнопка «FTP-сервер…» правит реквизиты; при доставке без реквизитов окно открывается само.
- **Проверить** — клиентский `SimplePatchTool.Run()` реально качает патч во временную папку; ловит
  неверный/пустой `BaseDownloadURL`.
- Защиты `BaseDownloadURL`: блок сборки при пустом; авто-добавление `/` при сохранении (движок клеит
  ссылки без слеша). Встроенный self-patcher (`src/UpdateManager.App/SelfPatcher/`, Content → bin)
  кладётся в каждый новый проект.
- **RSA-подпись** (`Core/Security/`) — «Безопасность → Сгенерировать RSA-ключи…»; `public.key`/`private.key`
  в корне проекта (не в `Output/`); при `SignPatch` манифесты подписываются после сборки (`PatchSigner`);
  приватный ключ можно залить коллегам в отдельную FTP-папку (`FtpKeyUploader`).
- **Самообновление** (`Core/SelfUpdate/`, `SelfUpdateViewModel`) — тихая проверка при старте +
  «Справка → Проверить обновления…»; `Run(true)` в кэш → `ApplySelfPatch` → выход. URL и RSA-ключ —
  `appsettings.json` (секция `SelfUpdate`). Билд самого UpdateManager распознаётся по
  ProductName/CompanyName (`SelfIdentity`) → предложение пресета (`IsSelfPatchingApp`, `BaseDownloadURL`,
  `IgnoredPaths`). Спек: `docs/superpowers/specs/2026-10-27-self-update-design.md`.
- **Темы** (`Services/ThemeService`) — светлая/тёмная/«как в Windows»: Fluent + `RequestedThemeVariant`.
  Режим в `%AppData%\UpdateManager\theme.txt` — тот же файл, что у 1.x (недавние и `ftp.xml` тоже совместимы).

Git инициализирован, история ведётся по шагам.

## Архитектура

MVVM + доменный слой, домен не знает про UI:

- `src/UpdateManager.Core` (`net10.0-windows`) — подпапки со строгими namespace'ами `UpdateManager.Core.*`:
  `Project/`, `Delivery/`, `Operations/` (`IEngineOperation`, `PatchBuilder`, `PatchVerifier`),
  `Versioning/`, `Common/`, `Security/`, `SelfUpdate/`. Код перенесён из 1.x почти без изменений
  (блочные namespace, без nullable) — правя его, держать этот стиль.
- `src/UpdateManager.App` (сборка `UpdateManager.exe`) — Avalonia 12 + CommunityToolkit.Mvvm:
  `ViewModels/` (`MainWindowViewModel` — логика главного окна, `SelfUpdateViewModel`, VM диалогов на базе
  `DialogViewModel`), `Views/` (окна на базе `DialogWindow`), `Services/` (`IDialogService` — все диалоги
  для VM, `ThemeService`). Композиция — `App.axaml.cs` (Microsoft.Extensions.DependencyInjection).
- `tests/UpdateManager.Tests` — xUnit v3: Core, VM с `FakeDialogService` и настоящим Core,
  `EnginePipelineTests` — сквозной прогон движка (сборка → подпись → «Проверить» через `file:///`).
- Настройки проекта движка правит только `Core/Project/ProjectService`; операции (`PatchBuilder`,
  `PatchVerifier`, `SelfUpdate*`) создают движковые объекты сами.
- Версия/Product/Company — `Directory.Build.props` (по Product/Company `SelfIdentity` узнаёт себя).
- Сборка: `dotnet build UpdateManager.sln`, `dotnet test tests/UpdateManager.Tests`; выкатка —
  `dotnet publish src/UpdateManager.App -p:PublishProfile=win-x64` → `Release/`
  (self-contained, без single-file — движок подменяет файлы по одному).
- Выпуск: `scripts/release.ps1 patch|minor|major|-To X.Y.Z|-NoBump` — тесты → версия в
  `Directory.Build.props` → publish в `Release/` (папка очищается) → сверка FileVersion. Ничего не коммитит.
  Скрипт — Windows PowerShell 5.1, сохранять в UTF-8 **с BOM** (иначе кириллица ломается).

## Осталось

- Новый дизайн экранов (через дизайн-скилл).
- Сквозной прогон самообновления 2.0 (`Run(true)` → SelfPatcher.exe → перезапуск) — тестами не покрыт;
  сервер `updates.borealis-soft-home` пока недоступен; заодно уточнить, где движок держит self-patcher
  на клиенте (`SPPatcher\` или наш `SelfPatcher\`).
- FTPS/TLS для FTP-доставки (сейчас только обычный FTP) — при необходимости.

## Стек

- C# / .NET 10 (`net10.0-windows`), SDK-style проекты; UI — Avalonia 12.1.2 (Fluent), CommunityToolkit.Mvvm.
- Движок патчей: SimplePatchTool (`lib/`, CLR 2.0, HintPath). Подпись — `System.Security.Cryptography.Xml`.
- FTP: FluentFTP 52.1.0 (NuGet). Шифрование секретов: DPAPI (`System.Security.Cryptography.ProtectedData`).
- Тесты: xUnit v3.

## Документация

- [docs/idea.md](docs/idea.md) — полная идея, мотивация, открытые вопросы.
- [docs/engine-reference.md](docs/engine-reference.md) — API SimplePatchTool: `PatchCreator`,
  `PatchUpdater`, security, формат сервера, консольные команды.
- [docs/plan.md](docs/plan.md) — предлагаемый план реализации по шагам (исторический, для 1.x).
- [docs/source-project-context.md](docs/source-project-context.md) — как патчи устроены в
  проекте-доноре Interactive Book (пример интеграции: `ConnectionSettings`, build-хук, layout сервера).

## Правила работы (от пользователя)

- Общение на русском языке.
- **Не делать git-коммиты без явной просьбы.** Никогда не пушить и не предлагать push —
  пользователь пушит сам.
- Стиль кода: не выравнивать поля/присваивания пробелами; следовать стилю окружающего кода.
