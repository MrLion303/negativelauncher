# Portabilidad de Negative Launcher

## Objetivo

Preparar versiones para Windows, Linux y macOS sin retirar funciones ni simplificar el comportamiento de la versión actual.

## Estado de partida

- La aplicación actual usa C# y WPF (`net10.0-windows`).
- WPF depende de Windows; no basta con cambiar el Runtime Identifier para obtener versiones funcionales para Linux o macOS.
- La interfaz está repartida entre varias ventanas XAML y archivos code-behind parciales de gran tamaño. La ventana principal y las pantallas de ajustes contienen una cantidad importante de diseño y lógica.
- La lógica de cuentas usa CmlLib.Core y CmlLib.Core.Auth.Microsoft.
- La gestión de instancias usa rutas derivadas de `Environment.SpecialFolder.ApplicationData`, que habrá que revisar para respetar las ubicaciones habituales de cada sistema operativo.
- El lanzamiento de Minecraft y la instalación de modpacks deben validarse por sistema operativo; compilar el launcher no demuestra que esos flujos funcionen.

## Reglas de migración

1. No modificar la rama `main` durante el trabajo de portabilidad.
2. No eliminar funciones ni reemplazarlas por implementaciones de menor alcance.
3. Mantener los servicios de negocio existentes siempre que sean compatibles y separar adaptaciones de plataforma de la lógica común.
4. Reproducir las ventanas, controles, estilos, estados, diálogos, navegación y comportamientos actuales en la interfaz multiplataforma.
5. Comparar capturas de pantalla de la aplicación actual y la migrada en los mismos estados de interfaz.
6. Verificar cuentas Microsoft, cuentas sin conexión, catálogo, instalación y actualización de modpacks, selección de instancias, configuración, galería, consola, temas y lanzamiento de Minecraft.
7. Publicar instaladores por plataforma solo después de compilar y completar las comprobaciones disponibles. Una compilación correcta no equivale a una prueba funcional completa.

## Estrategia

- **Fase 1 — inventario y línea base:** registrar ventanas, controles, servicios, paquetes, rutas, procesos externos y flujos funcionales. Mantener la versión Windows original como referencia.
- **Fase 2 — capa de plataforma:** centralizar rutas de datos, apertura de enlaces, selección de archivos, inicio de procesos y otras operaciones dependientes del sistema operativo.
- **Fase 3 — interfaz multiplataforma:** portar cada ventana y control manteniendo sus dimensiones, estilos, estados e interacciones. No reemplazar la interfaz hasta que cada área tenga una comparación y una lista de pruebas.
- **Fase 4 — publicación:** generar artefactos separados para Windows x64, Linux x64 y macOS (Apple Silicon e Intel, si las dependencias lo permiten).
- **Fase 5 — aceptación:** probar en sistemas operativos reales o runners apropiados, corregir diferencias y mantener la versión original sin regresiones.

## Condición de aceptación

No considerar una plataforma terminada si faltan funciones, hay pantallas sustituidas por versiones simplificadas o no se ha comprobado el flujo principal de inicio de sesión, instalación y lanzamiento del juego.