#!/bin/bash

BASE_DIR=~/Documents/Pluralsight/ASP.NET\ Microservices/microservices-communication-asp-dot-net-core/05/demos/Resiliency

# Start EventCatalog API
gnome-terminal -- bash -c "cd \"$BASE_DIR/GloboTicket.Services.EventCatalog\" && dotnet run; exec bash"

# Start ShoppingBasket API first (Marketing depends on this)
gnome-terminal -- bash -c "cd \"$BASE_DIR/GloboTicket.Services.ShoppingBasket\" && dotnet run; exec bash"

# Wait a few seconds to allow ShoppingBasket to start
#sleep 5

#Make sure ShoppingBasket is up & listening on port 5002
PORT=5002
while ! nc -z localhost $PORT; do
  echo "Waiting for ShoppingBasket to start..."
  sleep 1
done
echo "ShoppingBasket is up!"

# Start Marketing API
gnome-terminal -- bash -c "cd \"$BASE_DIR/GloboTicket.Services.Marketing\" && dotnet run; exec bash"

# Start remaining services
gnome-terminal -- bash -c "cd \"$BASE_DIR/GloboTicket.Services.Discount\" && dotnet run; exec bash"
gnome-terminal -- bash -c "cd \"$BASE_DIR/GloboTicket.Services.Order\" && dotnet run; exec bash"
gnome-terminal -- bash -c "cd \"$BASE_DIR/GloboTicket.Services.Payment\" && dotnet run; exec bash"
gnome-terminal -- bash -c "cd \"$BASE_DIR/External.PaymentGateway\" && dotnet run; exec bash"

# Start the MVC client last
gnome-terminal -- bash -c "cd \"$BASE_DIR/GloboTicket.Client\" && dotnet run; exec bash"
