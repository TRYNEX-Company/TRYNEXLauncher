[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+-(preview|beta|rc)\.\d+$|^\d+\.\d+\.\d+$')]
    [string] $Version,

    [ValidateSet('preview', 'stable')]
    [string] $Channel = 'preview',

    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64',

    [string] $OutputRoot = 'D:\TRYNEX-RELEASES',

    [Parameter(Mandatory = $true)]
    [string] $PrivateKeyPath,

    [Parameter(Mandatory = $true)]
    [string] $PublicKeyPath,

    [switch] $Mandatory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$launcherProject = Join-Path $repositoryRoot 'src\Trynex.Launcher\Trynex.Launcher.csproj'
$solutionPath = Join-Path $repositoryRoot 'TRYNEX.slnx'
$releaseToolProject = Join-Path $repositoryRoot 'tools\Trynex.ReleaseTool\Trynex.ReleaseTool.csproj'
$resolvedPrivateKeyPath = [System.IO.Path]::GetFullPath($PrivateKeyPath)
$resolvedPublicKeyPath = [System.IO.Path]::GetFullPath($PublicKeyPath)
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$releaseDirectory = Join-Path $resolvedOutputRoot (Join-Path 'launcher' (Join-Path $Channel $Version))
$packageName = "trynex-launcher-$Runtime.zip"
$objectPath = "launcher/$Channel/$Version/$packageName"
$temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) ("trynex-release-" + [Guid]::NewGuid().ToString('N'))))
$publishDirectory = Join-Path $temporaryRoot 'publish'
$temporaryReleaseDirectory = Join-Path $temporaryRoot 'release'
$packagePath = Join-Path $temporaryReleaseDirectory $packageName
$manifestPath = Join-Path $temporaryReleaseDirectory 'manifest.json'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Executable $($Arguments -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath $launcherProject -PathType Leaf)) {
    throw "Launcher project was not found: $launcherProject"
}

if (-not (Test-Path -LiteralPath $resolvedPrivateKeyPath -PathType Leaf)) {
    throw "Private signing key was not found: $resolvedPrivateKeyPath"
}

if (-not (Test-Path -LiteralPath $resolvedPublicKeyPath -PathType Leaf)) {
    throw "Public verification key was not found: $resolvedPublicKeyPath"
}

$projectText = Get-Content -LiteralPath $launcherProject -Raw -Encoding UTF8
if ($projectText -notmatch "<Version>$([Regex]::Escape($Version))</Version>") {
    throw "Trynex.Launcher.csproj does not contain version $Version. Update the project version first."
}

if (Test-Path -LiteralPath $releaseDirectory) {
    throw "Refusing to overwrite an existing release directory: $releaseDirectory"
}

New-Item -ItemType Directory -Path $publishDirectory | Out-Null
New-Item -ItemType Directory -Path $temporaryReleaseDirectory | Out-Null

try {
    Write-Host "[1/5] Running all automated tests..."
    Invoke-CheckedCommand -Executable 'dotnet' -Arguments @(
        'test', $solutionPath,
        '--configuration', 'Release',
        '--nologo',
        '-p:NuGetAudit=false'
    )

    Write-Host "[2/5] Publishing the self-contained Windows launcher..."
    Invoke-CheckedCommand -Executable 'dotnet' -Arguments @(
        'publish', $launcherProject,
        '--configuration', 'Release',
        '--runtime', $Runtime,
        '--self-contained', 'true',
        '--output', $publishDirectory,
        '--nologo',
        '-p:NuGetAudit=false'
    )

    Write-Host "[3/5] Creating the update package..."
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $packagePath -CompressionLevel Optimal

    Write-Host "[4/5] Signing the release manifest..."
    Invoke-CheckedCommand -Executable 'dotnet' -Arguments @(
        'run', '--project', $releaseToolProject,
        '--configuration', 'Release',
        '--no-restore', '--',
        'manifest',
        '--package', $packagePath,
        '--version', $Version,
        '--channel', $Channel,
        '--object-path', $objectPath,
        '--private-key', $resolvedPrivateKeyPath,
        '--output', $manifestPath,
        '--mandatory', $Mandatory.IsPresent.ToString().ToLowerInvariant()
    )

    Write-Host "[5/5] Verifying the signature, size and SHA-256..."
    Invoke-CheckedCommand -Executable 'dotnet' -Arguments @(
        'run', '--project', $releaseToolProject,
        '--configuration', 'Release',
        '--no-restore', '--',
        'verify',
        '--manifest', $manifestPath,
        '--public-key', $resolvedPublicKeyPath,
        '--package', $packagePath
    )

    $releaseParent = Split-Path -Path $releaseDirectory -Parent
    New-Item -ItemType Directory -Path $releaseParent -Force | Out-Null
    Move-Item -LiteralPath $temporaryReleaseDirectory -Destination $releaseDirectory

    Write-Host ''
    Write-Host "Release $Version is ready: $releaseDirectory"
    Write-Host "Upload ZIP first to: $objectPath"
    Write-Host "Upload manifest last to: launcher/$Channel/manifest.json"
}
finally {
    $systemTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ((Test-Path -LiteralPath $temporaryRoot) -and $temporaryRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
