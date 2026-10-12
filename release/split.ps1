$ErrorActionPreference = 'Stop'
$archiveName = 'BeQuiet-1.2.0-win-x64.zip'
$archive = Join-Path $PSScriptRoot $archiveName
$partSize = 4000000
$buffer = New-Object byte[] 1048576
$inputStream = [IO.File]::OpenRead($archive)
try {
    $partIndex = 0
    while ($inputStream.Position -lt $inputStream.Length) {
        $partIndex++
        $partName = $archiveName + '.' + $partIndex.ToString('000')
        $partPath = Join-Path $PSScriptRoot $partName
        $outputStream = [IO.File]::Create($partPath)
        try {
            $remaining = $partSize
            while ($remaining -gt 0 -and $inputStream.Position -lt $inputStream.Length) {
                $read = $inputStream.Read($buffer, 0, [Math]::Min($buffer.Length, $remaining))
                if ($read -eq 0) { throw 'Unexpected end of archive.' }
                $outputStream.Write($buffer, 0, $read)
                $remaining -= $read
            }
        } finally { $outputStream.Dispose() }
        $hash = (Get-FileHash -LiteralPath $partPath -Algorithm SHA256).Hash.ToLowerInvariant()
        Add-Content -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.txt') -Value "$hash  $partName" -Encoding ascii
    }
} finally { $inputStream.Dispose() }
# Remove obsolete numbered parts when a newer build needs fewer parts.
Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Name -match '^BeQuiet-1\.2\.0-win-x64\.zip\.([0-9]{3})$' -and [int]$Matches[1] -gt $partIndex } | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
