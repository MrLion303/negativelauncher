# Prueba rápida de Negative Launcher en Linux y macOS

Esta guía corresponde a la rama `portabilidad-fiel-0.1.1`. La rama Windows `fixes/0.1.1-discord-rich-presence` sigue siendo la referencia visual y funcional.

## 1. Descargar el launcher

Abre [las ejecuciones de compilación Linux y macOS](https://github.com/MrLion303/negativelauncher/actions/workflows/compilar-prototipo-multiplataforma.yml?query=branch%3Aportabilidad-fiel-0.1.1) y entra en una ejecución reciente que haya terminado con todos los trabajos en verde.

GitHub Actions publica solo estos dos artifacts:

- **Linux x64 y ARM64:** `NegativeLauncher-Linux`, con `NegativeLauncher-linux-universal.tar.gz`.
- **macOS Intel y Apple Silicon:** `NegativeLauncher-macOS`, con `NegativeLauncher-macos-universal.zip`.

## 2. Iniciar en Linux

Descomprime el artifact de GitHub y extrae `NegativeLauncher-linux-universal.tar.gz` en una carpeta permanente. Desde esa carpeta ejecuta:

```bash
./iniciar-negative-launcher.sh
```

El script selecciona automáticamente x64 o ARM64. Para añadir el launcher al menú de aplicaciones de tu usuario, ejecuta:

```bash
./instalar-acceso-escritorio.sh
```

No requiere permisos de administrador. Conserva juntas las carpetas `linux-x64`, `linux-arm64`, `Assets` y los dos scripts.

El registro predeterminado está en `~/.local/share/NegativeClient/logs/negative-launcher.log`. Si `XDG_DATA_HOME` apunta a una ruta absoluta, el launcher lo usa como raíz de datos.

## 3. Iniciar en macOS

Descomprime el artifact de GitHub y extrae `NegativeLauncher-macos-universal.zip`. Mueve `NegativeLauncher-macos-universal.app` a `Aplicaciones` y ábrela. El mismo paquete contiene los binarios Intel x64 y Apple Silicon ARM64.

La compilación de CI lleva una firma ad hoc y no tiene certificado Developer ID ni notarización. Gatekeeper puede mostrar un aviso o impedir el primer inicio; esta compilación sirve para pruebas y todavía no es el paquete firmado para distribución pública. El registro está en:

```text
~/Library/Application Support/NegativeClient/logs/negative-launcher.log
```

## 4. Recorrido básico

En ambos sistemas comprueba:

1. La ventana abre y permanece abierta.
2. Puedes navegar por Inicio, Galería, Instancias y Ajustes.
3. Los selectores de archivo y carpeta abren los diálogos del sistema.
4. Los ajustes se conservan al cerrar y volver a abrir el launcher.
5. La consola, la galería y el cierre/reinicio se comportan correctamente.

La CI valida inicio y navegación automatizados, pero no sustituye las pruebas interactivas en una computadora física.

## 5. Probar Minecraft Vanilla en modo desarrollador

Para una prueba local opcional, abre una terminal en la carpeta del launcher y configura credenciales temporales:

```bash
export NEGATIVE_LAUNCHER_DEV_USERNAME=admin
export NEGATIVE_LAUNCHER_DEV_PASSWORD='cambia-esta-clave-temporal'
./iniciar-negative-launcher.sh
```

En Ajustes, habilita el modo desarrollador con esa contraseña, entra en **DEV → Minecraft Vanilla**, selecciona una versión estable y prepara la instalación. Necesitas conexión a Internet, espacio libre y permisos de escritura en la carpeta de datos. Desactiva el modo desarrollador y cierra la aplicación al terminar.

No reutilices una contraseña real. La prueba Vanilla puede usar un perfil sin conexión; algunas funciones y servidores requieren una cuenta premium.

## 6. Inicio de sesión Microsoft

El Client ID de la aplicación Microsoft ya está integrado de fábrica en el proyecto para las compilaciones Linux x64/ARM64 y macOS; no hace falta configurar `NEGATIVE_LAUNCHER_MSAL_CLIENT_ID` en GitHub Actions ni pedirle al jugador que introduzca un ID. Basta con pulsar **Iniciar sesión con Microsoft** e iniciar sesión con la cuenta. El acceso sin conexión sigue disponible. Si Microsoft devuelve un error de registro, redirect URI o autorización para Xbox/Minecraft, consulta [la guía de autenticación Linux/macOS](AUTENTICACION_MICROSOFT_LINUX_MACOS.md). No añadas secretos, tokens ni contraseñas al repositorio.

El retorno del navegador, una cuenta real y el lanzamiento de Minecraft deben probarse manualmente en Linux y macOS. CI no tiene una cuenta Minecraft para completar ese flujo.

## Alcance

La aceptación final requiere comparar visualmente el launcher con Windows y probar las operaciones reales de Minecraft, OAuth, modpacks y capturas en equipos Linux y Mac. Para distribuir la app macOS también se necesita completar Developer ID y la notarización de Apple.
