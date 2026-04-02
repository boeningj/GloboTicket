FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5050
EXPOSE 5051

ENV ASPNETCORE_URLS="http://+:5050;https://+:5051"

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["GloboTicket.Gateway.Ocelot/GloboTicket.Gateway.Ocelot.csproj", "GloboTicket.Gateway.Ocelot/"]
RUN dotnet restore "GloboTicket.Gateway.Ocelot/GloboTicket.Gateway.Ocelot.csproj"
COPY . .
WORKDIR "/src/GloboTicket.Gateway.Ocelot"
RUN dotnet build "GloboTicket.Gateway.Ocelot.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "GloboTicket.Gateway.Ocelot.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GloboTicket.Gateway.Ocelot.dll"]
