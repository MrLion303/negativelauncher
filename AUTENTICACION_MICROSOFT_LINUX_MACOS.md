# Autenticación Microsoft en Linux y macOS

Negative Launcher conserva el inicio de sesión de Windows y utiliza MSAL en Linux/macOS. Para esta segunda ruta necesita el **Application (client) ID de una aplicación pública de Microsoft Entra**. El Client ID es un identificador público, no una contraseña ni un secreto.

> El launcher no incluye un Client ID inventado. Un GUID con formato correcto no garantiza que la aplicación esté autorizada para el inicio de sesión de Xbox/Minecraft.

## 1. Registrar una aplicación pública

1. Abre el [portal de Microsoft Entra](https://entra.microsoft.com/) y entra en **App registrations** → **New registration**.
2. Asigna un nombre para identificar la aplicación.
3. En tipos de cuenta, selecciona la opción que permita **cuentas personales de Microsoft** (por ejemplo, Xbox) y, si necesitas, cuentas organizativas.
4. Después de crear el registro, copia el **Application (client) ID** de la página Overview.
5. En **Authentication**, agrega la plataforma **Mobile and desktop applications** y registra el redirect URI `http://localhost` para el flujo con navegador del sistema.
6. En **Advanced settings**, activa **Allow public client flows** y guarda los cambios.
7. Sigue la guía oficial de CmlLib para el requisito de autorización/allowlist de Client ID para las API de autenticación de Minecraft. Si no se cumple, la autenticación de Xbox/Minecraft puede fallar aunque el registro MSAL sea válido.

Documentación de referencia:
- [CmlLib — obtener un Client ID para XboxAuthNet.Msal](https://cmllib.github.io/CmlLib.Core-wiki/en/auth.microsoft/xboxauthnet.game.msal/clientid/)
- [CmlLib — MsalClientHelper y caché de cuentas](https://cmllib.github.io/CmlLib.Core-wiki/en/auth.microsoft/xboxauthnet.game.msal/msalclienthelper/)
- [Microsoft — configurar aplicaciones de escritorio con MSAL](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)
- [Centro de ayuda de Minecraft — autorización de Client ID](https://help.minecraft.net/hc/en-us/articles/16254801392141)

## 2. Integrarlo en la compilación

Negative Launcher ya incorpora de fábrica el Application (client) ID de su aplicación pública:

`c8a6f4d3-36ba-4af5-9fe6-e983372e866c`

El proyecto lo integra en los metadatos de `NegativeLauncher.Core` mediante una propiedad MSBuild predeterminada. Por eso las compilaciones locales y las de GitHub Actions para Linux x64, Linux ARM64 y macOS incluyen el ID sin que el publicador tenga que configurar una variable de Actions. La variable opcional `NEGATIVE_LAUNCHER_MSAL_CLIENT_ID` puede sobrescribir el valor predeterminado al compilar, si fuera necesario.

El Client ID es un identificador público, no una contraseña ni un secreto. No guardes tokens ni contraseñas en el repositorio. El jugador no tiene que introducir ningún identificador en el launcher; solo pulsa **Iniciar sesión con Microsoft**. El modo sin conexión sigue disponible.

## 3. Si el inicio de sesión falla

- **Microsoft no está incluido en esta compilación**: el publicador debe configurar la variable de Actions y generar un paquete nuevo; el jugador no puede repararlo desde la interfaz.
- **Error de redirect URI o AADSTS50011**: revisa que `http://localhost` esté registrado bajo **Mobile and desktop applications**.
- **Error de cliente público**: comprueba **Allow public client flows**.
- **Error 403 de la API de Minecraft/Xbox**: confirma que la aplicación cumple el requisito de autorización/allowlist descrito por la documentación de CmlLib y Minecraft.
- **Cuenta sin conexión**: sigue disponible desde la pantalla de acceso, pero no sustituye a una cuenta oficial para servicios que la exijan.

CI puede comprobar la compilación, la configuración y el manejo de errores; no puede demostrar una autenticación Microsoft real sin un Client ID válido y autorizado ni una cuenta de prueba.
