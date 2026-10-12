# Matriz funcional para la portabilidad de Negative Launcher

Esta matriz registra la superficie visible y los puntos de entrada funcionales que deben conservarse durante la migración. La versión WPF actual sigue siendo la referencia de comportamiento. No se debe eliminar una pantalla ni sustituir una función por un marcador provisional para conseguir que compile.

## Criterios de aceptación globales

- [ ] Conservar las funciones actuales, los flujos de navegación y el orden de las operaciones.
- [ ] Conservar textos, iconos, imágenes, paleta, tamaños, espaciados, estados visuales y temas.
- [ ] Conservar los datos existentes del usuario y la compatibilidad con las instalaciones de Minecraft ya creadas.
- [ ] Mantener los flujos de cuenta Microsoft y cuenta sin conexión, incluidos sus mensajes de error.
- [ ] Mantener la instalación, configuración, actualización, verificación y lanzamiento de instancias.
- [ ] Mantener pausa/reanudación y detención de operaciones. Los modpacks y la preparación de Minecraft cuentan con controlador pausable/detenible; CI ya prueba la pausa inicial y la detención desde pausado. Sigue pendiente la aceptación manual de las pantallas en equipos reales.
- [ ] Mantener la consola de juego, las preferencias, las capturas, los temas festivos, los modpacks y las herramientas de desarrollador.
- [ ] Mantener la misma semántica de rutas y archivos, adaptando la ubicación al sistema operativo sin sobrescribir ni mover silenciosamente los datos antiguos.
- [ ] No considerar terminada una pantalla hasta comprobar sus estados vacío, cargando, éxito, error y acciones secundarias aplicables.
- [x] Construir artefactos Linux x64 y Linux ARM64 mediante GitHub Actions. Windows se conserva como referencia sin cambios; macOS queda aplazado para una fase posterior. Pendiente: pruebas manuales en equipos Linux reales.

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
4. **Minecraft/Java/loaders:** el port incorpora instaladores para Vanilla, Forge, NeoForge, Fabric y Quilt. Deben verificarse en equipos Linux/macOS las reglas de rutas, nombres de perfiles, permisos, selección de Java, descargas y lanzamiento de modpacks reales.
5. **Datos existentes:** las rutas actuales usan carpetas especiales de Windows. La migración debe elegir directorios apropiados por sistema y documentar cualquier importación de datos, sin borrar ni mover automáticamente los datos existentes.
6. **Paquetes nativos y publicación:** autenticación, librerías nativas y empaquetado deben validarse por cada RID de destino. No declarar soporte estable hasta ejecutar builds y pruebas reales en Linux x64 y Linux ARM64; macOS se abordará después.

## Orden de ejecución propuesto

1. [x] Inventariar pantallas, eventos visibles y servicios principales.
2. [x] Añadir workflow de compilación de referencia Windows en la rama de portabilidad.
3. [x] Confirmar compilación de referencia Windows en GitHub Actions.
4. [x] Confirmar la estrategia de UI multiplataforma mediante un prototipo Avalonia aislado, sin sustituir la versión WPF.
5. [ ] Portar y validar cada pantalla y ventana auxiliar conservando el comportamiento.
6. [ ] Completar la comparación de los parciales de la ventana principal, incluidos contadores globales y temas estacionales, para Linux.
7. [x] Generar paquetes autocontenidos para Linux x64 y Linux ARM64 mediante GitHub Actions.
8. [ ] Probar instalación, actualización, autenticación y lanzamiento de Minecraft en Linux real y comparar visualmente con la referencia Windows.

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

**Pendiente de aceptación:** validar visualmente el fondo y los estados de la página, probar errores del catálogo, confirmar que Vanilla no se inicia junto a otra instancia, y comprobar el comportamiento al cerrar el proceso en Linux x64 y Linux ARM64 durante esta fase.

### Preparación de resource packs y skins: portado en validación

Antes de iniciar una instancia normal, Avalonia ya invoca el servicio compartido de selección de resource packs del modpack y prepara o desactiva la skin local según el perfil. Si faltan packs requeridos o la preparación falla, muestra el motivo y cancela el lanzamiento en vez de abrir el juego con una configuración incompleta. Esta ruta requiere confirmar la compilación de los nuevos servicios vinculados y probar instancias reales.

### Ciclo de vida de operaciones pausadas — corrección y regresión

- [x] Al disponer un controlador de descarga/preparación, cancelar el token global y la fase activa antes de liberar sus recursos.
- [x] Liberar la espera de reanudación si el controlador se dispone mientras la operación está pausada, evitando tareas bloqueadas indefinidamente durante cierre o limpieza.
- [x] Hacer idempotente `Dispose()` para permitir que la limpieza de recursos se invoque más de una vez.
- [x] Añadir una regresión automatizada al conjunto de pruebas del núcleo para comprobar que una espera pausada termina con cancelación al disponer el controlador.
- [x] Confirmado en [CI Linux x64 + ARM64, ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183): pruebas del núcleo y publicación completadas en ambas arquitecturas.

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


## Persistencia y recuperación segura (2026-10-10)

- [x] Guardar preferencias, estado de última instancia e información de instancias mediante archivos temporales únicos y reemplazo atómico.
- [x] Conservar una copia del archivo de preferencias JSON dañado antes de recuperar valores predeterminados.
- [x] Añadir pruebas para recuperación de preferencias corruptas y limpieza de temporales tras guardados completados.
- [ ] Verificar las rutas de recuperación y reemplazo atómico en ejecuciones reales de Linux x64 y ARM64; Windows queda como referencia y macOS se abordará después.

### Medición de almacenamiento — tolerancia a archivos inaccesibles

La pantalla de ajustes calcula el tamaño de las instancias, los archivos compartidos de Minecraft, la caché y los temporales por separado. El recorrido ahora omite carpetas o archivos a los que el sistema no permita acceder, no sigue enlaces simbólicos a ubicaciones externas y evita que un archivo inaccesible interrumpa toda la medición. Si el total excede el rango de un entero de 64 bits, se satura en el máximo representable en lugar de desbordarse.

**Pendiente de aceptación:** probar el cálculo con carpetas protegidas, archivos eliminados durante la lectura, enlaces simbólicos y árboles grandes en Windows, Linux y macOS.

### Guardado automático al cerrar — ajustes integrados

La página de ajustes marca los cambios como pendientes y, si se solicita cerrar el launcher durante la espera de guardado automático, cancela el cierre momentáneamente, espera a que termine la tarea anterior y persiste los valores visibles antes de cerrar. Si el guardado falla, el launcher permanece abierto y permite reintentar; los cambios no se dan por guardados hasta que el servicio confirma la escritura.

**Pendiente de aceptación:** probar cierre con cambios recientes, cierre durante una escritura activa y fallo de permisos/almacenamiento en Linux x64 y Linux ARM64 durante esta fase.

### Instalación de modpack y preparación del runtime

El flujo de instalación de Avalonia ahora continúa con la preparación de Minecraft, Java y el cargador, en lugar de finalizar tras descargar los archivos del modpack. El progreso se divide entre descarga y preparación; las acciones de pausar, reanudar y detener también se aplican a la fase de runtime. Si se cancela o falla la preparación, la instancia descargada se conserva y puede recuperarse desde la lista para continuar con **JUGAR**.

**Pendiente de aceptación:** probar instalación nueva y actualización existente, pausa/reanudación durante descarga y durante preparación, cancelación en ambas fases y fallo de red/runtime en Linux x64 y Linux ARM64 durante esta fase.

### Mantenimiento de archivos durante ejecución

Las acciones de limpieza de caché, temporales y archivos no utilizados ahora se bloquean mientras haya una descarga/instalación, una preparación de runtime o un proceso de Minecraft activo. Esto evita borrar archivos que una operación en curso pueda necesitar.

**Pendiente de aceptación:** comprobar los tres bloqueos durante descarga, preparación, juego abierto y después de cerrar el juego.

El cambio de la carpeta de instalaciones también queda bloqueado mientras se descargan archivos, se prepara el runtime o Minecraft sigue abierto, evitando que una operación cambie de raíz a mitad del proceso.

### Eliminación segura de instancias

La interfaz ahora rastrea el ID de la instancia asociada al proceso de Minecraft y bloquea el borrado cuando esa instancia está abierta, durante una preparación o mientras otra operación de modpack está activa. El seguimiento del proceso se limpia al salir del juego, incluso si se cierra desde el launcher.

### Exclusión de operaciones de Minecraft

Se bloquea el inicio de una segunda instancia mientras exista una descarga/instalación o preparación activa, y se evita actualizar/verificar una instancia que se está ejecutando. Al cerrar Vanilla desde el launcher también se restablece el estado de su botón, evitando que quede visualmente atascado en **CERRAR**.


## Revisión funcional consolidada (10 de octubre de 2026)

La rama ya cuenta con estas áreas conectadas en el prototipo: acceso Microsoft y sin conexión; gestión de cuentas; ajustes y persistencia; limpieza y medición de almacenamiento; instalación/actualización de modpacks seguida de preparación del runtime; selección de resource packs y skin antes del lanzamiento; consola; galería de capturas; contadores globales; temas de temporada; modo desarrollador; y protecciones frente a operaciones de mantenimiento concurrentes.

La ejecución [273](https://github.com/MrLion303/negativelauncher/actions/runs/38082616063) validó la publicación para Windows x64, Linux x64, Linux ARM64, macOS Intel y macOS Apple Silicon, junto con las pruebas compartidas en Windows, Linux y macOS.

**Pendiente de aceptación:** validar inicio de sesión y retorno del navegador, inicio/cierre real de Minecraft, instalación y actualización con modpacks representativos, comportamiento de Forge y comparación visual completa con WPF en equipos reales. Los checks de CI no equivalen a una prueba de juego de extremo a extremo.


### Revisión de compilación y dependencias (10 de octubre de 2026)

La validación de portapapeles detectó una incompatibilidad de API en el prototipo. Se cambió la llamada a la API tipada `SetValueAsync(DataFormat.Bitmap, bitmap)`, se alinearon los paquetes de Avalonia en 11.3.13 y se fijó `Tmds.DBus.Protocol` en la versión corregida 0.21.3. La firma del instalador acepta ahora una instancia actual nula para reflejar que el mismo servicio admite instalaciones nuevas y actualizaciones. Estas modificaciones quedan pendientes de la ejecución multiplataforma posterior a este commit; no se marcan como validadas hasta que Actions termine.


### Resultado CI — 10 de octubre de 2026

- [x] Compilar los cinco paquetes de destino y ejecutar las pruebas compartidas en Windows, Linux y macOS: [ejecución 282](https://github.com/MrLion303/negativelauncher/actions/runs/38083530559).
- [x] Compilar la referencia de Windows: [ejecución 93](https://github.com/MrLion303/negativelauncher/actions/runs/38083530558).
- [x] Corregir la API de copia de capturas al portapapeles y actualizar las dependencias de Avalonia; el pin de `Tmds.DBus.Protocol 0.21.3` evita la versión vulnerable 0.21.2.
- [ ] Probar visualmente el portapapeles y la galería en equipos reales, así como el flujo de inicio de sesión y el lanzamiento de Minecraft en Linux x64 y Linux ARM64 durante esta fase.


### Fidelidad visual y actualización de versión — validación CI

- [x] Centrar el texto de versión sobre el ancho completo de la barra superior, independientemente de los controles laterales.
- [x] Cargar `Assets/home_icon.png` como icono circular del botón Inicio, conservando un fallback si el recurso no se puede cargar.
- [x] Mostrar en el aviso de actualización el número de la versión detectada en GitHub Releases; el formato admite versiones con distinta cantidad de componentes.
- [x] Cargar y cachear los iconos de cada instancia en la barra lateral, con recorte circular y texto como fallback si no hay icono o falla la descarga.
- [x] Validar el conjunto mediante [la ejecución 288](https://github.com/MrLion303/negativelauncher/actions/runs/38084022314): compilación para Windows x64, Linux x64/ARM64, macOS Intel/Apple Silicon y pruebas compartidas en Linux x64 y Linux ARM64 durante esta fase.
- [ ] Comparar visualmente estos ajustes en ejecución en equipos reales; CI confirma compilación, no fidelidad píxel por píxel.


### Robustez de iconos y verificación final — 10 de octubre de 2026

- [x] La carga de iconos circulares usa una caché independiente por ID de archivo, descargas temporales únicas y fallback al nombre de la instancia si hay errores de red, caché o decodificación.
- [x] Se envolvió el proceso de carga para que un fallo de la carpeta de caché o de la imagen no interrumpa la navegación.
- [x] Validación final de esta tanda: [ejecución 289](https://github.com/MrLion303/negativelauncher/actions/runs/38084172521) terminó correctamente en Linux x64 y Linux ARM64 durante esta fase, con las pruebas compartidas de Windows, Linux y macOS en verde.
- [ ] Sigue pendiente la prueba visual e interactiva en equipos reales; el CI no verifica el comportamiento del portapapeles ni la carga de iconos durante una sesión real.

### Empaquetado y comprobaciones del artefacto — 10 de octubre de 2026

- [x] El paquete Linux incluye `iniciar-negative-launcher.sh`, que resuelve su propia carpeta, comprueba el ejecutable y reenvía argumentos sin depender del directorio actual.
- [x] El paquete Linux incluye una guía de inicio en español y un instalador opcional que crea un acceso real en el menú de aplicaciones del usuario.
- [x] GitHub Actions comprueba la sintaxis de ambos scripts, prueba el lanzamiento desde una ruta con espacios, la conservación de argumentos y la creación del acceso de escritorio y verifica que el archivo comprimido contenga el lanzador y el ejecutable.
- [x] La creación del bundle macOS comprueba la validez sintáctica de `Info.plist`, la existencia y permisos del ejecutable y la presencia de los elementos esenciales dentro del ZIP.
- [ ] Estas comprobaciones detectan paquetes incompletos o mal formados, pero no sustituyen la apertura de la aplicación en escritorios Linux reales ni la firma/notarización de macOS.
- [x] Se añadió una prueba de arranque gráfico de Linux con Xvfb: el workflow comprueba que la aplicación se mantenga abierta y captura los mensajes de error si termina antes de tiempo.
- [ ] La prueba de arranque headless no sustituye la validación interactiva en un escritorio real ni el inicio de sesión y lanzamiento de Minecraft.

### Tanda adicional — migración de almacenamiento multiplataforma (2026-10-10)

- [x] Corregida la detección de cambios de volumen: en Linux/macOS, `Path.GetPathRoot` puede devolver `/` para ubicaciones montadas en sistemas de archivos distintos. Si el movimiento directo falla en un sistema no Windows, la migración ahora intenta copiar y después retirar el origen.
- [x] Si la copia falla, se intenta limpiar el destino parcial y se conserva la excepción original; el origen no se elimina antes de terminar la copia.
- [x] Añadidas pruebas compartidas para migrar metadatos de instancias, recursos compartidos de Minecraft y caché a una ruta con espacios.
- [x] Añadidas pruebas que confirman que un destino ocupado provoca el rechazo y conserva los datos; si el movimiento de instancias funciona pero el de Minecraft falla, el servicio revierte el primer movimiento y mantiene activa la raíz original.
- [x] Actualizado el nombre de versión enviado al proceso de Minecraft a 0.1.1 y sustituido un mensaje de error específico de Windows por uno multiplataforma.
- [x] Las pruebas compartidas de migración se ejecutan correctamente en Windows, Linux y macOS en la ejecución 347.

### Prueba de arranque gráfico — defecto encontrado y corregido (2026-10-10)

- [x] La prueba Linux con Xvfb encontró un cierre real durante `InitializeComponent`: `GalleryFilter_Changed` recibía `SelectionChanged` antes de que Avalonia terminara de crear todos los controles.
- [x] El manejador ahora comprueba que los selectores y el panel de imágenes estén inicializados antes de leer su estado.
- [x] La ejecución 307 superó la prueba de arranque gráfico en Linux x64 y las pruebas compartidas en Windows, Linux y macOS. La validación de empaquetado Linux detectó un falso fallo por `grep -q` cerrando la tubería de `tar`; se corrigió para validar contra un listado guardado en archivo.
- [x] La ejecución 347 confirmó el empaquetado final Linux x64/ARM64, Windows x64, macOS Intel y Apple Silicon tras los cambios del enlace de actualizaciones y los iconos nativos.

- [x] La comprobación de actualizaciones ya selecciona artefactos compatibles con el sistema y arquitectura actuales; Linux/macOS dejan de recibir por error el ejecutable de Windows cuando el release contiene únicamente `Negative.Client.exe`. Si no existe un artefacto compatible, el aviso abre la página general del release.
- [x] El enlace de actualización utiliza el servicio nativo de apertura de URI de Avalonia y valida que la dirección sea HTTP/HTTPS, en lugar de depender del comportamiento de `Process.Start` con `UseShellExecute` en cada sistema.


### Tanda adicional — recuperación segura de archivos entre volúmenes y paridad del launcher (10 de octubre de 2026)

- [x] La presencia enriquecida de Discord se ha enlazado al prototipo Avalonia con el mismo servicio e identificador de la versión Windows; las llamadas están protegidas para que la ausencia de Discord no interrumpa el launcher.
- [x] La ventana de acceso bloquea el redimensionado desde el constructor; después de iniciar sesión, el doble clic de la barra superior alterna el tamaño normal/maximizado.
- [x] Avalonia recupera el ID de la última instancia jugada, lo guarda al iniciar Minecraft y limpia ese estado cuando se elimina la instalación correspondiente, siguiendo el comportamiento de la referencia WPF.
- [x] La validación de extracción ZIP cubre separadores de rutas Windows y Unix, y la comparación de subrutas respeta la sensibilidad a mayúsculas del sistema de archivos.
- [x] La migración de archivos compartidos de Minecraft utiliza un movimiento con recuperación: si el rename entre volúmenes falla, copia a un temporal del destino, renombra el temporal y elimina el origen solo después de completar la copia.
- [x] Se añaden pruebas de movimiento correcto y de destino ocupado, incluida la conservación de ambos archivos si no se puede completar el movimiento.
- [x] La ejecución 347 confirmó compilación y pruebas de esta tanda en Windows, Linux y macOS; la ejecución 113 confirmó además la compilación WPF de referencia.
- [ ] Siguen pendientes las pruebas reales de autenticación Microsoft y lanzamiento de Minecraft en hardware Linux; macOS queda aplazado.


- [x] El menú rápido y el panel de cuentas usan la lista de cuentas de la capa de servicio, por lo que también pueden seleccionar o eliminar el perfil sin conexión activo; el límite de tres se calcula por cuentas Microsoft premium.
- [x] Java personalizado se valida y recibe permisos de ejecución en Linux/macOS tanto al preparar la versión como justo antes del lanzamiento; hay una prueba de permisos POSIX en la suite compartida.


- [x] Las rutas de capturas seleccionadas y el archivador usan la semántica de comparación de rutas de la plataforma; en sistemas Unix, dos nombres que solo difieren en mayúsculas no se fusionan por accidente.


- [x] El archivador de capturas admite volúmenes distintos: si el rename de la carpeta falla por E/S, copia los archivos, guarda los metadatos y elimina el origen solo después de completar el archivo.
- [x] La nueva copia de carpetas valida rutas de destino, conserva archivos anidados y limpia las copias parciales si falla; se añadieron pruebas sin depender de un disco adicional.


- [x] La validación de perfiles sin conexión se centraliza en el servicio y se reutiliza en el acceso, Ajustes y la eliminación de skin; acepta únicamente nombres ASCII de Minecraft de 3 a 16 caracteres, y la prueba confirma que un nombre inválido no borra la skin existente ni modifica el perfil guardado.


- [x] El instalador de modpacks usa la comparación de rutas de la plataforma para su inventario de archivos administrados y valida las rutas del ZIP con la misma regla de subrutas; los tests distinguen nombres con distinta capitalización en Windows y Unix.


- [x] La preparación normal de Minecraft y Minecraft Vanilla ahora comparte el controlador de pausa/reanudación/detención, con una barra global persistente sobre las páginas del launcher.
- [x] La verificación de integridad usa el mismo bucle por fases cancelables, de modo que pausar no deja su preparación del runtime en segundo plano.
- [x] La ejecución 347 pasó la matriz automatizada completa, incluyendo los controladores de pausa y detención.
- [ ] Pendiente: realizar el ciclo completo de autenticación/inicio real de Minecraft en Linux con un Client ID registrado y autorizado.


- [x] La compilación Windows incluye el mismo helper de reglas de rutas que utiliza la versión Avalonia; el servicio compartido de instalación de modpacks no deja referencias sin resolver al compilar la referencia WPF.


- [x] Se reforzaron las pruebas del controlador de operaciones: la pausa establecida antes de iniciar la primera fase se conserva, al reanudar el token nuevo queda activo, y detener desde pausado libera la espera.


- [x] La prueba manual de Java aplica la misma normalización de permisos de ejecución en Unix que usa la preparación/lanzamiento; evita dar un falso error por falta del permiso de ejecución.


- [x] La prueba de pausa anterior libera explícitamente la fase cancelada antes de reanudarla, de forma idéntica al ciclo `RunPausableRuntimePhaseAsync` de la interfaz.


### Comparación de rutas según el volumen real

- [x] La comparación de rutas detecta la sensibilidad a mayúsculas de la carpeta existente más cercana, en lugar de asumir un único comportamiento por sistema operativo; el resultado se almacena en caché por ubicación.
- [x] Las migraciones de Minecraft y almacenamiento compartido, el inventario del instalador de modpacks y los nombres de configuración preservados usan la semántica del volumen donde viven.
- [x] Las pruebas crean variantes de un archivo y verifican el resultado real del sistema de archivos; cubren APFS sensible y no sensible sin necesitar un volumen adicional.
- [x] Confirmado en la ejecución 347 para las pruebas compartidas y los cinco paquetes multiplataforma.


- [x] La ejecución 347 confirmó el icono XDG del paquete Linux y el ICNS nativo de ambos bundles macOS.


- [x] La ejecución 347 compiló los cambios de globos interactivos del tema de aniversario.
- [ ] Falta verificar visualmente en un escritorio real que los globos respondan al clic y no bloqueen los controles del launcher.


### Calendario estacional compartido

- [x] WPF y Avalonia consumen el mismo calendario estacional sin dependencias visuales, y usan una sola paleta para las fechas automáticas y las vistas previas.
- [x] Se añadieron pruebas para Navidad, Año Nuevo, Halloween, San Valentín, el 8 de marzo, el 27 de marzo, un día normal y el offset UTC-6 de Monterrey.
- [x] Confirmado en las ejecuciones 347 (WPF y los cinco paquetes/pruebas compartidas) y 113 (compilación WPF de referencia).


- [x] La página Minecraft Vanilla recupera el fondo `minecraft_vanilla.png` de la referencia Windows en una capa independiente; al salir se limpia el recurso sin interferir con los fondos de Inicio e instalaciones.
- [x] La ejecución 347 compiló la carga del fondo y su ciclo de limpieza.
- [ ] Falta revisar visualmente la página Vanilla en un escritorio real.


### Autenticación Microsoft fuera de Windows

- [x] Windows conserva el proveedor OAuth predeterminado de CmlLib; Linux/macOS usan MSAL y el mismo gestor de cuentas local.
- [x] Se incorporaron la preferencia de Client ID, la variable de entorno NEGATIVE_LAUNCHER_MSAL_CLIENT_ID y las pruebas de normalización GUID.
- [x] Los campos de configuración están integrados en el acceso y Ajustes; la ejecución 347 validó la compilación multiplataforma y la configuración MSAL.
- [ ] Pendiente: registrar/autorizar el Client ID de la app y probar login Microsoft real y lanzamiento de Minecraft Java en Linux.


### Configuración de la autenticación MSAL

- [x] El formulario de acceso permite guardar el Client ID antes de autenticar, también editable desde Ajustes → Cuentas en Linux/macOS.
- [x] La ventana de acceso cubre las tres filas del diseño para no dejar visible el contenido de Inicio detrás del panel de login.
- [x] La ejecución 347 compiló el formulario de configuración y pasó las pruebas automatizadas.
- [ ] El login Microsoft real en Linux sigue necesitando un Client ID registrado y autorizado, y una prueba interactiva con Minecraft.


### Última validación automatizada registrada

- [x] Ejecución 347: [workflow multiplataforma](https://github.com/MrLion303/negativelauncher/actions/runs/38093138145) — 9/9 trabajos correctos sobre el commit `79b05f7933a85ef255d66f17c99e48749ddaa9ea`.
- [x] Ejecución 113: [compilación de referencia Windows](https://github.com/MrLion303/negativelauncher/actions/runs/38093138165) — compilación WPF correcta sobre el mismo commit.
- [ ] Prueba final manual de esta fase: configurar una aplicación MSAL autorizada, iniciar sesión con Microsoft, instalar/verificar Java y lanzar Minecraft en un escritorio Linux. macOS se probará en una fase posterior.


### Superposición de contadores y avisos

- [x] Los contadores globales y el aviso de actualización quedan fijados al área principal, por fuera del ScrollViewer de las páginas, igual que en la referencia WPF; el contenido de cada página sigue desplazándose.
- [ ] Confirmar en una sesión de escritorio que el orden visual y las posiciones permanecen correctas al recorrer páginas largas y cambiar tamaño de ventana.


- [x] Se fijó la versión exacta de DiscordRichPresence a 1.2.1.24, que es la versión resuelta por NuGet en CI, para eliminar el aviso NU1603 de las compilaciones.


- [x] Se corrigió una prueba de ruta de skin que asumía insensibilidad a mayúsculas en todos los sistemas; ahora verifica la semántica detectada del volumen y cubre los dos comportamientos.
- [x] El resolvedor MSAL conserva la prioridad de una variable de entorno válida, pero cae de nuevo a la preferencia guardada cuando la variable existe y está mal formada.
- [ ] Confirmar ambos casos en la matriz CI de Linux x64 y Linux ARM64 de esta fase.


- [x] Añadida una guía de registro y autorización del Client ID MSAL con enlaces oficiales de CmlLib, Microsoft y Minecraft.
- [x] Los paneles de acceso y Ajustes → Cuentas incluyen un enlace directo a la guía; el error al abrir el navegador se comunica en el panel activo.
- [ ] Sigue pendiente comprobar en Linux real el retorno del navegador, el inicio de sesión Microsoft y el lanzamiento de Minecraft con un Client ID autorizado.


### Paridad de la galería de capturas

- [x] La galería multiplataforma combina capturas de las carpetas activas de cada instalación con las capturas archivadas tras eliminar una instalación, igual que WPF.
- [x] El recorrido portable filtra PNG/JPG/JPEG/BMP, recorre subcarpetas, omite entradas inaccesibles y no sigue directorios o archivos marcados como reparse point.
- [x] Se añadió una prueba compartida que comprueba una captura activa, una archivada y el descarte de un archivo de texto, sin necesidad de abrir la interfaz gráfica.
- [x] Confirmado con la matriz CI Linux x64 y Linux ARM64 de [la ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183).


- [x] La eliminación de una instalación limpia también las capturas que estaban archivadas para ella, igual que Windows.
- [x] El archivador valida que la carpeta a borrar sea estrictamente un hijo de la raíz configurada y que la ruta apunte al subdirectorio `screenshots`; metadatos falsificados fuera de la raíz se ignoran.
- [x] Se añadieron pruebas para el borrado normal de archivos archivados y para rechazar una ruta de metadatos que apunta fuera del archivo.
- [x] Confirmadas las nuevas pruebas y la compilación en Linux x64 y Linux ARM64 en [la ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183).


- [x] El lector de archivos archivados valida que cada metadato esté bajo la raíz y que su carpeta de capturas sea exactamente `<archivo>/screenshots`; los registros falsificados o externos se descartan.
- [x] El escáner también rechaza una carpeta raíz de capturas marcada como enlace simbólico, evitando recorrer árboles externos.
- [x] CI confirma compilación y pruebas compartidas de lectura segura de metadatos, galería y limpieza en ambas arquitecturas: [ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183).


- [x] La eliminación de una instalación retira también su caché de iconos y fondos, igual que la referencia Windows; si un archivo temporal está bloqueado, la limpieza de caché no revierte el borrado de la instalación.


- [x] El panel de cuentas muestra usuario, UUID y estado de la cuenta seleccionada como en Windows; la selección se restaura a la cuenta activa al refrescar la lista.
- [x] Reautenticar queda deshabilitado para el perfil local sin conexión, y los estados de botón respetan la selección actual en lugar de reactivar acciones que no corresponden al perfil elegido.
- [x] La recompilación de la interfaz y las pruebas automatizadas pasan en Linux x64 y Linux ARM64: [ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183).
- [ ] Sigue pendiente recorrer selección, reautenticación y cierre de sesión con una cuenta Microsoft real.


### Cabezas de cuenta en la lista de Microsoft

- [x] La lista de cuentas puede mostrar la cara y la capa exterior de la skin oficial usando un servicio portable y un conversor de recorte de Avalonia, sin dependencias de imagen WPF.
- [x] El servicio cachea por UUID, valida el PNG, limita su tamaño y solo acepta el host oficial `textures.minecraft.net`; las cargas se cancelan al refrescar la lista y no bloquean el acceso.
- [x] Pruebas sin red cubren URL oficial (conversión a HTTPS), host arbitrario y perfiles malformados.
- [x] XAML y pruebas validados en Linux x64 y Linux ARM64 en [la ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183).


- [x] El cliente HTTP de las skins no sigue redirecciones; solo descarga desde el host oficial aprobado. Se añadieron pruebas para puertos inesperados, credenciales incrustadas y dominios similares que no son el host oficial.
- [x] La lista de cuentas ofrece altura suficiente para mostrar las tres cuentas con sus avatares.


- [x] El botón de cuenta actual y el desplegable rápido de cambio de cuenta usan los mismos avatares pixelados que Ajustes.
- [x] La carga de avatares del menú rápido tiene su propio token de cancelación para no interrumpir la carga del panel de Ajustes.


- [x] Los textos de carga y estado vacío de la galería ya describen tanto capturas de instalaciones activas como capturas archivadas.


- [x] Corregido el namespace de `CornerRadius` del avatar dinámico del menú rápido, detectado por la compilación de publicación antes de sacar paquetes.
- [x] Recompilación de UI y publicación de ambos destinos confirmadas en [CI, ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183).


### Integridad de permisos al migrar almacenamiento

- [x] La copia de seguridad entre volúmenes conserva las marcas de tiempo de los archivos y, en Linux/macOS, los permisos Unix de archivos y carpetas.
- [x] Se añadió una prueba de regresión para asegurar que los bits de ejecución de un runtime Java sobreviven a una migración por copia.
- [x] Pruebas y empaquetado en ambas arquitecturas confirmados en [la ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183).

## Tanda de endurecimiento de Java y actualizaciones (12 de octubre de 2026)

- [x] La comprobación manual de Java ahora tiene un límite de 15 segundos; si el ejecutable no responde, el launcher intenta detener el proceso y muestra un error en lugar de esperar indefinidamente.
- [x] La búsqueda de Java en `PATH` se centralizó en un helper compartido que selecciona `java.exe` en Windows y `java` en sistemas Unix, admite entradas entre comillas e ignora directorios vacíos o inexistentes.
- [x] El botón de cerrar Minecraft solicita un cierre normal con el mecanismo del sistema: ventana principal en Windows y señal `SIGTERM` en Linux/macOS, antes de ofrecer el cierre forzado.
- [x] El selector rápido de cuentas se cierra al hacer clic fuera o al perder el foco, igual que en la versión Windows.
- [x] La configuración del perfil sin conexión muestra una vista previa PNG de la skin y la opción de quitarla queda pendiente hasta guardar, evitando borrar el archivo antes de confirmar el cambio.
- [x] Al reabrir Configuración se vuelve a cargar el perfil sin conexión guardado y se descartan cambios de skin no confirmados, evitando aplicar accidentalmente una eliminación pendiente.
- [x] Los ajustes de RAM se limitan según la memoria disponible del equipo y la ruta de Java personalizada queda desactivada cuando está activo el Java automático, siguiendo el comportamiento de Windows.
- [x] Se añadió una prueba Unix que inicia un proceso temporal y comprueba que termina tras recibir la solicitud de cierre normal.
- [x] La matriz de CI ejecuta ahora las pruebas compartidas en runners Linux x64 y Linux ARM64, además de publicar ambos paquetes.
- [x] Se añadieron pruebas de la resolución de Java por plataforma y de rutas `PATH` con espacios.
- [x] Confirmada en [la ejecución 38190098948](https://github.com/MrLion303/negativelauncher/actions/runs/38190098948), commit `6a3a16bc859501956fdc70524d459f67576b57cb`: pruebas compartidas, arranque gráfico y paquetes extraídos de Linux x64 y Linux ARM64 terminaron correctamente.

## Tanda de preparación para pruebas externas (11 de octubre de 2026)

- Se añadió un registro local de diagnóstico para excepciones de interfaz, inicio de sesión, mantenimiento, galería, preparación y lanzamiento. Está disponible desde Ajustes → Diagnóstico; no se envía automáticamente.
- La prueba gráfica Linux ahora exige una marca de inicialización completada, no solo que el proceso continúe vivo. La tarea conserva los registros del proceso y del propio launcher como artefactos descargables si la validación falla.
- La validación del loader ocurre antes de crear carpetas o descargar recursos. El runtime incluye preparación para Vanilla, Forge, NeoForge, Fabric y Quilt; CI prueba la normalización de nombres y compila los cinco RID, mientras el primer lanzamiento de cada loader/modpack real sigue pendiente de pruebas manuales.
- La preparación Vanilla comprueba Java una sola vez y completa el progreso sin repetir la misma operación. Los contadores globales siguen avanzando localmente cada segundo y consultan GitHub cada 30 segundos.
- La validación de sintaxis de CI revisa los workflows de compilación multiplataforma y publicación.

Esta tanda mejora la capacidad de probar y diagnosticar el port, pero no sustituye el inicio de sesión Microsoft real ni la prueba de juego en hardware Linux/macOS.


## Tanda de paridad de interfaz y recorrido automatizado (12 de octubre de 2026)

- [x] El workflow conserva la validación estática de manejadores, controles esenciales, dimensiones y base visual de la ventana contra la referencia WPF.
- [x] En Xvfb, el launcher completa la inicialización de datos en Linux x64 y Linux ARM64 y emite una marca explícita solo cuando supera el recorrido de interfaz.
- [x] El recorrido comprueba la compuerta de acceso y el perfil local sin conexión cuando aplica, las pantallas principales, la página Vanilla de desarrollador y la apertura/cierre de Configuración con restauración del contenido a su contenedor.
- [x] Se abre el diálogo real «Instalar modpack» y se cancela; el flujo de prueba no descarga manifiestos ni modifica el catálogo.
- [x] Se revisa la regla de cierre del selector rápido de cuentas al pulsar fuera, preservando el propio botón para alternar el menú.
- [x] Los ejecutables extraídos de los paquetes TAR.GZ de Linux x64 y Linux ARM64 repiten el arranque gráfico y el recorrido antes de publicarse como artefactos.
- [x] Todo lo anterior pasó en [la ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183), commit `61d0d7a9ef0f911a9f921b3c387f8b99b0f90144`.
- [ ] Sigue pendiente una comprobación interactiva en escritorios Linux reales, la comparación visual píxel por píxel y la validación del selector de cuenta con una cuenta real.
- [ ] Sigue pendiente el ciclo de extremo a extremo de Client ID Microsoft autorizado → retorno del navegador → descarga/preparación de Java y loaders → arranque de Minecraft y verificación dentro del juego.


## Tanda de recorrido adicional: cuentas y configuración OAuth (12 de octubre de 2026)

- [x] El smoke test activa el modo desarrollador de forma aislada y comprueba que el selector rápido de cuentas aparece en Vanilla, abre su menú real y verifica la acción para añadir una cuenta; no inicia un flujo OAuth ni requiere credenciales.
- [x] Al abrir Ajustes en Linux, el recorrido verifica que el panel de Client ID público de Microsoft esté disponible y que el contenido se restaure al contenedor original al cerrar la ventana.
- [ ] Sigue pendiente la prueba con una cuenta Microsoft real y un Client ID autorizado, incluida la apertura del navegador, el retorno a localhost y la selección/reautenticación de cuenta.
- [ ] Sigue pendiente la comparación visual píxel por píxel en escritorios físicos Linux x64 y ARM64; Xvfb comprueba el recorrido funcional de la UI, no la fidelidad visual de cada píxel.
