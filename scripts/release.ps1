<#
.SYNOPSIS
  Выпуск новой версии UpdateManager: тесты → подъём версии → publish в Release/.

.DESCRIPTION
  1. Прогоняет тесты (до правки файлов — красные тесты ничего не меняют).
  2. Поднимает версию в Directory.Build.props: <Version> X.Y.Z и <FileVersion> X.Y.Z.0
     (FileVersion главного exe читает UpdateManager при сборке патча).
  3. Делает self-contained publish в Release/ (папка очищается — см. win-x64.pubxml).
  4. Копирует встроенный self-patcher в Release/SPPatcher — туда же его кладёт движок в патче,
     поэтому только что выпущенная версия не скачивает его при первой проверке.
  5. Пишет version holder Release/<Product>_vers.sptv с версией X.Y.Z.0: по нему клиент движка
     проверяет обновления только по версии (CheckForUpdates(true)), не сверяя файлы, а UpdateManager
     берёт из него версию патча. Файл входит в патч — у клиентов он обновляется вместе с версией.
  6. Сверяет FileVersion собранного UpdateManager.exe и печатает следующие шаги.

  Схема версий — SemVer из трёх чисел: появилась новая возможность → minor, иначе patch.
  Скрипт НИЧЕГО не коммитит и не заливает — патч собирается и доставляется в самом UpdateManager.

.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File scripts/release.ps1 patch
.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File scripts/release.ps1 minor
.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File scripts/release.ps1 -To 2.1.0
.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File scripts/release.ps1 -NoBump      # пересобрать текущую версию
.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File scripts/release.ps1 patch -DryRun   # показать план, ничего не трогая
#>
[CmdletBinding()]
param(
    # Какой разряд поднять. Игнорируется, если задан -To или -NoBump.
    [Parameter(Position = 0)]
    [ValidateSet('major', 'minor', 'patch')]
    [string]$Part,

    # Явная версия X.Y.Z вместо вычисления по разряду.
    [string]$To,

    # Не поднимать версию — собрать текущую (например, первый выпуск или пересборка).
    [switch]$NoBump,

    # Не гонять тесты (только если они уже прогнаны на этом коммите).
    [switch]$SkipTests,

    # Показать план, файлы не менять и ничего не собирать.
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# UTF-8 в консоли — иначе кириллица выходит кракозябрами.
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding
$OutputEncoding = New-Object System.Text.UTF8Encoding

$RepoRoot = Split-Path -Parent $PSScriptRoot
$PropsPath = Join-Path $RepoRoot 'Directory.Build.props'
$AppProject = Join-Path $RepoRoot 'src\UpdateManager.App\UpdateManager.App.csproj'
$TestsProject = Join-Path $RepoRoot 'tests\UpdateManager.Tests\UpdateManager.Tests.csproj'
$ReleaseDir = Join-Path $RepoRoot 'Release'
$ReleaseExe = Join-Path $ReleaseDir 'UpdateManager.exe'
$VersionHolderPostfix = '_vers.sptv' # PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX движка
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function ConvertTo-Version([string]$text) {
    if ($text -notmatch '^\s*(\d+)\.(\d+)\.(\d+)\s*$') { return $null }
    return [version]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3])
}

function Format-Version([version]$v) { return "$($v.Major).$($v.Minor).$($v.Build)" }

if (-not $NoBump -and -not $Part -and -not $To) {
    throw "укажи разряд (patch/minor/major), явную версию через -To или -NoBump. Пример: powershell.exe -ExecutionPolicy Bypass -File scripts/release.ps1 patch"
}

# --- Текущая и новая версия ---------------------------------------------------------------------
$props = [IO.File]::ReadAllText($PropsPath)
if ($props -notmatch '<Version>([^<]+)</Version>') { throw "в $PropsPath нет <Version>" }
$current = ConvertTo-Version $Matches[1]
if (-not $current) { throw "<Version>$($Matches[1])</Version> не похожа на X.Y.Z" }

# Имя проекта обновлений = Product (по нему же SelfIdentity узнаёт себя) → <Product>_vers.sptv.
if ($props -notmatch '<Product>([^<]+)</Product>') { throw "в $PropsPath нет <Product>" }
$productName = $Matches[1].Trim()
$VersionHolder = Join-Path $ReleaseDir ($productName + $VersionHolderPostfix)

if ($NoBump) {
    $next = $current
} elseif ($To) {
    $next = ConvertTo-Version $To
    if (-not $next) { throw "-To '$To' не похожа на версию X.Y.Z" }
} else {
    switch ($Part) {
        'major' { $next = [version]::new($current.Major + 1, 0, 0) }
        'minor' { $next = [version]::new($current.Major, $current.Minor + 1, 0) }
        'patch' { $next = [version]::new($current.Major, $current.Minor, $current.Build + 1) }
    }
}

if (-not $NoBump -and $next -le $current) {
    throw "версия $(Format-Version $next) не больше текущей $(Format-Version $current) — номера идут только вперёд (движок строит инкрементальные патчи по возрастанию)"
}

$nextText = Format-Version $next
$fileVersion = "$nextText.0"
Write-Host "текущая: $(Format-Version $current)" -ForegroundColor Cyan
Write-Host "выпуск:  $nextText (FileVersion $fileVersion)" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host ""
    Write-Host "DryRun — план:" -ForegroundColor Yellow
    if (-not $SkipTests) { Write-Host "  dotnet test $TestsProject" }
    if (-not $NoBump) { Write-Host "  Directory.Build.props: Version $nextText, FileVersion $fileVersion" }
    Write-Host "  dotnet publish $AppProject -p:PublishProfile=win-x64 → $ReleaseDir (папка очищается)"
    Write-Host "  $ReleaseDir\SelfPatcher\* → $ReleaseDir\SPPatcher\"
    Write-Host "  $VersionHolder ← $fileVersion"
    return
}

# --- Запущенный из репозитория UpdateManager держит файлы сборки --------------------------------
# Из Release/ — publish не сможет очистить папку; из src\…\bin (dotnet run / IDE) — тесты не соберутся.
$running = Get-Process -Name 'UpdateManager' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($RepoRoot, [StringComparison]::OrdinalIgnoreCase) } |
    Where-Object { -not $SkipTests -or $_.Path.StartsWith($ReleaseDir, [StringComparison]::OrdinalIgnoreCase) }
if ($running) {
    $paths = ($running | ForEach-Object { "  PID $($_.Id): $($_.Path)" }) -join "`n"
    throw "закройте UpdateManager, запущенный из репозитория, — он держит файлы сборки:`n$paths"
}

# --- 1. Тесты -----------------------------------------------------------------------------------
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "== Тесты" -ForegroundColor Cyan
    & dotnet test $TestsProject --nologo
    if ($LASTEXITCODE -ne 0) { throw "тесты не прошли — версия не поднята, ничего не собрано" }
}

# --- 2. Версия ----------------------------------------------------------------------------------
if (-not $NoBump) {
    $props = $props -replace '<Version>[^<]+</Version>', "<Version>$nextText</Version>"
    $props = $props -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$fileVersion</FileVersion>"
    [IO.File]::WriteAllText($PropsPath, $props, $Utf8NoBom)
    Write-Host ""
    Write-Host "Directory.Build.props: $nextText / $fileVersion" -ForegroundColor Green
}

# --- 3. Publish ---------------------------------------------------------------------------------
Write-Host ""
Write-Host "== Publish → $ReleaseDir" -ForegroundColor Cyan
& dotnet publish $AppProject -p:PublishProfile=win-x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "publish не удался. Версия в Directory.Build.props уже $nextText — после исправления запустите с -NoBump."
}

# --- 3a. Self-patcher в SPPatcher\ --------------------------------------------------------------
# Движок кладёт self-patcher в патч под SPPatcher\ (PatchParameters.SELF_PATCHER_DIRECTORY). Без этой
# папки в выпуске свежая установка при первой проверке «обновляется» на те же 2.0.0 ради self-patcher'а.
$BundledSelfPatcher = Join-Path $ReleaseDir 'SelfPatcher'
$ClientSelfPatcher = Join-Path $ReleaseDir 'SPPatcher'
if (-not (Test-Path (Join-Path $BundledSelfPatcher 'SelfPatcher.exe'))) {
    throw "после publish нет $BundledSelfPatcher\SelfPatcher.exe"
}
New-Item -ItemType Directory -Force -Path $ClientSelfPatcher | Out-Null
Copy-Item -Path (Join-Path $BundledSelfPatcher '*') -Destination $ClientSelfPatcher -Recurse -Force
Write-Host ""
Write-Host "Self-patcher: $ClientSelfPatcher" -ForegroundColor Green

# --- 4. Version holder --------------------------------------------------------------------------
# Ровно версия, без BOM и перевода строки — так его читает движок (и VersionDetector при сборке патча).
[IO.File]::WriteAllText($VersionHolder, $fileVersion, $Utf8NoBom)
Write-Host ""
Write-Host "Version holder: $VersionHolder ← $fileVersion" -ForegroundColor Green

# --- 5. Сверка ----------------------------------------------------------------------------------
if (-not (Test-Path $ReleaseExe)) { throw "после publish нет $ReleaseExe" }
$built = (Get-Item $ReleaseExe).VersionInfo.FileVersion
if ($built -ne $fileVersion) {
    throw "FileVersion собранного exe = $built, ожидалась $fileVersion"
}
$holder = [IO.File]::ReadAllText($VersionHolder)
if ($holder -ne $fileVersion) {
    throw "в $VersionHolder записано '$holder', ожидалась $fileVersion"
}

Write-Host ""
Write-Host "Готово: UpdateManager $nextText в $ReleaseDir" -ForegroundColor Green
Write-Host ""
Write-Host "Дальше:"
Write-Host "  1. UpdateManager → проект обновлений самого UpdateManager; источник билда — $ReleaseDir"
Write-Host "  2. «Создать патч» (версия $fileVersion) → «Доставить патч» → «Проверить»."
if (-not $NoBump) {
    Write-Host "  3. Закоммитить подъём версии: git commit -am `"Версия $nextText`""
}
