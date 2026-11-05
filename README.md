# ELITE Launcher — Windows x64 starter

Primera base visual de ELITE en C# WPF. El objetivo de distribución es un instalador x64 que incluya el runtime .NET (publicación self-contained).

## Requisitos SOLO para compilar el proyecto
- Windows 10/11 x64
- .NET 8 SDK
- Inno Setup 6 (para generar el instalador)

El usuario final NO tendrá que instalar .NET si se publica con `publish.ps1` como está configurado.

## Compilar
Abre PowerShell en esta carpeta y ejecuta:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\publish.ps1
```

Para generar `ELITE-Setup-x64.exe`, abre `installer/ELITE.iss` con Inno Setup 6 y compílalo.

## Estado honesto de esta versión
- Incluye: ventana WPF, diseño oscuro inicial, navegación visual y estructura x64.
- Todavía no incluye: autenticación Microsoft, descarga de Java, instalación de Minecraft, motor de lanzamiento, mods ni optimizaciones aplicadas. El botón JUGAR está deshabilitado hasta integrar el motor.

## Próximas etapas
1. Motor de instalación y lanzamiento de Minecraft.
2. Gestión automática de runtimes Java compatibles por versión.
3. Inicio de sesión Microsoft mediante OAuth oficial.
4. Perfiles, mods y optimizaciones.
