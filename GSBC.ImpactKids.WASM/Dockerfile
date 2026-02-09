# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first to leverage Docker layer caching
# (If you have multiple projects, copy only the ones referenced by the main WASM project)
COPY ./GSBC.ImpactKids.WASM/*.csproj ./GSBC.ImpactKids.WASM/
RUN dotnet restore ./GSBC.ImpactKids.WASM/GSBC.ImpactKids.WASM.csproj

# Copy the rest of the source
COPY . .

# Publish optimized static assets
# -c Release: Release build
# /p:InvariantGlobalization=true: trims ICU data if your app doesn't need full globalization
# /p:DebuggerSupport=false, /p:StartupTracing=false: trims blazor payload
# /p:LinkerDumpDependencies=false: smaller publish
RUN dotnet publish ./GSBC.ImpactKids.WASM/GSBC.ImpactKids.WASM.csproj \
    -c Release -o /app/publish \
    /p:InvariantGlobalization=true \
    /p:DebuggerSupport=false \
    /p:StartupTracing=false \
    /p:LinkerDumpDependencies=false

# ---------- Runtime stage ----------
FROM nginx:latest AS runtime
COPY ./GSBC.ImpactKids.WASM/nginx.conf /etc/nginx/nginx.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Optional: verify index.html exists (fail early if publish broke)
RUN test -f /usr/share/nginx/html/index.html

EXPOSE 80

# Run nginx in foreground
CMD ["nginx", "-g", "daemon off;"]