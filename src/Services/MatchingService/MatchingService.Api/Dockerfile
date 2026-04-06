FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Common/Common.Domain/Common.Domain.csproj", "src/Common/Common.Domain/"]
COPY ["src/Common/Common.Application/Common.Application.csproj", "src/Common/Common.Application/"]
COPY ["src/Common/Common.Infrastructure/Common.Infrastructure.csproj", "src/Common/Common.Infrastructure/"]
COPY ["src/Services/MatchingService/MatchingService.Domain/MatchingService.Domain.csproj", "src/Services/MatchingService/MatchingService.Domain/"]
COPY ["src/Services/MatchingService/MatchingService.Application/MatchingService.Application.csproj", "src/Services/MatchingService/MatchingService.Application/"]
COPY ["src/Services/MatchingService/MatchingService.Infrastructure/MatchingService.Infrastructure.csproj", "src/Services/MatchingService/MatchingService.Infrastructure/"]
COPY ["src/Services/MatchingService/MatchingService.Api/MatchingService.Api.csproj", "src/Services/MatchingService/MatchingService.Api/"]
RUN dotnet restore "src/Services/MatchingService/MatchingService.Api/MatchingService.Api.csproj"
COPY . .
RUN dotnet build "src/Services/MatchingService/MatchingService.Api/MatchingService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Services/MatchingService/MatchingService.Api/MatchingService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MatchingService.Api.dll"]
