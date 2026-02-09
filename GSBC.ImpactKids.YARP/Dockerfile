FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GSBC.ImpactKids.YARP/GSBC.ImpactKids.YARP.csproj", "GSBC.ImpactKids.YARP/"]
RUN dotnet restore "GSBC.ImpactKids.YARP/GSBC.ImpactKids.YARP.csproj"
COPY . .
WORKDIR "/src/GSBC.ImpactKids.YARP"
RUN dotnet build "./GSBC.ImpactKids.YARP.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GSBC.ImpactKids.YARP.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GSBC.ImpactKids.YARP.dll"]
