# Load env vars
$envFile = ".\.env.k8s"

Write-Host "Loading environment variables from $envFile"

Get-Content $envFile | ForEach-Object {
    if ($_ -and -not $_.StartsWith("#")) {
        $name, $value = $_ -split "=", 2
        [System.Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

# Run client
cd .\GloboTicket.Client
dotnet run