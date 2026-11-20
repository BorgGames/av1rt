// Port of libaom aom/src/aom_encoder.c

namespace Av1rt.Aom;

// [port] FLOATING_POINT_INIT / FLOATING_POINT_RESTORE are empty on x86-64 (SSE math) and
// without HAVE_FEXCEPT && CONFIG_DEBUG, so they are dropped.
internal static partial class aom_encoder
{
    // #define SAVE_STATUS(ctx, var) (ctx ? (ctx->err = var) : var)
    private static aom_codec_err_t SAVE_STATUS(aom_codec_ctx_t ctx, aom_codec_err_t var)
    {
        return ctx != null ? (ctx.err = var) : var;
    }

    private static aom_codec_alg_priv_t get_alg_priv(aom_codec_ctx_t ctx)
    {
        return (aom_codec_alg_priv_t)ctx.priv;
    }

    internal static aom_codec_err_t aom_codec_enc_init_ver(aom_codec_ctx_t ctx,
                                                           aom_codec_iface_t iface,
                                                           aom_codec_enc_cfg_t cfg,
                                                           aom_codec_flags_t flags, int ver)
    {
        aom_codec_err_t res;
        // The value of AOM_ENCODER_ABI_VERSION in libaom v3.0.0 and v3.1.0 - v3.1.3.
        //
        // We are compatible with these older libaom releases. AOM_ENCODER_ABI_VERSION
        // was incremented after these releases for two reasons:
        // 1. AOM_ENCODER_ABI_VERSION takes contribution from
        //    AOM_EXT_PART_ABI_VERSION. The external partition API is still
        //    experimental, so it should not be considered as part of the stable ABI.
        // 2. As a way to detect the presence of speeds 7-9 in all-intra mode.
        const int aom_encoder_abi_version_25 = 25;

        if (ver != AOM_ENCODER_ABI_VERSION && ver != aom_encoder_abi_version_25)
            res = AOM_CODEC_ABI_MISMATCH;
        else if (ctx == null || iface == null || cfg == null)
            res = AOM_CODEC_INVALID_PARAM;
        else if (iface.abi_version != AOM_CODEC_INTERNAL_ABI_VERSION)
            res = AOM_CODEC_ABI_MISMATCH;
        else if ((iface.caps & AOM_CODEC_CAP_ENCODER) == 0)
            res = AOM_CODEC_INCAPABLE;
        else if ((flags & AOM_CODEC_USE_PSNR) != 0 && (iface.caps & AOM_CODEC_CAP_PSNR) == 0)
            res = AOM_CODEC_INCAPABLE;
        else if ((flags & AOM_CODEC_USE_HIGHBITDEPTH) != 0 &&
                 (iface.caps & AOM_CODEC_CAP_HIGHBITDEPTH) == 0)
        {
            res = AOM_CODEC_INCAPABLE;
        }
        else if (cfg.g_bit_depth > 8 &&
                 (flags & AOM_CODEC_USE_HIGHBITDEPTH) == 0)
        {
            res = AOM_CODEC_INVALID_PARAM;
            ctx.err_detail =
                "High bit-depth used without the AOM_CODEC_USE_HIGHBITDEPTH flag.";
        }
        else
        {
            ctx.iface = iface;
            ctx.name = iface.name;
            ctx.priv = null;
            ctx.init_flags = flags;
            ctx.config.enc = cfg;
            res = ctx.iface.init(ctx);

            if (res != 0)
            {
                // IMPORTANT: ctx->priv->err_detail must be null or point to a string
                // that remains valid after ctx->priv is destroyed, such as a C string
                // literal. This makes it safe to call aom_codec_error_detail() after
                // aom_codec_enc_init_ver() failed.
                ctx.err_detail = ctx.priv != null ? ctx.priv.err_detail : null;
                aom_codec_destroy(ctx);
            }
        }

        return SAVE_STATUS(ctx, res);
    }

    internal static aom_codec_err_t aom_codec_enc_config_default(aom_codec_iface_t iface,
                                                                 aom_codec_enc_cfg_t cfg,
                                                                 uint usage)
    {
        aom_codec_err_t res;

        if (iface == null || cfg == null)
            res = AOM_CODEC_INVALID_PARAM;
        else if ((iface.caps & AOM_CODEC_CAP_ENCODER) == 0)
            res = AOM_CODEC_INCAPABLE;
        else
        {
            res = AOM_CODEC_INVALID_PARAM;

            for (int i = 0; i < iface.enc.cfg_count; ++i)
            {
                if (iface.enc.cfgs[i].g_usage == usage)
                {
                    cfg.CopyFrom(iface.enc.cfgs[i]);
                    res = AOM_CODEC_OK;
                    /* default values */
                    cfg.encoder_cfg = default;
                    cfg.encoder_cfg.super_block_size = 0;  // Dynamic
                    cfg.encoder_cfg.max_partition_size = 128;
                    cfg.encoder_cfg.min_partition_size = 4;
                    cfg.encoder_cfg.disable_trellis_quant = 3;
                    break;
                }
            }
        }
        return res;
    }

    // [port] `unsigned long duration` is 32 bits on the oracle build (LLP64), so the
    // `duration > UINT32_MAX` check (ULONG_MAX > UINT32_MAX only) is compiled out.
    internal static aom_codec_err_t aom_codec_encode(aom_codec_ctx_t ctx, aom_image_t img,
                                                     aom_codec_pts_t pts, uint duration,
                                                     aom_enc_frame_flags_t flags)
    {
        aom_codec_err_t res = AOM_CODEC_OK;

        if (ctx == null || (img != null && duration == 0))
            res = AOM_CODEC_INVALID_PARAM;
        else if (ctx.iface == null || ctx.priv == null)
            res = AOM_CODEC_ERROR;
        else if ((ctx.iface.caps & AOM_CODEC_CAP_ENCODER) == 0)
            res = AOM_CODEC_INCAPABLE;
        else if (img != null && ((img.fmt & AOM_IMG_FMT_HIGHBITDEPTH) != 0) !=
                                ((ctx.init_flags & AOM_CODEC_USE_HIGHBITDEPTH) != 0))
        {
            res = AOM_CODEC_INVALID_PARAM;
            ctx.priv.err_detail =
                "img is high bit-depth and codec was not initialized with " +
                "AOM_CODEC_USE_HIGHBITDEPTH, or vice versa.";
        }
        else
        {
            res = ctx.iface.enc.encode(get_alg_priv(ctx), img, pts, duration, flags);
        }

        return SAVE_STATUS(ctx, res);
    }

    internal static aom_codec_cx_pkt_t aom_codec_get_cx_data(aom_codec_ctx_t ctx,
                                                             ref aom_codec_iter_t iter)
    {
        aom_codec_cx_pkt_t pkt = null;

        if (ctx != null)
        {
            // [port] `if (!iter) ctx->err = AOM_CODEC_INVALID_PARAM;`: a ref is never NULL.
            if (ctx.iface == null || ctx.priv == null)
                ctx.err = AOM_CODEC_ERROR;
            else if ((ctx.iface.caps & AOM_CODEC_CAP_ENCODER) == 0)
                ctx.err = AOM_CODEC_INCAPABLE;
            else
                pkt = ctx.iface.enc.get_cx_data(get_alg_priv(ctx), ref iter);
        }

        if (pkt != null && pkt.kind == AOM_CODEC_CX_FRAME_PKT)
        {
            // If the application has specified a destination area for the
            // compressed data, and the codec has not placed the data there,
            // and it fits, copy it.
            aom_codec_priv_t priv = ctx.priv;
            Ptr<byte> dst_buf = priv.enc.cx_data_dst_buf.buf;

            if (!dst_buf.IsNull && pkt.data.frame.buf != dst_buf &&
                (long)pkt.data.frame.sz + priv.enc.cx_data_pad_before +
                        priv.enc.cx_data_pad_after <=
                    priv.enc.cx_data_dst_buf.sz)
            {
                aom_codec_cx_pkt_t modified_pkt = priv.enc.cx_data_pkt;

                memcpy(dst_buf + (int)priv.enc.cx_data_pad_before, pkt.data.frame.buf,
                       pkt.data.frame.sz);
                modified_pkt.CopyFrom(pkt);
                modified_pkt.data.frame.buf = dst_buf;
                modified_pkt.data.frame.sz +=
                    (int)(priv.enc.cx_data_pad_before + priv.enc.cx_data_pad_after);
                pkt = modified_pkt;
            }

            if (dst_buf == pkt.data.frame.buf)
            {
                priv.enc.cx_data_dst_buf.buf = dst_buf + pkt.data.frame.sz;
                priv.enc.cx_data_dst_buf.sz -= pkt.data.frame.sz;
            }
        }

        return pkt;
    }

    // [subset] aom_codec_set_cx_data_buf, aom_codec_get_preview_frame and
    // aom_codec_get_global_headers are never called.

    internal static aom_codec_err_t aom_codec_enc_config_set(aom_codec_ctx_t ctx,
                                                             aom_codec_enc_cfg_t cfg)
    {
        aom_codec_err_t res;

        if (ctx == null || ctx.iface == null || ctx.priv == null || cfg == null)
            res = AOM_CODEC_INVALID_PARAM;
        else if ((ctx.iface.caps & AOM_CODEC_CAP_ENCODER) == 0)
            res = AOM_CODEC_INCAPABLE;
        else
            res = ctx.iface.enc.cfg_set(get_alg_priv(ctx), cfg);

        return SAVE_STATUS(ctx, res);
    }

    internal static int aom_codec_pkt_list_add(aom_codec_pkt_list list, aom_codec_cx_pkt_t pkt)
    {
        if (list.cnt < list.max)
        {
            list.pkts[list.cnt++].CopyFrom(pkt);
            return 0;
        }

        return 1;
    }

    internal static aom_codec_cx_pkt_t aom_codec_pkt_list_get(aom_codec_pkt_list list,
                                                              ref aom_codec_iter_t iter)
    {
        Ptr<aom_codec_cx_pkt_t> pkt;

        if (iter.IsNull)
        {
            iter = list.pkts;
        }

        pkt = iter;

        if ((uint)(pkt - (Ptr<aom_codec_cx_pkt_t>)list.pkts) < list.cnt)
        {
            iter = pkt + 1;
            return pkt.Value;
        }

        return null;
    }
}
