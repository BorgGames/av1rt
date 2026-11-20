/*
 * av1rt_oracle: drives libaom the way WebRTC drives it for a single-layer
 * (L1T1) camera stream, so the C# port (av1rt) can be compared against it
 * byte for byte. Writes an IVF file and, optionally, the encoder's
 * reconstruction of every frame as raw I420.
 *
 * Settings mirror modules/video_coding/codecs/av1/libaom_av1_encoder.cc
 * (InitEncode) with these deliberate test knobs:
 *   --speed        AOME_SET_CPUUSED (WebRTC default for >= 720p camera is 10;
 *                  av1rt targets 11)
 *   --threads      g_threads (WebRTC picks 1/2/4/8 from resolution and cores)
 *   --drop         rc_dropframe_thresh (WebRTC: 30 when frame dropping is on)
 *   --kf-interval  force a key frame every N frames (WebRTC: on request only)
 *
 * Usage: av1rt_oracle in.y4m out.ivf [--speed 11] [--bitrate 35000]
 *          [--threads 8] [--limit N] [--recon recon.yuv] [--qmax 56]
 *          [--drop 0] [--kf-interval 0] [--fps 60] [--sb 0|64|128]
 *          [--tile-cols log2 --tile-rows log2] [--aq 3] [--cdef 1]
 */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "aom/aom_encoder.h"
#include "aom/aomcx.h"

static void die(const char *msg) {
  fprintf(stderr, "error: %s\n", msg);
  exit(1);
}

static void die_codec(aom_codec_ctx_t *ctx, const char *msg) {
  const char *detail = aom_codec_error_detail(ctx);
  fprintf(stderr, "error: %s: %s\n", msg, aom_codec_error(ctx));
  if (detail) fprintf(stderr, "  %s\n", detail);
  exit(1);
}

#define CTRL(id, val)                                           \
  do {                                                          \
    if (aom_codec_control(&codec, id, val)) die_codec(&codec, #id); \
  } while (0)

static void put_le16(uint8_t *p, unsigned v) {
  p[0] = (uint8_t)v;
  p[1] = (uint8_t)(v >> 8);
}
static void put_le32(uint8_t *p, uint32_t v) {
  p[0] = (uint8_t)v;
  p[1] = (uint8_t)(v >> 8);
  p[2] = (uint8_t)(v >> 16);
  p[3] = (uint8_t)(v >> 24);
}

static void write_ivf_header(FILE *f, int w, int h, int fps, int frames) {
  uint8_t hdr[32] = { 'D', 'K', 'I', 'F' };
  put_le16(hdr + 4, 0);
  put_le16(hdr + 6, 32);
  memcpy(hdr + 8, "AV01", 4);
  put_le16(hdr + 12, (unsigned)w);
  put_le16(hdr + 14, (unsigned)h);
  put_le32(hdr + 16, (uint32_t)fps);
  put_le32(hdr + 20, 1);
  put_le32(hdr + 24, (uint32_t)frames);
  put_le32(hdr + 28, 0);
  fwrite(hdr, 1, 32, f);
}

static void write_ivf_frame(FILE *f, const void *data, size_t size,
                            int64_t pts) {
  uint8_t hdr[12];
  put_le32(hdr, (uint32_t)size);
  put_le32(hdr + 4, (uint32_t)(pts & 0xFFFFFFFF));
  put_le32(hdr + 8, (uint32_t)(pts >> 32));
  fwrite(hdr, 1, 12, f);
  fwrite(data, 1, size, f);
}

/* Minimal 8-bit 4:2:0 Y4M reader. */
typedef struct {
  FILE *f;
  int w, h;
} Y4m;

static void y4m_open(Y4m *y, const char *path) {
  char line[512];
  y->f = fopen(path, "rb");
  if (!y->f) die("cannot open input");
  if (!fgets(line, sizeof(line), y->f) || strncmp(line, "YUV4MPEG2", 9))
    die("not a y4m file");
  y->w = y->h = 0;
  for (char *tok = strtok(line, " \n"); tok; tok = strtok(NULL, " \n")) {
    if (tok[0] == 'W') y->w = atoi(tok + 1);
    if (tok[0] == 'H') y->h = atoi(tok + 1);
    if (tok[0] == 'C' && strncmp(tok, "C420", 4) != 0)
      die("only 8-bit 4:2:0 y4m is supported");
  }
  if (y->w <= 0 || y->h <= 0) die("bad y4m size");
}

static int y4m_read(Y4m *y, aom_image_t *img) {
  char line[256];
  if (!fgets(line, sizeof(line), y->f)) return 0;
  if (strncmp(line, "FRAME", 5)) die("bad y4m frame marker");
  for (int plane = 0; plane < 3; ++plane) {
    const int pw = plane ? (y->w + 1) >> 1 : y->w;
    const int ph = plane ? (y->h + 1) >> 1 : y->h;
    for (int r = 0; r < ph; ++r) {
      if (fread(img->planes[plane] + (size_t)r * img->stride[plane], 1,
                (size_t)pw, y->f) != (size_t)pw)
        return 0;
    }
  }
  return 1;
}

static void write_image(FILE *f, const aom_image_t *img) {
  for (int plane = 0; plane < 3; ++plane) {
    const int pw = plane ? (int)((img->d_w + 1) >> 1) : (int)img->d_w;
    const int ph = plane ? (int)((img->d_h + 1) >> 1) : (int)img->d_h;
    for (int r = 0; r < ph; ++r)
      fwrite(img->planes[plane] + (size_t)r * img->stride[plane], 1,
             (size_t)pw, f);
  }
}

static int webrtc_threads(int w, int h, int cores) {
  if (w * h > 1280 * 720 && cores > 8) return 8;
  if (w * h >= 640 * 360 && cores > 4) return 4;
  if (w * h >= 320 * 180 && cores > 2) return 2;
  return 1;
}

int main(int argc, char **argv) {
  if (argc < 3) {
    fprintf(stderr,
            "usage: av1rt_oracle in.y4m out.ivf [--speed 11] [--bitrate kbps]"
            " [--threads N] [--limit N] [--recon f.yuv] [--qmax 56]"
            " [--drop 0] [--kf-interval 0] [--fps 60] [--sb 0|64|128]"
            " [--tile-cols L] [--tile-rows L] [--aq 3] [--cdef 1]\n");
    return 2;
  }
  const char *in_path = argv[1], *out_path = argv[2], *recon_path = NULL;
  int speed = 11, bitrate = 35000, threads = -1, limit = 0, qmax = 56;
  int drop = 0, kf_interval = 0, fps = 60, sb = 0, tile_cols = -1,
      tile_rows = -1, aq = 3, cdef = 1;
  for (int i = 3; i + 1 < argc; i += 2) {
    const char *k = argv[i];
    const char *v = argv[i + 1];
    if (!strcmp(k, "--speed")) speed = atoi(v);
    else if (!strcmp(k, "--bitrate")) bitrate = atoi(v);
    else if (!strcmp(k, "--threads")) threads = atoi(v);
    else if (!strcmp(k, "--limit")) limit = atoi(v);
    else if (!strcmp(k, "--recon")) recon_path = v;
    else if (!strcmp(k, "--qmax")) qmax = atoi(v);
    else if (!strcmp(k, "--drop")) drop = atoi(v);
    else if (!strcmp(k, "--kf-interval")) kf_interval = atoi(v);
    else if (!strcmp(k, "--fps")) fps = atoi(v);
    else if (!strcmp(k, "--sb")) sb = atoi(v);
    else if (!strcmp(k, "--tile-cols")) tile_cols = atoi(v);
    else if (!strcmp(k, "--tile-rows")) tile_rows = atoi(v);
    else if (!strcmp(k, "--aq")) aq = atoi(v);
    else if (!strcmp(k, "--cdef")) cdef = atoi(v);
    else die("unknown option");
  }

  Y4m y4m;
  y4m_open(&y4m, in_path);
  if (threads < 0) threads = webrtc_threads(y4m.w, y4m.h, 16);

  aom_codec_iface_t *iface = aom_codec_av1_cx();
  aom_codec_enc_cfg_t cfg;
  if (aom_codec_enc_config_default(iface, &cfg, AOM_USAGE_REALTIME))
    die("config_default");
  cfg.g_w = (unsigned)y4m.w;
  cfg.g_h = (unsigned)y4m.h;
  cfg.g_threads = (unsigned)threads;
  cfg.g_timebase.num = 1;
  cfg.g_timebase.den = 90000;
  cfg.rc_target_bitrate = (unsigned)bitrate;
  cfg.rc_dropframe_thresh = (unsigned)drop;
  cfg.g_input_bit_depth = 8;
  cfg.kf_mode = AOM_KF_DISABLED;
  cfg.rc_min_quantizer = 10;
  cfg.rc_max_quantizer = (unsigned)qmax;
  cfg.rc_undershoot_pct = 50;
  cfg.rc_overshoot_pct = 50;
  cfg.rc_buf_initial_sz = 600;
  cfg.rc_buf_optimal_sz = 600;
  cfg.rc_buf_sz = 1000;
  cfg.g_usage = AOM_USAGE_REALTIME;
  cfg.g_error_resilient = 0;
  cfg.rc_end_usage = AOM_CBR;
  cfg.g_pass = AOM_RC_ONE_PASS;
  cfg.g_lag_in_frames = 0;

  aom_codec_ctx_t codec;
  if (aom_codec_enc_init(&codec, iface, &cfg, 0))
    die_codec(&codec, "enc_init");

  CTRL(AOME_SET_CPUUSED, speed);
  CTRL(AV1E_SET_ENABLE_CDEF, cdef);
  CTRL(AV1E_SET_ENABLE_TPL_MODEL, 0);
  CTRL(AV1E_SET_DELTAQ_MODE, 0);
  CTRL(AV1E_SET_ENABLE_ORDER_HINT, 0);
  CTRL(AV1E_SET_AQ_MODE, aq);
  CTRL(AOME_SET_MAX_INTRA_BITRATE_PCT, 300);
  CTRL(AV1E_SET_COEFF_COST_UPD_FREQ, 3);
  CTRL(AV1E_SET_MODE_COST_UPD_FREQ, 3);
  CTRL(AV1E_SET_MV_COST_UPD_FREQ, 3);
  CTRL(AV1E_SET_ENABLE_PALETTE, 0);
  if (tile_cols >= 0 || tile_rows >= 0) {
    CTRL(AV1E_SET_TILE_COLUMNS, tile_cols < 0 ? 0 : tile_cols);
    CTRL(AV1E_SET_TILE_ROWS, tile_rows < 0 ? 0 : tile_rows);
  } else {
    CTRL(AV1E_SET_AUTO_TILES, 1);
  }
  CTRL(AV1E_SET_ROW_MT, 1);
  CTRL(AV1E_SET_ENABLE_OBMC, 0);
  CTRL(AV1E_SET_NOISE_SENSITIVITY, 0);
  CTRL(AV1E_SET_ENABLE_WARPED_MOTION, 0);
  CTRL(AV1E_SET_ENABLE_GLOBAL_MOTION, 0);
  CTRL(AV1E_SET_ENABLE_REF_FRAME_MVS, 0);
  {
    unsigned int sbsize = AOM_SUPERBLOCK_SIZE_DYNAMIC;
    if (sb == 64) sbsize = AOM_SUPERBLOCK_SIZE_64X64;
    if (sb == 128) sbsize = AOM_SUPERBLOCK_SIZE_128X128;
    /* WebRTC GetSuperblockSize() stops at 1080p (dynamic = 128x128 there); av1rt also uses
       64x64 above: faster with threads and no worse in rate (Av1Encoder.SuperblockControl). */
    if (sb == 0 && threads >= 4 && y4m.w * y4m.h >= 960 * 540)
      sbsize = AOM_SUPERBLOCK_SIZE_64X64;
    CTRL(AV1E_SET_SUPERBLOCK_SIZE, sbsize);
  }
  CTRL(AV1E_SET_ENABLE_CFL_INTRA, 0);
  CTRL(AV1E_SET_ENABLE_SMOOTH_INTRA, 0);
  CTRL(AV1E_SET_ENABLE_ANGLE_DELTA, 0);
  CTRL(AV1E_SET_ENABLE_FILTER_INTRA, 0);
  CTRL(AV1E_SET_INTRA_DEFAULT_TX_ONLY, 1);
  CTRL(AV1E_SET_DISABLE_TRELLIS_QUANT, 1);
  CTRL(AV1E_SET_ENABLE_DIST_WTD_COMP, 0);
  CTRL(AV1E_SET_ENABLE_DIFF_WTD_COMP, 0);
  CTRL(AV1E_SET_ENABLE_DUAL_FILTER, 0);
  CTRL(AV1E_SET_ENABLE_INTERINTRA_COMP, 0);
  CTRL(AV1E_SET_ENABLE_INTERINTRA_WEDGE, 0);
  CTRL(AV1E_SET_ENABLE_INTRA_EDGE_FILTER, 0);
  CTRL(AV1E_SET_ENABLE_INTRABC, 0);
  CTRL(AV1E_SET_ENABLE_MASKED_COMP, 0);
  CTRL(AV1E_SET_ENABLE_PAETH_INTRA, 0);
  CTRL(AV1E_SET_ENABLE_QM, 0);
  CTRL(AV1E_SET_ENABLE_RECT_PARTITIONS, 0);
  CTRL(AV1E_SET_ENABLE_RESTORATION, 0);
  CTRL(AV1E_SET_ENABLE_SMOOTH_INTERINTRA, 0);
  CTRL(AV1E_SET_ENABLE_TX64, 0);
  CTRL(AV1E_SET_MAX_REFERENCE_FRAMES, 3);
  CTRL(AV1E_SET_MAX_CONSEC_FRAME_DROP_MS_CBR, 250);
  CTRL(AV1E_SET_POSTENCODE_DROP_RTC, 1);

  aom_image_t raw;
  if (!aom_img_alloc(&raw, AOM_IMG_FMT_I420, (unsigned)y4m.w,
                     (unsigned)y4m.h, 32))
    die("img_alloc");

  FILE *out = fopen(out_path, "wb");
  if (!out) die("cannot open output");
  FILE *recon = recon_path ? fopen(recon_path, "wb") : NULL;
  if (recon_path && !recon) die("cannot open recon output");
  write_ivf_header(out, y4m.w, y4m.h, fps, 0);

  const int64_t duration = 90000 / fps;
  int64_t pts = 0;
  int frames_in = 0, frames_out = 0;
  int64_t total_bytes = 0;
  int flushing = 0;
  while (1) {
    aom_image_t *img = NULL;
    aom_enc_frame_flags_t flags = 0;
    if (!flushing && (limit == 0 || frames_in < limit) &&
        y4m_read(&y4m, &raw)) {
      img = &raw;
      if (kf_interval > 0 && frames_in > 0 && frames_in % kf_interval == 0)
        flags |= AOM_EFLAG_FORCE_KF;
    } else {
      flushing = 1;
    }
    if (aom_codec_encode(&codec, img, pts, (unsigned long)duration, flags))
      die_codec(&codec, "encode");
    int got = 0;
    const aom_codec_cx_pkt_t *pkt;
    aom_codec_iter_t iter = NULL;
    while ((pkt = aom_codec_get_cx_data(&codec, &iter)) != NULL) {
      if (pkt->kind != AOM_CODEC_CX_FRAME_PKT) continue;
      got = 1;
      int q = -1;
      aom_codec_control(&codec, AOME_GET_LAST_QUANTIZER_64, &q);
      write_ivf_frame(out, pkt->data.frame.buf, pkt->data.frame.sz,
                      pkt->data.frame.pts / duration);
      total_bytes += (int64_t)pkt->data.frame.sz;
      printf("frame %d in %d size %u q %d%s\n", frames_out, frames_in,
             (unsigned)pkt->data.frame.sz, q,
             (pkt->data.frame.flags & AOM_FRAME_IS_KEY) ? " key" : "");
      ++frames_out;
      if (recon) {
        aom_image_t rimg;
        if (aom_codec_control(&codec, AV1_GET_NEW_FRAME_IMAGE, &rimg))
          die_codec(&codec, "AV1_GET_NEW_FRAME_IMAGE");
        write_image(recon, &rimg);
      }
    }
    if (img) {
      ++frames_in;
      pts += duration;
    } else if (!got) {
      break;
    }
  }
  fseek(out, 0, SEEK_SET);
  write_ivf_header(out, y4m.w, y4m.h, fps, frames_out);
  fclose(out);
  if (recon) fclose(recon);
  printf("frames_in %d frames_out %d bytes %lld kbps %.1f\n", frames_in,
         frames_out, (long long)total_bytes,
         frames_in ? total_bytes * 8.0 * fps / frames_in / 1000.0 : 0.0);
  aom_img_free(&raw);
  if (aom_codec_destroy(&codec)) die_codec(&codec, "destroy");
  return 0;
}
