# --------------------------------------------
# Start-All-Docker.ps1
# Runs containerized services + host-based MVC client
# Ensures containers can communicate via a dedicated network
# --------------------------------------------

# Name of the Docker network
$networkName = "globoticket-network"

# Check if the network exists; if not, create it
$existingNetwork = docker network ls --format "{{.Name}}" | Select-String -Pattern "^$networkName$"
if (-not $existingNetwork) {
    Write-Host "Creating Docker network '$networkName'..."
    docker network create $networkName
} else {
    Write-Host "Docker network '$networkName' already exists."
}

# Load environment variables for the client
$envFile = ".\.env.docker"
Write-Host "Loading environment variables from $envFile"
Get-Content $envFile | ForEach-Object {
    if ($_ -and -not $_.StartsWith("#")) {
        $name, $value = $_ -split "=", 2
        [System.Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

# Environment variables specific to IDP container
$idpEnvFile = ".\.env.idp.docker"
# Environment variables specific to EventCatalog container
$eventCatalogEnvFile = ".\.env.eventcatalog.docker"
# Environment variables specific to ShoppingBasket container
$shoppingBasketEnvFile = ".\.env.shoppingbasket.docker"
# Environment variables specific to Discount container
$discountEnvFile = ".\.env.discount.docker"
# Environment variables specific to Order container
$orderEnvFile = ".\.env.order.docker"
# Environment variables specific to Marketing container
$marketingEnvFile = ".\.env.marketing.docker"

# Stop any old containers
#docker rm -f globoticket-eventcatalog globoticket-shoppingbasket -ErrorAction SilentlyContinue

# Start containerized services in detached mode
Write-Host "Starting IDP container..."
docker run -d --rm --name globoticket-idp `
    --network $networkName `
    --env-file $envFile `
    --env-file $idpEnvFile `
    -v ".\GloboTicket.IDP\keys:/app/keys" `
    -v "$env:USERPROFILE\.aspnet\https:/https" `
    -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/globoticket-idp.pfx `
    -e ASPNETCORE_Kestrel__Certificates__Default__Password=idpdevcert! `
    -p 5020:5020 `
    -p 5021:5021 `
    globoticket.idp

Write-Host "Waiting for IDP JWKS endpoint..."
$jwksUrl = "http://globoticket-idp:5020/.well-known/openid-configuration/jwks"
do {
    Start-Sleep -Seconds 1
    $status = docker run --rm --network $networkName curlimages/curl:latest -o /dev/null -s -w "%{http_code}" -k "$jwksUrl"
} while ($status -ne "200")
Write-Host "IDP JWKS endpoint is ready."

Write-Host "Starting EventCatalog container..."
docker run -d --rm --name globoticket-eventcatalog `
    --network $networkName `
    --env-file $envFile `
    --env-file $eventCatalogEnvFile `
    -p 5001:5001 globoticket.eventcatalog    

Write-Host "Starting ShoppingBasket container..."
docker run -d --rm --name globoticket-shoppingbasket `
    --network $networkName `
    --env-file $envFile `
    --env-file $shoppingBasketEnvFile `
    -p 5002:5002 globoticket.shoppingbasket

Write-Host "Starting Discount container..."
docker run -d --rm --name globoticket-discount `
    --network $networkName `
    --env-file $envFile `
    --env-file $discountEnvFile `
    -p 5007:5007 globoticket.discount

Write-Host "Starting Ordering container..."
docker run -d --rm --name globoticket-ordering `
    --network $networkName `
    --env-file $envFile `
    --env-file $orderEnvFile `
    -p 5005:5005 globoticket.ordering

Write-Host "Starting External Payment Gateway container..."
docker run -d --rm --name globoticket-externalpaymentgateway `
    --network $networkName `
    --env-file $envFile `
    -p 5004:5004 globoticket.externalpaymentgateway

Write-Host "Starting Payment container..."
docker run -d --rm --name globoticket-payment `
    --network $networkName `
    --env-file $envFile `
    -p 5006:5006 globoticket.payment

Write-Host "Starting Marketing container..."
docker run -d --rm --name globoticket-marketing `
    --network $networkName `
    --env-file $envFile `
    --env-file $marketingEnvFile `
    -p 5008:5008 globoticket.marketing

Write-Host "Starting Integration Event Publisher container..."
docker run -d --rm --name globoticket-integrationeventpublisher `
    --network $networkName `
    --env-file $envFile `
    globoticket.integrationeventpublisher
# No port exposure is needed in this case since this service doesn’t serve HTTP requests

# Start Ocelot Gateway container if using TrustGateway mode
if ($env:AUTHENTICATIONOPTIONS__AUTHMODE -eq "TrustGateway") {

    Write-Host "Starting Ocelot Gateway container..."
    $ocelotEnvFile = ".\.env.gateway.ocelot.docker"
    docker run -d --rm --name globoticket-gateway-ocelot `
        --network $networkName `
        --env-file $envFile `
        --env-file $ocelotEnvFile `
        -v "$env:USERPROFILE\.aspnet\https:/https" `
        -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/globoticket-idp.pfx `
        -e ASPNETCORE_Kestrel__Certificates__Default__Password=idpdevcert! `
        -e ASPNETCORE_ENVIRONMENT=Docker `
        -p 5050:5050 `
        -p 5051:5051 `
        globoticket.gateway.ocelot

    # Wait for Ocelot Gateway to start
    $gatewayPort = 5050
    while (-not (Test-NetConnection -ComputerName "localhost" -Port $gatewayPort -WarningAction SilentlyContinue).TcpTestSucceeded) {
        Write-Host "Waiting for Ocelot Gateway on port $gatewayPort..."
        Start-Sleep -Seconds 1
    }
    Write-Host "Ocelot Gateway is up!"
    Write-Host "Gateway URI (EventCatalog) =" $env:APICONFIGS__EVENTCATALOG__GATEWAYURI
    Write-Host "Gateway URI (ShoppingBasket) =" $env:APICONFIGS__SHOPPINGBASKET__GATEWAYURI
    Write-Host "Gateway URI (Order) =" $env:APICONFIGS__ORDER__GATEWAYURI
}

# Start the non-containerized client in this terminal
Write-Host "Starting the non-containerized client..."
cd .\GloboTicket.Client
dotnet run