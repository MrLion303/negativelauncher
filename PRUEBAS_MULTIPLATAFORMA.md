# Guía de prueba rápida de Negative Launcher para Linux y macOS

Esta guía acompaña los paquetes de la rama `portabilidad-fiel-0.1.1`. No declara el port terminado: sirve para probarlo de forma repetible y reportar fallos que requieran una corrección.

## Descargar el paquete correcto

Abre [las ejecuciones de compilación multiplataforma](https://github.com/MrLion303/negativelauncher/actions/workflows/compilar-prototipo-multiplataforma.yml). Elige la ejecución más reciente que esté completada en verde y descarga el artefacto de tu plataforma:

- `NegativeLauncher-linux-x64`: Linux Intel/AMD de 64 bits.
- `NegativeLauncher-linux-arm64`: Linux ARM64.
- `NegativeLauncher-osx-x64`: macOS Intel; contiene ZIP y DMG.
- `NegativeLauncher-osx-arm64`: macOS Apple Silicon; contiene ZIP y DMG.
- `NegativeLauncher-win-x64`: Windows x64, útil como referencia.

Los artefactos de Actions son temporales. Para probar el parche, descarga siempre los de una sola ejecución y no mezcles archivos de commits distintos.

## Primera prueba en Linux

1. Extrae el TAR.GZ completo en una carpeta permanente.
2. Desde una terminal, entra en esa carpeta y ejecuta `./iniciar-negative-launcher.sh`. Conserva la terminal abierta durante la prueba, porque puede mostrar errores de bibliotecas de escritorio.
3. Abre Ajustes y confirma que la carpeta de datos tenga una ruta Linux normal. Si escribes manualmente una ruta, se aceptan `~`, `$HOME` y `${HOME}`.
4. Prueba crear/instalar una instancia de prueba y preparar Minecraft Vanilla o Forge. No uses una instancia importante durante la primera prueba.
5. Para añadir un acceso de escritorio, ejecuta `./instalar-acceso-escritorio.sh`.

## Primera prueba en macOS

1. Abre el DMG correspondiente a tu arquitectura.
2. Arrastra `Negative Launcher.app` al acceso `Applications` y ejecuta la aplicación desde Aplicaciones.
3. La aplicación todavía no está firmada ni notarizada. Si macOS bloquea su apertura, no desactives protecciones del sistema a nivel global; autorízala solo tras verificar que descargaste el artefacto esperado.
4. Abre Ajustes y comprueba la carpeta de datos. Debe usar `~/Library/Application Support/NegativeClient` de forma predeterminada.

## Autenticación y Minecraft

- Para probar la interfaz sin configurar OAuth, empieza con el perfil sin conexión y una instancia desechable. Esta modalidad permite comprobar la interfaz y pruebas locales, pero no autentica contra los servicios de Minecraft ni habilita servidores que exijan cuenta premium.
- Para una cuenta Microsoft en Linux/macOS, configura un Client ID público autorizado para Xbox/Minecraft siguiendo [la guía de autenticación](AUTENTICACION_MICROSOFT_LINUX_MACOS.md). El repositorio no contiene un Client ID inventado.
- En la versión multiplataforma están implementados Minecraft Vanilla y Forge. Fabric y NeoForge siguen pendientes; no los uses como criterio de prueba aprobado.
- No publiques una versión estable basándote solo en CI. Hace falta completar inicio de sesión real y una sesión de juego en hardware de destino.

## Lista de aceptación

Marca cada resultado como aprobado, fallido o no probado:

- [ ] La aplicación se abre sin terminal con errores y muestra la pantalla de acceso.
- [ ] Se puede entrar al launcher mediante el perfil sin conexión.
- [ ] La navegación Inicio, Instancias, Modpacks, Galería y Ajustes no se bloquea.
- [ ] Los ajustes básicos se guardan y sobreviven al reinicio.
- [ ] La ruta de datos funciona con espacios y no sobrescribe otras carpetas al migrarse.
- [ ] Una instancia Vanilla de prueba descarga/prepara sus recursos y abre Minecraft.
- [ ] Una instancia Forge de prueba instala y lanza con su versión esperada.
- [ ] La consola muestra salida sin bloquear el juego; cerrar su ventana no mata Minecraft de forma accidental.
- [ ] La galería lee y elimina capturas de una instancia de prueba.
- [ ] Limpiar caché, temporales o archivos no utilizados conserva instancias activas y datos externos.
- [ ] En macOS, el DMG se abre, el bundle se copia a Aplicaciones y la app vuelve a iniciar.
- [ ] Si se configura MSAL, la autenticación vuelve correctamente del navegador y Minecraft inicia con la sesión real.

## Cómo compartir un fallo

Desde **Ajustes → Diagnóstico → Abrir carpeta de registros**, abre la carpeta que contiene `negative-launcher.log`. El registro guarda información del sistema, arquitectura, versión de .NET y excepciones. No se envía automáticamente.

Adjunta el registro solo después de revisar y eliminar nombres de usuario, rutas privadas u otros datos que no quieras compartir. Incluye también:

- sistema operativo y arquitectura;
- nombre del artefacto y SHA/ID de la ejecución de Actions;
- pasos exactos para reproducirlo;
- comportamiento esperado y comportamiento real.

Ubicaciones predeterminadas de los datos:

- Windows: `%APPDATA%\\NegativeClient`
- Linux: `${XDG_DATA_HOME:-~/.local/share}/NegativeClient`
- macOS: `~/Library/Application Support/NegativeClient`

Los registros se guardan dentro de `logs` en la carpeta predeterminada del launcher; si no se puede escribir ahí, se utiliza una carpeta temporal local.
