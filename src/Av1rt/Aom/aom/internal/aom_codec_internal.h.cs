// Port of libaom aom/internal/aom_codec_internal.h

global using static Av1rt.Aom.aom_codec_internal;
global using aom_codec_iface = Av1rt.Aom.aom_codec_iface_t;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

/*!\file
 * \brief Describes the decoder algorithm interface for algorithm
 *        implementations.
 *
 * This file defines the private structures and data types that are only
 * relevant to implementing an algorithm, as opposed to using it.
 *
 * To create a decoder algorithm class, an interface structure is put
 * into the global namespace:
 *     <pre>
 *     my_codec.c:
 *       aom_codec_iface_t my_codec = {
 *           "My Codec v1.0",
 *           AOM_CODEC_ALG_ABI_VERSION,
 *           ...
 *       };
 *     </pre>
 *
 * An application instantiates a specific decoder instance by using
 * aom_codec_init() and a pointer to the algorithm's interface structure:
 *     <pre>
 *     my_app.c:
 *       extern aom_codec_iface_t my_codec;
 *       {
 *           aom_codec_ctx_t algo;
 *           aom_codec_init(&algo, &my_codec);
 *       }
 *     </pre>
 *
 * Once initialized, the instance is managed using other functions from
 * the aom_codec_* family.
 */

/*!\brief Instance private storage
 *
 * This structure is allocated by the algorithm's init function. It can be
 * extended in one of two ways. First, a second, algorithm specific structure
 * can be allocated and the priv member pointed to it. Alternatively, this
 * structure can be made the first member of the algorithm specific structure,
 * and the pointer cast to the proper type.
 */
// [port] A class, and the base class of aom_codec_alg_priv_t: C embeds it as the first member
// (`base`) of struct aom_codec_alg_priv and casts between the two pointers. `@base` gives C's
// `ctx->base.x` spelling (ctx.@base.x) without a separate object.
internal partial class aom_codec_priv_t
{
    public string err_detail;
    public aom_codec_flags_t init_flags;
    public readonly aom_codec_priv_enc enc = new();

    public aom_codec_priv_t @base => this;
}

// [port] The anonymous `enc` struct of aom_codec_priv. A class: it embeds the packet that
// aom_codec_get_cx_data hands out by pointer.
internal sealed partial class aom_codec_priv_enc
{
    public aom_fixed_buf_t cx_data_dst_buf;
    public uint cx_data_pad_before;
    public uint cx_data_pad_after;
    public readonly aom_codec_cx_pkt_t cx_data_pkt = new();
}

/*!\brief Algorithm private storage, defined by the algorithm (av1_cx_iface.c). */
// [port] Declared here because C# has no forward declarations; the port of av1/av1_cx_iface.c
// adds its fields (struct aom_codec_alg_priv) to this partial class. get_alg_priv(ctx) is
// `(aom_codec_alg_priv_t)ctx.priv`.
internal partial class aom_codec_alg_priv_t : aom_codec_priv_t
{
}

/*!\brief init function pointer prototype
 *
 * Performs algorithm-specific initialization of the decoder context. This
 * function is called by aom_codec_dec_init() and aom_codec_enc_init(), so
 * plugins implementing this interface may trust the input parameters to be
 * properly initialized.
 *
 * \param[in] ctx   Pointer to this instance's context
 * \retval #AOM_CODEC_OK
 *     The input stream was recognized and decoder initialized.
 * \retval #AOM_CODEC_MEM_ERROR
 *     Memory operation failed.
 */
internal delegate aom_codec_err_t aom_codec_init_fn_t(aom_codec_ctx_t ctx);

/*!\brief destroy function pointer prototype
 *
 * Performs algorithm-specific destruction of the decoder context. This
 * function is called by the generic aom_codec_destroy() wrapper function,
 * so plugins implementing this interface may trust the input parameters
 * to be properly initialized.
 *
 * \param[in] ctx   Pointer to this instance's context
 * \retval #AOM_CODEC_OK
 *     The input stream was recognized and decoder initialized.
 * \retval #AOM_CODEC_MEM_ERROR
 *     Memory operation failed.
 */
internal delegate aom_codec_err_t aom_codec_destroy_fn_t(aom_codec_alg_priv_t ctx);

/*!\brief control function pointer prototype
 *
 * This function is used to exchange algorithm specific data with the decoder
 * instance. This can be used to implement features specific to a particular
 * algorithm.
 *
 * This function is called by the generic aom_codec_control() wrapper
 * function, so plugins implementing this interface may trust the input
 * parameters to be properly initialized. However,  this interface does not
 * provide type safety for the exchanged data or assign meanings to the
 * control IDs. Those details should be specified in the algorithm's
 * header file. In particular, the ctrl_id parameter is guaranteed to exist
 * in the algorithm's control mapping table, and the data parameter may be NULL.
 *
 *
 * \param[in]     ctx              Pointer to this instance's context
 * \param[in]     ctrl_id          Algorithm specific control identifier
 * \param[in,out] data             Data to exchange with algorithm instance.
 *
 * \retval #AOM_CODEC_OK
 *     The internal state data was deserialized.
 */
internal delegate aom_codec_err_t aom_codec_control_fn_t(aom_codec_alg_priv_t ctx, va_list ap);

/*!\brief Sets the codec option.
 *
 * \param[in]     ctx              Pointer to this instance's context
 * \param[in]     name             A string of the option's name (key)
 * \param[in]     value            A string of the value to be set to
 *
 * \retval #AOM_CODEC_OK
 *     The option is successfully set to the value
 * \retval #AOM_CODEC_INVALID_PARAM
 *     The data was not valid.
 */
internal delegate aom_codec_err_t aom_codec_set_option_fn_t(aom_codec_alg_priv_t ctx, string name,
                                                            string value);

/*!\brief control function pointer mapping
 *
 * This structure stores the mapping between control identifiers and
 * implementing functions. Each algorithm provides a list of these
 * mappings. This list is searched by the aom_codec_control()
 * function to determine which function to invoke. The special
 * value defined by CTRL_MAP_END is used to indicate end-of-list, and must be
 * present. A NULL mapping may be used as a catch-all mapping. This
 * catch-all must be the last entry in the list.
 *
 * C: `{ AV1E_SET_X, ctrl_set_x }` is `new(AV1E_SET_X, ctrl_set_x)`.
 */
internal partial struct aom_codec_ctrl_fn_map_t
{
    public int ctrl_id;
    public aom_codec_control_fn_t fn;

    public aom_codec_ctrl_fn_map_t(int ctrl_id, aom_codec_control_fn_t fn)
    {
        this.ctrl_id = ctrl_id;
        this.fn = fn;
    }
}

/*!\brief encode function pointer prototype
 *
 * Encodes a frame.
 */
// [port] `unsigned long duration` is 32 bits on the oracle build.
internal delegate aom_codec_err_t aom_codec_encode_fn_t(aom_codec_alg_priv_t ctx, aom_image_t img,
                                                        aom_codec_pts_t pts, uint duration,
                                                        aom_enc_frame_flags_t flags);

/*!\brief Get compressed data function pointer prototype
 *
 * Returns the next compressed data packet, or NULL if none remain.
 */
internal delegate aom_codec_cx_pkt_t aom_codec_get_cx_data_fn_t(aom_codec_alg_priv_t ctx,
                                                                ref aom_codec_iter_t iter);

internal delegate aom_codec_err_t aom_codec_enc_config_set_fn_t(aom_codec_alg_priv_t ctx,
                                                                aom_codec_enc_cfg_t cfg);

// [port] Returns a pointer to a heap-allocated aom_fixed_buf_t (a one element array), or NULL.
internal delegate Ptr<aom_fixed_buf_t> aom_codec_get_global_headers_fn_t(aom_codec_alg_priv_t ctx);

internal delegate aom_image_t aom_codec_get_preview_frame_fn_t(aom_codec_alg_priv_t ctx);

/*!\brief Decoder algorithm interface
 *
 * All decoders \ref MUST expose a variable of this type.
 */
// [port] A class (C: `typedef const struct aom_codec_iface aom_codec_iface_t`; always used
// through pointers). The decoder half (struct aom_codec_dec_iface: peek_si, get_si, decode,
// get_frame, set_fb_fn) is not ported: the encoder's interface fills it with NULLs
// (NOT_IMPLEMENTED) and its types belong to aom_decoder.h.
internal sealed partial class aom_codec_iface_t
{
    public string name;                                /*< Identification String  */
    public int abi_version;                            /*< Implemented ABI version */
    public aom_codec_caps_t caps;                      /*< Decoder capabilities */
    public aom_codec_init_fn_t init;                   /*< \copydoc ::aom_codec_init_fn_t */
    public aom_codec_destroy_fn_t destroy;             /*< \copydoc ::aom_codec_destroy_fn_t */
    public Ptr<aom_codec_ctrl_fn_map_t> ctrl_maps;     /*< \copydoc ::aom_codec_ctrl_fn_map_t */
    public aom_codec_enc_iface enc;
    public aom_codec_set_option_fn_t set_option;
}

// [port] struct aom_codec_enc_iface, the `enc` member of struct aom_codec_iface.
internal partial struct aom_codec_enc_iface
{
    public int cfg_count;
    public aom_codec_enc_cfg_t[] cfgs;                       /*< \copydoc ::aom_codec_enc_cfg_t */
    public aom_codec_encode_fn_t encode;                     /*< \copydoc ::aom_codec_encode_fn_t */
    public aom_codec_get_cx_data_fn_t get_cx_data;           /*< \copydoc ::aom_codec_get_cx_data_fn_t */
    public aom_codec_enc_config_set_fn_t cfg_set;            /*< \copydoc ::aom_codec_enc_config_set_fn_t */
    public aom_codec_get_global_headers_fn_t get_glob_hdrs;  /*< \copydoc ::aom_codec_get_global_headers_fn_t */
    public aom_codec_get_preview_frame_fn_t get_preview;     /*< \copydoc ::aom_codec_get_preview_frame_fn_t */
}

/* Internal Utility Functions
 *
 * The following functions are intended to be used inside algorithms as
 * utilities for manipulating aom_codec_* data structures.
 */
// [port] A class: only used through pointers (&ctx->pkt_list.head). pkts holds every packet
// slot of the declaring aom_codec_pkt_list_decl(n) (C overlays the `pkts[1]` member onto the
// larger array that follows it), each slot allocated.
internal sealed partial class aom_codec_pkt_list
{
    public uint cnt;
    public uint max;
    public readonly aom_codec_cx_pkt_t[] pkts;

    public aom_codec_pkt_list(int n)
    {
        pkts = new aom_codec_cx_pkt_t[n];
        for (int i = 0; i < n; i++) pkts[i] = new aom_codec_cx_pkt_t();
    }
}

// [port] `struct aom_codec_pkt_list_decl(n) pkt_list;` becomes
// `public readonly aom_codec_pkt_list_decl pkt_list = new(n);`. `pkt_list.head` and
// `pkt_list.alloc.head` are the same list in C; the port only has `head`.
internal sealed partial class aom_codec_pkt_list_decl
{
    public readonly aom_codec_pkt_list head;

    public aom_codec_pkt_list_decl(int n)
    {
        head = new aom_codec_pkt_list(n);
    }
}

// [port] jmp_buf jmp is dropped: aom_internal_error throws AomException instead of longjmp
// (PORTING 5), and `setjmp` stays as the bookkeeping flag the C code sets and clears. A class:
// structs embed it by value (AV1_PRIMARY.error, thread data error_info) and use it through
// pointers; embedders allocate it (`public readonly aom_internal_error_info error = new();`).
internal sealed partial class aom_internal_error_info
{
    public aom_codec_err_t error_code;
    public int has_detail;
    // [port] char detail[ARG_ERR_MSG_MAX_LEN]
    public string detail;
    public int setjmp;  // Boolean: whether 'jmp' is valid.
}

// [port] C's va_list for aom_codec_control: the variadic arguments are passed as object[] and
// read in order with va_arg<T>(). Integer arguments convert between the C integer types the way
// a C callee reading them would (two's complement reinterpretation), since callers pass int
// literals for unsigned controls and vice versa.
internal sealed partial class va_list
{
    private readonly object[] _args;
    private int _next;

    public va_list(params object[] args)
    {
        _args = args ?? Array.Empty<object>();
    }

    public T va_arg<T>()
    {
        object o = _args[_next++];
        if (o is T t) return t;
        if (o == null) return default;
        long v = o switch
        {
            int i => i,
            uint u => u,
            long l => l,
            ulong ul => unchecked((long)ul),
            short s => s,
            ushort us => us,
            sbyte sb => sb,
            byte b => b,
            bool bo => bo ? 1 : 0,
            _ => throw new InvalidCastException("va_arg: argument type mismatch"),
        };
        if (typeof(T) == typeof(int)) return (T)(object)unchecked((int)v);
        if (typeof(T) == typeof(uint)) return (T)(object)unchecked((uint)v);
        if (typeof(T) == typeof(long)) return (T)(object)v;
        if (typeof(T) == typeof(ulong)) return (T)(object)unchecked((ulong)v);
        throw new InvalidCastException("va_arg: argument type mismatch");
    }
}

internal static partial class aom_codec_internal
{
    /*!\brief Current ABI version number
     *
     * \internal
     * If this file is altered in any way that changes the ABI, this value
     * must be bumped.  Examples include, but are not limited to, changing
     * types, removing or reassigning enums, adding/removing/rearranging
     * fields to structures
     */
    internal const int AOM_CODEC_INTERNAL_ABI_VERSION = (7); /*<\hideinitializer*/

    // [port] From common/args_helper.h, which defines the size of aom_internal_error_info.detail.
    internal const int ARG_ERR_MSG_MAX_LEN = 200;

    /*!\brief control function pointer mapping end marker: { 0, NULL } */
    internal static readonly aom_codec_ctrl_fn_map_t CTRL_MAP_END = new(0, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int at_ctrl_map_end(Ptr<aom_codec_ctrl_fn_map_t> e)
    {
        return B(e.Value.ctrl_id == 0 && e.Value.fn == null);
    }

    // CAST(id, arg): va_arg((arg), aom_codec_control_type_##id)
    // [port] The C type is looked up from AOM_CTRL_USE_TYPE(id, type) in aomcx.h / aom.h and
    // written as the type argument: CAST(AOME_SET_CPUUSED, args) is CAST<int>(AOME_SET_CPUUSED, args).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T CAST<T>(int id, va_list arg) => arg.va_arg<T>();

    // aom_codec_pkt_list_init(m)
    internal static void aom_codec_pkt_list_init(aom_codec_pkt_list_decl m)
    {
        m.head.cnt = 0;
        m.head.max = (uint)m.head.pkts.Length;
    }

    // [port] aom_codec_pkt_list_add / aom_codec_pkt_list_get are defined in
    // aom/src/aom_encoder.c, and aom_set_error / aom_internal_error / aom_internal_error_copy /
    // aom_merge_corrupted_flag in aom/src/aom_codec.c; they belong to those files' ports.
}
