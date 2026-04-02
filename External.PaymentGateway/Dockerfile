FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5004

ENV ASPNETCORE_URLS=http://+:5004

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["External.PaymentGateway/External.PaymentGateway.csproj", "External.PaymentGateway/"]
RUN dotnet restore "External.PaymentGateway/External.PaymentGateway.csproj"
COPY . .
WORKDIR "/src/External.PaymentGateway"
RUN dotnet build "External.PaymentGateway.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "External.PaymentGateway.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "External.PaymentGateway.dll"]
