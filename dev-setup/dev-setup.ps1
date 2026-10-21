param(
    [string]$SdoVersion = "2.2.0"  # default version of SDO to install
)
# Get the shared SDO setup module and import it
#########################
$url = "https://raw.githubusercontent.com/naz-hage/sdo/main/scripts/module-package/sdo-scripts.psm1"
$output = "./sdo-scripts.psm1"
Invoke-WebRequest -Uri $url -OutFile $output
Import-Module $output -Force

$fileName = Split-Path -Leaf $PSCommandPath

Write-OutputMessage $fileName "Started installation script."

# Check if admin
#########################
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-OutputMessage $fileName "Error: Please run this script as an administrator."
    exit 1
} else {
    Write-OutputMessage $fileName "Admin rights detected"
}

# install SDO
#########################
Write-OutputMessage $fileName "Installing SDO..."
$result = Install-Sdo -Version $SdoVersion
if (-not $result) {
    Write-Host "Failed to install SDO. Please check the logs for more details." -ForegroundColor Red
    exit 1
}

# install Nuget
#########################
Write-OutputMessage $fileName "Installing Nuget..."
& "$env:ProgramFiles/Sdo/sdo.exe" tool install --manifest .\nuget.yaml
if ($LASTEXITCODE -ne 0) {
    Write-OutputMessage $fileName "Error: Installation of nuget.yaml failed. Exiting script."
    exit 1
}

Write-OutputMessage $fileName "Completed installation script."
Write-OutputMessage $fileName "EmtpyLine"