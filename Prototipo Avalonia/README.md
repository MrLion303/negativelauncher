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

Antes de distribuirlo deben probarse en equipos reales: login Microsoft y retorno del navegador, descarga/actualización de modpacks, ejecución de Minecraft/Forge, consola, galería de capturas, preferencias y cierre/reinicio. La compilación automática ya ha pasado en los cinco destinos en la ejecución 197; los cambios posteriores de preparación de resource packs y skins se están validando en una nueva ejecución. El lanzamiento normal ahora reutiliza los servicios originales para aplicar la selección de resource packs del modpack y preparar o desactivar la skin local según la cuenta, cancelando el inicio si falta un recurso requerido.


## Contadores globales y temas de temporada: primer pase

El prototipo ahora consulta el feed compartido de contadores globales, muestra los contadores activos y vigentes en la parte superior del área de contenido, actualiza la cuenta regresiva local cada segundo y vuelve a consultar el estado remoto periódicamente. Al visitar Galería o Ajustes, los avisos se muestran con opacidad reducida, como en la referencia WPF. La actualización conserva el último estado recibido si falla temporalmente la red, según el servicio compartido.

También se trasladó una primera capa de los temas estacionales automáticos, usando la fecha de Monterrey (UTC-6), los periodos definidos por el launcher original y la preferencia existente para activarlos o desactivarlos. Esta capa aplica tinte al fondo de Inicio, color al botón JUGAR y franjas decorativas superior e inferior. También incluye una vista previa de temas para desarrollador cuando están activados tanto el modo desarrollador como los temas festivos. Los contadores y los temas deben comprobarse visualmente en ejecución, además de compilar.


### Efectos estacionales animados

Los fuegos artificiales se activan el 31 de diciembre y el 1 de enero; los globos, el 27 de marzo. Los efectos se generan en una capa no interactiva y se detienen cuando se desactivan los temas o termina el periodo correspondiente. Es una implementación multiplataforma de primer pase, no una reproducción píxel por píxel del sistema WPF.


## Guardado automático de ajustes

Los cambios de memoria RAM, Java, argumentos personalizados, consola, cierre al iniciar, temas de temporada y catálogo de modpacks se guardan automáticamente tras una breve pausa al dejar de editar. La ruta de almacenamiento y el modo desarrollador conservan el botón **Guardar ajustes**, ya que implican operaciones adicionales y no deben aplicarse silenciosamente mientras se escribe.


## Limpieza avanzada de archivos no utilizados

En Ajustes, **Limpiar archivos no utilizados** analiza las versiones de Minecraft, runtimes de Java y copias antiguas por instancia, conserva los componentes requeridos por las instalaciones activas y solicita confirmación antes de eliminar las carpetas detectadas. La operación se bloquea mientras hay una instalación o actualización de modpack en curso y actualiza el cálculo de almacenamiento al terminar.
