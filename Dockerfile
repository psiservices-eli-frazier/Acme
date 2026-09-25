# syntax=docker/dockerfile:1

# ---- client: build the SPA straight into the server's wwwroot -----------------------
FROM node:22-alpine AS client-build
WORKDIR /repo
COPY src/web.client/package.json src/web.client/package-lock.json src/web.client/
RUN npm ci --prefix src/web.client
COPY src/web.client/ src/web.client/
# vite.config.ts's outDir ('../Acme.Server/wwwroot') is relative to src/web.client, so
# the repo-relative layout above has to be preserved for the build to land in the right
# place: /repo/src/Acme.Server/wwwroot.
RUN npm run build --prefix src/web.client

# ---- server: publish with the built SPA already in wwwroot --------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /repo
COPY Directory.Build.props ./
COPY src/Acme.Server/ src/Acme.Server/
COPY --from=client-build /repo/src/Acme.Server/wwwroot/ src/Acme.Server/wwwroot/
RUN dotnet publish src/Acme.Server/Acme.Server.csproj --configuration Release --output /app/publish

# ---- runtime: framework-dependent, no SDK ------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=server-build /app/publish ./

# Render (and most PaaS Docker hosts) assign the listen port via $PORT; 10000 is Render's
# own default when nothing else is specified. Kestrel binds after the shell substitutes
# $PORT, and `exec` hands off PID 1 to dotnet so it receives SIGTERM directly on restart.
ENV PORT=10000
EXPOSE 10000
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:$PORT exec dotnet Acme.Server.dll"]
