// Port of imgui.cpp [SECTION] MAIN CODE (part 2): ImGui::Begin()/End(), item/disabled stacks,
// window queries and setters, SetNextWindow***() family, focus scopes, misc helpers.
// C++ source range: imgui.cpp lines ~7533-9058.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    // C++: bool ImGui::Begin(const char* name, bool* p_open, ImGuiWindowFlags flags)
    // C#-friendly Begin overloads (C++ bool* p_open becomes a ref overload or omitted).
    public static unsafe bool Begin(string name, ImGuiWindowFlags flags = 0) => BeginEx(name, null, flags);

    public static unsafe bool Begin(string name, ref bool p_open, ImGuiWindowFlags flags = 0)
    {
        fixed (bool* p = &p_open)
            return BeginEx(name, p, flags);
    }

    private static unsafe bool BeginEx(string name, bool* p_open, ImGuiWindowFlags flags)
    {
        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        Debug.Assert(name != null && name.Length != 0);     // Window name required
        Debug.Assert(g.WithinFrameScope);                   // Forgot to call ImGui::NewFrame()
        Debug.Assert(g.FrameCountEnded != g.FrameCount);    // Called ImGui::Render() or ImGui::EndFrame() and haven't called ImGui::NewFrame() again yet

        // Find or create
        ImGuiWindow window = FindWindowByName(name);
        bool window_just_created = (window == null);
        if (window_just_created)
            window = CreateNewWindow(name, flags);

        // [DEBUG] Debug break requested by user
        if (g.DebugBreakInWindow == window.ID)
            Debugger.Break();

        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasWindowFlags) != 0)
            flags = (flags | g.NextWindowData.WindowFlagsSet) & ~g.NextWindowData.WindowFlagsClear;

        // Automatically disable manual moving/resizing when NoInputs is set
        if ((flags & ImGuiWindowFlags_NoInputs) == ImGuiWindowFlags_NoInputs)
            flags |= ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize;

        int current_frame = g.FrameCount;
        bool first_begin_of_the_frame = (window.LastFrameActive != current_frame);
        window.IsFallbackWindow = (g.CurrentWindowStack.Size == 0 && g.WithinFrameScopeWithImplicitWindow);

        // Update the Appearing flag
        bool window_just_activated_by_user = (window.LastFrameActive < current_frame - 1);   // Not using !WasActive because the implicit "Debug" window would always toggle off->on
        if ((flags & ImGuiWindowFlags_Popup) != 0)
        {
            ImGuiPopupData popup_ref = g.OpenPopupStack[g.BeginPopupStack.Size];
            window_just_activated_by_user |= (window.PopupId != popup_ref.PopupId); // We recycle popups so treat window as activated if popup id changed
            window_just_activated_by_user |= (window != popup_ref.Window);
        }
        // Update the Appearing flag (note: the BeginDocked() path may also set this to true later)

        // Update Flags, LastFrameActive, BeginOrderXXX fields
        bool window_was_appearing = window.Appearing;
        if (first_begin_of_the_frame)
        {
            UpdateWindowInFocusOrderList(window, window_just_created, flags);
            window.Appearing = window_just_activated_by_user;
            if (window.Appearing)
                SetWindowConditionAllowFlags(window, ImGuiCond_Appearing, true);
            window.FlagsPreviousFrame = window.Flags;
            window.Flags = flags;
            window.ChildFlags = (g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasChildFlags) != 0 ? g.NextWindowData.ChildFlagsSet : 0;
            window.LastFrameActive = current_frame;
            window.LastTimeActive = (float)g.Time;
            window.BeginOrderWithinParent = 0;
            window.BeginOrderWithinContext = (short)(g.WindowsActiveCount++);
        }
        else
        {
            flags = window.Flags;
        }

        // Docking
        // (NB: during the frame dock nodes are created, it is possible that (window->DockIsActive == false) even though (window->DockNode->Windows.Size > 1)
        Debug.Assert(window.DockNode == null || window.DockNodeAsHost == null); // Cannot be both
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasDock) != 0)
            SetWindowDock(window, g.NextWindowData.DockId, g.NextWindowData.DockCond);
        if (first_begin_of_the_frame)
        {
            bool has_dock_node = (window.DockId != 0 || window.DockNode != null);
            bool new_auto_dock_node = !has_dock_node && GetWindowAlwaysWantOwnTabBar(window);
            bool dock_node_was_visible = window.DockNodeIsVisible;
            bool dock_tab_was_visible = window.DockTabIsVisible;
            window.DockIsActive = window.DockNodeIsVisible = window.DockTabIsVisible = false;

            if (has_dock_node || new_auto_dock_node)
            {
                BeginDocked(window, p_open);
                flags = window.Flags;
                if (window.DockIsActive)
                {
                    Debug.Assert(window.DockNode != null);
                    g.NextWindowData.HasFlags &= ~ImGuiNextWindowDataFlags_HasSizeConstraint; // Docking currently override constraints
                }

                // Amend the Appearing flag
                if (window.DockTabIsVisible && !dock_tab_was_visible && dock_node_was_visible && !window.Appearing && !window_was_appearing)
                {
                    window.Appearing = true;
                    SetWindowConditionAllowFlags(window, ImGuiCond_Appearing, true);
                }
            }
        }

        // Parent window is latched only on the first call to Begin() of the frame, so further append-calls can be done from a different window stack
        ImGuiWindow parent_window_in_stack = (window.DockIsActive && window.DockNode.HostWindow != null) ? window.DockNode.HostWindow : (g.CurrentWindowStack.empty() ? null : g.CurrentWindowStack.back().Window);
        ImGuiWindow parent_window = first_begin_of_the_frame ? (((flags & (ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_Popup | ImGuiWindowFlags_Tooltip)) != 0) ? parent_window_in_stack : null) : window.ParentWindow;
        Debug.Assert(parent_window != null || (flags & ImGuiWindowFlags_ChildWindow) == 0);

        // We allow window memory to be compacted so recreate the base stack when needed.
        if (window.IDStack.Size == 0)
            window.IDStack.push_back(window.ID);

        // Add to stack
        g.CurrentWindow = window;
        g.CurrentWindowStack.resize(g.CurrentWindowStack.Size + 1);
        ref ImGuiWindowStackData window_stack_data = ref g.CurrentWindowStack.back_ref();
        window_stack_data.Window = window;
        window_stack_data.ParentLastItemDataBackup = g.LastItemData;
        window_stack_data.DisabledOverrideReenable = (flags & ImGuiWindowFlags_Tooltip) != 0 && (g.CurrentItemFlags & ImGuiItemFlags_Disabled) != 0;
        window_stack_data.DisabledOverrideReenableAlphaBackup = 0.0f;
        ErrorRecoveryStoreState(ref window_stack_data.StackSizesInBegin);
        g.StackSizesInBeginForCurrentWindow = window_stack_data.StackSizesInBegin;
        if ((flags & ImGuiWindowFlags_ChildMenu) != 0)
            g.BeginMenuDepth++;

        // Update ->RootWindow and others pointers (before any possible call to FocusWindow)
        if (first_begin_of_the_frame)
        {
            UpdateWindowParentAndRootLinks(window, flags, parent_window);
            window.ParentWindowInBeginStack = parent_window_in_stack;

            // Focus route
            // There's little point to expose a flag to set this: because the interesting cases won't be using parent_window_in_stack,
            // Use for e.g. linking a tool window in a standalone viewport to a document window, regardless of their Begin() stack parenting. (#6798)
            window.ParentWindowForFocusRoute = (window.RootWindow != window) ? parent_window_in_stack : null;
            if (window.ParentWindowForFocusRoute == null && window.DockNode != null)
                if ((window.DockNode.MergedFlags & ImGuiDockNodeFlags_DockedWindowsInFocusRoute) != 0)
                    window.ParentWindowForFocusRoute = window.DockNode.HostWindow;

            // Override with SetNextWindowClass() field or direct call to SetWindowParentWindowForFocusRoute()
            if (window.WindowClass.FocusRouteParentWindowId != 0)
            {
                window.ParentWindowForFocusRoute = FindWindowByID(window.WindowClass.FocusRouteParentWindowId);
                Debug.Assert(window.ParentWindowForFocusRoute != null); // Invalid value for FocusRouteParentWindowId.
            }

            // Inherit SetWindowFontScale() from parent until we fix this system...
            window.FontWindowScaleParents = parent_window != null ? parent_window.FontWindowScaleParents * parent_window.FontWindowScale : 1.0f;
        }

        // Add to focus scope stack
        PushFocusScope((window.ChildFlags & ImGuiChildFlags_NavFlattened) != 0 ? g.CurrentFocusScopeId : window.ID);
        window.NavRootFocusScopeId = g.CurrentFocusScopeId;

        // Add to popup stacks: update OpenPopupStack[] data, push to BeginPopupStack[]
        if ((flags & ImGuiWindowFlags_Popup) != 0)
        {
            ImGuiPopupData popup_ref = g.OpenPopupStack[g.BeginPopupStack.Size];
            popup_ref.Window = window;
            popup_ref.ParentNavLayer = parent_window_in_stack.DC.NavLayerCurrent;
            g.OpenPopupStack[g.BeginPopupStack.Size] = popup_ref; // C++ binds ImGuiPopupData& — write the copy back into the slot
            g.BeginPopupStack.push_back(popup_ref);
            window.PopupId = popup_ref.PopupId;
        }

        // Process SetNextWindow***() calls
        // (FIXME: Consider splitting the HasXXX flags into X/Y components)
        bool window_pos_set_by_api = false;
        bool window_size_x_set_by_api = false, window_size_y_set_by_api = false;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasPos) != 0)
        {
            window_pos_set_by_api = (window.SetWindowPosAllowFlags & g.NextWindowData.PosCond) != 0;
            if (window_pos_set_by_api && ImUtil.ImLengthSqr(g.NextWindowData.PosPivotVal) > 0.00001f)
            {
                // May be processed on the next frame if this is our first frame and we are measuring size
                // FIXME: Look into removing the branch so everything can go through this same code path for consistency.
                window.SetWindowPosVal = g.NextWindowData.PosVal;
                window.SetWindowPosPivot = g.NextWindowData.PosPivotVal;
                window.SetWindowPosAllowFlags = (byte)(window.SetWindowPosAllowFlags & ~(ImGuiCond_Once | ImGuiCond_FirstUseEver | ImGuiCond_Appearing));
            }
            else
            {
                SetWindowPos(window, g.NextWindowData.PosVal, g.NextWindowData.PosCond);
            }
        }
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSize) != 0)
        {
            window_size_x_set_by_api = (window.SetWindowSizeAllowFlags & g.NextWindowData.SizeCond) != 0 && (g.NextWindowData.SizeVal.x > 0.0f);
            window_size_y_set_by_api = (window.SetWindowSizeAllowFlags & g.NextWindowData.SizeCond) != 0 && (g.NextWindowData.SizeVal.y > 0.0f);
            if ((window.ChildFlags & ImGuiChildFlags_ResizeX) != 0 && (window.SetWindowSizeAllowFlags & ImGuiCond_FirstUseEver) == 0) // Axis-specific conditions for BeginChild()
                g.NextWindowData.SizeVal.x = window.SizeFull.x;
            if ((window.ChildFlags & ImGuiChildFlags_ResizeY) != 0 && (window.SetWindowSizeAllowFlags & ImGuiCond_FirstUseEver) == 0)
                g.NextWindowData.SizeVal.y = window.SizeFull.y;
            SetWindowSize(window, g.NextWindowData.SizeVal, g.NextWindowData.SizeCond);
        }
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasScroll) != 0)
        {
            if (g.NextWindowData.ScrollVal.x >= 0.0f)
            {
                window.ScrollTarget.x = g.NextWindowData.ScrollVal.x;
                window.ScrollTargetCenterRatio.x = 0.0f;
            }
            if (g.NextWindowData.ScrollVal.y >= 0.0f)
            {
                window.ScrollTarget.y = g.NextWindowData.ScrollVal.y;
                window.ScrollTargetCenterRatio.y = 0.0f;
            }
        }
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasContentSize) != 0)
            window.ContentSizeExplicit = g.NextWindowData.ContentSizeVal;
        else if (first_begin_of_the_frame)
            window.ContentSizeExplicit = new ImVec2(0.0f, 0.0f);
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasWindowClass) != 0)
            window.WindowClass = g.NextWindowData.WindowClass;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasCollapsed) != 0)
            SetWindowCollapsed(window, g.NextWindowData.CollapsedVal, g.NextWindowData.CollapsedCond);
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasFocus) != 0)
            FocusWindow(window);
        if (window.Appearing)
            SetWindowConditionAllowFlags(window, ImGuiCond_Appearing, false);

        // [EXPERIMENTAL] Skip Refresh mode
        UpdateWindowSkipRefresh(window);

        // Nested root windows (typically tooltips) override disabled state
        if (window_stack_data.DisabledOverrideReenable && window.RootWindow == window)
            BeginDisabledOverrideReenable();

        // We intentionally set g.CurrentWindow to NULL to prevent usage until when the viewport is set, then will call SetCurrentWindow()
        g.CurrentWindow = null;

        // When reusing window again multiple times a frame, just append content (don't need to setup again)
        if (first_begin_of_the_frame && !window.SkipRefresh)
        {
            // Initialize
            bool window_is_child_tooltip = (flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_Tooltip) != 0; // FIXME-WIP: Undocumented behavior of Child+Tooltip for pinned tooltip (#1345)
            bool window_just_appearing_after_hidden_for_resize = (window.HiddenFramesCannotSkipItems > 0);
            window.Active = true;
            window.HasCloseButton = (p_open != null);
            window.ClipRect = new ImRect(-float.MaxValue, -float.MaxValue, +float.MaxValue, +float.MaxValue);
            window.IDStack.resize(1);
            window.DrawList._ResetForNewFrame();
            window.DC.CurrentTableIdx = -1;
            if ((flags & ImGuiWindowFlags_DockNodeHost) != 0)
            {
                window.DrawList._Splitter.Split(window.DrawList, 2); // C++: ChannelsSplit(2)
                window.DrawList._Splitter.SetCurrentChannel(window.DrawList, DOCKING_HOST_DRAW_CHANNEL_FG); // Render decorations on channel 1 as we will render the backgrounds manually later
            }

            // Restore buffer capacity when woken from a compacted state, to avoid
            if (window.MemoryCompacted)
                GcAwakeTransientWindowBuffers(window);

            // Update stored window name when it changes (which can _only_ happen with the "###" operator, so the ID would stay unchanged).
            // The title bar always display the 'name' parameter, so we only update the string storage if it needs to be visible to the end-user elsewhere.
            bool window_title_visible_elsewhere = false;
            if ((window.Viewport != null && window.Viewport.Window == window) || window.DockIsActive)
                window_title_visible_elsewhere = true;
            else if (g.NavWindowingListWindow != null && g.NavWindowingListWindow.WasActive && (flags & ImGuiWindowFlags_NoNavFocus) == 0)   // Window titles visible when using Ctrl+Tab
                window_title_visible_elsewhere = true;
            else if ((flags & ImGuiWindowFlags_ChildMenu) != 0)
                window_title_visible_elsewhere = true;
            if ((window_title_visible_elsewhere || window_just_activated_by_user) && !window_just_created && name != window.Name)
            {
                // C++: window->Name = ImStrdupcpy(window->Name, &buf_len, name); strings are managed in C#.
                window.Name = name;
                window.NameBufLen = name.Length + 1;
            }

            // UPDATE CONTENTS SIZE, UPDATE HIDDEN STATUS

            // Update contents size from last frame for auto-fitting (or use explicit size)
            CalcWindowContentSizes(window, out window.ContentSize, out window.ContentSizeIdeal);
            if (window.HiddenFramesCanSkipItems > 0)
                window.HiddenFramesCanSkipItems--;
            if (window.HiddenFramesCannotSkipItems > 0)
                window.HiddenFramesCannotSkipItems--;
            if (window.HiddenFramesForRenderOnly > 0)
                window.HiddenFramesForRenderOnly--;

            // Hide new windows for one frame until they calculate their size
            if (window_just_created && (!window_size_x_set_by_api || !window_size_y_set_by_api))
                window.HiddenFramesCannotSkipItems = 1;

            // Hide popup/tooltip window when re-opening while we measure size (because we recycle the windows)
            // We reset Size/ContentSize for reappearing popups/tooltips early in this function, so further code won't be tempted to use the old size.
            if (window_just_activated_by_user && (flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_Tooltip)) != 0)
            {
                window.HiddenFramesCannotSkipItems = 1;
                if ((flags & ImGuiWindowFlags_AlwaysAutoResize) != 0)
                {
                    if (!window_size_x_set_by_api)
                        window.Size.x = window.SizeFull.x = 0.0f;
                    if (!window_size_y_set_by_api)
                        window.Size.y = window.SizeFull.y = 0.0f;
                    window.ContentSize = window.ContentSizeIdeal = new ImVec2(0.0f, 0.0f);
                }
            }

            // SELECT VIEWPORT
            // We need to do this before using any style/font sizes, as viewport with a different DPI may affect font sizes.

            WindowSelectViewport(window);
            SetCurrentViewport(window, window.Viewport);
            SetCurrentWindow(window);
            flags = window.Flags;

            // LOCK BORDER SIZE AND PADDING FOR THE FRAME (so that altering them doesn't cause inconsistencies)
            // We read Style data after the call to UpdateSelectWindowViewport() which might be swapping the style.

            if (!window.DockIsActive && (flags & ImGuiWindowFlags_ChildWindow) != 0)
                window.WindowBorderSize = style.ChildBorderSize;
            else
                window.WindowBorderSize = ((flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_Tooltip)) != 0 && (flags & ImGuiWindowFlags_Modal) == 0) ? style.PopupBorderSize : style.WindowBorderSize;
            window.WindowPadding = style.WindowPadding;
            if (!window.DockIsActive && (flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_Popup) == 0 && (window.ChildFlags & ImGuiChildFlags_AlwaysUseWindowPadding) == 0 && window.WindowBorderSize == 0.0f)
                window.WindowPadding = new ImVec2(0.0f, (flags & ImGuiWindowFlags_MenuBar) != 0 ? style.WindowPadding.y : 0.0f);

            // Lock menu offset so size calculation can use it as menu-bar windows need a minimum size.
            window.DC.MenuBarOffset.x = ImMath.ImMax(ImMath.ImMax(window.WindowPadding.x, style.ItemSpacing.x), g.NextWindowData.MenuBarOffsetMinVal.x);
            window.DC.MenuBarOffset.y = g.NextWindowData.MenuBarOffsetMinVal.y;
            window.TitleBarHeight = (flags & ImGuiWindowFlags_NoTitleBar) != 0 ? 0.0f : g.FontSize + g.Style.FramePadding.y * 2.0f;
            window.MenuBarHeight = (flags & ImGuiWindowFlags_MenuBar) != 0 ? window.DC.MenuBarOffset.y + g.FontSize + g.Style.FramePadding.y * 2.0f : 0.0f;
            window.FontRefSize = g.FontSize; // Lock this to discourage calling window->CalcFontSize() outside of current window.

            // Depending on condition we use previous or current window size to compare against contents size to decide if a scrollbar should be visible.
            // Those flags will be altered further down in the function depending on more conditions.
            bool use_current_size_for_scrollbar_x = window_just_created;
            bool use_current_size_for_scrollbar_y = window_just_created;
            if (window_size_x_set_by_api && window.ContentSizeExplicit.x != 0.0f)
                use_current_size_for_scrollbar_x = true;
            if (window_size_y_set_by_api && window.ContentSizeExplicit.y != 0.0f) // #7252
                use_current_size_for_scrollbar_y = true;

            // Collapse window by double-clicking on title bar
            // At this point we don't have a clipping rectangle setup yet, so we can use the title bar area for hit detection and drawing
            if ((flags & ImGuiWindowFlags_NoTitleBar) == 0 && (flags & ImGuiWindowFlags_NoCollapse) == 0 && !window.DockIsActive)
            {
                // We don't use a regular button+id to test for double-click on title bar (mostly due to legacy reason, could be fixed),
                // so verify that we don't have items over the title bar.
                ImRect title_bar_rect_dblclick = window.TitleBarRect();
                if (g.HoveredWindow == window && g.HoveredId == 0 && g.HoveredIdPreviousFrame == 0 && g.ActiveId == 0 && IsMouseHoveringRect(title_bar_rect_dblclick.Min, title_bar_rect_dblclick.Max))
                    if (g.IO.MouseClickedCount[0] == 2 && GetKeyOwner(ImGuiKey_MouseLeft) == ImGuiKeyOwner_NoOwner)
                    {
                        window.WantCollapseToggle = true;
                        SetKeyOwner(ImGuiKey_MouseLeft, window.MoveId); // Claim input the same way ButtonBehavior() does. Prevent a same-frame move from triggering other items. (#9439)
                    }
                if (window.WantCollapseToggle)
                {
                    window.Collapsed = !window.Collapsed;
                    if (!window.Collapsed)
                        use_current_size_for_scrollbar_y = true;
                    MarkIniSettingsDirty(window);
                }
            }
            else
            {
                window.Collapsed = false;
            }
            window.WantCollapseToggle = false;

            // SIZE

            // Outer Decoration Sizes
            // (we need to clear ScrollbarSize immediately as CalcWindowAutoFitSize() needs it and can be called from other locations).
            ImVec2 scrollbar_sizes_from_last_frame = window.ScrollbarSizes;
            window.DecoOuterSizeX1 = 0.0f;
            window.DecoOuterSizeX2 = 0.0f;
            window.DecoOuterSizeY1 = window.TitleBarHeight + window.MenuBarHeight;
            window.DecoOuterSizeY2 = 0.0f;
            window.ScrollbarSizes = new ImVec2(0.0f, 0.0f);

            // Calculate auto-fit size, handle automatic resize
            // - Using SetNextWindowSize() overrides ImGuiWindowFlags_AlwaysAutoResize, so it can be used on tooltips/popups, etc.
            // - We still process initial auto-fit on collapsed windows to get a window width, but otherwise don't honor ImGuiWindowFlags_AlwaysAutoResize when collapsed.
            // - Auto-fit may only grow window during the first few frames.
            {
                bool size_auto_fit_x_always = !window_size_x_set_by_api && (flags & ImGuiWindowFlags_AlwaysAutoResize) != 0 && !window.Collapsed;
                bool size_auto_fit_y_always = !window_size_y_set_by_api && (flags & ImGuiWindowFlags_AlwaysAutoResize) != 0 && !window.Collapsed;
                bool size_auto_fit_x_current = !window_size_x_set_by_api && (window.AutoFitFramesX > 0);
                bool size_auto_fit_y_current = !window_size_y_set_by_api && (window.AutoFitFramesY > 0);
                int size_auto_fit_mask = 0;
                if (size_auto_fit_x_always || size_auto_fit_x_current)
                    size_auto_fit_mask |= (1 << ImGuiAxis_X);
                if (size_auto_fit_y_always || size_auto_fit_y_current)
                    size_auto_fit_mask |= (1 << ImGuiAxis_Y);
                ImVec2 size_auto_fit = CalcWindowAutoFitSize(window, window.ContentSizeIdeal, size_auto_fit_mask);

                ImVec2 old_size = window.SizeFull;
                if (size_auto_fit_x_always || size_auto_fit_x_current)
                {
                    if (size_auto_fit_x_always)
                        window.SizeFull.x = size_auto_fit.x;
                    else
                        window.SizeFull.x = window.AutoFitOnlyGrows ? ImMath.ImMax(window.SizeFull.x, size_auto_fit.x) : size_auto_fit.x;
                    use_current_size_for_scrollbar_x = true;
                }
                if (size_auto_fit_y_always || size_auto_fit_y_current)
                {
                    if (size_auto_fit_y_always)
                        window.SizeFull.y = size_auto_fit.y;
                    else
                        window.SizeFull.y = window.AutoFitOnlyGrows ? ImMath.ImMax(window.SizeFull.y, size_auto_fit.y) : size_auto_fit.y;
                    use_current_size_for_scrollbar_y = true;
                }
                if (old_size.x != window.SizeFull.x || old_size.y != window.SizeFull.y)
                    MarkIniSettingsDirty(window);
            }

            // Apply minimum/maximum window size constraints and final size
            window.SizeFull = CalcWindowSizeAfterConstraint(window, window.SizeFull);
            window.Size = window.Collapsed && (flags & ImGuiWindowFlags_ChildWindow) == 0 ? window.TitleBarRect().GetSize() : window.SizeFull;

            // POSITION

            // Popup latch its initial position, will position itself when it appears next frame
            if (window_just_activated_by_user)
            {
                window.AutoPosLastDirection = ImGuiDir_None;
                if ((flags & ImGuiWindowFlags_Popup) != 0 && (flags & ImGuiWindowFlags_Modal) == 0 && !window_pos_set_by_api) // FIXME: BeginPopup() could use SetNextWindowPos()
                    window.Pos = g.BeginPopupStack.back().OpenPopupPos;
            }

            // Position child window
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0)
            {
                Debug.Assert(parent_window != null && parent_window.Active);
                window.BeginOrderWithinParent = (short)parent_window.DC.ChildWindows.Size;
                parent_window.DC.ChildWindows.push_back(window);
                if ((flags & ImGuiWindowFlags_Popup) == 0 && !window_pos_set_by_api && !window_is_child_tooltip)
                    window.Pos = parent_window.DC.CursorPos;
            }

            bool window_pos_with_pivot = (window.SetWindowPosVal.x != float.MaxValue && window.HiddenFramesCannotSkipItems == 0);
            if (window_pos_with_pivot)
                SetWindowPos(window, window.SetWindowPosVal - window.Size * window.SetWindowPosPivot, 0); // Position given a pivot (e.g. for centering)
            else if ((flags & ImGuiWindowFlags_ChildMenu) != 0)
                window.Pos = FindBestWindowPosForPopup(window);
            else if ((flags & ImGuiWindowFlags_Popup) != 0 && !window_pos_set_by_api && window_just_appearing_after_hidden_for_resize)
                window.Pos = FindBestWindowPosForPopup(window);
            else if ((flags & ImGuiWindowFlags_Tooltip) != 0 && !window_pos_set_by_api && !window_is_child_tooltip)
                window.Pos = FindBestWindowPosForPopup(window);

            // Late create viewport if we don't fit within our current host viewport.
            if (window.ViewportAllowPlatformMonitorExtend >= 0 && !window.ViewportOwned && (window.Viewport.Flags & ImGuiViewportFlags_IsMinimized) == 0)
                if (!window.Viewport.GetMainRect().Contains(window.Rect()))
                {
                    // This is based on the assumption that the DPI will be known ahead (same as the DPI of the selection done in UpdateSelectWindowViewport)
                    //ImGuiViewport* old_viewport = window->Viewport;
                    window.Viewport = AddUpdateViewport(window, window.ID, window.Pos, window.Size, ImGuiViewportFlags_NoFocusOnAppearing);

                    // FIXME-DPI
                    //IM_ASSERT(old_viewport->DpiScale == window->Viewport->DpiScale); // FIXME-DPI: Something went wrong
                    SetCurrentViewport(window, window.Viewport);
                    SetCurrentWindow(window);
                }

            if (window.ViewportOwned)
                WindowSyncOwnedViewport(window, parent_window_in_stack);

            // Calculate the range of allowed position for that window (to be movable and visible past safe area padding)
            // When clamping to stay visible, we will enforce that window->Pos stays inside of visibility_rect.
            ImRect viewport_rect = window.Viewport.GetMainRect();
            ImRect viewport_work_rect = window.Viewport.GetWorkRect();
            ImVec2 visibility_padding = ImMath.ImMax(style.DisplayWindowPadding, style.DisplaySafeAreaPadding);
            ImRect visibility_rect = new ImRect(viewport_work_rect.Min + visibility_padding, viewport_work_rect.Max - visibility_padding);

            // Clamp position/size so window stays visible within its viewport or monitor
            // Ignore zero-sized display explicitly to avoid losing positions if a window manager reports zero-sized window when initializing or minimizing.
            // FIXME: Similar to code in GetWindowAllowedExtentRect()
            if (!window_pos_set_by_api && (flags & ImGuiWindowFlags_ChildWindow) == 0)
            {
                if (!window.ViewportOwned && viewport_rect.GetWidth() > 0.0f && viewport_rect.GetHeight() > 0.0f)
                {
                    ClampWindowPos(window, visibility_rect);
                }
                else if (window.ViewportOwned && g.PlatformIO.Monitors.Size > 0)
                {
                    if (g.MovingWindow != null && window.RootWindowDockTree == g.MovingWindow.RootWindowDockTree)
                    {
                        // While moving windows we allow them to straddle monitors (#7299, #3071)
                        visibility_rect = g.PlatformMonitorsFullWorkRect;
                    }
                    else
                    {
                        // When not moving ensure visible in its monitor
                        // Lost windows (e.g. a monitor disconnected) will naturally moved to the fallback/dummy monitor aka the main viewport.
                        ImGuiPlatformMonitor monitor = GetViewportPlatformMonitor(window.Viewport);
                        visibility_rect = new ImRect(monitor.WorkPos, monitor.WorkPos + monitor.WorkSize);
                    }
                    visibility_rect.Expand(-visibility_padding);
                    ClampWindowPos(window, visibility_rect);
                }
            }
            window.Pos = ImMath.ImTrunc(window.Pos);

            // Lock window rounding for the frame (so that altering them doesn't cause inconsistencies)
            // Large values tend to lead to variety of artifacts and are not recommended.
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && !window.DockIsActive)
                window.WindowRounding = style.ChildRounding;
            else if (window.RootWindowDockTree.ViewportOwned)
                window.WindowRounding = 0.0f;
            else
                window.WindowRounding = ((flags & ImGuiWindowFlags_Popup) != 0 && (flags & ImGuiWindowFlags_Modal) == 0) ? style.PopupRounding : style.WindowRounding;

            // For windows with title bar or menu bar, we clamp to FrameHeight(FontSize + FramePadding.y * 2.0f) to completely hide artifacts.
            //if ((window->Flags & ImGuiWindowFlags_MenuBar) || !(window->Flags & ImGuiWindowFlags_NoTitleBar))
            //    window->WindowRounding = ImMin(window->WindowRounding, g.FontSize + style.FramePadding.y * 2.0f);

            // Apply window focus (new and reactivated windows are moved to front)
            bool want_focus = false;
            if (window_just_activated_by_user && (flags & ImGuiWindowFlags_NoFocusOnAppearing) == 0)
            {
                if ((flags & ImGuiWindowFlags_Popup) != 0)
                    want_focus = true;
                else if ((window.DockIsActive || (flags & ImGuiWindowFlags_ChildWindow) == 0) && (flags & ImGuiWindowFlags_Tooltip) == 0)
                    want_focus = true;
            }

            // [Test Engine] Register whole window in the item system (before submitting further decorations)
            // (IMGUI_ENABLE_TEST_ENGINE block omitted)

            // Decide if we are going to handle borders and resize grips
            // 'window->SkipItems' is not updated yet so for child windows we rely on ParentWindow to avoid submitting decorations. (#8815)
            // Whenever we add support for full decorated child windows we will likely make this logic more general.
            bool handle_borders_and_resize_grips = (window.DockNodeAsHost != null || !window.DockIsActive);
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && window.ParentWindow.SkipItems)
                handle_borders_and_resize_grips = false;

            // Handle manual resize: Resize Grips, Borders, Gamepad
            // Child windows can only be resized when they have the flags set. The resize grip allows resizing in both directions, so it should appear only if both flags are set.
            int border_hovered = -1, border_held = -1;
            Span<ImU32> resize_grip_col = stackalloc ImU32[4];
            int resize_grip_count;
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_Popup) == 0)
                resize_grip_count = ((window.ChildFlags & ImGuiChildFlags_ResizeX) != 0 && (window.ChildFlags & ImGuiChildFlags_ResizeY) != 0) ? 1 : 0;
            else
                resize_grip_count = g.IO.ConfigWindowsResizeFromEdges ? 2 : 1; // Allow resize from lower-left if we have the mouse cursor feedback for it.

            float resize_grip_draw_size = ImMath.ImTrunc(ImMath.ImMax(g.FontSize * 1.10f, window.WindowRounding + 1.0f + g.FontSize * 0.2f));
            if (handle_borders_and_resize_grips && !window.Collapsed)
            {
                int auto_fit_mask = UpdateWindowManualResize(window, out border_hovered, out border_held, resize_grip_count, resize_grip_col, visibility_rect);
                if (auto_fit_mask != 0)
                {
                    if ((auto_fit_mask & (1 << ImGuiAxis_X)) != 0)
                        use_current_size_for_scrollbar_x = true;
                    if ((auto_fit_mask & (1 << ImGuiAxis_Y)) != 0)
                        use_current_size_for_scrollbar_y = true;
                }
            }
            window.ResizeBorderHovered = (sbyte)border_hovered;
            window.ResizeBorderHeld = (sbyte)border_held;

            // SCROLLBAR VISIBILITY

            // Update scrollbar visibility (based on the Size that was effective during last frame or the auto-resized Size).
            if (!window.Collapsed)
            {
                // When reading the current size we need to read it after size constraints have been applied.
                // Intentionally use previous frame values for InnerRect and ScrollbarSizes.
                // And when we use window->DecorationUp here it doesn't have ScrollbarSizes.y applied yet.
                ImVec2 avail_size_from_current_frame = new ImVec2(window.SizeFull.x, window.SizeFull.y - (window.DecoOuterSizeY1 + window.DecoOuterSizeY2));
                ImVec2 avail_size_from_last_frame = window.InnerRect.GetSize() + scrollbar_sizes_from_last_frame;
                ImVec2 needed_size_from_last_frame = window_just_created ? new ImVec2(0, 0) : window.ContentSize + window.WindowPadding * 2.0f;
                float size_for_scrollbars_x = use_current_size_for_scrollbar_x ? avail_size_from_current_frame.x : avail_size_from_last_frame.x;
                float size_for_scrollbars_y = use_current_size_for_scrollbar_y ? avail_size_from_current_frame.y : avail_size_from_last_frame.y;
                bool scrollbar_x_prev = window.ScrollbarX;
                //bool scrollbar_y_from_last_frame = window->ScrollbarY; // FIXME: May want to use that in the ScrollbarX expression? How many pros vs cons?
                window.ScrollbarY = (flags & ImGuiWindowFlags_AlwaysVerticalScrollbar) != 0 || ((needed_size_from_last_frame.y > size_for_scrollbars_y) && (flags & ImGuiWindowFlags_NoScrollbar) == 0);
                window.ScrollbarX = (flags & ImGuiWindowFlags_AlwaysHorizontalScrollbar) != 0 || ((needed_size_from_last_frame.x > size_for_scrollbars_x - (window.ScrollbarY ? style.ScrollbarSize : 0.0f)) && (flags & ImGuiWindowFlags_NoScrollbar) == 0 && (flags & ImGuiWindowFlags_HorizontalScrollbar) != 0);

                // Track when ScrollbarX visibility keeps toggling, which is a sign of a feedback loop, and stabilize by enforcing visibility (#3285, #8488)
                // (Feedback loops of this sort can manifest in various situations, but combining horizontal + vertical scrollbar + using a clipper with varying width items is one frequent cause.
                //  The better solution is to, either (1) enforce visibility by using ImGuiWindowFlags_AlwaysHorizontalScrollbar or (2) declare stable contents width with SetNextWindowContentSize(), if you can compute it)
                window.ScrollbarXStabilizeToggledHistory <<= 1;
                window.ScrollbarXStabilizeToggledHistory |= (byte)(scrollbar_x_prev != window.ScrollbarX ? 0x01 : 0x00);
                bool scrollbar_x_stabilize = (window.ScrollbarXStabilizeToggledHistory != 0) && ImUtil.ImCountSetBits(window.ScrollbarXStabilizeToggledHistory) >= 4; // 4 == half of bits in our U8 history.
                if (scrollbar_x_stabilize)
                    window.ScrollbarX = true;
                //if (scrollbar_x_stabilize && !window->ScrollbarXStabilizeEnabled)
                //    IMGUI_DEBUG_LOG("[scroll] Stabilize ScrollbarX for Window '%s'\n", window->Name);
                window.ScrollbarXStabilizeEnabled = scrollbar_x_stabilize;

                if (window.ScrollbarX && !window.ScrollbarY)
                    window.ScrollbarY = (needed_size_from_last_frame.y > size_for_scrollbars_y - style.ScrollbarSize) && (flags & ImGuiWindowFlags_NoScrollbar) == 0;
                window.ScrollbarSizes = new ImVec2(window.ScrollbarY ? style.ScrollbarSize : 0.0f, window.ScrollbarX ? style.ScrollbarSize : 0.0f);

                // Amend the partially filled window->DecorationXXX values.
                window.DecoOuterSizeX2 += window.ScrollbarSizes.x;
                window.DecoOuterSizeY2 += window.ScrollbarSizes.y;
            }

            // UPDATE RECTANGLES (1- THOSE NOT AFFECTED BY SCROLLING)
            // Update various regions. Variables they depend on should be set above in this function.
            // We set this up after processing the resize grip so that our rectangles doesn't lag by a frame.

            // Outer rectangle
            // Not affected by window border size. Used by:
            // - FindHoveredWindow() (w/ extra padding when border resize is enabled)
            // - Begin() initial clipping rect for drawing window background and borders.
            // - Begin() clipping whole child
            ImRect host_rect = ((flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_Popup) == 0 && !window_is_child_tooltip) ? parent_window.ClipRect : viewport_rect;
            ImRect outer_rect = window.Rect();
            ImRect title_bar_rect = window.TitleBarRect();
            window.OuterRectClipped = outer_rect;
            if (window.DockIsActive)
                window.OuterRectClipped.Min.y += window.TitleBarHeight;
            window.OuterRectClipped.ClipWith(host_rect);

            // Inner rectangle
            // Not affected by window border size. Used by:
            // - InnerClipRect
            // - ScrollToRectEx()
            // - NavUpdatePageUpPageDown()
            // - Scrollbar()
            window.InnerRect.Min.x = window.Pos.x + window.DecoOuterSizeX1;
            window.InnerRect.Min.y = window.Pos.y + window.DecoOuterSizeY1;
            window.InnerRect.Max.x = window.Pos.x + window.Size.x - window.DecoOuterSizeX2;
            window.InnerRect.Max.y = window.Pos.y + window.Size.y - window.DecoOuterSizeY2;

            // Inner clipping rectangle.
            // - Extend a outside of normal work region up to borders.
            // - This is to allow e.g. Selectable or CollapsingHeader or some separators to cover that space.
            // - It also makes clipped items be more noticeable.
            // - And is consistent on both axis (prior to 2024/05/03 ClipRect used WindowPadding.x * 0.5f on left and right edge), see #3312
            // - Force round operator last to ensure that e.g. (int)(max.x-min.x) in user's render code produce correct result.
            // Note that if our window is collapsed we will end up with an inverted (~null) clipping rectangle which is the correct behavior.
            // Affected by window/frame border size. Used by:
            // - Begin() initial clip rect
            float top_border_size = (((flags & ImGuiWindowFlags_MenuBar) != 0 || (flags & ImGuiWindowFlags_NoTitleBar) == 0) ? style.FrameBorderSize : window.WindowBorderSize);

            // Try to match the fact that our border is drawn centered over the window rectangle, rather than inner.
            // This is why we do a *0.5f here. We don't currently even technically support large values for WindowBorderSize,
            // see e.g #7887 #7888, but may do after we move the window border to become an inner border (and then we can remove the 0.5f here).
            window.InnerClipRect.Min.x = ImMath.ImFloor(0.5f + window.InnerRect.Min.x + window.WindowBorderSize * 0.5f);
            window.InnerClipRect.Min.y = ImMath.ImFloor(0.5f + window.InnerRect.Min.y + top_border_size * 0.5f);
            window.InnerClipRect.Max.x = ImMath.ImFloor(window.InnerRect.Max.x - window.WindowBorderSize * 0.5f);
            window.InnerClipRect.Max.y = ImMath.ImFloor(window.InnerRect.Max.y - window.WindowBorderSize * 0.5f);
            window.InnerClipRect.ClipWithFull(host_rect);

            // SCROLLING

            // Lock down maximum scrolling
            // The value of ScrollMax are ahead from ScrollbarX/ScrollbarY which is intentionally using InnerRect from previous rect in order to accommodate
            // for right/bottom aligned items without creating a scrollbar.
            window.ScrollMax.x = ImMath.ImMax(0.0f, window.ContentSize.x + window.WindowPadding.x * 2.0f - window.InnerRect.GetWidth());
            window.ScrollMax.y = ImMath.ImMax(0.0f, window.ContentSize.y + window.WindowPadding.y * 2.0f - window.InnerRect.GetHeight());

            // Apply scrolling
            window.Scroll = CalcNextScrollFromScrollTargetAndClamp(window);
            window.ScrollTarget = new ImVec2(float.MaxValue, float.MaxValue);
            window.DecoInnerSizeX1 = window.DecoInnerSizeY1 = 0.0f;

            // DRAWING

            // Setup draw list and outer clipping rectangle
            Debug.Assert(window.DrawList.CmdBuffer.Size == 1 && window.DrawList.CmdBuffer[0].ElemCount == 0);
            window.DrawList._SetPixelDensity(window.Viewport.FramebufferScale.x);
            window.DrawList.PushTexture(g.Font.OwnerAtlas.TexRef);
            PushClipRect(host_rect.Min, host_rect.Max, false);

            // Child windows can render their decoration (bg color, border, scrollbars, etc.) within their parent to save a draw call (since 1.71)
            // When using overlapping child windows, this will break the assumption that child z-order is mapped to submission order.
            // FIXME: User code may rely on explicit sorting of overlapping child window and would need to disable this somehow. Please get in contact if you are affected (github #4493)
            {
                bool render_decorations_in_parent = false;
                if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_Popup) == 0 && !window_is_child_tooltip)
                {
                    // - We test overlap with the previous child window only (testing all would end up being O(log N) not a good investment here)
                    // - We disable this when the parent window has zero vertices, which is a common pattern leading to laying out multiple overlapping childs
                    ImGuiWindow previous_child = parent_window.DC.ChildWindows.Size >= 2 ? parent_window.DC.ChildWindows[parent_window.DC.ChildWindows.Size - 2] : null;
                    bool previous_child_overlapping = previous_child != null ? previous_child.Rect().Overlaps(window.Rect()) : false;
                    bool parent_is_empty = (parent_window.DrawList.VtxBuffer.Size == 0);
                    if (window.DrawList.CmdBuffer.back().ElemCount == 0 && !parent_is_empty && !previous_child_overlapping)
                        render_decorations_in_parent = true;
                }
                if (render_decorations_in_parent)
                    window.DrawList = parent_window.DrawList;

                // Handle title bar, scrollbar, resize grips and resize borders
                ImGuiWindow window_to_highlight = g.NavWindowingTarget != null ? g.NavWindowingTarget : g.NavWindow;
                bool title_bar_is_highlight = want_focus || (window_to_highlight != null && window.RootWindowForTitleBarHighlight == window_to_highlight.RootWindowForTitleBarHighlight);
                RenderWindowDecorations(window, title_bar_rect, title_bar_is_highlight, handle_borders_and_resize_grips, resize_grip_count, resize_grip_col, resize_grip_draw_size);

                if (render_decorations_in_parent)
                    window.DrawList = window.DrawListInst;
            }

            // UPDATE RECTANGLES (2- THOSE AFFECTED BY SCROLLING)

            // Work rectangle.
            // Affected by window padding and border size. Used by:
            // - Columns() for right-most edge
            // - TreeNode(), CollapsingHeader() for right-most edge
            // - BeginTabBar() for right-most edge
            bool allow_scrollbar_x = (flags & ImGuiWindowFlags_NoScrollbar) == 0 && (flags & ImGuiWindowFlags_HorizontalScrollbar) != 0;
            bool allow_scrollbar_y = (flags & ImGuiWindowFlags_NoScrollbar) == 0;
            float work_rect_size_x = (window.ContentSizeExplicit.x != 0.0f ? window.ContentSizeExplicit.x : ImMath.ImMax(allow_scrollbar_x ? window.ContentSize.x : 0.0f, window.Size.x - window.WindowPadding.x * 2.0f - (window.DecoOuterSizeX1 + window.DecoOuterSizeX2)));
            float work_rect_size_y = (window.ContentSizeExplicit.y != 0.0f ? window.ContentSizeExplicit.y : ImMath.ImMax(allow_scrollbar_y ? window.ContentSize.y : 0.0f, window.Size.y - window.WindowPadding.y * 2.0f - (window.DecoOuterSizeY1 + window.DecoOuterSizeY2)));
            window.WorkRect.Min.x = ImMath.ImTrunc(window.InnerRect.Min.x - window.Scroll.x + ImMath.ImMax(window.WindowPadding.x, window.WindowBorderSize));
            window.WorkRect.Min.y = ImMath.ImTrunc(window.InnerRect.Min.y - window.Scroll.y + ImMath.ImMax(window.WindowPadding.y, window.WindowBorderSize));
            window.WorkRect.Max.x = window.WorkRect.Min.x + work_rect_size_x;
            window.WorkRect.Max.y = window.WorkRect.Min.y + work_rect_size_y;
            window.ParentWorkRect = window.WorkRect;

            // [LEGACY] Content Region
            // FIXME-OBSOLETE: window->ContentRegionRect.Max is currently very misleading / partly faulty, but some BeginChild() patterns relies on it.
            // Unless explicit content size is specified by user, this currently represent the region leading to no scrolling.
            // Used by:
            // - Mouse wheel scrolling + many other things
            window.ContentRegionRect.Min.x = window.Pos.x - window.Scroll.x + window.WindowPadding.x + window.DecoOuterSizeX1;
            window.ContentRegionRect.Min.y = window.Pos.y - window.Scroll.y + window.WindowPadding.y + window.DecoOuterSizeY1;
            window.ContentRegionRect.Max.x = window.ContentRegionRect.Min.x + (window.ContentSizeExplicit.x != 0.0f ? window.ContentSizeExplicit.x : (window.Size.x - window.WindowPadding.x * 2.0f - (window.DecoOuterSizeX1 + window.DecoOuterSizeX2)));
            window.ContentRegionRect.Max.y = window.ContentRegionRect.Min.y + (window.ContentSizeExplicit.y != 0.0f ? window.ContentSizeExplicit.y : (window.Size.y - window.WindowPadding.y * 2.0f - (window.DecoOuterSizeY1 + window.DecoOuterSizeY2)));

            // Setup drawing context
            // (NB: That term "drawing context / DC" lost its meaning a long time ago. Initially was meant to hold transient data only. Nowadays difference between window-> and window->DC-> is dubious.)
            window.DC.Indent.x = window.DecoOuterSizeX1 + window.WindowPadding.x - window.Scroll.x;
            window.DC.GroupOffset.x = 0.0f;
            window.DC.ColumnsOffset.x = 0.0f;

            // Record the loss of precision of CursorStartPos which can happen due to really large scrolling amount.
            // This is used by clipper to compensate and fix the most common use case of large scroll area. Easy and cheap, next best thing compared to switching everything to double or ImU64.
            double start_pos_highp_x = (double)window.Pos.x + window.WindowPadding.x - (double)window.Scroll.x + window.DecoOuterSizeX1 + window.DC.ColumnsOffset.x;
            double start_pos_highp_y = (double)window.Pos.y + window.WindowPadding.y - (double)window.Scroll.y + window.DecoOuterSizeY1;
            window.DC.CursorStartPos = new ImVec2((float)start_pos_highp_x, (float)start_pos_highp_y);
            window.DC.CursorStartPosLossyness = new ImVec2((float)(start_pos_highp_x - window.DC.CursorStartPos.x), (float)(start_pos_highp_y - window.DC.CursorStartPos.y));
            window.DC.CursorPos = window.DC.CursorStartPos;
            window.DC.CursorPosPrevLine = window.DC.CursorPos;
            window.DC.CursorMaxPos = window.DC.CursorStartPos;
            window.DC.IdealMaxPos = window.DC.CursorStartPos;
            window.DC.CurrLineSize = window.DC.PrevLineSize = new ImVec2(0.0f, 0.0f);
            window.DC.CurrLineTextBaseOffset = window.DC.PrevLineTextBaseOffset = 0.0f;
            window.DC.IsSameLine = window.DC.IsSetPos = false;

            window.DC.NavLayerCurrent = ImGuiNavLayer_Main;
            window.DC.NavLayersActiveMask = window.DC.NavLayersActiveMaskNext;
            window.DC.NavLayersActiveMaskNext = 0x00;
            window.DC.NavIsScrollPushableX = true;
            window.DC.NavHideHighlightOneFrame = false;
            window.DC.NavWindowHasScrollY = (window.ScrollMax.y > 0.0f);

            window.DC.MenuBarAppending = false;
            window.DC.MenuColumns.Update(style.ItemSpacing.x, window_just_activated_by_user);
            window.DC.TreeDepth = 0;
            window.DC.TreeHasStackDataDepthMask = window.DC.TreeRecordsClippedNodesY2Mask = 0x00;
            window.DC.ChildWindows.resize(0);
            window.DC.StateStorage = window.StateStorage;
            window.DC.CurrentColumns = null;
            window.DC.LayoutType = ImGuiLayoutType_Vertical;
            window.DC.ParentLayoutType = parent_window != null ? parent_window.DC.LayoutType : ImGuiLayoutType_Vertical;

            // Default item width. Make it proportional to window size if window can be manually resized.
            // (we cannot use AutoFitFramesX/AutoFitFramesY which is a temporary state)
            bool is_resizable_width;
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0)
                is_resizable_width = (window.Size.x > 0.0f) && (window.ChildFlags & (ImGuiChildFlags_AutoResizeX | ImGuiChildFlags_AlwaysAutoResize)) == 0;
            else
                is_resizable_width = (window.Size.x > 0.0f) && (flags & ImGuiWindowFlags_AlwaysAutoResize) == 0;
            if (is_resizable_width)
                window.DC.ItemWidthDefault = ImMath.ImTrunc(window.Size.x * 0.65f);
            else
                window.DC.ItemWidthDefault = ImMath.ImTrunc(g.FontSize * 16.0f);
            window.DC.ItemWidth = window.DC.ItemWidthDefault;
            window.DC.ItemWidthStack.resize(0);
            window.DC.TextWrapPos = -1.0f; // Disabled
            window.DC.TextWrapPosStack.resize(0);
            if ((flags & ImGuiWindowFlags_Modal) != 0)
                window.DC.ModalDimBgColor = ColorConvertFloat4ToU32(GetStyleColorVec4(ImGuiCol_ModalWindowDimBg));

            if (window.AutoFitFramesX > 0)
                window.AutoFitFramesX--;
            if (window.AutoFitFramesY > 0)
                window.AutoFitFramesY--;

            // Apply focus (we need to call FocusWindow() AFTER setting DC.CursorStartPos so our initial navigation reference rectangle can start around there)
            // We ImGuiFocusRequestFlags_UnlessBelowModal to:
            // - Avoid focusing a window that is created outside of a modal. This will prevent active modal from being closed.
            // - Position window behind the modal that is not a begin-parent of this window.
            if (want_focus)
                FocusWindow(window, ImGuiFocusRequestFlags_UnlessBelowModal);
            if (want_focus && window == g.NavWindow)
                NavInitWindow(window, false); // <-- this is in the way for us to be able to defer and sort reappearing FocusWindow() calls

            // Pressing Ctrl+C copy window content into the clipboard
            // [EXPERIMENTAL] Breaks on nested Begin/End pairs. We need to work that out and add better logging scope.
            // [EXPERIMENTAL] Text outputs has many issues.
            if (g.IO.ConfigWindowsCopyContentsWithCtrlC)
                if (g.NavWindow != null && g.NavWindow.RootWindow == window && g.ActiveId == 0 && Shortcut(ImGuiMod_Ctrl | ImGuiKey_C, 0, 0))
                    LogToClipboard(0);

            // Title bar
            if ((flags & ImGuiWindowFlags_NoTitleBar) == 0 && !window.DockIsActive)
                RenderWindowTitleBarContents(window, new ImRect(title_bar_rect.Min.x + window.WindowBorderSize, title_bar_rect.Min.y, title_bar_rect.Max.x - window.WindowBorderSize, title_bar_rect.Max.y), name, p_open);
            else if ((flags & ImGuiWindowFlags_NoTitleBar) == 0 && window.DockIsActive)
            {
                int rendered_end = name.IndexOf("##", StringComparison.Ordinal);
                if (rendered_end < 0) rendered_end = name.Length;
                LogText(name.Substring(0, rendered_end) + "\n");
            }

            // Clear hit test shape every frame
            window.HitTestHoleSize.x = window.HitTestHoleSize.y = 0;

            if ((flags & ImGuiWindowFlags_Tooltip) != 0)
                g.TooltipPreviousWindow = window;

            if ((g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable) != 0)
            {
                // Docking: Dragging a dockable window (or any of its child) turns it into a drag and drop source.
                // We need to do this _before_ we overwrite window.DC.LastItemId below because BeginDockableDragDropSource() also overwrites it.
                if (g.MovingWindow == window && (window.RootWindowDockTree.Flags & ImGuiWindowFlags_NoDocking) == 0)
                    BeginDockableDragDropSource(window);

                // Docking: Any dockable window can act as a target. For dock node hosts we call BeginDockableDragDropTarget() in DockNodeUpdate() instead.
                if (g.DragDropActive && (flags & ImGuiWindowFlags_NoDocking) == 0)
                    if (g.MovingWindow == null || g.MovingWindow.RootWindowDockTree != window)
                        if (window == window.RootWindowDockTree && (window.Flags & ImGuiWindowFlags_DockNodeHost) == 0)
                            BeginDockableDragDropTarget(window);
            }

            // Set default BgClickFlags
            // This is set at the end of this function, so UpdateWindowManualResize()/ClampWindowPos() may use last-frame value if overridden by user code.
            // FIXME: The general intent is that we will later expose config options to default to enable scrolling + select scrolling mouse button.
            window.BgClickFlags = (byte)((flags & ImGuiWindowFlags_ChildWindow) != 0 ? parent_window.BgClickFlags : (g.IO.ConfigWindowsMoveFromTitleBarOnly ? ImGuiWindowBgClickFlags_None : ImGuiWindowBgClickFlags_Move)); // -V1004

            // We fill last item data based on Title Bar/Tab, in order for IsItemHovered() and IsItemActive() to be usable after Begin().
            // This is useful to allow creating context menus on title bar only, etc.
            window.DC.WindowItemStatusFlags = ImGuiItemStatusFlags_None;
            window.DC.WindowItemStatusFlags |= IsMouseHoveringRect(title_bar_rect.Min, title_bar_rect.Max, false) ? ImGuiItemStatusFlags_HoveredRect : 0;
            SetLastItemDataForWindow(window, title_bar_rect);

            // [DEBUG]
            if (g.DebugLocateId != 0 && (window.ID == g.DebugLocateId || window.MoveId == g.DebugLocateId))
                DebugLocateItemResolveWithLastItem();

            // [Test Engine] Register title bar / tab with MoveId.
            // (IMGUI_ENABLE_TEST_ENGINE block omitted)
        }
        else
        {
            // Skip refresh always mark active
            if (window.SkipRefresh)
                SetWindowActiveForSkipRefresh(window);

            // Append
            SetCurrentWindow(window);
            SetLastItemDataForWindow(window, window.TitleBarRect());
        }

        if (!window.SkipRefresh)
            PushClipRect(window.InnerClipRect.Min, window.InnerClipRect.Max, true);

        // Clear 'accessed' flag last thing (After PushClipRect which will set the flag. We want the flag to stay false when the default "Debug" window is unused)
        window.WriteAccessed = false;
        window.BeginCount++;
        g.NextWindowData.ClearFlags();

        // Update visibility
        if (first_begin_of_the_frame && !window.SkipRefresh)
        {
            // When we are about to select this tab (which will only be visible on the _next frame_), flag it with a non-zero HiddenFramesCannotSkipItems.
            // This will have the important effect of actually returning true in Begin() and not setting SkipItems, allowing an earlier submission of the window contents.
            // This is analogous to regular windows being hidden from one frame.
            // It is especially important as e.g. nested TabBars would otherwise generate flicker in the form of one empty frame, or focus requests won't be processed.
            if (window.DockIsActive && !window.DockTabIsVisible)
            {
                if (window.LastFrameJustFocused == g.FrameCount)
                    window.HiddenFramesCannotSkipItems = 1;
                else
                    window.HiddenFramesCanSkipItems = 1;
            }

            if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_ChildMenu) == 0)
            {
                // Child window can be out of sight and have "negative" clip windows.
                // Mark them as collapsed so commands are skipped earlier (we can't manually collapse them because they have no title bar).
                Debug.Assert((flags & ImGuiWindowFlags_NoTitleBar) != 0 || window.DockIsActive);
                bool nav_request = (window.ChildFlags & ImGuiChildFlags_NavFlattened) != 0 && (g.NavAnyRequest && g.NavWindow != null && g.NavWindow.RootWindowForNav == window.RootWindowForNav);
                if (!g.LogEnabled && !nav_request)
                    if (window.OuterRectClipped.Min.x >= window.OuterRectClipped.Max.x || window.OuterRectClipped.Min.y >= window.OuterRectClipped.Max.y)
                    {
                        if (window.AutoFitFramesX > 0 || window.AutoFitFramesY > 0)
                            window.HiddenFramesCannotSkipItems = 1;
                        else
                            window.HiddenFramesCanSkipItems = 1;
                    }

                // Hide along with parent or if parent is collapsed
                if (parent_window != null && (parent_window.Collapsed || parent_window.HiddenFramesCanSkipItems > 0))
                    window.HiddenFramesCanSkipItems = 1;
                if (parent_window != null && parent_window.HiddenFramesCannotSkipItems > 0)
                    window.HiddenFramesCannotSkipItems = 1;
            }

            // Don't render if style alpha is 0.0 at the time of Begin(). This is arbitrary and inconsistent but has been there for a long while (may remove at some point)
            if (style.Alpha <= 0.0f)
                window.HiddenFramesCanSkipItems = 1;

            // Update the Hidden flag
            bool hidden_regular = (window.HiddenFramesCanSkipItems > 0) || (window.HiddenFramesCannotSkipItems > 0);
            window.Hidden = hidden_regular || (window.HiddenFramesForRenderOnly > 0);

            // Disable inputs for requested number of frames
            if (window.DisableInputsFrames > 0)
            {
                window.DisableInputsFrames--;
                window.Flags |= ImGuiWindowFlags_NoInputs;
            }

            // Update the SkipItems flag, used to early out of all items functions (no layout required)
            bool skip_items = false;
            if (window.Collapsed || !window.Active || hidden_regular)
                if (window.AutoFitFramesX <= 0 && window.AutoFitFramesY <= 0 && window.HiddenFramesCannotSkipItems <= 0)
                    skip_items = true;
            window.SkipItems = skip_items;
        }
        else if (first_begin_of_the_frame)
        {
            // Skip refresh mode
            window.SkipItems = true;
        }

        // [DEBUG] io.ConfigDebugBeginReturnValue override return value to test Begin/End and BeginChild/EndChild behaviors.
        // (The implicit fallback window is NOT automatically ended allowing it to always be able to receive commands without crashing)
        if (!window.IsFallbackWindow)
            if ((g.IO.ConfigDebugBeginReturnValueOnce && window_just_created) || (g.IO.ConfigDebugBeginReturnValueLoop && g.DebugBeginReturnValueCullDepth == g.CurrentWindowStack.Size))
            {
                if (window.AutoFitFramesX > 0) { window.AutoFitFramesX++; }
                if (window.AutoFitFramesY > 0) { window.AutoFitFramesY++; }
                return false;
            }

        return !window.SkipItems;
    }

    // C++: void ImGui::End()
    public static void End()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        // Error checking: verify that user hasn't called End() too many times!
        if (g.CurrentWindowStack.Size <= 1 && g.WithinFrameScopeWithImplicitWindow)
        {
            ImUtil.ImAssertUserError(g.CurrentWindowStack.Size > 1, "Calling End() too many times!");
            return;
        }
        ref ImGuiWindowStackData window_stack_data = ref g.CurrentWindowStack.back_ref();

        // Error checking: verify that user doesn't directly call End() on a child window.
        if ((window.Flags & ImGuiWindowFlags_Popup) != 0)
            ImUtil.ImAssertUserError(g.WithinEndPopupID == window.ID, "Must call EndPopup() and not End()!");
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0 && (window.Flags & ImGuiWindowFlags_DockNodeHost) == 0 && !window.DockIsActive)
            ImUtil.ImAssertUserError(g.WithinEndChildID == window.ID, "Must call EndChild() and not End()!");

        // Close anything that is open
        if (window.DC.CurrentColumns != null)
            EndColumns();
        if ((window.Flags & ImGuiWindowFlags_DockNodeHost) == 0 && !window.SkipRefresh)
            PopClipRect();   // Inner window clip rectangle
        PopFocusScope();
        if (window_stack_data.DisabledOverrideReenable && window.RootWindow == window)
            EndDisabledOverrideReenable();

        if (window.SkipRefresh)
        {
            Debug.Assert(window.DrawList == null);
            window.DrawList = window.DrawListInst;
        }

        // Stop logging
        if (g.LogWindow == window) // FIXME: add more options for scope of logging
            LogFinish();

        if (window.DC.IsSetPos)
            ErrorCheckUsingSetCursorPosToExtendParentBoundaries();

        // Pop from window stack
        g.LastItemData = window_stack_data.ParentLastItemDataBackup;
        if ((window.Flags & ImGuiWindowFlags_ChildMenu) != 0)
            g.BeginMenuDepth--;
        if ((window.Flags & ImGuiWindowFlags_Popup) != 0)
            g.BeginPopupStack.pop_back();

        // Error handling, state recovery
        if (g.IO.ConfigErrorRecovery)
            ErrorRecoveryTryToRecoverWindowState(window_stack_data.StackSizesInBegin);

        g.CurrentWindowStack.pop_back();
        SetCurrentWindow(g.CurrentWindowStack.Size == 0 ? null : g.CurrentWindowStack.back().Window);
    }

    // C++: void ImGui::PushItemFlag(ImGuiItemFlags option, bool enabled)
    public static void PushItemFlag(ImGuiItemFlags option, bool enabled)
    {
        ImGuiContext g = GImGui;
        int item_flags = g.CurrentItemFlags;
        Debug.Assert(item_flags == g.ItemFlagsStack.back());
        if (enabled)
            item_flags |= option;
        else
            item_flags &= ~option;
        g.CurrentItemFlags = item_flags;
        g.ItemFlagsStack.push_back(item_flags);
    }

    // C++: void ImGui::PopItemFlag()
    public static void PopItemFlag()
    {
        ImGuiContext g = GImGui;
        if (g.ItemFlagsStack.Size <= 1)
        {
            ImUtil.ImAssertUserError(false, "Calling PopItemFlag() too many times!");
            return;
        }
        g.ItemFlagsStack.pop_back();
        g.CurrentItemFlags = g.ItemFlagsStack.back();
    }

    // BeginDisabled()/EndDisabled()
    // - Those can be nested but it cannot be used to enable an already disabled section (a single BeginDisabled(true) in the stack is enough to keep everything disabled)
    // - Visually this is currently altering alpha, but it is expected that in a future styling system this would work differently.
    // - Feedback welcome at https://github.com/ocornut/imgui/issues/211
    // - BeginDisabled(false)/EndDisabled() essentially does nothing but is provided to facilitate use of boolean expressions.
    //   (as a micro-optimization: if you have tens of thousands of BeginDisabled(false)/EndDisabled() pairs, you might want to reformulate your code to avoid making those calls)
    // - Note: mixing up BeginDisabled() and PushItemFlag(ImGuiItemFlags_Disabled) is currently NOT SUPPORTED.
    // C++: void ImGui::BeginDisabled(bool disabled)
    public static void BeginDisabled(bool disabled)
    {
        ImGuiContext g = GImGui;
        bool was_disabled = (g.CurrentItemFlags & ImGuiItemFlags_Disabled) != 0;
        if (!was_disabled && disabled)
        {
            g.DisabledAlphaBackup = g.Style.Alpha;
            g.Style.Alpha *= g.Style.DisabledAlpha; // PushStyleVar(ImGuiStyleVar_Alpha, g.Style.Alpha * g.Style.DisabledAlpha);
        }
        if (was_disabled || disabled)
            g.CurrentItemFlags |= ImGuiItemFlags_Disabled;
        g.ItemFlagsStack.push_back(g.CurrentItemFlags); // FIXME-OPT: can we simply skip this and use DisabledStackSize?
        g.DisabledStackSize++;
    }

    // C++: void ImGui::EndDisabled()
    public static void EndDisabled()
    {
        ImGuiContext g = GImGui;
        if (g.DisabledStackSize <= 0)
        {
            ImUtil.ImAssertUserError(false, "Calling EndDisabled() too many times!");
            return;
        }
        g.DisabledStackSize--;
        bool was_disabled = (g.CurrentItemFlags & ImGuiItemFlags_Disabled) != 0;
        //PopItemFlag();
        g.ItemFlagsStack.pop_back();
        g.CurrentItemFlags = g.ItemFlagsStack.back();
        if (was_disabled && (g.CurrentItemFlags & ImGuiItemFlags_Disabled) == 0)
            g.Style.Alpha = g.DisabledAlphaBackup; //PopStyleVar();
    }

    // Could have been called BeginDisabledDisable() but it didn't want to be award nominated for most awkward function name.
    // Ideally we would use a shared e.g. BeginDisabled()->BeginDisabledEx() but earlier needs to be optimal.
    // The whole code for this is awkward, will reevaluate if we find a way to implement SetNextItemDisabled().
    // C++: void ImGui::BeginDisabledOverrideReenable()
    public static void BeginDisabledOverrideReenable()
    {
        ImGuiContext g = GImGui;
        Debug.Assert((g.CurrentItemFlags & ImGuiItemFlags_Disabled) != 0);
        g.CurrentWindowStack.back_ref().DisabledOverrideReenableAlphaBackup = g.Style.Alpha;
        g.Style.Alpha = g.DisabledAlphaBackup;
        g.CurrentItemFlags &= ~ImGuiItemFlags_Disabled;
        g.ItemFlagsStack.push_back(g.CurrentItemFlags);
        g.DisabledStackSize++;
    }

    // C++: void ImGui::EndDisabledOverrideReenable()
    public static void EndDisabledOverrideReenable()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.DisabledStackSize > 0);
        g.DisabledStackSize--;
        g.ItemFlagsStack.pop_back();
        g.CurrentItemFlags = g.ItemFlagsStack.back();
        g.Style.Alpha = g.CurrentWindowStack.back().DisabledOverrideReenableAlphaBackup;
    }

    // ATTENTION THIS IS IN LEGACY LOCAL SPACE.
    // C++: void ImGui::PushTextWrapPos(float wrap_local_pos_x)
    public static void PushTextWrapPos(float wrap_local_pos_x = 0.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        window.DC.TextWrapPosStack.push_back(window.DC.TextWrapPos);
        window.DC.TextWrapPos = wrap_local_pos_x;
    }

    // C++: void ImGui::PopTextWrapPos()
    public static void PopTextWrapPos()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.DC.TextWrapPosStack.Size <= 0)
        {
            ImUtil.ImAssertUserError(false, "Calling PopTextWrapPos() too many times!");
            return;
        }
        window.DC.TextWrapPos = window.DC.TextWrapPosStack.back();
        window.DC.TextWrapPosStack.pop_back();
    }

    // C++: static ImGuiWindow* GetCombinedRootWindow(ImGuiWindow* window, bool popup_hierarchy, bool dock_hierarchy)
    public static ImGuiWindow GetCombinedRootWindow(ImGuiWindow window, bool popup_hierarchy, bool dock_hierarchy = false)
    {
        ImGuiWindow last_window = null;
        while (last_window != window)
        {
            last_window = window;
            window = window.RootWindow;
            if (popup_hierarchy)
                window = window.RootWindowPopupTree;
            if (dock_hierarchy)
                window = window.RootWindowDockTree;
        }
        return window;
    }

    // C++: bool ImGui::IsWindowChildOf(ImGuiWindow* window, ImGuiWindow* potential_parent, bool popup_hierarchy, bool dock_hierarchy)
    public static bool IsWindowChildOf(ImGuiWindow window, ImGuiWindow potential_parent, bool popup_hierarchy, bool dock_hierarchy = false)
    {
        ImGuiWindow window_root = GetCombinedRootWindow(window, popup_hierarchy, dock_hierarchy);
        if (window_root == potential_parent)
            return true;
        while (window != null)
        {
            if (window == potential_parent)
                return true;
            if (window == window_root) // end of chain
                return false;
            window = window.ParentWindow;
        }
        return false;
    }

    // C++: bool ImGui::IsWindowInBeginStack(ImGuiWindow* window)
    public static bool IsWindowInBeginStack(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        for (int n = g.CurrentWindowStack.Size - 1; n >= 0; n--)
            if (g.CurrentWindowStack[n].Window == window)
                return true;
        return false;
    }

    // C++: bool ImGui::IsWindowWithinBeginStackOf(ImGuiWindow* window, ImGuiWindow* potential_parent)
    public static bool IsWindowWithinBeginStackOf(ImGuiWindow window, ImGuiWindow potential_parent)
    {
        if (window.RootWindow == potential_parent)
            return true;
        while (window != null)
        {
            if (window == potential_parent)
                return true;
            window = window.ParentWindowInBeginStack;
        }
        return false;
    }

    // C++: bool ImGui::IsWindowAbove(ImGuiWindow* potential_above, ImGuiWindow* potential_below)
    public static bool IsWindowAbove(ImGuiWindow potential_above, ImGuiWindow potential_below)
    {
        ImGuiContext g = GImGui;

        // It would be saner to ensure that display layer is always reflected in the g.Windows[] order, which would likely requires altering all manipulations of that array
        int display_layer_delta = GetWindowDisplayLayer(potential_above) - GetWindowDisplayLayer(potential_below);
        if (display_layer_delta != 0)
            return display_layer_delta > 0;

        for (int i = g.Windows.Size - 1; i >= 0; i--)
        {
            ImGuiWindow candidate_window = g.Windows[i];
            if (candidate_window == potential_above)
                return true;
            if (candidate_window == potential_below)
                return false;
        }
        return false;
    }

    // Is current window hovered and hoverable (e.g. not blocked by a popup/modal)? See ImGuiHoveredFlags_ for options.
    // IMPORTANT: If you are trying to check whether your mouse should be dispatched to Dear ImGui or to your underlying app,
    // you should not use this function! Use the 'io.WantCaptureMouse' boolean for that!
    // Refer to FAQ entry "How can I tell whether to dispatch mouse/keyboard to Dear ImGui or my application?" for details.
    // C++: bool ImGui::IsWindowHovered(ImGuiHoveredFlags flags)
    public static bool IsWindowHovered(ImGuiHoveredFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImUtil.ImAssertUserError((flags & ~ImGuiHoveredFlags_AllowedMaskForIsWindowHovered) == 0, "Invalid flags for IsWindowHovered()!");

        ImGuiWindow ref_window = g.HoveredWindow;
        ImGuiWindow cur_window = g.CurrentWindow;
        if (ref_window == null)
            return false;

        if ((flags & ImGuiHoveredFlags_AnyWindow) == 0)
        {
            Debug.Assert(cur_window != null); // Not inside a Begin()/End()
            bool popup_hierarchy = (flags & ImGuiHoveredFlags_NoPopupHierarchy) == 0;
            bool dock_hierarchy = (flags & ImGuiHoveredFlags_DockHierarchy) != 0;
            if ((flags & ImGuiHoveredFlags_RootWindow) != 0)
                cur_window = GetCombinedRootWindow(cur_window, popup_hierarchy, dock_hierarchy);

            bool result;
            if ((flags & ImGuiHoveredFlags_ChildWindows) != 0)
                result = IsWindowChildOf(ref_window, cur_window, popup_hierarchy, dock_hierarchy);
            else
                result = (ref_window == cur_window);
            if (!result)
                return false;
        }

        if (!IsWindowContentHoverable(ref_window, flags))
            return false;
        if ((flags & ImGuiHoveredFlags_AllowWhenBlockedByActiveItem) == 0)
            if (g.ActiveId != 0 && !g.ActiveIdAllowOverlap && g.ActiveId != ref_window.MoveId)
                return false;

        // When changing hovered window we requires a bit of stationary delay before activating hover timer.
        // FIXME: We don't support delay other than stationary one for now, other delay would need a way
        // to fulfill the possibility that multiple IsWindowHovered() with varying flag could return true
        // for different windows of the hierarchy. Possibly need a Hash(Current+Flags) ==> (Timer) cache.
        // We can implement this for _Stationary because the data is linked to HoveredWindow rather than CurrentWindow.
        if ((flags & ImGuiHoveredFlags_ForTooltip) != 0)
            flags = ApplyHoverFlagsForTooltip(flags, g.Style.HoverFlagsForTooltipMouse);
        if ((flags & ImGuiHoveredFlags_Stationary) != 0 && g.HoverWindowUnlockedStationaryId != ref_window.ID)
            return false;

        return true;
    }

    // C++: float ImGui::GetWindowWidth()
    public static float GetWindowWidth()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.Size.x;
    }

    // C++: float ImGui::GetWindowHeight()
    public static float GetWindowHeight()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.Size.y;
    }

    // C++: ImVec2 ImGui::GetWindowPos()
    public static ImVec2 GetWindowPos()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        return window.Pos;
    }

    // C++: void ImGui::SetWindowPos(ImGuiWindow* window, const ImVec2& pos, ImGuiCond cond)
    public static void SetWindowPos(ImGuiWindow window, ImVec2 pos, ImGuiCond cond)
    {
        // Test condition (NB: bit 0 is always true) and clear flags for next time
        if (cond != 0 && (window.SetWindowPosAllowFlags & cond) == 0)
            return;

        Debug.Assert(cond == 0 || ImMath.ImIsPowerOfTwo(cond)); // Make sure the user doesn't attempt to combine multiple condition flags.
        window.SetWindowPosAllowFlags = (byte)(window.SetWindowPosAllowFlags & ~(ImGuiCond_Once | ImGuiCond_FirstUseEver | ImGuiCond_Appearing));
        window.SetWindowPosVal = new ImVec2(float.MaxValue, float.MaxValue);

        // Set
        ImVec2 old_pos = window.Pos;
        window.Pos = ImMath.ImTrunc(pos);
        ImVec2 offset = window.Pos - old_pos;
        if (offset.x == 0.0f && offset.y == 0.0f)
            return;
        MarkIniSettingsDirty(window);
        window.DC.CursorPos += offset;         // As we happen to move the window while it is being appended to (which is a bad idea - will smear) let's at least offset the cursor
        window.DC.CursorMaxPos += offset;      // And more importantly we need to offset CursorMaxPos/CursorStartPos this so ContentSize calculation doesn't get affected.
        window.DC.IdealMaxPos += offset;
        window.DC.CursorStartPos += offset;
    }

    // C++: void ImGui::SetWindowPos(const ImVec2& pos, ImGuiCond cond)
    public static void SetWindowPos(ImVec2 pos, ImGuiCond cond = 0)
    {
        ImGuiWindow window = GetCurrentWindowRead();
        SetWindowPos(window, pos, cond);
    }

    // C++: void ImGui::SetWindowPos(const char* name, const ImVec2& pos, ImGuiCond cond)
    public static void SetWindowPos(string name, ImVec2 pos, ImGuiCond cond = 0)
    {
        ImGuiWindow window = FindWindowByName(name);
        if (window != null)
            SetWindowPos(window, pos, cond);
    }

    // C++: ImVec2 ImGui::GetWindowSize()
    public static ImVec2 GetWindowSize()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.Size;
    }

    // C++: void ImGui::SetWindowSize(ImGuiWindow* window, const ImVec2& size, ImGuiCond cond)
    public static void SetWindowSize(ImGuiWindow window, ImVec2 size, ImGuiCond cond)
    {
        // Test condition (NB: bit 0 is always true) and clear flags for next time
        if (cond != 0 && (window.SetWindowSizeAllowFlags & cond) == 0)
            return;

        Debug.Assert(cond == 0 || ImMath.ImIsPowerOfTwo(cond)); // Make sure the user doesn't attempt to combine multiple condition flags.
        window.SetWindowSizeAllowFlags = (byte)(window.SetWindowSizeAllowFlags & ~(ImGuiCond_Once | ImGuiCond_FirstUseEver | ImGuiCond_Appearing));

        // Enable auto-fit (not done in BeginChild() path unless appearing or combined with ImGuiChildFlags_AlwaysAutoResize)
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) == 0 || window.Appearing || (window.ChildFlags & ImGuiChildFlags_AlwaysAutoResize) != 0)
            window.AutoFitFramesX = (sbyte)(size.x <= 0.0f ? 2 : 0);
        if ((window.Flags & ImGuiWindowFlags_ChildWindow) == 0 || window.Appearing || (window.ChildFlags & ImGuiChildFlags_AlwaysAutoResize) != 0)
            window.AutoFitFramesY = (sbyte)(size.y <= 0.0f ? 2 : 0);

        // Set
        ImVec2 old_size = window.SizeFull;
        if (size.x <= 0.0f)
            window.AutoFitOnlyGrows = false;
        else
            window.SizeFull.x = ImMath.ImTrunc(size.x);
        if (size.y <= 0.0f)
            window.AutoFitOnlyGrows = false;
        else
            window.SizeFull.y = ImMath.ImTrunc(size.y);
        if (old_size.x != window.SizeFull.x || old_size.y != window.SizeFull.y)
            MarkIniSettingsDirty(window);
    }

    // C++: void ImGui::SetWindowSize(const ImVec2& size, ImGuiCond cond)
    public static void SetWindowSize(ImVec2 size, ImGuiCond cond = 0)
    {
        SetWindowSize(GImGui.CurrentWindow, size, cond);
    }

    // C++: void ImGui::SetWindowSize(const char* name, const ImVec2& size, ImGuiCond cond)
    public static void SetWindowSize(string name, ImVec2 size, ImGuiCond cond = 0)
    {
        ImGuiWindow window = FindWindowByName(name);
        if (window != null)
            SetWindowSize(window, size, cond);
    }

    // C++: void ImGui::SetWindowCollapsed(ImGuiWindow* window, bool collapsed, ImGuiCond cond)
    public static void SetWindowCollapsed(ImGuiWindow window, bool collapsed, ImGuiCond cond)
    {
        // Test condition (NB: bit 0 is always true) and clear flags for next time
        if (cond != 0 && (window.SetWindowCollapsedAllowFlags & cond) == 0)
            return;
        window.SetWindowCollapsedAllowFlags = (byte)(window.SetWindowCollapsedAllowFlags & ~(ImGuiCond_Once | ImGuiCond_FirstUseEver | ImGuiCond_Appearing));

        // Queue applying in Begin()
        if (window.WantCollapseToggle)
            window.Collapsed = !window.Collapsed; // C++: window->Collapsed ^= 1;
        window.WantCollapseToggle = (window.Collapsed != collapsed);
    }

    // C++: void ImGui::SetWindowHitTestHole(ImGuiWindow* window, const ImVec2& pos, const ImVec2& size)
    public static void SetWindowHitTestHole(ImGuiWindow window, ImVec2 pos, ImVec2 size)
    {
        Debug.Assert(window.HitTestHoleSize.x == 0);     // We don't support multiple holes/hit test filters
        window.HitTestHoleSize = new ImVec2ih((short)size.x, (short)size.y);
        window.HitTestHoleOffset = new ImVec2ih((short)(pos.x - window.Pos.x), (short)(pos.y - window.Pos.y));
    }

    // C++: void ImGui::SetWindowHiddenAndSkipItemsForCurrentFrame(ImGuiWindow* window)
    public static void SetWindowHiddenAndSkipItemsForCurrentFrame(ImGuiWindow window)
    {
        window.SkipItems = window.Hidden = true;
        window.HiddenFramesCanSkipItems = 1;
    }

    // C++: void ImGui::SetWindowCollapsed(bool collapsed, ImGuiCond cond)
    public static void SetWindowCollapsed(bool collapsed, ImGuiCond cond = 0)
    {
        SetWindowCollapsed(GImGui.CurrentWindow, collapsed, cond);
    }

    // C++: bool ImGui::IsWindowCollapsed()
    public static bool IsWindowCollapsed()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.Collapsed;
    }

    // C++: bool ImGui::IsWindowAppearing()
    public static bool IsWindowAppearing()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.Appearing;
    }

    // C++: void ImGui::SetWindowCollapsed(const char* name, bool collapsed, ImGuiCond cond)
    public static void SetWindowCollapsed(string name, bool collapsed, ImGuiCond cond = 0)
    {
        ImGuiWindow window = FindWindowByName(name);
        if (window != null)
            SetWindowCollapsed(window, collapsed, cond);
    }

    // C++: void ImGui::SetNextWindowPos(const ImVec2& pos, ImGuiCond cond, const ImVec2& pivot)
    public static void SetNextWindowPos(ImVec2 pos, ImGuiCond cond = 0, ImVec2 pivot = default)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(cond == 0 || ImMath.ImIsPowerOfTwo(cond)); // Make sure the user doesn't attempt to combine multiple condition flags.
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasPos;
        g.NextWindowData.PosVal = pos;
        g.NextWindowData.PosPivotVal = pivot;
        g.NextWindowData.PosCond = cond != 0 ? cond : ImGuiCond_Always;
    }

    // C++: void ImGui::SetNextWindowViewport(ImGuiID id)
    public static void SetNextWindowViewport(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasViewport;
        g.NextWindowData.ViewportId = id;
    }

    // C++: void ImGui::SetNextWindowDockID(ImGuiID id, ImGuiCond cond)
    public static void SetNextWindowDockID(ImGuiID id, ImGuiCond cond = 0)
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasDock;
        g.NextWindowData.DockCond = cond != 0 ? cond : ImGuiCond_Always;
        g.NextWindowData.DockId = id;
    }

    // C++: void ImGui::SetNextWindowClass(const ImGuiWindowClass* window_class)
    public static void SetNextWindowClass(ImGuiWindowClass window_class)
    {
        ImGuiContext g = GImGui;
        Debug.Assert((window_class.ViewportFlagsOverrideSet & window_class.ViewportFlagsOverrideClear) == 0); // Cannot set both set and clear for the same bit
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasWindowClass;
        g.NextWindowData.WindowClass = window_class;
    }

    // C++: void ImGui::SetNextWindowSize(const ImVec2& size, ImGuiCond cond)
    public static void SetNextWindowSize(ImVec2 size, ImGuiCond cond = 0)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(cond == 0 || ImMath.ImIsPowerOfTwo(cond)); // Make sure the user doesn't attempt to combine multiple condition flags.
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasSize;
        g.NextWindowData.SizeVal = size;
        g.NextWindowData.SizeCond = cond != 0 ? cond : ImGuiCond_Always;
    }

    // For each axis:
    // - Use 0.0f as min or FLT_MAX as max if you don't want limits, e.g. size_min = (500.0f, 0.0f), size_max = (FLT_MAX, FLT_MAX) sets a minimum width.
    // - Use -1 for both min and max of same axis to preserve current size which itself is a constraint.
    // - See "Demo->Examples->Constrained-resizing window" for examples.
    // C++: void ImGui::SetNextWindowSizeConstraints(const ImVec2& size_min, const ImVec2& size_max, ImGuiSizeCallback custom_callback, void* custom_callback_user_data)
    public static void SetNextWindowSizeConstraints(ImVec2 size_min, ImVec2 size_max, Func<ImGuiSizeCallbackData, float> custom_callback = null, object custom_callback_user_data = null)
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasSizeConstraint;
        g.NextWindowData.SizeConstraintRect = new ImRect(size_min, size_max);
        g.NextWindowData.SizeCallback = custom_callback;
        g.NextWindowData.SizeCallbackUserData = custom_callback_user_data;
    }

    // Content size = inner scrollable rectangle, padded with WindowPadding.
    // SetNextWindowContentSize(ImVec2(100,100)) + ImGuiWindowFlags_AlwaysAutoResize will always allow submitting a 100x100 item.
    // C++: void ImGui::SetNextWindowContentSize(const ImVec2& size)
    public static void SetNextWindowContentSize(ImVec2 size)
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasContentSize;
        g.NextWindowData.ContentSizeVal = ImMath.ImTrunc(size);
    }

    // C++: void ImGui::SetNextWindowScroll(const ImVec2& scroll)
    public static void SetNextWindowScroll(ImVec2 scroll)
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasScroll;
        g.NextWindowData.ScrollVal = scroll;
    }

    // C++: void ImGui::SetNextWindowCollapsed(bool collapsed, ImGuiCond cond)
    public static void SetNextWindowCollapsed(bool collapsed, ImGuiCond cond = 0)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(cond == 0 || ImMath.ImIsPowerOfTwo(cond)); // Make sure the user doesn't attempt to combine multiple condition flags.
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasCollapsed;
        g.NextWindowData.CollapsedVal = collapsed;
        g.NextWindowData.CollapsedCond = cond != 0 ? cond : ImGuiCond_Always;
    }

    // C++: void ImGui::SetNextWindowBgAlpha(float alpha)
    public static void SetNextWindowBgAlpha(float alpha)
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasBgAlpha;
        g.NextWindowData.BgAlphaVal = alpha;
    }

    // This is experimental and meant to be a toy for exploring a future/wider range of features.
    // C++: void ImGui::SetNextWindowRefreshPolicy(ImGuiWindowRefreshFlags flags)
    public static void SetNextWindowRefreshPolicy(ImGuiWindowRefreshFlags flags)
    {
        ImGuiContext g = GImGui;
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasRefreshPolicy;
        g.NextWindowData.RefreshFlagsVal = flags;
    }

    // C++: void ImGui::SetNextWindowFlags(ImGuiWindowFlags flags, bool enabled)
    public static void SetNextWindowFlags(ImGuiWindowFlags flags, bool enabled)
    {
        ImGuiContext g = GImGui;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasWindowFlags) == 0)
            g.NextWindowData.WindowFlagsSet = g.NextWindowData.WindowFlagsClear = ImGuiChildFlags_None;
        g.NextWindowData.WindowFlagsSet = enabled ? (g.NextWindowData.WindowFlagsSet | flags) : (g.NextWindowData.WindowFlagsSet & ~flags);
        g.NextWindowData.WindowFlagsClear = !enabled ? (g.NextWindowData.WindowFlagsClear | flags) : (g.NextWindowData.WindowFlagsClear & ~flags);
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasWindowFlags;
    }

    // C++: void ImGui::SetNextWindowChildFlags(ImGuiChildFlags flags, bool enabled)
    public static void SetNextWindowChildFlags(ImGuiChildFlags flags, bool enabled)
    {
        ImGuiContext g = GImGui;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasChildFlags) == 0)
            g.NextWindowData.ChildFlagsSet = g.NextWindowData.ChildFlagsClear = ImGuiChildFlags_None;
        g.NextWindowData.ChildFlagsSet = enabled ? (g.NextWindowData.ChildFlagsSet | flags) : (g.NextWindowData.ChildFlagsSet & ~flags);
        g.NextWindowData.ChildFlagsClear = !enabled ? (g.NextWindowData.ChildFlagsClear | flags) : (g.NextWindowData.ChildFlagsClear & ~flags);
        g.NextWindowData.HasFlags |= ImGuiNextWindowDataFlags_HasChildFlags;
    }

    // C++: ImDrawList* ImGui::GetWindowDrawList()
    public static ImDrawList GetWindowDrawList()
    {
        ImGuiWindow window = GetCurrentWindow();
        return window.DrawList;
    }

    // C++: ImFont* ImGui::GetFont()
    public static ImFont GetFont()
    {
        return GImGui.Font;
    }

    // C++: ImFontBaked* ImGui::GetFontBaked()
    public static ImFontBaked GetFontBaked()
    {
        return GImGui.FontBaked;
    }

    // Get current font size (= height in pixels) of current font, with global scale factors applied.
    // - Use style.FontSizeBase to get value before global scale factors.
    // - recap: ImGui::GetFontSize() == style.FontSizeBase * (style.FontScaleMain * style.FontScaleDpi * other_scaling_factors)
    // C++: float ImGui::GetFontSize()
    public static float GetFontSize()
    {
        return GImGui.FontSize;
    }

    // C++: ImVec2 ImGui::GetFontTexUvWhitePixel()
    public static ImVec2 GetFontTexUvWhitePixel()
    {
        return GImGui.DrawListSharedData.TexUvWhitePixel;
    }

    // Prefer using PushFont(NULL, style.FontSizeBase * factor), or use style.FontScaleMain to scale all windows.
    // C++: void ImGui::SetWindowFontScale(float scale)
    public static void SetWindowFontScale(float scale)
    {
        Debug.Assert(scale > 0.0f);
        ImGuiWindow window = GetCurrentWindow();
        window.FontWindowScale = scale;
        UpdateCurrentFontSize(0.0f);
    }

    // C++: void ImGui::PushFocusScope(ImGuiID id)
    public static void PushFocusScope(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        ImGuiFocusScopeData data;
        data.ID = id;
        data.WindowID = g.CurrentWindow.ID;
        g.FocusScopeStack.push_back(data);
        g.CurrentFocusScopeId = id;
    }

    // C++: void ImGui::PopFocusScope()
    public static void PopFocusScope()
    {
        ImGuiContext g = GImGui;
        if (g.FocusScopeStack.Size <= g.StackSizesInBeginForCurrentWindow.SizeOfFocusScopeStack)
        {
            ImUtil.ImAssertUserError(false, "Calling PopFocusScope() too many times!");
            return;
        }
        g.FocusScopeStack.pop_back();
        g.CurrentFocusScopeId = g.FocusScopeStack.Size != 0 ? g.FocusScopeStack.back().ID : 0;
    }

    // C++: bool ImGui::IsInNavFocusRoute(ImGuiID focus_scope_id)
    public static bool IsInNavFocusRoute(ImGuiID focus_scope_id)
    {
        ImGuiContext g = GImGui;
        if (g.NavFocusScopeId == focus_scope_id)
            return true;
        for (int n = 0; n < g.NavFocusRoute.Size; n++)
        {
            ImGuiFocusScopeData focus_scope = g.NavFocusRoute[n];
            if (focus_scope.ID == focus_scope_id)
                return true;
        }
        return false;
    }

    // C++: void ImGui::SetNavFocusScope(ImGuiID focus_scope_id)
    public static void SetNavFocusScope(ImGuiID focus_scope_id)
    {
        ImGuiContext g = GImGui;
        g.NavFocusScopeId = focus_scope_id;
        g.NavFocusRoute.resize(0); // Invalidate
        if (focus_scope_id == 0)
            return;
        Debug.Assert(g.NavWindow != null);

        // Store current path (in reverse order)
        if (focus_scope_id == g.CurrentFocusScopeId)
        {
            // Top of focus stack contains local focus scopes inside current window
            for (int n = g.FocusScopeStack.Size - 1; n >= 0 && g.FocusScopeStack.Data[n].WindowID == g.CurrentWindow.ID; n--)
                g.NavFocusRoute.push_back(g.FocusScopeStack.Data[n]);
        }
        else if (focus_scope_id == g.NavWindow.NavRootFocusScopeId)
        {
            g.NavFocusRoute.push_back(new ImGuiFocusScopeData { ID = focus_scope_id, WindowID = g.NavWindow.ID });
        }
        else
        {
            return;
        }

        // Then follow on manually set ParentWindowForFocusRoute field (#6798)
        for (ImGuiWindow window = g.NavWindow.ParentWindowForFocusRoute; window != null; window = window.ParentWindowForFocusRoute)
            g.NavFocusRoute.push_back(new ImGuiFocusScopeData { ID = window.NavRootFocusScopeId, WindowID = window.ID });
        Debug.Assert(g.NavFocusRoute.Size < 100); // Maximum depth is technically 251 as per CalcRoutingScore(): 254 - 3
    }

    // Focus = move navigation cursor, set scrolling, set focus window.
    // C++: void ImGui::FocusItem()
    public static void FocusItem()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        //IMGUI_DEBUG_LOG_FOCUS("FocusItem(0x%08x) in window \"%s\"\n", g.LastItemData.ID, window->Name);
        if (g.DragDropActive || g.MovingWindow != null) // FIXME: Opt-in flags for this?
        {
            //IMGUI_DEBUG_LOG_FOCUS("FocusItem() ignored while DragDropActive!\n");
            return;
        }

        int move_flags = ImGuiNavMoveFlags_IsTabbing | ImGuiNavMoveFlags_FocusApi | ImGuiNavMoveFlags_NoSetNavCursorVisible | ImGuiNavMoveFlags_NoSelect;
        int scroll_flags = window.Appearing ? ImGuiScrollFlags_KeepVisibleEdgeX | ImGuiScrollFlags_AlwaysCenterY : ImGuiScrollFlags_KeepVisibleEdgeX | ImGuiScrollFlags_KeepVisibleEdgeY;
        SetNavWindow(window);
        NavMoveRequestSubmit(ImGuiDir_None, ImGuiDir_Up, move_flags, scroll_flags);
        NavMoveRequestResolveWithLastItem(ref g.NavMoveResultLocal);
    }

    // C++: void ImGui::ActivateItemByID(ImGuiID id)
    public static void ActivateItemByID(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        g.NavNextActivateId = id;
        g.NavNextActivateFlags = ImGuiActivateFlags_None;
    }

    // Note: this will likely be called ActivateItem() once we rework our Focus/Activation system!
    // But ActivateItem() should function without altering scroll/focus?
    // C++: void ImGui::SetKeyboardFocusHere(int offset)
    public static void SetKeyboardFocusHere(int offset = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert(offset >= -1);    // -1 is allowed but not below
        //IMGUI_DEBUG_LOG_FOCUS("SetKeyboardFocusHere(%d) in window \"%s\"\n", offset, window->Name);

        // It makes sense in the vast majority of cases to never interrupt a drag and drop.
        // When we refactor this function into ActivateItem() we may want to make this an option.
        // MovingWindow is protected from most user inputs using SetActiveIdUsingNavAndKeys(), but
        // is also automatically dropped in the event g.ActiveId is stolen.
        if (g.DragDropActive || g.MovingWindow != null)
        {
            //IMGUI_DEBUG_LOG_FOCUS("SetKeyboardFocusHere() ignored while DragDropActive!\n");
            return;
        }

        SetNavWindow(window);

        int move_flags = ImGuiNavMoveFlags_IsTabbing | ImGuiNavMoveFlags_Activate | ImGuiNavMoveFlags_FocusApi | ImGuiNavMoveFlags_NoSetNavCursorVisible;
        int scroll_flags = window.Appearing ? ImGuiScrollFlags_KeepVisibleEdgeX | ImGuiScrollFlags_AlwaysCenterY : ImGuiScrollFlags_KeepVisibleEdgeX | ImGuiScrollFlags_KeepVisibleEdgeY;
        NavMoveRequestSubmit(ImGuiDir_None, offset < 0 ? ImGuiDir_Up : ImGuiDir_Down, move_flags, scroll_flags); // FIXME-NAV: Once we refactor tabbing, add LegacyApi flag to not activate non-inputable.
        if (offset == -1)
        {
            NavMoveRequestResolveWithLastItem(ref g.NavMoveResultLocal);
        }
        else
        {
            g.NavTabbingDir = 1;
            g.NavTabbingCounter = offset + 1;
        }
    }

    // C++: void ImGui::SetItemDefaultFocus()
    public static void SetItemDefaultFocus()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (!window.Appearing)
            return;
        if (g.NavWindow != window.RootWindowForNav || (!g.NavInitRequest && g.NavInitResult.ID == 0) || g.NavLayer != window.DC.NavLayerCurrent)
            return;

        g.NavInitRequest = false;
        NavApplyItemToResult(ref g.NavInitResult);
        NavUpdateAnyRequestFlag();

        // Scroll could be done in NavInitRequestApplyResult() via an opt-in flag (we however don't want regular init requests to scroll)
        if (!window.ClipRect.Contains(g.LastItemData.Rect))
            ScrollToRectEx(window, g.LastItemData.Rect, ImGuiScrollFlags_None);
    }

    // C++: void ImGui::SetStateStorage(ImGuiStorage* tree)
    public static void SetStateStorage(ImGuiStorage tree)
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        window.DC.StateStorage = tree != null ? tree : window.StateStorage;
    }

    // C++: ImGuiStorage* ImGui::GetStateStorage()
    public static ImGuiStorage GetStateStorage()
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.DC.StateStorage;
    }

    // C++: bool ImGui::IsRectVisible(const ImVec2& size)
    public static bool IsRectVisible(ImVec2 size)
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.ClipRect.Overlaps(new ImRect(window.DC.CursorPos, window.DC.CursorPos + size));
    }

    // C++: bool ImGui::IsRectVisible(const ImVec2& rect_min, const ImVec2& rect_max)
    public static bool IsRectVisible(ImVec2 rect_min, ImVec2 rect_max)
    {
        ImGuiWindow window = GImGui.CurrentWindow;
        return window.ClipRect.Overlaps(new ImRect(rect_min, rect_max));
    }
}
