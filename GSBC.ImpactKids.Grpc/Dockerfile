FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GSBC.ImpactKids.Grpc/GSBC.ImpactKids.Grpc.csproj", "GSBC.ImpactKids.Grpc/"]
RUN dotnet restore "GSBC.ImpactKids.Grpc/GSBC.ImpactKids.Grpc.csproj"
COPY . .
WORKDIR "/src/GSBC.ImpactKids.Grpc"
RUN dotnet build "./GSBC.ImpactKids.Grpc.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GSBC.ImpactKids.Grpc.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GSBC.ImpactKids.Grpc.dll"]
