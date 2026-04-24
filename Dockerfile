# syntax=docker/dockerfile:1.7

# ─────────── BUILD ───────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copia global.json y los Directory.* primero para aprovechar caché de restore.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/SincoPresupuesto.Domain/SincoPresupuesto.Domain.csproj src/SincoPresupuesto.Domain/
COPY src/SincoPresupuesto.Application/SincoPresupuesto.Application.csproj src/SincoPresupuesto.Application/
COPY src/SincoPresupuesto.Infrastructure/SincoPresupuesto.Infrastructure.csproj src/SincoPresupuesto.Infrastructure/
COPY src/SincoPresupuesto.Api/SincoPresupuesto.Api.csproj src/SincoPresupuesto.Api/

RUN dotnet restore src/SincoPresupuesto.Api/SincoPresupuesto.Api.csproj

# Copia el resto del código y publica.
COPY src/ src/
RUN dotnet publish src/SincoPresupuesto.Api/SincoPresupuesto.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ─────────── RUNTIME ───────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Usuario no-root (requerido por políticas de seguridad en ACA).
RUN addgroup -S app && adduser -S -G app app
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SincoPresupuesto.Api.dll"]
