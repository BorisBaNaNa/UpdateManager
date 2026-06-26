# CLAUDE.md

Инструкции Claude Code (claude.ai/code) для работы с репозиторием **UpdateManager**.

## Что это

**UpdateManager** — самостоятельное Windows-приложение (WinForms, .NET Framework 4.8) для
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

Рабочее приложение, сквозной цикл закрыт на догфуде (инструмент собирает патчи сам для себя в
тестовом проекте). Реализовано:

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
  ссылки без слеша). Встроенный self-patcher (`UpdateManager/SelfPatcher/`, Content → bin) кладётся в
  каждый новый проект.

Git инициализирован, история ведётся по шагам.

## Архитектура

MVP + доменный слой, инверсия зависимостей (домен не знает про WinForms):

- `Forms/` — UI (реализуют `Views/IMainView`); `Presenters/MainPresenter` — логика окна; композиция
  собирается в `Program.Main`.
- `Core/` по подпапкам со строгими namespace'ами `UpdateManager.Core.*`: `Project/`, `Delivery/`,
  `Operations/` (`IEngineOperation`, `PatchBuilder`, `PatchVerifier`), `Versioning/`, `Common/`.
- `Core/Project/ProjectService` — **единственный** слой, напрямую зовущий движок.

## Осталось

- Подпись манифеста (RSA, `SimplePatchToolSecurity`) — на потом.
- FTPS/TLS для FTP-доставки (сейчас только обычный FTP) — при необходимости.

## Стек

- C# / WinForms / .NET Framework 4.8 (классический `.csproj`, не SDK-style).
- Движок патчей: SimplePatchTool (`SimplePatchToolCore`, `SimplePatchToolSecurity`).
- FTP: **FluentFTP** (вендорится в `lib/FluentFTP.dll`, ссылка по HintPath — как DLL движка;
  под net48 без доп. зависимостей). Шифрование секретов: DPAPI (`System.Security`).

## Документация

- [docs/idea.md](docs/idea.md) — полная идея, мотивация, открытые вопросы.
- [docs/engine-reference.md](docs/engine-reference.md) — API SimplePatchTool: `PatchCreator`,
  `PatchUpdater`, security, формат сервера, консольные команды.
- [docs/plan.md](docs/plan.md) — предлагаемый план реализации по шагам.
- [docs/source-project-context.md](docs/source-project-context.md) — как патчи устроены в
  проекте-доноре Interactive Book (пример интеграции: `ConnectionSettings`, build-хук, layout сервера).

## Правила работы (от пользователя)

- Общение на русском языке.
- **Не делать git-коммиты без явной просьбы.** Никогда не пушить и не предлагать push —
  пользователь пушит сам.
- Стиль кода: не выравнивать поля/присваивания пробелами; следовать стилю окружающего кода.
