FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/AssetMonitoring.Api/AssetMonitoring.Api.csproj
RUN dotnet publish src/AssetMonitoring.Api/AssetMonitoring.Api.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AssetMonitoring.Api.dll"]
