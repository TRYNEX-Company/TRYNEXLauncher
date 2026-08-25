[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePath,

    [switch] $NoDesktopShortcut
)

$ErrorActionPreference = 'Stop'
$resolvedSource = [System.IO.Path]::GetFullPath($SourcePath)
if (-not (Test-Path -LiteralPath $resolvedSource -PathType Leaf)) {
    throw "TRYNEX.exe was not found: $resolvedSource"
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$installDirectory = Join-Path $localAppData 'TRYNEX\App'
$installedExecutable = Join-Path $installDirectory 'TRYNEX.exe'
$temporaryExecutable = Join-Path $installDirectory 'TRYNEX.exe.new'

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -LiteralPath $resolvedSource -Destination $temporaryExecutable -Force

$sourceHash = (Get-FileHash -LiteralPath $resolvedSource -Algorithm SHA256).Hash
$copiedHash = (Get-FileHash -LiteralPath $temporaryExecutable -Algorithm SHA256).Hash
if (-not [string]::Equals($sourceHash, $copiedHash, [StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -LiteralPath $temporaryExecutable -Force
    throw 'The installed copy does not match the release SHA-256.'
}

Move-Item -LiteralPath $temporaryExecutable -Destination $installedExecutable -Force

$shell = New-Object -ComObject WScript.Shell

function New-TrynexShortcut {
    param([Parameter(Mandatory = $true)][string] $ShortcutPath)

    $shortcutDirectory = Split-Path -Path $ShortcutPath -Parent
    New-Item -ItemType Directory -Path $shortcutDirectory -Force | Out-Null
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $installedExecutable
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$installedExecutable,0"
    $shortcut.Description = 'TRYNEX Launcher'
    $shortcut.Save()
}

$startMenu = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
New-TrynexShortcut -ShortcutPath (Join-Path $startMenu 'TRYNEX.lnk')

if (-not $NoDesktopShortcut) {
    $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    New-TrynexShortcut -ShortcutPath (Join-Path $desktop 'TRYNEX.lnk')
}

Write-Host "TRYNEX installed: $installedExecutable"
Write-Host "SHA-256: $($sourceHash.ToLowerInvariant())"
