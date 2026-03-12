# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore dependencies first (cached layer)
COPY ATLAS.csproj ./
RUN dotnet restore

# Copy source and publish
COPY . ./
RUN dotnet publish ATLAS.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create a directory for the SQLite database and make it a mount point
RUN mkdir -p /app/data

# Copy published output from build stage
COPY --from=build /app/publish .

# Store the database in /app/data so it can be persisted via a volume
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/atlas.db"

# Bind to 0.0.0.0 so the container port is accessible from the host
ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"

EXPOSE 8080

ENTRYPOINT ["dotnet", "ATLAS.dll"]
