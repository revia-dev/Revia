# Multi-stage build for Revia: builds the Blazor WebAssembly client + the API host
# (which serves the PWA + the REST API + the service worker) and ships a small
# runtime image on port 8080. Build context is the repository root.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for better layer caching (copy only the project files).
COPY Revia/Revia.csproj                 Revia/
COPY Revia.Shared/Revia.Shared.csproj   Revia.Shared/
COPY Revia.Client/Revia.Client.csproj   Revia.Client/
COPY Revia.Api/Revia.Api.csproj         Revia.Api/
RUN dotnet restore Revia.Api/Revia.Api.csproj

# Copy everything and publish the API (pulls in the client + service worker).
COPY . .
RUN dotnet publish Revia.Api/Revia.Api.csproj -c Release -o /app/publish

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Data (SQLite DB + uploaded photos/files) lives on the mounted /data volume.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/data/revia.db" \
    BmiPhotos__StoragePath=/data/bmi-photos \
    LendingPhotos__StoragePath=/data/lending-photos \
    PersonPhotos__StoragePath=/data/person-photos \
    GameFiles__StoragePath=/data/game-files
# Provide a stable Jwt__SigningKey (and optionally Seed__AdminPassword, Demo__Enabled)
# via the environment / compose file.

EXPOSE 8080
VOLUME /data
ENTRYPOINT ["dotnet", "Revia.Api.dll"]
