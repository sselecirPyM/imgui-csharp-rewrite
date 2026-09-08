// Port of ImGui core static helpers: key classification, key data, input events,
// menu columns, window IDs, list clipper machinery (imgui.cpp / imgui_widgets.cpp).
using System;

namespace ImGui;

public static partial class ImGuiUtil
{
    // ---- Key classification (imgui_internal.h) ----
    public const int ImGuiKey_LegacyNativeKey_BEGIN = 0;
    public const int ImGuiKey_LegacyNativeKey_END = 512;
    public const int ImGuiKey_Keyboard_BEGIN = ImGuiKey_NamedKey_BEGIN;
    public const int ImGuiKey_Keyboard_END = ImGuiKey_GamepadStart;
    public const int ImGuiKey_Gamepad_BEGIN = ImGuiKey_GamepadStart;
    public const int ImGuiKey_Gamepad_END = ImGuiKey_GamepadRStickDown + 1;
    public const int ImGuiKey_Mouse_BEGIN = ImGuiKey_MouseLeft;
    public const int ImGuiKey_Mouse_END = ImGuiKey_MouseWheelY + 1;
    public const int ImGuiKey_Aliases_BEGIN = ImGuiKey_Mouse_BEGIN;
    public const int ImGuiKey_Aliases_END = ImGuiKey_Mouse_END;

    public static bool IsNamedKey(ImGuiKey key) => key >= ImGuiKey_NamedKey_BEGIN && key < ImGuiKey_NamedKey_END;
    public static bool IsNamedKeyOrMod(ImGuiKey key) => (key >= ImGuiKey_NamedKey_BEGIN && key < ImGuiKey_NamedKey_END) || key == ImGuiMod_Ctrl || key == ImGuiMod_Shift || key == ImGuiMod_Alt || key == ImGuiMod_Super;
    public static bool IsLegacyKey(ImGuiKey key) => key >= ImGuiKey_LegacyNativeKey_BEGIN && key < ImGuiKey_LegacyNativeKey_END;
    public static bool IsKeyboardKey(ImGuiKey key) => key >= ImGuiKey_Keyboard_BEGIN && key < ImGuiKey_Keyboard_END;
    public static bool IsGamepadKey(ImGuiKey key) => key >= ImGuiKey_Gamepad_BEGIN && key < ImGuiKey_Gamepad_END;
    public static bool IsMouseKey(ImGuiKey key) => key >= ImGuiKey_Mouse_BEGIN && key < ImGuiKey_Mouse_END;
    public static bool IsAliasKey(ImGuiKey key) => key >= ImGuiKey_Aliases_BEGIN && key < ImGuiKey_Aliases_END;

    public static ImGuiKey ConvertSingleModFlagToKey(ImGuiContext ctx, ImGuiKey key)
    {
        ImGuiContext g = ctx;
        if (key == ImGuiMod_Ctrl) key = g.IO.ConfigMacOSXBehaviors ? ImGuiKey_LeftSuper : ImGuiKey_LeftCtrl;
        if (key == ImGuiMod_Shift) key = ImGuiKey_LeftShift;
        if (key == ImGuiMod_Alt) key = g.IO.ConfigMacOSXBehaviors ? ImGuiKey_LeftAlt : ImGuiKey_LeftAlt;
        if (key == ImGuiMod_Super) key = g.IO.ConfigMacOSXBehaviors ? ImGuiKey_LeftCtrl : ImGuiKey_LeftSuper;
        return key;
    }

    public static ImGuiKeyData GetKeyData(ImGuiContext ctx, ImGuiKey key)
    {
        ImGuiContext g = ctx;
        if ((key & ImGuiMod_Mask_) != 0)
            key = ConvertSingleModFlagToKey(ctx, key);
        System.Diagnostics.Debug.Assert(IsNamedKey(key));
        return g.IO.KeysData[key - ImGuiKey_NamedKey_BEGIN];
    }

    // ---- Input events ----
    public static ImGuiInputEvent? FindLatestInputEvent(ImGuiContext ctx, int type, int arg = -1)
    {
        ImGuiContext g = ctx;
        for (int n = g.InputEventsQueue.Size - 1; n >= 0; n--)
        {
            var e = g.InputEventsQueue[n];
            if (e.Type != type)
                continue;
            if (type == ImGuiInputEventType_Key && e.Key_Key != (ImGuiKey)arg)
                continue;
            if (type == ImGuiInputEventType_MouseButton && e.MouseButton_Button != arg)
                continue;
            return e;
        }
        return null;
    }

    // ---- ImGuiMenuColumns (imgui_widgets.cpp) ----
    public static void MenuColumnsUpdate(ImGuiMenuColumns columns, float spacing, bool window_reappearing)
    {
        if (window_reappearing)
            Array.Clear(columns.Widths, 0, columns.Widths.Length);
        columns.Spacing = (ImU16)(int)spacing;
        MenuColumnsCalcNextTotalWidth(columns, true);
        Array.Clear(columns.Widths, 0, columns.Widths.Length);
        columns.TotalWidth = columns.NextTotalWidth;
        columns.NextTotalWidth = 0;
    }

    public static void MenuColumnsCalcNextTotalWidth(ImGuiMenuColumns columns, bool update_offsets)
    {
        ImU16 offset = 0;
        bool want_spacing = false;
        for (int i = 0; i < columns.Widths.Length; i++)
        {
            ImU16 width = columns.Widths[i];
            if (want_spacing && width > 0)
                offset += columns.Spacing;
            want_spacing |= (width > 0);
            if (update_offsets)
            {
                if (i == 1) { columns.OffsetLabel = offset; }
                if (i == 2) { columns.OffsetShortcut = offset; }
                if (i == 3) { columns.OffsetMark = offset; }
            }
            offset += width;
        }
        columns.NextTotalWidth = offset;
    }

    public static float MenuColumnsDeclColumns(ImGuiMenuColumns columns, float w_icon, float w_label, float w_shortcut, float w_mark)
    {
        columns.Widths[0] = (ImU16)ImMath.ImMax((int)columns.Widths[0], (int)w_icon);
        columns.Widths[1] = (ImU16)ImMath.ImMax((int)columns.Widths[1], (int)w_label);
        columns.Widths[2] = (ImU16)ImMath.ImMax((int)columns.Widths[2], (int)w_shortcut);
        columns.Widths[3] = (ImU16)ImMath.ImMax((int)columns.Widths[3], (int)w_mark);
        MenuColumnsCalcNextTotalWidth(columns, false);
        return (float)Math.Max(columns.TotalWidth, columns.NextTotalWidth);
    }

    // ---- Window IDs (imgui.cpp) ----
    public static ImGuiID WindowGetID(ImGuiWindow window, string str, string str_end)
    {
        ImGuiID seed = window.IDStack.back();
        // C++ hashes bytes [str, str_end). ImHashStr's data_size is a UTF-8 byte count, so slice the
        // string instead of passing the char difference (wrong for non-ASCII text).
        ulong len = str_end != null ? (ulong)(str_end.Length - str.Length) : 0;
        ImGuiID id = len != 0 ? ImGuiHash.ImHashStr(str.Substring(0, (int)len), 0, seed) : ImGuiHash.ImHashStr(str, 0, seed);
        return id;
    }

    public static ImGuiID WindowGetIDFromPos(ImGuiWindow window, ImVec2 p_abs)
    {
        ImGuiID seed = window.IDStack.back();
        ImVec2 p_rel = new ImVec2(p_abs.x - window.Pos.x, p_abs.y - window.Pos.y);
        byte[] bytes = new byte[8];
        BitConverter.GetBytes(p_rel.x).CopyTo(bytes, 0);
        BitConverter.GetBytes(p_rel.y).CopyTo(bytes, 4);
        return ImGuiHash.ImHashData(bytes, 0, sizeof(float) * 2, seed);
    }

    public static ImGuiID WindowGetIDFromRectangle(ImGuiWindow window, ImRect r_abs)
    {
        ImGuiID seed = window.IDStack.back();
        ImRect r_rel = new ImRect(new ImVec2(r_abs.Min.x - window.Pos.x, r_abs.Min.y - window.Pos.y), new ImVec2(r_abs.Max.x - window.Pos.x, r_abs.Max.y - window.Pos.y));
        byte[] bytes = new byte[16];
        BitConverter.GetBytes(r_rel.Min.x).CopyTo(bytes, 0);
        BitConverter.GetBytes(r_rel.Min.y).CopyTo(bytes, 4);
        BitConverter.GetBytes(r_rel.Max.x).CopyTo(bytes, 8);
        BitConverter.GetBytes(r_rel.Max.y).CopyTo(bytes, 12);
        return ImGuiHash.ImHashData(bytes, 0, sizeof(float) * 4, seed);
    }

    // ---- List clipper (imgui.cpp) ----
    public static bool GetSkipItemForListClipping()
    {
        ImGuiContext g = ImGui.GImGui;
        return g.CurrentTable != null ? g.CurrentTable.HostSkipItems : g.CurrentWindow.SkipItems;
    }

    public static void ListClipperBegin(ImGuiListClipper clipper, int items_count, float items_height)
    {
        clipper.Ctx = ImGui.GetCurrentContext();

        ImGuiContext g = clipper.Ctx;
        ImGuiWindow window = g.CurrentWindow;

        if (g.CurrentTable != null && g.CurrentTable.IsInsideRow)
            ImGui.TableEndRow(g.CurrentTable);

        clipper.StartPosY = window.DC.CursorPos.y;
        clipper.ItemsHeight = items_height;
        clipper.ItemsCount = items_count;
        clipper.DisplayStart = -1;
        clipper.DisplayEnd = 0;

        // Acquire temporary buffer
        if (++g.ClipperTempDataStacked > g.ClipperTempData.Size)
            g.ClipperTempData.resize(g.ClipperTempDataStacked);
        if (g.ClipperTempData[g.ClipperTempDataStacked - 1] == null)
            g.ClipperTempData.Data[g.ClipperTempDataStacked - 1] = new ImGuiListClipperData();
        var data = g.ClipperTempData[g.ClipperTempDataStacked - 1];
        data.Reset(clipper);
        data.LossynessOffset = window.DC.CursorStartPosLossyness.y;
        clipper.TempData = data;
        clipper.StartSeekOffsetY = data.LossynessOffset;
    }

    public static void ListClipperEnd(ImGuiListClipper clipper)
    {
        var data = clipper.TempData;
        if (data != null)
        {
            ImGuiContext g = clipper.Ctx;
            if (clipper.ItemsCount >= 0 && clipper.ItemsCount < int.MaxValue && clipper.DisplayStart >= 0)
                ListClipperSeekCursorForItem(clipper, clipper.ItemsCount);

            System.Diagnostics.Debug.Assert(data.ListClipper == clipper);
            data.StepNo = data.Ranges.Size;
            if (--g.ClipperTempDataStacked > 0)
            {
                data = g.ClipperTempData[g.ClipperTempDataStacked - 1];
                data.ListClipper.TempData = data;
            }
            clipper.TempData = null;
        }
        clipper.DisplayStart = clipper.DisplayEnd = clipper.ItemsCount;
        clipper.ItemsCount = -1;
    }

    public static void ListClipperSeekCursorForItem(ImGuiListClipper clipper, int item_n)
    {
        float pos_y = (float)((double)clipper.StartPosY + clipper.StartSeekOffsetY + (double)item_n * clipper.ItemsHeight);
        ListClipperSeekCursorAndSetupPrevLine(clipper, pos_y, clipper.ItemsHeight);
    }

    public static void ListClipperSortAndFuseRanges(ImVector<ImGuiListClipperRange> ranges, int offset = 0)
    {
        if (ranges.Size - offset <= 1)
            return;

        for (int sort_end = ranges.Size - offset - 1; sort_end > 0; --sort_end)
            for (int i = offset; i < sort_end + offset; ++i)
                if (ranges[i].Min > ranges[i + 1].Min)
                    ImUtil.ImSwapRange(ref ranges.Data[i], ref ranges.Data[i + 1]);

        for (int i = 1 + offset; i < ranges.Size; i++)
        {
            if (ranges[i - 1].Max < ranges[i].Min)
                continue;
            ranges.Data[i - 1].Min = ImMath.ImMin(ranges[i - 1].Min, ranges[i].Min);
            ranges.Data[i - 1].Max = ImMath.ImMax(ranges[i - 1].Max, ranges[i].Max);
            ranges.erase(i);
            i--;
        }
    }

    public static void ListClipperSeekCursorAndSetupPrevLine(ImGuiListClipper clipper, float pos_y, float line_height)
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.CurrentWindow;
        float off_y = pos_y - window.DC.CursorPos.y;
        window.DC.CursorPos.y = pos_y;
        window.DC.CursorMaxPos.y = ImMath.ImMax(window.DC.CursorMaxPos.y, pos_y - g.Style.ItemSpacing.y);
        window.DC.CursorPosPrevLine.y = window.DC.CursorPos.y - line_height;
        window.DC.PrevLineSize.y = (line_height - g.Style.ItemSpacing.y);
        var columns = window.DC.CurrentColumns;
        if (columns != null)
            columns.LineMinY = window.DC.CursorPos.y;
        var table = g.CurrentTable;
        if (table != null)
        {
            if (table.IsInsideRow)
                ImGui.TableEndRow(table);
            int row_increase = (int)((off_y / line_height) + 0.5f);
            if (row_increase > 0 && (clipper.Flags & ImGuiListClipperFlags_NoSetTableRowCounters) == 0)
            {
                table.CurrentRow += row_increase;
                table.RowBgColorCounter += row_increase;
            }
            table.RowPosY2 = window.DC.CursorPos.y;
        }
    }

    static bool ListClipperStepInternal(ImGuiListClipper clipper)
    {
        ImGuiContext g = clipper.Ctx;
        ImGuiWindow window = g.CurrentWindow;
        var data = clipper.TempData;
        System.Diagnostics.Debug.Assert(data != null, "Called ImGuiListClipper::Step() too many times, or before ImGuiListClipper::Begin() ?");

        var table = g.CurrentTable;
        if (table != null && table.IsInsideRow)
            ImGui.TableEndRow(table);

        // No items
        if (clipper.ItemsCount == 0 || GetSkipItemForListClipping())
            return false;

        // While we are in frozen row state, keep displaying items one by one, unclipped
        if (data.StepNo == 0 && table != null && !table.IsUnfrozenRows)
        {
            clipper.DisplayStart = data.ItemsFrozen;
            clipper.DisplayEnd = ImMath.ImMin(data.ItemsFrozen + 1, clipper.ItemsCount);
            if (clipper.DisplayStart < clipper.DisplayEnd)
                data.ItemsFrozen++;
            return true;
        }

        // Step 0: Let you process the first element
        bool calc_clipping = false;
        if (data.StepNo == 0)
        {
            clipper.StartPosY = window.DC.CursorPos.y;
            if (clipper.ItemsHeight <= 0.0f)
            {
                data.Ranges.push_front(ImGuiListClipperRange.FromIndices(data.ItemsFrozen, data.ItemsFrozen + 1));
                clipper.DisplayStart = ImMath.ImMax(data.Ranges[0].Min, data.ItemsFrozen);
                clipper.DisplayEnd = ImMath.ImMin(data.Ranges[0].Max, clipper.ItemsCount);
                data.StepNo = 1;
                return true;
            }
            calc_clipping = true;
        }

        // Step 1: Let the clipper infer height from first range
        if (clipper.ItemsHeight <= 0.0f)
        {
            System.Diagnostics.Debug.Assert(data.StepNo == 1);
            bool affected_by_floating_point_precision = ImMath.ImIsFloatAboveGuaranteedIntegerPrecision((float)clipper.StartPosY) || ImMath.ImIsFloatAboveGuaranteedIntegerPrecision(window.DC.CursorPos.y);
            if (affected_by_floating_point_precision)
            {
                clipper.ItemsHeight = window.DC.PrevLineSize.y + g.Style.ItemSpacing.y;
                window.DC.CursorPos.y = (float)(clipper.StartPosY + clipper.ItemsHeight);
            }
            else
            {
                clipper.ItemsHeight = (float)(window.DC.CursorPos.y - clipper.StartPosY) / (float)(clipper.DisplayEnd - clipper.DisplayStart);
            }
            if (clipper.ItemsHeight == 0.0f && clipper.ItemsCount == int.MaxValue)
                return false;
            if (clipper.ItemsHeight <= 0.0f)
                return false;

            calc_clipping = true;
        }

        // Step 0 or 1: Calculate the actual ranges of visible elements.
        int already_submitted = clipper.DisplayEnd;
        if (calc_clipping)
        {
            clipper.StartSeekOffsetY = (double)data.LossynessOffset - data.ItemsFrozen * (double)clipper.ItemsHeight;

            if (g.LogEnabled)
            {
                data.Ranges.push_back(ImGuiListClipperRange.FromIndices(0, clipper.ItemsCount));
            }
            else
            {
                bool is_nav_request = (g.NavMoveScoringItems && g.NavWindow != null && g.NavWindow.RootWindowForNav == window.RootWindowForNav);
                int nav_off_min = (is_nav_request && g.NavMoveClipDir == ImGuiDir_Up) ? -1 : 0;
                int nav_off_max = (is_nav_request && g.NavMoveClipDir == ImGuiDir_Down) ? 1 : 0;
                if (is_nav_request)
                {
                    data.Ranges.push_back(ImGuiListClipperRange.FromPositions(g.NavScoringRect.Min.y, g.NavScoringRect.Max.y, nav_off_min, nav_off_max));
                    if (!g.NavScoringNoClipRect.IsInverted())
                        data.Ranges.push_back(ImGuiListClipperRange.FromPositions(g.NavScoringNoClipRect.Min.y, g.NavScoringNoClipRect.Max.y, nav_off_min, nav_off_max));
                }
                if (is_nav_request && (g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) != 0 && g.NavTabbingDir == -1)
                    data.Ranges.push_back(ImGuiListClipperRange.FromIndices(clipper.ItemsCount - 1, clipper.ItemsCount));

                ImRect nav_rect_abs = ImGui.WindowRectRelToAbs(window, window.NavRectRel[0]);
                if (g.NavId != 0 && window.NavLastIds[0] == g.NavId)
                    data.Ranges.push_back(ImGuiListClipperRange.FromPositions(nav_rect_abs.Min.y, nav_rect_abs.Max.y, 0, 0));

                float min_y = window.ClipRect.Min.y;
                float max_y = window.ClipRect.Max.y;

                var bs = g.BoxSelectState;
                if (bs.IsActive && bs.Window == window)
                {
                    float pad_y = g.Style.ItemSpacing.y;
                    min_y -= pad_y;
                    max_y += pad_y;
                    if (bs.UnclipMode)
                        data.Ranges.push_back(ImGuiListClipperRange.FromPositions(bs.UnclipRect.Min.y - pad_y, bs.UnclipRect.Max.y + pad_y, 0, 0));
                }

                data.Ranges.push_back(ImGuiListClipperRange.FromPositions(min_y, max_y, nav_off_min, nav_off_max));
            }

            // Convert position ranges to item index ranges
            for (int ri = 0; ri < data.Ranges.Size; ri++)
            {
                var range = data.Ranges[ri];
                if (range.PosToIndexConvert)
                {
                    int m1 = (int)(((double)range.Min - window.DC.CursorPos.y - data.LossynessOffset) / clipper.ItemsHeight);
                    int m2 = (int)((((double)range.Max - window.DC.CursorPos.y - data.LossynessOffset) / clipper.ItemsHeight) + 0.999999f);
                    range.Min = ImMath.ImClamp(already_submitted + m1 + range.PosToIndexOffsetMin, already_submitted, clipper.ItemsCount - 1);
                    range.Max = ImMath.ImClamp(already_submitted + m2 + range.PosToIndexOffsetMax, range.Min + 1, clipper.ItemsCount);
                    range.PosToIndexConvert = false;
                    data.Ranges.Data[ri] = range;
                }
            }
            ListClipperSortAndFuseRanges(data.Ranges, data.StepNo);
        }

        // Step 0+ (if item height is given in advance) or 1+: Display the next range in line.
        while (data.StepNo < data.Ranges.Size)
        {
            clipper.DisplayStart = ImMath.ImMax(data.Ranges[data.StepNo].Min, already_submitted);
            clipper.DisplayEnd = ImMath.ImMin(data.Ranges[data.StepNo].Max, clipper.ItemsCount);
            data.StepNo++;
            if (clipper.DisplayStart >= clipper.DisplayEnd)
                continue;
            if (clipper.DisplayStart > already_submitted)
                ListClipperSeekCursorForItem(clipper, clipper.DisplayStart);
            return true;
        }

        // After the last step: Advance the cursor to the end of the list and return false.
        if (clipper.ItemsCount < int.MaxValue)
            ListClipperSeekCursorForItem(clipper, clipper.ItemsCount);

        return false;
    }

    public static bool ListClipperStep(ImGuiListClipper clipper)
    {
        ImGuiContext g = clipper.Ctx;
        bool need_items_height = (clipper.ItemsHeight <= 0.0f);
        bool ret = ListClipperStepInternal(clipper);
        if (ret && (clipper.DisplayStart >= clipper.DisplayEnd))
            ret = false;
        if (ret == false)
            ListClipperEnd(clipper);
        return ret;
    }

    public static void ListClipperSeekOffsetForItem(ImGuiListClipper clipper, int item_index)
    {
        ListClipperSeekCursorForItem(clipper, item_index);
    }

    // ---- InputText callbacks/state (stb_textedit-based; minimal faithful subset) ----
    public static bool InputTextCallbackDataDeleteChars(ImGuiInputTextCallbackData data, int pos, int bytes_count)
    {
        if (bytes_count == 0 || pos + bytes_count > data.BufTextLen)
            return false;
        if (data.Buf != null)
        {
            Array.Copy(data.Buf, pos + bytes_count, data.Buf, pos, data.BufTextLen - (pos + bytes_count));
            Array.Clear(data.Buf, data.BufTextLen - bytes_count, bytes_count);
        }
        data.BufTextLen -= bytes_count;
        data.CursorPos = ImMath.ImClamp(data.CursorPos, 0, data.BufTextLen);
        data.SelectionStart = ImMath.ImClamp(data.SelectionStart, 0, data.CursorPos);
        data.SelectionEnd = ImMath.ImClamp(data.SelectionEnd, 0, data.CursorPos);
        data.BufDirty = true;
        return true;
    }

    public static bool InputTextCallbackDataInsertChars(ImGuiInputTextCallbackData data, int pos, string text, int text_end = -1)
    {
        int len = text_end >= 0 ? Math.Min(text_end, text.Length) : text.Length;
        if (len == 0)
            return false;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text.Substring(0, len));
        int insert_len = bytes.Length;
        if (data.BufTextLen + insert_len > data.BufSize)
            return false;
        if (data.Buf != null)
        {
            Array.Copy(data.Buf, pos, data.Buf, pos + insert_len, data.BufTextLen - pos);
            Array.Copy(bytes, 0, data.Buf, pos, insert_len);
            data.BufTextLen += insert_len;
            data.Buf[data.BufTextLen] = 0;
        }
        data.CursorPos = ImMath.ImClamp(data.CursorPos + insert_len, 0, data.BufTextLen);
        data.SelectionStart = data.SelectionEnd = data.CursorPos;
        data.BufDirty = true;
        return true;
    }

    // stb_textedit-backed state helpers. Full text editing behavior ports live in ImStbTextEdit.cs.
    public static void InputTextStateOnKeyPressed(ImGuiInputTextState state, int key) => ImStbTextEdit.StateOnKeyPressed(state, key); // C++ ImGuiInputTextState::OnKeyPressed()
    public static void InputTextStateOnCharPressed(ImGuiInputTextState state, uint c) => ImStbTextEdit.StbTexteditChar(state, c);
    public static float InputTextStateGetPreferredOffsetX(ImGuiInputTextState state) => ImStbTextEdit.StateGetPreferredOffsetX(state);
    public static void InputTextStateReloadUserBuf(ImGuiInputTextState state, int mode) => ImStbTextEdit.StateReloadUserBuf(state, mode);

    // C++ ImGuiViewport::GetDebugName() (docking branch)
    public static string ViewportGetDebugName(ImGuiViewport viewport)
    {
        var vp = viewport as ImGuiViewportP;
        return vp != null && vp.Window != null ? vp.Window.Name : "n/a";
    }
}
