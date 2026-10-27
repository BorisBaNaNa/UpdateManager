# lib/

DLL движка SimplePatchTool, скопированы из Unity-проекта Interactive Book
(`Assets/Plugins/SimplePatchTool/DLL/`). Собраны под CLR 2.0, в .NET 10 грузятся как есть
(проверяет `tests/UpdateManager.Tests/EnginePipelineTests`).

- `SimplePatchTool.dll` — `SimplePatchToolCore` (`ProjectManager`, `PatchCreator`, `PatchUpdater`,
  клиентский `SimplePatchTool`, `CompressionFormat`, `PatchResult`, `VersionCode`).
- `SimplePatchToolSecurity.dll` — `SimplePatchToolSecurity` (`XMLSigner`, `SecurityUtils` / RSA);
  внутри использует `SignedXml` → пакет `System.Security.Cryptography.Xml`.

Подключаются по HintPath (`src/UpdateManager.Core`, а также App/Tests, где типы движка нужны напрямую).
FluentFTP берётся из NuGet (в 1.x лежал здесь же под net472).

Апстрим: https://github.com/yasirkula/SimplePatchTool
