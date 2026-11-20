# Work packages

Line counts are lines that executed in the subset (see `.oracle/cov/summary/files.txt`).

## Phase 1 — shared headers (same tree, in parallel)

| id | scope |
|---|---|
| p1a | `av1/common/*.h` (AV1_COMMON, MACROBLOCKD, MB_MODE_INFO, FRAME_CONTEXT, enums, tables, inline helpers) |
| p1b1 | `av1/encoder` top-level headers (encoder.h, ratectrl.h, svc_layercontext.h, aq_cyclicrefresh.h, encoder_utils.h, encoder_alloc.h, lookahead.h, ...) |
| p1b2 | `av1/encoder` block-level headers (block.h, speed_features.h, rd.h, mcomp.h, nonrd_opt.h, encodetxb.h, context_tree.h, ...) |
| p1c | `aom/*.h`, `aom_dsp/*.h`, `aom_mem`, `aom_ports`, `aom_scale/yv12config.*`, `aom_util`, entropy coder `.c` files |

## Phase 2 — leaf modules (git worktrees, in parallel)

| id | C files | ~lines |
|---|---|---|
| e1 entropy | av1/common/entropy.c, entropymode.c, entropymv.c, token_cdfs.h (tables), scan.c, quant_common.c, seg_common.c, txb_common.c; av1/encoder/cost.c | 350 + tables |
| e2 transform | av1/common/av1_inv_txfm1d.c, av1_inv_txfm2d.c, idct.c, av1_txfm.c; av1/encoder/av1_fwd_txfm1d.c, av1_fwd_txfm2d.c, hybrid_fwd_txfm.c, av1_quantize.c, encodemb.c; aom_dsp/quantize.c | 2900 |
| e3 prediction | av1/common/reconinter.c (+template .inc), convolve.c, reconintra.c, scale.c, cfl.c, blockd.c, mvref_common.c, pred_common.c; aom_dsp/intrapred.c, aom_convolve.c; av1/encoder/reconinter_enc.c | 1300 |
| e4 filters | av1/common/av1_loopfilter.c, cdef.c, cdef_block.c, thread_common.c (single-thread LF/CDEF drivers); aom_dsp/loopfilter.c; av1/encoder/pickcdef.c, picklpf.c | 2400 |
| e5 dsp+buffers | aom_dsp/sad.c, variance.c, avg.c, sse.c, subtract.c; aom_scale/generic/yv12extend.c; av1/common/alloccommon.c, tile_common.c, resize.c, timing.c; av1/encoder/extend.c, lookahead.c, av1_noise_estimate.c, segmentation.c | 1200 |
| e6 motion | av1/encoder/mcomp.c, mv_prec.c, encodemv.c | 1200 |
| e7 ratecontrol | av1/encoder/ratectrl.c, aq_cyclicrefresh.c | 1600 |
| e8 sf+rd | av1/encoder/speed_features.c, rd.c | 1700 |

## Phase 3 — mid/top level (git worktrees, in parallel, after phase 2 is merged)

| id | C files | ~lines |
|---|---|---|
| f1 bitstream | av1/encoder/bitstream.c, encodetxb.c, tokenize.c | 2100 |
| f2 pickmode | av1/encoder/nonrd_pickmode.c, nonrd_opt.c | 2050 |
| f3 partition | av1/encoder/var_based_part.c, partition_search.c, context_tree.c | 2050 |
| f4 frame | av1/encoder/encodeframe.c, encodeframe_utils.c, encoder_utils.c, ethread.c (tile/row drivers) | 2500 |
| g1 top | av1/encoder/encoder.c, encode_strategy.c, superres_scale.c, av1_ext_ratectrl.c, external_partition.c; av1/av1_cx_iface.c (config + encode flow); aom/src/aom_encoder.c, aom_codec.c | 3200 |

## Phase 4 — integration (me)

End-to-end bit-exactness against the oracle, public API (`src/Av1rt/Api`), CLI, tile/row
parallelism, SIMD, faster-than-libaom presets for 4K60 on the i7-8700K target,
SIPSorceryMedia.AV1 adapter.
