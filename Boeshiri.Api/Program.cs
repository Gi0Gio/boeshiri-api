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

builder.Services.AddAuthorization();

// CORS: orígenes permitidos para el front (config "Cors:AllowedOrigins", separados
// por coma). Por defecto, el dev server de Vite en local.
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173,http://localhost:4173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// Autorización por permiso: [HasPermission("...")] → política dinámica "perm:...".
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Respuestas de error en formato problem+json.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

var app = builder.Build();

// Aplicar migraciones pendientes y sembrar datos de referencia (RBAC) al arrancar.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BoeshiriDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}

// "dotnet run -- --seed-only": migra y siembra, luego termina sin levantar el servidor.
if (args.Contains("--seed-only"))
    return;

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // El redirect HTTPS solo en local: en Railway el TLS se termina en el borde
    // y el contenedor recibe HTTP, por lo que redirigir causaría problemas.
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
