# Arquitectura de portabilidad de Negative Launcher

## Objetivo

Llevar el launcher existente a Windows, Linux y macOS sin recortar funciones ni alterar el comportamiento de la versión Windows. La versión WPF actual es la referencia de regresión; la aplicación Avalonia se desarrolla en paralelo y no sustituye todavía al producto.

## Decisiones que no deben romperse

- No modificar ni reemplazar `main` durante el trabajo de portabilidad.
- No cambiar silenciosamente los formatos JSON, nombres de carpetas, identificadores de instancias o archivos administrados.
- No borrar funciones para facilitar la compilación de una plataforma.
- No declarar una plataforma terminada solo porque `dotnet publish` haya terminado.
- Mantener los cambios de UI y las adaptaciones por sistema operativo separados de la lógica del launcher siempre que sea posible.

## Capas propuestas

### 1. Interfaz multiplataforma (Avalonia)

Responsable de ventanas, controles, navegación, diálogos de selección, notificaciones y coordinación del hilo visual. Debe reproducir los flujos de la UI WPF; no debe contener lógica propia de instalación, autenticación ni administración de instancias.

La UI se migra pantalla por pantalla. Cada pantalla conserva estados de carga, éxito, error, cancelación y confirmación que existan en la versión original.

### 2. Servicios de aplicación

Responsables de los casos de uso del launcher y de mantener el comportamiento observable:

- Cuentas Microsoft y perfiles sin conexión.
- Catálogo e instalación de modpacks.
- Instancias, almacenamiento compartido, verificación y lanzamiento de Minecraft.
- Preferencias y última instancia jugada.
- Control de descargas y cancelación.
- Selección de recursos, galería y capturas.
- Temas estacionales, temporizadores y herramientas de desarrollador.

Antes de compartir un servicio con Avalonia se debe revisar si importa tipos de WPF, usa rutas específicas de Windows o invoca controles directamente. Un servicio con dependencias visuales no se puede considerar portable por su nombre o ubicación.

### 3. Adaptadores de plataforma

Las operaciones dependientes del sistema deben tener un punto de entrada explícito, con implementaciones verificables:

- Directorios de configuración, datos y caché.
- Selector de archivos y carpetas.
- Apertura de URL y autenticación por navegador.
- Inicio de Java/Minecraft, permisos y ejecutables.
- Revelar archivos en el administrador de archivos.
- Decodificación de imágenes y creación de miniaturas.
- Mensajes de error, confirmaciones y acceso al hilo visual.

La implementación Windows debe seguir conservando sus rutas y decisiones actuales salvo que una corrección se apruebe y se pruebe por separado. Los adaptadores de Linux/macOS no deben migrar automáticamente las instalaciones existentes.

## Datos y compatibilidad

La inspección de los servicios confirma que preferencias y estado se guardan como JSON y que las carpetas de instancias, caché, archivos temporales y datos compartidos de Minecraft se construyen a partir de `InstanceService`.

Reglas para proteger esos datos:

1. Mantener nombres de archivos y propiedades serializadas compatibles.
2. No cambiar de ubicación los datos existentes ni copiar/mover instalaciones durante el primer arranque.
3. La nueva ubicación predeterminada de cada sistema debe ser explícita y documentada.
4. Si se detecta una instalación anterior, mostrar una opción de selección/importación en vez de modificarla automáticamente.
5. Cambios de almacenamiento deben ser cancelables y preservar los archivos personales que actualmente protege el instalador de modpacks.
6. Probar lectura de preferencias existentes, JSON incompleto/corrupto, rutas con espacios y cancelación de cambios de carpeta.

## Orden de migración

1. Cerrar el inventario de pantallas, eventos, diálogos, servicios y archivos persistentes.
2. Añadir pruebas de caracterización para los servicios sin UI, sin cambiar aún su comportamiento.
3. Definir interfaces de plataforma y seleccionar implementaciones para cada sistema.
4. Llevar una pantalla a Avalonia y comparar los estados y acciones con la WPF de referencia.
5. Integrar un caso de uso real de extremo a extremo; no dejar la nueva pantalla conectada a datos falsos.
6. Repetir por pantalla, validando también accesibilidad, escala, redimensionado y errores.
7. Probar la instalación y ejecución real de Minecraft en cada sistema objetivo.
8. Publicar artefactos separados únicamente para plataformas que superen los criterios de aceptación.

## Criterios mínimos antes de considerar una plataforma funcional

- [ ] Inicio de sesión Microsoft y cuenta sin conexión.
- [ ] Lectura y guardado de preferencias existentes.
- [ ] Catálogo y descarga de modpacks con manejo de errores.
- [ ] Instalación, verificación, actualización y eliminación de instancias.
- [ ] Selección de Java, memoria y argumentos.
- [ ] Inicio real de Minecraft con consola y control de operaciones.
- [ ] Compatibilidad de almacenamiento y preservación de archivos personales.
- [ ] Galería de capturas, filtros, visor y borrado.
- [ ] Ajustes, temas estacionales y funciones de desarrollador.
- [ ] Pruebas en el sistema operativo objetivo, no solo compilación cruzada.

## Estado

Este documento fija límites técnicos para la siguiente fase. El prototipo Avalonia actual sigue siendo una prueba aislada de publicación: todavía no contiene los servicios reales del launcher y no debe usarse como sustituto funcional.


## Primera extracción implementada

Se creó `NegativeLauncher.Core`, una biblioteca .NET 10 sin referencias a WPF/Avalonia. El modelo `LauncherPreferences` y `LauncherPreferencesService` ahora viven en esa biblioteca y se referencian desde el proyecto Windows y el prototipo Avalonia. El servicio conserva los nombres de propiedades JSON, el archivo `launcher-preferences.json`, los valores predeterminados, los límites de memoria y el guardado temporal antes de reemplazar el archivo.

La ruta predeterminada del servicio compartido sigue siendo `%AppData%/NegativeClient` en Windows, como la ruta que usaba `InstanceService.LauncherRoot`. También se añadió un constructor que permite proporcionar una carpeta de datos explícita, para probar el servicio sin tocar los datos reales del usuario.

Se creó `NegativeLauncher.Core.Tests`, un ejecutable de comprobaciones automatizadas sin paquetes externos, para verificar valores iniciales, lectura JSON insensible a mayúsculas, normalización de campos nulos, límites de RAM, guardado, nombres de propiedades y recuperación ante JSON dañado. El workflow ejecuta esas comprobaciones en Linux y mantiene la compilación de referencia WPF en Windows.

**Estado de validación:** la compilación de Windows y el nuevo job de pruebas están en ejecución. Esta extracción no se considera aprobada hasta que ambos terminen correctamente. El resto de servicios todavía no se ha migrado.


## Segunda extracción implementada: estado de la última instancia

Se movió `LauncherStateService` a `NegativeLauncher.Core` y el proyecto WPF excluye la implementación local anterior para evitar clases duplicadas. La interfaz pública conserva los métodos `GetLastPlayedInstanceIdAsync`, `SetLastPlayedInstanceIdAsync` y `ClearLastPlayedInstanceAsync`.

El servicio conserva el archivo `launcher-state.json`, la propiedad serializada `LastPlayedInstanceId`, el guardado mediante archivo temporal y el comportamiento de devolver `null` cuando no existe un estado válido. El constructor predeterminado utiliza la carpeta de datos de la aplicación; el constructor con ruta explícita permite probarlo sin tocar el perfil real.

Se ampliaron las pruebas automáticas para comprobar la ausencia inicial de estado, guardar y leer el identificador, compatibilidad del JSON, limpieza, recuperación ante JSON dañado y rechazo de identificadores vacíos. La ejecución de GitHub Actions debe confirmar esta extracción antes de darla por validada.


## Tercera extracción: rutas predeterminadas por plataforma

Se añadió `LauncherPaths` en la biblioteca compartida y se conectaron a ella las preferencias, el estado y `InstanceService`. La ruta de Windows sigue siendo `%AppData%/NegativeClient`, igual que en la implementación original. Para macOS se usa `~/Library/Application Support/NegativeClient`; para Linux se usa `XDG_DATA_HOME/NegativeClient` cuando `XDG_DATA_HOME` es absoluto y, en caso contrario, `~/.local/share/NegativeClient`.

Esta resolución solo determina la ubicación predeterminada en cada sistema. No mueve datos existentes, no importa instalaciones automáticamente ni modifica la ruta elegida por el usuario para el almacenamiento de Minecraft. Se añadieron comprobaciones para validar que la ruta sea absoluta, conservar el nombre de carpeta y comprobar en Windows que coincide con la ubicación original.

La siguiente validación de CI debe confirmar tanto la compilación WPF como las pruebas compartidas después de estos cambios.


## Cuarta extracción: mapa de carpetas de almacenamiento

Se añadió `LauncherStorageLayout` a la biblioteca compartida. Su responsabilidad es calcular, sin crear ni mover archivos, las rutas de instancias, caché de paquetes, temporales y los recursos compartidos de Minecraft (assets, libraries, versions y runtime). `InstanceService` utiliza ahora este mapa para resolver esas rutas.

El cambio conserva los nombres y la estructura de carpetas existentes. No se trasladó a la biblioteca la lógica que copia, mueve o elimina directorios: esa operación tiene efectos sobre los datos del usuario y requiere una migración aparte con pruebas específicas de colisiones, cancelación, errores de disco y preservación de archivos.

Se añadieron pruebas para cada subcarpeta y para el rechazo de una raíz vacía. Esta separación es un primer paso para hacer portable el almacenamiento sin reescribir de golpe el servicio completo.

## Quinta extracción: normalización compartida de la ruta de almacenamiento

La normalización de la carpeta de almacenamiento se centralizó en `LauncherStorageLayout.NormalizeRoot`. `InstanceService` delega ahora en esa función, manteniendo la misma regla: una ruta nula o en blanco vuelve a la carpeta predeterminada; una ruta configurada elimina espacios exteriores, se convierte en absoluta y elimina separadores finales.

Se añadieron comprobaciones para la ruta predeterminada, los valores en blanco y las rutas configuradas con espacios. La función compartida solo calcula la ruta: no crea carpetas ni mueve datos. La validación final de este cambio queda pendiente de los workflows de GitHub Actions.

## Sexta extracción: reglas de comparación de rutas

Se creó `LauncherPathRules` en la biblioteca compartida y `InstanceService` delega en ella las comparaciones de igualdad y contención de rutas usadas al cambiar la ubicación del almacenamiento. Se conservó el criterio de comparación que ya utilizaba el servicio, incluido el tratamiento sin distinción entre mayúsculas y minúsculas, para no introducir un cambio funcional inadvertido durante esta extracción.

Las pruebas cubren rutas equivalentes, una carpeta realmente anidada y el caso de nombres que solo comparten un prefijo. Estas funciones son de cálculo: no realizan operaciones de archivos. La compilación y las pruebas de GitHub Actions deben confirmar el cambio antes de darlo por validado.

## Séptima extracción: validar la nueva ubicación antes de mover datos

La comprobación que impide elegir una ubicación dentro de las carpetas actuales de instancias o Minecraft ahora vive en `LauncherPathRules.ValidateStorageRootChange`. `InstanceService` llama a esa validación antes de crear la carpeta de destino o iniciar cualquier movimiento.

Se mantuvieron la condición y el mensaje de error existentes. Las pruebas verifican que una ubicación externa se acepte y que se rechacen destinos dentro de las instancias actuales o de Minecraft compartido. No se cambió el algoritmo que mueve las carpetas; esta extracción aísla y prueba una barrera de seguridad antes de tocar los datos.

## Octava extracción: validación compartida de IDs de instancia

La regla de validación de los IDs de instancia se trasladó a `LauncherPathRules.ValidateInstanceId`. Se conservan los caracteres admitidos (letras, números, guion y guion bajo) y los mensajes de error anteriores; `InstanceService` delega en el método compartido.

Se añadieron pruebas para un ID válido, uno vacío y otro que intenta incluir separadores de ruta. La validación no toca archivos ni cambia el formato de las instancias.

## Novena extracción: modelo de instancia compartido

El modelo `InstalledInstance` ahora vive en `NegativeLauncher.Core`, conservando el namespace, los nombres de propiedades, los valores predeterminados y los tipos de datos. El proyecto WPF excluye su copia local y consume el modelo compartido a través de la referencia al Core; el prototipo Avalonia puede utilizar la misma estructura.

Se añadieron pruebas de serialización y deserialización JSON para comprobar que los identificadores, las versiones, el loader, los indicadores de preparación y los IDs de recursos no se pierdan. Este cambio no altera los archivos de instancia existentes ni ejecuta migraciones.

## Décima extracción: persistencia de instancias

Se añadió `InstanceDataStore` al Core compartido para leer, enumerar y guardar los archivos `instance.json`. `InstanceService` delega ahora en ese servicio y le proporciona la carpeta de instancias vigente, por lo que los cambios de ubicación siguen aplicándose sin fijar una ruta antigua.

Se conservan el nombre `instance.json`, las propiedades JSON, la validación de IDs y el comportamiento de omitir archivos dañados al enumerar instancias. Las pruebas nuevas guardan y recuperan una instancia en una carpeta temporal, verifican la ruta del archivo, comprueban que un JSON dañado no bloquea las demás y rechazan un ID que intente salir del directorio. No se han cambiado las operaciones de mover instalaciones.

