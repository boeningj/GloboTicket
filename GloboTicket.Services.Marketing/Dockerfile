FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5008

ENV ASPNETCORE_URLS=http://+:5008

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["GloboTicket.Services.Marketing/GloboTicket.Services.Marketing.csproj", "GloboTicket.Services.Marketing/"]
RUN dotnet restore "GloboTicket.Services.Marketing/GloboTicket.Services.Marketing.csproj"
COPY . .
WORKDIR "/src/GloboTicket.Services.Marketing"
RUN dotnet build "GloboTicket.Services.Marketing.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "GloboTicket.Services.Marketing.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GloboTicket.Services.Marketing.dll"]
