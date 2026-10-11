# Prueba rápida de Negative Launcher en Linux y macOS

Esta guía es para probar la rama multiplataforma antes de publicar una versión estable. Los artefactos proceden de GitHub Actions y **no equivalen todavía a una release firmada ni a una validación completa en equipos físicos**.

## 1. Descargar el paquete correcto

Abre [las ejecuciones de compilación](https://github.com/MrLion303/negativelauncher/actions/workflows/compilar-prototipo-multiplataforma.yml?query=branch%3Aportabilidad-fiel-0.1.1) y entra en la última ejecución que tenga todos los trabajos en verde. Descarga el artefacto de tu plataforma:

- Linux Intel/AMD de 64 bits: `NegativeLauncher-linux-x64`.
- Linux ARM de 64 bits: `NegativeLauncher-linux-arm64`.
- macOS Intel: `NegativeLauncher-osx-x64`.
- macOS Apple Silicon: `NegativeLauncher-osx-arm64`.

GitHub descarga cada artefacto como un ZIP. Descomprímelo una vez para obtener el paquete de instalación de la plataforma.

## 2. Linux

1. Extrae `NegativeLauncher-linux-x64.tar.gz` o `NegativeLauncher-linux-arm64.tar.gz` en una carpeta permanente de tu usuario.
2. Abre una terminal en esa carpeta.
3. Ejecuta:

```bash
chmod +x iniciar-negative-launcher.sh
./iniciar-negative-launcher.sh
```

4. Para integrar el launcher al menú de aplicaciones, ejecuta opcionalmente `./instalar-acceso-escritorio.sh`. El script instala el acceso para el usuario actual; no requiere ejecutarse como root.

Si la ventana no aparece, ejecuta el script desde una terminal y revisa el registro en `~/.local/share/NegativeClient/logs/negative-launcher.log` (o en `$XDG_DATA_HOME/NegativeClient/logs/negative-launcher.log` si tienes configurada esa variable).

## 3. macOS

1. Usa `NegativeLauncher-osx-x64.dmg` en un Mac Intel o `NegativeLauncher-osx-arm64.dmg` en un Mac con Apple Silicon. El ZIP contiene el mismo bundle `.app`.
2. Abre el DMG, arrastra **Negative Launcher.app** al acceso **Applications** y espera a que termine la copia.
3. Abre la aplicación desde Aplicaciones. Si macOS advierte que la app no está firmada o notarizada, verifica que descargaste el artefacto del repositorio correcto antes de autorizar su ejecución.
4. Si macOS bloquea el primer inicio, no desactives la seguridad del sistema globalmente. Consulta el mensaje de Gatekeeper y decide si confías en el archivo que descargaste.

El registro se guarda en `~/Library/Application Support/NegativeClient/logs/negative-launcher.log`.

## 4. Primer recorrido de prueba

Antes de intentar autenticación Microsoft, comprueba que la ventana aparece, puedes navegar entre Inicio, Galería y Ajustes, abrir los selectores de archivos/carpetas y cerrar/reabrir la aplicación.

Para probar la preparación de Minecraft Vanilla desde el modo desarrollador, abre la aplicación desde una terminal y configura una contraseña temporal para el modo de desarrollador.

Linux:

```bash
export NEGATIVE_LAUNCHER_DEV_USERNAME=admin
export NEGATIVE_LAUNCHER_DEV_PASSWORD='cambia-esta-clave-temporal'
./iniciar-negative-launcher.sh
```

macOS (desde la carpeta de la aplicación instalada):

```bash
export NEGATIVE_LAUNCHER_DEV_USERNAME=admin
export NEGATIVE_LAUNCHER_DEV_PASSWORD='cambia-esta-clave-temporal'
"/Applications/Negative Launcher.app/Contents/MacOS/NegativeLauncher.AvaloniaPrototype"
```

Después abre **Ajustes**, habilita el modo desarrollador con esa contraseña, entra en **DEV → Minecraft Vanilla**, selecciona una versión estable y prepárala. Esta prueba descarga Minecraft y Java, así que necesita acceso a Internet, espacio libre y permisos de escritura en tu carpeta de datos. Para quitar el acceso de desarrollador, desactívalo en Ajustes y cierra la aplicación.

La prueba Vanilla puede usar un perfil sin conexión para una comprobación local. Para entrar con una cuenta Microsoft en Linux/macOS, configura un Client ID público válido y autorizado siguiendo [AUTENTICACION_MICROSOFT_LINUX_MACOS.md](../AUTENTICACION_MICROSOFT_LINUX_MACOS.md). Un Client ID inventado o sin autorización no permitirá completar Xbox/Minecraft.

## 5. Qué reportar si falla

Comparte el sistema y arquitectura, el paso en el que falla y el registro de diagnóstico. Revisa el registro y elimina cualquier dato personal que no quieras compartir antes de publicarlo. No compartas contraseñas, tokens OAuth, archivos de sesión ni el archivo de cuentas guardadas.

## Alcance de esta prueba

La CI automatizada comprueba la compilación, el inicio de la ventana en Linux/macOS y la inicialización de datos. La autenticación Microsoft con una aplicación autorizada, la descarga real del runtime y el lanzamiento de Minecraft todavía deben comprobarse manualmente en cada plataforma. El runtime multiplataforma incorpora instaladores para Vanilla, Forge, NeoForge, Fabric y Quilt. La CI comprueba selección y compilación; el primer arranque real con cada loader y su modpack aún debe verificarse manualmente, especialmente en macOS.
