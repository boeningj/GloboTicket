FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["Globoticket.Services.IntegrationEventPublisher/Globoticket.Services.IntegrationEventPublisher.csproj", "Globoticket.Services.IntegrationEventPublisher/"]
RUN dotnet restore "Globoticket.Services.IntegrationEventPublisher/Globoticket.Services.IntegrationEventPublisher.csproj"
COPY . .
WORKDIR "/src/Globoticket.Services.IntegrationEventPublisher"
RUN dotnet build "Globoticket.Services.IntegrationEventPublisher.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Globoticket.Services.IntegrationEventPublisher.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Globoticket.Services.IntegrationEventPublisher.dll"]
