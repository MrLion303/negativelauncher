# Auditoría técnica para portabilidad de Negative Launcher

Esta auditoría describe la estructura visible en la rama `portabilidad-multiplataforma`. No implica que las versiones Linux/macOS estén implementadas ni que se hayan ejecutado pruebas de compilación.

## Proyecto y tecnología

- Solución: `Negative Client/Negative Client.slnx`.
- Proyecto principal: `Negative Client/Negative Client/Negative Client.csproj`.
- Tecnología de interfaz: WPF/XAML (`UseWPF=true`) y destino `net10.0-windows`.
- Salida actual configurada: `WinExe`, `win-x64`, autocontenida y `PublishSingleFile=true`.
- Paquetes NuGet declarados: `CmlLib.Core 4.0.6`, `CmlLib.Core.Auth.Microsoft 3.3.1` y `CmlLib.Core.Installer.Forge 1.1.1`.
- Recursos gráficos presentes en `Assets`: `home_icon.png`, `minecraft_vanilla.png`, `negativeclient.ico`, `negativeclient_bg.png` y `negativeclient_logo.png`.

## Interfaz identificada

- `MainWindow.xaml` y `MainWindow.xaml.cs`, además de los parciales `MainWindow.Countdowns.cs`, `MainWindow.DeveloperMode.cs`, `MainWindow.DeveloperVanillaBackground.cs`, `MainWindow.EmbeddedSettings.cs`, `MainWindow.Gallery.cs`, `MainWindow.HolidayTheme.cs`, `MainWindow.ResourcePackSelection.cs` y `MainWindow.UiPolish.cs`.
- Ventanas auxiliares: `AddModpackWindow`, `DeveloperLoginWindow`, `GameConsoleWindow`, `InstanceOptionsWindow` y `SettingsWindow`.
- Ajustes: `SettingsPage.xaml` y `SettingsPage.xaml.cs`.
- La interfaz está implementada en XAML/WPF. Avalonia u otra UI multiplataforma no interpreta directamente estas ventanas como ventanas nativas multiplataforma; cada vista requerirá migración y pruebas visuales.

## Servicios identificados

- Cuentas y perfiles: `MicrosoftAccountService`, `OfflineAccountService`, `MinecraftNameLookupService`, `MinecraftSkinService` y `OfflineSkinService`.
- Juego y almacenamiento: `MinecraftGameService`, `InstanceService` y `SharedMinecraftStorageService`.
- Modpacks: `ModpackCatalogService` y `ModpackInstallerService`.
- Preferencias y estado: `LauncherPreferencesService` y `LauncherStateService`.
- Descargas y mantenimiento: `DownloadOperationController` y `UnusedFilesCleanupService`.
- Interfaz y contenido complementario: `GlobalCountdownService`, `GoogleDriveService`, `HolidayThemeService`, `ImageCacheService`, `ResourcePackSelectionService`, `ScreenshotArchiveService` y `ScreenshotGalleryService`.

## Bloqueos técnicos confirmados

1. **WPF limita la aplicación a Windows.** Cambiar `RuntimeIdentifier` o el workflow no vuelve compatible la UI con Linux/macOS.
2. **La interfaz y el código están acoplados.** Hay archivos code-behind muy extensos, por lo que una migración segura necesita inventariar eventos y estados, no solo traducir XAML.
3. **El lanzamiento del juego es sensible al sistema operativo.** Deben verificarse rutas, ejecutables Java, argumentos, bibliotecas nativas y reglas de versiones en cada plataforma.
4. **Las funciones no se pueden dar por conservadas solo porque compilen.** Autenticación, instalación, actualización, selección de recursos, galería, temas y consola necesitan pruebas funcionales.
5. **Un EXE único es una configuración de Windows.** Linux y macOS necesitan sus propios artefactos; en macOS hay que decidir y probar arquitecturas Intel y Apple Silicon.

## Orden de trabajo seguro

1. Mantener `main` como versión Windows de referencia.
2. Documentar los flujos y estados de cada pantalla y las dependencias de cada servicio.
3. Definir interfaces para operaciones de plataforma sin cambiar el comportamiento observable de Windows.
4. Introducir una UI multiplataforma en paralelo, migrando una ventana a la vez y comparándola con capturas de referencia.
5. Ejecutar compilaciones y pruebas en runners de Windows, Linux y macOS; no publicar como funcional una plataforma que solo compila.
6. Cuando todas las comprobaciones estén aprobadas, preparar artefactos separados para cada sistema.

## Estado actual

- [x] Crear rama de trabajo separada de `main`.
- [x] Registrar el plan general de portabilidad en `PORTABILIDAD.md`.
- [x] Identificar proyecto, paquetes, ventanas y servicios principales.
- [ ] Inventariar todos los eventos, diálogos, estados visuales y flujos de usuario.
- [ ] Diseñar e implementar adaptaciones de plataforma.
- [ ] Migrar y comparar cada pantalla sin pérdida de funciones.
- [ ] Compilar y probar las tres plataformas.

**No se han modificado las ventanas ni los servicios de la aplicación en esta fase.**
## Dependencias de interfaz de Windows encontradas en code-behind

- `MainWindow.xaml.cs` contiene numerosas llamadas a `MessageBox` (80 apariciones) y referencias directas a tipos de controles/medios WPF.
- `SettingsPage.xaml.cs` usa `Microsoft.Win32` y `OpenFileDialog` (4 apariciones), además de `MessageBox` (40 apariciones) y tipos WPF.
- `SettingsWindow.xaml.cs` usa `Microsoft.Win32` y `OpenFileDialog` (2 apariciones), además de `MessageBox` (40 apariciones) y tipos WPF.
- `GameConsoleWindow.xaml.cs` usa `Dispatcher` para coordinar actualizaciones de interfaz.
- Estos puntos deberán pasar por adaptaciones equivalentes de diálogo, hilo de UI y selección de archivos en el destino multiplataforma. Sustituirlos por mensajes o controles simplificados incumpliría el requisito de conservar la experiencia.

Este inventario inicial es estático y no sustituye las pruebas de ejecución ni la revisión exhaustiva de todos los eventos XAML.
## Avance posterior de la fase de inventario

- Se añadieron referencias concretas a las ventanas principales y auxiliares y a los servicios de cuentas, juego, instancias, modpacks, preferencias, capturas, temas y recursos.
- Se encontraron dependencias directas de WPF y cuadros de diálogo de Windows en la ventana principal y las pantallas de ajustes; su reemplazo deberá mantener los flujos equivalentes, no suprimirlos.
- Se añadió `.github/workflows/validar-base-windows.yml`, que permite ejecutar una compilación de referencia de Windows x64 en la rama de portabilidad y conservar el EXE y un manifiesto como artefactos.
- La compilación de referencia Windows se ejecutó correctamente en GitHub Actions (run 37879170640). La publicación de Windows x64 del prototipo Avalonia también terminó correctamente (run 37879534658).

## Próxima tarea

Antes de migrar vistas, completar una matriz pantalla por pantalla: elementos visuales, eventos, estados, diálogos, datos persistentes y servicios invocados. Esa matriz será el criterio para detectar regresiones durante el portado.
## Matriz de funciones y hallazgos de servicios

Se añadió `MATRIZ_FUNCIONAL_PORTABILIDAD.md` como checklist de regresión para las siete ventanas identificadas y los servicios principales. Incluye eventos XAML localizados, estados y flujos de prueba, dependencias técnicas y orden recomendado de migración.

Hallazgos adicionales de la inspección directa de servicios:
- `InstanceService.cs` consulta `Environment.SpecialFolder.ApplicationData`; esa decisión de ruta debe pasar a una política de directorios por plataforma antes de habilitar Linux/macOS.
- `ScreenshotGalleryService.cs` usa `System.Windows.Media.Imaging`, así que incluso un servicio que parece de datos depende de WPF para decodificar/procesar imágenes. Hay que desacoplar esa parte o sustituirla por una implementación multiplataforma con pruebas de miniaturas/galería.
- En la muestra inspeccionada, `MinecraftGameService`, `MicrosoftAccountService`, `ModpackInstallerService`, `ResourcePackSelectionService`, `GoogleDriveService`, `LauncherPreferencesService` y `SharedMinecraftStorageService` no muestran referencias directas a `System.Windows` ni a cuadros de diálogo de Windows. Esto es una señal favorable, pero no demuestra por sí sola compatibilidad de todos sus paquetes y rutas con Linux/macOS.
- La UI actual tiene un acoplamiento fuerte entre XAML y code-behind. El portado debe preservar el comportamiento por flujo y no confiar en una conversión mecánica de XAML.

## Decisión de seguridad para la migración

No se ha modificado la interfaz ni la lógica de ejecución de Minecraft. No conviene introducir todavía cambios en los servicios productivos: antes hay que elegir y validar la tecnología de UI multiplataforma con un prototipo separado, manteniendo WPF como referencia funcional. Los artefactos Linux/macOS solo se deben anunciar después de compilar y probar en esos entornos.
## Prototipo aislado de Avalonia y compilación de referencia

- Se creó `Prototipo Avalonia/` con una ventana de prueba en tema oscuro y estructura de escritorio. Es deliberadamente independiente de la solución WPF; no se modificaron sus pantallas ni sus servicios.
- Se añadió `.github/workflows/compilar-prototipo-multiplataforma.yml` para publicar el prototipo en `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64` y `osx-arm64`.
- La ejecución de referencia Windows `37879170640` terminó con conclusión `success` en el workflow `Validar base Windows de Negative Launcher`.
- La ejecución de prueba Avalonia `37879534658` se inició; al registrar esta actualización todavía estaba ejecutándose/encolada. No se declara éxito de las cinco plataformas hasta revisar la conclusión final y los artefactos.

Este prototipo valida únicamente la base técnica de interfaz y publicación. No contiene autenticación, gestión de instancias, modpacks, launcher de Minecraft ni equivalencia visual con la aplicación WPF. La decisión de migración depende de builds correctos y pruebas de ejecución reales.

## Estado de compilación del prototipo multiplataforma

Ejecución de GitHub Actions: https://github.com/MrLion303/negativelauncher/actions/runs/37879534658

Artefactos publicados y visibles en la última consulta:
- Windows x64: correcto.
- Linux x64: correcto.
- Linux ARM64: correcto.
- macOS ARM64: correcto.
- macOS x64: sigue en cola; no se debe contar como completado hasta que termine.

Estos artefactos corresponden únicamente al prototipo Avalonia aislado. No incluyen todavía la lógica real de Negative Launcher ni prueban el arranque en máquinas físicas de cada sistema.

Se añadió ARQUITECTURA_MULTIPLATAFORMA.md para definir las capas de UI, servicios y adaptadores de plataforma, además de reglas de compatibilidad de datos y criterios de aceptación. Es una guía para ejecutar la migración sin eliminar funciones ni modificar la versión Windows de referencia.

## Próxima etapa de implementación

El siguiente paso técnico es comenzar la extracción controlada de lógica compartible, después de agregar pruebas de caracterización. No conviene copiar los servicios al prototipo sin revisar sus dependencias: ScreenshotGalleryService usa WPF para imágenes, y los servicios de almacenamiento dependen de rutas centralizadas en InstanceService. Esas dependencias deben resolverse explícitamente y compararse contra el comportamiento de Windows antes de conectar las pantallas.


## Actualización de extracción compartida

En la rama `portabilidad-multiplataforma`, `GlobalCountdownService` y sus modelos se trasladaron a `NegativeLauncher.Core`; el servicio utiliza `LauncherPaths.DefaultLauncherRoot` para la caché y los diagnósticos, sin depender de `InstanceService` ni de WPF.

También se trasladó la lógica de archivo de capturas a `ScreenshotArchiveStore`. El servicio WPF conserva una capa adaptadora con la API y la ruta predeterminada anteriores. Las pruebas compartidas cubren el archivado, la recuperación de metadatos y la limpieza al eliminar la última imagen. `ScreenshotGalleryService` sigue dependiendo de `System.Windows.Media.Imaging`, así que la galería visual aún necesita una implementación multiplataforma para miniaturas y visor.

Estas extracciones reducen dependencias de WPF, pero no completan la portabilidad de la interfaz, la autenticación Microsoft ni el lanzamiento de Minecraft.
