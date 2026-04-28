FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CrossMarketAnalyzer.sln", "./"]
COPY ["src/Common/", "src/Common/"]
COPY ["src/Services/ScrapingService/", "src/Services/ScrapingService/"]
RUN dotnet restore "src/Services/ScrapingService/ScrapingService.Worker/ScrapingService.Worker.csproj"
COPY . .
RUN dotnet build "src/Services/ScrapingService/ScrapingService.Worker/ScrapingService.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/ScrapingService/ScrapingService.Worker/ScrapingService.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install Chromium + system dependencies needed to launch headless Chrome.
# Microsoft.Playwright.dll has no runtimeconfig of its own — borrow the Worker's.
RUN dotnet exec \
    --runtimeconfig /app/ScrapingService.Worker.runtimeconfig.json \
    --depsfile /app/ScrapingService.Worker.deps.json \
    /app/Microsoft.Playwright.dll install --with-deps chromium \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "ScrapingService.Worker.dll"]
