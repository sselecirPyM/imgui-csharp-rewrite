// Port of Dear ImGui (1.93.0 WIP, IMGUI_VERSION_NUM 19295) imgui_widgets.cpp lines 7834-9793:
// - [SECTION] Widgets: Box-Select support
// - [SECTION] Widgets: Multi-Select support
// - [SECTION] Widgets: Multi-Select helpers (ImGuiSelectionBasicStorage / ImGuiSelectionExternalStorage)
// - [SECTION] Widgets: ListBox
// - [SECTION] Widgets: PlotLines, PlotHistogram
// - [SECTION] MenuItem, BeginMenu, EndMenu, etc.
// (ImGuiMenuColumns::Update/CalcNextTotalWidth/DeclColumns were already ported in ImGuiUtil.cs and are reused here.)
namespace ImGui;

using System;
using System.Diagnostics;

public static partial class ImGui
{
    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Box-Select support
    // This has been extracted away from Multi-Select logic in the hope that it could eventually be used elsewhere, but hasn't been yet.
    //-------------------------------------------------------------------------
    // Extra logic in MultiSelectItemFooter() and ImGuiListClipper::Step()
    //-------------------------------------------------------------------------
    // - BoxSelectPreStartDrag() [Internal]
    // - BoxSelectActivateDrag() [Internal]
    // - BoxSelectDeactivateDrag() [Internal]
    // - BoxSelectScrollWithMouseDrag() [Internal]
    // - BeginBoxSelect() [Internal]
    // - EndBoxSelect() [Internal]
    //-------------------------------------------------------------------------

    // C++ imgui_internal.h inline: ImGuiBoxSelectState* ImGui::GetBoxSelectState(ImGuiID id)
    public static ImGuiBoxSelectState GetBoxSelectState(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        return (id != 0 && g.BoxSelectState.ID == id && g.BoxSelectState.IsActive) ? g.BoxSelectState : null;
    }

    // Call on the initial click.
    // C++: static void BoxSelectPreStartDrag(ImGuiID id, ImGuiSelectionUserData clicked_item)   [imgui_widgets.cpp 7852]
    public static void BoxSelectPreStartDrag(ImGuiID id, ImGuiSelectionUserData clicked_item)
    {
        ImGuiContext g = GImGui;
        ImGuiBoxSelectState bs = g.BoxSelectState;
        bs.ID = id;
        bs.IsStarting = true; // Consider starting box-select.
        bs.IsStartedFromVoid = (clicked_item == ImGuiSelectionUserData_Invalid);
        bs.IsStartedSetNavIdOnce = bs.IsStartedFromVoid;
        bs.KeyMods = (ushort)g.IO.KeyMods;
        bs.StartPosRel = bs.EndPosRel = WindowPosAbsToRel(g.CurrentWindow, g.IO.MousePos);
        bs.ScrollAccum = new ImVec2(0.0f, 0.0f);
    }

    // C++: static void BoxSelectActivateDrag(ImGuiBoxSelectState* bs, ImGuiWindow* window)   [imgui_widgets.cpp 7864]
    public static void BoxSelectActivateDrag(ImGuiBoxSelectState bs, ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        // (IMGUI_DEBUG_LOG_SELECTION omitted)
        bs.IsActive = true;
        bs.Window = window;
        bs.IsStarting = false;
        SetActiveID(bs.ID, window);
        SetActiveIdUsingAllKeyboardKeys();
        if (bs.IsStartedFromVoid && (bs.KeyMods & (ImGuiMod_Ctrl | ImGuiMod_Shift)) == 0)
            bs.RequestClear = true;
    }

    // C++: static void BoxSelectDeactivateDrag(ImGuiBoxSelectState* bs)   [imgui_widgets.cpp 7875]
    public static void BoxSelectDeactivateDrag(ImGuiBoxSelectState bs)
    {
        ImGuiContext g = GImGui;
        bs.IsActive = bs.IsStarting = false;
        if (g.ActiveId == bs.ID)
        {
            // (IMGUI_DEBUG_LOG_SELECTION omitted)
            ClearActiveID();
        }
        bs.ID = 0;
    }

    // C++: static void BoxSelectScrollWithMouseDrag(ImGuiBoxSelectState* bs, ImGuiWindow* window, const ImRect& inner_r)   [imgui_widgets.cpp 7885]
    public static void BoxSelectScrollWithMouseDrag(ImGuiBoxSelectState bs, ImGuiWindow window, ImRect inner_r)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(bs.Window == window);
        for (int n = 0; n < 2; n++) // each axis
        {
            float mouse_pos = g.IO.MousePos[n];
            float dist = (mouse_pos > inner_r.Max[n]) ? mouse_pos - inner_r.Max[n] : (mouse_pos < inner_r.Min[n]) ? mouse_pos - inner_r.Min[n] : 0.0f;
            float scroll_curr = window.Scroll[n];
            if (dist == 0.0f || (dist < 0.0f && scroll_curr < 0.0f) || (dist > 0.0f && scroll_curr >= window.ScrollMax[n]))
                continue;

            float speed_multiplier = ImMath.ImLinearRemapClamp(g.FontSize, g.FontSize * 5.0f, 1.0f, 4.0f, MathF.Abs(dist)); // x1 to x4 depending on distance (C++ ImAbs)
            float scroll_step = g.FontSize * 35.0f * speed_multiplier * (dist < 0.0f ? -1.0f : (dist > 0.0f ? 1.0f : 0.0f)) * g.IO.DeltaTime; // C++ ImSign(dist)
            bs.ScrollAccum[n] += scroll_step;

            // Accumulate into a stored value so we can handle high-framerate
            float scroll_step_i = ImMath.ImFloor(bs.ScrollAccum[n]);
            if (scroll_step_i == 0.0f)
                continue;
            if (n == 0)
                SetScrollX(window, scroll_curr + scroll_step_i);
            else
                SetScrollY(window, scroll_curr + scroll_step_i);
            bs.ScrollAccum[n] -= scroll_step_i;
        }
    }

    // C++: bool ImGui::BeginBoxSelect(const ImRect& scope_rect, ImGuiWindow* window, ImGuiID box_select_id, ImGuiMultiSelectFlags ms_flags)   [imgui_widgets.cpp 7910]
    public static bool BeginBoxSelect(ImRect scope_rect, ImGuiWindow window, ImGuiID box_select_id, ImGuiMultiSelectFlags ms_flags)
    {
        ImGuiContext g = GImGui;
        ImGuiBoxSelectState bs = g.BoxSelectState;
        KeepAliveID(box_select_id);
        if (bs.ID != box_select_id)
            return false;

        // IsStarting is set by MultiSelectItemFooter() when considering a possible box-select. We validate it here and lock geometry.
        bs.UnclipMode = false;
        bs.RequestClear = false;
        if (bs.IsStarting && IsMouseDragPastThreshold(0))
            BoxSelectActivateDrag(bs, window);
        else if ((bs.IsStarting || bs.IsActive) && g.IO.MouseDown[0] == false)
            BoxSelectDeactivateDrag(bs);
        if (!bs.IsActive)
            return false;

        // Current frame absolute prev/current rectangles are used to toggle selection.
        // They are derived from positions relative to scrolling space, so "previous" rectangle is reprojected for current frame coordinates.
        ImVec2 start_pos_abs = WindowPosRelToAbs(window, bs.StartPosRel);
        ImVec2 prev_end_pos_abs = WindowPosRelToAbs(window, bs.EndPosRel); // Clamped already
        ImVec2 curr_end_pos_abs = g.IO.MousePos;
        if ((ms_flags & ImGuiMultiSelectFlags_ScopeWindow) != 0) // Box-select scrolling only happens with ScopeWindow
            curr_end_pos_abs = ImMath.ImClamp(curr_end_pos_abs, scope_rect.Min, scope_rect.Max);
        bs.BoxSelectRectPrev.Min = ImMath.ImMin(start_pos_abs, prev_end_pos_abs);
        bs.BoxSelectRectPrev.Max = ImMath.ImMax(start_pos_abs, prev_end_pos_abs);
        bs.BoxSelectRectCurr.Min = ImMath.ImMin(start_pos_abs, curr_end_pos_abs);
        bs.BoxSelectRectCurr.Max = ImMath.ImMax(start_pos_abs, curr_end_pos_abs);
        //IMGUI_DEBUG_LOG("StartPosRel (%.2f,%.2f) EndPosRel (%.2f,%.2f) -> (%.2f,%.2f)\n", ...); // omitted

        // Box-select 2D mode detects change of the rectangle.
        // Storing unclip rects which will be tested by widgets supporting box-select. Always update rectangles when active (even if we don't use them).
        // To facilitate understanding this: enable IMGUI_DEBUG_BOXSELECT and visualize all geometry.
        if ((ms_flags & (ImGuiMultiSelectFlags_BoxSelect1d | ImGuiMultiSelectFlags_BoxSelect2d)) != 0)
        {
            // For both sides, compute the area differing between Prev and Curr rectangles.
            bs.UnclipRects[0] = bs.UnclipRects[1] = new ImRect(float.MaxValue, float.MaxValue, -float.MaxValue, -float.MaxValue);
            for (int side = 0; side < 2; side++)
            {
                ImVec2 d_min = (side == 0) ? ImMath.ImMin(bs.BoxSelectRectCurr.Min, bs.BoxSelectRectPrev.Min) : ImMath.ImMin(bs.BoxSelectRectCurr.Max, bs.BoxSelectRectPrev.Max);
                ImVec2 d_max = (side == 0) ? ImMath.ImMax(bs.BoxSelectRectCurr.Min, bs.BoxSelectRectPrev.Min) : ImMath.ImMax(bs.BoxSelectRectCurr.Max, bs.BoxSelectRectPrev.Max);
                if (d_min.x != d_max.x)
                {
                    bs.UnclipRects[0].AddX(d_min.x);
                    bs.UnclipRects[0].AddX(d_max.x);
                }
                if (d_min.y != d_max.y)
                {
                    bs.UnclipRects[1].AddY(d_min.y);
                    bs.UnclipRects[1].AddY(d_max.y);
                }
            }

            ImRect box_select_intersection = bs.BoxSelectRectPrev;
            box_select_intersection.Add(bs.BoxSelectRectCurr);
            if ((ms_flags & ImGuiMultiSelectFlags_BoxSelect2d) != 0)
                if (bs.BoxSelectRectPrev.Min.x != bs.BoxSelectRectCurr.Min.x || bs.BoxSelectRectPrev.Max.x != bs.BoxSelectRectCurr.Max.x)
                {
                    bs.UnclipRects[0].AddY(box_select_intersection.Min.y);
                    bs.UnclipRects[0].AddY(box_select_intersection.Max.y);
                }
            if ((ms_flags & (ImGuiMultiSelectFlags_BoxSelect1d | ImGuiMultiSelectFlags_BoxSelect2d)) != 0)
                if (bs.BoxSelectRectPrev.Min.y != bs.BoxSelectRectCurr.Min.y || bs.BoxSelectRectPrev.Max.y != bs.BoxSelectRectCurr.Max.y)
                {
                    bs.UnclipRects[1].AddX(box_select_intersection.Min.x);
                    bs.UnclipRects[1].AddX(box_select_intersection.Max.x);
                }

            // Merge both rectangles into one.
            // FIXME-OPT: When UnclipRect.Area() is much larger than the sum of UnclipRects[0]/[1] Areas, widgets should
            // ideally first use UnclipRect as a first coarse cull layer + the individual ones as a second validation.
            bs.UnclipRect = bs.UnclipRects[0];
            bs.UnclipRect.Add(bs.UnclipRects[1]);
            if (!bs.UnclipRect.IsInverted() && (!window.ClipRect.Contains(bs.UnclipRect.Min) || !window.ClipRect.Contains(bs.UnclipRect.Max))) // !! Don't use Contains(ImRect)
                bs.UnclipMode = true;
            if (bs.UnclipMode && g.CurrentTable != null)
                TableApplyExternalUnclipRect(g.CurrentTable, bs.UnclipRect); // No need submitting both
        }

        // (IMGUI_DEBUG_BOXSELECT debug visualization omitted)
        return true;
    }

    // C++: void ImGui::EndBoxSelect(const ImRect& scope_rect, ImGuiMultiSelectFlags ms_flags)   [imgui_widgets.cpp 7999]
    public static void EndBoxSelect(ImRect scope_rect, ImGuiMultiSelectFlags ms_flags)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiBoxSelectState bs = g.BoxSelectState;
        Debug.Assert(bs.IsActive);
        bs.UnclipMode = false;

        // Render selection rectangle
        bs.EndPosRel = WindowPosAbsToRel(window, ImMath.ImClamp(g.IO.MousePos, scope_rect.Min, scope_rect.Max)); // Clamp stored position according to current scrolling view
        ImRect box_select_r = bs.BoxSelectRectCurr;
        box_select_r.ClipWith(scope_rect);
        ImGuiWindow draw_window = FindFrontMostVisibleChildWindow(window);
        draw_window.DrawList.AddRectFilled(box_select_r.Min, box_select_r.Max, GetColorU32(ImGuiCol_SeparatorHovered, 0.30f)); // FIXME-MULTISELECT: Styling
        draw_window.DrawList.AddRect(box_select_r.Min, box_select_r.Max, GetColorU32(ImGuiCol_NavCursor)); // FIXME-MULTISELECT FIXME-DPI: Styling

        // Scroll
        bool enable_scroll = (ms_flags & ImGuiMultiSelectFlags_ScopeWindow) != 0 && (ms_flags & ImGuiMultiSelectFlags_BoxSelectNoScroll) == 0;
        if (enable_scroll)
        {
            ImRect scroll_r = scope_rect;
            scroll_r.Expand(-g.FontSize);
            //GetForegroundDrawList()->AddRect(scroll_r.Min, scroll_r.Max, IM_COL32(0, 255, 0, 255)); // omitted debug
            if (!scroll_r.Contains(g.IO.MousePos))
                BoxSelectScrollWithMouseDrag(bs, window, scroll_r);
        }
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Multi-Select support
    //-------------------------------------------------------------------------
    // - DebugLogMultiSelectRequests() [Internal] (omitted: IMGUI_DEBUG_LOG_SELECTION)
    // - CalcScopeRect() [Internal]
    // - BeginMultiSelect()
    // - EndMultiSelect()
    // - SetNextItemSelectionUserData()
    // - MultiSelectItemHeader() [Internal]
    // - MultiSelectItemFooter() [Internal]
    // - DebugNodeMultiSelectState() [Internal]
    //-------------------------------------------------------------------------

    // C++: static ImRect CalcScopeRect(ImGuiMultiSelectTempData* ms, ImGuiWindow* window)   [imgui_widgets.cpp 8059]
    public static ImRect CalcScopeRect(ImGuiMultiSelectTempData ms, ImGuiWindow window)
    {
        if ((ms.Flags & ImGuiMultiSelectFlags_ScopeRect) != 0)
        {
            // Warning: this depends on CursorMaxPos so it means to be called by EndMultiSelect() only
            // This probably doesn't work inside a table as there are ample ambiguities related to exact time of calling BeginMultiSelect()/EndMultiSelect().
            return new ImRect(ms.ScopeRectMin, ImMath.ImMax(window.DC.CursorMaxPos, ms.ScopeRectMin));
        }
        else
        {
            //// When a table, pull HostClipRect, which allows us to predict ClipRect before first row/layout is performed. (#7970)
            ImRect scope_rect = window.InnerClipRect;
            //if (g.CurrentTable != NULL)
            //    scope_rect = g.CurrentTable->HostClipRect;

            // Add inner table decoration (#7821) // FIXME: Why not baking in InnerClipRect?
            scope_rect.Min = ImMath.ImMin(scope_rect.Min + new ImVec2(window.DecoInnerSizeX1, window.DecoInnerSizeY1), scope_rect.Max);
            return scope_rect;
        }
    }

    // Return ImGuiMultiSelectIO structure.
    // Lifetime: don't hold on ImGuiMultiSelectIO* pointers over multiple frames or past any subsequent call to BeginMultiSelect() or EndMultiSelect().
    // Passing 'selection_size' and 'items_count' parameters is currently optional.
    // - 'selection_size' is useful to disable some shortcut routing: e.g. ImGuiMultiSelectFlags_ClearOnEscape won't claim Escape key when selection_size 0,
    //    allowing a first press to clear selection THEN the second press to leave child window and return to parent.
    // - 'items_count' is stored in ImGuiMultiSelectIO which makes it a convenient way to pass the information to your ApplyRequest() handler (but you may pass it differently).
    // - If they are costly for you to compute (e.g. external intrusive selection without maintaining size), you may avoid them and pass -1.
    //   - If you can easily tell if your selection is empty or not, you may pass 0/1, or you may enable ImGuiMultiSelectFlags_ClearOnEscape flag dynamically.
    // C++: ImGuiMultiSelectIO* ImGui::BeginMultiSelect(ImGuiMultiSelectFlags flags, int selection_size, int items_count)   [imgui_widgets.cpp 8082]
    public static ImGuiMultiSelectIO BeginMultiSelect(ImGuiMultiSelectFlags flags, int selection_size, int items_count)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        if (++g.MultiSelectTempDataStacked > g.MultiSelectTempData.Size)
            g.MultiSelectTempData.resize(g.MultiSelectTempDataStacked);
        // (C++ resize() constructs new tail elements; the C# ImVector default-initializes them to null.)
        if (g.MultiSelectTempData.Data[g.MultiSelectTempDataStacked - 1] == null)
            g.MultiSelectTempData.Data[g.MultiSelectTempDataStacked - 1] = new ImGuiMultiSelectTempData();
        ImGuiMultiSelectTempData ms = g.MultiSelectTempData.Data[g.MultiSelectTempDataStacked - 1];
        // (IM_STATIC_ASSERT(offsetof(ImGuiMultiSelectTempData, IO) == 0) omitted: ms.Clear() relies on IO being first member, which holds here as well.)
        g.CurrentMultiSelect = ms;
        if ((flags & (ImGuiMultiSelectFlags_ScopeWindow | ImGuiMultiSelectFlags_ScopeRect)) == 0)
            flags |= ImGuiMultiSelectFlags_ScopeWindow;
        if ((flags & ImGuiMultiSelectFlags_SingleSelect) != 0)
            flags &= ~(ImGuiMultiSelectFlags_BoxSelect2d | ImGuiMultiSelectFlags_BoxSelect1d);
        if ((flags & ImGuiMultiSelectFlags_BoxSelect2d) != 0)
            flags &= ~ImGuiMultiSelectFlags_BoxSelect1d;

        // FIXME: Workaround to the fact we override CursorMaxPos, meaning size measurement are lost. (#8250)
        // They should perhaps be stacked properly?
        if (g.CurrentTable != null)
        {
            ImGuiTable table = g.CurrentTable;
            if (!table.IsLayoutLocked)
                TableUpdateLayout(table);
            else if (table.CurrentColumn != -1)
                TableEndCell(table); // This is currently safe to call multiple time. If that properly is lost we can extract the "save measurement" part of it.
        }

        // FIXME: BeginFocusScope()
        ImGuiID id = window.IDStack.back();
        ms.Clear();
        ms.FocusScopeId = id;
        ms.Flags = flags;
        ms.BackupCursorMaxPos = window.DC.CursorMaxPos;
        ms.ScopeRectMin = window.DC.CursorPos;
        if ((flags & ImGuiMultiSelectFlags_ScopeRect) != 0)
            window.DC.CursorMaxPos = ms.ScopeRectMin; // CalcScopeRect() for ImGuiMultiSelectFlags_ScopeRect will measure in EndMultiSelect().
        PushFocusScope(ms.FocusScopeId);
        ms.IsFocused = IsInNavFocusRoute(g.CurrentFocusScopeId);
        if ((flags & ImGuiMultiSelectFlags_ScopeWindow) != 0) // Mark parent child window as navigable into, with highlight. Assume user will always submit interactive items.
            window.DC.NavLayersActiveMask |= (short)(1 << ImGuiNavLayer_Main);

        // Use copy of keyboard mods at the time of the request, otherwise we would requires mods to be held for an extra frame.
        ms.KeyMods = g.NavJustMovedToId != 0 ? (g.NavJustMovedToIsTabbing ? (ImGuiKeyChord)0 : g.NavJustMovedToKeyMods) : g.IO.KeyMods;
        if ((flags & ImGuiMultiSelectFlags_NoRangeSelect) != 0)
            ms.KeyMods &= ~(ImGuiKeyChord)ImGuiMod_Shift;

        // Bind storage
        ImGuiMultiSelectState storage = g.MultiSelectStorage.GetOrAddByKey(id);
        storage.ID = id;
        storage.LastFrameActive = g.FrameCount;
        storage.LastSelectionSize = selection_size;
        storage.Window = window;
        ms.Storage = storage;

        // Output to user
        ms.IO.Requests.resize(0);
        ms.IO.RangeSrcItem = storage.RangeSrcItem;
        ms.IO.NavIdItem = storage.NavIdItem;
        ms.IO.NavIdSelected = (storage.NavIdSelected == 1) ? true : false;
        ms.IO.ItemsCount = items_count;

        // Clear when using Navigation to move within the scope
        // (we compare FocusScopeId so it possible to use multiple selections inside a same window)
        bool request_clear = false;
        bool request_select_all = false;
        if (g.NavJustMovedToId != 0 && g.NavJustMovedToFocusScopeId == ms.FocusScopeId && g.NavJustMovedToHasSelectionData)
        {
            if ((ms.KeyMods & ImGuiMod_Shift) != 0)
                ms.IsKeyboardSetRange = true;
            if (ms.IsKeyboardSetRange)
                Debug.Assert(storage.RangeSrcItem != ImGuiSelectionUserData_Invalid); // Not ready -> could clear?
            if ((ms.KeyMods & (ImGuiMod_Ctrl | ImGuiMod_Shift)) == 0 && (flags & (ImGuiMultiSelectFlags_NoAutoClear | ImGuiMultiSelectFlags_NoAutoSelect)) == 0)
                request_clear = true;
        }
        else if (g.NavJustMovedFromFocusScopeId == ms.FocusScopeId)
        {
            // Also clear on leaving scope (may be optional?)
            if ((ms.KeyMods & (ImGuiMod_Ctrl | ImGuiMod_Shift)) == 0 && (flags & (ImGuiMultiSelectFlags_NoAutoClear | ImGuiMultiSelectFlags_NoAutoSelect)) == 0)
                request_clear = true;
        }

        // Box-select handling: update active state.
        ImGuiBoxSelectState bs = g.BoxSelectState;
        if ((flags & (ImGuiMultiSelectFlags_BoxSelect1d | ImGuiMultiSelectFlags_BoxSelect2d)) != 0)
        {
            ms.BoxSelectId = GetID("##BoxSelect");
            if (BeginBoxSelect(CalcScopeRect(ms, window), window, ms.BoxSelectId, flags))
                request_clear |= bs.RequestClear;
        }

        if (ms.IsFocused)
        {
            // Shortcut: Clear selection (Escape)
            // - Only claim shortcut if selection is not empty, allowing further presses on Escape to e.g. leave current child window.
            // - Box select also handle Escape and needs to pass an id to bypass ActiveIdUsingAllKeyboardKeys lock.
            if ((flags & ImGuiMultiSelectFlags_ClearOnEscape) != 0)
            {
                if (selection_size != 0 || bs.IsActive)
                    if (Shortcut(ImGuiKey_Escape, ImGuiInputFlags_None, bs.IsActive ? bs.ID : 0))
                    {
                        request_clear = true;
                        if (bs.IsActive)
                            BoxSelectDeactivateDrag(bs);
                    }
            }

            // Shortcut: Select all (Ctrl+A)
            if ((flags & ImGuiMultiSelectFlags_SingleSelect) == 0 && (flags & ImGuiMultiSelectFlags_NoSelectAll) == 0)
                if (Shortcut(ImGuiMod_Ctrl | ImGuiKey_A))
                    request_select_all = true;
        }

        if (request_clear || request_select_all)
        {
            MultiSelectAddSetAll(ms, request_select_all);
            if (!request_select_all)
                storage.LastSelectionSize = 0;
        }
        ms.LoopRequestSetAll = request_select_all ? (sbyte)1 : request_clear ? (sbyte)0 : (sbyte)-1;
        ms.IsSoleOrUnknownSelectionSize = (storage.LastSelectionSize == 1) || (storage.LastSelectionSize == -1);
        //ms.PrevSubmittedItem = ImGuiSelectionUserData_Invalid;

        // (DebugLogMultiSelectRequests("BeginMultiSelect", &ms->IO) omitted: IMGUI_DEBUG_LOG_SELECTION)

        return ms.IO;
    }

    // Return updated ImGuiMultiSelectIO structure.
    // Lifetime: don't hold on ImGuiMultiSelectIO* pointers over multiple frames or past any subsequent call to BeginMultiSelect() or EndMultiSelect().
    // C++: ImGuiMultiSelectIO* ImGui::EndMultiSelect()   [imgui_widgets.cpp 8198]
    public static ImGuiMultiSelectIO EndMultiSelect()
    {
        ImGuiContext g = GImGui;
        ImGuiMultiSelectTempData ms = g.CurrentMultiSelect;
        ImGuiMultiSelectState storage = ms.Storage;
        ImGuiWindow window = g.CurrentWindow;
        ImUtil.ImAssertUserError(ms.FocusScopeId == g.CurrentFocusScopeId, "EndMultiSelect() FocusScope mismatch!");
        Debug.Assert(g.CurrentMultiSelect != null && storage.Window == g.CurrentWindow);
        Debug.Assert(g.MultiSelectTempDataStacked > 0 && g.MultiSelectTempData.Data[g.MultiSelectTempDataStacked - 1] == g.CurrentMultiSelect);

        ImRect scope_rect = CalcScopeRect(ms, window);
        if (ms.IsFocused)
        {
            // We currently don't allow user code to modify RangeSrcItem by writing to BeginIO's version, but that would be an easy change here.
            if (ms.IO.RangeSrcReset || (ms.RangeSrcPassedBy == false && ms.IO.RangeSrcItem != ImGuiSelectionUserData_Invalid)) // Can't read storage->RangeSrcItem here -> we want the state at beginning of the scope (see tests for easy failure)
            {
                // (IMGUI_DEBUG_LOG_SELECTION omitted) Will set be to NavId.
                storage.RangeSrcItem = ImGuiSelectionUserData_Invalid;
            }
            if (ms.NavIdPassedBy == false && storage.NavIdItem != ImGuiSelectionUserData_Invalid)
            {
                // (IMGUI_DEBUG_LOG_SELECTION omitted)
                storage.NavIdItem = ImGuiSelectionUserData_Invalid;
                storage.NavIdSelected = -1;
            }

            if ((ms.Flags & (ImGuiMultiSelectFlags_BoxSelect1d | ImGuiMultiSelectFlags_BoxSelect2d)) != 0 && GetBoxSelectState(ms.BoxSelectId) != null)
                EndBoxSelect(scope_rect, ms.Flags);
        }

        if (ms.IsEndIO == false)
            ms.IO.Requests.resize(0);

        // Clear selection when clicking void?
        // We specifically test for IsMouseDragPastThreshold(0) == false to allow box-selection!
        // The InnerRect test is necessary for non-child/decorated windows.
        bool scope_hovered = window.InnerRect.Contains(g.IO.MousePos) && IsWindowHovered(ImGuiHoveredFlags_ChildWindows);
        if (scope_hovered && (ms.Flags & ImGuiMultiSelectFlags_ScopeRect) != 0)
            scope_hovered &= scope_rect.Contains(g.IO.MousePos);
        if (scope_hovered && g.HoveredId == 0 && g.ActiveId == 0)
        {
            if ((ms.Flags & (ImGuiMultiSelectFlags_BoxSelect1d | ImGuiMultiSelectFlags_BoxSelect2d)) != 0)
            {
                if (!g.BoxSelectState.IsActive && !g.BoxSelectState.IsStarting && g.IO.MouseClickedCount[0] == 1)
                {
                    BoxSelectPreStartDrag(ms.BoxSelectId, ImGuiSelectionUserData_Invalid);
                    FocusWindow(window, ImGuiFocusRequestFlags_UnlessBelowModal);
                    SetHoveredID(ms.BoxSelectId);
                    if ((ms.Flags & ImGuiMultiSelectFlags_ScopeRect) != 0)
                        SetNavID(0, ImGuiNavLayer_Main, ms.FocusScopeId, new ImRect(g.IO.MousePos, g.IO.MousePos)); // Automatically switch FocusScope for initial click from void to box-select.
                }
            }

            if ((ms.Flags & ImGuiMultiSelectFlags_ClearOnClickVoid) != 0)
                if (IsMouseReleased(0) && IsMouseDragPastThreshold(0) == false && g.IO.KeyMods == ImGuiMod_None)
                    MultiSelectAddSetAll(ms, false);
        }

        // Courtesy nav wrapping helper flag
        if ((ms.Flags & ImGuiMultiSelectFlags_NavWrapX) != 0)
        {
            Debug.Assert((ms.Flags & ImGuiMultiSelectFlags_ScopeWindow) != 0); // Only supported at window scope
            NavMoveRequestTryWrapping(GetCurrentWindow(), ImGuiNavMoveFlags_WrapX);
        }

        // Unwind
        if (g.CurrentTable != null)
            if (g.CurrentTable.IsInsideRow)
                TableEndRow(g.CurrentTable);
        window.DC.CursorMaxPos = ImMath.ImMax(ms.BackupCursorMaxPos, window.DC.CursorMaxPos);
        PopFocusScope();

        // (DebugLogMultiSelectRequests("EndMultiSelect", &ms->IO) omitted: IMGUI_DEBUG_LOG_SELECTION)

        ms.FocusScopeId = 0;
        ms.Flags = ImGuiMultiSelectFlags_None;
        g.CurrentMultiSelect = (--g.MultiSelectTempDataStacked > 0) ? g.MultiSelectTempData.Data[g.MultiSelectTempDataStacked - 1] : null;

        return ms.IO;
    }

    // C++: void ImGui::SetNextItemSelectionUserData(ImGuiSelectionUserData selection_user_data)   [imgui_widgets.cpp 8294]
    public static void SetNextItemSelectionUserData(ImGuiSelectionUserData selection_user_data)
    {
        // Note that flags will be cleared by ItemAdd(), so it's only useful for Navigation code!
        // This designed so widgets can also cheaply set this before calling ItemAdd(), so we are not tied to MultiSelect api.
        ImGuiContext g = GImGui;
        g.NextItemData.SelectionUserData = selection_user_data;
        g.NextItemData.FocusScopeId = g.CurrentFocusScopeId;

        ImGuiMultiSelectTempData ms = g.CurrentMultiSelect;
        if (ms != null)
        {
            // Auto updating RangeSrcPassedBy for cases were clipper is not used (done before ItemAdd() clipping)
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_HasSelectionUserData | ImGuiItemFlags_IsMultiSelect;
            if (ms.IO.RangeSrcItem == selection_user_data)
                ms.RangeSrcPassedBy = true;
            //ms.PrevSubmittedItem = ms.CurrSubmittedItem; // Can't rely on previous g.NextItemData.SelectionUserData because NextItemData is not restored on nested multi-select.
            //ms.CurrSubmittedItem = selection_user_data;
        }
        else
        {
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_HasSelectionUserData;
        }
    }

    // In charge of:
    // - Applying SetAll for submitted items.
    // - Applying SetRange for submitted items and record end points.
    // - Altering button behavior flags to facilitate use with drag and drop.
    // C++: void ImGui::MultiSelectItemHeader(ImGuiID id, bool* p_selected, ImGuiButtonFlags* p_button_flags)   [imgui_widgets.cpp 8329]
    // (Checkbox() calls it with p_button_flags == NULL -> use the 2-parameter overload)
    public static void MultiSelectItemHeader(ImGuiID id, ref bool p_selected)
    {
        ImGuiButtonFlags button_flags = 0;
        MultiSelectItemHeaderInternal(id, ref p_selected, ref button_flags, false);
    }

    public static void MultiSelectItemHeader(ImGuiID id, ref bool p_selected, ref ImGuiButtonFlags p_button_flags)
    {
        MultiSelectItemHeaderInternal(id, ref p_selected, ref p_button_flags, true);
    }

    // Body of C++ MultiSelectItemHeader(); 'has_button_flags' mirrors C++ p_button_flags != NULL.
    static void MultiSelectItemHeaderInternal(ImGuiID id, ref bool p_selected, ref ImGuiButtonFlags p_button_flags, bool has_button_flags)
    {
        ImGuiContext g = GImGui;
        ImGuiMultiSelectTempData ms = g.CurrentMultiSelect;

        bool selected = p_selected;
        if (ms.IsFocused)
        {
            ImGuiMultiSelectState storage = ms.Storage;
            ImGuiSelectionUserData item_data = g.NextItemData.SelectionUserData;
            Debug.Assert(g.NextItemData.FocusScopeId == g.CurrentFocusScopeId); // "Forgot to call SetNextItemSelectionUserData() prior to item, required in BeginMultiSelect()/EndMultiSelect() scope"

            // Apply SetAll (Clear/SelectAll) requests requested by BeginMultiSelect().
            // This is only useful if the user hasn't processed them already, and this only works if the user isn't using the clipper.
            // If you are using a clipper you need to process the SetAll request after calling BeginMultiSelect()
            if (ms.LoopRequestSetAll != -1)
                selected = (ms.LoopRequestSetAll == 1);

            // When using Shift+Nav: because it can incur scrolling we cannot afford a frame of lag with the selection highlight (otherwise scrolling would happen before selection)
            // For this to work, we need someone to set 'RangeSrcPassedBy = true' at some point (either clipper either SetNextItemSelectionUserData() function)
            if (ms.IsKeyboardSetRange)
            {
                Debug.Assert(id != 0 && (ms.KeyMods & ImGuiMod_Shift) != 0);
                bool is_range_dst = (ms.RangeDstPassedBy == false) && g.NavJustMovedToId == id;     // Assume that g.NavJustMovedToId is not clipped.
                if (is_range_dst)
                    ms.RangeDstPassedBy = true;
                if (is_range_dst && storage.RangeSrcItem == ImGuiSelectionUserData_Invalid) // If we don't have RangeSrc, assign RangeSrc = RangeDst
                {
                    storage.RangeSrcItem = item_data;
                    storage.RangeSelected = (sbyte)(selected ? 1 : 0);
                }
                bool is_range_src = storage.RangeSrcItem == item_data;
                if (is_range_src || is_range_dst || ms.RangeSrcPassedBy != ms.RangeDstPassedBy)
                {
                    // Apply range-select value to visible items
                    Debug.Assert(storage.RangeSrcItem != ImGuiSelectionUserData_Invalid && storage.RangeSelected != -1);
                    selected = (storage.RangeSelected != 0);
                }
                else if ((ms.KeyMods & ImGuiMod_Ctrl) == 0 && (ms.Flags & ImGuiMultiSelectFlags_NoAutoClear) == 0)
                {
                    // Clear other items
                    selected = false;
                }
            }
            p_selected = selected;
        }

        // Alter button behavior flags
        // To handle drag and drop of multiple items we need to avoid clearing selection on click.
        // Enabling this test makes actions using Ctrl+Shift delay their effect on MouseUp which is annoying, but it allows drag and drop of multiple items.
        if (has_button_flags)
        {
            ImGuiButtonFlags button_flags = p_button_flags;
            button_flags |= ImGuiButtonFlags_NoHoveredOnFocus;
            button_flags &= ~(ImGuiButtonFlags_PressedOnClick | ImGuiButtonFlags_PressedOnClickRelease);
            if ((ms.Flags & ImGuiMultiSelectFlags_SelectOnClickAlways) != 0)
                button_flags |= ImGuiButtonFlags_PressedOnClick;
            else if ((ms.Flags & ImGuiMultiSelectFlags_SelectOnClickRelease) != 0)
                button_flags |= ImGuiButtonFlags_PressedOnClickRelease;
            else // ImGuiMultiSelectFlags_SelectOnAuto
                button_flags |= (!selected || (g.ActiveId == id && g.ActiveIdHasBeenPressedBefore)) ? ImGuiButtonFlags_PressedOnClick : ImGuiButtonFlags_PressedOnClickRelease;
            p_button_flags = button_flags;
        }
    }

    // In charge of:
    // - Auto-select on navigation.
    // - Box-select toggle handling.
    // - Right-click handling.
    // - Altering selection based on Ctrl/Shift modifiers, both for keyboard and mouse.
    // - Record current selection state for RangeSrc
    // This is all rather complex, best to run and refer to "widgets_multiselect_xxx" tests in imgui_test_suite.
    // C++: void ImGui::MultiSelectItemFooter(ImGuiID id, bool* p_selected, bool* p_pressed, ImGuiMultiSelectFlags extra_flags)   [imgui_widgets.cpp 8401]
    public static void MultiSelectItemFooter(ImGuiID id, ref bool p_selected, ref bool p_pressed, ImGuiMultiSelectFlags extra_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        bool selected = p_selected;
        bool pressed = p_pressed;
        ImGuiMultiSelectTempData ms = g.CurrentMultiSelect;
        ImGuiMultiSelectState storage = ms.Storage;
        if (pressed)
            ms.IsFocused = true;

        bool hovered = false;
        if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HoveredRect) != 0)
            hovered = IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup);
        if (!ms.IsFocused && !hovered)
            return;

        ImGuiSelectionUserData item_data = g.NextItemData.SelectionUserData;

        ImGuiMultiSelectFlags flags = ms.Flags | extra_flags;
        bool is_singleselect = (flags & ImGuiMultiSelectFlags_SingleSelect) != 0;
        bool is_ctrl = (ms.KeyMods & ImGuiMod_Ctrl) != 0;
        bool is_shift = (ms.KeyMods & ImGuiMod_Shift) != 0;

        bool apply_to_range_src = false;

        if (g.NavId == id && storage.RangeSrcItem == ImGuiSelectionUserData_Invalid)
            apply_to_range_src = true;
        if (ms.IsEndIO == false)
        {
            ms.IO.Requests.resize(0);
            ms.IsEndIO = true;
        }

        // Auto-select as you navigate a list
        if (g.NavJustMovedToId == id)
        {
            if ((flags & ImGuiMultiSelectFlags_NoAutoSelect) == 0)
            {
                if (is_ctrl && is_shift)
                    pressed = true;
                else if (!is_ctrl)
                    selected = pressed = true;
            }
            else
            {
                // With NoAutoSelect, using Shift+keyboard performs a write/copy
                if (is_shift)
                    pressed = true;
                else if (!is_ctrl)
                    apply_to_range_src = true; // Since if (pressed) {} main block is not running we update this
            }
        }

        if (apply_to_range_src)
        {
            storage.RangeSrcItem = item_data;
            storage.RangeSelected = (sbyte)(selected ? 1 : 0); // Will be updated at the end of this function anyway.
        }

        // Box-select toggle handling
        if (ms.BoxSelectId != 0)
        {
            ImGuiBoxSelectState bs = GetBoxSelectState(ms.BoxSelectId);
            if (bs != null)
            {
                ImRect item_rect = g.LastItemData.Rect;
                if (!window.DC.NavIsScrollPushableX) // FIXME: Rename to be more generic.
                    if (g.CurrentTable != null)
                        if (g.CurrentTable.CurrentColumn != -1)
                        {
                            // FIXME: We cannot solely use current ClipRect as it includes HostClipRect.
                            // However we account for ClipRect being larger than current column (e.g. when using SpanAllColumns)
                            // A more generic version would be nice, but window->WorkRect.Min/Max exclude CellPadding. (#7994, #9383)
                            ImGuiTable table = g.CurrentTable;
                            ImGuiTableColumn column = table.Columns[table.CurrentColumn];
                            float clip_min_x = (g.LastItemData.ItemFlags & ImGuiItemStatusFlags_HasClipRect) != 0 ? g.LastItemData.ClipRect.Min.x : window.ClipRect.Min.x;
                            float clip_max_x = (g.LastItemData.ItemFlags & ImGuiItemStatusFlags_HasClipRect) != 0 ? g.LastItemData.ClipRect.Max.x : window.ClipRect.Max.x;
                            if (clip_min_x != clip_max_x) // When zero sized we expect that bounds have been clamped and thus are unreliable
                            {
                                item_rect.Min.x = ImMath.ImMax(item_rect.Min.x, ImMath.ImMin(column.MinX, clip_min_x));
                                item_rect.Max.x = ImMath.ImMin(item_rect.Max.x, ImMath.ImMax(column.MaxX, clip_max_x));
                            }
                            else
                            {
                                item_rect.Min.x = ImMath.ImMax(item_rect.Min.x, column.MinX);
                                item_rect.Max.x = ImMath.ImMin(item_rect.Max.x, column.MaxX);
                            }
                            //GetForegroundDrawList()->AddRect(item_rect.Min, item_rect.Max, IM_COL32(255, 0, 255, 255)); // omitted debug
                        }
                bool rect_overlap_curr = bs.BoxSelectRectCurr.Overlaps(item_rect);
                bool rect_overlap_prev = bs.BoxSelectRectPrev.Overlaps(item_rect);
                if ((rect_overlap_curr && !rect_overlap_prev && !selected) || (rect_overlap_prev && !rect_overlap_curr))
                {
                    if (storage.LastSelectionSize <= 0 && bs.IsStartedSetNavIdOnce)
                    {
                        pressed = true; // First item act as a pressed: code below will emit selection request and set NavId (whatever we emit here will be overridden anyway)
                        bs.IsStartedSetNavIdOnce = false;
                    }
                    else
                    {
                        selected = !selected;
                        MultiSelectAddSetRange(ms, selected, +1, item_data, item_data);
                        // (IMGUI_DEBUG_BOXSELECT debug visualization omitted)
                    }
                    storage.LastSelectionSize = ImMath.ImMax(storage.LastSelectionSize + 1, 1);
                }
            }
        }

        // Right-click handling.
        // FIXME-MULTISELECT: Maybe should be moved to Selectable()? Also see #5816, #8200, #9015
        if (hovered && IsMouseClicked(1) && (flags & (ImGuiMultiSelectFlags_NoAutoSelect | ImGuiMultiSelectFlags_NoSelectOnRightClick)) == 0)
        {
            if (g.ActiveId != 0 && g.ActiveId != id)
                ClearActiveID();
            SetFocusID(id, window);
            if (!pressed && !selected)
            {
                pressed = true;
                is_ctrl = is_shift = false;
            }
        }

        // Unlike Space, Enter doesn't alter selection (but can still return a press) unless current item is not selected.
        // The later, "unless current item is not select", may become optional? It seems like a better default if Enter doesn't necessarily open something
        // (unlike e.g. Windows explorer). For use case where Enter always open something, we might decide to make this optional?
        bool enter_pressed = pressed && (g.NavActivateId == id) && (g.NavActivateFlags & ImGuiActivateFlags_PreferInput) != 0;

        // Alter selection
        if (pressed && (!enter_pressed || !selected))
        {
            // Box-select
            ImGuiInputSource input_source = (g.NavJustMovedToId == id || g.NavActivateId == id) ? (ImGuiInputSource)g.NavInputSource : ImGuiInputSource_Mouse;
            if ((flags & (ImGuiMultiSelectFlags_BoxSelect1d | ImGuiMultiSelectFlags_BoxSelect2d)) != 0)
                if (!g.BoxSelectState.IsActive && !g.BoxSelectState.IsStarting && input_source == ImGuiInputSource_Mouse && g.IO.MouseClickedCount[0] == 1)
                    BoxSelectPreStartDrag(ms.BoxSelectId, item_data);

            //----------------------------------------------------------------------------------------
            // ACTION                      | Begin  | Pressed/Activated  | End
            //----------------------------------------------------------------------------------------
            // Keys Navigated:             | Clear  | Src=item, Sel=1               SetRange 1
            // Keys Navigated: Ctrl        | n/a    | n/a
            // Keys Navigated:      Shift  | n/a    | Dst=item, Sel=1,   => Clear + SetRange 1
            // Keys Navigated: Ctrl+Shift  | n/a    | Dst=item, Sel=Src  => Clear + SetRange Src-Dst
            // Keys Activated:             | n/a    | Src=item, Sel=1    => Clear + SetRange 1
            // Keys Activated: Ctrl        | n/a    | Src=item, Sel=!Sel =>         SetSange 1
            // Keys Activated:      Shift  | n/a    | Dst=item, Sel=1    => Clear + SetSange 1
            //----------------------------------------------------------------------------------------
            // Mouse Pressed:              | n/a    | Src=item, Sel=1,   => Clear + SetRange 1
            // Mouse Pressed:  Ctrl        | n/a    | Src=item, Sel=!Sel =>         SetRange 1
            // Mouse Pressed:       Shift  | n/a    | Dst=item, Sel=1,   => Clear + SetRange 1
            // Mouse Pressed:  Ctrl+Shift  | n/a    | Dst=item, Sel=!Sel =>         SetRange Src-Dst
            //----------------------------------------------------------------------------------------

            if ((flags & ImGuiMultiSelectFlags_NoAutoClear) == 0)
            {
                bool request_clear = false;
                if (is_singleselect)
                    request_clear = true;
                else if ((input_source == ImGuiInputSource_Mouse || g.NavActivateId == id) && !is_ctrl)
                    request_clear = (flags & ImGuiMultiSelectFlags_NoAutoClearOnReselect) != 0 ? !selected : true;
                else if ((input_source == ImGuiInputSource_Keyboard || input_source == ImGuiInputSource_Gamepad) && is_shift && !is_ctrl)
                    request_clear = true; // With is_shift==false the RequestClear was done in BeginIO, not necessary to do again.
                if (request_clear)
                    MultiSelectAddSetAll(ms, false);
            }

            int range_direction;
            bool range_selected;
            if (is_shift && !is_singleselect)
            {
                //IM_ASSERT(storage->HasRangeSrc && storage->HasRangeValue);
                if (storage.RangeSrcItem == ImGuiSelectionUserData_Invalid)
                    storage.RangeSrcItem = item_data;
                if ((flags & ImGuiMultiSelectFlags_CheckboxMode_) == 0)
                {
                    // Shift+Arrow always select
                    // Ctrl+Shift+Arrow copy source selection state (already stored by BeginMultiSelect() in storage->RangeSelected)
                    range_selected = (is_ctrl && storage.RangeSelected != -1) ? (storage.RangeSelected != 0) : true;
                }
                else
                {
                    // Shift+Arrow copy source selection state
                    // Shift+Click always copy from target selection state
                    if (ms.IsKeyboardSetRange)
                        range_selected = (storage.RangeSelected != -1) ? (storage.RangeSelected != 0) : true;
                    else
                        range_selected = !selected;
                }
                range_direction = ms.RangeSrcPassedBy ? +1 : -1;
            }
            else
            {
                // Ctrl inverts selection, otherwise always select
                if ((flags & ImGuiMultiSelectFlags_CheckboxMode_) == 0)
                    selected = is_ctrl ? !selected : true;
                else
                    selected = !selected;
                storage.RangeSrcItem = item_data;
                range_selected = selected;
                range_direction = +1;
            }
            MultiSelectAddSetRange(ms, range_selected, range_direction, storage.RangeSrcItem, item_data);
        }

        // Update/store the selection state of the Source item (used by Ctrl+Shift, when Source is unselected we perform a range unselect)
        if (storage.RangeSrcItem == item_data)
            storage.RangeSelected = (sbyte)(selected ? 1 : 0);

        // Update/store the selection state of focused item
        if (g.NavId == id)
        {
            storage.NavIdItem = item_data;
            storage.NavIdSelected = (sbyte)(selected ? 1 : 0);
        }
        if (storage.NavIdItem == item_data)
            ms.NavIdPassedBy = true;

        p_selected = selected;
        p_pressed = pressed;
    }

    // C++: void ImGui::MultiSelectAddSetAll(ImGuiMultiSelectTempData* ms, bool selected)   [imgui_widgets.cpp 8570]
    public static void MultiSelectAddSetAll(ImGuiMultiSelectTempData ms, bool selected)
    {
        ImGuiSelectionRequest req = new ImGuiSelectionRequest
        {
            Type = ImGuiSelectionRequestType_SetAll,
            Selected = selected,
            RangeDirection = 0,
            RangeFirstItem = ImGuiSelectionUserData_Invalid,
            RangeLastItem = ImGuiSelectionUserData_Invalid,
        };
        ms.IO.Requests.resize(0);      // Can always clear previous requests
        ms.IO.Requests.push_back(req); // Add new request
    }

    // C++: void ImGui::MultiSelectAddSetRange(ImGuiMultiSelectTempData* ms, bool selected, int range_dir, ImGuiSelectionUserData first_item, ImGuiSelectionUserData last_item)   [imgui_widgets.cpp 8577]
    public static void MultiSelectAddSetRange(ImGuiMultiSelectTempData ms, bool selected, int range_dir, ImGuiSelectionUserData first_item, ImGuiSelectionUserData last_item)
    {
        // Merge contiguous spans into same request (unless NoRangeSelect is set which guarantees single-item ranges)
        // FIXME-OPT: Disabled on 2026/04/09 as this would break with any form of coarse clipping that we don't know about (e.g. TableNextColumn() return value).
        // The low-hanging fruit would be to know that ImGuiSelectionUserData are sequential indices, in which case we can trivially compare PrevSubmittedItem + RangeDir == FirstItem.
        // User can always perform this merge if required.
        // (C++ #if 0 block omitted)

        ImGuiSelectionRequest req = new ImGuiSelectionRequest
        {
            Type = ImGuiSelectionRequestType_SetRange,
            Selected = selected,
            RangeDirection = (ImS8)range_dir,
            RangeFirstItem = (range_dir > 0) ? first_item : last_item,
            RangeLastItem = (range_dir > 0) ? last_item : first_item,
        };
        ms.IO.Requests.push_back(req); // Add new request
    }

    // C++: void ImGui::DebugNodeMultiSelectState(ImGuiMultiSelectState* storage)   [imgui_widgets.cpp 8613]
    // (IMGUI_DISABLE_DEBUG_TOOLS guard omitted: debug tools are always compiled in this port)
    public static void DebugNodeMultiSelectState(ImGuiMultiSelectState storage)
    {
        bool is_active = (storage.LastFrameActive >= GetFrameCount() - 2); // Note that fully clipped early out scrolling tables will appear as inactive here.
        if (!is_active) { PushStyleColor(ImGuiCol_Text, GetStyleColorVec4(ImGuiCol_TextDisabled)); }
        bool open = TreeNode((object)storage.ID, "MultiSelect 0x%08X in '%s'%s", storage.ID, storage.Window != null ? storage.Window.Name : "N/A", is_active ? "" : " *Inactive*");
        if (!is_active) { PopStyleColor(); }
        if (!open)
            return;
        Text("RangeSrcItem = %lld (0x%llX), RangeSelected = %i", storage.RangeSrcItem, storage.RangeSrcItem, storage.RangeSelected);
        Text("NavIdItem = %lld (0x%llX), NavIdSelected = %i", storage.NavIdItem, storage.NavIdItem, storage.NavIdSelected);
        Text("LastSelectionSize = %i", storage.LastSelectionSize); // Provided by user
        TreePop();
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: ListBox
    //-------------------------------------------------------------------------
    // - BeginListBox()
    // - EndListBox()
    // - ListBox()
    //-------------------------------------------------------------------------

    // This is essentially a thin wrapper to using BeginChild/EndChild with the ImGuiChildFlags_FrameStyle flag for stylistic changes + displaying a label.
    // This handle some subtleties with capturing info from the label.
    // If you don't need a label you can pretty much directly use ImGui::BeginChild() with ImGuiChildFlags_FrameStyle.
    // Tip: To have a list filling the entire window width, use size.x = -FLT_MIN and pass an non-visible label e.g. "##empty"
    // Tip: If your vertical size is calculated from an item count (e.g. 10 * item_height) consider adding a fractional part to facilitate seeing scrolling boundaries (e.g. 10.5f * item_height).
    // C++: bool ImGui::BeginListBox(const char* label, const ImVec2& size_arg)   [imgui_widgets.cpp 8889]
    public static bool BeginListBox(string label, ImVec2 size_arg)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiStyle style = g.Style;
        ImGuiID id = GetID(label);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        // Size default to hold ~7.25 items.
        // Fractional number of items helps seeing that we can scroll down/up without looking at scrollbar.
        ImVec2 size = ImMath.ImTrunc(CalcItemSize(size_arg, CalcItemWidth(), GetTextLineHeightWithSpacing() * 7.25f + style.FramePadding.y * 2.0f));
        ImVec2 frame_size = new ImVec2(size.x, ImMath.ImMax(size.y, label_size.y));
        ImRect frame_bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + frame_size);
        ImRect bb = new ImRect(frame_bb.Min, frame_bb.Max + new ImVec2(label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f, 0.0f));
        g.NextItemData.ClearFlags();

        if (!IsRectVisible(bb.Min, bb.Max))
        {
            ItemSize(bb.GetSize(), style.FramePadding.y);
            ItemAdd(bb, 0, frame_bb);
            g.NextWindowData.ClearFlags(); // We behave like Begin() and need to consume those values
            return false;
        }

        // FIXME-OPT: We could omit the BeginGroup() if label_size.x == 0.0f but would need to omit the EndGroup() as well.
        BeginGroup();
        if (label_size.x > 0.0f)
        {
            ImVec2 label_pos = new ImVec2(frame_bb.Max.x + style.ItemInnerSpacing.x, frame_bb.Min.y + style.FramePadding.y);
            RenderText(label_pos, label, label_end, false);
            window.DC.CursorMaxPos = ImMath.ImMax(window.DC.CursorMaxPos, label_pos + label_size);
            AlignTextToFramePadding();
        }

        BeginChild(id, frame_bb.GetSize(), ImGuiChildFlags_FrameStyle);
        return true;
    }

    // C++: void ImGui::EndListBox()   [imgui_widgets.cpp 8943]
    public static void EndListBox()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert((window.Flags & ImGuiWindowFlags_ChildWindow) != 0); // "Mismatched BeginListBox/EndListBox calls. Did you test the return value of BeginListBox?"

        EndChild();
        EndGroup(); // This is only required to be able to do IsItemXXX query on the whole ListBox including label
    }

    // C++: bool ImGui::ListBox(const char* label, int* current_item, const char* const items[], int items_count, int height_items)   [imgui_widgets.cpp 8959]
    // (C++ static Items_ArrayGetter() is shared with the Combo() section: ported as ImGui.Items_ArrayGetter in ImGuiWidgets2.cs.)
    public static bool ListBox(string label, ref int current_item, string[] items, int items_count, int height_items = -1)
    {
        bool value_changed = ListBox(label, ref current_item, Items_ArrayGetter, items, items_count, height_items);
        return value_changed;
    }

    // This is merely a helper around BeginListBox(), EndListBox().
    // Considering using those directly to submit custom data or store selection differently.
    // C++: bool ImGui::ListBox(const char* label, int* current_item, const char* (*getter)(void* user_data, int idx), void* user_data, int items_count, int height_in_items)   [imgui_widgets.cpp 8966]
    public static bool ListBox(string label, ref int current_item, Func<object, int, string> getter, object user_data, int items_count, int height_in_items = -1)
    {
        ImGuiContext g = GImGui;

        // Calculate size from "height_in_items"
        if (height_in_items < 0)
            height_in_items = ImMath.ImMin(items_count, 7);
        float height_in_items_f = height_in_items + 0.25f;
        ImVec2 size = new ImVec2(0.0f, ImMath.ImTrunc(GetTextLineHeightWithSpacing() * height_in_items_f + g.Style.FramePadding.y * 2.0f));

        if (!BeginListBox(label, size))
            return false;

        // Assume all items have even height (= 1 line of text). If you need items of different height,
        // you can create a custom version of ListBox() in your code without using the clipper.
        bool value_changed = false;
        ImGuiListClipper clipper = new ImGuiListClipper();
        clipper.Begin(items_count, GetTextLineHeightWithSpacing()); // We know exactly our line height here so we pass it as a minor optimization, but generally you don't need to.
        clipper.IncludeItemByIndex(current_item);
        while (clipper.Step())
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                string item_text = getter(user_data, i);
                if (item_text == null)
                    item_text = "*Unknown item*";

                PushID(i);
                bool item_selected = (i == current_item);
                if (Selectable(item_text, item_selected))
                {
                    current_item = i;
                    value_changed = true;
                }
                if (item_selected)
                    SetItemDefaultFocus();
                PopID();
            }
        EndListBox();

        if (value_changed)
            MarkItemEdited(g.LastItemData.ID);

        return value_changed;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: PlotLines, PlotHistogram
    //-------------------------------------------------------------------------
    // - PlotEx() [Internal]
    // - PlotLines()
    // - PlotHistogram()
    //-------------------------------------------------------------------------
    // Plot/Graph widgets are not very good.
    // Consider using ImPlot (https://github.com/epezent/implot) which is much better!
    //-------------------------------------------------------------------------

    // C++: int ImGui::PlotEx(ImGuiPlotType plot_type, const char* label, float (*values_getter)(void* data, int idx), void* data, int values_count, int values_offset, const char* overlay_text, float scale_min, float scale_max, const ImVec2& size_arg)   [imgui_widgets.cpp 9016]
    public static int PlotEx(ImGuiPlotType plot_type, string label, Func<object, int, float> values_getter, object data, int values_count, int values_offset, string overlay_text, float scale_min, float scale_max, ImVec2 size_arg)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return -1;

        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);

        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImVec2 frame_size = CalcItemSize(size_arg, CalcItemWidth(), label_size.y + style.FramePadding.y * 2.0f);

        ImRect frame_bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + frame_size);
        ImRect inner_bb = new ImRect(frame_bb.Min + style.FramePadding, frame_bb.Max - style.FramePadding);
        ImRect total_bb = new ImRect(frame_bb.Min, frame_bb.Max + new ImVec2(label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f, 0));
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(total_bb, id, frame_bb, ImGuiItemFlags_NoNav))
            return -1;
        bool hovered, held;
        ButtonBehavior(frame_bb, id, out hovered, out held);

        // Determine scale from values if not specified
        if (scale_min == float.MaxValue || scale_max == float.MaxValue)
        {
            float v_min = float.MaxValue;
            float v_max = -float.MaxValue;
            for (int i = 0; i < values_count; i++)
            {
                float v = values_getter(data, i);
                if (v != v) // Ignore NaN values
                    continue;
                v_min = ImMath.ImMin(v_min, v);
                v_max = ImMath.ImMax(v_max, v);
            }
            if (scale_min == float.MaxValue)
                scale_min = v_min;
            if (scale_max == float.MaxValue)
                scale_max = v_max;
        }

        RenderFrame(frame_bb.Min, frame_bb.Max, GetColorU32(ImGuiCol_FrameBg), true, style.FrameRounding);

        int values_count_min = (plot_type == ImGuiPlotType_Lines) ? 2 : 1;
        int idx_hovered = -1;
        if (values_count >= values_count_min)
        {
            int res_w = ImMath.ImMin((int)frame_size.x, values_count) + ((plot_type == ImGuiPlotType_Lines) ? -1 : 0);
            int item_count = values_count + ((plot_type == ImGuiPlotType_Lines) ? -1 : 0);

            // Tooltip on hover
            if (hovered && inner_bb.Contains(g.IO.MousePos))
            {
                float t = ImMath.ImClamp((g.IO.MousePos.x - inner_bb.Min.x) / (inner_bb.Max.x - inner_bb.Min.x), 0.0f, 0.999f);
                int v_idx = (int)(t * item_count);
                Debug.Assert(v_idx >= 0 && v_idx < values_count);

                float hov_v0 = values_getter(data, (v_idx + values_offset) % values_count); // (C++ v0: renamed to avoid C# CS0136 scoping clash with the later v0)
                float hov_v1 = values_getter(data, (v_idx + 1 + values_offset) % values_count); // (C++ v1)
                if (plot_type == ImGuiPlotType_Lines)
                    SetTooltipV("%d: %8.4g\n%d: %8.4g", v_idx, hov_v0, v_idx + 1, hov_v1);
                else if (plot_type == ImGuiPlotType_Histogram)
                    SetTooltipV("%d: %8.4g", v_idx, hov_v0);
                idx_hovered = v_idx;
            }

            float t_step = 1.0f / (float)res_w;
            float inv_scale = (scale_min == scale_max) ? 0.0f : (1.0f / (scale_max - scale_min));

            float v0 = values_getter(data, (0 + values_offset) % values_count);
            float t0 = 0.0f;
            ImVec2 tp0 = new ImVec2(t0, 1.0f - ImMath.ImSaturate((v0 - scale_min) * inv_scale));               // Point in the normalized space of our target rectangle
            float histogram_zero_line_t = (scale_min * scale_max < 0.0f) ? (1 + scale_min * inv_scale) : (scale_min < 0.0f ? 0.0f : 1.0f);   // Where does the zero line stands

            ImU32 col_base = GetColorU32((plot_type == ImGuiPlotType_Lines) ? ImGuiCol_PlotLines : ImGuiCol_PlotHistogram);
            ImU32 col_hovered = GetColorU32((plot_type == ImGuiPlotType_Lines) ? ImGuiCol_PlotLinesHovered : ImGuiCol_PlotHistogramHovered);

            for (int n = 0; n < res_w; n++)
            {
                float t1 = t0 + t_step;
                int v1_idx = (int)(t0 * item_count + 0.5f);
                Debug.Assert(v1_idx >= 0 && v1_idx < values_count);
                float v1 = values_getter(data, (v1_idx + values_offset + 1) % values_count);
                ImVec2 tp1 = new ImVec2(t1, 1.0f - ImMath.ImSaturate((v1 - scale_min) * inv_scale));

                // NB: Draw calls are merged together by the DrawList system. Still, we should render our batch are lower level to save a bit of CPU.
                ImVec2 pos0 = ImMath.ImLerp(inner_bb.Min, inner_bb.Max, tp0);
                ImVec2 pos1 = ImMath.ImLerp(inner_bb.Min, inner_bb.Max, (plot_type == ImGuiPlotType_Lines) ? tp1 : new ImVec2(tp1.x, histogram_zero_line_t));
                if (plot_type == ImGuiPlotType_Lines)
                {
                    window.DrawList.AddLine(pos0, pos1, idx_hovered == v1_idx ? col_hovered : col_base);
                }
                else if (plot_type == ImGuiPlotType_Histogram)
                {
                    if (pos1.x >= pos0.x + 2.0f)
                        pos1.x -= 1.0f;
                    window.DrawList.AddRectFilled(pos0, pos1, idx_hovered == v1_idx ? col_hovered : col_base);
                }

                t0 = t1;
                tp0 = tp1;
            }
        }

        // Text overlay
        if (overlay_text != null)
            RenderTextClipped(new ImVec2(frame_bb.Min.x, frame_bb.Min.y + style.FramePadding.y), frame_bb.Max, overlay_text, -1, null, new ImVec2(0.5f, 0.0f));

        if (label_size.x > 0.0f)
            RenderText(new ImVec2(frame_bb.Max.x + style.ItemInnerSpacing.x, inner_bb.Min.y), label, label_end, false);

        // Return hovered index or -1 if none are hovered.
        // This is currently not exposed in the public API because we need a larger redesign of the whole thing, but in the short-term we are making it available in PlotEx().
        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        return idx_hovered;
    }

    // C++: struct ImGuiPlotArrayGetterData   [imgui_widgets.cpp 9101]
    // (C++ uses a raw pointer + byte stride; the C# port indexes the managed array by element.)
    private class ImGuiPlotArrayGetterData
    {
        public float[] Values;
        public int Stride; // In bytes, as in C++.

        public ImGuiPlotArrayGetterData(float[] values, int stride) { Values = values; Stride = stride; }
    }

    // C++: static float Plot_ArrayGetter(void* data, int idx)   [imgui_widgets.cpp 9108]
    static float Plot_ArrayGetter(object data, int idx)
    {
        ImGuiPlotArrayGetterData plot_data = (ImGuiPlotArrayGetterData)data;
        // C++: *(const float*)(const void*)((const unsigned char*)plot_data->Values + (size_t)idx * plot_data->Stride);
        int elem = (int)((idx * (long)plot_data.Stride) / sizeof(float));
        return plot_data.Values[elem];
    }

    // C++: void ImGui::PlotLines(const char* label, const float* values, int values_count, int values_offset, const char* overlay_text, float scale_min, float scale_max, ImVec2 graph_size, int stride)   [imgui_widgets.cpp 9115]
    public static void PlotLines(string label, float[] values, int values_count, int values_offset = 0, string overlay_text = null, float scale_min = float.MaxValue, float scale_max = float.MaxValue, ImVec2 graph_size = default, int stride = sizeof(float))
    {
        ImGuiPlotArrayGetterData data = new ImGuiPlotArrayGetterData(values, stride);
        PlotEx(ImGuiPlotType_Lines, label, Plot_ArrayGetter, data, values_count, values_offset, overlay_text, scale_min, scale_max, graph_size);
    }

    // C++: void ImGui::PlotLines(const char* label, float (*values_getter)(void* data, int idx), void* data, int values_count, int values_offset, const char* overlay_text, float scale_min, float scale_max, ImVec2 graph_size)   [imgui_widgets.cpp 9122]
    public static void PlotLines(string label, Func<object, int, float> values_getter, object data, int values_count, int values_offset = 0, string overlay_text = null, float scale_min = float.MaxValue, float scale_max = float.MaxValue, ImVec2 graph_size = default)
    {
        PlotEx(ImGuiPlotType_Lines, label, values_getter, data, values_count, values_offset, overlay_text, scale_min, scale_max, graph_size);
    }

    // Plot Histogram (the data provided _is_ histogram data. it doesn't compute the histogram of your data)
    // C++: void ImGui::PlotHistogram(const char* label, const float* values, int values_count, int values_offset, const char* overlay_text, float scale_min, float scale_max, ImVec2 graph_size, int stride)   [imgui_widgets.cpp 9129]
    public static void PlotHistogram(string label, float[] values, int values_count, int values_offset = 0, string overlay_text = null, float scale_min = float.MaxValue, float scale_max = float.MaxValue, ImVec2 graph_size = default, int stride = sizeof(float))
    {
        ImGuiPlotArrayGetterData data = new ImGuiPlotArrayGetterData(values, stride);
        PlotEx(ImGuiPlotType_Histogram, label, Plot_ArrayGetter, data, values_count, values_offset, overlay_text, scale_min, scale_max, graph_size);
    }

    // C++: void ImGui::PlotHistogram(const char* label, float (*values_getter)(void* data, int idx), void* data, int values_count, int values_offset, const char* overlay_text, float scale_min, float scale_max, ImVec2 graph_size)   [imgui_widgets.cpp 9136]
    public static void PlotHistogram(string label, Func<object, int, float> values_getter, object data, int values_count, int values_offset = 0, string overlay_text = null, float scale_min = float.MaxValue, float scale_max = float.MaxValue, ImVec2 graph_size = default)
    {
        PlotEx(ImGuiPlotType_Histogram, label, values_getter, data, values_count, values_offset, overlay_text, scale_min, scale_max, graph_size);
    }

    //-------------------------------------------------------------------------
    // [SECTION] MenuItem, BeginMenu, EndMenu, etc.
    //-------------------------------------------------------------------------
    // - ImGuiMenuColumns [Internal] (ported in ImGuiUtil.cs)
    // - BeginMenuBar()
    // - EndMenuBar()
    // - BeginMainMenuBar()
    // - EndMainMenuBar()
    // - BeginMenu()
    // - EndMenu()
    // - MenuItemEx() [Internal]
    // - MenuItem()
    //-------------------------------------------------------------------------

    // FIXME: Provided a rectangle perhaps e.g. a BeginMenuBarEx() could be used anywhere..
    // Currently the main responsibility of this function being to setup clip-rect + horizontal layout + menu navigation layer.
    // Ideally we also want this to be responsible for claiming space out of the main window scrolling rectangle, in which case ImGuiWindowFlags_MenuBar will become unnecessary.
    // Then later the same system could be used for multiple menu-bars, scrollbars, side-bars.
    // C++: bool ImGui::BeginMenuBar()   [imgui_widgets.cpp 9235]
    public static bool BeginMenuBar()
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;
        if ((window.Flags & ImGuiWindowFlags_MenuBar) == 0)
            return false;

        Debug.Assert(!window.DC.MenuBarAppending);
        BeginGroup(); // Backup position on layer 0 // FIXME: Misleading to use a group for that backup/restore
        PushID("##MenuBar");

        // We don't clip with current window clipping rectangle as it is already set to the area below. However we clip with window full rect.
        // We remove 1 worth of rounding to Max.x to that text in long menus and small windows don't tend to display over the lower-right rounded area, which looks particularly glitchy.
        float border_top = ImMath.ImMax(window.WindowBorderSize * 0.5f - window.TitleBarHeight, 0.0f);
        ImRect bar_rect = window.MenuBarRect();
        ImRect clip_rect = new ImRect(ImMath.ImRound(bar_rect.Min.x + window.WindowBorderSize * 0.5f), ImMath.ImRound(bar_rect.Min.y + border_top), ImMath.ImRound(ImMath.ImMax(bar_rect.Min.x, bar_rect.Max.x - ImMath.ImMax(window.WindowRounding, window.WindowBorderSize * 0.5f))), ImMath.ImRound(bar_rect.Max.y));
        clip_rect.ClipWith(window.OuterRectClipped);
        PushClipRect(clip_rect.Min, clip_rect.Max, false);

        // We overwrite CursorMaxPos because BeginGroup sets it to CursorPos (essentially the .EmitItem hack in EndMenuBar() would need something analogous here, maybe a BeginGroupEx() with flags).
        window.DC.CursorPos = window.DC.CursorMaxPos = new ImVec2(bar_rect.Min.x + window.DC.MenuBarOffset.x, bar_rect.Min.y + window.DC.MenuBarOffset.y);
        window.DC.LayoutType = ImGuiLayoutType_Horizontal;
        window.DC.IsSameLine = false;
        window.DC.NavLayerCurrent = ImGuiNavLayer_Menu;
        window.DC.MenuBarAppending = true;
        AlignTextToFramePadding();
        return true;
    }

    // C++: void ImGui::EndMenuBar()   [imgui_widgets.cpp 9269]
    public static void EndMenuBar()
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;
        ImGuiContext g = GImGui;

        Debug.Assert((window.Flags & ImGuiWindowFlags_MenuBar) != 0);
        Debug.Assert(window.DC.MenuBarAppending);

        // Nav: When a move request within one of our child menu failed, capture the request to navigate among our siblings.
        if (NavMoveRequestButNoResultYet() && (g.NavMoveDir == ImGuiDir_Left || g.NavMoveDir == ImGuiDir_Right) && (g.NavWindow.Flags & ImGuiWindowFlags_ChildMenu) != 0)
        {
            // Try to find out if the request is for one of our child menu
            ImGuiWindow nav_earliest_child = g.NavWindow;
            while (nav_earliest_child.ParentWindow != null && (nav_earliest_child.ParentWindow.Flags & ImGuiWindowFlags_ChildMenu) != 0)
                nav_earliest_child = nav_earliest_child.ParentWindow;
            if (nav_earliest_child.ParentWindow == window && nav_earliest_child.DC.ParentLayoutType == ImGuiLayoutType_Horizontal && (g.NavMoveFlags & ImGuiNavMoveFlags_Forwarded) == 0)
            {
                // To do so we claim focus back, restore NavId and then process the movement request for yet another frame.
                // This involve a one-frame delay which isn't very problematic in this situation. We could remove it by scoring in advance for multiple window (probably not worth bothering)
                int layer = ImGuiNavLayer_Menu;
                Debug.Assert((window.DC.NavLayersActiveMaskNext & (1 << layer)) != 0); // Sanity check (FIXME: Seems unnecessary)
                FocusWindow(window);
                SetNavID(window.NavLastIds[layer], layer, 0, window.NavRectRel[layer]);
                // FIXME-NAV: How to deal with this when not using g.IO.ConfigNavCursorVisibleAuto?
                if (g.NavCursorVisible)
                {
                    g.NavCursorVisible = false; // Hide nav cursor for the current frame so we don't see the intermediary selection. Will be set again
                    g.NavCursorHideFrames = 2;
                }
                g.NavHighlightItemUnderNav = g.NavMousePosDirty = true;
                NavMoveRequestForward(g.NavMoveDir, g.NavMoveClipDir, g.NavMoveFlags, g.NavMoveScrollFlags); // Repeat
            }
        }
        else
        {
            NavMoveRequestTryWrapping(window, ImGuiNavMoveFlags_WrapX);
        }

        PopClipRect();
        PopID();
        window.DC.MenuBarOffset.x = window.DC.CursorPos.x - window.Pos.x; // Save horizontal position so next append can reuse it. This is kinda equivalent to a per-layer CursorPos.

        // FIXME: Extremely confusing, cleanup by (a) working on WorkRect stack system (b) not using a Group confusingly here.
        // (C++ uses a reference into g.GroupStack; the C# port mutates a copy and writes it back below.)
        ImGuiGroupData group_data = g.GroupStack.back();
        group_data.EmitItem = false;
        ImVec2 restore_cursor_max_pos = group_data.BackupCursorMaxPos;
        window.DC.IdealMaxPos.x = ImMath.ImMax(window.DC.IdealMaxPos.x, window.DC.CursorMaxPos.x - window.Scroll.x); // Convert ideal extents for scrolling layer equivalent.
        g.GroupStack.Data[g.GroupStack.Size - 1] = group_data;
        EndGroup(); // Restore position on layer 0 // FIXME: Misleading to use a group for that backup/restore
        window.DC.LayoutType = ImGuiLayoutType_Vertical;
        window.DC.IsSameLine = false;
        window.DC.NavLayerCurrent = ImGuiNavLayer_Main;
        window.DC.MenuBarAppending = false;
        window.DC.CursorMaxPos = restore_cursor_max_pos;
    }

    // Important: calling order matters!
    // FIXME: Somehow overlapping with docking tech.
    // FIXME: The "rect-cut" aspect of this could be formalized into a lower-level helper (rect-cut: https://halt.software/dead-simple-layouts)
    // C++: bool ImGui::BeginViewportSideBar(const char* name, ImGuiViewport* viewport_p, ImGuiDir dir, float axis_size, ImGuiWindowFlags window_flags)   [imgui_widgets.cpp 9342]
    // ('unsafe' because Begin() takes a bool* p_open parameter; this file's class part is not declared unsafe.)
    public static unsafe bool BeginViewportSideBar(string name, ImGuiViewport viewport_p, ImGuiDir dir, float axis_size, ImGuiWindowFlags window_flags)
    {
        Debug.Assert(dir != ImGuiDir_None);

        ImGuiWindow bar_window = FindWindowByName(name);
        ImGuiViewportP viewport = (ImGuiViewportP)(viewport_p != null ? viewport_p : GetMainViewport());
        if (bar_window == null || bar_window.BeginCount == 0)
        {
            // Calculate and set window size/position
            ImRect avail_rect = viewport.GetBuildWorkRect();
            int axis = (dir == ImGuiDir_Up || dir == ImGuiDir_Down) ? ImGuiAxis_Y : ImGuiAxis_X;
            ImVec2 pos = avail_rect.Min;
            if (dir == ImGuiDir_Right || dir == ImGuiDir_Down)
                pos[axis] = avail_rect.Max[axis] - axis_size;
            ImVec2 size = avail_rect.GetSize();
            size[axis] = axis_size;
            SetNextWindowPos(pos);
            SetNextWindowSize(size);

            // Report our size into work area (for next frame) using actual window size
            if (dir == ImGuiDir_Up || dir == ImGuiDir_Left)
                viewport.BuildWorkInsetMin[axis] += axis_size;
            else if (dir == ImGuiDir_Down || dir == ImGuiDir_Right)
                viewport.BuildWorkInsetMax[axis] += axis_size;
        }

        window_flags |= ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoDocking;
        SetNextWindowViewport(viewport.ID); // Enforce viewport so we don't create our own viewport when ImGuiConfigFlags_ViewportsNoMerge is set.
        PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
        PushStyleVar(ImGuiStyleVar_WindowMinSize, new ImVec2(0, 0)); // Lift normal size constraint
        bool is_open = Begin(name, window_flags);
        PopStyleVar(2);

        return is_open;
    }

    // C++: bool ImGui::BeginMainMenuBar()   [imgui_widgets.cpp 9377]
    public static bool BeginMainMenuBar()
    {
        ImGuiContext g = GImGui;
        ImGuiViewportP viewport = (ImGuiViewportP)GetMainViewport();

        // Notify of viewport change so GetFrameHeight() can be accurate in case of DPI change
        SetCurrentViewport(null, viewport);

        // For the main menu bar, which cannot be moved, we honor g.Style.DisplaySafeAreaPadding to ensure text can be visible on a TV set.
        // FIXME: This could be generalized as an opt-in way to clamp window->DC.CursorStartPos to avoid SafeArea?
        // FIXME: Consider removing support for safe area down the line... it's messy. Nowadays consoles have support for TV calibration in OS settings.
        g.NextWindowData.MenuBarOffsetMinVal = new ImVec2(g.Style.DisplaySafeAreaPadding.x, ImMath.ImMax(g.Style.DisplaySafeAreaPadding.y - g.Style.FramePadding.y, 0.0f));
        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_MenuBar;
        float height = GetFrameHeight();
        bool is_open = BeginViewportSideBar("##MainMenuBar", viewport, ImGuiDir_Up, height, window_flags);
        g.NextWindowData.MenuBarOffsetMinVal = new ImVec2(0.0f, 0.0f);
        if (!is_open)
        {
            End();
            return false;
        }

        // Temporarily disable _NoSavedSettings, in the off-chance that tables or child windows submitted within the menu-bar may want to use settings. (#8356)
        g.CurrentWindow.Flags &= ~ImGuiWindowFlags_NoSavedSettings;
        BeginMenuBar();
        return is_open;
    }

    // C++: void ImGui::EndMainMenuBar()   [imgui_widgets.cpp 9400]
    public static void EndMainMenuBar()
    {
        ImGuiContext g = GImGui;
        if (g.CurrentWindow.DC.MenuBarAppending == false) // IM_ASSERT_USER_ERROR_RET: not technically testing that it is the main menu bar
        {
            ImUtil.ImAssertUserError(false, "Calling EndMainMenuBar() not from a menu-bar!");
            return;
        }

        EndMenuBar();
        g.CurrentWindow.Flags |= ImGuiWindowFlags_NoSavedSettings; // Restore _NoSavedSettings (#8356)

        // When the user has left the menu layer (typically: closed menus through activation of an item), we restore focus to the previous window
        // FIXME: With this strategy we won't be able to restore a NULL focus.
        if (g.CurrentWindow == g.NavWindow && g.NavLayer == ImGuiNavLayer_Main && !g.NavAnyRequest && g.ActiveId == 0)
            FocusTopMostWindowUnderOne(g.NavWindow, null, null, ImGuiFocusRequestFlags_UnlessBelowModal | ImGuiFocusRequestFlags_RestoreFocusedChild);

        End();
    }

    // C++: static bool IsRootOfOpenMenuSet()   [imgui_widgets.cpp 9422]
    static bool IsRootOfOpenMenuSet()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if ((g.OpenPopupStack.Size <= g.BeginPopupStack.Size) || (window.Flags & ImGuiWindowFlags_ChildMenu) != 0)
            return false;

        // Initially we used 'upper_popup->OpenParentId == window->IDStack.back()' to differentiate multiple menu sets from each others
        // (e.g. inside menu bar vs loose menu items) based on parent ID.
        // This would however prevent the use of e.g. PushID() user code submitting menus.
        // Previously this worked between popup and a first child menu because the first child menu always had the _ChildWindow flag,
        // making hovering on parent popup possible while first child menu was focused - but this was generally a bug with other side effects.
        // Instead we don't treat Popup specifically (in order to consistently support menu features in them), maybe the first child menu of a Popup
        // doesn't have the _ChildWindow flag, and we rely on this IsRootOfOpenMenuSet() check to allow hovering between root window/popup and first child menu.
        // In the end, lack of ID check made it so we could no longer differentiate between separate menu sets. To compensate for that, we at least check parent window nav layer.
        // This fixes the most common case of menu opening on hover when moving between window content and menu bar. Multiple different menu sets in same nav layer would still
        // open on hover, but that should be a lesser problem, because if such menus are close in proximity in window content then it won't feel weird and if they are far apart
        // it likely won't be a problem anyone runs into.
        ImGuiPopupData upper_popup = g.OpenPopupStack.Data[g.BeginPopupStack.Size];
        if (window.DC.NavLayerCurrent != upper_popup.ParentNavLayer)
            return false;
        return upper_popup.Window != null && (upper_popup.Window.Flags & ImGuiWindowFlags_ChildMenu) != 0 && IsWindowChildOf(upper_popup.Window, window, true);
    }

    // C++: bool ImGui::BeginMenuEx(const char* label, const char* icon, bool enabled)   [imgui_widgets.cpp 9445]
    public static bool BeginMenuEx(string label, string icon, bool enabled)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);
        bool menu_is_open = IsPopupOpen(id, ImGuiPopupFlags_None);

        // Sub-menus are ChildWindow so that mouse can be hovering across them (otherwise top-most popup menu would steal focus and not allow hovering on parent menu)
        // The first menu in a hierarchy isn't so hovering doesn't get across (otherwise e.g. resizing borders with ImGuiButtonFlags_FlattenChildren would react), but top-most BeginMenu() will bypass that limitation.
        ImGuiWindowFlags window_flags = ImGuiWindowFlags_ChildMenu | ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoNavFocus;
        if ((window.Flags & ImGuiWindowFlags_ChildMenu) != 0)
            window_flags |= ImGuiWindowFlags_ChildWindow;

        // If a menu with same the ID was already submitted, we will append to it, matching the behavior of Begin().
        // We are relying on a O(N) search - so O(N log N) over the frame - which seems like the most efficient for the expected small amount of BeginMenu() calls per frame.
        // If somehow this is ever becoming a problem we can switch to use e.g. ImGuiStorage mapping key to last frame used.
        if (g.MenusIdSubmittedThisFrame.contains(id))
        {
            if (menu_is_open)
                menu_is_open = BeginPopupMenuEx(id, label, window_flags); // menu_is_open can be 'false' when the popup is completely clipped (e.g. zero size display)
            else
                g.NextWindowData.ClearFlags();          // we behave like Begin() and need to consume those values
            return menu_is_open;
        }

        // Tag menu as used. Next time BeginMenu() with same ID is called it will append to existing menu
        g.MenusIdSubmittedThisFrame.push_back(id);

        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        // Odd hack to allow hovering across menus of a same menu-set (otherwise we wouldn't be able to hover parent without always being a Child window)
        // This is only done for items for the menu set and not the full parent window.
        bool menuset_is_open = IsRootOfOpenMenuSet();
        if (menuset_is_open)
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_NoWindowHoverableCheck;

        // The reference position stored in popup_pos will be used by Begin() to find a suitable position for the child menu,
        // However the final position is going to be different! It is chosen by FindBestWindowPosForPopup().
        // e.g. Menus tend to overlap each other horizontally to amplify relative Z-ordering.
        ImVec2 popup_pos;
        ImVec2 pos = window.DC.CursorPos;
        PushID(label);
        if (!enabled)
            BeginDisabled(true);

        bool pressed;

        float backup_rounding = style.SelectableRounding;
        style.SelectableRounding = style.MenuItemRounding;
        ImGuiSelectableFlags selectable_flags = ImGuiSelectableFlags_NoAutoClosePopups | ImGuiSelectableFlags_SelectOnClick;
        ImGuiMenuColumns offsets = window.DC.MenuColumns;
        if (window.DC.LayoutType == ImGuiLayoutType_Horizontal)
        {
            // Menu inside a horizontal menu bar
            // Selectable extend their highlight by half ItemSpacing in each direction.
            // For ChildMenu, the popup position will be overwritten by the call to FindBestWindowPosForPopup() in Begin()
            window.DC.CursorPos.x += ImMath.ImTrunc(style.ItemSpacing.x * 0.5f);
            PushStyleVarX(ImGuiStyleVar_ItemSpacing, style.ItemSpacing.x * 2.0f);
            ImVec2 text_pos = new ImVec2(window.DC.CursorPos.x + offsets.OffsetLabel, pos.y + window.DC.CurrLineTextBaseOffset);
            pressed = Selectable("", menu_is_open, selectable_flags, label_size);
            LogSetNextTextDecoration("[", "]");
            RenderText(text_pos, label, label_end, false);
            PopStyleVar();
            window.DC.CursorPos.x += ImMath.ImTrunc(style.ItemSpacing.x * (-1.0f + 0.5f)); // -1 spacing to compensate the spacing added when Selectable() did a SameLine(). It would also work to call SameLine() ourselves after the PopStyleVar().
            popup_pos = new ImVec2(pos.x - 1.0f - ImMath.ImTrunc(style.ItemSpacing.x * 0.5f), text_pos.y - style.FramePadding.y + window.MenuBarHeight);
        }
        else
        {
            // Menu inside a regular/vertical menu
            // (In a typical menu window where all items are BeginMenu() or MenuItem() calls, extra_w will always be 0.0f.
            //  Only when they are other items sticking out we're going to add spacing, yet only register minimum width into the layout system.)
            float icon_w = (icon != null && icon.Length > 0) ? CalcTextSize(icon, -1, false).x : 0.0f;
            float checkmark_w = ImMath.ImTrunc(g.FontSize * 1.20f);
            float min_w = offsets.DeclColumns(icon_w, label_size.x, 0.0f, checkmark_w); // Feedback to next frame
            float extra_w = ImMath.ImMax(0.0f, GetContentRegionAvail().x - min_w);
            ImVec2 text_pos = new ImVec2(window.DC.CursorPos.x, pos.y + window.DC.CurrLineTextBaseOffset);
            pressed = Selectable("", menu_is_open, selectable_flags | ImGuiSelectableFlags_SpanAvailWidth, new ImVec2(min_w, label_size.y));
            LogSetNextTextDecoration("", ">");
            RenderText(new ImVec2(text_pos.x + offsets.OffsetLabel, text_pos.y), label, label_end, false);
            if (icon_w > 0.0f)
                RenderText(new ImVec2(text_pos.x + offsets.OffsetIcon, text_pos.y), icon);
            RenderArrow(window.DrawList, new ImVec2(text_pos.x + offsets.OffsetMark + extra_w + g.FontSize * 0.30f, text_pos.y), GetColorU32(ImGuiCol_Text), ImGuiDir_Right);
            popup_pos = new ImVec2(pos.x, text_pos.y - style.WindowPadding.y);
        }
        style.SelectableRounding = backup_rounding;
        if (!enabled)
            EndDisabled();

        // Once dragged, release ActiveId + key ownership. This is to allow the idiom of mouse down a menu, dragging elsewhere, up on some other MenuItem(). (#8233, #9394)
        // Could move logic into lower-level ImGuiButtonFlags_AutoReleaseActiveId + ImGuiButtonFlags_AutoReleaseKeyOwner? Easier once we get rid of the Selectable() middle-man here.
        if (g.ActiveId == id && g.HoveredId != id && g.ActiveIdSource == ImGuiInputSource_Mouse && IsMouseDragging(0))
        {
            ClearActiveID();
            SetKeyOwner(ImGuiKey_MouseLeft, ImGuiKeyOwner_NoOwner);
        }

        bool hovered = (g.HoveredId == id) && enabled && !g.NavHighlightItemUnderNav;

        bool want_open = false;
        bool want_open_nav_init = false;
        bool want_close = false;
        if (window.DC.LayoutType == ImGuiLayoutType_Vertical) // (window->Flags & (ImGuiWindowFlags_Popup|ImGuiWindowFlags_ChildMenu))
        {
            // Close menu when not hovering it anymore unless we are moving roughly in the direction of the menu
            // Implement http://bjk5.com/post/44698559168/breaking-down-amazons-mega-dropdown to avoid using timers, so menus feels more reactive.
            bool moving_toward_child_menu = false;
            bool has_child_popup = g.BeginPopupStack.Size < g.OpenPopupStack.Size;
            ImGuiPopupData child_popup = has_child_popup ? g.OpenPopupStack.Data[g.BeginPopupStack.Size] : default; // Popup candidate (testing below)
            ImGuiWindow child_menu_window = (has_child_popup && child_popup.Window != null && child_popup.Window.ParentWindow == window) ? child_popup.Window : null;
            if (g.HoveredWindow == window && child_menu_window != null)
            {
                float ref_unit = g.FontSize; // FIXME-DPI
                float child_dir = (window.Pos.x < child_menu_window.Pos.x) ? 1.0f : -1.0f;
                ImRect next_window_rect = child_menu_window.Rect();
                ImVec2 ta = g.IO.MousePos - g.IO.MouseDelta;
                ImVec2 tb = (child_dir > 0.0f) ? next_window_rect.GetTL() : next_window_rect.GetTR();
                ImVec2 tc = (child_dir > 0.0f) ? next_window_rect.GetBL() : next_window_rect.GetBR();
                float pad_farmost_h = ImMath.ImClamp(MathF.Abs(ta.x - tb.x) * 0.30f, ref_unit * 0.5f, ref_unit * 2.5f); // Add a bit of extra slack. (C++ ImFabs)
                ta.x += child_dir * -0.5f;
                tb.x += child_dir * ref_unit;
                tc.x += child_dir * ref_unit;
                tb.y = ta.y + ImMath.ImMax((tb.y - pad_farmost_h) - ta.y, -ref_unit * 8.0f); // Triangle has maximum height to limit the slope and the bias toward large sub-menus
                tc.y = ta.y + ImMath.ImMin((tc.y + pad_farmost_h) - ta.y, +ref_unit * 8.0f);
                moving_toward_child_menu = ImGuiCore.ImTriangleContainsPoint(ta, tb, tc, g.IO.MousePos);
                //GetForegroundDrawList()->AddTriangleFilled(ta, tb, tc, moving_toward_child_menu ? IM_COL32(0,128,0,128) : IM_COL32(128,0,0,128)); // [DEBUG] omitted
            }

            // The 'HovereWindow == window' check creates an inconsistency (e.g. moving away from menu slowly tends to hit same window, whereas moving away fast does not)
            // But we also need to not close the top-menu menu when moving over void. Perhaps we should extend the triangle check to a larger polygon.
            // (Remember to test this on BeginPopup("A")->BeginMenu("B") sequence which behaves slightly differently as B isn't a Child of A and hovering isn't shared.)
            if (menu_is_open && !hovered && g.HoveredWindow == window && !moving_toward_child_menu && !g.NavHighlightItemUnderNav && g.ActiveId == 0)
                want_close = true;

            // Open
            // (note: at this point 'hovered' actually includes the NavDisableMouseHover == false test)
            if (!menu_is_open && pressed) // Click/activate to open
                want_open = true;
            else if (!menu_is_open && hovered && !moving_toward_child_menu) // Hover to open
                want_open = true;
            else if (!menu_is_open && hovered && g.HoveredIdTimer >= 0.30f && g.MouseStationaryTimer >= 0.30f) // Hover to open (timer fallback)
                want_open = true;
            if (g.NavId == id && g.NavMoveDir == ImGuiDir_Right) // Nav-Right to open
            {
                want_open = want_open_nav_init = true;
                NavMoveRequestCancel();
                SetNavCursorVisibleAfterMove();
            }
        }
        else
        {
            // Menu bar
            if (menu_is_open && pressed && menuset_is_open) // Click an open menu again to close it
            {
                want_close = true;
                want_open = menu_is_open = false;
            }
            else if (pressed || (hovered && menuset_is_open && !menu_is_open)) // First click to open, then hover to open others
            {
                want_open = true;
            }
            else if (g.NavId == id && g.NavMoveDir == ImGuiDir_Down) // Nav-Down to open
            {
                want_open = true;
                NavMoveRequestCancel();
            }
        }

        if (!enabled) // explicitly close if an open menu becomes disabled, facilitate users code a lot in pattern such as 'if (BeginMenu("options", has_object)) { ..use object.. }'
            want_close = true;
        if (want_close && IsPopupOpen(id, ImGuiPopupFlags_None))
            ClosePopupToLevel(g.BeginPopupStack.Size, true);

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        PopID();

        if (g.ActiveId == id && want_open)
            g.ActiveIdNoClearOnFocusLoss = true;

        if (want_open && !menu_is_open && g.OpenPopupStack.Size > g.BeginPopupStack.Size)
        {
            // Don't reopen/recycle same menu level in the same frame if it is a different menu ID, first close the other menu and yield for a frame.
            OpenPopup(label);
        }
        else if (want_open)
        {
            menu_is_open = true;
            OpenPopup(label, ImGuiPopupFlags_NoReopen);// | (want_open_nav_init ? ImGuiPopupFlags_NoReopenAlwaysNavInit : 0));
        }

        if (menu_is_open)
        {
            ImGuiLastItemData last_item_in_parent = g.LastItemData;
            SetNextWindowPos(popup_pos, ImGuiCond_Always);                  // Note: misleading: the value will serve as reference for FindBestWindowPosForPopup(), not actual pos.
            PushStyleVar(ImGuiStyleVar_ChildRounding, style.PopupRounding); // First level will use _PopupRounding, subsequent will use _ChildRounding
            menu_is_open = BeginPopupMenuEx(id, label, window_flags); // menu_is_open may be 'false' when the popup is completely clipped (e.g. zero size display)
            PopStyleVar();
            if (menu_is_open)
            {
                // Implement what ImGuiPopupFlags_NoReopenAlwaysNavInit would do:
                // Perform an init request in the case the popup was already open (via a previous mouse hover)
                if (want_open && want_open_nav_init && !g.NavInitRequest)
                {
                    FocusWindow(g.CurrentWindow, ImGuiFocusRequestFlags_UnlessBelowModal);
                    NavInitWindow(g.CurrentWindow, false);
                }

                // Restore LastItemData so IsItemXXXX functions can work after BeginMenu()/EndMenu()
                // (This fixes using IsItemClicked() and IsItemHovered(), but IsItemHovered() also relies on its support for ImGuiItemFlags_NoWindowHoverableCheck)
                g.LastItemData = last_item_in_parent;
                if (g.HoveredWindow == window)
                    g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredWindow;
            }
        }
        else
        {
            g.NextWindowData.ClearFlags(); // We behave like Begin() and need to consume those values
        }

        return menu_is_open;
    }

    // C++: bool ImGui::BeginMenu(const char* label, bool enabled)   [imgui_widgets.cpp 9664]
    public static bool BeginMenu(string label, bool enabled = true)
    {
        return BeginMenuEx(label, null, enabled);
    }

    // C++: void ImGui::EndMenu()   [imgui_widgets.cpp 9669]
    public static void EndMenu()
    {
        // Nav: When a left move request our menu failed, close ourselves.
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if ((window.Flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_ChildMenu)) != (ImGuiWindowFlags_Popup | ImGuiWindowFlags_ChildMenu)) // IM_ASSERT_USER_ERROR_RET
        {
            ImUtil.ImAssertUserError(false, "Calling EndMenu() in wrong window!");
            return;
        }

        ImGuiWindow parent_window = window.ParentWindow;  // Should always be != NULL is we passed assert.
        if (window.BeginCount == window.BeginCountPreviousFrame)
            if (g.NavMoveDir == ImGuiDir_Left && NavMoveRequestButNoResultYet())
                if (g.NavWindow != null && (g.NavWindow.RootWindowForNav == window) && parent_window.DC.LayoutType == ImGuiLayoutType_Vertical)
                {
                    ClosePopupToLevel(g.BeginPopupStack.Size - 1, true);
                    NavMoveRequestCancel();
                }

        EndPopup();
    }

    // C++: bool ImGui::MenuItemEx(const char* label, const char* icon, const char* shortcut, bool selected, bool enabled)   [imgui_widgets.cpp 9690]
    public static bool MenuItemEx(string label, string icon, string shortcut, bool selected, bool enabled)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImVec2 pos = window.DC.CursorPos;
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        // See BeginMenuEx() for comments about this.
        bool menuset_is_open = IsRootOfOpenMenuSet();
        if (menuset_is_open)
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_NoWindowHoverableCheck;

        // We've been using the equivalent of ImGuiSelectableFlags_SetNavIdOnHover on all Selectable() since early Nav system days (commit 43ee5d73),
        // but I am unsure whether this should be kept at all. For now moved it to be an opt-in feature used by menus only.
        bool pressed;
        PushID(label);
        if (!enabled)
            BeginDisabled(true);

        // We use ImGuiSelectableFlags_NoSetKeyOwner to allow down on one menu item, move, up on another.
        float backup_rounding = style.SelectableRounding;
        style.SelectableRounding = style.MenuItemRounding;
        ImGuiSelectableFlags selectable_flags = ImGuiSelectableFlags_SelectOnRelease | ImGuiSelectableFlags_SetNavIdOnHover;
        ImGuiMenuColumns offsets = window.DC.MenuColumns;
        if (window.DC.LayoutType == ImGuiLayoutType_Horizontal)
        {
            // Mimic the exact layout spacing of BeginMenu() to allow MenuItem() inside a menu bar, which is a little misleading but may be useful
            // Note that in this situation: we don't render the shortcut, we render a highlight instead of the selected tick mark.
            window.DC.CursorPos.x += ImMath.ImTrunc(style.ItemSpacing.x * 0.5f);
            ImVec2 text_pos = new ImVec2(window.DC.CursorPos.x + offsets.OffsetLabel, window.DC.CursorPos.y + window.DC.CurrLineTextBaseOffset);
            PushStyleVarX(ImGuiStyleVar_ItemSpacing, style.ItemSpacing.x * 2.0f);
            pressed = Selectable("", selected, selectable_flags, new ImVec2(label_size.x, 0.0f));
            PopStyleVar();
            if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Visible) != 0)
                RenderText(text_pos, label, label_end, false);
            window.DC.CursorPos.x += ImMath.ImTrunc(style.ItemSpacing.x * (-1.0f + 0.5f)); // -1 spacing to compensate the spacing added when Selectable() did a SameLine(). It would also work to call SameLine() ourselves after the PopStyleVar().
        }
        else
        {
            // Menu item inside a vertical menu
            // (In a typical menu window where all items are BeginMenu() or MenuItem() calls, extra_w will always be 0.0f.
            //  Only when they are other items sticking out we're going to add spacing, yet only register minimum width into the layout system.)
            float icon_w = (icon != null && icon.Length > 0) ? CalcTextSize(icon, -1, false).x : 0.0f;
            float shortcut_w = (shortcut != null && shortcut.Length > 0) ? CalcTextSize(shortcut, -1, false).x : 0.0f;
            float checkmark_w = ImMath.ImTrunc(g.FontSize * 1.20f); // FIXME: Always accounted for, even in a menu with none, because 'selected' has no neutral setting.
            float min_w = offsets.DeclColumns(icon_w, label_size.x, shortcut_w, checkmark_w); // Feedback for next frame
            float stretch_w = ImMath.ImMax(0.0f, GetContentRegionAvail().x - min_w);
            ImVec2 text_pos = new ImVec2(pos.x, pos.y + window.DC.CurrLineTextBaseOffset);
            pressed = Selectable("", false, selectable_flags | ImGuiSelectableFlags_SpanAvailWidth, new ImVec2(min_w, label_size.y));
            if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Visible) != 0)
            {
                RenderText(text_pos + new ImVec2(offsets.OffsetLabel, 0.0f), label, label_end, false);
                if (icon_w > 0.0f)
                    RenderText(text_pos + new ImVec2(offsets.OffsetIcon, 0.0f), icon);
                if (shortcut_w > 0.0f)
                {
                    PushStyleColor(ImGuiCol_Text, style.Colors[ImGuiCol_TextDisabled]);
                    LogSetNextTextDecoration("(", ")");
                    RenderText(text_pos + new ImVec2(offsets.OffsetShortcut + stretch_w, 0.0f), shortcut, -1, false);
                    PopStyleColor();
                }
                if (selected)
                    RenderCheckMark(window.DrawList, text_pos + new ImVec2(offsets.OffsetMark + stretch_w + g.FontSize * 0.40f, g.FontSize * 0.134f * 0.5f), GetColorU32(ImGuiCol_Text), g.FontSize * 0.866f);
            }
        }
        style.SelectableRounding = backup_rounding;

        // Once dragged, release ActiveId + key ownership. This is to allow the idiom of mouse down a menu, dragging elsewhere, up on some other MenuItem(). (#8233, #9394)
        // Could move logic into lower-level ImGuiButtonFlags_AutoReleaseActiveId + ImGuiButtonFlags_AutoReleaseKeyOwner? Easier once we get rid of the Selectable() middle-man here.
        ImGuiID id = g.LastItemData.ID;
        if (g.ActiveId == id && g.HoveredId != id && g.ActiveIdSource == ImGuiInputSource_Mouse && IsMouseDragging(0))
        {
            ClearActiveID();
            SetKeyOwner(ImGuiKey_MouseLeft, ImGuiKeyOwner_NoOwner);
        }

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        if (!enabled)
            EndDisabled();
        PopID();

        return pressed;
    }

    // C++: bool ImGui::MenuItem(const char* label, const char* shortcut, bool selected, bool enabled)   [imgui_widgets.cpp 9773]
    public static bool MenuItem(string label, string shortcut = null, bool selected = false, bool enabled = true)
    {
        return MenuItemEx(label, null, shortcut, selected, enabled);
    }

    // C++: bool ImGui::MenuItem(const char* label, const char* shortcut, bool* p_selected, bool enabled)   [imgui_widgets.cpp 9779]
    public static bool MenuItem(string label, string shortcut, ref bool p_selected, bool enabled = true)
    {
        if (MenuItemEx(label, null, shortcut, p_selected, enabled))
        {
            p_selected = !p_selected;
            return true;
        }
        return false;
    }
}

//-------------------------------------------------------------------------
// [SECTION] Widgets: Multi-Select helpers
//-------------------------------------------------------------------------
// - ImGuiSelectionBasicStorage
// - ImGuiSelectionExternalStorage
//-------------------------------------------------------------------------

// Optional helper to store multi-selection state + apply multi-selection requests. (imgui.h)
// - Used by our demos and provided as a convenience to easily implement basic multi-selection.
// - Iterate selection with 'int it = -1; ImGuiID id; while (selection.GetNextSelectedItem(ref it, out id)) { ... }'
//   (C++ uses 'void* it = NULL'; the C# port uses an index into _Storage.Data with -1 meaning 'not started'.)
// - USING THIS IS NOT MANDATORY. This is only a helper and not a required API.
// In ImGuiSelectionBasicStorage we:
// - always use indices in the multi-selection API (passed to SetNextItemSelectionUserData(), retrieved in ImGuiMultiSelectIO)
// - use the AdapterIndexToStorageId() indirection layer to abstract how persistent selection data is derived from an index.
// - use decently optimized logic to allow queries and insertion of very large selection sets.
// - do not preserve selection order.
public class ImGuiSelectionBasicStorage
{
    // Members
    public int Size;                //          // Number of selected items, maintained by this helper.
    public bool PreserveOrder;      // = false  // GetNextSelectedItem() will return ordered selection (currently implemented by two additional sorts of selection. Could be improved)
    public object UserData;         // = NULL   // User data for use by adapter function        // e.g. selection.UserData = (object)my_items;
    public Func<ImGuiSelectionBasicStorage, int, ImGuiID> AdapterIndexToStorageId;  // e.g. selection.AdapterIndexToStorageId = (self, idx) => ((MyItems[])self.UserData)[idx].ID;
    public int _SelectionOrder;     // [Internal] Increasing counter to store selection order
    public ImGuiStorage _Storage = new ImGuiStorage(); // [Internal] Selection set. Think of this as similar to e.g. std::set<ImGuiID>. Prefer not accessing directly: iterate with GetNextSelectedItem().

    // C++: ImGuiSelectionBasicStorage::ImGuiSelectionBasicStorage()   [imgui_widgets.cpp 8680]
    public ImGuiSelectionBasicStorage()
    {
        Size = 0;
        PreserveOrder = false;
        UserData = null;
        AdapterIndexToStorageId = (ImGuiSelectionBasicStorage self, int idx) => (ImGuiID)idx;
        _SelectionOrder = 1; // Always >0
    }

    // C++: void ImGuiSelectionBasicStorage::Clear()   [imgui_widgets.cpp 8689]
    public void Clear()
    {
        Size = 0;
        _SelectionOrder = 1; // Always >0
        _Storage.Data.resize(0);
    }

    // C++: void ImGuiSelectionBasicStorage::Swap(ImGuiSelectionBasicStorage& r)   [imgui_widgets.cpp 8695]
    public void Swap(ImGuiSelectionBasicStorage r)
    {
        ImMath.ImSwap(ref Size, ref r.Size);
        ImMath.ImSwap(ref _SelectionOrder, ref r._SelectionOrder);
        ImVector<ImGuiStoragePair> tmp = _Storage.Data;
        _Storage.Data = r._Storage.Data;
        r._Storage.Data = tmp;
    }

    // C++: bool ImGuiSelectionBasicStorage::Contains(ImGuiID id) const   [imgui_widgets.cpp 8703]
    public bool Contains(ImGuiID id)
    {
        return _Storage.GetInt(id, 0) != 0;
    }

    // C++: static int IMGUI_CDECL PairComparerByValueInt(const void* lhs, const void* rhs)   [imgui_widgets.cpp 8708]
    // (Inlined as the comparer in SortByValueInt() below.)

    // ~ImGuiStorage::BuildSortByKey() but by val_i (C++ uses ImQsort() + PairComparerByValueInt()).
    private void SortByValueInt()
    {
        var pairs = new ImGuiStoragePair[_Storage.Data.Size];
        Array.Copy(_Storage.Data.Data, pairs, _Storage.Data.Size);
        Array.Sort(pairs, (a, b) => a.val_i > b.val_i ? +1 : a.val_i < b.val_i ? -1 : 0);
        Array.Copy(pairs, _Storage.Data.Data, pairs.Length);
    }

    // GetNextSelectedItem() is an abstraction allowing us to change our underlying actual storage system without impacting user.
    // (e.g. store unselected vs compact down, compact down on demand, use raw ImVector<ImGuiID> instead of ImGuiStorage...)
    // C++: bool ImGuiSelectionBasicStorage::GetNextSelectedItem(void** opaque_it, ImGuiID* out_id)   [imgui_widgets.cpp 8716]
    // C# adaptation: 'opaque_it' is the next index into _Storage.Data (-1 == C++ NULL, i.e. iteration not started).
    public bool GetNextSelectedItem(ref int opaque_it, out ImGuiID out_id)
    {
        int it = opaque_it;
        int it_end = _Storage.Data.Size;
        if (PreserveOrder && it == -1 && it_end != 0)
            SortByValueInt(); // ~ImGuiStorage::BuildSortByValueInt()
        if (it == -1)
            it = 0;
        Debug.Assert(it >= 0 && it <= it_end);
        if (it != it_end)
            while (_Storage.Data.Data[it].val_i == 0 && it < it_end)
                it++;
        bool has_more = (it != it_end);
        opaque_it = has_more ? it + 1 : it;
        out_id = has_more ? _Storage.Data.Data[it].key : 0;
        if (PreserveOrder && !has_more)
            _Storage.BuildSortByKey();
        return has_more;
    }

    // C++: void ImGuiSelectionBasicStorage::SetItemSelected(ImGuiID id, bool selected)   [imgui_widgets.cpp 8733]
    public void SetItemSelected(ImGuiID id, bool selected)
    {
        ref int p_int = ref _Storage.GetIntRef(id, 0);
        if (selected && p_int == 0) { p_int = _SelectionOrder++; Size++; }
        else if (!selected && p_int != 0) { p_int = 0; Size--; }
    }

    // Optimized for batch edits (with same value of 'selected')
    // C++: static void ImGuiSelectionBasicStorage_BatchSetItemSelected(ImGuiSelectionBasicStorage* selection, ImGuiID id, bool selected, int size_before_amends, int selection_order)   [imgui_widgets.cpp 8741]
    static void ImGuiSelectionBasicStorage_BatchSetItemSelected(ImGuiSelectionBasicStorage selection, ImGuiID id, bool selected, int size_before_amends, int selection_order)
    {
        ImGuiStorage storage = selection._Storage;
        int it = ImLowerBound(storage.Data, size_before_amends, id);
        bool is_contained = (it != size_before_amends) && (storage.Data.Data[it].key == id);
        if (selected == (is_contained && storage.Data.Data[it].val_i != 0))
            return;
        if (selected && !is_contained)
            storage.Data.push_back(new ImGuiStoragePair(id, selection_order)); // Push unsorted at end of vector, will be sorted in SelectionMultiAmendsFinish()
        else if (is_contained)
            storage.Data.Data[it].val_i = selected ? selection_order : 0; // Modify in-place.
        selection.Size += selected ? +1 : -1;
    }

    // C++: static void ImGuiSelectionBasicStorage_BatchFinish(ImGuiSelectionBasicStorage* selection, bool selected, int size_before_amends)   [imgui_widgets.cpp 8756]
    static void ImGuiSelectionBasicStorage_BatchFinish(ImGuiSelectionBasicStorage selection, bool selected, int size_before_amends)
    {
        ImGuiStorage storage = selection._Storage;
        if (selected && selection.Size != size_before_amends)
            storage.BuildSortByKey(); // When done selecting: sort everything
    }

    // C++ imgui_internal.h: template<typename T> static inline T* ImLowerBound(T* in_array, const T* in_array_end, const T& v)
    // (C# adaptation over an ImVector<ImGuiStoragePair> prefix of 'count' elements.)
    static int ImLowerBound(ImVector<ImGuiStoragePair> data, int count, ImGuiID key)
    {
        int lo = 0;
        int hi = count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (data.Data[mid].key < key)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    // Apply requests coming from BeginMultiSelect() and EndMultiSelect().
    // - Enable 'Demo->Tools->Debug Log->Selection' to see selection requests as they happen.
    // - Honoring SetRange requests requires that you can iterate/interpolate between RangeFirstItem and RangeLastItem.
    //   - In this demo we often submit indices to SetNextItemSelectionUserData() + store the same indices in persistent selection.
    //   - Your code may do differently. If you store pointers or objects ID in ImGuiSelectionUserData you may need to perform
    //     a lookup in order to have some way to iterate/interpolate between two items.
    // - A full-featured application is likely to allow search/filtering which is likely to lead to using indices
    //   and constructing a view index <> object id/ptr data structure anyway.
    // WHEN YOUR APPLICATION SETTLES ON A CHOICE, YOU WILL PROBABLY PREFER TO GET RID OF THIS UNNECESSARY 'ImGuiSelectionBasicStorage' INDIRECTION LOGIC.
    // Notice that with the simplest adapter (using indices everywhere), all functions return their parameters.
    // The most simple implementation (using indices everywhere) would look like:
    //   for (ImGuiSelectionRequest& req : ms_io->Requests)
    //   {
    //      if (req.Type == ImGuiSelectionRequestType_SetAll)    { Clear(); if (req.Selected) { for (int n = 0; n < items_count; n++) { SetItemSelected(n, true); } }
    //      if (req.Type == ImGuiSelectionRequestType_SetRange)  { for (int n = (int)ms_io->RangeFirstItem; n <= (int)ms_io->RangeLastItem; n++) { SetItemSelected(n, ms_io->Selected); } }
    //   }
    // C++: void ImGuiSelectionBasicStorage::ApplyRequests(ImGuiMultiSelectIO* ms_io)   [imgui_widgets.cpp 8777]
    public void ApplyRequests(ImGuiMultiSelectIO ms_io)
    {
        // For convenience we obtain ItemsCount as passed to BeginMultiSelect(), which is optional.
        // It makes sense when using ImGuiSelectionBasicStorage to simply pass your items count to BeginMultiSelect().
        // Other scheme may handle SetAll differently.
        Debug.Assert(ms_io.ItemsCount != -1, "Missing value for items_count in BeginMultiSelect() call!");
        Debug.Assert(AdapterIndexToStorageId != null);

        // This is optimized/specialized to cope with very large selections (e.g. 100k+ items)
        // - A simpler version could call SetItemSelected() directly instead of ImGuiSelectionBasicStorage_BatchSetItemSelected() + ImGuiSelectionBasicStorage_BatchFinish().
        // - Optimized select can append unsorted, then sort in a second pass. Optimized unselect can clear in-place then compact in a second pass.
        // - A more optimal version wouldn't even use ImGuiStorage but directly a ImVector<ImGuiID> to reduce bandwidth, but this is a reasonable trade off to reuse code.
        // - There are many ways this could be better optimized. The worse case scenario being: using BoxSelect2d in a grid, box-select scrolling down while wiggling
        //   left and right: it affects coarse clipping + can emit multiple SetRange with 1 item each.
        // FIXME-OPT: For each block of consecutive SetRange request:
        // - add all requests to a sorted list, store ID, selected, offset in ImGuiStorage.
        // - rewrite sorted storage a single time.
        for (int req_i = 0; req_i < ms_io.Requests.Size; req_i++)
        {
            ImGuiSelectionRequest req = ms_io.Requests[req_i];
            if (req.Type == ImGuiSelectionRequestType_SetAll)
            {
                Clear();
                if (req.Selected)
                {
                    _Storage.Data.reserve(ms_io.ItemsCount);
                    int size_before_amends = _Storage.Data.Size;
                    for (int idx = 0; idx < ms_io.ItemsCount; idx++, _SelectionOrder++)
                        ImGuiSelectionBasicStorage_BatchSetItemSelected(this, GetStorageIdFromIndex(idx), req.Selected, size_before_amends, _SelectionOrder);
                    ImGuiSelectionBasicStorage_BatchFinish(this, req.Selected, size_before_amends);
                }
            }
            else if (req.Type == ImGuiSelectionRequestType_SetRange)
            {
                int selection_changes = (int)req.RangeLastItem - (int)req.RangeFirstItem + 1;
                //ImGuiContext& g = *GImGui; IMGUI_DEBUG_LOG_SELECTION("Req %d/%d: set %d to %d\n", ...); // omitted
                if (selection_changes == 1 || (selection_changes < Size / 100))
                {
                    // Multiple sorted insertion + copy likely to be faster.
                    // Technically we could do a single copy with a little more work (sort sequential SetRange requests)
                    for (int idx = (int)req.RangeFirstItem; idx <= (int)req.RangeLastItem; idx++)
                        SetItemSelected(GetStorageIdFromIndex(idx), req.Selected);
                }
                else
                {
                    // Append insertion + single sort likely be faster.
                    // Use req.RangeDirection to set order field so that Shift+Clicking from 1 to 5 is different than Shift+Clicking from 5 to 1
                    int size_before_amends = _Storage.Data.Size;
                    int selection_order = _SelectionOrder + ((req.RangeDirection < 0) ? selection_changes - 1 : 0);
                    for (int idx = (int)req.RangeFirstItem; idx <= (int)req.RangeLastItem; idx++, selection_order += req.RangeDirection)
                        ImGuiSelectionBasicStorage_BatchSetItemSelected(this, GetStorageIdFromIndex(idx), req.Selected, size_before_amends, selection_order);
                    if (req.Selected)
                        _SelectionOrder += selection_changes;
                    ImGuiSelectionBasicStorage_BatchFinish(this, req.Selected, size_before_amends);
                }
            }
        }
    }

    // C++: inline ImGuiID ImGuiSelectionBasicStorage::GetStorageIdFromIndex(int idx)   [imgui.h]
    public ImGuiID GetStorageIdFromIndex(int idx)
    {
        return AdapterIndexToStorageId(this, idx);
    }
}

// Optional helper to apply multi-selection requests to existing randomly accessible storage.
// Convenient if you want to quickly wire multi-select API on e.g. an array of bool or items storing their own selection state.
public class ImGuiSelectionExternalStorage
{
    // Members
    public object UserData;         // User data for use by adapter function                                // e.g. selection.UserData = (object)my_items;
    public Action<ImGuiSelectionExternalStorage, int, bool> AdapterSetItemSelected;  // e.g. AdapterSetItemSelected = (self, idx, selected) => { ((MyItems[])self.UserData)[idx].Selected = selected; }

    // C++: ImGuiSelectionExternalStorage::ImGuiSelectionExternalStorage()   [imgui_widgets.cpp 8843]
    public ImGuiSelectionExternalStorage()
    {
        UserData = null;
        AdapterSetItemSelected = null;
    }

    // Apply requests coming from BeginMultiSelect() and EndMultiSelect().
    // We also pull 'ms_io->ItemsCount' as passed for BeginMultiSelect() for consistency with ImGuiSelectionBasicStorage
    // This makes no assumption about underlying storage.
    // C++: void ImGuiSelectionExternalStorage::ApplyRequests(ImGuiMultiSelectIO* ms_io)   [imgui_widgets.cpp 8851]
    public void ApplyRequests(ImGuiMultiSelectIO ms_io)
    {
        Debug.Assert(AdapterSetItemSelected != null);
        for (int req_i = 0; req_i < ms_io.Requests.Size; req_i++)
        {
            ImGuiSelectionRequest req = ms_io.Requests[req_i];
            if (req.Type == ImGuiSelectionRequestType_SetAll)
                for (int idx = 0; idx < ms_io.ItemsCount; idx++)
                    AdapterSetItemSelected(this, idx, req.Selected);
            if (req.Type == ImGuiSelectionRequestType_SetRange)
                for (int idx = (int)req.RangeFirstItem; idx <= (int)req.RangeLastItem; idx++)
                    AdapterSetItemSelected(this, idx, req.Selected);
        }
    }
}
