$module = "Diz.PowerShell"
$basepath = ".\"

echo "Starting..."
Import-Module "$($basepath)$($module).dll"

WRite-Host "Current working dir---> $($pwd)'"

if ((Get-Command -module $module).Count -eq 0) { 
  Write-Host "WARNING: Couldn't find our module. (build issue?)" -ForegroundColor red
} else {
  Write-Host "Ready!" -ForegroundColor green
}

$dizproject = "testproject.dizraw"
$cmd_to_run = "Build-AssemblyFiles -ProjectNames (Resolve-Path '..\..\..\..\..\rom\$($dizproject)')"

Write-Host "Press enter to run this command: '$($cmd_to_run)'"
$null = Read-Host

Invoke-Expression $cmd_to_run