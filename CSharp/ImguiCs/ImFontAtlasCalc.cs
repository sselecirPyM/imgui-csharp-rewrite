// Port of font text measurement and word-wrapping (imgui_draw.cpp):
// ImFontCalcWordWrapPositionEx, ImFontCalcTextSizeEx, ImTextCalcWordWrapNextLineStart,
// and the 2-bit-per-char text classifiers.
using System;

namespace ImGui;

public partial class ImFontAtlas
{
    // ---- Text classifiers (2 bits per character) ----
    static ImU32[] g_CharClassifierIsSeparator_0000_007f = new ImU32[128 / 16];
    static ImU32[] g_CharClassifierIsSeparator_3000_300f = new ImU32[16 / 16];

    const int ImWcharClass_Other = 0;
    const int ImWcharClass_Blank = 1;
    const int ImWcharClass_Punct = 2;

    static int ImTextClassifierGet(ImU32[] bits, uint char_offset)
        => (int)((bits[char_offset >> 4] >> ((int)(char_offset & 15) << 1)) & 0x03);

    static void ImTextClassifierClear(ImU32[] bits, uint codepoint_min, uint codepoint_end, int char_class)
    {
        for (uint c = codepoint_min; c < codepoint_end; c++)
            ImTextClassifierSetCharClass(bits, codepoint_min, codepoint_end, char_class, c);
    }

    static void ImTextClassifierSetCharClass(ImU32[] bits, uint codepoint_min, uint codepoint_end, int char_class, uint c)
    {
        System.Diagnostics.Debug.Assert(c >= codepoint_min && c < codepoint_end);
        c -= codepoint_min;
        uint shift = (c & 15) << 1;
        bits[c >> 4] = (bits[c >> 4] & ~(0x03u << (int)shift)) | ((uint)char_class << (int)shift);
    }

    static void ImTextClassifierSetCharClassFromStr(ImU32[] bits, uint codepoint_min, uint codepoint_end, int char_class, string s)
    {
        foreach (char ch in s)
            ImTextClassifierSetCharClass(bits, codepoint_min, codepoint_end, char_class, ch);
    }

    public static void ImTextInitClassifiers()
    {
        if (ImTextClassifierGet(g_CharClassifierIsSeparator_0000_007f, ',') != 0)
            return;

        // List of hardcoded separators: .,;!?'" + blanks " \t"
        ImTextClassifierClear(g_CharClassifierIsSeparator_0000_007f, 0, 128, ImWcharClass_Other);
        ImTextClassifierSetCharClassFromStr(g_CharClassifierIsSeparator_0000_007f, 0, 128, ImWcharClass_Blank, " \t");
        ImTextClassifierSetCharClassFromStr(g_CharClassifierIsSeparator_0000_007f, 0, 128, ImWcharClass_Punct, ".,;!?\"");

        ImTextClassifierClear(g_CharClassifierIsSeparator_3000_300f, 0x3000, 0x300F, ImWcharClass_Other);
        ImTextClassifierSetCharClass(g_CharClassifierIsSeparator_3000_300f, 0x3000, 0x300F, ImWcharClass_Blank, 0x3000);
        ImTextClassifierSetCharClass(g_CharClassifierIsSeparator_3000_300f, 0x3000, 0x300F, ImWcharClass_Punct, 0x3001);
        ImTextClassifierSetCharClass(g_CharClassifierIsSeparator_3000_300f, 0x3000, 0x300F, ImWcharClass_Punct, 0x3002);
    }

    // Simple word-wrapping for English, not full-featured.
    // Operates on UTF-16 string indices (identical to C++ UTF-8 byte indices for ASCII).
    public static int ImFontCalcWordWrapPositionEx(ImFont font, float size, ReadOnlySpan<char> text, int text_begin, int text_end, float wrap_width, int flags)
    {
        ImTextInitClassifiers();

        ImFontBaked baked = font.GetFontBaked(size);
        float scale = size / baked.Size;

        float line_width = 0.0f;
        float blank_width = 0.0f;
        wrap_width /= scale; // We work with unscaled widths to avoid scaling every characters

        int s = text_begin;

        int prev_type = ImWcharClass_Other;
        bool keep_blanks = (flags & ImDrawTextFlags_.ImDrawTextFlags_WrapKeepBlanks) != 0;

        int span_end = s;
        float span_width = 0.0f;

        while (s < text_end)
        {
            uint c = text[s];
            int next_s;
            if (c < 0x80)
                next_s = s + 1;
            else
            {
                uint decoded = text[s];
                next_s = s + ImText.StringCodepointStep(text, s, text_end, ref decoded);
                c = decoded;
            }

            if (c < 32)
            {
                if (c == '\n')
                    return s; // Direct return, skip "Wrap_width is too small to fit anything" path.
                if (c == '\r')
                {
                    s = next_s; // Fast-skip
                    continue;
                }
            }

            // Optimized inline version of 'float char_width = GetCharAdvance((ImWchar)c);'
            float char_width = (c < (uint)baked.IndexAdvanceX.Size) ? baked.IndexAdvanceX.Data[c] : -1.0f;
            if (char_width < 0.0f)
                char_width = BuildLoadGlyphGetAdvanceOrFallback(baked, c);

            // Classify current character
            int curr_type;
            if (c < 128)
                curr_type = ImTextClassifierGet(g_CharClassifierIsSeparator_0000_007f, c);
            else if (c >= 0x3000 && c < 0x3010)
                curr_type = ImTextClassifierGet(g_CharClassifierIsSeparator_3000_300f, c & 15);
            else
                curr_type = ImWcharClass_Other;

            if (curr_type == ImWcharClass_Blank)
            {
                // End span: 'A ' or '. '
                if (prev_type != ImWcharClass_Blank && !keep_blanks)
                {
                    span_end = s;
                    line_width += span_width;
                    span_width = 0.0f;
                }
                blank_width += char_width;
            }
            else
            {
                // End span: '.X' unless X is a digit
                if (prev_type == ImWcharClass_Punct && curr_type != ImWcharClass_Punct && !(c >= '0' && c <= '9'))
                {
                    span_end = s;
                    line_width += span_width + blank_width;
                    span_width = blank_width = 0.0f;
                }
                // End span: 'A ' or '. '
                else if (prev_type == ImWcharClass_Blank && keep_blanks)
                {
                    span_end = s;
                    line_width += span_width + blank_width;
                    span_width = blank_width = 0.0f;
                }
                span_width += char_width;
            }

            if (span_width + blank_width + line_width > wrap_width)
            {
                if (span_width + blank_width > wrap_width)
                    break;
                return span_end;
            }

            prev_type = curr_type;
            s = next_s;
        }

        // Wrap_width is too small to fit anything. Force displaying 1 character to minimize the height discontinuity.
        if (s == text_begin && text_begin < text_end)
            return s + 1;
        return s;
    }

    // Trim trailing space and find beginning of next line
    public static int ImTextCalcWordWrapNextLineStart(ReadOnlySpan<char> text, int s, int text_end, int flags)
    {
        if ((flags & ImDrawTextFlags_.ImDrawTextFlags_WrapKeepBlanks) == 0)
            while (s < text_end && ImText.ImCharIsBlankA(text[s]))
                s++;
        if (s < text_end && text[s] == '\n')
            s++;
        return s;
    }

    public static ImVec2 ImFontCalcTextSizeEx(ImFont font, float size, float max_width, float wrap_width, ReadOnlySpan<char> text, int text_begin, int text_end_display, int text_end, out int remaining, ImVec2[] out_offset, int flags)
    {
        if (text_end < 0)
            text_end = text.Length;
        if (text_end_display < 0)
            text_end_display = text_end;

        ImFontBaked baked = font.GetFontBaked(size);
        float line_height = size;
        float scale = line_height / baked.Size;

        ImVec2 text_size = new ImVec2(0, 0);
        float line_width = 0.0f;

        bool word_wrap_enabled = (wrap_width > 0.0f);
        int word_wrap_eol = -1;

        int s = text_begin;
        while (s < text_end_display)
        {
            // Word-wrapping
            if (word_wrap_enabled)
            {
                if (word_wrap_eol < 0)
                    word_wrap_eol = ImFontCalcWordWrapPositionEx(font, size, text, s, text_end, wrap_width - line_width, flags);

                if (s >= word_wrap_eol)
                {
                    if (text_size.x < line_width)
                        text_size.x = line_width;
                    text_size.y += line_height;
                    line_width = 0.0f;
                    s = ImTextCalcWordWrapNextLineStart(text, s, text_end, flags); // Wrapping skips upcoming blanks
                    if ((flags & ImDrawTextFlags_.ImDrawTextFlags_StopOnNewLine) != 0)
                        break;
                    word_wrap_eol = -1;
                    continue;
                }
            }

            // Decode and advance source
            int prev_s = s;
            uint c = text[s];
            if (c < 0x80)
                s += 1;
            else
            {
                uint decoded = c;
                s += ImText.StringCodepointStep(text, s, text_end, ref decoded);
                c = decoded;
            }

            if (c == '\n')
            {
                text_size.x = ImMath.ImMax(text_size.x, line_width);
                text_size.y += line_height;
                line_width = 0.0f;
                if ((flags & ImDrawTextFlags_.ImDrawTextFlags_StopOnNewLine) != 0)
                    break;
                continue;
            }
            if (c == '\r')
                continue;

            // Optimized inline version of 'float char_width = GetCharAdvance((ImWchar)c);'
            float char_width = (c < (uint)baked.IndexAdvanceX.Size) ? baked.IndexAdvanceX.Data[c] : -1.0f;
            if (char_width < 0.0f)
                char_width = BuildLoadGlyphGetAdvanceOrFallback(baked, c);
            char_width *= scale;

            if (line_width + char_width >= max_width)
            {
                s = prev_s;
                break;
            }

            line_width += char_width;
        }

        if (text_size.x < line_width)
            text_size.x = line_width;

        if (out_offset != null && out_offset.Length > 0)
            out_offset[0] = new ImVec2(line_width, text_size.y + line_height);

        remaining = s; // (C# out param: assign unconditionally; C++ only fills it in the wrap case, callers ignore otherwise)

        if (line_width > 0 || text_size.y == 0.0f)                        // whereas size.y will ignore the trailing \n
            text_size.y += line_height;

        return text_size;
    }
}
