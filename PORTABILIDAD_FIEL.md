# Plan de portabilidad fiel de Negative Launcher

## Objetivo

Publicar el launcher que ya existe para Windows también en Linux y macOS, conservando su identidad visual, sus pantallas y sus funciones. La versión WPF original de `main` es la referencia funcional y visual; el prototipo de Avalonia no es una nueva definición del producto.

## Reglas de trabajo

- No cambiar la interfaz ni eliminar funciones del launcher original para simplificar el port.
- No modificar la rama `main` durante el trabajo de portabilidad.
- Portar las pantallas de forma deliberada, usando los XAML y el comportamiento del original como referencia.
- Reutilizar los servicios existentes siempre que no dependan de WPF o de APIs exclusivas de Windows.
- Toda función debe quedar marcada como portada, pendiente o incompatible con explicación; compilar no equivale a completar el port.
- Publicar artefactos independientes para Windows, Linux y macOS mediante GitHub Actions.
- No declarar una plataforma lista hasta validar el inicio, la persistencia de configuración, la autenticación, la gestión de instancias, la instalación/actualización de modpacks y el lanzamiento de Minecraft.

## Interfaz original que debe preservarse

Referencia principal: `Negative Client/Negative Client/MainWindow.xaml` y sus archivos parciales `MainWindow.*.cs`.

- Ventana principal de 1150 × 710, tamaño mínimo 900 × 560, tema oscuro y barra superior personalizada.
- Barra de título con marca, minimizar, maximizar/restaurar y cerrar.
- Navegación lateral compacta con Inicio, instalaciones/instancias, galería y acceso a ajustes/cuenta.
- Fondo de inicio e imagen/fondo de instalación, con las capas de oscurecimiento correspondientes.
- Pantalla principal centrada con marca Negative Studios, instalación seleccionada, selector de versión cuando se usa el modo desarrollador, progreso y botón JUGAR.
- Acciones de operación de descarga: detener y pausar/reanudar.
- Estado y progreso visibles para instalación, actualización y lanzamiento.
- Acceso rápido a la cuenta Microsoft, selector de cuenta y avisos de sesión.
- Opciones de la instalación seleccionada.
- Galería de capturas con filtros, orden, selección, eliminación y visor.
- Ventanas existentes: configuración, instalación de modpack por código, consola del juego, inicio de sesión de desarrollador y las demás ventanas definidas en el proyecto.

La referencia visual no se limita a los textos: también incluye distribución, proporciones, fondos, iconografía, espaciado, estados seleccionados, overlays y ventanas sin bordes.

## Funciones originales que deben auditarse y conservarse

- Inicio y restauración de preferencias, última instancia utilizada y raíz de almacenamiento configurable.
- Catálogo remoto de modpacks y lectura de manifiestos.
- Instalación y actualización de modpacks, descargas cancelables y pausables, seguimiento de progreso y manejo de errores.
- Gestión de instancias instaladas.
- Autenticación Microsoft, administración de cuentas y perfil sin conexión cuando esté habilitado.
- Preparación y lanzamiento de Minecraft con la configuración elegida.
- Consola del juego y preferencia para cerrar el launcher al iniciar el juego.
- Ajustes de memoria, Java, almacenamiento, catálogo y comportamiento del launcher.
- Modo desarrollador, selección de versiones y filtros de snapshots/betas.
- Galería y archivo de capturas, filtros, orden y eliminación.
- Personalización de marca, imágenes, temas y fondos que ya formen parte del original.
- Contadores, eventos, ventanas secundarias y cualquier comportamiento implementado en los archivos parciales del proyecto.

## Estado al comenzar esta corrección

- [x] Identificada la aplicación WPF original como fuente de verdad.
- [x] Confirmado que la interfaz actual de Avalonia es un prototipo simplificado y no tiene paridad visual ni funcional.
- [x] Auditar la estructura de la ventana principal y enumerar los parciales de comportamiento de la versión WPF.
- [ ] Completar el mapa de paridad de cada ventana, servicio y evento original.
- [ ] Migrar toda la interfaz original a Avalonia conservando su composición y estados.
- [x] Ajustar la ventana principal al tamaño de referencia, la barra superior oscura y la navegación lateral compacta.
- [x] Incluir los recursos gráficos originales en los paquetes y cargar el logotipo/fondo de inicio.
- [x] Añadir selección de instancias en la barra lateral y conectar esa selección con JUGAR.
- [x] Mostrar el progreso de preparación/lanzamiento en Inicio.
- [x] Mover el servicio y el modelo de cuentas Microsoft al núcleo compartido para reutilizarlos en Windows y Avalonia.
- [x] Conectar la interfaz con añadir, seleccionar, reautenticar y cerrar sesión de cuentas Microsoft.
- [x] Conectar JUGAR con la sesión Microsoft o el perfil local activo.
- [x] Añadir selector multiplataforma de ejecutable Java y carpeta de instalaciones.
- [x] Recuperar los ajustes de RAM con deslizador, argumentos Java personalizados y opción de temas de temporada.
- [x] Separar las pestañas General y Cuentas dentro de Ajustes.
- [x] Añadir una ventana de consola de Minecraft con salida estándar, errores y estado de cierre.
- [x] Replicar la barra de título personalizada con minimizar, maximizar/restaurar, cerrar y arrastre en Avalonia.
- [x] Añadir menú rápido de cuentas Microsoft en la barra superior, cambio de cuenta y acceso a administración desde Ajustes. Quedan pendientes las cabezas de perfil y el aviso visual de sesión.
- [x] Portar la ventana de opciones por instalación con búsqueda/instalación de actualizaciones, verificación de integridad del modpack y eliminación confirmada.
- [ ] Portar el selector de versiones de desarrollador y las acciones de pausa/detención.
- [x] Portar filtros por instalación, orden, selección múltiple, visor independiente y eliminación confirmada de capturas en Avalonia.
- [x] Añadir selección del modelo de skin (wide/slim) y selección/eliminación de skin PNG para el perfil local. Quedan pendientes la paridad completa de skins de cuentas Microsoft y la presentación visual de los modelos.
- [x] Añadir medición del espacio ocupado por instancias, Minecraft compartido, caché y temporales.
- [x] Añadir limpieza confirmada de la caché de ZIP de modpacks sin borrar instancias ni archivos compartidos de Minecraft.
- [ ] Completar el resto de limpieza avanzada y probar el guardado automático en todas las plataformas.
- [ ] Portar las ventanas independientes de instalación de modpack, opciones de instalación e inicio de sesión de desarrollador.
- [ ] Portar modo desarrollador, filtros de snapshots/betas, fondos por instalación, iconos y temas estacionales reales.
- [ ] Separar/adaptar las dependencias específicas de Windows sin reemplazar los comportamientos.
- [x] Conectar las acciones de opciones de instalación con la actualización y verificación real del modpack mediante el catálogo configurado.
- [x] Añadir controles para pausar, reanudar y detener operaciones de instalación, actualización y verificación desde la interfaz.
- [x] Guardar y restaurar la última instancia seleccionada.
- [ ] Probar interactivamente instalación, actualización, verificación y cancelación/pausa de descargas en cada sistema.
- [ ] Añadir pruebas funcionales de interfaz y validar la ejecución real en Windows, Linux y macOS.
- [ ] Compilar artefactos de los tres sistemas y validar su ejecución.

## Últimos avances

- La galería multiplataforma ya permite filtrar por instalación, ordenar por fecha o nombre, seleccionar varias capturas, abrir un visor grande con navegación anterior/siguiente y eliminar capturas con confirmación.
- La barra de título de Avalonia ya tiene controles propios para minimizar, maximizar/restaurar, cerrar y arrastrar la ventana.
- El menú rápido de la barra superior ya permite cambiar entre cuentas Microsoft guardadas, añadir otra cuenta (respetando el máximo del servicio) y abrir la pestaña de cuentas de Ajustes.
- El perfil sin conexión permite elegir entre modelo clásico y slim, seleccionar una skin PNG y quitar la skin personalizada.
- Las opciones por instalación ya conectan actualización y verificación con el catálogo y el instalador compartido; tras cambiar archivos se invalida la preparación previa del runtime.
- Ajustes mide el tamaño de instancias, runtime compartido, caché y temporales, y permite limpiar caché y temporales por separado.
- Cada instalación ya dispone de una ventana de opciones basada en la original: busca el modpack por su código guardado, actualiza desde el catálogo configurado, verifica/restaura los archivos administrados por el ZIP y permite eliminar la instancia con confirmación. Tras actualizar o reparar, invalida la preparación del runtime para que Minecraft vuelva a prepararse antes del siguiente lanzamiento.
- La compilación CI del código con galería, barra de título, menú rápido de cuentas y opciones de skin local pasó para Windows x64, Linux x64, Linux ARM64, macOS x64 y macOS ARM64, además de las pruebas de lógica compartida. Esto acredita que compila para los cinco destinos, no una prueba interactiva real de la aplicación.

## Último bloque de cambios

- La ventana de opciones de una instalación ahora ofrece actualización, verificación de integridad y eliminación, manteniendo el comportamiento del servicio compartido. La verificación vuelve a instalar los archivos administrados por el modpack y conserva archivos extra.
- La pantalla de modpacks incorpora pausa/reanudación y detención para las operaciones que usan `DownloadOperationController`; los controles se muestran solo mientras hay una operación activa.
- El launcher recuerda la última instancia seleccionada y vuelve a abrirla al iniciar, siempre que siga registrada.

La pestaña General de Ajustes muestra el uso de almacenamiento por categoría y permite volver a calcularlo. La limpieza de caché se bloquea durante una operación de modpack y pide confirmación antes de borrar archivos recuperables.

## Criterio de aceptación

La portabilidad se considera lista cuando una persona puede hacer en Linux o macOS las mismas operaciones principales que en Windows y reconoce la misma interfaz. Un workflow exitoso solo acredita que el código compila para el destino, no que la experiencia esté completa.
