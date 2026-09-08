// Port of Dear ImGui font classes (imgui.h/imgui_draw.cpp [SECTION] ImFontConfig, ImFontBaked, ImFont).
using System;
using System.Text;

namespace ImGui;

public static partial class ImUtil
{
    public static void ImSwapRange<T>(ref T a, ref T b) { T t = a; a = b; b = t; }

    public const ushort IM_FONTGLYPH_INDEX_UNUSED = 0xFFFF;
    public const ushort IM_FONTGLYPH_INDEX_NOT_FOUND = 0xFFFE;
    public const int IM_UNICODE_CODEPOINT_INVALID = 0xFFFD;
    public const int IM_UNICODE_CODEPOINT_MAX = 0xFFFF; // ImWchar16
    public const int IM_TABSIZE = 4;

    // C++ imgui_internal.h: inline bool ImCharIsBlankW(unsigned int c)
    public static bool ImCharIsBlankW(uint c) => c == ' ' || c == '\t' || c == 0x3000;
}

// A single font glyph (class: stable identity, mirrors C++ ImFontGlyph* usage)
public class ImFontGlyph
{
    public uint Colored;        // 1 bit in C++
    public uint Visible;        // 1 bit
    public uint SourceIdx;      // 4 bits
    public uint Codepoint;      // 26 bits
    public float AdvanceX;
    public float X0, Y0, X1, Y1;
    public float U0, V0, U1, V1;
    public int PackId;

    public ImFontGlyph() { PackId = -1; }
    public ImFontGlyph Clone()
    {
        var g = new ImFontGlyph();
        g.Colored = Colored; g.Visible = Visible; g.SourceIdx = SourceIdx; g.Codepoint = Codepoint;
        g.AdvanceX = AdvanceX; g.X0 = X0; g.Y0 = Y0; g.X1 = X1; g.Y1 = Y1;
        g.U0 = U0; g.V0 = V0; g.U1 = U1; g.V1 = V1; g.PackId = PackId;
        return g;
    }
}

// Configuration data when adding a font or merging fonts (class in C++; C++ copies by value -> use Clone())
public class ImFontConfig
{
    public string Name = "";
    public byte[] FontData;
    public int FontDataSize;
    public bool FontDataOwnedByAtlas = true;
    public bool MergeMode;
    public bool PixelSnapH;
    public int OversampleH;             // 0 == auto
    public int OversampleV;             // 0 == auto
    public ushort EllipsisChar;
    public float SizePixels;
    public ushort[] GlyphRanges;
    public ushort[] GlyphExcludeRanges;
    public ImVec2 GlyphOffset;
    public float GlyphMinAdvanceX;
    public float GlyphMaxAdvanceX = float.MaxValue;
    public float GlyphExtraAdvanceX;
    public uint FontNo;
    public uint FontLoaderFlags;
    public float RasterizerMultiply = 1.0f;
    public float RasterizerDensity = 1.0f;
    public float ExtraSizeScale = 1.0f;
    public int Flags;
    public ImFont DstFont;
    public ImFontLoader FontLoader;
    public object FontLoaderData;
    public bool PixelSnapV = true;

    public ImFontConfig()
    {
        FontDataOwnedByAtlas = true;
        OversampleH = 0;
        OversampleV = 0;
        ExtraSizeScale = 1.0f;
        GlyphMaxAdvanceX = float.MaxValue;
        RasterizerMultiply = 1.0f;
        RasterizerDensity = 1.0f;
        EllipsisChar = 0;
    }

    public ImFontConfig Clone()
    {
        var c = new ImFontConfig();
        c.Name = Name; c.FontData = FontData; c.FontDataSize = FontDataSize;
        c.FontDataOwnedByAtlas = FontDataOwnedByAtlas; c.MergeMode = MergeMode; c.PixelSnapH = PixelSnapH;
        c.OversampleH = OversampleH; c.OversampleV = OversampleV; c.EllipsisChar = EllipsisChar;
        c.SizePixels = SizePixels; c.GlyphRanges = GlyphRanges; c.GlyphExcludeRanges = GlyphExcludeRanges;
        c.GlyphOffset = GlyphOffset; c.GlyphMinAdvanceX = GlyphMinAdvanceX; c.GlyphMaxAdvanceX = GlyphMaxAdvanceX;
        c.GlyphExtraAdvanceX = GlyphExtraAdvanceX; c.FontNo = FontNo; c.FontLoaderFlags = FontLoaderFlags;
        c.RasterizerMultiply = RasterizerMultiply; c.RasterizerDensity = RasterizerDensity; c.ExtraSizeScale = ExtraSizeScale;
        c.Flags = Flags; c.DstFont = DstFont; c.FontLoader = FontLoader; c.FontLoaderData = FontLoaderData;
        c.PixelSnapV = PixelSnapV;
        return c;
    }
}

// Opaque interface to a font loading backend (stb_truetype here). Function-pointer style like C++.
public class ImFontLoader
{
    public string Name;
    public int FontBakedSrcLoaderDataSize;
    public Func<ImFontAtlas, ImFontConfig, bool> FontSrcInit;
    public Action<ImFontAtlas, ImFontConfig> FontSrcDestroy;
    public Func<ImFontAtlas, ImFontConfig, ushort, bool> FontSrcContainsGlyph;
    public Func<ImFontAtlas, ImFontConfig, ImFontBaked, object, bool> FontBakedInit;
    public Action<ImFontAtlas, ImFontConfig, ImFontBaked, object> FontBakedDestroy;
    public Func<ImFontAtlas, ImFontConfig, ImFontBaked, object, ushort, ImFontGlyph, float[], bool> FontBakedLoadGlyph;
    // FontBakedLoadGlyph semantics: (atlas, src, baked, loader_data, codepoint, out_glyph_or_null, out_advance_x_or_null[1])
    // returns true when the glyph is provided.
}

// Baked data for a ImFont at a given size
public class ImFontBaked
{
    public ImVector<float> IndexAdvanceX = new ImVector<float>();
    public float FallbackAdvanceX;
    public float Size;
    public float RasterizerDensity;
    public ImVector<ushort> IndexLookup = new ImVector<ushort>();
    public ImVector<ImFontGlyph> Glyphs = new ImVector<ImFontGlyph>();
    public int FallbackGlyphIndex = -1;
    public float Ascent, Descent;
    public uint MetricsTotalSurface;
    public bool WantDestroy;
    public bool LoadNoFallback;
    public bool LoadNoRenderOnLayout;
    public int LastUsedFrame;
    public uint BakedId;
    public ImFont OwnerFont;
    public object FontLoaderDatas;      // per-source loader storage (list)

    public ImFontBaked()
    {
        FallbackGlyphIndex = -1;
    }

    public void ClearOutputData()
    {
        FallbackAdvanceX = 0.0f;
        Glyphs.clear();
        IndexAdvanceX.clear();
        IndexLookup.clear();
        FallbackGlyphIndex = -1;
        Ascent = Descent = 0.0f;
        MetricsTotalSurface = 0;
    }

    // Find glyph, load if necessary, return fallback if missing
    public ImFontGlyph FindGlyph(ushort c)
    {
        if (c < IndexLookup.Size)
        {
            int i = IndexLookup.Data[c];
            if (i == ImUtil.IM_FONTGLYPH_INDEX_NOT_FOUND)
                return Glyphs.Data[FallbackGlyphIndex];
            if (i != ImUtil.IM_FONTGLYPH_INDEX_UNUSED)
                return Glyphs.Data[i];
        }
        ImFontGlyph glyph = ImFontAtlas.ImFontBaked_BuildLoadGlyph(this, c, null);
        return glyph != null ? glyph : Glyphs.Data[FallbackGlyphIndex];
    }

    // Attempt to load but when missing, return NULL instead of FallbackGlyph
    public ImFontGlyph FindGlyphNoFallback(ushort c)
    {
        if (c < IndexLookup.Size)
        {
            int i = IndexLookup.Data[c];
            if (i == ImUtil.IM_FONTGLYPH_INDEX_NOT_FOUND)
                return null;
            if (i != ImUtil.IM_FONTGLYPH_INDEX_UNUSED)
                return Glyphs.Data[i];
        }
        LoadNoFallback = true;
        ImFontGlyph glyph = ImFontAtlas.ImFontBaked_BuildLoadGlyph(this, c, null);
        LoadNoFallback = false;
        return glyph;
    }

    public bool IsGlyphLoaded(ushort c)
    {
        if (c < IndexLookup.Size)
        {
            int i = IndexLookup.Data[c];
            if (i == ImUtil.IM_FONTGLYPH_INDEX_NOT_FOUND)
                return false;
            if (i != ImUtil.IM_FONTGLYPH_INDEX_UNUSED)
                return true;
        }
        return false;
    }

    public float GetCharAdvance(ushort c)
    {
        if ((int)c < IndexAdvanceX.Size)
        {
            float x = IndexAdvanceX.Data[c];
            if (x >= 0.0f)
                return x;
        }
        return ImFontAtlas.ImFontBaked_BuildLoadGlyphAdvanceX(this, c);
    }
}

// Runtime data for a single font within a parent ImFontAtlas
public class ImFont
{
    public ImFontBaked LastBaked;
    public ImFontAtlas OwnerAtlas;
    public int Flags;
    public float CurrentRasterizerDensity;
    public float Scale = 1.0f;              // Legacy scale factor (obsolete-functions field kept like C++)
    public uint FontId;
    public float LegacySize;
    public ImVector<ImFontConfig> Sources = new ImVector<ImFontConfig>();
    public ushort EllipsisChar;
    public ushort FallbackChar;
    public byte[] Used8kPagesMap = new byte[(ImUtil.IM_UNICODE_CODEPOINT_MAX + 1) / 8192 / 8];
    public bool EllipsisAutoBake;
    public ImGuiStorage RemapPairs = new ImGuiStorage();

    public ImFont()
    {
    }

    public bool IsLoaded() => OwnerAtlas != null;
    public string GetDebugName() => Sources.Size > 0 ? Sources[0].Name : "<unknown>";

    public void ClearOutputData()
    {
        if (OwnerAtlas != null)
            ImFontAtlas.ImFontAtlasFontDiscardBakes(OwnerAtlas, this, 0);
        Array.Clear(Used8kPagesMap, 0, Used8kPagesMap.Length);
        LastBaked = null;
    }

    public bool IsGlyphRangeUnused(uint c_begin, uint c_last)
    {
        uint page_begin = (c_begin / 8192);
        uint page_last = (c_last / 8192);
        for (uint page_n = page_begin; page_n <= page_last; page_n++)
            if ((page_n >> 3) < Used8kPagesMap.Length)
                if ((Used8kPagesMap[page_n >> 3] & (1 << (int)(page_n & 7))) != 0)
                    return false;
        return true;
    }

    public void AddRemapChar(ushort from_codepoint, ushort to_codepoint)
    {
        RemapPairs.SetInt((uint)from_codepoint, (int)to_codepoint);
    }

    public bool IsGlyphInFont(ushort c_in)
    {
        ImFontAtlas atlas = OwnerAtlas;
        ushort c = ImFontAtlas.ImFontAtlas_FontHookRemapCodepoint(atlas, this, c_in);
        for (int i = 0; i < Sources.Size; i++)
        {
            var src = Sources[i];
            var loader = src.FontLoader ?? atlas.FontLoader;
            if (loader.FontSrcContainsGlyph != null && loader.FontSrcContainsGlyph(atlas, src, c))
                return true;
        }
        return false;
    }

    // ImFontBaked pointers are valid for the entire frame but shall never be kept between frames.
    public ImFontBaked GetFontBaked(float size, float density = -1.0f)
    {
        ImFontBaked baked = LastBaked;

        size = ImGuiCore.GetRoundedFontSize(size);

        if (density < 0.0f)
            density = CurrentRasterizerDensity;
        if (baked != null && baked.Size == size && baked.RasterizerDensity == density)
            return baked;

        ImFontAtlas atlas = OwnerAtlas;
        ImFontAtlasBuilder builder = atlas.Builder;
        baked = ImFontAtlas.ImFontAtlasBakedGetOrAdd(atlas, this, size, density);
        if (baked == null)
            return null;
        baked.LastUsedFrame = builder.FrameCount;
        LastBaked = baked;
        return baked;
    }

    public int CalcWordWrapPosition(float size, string text, int text_begin, int text_end, float wrap_width)
    {
        return ImFontAtlas.ImFontCalcWordWrapPositionEx(this, size, text, text_begin, text_end, wrap_width, ImDrawTextFlags_.ImDrawTextFlags_None);
    }

    public ImVec2 CalcTextSizeA(float size, float max_width, float wrap_width, string text, int text_begin = 0, int text_end = -1)
        => CalcTextSizeA(size, max_width, wrap_width, text.AsSpan(), text_begin, text_end);

    public ImVec2 CalcTextSizeA(float size, float max_width, float wrap_width, ReadOnlySpan<char> text, int text_begin = 0, int text_end = -1)
    {
        int remaining;
        return ImFontAtlas.ImFontCalcTextSizeEx(this, size, max_width, wrap_width, text, text_begin, text_end < 0 ? text.Length : text_end, text_end < 0 ? text.Length : text_end, out remaining, null, ImDrawTextFlags_.ImDrawTextFlags_None);
    }

    // Note: as with every ImDrawList drawing function, this expects that the font atlas texture is bound.
    public void RenderChar(ImDrawList draw_list, float size, ImVec2 pos, ImU32 col, ushort c, ImVec4? cpu_fine_clip = null)
    {
        ImFontBaked baked = GetFontBaked(size);
        ImFontGlyph glyph = baked.FindGlyph(c);
        if (glyph == null || glyph.Visible == 0)
            return;
        if (glyph.Colored != 0)
            col |= ~ImGuiCore.IM_COL32_A_MASK;
        float scale = (size >= 0.0f) ? (size / baked.Size) : 1.0f;
        float x = pos.x;
        float y = pos.y;
        if ((draw_list.Flags & ImDrawListFlags_.ImDrawListFlags_TextNoPixelSnap) == 0)
        {
            x = (float)(int)x;
            y = (float)(int)y;
        }

        float x1 = x + glyph.X0 * scale;
        float x2 = x + glyph.X1 * scale;
        if (cpu_fine_clip.HasValue && (x1 > cpu_fine_clip.Value.z || x2 < cpu_fine_clip.Value.x))
            return;
        float y1 = y + glyph.Y0 * scale;
        float y2 = y + glyph.Y1 * scale;
        float u1 = glyph.U0;
        float v1 = glyph.V0;
        float u2 = glyph.U1;
        float v2 = glyph.V1;

        if (cpu_fine_clip.HasValue)
        {
            var cr = cpu_fine_clip.Value;
            if (x1 < cr.x) { u1 = u1 + (1.0f - (x2 - cr.x) / (x2 - x1)) * (u2 - u1); x1 = cr.x; }
            if (y1 < cr.y) { v1 = v1 + (1.0f - (y2 - cr.y) / (y2 - y1)) * (v2 - v1); y1 = cr.y; }
            if (x2 > cr.z) { u2 = u1 + ((cr.z - x1) / (x2 - x1)) * (u2 - u1); x2 = cr.z; }
            if (y2 > cr.w) { v2 = v1 + ((cr.w - y1) / (y2 - y1)) * (v2 - v1); y2 = cr.w; }
            if (y1 >= y2)
                return;
        }
        draw_list.PrimReserve(6, 4);
        draw_list.PrimRectUV(new ImVec2(x1, y1), new ImVec2(x2, y2), new ImVec2(u1, v1), new ImVec2(u2, v2), col);
    }

    // Note: as with every ImDrawList drawing function, this expects that the font atlas texture is bound.
    public void RenderText(ImDrawList draw_list, float size, ImVec2 pos, ImU32 col, ImVec4 clip_rect, string text, int text_begin, int text_end, float wrap_width, int flags)
        => RenderText(draw_list, size, pos, col, clip_rect, text.AsSpan(), text_begin, text_end, wrap_width, flags);

    public void RenderText(ImDrawList draw_list, float size, ImVec2 pos, ImU32 col, ImVec4 clip_rect, ReadOnlySpan<char> text, int text_begin, int text_end, float wrap_width, int flags)
    {
    begin:
        float x = pos.x;
        float y = pos.y;
        if (y > clip_rect.w)
            return;
        if ((draw_list.Flags & ImDrawListFlags_.ImDrawListFlags_TextNoPixelSnap) == 0)
        {
            x = (float)(int)x;
            y = (float)(int)y;
        }

        if (text_end < 0)
            text_end = text.Length;

        float line_height = size;
        ImFontBaked baked = GetFontBaked(size);

        float scale = size / baked.Size;
        float origin_x = x;
        bool word_wrap_enabled = (wrap_width > 0.0f);

        // Fast-forward to first visible line
        int s = text_begin;
        if (y + line_height < clip_rect.y)
            while (y + line_height < clip_rect.y && s < text_end)
            {
                int rel = text.Slice(s, text_end - s).IndexOf('\n');
                int line_end = rel < 0 ? -1 : s + rel;
                if (word_wrap_enabled)
                {
                    s = ImFontAtlas.ImFontCalcWordWrapPositionEx(this, size, text, s, line_end >= 0 ? line_end : text_end, wrap_width, flags);
                    s = ImFontAtlas.ImTextCalcWordWrapNextLineStart(text, s, text_end, flags);
                }
                else
                {
                    s = line_end >= 0 ? line_end + 1 : text_end;
                }
                y += line_height;
            }

        // (Large-text last-visible-line scan omitted: C++ guards it with text_end - s > 10000 bytes.)
        if (s == text_end)
            return;

        // Reserve vertices for remaining worse case
        int char_count = text_end - s;
        int vtx_count_max = char_count * 4;
        int idx_count_max = char_count * 6;
        int idx_expected_size = draw_list.IdxBuffer.Size + idx_count_max;
        draw_list.PrimReserve(idx_count_max, vtx_count_max);
        int vtx_write = draw_list._VtxWritePos;
        int idx_write = draw_list._IdxWritePos;
        uint vtx_index = draw_list._VtxCurrentIdx;
        int cmd_count = draw_list.CmdBuffer.Size;
        bool cpu_fine_clip = (flags & ImDrawTextFlags_.ImDrawTextFlags_CpuFineClip) != 0;

        ImU32 col_untinted = col | ~ImGuiCore.IM_COL32_A_MASK;
        int word_wrap_eol = -1;

        var vb = draw_list.VtxBuffer.Data;
        var ib = draw_list.IdxBuffer.Data;

        while (s < text_end)
        {
            if (word_wrap_enabled)
            {
                if (word_wrap_eol < 0)
                    word_wrap_eol = ImFontAtlas.ImFontCalcWordWrapPositionEx(this, size, text, s, text_end, wrap_width - (x - origin_x), flags);

                if (s >= word_wrap_eol)
                {
                    x = origin_x;
                    y += line_height;
                    if (y > clip_rect.w)
                        break;
                    word_wrap_eol = -1;
                    s = ImFontAtlas.ImTextCalcWordWrapNextLineStart(text, s, text_end, flags);
                    continue;
                }
            }

            // Decode and advance source
            char ch = text[s];
            uint c;
            int step;
            if (char.IsHighSurrogate(ch) && s + 1 < text_end && char.IsLowSurrogate(text[s + 1]))
            {
                c = 0x10000u + (uint)(((ch - 0xD800) << 10) + (text[s + 1] - 0xDC00));
                step = 2;
            }
            else
            {
                c = ch;
                step = 1;
            }
            s += step;

            if (c < 32)
            {
                if (c == '\n')
                {
                    x = origin_x;
                    y += line_height;
                    if (y > clip_rect.w)
                        break;
                    continue;
                }
                if (c == '\r')
                    continue;
            }

            ImFontGlyph glyph = baked.FindGlyph((ushort)c);
            //if (glyph == NULL) continue;

            float char_width = glyph.AdvanceX * scale;
            if (glyph.Visible != 0)
            {
                float x1 = x + glyph.X0 * scale;
                float x2 = x + glyph.X1 * scale;
                float y1 = y + glyph.Y0 * scale;
                float y2 = y + glyph.Y1 * scale;
                if (x1 <= clip_rect.z && x2 >= clip_rect.x)
                {
                    float u1 = glyph.U0;
                    float v1 = glyph.V0;
                    float u2 = glyph.U1;
                    float v2 = glyph.V1;

                    if (cpu_fine_clip)
                    {
                        if (x1 < clip_rect.x) { u1 = u1 + (1.0f - (x2 - clip_rect.x) / (x2 - x1)) * (u2 - u1); x1 = clip_rect.x; }
                        if (y1 < clip_rect.y) { v1 = v1 + (1.0f - (y2 - clip_rect.y) / (y2 - y1)) * (v2 - v1); y1 = clip_rect.y; }
                        if (x2 > clip_rect.z) { u2 = u1 + ((clip_rect.z - x1) / (x2 - x1)) * (u2 - u1); x2 = clip_rect.z; }
                        if (y2 > clip_rect.w) { v2 = v1 + ((clip_rect.w - y1) / (y2 - y1)) * (v2 - v1); y2 = clip_rect.w; }
                        if (y1 >= y2)
                        {
                            x += char_width;
                            continue;
                        }
                    }

                    ImU32 glyph_col = (glyph.Colored != 0) ? col_untinted : col;

                    {
                        vb[vtx_write + 0].pos = new ImVec2(x1, y1); vb[vtx_write + 0].col = glyph_col; vb[vtx_write + 0].uv = new ImVec2(u1, v1);
                        vb[vtx_write + 1].pos = new ImVec2(x2, y1); vb[vtx_write + 1].col = glyph_col; vb[vtx_write + 1].uv = new ImVec2(u2, v1);
                        vb[vtx_write + 2].pos = new ImVec2(x2, y2); vb[vtx_write + 2].col = glyph_col; vb[vtx_write + 2].uv = new ImVec2(u2, v2);
                        vb[vtx_write + 3].pos = new ImVec2(x1, y2); vb[vtx_write + 3].col = glyph_col; vb[vtx_write + 3].uv = new ImVec2(u1, v2);
                        ib[idx_write + 0] = (ImDrawIdx)(vtx_index); ib[idx_write + 1] = (ImDrawIdx)(vtx_index + 1); ib[idx_write + 2] = (ImDrawIdx)(vtx_index + 2);
                        ib[idx_write + 3] = (ImDrawIdx)(vtx_index); ib[idx_write + 4] = (ImDrawIdx)(vtx_index + 2); ib[idx_write + 5] = (ImDrawIdx)(vtx_index + 3);
                        vtx_write += 4;
                        vtx_index += 4;
                        idx_write += 6;
                    }
                }
            }
            x += char_width;
        }

        // Edge case: calling RenderText() with unloaded glyphs triggering texture change.
        if (cmd_count != draw_list.CmdBuffer.Size)
        {
            System.Diagnostics.Debug.Assert(draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1].ElemCount == 0);
            draw_list.CmdBuffer.pop_back();
            draw_list.PrimUnreserve(idx_count_max, vtx_count_max);
            draw_list.AddDrawCmd();
            goto begin;
        }

        // Give back unused vertices (clipped ones, blanks)
        draw_list.VtxBuffer.Size = vtx_write;
        draw_list.IdxBuffer.Size = idx_write;
        var last_cmd = draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1];
        last_cmd.ElemCount -= (uint)(idx_expected_size - draw_list.IdxBuffer.Size);
        draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1] = last_cmd;
        draw_list._VtxWritePos = vtx_write;
        draw_list._IdxWritePos = idx_write;
        draw_list._VtxCurrentIdx = vtx_index;
    }
}
