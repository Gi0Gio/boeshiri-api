using System.Text;
using System.Text.Json.Serialization;
using Boeshiri.Api;
using Boeshiri.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Boeshiri.Infrastructure;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Railway inyecta la variable de entorno PORT. Kestrel enlaza por defecto a
// localhost, que en el contenedor no recibe tráfico: hay que enlazar a 0.0.0.0.
// En local, si PORT no está definido, usa 8080.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.

builder.Services.AddControllers()
    // Enums en JSON como texto (p. ej. "Aceptar"/"Rechazar", estados...).
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Errores de validación (400) en español y legibles, conservando el mapa `errors`.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var campos = string.Join(", ", context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .Select(e => e.Key)
            // Normaliza claves internas del binder ("$", "request.", "$.") a nombres de campo.
            .Select(k => k.StartsWith("$.", StringComparison.Ordinal) ? k[2..]
                : k.StartsWith("request.", StringComparison.Ordinal) ? k["request.".Length..]
                : k)
            .Where(k => !string.IsNullOrEmpty(k) && k != "$" && k != "request")
            .Distinct(StringComparer.OrdinalIgnoreCase));

        var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
        {
            Status = 400,
            Title = "Datos inválidos",
            Detail = string.IsNullOrEmpty(campos)
                ? "Revisa los datos enviados."
                : $"Revisa estos campos: {campos}.",
        };
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    };
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Persistencia + auth + correo (EF Core, Identity hasher, JWT, email).
builder.Services.AddInfrastructure(builder.Configuration);

// Autenticación por JWT Bearer. La clave viene de config (user-secrets en dev).
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'Jwt'.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Conservar los nombres originales de los claims ("sub", "perm", "role").
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(o => o.AddPolicy(MiembroActivoAttribute.Politica, p => p
    .RequireAuthenticatedUser()
    .RequireClaim(MiembroActivoAttribute.ClaimEstado, MiembroActivoAttribute.EstadoActivo)));

// CORS: orígenes permitidos para el front (config "Cors:AllowedOrigins", separados
// por coma). Por defecto, el dev server de Vite en local.
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173,http://localhost:4173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// Railway termina el TLS en su borde y reenvía la IP real en X-Forwarded-For. Sin
// leerla, todas las peticiones parecían venir de la IP del borde (y el límite del
// formulario de contacto era uno solo para todo el mundo). ForwardLimit = 1: solo
// cuenta la entrada que añadió el borde; las anteriores las pudo escribir el cliente.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 1;
});

builder.Services.AddLimites();

// Autorización por permiso: [HasPermission("...")] → política dinámica "perm:...".
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Respuestas de error en formato problem+json, con títulos en español por defecto.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        if (!string.IsNullOrEmpty(ctx.ProblemDetails.Title))
            return;

        ctx.ProblemDetails.Title = ctx.ProblemDetails.Status switch
        {
            400 => "Solicitud inválida.",
            401 => "Necesitas iniciar sesión.",
            403 => "No tienes permiso para esta acción.",
            404 => "No se encontró el recurso.",
            409 => "Conflicto con el estado actual.",
            429 => "Demasiadas peticiones seguidas. Espera un momento.",
            >= 500 => "Ocurrió un error en el servidor. Inténtalo de nuevo.",
            _ => "Ocurrió un error.",
        };
    };
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();

var app = builder.Build();

// Aplicar migraciones pendientes y sembrar datos de referencia (RBAC) al arrancar.
//
// En desarrollo NO se migra una base remota salvo que se pida explícitamente
// (Database:AllowRemoteMigrations=true): si la cadena local apunta a la base de
// producción, arrancar en local una rama con una migración nueva la aplicaría en
// producción antes de desplegar nada. Los tests desactivan la migración del todo
// (Database:MigrateOnStartup=false) porque crean su propio esquema.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BoeshiriDbContext>();
    var host = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Host ?? "";
    var esLocal = host is "localhost" or "127.0.0.1" or "::1" or "postgres" or "host.docker.internal";

    if (app.Environment.IsDevelopment() && !esLocal && !app.Configuration.GetValue("Database:AllowRemoteMigrations", false))
    {
        app.Logger.LogWarning(
            "Base remota ({Host}) en Development: no se aplican migraciones ni semilla al arrancar. " +
            "Si de verdad quieres migrar esa base, define Database:AllowRemoteMigrations=true.", host);
    }
    else
    {
        await db.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(db);
    }
}

// Deja constancia de qué emisor de correo quedó activo: sin esto, "no llegan los
// correos" obliga a revisar configuración a ciegas (ADR-0003).
using (var scope = app.Services.CreateScope())
{
    var sender = scope.ServiceProvider.GetRequiredService<Boeshiri.Application.Abstractions.IEmailSender>();
    app.Logger.LogInformation("Emisor de correo activo: {Sender}", sender.GetType().Name);
}

// "dotnet run -- --seed-only": migra y siembra, luego termina sin levantar el servidor.
if (args.Contains("--seed-only"))
    return;

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // El redirect HTTPS solo en local: en Railway el TLS se termina en el borde
    // y el contenedor recibe HTTP, por lo que redirigir causaría problemas.
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Sonda de salud (anónima). Comprueba también la base: antes respondía «ok» con la
// base caída y el monitor no se enteraba.
app.MapGet("/health", async (BoeshiriDbContext db, CancellationToken ct) =>
{
    bool baseOk;
    try { baseOk = await db.Database.CanConnectAsync(ct); }
    catch { baseOk = false; }

    var cuerpo = new { status = baseOk ? "ok" : "degradado", service = "boeshiri-api", db = baseOk ? "ok" : "sin conexión" };
    return baseOk ? Results.Ok(cuerpo) : Results.Json(cuerpo, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.Run();

/// <summary>Visible para los tests de integración (WebApplicationFactory).</summary>
public partial class Program;
