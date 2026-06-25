# Контекст проекта-донора (Interactive Book)

UpdateManager **универсален**, но первым потребителем будет Interactive Book (Unity, Windows).
Здесь — как устроены обновления там, как образец интеграции. Это НЕ требования к инструменту,
а пример раскладки и параметров.

## Сервер обновлений

Адрес НЕ хардкодится — в Unity он в ассете
`Assets/Resources/UpdateModue/UpdateConnectionSettings.asset` (папка называется `UpdateModue`,
без `l` — так в проекте). Это `ScriptableObject` типа `ConnectionSettings` с тремя полями:

- `RemoteUrl` — напр. `http://195.144.244.42:180`
- `ProjectName` — напр. `InteractiveBook`
- `VersionFolder` — напр. `Output`

`GetURL()` собирает version-info URL: `{RemoteUrl}/{ProjectName}/{VersionFolder}/VersionInfo.info`.
В DEVELOPMENT_BUILD сначала ищет папку проекта на локальных дисках (для локального теста).

Патч раскладывается в эту же расшаренную по HTTP папку: `VersionInfo.info`, `RepairPatch/`
(файлы сжаты, расширение `.lzdat`), `InstallerPatch/`. → целевые значения для полей UpdateManager
(`ProjectName`, `VersionFolder`/output, `BaseDownloadURL`).

## Как готовится папка билда (build-хук)

`Assets/Scripts/Editor/VersionReminder.cs` (`VersionReminderBuildHook`) после Windows-сборки
главного приложения:

1. **Version holder**: пишет `InteractiveBook_vers.sptv` с числовой версией (всё после `-`
   отбрасывается — патчер понимает только `X.Y.Z`). Из `PlayerSettings.bundleVersion`.
2. **Удаляет `CameraConfig.json`** из `<Product>_Data/StreamingAssets/` — он специфичен для
   машины клиента и не должен попасть в манифест патча (иначе перезатрёт калибровку при обновлении).
   → кандидат в `IgnoredPaths`.
3. **Докладывает сменщик пароля** (`Password Changer.exe` + `_Data`) из `Build/CleanPassCh` в
   папку билда (без `StreamingAssets`).

Итог: `rootPath` для `PatchCreator` = папка готового билда после этого хука.

## Практические выводы для UpdateManager

- В пресет Interactive Book: `ProjectName=InteractiveBook`, `VersionFolder=Output`,
  `RemoteUrl/BaseDownloadURL` под сервер `http://195.144.244.42:180`.
- `IgnoredPaths` должен уметь исключать `CameraConfig.json` и `*output_log.txt`.
- Version holder `*.sptv` пишется отдельно (build-хуком Unity), к UpdateManager прямого
  отношения не имеет — но полезно помнить про числовой формат версии.
