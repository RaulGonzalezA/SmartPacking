# Azure SQL Database Serverless

SmartPacking usa `SqlServer` como proveedor al ejecutar con el entorno `Azure`. La cadena de conexión no se guarda en el repositorio: configúrala como secreto de Container Apps o Key Vault bajo `ConnectionStrings__SmartPacking`.

```text
Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=smartpacking;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity
```

Para un primer despliegue con usuario y contraseña de SQL, usa una cadena equivalente con el secreto separado del repositorio. Sustitúyela por identidad administrada antes de producción.

La aplicación selecciona el proveedor mediante `Persistence__Provider=SqlServer` y ejecuta las migraciones contenidas en `SmartPacking.Infrastructure.SqlServer`.

Genera una migración SQL Server nueva con:

```powershell
dotnet ef migrations add <Nombre> --project src/SmartPacking.Infrastructure.SqlServer --startup-project src/SmartPacking.Infrastructure.SqlServer --context SmartPackingDbContext
```
