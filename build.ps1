param(
    [switch]$SkipFfmpeg
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $Root "PewPewRecorder"
$FfmpegDir = Join-Path $ProjectDir "ffmpeg"
$FfmpegExe = Join-Path $FfmpegDir "ffmpeg.exe"

if (-not $SkipFfmpeg) {
    if (-not (Test-Path $FfmpegExe)) {
        Write-Host "Downloading FFmpeg..."
        New-Item -ItemType Directory -Force -Path $FfmpegDir | Out-Null

        $zipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
        $zipPath = Join-Path $env:TEMP "ffmpeg-win64.zip"

        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing

        $extractDir = Join-Path $env:TEMP "ffmpeg-extract"
        if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
        Expand-Archive -Path $zipPath -DestinationPath $extractDir

        $found = Get-ChildItem -Path $extractDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
        if (-not $found) { throw "ffmpeg.exe not found in downloaded archive." }

        Copy-Item $found.FullName $FfmpegExe -Force
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
        Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue

        Write-Host "FFmpeg installed to $FfmpegExe"
    } else {
        Write-Host "FFmpeg already present."
    }
}

Write-Host "Publishing PewPewRecorder..."
Push-Location $ProjectDir
try {
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
} finally {
    Pop-Location
}

$publishDir = Join-Path $ProjectDir "bin\Release\net8.0-windows\win-x64\publish"
$publishFfmpegDir = Join-Path $publishDir "ffmpeg"
New-Item -ItemType Directory -Force -Path $publishFfmpegDir | Out-Null
Copy-Item $FfmpegExe (Join-Path $publishFfmpegDir "ffmpeg.exe") -Force

$launcher = Join-Path $publishDir "Run Recorder.bat"
@(
    '@echo off',
    'cd /d "%~dp0"',
    'start "" "%~dp0PewPewRecorder.exe"'
) | Set-Content -Path $launcher -Encoding ASCII

Write-Host ""
Write-Host "Build complete!"
Write-Host "  App:    $(Join-Path $publishDir 'PewPewRecorder.exe')"
Write-Host "  FFmpeg: $(Join-Path $publishDir 'ffmpeg\ffmpeg.exe')"
