# UpdateManager — работа с проектом

Общайся на русском. Это универсальный Windows-инструмент сборки, доставки и проверки обновлений: C# / .NET 10, Avalonia 12, SimplePatchTool. Не привязывай его к конкретному приложению-потребителю. Версия — в `Directory.Build.props`; `main` содержит 2.x, WinForms 1.x осталась в ветке `v1`.

## Перед правками

- Проверь ветку и `git status --short`, сохрани чужие изменения. Дополнительных ограничений для `main` в проектных источниках не задано.
- Сделал тему → проверил сборку и тесты → самостоятельно закоммить эту тему. Не спрашивай разрешение на обычный коммит. Никогда не пушь и не предлагай push: пользователь делает его сам.
- Правь файлы инструментом редактирования (`apply_patch` в Codex), не через sed, shell heredoc или генерирующий скрипт: в Windows ломались экранирование и кириллица. `scripts/release.ps1` сохраняй в UTF-8 с BOM.
- Следуй стилю соседнего кода, без выравнивания присваиваний пробелами. Core сохраняет блочные namespace и отключённый nullable; App использует MVVM и `IDialogService`.
- Прочитай источники по теме ниже **до** зависимых решений. `.claude/CLAUDE.md` остаётся подробной общей справкой; её Claude-specific названия инструментов не являются API Codex. Автоматическое исполнение Claude hooks или загрузка файлов `.claude` здесь не предполагаются.

## Карта знаний

| Задача | Прочитать |
|---|---|
| Архитектура, контракты этапов, каталог выпусков | `.claude/CLAUDE.md`, соответствующий код `src/UpdateManager.Core` и `src/UpdateManager.App/ViewModels` |
| Проект, версии, удаление, откат, совместимость | `README.md` (версии и структура проекта), `.claude/CLAUDE.md`, `Core/Project/ProjectService`, `ProjectUpgrade`, тесты `CoreTests` и `EnginePipelineTests` |
| Патчи, API движка, проверка скачивания | `.claude/docs/engine-reference.md`, `Core/Operations`, `lib/README.md`; исторические примеры API сверять с текущими DLL и кодом |
| Android, подпись, связь с Windows | `docs/superpowers/specs/2026-10-28-android-platform-design.md`, актуальные уточнения `.claude/CLAUDE.md` и `README.md`, `AndroidTests.cs` |
| Доставка, FTP, RSA | `.claude/CLAUDE.md`, `Core/Delivery`, `Core/Security`, `Core/Operations/AndroidChannelVerifier.cs` |
| UI, дизайн, жесты, headless-проверки | `PRODUCT.md`, `DESIGN.md`, `.impeccable/surfaces/`, skill `.agents/skills/impeccable/SKILL.md`, `tests/UpdateManager.Tests/HeadlessUi.cs` |
| Самообновление | `docs/superpowers/specs/2026-10-27-self-update-design.md`, `Core/SelfUpdate`, `.claude/CLAUDE.md` |
| Выпуск, версия, заметки, GitHub Release | skill `.agents/skills/release-updatemanager/SKILL.md`, `scripts/release.ps1`, `docs/releases/` |
| Планы / CLI umcli | `.claude/docs/plan.md`, `docs/superpowers/specs/2026-10-28-cli-release-design.md`; CLI запланирован, не реализован |
| История и исходный потребитель | `.claude/docs/idea.md`, `.claude/docs/source-project-context.md`; исторический контекст, не текущие требования |

Пути `Core/...` в таблице относительно `src/UpdateManager.Core/`. Если тема неизвестна: `rg -n -i --hidden "термин" .claude/CLAUDE.md .claude/docs docs PRODUCT.md DESIGN.md src tests`.

## Сквозные ограничения

- Настройки движка изменяет `ProjectService`. Создавай движок через `ProjectService.CreateEngine`: он перенаправляет `versionsPath` на `Versions/Windows`, иначе DLL считает `Android` номером версии. DLL движка не менять ради обхода.
- `BaseDownloadURL` обязателен до сборки, нормализуется с завершающим `/`. «Проверить» реально скачивает патч клиентом, одной HTTP-пробы манифеста недостаточно.
- Повтор этапа сбрасывает его и следующие этапы версии. Удаление версии не меняет сервер и `Output/`; откат требует последующей доставки и проверки. Выпущенные удалённые номера повторно не использовать на соответствующей платформе.
- Android: подпись RSA SHA-256 PKCS#1 v1.5 по сырым байтам; маски, признак подписи и хеши — из записи выпуска. Доставка из сохранённой `Versions/Android/<версия>`. Обе платформы включены — версия Android связана с Windows; только Android — `versionName` из JSON в папке билда и подпапках, при отсутствии однозначной допустимой версии — ручной ввод. `versionCode` номером выпуска не является. Подпись APK вне инструмента.
- Доставка зеркальная и может удалять старые файлы назначения; не запускать её, выпуск или откат как проверку документации. Реквизиты FTP хранятся в профиле пользователя, пароль DPAPI; ключи и секреты не коммитить.
- Проект после перехода на раскладку 2.2 нельзя открывать в 2.1. Самообновление поставляет отдельные файлы, не single-file; self-patcher — `SPPatcher/SelfPatcher.exe`.

## Проверки

Из корня: `dotnet build UpdateManager.sln`, затем `dotnet test tests/UpdateManager.Tests`. В этой Windows-среде SDK доступен также как `C:\Program Files\dotnet\dotnet.exe` (PowerShell: `& 'C:\Program Files\dotnet\dotnet.exe' ...`). Если запущенное приложение держит DLL, используй `--artifacts-path <временный-каталог>` вне рабочего дерева. Не закрывай приложение пользователя ради теста.

UI проверять через Avalonia.Headless и визуально по рендеру; снимки — `.impeccable/review/` (не Git). Самообновление проверялось вручную; это не автоматическое покрытие. Для документации проверяй пути, ссылки, UTF-8 и `git diff --check`.

## Приватная память

Используй нативную память активного Codex home с областью этого проекта. Если фоновая сводка отсутствует, проверь `memories*/extensions/external_agent_import/resources/*/scope.json`, выбери только ресурс, чей `cwd` совпадает с корнем текущего проекта, прочитай его `MEMORY.md` и нужные заметки. Не обходи память других проектов и не копируй её содержание в Git. Наличие файлов само по себе не доказывает загрузку на старте; состояние переноса см. `docs/knowledge-migration.md`.
