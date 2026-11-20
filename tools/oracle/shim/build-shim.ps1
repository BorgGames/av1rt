# Builds tools/oracle/shim/shim_<Name>.c into aomshim_<Name>.dll and copies it to
# <this checkout>/.oracle/shims/, where the test project picks it up.
# Works from git worktrees too: libaom and its pure-C build are taken from the main checkout
# (the oracle build is not committed), i.e. <main>/.oracle/build and <main>/../aom.
param([Parameter(Mandatory)][string]$Name)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$commonGit = (& git -C $repo rev-parse --path-format=absolute --git-common-dir).Trim()
$mainRepo = Split-Path $commonGit -Parent
$aomRoot = (Resolve-Path "$mainRepo\..\aom").Path
$aomBuild = "$mainRepo\.oracle\build"
if (-not (Test-Path "$aomBuild\Release\aom.lib")) { throw "pure-C libaom not built: run tools/oracle/build.ps1 in $mainRepo" }
$cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$build = "$repo\.oracle\shim-$Name"
& $cmake -S $PSScriptRoot -B $build -G 'Visual Studio 18 2026' -A x64 "-DAOM_ROOT=$aomRoot" "-DAOM_BUILD_DIR=$aomBuild" "-DSHIM=$Name" | Out-Null
if ($LASTEXITCODE) { throw 'configure failed' }
& $cmake --build $build --config Release
if ($LASTEXITCODE) { throw 'build failed' }
New-Item -ItemType Directory -Force "$repo\.oracle\shims" | Out-Null
Copy-Item "$build\Release\aomshim_$Name.dll" "$repo\.oracle\shims\" -Force
Write-Host "built $repo\.oracle\shims\aomshim_$Name.dll"
