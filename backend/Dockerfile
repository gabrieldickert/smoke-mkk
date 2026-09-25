FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SmokeMkk.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
# The image's built-in non-root user; the image listens on 8080 (ASPNETCORE_HTTP_PORTS), so no ASPNETCORE_URLS.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmokeMkk.Api.dll"]
