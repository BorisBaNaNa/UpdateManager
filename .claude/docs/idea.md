# Идея: отдельное ПО для сборки патчей

## Суть

Отдельное **WinForms-приложение** для удобного создания патчей и заливки на сервер обновлений.
Сценарий: на локальном ПК указываешь параметры → собираешь патч → заливаешь по FTP в папку выдачи.

**Инструмент универсальный** — не привязан к Interactive Book. Параметры (root-папка билда,
имя/версия проекта, сервер, ключи) задаются пользователем и сохраняются в пресетах. Interactive
Book — лишь первый потребитель и образец интеграции (см. [source-project-context.md](source-project-context.md)).

## Зачем

- Убрать ручную работу в редакторном окне SimplePatchTool, жившем внутри Unity-проекта.
- Автоматизировать заливку патча на сервер.
- Закрыть класс ошибки с забытым `BaseDownloadURL` (см. ниже).

## Осуществимость — подтверждена

- Движок уже готов: `lib/SimplePatchTool.dll` содержит `SimplePatchToolCore` (классы `PatchCreator`,
  `PatchUpdater`, `PatchUtils`, enum `CompressionFormat`, `PatchResult`). `lib/SimplePatchToolSecurity.dll`
  содержит `XMLSigner`, `SecurityUtils` (RSA).
- Эталонный пример вызова всех операций — был в Unity: `PatcherEditorLegacy.cs`. Его разбор
  перенесён в [engine-reference.md](engine-reference.md).
- Standalone-приложение подключает эти DLL напрямую (Reference в `.csproj`).

## Параметры инструмента (с пресетом в JSON)

- `root` — папка свежего билда (актуальная версия).
- `output` — папка вывода патча (должна быть пустой).
- `ProjectName`, `Version`.
- `BaseDownloadURL` — базовый URL, по которому клиент качает файлы патча. **Критичный параметр.**
- `IgnoredPaths` — список игнорируемых путей (по одному на строку), напр. `*output_log.txt`.
- Флаги: создавать Repair / Installer / Incremental патч.
- Опционально: путь к предыдущей версии (для инкрементального патча), путь к патч-файлам
  предыдущей версии (для пропуска неизменённых), форматы сжатия, RSA-ключ для подписи.
- FTP-параметры: хост, порт, логин/пароль, целевая папка.

## Фича «Проверить» (защита от частой ошибки)

HTTP-проба `VersionInfo.info` + наличия файлов патча (как `curl`) — ловит 404 и забытый
`BaseDownloadURL`.

**Ошибка `BaseDownloadURL`:** если в `VersionInfo.info` не указан (пустой/неверный) `BaseDownloadURL`,
проверка обновлений проходит успешно (`Checking for updates → New version available`), а скачивание
файлов патча падает с `DownloadError: ... could not be downloaded` (напр. `SPPatcher\SelfPatcher.exe`,
`Installer.patch`). Причина: `VersionInfo.info` тянется по отдельному version-info URL, а файлы
патча качаются по `BaseDownloadURL` из манифеста. По логу `spt_logs.txt`: успешный
«Retrieving version info» + «could not be downloaded» на этапе загрузки = первым делом проверять
`BaseDownloadURL`, а не наличие файлов на сервере.

## Открытые вопросы (НЕ решены — обсудить в начале следующей сессии)

1. **Инкрементальные патчи** — нужны ли? (увеличивают сложность; SimplePatchTool их умеет).
2. **RSA-подпись** — нужна ли? `SelfPatchProcessor` уже умеет проверять `_versionInfoRsa`/`_patchInfoRsa`;
   `SimplePatchToolSecurity` умеет генерировать ключи и подписывать XML.
3. **Целевой фреймворк** — оставить .NET Framework 4.8 (текущая заготовка, проще с WinForms-дизайнером
   и совместимостью с DLL движка) или перейти на современный .NET 8 (нужно проверить, что DLL движка
   совместимы — они из Unity, вероятно .NET Standard 2.0, тогда подключатся).
4. **FTP vs другие протоколы** — пока план FTP (FluentFTP). Возможны SFTP/обычная папка.
