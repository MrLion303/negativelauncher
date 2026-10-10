# Negative Launcher multiplataforma

Esta aplicación Avalonia es la base de interfaz multiplataforma para trasladar Negative Launcher 0.1.1 a Linux y macOS. El launcher original de Windows se conserva en `Negative Client/Negative Client` y sigue siendo la referencia visual y funcional.

## Objetivo de esta rama

- Trabajar desde `fixes/0.1.1-discord-rich-presence`, que contiene el launcher actual.
- Mantener el código WPF original y sus cambios recientes intactos.
- Reutilizar los servicios compartidos en `NegativeLauncher.Core` para evitar duplicar lógica de cuentas, almacenamiento, instancias, catálogo y runtime.
- Portar cada pantalla y acción de forma deliberada, contrastándola con el original.
- Generar paquetes independientes mediante GitHub Actions para Windows x64, Linux x64, Linux ARM64, macOS Intel y macOS Apple Silicon.

## Compilación

El workflow `.github/workflows/compilar-prototipo-multiplataforma.yml` publica los artefactos autocontenidos y ejecuta las pruebas de lógica compartida.

- Windows: archivo ZIP con la publicación x64.
- Linux: archivo TAR.GZ con la publicación correspondiente a la arquitectura.
- macOS: archivo ZIP que contiene el bundle `.app` correspondiente a Intel o Apple Silicon.

## Estado real de paridad

La compilación multiplataforma no significa que el port esté terminado. La interfaz Avalonia sigue siendo una implementación en curso y todavía requiere comparar pantalla por pantalla la distribución, tipografía, fondos, controles, ventanas secundarias y estados del launcher WPF 0.1.1. También deben probarse en los sistemas de destino el inicio de sesión Microsoft, la instalación y actualización de modpacks, la autenticación, la preparación y el lanzamiento de Minecraft, y las operaciones de capturas.

No reemplazar ni simplificar la interfaz original para declarar el port terminado. La aceptación requiere que el launcher de Linux y macOS conserve la apariencia y el comportamiento del launcher actual de Windows, salvo las adaptaciones estrictamente necesarias por plataforma.
## Paridad con el launcher de Windows

La pantalla inicial de acceso se ha incorporado al prototipo multiplataforma con las dos rutas de la versión Windows: autenticación Microsoft y perfil sin conexión. Cuando no existe una sesión válida, el acceso se muestra como una capa completa y bloquea el cambio de tamaño; al completar el acceso, vuelve a habilitarse la ventana normal.

Antes de distribuirlo deben probarse en equipos reales: login Microsoft y retorno del navegador, descarga/actualización de modpacks, ejecución de Minecraft/Forge, consola, galería de capturas, preferencias y cierre/reinicio. La ejecución 273 terminó correctamente en los cinco destinos de publicación y en las pruebas de lógica compartida para Windows, Linux y macOS. Esto valida compilación y pruebas automatizadas, pero no sustituye las pruebas interactivas en equipos físicos. El lanzamiento normal ahora reutiliza los servicios originales para aplicar la selección de resource packs del modpack y preparar o desactivar la skin local según la cuenta, cancelando el inicio si falta un recurso requerido.


## Contadores globales y temas de temporada: primer pase

El prototipo ahora consulta el feed compartido de contadores globales, muestra los contadores activos y vigentes en la parte superior del área de contenido, actualiza la cuenta regresiva local cada segundo y vuelve a consultar el estado remoto periódicamente. Al visitar Galería o Ajustes, los avisos se muestran con opacidad reducida, como en la referencia WPF. La actualización conserva el último estado recibido si falla temporalmente la red, según el servicio compartido.

También se trasladó una primera capa de los temas estacionales automáticos, usando la fecha de Monterrey (UTC-6), los periodos definidos por el launcher original y la preferencia existente para activarlos o desactivarlos. Esta capa aplica tinte al fondo de Inicio, color al botón JUGAR y franjas decorativas superior e inferior. También incluye una vista previa de temas para desarrollador cuando están activados tanto el modo desarrollador como los temas festivos. Los contadores y los temas deben comprobarse visualmente en ejecución, además de compilar.


### Efectos estacionales animados

Los fuegos artificiales se activan el 31 de diciembre y el 1 de enero; los globos, el 27 de marzo. Los efectos se generan en una capa no interactiva y se detienen cuando se desactivan los temas o termina el periodo correspondiente. Es una implementación multiplataforma de primer pase, no una reproducción píxel por píxel del sistema WPF.


## Acceso protegido al modo desarrollador

El modo desarrollador no reutiliza las credenciales fijas del launcher WPF antiguo. Para habilitarlo, configura una contraseña en la variable de entorno `NEGATIVE_LAUNCHER_DEV_PASSWORD` antes de abrir el launcher. El nombre de usuario esperado es `admin` por defecto; puede cambiarse con `NEGATIVE_LAUNCHER_DEV_USERNAME`. Si la contraseña no está configurada, el modo desarrollador permanece deshabilitado.

En Windows, define las variables desde la configuración de variables de entorno de tu usuario y vuelve a iniciar el launcher. En Linux o macOS, expórtalas desde el entorno que utilices para ejecutar la aplicación. No guardes la contraseña en el repositorio, en los ajustes del launcher ni en argumentos de línea de comandos.

## Guardado automático de ajustes

Los cambios de memoria RAM, Java, argumentos personalizados, consola, cierre al iniciar, temas de temporada y catálogo de modpacks se guardan automáticamente tras una breve pausa al dejar de editar. La ruta de almacenamiento y el modo desarrollador conservan el botón **Guardar ajustes**, ya que implican operaciones adicionales y no deben aplicarse silenciosamente mientras se escribe.


## Limpieza avanzada de archivos no utilizados

En Ajustes, **Limpiar archivos no utilizados** analiza las versiones de Minecraft, runtimes de Java y copias antiguas por instancia, conserva los componentes requeridos por las instalaciones activas y solicita confirmación antes de eliminar las carpetas detectadas. La operación se bloquea mientras hay una instalación o actualización de modpack en curso y actualiza el cálculo de almacenamiento al terminar.

### Cancelación de Minecraft Vanilla

Durante la preparación de una versión Vanilla, el panel de desarrollador muestra un control para cancelar la operación. La cancelación utiliza el mismo token que el servicio de preparación; no interrumpe por la fuerza un proceso de Minecraft que ya se haya iniciado. Los errores al guardar los filtros o la versión seleccionada se muestran en la interfaz en lugar de quedar como excepciones no controladas.


## Publicar una release con paquetes de los cinco destinos

El flujo `.github/workflows/publicar-release-multiplataforma.yml` permite publicar manualmente los paquetes de una ejecución ya validada. No publica automáticamente cada commit ni crea una release durante la compilación normal.

1. Abre **Actions → Compilar base multiplataforma de Negative Launcher** y confirma que los ocho trabajos estén en verde.
2. Copia el ID numérico de esa ejecución y abre **Actions → Publicar release multiplataforma → Run workflow**.
3. Introduce ese ID y una etiqueta cuya versión coincida exactamente con `<Version>` del proyecto, por ejemplo `v0.1.1`. Marca `prerelease` solo para una compilación preliminar.
4. El flujo valida que la ejecución de origen haya terminado correctamente en esta rama, comprueba la versión real del ensamblado, descarga los cinco artefactos y calcula sus SHA-256 antes de crear la release.

La release contiene los nombres de archivo que reconoce el comprobador de actualizaciones: `NegativeLauncher-win-x64.zip`, `NegativeLauncher-linux-x64.tar.gz`, `NegativeLauncher-linux-arm64.tar.gz`, `NegativeLauncher-osx-x64.zip` y `NegativeLauncher-osx-arm64.zip`. El flujo rechaza etiquetas ya existentes y no permite publicar con una etiqueta que no coincida con la versión compilada.

**No publiques una release estable solo porque CI haya pasado.** Antes, confirma el retorno del acceso Microsoft y el inicio real de Minecraft en los sistemas disponibles.

## Validación y límites actuales (10 de octubre de 2026)

- La ejecución [282](https://github.com/MrLion303/negativelauncher/actions/runs/38083530559) completó con éxito los cinco paquetes (Windows x64, Linux x64, Linux ARM64, macOS Intel y macOS Apple Silicon) y las pruebas compartidas en Windows, Linux y macOS. La compilación de referencia de Windows también terminó correctamente en la ejecución [93](https://github.com/MrLion303/negativelauncher/actions/runs/38083530558).
- El flujo de acceso implementa inicio de sesión Microsoft interactivo y perfil sin conexión; los ajustes de cuentas permiten añadir, seleccionar, reautenticar y cerrar sesión. Falta validar el retorno del navegador y las sesiones reales en equipos de destino.
- La instalación de modpacks continúa con la preparación del runtime; el lanzamiento normal prepara los resource packs requeridos y la skin local según el perfil.
- La galería admite filtros, orden, visor, selección, eliminación y copia de una captura al portapapeles con clic derecho. Su lectura omite directorios inaccesibles y evita seguir enlaces simbólicos al recorrer las carpetas de capturas.
- La barra superior muestra la versión del ensamblado y consulta GitHub Releases para ofrecer un aviso enlazado cuando hay una versión más reciente. La comprobación es opcional y no bloquea el inicio si GitHub no responde.
- Siguen pendientes la comparación visual completa con WPF y las pruebas de extremo a extremo con Minecraft, Java, Forge y cada modpack en máquinas reales. No declarar el port terminado solo por una compilación satisfactoria.


## Dependencias y portapapeles

La galería usa la API tipada del portapapeles de Avalonia para copiar capturas como imágenes. Los paquetes de Avalonia se fijan en la misma versión estable y `Tmds.DBus.Protocol` se fija en `0.21.3`, versión con la corrección de seguridad para evitar restaurar la versión vulnerable transitiva `0.21.2`. La validación CI debe confirmar la resolución efectiva de dependencias y la compilación de los cinco destinos antes de considerar cerrada esta corrección.


La corrección del portapapeles, la actualización de Avalonia y el pin de seguridad de `Tmds.DBus.Protocol 0.21.3` quedaron validados por la ejecución 282. Esto confirma compilación y pruebas compartidas, pero no reemplaza las pruebas manuales de la aplicación y Minecraft en equipos de destino.


## Iconos nativos del escritorio

El paquete Linux registra el logo en el acceso de escritorio y copia el PNG a la jerarquía de iconos XDG del usuario. El bundle de macOS genera un archivo ICNS con varios tamaños a partir del logo del launcher y lo enlaza desde Info.plist. Los dos empaquetadores comprueban que los recursos existan antes de subir los artefactos.


En el tema de aniversario, los globos flotantes se pueden pulsar para generar una pequeña ráfaga de confeti. La capa de efectos solo recibe interacciones mientras se muestra ese tema; en los demás estados permanece transparente al ratón para no bloquear los controles del launcher.


## Fondo del modo desarrollador

Al abrir la página Minecraft Vanilla, el prototipo carga `Assets/minecraft_vanilla.png` en una capa independiente y conserva el oscurecimiento que mantiene legible el contenido. Al abandonar la página o desactivar el modo desarrollador, limpia la imagen y libera el bitmap; no toca el fondo de Inicio ni el fondo dinámico de la instancia seleccionada.
