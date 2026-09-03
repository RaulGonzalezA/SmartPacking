# Configuración de Auth0

SmartPacking usa Auth0 Universal Login para el registro e inicio de sesión, y valida en la API el token recibido.

## 1. Crear la API

En Auth0 Dashboard abre **Applications > APIs > Create API**.

- Name: `SmartPacking API`
- Identifier: `https://smartpacking-api`
- Signing Algorithm: `RS256`

Guarda el identificador: será el `Audience` de web y API.

## 2. Crear la aplicación web

En **Applications > Applications > Create Application**, crea una **Regular Web Application**.

En sus URLs permitidas añade, para desarrollo:

- Allowed Callback URLs: `http://127.0.0.1:54272/signin-oidc`
- Allowed Logout URLs: `http://127.0.0.1:54272/`
- Allowed Web Origins: `http://127.0.0.1:54272`

Habilita una conexión de base de datos en **Authentication > Database** para permitir el registro con correo y contraseña. Auth0 mostrará esa opción al acceder a `Crear cuenta`.

## 3. Configurar secretos locales

No guardes el secreto en `appsettings.json`. En PowerShell, para tu sesión de desarrollo:

```powershell
$env:Authentication__Enabled = "true"
$env:Authentication__OpenIdConnect__Authority = "https://TU_TENANT.eu.auth0.com/"
$env:Authentication__OpenIdConnect__ClientId = "TU_CLIENT_ID"
$env:Authentication__OpenIdConnect__ClientSecret = "TU_CLIENT_SECRET"
$env:Authentication__OpenIdConnect__Audience = "https://smartpacking-api"
$env:Authentication__JwtBearer__Authority = "https://TU_TENANT.eu.auth0.com/"
$env:Authentication__JwtBearer__Audience = "https://smartpacking-api"
```

Ejecuta API y web desde esa misma sesión. En producción, configura los mismos valores como secretos del entorno de despliegue.

El `Audience` debe coincidir exactamente con el Identifier de la API. Cada token contiene el `sub` de Auth0; SmartPacking lo combina con el `issuer` para generar y persistir el usuario interno correspondiente en el primer acceso.
