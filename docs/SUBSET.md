# The realtime subset av1rt implements

av1rt ports exactly what libaom executes when it is driven the way WebRTC (Chrome) drives it
for a single-layer camera-style stream, at speed 11. `tools/oracle/av1rt_oracle.c` is that
configuration in code; it is the reference for bit-exactness.

## Fixed settings

* Profile 0 (Main), 8-bit, 4:2:0 input, no high bitdepth anywhere.
* `g_usage = AOM_USAGE_REALTIME`, one pass, `g_lag_in_frames = 0`, CBR.
* Speed (`AOME_SET_CPUUSED`) **11**, `tune_content = default` (not screen).
  Speed **12** is an av1rt extension (`set_av1rt_speed12_features`): speed 11 plus CDEF
  strengths from the quantizer, half-pel motion vectors, no temporal source filter and zero-MV
  partition variance. It has no libaom reference stream; `Speed12Tests` checks that libaom's
  decoder accepts it.
* `kf_mode = AOM_KF_DISABLED`: key frames only on request (`AOM_EFLAG_FORCE_KF`) and at start.
* Rate control: `rc_min_quantizer 10`, `rc_max_quantizer` configurable (default 56),
  undershoot/overshoot 50 %, buffer initial/optimal/size 600/600/1000 ms,
  `max_intra_bitrate_pct 300`, optional frame dropping (`rc_dropframe_thresh`, 0 or 30) with
  `max_consec_frame_drop_ms_cbr 250` and `postencode_drop_rtc 1`.
* `aq_mode 3` (cyclic refresh), `deltaq_mode 0`, CDEF on, loop restoration off,
  `loopfilter_control` default, no TPL, no order hint, no ref-frame MVs, no global motion,
  no warped motion, no OBMC, no palette, no intra BC, no CfL, no smooth intra, no Paeth,
  no angle delta, no filter intra, no intra edge filter, no dual filter, no masked /
  wedge / diff-weighted / distance-weighted / inter-intra compound, no TX64, no rectangular
  partitions, no quantisation matrices, intra default TX only, trellis disabled,
  `max_reference_frames 3`, coefficient/mode/MV cost update frequency 3, noise sensitivity 0.
* Superblock size: WebRTC rule (64x64 when ≥4 threads and 960x540 ≤ area < 1920x1080,
  otherwise dynamic, which gives 128x128 above 720p and 64x64 otherwise).
* Threads: `g_threads` 1..N with row multithreading and automatic tiles
  (`AV1E_SET_AUTO_TILES`: about one tile per thread, at least 128x128 per tile).
* Timebase 1/90000.

## Not in the subset

Decoding, two-pass, lookahead/ARF/GF boosting beyond what one-pass CBR RT does, SVC
(spatial/temporal layers), screen content tools, resize/superres, film grain, high bitdepth,
4:4:4/4:2:2, monochrome input, external rate control, speeds other than 11.

## Oracle

```
./tools/oracle/build.ps1            # .oracle/oracle/Release/av1rt_oracle.exe
./tools/oracle/build.ps1 -Coverage  # .oracle/oracle-cov/... (llvm-cov instrumented)
.oracle/oracle/Release/av1rt_oracle.exe in.y4m out.ivf --bitrate 35000 --threads 8 --recon recon.yuv
```

Test clips live in `.testdata/` (not committed): `dota4k.y4m` (3840x2160 game capture),
`dota1080.y4m`, `dota1366x766.y4m`, `dota352.y4m`, `desk4k.y4m` (desktop), `synth1080.y4m`.
