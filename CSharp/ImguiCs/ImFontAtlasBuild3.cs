// Port of Dear ImGui font atlas build machinery, part 3: packing, texture sizing,
// baked glyph loading, stb_truetype loader glue, text measurement.
using System;
using System.Collections.Generic;

namespace ImGui;

public partial class ImFontAtlas
{
    public static void ImFontAtlasTextureRepack(ImFontAtlas atlas, int w, int h)
    {
        var builder = atlas.Builder;
        builder.LockDisableResize = true;

        ImTextureData old_tex = atlas.TexData;
        ImTextureData new_tex = ImFontAtlasTextureAdd(atlas, w, h);
        new_tex.UseColors = old_tex.UseColors;

        ImFontAtlasPackInit(atlas);
        var old_rects = builder.Rects;
        var old_index = builder.RectsIndex;
        builder.Rects = new ImVector<ImTextureRect>();

        bool failed = false;
        for (int i = 0; i < builder.RectsIndex.Size; i++)
        {
            var index_entry = builder.RectsIndex[i];
            if (!index_entry.IsUsed)
                continue;
            var old_r = old_rects[index_entry.TargetIndex];
            if (old_r.w == 0 && old_r.h == 0)
                continue;
            int new_r_id = ImFontAtlasPackAddRect(atlas, old_r.w, old_r.h, index_entry, i);
            if (new_r_id == ImUtil.ImFontAtlasRectId_Invalid)
            {
                failed = true;
                break;
            }
            var new_r = ImFontAtlasPackGetRect(atlas, new_r_id).Value;
            ImFontAtlasTextureBlockCopy(old_tex, old_r.x, old_r.y, new_tex, new_r.x, new_r.y, new_r.w, new_r.h);
        }
        if (failed)
        {
            // Undo, grow texture and try repacking again.
            new_tex.WantDestroyNextFrame = true;
            builder.Rects = old_rects;
            builder.RectsIndex = old_index;
            ImFontAtlasBuildSetTexture(atlas, old_tex);
            builder.LockDisableResize = false;
            ImFontAtlasTextureGrow(atlas, w, h);
            return;
        }
        builder.RectsDiscardedCount = 0;
        builder.RectsDiscardedSurface = 0;

        // Patch glyphs UV
        for (int baked_n = 0; baked_n < builder.BakedPool.Size; baked_n++)
            for (int g = 0; g < builder.BakedPool[baked_n].Glyphs.Size; g++)
            {
                var glyph = builder.BakedPool[baked_n].Glyphs[g];
                if (glyph.PackId != ImUtil.ImFontAtlasRectId_Invalid)
                {
                    var r = ImFontAtlasPackGetRect(atlas, glyph.PackId).Value;
                    glyph.U0 = (r.x) * atlas.TexUvScale.x;
                    glyph.V0 = (r.y) * atlas.TexUvScale.y;
                    glyph.U1 = (r.x + r.w) * atlas.TexUvScale.x;
                    glyph.V1 = (r.y + r.h) * atlas.TexUvScale.y;
                }
            }

        ImFontAtlasBuildUpdateTexData(atlas);

        builder.LockDisableResize = false;
        ImFontAtlasUpdateDrawListsSharedData(atlas);
    }

    public static void ImFontAtlasTextureGrow(ImFontAtlas atlas, int old_tex_w = -1, int old_tex_h = -1)
    {
        var builder = atlas.Builder;
        if (old_tex_w == -1)
            old_tex_w = atlas.TexData.Width;
        if (old_tex_h == -1)
            old_tex_h = atlas.TexData.Height;

        // Grow texture so it follows roughly a square.
        int new_tex_w = (old_tex_h <= old_tex_w) ? old_tex_w : old_tex_w * 2;
        int new_tex_h = (old_tex_h <= old_tex_w) ? old_tex_h * 2 : old_tex_h;

        int pack_padding = atlas.TexGlyphPadding;
        new_tex_w = Math.Max(new_tex_w, ImMath.ImUpperPowerOfTwo(builder.MaxRectSize.x + pack_padding));
        new_tex_h = Math.Max(new_tex_h, ImMath.ImUpperPowerOfTwo(builder.MaxRectSize.y + pack_padding));
        new_tex_w = ImMath.ImClamp(new_tex_w, atlas.TexMinWidth, atlas.TexMaxWidth);
        new_tex_h = ImMath.ImClamp(new_tex_h, atlas.TexMinHeight, atlas.TexMaxHeight);
        if (new_tex_w == old_tex_w && new_tex_h == old_tex_h)
            return;

        ImFontAtlasTextureRepack(atlas, new_tex_w, new_tex_h);
    }

    public static void ImFontAtlasTextureMakeSpace(ImFontAtlas atlas)
    {
        var builder = atlas.Builder;
        ImFontAtlasBuildDiscardBakes(atlas, 2);

        if (builder.RectsDiscardedSurface < builder.RectsPackedSurface * 0.20f)
            ImFontAtlasTextureGrow(atlas);
        else
            ImFontAtlasTextureRepack(atlas, atlas.TexData.Width, atlas.TexData.Height);
    }

    public static ImVec2i ImFontAtlasTextureGetSizeEstimate(ImFontAtlas atlas)
    {
        int min_w = ImMath.ImUpperPowerOfTwo(atlas.TexMinWidth);
        int min_h = ImMath.ImUpperPowerOfTwo(atlas.TexMinHeight);
        if (atlas.Builder == null || atlas.TexData == null || atlas.TexData.Status == ImTextureStatus_.ImTextureStatus_WantDestroy)
            return new ImVec2i(min_w, min_h);

        var builder = atlas.Builder;
        min_w = Math.Max(ImMath.ImUpperPowerOfTwo(builder.MaxRectSize.x), min_w);
        min_h = Math.Max(ImMath.ImUpperPowerOfTwo(builder.MaxRectSize.y), min_h);
        int surface_approx = builder.RectsPackedSurface - builder.RectsDiscardedSurface;
        int surface_sqrt = (int)Math.Sqrt((float)surface_approx);

        int new_tex_w;
        int new_tex_h;
        if (min_w >= min_h)
        {
            new_tex_w = Math.Max(min_w, ImMath.ImUpperPowerOfTwo(surface_sqrt));
            new_tex_h = Math.Max(min_h, (surface_approx + new_tex_w - 1) / new_tex_w);
            if ((atlas.Flags & ImFontAtlasFlags_.ImFontAtlasFlags_NoPowerOfTwoHeight) == 0)
                new_tex_h = ImMath.ImUpperPowerOfTwo(new_tex_h);
        }
        else
        {
            new_tex_h = Math.Max(min_h, ImMath.ImUpperPowerOfTwo(surface_sqrt));
            if ((atlas.Flags & ImFontAtlasFlags_.ImFontAtlasFlags_NoPowerOfTwoHeight) == 0)
                new_tex_h = ImMath.ImUpperPowerOfTwo(new_tex_h);
            new_tex_w = Math.Max(min_w, (surface_approx + new_tex_h - 1) / new_tex_h);
        }

        return new ImVec2i(new_tex_w, new_tex_h);
    }

    // Clear all output. Invalidates all AddCustomRect() return values!
    public static void ImFontAtlasBuildClear(ImFontAtlas atlas)
    {
        ImVec2i new_tex_size = ImFontAtlasTextureGetSizeEstimate(atlas);
        ImFontAtlasBuildDestroy(atlas);
        ImFontAtlasTextureAdd(atlas, new_tex_size.x, new_tex_size.y);
        ImFontAtlasBuildInit(atlas);
        for (int i = 0; i < atlas.Sources.Size; i++)
            ImFontAtlasFontSourceInit(atlas, atlas.Sources[i]);
        for (int i = 0; i < atlas.Fonts.Size; i++)
            for (int j = 0; j < atlas.Fonts[i].Sources.Size; j++)
                ImFontAtlasFontSourceAddToFont(atlas, atlas.Fonts[i], atlas.Fonts[i].Sources[j]);
    }

    public static void ImFontAtlasTextureCompact(ImFontAtlas atlas)
    {
        var builder = atlas.Builder;
        ImFontAtlasBuildDiscardBakes(atlas, 1);

        ImTextureData old_tex = atlas.TexData;
        ImVec2i old_tex_size = new ImVec2i(old_tex.Width, old_tex.Height);
        ImVec2i new_tex_size = ImFontAtlasTextureGetSizeEstimate(atlas);
        if (builder.RectsDiscardedCount == 0 && new_tex_size.x == old_tex_size.x && new_tex_size.y == old_tex_size.y)
            return;

        ImFontAtlasTextureRepack(atlas, new_tex_size.x, new_tex_size.y);
    }

    // Start packing over current empty texture
    public static void ImFontAtlasBuildInit(ImFontAtlas atlas)
    {
        if (atlas.FontLoader == null)
        {
            atlas.SetFontLoader(ImFontAtlasGetFontLoaderForStbTruetype());
        }

        if (atlas.TexData == null || atlas.TexData.Pixels == null)
            ImFontAtlasTextureAdd(atlas, ImMath.ImUpperPowerOfTwo(atlas.TexMinWidth), ImMath.ImUpperPowerOfTwo(atlas.TexMinHeight));

        atlas.Builder = new ImFontAtlasBuilder();

        ImFontAtlasBuildUpdateRendererHasTexturesFromContext(atlas);

        ImFontAtlasPackInit(atlas);

        ImFontAtlasBuildUpdateTexData(atlas);

        ImFontAtlasBuildUpdatePointers(atlas);

        ImFontAtlasUpdateDrawListsSharedData(atlas);

        ImTextInitClassifiers();
    }

    // Destroy builder and all cached glyphs. Do not destroy actual fonts.
    public static void ImFontAtlasBuildDestroy(ImFontAtlas atlas)
    {
        for (int i = 0; i < atlas.Fonts.Size; i++)
            ImFontAtlasFontDestroyOutput(atlas, atlas.Fonts[i]);
        atlas.Builder = null;
    }

    // ---- Packing ----

    public static void ImFontAtlasPackInit(ImFontAtlas atlas)
    {
        ImTextureData tex = atlas.TexData;
        var builder = atlas.Builder;

        int pack_node_count = tex.Width / 2;
        builder.PackNodes = new List<StbRectPack.stbrp_node>(pack_node_count);
        for (int i = 0; i < pack_node_count; i++)
            builder.PackNodes.Add(new StbRectPack.stbrp_node());
        builder.PackContext.init_target(tex.Width, tex.Height, builder.PackNodes.ToArray(), pack_node_count);
        builder.RectsPackedSurface = builder.RectsPackedCount = 0;
        builder.MaxRectSize = new ImVec2i(0, 0);
        builder.MaxRectBounds = new ImVec2i(0, 0);
    }

    static int ImFontAtlasPackAllocRectEntry(ImFontAtlas atlas, int rect_idx)
    {
        var builder = atlas.Builder;
        int index_idx;
        if (builder.RectsIndexFreeListStart < 0)
        {
            builder.RectsIndex.resize(builder.RectsIndex.Size + 1);
            index_idx = builder.RectsIndex.Size - 1;
            builder.RectsIndex.Data[index_idx] = default;
        }
        else
        {
            index_idx = builder.RectsIndexFreeListStart;
            var e = builder.RectsIndex.Data[index_idx];
            builder.RectsIndexFreeListStart = e.TargetIndex;
        }
        var entry = builder.RectsIndex.Data[index_idx];
        entry.TargetIndex = rect_idx;
        entry.IsUsed = true;
        builder.RectsIndex.Data[index_idx] = entry;
        return ImUtil.ImFontAtlasRectId_Make(index_idx, (int)entry.Generation);
    }

    static int ImFontAtlasPackReuseRectEntry(ImFontAtlas atlas, ref ImFontAtlasRectEntry index_entry, int index_idx)
    {
        index_entry.TargetIndex = atlas.Builder.Rects.Size - 1;
        atlas.Builder.RectsIndex.Data[index_idx] = index_entry;
        return ImUtil.ImFontAtlasRectId_Make(index_idx, (int)index_entry.Generation);
    }

    public static void ImFontAtlasPackDiscardRect(ImFontAtlas atlas, int id)
    {
        var rect_opt = ImFontAtlasPackGetRect(atlas, id);
        if (!rect_opt.HasValue)
            return;
        var rect = rect_opt.Value;

        var builder = atlas.Builder;
        int index_idx = ImUtil.ImFontAtlasRectId_GetIndex(id);
        var index_entry = builder.RectsIndex.Data[index_idx];
        index_entry.IsUsed = false;
        index_entry.TargetIndex = builder.RectsIndexFreeListStart;
        index_entry.Generation++;
        if (index_entry.Generation == 0)
            index_entry.Generation++;

        int pack_padding = atlas.TexGlyphPadding;
        builder.RectsIndexFreeListStart = index_idx;
        builder.RectsDiscardedCount++;
        builder.RectsDiscardedSurface += (rect.w + pack_padding) * (rect.h + pack_padding);
        rect.w = rect.h = 0;
        builder.Rects.Data[index_entry.TargetIndex] = rect;
        builder.RectsIndex.Data[index_idx] = index_entry;
    }

    public static int ImFontAtlasPackAddRect(ImFontAtlas atlas, int w, int h)
    {
        return ImFontAtlasPackAddRect(atlas, w, h, null, -1);
    }

    public static int ImFontAtlasPackAddRect(ImFontAtlas atlas, int w, int h, ImFontAtlasRectEntry? overwrite_entry, int overwrite_index)
    {
        var builder = atlas.Builder;
        int pack_padding = atlas.TexGlyphPadding;
        builder.MaxRectSize.x = Math.Max(builder.MaxRectSize.x, w);
        builder.MaxRectSize.y = Math.Max(builder.MaxRectSize.y, h);

        var r = new ImTextureRect(0, 0, w, h);
        for (int attempts_remaining = 3; attempts_remaining >= 0; attempts_remaining--)
        {
            // C++: stbrp_rect pack_r = {}; pack_r.w = w + pack_padding; pack_r.h = h + pack_padding;
            //      stbrp_pack_rects(&builder->PackContext, &pack_r, 1);
            // (mutate the array element in place — do NOT copy the struct out of the array)
            var pack_arr = new StbRectPack.stbrp_rect[1];
            pack_arr[0].w = w + pack_padding;
            pack_arr[0].h = h + pack_padding;
            builder.PackContext.pack_rects(pack_arr, 1);
            var pack_r = pack_arr[0];
            r.x = (ushort)pack_r.x;
            r.y = (ushort)pack_r.y;
            if (pack_r.was_packed != 0)
                break;

            if (attempts_remaining == 0 || builder.LockDisableResize)
                return ImUtil.ImFontAtlasRectId_Invalid;

            ImFontAtlasTextureMakeSpace(atlas);
        }

        builder.MaxRectBounds.x = Math.Max(builder.MaxRectBounds.x, r.x + r.w + pack_padding);
        builder.MaxRectBounds.y = Math.Max(builder.MaxRectBounds.y, r.y + r.h + pack_padding);
        builder.RectsPackedCount++;
        builder.RectsPackedSurface += (w + pack_padding) * (h + pack_padding);

        builder.Rects.push_back(r);
        if (overwrite_entry.HasValue)
        {
            var entry_copy = overwrite_entry.Value;
            return ImFontAtlasPackReuseRectEntry(atlas, ref entry_copy, overwrite_index);
        }
        else
            return ImFontAtlasPackAllocRectEntry(atlas, builder.Rects.Size - 1);
    }

    public static ImTextureRect? ImFontAtlasPackGetRect(ImFontAtlas atlas, int id)
    {
        int index_idx = ImUtil.ImFontAtlasRectId_GetIndex(id);
        var builder = atlas.Builder;
        var index_entry = builder.RectsIndex.Data[index_idx];
        return builder.Rects.Data[index_entry.TargetIndex];
    }

    public static ImTextureRect? ImFontAtlasPackGetRectSafe(ImFontAtlas atlas, int id)
    {
        if (id == ImUtil.ImFontAtlasRectId_Invalid)
            return null;
        int index_idx = ImUtil.ImFontAtlasRectId_GetIndex(id);
        if (atlas.Builder == null)
            ImFontAtlasBuildInit(atlas);
        var builder = atlas.Builder;
        if (index_idx >= builder.RectsIndex.Size)
            return null;
        var index_entry = builder.RectsIndex.Data[index_idx];
        if (index_entry.Generation != ImUtil.ImFontAtlasRectId_GetGeneration(id) || !index_entry.IsUsed)
            return null;
        return builder.Rects.Data[index_entry.TargetIndex];
    }

    static bool ImFontAtlasBuildAcceptCodepointForSource(ImFontConfig src, ushort codepoint)
    {
        if (src.GlyphExcludeRanges != null)
            for (int i = 0; i + 1 < src.GlyphExcludeRanges.Length && src.GlyphExcludeRanges[i] != 0; i += 2)
                if (codepoint >= src.GlyphExcludeRanges[i] && codepoint <= src.GlyphExcludeRanges[i + 1])
                    return false;
        return true;
    }

    public static ushort ImFontAtlas_FontHookRemapCodepoint(ImFontAtlas atlas, ImFont font, ushort c_in)
    {
        if (font.RemapPairs.Data.Size != 0)
            return (ushort)font.RemapPairs.GetInt((uint)c_in, (int)c_in);
        return c_in;
    }

    public static void ImFontBaked_BuildGrowIndex(ImFontBaked baked, int new_size)
    {
        if (new_size <= baked.IndexLookup.Size)
            return;
        baked.IndexAdvanceX.resize(new_size, -1.0f);
        baked.IndexLookup.resize(new_size, ImUtil.IM_FONTGLYPH_INDEX_UNUSED);
    }

    public static ImFontGlyph ImFontBaked_BuildLoadGlyph(ImFontBaked baked, ushort codepoint, float[] only_load_advance_x)
    {
        ImFont font = baked.OwnerFont;
        ImFontAtlas atlas = font.OwnerAtlas;
        if (atlas.Locked || (font.Flags & ImFontFlags_.ImFontFlags_NoLoadGlyphs) != 0)
        {
            if (baked.FallbackGlyphIndex == -1 && !baked.LoadNoFallback)
                ImFontAtlasBuildSetupFontBakedFallback(baked);
            return null;
        }

        // User remapping hooks
        ushort src_codepoint = codepoint;
        codepoint = ImFontAtlas_FontHookRemapCodepoint(atlas, font, codepoint);

        // Special hook
        if (codepoint == font.EllipsisChar && font.EllipsisAutoBake)
        {
            ImFontGlyph ellipsis_glyph = ImFontAtlasBuildSetupFontBakedEllipsis(atlas, baked);
            if (ellipsis_glyph != null)
                return ellipsis_glyph;
        }

        // Call backend
        int src_n = 0;
        for (int si = 0; si < font.Sources.Size; si++)
        {
            var src = font.Sources[si];
            var loader = src.FontLoader ?? atlas.FontLoader;
            if (src.GlyphExcludeRanges == null || ImFontAtlasBuildAcceptCodepointForSource(src, codepoint))
            {
                if (only_load_advance_x == null)
                {
                    var glyph_buf = new ImFontGlyph();
                    if (loader.FontBakedLoadGlyph(atlas, src, baked, null, codepoint, glyph_buf, null))
                    {
                        glyph_buf.Codepoint = src_codepoint;
                        glyph_buf.SourceIdx = (uint)src_n;
                        return ImFontAtlasBakedAddFontGlyph(atlas, baked, src, glyph_buf);
                    }
                }
                else
                {
                    float[] adv = new float[1];
                    if (loader.FontBakedLoadGlyph(atlas, src, baked, null, codepoint, null, adv))
                    {
                        ImFontAtlasBakedAddFontGlyphAdvancedX(atlas, baked, src, codepoint, adv[0]);
                        only_load_advance_x[0] = adv[0];
                        return null;
                    }
                }
            }
            src_n++;
        }

        // Lazily load fallback glyph
        if (baked.LoadNoFallback)
            return null;
        if (baked.FallbackGlyphIndex == -1)
            ImFontAtlasBuildSetupFontBakedFallback(baked);

        // Mark index as not found, so we don't attempt the search twice
        ImFontBaked_BuildGrowIndex(baked, codepoint + 1);
        baked.IndexAdvanceX.Data[codepoint] = baked.FallbackAdvanceX;
        baked.IndexLookup.Data[codepoint] = ImUtil.IM_FONTGLYPH_INDEX_NOT_FOUND;
        return null;
    }

    public static float ImFontBaked_BuildLoadGlyphAdvanceX(ImFontBaked baked, ushort codepoint)
    {
        if (baked.Size >= ImGuiCore.IMGUI_FONT_SIZE_THRESHOLD_FOR_LOADADVANCEXONLYMODE || baked.LoadNoRenderOnLayout)
        {
            float[] only_advance_x = new float[1];
            ImFontGlyph glyph = ImFontBaked_BuildLoadGlyph(baked, codepoint, only_advance_x);
            return glyph != null ? glyph.AdvanceX : only_advance_x[0];
        }
        else
        {
            ImFontGlyph glyph = ImFontBaked_BuildLoadGlyph(baked, codepoint, null);
            return glyph != null ? glyph.AdvanceX : baked.FallbackAdvanceX;
        }
    }

    public static float BuildLoadGlyphGetAdvanceOrFallback(ImFontBaked baked, uint codepoint)
    {
        return ImFontBaked_BuildLoadGlyphAdvanceX(baked, (ushort)codepoint);
    }

    // Port of ImFontAtlasBakedAddFontGlyph
    public static ImFontGlyph ImFontAtlasBakedAddFontGlyph(ImFontAtlas atlas, ImFontBaked baked, ImFontConfig src, ImFontGlyph in_glyph)
    {
        int glyph_idx = baked.Glyphs.Size;
        var glyph = in_glyph.Clone();
        baked.Glyphs.push_back(glyph);

        // Set UV from packed rectangle
        if (glyph.PackId != ImUtil.ImFontAtlasRectId_Invalid)
        {
            var r = ImFontAtlasPackGetRect(atlas, glyph.PackId).Value;
            glyph.U0 = (r.x) * atlas.TexUvScale.x;
            glyph.V0 = (r.y) * atlas.TexUvScale.y;
            glyph.U1 = (r.x + r.w) * atlas.TexUvScale.x;
            glyph.V1 = (r.y + r.h) * atlas.TexUvScale.y;
            baked.MetricsTotalSurface += (uint)(r.w * r.h);
        }

        if (src != null)
        {
            // Clamp & recenter if needed
            float ref_size = baked.OwnerFont.Sources[0].SizePixels;
            float offsets_scale = (ref_size != 0.0f) ? (baked.Size / ref_size) : 1.0f;
            float advance_x = ImMath.ImClamp(glyph.AdvanceX, src.GlyphMinAdvanceX * offsets_scale, src.GlyphMaxAdvanceX * offsets_scale);
            if (advance_x != glyph.AdvanceX)
            {
                float char_off_x = src.PixelSnapH ? ImMath.ImTrunc((advance_x - glyph.AdvanceX) * 0.5f) : (advance_x - glyph.AdvanceX) * 0.5f;
                glyph.X0 += char_off_x;
                glyph.X1 += char_off_x;
            }

            // Snap to pixel
            if (src.PixelSnapH)
                advance_x = (float)(int)(advance_x + 0.5f);

            // Bake spacing
            glyph.AdvanceX = advance_x + src.GlyphExtraAdvanceX;
        }
        if (glyph.Colored != 0)
            atlas.TexPixelsUseColors = atlas.TexData.UseColors = true;

        // Update lookup tables
        int codepoint = (int)glyph.Codepoint;
        ImFontBaked_BuildGrowIndex(baked, codepoint + 1);
        baked.IndexAdvanceX.Data[codepoint] = glyph.AdvanceX;
        baked.IndexLookup.Data[codepoint] = (ushort)glyph_idx;
        int page_n = codepoint / 8192;
        baked.OwnerFont.Used8kPagesMap[page_n >> 3] |= (byte)(1 << (page_n & 7));

        return glyph;
    }

    public static void ImFontAtlasBakedAddFontGlyphAdvancedX(ImFontAtlas atlas, ImFontBaked baked, ImFontConfig src, ushort codepoint, float advance_x)
    {
        if (src != null)
        {
            float ref_size = baked.OwnerFont.Sources[0].SizePixels;
            float offsets_scale = (ref_size != 0.0f) ? (baked.Size / ref_size) : 1.0f;
            advance_x = ImMath.ImClamp(advance_x, src.GlyphMinAdvanceX * offsets_scale, src.GlyphMaxAdvanceX * offsets_scale);

            if (src.PixelSnapH)
                advance_x = (float)(int)(advance_x + 0.5f);

            advance_x += src.GlyphExtraAdvanceX;
        }

        ImFontBaked_BuildGrowIndex(baked, codepoint + 1);
        baked.IndexAdvanceX.Data[codepoint] = advance_x;
    }

    // Copy to texture, post-process and queue update for backend
    public static void ImFontAtlasBakedSetFontGlyphBitmap(ImFontAtlas atlas, ImFontBaked baked, ImFontConfig src, ImFontGlyph glyph, ImTextureRect r, byte[] src_pixels, int src_fmt, int src_pitch)
    {
        ImTextureData tex = atlas.TexData;
        System.Diagnostics.Debug.Assert(r.x + r.w <= tex.Width && r.y + r.h <= tex.Height);
        int dst_offset = tex.GetPixelsAt(r.x, r.y);
        ImFontAtlasTextureBlockConvert(src_pixels, src_fmt, src_pitch, tex.Pixels, dst_offset, tex.Format, tex.GetPitch(), r.w, r.h);
        // (pixel data post-processing operates on texture pixels only)
        ImFontAtlasPostProcessData pp_data = new ImFontAtlasPostProcessData { FontAtlas = atlas, Font = baked.OwnerFont, FontSrc = src, FontBaked = baked, Glyph = glyph, Pixels = tex.Pixels, Offset = dst_offset, Format = tex.Format, Pitch = tex.GetPitch(), Width = r.w, Height = r.h };
        ImFontAtlasTextureBlockPostProcess(pp_data);
        ImUtil.ImFontAtlasTextureBlockQueueUpload(atlas, tex, r.x, r.y, r.w, r.h);
    }

    public static uint ImFontAtlasBakedGetId(uint font_id, float baked_size, float rasterizer_density)
    {
        // hash struct { ImGuiID FontId; float BakedSize; float RasterizerDensity; } (12 bytes LE)
        byte[] hashed_data = new byte[12];
        hashed_data[0] = (byte)font_id; hashed_data[1] = (byte)(font_id >> 8); hashed_data[2] = (byte)(font_id >> 16); hashed_data[3] = (byte)(font_id >> 24);
        uint bs = BitConverter.SingleToUInt32Bits(baked_size);
        uint rd = BitConverter.SingleToUInt32Bits(rasterizer_density);
        hashed_data[4] = (byte)bs; hashed_data[5] = (byte)(bs >> 8); hashed_data[6] = (byte)(bs >> 16); hashed_data[7] = (byte)(bs >> 24);
        hashed_data[8] = (byte)rd; hashed_data[9] = (byte)(rd >> 8); hashed_data[10] = (byte)(rd >> 16); hashed_data[11] = (byte)(rd >> 24);
        return ImGuiHash.ImHashData(hashed_data, 0, 12);
    }

    public static ImFontBaked ImFontAtlasBakedGetOrAdd(ImFontAtlas atlas, ImFont font, float font_size, float font_rasterizer_density)
    {
        uint baked_id = ImFontAtlasBakedGetId(font.FontId, font_size, font_rasterizer_density);
        var builder = atlas.Builder;
        var baked = (ImFontBaked)builder.BakedMap.GetVoidPtr(baked_id);
        if (baked != null)
            return baked;

        if ((font.Flags & ImFontFlags_.ImFontFlags_LockBakedSizes) != 0 || atlas.Locked)
        {
            baked = ImFontAtlasBakedGetClosestMatch(atlas, font, font_size, font_rasterizer_density);
            if (baked != null)
                return baked;
            if (atlas.Locked)
                return null;
        }

        baked = ImFontAtlasBakedAdd(atlas, font, font_size, font_rasterizer_density, baked_id);
        builder.BakedMap.SetVoidPtr(baked_id, baked);
        return baked;
    }

    public static bool ImFontAtlasGetMouseCursorTexData(ImFontAtlas atlas, int cursor_type, ref ImVec2 out_offset, ref ImVec2 out_size, ImVec2[] out_uv_border, ImVec2[] out_uv_fill)
    {
        if (cursor_type <= ImGuiMouseCursor_.ImGuiMouseCursor_None || cursor_type >= ImGuiMouseCursor_.ImGuiMouseCursor_COUNT)
            return false;
        if ((atlas.Flags & ImFontAtlasFlags_.ImFontAtlasFlags_NoMouseCursors) != 0)
            return false;

        var r = ImFontAtlasPackGetRect(atlas, atlas.Builder.PackIdMouseCursors).Value;
        ImVec2 pos = FONT_ATLAS_DEFAULT_TEX_CURSOR_DATA[cursor_type][0] + new ImVec2(r.x, r.y);
        ImVec2 size = FONT_ATLAS_DEFAULT_TEX_CURSOR_DATA[cursor_type][1];
        out_size = size;
        out_offset = FONT_ATLAS_DEFAULT_TEX_CURSOR_DATA[cursor_type][2];
        out_uv_border[0] = pos * atlas.TexUvScale;
        out_uv_border[1] = (pos + size) * atlas.TexUvScale;
        pos.x += FONT_ATLAS_DEFAULT_TEX_DATA_W + 1;
        out_uv_fill[0] = pos * atlas.TexUvScale;
        out_uv_fill[1] = (pos + size) * atlas.TexUvScale;
        return true;
    }
}
