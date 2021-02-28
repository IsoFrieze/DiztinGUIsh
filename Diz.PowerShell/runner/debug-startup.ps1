$module = "Diz.PowerShell"
$basepath = ".\Diz.PowerShell\bin\Debug\net5.0\"

echo "Starting..."
Import-Module "$($basepath)$($module).dll"
pwd

if ((Get-Command -module $module).Count -eq 0) { 
  Write-Host "WARNING: Couldn't find our module. (build issue?)" -ForegroundColor red
} else {
  Write-Host "Ready!" -ForegroundColor green
}