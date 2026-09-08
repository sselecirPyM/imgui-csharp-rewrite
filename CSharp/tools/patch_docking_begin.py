import io
p = 'ImGuiWindows2.cs'
s = io.open(p, encoding='utf-8').read()

# --- 1) Appearing flag relocation + FlagsPreviousFrame ---
o = """        window.Appearing = window_just_activated_by_user;
        if (window.Appearing)
            SetWindowConditionAllowFlags(window, ImGuiCond_Appearing, true);

        // Update Flags, LastFrameActive, BeginOrderXXX fields
        if (first_begin_of_the_frame)
        {
            UpdateWindowInFocusOrderList(window, window_just_created, flags);
            window.Flags = flags;"""
n = """        // Update the Appearing flag (note: the BeginDocked() path may also set this to true later)

        // Update Flags, LastFrameActive, BeginOrderXXX fields
        bool window_was_appearing = window.Appearing;
        if (first_begin_of_the_frame)
        {
            UpdateWindowInFocusOrderList(window, window_just_created, flags);
            window.Appearing = window_just_activated_by_user;
            if (window.Appearing)
                SetWindowConditionAllowFlags(window, ImGuiCond_Appearing, true);
            window.FlagsPreviousFrame = window.Flags;
            window.Flags = flags;"""
assert o in s, "1"
s = s.replace(o, n)

# --- 2) Docking block before parent_window_in_stack ---
o = """        // Parent window is latched only on the first call to Begin() of the frame, so further append-calls can be done from a different window stack
        ImGuiWindow parent_window_in_stack = g.CurrentWindowStack.empty() ? null : g.CurrentWindowStack.back().Window;"""
n = """        // Docking
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
        ImGuiWindow parent_window_in_stack = (window.DockIsActive && window.DockNode.HostWindow != null) ? window.DockNode.HostWindow : (g.CurrentWindowStack.empty() ? null : g.CurrentWindowStack.back().Window);"""
assert o in s, "2"
s = s.replace(o, n)

# --- 3) Focus route ---
o = """            // There's little point to expose a flag to set this: because the interesting cases won't be using parent_window_in_stack,
            // e.g. linking a tool window in a standalone viewport to a document window, regardless of their Begin() stack parenting. (#6798)
            window.ParentWindowForFocusRoute = (flags & ImGuiWindowFlags_ChildWindow) != 0 ? parent_window_in_stack : null;

            // Inherent SetWindowFontScale() from parent until we fix this system..."""
n = """            // Focus route
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

            // Inherit SetWindowFontScale() from parent until we fix this system..."""
assert o in s, "3"
s = s.replace(o, n)

# --- 4) WindowClass copy ---
o = """        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasContentSize) != 0)
            window.ContentSizeExplicit = g.NextWindowData.ContentSizeVal;
        else if (first_begin_of_the_frame)
            window.ContentSizeExplicit = new ImVec2(0.0f, 0.0f);
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasCollapsed) != 0)"""
n = """        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasContentSize) != 0)
            window.ContentSizeExplicit = g.NextWindowData.ContentSizeVal;
        else if (first_begin_of_the_frame)
            window.ContentSizeExplicit = new ImVec2(0.0f, 0.0f);
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasWindowClass) != 0)
            window.WindowClass = g.NextWindowData.WindowClass;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasCollapsed) != 0)"""
assert o in s, "4"
s = s.replace(o, n)

# --- 5) DockNodeHost channels split ---
o = """            window.IDStack.resize(1);
            window.DrawList._ResetForNewFrame();
            window.DC.CurrentTableIdx = -1;

            // Restore buffer capacity when woken from a compacted state, to avoid"""
n = """            window.IDStack.resize(1);
            window.DrawList._ResetForNewFrame();
            window.DC.CurrentTableIdx = -1;
            if ((flags & ImGuiWindowFlags_DockNodeHost) != 0)
            {
                window.DrawList._Splitter.Split(window.DrawList, 2); // C++: ChannelsSplit(2)
                window.DrawList._Splitter.SetCurrentChannel(window.DrawList, DOCKING_HOST_DRAW_CHANNEL_FG); // Render decorations on channel 1 as we will render the backgrounds manually later
            }

            // Restore buffer capacity when woken from a compacted state, to avoid"""
assert o in s, "5"
s = s.replace(o, n)

# --- 6) window_title_visible_elsewhere ---
o = """            bool window_title_visible_elsewhere = false;
            if (g.NavWindowingListWindow != null && g.NavWindowingListWindow.WasActive && (flags & ImGuiWindowFlags_NoNavFocus) == 0)   // Window titles visible when using Ctrl+Tab
                window_title_visible_elsewhere = true;
            if ((flags & ImGuiWindowFlags_ChildMenu) != 0)
                window_title_visible_elsewhere = true;"""
n = """            bool window_title_visible_elsewhere = false;
            if ((window.Viewport != null && window.Viewport.Window == window) || window.DockIsActive)
                window_title_visible_elsewhere = true;
            else if (g.NavWindowingListWindow != null && g.NavWindowingListWindow.WasActive && (flags & ImGuiWindowFlags_NoNavFocus) == 0)   // Window titles visible when using Ctrl+Tab
                window_title_visible_elsewhere = true;
            else if ((flags & ImGuiWindowFlags_ChildMenu) != 0)
                window_title_visible_elsewhere = true;"""
assert o in s, "6"
s = s.replace(o, n)

# --- 7) SELECT VIEWPORT ---
o = """            // SELECT VIEWPORT
            // FIXME-VIEWPORT: In the docking/viewport branch, this is the point where we select the current viewport (which may affect the style)

            ImGuiViewportP viewport = (ImGuiViewportP)GetMainViewport();
            SetWindowViewport(window, viewport);
            SetCurrentWindow(window);

            // LOCK BORDER SIZE AND PADDING FOR THE FRAME (so that altering them doesn't cause inconsistencies)

            if ((flags & ImGuiWindowFlags_ChildWindow) != 0)
                window.WindowBorderSize = style.ChildBorderSize;
            else
                window.WindowBorderSize = ((flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_Tooltip)) != 0 && (flags & ImGuiWindowFlags_Modal) == 0) ? style.PopupBorderSize : style.WindowBorderSize;
            window.WindowPadding = style.WindowPadding;
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && (flags & ImGuiWindowFlags_Popup) == 0 && (window.ChildFlags & ImGuiChildFlags_AlwaysUseWindowPadding) == 0 && window.WindowBorderSize == 0.0f)
                window.WindowPadding = new ImVec2(0.0f, (flags & ImGuiWindowFlags_MenuBar) != 0 ? style.WindowPadding.y : 0.0f);"""
n = """            // SELECT VIEWPORT
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
                window.WindowPadding = new ImVec2(0.0f, (flags & ImGuiWindowFlags_MenuBar) != 0 ? style.WindowPadding.y : 0.0f);"""
assert o in s, "7"
s = s.replace(o, n)

# --- 8) Collapse dbl-click dock skip ---
o = "            if ((flags & ImGuiWindowFlags_NoTitleBar) == 0 && (flags & ImGuiWindowFlags_NoCollapse) == 0)\n            {\n                // We don't use a regular button+id to test for double-click on title bar (mostly due to legacy reason, could be fixed),"
n = "            if ((flags & ImGuiWindowFlags_NoTitleBar) == 0 && (flags & ImGuiWindowFlags_NoCollapse) == 0 && !window.DockIsActive)\n            {\n                // We don't use a regular button+id to test for double-click on title bar (mostly due to legacy reason, could be fixed),"
assert o in s, "8"
s = s.replace(o, n)

# --- 9) Late viewport create + ViewportOwned sync + viewport rects + clamping ---
o = """            // Calculate the range of allowed position for that window (to be movable and visible past safe area padding)
            // When clamping to stay visible, we will enforce that window->Pos stays inside of visibility_rect.
            ImRect viewport_rect = viewport.GetMainRect();
            ImRect viewport_work_rect = viewport.GetWorkRect();
            ImVec2 visibility_padding = ImMath.ImMax(style.DisplayWindowPadding, style.DisplaySafeAreaPadding);
            ImRect visibility_rect = new ImRect(viewport_work_rect.Min + visibility_padding, viewport_work_rect.Max - visibility_padding);

            // Clamp position/size so window stays visible within its viewport or monitor
            // Ignore zero-sized display explicitly to avoid losing positions if a window manager reports zero-sized window when initializing or minimizing.
            if (!window_pos_set_by_api && (flags & ImGuiWindowFlags_ChildWindow) == 0)
                if (viewport_rect.GetWidth() > 0.0f && viewport_rect.GetHeight() > 0.0f)
                    ClampWindowPos(window, visibility_rect);
            window.Pos = ImMath.ImTrunc(window.Pos);

            // Lock window rounding for the frame (so that altering them doesn't cause inconsistencies)
            // Large values tend to lead to variety of artifacts and are not recommended.
            window.WindowRounding = (flags & ImGuiWindowFlags_ChildWindow) != 0 ? style.ChildRounding : (((flags & ImGuiWindowFlags_Popup) != 0 && (flags & ImGuiWindowFlags_Modal) == 0) ? style.PopupRounding : style.WindowRounding);"""
n = """            // Late create viewport if we don't fit within our current host viewport.
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
                window.WindowRounding = ((flags & ImGuiWindowFlags_Popup) != 0 && (flags & ImGuiWindowFlags_Modal) == 0) ? style.PopupRounding : style.WindowRounding;"""
assert o in s, "9"
s = s.replace(o, n)

# --- 10) want_focus ---
o = """                if ((flags & ImGuiWindowFlags_Popup) != 0)
                    want_focus = true;
                else if ((flags & (ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_Tooltip)) == 0)
                    want_focus = true;"""
n = """                if ((flags & ImGuiWindowFlags_Popup) != 0)
                    want_focus = true;
                else if ((window.DockIsActive || (flags & ImGuiWindowFlags_ChildWindow) == 0) && (flags & ImGuiWindowFlags_Tooltip) == 0)
                    want_focus = true;"""
assert o in s, "10"
s = s.replace(o, n)

# --- 11) handle_borders_and_resize_grips ---
o = """            bool handle_borders_and_resize_grips = true;
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && window.ParentWindow.SkipItems)
                handle_borders_and_resize_grips = false;"""
n = """            bool handle_borders_and_resize_grips = (window.DockNodeAsHost != null || !window.DockIsActive);
            if ((flags & ImGuiWindowFlags_ChildWindow) != 0 && window.ParentWindow.SkipItems)
                handle_borders_and_resize_grips = false;"""
assert o in s, "11"
s = s.replace(o, n)

io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print("part1 done")
