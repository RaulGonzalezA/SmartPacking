# Maleta Inteligente

MVP para preparar maletas con prendas reales del armario del usuario.

Incluye un armario de ejemplo, un viaje a Roma y un motor de recomendación explicable que prioriza clima, actividad, combinaciones y preferencias. Los datos de esta versión se guardan en SQLite local (`smartpacking.db`), para poder usar y probar el flujo sin servicios externos.

## Ejecutar

```powershell
dotnet run --project src/SmartPacking.Api
```

Abre la URL indicada por la aplicación. La API queda disponible bajo `/api` y la interfaz de demostración en `/`.

## Siguientes pasos

- Configurar PostgreSQL para el backend compartido (SQLite queda como almacenamiento de desarrollo).
- Autenticación y perfiles familiares.
- Cliente .NET MAUI con SQLite y sincronización.
- Tiempo real, fotos y análisis de prendas.
