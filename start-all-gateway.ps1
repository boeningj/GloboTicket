# start-all-gateway.ps1
# Run all GloboTicket services, BFFs, and MVC client on Windows in separate terminals

$baseDir = "D:\home.jonathan\Documents\Pluralsight\ASP.NET Microservices\microservices-communication-asp-dot-net-core\05\demos\Resiliency"

function Start-ServiceWindow {
    param (
        [string]$Path,
        [string]$Name
    )
    Write-Host "Starting $Name..."
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$Path'; dotnet run"
}

# ----------------------------------------------
# Start GloboTicket.IDP
# ----------------------------------------------
Start-ServiceWindow "$baseDir\GloboTicket.IDP" "GloboTicket IDP"

# ----------------------------------------------
# Start EventCatalog
# ----------------------------------------------
Start-ServiceWindow "$baseDir\GloboTicket.Services.EventCatalog" "EventCatalog API"

# ----------------------------------------------
# Start ShoppingBasket (needs to be first)
# ----------------------------------------------
Start-ServiceWindow "$baseDir\GloboTicket.Services.ShoppingBasket" "ShoppingBasket API"

# Wait for ShoppingBasket API to become available on port 5002
$port = 5002
Write-Host "Waiting for ShoppingBasket to start on port $port..."
while (-not (Test-NetConnection -ComputerName "localhost" -Port $port -WarningAction SilentlyContinue).TcpTestSucceeded) {
    Start-Sleep -Seconds 1
}
Write-Host "ShoppingBasket is up!"

# ----------------------------------------------
# Start Marketing (depends on ShoppingBasket)
# ----------------------------------------------
Start-ServiceWindow "$baseDir\GloboTicket.Services.Marketing" "Marketing API"

# ----------------------------------------------
# Start remaining microservices
# ----------------------------------------------
Start-ServiceWindow "$baseDir\GloboTicket.Services.Discount" "Discount API"
Start-ServiceWindow "$baseDir\GloboTicket.Services.Order" "Order API"
Start-ServiceWindow "$baseDir\GloboTicket.Services.Payment" "Payment API"
Start-ServiceWindow "$baseDir\External.PaymentGateway" "External Payment Gateway"

# ----------------------------------------------
# Start API Gateways / BFFs
# ----------------------------------------------
Start-ServiceWindow "$baseDir\GloboTicket.Gateway.WebBff" "Web BFF (Gateway)"
#Start-ServiceWindow "$baseDir\GloboTicket.Web.Bff" "MVC BFF"