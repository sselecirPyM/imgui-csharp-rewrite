// Port of Dear ImGui imgui.cpp [SECTION] MAIN CODE (lines 4665..7600 + remainder of Begin()).
// Window lifecycle, active/hovered id, item status, NewFrame/EndFrame/Render, Begin/BeginChild.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    // static void SetCurrentWindow(ImGuiWindow* window) (imgui.cpp)
    public static void SetCurrentWindow(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        g.CurrentWindow = window;
        g.StackSizesInBeginForCurrentWindow = (window != null) ? g.CurrentWindowStack.back().StackSizesInBegin : default;
        g.CurrentTable = (window != null && window.DC.CurrentTableIdx != -1) ? g.Tables.GetByIndex(window.DC.CurrentTableIdx) : null;
        if (window != null)
        {
            if ((g.IO.BackendFlags & ImGuiBackendFlags_RendererHasTextures) != 0)
            {
                g.CurrentPixelDensity = window.Viewport.FramebufferScale.x; // == SetPixelDensity()
                Debug.Assert(g.CurrentPixelDensity != 0.0f);
            }
            bool backup_skip_items = window.SkipItems;
            window.SkipItems = false;
            UpdateCurrentFontSize(0.0f);
            window.SkipItems = backup_skip_items;
            NavUpdateCurrentWindowIsScrollPushableX();
        }
    }

    // void ImGui::GcCompactTransientMiscBuffers()
    public static void GcCompactTransientMiscBuffers()
    {
        ImGuiContext g = GImGui;
        g.ItemFlagsStack.clear();
        g.GroupStack.clear();
        g.InputTextLineIndex.clear();
        g.MultiSelectTempDataStacked = 0;
        g.MultiSelectTempData.clear();
        TableGcCompactSettings();
        for (int i = 0; i < g.FontAtlases.Size; i++)
            g.FontAtlases[i].CompactCache();
    }

    // Free up/compact internal window buffers, we can use this when a window becomes unused.
    // Not freed:
    // - ImGuiWindow, ImGuiWindowSettings, Name, StateStorage, ColumnsStorage (may hold useful data)
    // This should have no noticeable visual effect. When the window reappear however, expect new allocation/buffer growth/copy cost.
    // FIXME: Consider exposing of elaborating GC policy, e.g. being able to trim excessive ImDrawList gaps. (#9303)
    public static void GcCompactTransientWindowBuffers(ImGuiWindow window)
    {
        window.MemoryCompacted = true;
        window.MemoryDrawListIdxCapacity = ImMath.ImMin((int)(window.DrawList.IdxBuffer.Size * 1.05f), window.DrawList.IdxBuffer.Capacity);
        window.MemoryDrawListVtxCapacity = ImMath.ImMin((int)(window.DrawList.VtxBuffer.Size * 1.05f), window.DrawList.VtxBuffer.Capacity);
        window.IDStack.clear();
        window.DrawList._ClearFreeMemory();
        window.DC.ChildWindows.clear();
        window.DC.ItemWidthStack.clear();
        window.DC.TextWrapPosStack.clear();
    }

    public static void GcAwakeTransientWindowBuffers(ImGuiWindow window)
    {
        // We stored capacity of the ImDrawList buffer to reduce growth-caused allocation/copy when awakening.
        // The other buffers tends to amortize much faster.
        window.MemoryCompacted = false;
        window.DrawList.IdxBuffer.reserve(window.MemoryDrawListIdxCapacity);
        window.DrawList.VtxBuffer.reserve(window.MemoryDrawListVtxCapacity);
        window.MemoryDrawListIdxCapacity = window.MemoryDrawListVtxCapacity = 0;
    }

    // void ImGui::SetActiveID(ImGuiID id, ImGuiWindow* window)
    public static void SetActiveID(ImGuiID id, ImGuiWindow window)
    {
        ImGuiContext g = GImGui;

        // Clear previous active id
        if (g.ActiveId != 0)
        {
            // Store deactivate data
            // (ImGuiDeactivatedItemData is embedded in the context; mutate in place as the C++ does through the pointer)
            g.DeactivatedItemData.ID = g.ActiveId;
            g.DeactivatedItemData.ElapseFrame = (g.LastItemData.ID == g.ActiveId) ? g.FrameCount : g.FrameCount + 1; // FIXME: OK to use LastItemData?
            g.DeactivatedItemData.HasBeenEditedBefore = g.ActiveIdHasBeenEditedBefore;
            g.DeactivatedItemData.IsAlive = (g.ActiveIdIsAlive == g.ActiveId);

            // This could be written in a more general way (e.g associate a hook to ActiveId),
            // but since this is currently quite an exception we'll leave it as is.
            // One common scenario leading to this is: pressing Key ->NavMoveRequestApplyResult() -> ClearActiveID()
            if (g.InputTextState.ID == g.ActiveId)
                InputTextDeactivateHook(g.ActiveId);

            // While most behaved code would make an effort to not steal active id during window move/drag operations,
            // we at least need to be resilient to it. Canceling the move is rather aggressive and users of 'master' branch
            // may prefer the weird ill-defined half working situation ('docking' did assert), so may need to rework that.
            if (g.MovingWindow != null && g.ActiveId == g.MovingWindow.MoveId)
                StopMouseMovingWindow();
        }

        // Set active id
        g.ActiveIdIsJustActivated = (g.ActiveId != id);
        if (g.ActiveIdIsJustActivated)
        {
            g.ActiveIdTimer = 0.0f;
            g.ActiveIdHasBeenPressedBefore = false;
            g.ActiveIdHasBeenEditedBefore = false;
            g.ActiveIdMouseButton = -1;
            if (id != 0)
            {
                g.LastActiveId = id;
                g.LastActiveIdTimer = 0.0f;
            }
        }
        g.ActiveId = id;
        g.ActiveIdAllowOverlap = false;
        g.ActiveIdNoClearOnFocusLoss = false;
        g.ActiveIdWindow = window;
        g.ActiveIdHasBeenEditedThisFrame = false;
        g.ActiveIdFromShortcut = false;
        g.ActiveIdDisabledId = 0;
        if (id != 0)
        {
            g.ActiveIdIsAlive = id;
            g.ActiveIdSource = (g.NavActivateId == id || g.NavJustMovedToId == id) ? g.NavInputSource : ImGuiInputSource_Mouse;
            Debug.Assert(g.ActiveIdSource != ImGuiInputSource_None);
        }

        // Clear declaration of inputs claimed by the widget
        // (Please note that this is WIP and not all keys/inputs are thoroughly declared by all widgets yet)
        g.ActiveIdUsingNavDirMask = 0x00;
        g.ActiveIdUsingAllKeyboardKeys = false;
    }

    // void ImGui::ClearActiveID()
    public static void ClearActiveID()
    {
        SetActiveID(0, null); // g.ActiveId = 0;
    }

    // void ImGui::SetHoveredID(ImGuiID id)
    public static void SetHoveredID(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        g.HoveredId = id;
        g.HoveredIdAllowOverlap = false;
        if (id != 0 && g.HoveredIdPreviousFrame != id)
            g.HoveredIdTimer = g.HoveredIdNotActiveTimer = 0.0f;
    }

    public static ImGuiID GetHoveredID()
    {
        ImGuiContext g = GImGui;
        return g.HoveredId != 0 ? g.HoveredId : g.HoveredIdPreviousFrame;
    }

    // void ImGui::MarkItemEdited(ImGuiID id)
    public static void MarkItemEdited(ImGuiID id)
    {
        // This marking is to be able to provide info for IsItemDeactivatedAfterEdit().
        // ActiveId might have been released by the time we call this (as in the typical press/release button behavior) but still need to fill the data.
        ImGuiContext g = GImGui;

        //Debug.Assert(g.LastItemData.ID == id); // Failing cases include: "widgets_inputtext_scrolling", "widgets_inputtext_multiline_status", "widgets_selectable_input" = case of e.g TempInputText() overlayed manually with different ID (#2718)
        g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_EditedInternal;
        if ((g.LastItemData.ItemFlags & ImGuiItemFlags_NoMarkEdited) != 0)
            return;
        g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_Edited;

        if (g.ActiveId == id || g.ActiveId == 0)
        {
            // FIXME: Can't we fully rely on LastItemData yet?
            g.AnyIdHasBeenEditedThisFrame = g.ActiveIdHasBeenEditedThisFrame = g.ActiveIdHasBeenEditedBefore = true;
        }
        if (g.DeactivatedItemData.ID == id)
            g.DeactivatedItemData.HasBeenEditedBefore = true;

        // We accept a MarkItemEdited() on drag and drop targets (see https://github.com/ocornut/imgui/issues/1875#issuecomment-978243343)
        // We accept 'ActiveIdPreviousFrame == id' for InputText() returning an edit after it has been taken ActiveId away (#4714)
        // FIXME: This assert is getting a bit meaningless over time. It helped detect some unusual use cases but eventually it is becoming an unnecessary restriction.
        Debug.Assert(g.DragDropActive || g.ActiveId == id || g.ActiveId == 0 || g.DeactivatedItemData.ID == id || g.ActiveIdPreviousFrame == id || g.NavJustMovedToId != 0 || (g.CurrentMultiSelect != null && g.BoxSelectState.IsActive));
    }

    // bool ImGui::IsWindowContentHoverable(ImGuiWindow* window, ImGuiHoveredFlags flags)
    public static bool IsWindowContentHoverable(ImGuiWindow window, ImGuiHoveredFlags flags)
    {
        // An active popup disable hovering on other windows (apart from its own children)
        // FIXME-OPT: This could be cached/stored within the window.
        ImGuiContext g = GImGui;
        if (g.NavWindow != null)
        {
            ImGuiWindow focused_root_window = g.NavWindow.RootWindowDockTree;
            if (focused_root_window != null && focused_root_window.WasActive && focused_root_window != window.RootWindowDockTree)
            {
                // For the purpose of those flags we differentiate "standard popup" from "modal popup"
                // NB: The 'else' is important because Modal windows are also Popups.
                bool want_inhibit = false;
                if ((focused_root_window.Flags & ImGuiWindowFlags_Modal) != 0)
                    want_inhibit = true;
                else if ((focused_root_window.Flags & ImGuiWindowFlags_Popup) != 0 && (flags & ImGuiHoveredFlags_AllowWhenBlockedByPopup) == 0)
                    want_inhibit = true;

                // Inhibit hover unless the window is within the stack of our modal/popup
                if (want_inhibit)
                    if (!IsWindowWithinBeginStackOf(window.RootWindow, focused_root_window))
                        return false;
            }
        }

        // Filter by viewport
        if (window.Viewport != g.MouseViewport)
            if (g.MovingWindow == null || window.RootWindowDockTree != g.MovingWindow.RootWindowDockTree)
                return false;

        return true;
    }

    // static inline float CalcDelayFromHoveredFlags(ImGuiHoveredFlags flags) (imgui.cpp)
    static float CalcDelayFromHoveredFlags(ImGuiHoveredFlags flags)
    {
        ImGuiContext g = GImGui;
        if ((flags & ImGuiHoveredFlags_DelayNormal) != 0)
            return g.Style.HoverDelayNormal;
        if ((flags & ImGuiHoveredFlags_DelayShort) != 0)
            return g.Style.HoverDelayShort;
        return 0.0f;
    }

    // static ImGuiHoveredFlags ApplyHoverFlagsForTooltip(ImGuiHoveredFlags user_flags, ImGuiHoveredFlags shared_flags) (imgui.cpp)
    static ImGuiHoveredFlags ApplyHoverFlagsForTooltip(ImGuiHoveredFlags user_flags, ImGuiHoveredFlags shared_flags)
    {
        // Allow instance flags to override shared flags
        if ((user_flags & (ImGuiHoveredFlags_DelayNone | ImGuiHoveredFlags_DelayShort | ImGuiHoveredFlags_DelayNormal)) != 0)
            shared_flags &= ~(ImGuiHoveredFlags_DelayNone | ImGuiHoveredFlags_DelayShort | ImGuiHoveredFlags_DelayNormal);
        return user_flags | shared_flags;
    }

    // This is roughly matching the behavior of internal-facing ItemHoverable()
    // - we allow hovering to be true when ActiveId==window->MoveID, so that clicking on non-interactive items such as a Text() item still returns true with IsItemHovered()
    // - this should work even for non-interactive items that have no ID, so we cannot use LastItemId
    public static bool IsItemHovered(ImGuiHoveredFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImUtil.ImAssertUserError((flags & ~ImGuiHoveredFlags_AllowedMaskForIsItemHovered) == 0, "Invalid flags for IsItemHovered()!");

        if (g.NavHighlightItemUnderNav && g.NavCursorVisible && (flags & ImGuiHoveredFlags_NoNavOverride) == 0)
        {
            if (!IsItemFocused())
                return false;
            if ((g.LastItemData.ItemFlags & ImGuiItemFlags_Disabled) != 0 && (flags & ImGuiHoveredFlags_AllowWhenDisabled) == 0)
                return false;

            if ((flags & ImGuiHoveredFlags_ForTooltip) != 0)
                flags = ApplyHoverFlagsForTooltip(flags, g.Style.HoverFlagsForTooltipNav);
        }
        else
        {
            // Test for bounding box overlap, as updated as ItemAdd()
            ImGuiItemStatusFlags status_flags = g.LastItemData.StatusFlags;
            if ((status_flags & ImGuiItemStatusFlags_HoveredRect) == 0)
                return false;

            if ((flags & ImGuiHoveredFlags_ForTooltip) != 0)
                flags = ApplyHoverFlagsForTooltip(flags, g.Style.HoverFlagsForTooltipMouse);

            // Done with rectangle culling so we can perform heavier checks now
            // Test if we are hovering the right window (our window could be behind another window)
            // [2021/03/02] Reworked / reverted the revert, finally. Note we want e.g. BeginGroup/ItemAdd/EndGroup to work as well. (#3851)
            // [2017/10/16] Reverted commit 344d48be3 and testing RootWindow instead. I believe it is correct to NOT test for RootWindow but this leaves us unable
            // to use IsItemHovered() after EndChild() itself. Until a solution is found I believe reverting to the test from 2017/09/27 is safe since this was
            // the test that has been running for a long while.
            if (g.HoveredWindow != window && (status_flags & ImGuiItemStatusFlags_HoveredWindow) == 0)
                if ((flags & ImGuiHoveredFlags_AllowWhenOverlappedByWindow) == 0)
                    return false;

            // Test if another item is active (e.g. being dragged)
            ImGuiID id = g.LastItemData.ID;
            if ((flags & ImGuiHoveredFlags_AllowWhenBlockedByActiveItem) == 0)
                if (g.ActiveId != 0 && g.ActiveId != id && !g.ActiveIdAllowOverlap && !g.ActiveIdFromShortcut)
                {
                    // When ActiveId == MoveId it means that either:
                    // - (1) user clicked on void _or_ an item with no id, which triggers moving window (ActiveId is set even when window has _NoMove flag)
                    //   - the (id == 0) test handles it, however, IsItemHovered() will leak between id==0 items (mostly visible when using _NoMove). // FIXME: May be fixed.
                    // - (2) user clicked a disabled item. UpdateMouseMovingWindowEndFrame() uses ActiveId == MoveId to avoid interference with item logic + sets ActiveIdDisabledId.
                    bool cancel_is_hovered = true;
                    if (g.ActiveId == window.MoveId && (id == 0 || g.ActiveIdDisabledId == id))
                        cancel_is_hovered = false;
                    // When ActiveId == TabId it means user clicked docking tab for the window.
                    if (g.ActiveId == window.TabId)
                        cancel_is_hovered = false;
                    if (cancel_is_hovered)
                        return false;
                }

            // Test if interactions on this window are blocked by an active popup or modal.
            // The ImGuiHoveredFlags_AllowWhenBlockedByPopup flag will be tested here.
            if (!IsWindowContentHoverable(window, flags) && (g.LastItemData.ItemFlags & ImGuiItemFlags_NoWindowHoverableCheck) == 0)
                return false;

            // Test if the item is disabled
            if ((g.LastItemData.ItemFlags & ImGuiItemFlags_Disabled) != 0 && (flags & ImGuiHoveredFlags_AllowWhenDisabled) == 0)
                return false;

            // Special handling for calling after Begin() which represent the title bar or tab.
            // When the window is skipped/collapsed (SkipItems==true) that last item (always ->MoveId submitted by Begin)
            // will never be overwritten so we need to detect the case.
            if (id == window.MoveId && window.WriteAccessed)
                return false;

            // Test if using AllowOverlap and overlapped
            if ((g.LastItemData.ItemFlags & ImGuiItemFlags_AllowOverlap) != 0 && id != 0)
                if ((flags & ImGuiHoveredFlags_AllowWhenOverlappedByItem) == 0)
                    if (g.HoveredIdPreviousFrame != g.LastItemData.ID)
                        return false;
        }

        // Handle hover delay
        // (some ideas: https://www.nngroup.com/articles/timing-exposing-content)
        float delay = CalcDelayFromHoveredFlags(flags);
        if (delay > 0.0f || (flags & ImGuiHoveredFlags_Stationary) != 0)
        {
            ImGuiID hover_delay_id = (g.LastItemData.ID != 0) ? g.LastItemData.ID : window.GetIDFromPos(g.LastItemData.Rect.Min);
            if ((flags & ImGuiHoveredFlags_NoSharedDelay) != 0 && (g.HoverItemDelayIdPreviousFrame != hover_delay_id))
                g.HoverItemDelayTimer = 0.0f;
            g.HoverItemDelayId = hover_delay_id;

            // When changing hovered item we requires a bit of stationary delay before activating hover timer,
            // but once unlocked on a given item we also moving.
            if ((flags & ImGuiHoveredFlags_Stationary) != 0 && g.HoverItemUnlockedStationaryId != hover_delay_id)
                return false;

            if (g.HoverItemDelayTimer < delay)
                return false;
        }

        return true;
    }

    // Internal facing ItemHoverable() used when submitting widgets. THIS IS A SUBMISSION NOT A HOVER CHECK.
    // Returns whether the item was hovered, logic differs slightly from IsItemHovered().
    // (this does not rely on LastItemData it can be called from a ButtonBehavior() call not following an ItemAdd() call)
    // FIXME-LEGACY: the 'ImGuiItemFlags item_flags' parameter was added on 2023-06-28.
    // If you used this in your legacy/custom widgets code:
    // - Commonly: if your ItemHoverable() call comes after an ItemAdd() call: pass 'item_flags = g.LastItemData.ItemFlags'.
    // - Rare: otherwise you may pass 'item_flags = 0' (ImGuiItemFlags_None) unless you want to benefit from special behavior handled by ItemHoverable.
    public static bool ItemHoverable(ImRect bb, ImGuiID id, ImGuiItemFlags item_flags)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        // Detect ID conflicts
        // (this is specifically done here by comparing on hover because it allows us a detection of duplicates that is algorithmically extra cheap, 1 u32 compare per item. No O(log N) lookup whatsoever)
        if (id != 0 && g.HoveredIdPreviousFrame == id && (item_flags & ImGuiItemFlags_AllowDuplicateId) == 0)
        {
            g.HoveredIdPreviousFrameItemCount++;
            if (g.DebugDrawIdConflictsId == id)
                window.DrawList.AddRect(bb.Min - new ImVec2(1, 1), bb.Max + new ImVec2(1, 1), ImGuiCore.IM_COL32(255, 0, 0, 255), 0.0f, 2.0f);
        }

        if (g.HoveredWindow != window)
            return false;
        if (!IsMouseHoveringRect(bb.Min, bb.Max))
            return false;

        if (g.HoveredId != 0 && g.HoveredId != id && !g.HoveredIdAllowOverlap)
            return false;
        if (g.ActiveId != 0 && g.ActiveId != id && !g.ActiveIdAllowOverlap)
            if (!g.ActiveIdFromShortcut)
                return false;

        // We are done with rectangle culling so we can perform heavier checks now.
        if ((item_flags & ImGuiItemFlags_NoWindowHoverableCheck) == 0 && !IsWindowContentHoverable(window, ImGuiHoveredFlags_None))
        {
            g.HoveredIdIsDisabled = true;
            return false;
        }

        // We exceptionally allow this function to be called with id==0 to allow using it for easy high-level
        // hover test in widgets code. We could also decide to split this function is two.
        if (id != 0)
        {
            // Drag source doesn't report as hovered
            if (g.DragDropActive && g.DragDropPayload.SourceId == id && (g.DragDropSourceFlags & ImGuiDragDropFlags_SourceNoDisableHover) == 0)
                return false;

            SetHoveredID(id);

            // AllowOverlap mode (rarely used) requires previous frame HoveredId to be null or to match.
            // This allows using patterns where a later submitted widget overlaps a previous one. Generally perceived as a front-to-back hit-test.
            if ((item_flags & ImGuiItemFlags_AllowOverlap) != 0)
            {
                g.HoveredIdAllowOverlap = true;
                if (g.HoveredIdPreviousFrame != id)
                    return false;
            }

            // Display shortcut (only works with mouse)
            // (ImGuiItemStatusFlags_HasShortcut in LastItemData denotes we want a tooltip)
            if (id == g.LastItemData.ID && (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HasShortcut) != 0 && g.ActiveId != id)
                if (IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_DelayNormal))
                    SetTooltip(GetKeyChordName(g.LastItemData.Shortcut));
        }

        // When disabled we'll return false but still set HoveredId
        if ((item_flags & ImGuiItemFlags_Disabled) != 0)
        {
            // Release active id if turning disabled
            if (g.ActiveId == id && id != 0)
                ClearActiveID();
            g.HoveredIdIsDisabled = true;
            return false;
        }

        if (id != 0)
        {
            // [DEBUG] Item Picker tool!
            // We perform the check here because reaching is path is rare (1~ time a frame),
            // making the cost of this tool near-zero! We could get better call-stack and support picking non-hovered
            // items if we performed the test in ItemAdd(), but that would incur a bigger runtime cost.
            if (g.DebugItemPickerActive && g.HoveredIdPreviousFrame == id)
                GetForegroundDrawList().AddRect(bb.Min, bb.Max, ImGuiCore.IM_COL32(255, 255, 0, 255));
            if (g.DebugItemPickerBreakId == id)
                Debugger.Break(); // IM_DEBUG_BREAK()
        }

        if (g.NavHighlightItemUnderNav && (item_flags & ImGuiItemFlags_NoNavDisableMouseHover) == 0)
            return false;

        return true;
    }

    // FIXME: This is inlined/duplicated in ItemAdd()
    // FIXME: The id != 0 path is not used by our codebase, may get rid of it?
    public static bool IsClippedEx(ImRect bb, ImGuiID id)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (!bb.Overlaps(window.ClipRect))
            if (id == 0 || (id != g.ActiveId && id != g.ActiveIdPreviousFrame && id != g.NavId && id != g.NavActivateId))
                if (!g.ItemUnclipByLog)
                    return true;
        return false;
    }

    // This is also inlined in ItemAdd()
    // Note: if ImGuiItemStatusFlags_HasDisplayRect is set, user needs to set g.LastItemData.DisplayRect.
    public static void SetLastItemData(ImGuiID item_id, ImGuiItemFlags item_flags, ImGuiItemStatusFlags status_flags, ImRect item_rect)
    {
        ImGuiContext g = GImGui;
        g.LastItemData.ID = item_id;
        g.LastItemData.ItemFlags = item_flags;
        g.LastItemData.StatusFlags = status_flags;
        g.LastItemData.Rect = item_rect;
        g.LastItemData.NavRect = item_rect;
    }

    // static void ImGui::SetLastItemDataForWindow(ImGuiWindow* window, const ImRect& rect)
    static void SetLastItemDataForWindow(ImGuiWindow window, ImRect rect)
    {
        ImGuiContext g = GImGui;
        if (window.DockIsActive)
            SetLastItemData(window.MoveId, g.CurrentItemFlags, window.DC.DockTabItemStatusFlags, window.DC.DockTabItemRect);
        else
            SetLastItemData(window.MoveId, g.CurrentItemFlags, window.DC.WindowItemStatusFlags, rect);
    }

    // static void ImGui::SetLastItemDataForChildWindowItem(ImGuiWindow* window, const ImRect& rect)
    static void SetLastItemDataForChildWindowItem(ImGuiWindow window, ImRect rect)
    {
        ImGuiContext g = GImGui;
        SetLastItemData(window.ChildId, g.CurrentItemFlags, window.DC.ChildItemStatusFlags, rect);
    }

    public static float CalcWrapWidthForPos(ImVec2 pos, float wrap_pos_x)
    {
        if (wrap_pos_x < 0.0f)
            return 0.0f;

        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (wrap_pos_x == 0.0f)
        {
            // We could decide to setup a default wrapping max point for auto-resizing windows,
            // or have auto-wrap (with unspecified wrapping pos) behave as a ContentSize extending function?
            //if (window->Hidden && (window->Flags & ImGuiWindowFlags_AlwaysAutoResize))
            //    wrap_pos_x = ImMax(window->WorkRect.Min.x + g.FontSize * 10.0f, window->WorkRect.Max.x);
            //else
            wrap_pos_x = window.WorkRect.Max.x;
        }
        else if (wrap_pos_x > 0.0f)
        {
            wrap_pos_x += window.Pos.x - window.Scroll.x; // wrap_pos_x is provided is window local space
        }

        return ImMath.ImMax(wrap_pos_x - pos.x, 1.0f);
    }

    // IM_ALLOC() == ImGui::MemAlloc() — managed stand-in (no custom allocator hooks in the port).
    public static object MemAlloc(ulong size)
    {
        object ptr = new byte[size];
        ImGuiContext ctx = GImGui;
        if (ctx != null)
            DebugAllocHook(ctx.DebugAllocInfo, ctx.FrameCount, ptr, size);
        return ptr;
    }

    // IM_FREE() == ImGui::MemFree() — managed stand-in (GC handles memory; allocation bookkeeping only).
    public static void MemFree(object ptr)
    {
        ImGuiContext ctx = GImGui;
        if (ptr != null && ctx != null)
            DebugAllocHook(ctx.DebugAllocInfo, ctx.FrameCount, ptr, ulong.MaxValue); // (size_t)-1
    }

    public static void DemoMarker(string file, int line, string section)
    {
        ImGuiContext g = GImGui;
        if (g.DemoMarkerCallback != null)
            g.DemoMarkerCallback(file, line, section);
    }

    // We record the number of allocation in recent frames, as a way to audit/sanitize our guiding principles of "no allocations on idle/repeating frames"
    public static void DebugAllocHook(ImGuiDebugAllocInfo info, int frame_count, object ptr, ulong size)
    {
        int entry_idx = info.LastEntriesIdx;
        ImGuiDebugAllocEntry entry = info.LastEntriesBuf[entry_idx];
        if (entry.FrameCount != frame_count)
        {
            info.LastEntriesIdx = (short)((info.LastEntriesIdx + 1) % info.LastEntriesBuf.Length);
            entry_idx = info.LastEntriesIdx;
            entry = info.LastEntriesBuf[entry_idx];
            entry.FrameCount = frame_count;
            entry.AllocCount = entry.FreeCount = 0;
        }
        if (size != ulong.MaxValue)
        {
            entry.AllocCount++;
            info.TotalAllocCount++;
        }
        else
        {
            entry.FreeCount++;
            info.TotalFreeCount++;
        }
        info.LastEntriesBuf[entry_idx] = entry;
    }

    // A conformant backend should return NULL on failure (e.g. clipboard data is not text).
    public static string GetClipboardText()
    {
        ImGuiContext g = GImGui;
        return g.PlatformIO.Platform_GetClipboardTextFn != null ? g.PlatformIO.Platform_GetClipboardTextFn(g) : null;
    }

    public static void SetClipboardText(string text)
    {
        ImGuiContext g = GImGui;
        if (g.PlatformIO.Platform_SetClipboardTextFn != null)
            g.PlatformIO.Platform_SetClipboardTextFn(g, text);
    }

    public static string GetVersion()
    {
        return ImGuiVersion; // IMGUI_VERSION
    }

    public static ImGuiIO GetIO()
    {
        Debug.Assert(GImGui != null, "No current context. Did you call ImGui::CreateContext() and ImGui::SetCurrentContext() ?");
        return GImGui.IO;
    }

    // This variant exists to facilitate backends experimenting with multi-threaded parallel context. (#8069, #6293, #5856)
    public static ImGuiIO GetIO(ImGuiContext ctx)
    {
        Debug.Assert(ctx != null);
        return ctx.IO;
    }

    public static ImGuiPlatformIO GetPlatformIO()
    {
        Debug.Assert(GImGui != null, "No current context. Did you call ImGui::CreateContext() and ImGui::SetCurrentContext()?");
        return GImGui.PlatformIO;
    }

    // This variant exists to facilitate backends experimenting with multi-threaded parallel context. (#8069, #6293, #5856)
    public static ImGuiPlatformIO GetPlatformIO(ImGuiContext ctx)
    {
        Debug.Assert(ctx != null);
        return ctx.PlatformIO;
    }

    // Pass this to your backend rendering function! Valid after Render() and until the next call to NewFrame()
    public static ImDrawData GetDrawData()
    {
        ImGuiContext g = GImGui;
        ImGuiViewportP viewport = g.Viewports[0];
        return viewport.DrawDataP.Valid ? viewport.DrawDataP : null;
    }

    public static double GetTime()
    {
        return GImGui.Time;
    }

    public static int GetFrameCount()
    {
        return GImGui.FrameCount;
    }

    // static ImDrawList* GetViewportBgFgDrawList(ImGuiViewportP* viewport, size_t drawlist_no, const char* drawlist_name) (imgui.cpp)
    static ImDrawList GetViewportBgFgDrawList(ImGuiViewportP viewport, int drawlist_no, string drawlist_name)
    {
        // Create the draw list on demand, because they are not frequently used for all viewports
        ImGuiContext g = GImGui;
        Debug.Assert(drawlist_no < viewport.BgFgDrawLists.Length);
        ImDrawList draw_list = viewport.BgFgDrawLists[drawlist_no];
        if (draw_list == null)
        {
            draw_list = new ImDrawList(g.DrawListSharedData);
            draw_list._OwnerName = drawlist_name;
            viewport.BgFgDrawLists[drawlist_no] = draw_list;
        }

        // Our ImDrawList system requires that there is always a command
        if (viewport.BgFgDrawListsLastFrameActive[drawlist_no] != g.FrameCount)
        {
            draw_list._ResetForNewFrame();
            draw_list._SetPixelDensity(viewport.FramebufferScale.x);
            draw_list.PushTexture(g.IO.Fonts.TexRef);
            draw_list.PushClipRect(viewport.Pos, viewport.Pos + viewport.Size, false);
            viewport.BgFgDrawListsLastFrameActive[drawlist_no] = g.FrameCount;
            viewport.BgFgDrawListsLastTimeActive[drawlist_no] = (float)g.Time;
        }
        return draw_list;
    }

    public static ImDrawList GetBackgroundDrawList(ImGuiViewport viewport)
    {
        if (viewport == null)
            viewport = GImGui.CurrentWindow.Viewport;
        return GetViewportBgFgDrawList((ImGuiViewportP)viewport, 0, "##Background");
    }

    public static ImDrawList GetBackgroundDrawList()
    {
        return GetBackgroundDrawList((ImGuiViewport)null);
    }

    public static ImDrawList GetForegroundDrawList(ImGuiViewport viewport)
    {
        if (viewport == null)
            viewport = GImGui.CurrentWindow.Viewport;
        return GetViewportBgFgDrawList((ImGuiViewportP)viewport, 1, "##Foreground");
    }

    public static ImDrawList GetForegroundDrawList()
    {
        return GetForegroundDrawList((ImGuiViewport)null);
    }

    public static ImDrawListSharedData GetDrawListSharedData()
    {
        return GImGui.DrawListSharedData;
    }

    // void ImGui::StartMouseMovingWindow(ImGuiWindow* window)
    public static void StartMouseMovingWindow(ImGuiWindow window)
    {
        // Set ActiveId even if the _NoMove flag is set. Without it, dragging away from a window with _NoMove would activate hover on other windows.
        // We _also_ call this when clicking in a window empty space when io.ConfigWindowsMoveFromTitleBarOnly is set, but clear g.MovingWindow afterward.
        // This is because we want ActiveId to be set even when the window is not permitted to move.
        ImGuiContext g = GImGui;
        FocusWindow(window);
        SetActiveID(window.MoveId, window);
        if (g.IO.ConfigNavCursorVisibleAuto)
            g.NavCursorVisible = false;
        g.ActiveIdClickOffset = g.IO.MouseClickedPos[0] - window.RootWindowDockTree.Pos;
        g.ActiveIdNoClearOnFocusLoss = true;
        SetActiveIdUsingAllKeyboardKeys();

        bool can_move_window = true;
        if ((window.Flags & ImGuiWindowFlags_NoMove) != 0 || (window.RootWindowDockTree.Flags & ImGuiWindowFlags_NoMove) != 0)
            can_move_window = false;
        if (window.DockNodeAsHost != null)
            if (window.DockNodeAsHost.VisibleWindow != null && (window.DockNodeAsHost.VisibleWindow.Flags & ImGuiWindowFlags_NoMove) != 0)
                can_move_window = false;
        if (can_move_window)
            g.MovingWindow = window;
    }

    // We use 'undock == false' when dragging from title bar to allow moving groups of floating nodes without undocking them.
    // void ImGui::StartMouseMovingWindowOrNode(ImGuiWindow* window, ImGuiDockNode* node, bool undock)
    public static void StartMouseMovingWindowOrNode(ImGuiWindow window, ImGuiDockNode node, bool undock)
    {
        ImGuiContext g = GImGui;
        bool can_undock_node = false;
        if (undock && node != null && node.VisibleWindow != null && (node.VisibleWindow.Flags & ImGuiWindowFlags_NoMove) == 0 && (node.MergedFlags & ImGuiDockNodeFlags_NoUndocking) == 0)
        {
            // Can undock if:
            // - part of a hierarchy with more than one visible node (if only one is visible, we'll just move the root window)
            // - part of a dockspace node hierarchy: so we can undock the last single visible node too. Undocking from a fixed/central node will create a new node and copy windows.
            ImGuiDockNode root_node = DockNodeGetRootNode(node);
            if (root_node.OnlyNodeWithWindows != node || root_node.CentralNode != null)   // -V1051 PVS-Studio thinks node should be root_node and is wrong about that.
                can_undock_node = true;
        }

        bool clicked = IsMouseClicked(0);
        bool dragging = IsMouseDragging(0);
        if (can_undock_node && dragging)
            DockContextQueueUndockNode(g, node); // Will lead to DockNodeStartMouseMovingWindow() -> StartMouseMovingWindow() being called next frame
        else if (!can_undock_node && (clicked || dragging) && g.MovingWindow != window)
            StartMouseMovingWindow(window);
    }

    // This is not 100% symmetric with StartMouseMovingWindow().
    // We do NOT clear ActiveID, because:
    // - It would lead to rather confusing recursive code paths. Caller can call ClearActiveID() if desired.
    // - Some code intentionally cancel moving but keep the ActiveID to lock inputs (e.g. code path taken when clicking a disabled item).
    public static void StopMouseMovingWindow()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.MovingWindow;

        // Ref commits 6b7766817, 36055213c for some partial history on checking if viewport != NULL.
        if (window != null && window.Viewport != null)
        {
            // Try to merge the window back into the main viewport.
            // This works because MouseViewport should be != MovingWindow->Viewport on release (as per code in UpdateViewports)
            if ((g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable) != 0)
                UpdateTryMergeWindowIntoHostViewport(window.RootWindowDockTree, g.MouseViewport);

            // Restore the mouse viewport so that we don't hover the viewport _under_ the moved window during the frame we released the mouse button.
            if (!IsDragDropPayloadBeingAccepted())
                g.MouseViewport = window.Viewport;

            // Clear the NoInputs window flag set by the Viewport system in AddUpdateViewport()
            bool window_can_use_inputs = ((window.Flags & ImGuiWindowFlags_NoMouseInputs) != 0 && (window.Flags & ImGuiWindowFlags_NoNavInputs) != 0) == false;
            if (window_can_use_inputs)
                window.Viewport.Flags &= ~ImGuiViewportFlags_NoInputs;
        }

        g.MovingWindow = null;
    }

    // Handle mouse moving window
    // Note: moving window with the navigation keys (Square + d-pad / Ctrl+Tab + Arrows) are processed in NavUpdateWindowing()
    // FIXME: We don't have strong guarantee that g.MovingWindow stay synced with g.ActiveId == g.MovingWindow->MoveId.
    // This is currently enforced by the fact that BeginDragDropSource() is setting all g.ActiveIdUsingXXXX flags to inhibit navigation inputs,
    // but if we should more thoroughly test cases where g.ActiveId or g.MovingWindow gets changed and not the other.
    public static void UpdateMouseMovingWindowNewFrame()
    {
        ImGuiContext g = GImGui;
        if (g.MovingWindow != null)
        {
            // We actually want to move the root window. g.MovingWindow == window we clicked on (could be a child window).
            // We track it to preserve Focus and so that generally ActiveIdWindow == MovingWindow and ActiveId == MovingWindow->MoveId for consistency.
            KeepAliveID(g.ActiveId);
            Debug.Assert(g.MovingWindow != null && g.MovingWindow.RootWindowDockTree != null);
            ImGuiWindow moving_window = g.MovingWindow.RootWindowDockTree;

            // When a window stop being submitted while being dragged, it may will its viewport until next Begin()
            bool window_disappeared = (!moving_window.WasActive && !moving_window.Active);
            if (g.IO.MouseDown[0] && IsMousePosValid(ref g.IO.MousePos) && !window_disappeared)
            {
                ImVec2 pos = g.IO.MousePos - g.ActiveIdClickOffset;
                if (moving_window.Pos.x != pos.x || moving_window.Pos.y != pos.y)
                {
                    SetWindowPos(moving_window, pos, ImGuiCond_Always);
                    if (moving_window.Viewport != null && moving_window.ViewportOwned) // Synchronize viewport immediately because some overlays may relies on clipping rectangle before we Begin() into the window.
                    {
                        moving_window.Viewport.Pos = pos;
                        moving_window.Viewport.UpdateWorkRect();
                    }
                }
                FocusWindow(g.MovingWindow);
            }
            else
            {
                StopMouseMovingWindow();
                ClearActiveID();
            }
        }
        else
        {
            // When clicking/dragging from a window that has the _NoMove flag, we still set the ActiveId in order to prevent hovering others.
            if (g.ActiveIdWindow != null && g.ActiveIdWindow.MoveId == g.ActiveId)
            {
                KeepAliveID(g.ActiveId);
                if (!g.IO.MouseDown[0])
                    ClearActiveID();
            }
        }
    }

    // Initiate focusing and moving window when clicking on empty space or title bar.
    // Initiate focusing window when clicking on a disabled item.
    // Handle left-click and right-click focus.
    public static void UpdateMouseMovingWindowEndFrame()
    {
        ImGuiContext g = GImGui;
        if (g.ActiveId != 0 || (g.HoveredId != 0 && !g.HoveredIdIsDisabled))
            return;

        // Unless we just made a window/popup appear
        if (g.NavWindow != null && g.NavWindow.Appearing)
            return;

        ImGuiWindow hovered_window = g.HoveredWindow;

        // Click on empty space to focus window and start moving
        // (after we're done with all our widgets, so e.g. clicking on docking tab-bar which have set HoveredId already and not get us here!)
        if (IsMouseClicked(0, ImGuiInputFlags_None, ImGuiKeyOwner_NoOwner))
        {
            // Handle the edge case of a popup being closed while clicking in its empty space.
            // If we try to focus it, FocusWindow() > ClosePopupsOverWindow() will accidentally close any parent popups because they are not linked together any more.
            ImGuiWindow hovered_root = hovered_window != null ? hovered_window.RootWindow : null;
            bool is_closed_popup = hovered_root != null && (hovered_root.Flags & ImGuiWindowFlags_Popup) != 0 && !IsPopupOpen(hovered_root.PopupId, ImGuiPopupFlags_AnyPopupLevel);
            bool is_queued_focus_request = g.NavMoveSubmitted && (g.NavMoveFlags & ImGuiNavMoveFlags_FocusApi) != 0;

            if (hovered_window != null && !is_closed_popup && !is_queued_focus_request)
            {
                StartMouseMovingWindow(hovered_window); //-V595

                // FIXME: In principle we might be able to call StopMouseMovingWindow() below.
                // Please note how StartMouseMovingWindow() and StopMouseMovingWindow() and not entirely symmetrical, at the later doesn't clear ActiveId.

                // Cancel moving if clicked outside of title bar
                if ((hovered_window.BgClickFlags & ImGuiWindowBgClickFlags_Move) == 0) // set by io.ConfigWindowsMoveFromTitleBarOnly
                    if ((hovered_root.Flags & ImGuiWindowFlags_NoTitleBar) == 0)
                        if (!hovered_root.TitleBarRect().Contains(g.IO.MouseClickedPos[0]))
                            g.MovingWindow = null;

                // Cancel moving if clicked over an item which was disabled or inhibited by popups
                // (when g.HoveredIdIsDisabled == true && g.HoveredId == 0 we are inhibited by popups, when g.HoveredIdIsDisabled == true && g.HoveredId != 0 we are over a disabled item)
                if (g.HoveredIdIsDisabled)
                {
                    g.MovingWindow = null;
                    g.ActiveIdDisabledId = g.HoveredId;
                }
            }
            else if (hovered_window == null && g.NavWindow != null)
            {
                // Clicking on void disable focus
                FocusWindow(null, ImGuiFocusRequestFlags_UnlessBelowModal);
            }
        }

        // With right mouse button we close popups without changing focus based on where the mouse is aimed
        // Instead, focus will be restored to the window under the bottom-most closed popup.
        // (The left mouse button path calls FocusWindow on the hovered window, which will lead NewFrame->ClosePopupsOverWindow to trigger)
        if (g.HoveredId == 0 && IsMouseClicked(1, ImGuiInputFlags_None, ImGuiKeyOwner_NoOwner))
        {
            // Find the top-most window between HoveredWindow and the top-most Modal Window.
            // This is where we can trim the popup stack.
            ImGuiWindow modal = GetTopMostPopupModal();
            bool hovered_window_above_modal = hovered_window != null && (modal == null || IsWindowAbove(hovered_window, modal));
            ClosePopupsOverWindow(hovered_window_above_modal ? hovered_window : modal, true);
        }
    }

    // static bool IsWindowActiveAndVisible(ImGuiWindow* window) (imgui.cpp)
    static bool IsWindowActiveAndVisible(ImGuiWindow window)
    {
        return window.Active && !window.Hidden;
    }

    // The reason this is exposed in imgui_internal.h is: on touch-based system that don't have hovering, we want to dispatch inputs to the right target (imgui vs imgui+app)
    public static void UpdateHoveredWindowAndCaptureFlags(ImVec2 mouse_pos)
    {
        ImGuiContext g = GImGui;
        ImGuiIO io = g.IO;

        // FIXME-DPI: This storage was added on 2021/03/31 for test engine, but if we want to multiply WINDOWS_HOVER_PADDING
        // by DpiScale, we need to make this window-agnostic anyhow, maybe need storing inside ImGuiWindow.
        g.WindowsBorderHoverPadding = ImMath.ImMax(ImMath.ImMax(g.Style.TouchExtraPadding.x, g.Style.TouchExtraPadding.y), g.Style.WindowBorderHoverPadding);

        // Find the window hovered by mouse:
        // - Child windows can extend beyond the limit of their parent so we need to derive HoveredRootWindow from HoveredWindow.
        // - When moving a window we can skip the search, which also conveniently bypasses the fact that window->WindowRectClipped is lagging as this point of the frame.
        // - We also support the moved window toggling the NoInputs flag after moving has started in order to be able to detect windows below it, which is useful for e.g. docking mechanisms.
        bool clear_hovered_windows = false;
        FindHoveredWindowEx(mouse_pos, false, out g.HoveredWindow, out g.HoveredWindowUnderMovingWindow);
        Debug.Assert(g.HoveredWindow == null || g.HoveredWindow == g.MovingWindow || g.HoveredWindow.Viewport == g.MouseViewport);
        g.HoveredWindowBeforeClear = g.HoveredWindow;

        // Modal windows prevents mouse from hovering behind them.
        ImGuiWindow modal_window = GetTopMostPopupModal();
        if (modal_window != null && g.HoveredWindow != null && !IsWindowWithinBeginStackOf(g.HoveredWindow.RootWindow, modal_window))
            clear_hovered_windows = true;

        // Disabled mouse hovering (we don't currently clear MousePos, we could)
        if ((io.ConfigFlags & ImGuiConfigFlags_NoMouse) != 0)
            clear_hovered_windows = true;

        // We track click ownership. When clicked outside of a window the click is owned by the application and
        // won't report hovering nor request capture even while dragging over our windows afterward.
        bool has_open_popup = (g.OpenPopupStack.Size > 0);
        bool has_open_modal = (modal_window != null);
        int mouse_earliest_down = -1;
        bool mouse_any_down = false;
        for (int i = 0; i < io.MouseDown.Length; i++)
        {
            if (io.MouseClicked[i])
            {
                io.MouseDownOwned[i] = (g.HoveredWindow != null) || has_open_popup;
                io.MouseDownOwnedUnlessPopupClose[i] = (g.HoveredWindow != null) || has_open_modal;
            }
            mouse_any_down |= io.MouseDown[i];
            if (io.MouseDown[i] || io.MouseReleased[i]) // Increase release frame for our evaluation of earliest button (#1392)
                if (mouse_earliest_down == -1 || io.MouseClickedTime[i] < io.MouseClickedTime[mouse_earliest_down])
                    mouse_earliest_down = i;
        }
        bool mouse_avail = (mouse_earliest_down == -1) || io.MouseDownOwned[mouse_earliest_down];
        bool mouse_avail_unless_popup_close = (mouse_earliest_down == -1) || io.MouseDownOwnedUnlessPopupClose[mouse_earliest_down];

        // If mouse was first clicked outside of ImGui bounds we also cancel out hovering.
        // FIXME: For patterns of drag and drop across OS windows, we may need to rework/remove this test (first committed 311c0ca9 on 2015/02)
        bool mouse_dragging_extern_payload = g.DragDropActive && (g.DragDropSourceFlags & ImGuiDragDropFlags_SourceExtern) != 0;
        if (!mouse_avail && !mouse_dragging_extern_payload)
            clear_hovered_windows = true;

        if (clear_hovered_windows)
        {
            g.HoveredWindow = null;
            g.HoveredWindowUnderMovingWindow = null;
        }

        // Update io.WantCaptureMouse for the user application (true = dispatch mouse info to Dear ImGui only, false = dispatch mouse to Dear ImGui + underlying app)
        // Update io.WantCaptureMouseAllowPopupClose (experimental) to give a chance for app to react to popup closure with a drag
        if (g.WantCaptureMouseNextFrame != -1)
        {
            io.WantCaptureMouse = io.WantCaptureMouseUnlessPopupClose = (g.WantCaptureMouseNextFrame != 0);
        }
        else
        {
            io.WantCaptureMouse = (mouse_avail && (g.HoveredWindow != null || mouse_any_down)) || has_open_popup;
            io.WantCaptureMouseUnlessPopupClose = (mouse_avail_unless_popup_close && (g.HoveredWindow != null || mouse_any_down)) || has_open_modal;
        }

        // Update io.WantCaptureKeyboard for the user application (true = dispatch keyboard info to Dear ImGui only, false = dispatch keyboard info to Dear ImGui + underlying app)
        io.WantCaptureKeyboard = false;
        if ((io.ConfigFlags & ImGuiConfigFlags_NoKeyboard) == 0)
        {
            if (g.ActiveId != 0 || modal_window != null)
                io.WantCaptureKeyboard = true;
            else if (io.NavActive && (io.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0 && io.ConfigNavCaptureKeyboard)
                io.WantCaptureKeyboard = true;
        }
        if (g.WantCaptureKeyboardNextFrame != -1) // Manual override
            io.WantCaptureKeyboard = (g.WantCaptureKeyboardNextFrame != 0);

        // Update io.WantTextInput flag, this is to allow systems without a keyboard (e.g. mobile, hand-held) to show a software keyboard if possible
        io.WantTextInput = (g.WantTextInputNextFrame != -1) ? (g.WantTextInputNextFrame != 0) : false;
    }

    // Called once a frame. Followed by SetCurrentFont() which sets up the remaining data.
    // FIXME-VIEWPORT: the concept of a single ClipRectFullscreen is not ideal!
    static void SetupDrawListSharedData()
    {
        ImGuiContext g = GImGui;
        ImRect virtual_space = new ImRect(float.MaxValue, float.MaxValue, -float.MaxValue, -float.MaxValue);
        for (int i = 0; i < g.Viewports.Size; i++)
            virtual_space.Add(g.Viewports[i].GetMainRect());
        g.DrawListSharedData.ClipRectFullscreen = new ImVec4(virtual_space.Min.x, virtual_space.Min.y, virtual_space.Max.x, virtual_space.Max.y); // ImRect::ToVec4()
        g.DrawListSharedData.CurveTessellationMaxError = g.Style.CurveTessellationMaxError;
        g.DrawListSharedData.SetCircleTessellationMaxError(g.Style.CircleTessellationMaxError);
        g.DrawListSharedData.InitialFlags = ImDrawListFlags_None;
        if (g.Style.AntiAliasedLines)
            g.DrawListSharedData.InitialFlags |= ImDrawListFlags_AntiAliasedLines;
        if (g.Style.AntiAliasedLinesUseTex && (g.IO.Fonts.Flags & ImFontAtlasFlags_NoBakedLines) == 0)
            g.DrawListSharedData.InitialFlags |= ImDrawListFlags_AntiAliasedLinesUseTex;
        if (g.Style.AntiAliasedFill)
            g.DrawListSharedData.InitialFlags |= ImDrawListFlags_AntiAliasedFill;
        if ((g.IO.BackendFlags & ImGuiBackendFlags_RendererHasVtxOffset) != 0)
            g.DrawListSharedData.InitialFlags |= ImDrawListFlags_AllowVtxOffset;
    }

    // void ImGui::NewFrame()
    public static unsafe void NewFrame()
    {
        Debug.Assert(GImGui != null, "No current context. Did you call ImGui::CreateContext() and ImGui::SetCurrentContext() ?");
        ImGuiContext g = GImGui;

        // Remove pending delete hooks before frame start.
        // This deferred removal avoid issues of removal while iterating the hook vector
        for (int n = g.Hooks.Size - 1; n >= 0; n--)
            if (g.Hooks[n].Type == ImGuiContextHookType_PendingRemoval_)
                g.Hooks.erase(n);

        CallContextHooks(g, ImGuiContextHookType_NewFramePre);

        // Check and assert for various common IO and Configuration mistakes
        g.ConfigFlagsLastFrame = g.ConfigFlagsCurrFrame;
        ErrorCheckNewFrameSanityChecks();
        g.ConfigFlagsCurrFrame = g.IO.ConfigFlags;

        // Load settings on first frame, save settings when modified (after a delay)
        UpdateSettings();

        g.Time += g.IO.DeltaTime;
        g.FrameCount += 1;
        g.SessionDate = new ImGuiPackedDate { Packed = (ushort)g.PlatformIO.Platform_SessionDate };
        g.TooltipOverrideCount = 0;
        g.WindowsActiveCount = 0;
        g.MenusIdSubmittedThisFrame.resize(0);

        // Calculate frame-rate for the user, as a purely luxurious feature
        g.FramerateSecPerFrameAccum += g.IO.DeltaTime - g.FramerateSecPerFrame[g.FramerateSecPerFrameIdx];
        g.FramerateSecPerFrame[g.FramerateSecPerFrameIdx] = g.IO.DeltaTime;
        g.FramerateSecPerFrameIdx = (g.FramerateSecPerFrameIdx + 1) % g.FramerateSecPerFrame.Length;
        g.FramerateSecPerFrameCount = ImMath.ImMin(g.FramerateSecPerFrameCount + 1, g.FramerateSecPerFrame.Length);
        g.IO.Framerate = (g.FramerateSecPerFrameAccum > 0.0f) ? (1.0f / (g.FramerateSecPerFrameAccum / (float)g.FramerateSecPerFrameCount)) : float.MaxValue;

        // Process input queue (trickle as many events as possible), turn events into writes to IO structure
        g.InputEventsTrail.resize(0);
        UpdateInputEvents(g.IO.ConfigInputTrickleEventQueue);

        // Update viewports (after processing input queue, so io.MouseHoveredViewport is set)
        UpdateViewportsNewFrame();

        // Update texture list (collect destroyed textures, etc.)
        UpdateTexturesNewFrame();

        // Setup current font and draw list shared data
        SetupDrawListSharedData();
        UpdateFontsNewFrame();

        g.WithinFrameScope = true;

        // Mark rendering data as invalid to prevent user who may have a handle on it to use it.
        for (int i = 0; i < g.Viewports.Size; i++)
        {
            g.Viewports[i].DrawData = null;
            g.Viewports[i].DrawDataP.Valid = false;
        }

        // Drag and drop keep the source ID alive so even if the source disappear our state is consistent
        if (g.DragDropActive && g.DragDropPayload.SourceId == g.ActiveId)
            KeepAliveID(g.DragDropPayload.SourceId);

        // [DEBUG]
        if (!g.IO.ConfigDebugHighlightIdConflicts || !g.IO.KeyCtrl) // Count is locked while holding Ctrl
            g.DebugDrawIdConflictsId = 0;
        if (g.IO.ConfigDebugHighlightIdConflicts && g.HoveredIdPreviousFrameItemCount > 1)
            g.DebugDrawIdConflictsId = g.HoveredIdPreviousFrame;

        // Update HoveredId data
        if (g.HoveredIdPreviousFrame == 0)
            g.HoveredIdTimer = 0.0f;
        if (g.HoveredIdPreviousFrame == 0 || (g.HoveredId != 0 && g.ActiveId == g.HoveredId))
            g.HoveredIdNotActiveTimer = 0.0f;
        if (g.HoveredId != 0)
            g.HoveredIdTimer += g.IO.DeltaTime;
        if (g.HoveredId != 0 && g.ActiveId != g.HoveredId)
            g.HoveredIdNotActiveTimer += g.IO.DeltaTime;
        g.HoveredIdPreviousFrame = g.HoveredId;
        g.HoveredIdPreviousFrameItemCount = 0;
        g.HoveredId = 0;
        g.HoveredIdAllowOverlap = false;
        g.HoveredIdIsDisabled = false;

        // Clear ActiveID if the item is not alive anymore.
        // In 1.87, the common most call to KeepAliveID() was moved from GetID() to ItemAdd().
        // As a result, custom widget using ButtonBehavior() _without_ ItemAdd() need to call KeepAliveID() themselves.
        if (g.ActiveId != 0 && g.ActiveIdIsAlive != g.ActiveId && g.ActiveIdPreviousFrame == g.ActiveId)
            ClearActiveID();

        // Update ActiveId data (clear reference to active widget if the widget isn't alive anymore)
        if (g.ActiveId != 0)
            g.ActiveIdTimer += g.IO.DeltaTime;
        if (g.ActiveId != 0 && g.ActiveId == g.LastActiveId)
        {
            g.LastActiveIdWasSelected = g.ActiveIdWasSelected;
            g.LastActiveIdWasSoleSelected = g.ActiveIdWasSoleSelected;
        }
        g.LastActiveIdTimer += g.IO.DeltaTime;
        g.ActiveIdPreviousFrame = g.ActiveId;
        g.ActiveIdIsAlive = 0;
        g.AnyIdHasBeenEditedThisFrame = false;
        g.ActiveIdHasBeenEditedThisFrame = false;
        g.ActiveIdIsJustActivated = false;
        if (g.TempInputId != 0 && g.ActiveId != g.TempInputId)
            g.TempInputId = 0;
        if (g.InputTextReactivateId != 0 && g.InputTextReactivateId != g.DeactivatedItemData.ID)
            g.InputTextReactivateId = 0;
        if (g.ActiveId == 0)
        {
            g.ActiveIdUsingNavDirMask = 0x00;
            g.ActiveIdUsingAllKeyboardKeys = false;
        }
        if (g.DeactivatedItemData.ElapseFrame < g.FrameCount)
            g.DeactivatedItemData.ID = 0;
        g.DeactivatedItemData.IsAlive = false;
        if (g.InputTextDeactivatedState.ElapseFrame < g.FrameCount)
            g.InputTextDeactivatedState.ID = 0;

        // Record when we have been stationary as this state is preserved while over same item.
        // FIXME: The way this is expressed means user cannot alter HoverStationaryDelay during the frame to use varying values.
        // To allow this we should store HoverItemMaxStationaryTime+ID and perform the >= check in IsItemHovered() function.
        if (g.HoverItemDelayId != 0 && g.MouseStationaryTimer >= g.Style.HoverStationaryDelay)
            g.HoverItemUnlockedStationaryId = g.HoverItemDelayId;
        else if (g.HoverItemDelayId == 0)
            g.HoverItemUnlockedStationaryId = 0;
        if (g.HoveredWindow != null && g.MouseStationaryTimer >= g.Style.HoverStationaryDelay)
            g.HoverWindowUnlockedStationaryId = g.HoveredWindow.ID;
        else if (g.HoveredWindow == null)
            g.HoverWindowUnlockedStationaryId = 0;

        // Update hover delay for IsItemHovered() with delays and tooltips
        g.HoverItemDelayIdPreviousFrame = g.HoverItemDelayId;
        if (g.HoverItemDelayId != 0)
        {
            g.HoverItemDelayTimer += g.IO.DeltaTime;
            g.HoverItemDelayClearTimer = 0.0f;
            g.HoverItemDelayId = 0;
        }
        else if (g.HoverItemDelayTimer > 0.0f)
        {
            // This gives a little bit of leeway before clearing the hover timer, allowing mouse to cross gaps
            // We could expose 0.25f as style.HoverClearDelay but I am not sure of the logic yet, this is particularly subtle.
            g.HoverItemDelayClearTimer += g.IO.DeltaTime;
            if (g.HoverItemDelayClearTimer >= ImMath.ImMax(0.25f, g.IO.DeltaTime * 2.0f)) // ~7 frames at 30 Hz + allow for low framerate
                g.HoverItemDelayTimer = g.HoverItemDelayClearTimer = 0.0f; // May want a decaying timer, in which case need to clamp at max first, based on max of caller last requested timer.
        }

        // Close popups on focus lost (currently wip/opt-in)
        //if (g.IO.AppFocusLost)
        //    ClosePopupsExceptModals();

        // Update keyboard input state
        UpdateKeyboardInputs();

        //IM_ASSERT(g.IO.KeyCtrl == IsKeyDown(ImGuiKey_LeftCtrl) || IsKeyDown(ImGuiKey_RightCtrl));
        //IM_ASSERT(g.IO.KeyShift == IsKeyDown(ImGuiKey_LeftShift) || IsKeyDown(ImGuiKey_RightShift));
        //IM_ASSERT(g.IO.KeyAlt == IsKeyDown(ImGuiKey_LeftAlt) || IsKeyDown(ImGuiKey_RightAlt));
        //IM_ASSERT(g.IO.KeySuper == IsKeyDown(ImGuiKey_LeftSuper) || IsKeyDown(ImGuiKey_RightSuper));

        // Drag and drop
        g.DragDropAcceptIdPrev = g.DragDropAcceptIdCurr;
        g.DragDropAcceptIdCurr = 0;
        g.DragDropAcceptFlagsPrev = g.DragDropAcceptFlagsCurr;
        g.DragDropAcceptFlagsCurr = ImGuiDragDropFlags_None;
        g.DragDropAcceptIdCurrRectSurface = float.MaxValue;
        g.DragDropWithinSource = false;
        g.DragDropWithinTarget = false;
        g.DragDropHoldJustPressedId = 0;
        if (g.DragDropActive)
        {
            // Also works when g.ActiveId==0 (aka leftover payload in progress, no active id)
            // You may disable this externally by hijacking the input route:
            //  'if (GetDragDropPayload() != NULL) { Shortcut(ImGuiKey_Escape, ImGuiInputFlags_RouteGlobal | ImGuiInputFlags_RouteOverActive); }
            // but you will not get a return value from Shortcut() due to ActiveIdUsingAllKeyboardKeys logic. You can however poll IsKeyPressed(ImGuiKey_Escape) afterwards.
            ImGuiID owner_id = g.ActiveId != 0 ? g.ActiveId : ImGuiHash.ImHashStr("##DragDropCancelHandler", 0);
            if (Shortcut(ImGuiKey_Escape, ImGuiInputFlags_RouteGlobal, owner_id))
            {
                ClearActiveID();
                ClearDragDrop();
            }
        }
        g.TooltipPreviousWindow = null;

        // Update keyboard/gamepad navigation
        NavUpdate();

        // Update mouse input state
        UpdateMouseInputs();

        // Undocking
        // (needs to be before UpdateMouseMovingWindowNewFrame so the window is already offset and following the mouse on the detaching frame)
        DockContextNewFrameUpdateUndocking(g);

        // Mark all windows as not visible and compact unused memory.
        Debug.Assert(g.WindowsFocusOrder.Size <= g.Windows.Size);
        bool gc_all = (g.GcCompactAll || g.IO.ConfigMemoryCompactTimer < 0.0f);
        float memory_compact_start_time = gc_all ? float.MaxValue : (float)g.Time - g.IO.ConfigMemoryCompactTimer;
        for (int i = 0; i < g.Windows.Size; i++)
        {
            ImGuiWindow window = g.Windows[i];
            window.WasActive = window.Active;
            window.Active = false;
            window.WriteAccessed = false;
            window.BeginCountPreviousFrame = window.BeginCount;
            window.BeginCount = 0;

            // Garbage collect transient buffers of recently unused windows
            if ((!window.WasActive || gc_all) && !window.MemoryCompacted && window.LastTimeActive < memory_compact_start_time)
                GcCompactTransientWindowBuffers(window);
        }

        // Find hovered window
        // (needs to be before UpdateMouseMovingWindowNewFrame so we fill g.HoveredWindowUnderMovingWindow on the mouse release frame)
        // (currently needs to be done after the WasActive=Active loop and FindHoveredWindowEx uses ->Active)
        UpdateHoveredWindowAndCaptureFlags(g.IO.MousePos);

        // Handle user moving window with mouse (at the beginning of the frame to avoid input lag or sheering)
        UpdateMouseMovingWindowNewFrame();

        // Background darkening/whitening
        if (GetTopMostPopupModal() != null || (g.NavWindowingTarget != null && g.NavWindowingHighlightAlpha > 0.0f))
            g.DimBgRatio = ImMath.ImMin(g.DimBgRatio + g.IO.DeltaTime * 6.0f, 1.0f);
        else
            g.DimBgRatio = ImMath.ImMax(g.DimBgRatio - g.IO.DeltaTime * 10.0f, 0.0f);

        g.MouseCursor = ImGuiMouseCursor_Arrow;
        g.WantCaptureMouseNextFrame = g.WantCaptureKeyboardNextFrame = g.WantTextInputNextFrame = -1;

        // Platform IME data: reset for the frame
        g.PlatformImeDataPrev = g.PlatformImeData;
        g.PlatformImeData.WantVisible = false;
        g.PlatformImeData.WantTextInput = false;

        // Mouse wheel scrolling, scale
        UpdateMouseWheel();

        // Garbage collect transient buffers of recently unused tables
        for (int i = 0; i < g.TablesLastTimeActive.Size; i++)
            if (g.TablesLastTimeActive[i] >= 0.0f && g.TablesLastTimeActive[i] < memory_compact_start_time)
                TableGcCompactTransientBuffers(g.Tables.GetByIndex(i));
        for (int i = 0; i < g.TablesTempData.Size; i++)
        {
            ImGuiTableTempData table_temp_data = g.TablesTempData[i];
            table_temp_data.ReconcileColumnsRequests.clear(); // Unusual: clear every frame because this is rarely used.
            if (table_temp_data.LastTimeActive >= 0.0f && table_temp_data.LastTimeActive < memory_compact_start_time)
                TableGcCompactTransientBuffers(table_temp_data);
        }
        if (g.GcCompactAll)
            GcCompactTransientMiscBuffers();
        g.GcCompactAll = false;

        // Closing the focused window restore focus to the first active root window in descending z-order
        if (g.NavWindow != null && !g.NavWindow.WasActive)
            FocusTopMostWindowUnderOne(null, null, null, ImGuiFocusRequestFlags_RestoreFocusedChild);

        // No window should be open at the beginning of the frame.
        // But in order to allow the user to call NewFrame() multiple times without calling Render(), we are doing an explicit clear.
        g.CurrentWindowStack.resize(0);
        g.BeginPopupStack.resize(0);
        g.ItemFlagsStack.resize(0);
        g.ItemFlagsStack.push_back(ImGuiItemFlags_Default_); // Default flags
        g.CurrentItemFlags = g.ItemFlagsStack.back();
        g.GroupStack.resize(0);

        // Docking
        DockContextNewFrameUpdateDocking(g);

        // [DEBUG] Update debug features
        UpdateDebugToolItemPicker();
        UpdateDebugToolItemPathQuery();
        UpdateDebugToolFlashStyleColor();
        if (g.DebugLocateFrames > 0 && --g.DebugLocateFrames == 0)
        {
            g.DebugLocateId = 0;
            g.DebugBreakInLocateId = false;
        }
        if (g.DebugLogAutoDisableFrames > 0 && --g.DebugLogAutoDisableFrames == 0)
        {
            //DebugLog("(Debug Log: Auto-disabled some ImGuiDebugLogFlags after 2 frames)\n"); // IMGUI_DEBUG_LOG: pure logging, omitted
            g.DebugLogFlags &= ~g.DebugLogAutoDisableFlags;
            g.DebugLogAutoDisableFlags = ImGuiDebugLogFlags_None;
        }

        // Create implicit/fallback window - which we will only render it if the user has added something to it.
        // We don't use "Debug" to avoid colliding with user trying to create a "Debug" window with custom flags.
        // This fallback is particularly important as it prevents ImGui:: calls from crashing.
        g.WithinFrameScopeWithImplicitWindow = true;
        SetNextWindowSize(new ImVec2(400, 400), ImGuiCond_FirstUseEver);
        Begin("Debug##Default");
        Debug.Assert(g.CurrentWindow.IsFallbackWindow == true);

        // Store stack sizes
        g.ErrorCountCurrentFrame = 0;
        ErrorRecoveryStoreState(ref g.StackSizesInNewFrame);

        // [DEBUG] When io.ConfigDebugBeginReturnValue is set, we make Begin()/BeginChild() return false at different level of the window-stack,
        // allowing to validate correct Begin/End behavior in user code.
        if (g.IO.ConfigDebugBeginReturnValueLoop)
            g.DebugBeginReturnValueCullDepth = (g.DebugBeginReturnValueCullDepth == -1) ? (sbyte)0 : (sbyte)((g.DebugBeginReturnValueCullDepth + ((g.FrameCount % 4) == 0 ? 1 : 0)) % 10);
        else
            g.DebugBeginReturnValueCullDepth = -1;

        CallContextHooks(g, ImGuiContextHookType_NewFramePost);
    }

    // FIXME: Add a more explicit sort order in the window structure.
    // static int IMGUI_CDECL ChildWindowComparer(const void* lhs, const void* rhs) (imgui.cpp)
    static int ChildWindowComparer(ImGuiWindow lhs, ImGuiWindow rhs)
    {
        ImGuiWindow a = lhs;
        ImGuiWindow b = rhs;
        int d = (a.Flags & ImGuiWindowFlags_Popup) - (b.Flags & ImGuiWindowFlags_Popup);
        if (d != 0)
            return d;
        d = (a.Flags & ImGuiWindowFlags_Tooltip) - (b.Flags & ImGuiWindowFlags_Tooltip);
        if (d != 0)
            return d;
        return a.BeginOrderWithinParent - b.BeginOrderWithinParent;
    }

    // static void AddWindowToSortBuffer(ImVector<ImGuiWindow*>* out_sorted_windows, ImGuiWindow* window) (imgui.cpp)
    static void AddWindowToSortBuffer(ImVector<ImGuiWindow> out_sorted_windows, ImGuiWindow window)
    {
        out_sorted_windows.push_back(window);
        if (window.Active)
        {
            int count = window.DC.ChildWindows.Size;
            Array.Sort(window.DC.ChildWindows.Data, 0, count, System.Collections.Generic.Comparer<ImGuiWindow>.Create((lhs, rhs) => ChildWindowComparer(lhs, rhs))); // ImQsort()
            for (int i = 0; i < count; i++)
            {
                ImGuiWindow child = window.DC.ChildWindows[i];
                if (child.Active)
                    AddWindowToSortBuffer(out_sorted_windows, child);
            }
        }
    }

    // static void AddWindowToDrawData(ImGuiWindow* window, int layer) (imgui.cpp)
    static void AddWindowToDrawData(ImGuiWindow window, int layer)
    {
        ImGuiContext g = GImGui;
        ImGuiViewportP viewport = window.Viewport;
        Debug.Assert(viewport != null);
        g.IO.MetricsRenderWindows++;
        if (window.DrawList._Splitter._Count > 1)
            window.DrawList._Splitter.Merge(window.DrawList); // ImDrawList::ChannelsMerge(): Merge if user forgot to merge back. Also required in Docking branch for ImGuiWindowFlags_DockNodeHost windows.
        ImGuiCore.AddDrawListToDrawDataEx(viewport.DrawDataP, viewport.DrawDataBuilder.GetLayer(layer), window.DrawList);
        for (int i = 0; i < window.DC.ChildWindows.Size; i++)
        {
            ImGuiWindow child = window.DC.ChildWindows[i];
            if (IsWindowActiveAndVisible(child)) // Clipped children may have been marked not active
                AddWindowToDrawData(child, layer);
        }
    }

    // static inline int GetWindowDisplayLayer(ImGuiWindow* window) (imgui.cpp)
    static int GetWindowDisplayLayer(ImGuiWindow window)
    {
        return (window.Flags & ImGuiWindowFlags_Tooltip) != 0 ? 1 : 0;
    }

    // Layer is locked for the root window, however child windows may use a different viewport (e.g. extruding menu)
    // static inline void AddRootWindowToDrawData(ImGuiWindow* window) (imgui.cpp)
    static void AddRootWindowToDrawData(ImGuiWindow window)
    {
        AddWindowToDrawData(window, GetWindowDisplayLayer(window));
    }

    // static void FlattenDrawDataIntoSingleLayer(ImDrawDataBuilder* builder) (imgui.cpp)
    static void FlattenDrawDataIntoSingleLayer(ImDrawDataBuilder builder)
    {
        int n = builder.GetLayer(0).Size;
        int full_size = n;
        for (int i = 1; i < builder.Layers.Length; i++)
            full_size += builder.GetLayer(i).Size;
        builder.GetLayer(0).resize(full_size);
        for (int layer_n = 1; layer_n < builder.Layers.Length; layer_n++)
        {
            ImVector<ImDrawList> layer = builder.GetLayer(layer_n);
            if (layer.empty())
                continue;
            Array.Copy(layer.Data, 0, builder.GetLayer(0).Data, n, layer.Size);
            n += layer.Size;
            layer.resize(0);
        }
    }

    // static void InitViewportDrawData(ImGuiViewportP* viewport) (imgui.cpp)
    static void InitViewportDrawData(ImGuiViewportP viewport)
    {
        ImGuiContext g = GImGui;
        ImDrawData draw_data = viewport.DrawDataP;

        viewport.DrawData = draw_data; // Make publicly accessible
        viewport.DrawDataBuilder.Layers[0] = draw_data.CmdLists;
        viewport.DrawDataBuilder.Layers[1] = viewport.DrawDataBuilder.LayerData1;
        viewport.DrawDataBuilder.Layers[0].resize(0);
        viewport.DrawDataBuilder.Layers[1].resize(0);

        // When minimized, we report draw_data->DisplaySize as zero to be consistent with non-viewport mode,
        // and to allow applications/backends to easily skip rendering.
        bool is_minimized = (viewport.Flags & ImGuiViewportFlags_IsMinimized) != 0;

        draw_data.Valid = true;
        draw_data.FrameCount = g.FrameCount;
        draw_data.TotalVtxCount = 0;
        draw_data.TotalIdxCount = 0;
        draw_data.DisplayPos = viewport.Pos;
        draw_data.DisplaySize = is_minimized ? new ImVec2(0.0f, 0.0f) : viewport.Size;
        draw_data.FramebufferScale = viewport.FramebufferScale;
        draw_data.OwnerViewport = viewport;
    }

    // Push a clipping rectangle for both ImGui logic (hit-testing etc.) and low-level ImDrawList rendering.
    // - When using this function it is sane to ensure that float are perfectly rounded to integer values,
    //   so that e.g. (int)(max.x-min.x) in user's render produce correct result.
    // - If the code here changes, may need to update code of functions like NextColumn() and PushColumnClipRect():
    //   some frequently called functions which to modify both channels and clipping simultaneously tend to use the
    //   more specialized SetWindowClipRectBeforeSetChannel() to avoid extraneous updates of underlying ImDrawCmds.
    // - This is analogous to PushFont()/PopFont() in the sense that are a mixing a global stack and a window stack,
    //   which in the case of ClipRect is not so problematic but tends to be more restrictive for fonts.
    public static void PushClipRect(ImVec2 clip_rect_min, ImVec2 clip_rect_max, bool intersect_with_current_clip_rect)
    {
        ImGuiWindow window = GetCurrentWindow();
        window.DrawList.PushClipRect(clip_rect_min, clip_rect_max, intersect_with_current_clip_rect);
        ImVec4 cr = window.DrawList._ClipRectStack.back();
        window.ClipRect = new ImRect(cr.x, cr.y, cr.z, cr.w); // ImRect(ImVec4)
    }

    public static void PopClipRect()
    {
        ImGuiWindow window = GetCurrentWindow();
        window.DrawList.PopClipRect();
        ImVec4 cr = window.DrawList._ClipRectStack.back();
        window.ClipRect = new ImRect(cr.x, cr.y, cr.z, cr.w); // ImRect(ImVec4)
    }

    // static void ImGui::RenderDimmedBackgroundBehindWindow(ImGuiWindow* window, ImU32 col)
    static void RenderDimmedBackgroundBehindWindow(ImGuiWindow window, ImU32 col)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;

        ImGuiViewportP viewport = window.Viewport;
        ImRect viewport_rect = viewport.GetMainRect();

        // Draw behind window by moving the draw command at the FRONT of the draw list
        {
            // Draw list have been trimmed already, hence the explicit recreation of a draw command if missing.
            // FIXME: This is a little bit complicated, solely to avoid creating/injecting an extra drawlist in drawdata.
            ImDrawList draw_list = window.RootWindowDockTree.DrawList;
            draw_list._Splitter.Merge(draw_list); // C# port: ImDrawList::ChannelsMerge()
            if (draw_list.CmdBuffer.Size == 0)
                draw_list.AddDrawCmd();
            draw_list.PushClipRect(viewport_rect.Min - new ImVec2(1, 1), viewport_rect.Max + new ImVec2(1, 1), false); // FIXME: Need to strictly ensure ImDrawCmd are not merged (ElemCount==6 checks below will verify that)
            ImDrawCmd cmd = draw_list.CmdBuffer.back();
            Debug.Assert(cmd.ElemCount == 0);
            draw_list.AddRectFilled(viewport_rect.Min, viewport_rect.Max, col);
            cmd = draw_list.CmdBuffer.back();
            draw_list.CmdBuffer.pop_back();
            draw_list.CmdBuffer.push_front(cmd);
            draw_list.AddDrawCmd(); // We need to create a command as CmdBuffer.back().IdxOffset won't be correct if we append to same command.
            draw_list.PopClipRect();
        }

        // Draw over sibling docking nodes in a same docking tree
        if (window.RootWindow.DockIsActive)
        {
            ImDrawList draw_list = FindFrontMostVisibleChildWindow(window.RootWindowDockTree).DrawList;
            draw_list._Splitter.Merge(draw_list);
            if (draw_list.CmdBuffer.Size == 0)
                draw_list.AddDrawCmd();
            draw_list.PushClipRect(viewport_rect.Min, viewport_rect.Max, false);
            RenderRectFilledWithHole(draw_list, window.RootWindowDockTree.Rect(), window.RootWindow.Rect(), col, 0.0f);// window->RootWindowDockTree->WindowRounding);
            draw_list.PopClipRect();
        }
    }

    // ImGuiWindow* ImGui::FindBottomMostVisibleWindowWithinBeginStack(ImGuiWindow* parent_window)
    public static ImGuiWindow FindBottomMostVisibleWindowWithinBeginStack(ImGuiWindow parent_window)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow bottom_most_visible_window = parent_window;
        for (int i = FindWindowDisplayIndex(parent_window); i >= 0; i--)
        {
            ImGuiWindow window = g.Windows[i];
            if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0)
                continue;
            if (!IsWindowWithinBeginStackOf(window, parent_window))
                break;
            if (IsWindowActiveAndVisible(window) && GetWindowDisplayLayer(window) <= GetWindowDisplayLayer(parent_window))
                bottom_most_visible_window = window;
        }
        return bottom_most_visible_window;
    }

    // static void ImGui::RenderDimmedBackgrounds()
    static void RenderDimmedBackgrounds()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow modal_window = GetTopMostAndVisiblePopupModal();
        if (g.DimBgRatio <= 0.0f && g.NavWindowingHighlightAlpha <= 0.0f)
            return;
        bool dim_bg_for_modal = (modal_window != null);
        bool dim_bg_for_window_list = (g.NavWindowingTargetAnim != null && g.NavWindowingTargetAnim.Active);
        if (!dim_bg_for_modal && !dim_bg_for_window_list)
            return;

        ImGuiViewport[] viewports_already_dimmed = new ImGuiViewport[2] { null, null };
        if (dim_bg_for_modal)
        {
            // Draw dimming behind modal or a begin stack child, whichever comes first in draw order.
            ImGuiWindow dim_behind_window = FindBottomMostVisibleWindowWithinBeginStack(modal_window);
            RenderDimmedBackgroundBehindWindow(dim_behind_window, GetColorU32(modal_window.DC.ModalDimBgColor, g.DimBgRatio));
            viewports_already_dimmed[0] = modal_window.Viewport;
        }
        else if (dim_bg_for_window_list)
        {
            // Draw dimming behind Ctrl+Tab target window and behind Ctrl+Tab UI window
            RenderDimmedBackgroundBehindWindow(g.NavWindowingTargetAnim, GetColorU32(ImGuiCol_NavWindowingDimBg, g.DimBgRatio));
            viewports_already_dimmed[0] = g.NavWindowingTargetAnim.Viewport;
            if (g.NavWindowingListWindow != null && g.NavWindowingListWindow.Active && g.NavWindowingListWindow.Viewport != null && g.NavWindowingListWindow.Viewport != g.NavWindowingTargetAnim.Viewport)
            {
                RenderDimmedBackgroundBehindWindow(g.NavWindowingListWindow, GetColorU32(ImGuiCol_NavWindowingDimBg, g.DimBgRatio));
                viewports_already_dimmed[1] = g.NavWindowingListWindow.Viewport;
            }

            // Draw border around Ctrl+Tab target window
            ImGuiWindow window = g.NavWindowingTargetAnim;
            ImGuiViewport viewport = window.Viewport;
            float distance = g.FontSize;
            ImRect bb = window.Rect();
            bb.Expand(distance);
            if (bb.GetWidth() >= viewport.Size.x && bb.GetHeight() >= viewport.Size.y)
                bb.Expand(-distance - 1.0f); // If a window fits the entire viewport, adjust its highlight inward
            window.DrawList._Splitter.Merge(window.DrawList);
            if (window.DrawList.CmdBuffer.Size == 0)
                window.DrawList.AddDrawCmd();
            window.DrawList.PushClipRect(viewport.Pos, viewport.Pos + viewport.Size);
            window.DrawList.AddRect(bb.Min, bb.Max, GetColorU32(ImGuiCol_NavWindowingHighlight, g.NavWindowingHighlightAlpha), window.WindowRounding, 3.0f); // FIXME-DPI
            window.DrawList.PopClipRect();
        }

        // Draw dimming background on _other_ viewports than the ones our windows are in
        for (int i = 0; i < g.Viewports.Size; i++)
        {
            ImGuiViewportP viewport = g.Viewports[i];
            if (viewport == viewports_already_dimmed[0] || viewport == viewports_already_dimmed[1])
                continue;
            if (modal_window != null && viewport.Window != null && IsWindowAbove(viewport.Window, modal_window))
                continue;
            ImDrawList draw_list = GetForegroundDrawList(viewport);
            ImU32 dim_bg_col = GetColorU32(dim_bg_for_modal ? ImGuiCol_ModalWindowDimBg : ImGuiCol_NavWindowingDimBg, g.DimBgRatio);
            draw_list.AddRectFilled(viewport.Pos, viewport.Pos + viewport.Size, dim_bg_col);
        }
    }

    // This is normally called by Render(). You may want to call it directly if you want to avoid calling Render() but the gain will be very minimal.
    // void ImGui::EndFrame()
    public static void EndFrame()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.Initialized);

        // Don't process EndFrame() multiple times.
        if (g.FrameCountEnded == g.FrameCount)
            return;
        ImUtil.ImAssertUserError(g.WithinFrameScope, "Forgot to call ImGui::NewFrame()?");

        CallContextHooks(g, ImGuiContextHookType_EndFramePre);

        // [EXPERIMENTAL] Recover from errors
        if (g.IO.ConfigErrorRecovery)
            ErrorRecoveryTryToRecoverState(ref g.StackSizesInNewFrame);
        ErrorCheckEndFrameSanityChecks();
        ErrorCheckEndFrameFinalizeErrorTooltip();

        // Notify Platform/OS when our Input Method Editor cursor has moved (e.g. CJK inputs using Microsoft IME)
        ImGuiPlatformImeData ime_data = g.PlatformImeData;
        if (g.PlatformIO.Platform_SetImeDataFn != null && !PlatformImeDataEquals(ime_data, g.PlatformImeDataPrev)) // memcmp()
        {
            ImGuiViewport viewport = FindViewportByID(ime_data.ViewportId);
            if (viewport == null)
                viewport = GetMainViewport();
            // IMGUI_DEBUG_LOG_IO: pure logging, omitted
            g.PlatformIO.Platform_SetImeDataFn(g, viewport, ime_data);
        }
        g.WantTextInputNextFrame = ime_data.WantTextInput ? 1 : 0;

        // Hide and unfocus implicit/fallback "Debug" window if it hasn't been used
        g.WithinFrameScopeWithImplicitWindow = false;
        if (g.CurrentWindow != null && g.CurrentWindow.IsFallbackWindow && g.CurrentWindow.WriteAccessed == false)
        {
            g.CurrentWindow.Active = false;
            if (g.NavWindow != null && g.NavWindow.RootWindow == g.CurrentWindow)
                FocusWindow(null);
        }
        End();

        // Update navigation: Ctrl+Tab, wrap-around requests
        NavEndFrame();

        // Update docking
        DockContextEndFrame(g);

        SetCurrentViewport(null, null);

        // Drag and Drop: Elapse payload (if delivered, or if source stops being submitted)
        if (g.DragDropActive)
        {
            bool is_delivered = g.DragDropPayload.Delivery;
            bool is_elapsed = (g.DragDropSourceFrameCount + 1 < g.FrameCount) && ((g.DragDropSourceFlags & ImGuiDragDropFlags_PayloadAutoExpire) != 0 || g.DragDropMouseButton == -1 || !IsMouseDown(g.DragDropMouseButton));
            if (is_delivered || is_elapsed)
                ClearDragDrop();
        }

        // Drag and Drop: Fallback for missing source tooltip. This is not ideal but better than nothing.
        // If you want to handle source item disappearing: instead of submitting your description tooltip
        // in the BeginDragDropSource() block of the dragged item, you can submit them from a safe single spot
        // (e.g. end of your item loop, or before EndFrame) by reading payload data.
        // In the typical case, the contents of drag tooltip should be possible to infer solely from payload data.
        if (g.DragDropActive && g.DragDropSourceFrameCount + 1 < g.FrameCount && (g.DragDropSourceFlags & ImGuiDragDropFlags_SourceNoPreviewTooltip) == 0)
        {
            g.DragDropWithinSource = true;
            SetTooltip("...");
            g.DragDropWithinSource = false;
        }

        // End frame
        g.WithinFrameScope = false;
        g.FrameCountEnded = g.FrameCount;
        UpdateFontsEndFrame();

        // Initiate moving window + handle left-click and right-click focus
        UpdateMouseMovingWindowEndFrame();

        // Update user-facing viewport list (g.Viewports -> g.PlatformIO.Viewports after filtering out some)
        UpdateViewportsEndFrame();

        // Sort the window list so that all child windows are after their parent
        // We cannot do that on FocusWindow() because children may not exist yet
        g.WindowsTempSortBuffer.resize(0);
        g.WindowsTempSortBuffer.reserve(g.Windows.Size);
        for (int i = 0; i < g.Windows.Size; i++)
        {
            ImGuiWindow window = g.Windows[i];
            if (window.Active && (window.Flags & ImGuiWindowFlags_ChildWindow) != 0)       // if a child is active its parent will add it
                continue;
            AddWindowToSortBuffer(g.WindowsTempSortBuffer, window);
        }

        // This usually assert if there is a mismatch between the ImGuiWindowFlags_ChildWindow / ParentWindow values and DC.ChildWindows[] in parents, aka we've done something wrong.
        Debug.Assert(g.Windows.Size == g.WindowsTempSortBuffer.Size);
        ImVectorSwap(g.Windows, g.WindowsTempSortBuffer); // g.Windows.swap(g.WindowsTempSortBuffer)
        g.IO.MetricsActiveWindows = g.WindowsActiveCount;

        UpdateTexturesEndFrame();

        // Unlock font atlas
        for (int i = 0; i < g.FontAtlases.Size; i++)
            g.FontAtlases[i].Locked = false;

        // Clear Input data for next frame
        g.IO.MousePosPrev = g.IO.MousePos;
        g.IO.AppFocusLost = false;
        g.IO.MouseWheel = 0.0f;
        g.IO.MouseWheelH = 0.0f;
        g.IO.InputQueueCharacters.resize(0);

        CallContextHooks(g, ImGuiContextHookType_EndFramePost);
    }

    // Port of memcmp(ime_data, &g.PlatformImeDataPrev, sizeof(ImGuiPlatformImeData)) != 0
    static bool PlatformImeDataEquals(ImGuiPlatformImeData a, ImGuiPlatformImeData b)
    {
        return a.WantVisible == b.WantVisible && a.WantTextInput == b.WantTextInput
            && a.InputPos == b.InputPos && a.InputLineHeight == b.InputLineHeight && a.ViewportId == b.ViewportId;
    }

    // ImVector::swap() helper (member templates have no direct C# equivalent).
    static void ImVectorSwap<T>(ImVector<T> a, ImVector<T> b)
    {
        (a.Data, b.Data) = (b.Data, a.Data);
        (a.Size, b.Size) = (b.Size, a.Size);
        (a.Capacity, b.Capacity) = (b.Capacity, a.Capacity);
    }

    // ImRect::ContainsWithPad(const ImVec2& p, const ImVec2& pad) (imgui_internal.h)
    static bool RectContainsWithPad(ImRect r, ImVec2 p, ImVec2 pad)
    {
        if (p.x < r.Min.x - pad.x || p.y < r.Min.y - pad.y || p.x >= r.Max.x + pad.x || p.y >= r.Max.y + pad.y)
            return false;
        return true;
    }

    // Prepare the data for rendering so you can call GetDrawData()
    // (As with anything within the ImGui:: namespace this doesn't touch your GPU or graphics API at all:
    // it is the role of the ImGui_ImplXXXX_RenderDrawData() function provided by the renderer backend)
    // void ImGui::Render()
    public static void Render()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.Initialized);

        if (g.FrameCountEnded != g.FrameCount)
            EndFrame();
        if (g.FrameCountRendered == g.FrameCount)
            return;
        g.FrameCountRendered = g.FrameCount;

        g.IO.MetricsRenderWindows = 0;
        CallContextHooks(g, ImGuiContextHookType_RenderPre);

        // Add background ImDrawList (for each active viewport)
        for (int i = 0; i < g.Viewports.Size; i++)
        {
            ImGuiViewportP viewport = g.Viewports[i];
            InitViewportDrawData(viewport);
            if (viewport.BgFgDrawLists[0] != null && viewport.BgFgDrawListsLastFrameActive[0] == g.FrameCount)
                ImGuiCore.AddDrawListToDrawDataEx(viewport.DrawDataP, viewport.DrawDataBuilder.Layers[0], GetBackgroundDrawList(viewport));
        }

        // Draw modal/window whitening backgrounds
        RenderDimmedBackgrounds();

        // Add ImDrawList to render
        ImGuiWindow[] windows_to_render_top_most = new ImGuiWindow[2];
        windows_to_render_top_most[0] = (g.NavWindowingTarget != null && (g.NavWindowingTarget.Flags & ImGuiWindowFlags_NoBringToFrontOnFocus) == 0) ? g.NavWindowingTarget.RootWindowDockTree : null;
        windows_to_render_top_most[1] = (g.NavWindowingTarget != null) ? g.NavWindowingListWindow : null;
        for (int i = 0; i < g.Windows.Size; i++)
        {
            ImGuiWindow window = g.Windows[i];
            if (IsWindowActiveAndVisible(window) && (window.Flags & ImGuiWindowFlags_ChildWindow) == 0 && window != windows_to_render_top_most[0] && window != windows_to_render_top_most[1])
                AddRootWindowToDrawData(window);
        }
        for (int n = 0; n < windows_to_render_top_most.Length; n++)
            if (windows_to_render_top_most[n] != null && IsWindowActiveAndVisible(windows_to_render_top_most[n])) // NavWindowingTarget is always temporarily displayed as the top-most window
                AddRootWindowToDrawData(windows_to_render_top_most[n]);

        // Draw software mouse cursor if requested by io.MouseDrawCursor flag
        if (g.IO.MouseDrawCursor && g.MouseCursor != ImGuiMouseCursor_None)
            RenderMouseCursor(g.IO.MousePos, g.Style.MouseCursorScale, g.MouseCursor, ImGuiCore.IM_COL32_WHITE, ImGuiCore.IM_COL32_BLACK, ImGuiCore.IM_COL32(0, 0, 0, 48));

        // Setup ImDrawData structures for end-user
        g.IO.MetricsRenderVertices = 0;
        g.IO.MetricsRenderIndices = 0;
        for (int i = 0; i < g.Viewports.Size; i++)
        {
            ImGuiViewportP viewport = g.Viewports[i];
            FlattenDrawDataIntoSingleLayer(viewport.DrawDataBuilder);

            // Add foreground ImDrawList (for each active viewport)
            if (viewport.BgFgDrawLists[1] != null && viewport.BgFgDrawListsLastFrameActive[1] == g.FrameCount)
                ImGuiCore.AddDrawListToDrawDataEx(viewport.DrawDataP, viewport.DrawDataBuilder.Layers[0], GetForegroundDrawList(viewport));

            // We call _PopUnusedDrawCmd() last thing, as RenderDimmedBackgrounds() rely on a valid command being there (especially in docking branch).
            ImDrawData draw_data = viewport.DrawDataP;
            for (int j = 0; j < draw_data.CmdLists.Size; j++)
                draw_data.CmdLists[j]._PopUnusedDrawCmd();
            draw_data.CmdListsCount = draw_data.CmdLists.Size; // #ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS
            g.IO.MetricsRenderVertices += draw_data.TotalVtxCount;
            g.IO.MetricsRenderIndices += draw_data.TotalIdxCount;
        }

        // #ifndef IMGUI_DISABLE_DEBUG_TOOLS: ImFontAtlasDebugLogTextureRequests() — pure texture-request logging, omitted
        // if ((g.IO.BackendFlags & ImGuiBackendFlags_RendererHasTextures) != 0)
        //     for each atlas in g.FontAtlases: ImFontAtlasDebugLogTextureRequests(atlas);

        CallContextHooks(g, ImGuiContextHookType_RenderPost);
    }

    // Calculate text size. Text can be multi-line. Optionally ignore text after a ## marker.
    // CalcTextSize("") should return ImVec2(0.0f, g.FontSize)
    // ImVec2 ImGui::CalcTextSize(const char* text, const char* text_end, bool hide_text_after_double_hash, float wrap_width)
    public static ImVec2 CalcTextSize(string text, int text_end = -1, bool hide_text_after_double_hash = true, float wrap_width = -1.0f)
    {
        ImGuiContext g = GImGui;

        int text_display_end;
        if (hide_text_after_double_hash)
            text_display_end = FindRenderedTextEnd(text, text_end);      // Hide anything after a '##' string
        else
            text_display_end = text_end;
        if (text_display_end < 0)
            text_display_end = text.Length;

        ImFont font = g.Font;
        float font_size = g.FontSize;
        if (text_display_end == 0) // text == text_display_end
            return new ImVec2(0.0f, font_size);
        ImVec2 text_size = font.CalcTextSizeA(font_size, float.MaxValue, wrap_width, text, 0, text_display_end);

        // Round
        // (see 7b0bf230, 4622fa4b6, #791 for details about this.)
        // FIXME: Add a way to disable this.
        text_size.x = ImCeilFast(text_size.x); // ImCeilFast(): (int)f + (f > (float)(int)f)

        return text_size;
    }

    // Span overload used by the non-allocating formatted-text paths.
    public static ImVec2 CalcTextSize(ReadOnlySpan<char> text, bool hide_text_after_double_hash = true, float wrap_width = -1.0f)
    {
        ImGuiContext g = GImGui;

        int text_display_end;
        if (hide_text_after_double_hash)
            text_display_end = FindRenderedTextEnd(text);                // Hide anything after a '##' string
        else
            text_display_end = text.Length;

        ImFont font = g.Font;
        float font_size = g.FontSize;
        if (text_display_end == 0) // text == text_display_end
            return new ImVec2(0.0f, font_size);
        ImVec2 text_size = font.CalcTextSizeA(font_size, float.MaxValue, wrap_width, text, 0, text_display_end);

        text_size.x = ImCeilFast(text_size.x);

        return text_size;
    }

    // inline float ImCeilFast(float f) (imgui_internal.h): int i = (int)f; return (float)(i + (f > (float)i));
    static float ImCeilFast(float f)
    {
        int i = (int)f;
        return i + (f > (float)i ? 1 : 0);
    }

    // Find window given position, search front-to-back
    // - Typically write output back to g.HoveredWindow and g.HoveredWindowUnderMovingWindow.
    // - FIXME: Note that we have an inconsequential lag here: OuterRectClipped is updated in Begin(), so windows moved programmatically
    //   with SetWindowPos() and not SetNextWindowPos() will have that rectangle lagging by a frame at the time FindHoveredWindow() is
    //   called, aka before the next Begin(). Moving window isn't affected.
    // - The 'find_first_and_in_any_viewport = true' mode is only used by TestEngine. It is simpler to maintain here.
    // void ImGui::FindHoveredWindowEx(const ImVec2& pos, bool find_first_and_in_any_viewport, ImGuiWindow** out_hovered_window, ImGuiWindow** out_hovered_window_under_moving_window)
    public static void FindHoveredWindowEx(ImVec2 pos, bool find_first_and_in_any_viewport, out ImGuiWindow out_hovered_window, out ImGuiWindow out_hovered_window_under_moving_window)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow hovered_window = null;
        ImGuiWindow hovered_window_under_moving_window = null;

        // Special handling for the window being moved: Ignore the mouse viewport check (because it may reset/lose its viewport during the undocking frame)
        ImGuiViewportP backup_moving_window_viewport = null;
        if (find_first_and_in_any_viewport == false && g.MovingWindow != null)
        {
            backup_moving_window_viewport = g.MovingWindow.Viewport;
            g.MovingWindow.Viewport = g.MouseViewport;
            if ((g.MovingWindow.Flags & ImGuiWindowFlags_NoMouseInputs) == 0)
                hovered_window = g.MovingWindow;
        }

        ImVec2 padding_regular = g.Style.TouchExtraPadding;
        ImVec2 padding_for_resize = ImMath.ImMax(g.Style.TouchExtraPadding, new ImVec2(g.Style.WindowBorderHoverPadding, g.Style.WindowBorderHoverPadding));
        for (int i = g.Windows.Size - 1; i >= 0; i--)
        {
            ImGuiWindow window = g.Windows[i];
            if (!window.WasActive || window.Hidden)
                continue;
            if ((window.Flags & ImGuiWindowFlags_NoMouseInputs) != 0)
                continue;
            Debug.Assert(window.Viewport != null);
            if (window.Viewport != g.MouseViewport)
                continue;

            // Using the clipped AABB, a child window will typically be clipped by its parent (not always)
            ImVec2 hit_padding = (window.Flags & (ImGuiWindowFlags_NoResize | ImGuiWindowFlags_AlwaysAutoResize)) != 0 ? padding_regular : padding_for_resize;
            if (!RectContainsWithPad(window.OuterRectClipped, pos, hit_padding)) // ImRect::ContainsWithPad()
                continue;

            // Support for one rectangular hole in any given window
            // FIXME: Consider generalizing hit-testing override (with more generic data, callback, etc.) (#1512)
            if (window.HitTestHoleSize.x != 0)
            {
                ImVec2 hole_pos = new ImVec2(window.Pos.x + (float)window.HitTestHoleOffset.x, window.Pos.y + (float)window.HitTestHoleOffset.y);
                ImVec2 hole_size = new ImVec2((float)window.HitTestHoleSize.x, (float)window.HitTestHoleSize.y);
                if (new ImRect(hole_pos, hole_pos + hole_size).Contains(pos))
                    continue;
            }

            if (find_first_and_in_any_viewport)
            {
                hovered_window = window;
                break;
            }
            else
            {
                if (hovered_window == null)
                    hovered_window = window;
                if (hovered_window_under_moving_window == null && (g.MovingWindow == null || window.RootWindowDockTree != g.MovingWindow.RootWindowDockTree))
                    hovered_window_under_moving_window = window;
                if (hovered_window != null && hovered_window_under_moving_window != null)
                    break;
            }
        }

        out_hovered_window = hovered_window;
        out_hovered_window_under_moving_window = hovered_window_under_moving_window;
        if (find_first_and_in_any_viewport == false && g.MovingWindow != null)
            g.MovingWindow.Viewport = backup_moving_window_viewport;
    }

    public static bool IsItemActive()
    {
        ImGuiContext g = GImGui;
        if (g.ActiveId != 0)
            return g.ActiveId == g.LastItemData.ID;
        return false;
    }

    public static bool IsItemActivated()
    {
        ImGuiContext g = GImGui;
        if (g.ActiveId != 0)
            if (g.ActiveId == g.LastItemData.ID && g.ActiveIdPreviousFrame != g.LastItemData.ID)
                return true;
        return false;
    }

    public static bool IsItemDeactivated()
    {
        ImGuiContext g = GImGui;
        if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HasDeactivated) != 0)
            return (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Deactivated) != 0;
        return g.DeactivatedItemData.ID == g.LastItemData.ID && g.LastItemData.ID != 0 && g.DeactivatedItemData.ElapseFrame >= g.FrameCount;
    }

    // Since 1.92.9: consider using NoLiveEdit flags if all you need to that values are not written bo backing variable while typing.
    public static bool IsItemDeactivatedAfterEdit()
    {
        ImGuiContext g = GImGui;
        return IsItemDeactivated() && g.DeactivatedItemData.HasBeenEditedBefore;
    }

    // == (GetItemID() == GetFocusID() && GetFocusID() != 0)
    public static bool IsItemFocused()
    {
        ImGuiContext g = GImGui;
        if (g.NavId != g.LastItemData.ID || g.NavId == 0)
            return false;

        // Special handling for the dummy item after Begin() which represent the title bar or tab.
        // When the window is collapsed (SkipItems==true) that last item will never be overwritten so we need to detect the case.
        ImGuiWindow window = g.CurrentWindow;
        if (g.LastItemData.ID == window.ID && window.WriteAccessed)
            return false;

        return true;
    }

    // Important: this can be useful but it is NOT equivalent to the behavior of e.g.Button()!
    // Most widgets have specific reactions based on mouse-up/down state, mouse position etc.
    public static bool IsItemClicked(ImGuiMouseButton mouse_button)
    {
        return IsMouseClicked(mouse_button) && IsItemHovered(ImGuiHoveredFlags_None);
    }

    public static bool IsItemToggledOpen()
    {
        ImGuiContext g = GImGui;
        return (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_ToggledOpen) != 0;
    }

    // Call after a Selectable() or TreeNode() items inside a BeginMultiSelect()/EndMultiSelect() scope.
    // - Useful if you need the per-item information before reaching EndMultiSelect(), e.g. for rendering purpose.
    // Outside of a multi-select block:
    // - It would be misleading/ambiguous to report this signal, as widgets return e.g. a pressed event,
    //   and user code is in charge of altering selection in ways we cannot predict.
    //   Prefer using 'if (IsItemClicked() && !IsItemToggledOpen())' for a manual reimplementation of selection.
    public static bool IsItemToggledSelection()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.CurrentMultiSelect != null); // Can only be used inside a BeginMultiSelect()/EndMultiSelect()
        return (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_ToggledSelection) != 0;
    }

    // IMPORTANT: If you are trying to check whether your mouse should be dispatched to Dear ImGui or to your underlying app,
    // you should not use this function! Use the 'io.WantCaptureMouse' boolean for that!
    // Refer to FAQ entry "How can I tell whether to dispatch mouse/keyboard to Dear ImGui or my application?" for details.
    public static bool IsAnyItemHovered()
    {
        ImGuiContext g = GImGui;
        return g.HoveredId != 0 || g.HoveredIdPreviousFrame != 0;
    }

    public static bool IsAnyItemActive()
    {
        ImGuiContext g = GImGui;
        return g.ActiveId != 0;
    }

    public static bool IsAnyItemFocused()
    {
        ImGuiContext g = GImGui;
        return g.NavId != 0 && g.NavCursorVisible;
    }

    public static bool IsItemVisible()
    {
        ImGuiContext g = GImGui;
        return (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Visible) != 0;
    }

    public static bool IsItemEdited()
    {
        ImGuiContext g = GImGui;
        return (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Edited) != 0;
    }

    // Allow next item to be overlapped by subsequent items.
    // This works by requiring HoveredId to match for two subsequent frames,
    // so if a following items overwrite it our interactions will naturally be disabled.
    public static void SetNextItemAllowOverlap()
    {
        ImGuiContext g = GImGui;
        g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_AllowOverlap;
    }

    // #ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS: SetItemAllowOverlap() is obsolete, not ported.

    // This is a shortcut for not taking ownership of 100+ keys, frequently used by drag operations.
    // FIXME: It might be undesirable that this will likely disable KeyOwner-aware shortcuts systems. Consider a more fine-tuned version if needed?
    public static void SetActiveIdUsingAllKeyboardKeys()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.ActiveId != 0);
        g.ActiveIdUsingNavDirMask = (ImU32)((1 << ImGuiDir_COUNT) - 1);
        g.ActiveIdUsingAllKeyboardKeys = true;
        NavMoveRequestCancel();
    }

    public static ImGuiID GetItemID()
    {
        ImGuiContext g = GImGui;
        return g.LastItemData.ID;
    }

    public static ImVec2 GetItemRectMin()
    {
        ImGuiContext g = GImGui;
        return g.LastItemData.Rect.Min;
    }

    public static ImVec2 GetItemRectMax()
    {
        ImGuiContext g = GImGui;
        return g.LastItemData.Rect.Max;
    }

    public static ImVec2 GetItemRectSize()
    {
        ImGuiContext g = GImGui;
        return g.LastItemData.Rect.GetSize();
    }

    public static ImGuiItemFlags GetItemFlags()
    {
        ImGuiContext g = GImGui;
        return g.LastItemData.ItemFlags;
    }

    // Prior to v1.90 2023/10/16, the BeginChild() function took a 'bool border = false' parameter instead of 'ImGuiChildFlags child_flags = 0'.
    // ImGuiChildFlags_Borders is defined as always == 1 in order to allow old code passing 'true'. Read comments in imgui.h for details!
    // bool ImGui::BeginChild(const char* str_id, const ImVec2& size_arg, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags)
    public static bool BeginChild(string str_id, ImVec2 size_arg, ImGuiChildFlags child_flags = 0, ImGuiWindowFlags window_flags = 0)
    {
        ImGuiID id = GetCurrentWindow().GetID(str_id);
        return BeginChildEx(str_id, id, size_arg, child_flags, window_flags);
    }

    // bool ImGui::BeginChild(ImGuiID id, const ImVec2& size_arg, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags)
    public static bool BeginChild(ImGuiID id, ImVec2 size_arg, ImGuiChildFlags child_flags = 0, ImGuiWindowFlags window_flags = 0)
    {
        return BeginChildEx(null, id, size_arg, child_flags, window_flags);
    }

    // bool ImGui::BeginChildEx(const char* name, ImGuiID id, const ImVec2& size_arg, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags)
    static unsafe bool BeginChildEx(string name, ImGuiID id, ImVec2 size_arg, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow parent_window = g.CurrentWindow;
        Debug.Assert(id != 0);

        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasChildFlags) != 0)
            child_flags = (child_flags | g.NextWindowData.ChildFlagsSet) & ~g.NextWindowData.ChildFlagsClear;

        // Sanity check as it is likely that some user will accidentally pass ImGuiWindowFlags into the ImGuiChildFlags argument.
        ImGuiChildFlags ImGuiChildFlags_SupportedMask_ = ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding | ImGuiChildFlags_ResizeX | ImGuiChildFlags_ResizeY | ImGuiChildFlags_AutoResizeX | ImGuiChildFlags_AutoResizeY | ImGuiChildFlags_AlwaysAutoResize | ImGuiChildFlags_FrameStyle | ImGuiChildFlags_NavFlattened;
        Debug.Assert((child_flags & ~ImGuiChildFlags_SupportedMask_) == 0, "Illegal ImGuiChildFlags value. Did you pass ImGuiWindowFlags values instead of ImGuiChildFlags?");
        Debug.Assert((window_flags & ImGuiWindowFlags_AlwaysAutoResize) == 0, "Cannot specify ImGuiWindowFlags_AlwaysAutoResize for BeginChild(). Use ImGuiChildFlags_AlwaysAutoResize!");
        if ((child_flags & ImGuiChildFlags_AlwaysAutoResize) != 0)
        {
            Debug.Assert((child_flags & (ImGuiChildFlags_ResizeX | ImGuiChildFlags_ResizeY)) == 0, "Cannot use ImGuiChildFlags_ResizeX or ImGuiChildFlags_ResizeY with ImGuiChildFlags_AlwaysAutoResize!");
            Debug.Assert((child_flags & (ImGuiChildFlags_AutoResizeX | ImGuiChildFlags_AutoResizeY)) != 0, "Must use ImGuiChildFlags_AutoResizeX or ImGuiChildFlags_AutoResizeY with ImGuiChildFlags_AlwaysAutoResize!");
        }
        // #ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS
        //if (window_flags & ImGuiWindowFlags_AlwaysUseWindowPadding) { child_flags |= ImGuiChildFlags_AlwaysUseWindowPadding; }
        //if (window_flags & ImGuiWindowFlags_NavFlattened) { child_flags |= ImGuiChildFlags_NavFlattened; }
        // #endif
        if ((child_flags & ImGuiChildFlags_AutoResizeX) != 0)
            child_flags &= ~ImGuiChildFlags_ResizeX;
        if ((child_flags & ImGuiChildFlags_AutoResizeY) != 0)
            child_flags &= ~ImGuiChildFlags_ResizeY;

        // Set window flags
        window_flags |= ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoDocking;
        window_flags |= (parent_window.Flags & ImGuiWindowFlags_NoMove); // Inherit the NoMove flag
        if ((child_flags & (ImGuiChildFlags_AutoResizeX | ImGuiChildFlags_AutoResizeY | ImGuiChildFlags_AlwaysAutoResize)) != 0)
            window_flags |= ImGuiWindowFlags_AlwaysAutoResize; // FIXME: Would be sane to not make single-axis flag set this. (#9355)
        if ((child_flags & (ImGuiChildFlags_ResizeX | ImGuiChildFlags_ResizeY)) == 0)
            window_flags |= ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoSavedSettings;

        // Special framed style
        if ((child_flags & ImGuiChildFlags_FrameStyle) != 0)
        {
            PushStyleColor(ImGuiCol_ChildBg, g.Style.Colors[ImGuiCol_FrameBg]);
            PushStyleVar(ImGuiStyleVar_ChildRounding, g.Style.FrameRounding);
            PushStyleVar(ImGuiStyleVar_ChildBorderSize, g.Style.FrameBorderSize);
            PushStyleVar(ImGuiStyleVar_WindowPadding, g.Style.FramePadding);
            child_flags |= ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding;
            window_flags |= ImGuiWindowFlags_NoMove;
        }

        // Forward size
        // Important: Begin() has special processing to switch condition to ImGuiCond_FirstUseEver for a given axis when ImGuiChildFlags_ResizeXXX is set.
        // (the alternative would to store conditional flags per axis, which is possible but more code)
        ImVec2 size_avail = GetContentRegionAvail();
        ImVec2 size_default = new ImVec2((child_flags & ImGuiChildFlags_AutoResizeX) != 0 ? 0.0f : size_avail.x, (child_flags & ImGuiChildFlags_AutoResizeY) != 0 ? 0.0f : size_avail.y);
        ImVec2 size = CalcItemSize(size_arg, size_default.x, size_default.y);

        // A SetNextWindowSize() call always has priority (#8020)
        // (since the code in Begin() never supported SizeVal==0.0f aka auto-resize via SetNextWindowSize() call, we don't support it here for now)
        // FIXME: We only support ImGuiCond_Always in this path. Supporting other paths would requires to obtain window pointer.
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSize) != 0 && (g.NextWindowData.SizeCond & ImGuiCond_Always) != 0)
        {
            if (g.NextWindowData.SizeVal.x > 0.0f)
            {
                size.x = g.NextWindowData.SizeVal.x;
                child_flags &= ~ImGuiChildFlags_ResizeX;
            }
            if (g.NextWindowData.SizeVal.y > 0.0f)
            {
                size.y = g.NextWindowData.SizeVal.y;
                child_flags &= ~ImGuiChildFlags_ResizeY;
            }
        }
        SetNextWindowSize(size, 0);
        SetNextWindowChildFlags(child_flags, true);

        // Build up name. If you need to append to a same child from multiple location in the ID stack, use BeginChild(ImGuiID id) with a stable value.
        // FIXME: 2023/11/14: commented out shorted version. We had an issue with multiple ### in child window path names, which the trailing hash helped workaround.
        // e.g. "ParentName###ParentIdentifier/ChildName###ChildIdentifier" would get hashed incorrectly by ImHashStr(), trailing _%08X somehow fixes it.
        //string temp_window_name;
        string temp_window_name;
        /*if (name && parent_window->IDStack.back() == parent_window->ID)
            ImFormatStringToTempBuffer(&temp_window_name, NULL, "%s/%s", parent_window->Name, name); // May omit ID if in root of ID stack
        else*/
        if (name != null)
            temp_window_name = ImFmt.Sprintf("%s/%s_%08X", parent_window.Name, name, id); // ImFormatStringToTempBuffer()
        else
            temp_window_name = ImFmt.Sprintf("%s/%08X", parent_window.Name, id);

        // Set style
        float backup_border_size = g.Style.ChildBorderSize;
        if ((child_flags & ImGuiChildFlags_Borders) == 0)
            g.Style.ChildBorderSize = 0.0f;

        // Begin into window
        bool ret = Begin(temp_window_name, flags: window_flags);

        // Restore style
        g.Style.ChildBorderSize = backup_border_size;
        if ((child_flags & ImGuiChildFlags_FrameStyle) != 0)
        {
            PopStyleVar(3);
            PopStyleColor();
        }

        ImGuiWindow child_window = g.CurrentWindow;
        child_window.ChildId = id;

        // Set the cursor to handle case where the user called SetNextWindowPos()+BeginChild() manually.
        // While this is not really documented/defined, it seems that the expected thing to do.
        if (child_window.BeginCount == 1)
            parent_window.DC.CursorPos = child_window.Pos;

        // Process navigation-in immediately so NavInit can run on first frame
        // Can enter a child if (A) it has navigable items or (B) it can be scrolled.
        ImGuiID temp_id_for_activation = ImGuiHash.ImHashStr("##Child", 0, id);
        if (g.ActiveId == temp_id_for_activation)
            ClearActiveID();
        if (g.NavActivateId == id && (child_flags & ImGuiChildFlags_NavFlattened) == 0 && (child_window.DC.NavLayersActiveMask != 0 || child_window.DC.NavWindowHasScrollY))
        {
            FocusWindow(child_window);
            NavInitWindow(child_window, false);
            SetActiveID(temp_id_for_activation, child_window); // Steal ActiveId with another arbitrary id so that key-press won't activate child item
            g.ActiveIdSource = g.NavInputSource;
        }
        return ret;
    }

    // void ImGui::EndChild()
    public static void EndChild()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow child_window = g.CurrentWindow;

        ImGuiID backup_within_end_child_id = g.WithinEndChildID;
        Debug.Assert((child_window.Flags & ImGuiWindowFlags_ChildWindow) != 0);   // Mismatched BeginChild()/EndChild() calls

        g.WithinEndChildID = child_window.ID;
        ImVec2 child_size = child_window.Size;
        End();
        if (child_window.BeginCount == 1)
        {
            ImGuiWindow parent_window = g.CurrentWindow;
            ImRect bb = new ImRect(parent_window.DC.CursorPos, parent_window.DC.CursorPos + child_size);
            ItemSize(child_size);
            bool nav_flattened = (child_window.ChildFlags & ImGuiChildFlags_NavFlattened) != 0;
            if ((child_window.DC.NavLayersActiveMask != 0 || child_window.DC.NavWindowHasScrollY) && !nav_flattened)
            {
                ItemAdd(bb, child_window.ChildId);
                RenderNavCursor(bb, child_window.ChildId);

                // When browsing a window that has no activable items (scroll only) we keep a highlight on the child (pass g.NavId to trick into always displaying)
                if (child_window.DC.NavLayersActiveMask == 0 && child_window == g.NavWindow)
                    RenderNavCursor(new ImRect(bb.Min - new ImVec2(2, 2), bb.Max + new ImVec2(2, 2)), g.NavId, ImGuiNavRenderCursorFlags_Compact);
            }
            else
            {
                // Not navigable into
                // - This is a bit of a fringe use case, mostly useful for undecorated, non-scrolling contents childs, or empty childs.
                // - We could later decide to not apply this path if ImGuiChildFlags_FrameStyle or ImGuiChildFlags_Borders is set.
                ItemAdd(bb, child_window.ChildId, null, ImGuiItemFlags_NoNav);

                // But when flattened we directly reach items, adjust active layer mask accordingly
                if (nav_flattened)
                    parent_window.DC.NavLayersActiveMaskNext |= child_window.DC.NavLayersActiveMaskNext;
            }
            if (g.HoveredWindow == child_window)
                g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredWindow;
            child_window.DC.ChildItemStatusFlags = g.LastItemData.StatusFlags;
            //SetLastItemDataForChildWindowItem(child_window, child_window->Rect()); // Not needed, effectively done by ItemAdd()
        }
        else
        {
            SetLastItemDataForChildWindowItem(child_window, child_window.Rect());
        }

        g.WithinEndChildID = backup_within_end_child_id;
        g.LogLinePosY = -float.MaxValue; // To enforce a carriage return
    }

    // ImGuiWindow* ImGui::FindFrontMostVisibleChildWindow(ImGuiWindow* window)
    public static ImGuiWindow FindFrontMostVisibleChildWindow(ImGuiWindow window)
    {
        for (int n = window.DC.ChildWindows.Size - 1; n >= 0; n--)
            if (IsWindowActiveAndVisible(window.DC.ChildWindows[n]))
                return FindFrontMostVisibleChildWindow(window.DC.ChildWindows[n]);
        return window;
    }

    // static void SetWindowConditionAllowFlags(ImGuiWindow* window, ImGuiCond flags, bool enabled) (imgui.cpp)
    static void SetWindowConditionAllowFlags(ImGuiWindow window, ImGuiCond flags, bool enabled)
    {
        window.SetWindowPosAllowFlags       = enabled ? (byte)(window.SetWindowPosAllowFlags       | flags) : (byte)(window.SetWindowPosAllowFlags       & ~flags);
        window.SetWindowSizeAllowFlags      = enabled ? (byte)(window.SetWindowSizeAllowFlags      | flags) : (byte)(window.SetWindowSizeAllowFlags      & ~flags);
        window.SetWindowCollapsedAllowFlags = enabled ? (byte)(window.SetWindowCollapsedAllowFlags | flags) : (byte)(window.SetWindowCollapsedAllowFlags & ~flags);
        window.SetWindowDockAllowFlags      = enabled ? (byte)(window.SetWindowDockAllowFlags      | flags) : (byte)(window.SetWindowDockAllowFlags      & ~flags);
    }

    // [SKIPPED: ImGui::FindWindowByID() / ImGui::FindWindowByName() already ported in ImGuiSettings.cs]

    // static void ApplyWindowSettings(ImGuiWindow* window, ImGuiWindowSettings* settings) (imgui.cpp)
    // (previous throwing placeholder in ImGuiSettings.cs replaced by this faithful port)
    static void ApplyWindowSettings(ImGuiWindow window, ImGuiWindowSettings settings)
    {
        if (settings != null)
        {
            ImGuiViewport main_viewport = GetMainViewport();
            window.ViewportPos = main_viewport.Pos;
            if (settings.ViewportId != 0)
            {
                window.ViewportId = settings.ViewportId;
                window.ViewportPos = new ImVec2(settings.ViewportPos.x, settings.ViewportPos.y);
            }
            window.Pos = ImMath.ImTrunc(new ImVec2(settings.Pos.x + window.ViewportPos.x, settings.Pos.y + window.ViewportPos.y));
            if (settings.Size.x > 0 && settings.Size.y > 0)
            {
                window.Size = window.SizeFull = ImMath.ImTrunc(new ImVec2(settings.Size.x, settings.Size.y));
                window.AutoFitFramesX = window.AutoFitFramesY = 0;
            }
            window.Collapsed = settings.Collapsed;
            window.DockId = settings.DockId;
            window.DockOrder = settings.DockOrder;
            SetWindowConditionAllowFlags(window, ImGuiCond_FirstUseEver, false);
        }

        window.DC.CursorStartPos = window.DC.CursorMaxPos = window.DC.IdealMaxPos = window.Pos; // So first call to CalcWindowContentSizes() doesn't return crazy values
        if ((window.Flags & ImGuiWindowFlags_AlwaysAutoResize) != 0)
        {
            window.AutoFitFramesX = window.AutoFitFramesY = 2;
            window.AutoFitOnlyGrows = false;
        }
        else
        {
            window.AutoFitFramesX = (sbyte)((window.Size.x <= 0.0f) ? 2 : 0);
            window.AutoFitFramesY = (sbyte)((window.Size.y <= 0.0f) ? 2 : 0);
            window.AutoFitOnlyGrows = (window.AutoFitFramesX > 0) || (window.AutoFitFramesY > 0);
        }
    }

    // Note that 'settings' may be NULL.
    // Sets _Once, _Appearing, _FirstUseEver. _FirstUseEver will be cleared again if there are settings.
    // static void InitOrLoadWindowSettings(ImGuiWindow* window, ImGuiWindowSettings* settings) (imgui.cpp)
    static void InitOrLoadWindowSettings(ImGuiWindow window, ImGuiWindowSettings settings)
    {
        // Initial window state with e.g. default/arbitrary window position
        // Use SetNextWindowPos() with the appropriate condition flag to change the initial position of a window.
        ImGuiContext g = GImGui;
        ImGuiViewport main_viewport = GetMainViewport();
        window.Pos = main_viewport.Pos + new ImVec2(60, 60);
        window.Size = window.SizeFull = new ImVec2(0, 0);
        window.ViewportPos = main_viewport.Pos;
        window.SetWindowPosAllowFlags = window.SetWindowSizeAllowFlags = window.SetWindowCollapsedAllowFlags = window.SetWindowDockAllowFlags = (byte)(ImGuiCond_Always | ImGuiCond_Once | ImGuiCond_FirstUseEver | ImGuiCond_Appearing);
        ApplyWindowSettings(window, settings);
        if (settings != null)
            settings.LastUsedDate = g.SessionDate;
    }

    // static ImGuiWindow* CreateNewWindow(const char* name, ImGuiWindowFlags flags) (imgui.cpp)
    static ImGuiWindow CreateNewWindow(string name, ImGuiWindowFlags flags)
    {
        // Create window the first time
        //IMGUI_DEBUG_LOG("CreateNewWindow '%s', flags = 0x%08X\n", name, flags);
        ImGuiContext g = GImGui;
        ImGuiWindow window = new ImGuiWindow(g, name);
        window.Flags = flags;
        g.WindowsById.SetVoidPtr(window.ID, window);

        ImGuiWindowSettings settings = null;
        if ((flags & ImGuiWindowFlags_NoSavedSettings) == 0)
            if ((settings = FindWindowSettingsByWindow(window)) != null)
                window.SettingsOffset = g.SettingsWindows.offset_from_ptr(settings);

        InitOrLoadWindowSettings(window, settings);

        if ((flags & ImGuiWindowFlags_NoBringToFrontOnFocus) != 0)
            g.Windows.push_front(window); // Quite slow but rare and only once
        else
            g.Windows.push_back(window);

        return window;
    }

    // static inline ImVec2 CalcWindowMinSize(ImGuiWindow* window) (imgui.cpp)
    static ImVec2 CalcWindowMinSize(ImGuiWindow window)
    {
        // We give windows non-zero minimum size to facilitate understanding problematic cases (e.g. empty popups)
        // FIXME: Essentially we want to restrict manual resizing to WindowMinSize+Decoration, and allow api resizing to be smaller.
        // Perhaps should tend further a neater test for this.
        ImGuiContext g = GImGui;
        ImVec2 size_min = new ImVec2(0.0f, 0.0f);
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0 && (window.Flags & ImGuiWindowFlags_Popup) == 0)
        {
            size_min.x = (window.ChildFlags & ImGuiChildFlags_ResizeX) != 0 ? g.Style.WindowMinSize.x : IMGUI_WINDOW_HARD_MIN_SIZE;
            size_min.y = (window.ChildFlags & ImGuiChildFlags_ResizeY) != 0 ? g.Style.WindowMinSize.y : IMGUI_WINDOW_HARD_MIN_SIZE;
        }
        else
        {
            size_min.x = ((window.Flags & ImGuiWindowFlags_AlwaysAutoResize) == 0) ? g.Style.WindowMinSize.x : IMGUI_WINDOW_HARD_MIN_SIZE;
            size_min.y = ((window.Flags & ImGuiWindowFlags_AlwaysAutoResize) == 0) ? g.Style.WindowMinSize.y : IMGUI_WINDOW_HARD_MIN_SIZE;
        }

        // Reduce artifacts with very small windows
        ImGuiWindow window_for_height = GetWindowForTitleAndMenuHeight(window);
        size_min.y = ImMath.ImMax(size_min.y, window_for_height.TitleBarHeight + window_for_height.MenuBarHeight + ImMath.ImMax(0.0f, g.Style.WindowRounding - 1.0f));
        return size_min;
    }

    // imgui_internal.h: #define IMGUI_WINDOW_HARD_MIN_SIZE 4.0f
    const float IMGUI_WINDOW_HARD_MIN_SIZE = 4.0f;

    // static ImVec2 CalcWindowSizeAfterConstraint(ImGuiWindow* window, const ImVec2& size_desired) (imgui.cpp)
    static ImVec2 CalcWindowSizeAfterConstraint(ImGuiWindow window, ImVec2 size_desired)
    {
        ImGuiContext g = GImGui;
        ImVec2 new_size = size_desired;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSizeConstraint) != 0)
        {
            // See comments in SetNextWindowSizeConstraints() for details about setting size_min an size_max.
            ImRect cr = g.NextWindowData.SizeConstraintRect;
            new_size.x = (cr.Min.x >= 0 && cr.Max.x >= 0) ? ImMath.ImClamp(new_size.x, cr.Min.x, cr.Max.x) : window.SizeFull.x;
            new_size.y = (cr.Min.y >= 0 && cr.Max.y >= 0) ? ImMath.ImClamp(new_size.y, cr.Min.y, cr.Max.y) : window.SizeFull.y;
            if (g.NextWindowData.SizeCallback != null)
            {
                ImGuiSizeCallbackData data = new ImGuiSizeCallbackData();
                data.UserData = g.NextWindowData.SizeCallbackUserData;
                data.Pos = window.Pos;
                data.CurrentSize = window.SizeFull;
                data.DesiredSize = new_size;
                g.NextWindowData.SizeCallback(data);
                new_size = data.DesiredSize;
            }
            new_size.x = ImMath.ImTrunc(new_size.x); // IM_TRUNC()
            new_size.y = ImMath.ImTrunc(new_size.y);
        }

        // Minimum size
        ImVec2 size_min = CalcWindowMinSize(window);
        return ImMath.ImMax(new_size, size_min);
    }

    // static void CalcWindowContentSizes(ImGuiWindow* window, ImVec2* content_size_current, ImVec2* content_size_ideal) (imgui.cpp)
    static void CalcWindowContentSizes(ImGuiWindow window, out ImVec2 content_size_current, out ImVec2 content_size_ideal)
    {
        bool preserve_old_content_sizes = false;
        if (window.Collapsed && window.AutoFitFramesX <= 0 && window.AutoFitFramesY <= 0)
            preserve_old_content_sizes = true;
        else if (window.Hidden && window.HiddenFramesCannotSkipItems == 0 && window.HiddenFramesCanSkipItems > 0)
            preserve_old_content_sizes = true;
        if (preserve_old_content_sizes)
        {
            content_size_current = window.ContentSize;
            content_size_ideal = window.ContentSizeIdeal;
            return;
        }

        content_size_current = default;
        content_size_ideal = default;
        content_size_current.x = (window.ContentSizeExplicit.x != 0.0f) ? window.ContentSizeExplicit.x : ImMath.ImTrunc64(window.DC.CursorMaxPos.x - window.DC.CursorStartPos.x);
        content_size_current.y = (window.ContentSizeExplicit.y != 0.0f) ? window.ContentSizeExplicit.y : ImMath.ImTrunc64(window.DC.CursorMaxPos.y - window.DC.CursorStartPos.y);
        content_size_ideal.x = (window.ContentSizeExplicit.x != 0.0f) ? window.ContentSizeExplicit.x : ImMath.ImTrunc64(ImMath.ImMax(window.DC.CursorMaxPos.x, window.DC.IdealMaxPos.x) - window.DC.CursorStartPos.x);
        content_size_ideal.y = (window.ContentSizeExplicit.y != 0.0f) ? window.ContentSizeExplicit.y : ImMath.ImTrunc64(ImMath.ImMax(window.DC.CursorMaxPos.y, window.DC.IdealMaxPos.y) - window.DC.CursorStartPos.y);
    }

    // static ImVec2 CalcWindowAutoFitSize(ImGuiWindow* window, const ImVec2& size_contents, int axis_mask) (imgui.cpp)
    static ImVec2 CalcWindowAutoFitSize(ImGuiWindow window, ImVec2 size_contents, int axis_mask)
    {
        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        float decoration_w_without_scrollbars = window.DecoOuterSizeX1 + window.DecoOuterSizeX2 - window.ScrollbarSizes.x;
        float decoration_h_without_scrollbars = window.DecoOuterSizeY1 + window.DecoOuterSizeY2 - window.ScrollbarSizes.y;
        ImVec2 size_pad = window.WindowPadding * 2.0f;
        ImVec2 size_desired = new ImVec2(0.0f, 0.0f);
        size_desired.x = (axis_mask & 1) != 0 ? size_contents.x + size_pad.x + decoration_w_without_scrollbars : window.Size.x;
        size_desired.y = (axis_mask & 2) != 0 ? size_contents.y + size_pad.y + decoration_h_without_scrollbars : window.Size.y;

        // Determine maximum window size
        // Child windows are laid within their parent (unless they are also popups/menus) and thus have no restriction
        ImVec2 size_max = ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0 && (window.Flags & ImGuiWindowFlags_Popup) == 0) ? new ImVec2(float.MaxValue, float.MaxValue) : GetMainViewport().WorkSize - style.DisplaySafeAreaPadding * 2.0f;

        if ((window.Flags & ImGuiWindowFlags_Tooltip) != 0)
        {
            // Tooltip always resize (up to maximum size)
            return ImMath.ImMin(size_desired, size_max);
        }
        else
        {
            ImVec2 size_min = CalcWindowMinSize(window);
            ImVec2 size_auto_fit = ImMath.ImClamp(size_desired, ImMath.ImMin(size_min, size_max), size_max);

            // When the window cannot fit all contents (either because of constraints, either because screen is too small),
            // we are growing the size on the other axis to compensate for expected scrollbar. FIXME: Might turn bigger than ViewportSize-WindowPadding.
            ImVec2 size_auto_fit_after_constraint = CalcWindowSizeAfterConstraint(window, size_auto_fit);
            float size_contents_for_scrollbar_x = (axis_mask & 1) != 0 ? size_contents.x : window.ContentSize.x; // See #9352. In theory this should use same logic as `window->ScrollbarY = ...` codepath in Begin(). Needs some plumbling.
            float size_contents_for_scrollbar_y = (axis_mask & 2) != 0 ? size_contents.y : window.ContentSize.y;
            bool will_have_scrollbar_x = (size_auto_fit_after_constraint.x < size_contents_for_scrollbar_x + size_pad.x + decoration_w_without_scrollbars && (window.Flags & ImGuiWindowFlags_NoScrollbar) == 0 && (window.Flags & ImGuiWindowFlags_HorizontalScrollbar) != 0) || (window.Flags & ImGuiWindowFlags_AlwaysHorizontalScrollbar) != 0;
            bool will_have_scrollbar_y = (size_auto_fit_after_constraint.y < size_contents_for_scrollbar_y + size_pad.y + decoration_h_without_scrollbars && (window.Flags & ImGuiWindowFlags_NoScrollbar) == 0) || (window.Flags & ImGuiWindowFlags_AlwaysVerticalScrollbar) != 0;
            if (will_have_scrollbar_x)
                size_auto_fit.y += style.ScrollbarSize;
            if (will_have_scrollbar_y)
                size_auto_fit.x += style.ScrollbarSize;
            return size_auto_fit;
        }
    }

    // ImVec2 ImGui::CalcWindowNextAutoFitSize(ImGuiWindow* window)
    public static ImVec2 CalcWindowNextAutoFitSize(ImGuiWindow window)
    {
        CalcWindowContentSizes(window, out ImVec2 size_contents_current, out ImVec2 size_contents_ideal);
        ImVec2 size_auto_fit = CalcWindowAutoFitSize(window, size_contents_ideal, ~0);
        ImVec2 size_final = CalcWindowSizeAfterConstraint(window, size_auto_fit);
        return size_final;
    }

    // static ImGuiCol GetWindowBgColorIdx(ImGuiWindow* window) (imgui.cpp)
    static ImGuiCol GetWindowBgColorIdx(ImGuiWindow window)
    {
        if ((window.Flags & (ImGuiWindowFlags_Tooltip | ImGuiWindowFlags_Popup)) != 0)
            return ImGuiCol_PopupBg;
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0 && !window.DockIsActive)
            return ImGuiCol_ChildBg;
        return ImGuiCol_WindowBg;
    }

    // static void CalcResizePosSizeFromAnyCorner(ImGuiWindow* window, const ImVec2& corner_target_arg, const ImVec2& corner_norm, ImVec2* out_pos, ImVec2* out_size) (imgui.cpp)
    static void CalcResizePosSizeFromAnyCorner(ImGuiWindow window, ImVec2 corner_target_arg, ImVec2 corner_norm, out ImVec2 out_pos, out ImVec2 out_size)
    {
        ImVec2 corner_target = corner_target_arg;
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0) // Clamp resizing of childs within parent
        {
            ImGuiWindow parent_window = window.ParentWindow;
            ImGuiWindowFlags parent_flags = parent_window.Flags;
            ImRect limit_rect = parent_window.InnerRect;
            limit_rect.Expand(new ImVec2(-ImMath.ImMax(parent_window.WindowPadding.x, parent_window.WindowBorderSize), -ImMath.ImMax(parent_window.WindowPadding.y, parent_window.WindowBorderSize)));
            if ((parent_flags & (ImGuiWindowFlags_HorizontalScrollbar | ImGuiWindowFlags_AlwaysHorizontalScrollbar)) == 0 || (parent_flags & ImGuiWindowFlags_NoScrollbar) != 0)
                corner_target.x = ImMath.ImClamp(corner_target.x, limit_rect.Min.x, limit_rect.Max.x);
            if ((parent_flags & ImGuiWindowFlags_NoScrollbar) != 0)
                corner_target.y = ImMath.ImClamp(corner_target.y, limit_rect.Min.y, limit_rect.Max.y);
        }
        ImVec2 pos_min = ImMath.ImLerp(corner_target, window.Pos, corner_norm);                // Expected window upper-left
        ImVec2 pos_max = ImMath.ImLerp(window.Pos + window.Size, corner_target, corner_norm); // Expected window lower-right
        ImVec2 size_expected = pos_max - pos_min;
        ImVec2 size_constrained = CalcWindowSizeAfterConstraint(window, size_expected);
        out_pos = pos_min;
        if (corner_norm.x == 0.0f)
            out_pos.x -= (size_constrained.x - size_expected.x);
        if (corner_norm.y == 0.0f)
            out_pos.y -= (size_constrained.y - size_expected.y);
        out_size = size_constrained;
    }

    // Data for resizing from resize grip / corner
    // struct ImGuiResizeGripDef (imgui.cpp)
    private struct ImGuiResizeGripDef
    {
        public ImVec2 CornerPosN;
        public ImVec2 InnerDir;
        public int AngleMin12, AngleMax12;
    }
    static readonly ImGuiResizeGripDef[] resize_grip_def =
    {
        new ImGuiResizeGripDef { CornerPosN = new ImVec2(1, 1), InnerDir = new ImVec2(-1, -1), AngleMin12 = 0, AngleMax12 = 3 },   // Lower-right
        new ImGuiResizeGripDef { CornerPosN = new ImVec2(0, 1), InnerDir = new ImVec2(+1, -1), AngleMin12 = 3, AngleMax12 = 6 },   // Lower-left
        new ImGuiResizeGripDef { CornerPosN = new ImVec2(0, 0), InnerDir = new ImVec2(+1, +1), AngleMin12 = 6, AngleMax12 = 9 },   // Upper-left (Unused)
        new ImGuiResizeGripDef { CornerPosN = new ImVec2(1, 0), InnerDir = new ImVec2(-1, +1), AngleMin12 = 9, AngleMax12 = 12 },  // Upper-right (Unused)
    };

    // Data for resizing from borders
    // struct ImGuiResizeBorderDef (imgui.cpp)
    private struct ImGuiResizeBorderDef
    {
        public ImVec2 InnerDir;               // Normal toward inside
        public ImVec2 SegmentN1, SegmentN2;   // End positions, normalized (0,0: upper left)
        public float OuterAngle;              // Angle toward outside
    }
    static readonly ImGuiResizeBorderDef[] resize_border_def =
    {
        new ImGuiResizeBorderDef { InnerDir = new ImVec2(+1, 0), SegmentN1 = new ImVec2(0, 1), SegmentN2 = new ImVec2(0, 0), OuterAngle = ImMath.IM_PI * 1.00f }, // Left
        new ImGuiResizeBorderDef { InnerDir = new ImVec2(-1, 0), SegmentN1 = new ImVec2(1, 0), SegmentN2 = new ImVec2(1, 1), OuterAngle = ImMath.IM_PI * 0.00f }, // Right
        new ImGuiResizeBorderDef { InnerDir = new ImVec2(0, +1), SegmentN1 = new ImVec2(0, 0), SegmentN2 = new ImVec2(1, 0), OuterAngle = ImMath.IM_PI * 1.50f }, // Up
        new ImGuiResizeBorderDef { InnerDir = new ImVec2(0, -1), SegmentN1 = new ImVec2(1, 1), SegmentN2 = new ImVec2(0, 1), OuterAngle = ImMath.IM_PI * 0.50f }, // Down
    };

    // static ImRect GetResizeBorderRect(ImGuiWindow* window, int border_n, float perp_padding, float thickness) (imgui.cpp)
    static ImRect GetResizeBorderRect(ImGuiWindow window, int border_n, float perp_padding, float thickness)
    {
        ImRect rect = window.Rect();
        if (thickness == 0.0f)
        {
            rect.Max -= new ImVec2(1, 1);
        }
        if (border_n == ImGuiDir_Left)  { return new ImRect(rect.Min.x - thickness,    rect.Min.y + perp_padding, rect.Min.x + thickness,    rect.Max.y - perp_padding); }
        if (border_n == ImGuiDir_Right) { return new ImRect(rect.Max.x - thickness,    rect.Min.y + perp_padding, rect.Max.x + thickness,    rect.Max.y - perp_padding); }
        if (border_n == ImGuiDir_Up)    { return new ImRect(rect.Min.x + perp_padding, rect.Min.y - thickness,    rect.Max.x - perp_padding, rect.Min.y + thickness);    }
        if (border_n == ImGuiDir_Down)  { return new ImRect(rect.Min.x + perp_padding, rect.Max.y - thickness,    rect.Max.x - perp_padding, rect.Max.y + thickness);    }
        Debug.Assert(false);
        return new ImRect();
    }

    // 0..3: corners (Lower-right, Lower-left, Unused, Unused)
    // ImGuiID ImGui::GetWindowResizeCornerID(ImGuiWindow* window, int n)
    public static ImGuiID GetWindowResizeCornerID(ImGuiWindow window, int n)
    {
        Debug.Assert(n >= 0 && n < 4);
        ImGuiID id = window.DockIsActive ? window.DockNode.HostWindow.ID : window.ID;
        id = ImGuiHash.ImHashStr("#RESIZE", 0, id);
        byte[] n_bytes = BitConverter.GetBytes(n);
        id = ImGuiHash.ImHashData(n_bytes, 0, sizeof(int), id); // ImHashData(&n, sizeof(int), id)
        return id;
    }

    // Borders (Left, Right, Up, Down)
    // ImGuiID ImGui::GetWindowResizeBorderID(ImGuiWindow* window, ImGuiDir dir)
    public static ImGuiID GetWindowResizeBorderID(ImGuiWindow window, ImGuiDir dir)
    {
        Debug.Assert(dir >= 0 && dir < 4);
        int n = (int)dir + 4;
        ImGuiID id = window.DockIsActive ? window.DockNode.HostWindow.ID : window.ID;
        id = ImGuiHash.ImHashStr("#RESIZE", 0, id);
        byte[] n_bytes = BitConverter.GetBytes(n);
        id = ImGuiHash.ImHashData(n_bytes, 0, sizeof(int), id);
        return id;
    }

    // Handle resize for: Resize Grips, Borders, Gamepad
    // Return true when using auto-fit (double-click on resize grip)
    // static int ImGui::UpdateWindowManualResize(ImGuiWindow* window, int* border_hovered, int* border_held, int resize_grip_count, ImU32 resize_grip_col[4], const ImRect& visibility_rect)
    static int UpdateWindowManualResize(ImGuiWindow window, out int border_hovered, out int border_held, int resize_grip_count, Span<ImU32> resize_grip_col, ImRect visibility_rect)
    {
        ImGuiContext g = GImGui;
        ImGuiWindowFlags flags = window.Flags;

        // (C++ Begin() initializes *border_hovered/*border_held to -1 before the call; only overwritten when hovered/held.)
        border_hovered = -1;
        border_held = -1;
        if ((flags & ImGuiWindowFlags_NoResize) != 0 || window.AutoFitFramesX > 0 || window.AutoFitFramesY > 0)
            return 0;
        if ((flags & ImGuiWindowFlags_AlwaysAutoResize) != 0 && (window.ChildFlags & (ImGuiChildFlags_ResizeX | ImGuiChildFlags_ResizeY)) == 0)
            return 0;
        if (window.WasActive == false) // Early out to avoid running this code for e.g. a hidden implicit/fallback Debug window.
            return 0;

        int ret_auto_fit_mask = 0x00;
        float grip_draw_size = ImMath.ImTrunc(ImMath.ImMax(g.FontSize * 1.35f, window.WindowRounding + 1.0f + g.FontSize * 0.2f)); // IM_TRUNC()
        float grip_hover_inner_size = (resize_grip_count > 0) ? ImMath.ImTrunc(grip_draw_size * 0.75f) : 0.0f;
        float grip_hover_outer_size = g.WindowsBorderHoverPadding;

        ImRect clamp_rect = visibility_rect;
        bool window_move_from_title_bar = (window.BgClickFlags & ImGuiWindowBgClickFlags_Move) == 0 && (window.Flags & ImGuiWindowFlags_NoTitleBar) == 0;
        if (window_move_from_title_bar)
            clamp_rect.Min.y -= window.TitleBarHeight;

        ImVec2 pos_target = new ImVec2(float.MaxValue, float.MaxValue);
        ImVec2 size_target = new ImVec2(float.MaxValue, float.MaxValue);

        // Clip mouse interaction rectangles within the viewport rectangle (in practice the narrowing is going to happen most of the time).
        // - Not narrowing would mostly benefit the situation where OS windows _without_ decoration have a threshold for hovering when outside their limits.
        //   This is however not the case with current backends under Win32, but a custom borderless window implementation would benefit from it.
        // - When decoration are enabled we typically benefit from that distance, but then our resize elements would be conflicting with OS resize elements, so we also narrow.
        // - Note that we are unable to tell if the platform setup allows hovering with a distance threshold (on Win32, decorated window have such threshold).
        // We only clip interaction so we overwrite window->ClipRect, cannot call PushClipRect() yet as DrawList is not yet setup.
        bool clip_with_viewport_rect = (g.IO.BackendFlags & ImGuiBackendFlags_HasMouseHoveredViewport) == 0 || (g.IO.MouseHoveredViewport != window.ViewportId) || (window.Viewport.Flags & ImGuiViewportFlags_NoDecoration) == 0;
        if (clip_with_viewport_rect)
            window.ClipRect = window.Viewport.GetMainRect();

        // Resize grips and borders are on layer 1
        window.DC.NavLayerCurrent = ImGuiNavLayer_Menu;

        // Manual resize grips
        PushID("#RESIZE");
        for (int resize_grip_n = 0; resize_grip_n < resize_grip_count; resize_grip_n++)
        {
            ImGuiResizeGripDef def = resize_grip_def[resize_grip_n];
            ImVec2 corner = ImMath.ImLerp(window.Pos, window.Pos + window.Size, def.CornerPosN);

            // Using the FlattenChilds button flag we make the resize button accessible even if we are hovering over a child window
            bool hovered, held;
            ImRect resize_rect = new ImRect(corner - def.InnerDir * grip_hover_outer_size, corner + def.InnerDir * grip_hover_inner_size);
            if (resize_rect.Min.x > resize_rect.Max.x) ImMath.ImSwap(ref resize_rect.Min.x, ref resize_rect.Max.x);
            if (resize_rect.Min.y > resize_rect.Max.y) ImMath.ImSwap(ref resize_rect.Min.y, ref resize_rect.Max.y);
            ImGuiID resize_grip_id = window.GetID(resize_grip_n); // == GetWindowResizeCornerID()
            ItemAdd(resize_rect, resize_grip_id, null, ImGuiItemFlags_NoNav);
            ButtonBehavior(resize_rect, resize_grip_id, out hovered, out held, ImGuiButtonFlags_FlattenChildren | ImGuiButtonFlags_NoNavFocus);
            //GetForegroundDrawList(window).AddRect(resize_rect.Min, resize_rect.Max, IM_COL32(255, 255, 0, 255));
            if (hovered || held)
                SetMouseCursor((resize_grip_n & 1) != 0 ? ImGuiMouseCursor_ResizeNESW : ImGuiMouseCursor_ResizeNWSE);

            if (held && g.IO.MouseDoubleClicked[0])
            {
                // Auto-fit when double-clicking
                ImVec2 size_auto_fit = CalcWindowAutoFitSize(window, window.ContentSizeIdeal, ~0);
                size_target = CalcWindowSizeAfterConstraint(window, size_auto_fit);
                ret_auto_fit_mask = 0x03; // Both axes
                ClearActiveID();
            }
            else if (held)
            {
                // Resize from any of the four corners
                // We don't use an incremental MouseDelta but rather compute an absolute target size based on mouse position
                ImVec2 clamp_min = new ImVec2(def.CornerPosN.x == 1.0f ? clamp_rect.Min.x : -float.MaxValue, (def.CornerPosN.y == 1.0f || (def.CornerPosN.y == 0.0f && window_move_from_title_bar)) ? clamp_rect.Min.y : -float.MaxValue);
                ImVec2 clamp_max = new ImVec2(def.CornerPosN.x == 0.0f ? clamp_rect.Max.x : +float.MaxValue, def.CornerPosN.y == 0.0f ? clamp_rect.Max.y : +float.MaxValue);
                ImVec2 corner_target = g.IO.MousePos - g.ActiveIdClickOffset + ImMath.ImLerp(def.InnerDir * grip_hover_outer_size, def.InnerDir * -grip_hover_inner_size, def.CornerPosN); // Corner of the window corresponding to our corner grip
                corner_target = ImMath.ImClamp(corner_target, clamp_min, clamp_max);
                CalcResizePosSizeFromAnyCorner(window, corner_target, def.CornerPosN, out pos_target, out size_target);
            }

            // Only lower-left grip is visible before hovering/activating
            bool resize_grip_visible = held || hovered || (resize_grip_n == 0 && (window.Flags & ImGuiWindowFlags_ChildWindow) == 0);
            if (resize_grip_visible)
                resize_grip_col[resize_grip_n] = GetColorU32(held ? ImGuiCol_ResizeGripActive : hovered ? ImGuiCol_ResizeGripHovered : ImGuiCol_ResizeGrip);
        }

        int resize_border_mask = 0x00;
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0)
            resize_border_mask |= ((window.ChildFlags & ImGuiChildFlags_ResizeX) != 0 ? 0x02 : 0) | ((window.ChildFlags & ImGuiChildFlags_ResizeY) != 0 ? 0x08 : 0);
        else
            resize_border_mask = g.IO.ConfigWindowsResizeFromEdges ? 0x0F : 0x00;
        for (int border_n = 0; border_n < 4; border_n++)
        {
            if ((resize_border_mask & (1 << border_n)) == 0)
                continue;
            ImGuiResizeBorderDef def = resize_border_def[border_n];
            ImGuiAxis axis = (border_n == ImGuiDir_Left || border_n == ImGuiDir_Right) ? ImGuiAxis_X : ImGuiAxis_Y;

            bool hovered, held;
            ImRect border_rect = GetResizeBorderRect(window, border_n, grip_hover_inner_size, g.WindowsBorderHoverPadding);
            ImGuiID border_id = window.GetID(border_n + 4); // == GetWindowResizeBorderID()
            ItemAdd(border_rect, border_id, null, ImGuiItemFlags_NoNav);
            ButtonBehavior(border_rect, border_id, out hovered, out held, ImGuiButtonFlags_FlattenChildren | ImGuiButtonFlags_NoNavFocus);
            //GetForegroundDrawList(window).AddRect(border_rect.Min, border_rect.Max, IM_COL32(255, 255, 0, 255));
            if (hovered && g.HoveredIdTimer <= WINDOWS_RESIZE_FROM_EDGES_FEEDBACK_TIMER)
                hovered = false;
            if (hovered || held)
                SetMouseCursor((axis == ImGuiAxis_X) ? ImGuiMouseCursor_ResizeEW : ImGuiMouseCursor_ResizeNS);
            if (held && g.IO.MouseDoubleClicked[0])
            {
                // Double-clicking bottom or right border auto-fit on this axis
                // FIXME: Support top and right borders: rework CalcResizePosSizeFromAnyCorner() to be reusable in both cases.
                if (border_n == 1 || border_n == 3) // Right and bottom border
                {
                    ImVec2 size_auto_fit = CalcWindowAutoFitSize(window, window.ContentSizeIdeal, 1 << axis);
                    size_target[axis] = CalcWindowSizeAfterConstraint(window, size_auto_fit)[axis];
                    ret_auto_fit_mask |= (1 << axis);
                    hovered = held = false; // So border doesn't show highlighted at new position
                }
                ClearActiveID();
            }
            else if (held)
            {
                // Switch to relative resizing mode when border geometry moved (e.g. resizing a child altering parent scroll), in order to avoid resizing feedback loop.
                // Currently only using relative mode on resizable child windows, as the problem to solve is more likely noticeable for them, but could apply for all windows eventually.
                // FIXME: May want to generalize this idiom at lower-level, so more widgets can use it!
                bool just_scrolled_manually_while_resizing = (g.WheelingWindow != null && g.WheelingWindowScrolledFrame == g.FrameCount && IsWindowChildOf(window, g.WheelingWindow, false, true));
                if (g.ActiveIdIsJustActivated || just_scrolled_manually_while_resizing)
                {
                    g.WindowResizeBorderExpectedRect = border_rect;
                    g.WindowResizeRelativeMode = false;
                }
                if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0 && (g.WindowResizeBorderExpectedRect.Min != border_rect.Min || g.WindowResizeBorderExpectedRect.Max != border_rect.Max)) // memcmp()
                    g.WindowResizeRelativeMode = true;

                ImVec2 border_curr = window.Pos + ImMath.ImMul(ImMath.ImMin(def.SegmentN1, def.SegmentN2), window.Size);
                float border_target_rel_mode_for_axis = border_curr[axis] + g.IO.MouseDelta[axis];
                float border_target_abs_mode_for_axis = g.IO.MousePos[axis] - g.ActiveIdClickOffset[axis] + g.WindowsBorderHoverPadding; // Match ButtonBehavior() padding above.

                // Use absolute mode position
                ImVec2 border_target = window.Pos;
                border_target[axis] = border_target_abs_mode_for_axis;

                // Use relative mode target for child window, ignore resize when moving back toward the ideal absolute position.
                bool ignore_resize = false;
                if (g.WindowResizeRelativeMode)
                {
                    //GetForegroundDrawList().AddText(GetMainViewport().WorkPos, IM_COL32_WHITE, "Relative Mode");
                    border_target[axis] = border_target_rel_mode_for_axis;
                    if (g.IO.MouseDelta[axis] == 0.0f || (g.IO.MouseDelta[axis] > 0.0f) == (border_target_rel_mode_for_axis > border_target_abs_mode_for_axis))
                        ignore_resize = true;
                }

                // Clamp, apply
                ImVec2 clamp_min = new ImVec2(border_n == ImGuiDir_Right ? clamp_rect.Min.x : -float.MaxValue, border_n == ImGuiDir_Down || (border_n == ImGuiDir_Up && window_move_from_title_bar) ? clamp_rect.Min.y : -float.MaxValue);
                ImVec2 clamp_max = new ImVec2(border_n == ImGuiDir_Left ? clamp_rect.Max.x : +float.MaxValue, border_n == ImGuiDir_Up ? clamp_rect.Max.y : +float.MaxValue);
                border_target = ImMath.ImClamp(border_target, clamp_min, clamp_max);
                if (!ignore_resize)
                    CalcResizePosSizeFromAnyCorner(window, border_target, ImMath.ImMin(def.SegmentN1, def.SegmentN2), out pos_target, out size_target);
            }
            if (hovered)
                border_hovered = border_n;
            if (held)
                border_held = border_n;
        }
        PopID();

        // Restore nav layer
        window.DC.NavLayerCurrent = ImGuiNavLayer_Main;

        // Navigation resize (keyboard/gamepad)
        // FIXME: This cannot be moved to NavUpdateWindowing() because CalcWindowSizeAfterConstraint() need to callback into user.
        // Not even sure the callback works here.
        if (g.NavWindowingTarget != null && g.NavWindowingTarget.RootWindowDockTree == window)
        {
            ImVec2 nav_resize_dir = new ImVec2(0.0f, 0.0f);
            if (g.NavInputSource == ImGuiInputSource_Keyboard && g.IO.KeyShift)
                nav_resize_dir = GetKeyMagnitude2d(ImGuiKey_LeftArrow, ImGuiKey_RightArrow, ImGuiKey_UpArrow, ImGuiKey_DownArrow);
            if (g.NavInputSource == ImGuiInputSource_Gamepad)
                nav_resize_dir = GetKeyMagnitude2d(ImGuiKey_GamepadDpadLeft, ImGuiKey_GamepadDpadRight, ImGuiKey_GamepadDpadUp, ImGuiKey_GamepadDpadDown);
            if (nav_resize_dir.x != 0.0f || nav_resize_dir.y != 0.0f)
            {
                const float NAV_RESIZE_SPEED = 600.0f;
                float resize_step = NAV_RESIZE_SPEED * g.IO.DeltaTime * GetScale();
                g.NavWindowingAccumDeltaSize += nav_resize_dir * resize_step;
                g.NavWindowingAccumDeltaSize = ImMath.ImMax(g.NavWindowingAccumDeltaSize, clamp_rect.Min - window.Pos - window.Size); // We need Pos+Size >= clmap_rect.Min, so Size >= clmap_rect.Min - Pos, so size_delta >= clmap_rect.Min - window->Pos - window->Size
                g.NavWindowingToggleLayer = false;
                g.NavHighlightItemUnderNav = true;
                resize_grip_col[0] = GetColorU32(ImGuiCol_ResizeGripActive);
                ImVec2 accum_floored = ImMath.ImTrunc(g.NavWindowingAccumDeltaSize);
                if (accum_floored.x != 0.0f || accum_floored.y != 0.0f)
                {
                    // FIXME-NAV: Should store and accumulate into a separate size buffer to handle sizing constraints properly, right now a constraint will make us stuck.
                    size_target = CalcWindowSizeAfterConstraint(window, window.SizeFull + accum_floored);
                    g.NavWindowingAccumDeltaSize -= accum_floored;
                }
            }
        }

        // Apply back modified position/size to window
        ImVec2 old_pos = window.Pos;
        ImVec2 old_size = window.SizeFull;
        if (size_target.x != float.MaxValue && (window.Size.x != size_target.x || window.SizeFull.x != size_target.x))
        {
            window.Size.x = size_target.x;
            window.SizeFull.x = size_target.x;
        }
        if (size_target.y != float.MaxValue && (window.Size.y != size_target.y || window.SizeFull.y != size_target.y))
        {
            window.Size.y = size_target.y;
            window.SizeFull.y = size_target.y;
        }
        if (pos_target.x != float.MaxValue && window.Pos.x != ImMath.ImTrunc(pos_target.x))
            window.Pos.x = ImMath.ImTrunc(pos_target.x);
        if (pos_target.y != float.MaxValue && window.Pos.y != ImMath.ImTrunc(pos_target.y))
            window.Pos.y = ImMath.ImTrunc(pos_target.y);
        if (old_pos.x != window.Pos.x || old_pos.y != window.Pos.y || old_size.x != window.SizeFull.x || old_size.y != window.SizeFull.y)
            MarkIniSettingsDirty(window);

        // Recalculate next expected border expected coordinates
        if (border_held != -1)
            g.WindowResizeBorderExpectedRect = GetResizeBorderRect(window, border_held, grip_hover_inner_size, g.WindowsBorderHoverPadding);

        return ret_auto_fit_mask;
    }

    // imgui.cpp (file-static, near top of file): static const float WINDOWS_RESIZE_FROM_EDGES_FEEDBACK_TIMER = 0.04f;
    const float WINDOWS_RESIZE_FROM_EDGES_FEEDBACK_TIMER = 0.04f;

    // static inline void ClampWindowPos(ImGuiWindow* window, const ImRect& visibility_rect) (imgui.cpp)
    static void ClampWindowPos(ImGuiWindow window, ImRect visibility_rect)
    {
        ImVec2 size_for_clamping = window.Size;
        bool move_from_title_bar_only = (window.BgClickFlags & ImGuiWindowBgClickFlags_Move) == 0;
        if (move_from_title_bar_only && window.DockNodeAsHost != null)
            size_for_clamping.y = GetFrameHeight(); // Not using window->TitleBarHeight() as DockNodeAsHost will report 0.0f here.
        else if (move_from_title_bar_only && (window.Flags & ImGuiWindowFlags_NoTitleBar) == 0)
            size_for_clamping.y = window.TitleBarHeight;
        window.Pos = ImMath.ImClamp(window.Pos, visibility_rect.Min - size_for_clamping, visibility_rect.Max);
    }

    // static void RenderWindowOuterSingleBorder(ImGuiWindow* window, int border_n, ImU32 border_col, float border_size) (imgui.cpp)
    static void RenderWindowOuterSingleBorder(ImGuiWindow window, int border_n, ImU32 border_col, float border_size)
    {
        ImGuiResizeBorderDef def = resize_border_def[border_n];
        float rounding = window.WindowRounding;
        ImRect border_r = GetResizeBorderRect(window, border_n, rounding, 0.0f);
        window.DrawList.PathArcTo(ImMath.ImLerp(border_r.Min, border_r.Max, def.SegmentN1) + new ImVec2(0.5f, 0.5f) + def.InnerDir * rounding, rounding, def.OuterAngle - ImMath.IM_PI * 0.25f, def.OuterAngle, 0);
        window.DrawList.PathArcTo(ImMath.ImLerp(border_r.Min, border_r.Max, def.SegmentN2) + new ImVec2(0.5f, 0.5f) + def.InnerDir * rounding, rounding, def.OuterAngle, def.OuterAngle + ImMath.IM_PI * 0.25f, 0);
        window.DrawList.PathStroke(border_col, border_size);
    }

    // static void ImGui::RenderWindowOuterBorders(ImGuiWindow* window)
    static void RenderWindowOuterBorders(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        float border_size = window.WindowBorderSize;
        ImU32 border_col = GetColorU32(ImGuiCol_Border);
        if (border_size > 0.0f && (window.Flags & ImGuiWindowFlags_NoBackground) == 0)
            window.DrawList.AddRect(window.Pos, window.Pos + window.Size, border_col, window.WindowRounding, window.WindowBorderSize);
        else if (border_size > 0.0f)
        {
            if ((window.ChildFlags & ImGuiChildFlags_ResizeX) != 0) // Similar code as 'resize_border_mask' computation in UpdateWindowManualResize() but we specifically only always draw explicit child resize border.
                RenderWindowOuterSingleBorder(window, 1, border_col, border_size);
            if ((window.ChildFlags & ImGuiChildFlags_ResizeY) != 0)
                RenderWindowOuterSingleBorder(window, 3, border_col, border_size);
        }
        if (window.ResizeBorderHovered != -1 || window.ResizeBorderHeld != -1)
        {
            int border_n = (window.ResizeBorderHeld != -1) ? window.ResizeBorderHeld : window.ResizeBorderHovered;
            ImU32 border_col_resizing = GetColorU32((window.ResizeBorderHeld != -1) ? ImGuiCol_SeparatorActive : ImGuiCol_SeparatorHovered);
            RenderWindowOuterSingleBorder(window, border_n, border_col_resizing, ImMath.ImMax(2.0f, window.WindowBorderSize)); // Thicker than usual
        }
        if (g.Style.FrameBorderSize > 0 && (window.Flags & ImGuiWindowFlags_NoTitleBar) == 0 && !window.DockIsActive)
        {
            float y = window.Pos.y + window.TitleBarHeight - 1;
            window.DrawList.AddLineH(window.Pos.x + border_size * 0.5f, window.Pos.x + window.Size.x - border_size * 0.5f, y, border_col, g.Style.FrameBorderSize);
        }
    }

    // Draw background and borders
    // Draw and handle scrollbars
    // void ImGui::RenderWindowDecorations(ImGuiWindow* window, const ImRect& title_bar_rect, bool title_bar_is_highlight, bool handle_borders_and_resize_grips, int resize_grip_count, const ImU32 resize_grip_col[4], float resize_grip_draw_size)
    public static void RenderWindowDecorations(ImGuiWindow window, ImRect title_bar_rect, bool title_bar_is_highlight, bool handle_borders_and_resize_grips, int resize_grip_count, ReadOnlySpan<ImU32> resize_grip_col, float resize_grip_draw_size)
    {
        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiWindowFlags flags = window.Flags;

        // Ensure that Scrollbar() doesn't read last frame's SkipItems
        Debug.Assert(window.BeginCount == 0);
        window.SkipItems = false;
        window.DC.NavLayerCurrent = ImGuiNavLayer_Menu;

        // Draw window + handle manual resize
        // As we highlight the title bar when want_focus is set, multiple reappearing windows will have their title bar highlighted on their reappearing frame.
        float window_rounding = window.WindowRounding;
        float window_border_size = window.WindowBorderSize;
        if (window.Collapsed)
        {
            // Title bar only
            float backup_border_size = style.FrameBorderSize;
            g.Style.FrameBorderSize = window.WindowBorderSize;
            ImU32 title_bar_col = GetColorU32((title_bar_is_highlight && g.NavCursorVisible) ? ImGuiCol_TitleBgActive : ImGuiCol_TitleBgCollapsed);
            if (window.ViewportOwned)
                title_bar_col |= ImGuiCore.IM_COL32_A_MASK; // No alpha (we don't support is_docking_transparent_payload here because simpler and less meaningful, but could with a bit of code shuffle/reuse)
            RenderFrame(title_bar_rect.Min, title_bar_rect.Max, title_bar_col, true, window_rounding);
            g.Style.FrameBorderSize = backup_border_size;
        }
        else
        {
            // Window background
            if ((flags & ImGuiWindowFlags_NoBackground) == 0)
            {
                bool is_docking_transparent_payload = false;
                if (g.DragDropActive && (g.FrameCount - g.DragDropAcceptFrameCount) <= 1 && g.IO.ConfigDockingTransparentPayload)
                    if (g.DragDropPayload.IsDataType(IMGUI_PAYLOAD_TYPE_WINDOW) && DockNodeGetWindowFromPayload(g.DragDropPayload) == window)
                        is_docking_transparent_payload = true;

                ImU32 bg_col = GetColorU32(GetWindowBgColorIdx(window));
                if (window.ViewportOwned)
                {
                    bg_col |= ImGuiCore.IM_COL32_A_MASK; // No alpha
                    if (is_docking_transparent_payload)
                        window.Viewport.Alpha *= DOCKING_TRANSPARENT_PAYLOAD_ALPHA;
                }
                else
                {
                    // Adjust alpha. For docking
                    bool override_alpha = false;
                    float alpha = 1.0f;
                    if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasBgAlpha) != 0)
                    {
                        alpha = g.NextWindowData.BgAlphaVal;
                        override_alpha = true;
                    }
                    if (is_docking_transparent_payload)
                    {
                        alpha *= DOCKING_TRANSPARENT_PAYLOAD_ALPHA; // FIXME-DOCK: Should that be an override?
                        override_alpha = true;
                    }
                    if (override_alpha)
                        bg_col = (bg_col & ~ImGuiCore.IM_COL32_A_MASK) | ((ImU32)(int)(ImMath.ImSaturate(alpha) * 255.0f + 0.5f) << ImGuiCore.IM_COL32_A_SHIFT); // IM_F32_TO_INT8_SAT()
                }

                // Render, for docked windows and host windows we ensure BG goes before decorations
                if (window.DockIsActive)
                    window.DockNode.LastBgColor = bg_col;
                if ((flags & ImGuiWindowFlags_DockNodeHost) != 0)
                    bg_col = 0;
                if ((bg_col & ImGuiCore.IM_COL32_A_MASK) != 0)
                {
                    ImRect bg_rect = new ImRect(window.Pos + new ImVec2(0, window.TitleBarHeight), window.Pos + window.Size);
                    ImDrawFlags bg_rounding_flags;
                    if (window.DockIsActive)
                        bg_rounding_flags = (ImDrawFlags)CalcRoundingFlagsForRectInRect(bg_rect, window.DockNode.HostWindow.Rect(), 0.0f);
                    else
                        bg_rounding_flags = (flags & ImGuiWindowFlags_NoTitleBar) != 0 ? ImDrawFlags_RoundCornersAll : ImDrawFlags_RoundCornersBottom;
                    ImDrawList bg_draw_list = window.DockIsActive ? window.DockNode.HostWindow.DrawList : window.DrawList;
                    if (window.DockIsActive)
                        bg_draw_list._Splitter.SetCurrentChannel(bg_draw_list, DOCKING_HOST_DRAW_CHANNEL_BG);
                    bg_draw_list.AddRectFilled(bg_rect.Min, bg_rect.Max, bg_col, window_rounding, bg_rounding_flags);
                    if (window.DockIsActive)
                        bg_draw_list._Splitter.SetCurrentChannel(bg_draw_list, DOCKING_HOST_DRAW_CHANNEL_FG);
                }
            }
            if (window.DockIsActive)
                window.DockNode.IsBgDrawnThisFrame = true;

            // Title bar
            // (when docked, DockNode are drawing their own title bar. Individual windows however do NOT set the _NoTitleBar flag,
            // in order for their pos/size to be matching their undocking state.)
            if ((flags & ImGuiWindowFlags_NoTitleBar) == 0 && !window.DockIsActive)
            {
                ImU32 title_bar_col = GetColorU32(title_bar_is_highlight ? ImGuiCol_TitleBgActive : ImGuiCol_TitleBg);
                if (window.ViewportOwned)
                    title_bar_col |= ImGuiCore.IM_COL32_A_MASK; // No alpha
                window.DrawList.AddRectFilled(title_bar_rect.Min, title_bar_rect.Max, title_bar_col, window_rounding, ImDrawFlags_RoundCornersTop);
            }

            // Menu bar
            if ((flags & ImGuiWindowFlags_MenuBar) != 0)
            {
                ImRect menu_bar_rect = window.MenuBarRect();
                menu_bar_rect.ClipWith(window.Rect());  // Soft clipping, in particular child window don't have minimum size covering the menu bar so this is useful for them.
                window.DrawList.AddRectFilled(menu_bar_rect.Min, menu_bar_rect.Max, GetColorU32(ImGuiCol_MenuBarBg), (flags & ImGuiWindowFlags_NoTitleBar) != 0 ? window_rounding : 0.0f, ImDrawFlags_RoundCornersTop);
                if (style.FrameBorderSize > 0.0f && menu_bar_rect.Max.y < window.Pos.y + window.Size.y)
                    window.DrawList.AddLineH(menu_bar_rect.Min.x + window_border_size * 0.5f, menu_bar_rect.Max.x - window_border_size * 0.5f, menu_bar_rect.Max.y, GetColorU32(ImGuiCol_Border), style.FrameBorderSize);
            }

            // Docking: Unhide tab bar (small triangle in the corner), drag from small triangle to quickly undock
            {
                ImGuiDockNode node = window.DockNode;
                if (window.DockIsActive && node.IsHiddenTabBar() && !node.IsNoTabBar())
                {
                    float unhide_sz_draw = ImMath.ImTrunc(g.FontSize * 0.70f);
                    float unhide_sz_hit = ImMath.ImTrunc(g.FontSize * 0.55f);
                    ImVec2 p = node.Pos;
                    ImRect r = new ImRect(p, p + new ImVec2(unhide_sz_hit, unhide_sz_hit));
                    ImGuiID unhide_id = window.GetID("#UNHIDE");
                    KeepAliveID(unhide_id);
                    bool hovered, held;
                    if (ButtonBehavior(r, unhide_id, out hovered, out held, ImGuiButtonFlags_FlattenChildren))
                        node.WantHiddenTabBarToggle = true;
                    else if (held && IsMouseDragging(0))
                        StartMouseMovingWindowOrNode(window, node, true); // Undock from tab-bar triangle = same as window/collapse menu button

                    // FIXME-DOCK: Ideally we'd use ImGuiCol_TitleBgActive/ImGuiCol_TitleBg here, but neither is guaranteed to be visible enough at this sort of size..
                    ImU32 col = GetColorU32(((held && hovered) || (node.IsFocused && !hovered)) ? ImGuiCol_ButtonActive : hovered ? ImGuiCol_ButtonHovered : ImGuiCol_Button);
                    window.DrawList.AddTriangleFilled(p, p + new ImVec2(unhide_sz_draw, 0.0f), p + new ImVec2(0.0f, unhide_sz_draw), col);
                }
            }

            // Scrollbars
            if (window.ScrollbarX)
                Scrollbar(ImGuiAxis_X);
            if (window.ScrollbarY)
                Scrollbar(ImGuiAxis_Y);

            // Render resize grips (after their input handling so we don't have a frame of latency)
            if (handle_borders_and_resize_grips && (flags & ImGuiWindowFlags_NoResize) == 0)
            {
                for (int resize_grip_n = 0; resize_grip_n < resize_grip_count; resize_grip_n++)
                {
                    ImU32 col = resize_grip_col[resize_grip_n];
                    if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
                        continue;
                    ImGuiResizeGripDef grip = resize_grip_def[resize_grip_n];
                    ImVec2 corner = ImMath.ImLerp(window.Pos, window.Pos + window.Size, grip.CornerPosN);
                    float border_inner = ImMath.ImRound(window_border_size * 0.5f); // IM_ROUND()
                    window.DrawList.PathLineTo(corner + grip.InnerDir * ((resize_grip_n & 1) != 0 ? new ImVec2(border_inner, resize_grip_draw_size) : new ImVec2(resize_grip_draw_size, border_inner)));
                    window.DrawList.PathLineTo(corner + grip.InnerDir * ((resize_grip_n & 1) != 0 ? new ImVec2(resize_grip_draw_size, border_inner) : new ImVec2(border_inner, resize_grip_draw_size)));
                    window.DrawList.PathArcToFast(new ImVec2(corner.x + grip.InnerDir.x * (window_rounding + border_inner), corner.y + grip.InnerDir.y * (window_rounding + border_inner)), window_rounding, grip.AngleMin12, grip.AngleMax12);
                    window.DrawList.PathFillConvex(col);
                }
            }

            // Borders (for dock node host they will be rendered over after the tab bar)
            if (handle_borders_and_resize_grips && window.DockNodeAsHost == null)
                RenderWindowOuterBorders(window);
        }
        window.DC.NavLayerCurrent = ImGuiNavLayer_Main;
    }

    // Render title text, collapse button, close button
    // void ImGui::RenderWindowTitleBarContents(ImGuiWindow* window, const ImRect& title_bar_rect, const char* name, bool* p_open)
    public static unsafe void RenderWindowTitleBarContents(ImGuiWindow window, ImRect title_bar_rect, string name, bool* p_open)
    {
        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiWindowFlags flags = window.Flags;

        bool has_close_button = (p_open != null);
        bool has_collapse_button = (flags & ImGuiWindowFlags_NoCollapse) == 0 && (style.WindowMenuButtonPosition != ImGuiDir_None);

        // Close & Collapse button are on the Menu NavLayer and don't default focus (unless there's nothing else on that layer)
        // FIXME-NAV: Might want (or not?) to set the equivalent of ImGuiButtonFlags_NoNavFocus so that mouse clicks on standard title bar items don't necessarily set nav/keyboard ref?
        ImGuiItemFlags item_flags_backup = g.CurrentItemFlags;
        g.CurrentItemFlags |= ImGuiItemFlags_NoNavDefaultFocus;
        window.DC.NavLayerCurrent = ImGuiNavLayer_Menu;

        // Layout buttons
        // FIXME: Would be nice to generalize the subtleties expressed here into reusable code.
        float pad_l = style.FramePadding.x;
        float pad_r = style.FramePadding.x;
        float button_sz = g.FontSize;
        ImVec2 close_button_pos = default;
        ImVec2 collapse_button_pos = default;
        if (has_close_button)
        {
            close_button_pos = new ImVec2(title_bar_rect.Max.x - pad_r - button_sz, title_bar_rect.Min.y + style.FramePadding.y);
            pad_r += button_sz + style.ItemInnerSpacing.x;
        }
        if (has_collapse_button && style.WindowMenuButtonPosition == ImGuiDir_Right)
        {
            collapse_button_pos = new ImVec2(title_bar_rect.Max.x - pad_r - button_sz, title_bar_rect.Min.y + style.FramePadding.y);
            pad_r += button_sz + style.ItemInnerSpacing.x;
        }
        if (has_collapse_button && style.WindowMenuButtonPosition == ImGuiDir_Left)
        {
            collapse_button_pos = new ImVec2(title_bar_rect.Min.x + pad_l, title_bar_rect.Min.y + style.FramePadding.y);
            pad_l += button_sz + style.ItemInnerSpacing.x;
        }

        // Collapse button (submitting first so it gets priority when choosing a navigation init fallback)
        if (has_collapse_button)
            if (CollapseButton(window.GetID("#COLLAPSE"), collapse_button_pos, null))
                window.WantCollapseToggle = true; // Defer actual collapsing to next frame as we are too far in the Begin() function

        // Close button
        if (has_close_button)
        {
            ImGuiItemFlags backup_item_flags = g.CurrentItemFlags;
            g.CurrentItemFlags |= ImGuiItemFlags_NoFocus;
            if (CloseButton(window.GetID("#CLOSE"), close_button_pos))
                *p_open = false;
            g.CurrentItemFlags = backup_item_flags;
        }

        window.DC.NavLayerCurrent = ImGuiNavLayer_Main;
        g.CurrentItemFlags = item_flags_backup;

        // Title bar text (with: horizontal alignment, avoiding collapse/close button, optional "unsaved document" marker)
        // FIXME: Refactor text alignment facilities along with RenderText helpers, this is WAY too much messy code..
        float marker_size_x = (flags & ImGuiWindowFlags_UnsavedDocument) != 0 ? button_sz * 0.80f : 0.0f;
        ImVec2 text_size = CalcTextSize(name, -1, true) + new ImVec2(marker_size_x, 0.0f);

        // As a nice touch we try to ensure that centered title text doesn't get affected by visibility of Close/Collapse button,
        // while uncentered title text will still reach edges correctly.
        if (pad_l > style.FramePadding.x)
            pad_l += g.Style.ItemInnerSpacing.x;
        if (pad_r > style.FramePadding.x)
            pad_r += g.Style.ItemInnerSpacing.x;
        if (style.WindowTitleAlign.x > 0.0f && style.WindowTitleAlign.x < 1.0f)
        {
            float centerness = ImMath.ImSaturate(1.0f - MathF.Abs(style.WindowTitleAlign.x - 0.5f) * 2.0f); // 0.0f on either edges, 1.0f on center
            float pad_extend = ImMath.ImMin(ImMath.ImMax(pad_l, pad_r), title_bar_rect.GetWidth() - pad_l - pad_r - text_size.x);
            pad_l = ImMath.ImMax(pad_l, pad_extend * centerness);
            pad_r = ImMath.ImMax(pad_r, pad_extend * centerness);
        }

        ImRect layout_r = new ImRect(title_bar_rect.Min.x + pad_l, title_bar_rect.Min.y, title_bar_rect.Max.x - pad_r, title_bar_rect.Max.y);
        ImRect clip_r = new ImRect(layout_r.Min.x, layout_r.Min.y, ImMath.ImMin(layout_r.Max.x + g.Style.ItemInnerSpacing.x, title_bar_rect.Max.x), layout_r.Max.y);
        if ((flags & ImGuiWindowFlags_UnsavedDocument) != 0)
        {
            ImVec2 marker_pos = new ImVec2(0.0f, 0.0f);
            marker_pos.x = ImMath.ImClamp(layout_r.Min.x + (layout_r.GetWidth() - text_size.x) * style.WindowTitleAlign.x + text_size.x, layout_r.Min.x, layout_r.Max.x);
            marker_pos.y = (layout_r.Min.y + layout_r.Max.y) * 0.5f;
            if (marker_pos.x > layout_r.Min.x)
            {
                RenderBullet(window.DrawList, marker_pos, GetColorU32(ImGuiCol_UnsavedMarker));
                clip_r.Max.x = ImMath.ImMin(clip_r.Max.x, marker_pos.x - (int)(marker_size_x * 0.5f));
            }
        }
        //if (g.IO.KeyShift) window->DrawList->AddRect(layout_r.Min, layout_r.Max, IM_COL32(255, 128, 0, 255)); // [DEBUG]
        //if (g.IO.KeyCtrl) window->DrawList->AddRect(clip_r.Min, clip_r.Max, IM_COL32(255, 128, 0, 255)); // [DEBUG]
        RenderTextClipped(layout_r.Min, layout_r.Max, name, -1, text_size, style.WindowTitleAlign, clip_r);
    }

    // void ImGui::UpdateWindowParentAndRootLinks(ImGuiWindow* window, ImGuiWindowFlags flags, ImGuiWindow* parent_window)
    public static void UpdateWindowParentAndRootLinks(ImGuiWindow window, ImGuiWindowFlags flags, ImGuiWindow parent_window)
    {
        window.ParentWindow = parent_window;
        window.RootWindow = window.RootWindowPopupTree = window.RootWindowDockTree = window.RootWindowForTitleBarHighlight = window.RootWindowForNav = window;
        if (parent_window != null && (flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_Tooltip) == 0)
        {
            window.RootWindowDockTree = parent_window.RootWindowDockTree;
            if (!window.DockIsActive && (parent_window.Flags & ImGuiWindowFlags_DockNodeHost) == 0)
                window.RootWindow = parent_window.RootWindow;
        }
        if (parent_window != null && (flags & ImGuiWindowFlags_Popup) != 0)
            window.RootWindowPopupTree = parent_window.RootWindowPopupTree;
        if (parent_window != null && (flags & ImGuiWindowFlags_Modal) == 0 && (flags & (ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_Popup | ImGuiWindowFlags_Tooltip)) != 0)
            window.RootWindowForTitleBarHighlight = parent_window.RootWindowForTitleBarHighlight;
        while ((window.RootWindowForNav.ChildFlags & ImGuiChildFlags_NavFlattened) != 0)
        {
            Debug.Assert(window.RootWindowForNav.ParentWindow != null);
            window.RootWindowForNav = window.RootWindowForNav.ParentWindow;
        }
    }

    // [EXPERIMENTAL] Called by Begin(). NextWindowData is valid at this point.
    // This is designed as a toy/test-bed for
    // void ImGui::UpdateWindowSkipRefresh(ImGuiWindow* window)
    public static void UpdateWindowSkipRefresh(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        window.SkipRefresh = false;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasRefreshPolicy) == 0)
            return;
        if ((g.NextWindowData.RefreshFlagsVal & ImGuiWindowRefreshFlags_TryToAvoidRefresh) != 0)
        {
            // FIXME-IDLE: Tests for e.g. mouse clicks or keyboard while focused.
            if (window.Appearing) // If currently appearing
                return;
            if (window.Hidden) // If was hidden (previous frame)
                return;
            if ((g.NextWindowData.RefreshFlagsVal & ImGuiWindowRefreshFlags_RefreshOnHover) != 0 && g.HoveredWindow != null)
                if (window.RootWindow == g.HoveredWindow.RootWindow || IsWindowWithinBeginStackOf(g.HoveredWindow.RootWindow, window))
                    return;
            if ((g.NextWindowData.RefreshFlagsVal & ImGuiWindowRefreshFlags_RefreshOnFocus) != 0 && g.NavWindow != null)
                if (window.RootWindow == g.NavWindow.RootWindow || IsWindowWithinBeginStackOf(g.NavWindow.RootWindow, window))
                    return;
            window.DrawList = null;
            window.SkipRefresh = true;
        }
    }

    // static void SetWindowActiveForSkipRefresh(ImGuiWindow* window) (imgui.cpp)
    static void SetWindowActiveForSkipRefresh(ImGuiWindow window)
    {
        window.Active = true;
        for (int i = 0; i < window.DC.ChildWindows.Size; i++)
        {
            ImGuiWindow child = window.DC.ChildWindows[i];
            if (!child.Hidden)
            {
                child.Active = child.SkipRefresh = true;
                SetWindowActiveForSkipRefresh(child);
            }
        }
    }
    // NOTE: Begin() is ported in ImGuiWindows2.cs (the function starts in this file range but the owning agent continues from there).
}
