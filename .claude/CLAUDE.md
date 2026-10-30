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

**Версия 2.3.0** (Avalonia / .NET 10) — выпущена 2026-10-30, тег `v2.3.0`, GitHub Release с архивом сборки.
В 2.3 добавлены определение версии отдельного Android-проекта из JSON-манифестов и повторное сканирование
сборок кнопкой справа от «ВЫПУСКИ». Предыдущие выпуски: 2.1 — платформа Android, «Что нового», зеркальная
доставка; 2.2 — версии по платформам, удаление и откат версий, Android без подписи и в привязке к Windows.
Сохранён функциональный паритет с 1.x. Самообновление проверено сквозь: 2.0 → 2.1 своим же патчем. Все окна в новом дизайне (мир
«каталог», см. `DESIGN.md`): главное окно и диалоги на общих стилях `App.axaml` (подвал `footer`,
подписи `field`, разделы `rule` + `heading`, провал `failure`, журнал `TextBox.log`). 1.x (WinForms, .NET Framework 4.8) живёт в ветке `v1` (тег `v1.0.3`), в `main` её нет. Спек 2.0
и статус по шагам: `docs/superpowers/specs/2026-10-27-v2-avalonia-design.md`.

**Главное окно.** Без проекта — стартовый экран: последний проект крупно (его полоса этапов, «Открыть»)
и список недавних. С проектом — слева каталог выпусков (версия, «№ N · состояние словами», мини-полоса),
справа выбранный выпуск: номер версии, блоки этапов Билд → Патч → Доставка → Проверка (залит — пройден,
контур — нет, красный контур — провал) и одна главная кнопка следующего шага; ниже сведения проекта
с действиями. Новый билд в папке-источнике (версия новее собранных) — отдельная запись «новый».
Модель выпуска (`ViewModels/ReleaseModels.cs`: `ReleaseCatalog`, `ReleaseItem`, `PlatformTrack`,
`StageItem`) держит несколько платформ в одном выпуске; у каждой платформы свой текущий выпуск —
`PlatformTrack.IsCurrent`. На экране проекта под версией одна полоса — выбранной платформы (`SelectedTrack`,
`Carousel TrackCarousel`); имена платформ у версии — переключатель, вторая полоса въезжает справа свайпом
(перетаскивание или прокрутка тачпада, код окна `MainWindow.axaml.cs` → `ShiftTrack`); главная кнопка — шаг
видимой полосы; выбор платформы запоминается между выпусками. В каталоге и на стартовом экране полосы друг под другом. Блоки этапов текущего выпуска — кнопки: пройденный этап можно
повторить (пересобрать ту же версию, доставить/проверить снова), следующий — выполнить, дальше следующего —
заперто; архивные выпуски только для просмотра (`StageItem.IsActionable`, `RunStageCommand`). Повтор этапа
стирает записи его и следующих этапов версии в момент старта (`ResetStagesFrom`): пересобранный патч — не
доставлен и не проверен, доставленный заново — не проверен; каталог к тому же не засчитывает этап, сделанный
раньше предыдущего («устарела: …»). Те же действия —
в меню «Выпуск». Блок «Билд» — вход извне: сам билд собирается вне UpdateManager (IDE, `release.ps1`),
блок показывает, откуда он взят, а нажатие меняет папку билда.

Реализовано (перенесено из 1.x):

- **Убрать / удалить проект** — только контекстное меню (правый клик) карточки недавнего проекта: «Убрать из списка»
  (только из показа, открытый закрывается) и «Удалить проект…» (подтверждение с предупреждением про `private.key`,
  папка целиком в Корзину — `Core/Common/RecycleBin`, SHFileOperation; удаляется только папка с `Settings.xml`;
  в тестах `ProjectService.DeleteFolder` подменяется). Недавних — 3 последних (`RecentProjectsStore.MaxItems`).
- **Удалить версию / откатить сервер** (2.2, `MainWindowViewModel.Catalog.cs`) — контекстное меню каталога
  (`CatalogList.ContextMenu`, действует на `SelectedRelease`: правый клик выбирает запись). Несколько — выделение
  областью (код окна): удержание записи 1 с (сдвиг > 8 px раньше — не удержание) задаёт точку начала, протяжка
  выделяет диапазон от неё до записи под указателем (`MarkTo`; назад — сжимается), у краёв список прокручивается
  (`_edgeTimer`); по отпусканию — отдельное меню `MarkedMenu` («Удалить выделенные (N)…»), закрыли мимо —
  `EndMarking` снимает выделение (через `Post`, чтобы команда пункта успела забрать его). Флажков нет — выделенное
  залито `MarkedBrush`. Удалять можно только
  `ReleaseItem.CanDelete` (не «новый», ни одна полоса не текущая). `ProjectService.DeleteVersions`: папки в Корзину,
  `ProjectMeta.ForgetVersion`; Output/ и сервер не трогает. Номера выпусков стабильны: удалённые/откатанные — в
  `RemovedVersions` (любая платформа — только нумерация каталога). Повтор номера запрещают `RemovedWindowsVersions` /
  `RemovedAndroidVersions` — и только на платформе, которая этот номер действительно выпускала (`ForgetVersion(v,
  windowsBuilt)`): иначе удаление Android-выпуска 0.9.8.0 запрещало Windows собрать свой 0.9.8.0 (так и сломалось).
  Клиент с той же версией не обновился бы; такие номера не дают и записи «новый». Откат (`CanRollback` —
  архивная полоса с `PlatformTrack.CanRestore`: Windows всегда, Android — если есть `Versions/Android/<версия>`), по
  каждой такой платформе. Windows — `ProjectService.RollbackTo`: версии новее в Корзину (`ForgetWindowsVersion`), из
  `Output/IncrementalPatch` и из `VersionInfo.info` убираются инкрементальные патчи к ним/от них (движок переносит
  список из прежнего VersionInfo), у цели сбрасываются патч/доставка/проверка → `BuildStagedPatch` пересобирает её
  последней. Android — `RollbackAndroidTo`: выкладки новее в Корзину (`ForgetAndroidVersion`), у цели сбрасываются
  доставка/проверка. Дальше — доставка и проверка как обычно. Движок понижает версию у клиентов (сравнивает на
  неравенство) — `EnginePipelineTests.RollbackTo_…`.
- **Создать / открыть / недавние проекты** (как IDE). Проект = папка движка (`Settings.xml`,
  `Versions/ Output/ SelfPatcher/ Other/`) + наш `updatemanager.project.xml` (мета: главный exe,
  последний источник билда, конфиг доставки, по версиям — даты сборки патча `PatchedVersions`,
  доставки `DeliveredVersions` и итог проверки `VerifiedVersions`).
- **Настройки проекта** — окно правки `Settings.xml` (Name, `BaseDownloadURL`, `MaintenanceCheckURL`,
  `IsSelfPatchingApp`, типы патчей, `IgnoredPaths`, `CreateAllIncrementalPatches`) + раздел
  «Дополнительно» (форматы сжатия Repair/Installer/Incremental, `BinaryDiffQuality`,
  `DontCreatePatchFilesForUnchangedFiles`); прочие продвинутые поля движка сохраняются как есть.
- **Создать патч** — папка билда → версия из `FileVersion` главного exe (валидация через `VersionCode`)
  → копия в `Versions/Windows/<версия>` → `GeneratePatch()` (окно лога). Инкрементальные патчи
  движок делает сам из накопленных версий.
- **Версии по платформам** (2.2, схема меты 2): `Versions/Windows/<версия>` и `Versions/Android/<версия>`
  (`ProjectService.Windows|AndroidVersionPath`). Движок считает версиями ВСЕ подпапки `Versions/` и падает на
  нечисловых («Failed to compare two elements») — поэтому он смотрит в `Versions/Windows`: `ProjectService.CreateEngine`
  переназначает readonly-поле `ProjectManager.versionsPath` через reflection (DLL движка неизменна); `PatchBuilder`
  создаёт движок только так. Android: запись версии копирует выкладку в `Versions/Android/<версия>`, доставка и
  проверка берут её оттуда (нет копии — запись до 2.2 — папка билда). Проекты прежней раскладки донастраивает
  `ProjectUpgrade.Run` при открытии (`OpenPath`, идемпотентна; итог — одно сообщение).
- **Доставить патч** — два метода: **папка** (стратегия `IDeliveryTarget`, копия `Output/`) и **FTP**
  (`FtpUploadOperation` на FluentFTP, Binary-режим, прогресс через `IEngineOperation`). Реквизиты FTP
  (host/port/user/path + пароль) хранятся в профиле пользователя (`%AppData%\UpdateManager\ftp.xml`),
  а НЕ в файле проекта; пароль шифруется через DPAPI (`SecretProtector`, `CurrentUser`). Отдельная
  кнопка «FTP-сервер…» правит реквизиты; при доставке без реквизитов окно открывается само.
- **Проверить** — клиентский `SimplePatchTool.Run()` реально качает патч во временную папку; ловит
  неверный/пустой `BaseDownloadURL`.
- Защиты `BaseDownloadURL`: блок сборки при пустом; авто-добавление `/` при сохранении (движок клеит
  ссылки без слеша). Встроенный self-patcher (`src/UpdateManager.App/SPPatcher/`, Content → `SPPatcher\` рядом
  с exe — туда же его кладёт движок из self-patching-патча) кладётся в каждый новый проект (в его `SelfPatcher/`)
  и запускается при самообновлении. Другой копии рядом с exe нет.
- **RSA-подпись** (`Core/Security/`) — «Безопасность → Сгенерировать RSA-ключи…»; `public.key`/`private.key`
  в корне проекта (не в `Output/`); при `SignPatch` манифесты подписываются после сборки (`PatchSigner`);
  приватный ключ можно залить коллегам в отдельную FTP-папку (`FtpKeyUploader`).
- **Самообновление** (`Core/SelfUpdate/`, `SelfUpdateViewModel`) — тихая проверка при старте +
  «Справка → Проверить обновления…»; `Run(true)` в кэш → `ApplySelfPatch` → выход. URL и RSA-ключ —
  `appsettings.json` (секция `SelfUpdate`). Билд самого UpdateManager распознаётся по
  ProductName/CompanyName (`SelfIdentity`) → предложение пресета (`IsSelfPatchingApp`, `BaseDownloadURL`,
  `IgnoredPaths`). Спек: `docs/superpowers/specs/2026-10-27-self-update-design.md`.
- **Платформа Android** (2.1, спек `docs/superpowers/specs/2026-10-28-android-platform-design.md`) — платформы
  включаются в настройках проекта (мета `<Platforms>`, `AndroidConfig`: папка билда, канал `AndroidPatch`, маски
  подписи и «не выкладывать»). Без патчей: Билд → Подпись (`StageKind.Files`) → Доставка → Проверка;
  `MainWindowViewModel.Android.cs`. Подпись — `Core/Security/FileSigner` (RSA SHA-256 PKCS#1 v1.5 по сырым байтам,
  `<файл>.sig` base64 без BOM, самопроверка, маска без совпадений — ошибка; ключ — тот же `private.key` проекта).
  Подпись выключаемая, как `SignPatch` у Windows (`AndroidConfig.Sign`, атрибут `sign`; в мете 2.1.0 его нет —
  берётся «маски не пусты»): выключена — этап зовётся «Версия» и только записывает версию, ключ не нужен, маски
  не действуют (`ActiveSignMasks` пуст — ни подписи, ни сверки, ни одного `.sig` в канале); включена без масок — ошибка.
  Этап записывает `AndroidRecord` версии (мета `AndroidRecords`): подписана ли, маски и SHA-256 файлов выкладки
  (`AndroidPackage.Hashes`/`Changes`). Каталог показывает этап по записи, а не по текущим настройкам; доставка и
  проверка берут маски записи; файл изменён/добавлен/удалён после записи — выкладка отказывает (у записей 2.1.0
  записи нет — прежнее поведение);
  маски — `Core/Common/FileMask` (glob). Что выкладывается — `Core/Delivery/AndroidPackage` (без исключённых и
  чужих `.sig`); канал = папка патча + `AndroidPatch`, публичный адрес = `BaseDownloadURL` + канал; проверка —
  `Core/Operations/AndroidChannelVerifier` (скачать, сверить байты и подписи). Для отдельного Android-проекта
  `Core/Versioning/AndroidVersionDetector` ищет JSON в папке билда и подпапках, читает строку `versionName`
  (не `versionCode`); нет допустимой версии или есть разные — ручной ввод. Кнопка справа от «ВЫПУСКИ»
  (`RescanBuildsCommand`) перечитывает сборки обеих платформ без изменения записанных этапов.
  **Привязка к Windows** (решение пользователя: разные версии — это разные проекты): включены обе платформы
  (`IsAndroidBoundToWindows`) — Android пишется под версией Windows без вопроса (`BoundAndroidVersion`: новый
  Windows-билд `_newWindowsVersion`, иначе последняя собранная; Windows-версии нет — ошибка), его полоса — только в
  последнем выпуске Windows, отдельной записи «новый» нет. Только Android — версия из JSON либо ручной ввод;
  новый номер из манифеста даёт запись «новый» независимо от дат файлов. Без версии в JSON сохраняется обнаружение
  нового билда по датам. Записи этапов — `AndroidSigned/Delivered/VerifiedVersions`.
- **Доставка зеркальная** (с 2.1): после успешной отправки в папке назначения остаётся ровно новая выкладка
  (FTP — атомарная подмена `_new`/`_old`, папка — удаление лишнего); сохраняется только папка канала Android
  внутри папки патча (`keepNames`). Папка: отказ на корне диска и пересечении с источником.
- **Что нового** — заметки выпусков `docs/releases/X.Y.Z.md` (тот же текст, что в GitHub Release) вшиты
  в exe (EmbeddedResource `ReleaseNotes.X.Y.Z.md`, `Services/ReleaseNotesService`). Окно `ReleaseNotesWindow`
  (свой мини-рендер Markdown `Views/MarkdownView`: `##`, `###`, `- `, абзацы, `**жирный**`, `` `код` ``)
  открывается само один раз после обновления — версии новее последней показанной
  (`%AppData%\UpdateManager\whatsnew.txt`; нет файла — всё до текущей), затем идёт тихая проверка обновлений;
  под отладчиком не показывается. И всегда — «Справка → Что нового…».
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
  `EnginePipelineTests` — сквозной прогон движка (сборка → подпись → «Проверить» через `file:///`; откат с понижением
  версии у клиентов). Поведение привязок и жестов — настоящий `MainWindow` без экрана: `HeadlessUi.Run`
  (Avalonia.Headless, одна сессия на процесс; `Click`, `CenterOf`, `Wait`, `window.MouseDown/Move/Up`).
  Запущенный из репозитория UpdateManager держит `bin\Debug` — тогда тесты с `--artifacts-path <scratch>`.
- Настройки проекта движка правит только `Core/Project/ProjectService`; операции (`PatchBuilder`,
  `PatchVerifier`, `SelfUpdate*`) создают движковые объекты сами.
- Версия/Product/Company — `Directory.Build.props` (по Product/Company `SelfIdentity` узнаёт себя).
- Сборка: `dotnet build UpdateManager.sln`, `dotnet test tests/UpdateManager.Tests`; выкатка —
  `dotnet publish src/UpdateManager.App -p:PublishProfile=win-x64` → `Release/`
  (self-contained, без single-file — движок подменяет файлы по одному).
- Выпуск: `scripts/release.ps1 patch|minor|major|-To X.Y.Z|-NoBump` — заметки `docs/releases/X.Y.Z.md`
  обязательны (пишутся до сборки, иначе скрипт останавливается) → тесты → версия в
  `Directory.Build.props` → publish в `Release/` (папка очищается) → `Release/<Product>_vers.sptv` с версией
  X.Y.Z.0 (version holder движка: клиентский `CheckForUpdates(true)` и наш `VersionDetector`) → сверка FileVersion
  и наличия `Release/SPPatcher/SelfPatcher.exe`.
  Ничего не коммитит; при запущенном из репозитория UpdateManager останавливается с понятной ошибкой.
- **Каждый выпуск сопровождается тегом git и GitHub Release.** Порядок: Claude собирает выпуск →
  пользователь проверяет и ставит патч → по его слову Claude делает коммит подъёма версии и аннотированный тег
  `vX.Y.Z` на коммите, из которого собран `Release/` (`git tag -a vX.Y.Z -m "UpdateManager X.Y.Z"`) →
  пользователь пушит `main` и тег → Claude запускает `scripts/release.ps1 -Publish`.
  `-Publish` ничего не собирает: публикует через `gh` (`C:\Program Files\GitHub CLI\gh.exe`, вход выполнен)
  выпуск текущей версии — заголовок «UpdateManager vX.Y.Z.0», текст `docs/releases/X.Y.Z.md` (по темам, как у
  `v1.0.3`), архив `Release/` как `UpdateManager.vX.Y.Z.zip`. Сверяет FileVersion в `Release/`, тег локально и на
  origin, отсутствие выпуска; архив собирается в `%TEMP%` (пути в zip через `/` — `Compress-Archive` в PS 5.1
  пишет `\`) и удаляется после отправки, удачной или нет.
  Схема версий — SemVer из трёх чисел: новая возможность → minor, иначе patch.
  Скрипт — Windows PowerShell 5.1, сохранять в UTF-8 **с BOM** (иначе кириллица ломается).

## Осталось

- Дизайн дальше — скилл Impeccable (`.claude/skills/impeccable`, `/impeccable …`); контекст — `PRODUCT.md`,
  `DESIGN.md`, бриф главного окна `.impeccable/surfaces/`. Скриншоты headless-обвязкой (Avalonia.Headless,
  `CaptureRenderedFrame`), в `.impeccable/review/` (не в git).
- Самообновление проверено вручную (2.0 → 2.1), автотестами не покрыто;
  сервер `https://updates.borealis-soft-home.duckdns.org:51943/` (только HTTPS; FTP — 192.168.0.100 в LAN).
  Роутер без NAT loopback: изнутри LAN публичный адрес недоступен (проверка/«Проверить» виснут до таймаута),
  а снаружи недоступен FTP 192.168.0.100 — HTTP-проверки делать из внешней сети.
- FTPS/TLS для FTP-доставки (сейчас только обычный FTP) — при необходимости.
- Открыто: выбор платформы сбрасывался на первую после действия с этапом — в тестах (VM и headless-окно, в т.ч.
  настоящий клик) не воспроизводится; ждём точные шаги.
- Проект, донастроенный под 2.2 (`Versions/Windows`), нельзя открывать в 2.1: она видит папки платформ версиями,
  а её движок падает на них при сборке. Обратной совместимости вниз нет.
- **CLI выпуска `umcli` (план, не реализован в 2.3)** — выпуск без окна для агентов, `release.ps1` и CI: сначала вынос
  логики выпуска из `MainWindowViewModel` в Core (`Core/Release/`), затем консольный `UpdateManager.Cli`.
  Спек: `docs/superpowers/specs/2026-10-28-cli-release-design.md`. Не начато.

## Стек

- C# / .NET 10 (`net10.0-windows`), SDK-style проекты; UI — Avalonia 12.1.2 (Fluent), CommunityToolkit.Mvvm;
  шрифт Golos Text.
- Движок патчей: SimplePatchTool (`lib/`, CLR 2.0, HintPath). Подпись — `System.Security.Cryptography.Xml`.
- FTP: FluentFTP 52.1.0 (NuGet). Шифрование секретов: DPAPI (`System.Security.Cryptography.ProtectedData`).
- Тесты: xUnit v3.

## Документация

- [PRODUCT.md](../PRODUCT.md) — продукт для дизайн-скилла: пользователи, сценарий, принципы, планы (Android).
- [DESIGN.md](../DESIGN.md) — дизайн-система (мир «каталог»): токены, типографика, компоненты, диалоги.
- [docs/plan.md](docs/plan.md) — план: что дальше (самообновление, CLI, Android, FTPS), что сделано, от чего отказались.
- [docs/superpowers/specs/](../docs/superpowers/specs/) — спеки 2.0 (переезд на Avalonia), самообновления и CLI выпуска.
- `lib/README.md` — откуда DLL движка; [docs/engine-reference.md](docs/engine-reference.md) — справочник API и исторические примеры, которые нужно сверять с текущими DLL и кодом.
- [AGENTS.md](../AGENTS.md) — краткая вводная и маршруты для Codex; подробная общая справка остаётся здесь.

## Правила работы (от пользователя)

- Общение на русском языке.
- **Коммитить самому, по темам, по ходу работы**: сделал → проверил (сборка + тесты) → коммит.
  Никогда не пушить и не предлагать push — пользователь пушит сам.
- Стиль кода: не выравнивать поля/присваивания пробелами; следовать стилю окружающего кода.
