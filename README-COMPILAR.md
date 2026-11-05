# Obtener ELITE.exe y ELITE-Setup-x64.exe sin instalar herramientas en tu PC

Este proyecto todavía es una **base visual**. Aún no instala ni inicia Minecraft, no administra Java y no incluye inicio de sesión Microsoft. El flujo siguiente solo compila la aplicación existente.

## Compilación automática en GitHub

1. Inicia sesión o crea una cuenta en GitHub.
2. Crea un repositorio nuevo llamado `elite-launcher`.
3. Sube **todo el contenido** de esta carpeta al repositorio (incluida la carpeta oculta `.github`). La carpeta `.github/workflows/build-windows.yml` es necesaria.
4. Abre la pestaña **Actions** del repositorio.
5. Selecciona **Build ELITE for Windows x64** y pulsa **Run workflow**. También se ejecuta al subir cambios a `main`.
6. Cuando termine correctamente, abre la ejecución y descarga el artefacto `ELITE-Windows-x64` en **Artifacts**.
7. Dentro encontrarás `ELITE.exe` y `ELITE-Setup-x64.exe`.

No necesitas instalar .NET, Gradle, Java ni Inno Setup en tu computadora para esta compilación: GitHub Actions usa una máquina Windows temporal.

## Requisitos para usuarios finales

La publicación usa .NET 8 self-contained y single-file para incluir el runtime de .NET. Eso no incluye Java ni los componentes de Minecraft. Esos elementos deberán implementarse en etapas posteriores antes de poder jugar desde ELITE.
