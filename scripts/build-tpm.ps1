param(
    [string] $RoburInstallDir = "C:\Program Files\Topomatic Robur Road 16.0",
    [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $repoRoot "RoburPseudoCommands.csproj"
$packageJsonPath = Join-Path $repoRoot "package.json"
$pluginPath = Join-Path $repoRoot "RoburPseudoCommands.plugin"
$aliasesPath = Join-Path $repoRoot "aliases.json"
$iconsDir = Join-Path $repoRoot "Icons"
$distDir = Join-Path $repoRoot "dist"

$package = Get-Content -Raw -Encoding UTF8 $packageJsonPath | ConvertFrom-Json
$version = $package.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "package.json does not contain a version."
}

& dotnet build $projectPath -c $Configuration "-p:RoburInstallDir=$RoburInstallDir"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$frameworkDir = Join-Path $repoRoot (Join-Path "bin" (Join-Path $Configuration "net48"))
$dllPath = Join-Path $frameworkDir "RoburPseudoCommands.dll"
if (-not (Test-Path -LiteralPath $dllPath)) {
    throw "Build output not found: $dllPath"
}

if (-not (Test-Path -LiteralPath $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

$tpmPath = Join-Path $distDir "RoburPseudoCommands-$version.tpm"
if (Test-Path -LiteralPath $tpmPath) {
    Remove-Item -LiteralPath $tpmPath
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($tpmPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $zip.CreateEntry("icons/") | Out-Null

    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $packageJsonPath, "package.json") | Out-Null
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dllPath, "bin/RoburPseudoCommands.dll") | Out-Null
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $aliasesPath, "bin/aliases.json") | Out-Null
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $pluginPath, "plugins/RoburPseudoCommands.plugin") | Out-Null

    if (Test-Path -LiteralPath $iconsDir) {
        Get-ChildItem -LiteralPath $iconsDir -Filter "*.png" | Sort-Object Name | ForEach-Object {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, "icons/$($_.Name)") | Out-Null
        }
    }
}
finally {
    if ($zip) {
        $zip.Dispose()
    }
}

Get-Item -LiteralPath $tpmPath
