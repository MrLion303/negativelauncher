# Matriz funcional para la portabilidad de Negative Launcher

Esta matriz registra la superficie visible y los puntos de entrada funcionales que deben conservarse durante la migración. La versión WPF actual sigue siendo la referencia de comportamiento. No se debe eliminar una pantalla ni sustituir una función por un marcador provisional para conseguir que compile.

## Criterios de aceptación globales

- [ ] Conservar las funciones actuales, los flujos de navegación y el orden de las operaciones.
- [ ] Conservar textos, iconos, imágenes, paleta, tamaños, espaciados, estados visuales y temas.
- [ ] Conservar los datos existentes del usuario y la compatibilidad con las instalaciones de Minecraft ya creadas.
- [ ] Mantener los flujos de cuenta Microsoft y cuenta sin conexión, incluidos sus mensajes de error.
- [ ] Mantener la instalación, configuración, actualización, verificación y lanzamiento de instancias.
- [ ] Mantener pausa/reanudación y cancelación de operaciones cuando estén disponibles. Parcial: modpacks permiten pausar/reanudar/detener y la preparación Vanilla ya permite cancelar; faltan las pruebas de aceptación y comprobar el resto de operaciones.
- [ ] Mantener la consola de juego, las preferencias, las capturas, los temas festivos, los modpacks y las herramientas de desarrollador.
- [ ] Mantener la misma semántica de rutas y archivos, adaptando la ubicación al sistema operativo sin sobrescribir ni mover silenciosamente los datos antiguos.
- [ ] No considerar terminada una pantalla hasta comprobar sus estados vacío, cargando, éxito, error y acciones secundarias aplicables.
- [x] Construir artefactos independientes para Windows x64, Linux x64/ARM64 y macOS Intel/Apple Silicon mediante GitHub Actions. Pendiente: pruebas manuales en equipos reales.

## Matriz de pantallas

### 1. Ventana principal — `MainWindow.xaml` y archivos parciales

**Responsabilidad:** pantalla central y navegación principal del launcher.

Superficie identificada en XAML:
- Barra superior personalizada: logo, controles de ventana y arrastre.
- Navegación principal: Inicio, galería y accesos de cuenta/configuración.
- Catálogo/listado de instancias o modpacks, fondos y elementos visuales asociados.
- Acciones para agregar modpack, abrir opciones de instancia y jugar.
- Indicadores de operaciones con acciones de pausar/reanudar y detener.
- Galería de capturas: filtros, selección, visor, anterior/siguiente, actualización y eliminación.

Eventos XAML identificados:
- `HomeButton_Click`, `GalleryButton_Click`
- `AccountQuickButton_Click`, `AccountSettingsButton_Click`, `QuickAddAccountButton_Click`
- `AddModpackButton_Click`, `InstanceOptionsButton_Click`, `PlayButton_Click`
- `OperationPauseResumeButton_Click`, `OperationStopButton_Click`
- `GalleryCloseViewerButton_Click`, `GalleryDeleteSelectedButton_Click`, `GalleryNextButton_Click`, `GalleryPreviousButton_Click`, `GalleryRefreshButton_Click`, `GallerySelectionModeButton_Click`, `GalleryViewerDeleteButton_Click`, `GalleryFilters_Changed`, `ScreenshotCard_Click`
- `DeveloperVersionComboBox_SelectionChanged`, `DeveloperVersionFilter_Changed`
- `Close_Click`, `Maximize_Click`, `Minimize_Click`, `TitleBar_MouseLeftButtonDown`

Archivos parciales relacionados que deben revisarse y migrarse:
- `MainWindow.Countdowns.cs`
- `MainWindow.DeveloperMode.cs`
- `MainWindow.DeveloperVanillaBackground.cs`
- `MainWindow.EmbeddedSettings.cs`
- `MainWindow.Gallery.cs`
- `MainWindow.HolidayTheme.cs`
- `MainWindow.ResourcePackSelection.cs`
- `MainWindow.UiPolish.cs`

**Pruebas de aceptación:** navegación entre secciones; selección y lanzamiento de instancia; agregar modpack; opciones de instancia; operaciones largas y sus controles; galería con filtros, visor y borrado; tema estacional; funciones de desarrollador; redimensionado/minimizado/cierre de ventana.

### 2. Ajustes integrados — `SettingsPage.xaml`

Superficie identificada:
- Pestañas de ajustes generales y cuentas.
- Memoria RAM y selección de Java.
- Java automático, ruta de Java personalizada y argumentos personalizados.
- Cierre del launcher al iniciar el juego y visibilidad de la consola.
- Ubicación de instalaciones y datos de almacenamiento.
- Temas festivos y modo de desarrollador.
- Guardado automático, limpieza de archivos no usados y estado de preferencias.
- Gestión de cuentas Microsoft y perfiles sin conexión; selección, alta, uso, reautenticación, cierre de sesión y piel sin conexión.

Eventos identificados:
- `NormalSettingsTabButton_Click`, `AccountsTabButton_Click`
- `RamSlider_ValueChanged`, `AutomaticJavaCheckBox_Changed`, `CustomJavaArgumentsEnabledCheckBox_Changed`
- `BrowseInstallationsLocationButton_Click`, `BrowseJavaButton_Click`
- `HolidayThemesCheckBox_Changed`, `DeveloperModeText_MouseLeftButtonUp`
- `AutoSaveText_Changed`, `AutoSaveToggle_Changed`, `CleanupUnusedFilesButton_Click`
- `AccountsListBox_SelectionChanged`, `AddAccountButton_Click`, `UseAccountButton_Click`, `ReauthenticateButton_Click`, `SignOutButton_Click`
- `PremiumAccountModeRadioButton_Checked`, `OfflineAccountModeRadioButton_Checked`, `OfflineUsernameTextBox_TextChanged`, `SaveOfflineProfileButton_Click`
- `BrowseOfflineSkinButton_Click`, `RemoveOfflineSkinButton_Click`, `AutomaticJavaCheckBox_Changed`

**Pruebas de aceptación:** persistencia tras reinicio; validación de rutas; selección de Java y argumentos; cambios de RAM; perfiles premium/sin conexión; estados de autenticación; selector de archivos; limpieza con confirmación y resultado; ajustes accesibles desde la ventana principal.

### 3. Ventana de ajustes — `SettingsWindow.xaml`

Contiene una superficie de ajustes y cuenta similar a la página integrada, con controles para cuentas, RAM, Java, ubicación de instalaciones, consola y limpieza, además de la barra de ventana propia.

**Punto por resolver antes de migrar:** determinar con llamadas reales cuándo se usa esta ventana independiente y cuándo `SettingsPage`. No eliminar ninguna de las dos hasta demostrar que una es código muerto y verificar que todas sus acciones están cubiertas por la otra.

### 4. Agregar modpack — `AddModpackWindow.xaml`

- Entrada de código de modpack.
- Instalación y presentación de errores.
- Cierre y cancelación.
- Envío mediante teclado desde `CodeTextBox_KeyDown`.

**Pruebas:** código válido/inválido, error de red, operación en curso, cancelación, cierre y envío con teclado.

### 5. Inicio de sesión de desarrollador — `DeveloperLoginWindow.xaml`

- Campos de usuario y contraseña.
- Acción de inicio de sesión y cierre de ventana.

**Pruebas:** credenciales válidas e inválidas, mensajes de error, campos vacíos, teclado y cierre.

### 6. Consola del juego — `GameConsoleWindow.xaml`

- Salida de texto del juego, estado de consola y acción para limpiar.
- Minimizar/cerrar y tratamiento del evento de cierre.

**Pruebas:** salida incremental, gran volumen de texto, juego terminado, minimización y cierre según las preferencias configuradas.

### 7. Opciones de instancia — `InstanceOptionsWindow.xaml`

- Verificación de integridad.
- Comprobación de actualizaciones.
- Eliminación de instancia y cierre.

**Pruebas:** instancia válida/incompleta, errores de acceso o red, confirmación de borrado, cancelación y actualización del listado tras finalizar.

## Inventario de servicios que deben seguir funcionando

| Servicio | Área que hay que preservar |
|---|---|
| `MinecraftGameService` | Preparación y lanzamiento de Minecraft, Java y loaders |
| `MicrosoftAccountService` | Autenticación y gestión de cuenta Microsoft |
| `OfflineAccountService` | Perfiles locales sin conexión |
| `InstanceService` | Instancias, rutas y datos locales |
| `ModpackCatalogService` | Catálogo de modpacks |
| `ModpackInstallerService` | Instalación de modpacks |
| `LauncherPreferencesService` | Preferencias persistentes |
| `LauncherStateService` | Estado del launcher |
| `DownloadOperationController` | Control de operaciones largas |
| `GlobalCountdownService` | Contadores |
| `GoogleDriveService` | Integración con Google Drive |
| `ResourcePackSelectionService` | Selección y gestión de paquetes de recursos |
| `SharedMinecraftStorageService` | Almacenamiento compartido de Minecraft |
| `UnusedFilesCleanupService` | Limpieza de archivos no usados |
| `ScreenshotArchiveService`, `ScreenshotGalleryService` | Archivo y galería de capturas |
| `MinecraftSkinService`, `OfflineSkinService` | Pieles de usuario |
| `MinecraftNameLookupService` | Consulta de nombres |
| `ImageCacheService` | Caché de imágenes |
| `HolidayThemeService` | Temas estacionales |

Otros componentes transversales: `MainWindow.HolidayTheme.cs`, `MainWindow.ResourcePackSelection.cs`, `MainWindow.DeveloperMode.cs`, `MainWindow.Countdowns.cs`, `MainWindow.Gallery.cs` y `MainWindow.EmbeddedSettings.cs`.

## Riesgos técnicos conocidos

1. **Interfaz:** WPF y sus controles/plantillas XAML no se ejecutan de forma nativa en Linux o macOS. Cambiar únicamente el identificador de runtime no hace multiplataforma la aplicación.
2. **Diálogos y ventanas:** hay usos de `MessageBox`, `Microsoft.Win32.OpenFileDialog` y APIs de ventana/Dispatcher. Deben mapearse a equivalentes de la nueva UI conservando resultados, cancelación y mensajes.
3. **Lógica acoplada a la UI:** los code-behind principales son grandes y conectan directamente eventos, controles y servicios. El portado requiere pruebas de regresión por flujo, no una conversión automática masiva.
4. **Minecraft/Java/loaders:** deben verificarse las capacidades de CmlLib y del instalador Forge en Linux y macOS, así como reglas de rutas, nombres de archivos, permisos y selección de runtime.
5. **Datos existentes:** las rutas actuales usan carpetas especiales de Windows. La migración debe elegir directorios apropiados por sistema y documentar cualquier importación de datos, sin borrar ni mover automáticamente los datos existentes.
6. **Paquetes nativos y publicación:** autenticación, librerías nativas y empaquetado deben validarse por cada RID de destino. No declarar soporte estable hasta ejecutar builds y pruebas reales en los tres sistemas.

## Orden de ejecución propuesto

1. [x] Inventariar pantallas, eventos visibles y servicios principales.
2. [x] Añadir workflow de compilación de referencia Windows en la rama de portabilidad.
3. [x] Confirmar compilación de referencia Windows en GitHub Actions.
4. [x] Confirmar la estrategia de UI multiplataforma mediante un prototipo Avalonia aislado, sin sustituir la versión WPF.
5. [ ] Portar y validar cada pantalla y ventana auxiliar conservando el comportamiento.
6. [ ] Migrar los parciales restantes de la ventana principal, incluidos contadores globales y temas estacionales.
7. [x] Generar builds autocontenidos para los cinco destinos mediante GitHub Actions.
8. [ ] Probar instalación, actualización, autenticación y lanzamiento de Minecraft en equipos reales y comparar visualmente con Windows.

**Estado:** esta matriz es un inventario de trabajo, no una afirmación de que las pruebas ya se hayan realizado. El proyecto WPF original continúa sin cambios en esta rama aparte de los documentos y workflows de portabilidad.

## Hallazgos de la comparación directa WPF ↔ Avalonia (2026-10-09)

La comparación directa confirma que la paridad funcional sigue en progreso. El prototipo Avalonia ya incluye la pantalla de acceso, navegación principal, instancias, modpacks, galería con visor, preferencias y un primer pase del modo desarrollador. No se considera completa la migración de todos los parciales WPF.

### Modo desarrollador: primer pase implementado

La versión Avalonia ya incorpora:
- Activar o desactivar el modo desarrollador desde preferencias persistentes.
- Mostrar u ocultar el acceso **DEV** en la navegación.
- Página **MINECRAFT VANILLA** con catálogo oficial de versiones.
- Filtros de snapshots y versiones beta/alpha antiguas.
- Guardado de versión seleccionada y filtros.
- Preparación y lanzamiento mediante el runtime compartido.
- Cancelación visible de la preparación Vanilla, con uso del token de cancelación del runtime.
- Errores de guardado de filtros y versión seleccionada gestionados desde la interfaz, restaurando la selección anterior.

**Pendiente de aceptación:** validar visualmente el fondo y los estados de la página, probar errores del catálogo, confirmar que Vanilla no se inicia junto a otra instancia, y comprobar el comportamiento al cerrar el proceso en los cinco destinos.

### Preparación de resource packs y skins: portado en validación

Antes de iniciar una instancia normal, Avalonia ya invoca el servicio compartido de selección de resource packs del modpack y prepara o desactiva la skin local según el perfil. Si faltan packs requeridos o la preparación falla, muestra el motivo y cancela el lanzamiento en vez de abrir el juego con una configuración incompleta. Esta ruta requiere confirmar la compilación de los nuevos servicios vinculados y probar instancias reales.

### Regla de trabajo

Cada tanda de portabilidad debe partir de una función comprobada de la versión WPF, conservar su comportamiento y registrar las diferencias reales. No añadir acciones, botones ni preferencias que no existan en la referencia. La compilación satisfactoria, por sí sola, no demuestra paridad funcional.

### Corrección de actualización de archivos administrados

La prueba multiplataforma encontró un caso real: el instalador podía considerar iguales dos archivos solo porque compartían tamaño y fecha, aunque su contenido hubiera cambiado. En la rama de portabilidad, la comparación ahora calcula el hash cuando ambos archivos tienen el mismo tamaño; así no se omiten actualizaciones válidas. La rama de referencia Windows se dejó restaurada sin este cambio.



### Contadores globales y temas estacionales — primer pase en Avalonia

- [x] Consultar el feed remoto compartido de contadores y conservar el último estado si hay un fallo temporal de red.
- [x] Renderizar contadores activos y vigentes en el área de contenido, con actualización local cada segundo y refresco remoto periódico.
- [x] Reducir su opacidad en Galería y Ajustes.
- [x] Aplicar los colores base de los temas automáticos en la fecha de Monterrey (UTC-6) cuando la preferencia de temas esté activada.
- [ ] Verificar comportamiento visual y refresco en ejecución real.
- [x] Añadir efectos animados de partículas para globos de aniversario y fuegos artificiales de Año Nuevo.
- [x] Portar las franjas decorativas superior e inferior para los temas festivos.
- [x] Portar el ciclo de vista previa de temas de desarrollador (automático, Navidad, Halloween, San Valentín, Día de la Mujer, aniversario y Año Nuevo).
- [ ] Comparar visualmente las franjas, el control de vista previa y los efectos con WPF.
