# Prototipo de portabilidad de Negative Launcher

Este proyecto es una prueba técnica aislada para validar Avalonia UI y la publicación para distintos sistemas operativos. No reemplaza el launcher WPF, no carga sus datos y no implementa todavía las funciones del producto.

## Qué valida

- Restauración de paquetes y compilación con .NET 10.
- Arranque de una aplicación de escritorio Avalonia.
- XAML básico, ventana redimensionable y tema oscuro de referencia.
- Publicación por arquitectura para Windows, Linux y macOS mediante GitHub Actions.

## Qué no valida todavía

- Inicio de sesión Microsoft o cuentas sin conexión.
- Descarga, instalación, actualización o ejecución de Minecraft.
- Forge, instancias, modpacks, consola, capturas, temas estacionales o preferencias.
- Fidelidad visual exacta con la interfaz WPF.
- Ejecución real en cada sistema operativo; para eso se necesitan pruebas en máquinas objetivo.

## Regla de migración

La interfaz y los servicios WPF existentes se mantienen intactos. Este prototipo sirve para evaluar la base tecnológica; no debe presentarse como una versión multiplataforma terminada.
