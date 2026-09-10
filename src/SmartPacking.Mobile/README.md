# SmartPacking Mobile · Iteración 2

Segundo vertical slice móvil de SmartPacking con .NET MAUI para Android.

Incluye lo de la iteración 1:

- autenticación Auth0 mediante Authorization Code + PKCE;
- almacenamiento seguro del access token;
- listado de viajes;
- detalle básico del viaje;
- previsión y progreso de preparación;
- checklist interactivo;
- cliente HTTP compartible en `SmartPacking.Client`.

Y añade en esta iteración:

- acceso al armario desde la pantalla de viajes;
- listado de prendas con fotografía privada obtenida mediante la API autenticada;
- captura de prendas con la cámara;
- selección de fotografías desde la galería;
- optimización local de la imagen antes de subirla: máximo 1280 px y JPEG al 82 %;
- análisis de la fotografía con Gemini;
- formulario de confirmación/corrección de la propuesta de Gemini;
- creación de la prenda y subida posterior de su fotografía optimizada.

## Configuración Auth0

Crea una aplicación **Native** independiente de la aplicación Web de Auth0. No reutilices el Client Secret de la Web y no incluyas ningún secreto en la app móvil.

Configura como callback URL:

```text
smartpacking://callback
```

En `MobileOptions.cs` sustituye los placeholders de la configuración correspondiente:

```text
https://YOUR_AUTH0_DOMAIN/
YOUR_NATIVE_AUTH0_CLIENT_ID
```

por el dominio y Client ID de la aplicación Native. El audience se mantiene en `https://smartpacking-api`.

## Configuración Debug y Release

`MobileOptions.Current` selecciona automáticamente la configuración según la compilación:

- `Debug` usa `MobileOptions.Development`;
- `Release` usa `MobileOptions.Production`.

La configuración de desarrollo usa para Android Emulator:

```text
http://10.0.2.2:8080/
```

`10.0.2.2` es la dirección que Android Emulator utiliza para acceder al host. El tráfico HTTP sin TLS se habilita **solo en Debug** mediante `[Application(UsesCleartextTraffic = true)]`. El manifest base no permite cleartext y las compilaciones `Release` exigen HTTPS para la API y Auth0.

Antes de distribuir una build `Release`, sustituye también:

```text
https://YOUR_API_HOST/
```

por la URL HTTPS real de la API. En un dispositivo físico de desarrollo usa igualmente una URL de API accesible desde el teléfono.

## Cámara y fotografías

El proyecto declara el permiso Android `CAMERA`. `MediaPicker` se utiliza tanto para captura como para selección de imágenes.

Antes de enviarla a Gemini o almacenarla, la fotografía se decodifica en el dispositivo y se vuelve a generar como JPEG:

- dimensión máxima: 1280 px;
- calidad JPEG: 82;
- el nuevo JPEG no conserva los metadatos EXIF del fichero original.

La decodificación usa `InJustDecodeBounds` e `InSampleSize` para evitar cargar fotografías grandes a resolución completa. La misma fotografía optimizada se reutiliza para el reconocimiento y para el almacenamiento del armario.

Si la prenda se crea correctamente pero falla la subida de la fotografía, la pantalla conserva el identificador de la prenda y permite reintentar **solo** la fotografía, evitando crear duplicados.

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

Para siguientes iteraciones quedan, entre otras mejoras:

- edición y borrado de prendas desde móvil;
- carga progresiva/paginación del armario y miniaturas;
- renovación de sesión con refresh token rotation;
- SQLite y funcionamiento offline;
- sincronización incremental;
- notificaciones de cambios meteorológicos y preparación del viaje;
- soporte iOS cuando se incorpore el target correspondiente.
