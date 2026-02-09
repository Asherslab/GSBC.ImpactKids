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
FROM nginx:1.27-alpine AS runtime
# Install Brotli module tools (serve .br files via nginx config)
RUN apk add --no-cache bash brotli

# Copy a hardened nginx config (added below) and mime types
COPY ./nginx.conf /etc/nginx/nginx.conf

# Copy published static site to nginx html root
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Optional: verify index.html exists (fail early if publish broke)
RUN test -f /usr/share/nginx/html/index.html

# Expose port
EXPOSE 80

# Healthcheck (simple)
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget -qO- http://127.0.0.1/healthz || exit 1

# Provide a basic /healthz file
RUN printf "ok" > /usr/share/nginx/html/healthz

# Run nginx in foreground
CMD ["nginx", "-g", "daemon off;"]