# Vedere https://aka.ms/customizecontainer per informazioni su come personalizzare il contenitore di debug e su come Visual Studio usa questo Dockerfile per compilare le immagini per un debug più rapido.

# Questa fase viene usata durante l'esecuzione da Visual Studio in modalità rapida (impostazione predefinita per la configurazione di debug)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Questa fase viene usata per compilare il progetto di servizio
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SagraFacile.NET.API/SagraFacile.NET.API.csproj", "SagraFacile.NET.API/"]
RUN dotnet restore "./SagraFacile.NET.API/SagraFacile.NET.API.csproj"
COPY . .
WORKDIR "/src/SagraFacile.NET.API"
RUN dotnet build "./SagraFacile.NET.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Questa fase viene usata per pubblicare il progetto di servizio da copiare nella fase finale
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SagraFacile.NET.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Questa fase viene usata nell'ambiente di produzione o durante l'esecuzione da Visual Studio in modalità normale (impostazione predefinita quando non si usa la configurazione di debug)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create the media directory and set permissions
RUN mkdir -p wwwroot/media/promo && \
    chown -R $APP_UID:$APP_UID wwwroot/media && \
    chmod -R 755 wwwroot/media

ENTRYPOINT ["dotnet", "SagraFacile.NET.API.dll"]
