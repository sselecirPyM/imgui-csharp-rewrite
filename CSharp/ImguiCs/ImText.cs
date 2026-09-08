// Port of Dear ImGui text utilities (imgui_draw.cpp): UTF-8 <-> codepoints.
using System;
using System.Text;

namespace ImGui;

public static unsafe partial class ImText
{
    // ImTextCharFromUtf8(): read one character. return input UTF-8 bytes count.
    // C# port: reads from byte[]; in_text_end < 0 means "until end of array" (C++ NULL).
    // Faithful port of the imgui_decode_utf8 sequence: invalid/incomplete sequences return the
    // number of non-zero bytes of the sequence (at most 'wanted'), with *out_char set to 0xFFFD.
    public static int ImTextCharFromUtf8(out uint out_char, byte[] in_text, int offset, int in_text_end)
    {
        ReadOnlySpan<byte> lengths = stackalloc byte[32] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 3, 3, 4, 0 };
        ReadOnlySpan<uint> masks = stackalloc uint[5] { 0x00, 0x7f, 0x1f, 0x0f, 0x07 };
        ReadOnlySpan<uint> mins = stackalloc uint[5] { 0x400000, 0, 0x80, 0x800, 0x10000 };
        ReadOnlySpan<int> shiftc = stackalloc int[5] { 0, 18, 12, 6, 0 };
        ReadOnlySpan<int> shifte = stackalloc int[5] { 0, 6, 4, 2, 0 };
        out_char = 0;
        int p = offset;
        byte lead = (p >= 0 && p < in_text.Length) ? in_text[p] : (byte)0;
        int wanted = lengths[lead >> 3];
        wanted += (wanted != 0) ? 0 : 1;

        // IMPORTANT: if in_text_end < 0 (C++ NULL) we assume we have enough space!
        int end = in_text_end < 0 ? in_text.Length : in_text_end;

        // Copy at most 'len' bytes, stop copying at 0 or past in_text_end.
        Span<byte> s = stackalloc byte[4];
        s[0] = (p + 0 < end && p + 0 < in_text.Length) ? in_text[p + 0] : (byte)0;
        s[1] = (p + 1 < end && p + 1 < in_text.Length) ? in_text[p + 1] : (byte)0;
        s[2] = (p + 2 < end && p + 2 < in_text.Length) ? in_text[p + 2] : (byte)0;
        s[3] = (p + 3 < end && p + 3 < in_text.Length) ? in_text[p + 3] : (byte)0;

        // Assume a four-byte character and load four bytes. Unused bits are shifted out.
        int len = lengths[lead >> 3];
        out_char = (uint)(s[0] & masks[len]) << 18;
        out_char |= (uint)(s[1] & 0x3f) << 12;
        out_char |= (uint)(s[2] & 0x3f) << 6;
        out_char |= (uint)(s[3] & 0x3f) << 0;
        out_char >>= shiftc[len];

        // Accumulate the various error conditions.
        int e = 0;
        e = (out_char < mins[len] ? 1 : 0) << 6;              // non-canonical encoding
        e |= ((out_char >> 11) == 0x1b ? 1 : 0) << 7;         // surrogate half?
        e |= (out_char > ImUtil.IM_UNICODE_CODEPOINT_MAX ? 1 : 0) << 8; // out of range we can store in ImWchar (FIXME: May evolve)
        e |= (s[1] & 0xc0) >> 2;
        e |= (s[2] & 0xc0) >> 4;
        e |= s[3] >> 6;
        e ^= 0x2a; // top two bits of each tail byte correct?
        e >>= shifte[len];

        if (e != 0)
        {
            // No bytes are consumed when *in_text == 0 || in_text == in_text_end.
            // One byte is consumed in case of invalid first byte of in_text.
            // All available bytes (at most `len` bytes) are consumed on incomplete/invalid second to last bytes.
            // Invalid or incomplete input may consume less bytes than wanted, therefore every byte has to be inspected in s.
            wanted = ImMath.ImMin(wanted, (s[0] != 0 ? 1 : 0) + (s[1] != 0 ? 1 : 0) + (s[2] != 0 ? 1 : 0) + (s[3] != 0 ? 1 : 0));
            out_char = ImUtil.IM_UNICODE_CODEPOINT_INVALID;
        }

        return wanted;
    }

    // ImTextCharToUtf8: write char into byte buffer, return count of bytes written (out of 1..4)
    public static int ImTextCharToUtf8(byte[] out_buf, int offset, uint c)
    {
        if (c < 0x80) { out_buf[offset] = (byte)c; return 1; }
        if (c < 0x800)
        {
            out_buf[offset] = (byte)(0xc0 | (c >> 6));
            out_buf[offset + 1] = (byte)(0x80 | (c & 0x3f));
            return 2;
        }
        if (c < 0x10000)
        {
            out_buf[offset] = (byte)(0xe0 | (c >> 12));
            out_buf[offset + 1] = (byte)(0x80 | ((c >> 6) & 0x3f));
            out_buf[offset + 2] = (byte)(0x80 | (c & 0x3f));
            return 3;
        }
        if (c < 0x110000)
        {
            out_buf[offset] = (byte)(0xf0 | (c >> 18));
            out_buf[offset + 1] = (byte)(0x80 | ((c >> 12) & 0x3f));
            out_buf[offset + 2] = (byte)(0x80 | ((c >> 6) & 0x3f));
            out_buf[offset + 3] = (byte)(0x80 | (c & 0x3f));
            return 4;
        }
        return 0;
    }

    public static string Utf16FromUtf8(byte[] bytes, int offset, int count)
    {
        // Decode permissively like ImGui does (invalid sequences -> 0xFFFD) - but for internal
        // purposes Encoding.UTF8 with replacement is equivalent for valid text.
        return Encoding.UTF8.GetString(bytes, offset, count);
    }

    public static byte[] Utf8FromUtf16(string s) => Encoding.UTF8.GetBytes(s ?? "");

    // Iterate codepoints of a UTF-16 string, joining surrogate pairs.
    public delegate void CodepointVisitor(uint cp, int byte_len_utf8, int utf16_index);
    public static void ForEachCodepoint(string s, int begin, int end, CodepointVisitor visit)
    {
        int i = begin;
        while (i < end)
        {
            char c = s[i];
            uint cp;
            int len;
            if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(s[i + 1]))
            {
                cp = 0x10000u + (uint)(((c - 0xD800) << 10) + (s[i + 1] - 0xDC00));
                len = 2;
            }
            else
            {
                cp = c;
                len = 1;
                if (c >= 0xD800 && c <= 0xDFFF) cp = 0xFFFFFFFF; // lone surrogate: invalid like C++ decoder
            }
            visit(cp, ImTextUtf8Len(cp), i);
            i += len;
        }
    }

    public static int ImTextUtf8Len(uint c) => c < 0x80 ? 1 : c < 0x800 ? 2 : c < 0x10000 ? 3 : 4;
}

public static partial class ImText
{
    // Step over one codepoint in a UTF-16 string starting at index i.
    // Sets decoded to the codepoint; returns step size (1 or 2 chars).
    public static int StringCodepointStep(ReadOnlySpan<char> s, int i, int end, ref uint decoded)
    {
        char ch = s[i];
        if (char.IsHighSurrogate(ch) && i + 1 < end && char.IsLowSurrogate(s[i + 1]))
        {
            decoded = 0x10000u + (uint)((ch - 0xD800) << 10) + (uint)(s[i + 1] - 0xDC00);
            return 2;
        }
        decoded = ch;
        return 1;
    }

    // Blank space for word-wrap trimming (ImCharIsBlankA)
    public static bool ImCharIsBlankA(char c) => c == ' ' || c == '\t';
}
