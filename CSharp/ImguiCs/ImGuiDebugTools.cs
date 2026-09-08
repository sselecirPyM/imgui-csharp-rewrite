// Port of imgui.cpp [SECTION] DEBUG TOOLS:
// DebugLog/DebugLogV, ShowDebugLogWindow, DebugTextEncoding, DebugFlashStyleColor,
// item-locate/draw helpers. Item picker lives in ImGuiMiscHelpers.cs.
using System;
using System.Text;

namespace ImGui;

public static partial class ImGui
{
    // ---- Debug Log (imgui.cpp 18083-18120) ----

    // C++: void ImGui::DebugLog(const char* fmt, ...)
    public static void DebugLog(string fmt, params object[] args) => DebugLogV(fmt, args);

    // C++: void ImGui::DebugLogV(const char* fmt, va_list args)
    public static void DebugLogV(string fmt, object[] args)
    {
        ImGuiContext g = GImGui;
        int old_size = g.DebugLogBuf.size();
        if (g.ContextName.Length != 0)
            g.DebugLogBuf.appendf("[%s] [%05d] ", g.ContextName, g.FrameCount);
        else
            g.DebugLogBuf.appendf("[%05d] ", g.FrameCount);
        g.DebugLogBuf.appendfv(fmt, args);
        g.DebugLogIndex.append(g.DebugLogBuf.Buf.Data, old_size, g.DebugLogBuf.size());

        if ((g.DebugLogFlags & ImGuiDebugLogFlags_OutputToTTY) != 0)
            Console.Write(Encoding.UTF8.GetString(g.DebugLogBuf.Buf.Data, old_size, g.DebugLogBuf.size() - old_size));
        if ((g.DebugLogFlags & ImGuiDebugLogFlags_OutputToDebugger) != 0)
            System.Diagnostics.Debug.Write("[imgui] " + Encoding.UTF8.GetString(g.DebugLogBuf.Buf.Data, old_size, g.DebugLogBuf.size() - old_size));
        // IMGUI_ENABLE_TEST_ENGINE output is not applicable to the C# port.
    }

    // C++: static void SameLineOrWrap(const ImVec2& size)  (FIXME-LAYOUT)
    static void SameLineOrWrap(ImVec2 size)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImVec2 pos = new ImVec2(window.DC.CursorPosPrevLine.x + g.Style.ItemSpacing.x, window.DC.CursorPosPrevLine.y);
        if (window.WorkRect.Contains(new ImRect(pos, pos + size)))
            SameLine();
    }

    // C++: static void ShowDebugLogFlag(const char* name, ImGuiDebugLogFlags flags)
    static void ShowDebugLogFlag(string name, int flags)
    {
        ImGuiContext g = GImGui;
        ImVec2 size = new ImVec2(GetFrameHeight() + g.Style.ItemInnerSpacing.x + CalcTextSize(name).x, GetFrameHeight());
        SameLineOrWrap(size); // FIXME-LAYOUT: To be done automatically once we rework ItemSize/ItemAdd into ItemLayout.

        bool highlight_errors = (flags == ImGuiDebugLogFlags_EventError && g.DebugLogSkippedErrors > 0);
        if (highlight_errors)
            PushStyleColor(ImGuiCol_Text, ImMath.ImLerp(g.Style.Colors[ImGuiCol_Text], new ImVec4(1.0f, 0.0f, 0.0f, 1.0f), 0.30f));
        if (CheckboxFlags(name, ref g.DebugLogFlags, flags) && g.IO.KeyShift && (g.DebugLogFlags & flags) != 0)
        {
            g.DebugLogAutoDisableFrames = 2;
            g.DebugLogAutoDisableFlags |= flags;
        }
        if (highlight_errors)
        {
            PopStyleColor();
            SetItemTooltip("%d past errors skipped.", g.DebugLogSkippedErrors);
        }
        else
        {
            SetItemTooltip("Hold Shift when clicking to enable for 2 frames only (useful for spammy log entries)");
        }
    }

    // C++: void ImGui::ShowDebugLogWindow(bool* p_open)
    public static void ShowDebugLogWindow(ref bool p_open) => ShowDebugLogWindow(ref p_open, null);
    public static void ShowDebugLogWindow() => ShowDebugLogWindow(ref NullBoolRef._false, null);
    public static void ShowDebugLogWindow(string label) => ShowDebugLogWindow(ref NullBoolRef._false, label);
    static class NullBoolRef { public static bool _false; }

    public static void ShowDebugLogWindow(ref bool p_open, string label)
    {
        ImGuiContext g = GImGui;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSize) == 0)
            SetNextWindowSize(new ImVec2(0.0f, GetFontSize() * 12.0f), ImGuiCond_FirstUseEver);
        if (!Begin(label ?? "Dear ImGui Debug Log", ref p_open) || GetCurrentWindow().BeginCount > 1)
        {
            End();
            return;
        }

        int all_enable_flags = ImGuiDebugLogFlags_EventMask_ & ~ImGuiDebugLogFlags_EventInputRouting;
        CheckboxFlags("All", ref g.DebugLogFlags, all_enable_flags);
        SetItemTooltip("(except InputRouting which is spammy)");

        ShowDebugLogFlag("Errors", ImGuiDebugLogFlags_EventError);
        ShowDebugLogFlag("ActiveId", ImGuiDebugLogFlags_EventActiveId);
        ShowDebugLogFlag("Clipper", ImGuiDebugLogFlags_EventClipper);
        ShowDebugLogFlag("Focus", ImGuiDebugLogFlags_EventFocus);
        ShowDebugLogFlag("IO", ImGuiDebugLogFlags_EventIO);
        ShowDebugLogFlag("Font", ImGuiDebugLogFlags_EventFont);
        ShowDebugLogFlag("Nav", ImGuiDebugLogFlags_EventNav);
        ShowDebugLogFlag("Popup", ImGuiDebugLogFlags_EventPopup);
        ShowDebugLogFlag("Selection", ImGuiDebugLogFlags_EventSelection);
        ShowDebugLogFlag("Table", ImGuiDebugLogFlags_EventTable);
        ShowDebugLogFlag("InputRouting", ImGuiDebugLogFlags_EventInputRouting);

        if (SmallButton("Clear"))
        {
            g.DebugLogBuf.clear();
            g.DebugLogIndex.clear();
            g.DebugLogSkippedErrors = 0;
        }
        SameLine();
        if (SmallButton("Copy"))
            SetClipboardText(g.DebugLogBuf.c_str());
        SameLine();
        if (SmallButton("Configure Outputs.."))
            OpenPopup("Outputs");
        if (BeginPopup("Outputs"))
        {
            CheckboxFlags("OutputToTTY", ref g.DebugLogFlags, ImGuiDebugLogFlags_OutputToTTY);
            CheckboxFlags("OutputToDebugger", ref g.DebugLogFlags, ImGuiDebugLogFlags_OutputToDebugger);
            BeginDisabled(true); // IMGUI_ENABLE_TEST_ENGINE is not compiled in the C# port
            CheckboxFlags("OutputToTestEngine", ref g.DebugLogFlags, ImGuiDebugLogFlags_OutputToTestEngine);
            EndDisabled();
            EndPopup();
        }

        BeginChild("##log", new ImVec2(0.0f, 0.0f), ImGuiChildFlags_Borders, ImGuiWindowFlags_AlwaysVerticalScrollbar | ImGuiWindowFlags_AlwaysHorizontalScrollbar);

        int backup_log_flags = g.DebugLogFlags;
        g.DebugLogFlags &= ~ImGuiDebugLogFlags_EventClipper;

        var clipper = new ImGuiListClipper();
        clipper.Begin(g.DebugLogIndex.size());
        while (clipper.Step())
            for (int line_no = clipper.DisplayStart; line_no < clipper.DisplayEnd; line_no++)
                DebugTextUnformattedWithLocateItem(g.DebugLogBuf.Buf.Data, g.DebugLogIndex.get_line_begin(line_no), g.DebugLogIndex.get_line_end(line_no));
        g.DebugLogFlags = backup_log_flags;
        if (GetScrollY() >= GetScrollMaxY())
            SetScrollHereY(1.0f);
        EndChild();

        End();
    }

    // ---- Item locate helpers (imgui.cpp 18228-18330) ----

    // Display line, search for 0xXXXXXXXX identifiers and call DebugLocateItemOnHover() when hovered.
    // C++: void ImGui::DebugTextUnformattedWithLocateItem(const char* line_begin, const char* line_end)
    public static void DebugTextUnformattedWithLocateItem(byte[] base_buf, int line_begin, int line_end)
    {
        // Decode once; the scan for 0xXXXXXXXX ids happens in the string domain (matches rendering).
        string line = Encoding.UTF8.GetString(base_buf, line_begin, line_end - line_begin).TrimEnd('\0');
        TextUnformatted(line);
        if (!IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup | ImGuiHoveredFlags_AllowWhenBlockedByActiveItem))
            return;
        ImGuiContext g = GImGui;
        ImRect text_rect = g.LastItemData.Rect;
        for (int i = 0; i + 10 <= line.Length; i++)
        {
            if (line[i] != '0' || (line[i + 1] != 'x' && line[i + 1] != 'X'))
                continue;
            uint id = 0;
            bool all_hex = true;
            for (int j = 0; j < 8; j++)
            {
                char ch = line[i + 2 + j];
                int d = Uri.IsHexDigit(ch) ? (char.IsDigit(ch) ? ch - '0' : char.ToLowerInvariant(ch) - 'a' + 10) : -1;
                if (d < 0) { all_hex = false; break; }
                id = (id << 4) | (uint)d;
            }
            if (!all_hex || (i + 10 < line.Length && Uri.IsHexDigit(line[i + 10])))
                continue;
            ImVec2 p0 = CalcTextSize(line.Substring(0, i));
            ImVec2 p1 = CalcTextSize(line.Substring(i, 10));
            g.LastItemData.Rect = new ImRect(text_rect.Min + new ImVec2(p0.x, 0.0f), text_rect.Min + new ImVec2(p0.x + p1.x, p1.y));
            if (IsMouseHoveringRect(g.LastItemData.Rect.Min, g.LastItemData.Rect.Max, true))
                DebugLocateItemOnHover(id);
            i += 10;
        }
    }

    // Draw a small cross at current CursorPos in current window's DrawList
    // C++: void ImGui::DebugDrawCursorPos(ImU32 col)
    public static void DebugDrawCursorPos(ImU32 col)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImVec2 pos = window.DC.CursorPos;
        window.DrawList.AddLineV(pos.x, pos.y - 3.0f, pos.y + 4.0f, col, 1.0f);
        window.DrawList.AddLineH(pos.x - 3.0f, pos.x + 4.0f, pos.y, col, 1.0f);
    }

    // Draw a 10px wide rectangle around CursorPos.x using Line Y1/Y2 in current window's DrawList
    // C++: void ImGui::DebugDrawLineExtents(ImU32 col)
    public static void DebugDrawLineExtents(ImU32 col)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        float curr_x = window.DC.CursorPos.x;
        float line_y1 = (window.DC.IsSameLine ? window.DC.CursorPosPrevLine.y : window.DC.CursorPos.y);
        float line_y2 = line_y1 + (window.DC.IsSameLine ? window.DC.PrevLineSize.y : window.DC.CurrLineSize.y);
        window.DrawList.AddLineH(curr_x - 5.0f, curr_x + 5.0f, line_y1, col, 1.0f);
        window.DrawList.AddLineV(curr_x - 0.5f, line_y1, line_y2, col, 1.0f);
        window.DrawList.AddLineH(curr_x - 5.0f, curr_x + 5.0f, line_y2, col, 1.0f);
    }

    // Draw last item rect in ForegroundDrawList (so it is always visible)
    // C++: void ImGui::DebugDrawItemRect(ImU32 col)
    public static void DebugDrawItemRect(ImU32 col)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        GetForegroundDrawList(window).AddRect(g.LastItemData.Rect.Min, g.LastItemData.Rect.Max, col);
    }

    // [DEBUG] Locate item position/rectangle given an ID.
    static readonly ImU32 DEBUG_LOCATE_ITEM_COLOR = ImGuiCore.IM_COL32(0, 255, 0, 255);  // Green

    // C++: void ImGui::DebugLocateItem(ImGuiID target_id)
    public static void DebugLocateItem(ImGuiID target_id)
    {
        ImGuiContext g = GImGui;
        g.DebugLocateId = target_id;
        g.DebugLocateFrames = 2;
        g.DebugBreakInLocateId = false;
    }

    // FIXME: Doesn't work through a modal window, because they clear HoveredWindow.
    // C++: void ImGui::DebugLocateItemOnHover(ImGuiID target_id)
    public static void DebugLocateItemOnHover(ImGuiID target_id)
    {
        if (target_id == 0 || !IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem | ImGuiHoveredFlags_AllowWhenBlockedByPopup))
            return;
        ImGuiContext g = GImGui;
        DebugLocateItem(target_id);
        GetForegroundDrawList(g.CurrentWindow).AddRect(g.LastItemData.Rect.Min - new ImVec2(3.0f, 3.0f), g.LastItemData.Rect.Max + new ImVec2(3.0f, 3.0f), DEBUG_LOCATE_ITEM_COLOR);

        // Can't easily use a context menu here because it will mess with focus, active id etc.
        if (g.IO.ConfigDebugIsDebuggerPresent && g.MouseStationaryTimer > 1.0f)
        {
            DebugBreakButtonTooltip(false, "in ItemAdd()");
            if (IsKeyChordPressed(g.DebugBreakKeyChord))
                g.DebugBreakInLocateId = true;
        }
    }

    // C++: void ImGui::DebugLocateItemResolveWithLastItem()
    public static void DebugLocateItemResolveWithLastItem()
    {
        ImGuiContext g = GImGui;

        // [DEBUG] Debug break requested by user
        if (g.DebugBreakInLocateId)
            System.Diagnostics.Debugger.Break();

        ImGuiLastItemData item_data = g.LastItemData;
        g.DebugLocateId = 0;
        ImDrawList draw_list = GetForegroundDrawList(g.CurrentWindow);
        ImRect r = item_data.Rect;
        r.Expand(3.0f);
        ImVec2 p1 = g.IO.MousePos;
        ImVec2 p2 = new ImVec2((p1.x < r.Min.x) ? r.Min.x : (p1.x > r.Max.x) ? r.Max.x : p1.x, (p1.y < r.Min.y) ? r.Min.y : (p1.y > r.Max.y) ? r.Max.y : p1.y);
        draw_list.AddRect(r.Min, r.Max, DEBUG_LOCATE_ITEM_COLOR);
        draw_list.AddLine(p1, p2, DEBUG_LOCATE_ITEM_COLOR);
    }

    // ---- Debug Text Encoding (imgui.cpp 16563-16599) ----

    // C++: void ImGui::DebugTextEncoding(const char* str)
    public static void DebugTextEncoding(string str)
    {
        Text("Text: \"%s\"", str);
        if (!BeginTable("##DebugTextEncoding", 4, ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_Resizable))
            return;
        TableSetupColumn("Offset");
        TableSetupColumn("UTF-8");
        TableSetupColumn("Glyph");
        TableSetupColumn("Codepoint");
        TableHeadersRow();
        // As we may receive malformed UTF-8, pass an explicit end instead of relying on ImTextCharFromUtf8() assuming enough space.
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        int str_end = bytes.Length;
        int p = 0;
        while (p < str_end && bytes[p] != 0)
        {
            int c_utf8_len = ImText.ImTextCharFromUtf8(out uint c, bytes, p, str_end);
            TableNextColumn();
            Text("%d", p);
            TableNextColumn();
            for (int byte_index = 0; byte_index < c_utf8_len; byte_index++)
            {
                if (byte_index > 0)
                    SameLine();
                Text("0x%02X", bytes[p + byte_index]);
            }
            TableNextColumn();
            TextUnformatted(Encoding.UTF8.GetString(bytes, p, c_utf8_len));
            if (!GetFont().IsGlyphInFont((ushort)c))
            {
                SameLine();
                TextUnformatted("[missing]");
            }
            TableNextColumn();
            Text("U+%04X", c);
            p += c_utf8_len;
        }
        EndTable();
    }

    // ---- Flash style color (imgui.cpp 16426-16447, 16601-16628) ----

    // C++: static void DebugFlashStyleColorStop()
    static void DebugFlashStyleColorStop()
    {
        ImGuiContext g = GImGui;
        if (g.DebugFlashStyleColorIdx != ImGuiCol_COUNT)
            g.Style.Colors[g.DebugFlashStyleColorIdx] = g.DebugFlashStyleColorBackup;
        g.DebugFlashStyleColorIdx = ImGuiCol_COUNT;
    }

    // Flash a given style color for some time + inhibit modifications of this color via PushStyleColor() calls.
    // C++: void ImGui::DebugFlashStyleColor(ImGuiCol idx)
    public static void DebugFlashStyleColor(int idx)
    {
        ImGuiContext g = GImGui;
        DebugFlashStyleColorStop();
        g.DebugFlashStyleColorTime = 0.5f;
        g.DebugFlashStyleColorIdx = idx;
        g.DebugFlashStyleColorBackup = g.Style.Colors[idx];
    }

    // C++: void ImGui::UpdateDebugToolFlashStyleColor()
    public static void UpdateDebugToolFlashStyleColor()
    {
        ImGuiContext g = GImGui;
        if (g.DebugFlashStyleColorTime <= 0.0f)
            return;
        ColorConvertHSVtoRGB(ImMath.ImCos(g.DebugFlashStyleColorTime * 6.0f) * 0.5f + 0.5f, 0.5f, 0.5f,
            out g.Style.Colors[g.DebugFlashStyleColorIdx].x, out g.Style.Colors[g.DebugFlashStyleColorIdx].y, out g.Style.Colors[g.DebugFlashStyleColorIdx].z);
        g.Style.Colors[g.DebugFlashStyleColorIdx].w = 1.0f;
        if ((g.DebugFlashStyleColorTime -= g.IO.DeltaTime) <= 0.0f)
            DebugFlashStyleColorStop();
    }

    // C++: ImU64 ImGui::DebugTextureIDToU64(ImTextureID tex_id)
    public static ImU64 DebugTextureIDToU64(ImTextureID tex_id) => tex_id;

    // ---- Debug break helpers (imgui.cpp 17463-17520) ----

    // C++: void ImGui::DebugBreakClearData()
    public static void DebugBreakClearData()
    {
        // Those fields are scattered in their respective subsystem to stay in hot-data locations
        ImGuiContext g = GImGui;
        g.DebugBreakInWindow = 0;
        g.DebugBreakInTable = 0;
        g.DebugBreakInShortcutRouting = ImGuiKey_None;
    }

    // C++: void ImGui::DebugBreakButtonTooltip(bool keyboard_only, const char* description_of_location)
    public static void DebugBreakButtonTooltip(bool keyboard_only, string description_of_location)
    {
        if (!BeginItemTooltip())
            return;
        Text("To call IM_DEBUG_BREAK() %s:", description_of_location);
        Separator();
        TextUnformatted(keyboard_only ? "- Press 'Pause/Break' on keyboard." : "- Press 'Pause/Break' on keyboard.\n- or Click (may alter focus/active id).\n- or navigate using keyboard and press space.");
        Separator();
        TextUnformatted("Choose one way that doesn't interfere with what you are trying to debug!\nYou need a debugger attached or this will crash!");
        EndTooltip();
    }

    // Special button that doesn't take focus, doesn't take input owner, and can be activated without a click etc.
    // In order to reduce interferences with the contents we are trying to debug into.
    // C++: bool ImGui::DebugBreakButton(const char* label, const char* description_of_location)
    public static bool DebugBreakButton(string label, string description_of_location)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiID id = window.GetID(label);
        ImVec2 label_size = CalcTextSize(label, -1, true);
        ImVec2 pos = window.DC.CursorPos + new ImVec2(0.0f, window.DC.CurrLineTextBaseOffset);
        ImVec2 size = new ImVec2(label_size.x + g.Style.FramePadding.x * 2.0f, label_size.y);

        ImRect bb = new ImRect(pos, pos + size);
        ItemSize(size, 0.0f);
        if (!ItemAdd(bb, id))
            return false;

        // WE DO NOT USE ButtonEx() or ButtonBehavior() in order to reduce our side-effects.
        bool hovered = ItemHoverable(bb, id, g.CurrentItemFlags);
        bool pressed = hovered && (IsKeyChordPressed(g.DebugBreakKeyChord) || IsMouseClicked(0) || g.NavActivateId == id);
        DebugBreakButtonTooltip(false, description_of_location);

        ImVec4 col4f = GetStyleColorVec4(hovered ? ImGuiCol_ButtonHovered : ImGuiCol_Button);
        ColorConvertRGBtoHSV(col4f.x, col4f.y, col4f.z, out float hsv_x, out float hsv_y, out float hsv_z);
        ColorConvertHSVtoRGB(hsv_x + 0.20f, hsv_y, hsv_z, out col4f.x, out col4f.y, out col4f.z);

        RenderNavCursor(bb, id);
        RenderFrame(bb.Min, bb.Max, GetColorU32(col4f), true, g.Style.FrameRounding);
        RenderTextClipped(bb.Min, bb.Max, label, -1, label_size, g.Style.ButtonTextAlign, bb);

        return pressed;
    }

    // ---- ID Stack Tool / item path query (imgui.cpp 18365-18590) ----

    // Update queries. The steps are: -1: query Stack, >= 0: query each stack item
    // We can only perform 1 ID Info query every frame. This is designed so the GetID() tests are cheap and constant-time
    // C++: static ImGuiID DebugItemPathQuery_UpdateAndGetHookId(ImGuiDebugItemPathQuery* query, ImGuiID id)
    static ImGuiID DebugItemPathQuery_UpdateAndGetHookId(ImGuiDebugItemPathQuery query, ImGuiID id)
    {
        // Update query. Clear hook when no active query
        if (query.MainID != id)
        {
            query.MainID = id;
            query.Step = -1;
            query.Complete = false;
            query.Results.resize(0);
            query.ResultsDescBuf.clear();
        }
        query.Active = false;
        if (id == 0)
            return 0;

        // Advance to next stack level when we got our result, or after 2 frames (in case we never get a result)
        if (query.Step >= 0 && query.Step < query.Results.Size)
            if (query.Results[query.Step].QuerySuccess || query.Results[query.Step].QueryFrameCount > 2)
                query.Step++;

        // Update status and hook
        query.Complete = (query.Step == query.Results.Size);
        if (query.Step == -1)
        {
            query.Active = true;
            return id;
        }
        else if (query.Step >= 0 && query.Step < query.Results.Size)
        {
            ImGuiStackLevelInfo step_info = query.Results[query.Step];
            step_info.QueryFrameCount++;
            query.Results[query.Step] = step_info;
            query.Active = true;
            return step_info.ID;
        }
        return 0;
    }

    // [DEBUG] ID Stack Tool: update query. Called by NewFrame()
    // C++: void ImGui::UpdateDebugToolItemPathQuery()
    public static void UpdateDebugToolItemPathQuery()
    {
        ImGuiContext g = GImGui;
        ImGuiID id = 0;
        if (g.DebugIDStackTool.LastActiveFrame + 1 == g.FrameCount)
            id = g.HoveredIdPreviousFrame != 0 ? g.HoveredIdPreviousFrame : g.ActiveId;
        g.DebugHookIdInfoId = DebugItemPathQuery_UpdateAndGetHookId(g.DebugItemPathQuery, id);
    }

    // [DEBUG] ID Stack tool: hooks called by GetID() family functions
    // C++: void ImGui::DebugHookIdInfo(ImGuiID id, ImGuiDataType data_type, const void* data_id, const void* data_id_end)
    public static void DebugHookIdInfo(ImGuiID id, int data_type, object data_id, object data_id_end)
    {
        ImGuiContext g = GImGui;
        ImGuiDebugItemPathQuery query = g.DebugItemPathQuery;
        if (query.Active == false)
        {
            System.Diagnostics.Debug.Assert(id == 0);
            return;
        }
        ImGuiWindow window = g.CurrentWindow;

        // Step -1: stack query
        // This assumes that the ID was computed with the current ID stack, which tends to be the case for our widget.
        if (query.Step == -1)
        {
            System.Diagnostics.Debug.Assert(query.Results.Size == 0);
            query.Step++;
            query.Results.resize(window.IDStack.Size + 1, new ImGuiStackLevelInfo());
            for (int n = 0; n < window.IDStack.Size + 1; n++)
            {
                var level = query.Results[n];
                level.ID = (n < window.IDStack.Size) ? window.IDStack[n] : id;
                query.Results[n] = level;
            }
            return;
        }

        // Step 0+: query for individual level
        System.Diagnostics.Debug.Assert(query.Step >= 0);
        if (query.Step != window.IDStack.Size)
            return;
        ImGuiStackLevelInfo info = query.Results[query.Step];
        System.Diagnostics.Debug.Assert(info.ID == id && info.QueryFrameCount > 0);

        if (info.DescOffset == -1)
        {
            string result;
            switch (data_type)
            {
                case ImGuiDataType_S32:
                    result = ImFmt.Sprintf("%d", data_id);
                    break;
                case ImGuiDataType_String:
                    result = (string)data_id ?? "";
                    break;
                case ImGuiDataType_Pointer:
                    result = ImFmt.Sprintf("(void*)0x%p", data_id);
                    break;
                case ImGuiDataType_ID:
                    // PushOverrideID() is often used to avoid hashing twice, which would lead to 2 calls to DebugHookIdInfo(). We prioritize the first one.
                    result = ImFmt.Sprintf("0x%08X [override]", id);
                    break;
                default:
                    result = "???";
                    break;
            }
            info.DescOffset = query.ResultsDescBuf.size();
            query.ResultsDescBuf.append(result, result.Length + 1); // Include zero terminator
        }
        info.QuerySuccess = true;
        if (info.DataType == -1)
            info.DataType = (sbyte)data_type;
        query.Results[query.Step] = info;
    }

    // C++: static int DebugItemPathQuery_FormatLevelInfo(query, n, bool format_for_ui, char* buf, size_t buf_size)
    static string DebugItemPathQuery_FormatLevelInfo(ImGuiDebugItemPathQuery query, int n, bool format_for_ui)
    {
        ImGuiStackLevelInfo info = query.Results[n];
        ImGuiWindow window = (info.DescOffset == -1 && n == 0) ? FindWindowByID(info.ID) : null;
        if (window != null)                         // Source: window name (because the root ID don't call GetID() and so doesn't get hooked)
            return format_for_ui ? $"\"{ImGuiHash.ImHashSkipUncontributingPrefix(window.Name)}\" [window]" : ImGuiHash.ImHashSkipUncontributingPrefix(window.Name);
        if (info.QuerySuccess)                      // Source: GetID() hooks (prioritize over ItemInfo() because we frequently use patterns like: PushID(str), Button("") where they both have same id)
        {
            string desc = TextBufferGetCStr(query.ResultsDescBuf, info.DescOffset);
            string text = ImGuiHash.ImHashSkipUncontributingPrefix(desc);
            return (format_for_ui && info.DataType == ImGuiDataType_String) ? $"\"{text}\"" : text;
        }
        if (query.Step < query.Results.Size)        // Only start using fallback below when all queries are done, so during queries we don't flickering ??? markers.
            return "";
        return "???";
    }

    // Read a zero-terminated string stored at 'offset' in a ImGuiTextBuffer.
    static string TextBufferGetCStr(ImGuiTextBuffer buf, int offset)
    {
        int end = offset;
        while (end < buf.Buf.Size && buf.Buf.Data[end] != 0)
            end++;
        return Encoding.UTF8.GetString(buf.Buf.Data, offset, end - offset);
    }

    // C++: static const char* DebugItemPathQuery_GetResultAsPath(ImGuiDebugItemPathQuery* query, bool hex_encode_non_ascii_chars)
    static string DebugItemPathQuery_GetResultAsPath(ImGuiDebugItemPathQuery query, bool hex_encode_non_ascii_chars)
    {
        ImGuiTextBuffer buf = query.ResultPathBuf;
        buf.clear();
        for (int stack_n = 0; stack_n < query.Results.Size; stack_n++)
        {
            string level_desc = DebugItemPathQuery_FormatLevelInfo(query, stack_n, false);
            buf.append(stack_n == 0 ? "//" : "/");
            foreach (char ch in level_desc)
            {
                if (ch == '/')
                    buf.append("\\");
                string ch_str = ch.ToString();
                if (ch < 256 || !hex_encode_non_ascii_chars)
                {
                    buf.append(ch_str);
                }
                else
                {
                    byte[] utf8 = Encoding.UTF8.GetBytes(ch_str);
                    foreach (byte b in utf8)
                        buf.appendf("\\x%02x", b);
                }
            }
        }
        return buf.c_str();
    }

    // ID Stack Tool: Display UI
    // C++: void ImGui::ShowIDStackToolWindow(bool* p_open)
    public static void ShowIDStackToolWindow(ref bool p_open) => ShowIDStackToolWindow(ref p_open, null);
    public static void ShowIDStackToolWindow() => ShowIDStackToolWindow(ref _idStackP_openDummy, null);
    static bool _idStackP_openDummy;
    public static void ShowIDStackToolWindow(string label) => ShowIDStackToolWindow(ref _idStackP_openDummy, label);

    public static void ShowIDStackToolWindow(ref bool p_open, string label)
    {
        ImGuiContext g = GImGui;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSize) == 0)
            SetNextWindowSize(new ImVec2(0.0f, GetFontSize() * 8.0f), ImGuiCond_FirstUseEver);
        if (!Begin(label ?? "Dear ImGui ID Stack Tool", ref p_open) || GetCurrentWindow().BeginCount > 1)
        {
            End();
            return;
        }

        ImGuiDebugItemPathQuery query = g.DebugItemPathQuery;
        ImGuiIDStackTool tool = g.DebugIDStackTool;
        tool.LastActiveFrame = g.FrameCount;
        string result_path = DebugItemPathQuery_GetResultAsPath(query, tool.OptHexEncodeNonAsciiChars);
        Text("0x%08X", query.MainID);
        SameLine();
        MetricsHelpMarker("Hover an item with the mouse to display elements of the ID Stack leading to the item's final ID.\nEach level of the stack correspond to a PushID() call.\nAll levels of the stack are hashed together to make the final ID of a widget (ID displayed at the bottom level of the stack).\nRead FAQ entry about the ID stack for details.");

        // Ctrl+C to copy path
        float time_since_copy = (float)g.Time - tool.CopyToClipboardLastTime;
        PushStyleVarY(ImGuiStyleVar_FramePadding, 0.0f);
        Checkbox("Hex-encode non-ASCII", ref tool.OptHexEncodeNonAsciiChars);
        SameLine();
        Checkbox("Ctrl+C: copy path", ref tool.OptCopyToClipboardOnCtrlC);
        PopStyleVar();
        SameLine();
        TextColored((time_since_copy >= 0.0f && time_since_copy < 0.75f && ImMath.ImFmod(time_since_copy, 0.25f) < 0.25f * 0.5f) ? new ImVec4(1.0f, 1.0f, 0.3f, 1.0f) : new ImVec4(), "*COPIED*");
        if (tool.OptCopyToClipboardOnCtrlC && Shortcut(ImGuiMod_Ctrl | ImGuiKey_C, ImGuiInputFlags_RouteGlobal | ImGuiInputFlags_RouteOverFocused))
        {
            tool.CopyToClipboardLastTime = (float)g.Time;
            SetClipboardText(result_path);
        }

        Text("- Path \"%s\"", query.Complete ? result_path : "");
        Separator();

        // Display decorated stack
        if (query.Results.Size > 0 && BeginTable("##table", 3, ImGuiTableFlags_Borders))
        {
            float id_width = CalcTextSize("0xDDDDDDDD").x;
            TableSetupColumn("Seed", ImGuiTableColumnFlags_WidthFixed, id_width);
            TableSetupColumn("PushID", ImGuiTableColumnFlags_WidthStretch);
            TableSetupColumn("Result", ImGuiTableColumnFlags_WidthFixed, id_width);
            TableHeadersRow();
            for (int n = 0; n < query.Results.Size; n++)
            {
                ImGuiStackLevelInfo info = query.Results[n];
                TableNextColumn();
                Text("0x%08X", (n > 0) ? query.Results[n - 1].ID : 0);
                TableNextColumn();
                TextUnformatted(DebugItemPathQuery_FormatLevelInfo(query, n, true));
                TableNextColumn();
                Text("0x%08X", info.ID);
                if (n == query.Results.Size - 1)
                    TableSetBgColor(ImGuiTableBgTarget_CellBg, GetColorU32(ImGuiCol_Header));
            }
            EndTable();
        }
        End();
    }
}
