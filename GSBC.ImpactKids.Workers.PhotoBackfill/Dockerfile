FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GSBC.ImpactKids.Workers.PhotoBackfill/GSBC.ImpactKids.Workers.PhotoBackfill.csproj", "GSBC.ImpactKids.Workers.PhotoBackfill/"]
RUN dotnet restore "GSBC.ImpactKids.Workers.PhotoBackfill/GSBC.ImpactKids.Workers.PhotoBackfill.csproj"
COPY . .
WORKDIR "/src/GSBC.ImpactKids.Workers.PhotoBackfill"
RUN dotnet build "./GSBC.ImpactKids.Workers.PhotoBackfill.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GSBC.ImpactKids.Workers.PhotoBackfill.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GSBC.ImpactKids.Workers.PhotoBackfill.dll"]
