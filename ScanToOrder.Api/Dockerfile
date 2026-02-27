FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ScanToOrder.Api/ScanToOrder.Api.csproj", "ScanToOrder.Api/"]
COPY ["ScanToOrder.Application/ScanToOrder.Application.csproj", "ScanToOrder.Application/"]
COPY ["ScanToOrder.Domain/ScanToOrder.Domain.csproj", "ScanToOrder.Domain/"]
COPY ["ScanToOrder.Infrastructure/ScanToOrder.Infrastructure.csproj", "ScanToOrder.Infrastructure/"]
RUN dotnet restore "ScanToOrder.Api/ScanToOrder.Api.csproj"
COPY . .
WORKDIR "/src/ScanToOrder.Api"
RUN dotnet build "ScanToOrder.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ScanToOrder.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ScanToOrder.Api.dll"]
