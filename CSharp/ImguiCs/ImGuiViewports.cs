// Port of Dear ImGui imgui.cpp [SECTION] VIEWPORTS, PLATFORM WINDOWS (docking branch).
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    //-----------------------------------------------------------------------------
    // [SECTION] VIEWPORTS, PLATFORM WINDOWS
    //-----------------------------------------------------------------------------
    // - GetMainViewport()                  (already ported in ImGuiInput.cs)
    // - FindViewportByID()
    // - FindViewportByPlatformHandle()
    // - SetCurrentViewport() [Internal]
    // - SetWindowViewport() [Internal]     (docking-branch version; master version in ImGuiMiscHelpers.cs is superseded)
    // - GetWindowAlwaysWantOwnViewport() [Internal]
    // - UpdateTryMergeWindowIntoHostViewport() [Internal]
    // - UpdateTryMergeWindowIntoHostViewports() [Internal]
    // - TranslateWindowsInViewport() [Internal] (+ TranslateWindow() helper from imgui.cpp)
    // - ScaleWindowsInViewport() [Internal]     (+ ScaleWindow() helper from imgui.cpp)
    // - FindHoveredViewportFromPlatformWindowStack() [Internal]
    // - UpdateViewportsNewFrame() [Internal]    (docking-branch version; master version in ImGuiInput.cs is superseded)
    // - UpdateViewportsEndFrame() [Internal]
    // - AddUpdateViewport() [Internal]
    // - WindowSelectViewport() [Internal]
    // - WindowSyncOwnedViewport() [Internal]
    // - UpdatePlatformWindows()
    // - RenderPlatformWindowsDefault()
    // - FindPlatformMonitorForPos() [Internal]
    // - FindPlatformMonitorForRect() [Internal]
    // - UpdateViewportPlatformMonitor() [Internal]
    // - DestroyPlatformWindow() [Internal]
    // - DestroyPlatformWindows()
    // (ImGuiViewport::GetDebugName(), ImGuiPlatformIO::ClearPlatformHandlers()/ClearRendererHandlers()
    //  are already ported in ImGuiPublicTypes.cs / ImGuiUtil.cs.)
    //-----------------------------------------------------------------------------

    // FIXME: This leaks access to viewports not listed in PlatformIO.Viewports[]. Problematic? (#4236)
    // ImGuiViewport* ImGui::FindViewportByID(ImGuiID viewport_id)
    public static ImGuiViewport FindViewportByID(ImGuiID viewport_id)
    {
        ImGuiContext g = GImGui;
        for (int n = 0; n < g.Viewports.Size; n++)
            if (g.Viewports[n].ID == viewport_id)
                return g.Viewports[n];
        return null;
    }

    // ImGuiViewport* ImGui::FindViewportByPlatformHandle(void* platform_handle)
    public static ImGuiViewport FindViewportByPlatformHandle(object platform_handle)
    {
        ImGuiContext g = GImGui;
        for (int n = 0; n < g.Viewports.Size; n++)
            if (g.Viewports[n].PlatformHandle == platform_handle)
                return g.Viewports[n];
        return null;
    }

    // void ImGui::SetCurrentViewport(ImGuiWindow* current_window, ImGuiViewportP* viewport)
    public static void SetCurrentViewport(ImGuiWindow current_window, ImGuiViewportP viewport)
    {
        ImGuiContext g = GImGui;
        // (void)current_window; // Unused

        if (viewport != null)
            viewport.LastFrameActive = g.FrameCount;
        if (g.CurrentViewport == viewport)
            return;
        g.CurrentDpiScale = viewport != null ? viewport.DpiScale : 1.0f;
        g.CurrentViewport = viewport;
        Debug.Assert(g.CurrentDpiScale > 0.0f && g.CurrentDpiScale < 99.0f); // Typical correct values would be between 1.0f and 4.0f
        //IMGUI_DEBUG_LOG_VIEWPORT("[viewport] SetCurrentViewport changed '%s' 0x%08X\n", current_window ? current_window->Name : NULL, viewport ? viewport->ID : 0);
        if (g.IO.ConfigDpiScaleFonts)
            g.Style.FontScaleDpi = g.CurrentDpiScale;

        // Notify platform layer of viewport changes
        // FIXME-DPI: This is only currently used for experimenting with handling of multiple DPI
        if (g.CurrentViewport != null && g.PlatformIO.Platform_OnChangedViewport != null)
            g.PlatformIO.Platform_OnChangedViewport(g.CurrentViewport);
    }

    // void ImGui::SetWindowViewport(ImGuiWindow* window, ImGuiViewportP* viewport)
    // (docking-branch version: also maintains ViewportId/ViewportOwned; supersedes the master-branch version in ImGuiMiscHelpers.cs)
    public static void SetWindowViewport(ImGuiWindow window, ImGuiViewportP viewport)
    {
        // Abandon viewport
        if (window.ViewportOwned && window.Viewport.Window == window)
            window.Viewport.Size = new ImVec2(0.0f, 0.0f);

        window.Viewport = viewport;
        window.ViewportId = viewport.ID;
        window.ViewportOwned = (viewport.Window == window);
    }

    // C++ static: bool ImGui::GetWindowAlwaysWantOwnViewport(ImGuiWindow* window)
    public static bool GetWindowAlwaysWantOwnViewport(ImGuiWindow window)
    {
        // Tooltips and menus are not automatically forced into their own viewport when the NoMerge flag is set, however the multiplication of viewports makes them more likely to protrude and create their own.
        ImGuiContext g = GImGui;
        if (g.IO.ConfigViewportsNoAutoMerge || (window.WindowClass.ViewportFlagsOverrideSet & ImGuiViewportFlags_NoAutoMerge) != 0)
            if ((g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable) != 0)
                if (!window.DockIsActive)
                    if ((window.Flags & (ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_ChildMenu | ImGuiWindowFlags_Tooltip)) == 0)
                        if ((window.Flags & ImGuiWindowFlags_Popup) == 0 || (window.Flags & ImGuiWindowFlags_Modal) != 0)
                            return true;
        return false;
    }

    // C++ static (file-scope): bool IsViewportAbove(ImGuiViewportP* potential_above, ImGuiViewportP* potential_below)
    // Heuristic, see #8948: depends on how backends handle OS-level parenting.
    // Due to how parent viewport stack is layed out, note that IsViewportAbove(a,b) isn't always the same as !IsViewportAbove(b,a).
    static bool IsViewportAbove(ImGuiViewportP potential_above, ImGuiViewportP potential_below)
    {
        // If ImGuiBackendFlags_HasParentViewport if set, ->ParentViewport chain should be accurate.
        ImGuiContext g = GImGui;
        if ((g.IO.BackendFlags & ImGuiBackendFlags_HasParentViewport) != 0)
        {
            for (ImGuiViewport v = potential_above; v != null && v.ParentViewport != null; v = v.ParentViewport)
                if (v.ParentViewport == potential_below)
                    return true;
        }
        else
        {
            if (potential_above.ParentViewport == potential_below)
                return true;
        }

        if (potential_above.LastFocusedStampCount > potential_below.LastFocusedStampCount)
            return true;
        return false;
    }

    // C++ static: bool ImGui::UpdateTryMergeWindowIntoHostViewport(ImGuiWindow* window, ImGuiViewportP* viewport_dst)
    public static bool UpdateTryMergeWindowIntoHostViewport(ImGuiWindow window, ImGuiViewportP viewport_dst)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(window == window.RootWindowDockTree);
        ImGuiViewportP viewport_src = window.Viewport; // Current viewport
        if (viewport_src == viewport_dst)
            return false;
        if ((viewport_dst.Flags & ImGuiViewportFlags_CanHostOtherWindows) == 0)
            return false;
        if ((viewport_dst.Flags & ImGuiViewportFlags_IsMinimized) != 0)
            return false;
        if (!viewport_dst.GetMainRect().Contains(window.Rect()))
            return false;
        if (GetWindowAlwaysWantOwnViewport(window))
            return false;

        for (int n = 0; n < g.Viewports.Size; n++)
        {
            ImGuiViewportP viewport_obstructing = g.Viewports[n];
            if (viewport_obstructing == viewport_src || viewport_obstructing == viewport_dst || !viewport_obstructing.PlatformWindowCreated)
                continue;
            if (viewport_obstructing.GetMainRect().Overlaps(window.Rect()))
                if (IsViewportAbove(viewport_obstructing, viewport_dst))
                    if (viewport_src == null || IsViewportAbove(viewport_src, viewport_obstructing))
                        return false; // viewport_obstructing is between viewport_src and viewport_dst -> Cannot merge.
        }

        // Move to the existing viewport, Move child/hosted windows as well (FIXME-OPT: iterate child)
        IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Window '{0}' merge into Viewport 0X{1:X8}\n", window.Name, viewport_dst.ID);
        if (window.ViewportOwned)
            for (int n = 0; n < g.Windows.Size; n++)
                if (g.Windows[n].Viewport == viewport_src)
                    SetWindowViewport(g.Windows[n], viewport_dst);
        SetWindowViewport(window, viewport_dst);
        if ((window.Flags & ImGuiWindowFlags_NoBringToFrontOnFocus) == 0)
            BringWindowToDisplayFront(window);

        return true;
    }

    // C++ static: bool ImGui::UpdateTryMergeWindowIntoHostViewports(ImGuiWindow* window)
    // FIXME: handle 0 to N host viewports
    public static bool UpdateTryMergeWindowIntoHostViewports(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        return UpdateTryMergeWindowIntoHostViewport(window, g.Viewports[0]);
    }

    // C++ static (imgui.cpp, ported here because TranslateWindowsInViewport() depends on it):
    // static void TranslateWindow(ImGuiWindow* window, const ImVec2& delta)
    // This is called during NewFrame()->UpdateViewportsNewFrame() only.
    // Need to keep in sync with SetWindowPos()
    public static void TranslateWindow(ImGuiWindow window, ImVec2 delta)
    {
        window.Pos += delta;
        window.ClipRect.Translate(delta);
        window.OuterRectClipped.Translate(delta);
        window.InnerRect.Translate(delta);
        window.DC.CursorPos += delta;
        window.DC.CursorStartPos += delta;
        window.DC.CursorMaxPos += delta;
        window.DC.IdealMaxPos += delta;
    }

    // Translate Dear ImGui windows when a Host Viewport has been moved
    // (This additionally keeps windows at the same place when ImGuiConfigFlags_ViewportsEnable is toggled!)
    // void ImGui::TranslateWindowsInViewport(ImGuiViewportP* viewport, const ImVec2& old_pos, const ImVec2& new_pos, const ImVec2& old_size, const ImVec2& new_size)
    public static void TranslateWindowsInViewport(ImGuiViewportP viewport, ImVec2 old_pos, ImVec2 new_pos, ImVec2 old_size, ImVec2 new_size)
    {
        ImGuiContext g = GImGui;
        //IMGUI_DEBUG_LOG_VIEWPORT("[viewport] TranslateWindowsInViewport 0x%08X\n", viewport->ID);
        Debug.Assert(viewport.Window == null && (viewport.Flags & ImGuiViewportFlags_CanHostOtherWindows) != 0);

        // 1) We test if ImGuiConfigFlags_ViewportsEnable was just toggled, which allows us to conveniently
        // translate imgui windows from OS-window-local to absolute coordinates or vice-versa.
        // 2) If it's not going to fit into the new size, keep it at same absolute position.
        // One problem with this is that most Win32 applications doesn't update their render while dragging,
        // and so the window will appear to teleport when releasing the mouse.
        bool translate_all_windows = (g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable) != (g.ConfigFlagsLastFrame & ImGuiConfigFlags_ViewportsEnable);
        ImRect test_still_fit_rect = new ImRect(old_pos, old_pos + old_size);
        ImVec2 delta_pos = new_pos - old_pos;
        for (int n = 0; n < g.Windows.Size; n++) // FIXME-OPT
        {
            ImGuiWindow window = g.Windows[n];
            if (translate_all_windows || (window.Viewport == viewport && (old_size == new_size || test_still_fit_rect.Contains(window.Rect()))))
                TranslateWindow(window, delta_pos);
        }
    }

    // C++ static (imgui.cpp, ported here because ScaleWindowsInViewport() depends on it):
    // static void ScaleWindow(ImGuiWindow* window, float scale)
    public static void ScaleWindow(ImGuiWindow window, float scale)
    {
        ImVec2 origin = window.Viewport.Pos;
        window.Pos = ImMath.ImFloor((window.Pos - origin) * scale + origin);
        window.Size = ImMath.ImTrunc(window.Size * scale);
        window.SizeFull = ImMath.ImTrunc(window.SizeFull * scale);
        window.ContentSize = ImMath.ImTrunc(window.ContentSize * scale);
    }

    // Scale all windows (position, size). Use when e.g. changing DPI. (This is a lossy operation!)
    // void ImGui::ScaleWindowsInViewport(ImGuiViewportP* viewport, float scale)
    public static void ScaleWindowsInViewport(ImGuiViewportP viewport, float scale)
    {
        ImGuiContext g = GImGui;
        //IMGUI_DEBUG_LOG_VIEWPORT("[viewport] ScaleWindowsInViewport 0x%08X\n", viewport->ID);
        if (viewport.Window != null)
        {
            ScaleWindow(viewport.Window, scale);
        }
        else
        {
            for (int n = 0; n < g.Windows.Size; n++)
            {
                ImGuiWindow window = g.Windows[n];
                if (window.Viewport == viewport)
                    ScaleWindow(window, scale);
            }
        }
    }

    // If the backend doesn't support ImGuiBackendFlags_HasMouseHoveredViewport or doesn't honor ImGuiViewportFlags_NoInputs for it, we do a search ourselves.
    // A) It won't take account of the possibility that non-imgui windows may be in-between our dragged window and our target window.
    // B) It requires Platform_GetWindowFocus to be implemented by backend.
    // ImGuiViewportP* ImGui::FindHoveredViewportFromPlatformWindowStack(const ImVec2& mouse_platform_pos)
    public static ImGuiViewportP FindHoveredViewportFromPlatformWindowStack(ImVec2 mouse_platform_pos)
    {
        ImGuiContext g = GImGui;
        ImGuiViewportP best_candidate = null;
        for (int n = 0; n < g.Viewports.Size; n++)
        {
            ImGuiViewportP viewport = g.Viewports[n];
            if ((viewport.Flags & (ImGuiViewportFlags_NoInputs | ImGuiViewportFlags_IsMinimized)) == 0 && viewport.GetMainRect().Contains(mouse_platform_pos))
                if (best_candidate == null || best_candidate.LastFocusedStampCount < viewport.LastFocusedStampCount)
                    if (viewport.PlatformWindowCreated)
                        best_candidate = viewport;
        }
        return best_candidate;
    }

    // Update viewports and monitor infos
    // Note that this is running even if 'ImGuiConfigFlags_ViewportsEnable' is not set, in order to clear unused viewports (if any) and update monitor info.
    // C++ static: void ImGui::UpdateViewportsNewFrame() (docking-branch version; supersedes the master-branch version in ImGuiInput.cs)
    public static void UpdateViewportsNewFrame()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.PlatformIO.Viewports.Size <= g.Viewports.Size);

        // Update Minimized status (we need it first in order to decide if we'll apply Pos/Size of the main viewport)
        // Update Focused status
        bool viewports_enabled = (g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable) != 0;
        if (viewports_enabled)
        {
            ImGuiViewportP focused_viewport = null;
            for (int n = 0; n < g.Viewports.Size; n++)
            {
                ImGuiViewportP viewport = g.Viewports[n];
                bool platform_funcs_available = viewport.PlatformWindowCreated;
                if (g.PlatformIO.Platform_GetWindowMinimized != null && platform_funcs_available)
                {
                    bool is_minimized = g.PlatformIO.Platform_GetWindowMinimized(viewport);
                    if (is_minimized)
                        viewport.Flags |= ImGuiViewportFlags_IsMinimized;
                    else
                        viewport.Flags &= ~ImGuiViewportFlags_IsMinimized;
                }

                // Update our implicit z-order knowledge of platform windows, which is used when the backend cannot provide io.MouseHoveredViewport.
                // When setting Platform_GetWindowFocus, it is expected that the platform backend can handle calls without crashing if it doesn't have data stored.
                if (g.PlatformIO.Platform_GetWindowFocus != null && platform_funcs_available)
                {
                    bool is_focused = g.PlatformIO.Platform_GetWindowFocus(viewport);
                    if (is_focused)
                        viewport.Flags |= ImGuiViewportFlags_IsFocused;
                    else
                        viewport.Flags &= ~ImGuiViewportFlags_IsFocused;
                    if (is_focused)
                        focused_viewport = viewport;
                }
            }

            // Focused viewport has changed?
            if (focused_viewport != null && g.PlatformLastFocusedViewportId != focused_viewport.ID)
            {
                IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Focused viewport changed {0:X8} -> {1:X8} '{2}', attempting to apply our focus.\n", g.PlatformLastFocusedViewportId, focused_viewport.ID, focused_viewport.Window != null ? focused_viewport.Window.Name : "n/a");
                ImGuiViewportP prev_focused_viewport = (ImGuiViewportP)FindViewportByID(g.PlatformLastFocusedViewportId);
                bool prev_focused_has_been_destroyed = (prev_focused_viewport == null) || (prev_focused_viewport.PlatformWindowCreated == false);

                // Store a tag so we can infer z-order easily from all our windows
                // We compare PlatformLastFocusedViewportId so newly created viewports with _NoFocusOnAppearing flag
                // will keep the front most stamp instead of losing it back to their parent viewport.
                if (focused_viewport.LastFocusedStampCount != g.ViewportFocusedStampCount)
                    focused_viewport.LastFocusedStampCount = ++g.ViewportFocusedStampCount;
                g.PlatformLastFocusedViewportId = focused_viewport.ID;

                // Focus associated dear imgui window
                // - if focus didn't happen with a click within imgui boundaries, e.g. Clicking platform title bar. (#6299)
                // - if focus didn't happen because we destroyed another window (#6462)
                // FIXME: perhaps 'FocusTopMostWindowUnderOne()' can handle the 'focused_window->Window != NULL' case as well.
                bool apply_imgui_focus_on_focused_viewport = !IsAnyMouseDown() && !prev_focused_has_been_destroyed;
                if (apply_imgui_focus_on_focused_viewport && g.IO.ConfigViewportsPlatformFocusSetsImGuiFocus)
                {
                    focused_viewport.LastFocusedHadNavWindow |= (g.NavWindow != null) && (g.NavWindow.Viewport == focused_viewport); // Update so a window changing viewport won't lose focus.
                    ImGuiFocusRequestFlags focus_request_flags = ImGuiFocusRequestFlags_UnlessBelowModal | ImGuiFocusRequestFlags_RestoreFocusedChild;
                    if (focused_viewport.Window != null)
                        FocusWindow(focused_viewport.Window, focus_request_flags);
                    else if (focused_viewport.LastFocusedHadNavWindow)
                        FocusTopMostWindowUnderOne(null, null, focused_viewport, focus_request_flags); // Focus top most in viewport
                    else
                        FocusWindow(null, focus_request_flags); // No window had focus last time viewport was focused
                }
            }
            if (focused_viewport != null)
                focused_viewport.LastFocusedHadNavWindow = (g.NavWindow != null) && (g.NavWindow.Viewport == focused_viewport);
        }

        // Create/update main viewport with current platform position.
        // FIXME-VIEWPORT: Size is driven by backend/user code for backward-compatibility but we should aim to make this more consistent.
        ImGuiViewportP main_viewport = g.Viewports[0];
        Debug.Assert(main_viewport.ID == IMGUI_VIEWPORT_DEFAULT_ID);
        Debug.Assert(main_viewport.Window == null);
        ImVec2 main_viewport_pos = viewports_enabled ? g.PlatformIO.Platform_GetWindowPos(main_viewport) : new ImVec2(0.0f, 0.0f);
        ImVec2 main_viewport_size = g.IO.DisplaySize;
        if (viewports_enabled && (main_viewport.Flags & ImGuiViewportFlags_IsMinimized) != 0)
        {
            main_viewport_pos = main_viewport.Pos; // Preserve last pos/size when minimized (FIXME: We don't do the same for Size outside of the viewport path)
            main_viewport_size = main_viewport.Size;
        }
        AddUpdateViewport(null, IMGUI_VIEWPORT_DEFAULT_ID, main_viewport_pos, main_viewport_size, ImGuiViewportFlags_OwnedByApp | ImGuiViewportFlags_CanHostOtherWindows);

        float memory_compact_start_time = (g.GcCompactAll || g.IO.ConfigMemoryCompactTimer < 0.0f) ? float.MaxValue : (float)g.Time - g.IO.ConfigMemoryCompactTimer;

        g.CurrentDpiScale = 0.0f;
        g.CurrentViewport = null;
        g.MouseViewport = null;
        for (int n = 0; n < g.Viewports.Size; n++)
        {
            ImGuiViewportP viewport = g.Viewports[n];
            viewport.Idx = n;

            // Erase unused viewports
            if (n > 0 && viewport.LastFrameActive < g.FrameCount - 2)
            {
                DestroyViewport(viewport);
                n--;
                continue;
            }

            bool platform_funcs_available = viewport.PlatformWindowCreated;
            if (viewports_enabled)
            {
                // Update Position and Size (from Platform Window to ImGui) if requested.
                // We do it early in the frame instead of waiting for UpdatePlatformWindows() to avoid a frame of lag when moving/resizing using OS facilities.
                if ((viewport.Flags & ImGuiViewportFlags_IsMinimized) == 0 && platform_funcs_available)
                {
                    // Viewport->WorkPos and WorkSize will be updated below
                    if (viewport.PlatformRequestMove)
                        viewport.Pos = viewport.LastPlatformPos = g.PlatformIO.Platform_GetWindowPos(viewport);
                    if (viewport.PlatformRequestResize)
                        viewport.Size = viewport.LastPlatformSize = g.PlatformIO.Platform_GetWindowSize(viewport);
                    if (g.PlatformIO.Platform_GetWindowFramebufferScale != null)
                        viewport.FramebufferScale = g.PlatformIO.Platform_GetWindowFramebufferScale(viewport);
                }
            }

            // Update/copy monitor info
            UpdateViewportPlatformMonitor(viewport);

            // Lock down space taken by menu bars and status bars + query initial insets from backend
            // Setup initial value for functions like BeginMainMenuBar(), DockSpaceOverViewport() etc.
            viewport.WorkInsetMin = viewport.BuildWorkInsetMin;
            viewport.WorkInsetMax = viewport.BuildWorkInsetMax;
            viewport.BuildWorkInsetMin = viewport.BuildWorkInsetMax = new ImVec2(0.0f, 0.0f);
            if (g.PlatformIO.Platform_GetWindowWorkAreaInsets != null && platform_funcs_available)
            {
                ImVec4 insets = g.PlatformIO.Platform_GetWindowWorkAreaInsets(viewport);
                Debug.Assert(insets.x >= 0.0f && insets.y >= 0.0f && insets.z >= 0.0f && insets.w >= 0.0f);
                viewport.BuildWorkInsetMin = new ImVec2(insets.x, insets.y);
                viewport.BuildWorkInsetMax = new ImVec2(insets.z, insets.w);
            }
            viewport.UpdateWorkRect();

            // Garbage collect transient buffers of recently BG/FG drawlists
            // (C++ frees the draw list here via IM_DELETE(); C# relies on the GC.)
            for (int dl_n = 0; dl_n < viewport.BgFgDrawLists.Length; dl_n++)
                if (viewport.BgFgDrawListsLastTimeActive[dl_n] < memory_compact_start_time && viewport.BgFgDrawLists[dl_n] != null)
                    viewport.BgFgDrawLists[dl_n] = null;

            // Reset alpha every frame. Users of transparency (docking) needs to request a lower alpha back.
            viewport.Alpha = 1.0f;

            // Translate Dear ImGui windows when a Host Viewport has been moved
            // (This additionally keeps windows at the same place when ImGuiConfigFlags_ViewportsEnable is toggled!)
            ImVec2 viewport_delta_pos = viewport.Pos - viewport.LastPos;
            if ((viewport.Flags & ImGuiViewportFlags_CanHostOtherWindows) != 0 && (viewport_delta_pos.x != 0.0f || viewport_delta_pos.y != 0.0f))
                TranslateWindowsInViewport(viewport, viewport.LastPos, viewport.Pos, viewport.LastSize, viewport.Size);

            // Update DPI scale
            float new_dpi_scale;
            if (g.PlatformIO.Platform_GetWindowDpiScale != null && platform_funcs_available)
                new_dpi_scale = g.PlatformIO.Platform_GetWindowDpiScale(viewport);
            else if (viewport.PlatformMonitor != -1)
                new_dpi_scale = g.PlatformIO.Monitors[viewport.PlatformMonitor].DpiScale;
            else
                new_dpi_scale = (viewport.DpiScale != 0.0f) ? viewport.DpiScale : 1.0f;
            Debug.Assert(new_dpi_scale > 0.0f && new_dpi_scale < 99.0f); // Typical correct values would be between 1.0f and 4.0f
            if (viewport.DpiScale != 0.0f && new_dpi_scale != viewport.DpiScale)
            {
                float scale_factor = new_dpi_scale / viewport.DpiScale;
                if (g.IO.ConfigDpiScaleViewports)
                    ScaleWindowsInViewport(viewport, scale_factor);
                //if (viewport == GetMainViewport())
                //    g.PlatformInterface.SetWindowSize(viewport, viewport->Size * scale_factor);

                // Scale our window moving pivot so that the window will rescale roughly around the mouse position.
                // FIXME-VIEWPORT: This currently creates a resizing feedback loop when a window is straddling a DPI transition border.
                // (Minor: since our sizes do not perfectly linearly scale, deferring the click offset scale until we know the actual window scale ratio may get us slightly more precise mouse positioning.)
                //if (g.MovingWindow != NULL && g.MovingWindow->Viewport == viewport)
                //    g.ActiveIdClickOffset = ImTrunc(g.ActiveIdClickOffset * scale_factor);
            }
            viewport.DpiScale = new_dpi_scale;
        }

        // Update fallback monitor
        g.PlatformMonitorsFullWorkRect = new ImRect(+float.MaxValue, +float.MaxValue, -float.MaxValue, -float.MaxValue);
        if (g.PlatformIO.Monitors.Size == 0)
        {
            ImGuiPlatformMonitor monitor = g.FallbackMonitor;
            monitor.MainPos = main_viewport.Pos;
            monitor.MainSize = main_viewport.Size;
            monitor.WorkPos = main_viewport.WorkPos;
            monitor.WorkSize = main_viewport.WorkSize;
            monitor.DpiScale = main_viewport.DpiScale;
            g.PlatformMonitorsFullWorkRect.Add(monitor.WorkPos);
            g.PlatformMonitorsFullWorkRect.Add(monitor.WorkPos + monitor.WorkSize);
        }
        else
        {
            g.FallbackMonitor = g.PlatformIO.Monitors[0];
        }
        for (int monitor_n = 0; monitor_n < g.PlatformIO.Monitors.Size; monitor_n++)
        {
            ImGuiPlatformMonitor monitor = g.PlatformIO.Monitors[monitor_n];
            g.PlatformMonitorsFullWorkRect.Add(monitor.WorkPos);
            g.PlatformMonitorsFullWorkRect.Add(monitor.WorkPos + monitor.WorkSize);
        }

        if (!viewports_enabled)
        {
            g.MouseViewport = main_viewport;
            return;
        }

        // Mouse handling: decide on the actual mouse viewport for this frame between the active/focused viewport and the hovered viewport.
        // Note that 'viewport_hovered' should skip over any viewport that has the ImGuiViewportFlags_NoInputs flags set.
        ImGuiViewportP viewport_hovered = null;
        if ((g.IO.BackendFlags & ImGuiBackendFlags_HasMouseHoveredViewport) != 0)
        {
            viewport_hovered = g.IO.MouseHoveredViewport != 0 ? (ImGuiViewportP)FindViewportByID(g.IO.MouseHoveredViewport) : null;
            if (viewport_hovered != null && (viewport_hovered.Flags & ImGuiViewportFlags_NoInputs) != 0)
                viewport_hovered = FindHoveredViewportFromPlatformWindowStack(g.IO.MousePos); // Backend failed to handle _NoInputs viewport: revert to our fallback.
        }
        else
        {
            // If the backend doesn't know how to honor ImGuiViewportFlags_NoInputs, we do a search ourselves. Note that this search:
            // A) won't take account of the possibility that non-imgui windows may be in-between our dragged window and our target window.
            // B) won't take account of how the backend apply parent<>child relationship to secondary viewports, which affects their Z order.
            // C) uses LastFocusedStampCount as a flawed replacement for the last time a window was focused (we could/should fix that by introducing Focus functions in PlatformIO)
            viewport_hovered = FindHoveredViewportFromPlatformWindowStack(g.IO.MousePos);
        }
        if (viewport_hovered != null)
            g.MouseLastHoveredViewport = viewport_hovered;
        else if (g.MouseLastHoveredViewport == null)
            g.MouseLastHoveredViewport = g.Viewports[0];

        // Update mouse reference viewport
        // (when moving a window we aim at its viewport, but this will be overwritten below if we go in drag and drop mode)
        // (MovingViewport->Viewport will be NULL in the rare situation where the window disappared while moving, set UpdateMouseMovingWindowNewFrame() for details)
        if (g.MovingWindow != null && g.MovingWindow.Viewport != null)
            g.MouseViewport = g.MovingWindow.Viewport;
        else
            g.MouseViewport = g.MouseLastHoveredViewport;

        // When dragging something, always refer to the last hovered viewport.
        // - when releasing a moving window we will revert to aiming behind (at viewport_hovered)
        // - when we are between viewports, our dragged preview will tend to show in the last viewport _even_ if we don't have tooltips in their viewports (when lacking monitor info)
        // - consider the case of holding on a menu item to browse child menus: even thou a mouse button is held, there's no active id because menu items only react on mouse release.
        // FIXME-VIEWPORT: This is essentially broken, when ImGuiBackendFlags_HasMouseHoveredViewport is set we want to trust when viewport_hovered==NULL and use that.
        bool is_mouse_dragging_with_an_expected_destination = g.DragDropActive;
        if (is_mouse_dragging_with_an_expected_destination && viewport_hovered == null)
            viewport_hovered = g.MouseLastHoveredViewport;
        if (is_mouse_dragging_with_an_expected_destination || g.ActiveId == 0 || !IsAnyMouseDown())
            if (viewport_hovered != null && viewport_hovered != g.MouseViewport && (viewport_hovered.Flags & ImGuiViewportFlags_NoInputs) == 0)
                g.MouseViewport = viewport_hovered;

        Debug.Assert(g.MouseViewport != null);
    }

    // Update user-facing viewport list (g.Viewports -> g.PlatformIO.Viewports after filtering out some)
    // C++ static: void ImGui::UpdateViewportsEndFrame()
    public static void UpdateViewportsEndFrame()
    {
        ImGuiContext g = GImGui;
        g.PlatformIO.Viewports.resize(0);
        for (int i = 0; i < g.Viewports.Size; i++)
        {
            ImGuiViewportP viewport = g.Viewports[i];
            viewport.LastPos = viewport.Pos;
            viewport.LastSize = viewport.Size;
            if (viewport.LastFrameActive < g.FrameCount || viewport.Size.x <= 0.0f || viewport.Size.y <= 0.0f)
                if (i > 0) // Always include main viewport in the list
                    continue;
            if (viewport.Window != null && !IsWindowActiveAndVisible(viewport.Window))
                continue;
            if (i > 0)
                Debug.Assert(viewport.Window != null);
            g.PlatformIO.Viewports.push_back(viewport);
        }
        g.Viewports[0].ClearRequestFlags(); // Clear main viewport flags because UpdatePlatformWindows() won't do it and may not even be called
    }

    // FIXME: We should ideally refactor the system to call this every frame (we currently don't)
    // ImGuiViewportP* ImGui::AddUpdateViewport(ImGuiWindow* window, ImGuiID id, const ImVec2& pos, const ImVec2& size, ImGuiViewportFlags flags)
    public static ImGuiViewportP AddUpdateViewport(ImGuiWindow window, ImGuiID id, ImVec2 pos, ImVec2 size, ImGuiViewportFlags flags)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(id != 0);

        flags |= ImGuiViewportFlags_IsPlatformWindow;
        if (window != null)
        {
            bool window_can_use_inputs = !((window.Flags & ImGuiWindowFlags_NoMouseInputs) != 0 && (window.Flags & ImGuiWindowFlags_NoNavInputs) != 0);
            if (g.MovingWindow != null && g.MovingWindow.RootWindowDockTree == window)
                flags |= ImGuiViewportFlags_NoInputs | ImGuiViewportFlags_NoFocusOnAppearing;
            if (!window_can_use_inputs)
                flags |= ImGuiViewportFlags_NoInputs;
            if ((window.Flags & ImGuiWindowFlags_NoFocusOnAppearing) != 0)
                flags |= ImGuiViewportFlags_NoFocusOnAppearing;
        }

        ImGuiViewportP viewport = (ImGuiViewportP)FindViewportByID(id);
        if (viewport != null)
        {
            // Always update for main viewport as we are already pulling correct platform pos/size (see #4900)
            ImVec2 prev_pos = viewport.Pos;
            ImVec2 prev_size = viewport.Size;
            if (!viewport.PlatformRequestMove || viewport.ID == IMGUI_VIEWPORT_DEFAULT_ID)
                viewport.Pos = pos;
            if (!viewport.PlatformRequestResize || viewport.ID == IMGUI_VIEWPORT_DEFAULT_ID)
                viewport.Size = size;
            viewport.Flags = flags | (viewport.Flags & (ImGuiViewportFlags_IsMinimized | ImGuiViewportFlags_IsFocused)); // Preserve existing flags
            if (prev_pos != viewport.Pos || prev_size != viewport.Size)
                UpdateViewportPlatformMonitor(viewport);
        }
        else
        {
            // New viewport
            viewport = new ImGuiViewportP();
            viewport.ID = id;
            viewport.Idx = g.Viewports.Size;
            viewport.Pos = viewport.LastPos = pos;
            viewport.Size = viewport.LastSize = size;
            viewport.Flags = flags;
            UpdateViewportPlatformMonitor(viewport);
            g.Viewports.push_back(viewport);
            g.ViewportCreatedCount++;
            IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Add Viewport {0:X8} '{1}'\n", id, window != null ? window.Name : "<NULL>");

            // We assume the window becomes front-most (even when ImGuiViewportFlags_NoFocusOnAppearing is used).
            // This is useful for our platform z-order heuristic when io.MouseHoveredViewport is not available.
            viewport.LastFocusedStampCount = ++g.ViewportFocusedStampCount;

            // We normally setup for all viewports in NewFrame() but here need to handle the mid-frame creation of a new viewport.
            // We need to extend the fullscreen clip rect so the OverlayDrawList clip is correct for that the first frame
            g.DrawListSharedData.ClipRectFullscreen.x = ImMath.ImMin(g.DrawListSharedData.ClipRectFullscreen.x, viewport.Pos.x);
            g.DrawListSharedData.ClipRectFullscreen.y = ImMath.ImMin(g.DrawListSharedData.ClipRectFullscreen.y, viewport.Pos.y);
            g.DrawListSharedData.ClipRectFullscreen.z = ImMath.ImMax(g.DrawListSharedData.ClipRectFullscreen.z, viewport.Pos.x + viewport.Size.x);
            g.DrawListSharedData.ClipRectFullscreen.w = ImMath.ImMax(g.DrawListSharedData.ClipRectFullscreen.w, viewport.Pos.y + viewport.Size.y);

            // Store initial DpiScale before the OS platform window creation, based on expected monitor data.
            // This is so we can select an appropriate font size on the first frame of our window lifetime
            viewport.DpiScale = GetViewportPlatformMonitor(viewport).DpiScale;
        }

        viewport.Window = window;
        viewport.LastFrameActive = g.FrameCount;
        viewport.UpdateWorkRect();
        Debug.Assert(window == null || viewport.ID == window.ID);

        // Initialize FramebufferScale for main viewport and new viewports, before UpdatePlatformWindows() -> Platform_GetWindowFramebufferScale() has a chance to run.
        // FIXME: New viewport on monitors with a different scale will run for a frame with wrong scale? (#9502)
        // A workaround could be to cache last known FramebufferScale per monitor, so the heuristic can pull it from monitor.
        if (viewport.FramebufferScale.x <= 0.0f || viewport.FramebufferScale.y <= 0.0f || viewport.ID == IMGUI_VIEWPORT_DEFAULT_ID)
            viewport.FramebufferScale = g.IO.DisplayFramebufferScale;

        if (window != null)
            window.ViewportOwned = true;

        return viewport;
    }

    // C++ static: void ImGui::DestroyViewport(ImGuiViewportP* viewport)
    public static void DestroyViewport(ImGuiViewportP viewport)
    {
        // Clear references to this viewport in windows (window->ViewportId becomes the master data)
        ImGuiContext g = GImGui;
        for (int n = 0; n < g.Windows.Size; n++)
        {
            ImGuiWindow window = g.Windows[n];
            if (window.Viewport != viewport)
                continue;
            window.Viewport = null;
            window.ViewportOwned = false;
        }
        if (viewport == g.MouseLastHoveredViewport)
            g.MouseLastHoveredViewport = null;

        // Destroy
        IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Delete Viewport {0:X8} '{1}'\n", viewport.ID, viewport.Window != null ? viewport.Window.Name : "n/a");
        DestroyPlatformWindow(viewport); // In most circumstances the platform window will already be destroyed here.
        Debug.Assert(g.PlatformIO.Viewports.contains(viewport) == false);
        Debug.Assert(g.Viewports[viewport.Idx] == viewport);
        g.Viewports.erase(viewport.Idx);
        // (C++ frees the viewport here via IM_DELETE(); C# relies on the GC.)
    }

    // C++ static: void ImGui::WindowSelectViewport(ImGuiWindow* window)
    // FIXME-VIEWPORT: This is all super messy and ought to be clarified or rewritten.
    public static void WindowSelectViewport(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        ImGuiWindowFlags flags = window.Flags;
        window.ViewportAllowPlatformMonitorExtend = -1;

        // Restore main viewport if multi-viewport is not supported by the backend
        ImGuiViewportP main_viewport = (ImGuiViewportP)GetMainViewport();
        if ((g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable) == 0)
        {
            SetWindowViewport(window, main_viewport);
            return;
        }
        window.ViewportOwned = false;

        // Appearing popups reset their viewport so they can inherit again
        if ((flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_Tooltip)) != 0 && window.Appearing)
        {
            window.Viewport = null;
            window.ViewportId = 0;
        }

        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasViewport) == 0)
        {
            // By default inherit from parent window
            if (window.Viewport == null && window.ParentWindow != null && (!window.ParentWindow.IsFallbackWindow || window.ParentWindow.WasActive))
                window.Viewport = window.ParentWindow.Viewport;

            // Attempt to restore saved viewport id (= window that hasn't been activated yet), try to restore the viewport based on saved 'window->ViewportPos' restored from .ini file
            if (window.Viewport == null && window.ViewportId != 0)
            {
                window.Viewport = (ImGuiViewportP)FindViewportByID(window.ViewportId);
                if (window.Viewport == null && window.ViewportPos.x != float.MaxValue && window.ViewportPos.y != float.MaxValue)
                    window.Viewport = AddUpdateViewport(window, window.ID, window.ViewportPos, window.Size, ImGuiViewportFlags_None);
            }
        }

        bool lock_viewport = false;
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasViewport) != 0)
        {
            // Code explicitly request a viewport
            window.Viewport = (ImGuiViewportP)FindViewportByID(g.NextWindowData.ViewportId);
            window.ViewportId = g.NextWindowData.ViewportId; // Store ID even if Viewport isn't resolved yet.
            if (window.Viewport != null && (window.Flags & ImGuiWindowFlags_DockNodeHost) != 0 && window.Viewport.Window != null)
            {
                window.Viewport.Window = window;
                window.Viewport.ID = window.ViewportId = window.ID; // Overwrite ID (always owned by node)
            }
            lock_viewport = true;
        }
        else if ((flags & ImGuiWindowFlags_ChildWindow) != 0 || (flags & ImGuiWindowFlags_ChildMenu) != 0)
        {
            // Always inherit viewport from parent window
            if (window.DockNode != null && window.DockNode.HostWindow != null)
                Debug.Assert(window.DockNode.HostWindow.Viewport == window.ParentWindow.Viewport);
            window.Viewport = window.ParentWindow.Viewport;
        }
        else if (window.DockNode != null && window.DockNode.HostWindow != null)
        {
            // This covers the "always inherit viewport from parent window" case for when a window reattach to a node that was just created mid-frame
            window.Viewport = window.DockNode.HostWindow.Viewport;
        }
        else if ((flags & ImGuiWindowFlags_Tooltip) != 0)
        {
            window.Viewport = g.MouseViewport;
        }
        else if (GetWindowAlwaysWantOwnViewport(window))
        {
            window.Viewport = AddUpdateViewport(window, window.ID, window.Pos, window.Size, ImGuiViewportFlags_None);
        }
        else if (g.MovingWindow != null && g.MovingWindow.RootWindowDockTree == window && IsMousePosValid())
        {
            if (window.Viewport != null && window.Viewport.Window == window)
                window.Viewport = AddUpdateViewport(window, window.ID, window.Pos, window.Size, ImGuiViewportFlags_None);
        }
        else
        {
            // Merge into host viewport?
            // We cannot test window->ViewportOwned as it set lower in the function.
            // Testing (g.ActiveId == 0 || g.ActiveIdAllowOverlap) to avoid merging during a short-term widget interaction. Main intent was to avoid during resize (see #4212)
            bool try_to_merge_into_host_viewport = (window.Viewport != null && window == window.Viewport.Window && (g.ActiveId == 0 || g.ActiveIdAllowOverlap));
            if (try_to_merge_into_host_viewport)
                UpdateTryMergeWindowIntoHostViewports(window);
        }

        // Fallback: merge in default viewport if z-order matches, otherwise create a new viewport
        if (window.Viewport == null)
            if (!UpdateTryMergeWindowIntoHostViewport(window, main_viewport))
                window.Viewport = AddUpdateViewport(window, window.ID, window.Pos, window.Size, ImGuiViewportFlags_None);

        // Mark window as allowed to protrude outside of its viewport and into the current monitor
        if (!lock_viewport)
        {
            if ((flags & (ImGuiWindowFlags_Tooltip | ImGuiWindowFlags_Popup)) != 0)
            {
                // We need to take account of the possibility that mouse may become invalid.
                // Popups/Tooltip always set ViewportAllowPlatformMonitorExtend so GetWindowAllowedExtentRect() will return full monitor bounds.
                ImVec2 mouse_ref = (flags & ImGuiWindowFlags_Tooltip) != 0 ? g.IO.MousePos : g.BeginPopupStack.back().OpenMousePos;
                bool use_mouse_ref = (!g.NavCursorVisible || !g.NavHighlightItemUnderNav || g.NavWindow == null);
                bool mouse_valid = IsMousePosValid(ref mouse_ref);
                if ((window.Appearing || (flags & (ImGuiWindowFlags_Tooltip | ImGuiWindowFlags_ChildMenu)) != 0) && (!use_mouse_ref || mouse_valid))
                    window.ViewportAllowPlatformMonitorExtend = FindPlatformMonitorForPos((use_mouse_ref && mouse_valid) ? mouse_ref : NavCalcPreferredRefPos(window.Flags));
                else
                    window.ViewportAllowPlatformMonitorExtend = window.Viewport.PlatformMonitor;
            }
            else if (window.Viewport != null && window != window.Viewport.Window && window.Viewport.Window != null && (flags & ImGuiWindowFlags_ChildWindow) == 0 && window.DockNode == null)
            {
                // When called from Begin() we don't have access to a proper version of the Hidden flag yet, so we replicate this code.
                bool will_be_visible = (window.DockIsActive && !window.DockTabIsVisible) ? false : true;
                if ((window.Flags & ImGuiWindowFlags_DockNodeHost) != 0 && window.Viewport.LastFrameActive < g.FrameCount && will_be_visible)
                {
                    // Steal/transfer ownership
                    IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Window '{0}' steal Viewport {1:X8} from Window '{2}'\n", window.Name, window.Viewport.ID, window.Viewport.Window.Name);
                    window.Viewport.Window = window;
                    window.Viewport.ID = window.ID;
                    window.Viewport.LastNameHash = 0;
                }
                else if (!UpdateTryMergeWindowIntoHostViewports(window)) // Merge?
                {
                    // New viewport
                    window.Viewport = AddUpdateViewport(window, window.ID, window.Pos, window.Size, ImGuiViewportFlags_NoFocusOnAppearing);
                }
            }
            else if (window.ViewportAllowPlatformMonitorExtend < 0 && (flags & ImGuiWindowFlags_ChildWindow) == 0)
            {
                // Regular (non-child, non-popup) windows by default are also allowed to protrude
                // Child windows are kept contained within their parent.
                window.ViewportAllowPlatformMonitorExtend = window.Viewport.PlatformMonitor;
            }
        }

        // Update flags
        window.ViewportOwned = (window == window.Viewport.Window);
        window.ViewportId = window.Viewport.ID;

        // If the OS window has a title bar, hide our imgui title bar
        //if (window->ViewportOwned && !(window->Viewport->Flags & ImGuiViewportFlags_NoDecoration))
        //    window->Flags |= ImGuiWindowFlags_NoTitleBar;
    }

    // void ImGui::WindowSyncOwnedViewport(ImGuiWindow* window, ImGuiWindow* parent_window_in_stack)
    public static void WindowSyncOwnedViewport(ImGuiWindow window, ImGuiWindow parent_window_in_stack)
    {
        ImGuiContext g = GImGui;

        bool viewport_rect_changed = false;

        // Synchronize window --> viewport in most situations
        // Synchronize viewport -> window in case the platform window has been moved or resized from the OS/WM
        if (window.Viewport.PlatformRequestMove)
        {
            window.Pos = window.Viewport.Pos;
            MarkIniSettingsDirty(window);
        }
        else if (window.Viewport.Pos != window.Pos) // (C++: memcmp(&window->Viewport->Pos, &window->Pos, sizeof(window->Pos)) != 0)
        {
            viewport_rect_changed = true;
            window.Viewport.Pos = window.Pos;
        }

        if (window.Viewport.PlatformRequestResize)
        {
            window.Size = window.SizeFull = window.Viewport.Size;
            MarkIniSettingsDirty(window);
        }
        else if (window.Viewport.Size != window.Size) // (C++: memcmp(&window->Viewport->Size, &window->Size, sizeof(window->Size)) != 0)
        {
            viewport_rect_changed = true;
            window.Viewport.Size = window.Size;
        }
        window.Viewport.UpdateWorkRect();

        // The viewport may have changed monitor since the global update in UpdateViewportsNewFrame()
        // Either a SetNextWindowPos() call in the current frame or a SetWindowPos() call in the previous frame may have this effect.
        if (viewport_rect_changed)
            UpdateViewportPlatformMonitor(window.Viewport);

        // Update common viewport flags
        ImGuiViewportFlags viewport_flags_to_clear = ImGuiViewportFlags_TopMost | ImGuiViewportFlags_NoTaskBarIcon | ImGuiViewportFlags_NoDecoration | ImGuiViewportFlags_NoRendererClear;
        ImGuiViewportFlags viewport_flags = window.Viewport.Flags & ~viewport_flags_to_clear;
        ImGuiWindowFlags window_flags = window.Flags;
        bool is_modal = (window_flags & ImGuiWindowFlags_Modal) != 0;
        bool is_short_lived_floating_window = (window_flags & (ImGuiWindowFlags_ChildMenu | ImGuiWindowFlags_Tooltip | ImGuiWindowFlags_Popup)) != 0;
        if ((window_flags & ImGuiWindowFlags_Tooltip) != 0)
            viewport_flags |= ImGuiViewportFlags_TopMost;
        if ((g.IO.ConfigViewportsNoTaskBarIcon || is_short_lived_floating_window) && !is_modal)
            viewport_flags |= ImGuiViewportFlags_NoTaskBarIcon;
        if (g.IO.ConfigViewportsNoDecoration || is_short_lived_floating_window)
            viewport_flags |= ImGuiViewportFlags_NoDecoration;

        // Not correct to set modal as topmost because:
        // - Because other popups can be stacked above a modal (e.g. combo box in a modal)
        // - ImGuiViewportFlags_TopMost is currently handled different in backends: in Win32 it is "appear top most" whereas in GLFW and SDL it is "stay topmost"
        //if (flags & ImGuiWindowFlags_Modal)
        //    viewport_flags |= ImGuiViewportFlags_TopMost;

        // For popups and menus that may be protruding out of their parent viewport, we enable _NoFocusOnClick so that clicking on them
        // won't steal the OS focus away from their parent window (which may be reflected in OS the title bar decoration).
        // Setting _NoFocusOnClick would technically prevent us from bringing back to front in case they are being covered by an OS window from a different app,
        // but it shouldn't be much of a problem considering those are already popups that are closed when clicking elsewhere.
        if (is_short_lived_floating_window && !is_modal)
            viewport_flags |= ImGuiViewportFlags_NoFocusOnAppearing | ImGuiViewportFlags_NoFocusOnClick;

        // We can overwrite viewport flags using ImGuiWindowClass (advanced users)
        if (window.WindowClass.ViewportFlagsOverrideSet != 0)
            viewport_flags |= window.WindowClass.ViewportFlagsOverrideSet;
        if (window.WindowClass.ViewportFlagsOverrideClear != 0)
            viewport_flags &= ~window.WindowClass.ViewportFlagsOverrideClear;

        // We can also tell the backend that clearing the platform window won't be necessary,
        // as our window background is filling the viewport and we have disabled BgAlpha.
        // FIXME: Work on support for per-viewport transparency (#2766)
        if ((window_flags & ImGuiWindowFlags_NoBackground) == 0)
            viewport_flags |= ImGuiViewportFlags_NoRendererClear;

        window.Viewport.Flags = viewport_flags;

        window.Viewport.PlatformIconData = window.WindowClass.PlatformIconData;

        // Update parent viewport ID
        // (the !IsFallbackWindow test mimic the one done in WindowSelectViewport())
        if (window.WindowClass.ParentViewportId != 0xFFFFFFFFu) // C++: != (ImGuiID)-1
        {
            ImGuiID old_parent_viewport_id = window.Viewport.ParentViewportId;
            window.Viewport.ParentViewportId = window.WindowClass.ParentViewportId;
            if (window.Viewport.ParentViewportId != old_parent_viewport_id)
                window.Viewport.ParentViewport = FindViewportByID(window.Viewport.ParentViewportId);
        }
        else if ((window_flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_Tooltip)) != 0 && parent_window_in_stack != null && (!parent_window_in_stack.IsFallbackWindow || parent_window_in_stack.WasActive))
        {
            window.Viewport.ParentViewport = parent_window_in_stack.Viewport;
            window.Viewport.ParentViewportId = parent_window_in_stack.Viewport.ID;
        }
        else
        {
            window.Viewport.ParentViewport = g.IO.ConfigViewportsNoDefaultParent ? null : (ImGuiViewportP)GetMainViewport();
            window.Viewport.ParentViewportId = g.IO.ConfigViewportsNoDefaultParent ? 0 : IMGUI_VIEWPORT_DEFAULT_ID;
        }
    }

    // C++ static (imgui.cpp, ported here because UpdatePlatformWindows() depends on it):
    // static ImGuiWindow* GetWindowForTitleDisplay(ImGuiWindow* window)
    public static ImGuiWindow GetWindowForTitleDisplay(ImGuiWindow window)
    {
        return window.DockNodeAsHost != null ? window.DockNodeAsHost.VisibleWindow : window;
    }

    // Called by user at the end of the main loop, after EndFrame()
    // This will handle the creation/update of all OS windows via function defined in the ImGuiPlatformIO api.
    // void ImGui::UpdatePlatformWindows()
    public static void UpdatePlatformWindows()
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.FrameCountEnded == g.FrameCount, "Forgot to call Render() or EndFrame() before UpdatePlatformWindows()?");
        Debug.Assert(g.FrameCountPlatformEnded < g.FrameCount);
        g.FrameCountPlatformEnded = g.FrameCount;
        if ((g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable) == 0)
            return;

        // Create/resize/destroy platform windows to match each active viewport.
        // Skip the main viewport (index 0), which is always fully handled by the application!
        for (int i = 1; i < g.Viewports.Size; i++)
        {
            ImGuiViewportP viewport = g.Viewports[i];

            // Destroy platform window if the viewport hasn't been submitted or if it is hosting a hidden window
            // (the implicit/fallback Debug##Default window will be registering its viewport then be disabled, causing a dummy DestroyPlatformWindow to be made each frame)
            bool destroy_platform_window = false;
            destroy_platform_window |= (viewport.LastFrameActive < g.FrameCount - 1);
            destroy_platform_window |= (viewport.Window != null && !IsWindowActiveAndVisible(viewport.Window));
            if (destroy_platform_window)
            {
                DestroyPlatformWindow(viewport);
                continue;
            }

            // New windows that appears directly in a new viewport won't always have a size on their first frame
            if (viewport.LastFrameActive < g.FrameCount || viewport.Size.x <= 0 || viewport.Size.y <= 0)
                continue;

            // Create window
            bool is_new_platform_window = (viewport.PlatformWindowCreated == false);
            if (is_new_platform_window)
            {
                IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Create Platform Window {0:X8} '{1}'\n", viewport.ID, viewport.Window != null ? viewport.Window.Name : "n/a");
                g.PlatformIO.Platform_CreateWindow(viewport);
                if (g.PlatformIO.Renderer_CreateWindow != null)
                    g.PlatformIO.Renderer_CreateWindow(viewport);
                g.PlatformWindowsCreatedCount++;
                viewport.LastNameHash = 0;
                viewport.LastPlatformPos = viewport.LastPlatformSize = new ImVec2(float.MaxValue, float.MaxValue); // By clearing those we'll enforce a call to Platform_SetWindowPos/Size below, before Platform_ShowWindow (FIXME: Is that necessary?)
                viewport.LastRendererSize = viewport.Size;                                                          // We don't need to call Renderer_SetWindowSize() as it is expected Renderer_CreateWindow() already did it.
                viewport.PlatformWindowCreated = true;
            }

            // Apply Position and Size (from ImGui to Platform/Renderer backends)
            if ((viewport.LastPlatformPos.x != viewport.Pos.x || viewport.LastPlatformPos.y != viewport.Pos.y) && !viewport.PlatformRequestMove)
                g.PlatformIO.Platform_SetWindowPos(viewport, viewport.Pos);
            if ((viewport.LastPlatformSize.x != viewport.Size.x || viewport.LastPlatformSize.y != viewport.Size.y) && !viewport.PlatformRequestResize)
                g.PlatformIO.Platform_SetWindowSize(viewport, viewport.Size);
            if ((viewport.LastRendererSize.x != viewport.Size.x || viewport.LastRendererSize.y != viewport.Size.y) && g.PlatformIO.Renderer_SetWindowSize != null)
                g.PlatformIO.Renderer_SetWindowSize(viewport, viewport.Size);
            viewport.LastPlatformPos = viewport.Pos;
            viewport.LastPlatformSize = viewport.LastRendererSize = viewport.Size;

            // Update title bar (if it changed)
            // (C++ temporarily NUL-cuts the window name buffer at title_end; the C# port uses a Substring instead.)
            ImGuiWindow window_for_title = GetWindowForTitleDisplay(viewport.Window);
            if (window_for_title != null)
            {
                string title_begin = window_for_title.Name;
                int title_end = FindRenderedTextEnd(title_begin); // C++: title_end points at the end of the rendered text
                // C++ hashes bytes [title_begin, title_end); hash the rendered prefix fully rather than
                // passing the char index as a UTF-8 byte count.
                ImGuiID title_hash = ImGuiHash.ImHashStr(title_end < title_begin.Length ? title_begin.Substring(0, title_end) : title_begin, 0);
                if (viewport.LastNameHash != title_hash)
                {
                    string title = title_end < title_begin.Length ? title_begin.Substring(0, title_end) : title_begin; // Cut existing buffer short instead of doing an alloc/free, no small gain.
                    g.PlatformIO.Platform_SetWindowTitle(viewport, title);
                    viewport.LastNameHash = title_hash;
                }
            }

            // Update alpha (if it changed)
            if (viewport.LastAlpha != viewport.Alpha && g.PlatformIO.Platform_SetWindowAlpha != null)
                g.PlatformIO.Platform_SetWindowAlpha(viewport, viewport.Alpha);
            viewport.LastAlpha = viewport.Alpha;

            // Optional, general purpose call to allow the backend to perform general book-keeping even if things haven't changed.
            if (g.PlatformIO.Platform_UpdateWindow != null)
                g.PlatformIO.Platform_UpdateWindow(viewport);

            if (is_new_platform_window)
            {
                // On startup ensure new platform window don't steal focus (give it a few frames, as nested contents may lead to viewport being created a few frames late)
                if (g.FrameCount < 3)
                    viewport.Flags |= ImGuiViewportFlags_NoFocusOnAppearing;

                // Show window
                g.PlatformIO.Platform_ShowWindow(viewport);
            }

            // Clear request flags
            viewport.ClearRequestFlags();
        }
    }

    // This is a default/basic function for performing the rendering/swap of multiple Platform Windows.
    // Custom renderers may prefer to not call this function at all, and instead iterate the publicly exposed platform data and handle rendering/sync themselves.
    // The Render/Swap functions stored in ImGuiPlatformIO are merely here to allow for this helper to exist, but you can do it yourself:
    //
    //    ImGuiPlatformIO& platform_io = ImGui::GetPlatformIO();
    //    for (int i = 1; i < platform_io.Viewports.Size; i++)
    //        if ((platform_io.Viewports[i]->Flags & ImGuiViewportFlags_Minimized) == 0)
    //            MyRenderFunction(platform_io.Viewports[i], my_args);
    //    for (int i = 1; i < platform_io.Viewports.Size; i++)
    //        if ((platform_io.Viewports[i]->Flags & ImGuiViewportFlags_Minimized) == 0)
    //            MySwapBufferFunction(platform_io.Viewports[i], my_args);
    //
    // void ImGui::RenderPlatformWindowsDefault(void* platform_render_arg, void* renderer_render_arg)
    public static void RenderPlatformWindowsDefault(object platform_render_arg = null, object renderer_render_arg = null)
    {
        // Skip the main viewport (index 0), which is always fully handled by the application!
        ImGuiPlatformIO platform_io = GetPlatformIO();
        for (int i = 1; i < platform_io.Viewports.Size; i++)
        {
            ImGuiViewport viewport = platform_io.Viewports[i];
            if ((viewport.Flags & ImGuiViewportFlags_IsMinimized) != 0)
                continue;
            if (platform_io.Platform_RenderWindow != null) platform_io.Platform_RenderWindow(viewport, platform_render_arg);
            if (platform_io.Renderer_RenderWindow != null) platform_io.Renderer_RenderWindow(viewport, renderer_render_arg);
        }
        for (int i = 1; i < platform_io.Viewports.Size; i++)
        {
            ImGuiViewport viewport = platform_io.Viewports[i];
            if ((viewport.Flags & ImGuiViewportFlags_IsMinimized) != 0)
                continue;
            if (platform_io.Platform_SwapBuffers != null) platform_io.Platform_SwapBuffers(viewport, platform_render_arg);
            if (platform_io.Renderer_SwapBuffers != null) platform_io.Renderer_SwapBuffers(viewport, renderer_render_arg);
        }
    }

    // C++ static: int ImGui::FindPlatformMonitorForPos(const ImVec2& pos)
    public static int FindPlatformMonitorForPos(ImVec2 pos)
    {
        ImGuiContext g = GImGui;
        for (int monitor_n = 0; monitor_n < g.PlatformIO.Monitors.Size; monitor_n++)
        {
            ImGuiPlatformMonitor monitor = g.PlatformIO.Monitors[monitor_n];
            if (new ImRect(monitor.MainPos, monitor.MainPos + monitor.MainSize).Contains(pos))
                return monitor_n;
        }
        return -1;
    }

    // Search for the monitor with the largest intersection area with the given rectangle
    // We generally try to avoid searching loops but the monitor count should be very small here
    // FIXME-OPT: We could test the last monitor used for that viewport first, and early
    // C++ static: int ImGui::FindPlatformMonitorForRect(const ImRect& rect)
    public static int FindPlatformMonitorForRect(ImRect rect)
    {
        ImGuiContext g = GImGui;

        int monitor_count = g.PlatformIO.Monitors.Size;
        if (monitor_count <= 1)
            return monitor_count - 1;

        // Use a minimum threshold of 1.0f so a zero-sized rect won't false positive, and will still find the correct monitor given its position.
        // This is necessary for tooltips which always resize down to zero at first.
        float surface_threshold = ImMath.ImMax(rect.GetWidth() * rect.GetHeight() * 0.5f, 1.0f);
        int best_monitor_n = 0; // Default to the first monitor as fallback
        float best_monitor_surface = 0.001f;

        for (int monitor_n = 0; monitor_n < g.PlatformIO.Monitors.Size && best_monitor_surface < surface_threshold; monitor_n++)
        {
            ImGuiPlatformMonitor monitor = g.PlatformIO.Monitors[monitor_n];
            ImRect monitor_rect = new ImRect(monitor.MainPos, monitor.MainPos + monitor.MainSize);
            if (monitor_rect.Contains(rect))
                return monitor_n;
            ImRect overlapping_rect = rect;
            overlapping_rect.ClipWithFull(monitor_rect);
            float overlapping_surface = overlapping_rect.GetWidth() * overlapping_rect.GetHeight();
            if (overlapping_surface < best_monitor_surface)
                continue;
            best_monitor_surface = overlapping_surface;
            best_monitor_n = monitor_n;
        }
        return best_monitor_n;
    }

    // Update monitor from viewport rectangle (we'll use this info to clamp windows and save windows lost in a removed monitor)
    // C++ static: void ImGui::UpdateViewportPlatformMonitor(ImGuiViewportP* viewport)
    public static void UpdateViewportPlatformMonitor(ImGuiViewportP viewport)
    {
        viewport.PlatformMonitor = (short)FindPlatformMonitorForRect(viewport.GetMainRect());
    }

    // Return value is always != NULL, but don't hold on it across frames.
    // const ImGuiPlatformMonitor* ImGui::GetViewportPlatformMonitor(ImGuiViewport* viewport_p)
    public static ImGuiPlatformMonitor GetViewportPlatformMonitor(ImGuiViewport viewport_p)
    {
        ImGuiContext g = GImGui;
        ImGuiViewportP viewport = (ImGuiViewportP)viewport_p;
        int monitor_idx = viewport.PlatformMonitor;
        if (monitor_idx >= 0 && monitor_idx < g.PlatformIO.Monitors.Size)
            return g.PlatformIO.Monitors[monitor_idx];
        return g.FallbackMonitor;
    }

    // void ImGui::DestroyPlatformWindow(ImGuiViewportP* viewport)
    public static void DestroyPlatformWindow(ImGuiViewportP viewport)
    {
        ImGuiContext g = GImGui;
        if (viewport.PlatformWindowCreated)
        {
            IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Destroy Platform Window {0:X8} '{1}'\n", viewport.ID, viewport.Window != null ? viewport.Window.Name : "n/a");
            if (g.PlatformIO.Renderer_DestroyWindow != null)
                g.PlatformIO.Renderer_DestroyWindow(viewport);
            if (g.PlatformIO.Platform_DestroyWindow != null)
                g.PlatformIO.Platform_DestroyWindow(viewport);
            Debug.Assert(viewport.RendererUserData == null && viewport.PlatformUserData == null);

            // Don't clear PlatformWindowCreated for the main viewport, as we initially set that up to true in Initialize()
            // The righter way may be to leave it to the backend to set this flag all-together, and made the flag public.
            if (viewport.ID != IMGUI_VIEWPORT_DEFAULT_ID)
                viewport.PlatformWindowCreated = false;
        }
        else
        {
            Debug.Assert(viewport.RendererUserData == null && viewport.PlatformUserData == null && viewport.PlatformHandle == null);
        }
        viewport.RendererUserData = viewport.PlatformUserData = viewport.PlatformHandle = null;
        viewport.ClearRequestFlags();
    }

    // void ImGui::DestroyPlatformWindows()
    public static void DestroyPlatformWindows()
    {
        // We call the destroy window on every viewport (including the main viewport, index 0) to give a chance to the backend
        // to clear any data they may have stored in e.g. PlatformUserData, RendererUserData.
        // It is convenient for the platform backend code to store something in the main viewport, in order for e.g. the mouse handling
        // code to operator a consistent manner.
        // It is expected that the backend can handle calls to Renderer_DestroyWindow/Platform_DestroyWindow without
        // crashing if it doesn't have data stored.
        ImGuiContext g = GImGui;
        for (int n = 0; n < g.Viewports.Size; n++)
            DestroyPlatformWindow(g.Viewports[n]);
    }
}
