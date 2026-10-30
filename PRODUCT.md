# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

Impeccable has no desktop value; `web` is the nearest (no native mobile design language). The real platform is a Windows 10/11 x64 desktop app: Avalonia 12 (Fluent theme) on .NET 10, XAML views, desktop windows, menus, modal dialogs, native file/folder pickers. No browser, no responsive breakpoints; web-only checks (CSS, DOM detector, live mode) do not apply.

## Users

The developer who wrote the tool plus a couple of colleagues: a small team that ships updates for its own Windows applications (Interactive Book, UpdateManager itself). Everyone knows what a build, patch, version and FTP are; nobody needs the domain explained, but everyone forgets the exact sequence between releases.

## Product Purpose

Turns a finished application build into a published update: pick the build folder → version is read from the main exe → SimplePatchTool builds repair/installer/incremental patches → the patch is delivered to a folder or FTP server → "Проверить" downloads it back over HTTP the way a real client would. It replaces manual work in the SimplePatchTool editor window inside Unity.

Success: a release goes out without a forgotten step and without a broken `BaseDownloadURL` (the classic failure: the update check succeeds, but downloading patch files fails on clients).

## Positioning

A universal, standalone front end for the SimplePatchTool engine: works for any project (folder of a build in, published patch out), and closes the loop by verifying the published patch end-to-end instead of trusting the upload.

## Operating Context

- Used rarely, release by release (every few days or weeks). Between uses the operator forgets the order of steps and the settings, so the screen itself must show where the project stands: last build source, detected version, versions already built, what was delivered and when.
- One update project = one engine folder (`Settings.xml`, `Versions/`, `Output/`, `SelfPatcher/`, `Other/`) plus `updatemanager.project.xml`. Projects are opened like in an IDE, with a recent list.
- Delivery goes to a LAN FTP server or a local folder; verification goes over public HTTPS. The router has no NAT loopback, so verification from inside the LAN times out — the UI must survive slow or failing network calls with clear, human-readable errors.
- Long operations (patch build, FTP upload, verification) run with a log and progress.
- UpdateManager updates itself through the same engine (silent check on start, manual check in Help).

## Capabilities and Constraints

- Create / open / recent projects; project settings editor for engine `Settings.xml` (name, `BaseDownloadURL`, patch types, ignored paths, compression, diff quality).
- Build patch: build folder → version from `FileVersion` (or `<Product>_vers.sptv`) → copy to `Versions/<version>` → engine `GeneratePatch()`. Versions only go up.
- Deliver: folder or FTP (credentials stored per user, password DPAPI-encrypted, FTP browser to pick the remote folder); delivery time recorded per version.
- Verify: real client download into a temp folder.
- RSA signing (key generation, signing manifests, uploading the private key to colleagues over FTP).
- Self-update of UpdateManager.
- Light, dark and "as in Windows" theme.
- Platforms: Windows and Android are switched on per project; a release can carry both or either. Android has no patches: build folder (apk, manifest; built outside) → optionally sign files by mask with the project's RSA key (`<file>.sig`, the same key as Windows patches) → deliver to a channel folder (`AndroidPatch` inside the patch folder) → verify by downloading the channel and checking bytes and signatures. Android-only projects read `versionName` from JSON manifests in the build folder and subfolders, falling back to manual entry when no unambiguous valid version exists. With both platforms enabled, Android uses the Windows version. A quiet rescan button beside the release catalog heading rereads both build sources after a rebuild. Windows keeps build → patch → deliver → verify.
- Delivery mirrors: after a successful upload the target holds exactly the new set (the Android channel folder inside the Windows patch folder is kept).
- Terminology stays the engine's and the team's: патч, билд, версия, доставка, проверка, `BaseDownloadURL`, repair/installer/incremental.

## Brand Commitments

- Interface language: Russian.
- No logo, brand colors or identity exist; any visual identity may be defined from scratch.
- Must support light and dark themes plus "as in Windows".

## Evidence on Hand

- Working app with all screens: `src/UpdateManager.App/Views/` (main window, project settings, delivery, FTP connection, FTP browser, operation log, message, prompt).
- No screenshots, testimonials or metrics exist; do not invent them.

## Product Principles

- Show state, not just actions: after weeks away, one look at the main window tells what was built, what was delivered, and what to do next.
- Prevent the expensive mistake before it ships: block or warn on empty/wrong `BaseDownloadURL`, version regressions, and missing delivery settings, and make verification the natural last step.
- Errors in plain language: the cause in one or two lines, details available but not in the way.
- Tool, not showcase: precision and calm over decoration; every control earns its place.
