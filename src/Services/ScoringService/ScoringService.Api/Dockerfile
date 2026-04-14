FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CrossMarketAnalyzer.sln", "./"]
COPY ["src/Common/", "src/Common/"]
COPY ["src/Services/ScoringService/", "src/Services/ScoringService/"]
RUN dotnet restore "src/Services/ScoringService/ScoringService.Api/ScoringService.Api.csproj"
COPY . .
RUN dotnet build "src/Services/ScoringService/ScoringService.Api/ScoringService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/ScoringService/ScoringService.Api/ScoringService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ScoringService.Api.dll"]
