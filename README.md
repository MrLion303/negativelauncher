# Negative Launcher — port multiplataforma

**Rama de desarrollo:** `portabilidad-fiel-0.1.1`  
**Versión base:** 0.1.1  
**Estado:** beta para pruebas; no publicar como versión estable solo porque CI termine correctamente.

Negative Launcher está trasladando su interfaz y funciones actuales a Avalonia, preservando la versión WPF original como referencia funcional y visual. **La fase activa se limita a Linux x64 y Linux ARM64**; macOS no forma parte de los paquetes que se están preparando ahora. El port comparte servicios para cuentas, rutas, almacenamiento, instancias, catálogo y preparación de Minecraft.

## Cómo probarlo

Empieza por la [guía rápida de pruebas](PRUEBA_RAPIDA_LINUX_MACOS.md). En esta fase, sigue únicamente las instrucciones y los artefactos de Linux; las indicaciones para macOS no implican que exista un paquete compatible en la rama activa.

1. Abre [Compilar base multiplataforma de Negative Launcher en GitHub Actions](https://github.com/MrLion303/negativelauncher/actions/workflows/compilar-prototipo-multiplataforma.yml?query=branch%3Aportabilidad-fiel-0.1.1).
2. Selecciona la ejecución más reciente que haya terminado con éxito en todos los trabajos.
3. Descarga el artefacto correspondiente a tu sistema y arquitectura. GitHub entrega los artefactos de Actions como archivos ZIP que contienen el paquete de instalación.
4. Sigue la guía rápida antes de intentar una autenticación Microsoft real o instalar un modpack.

En esta rama, el workflow genera paquetes únicamente para Linux x64 y Linux ARM64. La rama de Windows de referencia se mantiene separada y sin modificaciones; macOS se abordará en una fase posterior. Linux pasa una comprobación de inicio de la interfaz en CI; esto verifica que el proceso inicie y termine la inicialización local, no que el launcher esté validado en todas las distribuciones y equipos físicos.

## Funciones multiplataforma en curso

- Cuentas Microsoft y perfil sin conexión.
- Almacenamiento, instancias y migración de datos con validaciones de rutas.
- Preparación del runtime de Minecraft y selección de Java.
- Instaladores para Vanilla, Forge, NeoForge, Fabric y Quilt.
- Instalación/actualización de modpacks, verificación de integridad, consola, galería de capturas, preferencias, temas y herramientas de desarrollador.
- Paquetes de Linux x64 y Linux ARM64. Los paquetes de macOS y sus formatos de distribución quedan fuera de la fase activa.
- Registro local de diagnóstico en Ajustes; no se envía automáticamente.

La presencia de una función en esta lista no significa que ya haya superado pruebas interactivas de extremo a extremo en todos los sistemas. El primer lanzamiento real de Minecraft y los modpacks aún deben probarse manualmente por plataforma y loader.

## Autenticación Microsoft

El inicio de sesión Microsoft en Linux requiere un **Client ID público de Microsoft Entra válido y autorizado para Xbox/Minecraft**. No se incluye un identificador inventado ni una credencial compartida en el repositorio. Consulta la [guía de autenticación Microsoft para esta fase Linux](AUTENTICACION_MICROSOFT_LINUX_MACOS.md) antes de probar el acceso premium.

Para revisar errores, abre Ajustes → Diagnóstico → **Abrir carpeta de registros**. El archivo puede contener rutas locales o información de la sesión; revisa y censura los datos privados antes de compartirlo. No compartas archivos de cuenta, tokens OAuth ni contraseñas.

## Notas de seguridad e instalación

- No borres manualmente datos del launcher para “arreglar” una prueba; usa una carpeta de almacenamiento temporal si necesitas un entorno limpio.
- El port rechaza ciertos enlaces simbólicos durante migraciones y tareas de limpieza para evitar alterar archivos externos al almacenamiento administrado.
- La publicación de releases es un flujo manual y genera sumas SHA-256. No reemplaza la validación real de inicio de sesión y de lanzamiento de Minecraft.

## Documentación técnica

- [Guía rápida de pruebas](PRUEBA_RAPIDA_LINUX_MACOS.md)
- [Autenticación Microsoft en Linux](AUTENTICACION_MICROSOFT_LINUX_MACOS.md)
- [Matriz funcional y pendientes de portabilidad](MATRIZ_FUNCIONAL_PORTABILIDAD.md)
- [Documentación del prototipo Avalonia](Prototipo%20Avalonia/README.md)

El objetivo de aceptación de esta fase es que Linux conserve la apariencia, las operaciones y los datos del launcher actual, con adaptaciones específicas de plataforma donde sean necesarias. macOS se evaluará por separado después de cerrar la fase Linux. La compilación correcta es un requisito, no la declaración de finalización.
