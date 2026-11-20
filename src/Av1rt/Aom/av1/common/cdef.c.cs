// Port of libaom av1/common/cdef.c

using System;

namespace Av1rt.Aom;

internal static partial class cdef
{
    // [port] `MB_MODE_INFO **grid` is the mi_grid_base array; the pointer arithmetic
    // `grid + mi_row * mi_stride + mi_col` is an index into it.
    private static int is_8x8_block_skip(MB_MODE_INFO[] grid, int mi_row, int mi_col,
                                         int mi_stride)
    {
        int mbmi = mi_row * mi_stride + mi_col;
        for (int r = 0; r < mi_size_high[BLOCK_8X8]; ++r, mbmi += mi_stride)
        {
            for (int c = 0; c < mi_size_wide[BLOCK_8X8]; ++c)
            {
                if (grid[mbmi + c].skip_txfm == 0) return 0;
            }
        }

        return 1;
    }

    internal static int av1_cdef_compute_sb_list(CommonModeInfoParams mi_params,
                                                 int mi_row, int mi_col, Ptr<cdef_list> dlist,
                                                 BLOCK_SIZE bs)
    {
        MB_MODE_INFO[] grid = mi_params.mi_grid_base;
        int maxc = mi_params.mi_cols - mi_col;
        int maxr = mi_params.mi_rows - mi_row;

        if (bs == BLOCK_128X128 || bs == BLOCK_128X64)
            maxc = AOMMIN(maxc, MI_SIZE_128X128);
        else
            maxc = AOMMIN(maxc, MI_SIZE_64X64);
        if (bs == BLOCK_128X128 || bs == BLOCK_64X128)
            maxr = AOMMIN(maxr, MI_SIZE_128X128);
        else
            maxr = AOMMIN(maxr, MI_SIZE_64X64);

        const int r_step = 2;  // mi_size_high[BLOCK_8X8]
        const int c_step = 2;  // mi_size_wide[BLOCK_8X8]
        const int r_shift = 1;
        const int c_shift = 1;
        int count = 0;
        for (int r = 0; r < maxr; r += r_step)
        {
            for (int c = 0; c < maxc; c += c_step)
            {
                if (is_8x8_block_skip(grid, mi_row + r, mi_col + c,
                                      mi_params.mi_stride) == 0)
                {
                    dlist[count].by = (byte)(r >> r_shift);
                    dlist[count].bx = (byte)(c >> c_shift);
                    count++;
                }
            }
        }
        return count;
    }

    internal static void cdef_copy_rect8_8bit_to_16bit_c(Ptr<ushort> dst, int dstride,
                                                         Ptr<byte> src, int sstride, int width,
                                                         int height)
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                dst[i * dstride + j] = src[i * sstride + j];
            }
        }
    }

    // [subset] cdef_copy_rect8_16bit_to_16bit_c / av1_cdef_copy_sb8_16_highbd:
    // CONFIG_AV1_HIGHBITDEPTH == 0.

    internal static void av1_cdef_copy_sb8_16_lowbd(Ptr<ushort> dst, int dstride,
                                                    Ptr<byte> src, int src_voffset,
                                                    int src_hoffset, int sstride, int vsize,
                                                    int hsize)
    {
        Ptr<byte> @base = src + (src_voffset * sstride + src_hoffset);
        cdef_copy_rect8_8bit_to_16bit(dst, dstride, @base, sstride, hsize, vsize);
    }

    internal static void av1_cdef_copy_sb8_16(AV1_COMMON cm, Ptr<ushort> dst,
                                              int dstride, Ptr<byte> src, int src_voffset,
                                              int src_hoffset, int sstride, int vsize, int hsize)
    {
        // [subset] use_highbitdepth branch: CONFIG_AV1_HIGHBITDEPTH == 0.
        av1_cdef_copy_sb8_16_lowbd(dst, dstride, src, src_voffset, src_hoffset,
                                   sstride, vsize, hsize);
    }

    private static void copy_rect(Ptr<ushort> dst, int dstride, Ptr<ushort> src,
                                  int sstride, int v, int h)
    {
        for (int i = 0; i < v; i++)
        {
            for (int j = 0; j < h; j++)
            {
                dst[i * dstride + j] = src[i * sstride + j];
            }
        }
    }

    // Prepares intermediate input buffer for CDEF.
    // Inputs:
    //   cm: Pointer to common structure.
    //   fb_info: Pointer to the CDEF block-level parameter structure.
    //   colbuf: Left column buffer for CDEF.
    //   cdef_left: Left block is filtered or not.
    //   fbc, fbr: col and row index of a block.
    //   plane: plane index Y/CB/CR.
    // Returns:
    //   Nothing will be returned.
    private static void cdef_prepare_fb(AV1_COMMON cm, CdefBlockInfo fb_info,
                                        Ptr<ushort>[] colbuf, int cdef_left,
                                        int fbc, int fbr, int plane)
    {
        CommonModeInfoParams mi_params = cm.mi_params;
        Ptr<ushort> src = fb_info.src;
        int luma_stride =
            ALIGN_POWER_OF_TWO(mi_params.mi_cols << MI_SIZE_LOG2, 4);
        int nvfb = (mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        int nhfb = (mi_params.mi_cols + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        int cstart = 0;
        if (cdef_left == 0) cstart = -CDEF_HBORDER;
        int rend, cend;
        int nhb =
            AOMMIN(MI_SIZE_64X64, mi_params.mi_cols - MI_SIZE_64X64 * fbc);
        int nvb =
            AOMMIN(MI_SIZE_64X64, mi_params.mi_rows - MI_SIZE_64X64 * fbr);
        int hsize = nhb << fb_info.mi_wide_l2;
        int vsize = nvb << fb_info.mi_high_l2;
        Ptr<ushort> top_linebuf = fb_info.top_linebuf[plane];
        Ptr<ushort> bot_linebuf = fb_info.bot_linebuf[plane];
        int bot_offset = (vsize + CDEF_VBORDER) * CDEF_BSTRIDE;
        int stride =
            luma_stride >> (plane == AOM_PLANE_Y ? 0 : cm.seq_params.subsampling_x);

        if (fbc == nhfb - 1)
            cend = hsize;
        else
            cend = hsize + CDEF_HBORDER;

        if (fbr == nvfb - 1)
            rend = vsize;
        else
            rend = vsize + CDEF_VBORDER;

        /* Copy in the pixels we need from the current superblock for
        deringing.*/
        av1_cdef_copy_sb8_16(
            cm, src + (CDEF_VBORDER * CDEF_BSTRIDE + CDEF_HBORDER + cstart),
            CDEF_BSTRIDE, fb_info.dst, fb_info.roffset, fb_info.coffset + cstart,
            fb_info.dst_stride, vsize, cend - cstart);

        /* Copy in the pixels we need for the current superblock from bottom buffer.*/
        if (fbr < nvfb - 1)
        {
            copy_rect(src + (bot_offset + CDEF_HBORDER), CDEF_BSTRIDE,
                      bot_linebuf + fb_info.coffset, stride, CDEF_VBORDER, hsize);
        }
        else
        {
            fill_rect(src + (bot_offset + CDEF_HBORDER), CDEF_BSTRIDE, CDEF_VBORDER,
                      hsize, CDEF_VERY_LARGE);
        }
        if (fbr < nvfb - 1 && fbc > 0)
        {
            copy_rect(src + bot_offset, CDEF_BSTRIDE,
                      bot_linebuf + (fb_info.coffset - CDEF_HBORDER), stride,
                      CDEF_VBORDER, CDEF_HBORDER);
        }
        else
        {
            fill_rect(src + bot_offset, CDEF_BSTRIDE, CDEF_VBORDER, CDEF_HBORDER,
                      CDEF_VERY_LARGE);
        }
        if (fbr < nvfb - 1 && fbc < nhfb - 1)
        {
            copy_rect(src + (bot_offset + hsize + CDEF_HBORDER), CDEF_BSTRIDE,
                      bot_linebuf + (fb_info.coffset + hsize), stride, CDEF_VBORDER,
                      CDEF_HBORDER);
        }
        else
        {
            fill_rect(src + (bot_offset + hsize + CDEF_HBORDER), CDEF_BSTRIDE,
                      CDEF_VBORDER, CDEF_HBORDER, CDEF_VERY_LARGE);
        }

        /* Copy in the pixels we need from the current superblock from top buffer.*/
        if (fbr > 0)
        {
            copy_rect(src + CDEF_HBORDER, CDEF_BSTRIDE, top_linebuf + fb_info.coffset,
                      stride, CDEF_VBORDER, hsize);
        }
        else
        {
            fill_rect(src + CDEF_HBORDER, CDEF_BSTRIDE, CDEF_VBORDER, hsize,
                      CDEF_VERY_LARGE);
        }
        if (fbr > 0 && fbc > 0)
        {
            copy_rect(src, CDEF_BSTRIDE, top_linebuf + (fb_info.coffset - CDEF_HBORDER),
                      stride, CDEF_VBORDER, CDEF_HBORDER);
        }
        else
        {
            fill_rect(src, CDEF_BSTRIDE, CDEF_VBORDER, CDEF_HBORDER, CDEF_VERY_LARGE);
        }
        if (fbr > 0 && fbc < nhfb - 1)
        {
            copy_rect(src + (hsize + CDEF_HBORDER), CDEF_BSTRIDE,
                      top_linebuf + (fb_info.coffset + hsize), stride, CDEF_VBORDER,
                      CDEF_HBORDER);
        }
        else
        {
            fill_rect(src + (hsize + CDEF_HBORDER), CDEF_BSTRIDE, CDEF_VBORDER,
                      CDEF_HBORDER, CDEF_VERY_LARGE);
        }
        if (cdef_left != 0)
        {
            /* If we deringed the superblock on the left then we need to copy in
            saved pixels. */
            copy_rect(src, CDEF_BSTRIDE, colbuf[plane], CDEF_HBORDER,
                      rend + CDEF_VBORDER, CDEF_HBORDER);
        }
        /* Saving pixels in case we need to dering the superblock on the
        right. */
        copy_rect(colbuf[plane], CDEF_HBORDER, src + hsize, CDEF_BSTRIDE,
                  rend + CDEF_VBORDER, CDEF_HBORDER);

        if (fb_info.frame_boundary[LEFT] != 0)
        {
            fill_rect(src, CDEF_BSTRIDE, vsize + 2 * CDEF_VBORDER, CDEF_HBORDER,
                      CDEF_VERY_LARGE);
        }
        if (fb_info.frame_boundary[RIGHT] != 0)
        {
            fill_rect(src + (hsize + CDEF_HBORDER), CDEF_BSTRIDE,
                      vsize + 2 * CDEF_VBORDER, CDEF_HBORDER, CDEF_VERY_LARGE);
        }
    }

    private static void cdef_filter_fb(CdefBlockInfo fb_info, int plane,
                                       byte use_highbitdepth)
    {
        int offset = fb_info.dst_stride * fb_info.roffset + fb_info.coffset;
        if (use_highbitdepth != 0)
        {
            // [subset] CONVERT_TO_SHORTPTR destination: high bitdepth is not in the subset.
            throw new NotSupportedException("cdef_filter_fb: high bitdepth");
        }
        else
        {
            av1_cdef_filter_fb(
                fb_info.dst + offset, Ptr<ushort>.Null, fb_info.dst_stride,
                fb_info.src + (CDEF_VBORDER * CDEF_BSTRIDE + CDEF_HBORDER),
                fb_info.xdec, fb_info.ydec, fb_info.dir, Ptr<int>.Null, fb_info.var, plane,
                fb_info.dlist, fb_info.cdef_count, fb_info.level,
                fb_info.sec_strength, fb_info.damping, fb_info.coeff_shift);
        }
    }

    // Initializes block-level parameters for CDEF.
    private static void cdef_init_fb_col(MACROBLOCKD xd,
                                         CdefBlockInfo fb_info, Ptr<int> level,
                                         Ptr<int> sec_strength, int fbc, int fbr,
                                         int plane)
    {
        PLANE_TYPE plane_type = get_plane_type(plane);
        fb_info.level = level[plane_type];
        fb_info.sec_strength = sec_strength[plane_type];
        fb_info.dst = xd.plane[plane].dst.buf;
        fb_info.dst_stride = xd.plane[plane].dst.stride;

        fb_info.xdec = xd.plane[plane].subsampling_x;
        fb_info.ydec = xd.plane[plane].subsampling_y;
        fb_info.mi_wide_l2 = MI_SIZE_LOG2 - xd.plane[plane].subsampling_x;
        fb_info.mi_high_l2 = MI_SIZE_LOG2 - xd.plane[plane].subsampling_y;
        fb_info.roffset = MI_SIZE_64X64 * fbr << fb_info.mi_high_l2;
        fb_info.coffset = MI_SIZE_64X64 * fbc << fb_info.mi_wide_l2;
    }

    // [port] Per-thread scratch for the function-local arrays of cdef_fb_col / av1_cdef_fb_row
    // (PORTING 4: neither function is re-entered on a thread while the buffers are live).
    private sealed class cdef_fb_scratch
    {
        public readonly CdefBlockInfo fb_info = new();
        public readonly int[] cdef_left = new int[MAX_MB_PLANE];
        public readonly int[] is_zero_level = new int[PLANE_TYPES];
        public readonly int[] level = new int[PLANE_TYPES];
        public readonly int[] sec_strength = new int[PLANE_TYPES];
    }

    [ThreadStatic] private static cdef_fb_scratch s_fb_scratch;

    private static cdef_fb_scratch get_fb_scratch() => s_fb_scratch ??= new cdef_fb_scratch();

    private static void cdef_fb_col(AV1_COMMON cm, MACROBLOCKD xd,
                                    CdefBlockInfo fb_info, Ptr<ushort>[] colbuf,
                                    Ptr<int> cdef_left, int fbc, int fbr)
    {
        CommonModeInfoParams mi_params = cm.mi_params;
        MB_MODE_INFO mbmi_tl =
            mi_params.mi_grid_base[MI_SIZE_64X64 * fbr * mi_params.mi_stride +
                                   MI_SIZE_64X64 * fbc];
        // [port] C reads ->cdef_strength before the NULL check below; a NULL entry would have
        // crashed there, so the read is guarded here only to keep the C# exception-free.
        int mbmi_cdef_strength = mbmi_tl != null ? mbmi_tl.cdef_strength : 0;
        int num_planes = av1_num_planes(cm);
        cdef_fb_scratch scratch = get_fb_scratch();
        int[] is_zero_level = scratch.is_zero_level;
        int[] level = scratch.level;
        int[] sec_strength = scratch.sec_strength;
        is_zero_level[0] = 1; is_zero_level[1] = 1;
        level[0] = 0; level[1] = 0;
        sec_strength[0] = 0; sec_strength[1] = 0;
        CdefInfo cdef_info = cm.cdef_info;

        if (mbmi_tl == null || mbmi_cdef_strength == -1)
        {
            av1_zero_array(cdef_left, num_planes);
            return;
        }

        // Compute level and secondary strength for planes
        level[PLANE_TYPE_Y] =
            cdef_info.cdef_strengths[mbmi_cdef_strength] / CDEF_SEC_STRENGTHS;
        sec_strength[PLANE_TYPE_Y] =
            cdef_info.cdef_strengths[mbmi_cdef_strength] % CDEF_SEC_STRENGTHS;
        sec_strength[PLANE_TYPE_Y] += B(sec_strength[PLANE_TYPE_Y] == 3);
        is_zero_level[PLANE_TYPE_Y] =
            B((level[PLANE_TYPE_Y] == 0) && (sec_strength[PLANE_TYPE_Y] == 0));

        if (num_planes > 1)
        {
            level[PLANE_TYPE_UV] =
                cdef_info.cdef_uv_strengths[mbmi_cdef_strength] / CDEF_SEC_STRENGTHS;
            sec_strength[PLANE_TYPE_UV] =
                cdef_info.cdef_uv_strengths[mbmi_cdef_strength] % CDEF_SEC_STRENGTHS;
            sec_strength[PLANE_TYPE_UV] += B(sec_strength[PLANE_TYPE_UV] == 3);
            is_zero_level[PLANE_TYPE_UV] =
                B((level[PLANE_TYPE_UV] == 0) && (sec_strength[PLANE_TYPE_UV] == 0));
        }

        if (is_zero_level[PLANE_TYPE_Y] != 0 && is_zero_level[PLANE_TYPE_UV] != 0)
        {
            av1_zero_array(cdef_left, num_planes);
            return;
        }

        fb_info.cdef_count = av1_cdef_compute_sb_list(mi_params, fbr * MI_SIZE_64X64,
                                                      fbc * MI_SIZE_64X64,
                                                      fb_info.dlist, BLOCK_64X64);
        if (fb_info.cdef_count == 0)
        {
            av1_zero_array(cdef_left, num_planes);
            return;
        }

        for (int plane = 0; plane < num_planes; plane++)
        {
            // Do not skip cdef filtering for luma plane as filter direction is
            // computed based on luma.
            if (plane != 0 && is_zero_level[get_plane_type(plane)] != 0)
            {
                cdef_left[plane] = 0;
                continue;
            }
            cdef_init_fb_col(xd, fb_info, level, sec_strength, fbc, fbr, plane);
            cdef_prepare_fb(cm, fb_info, colbuf, cdef_left[plane], fbc, fbr, plane);
            cdef_filter_fb(fb_info, plane, cm.seq_params.use_highbitdepth);
            cdef_left[plane] = 1;
        }
    }

    // Initializes row-level parameters for CDEF frame.
    internal static void av1_cdef_init_fb_row(AV1_COMMON cm,
                                              MACROBLOCKD xd,
                                              CdefBlockInfo fb_info,
                                              Ptr<ushort>[] linebuf, Ptr<ushort> src,
                                              AV1CdefSyncData cdef_sync, int fbr)
    {
        int num_planes = av1_num_planes(cm);
        int nvfb = (cm.mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        int luma_stride =
            ALIGN_POWER_OF_TWO(cm.mi_params.mi_cols << MI_SIZE_LOG2, 4);
        int ping_pong = fbr & 1;
        // for the current filter block, it's top left corner mi structure (mi_tl)
        // is first accessed to check whether the top and left boundaries are
        // frame boundaries. Then bottom-left and top-right mi structures are
        // accessed to check whether the bottom and right boundaries
        // (respectively) are frame boundaries.
        //
        // Note that we can't just check the bottom-right mi structure - eg. if
        // we're at the right-hand edge of the frame but not the bottom, then
        // the bottom-right mi is NULL but the bottom-left is not.
        fb_info.frame_boundary[TOP] = (MI_SIZE_64X64 * fbr == 0) ? 1 : 0;
        if (fbr != nvfb - 1)
            fb_info.frame_boundary[BOTTOM] =
                (MI_SIZE_64X64 * (fbr + 1) == cm.mi_params.mi_rows) ? 1 : 0;
        else
            fb_info.frame_boundary[BOTTOM] = 1;

        fb_info.src = src;
        fb_info.damping = cm.cdef_info.cdef_damping;
        fb_info.coeff_shift = AOMMAX(cm.seq_params.bit_depth - 8, 0);
        av1_zero(fb_info.dir);
        av1_zero(fb_info.var);

        for (int plane = 0; plane < num_planes; plane++)
        {
            int mi_high_l2 = MI_SIZE_LOG2 - xd.plane[plane].subsampling_y;
            int offset = MI_SIZE_64X64 * (fbr + 1) << mi_high_l2;
            int stride = luma_stride >> xd.plane[plane].subsampling_x;
            // here ping-pong buffers are maintained for top linebuf
            // to avoid linebuf over-write by consecutive row.
            Ptr<ushort> top_linebuf =
                linebuf[plane] + (ping_pong * CDEF_VBORDER * stride);
            fb_info.bot_linebuf[plane] = linebuf[plane] + ((CDEF_VBORDER << 1) * stride);

            if (fbr != nvfb - 1)  // top line buffer copy
                av1_cdef_copy_sb8_16(cm, top_linebuf, stride, xd.plane[plane].dst.buf,
                                     offset - CDEF_VBORDER, 0,
                                     xd.plane[plane].dst.stride, CDEF_VBORDER, stride);
            fb_info.top_linebuf[plane] =
                linebuf[plane] + ((ping_pong ^ 1) * CDEF_VBORDER * stride);

            if (fbr != nvfb - 1)  // bottom line buffer copy
                av1_cdef_copy_sb8_16(cm, fb_info.bot_linebuf[plane], stride,
                                     xd.plane[plane].dst.buf, offset, 0,
                                     xd.plane[plane].dst.stride, CDEF_VBORDER, stride);
        }
    }

    internal static void av1_cdef_fb_row(AV1_COMMON cm, MACROBLOCKD xd,
                                         Ptr<ushort>[] linebuf, Ptr<ushort>[] colbuf,
                                         Ptr<ushort> src, int fbr,
                                         cdef_init_fb_row_t cdef_init_fb_row_fn,
                                         AV1CdefSyncData cdef_sync,
                                         aom_internal_error_info error_info)
    {
        // TODO(aomedia:3276): Pass error_info to the low-level functions as required
        // in future to handle error propagation.
        cdef_fb_scratch scratch = get_fb_scratch();
        CdefBlockInfo fb_info = scratch.fb_info;
        int[] cdef_left = scratch.cdef_left;
        cdef_left[0] = 1; cdef_left[1] = 1; cdef_left[2] = 1;
        int nhfb = (cm.mi_params.mi_cols + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;

        cdef_init_fb_row_fn(cm, xd, fb_info, linebuf, src, cdef_sync, fbr);
        if (cdef_sync != null && cm.cdef_info.allocated_num_workers > 1)
        {
            pthread_mutex_lock(cdef_sync.mutex_);
            bool cdef_mt_exit = cdef_sync.cdef_mt_exit;
            pthread_mutex_unlock(cdef_sync.mutex_);
            // Exit in case any worker has encountered an error.
            if (cdef_mt_exit) return;
        }
        for (int fbc = 0; fbc < nhfb; fbc++)
        {
            fb_info.frame_boundary[LEFT] = (MI_SIZE_64X64 * fbc == 0) ? 1 : 0;
            if (fbc != nhfb - 1)
                fb_info.frame_boundary[RIGHT] =
                    (MI_SIZE_64X64 * (fbc + 1) == cm.mi_params.mi_cols) ? 1 : 0;
            else
                fb_info.frame_boundary[RIGHT] = 1;
            cdef_fb_col(cm, xd, fb_info, colbuf, cdef_left, fbc, fbr);
        }
    }

    // Perform CDEF on input frame.
    // Inputs:
    //   frame: Pointer to input frame buffer.
    //   cm: Pointer to common structure.
    //   xd: Pointer to common current coding block structure.
    // Returns:
    //   Nothing will be returned.
    internal static void av1_cdef_frame(YV12_BUFFER_CONFIG frame, AV1_COMMON cm,
                                        MACROBLOCKD xd, cdef_init_fb_row_t cdef_init_fb_row_fn)
    {
        int num_planes = av1_num_planes(cm);
        int nvfb = (cm.mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;

        av1_setup_dst_planes(xd.plane, cm.seq_params.sb_size, frame, 0, 0, 0,
                             num_planes);

        for (int fbr = 0; fbr < nvfb; fbr++)
            av1_cdef_fb_row(cm, xd, cm.cdef_info.linebuf, cm.cdef_info.colbuf,
                            cm.cdef_info.srcbuf, fbr, cdef_init_fb_row_fn, null,
                            xd.error_info);
    }

    // ---- RTCD dispatch (av1_rtcd.h, pure-C build) ------------------------------------------
    internal static void cdef_copy_rect8_8bit_to_16bit(Ptr<ushort> dst, int dstride, Ptr<byte> src, int sstride, int width, int height) => cdef_copy_rect8_8bit_to_16bit_c(dst, dstride, src, sstride, width, height);
}
