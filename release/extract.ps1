$ErrorActionPreference = 'Stop'
$archiveName = 'BeQuiet-1.2.0-win-x64.zip'
$archive = Join-Path $PSScriptRoot $archiveName
$destination = Join-Path $PSScriptRoot 'BeQuiet-1.2.0-win-x64'
$checksums = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.txt')) {
    if ($line -match '^([a-fA-F0-9]{64})\s+(.+)$') { $checksums[$Matches[2]] = $Matches[1] }
}
function Assert-Checksum([string]$FilePath, [string]$Name) {
    if (!$checksums.ContainsKey($Name)) { throw "Missing checksum: $Name" }
    if ((Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash -ne $checksums[$Name]) { throw "Checksum mismatch: $Name" }
}
$parts = @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Name -match '^BeQuiet-1\.2\.0-win-x64\.zip\.[0-9]{3}$' } | Sort-Object Name)
if ($parts.Count -eq 0) { throw 'No release archive parts found. Download all numbered ZIP parts into this folder.' }
for ($partIndex = 0; $partIndex -lt $parts.Count; $partIndex++) {
    $expectedName = $archiveName + '.' + ($partIndex + 1).ToString('000')
    if ($parts[$partIndex].Name -ne $expectedName) { throw "Missing archive part: $expectedName" }
    Assert-Checksum $parts[$partIndex].FullName $expectedName
}
$outputStream = [IO.File]::Create($archive)
try {
    foreach ($part in $parts) {
        $inputStream = [IO.File]::OpenRead($part.FullName)
        try { $inputStream.CopyTo($outputStream) } finally { $inputStream.Dispose() }
    }
} finally { $outputStream.Dispose() }
Assert-Checksum $archive $archiveName
Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
Assert-Checksum (Join-Path $destination 'BeQuiet.exe') 'BeQuiet.exe'
Write-Output "Release ready: $destination"
