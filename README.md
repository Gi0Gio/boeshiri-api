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

## Despliegue en Railway

- Railpack **no soporta .NET** → se despliega con el `Dockerfile` incluido.
- Kestrel enlaza a `0.0.0.0:$PORT` (Railway inyecta `PORT`) — ya configurado en
  `Program.cs`.
- Secrets (cadena de conexión, R2, Resend) como variables de entorno en Railway.

## MCP / trabajo asistido por IA

Config MCP de este repo (back): **PostgreSQL (read-only)** y **GitHub**. Copiar
`.mcp.json.example` → `.mcp.json` y completar con un **rol de BD de solo lectura**.
Guía completa: `v1/04-Entorno-y-Flujo-Asistido-IA.md`.
