# Prueba rápida de Negative Launcher en Linux

Esta guía corresponde a la rama `portabilidad-fiel-0.1.1`. Durante esta fase se generan y prueban únicamente paquetes Linux. La rama Windows `fixes/0.1.1-discord-rich-presence` sigue siendo la referencia visual y funcional; el port de macOS se retomará después.

## 1. Descargar el paquete

Abre [las ejecuciones de compilación Linux](https://github.com/MrLion303/negativelauncher/actions/workflows/compilar-prototipo-multiplataforma.yml?query=branch%3Aportabilidad-fiel-0.1.1) y entra en la ejecución más reciente que haya terminado con todos los trabajos en verde.

Descarga el artefacto adecuado:

- **Linux Intel/AMD de 64 bits:** `NegativeLauncher-linux-x64`.
- **Linux ARM de 64 bits:** `NegativeLauncher-linux-arm64`.

GitHub entrega cada artefacto como un ZIP. Descomprímelo; dentro encontrarás el archivo TAR.GZ de la publicación Linux. Extrae ese archivo en una carpeta permanente de tu usuario. No ejecutes el launcher desde una carpeta temporal ni separes el ejecutable de los archivos que lo acompañan.

## 2. Iniciar el launcher

Abre una terminal dentro de la carpeta extraída y ejecuta:

```bash
chmod +x iniciar-negative-launcher.sh
./iniciar-negative-launcher.sh
```

Para instalar un acceso en el menú de aplicaciones de tu escritorio, puedes ejecutar:

```bash
./instalar-acceso-escritorio.sh
```

El instalador crea el acceso para el usuario actual y no necesita `sudo`.

## 3. Diagnóstico si no abre

Ejecuta el script desde una terminal para conservar los mensajes de error. El registro predeterminado está en:

```text
~/.local/share/NegativeClient/logs/negative-launcher.log
```

Si tienes definida la variable `XDG_DATA_HOME`, revisa en su lugar:

```text
$XDG_DATA_HOME/NegativeClient/logs/negative-launcher.log
```

Si reportas un problema, indica tu distribución, versión del sistema, arquitectura (`x64` o `ARM64`), el paso exacto que falla y el mensaje de error. Revisa el registro antes de compartirlo y elimina rutas privadas o cualquier otro dato personal. No compartas contraseñas, tokens OAuth ni archivos de sesión.

## 4. Primer recorrido de prueba

Antes de autenticarte, comprueba lo siguiente:

1. La ventana abre y no se cierra inesperadamente.
2. Puedes navegar entre Inicio, Galería y Ajustes.
3. Los botones de la barra superior funcionan.
4. Los selectores de archivo y carpeta abren el diálogo del sistema.
5. El launcher se puede cerrar y volver a iniciar.
6. Los ajustes se conservan después de reiniciar la aplicación.

La compilación y el inicio gráfico de CI son pruebas automáticas; no equivalen a la validación interactiva de todas las funciones en una computadora física.

## 5. Probar Minecraft Vanilla en modo desarrollador

Para una prueba local opcional, abre una terminal en la carpeta de publicación y configura credenciales temporales para el modo desarrollador:

```bash
export NEGATIVE_LAUNCHER_DEV_USERNAME=admin
export NEGATIVE_LAUNCHER_DEV_PASSWORD='cambia-esta-clave-temporal'
./iniciar-negative-launcher.sh
```

En el launcher, abre Ajustes, habilita el modo desarrollador con esa contraseña, entra en **DEV → Minecraft Vanilla**, selecciona una versión estable y prepara la instalación. Esta prueba requiere conexión a Internet, espacio libre y permisos de escritura en la carpeta de datos. Desactiva el modo desarrollador y cierra la aplicación al terminar.

No reutilices una contraseña real. La prueba Vanilla puede usar un perfil sin conexión; para acceder a servidores o funciones que requieran una cuenta premium, tendrás que configurar una aplicación Microsoft válida y autorizada.

## 6. Inicio de sesión Microsoft

El inicio de sesión real requiere un Client ID público de Microsoft Entra autorizado para Xbox/Minecraft. Sigue la guía [AUTENTICACION_MICROSOFT_LINUX_MACOS.md](AUTENTICACION_MICROSOFT_LINUX_MACOS.md) para registrar y configurar la aplicación. El Client ID no es una contraseña; no añadas secretos, tokens ni contraseñas al repositorio.

La autenticación con una cuenta real, el retorno del navegador y el lanzamiento de Minecraft deben comprobarse manualmente en Linux. CI no dispone de una cuenta Minecraft de prueba y no puede confirmar por sí sola ese flujo.

## Alcance

En esta fase se publican `linux-x64` y `linux-arm64`. La paridad visual y funcional con Windows sigue en desarrollo; macOS queda fuera del flujo de compilación y se retomará en una fase posterior.
