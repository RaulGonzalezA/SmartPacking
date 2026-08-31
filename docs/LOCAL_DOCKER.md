# Entorno local Docker

Ejecuta el entorno completo con:

```powershell
docker compose up --build
```

Servicios disponibles:

- Web: `http://localhost:8081`
- API: `http://localhost:8080`
- Health check: `http://localhost:8080/health`
- Scalar: `http://localhost:8080/scalar`
- PostgreSQL: `localhost:5432` (usuario, contraseña y base de datos: `smartpacking`)
- Azurite Blob: `http://localhost:10000/devstoreaccount1`

Para detener y conservar datos:

```powershell
docker compose down
```

Para reiniciar también PostgreSQL y Azurite desde cero:

```powershell
docker compose down --volumes
```

En Docker la API espera PostgreSQL, vuelve a intentar conexiones transitorias y aplica el conjunto de migraciones específico de PostgreSQL al iniciarse.
