// Port of stb_decompress (imgui_draw.cpp Decompression code section).
// Compressed with stb_compress() then base85-encoded (misc/fonts/binary_to_compressed_c.cpp).
using System;

namespace ImGui;

public static class StbDecompress
{
    public static uint stb_decompress_length(byte[] input)
        => (uint)((input[8] << 24) + (input[9] << 16) + (input[10] << 8) + input[11]);

    static byte[] s_out;                    // output buffer
    static int s_out_end;                   // stb__barrier_out_e (index)
    static int s_out_begin;                 // stb__barrier_out_b (index)
    static byte[] s_in;                     // input buffer
    static int s_dout;                      // stb__dout (index)

    static void stb__match(int data, uint length)
    {
        // INVERSE of memmove... write each byte before copying the next...
        // (data is an index into the OUTPUT buffer - back-reference)
        if (s_dout + length > s_out_end) { s_dout += (int)length; return; }
        if (data < s_out_begin) { s_dout = s_out_end + 1; return; }
        while (length-- != 0) s_out[s_dout++] = s_out[data++];
    }

    static void stb__lit(int data, uint length)
    {
        if (s_dout + length > s_out_end) { s_dout += (int)length; return; }
        if (data < 0) { s_dout = s_out_end + 1; return; }
        Array.Copy(s_in, data, s_out, s_dout, length);
        s_dout += (int)length;
    }

    static int stb__in2(int[] i, int x) => (i[x] << 8) + i[x + 1];
    static int stb__in3(int[] i, int x) => (i[x] << 16) + stb__in2(i, x + 1);
    static int stb__in4(int[] i, int x) => (i[x] << 24) + stb__in3(i, x + 1);

    static int stb_decompress_token(int[] i, int idx)
    {
        if (i[idx] >= 0x20)
        {
            if (i[idx] >= 0x80) { stb__match(s_dout - i[idx + 1] - 1, (uint)(i[idx] - 0x80 + 1)); idx += 2; }
            else if (i[idx] >= 0x40) { stb__match(s_dout - (stb__in2(i, idx) - 0x4000 + 1), (uint)(i[idx + 2] + 1)); idx += 3; }
            else { stb__lit(idx + 1, (uint)(i[idx] - 0x20 + 1)); idx += 1 + (i[idx] - 0x20 + 1); }
        }
        else
        {
            if (i[idx] >= 0x18) { stb__match(s_dout - (stb__in3(i, idx) - 0x180000 + 1), (uint)(i[idx + 3] + 1)); idx += 4; }
            else if (i[idx] >= 0x10) { stb__match(s_dout - (stb__in3(i, idx) - 0x100000 + 1), (uint)(stb__in2(i, 3 + idx) + 1)); idx += 5; }
            else if (i[idx] >= 0x08) { stb__lit(idx + 2, (uint)(stb__in2(i, idx) - 0x0800 + 1)); idx += 2 + (stb__in2(i, idx) - 0x0800 + 1); }
            else if (i[idx] == 0x07) { stb__lit(idx + 3, (uint)(stb__in2(i, 1 + idx) + 1)); idx += 3 + (stb__in2(i, 1 + idx) + 1); }
            else if (i[idx] == 0x06) { stb__match(s_dout - (stb__in3(i, 1 + idx) + 1), (uint)(i[idx + 4] + 1)); idx += 5; }
            else if (i[idx] == 0x04) { stb__match(s_dout - (stb__in3(i, 1 + idx) + 1), (uint)(stb__in2(i, 4 + idx) + 1)); idx += 6; }
        }
        return idx;
    }

    static uint stb_adler32(uint adler32, byte[] buffer, uint buflen)
    {
        const uint ADLER_MOD = 65521;
        uint s1 = adler32 & 0xffff, s2 = adler32 >> 16;
        uint blocklen = buflen % 5552;

        uint i;
        int buf = 0;
        while (buflen != 0)
        {
            for (i = 0; i + 7 < blocklen; i += 8)
            {
                s1 += buffer[buf + 0]; s2 += s1;
                s1 += buffer[buf + 1]; s2 += s1;
                s1 += buffer[buf + 2]; s2 += s1;
                s1 += buffer[buf + 3]; s2 += s1;
                s1 += buffer[buf + 4]; s2 += s1;
                s1 += buffer[buf + 5]; s2 += s1;
                s1 += buffer[buf + 6]; s2 += s1;
                s1 += buffer[buf + 7]; s2 += s1;
                buf += 8;
            }

            for (; i < blocklen; ++i)
            {
                s1 += buffer[buf++]; s2 += s1;
            }

            s1 %= ADLER_MOD; s2 %= ADLER_MOD;
            buflen -= blocklen;
            blocklen = 5552;
        }
        return (uint)(s2 << 16) + (uint)s1;
    }

    public static uint stb_decompress(byte[] output, byte[] input, uint length)
    {
        int[] i = new int[input.Length];
        for (int k = 0; k < input.Length; k++) i[k] = input[k];

        if (stb__in4(i, 0) != unchecked((int)0x57bC0000)) return 0;
        if (stb__in4(i, 4) != 0) return 0; // error! stream is > 4GB
        uint olen = stb_decompress_length(input);
        s_in = input;
        s_out = output;
        s_out_end = (int)olen;
        s_out_begin = 0;
        int idx = 16;

        s_dout = 0;
        for (; ; )
        {
            int old_idx = idx;
            idx = stb_decompress_token(i, idx);
            if (idx == old_idx)
            {
                if (i[idx] == 0x05 && i[idx + 1] == 0xfa)
                {
                    System.Diagnostics.Debug.Assert(s_dout == olen);
                    if (s_dout != olen) return 0;
                    if (stb_adler32(1, output, olen) != (uint)stb__in4(i, 2))
                        return 0;
                    return olen;
                }
                else
                {
                    System.Diagnostics.Debug.Assert(false); /* NOTREACHED */
                    return 0;
                }
            }
            System.Diagnostics.Debug.Assert(s_dout <= olen);
            if (s_dout > olen)
                return 0;
        }
    }

    public static byte Decode85Byte(byte c) => (byte)(c >= '\\' ? c - 36 : c - 35);

    public static void Decode85(byte[] src, int src_offset, byte[] dst)
    {
        int s = src_offset;
        int d = 0;
        while (s < src.Length && src[s] != 0)
        {
            uint tmp = (uint)Decode85Byte(src[s]) + 85u * ((uint)Decode85Byte(src[s + 1]) + 85u * ((uint)Decode85Byte(src[s + 2]) + 85u * ((uint)Decode85Byte(src[s + 3]) + 85u * (uint)Decode85Byte(src[s + 4]))));
            dst[d + 0] = (byte)((tmp >> 0) & 0xFF);
            dst[d + 1] = (byte)((tmp >> 8) & 0xFF);
            dst[d + 2] = (byte)((tmp >> 16) & 0xFF);
            dst[d + 3] = (byte)((tmp >> 24) & 0xFF);
            s += 5;
            d += 4;
        }
    }
}
