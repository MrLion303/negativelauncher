# Pruebas de Negative Launcher en Linux

Esta guía corresponde a la fase activa de la rama `portabilidad-fiel-0.1.1`. En esta fase se publican paquetes para Linux x64 y Linux ARM64. macOS está aplazado y la rama `fixes/0.1.1-discord-rich-presence` permanece como referencia Windows.

Para descargar e iniciar el paquete, sigue la [guía de prueba rápida de Linux](PRUEBA_RAPIDA_LINUX_MACOS.md). Descarga ambos artefactos de una misma ejecución de GitHub Actions que haya terminado correctamente; no mezcles archivos de ejecuciones distintas.

## Aceptación funcional

Marca cada resultado como aprobado, fallido o no probado. Una compilación en verde no equivale a una aceptación funcional.

- [ ] El paquete se extrae y abre en un escritorio Linux real.
- [ ] La ventana de acceso y la navegación principal funcionan.
- [ ] El perfil sin conexión puede entrar al launcher.
- [ ] Los ajustes se guardan y se recuperan al reiniciar.
- [ ] Java se detecta o instala correctamente, y la ruta personalizada funciona.
- [ ] Una instalación Vanilla de prueba prepara recursos y abre Minecraft.
- [ ] Una instancia Forge de prueba prepara el loader y abre Minecraft.
- [ ] Pausar, reanudar y cancelar operaciones deja la interfaz en un estado correcto.
- [ ] El cierre normal de Minecraft funciona; el cierre forzado solo se ofrece si el proceso no responde.
- [ ] La consola, la galería, la selección de capturas y las operaciones de mantenimiento funcionan sin bloquear el juego ni borrar datos activos.
- [ ] Las rutas de datos con espacios funcionan y la migración de almacenamiento conserva los datos.
- [ ] El inicio de sesión Microsoft regresa del navegador y permite lanzar Minecraft con una cuenta real autorizada.

## Autenticación Microsoft

El acceso premium en Linux requiere un Client ID público de Microsoft Entra autorizado para Xbox/Minecraft. Consulta [AUTENTICACION_MICROSOFT_LINUX_MACOS.md](AUTENTICACION_MICROSOFT_LINUX_MACOS.md). Sin esa configuración, puede probarse la interfaz y el modo sin conexión, pero no debe considerarse validado el acceso premium.

## Cómo reportar un fallo

Incluye el sistema operativo, arquitectura, ID de la ejecución de Actions, pasos para reproducir el problema y resultado esperado/real. El registro local se puede abrir desde **Ajustes → Diagnóstico**. Revísalo antes de compartirlo y elimina rutas privadas u otros datos personales. Nunca compartas contraseñas, tokens OAuth ni archivos de sesión.

## Criterio para declarar Linux terminado

Linux no se considerará terminado hasta completar las pruebas anteriores en hardware real, confirmar la autenticación Microsoft y el lanzamiento de Minecraft, y resolver las diferencias visuales y funcionales que aún existan frente a la referencia Windows. Los resultados de CI validan compilación, pruebas compartidas y arranque automatizado; no sustituyen esa aceptación manual.
