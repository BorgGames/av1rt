# Dot-sourced by build.ps1 and shim/build-shim.ps1: finds libaom, Visual Studio 2026, CMake and Perl.
#
#   $env:AOM_ROOT   libaom sources; default ..\aom next to the main checkout. They should be at the
#                   commit in libaom.commit, which CI fetches.
#
# Visual Studio 2026 (any edition, with the C++ x64 tools) is located with vswhere.

function Get-OracleTools([string]$mainRepo) {
    $aomRoot = if ($env:AOM_ROOT) { $env:AOM_ROOT } else { "$mainRepo\..\aom" }
    $aomRoot = (Resolve-Path $aomRoot).Path

    $pinned = (Get-Content "$PSScriptRoot\libaom.commit" -TotalCount 1).Trim()
    if (Test-Path "$aomRoot\.git") {
        $head = (& git -C $aomRoot rev-parse HEAD).Trim()
        if ($head -ne $pinned) { Write-Warning "libaom at $aomRoot is $head, the port matches $pinned (tools/oracle/libaom.commit)" }
    }

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs = & $vswhere -latest -products * -version '[18.0,19.0)' `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $vs) { throw 'Visual Studio 2026 with the C++ x64 tools not found' }

    $cmake = "$vs\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
    if (-not (Test-Path $cmake)) { $cmake = (Get-Command cmake -ErrorAction Stop).Source }

    $perl = 'C:/Program Files/Git/usr/bin/perl.exe'
    if (-not (Test-Path $perl)) { $perl = (Get-Command perl -ErrorAction Stop).Source -replace '\\', '/' }

    [pscustomobject]@{ AomRoot = $aomRoot; VS = $vs; CMake = $cmake; Perl = $perl }
}
