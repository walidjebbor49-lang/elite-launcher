# Script de publicación automatizada para ELITE Launcher
Write-Host "Iniciando compilacion de ELITE Launcher..." -ForegroundColor Cyan

# Busca el archivo .csproj de forma automática dentro del repositorio
$csproj = Get-ChildItem -Recurse -Filter "ELITE.Launcher.csproj" | Select-Object -First 1

if (-not $csproj) {
    Write-Error "No se encontró el archivo ELITE.Launcher.csproj en el repositorio."
    exit 1
}

Write-Host "Proyecto encontrado en: $($csproj.FullName)" -ForegroundColor Green

# Ejecuta la publicación self-contained apuntando a la ruta encontrada
dotnet publish $csproj.FullName -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64

if ($LASTEXITCODE -ne 0) { 
    throw 'No se pudo publicar ELITE.' 
}

Write-Host "¡Publicación completada con éxito!" -ForegroundColor Green
