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
- [ ] Auditar y mapear cada ventana, servicio y evento del original.
- [ ] Migrar la interfaz original a Avalonia conservando su composición y estados.
- [ ] Separar/adaptar las dependencias específicas de Windows sin reemplazar los comportamientos.
- [ ] Completar autenticación y cuentas en Linux/macOS.
- [ ] Completar equivalencia de instalación, actualización, consola y lanzamiento.
- [ ] Añadir pruebas funcionales y validaciones multiplataforma.
- [ ] Compilar artefactos de los tres sistemas y validar su ejecución.

## Criterio de aceptación

La portabilidad se considera lista cuando una persona puede hacer en Linux o macOS las mismas operaciones principales que en Windows y reconoce la misma interfaz. Un workflow exitoso solo acredita que el código compila para el destino, no que la experiencia esté completa.
