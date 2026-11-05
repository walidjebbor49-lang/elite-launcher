$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src/ELITE.Launcher/ELITE.Launcher.csproj'
$out = Join-Path $PSScriptRoot 'publish/win-x64'
 dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $out
if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar ELITE.' }
Write-Host "ELITE publicado en $out"
