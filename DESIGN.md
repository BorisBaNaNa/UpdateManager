---
name: UpdateManager
description: A release catalog for SimplePatchTool projects; every version is an entry, its state a coded strip of stage blocks.
colors:
  ground: "#EFEFED"
  panel: "#E5E5E3"
  selected: "#D6D6D3"
  ink: "#111111"
  ink-inverse: "#F4F4F2"
  muted: "#595957"
  hairline: "#C9C9C6"
  pending: "#8A8A87"
  failed: "#C8262B"
  ground-dark: "#111111"
  panel-dark: "#161616"
  selected-dark: "#242424"
  ink-dark: "#F2F2F0"
  ink-inverse-dark: "#111111"
  muted-dark: "#9B9B98"
  hairline-dark: "#2C2C2C"
  pending-dark: "#686866"
  failed-dark: "#FF5A4F"
  stage-build: "#2E86AB"
  stage-patch: "#F3B61F"
  stage-files: "#9B5DE5"
  stage-delivery: "#1FA39A"
  stage-verify: "#76B041"
  stage-ink: "#111111"
typography:
  display:
    fontFamily: "Golos Text"
    fontSize: "72px"
    fontWeight: 400
    letterSpacing: "-2px"
    fontFeature: "tnum"
  headline:
    fontFamily: "Golos Text"
    fontSize: "56px"
    fontWeight: 400
    letterSpacing: "-1.5px"
  title:
    fontFamily: "Golos Text"
    fontSize: "17px"
    fontWeight: 600
  title-sm:
    fontFamily: "Golos Text"
    fontSize: "15px"
    fontWeight: 600
  entry:
    fontFamily: "Golos Text"
    fontSize: "15px"
    fontWeight: 400
    fontFeature: "tnum"
  body:
    fontFamily: "Golos Text"
    fontSize: "14px"
    fontWeight: 400
  detail:
    fontFamily: "Golos Text"
    fontSize: "12px"
    fontWeight: 400
  label:
    fontFamily: "Golos Text"
    fontSize: "11px"
    fontWeight: 500
    letterSpacing: "1.6px"
rounded:
  none: "0px"
spacing:
  stage-gap: "6px"
  button-gap: "10px"
  row: "11px"
  inset: "20px"
  section: "24px"
  gutter: "44px"
components:
  button-primary:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.ink-inverse}"
    typography: "{typography.title-sm}"
    rounded: "{rounded.none}"
    padding: "11px 22px"
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.none}"
    padding: "5px 12px"
  button-quiet-hover:
    backgroundColor: "{colors.selected}"
  catalog-row:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.entry}"
    padding: "11px 20px"
  catalog-row-selected:
    backgroundColor: "{colors.selected}"
    textColor: "{colors.ink}"
  stage-block-pending:
    backgroundColor: "transparent"
    textColor: "{colors.muted}"
    rounded: "{rounded.none}"
    padding: "12px 14px"
    height: "112px"
  stage-block-done-build:
    backgroundColor: "{colors.stage-build}"
    textColor: "{colors.stage-ink}"
    rounded: "{rounded.none}"
    padding: "12px 14px"
    height: "112px"
  stage-block-failed:
    backgroundColor: "transparent"
    textColor: "{colors.failed}"
    rounded: "{rounded.none}"
    padding: "12px 14px"
    height: "112px"
  stage-chip:
    rounded: "{rounded.none}"
    width: "14px"
    height: "8px"
---

# Design System: UpdateManager

## Overview

**Creative North Star: "The Catalog Sleeve"**

The world borrows from a record-label catalog sleeve (Saville / Factory): a matte ground, hairline rules, ink type, and colour that appears only as code. Every version of an application is a catalog entry. Its state is a strip of square stage blocks per platform (Windows: Билд, Патч, Доставка, Проверка; Android: Билд, Подпись, Доставка, Проверка) read left to right: filled means done, a hairline outline means not yet, a heavy red outline means failed. The strip is always spelled out in words beside or inside it, so colour is never the only carrier of meaning.

The surface is a tool, not a showcase. Density is calm: one oversized version number per screen, one row of stage blocks, one primary ink button that names the next step, and a quiet label/value/action table under a hairline. Everything else is neutral grey on matte ground. Light and dark themes are the same world with the values inverted; stage colours are identical in both because they are code, not decoration.

Scope as built: every window is drawn in this world: the main window (start screen and project screen) and the dialogs (project settings, delivery, FTP connection, FTP browser, operation log, message and prompt), which share one dialog grammar (see Components → Dialogs).

**Key Characteristics:**
- Matte neutral ground, ink type, hairline rules; no gradients, no shadows, no rounded corners.
- Colour exists only as the five stage hues; state is encoded as fill (done), outline (pending), red outline (failed).
- One display-size version number is the only headline; metadata sits beside it on the baseline.
- One filled ink button per screen; every other action is a hairline-outlined quiet button or a menu item.
- Every coded state is also written in Russian words («№ 5 · проверка не прошла»).

## Colors

A near-achromatic warm-grey ground with ink type, plus five saturated stage hues that are reserved for release state.

### Primary
- **Ink** (`ink` / `ink-dark`): all primary type, the filled primary button, the selected catalog entry's text. Inverts per theme; the primary button's label uses **Ink Inverse** (`ink-inverse`).

### Secondary (stage code)
- **Build Blue** (`stage-build`): Билд stage when done. Also the Fluent accent colour for stock controls.
- **Patch Amber** (`stage-patch`): Патч stage when done.
- **Files Violet** (`stage-files`): Подпись stage of the Android track when done (files signed by mask).
- **Delivery Teal** (`stage-delivery`): Доставка stage when done.
- **Verify Green** (`stage-verify`): Проверка stage when done.
- **Stage Ink** (`stage-ink`): text on any filled stage block, dark in both themes because all stage hues are light enough to carry it.

### Tertiary
- **Failure Red** (`failed` light / `failed-dark` dark): the 2px outline and text of a failed stage block and chip. Nothing else is red.

### Neutral
- **Matte Ground** (`ground` / `ground-dark`): window background.
- **Panel Grey** (`panel` / `panel-dark`): the 280px left column (recent projects or release catalog), one step off the ground.
- **Selected Grey** (`selected` / `selected-dark`): hover and selection fill for catalog rows and quiet buttons. Selection is a fill, never a coloured bar.
- **Muted Grey** (`muted` / `muted-dark`): secondary text: paths, catalog second lines, status line, table labels, list labels, pending stage text.
- **Hairline** (`hairline` / `hairline-dark`): 1px rules under the menu, between catalog rows, beside the panel, above the info table, and quiet button outlines.
- **Pending Grey** (`pending` / `pending-dark`): outline of not-yet-done stage blocks and chips; quiet button hover border; disabled quiet text.

### Named Rules
**The Colour Is Code Rule.** The five stage hues and the failure red appear only on stage blocks and stage chips (failure red also on the failure line and box that report a failed stage or operation). Text, buttons, panels, rules and selection stay neutral. The one exception is Build Blue as Fluent's accent inside stock inputs (see Components → Stock Chrome).

**The Same Code In Both Themes Rule.** Stage hues and Stage Ink do not change between light and dark; only neutrals and Failure Red adapt.

**The Spelled-Out Rule.** Every stage state shown in colour is also present as words: the block title and detail, the catalog line («новый · патч не собран»), and the tooltip («Проверка: не прошла …»).

## Typography

**Display Font:** Golos Text (bundled, OFL; static 400/500/600)
**Body Font:** Golos Text
**Label/Mono Font:** none distinct; version numbers, dates and counts use Golos Text with tabular figures (`tnum`).

**Character:** A single Cyrillic-first grotesk doing everything; hierarchy comes from size and tracking, not from a second face. Large sizes stay at regular weight with tight negative tracking; small sizes carry weight 600 for titles.

### Hierarchy
- **Display** (400, 72px, -2px tracking, tabular): the selected release's version number («2.0.0.0»). Exactly one per screen.
- **Headline** (400, 56px, -1.5px tracking): the featured recent project's name on the start screen, and the empty-state headings «Первый проект» / «Первый выпуск».
- **Title** (600, 17px): project name at the top of the catalog panel.
- **Title Small** (600, 15px): stage block titles, recent project names, primary button label.
- **Entry** (400, 15px, tabular): version in a catalog row.
- **Body** (400, 14px): status line beside the primary button, metadata beside the version («Windows · выпуск 5»), start-screen summary; Fluent default for table values and buttons.
- **Detail** (400, 12px): paths, catalog second line, stage block detail (max 3 lines, ellipsis).
- **Label** (500, 11px, 1.6px tracking, uppercase, muted): list headers in the left panel only («НЕДАВНИЕ ПРОЕКТЫ», «ВЫПУСКИ»).

### Named Rules
**The One Big Number Rule.** The version number is the only display-size text on the project screen; platform and release number sit beside it on the baseline in muted body text, never above it.

**The Tabular Figures Rule.** Any text that is a version, date, number or count uses tabular figures so catalog columns and dates align.

## Layout

A fixed two-column window (default 1100×700, minimum 720×520). Left: a 280px panel with a 1px hairline on its right edge, holding either the recent-projects list (start screen) or the project name, path and release catalog (project screen). Right: a scrolling content column with 44px side gutters, 32px top and 36px bottom margin, stretching to a maximum width of 980px so it does not shift between states.

Vertical rhythm in the content column: display number → 24px → stage strip → 18px → primary button and status line (10px gap between buttons) → 22px → hairline → label/value/action table with 11px vertical row padding and columns 150px / flexible / 128px. The left panel uses a 20px inset; catalog rows are 11px × 20px with a hairline top border.

The stage strip is a custom grid: all blocks in one row with 6px gaps while each block keeps at least 150px; otherwise two per row (2×2 for a four-stage track). Gaps exist only between blocks; the strip's outer edges align with the column. Cell positions snap to whole pixels so outlines never clip.

### Named Rules
**The Single Strip Rule.** Stages render as one strip per platform track; never as a dashboard of equal cards spread across the screen. When a release carries two platforms, the strips stack (Windows first) and each gets a muted 14px caption above it, «Windows · проверка не прошла» / «Android · не проверялся»; with one platform there is no caption. In the catalog the chip strips stack the same way.

## Elevation & Depth

Flat. There are no shadows anywhere. Depth is conveyed by tonal steps only: Ground → Panel → Selected, and by 1px hairlines. Fluent overlays (menus, tooltips) keep their stock behaviour but with square corners.

### Named Rules
**The No Lift Rule.** Nothing casts a shadow or floats. Hover and selection change fill (Selected Grey) or opacity, never elevation.

**The Still Catalog Rule.** No motion. State changes are instant: after an operation the catalog is rebuilt and the strip simply shows the new state, spelled out in words. Only Fluent's own control feedback (scrollbar expand, progress bar) moves.

## Shapes

Square everywhere: corner radius 0 on buttons, stage blocks, chips, and through `ControlCornerRadius` / `OverlayCornerRadius` on every stock Fluent control (inputs, menus, tooltips). Borders are 1px hairlines; the only heavier stroke is the 2px failure outline. The recurring silhouette is the rectangle: large stage blocks (min 112px tall) and their 14×8px miniature chips.

## Components

### Buttons
Restrained, square, typographic.
- **Shape:** square corners (0).
- **Primary:** Ink fill, Ink Inverse label, Title Small (600, 15px), padding 11px × 22px, no border. Label always names the next step («Создать патч», «Доставить», «Проверить снова», «Открыть»). One per screen.
- **Hover / Pressed:** same colours, opacity 0.86 on hover and 0.72 when pressed.
- **Quiet:** transparent with a 1px Hairline outline, padding 5px × 12px (16px × 10px when sitting next to a primary button). Hover fills Selected Grey and darkens the outline to Pending Grey. Disabled keeps the hairline and greys the label to Pending.

### Chips (stage chips)
- **Style:** 14×8px squares, 3px apart, one per stage, in catalog rows and recent-project rows. Same code as stage blocks: filled stage hue when done, 1px Pending outline when not yet, 2px red outline when failed. No text; a tooltip carries «Этап: деталь».

### Cards / Containers
There are no cards. Grouping is done by the left panel's tonal step and by hairlines.

### Navigation
- **Menu bar:** stock Fluent menu (Проект, Выпуск, Безопасность, Вид, Справка), with a 1px hairline beneath it. Secondary and repeat actions (rebuild, redeliver, keys, theme) live here, not on the surface.
- **Catalog list:** rows separated by hairlines; version (Entry) over a muted Detail line «№ N · состояние» or «новый · …», chip strip right-aligned. Hover and selection fill Selected Grey; no accent bar. A recent project whose folder is gone renders at 0.55 opacity with «папка не найдена — нажмите, чтобы убрать из списка».

### Stage Block (signature)
The world's defining component. A square rectangle, min 112px tall, padding 12px × 14px, Title Small stage name over a Detail line (date, method, or first line of the failure reason, max 3 lines).
- **Done:** filled with the stage's hue, Stage Ink text, no border.
- **Pending:** transparent, 1px Pending Grey outline, Muted text («не собран», «не доставлен», «не проверялся», «устарела: патч пересобран»).
- **Failed:** transparent, 2px Failure Red outline, red text («не прошла 27.10.2026 21:16» + reason).
- **Actionable:** in each platform's current release (the newest entry carrying that platform), done stages and the first not-done stage are buttons (no Fluent chrome). An underlined 12px SemiBold action line sits at the bottom («Сменить папку билда», «Пересобрать», «Подписать заново», «Доставить снова», «Проверить снова»). Hover sets an Ink outline, or opacity 0.88 on filled blocks; pressed sets opacity 0.74. Stages after the first not-done one, archive releases and the start-screen preview are not clickable and show no action line.

### Label / Value / Action Table
Under a hairline: muted label (150px), value in body text trimmed with path ellipsis and a full-text tooltip, quiet button (128px) at the end of each row («Выбрать…», «Настройки…», «Настроить…», «Открыть»).

### Dialogs
One grammar for every modal window.
- **Body:** Ground, inset 24px (22px top). Optional Title heading (17px, 600) naming the task («Куда доставлять патч», «Проект и сервер»); a message's heading is its kind in words («Ошибка», «Подтверждение»), red only for an error.
- **Form rows:** muted label (`field`, 140–170px column) left, stock input right, 10px row spacing; a quiet «Выбрать…» button ends a row that needs a picker. Help and derived values sit under the input as a 12px muted Detail line («Клиенты будут качать патч с …», «Итоговый путь загрузки: …»).
- **Sections:** a hairline (`rule`) with 24px above, then a Title heading 18px below it. No framed groups.
- **Choice of few:** a short list of options (delivery method) renders as catalog rows with a Selected Grey fill, not a dropdown; the fill bleeds 12px into the gutter so text stays on the heading's edge.
- **Footer:** Panel Grey band under a hairline, padding 24px × 14px, buttons right-aligned: one primary naming the action («Сохранить», «Доставить», «Выбрать», «Да»), then quiet «Отмена»/«Нет» (16px × 10px, min width 96px). A secondary tool action («Проверить соединение») sits quiet on the left.
- **Operation:** heading = operation title, state in muted words below («Выполняется · 42 %», «Готово», «Завершилось с ошибкой»), a 4px Ink-on-Hairline progress bar; on failure the bar gives way to the reason in a 2px Failure Red outline (`failure`) above the log. The log is data, so it is monospace (Cascadia Mono / Consolas, 12px) on a flat Panel Grey field. «Прервать» is quiet while running; «Закрыть» becomes primary when done.
- **Tree (FTP folders):** stock Fluent tree with selection as Selected Grey fill; a hairline status strip above the footer shows what is loading or the selected path.

### Release Notes («Что нового»)
A reading surface inside the dialog grammar: Title heading «Что нового» with a muted line under it («Обновлено до 2.0.1» / «Версия 2.0.1»), a hairline, then releases newest first, separated by hairlines. Each release opens with its version at 32px regular, tabular, -0.8 tracking (a smaller echo of the display number, never larger); `###` sections use the Title heading; bullets are an en dash in Muted in an 18px column; inline code is Cascadia Mono 13px. Text column capped at 640px for reading. One primary «Закрыть» in the footer.

### Stock Chrome
The parts Fluent draws itself carry the palette too (overridden by resource key in `App.axaml`, per theme):
- **Focus ring:** keyboard focus only; a 2px Ink outline with a 1px Ground gap inside (`SystemControlFocusVisualPrimaryBrush` / `SecondaryBrush`), square.
- **Scrollbars:** thumb Pending Grey, Muted under the pointer, Ink when pressed; track Panel Grey; square thumb. Collapsed to Fluent's thin line until hovered.
- **Inputs, checkboxes, text selection:** stock Fluent with square corners and Build Blue as the accent (focus underline, checked fill, selection), the one place the accent appears outside stage blocks.

## Do's and Don'ts

### Do:
- **Do** keep colour exclusively on stage blocks and chips: fill for done, 1px Pending outline for not yet, 2px Failure Red outline for failed.
- **Do** spell out every coded state in Russian words next to or inside the colour («№ 5 · проверка не прошла»).
- **Do** use one Ink primary button per screen whose label names the next step; put every other action in quiet hairline buttons or the menu.
- **Do** use square corners (0) and 1px hairlines on every surface, including stock Fluent controls and every new window.
- **Do** use tabular figures for versions, dates and counts.
- **Do** keep stage hues and Stage Ink identical in light and dark themes.

### Don't:
- **Don't** add shadows, gradients or rounded corners.
- **Don't** use stage hues for buttons, links, headings, selection or decoration.
- **Don't** mark selection with coloured bars or accent strips; selection is a Selected Grey fill.
- **Don't** lay stages out as a dashboard of equal cards or actions as a row of equal buttons.
- **Don't** place small uppercase labels above the display number or headings; the tracked caps label is only a list header in the left panel.
