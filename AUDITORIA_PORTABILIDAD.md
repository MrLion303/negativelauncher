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
- La API de GitHub todavía no muestra ejecuciones de Actions para esa rama; por tanto, la nueva compilación está configurada, pero no se ha verificado que haya corrido ni que termine con éxito.

## Próxima tarea

Antes de migrar vistas, completar una matriz pantalla por pantalla: elementos visuales, eventos, estados, diálogos, datos persistentes y servicios invocados. Esa matriz será el criterio para detectar regresiones durante el portado.