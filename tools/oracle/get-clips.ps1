# Makes the public end-to-end clips in .testdata/ from Big Buck Bunny (4K60, (c) Blender Foundation,
# CC BY 3.0, https://peach.blender.org): one-second excerpts are cut over HTTP without downloading
# the whole film (.testdata/bbb-<seconds>.mp4, kept for reuse), then decoded and scaled to y4m.
# Requires ffmpeg on PATH. Existing files are kept.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$out = "$repo\.testdata"
New-Item -ItemType Directory -Force $out | Out-Null
$source = 'https://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_2160p_60fps_normal.mp4'

function Get-Excerpt([int]$seconds) {
    $f = "$out\bbb-$seconds.mp4"
    if (-not (Test-Path $f)) {
        & ffmpeg -nostdin -loglevel error -y -ss $seconds -i $source -t 1.0 -c copy -an "$f.tmp.mp4"
        if ($LASTEXITCODE) { throw "cannot cut $seconds s from $source" }
        Move-Item -Force "$f.tmp.mp4" $f
    }
    $f
}

# name, excerpt (s), size (or $null for 4K), frames
$clips = @(
    @('bbb4k', 470, $null, 60),         # meadow: dense grass, fine detail
    @('bbb1080', 470, '1920:1080', 60),
    @('bbb1366x766', 470, '1366:766', 30),
    @('bbb352', 470, '352:288', 30),
    @('bbbtext4k', 560, $null, 40)      # end credits: text over flat background
)
foreach ($c in $clips) {
    $name, $seconds, $size, $frames = $c
    $y4m = "$out\$name.y4m"
    if (Test-Path $y4m) { continue }
    $src = Get-Excerpt $seconds
    $vf = if ($size) { @('-vf', "scale=${size}:flags=bicubic") } else { @() }
    & ffmpeg -nostdin -loglevel error -y -i $src @vf -frames:v $frames -pix_fmt yuv420p -f yuv4mpegpipe "$y4m.tmp"
    if ($LASTEXITCODE) { throw "cannot make $y4m" }
    Move-Item -Force "$y4m.tmp" $y4m
    Write-Host "made $y4m"
}
