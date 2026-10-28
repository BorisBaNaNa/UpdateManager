<#
.SYNOPSIS
  Выпуск новой версии UpdateManager: тесты → подъём версии → publish в Release/.

.DESCRIPTION
  0. Требует заметки выпуска docs/releases/X.Y.Z.md — они вшиваются в окно «Что нового» и идут в GitHub Release.
  1. Прогоняет тесты (до правки файлов — красные тесты ничего не меняют).
  2. Поднимает версию в Directory.Build.props: <Version> X.Y.Z и <FileVersion> X.Y.Z.0
     (FileVersion главного exe читает UpdateManager при сборке патча).
  3. Делает self-contained publish в Release/ (папка очищается — см. win-x64.pubxml).
  4. Пишет version holder Release/<Product>_vers.sptv с версией X.Y.Z.0: по нему клиент движка
     проверяет обновления только по версии (CheckForUpdates(true)), не сверяя файлы, а UpdateManager
     берёт из него версию патча. Файл входит в патч — у клиентов он обновляется вместе с версией.
  5. Сверяет FileVersion собранного UpdateManager.exe и наличие self-patcher'а в Release/SPPatcher
     (туда же его кладёт движок в патче — свежая установка его не докачивает) и печатает следующие шаги.

  Схема версий — SemVer из трёх чисел: появилась новая возможность → minor, иначе patch.
  Скрипт НИЧЕГО не коммитит и не пушит; патч собирается и доставляется в самом UpdateManager.

  -Publish — отдельный шаг, когда патч проверен, тег поставлен и запушен: ничего не собирает, а публикует
  GitHub Release текущей версии (Directory.Build.props) — заголовок «UpdateManager vX.Y.Z.0», текст
  docs/releases/X.Y.Z.md, архив Release/ как UpdateManager.vX.Y.Z.zip. Нужен GitHub CLI (gh) со входом.
  Перед публикацией сверяет: FileVersion в Release/ = версия, тег vX.Y.Z есть локально и на origin
  (тот же), выпуска с этим тегом на GitHub ещё нет.

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
.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File scripts/release.ps1 -Publish     # GitHub Release текущей версии
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
    [switch]$DryRun,

    # Не собирать, а опубликовать GitHub Release текущей версии (после проверки патча и push тега).
    [switch]$Publish
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

if ($Publish -and ($NoBump -or $Part -or $To -or $SkipTests)) {
    throw "-Publish ничего не собирает и публикует текущую версию — без разряда, -To, -NoBump и -SkipTests"
}
if (-not $Publish -and -not $NoBump -and -not $Part -and -not $To) {
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

# --- -Publish: GitHub Release уже собранной и проверенной версии --------------------------------
if ($Publish) {
    $versionText = Format-Version $current
    $tag = "v$versionText"
    $title = "$productName $tag.0"
    $NotesFile = Join-Path $RepoRoot "docs\releases\$versionText.md"
    $zipName = "$productName.$tag.zip"
    Write-Host "публикация: $title (тег $tag)" -ForegroundColor Cyan

    if (-not (Test-Path $NotesFile)) { throw "нет заметок выпуска $NotesFile" }
    if (-not (Test-Path $ReleaseExe)) { throw "нет $ReleaseExe — сначала соберите выпуск" }
    $built = (Get-Item $ReleaseExe).VersionInfo.FileVersion
    if ($built -ne "$versionText.0") {
        throw "в Release/ собрана версия $built, а в Directory.Build.props — $versionText. Пересоберите: release.ps1 -NoBump"
    }

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    $ghPath = if ($gh) { $gh.Source } else { Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe' }
    if (-not (Test-Path $ghPath)) { throw "нет GitHub CLI: winget install --id GitHub.cli, затем gh auth login" }

    # Тег должен быть и локально, и на origin — тот же самый: выпуск строится по тегу на сервере.
    $localTag = & git -C $RepoRoot rev-parse -q --verify "refs/tags/$tag"
    if ($LASTEXITCODE -ne 0) { throw "нет тега $tag — поставьте его на коммит, из которого собран Release/: git tag -a $tag -m `"$productName $versionText`"" }
    $remoteLine = & git -C $RepoRoot ls-remote origin "refs/tags/$tag"
    if ($LASTEXITCODE -ne 0) { throw "не удалось спросить origin о теге $tag" }
    if (-not $remoteLine) { throw "тега $tag нет на origin — запушьте: git push origin main $tag" }
    if (($remoteLine -split '\s+')[0] -ne $localTag) { throw "тег $tag на origin не совпадает с локальным" }

    # Перенаправленный stderr внешней команды при 'Stop' в Windows PowerShell 5.1 — прерывающая ошибка.
    $ErrorActionPreference = 'Continue'
    & $ghPath release view $tag *> $null
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -eq 0) { throw "выпуск $tag на GitHub уже есть — правьте его на сайте или: gh release upload $tag <файл> --clobber" }

    if ($DryRun) {
        Write-Host ""
        Write-Host "DryRun — план:" -ForegroundColor Yellow
        Write-Host "  архив $ReleaseDir → $zipName"
        Write-Host "  gh release create $tag --verify-tag --title `"$title`" --notes-file $NotesFile $zipName"
        return
    }

    # Архив — во временной папке вне репозитория; удаляется после отправки, удачной или нет.
    # Записи по одной: Compress-Archive и ZipFile.CreateFromDirectory в Windows PowerShell 5.1
    # пишут пути с обратной косой (SPPatcher\SelfPatcher.exe), а в zip разделитель — '/'.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zipDir = Join-Path ([IO.Path]::GetTempPath()) "UpdateManager-release-$tag"
    if (Test-Path $zipDir) { Remove-Item $zipDir -Recurse -Force }
    New-Item -ItemType Directory $zipDir | Out-Null
    try {
        $zip = Join-Path $zipDir $zipName
        Write-Host ""
        Write-Host "== Архив → $zip" -ForegroundColor Cyan
        $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in Get-ChildItem $ReleaseDir -Recurse -File) {
                $entry = $file.FullName.Substring($ReleaseDir.Length + 1).Replace('\', '/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        } finally {
            $archive.Dispose()
        }

        Write-Host ""
        Write-Host "== GitHub Release $tag" -ForegroundColor Cyan
        & $ghPath release create $tag --verify-tag --title $title --notes-file $NotesFile $zip
        if ($LASTEXITCODE -ne 0) { throw "gh release create не удался (вход: gh auth status)" }
    } finally {
        Remove-Item $zipDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Host "Опубликовано: $title" -ForegroundColor Green
    return
}

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

# Заметки выпуска вшиваются в программу (окно «Что нового») и идут в GitHub Release — пишутся ДО сборки.
$NotesFile = Join-Path $RepoRoot "docs\releases\$nextText.md"
if (-not (Test-Path $NotesFile)) {
    throw "нет заметок выпуска $NotesFile — напишите их до сборки (формат — как docs\releases\2.0.0.md): они вшиваются в окно «Что нового» и идут в GitHub Release"
}

if ($DryRun) {
    Write-Host ""
    Write-Host "DryRun — план:" -ForegroundColor Yellow
    if (-not $SkipTests) { Write-Host "  dotnet test $TestsProject" }
    if (-not $NoBump) { Write-Host "  Directory.Build.props: Version $nextText, FileVersion $fileVersion" }
    Write-Host "  dotnet publish $AppProject -p:PublishProfile=win-x64 → $ReleaseDir (папка очищается)"
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
$selfPatcher = Join-Path $ReleaseDir 'SPPatcher\SelfPatcher.exe'
if (-not (Test-Path $selfPatcher)) {
    throw "после publish нет $selfPatcher — без него самообновление не применит патч"
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
Write-Host "  Когда патч проверен — тег на коммите, из которого собран Release/: git tag -a v$nextText -m `"UpdateManager $nextText`""
Write-Host "  После push main и тега — GitHub Release с архивом: scripts/release.ps1 -Publish"
