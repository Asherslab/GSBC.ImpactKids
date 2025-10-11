FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GSBC.ImpactKids.Workers.DbMigrations/GSBC.ImpactKids.Workers.DbMigrations.csproj", "GSBC.ImpactKids.Workers.DbMigrations/"]
RUN dotnet restore "GSBC.ImpactKids.Workers.DbMigrations/GSBC.ImpactKids.Workers.DbMigrations.csproj"
COPY . .
WORKDIR "/src/GSBC.ImpactKids.Workers.DbMigrations"
RUN dotnet build "./GSBC.ImpactKids.Workers.DbMigrations.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GSBC.ImpactKids.Workers.DbMigrations.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GSBC.ImpactKids.Workers.DbMigrations.dll"]
