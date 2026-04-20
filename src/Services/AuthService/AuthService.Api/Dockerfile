FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CrossMarketAnalyzer.sln", "./"]
COPY ["src/Common/", "src/Common/"]
COPY ["src/Services/AuthService/", "src/Services/AuthService/"]
COPY ["src/Services/AuthService/CrossMarket.SharedKernel/", "src/Services/AuthService/CrossMarket.SharedKernel/"]
RUN dotnet restore "src/Services/AuthService/AuthService.Api/AuthService.Api.csproj"
COPY . .
RUN dotnet build "src/Services/AuthService/AuthService.Api/AuthService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/AuthService/AuthService.Api/AuthService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["src/Services/AuthService/AuthService.Api/appsettings.json", "./"]
ENTRYPOINT ["dotnet", "AuthService.Api.dll"]
