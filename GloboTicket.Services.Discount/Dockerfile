FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5007

ENV ASPNETCORE_URLS=http://+:5007

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["GloboTicket.Services.Discount/GloboTicket.Services.Discount.csproj", "GloboTicket.Services.Discount/"]
RUN dotnet restore "GloboTicket.Services.Discount/GloboTicket.Services.Discount.csproj"
COPY . .
WORKDIR "/src/GloboTicket.Services.Discount"
RUN dotnet build "GloboTicket.Services.Discount.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "GloboTicket.Services.Discount.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GloboTicket.Services.Discount.dll"]
