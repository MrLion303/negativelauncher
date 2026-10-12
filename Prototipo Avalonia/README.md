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

Antes de distribuirlo deben probarse en equipos reales: login Microsoft y retorno del navegador, descarga/actualización de modpacks, ejecución de Minecraft/Forge, consola, galería de capturas, preferencias y cierre/reinicio. La fase actual valida únicamente Linux x64 y Linux ARM64; no se ejecutan trabajos de publicación para Windows ni macOS en este flujo. Esto valida compilación y pruebas automatizadas, pero no sustituye las pruebas interactivas en equipos físicos. El lanzamiento normal ahora reutiliza los servicios originales para aplicar la selección de resource packs del modpack y preparar o desactivar la skin local según la cuenta, cancelando el inicio si falta un recurso requerido.


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
- La galería admite filtros, orden, visor, selección, eliminación y copia de capturas al portapapeles. Su escáner omite directorios inaccesibles y evita seguir enlaces simbólicos.
- La barra superior consulta GitHub Releases de forma opcional y ofrece un aviso enlazado si hay una versión más reciente con paquete Linux compatible para la arquitectura actual. Si la release no incluye ese archivo, no muestra un aviso de descarga ambiguo.
- [La ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183) pasó en Linux x64 y Linux ARM64: pruebas del núcleo, compilación, arranque gráfico, recorrido de interfaz y prueba de los paquetes extraídos. El recorrido automatizado comprueba acceso local cuando aplica, pantallas principales, diálogo de instalación de modpack cancelado, ajuste de la regla de cierre del selector rápido, Vanilla de desarrollador y apertura/cierre de Configuración.
- Siguen pendientes la comparación visual píxel por píxel con WPF y las pruebas de extremo a extremo con una cuenta Microsoft real, Java, Forge/Fabric/NeoForge/Quilt y Minecraft dentro de un escritorio Linux real. La prueba automatizada usa Xvfb y no demuestra el funcionamiento con una GPU o escritorio de usuario.

## Dependencias y portapapeles

La galería usa la API tipada del portapapeles de Avalonia para copiar capturas como imágenes. Los paquetes de Avalonia se fijan en la misma versión estable y `Tmds.DBus.Protocol` se fija en `0.21.3`, versión con la corrección de seguridad para evitar restaurar la versión vulnerable transitiva `0.21.2`. La validación CI debe confirmar la resolución efectiva de dependencias y la compilación de ambos destinos Linux antes de considerar cerrada esta corrección.


La corrección del portapapeles, la actualización de Avalonia y el pin de seguridad de `Tmds.DBus.Protocol 0.21.3` están validados por las comprobaciones de compilación y dependencias de ambos destinos en [la ejecución 38221613183](https://github.com/MrLion303/negativelauncher/actions/runs/38221613183). La copia real al portapapeles y el comportamiento visual siguen necesitando comprobación en un escritorio Linux.


## Icono del escritorio Linux

El paquete Linux registra el logo en el acceso de escritorio y copia el PNG a la jerarquía de iconos XDG del usuario. La publicación Linux comprueba que los recursos del acceso de escritorio existan antes de subir los artefactos.


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


### Estabilidad de la consola de juego

La consola gráfica agrupa registros de stdout y stderr antes de actualizar el control de texto, conserva una cola limitada de líneas y mantiene los primeros mensajes aunque lleguen antes de mostrarse la ventana. Cuando la consola está desactivada, ambas rutas de lanzamiento usan el mismo drenaje seguro para impedir que se llenen los pipes del proceso de Java. La validación de compilación no sustituye una sesión prolongada de Minecraft en un escritorio Linux.


### Contrato ampliado de controles

La validación de CI compara ahora 60 equivalencias de controles con la ventana WPF de referencia, incluyendo logo, fondos, galería, cuenta rápida, avisos, selección de versión y acceso Microsoft/offline. Esta protección detecta pérdidas de controles o renombrados sin correspondencia, pero no reemplaza las pruebas visuales y funcionales en un escritorio Linux real.


### Navegación en el smoke test

La prueba gráfica automatizada pulsa los botones reales de Inicio, Instancias y Galería, y espera a que se muestre cada página. Así se comprueba el cableado de los eventos además de la construcción de las vistas; el renderizado de escritorio real sigue pendiente.
