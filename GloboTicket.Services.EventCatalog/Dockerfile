FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

ENV ASPNETCORE_URLS=http://+:5001

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["GloboTicket.Services.EventCatalog/GloboTicket.Services.EventCatalog.csproj", "GloboTicket.Services.EventCatalog/"]
RUN dotnet restore "GloboTicket.Services.EventCatalog/GloboTicket.Services.EventCatalog.csproj"
COPY . .
WORKDIR "/src/GloboTicket.Services.EventCatalog"
RUN dotnet build "GloboTicket.Services.EventCatalog.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "GloboTicket.Services.EventCatalog.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GloboTicket.Services.EventCatalog.dll"]
