// Port of Dear ImGui imgui.cpp [SECTION] POPUPS (lines ~12368-13036),
// [SECTION] WINDOW FOCUS (lines ~13036-13304) and
// [SECTION] DRAG AND DROP (lines ~15026-15427).
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    //-----------------------------------------------------------------------------
    // [SECTION] POPUPS
    //-----------------------------------------------------------------------------

    // Supported flags: ImGuiPopupFlags_AnyPopupId, ImGuiPopupFlags_AnyPopupLevel
    public static bool IsPopupOpen(ImGuiID id, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        if ((popup_flags & ImGuiPopupFlags_AnyPopupId) != 0)
        {
            // Return true if any popup is open at the current BeginPopup() level of the popup stack
            // This may be used to e.g. test for another popups already opened to handle popups priorities at the same level.
            Debug.Assert(id == 0);
            if ((popup_flags & ImGuiPopupFlags_AnyPopupLevel) != 0)
                return g.OpenPopupStack.Size > 0;
            else
                return g.OpenPopupStack.Size > g.BeginPopupStack.Size;
        }
        else
        {
            if ((popup_flags & ImGuiPopupFlags_AnyPopupLevel) != 0)
            {
                // Return true if the popup is open anywhere in the popup stack
                for (int n = 0; n < g.OpenPopupStack.Size; n++)
                    if (g.OpenPopupStack[n].PopupId == id)
                        return true;
                return false;
            }
            else
            {
                // Return true if the popup is open at the current BeginPopup() level of the popup stack (this is the most-common query)
                return g.OpenPopupStack.Size > g.BeginPopupStack.Size && g.OpenPopupStack[g.BeginPopupStack.Size].PopupId == id;
            }
        }
    }

    public static bool IsPopupOpen(string str_id, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiID id = (popup_flags & ImGuiPopupFlags_AnyPopupId) != 0 ? 0 : g.CurrentWindow.GetID(str_id);
        if ((popup_flags & ImGuiPopupFlags_AnyPopupLevel) != 0 && id != 0)
            Debug.Assert(false, "Cannot use IsPopupOpen() with a string id and ImGuiPopupFlags_AnyPopupLevel."); // But non-string version is legal and used internally
        return IsPopupOpen(id, popup_flags);
    }

    // Also see FindBlockingModal(NULL)
    public static ImGuiWindow GetTopMostPopupModal()
    {
        ImGuiContext g = GImGui;
        for (int n = g.OpenPopupStack.Size - 1; n >= 0; n--)
            if (g.OpenPopupStack.Data[n].Window is ImGuiWindow popup)
                if ((popup.Flags & ImGuiWindowFlags_Modal) != 0)
                    return popup;
        return null;
    }

    // See Demo->Stacked Modal to confirm what this is for.
    public static ImGuiWindow GetTopMostAndVisiblePopupModal()
    {
        ImGuiContext g = GImGui;
        for (int n = g.OpenPopupStack.Size - 1; n >= 0; n--)
            if (g.OpenPopupStack.Data[n].Window is ImGuiWindow popup)
                if ((popup.Flags & ImGuiWindowFlags_Modal) != 0 && IsWindowActiveAndVisible(popup))
                    return popup;
        return null;
    }

    // When a modal popup is open, newly created windows that want focus (i.e. are not popups and do not specify ImGuiWindowFlags_NoFocusOnAppearing)
    // should be positioned behind that modal window, unless the window was created inside the modal begin-stack.
    // In case of multiple stacked modals newly created window honors begin stack order and does not go below its own modal parent.
    // - WindowA            // FindBlockingModal() returns Modal1
    //   - WindowB          //                  .. returns Modal1
    //   - Modal1           //                  .. returns Modal2
    //      - WindowC       //                  .. returns Modal2
    //          - WindowD   //                  .. returns Modal2
    //          - Modal2    //                  .. returns Modal2
    //            - WindowE //                  .. returns NULL
    // Notes:
    // - FindBlockingModal(NULL) == NULL is generally equivalent to GetTopMostPopupModal() == NULL.
    //   Only difference is here we check for ->Active/WasActive but it may be unnecessary.
    public static ImGuiWindow FindBlockingModal(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        if (g.OpenPopupStack.Size <= 0)
            return null;

        // Find a modal that has common parent with specified window. Specified window should be positioned behind that modal.
        for (int n = 0; n < g.OpenPopupStack.Size; n++)
        {
            ImGuiPopupData popup_data = g.OpenPopupStack[n];
            ImGuiWindow popup_window = popup_data.Window;
            if (popup_window == null || (popup_window.Flags & ImGuiWindowFlags_Modal) == 0)
                continue;
            if (!popup_window.Active && !popup_window.WasActive)  // Check WasActive, because this code may run before popup renders on current frame, also check Active to handle newly created windows.
                continue;
            if (window == null)                                   // FindBlockingModal(NULL) test for if FocusWindow(NULL) is naturally possible via a mouse click.
                return popup_window;
            if (IsWindowWithinBeginStackOf(window, popup_window)) // Window may be over modal
                continue;
            return popup_window;                                  // Place window right below first block modal
        }
        return null;
    }

    public static bool OpenPopup(string str_id, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiID id = g.CurrentWindow.GetID(str_id);
        return OpenPopupEx(id, popup_flags);
    }

    public static bool OpenPopup(ImGuiID id, ImGuiPopupFlags popup_flags = 0)
    {
        return OpenPopupEx(id, popup_flags);
    }

    // Mark popup as open (toggle toward open state).
    // - Return true when the popup is toggled open, which allows you to capture local state if needed.
    //   You may also call IsWindowAppearing() inside the later BeginPopup() scope if you need to prepare/compute data for the popup.
    // - Popups are closed when user click outside, or activate a pressable item, or CloseCurrentPopup() is called within a BeginPopup()/EndPopup() block.
    // - Popup identifiers are relative to the current ID-stack (so OpenPopup and BeginPopup needs to be at the same level).
    // - One open popup per level of the popup hierarchy (NB: when assigning we reset the Window member of ImGuiPopupRef to NULL).
    public static bool OpenPopupEx(ImGuiID id, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow parent_window = g.CurrentWindow;
        int current_stack_size = g.BeginPopupStack.Size;

        if ((popup_flags & ImGuiPopupFlags_NoOpenOverExistingPopup) != 0)
            if (IsPopupOpen((ImGuiID)0, ImGuiPopupFlags_AnyPopupId))
                return false;

        ImGuiPopupData popup_ref = default; // Tagged as new ref as Window will be set back to NULL if we write this into OpenPopupStack.
        popup_ref.PopupId = id;
        popup_ref.Window = null;
        popup_ref.RestoreNavWindow = g.NavWindow;           // When popup closes focus may be restored to NavWindow (depend on window type).
        popup_ref.OpenFrameCount = g.FrameCount;
        popup_ref.OpenParentId = parent_window.IDStack.back();
        popup_ref.OpenPopupPos = NavCalcPreferredRefPos(ImGuiWindowFlags_Popup);
        popup_ref.OpenMousePos = IsMousePosValid(ref g.IO.MousePos) ? g.IO.MousePos : popup_ref.OpenPopupPos;

        if (g.OpenPopupStack.Size < current_stack_size + 1)
        {
            g.OpenPopupStack.push_back(popup_ref);
            return true;
        }
        else
        {
            // Gently handle the user mistakenly calling OpenPopup() every frames: it is likely a programming mistake!
            // However, if we were to run the regular code path, the ui would become completely unusable because the popup will always be
            // in hidden-while-calculating-size state _while_ claiming focus. Which is extremely confusing situation for the programmer.
            // Instead, for successive frames calls to OpenPopup(), we silently avoid reopening even if ImGuiPopupFlags_NoReopen is not specified.
            bool keep_existing = false;
            if (g.OpenPopupStack[current_stack_size].PopupId == id)
                if ((g.OpenPopupStack[current_stack_size].OpenFrameCount == g.FrameCount - 1) || (popup_flags & ImGuiPopupFlags_NoReopen) != 0)
                    keep_existing = true;
            if (keep_existing)
            {
                // No reopen
                ImGuiPopupData slot = g.OpenPopupStack[current_stack_size];
                slot.OpenFrameCount = popup_ref.OpenFrameCount;
                g.OpenPopupStack[current_stack_size] = slot;
            }
            else
            {
                // Reopen: close child popups if any, then flag popup for open/reopen (set position, focus, init navigation)
                ClosePopupToLevel(current_stack_size, true);
                g.OpenPopupStack.push_back(popup_ref);
            }

            // When reopening a popup we first refocus its parent, otherwise if its parent is itself a popup it would get closed by ClosePopupsOverWindow().
            // This is equivalent to what ClosePopupToLevel() does.
            //if (g.OpenPopupStack[current_stack_size].PopupId == id)
            //    FocusWindow(parent_window);

            return !keep_existing;
        }
    }

    // When popups are stacked, clicking on a lower level popups puts focus back to it and close popups above it.
    // This function closes any popups that are over 'ref_window'.
    public static void ClosePopupsOverWindow(ImGuiWindow ref_window, bool restore_focus_to_window_under_popup)
    {
        ImGuiContext g = GImGui;
        if (g.OpenPopupStack.Size == 0)
            return;

        // Don't close our own child popup windows.
        int popup_count_to_keep = 0;
        if (ref_window != null)
        {
            // Find the highest popup which is a descendant of the reference window (generally reference window = NavWindow)
            for (; popup_count_to_keep < g.OpenPopupStack.Size; popup_count_to_keep++)
            {
                ImGuiPopupData popup = g.OpenPopupStack[popup_count_to_keep];
                if (popup.Window == null)
                    continue;
                Debug.Assert((popup.Window.Flags & ImGuiWindowFlags_Popup) != 0);

                // Trim the stack unless the popup is a direct parent of the reference window (the reference window is often the NavWindow)
                // - Clicking/Focusing Window2 won't close Popup1:
                //     Window -> Popup1 -> Window2(Ref)
                // - Clicking/focusing Popup1 will close Popup2 and Popup3:
                //     Window -> Popup1(Ref) -> Popup2 -> Popup3
                // - Each popups may contain child windows, which is why we compare ->RootWindow!
                //     Window -> Popup1 -> Popup1_Child -> Popup2 -> Popup2_Child
                // We step through every popup from bottom to top to validate their position relative to reference window.
                bool ref_window_is_descendant_of_popup = false;
                for (int n = popup_count_to_keep; n < g.OpenPopupStack.Size; n++)
                    if (g.OpenPopupStack[n].Window is ImGuiWindow popup_window)
                        if (IsWindowWithinBeginStackOf(ref_window, popup_window))
                        {
                            ref_window_is_descendant_of_popup = true;
                            break;
                        }
                if (!ref_window_is_descendant_of_popup)
                    break;
            }
        }
        if (popup_count_to_keep < g.OpenPopupStack.Size) // This test is not required but it allows to set a convenient breakpoint on the statement below
            ClosePopupToLevel(popup_count_to_keep, restore_focus_to_window_under_popup);
    }

    public static void ClosePopupsExceptModals()
    {
        ImGuiContext g = GImGui;

        int popup_count_to_keep;
        for (popup_count_to_keep = g.OpenPopupStack.Size; popup_count_to_keep > 0; popup_count_to_keep--)
        {
            ImGuiWindow window = g.OpenPopupStack[popup_count_to_keep - 1].Window;
            if (window == null || (window.Flags & ImGuiWindowFlags_Modal) != 0)
                break;
        }
        if (popup_count_to_keep < g.OpenPopupStack.Size) // This test is not required but it allows to set a convenient breakpoint on the statement below
            ClosePopupToLevel(popup_count_to_keep, true);
    }

    public static void ClosePopupToLevel(int remaining, bool restore_focus_to_window_under_popup)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(remaining >= 0 && remaining < g.OpenPopupStack.Size);

        // Trim open popup stack
        ImGuiPopupData prev_popup = g.OpenPopupStack[remaining];
        g.OpenPopupStack.resize(remaining);

        // Restore focus (unless popup window was not yet submitted, and didn't have a chance to take focus anyhow. See #7325 for an edge case)
        if (restore_focus_to_window_under_popup && prev_popup.Window != null)
        {
            ImGuiWindow popup_window = prev_popup.Window;
            ImGuiWindow focus_window = (popup_window.Flags & ImGuiWindowFlags_ChildMenu) != 0 ? popup_window.ParentWindow : prev_popup.RestoreNavWindow;
            if (focus_window != null && !focus_window.WasActive)
                FocusTopMostWindowUnderOne(popup_window, null, null, ImGuiFocusRequestFlags_RestoreFocusedChild); // Fallback
            else
                FocusWindow(focus_window, (g.NavLayer == ImGuiNavLayer_Main) ? ImGuiFocusRequestFlags_RestoreFocusedChild : ImGuiFocusRequestFlags_None);
        }
    }

    // Close the popup we have begin-ed into.
    public static void CloseCurrentPopup()
    {
        ImGuiContext g = GImGui;
        int popup_idx = g.BeginPopupStack.Size - 1;
        if (popup_idx < 0 || popup_idx >= g.OpenPopupStack.Size || g.BeginPopupStack[popup_idx].PopupId != g.OpenPopupStack[popup_idx].PopupId)
            return;

        // Closing a menu closes its top-most parent popup (unless a modal)
        while (popup_idx > 0)
        {
            ImGuiWindow popup_window = g.OpenPopupStack[popup_idx].Window;
            ImGuiWindow parent_popup_window = g.OpenPopupStack[popup_idx - 1].Window;
            bool close_parent = false;
            if (popup_window != null && (popup_window.Flags & ImGuiWindowFlags_ChildMenu) != 0)
                if (parent_popup_window != null && (parent_popup_window.Flags & ImGuiWindowFlags_MenuBar) == 0)
                    close_parent = true;
            if (!close_parent)
                break;
            popup_idx--;
        }
        ClosePopupToLevel(popup_idx, true);

        // A common pattern is to close a popup when selecting a menu item/selectable that will open another window.
        // To improve this usage pattern, we avoid nav highlight for a single frame in the parent window.
        // Similarly, we could avoid mouse hover highlight in this window but it is less visually problematic.
        if (g.NavWindow != null)
            g.NavWindow.DC.NavHideHighlightOneFrame = true;
    }

    // Attention! BeginPopup() adds default flags when calling BeginPopupEx()!
    public static unsafe bool BeginPopupEx(ImGuiID id, ImGuiWindowFlags extra_window_flags)
    {
        ImGuiContext g = GImGui;
        if (!IsPopupOpen(id, ImGuiPopupFlags_None))
        {
            g.NextWindowData.ClearFlags(); // We behave like Begin() and need to consume those values
            return false;
        }

        string name = $"##Popup_{id:x8}"; // No recycling, so we can close/open during the same frame
        Debug.Assert((extra_window_flags & ImGuiWindowFlags_ChildMenu) == 0); // Use BeginPopupMenuEx()

        bool is_open = Begin(name, extra_window_flags | ImGuiWindowFlags_Popup);
        if (!is_open) // NB: Begin can return false when the popup is completely clipped (e.g. zero size display)
            EndPopup();
        //g.CurrentWindow->FocusRouteParentWindow = g.CurrentWindow->ParentWindowInBeginStack;
        return is_open;
    }

    public static unsafe bool BeginPopupMenuEx(ImGuiID id, string label, ImGuiWindowFlags extra_window_flags)
    {
        ImGuiContext g = GImGui;
        if (!IsPopupOpen(id, ImGuiPopupFlags_None))
        {
            g.NextWindowData.ClearFlags(); // We behave like Begin() and need to consume those values
            return false;
        }

        // As we bypass BeginChild(), set ImGuiChildFlags_AlwaysAutoResize as it is checked independently from ImGuiWindowFlags_AlwaysAutoResize for now (see #9355)
        // Ideally we should remove setting ImGuiWindowFlags_AlwaysAutoResize in BeginChild().
        if ((extra_window_flags & ImGuiWindowFlags_ChildWindow) != 0 && (extra_window_flags & ImGuiWindowFlags_AlwaysAutoResize) != 0)
            SetNextWindowChildFlags(ImGuiChildFlags_AlwaysAutoResize, true);

        string name = $"{label}###Menu_{g.BeginMenuDepth:D2}"; // Recycle windows based on depth
        Debug.Assert((extra_window_flags & ImGuiWindowFlags_ChildMenu) != 0);
        bool is_open = Begin(name, extra_window_flags | ImGuiWindowFlags_Popup);
        if (!is_open) // NB: Begin can return false when the popup is completely clipped (e.g. zero size display)
            EndPopup();
        //g.CurrentWindow->FocusRouteParentWindow = g.CurrentWindow->ParentWindowInBeginStack;
        return is_open;
    }

    public static bool BeginPopup(string str_id, ImGuiWindowFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        if (g.OpenPopupStack.Size <= g.BeginPopupStack.Size) // Early out for performance
        {
            g.NextWindowData.ClearFlags(); // We behave like Begin() and need to consume those values
            return false;
        }
        flags |= ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoSavedSettings;
        ImGuiID id = g.CurrentWindow.GetID(str_id);
        return BeginPopupEx(id, flags);
    }

    // If 'p_open' is specified for a modal popup window, the popup will have a regular close button which will close the popup.
    // Note that popup visibility status is owned by Dear ImGui (and manipulated with e.g. OpenPopup).
    // - *p_open set back to false in BeginPopupModal() when popup is not open.
    // - if you set *p_open to false before calling BeginPopupModal(), it will close the popup.
        public static unsafe bool BeginPopupModal(string name, ImGuiWindowFlags flags = 0) => BeginPopupModalEx(name, null, flags);

    public static unsafe bool BeginPopupModal(string name, ref bool p_open, ImGuiWindowFlags flags = 0)
    {
        fixed (bool* p = &p_open)
            return BeginPopupModalEx(name, p, flags);
    }

private static unsafe bool BeginPopupModalEx(string name, bool* p_open, ImGuiWindowFlags flags)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = window.GetID(name);
        if (!IsPopupOpen(id, ImGuiPopupFlags_None))
        {
            g.NextWindowData.ClearFlags(); // We behave like Begin() and need to consume those values
            if (p_open != null && *p_open)
                *p_open = false;
            return false;
        }

        // Center modal windows by default for increased visibility
        // (this won't really last as settings will kick in, and is mostly for backward compatibility. user may do the same themselves)
        // FIXME: Should test for (PosCond & window->SetWindowPosAllowFlags) with the upcoming window.
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasPos) == 0)
        {
            ImGuiViewport viewport = GetMainViewport();
            SetNextWindowPos(viewport.GetCenter(), ImGuiCond_FirstUseEver, new ImVec2(0.5f, 0.5f));
        }

        flags |= ImGuiWindowFlags_Popup | ImGuiWindowFlags_Modal | ImGuiWindowFlags_NoCollapse;
        bool is_open = BeginEx(name, p_open, flags);
        if (!is_open || (p_open != null && !*p_open)) // NB: is_open can be 'false' when the popup is completely clipped (e.g. zero size display)
        {
            EndPopup();
            if (is_open)
                ClosePopupToLevel(g.BeginPopupStack.Size, true);
            return false;
        }
        return is_open;
    }

    public static void EndPopup()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (!((window.Flags & ImGuiWindowFlags_Popup) != 0 && g.BeginPopupStack.Size > 0))
        {
            Debug.Assert(false, "Calling EndPopup() in wrong window!"); // IM_ASSERT_USER_ERROR_RET
            return;
        }

        // Make all menus and popups wrap around for now, may need to expose that policy (e.g. focus scope could include wrap/loop policy flags used by new move requests)
        if (g.NavWindow == window)
            NavMoveRequestTryWrapping(window, ImGuiNavMoveFlags_LoopY);

        // Child-popups don't need to be laid out
        ImGuiID backup_within_end_popup_id = g.WithinEndPopupID;
        ImGuiID backup_within_end_child_id = g.WithinEndChildID;
        g.WithinEndPopupID = window.ID;
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0)
            g.WithinEndChildID = window.ID;
        End();
        g.WithinEndPopupID = backup_within_end_popup_id;
        g.WithinEndChildID = backup_within_end_child_id;
    }

    public static ImGuiMouseButton GetMouseButtonFromPopupFlags(ImGuiPopupFlags flags)
    {
        if ((flags & ImGuiPopupFlags_InvalidMask_) != 0) // 1,2 --> ImGuiMouseButton_Right, ImGuiMouseButton_Middle (obsolete mapping, IMGUI_DISABLE_OBSOLETE_FUNCTIONS not defined)
            return (flags & ImGuiPopupFlags_InvalidMask_);
        if ((flags & ImGuiPopupFlags_MouseButtonMask_) != 0)
            return ((flags & ImGuiPopupFlags_MouseButtonMask_) >> ImGuiPopupFlags_MouseButtonShift_) - 1;
        return ImGuiMouseButton_Right; // Default == 1
    }

    public static bool IsPopupOpenRequestForItem(ImGuiPopupFlags popup_flags, ImGuiID id)
    {
        ImGuiContext g = GImGui;
        ImGuiMouseButton mouse_button = GetMouseButtonFromPopupFlags(popup_flags);
        if (IsMouseReleased(mouse_button) && IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup))
            return true;
        if (g.NavOpenContextMenuItemId == id && (IsItemFocused() || id == g.CurrentWindow.MoveId))
            return true;
        return false;
    }

    public static bool IsPopupOpenRequestForWindow(ImGuiPopupFlags popup_flags)
    {
        ImGuiContext g = GImGui;
        ImGuiMouseButton mouse_button = GetMouseButtonFromPopupFlags(popup_flags);
        if (IsMouseReleased(mouse_button) && IsWindowHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup))
            if ((popup_flags & ImGuiPopupFlags_NoOpenOverItems) == 0 || !IsAnyItemHovered())
                return true;
        if (g.NavOpenContextMenuWindowId != 0 && g.CurrentWindow.ID != 0)
            if (IsWindowChildOf(g.NavWindow, g.CurrentWindow, false)) // This enable ordering to be used to disambiguate item vs window (#8803)
                return true;
        return false;
    }

    // Helper to open a popup if mouse button is released over the item
    // - This is essentially the same as BeginPopupContextItem() but without the trailing BeginPopup()
    public static bool OpenPopupOnItemClick(string str_id = null, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        if (IsPopupOpenRequestForItem(popup_flags, g.LastItemData.ID))
        {
            ImGuiWindow window = g.CurrentWindow;
            ImGuiID id = str_id != null ? window.GetID(str_id) : g.LastItemData.ID; // If user hasn't passed an ID, we can use the LastItemID. Using LastItemID as a Popup ID won't conflict!
            Debug.Assert(id != 0);                                                  // You cannot pass a NULL str_id if the last item has no identifier (e.g. a Text() item)
            return OpenPopupEx(id, popup_flags);
        }
        return false;
    }

    // This is a helper to handle the simplest case of associating one named popup to one given widget.
    // - To create a popup associated to the last item, you generally want to pass a NULL value to str_id.
    // - To create a popup with a specific identifier, pass it in str_id.
    //    - This is useful when using using BeginPopupContextItem() on an item which doesn't have an identifier, e.g. a Text() call.
    //    - This is useful when multiple code locations may want to manipulate/open the same popup, given an explicit id.
    // - You may want to handle the whole on user side if you have specific needs (e.g. tweaking IsItemHovered() parameters).
    //   This is essentially the same as:
    //       id = str_id ? GetID(str_id) : GetItemID();
    //       OpenPopupOnItemClick(str_id, ImGuiPopupFlags_MouseButtonRight);
    //       return BeginPopup(id);
    //   Which is essentially the same as:
    //       id = str_id ? GetID(str_id) : GetItemID();
    //       if (IsItemHovered() && IsMouseReleased(ImGuiMouseButton_Right))
    //           OpenPopup(id);
    //       return BeginPopup(id);
    //   The main difference being that this is tweaked to avoid computing the ID twice.
    public static bool BeginPopupContextItem(string str_id = null, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;
        ImGuiID id = str_id != null ? window.GetID(str_id) : g.LastItemData.ID; // If user hasn't passed an ID, we can use the LastItem ID. Using LastItem ID as a Popup ID won't conflict!
        Debug.Assert(id != 0);                                                  // You cannot pass a NULL str_id if the last item has no identifier (e.g. a Text() item)
        if (IsPopupOpenRequestForItem(popup_flags, g.LastItemData.ID))
            OpenPopupEx(id, popup_flags);
        return BeginPopupEx(id, ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoSavedSettings);
    }

    public static bool BeginPopupContextWindow(string str_id = null, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (str_id == null)
            str_id = "window_context";
        ImGuiID id = window.GetID(str_id);
        if (IsPopupOpenRequestForWindow(popup_flags))
            OpenPopupEx(id, popup_flags);
        return BeginPopupEx(id, ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoSavedSettings);
    }

    public static bool BeginPopupContextVoid(string str_id = null, ImGuiPopupFlags popup_flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (str_id == null)
            str_id = "void_context";
        ImGuiID id = window.GetID(str_id);
        ImGuiMouseButton mouse_button = GetMouseButtonFromPopupFlags(popup_flags);
        if (IsMouseReleased(mouse_button) && !IsWindowHovered(ImGuiHoveredFlags_AnyWindow))
            if (GetTopMostPopupModal() == null)
                OpenPopupEx(id, popup_flags);
        return BeginPopupEx(id, ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoSavedSettings);
    }

    // r_avoid = the rectangle to avoid (e.g. for tooltip it is a rectangle around the mouse cursor which we want to avoid. for popups it's a small point around the cursor.)
    // r_outer = the visible area rectangle, minus safe area padding. If our popup size won't fit because of safe area padding we ignore it.
    // (r_outer is usually equivalent to the viewport rectangle minus padding, but when multi-viewports are enabled and monitor
    //  information are available, it may represent the entire platform monitor from the frame of reference of the current viewport.
    //  this allows us to have tooltips/popups displayed out of the parent viewport.)
    public static ImVec2 FindBestWindowPosForPopupEx(ImVec2 ref_pos, ImVec2 size, ref ImGuiDir last_dir, ImRect r_outer, ImRect r_avoid, ImGuiPopupPositionPolicy policy)
    {
        ImVec2 base_pos_clamped = ImMath.ImClamp(ref_pos, r_outer.Min, r_outer.Max - size);
        //GetForegroundDrawList()->AddRect(r_avoid.Min, r_avoid.Max, IM_COL32(255,0,0,255));
        //GetForegroundDrawList()->AddRect(r_outer.Min, r_outer.Max, IM_COL32(0,255,0,255));

        // Combo Box policy (we want a connecting edge)
        if (policy == ImGuiPopupPositionPolicy_ComboBox)
        {
            int[] dir_preferred_order = { ImGuiDir_Down, ImGuiDir_Right, ImGuiDir_Left, ImGuiDir_Up };
            for (int n = (last_dir != ImGuiDir_None) ? -1 : 0; n < ImGuiDir_COUNT; n++)
            {
                ImGuiDir dir = (n == -1) ? last_dir : dir_preferred_order[n];
                if (n != -1 && dir == last_dir) // Already tried this direction?
                    continue;
                ImVec2 pos = default;
                if (dir == ImGuiDir_Down) pos = new ImVec2(r_avoid.Min.x, r_avoid.Max.y);          // Below, Toward Right (default)
                if (dir == ImGuiDir_Right) pos = new ImVec2(r_avoid.Min.x, r_avoid.Min.y - size.y); // Above, Toward Right
                if (dir == ImGuiDir_Left) pos = new ImVec2(r_avoid.Max.x - size.x, r_avoid.Max.y); // Below, Toward Left
                if (dir == ImGuiDir_Up) pos = new ImVec2(r_avoid.Max.x - size.x, r_avoid.Min.y - size.y); // Above, Toward Left
                if (!r_outer.Contains(new ImRect(pos, pos + size)))
                    continue;
                last_dir = dir;
                return pos;
            }
        }

        // Tooltip and Default popup policy
        // (Always first try the direction we used on the last frame, if any)
        if (policy == ImGuiPopupPositionPolicy_Tooltip || policy == ImGuiPopupPositionPolicy_Default)
        {
            int[] dir_preferred_order = { ImGuiDir_Right, ImGuiDir_Down, ImGuiDir_Up, ImGuiDir_Left };
            for (int n = (last_dir != ImGuiDir_None) ? -1 : 0; n < ImGuiDir_COUNT; n++)
            {
                ImGuiDir dir = (n == -1) ? last_dir : dir_preferred_order[n];
                if (n != -1 && dir == last_dir) // Already tried this direction?
                    continue;

                float avail_w = (dir == ImGuiDir_Left ? r_avoid.Min.x : r_outer.Max.x) - (dir == ImGuiDir_Right ? r_avoid.Max.x : r_outer.Min.x);
                float avail_h = (dir == ImGuiDir_Up ? r_avoid.Min.y : r_outer.Max.y) - (dir == ImGuiDir_Down ? r_avoid.Max.y : r_outer.Min.y);

                // If there's not enough room on one axis, there's no point in positioning on a side on this axis (e.g. when not enough width, use a top/bottom position to maximize available width)
                if (avail_w < size.x && (dir == ImGuiDir_Left || dir == ImGuiDir_Right))
                    continue;
                if (avail_h < size.y && (dir == ImGuiDir_Up || dir == ImGuiDir_Down))
                    continue;

                ImVec2 pos;
                pos.x = (dir == ImGuiDir_Left) ? r_avoid.Min.x - size.x : (dir == ImGuiDir_Right) ? r_avoid.Max.x : base_pos_clamped.x;
                pos.y = (dir == ImGuiDir_Up) ? r_avoid.Min.y - size.y : (dir == ImGuiDir_Down) ? r_avoid.Max.y : base_pos_clamped.y;

                // Clamp top-left corner of popup
                pos.x = ImMath.ImMax(pos.x, r_outer.Min.x);
                pos.y = ImMath.ImMax(pos.y, r_outer.Min.y);

                last_dir = dir;
                return pos;
            }
        }

        // Fallback when not enough room:
        last_dir = ImGuiDir_None;

        // For tooltip we prefer avoiding the cursor at all cost even if it means that part of the tooltip won't be visible.
        if (policy == ImGuiPopupPositionPolicy_Tooltip)
            return ref_pos + new ImVec2(2, 2);

        // Otherwise try to keep within display
        ImVec2 fallback_pos = ref_pos;
        fallback_pos.x = ImMath.ImMax(ImMath.ImMin(fallback_pos.x + size.x, r_outer.Max.x) - size.x, r_outer.Min.x);
        fallback_pos.y = ImMath.ImMax(ImMath.ImMin(fallback_pos.y + size.y, r_outer.Max.y) - size.y, r_outer.Min.y);
        return fallback_pos;
    }

    // Note that this is used for popups, which can overlap the non work-area of individual viewports.
    public static ImRect GetPopupAllowedExtentRect(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        ImRect r_screen = ((ImGuiViewportP)GetMainViewport()).GetMainRect();
        ImVec2 padding = g.Style.DisplaySafeAreaPadding;
        r_screen.Expand(new ImVec2((r_screen.GetWidth() > padding.x * 2) ? -padding.x : 0.0f, (r_screen.GetHeight() > padding.y * 2) ? -padding.y : 0.0f));
        return r_screen;
    }

    public static ImVec2 FindBestWindowPosForPopup(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;

        ImRect r_outer = GetPopupAllowedExtentRect(window);
        if ((window.Flags & ImGuiWindowFlags_ChildMenu) != 0)
        {
            // Child menus typically request _any_ position within the parent menu item, and then we move the new menu outside the parent bounds.
            // This is how we end up with child menus appearing (most-commonly) on the right of the parent menu.
            Debug.Assert(g.CurrentWindow == window);
            ImGuiWindow parent_window = g.CurrentWindowStack[g.CurrentWindowStack.Size - 2].Window;
            float horizontal_overlap = g.Style.ItemInnerSpacing.x; // We want some overlap to convey the relative depth of each menu (currently the amount of overlap is hard-coded to style.ItemSpacing.x).
            ImRect r_avoid;
            if (parent_window.DC.MenuBarAppending)
                r_avoid = new ImRect(-float.MaxValue, parent_window.ClipRect.Min.y, float.MaxValue, parent_window.ClipRect.Max.y); // Avoid parent menu-bar. If we wanted multi-line menu-bar, we may instead want to have the calling window setup e.g. a NextWindowData.PosConstraintAvoidRect field
            else
                r_avoid = new ImRect(parent_window.Pos.x + horizontal_overlap, -float.MaxValue, parent_window.Pos.x + parent_window.Size.x - horizontal_overlap - parent_window.ScrollbarSizes.x, float.MaxValue);
            return FindBestWindowPosForPopupEx(window.Pos, window.Size, ref window.AutoPosLastDirection, r_outer, r_avoid, ImGuiPopupPositionPolicy_Default);
        }
        if ((window.Flags & ImGuiWindowFlags_Popup) != 0)
        {
            return FindBestWindowPosForPopupEx(window.Pos, window.Size, ref window.AutoPosLastDirection, r_outer, new ImRect(window.Pos, window.Pos), ImGuiPopupPositionPolicy_Default); // Ideally we'd disable r_avoid here
        }
        if ((window.Flags & ImGuiWindowFlags_Tooltip) != 0)
        {
            // Position tooltip (always follows mouse + clamp within outer boundaries)
            // FIXME:
            // - Too many paths. One problem is that FindBestWindowPosForPopupEx() doesn't allow passing a suggested position (so touch screen path doesn't use it by default).
            // - Drag and drop tooltips are not using this path either: BeginTooltipEx() manually sets their position.
            // - Require some tidying up. In theory we could handle both cases in same location, but requires a bit of shuffling
            //   as drag and drop tooltips are calling SetNextWindowPos() leading to 'window_pos_set_by_api' being set in Begin().
            Debug.Assert(g.CurrentWindow == window);
            float scale = g.Style.MouseCursorScale;
            ImVec2 ref_pos = NavCalcPreferredRefPos(ImGuiWindowFlags_Tooltip);

            if (g.IO.MouseSource == ImGuiMouseSource_TouchScreen && NavCalcPreferredRefPosSource(ImGuiWindowFlags_Tooltip) == ImGuiInputSource_Mouse)
            {
                // ImVec2 tooltip_pos = ref_pos + TOOLTIP_DEFAULT_OFFSET_TOUCH * scale - (TOOLTIP_DEFAULT_PIVOT_TOUCH * window->Size);
                ImVec2 tooltip_pos = ref_pos + new ImVec2(0, -20) * scale - (new ImVec2(0.5f, 1.0f) * window.Size); // TOOLTIP_DEFAULT_OFFSET_TOUCH = (0,-20), TOOLTIP_DEFAULT_PIVOT_TOUCH = (0.5,1.0)
                if (r_outer.Contains(new ImRect(tooltip_pos, tooltip_pos + window.Size)))
                    return tooltip_pos;
            }

            ImVec2 tooltip_pos_mouse = ref_pos + new ImVec2(16, 10) * scale; // TOOLTIP_DEFAULT_OFFSET_MOUSE = (16,10)
            ImRect r_avoid;
            if (g.NavCursorVisible && g.NavHighlightItemUnderNav && !g.IO.ConfigNavMoveSetMousePos)
                r_avoid = new ImRect(ref_pos.x - 16, ref_pos.y - 8, ref_pos.x + 16, ref_pos.y + 8);
            else
                r_avoid = new ImRect(ref_pos.x - 16, ref_pos.y - 8, ref_pos.x + 24 * scale, ref_pos.y + 24 * scale); // FIXME: Hard-coded based on mouse cursor shape expectation. Exact dimension not very important.
            //GetForegroundDrawList()->AddRect(r_avoid.Min, r_avoid.Max, IM_COL32(255, 0, 255, 255));

            return FindBestWindowPosForPopupEx(tooltip_pos_mouse, window.Size, ref window.AutoPosLastDirection, r_outer, r_avoid, ImGuiPopupPositionPolicy_Tooltip);
        }
        Debug.Assert(false);
        return window.Pos;
    }

    //-----------------------------------------------------------------------------
    // [SECTION] WINDOW FOCUS
    //-----------------------------------------------------------------------------
    // - SetWindowFocus()
    // - SetNextWindowFocus()
    // - IsWindowFocused()
    // - UpdateWindowInFocusOrderList() [Internal]
    // - BringWindowToFocusFront() [Internal]
    // - BringWindowToDisplayFront() [Internal]
    // - BringWindowToDisplayBack() [Internal]
    // - BringWindowToDisplayBehind() [Internal]
    // - FindWindowDisplayIndex() [Internal]
    // - FocusWindow() [Internal]
    // - FocusTopMostWindowUnderOne() [Internal]
    //-----------------------------------------------------------------------------

    public static void SetWindowFocus()
    {
        FocusWindow(GImGui.CurrentWindow);
    }

    public static void SetWindowFocus(string name)
    {
        if (name != null)
        {
            if (FindWindowByName(name) is ImGuiWindow window)
                FocusWindow(window);
        }
        else
        {
            FocusWindow(null);
        }
    }

    public static void SetNextWindowFocus()
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasFocus;
    }

    // Similar to IsWindowHovered()
    public static bool IsWindowFocused(ImGuiFocusedFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow ref_window = g.NavWindow;
        ImGuiWindow cur_window = g.CurrentWindow;

        if (ref_window == null)
            return false;
        if ((flags & ImGuiFocusedFlags_AnyWindow) != 0)
            return true;

        Debug.Assert(cur_window != null); // Not inside a Begin()/End()
        bool popup_hierarchy = (flags & ImGuiFocusedFlags_NoPopupHierarchy) == 0;
        bool dock_hierarchy = (flags & ImGuiFocusedFlags_DockHierarchy) != 0;
        if ((flags & ImGuiFocusedFlags_RootWindow) != 0)
            cur_window = GetCombinedRootWindow(cur_window, popup_hierarchy, dock_hierarchy);

        if ((flags & ImGuiFocusedFlags_ChildWindows) != 0)
            return IsWindowChildOf(ref_window, cur_window, popup_hierarchy, dock_hierarchy);
        else
            return ref_window == cur_window;
    }

    public static int FindWindowFocusIndex(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        int order = window.FocusOrder;
        Debug.Assert(window.RootWindow == window); // No child window (not testing _ChildWindow because of docking)
        Debug.Assert(g.WindowsFocusOrder[order] == window);
        return order;
    }

    public static void UpdateWindowInFocusOrderList(ImGuiWindow window, bool just_created, ImGuiWindowFlags new_flags)
    {
        ImGuiContext g = GImGui;

        bool new_is_explicit_child = (new_flags & ImGuiWindowFlags_ChildWindow) != 0 && ((new_flags & ImGuiWindowFlags_Popup) == 0 || (new_flags & ImGuiWindowFlags_ChildMenu) != 0);
        bool child_flag_changed = new_is_explicit_child != window.IsExplicitChild;
        if ((just_created || child_flag_changed) && !new_is_explicit_child)
        {
            Debug.Assert(!g.WindowsFocusOrder.contains(window));
            g.WindowsFocusOrder.push_back(window);
            window.FocusOrder = (short)(g.WindowsFocusOrder.Size - 1);
        }
        else if (!just_created && child_flag_changed && new_is_explicit_child)
        {
            Debug.Assert(g.WindowsFocusOrder[window.FocusOrder] == window);
            for (int n = window.FocusOrder + 1; n < g.WindowsFocusOrder.Size; n++)
                g.WindowsFocusOrder[n].FocusOrder--;
            g.WindowsFocusOrder.erase(window.FocusOrder);
            window.FocusOrder = -1;
        }
        window.IsExplicitChild = new_is_explicit_child;
    }

    public static void BringWindowToFocusFront(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(window == window.RootWindow);

        int cur_order = window.FocusOrder;
        Debug.Assert(g.WindowsFocusOrder[cur_order] == window);
        if (g.WindowsFocusOrder.back() == window)
            return;

        int new_order = g.WindowsFocusOrder.Size - 1;
        for (int n = cur_order; n < new_order; n++)
        {
            g.WindowsFocusOrder[n] = g.WindowsFocusOrder[n + 1];
            g.WindowsFocusOrder[n].FocusOrder--;
            Debug.Assert(g.WindowsFocusOrder[n].FocusOrder == n);
        }
        g.WindowsFocusOrder[new_order] = window;
        window.FocusOrder = (short)new_order;
    }

    // Note technically focus related but rather adjacent and close to BringWindowToFocusFront()
    // FIXME-FOCUS: Could opt-in/opt-out enable modal check like in FocusWindow().
    public static void BringWindowToDisplayFront(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow current_front_window = g.Windows.back();
        if (current_front_window == window || current_front_window.RootWindow == window) // Cheap early out (could be better)
            return;
        for (int i = g.Windows.Size - 2; i >= 0; i--) // We can ignore the top-most window
            if (g.Windows[i] == window)
            {
                // memmove(&g.Windows[i], &g.Windows[i + 1], (g.Windows.Size - i - 1) * sizeof(ImGuiWindow*));
                Array.Copy(g.Windows.Data, i + 1, g.Windows.Data, i, g.Windows.Size - i - 1);
                g.Windows.Data[g.Windows.Size - 1] = window;
                break;
            }
    }

    public static void BringWindowToDisplayBack(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        if (g.Windows[0] == window)
            return;
        for (int i = 0; i < g.Windows.Size; i++)
            if (g.Windows[i] == window)
            {
                // memmove(&g.Windows[1], &g.Windows[0], i * sizeof(ImGuiWindow*));
                Array.Copy(g.Windows.Data, 0, g.Windows.Data, 1, i);
                g.Windows.Data[0] = window;
                break;
            }
    }

    public static void BringWindowToDisplayBehind(ImGuiWindow window, ImGuiWindow behind_window)
    {
        Debug.Assert(window != null && behind_window != null);
        ImGuiContext g = GImGui;
        window = window.RootWindow;
        behind_window = behind_window.RootWindow;
        int pos_wnd = FindWindowDisplayIndex(window);
        int pos_beh = FindWindowDisplayIndex(behind_window);
        if (pos_wnd < pos_beh)
        {
            // memmove(&g.Windows.Data[pos_wnd], &g.Windows.Data[pos_wnd + 1], (pos_beh - pos_wnd - 1) * sizeof(ImGuiWindow*));
            Array.Copy(g.Windows.Data, pos_wnd + 1, g.Windows.Data, pos_wnd, pos_beh - pos_wnd - 1);
            g.Windows.Data[pos_beh - 1] = window;
        }
        else
        {
            // memmove(&g.Windows.Data[pos_beh + 1], &g.Windows.Data[pos_beh], (pos_wnd - pos_beh) * sizeof(ImGuiWindow*));
            Array.Copy(g.Windows.Data, pos_beh, g.Windows.Data, pos_beh + 1, pos_wnd - pos_beh);
            g.Windows.Data[pos_beh] = window;
        }
    }

    public static int FindWindowDisplayIndex(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        return g.Windows.index_of_ref(window);
    }

    // Moving window to front of display and set focus (which happens to be back of our sorted list)
    public static void FocusWindow(ImGuiWindow window, ImGuiFocusRequestFlags flags = 0)
    {
        ImGuiContext g = GImGui;

        // Modal check?
        if ((flags & ImGuiFocusRequestFlags_UnlessBelowModal) != 0 && (g.NavWindow != window)) // Early out in common case.
        {
            ImGuiWindow blocking_modal = FindBlockingModal(window);
            if (blocking_modal != null)
            {
                // This block would typically be reached in two situations:
                // - API call to FocusWindow() with a window under a modal and ImGuiFocusRequestFlags_UnlessBelowModal flag.
                // - User clicking on void or anything behind a modal while a modal is open (window == NULL)
                if (window != null && window == window.RootWindow && (window.Flags & ImGuiWindowFlags_NoBringToFrontOnFocus) == 0)
                    BringWindowToDisplayBehind(window, blocking_modal); // Still bring right under modal. (FIXME: Could move in focus list too?)
                ClosePopupsOverWindow(GetTopMostPopupModal(), false); // Note how we need to use GetTopMostPopupModal() aad NOT blocking_modal, to handle nested modals
                return;
            }
        }

        // Find last focused child (if any) and focus it instead.
        if ((flags & ImGuiFocusRequestFlags_RestoreFocusedChild) != 0 && window != null)
            window = NavRestoreLastChildNavWindow(window);

        // Apply focus
        if (g.NavWindow != window)
        {
            SetNavWindow(window);
            if (window != null && g.NavHighlightItemUnderNav)
                g.NavMousePosDirty = true;
            g.NavId = window != null ? window.NavLastIds[0] : 0; // Restore NavId
            g.NavLayer = ImGuiNavLayer_Main;
            SetNavFocusScope(window != null ? window.NavRootFocusScopeId : 0);
            g.NavIdIsAlive = false;
            g.NavLastValidSelectionUserData = -1; // ImGuiSelectionUserData_Invalid

            // Close popups if any
            ClosePopupsOverWindow(window, false);
        }

        // Move the root window to the top of the pile
        Debug.Assert(window == null || window.RootWindow != null);
        ImGuiWindow focus_front_window = window != null ? window.RootWindow : null; // NB: In docking branch this is window->RootWindowDockStop
        ImGuiWindow display_front_window = window != null ? window.RootWindowDockTree : null;
        ImGuiDockNode dock_node = window != null ? window.DockNode : null;
        bool active_id_window_is_dock_node_host = g.ActiveIdWindow != null && dock_node != null && dock_node.HostWindow == g.ActiveIdWindow;

        // Steal active widgets. Some of the cases it triggers includes:
        // - Focus a window while an InputText in another window is active, if focus happens before the old InputText can run.
        // - When using Nav to activate nav items (due to timing of activating on press->new window appears->losing ActiveId)
        // - Using dock host items (tab, collapse button) can trigger this before we redirect the ActiveIdWindow toward the child window.
        if (g.ActiveId != 0 && g.ActiveIdWindow != null && g.ActiveIdWindow.RootWindow != focus_front_window)
            if (!g.ActiveIdNoClearOnFocusLoss && !active_id_window_is_dock_node_host)
                ClearActiveID();

        // Passing NULL allow to disable keyboard focus
        if (window == null)
            return;
        window.LastFrameJustFocused = g.FrameCount;

        // Select in dock node
        // For #2304 we avoid applying focus immediately before the tabbar is visible.
        //if (dock_node && dock_node->TabBar)
        //    dock_node->TabBar->SelectedTabId = dock_node->TabBar->NextSelectedTabId = window->TabId;

        // Bring to front
        BringWindowToFocusFront(focus_front_window);
        if (((window.Flags | focus_front_window.Flags | display_front_window.Flags) & ImGuiWindowFlags_NoBringToFrontOnFocus) == 0)
            BringWindowToDisplayFront(display_front_window);
    }

    public static void FocusTopMostWindowUnderOne(ImGuiWindow under_this_window, ImGuiWindow ignore_window, ImGuiViewport filter_viewport, ImGuiFocusRequestFlags flags)
    {
        ImGuiContext g = GImGui;
        // filter_viewport: Unused in master branch.
        int start_idx = g.WindowsFocusOrder.Size - 1;
        if (under_this_window != null)
        {
            // Aim at root window behind us, if we are in a child window that's our own root (see #4640)
            int offset = -1;
            while ((under_this_window.Flags & ImGuiWindowFlags_ChildWindow) != 0)
            {
                under_this_window = under_this_window.ParentWindow;
                offset = 0;
            }
            start_idx = FindWindowFocusIndex(under_this_window) + offset;
        }
        for (int i = start_idx; i >= 0; i--)
        {
            // We may later decide to test for different NoXXXInputs based on the active navigation input (mouse vs nav) but that may feel more confusing to the user.
            ImGuiWindow window = g.WindowsFocusOrder[i];
            if (window == ignore_window || !window.WasActive)
                continue;
            if ((window.Flags & (ImGuiWindowFlags_NoMouseInputs | ImGuiWindowFlags_NoNavInputs)) != (ImGuiWindowFlags_NoMouseInputs | ImGuiWindowFlags_NoNavInputs))
            {
                FocusWindow(window, flags);
                return;
            }
        }
        FocusWindow(null, flags);
    }

    //-----------------------------------------------------------------------------
    // [SECTION] DRAG AND DROP
    //-----------------------------------------------------------------------------

    public static bool IsDragDropActive()
    {
        ImGuiContext g = GImGui;
        return g.DragDropActive;
    }

    public static void ClearDragDrop()
    {
        ImGuiContext g = GImGui;
        g.DragDropActive = false;
        g.DragDropPayload.Clear();
        g.DragDropAcceptFlagsCurr = ImGuiDragDropFlags_None;
        g.DragDropAcceptIdCurr = g.DragDropAcceptIdPrev = 0;
        g.DragDropAcceptIdCurrRectSurface = float.MaxValue;
        g.DragDropAcceptFrameCount = -1;

        g.DragDropPayloadBufHeap.clear();
        Array.Clear(g.DragDropPayloadBufLocal, 0, g.DragDropPayloadBufLocal.Length);
    }

    public static unsafe bool BeginTooltipHidden()
    {
        ImGuiContext g = GImGui;
        bool ret = Begin("##Tooltip_Hidden", ImGuiWindowFlags_Tooltip | ImGuiWindowFlags_NoInputs | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_AlwaysAutoResize);
        SetWindowHiddenAndSkipItemsForCurrentFrame(g.CurrentWindow);
        return ret;
    }

    // When this returns true you need to: a) call SetDragDropPayload() exactly once, b) you may render the payload visual/description, c) call EndDragDropSource()
    // If the item has an identifier:
    // - This assume/require the item to be activated (typically via ButtonBehavior).
    // - Therefore if you want to use this with a mouse button other than left mouse button, it is up to the item itself to activate with another button.
    // - We then pull and use the mouse button that was used to activate the item and use it to carry on the drag.
    // If the item has no identifier:
    // - Currently always assume left mouse button.
    public static bool BeginDragDropSource(ImGuiDragDropFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        // FIXME-DRAGDROP: While in the common-most "drag from non-zero active id" case we can tell the mouse button,
        // in both SourceExtern and id==0 cases we may requires something else (explicit flags or some heuristic).
        ImGuiMouseButton mouse_button = ImGuiMouseButton_Left;

        bool source_drag_active = false;
        ImGuiID source_id = 0;
        ImGuiID source_parent_id = 0;
        if ((flags & ImGuiDragDropFlags_SourceExtern) == 0)
        {
            source_id = g.LastItemData.ID;
            if (source_id != 0)
            {
                // Common path: items with ID
                if (g.ActiveId != source_id)
                    return false;
                if (g.ActiveIdMouseButton != -1)
                    mouse_button = g.ActiveIdMouseButton;
                if (g.IO.MouseDown[mouse_button] == false || window.SkipItems)
                    return false;
                g.ActiveIdAllowOverlap = false;
            }
            else
            {
                // Uncommon path: items without ID
                if (g.IO.MouseDown[mouse_button] == false || window.SkipItems)
                    return false;
                if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HoveredRect) == 0 && (g.ActiveId == 0 || g.ActiveIdWindow != window))
                    return false;

                // If you want to use BeginDragDropSource() on an item with no unique identifier for interaction, such as Text() or Image(), you need to:
                // A) Read the explanation below, B) Use the ImGuiDragDropFlags_SourceAllowNullID flag.
                if ((flags & ImGuiDragDropFlags_SourceAllowNullID) == 0)
                {
                    Debug.Assert(false);
                    return false;
                }

                // Magic fallback to handle items with no assigned ID, e.g. Text(), Image()
                // We build a throwaway ID based on current ID stack + relative AABB of items in window.
                // THE IDENTIFIER WON'T SURVIVE ANY REPOSITIONING/RESIZING OF THE WIDGET, so if your widget moves your dragging operation will be canceled.
                // We don't need to maintain/call ClearActiveID() as releasing the button will early out this function and trigger !ActiveIdIsAlive.
                // Rely on keeping other window->LastItemXXX fields intact.
                source_id = g.LastItemData.ID = window.GetIDFromRectangle(g.LastItemData.Rect);
                KeepAliveID(source_id);
                bool is_hovered = ItemHoverable(g.LastItemData.Rect, source_id, g.LastItemData.ItemFlags);
                if (is_hovered && g.IO.MouseClicked[mouse_button])
                {
                    SetActiveID(source_id, window);
                    FocusWindow(window);
                }
                if (g.ActiveId == source_id) // Allow the underlying widget to display/return hovered during the mouse release frame, else we would get a flicker.
                    g.ActiveIdAllowOverlap = is_hovered;
            }
            if (g.ActiveId != source_id)
                return false;
            source_parent_id = window.IDStack.back();
            source_drag_active = IsMouseDragging(mouse_button);

            // Disable navigation and key inputs while dragging + cancel existing request if any
            SetActiveIdUsingAllKeyboardKeys();
        }
        else
        {
            // When ImGuiDragDropFlags_SourceExtern is set:
            window = null;
            source_id = ImGuiHash.ImHashStr("#SourceExtern", 0);
            source_drag_active = true;
            mouse_button = g.IO.MouseDown[0] ? 0 : -1;
            KeepAliveID(source_id);
            SetActiveID(source_id, null);
        }

        Debug.Assert(g.DragDropWithinTarget == false); // Can't nest BeginDragDropSource() and BeginDragDropTarget()
        if (!source_drag_active)
            return false;

        // Activate drag and drop
        if (!g.DragDropActive)
        {
            Debug.Assert(source_id != 0);
            ClearDragDrop();
            ImGuiPayload payload = g.DragDropPayload;
            payload.SourceId = source_id;
            payload.SourceParentId = source_parent_id;
            g.DragDropActive = true;
            g.DragDropSourceFlags = flags;
            g.DragDropMouseButton = mouse_button;
            if (payload.SourceId == g.ActiveId)
                g.ActiveIdNoClearOnFocusLoss = true;
        }
        g.DragDropSourceFrameCount = g.FrameCount;
        g.DragDropWithinSource = true;

        if ((flags & ImGuiDragDropFlags_SourceNoPreviewTooltip) == 0)
        {
            // Target can request the Source to not display its tooltip (we use a dedicated flag to make this request explicit)
            // We unfortunately can't just modify the source flags and skip the call to BeginTooltip, as caller may be emitting contents.
            bool ret;
            if (g.DragDropAcceptIdPrev != 0 && (g.DragDropAcceptFlagsPrev & ImGuiDragDropFlags_AcceptNoPreviewTooltip) != 0)
                ret = BeginTooltipHidden();
            else
                ret = BeginTooltip();
            Debug.Assert(ret); // FIXME-NEWBEGIN: If this ever becomes false, we need to Begin("##Hidden", NULL, ImGuiWindowFlags_NoSavedSettings) + SetWindowHiddenAndSkipItemsForCurrentFrame().
        }

        if ((flags & ImGuiDragDropFlags_SourceNoDisableHover) == 0 && (flags & ImGuiDragDropFlags_SourceExtern) == 0)
            g.LastItemData.StatusFlags &= ~ImGuiItemStatusFlags_HoveredRect;

        return true;
    }

    public static void EndDragDropSource()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.DragDropActive);
        Debug.Assert(g.DragDropWithinSource, "Not after a BeginDragDropSource()?");

        if ((g.DragDropSourceFlags & ImGuiDragDropFlags_SourceNoPreviewTooltip) == 0)
            EndTooltip();

        // Discard the drag if have not called SetDragDropPayload()
        if (g.DragDropPayload.DataFrameCount == -1)
            ClearDragDrop();
        g.DragDropWithinSource = false;
    }

    // Use 'cond' to choose to submit payload on drag start or every frame
    public static bool SetDragDropPayload(string type, byte[] data, ulong data_size, ImGuiCond cond = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiPayload payload = g.DragDropPayload;
        if (cond == 0)
            cond = ImGuiCond_Always;

        Debug.Assert(type != null);
        Debug.Assert(type.Length < 32, "Payload type can be at most 32 characters long"); // ImStrlen(type) < IM_COUNTOF(payload.DataType)
        Debug.Assert((data != null && data_size > 0) || (data == null && data_size == 0));
        Debug.Assert(cond == ImGuiCond_Always || cond == ImGuiCond_Once);
        Debug.Assert(payload.SourceId != 0); // Not called between BeginDragDropSource() and EndDragDropSource()

        if (cond == ImGuiCond_Always || payload.DataFrameCount == -1)
        {
            // Copy payload
            payload.DataType = type; // ImStrncpy(payload.DataType, type, IM_COUNTOF(payload.DataType))
            g.DragDropPayloadBufHeap.resize(0);
            if (data_size > (ulong)g.DragDropPayloadBufLocal.Length) // sizeof(g.DragDropPayloadBufLocal) == 16
            {
                // Store in heap
                g.DragDropPayloadBufHeap.resize((int)data_size);
                Array.Copy(data, 0, g.DragDropPayloadBufHeap.Data, 0, (int)data_size);
                payload.Data = g.DragDropPayloadBufHeap.Data;
            }
            else if (data_size > 0)
            {
                // Store locally
                Array.Clear(g.DragDropPayloadBufLocal, 0, g.DragDropPayloadBufLocal.Length);
                payload.Data = g.DragDropPayloadBufLocal;
                Array.Copy(data, 0, payload.Data, 0, (int)data_size);
            }
            else
            {
                payload.Data = null;
            }
            payload.DataSize = (int)data_size;
        }
        payload.DataFrameCount = g.FrameCount;

        // Return whether the payload has been accepted
        return (g.DragDropAcceptFrameCount == g.FrameCount) || (g.DragDropAcceptFrameCount == g.FrameCount - 1);
    }

    public static bool BeginDragDropTargetCustom(ImRect bb, ImGuiID id)
    {
        ImGuiContext g = GImGui;
        if (!g.DragDropActive)
            return false;

        ImGuiWindow window = g.CurrentWindow;
        ImGuiWindow hovered_window = g.HoveredWindowUnderMovingWindow;
        if (hovered_window == null || window.RootWindowDockTree != hovered_window.RootWindowDockTree)
            return false;
        Debug.Assert(id != 0);
        if (!IsMouseHoveringRect(bb.Min, bb.Max) || (id == g.DragDropPayload.SourceId))
            return false;
        if (window.SkipItems)
            return false;

        Debug.Assert(g.DragDropWithinTarget == false && g.DragDropWithinSource == false); // Can't nest BeginDragDropSource() and BeginDragDropTarget()
        g.DragDropTargetRect = bb;
        g.DragDropTargetClipRect = window.ClipRect; // May want to be overridden by user depending on use case?
        g.DragDropTargetId = id;
        g.DragDropTargetFullViewport = 0;
        g.DragDropWithinTarget = true;
        return true;
    }

    // Typical usage would be:
    //   if (!ImGui.IsWindowHovered(ImGuiHoveredFlags_AnyWindow | ImGuiHoveredFlags_AllowWhenBlockedByActiveItem))
    //       if (ImGui.BeginDragDropTargetViewport(ImGui.GetMainViewport(), null))
    // But we are leaving the hover test to the caller for maximum flexibility.
    public static bool BeginDragDropTargetViewport(ImGuiViewport viewport, ImRect? p_bb = null)
    {
        ImGuiContext g = GImGui;
        if (!g.DragDropActive)
            return false;

        ImRect bb = p_bb.HasValue ? p_bb.Value : ((ImGuiViewportP)viewport).GetWorkRect();
        ImGuiID id = viewport.ID;
        if (!IsMouseHoveringRect(bb.Min, bb.Max, false) || (id == g.DragDropPayload.SourceId))
            return false;

        Debug.Assert(g.DragDropWithinTarget == false && g.DragDropWithinSource == false); // Can't nest BeginDragDropSource() and BeginDragDropTarget()
        g.DragDropTargetRect = bb;
        g.DragDropTargetClipRect = bb;
        g.DragDropTargetId = id;
        g.DragDropTargetFullViewport = id;
        g.DragDropWithinTarget = true;
        return true;
    }

    // We don't use BeginDragDropTargetCustom() and duplicate its code because:
    // 1) we use LastItemData's ImGuiItemStatusFlags_HoveredRect which handles items that push a temporarily clip rectangle in their code. Calling BeginDragDropTargetCustom(LastItemRect) would not handle them.
    // 2) and it's faster. as this code may be very frequently called, we want to early out as fast as we can.
    // Also note how the HoveredWindow test is positioned differently in both functions (in both functions we optimize for the cheapest early out case)
    public static bool BeginDragDropTarget()
    {
        ImGuiContext g = GImGui;
        if (!g.DragDropActive)
            return false;

        ImGuiWindow window = g.CurrentWindow;
        if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HoveredRect) == 0)
            return false;
        ImGuiWindow hovered_window = g.HoveredWindowUnderMovingWindow;
        if (hovered_window == null || window.RootWindowDockTree != hovered_window.RootWindowDockTree || window.SkipItems)
            return false;

        ImRect display_rect = (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HasDisplayRect) != 0 ? g.LastItemData.DisplayRect : g.LastItemData.Rect;
        ImGuiID id = g.LastItemData.ID;
        if (id == 0)
        {
            id = window.GetIDFromRectangle(display_rect);
            KeepAliveID(id);
        }
        if (g.DragDropPayload.SourceId == id)
            return false;

        Debug.Assert(g.DragDropWithinTarget == false && g.DragDropWithinSource == false); // Can't nest BeginDragDropSource() and BeginDragDropTarget()
        g.DragDropTargetRect = display_rect;
        g.DragDropTargetClipRect = (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HasClipRect) != 0 ? g.LastItemData.ClipRect : window.ClipRect;
        g.DragDropTargetId = id;
        g.DragDropWithinTarget = true;
        return true;
    }

    public static bool IsDragDropPayloadBeingAccepted()
    {
        ImGuiContext g = GImGui;
        return g.DragDropActive && g.DragDropAcceptIdPrev != 0;
    }

    public static ImGuiPayload AcceptDragDropPayload(string type, ImGuiDragDropFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiPayload payload = g.DragDropPayload;
        Debug.Assert(g.DragDropActive);                        // Not called between BeginDragDropTarget() and EndDragDropTarget() ?
        Debug.Assert(payload.DataFrameCount != -1);            // Forgot to call EndDragDropTarget() ?
        if (type != null && !payload.IsDataType(type))
            return null;

        // Accept smallest drag target bounding box, this allows us to nest drag targets conveniently without ordering constraints.
        // NB: We currently accept NULL id as target. However, overlapping targets requires a unique ID to function!
        bool was_accepted_previously = (g.DragDropAcceptIdPrev == g.DragDropTargetId);
        ImRect r = g.DragDropTargetRect;
        float r_surface = r.GetWidth() * r.GetHeight();
        if (r_surface > g.DragDropAcceptIdCurrRectSurface)
            return null;

        g.DragDropAcceptFlagsCurr = flags;
        g.DragDropAcceptIdCurr = g.DragDropTargetId;
        g.DragDropAcceptIdCurrRectSurface = r_surface;
        //IMGUI_DEBUG_LOG("AcceptDragDropPayload(): %08X: accept\n", g.DragDropTargetId);

        // Render default drop visuals
        payload.Preview = was_accepted_previously;
        flags |= (g.DragDropSourceFlags & ImGuiDragDropFlags_AcceptNoDrawDefaultRect); // Source can also inhibit the preview (useful for external sources that live for 1 frame)
        bool draw_target_rect = payload.Preview && (flags & ImGuiDragDropFlags_AcceptNoDrawDefaultRect) == 0;
        if (draw_target_rect && g.DragDropTargetFullViewport != 0)
        {
            ImRect bb = g.DragDropTargetRect;
            bb.Expand(-3.5f);
            RenderDragDropTargetRectEx(GetForegroundDrawList(), bb, g.Style.DragDropTargetRounding);
        }
        else if (draw_target_rect)
        {
            RenderDragDropTargetRectForItem(r);
        }

        g.DragDropAcceptFrameCount = g.FrameCount;
        if ((g.DragDropSourceFlags & ImGuiDragDropFlags_SourceExtern) != 0 && g.DragDropMouseButton == -1)
            payload.Delivery = was_accepted_previously && (g.DragDropSourceFrameCount < g.FrameCount);
        else
            payload.Delivery = was_accepted_previously && !IsMouseDown(g.DragDropMouseButton); // For extern drag sources affecting OS window focus, it's easier to just test !IsMouseDown() instead of IsMouseReleased()
        if (!payload.Delivery && (flags & ImGuiDragDropFlags_AcceptBeforeDelivery) == 0)
            return null;

        return payload;
    }

    // FIXME-STYLE FIXME-DRAGDROP: Settle on a proper default visuals for drop target.
    public static void RenderDragDropTargetRectForItem(ImRect bb)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImRect bb_display = bb;
        bb_display.ClipWith(g.DragDropTargetClipRect); // Clip THEN expand so we have a way to visualize that target is not entirely visible.
        bb_display.Expand(g.Style.DragDropTargetPadding);
        bool push_clip_rect = !window.ClipRect.Contains(bb_display);
        if (push_clip_rect)
            window.DrawList.PushClipRectFullScreen();
        RenderDragDropTargetRectEx(window.DrawList, bb_display, g.Style.DragDropTargetRounding);
        if (push_clip_rect)
            window.DrawList.PopClipRect();
    }

    public static void RenderDragDropTargetRectEx(ImDrawList draw_list, ImRect bb, float rounding)
    {
        ImGuiContext g = GImGui;
        draw_list.AddRectFilled(bb.Min, bb.Max, GetColorU32(ImGuiCol_DragDropTargetBg), rounding, 0);
        draw_list.AddRect(bb.Min, bb.Max, GetColorU32(ImGuiCol_DragDropTarget), rounding, g.Style.DragDropTargetBorderSize);
    }

    public static ImGuiPayload GetDragDropPayload()
    {
        ImGuiContext g = GImGui;
        return (g.DragDropActive && g.DragDropPayload.DataFrameCount != -1) ? g.DragDropPayload : null;
    }

    public static void EndDragDropTarget()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.DragDropActive);
        Debug.Assert(g.DragDropWithinTarget);
        g.DragDropWithinTarget = false;

        // Clear drag and drop state payload right after delivery
        if (g.DragDropPayload.Delivery)
            ClearDragDrop();
    }
}
