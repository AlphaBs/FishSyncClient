#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('windows', 'darwin', 'linux')]
    [string] $Os = $(if ($IsWindows) { 'windows' } elseif ($IsMacOS) { 'darwin' } else { 'linux' }),
    [ValidateSet('x64', 'aarch64')]
    [string] $Arch = $(if ([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq 'Arm64') { 'aarch64' } else { 'x64' }),
    [switch] $All
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'gui/gui.csproj'
[xml] $project = Get-Content -LiteralPath $projectPath -Raw
$version = [string]($project.Project.PropertyGroup.ClientVersion | Where-Object { $_ } | Select-Object -First 1)
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'gui.csproj의 ClientVersion은 숫자 세 부분으로 된 버전이어야 합니다.' }
$outputRoot = Join-Path $repoRoot 'artifacts/releases'
$workRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/package-work'))
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
[IO.Directory]::CreateDirectory($workRoot) | Out-Null
$targets = if ($All) {
    foreach ($targetOs in @('windows', 'darwin', 'linux')) {
        foreach ($targetArch in @('x64', 'aarch64')) {
            @{ Os = $targetOs; Arch = $targetArch }
        }
    }
} else { @{ Os = $Os; Arch = $Arch } }

foreach ($target in $targets) {
    $ridOs = @{ windows = 'win'; darwin = 'osx'; linux = 'linux' }[$target.Os]
    $ridArch = if ($target.Arch -eq 'aarch64') { 'arm64' } else { 'x64' }
    $rid = "$ridOs-$ridArch"
    $packageName = "FishSyncClientGui-$($target.Os)-$($target.Arch)-$version.zip"
    $stage = Join-Path $workRoot ([Guid]::NewGuid().ToString('N'))
    $publishDir = Join-Path $stage 'publish'
    $temporaryZip = Join-Path $stage $packageName
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    try {
        & dotnet publish $projectPath -c Release -r $rid --self-contained true `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:DebugType=None -p:DebugSymbols=false -o $publishDir
        if ($LASTEXITCODE -ne 0) { throw "게시 실패: $rid" }

        $binaryName = if ($target.Os -eq 'windows') { 'gui.exe' } else { 'gui' }
        $binaryPath = Join-Path $publishDir $binaryName
        $configPath = Join-Path $publishDir 'config/config.json'
        if (!(Test-Path -LiteralPath $binaryPath -PathType Leaf) -or
            !(Test-Path -LiteralPath $configPath -PathType Leaf)) { throw '실행 파일 또는 기본 설정 파일이 없습니다.' }
        $files = @(Get-ChildItem -LiteralPath $publishDir -Recurse -File)
        if ($files.Count -ne 2) { throw '단일 실행 파일과 config/config.json 이외의 게시 파일이 있습니다.' }

        # Only package the fresh publish output, never a working installation's settings/buckets.
        $zip = [IO.Compression.ZipFile]::Open($temporaryZip, [IO.Compression.ZipArchiveMode]::Create)
        try {
            $entry = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $binaryPath, $binaryName, [IO.Compression.CompressionLevel]::Optimal)
            if ($target.Os -ne 'windows') {
                # Preserve executable mode (regular file, 0755) for Unix extractors.
                $entry.ExternalAttributes = -2115174400 # signed Int32 representation of 0x81ED0000
            }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $configPath, 'config/config.json', [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        } finally { $zip.Dispose() }

        $destination = Join-Path $outputRoot $packageName
        Move-Item -LiteralPath $temporaryZip -Destination $destination -Force
        Get-Item -LiteralPath $destination | Select-Object FullName, Length
    } finally {
        $resolvedStage = [IO.Path]::GetFullPath($stage)
        $allowedPrefix = $workRoot + [IO.Path]::DirectorySeparatorChar
        if (!$resolvedStage.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw '빌드 임시 폴더가 허용된 경로 밖에 있습니다.'
        }
        if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
    }
}
