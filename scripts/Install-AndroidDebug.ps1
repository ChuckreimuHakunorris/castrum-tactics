[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Apk,

    [string] $Serial,

    [string] $AndroidSdkRoot = $(if ($env:ANDROID_HOME) { $env:ANDROID_HOME }
        elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT }
        else { Join-Path $env:LOCALAPPDATA 'Android/Sdk' }),

    [switch] $RemoveLegacy8C
)

$ErrorActionPreference = 'Stop'
$package = 'com.castrumtactics.prototype'
$legacyPackage = 'com.castrumtactics.milestone8c'
$adb = Join-Path $AndroidSdkRoot 'platform-tools/adb.exe'
$buildTools = Join-Path $AndroidSdkRoot 'build-tools'
if (!(Test-Path -LiteralPath $adb -PathType Leaf) -or !(Test-Path -LiteralPath $buildTools -PathType Container)) {
    throw "ADB and Android Build Tools are required. Check the SDK path '$AndroidSdkRoot'."
}
$aapt = Get-ChildItem -LiteralPath $buildTools -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
    Sort-Object { [version] $_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'aapt.exe' } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (!$aapt) { throw "aapt.exe was not found in '$buildTools'." }

$apkPath = (Get-Item -LiteralPath $Apk).FullName
$badging = & $aapt dump badging $apkPath
if ($LASTEXITCODE -ne 0) { throw "Could not read the package ID from '$apkPath'." }
$packageLine = $badging | Where-Object { $_ -match '^package: name=' } | Select-Object -First 1
if ($packageLine -notmatch "^package: name='([^']+)'") { throw 'The APK package ID could not be determined.' }
$apkPackage = $Matches[1]
if ($apkPackage -cne $package) {
    throw "APK has package ID '$apkPackage'. Export with the Android preset using '$package'; a different ID installs as a separate app."
}

$device = @()
if ($Serial) { $device = @('-s', $Serial) }

function Get-GamePackages {
    $installed = & $adb @device shell pm list packages --user 0 'com.castrumtactics.'
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the phone. Connect ADB and use -Serial if multiple devices are connected.' }
    return $installed
}

$installed = Get-GamePackages
$hasLegacy = $installed -ccontains "package:$legacyPackage"
if ($hasLegacy -and !$RemoveLegacy8C) {
    throw "The isolated 8C package is installed. Run again with -RemoveLegacy8C to remove it after the standard APK installs. Android will be asked to keep its data."
}

& $adb @device install --user 0 -r $apkPath
if ($LASTEXITCODE -ne 0) {
    throw 'APK installation failed. Check the ADB message above and use the same signing key as the installed build.'
}

if ($hasLegacy) {
    # Keep legacy data separate and touch the older app only after its replacement installs.
    & $adb @device shell pm uninstall -k --user 0 $legacyPackage
    if ($LASTEXITCODE -ne 0) { throw "The standard APK installed, but removing '$legacyPackage' failed." }
}

$installed = Get-GamePackages
if ($installed -cnotcontains "package:$package" -or $installed -ccontains "package:$legacyPackage") {
    throw 'Android did not report the expected standard installation for user 0.'
}
Write-Output "Verified '$package' on Android user 0."
