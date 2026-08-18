[CmdletBinding()]
param(
    [string]$ManifestPath = 'tests/UCAD.Core.Tests/Fixtures/AutoCad/manifest.json',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    if ($env:RUNNER_TEMP) {
        $OutputDirectory = Join-Path $env:RUNNER_TEMP 'ucad-autocad-fixtures'
    }
    else {
        $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) 'ucad-autocad-fixtures'
    }
}

$manifestFullPath = [IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $manifestFullPath)) {
    throw "AutoCAD fixture manifest not found: $manifestFullPath"
}

$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
if ($manifest.schema -ne 'ucad-autocad-fixtures-v1') {
    throw "Unsupported AutoCAD fixture manifest schema '$($manifest.schema)'."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Get-GitBlobSha1 {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $prefix = [Text.Encoding]::UTF8.GetBytes("blob $($Bytes.Length)`0")
    $payload = New-Object byte[] ($prefix.Length + $Bytes.Length)
    [Buffer]::BlockCopy($prefix, 0, $payload, 0, $prefix.Length)
    [Buffer]::BlockCopy($Bytes, 0, $payload, $prefix.Length, $Bytes.Length)
    return [Convert]::ToHexString([Security.Cryptography.SHA1]::HashData($payload)).ToLowerInvariant()
}

foreach ($fixture in $manifest.fixtures) {
    $destination = Join-Path $OutputDirectory $fixture.fileName
    $download = $true

    if (Test-Path -LiteralPath $destination) {
        $existing = [IO.File]::ReadAllBytes($destination)
        if ($existing.Length -eq [int64]$fixture.size -and (Get-GitBlobSha1 -Bytes $existing) -eq $fixture.gitBlobSha1) {
            $download = $false
        }
    }

    if ($download) {
        Write-Host "Downloading $($fixture.id) from pinned upstream commit..."
        Invoke-WebRequest -Uri $fixture.url -OutFile $destination -UseBasicParsing
    }

    $bytes = [IO.File]::ReadAllBytes($destination)
    if ($bytes.Length -ne [int64]$fixture.size) {
        throw "Fixture $($fixture.id) length mismatch. Expected $($fixture.size), got $($bytes.Length)."
    }

    $blobSha1 = Get-GitBlobSha1 -Bytes $bytes
    if ($blobSha1 -ne $fixture.gitBlobSha1) {
        throw "Fixture $($fixture.id) Git blob SHA-1 mismatch. Expected $($fixture.gitBlobSha1), got $blobSha1."
    }

    Write-Host "Verified $($fixture.id): $($bytes.Length) bytes / $blobSha1"
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
$env:UCAD_AUTOCAD_FIXTURE_DIR = $resolvedOutput
$env:UCAD_REQUIRE_AUTOCAD_FIXTURES = '1'

if ($env:GITHUB_ENV) {
    "UCAD_AUTOCAD_FIXTURE_DIR=$resolvedOutput" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
    'UCAD_REQUIRE_AUTOCAD_FIXTURES=1' | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
}

Write-Host "AutoCAD fixture corpus ready at $resolvedOutput"
