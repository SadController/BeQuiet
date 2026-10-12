$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (!$dotnetCommand) { throw 'Install the .NET 9 SDK or newer for Windows.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
$helpers = Join-Path $PSScriptRoot 'helpers'
New-Item -ItemType Directory -Path $helpers -Force | Out-Null
& $compiler /nologo /target:winexe ("/out:" + (Join-Path $helpers 'FocusTarget.exe')) (Join-Path $PSScriptRoot 'SilentAudioSession.cs')
if ($LASTEXITCODE -ne 0) { throw 'Silent audio helper compilation failed.' }
Copy-Item -LiteralPath (Join-Path $helpers 'FocusTarget.exe') -Destination (Join-Path $helpers 'BackgroundTest.exe') -Force
Copy-Item -LiteralPath (Join-Path $helpers 'FocusTarget.exe') -Destination (Join-Path $helpers 'NewBackgroundTest.exe') -Force
Copy-Item -LiteralPath (Join-Path $helpers 'FocusTarget.exe') -Destination (Join-Path $helpers 'ProtectedAudioTest.exe') -Force
$output = Join-Path $root 'build\tests'
& $dotnetCommand.Source publish (Join-Path $PSScriptRoot 'BeQuiet.Tests.csproj') -c Release -r win-x64 --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Test project compilation failed.' }
$previousSettingsDirectory = [Environment]::GetEnvironmentVariable('BEQUIET_SETTINGS_DIR', 'Process')
try {
    $env:BEQUIET_SETTINGS_DIR = Join-Path $root 'build\test-settings'
    & (Join-Path $output 'BeQuiet.exe') $root
    if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE" }
} finally {
    [Environment]::SetEnvironmentVariable('BEQUIET_SETTINGS_DIR', $previousSettingsDirectory, 'Process')
}
