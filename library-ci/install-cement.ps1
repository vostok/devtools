Write-Host Cement: Dowloading latest release
Invoke-WebRequest "https://github.com/skbkontur/cement/releases/download/v1.0.32/ed4b4edd55fe9d8e3d598ea8e8cce5a5665bdccd.zip" -Out "cement.zip"

Write-Host Cement: Extracting release files
Expand-Archive "cement.zip" -Force -DestinationPath "cement"

New-Item -ItemType directory -Path "$env:USERPROFILE\bin\dotnet" > null

Write-Host Cement: Dowloading settings
Invoke-WebRequest "https://raw.githubusercontent.com/vostok/cement-modules/master/settings" -OutFile "$env:USERPROFILE\bin\dotnet\defaultSettings.json"

Write-Host Cement: Adding default log.config
New-Item -Path "$env:USERPROFILE\bin\dotnet" -Name 'log.config.xml' -Value '<?xml version="1.0" encoding="utf-8"?><log4net/>' > null

$cmpath = "$env:appveyor_build_folder\cement\dotnet\cm.exe"
$env:cm = $cmpath

Write-Host Cement: Configuring environment variable

[Environment]::SetEnvironmentVariable("cm", $cmpath, "User")

Write-Host Cement: Installation completed