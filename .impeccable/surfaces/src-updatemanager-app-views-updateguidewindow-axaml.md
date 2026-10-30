---
version: 1
slug: "src-updatemanager-app-views-updateguidewindow-axaml"
primary_target: "src/UpdateManager.App/Views/UpdateGuideWindow.axaml"
related_targets: ["src/UpdateManager.App/ViewModels/UpdateGuideViewModel.cs"]
---

# Как подключить обновления

Mode: Read. Немодальное справочное окно Windows/Avalonia; доступно без проекта через «Справка».
Читатель — владелец приложения, впервые подключающий Windows/Android-обновления. Текст сначала подготовлен
в docs/UPDATE_SYSTEM_USER_GUIDE.md; окно использует тот же встроенный документ. Исходная методичка для агента
сохранена отдельно. Никаких автоматических изменений проекта или отметок выполненной настройки.

## Direction contract

THESIS: «Маршрут первого обновления» — три последовательных этапа с раскрываемыми темами, а не длинная
статья без ориентира. Выбран пользователем после сравнения трёх эскизов.
OWN-WORLD: Существующий Catalog Sleeve: Golos Text, нейтральные Ground/Panel/Selected, тонкие линии,
квадратные кнопки, без теней, новых акцентов и анимации. Никакого браузера в готовом приложении.
STORY: Читатель готовит клиент, настраивает издатель, затем проверяет переход A → B. Можно открыть любую
тему и держать окно рядом с проектом, не блокируя настройки.
FIRST VIEWPORT: Заголовок и пояснение сверху; ниже горизонтальная полоса трёх этапов; в середине одна
прокручиваемая колонка чтения с раскрываемыми темами, без карточек. Внизу общий footer: документы,
назад, закрыть и одна главная кнопка следующего этапа. Минимум 720×520, по умолчанию 1000×720.
FORM: Маршрут первого обновления, кандидат 6, выбранный третий эскиз; seed:37abe8b34a5c0aa7.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
