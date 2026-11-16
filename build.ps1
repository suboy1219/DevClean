param(
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if ($SelfContained) {
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
} else {
    dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
}

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "输出目录: $PSScriptRoot\publish" -ForegroundColor Green
Get-ChildItem "$PSScriptRoot\publish" | Select-Object Name, Length | Format-Table -AutoSize
