# Builds tools/oracle/shim/shim_<Name>.c into aomshim_<Name>.dll and copies it to
# <this checkout>/.oracle/shims/, where the test project picks it up. -All builds every shim.
# Works from git worktrees too: libaom and its pure-C build are taken from the main checkout
# (the oracle build is not committed), i.e. <main>/.oracle/build and <main>/../aom (or $env:AOM_ROOT).
param([string]$Name, [switch]$All)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\common.ps1"
if (-not $All -and -not $Name) { throw 'pass -Name <shim> or -All' }
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$commonGit = (& git -C $repo rev-parse --path-format=absolute --git-common-dir).Trim()
$mainRepo = Split-Path $commonGit -Parent
$tools = Get-OracleTools $mainRepo
$aomBuild = "$mainRepo\.oracle\build"
if (-not (Test-Path "$aomBuild\Release\aom.lib")) { throw "pure-C libaom not built: run tools/oracle/build.ps1 in $mainRepo" }
$names = if ($All) { Get-ChildItem "$PSScriptRoot\shim_*.c" | ForEach-Object { $_.BaseName.Substring(5) } } else { @($Name) }
New-Item -ItemType Directory -Force "$repo\.oracle\shims" | Out-Null
foreach ($n in $names) {
    $build = "$repo\.oracle\shim-$n"
    & $tools.CMake -S $PSScriptRoot -B $build -G 'Visual Studio 18 2026' -A x64 "-DAOM_ROOT=$($tools.AomRoot)" "-DAOM_BUILD_DIR=$aomBuild" "-DSHIM=$n" | Out-Null
    if ($LASTEXITCODE) { throw "configure of $n failed" }
    & $tools.CMake --build $build --config Release
    if ($LASTEXITCODE) { throw "build of $n failed" }
    Copy-Item "$build\Release\aomshim_$n.dll" "$repo\.oracle\shims\" -Force
    Write-Host "built $repo\.oracle\shims\aomshim_$n.dll"
}
