# Builds dist\BJManager.exe: one self-contained file, nothing to install beside it.
#
#   .\build.ps1                 build
#   .\build.ps1 -Version 1.2.0  build with that version stamped in (the exe's title and the
#                               number the self-updater compares against release tags)
param([string]$Version)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$args = @("publish", (Join-Path $root "src\BJManager.csproj"), "-c", "Release", "-o", (Join-Path $root "dist"), "-nologo")
if ($Version) { $args += "-p:Version=$Version" }
& dotnet @args
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
Get-Item (Join-Path $root "dist\BJManager.exe") | Select-Object FullName, Length, LastWriteTime
