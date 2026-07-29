# ── Build ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar primero (mejor caché de capas)
COPY Boeshiri.slnx .
COPY Boeshiri.Api/Boeshiri.Api.csproj Boeshiri.Api/
COPY Boeshiri.Application/Boeshiri.Application.csproj Boeshiri.Application/
COPY Boeshiri.Domain/Boeshiri.Domain.csproj Boeshiri.Domain/
COPY Boeshiri.Infrastructure/Boeshiri.Infrastructure.csproj Boeshiri.Infrastructure/
RUN dotnet restore

# Copiar el resto y publicar
COPY . .
RUN dotnet publish Boeshiri.Api -c Release -o /app --no-restore

# ── Runtime ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Railway inyecta PORT; Program.cs enlaza a 0.0.0.0:$PORT.
# EXPOSE es informativo; el puerto real lo define Railway en runtime.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Boeshiri.Api.dll"]
