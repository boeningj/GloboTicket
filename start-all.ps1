# start-all.ps1
# Run all GloboTicket services and client on Windows in separate terminals

# --------------------------------------------
# Helper: Load env file into a hashtable
# --------------------------------------------
function Load-EnvFileToHash {
    param (
        [Parameter(Mandatory)]
        [string]$EnvFilePath
    )

    $envHash = @{}

    if (Test-Path $EnvFilePath) {
        Get-Content $EnvFilePath | ForEach-Object {
            if ($_ -and $_ -notmatch '^\s*#') {
                $name, $value = $_ -split '=', 2
                $envHash[$name.Trim()] = $value.Trim()
            }
        }
        Write-Host "Loaded env vars from $EnvFilePath"
    } else {
        Write-Host "$EnvFilePath not found — skipping"
    }

    return $envHash
}

# --------------------------------------------
# Helper: Start a service in a new PowerShell process with env vars
# --------------------------------------------
function Start-ServiceWithEnv {
    param (
        [string]$Path,
        [string]$Name,
        [hashtable]$EnvVars
    )

    Write-Host "Starting $Name..."

    # Build env var arguments
    $envArgs = (
    $EnvVars.GetEnumerator() | ForEach-Object {
        "[Environment]::SetEnvironmentVariable('$($_.Key)', '$($_.Value)', 'Process');"
    }
    ) -join " "

    $psCommand = "$envArgs cd '$Path'; dotnet run"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $psCommand
}

# --------------------------------------------
# Load shared env vars once
# --------------------------------------------
$sharedEnv = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.local")

# Determine authentication mode
$authMode = $sharedEnv["AUTHENTICATIONOPTIONS__AUTHMODE"]
Write-Host "Authentication Mode = $authMode"

# --------------------------------------------
# Start services
# --------------------------------------------

$baseDir = "D:\home.jonathan\Documents\Pluralsight\ASP.NET Microservices\microservices-communication-asp-dot-net-core\05\demos\Resiliency"

# --------------------------------------------
# Start GloboTicket.IDP - IDP service gets its own env overlay
# --------------------------------------------
$idpEnv = $sharedEnv.Clone()
$idpSpecific = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.idp.local")
foreach ($k in $idpSpecific.Keys) { $idpEnv[$k] = $idpSpecific[$k] }
Start-ServiceWithEnv "$baseDir\GloboTicket.IDP" "GloboTicket IDP" $idpEnv

# --------------------------------------------
# Start EventCatalog - EventCatalog service gets its own env overlay
# --------------------------------------------
$eventCatalogEnv = $sharedEnv.Clone()
$eventCatalogSpecific = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.eventcatalog.local")
foreach ($k in $eventCatalogSpecific.Keys) { $eventCatalogEnv[$k] = $eventCatalogSpecific[$k] }
Start-ServiceWithEnv "$baseDir\GloboTicket.Services.EventCatalog" "EventCatalog API" $eventCatalogEnv

# Start IntegrationEventPublisher
Start-ServiceWithEnv "$baseDir\GloboTicket.Services.IntegrationEventPublisher" "IntegrationEventPublisher API" $sharedEnv

# --------------------------------------------
# Start ShoppingBasket - ShoppingBasket service gets its own env overlay
# --------------------------------------------
$shoppingBasketEnv = $sharedEnv.Clone()
$shoppingBasketSpecific = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.shoppingbasket.local")
foreach ($k in $shoppingBasketSpecific.Keys) { $shoppingBasketEnv[$k] = $shoppingBasketSpecific[$k] }
Start-ServiceWithEnv "$baseDir\GloboTicket.Services.ShoppingBasket" "ShoppingBasket API" $shoppingBasketEnv

# Wait for ShoppingBasket to start (port 5002)
$port = 5002
while (-not (Test-NetConnection -ComputerName "localhost" -Port $port -WarningAction SilentlyContinue).TcpTestSucceeded) {
    Write-Host "Waiting for ShoppingBasket on port $port..."
    Start-Sleep -Seconds 1
}
Write-Host "ShoppingBasket is up!"

# --------------------------------------------
# Start Marketing - Marketing service gets its own env overlay
# --------------------------------------------
$marketingEnv = $sharedEnv.Clone()
$marketingSpecific = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.marketing.local")
foreach ($k in $marketingSpecific.Keys) { $marketingEnv[$k] = $marketingSpecific[$k] }
Start-ServiceWithEnv "$baseDir\GloboTicket.Services.Marketing" "Marketing API" $marketingEnv

# --------------------------------------------
# Start Discount - Discount service gets its own env overlay
# --------------------------------------------
$discountEnv = $sharedEnv.Clone()
$discountSpecific = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.discount.local")
foreach ($k in $discountSpecific.Keys) { $discountEnv[$k] = $discountSpecific[$k] }
Start-ServiceWithEnv "$baseDir\GloboTicket.Services.Discount" "Discount API" $discountEnv

# --------------------------------------------
# Start Order - Order service gets its own env overlay
# --------------------------------------------
$orderEnv = $sharedEnv.Clone()
$orderSpecific = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.order.local")
foreach ($k in $orderSpecific.Keys) { $orderEnv[$k] = $orderSpecific[$k] }
Start-ServiceWithEnv "$baseDir\GloboTicket.Services.Order" "Order API" $orderEnv

# Start Payment
Start-ServiceWithEnv "$baseDir\GloboTicket.Services.Payment" "Payment API" $sharedEnv

# Start PaymentGateway
Start-ServiceWithEnv "$baseDir\External.PaymentGateway" "External Payment Gateway" $sharedEnv

# --------------------------------------------
# Start Ocelot Gateway if using TrustGateway mode
# --------------------------------------------
if ($authMode -eq "TrustGateway") {
    # --------------------------------------------
    # Ocelot service gets its own env overlay
    # --------------------------------------------
    $ocelotGatewayEnv = $sharedEnv.Clone()
    $ocelotGatewaySpecific = Load-EnvFileToHash (Join-Path $PSScriptRoot ".env.gateway.ocelot.local")
    foreach ($k in $ocelotGatewaySpecific.Keys) { $ocelotGatewayEnv[$k] = $ocelotGatewaySpecific[$k] }
    Start-ServiceWithEnv "$baseDir\GloboTicket.Gateway.Ocelot" "Ocelot Gateway" $ocelotGatewayEnv

    # Wait for Ocelot Gateway to start (port 5050)
    $gatewayPort = 5050
    while (-not (Test-NetConnection -ComputerName "localhost" -Port $gatewayPort -WarningAction SilentlyContinue).TcpTestSucceeded) {
        Write-Host "Waiting for Ocelot Gateway on port $gatewayPort..."
        Start-Sleep -Seconds 1
    }
    Write-Host "Ocelot Gateway is up!"
}

# Finally, start the MVC client
Start-ServiceWithEnv "$baseDir\GloboTicket.Client" "MVC Client" $sharedEnv