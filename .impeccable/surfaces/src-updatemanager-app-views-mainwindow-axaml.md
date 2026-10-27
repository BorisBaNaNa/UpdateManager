---
version: 1
slug: "src-updatemanager-app-views-mainwindow-axaml"
primary_target: "src/UpdateManager.App/Views/MainWindow.axaml"
related_targets: ["src/UpdateManager.App/App.axaml"]
---

# Главное окно UpdateManager

Mode: Operate. Desktop window (Avalonia), min 720×520, light + dark + system theme. Russian UI.

## Job
A developer (or one of two colleagues) opens the tool every few days or weeks to ship a release. They have forgotten the step order; one look must say where the latest release stands and what to press next. Releases may carry several platforms at once (Windows now; Android later: build apk → files (version, license) → delivery → verification, no patch).

## Scope
Main window: start screen (no project: recent projects + create/open) and project screen (release catalog + selected release). Other windows (settings, delivery, FTP, operation log) are out of scope for this round and inherit the tokens only.

## Direction contract
THESIS: Every version is a catalog entry; its state is a coded strip of stage blocks that is always also spelled out in words. Refuses the category default of a dashboard of equal cards and a row of equal buttons.
OWN-WORLD: Matte ground (#111 dark / #EFEFED light), hairline rules, ink type in Golos Text; colour exists only as stage code — Build #2E86AB, Patch #F3B61F, Files #9B5DE5, Delivery #1FA39A, Verify #76B041; filled = done, hairline outline = pending, 2px red outline = failed. Square corners, no shadows.
STORY: The visitor reads the big version number, scans four blocks left to right, sees the first unfilled or red one, and presses the single primary button that names that step.
FIRST VIEWPORT: Left 280px catalog panel: project name, path, caps label, release rows (version, № / «новый», mini strip). Right: 72px version number with "Windows · выпуск N" beside it on the baseline; stage blocks in one row (2×2 when narrow); primary ink button + one-line status; hairline; label/value/action table (build folder, main exe, server, delivery, project folder).
FORM: Saville / Factory catalog sleeve (challenger, chosen by the user over the assigned "Формуляр"), position: challenger card; seed: d3508e2cf64afaf2
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Open decisions
- Android stages and platform tags are modelled (PlatformTrack per platform) but only Windows is produced today.
- Re-run actions (rebuild same version, redeliver) live in the «Выпуск» menu, not on the surface.
