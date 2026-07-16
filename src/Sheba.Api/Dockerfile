# ─────────────────────────────────────────────────────────────────────────────
# Sheba e-Government API — multi-stage Dockerfile
# Build context must be the repository root (so Directory.Packages.props and all
# module projects are visible):  docker build -f src/Sheba.Api/Dockerfile .
# ─────────────────────────────────────────────────────────────────────────────

# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy central build files first for better layer caching
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY Sheba.sln ./

# Copy all source (src + tests are referenced by the solution)
COPY src/ ./src/

# Restore and publish only the API (transitively builds all referenced modules)
RUN dotnet restore "src/Sheba.Api/Sheba.Api.csproj"
RUN dotnet publish "src/Sheba.Api/Sheba.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Kestrel listens on 8080 inside the container (mapped to 5000 by docker-compose)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Sheba.Api.dll"]
