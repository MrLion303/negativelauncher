# Negative Launcher — port multiplataforma

**Rama de desarrollo:** `portabilidad-fiel-0.1.1`  
**Versión base:** 0.1.1  
**Estado:** beta para pruebas; no publicar como versión estable solo porque CI termine correctamente.

Negative Launcher está trasladando su interfaz y funciones actuales a Avalonia para poder ejecutarse en Linux y macOS, preservando la versión WPF original como referencia funcional y visual. El port comparte servicios para cuentas, rutas, almacenamiento, instancias, catálogo y preparación de Minecraft.

## Cómo probarlo

Empieza por la [guía rápida para Linux y macOS](PRUEBA_RAPIDA_LINUX_MACOS.md). Explica qué artefacto descargar, cómo instalarlo, cómo recoger los registros y cómo hacer una primera prueba local de Minecraft Vanilla.

1. Abre [Compilar base multiplataforma de Negative Launcher en GitHub Actions](https://github.com/MrLion303/negativelauncher/actions/workflows/compilar-prototipo-multiplataforma.yml?query=branch%3Aportabilidad-fiel-0.1.1).
2. Selecciona la ejecución más reciente que haya terminado con éxito en todos los trabajos.
3. Descarga el artefacto correspondiente a tu sistema y arquitectura. GitHub entrega los artefactos de Actions como archivos ZIP que contienen el paquete de instalación.
4. Sigue la guía rápida antes de intentar una autenticación Microsoft real o instalar un modpack.

La matriz genera paquetes para Windows x64, Linux x64, Linux ARM64, macOS Intel y macOS Apple Silicon. Linux/macOS pasan una comprobación de inicio de la interfaz en CI; esto verifica que el proceso inicie y termine la inicialización local, no que el launcher esté validado en todas las distribuciones y equipos físicos.

## Funciones multiplataforma en curso

- Cuentas Microsoft y perfil sin conexión.
- Almacenamiento, instancias y migración de datos con validaciones de rutas.
- Preparación del runtime de Minecraft y selección de Java.
- Instaladores para Vanilla, Forge, NeoForge, Fabric y Quilt.
- Instalación/actualización de modpacks, verificación de integridad, consola, galería de capturas, preferencias, temas y herramientas de desarrollador.
- Paquetes nativos de Linux y bundles `.app` de macOS, con ZIP y DMG para macOS.
- Registro local de diagnóstico en Ajustes; no se envía automáticamente.

La presencia de una función en esta lista no significa que ya haya superado pruebas interactivas de extremo a extremo en todos los sistemas. El primer lanzamiento real de Minecraft y los modpacks aún deben probarse manualmente por plataforma y loader.

## Autenticación Microsoft

El inicio de sesión Microsoft en Linux/macOS requiere un **Client ID público de Microsoft Entra válido y autorizado para Xbox/Minecraft**. No se incluye un identificador inventado ni una credencial compartida en el repositorio. Consulta la [guía de autenticación Microsoft en Linux/macOS](AUTENTICACION_MICROSOFT_LINUX_MACOS.md) antes de probar el acceso premium.

Para revisar errores, abre Ajustes → Diagnóstico → **Abrir carpeta de registros**. El archivo puede contener rutas locales o información de la sesión; revisa y censura los datos privados antes de compartirlo. No compartas archivos de cuenta, tokens OAuth ni contraseñas.

## Notas de seguridad e instalación

- No borres manualmente datos del launcher para “arreglar” una prueba; usa una carpeta de almacenamiento temporal si necesitas un entorno limpio.
- El port rechaza ciertos enlaces simbólicos durante migraciones y tareas de limpieza para evitar alterar archivos externos al almacenamiento administrado.
- Los DMG generados por CI no están firmados ni notarizados. macOS puede mostrar advertencias de Gatekeeper. Verifica que el artefacto proceda de este repositorio antes de permitir su ejecución.
- La publicación de releases es un flujo manual y genera sumas SHA-256. No reemplaza la validación real de inicio de sesión y de lanzamiento de Minecraft.

## Documentación técnica

- [Guía rápida de pruebas](PRUEBA_RAPIDA_LINUX_MACOS.md)
- [Autenticación Microsoft en Linux/macOS](AUTENTICACION_MICROSOFT_LINUX_MACOS.md)
- [Matriz funcional y pendientes de portabilidad](MATRIZ_FUNCIONAL_PORTABILIDAD.md)
- [Documentación del prototipo Avalonia](Prototipo%20Avalonia/README.md)

El objetivo de aceptación es que Linux y macOS conserven la apariencia, las operaciones y los datos del launcher actual, con adaptaciones específicas de plataforma donde sean necesarias. La compilación correcta es un requisito, no la declaración de finalización.
