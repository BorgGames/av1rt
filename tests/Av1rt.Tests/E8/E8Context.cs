using Av1rt.Aom;
using static Av1rt.Aom.aom_encoder;
using static Av1rt.Aom.aomcx;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.enc_enums;
using static Av1rt.Aom.encoder;
using static Av1rt.Aom.ratectrl;
using static Av1rt.Aom.seg_common;

namespace Av1rt.Tests.E8;

// Indices of the encoder parameters passed to the shim (same order as the enum in shim_e8.c).
internal static class P
{
    public const int SPEED = 0, WIDTH = 1, HEIGHT = 2, FRAME_TYPE = 3, UPDATE_TYPE = 4, BASE_QINDEX = 5,
        AQ_MODE = 6, RC_MODE = 7, NUM_WORKERS = 8, ROW_MT = 9, KEY_FREQ_MAX = 10, ENABLE_TX_SIZE_SEARCH = 11,
        GF_CBR_BOOST_PCT = 12, DISABLE_TRELLIS_QUANT = 13, RTC_EXTERNAL_RATECTRL = 14, LAG_IN_FRAMES = 15,
        ALT_REF_FRAME = 16, IS_SRC_FRAME_ALT_REF = 17, ACTIVE_MAP = 18, ENABLE_SUPERRES = 19, CALC_PSNR = 20,
        CFG_WIDTH = 21, CFG_HEIGHT = 22, TUNING = 23, LOSSLESS = 24, SEQ_PARAMS_LOCKED = 25, SHOW_FRAME = 26,
        Y_DC_DELTA_Q = 27, LAYER_DEPTH = 28, GFU_BOOST = 29, USE_FIXED_QP_OFFSETS = 30, FRAMES_SINCE_KEY = 31,
        FRAME_NUMBER = 32, DISABLE_CDF_UPDATE = 33, ALLOW_HP = 34, FORCE_INTEGER_MV = 35, SKIP_MODE_FLAG = 36,
        COST_UPD_COEFF = 37, COST_UPD_MODE = 38, COST_UPD_MV = 39, COST_UPD_DV = 40, CDF_UPDATE_MODE = 41,
        FRAMES_SINCE_LAST_UPDATE = 42, HIGH_SOURCE_SAD = 43, MAXQ_SCENE_CHANGE = 44, SEG_ENABLED = 45,
        ALLOC_MV_COSTS = 46, COUNT = 47;

    // A typical single-layer realtime CBR configuration.
    public static int[] Default(int speed, int width, int height)
    {
        var p = new int[COUNT];
        p[SPEED] = speed;
        p[WIDTH] = width;
        p[HEIGHT] = height;
        p[FRAME_TYPE] = INTER_FRAME;
        p[UPDATE_TYPE] = LF_UPDATE;
        p[BASE_QINDEX] = 140;
        p[AQ_MODE] = CYCLIC_REFRESH_AQ;
        p[RC_MODE] = AOM_CBR;
        p[NUM_WORKERS] = 1;
        p[ROW_MT] = 1;
        p[KEY_FREQ_MAX] = 9999;
        p[ENABLE_TX_SIZE_SEARCH] = 1;
        p[DISABLE_TRELLIS_QUANT] = 3;
        p[CFG_WIDTH] = width;
        p[CFG_HEIGHT] = height;
        p[TUNING] = AOM_TUNE_PSNR;
        p[SHOW_FRAME] = 1;
        p[FRAMES_SINCE_KEY] = 5;
        p[FRAME_NUMBER] = 5;
        p[COST_UPD_COEFF] = COST_UPD_SB;
        p[COST_UPD_MODE] = COST_UPD_SB;
        p[COST_UPD_MV] = COST_UPD_SB;
        p[COST_UPD_DV] = COST_UPD_SB;
        p[CDF_UPDATE_MODE] = 1;
        p[FRAMES_SINCE_LAST_UPDATE] = 1;
        p[MAXQ_SCENE_CHANGE] = 20;
        return p;
    }

    public static string Describe(int[] p) =>
        $"speed={p[SPEED]} {p[WIDTH]}x{p[HEIGHT]} frame_type={p[FRAME_TYPE]} update={p[UPDATE_TYPE]} q={p[BASE_QINDEX]} " +
        $"aq={p[AQ_MODE]} rc={p[RC_MODE]} workers={p[NUM_WORKERS]} trellis={p[DISABLE_TRELLIS_QUANT]} lag={p[LAG_IN_FRAMES]} " +
        $"tuning={p[TUNING]} lossless={p[LOSSLESS]} fsk={p[FRAMES_SINCE_KEY]} fn={p[FRAME_NUMBER]} seg={p[SEG_ENABLED]}";
}

// C# mirror of ctx_setup() in shim_e8.c.
internal static class E8Context
{
    public static AV1_COMP Create(int[] p)
    {
        var cpi = new AV1_COMP();
        cpi.ppi = new AV1_PRIMARY();
        AV1_COMMON cm = cpi.common;
        cm.seq_params = cpi.ppi.seq_params;
        cm.fc = new FRAME_CONTEXT();
        cpi.cyclic_refresh = new CYCLIC_REFRESH();
        cpi.td.mb.mv_costs = p[P.ALLOC_MV_COSTS] != 0 ? new MvCosts() : null;

        SequenceHeader seq = cm.seq_params;
        seq.bit_depth = 8;
        seq.enable_masked_compound = 1;
        seq.enable_interintra_compound = 1;
        seq.enable_dual_filter = 1;
        seq.enable_restoration = 1;
        seq.order_hint_info.enable_dist_wtd_comp = 1;

        cpi.oxcf.mode = REALTIME;
        cpi.oxcf.pass = 0; // AOM_RC_ONE_PASS
        cpi.speed = p[P.SPEED];
        cm.width = p[P.WIDTH];
        cm.height = p[P.HEIGHT];
        cm.current_frame.frame_type = p[P.FRAME_TYPE];
        cpi.gf_frame_index = 0;
        cpi.ppi.gf_group.update_type[0] = p[P.UPDATE_TYPE];
        cpi.ppi.gf_group.layer_depth[0] = p[P.LAYER_DEPTH];
        cm.quant_params.base_qindex = p[P.BASE_QINDEX];
        cm.quant_params.y_dc_delta_q = p[P.Y_DC_DELTA_Q];
        cpi.oxcf.q_cfg.aq_mode = p[P.AQ_MODE];
        cpi.oxcf.q_cfg.use_fixed_qp_offsets = p[P.USE_FIXED_QP_OFFSETS];
        cpi.oxcf.rc_cfg.mode = p[P.RC_MODE];
        cpi.mt_info.num_workers = p[P.NUM_WORKERS];
        cpi.oxcf.row_mt = p[P.ROW_MT] != 0;
        cpi.oxcf.kf_cfg.key_freq_max = p[P.KEY_FREQ_MAX];
        cpi.oxcf.txfm_cfg.enable_tx_size_search = p[P.ENABLE_TX_SIZE_SEARCH] != 0;
        cpi.oxcf.rc_cfg.gf_cbr_boost_pct = (uint)p[P.GF_CBR_BOOST_PCT];
        cpi.oxcf.algo_cfg.disable_trellis_quant = p[P.DISABLE_TRELLIS_QUANT];
        cpi.oxcf.algo_cfg.cdf_update_mode = (byte)p[P.CDF_UPDATE_MODE];
        cpi.rc.rtc_external_ratectrl = p[P.RTC_EXTERNAL_RATECTRL];
        cpi.oxcf.gf_cfg.lag_in_frames = p[P.LAG_IN_FRAMES];
        cpi.refresh_frame.alt_ref_frame = p[P.ALT_REF_FRAME] != 0;
        cpi.rc.is_src_frame_alt_ref = p[P.IS_SRC_FRAME_ALT_REF];
        cpi.active_map.enabled = p[P.ACTIVE_MAP];
        cpi.oxcf.superres_cfg.enable_superres = p[P.ENABLE_SUPERRES] != 0;
        cpi.ppi.b_calculate_psnr = p[P.CALC_PSNR];
        cpi.oxcf.frm_dim_cfg.width = p[P.CFG_WIDTH];
        cpi.oxcf.frm_dim_cfg.height = p[P.CFG_HEIGHT];
        cpi.oxcf.tune_cfg.tuning = p[P.TUNING];
        cpi.oxcf.rc_cfg.best_allowed_q = 0;
        cpi.oxcf.rc_cfg.worst_allowed_q = p[P.LOSSLESS] != 0 ? 0 : 255;
        cpi.ppi.seq_params_locked = p[P.SEQ_PARAMS_LOCKED];
        cm.show_frame = p[P.SHOW_FRAME];
        cpi.svc.number_spatial_layers = 1;
        cpi.svc.number_temporal_layers = 1;

        cpi.ppi.p_rc.gfu_boost = p[P.GFU_BOOST];
        cpi.rc.frames_since_key = p[P.FRAMES_SINCE_KEY];
        cm.current_frame.frame_number = (uint)p[P.FRAME_NUMBER];
        cm.features.disable_cdf_update = p[P.DISABLE_CDF_UPDATE] != 0;
        cm.features.allow_high_precision_mv = p[P.ALLOW_HP] != 0;
        cm.features.cur_frame_force_integer_mv = p[P.FORCE_INTEGER_MV] != 0;
        cm.current_frame.skip_mode_info.skip_mode_flag = p[P.SKIP_MODE_FLAG];
        cpi.oxcf.cost_upd_freq.coeff = p[P.COST_UPD_COEFF];
        cpi.oxcf.cost_upd_freq.mode = p[P.COST_UPD_MODE];
        cpi.oxcf.cost_upd_freq.mv = p[P.COST_UPD_MV];
        cpi.oxcf.cost_upd_freq.dv = p[P.COST_UPD_DV];
        cpi.frames_since_last_update = p[P.FRAMES_SINCE_LAST_UPDATE];
        cpi.rc.high_source_sad = p[P.HIGH_SOURCE_SAD];
        cpi.cyclic_refresh.counter_encode_maxq_scene_change = p[P.MAXQ_SCENE_CHANGE];
        if (p[P.SEG_ENABLED] != 0)
        {
            segmentation seg = cm.seg;
            seg.enabled = 1;
            seg.feature_mask[1] |= 1u << SEG_LVL_ALT_Q;
            seg.feature_data[1][SEG_LVL_ALT_Q] = -20;
            seg.feature_mask[2] |= 1u << SEG_LVL_ALT_Q;
            seg.feature_data[2][SEG_LVL_ALT_Q] = 35;
        }
        return cpi;
    }

    public static void SetSpeedFeatures(AV1_COMP cpi)
    {
        speed_features.av1_set_speed_features_framesize_independent(cpi, cpi.speed);
        speed_features.av1_set_speed_features_framesize_dependent(cpi, cpi.speed);
        speed_features.av1_set_speed_features_qindex_dependent(cpi, cpi.speed);
    }
}
