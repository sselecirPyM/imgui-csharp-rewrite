// Port of Dear ImGui font atlas (imgui_draw.cpp [SECTION] ImFontAtlas, ImFontAtlasBuilder,
// stb_truetype backend glue, glyph ranges, text classifiers, word-wrap helpers, decompression).
using System;
using System.Collections.Generic;

namespace ImGui;

// Rect id encoding (imgui_internal.h)
public static partial class ImUtil
{
    public const int ImFontAtlasRectId_IndexMask_ = 0x0007FFFF;
    public const uint ImFontAtlasRectId_GenerationMask_ = 0x3FF00000;
    public const int ImFontAtlasRectId_GenerationShift_ = 20;
    public const int ImFontAtlasRectId_Invalid = -1;

    public static int ImFontAtlasRectId_GetIndex(int id) => id & ImFontAtlasRectId_IndexMask_;
    public static uint ImFontAtlasRectId_GetGeneration(int id) => (uint)((uint)id & ImFontAtlasRectId_GenerationMask_) >> ImFontAtlasRectId_GenerationShift_;
    public static int ImFontAtlasRectId_Make(int index_idx, int gen_idx) => (int)(index_idx | (gen_idx << ImFontAtlasRectId_GenerationShift_));
}

// Output of ImFontAtlas::GetCustomRect()
public struct ImFontAtlasRect
{
    public ushort x, y;
    public ushort w, h;
    public ImVec2 uv0, uv1;
}

// Packed rectangle lookup entry
public struct ImFontAtlasRectEntry
{
    public int TargetIndex;
    public uint Generation;
    public bool IsUsed;
}

// [Internal] stb_truetype per-source data
public class ImGuiImplStbTrueTypeFontSrcData
{
    public StbTrueType.stbtt_fontinfo FontInfo = new StbTrueType.stbtt_fontinfo();
    public float ScaleFactor;
}

// ImFontAtlasBuilder
public class ImFontAtlasBuilder
{
    public List<StbRectPack.stbrp_node> PackNodes = new List<StbRectPack.stbrp_node>();
    public StbRectPack.stbrp_context PackContext = new StbRectPack.stbrp_context();
    public ImVector<ImTextureRect> Rects = new ImVector<ImTextureRect>();
    public ImVector<ImFontAtlasRectEntry> RectsIndex = new ImVector<ImFontAtlasRectEntry>();
    public ImVector<byte> TempBuffer = new ImVector<byte>();
    public int RectsIndexFreeListStart;
    public int RectsPackedCount;
    public int RectsPackedSurface;
    public int RectsDiscardedCount;
    public int RectsDiscardedSurface;
    public int FrameCount;
    public ImVec2i MaxRectSize;
    public ImVec2i MaxRectBounds;
    public bool LockDisableResize;
    public bool PreloadedAllGlyphsRanges;
    public ImVector<ImFontBaked> BakedPool = new ImVector<ImFontBaked>();
    public ImGuiStorage BakedMap = new ImGuiStorage();
    public int BakedDiscardedCount;
    public int PackIdMouseCursors;
    public int PackIdLinesTexData;

    public ImFontAtlasBuilder()
    {
        FrameCount = -1;
        RectsIndexFreeListStart = -1;
        PackIdMouseCursors = PackIdLinesTexData = -1;
    }
}

// Runtime data for multiple fonts, bake multiple fonts into a single texture
public partial class ImFontAtlas
{
    public ImFontAtlasFlags Flags;
    public int TexDesiredFormat;
    public int TexGlyphPadding;
    public int TexMinWidth;
    public int TexMinHeight;
    public int TexMaxWidth;
    public int TexMaxHeight;
    public object UserData;
    public ImTextureRef TexRef;
    public ImTextureData TexData;
    public ImVector<ImTextureData> TexList = new ImVector<ImTextureData>();
    public bool Locked;
    public bool RendererHasTextures;
    public bool TexIsBuilt;
    public bool TexPixelsUseColors;
    public ImVec2 TexUvScale;
    public ImVec2 TexUvWhitePixel;
    public ImVector<ImFont> Fonts = new ImVector<ImFont>();
    public ImVector<ImFontConfig> Sources = new ImVector<ImFontConfig>();
    public ImVec4[] TexUvLines = new ImVec4[ImDrawListConst.IM_DRAWLIST_TEX_LINES_WIDTH_MAX + 1];
    public int TexNextUniqueID;
    public int FontNextUniqueID;
    public ImVector<ImDrawListSharedData> DrawListSharedDatas = new ImVector<ImDrawListSharedData>();
    public ImFontAtlasBuilder Builder;
    public ImFontLoader FontLoader;
    public string FontLoaderName;
    public object FontLoaderData;
    public uint FontLoaderFlags;
    public int RefCount;
    public ImGuiContext OwnerContext;
    public ImFontAtlasRect TempRect;

    public ImFontAtlas()
    {
        TexDesiredFormat = ImTextureFormat_.ImTextureFormat_RGBA32;
        TexGlyphPadding = 1;
        TexMinWidth = 512;
        TexMinHeight = 128;
        TexMaxWidth = 8192;
        TexMaxHeight = 8192;
        TexRef = new ImTextureRef(ImUtil.ImTextureID_Invalid);
        RendererHasTextures = false;
        TexNextUniqueID = 1;
        FontNextUniqueID = 1;
        Builder = null;
    }

    public void Clear()
    {
        bool backup_renderer_has_textures = RendererHasTextures;
        RendererHasTextures = false;
        ClearFonts();
        ClearTexData();
        RendererHasTextures = backup_renderer_has_textures;
    }

    public void ClearFonts()
    {
        for (int i = 0; i < Fonts.Size; i++)
            ImFontAtlasBuildNotifySetFont(this, Fonts[i], null);
        ImFontAtlasBuildDestroy(this);
        ClearInputData();
        Fonts.clear();
        TexIsBuilt = false;
        for (int i = 0; i < DrawListSharedDatas.Size; i++)
        {
            var shared_data = DrawListSharedDatas[i];
            if (shared_data.FontAtlas == this)
            {
                shared_data.Font = null;
                shared_data.FontScale = shared_data.FontSize = 0.0f;
            }
        }
    }

    public void ClearInputData()
    {
        for (int i = 0; i < Fonts.Size; i++)
            ImFontAtlasFontDestroyOutput(this, Fonts[i]);
        for (int i = 0; i < Sources.Size; i++)
            ImFontAtlasFontDestroySourceData(this, Sources[i]);
        for (int i = 0; i < Fonts.Size; i++)
        {
            Fonts[i].Sources.clear();
            Fonts[i].Flags |= ImFontFlags_.ImFontFlags_NoLoadGlyphs;
        }
        Sources.clear();
    }

    public void ClearTexData()
    {
        for (int i = 0; i < TexList.Size; i++)
            TexList[i].DestroyPixels();
    }

    public void CompactCache() => ImFontAtlasTextureCompact(this);

    public void SetFontLoader(ImFontLoader font_loader) => ImFontAtlasBuildSetupFontLoader(this, font_loader);

    public void SetTexID(ImTextureID id)
    {
        System.Diagnostics.Debug.Assert(TexRef._TexID == ImUtil.ImTextureID_Invalid);
        TexRef._TexData.TexID = id;
    }

    public bool IsBuilt() => Fonts.Size > 0 && TexIsBuilt;

    // ---- Legacy API (GetTexDataAsRGBA32 / Build) ----

    static void GetTexDataAsFormat(ImFontAtlas atlas, int format, out byte[] out_pixels, out int out_width, out int out_height, out int out_bytes_per_pixel)
    {
        var tex = atlas.TexData;
        if (!atlas.TexIsBuilt || tex == null || tex.Pixels == null || atlas.TexDesiredFormat != format)
        {
            atlas.TexDesiredFormat = format;
            atlas.Build();
            tex = atlas.TexData;
        }
        out_pixels = tex.Pixels;
        out_width = tex.Width;
        out_height = tex.Height;
        out_bytes_per_pixel = tex.BytesPerPixel;
    }

    public void GetTexDataAsAlpha8(out byte[] out_pixels, out int out_width, out int out_height, out int out_bytes_per_pixel)
        => GetTexDataAsFormat(this, ImTextureFormat_.ImTextureFormat_Alpha8, out out_pixels, out out_width, out out_height, out out_bytes_per_pixel);

    public void GetTexDataAsRGBA32(out byte[] out_pixels, out int out_width, out int out_height, out int out_bytes_per_pixel)
        => GetTexDataAsFormat(this, ImTextureFormat_.ImTextureFormat_RGBA32, out out_pixels, out out_width, out out_height, out out_bytes_per_pixel);

    public bool Build()
    {
        ImFontAtlasBuildMain(this);
        return true;
    }

    // ---- Adding fonts ----

    public ImFont AddFont(ImFontConfig font_cfg_in)
    {
        System.Diagnostics.Debug.Assert((font_cfg_in.FontData != null && font_cfg_in.FontDataSize > 0) || (font_cfg_in.FontLoader != null));
        System.Diagnostics.Debug.Assert(font_cfg_in.RasterizerDensity > 0.0f);
        if (font_cfg_in.GlyphOffset.x != 0.0f || font_cfg_in.GlyphOffset.y != 0.0f || font_cfg_in.GlyphMinAdvanceX != 0.0f || font_cfg_in.GlyphMaxAdvanceX != float.MaxValue)
            System.Diagnostics.Debug.Assert(font_cfg_in.SizePixels != 0.0f);

        if (Builder == null)
            ImFontAtlasBuildInit(this);

        bool is_first_font = (Fonts.Size == 0);
        ImFont font;
        if (!font_cfg_in.MergeMode)
        {
            font = new ImFont();
            font.FontId = (uint)FontNextUniqueID++;
            font.Flags = font_cfg_in.Flags;
            font.LegacySize = font_cfg_in.SizePixels;
            font.CurrentRasterizerDensity = font_cfg_in.RasterizerDensity;
            Fonts.push_back(font);
        }
        else
        {
            System.Diagnostics.Debug.Assert(Fonts.Size > 0);
            font = font_cfg_in.DstFont != null ? font_cfg_in.DstFont : Fonts.back();
            ImFontAtlasFontDiscardBakes(this, font, 0);
        }

        Sources.push_back(font_cfg_in.Clone());
        ImFontConfig font_cfg = Sources.back();
        if (font_cfg.DstFont == null)
            font_cfg.DstFont = font;
        font.Sources.push_back(font_cfg);
        ImFontAtlasBuildUpdatePointers(this);

        if (font_cfg.GlyphExcludeRanges != null)
        {
            // (copy ownership like ImMemdup)
            int size = font_cfg.GlyphExcludeRanges.Length;
            var copy = new ushort[size];
            Array.Copy(font_cfg.GlyphExcludeRanges, copy, size);
            font_cfg.GlyphExcludeRanges = copy;
        }

        if (!ImFontAtlasFontSourceInit(this, font_cfg))
        {
            ImFontAtlasFontDestroySourceData(this, font_cfg);
            Sources.pop_back();
            font.Sources.pop_back();
            if (!font_cfg.MergeMode)
            {
                Fonts.pop_back();
            }
            return null;
        }
        ImFontAtlasFontSourceAddToFont(this, font, font_cfg);

        if (is_first_font)
            ImFontAtlasBuildNotifySetFont(this, null, font);
        return font;
    }

    public ImFont AddFontDefault(ImFontConfig font_cfg = null)
    {
        if (OwnerContext == null || ImFontAtlasGetExpectedContextFontSize(OwnerContext) >= 15.0f)
            return AddFontDefaultVector(font_cfg);
        else
            return AddFontDefaultBitmap(font_cfg);
    }

    // Load embedded ProggyClean.ttf. Default size 13, disable oversampling.
    public ImFont AddFontDefaultBitmap(ImFontConfig font_cfg_template)
    {
        var font_cfg = font_cfg_template != null ? font_cfg_template.Clone() : new ImFontConfig();
        if (font_cfg_template == null)
            font_cfg.PixelSnapH = true;
        if (font_cfg.SizePixels <= 0.0f)
        {
            font_cfg.SizePixels = 13.0f;
            font_cfg.Flags |= ImFontFlags_.ImFontFlags_ImplicitRefSize;
        }
        if (font_cfg.Name.Length == 0)
            font_cfg.Name = "ProggyClean.ttf";
        font_cfg.EllipsisChar = 0x0085;
        font_cfg.GlyphOffset.y += 1.0f * (font_cfg.SizePixels / 13.0f);

        var ttf_compressed = EmbeddedFonts.proggy_clean_ttf_compressed_data;
        int ttf_compressed_size = (int)EmbeddedFonts.proggy_clean_ttf_compressed_size;
        return AddFontFromMemoryCompressedTTF(ttf_compressed, ttf_compressed_size, font_cfg.SizePixels, font_cfg);
    }

    // Load a minimal version of ProggyForever, designed to match ProggyClean, but nicely scalable.
    public ImFont AddFontDefaultVector(ImFontConfig font_cfg_template)
    {
        var font_cfg = font_cfg_template != null ? font_cfg_template.Clone() : new ImFontConfig();
        if (font_cfg_template == null)
            font_cfg.PixelSnapH = true;
        if (font_cfg.SizePixels <= 0.0f)
        {
            font_cfg.SizePixels = 13.0f;
            font_cfg.Flags |= ImFontFlags_.ImFontFlags_ImplicitRefSize;
        }
        if (font_cfg.Name.Length == 0)
            font_cfg.Name = "ProggyForever.ttf";
        font_cfg.ExtraSizeScale *= 1.015f;
        font_cfg.GlyphOffset.y += 0.5f * (font_cfg.SizePixels / 16.0f);

        var ttf_compressed = EmbeddedFonts.proggy_forever_minimal_ttf_compressed_data;
        int ttf_compressed_size = (int)EmbeddedFonts.proggy_forever_minimal_ttf_compressed_size;
        return AddFontFromMemoryCompressedTTF(ttf_compressed, ttf_compressed_size, font_cfg.SizePixels, font_cfg);
    }

    public ImFont AddFontFromFileTTF(string filename, float size_pixels, ImFontConfig font_cfg_template = null, ushort[] glyph_ranges = null)
    {
        byte[] data;
        try { data = System.IO.File.ReadAllBytes(filename); }
        catch
        {
            if (font_cfg_template == null || (font_cfg_template.Flags & ImFontFlags_.ImFontFlags_NoLoadError) == 0)
                System.Diagnostics.Debug.Assert(false, "Could not load font file!");
            return null;
        }
        var font_cfg = font_cfg_template != null ? font_cfg_template.Clone() : new ImFontConfig();
        if (font_cfg.Name.Length == 0)
        {
            int p = filename.Length;
            while (p > 0 && filename[p - 1] != '/' && filename[p - 1] != '\\') p--;
            font_cfg.Name = filename.Substring(p);
        }
        return AddFontFromMemoryTTF(data, data.Length, size_pixels, font_cfg, glyph_ranges);
    }

    public ImFont AddFontFromMemoryTTF(byte[] font_data, int font_data_size, float size_pixels, ImFontConfig font_cfg_template = null, ushort[] glyph_ranges = null)
    {
        var font_cfg = font_cfg_template != null ? font_cfg_template.Clone() : new ImFontConfig();
        System.Diagnostics.Debug.Assert(font_cfg.FontData == null);
        System.Diagnostics.Debug.Assert(font_data_size > 100);
        font_cfg.FontData = font_data;
        font_cfg.FontDataSize = font_data_size;
        font_cfg.SizePixels = size_pixels > 0.0f ? size_pixels : font_cfg.SizePixels;
        if (glyph_ranges != null)
            font_cfg.GlyphRanges = glyph_ranges;
        return AddFont(font_cfg);
    }

    public ImFont AddFontFromMemoryCompressedTTF(byte[] compressed_ttf_data, int compressed_ttf_size, float size_pixels, ImFontConfig font_cfg_template = null, ushort[] glyph_ranges = null)
    {
        uint buf_decompressed_size = StbDecompress.stb_decompress_length(compressed_ttf_data);
        byte[] buf_decompressed_data = new byte[buf_decompressed_size];
        StbDecompress.stb_decompress(buf_decompressed_data, compressed_ttf_data, (uint)compressed_ttf_size);

        var font_cfg = font_cfg_template != null ? font_cfg_template.Clone() : new ImFontConfig();
        System.Diagnostics.Debug.Assert(font_cfg.FontData == null);
        font_cfg.FontDataOwnedByAtlas = true;
        return AddFontFromMemoryTTF(buf_decompressed_data, (int)buf_decompressed_size, size_pixels, font_cfg, glyph_ranges);
    }

    public ImFont AddFontFromMemoryCompressedBase85TTF(string compressed_ttf_data_base85, float size_pixels, ImFontConfig font_cfg = null, ushort[] glyph_ranges = null)
    {
        int compressed_ttf_size = (((compressed_ttf_data_base85.Length) + 4) / 5) * 4;
        byte[] compressed_ttf = new byte[compressed_ttf_size];
        StbDecompress.Decode85(System.Text.Encoding.ASCII.GetBytes(compressed_ttf_data_base85), 0, compressed_ttf);
        ImFont font = AddFontFromMemoryCompressedTTF(compressed_ttf, compressed_ttf_size, size_pixels, font_cfg, glyph_ranges);
        return font;
    }

    public void RemoveFont(ImFont font)
    {
        ImFontAtlasFontDestroyOutput(this, font);
        for (int i = 0; i < font.Sources.Size; i++)
            ImFontAtlasFontDestroySourceData(this, font.Sources[i]);
        for (int src_n = 0; src_n < Sources.Size; src_n++)
            if (Sources[src_n].DstFont == font)
                Sources.erase(src_n--);

        bool removed = Fonts.find_index_of_ref(font) != -1;
        if (removed)
            Fonts.erase(Fonts.index_of_ref(font));

        ImFontAtlasBuildUpdatePointers(this);
        font.OwnerAtlas = null;

        ImFont new_current_font = Fonts.Size > 0 ? Fonts[0] : null;
        ImFontAtlasBuildNotifySetFont(this, font, new_current_font);
    }

    // ---- Custom rects ----

    public int AddCustomRect(int width, int height, ref ImFontAtlasRect out_r)
    {
        if (Builder == null)
            ImFontAtlasBuildInit(this);
        int r_id = ImFontAtlasPackAddRect(this, width, height);
        if (r_id == ImUtil.ImFontAtlasRectId_Invalid)
            return ImUtil.ImFontAtlasRectId_Invalid;
        GetCustomRect(r_id, ref out_r);
        if (RendererHasTextures)
        {
            var r_opt = ImFontAtlasPackGetRectSafe(this, r_id);
            if (r_opt.HasValue)
            {
                var r = r_opt.Value;
                ImUtil.ImFontAtlasTextureBlockQueueUpload(this, TexData, r.x, r.y, r.w, r.h);
            }
        }
        return r_id;
    }

    public void RemoveCustomRect(int id)
    {
        if (ImFontAtlasPackGetRectSafe(this, id).HasValue == false)
            return;
        ImFontAtlasPackDiscardRect(this, id);
    }

    public bool GetCustomRect(int id, ref ImFontAtlasRect out_r)
    {
        var r_opt = ImFontAtlasPackGetRectSafe(this, id);
        if (!r_opt.HasValue)
            return false;
        var r = r_opt.Value;
        out_r.x = r.x;
        out_r.y = r.y;
        out_r.w = r.w;
        out_r.h = r.h;
        out_r.uv0 = new ImVec2(r.x, r.y) * TexUvScale;
        out_r.uv1 = new ImVec2(r.x + r.w, r.y + r.h) * TexUvScale;
        return true;
    }

    // C++ (obsolete): ImFontAtlasRectId ImFontAtlas::AddCustomRectFontGlyph(ImFont* font, ImWchar codepoint, int width, int height, float advance_x, const ImVec2& offset)
    // This API does not make sense anymore with scalable fonts; kept for API parity. Uses font.LegacySize.
    public int AddCustomRectFontGlyph(ImFont font, ushort codepoint, int width, int height, float advance_x, ImVec2 offset)
    {
        float font_size = font.LegacySize;
        return AddCustomRectFontGlyphForSize(font, font_size, codepoint, width, height, advance_x, offset);
    }

    // C++ (obsolete): ImFontAtlasRectId ImFontAtlas::AddCustomRectFontGlyphForSize(ImFont* font, float font_size, ImWchar codepoint, int width, int height, float advance_x, const ImVec2& offset)
    // FIXME: we automatically set glyph.Colored=true by default.
    public int AddCustomRectFontGlyphForSize(ImFont font, float font_size, ushort codepoint, int width, int height, float advance_x, ImVec2 offset)
    {
        System.Diagnostics.Debug.Assert(font != null);
        System.Diagnostics.Debug.Assert(width > 0 && width <= 0xFFFF);
        System.Diagnostics.Debug.Assert(height > 0 && height <= 0xFFFF);

        if (Builder == null)
            ImFontAtlasBuildInit(this);
        ImFontBaked baked = font.GetFontBaked(font_size);

        int r_id = ImFontAtlasPackAddRect(this, width, height);
        if (r_id == ImUtil.ImFontAtlasRectId_Invalid)
            return ImUtil.ImFontAtlasRectId_Invalid;
        var r = ImFontAtlasPackGetRect(this, r_id).Value;
        if (RendererHasTextures)
            ImUtil.ImFontAtlasTextureBlockQueueUpload(this, TexData, r.x, r.y, r.w, r.h);

        if (baked.IsGlyphLoaded(codepoint))
            ImFontAtlasBakedDiscardFontGlyph(this, font, baked, baked.FindGlyph(codepoint));

        var glyph = new ImFontGlyph();
        glyph.Codepoint = codepoint;
        glyph.AdvanceX = advance_x;
        glyph.X0 = offset.x;
        glyph.Y0 = offset.y;
        glyph.X1 = offset.x + r.w;
        glyph.Y1 = offset.y + r.h;
        glyph.Visible = 1;
        glyph.Colored = 1; // FIXME: Arbitrary
        ImFontAtlasBakedAddFontGlyph(this, baked, font.Sources[0], glyph);
        return r_id;
    }

    // ---- Glyph ranges ----

    public ushort[] GetGlyphRangesDefault() => ImFontRanges.Ranges_Default;
    public ushort[] GetGlyphRangesGreek() => ImFontRanges.Ranges_Greek;
    public ushort[] GetGlyphRangesKorean() => ImFontRanges.Ranges_Korean;
    public ushort[] GetGlyphRangesJapanese() => ImFontRanges.Ranges_Japanese;
    public ushort[] GetGlyphRangesCyrillic() => ImFontRanges.Ranges_Cyrillic;
    public ushort[] GetGlyphRangesThai() => ImFontRanges.Ranges_Thai;
    public ushort[] GetGlyphRangesChineseFull() => ImFontRanges.Ranges_ChineseFull;
    public ushort[] GetGlyphRangesChineseSimplifiedCommon() => ImFontRanges.Ranges_ChineseSimplifiedCommon;
    public ushort[] GetGlyphRangesVietnamese() => ImFontRanges.Ranges_Vietnamese;
}
