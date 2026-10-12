param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (!$dotnetCommand) { throw 'Install the .NET 9 SDK or newer for Windows, then run this script again.' }
$project = Join-Path $PSScriptRoot 'source\BeQuiet.csproj'
$output = Join-Path $PSScriptRoot 'build\publish'
& $dotnetCommand.Source publish $project -c $Configuration -r win-x64 --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
$release = Join-Path $PSScriptRoot 'release'
Copy-Item -LiteralPath (Join-Path $output 'BeQuiet.exe') -Destination (Join-Path $release 'BeQuiet.exe') -Force
$hash = (Get-FileHash -LiteralPath (Join-Path $release 'BeQuiet.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Value "$hash  BeQuiet.exe" -Encoding ascii
$archive = Join-Path $release 'BeQuiet-1.2.0-win-x64.zip'
$archiveFiles = @('BeQuiet.exe', 'README.md', 'LICENSE', 'LICENSE-DotNet.txt', 'LICENSE-NAudio.txt', 'THIRD-PARTY-NOTICES.md', 'SHA256SUMS.txt') | ForEach-Object { Join-Path $release $_ }
Compress-Archive -LiteralPath $archiveFiles -DestinationPath $archive -Force
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Add-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Value "$archiveHash  BeQuiet-1.2.0-win-x64.zip" -Encoding ascii
& (Join-Path $release 'split.ps1')
Write-Output "Release ready: $release"
