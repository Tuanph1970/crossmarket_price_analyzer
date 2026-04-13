FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CrossMarketAnalyzer.sln", "./"]
COPY ["src/Common/Common.Domain/Common.Domain.csproj", "src/Common/Common.Domain/"]
COPY ["src/Common/Common.Application/Common.Application.csproj", "src/Common/Common.Application/"]
COPY ["src/Common/Common.Infrastructure/Common.Infrastructure.csproj", "src/Common/Common.Infrastructure/"]
COPY ["src/Services/ProductService/ProductService.Domain/ProductService.Domain.csproj", "src/Services/ProductService/ProductService.Domain/"]
COPY ["src/Services/ProductService/ProductService.Application/ProductService.Application.csproj", "src/Services/ProductService/ProductService.Application/"]
COPY ["src/Services/ProductService/ProductService.Infrastructure/ProductService.Infrastructure.csproj", "src/Services/ProductService/ProductService.Infrastructure/"]
COPY ["src/Services/ProductService/ProductService.Api/ProductService.Api.csproj", "src/Services/ProductService/ProductService.Api/"]
COPY ["src/Services/ProductService/ProductService.Contracts/ProductService.Contracts.csproj", "src/Services/ProductService/ProductService.Contracts/"]
RUN dotnet restore "src/Services/ProductService/ProductService.Api/ProductService.Api.csproj"
COPY . .
RUN dotnet build "src/Services/ProductService/ProductService.Api/ProductService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/ProductService/ProductService.Api/ProductService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProductService.Api.dll"]
