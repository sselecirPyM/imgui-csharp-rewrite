// Port of imgui.cpp sections:
//   [SECTION] STYLING            (lines 3612-3882)
//   [SECTION] RENDER HELPERS     (lines 3882-4153)
//   [SECTION] ID STACK           (lines 9326-9489)
//   [SECTION] ITEM SUBMISSION    (lines 11469-11595)
//   [SECTION] LAYOUT             (lines 11595-12024)
//   [SECTION] SCROLLING          (lines 12024-12265)
//   [SECTION] TOOLTIPS           (lines 12265-12368)
// StyleColorsDark/StyleColorsLight live in ImGuiStyle.cs; WindowGetID/GetIDFromPos/GetIDFromRectangle in ImGuiUtil.cs.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    //-----------------------------------------------------------------------------
    // [SECTION] STYLING
    //-----------------------------------------------------------------------------

    // (imgui.cpp: ImGui::GetStyle() is already ported in ImGui.cs)

    public static ImU32 GetColorU32(ImGuiCol idx, float alpha_mul = 1.0f)
    {
        ImGuiStyle style = GImGui.Style;
        ImVec4 c = style.Colors[idx];
        c.w *= style.Alpha * alpha_mul;
        return ColorConvertFloat4ToU32(c);
    }

    public static ImU32 GetColorU32(ImVec4 col)
    {
        ImGuiStyle style = GImGui.Style;
        ImVec4 c = col;
        c.w *= style.Alpha;
        return ColorConvertFloat4ToU32(c);
    }

    public static ImVec4 GetStyleColorVec4(ImGuiCol idx)
    {
        ImGuiStyle style = GImGui.Style;
        return style.Colors[idx];
    }

    public static ImU32 GetColorU32(ImU32 col, float alpha_mul = 1.0f)
    {
        ImGuiStyle style = GImGui.Style;
        alpha_mul *= style.Alpha;
        if (alpha_mul >= 1.0f)
            return col;
        ImU32 a = (col & ImGuiCore.IM_COL32_A_MASK) >> ImGuiCore.IM_COL32_A_SHIFT;
        a = (ImU32)(a * alpha_mul); // We don't need to clamp 0..255 because alpha is in 0..1 range.
        return (col & ~ImGuiCore.IM_COL32_A_MASK) | (a << ImGuiCore.IM_COL32_A_SHIFT);
    }

    // FIXME: This may incur a round-trip (if the end user got their data from a float4) but eventually we aim to store the in-flight colors as ImU32
    public static void PushStyleColor(ImGuiCol idx, ImU32 col)
    {
        ImGuiContext g = GImGui;
        ImGuiColorMod backup;
        backup.Col = idx;
        backup.BackupValue = g.Style.Colors[idx];
        g.ColorStack.push_back(backup);
        if (g.DebugFlashStyleColorIdx != idx)
            g.Style.Colors[idx] = ColorConvertU32ToFloat4(col);
    }

    public static void PushStyleColor(ImGuiCol idx, ImVec4 col)
    {
        ImGuiContext g = GImGui;
        ImGuiColorMod backup;
        backup.Col = idx;
        backup.BackupValue = g.Style.Colors[idx];
        g.ColorStack.push_back(backup);
        if (g.DebugFlashStyleColorIdx != idx)
            g.Style.Colors[idx] = col;
    }

    public static void PopStyleColor(int count = 1)
    {
        ImGuiContext g = GImGui;
        if (g.ColorStack.Size < count)
        {
            ImUtil.ImAssertUserError(false, "Calling PopStyleColor() too many times!");
            count = g.ColorStack.Size;
        }
        while (count > 0)
        {
            ImGuiColorMod backup = g.ColorStack.back();
            g.Style.Colors[backup.Col] = backup.BackupValue;
            g.ColorStack.pop_back();
            count--;
        }
    }

    // Port of GStyleVarsInfo[] (imgui.cpp): all entries are ImGuiDataType_Float with Count 1 or 2.
    // C++ reads/writes the variable via offsetof()+pointer; the C# port routes through switch accessors below.
    private struct ImGuiStyleVarInfo
    {
        public int Count;
        public int DataType;                   // ImGuiDataType
    }

    private static readonly int[] GStyleVarsInfo =
    {
        1, // ImGuiStyleVar_Alpha
        1, // ImGuiStyleVar_DisabledAlpha
        2, // ImGuiStyleVar_WindowPadding
        1, // ImGuiStyleVar_WindowRounding
        1, // ImGuiStyleVar_WindowBorderSize
        2, // ImGuiStyleVar_WindowMinSize
        2, // ImGuiStyleVar_WindowTitleAlign
        1, // ImGuiStyleVar_ChildRounding
        1, // ImGuiStyleVar_ChildBorderSize
        1, // ImGuiStyleVar_PopupRounding
        1, // ImGuiStyleVar_PopupBorderSize
        2, // ImGuiStyleVar_FramePadding
        1, // ImGuiStyleVar_FrameRounding
        1, // ImGuiStyleVar_FrameBorderSize
        2, // ImGuiStyleVar_ItemSpacing
        2, // ImGuiStyleVar_ItemInnerSpacing
        1, // ImGuiStyleVar_IndentSpacing
        2, // ImGuiStyleVar_CellPadding
        1, // ImGuiStyleVar_ScrollbarSize
        1, // ImGuiStyleVar_ScrollbarRounding
        1, // ImGuiStyleVar_ScrollbarPadding
        1, // ImGuiStyleVar_GrabMinSize
        1, // ImGuiStyleVar_GrabRounding
        1, // ImGuiStyleVar_ImageRounding
        1, // ImGuiStyleVar_ImageBorderSize
        1, // ImGuiStyleVar_TabRounding
        1, // ImGuiStyleVar_TabBorderSize
        1, // ImGuiStyleVar_TabMinWidthBase
        1, // ImGuiStyleVar_TabMinWidthShrink
        1, // ImGuiStyleVar_TabBarBorderSize
        1, // ImGuiStyleVar_TabBarOverlineSize
        1, // ImGuiStyleVar_TableAngledHeadersAngle
        2, // ImGuiStyleVar_TableAngledHeadersTextAlign
        1, // ImGuiStyleVar_TreeLinesSize
        1, // ImGuiStyleVar_TreeLinesRounding
        1, // ImGuiStyleVar_MenuItemRounding
        1, // ImGuiStyleVar_SelectableRounding
        1, // ImGuiStyleVar_DragDropTargetRounding
        2, // ImGuiStyleVar_ButtonTextAlign
        2, // ImGuiStyleVar_SelectableTextAlign
        1, // ImGuiStyleVar_SeparatorSize
        1, // ImGuiStyleVar_SeparatorTextBorderSize
        2, // ImGuiStyleVar_SeparatorTextAlign
        2, // ImGuiStyleVar_SeparatorTextPadding
        1, // ImGuiStyleVar_DockingSeparatorSize
    };

    private static ImGuiStyleVarInfo GetStyleVarInfo(ImGuiStyleVar idx)
    {
        Debug.Assert(idx >= 0 && idx < ImGuiStyleVar_COUNT);
        return new ImGuiStyleVarInfo { Count = GStyleVarsInfo[idx], DataType = ImGuiDataType_Float };
    }

    // var_info->GetVarPtr(&g.Style) equivalents (single-float variables)
    private static float StyleVarGetFloat(ImGuiStyle style, int idx)
    {
        switch (idx)
        {
            case ImGuiStyleVar_Alpha: return style.Alpha;
            case ImGuiStyleVar_DisabledAlpha: return style.DisabledAlpha;
            case ImGuiStyleVar_WindowRounding: return style.WindowRounding;
            case ImGuiStyleVar_WindowBorderSize: return style.WindowBorderSize;
            case ImGuiStyleVar_ChildRounding: return style.ChildRounding;
            case ImGuiStyleVar_ChildBorderSize: return style.ChildBorderSize;
            case ImGuiStyleVar_PopupRounding: return style.PopupRounding;
            case ImGuiStyleVar_PopupBorderSize: return style.PopupBorderSize;
            case ImGuiStyleVar_FrameRounding: return style.FrameRounding;
            case ImGuiStyleVar_FrameBorderSize: return style.FrameBorderSize;
            case ImGuiStyleVar_IndentSpacing: return style.IndentSpacing;
            case ImGuiStyleVar_ScrollbarSize: return style.ScrollbarSize;
            case ImGuiStyleVar_ScrollbarRounding: return style.ScrollbarRounding;
            case ImGuiStyleVar_ScrollbarPadding: return style.ScrollbarPadding;
            case ImGuiStyleVar_GrabMinSize: return style.GrabMinSize;
            case ImGuiStyleVar_GrabRounding: return style.GrabRounding;
            case ImGuiStyleVar_ImageRounding: return style.ImageRounding;
            case ImGuiStyleVar_ImageBorderSize: return style.ImageBorderSize;
            case ImGuiStyleVar_TabRounding: return style.TabRounding;
            case ImGuiStyleVar_TabBorderSize: return style.TabBorderSize;
            case ImGuiStyleVar_TabMinWidthBase: return style.TabMinWidthBase;
            case ImGuiStyleVar_TabMinWidthShrink: return style.TabMinWidthShrink;
            case ImGuiStyleVar_TabBarBorderSize: return style.TabBarBorderSize;
            case ImGuiStyleVar_TabBarOverlineSize: return style.TabBarOverlineSize;
            case ImGuiStyleVar_TableAngledHeadersAngle: return style.TableAngledHeadersAngle;
            case ImGuiStyleVar_TreeLinesSize: return style.TreeLinesSize;
            case ImGuiStyleVar_TreeLinesRounding: return style.TreeLinesRounding;
            case ImGuiStyleVar_MenuItemRounding: return style.MenuItemRounding;
            case ImGuiStyleVar_SelectableRounding: return style.SelectableRounding;
            case ImGuiStyleVar_DragDropTargetRounding: return style.DragDropTargetRounding;
            case ImGuiStyleVar_SeparatorSize: return style.SeparatorSize;
            case ImGuiStyleVar_SeparatorTextBorderSize: return style.SeparatorTextBorderSize;
            case ImGuiStyleVar_DockingSeparatorSize: return style.DockingSeparatorSize;
            default:
                Debug.Assert(false, "Not a single-float style var");
                return 0.0f;
        }
    }

    private static void StyleVarSetFloat(ImGuiStyle style, int idx, float v)
    {
        switch (idx)
        {
            case ImGuiStyleVar_Alpha: style.Alpha = v; break;
            case ImGuiStyleVar_DisabledAlpha: style.DisabledAlpha = v; break;
            case ImGuiStyleVar_WindowRounding: style.WindowRounding = v; break;
            case ImGuiStyleVar_WindowBorderSize: style.WindowBorderSize = v; break;
            case ImGuiStyleVar_ChildRounding: style.ChildRounding = v; break;
            case ImGuiStyleVar_ChildBorderSize: style.ChildBorderSize = v; break;
            case ImGuiStyleVar_PopupRounding: style.PopupRounding = v; break;
            case ImGuiStyleVar_PopupBorderSize: style.PopupBorderSize = v; break;
            case ImGuiStyleVar_FrameRounding: style.FrameRounding = v; break;
            case ImGuiStyleVar_FrameBorderSize: style.FrameBorderSize = v; break;
            case ImGuiStyleVar_IndentSpacing: style.IndentSpacing = v; break;
            case ImGuiStyleVar_ScrollbarSize: style.ScrollbarSize = v; break;
            case ImGuiStyleVar_ScrollbarRounding: style.ScrollbarRounding = v; break;
            case ImGuiStyleVar_ScrollbarPadding: style.ScrollbarPadding = v; break;
            case ImGuiStyleVar_GrabMinSize: style.GrabMinSize = v; break;
            case ImGuiStyleVar_GrabRounding: style.GrabRounding = v; break;
            case ImGuiStyleVar_ImageRounding: style.ImageRounding = v; break;
            case ImGuiStyleVar_ImageBorderSize: style.ImageBorderSize = v; break;
            case ImGuiStyleVar_TabRounding: style.TabRounding = v; break;
            case ImGuiStyleVar_TabBorderSize: style.TabBorderSize = v; break;
            case ImGuiStyleVar_TabMinWidthBase: style.TabMinWidthBase = v; break;
            case ImGuiStyleVar_TabMinWidthShrink: style.TabMinWidthShrink = v; break;
            case ImGuiStyleVar_TabBarBorderSize: style.TabBarBorderSize = v; break;
            case ImGuiStyleVar_TabBarOverlineSize: style.TabBarOverlineSize = v; break;
            case ImGuiStyleVar_TableAngledHeadersAngle: style.TableAngledHeadersAngle = v; break;
            case ImGuiStyleVar_TreeLinesSize: style.TreeLinesSize = v; break;
            case ImGuiStyleVar_TreeLinesRounding: style.TreeLinesRounding = v; break;
            case ImGuiStyleVar_MenuItemRounding: style.MenuItemRounding = v; break;
            case ImGuiStyleVar_SelectableRounding: style.SelectableRounding = v; break;
            case ImGuiStyleVar_DragDropTargetRounding: style.DragDropTargetRounding = v; break;
            case ImGuiStyleVar_SeparatorSize: style.SeparatorSize = v; break;
            case ImGuiStyleVar_SeparatorTextBorderSize: style.SeparatorTextBorderSize = v; break;
            case ImGuiStyleVar_DockingSeparatorSize: style.DockingSeparatorSize = v; break;
            default:
                Debug.Assert(false, "Not a single-float style var");
                break;
        }
    }

    // var_info->GetVarPtr(&g.Style) equivalents (ImVec2 variables)
    private static ImVec2 StyleVarGetVec2(ImGuiStyle style, int idx)
    {
        switch (idx)
        {
            case ImGuiStyleVar_WindowPadding: return style.WindowPadding;
            case ImGuiStyleVar_WindowMinSize: return style.WindowMinSize;
            case ImGuiStyleVar_WindowTitleAlign: return style.WindowTitleAlign;
            case ImGuiStyleVar_FramePadding: return style.FramePadding;
            case ImGuiStyleVar_ItemSpacing: return style.ItemSpacing;
            case ImGuiStyleVar_ItemInnerSpacing: return style.ItemInnerSpacing;
            case ImGuiStyleVar_CellPadding: return style.CellPadding;
            case ImGuiStyleVar_TableAngledHeadersTextAlign: return style.TableAngledHeadersTextAlign;
            case ImGuiStyleVar_ButtonTextAlign: return style.ButtonTextAlign;
            case ImGuiStyleVar_SelectableTextAlign: return style.SelectableTextAlign;
            case ImGuiStyleVar_SeparatorTextAlign: return style.SeparatorTextAlign;
            case ImGuiStyleVar_SeparatorTextPadding: return style.SeparatorTextPadding;
            default:
                Debug.Assert(false, "Not a ImVec2 style var");
                return default;
        }
    }

    private static void StyleVarSetVec2(ImGuiStyle style, int idx, ImVec2 v)
    {
        switch (idx)
        {
            case ImGuiStyleVar_WindowPadding: style.WindowPadding = v; break;
            case ImGuiStyleVar_WindowMinSize: style.WindowMinSize = v; break;
            case ImGuiStyleVar_WindowTitleAlign: style.WindowTitleAlign = v; break;
            case ImGuiStyleVar_FramePadding: style.FramePadding = v; break;
            case ImGuiStyleVar_ItemSpacing: style.ItemSpacing = v; break;
            case ImGuiStyleVar_ItemInnerSpacing: style.ItemInnerSpacing = v; break;
            case ImGuiStyleVar_CellPadding: style.CellPadding = v; break;
            case ImGuiStyleVar_TableAngledHeadersTextAlign: style.TableAngledHeadersTextAlign = v; break;
            case ImGuiStyleVar_ButtonTextAlign: style.ButtonTextAlign = v; break;
            case ImGuiStyleVar_SelectableTextAlign: style.SelectableTextAlign = v; break;
            case ImGuiStyleVar_SeparatorTextAlign: style.SeparatorTextAlign = v; break;
            case ImGuiStyleVar_SeparatorTextPadding: style.SeparatorTextPadding = v; break;
            default:
                Debug.Assert(false, "Not a ImVec2 style var");
                break;
        }
    }

    public static void PushStyleVar(ImGuiStyleVar idx, float val)
    {
        ImGuiContext g = GImGui;
        ImGuiStyleVarInfo var_info = GetStyleVarInfo(idx);
        if (!(var_info.DataType == ImGuiDataType_Float && var_info.Count == 1))
        {
            ImUtil.ImAssertUserError(false, "Calling PushStyleVar() variant with wrong type!");
            return;
        }
        float pvar = StyleVarGetFloat(g.Style, idx);
        g.StyleVarStack.push_back(new ImGuiStyleMod(idx, pvar));
        StyleVarSetFloat(g.Style, idx, val);
    }

    public static void PushStyleVarX(ImGuiStyleVar idx, float val_x)
    {
        ImGuiContext g = GImGui;
        ImGuiStyleVarInfo var_info = GetStyleVarInfo(idx);
        if (!(var_info.DataType == ImGuiDataType_Float && var_info.Count == 2))
        {
            ImUtil.ImAssertUserError(false, "Calling PushStyleVar() variant with wrong type!");
            return;
        }
        ImVec2 pvar = StyleVarGetVec2(g.Style, idx);
        g.StyleVarStack.push_back(new ImGuiStyleMod(idx, pvar));
        pvar.x = val_x;
        StyleVarSetVec2(g.Style, idx, pvar);
    }

    public static void PushStyleVarY(ImGuiStyleVar idx, float val_y)
    {
        ImGuiContext g = GImGui;
        ImGuiStyleVarInfo var_info = GetStyleVarInfo(idx);
        if (!(var_info.DataType == ImGuiDataType_Float && var_info.Count == 2))
        {
            ImUtil.ImAssertUserError(false, "Calling PushStyleVar() variant with wrong type!");
            return;
        }
        ImVec2 pvar = StyleVarGetVec2(g.Style, idx);
        g.StyleVarStack.push_back(new ImGuiStyleMod(idx, pvar));
        pvar.y = val_y;
        StyleVarSetVec2(g.Style, idx, pvar);
    }

    public static void PushStyleVar(ImGuiStyleVar idx, ImVec2 val)
    {
        ImGuiContext g = GImGui;
        ImGuiStyleVarInfo var_info = GetStyleVarInfo(idx);
        if (!(var_info.DataType == ImGuiDataType_Float && var_info.Count == 2))
        {
            ImUtil.ImAssertUserError(false, "Calling PushStyleVar() variant with wrong type!");
            return;
        }
        ImVec2 pvar = StyleVarGetVec2(g.Style, idx);
        g.StyleVarStack.push_back(new ImGuiStyleMod(idx, pvar));
        StyleVarSetVec2(g.Style, idx, val);
    }

    public static void PopStyleVar(int count = 1)
    {
        ImGuiContext g = GImGui;
        if (g.StyleVarStack.Size < count)
        {
            ImUtil.ImAssertUserError(false, "Calling PopStyleVar() too many times!");
            count = g.StyleVarStack.Size;
        }
        while (count > 0)
        {
            // We avoid a generic memcpy(data, &backup.Backup.., GDataTypeSize[info->Type] * info->Count), the overhead in Debug is not worth it.
            ImGuiStyleMod backup = g.StyleVarStack.back();
            ImGuiStyleVarInfo var_info = GetStyleVarInfo(backup.VarIdx);
            if (var_info.DataType == ImGuiDataType_Float && var_info.Count == 1)
                StyleVarSetFloat(g.Style, backup.VarIdx, backup.BackupFloat0);
            else if (var_info.DataType == ImGuiDataType_Float && var_info.Count == 2)
                StyleVarSetVec2(g.Style, backup.VarIdx, new ImVec2(backup.BackupFloat0, backup.BackupFloat1));
            g.StyleVarStack.pop_back();
            count--;
        }
    }

    public static string GetStyleColorName(ImGuiCol idx)
    {
        // Create switch-case from enum with regexp: ImGuiCol_{.*}, --> case ImGuiCol_\1: return "\1";
        switch (idx)
        {
            case ImGuiCol_Text: return "Text";
            case ImGuiCol_TextDisabled: return "TextDisabled";
            case ImGuiCol_WindowBg: return "WindowBg";
            case ImGuiCol_ChildBg: return "ChildBg";
            case ImGuiCol_PopupBg: return "PopupBg";
            case ImGuiCol_Border: return "Border";
            case ImGuiCol_BorderShadow: return "BorderShadow";
            case ImGuiCol_FrameBg: return "FrameBg";
            case ImGuiCol_FrameBgHovered: return "FrameBgHovered";
            case ImGuiCol_FrameBgActive: return "FrameBgActive";
            case ImGuiCol_TitleBg: return "TitleBg";
            case ImGuiCol_TitleBgActive: return "TitleBgActive";
            case ImGuiCol_TitleBgCollapsed: return "TitleBgCollapsed";
            case ImGuiCol_MenuBarBg: return "MenuBarBg";
            case ImGuiCol_ScrollbarBg: return "ScrollbarBg";
            case ImGuiCol_ScrollbarGrab: return "ScrollbarGrab";
            case ImGuiCol_ScrollbarGrabHovered: return "ScrollbarGrabHovered";
            case ImGuiCol_ScrollbarGrabActive: return "ScrollbarGrabActive";
            case ImGuiCol_CheckMark: return "CheckMark";
            case ImGuiCol_CheckboxSelectedBg: return "CheckboxSelectedBg";
            case ImGuiCol_SliderGrab: return "SliderGrab";
            case ImGuiCol_SliderGrabActive: return "SliderGrabActive";
            case ImGuiCol_Button: return "Button";
            case ImGuiCol_ButtonHovered: return "ButtonHovered";
            case ImGuiCol_ButtonActive: return "ButtonActive";
            case ImGuiCol_Header: return "Header";
            case ImGuiCol_HeaderHovered: return "HeaderHovered";
            case ImGuiCol_HeaderActive: return "HeaderActive";
            case ImGuiCol_Separator: return "Separator";
            case ImGuiCol_SeparatorHovered: return "SeparatorHovered";
            case ImGuiCol_SeparatorActive: return "SeparatorActive";
            case ImGuiCol_ResizeGrip: return "ResizeGrip";
            case ImGuiCol_ResizeGripHovered: return "ResizeGripHovered";
            case ImGuiCol_ResizeGripActive: return "ResizeGripActive";
            case ImGuiCol_InputTextCursor: return "InputTextCursor";
            case ImGuiCol_TabHovered: return "TabHovered";
            case ImGuiCol_Tab: return "Tab";
            case ImGuiCol_TabSelected: return "TabSelected";
            case ImGuiCol_TabSelectedOverline: return "TabSelectedOverline";
            case ImGuiCol_TabDimmed: return "TabDimmed";
            case ImGuiCol_TabDimmedSelected: return "TabDimmedSelected";
            case ImGuiCol_TabDimmedSelectedOverline: return "TabDimmedSelectedOverline";
            case ImGuiCol_DockingPreview: return "DockingPreview";
            case ImGuiCol_DockingEmptyBg: return "DockingEmptyBg";
            case ImGuiCol_PlotLines: return "PlotLines";
            case ImGuiCol_PlotLinesHovered: return "PlotLinesHovered";
            case ImGuiCol_PlotHistogram: return "PlotHistogram";
            case ImGuiCol_PlotHistogramHovered: return "PlotHistogramHovered";
            case ImGuiCol_TableHeaderBg: return "TableHeaderBg";
            case ImGuiCol_TableBorderStrong: return "TableBorderStrong";
            case ImGuiCol_TableBorderLight: return "TableBorderLight";
            case ImGuiCol_TableRowBg: return "TableRowBg";
            case ImGuiCol_TableRowBgAlt: return "TableRowBgAlt";
            case ImGuiCol_TextLink: return "TextLink";
            case ImGuiCol_TextSelectedBg: return "TextSelectedBg";
            case ImGuiCol_TreeLines: return "TreeLines";
            case ImGuiCol_DragDropTarget: return "DragDropTarget";
            case ImGuiCol_DragDropTargetBg: return "DragDropTargetBg";
            case ImGuiCol_UnsavedMarker: return "UnsavedMarker";
            case ImGuiCol_NavCursor: return "NavCursor";
            case ImGuiCol_NavWindowingHighlight: return "NavWindowingHighlight";
            case ImGuiCol_NavWindowingDimBg: return "NavWindowingDimBg";
            case ImGuiCol_ModalWindowDimBg: return "ModalWindowDimBg";
        }
        Debug.Assert(false);
        return "Unknown";
    }

    // imgui_internal.h: inline float ImGui::GetScale()  (FIXME-DPI)
    public static float GetScale() { ImGuiContext g = GImGui; return g.Style._MainScale; }

    //-----------------------------------------------------------------------------
    // [SECTION] RENDER HELPERS
    // Some of those (internal) functions are currently quite a legacy mess - their signature and behavior will change,
    // we need a nicer separation between low-level functions and high-level functions relying on the ImGui context.
    // Also see imgui_draw.cpp for some more which have been reworked to not rely on ImGui:: context.
    //-----------------------------------------------------------------------------

    // text_end: -1 means "use whole string" (C++ NULL pointer). Returns display-end index.
    public static int FindRenderedTextEnd(string text, int text_end = -1)
        => FindRenderedTextEnd(text.AsSpan(), text_end);

    public static int FindRenderedTextEnd(ReadOnlySpan<char> text, int text_end = -1)
    {
        int text_display_end = 0;
        if (text_end < 0)
            text_end = text.Length;

        while (text_display_end < text_end && text[text_display_end] != '\0'
               && (text[text_display_end] != '#' || (text_display_end + 1 >= text.Length || text[text_display_end + 1] != '#')))
            text_display_end++;
        return text_display_end;
    }

    // Internal ImGui functions to render text
    // RenderText***() functions calls ImDrawList::AddText() calls ImBitmapFont::RenderText()
    public static void RenderText(ImVec2 pos, string text, int text_end = -1, bool hide_text_after_hash = true)
        => RenderText(pos, text.AsSpan(0, text_end < 0 ? text.Length : text_end), hide_text_after_hash);

    public static void RenderText(ImVec2 pos, ReadOnlySpan<char> text, bool hide_text_after_hash = true)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        // Hide anything after a '##' string
        int text_display_end;
        if (hide_text_after_hash)
        {
            text_display_end = FindRenderedTextEnd(text);
        }
        else
        {
            text_display_end = text.Length;
        }

        if (text_display_end != 0) // (C++: text != text_display_end)
        {
            window.DrawList.AddText(g.Font, g.FontSize, pos, GetColorU32(ImGuiCol_Text), text, 0, text_display_end);
            if (g.LogEnabled)
                LogRenderedText(pos, text.Slice(0, text_display_end).ToString(), text_display_end);
        }
    }

    public static void RenderTextWrapped(ImVec2 pos, string text, int text_end, float wrap_width)
        => RenderTextWrapped(pos, text.AsSpan(0, text_end < 0 ? text.Length : text_end), wrap_width);

    public static void RenderTextWrapped(ImVec2 pos, ReadOnlySpan<char> text, float wrap_width)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        if (text.Length != 0)
        {
            window.DrawList.AddText(g.Font, g.FontSize, pos, GetColorU32(ImGuiCol_Text), text, 0, text.Length, wrap_width);
            if (g.LogEnabled)
                LogRenderedText(pos, text.ToString(), text.Length);
        }
    }

    // Default clip_rect uses (pos_min,pos_max)
    // Handle clipping on CPU immediately (vs typically let the GPU clip the triangles that are overlapping the clipping rectangle edges)
    // FIXME-OPT: Since we have or calculate text_size we could coarse clip whole block immediately, especially for text above draw_list->DrawList.
    // Effectively as this is called from widget doing their own coarse clipping it's not very valuable presently. Next time function will take
    // better advantage of the render function taking size into account for coarse clipping.
    public static void RenderTextClippedEx(ImDrawList draw_list, ImVec2 pos_min, ImVec2 pos_max, string text, int text_display_end,
        ImVec2? text_size_if_known = null, ImVec2? align = null, ImRect? clip_rect = null)
        => RenderTextClippedEx(draw_list, pos_min, pos_max, text.AsSpan(0, text_display_end), text_size_if_known, align, clip_rect);

    public static void RenderTextClippedEx(ImDrawList draw_list, ImVec2 pos_min, ImVec2 pos_max, ReadOnlySpan<char> text,
        ImVec2? text_size_if_known = null, ImVec2? align = null, ImRect? clip_rect = null)
    {
        // Perform CPU side clipping for single clipped element to avoid using scissor state
        ImVec2 pos = pos_min;
        ImVec2 text_size = text_size_if_known.HasValue ? text_size_if_known.Value : CalcTextSize(text, false, 0.0f);

        ImVec2 clip_min = clip_rect.HasValue ? clip_rect.Value.Min : pos_min;
        ImVec2 clip_max = clip_rect.HasValue ? clip_rect.Value.Max : pos_max;
        bool need_clipping = (pos.x + text_size.x >= clip_max.x) || (pos.y + text_size.y >= clip_max.y);
        if (clip_rect.HasValue) // If we had no explicit clipping rectangle then pos==clip_min
            need_clipping |= (pos.x < clip_min.x) || (pos.y < clip_min.y);

        // Align whole block. We should defer that to the better rendering function when we'll have support for individual line alignment.
        ImVec2 align_v = align ?? new ImVec2(0.0f, 0.0f);
        if (align_v.x > 0.0f) pos.x = ImMath.ImMax(pos.x, pos.x + (pos_max.x - pos.x - text_size.x) * align_v.x);
        if (align_v.y > 0.0f) pos.y = ImMath.ImMax(pos.y, pos.y + (pos_max.y - pos.y - text_size.y) * align_v.y);

        // Render
        if (need_clipping)
        {
            var fine_clip_rect = new ImVec4(clip_min.x, clip_min.y, clip_max.x, clip_max.y);
            draw_list.AddText(null, 0.0f, pos, GetColorU32(ImGuiCol_Text), text, 0, text.Length, 0.0f, fine_clip_rect);
        }
        else
        {
            draw_list.AddText(null, 0.0f, pos, GetColorU32(ImGuiCol_Text), text, 0, text.Length, 0.0f, null);
        }
    }

    public static void RenderTextClipped(ImVec2 pos_min, ImVec2 pos_max, string text, int text_end = -1,
        ImVec2? text_size_if_known = null, ImVec2? align = null, ImRect? clip_rect = null)
        => RenderTextClipped(pos_min, pos_max, text.AsSpan(0, text_end < 0 ? text.Length : text_end), text_size_if_known, align, clip_rect);

    public static void RenderTextClipped(ImVec2 pos_min, ImVec2 pos_max, ReadOnlySpan<char> text,
        ImVec2? text_size_if_known = null, ImVec2? align = null, ImRect? clip_rect = null)
    {
        // Hide anything after a '##' string
        int text_display_end = FindRenderedTextEnd(text);
        if (text_display_end == 0)
            return;
        text = text.Slice(0, text_display_end);

        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        RenderTextClippedEx(window.DrawList, pos_min, pos_max, text, text_size_if_known, align, clip_rect);
        if (g.LogEnabled)
            LogRenderedText(pos_min, text.ToString(), text_display_end);
    }

    // Another overly complex function until we reorganize everything into a nice all-in-one helper.
    // This is made more complex because we have dissociated the layout rectangle (pos_min..pos_max) from 'ellipsis_max_x' which may be beyond it.
    // This is because in the context of tabs we selectively hide part of the text when the Close Button appears, but we don't want the ellipsis to move.
    // (BREAKING) On 2025/04/16 we removed the 'float clip_max_x' parameters which was preceding 'float ellipsis_max' and was the same value for 99% of users.
    public static void RenderTextEllipsis(ImDrawList draw_list, ImVec2 pos_min, ImVec2 pos_max, float ellipsis_max_x, string text, int text_end_full = -1, ImVec2? text_size_if_known = null)
        => RenderTextEllipsis(draw_list, pos_min, pos_max, ellipsis_max_x, text.AsSpan(0, text_end_full < 0 ? text.Length : text_end_full), text_size_if_known);

    public static void RenderTextEllipsis(ImDrawList draw_list, ImVec2 pos_min, ImVec2 pos_max, float ellipsis_max_x, ReadOnlySpan<char> text, ImVec2? text_size_if_known = null)
    {
        ImGuiContext g = GImGui;
        int text_end_full = FindRenderedTextEnd(text);
        if (text_end_full < text.Length)
            text = text.Slice(0, text_end_full);
        ImVec2 text_size = text_size_if_known.HasValue ? text_size_if_known.Value : CalcTextSize(text, false, 0.0f);

        //draw_list->AddLineV(pos_max.x, pos_min.y - 4, pos_max.y + 6, IM_COL32(0, 0, 255, 255));
        //draw_list->AddLineV(ellipsis_max_x, pos_min.y - 2, pos_max.y + 3, IM_COL32(0, 255, 0, 255));

        // FIXME: We could technically remove (last_glyph->AdvanceX - last_glyph->X1) from text_size.x here and save a few pixels.
        if (text_size.x > pos_max.x - pos_min.x)
        {
            // Hello wo...
            // |       |   |
            // min   max   ellipsis_max
            //          <-> this is generally some padding value

            ImFont font = draw_list._Data.Font;
            float font_size = draw_list._Data.FontSize;
            float font_scale = draw_list._Data.FontScale;
            int text_end_ellipsis;
            ImFontBaked baked = font.GetFontBaked(font_size);
            float ellipsis_width = baked.GetCharAdvance(font.EllipsisChar) * font_scale;

            // We can now claim the space between pos_max.x and ellipsis_max.x
            float text_avail_width = ImMath.ImMax((ImMath.ImMax(pos_max.x, ellipsis_max_x) - ellipsis_width) - pos_min.x, 1.0f);
            // (C++: font->CalcTextSizeA(font_size, text_avail_width, 0.0f, text, text_end_full, &text_end_ellipsis).x)
            float text_size_clipped_x = ImFontAtlas.ImFontCalcTextSizeEx(font, font_size, text_avail_width, 0.0f, text, 0, text_end_full, text_end_full, out text_end_ellipsis, null, ImDrawTextFlags_.ImDrawTextFlags_None).x;

            // Render text, render ellipsis
            RenderTextClippedEx(draw_list, pos_min, pos_max, text.Slice(0, text_end_ellipsis), text_size, new ImVec2(0.0f, 0.0f));
            var cpu_fine_clip_rect = new ImVec4(pos_min.x, pos_min.y, pos_max.x, pos_max.y);
            ImVec2 ellipsis_pos = ImMath.ImTrunc(new ImVec2(pos_min.x + text_size_clipped_x, pos_min.y));
            font.RenderChar(draw_list, font_size, ellipsis_pos, GetColorU32(ImGuiCol_Text), font.EllipsisChar, cpu_fine_clip_rect);
        }
        else
        {
            RenderTextClippedEx(draw_list, pos_min, pos_max, text, text_size, new ImVec2(0.0f, 0.0f));
        }

        if (g.LogEnabled)
            LogRenderedText(pos_min, text.ToString(), text_end_full);
    }

    // Render a rectangle shaped with optional rounding and borders
    public static void RenderFrame(ImVec2 p_min, ImVec2 p_max, ImU32 fill_col, bool borders = true, float rounding = 0.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        window.DrawList.AddRectFilled(p_min, p_max, fill_col, rounding);
        float border_size = g.Style.FrameBorderSize;
        if (borders && border_size > 0.0f)
        {
            window.DrawList.AddRect(p_min + new ImVec2(1, 1), p_max + new ImVec2(1, 1), GetColorU32(ImGuiCol_BorderShadow), rounding, border_size);
            window.DrawList.AddRect(p_min, p_max, GetColorU32(ImGuiCol_Border), rounding, border_size);
        }
    }

    public static void RenderFrameBorder(ImVec2 p_min, ImVec2 p_max, float rounding = 0.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        float border_size = g.Style.FrameBorderSize;
        if (border_size > 0.0f)
        {
            window.DrawList.AddRect(p_min + new ImVec2(1, 1), p_max + new ImVec2(1, 1), GetColorU32(ImGuiCol_BorderShadow), rounding, border_size);
            window.DrawList.AddRect(p_min, p_max, GetColorU32(ImGuiCol_Border), rounding, border_size);
        }
    }

    public static void RenderColorComponentMarker(ImRect bb, ImU32 col, float rounding)
    {
        if (bb.Min.x + 1 >= bb.Max.x)
            return;
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        RenderRectFilledInRangeH(window.DrawList, bb, col, bb.Min.x, ImMath.ImMin(bb.Min.x + g.Style.ColorMarkerSize, bb.Max.x), rounding);
    }

    public static void RenderNavCursor(ImRect bb, ImGuiID id, ImGuiNavRenderCursorFlags flags = 0, float rounding = -1.0f)
    {
        ImGuiContext g = GImGui;
        if (id != g.NavId)
            return;
        if (!g.NavCursorVisible && (flags & ImGuiNavRenderCursorFlags_AlwaysDraw) == 0)
            return;
        if (id == g.LastItemData.ID && (g.LastItemData.ItemFlags & ImGuiItemFlags_NoNav) != 0)
            return;

        // We don't early out on 'window->Flags & ImGuiWindowFlags_NoNavInputs' because it would be inconsistent with
        // other code directly checking NavCursorVisible. Instead we aim for NavCursorVisible to always be false.
        ImGuiWindow window = g.CurrentWindow;
        if (window.DC.NavHideHighlightOneFrame)
            return;

        if (rounding < 0.0f && (flags & ImGuiNavRenderCursorFlags_NoRounding) != 0)
            rounding = 0.0f;
        if (rounding < 0.0f)
            rounding = g.Style.FrameRounding;

        ImRect display_rect = bb;
        display_rect.ClipWith(window.ClipRect);
        float scale_factor = GetScale(); // FIXME-DPI
        float thickness = (float)(int)ImMath.ImMax(2.0f, 1.5f * scale_factor);
        if ((flags & ImGuiNavRenderCursorFlags_Compact) != 0)
        {
            window.DrawList.AddRect(display_rect.Min, display_rect.Max, GetColorU32(ImGuiCol_NavCursor), rounding, thickness);
        }
        else
        {
            float distance = (float)(int)(3.0f + thickness * 0.5f);
            display_rect.Expand(new ImVec2(distance, distance));
            bool fully_visible = window.ClipRect.Contains(display_rect);
            if (!fully_visible)
                window.DrawList.PushClipRect(display_rect.Min, display_rect.Max);
            window.DrawList.AddRect(display_rect.Min, display_rect.Max, GetColorU32(ImGuiCol_NavCursor), rounding, thickness);
            if (!fully_visible)
                window.DrawList.PopClipRect();
        }
    }

    public static void RenderMouseCursor(ImVec2 base_pos, float base_scale, ImGuiMouseCursor mouse_cursor, ImU32 col_fill, ImU32 col_border, ImU32 col_shadow)
    {
        ImGuiContext g = GImGui;
        if (mouse_cursor <= ImGuiMouseCursor_None || mouse_cursor >= ImGuiMouseCursor_COUNT) // We intentionally accept out of bound values.
            mouse_cursor = ImGuiMouseCursor_Arrow;
        ImFontAtlas font_atlas = g.DrawListSharedData.FontAtlas;
        for (int vp_i = 0; vp_i < g.Viewports.Size; vp_i++)
        {
            ImGuiViewportP viewport = g.Viewports[vp_i];
            // We scale cursor with current viewport/monitor, however Windows 10 for its own hardware cursor seems to be using a different scale factor.
            ImVec2 offset = default; ImVec2 size = default;
            // C++ fills uv[0..1] (border) and uv[2..3] (fill) via two pointers into one array.
            var uv_border = new ImVec2[2];
            var uv_fill = new ImVec2[2];
            if (!ImFontAtlas.ImFontAtlasGetMouseCursorTexData(font_atlas, mouse_cursor, ref offset, ref size, uv_border, uv_fill))
                continue;
            var pos = base_pos - offset;
            float scale = base_scale * viewport.DpiScale;
            if (!viewport.GetMainRect().Overlaps(new ImRect(pos, pos + new ImVec2(size.x + 2, size.y + 2) * scale)))
                continue;
            ImDrawList draw_list = GetForegroundDrawList(viewport);
            ImTextureRef tex_ref = font_atlas.TexRef;
            draw_list.PushTexture(tex_ref);
            draw_list.AddImage(tex_ref, pos + new ImVec2(1, 0) * scale, pos + (new ImVec2(1, 0) + size) * scale, uv_fill[0], uv_fill[1], col_shadow);
            draw_list.AddImage(tex_ref, pos + new ImVec2(2, 0) * scale, pos + (new ImVec2(2, 0) + size) * scale, uv_fill[0], uv_fill[1], col_shadow);
            draw_list.AddImage(tex_ref, pos, pos + size * scale, uv_fill[0], uv_fill[1], col_border);
            draw_list.AddImage(tex_ref, pos, pos + size * scale, uv_border[0], uv_border[1], col_fill);
            if (mouse_cursor == ImGuiMouseCursor_Wait || mouse_cursor == ImGuiMouseCursor_Progress)
            {
                float a_min = ImMath.ImFmod((float)g.Time * 5.0f, 2.0f * ImMath.IM_PI);
                float a_max = a_min + ImMath.IM_PI * 1.65f;
                draw_list.PathArcTo(pos + new ImVec2(14, -1) * scale, 6.0f * scale, a_min, a_max, 0);
                draw_list.PathStroke(col_fill, 3.0f * scale);
            }
            draw_list.PopTexture();
        }
    }

    //-----------------------------------------------------------------------------
    // [SECTION] ID STACK
    //-----------------------------------------------------------------------------

    // ImGuiWindow::GetID()/GetIDFromPos()/GetIDFromRectangle() are ported in ImGuiWindow.cs/ImGuiUtil.cs (call them, don't duplicate).

    // This is only used in rare/specific situations to manufacture an ID out of nowhere.
    // FIXME: Consider instead storing last non-zero ID + count of successive zero-ID, and combine those?
    public static void PushID(string str_id)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = window.GetID(str_id);
        window.IDStack.push_back(id);
    }

    public static void PushID(string str_id_begin, string str_id_end)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = window.GetID(str_id_begin, str_id_end);
        window.IDStack.push_back(id);
    }

    // C++ PushID(const void*): hashes the pointer value. The C# port hashes the reference identity.
    public static void PushID(object ptr_id)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = GetID(ptr_id);
        window.IDStack.push_back(id);
    }

    public static void PushID(int int_id)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = window.GetID(int_id);
        window.IDStack.push_back(id);
    }

    // C++ has no PushID(ImGuiID) overload: PushID(unsigned int) binds to PushID(int) by implicit conversion.
    // Without this, a PushID(node.ID) call in ported code boxes the uint and binds to PushID(object),
    // which hashes the box's reference identity — a different id every frame (broke dock splitter resize).
    public static void PushID(ImGuiID int_id) => PushID((int)int_id);

    // Push a given id value ignoring the ID stack as a seed.
    public static void PushOverrideID(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (g.DebugHookIdInfoId == id)
            DebugHookIdInfo(id, ImGuiDataType_ID, null, null);
        window.IDStack.push_back(id);
    }

    // Helper to avoid a common series of PushOverrideID -> GetID() -> PopID() call
    // (note that when using this pattern, ID Stack Tool will tend to not display the intermediate stack level.
    //  for that to work we would need to do PushOverrideID() -> ItemAdd() -> PopID() which would alter widget code a little more)
    public static ImGuiID GetIDWithSeed(string str, string str_end, ImGuiID seed)
    {
        // (same str_begin/str_end convention as ImGuiUtil.WindowGetID)
        ulong len = str_end != null ? (ulong)(str_end.Length - str.Length) : 0;
        ImGuiID id = ImGuiHash.ImHashStr(str, len, seed);
        ImGuiContext g = GImGui;
        if (g.DebugHookIdInfoId == id)
            DebugHookIdInfo(id, ImGuiDataType_String, str, str_end);
        return id;
    }

    public static ImGuiID GetIDWithSeed(int n, ImGuiID seed)
    {
        byte[] n_bytes = BitConverter.GetBytes(n);
        ImGuiID id = ImGuiHash.ImHashData(n_bytes, 0, sizeof(int), seed);
        ImGuiContext g = GImGui;
        if (g.DebugHookIdInfoId == id)
            DebugHookIdInfo(id, ImGuiDataType_S32, n, null);
        return id;
    }

    public static void PopID()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        if (!(window.IDStack.Size > 1))
        {
            ImUtil.ImAssertUserError(false, "Calling PopID() too many times!");
            return;
        }
        window.IDStack.pop_back();
    }

    public static ImGuiID GetID(string str_id)
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.GetID(str_id);
    }

    public static ImGuiID GetID(string str_id_begin, string str_id_end)
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.GetID(str_id_begin, str_id_end);
    }

    // C++ GetID(const void*): hashes the pointer value. The C# port hashes the reference identity.
    public static ImGuiID GetID(object ptr_id)
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        ImGuiID seed = window.IDStack.back();
        byte[] bytes = BitConverter.GetBytes(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ptr_id));
        ImGuiID id = ImGuiHash.ImHashData(bytes, 0, sizeof(int), seed);
        if (GImGui.DebugHookIdInfoId == id)
            DebugHookIdInfo(id, ImGuiDataType_Pointer, ptr_id, null);
        return id;
    }

    public static ImGuiID GetID(int int_id)
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.GetID(int_id);
    }

    //-----------------------------------------------------------------------------
    // [SECTION] ITEM SUBMISSION
    //-----------------------------------------------------------------------------
    // - KeepAliveID()
    // - ItemAdd()
    //-----------------------------------------------------------------------------

    // Code not using ItemAdd() may need to call this manually otherwise ActiveId will be cleared. In IMGUI_VERSION_NUM < 18717 this was called by GetID().
    public static void KeepAliveID(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        if (g.ActiveId == id)
            g.ActiveIdIsAlive = id;
        if (g.DeactivatedItemData.ID == id)
            g.DeactivatedItemData.IsAlive = true;
    }

    // Declare item bounding box for clipping and interaction.
    // Note that the size can be different than the one provided to ItemSize(). Typically, widgets that spread over available surface
    // declare their minimum size requirement to ItemSize() and provide a larger region to ItemAdd() which is used drawing/interaction.
    // THIS IS IN THE PERFORMANCE CRITICAL PATH (UNTIL THE CLIPPING TEST AND EARLY-RETURN)
    public static bool ItemAdd(ImRect bb, ImGuiID id, ImRect? nav_bb_arg = null, ImGuiItemFlags extra_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        // Set item data
        // (DisplayRect is left untouched, made valid when ImGuiItemStatusFlags_HasDisplayRect is set)
        g.LastItemData.ID = id;
        g.LastItemData.Rect = bb;
        g.LastItemData.NavRect = nav_bb_arg.HasValue ? nav_bb_arg.Value : bb;
        g.LastItemData.ItemFlags = g.CurrentItemFlags | g.NextItemData.ItemFlagsSet | extra_flags;
        g.LastItemData.StatusFlags = ImGuiItemStatusFlags_None;
        // Note: we don't copy 'g.NextItemData.SelectionUserData' to an hypothetical g.LastItemData.SelectionUserData: since the former is not cleared.

        if (id != 0)
        {
            KeepAliveID(id);

            // Directional navigation processing
            // Runs prior to clipping early-out
            //  (a) So that NavInitRequest can be honored, for newly opened windows to select a default widget
            //  (b) So that we can scroll up/down past clipped items. This adds a small O(N) cost to regular navigation requests
            //      unfortunately, but it is still limited to one window. It may not scale very well for windows with ten of
            //      thousands of item, but at least NavMoveRequest is only set on user interaction, aka maximum once a frame.
            //      We could early out with "if (is_clipped && !g.NavInitRequest) return false;" but when we wouldn't be able
            //      to reach unclipped widgets. This would work if user had explicit scrolling control (e.g. mapped on a stick).
            // We intentionally don't check if g.NavWindow != NULL because g.NavAnyRequest should only be set when it is non null.
            // If we crash on a NULL g.NavWindow we need to fix the bug elsewhere.
            if ((g.LastItemData.ItemFlags & ImGuiItemFlags_NoNav) == 0)
            {
                // FIXME-NAV: investigate changing the window tests into a simple 'if (g.NavFocusScopeId == g.CurrentFocusScopeId)' test.
                window.DC.NavLayersActiveMaskNext |= (short)(1 << window.DC.NavLayerCurrent);
                if (g.NavId == id || g.NavAnyRequest)
                    if (g.NavWindow.RootWindowForNav == window.RootWindowForNav)
                        if (window == g.NavWindow || ((window.ChildFlags | g.NavWindow.ChildFlags) & ImGuiChildFlags_NavFlattened) != 0)
                            NavProcessItem();
            }

            if ((g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasShortcut) != 0)
                ItemHandleShortcut(id);
        }

        // Lightweight clear of SetNextItemXXX data.
        g.NextItemData.HasFlags = ImGuiNextItemDataFlags_None;
        g.NextItemData.ItemFlagsSet = ImGuiItemFlags_None;

        // Clipping test
        // (this is an inline copy of IsClippedEx() so we can reuse the is_rect_visible value, otherwise we'd do 'if (IsClippedEx(bb, id)) return false')
        // g.NavActivateId is not necessarily == g.NavId, in the case of remote activation (e.g. shortcuts)
        bool is_rect_visible = bb.Overlaps(window.ClipRect);
        if (!is_rect_visible)
            if (id == 0 || (id != g.ActiveId && id != g.ActiveIdPreviousFrame && id != g.NavId && id != g.NavActivateId))
                if (!g.ItemUnclipByLog)
                    return false;

        // [DEBUG]
        if (id != 0)
        {
            if (id == g.DebugLocateId)
                DebugLocateItemResolveWithLastItem();

            // [DEBUG] People keep stumbling on this problem and using "" as identifier in the root of a window instead of "##something".
            // Empty identifier are valid and useful in a small amount of cases, but 99.9% of the time you want to use "##something".
            // READ THE FAQ: https://dearimgui.com/faq
            Debug.Assert(id != window.ID, "Cannot have an empty ID at the root of a window. If you need an empty label, use ## and read the FAQ about how the ID Stack works!");
        }

        if (id != 0 && g.DeactivatedItemData.ID == id)
            g.DeactivatedItemData.ElapseFrame = g.FrameCount;

        // We need to calculate this now to take account of the current clipping rectangle (as items like Selectable may change them)
        if (is_rect_visible)
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_Visible;
        if (IsMouseHoveringRect(bb.Min, bb.Max))
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredRect;
        return true;
    }

    //-----------------------------------------------------------------------------
    // [SECTION] LAYOUT
    //-----------------------------------------------------------------------------
    // - ItemSize()
    // - SameLine()
    // - GetCursorScreenPos()
    // - SetCursorScreenPos()
    // - GetCursorPos(), GetCursorPosX(), GetCursorPosY()
    // - SetCursorPos(), SetCursorPosX(), SetCursorPosY()
    // - GetCursorStartPos()
    // - Indent()
    // - Unindent()
    // - SetNextItemWidth()
    // - PushItemWidth()
    // - PushMultiItemsWidths()
    // - PopItemWidth()
    // - CalcItemWidth()
    // - CalcItemSize()
    // - GetTextLineHeight()
    // - GetTextLineHeightWithSpacing()
    // - GetFrameHeight()
    // - GetFrameHeightWithSpacing()
    // - GetContentRegionMax()
    // - GetContentRegionAvail(),
    // - BeginGroup()
    // - EndGroup()
    // Also see in imgui_widgets: tab bars, and in imgui_tables: tables, columns.
    //-----------------------------------------------------------------------------

    // Advance cursor given item size for layout.
    // Register minimum needed size so it can extend the bounding box used for auto-fit calculation.
    // See comments in ItemAdd() about how/why the size provided to ItemSize() vs ItemAdd() may often different.
    // THIS IS IN THE PERFORMANCE CRITICAL PATH.
    public static void ItemSize(ImVec2 size, float text_baseline_y = -1.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        // We increase the height in this function to accommodate for baseline offset.
        // In theory we should be offsetting the starting position (window->DC.CursorPos), that will be the topic of a larger refactor,
        // but since ItemSize() is not yet an API that moves the cursor (to handle e.g. wrapping) enlarging the height has the same effect.
        float offset_to_match_baseline_y = (text_baseline_y >= 0) ? ImMath.ImMax(0.0f, window.DC.CurrLineTextBaseOffset - text_baseline_y) : 0.0f;

        float line_y1 = window.DC.IsSameLine ? window.DC.CursorPosPrevLine.y : window.DC.CursorPos.y;
        float line_height = ImMath.ImMax(window.DC.CurrLineSize.y, /*ImMax(*/window.DC.CursorPos.y - line_y1/*, 0.0f)*/ + size.y + offset_to_match_baseline_y);

        // Always align ourselves on pixel boundaries
        //if (g.IO.KeyAlt) window->DrawList->AddRect(window->DC.CursorPos, window->DC.CursorPos + ImVec2(size.x, line_height), IM_COL32(255,0,0,200)); // [DEBUG]
        window.DC.CursorPosPrevLine.x = window.DC.CursorPos.x + size.x;
        window.DC.CursorPosPrevLine.y = line_y1;
        window.DC.CursorPos.x = ImMath.ImTrunc(window.Pos.x + window.DC.Indent.x + window.DC.ColumnsOffset.x);  // Next line
        window.DC.CursorPos.y = ImMath.ImTrunc(line_y1 + line_height + g.Style.ItemSpacing.y);                  // Next line
        window.DC.CursorMaxPos.x = ImMath.ImMax(window.DC.CursorMaxPos.x, window.DC.CursorPosPrevLine.x);
        window.DC.CursorMaxPos.y = ImMath.ImMax(window.DC.CursorMaxPos.y, window.DC.CursorPos.y - g.Style.ItemSpacing.y);
        //if (g.IO.KeyAlt) window->DrawList->AddCircle(window->DC.CursorMaxPos, 3.0f, IM_COL32(255,0,0,255), 4); // [DEBUG]

        window.DC.PrevLineSize.y = line_height;
        window.DC.CurrLineSize.y = 0.0f;
        window.DC.PrevLineTextBaseOffset = ImMath.ImMax(window.DC.CurrLineTextBaseOffset, text_baseline_y);
        window.DC.CurrLineTextBaseOffset = 0.0f;
        window.DC.IsSameLine = window.DC.IsSetPos = false;

        // Horizontal layout mode
        if (window.DC.LayoutType == ImGuiLayoutType_Horizontal)
            SameLine();
    }

    // Gets back to previous line and continue with horizontal layout
    //      offset_from_start_x == 0 : follow right after previous item
    //      offset_from_start_x != 0 : align to specified x position (relative to window/group left)
    //      spacing_w < 0            : use default spacing if offset_from_start_x == 0, no spacing if offset_from_start_x != 0
    //      spacing_w >= 0           : enforce spacing amount
    public static void SameLine(float offset_from_start_x = 0.0f, float spacing_w = -1.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        if (offset_from_start_x != 0.0f)
        {
            if (spacing_w < 0.0f)
                spacing_w = 0.0f;
            window.DC.CursorPos.x = window.Pos.x - window.Scroll.x + offset_from_start_x + spacing_w + window.DC.GroupOffset.x + window.DC.ColumnsOffset.x;
            window.DC.CursorPos.y = window.DC.CursorPosPrevLine.y;
        }
        else
        {
            if (spacing_w < 0.0f)
                spacing_w = g.Style.ItemSpacing.x;
            window.DC.CursorPos.x = window.DC.CursorPosPrevLine.x + spacing_w;
            window.DC.CursorPos.y = window.DC.CursorPosPrevLine.y;
        }
        window.DC.CurrLineSize = window.DC.PrevLineSize;
        window.DC.CurrLineTextBaseOffset = window.DC.PrevLineTextBaseOffset;
        window.DC.IsSameLine = true;
    }

    public static ImVec2 GetCursorScreenPos()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.DC.CursorPos;
    }

    public static void SetCursorScreenPos(ImVec2 pos)
    {
        ImGuiWindow window = GetCurrentWindow();
        window.DC.CursorPos = pos;
        //window->DC.CursorMaxPos = ImMax(window->DC.CursorMaxPos, window->DC.CursorPos);
        window.DC.IsSetPos = true;
    }

    // User generally sees positions in window coordinates. Internally we store CursorPos in absolute screen coordinates because it is more convenient.
    // Conversion happens as we pass the value to user, but it makes our naming convention confusing because GetCursorPos() == (DC.CursorPos - window.Pos). May want to rename 'DC.CursorPos'.
    public static ImVec2 GetCursorPos()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return new ImVec2(window.DC.CursorPos.x - window.Pos.x + window.Scroll.x, window.DC.CursorPos.y - window.Pos.y + window.Scroll.y);
    }

    public static float GetCursorPosX()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.DC.CursorPos.x - window.Pos.x + window.Scroll.x;
    }

    public static float GetCursorPosY()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.DC.CursorPos.y - window.Pos.y + window.Scroll.y;
    }

    public static void SetCursorPos(ImVec2 local_pos)
    {
        ImGuiWindow window = GetCurrentWindow();
        window.DC.CursorPos = new ImVec2(window.Pos.x - window.Scroll.x + local_pos.x, window.Pos.y - window.Scroll.y + local_pos.y);
        //window->DC.CursorMaxPos = ImMax(window->DC.CursorMaxPos, window->DC.CursorPos);
        window.DC.IsSetPos = true;
    }

    public static void SetCursorPosX(float x)
    {
        ImGuiWindow window = GetCurrentWindow();
        window.DC.CursorPos.x = window.Pos.x - window.Scroll.x + x;
        //window->DC.CursorMaxPos.x = ImMax(window->DC.CursorMaxPos.x, window->DC.CursorPos.x);
        window.DC.IsSetPos = true;
    }

    public static void SetCursorPosY(float y)
    {
        ImGuiWindow window = GetCurrentWindow();
        window.DC.CursorPos.y = window.Pos.y - window.Scroll.y + y;
        //window->DC.CursorMaxPos.y = ImMax(window->DC.CursorMaxPos.y, window->DC.CursorPos.y);
        window.DC.IsSetPos = true;
    }

    public static ImVec2 GetCursorStartPos()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return new ImVec2(window.DC.CursorStartPos.x - window.Pos.x, window.DC.CursorStartPos.y - window.Pos.y);
    }

    public static void Indent(float indent_w = 0.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        window.DC.Indent.x += (indent_w != 0.0f) ? indent_w : g.Style.IndentSpacing;
        window.DC.CursorPos.x = window.Pos.x + window.DC.Indent.x + window.DC.ColumnsOffset.x;
    }

    public static void Unindent(float indent_w = 0.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        window.DC.Indent.x -= (indent_w != 0.0f) ? indent_w : g.Style.IndentSpacing;
        window.DC.CursorPos.x = window.Pos.x + window.DC.Indent.x + window.DC.ColumnsOffset.x;
    }

    // Affect large frame+labels widgets only.
    public static void SetNextItemWidth(float item_width)
    {
        ImGuiContext g = GImGui;
        g.NextItemData.HasFlags |= ImGuiNextItemDataFlags_HasWidth;
        g.NextItemData.Width = item_width;
    }

    // FIXME: Remove the == 0.0f behavior?
    public static void PushItemWidth(float item_width)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        window.DC.ItemWidthStack.push_back(window.DC.ItemWidth); // Backup current width
        window.DC.ItemWidth = (item_width == 0.0f ? window.DC.ItemWidthDefault : item_width);
        g.NextItemData.HasFlags &= ~ImGuiNextItemDataFlags_HasWidth;
    }

    public static void PushMultiItemsWidths(int components, float w_full)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert(components > 0);
        ImGuiStyle style = g.Style;
        window.DC.ItemWidthStack.push_back(window.DC.ItemWidth); // Backup current width
        float w_items = w_full - style.ItemInnerSpacing.x * (components - 1);
        float prev_split = w_items;
        for (int i = components - 1; i > 0; i--)
        {
            float next_split = ImMath.ImTrunc(w_items * i / components);
            window.DC.ItemWidthStack.push_back(ImMath.ImMax(prev_split - next_split, 1.0f));
            prev_split = next_split;
        }
        window.DC.ItemWidth = ImMath.ImMax(prev_split, 1.0f);
        g.NextItemData.HasFlags &= ~ImGuiNextItemDataFlags_HasWidth;
    }

    public static void PopItemWidth()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.DC.ItemWidthStack.Size <= 0)
        {
            ImUtil.ImAssertUserError(false, "Calling PopItemWidth() too many times!");
            return;
        }
        window.DC.ItemWidth = window.DC.ItemWidthStack.back();
        window.DC.ItemWidthStack.pop_back();
    }

    // Calculate default item width given value passed to PushItemWidth() or SetNextItemWidth().
    // The SetNextItemWidth() data is generally cleared/consumed by ItemAdd() or NextItemData.ClearFlags()
    public static float CalcItemWidth()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        float w;
        if ((g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasWidth) != 0)
            w = g.NextItemData.Width;
        else
            w = window.DC.ItemWidth;
        if (w < 0.0f)
        {
            float region_avail_x = GetContentRegionAvail().x;
            w = ImMath.ImMax(1.0f, region_avail_x + w);
        }
        w = ImMath.ImTrunc(w);
        return w;
    }

    // [Internal] Calculate full item size given user provided 'size' parameter and default width/height. Default width is often == CalcItemWidth().
    // Those two functions CalcItemWidth vs CalcItemSize are awkwardly named because they are not fully symmetrical.
    // Note that only CalcItemWidth() is publicly exposed.
    // The 4.0f here may be changed to match CalcItemWidth() and/or BeginChild() (right now we have a mismatch which is harmless but undesirable)
    public static ImVec2 CalcItemSize(ImVec2 size, float default_w, float default_h)
    {
        ImVec2 avail = default; // (C++ leaves it uninitialized; only read when x or y is negative, in which case it is assigned)
        if (size.x < 0.0f || size.y < 0.0f)
            avail = GetContentRegionAvail();

        if (size.x == 0.0f)
            size.x = default_w;
        else if (size.x < 0.0f)
            size.x = ImMath.ImMax(4.0f, avail.x + size.x); // <-- size.x is negative here so we are subtracting

        if (size.y == 0.0f)
            size.y = default_h;
        else if (size.y < 0.0f)
            size.y = ImMath.ImMax(4.0f, avail.y + size.y); // <-- size.y is negative here so we are subtracting

        return size;
    }

    public static float GetTextLineHeight()
    {
        ImGuiContext g = GImGui;
        return g.FontSize;
    }

    public static float GetTextLineHeightWithSpacing()
    {
        ImGuiContext g = GImGui;
        return g.FontSize + g.Style.ItemSpacing.y;
    }

    public static float GetFrameHeight()
    {
        ImGuiContext g = GImGui;
        return g.FontSize + g.Style.FramePadding.y * 2.0f;
    }

    public static float GetFrameHeightWithSpacing()
    {
        ImGuiContext g = GImGui;
        return g.FontSize + g.Style.FramePadding.y * 2.0f + g.Style.ItemSpacing.y;
    }

    public static ImVec2 GetContentRegionAvail()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImVec2 mx = (window.DC.CurrentColumns != null || g.CurrentTable != null) ? window.WorkRect.Max : window.ContentRegionRect.Max;
        return new ImVec2(mx.x - window.DC.CursorPos.x, mx.y - window.DC.CursorPos.y);
    }

    // (Obsolete functions, kept for parity, cf. #ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS in C++)
    // You should never need those functions. Always use GetCursorScreenPos() and GetContentRegionAvail()!
    // They are bizarre local-coordinates which don't play well with scrolling.
    public static ImVec2 GetContentRegionMax()
    {
        return new ImVec2(GetContentRegionAvail().x + GetCursorScreenPos().x - GetWindowPos().x, GetContentRegionAvail().y + GetCursorScreenPos().y - GetWindowPos().y);
    }

    public static ImVec2 GetWindowContentRegionMin()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return new ImVec2(window.ContentRegionRect.Min.x - window.Pos.x, window.ContentRegionRect.Min.y - window.Pos.y);
    }

    public static ImVec2 GetWindowContentRegionMax()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return new ImVec2(window.ContentRegionRect.Max.x - window.Pos.x, window.ContentRegionRect.Max.y - window.Pos.y);
    }

    // Lock horizontal starting position + capture group bounding box into one "item" (so you can use IsItemHovered() or layout primitives such as SameLine() on whole group, etc.)
    // Groups are currently a mishmash of functionalities which should perhaps be clarified and separated.
    // FIXME-OPT: Could we safely early out on ->SkipItems?
    public static void BeginGroup()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        g.GroupStack.resize(g.GroupStack.Size + 1);
        ImGuiGroupData group_data = g.GroupStack.back();
        group_data.WindowID = window.ID;
        group_data.BackupCursorPos = window.DC.CursorPos;
        group_data.BackupCursorPosPrevLine = window.DC.CursorPosPrevLine;
        group_data.BackupCursorMaxPos = window.DC.CursorMaxPos;
        group_data.BackupIndent = window.DC.Indent;
        group_data.BackupGroupOffset = window.DC.GroupOffset;
        group_data.BackupCurrLineSize = window.DC.CurrLineSize;
        group_data.BackupCurrLineTextBaseOffset = window.DC.CurrLineTextBaseOffset;
        group_data.BackupActiveIdIsAlive = g.ActiveIdIsAlive;
        group_data.BackupHoveredIdIsAlive = g.HoveredId != 0;
        group_data.BackupIsSameLine = window.DC.IsSameLine;
        group_data.BackupAnyIdHasBeenEditedThisFrame = g.AnyIdHasBeenEditedThisFrame;
        group_data.BackupDeactivatedIdIsAlive = g.DeactivatedItemData.IsAlive;
        group_data.EmitItem = true;
        g.GroupStack.Data[g.GroupStack.Size - 1] = group_data;

        window.DC.GroupOffset.x = window.DC.CursorPos.x - window.Pos.x - window.DC.ColumnsOffset.x;
        window.DC.Indent = window.DC.GroupOffset;
        window.DC.CursorMaxPos = window.DC.CursorPos;
        window.DC.CurrLineSize = new ImVec2(0.0f, 0.0f);
        if (g.LogEnabled)
            g.LogLinePosY = -float.MaxValue; // To enforce a carriage return
    }

    public static void EndGroup()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert(g.GroupStack.Size > 0); // Mismatched BeginGroup()/EndGroup() calls

        ImGuiGroupData group_data = g.GroupStack.back();
        Debug.Assert(group_data.WindowID == window.ID); // EndGroup() in wrong window?

        if (window.DC.IsSetPos)
            ErrorCheckUsingSetCursorPosToExtendParentBoundaries();

        // Include LastItemData.Rect.Max as a workaround for e.g. EndTable() undershooting with CursorMaxPos report. (#7543)
        ImRect group_bb = new ImRect(group_data.BackupCursorPos, ImMath.ImMax(ImMath.ImMax(window.DC.CursorMaxPos, g.LastItemData.Rect.Max), group_data.BackupCursorPos));
        window.DC.CursorPos = group_data.BackupCursorPos;
        window.DC.CursorPosPrevLine = group_data.BackupCursorPosPrevLine;
        window.DC.CursorMaxPos = ImMath.ImMax(group_data.BackupCursorMaxPos, group_bb.Max);
        window.DC.Indent = group_data.BackupIndent;
        window.DC.GroupOffset = group_data.BackupGroupOffset;
        window.DC.CurrLineSize = group_data.BackupCurrLineSize;
        window.DC.CurrLineTextBaseOffset = group_data.BackupCurrLineTextBaseOffset;
        window.DC.IsSameLine = group_data.BackupIsSameLine;
        if (g.LogEnabled)
            g.LogLinePosY = -float.MaxValue; // To enforce a carriage return

        if (!group_data.EmitItem)
        {
            g.GroupStack.pop_back();
            return;
        }

        window.DC.CurrLineTextBaseOffset = ImMath.ImMax(window.DC.PrevLineTextBaseOffset, group_data.BackupCurrLineTextBaseOffset); // FIXME: Incorrect, we should grab the base offset from the *first line* of the group but it is hard to obtain now.
        ItemSize(group_bb.GetSize());
        ItemAdd(group_bb, 0, null, ImGuiItemFlags_NoTabStop);

        // If the current ActiveId was declared within the boundary of our group, we copy it to LastItemId so IsItemActive(), IsItemDeactivated() etc. will be functional on the entire group.
        // It would be neater if we replaced window.DC.LastItemId by e.g. 'bool LastItemIsActive', but would put a little more burden on individual widgets.
        // Also if you grep for LastItemId you'll notice it is only used in that context.
        // (The two tests not the same because ActiveIdIsAlive is an ID itself, in order to be able to handle ActiveId being overwritten during the frame.)
        bool group_contains_curr_active_id = (group_data.BackupActiveIdIsAlive != g.ActiveId) && (g.ActiveIdIsAlive == g.ActiveId) && g.ActiveId != 0;
        bool group_contains_deactivated_id = (group_data.BackupDeactivatedIdIsAlive == false) && (g.DeactivatedItemData.IsAlive == true);
        if (group_contains_curr_active_id)
            g.LastItemData.ID = g.ActiveId;
        else if (group_contains_deactivated_id)
            g.LastItemData.ID = g.DeactivatedItemData.ID;
        g.LastItemData.Rect = group_bb;

        // Forward Hovered flag
        bool group_contains_curr_hovered_id = (group_data.BackupHoveredIdIsAlive == false) && g.HoveredId != 0;
        if (group_contains_curr_hovered_id)
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredWindow;

        // Forward Edited flag
        if (g.AnyIdHasBeenEditedThisFrame && !group_data.BackupAnyIdHasBeenEditedThisFrame)
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_Edited;

        // Forward Deactivated flag
        g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HasDeactivated;
        if (group_contains_deactivated_id)
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_Deactivated;

        g.GroupStack.pop_back();
        if (g.DebugShowGroupRects)
            window.DrawList.AddRect(group_bb.Min, group_bb.Max, ImGuiCore.IM_COL32(255, 0, 255, 255));   // [Debug]
    }

    //-----------------------------------------------------------------------------
    // [SECTION] SCROLLING
    //-----------------------------------------------------------------------------

    // Helper to snap on edges when aiming at an item very close to the edge,
    // So the difference between WindowPadding and ItemSpacing will be in the visible area after scrolling.
    // When we refactor the scrolling API this may be configurable with a flag?
    // Note that the effect for this won't be visible on X axis with default Style settings as WindowPadding.x == ItemSpacing.x by default.
    static float CalcScrollEdgeSnap(float target, float snap_min, float snap_max, float snap_threshold, float center_ratio)
    {
        if (target <= snap_min + snap_threshold)
            return ImMath.ImLerp(snap_min, target, center_ratio);
        if (target >= snap_max - snap_threshold)
            return ImMath.ImLerp(target, snap_max, center_ratio);
        return target;
    }

    static ImVec2 CalcNextScrollFromScrollTargetAndClamp(ImGuiWindow window)
    {
        ImVec2 scroll = window.Scroll;
        ImVec2 decoration_size = new ImVec2(window.DecoOuterSizeX1 + window.DecoInnerSizeX1 + window.DecoOuterSizeX2, window.DecoOuterSizeY1 + window.DecoInnerSizeY1 + window.DecoOuterSizeY2);
        for (int axis = 0; axis < 2; axis++)
        {
            if (window.ScrollTarget[axis] < float.MaxValue)
            {
                float center_ratio = window.ScrollTargetCenterRatio[axis];
                float scroll_target = window.ScrollTarget[axis];
                if (window.ScrollTargetEdgeSnapDist[axis] > 0.0f)
                {
                    float snap_min = 0.0f;
                    float snap_max = window.ScrollMax[axis] + window.SizeFull[axis] - decoration_size[axis];
                    scroll_target = CalcScrollEdgeSnap(scroll_target, snap_min, snap_max, window.ScrollTargetEdgeSnapDist[axis], center_ratio);
                }
                scroll[axis] = scroll_target - center_ratio * (window.SizeFull[axis] - decoration_size[axis]);
            }
            scroll[axis] = (float)(long)(scroll[axis] + 0.5f); // ImRound64()
            if (!window.Collapsed && !window.SkipItems)
                scroll[axis] = ImMath.ImMin(scroll[axis], window.ScrollMax[axis]);
        }
        return scroll;
    }

    public static void ScrollToItem(ImGuiScrollFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ScrollToRectEx(window, g.LastItemData.NavRect, flags);
    }

    public static void ScrollToRect(ImGuiWindow window, ImRect item_rect, ImGuiScrollFlags flags = 0)
    {
        ScrollToRectEx(window, item_rect, flags);
    }

    // Scroll to keep newly navigated item fully into view
    public static ImVec2 ScrollToRectEx(ImGuiWindow window, ImRect item_rect, ImGuiScrollFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImRect scroll_rect = new ImRect(new ImVec2(window.InnerRect.Min.x - 1, window.InnerRect.Min.y - 1), new ImVec2(window.InnerRect.Max.x + 1, window.InnerRect.Max.y + 1));
        scroll_rect.Min.x = ImMath.ImMin(scroll_rect.Min.x + window.DecoInnerSizeX1, scroll_rect.Max.x);
        scroll_rect.Min.y = ImMath.ImMin(scroll_rect.Min.y + window.DecoInnerSizeY1, scroll_rect.Max.y);
        //GetForegroundDrawList(window)->AddRect(item_rect.Min, item_rect.Max, IM_COL32(255,0,0,255), 0.0f, 0, 5.0f); // [DEBUG]
        //GetForegroundDrawList(window)->AddRect(scroll_rect.Min, scroll_rect.Max, IM_COL32_WHITE); // [DEBUG]

        // Check that only one behavior is selected per axis
        Debug.Assert((flags & ImGuiScrollFlags_MaskX_) == 0 || ImMath.ImIsPowerOfTwo(flags & ImGuiScrollFlags_MaskX_));
        Debug.Assert((flags & ImGuiScrollFlags_MaskY_) == 0 || ImMath.ImIsPowerOfTwo(flags & ImGuiScrollFlags_MaskY_));

        // Defaults
        ImGuiScrollFlags in_flags = flags;
        if ((flags & ImGuiScrollFlags_MaskX_) == 0 && window.ScrollbarX)
            flags |= ImGuiScrollFlags_KeepVisibleEdgeX;
        if ((flags & ImGuiScrollFlags_MaskY_) == 0)
            flags |= window.Appearing ? ImGuiScrollFlags_AlwaysCenterY : ImGuiScrollFlags_KeepVisibleEdgeY;

        bool fully_visible_x = item_rect.Min.x >= scroll_rect.Min.x && item_rect.Max.x <= scroll_rect.Max.x;
        bool fully_visible_y = item_rect.Min.y >= scroll_rect.Min.y && item_rect.Max.y <= scroll_rect.Max.y;
        bool can_be_fully_visible_x = (item_rect.GetWidth() + g.Style.ItemSpacing.x * 2.0f) <= scroll_rect.GetWidth() || (window.AutoFitFramesX > 0) || (window.Flags & ImGuiWindowFlags_AlwaysAutoResize) != 0;
        bool can_be_fully_visible_y = (item_rect.GetHeight() + g.Style.ItemSpacing.y * 2.0f) <= scroll_rect.GetHeight() || (window.AutoFitFramesY > 0) || (window.Flags & ImGuiWindowFlags_AlwaysAutoResize) != 0;

        if ((flags & ImGuiScrollFlags_KeepVisibleEdgeX) != 0 && !fully_visible_x)
        {
            if (item_rect.Min.x < scroll_rect.Min.x || !can_be_fully_visible_x)
                SetScrollFromPosX(window, item_rect.Min.x - g.Style.ItemSpacing.x - window.Pos.x, 0.0f);
            else if (item_rect.Max.x >= scroll_rect.Max.x)
                SetScrollFromPosX(window, item_rect.Max.x + g.Style.ItemSpacing.x - window.Pos.x, 1.0f);
        }
        else if (((flags & ImGuiScrollFlags_KeepVisibleCenterX) != 0 && !fully_visible_x) || (flags & ImGuiScrollFlags_AlwaysCenterX) != 0)
        {
            if (can_be_fully_visible_x)
                SetScrollFromPosX(window, ImMath.ImTrunc((item_rect.Min.x + item_rect.Max.x) * 0.5f) - window.Pos.x, 0.5f);
            else
                SetScrollFromPosX(window, item_rect.Min.x - window.Pos.x, 0.0f);
        }

        if ((flags & ImGuiScrollFlags_KeepVisibleEdgeY) != 0 && !fully_visible_y)
        {
            if (item_rect.Min.y < scroll_rect.Min.y || !can_be_fully_visible_y)
                SetScrollFromPosY(window, item_rect.Min.y - g.Style.ItemSpacing.y - window.Pos.y, 0.0f);
            else if (item_rect.Max.y >= scroll_rect.Max.y)
                SetScrollFromPosY(window, item_rect.Max.y + g.Style.ItemSpacing.y - window.Pos.y, 1.0f);
        }
        else if (((flags & ImGuiScrollFlags_KeepVisibleCenterY) != 0 && !fully_visible_y) || (flags & ImGuiScrollFlags_AlwaysCenterY) != 0)
        {
            if (can_be_fully_visible_y)
                SetScrollFromPosY(window, ImMath.ImTrunc((item_rect.Min.y + item_rect.Max.y) * 0.5f) - window.Pos.y, 0.5f);
            else
                SetScrollFromPosY(window, item_rect.Min.y - window.Pos.y, 0.0f);
        }

        ImVec2 next_scroll = CalcNextScrollFromScrollTargetAndClamp(window);
        ImVec2 delta_scroll = new ImVec2(next_scroll.x - window.Scroll.x, next_scroll.y - window.Scroll.y);

        // Also scroll parent window to keep us into view if necessary
        if ((flags & ImGuiScrollFlags_NoScrollParent) == 0 && (window.Flags & ImGuiWindowFlags_ChildWindow) != 0)
        {
            // FIXME-SCROLL: May be an option?
            if ((in_flags & (ImGuiScrollFlags_AlwaysCenterX | ImGuiScrollFlags_KeepVisibleCenterX)) != 0)
                in_flags = (in_flags & ~ImGuiScrollFlags_MaskX_) | ImGuiScrollFlags_KeepVisibleEdgeX;
            if ((in_flags & (ImGuiScrollFlags_AlwaysCenterY | ImGuiScrollFlags_KeepVisibleCenterY)) != 0)
                in_flags = (in_flags & ~ImGuiScrollFlags_MaskY_) | ImGuiScrollFlags_KeepVisibleEdgeY;
            var shifted = new ImRect(new ImVec2(item_rect.Min.x - delta_scroll.x, item_rect.Min.y - delta_scroll.y), new ImVec2(item_rect.Max.x - delta_scroll.x, item_rect.Max.y - delta_scroll.y));
            ImVec2 parent_delta = ScrollToRectEx(window.ParentWindow, shifted, in_flags);
            delta_scroll = new ImVec2(delta_scroll.x + parent_delta.x, delta_scroll.y + parent_delta.y);
        }

        return delta_scroll;
    }

    public static float GetScrollX()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.Scroll.x;
    }

    public static float GetScrollY()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.Scroll.y;
    }

    public static float GetScrollMaxX()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.ScrollMax.x;
    }

    public static float GetScrollMaxY()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.ScrollMax.y;
    }

    public static void SetScrollX(ImGuiWindow window, float scroll_x)
    {
        window.ScrollTarget.x = scroll_x;
        window.ScrollTargetCenterRatio.x = 0.0f;
        window.ScrollTargetEdgeSnapDist.x = 0.0f;
    }

    public static void SetScrollY(ImGuiWindow window, float scroll_y)
    {
        window.ScrollTarget.y = scroll_y;
        window.ScrollTargetCenterRatio.y = 0.0f;
        window.ScrollTargetEdgeSnapDist.y = 0.0f;
    }

    public static void SetScrollX(float scroll_x)
    {
        ImGuiContext g = GImGui;
        SetScrollX(g.CurrentWindow, scroll_x);
    }

    public static void SetScrollY(float scroll_y)
    {
        ImGuiContext g = GImGui;
        SetScrollY(g.CurrentWindow, scroll_y);
    }

    // Note that a local position will vary depending on initial scroll value,
    // This is a little bit confusing so bear with us:
    //  - local_pos = (absolution_pos - window->Pos)
    //  - So local_x/local_y are 0.0f for a position at the upper-left corner of a window,
    //    and generally local_x/local_y are >(padding+decoration) && <(size-padding-decoration) when in the visible area.
    //  - They mostly exist because of legacy API.
    // Following the rules above, when trying to work with scrolling code, consider that:
    //  - SetScrollFromPosY(0.0f) == SetScrollY(0.0f + scroll.y) == has no effect!
    //  - SetScrollFromPosY(-scroll.y) == SetScrollY(-scroll.y + scroll.y) == SetScrollY(0.0f) == reset scroll. Of course writing SetScrollY(0.0f) directly then makes more sense
    // We store a target position so centering and clamping can occur on the next frame when we are guaranteed to have a known window size
    public static void SetScrollFromPosX(ImGuiWindow window, float local_x, float center_x_ratio)
    {
        Debug.Assert(center_x_ratio >= 0.0f && center_x_ratio <= 1.0f);
        window.ScrollTarget.x = ImMath.ImTrunc(local_x - window.DecoOuterSizeX1 - window.DecoInnerSizeX1 + window.Scroll.x); // Convert local position to scroll offset
        window.ScrollTargetCenterRatio.x = center_x_ratio;
        window.ScrollTargetEdgeSnapDist.x = 0.0f;
    }

    public static void SetScrollFromPosY(ImGuiWindow window, float local_y, float center_y_ratio)
    {
        Debug.Assert(center_y_ratio >= 0.0f && center_y_ratio <= 1.0f);
        window.ScrollTarget.y = ImMath.ImTrunc(local_y - window.DecoOuterSizeY1 - window.DecoInnerSizeY1 + window.Scroll.y); // Convert local position to scroll offset
        window.ScrollTargetCenterRatio.y = center_y_ratio;
        window.ScrollTargetEdgeSnapDist.y = 0.0f;
    }

    public static void SetScrollFromPosX(float local_x, float center_x_ratio = 0.5f)
    {
        ImGuiContext g = GImGui;
        SetScrollFromPosX(g.CurrentWindow, local_x, center_x_ratio);
    }

    public static void SetScrollFromPosY(float local_y, float center_y_ratio = 0.5f)
    {
        ImGuiContext g = GImGui;
        SetScrollFromPosY(g.CurrentWindow, local_y, center_y_ratio);
    }

    // center_x_ratio: 0.0f left of last item, 0.5f horizontal center of last item, 1.0f right of last item.
    public static void SetScrollHereX(float center_x_ratio = 0.5f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        float spacing_x = ImMath.ImMax(window.WindowPadding.x, g.Style.ItemSpacing.x);
        float target_pos_x = ImMath.ImLerp(g.LastItemData.Rect.Min.x - spacing_x, g.LastItemData.Rect.Max.x + spacing_x, center_x_ratio);
        SetScrollFromPosX(window, target_pos_x - window.Pos.x, center_x_ratio); // Convert from absolute to local pos

        // Tweak: snap on edges when aiming at an item very close to the edge
        window.ScrollTargetEdgeSnapDist.x = ImMath.ImMax(0.0f, window.WindowPadding.x - spacing_x);
    }

    // center_y_ratio: 0.0f top of last item, 0.5f vertical center of last item, 1.0f bottom of last item.
    public static void SetScrollHereY(float center_y_ratio = 0.5f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        float spacing_y = ImMath.ImMax(window.WindowPadding.y, g.Style.ItemSpacing.y);
        float target_pos_y = ImMath.ImLerp(window.DC.CursorPosPrevLine.y - spacing_y, window.DC.CursorPosPrevLine.y + window.DC.PrevLineSize.y + spacing_y, center_y_ratio);
        SetScrollFromPosY(window, target_pos_y - window.Pos.y, center_y_ratio); // Convert from absolute to local pos

        // Tweak: snap on edges when aiming at an item very close to the edge
        window.ScrollTargetEdgeSnapDist.y = ImMath.ImMax(0.0f, window.WindowPadding.y - spacing_y);
    }

    //-----------------------------------------------------------------------------
    // [SECTION] TOOLTIPS
    //-----------------------------------------------------------------------------

    // Tooltip offset (imgui.cpp static constants)
    private static readonly ImVec2 TOOLTIP_DEFAULT_OFFSET_MOUSE = new ImVec2(16, 10);      // Multiplied by g.Style.MouseCursorScale
    private static readonly ImVec2 TOOLTIP_DEFAULT_OFFSET_TOUCH = new ImVec2(0, -20);      // Multiplied by g.Style.MouseCursorScale
    private static readonly ImVec2 TOOLTIP_DEFAULT_PIVOT_TOUCH = new ImVec2(0.5f, 1.0f);   // Multiplied by g.Style.MouseCursorScale

    public static bool BeginTooltip()
    {
        return BeginTooltipEx(ImGuiTooltipFlags_None, ImGuiWindowFlags_None);
    }

    public static bool BeginItemTooltip()
    {
        if (!IsItemHovered(ImGuiHoveredFlags_ForTooltip))
            return false;
        return BeginTooltipEx(ImGuiTooltipFlags_None, ImGuiWindowFlags_None);
    }

    // (unsafe: Begin() takes a bool* p_open, mirroring the C++ signature; we pass NULL)
    public static unsafe bool BeginTooltipEx(ImGuiTooltipFlags tooltip_flags, ImGuiWindowFlags extra_window_flags)
    {
        ImGuiContext g = GImGui;

        bool is_dragdrop_tooltip = g.DragDropWithinSource || g.DragDropWithinTarget;
        if (is_dragdrop_tooltip)
        {
            // Drag and Drop tooltips are positioning differently than other tooltips:
            // - offset visibility to increase visibility around mouse.
            // - never clamp within outer viewport boundary.
            // We call SetNextWindowPos() to enforce position and disable clamping.
            // See FindBestWindowPosForPopup() for positioning logic of other tooltips (not drag and drop ones).
            //ImVec2 tooltip_pos = g.IO.MousePos - g.ActiveIdClickOffset - g.Style.WindowPadding;
            bool is_touchscreen = (g.IO.MouseSource == ImGuiMouseSource_TouchScreen);
            if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasPos) == 0)
            {
                ImVec2 tooltip_pos = is_touchscreen ? (g.IO.MousePos + TOOLTIP_DEFAULT_OFFSET_TOUCH * g.Style.MouseCursorScale) : (g.IO.MousePos + TOOLTIP_DEFAULT_OFFSET_MOUSE * g.Style.MouseCursorScale);
                ImVec2 tooltip_pivot = is_touchscreen ? TOOLTIP_DEFAULT_PIVOT_TOUCH : new ImVec2(0.0f, 0.0f);
                SetNextWindowPos(tooltip_pos, ImGuiCond_None, tooltip_pivot);
            }

            SetNextWindowBgAlpha(g.Style.Colors[ImGuiCol_PopupBg].w * 0.60f);
            //PushStyleVar(ImGuiStyleVar_Alpha, g.Style.Alpha * 0.60f); // This would be nice but e.g ColorButton with checkerboard has issue with transparent colors :(
            tooltip_flags |= ImGuiTooltipFlags_OverridePrevious;
        }

        // Hide previous tooltip from being displayed. We can't easily "reset" the content of a window so we create a new one.
        if ((tooltip_flags & ImGuiTooltipFlags_OverridePrevious) != 0 && g.TooltipPreviousWindow != null && g.TooltipPreviousWindow.Active && !IsWindowInBeginStack(g.TooltipPreviousWindow))
        {
            //IMGUI_DEBUG_LOG("[tooltip] '%s' already active, using +1 for this frame\n", window_name);
            SetWindowHiddenAndSkipItemsForCurrentFrame(g.TooltipPreviousWindow);
            g.TooltipOverrideCount++;
        }

        string window_name = ImFmt.Sprintf(is_dragdrop_tooltip ? "##Tooltip_DragDrop_%02d" : "##Tooltip_%02d", g.TooltipOverrideCount);
        ImGuiWindowFlags flags = ImGuiWindowFlags_Tooltip | ImGuiWindowFlags_NoInputs | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_AlwaysAutoResize;
        Begin(window_name, flags | extra_window_flags);
        // 2023-03-09: Added bool return value to the API, but currently always returning true.
        // If this ever returns false we need to update BeginDragDropSource() accordingly.
        //if (!ret)
        //    End();
        //return ret;
        return true;
    }

    public static void EndTooltip()
    {
        Debug.Assert((GetCurrentWindowRead().Flags & ImGuiWindowFlags_Tooltip) != 0);   // Mismatched BeginTooltip()/EndTooltip() calls
        End();
    }

    public static void SetTooltip(string fmt)
    {
        SetTooltipV(fmt);
    }

    public static void SetTooltipV(string fmt, params object[] args)
    {
        if (!BeginTooltipEx(ImGuiTooltipFlags_OverridePrevious, ImGuiWindowFlags_None))
            return;
        TextV(fmt, args);
        EndTooltip();
    }

    // Shortcut to use 'style.HoverFlagsForTooltipMouse' or 'style.HoverFlagsForTooltipNav'.
    // Defaults to == ImGuiHoveredFlags_Stationary | ImGuiHoveredFlags_DelayShort when using the mouse.
    public static void SetItemTooltip(string fmt, params object[] args)
    {
        if (IsItemHovered(ImGuiHoveredFlags_ForTooltip))
            SetTooltipV(fmt, args);
    }

    public static void SetItemTooltipV(string fmt, params object[] args)
    {
        if (IsItemHovered(ImGuiHoveredFlags_ForTooltip))
            SetTooltipV(fmt, args);
    }
}
