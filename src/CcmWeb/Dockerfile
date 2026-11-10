# Image for the CcmWeb MVC application (net10.0). Two stages: publish with the SDK, run on the
# smaller ASP.NET Core runtime. The connection string never enters the image - docker-compose.yml
# passes it as ConnectionStrings__DefaultConnection at run time.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, in its own layer, so an edit to the source does not re-download the packages.
COPY CcmWeb.csproj ./
RUN dotnet restore CcmWeb.csproj

COPY . ./
RUN dotnet publish CcmWeb.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Data Protection keys are written here so a restart does not sign everybody out; compose mounts a
# volume on this path. The runtime image runs as the unprivileged user "app", which cannot create a
# directory under /, so it is created and handed over while still root.
RUN mkdir -p /keys && chown -R app:app /keys

COPY --from=build /app/publish ./

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "CcmWeb.dll"]
