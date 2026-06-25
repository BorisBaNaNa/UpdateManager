# CLAUDE.md

Инструкции Claude Code (claude.ai/code) для работы с репозиторием **UpdateManager**.

## Что это

**UpdateManager** — самостоятельное Windows-приложение (WinForms, .NET Framework 4.8) для
**сборки патчей и заливки их на сервер обновлений**. Инструмент **универсальный**: он не привязан
к конкретному приложению и должен уметь собирать патчи для любого проекта (указал папку билда →
собрал патч → залил на сервер).

Под капотом — движок **SimplePatchTool** (`lib/SimplePatchTool.dll`, `lib/SimplePatchToolSecurity.dll`),
класс `PatchCreator` и связанные. Цель — заменить ручную работу в редакторном окне SimplePatchTool
(которое жило внутри Unity-проекта) на отдельную программу с пресетами и FTP-загрузкой.

## Зачем (мотивация)

- Убрать ручную работу в редакторном окне SimplePatchTool внутри Unity.
- Автоматизировать заливку патча на сервер (FTP).
- Закрыть класс ошибок с забытым/неверным `BaseDownloadURL`: при нём проверка обновлений
  проходит успешно, а **скачивание файлов патча падает** (`DownloadError: ... could not be
  downloaded`). Поэтому в инструменте нужна фича «Проверить» — HTTP-проба `VersionInfo.info`
  и файлов патча.

## Текущее состояние

- Заготовка VS-солюшна: `UpdateManager.sln` + проект `UpdateManager/` (WinForms, .NET FW 4.8,
  классический csproj, `Form1`).
- Движок лежит в `lib/` (скопирован из Unity-проекта Interactive Book). Ещё **не подключён**
  как Reference в `.csproj` и не интегрирован в UI.
- Git **не инициализирован**.

## Стек

- C# / WinForms / .NET Framework 4.8 (классический `.csproj`, не SDK-style).
- Движок патчей: SimplePatchTool (`SimplePatchToolCore`, `SimplePatchToolSecurity`).
- FTP: планируется **FluentFTP** (через NuGet).

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
