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

**Версия 2.0** (Avalonia / .NET 10) — функциональный паритет с 1.x. Все окна в новом дизайне (мир
«каталог», см. `DESIGN.md`): главное окно и диалоги на общих стилях `App.axaml` (подвал `footer`,
подписи `field`, разделы `rule` + `heading`, провал `failure`, журнал `TextBox.log`). 1.x (WinForms, .NET Framework 4.8) живёт в ветке `v1` (тег `v1.0.3`), в `main` её нет. Спек 2.0
и статус по шагам: `docs/superpowers/specs/2026-10-27-v2-avalonia-design.md`.

**Главное окно.** Без проекта — стартовый экран: последний проект крупно (его полоса этапов, «Открыть»)
и список недавних. С проектом — слева каталог выпусков (версия, «№ N · состояние словами», мини-полоса),
справа выбранный выпуск: номер версии, блоки этапов Билд → Патч → Доставка → Проверка (залит — пройден,
контур — нет, красный контур — провал) и одна главная кнопка следующего шага; ниже сведения проекта
с действиями. Новый билд в папке-источнике (версия новее собранных) — отдельная запись «новый».
Модель выпуска (`ViewModels/ReleaseModels.cs`: `ReleaseCatalog`, `ReleaseItem`, `PlatformTrack`,
`StageItem`) держит несколько платформ в одном выпуске — задел под Android (без патчей: apk → файлы
версии/лицензии → доставка → проверка). Блоки этапов последнего выпуска — кнопки: пройденный этап можно
повторить (пересобрать ту же версию, доставить/проверить снова), следующий — выполнить, дальше следующего —
заперто; архивные выпуски только для просмотра (`StageItem.IsActionable`, `RunStageCommand`). Те же действия —
в меню «Выпуск». Блок «Билд» — вход извне: сам билд собирается вне UpdateManager (IDE, `release.ps1`),
блок показывает, откуда он взят, а нажатие меняет папку билда.

Реализовано (перенесено из 1.x):

- **Создать / открыть / недавние проекты** (как IDE). Проект = папка движка (`Settings.xml`,
  `Versions/ Output/ SelfPatcher/ Other/`) + наш `updatemanager.project.xml` (мета: главный exe,
  последний источник билда, конфиг доставки, по версиям — даты сборки патча `PatchedVersions`,
  доставки `DeliveredVersions` и итог проверки `VerifiedVersions`).
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
  Токены дизайна (фон, панель, краска, линии, цвета этапов) — `App.axaml` (ThemeDictionaries Light/Dark);
  шрифт — Golos Text (`Assets/Fonts`, OFL), задан по умолчанию в `Program.cs`.

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
  `Directory.Build.props` → publish в `Release/` (папка очищается) → копия `SelfPatcher\` в `Release/SPPatcher\`
  (туда self-patcher кладёт движок в патче; без неё свежий выпуск сразу «обновляется» ради него) → `Release/<Product>_vers.sptv` с версией
  X.Y.Z.0 (version holder движка: клиентский `CheckForUpdates(true)` и наш `VersionDetector`) → сверка FileVersion.
  Ничего не коммитит; при запущенном из репозитория UpdateManager останавливается с понятной ошибкой.
  Скрипт — Windows PowerShell 5.1, сохранять в UTF-8 **с BOM** (иначе кириллица ломается).

## Осталось

- Дизайн дальше — скилл Impeccable (`.claude/skills/impeccable`, `/impeccable …`); контекст — `PRODUCT.md`,
  `DESIGN.md`, бриф главного окна `.impeccable/surfaces/`. Скриншоты headless-обвязкой (Avalonia.Headless,
  `CaptureRenderedFrame`), в `.impeccable/review/` (не в git). Окна в живом приложении глазами не проверены.
- Сквозной прогон самообновления 2.0 (`Run(true)` → SelfPatcher.exe → перезапуск) — тестами не покрыт;
  сервер `https://updates.borealis-soft-home.duckdns.org:51943/` (только HTTPS; FTP — 192.168.0.100 в LAN).
  Роутер без NAT loopback: изнутри LAN публичный адрес недоступен (проверка/«Проверить» виснут до таймаута),
  а снаружи недоступен FTP 192.168.0.100 — HTTP-проверки делать из внешней сети.
  На клиенте движок держит self-patcher в `SPPatcher\` (наш `SelfPatcher\` — запасной путь `FindSelfPatcher`).
- FTPS/TLS для FTP-доставки (сейчас только обычный FTP) — при необходимости.

## Стек

- C# / .NET 10 (`net10.0-windows`), SDK-style проекты; UI — Avalonia 12.1.2 (Fluent), CommunityToolkit.Mvvm;
  шрифт Golos Text.
- Движок патчей: SimplePatchTool (`lib/`, CLR 2.0, HintPath). Подпись — `System.Security.Cryptography.Xml`.
- FTP: FluentFTP 52.1.0 (NuGet). Шифрование секретов: DPAPI (`System.Security.Cryptography.ProtectedData`).
- Тесты: xUnit v3.

## Документация

- [PRODUCT.md](../PRODUCT.md) — продукт для дизайн-скилла: пользователи, сценарий, принципы, планы (Android).
- [DESIGN.md](../DESIGN.md) — дизайн-система (мир «каталог»): токены, типографика, компоненты, диалоги.
- [docs/superpowers/specs/](../docs/superpowers/specs/) — спеки 2.0 (переезд на Avalonia) и самообновления.
- `lib/README` — откуда DLL движка. Справочника по API SimplePatchTool в репозитории нет.

## Правила работы (от пользователя)

- Общение на русском языке.
- **Коммитить самому, по темам, по ходу работы**: сделал → проверил (сборка + тесты) → коммит.
  Никогда не пушить и не предлагать push — пользователь пушит сам.
- Стиль кода: не выравнивать поля/присваивания пробелами; следовать стилю окружающего кода.
