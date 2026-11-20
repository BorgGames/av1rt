/* Smoke shim: proves the shim build links against libaom's internals. */
#include "aom_dsp/entenc.h"
#include "aom_dsp/prob.h"

__declspec(dllexport) int aomshim_smoke_get_msb(unsigned int n) { return get_msb(n); }

/* Encodes nsyms-ary symbols with a fixed CDF; returns the byte count written to out. */
__declspec(dllexport) int aomshim_smoke_ec(const int *symbols, int count,
                                           const uint16_t *icdf, int nsyms,
                                           unsigned char *out, int out_cap) {
  od_ec_enc enc;
  od_ec_enc_init(&enc, 64);
  for (int i = 0; i < count; ++i) od_ec_encode_cdf_q15(&enc, symbols[i], icdf, nsyms);
  uint32_t n;
  unsigned char *buf = od_ec_enc_done(&enc, &n);
  int written = (int)n <= out_cap ? (int)n : -1;
  if (written > 0) memcpy(out, buf, n);
  od_ec_enc_clear(&enc);
  return written;
}
