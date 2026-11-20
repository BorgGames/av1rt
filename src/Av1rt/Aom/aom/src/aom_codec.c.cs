// Port of libaom aom/src/aom_codec.c

using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class aom_codec
{
    internal static string aom_codec_iface_name(aom_codec_iface_t iface)
    {
        return iface != null ? iface.name : "<invalid interface>";
    }

    internal static string aom_codec_err_to_string(aom_codec_err_t err)
    {
        switch (err)
        {
            case AOM_CODEC_OK: return "Success";
            case AOM_CODEC_ERROR: return "Unspecified internal error";
            case AOM_CODEC_MEM_ERROR: return "Memory allocation error";
            case AOM_CODEC_ABI_MISMATCH: return "ABI version mismatch";
            case AOM_CODEC_INCAPABLE:
                return "Codec does not implement requested capability";
            case AOM_CODEC_UNSUP_BITSTREAM:
                return "Bitstream not supported by this decoder";
            case AOM_CODEC_UNSUP_FEATURE:
                return "Bitstream required feature not supported by this decoder";
            case AOM_CODEC_CORRUPT_FRAME: return "Corrupt frame detected";
            case AOM_CODEC_INVALID_PARAM: return "Invalid parameter";
            case AOM_CODEC_LIST_END: return "End of iterated list";
        }

        return "Unrecognized error code";
    }

    internal static string aom_codec_error(aom_codec_ctx_t ctx)
    {
        return (ctx != null) ? aom_codec_err_to_string(ctx.err)
                             : aom_codec_err_to_string(AOM_CODEC_INVALID_PARAM);
    }

    internal static string aom_codec_error_detail(aom_codec_ctx_t ctx)
    {
        if (ctx != null && ctx.err != 0)
            return ctx.priv != null ? ctx.priv.err_detail : ctx.err_detail;

        return null;
    }

    internal static aom_codec_err_t aom_codec_destroy(aom_codec_ctx_t ctx)
    {
        if (ctx == null)
        {
            return AOM_CODEC_INVALID_PARAM;
        }
        if (ctx.iface == null || ctx.priv == null)
        {
            ctx.err = AOM_CODEC_ERROR;
            return AOM_CODEC_ERROR;
        }
        ctx.iface.destroy((aom_codec_alg_priv_t)ctx.priv);
        ctx.iface = null;
        ctx.name = null;
        ctx.priv = null;
        ctx.err = AOM_CODEC_OK;
        return AOM_CODEC_OK;
    }

    internal static aom_codec_caps_t aom_codec_get_caps(aom_codec_iface_t iface)
    {
        return iface != null ? iface.caps : 0;
    }

    // [port] The variadic arguments arrive as object[] and are handed to the control function
    // as a va_list (see aom_codec_internal.h.cs).
    internal static aom_codec_err_t aom_codec_control(aom_codec_ctx_t ctx, int ctrl_id,
                                                      params object[] args)
    {
        if (ctx == null)
        {
            return AOM_CODEC_INVALID_PARAM;
        }
        // Control ID must be non-zero.
        if (ctrl_id == 0)
        {
            ctx.err = AOM_CODEC_INVALID_PARAM;
            return AOM_CODEC_INVALID_PARAM;
        }
        if (ctx.iface == null || ctx.priv == null || ctx.iface.ctrl_maps.IsNull)
        {
            ctx.err = AOM_CODEC_ERROR;
            return AOM_CODEC_ERROR;
        }

        // "ctrl_maps" is an array of (control ID, function pointer) elements,
        // with CTRL_MAP_END as a sentinel.
        for (Ptr<aom_codec_ctrl_fn_map_t> entry = ctx.iface.ctrl_maps;
             at_ctrl_map_end(entry) == 0; ++entry)
        {
            if (entry.Value.ctrl_id == ctrl_id)
            {
                va_list ap = new va_list(args);
                ctx.err = entry.Value.fn((aom_codec_alg_priv_t)ctx.priv, ap);
                return ctx.err;
            }
        }
        ctx.err = AOM_CODEC_ERROR;
        ctx.priv.err_detail = "Invalid control ID";
        return AOM_CODEC_ERROR;
    }

    // [port] set_error(info, error, fmt, ap): callers format the message themselves (C# string
    // interpolation) and pass it as `fmt`; NULL means no detail. C truncates the detail to
    // sizeof(info->detail) - 2 characters (vsnprintf into sz - 1 bytes).
    private static void set_error(aom_internal_error_info info, aom_codec_err_t error, string fmt)
    {
        info.error_code = error;
        info.has_detail = 0;

        if (fmt != null)
        {
            int sz = ARG_ERR_MSG_MAX_LEN;

            info.has_detail = 1;
            info.detail = fmt.Length > sz - 2 ? fmt.Substring(0, sz - 2) : fmt;
        }
    }

    internal static void aom_set_error(aom_internal_error_info info, aom_codec_err_t error,
                                       string fmt)
    {
        set_error(info, error, fmt);

        Debug.Assert(info.setjmp == 0);
    }

    // [port] longjmp(info->jmp, ...) becomes a throw (PORTING 5). C only jumps when
    // info->setjmp is set and otherwise returns to the caller; every in-scope caller either runs
    // under a setjmp or treats the error as fatal, so the port always throws. The catch sites
    // (the ports of the setjmp sites) restore the C state with aom_set_error_from_exception.
    internal static void aom_internal_error(aom_internal_error_info info, aom_codec_err_t error,
                                            string fmt)
    {
        set_error(info, error, fmt);

        throw new AomException(info.error_code, info.has_detail != 0 ? info.detail : null)
        {
            has_detail = info.has_detail,
        };
    }

    internal static void aom_internal_error_copy(aom_internal_error_info info,
                                                 aom_internal_error_info src)
    {
        Debug.Assert(info != src);
        Debug.Assert(src.setjmp == 0);

        if (src.has_detail == 0)
        {
            aom_internal_error(info, src.error_code, null);
        }
        else
        {
            aom_internal_error(info, src.error_code, src.detail);
        }
    }

    internal static void aom_merge_corrupted_flag(ref int corrupted, int value)
    {
        corrupted |= value;
    }

    // [port] Not in libaom. Other modules throw AomException directly instead of calling
    // aom_internal_error(info, ...) (the info is often not at hand), so the ports of setjmp
    // sites call this in their catch block to leave `info` as C's set_error would have.
    // AomException.has_detail (src/Av1rt/Aom/_fields/g1.cs) restores a NULL detail.
    internal static void aom_set_error_from_exception(aom_internal_error_info info, AomException e)
    {
        set_error(info, e.ErrorCode, e.has_detail != 0 ? e.Message : null);
    }
}
