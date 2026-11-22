FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src
COPY global.json .
COPY apps/api/src/Trono.Api/Trono.Api.csproj apps/api/src/Trono.Api/
COPY apps/api/src/Trono.Modules.Identity/Trono.Modules.Identity.csproj apps/api/src/Trono.Modules.Identity/
COPY apps/api/src/Trono.Modules.Records/Trono.Modules.Records.csproj apps/api/src/Trono.Modules.Records/
RUN dotnet restore apps/api/src/Trono.Api/Trono.Api.csproj
COPY apps/api/src/Trono.Api/ apps/api/src/Trono.Api/
COPY apps/api/src/Trono.Modules.Identity/ apps/api/src/Trono.Modules.Identity/
COPY apps/api/src/Trono.Modules.Records/ apps/api/src/Trono.Modules.Records/
RUN dotnet publish apps/api/src/Trono.Api/Trono.Api.csproj -c Release --no-restore -o /app

FROM build AS migrations
COPY .config/dotnet-tools.json .config/dotnet-tools.json
RUN dotnet tool restore
COPY infrastructure/migrate-local.sh /usr/local/bin/trono-migrate-local
ENTRYPOINT ["/bin/sh", "/usr/local/bin/trono-migrate-local"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
EXPOSE 8443
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Trono.Api.dll"]
