// Port of Dear ImGui font atlas build machinery (imgui_draw.cpp static functions).
using System;
using System.Collections.Generic;

namespace ImGui;

public static partial class ImUtil
{
    public static void ImFontAtlasTextureBlockQueueUpload(ImFontAtlas atlas, ImTextureData tex, int x, int y, int w, int h)
    {
        ImTextureDataQueueUpload(tex, x, y, w, h);
        atlas.TexIsBuilt = false;
    }
}

public partial class ImFontAtlas
{
    // Port of FONT_ATLAS_DEFAULT_TEX_DATA_* (the mouse cursor bitmaps)
    public const int FONT_ATLAS_DEFAULT_TEX_DATA_W = 122; // Actual texture will be 2 times that + 1 spacing.
    public const int FONT_ATLAS_DEFAULT_TEX_DATA_H = 27;
    public static readonly string FONT_ATLAS_DEFAULT_TEX_DATA_PIXELS =
        "..-         -XXXXXXX-    X    -           X           -XXXXXXX          -          XXXXXXX-     XX          - XX       XX " +
        "..-         -X.....X-   X.X   -          X.X          -X.....X          -          X.....X-    X..X         -X..X     X..X" +
        "---         -XXX.XXX-  X...X  -         X...X         -X....X           -           X....X-    X..X         -X...X   X...X" +
        "X           -  X.X  - X.....X -        X.....X        -X...X            -            X...X-    X..X         - X...X X...X " +
        "XX          -  X.X  -X.......X-       X.......X       -X..X.X           -           X.X..X-    X..X         -  X...X...X  " +
        "X.X         -  X.X  -XXXX.XXXX-       XXXX.XXXX       -X.X X.X          -          X.X X.X-    X..XXX       -   X.....X   " +
        "X..X        -  X.X  -   X.X   -          X.X          -XX   X.X         -         X.X   XX-    X..X..XXX    -    X...X    " +
        "X...X       -  X.X  -   X.X   -    XX    X.X    XX    -      X.X        -        X.X      -    X..X..X..XX  -     X.X     " +
        "X....X      -  X.X  -   X.X   -   X.X    X.X    X.X   -       X.X       -       X.X       -    X..X..X..X.X -    X...X    " +
        "X.....X     -  X.X  -   X.X   -  X..X    X.X    X..X  -        X.X      -      X.X        -XXX X..X..X..X..X-   X.....X   " +
        "X......X    -  X.X  -   X.X   - X...XXXXXX.XXXXXX...X -         X.X   XX-XX   X.X         -X..XX........X..X-  X...X...X  " +
        "X.......X   -  X.X  -   X.X   -X.....................X-          X.X X.X-X.X X.X          -X...X...........X- X...X X...X " +
        "X........X  -  X.X  -   X.X   - X...XXXXXX.XXXXXX...X -           X.X..X-X..X.X           - X..............X-X...X   X...X" +
        "X.........X -XXX.XXX-   X.X   -  X..X    X.X    X..X  -            X...X-X...X            -  X.............X-X..X     X..X" +
        "X..........X-X.....X-   X.X   -   X.X    X.X    X.X   -           X....X-X....X           -  X.............X- XX       XX " +
        "X......XXXXX-XXXXXXX-   X.X   -    XX    X.X    XX    -          X.....X-X.....X          -   X............X--------------" +
        "X...X..X    ---------   X.X   -          X.X          -          XXXXXXX-XXXXXXX          -   X...........X -             " +
        "X..X X..X   -       -XXXX.XXXX-       XXXX.XXXX       -------------------------------------    X..........X -             " +
        "X.X  X..X   -       -X.......X-       X.......X       -    XX           XX    -           -    X..........X -             " +
        "XX    X..X  -       - X.....X -        X.....X        -   X.X           X.X   -           -     X........X  -             " +
        "      X..X  -       -  X...X  -         X...X         -  X..X           X..X  -           -     X........X  -             " +
        "       XX   -       -   X.X   -          X.X          - X...XXXXXXXXXXXXX...X -           -     XXXXXXXXXX  -             " +
        "-------------       -    X    -           X           -X.....................X-           -------------------             " +
        "                    ----------------------------------- X...XXXXXXXXXXXXX...X -                                           " +
        "                                                      -  X..X           X..X  -                                           " +
        "                                                      -   X.X           X.X   -                                           " +
        "                                                      -    XX           XX    -                                           ";

    public static readonly ImVec2[][] FONT_ATLAS_DEFAULT_TEX_CURSOR_DATA = CreateCursorData();

    static ImVec2[][] CreateCursorData()
    {
        // Pos, Size, Offset per cursor
        return new ImVec2[][]
        {
            new[] { new ImVec2( 0,3), new ImVec2(12,19), new ImVec2( 0, 0) }, // ImGuiMouseCursor_Arrow
            new[] { new ImVec2(13,0), new ImVec2( 7,16), new ImVec2( 1, 8) }, // ImGuiMouseCursor_TextInput
            new[] { new ImVec2(31,0), new ImVec2(23,23), new ImVec2(11,11) }, // ImGuiMouseCursor_ResizeAll
            new[] { new ImVec2(21,0), new ImVec2( 9,23), new ImVec2( 4,11) }, // ImGuiMouseCursor_ResizeNS
            new[] { new ImVec2(55,18),new ImVec2(23, 9), new ImVec2(11, 4) }, // ImGuiMouseCursor_ResizeEW
            new[] { new ImVec2(73,0), new ImVec2(17,17), new ImVec2( 8, 8) }, // ImGuiMouseCursor_ResizeNESW
            new[] { new ImVec2(55,0), new ImVec2(17,17), new ImVec2( 8, 8) }, // ImGuiMouseCursor_ResizeNWSE
            new[] { new ImVec2(91,0), new ImVec2(17,22), new ImVec2( 5, 0) }, // ImGuiMouseCursor_Hand
            new[] { new ImVec2(0,3),  new ImVec2(12,19), new ImVec2( 0, 0) }, // ImGuiMouseCursor_Wait
            new[] { new ImVec2(0,3),  new ImVec2(12,19), new ImVec2( 0, 0) }, // ImGuiMouseCursor_Progress
            new[] { new ImVec2(109,0),new ImVec2(13,15), new ImVec2( 6, 7) }, // ImGuiMouseCursor_NotAllowed
        };
    }

    // Port of GetExpectedContextFontSize
    public static float ImFontAtlasGetExpectedContextFontSize(ImGuiContext ctx)
    {
        float fb_scale = (ctx.IO.DisplayFramebufferScale.x != 0.0f) ? ctx.IO.DisplayFramebufferScale.x : 1.0f;
        if (ctx.IO.DisplayFramebufferScale.x == 1.0f && ctx.IO.DisplaySize.x <= 0.0f && ctx.IO.ConfigMacOSXBehaviors)
            fb_scale = 2.0f;
        float scale = ctx.Style.FontScaleMain * ctx.Style.FontScaleDpi * fb_scale;
        return ((ctx.Style.FontSizeBase > 0.0f) ? ctx.Style.FontSizeBase : 13.0f) * scale;
    }

    static void ImFontAtlasBuildUpdateRendererHasTexturesFromContext(ImFontAtlas atlas)
    {
        for (int i = 0; i < atlas.DrawListSharedDatas.Size; i++)
        {
            var shared_data = atlas.DrawListSharedDatas[i];
            if (shared_data.Context != null)
            {
                atlas.RendererHasTextures = (shared_data.Context.IO.BackendFlags & ImGuiBackendFlags_.ImGuiBackendFlags_RendererHasTextures) != 0;
                break;
            }
        }
    }

    // Called by NewFrame() for atlases owned by a context.
    public static void ImFontAtlasUpdateNewFrame(ImFontAtlas atlas, int frame_count, bool renderer_has_textures)
    {
        atlas.RendererHasTextures = renderer_has_textures;

        for (int tex_n = 0; tex_n < atlas.TexList.Size; tex_n++)
        {
            var tex = atlas.TexList[tex_n];
            bool remove_from_list = ImUtil.ImTextureDataUpdateNewFrame(tex);
            if (remove_from_list)
            {
                tex.DestroyPixels();
                atlas.TexList.erase(tex_n);
                tex_n--;
            }
        }

        if (atlas.RendererHasTextures)
        {
            atlas.TexIsBuilt = true;
            if (atlas.Builder == null)
                ImFontAtlasBuildMain(atlas);
        }
        if (!atlas.RendererHasTextures)
            System.Diagnostics.Debug.Assert(atlas.TexIsBuilt, "Backend does not support ImGuiBackendFlags_RendererHasTextures, and font atlas is not built!");
        if (atlas.TexIsBuilt && atlas.Builder.PreloadedAllGlyphsRanges)
            System.Diagnostics.Debug.Assert(atlas.RendererHasTextures == false, "Called ImFontAtlas::Build() before ImGuiBackendFlags_RendererHasTextures got set!");

        var builder = atlas.Builder;
        builder.FrameCount = frame_count;
        for (int i = 0; i < atlas.Fonts.Size; i++)
            atlas.Fonts[i].LastBaked = null;

        // Garbage collect BakedPool
        if (builder.BakedDiscardedCount > 0)
        {
            int dst_n = 0;
            for (int src_n = 0; src_n < builder.BakedPool.Size; src_n++)
            {
                var p_src = builder.BakedPool[src_n];
                if (p_src.WantDestroy)
                    continue;
                builder.BakedPool[dst_n++] = p_src;
            }
            builder.BakedPool.Size -= builder.BakedDiscardedCount;
            builder.BakedDiscardedCount = 0;
        }
    }

    // ---- Texture block helpers ----

    public static void ImFontAtlasTextureBlockConvert(byte[] src_pixels, int src_fmt, int src_pitch, byte[] dst_pixels, int dst_offset, int dst_fmt, int dst_pitch, int w, int h)
    {
        if (src_fmt == dst_fmt)
        {
            int line_sz = w * ImUtil.ImTextureDataGetFormatBytesPerPixel(src_fmt);
            for (int ny = 0, so = 0, doff = dst_offset; ny < h; ny++, so += src_pitch, doff += dst_pitch)
                Array.Copy(src_pixels, so, dst_pixels, doff, line_sz);
        }
        else if (src_fmt == ImTextureFormat_.ImTextureFormat_Alpha8 && dst_fmt == ImTextureFormat_.ImTextureFormat_RGBA32)
        {
            for (int ny = 0, so = 0, doff = dst_offset; ny < h; ny++, so += src_pitch, doff += dst_pitch)
            {
                for (int nx = 0; nx < w; nx++)
                {
                    byte a = src_pixels[so + nx];
                    uint v = ImGuiCore.IM_COL32(255, 255, 255, a);
                    int o = doff + nx * 4;
                    dst_pixels[o + 0] = (byte)(v & 0xFF);
                    dst_pixels[o + 1] = (byte)((v >> 8) & 0xFF);
                    dst_pixels[o + 2] = (byte)((v >> 16) & 0xFF);
                    dst_pixels[o + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
        }
        else if (src_fmt == ImTextureFormat_.ImTextureFormat_RGBA32 && dst_fmt == ImTextureFormat_.ImTextureFormat_Alpha8)
        {
            for (int ny = 0, so = 0, doff = dst_offset; ny < h; ny++, so += src_pitch, doff += dst_pitch)
            {
                for (int nx = 0; nx < w; nx++)
                {
                    int o = so + nx * 4;
                    uint col = (uint)(src_pixels[o] | (src_pixels[o + 1] << 8) | (src_pixels[o + 2] << 16) | (src_pixels[o + 3] << 24));
                    dst_pixels[doff + nx] = (byte)((col >> ImGuiCore.IM_COL32_A_SHIFT) & 0xFF);
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.Assert(false);
        }
    }

    // Source buffer may be written to (used for in-place mods).
    public static void ImFontAtlasTextureBlockPostProcess(ImFontAtlasPostProcessData data)
    {
        if (data.FontSrc.RasterizerMultiply != 1.0f)
            ImFontAtlasTextureBlockPostProcessMultiply(data, data.FontSrc.RasterizerMultiply);
    }

    public static void ImFontAtlasTextureBlockPostProcessMultiply(ImFontAtlasPostProcessData data, float multiply_factor)
    {
        byte[] pixels = data.Pixels;
        int pitch = data.Pitch;
        if (data.Format == ImTextureFormat_.ImTextureFormat_Alpha8)
        {
            for (int ny = 0, off = data.Offset; ny < data.Height; ny++, off += pitch)
            {
                for (int nx = 0; nx < data.Width; nx++)
                {
                    uint v = Math.Min((uint)(pixels[off + nx] * multiply_factor), 255u);
                    pixels[off + nx] = (byte)v;
                }
            }
        }
        else if (data.Format == ImTextureFormat_.ImTextureFormat_RGBA32)
        {
            for (int ny = 0, off = data.Offset; ny < data.Height; ny++, off += pitch)
            {
                for (int nx = 0; nx < data.Width; nx++)
                {
                    int o = off + nx * 4;
                    uint col = (uint)(pixels[o] | (pixels[o + 1] << 8) | (pixels[o + 2] << 16) | (pixels[o + 3] << 24));
                    uint a = Math.Min((uint)(((col >> ImGuiCore.IM_COL32_A_SHIFT) & 0xFF) * multiply_factor), 255u);
                    col = ImGuiCore.IM_COL32((int)((col >> ImGuiCore.IM_COL32_R_SHIFT) & 0xFF), (int)((col >> ImGuiCore.IM_COL32_G_SHIFT) & 0xFF), (int)((col >> ImGuiCore.IM_COL32_B_SHIFT) & 0xFF), (int)a);
                    pixels[o + 0] = (byte)(col & 0xFF);
                    pixels[o + 1] = (byte)((col >> 8) & 0xFF);
                    pixels[o + 2] = (byte)((col >> 16) & 0xFF);
                    pixels[o + 3] = (byte)((col >> 24) & 0xFF);
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.Assert(false);
        }
    }

    public static void ImFontAtlasTextureBlockFill(ImTextureData dst_tex, int dst_x, int dst_y, int w, int h, ImU32 col)
    {
        if (dst_tex.Format == ImTextureFormat_.ImTextureFormat_Alpha8)
        {
            byte col_a = (byte)((col >> ImGuiCore.IM_COL32_A_SHIFT) & 0xFF);
            for (int y = 0; y < h; y++)
            {
                int o = dst_tex.GetPixelsAt(dst_x, dst_y + y);
                for (int x = 0; x < w; x++)
                    dst_tex.Pixels[o + x] = col_a;
            }
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                int o = dst_tex.GetPixelsAt(dst_x, dst_y + y);
                for (int x = 0; x < w; x++)
                {
                    dst_tex.Pixels[o + x * 4 + 0] = (byte)(col & 0xFF);
                    dst_tex.Pixels[o + x * 4 + 1] = (byte)((col >> 8) & 0xFF);
                    dst_tex.Pixels[o + x * 4 + 2] = (byte)((col >> 16) & 0xFF);
                    dst_tex.Pixels[o + x * 4 + 3] = (byte)((col >> 24) & 0xFF);
                }
            }
        }
    }

    public static void ImFontAtlasTextureBlockCopy(ImTextureData src_tex, int src_x, int src_y, ImTextureData dst_tex, int dst_x, int dst_y, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            int so = src_tex.GetPixelsAt(src_x, src_y + y);
            int doff = dst_tex.GetPixelsAt(dst_x, dst_y + y);
            Array.Copy(src_tex.Pixels, so, dst_tex.Pixels, doff, w * dst_tex.BytesPerPixel);
        }
    }
}

// Post-process data container
public class ImFontAtlasPostProcessData
{
    public ImFontAtlas FontAtlas;
    public ImFont Font;
    public ImFontConfig FontSrc;
    public ImFontBaked FontBaked;
    public ImFontGlyph Glyph;
    public byte[] Pixels;
    public int Offset;
    public int Format;
    public int Pitch;
    public int Width;
    public int Height;
}
