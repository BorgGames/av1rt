# Builds the libaom oracle used to check av1rt for bit-exactness.
#
#   ./tools/oracle/build.ps1            pure-C realtime-only libaom + av1rt_oracle (MSVC)
#   ./tools/oracle/build.ps1 -Coverage  same, instrumented for llvm-cov (clang-cl)
#
# Outputs go to .oracle/ in the repository root. Requires Visual Studio 2026 and the libaom
# sources at the commit in libaom.commit, next to this repository (..\aom) or in $env:AOM_ROOT.
param([switch]$Coverage)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\common.ps1"

$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$tools = Get-OracleTools $repo
$aomRoot = $tools.AomRoot
$vs = $tools.VS
$cmake = $tools.CMake
$perl = $tools.Perl

$suffix = if ($Coverage) { '-cov' } else { '' }
$aomBuild = "$repo\.oracle\build$suffix"
$oracleBuild = "$repo\.oracle\oracle$suffix"

$aomArgs = @(
    '-S', $aomRoot, '-B', $aomBuild, '-G', 'Visual Studio 18 2026', '-A', 'x64',
    "-DPERL_EXECUTABLE=$perl", '-DAOM_TARGET_CPU=generic', '-DCONFIG_REALTIME_ONLY=1',
    '-DCONFIG_AV1_HIGHBITDEPTH=0', '-DENABLE_TESTS=0', '-DENABLE_DOCS=0', '-DENABLE_TOOLS=0',
    '-DCONFIG_AV1_DECODER=1', '-DENABLE_EXAMPLES=1')
$oracleArgs = @('-S', $PSScriptRoot, '-B', $oracleBuild, '-G', 'Visual Studio 18 2026', '-A', 'x64',
    "-DAOM_ROOT=$aomRoot", "-DAOM_BUILD_DIR=$aomBuild")

if ($Coverage) {
    $profileLib = (Get-ChildItem "$vs\VC\Tools\Llvm\x64\lib\clang" -Recurse -Filter 'clang_rt.profile-x86_64.lib' |
        Select-Object -First 1).FullName -replace '\\', '/'
    $covFlags = '-fprofile-instr-generate -fcoverage-mapping'
    $aomArgs += @('-T', 'ClangCL', "-DCMAKE_C_FLAGS=$covFlags", "-DCMAKE_CXX_FLAGS=$covFlags",
        "-DCMAKE_EXE_LINKER_FLAGS=`"$profileLib`"")
    $oracleArgs += @('-T', 'ClangCL', "-DEXTRA_LINK_LIBS=$profileLib")
}

& $cmake @aomArgs
if ($LASTEXITCODE) { throw 'libaom configure failed' }
& $cmake --build $aomBuild --config Release --parallel --target aom
if ($LASTEXITCODE) { throw 'libaom build failed' }
& $cmake @oracleArgs
if ($LASTEXITCODE) { throw 'oracle configure failed' }
& $cmake --build $oracleBuild --config Release
if ($LASTEXITCODE) { throw 'oracle build failed' }
Write-Host "oracle: $oracleBuild\Release\av1rt_oracle.exe"
