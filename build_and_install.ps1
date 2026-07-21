$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Please run this script as an Administrator."
    Exit 1
}

# Define compiler and file paths
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$sourceFile = Join-Path $PSScriptRoot "Serial2KeyboardService.cs"
$outputExe = Join-Path $PSScriptRoot "Serial2KeyboardService.exe"

Write-Host "Compiling C# Service and Agent..."
if (-not (Test-Path $csc)) {
    Write-Error "C# Compiler (csc.exe) not found at: $csc"
    Exit 1
}

# Compile with target:winexe to hide console window and reference System.ServiceProcess
& $csc -target:winexe -r:System.ServiceProcess.dll -out:"$outputExe" "$sourceFile"

if (-not (Test-Path $outputExe)) {
    Write-Error "Compilation failed."
    Exit 1
}
Write-Host "Compilation successful: $outputExe"

# Service installation name
$serviceName = "Serial2KeyboardService"

# Stop and remove existing service if present
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    
    # Wait for service to stop
    Start-Sleep -Seconds 1
    
    Write-Host "Removing existing service..."
    & sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}

# Register the service
Write-Host "Registering Windows Service..."
try {
    New-Service -Name $serviceName -BinaryPathName "`"$outputExe`"" -StartupType Automatic -ErrorAction Stop | Out-Null
} catch {
    Write-Error "Failed to register service: $_"
    Exit 1
}

# Set service description
& sc.exe description $serviceName "Monitors serial ports and pushes inputs to active user keyboard." | Out-Null

# Start the service
Write-Host "Starting service..."
Start-Service -Name $serviceName

if ((Get-Service -Name $serviceName).Status -eq "Running") {
    Write-Host "Service started successfully!"
} else {
    Write-Warning "Service was registered but failed to start. Check service.log."
}
