FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Apps/CMA.Gateway/CMA.Gateway.csproj", "src/Apps/CMA.Gateway/"]
RUN dotnet restore "src/Apps/CMA.Gateway/CMA.Gateway.csproj"
COPY . .
RUN dotnet build "src/Apps/CMA.Gateway/CMA.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Apps/CMA.Gateway/CMA.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CMA.Gateway.dll"]
