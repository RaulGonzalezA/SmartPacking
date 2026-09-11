# SmartPacking Mobile · Iteración 3

Tercer vertical slice móvil de SmartPacking con .NET MAUI para Android.

Mantiene las iteraciones anteriores (Auth0 + PKCE, viajes, dashboard, checklist, cámara, Gemini y alta de prendas) y añade:

- paginación progresiva del armario en páginas de 20 prendas;
- miniaturas JPEG de 320 px para evitar descargar la fotografía completa en el listado;
- detalle de prenda con edición de datos, cambio de fotografía y borrado a papelera;
- refresh token en `SecureStorage` con renovación automática de la sesión;
- Refresh Token Rotation: si Auth0 devuelve un refresh token nuevo, sustituye al anterior;
- reintento automático de una petición una sola vez cuando la API responde `401 Unauthorized` y la renovación tiene éxito;
- pruebas específicas de `SmartPacking.Client` para bearer token, reintento tras 401, paginación y multipart con miniatura.

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

La iteración 3 solicita el scope `offline_access`. Para que Auth0 entregue `refresh_token`, habilita **Allow Offline Access** para la API y **Refresh Token Rotation** para la aplicación Native. Si Auth0 no devuelve refresh token, SmartPacking muestra un error de configuración en lugar de crear una sesión que caducará sin posibilidad de renovación.

El access token y refresh token se guardan mediante `SecureStorage`. Cuando el access token está próximo a caducar se renueva antes de la petición; si una llamada responde 401, `BearerTokenHandler` fuerza una única renovación y reintenta la misma petición una vez.

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

## Cámara, fotografías y miniaturas

El proyecto declara el permiso Android `CAMERA`. `MediaPicker` se utiliza tanto para captura como para selección de imágenes.

Antes de enviarla a Gemini o almacenarla, la fotografía se decodifica en el dispositivo y se vuelve a generar como JPEG:

- fotografía principal: dimensión máxima 1280 px y calidad JPEG 82;
- miniatura: dimensión máxima 320 px y calidad JPEG 76;
- los JPEG regenerados no conservan los metadatos EXIF del fichero original.

La API almacena la miniatura de forma privada junto a la fotografía principal y expone `GET /api/wardrobe/{id}/thumbnail`. Para fotografías antiguas sin miniatura, el endpoint hace fallback a la imagen original, por lo que la migración es compatible hacia atrás.

Si la prenda se crea correctamente pero falla la subida de la fotografía, la pantalla conserva el identificador de la prenda y permite reintentar **solo** la fotografía, evitando crear duplicados.

## Armario móvil

El listado solicita 20 prendas por página y carga la siguiente página al acercarse al final de `CollectionView`. En el listado solo se descargan miniaturas. Al tocar una prenda se abre el detalle, donde se carga la fotografía completa y se pueden modificar:

- nombre, tipo, temporada, color, material y estilo;
- nivel de abrigo, impermeabilidad y peso;
- estado limpia/disponible;
- fotografía;
- borrado a papelera.

## Ejecutar

Instala el workload de MAUI si todavía no está disponible:

```powershell
dotnet workload install maui
```

Después:

```powershell
dotnet restore SmartPacking.slnx
dotnet build src/SmartPacking.Mobile/SmartPacking.Mobile.csproj -f net10.0-android
dotnet test tests/SmartPacking.Client.Tests/SmartPacking.Client.Tests.csproj
```

También puedes seleccionar `SmartPacking.Mobile` como proyecto de inicio desde Visual Studio y ejecutar sobre un emulador o dispositivo Android.

## Alcance pendiente

Para siguientes iteraciones quedan, entre otras mejoras:

- SQLite y funcionamiento offline;
- sincronización incremental y resolución de conflictos;
- notificaciones de cambios meteorológicos y preparación del viaje;
- compartir viajes/deep links;
- soporte iOS cuando se incorpore el target correspondiente.
