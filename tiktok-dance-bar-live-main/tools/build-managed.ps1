param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $projectRoot 'Build/TIKTOK_LIVE_BAR_Data/Managed'
$outputDir = Join-Path $projectRoot '.maintenance/build'
New-Item -ItemType Directory -Force $outputDir | Out-Null
$sdk = @(& dotnet --list-sdks) | Select-Object -Last 1
if (!$sdk) { throw 'Install .NET SDK to use the managed build helper.' }
$sdkVersion = ($sdk -split ' ')[0]
$sdkRoot = [regex]::Match($sdk, '\[(.+)\]').Groups[1].Value
$compiler = Join-Path $sdkRoot "$sdkVersion/Roslyn/bincore/csc.dll"
$outputDll = Join-Path $outputDir 'Assembly-CSharp.dll'
$arguments = @('/nostdlib+', '/target:library', '/langversion:latest', ('/out:"' + $outputDll + '"'))
$arguments += Get-ChildItem -LiteralPath $managed -Filter '*.dll' | Where-Object Name -ne 'Assembly-CSharp.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$arguments += Get-ChildItem -LiteralPath (Join-Path $projectRoot 'UnityProject/Assets/Scripts') -Filter '*.cs' -Recurse | ForEach-Object { '"' + $_.FullName + '"' }
$responseFile = Join-Path $outputDir 'compile.rsp'
$arguments | Set-Content -LiteralPath $responseFile -Encoding utf8
& dotnet $compiler "@$responseFile"
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
if ($Apply) {
    if (Get-Process -Name TIKTOK_LIVE_BAR -ErrorAction SilentlyContinue) { throw 'Close the game before applying the DLL.' }
    Copy-Item -LiteralPath $outputDll -Destination (Join-Path $managed 'Assembly-CSharp.dll') -Force
}
Write-Output "Compiled: $outputDll"
