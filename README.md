# Maleta Inteligente

MVP para preparar maletas con prendas reales del armario del usuario.

Incluye un armario de ejemplo, un viaje a Roma y un motor de recomendación explicable que prioriza clima, actividad, combinaciones y preferencias. Los datos de esta versión se guardan en SQLite local (`smartpacking.db`), para poder usar y probar el flujo sin servicios externos.

## Requisitos

- .NET SDK 10, para ejecutar el servidor local.
- Docker Desktop, para el entorno compuesto.
- Una aplicación Auth0 configurada con los callback URLs que correspondan a la URL local o pública de la web.

## Ejecutar localmente

```powershell
dotnet run --project src/SmartPacking.Api
```

Abre la URL indicada por la aplicación. La API queda disponible bajo `/api` y la interfaz de demostración en `/`.

Para desarrollo con Docker, copia primero la plantilla de configuración y completa sus valores:

```powershell
Copy-Item .env.example .env
docker compose -f compose.yaml -f compose.development.yaml up --build
```

La interfaz queda disponible en `http://localhost:8081`, la API en `http://localhost:8080` y, solo en desarrollo, PostgreSQL, Redis, Azurite y Seq se exponen en sus puertos habituales. Para detenerlo, usa `docker compose -f compose.yaml -f compose.development.yaml down`.

Las migraciones se aplican automáticamente cuando arranca la API. Los datos de Docker se mantienen en volúmenes; para eliminar deliberadamente esos datos, ejecuta `docker compose -f compose.yaml -f compose.development.yaml down --volumes`.

## Configuración por entorno

La configuración base no contiene secretos y mantiene la autenticación desactivada. Desarrollo carga `appsettings.Development.json`; pruebas carga `appsettings.Testing.json` con una base SQLite en memoria; producción carga `appsettings.Production.json` y debe recibir toda la configuración operativa mediante variables de entorno.

El Compose base está preparado como configuración restringida: PostgreSQL, Redis, Azurite, Seq y la API no publican puertos al host; Redis requiere contraseña y Seq no permite acceso anónimo. Solo la web se publica en el puerto `8081`. Usa el override `compose.development.yaml` únicamente en local para exponer herramientas y habilitar el acceso anónimo a Seq.

Para producción, crea `.env` desde `.env.example`, utiliza contraseñas aleatorias largas y establece `ASPNETCORE_ENVIRONMENT=Production`. Nunca subas `.env`: contiene `POSTGRES_PASSWORD`, `REDIS_PASSWORD` y `AUTH0_CLIENT_SECRET`. En una plataforma de despliegue, configura esos valores mediante su almacén de secretos en lugar de copiar el archivo.

GitHub Actions ejecuta Gitleaks en cada pull request y push a `main`; si detecta una credencial, el flujo fallará. Si una credencial llegara a un commit histórico, revócala o rótala en el proveedor: ocultarla en un commit posterior no basta.

La CI también restaura, compila, valida el formato, ejecuta las pruebas y publica los informes OpenCover como artefacto. Sonar no se ejecuta en GitHub porque el servidor SonarQube solo está disponible en local; el informe OpenCover generado por la CI usa el mismo formato que consume el análisis local.

## Verificación

```powershell
dotnet restore SmartPacking.slnx
dotnet build SmartPacking.slnx --configuration Release --no-restore
dotnet test SmartPacking.slnx --configuration Release --no-build --no-restore
```

## Siguientes pasos

- Configurar PostgreSQL para el backend compartido (SQLite queda como almacenamiento de desarrollo).
- Autenticación y perfiles familiares.
- Cliente .NET MAUI con SQLite y sincronización.
- Tiempo real, fotos y análisis de prendas.
