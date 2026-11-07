# Get the common SDO Install module and import it
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

# install SDO Tools
#########################
Write-OutputMessage $fileName "Installing SDO Tools..."
$result = Install-Sdo
if (-not $result) {
    Write-Host "Failed to install sdo. Please check the logs for more details." -ForegroundColor Red
    exit 1
}

Write-OutputMessage $fileName "Completed installation script."
Write-OutputMessage $fileName "EmtpyLine"
exit 0
