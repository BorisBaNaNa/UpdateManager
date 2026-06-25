# План реализации UpdateManager

Предлагаемый порядок. Перед стартом — закрыть открытые вопросы из [idea.md](idea.md)
(инкрементальные патчи, RSA, фреймворк, протокол заливки).

## Шаг 0. Решить про фреймворк

- Заготовка уже на .NET Framework 4.8 (WinForms, классический csproj).
- Проверить совместимость `lib/SimplePatchTool.dll` (вероятно .NET Standard 2.0 из Unity) —
  подключить Reference и собрать пробный вызов `new PatchCreator(...)`.
- Если совместимо — остаёмся на 4.8. Если нет/хочется современнее — мигрировать на .NET 8 WinForms.

## Шаг 1. Подключить движок

- Добавить в `UpdateManager.csproj` Reference на `lib/SimplePatchTool.dll` и
  `lib/SimplePatchToolSecurity.dll` (HintPath).
- Скомпилировать «пустой» вызов, убедиться что типы видны.

## Шаг 2. UI сборки патча (вкладка CREATE)

Повторить поля legacy-окна (см. [engine-reference.md](engine-reference.md)), но в WinForms:

- Поля путей с кнопкой обзора: root, prevRoot, prevOutputPath, output.
- ProjectName, Version, BaseDownloadURL.
- Чекбоксы: Repair, Installer, (Incremental — авто по наличию prevRoot), SkipUnchanged.
- Выбор CompressionFormat (3 шт.).
- TextArea для IgnoredPaths.
- Кнопка «Создать патч» → `PatchCreator.Run()` + откачка лога в текстовое поле (таймер/поток).

## Шаг 3. Пресеты (JSON)

- Сохранение/загрузка всех параметров в JSON (один пресет = один проект).
- Список пресетов, кнопки Save / Save As / Load / Delete.
- Для универсальности — именно пресеты делают инструмент не привязанным к одному проекту.

## Шаг 4. FTP-заливка

- NuGet **FluentFTP**.
- Параметры подключения в пресете (хост, порт, логин, пароль — пароль не хранить в открытом
  виде, либо DPAPI/спрашивать).
- Заливка папки `output/` в целевую папку сервера (рекурсивно).
- Прогресс-бар.

## Шаг 5. Фича «Проверить»

- HTTP-GET `VersionInfo.info` по version-info URL.
- Распарсить манифест, взять `BaseDownloadURL`, проверить доступность нескольких файлов патча
  (HEAD/GET) — поймать 404 и забытый `BaseDownloadURL`.
- Понятный отчёт: что доступно, что нет.

## Шаг 6. (опц.) Security-вкладка

- Генерация RSA-пары, подпись/проверка манифеста через `SimplePatchToolSecurity`.

## Шаг 7. (опц.) Обновление ссылок — `PatchUpdater`

- Если `BaseDownloadURL` нельзя задать на этапе создания — вкладка для проставления ссылок
  в готовый `VersionInfo.info`.
