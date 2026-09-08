// Port of imgui_widgets.cpp ranges:
//   [SECTION] Widgets: Text, etc.                (lines 143-461)
//   [SECTION] Widgets: Main                      (lines 461-1593)
//   [SECTION] Widgets: Low-level Layout helpers  (lines 1593-1921, minus SeparatorTextEx/SeparatorText which are
//                                                 ported with the TreeNode range)
//   [SECTION] Widgets: Value helpers             (lines 9149-9185)
// Render helpers used here (RenderArrow/RenderBullet/RenderCheckMark/RenderRectFilledInRangeH) live in
// imgui_draw.cpp and are ported in ImGuiMiscHelpers.cs.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    // imgui_internal.h: #define DRAGDROP_HOLD_TO_OPEN_TIMER 0.70f
    private const float DRAGDROP_HOLD_TO_OPEN_TIMER = 0.70f;

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Text, etc.
    //-------------------------------------------------------------------------

    // C++: void ImGui::TextEx(const char* text, const char* text_end, ImGuiTextFlags flags)   [imgui_widgets.cpp 161]
    public static void TextEx(string text, int text_end = -1, ImGuiTextFlags flags = 0)
    {
        if (text == null)
        {
            text = "";
            text_end = 0;
        }
        TextEx(text.AsSpan(0, text_end < 0 ? text.Length : text_end), flags);
    }

    public static void TextEx(ReadOnlySpan<char> text, ImGuiTextFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;
        ImGuiContext g = GImGui;

        int text_begin = 0;
        int text_end = text.Length;

        ImVec2 text_pos = new ImVec2(window.DC.CursorPos.x, window.DC.CursorPos.y + window.DC.CurrLineTextBaseOffset);
        float wrap_pos_x = window.DC.TextWrapPos;
        bool wrap_enabled = (wrap_pos_x >= 0.0f);
        if (text_end - text_begin <= 2000 || wrap_enabled)
        {
            // Common case
            float wrap_width = wrap_enabled ? CalcWrapWidthForPos(window.DC.CursorPos, wrap_pos_x) : 0.0f;
            ImVec2 text_size = CalcTextSize(text, false, wrap_width);

            ImRect bb = new ImRect(text_pos, text_pos + text_size);
            ItemSize(text_size, 0.0f);
            if (!ItemAdd(bb, 0))
                return;

            // Render (we don't hide text after ## in this end-user function)
            RenderTextWrapped(bb.Min, text, wrap_width);
        }
        else
        {
            // Long text!
            // Perform manual coarse clipping to optimize for long multi-line text
            // - From this point we will only compute the width of lines that are visible. Optimization only available when word-wrapping is disabled.
            // - We also don't vertically center the text within the line full height, which is unlikely to matter because we are likely the biggest and only item on the line.
            // - We use memchr(), pay attention that well optimized versions of those str/mem functions are much faster than a casually written loop.
            int line = text_begin;
            float line_height = GetTextLineHeight();
            ImVec2 text_size = new ImVec2(0, 0);

            // Lines to skip (can't skip when logging text)
            ImVec2 pos = text_pos;
            if (!g.LogEnabled)
            {
                int lines_skippable = (int)((window.ClipRect.Min.y - text_pos.y) / line_height);
                if (lines_skippable > 0)
                {
                    int lines_skipped = 0;
                    while (line < text_end && lines_skipped < lines_skippable)
                    {
                        int line_end = TextFindLineEnd(text, line, text_end); // (C++: ImMemchr(line, '\n', text_end - line))
                        if ((flags & ImGuiTextFlags_NoWidthForLargeClippedText) == 0)
                            text_size.x = ImMath.ImMax(text_size.x, CalcTextSize(text.Slice(line, line_end - line), false).x);
                        line = line_end + 1;
                        lines_skipped++;
                    }
                    pos.y += lines_skipped * line_height;
                }
            }

            // Lines to render
            if (line < text_end)
            {
                ImRect line_rect = new ImRect(pos, pos + new ImVec2(float.MaxValue, line_height));
                while (line < text_end)
                {
                    if (IsClippedEx(line_rect, 0))
                        break;

                    int line_end = TextFindLineEnd(text, line, text_end);
                    text_size.x = ImMath.ImMax(text_size.x, CalcTextSize(text.Slice(line, line_end - line), false).x);
                    RenderText(pos, text.Slice(line, line_end - line), false);
                    line = line_end + 1;
                    line_rect.Min.y += line_height;
                    line_rect.Max.y += line_height;
                    pos.y += line_height;
                }

                // Count remaining lines
                int lines_skipped = 0;
                while (line < text_end)
                {
                    int line_end = TextFindLineEnd(text, line, text_end);
                    if ((flags & ImGuiTextFlags_NoWidthForLargeClippedText) == 0)
                        text_size.x = ImMath.ImMax(text_size.x, CalcTextSize(text.Slice(line, line_end - line), false).x);
                    line = line_end + 1;
                    lines_skipped++;
                }
                pos.y += lines_skipped * line_height;
            }
            text_size.y = (pos - text_pos).y;

            ImRect bb = new ImRect(text_pos, text_pos + text_size);
            ItemSize(text_size, 0.0f);
            ItemAdd(bb, 0);
        }
    }

    // (C++: (const char*)ImMemchr(line, '\n', text_end - line); returns text_end when not found)
    private static int TextFindLineEnd(ReadOnlySpan<char> text, int line, int text_end)
    {
        int idx = text.Slice(line, text_end - line).IndexOf('\n');
        return idx < 0 ? text_end : line + idx;
    }

    // Note that all functions taking format strings in the API may be passed ("%s", text) or ("%.*s", text_len, text),
    // which will automatically bypass the formatter.
    // C++: void ImGui::TextUnformatted(const char* text, const char* text_end)   [imgui_widgets.cpp 271]
    public static void TextUnformatted(string text, int text_end = -1)
    {
        TextEx(text, text_end, ImGuiTextFlags_NoWidthForLargeClippedText);
    }

    // C++: void ImGui::Text(const char* fmt, ...)   [imgui_widgets.cpp 276]
    public static void Text(string fmt, params object[] args)
    {
        TextV(fmt, args);
    }

    // C++: void ImGui::TextV(const char* fmt, va_list args)   [imgui_widgets.cpp 284]
    public static void TextV(string fmt, params object[] args)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        // C++: ImFormatStringToTempBufferV(&text, &text_end, fmt, args);
        string text = ImFmt.Sprintf(fmt, args);
        TextEx(text, text.Length, ImGuiTextFlags_NoWidthForLargeClippedText);
    }

    //-------------------------------------------------------------------------
    // Non-allocating generic overloads (printf-style, 1..4 arguments).
    //
    // These mirror the C++ variadic Text()/SliderFloat()/etc.: arguments stay typed
    // (no object[], no boxing) and the output is formatted into a stack buffer like
    // C++ ImFormatStringToTempBuffer(), falling back to a shared scratch buffer only
    // for oversized output. Output is byte-identical to the Sprintf-based path.
    //-------------------------------------------------------------------------

    static void TextFormatExV<T0, T1, T2, T3>(ImGuiTextFlags flags, string fmt, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        // C++: ImFormatStringToTempBufferV(&text, &text_end, fmt, args);
        Span<char> buf = stackalloc char[512];
        int n = ImFmt.FormatInto(buf, fmt, a0, a1, a2, a3);
        if (n > buf.Length)
        {
            char[] scratch = ImFmt.RentScratch(n);
            n = ImFmt.FormatInto(scratch, fmt, a0, a1, a2, a3);
            TextEx(scratch.AsSpan(0, n), flags);
            return;
        }
        TextEx(buf.Slice(0, n), flags);
    }

    public static void Text<T1>(string fmt, T1 a1) => TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, default(T1), default(T1), default(T1));
    public static void Text<T1, T2>(string fmt, T1 a1, T2 a2) => TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, default(T2), default(T2));
    public static void Text<T1, T2, T3>(string fmt, T1 a1, T2 a2, T3 a3) => TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, default(T3));
    public static void Text<T1, T2, T3, T4>(string fmt, T1 a1, T2 a2, T3 a3, T4 a4) => TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, a4);

    // C++: void ImGui::TextColored(const ImVec4& col, const char* fmt, ...)   [imgui_widgets.cpp 295]
    public static void TextColored(ImVec4 col, string fmt, params object[] args)
    {
        TextColoredV(col, fmt, args);
    }

    // C++: void ImGui::TextColoredV(const ImVec4& col, const char* fmt, va_list args)   [imgui_widgets.cpp 303]
    public static void TextColoredV(ImVec4 col, string fmt, params object[] args)
    {
        PushStyleColor(ImGuiCol_Text, col);
        TextV(fmt, args);
        PopStyleColor();
    }

    public static void TextColored<T1>(ImVec4 col, string fmt, T1 a1) { PushStyleColor(ImGuiCol_Text, col); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, default(T1), default(T1), default(T1)); PopStyleColor(); }
    public static void TextColored<T1, T2>(ImVec4 col, string fmt, T1 a1, T2 a2) { PushStyleColor(ImGuiCol_Text, col); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, default(T2), default(T2)); PopStyleColor(); }
    public static void TextColored<T1, T2, T3>(ImVec4 col, string fmt, T1 a1, T2 a2, T3 a3) { PushStyleColor(ImGuiCol_Text, col); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, default(T3)); PopStyleColor(); }
    public static void TextColored<T1, T2, T3, T4>(ImVec4 col, string fmt, T1 a1, T2 a2, T3 a3, T4 a4) { PushStyleColor(ImGuiCol_Text, col); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, a4); PopStyleColor(); }

    // C++: void ImGui::TextDisabled(const char* fmt, ...)   [imgui_widgets.cpp 310]
    public static void TextDisabled(string fmt, params object[] args)
    {
        TextDisabledV(fmt, args);
    }

    // C++: void ImGui::TextDisabledV(const char* fmt, va_list args)   [imgui_widgets.cpp 318]
    public static void TextDisabledV(string fmt, params object[] args)
    {
        ImGuiContext g = GImGui;
        PushStyleColor(ImGuiCol_Text, g.Style.Colors[ImGuiCol_TextDisabled]);
        TextV(fmt, args);
        PopStyleColor();
    }

    public static void TextDisabled<T1>(string fmt, T1 a1) { ImGuiContext g = GImGui; PushStyleColor(ImGuiCol_Text, g.Style.Colors[ImGuiCol_TextDisabled]); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, default(T1), default(T1), default(T1)); PopStyleColor(); }
    public static void TextDisabled<T1, T2>(string fmt, T1 a1, T2 a2) { ImGuiContext g = GImGui; PushStyleColor(ImGuiCol_Text, g.Style.Colors[ImGuiCol_TextDisabled]); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, default(T2), default(T2)); PopStyleColor(); }
    public static void TextDisabled<T1, T2, T3>(string fmt, T1 a1, T2 a2, T3 a3) { ImGuiContext g = GImGui; PushStyleColor(ImGuiCol_Text, g.Style.Colors[ImGuiCol_TextDisabled]); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, default(T3)); PopStyleColor(); }
    public static void TextDisabled<T1, T2, T3, T4>(string fmt, T1 a1, T2 a2, T3 a3, T4 a4) { ImGuiContext g = GImGui; PushStyleColor(ImGuiCol_Text, g.Style.Colors[ImGuiCol_TextDisabled]); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, a4); PopStyleColor(); }

    // C++: void ImGui::TextWrapped(const char* fmt, ...)   [imgui_widgets.cpp 326]
    public static void TextWrapped(string fmt, params object[] args)
    {
        TextWrappedV(fmt, args);
    }

    // C++: void ImGui::TextWrappedV(const char* fmt, va_list args)   [imgui_widgets.cpp 334]
    public static void TextWrappedV(string fmt, params object[] args)
    {
        ImGuiContext g = GImGui;
        bool need_backup = (g.CurrentWindow.DC.TextWrapPos < 0.0f);  // Keep existing wrap position if one is already set
        if (need_backup)
            PushTextWrapPos(0.0f);
        TextV(fmt, args);
        if (need_backup)
            PopTextWrapPos();
    }

    public static void TextWrapped<T1>(string fmt, T1 a1) { ImGuiContext g = GImGui; bool need_backup = (g.CurrentWindow.DC.TextWrapPos < 0.0f); if (need_backup) PushTextWrapPos(0.0f); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, default(T1), default(T1), default(T1)); if (need_backup) PopTextWrapPos(); }
    public static void TextWrapped<T1, T2>(string fmt, T1 a1, T2 a2) { ImGuiContext g = GImGui; bool need_backup = (g.CurrentWindow.DC.TextWrapPos < 0.0f); if (need_backup) PushTextWrapPos(0.0f); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, default(T2), default(T2)); if (need_backup) PopTextWrapPos(); }
    public static void TextWrapped<T1, T2, T3>(string fmt, T1 a1, T2 a2, T3 a3) { ImGuiContext g = GImGui; bool need_backup = (g.CurrentWindow.DC.TextWrapPos < 0.0f); if (need_backup) PushTextWrapPos(0.0f); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, default(T3)); if (need_backup) PopTextWrapPos(); }
    public static void TextWrapped<T1, T2, T3, T4>(string fmt, T1 a1, T2 a2, T3 a3, T4 a4) { ImGuiContext g = GImGui; bool need_backup = (g.CurrentWindow.DC.TextWrapPos < 0.0f); if (need_backup) PushTextWrapPos(0.0f); TextFormatExV(ImGuiTextFlags_NoWidthForLargeClippedText, fmt, a1, a2, a3, a4); if (need_backup) PopTextWrapPos(); }

    // C++: void ImGui::TextAligned(float align_x, float width, const char* fmt, ...)   [imgui_widgets.cpp 345]
    public static void TextAligned(float align_x, float width, string fmt, params object[] args)
    {
        TextAlignedV(align_x, width, fmt, args);
    }

    // align_x: 0.0f = left, 0.5f = center, 1.0f = right.
    // width < 0.0f (e.g. -FLT_MIN): shortcut for GetContentRegionAvail().x
    // FIXME-WIP: Works but API is likely to be reworked. This is designed for 1 item on the line. (#7024, #9455)
    // C++: void ImGui::TextAlignedV(float align_x, float width, const char* fmt, va_list args)   [imgui_widgets.cpp 356]
    public static void TextAlignedV(float align_x, float width, string fmt, params object[] args)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        // C++: ImFormatStringToTempBufferV(&text, &text_end, fmt, args);
        string text = ImFmt.Sprintf(fmt, args);
        int text_end = text.Length;
        ImVec2 text_size = CalcTextSize(text, text_end);

        ImVec2 size = CalcItemSize(new ImVec2(width, 0.0f), 0.0f, text_size.y);
        ImVec2 pos = new ImVec2(window.DC.CursorPos.x, window.DC.CursorPos.y + window.DC.CurrLineTextBaseOffset);
        ImVec2 pos_max = new ImVec2(pos.x + size.x, window.ClipRect.Max.y);

        window.DC.CursorMaxPos.x = ImMath.ImMax(window.DC.CursorMaxPos.x, pos.x + text_size.x);
        window.DC.IdealMaxPos.x = ImMath.ImMax(window.DC.IdealMaxPos.x, pos.x + text_size.x);
        ImVec2 backup_max_pos = window.DC.CursorMaxPos;
        ItemSize(size);
        bool is_visible = ItemAdd(new ImRect(pos, pos + size), 0);
        window.DC.CursorMaxPos.x = backup_max_pos.x; // Cancel out extending content to 'size' to use 'text_size' allowing auto-resize to function with align_x > 0.0f
        if (!is_visible)
            return;

        if (align_x > 0.0f && text_size.x < size.x)
            pos.x += ImMath.ImTrunc((size.x - text_size.x) * align_x);
        RenderTextEllipsis(window.DrawList, pos, pos_max, pos_max.x, text, text_end, text_size);

        // (C++: SetTooltip("%.*s", (int)(text_end - text), text);)
        if (size.x < text_size.x && IsItemHovered(ImGuiHoveredFlags_NoNavOverride | ImGuiHoveredFlags_AllowWhenDisabled | ImGuiHoveredFlags_ForTooltip))
            SetTooltipV("%s", text_end < text.Length ? text.Substring(0, text_end) : text);
    }

    // C++: void ImGui::LabelText(const char* label, const char* fmt, ...)   [imgui_widgets.cpp 387]
    public static void LabelText(string label, string fmt = null, params object[] args)
    {
        LabelTextV(label, fmt ?? label, args);
    }

    // Add a label+text combo aligned to other label+value widgets
    // C++: void ImGui::LabelTextV(const char* label, const char* fmt, va_list args)   [imgui_widgets.cpp 396]
    public static void LabelTextV(string label, string fmt, params object[] args)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        float w = CalcItemWidth();

        // C++: ImFormatStringToTempBufferV(&value_text_begin, &value_text_end, fmt, args);
        string value_text = ImFmt.Sprintf(fmt, args);
        int value_text_end = value_text.Length;
        ImVec2 value_size = CalcTextSize(value_text, value_text_end, false);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        ImVec2 pos = window.DC.CursorPos;
        ImRect value_bb = new ImRect(pos, pos + new ImVec2(w, value_size.y + style.FramePadding.y * 2));
        ImRect total_bb = new ImRect(pos, pos + new ImVec2(w + (label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f), ImMath.ImMax(value_size.y, label_size.y) + style.FramePadding.y * 2));
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(total_bb, 0))
            return;

        // Render
        RenderTextClipped(value_bb.Min + style.FramePadding, value_bb.Max, value_text, value_text_end, value_size, new ImVec2(0.0f, 0.0f));
        if (label_size.x > 0.0f)
            RenderText(new ImVec2(value_bb.Max.x + style.ItemInnerSpacing.x, value_bb.Min.y + style.FramePadding.y), label, label_end, false);
    }

    public static void LabelText<T1>(string label, string fmt, T1 a1) => LabelTextFmt(label, fmt, a1, default(T1), default(T1), default(T1));
    public static void LabelText<T1, T2>(string label, string fmt, T1 a1, T2 a2) => LabelTextFmt(label, fmt, a1, a2, default(T2), default(T2));
    public static void LabelText<T1, T2, T3>(string label, string fmt, T1 a1, T2 a2, T3 a3) => LabelTextFmt(label, fmt, a1, a2, a3, default(T3));
    public static void LabelText<T1, T2, T3, T4>(string label, string fmt, T1 a1, T2 a2, T3 a3, T4 a4) => LabelTextFmt(label, fmt, a1, a2, a3, a4);

    static void LabelTextFmt<T0, T1, T2, T3>(string label, string fmt, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        float w = CalcItemWidth();

        // C++: ImFormatStringToTempBufferV(&value_text_begin, &value_text_end, fmt, args);
        Span<char> buf = stackalloc char[512];
        int n = ImFmt.FormatInto(buf, fmt, a0, a1, a2, a3);
        ReadOnlySpan<char> value_text;
        if (n > buf.Length)
        {
            char[] scratch = ImFmt.RentScratch(n);
            n = ImFmt.FormatInto(scratch, fmt, a0, a1, a2, a3);
            value_text = scratch.AsSpan(0, n);
        }
        else
        {
            value_text = buf.Slice(0, n);
        }
        ImVec2 value_size = CalcTextSize(value_text, false);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        ImVec2 pos = window.DC.CursorPos;
        ImRect value_bb = new ImRect(pos, pos + new ImVec2(w, value_size.y + style.FramePadding.y * 2));
        ImRect total_bb = new ImRect(pos, pos + new ImVec2(w + (label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f), ImMath.ImMax(value_size.y, label_size.y) + style.FramePadding.y * 2));
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(total_bb, 0))
            return;

        // Render
        RenderTextClipped(value_bb.Min + style.FramePadding, value_bb.Max, value_text, value_size, new ImVec2(0.0f, 0.0f));
        if (label_size.x > 0.0f)
            RenderText(new ImVec2(value_bb.Max.x + style.ItemInnerSpacing.x, value_bb.Min.y + style.FramePadding.y), label, label_end, false);
    }

    // C++: void ImGui::BulletText(const char* fmt, ...)   [imgui_widgets.cpp 425]
    public static void BulletText(string fmt, params object[] args)
    {
        BulletTextV(fmt, args);
    }

    // Text with a little bullet aligned to the typical tree node.
    // C++: void ImGui::BulletTextV(const char* fmt, va_list args)   [imgui_widgets.cpp 434]
    public static void BulletTextV(string fmt, params object[] args)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;

        // C++: ImFormatStringToTempBufferV(&text_begin, &text_end, fmt, args);
        string text = ImFmt.Sprintf(fmt, args);
        int text_end = text.Length;
        ImVec2 label_size = CalcTextSize(text, text_end, false);
        ImVec2 total_size = new ImVec2(g.FontSize + (label_size.x > 0.0f ? (label_size.x + style.FramePadding.x * 2) : 0.0f), label_size.y);  // Empty text doesn't add padding
        ImVec2 pos = window.DC.CursorPos;
        pos.y += window.DC.CurrLineTextBaseOffset;
        ItemSize(total_size, 0.0f);
        ImRect bb = new ImRect(pos, pos + total_size);
        if (!ItemAdd(bb, 0))
            return;

        // Render
        ImU32 text_col = GetColorU32(ImGuiCol_Text);
        RenderBullet(window.DrawList, bb.Min + new ImVec2(style.FramePadding.x + g.FontSize * 0.5f, g.FontSize * 0.5f), text_col);
        RenderText(bb.Min + new ImVec2(g.FontSize + style.FramePadding.x * 2, 0.0f), text, text_end, false);
    }

    public static void BulletText<T1>(string fmt, T1 a1) => BulletTextFmt(fmt, a1, default(T1), default(T1), default(T1));
    public static void BulletText<T1, T2>(string fmt, T1 a1, T2 a2) => BulletTextFmt(fmt, a1, a2, default(T2), default(T2));
    public static void BulletText<T1, T2, T3>(string fmt, T1 a1, T2 a2, T3 a3) => BulletTextFmt(fmt, a1, a2, a3, default(T3));
    public static void BulletText<T1, T2, T3, T4>(string fmt, T1 a1, T2 a2, T3 a3, T4 a4) => BulletTextFmt(fmt, a1, a2, a3, a4);

    static void BulletTextFmt<T0, T1, T2, T3>(string fmt, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;

        // C++: ImFormatStringToTempBuffer(&text_begin, &text_end, fmt, args);
        Span<char> buf = stackalloc char[512];
        int n = ImFmt.FormatInto(buf, fmt, a0, a1, a2, a3);
        ReadOnlySpan<char> text;
        if (n > buf.Length)
        {
            char[] scratch = ImFmt.RentScratch(n);
            n = ImFmt.FormatInto(scratch, fmt, a0, a1, a2, a3);
            text = scratch.AsSpan(0, n);
        }
        else
        {
            text = buf.Slice(0, n);
        }
        ImVec2 label_size = CalcTextSize(text, false);
        ImVec2 total_size = new ImVec2(g.FontSize + (label_size.x > 0.0f ? (label_size.x + style.FramePadding.x * 2) : 0.0f), label_size.y);  // Empty text doesn't add padding
        ImVec2 pos = window.DC.CursorPos;
        pos.y += window.DC.CurrLineTextBaseOffset;
        ItemSize(total_size, 0.0f);
        ImRect bb = new ImRect(pos, pos + total_size);
        if (!ItemAdd(bb, 0))
            return;

        // Render
        ImU32 text_col = GetColorU32(ImGuiCol_Text);
        RenderBullet(window.DrawList, bb.Min + new ImVec2(style.FramePadding.x + g.FontSize * 0.5f, g.FontSize * 0.5f), text_col);
        RenderText(bb.Min + new ImVec2(g.FontSize + style.FramePadding.x * 2, 0.0f), text, false);
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Main
    //-------------------------------------------------------------------------

    // The ButtonBehavior() function is key to many interactions and used by many/most widgets.
    // Because we handle so many cases (keyboard/gamepad navigation, drag and drop) and many specific behavior (via ImGuiButtonFlags_),
    // this code is a little complex.
    // By far the most common path is interacting with the Mouse using the default ImGuiButtonFlags_PressedOnClickRelease button behavior.
    // See the series of events below and the corresponding state reported by dear imgui:
    //------------------------------------------------------------------------------------------------------------------------------------------------
    // with PressedOnClickRelease:             return-value  IsItemHovered()  IsItemActive()  IsItemActivated()  IsItemDeactivated()  IsItemClicked()
    //   Frame N+0 (mouse is outside bb)        -             -                -               -                  -                    -
    //   Frame N+1 (mouse moves inside bb)      -             true             -               -                  -                    -
    //   Frame N+2 (mouse button is down)       -             true             true            true               -                    true
    //   Frame N+3 (mouse button is down)       -             true             true            -                  -                    -
    //   Frame N+4 (mouse moves outside bb)     -             -                true            -                  -                    -
    //   Frame N+5 (mouse moves inside bb)      -             true             true            -                  -                    -
    //   Frame N+6 (mouse button is released)   true          true             -               -                  true                 -
    //   Frame N+7 (mouse button is released)   -             true             -               -                  -                    -
    //   Frame N+8 (mouse moves outside bb)     -             -                -               -                  -                    -
    //------------------------------------------------------------------------------------------------------------------------------------------------
    // with PressedOnClick:                    return-value  IsItemHovered()  IsItemActive()  IsItemActivated()  IsItemDeactivated()  IsItemClicked()
    //   Frame N+2 (mouse button is down)       true          true             true            true               -                    true
    //   Frame N+3 (mouse button is down)       -             true             true            -                  -                    -
    //   Frame N+6 (mouse button is released)   -             true             -               -                  true                 -
    //   Frame N+7 (mouse button is released)   -             true             -               -                  -                    -
    //------------------------------------------------------------------------------------------------------------------------------------------------
    // with PressedOnRelease:                  return-value  IsItemHovered()  IsItemActive()  IsItemActivated()  IsItemDeactivated()  IsItemClicked()
    //   Frame N+2 (mouse button is down)       -             true             -               -                  -                    true
    //   Frame N+3 (mouse button is down)       -             true             -               -                  -                    -
    //   Frame N+6 (mouse button is released)   true          true             -               -                  -                    -
    //   Frame N+7 (mouse button is released)   -             true             -               -                  -                    -
    //------------------------------------------------------------------------------------------------------------------------------------------------
    // with PressedOnDoubleClick:              return-value  IsItemHovered()  IsItemActive()  IsItemActivated()  IsItemDeactivated()  IsItemClicked()
    //   Frame N+0 (mouse button is down)       -             true             -               -                  -                    true
    //   Frame N+1 (mouse button is down)       -             true             -               -                  -                    -
    //   Frame N+2 (mouse button is released)   -             true             -               -                  -                    -
    //   Frame N+3 (mouse button is released)   -             true             -               -                  -                    -
    //   Frame N+4 (mouse button is down)       true          true             true            true               -                    true
    //   Frame N+5 (mouse button is down)       -             true             true            -                  -                    -
    //   Frame N+6 (mouse button is released)   -             true             -               -                  true                 -
    //   Frame N+7 (mouse button is released)   -             true             -               -                  -                    -
    //------------------------------------------------------------------------------------------------------------------------------------------------
    // Note that some combinations are supported,
    // - PressedOnDragDropHold can generally be associated with any flag.
    // - PressedOnDoubleClick can be associated by PressedOnClickRelease/PressedOnRelease, in which case the second release event won't be reported.
    //------------------------------------------------------------------------------------------------------------------------------------------------
    // The behavior of the return-value changes when ImGuiItemFlags_ButtonRepeat is set:
    //                                         Repeat+                  Repeat+           Repeat+             Repeat+
    //                                         PressedOnClickRelease    PressedOnClick    PressedOnRelease    PressedOnDoubleClick
    //-------------------------------------------------------------------------------------------------------------------------------------------------
    //   Frame N+0 (mouse button is down)       -                        true              -                   true
    //   ...                                    -                        -                 -                   -
    //   Frame N + RepeatDelay                  true                     true              -                   true
    //   ...                                    -                        -                 -                   -
    //   Frame N + RepeatDelay + RepeatRate*N   true                     true              -                   true
    //-------------------------------------------------------------------------------------------------------------------------------------------------

    // - FIXME: For refactor we could output flags, incl mouse hovered vs nav keyboard vs nav triggered etc.
    //   And better standardize how widgets use 'GetColor32((held && hovered) ? ... : hovered ? ...)' vs 'GetColor32(held ? ... : hovered ? ...);'
    //   For mouse feedback we typically prefer the 'held && hovered' test, but for nav feedback not always. Outputting hovered=true on Activation may be misleading.
    // - Since v1.91.2 (Sept 2024) we included io.ConfigDebugHighlightIdConflicts feature.
    //   One idiom which was previously valid which will now emit a warning is when using multiple overlaid ButtonBehavior()
    //   with same ID and different MouseButton (see #8030). You can fix it by:
    //       (1) switching to use a single ButtonBehavior() with multiple _MouseButton flags.
    //    or (2) surrounding those calls with PushItemFlag(ImGuiItemFlags_AllowDuplicateId, true); ... PopItemFlag()
    // C++: bool ImGui::ButtonBehavior(const ImRect& bb, ImGuiID id, bool* out_hovered, bool* out_held, ImGuiButtonFlags flags)   [imgui_widgets.cpp 547]
    public static bool ButtonBehavior(ImRect bb, ImGuiID id, out bool out_hovered, out bool out_held, ImGuiButtonFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();

        // Default behavior inherited from item flags
        // Note that _both_ ButtonFlags and ItemFlags are valid sources, so copy one into the item_flags and only check that.
        ImGuiItemFlags item_flags = (g.LastItemData.ID == id ? g.LastItemData.ItemFlags : g.CurrentItemFlags);
        if ((flags & ImGuiButtonFlags_AllowOverlap) != 0)
            item_flags |= ImGuiItemFlags_AllowOverlap;
        if ((item_flags & ImGuiItemFlags_NoFocus) != 0)
            flags |= ImGuiButtonFlags_NoFocus | ImGuiButtonFlags_NoNavFocus;

        // Default only reacts to left mouse button
        if ((flags & ImGuiButtonFlags_MouseButtonMask_) == 0)
            flags |= ImGuiButtonFlags_MouseButtonLeft;

        // Default behavior requires click + release inside bounding box
        if ((flags & ImGuiButtonFlags_PressedOnMask_) == 0)
            flags |= ((item_flags & ImGuiItemFlags_ButtonRepeat) != 0) ? ImGuiButtonFlags_PressedOnClick : ImGuiButtonFlags_PressedOnDefault_;

        ImGuiWindow backup_hovered_window = g.HoveredWindow;
        bool flatten_hovered_children = (flags & ImGuiButtonFlags_FlattenChildren) != 0 && g.HoveredWindow != null && g.HoveredWindow.RootWindowDockTree == window.RootWindowDockTree;
        if (flatten_hovered_children)
            g.HoveredWindow = window;

        // (IMGUI_ENABLE_TEST_ENGINE: alternate registration spot, for when caller didn't use ItemAdd(), omitted)

        bool pressed = false;
        bool hovered = ItemHoverable(bb, id, item_flags);

        // Special mode for Drag and Drop used by openables (tree nodes, tabs etc.)
        // where holding the button pressed for a long time while drag a payload item triggers the button.
        if (g.DragDropActive)
        {
            if ((flags & ImGuiButtonFlags_PressedOnDragDropHold) != 0 && (g.DragDropSourceFlags & ImGuiDragDropFlags_SourceNoHoldToOpenOthers) == 0 && IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem))
            {
                hovered = true;
                SetHoveredID(id);
                if (g.HoveredIdTimer - g.IO.DeltaTime <= DRAGDROP_HOLD_TO_OPEN_TIMER && g.HoveredIdTimer >= DRAGDROP_HOLD_TO_OPEN_TIMER)
                {
                    pressed = true;
                    g.DragDropHoldJustPressedId = id;
                    FocusWindow(window);
                }
            }
            if (g.DragDropAcceptIdPrev == id && (g.DragDropAcceptFlagsPrev & ImGuiDragDropFlags_AcceptDrawAsHovered) != 0)
                hovered = true;
        }

        if (flatten_hovered_children)
            g.HoveredWindow = backup_hovered_window;

        // Mouse handling
        ImGuiID test_owner_id = (flags & ImGuiButtonFlags_NoTestKeyOwner) != 0 ? ImGuiKeyOwner_Any : id;
        if (hovered)
        {
            Debug.Assert(id != 0); // Lazily check inside rare path.

            // Poll mouse buttons
            // - 'mouse_button_clicked' is generally carried into ActiveIdMouseButton when setting ActiveId.
            // - Technically we only need some values in one code path, but since this is gated by hovered test this is fine.
            int mouse_button_clicked = -1;
            int mouse_button_released = -1;
            for (int button = 0; button < 3; button++)
                if ((flags & (ImGuiButtonFlags_MouseButtonLeft << button)) != 0) // Handle ImGuiButtonFlags_MouseButtonRight and ImGuiButtonFlags_MouseButtonMiddle here.
                {
                    if (IsMouseClicked(button, ImGuiInputFlags_None, test_owner_id) && mouse_button_clicked == -1) { mouse_button_clicked = button; }
                    if (IsMouseReleased(button, test_owner_id) && mouse_button_released == -1) { mouse_button_released = button; }
                }

            // Process initial action
            bool mods_ok = ((flags & ImGuiButtonFlags_NoKeyModsAllowed) == 0) || (!g.IO.KeyCtrl && !g.IO.KeyShift && !g.IO.KeyAlt);
            if (mods_ok)
            {
                if (mouse_button_clicked != -1 && g.ActiveId != id)
                {
                    if ((flags & ImGuiButtonFlags_NoSetKeyOwner) == 0)
                        SetKeyOwner(MouseButtonToKey(mouse_button_clicked), id);
                    if ((flags & (ImGuiButtonFlags_PressedOnClickRelease | ImGuiButtonFlags_PressedOnClickReleaseAnywhere)) != 0)
                    {
                        SetActiveID(id, window);
                        g.ActiveIdMouseButton = (ImS8)mouse_button_clicked;
                        if ((flags & ImGuiButtonFlags_NoNavFocus) == 0)
                        {
                            SetFocusID(id, window);
                            FocusWindow(window);
                        }
                        else if ((flags & ImGuiButtonFlags_NoFocus) == 0)
                        {
                            FocusWindow(window, ImGuiFocusRequestFlags_RestoreFocusedChild); // Still need to focus and bring to front, but try to avoid losing NavId when navigating a child
                        }
                    }
                    if ((flags & ImGuiButtonFlags_PressedOnClick) != 0 || ((flags & ImGuiButtonFlags_PressedOnDoubleClick) != 0 && g.IO.MouseClickedCount[mouse_button_clicked] == 2))
                    {
                        pressed = true;
                        if ((flags & ImGuiButtonFlags_NoHoldingActiveId) != 0)
                            ClearActiveID();
                        else
                            SetActiveID(id, window); // Hold on ID
                        g.ActiveIdMouseButton = (ImS8)mouse_button_clicked;
                        if ((flags & ImGuiButtonFlags_NoNavFocus) == 0)
                        {
                            SetFocusID(id, window);
                            FocusWindow(window);
                        }
                        else if ((flags & ImGuiButtonFlags_NoFocus) == 0)
                        {
                            FocusWindow(window, ImGuiFocusRequestFlags_RestoreFocusedChild); // Still need to focus and bring to front, but try to avoid losing NavId when navigating a child
                        }
                    }
                    if ((flags & ImGuiButtonFlags_PressedOnRelease) != 0)
                    {
                        // FIXME: Traditionally ImGuiButtonFlags_PressedOnRelease never took ActiveId. Adding it in 2026-03-20 since ImGuiButtonFlags_NoHoldingActiveId can always be added.
                        // We don't yet perform an explicit ClearActiveID() to reduce scope of change, but this possibility could be investigated.
                        if ((flags & ImGuiButtonFlags_NoHoldingActiveId) == 0)
                            SetActiveID(id, window); // Hold on ID
                        g.ActiveIdMouseButton = (ImS8)mouse_button_clicked;
                    }
                }
                if ((flags & ImGuiButtonFlags_PressedOnRelease) != 0)
                {
                    if (mouse_button_released != -1)
                    {
                        bool has_repeated_at_least_once = (item_flags & ImGuiItemFlags_ButtonRepeat) != 0 && g.IO.MouseDownDurationPrev[mouse_button_released] >= g.IO.KeyRepeatDelay; // Repeat mode trumps on release behavior
                        if (!has_repeated_at_least_once)
                            pressed = true;
                        if ((flags & ImGuiButtonFlags_NoNavFocus) == 0)
                            SetFocusID(id, window); // FIXME: Lack of FocusWindow() call here is inconsistent with other paths. Research why.
                        ClearActiveID();
                    }
                }

                // 'Repeat' mode acts when held regardless of _PressedOn flags (see table above).
                // Relies on repeat logic of IsMouseClicked() but we may as well do it ourselves if we end up exposing finer RepeatDelay/RepeatRate settings.
                if (g.ActiveId == id && (item_flags & ImGuiItemFlags_ButtonRepeat) != 0)
                    if (g.IO.MouseDownDuration[g.ActiveIdMouseButton] > 0.0f && IsMouseClicked(g.ActiveIdMouseButton, ImGuiInputFlags_Repeat, test_owner_id))
                        pressed = true;
            }

            if (pressed && g.IO.ConfigNavCursorVisibleAuto)
                g.NavCursorVisible = false;
        }

        // Keyboard/Gamepad navigation handling
        // We report navigated and navigation-activated items as hovered but we don't set g.HoveredId to not interfere with mouse.
        if ((item_flags & ImGuiItemFlags_Disabled) == 0)
        {
            if (g.NavId == id && g.NavCursorVisible && g.NavHighlightItemUnderNav)
                if ((flags & ImGuiButtonFlags_NoHoveredOnFocus) == 0)
                    hovered = true;
            if (g.NavActivateDownId == id)
            {
                bool nav_activated_by_code = (g.NavActivateId == id);
                bool nav_activated_by_inputs = (g.NavActivatePressedId == id);
                if (!nav_activated_by_inputs && (item_flags & ImGuiItemFlags_ButtonRepeat) != 0)
                {
                    // Avoid pressing multiple keys from triggering excessive amount of repeat events
                    ImGuiKeyData key1 = GetKeyData(ImGuiKey_Space);
                    ImGuiKeyData key2 = GetKeyData(ImGuiKey_Enter);
                    ImGuiKeyData key3 = GetKeyData(ImGuiKey_NavGamepadActivate);
                    float t1 = ImMath.ImMax(ImMath.ImMax(key1.DownDuration, key2.DownDuration), key3.DownDuration);
                    nav_activated_by_inputs = CalcTypematicRepeatAmount(t1 - g.IO.DeltaTime, t1, g.IO.KeyRepeatDelay, g.IO.KeyRepeatRate) > 0;
                }
                if (nav_activated_by_code || nav_activated_by_inputs)
                {
                    // Set active id so it can be queried by user via IsItemActive(), equivalent of holding the mouse button.
                    pressed = true;
                    SetActiveID(id, window);
                    g.ActiveIdSource = g.NavInputSource;
                    if ((flags & ImGuiButtonFlags_NoNavFocus) == 0 && (g.NavActivateFlags & ImGuiActivateFlags_FromShortcut) == 0)
                        SetFocusID(id, window);
                    if ((g.NavActivateFlags & ImGuiActivateFlags_FromShortcut) != 0)
                        g.ActiveIdFromShortcut = true;
                }
            }
        }

        // Process while held
        bool held = false;
        if (g.ActiveId == id)
        {
            if (g.ActiveIdSource == ImGuiInputSource_Mouse)
            {
                if (g.ActiveIdIsJustActivated)
                    g.ActiveIdClickOffset = g.IO.MousePos - bb.Min;

                int mouse_button = g.ActiveIdMouseButton;
                if (mouse_button == -1)
                {
                    // Fallback for the rare situation were g.ActiveId was set programmatically or from another widget (e.g. #6304).
                    ClearActiveID();
                }
                else if (IsMouseDown(mouse_button, test_owner_id))
                {
                    held = true;
                }
                else
                {
                    bool release_in = hovered && (flags & ImGuiButtonFlags_PressedOnClickRelease) != 0;
                    bool release_anywhere = (flags & ImGuiButtonFlags_PressedOnClickReleaseAnywhere) != 0;
                    if ((release_in || release_anywhere) && !g.DragDropActive)
                    {
                        // Report as pressed when releasing the mouse (this is the most common path)
                        bool is_double_click_release = (flags & ImGuiButtonFlags_PressedOnDoubleClick) != 0 && g.IO.MouseReleased[mouse_button] && g.IO.MouseClickedLastCount[mouse_button] == 2;
                        bool is_repeating_already = (item_flags & ImGuiItemFlags_ButtonRepeat) != 0 && g.IO.MouseDownDurationPrev[mouse_button] >= g.IO.KeyRepeatDelay; // Repeat mode trumps <on release>
                        bool is_button_avail_or_owned = TestKeyOwner(MouseButtonToKey(mouse_button), test_owner_id);
                        if (!is_double_click_release && !is_repeating_already && is_button_avail_or_owned)
                            pressed = true;
                    }
                    ClearActiveID();
                }
                if ((flags & ImGuiButtonFlags_NoNavFocus) == 0 && g.IO.ConfigNavCursorVisibleAuto)
                    g.NavCursorVisible = false;
            }
            else if (g.ActiveIdSource == ImGuiInputSource_Keyboard || g.ActiveIdSource == ImGuiInputSource_Gamepad)
            {
                // When activated using Nav, we hold on the ActiveID until activation button is released
                if (g.NavActivateDownId == id)
                    held = true; // hovered == true not true as we are already likely hovered on direct activation.
                else
                    ClearActiveID();
            }
            if (pressed)
                g.ActiveIdHasBeenPressedBefore = true;
        }

        // Activation highlight (this may be a remote activation)
        if (g.NavHighlightActivatedId == id && (item_flags & ImGuiItemFlags_Disabled) == 0)
            hovered = true;

        out_hovered = hovered;
        out_held = held;

        return pressed;
    }

    // C++: bool ImGui::ButtonEx(const char* label, const ImVec2& size_arg, ImGuiButtonFlags flags)   [imgui_widgets.cpp 788]
    public static bool ButtonEx(string label, ImVec2? size_arg = null, ImGuiButtonFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImVec2 size_arg_v = size_arg ?? new ImVec2(0.0f, 0.0f);

        ImVec2 pos = window.DC.CursorPos;
        if ((flags & ImGuiButtonFlags_AlignTextBaseLine) != 0 && style.FramePadding.y < window.DC.CurrLineTextBaseOffset) // Try to vertically align buttons that are smaller/have no padding so that text baseline matches (bit hacky, since it shouldn't be a flag)
            pos.y += window.DC.CurrLineTextBaseOffset - style.FramePadding.y;
        ImVec2 size = CalcItemSize(size_arg_v, label_size.x + style.FramePadding.x * 2.0f, label_size.y + style.FramePadding.y * 2.0f);

        ImRect bb = new ImRect(pos, pos + size);
        ItemSize(size, style.FramePadding.y);
        if (!ItemAdd(bb, id))
            return false;

        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held, flags);

        // Render
        ImU32 col = GetColorU32((held && hovered) ? ImGuiCol_ButtonActive : hovered ? ImGuiCol_ButtonHovered : ImGuiCol_Button);
        RenderNavCursor(bb, id);
        RenderFrame(bb.Min, bb.Max, col, true, style.FrameRounding);

        if (g.LogEnabled)
            LogSetNextTextDecoration("[", "]");
        RenderTextClipped(bb.Min + style.FramePadding, bb.Max - style.FramePadding, label, label_end, label_size, style.ButtonTextAlign, bb);

        // Automatically close popups
        //if (pressed && !(flags & ImGuiButtonFlags_DontClosePopups) && (window->Flags & ImGuiWindowFlags_Popup))
        //    CloseCurrentPopup();

        return pressed;
    }

    // C++: bool ImGui::Button(const char* label, const ImVec2& size_arg)   [imgui_widgets.cpp 830]
    public static bool Button(string label, ImVec2? size = null)
    {
        return ButtonEx(label, size ?? new ImVec2(0.0f, 0.0f), ImGuiButtonFlags_None);
    }

    // Small buttons fits within text without additional vertical spacing.
    // C++: bool ImGui::SmallButton(const char* label)   [imgui_widgets.cpp 836]
    public static bool SmallButton(string label)
    {
        ImGuiContext g = GImGui;
        float backup_padding_y = g.Style.FramePadding.y;
        g.Style.FramePadding.y = 0.0f;
        bool pressed = ButtonEx(label, new ImVec2(0, 0), ImGuiButtonFlags_AlignTextBaseLine);
        g.Style.FramePadding.y = backup_padding_y;
        return pressed;
    }

    // Tip: use ImGui::PushID()/PopID() to push indices or pointers in the ID stack.
    // Then you can keep 'str_id' empty or the same for all your buttons (instead of creating a string based on a non-string id)
    // C++: bool ImGui::InvisibleButton(const char* str_id, const ImVec2& size_arg, ImGuiButtonFlags flags)   [imgui_widgets.cpp 848]
    public static bool InvisibleButton(string str_id, ImVec2 size_arg, ImGuiButtonFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        // Ensure zero-size fits to contents
        ImVec2 size = CalcItemSize(new ImVec2(size_arg.x != 0.0f ? size_arg.x : -float.Epsilon /* -FLT_MIN */, size_arg.y != 0.0f ? size_arg.y : -float.Epsilon /* -FLT_MIN */), 0.0f, 0.0f);

        ImGuiID id = window.GetID(str_id);
        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + size);
        ItemSize(size);
        if (!ItemAdd(bb, id, null, (flags & ImGuiButtonFlags_EnableNav) != 0 ? ImGuiItemFlags_None : ImGuiItemFlags_NoNav))
            return false;

        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held, flags);
        RenderNavCursor(bb, id);

        return pressed;
    }

    // C++: bool ImGui::ArrowButtonEx(const char* str_id, ImGuiDir dir, ImVec2 size, ImGuiButtonFlags flags)   [imgui_widgets.cpp 872]
    public static bool ArrowButtonEx(string str_id, ImGuiDir dir, ImVec2 size, ImGuiButtonFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiID id = window.GetID(str_id);
        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + size);
        float default_size = GetFrameHeight();
        ItemSize(size, (size.y >= default_size) ? g.Style.FramePadding.y : -1.0f);
        if (!ItemAdd(bb, id))
            return false;

        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held, flags);

        // Render
        ImU32 bg_col = GetColorU32((held && hovered) ? ImGuiCol_ButtonActive : hovered ? ImGuiCol_ButtonHovered : ImGuiCol_Button);
        ImU32 text_col = GetColorU32(ImGuiCol_Text);
        RenderNavCursor(bb, id);
        RenderFrame(bb.Min, bb.Max, bg_col, true, g.Style.FrameRounding);
        RenderArrow(window.DrawList, bb.Min + new ImVec2(ImMath.ImMax(0.0f, (size.x - g.FontSize) * 0.5f), ImMath.ImMax(0.0f, (size.y - g.FontSize) * 0.5f)), text_col, dir);

        return pressed;
    }

    // C++: bool ImGui::ArrowButton(const char* str_id, ImGuiDir dir)   [imgui_widgets.cpp 900]
    public static bool ArrowButton(string str_id, ImGuiDir dir)
    {
        float sz = GetFrameHeight();
        return ArrowButtonEx(str_id, dir, new ImVec2(sz, sz), ImGuiButtonFlags_None);
    }

    // Button to close a window
    // C++: bool ImGui::CloseButton(ImGuiID id, const ImVec2& pos)   [imgui_widgets.cpp 907]
    public static bool CloseButton(ImGuiID id, ImVec2 pos)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        // Tweak 1: Shrink hit-testing area if button covers an abnormally large proportion of the visible region. That's in order to facilitate moving the window away. (#3825)
        // This may better be applied as a general hit-rect reduction mechanism for all widgets to ensure the area to move window is always accessible?
        ImRect bb = new ImRect(pos, pos + new ImVec2(g.FontSize, g.FontSize));
        ImRect bb_interact = bb;
        float area_to_visible_ratio = window.OuterRectClipped.GetArea() / bb.GetArea();
        if (area_to_visible_ratio < 1.5f)
            bb_interact.Expand(ImMath.ImTrunc(bb_interact.GetSize() * -0.25f));

        // Tweak 2: We intentionally allow interaction when clipped so that a mechanical Alt,Right,Activate sequence can always close a window.
        // (this isn't the common behavior of buttons, but it doesn't affect the user because navigation tends to keep items visible in scrolling layer).
        bool is_clipped = !ItemAdd(bb_interact, id);

        bool hovered, held;
        bool pressed = ButtonBehavior(bb_interact, id, out hovered, out held);
        if (is_clipped)
            return pressed;

        // Render
        ImU32 bg_col = GetColorU32(held ? ImGuiCol_ButtonActive : ImGuiCol_ButtonHovered);
        if (hovered)
            window.DrawList.AddRectFilled(bb.Min, bb.Max, bg_col);
        RenderNavCursor(bb, id, ImGuiNavRenderCursorFlags_Compact);
        ImU32 cross_col = GetColorU32(ImGuiCol_Text);
        ImVec2 cross_center = bb.GetCenter() - new ImVec2(0.5f, 0.5f);
        float cross_extent = g.FontSize * 0.5f * 0.7071f - 1.0f;
        float cross_thickness = 1.0f * (float)(int)g.Style._MainScale; // FIXME-DPI
        window.DrawList.AddLine(cross_center + new ImVec2(+cross_extent, +cross_extent), cross_center + new ImVec2(-cross_extent, -cross_extent), cross_col, cross_thickness);
        window.DrawList.AddLine(cross_center + new ImVec2(+cross_extent, -cross_extent), cross_center + new ImVec2(-cross_extent, +cross_extent), cross_col, cross_thickness);

        return pressed;
    }

    // C++: bool ImGui::CollapseButton(ImGuiID id, const ImVec2& pos)   [imgui_widgets.cpp 944]
    // The Collapse button also functions as a Dock Menu button.
    // bool ImGui::CollapseButton(ImGuiID id, const ImVec2& pos, ImGuiDockNode* dock_node)
    public static bool CollapseButton(ImGuiID id, ImVec2 pos, ImGuiDockNode dock_node)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        ImRect bb = new ImRect(pos, pos + new ImVec2(g.FontSize, g.FontSize));
        bool is_clipped = !ItemAdd(bb, id);
        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held, ImGuiButtonFlags_None);
        if (is_clipped)
            return pressed;

        // Render
        //bool is_dock_menu = (window.DockNodeAsHost != null && !window.Collapsed);
        ImU32 bg_col = GetColorU32((held && hovered) ? ImGuiCol_ButtonActive : hovered ? ImGuiCol_ButtonHovered : ImGuiCol_Button);
        ImU32 text_col = GetColorU32(ImGuiCol_Text);
        if (hovered || held)
            window.DrawList.AddRectFilled(bb.Min, bb.Max, bg_col);
        RenderNavCursor(bb, id, ImGuiNavRenderCursorFlags_Compact);

        if (dock_node != null)
            RenderArrowDockMenu(window.DrawList, bb.Min, g.FontSize, text_col);
        else
            RenderArrow(window.DrawList, bb.Min, text_col, window.Collapsed ? ImGuiDir_Right : ImGuiDir_Down, 1.0f);

        // Switch to moving the window after mouse is moved beyond the initial drag threshold
        if (IsItemActive() && IsMouseDragging(0))
            StartMouseMovingWindowOrNode(window, dock_node, true); // Undock from window/collapse menu button

        return pressed;
    }

    // C++: ImGuiID ImGui::GetWindowScrollbarID(ImGuiWindow* window, ImGuiAxis axis)   [imgui_widgets.cpp 971]
    public static ImGuiID GetWindowScrollbarID(ImGuiWindow window, ImGuiAxis axis)
    {
        return window.GetID(axis == ImGuiAxis_X ? "#SCROLLX" : "#SCROLLY");
    }

    // Return scrollbar rectangle, must only be called for corresponding axis if window->ScrollbarX/Y is set.
    // C++: ImRect ImGui::GetWindowScrollbarRect(ImGuiWindow* window, ImGuiAxis axis)   [imgui_widgets.cpp 977]
    public static ImRect GetWindowScrollbarRect(ImGuiWindow window, ImGuiAxis axis)
    {
        ImGuiContext g = GImGui;
        ImRect outer_rect = window.Rect();
        ImRect inner_rect = window.InnerRect;
        float scrollbar_size = window.ScrollbarSizes[axis ^ 1]; // (ScrollbarSizes.x = width of Y scrollbar; ScrollbarSizes.y = height of X scrollbar)
        Debug.Assert(scrollbar_size >= 0.0f);
        float border_size = ImMath.ImRound(window.WindowBorderSize * 0.5f);
        float border_top = (window.Flags & ImGuiWindowFlags_MenuBar) != 0 ? ImMath.ImRound(g.Style.FrameBorderSize * 0.5f) : (window.Flags & ImGuiWindowFlags_NoTitleBar) != 0 ? border_size : 0.0f;
        if (axis == ImGuiAxis_X)
            return new ImRect(inner_rect.Min.x + border_size, ImMath.ImMax(outer_rect.Min.y + border_size, outer_rect.Max.y - border_size - scrollbar_size), inner_rect.Max.x - border_size, outer_rect.Max.y - border_size);
        else
            return new ImRect(ImMath.ImMax(outer_rect.Min.x, outer_rect.Max.x - border_size - scrollbar_size), inner_rect.Min.y + border_top, outer_rect.Max.x - border_size, inner_rect.Max.y - border_size);
    }

    // C++: void ImGui::ExtendHitBoxWhenNearViewportEdge(ImGuiWindow* window, ImRect* bb, float threshold, ImGuiAxis axis)   [imgui_widgets.cpp 992]
    public static void ExtendHitBoxWhenNearViewportEdge(ImGuiWindow window, ref ImRect bb, float threshold, ImGuiAxis axis)
    {
        ImRect window_rect = window.RootWindow.Rect();
        ImRect viewport_rect = window.Viewport.GetMainRect();
        if (window_rect.Min[axis] == viewport_rect.Min[axis] && bb.Min[axis] > window_rect.Min[axis] && bb.Min[axis] - threshold <= window_rect.Min[axis])
            bb.Min[axis] = window_rect.Min[axis];
        if (window_rect.Max[axis] == viewport_rect.Max[axis] && bb.Max[axis] < window_rect.Max[axis] && bb.Max[axis] + threshold >= window_rect.Max[axis])
            bb.Max[axis] = window_rect.Max[axis];
    }

    // C++: void ImGui::Scrollbar(ImGuiAxis axis)   [imgui_widgets.cpp 1002]
    public static void Scrollbar(ImGuiAxis axis)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = GetWindowScrollbarID(window, axis);

        // Calculate scrollbar bounding box
        ImRect bb = GetWindowScrollbarRect(window, axis);
        ImRect host_rect = (window.DockIsActive ? window.DockNode.HostWindow : window).Rect();
        ImDrawFlags rounding_corners = CalcRoundingFlagsForRectInRect(bb, host_rect, g.Style.WindowBorderSize);
        float size_visible = window.InnerRect.Max[axis] - window.InnerRect.Min[axis];
        float size_contents = window.ContentSize[axis] + window.WindowPadding[axis] * 2.0f;
        ImS64 scroll = (ImS64)window.Scroll[axis];
        ScrollbarEx(bb, id, axis, ref scroll, (ImS64)size_visible, (ImS64)size_contents, rounding_corners);
        window.Scroll[axis] = (float)scroll;
    }

    // Vertical/Horizontal scrollbar
    // The entire piece of code below is rather confusing because:
    // - We handle absolute seeking (when first clicking outside the grab) and relative manipulation (afterward or when clicking inside the grab)
    // - We store values as normalized ratio and in a form that allows the window content to change while we are holding on a scrollbar
    // - We handle both horizontal and vertical scrollbars, which makes the terminology not ideal.
    // Still, the code should probably be made simpler..
    // C++: bool ImGui::ScrollbarEx(const ImRect& bb_frame, ImGuiID id, ImGuiAxis axis, ImS64* p_scroll_v, ImS64 size_visible_v, ImS64 size_contents_v, ImDrawFlags draw_rounding_flags)   [imgui_widgets.cpp 1024]
    public static bool ScrollbarEx(ImRect bb_frame, ImGuiID id, ImGuiAxis axis, ref ImS64 p_scroll_v, ImS64 size_visible_v, ImS64 size_contents_v, ImDrawFlags draw_rounding_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;

        float bb_frame_width = bb_frame.GetWidth();
        float bb_frame_height = bb_frame.GetHeight();
        if (bb_frame_width <= 0.0f || bb_frame_height <= 0.0f)
            return false;

        // When we are too small, start hiding and disabling the grab (this reduce visual noise on very small window and facilitate using the window resize grab)
        float alpha = 1.0f;
        if ((axis == ImGuiAxis_Y) && bb_frame_height < bb_frame_width)
            alpha = ImMath.ImSaturate(bb_frame_height / ImMath.ImMax(bb_frame_width * 2.0f, 1.0f));
        if (alpha <= 0.0f)
            return false;

        ImGuiStyle style = g.Style;
        bool allow_interaction = (alpha >= 1.0f);

        ImRect bb = bb_frame;
        float padding = ImMath.ImTrunc(ImMath.ImMin(style.ScrollbarPadding, ImMath.ImMin(bb_frame_width, bb_frame_height) * 0.5f));
        bb.Expand(-padding);

        // V denote the main, longer axis of the scrollbar (= height for a vertical scrollbar)
        float scrollbar_size_v = (axis == ImGuiAxis_X) ? bb.GetWidth() : bb.GetHeight();
        if (scrollbar_size_v < 1.0f)
            return false;

        // Calculate the height of our grabbable box. It generally represent the amount visible (vs the total scrollable amount)
        // But we maintain a minimum size in pixel to allow for the user to still aim inside.
        Debug.Assert(System.Math.Max(size_contents_v, size_visible_v) > 0); // Adding this assert to check if the ImMax(XXX,1.0f) is still needed. PLEASE CONTACT ME if this triggers.
        ImS64 win_size_v = System.Math.Max(System.Math.Max(size_contents_v, size_visible_v), (ImS64)1);
        float grab_h_minsize = ImMath.ImMin(bb.GetSize()[axis], style.GrabMinSize);
        float grab_h_pixels = (float)(int)ImMath.ImClamp(scrollbar_size_v * ((float)size_visible_v / (float)win_size_v), grab_h_minsize, scrollbar_size_v);
        float grab_h_norm = grab_h_pixels / scrollbar_size_v;

        // As a special thing, we allow scrollbar near the edge of a screen/viewport to be reachable with mouse at the extreme edge (#9276)
        ImRect bb_hit = bb_frame;
        ExtendHitBoxWhenNearViewportEdge(window, ref bb_hit, g.Style.WindowBorderSize, (ImGuiAxis)(axis ^ 1));

        // Handle input right away. None of the code of Begin() is relying on scrolling position before calling Scrollbar().
        bool held = false;
        bool hovered = false;
        ItemAdd(bb_frame, id, null, ImGuiItemFlags_NoNav);
        ButtonBehavior(bb_hit, id, out hovered, out held, ImGuiButtonFlags_NoNavFocus);

        ImS64 scroll_max = System.Math.Max((ImS64)1, size_contents_v - size_visible_v);
        float scroll_ratio = ImMath.ImSaturate((float)p_scroll_v / (float)scroll_max);
        float grab_v_norm = scroll_ratio * (scrollbar_size_v - grab_h_pixels) / scrollbar_size_v; // Grab position in normalized space
        if (held && allow_interaction && grab_h_norm < 1.0f)
        {
            float scrollbar_pos_v = bb.Min[axis];
            float mouse_pos_v = g.IO.MousePos[axis];

            // Click position in scrollbar normalized space (0.0f->1.0f)
            float clicked_v_norm = ImMath.ImSaturate((mouse_pos_v - scrollbar_pos_v) / scrollbar_size_v);

            int held_dir = (clicked_v_norm < grab_v_norm) ? -1 : (clicked_v_norm > grab_v_norm + grab_h_norm) ? +1 : 0;
            if (g.ActiveIdIsJustActivated)
            {
                // On initial click when held_dir == 0 (clicked over grab): calculate the distance between mouse and the center of the grab
                bool scroll_to_clicked_location = (g.IO.ConfigScrollbarScrollByPage == false || g.IO.KeyShift || held_dir == 0);
                g.ScrollbarSeekMode = scroll_to_clicked_location ? (short)0 : (short)held_dir;
                g.ScrollbarClickDeltaToGrabCenter = (held_dir == 0 && !g.IO.KeyShift) ? clicked_v_norm - grab_v_norm - grab_h_norm * 0.5f : 0.0f;
            }

            // Apply scroll (p_scroll_v will generally point on one member of window->Scroll)
            // It is ok to modify Scroll here because we are being called in Begin() after the calculation of ContentSize and before setting up our starting position
            if (g.ScrollbarSeekMode == 0)
            {
                // Absolute seeking
                float scroll_v_norm = ImMath.ImSaturate((clicked_v_norm - g.ScrollbarClickDeltaToGrabCenter - grab_h_norm * 0.5f) / (1.0f - grab_h_norm));
                p_scroll_v = (ImS64)(scroll_v_norm * (float)scroll_max);
            }
            else
            {
                // Page by page
                if (IsMouseClicked(ImGuiMouseButton_Left, ImGuiInputFlags_Repeat, ImGuiKeyOwner_Any) && held_dir == g.ScrollbarSeekMode)
                {
                    float page_dir = (g.ScrollbarSeekMode > 0.0f) ? +1.0f : -1.0f;
                    p_scroll_v = System.Math.Clamp(p_scroll_v + (ImS64)(page_dir * (float)size_visible_v), (ImS64)0, scroll_max); // (ImClamp)
                }
            }

            // Update values for rendering
            scroll_ratio = ImMath.ImSaturate((float)p_scroll_v / (float)scroll_max);
            grab_v_norm = scroll_ratio * (scrollbar_size_v - grab_h_pixels) / scrollbar_size_v;

            // Update distance to grab now that we have seek'ed and saturated
            //if (seek_absolute)
            //    g.ScrollbarClickDeltaToGrabCenter = clicked_v_norm - grab_v_norm - grab_h_norm * 0.5f;
        }

        // Render
        ImU32 bg_col = GetColorU32(ImGuiCol_ScrollbarBg);
        ImU32 grab_col = GetColorU32(held ? ImGuiCol_ScrollbarGrabActive : hovered ? ImGuiCol_ScrollbarGrabHovered : ImGuiCol_ScrollbarGrab, alpha);
        window.DrawList.AddRectFilled(bb_frame.Min, bb_frame.Max, bg_col, window.WindowRounding, draw_rounding_flags);
        ImRect grab_rect;
        if (axis == ImGuiAxis_X)
            grab_rect = new ImRect(ImMath.ImLerp(bb.Min.x, bb.Max.x, grab_v_norm), bb.Min.y, ImMath.ImLerp(bb.Min.x, bb.Max.x, grab_v_norm) + grab_h_pixels, bb.Max.y);
        else
            grab_rect = new ImRect(bb.Min.x, ImMath.ImLerp(bb.Min.y, bb.Max.y, grab_v_norm), bb.Max.x, ImMath.ImLerp(bb.Min.y, bb.Max.y, grab_v_norm) + grab_h_pixels);
        window.DrawList.AddRectFilled(grab_rect.Min, grab_rect.Max, grab_col, style.ScrollbarRounding);

        return held;
    }

    // - Read about ImTextureID/ImTextureRef here: https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples
    // - 'uv0' and 'uv1' are texture coordinates. Read about them from the same link above.
    // C++: void ImGui::ImageWithBg(ImTextureRef tex_ref, const ImVec2& image_size, const ImVec2& uv0, const ImVec2& uv1, const ImVec4& bg_col, const ImVec4& tint_col)   [imgui_widgets.cpp 1136]
    public static void ImageWithBg(ImTextureRef tex_ref, ImVec2 image_size, ImVec2? uv0 = null, ImVec2? uv1 = null, ImVec4? bg_col = null, ImVec4? tint_col = null)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImVec2 uv0_v = uv0 ?? new ImVec2(0.0f, 0.0f);
        ImVec2 uv1_v = uv1 ?? new ImVec2(1.0f, 1.0f);
        ImVec4 bg_col_v = bg_col ?? new ImVec4(0.0f, 0.0f, 0.0f, 0.0f);
        ImVec4 tint_col_v = tint_col ?? new ImVec4(1.0f, 1.0f, 1.0f, 1.0f);

        ImVec2 padding = new ImVec2(g.Style.ImageBorderSize, g.Style.ImageBorderSize);
        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + image_size + padding * 2.0f);
        ItemSize(bb.GetSize());
        if (!ItemAdd(bb, 0))
            return;

        // Render
        float rounding = g.Style.ImageRounding;
        if (bg_col_v.w > 0.0f)
            window.DrawList.AddRectFilled(bb.Min + padding, bb.Max - padding, GetColorU32(bg_col_v), rounding);
        if (rounding > 0.0f)
            window.DrawList.AddImageRounded(tex_ref, bb.Min + padding, bb.Max - padding, uv0_v, uv1_v, GetColorU32(tint_col_v), rounding);
        else
            window.DrawList.AddImage(tex_ref, bb.Min + padding, bb.Max - padding, uv0_v, uv1_v, GetColorU32(tint_col_v));
        if (g.Style.ImageBorderSize > 0.0f)
            window.DrawList.AddRect(bb.Min, bb.Max, GetColorU32(ImGuiCol_Border), rounding, g.Style.ImageBorderSize);
    }

    // C++: void ImGui::Image(ImTextureRef tex_ref, const ImVec2& image_size, const ImVec2& uv0, const ImVec2& uv1)   [imgui_widgets.cpp 1161]
    public static void Image(ImTextureRef tex_ref, ImVec2 image_size, ImVec2? uv0 = null, ImVec2? uv1 = null)
    {
        ImageWithBg(tex_ref, image_size, uv0, uv1);
    }

    // 1.91.9 (February 2025) removed 'tint_col' and 'border_col' parameters, made border size not depend on color value. (#8131, #8238)
    // (kept for parity: #ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS is not defined in the default C++ build)
    // C++: void ImGui::Image(ImTextureRef tex_ref, const ImVec2& image_size, const ImVec2& uv0, const ImVec2& uv1, const ImVec4& tint_col, const ImVec4& border_col)   [imgui_widgets.cpp 1168]
    public static void Image(ImTextureRef tex_ref, ImVec2 image_size, ImVec2 uv0, ImVec2 uv1, ImVec4 tint_col, ImVec4 border_col)
    {
        ImGuiContext g = GImGui;
        PushStyleVar(ImGuiStyleVar_ImageBorderSize, (border_col.w > 0.0f) ? ImMath.ImMax(1.0f, g.Style.ImageBorderSize) : 0.0f); // Preserve legacy behavior where border is always visible when border_col's Alpha is >0.0f
        PushStyleColor(ImGuiCol_Border, border_col);
        ImageWithBg(tex_ref, image_size, uv0, uv1, new ImVec4(0, 0, 0, 0), tint_col);
        PopStyleColor();
        PopStyleVar();
    }

    // C++: bool ImGui::ImageButtonEx(ImGuiID id, ImTextureRef tex_ref, const ImVec2& image_size, const ImVec2& uv0, const ImVec2& uv1, const ImVec4& bg_col, const ImVec4& tint_col, ImGuiButtonFlags flags)   [imgui_widgets.cpp 1179]
    public static bool ImageButtonEx(ImGuiID id, ImTextureRef tex_ref, ImVec2 image_size, ImVec2? uv0 = null, ImVec2? uv1 = null, ImVec4? bg_col = null, ImVec4? tint_col = null, ImGuiButtonFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImVec2 uv0_v = uv0 ?? new ImVec2(0.0f, 0.0f);
        ImVec2 uv1_v = uv1 ?? new ImVec2(1.0f, 1.0f);
        ImVec4 bg_col_v = bg_col ?? new ImVec4(0.0f, 0.0f, 0.0f, 0.0f);
        ImVec4 tint_col_v = tint_col ?? new ImVec4(1.0f, 1.0f, 1.0f, 1.0f);

        ImVec2 padding = g.Style.FramePadding;
        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + image_size + padding * 2.0f);
        ItemSize(bb.GetSize());
        if (!ItemAdd(bb, id))
            return false;

        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held, flags);

        // Render
        ImU32 col = GetColorU32((held && hovered) ? ImGuiCol_ButtonActive : hovered ? ImGuiCol_ButtonHovered : ImGuiCol_Button);
        RenderNavCursor(bb, id);
        RenderFrame(bb.Min, bb.Max, col, true, g.Style.FrameRounding);
        if (bg_col_v.w > 0.0f)
            window.DrawList.AddRectFilled(bb.Min + padding, bb.Max - padding, GetColorU32(bg_col_v));
        float image_rounding = ImMath.ImMax(g.Style.FrameRounding - ImMath.ImMax(padding.x, padding.y), g.Style.ImageRounding);
        if (image_rounding > 0.0f)
            window.DrawList.AddImageRounded(tex_ref, bb.Min + padding, bb.Max - padding, uv0_v, uv1_v, GetColorU32(tint_col_v), image_rounding);
        else
            window.DrawList.AddImage(tex_ref, bb.Min + padding, bb.Max - padding, uv0_v, uv1_v, GetColorU32(tint_col_v));

        return pressed;
    }

    // - ImageButton() adds style.FramePadding*2.0f to provided size. This is in order to facilitate fitting an image in a button.
    // - ImageButton() draws a background based on regular Button() color + optionally an inner background if specified. (#8165) // FIXME: Maybe that's not the best design?
    // C++: bool ImGui::ImageButton(const char* str_id, ImTextureRef tex_ref, const ImVec2& image_size, const ImVec2& uv0, const ImVec2& uv1, const ImVec4& bg_col, const ImVec4& tint_col)   [imgui_widgets.cpp 1212]
    public static bool ImageButton(string str_id, ImTextureRef tex_ref, ImVec2 image_size, ImVec2? uv0 = null, ImVec2? uv1 = null, ImVec4? bg_col = null, ImVec4? tint_col = null)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;

        return ImageButtonEx(window.GetID(str_id), tex_ref, image_size, uv0, uv1, bg_col, tint_col);
    }

    // C++: bool ImGui::Checkbox(const char* label, bool* v)   [imgui_widgets.cpp 1244]
    public static bool Checkbox(string label, ref bool v)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        float square_sz = GetFrameHeight();
        ImVec2 pos = window.DC.CursorPos;
        ImRect total_bb = new ImRect(pos, pos + new ImVec2(square_sz + (label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f), label_size.y + style.FramePadding.y * 2.0f));
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        bool is_visible = ItemAdd(total_bb, id);
        bool is_multi_select = (g.LastItemData.ItemFlags & ImGuiItemFlags_IsMultiSelect) != 0;
        if (!is_visible)
            if (!is_multi_select || !g.BoxSelectState.UnclipMode || !g.BoxSelectState.UnclipRect.Overlaps(total_bb)) // Extra layer of "no logic clip" for box-select support
            {
                // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
                return false;
            }

        // Range-Selection/Multi-selection support (header)
        bool @checked = v;
        if (is_multi_select)
            MultiSelectItemHeader(id, ref @checked);

        bool hovered, held;
        bool pressed = ButtonBehavior(total_bb, id, out hovered, out held);

        // Range-Selection/Multi-selection support (footer)
        if (is_multi_select)
            MultiSelectItemFooter(id, ref @checked, ref pressed, ImGuiMultiSelectFlags_CheckboxMode_);
        else if (pressed)
            @checked = !@checked;

        if (v != @checked)
        {
            v = @checked;
            pressed = true; // return value
            MarkItemEdited(id);
        }

        ImRect check_bb = new ImRect(pos, pos + new ImVec2(square_sz, square_sz));
        bool mixed_value = (g.LastItemData.ItemFlags & ImGuiItemFlags_MixedValue) != 0;
        if (is_visible)
        {
            RenderNavCursor(total_bb, id);
            ImU32 bg_col = GetColorU32((held && hovered) ? ImGuiCol_FrameBgActive : hovered ? ImGuiCol_FrameBgHovered : (mixed_value || @checked) ? ImGuiCol_CheckboxSelectedBg : ImGuiCol_FrameBg);
            ImU32 check_col = GetColorU32(ImGuiCol_CheckMark);
            RenderFrame(check_bb.Min, check_bb.Max, bg_col, true, style.FrameRounding);
            if (mixed_value)
            {
                // Undocumented tristate/mixed/indeterminate checkbox (#2644)
                // This may seem awkwardly designed because the aim is to make ImGuiItemFlags_MixedValue supported by all widgets (not just checkbox)
                ImVec2 pad = new ImVec2(ImMath.ImMax(1.0f, ImMath.ImTrunc(square_sz / 3.6f)), ImMath.ImMax(1.0f, ImMath.ImTrunc(square_sz / 3.6f)));
                window.DrawList.AddRectFilled(check_bb.Min + pad, check_bb.Max - pad, check_col, style.FrameRounding);
            }
            else if (v)
            {
                float pad = ImMath.ImMax(1.0f, ImMath.ImTrunc(square_sz / 6.0f));
                RenderCheckMark(window.DrawList, check_bb.Min + new ImVec2(pad, pad), check_col, square_sz - pad * 2.0f);
            }
        }
        ImVec2 label_pos = new ImVec2(check_bb.Max.x + style.ItemInnerSpacing.x, check_bb.Min.y + style.FramePadding.y);
        if (g.LogEnabled)
            LogRenderedText(label_pos, mixed_value ? "[~]" : v ? "[x]" : "[ ]");
        if (is_visible && label_size.x > 0.0f)
            RenderText(label_pos, label, label_end, false);

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        return pressed;
    }

    // C++ template<typename T> bool ImGui::CheckboxFlagsT(const char* label, T* flags, T flags_value)   [imgui_widgets.cpp 1322]
    private static bool CheckboxFlagsT(string label, ref int flags, int flags_value)
    {
        bool all_on = (flags & flags_value) == flags_value;
        bool any_on = (flags & flags_value) != 0;
        bool pressed;
        if (!all_on && any_on)
        {
            ImGuiContext g = GImGui;
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_MixedValue;
            pressed = Checkbox(label, ref all_on);
        }
        else
        {
            pressed = Checkbox(label, ref all_on);

        }
        if (pressed)
        {
            if (all_on)
                flags |= flags_value;
            else
                flags &= ~flags_value;
        }
        return pressed;
    }

    private static bool CheckboxFlagsT(string label, ref uint flags, uint flags_value)
    {
        bool all_on = (flags & flags_value) == flags_value;
        bool any_on = (flags & flags_value) != 0;
        bool pressed;
        if (!all_on && any_on)
        {
            ImGuiContext g = GImGui;
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_MixedValue;
            pressed = Checkbox(label, ref all_on);
        }
        else
        {
            pressed = Checkbox(label, ref all_on);

        }
        if (pressed)
        {
            if (all_on)
                flags |= flags_value;
            else
                flags &= ~flags_value;
        }
        return pressed;
    }

    private static bool CheckboxFlagsT(string label, ref ImS64 flags, ImS64 flags_value)
    {
        bool all_on = (flags & flags_value) == flags_value;
        bool any_on = (flags & flags_value) != 0;
        bool pressed;
        if (!all_on && any_on)
        {
            ImGuiContext g = GImGui;
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_MixedValue;
            pressed = Checkbox(label, ref all_on);
        }
        else
        {
            pressed = Checkbox(label, ref all_on);

        }
        if (pressed)
        {
            if (all_on)
                flags |= flags_value;
            else
                flags &= ~flags_value;
        }
        return pressed;
    }

    private static bool CheckboxFlagsT(string label, ref ImU64 flags, ImU64 flags_value)
    {
        bool all_on = (flags & flags_value) == flags_value;
        bool any_on = (flags & flags_value) != 0;
        bool pressed;
        if (!all_on && any_on)
        {
            ImGuiContext g = GImGui;
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_MixedValue;
            pressed = Checkbox(label, ref all_on);
        }
        else
        {
            pressed = Checkbox(label, ref all_on);

        }
        if (pressed)
        {
            if (all_on)
                flags |= flags_value;
            else
                flags &= ~flags_value;
        }
        return pressed;
    }

    // C++: bool ImGui::CheckboxFlags(const char* label, int* flags, int flags_value)   [imgui_widgets.cpp 1348]
    public static bool CheckboxFlags(string label, ref int flags, int flags_value)
    {
        return CheckboxFlagsT(label, ref flags, flags_value);
    }

    // C++: bool ImGui::CheckboxFlags(const char* label, unsigned int* flags, unsigned int flags_value)   [imgui_widgets.cpp 1353]
    public static bool CheckboxFlags(string label, ref uint flags, uint flags_value)
    {
        return CheckboxFlagsT(label, ref flags, flags_value);
    }

    // C++: bool ImGui::CheckboxFlags(const char* label, ImS64* flags, ImS64 flags_value)   [imgui_widgets.cpp 1358]
    public static bool CheckboxFlags(string label, ref ImS64 flags, ImS64 flags_value)
    {
        return CheckboxFlagsT(label, ref flags, flags_value);
    }

    // C++: bool ImGui::CheckboxFlags(const char* label, ImU64* flags, ImU64 flags_value)   [imgui_widgets.cpp 1363]
    public static bool CheckboxFlags(string label, ref ImU64 flags, ImU64 flags_value)
    {
        return CheckboxFlagsT(label, ref flags, flags_value);
    }

    // C++: bool ImGui::RadioButton(const char* label, bool active)   [imgui_widgets.cpp 1368]
    public static bool RadioButton(string label, bool active)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        float square_sz = GetFrameHeight();
        ImVec2 pos = window.DC.CursorPos;
        ImRect check_bb = new ImRect(pos, pos + new ImVec2(square_sz, square_sz));
        ImRect total_bb = new ImRect(pos, pos + new ImVec2(square_sz + (label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f), label_size.y + style.FramePadding.y * 2.0f));
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(total_bb, id))
            return false;

        ImVec2 center = check_bb.GetCenter();
        center.x = ImMath.ImRound(center.x);
        center.y = ImMath.ImRound(center.y);
        float radius = (square_sz - 1.0f) * 0.5f;

        bool hovered, held;
        bool pressed = ButtonBehavior(total_bb, id, out hovered, out held);
        if (pressed)
            MarkItemEdited(id);

        RenderNavCursor(total_bb, id);
        int num_segment = window.DrawList._CalcCircleAutoSegmentCount(radius);
        window.DrawList.AddCircleFilled(center, radius, GetColorU32((held && hovered) ? ImGuiCol_FrameBgActive : hovered ? ImGuiCol_FrameBgHovered : ImGuiCol_FrameBg), num_segment);
        if (active)
        {
            float pad = ImMath.ImMax(1.0f, ImMath.ImTrunc(square_sz / 6.0f));
            window.DrawList.AddCircleFilled(center, radius - pad, GetColorU32(ImGuiCol_CheckMark));
        }

        if (style.FrameBorderSize > 0.0f)
        {
            window.DrawList.AddCircle(center + new ImVec2(1, 1), radius, GetColorU32(ImGuiCol_BorderShadow), num_segment, style.FrameBorderSize);
            window.DrawList.AddCircle(center, radius, GetColorU32(ImGuiCol_Border), num_segment, style.FrameBorderSize);
        }

        ImVec2 label_pos = new ImVec2(check_bb.Max.x + style.ItemInnerSpacing.x, check_bb.Min.y + style.FramePadding.y);
        if (g.LogEnabled)
            LogRenderedText(label_pos, active ? "(x)" : "( )");
        if (label_size.x > 0.0f)
            RenderText(label_pos, label, label_end, false);

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        return pressed;
    }

    // FIXME: This would work nicely if it was a public template, e.g. 'template<T> RadioButton(const char* label, T* v, T v_button)', but I'm not sure how we would expose it..
    // C++: bool ImGui::RadioButton(const char* label, int* v, int v_button)   [imgui_widgets.cpp 1424]
    public static bool RadioButton(string label, ref int v, int v_button)
    {
        bool pressed = RadioButton(label, v == v_button);
        if (pressed)
            v = v_button;
        return pressed;
    }

    // size_arg (for each axis) < 0.0f: align to end, 0.0f: auto, > 0.0f: specified size
    // C++: void ImGui::ProgressBar(float fraction, const ImVec2& size_arg, const char* overlay)   [imgui_widgets.cpp 1433]
    public static void ProgressBar(float fraction, ImVec2? size_arg = null, string overlay = null)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImVec2 size_arg_v = size_arg ?? new ImVec2(-float.Epsilon, 0.0f); // C++ default: ImVec2(-FLT_MIN, 0)

        ImVec2 pos = window.DC.CursorPos;
        ImVec2 size = CalcItemSize(size_arg_v, CalcItemWidth(), g.FontSize + style.FramePadding.y * 2.0f);
        ImRect bb = new ImRect(pos, pos + size);
        ItemSize(size, style.FramePadding.y);
        if (!ItemAdd(bb, 0))
            return;

        // Fraction < 0.0f will display an indeterminate progress bar animation
        // The value must be animated along with time, so e.g. passing '-1.0f * ImGui::GetTime()' as fraction works.
        bool is_indeterminate = (fraction < 0.0f);
        if (!is_indeterminate)
            fraction = ImMath.ImSaturate(fraction);

        // Out of courtesy we accept a NaN fraction without crashing
        float fill_n0 = 0.0f;
        float fill_n1 = (fraction == fraction) ? fraction : 0.0f;

        if (is_indeterminate)
        {
            const float fill_width_n = 0.2f;
            fill_n0 = ImMath.ImFmod(-fraction, 1.0f) * (1.0f + fill_width_n) - fill_width_n;
            fill_n1 = ImMath.ImSaturate(fill_n0 + fill_width_n);
            fill_n0 = ImMath.ImSaturate(fill_n0);
        }

        // Render
        RenderFrame(bb.Min, bb.Max, GetColorU32(ImGuiCol_FrameBg), true, style.FrameRounding);
        bb.Expand(new ImVec2(-style.FrameBorderSize, -style.FrameBorderSize));
        float fill_x0 = ImMath.ImLerp(bb.Min.x, bb.Max.x, fill_n0);
        float fill_x1 = ImMath.ImLerp(bb.Min.x, bb.Max.x, fill_n1);
        if (fill_x0 < fill_x1)
            RenderRectFilledInRangeH(window.DrawList, bb, GetColorU32(ImGuiCol_PlotHistogram), fill_x0, fill_x1, style.FrameRounding);

        // Default displaying the fraction as percentage string, but user can override it
        // Don't display text for indeterminate bars by default
        if (!is_indeterminate || overlay != null)
        {
            if (overlay == null)
            {
                overlay = ImFmt.Sprintf("%.0f%%", fraction * 100 + 0.01f);
            }

            ImVec2 overlay_size = CalcTextSize(overlay, -1);
            if (overlay_size.x > 0.0f)
            {
                float text_x = is_indeterminate ? (bb.Min.x + bb.Max.x - overlay_size.x) * 0.5f : fill_x1 + style.ItemSpacing.x;
                RenderTextClipped(new ImVec2(ImMath.ImClamp(text_x, bb.Min.x, bb.Max.x - overlay_size.x - style.ItemInnerSpacing.x), bb.Min.y), bb.Max, overlay, -1, overlay_size, new ImVec2(0.0f, 0.5f), bb);
            }
        }
    }

    // C++: void ImGui::Bullet()   [imgui_widgets.cpp 1495]
    public static void Bullet()
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        float line_height = ImMath.ImMax(ImMath.ImMin(window.DC.CurrLineSize.y, g.FontSize + style.FramePadding.y * 2), g.FontSize);
        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + new ImVec2(g.FontSize, line_height));
        ItemSize(bb.GetSize());
        if (!ItemAdd(bb, 0))
        {
            SameLine(0, style.FramePadding.x * 2);
            return;
        }

        // Render and stay on same line
        ImU32 text_col = GetColorU32(ImGuiCol_Text);
        RenderBullet(window.DrawList, bb.Min + new ImVec2(style.FramePadding.x + g.FontSize * 0.5f, line_height * 0.5f), text_col);
        SameLine(0, style.FramePadding.x * 2.0f);
    }

    // This is provided as a convenience for being an often requested feature.
    // FIXME-STYLE: we delayed adding as there is a larger plan to revamp the styling system.
    // Because of this we currently don't provide many styling options for this widget
    // (e.g. hovered/active colors are automatically inferred from a single color).
    // C++: bool ImGui::TextLink(const char* label)   [imgui_widgets.cpp 1522]
    public static bool TextLink(string label)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiID id = window.GetID(label);
        int label_end = FindRenderedTextEnd(label);

        ImVec2 pos = new ImVec2(window.DC.CursorPos.x, window.DC.CursorPos.y + window.DC.CurrLineTextBaseOffset);
        ImVec2 size = CalcTextSize(label, label_end, false);
        ImRect bb = new ImRect(pos, pos + size);
        ItemSize(size, 0.0f);
        if (!ItemAdd(bb, id))
            return false;

        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held);
        RenderNavCursor(bb, id);

        if (hovered)
            SetMouseCursor(ImGuiMouseCursor_Hand);

        ImVec4 text_colf = g.Style.Colors[ImGuiCol_TextLink];
        ImVec4 line_colf = text_colf;
        {
            // FIXME-STYLE: Read comments above. This widget is NOT written in the same style as some earlier widgets,
            // as we are currently experimenting/planning a different styling system.
            float h, s, v;
            ColorConvertRGBtoHSV(text_colf.x, text_colf.y, text_colf.z, out h, out s, out v);
            if (held || hovered)
            {
                v = ImMath.ImSaturate(v + (held ? 0.4f : 0.3f));
                h = ImMath.ImFmod(h + 0.02f, 1.0f);
            }
            ColorConvertHSVtoRGB(h, s, v, out text_colf.x, out text_colf.y, out text_colf.z);
            v = ImMath.ImSaturate(v - 0.20f);
            ColorConvertHSVtoRGB(h, s, v, out line_colf.x, out line_colf.y, out line_colf.z);
        }

        float line_y = bb.Max.y + ImMath.ImFloor(g.FontBaked.Descent * g.FontBakedScale * 0.20f);
        window.DrawList.AddLineH(bb.Min.x, bb.Max.x, line_y, GetColorU32(line_colf), 1.0f * (float)(int)g.Style._MainScale); // FIXME-TEXT: Underline mode // FIXME-DPI

        PushStyleColor(ImGuiCol_Text, GetColorU32(text_colf));
        RenderText(bb.Min, label, label_end, false);
        PopStyleColor();

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        return pressed;
    }

    // C++: bool ImGui::TextLinkOpenURL(const char* label, const char* url)   [imgui_widgets.cpp 1574]
    public static bool TextLinkOpenURL(string label, string url = null)
    {
        ImGuiContext g = GImGui;
        if (url == null)
            url = label;
        bool pressed = TextLink(label);
        if (pressed && g.PlatformIO.Platform_OpenInShellFn != null)
            g.PlatformIO.Platform_OpenInShellFn(g, url);
        SetItemTooltip(LocalizeGetMsg(ImGuiLocKey_OpenLink_s), url); // It is more reassuring for user to _always_ display URL when it is the same as the label.
        if (BeginPopupContextItem())
        {
            if (MenuItem(LocalizeGetMsg(ImGuiLocKey_CopyLink)))
                SetClipboardText(url);
            EndPopup();
        }
        return pressed;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Low-level Layout helpers
    //-------------------------------------------------------------------------

    // C++: void ImGui::Spacing()   [imgui_widgets.cpp 1605]
    public static void Spacing()
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;
        ItemSize(new ImVec2(0, 0));
    }

    // C++: void ImGui::Dummy(const ImVec2& size)   [imgui_widgets.cpp 1613]
    public static void Dummy(ImVec2 size)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + size);
        ItemSize(size);
        ItemAdd(bb, 0);
    }

    // C++: void ImGui::NewLine()   [imgui_widgets.cpp 1624]
    public static void NewLine()
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        ImGuiLayoutType backup_layout_type = (ImGuiLayoutType)window.DC.LayoutType;
        window.DC.LayoutType = ImGuiLayoutType_Vertical;
        window.DC.IsSameLine = false;
        if (window.DC.CurrLineSize.y > 0.0f)     // In the event that we are on a line with items that is smaller that FontSize high, we will preserve its height.
            ItemSize(new ImVec2(0, 0));
        else
            ItemSize(new ImVec2(0.0f, g.FontSize));
        window.DC.LayoutType = backup_layout_type;
    }

    // C++: void ImGui::AlignTextToFramePadding()   [imgui_widgets.cpp 1641]
    public static void AlignTextToFramePadding()
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        window.DC.CurrLineSize.y = ImMath.ImMax(window.DC.CurrLineSize.y, g.FontSize + g.Style.FramePadding.y * 2);
        window.DC.CurrLineTextBaseOffset = ImMath.ImMax(window.DC.CurrLineTextBaseOffset, g.Style.FramePadding.y);
    }

    // Horizontal/vertical separating line
    // FIXME: Surprisingly, this seemingly trivial widget is a victim of many different legacy/tricky layout issues.
    // Note how thickness == 1.0f is handled specifically as not moving CursorPos by 'thickness', but other values are.
    // C++: void ImGui::SeparatorEx(ImGuiSeparatorFlags flags, float thickness)   [imgui_widgets.cpp 1655]
    public static void SeparatorEx(ImGuiSeparatorFlags flags, float thickness = 1.0f)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        ImGuiContext g = GImGui;
        Debug.Assert(ImMath.ImIsPowerOfTwo((int)(flags & (ImGuiSeparatorFlags_Horizontal | ImGuiSeparatorFlags_Vertical))));   // Check that only 1 option is selected
        Debug.Assert(thickness > 0.0f);

        if ((flags & ImGuiSeparatorFlags_Vertical) != 0)
        {
            // Vertical separator, for menu bars (use current line height).
            float y1 = window.DC.CursorPos.y;
            float y2 = window.DC.CursorPos.y + window.DC.CurrLineSize.y;
            ImRect bb = new ImRect(new ImVec2(window.DC.CursorPos.x, y1), new ImVec2(window.DC.CursorPos.x + thickness, y2));
            ItemSize(new ImVec2(thickness, 0.0f));
            if (!ItemAdd(bb, 0))
                return;

            // Draw
            window.DrawList.AddRectFilled(bb.Min, bb.Max, GetColorU32(ImGuiCol_Separator));
            if (g.LogEnabled)
                LogText(" |");
        }
        else if ((flags & ImGuiSeparatorFlags_Horizontal) != 0)
        {
            // Horizontal Separator
            float x1 = window.DC.CursorPos.x;
            float x2 = window.WorkRect.Max.x;

            // Preserve legacy behavior inside Columns()
            // Before Tables API happened, we relied on Separator() to span all columns of a Columns() set.
            // We currently don't need to provide the same feature for tables because tables naturally have border features.
            ImGuiOldColumns columns = (flags & ImGuiSeparatorFlags_SpanAllColumns) != 0 ? window.DC.CurrentColumns : null;
            if (columns != null)
            {
                x1 = window.Pos.x + window.DC.Indent.x; // Used to be Pos.x before 2023/10/03
                x2 = window.Pos.x + window.Size.x;
                PushColumnsBackground();
            }

            // We don't provide our width to the layout so that it doesn't get feed back into AutoFit
            // FIXME: This prevents ->CursorMaxPos based bounding box evaluation from working (e.g. TableEndCell)

            // Between 1.71 and 1.92.7, we maintained a hack where a 1.0f thin Separator() would not impact layout.
            // This was mostly chosen to allow backward compatibility with user's code assuming zero-height when calculating height for layout (e.g. bottom alignment of a status bar).
            // In order to handle scaling we need to scale separator thickness and it would not makes sense to have a disparity depending on height.
            ////float thickness_for_layout = (thickness == 1.0f) ? 0.0f : thickness; // FIXME: See 1.70/1.71 Separator() change: makes legacy 1-px separator not affect layout yet. Should change.
            ImRect bb = new ImRect(new ImVec2(x1, window.DC.CursorPos.y), new ImVec2(x2, window.DC.CursorPos.y + thickness));
            ItemSize(new ImVec2(0.0f, thickness));

            if (ItemAdd(bb, 0))
            {
                // Draw
                window.DrawList.AddRectFilled(bb.Min, bb.Max, GetColorU32(ImGuiCol_Separator));
                if (g.LogEnabled)
                    LogRenderedText(bb.Min, "--------------------------------\n");

            }
            if (columns != null)
            {
                PopColumnsBackground();
                columns.LineMinY = window.DC.CursorPos.y;
            }
        }
    }

    // C++: void ImGui::Separator()   [imgui_widgets.cpp 1723]
    public static void Separator()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        // Those flags should eventually be configurable by the user
        ImGuiSeparatorFlags flags = (window.DC.LayoutType == ImGuiLayoutType_Horizontal) ? ImGuiSeparatorFlags_Vertical : ImGuiSeparatorFlags_Horizontal;

        // Only applies to legacy Columns() api as they relied on Separator() a lot.
        if (window.DC.CurrentColumns != null)
            flags |= ImGuiSeparatorFlags_SpanAllColumns;

        SeparatorEx(flags, ImMath.ImMax(g.Style.SeparatorSize, 1.0f));
    }

    // (SeparatorTextEx()/SeparatorText() [imgui_widgets.cpp 1740/1790] are ported in ImGuiWidgets4.cs
    //  together with the TreeNode range.)

    // Using 'hover_visibility_delay' allows us to hide the highlight and mouse cursor for a short time, which can be convenient to reduce visual noise.
    // C++: bool ImGui::SplitterBehavior(const ImRect& bb, ImGuiID id, ImGuiAxis axis, float* size1, float* size2, float min_size1, float min_size2, float hover_extend, float hover_visibility_delay, ImU32 bg_col)   [imgui_widgets.cpp 1806]
    public static bool SplitterBehavior(ImRect bb, ImGuiID id, ImGuiAxis axis, ref float size1, ref float size2, float min_size1, float min_size2, float hover_extend = 0.0f, float hover_visibility_delay = 0.0f, ImU32 bg_col = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        if (!ItemAdd(bb, id, null, ImGuiItemFlags_NoNav))
            return false;

        // FIXME: AFAIK the only leftover reason for passing ImGuiButtonFlags_AllowOverlap here is
        // to allow caller of SplitterBehavior() to call SetItemAllowOverlap() after the item.
        // Nowadays we would instead want to use SetNextItemAllowOverlap() before the item.
        ImGuiButtonFlags button_flags = ImGuiButtonFlags_FlattenChildren;
        // #ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS
        button_flags |= ImGuiButtonFlags_AllowOverlap;
        // #endif

        bool hovered, held;
        ImRect bb_interact = bb;
        bb_interact.Expand(axis == ImGuiAxis_Y ? new ImVec2(0.0f, hover_extend) : new ImVec2(hover_extend, 0.0f));
        ButtonBehavior(bb_interact, id, out hovered, out held, button_flags);
        if (hovered)
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredRect; // for IsItemHovered(), because bb_interact is larger than bb

        if (held || (hovered && g.HoveredIdPreviousFrame == id && g.HoveredIdTimer >= hover_visibility_delay))
            SetMouseCursor(axis == ImGuiAxis_Y ? ImGuiMouseCursor_ResizeNS : ImGuiMouseCursor_ResizeEW);

        ImRect bb_render = bb;
        if (held)
        {
            float mouse_delta = (g.IO.MousePos - g.ActiveIdClickOffset - bb_interact.Min)[axis];

            // Minimum pane size
            float size_1_maximum_delta = ImMath.ImMax(0.0f, size1 - min_size1);
            float size_2_maximum_delta = ImMath.ImMax(0.0f, size2 - min_size2);
            if (mouse_delta < -size_1_maximum_delta)
                mouse_delta = -size_1_maximum_delta;
            if (mouse_delta > size_2_maximum_delta)
                mouse_delta = size_2_maximum_delta;

            // Apply resize
            if (mouse_delta != 0.0f)
            {
                size1 = ImMath.ImMax(size1 + mouse_delta, min_size1);
                size2 = ImMath.ImMax(size2 - mouse_delta, min_size2);
                bb_render.Translate((axis == ImGuiAxis_X) ? new ImVec2(mouse_delta, 0.0f) : new ImVec2(0.0f, mouse_delta));
                MarkItemEdited(id);
            }
        }

        // Render at new position
        if ((bg_col & ImGuiCore.IM_COL32_A_MASK) != 0)
            window.DrawList.AddRectFilled(bb_render.Min, bb_render.Max, bg_col, 0.0f);
        ImU32 col = GetColorU32(held ? ImGuiCol_SeparatorActive : (hovered && g.HoveredIdTimer >= hover_visibility_delay) ? ImGuiCol_SeparatorHovered : ImGuiCol_Separator);
        window.DrawList.AddRectFilled(bb_render.Min, bb_render.Max, col, 0.0f);

        return held;
    }

    // C++: static int IMGUI_CDECL ShrinkWidthItemComparer(const void* lhs, const void* rhs)   [imgui_widgets.cpp 1864]
    private static int ShrinkWidthItemComparer(ImGuiShrinkWidthItem a, ImGuiShrinkWidthItem b)
    {
        int d = (int)(b.Width - a.Width);
        if (d != 0)
            return d;
        return b.Index - a.Index;
    }

    // Shrink excess width from a set of item, by removing width from the larger items first.
    // Set items Width to -1.0f to disable shrinking this item.
    // C++: void ImGui::ShrinkWidths(ImGuiShrinkWidthItem* items, int count, float width_excess, float width_min)   [imgui_widgets.cpp 1875]
    public static void ShrinkWidths(ImGuiShrinkWidthItem[] items, int count, float width_excess, float width_min)
    {
        if (count == 1)
        {
            if (items[0].Width >= 0.0f)
                items[0].Width = ImMath.ImMax(items[0].Width - width_excess, width_min);
            return;
        }
        // C++: ImQsort(items, (size_t)count, sizeof(ImGuiShrinkWidthItem), ShrinkWidthItemComparer); // Sort largest first, smallest last.
        Array.Sort(items, 0, count, System.Collections.Generic.Comparer<ImGuiShrinkWidthItem>.Create(ShrinkWidthItemComparer));
        int count_same_width = 1;
        while (width_excess > 0.001f && count_same_width < count)
        {
            while (count_same_width < count && items[0].Width <= items[count_same_width].Width)
                count_same_width++;
            float max_width_to_remove_per_item = (count_same_width < count && items[count_same_width].Width >= 0.0f) ? (items[0].Width - items[count_same_width].Width) : (items[0].Width - 1.0f);
            max_width_to_remove_per_item = ImMath.ImMin(items[0].Width - width_min, max_width_to_remove_per_item);
            if (max_width_to_remove_per_item <= 0.0f)
                break;
            float base_width_to_remove_per_item = ImMath.ImMin(width_excess / count_same_width, max_width_to_remove_per_item);
            for (int item_n = 0; item_n < count_same_width; item_n++)
            {
                float width_to_remove_for_this_item = ImMath.ImMin(base_width_to_remove_per_item, items[item_n].Width - width_min);
                items[item_n].Width -= width_to_remove_for_this_item;
                width_excess -= width_to_remove_for_this_item;
            }
        }

        // Round width and redistribute remainder
        // Ensure that e.g. the right-most tab of a shrunk tab-bar always reaches exactly at the same distance from the right-most edge of the tab bar separator.
        width_excess = 0.0f;
        for (int n = 0; n < count; n++)
        {
            float width_rounded = ImMath.ImTrunc(items[n].Width);
            width_excess += items[n].Width - width_rounded;
            items[n].Width = width_rounded;
        }
        while (width_excess > 0.0f)
            for (int n = 0; n < count && width_excess > 0.0f; n++)
            {
                float width_to_add = ImMath.ImMin(items[n].InitialWidth - items[n].Width, 1.0f);
                items[n].Width += width_to_add;
                width_excess -= width_to_add;
            }
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Value helpers
    // Those is not very useful, legacy API.
    //-------------------------------------------------------------------------

    // C++: void ImGui::Value(const char* prefix, bool b)   [imgui_widgets.cpp 9157]
    public static void Value(string prefix, bool b)
    {
        Text("%s: %s", prefix, b ? "true" : "false");
    }

    // C++: void ImGui::Value(const char* prefix, int v)   [imgui_widgets.cpp 9161]
    public static void Value(string prefix, int v)
    {
        Text("%s: %d", prefix, v);
    }

    // C++: void ImGui::Value(const char* prefix, unsigned int v)   [imgui_widgets.cpp 9165]
    // (C++ passes an unsigned int to "%d"; printf reinterprets it as signed)
    public static void Value(string prefix, uint v)
    {
        Text("%s: %d", prefix, unchecked((int)v));
    }

    // C++: void ImGui::Value(const char* prefix, float v, const char* float_format)   [imgui_widgets.cpp 9169]
    public static void Value(string prefix, float v, string float_format = null)
    {
        if (float_format != null)
        {
            string fmt = ImFmt.Sprintf("%%s: %s", float_format);
            Text(fmt, prefix, v);
        }
        else
        {
            Text("%s: %.3f", prefix, v);
        }
    }
}
