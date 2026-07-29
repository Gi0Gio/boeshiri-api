# ── Build ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar los csproj de la API y sus dependencias (los tests no van a producción)
# y restaurar solo el proyecto de la API (restaura sus referencias transitivas).
COPY Boeshiri.Api/Boeshiri.Api.csproj Boeshiri.Api/
COPY Boeshiri.Application/Boeshiri.Application.csproj Boeshiri.Application/
COPY Boeshiri.Domain/Boeshiri.Domain.csproj Boeshiri.Domain/
COPY Boeshiri.Infrastructure/Boeshiri.Infrastructure.csproj Boeshiri.Infrastructure/
RUN dotnet restore Boeshiri.Api/Boeshiri.Api.csproj

# Copiar el resto y publicar solo la API
COPY . .
RUN dotnet publish Boeshiri.Api/Boeshiri.Api.csproj -c Release -o /app --no-restore

# ── Runtime ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Railway inyecta PORT; Program.cs enlaza a 0.0.0.0:$PORT.
# EXPOSE es informativo; el puerto real lo define Railway en runtime.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Boeshiri.Api.dll"]
