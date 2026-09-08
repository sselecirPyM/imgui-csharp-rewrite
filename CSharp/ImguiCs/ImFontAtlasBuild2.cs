// Port of Dear ImGui font atlas build machinery, part 2: build main, packing, baked glyphs,
// stb_truetype loader glue, word-wrap, text classifiers, ranges, decompression.
using System;
using System.Collections.Generic;

namespace ImGui;

public partial class ImFontAtlas
{
    // ---- Build main / init / destroy ----

    // When atlas->RendererHasTextures = true, this is only called if no font were loaded.
    public static void ImFontAtlasBuildMain(ImFontAtlas atlas)
    {
        if (atlas.TexData != null && atlas.TexData.Format != atlas.TexDesiredFormat)
            ImFontAtlasBuildClear(atlas);

        if (atlas.Builder == null)
            ImFontAtlasBuildInit(atlas);

        // Default font if none are specified
        if (atlas.Sources.Size == 0)
            atlas.AddFontDefault();

        // [LEGACY] For backends not supporting RendererHasTextures: preload all glyphs
        ImFontAtlasBuildUpdateRendererHasTexturesFromContext(atlas);
        if (atlas.RendererHasTextures == false)
            ImFontAtlasBuildLegacyPreloadAllGlyphRanges(atlas);
        atlas.TexIsBuilt = true;
    }

    public static void ImFontAtlasBuildGetOversampleFactors(ImFontConfig src, ImFontBaked baked, out int out_oversample_h, out int out_oversample_v)
    {
        float raster_size = baked.Size * baked.RasterizerDensity * src.RasterizerDensity;
        out_oversample_h = (src.OversampleH != 0) ? src.OversampleH : (raster_size > 36.0f || src.PixelSnapH) ? 1 : 2;
        out_oversample_v = (src.OversampleV != 0) ? src.OversampleV : 1;
    }

    // Setup main font loader for the atlas
    public static void ImFontAtlasBuildSetupFontLoader(ImFontAtlas atlas, ImFontLoader font_loader)
    {
        if (atlas.FontLoader == font_loader)
            return;

        for (int i = 0; i < atlas.Fonts.Size; i++)
            ImFontAtlasFontDestroyOutput(atlas, atlas.Fonts[i]);

        atlas.FontLoader = font_loader;
        atlas.FontLoaderName = font_loader != null ? font_loader.Name : "NULL";

        if (atlas.Builder != null && atlas.FontLoader != null)
        {
        }
        for (int i = 0; i < atlas.Fonts.Size; i++)
            ImFontAtlasFontInitOutput(atlas, atlas.Fonts[i]);
        for (int i = 0; i < atlas.Fonts.Size; i++)
            for (int j = 0; j < atlas.Fonts[i].Sources.Size; j++)
                ImFontAtlasFontSourceAddToFont(atlas, atlas.Fonts[i], atlas.Fonts[i].Sources[j]);
    }

    // Preload all glyph ranges for legacy backends.
    public static void ImFontAtlasBuildLegacyPreloadAllGlyphRanges(ImFontAtlas atlas)
    {
        atlas.Builder.PreloadedAllGlyphsRanges = true;
        for (int fi = 0; fi < atlas.Fonts.Size; fi++)
        {
            var font = atlas.Fonts[fi];
            var baked = font.GetFontBaked(font.LegacySize);
            if (font.FallbackChar != 0)
                baked.FindGlyph(font.FallbackChar);
            if (font.EllipsisChar != 0)
                baked.FindGlyph(font.EllipsisChar);
            for (int si = 0; si < font.Sources.Size; si++)
            {
                var src = font.Sources[si];
                var ranges = src.GlyphRanges != null ? src.GlyphRanges : atlas.GetGlyphRangesDefault();
                for (int r = 0; r + 1 < ranges.Length && ranges[r] != 0; r += 2)
                    for (uint c = ranges[r]; c <= ranges[r + 1] && c <= ImUtil.IM_UNICODE_CODEPOINT_MAX; c++)
                        baked.FindGlyph((ushort)c);
            }
        }
    }

    public static void ImFontAtlasBuildUpdatePointers(ImFontAtlas atlas)
    {
        for (int i = 0; i < atlas.Fonts.Size; i++)
            atlas.Fonts[i].Sources.resize(0);
        for (int i = 0; i < atlas.Sources.Size; i++)
            atlas.Sources[i].DstFont.Sources.push_back(atlas.Sources.Data[i]);
    }

    // Render a white-colored bitmap encoded in a string
    public static void ImFontAtlasBuildRenderBitmapFromString(ImFontAtlas atlas, int x, int y, int w, int h, string in_str, char in_marker_char)
    {
        ImTextureData tex = atlas.TexData;

        if (tex.Format == ImTextureFormat_.ImTextureFormat_Alpha8)
        {
            int out_p = tex.GetPixelsAt(x, y);
            for (int off_y = 0; off_y < h; off_y++, out_p += tex.Width)
                for (int off_x = 0; off_x < w; off_x++)
                    tex.Pixels[out_p + off_x] = (in_str[off_y * w + off_x] == in_marker_char) ? (byte)0xFF : (byte)0x00;
        }
        else // RGBA32
        {
            int out_p = tex.GetPixelsAt(x, y);
            uint white = ImGuiCore.IM_COL32_WHITE;
            uint black_trans = ImGuiCore.IM_COL32_BLACK_TRANS;
            for (int off_y = 0; off_y < h; off_y++, out_p += tex.Width * 4)
                for (int off_x = 0; off_x < w; off_x++)
                {
                    uint col = (in_str[off_y * w + off_x] == in_marker_char) ? white : black_trans;
                    int o = out_p + off_x * 4;
                    tex.Pixels[o + 0] = (byte)(col & 0xFF);
                    tex.Pixels[o + 1] = (byte)((col >> 8) & 0xFF);
                    tex.Pixels[o + 2] = (byte)((col >> 16) & 0xFF);
                    tex.Pixels[o + 3] = (byte)((col >> 24) & 0xFF);
                }
        }
    }

    static void ImFontAtlasBuildUpdateTexDataBasic(ImFontAtlas atlas)
    {
        var builder = atlas.Builder;
        ImVec2i pack_size = ((atlas.Flags & ImFontAtlasFlags_.ImFontAtlasFlags_NoMouseCursors) != 0) ? new ImVec2i(2, 2) : new ImVec2i(FONT_ATLAS_DEFAULT_TEX_DATA_W * 2 + 1, FONT_ATLAS_DEFAULT_TEX_DATA_H);

        var r = new ImFontAtlasRect();
        bool add_and_draw = (atlas.GetCustomRect(builder.PackIdMouseCursors, ref r) == false);
        if (add_and_draw)
        {
            builder.PackIdMouseCursors = atlas.AddCustomRect(pack_size.x, pack_size.y, ref r);

            if ((atlas.Flags & ImFontAtlasFlags_.ImFontAtlasFlags_NoMouseCursors) != 0)
            {
                ImFontAtlasBuildRenderBitmapFromString(atlas, r.x, r.y, 2, 2, "XXXX", 'X');
            }
            else
            {
                int x_for_white = r.x;
                int x_for_black = r.x + FONT_ATLAS_DEFAULT_TEX_DATA_W + 1;
                ImFontAtlasBuildRenderBitmapFromString(atlas, x_for_white, r.y, FONT_ATLAS_DEFAULT_TEX_DATA_W, FONT_ATLAS_DEFAULT_TEX_DATA_H, FONT_ATLAS_DEFAULT_TEX_DATA_PIXELS, '.');
                ImFontAtlasBuildRenderBitmapFromString(atlas, x_for_black, r.y, FONT_ATLAS_DEFAULT_TEX_DATA_W, FONT_ATLAS_DEFAULT_TEX_DATA_H, FONT_ATLAS_DEFAULT_TEX_DATA_PIXELS, 'X');
            }
        }

        atlas.TexUvWhitePixel = new ImVec2((r.x + 0.5f) * atlas.TexUvScale.x, (r.y + 0.5f) * atlas.TexUvScale.y);
    }

    static void ImFontAtlasBuildUpdateTexDataLines(ImFontAtlas atlas)
    {
        if ((atlas.Flags & ImFontAtlasFlags_.ImFontAtlasFlags_NoBakedLines) != 0)
            return;

        ImTextureData tex = atlas.TexData;
        var builder = atlas.Builder;

        var r = new ImFontAtlasRect();
        bool add_and_draw = atlas.GetCustomRect(builder.PackIdLinesTexData, ref r) == false;
        if (add_and_draw)
        {
            ImVec2i pack_size = new ImVec2i(ImDrawListConst.IM_DRAWLIST_TEX_LINES_WIDTH_MAX + 2, ImDrawListConst.IM_DRAWLIST_TEX_LINES_WIDTH_MAX + 1);
            builder.PackIdLinesTexData = atlas.AddCustomRect(pack_size.x, pack_size.y, ref r);
        }

        for (int n = 0; n < ImDrawListConst.IM_DRAWLIST_TEX_LINES_WIDTH_MAX + 1; n++)
        {
            int y = n;
            int line_width = n;
            int pad_left = (r.w - line_width) / 2;
            int pad_right = r.w - (pad_left + line_width);

            if (add_and_draw && tex.Format == ImTextureFormat_.ImTextureFormat_Alpha8)
            {
                int write_ptr = tex.GetPixelsAt(r.x, r.y + y);
                for (int i = 0; i < pad_left; i++)
                    tex.Pixels[write_ptr + i] = 0x00;
                for (int i = 0; i < line_width; i++)
                    tex.Pixels[write_ptr + pad_left + i] = 0xFF;
                for (int i = 0; i < pad_right; i++)
                    tex.Pixels[write_ptr + pad_left + line_width + i] = 0x00;
            }
            else if (add_and_draw && tex.Format == ImTextureFormat_.ImTextureFormat_RGBA32)
            {
                int write_ptr = tex.GetPixelsAt(r.x, r.y + y);
                for (int i = 0; i < pad_left; i++)
                    SetCol(tex, write_ptr + i * 4, ImGuiCore.IM_COL32(255, 255, 255, 0));
                for (int i = 0; i < line_width; i++)
                    SetCol(tex, write_ptr + (pad_left + i) * 4, ImGuiCore.IM_COL32_WHITE);
                for (int i = 0; i < pad_right; i++)
                    SetCol(tex, write_ptr + (pad_left + line_width + i) * 4, ImGuiCore.IM_COL32(255, 255, 255, 0));
            }

            ImVec2 uv0 = new ImVec2((r.x + pad_left - 1), (r.y + y)) * atlas.TexUvScale;
            ImVec2 uv1 = new ImVec2((r.x + pad_left + line_width + 1), (r.y + y + 1)) * atlas.TexUvScale;
            float half_v = (uv0.y + uv1.y) * 0.5f;
            atlas.TexUvLines[n] = new ImVec4(uv0.x, half_v, uv1.x, half_v);
        }
    }

    static void SetCol(ImTextureData tex, int byte_off, uint col)
    {
        tex.Pixels[byte_off + 0] = (byte)(col & 0xFF);
        tex.Pixels[byte_off + 1] = (byte)((col >> 8) & 0xFF);
        tex.Pixels[byte_off + 2] = (byte)((col >> 16) & 0xFF);
        tex.Pixels[byte_off + 3] = (byte)((col >> 24) & 0xFF);
    }

    // ---- Per-font source/output lifecycle ----

    public static bool ImFontAtlasFontInitOutput(ImFontAtlas atlas, ImFont font)
    {
        bool ret = true;
        for (int i = 0; i < font.Sources.Size; i++)
            if (!ImFontAtlasFontSourceInit(atlas, font.Sources[i]))
                ret = false;
        return ret;
    }

    public static void ImFontAtlasFontDestroyOutput(ImFontAtlas atlas, ImFont font)
    {
        font.ClearOutputData();
        for (int i = 0; i < font.Sources.Size; i++)
        {
            var src = font.Sources[i];
            var loader = src.FontLoader ?? atlas.FontLoader;
            if (loader != null && loader.FontSrcDestroy != null)
                loader.FontSrcDestroy(atlas, src);
        }
    }

    public static void ImFontAtlasFontRebuildOutput(ImFontAtlas atlas, ImFont font)
    {
        ImFontAtlasFontDestroyOutput(atlas, font);
        ImFontAtlasFontInitOutput(atlas, font);
    }

    public static bool ImFontAtlasFontSourceInit(ImFontAtlas atlas, ImFontConfig src)
    {
        var loader = src.FontLoader ?? atlas.FontLoader;
        if (loader.FontSrcInit != null && !loader.FontSrcInit(atlas, src))
            return false;
        return true;
    }

    public static void ImFontAtlasFontSourceAddToFont(ImFontAtlas atlas, ImFont font, ImFontConfig src)
    {
        if (src.MergeMode == false)
        {
            font.ClearOutputData();
            font.OwnerAtlas = atlas;
        }
        atlas.TexIsBuilt = false; // For legacy backends
        ImFontAtlasBuildSetupFontSpecialGlyphs(atlas, font, src);
    }

    public static void ImFontAtlasFontDestroySourceData(ImFontAtlas atlas, ImFontConfig src)
    {
        if (src.FontDataOwnedByAtlas)
            src.FontData = null; // GC-managed; no IM_FREE equivalent needed
        src.FontData = null;
        src.GlyphExcludeRanges = null;
    }

    // Create a compact, baked "..." if it doesn't exist, by using the ".".
    public static ImFontGlyph ImFontAtlasBuildSetupFontBakedEllipsis(ImFontAtlas atlas, ImFontBaked baked)
    {
        ImFont font = baked.OwnerFont;

        ImFontGlyph dot_glyph = baked.FindGlyphNoFallback((ushort)'.');
        if (dot_glyph == null)
            dot_glyph = baked.FindGlyphNoFallback((ushort)0xFF0E);
        if (dot_glyph == null)
            return null;
        int dot_r_id = dot_glyph.PackId;
        var dot_r = ImFontAtlasPackGetRect(atlas, dot_r_id).Value;
        int dot_spacing = 1;
        float dot_step = (dot_glyph.X1 - dot_glyph.X0) + dot_spacing;

        int pack_id = ImFontAtlasPackAddRect(atlas, (dot_r.w * 3 + dot_spacing * 2), dot_r.h);
        var r = ImFontAtlasPackGetRect(atlas, pack_id).Value;

        var glyph_in = new ImFontGlyph();
        glyph_in.Codepoint = font.EllipsisChar;
        glyph_in.AdvanceX = Math.Max(dot_glyph.AdvanceX, dot_glyph.X0 + dot_step * 3.0f - dot_spacing);
        glyph_in.X0 = dot_glyph.X0;
        glyph_in.Y0 = dot_glyph.Y0;
        glyph_in.X1 = dot_glyph.X0 + dot_step * 3 - dot_spacing;
        glyph_in.Y1 = dot_glyph.Y1;
        glyph_in.Visible = 1;
        glyph_in.PackId = pack_id;
        ImFontGlyph glyph = ImFontAtlasBakedAddFontGlyph(atlas, baked, null, glyph_in);

        // Copy to texture, post-process and queue update for backend
        dot_r = ImFontAtlasPackGetRect(atlas, dot_r_id).Value;
        ImTextureData tex = atlas.TexData;
        for (int n = 0; n < 3; n++)
            ImFontAtlasTextureBlockCopy(tex, dot_r.x, dot_r.y, tex, r.x + (dot_r.w + dot_spacing) * n, r.y, dot_r.w, dot_r.h);
        ImUtil.ImFontAtlasTextureBlockQueueUpload(atlas, tex, r.x, r.y, r.w, r.h);

        return glyph;
    }

    // Load fallback in order to obtain its index
    public static void ImFontAtlasBuildSetupFontBakedFallback(ImFontBaked baked)
    {
        ImFont font = baked.OwnerFont;
        ImFontGlyph fallback_glyph = null;
        if (font.FallbackChar != 0)
            fallback_glyph = baked.FindGlyphNoFallback(font.FallbackChar);
        if (fallback_glyph == null)
        {
            ImFontGlyph space_glyph = baked.FindGlyphNoFallback((ushort)' ');
            var glyph = new ImFontGlyph();
            glyph.Codepoint = 0;
            glyph.AdvanceX = space_glyph != null ? space_glyph.AdvanceX : (float)(int)(baked.Size * 0.40f + 0.5f);
            fallback_glyph = ImFontAtlasBakedAddFontGlyph(font.OwnerAtlas, baked, null, glyph);
        }
        baked.FallbackGlyphIndex = fallback_glyph_idx(baked, fallback_glyph);
        baked.FallbackAdvanceX = fallback_glyph.AdvanceX;
    }
    static int fallback_glyph_idx(ImFontBaked baked, ImFontGlyph g) => baked.Glyphs.index_of_ref(g);

    public static void ImFontAtlasBuildSetupFontBakedBlanks(ImFontAtlas atlas, ImFontBaked baked)
    {
        // Mark space as always hidden
        ImFontGlyph space_glyph = baked.FindGlyphNoFallback((ushort)' ');
        if (space_glyph != null)
            space_glyph.Visible = 0;

        // Setup Tab character.
        if (baked.FindGlyphNoFallback((ushort)'\t') == null && space_glyph != null)
        {
            var tab_glyph = new ImFontGlyph();
            tab_glyph.Codepoint = '\t';
            tab_glyph.AdvanceX = space_glyph.AdvanceX * ImUtil.IM_TABSIZE;
            ImFontAtlasBakedAddFontGlyph(atlas, baked, null, tab_glyph);
        }
    }

    // Load/identify special glyphs
    public static void ImFontAtlasBuildSetupFontSpecialGlyphs(ImFontAtlas atlas, ImFont font, ImFontConfig src)
    {
        // Find Fallback character. Actual glyph loaded in GetFontBaked().
        ushort[] fallback_chars = { font.FallbackChar, (ushort)ImUtil.IM_UNICODE_CODEPOINT_INVALID, (ushort)'?', (ushort)' ' };
        if (font.FallbackChar == 0)
            foreach (var candidate_char in fallback_chars)
                if (candidate_char != 0 && font.IsGlyphInFont(candidate_char))
                {
                    font.FallbackChar = candidate_char;
                    break;
                }

        // Setup Ellipsis character.
        ushort[] ellipsis_chars = { src.EllipsisChar, (ushort)0x2026, (ushort)0x0085 };
        if (font.EllipsisChar == 0)
            foreach (var candidate_char in ellipsis_chars)
                if (candidate_char != 0 && font.IsGlyphInFont(candidate_char))
                {
                    font.EllipsisChar = candidate_char;
                    break;
                }
        if (font.EllipsisChar == 0)
        {
            font.EllipsisChar = 0x0085;
            font.EllipsisAutoBake = true;
        }
    }

    public static void ImFontAtlasBakedDiscardFontGlyph(ImFontAtlas atlas, ImFont font, ImFontBaked baked, ImFontGlyph glyph)
    {
        if (glyph.PackId != ImUtil.ImFontAtlasRectId_Invalid)
        {
            ImFontAtlasPackDiscardRect(atlas, glyph.PackId);
            glyph.PackId = ImUtil.ImFontAtlasRectId_Invalid;
        }
        ushort c = (ushort)glyph.Codepoint;
        baked.IndexLookup.Data[c] = ImUtil.IM_FONTGLYPH_INDEX_UNUSED;
        baked.IndexAdvanceX.Data[c] = baked.FallbackAdvanceX;
    }

    public static ImFontBaked ImFontAtlasBakedAdd(ImFontAtlas atlas, ImFont font, float font_size, float font_rasterizer_density, uint baked_id)
    {
        var baked = new ImFontBaked();
        atlas.Builder.BakedPool.push_back(baked);
        baked.Size = font_size;
        baked.RasterizerDensity = font_rasterizer_density;
        baked.BakedId = baked_id;
        baked.OwnerFont = font;
        baked.LastUsedFrame = atlas.Builder.FrameCount;

        // Initialize backend data
        int src_count = font.Sources.Size;
        var loader_datas = new object[src_count];
        for (int i = 0; i < src_count; i++)
        {
            var src = font.Sources[i];
            var loader = src.FontLoader ?? atlas.FontLoader;
            if (loader.FontBakedInit != null)
                loader.FontBakedInit(atlas, src, baked, null);
            loader_datas[i] = src.FontLoaderData;
        }
        baked.FontLoaderDatas = loader_datas;

        ImFontAtlasBuildSetupFontBakedBlanks(atlas, baked);
        return baked;
    }

    public static ImFontBaked ImFontAtlasBakedGetClosestMatch(ImFontAtlas atlas, ImFont font, float font_size, float font_rasterizer_density)
    {
        var builder = atlas.Builder;
        for (int step_n = 0; step_n < 2; step_n++)
        {
            ImFontBaked closest_larger_match = null;
            ImFontBaked closest_smaller_match = null;
            for (int baked_n = 0; baked_n < builder.BakedPool.Size; baked_n++)
            {
                var baked = builder.BakedPool[baked_n];
                if (baked.OwnerFont != font || baked.WantDestroy)
                    continue;
                if (step_n == 0 && baked.RasterizerDensity != font_rasterizer_density)
                    continue;
                if (baked.Size > font_size && (closest_larger_match == null || baked.Size < closest_larger_match.Size))
                    closest_larger_match = baked;
                if (baked.Size < font_size && (closest_smaller_match == null || baked.Size > closest_smaller_match.Size))
                    closest_smaller_match = baked;
            }
            if (closest_larger_match != null)
                if (closest_smaller_match == null || (closest_larger_match.Size >= font_size * 2.0f && closest_smaller_match.Size > font_size * 0.5f))
                    return closest_larger_match;
            if (closest_smaller_match != null)
                return closest_smaller_match;
        }
        return null;
    }

    public static void ImFontAtlasBakedDiscard(ImFontAtlas atlas, ImFont font, ImFontBaked baked)
    {
        var builder = atlas.Builder;

        for (int i = 0; i < baked.Glyphs.Size; i++)
            if (baked.Glyphs[i].PackId != ImUtil.ImFontAtlasRectId_Invalid)
                ImFontAtlasPackDiscardRect(atlas, baked.Glyphs[i].PackId);

        builder.BakedMap.SetVoidPtr(baked.BakedId, null);
        builder.BakedDiscardedCount++;
        baked.ClearOutputData();
        baked.WantDestroy = true;
        font.LastBaked = null;
    }

    // use unused_frames==0 to discard everything.
    public static void ImFontAtlasFontDiscardBakes(ImFontAtlas atlas, ImFont font, int unused_frames)
    {
        if (atlas.Builder != null)
            for (int baked_n = 0; baked_n < atlas.Builder.BakedPool.Size; baked_n++)
            {
                var baked = atlas.Builder.BakedPool[baked_n];
                if (baked.LastUsedFrame + unused_frames > atlas.Builder.FrameCount)
                    continue;
                if (baked.OwnerFont != font || baked.WantDestroy)
                    continue;
                ImFontAtlasBakedDiscard(atlas, font, baked);
            }
    }

    public static void ImFontAtlasBuildDiscardBakes(ImFontAtlas atlas, int unused_frames)
    {
        var builder = atlas.Builder;
        for (int baked_n = 0; baked_n < builder.BakedPool.Size; baked_n++)
        {
            var baked = builder.BakedPool[baked_n];
            if (baked.LastUsedFrame + unused_frames > builder.FrameCount)
                continue;
            if (baked.WantDestroy || (baked.OwnerFont.Flags & ImFontFlags_.ImFontFlags_LockBakedSizes) != 0)
                continue;
            ImFontAtlasBakedDiscard(atlas, baked.OwnerFont, baked);
        }
    }

    public static void ImFontAtlasAddDrawListSharedData(ImFontAtlas atlas, ImDrawListSharedData data)
    {
        atlas.DrawListSharedDatas.push_back(data);
    }

    public static void ImFontAtlasRemoveDrawListSharedData(ImFontAtlas atlas, ImDrawListSharedData data)
    {
        atlas.DrawListSharedDatas.erase_unsorted(atlas.DrawListSharedDatas.index_of_ref(data));
    }

    // Update texture identifier in all active draw lists
    public static void ImFontAtlasUpdateDrawListsTextures(ImFontAtlas atlas, ImTextureRef old_tex, ImTextureRef new_tex)
    {
        for (int i = 0; i < atlas.DrawListSharedDatas.Size; i++)
        {
            var shared_data = atlas.DrawListSharedDatas[i];
            if (shared_data.Context != null && !shared_data.Context.WithinFrameScope)
                continue;

            for (int j = 0; j < shared_data.DrawLists.Size; j++)
            {
                var draw_list = shared_data.DrawLists[j];
                if (draw_list.CmdBuffer.Size > 0 && draw_list._CmdHeader.TexRef.Equals(old_tex))
                    draw_list._SetTexture(new_tex);

                for (int k = 0; k < draw_list._TextureStack.Size; k++)
                    if (draw_list._TextureStack[k].Equals(old_tex))
                        draw_list._TextureStack.Data[k] = new_tex;
            }
        }
    }

    public static void ImFontAtlasUpdateDrawListsSharedData(ImFontAtlas atlas)
    {
        for (int i = 0; i < atlas.DrawListSharedDatas.Size; i++)
        {
            var shared_data = atlas.DrawListSharedDatas[i];
            if (shared_data.FontAtlas == atlas)
            {
                shared_data.TexUvWhitePixel = atlas.TexUvWhitePixel;
                shared_data.TexUvLines = atlas.TexUvLines;
            }
        }
    }

    // Set current texture
    public static void ImFontAtlasBuildSetTexture(ImFontAtlas atlas, ImTextureData tex)
    {
        ImTextureRef old_tex_ref = atlas.TexRef;
        atlas.TexData = tex;
        atlas.TexUvScale = new ImVec2(1.0f / tex.Width, 1.0f / tex.Height);
        atlas.TexRef._TexData = tex;
        atlas.TexRef._TexID = ImUtil.ImTextureID_Invalid;
        ImFontAtlasUpdateDrawListsTextures(atlas, old_tex_ref, atlas.TexRef);
    }

    public static void ImFontAtlasBuildUpdateTexData(ImFontAtlas atlas)
    {
        ImFontAtlasBuildUpdateTexDataBasic(atlas);
        ImFontAtlasBuildUpdateTexDataLines(atlas);
    }

    // Create a new texture, discard previous one
    public static ImTextureData ImFontAtlasTextureAdd(ImFontAtlas atlas, int w, int h)
    {
        ImTextureData old_tex = atlas.TexData;
        ImTextureData new_tex;

        {
            new_tex = new ImTextureData();
            new_tex.UniqueID = atlas.TexNextUniqueID++;
            atlas.TexList.push_back(new_tex);
        }
        if (old_tex != null)
        {
            old_tex.WantDestroyNextFrame = true;
        }

        new_tex.Create(atlas.TexDesiredFormat, w, h);
        atlas.TexIsBuilt = false;

        ImFontAtlasBuildSetTexture(atlas, new_tex);

        return new_tex;
    }
}
