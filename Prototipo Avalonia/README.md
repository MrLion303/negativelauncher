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
