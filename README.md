# Boesh Irí — API (Backend)

API REST del colectivo **Boesh Irí**. Autenticación, RBAC aditivo, publicaciones,
grupos, eventos, documentos, marketplace, finanzas, transparencia y auditoría.

- **Stack:** ASP.NET Core 10 (LTS) · EF Core 10 + Npgsql · PostgreSQL
- **Deploy:** Railway (vía `Dockerfile`)
- **Frontend:** repo separado `boeshiri-web` (React/Vite en Netlify)
- **Documentación de ingeniería:** vault Obsidian → `1. Proyectos/Boeshiri/Web/v1/`

## Estructura (arquitectura en capas — Clean/Onion)

```
Boeshiri.sln
├── Boeshiri.Api             Presentación: Controllers, auth handlers, middleware
├── Boeshiri.Application     Casos de uso: services, DTOs, validación
├── Boeshiri.Domain          Dominio: entidades, invariantes, enums (sin dependencias)
└── Boeshiri.Infrastructure  EF Core, Postgres, cliente R2, cliente Resend
```

Dependencias: `Api → Application → Domain`; `Infrastructure → Domain`;
`Api → Infrastructure` (solo para wiring de DI).

## Desarrollo

```bash
dotnet restore
dotnet build
dotnet run --project Boeshiri.Api    # escucha en http://localhost:8080
```

Las migraciones y la semilla del RBAC se aplican solas al arrancar. Para hacerlo
sin levantar el servidor: `dotnet run --project Boeshiri.Api -- --seed-only`.

### Entorno de pruebas local (API + web en tu equipo)

```bash
dotnet run --project Boeshiri.Api --launch-profile local   # API en http://localhost:8080
# en boeshiri-web:
npm run dev:local                                           # web en http://localhost:5173
```

El perfil `local` usa `ASPNETCORE_ENVIRONMENT=Local` (`appsettings.Local.json`). En ese
entorno .NET **no carga los user-secrets**, así que no toca producción aunque estén puestos:
base = Postgres de este equipo, correo = log de la consola (ahí salen los enlaces de
verificación y recuperación), subida de archivos deshabilitada. Comprueba en el log
`Hosting environment: Local` y `Emisor de correo activo: LoggingEmailSender`.

Al arrancar crea estas cuentas (contraseña `boeshiri-local`):

| Correo | Perfil |
|---|---|
| `super@local.test` | Super Administrador |
| `junta@local.test` | Junta Directiva |
| `tesorero@local.test` | Tesorero |
| `miembro@local.test` | Miembro activo |
| `postulante@local.test` | Postulante (ve «Tu solicitud está en revisión») |

### API local contra la base de producción (`local-nube`)

```bash
dotnet run --project Boeshiri.Api --launch-profile local-nube   # API en http://localhost:8080
# en boeshiri-web:
npm run dev:local
```

`ASPNETCORE_ENVIRONMENT=LocalNube` (`appsettings.LocalNube.json`). De los user-secrets toma
**solo** `ConnectionStrings:Default` (la base de Railway); R2 y Resend no se cargan, así que el
correo sale en la consola y la subida de archivos está deshabilitada. No migra ni siembra al
arrancar. **Los datos son los reales:** lo que crees aquí lo ven los miembros. Las cuentas
de prueba de `local` no existen en esta base; entra con tu cuenta real.

### Secretos en local

Nunca en el repo: van en *user-secrets* (`dotnet user-secrets set "Clave" "valor" --project Boeshiri.Api`).
En `Development` pisan a `appsettings.Development.json`.

| Clave | Necesaria para |
|---|---|
| `ConnectionStrings:Default` | Solo si no se usa el Postgres de `docker-compose` |
| `Jwt:Key` | **Obligatoria** — la API no arranca sin ella |
| `Resend:ApiKey` | Envío real de correo; sin ella se usa el emisor que escribe en el log |
| `Resend:From` | Remitente. Su dominio debe estar verificado en Resend |
| `R2:AccountId`, `R2:AccessKeyId`, `R2:SecretAccessKey`, `R2:Bucket`, `R2:PublicBaseUrl` | Subida de archivos; si falta alguna, el almacenamiento queda deshabilitado |

## Despliegue en Railway

- Railpack **no soporta .NET** → se despliega con el `Dockerfile` incluido.
- Kestrel enlaza a `0.0.0.0:$PORT` (Railway inyecta `PORT`) — ya configurado en
  `Program.cs`.

Variables de entorno del servicio (doble guion bajo = separador de sección):

| Variable | Valor | Notas |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `ConnectionStrings__Default` | `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Require;Trust Server Certificate=true` | Referencia al servicio Postgres |
| `Jwt__Key` | *(secreto, ≥32 bytes)* | |
| `Jwt__Issuer` / `Jwt__Audience` | `boeshiri-api` / `boeshiri-web` | |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | `30` / `30` *(por defecto)* | El JWT dura poco; la cookie `boeshiri_sesion` lo renueva. El front llama a `/auth/login`, `/auth/renovar` y `/auth/salir` por el proxy de Netlify para que la cookie sea de su dominio |
| `App__PublicBaseUrl` | URL del **frontend**, p. ej. `https://boeshiri.org` | Los enlaces de los correos apuntan aquí, **no** a la API |
| `Cors__AllowedOrigins` | Orígenes del front separados por coma | Sin esto el navegador bloquea las llamadas |
| `Resend__ApiKey` | *(secreto)* | Sin ella no sale ningún correo de verificación |
| `Resend__From` | `Boesh Irí <hola@tu-dominio>` | El dominio debe estar **verificado** en Resend |
| `R2__*` | Credenciales de Cloudflare R2 | Sin ellas no hay subida de archivos |

> Al arrancar, la API registra en el log qué emisor de correo quedó activo
> (`ResendEmailSender` o `LoggingEmailSender`): es la forma rápida de saber si
> `Resend__ApiKey` llegó bien.

## MCP / trabajo asistido por IA

Config MCP de este repo (back): **PostgreSQL (read-only)** y **GitHub**. Copiar
`.mcp.json.example` → `.mcp.json` y completar con un **rol de BD de solo lectura**.
Guía completa: `v1/04-Entorno-y-Flujo-Asistido-IA.md`.
