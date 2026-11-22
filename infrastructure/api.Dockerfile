FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src
COPY global.json .
COPY apps/api/src/Trono.Api/Trono.Api.csproj apps/api/src/Trono.Api/
COPY apps/api/src/Trono.Modules.Identity/Trono.Modules.Identity.csproj apps/api/src/Trono.Modules.Identity/
RUN dotnet restore apps/api/src/Trono.Api/Trono.Api.csproj
COPY apps/api/src/Trono.Api/ apps/api/src/Trono.Api/
COPY apps/api/src/Trono.Modules.Identity/ apps/api/src/Trono.Modules.Identity/
RUN dotnet publish apps/api/src/Trono.Api/Trono.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Trono.Api.dll"]
