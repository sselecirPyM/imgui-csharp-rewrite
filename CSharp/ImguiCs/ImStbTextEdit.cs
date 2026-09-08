// Port of the stb_textedit machinery used by InputText:
//  - the ImStb glue layer declared in imgui_widgets.cpp (STB_TEXTEDIT_* implementations for
//    ImGuiInputTextState, UTF-8 word boundaries, word/line movement, DELETECHARS/INSERTCHARS),
//  - the imstb_textedit.h implementation (locate_coord/click/drag/find_charpos/key/text/cut/paste,
//    the undo stack, makeundo_* helpers, stb_textedit_replace() override).
// The golden scenarios do not inject key/character events, so only the rendering paths are
// exercised; the editing machinery is nonetheless ported faithfully and is non-throwing.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImStbTextEdit
{
    // We don't use an enum so we can build even with conflicting symbols (if another user of stb_textedit.h leak their STB_TEXTEDIT_K_* symbols)
    public const int STB_TEXTEDIT_K_LEFT = 0x200000;         // keyboard input to move cursor left
    public const int STB_TEXTEDIT_K_RIGHT = 0x200001;        // keyboard input to move cursor right
    public const int STB_TEXTEDIT_K_UP = 0x200002;           // keyboard input to move cursor up
    public const int STB_TEXTEDIT_K_DOWN = 0x200003;         // keyboard input to move cursor down
    public const int STB_TEXTEDIT_K_LINESTART = 0x200004;    // keyboard input to move cursor to start of line
    public const int STB_TEXTEDIT_K_LINEEND = 0x200005;      // keyboard input to move cursor to end of line
    public const int STB_TEXTEDIT_K_TEXTSTART = 0x200006;    // keyboard input to move cursor to start of text
    public const int STB_TEXTEDIT_K_TEXTEND = 0x200007;      // keyboard input to move cursor to end of text
    public const int STB_TEXTEDIT_K_DELETE = 0x200008;       // keyboard input to delete selection or character under cursor
    public const int STB_TEXTEDIT_K_BACKSPACE = 0x200009;    // keyboard input to delete selection or character left of cursor
    public const int STB_TEXTEDIT_K_UNDO = 0x20000A;         // keyboard input to perform undo
    public const int STB_TEXTEDIT_K_REDO = 0x20000B;         // keyboard input to perform redo
    public const int STB_TEXTEDIT_K_WORDLEFT = 0x20000C;     // keyboard input to move cursor left one word
    public const int STB_TEXTEDIT_K_WORDRIGHT = 0x20000D;    // keyboard input to move cursor right one word
    public const int STB_TEXTEDIT_K_PGUP = 0x20000E;         // keyboard input to move cursor up a page
    public const int STB_TEXTEDIT_K_PGDOWN = 0x20000F;       // keyboard input to move cursor down a page
    public const int STB_TEXTEDIT_K_SHIFT = 0x400000;

    // imgui_internal.h: #define IMSTB_TEXTEDIT_GETWIDTH_NEWLINE   (-1.0f)
    public const float IMSTB_TEXTEDIT_GETWIDTH_NEWLINE = -1.0f;

    // imstb_textedit.h defaults
    public const int IMSTB_TEXTEDIT_UNDOSTATECOUNT = 99;
    public const int IMSTB_TEXTEDIT_UNDOCHARCOUNT = 999;

    // Result of layout query, used by stb_textedit to determine where the text in each row is (imstb_textedit.h StbTexteditRow)
    public struct StbTexteditRow
    {
        public float x0, x1;             // starting x location, end x location (allows for align=right, etc)
        public float baseline_y_delta;   // position of baseline relative to previous row's baseline
        public float ymin, ymax;         // height of row above and below baseline
        public int num_chars;
    }

    // imstb_textedit.h StbFindState
    private struct StbFindState
    {
        public float x, y;       // position of n'th character
        public float height;     // height of line
        public int first_char;   // first char of row
        public int length;       // length of row
        public int prev_first;   // first char of previous row
    }

    //-------------------------------------------------------------------------
    // STB_TEXTEDIT string access glue (imgui_widgets.cpp namespace ImStb)
    //-------------------------------------------------------------------------

    // C++: STB_TEXTEDIT_STRINGLEN(const ImGuiInputTextState* obj) -> obj->TextLen
    private static int StbTexteditStringLen(ImGuiInputTextState obj) => obj.TextLen;

    // C++: STB_TEXTEDIT_GETCHAR(const ImGuiInputTextState* obj, int idx) -> obj->TextSrc[idx]
    public static char StbTexteditGetChar(ImGuiInputTextState obj, int idx)
    {
        byte[] src = obj.TextSrc ?? obj.TextA.Data;
        Debug.Assert(idx >= 0 && idx <= obj.TextLen);
        if (src == null || idx < 0 || idx >= src.Length)
            return '\0';
        return (char)src[idx];
    }

    // C++: STB_TEXTEDIT_GETWIDTH(ImGuiInputTextState* obj, int line_start_idx, int char_idx)
    private static float StbTexteditGetWidth(ImGuiInputTextState obj, int line_start_idx, int char_idx)
    {
        byte[] src = obj.TextSrc ?? obj.TextA.Data;
        if (src == null)
            return 0.0f;
        int c_len = ImText.ImTextCharFromUtf8(out uint c, src, line_start_idx + char_idx, obj.TextLen);
        if (c_len == 0)
            return 0.0f;
        if ((ImWchar)c == '\n')
            return IMSTB_TEXTEDIT_GETWIDTH_NEWLINE;
        ImGuiContext g = obj.Ctx;
        return g.FontBaked.GetCharAdvance((ImWchar)c) * g.FontBakedScale;
    }

    // C++: STB_TEXTEDIT_LAYOUTROW(StbTexteditRow* r, ImGuiInputTextState* obj, int line_start_idx)
    private static void StbTexteditLayoutRow(out StbTexteditRow r, ImGuiInputTextState obj, int line_start_idx)
    {
        byte[] text = obj.TextSrc ?? obj.TextA.Data ?? Array.Empty<byte>();
        // const ImVec2 size = InputTextCalcTextSize(obj->Ctx, text + line_start_idx, text + obj->TextLen, text + obj->TextLen, &text_remaining, NULL, StopOnNewLine | WrapKeepBlanks);
        ImVec2 size = ImGui.InputTextCalcTextSize(text, line_start_idx, obj.TextLen, obj.TextLen, out int text_remaining, null, ImDrawTextFlags_StopOnNewLine | ImDrawTextFlags_WrapKeepBlanks);
        r.x0 = 0.0f;
        r.x1 = size.x;
        r.baseline_y_delta = size.y;
        r.ymin = 0.0f;
        r.ymax = size.y;
        r.num_chars = text_remaining;
    }

    // C++: IMSTB_TEXTEDIT_GETNEXTCHARINDEX_IMPL(ImGuiInputTextState* obj, int idx)
    public static int StbTexteditGetNextCharIndex(ImGuiInputTextState obj, int idx)
    {
        if (idx >= obj.TextLen)
            return obj.TextLen + 1;
        byte[] src = obj.TextSrc ?? obj.TextA.Data;
        if (src == null)
            return idx + 1;
        int c_len = ImText.ImTextCharFromUtf8(out uint c, src, idx, obj.TextLen);
        return idx + (c_len > 0 ? c_len : 1);
    }

    // C++: IMSTB_TEXTEDIT_GETPREVCHARINDEX_IMPL(ImGuiInputTextState* obj, int idx)
    public static int StbTexteditGetPrevCharIndex(ImGuiInputTextState obj, int idx)
    {
        if (idx <= 0)
            return -1;
        byte[] src = obj.TextSrc ?? obj.TextA.Data;
        if (src == null)
            return idx - 1;
        return ImTextFindPreviousUtf8Codepoint(src, 0, idx);
    }

    // C++ imgui_draw.cpp: const char* ImTextFindPreviousUtf8Codepoint(const char* in_text_start, const char* in_p)
    // C# port: returns the byte offset of the codepoint starting immediately before offset 'p'.
    internal static int ImTextFindPreviousUtf8Codepoint(byte[] buf, int start, int p)
    {
        while (p > start)
        {
            p--;
            if ((p >= buf.Length) || ((buf[p] & 0xC0) != 0x80))
                return p;
        }
        return start;
    }

    // C++ imgui.cpp: const char* ImTextFindValidUtf8CodepointEnd(const char* in_text_start, const char* in_text_end, const char* in_p)
    // C# port: 'p' is a byte offset; returns the offset of the end of the valid codepoint at or before it.
    internal static int ImTextFindValidUtf8CodepointEnd(byte[] buf, int text_start, int text_end, int p)
    {
        if (text_start == p)
            return text_start;
        int prev = ImTextFindPreviousUtf8Codepoint(buf, text_start, p);
        int prev_c_len = ImText.ImTextCharFromUtf8(out uint prev_c, buf, prev, text_end);
        if (prev_c != ImUtil.IM_UNICODE_CODEPOINT_INVALID && prev_c_len > 0 && prev_c_len <= p - prev)
            return p;
        return prev;
    }

    // C++: static bool ImCharIsSeparatorW(unsigned int c)
    private static bool ImCharIsSeparatorW(uint c)
    {
        ReadOnlySpan<uint> separator_list = stackalloc uint[]
        {
            ',', 0x3001, '.', 0x3002, ';', 0xFF1B, '(', 0xFF08, ')', 0xFF09, '{', 0xFF5B, '}', 0xFF5D,
            '[', 0x300C, ']', 0x300D, '|', 0xFF5C, '!', 0xFF01, '\\', 0xFFE5, '/', 0x30FB, 0xFF0F,
            '\n', '\r',
        };
        for (int i = 0; i < separator_list.Length; i++)
            if (c == separator_list[i])
                return true;
        return false;
    }

    // C++: static int is_word_boundary_from_right(ImGuiInputTextState* obj, int idx)
    private static int IsWordBoundaryFromRight(ImGuiInputTextState obj, int idx)
    {
        // When ImGuiInputTextFlags_Password is set, we don't want actions such as Ctrl+Arrow to leak the fact that underlying data are blanks or separators.
        if ((obj.Flags & ImGuiInputTextFlags_Password) != 0 || idx <= 0)
            return 0;

        byte[] src = obj.TextSrc ?? obj.TextA.Data;
        int curr_p = idx;
        int prev_p = ImTextFindPreviousUtf8Codepoint(src, 0, curr_p);
        ImText.ImTextCharFromUtf8(out uint curr_c, src, curr_p, obj.TextLen);
        ImText.ImTextCharFromUtf8(out uint prev_c, src, prev_p, obj.TextLen);

        bool prev_white = ImUtil.ImCharIsBlankW(prev_c);
        bool prev_separ = ImCharIsSeparatorW(prev_c);
        bool curr_white = ImUtil.ImCharIsBlankW(curr_c);
        bool curr_separ = ImCharIsSeparatorW(curr_c);
        return ((prev_white || prev_separ) && !(curr_separ || curr_white)) || (curr_separ && !prev_separ) ? 1 : 0;
    }

    // C++: static int is_word_boundary_from_left(ImGuiInputTextState* obj, int idx)
    // (kept exactly as the C++ code, including the swapped prev_c/curr_c decode sources)
    private static int IsWordBoundaryFromLeft(ImGuiInputTextState obj, int idx)
    {
        if ((obj.Flags & ImGuiInputTextFlags_Password) != 0 || idx <= 0)
            return 0;

        byte[] src = obj.TextSrc ?? obj.TextA.Data;
        int curr_p = idx;
        int prev_p = ImTextFindPreviousUtf8Codepoint(src, 0, curr_p);
        ImText.ImTextCharFromUtf8(out uint prev_c, src, curr_p, obj.TextLen);
        ImText.ImTextCharFromUtf8(out uint curr_c, src, prev_p, obj.TextLen);

        bool prev_white = ImUtil.ImCharIsBlankW(prev_c);
        bool prev_separ = ImCharIsSeparatorW(prev_c);
        bool curr_white = ImUtil.ImCharIsBlankW(curr_c);
        bool curr_separ = ImCharIsSeparatorW(curr_c);
        return ((prev_white) && !(curr_separ || curr_white)) || (curr_separ && !prev_separ) ? 1 : 0;
    }

    // C++: static int STB_TEXTEDIT_MOVEWORDLEFT_IMPL(ImGuiInputTextState* obj, int idx)
    public static int StbTexteditMoveWordLeft(ImGuiInputTextState obj, int idx)
    {
        idx = StbTexteditGetPrevCharIndex(obj, idx);
        while (idx >= 0 && IsWordBoundaryFromRight(obj, idx) == 0)
            idx = StbTexteditGetPrevCharIndex(obj, idx);
        return idx < 0 ? 0 : idx;
    }

    // C++: static int STB_TEXTEDIT_MOVEWORDRIGHT_MAC(ImGuiInputTextState* obj, int idx)
    public static int StbTexteditMoveWordRightMac(ImGuiInputTextState obj, int idx)
    {
        int len = obj.TextLen;
        idx = StbTexteditGetNextCharIndex(obj, idx);
        while (idx < len && IsWordBoundaryFromLeft(obj, idx) == 0)
            idx = StbTexteditGetNextCharIndex(obj, idx);
        return idx > len ? len : idx;
    }

    // C++: static int STB_TEXTEDIT_MOVEWORDRIGHT_WIN(ImGuiInputTextState* obj, int idx)
    public static int StbTexteditMoveWordRightWin(ImGuiInputTextState obj, int idx)
    {
        idx = StbTexteditGetNextCharIndex(obj, idx);
        int len = obj.TextLen;
        while (idx < len && IsWordBoundaryFromRight(obj, idx) == 0)
            idx = StbTexteditGetNextCharIndex(obj, idx);
        return idx > len ? len : idx;
    }

    // C++: static int STB_TEXTEDIT_MOVEWORDRIGHT_IMPL(ImGuiInputTextState* obj, int idx)
    public static int StbTexteditMoveWordRight(ImGuiInputTextState obj, int idx)
    {
        ImGuiContext g = obj.Ctx;
        if (g.IO.ConfigMacOSXBehaviors)
            return StbTexteditMoveWordRightMac(obj, idx);
        else
            return StbTexteditMoveWordRightWin(obj, idx);
    }

    // C++ ImStrbol: find beginning-of-line. C# port over byte buffer offsets.
    private static int ImStrbol(byte[] buf, int buf_mid_line, int buf_begin)
    {
        while (buf_mid_line > buf_begin && buf_mid_line > 0 && buf[buf_mid_line - 1] != (byte)'\n')
            buf_mid_line--;
        return buf_mid_line;
    }

    // C++: static int STB_TEXTEDIT_MOVELINESTART_IMPL(ImGuiInputTextState* obj, ImStb::STB_TexteditState* state, int cursor)
    // Reimplementation of stb_textedit_move_line_start()/stb_textedit_move_line_end() which supports word-wrapping.
    public static int StbTexteditMoveLineStart(ImGuiInputTextState obj, ImStbTexteditState state, int cursor)
    {
        if (state.single_line != 0)
            return 0;

        if (obj.WrapWidth > 0.0f && obj.TextSrc != null)
        {
            int p_cursor = cursor;                                          // obj->TextSrc + cursor
            int p_bol = ImStrbol(obj.TextSrc, p_cursor, 0);
            int p = p_bol;
            int text_end = obj.TextLen;                                     // End of line would be enough
            while (p >= p_bol)
            {
                int p_eol = ImGui.ImFontCalcWordWrapPositionExBytes(obj.TextSrc, p, text_end, obj.WrapWidth, ImDrawTextFlags_WrapKeepBlanks);
                if (p == p_cursor) // If we are already on a visible beginning-of-line, return real beginning-of-line (would be same as regular handler below)
                    return p_bol;
                if (p_eol == p_cursor && obj.TextA[cursor] != (byte)'\n' && obj.LastMoveDirectionLR == (sbyte)ImGuiDir_Left)
                    return p_bol;
                if (p_eol >= p_cursor)
                    return p;
                p = (obj.TextSrc[p_eol] == (byte)'\n') ? p_eol + 1 : p_eol;
            }
        }

        // Regular handler, same as stb_textedit_move_line_start()
        while (cursor > 0)
        {
            int prev_cursor = StbTexteditGetPrevCharIndex(obj, cursor);
            if (StbTexteditGetChar(obj, prev_cursor) == '\n')
                break;
            cursor = prev_cursor;
        }
        return cursor;
    }

    // C++: static int STB_TEXTEDIT_MOVELINEEND_IMPL(ImGuiInputTextState* obj, ImStb::STB_TexteditState* state, int cursor)
    public static int StbTexteditMoveLineEnd(ImGuiInputTextState obj, ImStbTexteditState state, int cursor)
    {
        int n = StbTexteditStringLen(obj);
        if (state.single_line != 0)
            return n;

        if (obj.WrapWidth > 0.0f && obj.TextSrc != null)
        {
            int p_cursor = cursor;
            int p = ImStrbol(obj.TextSrc, p_cursor, 0);
            int text_end = obj.TextLen;                                     // End of line would be enough
            while (p < text_end)
            {
                int p_eol = ImGui.ImFontCalcWordWrapPositionExBytes(obj.TextSrc, p, text_end, obj.WrapWidth, ImDrawTextFlags_WrapKeepBlanks);
                cursor = p_eol;
                if (p_eol == p_cursor && obj.LastMoveDirectionLR != (sbyte)ImGuiDir_Left) // If we are already on a visible end-of-line, switch to regular handle
                    break;
                if (p_eol > p_cursor)
                    return cursor;
                p = (obj.TextSrc[p_eol] == (byte)'\n') ? p_eol + 1 : p_eol;
            }
        }
        // Regular handler, same as stb_textedit_move_line_end()
        while (cursor < n && StbTexteditGetChar(obj, cursor) != '\n')
            cursor = StbTexteditGetNextCharIndex(obj, cursor);
        return cursor;
    }

    // C++: static void STB_TEXTEDIT_DELETECHARS(ImGuiInputTextState* obj, int pos, int n)
    private static void StbTexteditDeleteChars(ImGuiInputTextState obj, int pos, int n)
    {
        // Offset remaining text (+ copy zero terminator)
        Debug.Assert(obj.TextSrc == obj.TextA.Data);
        byte[] data = obj.TextA.Data;
        if (data == null || n <= 0)
            return;
        Array.Copy(data, pos + n, data, pos, obj.TextLen - n - pos + 1);
        obj.EditedBefore = obj.EditedThisFrame = true;
        obj.TextLen -= n;
    }

    // C++: static int STB_TEXTEDIT_INSERTCHARS(ImGuiInputTextState* obj, int pos, const char* new_text, int new_text_len)
    // C# port: 'new_text_off' is the offset of the new text within 'new_text' (callers often insert from the undo buffer).
    private static int StbTexteditInsertChars(ImGuiInputTextState obj, int pos, byte[] new_text, int new_text_off, int new_text_len)
    {
        bool is_resizable = (obj.Flags & ImGuiInputTextFlags_CallbackResize) != 0;
        int text_len = obj.TextLen;
        Debug.Assert(pos <= text_len);

        // We support partial insertion (with a mod in stb_textedit.h)
        int avail = obj.BufCapacity - 1 - obj.TextLen;
        if (!is_resizable && new_text_len > avail)
            new_text_len = ImTextFindValidUtf8CodepointEnd(new_text, new_text_off, new_text_off + new_text_len, new_text_off + avail) - new_text_off; // Truncate to closest UTF-8 codepoint. Alternative: return 0 to cancel insertion.
        if (new_text_len == 0)
            return 0;

        // Grow internal buffer if needed
        Debug.Assert(obj.TextSrc == obj.TextA.Data);
        if (text_len + new_text_len + 1 > obj.TextA.Size && is_resizable)
        {
            obj.TextA.resize(text_len + ImMath.ImClamp(new_text_len, 32, ImMath.ImMax(256, new_text_len)) + 1);
            obj.TextSrc = obj.TextA.Data;
        }

        byte[] text = obj.TextA.Data;
        if (pos != text_len)
            Array.Copy(text, pos, text, pos + new_text_len, text_len - pos);
        Array.Copy(new_text, new_text_off, text, pos, new_text_len);

        obj.EditedBefore = obj.EditedThisFrame = true;
        obj.TextLen += new_text_len;
        text[obj.TextLen] = 0;

        return new_text_len;
    }

    private static int StbTexteditInsertChars(ImGuiInputTextState obj, int pos, byte[] new_text, int new_text_len)
    {
        return StbTexteditInsertChars(obj, pos, new_text, 0, new_text_len);
    }

    //-------------------------------------------------------------------------
    // Mouse input handling (imstb_textedit.h)
    //-------------------------------------------------------------------------

    // traverse the layout to locate the nearest character to a display position
    // C++: static int stb_text_locate_coord(IMSTB_TEXTEDIT_STRING* str, float x, float y, int* out_side_on_line)
    private static int StbTextLocateCoord(ImGuiInputTextState str, float x, float y, out int out_side_on_line)
    {
        StbTexteditRow r = default;
        int n = StbTexteditStringLen(str);
        float base_y = 0, prev_x;
        int i = 0, k;

        r.x0 = r.x1 = 0;
        r.ymin = r.ymax = 0;
        r.num_chars = 0;
        out_side_on_line = 0;

        // search rows to find one that straddles 'y'
        while (i < n)
        {
            StbTexteditLayoutRow(out r, str, i);
            if (r.num_chars <= 0)
            {
                out_side_on_line = 0;
                return n;
            }

            if (i == 0 && y < base_y + r.ymin)
            {
                out_side_on_line = 0;
                return 0;
            }

            if (y < base_y + r.ymax)
                break;

            i += r.num_chars;
            base_y += r.baseline_y_delta;
        }

        // below all text, return 'after' last character
        if (i >= n)
        {
            out_side_on_line = 1;
            return n;
        }

        // check if it's before the beginning of the line
        if (x < r.x0)
        {
            out_side_on_line = 0;
            return i;
        }

        // check if it's before the end of the line
        if (x < r.x1)
        {
            // search characters in row for one that straddles 'x'
            prev_x = r.x0;
            for (k = 0; k < r.num_chars; k = StbTexteditGetNextCharIndex(str, i + k) - i)
            {
                float w = StbTexteditGetWidth(str, i, k);
                if (x < prev_x + w)
                {
                    out_side_on_line = (k == 0) ? 0 : 1;
                    if (x < prev_x + w / 2)
                        return k + i;
                    else
                        return StbTexteditGetNextCharIndex(str, i + k);
                }
                prev_x += w;
            }
            // shouldn't happen, but if it does, fall through to end-of-line case
        }

        // if the last character is a newline, return that. otherwise return 'after' the last character
        out_side_on_line = 1;
        if (StbTexteditGetChar(str, i + r.num_chars - 1) == '\n')
            return i + r.num_chars - 1;
        else
            return i + r.num_chars;
    }

    // API click: on mouse down, move the cursor to the clicked location, and reset the selection
    // C++: static void stb_textedit_click(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, float x, float y)
    public static void StbTexteditClick(ImGuiInputTextState str, float x, float y)
    {
        ImStbTexteditState state = str.Stb;
        // In single-line mode, just always make y = 0. This lets the drag keep working if the mouse goes off the top or bottom of the text
        int side_on_line;
        if (state.single_line != 0)
        {
            StbTexteditLayoutRow(out StbTexteditRow r, str, 0);
            y = r.ymin;
        }

        state.cursor = StbTextLocateCoord(str, x, y, out side_on_line);
        state.select_start = state.cursor;
        state.select_end = state.cursor;
        state.has_preferred_x = 0;
        str.LastMoveDirectionLR = (sbyte)(side_on_line != 0 ? ImGuiDir_Right : ImGuiDir_Left);
    }

    // API drag: on mouse drag, move the cursor and selection endpoint to the clicked location
    // C++: static void stb_textedit_drag(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, float x, float y)
    public static void StbTexteditDrag(ImGuiInputTextState str, float x, float y)
    {
        ImStbTexteditState state = str.Stb;
        int p = 0;
        int side_on_line;

        // In single-line mode, just always make y = 0. This lets the drag keep working if the mouse goes off the top or bottom of the text
        if (state.single_line != 0)
        {
            StbTexteditLayoutRow(out StbTexteditRow r, str, 0);
            y = r.ymin;
        }

        if (state.select_start == state.select_end)
            state.select_start = state.cursor;

        p = StbTextLocateCoord(str, x, y, out side_on_line);
        state.cursor = state.select_end = p;
        str.LastMoveDirectionLR = (sbyte)(side_on_line != 0 ? ImGuiDir_Right : ImGuiDir_Left);
    }

    //-------------------------------------------------------------------------
    // Keyboard input handling (imstb_textedit.h)
    //-------------------------------------------------------------------------

    // find the x/y location of a character, and remember info about the previous row in
    // case we get a move-up event (for page up, we'll have to rescan)
    // C++: static void stb_textedit_find_charpos(StbFindState* find, IMSTB_TEXTEDIT_STRING* str, int n, int single_line)
    private static void StbTexteditFindCharpos(out StbFindState find, ImGuiInputTextState str, int n, int single_line)
    {
        find = default;
        StbTexteditRow r = default;
        int prev_start = 0;
        int z = StbTexteditStringLen(str);
        int i = 0, first;

        if (n == z && single_line != 0)
        {
            // special case if it's at the end (may not be needed?)
            StbTexteditLayoutRow(out r, str, 0);
            find.y = 0;
            find.first_char = 0;
            find.length = z;
            find.height = r.ymax - r.ymin;
            find.x = r.x1;
            return;
        }

        // search rows to find the one that straddles character n
        find.y = 0;

        for (; ; )
        {
            StbTexteditLayoutRow(out r, str, i);
            if (n < i + r.num_chars)
                break;
            if (str.LastMoveDirectionLR == ImGuiDir_Right && str.Stb.cursor > 0 && str.Stb.cursor == i + r.num_chars && StbTexteditGetChar(str, i + r.num_chars - 1) != '\n') // [DEAR IMGUI] Wrapping point handling
                break;
            if (i + r.num_chars == z && z > 0 && StbTexteditGetChar(str, z - 1) != '\n')  // [DEAR IMGUI] special handling for last line
                break;   // [DEAR IMGUI]
            prev_start = i;
            i += r.num_chars;
            find.y += r.baseline_y_delta;
            if (i == z) // [DEAR IMGUI]
            {
                r.num_chars = 0; // [DEAR IMGUI]
                break;   // [DEAR IMGUI]
            }
        }

        find.first_char = first = i;
        find.length = r.num_chars;
        find.height = r.ymax - r.ymin;
        find.prev_first = prev_start;

        // now scan to find xpos
        find.x = r.x0;
        for (i = 0; first + i < n; i = StbTexteditGetNextCharIndex(str, first + i) - first)
            find.x += StbTexteditGetWidth(str, first, i);
    }

    // C++: #define STB_TEXT_HAS_SELECTION(s) ((s)->select_start != (s)->select_end)
    private static bool StbTextHasSelection(ImStbTexteditState s) => s.select_start != s.select_end;

    // make the selection/cursor state valid if client altered the string
    // C++: static void stb_textedit_clamp(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state)
    public static void StbTexteditClamp(ImGuiInputTextState str)
    {
        ImStbTexteditState state = str.Stb;
        int n = StbTexteditStringLen(str);
        if (StbTextHasSelection(state))
        {
            if (state.select_start > n) state.select_start = n;
            if (state.select_end > n) state.select_end = n;
            // if clamping forced them to be equal, move the cursor to match
            if (state.select_start == state.select_end)
                state.cursor = state.select_start;
        }
        if (state.cursor > n)
            state.cursor = n;
    }

    // delete characters while updating undo
    // C++: static void stb_textedit_delete(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, int where, int len)
    private static void StbTexteditDelete(ImGuiInputTextState str, ImStbTexteditState state, int where, int len)
    {
        StbTextMakeundoDelete(str, state, where, len);
        StbTexteditDeleteChars(str, where, len);
        state.has_preferred_x = 0;
    }

    // delete the section
    // C++: static void stb_textedit_delete_selection(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state)
    private static void StbTexteditDeleteSelection(ImGuiInputTextState str, ImStbTexteditState state)
    {
        StbTexteditClamp(str);
        if (StbTextHasSelection(state))
        {
            if (state.select_start < state.select_end)
            {
                StbTexteditDelete(str, state, state.select_start, state.select_end - state.select_start);
                state.select_end = state.cursor = state.select_start;
            }
            else
            {
                StbTexteditDelete(str, state, state.select_end, state.select_start - state.select_end);
                state.select_start = state.cursor = state.select_end;
            }
            state.has_preferred_x = 0;
        }
    }

    // canoncialize the selection so start <= end
    // C++: static void stb_textedit_sortselection(STB_TexteditState* state)
    private static void StbTexteditSortselection(ImStbTexteditState state)
    {
        if (state.select_end < state.select_start)
        {
            (state.select_start, state.select_end) = (state.select_end, state.select_start);
        }
    }

    // move cursor to first character of selection
    // C++: static void stb_textedit_move_to_first(STB_TexteditState* state)
    private static void StbTexteditMoveToFirst(ImStbTexteditState state)
    {
        if (StbTextHasSelection(state))
        {
            StbTexteditSortselection(state);
            state.cursor = state.select_start;
            state.select_end = state.select_start;
            state.has_preferred_x = 0;
        }
    }

    // move cursor to last character of selection
    // C++: static void stb_textedit_move_to_last(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state)
    private static void StbTexteditMoveToLast(ImGuiInputTextState str, ImStbTexteditState state)
    {
        if (StbTextHasSelection(state))
        {
            StbTexteditSortselection(state);
            StbTexteditClamp(str);
            state.cursor = state.select_end;
            state.select_start = state.select_end;
            state.has_preferred_x = 0;
        }
    }

    // update selection and cursor to match each other
    // C++: static void stb_textedit_prep_selection_at_cursor(STB_TexteditState* state)
    public static void StbTexteditPrepSelectionAtCursor(ImStbTexteditState state)
    {
        if (!StbTextHasSelection(state))
            state.select_start = state.select_end = state.cursor;
        else
            state.cursor = state.select_end;
    }

    // API cut: delete selection
    // C++: static int stb_textedit_cut(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state)
    public static int StbTexteditCut(ImGuiInputTextState str)
    {
        ImStbTexteditState state = str.Stb;
        if (StbTextHasSelection(state))
        {
            StbTexteditDeleteSelection(str, state); // implicitly clamps
            state.has_preferred_x = 0;
            return 1;
        }
        return 0;
    }

    // API paste: replace existing selection with passed-in text
    // C++: static int stb_textedit_paste_internal(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, IMSTB_TEXTEDIT_CHARTYPE* text, int len)
    private static int StbTexteditPasteInternal(ImGuiInputTextState str, ImStbTexteditState state, byte[] text, int len)
    {
        // if there's a selection, the paste should delete it
        StbTexteditClamp(str);
        StbTexteditDeleteSelection(str, state);
        // try to insert the characters
        len = StbTexteditInsertChars(str, state.cursor, text, len);
        if (len != 0)
        {
            StbTextMakeundoInsert(state, state.cursor, len);
            state.cursor += len;
            state.has_preferred_x = 0;
            return 1;
        }
        // note: paste failure will leave deleted selection, may be restored with an undo (see https://github.com/nothings/stb/issues/734 for details)
        return 0;
    }

    // C++: static int stb_textedit_paste(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, IMSTB_TEXTEDIT_CHARTYPE const* ctext, int len)
    public static int StbTexteditPaste(ImGuiInputTextState str, byte[] ctext, int len)
    {
        return StbTexteditPasteInternal(str, str.Stb, ctext, len);
    }

    // API key: process text input
    // [DEAR IMGUI] Added stb_textedit_text(), extracted out and called by stb_textedit_key() for backward compatibility.
    // C++: static void stb_textedit_text(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, const IMSTB_TEXTEDIT_CHARTYPE* text, int text_len)
    public static void StbTexteditText(ImGuiInputTextState str, byte[] text, int text_len)
    {
        ImStbTexteditState state = str.Stb;
        if (text_len <= 0)
            return; // (C++ reads text[0] unconditionally; callers never pass an empty string)

        // can't add newline in single-line mode
        if (text[0] == (byte)'\n' && state.single_line != 0)
            return;

        if (state.insert_mode != 0 && !StbTextHasSelection(state) && state.cursor < StbTexteditStringLen(str))
        {
            StbTextMakeundoReplace(str, state, state.cursor, 1, 1);
            StbTexteditDeleteChars(str, state.cursor, 1);
            text_len = StbTexteditInsertChars(str, state.cursor, text, text_len);
            if (text_len != 0)
            {
                state.cursor += text_len;
                state.has_preferred_x = 0;
            }
        }
        else
        {
            StbTexteditDeleteSelection(str, state); // implicitly clamps
            text_len = StbTexteditInsertChars(str, state.cursor, text, text_len);
            if (text_len != 0)
            {
                StbTextMakeundoInsert(state, state.cursor, text_len);
                state.cursor += text_len;
                state.has_preferred_x = 0;
            }
        }
    }

    // API key: process a keyboard input
    // C++: static void stb_textedit_key(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, STB_TEXTEDIT_KEYTYPE key)
    public static void StbTexteditKey(ImGuiInputTextState str, int key)
    {
        ImStbTexteditState state = str.Stb;
        if (state == null)
            return;
        if (str.TextSrc == null)
            str.TextSrc = str.TextA.Data ?? Array.Empty<byte>();

    retry:
        switch (key)
        {
            default:
                break;

            case STB_TEXTEDIT_K_UNDO:
                StbTextUndo(str, state);
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_REDO:
                StbTextRedo(str, state);
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_LEFT:
                // if currently there's a selection, move cursor to start of selection
                if (StbTextHasSelection(state))
                    StbTexteditMoveToFirst(state);
                else if (state.cursor > 0)
                    state.cursor = StbTexteditGetPrevCharIndex(str, state.cursor);
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_RIGHT:
                // if currently there's a selection, move cursor to end of selection
                if (StbTextHasSelection(state))
                    StbTexteditMoveToLast(str, state);
                else
                    state.cursor = StbTexteditGetNextCharIndex(str, state.cursor);
                StbTexteditClamp(str);
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_LEFT | STB_TEXTEDIT_K_SHIFT:
                StbTexteditClamp(str);
                StbTexteditPrepSelectionAtCursor(state);
                // move selection left
                if (state.select_end > 0)
                    state.select_end = StbTexteditGetPrevCharIndex(str, state.select_end);
                state.cursor = state.select_end;
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_WORDLEFT:
                if (StbTextHasSelection(state))
                    StbTexteditMoveToFirst(state);
                else
                {
                    state.cursor = StbTexteditMoveWordLeft(str, state.cursor);
                    StbTexteditClamp(str);
                }
                break;

            case STB_TEXTEDIT_K_WORDLEFT | STB_TEXTEDIT_K_SHIFT:
                if (!StbTextHasSelection(state))
                    StbTexteditPrepSelectionAtCursor(state);

                state.cursor = StbTexteditMoveWordLeft(str, state.cursor);
                state.select_end = state.cursor;

                StbTexteditClamp(str);
                break;

            case STB_TEXTEDIT_K_WORDRIGHT:
                if (StbTextHasSelection(state))
                    StbTexteditMoveToLast(str, state);
                else
                {
                    state.cursor = StbTexteditMoveWordRight(str, state.cursor);
                    StbTexteditClamp(str);
                }
                break;

            case STB_TEXTEDIT_K_WORDRIGHT | STB_TEXTEDIT_K_SHIFT:
                if (!StbTextHasSelection(state))
                    StbTexteditPrepSelectionAtCursor(state);

                state.cursor = StbTexteditMoveWordRight(str, state.cursor);
                state.select_end = state.cursor;

                StbTexteditClamp(str);
                break;

            case STB_TEXTEDIT_K_RIGHT | STB_TEXTEDIT_K_SHIFT:
                StbTexteditPrepSelectionAtCursor(state);
                // move selection right
                state.select_end = StbTexteditGetNextCharIndex(str, state.select_end);
                StbTexteditClamp(str);
                state.cursor = state.select_end;
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_DOWN:
            case STB_TEXTEDIT_K_DOWN | STB_TEXTEDIT_K_SHIFT:
            case STB_TEXTEDIT_K_PGDOWN:
            case STB_TEXTEDIT_K_PGDOWN | STB_TEXTEDIT_K_SHIFT:
            {
                StbFindState find;
                StbTexteditRow row;
                int i, j, sel = (key & STB_TEXTEDIT_K_SHIFT) != 0 ? 1 : 0;
                bool is_page = (key & ~STB_TEXTEDIT_K_SHIFT) == STB_TEXTEDIT_K_PGDOWN;
                int row_count = is_page ? state.row_count_per_page : 1;

                if (!is_page && state.single_line != 0)
                {
                    // on windows, up&down in single-line behave like left&right
                    key = STB_TEXTEDIT_K_RIGHT | (key & STB_TEXTEDIT_K_SHIFT);
                    goto retry;
                }

                if (sel != 0)
                    StbTexteditPrepSelectionAtCursor(state);
                else if (StbTextHasSelection(state))
                    StbTexteditMoveToLast(str, state);

                // compute current position of cursor point
                StbTexteditClamp(str);
                StbTexteditFindCharpos(out find, str, state.cursor, state.single_line);

                for (j = 0; j < row_count; ++j)
                {
                    float x, goal_x = state.has_preferred_x != 0 ? state.preferred_x : find.x;
                    int start = find.first_char + find.length;

                    if (find.length == 0)
                        break;

                    // [DEAR IMGUI]
                    // going down while being on the last line shouldn't bring us to that line end
                    //if (STB_TEXTEDIT_GETCHAR(str, find.first_char + find.length - 1) != STB_TEXTEDIT_NEWLINE)
                    //   break;

                    // now find character position down a row
                    state.cursor = start;
                    StbTexteditLayoutRow(out row, str, state.cursor);
                    x = row.x0;
                    for (i = 0; i < row.num_chars; )
                    {
                        float dx = StbTexteditGetWidth(str, start, i);
                        int next = StbTexteditGetNextCharIndex(str, state.cursor);
                        if (dx == IMSTB_TEXTEDIT_GETWIDTH_NEWLINE)
                            break;
                        x += dx;
                        if (x > goal_x)
                            break;
                        i += next - state.cursor;
                        state.cursor = next;
                    }
                    StbTexteditClamp(str);

                    if (state.cursor == find.first_char + find.length)
                        str.LastMoveDirectionLR = (sbyte)ImGuiDir_Left;
                    state.has_preferred_x = 1;
                    state.preferred_x = goal_x;

                    if (sel != 0)
                        state.select_end = state.cursor;

                    // go to next line
                    find.first_char = find.first_char + find.length;
                    find.length = row.num_chars;
                }
                break;
            }

            case STB_TEXTEDIT_K_UP:
            case STB_TEXTEDIT_K_UP | STB_TEXTEDIT_K_SHIFT:
            case STB_TEXTEDIT_K_PGUP:
            case STB_TEXTEDIT_K_PGUP | STB_TEXTEDIT_K_SHIFT:
            {
                StbFindState find;
                StbTexteditRow row;
                int i, j, prev_scan, sel = (key & STB_TEXTEDIT_K_SHIFT) != 0 ? 1 : 0;
                bool is_page = (key & ~STB_TEXTEDIT_K_SHIFT) == STB_TEXTEDIT_K_PGUP;
                int row_count = is_page ? state.row_count_per_page : 1;

                if (!is_page && state.single_line != 0)
                {
                    // on windows, up&down become left&right
                    key = STB_TEXTEDIT_K_LEFT | (key & STB_TEXTEDIT_K_SHIFT);
                    goto retry;
                }

                if (sel != 0)
                    StbTexteditPrepSelectionAtCursor(state);
                else if (StbTextHasSelection(state))
                    StbTexteditMoveToFirst(state);

                // compute current position of cursor point
                StbTexteditClamp(str);
                StbTexteditFindCharpos(out find, str, state.cursor, state.single_line);

                for (j = 0; j < row_count; ++j)
                {
                    float x, goal_x = state.has_preferred_x != 0 ? state.preferred_x : find.x;

                    // can only go up if there's a previous row
                    if (find.prev_first == find.first_char)
                        break;

                    // now find character position up a row
                    state.cursor = find.prev_first;
                    StbTexteditLayoutRow(out row, str, state.cursor);
                    x = row.x0;
                    for (i = 0; i < row.num_chars; )
                    {
                        float dx = StbTexteditGetWidth(str, find.prev_first, i);
                        int next = StbTexteditGetNextCharIndex(str, state.cursor);
                        if (dx == IMSTB_TEXTEDIT_GETWIDTH_NEWLINE)
                            break;
                        x += dx;
                        if (x > goal_x)
                            break;
                        i += next - state.cursor;
                        state.cursor = next;
                    }
                    StbTexteditClamp(str);

                    if (state.cursor == find.first_char)
                        str.LastMoveDirectionLR = (sbyte)ImGuiDir_Right;
                    else if (state.cursor == find.prev_first)
                        str.LastMoveDirectionLR = (sbyte)ImGuiDir_Left;
                    state.has_preferred_x = 1;
                    state.preferred_x = goal_x;

                    if (sel != 0)
                        state.select_end = state.cursor;

                    // go to previous line
                    // (we need to scan previous line the hard way. maybe we could expose this as a new API function?)
                    prev_scan = find.prev_first > 0 ? find.prev_first - 1 : 0;
                    while (prev_scan > 0)
                    {
                        int prev = StbTexteditGetPrevCharIndex(str, prev_scan);
                        if (StbTexteditGetChar(str, prev) == '\n')
                            break;
                        prev_scan = prev;
                    }
                    find.first_char = find.prev_first;
                    find.prev_first = StbTexteditMoveLineStart(str, state, prev_scan);
                }
                break;
            }

            case STB_TEXTEDIT_K_DELETE:
            case STB_TEXTEDIT_K_DELETE | STB_TEXTEDIT_K_SHIFT:
                if (StbTextHasSelection(state))
                    StbTexteditDeleteSelection(str, state);
                else
                {
                    int n = StbTexteditStringLen(str);
                    if (state.cursor < n)
                        StbTexteditDelete(str, state, state.cursor, StbTexteditGetNextCharIndex(str, state.cursor) - state.cursor);
                }
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_BACKSPACE:
            case STB_TEXTEDIT_K_BACKSPACE | STB_TEXTEDIT_K_SHIFT:
                if (StbTextHasSelection(state))
                    StbTexteditDeleteSelection(str, state);
                else
                {
                    StbTexteditClamp(str);
                    if (state.cursor > 0)
                    {
                        int prev = StbTexteditGetPrevCharIndex(str, state.cursor);
                        StbTexteditDelete(str, state, prev, state.cursor - prev);
                        state.cursor = prev;
                    }
                }
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_TEXTSTART:
                state.cursor = state.select_start = state.select_end = 0;
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_TEXTEND:
                state.cursor = StbTexteditStringLen(str);
                state.select_start = state.select_end = 0;
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_TEXTSTART | STB_TEXTEDIT_K_SHIFT:
                StbTexteditPrepSelectionAtCursor(state);
                state.cursor = state.select_end = 0;
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_TEXTEND | STB_TEXTEDIT_K_SHIFT:
                StbTexteditPrepSelectionAtCursor(state);
                state.cursor = state.select_end = StbTexteditStringLen(str);
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_LINESTART:
                StbTexteditClamp(str);
                StbTexteditMoveToFirst(state);
                state.cursor = StbTexteditMoveLineStart(str, state, state.cursor);
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_LINEEND:
                StbTexteditClamp(str);
                StbTexteditMoveToLast(str, state);
                state.cursor = StbTexteditMoveLineEnd(str, state, state.cursor);
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_LINESTART | STB_TEXTEDIT_K_SHIFT:
                StbTexteditClamp(str);
                StbTexteditPrepSelectionAtCursor(state);
                state.cursor = StbTexteditMoveLineStart(str, state, state.cursor);
                state.select_end = state.cursor;
                state.has_preferred_x = 0;
                break;

            case STB_TEXTEDIT_K_LINEEND | STB_TEXTEDIT_K_SHIFT:
                StbTexteditClamp(str);
                StbTexteditPrepSelectionAtCursor(state);
                state.cursor = StbTexteditMoveLineEnd(str, state, state.cursor);
                state.select_end = state.cursor;
                state.has_preferred_x = 0;
                break;
        }
    }

    //-------------------------------------------------------------------------
    // Undo processing (imstb_textedit.h)
    // @OPTIMIZE: the undo/redo buffer should be circular
    //-------------------------------------------------------------------------

    // C++: static void stb_textedit_flush_redo(StbUndoState* state)
    private static void StbTexteditFlushRedo(ImStbTexteditState state)
    {
        state.redo_point = IMSTB_TEXTEDIT_UNDOSTATECOUNT;
        state.redo_char_point = IMSTB_TEXTEDIT_UNDOCHARCOUNT;
    }

    // discard the oldest entry in the undo list
    // C++: static void stb_textedit_discard_undo(StbUndoState* state)
    private static void StbTexteditDiscardUndo(ImStbTexteditState state)
    {
        if (state.undo_point > 0)
        {
            // if the 0th undo state has characters, clean those up
            if (state.undo_rec[0].char_storage >= 0)
            {
                int n = state.undo_rec[0].insert_length;
                // delete n characters from all other records
                state.undo_char_point -= n;
                Array.Copy(state.undo_char, n, state.undo_char, 0, state.undo_char_point); // IMSTB_TEXTEDIT_memmove()
                for (int i = 0; i < state.undo_point; ++i)
                    if (state.undo_rec[i].char_storage >= 0)
                        state.undo_rec[i].char_storage -= n; // @OPTIMIZE: get rid of char_storage and infer it
            }
            state.undo_point--;
            Array.Copy(state.undo_rec, 1, state.undo_rec, 0, state.undo_point); // IMSTB_TEXTEDIT_memmove()
        }
    }

    // discard the oldest entry in the redo list--it's bad if this
    // ever happens, but because undo & redo have to store the actual
    // characters in different cases, the redo character buffer can
    // fill up even though the undo buffer didn't
    // C++: static void stb_textedit_discard_redo(StbUndoState* state)
    private static void StbTexteditDiscardRedo(ImStbTexteditState state)
    {
        int k = IMSTB_TEXTEDIT_UNDOSTATECOUNT - 1;

        if (state.redo_point <= k)
        {
            // if the k'th undo state has characters, clean those up
            if (state.undo_rec[k].char_storage >= 0)
            {
                int n = state.undo_rec[k].insert_length;
                // move the remaining redo character data to the end of the buffer
                state.redo_char_point += n;
                Array.Copy(state.undo_char, state.redo_char_point - n, state.undo_char, state.redo_char_point, IMSTB_TEXTEDIT_UNDOCHARCOUNT - state.redo_char_point); // IMSTB_TEXTEDIT_memmove()
                // adjust the position of all the other records to account for above memmove
                for (int i = state.redo_point; i < k; ++i)
                    if (state.undo_rec[i].char_storage >= 0)
                        state.undo_rec[i].char_storage += n;
            }
            // now move all the redo records towards the end of the buffer; the first one is at 'redo_point'
            int move_size = IMSTB_TEXTEDIT_UNDOSTATECOUNT - state.redo_point - 1;
            if (move_size > 0)
                Array.Copy(state.undo_rec, state.redo_point, state.undo_rec, state.redo_point + 1, move_size); // IMSTB_TEXTEDIT_memmove()

            // now move redo_point to point to the new one
            state.redo_point++;
        }
    }

    // C++: static StbUndoRecord* stb_text_create_undo_record(StbUndoState* state, int numchars)
    // Returns the index of the created record, or -1.
    private static int StbTextCreateUndoRecord(ImStbTexteditState state, int numchars)
    {
        // any time we create a new undo record, we discard redo
        StbTexteditFlushRedo(state);

        // if we have no free records, we have to make room, by sliding the existing records down
        if (state.undo_point == IMSTB_TEXTEDIT_UNDOSTATECOUNT)
            StbTexteditDiscardUndo(state);

        // if the characters to store won't possibly fit in the buffer, we can't undo
        if (numchars > IMSTB_TEXTEDIT_UNDOCHARCOUNT)
        {
            state.undo_point = 0;
            state.undo_char_point = 0;
            return -1;
        }

        // if we don't have enough free characters in the buffer, we have to make room
        while (state.undo_char_point + numchars > IMSTB_TEXTEDIT_UNDOCHARCOUNT)
            StbTexteditDiscardUndo(state);

        int idx = state.undo_point;
        state.undo_point++;
        return idx;
    }

    // C++: static IMSTB_TEXTEDIT_CHARTYPE* stb_text_createundo(StbUndoState* state, int pos, int insert_len, int delete_len)
    // C# port: returns the char_storage index into state.undo_char where callers must copy the deleted characters, or -1.
    public static int StbTextCreateundo(ImStbTexteditState state, int pos, int insert_len, int delete_len)
    {
        int r = StbTextCreateUndoRecord(state, insert_len);
        if (r == -1)
            return -1;

        state.undo_rec[r].where = pos;
        state.undo_rec[r].insert_length = insert_len;
        state.undo_rec[r].delete_length = delete_len;

        if (insert_len == 0)
        {
            state.undo_rec[r].char_storage = -1;
            return -1;
        }
        else
        {
            state.undo_rec[r].char_storage = state.undo_char_point;
            state.undo_char_point += insert_len;
            return state.undo_rec[r].char_storage;
        }
    }

    // C++: static void stb_text_undo(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state)
    private static void StbTextUndo(ImGuiInputTextState str, ImStbTexteditState state)
    {
        if (state.undo_point == 0)
            return;

        // we need to do two things: apply the undo record, and create a redo record
        StbUndoRecord u = state.undo_rec[state.undo_point - 1];
        int r_idx = state.redo_point - 1;
        state.undo_rec[r_idx].char_storage = -1;

        state.undo_rec[r_idx].insert_length = u.delete_length;
        state.undo_rec[r_idx].delete_length = u.insert_length;
        state.undo_rec[r_idx].where = u.where;

        if (u.delete_length != 0)
        {
            // if the undo record says to delete characters, then the redo record will
            // need to re-insert the characters that get deleted, so we need to store them.

            // there are three cases:
            //    there's enough room to store the characters
            //    characters stored for *redoing* don't leave room for redo
            //    characters stored for *undoing* don't leave room for redo
            // if the last is true, we have to bail

            if (state.undo_char_point + u.delete_length >= IMSTB_TEXTEDIT_UNDOCHARCOUNT)
            {
                // the undo records take up too much character space; there's no space to store the redo characters
                state.undo_rec[r_idx].insert_length = 0;
            }
            else
            {
                // there's definitely room to store the characters eventually
                while (state.undo_char_point + u.delete_length > state.redo_char_point)
                {
                    // should never happen:
                    if (state.redo_point == IMSTB_TEXTEDIT_UNDOSTATECOUNT)
                        return;
                    // there's currently not enough room, so discard a redo record
                    StbTexteditDiscardRedo(state);
                }
                r_idx = state.redo_point - 1;

                state.undo_rec[r_idx].char_storage = state.redo_char_point - u.delete_length;
                state.redo_char_point = state.redo_char_point - u.delete_length;

                // now save the characters
                for (int i = 0; i < u.delete_length; ++i)
                    state.undo_char[state.undo_rec[r_idx].char_storage + i] = (byte)StbTexteditGetChar(str, u.where + i);
            }

            // now we can carry out the deletion
            StbTexteditDeleteChars(str, u.where, u.delete_length);
        }

        // check type of recorded action:
        if (u.insert_length != 0)
        {
            // easy case: was a deletion, so we need to insert n characters
            u.insert_length = StbTexteditInsertChars(str, u.where, state.undo_char, u.char_storage, u.insert_length);
            state.undo_char_point -= u.insert_length;
        }

        state.cursor = u.where + u.insert_length;

        state.undo_point--;
        state.redo_point--;
    }

    // C++: static void stb_text_redo(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state)
    private static void StbTextRedo(ImGuiInputTextState str, ImStbTexteditState state)
    {
        if (state.redo_point == IMSTB_TEXTEDIT_UNDOSTATECOUNT)
            return;

        // we need to do two things: apply the redo record, and create an undo record
        StbUndoRecord u = state.undo_rec[state.undo_point];  // C++: u = &s->undo_rec[s->undo_point]
        StbUndoRecord r = state.undo_rec[state.redo_point];

        // we KNOW there must be room for the undo record, because the redo record was derived from an undo record

        u.delete_length = r.insert_length;
        u.insert_length = r.delete_length;
        u.where = r.where;
        u.char_storage = -1;

        if (r.delete_length != 0)
        {
            // the redo record requires us to delete characters, so the undo record needs to store the characters
            if (state.undo_char_point + u.insert_length > state.redo_char_point)
            {
                u.insert_length = 0;
                u.delete_length = 0;
            }
            else
            {
                u.char_storage = state.undo_char_point;
                state.undo_char_point = state.undo_char_point + u.insert_length;

                // now save the characters
                for (int i = 0; i < u.insert_length; ++i)
                    state.undo_char[u.char_storage + i] = (byte)StbTexteditGetChar(str, u.where + i);
            }

            StbTexteditDeleteChars(str, r.where, r.delete_length);
        }

        if (r.insert_length != 0)
        {
            // easy case: need to insert n characters
            r.insert_length = StbTexteditInsertChars(str, r.where, state.undo_char, r.char_storage, r.insert_length);
            state.redo_char_point += r.insert_length;
        }

        state.undo_rec[state.undo_point] = u;
        state.undo_rec[state.redo_point] = r;

        state.cursor = r.where + r.insert_length;

        state.undo_point++;
        state.redo_point++;
    }

    // C++: static void stb_text_makeundo_insert(STB_TexteditState* state, int where, int length)
    private static void StbTextMakeundoInsert(ImStbTexteditState state, int where, int length)
    {
        StbTextCreateundo(state, where, 0, length);
    }

    // C++: static void stb_text_makeundo_delete(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, int where, int length)
    private static void StbTextMakeundoDelete(ImGuiInputTextState str, ImStbTexteditState state, int where, int length)
    {
        int p = StbTextCreateundo(state, where, length, 0);
        if (p >= 0)
            for (int i = 0; i < length; ++i)
                state.undo_char[p + i] = (byte)StbTexteditGetChar(str, where + i);
    }

    // C++: static void stb_text_makeundo_replace(IMSTB_TEXTEDIT_STRING* str, STB_TexteditState* state, int where, int old_length, int new_length)
    private static void StbTextMakeundoReplace(ImGuiInputTextState str, ImStbTexteditState state, int where, int old_length, int new_length)
    {
        int p = StbTextCreateundo(state, where, old_length, new_length);
        if (p >= 0)
            for (int i = 0; i < old_length; ++i)
                state.undo_char[p + i] = (byte)StbTexteditGetChar(str, where + i);
    }

    //-------------------------------------------------------------------------
    // State initialization + the ImGui stb_textedit_replace() override
    //-------------------------------------------------------------------------

    // reset the state to default
    // C++: static void stb_textedit_clear_state(STB_TexteditState* state, int is_single_line)
    private static void StbTexteditClearState(ImStbTexteditState state, bool is_single_line)
    {
        state.undo_point = 0;
        state.undo_char_point = 0;
        state.redo_point = IMSTB_TEXTEDIT_UNDOSTATECOUNT;
        state.redo_char_point = IMSTB_TEXTEDIT_UNDOCHARCOUNT;
        state.select_end = state.select_start = 0;
        state.cursor = 0;
        state.has_preferred_x = 0;
        state.preferred_x = 0;
        state.cursor_at_end_of_line = 0;
        state.initialized = 1;
        state.single_line = (byte)(is_single_line ? 1 : 0);
        state.insert_mode = 0;
        state.row_count_per_page = 0;
    }

    // API initialize
    // C++: static void stb_textedit_initialize_state(STB_TexteditState* state, int is_single_line)
    public static void StbTexteditInitializeState(ImStbTexteditState state, bool is_single_line)
    {
        StbTexteditClearState(state, is_single_line);
    }

    // C++: static void stb_textedit_replace(ImGuiInputTextState* str, STB_TexteditState* state, const IMSTB_TEXTEDIT_CHARTYPE* text, int text_len)
    // stb_textedit internally allows for a single undo record to do addition and deletion, but somehow, calling
    // the stb_textedit_paste() function creates two separate records, so we perform it manually. (FIXME: Report to nothings/stb?)
    public static void StbTexteditReplace(ImGuiInputTextState str, byte[] text, int text_len)
    {
        ImStbTexteditState state = str.Stb;
        StbTextMakeundoReplace(str, state, 0, str.TextLen, text_len);
        StbTexteditDeleteChars(str, 0, str.TextLen);
        state.cursor = state.select_start = state.select_end = 0;
        if (text_len <= 0)
            return;
        int text_len_inserted = StbTexteditInsertChars(str, 0, text, text_len);
        if (text_len_inserted > 0)
        {
            state.cursor = state.select_start = state.select_end = text_len;
            state.has_preferred_x = 0;
            return;
        }
        Debug.Assert(false); // Failed to insert character, normally shouldn't happen because of how we currently use stb_textedit_replace()
    }

    //-------------------------------------------------------------------------
    // ImGuiInputTextState method backends (called via ImGuiUtil wrappers)
    //-------------------------------------------------------------------------

    // C++: float ImGuiInputTextState::GetPreferredOffsetX() const { return Stb->has_preferred_x ? Stb->preferred_x : -1; }
    public static float StateGetPreferredOffsetX(ImGuiInputTextState state)
    {
        return state.Stb.has_preferred_x != 0 ? state.Stb.preferred_x : -1.0f;
    }

    // C++: void ImGuiInputTextState::OnKeyPressed(int key)
    // (the stb_textedit_key call is done by ImGuiUtil.InputTextStateOnKeyPressed; the LastMoveDirectionLR update lives there for the
    // legacy skeleton; we keep the full behavior here by exposing the key handler. OnKeyPressed() itself:
    //   stb_textedit_key(this, Stb, key); CursorFollow = true; CursorAnimReset();
    //   then sets LastMoveDirectionLR based on key_u)
    public static void StateOnKeyPressed(ImGuiInputTextState state, int key)
    {
        StbTexteditKey(state, key);
        state.CursorFollow = true;
        state.CursorAnimReset();
        int key_u = (key & ~STB_TEXTEDIT_K_SHIFT);
        if (key_u == STB_TEXTEDIT_K_LEFT || key_u == STB_TEXTEDIT_K_LINESTART || key_u == STB_TEXTEDIT_K_TEXTSTART || key_u == STB_TEXTEDIT_K_BACKSPACE || key_u == STB_TEXTEDIT_K_WORDLEFT)
            state.LastMoveDirectionLR = (sbyte)ImGuiDir_Left;
        else if (key_u == STB_TEXTEDIT_K_RIGHT || key_u == STB_TEXTEDIT_K_LINEEND || key_u == STB_TEXTEDIT_K_TEXTEND || key_u == STB_TEXTEDIT_K_DELETE || key_u == STB_TEXTEDIT_K_WORDRIGHT)
            state.LastMoveDirectionLR = (sbyte)ImGuiDir_Right;
    }

    // C++: void ImGuiInputTextState::OnCharPressed(unsigned int c)
    // Convert the key to a UTF8 byte sequence.
    // The changes we had to make to stb_textedit_key made it very much UTF-8 specific which is not too great.
    public static void StbTexteditChar(ImGuiInputTextState state, uint c)
    {
        byte[] utf8 = new byte[5];
        int len = ImText.ImTextCharToUtf8(utf8, 0, c);
        if (len <= 0)
            return;
        utf8[len] = 0;
        StbTexteditText(state, utf8, len);
        state.CursorFollow = true;
        state.CursorAnimReset();
    }

    // C++: void ImGuiInputTextState::ReloadUserBufAndSelectAll() { WantReloadUserBuf = true; ReloadSelectionStart = 0; ReloadSelectionEnd = INT_MAX; }
    // C++: void ImGuiInputTextState::ReloadUserBufAndKeepSelection() { WantReloadUserBuf = true; ReloadSelectionStart = Stb->select_start; ReloadSelectionEnd = Stb->select_end; }
    // C++: void ImGuiInputTextState::ReloadUserBufAndMoveToEnd() { WantReloadUserBuf = true; ReloadSelectionStart = ReloadSelectionEnd = INT_MAX; }
    public static void StateReloadUserBuf(ImGuiInputTextState state, int mode)
    {
        switch (mode)
        {
            case 0: // ReloadUserBufAndSelectAll
                state.WantReloadUserBuf = true;
                state.ReloadSelectionStart = 0;
                state.ReloadSelectionEnd = int.MaxValue;
                break;
            case 1: // ReloadUserBufAndKeepSelection
                state.WantReloadUserBuf = true;
                state.ReloadSelectionStart = state.Stb.select_start;
                state.ReloadSelectionEnd = state.Stb.select_end;
                break;
            case 2: // ReloadUserBufAndMoveToEnd
                state.WantReloadUserBuf = true;
                state.ReloadSelectionStart = state.ReloadSelectionEnd = int.MaxValue;
                break;
        }
    }
}
