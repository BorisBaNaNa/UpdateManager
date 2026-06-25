# lib/

DLL движка SimplePatchTool, скопированы из Unity-проекта Interactive Book
(`Assets/Plugins/SimplePatchTool/DLL/`):

- `SimplePatchTool.dll` — `SimplePatchToolCore` (`PatchCreator`, `PatchUpdater`, `PatchUtils`,
  `CompressionFormat`, `PatchResult`).
- `SimplePatchToolSecurity.dll` — `SimplePatchToolSecurity` (`XMLSigner`, `SecurityUtils` / RSA).

Подключить как Reference (HintPath) в `UpdateManager.csproj`. Перед этим проверить совместимость
целевого фреймворка — DLL собраны для Unity (вероятно .NET Standard 2.0).

Апстрим: https://github.com/yasirkula/SimplePatchTool
