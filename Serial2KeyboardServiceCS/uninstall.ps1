$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Please run this script as an Administrator."
    Exit 1
}

$serviceName = "Serial2KeyboardService"

# Stop the service
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping service..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    
    # Remove service
    Write-Host "Uninstalling service..."
    & sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}

# Stop all running instances of the executable (including the agent)
Write-Host "Stopping agent processes..."
Stop-Process -Name "Serial2KeyboardService" -Force -ErrorAction SilentlyContinue

Write-Host "Uninstallation complete."
