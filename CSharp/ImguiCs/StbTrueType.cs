// Port of the stb_truetype subset used by Dear ImGui (imstb_truetype.h).
// - Font table parsing, cmap lookup, glyph metrics and bitmap-box computation are
//   exact ports (they drive glyph layout and atlas UVs).
// - Coverage rasterizer: port of stbtt_GetGlyphShapeTT (glyf outlines incl. compound
//   glyphs) and the STBTT_RASTERIZER_VERSION==2 scanline rasterizer (stbtt_Rasterize,
//   stbtt__rasterize_sorted_edges, stbtt__fill_active_edges_new, prefilters).
using System;

namespace ImGui;

public static class StbTrueType
{
    // Big-endian readers (C macros ttBYTE/ttUSHORT/ttSHORT/ttULONG)
    public static int ttBYTE(byte[] d, int o) => d[o];
    public static int ttUSHORT(byte[] d, int o) => (d[o] << 8) | d[o + 1];
    public static int ttSHORT(byte[] d, int o) => (short)((d[o] << 8) | d[o + 1]);
    public static uint ttULONG(byte[] d, int o) => ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];

    const int STBTT_PLATFORM_ID_MICROSOFT = 3;
    const int STBTT_MS_EID_UNICODE_BMP = 1;
    const int STBTT_MS_EID_UNICODE_FULL = 10;
    const int STBTT_PLATFORM_ID_UNICODE = 0;

    public class stbtt_fontinfo
    {
        public byte[] data;
        public int fontstart;
        public int numGlyphs;
        public int loca, head, glyf, hhea, hmtx, kern, gpos, svg; // table locations
        public int index_map;
        public int indexToLocFormat;
    }

    static bool TagIs(byte[] d, int o, string tag)
    {
        for (int i = 0; i < tag.Length; i++)
            if (d[o + i] != (byte)tag[i]) return false;
        return true;
    }

    static bool TagIs4(byte[] d, int o, byte c0, byte c1, byte c2, byte c3)
        => d[o] == c0 && d[o + 1] == c1 && d[o + 2] == c2 && d[o + 3] == c3;

    static bool isfont(byte[] d, int fontstart)
    {
        int o = fontstart;
        if (TagIs4(d, o, (byte)'1', 0, 0, 0)) return true;
        if (TagIs(d, o, "typ1")) return true;
        if (TagIs(d, o, "OTTO")) return true;
        if (TagIs4(d, o, 0, 1, 0, 0)) return true;
        if (TagIs(d, o, "true")) return true;
        return false;
    }

    static uint find_table(byte[] d, int fontstart, string tag)
    {
        int num_tables = ttUSHORT(d, fontstart + 4);
        uint tabledir = (uint)fontstart + 12;
        for (int i = 0; i < num_tables; ++i)
        {
            uint loc = tabledir + (uint)(16 * i);
            if (TagIs(d, (int)loc, tag))
                return ttULONG(d, (int)loc + 8);
        }
        return 0;
    }

    public static int GetFontOffsetForIndex(byte[] font_collection, int index)
    {
        if (isfont(font_collection, 0))
            return index == 0 ? 0 : -1;
        if (TagIs(font_collection, 0, "ttcf"))
        {
            if (ttULONG(font_collection, 4) == 0x00010000 || ttULONG(font_collection, 4) == 0x00020000)
            {
                int n = ttSHORT(font_collection, 8);
                if (index >= n)
                    return -1;
                return (int)ttULONG(font_collection, 12 + index * 4);
            }
        }
        return -1;
    }

    public static bool InitFont(stbtt_fontinfo info, byte[] data, int fontstart)
    {
        info.data = data;
        info.fontstart = fontstart;

        uint cmap = find_table(data, fontstart, "cmap");
        info.loca = (int)find_table(data, fontstart, "loca");
        info.head = (int)find_table(data, fontstart, "head");
        info.glyf = (int)find_table(data, fontstart, "glyf");
        info.hhea = (int)find_table(data, fontstart, "hhea");
        info.hmtx = (int)find_table(data, fontstart, "hmtx");
        info.kern = (int)find_table(data, fontstart, "kern");
        info.gpos = (int)find_table(data, fontstart, "GPOS");

        if (cmap == 0 || info.head == 0 || info.hhea == 0 || info.hmtx == 0)
            return false;
        if (info.glyf != 0)
        {
            if (info.loca == 0) return false;
        }
        else
        {
            // CFF/OTF fonts are not supported by this port.
            return false;
        }

        uint t = find_table(data, fontstart, "maxp");
        info.numGlyphs = (t != 0) ? ttUSHORT(data, (int)t + 4) : 0xffff;
        info.svg = -1;

        // find a cmap encoding table we understand *now* to avoid searching later.
        int numTables = ttUSHORT(data, (int)cmap + 2);
        info.index_map = 0;
        for (int i = 0; i < numTables; ++i)
        {
            uint encoding_record = cmap + 4u + (uint)(8 * i);
            switch (ttUSHORT(data, (int)encoding_record))
            {
                case STBTT_PLATFORM_ID_MICROSOFT:
                    switch (ttUSHORT(data, (int)encoding_record + 2))
                    {
                        case STBTT_MS_EID_UNICODE_BMP:
                        case STBTT_MS_EID_UNICODE_FULL:
                            info.index_map = (int)(cmap + ttULONG(data, (int)encoding_record + 4));
                            break;
                    }
                    break;
                case STBTT_PLATFORM_ID_UNICODE:
                    info.index_map = (int)(cmap + ttULONG(data, (int)encoding_record + 4));
                    break;
            }
        }
        if (info.index_map == 0)
            return false;

        info.indexToLocFormat = ttUSHORT(data, info.head + 50);
        return true;
    }

    public static int FindGlyphIndex(stbtt_fontinfo info, int unicode_codepoint)
    {
        byte[] data = info.data;
        int index_map = info.index_map;

        int format = ttUSHORT(data, index_map + 0);
        if (format == 0)
        {
            int bytes = ttUSHORT(data, index_map + 2);
            if (unicode_codepoint < bytes - 6)
                return ttBYTE(data, index_map + 6 + unicode_codepoint);
            return 0;
        }
        else if (format == 6)
        {
            uint first = (uint)ttUSHORT(data, index_map + 6);
            uint count = (uint)ttUSHORT(data, index_map + 8);
            if ((uint)unicode_codepoint >= first && (uint)unicode_codepoint < first + count)
                return ttUSHORT(data, index_map + 10 + (int)((uint)unicode_codepoint - first) * 2);
            return 0;
        }
        else if (format == 2)
        {
            return 0; // @TODO: high-byte mapping for japanese/chinese/korean
        }
        else if (format == 4)
        {
            int segcount = ttUSHORT(data, index_map + 6) >> 1;
            int searchRange = ttUSHORT(data, index_map + 8) >> 1;
            int entrySelector = ttUSHORT(data, index_map + 10);
            int rangeShift = ttUSHORT(data, index_map + 12) >> 1;

            int endCount = index_map + 14;
            int search = endCount;

            if (unicode_codepoint > 0xffff)
                return 0;

            if (unicode_codepoint >= ttUSHORT(data, search + rangeShift * 2))
                search += rangeShift * 2;

            search -= 2;
            while (entrySelector != 0)
            {
                int end;
                searchRange >>= 1;
                end = ttUSHORT(data, search + searchRange * 2);
                if (unicode_codepoint > end)
                    search += searchRange * 2;
                --entrySelector;
            }
            search += 2;

            {
                int offset, start, last;
                int item = (search - endCount) >> 1;

                start = ttUSHORT(data, index_map + 14 + segcount * 2 + 2 + 2 * item);
                last = ttUSHORT(data, endCount + 2 * item);
                if (unicode_codepoint < start || unicode_codepoint > last)
                    return 0;

                offset = ttUSHORT(data, index_map + 14 + segcount * 6 + 2 + 2 * item);
                if (offset == 0)
                    return (short)(unicode_codepoint + ttSHORT(data, index_map + 14 + segcount * 4 + 2 + 2 * item)) & 0xffff;

                return ttUSHORT(data, offset + (unicode_codepoint - start) * 2 + index_map + 14 + segcount * 6 + 2 + 2 * item);
            }
        }
        else if (format == 12 || format == 13)
        {
            int ngroups = (int)ttULONG(data, index_map + 12);
            int low = 0, high = ngroups;
            while (low < high)
            {
                int mid = low + ((high - low) >> 1);
                uint start_char = ttULONG(data, index_map + 16 + mid * 12);
                uint end_char = ttULONG(data, index_map + 16 + mid * 12 + 4);
                if ((uint)unicode_codepoint < start_char)
                    high = mid;
                else if ((uint)unicode_codepoint > end_char)
                    low = mid + 1;
                else
                {
                    uint start_glyph = ttULONG(data, index_map + 16 + mid * 12 + 8);
                    if (format == 12)
                        return (int)(start_glyph + (uint)unicode_codepoint - start_char);
                    else
                        return (int)start_glyph;
                }
            }
            return 0;
        }
        return 0;
    }

    public static void GetFontVMetrics(stbtt_fontinfo info, out int ascent, out int descent, out int lineGap)
    {
        ascent = ttSHORT(info.data, info.hhea + 4);
        descent = ttSHORT(info.data, info.hhea + 6);
        lineGap = ttSHORT(info.data, info.hhea + 8);
    }

    public static float ScaleForPixelHeight(stbtt_fontinfo info, float pixels)
    {
        int fheight = ttSHORT(info.data, info.hhea + 4) - ttSHORT(info.data, info.hhea + 6);
        return pixels / fheight;
    }

    public static void GetGlyphHMetrics(stbtt_fontinfo info, int glyph_index, out int advanceWidth, out int leftSideBearing)
    {
        int numOfLongHorMetrics = ttUSHORT(info.data, info.hhea + 34);
        if (glyph_index < numOfLongHorMetrics)
        {
            advanceWidth = ttSHORT(info.data, info.hmtx + 4 * glyph_index);
            leftSideBearing = ttSHORT(info.data, info.hmtx + 4 * glyph_index + 2);
        }
        else
        {
            advanceWidth = ttSHORT(info.data, info.hmtx + 4 * (numOfLongHorMetrics - 1));
            leftSideBearing = ttSHORT(info.data, info.hmtx + 4 * numOfLongHorMetrics + 2 * (glyph_index - numOfLongHorMetrics));
        }
    }

    static int GetGlyfOffset(stbtt_fontinfo info, int glyph_index)
    {
        if (glyph_index >= info.numGlyphs) return -1;
        if (info.indexToLocFormat >= 2) return -1;

        int g1, g2;
        if (info.indexToLocFormat == 0)
        {
            g1 = info.glyf + ttUSHORT(info.data, info.loca + glyph_index * 2) * 2;
            g2 = info.glyf + ttUSHORT(info.data, info.loca + glyph_index * 2 + 2) * 2;
        }
        else
        {
            g1 = info.glyf + (int)ttULONG(info.data, info.loca + glyph_index * 4);
            g2 = info.glyf + (int)ttULONG(info.data, info.loca + glyph_index * 4 + 4);
        }
        return g1 == g2 ? -1 : g1;
    }

    public static bool GetGlyphBox(stbtt_fontinfo info, int glyph_index, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = y0 = x1 = y1 = 0;
        int g = GetGlyfOffset(info, glyph_index);
        if (g < 0) return false;

        x0 = ttSHORT(info.data, g + 2);
        y0 = ttSHORT(info.data, g + 4);
        x1 = ttSHORT(info.data, g + 6);
        y1 = ttSHORT(info.data, g + 8);
        return true;
    }

    // STBTT_ifloor / STBTT_iceil: floor/ceil to int
    static int ifloor(float x) => (int)Math.Floor(x);
    static int iceil(float x) => (int)Math.Ceiling(x);

    public static void GetGlyphBitmapBoxSubpixel(stbtt_fontinfo font, int glyph, float scale_x, float scale_y, float shift_x, float shift_y, out int ix0, out int iy0, out int ix1, out int iy1)
    {
        int x0 = 0, y0 = 0, x1, y1;
        if (!GetGlyphBox(font, glyph, out x0, out y0, out x1, out y1))
        {
            // e.g. space character
            ix0 = 0; iy0 = 0; ix1 = 0; iy1 = 0;
        }
        else
        {
            ix0 = ifloor(x0 * scale_x + shift_x);
            iy0 = ifloor(-y1 * scale_y + shift_y);
            ix1 = iceil(x1 * scale_x + shift_x);
            iy1 = iceil(-y0 * scale_y + shift_y);
        }
    }

    public static void GetGlyphBitmapBox(stbtt_fontinfo font, int glyph, float scale_x, float scale_y, out int ix0, out int iy0, out int ix1, out int iy1)
        => GetGlyphBitmapBoxSubpixel(font, glyph, scale_x, scale_y, 0.0f, 0.0f, out ix0, out iy0, out ix1, out iy1);

    static float oversample_shift(int oversample)
    {
        if (oversample == 0)
            return 0.0f;
        return -(float)(oversample - 1) / (2.0f * oversample);
    }

    public static int GetGlyphKernAdvance(stbtt_fontinfo info, int glyph1, int glyph2)
    {
        // Port of stbtt__GetKernTable-based lookup for legacy 'kern' tables (format 0 only).
        // Fonts without a kern table (e.g. ProggyClean) return 0, matching the C++ build.
        int kerning = 0;
        if (info.kern != 0)
        {
            byte[] data = info.data;
            int needle = info.kern + 6;
            int min = 0, max = ttUSHORT(data, info.kern + 4) - 1;
            while (min <= max)
            {
                int mid = (min + max) >> 1;
                int off = needle + mid * 6;
                int g1 = ttUSHORT(data, off);
                int g2 = ttUSHORT(data, off + 2);
                if (g1 == glyph1 && g2 == glyph2)
                {
                    kerning = ttSHORT(data, off + 4);
                    break;
                }
                else if (g1 < glyph1 || (g1 == glyph1 && g2 < glyph2))
                    min = mid + 1;
                else
                    max = mid - 1;
            }
        }
        return kerning;
    }

    // ------------------------------------------------------------------------
    // Glyph outline extraction (stbtt__GetGlyphShapeTT, glyf outlines only; this
    // port does not support CFF/OTF so stbtt_GetGlyphShape always dispatches here)
    // ------------------------------------------------------------------------

    const byte STBTT_vmove = 1;
    const byte STBTT_vline = 2;
    const byte STBTT_vcurve = 3;
    const byte STBTT_vcubic = 4;

    public class stbtt_vertex
    {
        public byte type;
        public short x, y, cx, cy, cx1, cy1;
    }

    static void stbtt_setvertex(stbtt_vertex v, byte type, int x, int y, int cx, int cy)
    {
        v.type = type;
        v.x = (short)x;
        v.y = (short)y;
        v.cx = (short)cx;
        v.cy = (short)cy;
    }

    static int ttCHAR(byte[] d, int o) => (sbyte)d[o];

    static int stbtt__close_shape(stbtt_vertex[] vertices, int num_vertices, int was_off, int start_off,
        int sx, int sy, int scx, int scy, int cx, int cy)
    {
        if (start_off != 0)
        {
            if (was_off != 0)
                stbtt_setvertex(vertices[num_vertices++], STBTT_vcurve, (cx + scx) >> 1, (cy + scy) >> 1, cx, cy);
            stbtt_setvertex(vertices[num_vertices++], STBTT_vcurve, sx, sy, scx, scy);
        }
        else
        {
            if (was_off != 0)
                stbtt_setvertex(vertices[num_vertices++], STBTT_vcurve, sx, sy, cx, cy);
            else
                stbtt_setvertex(vertices[num_vertices++], STBTT_vline, sx, sy, 0, 0);
        }
        return num_vertices;
    }

    public static int GetGlyphShape(stbtt_fontinfo info, int glyph_index, out stbtt_vertex[] pvertices)
    {
        pvertices = null;
        byte[] data = info.data;
        int g = GetGlyfOffset(info, glyph_index);
        if (g < 0) return 0;

        short numberOfContours = (short)ttUSHORT(data, g);
        stbtt_vertex[] vertices = null;
        int num_vertices = 0;

        if (numberOfContours > 0)
        {
            byte flags = 0, flagcount = 0;
            int ins, i, j = 0, m, n, next_move, was_off = 0, start_off = 0;
            int x, y, cx, cy, sx, sy, scx, scy;
            int points;
            int endPtsOfContours = g + 10;
            ins = ttUSHORT(data, g + 10 + numberOfContours * 2);
            points = g + 10 + numberOfContours * 2 + 2 + ins;

            n = 1 + ttUSHORT(data, endPtsOfContours + numberOfContours * 2 - 2);

            m = n + 2 * numberOfContours;  // a loose bound on how many vertices we might need
            vertices = new stbtt_vertex[m];
            for (i = 0; i < m; ++i)
                vertices[i] = new stbtt_vertex();

            next_move = 0;
            flagcount = 0;

            // in first pass, we load uninterpreted data into the allocated array
            // above, shifted to the end of the array so we won't overwrite it when
            // we create our final data starting from the front

            int off = m - n; // starting offset for uninterpreted data

            // first load flags
            for (i = 0; i < n; ++i)
            {
                if (flagcount == 0)
                {
                    flags = (byte)data[points++];
                    if ((flags & 8) != 0)
                        flagcount = data[points++];
                }
                else
                    --flagcount;
                vertices[off + i].type = flags;
            }

            // now load x coordinates
            x = 0;
            for (i = 0; i < n; ++i)
            {
                flags = vertices[off + i].type;
                if ((flags & 2) != 0)
                {
                    int dx = data[points++];
                    x += (flags & 16) != 0 ? dx : -dx;
                }
                else
                {
                    if ((flags & 16) == 0)
                    {
                        x = x + (short)(data[points] * 256 + data[points + 1]);
                        points += 2;
                    }
                }
                vertices[off + i].x = (short)x;
            }

            // now load y coordinates
            y = 0;
            for (i = 0; i < n; ++i)
            {
                flags = vertices[off + i].type;
                if ((flags & 4) != 0)
                {
                    int dy = data[points++];
                    y += (flags & 32) != 0 ? dy : -dy;
                }
                else
                {
                    if ((flags & 32) == 0)
                    {
                        y = y + (short)(data[points] * 256 + data[points + 1]);
                        points += 2;
                    }
                }
                vertices[off + i].y = (short)y;
            }

            // now convert them to our format
            num_vertices = 0;
            sx = sy = cx = cy = scx = scy = 0;
            for (i = 0; i < n; ++i)
            {
                flags = vertices[off + i].type;
                x = vertices[off + i].x;
                y = vertices[off + i].y;

                if (next_move == i)
                {
                    if (i != 0)
                        num_vertices = stbtt__close_shape(vertices, num_vertices, was_off, start_off, sx, sy, scx, scy, cx, cy);

                    // now start the new one
                    start_off = (flags & 1) == 0 ? 1 : 0;
                    if (start_off != 0)
                    {
                        // if we start off with an off-curve point, then when we need to find a point on the curve
                        // where we can start, and we need to save some state for when we wraparound.
                        scx = x;
                        scy = y;
                        if ((vertices[off + i + 1].type & 1) == 0)
                        {
                            // next point is also a curve point, so interpolate an on-point curve
                            sx = (x + vertices[off + i + 1].x) >> 1;
                            sy = (y + vertices[off + i + 1].y) >> 1;
                        }
                        else
                        {
                            // otherwise just use the next point as our start point
                            sx = vertices[off + i + 1].x;
                            sy = vertices[off + i + 1].y;
                            ++i; // we're using point i+1 as the starting point, so skip it
                        }
                    }
                    else
                    {
                        sx = x;
                        sy = y;
                    }
                    stbtt_setvertex(vertices[num_vertices++], STBTT_vmove, sx, sy, 0, 0);
                    was_off = 0;
                    next_move = 1 + ttUSHORT(data, endPtsOfContours + j * 2);
                    ++j;
                }
                else
                {
                    if ((flags & 1) == 0)
                    { // if it's a curve
                        if (was_off != 0) // two off-curve control points in a row means interpolate an on-curve midpoint
                            stbtt_setvertex(vertices[num_vertices++], STBTT_vcurve, (cx + x) >> 1, (cy + y) >> 1, cx, cy);
                        cx = x;
                        cy = y;
                        was_off = 1;
                    }
                    else
                    {
                        if (was_off != 0)
                            stbtt_setvertex(vertices[num_vertices++], STBTT_vcurve, x, y, cx, cy);
                        else
                            stbtt_setvertex(vertices[num_vertices++], STBTT_vline, x, y, 0, 0);
                        was_off = 0;
                    }
                }
            }
            num_vertices = stbtt__close_shape(vertices, num_vertices, was_off, start_off, sx, sy, scx, scy, cx, cy);
        }
        else if (numberOfContours < 0)
        {
            // Compound shapes.
            int more = 1;
            int comp = g + 10;
            num_vertices = 0;
            vertices = null;
            while (more != 0)
            {
                ushort cflags, gidx;
                int comp_num_verts, i;
                stbtt_vertex[] comp_verts;

                cflags = (ushort)ttUSHORT(data, comp); comp += 2;
                gidx = (ushort)ttUSHORT(data, comp); comp += 2;

                float m00 = 1, m01 = 0, m10 = 0, m11 = 1, m20 = 0, m21 = 0;

                if ((cflags & 2) != 0)
                { // XY values
                    if ((cflags & 1) != 0)
                    { // shorts
                        m20 = ttSHORT(data, comp); comp += 2;
                        m21 = ttSHORT(data, comp); comp += 2;
                    }
                    else
                    {
                        m20 = ttCHAR(data, comp); comp += 1;
                        m21 = ttCHAR(data, comp); comp += 1;
                    }
                }
                else
                {
                    // @TODO handle matching point
                }
                if ((cflags & (1 << 3)) != 0)
                { // WE_HAVE_A_SCALE
                    m00 = m11 = ttSHORT(data, comp) / 16384.0f; comp += 2;
                }
                else if ((cflags & (1 << 6)) != 0)
                { // WE_HAVE_AN_X_AND_YSCALE
                    m00 = ttSHORT(data, comp) / 16384.0f; comp += 2;
                    m01 = 0; m10 = 0;
                    m11 = ttSHORT(data, comp) / 16384.0f; comp += 2;
                }
                else if ((cflags & (1 << 7)) != 0)
                { // WE_HAVE_A_TWO_BY_TWO
                    m00 = ttSHORT(data, comp) / 16384.0f; comp += 2;
                    m01 = ttSHORT(data, comp) / 16384.0f; comp += 2;
                    m10 = ttSHORT(data, comp) / 16384.0f; comp += 2;
                    m11 = ttSHORT(data, comp) / 16384.0f; comp += 2;
                }

                // Find transformation scales.
                float mm = MathF.Sqrt(m00 * m00 + m01 * m01);
                float nn = MathF.Sqrt(m10 * m10 + m11 * m11);

                // Get indexed glyph.
                comp_num_verts = GetGlyphShape(info, gidx, out comp_verts);
                if (comp_num_verts > 0)
                {
                    // Transform vertices.
                    for (i = 0; i < comp_num_verts; ++i)
                    {
                        stbtt_vertex v = comp_verts[i];
                        float x0 = v.x, y0 = v.y;
                        v.x = (short)(mm * (m00 * x0 + m10 * y0 + m20));
                        v.y = (short)(nn * (m01 * x0 + m11 * y0 + m21));
                        float cxx = v.cx, cyy = v.cy;
                        v.cx = (short)(mm * (m00 * cxx + m10 * cyy + m20));
                        v.cy = (short)(nn * (m01 * cxx + m11 * cyy + m21));
                    }
                    // Append vertices.
                    int base_count = num_vertices;
                    stbtt_vertex[] tmp = new stbtt_vertex[num_vertices + comp_num_verts];
                    for (i = 0; i < num_vertices; ++i)
                        tmp[i] = vertices[i];
                    for (i = 0; i < comp_num_verts; ++i)
                        tmp[num_vertices + i] = comp_verts[i];
                    vertices = tmp;
                    num_vertices += comp_num_verts;
                }
                // More components ?
                more = cflags & (1 << 5);
            }
        }
        // numberOfContours == 0: do nothing

        pvertices = vertices;
        return num_vertices;
    }

    // ------------------------------------------------------------------------
    // Coverage rasterizer (STBTT_RASTERIZER_VERSION == 2)
    // ------------------------------------------------------------------------

    struct stbtt__point { public float x, y; }

    struct stbtt__edge
    {
        public float x0, y0, x1, y1;
        public int invert;
    }

    class stbtt__active_edge
    {
        public stbtt__active_edge next;
        public float fx, fdx, fdy;
        public float direction;
        public float sy;
        public float ey;
    }

    // hheap substitute: GC handles chunk storage, keep a free list for reuse
    class stbtt__hheap
    {
        public stbtt__active_edge first_free;
        public stbtt__active_edge Alloc() { var z = first_free; if (z != null) { first_free = z.next; z.next = null; return z; } return new stbtt__active_edge(); }
        public void Free(stbtt__active_edge z) { z.next = first_free; first_free = z; }
    }

    static stbtt__active_edge stbtt__new_active(stbtt__hheap hh, ref stbtt__edge e, int off_x, float start_point)
    {
        stbtt__active_edge z = hh.Alloc();
        float dxdy = (e.x1 - e.x0) / (e.y1 - e.y0);
        z.fdx = dxdy;
        z.fdy = dxdy != 0.0f ? (1.0f / dxdy) : 0.0f;
        z.fx = e.x0 + dxdy * (start_point - e.y0);
        z.fx -= off_x;
        z.direction = e.invert != 0 ? 1.0f : -1.0f;
        z.sy = e.y0;
        z.ey = e.y1;
        z.next = null;
        return z;
    }

    // the edge passed in here does not cross the vertical line at x or the vertical line at x+1
    // (i.e. it has already been clipped to those)
    static void stbtt__handle_clipped_edge(float[] scanline, int x, stbtt__active_edge e, float x0, float y0, float x1, float y1)
    {
        if (y0 == y1) return;
        if (y0 > e.ey) return;
        if (y1 < e.sy) return;
        if (y0 < e.sy)
        {
            x0 += (x1 - x0) * (e.sy - y0) / (y1 - y0);
            y0 = e.sy;
        }
        if (y1 > e.ey)
        {
            x1 += (x1 - x0) * (e.ey - y1) / (y1 - y0);
            y1 = e.ey;
        }

        if (x0 <= x && x1 <= x)
            scanline[x] += e.direction * (y1 - y0);
        else if (x0 >= x + 1 && x1 >= x + 1)
            ;
        else
            scanline[x] += e.direction * (y1 - y0) * (1 - ((x0 - x) + (x1 - x)) / 2); // coverage = 1 - average x position
    }

    static float stbtt__sized_trapezoid_area(float height, float top_width, float bottom_width)
    {
        return (top_width + bottom_width) / 2.0f * height;
    }

    static float stbtt__position_trapezoid_area(float height, float tx0, float tx1, float bx0, float bx1)
    {
        return stbtt__sized_trapezoid_area(height, tx1 - tx0, bx1 - bx0);
    }

    static float stbtt__sized_triangle_area(float height, float width)
    {
        return height * width / 2;
    }

    // C++ passes scanline2+1 as scanline_fill; here scanline2 is passed raw and all
    // fill accesses use +1 (C++ scanline_fill[i] == scanline2[i+1]; scanline_fill-1
    // as handle_clipped_edge target == scanline2 unshifted).
    static void stbtt__fill_active_edges_new(float[] scanline, float[] scanline_fill, int len, stbtt__active_edge e, float y_top)
    {
        float y_bottom = y_top + 1;

        while (e != null)
        {
            // brute force every pixel

            // compute intersection points with top & bottom

            if (e.fdx == 0)
            {
                float x0 = e.fx;
                if (x0 < len)
                {
                    if (x0 >= 0)
                    {
                        stbtt__handle_clipped_edge(scanline, (int)x0, e, x0, y_top, x0, y_bottom);
                        stbtt__handle_clipped_edge(scanline_fill, (int)x0 + 1, e, x0, y_top, x0, y_bottom); // C++: (scanline_fill-1, x0+1)
                    }
                    else
                    {
                        stbtt__handle_clipped_edge(scanline_fill, 0, e, x0, y_top, x0, y_bottom); // C++: (scanline_fill-1, 0)
                    }
                }
            }
            else
            {
                float x0 = e.fx;
                float dx = e.fdx;
                float xb = x0 + dx;
                float x_top, x_bottom;
                float sy0, sy1;
                float dy = e.fdy;

                // compute endpoints of line segment clipped to this scanline (if the
                // line segment starts on this scanline. x0 is the intersection of the
                // line with y_top, but that may be off the line segment.
                if (e.sy > y_top)
                {
                    x_top = x0 + dx * (e.sy - y_top);
                    sy0 = e.sy;
                }
                else
                {
                    x_top = x0;
                    sy0 = y_top;
                }
                if (e.ey < y_bottom)
                {
                    x_bottom = x0 + dx * (e.ey - y_top);
                    sy1 = e.ey;
                }
                else
                {
                    x_bottom = xb;
                    sy1 = y_bottom;
                }

                if (x_top >= 0 && x_bottom >= 0 && x_top < len && x_bottom < len)
                {
                    // from here on, we don't have to range check x values

                    if ((int)x_top == (int)x_bottom)
                    {
                        // simple case, only spans one pixel
                        int x = (int)x_top;
                        float height = (sy1 - sy0) * e.direction;
                        scanline[x] += stbtt__position_trapezoid_area(height, x_top, x + 1.0f, x_bottom, x + 1.0f);
                        scanline_fill[x + 1] += height; // everything right of this pixel is filled
                    }
                    else
                    {
                        int x, x1, x2;
                        // covers 2+ pixels
                        if (x_top > x_bottom)
                        {
                            // flip scanline vertically; signed area is the same
                            float t;
                            sy0 = y_bottom - (sy0 - y_top);
                            sy1 = y_bottom - (sy1 - y_top);
                            t = sy0; sy0 = sy1; sy1 = t;
                            t = x_bottom; x_bottom = x_top; x_top = t;
                            dx = -dx;
                            dy = -dy;
                            t = x0; x0 = xb; xb = t;
                        }

                        x1 = (int)x_top;
                        x2 = (int)x_bottom;
                        // compute intersection with y axis at x1+1
                        float y_crossing = y_top + dy * (x1 + 1 - x0);

                        // compute intersection with y axis at x2
                        float y_final = y_top + dy * (x2 - x0);

                        // if x2 is right at the right edge of x1, y_crossing can blow up, github #1057
                        if (y_crossing > y_bottom)
                            y_crossing = y_bottom;

                        float sign = e.direction;

                        // area of the rectangle covered from sy0..y_crossing
                        float area = sign * (y_crossing - sy0);

                        // area of the triangle (x_top,sy0), (x1+1,sy0), (x1+1,y_crossing)
                        scanline[x1] += stbtt__sized_triangle_area(area, x1 + 1 - x_top);

                        // check if final y_crossing is blown up; no test case for this
                        if (y_final > y_bottom)
                        {
                            int denom = (x2 - (x1 + 1));
                            y_final = y_bottom;
                            if (denom != 0)
                            {
                                dy = (y_final - y_crossing) / denom;
                            }
                        }

                        // in second pixel, area covered by line segment found in first pixel
                        // is always a rectangle 1 wide * the height of that line segment; this
                        // is exactly what the variable 'area' stores.
                        float step = sign * dy * 1;

                        for (x = x1 + 1; x < x2; ++x)
                        {
                            scanline[x] += area + step / 2;
                            area += step;
                        }

                        // area covered in the last pixel is the rectangle from all the pixels to the left,
                        // plus the trapezoid filled by the line segment in this pixel all the way to the right edge
                        scanline[x2] += area + sign * stbtt__position_trapezoid_area(sy1 - y_final, x2, x2 + 1.0f, x_bottom, x2 + 1.0f);

                        // the rest of the line is filled based on the total height of the line segment in this pixel
                        scanline_fill[x2 + 1] += sign * (sy1 - sy0);
                    }
                }
                else
                {
                    // if edge goes outside of box we're drawing, we require
                    // clipping logic. since this does not match the intended use
                    // of this library, we use a different, very slow brute force implementation
                    int x;
                    for (x = 0; x < len; ++x)
                    {
                        // rename variables to clearly-defined pairs
                        float y0 = y_top;
                        float x1 = x;
                        float x2 = x + 1;
                        float x3 = xb;
                        float y3 = y_bottom;

                        float y1 = (x - x0) / dx + y_top;
                        float y2 = (x + 1 - x0) / dx + y_top;

                        if (x0 < x1 && x3 > x2)
                        {         // three segments descending down-right
                            stbtt__handle_clipped_edge(scanline, x, e, x0, y0, x1, y1);
                            stbtt__handle_clipped_edge(scanline, x, e, x1, y1, x2, y2);
                            stbtt__handle_clipped_edge(scanline, x, e, x2, y2, x3, y3);
                        }
                        else if (x3 < x1 && x0 > x2)
                        {  // three segments descending down-left
                            stbtt__handle_clipped_edge(scanline, x, e, x0, y0, x2, y2);
                            stbtt__handle_clipped_edge(scanline, x, e, x2, y2, x1, y1);
                            stbtt__handle_clipped_edge(scanline, x, e, x1, y1, x3, y3);
                        }
                        else if (x0 < x1 && x3 > x1)
                        {  // two segments across x, down-right
                            stbtt__handle_clipped_edge(scanline, x, e, x0, y0, x1, y1);
                            stbtt__handle_clipped_edge(scanline, x, e, x1, y1, x3, y3);
                        }
                        else if (x3 < x1 && x0 > x1)
                        {  // two segments across x, down-left
                            stbtt__handle_clipped_edge(scanline, x, e, x0, y0, x1, y1);
                            stbtt__handle_clipped_edge(scanline, x, e, x1, y1, x3, y3);
                        }
                        else if (x0 < x2 && x3 > x2)
                        {  // two segments across x+1, down-right
                            stbtt__handle_clipped_edge(scanline, x, e, x0, y0, x2, y2);
                            stbtt__handle_clipped_edge(scanline, x, e, x2, y2, x3, y3);
                        }
                        else if (x3 < x2 && x0 > x2)
                        {  // two segments across x+1, down-left
                            stbtt__handle_clipped_edge(scanline, x, e, x0, y0, x2, y2);
                            stbtt__handle_clipped_edge(scanline, x, e, x2, y2, x3, y3);
                        }
                        else
                        {  // one segment
                            stbtt__handle_clipped_edge(scanline, x, e, x0, y0, x3, y3);
                        }
                    }
                }
            }
            e = e.next;
        }
    }

    // directly AA rasterize edges w/o supersampling
    static void stbtt__rasterize_sorted_edges(stbtt__bitmap result, stbtt__edge[] e, int n, int vsubsample, int off_x, int off_y)
    {
        stbtt__hheap hh = new stbtt__hheap();
        stbtt__active_edge active = null;
        int y, j = 0, i;
        int ei = 0;
        float[] scanline, scanline2;

        if (result.w > 64)
            scanline = new float[result.w * 2 + 1];
        else
            scanline = new float[129];

        scanline2 = new float[scanline.Length];
        // (C++ overlaps scanline2 onto scanline + result->w; independent arrays behave identically
        // because each is zeroed per scanline)

        y = off_y;
        {
            var sentinel = e[n];
            sentinel.y0 = off_y + result.h + 1;
            e[n] = sentinel;
        }

        while (j < result.h)
        {
            // find center of pixel for this scanline
            float scan_y_top = y + 0.0f;
            float scan_y_bottom = y + 1.0f;
            stbtt__active_edge step = active;
            stbtt__active_edge prev = null;

            Array.Clear(scanline, 0, result.w);
            Array.Clear(scanline2, 0, result.w + 1);

            // update all active edges;
            // remove all active edges that terminate before the top of this scanline
            while (step != null)
            {
                stbtt__active_edge z = step;
                if (z.ey <= scan_y_top)
                {
                    // delete from list
                    if (prev == null) active = z.next; else prev.next = z.next;
                    hh.Free(z);
                    step = (prev == null) ? active : prev.next;
                }
                else
                {
                    prev = step;
                    step = step.next;
                }
            }

            // insert all edges that start before the bottom of this scanline
            while (e[ei].y0 <= scan_y_bottom)
            {
                if (e[ei].y0 != e[ei].y1)
                {
                    stbtt__active_edge z = stbtt__new_active(hh, ref e[ei], off_x, scan_y_top);
                    if (j == 0 && off_y != 0)
                    {
                        if (z.ey < scan_y_top)
                        {
                            // this can happen due to subpixel positioning and some kind of fp rounding error i think
                            z.ey = scan_y_top;
                        }
                    }
                    // insert at front
                    z.next = active;
                    active = z;
                }
                ++ei;
            }

            // now process all active edges
            if (active != null)
                stbtt__fill_active_edges_new(scanline, scanline2, result.w, active, scan_y_top);

            {
                float sum = 0;
                for (i = 0; i < result.w; ++i)
                {
                    float k;
                    int mmm;
                    sum += scanline2[i];
                    k = scanline[i] + sum;
                    k = MathF.Abs(k) * 255 + 0.5f;
                    mmm = (int)k;
                    if (mmm > 255) mmm = 255;
                    result.pixels[j * result.stride + i] = (byte)mmm;
                }
            }
            // advance all the edges
            step = active;
            while (step != null)
            {
                step.fx += step.fdx;
                step = step.next;
            }

            ++y;
            ++j;
        }
    }

    static bool stbtt__edge_compare(ref stbtt__edge a, ref stbtt__edge b) => a.y0 < b.y0;

    static void stbtt__sort_edges_ins_sort(stbtt__edge[] p, int n)
    {
        for (int i = 1; i < n; ++i)
        {
            var t = p[i];
            int j = i;
            while (j > 0)
            {
                if (!(t.y0 < p[j - 1].y0)) break;
                p[j] = p[j - 1];
                --j;
            }
            if (i != j)
                p[j] = t;
        }
    }

    static void stbtt__sort_edges_quicksort(stbtt__edge[] p, int n)
    {
        stbtt__sort_edges_quicksort_range(p, 0, n);
    }

    // C++ advances the p pointer for the "iterate on larger" tail; here the range
    // start `o` plays that role.
    static void stbtt__sort_edges_quicksort_range(stbtt__edge[] p, int o, int n)
    {
        // threshold for transitioning to insertion sort
        while (n > 12)
        {
            stbtt__edge t;
            int c01, c12, c, m, i, j;

            // compute median of three
            m = n >> 1;
            c01 = p[o].y0 < p[o + m].y0 ? 1 : 0;
            c12 = p[o + m].y0 < p[o + n - 1].y0 ? 1 : 0;
            // if 0 >= mid >= end, or 0 < mid < end, then use mid
            if (c01 != c12)
            {
                // otherwise, we'll need to swap something else to middle
                int z;
                c = p[o].y0 < p[o + n - 1].y0 ? 1 : 0;
                z = (c == c12) ? 0 : n - 1;
                t = p[o + z];
                p[o + z] = p[o + m];
                p[o + m] = t;
            }
            // now p[m] is the median-of-three
            // swap it to the beginning so it won't move around
            t = p[o];
            p[o] = p[o + m];
            p[o + m] = t;

            // partition loop
            i = 1;
            j = n - 1;
            for (; ; )
            {
                // handling of equality is crucial here
                for (; ; ++i)
                {
                    if (!(p[o + i].y0 < p[o].y0)) break;
                }
                for (; ; --j)
                {
                    if (!(p[o].y0 < p[o + j].y0)) break;
                }
                // make sure we haven't crossed
                if (i >= j) break;
                t = p[o + i];
                p[o + i] = p[o + j];
                p[o + j] = t;

                ++i;
                --j;
            }
            // recurse on smaller side, iterate on larger
            if (j < (n - i))
            {
                stbtt__sort_edges_quicksort_range(p, o, j);
                o += i;
                n = n - i;
            }
            else
            {
                stbtt__sort_edges_quicksort_range(p, o + i, n - i);
                n = j;
            }
        }
    }

    static void stbtt__sort_edges(stbtt__edge[] p, int n)
    {
        stbtt__sort_edges_quicksort(p, n);
        stbtt__sort_edges_ins_sort(p, n);
    }

    static void stbtt__rasterize(stbtt__bitmap result, stbtt__point[] pts, int[] wcount, int windings, float scale_x, float scale_y, float shift_x, float shift_y, int off_x, int off_y, int invert)
    {
        float y_scale_inv = invert != 0 ? -scale_y : scale_y;
        int n, i, j, k, m;
        int vsubsample = 1; // STBTT_RASTERIZER_VERSION == 2

        // now we have to blow out the windings into explicit edge lists
        n = 0;
        for (i = 0; i < windings; ++i)
            n += wcount[i];

        stbtt__edge[] e = new stbtt__edge[n + 1]; // add an extra one as a sentinel
        n = 0;

        m = 0;
        for (i = 0; i < windings; ++i)
        {
            var p = new ArraySegment<stbtt__point>(pts, m, wcount[i]);
            m += wcount[i];
            j = wcount[i] - 1;
            for (k = 0; k < wcount[i]; j = k++)
            {
                int a = k, b = j;
                // skip the edge if horizontal
                if (p.Array[p.Offset + j].y == p.Array[p.Offset + k].y)
                    continue;
                // add edge from j to k to the list
                e[n].invert = 0;
                if (invert != 0 ? p.Array[p.Offset + j].y > p.Array[p.Offset + k].y : p.Array[p.Offset + j].y < p.Array[p.Offset + k].y)
                {
                    e[n].invert = 1;
                    a = j; b = k;
                }
                e[n].x0 = p.Array[p.Offset + a].x * scale_x + shift_x;
                e[n].y0 = (p.Array[p.Offset + a].y * y_scale_inv + shift_y) * vsubsample;
                e[n].x1 = p.Array[p.Offset + b].x * scale_x + shift_x;
                e[n].y1 = (p.Array[p.Offset + b].y * y_scale_inv + shift_y) * vsubsample;
                ++n;
            }
        }

        // now sort the edges by their highest point (should snap to integer, and then by x)
        stbtt__sort_edges(e, n);

        // now, traverse the scanlines and find the intersections on each scanline, use non-zero winding rule
        stbtt__rasterize_sorted_edges(result, e, n, vsubsample, off_x, off_y);
    }

    static void stbtt__add_point(stbtt__point[] points, int n, float x, float y)
    {
        if (points == null) return; // during first pass, it's unallocated
        points[n].x = x;
        points[n].y = y;
    }

    // tessellate until threshold p is happy
    static void stbtt__tesselate_curve(stbtt__point[] points, ref int num_points, float x0, float y0, float x1, float y1, float x2, float y2, float objspace_flatness_squared, int n)
    {
        // midpoint
        float mx = (x0 + 2 * x1 + x2) / 4;
        float my = (y0 + 2 * y1 + y2) / 4;
        // versus directly drawn line
        float dx = (x0 + x2) / 2 - mx;
        float dy = (y0 + y2) / 2 - my;
        if (n > 16) // 65536 segments on one curve better be enough!
            return;
        if (dx * dx + dy * dy > objspace_flatness_squared)
        { // half-pixel error allowed... need to be smaller if AA
            stbtt__tesselate_curve(points, ref num_points, x0, y0, (x0 + x1) / 2.0f, (y0 + y1) / 2.0f, mx, my, objspace_flatness_squared, n + 1);
            stbtt__tesselate_curve(points, ref num_points, mx, my, (x1 + x2) / 2.0f, (y1 + y2) / 2.0f, x2, y2, objspace_flatness_squared, n + 1);
        }
        else
        {
            stbtt__add_point(points, num_points, x2, y2);
            num_points++;
        }
    }

    static void stbtt__tesselate_cubic(stbtt__point[] points, ref int num_points, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, float objspace_flatness_squared, int n)
    {
        // @TODO this "flatness" calculation is just made-up nonsense that seems to work well enough
        float dx0 = x1 - x0;
        float dy0 = y1 - y0;
        float dx1 = x2 - x1;
        float dy1 = y2 - y1;
        float dx2 = x3 - x2;
        float dy2 = y3 - y2;
        float dx = x3 - x0;
        float dy = y3 - y0;
        float longlen = MathF.Sqrt(dx0 * dx0 + dy0 * dy0) + MathF.Sqrt(dx1 * dx1 + dy1 * dy1) + MathF.Sqrt(dx2 * dx2 + dy2 * dy2);
        float shortlen = MathF.Sqrt(dx * dx + dy * dy);
        float flatness_squared = longlen * longlen - shortlen * shortlen;

        if (n > 16)
            return;

        if (flatness_squared > objspace_flatness_squared)
        {
            float x01 = (x0 + x1) / 2;
            float y01 = (y0 + y1) / 2;
            float x12 = (x1 + x2) / 2;
            float y12 = (y1 + y2) / 2;
            float x23 = (x2 + x3) / 2;
            float y23 = (y2 + y3) / 2;

            float xa = (x01 + x12) / 2;
            float ya = (y01 + y12) / 2;
            float xb = (x12 + x23) / 2;
            float yb = (y12 + y23) / 2;

            float mx = (xa + xb) / 2;
            float my = (ya + yb) / 2;

            stbtt__tesselate_cubic(points, ref num_points, x0, y0, x01, y01, xa, ya, mx, my, objspace_flatness_squared, n + 1);
            stbtt__tesselate_cubic(points, ref num_points, mx, my, xb, yb, x23, y23, x3, y3, objspace_flatness_squared, n + 1);
        }
        else
        {
            stbtt__add_point(points, num_points, x3, y3);
            num_points++;
        }
    }

    // returns number of contours
    static stbtt__point[] stbtt_FlattenCurves(stbtt_vertex[] vertices, int num_verts, float objspace_flatness, out int[] contour_lengths, out int num_contours)
    {
        stbtt__point[] points = null;
        int num_points = 0;

        float objspace_flatness_squared = objspace_flatness * objspace_flatness;
        int i, n = 0, start = 0, pass;

        // count how many "moves" there are to get the contour count
        for (i = 0; i < num_verts; ++i)
            if (vertices[i].type == STBTT_vmove)
                ++n;

        num_contours = n;
        contour_lengths = null;
        if (n == 0) return null;

        contour_lengths = new int[n];

        // make two passes through the points so we don't need to realloc
        for (pass = 0; pass < 2; ++pass)
        {
            float x = 0, y = 0;
            if (pass == 1)
                points = new stbtt__point[num_points];
            num_points = 0;
            n = -1;
            for (i = 0; i < num_verts; ++i)
            {
                switch (vertices[i].type)
                {
                    case STBTT_vmove:
                        // start the next contour
                        if (n >= 0)
                            contour_lengths[n] = num_points - start;
                        ++n;
                        start = num_points;

                        x = vertices[i].x; y = vertices[i].y;
                        stbtt__add_point(points, num_points++, x, y);
                        break;
                    case STBTT_vline:
                        x = vertices[i].x; y = vertices[i].y;
                        stbtt__add_point(points, num_points++, x, y);
                        break;
                    case STBTT_vcurve:
                        stbtt__tesselate_curve(points, ref num_points, x, y,
                                                 vertices[i].cx, vertices[i].cy,
                                                 vertices[i].x, vertices[i].y,
                                                 objspace_flatness_squared, 0);
                        x = vertices[i].x; y = vertices[i].y;
                        break;
                    case STBTT_vcubic:
                        stbtt__tesselate_cubic(points, ref num_points, x, y,
                                                 vertices[i].cx, vertices[i].cy,
                                                 vertices[i].cx1, vertices[i].cy1,
                                                 vertices[i].x, vertices[i].y,
                                                 objspace_flatness_squared, 0);
                        x = vertices[i].x; y = vertices[i].y;
                        break;
                }
            }
            contour_lengths[n] = num_points - start;
        }

        return points;
    }

    struct stbtt__bitmap
    {
        public int w, h, stride;
        public byte[] pixels;
    }

    static void stbtt_Rasterize(stbtt__bitmap result, float flatness_in_pixels, stbtt_vertex[] vertices, int num_verts, float scale_x, float scale_y, float shift_x, float shift_y, int x_off, int y_off, int invert)
    {
        float scale = scale_x > scale_y ? scale_y : scale_x;
        int winding_count = 0;
        int[] winding_lengths = null;
        stbtt__point[] windings = stbtt_FlattenCurves(vertices, num_verts, flatness_in_pixels / scale, out winding_lengths, out winding_count);
        if (windings != null)
        {
            stbtt__rasterize(result, windings, winding_lengths, winding_count, scale_x, scale_y, shift_x, shift_y, x_off, y_off, invert);
        }
    }

    static void MakeGlyphBitmapSubpixel(stbtt_fontinfo info, byte[] output, int out_w, int out_h, int out_stride, float scale_x, float scale_y, float shift_x, float shift_y, int glyph)
    {
        int ix0, iy0;
        stbtt_vertex[] vertices;
        int num_verts = GetGlyphShape(info, glyph, out vertices);
        stbtt__bitmap gbm;

        GetGlyphBitmapBoxSubpixel(info, glyph, scale_x, scale_y, shift_x, shift_y, out ix0, out iy0, out _, out _);
        gbm.pixels = output;
        gbm.w = out_w;
        gbm.h = out_h;
        gbm.stride = out_stride;

        if (gbm.w != 0 && gbm.h != 0)
            stbtt_Rasterize(gbm, 0.35f, vertices, num_verts, scale_x, scale_y, shift_x, shift_y, ix0, iy0, 1);
    }

    public static void MakeGlyphBitmapSubpixelPrefilter(stbtt_fontinfo info, byte[] output, int out_w, int out_h, int out_stride, float scale_x, float scale_y, float shift_x, float shift_y, int prefilter_x, int prefilter_y, out float sub_x, out float sub_y, int glyph)
    {
        MakeGlyphBitmapSubpixel(info,
                                output,
                                out_w - (prefilter_x - 1),
                                out_h - (prefilter_y - 1),
                                out_stride,
                                scale_x,
                                scale_y,
                                shift_x,
                                shift_y,
                                glyph);

        if (prefilter_x > 1)
            stbtt__h_prefilter(output, 0, out_w, out_h, out_stride, (uint)prefilter_x);

        if (prefilter_y > 1)
            stbtt__v_prefilter(output, 0, out_w, out_h, out_stride, (uint)prefilter_y);

        sub_x = oversample_shift(prefilter_x);
        sub_y = oversample_shift(prefilter_y);
    }

    const int STBTT_MAX_OVERSAMPLE = 8;
    const int STBTT__OVER_MASK = STBTT_MAX_OVERSAMPLE - 1;

    static void stbtt__h_prefilter(byte[] pixels, int pixels_offset, int w, int h, int stride_in_bytes, uint kernel_width)
    {
        Span<byte> buffer = stackalloc byte[STBTT_MAX_OVERSAMPLE];
        int safe_w = w - (int)kernel_width;
        int j;
        for (j = 0; j < h; ++j)
        {
            int i;
            uint total;
            buffer.Slice(0, (int)kernel_width).Clear();

            total = 0;
            int po = pixels_offset + j * stride_in_bytes;

            for (i = 0; i <= safe_w; ++i)
            {
                total += (uint)((int)pixels[po + i] - buffer[i & STBTT__OVER_MASK]);
                buffer[(i + (int)kernel_width) & STBTT__OVER_MASK] = pixels[po + i];
                pixels[po + i] = (byte)(total / kernel_width);
            }

            for (; i < w; ++i)
            {
                total -= buffer[i & STBTT__OVER_MASK];
                pixels[po + i] = (byte)(total / kernel_width);
            }
        }
    }

    static void stbtt__v_prefilter(byte[] pixels, int pixels_offset, int w, int h, int stride_in_bytes, uint kernel_width)
    {
        Span<byte> buffer = stackalloc byte[STBTT_MAX_OVERSAMPLE];
        int safe_h = h - (int)kernel_width;
        int j;
        for (j = 0; j < w; ++j)
        {
            int i;
            uint total;
            buffer.Slice(0, (int)kernel_width).Clear();

            total = 0;
            int po = pixels_offset + j;

            for (i = 0; i <= safe_h; ++i)
            {
                total += (uint)((int)pixels[po + i * stride_in_bytes] - buffer[i & STBTT__OVER_MASK]);
                buffer[(i + (int)kernel_width) & STBTT__OVER_MASK] = pixels[po + i * stride_in_bytes];
                pixels[po + i * stride_in_bytes] = (byte)(total / kernel_width);
            }

            for (; i < h; ++i)
            {
                total -= buffer[i & STBTT__OVER_MASK];
                pixels[po + i * stride_in_bytes] = (byte)(total / kernel_width);
            }
        }
    }
}
