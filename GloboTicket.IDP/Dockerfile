FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5020
EXPOSE 5021

ENV ASPNETCORE_URLS="http://+:5020;https://+:5021"

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["GloboTicket.IDP/GloboTicket.IDP.csproj", "GloboTicket.IDP/"]
RUN dotnet restore "GloboTicket.IDP/GloboTicket.IDP.csproj"
COPY . .
WORKDIR "/src/GloboTicket.IDP"
RUN dotnet build "GloboTicket.IDP.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "GloboTicket.IDP.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GloboTicket.IDP.dll"]
