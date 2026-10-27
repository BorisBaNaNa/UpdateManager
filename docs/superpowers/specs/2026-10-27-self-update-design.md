# Самообновление UpdateManager («уроборос») — дизайн

## Цель

UpdateManager сам узнаёт о своей новой версии, скачивает её и перезапускается обновлённым.
Патчи для самого UpdateManager собираются им же (догфуд) и раздаются по HTTP, как у любого проекта.

Успех: на машине с версией N при запуске (или по пункту меню) появляется предложение обновиться
до N+1; после согласия приложение закрывается, `SelfPatcher.exe` подменяет файлы и запускает
UpdateManager уже версии N+1.

## Решения (согласовано)

| Вопрос | Решение |
|---|---|
| Когда проверять | Тихая фоновая проверка при старте + пункт меню «Справка → Проверить обновления…» |
| Откуда адрес | Только `App.config` (appSettings), без UI-правки |
| Адрес | `http://updates.borealis-soft-home/UpdateManager/VersionInfo.info` (сервер пока недоступен) |
| RSA-подпись | Проверяется, если в `App.config` задан публичный ключ; иначе без проверки |
| Механизм | Встроенный движок SimplePatchTool в self-patching-режиме (не отдельный лаунчер) |
| Разработчик | `AssemblyCompany("BorealisSoft")` — не меняем |
| Настройка проекта для себя | Распознавание «себя» в коде + предложение применить пресет (не скрипт) |

## API движка (сверено рефлексией по `lib/SimplePatchTool.dll`)

- `new SimplePatchTool(rootPath, versionInfoUrl)`
- `CheckForUpdates(bool checkVersionOnly)` — асинхронно; итог в `Result`
  (`Success` = есть обновление, `AlreadyUpToDate`, `Failed`), `NewVersion`, `FailDetails`.
- `Run(bool isSelfPatchingApp)` — при `true` файлы качаются в кэш (exe занят), а не поверх.
- `ApplySelfPatch(string selfPatcherExecutable, string postSelfPatchExecutable)` — запускает
  self-patcher; после этого приложение должно выйти.
- `UseVersionInfoVerifier(XMLVerifier)` / `UsePatchInfoVerifier(XMLVerifier)` — RSA.
- `IsRunning`, `Cancel()`, `FetchLog()`, `FetchOverallProgress()`.

## Конфиг (`App.config`)

```xml
<appSettings>
  <add key="SelfUpdate.VersionInfoUrl" value="http://updates.borealis-soft-home/UpdateManager/VersionInfo.info" />
  <add key="SelfUpdate.PublicKey" value="" />
</appSettings>
```

- Пустой `VersionInfoUrl` → самообновление выключено, пункт меню неактивен.
- Пустой `PublicKey` → без проверки подписи. Значение — RSA-XML из `public.key` проекта.
- `UpdateManager.exe.config` сам входит в патч, поэтому смена сервера разъезжается с обновлением.

## Компоненты

### Домен — `Core/SelfUpdate/` (namespace `UpdateManager.Core.SelfUpdate`)

- **`SelfUpdateConfig`** — читает appSettings: `VersionInfoUrl`, `PublicKey`, `IsEnabled`,
  `BaseDownloadUrl` (URL без `VersionInfo.info`, для пресета проекта).
- **`SelfIdentity`** — «кто я»: `ProductName`/`CompanyName` берутся из атрибутов сборки
  запущенного приложения (`AssemblyProduct`, `AssemblyCompany`), не дублируются строками.
  `IsSelf(string exePath)` — сравнивает с `FileVersionInfo` указанного exe
  (без учёта регистра). Текущая версия — `AssemblyFileVersion`.
- **`SelfUpdateChecker`** — создаёт `SimplePatchTool` (rootPath = `AppDomain.CurrentDomain.BaseDirectory`,
  верификаторы при наличии ключа), вызывает `CheckForUpdates(false)` (сверка по файлам — работает
  и без version holder `.sptv`), ждёт завершения в фоновом потоке, возвращает
  `SelfUpdateCheckResult { Status: UpdateAvailable | UpToDate | Failed, NewVersion, Error }`.
- **`SelfUpdateOperation : IEngineOperation`** — обёртка над `Run(true)` для `OperationProgressForm`
  (как `PatchVerifier`). Типы патчей: repair + incremental + installer (движок выберет сам).
- **`SelfUpdateLauncher.Apply()`** — ищет self-patcher:
  1. `<app>\SPPatcher\SelfPatcher.exe` (куда его раскладывает движок при self-patching-патче);
  2. `<app>\SelfPatcher\SelfPatcher.exe` (наш встроенный, Content → bin) — запасной.
  Нет ни одного → `InvalidOperationException` с понятным текстом. Иначе
  `ApplySelfPatch(selfPatcher, <app>\UpdateManager.exe)`.

### Пресет проекта для «себя» — `Core/SelfUpdate/SelfProjectPreset`

`Apply(ProjectSettings s, SelfUpdateConfig cfg)`:
- `IsSelfPatchingApp = true`;
- `BaseDownloadURL = cfg.BaseDownloadUrl` (если в конфиге задан);
- в `IgnoredPaths` добавляются (без дублей) `*.pdb`, `*.vshost.*`, логи движка.

`IsApplied(ProjectSettings s)` = `IsSelfPatchingApp` уже `true` (пресет не навязывается повторно).

### UI / презентер

- **Меню**: «Справка → Проверить обновления…» (меню «Справка» создаётся, если его нет).
  Событие `CheckSelfUpdateRequested` в `IMainView`.
- **Старт** (`Shown`): если `cfg.IsEnabled` и не `Debugger.IsAttached` — фоновая проверка.
  `Failed` → молча. `UpdateAvailable` → диалог «Доступна версия X (у вас Y). Обновить сейчас?».
- **Ручная проверка**: всегда явный ответ — «у вас последняя версия», «доступна X…», или ошибка
  с `FailDetails`. Под отладчиком тоже работает, но с предупреждением, что обновится `bin`.
- **Обновление**: «Да» → `ShowOperation(new SelfUpdateOperation(...))` → при успехе
  «Обновление скачано. Перезапустить сейчас?» → `SelfUpdateLauncher.Apply()` + `Application.Exit()`.
  Если пользователь отказался перезапускаться — кэш остаётся, при следующей проверке движок
  докачает/применит.
- Пока идёт другая модальная операция (сборка/доставка), стартовый диалог не показывается —
  результат фоновой проверки отбрасывается; обновиться можно через пункт меню.
- Логику держит отдельный **`SelfUpdatePresenter`** (не раздуваем `MainPresenter`), собирается
  в `Program.Main`.

### Распознавание «себя» в проекте (MainPresenter)

- В `ApplyBuildSource(..., persist: true)` и `OnBrowseMainExecutable`: если найденный главный exe
  `SelfIdentity.IsSelf(...)` и `!SelfProjectPreset.IsApplied(settings)` → вопрос
  «Похоже, это сам UpdateManager. Применить настройки самообновления?» → пресет + `SaveSettings`.
  Никаких тихих изменений.
- Перед сборкой патча: exe — «мы», а `IsSelfPatchingApp == false` → предупреждение
  «Без IsSelfPatchingApp клиент попытается перезаписать запущенный exe — самообновление сломается.
  Всё равно собрать?».

## Обработка ошибок

| Ситуация | Поведение |
|---|---|
| Сервер недоступен при старте | Молча, ничего не показываем |
| Сервер недоступен при ручной проверке | `ShowError` с `FailDetails` |
| Подпись не сошлась | «Патч не прошёл проверку подписи», ничего не применяется |
| Нет self-patcher | Понятная ошибка, приложение не закрывается |
| Отмена скачивания | `Cancel()` через окно прогресса, файлы приложения не тронуты |
| Запуск под отладчиком | Автопроверки нет |

## Тестирование

Юнит-тестов в репозитории нет. Сейчас — только сборка без ошибок. Сервер спит, поэтому
сквозной ручной прогон откладывается:

1. Собрать 1.0.3 → проект UpdateManager (пресет применён) → патч.
2. Поднять версию до 1.0.4 → патч → доставить на `updates.borealis-soft-home/UpdateManager`.
3. Запустить 1.0.3 из отдельной папки → предложение → скачивание → перезапуск → FileVersion `UpdateManager.exe` = 1.0.4.
4. Негатив: испорченная подпись → отказ; сервер выключен → при старте тишина, вручную — ошибка.

Опционально без сервера: в копии `UpdateManager.exe.config` подменить URL на `file:///…`.

## Вне рамок

- Правка адреса сервера через UI.
- «Пропустить эту версию», расписание проверок, канал бета-версий.
- HTTPS-специфика (работает, если сервер её даст, отдельной логики нет).
