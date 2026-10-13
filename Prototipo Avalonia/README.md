# Negative Launcher para Linux

Esta aplicación Avalonia es la base para portar Negative Launcher 0.1.1 a Linux. El trabajo de macOS queda aplazado para una fase posterior. El launcher original de Windows se conserva en `Negative Client/Negative Client` y sigue siendo la referencia visual y funcional.

**Para instalarlo y hacer la primera prueba:** consulta [PRUEBA_RAPIDA_LINUX_MACOS.md](../PRUEBA_RAPIDA_LINUX_MACOS.md), que se está usando como guía Linux durante esta fase.

## Objetivo de esta rama

- Trabajar desde `fixes/0.1.1-discord-rich-presence`, que contiene el launcher actual.
- Mantener el código WPF original y sus cambios recientes intactos.
- Reutilizar los servicios compartidos en `NegativeLauncher.Core` para evitar duplicar lógica de cuentas, almacenamiento, instancias, catálogo y runtime.
- Portar cada pantalla y acción de forma deliberada, contrastándola con el original.
- Generar paquetes Linux autocontenidos mediante GitHub Actions para x64 (Intel/AMD) y ARM64.

## Compilación

El workflow `.github/workflows/compilar-prototipo-multiplataforma.yml` publica los artefactos autocontenidos solo para Linux x64 y Linux ARM64 y ejecuta las pruebas de lógica compartida en Linux.

- Linux: archivo TAR.GZ con la publicación correspondiente a la arquitectura.
- El aviso de actualización solo se muestra si la release contiene el paquete Linux compatible con la arquitectura actual; no dirige a instaladores de otro sistema.
- La interfaz prioriza `gg sans` si está instalada y usa Inter como alternativa incluida para mantener una tipografía consistente en instalaciones Linux limpias.

## Estado real de paridad

La compilación de Linux no significa que el port esté terminado. La interfaz Avalonia sigue siendo una implementación en curso y todavía requiere comparar pantalla por pantalla la distribución, tipografía, fondos, controles, ventanas secundarias y estados del launcher WPF 0.1.1. También deben probarse en los sistemas de destino el inicio de sesión Microsoft, la instalación y actualización de modpacks, la autenticación, la preparación y el lanzamiento de Minecraft, y las operaciones de capturas.

No reemplazar ni simplificar la interfaz original para declarar el port terminado. La aceptación de esta fase requiere que el launcher de Linux conserve la apariencia y el comportamiento del launcher actual de Windows, salvo las adaptaciones estrictamente necesarias por plataforma.
## Paridad con el launcher de Windows

La pantalla inicial de acceso se ha incorporado al prototipo multiplataforma con las dos rutas de la versión Windows: autenticación Microsoft y perfil sin conexión. Cuando no existe una sesión válida, el acceso se muestra como una capa completa y bloquea el cambio de tamaño; al completar el acceso, vuelve a habilitarse la ventana normal.

Antes de distribuirlo deben probarse en equipos reales: login Microsoft y retorno del navegador, descarga/actualización de modpacks, ejecución de Minecraft/Forge, consola, galería de capturas, preferencias y cierre/reinicio. La fase actual valida únicamente Linux x64 y Linux ARM64; no se ejecutan trabajos de publicación para Windows ni macOS en este flujo. Esto valida compilación y pruebas automatizadas, pero no sustituye las pruebas interactivas en equipos físicos. El lanzamiento normal ahora reutiliza los servicios originales para aplicar la selección de resource packs del modpack y preparar o desactivar la skin local según la cuenta, cancelando el inicio si falta un recurso requerido. El botón Jugar también respeta la exclusión de instancias de Windows: si Minecraft sigue abierto desde otra instalación, muestra un aviso y no intenta cerrarlo como si fuera la instancia seleccionada. Si el proceso ya terminó, limpia el estado pendiente antes de continuar. El guardado de preferencias ahora serializa escrituras concurrentes con un bloqueo compartido y usa archivos temporales únicos antes del reemplazo atómico. Si el JSON de preferencias está dañado, se intenta conservar una copia `.corrupt-*` y restaurar valores predeterminados sin sobrescribir el único archivo original cuando no se puede crear la copia. Al cambiar la ubicación de almacenamiento entre volúmenes, la migración copia primero a una carpeta provisional en el destino y solo la publica cuando termina; el origen se elimina después. En Linux, la detección del volumen compara los puntos de montaje más específicos en vez de asumir que todas las rutas bajo `/` pertenecen al mismo disco.


## Contadores globales y temas de temporada: primer pase

El prototipo ahora consulta el feed compartido de contadores globales, muestra los contadores activos y vigentes en la parte superior del área de contenido, actualiza la cuenta regresiva local cada segundo y vuelve a consultar el estado remoto periódicamente. Al visitar Galería o Ajustes, los avisos se muestran con opacidad reducida, como en la referencia WPF. La actualización conserva el último estado recibido si falla temporalmente la red, según el servicio compartido.

También se trasladó una primera capa de los temas estacionales automáticos, usando la fecha de Monterrey (UTC-6), los periodos definidos por el launcher original y la preferencia existente para activarlos o desactivarlos. Esta capa aplica tinte al fondo de Inicio, color al botón JUGAR y franjas decorativas superior e inferior. También incluye una vista previa de temas para desarrollador cuando están activados tanto el modo desarrollador como los temas festivos. Los contadores y los temas deben comprobarse visualmente en ejecución, además de compilar.


### Efectos estacionales animados

Los fuegos artificiales se activan el 31 de diciembre y el 1 de enero; los globos, el 27 de marzo. Los efectos se generan en una capa no interactiva y se detienen cuando se desactivan los temas o termina el periodo correspondiente. Es una implementación multiplataforma de primer pase, no una reproducción píxel por píxel del sistema WPF.


## Acceso protegido al modo desarrollador

El modo desarrollador no reutiliza las credenciales fijas del launcher WPF antiguo. Para habilitarlo, configura una contraseña en la variable de entorno `NEGATIVE_LAUNCHER_DEV_PASSWORD` antes de abrir el launcher. El nombre de usuario esperado es `admin` por defecto; puede cambiarse con `NEGATIVE_LAUNCHER_DEV_USERNAME`. Si la contraseña no está configurada, el modo desarrollador permanece deshabilitado.

En Windows, define las variables desde la configuración de variables de entorno de tu usuario y vuelve a iniciar el launcher. En Linux, expórtalas desde el entorno que utilices para ejecutar la aplicación. No guardes la contraseña en el repositorio, en los ajustes del launcher ni en argumentos de línea de comandos.

## Guardado automático de ajustes

Los cambios de memoria RAM, Java, argumentos personalizados, consola, cierre al iniciar, temas de temporada y catálogo de modpacks se guardan automáticamente tras una breve pausa al dejar de editar. La ruta de almacenamiento y el modo desarrollador conservan el botón **Guardar ajustes**, ya que implican operaciones adicionales y no deben aplicarse silenciosamente mientras se escribe.


## Limpieza avanzada de archivos no utilizados

En Ajustes, **Limpiar archivos no utilizados** analiza las versiones de Minecraft, runtimes de Java y copias antiguas por instancia, conserva los componentes requeridos por las instalaciones activas y solicita confirmación antes de eliminar las carpetas detectadas. La operación se bloquea mientras hay una instalación o actualización de modpack en curso y actualiza el cálculo de almacenamiento al terminar.

### Cancelación de Minecraft Vanilla

Durante la preparación de una versión Vanilla, el panel de desarrollador muestra un control para cancelar la operación. La cancelación utiliza el mismo token que el servicio de preparación; no interrumpe por la fuerza un proceso de Minecraft que ya se haya iniciado. Los errores al guardar los filtros o la versión seleccionada se muestran en la interfaz en lugar de quedar como excepciones no controladas.


## Publicar una release Linux

El flujo `.github/workflows/publicar-release-multiplataforma.yml` publica manualmente paquetes Linux de una ejecución validada. No publica cada commit automáticamente.

1. Abre **Actions → Compilar Negative Launcher para Linux** y confirma que la ejecución haya terminado con todos los trabajos en verde.
2. Copia el ID numérico de esa ejecución y abre **Actions → Publicar release Linux → Run workflow**.
3. Introduce ese ID y una etiqueta que coincida con `<Version>` del proyecto, por ejemplo `v0.1.1`. Marca `prerelease` solo para una compilación preliminar.
4. El flujo comprueba la ejecución de origen, la rama y la versión, descarga los dos paquetes Linux y calcula sus SHA-256 antes de crear la release.

La release contiene `NegativeLauncher-linux-x64.tar.gz` y `NegativeLauncher-linux-arm64.tar.gz`, junto con `SHA256SUMS.txt`. Las etiquetas existentes no se sobrescriben.

**No publiques una release estable solo porque CI haya pasado.** Antes, confirma el acceso Microsoft y el inicio real de Minecraft en un equipo Linux.

## Prueba rápida del port Linux

Descarga el artefacto de la ejecución verde más reciente de la rama `portabilidad-fiel-0.1.1`. GitHub entrega el artefacto como ZIP; descomprímelo y extrae el TAR.GZ incluido en una carpeta permanente.

- Linux Intel/AMD de 64 bits: `NegativeLauncher-linux-x64.tar.gz`.
- Linux ARM de 64 bits: `NegativeLauncher-linux-arm64.tar.gz`.

Desde la carpeta extraída, ejecuta `./iniciar-negative-launcher.sh`. Para crear un acceso en el menú de aplicaciones, ejecuta opcionalmente `./instalar-acceso-escritorio.sh`; no necesita permisos de administrador.

Comprueba el arranque y cierre, Inicio, Galería, Ajustes, selectores del sistema, ajustes de RAM/Java y persistencia de preferencias. Después prueba el perfil sin conexión y, si tienes un Client ID autorizado, el inicio de sesión Microsoft y el lanzamiento de Minecraft. La autenticación y la descarga de runtimes requieren conexión a Internet.

Para usar una cuenta Microsoft en Linux se requiere configurar un Client ID de una aplicación pública de Microsoft Entra autorizada para Xbox/Minecraft. Consulta [AUTENTICACION_MICROSOFT_LINUX_MACOS.md](../AUTENTICACION_MICROSOFT_LINUX_MACOS.md). No compartas contraseñas, tokens OAuth ni archivos de sesión.

Si falla, registra distribución, arquitectura, el paso exacto y el archivo `negative-launcher.log`. Revisa el registro y elimina rutas privadas u otros datos personales antes de compartirlo.

## Validación y límites actuales (12 de octubre de 2026)

- El flujo de publicación de esta rama ejecuta únicamente Linux x64 y Linux ARM64. La rama `fixes/0.1.1-discord-rich-presence` se conserva como referencia de Windows; el port de macOS se retomará más adelante.
- El flujo de acceso incluye autenticación Microsoft interactiva y perfil sin conexión. Falta validar el retorno del navegador y las sesiones reales en un equipo Linux.
- La instalación de modpacks prepara el runtime; el lanzamiento normal prepara los resource packs requeridos y la skin local según el perfil.
- Cuando se selecciona Java manual, el port valida la ruta y los permisos de ejecución antes de crear la instancia o descargar Minecraft; una ruta vacía o inexistente muestra una indicación concreta para corregir Ajustes.
- Si se vuelve a preparar una instancia, el estado de runtime anterior se invalida antes de comenzar las descargas. Si la nueva preparación falla o se cancela, el launcher no debe intentar iniciar silenciosamente una versión anterior con el perfil recién modificado.
- Al instalar o actualizar un modpack, la cancelación se comprueba antes de modificar la instancia. Una vez aplicados los archivos, se termina de guardar el manifiesto de archivos administrados para no dejar una actualización aplicada con metadatos antiguos. Ese manifiesto se reemplaza de forma atómica para reducir el riesgo de que un cierre inesperado deje el archivo truncado. Una diferencia deliberada respecto a una optimización reciente de Windows es la comparación de archivos existentes: Linux sigue comprobando SHA-256, porque tamaño y fecha iguales no garantizan que el contenido sea idéntico. Así se evita omitir una actualización válida si un archivo cambió conservando ambos metadatos.
- La consola de Minecraft solo redirige stdout/stderr cuando está activada. Si se desactiva, el proceso Java conserva sus streams normales para evitar que buffers sin consumir bloqueen el juego durante sesiones largas. Al activarla, la ventana o el drenaje se conectan antes de esperar el guardado del estado de la última instancia.
- Al cargar instancias, el almacén comprueba que el ID guardado en `instance.json` coincida con el nombre de la carpeta y rechaza IDs inseguros o archivos copiados a una ubicación ajena. Esto evita que datos inconsistentes hagan que una carpeta se interprete como otra instancia.
- El estado de la última instancia también valida los IDs al leer y guardar. Un `launcher-state.json` manipulado o dañado que contenga rutas como `../fuera` se ignora, y la selección de la última instancia distingue mayúsculas y minúsculas para respetar los nombres válidos en Linux.
- La galería admite filtros, orden, visor, selección, eliminación y copia de capturas al portapapeles. Su escáner omite directorios inaccesibles y evita seguir enlaces simbólicos.
- La barra superior consulta GitHub Releases de forma opcional y ofrece un aviso enlazado si hay una versión más reciente con paquete Linux compatible para la arquitectura actual. Si la release no incluye ese archivo, no muestra un aviso de descarga ambiguo.
- [La ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183) pasó en Linux x64 y Linux ARM64: pruebas del núcleo, compilación, arranque gráfico, recorrido de interfaz y prueba de los paquetes extraídos. El recorrido automatizado comprueba acceso local cuando aplica, pantallas principales, diálogo de instalación de modpack cancelado, ajuste de la regla de cierre del selector rápido, Vanilla de desarrollador y apertura/cierre de Configuración.
- Siguen pendientes la comparación visual píxel por píxel con WPF y las pruebas de extremo a extremo con una cuenta Microsoft real, Java, Forge/Fabric/NeoForge/Quilt y Minecraft dentro de un escritorio Linux real. La prueba automatizada usa Xvfb y no demuestra el funcionamiento con una GPU o escritorio de usuario.

## Dependencias y portapapeles

La galería usa la API tipada del portapapeles de Avalonia para copiar capturas como imágenes. Los paquetes de Avalonia se fijan en la misma versión estable y `Tmds.DBus.Protocol` se fija en `0.21.3`, versión con la corrección de seguridad para evitar restaurar la versión vulnerable transitiva `0.21.2`. La validación CI debe confirmar la resolución efectiva de dependencias y la compilación de ambos destinos Linux antes de considerar cerrada esta corrección.


La corrección del portapapeles, la actualización de Avalonia y el pin de seguridad de `Tmds.DBus.Protocol 0.21.3` están validados por las comprobaciones de compilación y dependencias de ambos destinos en [la ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183). La copia real al portapapeles y el comportamiento visual siguen necesitando comprobación en un escritorio Linux.


## Icono del escritorio Linux

El paquete Linux registra el logo en el acceso de escritorio y copia el PNG a la jerarquía de iconos XDG del usuario. La publicación Linux comprueba que los recursos del acceso de escritorio existan antes de subir los artefactos.


La comprobación adicional de recursos inspecciona las referencias `Assets/...` de los archivos XAML y C#, respeta las mayúsculas de Linux y valida la cabecera y las dimensiones de los PNG principales, además de la cabecera ICO. Esto evita publicar un paquete que compile pero pierda imágenes por un nombre incorrecto o un archivo gráfico truncado. Esta validación está añadida al workflow; su resultado debe confirmarse en la ejecución de GitHub Actions posterior al cambio.


En el tema de aniversario, los globos flotantes se pueden pulsar para generar una pequeña ráfaga de confeti. La capa de efectos solo recibe interacciones mientras se muestra ese tema; en los demás estados permanece transparente al ratón para no bloquear los controles del launcher.


## Fondo del modo desarrollador

Al abrir la página Minecraft Vanilla, el prototipo carga `Assets/minecraft_vanilla.png` en una capa independiente y conserva el oscurecimiento que mantiene legible el contenido. Al abandonar la página o desactivar el modo desarrollador, limpia la imagen y libera el bitmap; no toca el fondo de Inicio ni el fondo dinámico de la instancia seleccionada.


### Inicio de sesión Microsoft en Linux

El proveedor OAuth predeterminado de CmlLib solo se usa en Windows. En Linux, Negative Launcher utiliza XboxAuthNet.Game.Msal para autenticarse con MSAL y conservar el mismo gestor de cuentas local.

Para habilitarlo, registra una aplicación pública en Microsoft Entra que admita cuentas personales y el redirect http://localhost, habilita los flujos de cliente público y tramita la autorización/allowlist requerida para el acceso de Minecraft. Introduce el Application (client) ID en el panel de autenticación de la pantalla de acceso antes del primer inicio de sesión; también se puede actualizar desde Ajustes → Cuentas. El ID es público y no es una contraseña.

Para despliegues administrados se admite la variable de entorno NEGATIVE_LAUNCHER_MSAL_CLIENT_ID, que tiene prioridad sobre la preferencia local. El repositorio no incluye un Client ID inventado: el inicio de sesión real requiere un registro válido y autorizado, así que CI puede validar la compilación pero no completar una autenticación con una cuenta Minecraft real.


## Autenticación Microsoft en Linux

El acceso Microsoft en Linux usa MSAL y requiere un Client ID público válido y autorizado para Xbox/Minecraft. La guía paso a paso está en [AUTENTICACION_MICROSOFT_LINUX_MACOS.md](../AUTENTICACION_MICROSOFT_LINUX_MACOS.md). También se puede abrir desde el panel de acceso o desde Ajustes → Cuentas.

CI no puede realizar el login real con una cuenta Minecraft: la prueba manual sigue pendiente hasta configurar un Client ID permitido y validar el retorno del navegador en un equipo Linux.


## Recorrido gráfico ampliado (12 de octubre de 2026)

El smoke test de Linux también abre y cierra el selector rápido de cuentas dentro de la página Vanilla de desarrollador y comprueba que Ajustes expone el panel de Client ID OAuth en Linux. Estos pasos verifican la estructura y navegación sin simular ni declarar una autenticación Microsoft real. Se ejecutan en ambos destinos Linux y sobre los paquetes extraídos; la comparación visual en escritorio físico y el flujo OAuth real siguen siendo pruebas externas pendientes.


### Matriz de pruebas de lógica compartida

El workflow multiplataforma ejecuta las pruebas de `NegativeLauncher.Core.Tests` en Linux x64, Linux ARM64 y Windows x64. Así se detectan regresiones en rutas, migración de archivos, preparación de instancias y servicios compartidos que podrían pasar inadvertidas si solo se ejecutan en un sistema. La inclusión de Windows en la matriz queda pendiente de confirmación en el resultado de Actions tras el cambio.

### Resolución de Java en Linux

La búsqueda de Java en PATH ahora normaliza cada candidato a una ruta absoluta antes de devolverlo. Así, si el PATH contiene una entrada relativa, el launcher no depende de que el directorio de trabajo siga siendo el mismo cuando se inicia la comprobación o Minecraft. Las entradas inválidas se omiten para que una ruta defectuosa no bloquee las siguientes. Se añadió una prueba automatizada para este caso; debe confirmarse su resultado en la ejecución CI posterior al cambio.

### Lanzamiento de Java en rutas multiplataforma

El servicio de ejecución normaliza a rutas absolutas el Java encontrado en el runtime de Minecraft antes de devolverlo al proceso de juego. La misma regla se aplica al Java localizado después de preparar los archivos: runtime descargado, ruta predeterminada de CmlLib o PATH del sistema. Las pruebas de Core cubren las entradas relativas y las entradas PATH inválidas, y ahora también ejecutan un script de prueba real después de reparar sus permisos Unix. Se mantiene la comprobación de que no se amplíen los permisos de grupo ni se cambie el destino de un enlace simbólico. Esto reduce los fallos cuando Minecraft se inicia desde el directorio de la instancia. El resultado de CI de estos commits aún debe confirmarse.

### Estabilidad de la consola de juego

La consola gráfica agrupa registros de stdout y stderr antes de actualizar el control de texto, conserva una cola limitada de líneas y mantiene los primeros mensajes aunque lleguen antes de mostrarse la ventana. Cuando la consola está desactivada, ambas rutas de lanzamiento usan el mismo drenaje seguro para impedir que se llenen los pipes del proceso de Java. La validación de compilación no sustituye una sesión prolongada de Minecraft en un escritorio Linux.


### Contrato ampliado de controles

La validación de CI compara ahora 60 equivalencias de controles con la ventana WPF de referencia, incluyendo logo, fondos, galería, cuenta rápida, avisos, selección de versión y acceso Microsoft/offline. Esta protección detecta pérdidas de controles o renombrados sin correspondencia, pero no reemplaza las pruebas visuales y funcionales en un escritorio Linux real.


### Navegación en el smoke test

La prueba gráfica automatizada pulsa los botones reales de Inicio, Instancias y Galería, y espera a que se muestre cada página. Así se comprueba el cableado de los eventos además de la construcción de las vistas; el renderizado de escritorio real sigue pendiente.


### Límites de la consola y control de paleta

Además de limitar la cola pendiente y el texto visible, la consola trunca líneas individuales superiores a 16.384 caracteres para evitar que un único registro desmesurado consuma memoria excesiva. La validación visual automatizada también comprueba más colores compartidos con la referencia WPF; una comparación completa de capturas sigue pendiente.


### Botón real de Configuración

La prueba de interfaz abre la ventana de Configuración mediante el botón de navegación de la aplicación y comprueba que el contenido vuelve al contenedor principal después del cierre. Esto añade una comprobación de evento real al recorrido Linux automatizado.


### Diálogo de instalación mediante el botón lateral

El smoke test pulsa el botón + real de la navegación para abrir el diálogo de instalación de modpacks y lo cancela. Esto comprueba el evento de usuario sin disparar descargas, tocar el catálogo ni requerir conectividad externa.


### Comparación de propiedades visuales concretas

La validación de CI compara valores exactos de 43 propiedades en ocho controles emparejados con WPF: panel central, título, texto de instancia seleccionada, panel Vanilla, logo y botones de galería. Comprueba dimensiones, márgenes, colores, tipografía y bordes cuando las propiedades son comparables entre los dos frameworks.


### Visor de capturas alineado con Windows

El visor vuelve a utilizar flechas de navegación laterales, cabecera con acciones de eliminar y cerrar, imagen centrada y metadatos debajo de la imagen. También se alinean el tamaño del título y el color de los metadatos con WPF. El contrato automatizado compara ahora 86 propiedades de 17 controles equivalentes.


El visor usa `StrokeLineCap="Round"` de Avalonia para los extremos redondeados de las flechas; no se reutilizan propiedades de trazado específicas de WPF que impiden compilar el XAML multiplataforma.


### Ajustes de equivalencias y login

Se corrigieron varios destinos del contrato de paridad para apuntar a los controles visibles que cumplen la función equivalente. También se alinearon las alturas de los botones de login con Windows: Microsoft 46 px, Entrar 44 px y Volver 38 px. La validación compara ahora 105 propiedades en 22 controles.


### Panel de progreso visible durante las operaciones

Se corrigió un problema funcional: el mensaje y la barra globales estaban dentro de un contenedor permanentemente oculto. Ahora el ciclo de operación muestra el panel, el mensaje, la barra y las acciones de detener/pausar; al terminar, se restablece el botón Jugar. El smoke test cubre ambos estados y CI compara 120 propiedades en 28 controles.


La prueba gráfica también comprueba que la barra de progreso está visible durante una operación simulada, refleja un valor y se oculta al terminar. La comparación visual contempla que el relleno se expresa como `Background` en WPF y como `Foreground` en el ProgressBar de Avalonia.


### Espaciado de inicio de sesión

Se eliminaron separaciones globales de Avalonia que se sumaban a los márgenes explícitos de los controles, y se alinearon los márgenes del logo y del eslogan con la referencia WPF. La validación visual definitiva requiere comparar capturas en escritorio real.


### Controles flotantes de cuenta

CI también compara las propiedades visuales de la advertencia de cuenta y del contenedor del selector rápido para detectar cambios accidentales de alineación, márgenes, padding, color o tamaño respecto a WPF.


### Transiciones de la pantalla de acceso

El smoke test gráfico comprueba los estados visibles de la pantalla de acceso antes y después de elegir el modo sin conexión. Esto detecta regresiones en las transiciones de paneles sin necesitar credenciales Microsoft reales.


### Volver a las opciones de cuenta

El recorrido gráfico prueba también el botón Volver, verifica que restaure el panel de opciones y vuelve a abrir el formulario sin conexión antes de completar el acceso. Así se comprueba que la transición entre ambos estados funciona en ambos sentidos.


### Controles críticos de Configuración

El recorrido de interfaz en CI ahora comprueba también los estados dependientes de Java automático y de los argumentos JVM personalizados, así como la actualización de la etiqueta de memoria RAM. La prueba restaura los valores previos antes de cerrar Configuración. El recorrido completo y la compilación de Linux x64 y ARM64 pasaron en [la ejecución 38315645391](https://github.com/MrLion303/negativelauncher/actions/runs/38315645391). La revisión visual final en escritorios reales sigue pendiente.


### Rutas personalizadas de Java

Antes de preparar o iniciar Minecraft, el launcher convierte la ruta configurada de Java en una ruta absoluta y comprueba que el archivo exista. Esto evita que una ruta relativa se interprete desde la carpeta de la instancia después de cambiar el directorio de trabajo del proceso. Las pruebas de rutas y la validación de los paquetes Linux x64/ARM64 pasaron en [la ejecución 38315949610](https://github.com/MrLion303/negativelauncher/actions/runs/38315949610). El inicio real de Minecraft sigue pendiente de validación manual.


### Validación de errores de autenticación y cancelación

El recorrido gráfico Linux comprueba que un Client ID mal formado no se guarda y que el inicio de sesión Microsoft se bloquea con instrucciones claras cuando falta la configuración. Las pruebas del núcleo también cubren la cancelación y disposición del controlador de operaciones. La compilación, las pruebas compartidas, el arranque gráfico y la ejecución de los paquetes extraídos pasaron en Linux x64 y ARM64 en [la ejecución 38321221843](https://github.com/MrLion303/negativelauncher/actions/runs/38321221843); la referencia Windows x64 pasó en [la ejecución 38321221912](https://github.com/MrLion303/negativelauncher/actions/runs/38321221912). La autenticación Microsoft real, la comparación visual píxel por píxel y el inicio de Minecraft en un escritorio Linux siguen pendientes.


## Paridad visual de fondos personalizados

El workflow protege el mapeo WPF/Avalonia del fondo personalizado de cada instancia y valida caché, protección frente a selección obsoleta, overlay y restauración del panel central. Estas comprobaciones pasaron en [CI 38321476957](https://github.com/MrLion303/negativelauncher/actions/runs/38321476957). La inspección visual manual en escritorio Linux real sigue pendiente.
