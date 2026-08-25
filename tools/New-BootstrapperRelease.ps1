[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+-(preview|beta|rc)\.\d+$|^\d+\.\d+\.\d+$')]
    [string] $Version,

    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64',

    [string] $OutputRoot = 'D:\TRYNEX-RELEASES'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bootstrapperProject = Join-Path $repositoryRoot 'src\Trynex.Bootstrapper\Trynex.Bootstrapper.csproj'
$trustConfiguration = Join-Path $repositoryRoot 'src\Trynex.Bootstrapper\UpdateTrustConfiguration.cs'
$solutionPath = Join-Path $repositoryRoot 'TRYNEX.slnx'
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$releaseDirectory = Join-Path $resolvedOutputRoot (Join-Path 'bootstrapper' $Version)
$temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) ("trynex-bootstrapper-" + [Guid]::NewGuid().ToString('N'))))
$publishDirectory = Join-Path $temporaryRoot 'publish'

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

if (-not (Test-Path -LiteralPath $bootstrapperProject -PathType Leaf)) {
    throw "Bootstrapper project was not found: $bootstrapperProject"
}

$projectText = Get-Content -LiteralPath $bootstrapperProject -Raw -Encoding UTF8
if ($projectText -notmatch "<Version>$([Regex]::Escape($Version))</Version>") {
    throw "Trynex.Bootstrapper.csproj does not contain version $Version. Update the project version first."
}

$trustText = Get-Content -LiteralPath $trustConfiguration -Raw -Encoding UTF8
if ($trustText -notmatch "BootstrapperVersion = `"$([Regex]::Escape($Version))`"") {
    throw "UpdateTrustConfiguration.cs does not contain bootstrapper version $Version."
}

if (Test-Path -LiteralPath $releaseDirectory) {
    throw "Refusing to overwrite an existing release directory: $releaseDirectory"
}

New-Item -ItemType Directory -Path $publishDirectory | Out-Null

try {
    Write-Host "[1/3] Running all automated tests..."
    Invoke-CheckedCommand -Executable 'dotnet' -Arguments @(
        'test', $solutionPath,
        '--configuration', 'Release',
        '--nologo',
        '-p:NuGetAudit=false'
    )

    Write-Host "[2/3] Publishing one self-contained TRYNEX.exe..."
    Invoke-CheckedCommand -Executable 'dotnet' -Arguments @(
        'publish', $bootstrapperProject,
        '--configuration', 'Release',
        '--runtime', $Runtime,
        '--self-contained', 'true',
        '--output', $publishDirectory,
        '--nologo',
        '-p:NuGetAudit=false',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=None',
        '-p:DebugSymbols=false'
    )

    $publishedExecutable = Join-Path $publishDirectory 'TRYNEX.exe'
    if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
        throw "Published TRYNEX.exe was not found: $publishedExecutable"
    }

    Write-Host "[3/3] Calculating SHA-256 and preparing release directory..."
    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    $releaseExecutable = Join-Path $releaseDirectory 'TRYNEX.exe'
    Copy-Item -LiteralPath $publishedExecutable -Destination $releaseExecutable
    $hash = (Get-FileHash -LiteralPath $releaseExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath (Join-Path $releaseDirectory 'TRYNEX.exe.sha256') -Value "$hash *TRYNEX.exe" -Encoding ASCII

    Write-Host ''
    Write-Host "Bootstrapper $Version is ready: $releaseExecutable"
    Write-Host "SHA-256: $hash"
}
catch {
    if (Test-Path -LiteralPath $releaseDirectory) {
        Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
    }

    throw
}
finally {
    $systemTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ((Test-Path -LiteralPath $temporaryRoot) -and $temporaryRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
