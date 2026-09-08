// Port of the stb_truetype font loader backend glue (imgui_draw.cpp, IMGUI_ENABLE_STB_TRUETYPE section)
// plus ImFontAtlasBuildNotifySetFont() / ImFontAtlasAddDrawListSharedData() and
// ImGui::SetCurrentFont() / ImGui::UpdateCurrentFontSize().
using System;

namespace ImGui;

public static partial class ImUtil
{
    // IM_ASSERT_USER_ERROR: recoverable user-facing assert. The C++ version logs and continues (or breaks in debug);
    // the port throws loudly since these paths indicate a programming error.
    public static void ImAssertUserError(bool exp, string msg)
    {
        if (!exp)
            throw new InvalidOperationException(msg);
    }
}

public static partial class ImGui
{
    // imgui_internal.h: IMGUI_FONT_SIZE_MAX
    public const float IMGUI_FONT_SIZE_MAX = 512.0f;

    public static void SetCurrentFont(ImFont font, float font_size_before_scaling, float font_size_after_scaling)
    {
        ImGuiContext g = GImGui;
        g.Font = font;
        g.FontSizeBase = font_size_before_scaling;
        UpdateCurrentFontSize(font_size_after_scaling);

        if (font != null)
        {
            System.Diagnostics.Debug.Assert(font.IsLoaded());    // Font Atlas not created. Did you call io.Fonts->GetTexDataAsRGBA32 / GetTexDataAsAlpha8 ?
            ImFontAtlas atlas = font.OwnerAtlas;
            g.DrawListSharedData.FontAtlas = atlas;
            g.DrawListSharedData.Font = font;
            ImFontAtlas.ImFontAtlasUpdateDrawListsSharedData(atlas);
            if (g.CurrentWindow != null)
                g.CurrentWindow.DrawList._SetTexture(atlas.TexRef);
        }
    }

    public static void UpdateCurrentFontSize(float restore_font_size_after_scaling)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        g.Style.FontSizeBase = g.FontSizeBase;

        // Restoring is pretty much only used by PopFont()
        float final_size = (restore_font_size_after_scaling > 0.0f) ? restore_font_size_after_scaling : 0.0f;
        if (final_size == 0.0f)
        {
            final_size = g.FontSizeBase;

            // Global scale factors
            final_size *= g.Style.FontScaleMain;    // Main global scale factor
            final_size *= g.Style.FontScaleDpi;     // Per-monitor/viewport DPI scale factor (in docking branch: automatically updated when io.ConfigDpiScaleFonts is enabled).

            // Window scale (mostly obsolete now)
            if (window != null)
                final_size *= window.FontWindowScale;

            // Legacy scale factors
            final_size *= g.IO.FontGlobalScale; // Use style.FontScaleMain instead!
            if (g.Font != null)
                final_size *= g.Font.Scale;     // Was never really useful.
        }

        // Round font size
        // - We started rounding in 1.90 WIP (18991) as our layout system currently doesn't support non-rounded font size well yet.
        // - We may support it better later and remove this rounding.
        final_size = ImGuiCore.GetRoundedFontSize(final_size);
        final_size = ImMath.ImClamp(final_size, 1.0f, IMGUI_FONT_SIZE_MAX);
        if (g.Font != null && (g.IO.BackendFlags & ImGuiBackendFlags_RendererHasTextures) != 0)
            g.Font.CurrentRasterizerDensity = g.CurrentPixelDensity;

        g.FontSize = final_size;
        g.DrawListSharedData.FontSize = g.FontSize;

        // Early out to avoid hidden window keeping bakes referenced and out of GC reach.
        // - However this leave a pretty subtle and damning error surface area if g.FontBaked was mismatching.
        //   Probably needs to be reevaluated into e.g. setting g.FontBaked = nullptr to mark it as dirty.
        // - Note that 'PushFont(); Begin(); End(); PopFont()' from within any collapsed window is not compromised, because Begin() calls SetCurrentWindow()->...->UpdateCurrentSize()
        if (window != null && window.SkipItems)
        {
            ImGuiTable table = g.CurrentTable;
            bool allow_early_out = table == null || (table.CurrentColumn != -1 && table.Columns[table.CurrentColumn].IsSkipItems == false); // See 8465#issuecomment-2951509561 and #8865. Ideally the SkipItems=true in tables would be amended with extra data.
            if (allow_early_out)
                return;
        }

        g.FontBaked = (g.Font != null && window != null) ? g.Font.GetFontBaked(final_size) : null;
        g.FontBakedScale = (g.FontBaked != null) ? (g.FontSize / g.FontBaked.Size) : 0.0f;
        g.DrawListSharedData.FontScale = g.FontBakedScale;
    }
}

public partial class ImFontAtlas
{
    // On font removal we need to remove references (otherwise we could queue removal?)
    // We allow old_font == new_font which forces updating all values (e.g. sizes)
    public static void ImFontAtlasBuildNotifySetFont(ImFontAtlas atlas, ImFont old_font, ImFont new_font)
    {
        for (int i = 0; i < atlas.DrawListSharedDatas.Size; i++)
        {
            ImDrawListSharedData shared_data = atlas.DrawListSharedDatas[i];
            if (shared_data.Font == old_font)
                shared_data.Font = new_font;
            ImGuiContext ctx = shared_data.Context;
            if (ctx != null)
            {
                // While this should work either way, we save ourselves the bother / debugging confusion of running ImGui code so early when it is not needed.
                // Also fixes erroneously rewriting style.FontSizeBase during init if adding default fonts.
                if (old_font == null && ctx.Font == null && ctx.FontSizeBase == 0.0f)
                    continue;

                if (ctx.IO.FontDefault == old_font)
                    ctx.IO.FontDefault = new_font;
                if (ctx.Font == old_font)
                {
                    ImGuiContext curr_ctx = ImGui.GetCurrentContext();
                    bool need_bind_ctx = ctx != curr_ctx;
                    if (need_bind_ctx)
                        ImGui.SetCurrentContext(ctx);
                    ImGui.SetCurrentFont(new_font, ctx.FontSizeBase, ctx.FontSize);
                    if (need_bind_ctx)
                        ImGui.SetCurrentContext(curr_ctx);
                }
                for (int n = 0; n < ctx.FontStack.Size; n++)
                {
                    var font_stack_data = ctx.FontStack[n];
                    if (font_stack_data.Font == old_font)
                    {
                        font_stack_data.Font = new_font;
                        ctx.FontStack[n] = font_stack_data;
                    }
                }
            }
        }
    }

    // ImFontAtlasAddDrawListSharedData / ImFontAtlasRemoveDrawListSharedData are in ImFontAtlasBuild2.cs
}

// One for each ConfigData
// (ImGui_ImplStbTrueType_FontSrcData in C++; data stored in ImFontConfig.FontLoaderData)
public partial class ImFontAtlas
{
    static ImFontLoader StbTruetypeLoader;

    public static ImFontLoader ImFontAtlasGetFontLoaderForStbTruetype()
    {
        if (StbTruetypeLoader == null)
        {
            var loader = new ImFontLoader();
            loader.Name = "stb_truetype";
            loader.FontSrcInit = StbTrueTypeFontSrcInit;
            loader.FontSrcDestroy = StbTrueTypeFontSrcDestroy;
            loader.FontSrcContainsGlyph = StbTrueTypeFontSrcContainsGlyph;
            loader.FontBakedInit = StbTrueTypeFontBakedInit;
            loader.FontBakedDestroy = null;
            loader.FontBakedLoadGlyph = StbTrueTypeFontBakedLoadGlyph;
            StbTruetypeLoader = loader;
        }
        return StbTruetypeLoader;
    }

    static bool StbTrueTypeFontSrcInit(ImFontAtlas atlas, ImFontConfig src)
    {
        var bd_font_data = new ImGuiImplStbTrueTypeFontSrcData();
        System.Diagnostics.Debug.Assert(src.FontLoaderData == null);

        // Initialize helper structure for font loading and verify that the TTF/OTF data is correct
        int font_offset = StbTrueType.GetFontOffsetForIndex(src.FontData, (int)src.FontNo);
        if (font_offset < 0)
        {
            ImUtil.ImAssertUserError(false, "stbtt_GetFontOffsetForIndex(): FontData is incorrect, or FontNo cannot be found.");
            return false;
        }
        if (!StbTrueType.InitFont(bd_font_data.FontInfo, src.FontData, font_offset))
        {
            ImUtil.ImAssertUserError(false, "stbtt_InitFont(): failed to parse FontData. It is correct and complete? Check FontDataSize.");
            return false;
        }
        src.FontLoaderData = bd_font_data;

        float ref_size = src.DstFont.Sources[0].SizePixels;
        if (src.MergeMode && src.SizePixels == 0.0f)
            src.SizePixels = ref_size;

        bd_font_data.ScaleFactor = StbTrueType.ScaleForPixelHeight(bd_font_data.FontInfo, 1.0f);
        if (src.MergeMode && src.SizePixels != 0.0f && ref_size != 0.0f)
            bd_font_data.ScaleFactor *= src.SizePixels / ref_size; // FIXME-NEWATLAS: Should tidy up that a bit
        bd_font_data.ScaleFactor *= src.ExtraSizeScale;

        return true;
    }

    static void StbTrueTypeFontSrcDestroy(ImFontAtlas atlas, ImFontConfig src)
    {
        src.FontLoaderData = null;
    }

    static bool StbTrueTypeFontSrcContainsGlyph(ImFontAtlas atlas, ImFontConfig src, ushort codepoint)
    {
        var bd_font_data = (ImGuiImplStbTrueTypeFontSrcData)src.FontLoaderData;
        System.Diagnostics.Debug.Assert(bd_font_data != null);

        int glyph_index = StbTrueType.FindGlyphIndex(bd_font_data.FontInfo, codepoint);
        return glyph_index != 0;
    }

    static bool StbTrueTypeFontBakedInit(ImFontAtlas atlas, ImFontConfig src, ImFontBaked baked, object _)
    {
        var bd_font_data = (ImGuiImplStbTrueTypeFontSrcData)src.FontLoaderData;
        if (src.MergeMode == false)
        {
            // FIXME-NEWFONTS: reevaluate how to use sizing metrics
            // FIXME-NEWFONTS: make use of line gap value
            float scale_for_layout = bd_font_data.ScaleFactor * baked.Size / src.ExtraSizeScale;
            StbTrueType.GetFontVMetrics(bd_font_data.FontInfo, out int unscaled_ascent, out int unscaled_descent, out int unscaled_line_gap);
            baked.Ascent = ImMath.ImCeil(unscaled_ascent * scale_for_layout);
            baked.Descent = ImMath.ImFloor(unscaled_descent * scale_for_layout);
        }
        return true;
    }

    static bool StbTrueTypeFontBakedLoadGlyph(ImFontAtlas atlas, ImFontConfig src, ImFontBaked baked, object _, ushort codepoint, ImFontGlyph out_glyph, float[] out_advance_x)
    {
        // Search for first font which has the glyph
        var bd_font_data = (ImGuiImplStbTrueTypeFontSrcData)src.FontLoaderData;
        System.Diagnostics.Debug.Assert(bd_font_data != null);
        int glyph_index = StbTrueType.FindGlyphIndex(bd_font_data.FontInfo, codepoint);
        if (glyph_index == 0)
            return false;

        // Fonts unit to pixels
        ImFontAtlasBuildGetOversampleFactors(src, baked, out int oversample_h, out int oversample_v);
        float scale_for_layout = bd_font_data.ScaleFactor * baked.Size;
        float rasterizer_density = src.RasterizerDensity * baked.RasterizerDensity;
        float scale_for_raster_x = bd_font_data.ScaleFactor * baked.Size * rasterizer_density * oversample_h;
        float scale_for_raster_y = bd_font_data.ScaleFactor * baked.Size * rasterizer_density * oversample_v;

        // Obtain size and advance
        StbTrueType.GetGlyphBitmapBoxSubpixel(bd_font_data.FontInfo, glyph_index, scale_for_raster_x, scale_for_raster_y, 0, 0, out int x0, out int y0, out int x1, out int y1);
        StbTrueType.GetGlyphHMetrics(bd_font_data.FontInfo, glyph_index, out int advance, out int lsb);

        // Load metrics only mode
        if (out_advance_x != null)
        {
            System.Diagnostics.Debug.Assert(out_glyph == null);
            out_advance_x[0] = advance * scale_for_layout;
            return true;
        }

        // Prepare glyph
        out_glyph.Codepoint = codepoint;
        out_glyph.AdvanceX = advance * scale_for_layout;

        // Pack and retrieve position inside texture atlas
        // (generally based on stbtt_PackFontRangesRenderIntoRects)
        bool is_visible = (x0 != x1 && y0 != y1);
        if (is_visible)
        {
            int w = (x1 - x0 + oversample_h - 1);
            int h = (y1 - y0 + oversample_v - 1);
            int pack_id = ImFontAtlasPackAddRect(atlas, w, h);
            if (pack_id == ImUtil.ImFontAtlasRectId_Invalid)
            {
                // Pathological out of memory case (TexMaxWidth/TexMaxHeight set too small?)
                System.Diagnostics.Debug.Assert(pack_id != ImUtil.ImFontAtlasRectId_Invalid, "Out of texture memory.");
                return false;
            }
            var r = ImFontAtlasPackGetRect(atlas, pack_id).Value;

            // Render
            StbTrueType.GetGlyphBitmapBox(bd_font_data.FontInfo, glyph_index, scale_for_raster_x, scale_for_raster_y, out x0, out y0, out x1, out y1);
            ImFontAtlasBuilder builder = atlas.Builder;
            builder.TempBuffer.resize(w * h * 1);
            byte[] bitmap_pixels = builder.TempBuffer.Data;
            Array.Clear(bitmap_pixels, 0, w * h * 1);

            // Render with oversampling
            // (those functions conveniently assert if pixels are not cleared, which is another safety layer)
            StbTrueType.MakeGlyphBitmapSubpixelPrefilter(bd_font_data.FontInfo, bitmap_pixels, w, h, w,
                scale_for_raster_x, scale_for_raster_y, 0, 0, oversample_h, oversample_v, out float sub_x, out float sub_y, glyph_index);

            float ref_size = baked.OwnerFont.Sources[0].SizePixels;
            float offsets_scale = (ref_size != 0.0f) ? (baked.Size / ref_size) : 1.0f;
            float font_off_x = ImMath.ImFloor(src.GlyphOffset.x * offsets_scale + 0.5f); // Snap scaled offset.
            float font_off_y = ImMath.ImFloor(src.GlyphOffset.y * offsets_scale + 0.5f);
            font_off_x += sub_x;
            font_off_y += sub_y + ImMath.ImRound(baked.Ascent);
            float recip_h = 1.0f / (oversample_h * rasterizer_density);
            float recip_v = 1.0f / (oversample_v * rasterizer_density);

            // Register glyph
            // r.x r.y are coordinates inside texture (in pixels)
            // glyph.X0, glyph.Y0 are drawing coordinates from base text position, and accounting for oversampling.
            out_glyph.X0 = x0 * recip_h + font_off_x;
            out_glyph.Y0 = y0 * recip_v + font_off_y;
            out_glyph.X1 = (x0 + r.w) * recip_h + font_off_x;
            out_glyph.Y1 = (y0 + r.h) * recip_v + font_off_y;
            out_glyph.Visible = 1;
            out_glyph.PackId = pack_id;
            ImFontAtlasBakedSetFontGlyphBitmap(atlas, baked, src, out_glyph, r, bitmap_pixels, ImTextureFormat_Alpha8, w);
        }

        return true;
    }
}
