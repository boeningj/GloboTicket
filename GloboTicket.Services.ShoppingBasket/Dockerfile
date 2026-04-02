FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5002

ENV ASPNETCORE_URLS=http://+:5002

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["GloboTicket.Services.ShoppingBasket/GloboTicket.Services.ShoppingBasket.csproj", "GloboTicket.Services.ShoppingBasket/"]
RUN dotnet restore "GloboTicket.Services.ShoppingBasket/GloboTicket.Services.ShoppingBasket.csproj"
COPY . .
WORKDIR "/src/GloboTicket.Services.ShoppingBasket"
RUN dotnet build "GloboTicket.Services.ShoppingBasket.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "GloboTicket.Services.ShoppingBasket.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GloboTicket.Services.ShoppingBasket.dll"]
