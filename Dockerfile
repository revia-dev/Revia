FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PrivateLibrary/PrivateLibrary.csproj PrivateLibrary/
RUN dotnet restore PrivateLibrary/PrivateLibrary.csproj

COPY PrivateLibrary/ PrivateLibrary/
RUN dotnet publish PrivateLibrary/PrivateLibrary.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Persistent data volume (DB + mutable CSV) should be mounted at /home/data
ENV ConnectionStrings__DefaultConnection="Data Source=/home/data/private-library.db"
ENV Movies__CsvPath="/home/data/movies.csv"

# Seed:AdminPassword and Tmdb:ApiKey must be provided via environment variables or Azure App Settings

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PrivateLibrary.dll"]
