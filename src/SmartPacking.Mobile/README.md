# SmartPacking Mobile · Iteración 1

Primer vertical slice móvil de SmartPacking con .NET MAUI para Android.

Incluye:

- autenticación Auth0 mediante Authorization Code + PKCE;
- almacenamiento seguro del access token;
- listado de viajes;
- detalle básico del viaje;
- previsión y progreso de preparación;
- checklist interactivo;
- cliente HTTP compartible en `SmartPacking.Client`.

## Configuración Auth0

Crea una aplicación **Native** independiente de la aplicación Web de Auth0. No reutilices el Client Secret de la Web y no incluyas ningún secreto en la app móvil.

Configura como callback URL:

```text
smartpacking://callback
```

En `MobileOptions.cs` sustituye:

```text
https://YOUR_AUTH0_DOMAIN/
YOUR_NATIVE_AUTH0_CLIENT_ID
```

por el dominio y Client ID de la aplicación Native. El audience se mantiene en `https://smartpacking-api`.

## API local desde Android Emulator

La configuración de desarrollo usa:

```text
http://10.0.2.2:8080/
```

`10.0.2.2` es la dirección que Android Emulator utiliza para acceder al host. El manifest permite HTTP sin TLS únicamente para facilitar esta primera iteración local. Antes de una build de distribución, usa HTTPS y elimina `android:usesCleartextTraffic="true"`.

En un dispositivo físico cambia `ApiBaseAddress` por una URL de la API accesible desde el teléfono.

## Ejecutar

Instala el workload de MAUI si todavía no está disponible:

```powershell
dotnet workload install maui
```

Después:

```powershell
dotnet restore SmartPacking.slnx
dotnet build src/SmartPacking.Mobile/SmartPacking.Mobile.csproj -f net10.0-android
```

También puedes seleccionar `SmartPacking.Mobile` como proyecto de inicio desde Visual Studio y ejecutar sobre un emulador o dispositivo Android.

## Alcance pendiente

La siguiente iteración debería incorporar armario + cámara + análisis de prenda. Offline/SQLite, sincronización y notificaciones quedan deliberadamente fuera de este primer vertical slice.
