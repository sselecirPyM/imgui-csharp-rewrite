// Port of Dear ImGui imgui.cpp [SECTION] DOCKING (docking branch).
// Docking: ImGuiDockContext / ImGuiDockNode / DockBuilder / Begin-End support / Settings.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    // C# macro IMGUI_DEBUG_LOG_DOCKING(...) — format strings here use C# {0} placeholders.
    static void IMGUI_DEBUG_LOG_DOCKING(ImGuiContext g, string fmt, params object[] args)
    {
        if ((g.DebugLogFlags & ImGuiDebugLogFlags_EventDocking) != 0)
            DebugLog(string.Format(fmt, args));
    }

    // C# helper IMGUI_DEBUG_LOG_VIEWPORT(...) (used by some docking paths)
    static void IMGUI_DEBUG_LOG_VIEWPORT(ImGuiContext g, string fmt, params object[] args)
    {
        if ((g.DebugLogFlags & ImGuiDebugLogFlags_EventViewport) != 0)
            DebugLog(string.Format(fmt, args));
    }

    // static ImGuiWindow* ImGui::GetWindowForTitleAndMenuHeight(ImGuiWindow* window)
    public static ImGuiWindow GetWindowForTitleAndMenuHeight(ImGuiWindow window)
    {
        return (window.DockNodeAsHost != null && window.DockNodeAsHost.VisibleWindow != null) ? window.DockNodeAsHost.VisibleWindow : window;
    }

    // C++ imgui_internal.h: inline ImGuiDockNode* ImGui::DockNodeGetRootNode(ImGuiDockNode* node)
    public static ImGuiDockNode DockNodeGetRootNode(ImGuiDockNode node)
    {
        while (node.ParentNode != null)
            node = node.ParentNode;
        return node;
    }

    // C++ imgui_internal.h: inline bool ImGui::DockNodeIsInHierarchyOf(ImGuiDockNode* node, ImGuiDockNode* parent)
    public static bool DockNodeIsInHierarchyOf(ImGuiDockNode node, ImGuiDockNode parent)
    {
        while (node != null)
        {
            if (node == parent)
                return true;
            node = node.ParentNode;
        }
        return false;
    }

    // static void ImGui::DockNodeAddTabBar(ImGuiDockNode* node)
    static void DockNodeAddTabBar(ImGuiDockNode node)
    {
        Debug.Assert(node.TabBar == null);
        node.TabBar = new ImGuiTabBar();
    }

    // static void ImGui::DockNodeRemoveTabBar(ImGuiDockNode* node)
    static void DockNodeRemoveTabBar(ImGuiDockNode node)
    {
        if (node.TabBar == null)
            return;
        node.TabBar = null;
    }

    // static void ImGui::DockNodeWindowMenuUpdate(ImGuiDockNode* node, ImGuiTabBar* tab_bar)
    static void DockNodeWindowMenuUpdate(ImGuiDockNode node, ImGuiTabBar tab_bar)
    {
        // Try to position the menu so it is more likely to stays within the same viewport
        ImGuiContext g = GImGui;
        if (g.Style.WindowMenuButtonPosition == ImGuiDir_Left)
            SetNextWindowPos(new ImVec2(node.Pos.x, node.Pos.y + GetFrameHeight()), ImGuiCond_Always, new ImVec2(0.0f, 0.0f));
        else
            SetNextWindowPos(new ImVec2(node.Pos.x + node.Size.x, node.Pos.y + GetFrameHeight()), ImGuiCond_Always, new ImVec2(1.0f, 0.0f));
        if (BeginPopup("#WindowMenu"))
        {
            node.IsFocused = true;
            g.DockNodeWindowMenuHandler(g, node, tab_bar);
            EndPopup();
        }
    }

    // static bool DockNodeIsDropAllowedOne(ImGuiWindow* payload, ImGuiWindow* host_window)
    static bool DockNodeIsDropAllowedOne(ImGuiWindow payload, ImGuiWindow host_window)
    {
        if (host_window.DockNodeAsHost != null && host_window.DockNodeAsHost.IsDockSpace() && payload.BeginOrderWithinContext < host_window.BeginOrderWithinContext)
            return false;

        ImGuiWindowClass host_class = host_window.DockNodeAsHost != null ? host_window.DockNodeAsHost.WindowClass : host_window.WindowClass;
        ImGuiWindowClass payload_class = payload.WindowClass;
        if (host_class.ClassId != payload_class.ClassId)
        {
            bool pass = false;
            if (host_class.ClassId != 0 && host_class.DockingAllowUnclassed && payload_class.ClassId == 0)
                pass = true;
            if (payload_class.ClassId != 0 && payload_class.DockingAllowUnclassed && host_class.ClassId == 0)
                pass = true;
            if (!pass)
                return false;
        }

        // Prevent docking any window created above a popup
        // Technically we should support it (e.g. in the case of a long-lived modal window that had fancy docking features),
        // by e.g. adding a 'if (!ImGui::IsWindowWithinBeginStackOf(host_window, popup_window))' test.
        // But it would requires more work on our end because the dock host windows is technically created in NewFrame()
        // and our ->ParentXXX and ->RootXXX pointers inside windows are currently mislading or lacking.
        ImGuiContext g = GImGui;
        for (int i = g.OpenPopupStack.Size - 1; i >= 0; i--)
            if (g.OpenPopupStack[i].Window is ImGuiWindow popup_window)
                if (IsWindowWithinBeginStackOf(payload, popup_window))   // Payload is created from within a popup begin stack.
                    return false;

        return true;
    }

    // static void ImGui::DockNodeCalcSplitRects(ImVec2& pos_old, ImVec2& size_old, ImVec2& pos_new, ImVec2& size_new, ImGuiDir dir, ImVec2 size_new_desired)
    static void DockNodeCalcSplitRects(ref ImVec2 pos_old, ref ImVec2 size_old, ref ImVec2 pos_new, ref ImVec2 size_new, int dir, ImVec2 size_new_desired)
    {
        ImGuiContext g = GImGui;
        float dock_spacing = g.Style.ItemInnerSpacing.x;
        int axis = (dir == ImGuiDir_Left || dir == ImGuiDir_Right) ? ImGuiAxis_X : ImGuiAxis_Y;
        pos_new[axis ^ 1] = pos_old[axis ^ 1];
        size_new[axis ^ 1] = size_old[axis ^ 1];

        // Distribute size on given axis (with a desired size or equally)
        float w_avail = size_old[axis] - dock_spacing;
        if (size_new_desired[axis] > 0.0f && size_new_desired[axis] <= w_avail * 0.5f)
        {
            size_new[axis] = size_new_desired[axis];
            size_old[axis] = ImMath.ImTrunc(w_avail - size_new[axis]);
        }
        else
        {
            size_new[axis] = ImMath.ImTrunc(w_avail * 0.5f);
            size_old[axis] = ImMath.ImTrunc(w_avail - size_new[axis]);
        }

        // Position each node
        if (dir == ImGuiDir_Right || dir == ImGuiDir_Down)
        {
            pos_new[axis] = pos_old[axis] + size_old[axis] + dock_spacing;
        }
        else if (dir == ImGuiDir_Left || dir == ImGuiDir_Up)
        {
            pos_new[axis] = pos_old[axis];
            pos_old[axis] = pos_new[axis] + size_new[axis] + dock_spacing;
        }
    }

    // static void DockNodeGetHostWindowTitle()
    // C++ imgui_internal.h: inline int ImGui::DockNodeGetDepth(const ImGuiDockNode* node)
    public static int DockNodeGetDepth(ImGuiDockNode node)
    {
        int depth = 0;
        while (node.ParentNode != null)
        {
            node = node.ParentNode;
            depth++;
        }
        return depth;
    }

    // static ImVec2 ImGui::FixLargeWindowsWhenUndocking(const ImVec2& size, ImGuiViewport* ref_viewport)
    static ImVec2 FixLargeWindowsWhenUndocking(ImVec2 size, ImGuiViewport ref_viewport)
    {
        if (ref_viewport == null)
            return size;

        ImGuiContext g = GImGui;
        ImVec2 max_size = ImMath.ImTrunc(ref_viewport.WorkSize * 0.90f);
        if ((g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable) != 0)
        {
            ImGuiPlatformMonitor monitor = GetViewportPlatformMonitor(ref_viewport);
            max_size = ImMath.ImTrunc(monitor.WorkSize * 0.90f);
        }
        return ImMath.ImMin(size, max_size);
    }

    static string DockNodeGetHostWindowTitle(ImGuiDockNode node) => $"##DockNode_{node.ID:X2}";

    // C++ ImVector<ImGuiWindow*>::find_erase()
    static void FindEraseWindow(ImVector<ImGuiWindow> v, ImGuiWindow w)
    {
        int idx = v.find_index_of_ref(w);
        if (idx != -1)
            v.erase(idx);
    }

    // C++ adaptation of *(ImGuiWindow**)payload->Data: the payload stores the window ID (see BeginDockableDragDropSource).
    static ImGuiWindow DockNodeGetWindowFromPayload(ImGuiPayload payload)
    {
        if (payload == null || payload.DataSize != sizeof(uint))
            return null;
        ImGuiID window_id = BitConverter.ToUInt32(payload.Data, 0);
        return (ImGuiWindow)GImGui.WindowsById.GetVoidPtr(window_id);
    }

    //-----------------------------------------------------------------------------
    // Docking: ImGuiDockContext
    //-----------------------------------------------------------------------------
    // The lifetime model is different from the one of regular windows: we always create a ImGuiDockNode for each ImGuiDockNodeSettings,
    // or we always hold the entire docking node tree. Nodes are frequently hidden, e.g. if the window(s) or child nodes they host are not active.
    // At boot time only, we run a simple GC to remove nodes that have no references.
    //-----------------------------------------------------------------------------

    // void ImGui::DockContextInitialize(ImGuiContext* ctx)
    public static void DockContextInitialize(ImGuiContext ctx)
    {
        ImGuiContext g = ctx;

        // Add .ini handle for persistent docking data
        ImGuiSettingsHandler ini_handler = new ImGuiSettingsHandler();
        ini_handler.TypeName = "Docking";
        ini_handler.TypeHash = ImGuiHash.ImHashStr("Docking", 0);
        ini_handler.ClearAllFn = DockSettingsHandler_ClearAll;
        ini_handler.ReadInitFn = DockSettingsHandler_ClearAll; // Also clear on read
        ini_handler.ReadOpenFn = DockSettingsHandler_ReadOpen;
        ini_handler.ReadLineFn = DockSettingsHandler_ReadLine;
        ini_handler.ApplyAllFn = DockSettingsHandler_ApplyAll;
        ini_handler.WriteAllFn = DockSettingsHandler_WriteAll;
        g.SettingsHandlers.push_back(ini_handler);

        g.DockNodeWindowMenuHandler = DockNodeWindowMenuHandler_Default;
    }

    // void ImGui::DockContextShutdown(ImGuiContext* ctx)
    public static void DockContextShutdown(ImGuiContext ctx)
    {
        ImGuiDockContext dc = ctx.DockContext;
        for (int n = 0; n < dc.Nodes.Data.Size; n++)
            if (dc.Nodes.Data.Data[n].val_p is ImGuiDockNode node)
                DockContextDeleteNode(ctx, node);
    }

    // void ImGui::DockContextClearNodes(ImGuiContext* ctx, ImGuiID root_id, bool clear_settings_refs)
    public static void DockContextClearNodes(ImGuiContext ctx, ImGuiID root_id, bool clear_settings_refs)
    {
        Debug.Assert(ctx == GImGui);
        DockBuilderRemoveNodeDockedWindows(root_id, clear_settings_refs);
        DockBuilderRemoveNodeChildNodes(root_id);
    }

    // [DEBUG] This function also acts as a defacto test to make sure we can rebuild from scratch without a glitch
    // (Different from DockSettingsHandler_ClearAll() + DockSettingsHandler_ApplyAll() because this reuses current settings!)
    // void ImGui::DockContextRebuildNodes(ImGuiContext* ctx)
    public static void DockContextRebuildNodes(ImGuiContext ctx)
    {
        ImGuiContext g = ctx;
        ImGuiDockContext dc = ctx.DockContext;
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextRebuildNodes\n");
        SaveIniSettingsToMemory(out _);
        ImGuiID root_id = 0; // Rebuild all
        DockContextClearNodes(ctx, root_id, false);
        DockContextBuildNodesFromSettings(ctx, dc.NodesSettings);
        DockContextBuildAddWindowsToNodes(ctx, root_id);
    }

    // Docking context update function, called by NewFrame()
    // void ImGui::DockContextNewFrameUpdateUndocking(ImGuiContext* ctx)
    public static void DockContextNewFrameUpdateUndocking(ImGuiContext ctx)
    {
        ImGuiContext g = ctx;
        ImGuiDockContext dc = ctx.DockContext;
        if ((g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable) == 0)
        {
            if (dc.Nodes.Data.Size > 0 || dc.Requests.Size > 0)
                DockContextClearNodes(ctx, 0, true);
            return;
        }

        // Setting NoSplit at runtime merges all nodes
        if (g.IO.ConfigDockingNoSplit)
            for (int n = 0; n < dc.Nodes.Data.Size; n++)
                if (dc.Nodes.Data.Data[n].val_p is ImGuiDockNode node)
                    if (node.IsRootNode() && node.IsSplitNode())
                    {
                        DockBuilderRemoveNodeChildNodes(node.ID);
                        //dc.WantFullRebuild = true;
                    }

        // Process full rebuild
        if (dc.WantFullRebuild)
        {
            DockContextRebuildNodes(ctx);
            dc.WantFullRebuild = false;
        }

        // Process Undocking requests (we need to process them _before_ the UpdateMouseMovingWindowNewFrame call in NewFrame)
        for (int req_n = 0; req_n < dc.Requests.Size; req_n++)
        {
            ImGuiDockRequest req = dc.Requests[req_n];
            if (req.Type == ImGuiDockRequestType.ImGuiDockRequestType_Undock && req.UndockTargetWindow != null)
                DockContextProcessUndockWindow(ctx, req.UndockTargetWindow, true);
            else if (req.Type == ImGuiDockRequestType.ImGuiDockRequestType_Undock && req.UndockTargetNode != null)
                DockContextProcessUndockNode(ctx, req.UndockTargetNode);
        }
    }

    // Docking context update function, called by NewFrame()
    // void ImGui::DockContextNewFrameUpdateDocking(ImGuiContext* ctx)
    public static void DockContextNewFrameUpdateDocking(ImGuiContext ctx)
    {
        ImGuiContext g = ctx;
        ImGuiDockContext dc = ctx.DockContext;
        if ((g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable) == 0)
            return;

        // [DEBUG] Store hovered dock node.
        // We could in theory use DockNodeTreeFindVisibleNodeByPos() on the root host dock node, but using ->DockNode is a good shortcut.
        // Note this is mostly a debug thing and isn't actually used for docking target, because docking involve more detailed filtering.
        g.DebugHoveredDockNode = null;
        ImGuiWindow hovered_window = g.HoveredWindowUnderMovingWindow;
        if (hovered_window != null)
        {
            if (hovered_window.DockNodeAsHost != null)
                g.DebugHoveredDockNode = DockNodeTreeFindVisibleNodeByPos(hovered_window.DockNodeAsHost, g.IO.MousePos);
            else if (hovered_window.RootWindow != null && hovered_window.RootWindow.DockNode != null)
                g.DebugHoveredDockNode = hovered_window.RootWindow.DockNode;
        }

        // Process Docking requests
        for (int req_n = 0; req_n < dc.Requests.Size; req_n++)
        {
            ImGuiDockRequest req = dc.Requests[req_n];
            if (req.Type == ImGuiDockRequestType.ImGuiDockRequestType_Dock)
                DockContextProcessDock(ctx, req);
        }
        dc.Requests.resize(0);

        // Create windows for each automatic docking nodes
        // We can have NULL pointers when we delete nodes, but because ID are recycled this should amortize nicely (and our node count will never be very high)
        for (int n = 0; n < dc.Nodes.Data.Size; n++)
            if (dc.Nodes.Data.Data[n].val_p is ImGuiDockNode node)
                if (node.IsFloatingNode())
                    DockNodeUpdate(node);
    }

    // void ImGui::DockContextEndFrame(ImGuiContext* ctx)
    public static void DockContextEndFrame(ImGuiContext ctx)
    {
        // Draw backgrounds of node missing their window
        ImGuiContext g = ctx;
        ImGuiDockContext dc = g.DockContext;
        for (int n = 0; n < dc.Nodes.Data.Size; n++)
            if (dc.Nodes.Data.Data[n].val_p is ImGuiDockNode node)
                if (node.LastFrameActive == g.FrameCount && node.IsVisible && node.HostWindow != null && node.IsLeafNode() && !node.IsBgDrawnThisFrame)
                {
                    ImRect bg_rect = new ImRect(node.Pos + new ImVec2(0.0f, GetFrameHeight()), node.Pos + node.Size);
                    ImDrawFlags bg_rounding_flags = CalcRoundingFlagsForRectInRect(bg_rect, node.HostWindow.Rect(), g.Style.DockingSeparatorSize);
                    node.HostWindow.DrawList._Splitter.SetCurrentChannel(node.HostWindow.DrawList, DOCKING_HOST_DRAW_CHANNEL_BG);
                    node.HostWindow.DrawList.AddRectFilled(bg_rect.Min, bg_rect.Max, node.LastBgColor, node.HostWindow.WindowRounding, bg_rounding_flags);
                }
    }

    // ImGuiDockNode* ImGui::DockContextFindNodeByID(ImGuiContext* ctx, ImGuiID id)
    public static ImGuiDockNode DockContextFindNodeByID(ImGuiContext ctx, ImGuiID id)
    {
        return ctx.DockContext.Nodes.GetVoidPtr(id) as ImGuiDockNode;
    }

    // ImGuiID ImGui::DockContextGenNodeID(ImGuiContext* ctx)
    public static ImGuiID DockContextGenNodeID(ImGuiContext ctx)
    {
        // Generate an ID for new node (the exact ID value doesn't matter as long as it is not already used)
        // FIXME-OPT FIXME-DOCK: This is suboptimal, even if the node count is small enough not to be a worry.
        // We should poke in ctx->Nodes to find a suitable ID faster. Even more so trivial that ctx->Nodes lookup is already sorted.
        ImGuiID id = 0x0001;
        while (DockContextFindNodeByID(ctx, id) != null)
            id++;
        return id;
    }

    // static ImGuiDockNode* ImGui::DockContextAddNode(ImGuiContext* ctx, ImGuiID id)
    static ImGuiDockNode DockContextAddNode(ImGuiContext ctx, ImGuiID id)
    {
        // Generate an ID for the new node (the exact ID value doesn't matter as long as it is not already used) and add the first window.
        ImGuiContext g = ctx;
        if (id == 0)
            id = DockContextGenNodeID(ctx);
        else
            Debug.Assert(DockContextFindNodeByID(ctx, id) == null);

        // We don't set node->LastFrameAlive on construction. Nodes are always created at all time to reflect .ini settings!
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextAddNode 0x{0:X8}\n", id);
        ImGuiDockNode node = new ImGuiDockNode(id);
        ctx.DockContext.Nodes.SetVoidPtr(node.ID, node);
        return node;
    }
    // static void ImGui::DockContextRemoveNode(ImGuiContext* ctx, ImGuiDockNode* node, bool merge_sibling_into_parent_node)
    static void DockContextRemoveNode(ImGuiContext ctx, ImGuiDockNode node, bool merge_sibling_into_parent_node)
    {
        ImGuiContext g = ctx;

        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextRemoveNode 0x{0:X8}\n", node.ID);
        Debug.Assert(DockContextFindNodeByID(ctx, node.ID) == node);
        Debug.Assert(node.ChildNodes[0] == null && node.ChildNodes[1] == null);
        Debug.Assert(node.Windows.Size == 0);

        if (node.HostWindow != null)
            node.HostWindow.DockNodeAsHost = null;

        ImGuiDockNode parent_node = node.ParentNode;
        bool merge = merge_sibling_into_parent_node && parent_node != null;
        if (merge)
        {
            Debug.Assert(parent_node.ChildNodes[0] == node || parent_node.ChildNodes[1] == node);
            ImGuiDockNode sibling_node = (parent_node.ChildNodes[0] == node) ? parent_node.ChildNodes[1] : parent_node.ChildNodes[0];
            DockNodeTreeMerge(g, parent_node, sibling_node);
        }
        else
        {
            for (int n = 0; parent_node != null && n < parent_node.ChildNodes.Length; n++)
                if (parent_node.ChildNodes[n] == node)
                    node.ParentNode.ChildNodes[n] = null;
            DockContextDeleteNode(ctx, node);
        }
    }

    // Raw-ish delete
    // static void ImGui::DockContextDeleteNode(ImGuiContext* ctx, ImGuiDockNode* node)
    static void DockContextDeleteNode(ImGuiContext ctx, ImGuiDockNode node)
    {
        ImGuiDockContext dc = ctx.DockContext;
        node.TabBar = null; // (C++: IM_DELETE(node->TabBar); C# relies on the GC.)
        dc.Nodes.SetVoidPtr(node.ID, null);
    }

    // static int DockNodeComparerDepthMostFirst(const void* lhs, const void* rhs)
    static int DockNodeComparerDepthMostFirst(ImGuiDockNode a, ImGuiDockNode b)
    {
        return DockNodeGetDepth(b) - DockNodeGetDepth(a);
    }

    // Garbage collect unused nodes (run once at init time)
    // static void ImGui::DockContextPruneUnusedSettingsNodes(ImGuiContext* ctx)
    static void DockContextPruneUnusedSettingsNodes(ImGuiContext ctx)
    {
        ImGuiContext g = ctx;
        ImGuiDockContext dc = ctx.DockContext;
        Debug.Assert(g.Windows.Size == 0);

        ImPool<ImGuiDockContextPruneNodeData> pool = new ImPool<ImGuiDockContextPruneNodeData>();

        // Count child nodes and compute RootID
        for (int settings_n = 0; settings_n < dc.NodesSettings.Size; settings_n++)
        {
            ImGuiDockNodeSettings settings = dc.NodesSettings[settings_n];
            if (pool.GetByKey(settings.ID) != null)
            {
                settings.ID = 0; // Duplicate
                continue;
            }
            ImGuiDockContextPruneNodeData parent_data = settings.ParentNodeId != 0 ? pool.GetByKey(settings.ParentNodeId) : null;
            pool.GetOrAddByKey(settings.ID).RootId = parent_data != null ? parent_data.RootId : settings.ID;
            if (settings.ParentNodeId != 0)
                pool.GetOrAddByKey(settings.ParentNodeId).CountChildNodes++;
        }

        // Count reference to dock ids from dockspaces
        // We track the 'auto-DockNode <- manual-Window <- manual-DockSpace' in order to avoid 'auto-DockNode' being ditched by DockContextPruneUnusedSettingsNodes()
        for (int settings_n = 0; settings_n < dc.NodesSettings.Size; settings_n++)
        {
            ImGuiDockNodeSettings settings = dc.NodesSettings[settings_n];
            if (settings.ParentWindowId != 0)
            {
                ImGuiWindowSettings window_settings = FindWindowSettingsByID(settings.ParentWindowId);
                if (window_settings != null && window_settings.DockId != 0)
                {
                    ImGuiDockContextPruneNodeData data = pool.GetByKey(window_settings.DockId);
                    if (data != null)
                        data.CountChildNodes++;
                }
            }
        }

        // Count reference to dock ids from window settings
        // We guard against the possibility of an invalid .ini file (RootID may point to a missing node)
        for (int settings_i = 0; settings_i < g.SettingsWindows.size(); settings_i++)
        {
            ImGuiWindowSettings settings = g.SettingsWindows[settings_i];
            ImGuiID dock_id = settings.DockId;
            if (dock_id == 0)
                continue;
            ImGuiDockContextPruneNodeData data = pool.GetByKey(dock_id);
            if (data != null)
            {
                data.CountWindows++;
                ImGuiDockContextPruneNodeData data_root = (data.RootId == dock_id) ? data : pool.GetByKey(data.RootId);
                if (data_root != null)
                    data_root.CountChildWindows++;
            }
        }

        // Prune
        for (int settings_n = 0; settings_n < dc.NodesSettings.Size; settings_n++)
        {
            ImGuiDockNodeSettings settings = dc.NodesSettings[settings_n];
            ImGuiDockContextPruneNodeData data = pool.GetByKey(settings.ID);
            if (data == null || data.CountWindows > 1)
                continue;
            ImGuiDockContextPruneNodeData data_root = (settings.ID == data.RootId) ? data : pool.GetByKey(data.RootId);
            ImGuiDockContextPruneNodeData data_parent = settings.ParentNodeId != 0 ? pool.GetByKey(settings.ParentNodeId) : null;

            bool remove = false;
            remove |= (data.CountWindows == 1 && settings.ParentNodeId == 0 && data.CountChildNodes == 0 && (settings.Flags & ImGuiDockNodeFlags_CentralNode) == 0);  // Floating root node with only 1 window
            remove |= (data.CountWindows == 0 && settings.ParentNodeId == 0 && data.CountChildNodes == 0); // Leaf nodes with 0 window
            remove |= (data_root == null || data_root.CountChildWindows == 0);
            if (remove)
            {
                IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextPruneUnusedSettingsNodes: Prune 0x{0:X8}\n", settings.ID);
                DockSettingsRemoveNodeReferences(new ImGuiID[] { settings.ID }, 1);
                settings.ID = 0;
            }
            else if (data_parent != null && data_parent.CountChildNodes == 1)
            {
                IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextPruneUnusedSettingsNodes: Merge 0x{0:X8}->0X{1:X8}\n", settings.ID, settings.ParentNodeId);
                DockSettingsRenameNodeReferences(settings.ID, settings.ParentNodeId);
                settings.ID = 0;
            }
        }
    }

    // static void ImGui::DockContextBuildNodesFromSettings(ImGuiContext* ctx, ImGuiDockNodeSettings* node_settings_array, int node_settings_count)
    static void DockContextBuildNodesFromSettings(ImGuiContext ctx, ImVector<ImGuiDockNodeSettings> node_settings_array)
    {
        // Build nodes
        for (int node_n = 0; node_n < node_settings_array.Size; node_n++)
        {
            ImGuiDockNodeSettings settings = node_settings_array[node_n];
            if (settings.ID == 0)
                continue;
            if (DockContextFindNodeByID(ctx, settings.ID) != null)
            {
                IMGUI_DEBUG_LOG_DOCKING(ctx, "[docking] DockContextBuildNodesFromSettings: skip duplicate node 0x{0:X8}\n", settings.ID);
                continue;
            }
            ImGuiDockNode node = DockContextAddNode(ctx, settings.ID);
            node.ParentNode = settings.ParentNodeId != 0 ? DockContextFindNodeByID(ctx, settings.ParentNodeId) : null;
            node.Pos = new ImVec2(settings.Pos.x, settings.Pos.y);
            node.Size = new ImVec2(settings.Size.x, settings.Size.y);
            node.SizeRef = new ImVec2(settings.SizeRef.x, settings.SizeRef.y);
            node.SizeRef = new ImVec2(settings.SizeRef.x, settings.SizeRef.y);
            node.AuthorityForPos = node.AuthorityForSize = node.AuthorityForViewport = (byte)ImGuiDataAuthority_DockNode;
            if (node.ParentNode != null && node.ParentNode.ChildNodes[0] == null)
                node.ParentNode.ChildNodes[0] = node;
            else if (node.ParentNode != null && node.ParentNode.ChildNodes[1] == null)
                node.ParentNode.ChildNodes[1] = node;
            node.SelectedTabId = settings.SelectedTabId;
            node.SplitAxis = settings.SplitAxis;
            node.SetLocalFlags(settings.Flags & ImGuiDockNodeFlags_SavedFlagsMask_);

            // Bind host window immediately if it already exist (in case of a rebuild)
            // This is useful as the RootWindowForTitleBarHighlight links necessary to highlight the currently focused node requires node->HostWindow to be set.
            ImGuiDockNode root_node = DockNodeGetRootNode(node);
            node.HostWindow = FindWindowByName(DockNodeGetHostWindowTitle(root_node));
        }
    }

    // void ImGui::DockContextBuildAddWindowsToNodes(ImGuiContext* ctx, ImGuiID root_id)
    public static void DockContextBuildAddWindowsToNodes(ImGuiContext ctx, ImGuiID root_id)
    {
        // Rebind all windows to nodes (they can also lazily rebind but we'll have a visible glitch during the first frame)
        ImGuiContext g = ctx;
        for (int window_n = 0; window_n < g.Windows.Size; window_n++)
        {
            ImGuiWindow window = g.Windows[window_n];
            if (window.DockId == 0 || window.LastFrameActive < g.FrameCount - 1)
                continue;
            if (window.DockNode != null)
                continue;

            ImGuiDockNode node = DockContextFindNodeByID(ctx, window.DockId);
            Debug.Assert(node != null);   // This should have been called after DockContextBuildNodesFromSettings()
            if (root_id == 0 || DockNodeGetRootNode(node).ID == root_id)
                DockNodeAddWindow(node, window, true);
        }
    }

    //-----------------------------------------------------------------------------
    // Docking: ImGuiDockContext Docking/Undocking functions
    //-----------------------------------------------------------------------------

    // void ImGui::DockContextQueueDock(ImGuiContext* ctx, ImGuiWindow* target, ImGuiDockNode* target_node, ImGuiWindow* payload, ImGuiDir split_dir, float split_ratio, bool split_outer)
    public static void DockContextQueueDock(ImGuiContext ctx, ImGuiWindow target, ImGuiDockNode target_node, ImGuiWindow payload, int split_dir, float split_ratio, bool split_outer)
    {
        Debug.Assert(target != payload);
        ImGuiDockRequest req = new ImGuiDockRequest();
        req.Type = ImGuiDockRequestType.ImGuiDockRequestType_Dock;
        req.DockTargetWindow = target;
        req.DockTargetNode = target_node;
        req.DockPayload = payload;
        req.DockSplitDir = split_dir;
        req.DockSplitRatio = split_ratio;
        req.DockSplitOuter = split_outer;
        ctx.DockContext.Requests.push_back(req);
    }

    // void ImGui::DockContextQueueUndockWindow(ImGuiContext* ctx, ImGuiWindow* window)
    public static void DockContextQueueUndockWindow(ImGuiContext ctx, ImGuiWindow window)
    {
        ImGuiDockRequest req = new ImGuiDockRequest();
        req.Type = ImGuiDockRequestType.ImGuiDockRequestType_Undock;
        req.UndockTargetWindow = window;
        ctx.DockContext.Requests.push_back(req);
    }

    // void ImGui::DockContextQueueUndockNode(ImGuiContext* ctx, ImGuiDockNode* node)
    public static void DockContextQueueUndockNode(ImGuiContext ctx, ImGuiDockNode node)
    {
        ImGuiDockRequest req = new ImGuiDockRequest();
        req.Type = ImGuiDockRequestType.ImGuiDockRequestType_Undock;
        req.UndockTargetNode = node;
        ctx.DockContext.Requests.push_back(req);
    }

    // static void ImGui::DockContextQueueNotifyRemovedNode(ImGuiContext* ctx, ImGuiDockNode* node)
    static void DockContextQueueNotifyRemovedNode(ImGuiContext ctx, ImGuiDockNode node)
    {
        ImGuiDockContext dc = ctx.DockContext;
        for (int req_n = 0; req_n < dc.Requests.Size; req_n++)
            if (dc.Requests[req_n].DockTargetNode == node)
                dc.Requests[req_n].Type = ImGuiDockRequestType.ImGuiDockRequestType_None;
    }

    // void ImGui::DockContextProcessDock(ImGuiContext* ctx, ImGuiDockRequest* req)
    static void DockContextProcessDock(ImGuiContext ctx, ImGuiDockRequest req)
    {
        Debug.Assert((req.Type == ImGuiDockRequestType.ImGuiDockRequestType_Dock && req.DockPayload != null) || (req.Type == ImGuiDockRequestType.ImGuiDockRequestType_Split && req.DockPayload == null));
        Debug.Assert(req.DockTargetWindow != null || req.DockTargetNode != null);

        ImGuiContext g = ctx;

        ImGuiWindow payload_window = req.DockPayload;     // Optional
        ImGuiWindow target_window = req.DockTargetWindow;
        ImGuiDockNode node = req.DockTargetNode;
        if (payload_window != null)
            IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextProcessDock node {0:X8} target '{1}' dock window '{2}', split_dir {3}\n", node != null ? node.ID : 0, target_window != null ? target_window.Name : "NULL", payload_window.Name, req.DockSplitDir);
        else
            IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextProcessDock node {0:X8}, split_dir {1}\n", node != null ? node.ID : 0, req.DockSplitDir);

        // Decide which Tab will be selected at the end of the operation
        ImGuiID next_selected_id = 0;
        ImGuiDockNode payload_node = null;
        if (payload_window != null)
        {
            payload_node = payload_window.DockNodeAsHost;
            payload_window.DockNodeAsHost = null; // Important to clear this as the node will have its life as a child which might be merged/deleted later.
            if (payload_node != null && payload_node.IsLeafNode())
                next_selected_id = payload_node.TabBar.NextSelectedTabId != 0 ? payload_node.TabBar.NextSelectedTabId : payload_node.TabBar.SelectedTabId;
            if (payload_node == null)
                next_selected_id = payload_window.TabId;
        }

        // FIXME-DOCK: When we are trying to dock an existing single-window node into a loose window, transfer Node ID as well
        // When processing an interactive split, usually LastFrameAlive will be < g.FrameCount. But DockBuilder operations can make it ==.
        if (node != null)
            Debug.Assert(node.LastFrameAlive <= g.FrameCount);
        if (node != null && target_window != null && node == target_window.DockNodeAsHost)
            Debug.Assert(node.Windows.Size > 0 || node.IsSplitNode() || node.IsCentralNode());

        // Create new node and add existing window to it
        if (node == null)
        {
            node = DockContextAddNode(ctx, 0);
            node.Pos = target_window.Pos;
            node.Size = target_window.Size;
            if (target_window.DockNodeAsHost == null)
            {
                DockNodeAddWindow(node, target_window, true);
                node.TabBar.Tabs[0].Flags &= ~ImGuiTabItemFlags_Unsorted;
                target_window.DockIsActive = true;
            }
        }

        int split_dir = req.DockSplitDir;
        if (split_dir != ImGuiDir_None)
        {
            // Split into two, one side will be our payload node unless we are dropping a loose window
            int split_axis = (split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Right) ? ImGuiAxis_X : ImGuiAxis_Y;
            int split_inheritor_child_idx = (split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? 1 : 0; // Current contents will be moved to the opposite side
            float split_ratio = req.DockSplitRatio;
            DockNodeTreeSplit(ctx, node, split_axis, split_inheritor_child_idx, split_ratio, payload_node);  // payload_node may be NULL here!
            ImGuiDockNode new_node = node.ChildNodes[split_inheritor_child_idx ^ 1];
            new_node.HostWindow = node.HostWindow;
            node = new_node;
        }
        node.SetLocalFlags(node.LocalFlags & ~ImGuiDockNodeFlags_HiddenTabBar);

        if (node != payload_node)
        {
            // Create tab bar before we call DockNodeMoveWindows (which would attempt to move the old tab-bar, which would lead us to payload tabs wrongly appearing before target tabs!)
            if (node.Windows.Size > 0 && node.TabBar == null)
            {
                DockNodeAddTabBar(node);
                for (int n = 0; n < node.Windows.Size; n++)
                    TabBarAddTab(node.TabBar, ImGuiTabItemFlags_None, node.Windows[n]);
            }

            if (payload_node != null)
            {
                // Transfer full payload node (with 1+ child windows or child nodes)
                if (payload_node.IsSplitNode())
                {
                    if (node.Windows.Size > 0)
                    {
                        // We can dock a split payload into a node that already has windows _only_ if our payload is a node tree with a single visible node.
                        // In this situation, we move the windows of the target node into the currently visible node of the payload.
                        // This allows us to preserve some of the underlying dock tree settings nicely.
                        Debug.Assert(payload_node.OnlyNodeWithWindows != null); // The docking should have been blocked by DockNodePreviewDockSetup() early on and never submitted.
                        ImGuiDockNode visible_node = payload_node.OnlyNodeWithWindows;
                        if (visible_node.TabBar != null)
                            Debug.Assert(visible_node.TabBar.Tabs.Size > 0);
                        DockNodeMoveWindows(node, visible_node);
                        DockNodeMoveWindows(visible_node, node);
                        DockSettingsRenameNodeReferences(node.ID, visible_node.ID);
                    }
                    if (node.IsCentralNode())
                    {
                        // Central node property needs to be moved to a leaf node, pick the last focused one.
                        // FIXME-DOCK: If we had to transfer other flags here, what would the policy be?
                        ImGuiDockNode last_focused_node = DockContextFindNodeByID(ctx, payload_node.LastFocusedNodeId);
                        Debug.Assert(last_focused_node != null);
                        ImGuiDockNode last_focused_root_node = DockNodeGetRootNode(last_focused_node);
                        Debug.Assert(last_focused_root_node == DockNodeGetRootNode(payload_node));
                        last_focused_node.SetLocalFlags(last_focused_node.LocalFlags | ImGuiDockNodeFlags_CentralNode);
                        node.SetLocalFlags(node.LocalFlags & ~ImGuiDockNodeFlags_CentralNode);
                        last_focused_root_node.CentralNode = last_focused_node;
                    }

                    Debug.Assert(node.Windows.Size == 0);
                    DockNodeMoveChildNodes(node, payload_node);
                }
                else
                {
                    ImGuiID payload_dock_id = payload_node.ID;
                    DockNodeMoveWindows(node, payload_node);
                    DockSettingsRenameNodeReferences(payload_dock_id, node.ID);
                }
                DockContextRemoveNode(ctx, payload_node, true);
            }
            else if (payload_window != null)
            {
                // Transfer single window
                ImGuiID payload_dock_id = payload_window.DockId;
                node.VisibleWindow = payload_window;
                DockNodeAddWindow(node, payload_window, true);
                if (payload_dock_id != 0)
                    DockSettingsRenameNodeReferences(payload_dock_id, node.ID);
            }
        }
        else
        {
            // When docking a floating single window node we want to reevaluate auto-hiding of the tab bar
            node.WantHiddenTabBarUpdate = true;
        }

        // Update selection immediately
        if (node.TabBar != null)
            node.TabBar.NextSelectedTabId = next_selected_id;
        MarkIniSettingsDirty();
    }

    // This is mostly used for automation.
    // bool ImGui::DockContextCalcDropPosForDocking(ImGuiWindow* target, ImGuiDockNode* target_node, ImGuiWindow* payload_window, ImGuiDockNode* payload_node, ImGuiDir split_dir, bool split_outer, ImVec2* out_pos)
    public static bool DockContextCalcDropPosForDocking(ImGuiWindow target, ImGuiDockNode target_node, ImGuiWindow payload_window, ImGuiDockNode payload_node, int split_dir, bool split_outer, out ImVec2 out_pos)
    {
        out_pos = default;
        if (target != null && target_node == null)
            target_node = target.DockNode;

        // In DockNodePreviewDockSetup() for a root central node instead of showing both "inner" and "outer" drop rects
        // (which would be functionally identical) we only show the outer one. Reflect this here.
        if (target_node != null && target_node.ParentNode == null && target_node.IsCentralNode() && split_dir != ImGuiDir_None)
            split_outer = true;
        ImGuiDockPreviewData split_data = new ImGuiDockPreviewData();
        DockNodePreviewDockSetup(target, target_node, payload_window, payload_node, split_data, false, split_outer);
        if (split_data.DropRectsDraw[split_dir + 1].IsInverted())
            return false;
        out_pos = split_data.DropRectsDraw[split_dir + 1].GetCenter();
        return true;
    }

    // int ImGui::DockNodeGetTabOrder(ImGuiWindow* window)
    public static int DockNodeGetTabOrder(ImGuiWindow window)
    {
        ImGuiTabBar tab_bar = window.DockNode.TabBar;
        if (tab_bar == null)
            return -1;
        ImGuiTabItem tab = TabBarFindTabByID(tab_bar, window.TabId);
        return tab != null ? TabBarGetTabOrder(tab_bar, tab) : -1;
    }

    // static void DockNodeHideWindowDuringHostWindowCreation(ImGuiWindow* window)
    static void DockNodeHideWindowDuringHostWindowCreation(ImGuiWindow window)
    {
        window.Hidden = true;
        window.HiddenFramesCanSkipItems = window.Active ? (sbyte)1 : (sbyte)2;
    }

    // static void ImGui::DockNodeAddWindow(ImGuiDockNode* node, ImGuiWindow* window, bool add_to_tab_bar)
    static void DockNodeAddWindow(ImGuiDockNode node, ImGuiWindow window, bool add_to_tab_bar)
    {
        ImGuiContext g = GImGui;
        if (window.DockNode != null)
        {
            // Can overwrite an existing window->DockNode (e.g. pointing to a disabled DockSpace node)
            Debug.Assert(window.DockNode.ID != node.ID);
            DockNodeRemoveWindow(window.DockNode, window, 0);
        }
        Debug.Assert(window.DockNode == null || window.DockNodeAsHost == null);
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockNodeAddWindow node 0x{0:X8} window '{1}'\n", node.ID, window.Name);

        // If more than 2 windows appeared on the same frame leading to the creation of a new hosting window,
        // we'll hide windows until the host window is ready. Hide the 1st window after its been output (so it is not visible for one frame).
        // We will call DockNodeHideWindowDuringHostWindowCreation() on ourselves in Begin()
        if (node.HostWindow == null && node.Windows.Size == 1 && node.Windows[0].WasActive == false)
            DockNodeHideWindowDuringHostWindowCreation(node.Windows[0]);

        node.Windows.push_back(window);
        node.WantHiddenTabBarUpdate = true;
        window.DockNode = node;
        window.DockId = node.ID;
        window.DockIsActive = (node.Windows.Size > 1);
        window.DockTabWantClose = false;

        // When reactivating a node with one or two loose window, the window pos/size/viewport are authoritative over the node storage.
        // In particular it is important we init the viewport from the first window so we don't create two viewports and drop one.
        if (node.HostWindow == null && node.IsFloatingNode())
        {
            if (node.AuthorityForPos == ImGuiDataAuthority_Auto)
                node.AuthorityForPos = (byte)ImGuiDataAuthority_Window;
            if (node.AuthorityForSize == ImGuiDataAuthority_Auto)
                node.AuthorityForSize = (byte)ImGuiDataAuthority_Window;
            if (node.AuthorityForViewport == ImGuiDataAuthority_Auto)
                node.AuthorityForViewport = (byte)ImGuiDataAuthority_Window;
        }

        // Add to tab bar if requested
        if (add_to_tab_bar)
        {
            if (node.TabBar == null)
            {
                DockNodeAddTabBar(node);
                node.TabBar.SelectedTabId = node.TabBar.NextSelectedTabId = node.SelectedTabId;

                // Add existing windows
                for (int n = 0; n < node.Windows.Size - 1; n++)
                    TabBarAddTab(node.TabBar, ImGuiTabItemFlags_None, node.Windows[n]);
            }
            TabBarAddTab(node.TabBar, ImGuiTabItemFlags_Unsorted, window);
        }

        DockNodeUpdateVisibleFlag(node);

        // Update this without waiting for the next time we Begin() in the window, so our host window will have the proper title bar color on its first frame.
        if (node.HostWindow != null)
            UpdateWindowParentAndRootLinks(window, window.Flags | ImGuiWindowFlags_ChildWindow, node.HostWindow);
    }

    // static void ImGui::DockNodeRemoveWindow(ImGuiDockNode* node, ImGuiWindow* window, ImGuiID save_dock_id)
    static void DockNodeRemoveWindow(ImGuiDockNode node, ImGuiWindow window, ImGuiID save_dock_id)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(window.DockNode == node);
        //Debug.Assert(window.RootWindowDockTree == node.HostWindow);
        //Debug.Assert(window.LastFrameActive < g.FrameCount);    // We may call this from Begin()
        Debug.Assert(save_dock_id == 0 || save_dock_id == node.ID);
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockNodeRemoveWindow node 0x{0:X8} window '{1}'\n", node.ID, window.Name);

        window.DockNode = null;
        window.DockIsActive = window.DockTabWantClose = false;
        window.DockId = save_dock_id;
        window.Flags &= ~ImGuiWindowFlags_ChildWindow;
        if (window.ParentWindow != null)
            FindEraseWindow(window.ParentWindow.DC.ChildWindows, window);
        UpdateWindowParentAndRootLinks(window, window.Flags, null); // Update immediately

        if (node.HostWindow != null && node.HostWindow.ViewportOwned)
        {
            // When undocking from a user interaction this will always run in NewFrame() and have not much effect.
            // But mid-frame, if we clear viewport we need to mark window as hidden as well.
            window.Viewport = null;
            window.ViewportId = 0;
            window.ViewportOwned = false;
            window.Hidden = true;
        }

        // Remove window
        bool erased = false;
        for (int n = 0; n < node.Windows.Size; n++)
            if (node.Windows[n] == window)
            {
                node.Windows.erase(n);
                erased = true;
                break;
            }
        Debug.Assert(erased);
        if (node.VisibleWindow == window)
            node.VisibleWindow = null;

        // Remove tab and possibly tab bar
        node.WantHiddenTabBarUpdate = true;
        if (node.TabBar != null)
        {
            TabBarRemoveTab(node.TabBar, window.TabId);
            int tab_count_threshold_for_tab_bar = node.IsCentralNode() ? 1 : 2;
            if (node.Windows.Size < tab_count_threshold_for_tab_bar)
                DockNodeRemoveTabBar(node);
        }

        if (node.Windows.Size == 0 && !node.IsCentralNode() && !node.IsDockSpace() && window.DockId != node.ID)
        {
            // Automatic dock node delete themselves if they are not holding at least one tab
            DockContextRemoveNode(g, node, true);
            return;
        }

        if (node.Windows.Size == 1 && !node.IsCentralNode() && node.HostWindow != null)
        {
            ImGuiWindow remaining_window = node.Windows[0];
            // Note: we used to transport viewport ownership here.
            remaining_window.Collapsed = node.HostWindow.Collapsed;
        }

        // Update visibility immediately is required so the DockNodeUpdateRemoveInactiveChilds() processing can reflect changes up the tree
        DockNodeUpdateVisibleFlag(node);
    }

    // static void ImGui::DockNodeMoveChildNodes(ImGuiDockNode* dst_node, ImGuiDockNode* src_node)
    static void DockNodeMoveChildNodes(ImGuiDockNode dst_node, ImGuiDockNode src_node)
    {
        Debug.Assert(dst_node.Windows.Size == 0);
        dst_node.ChildNodes[0] = src_node.ChildNodes[0];
        dst_node.ChildNodes[1] = src_node.ChildNodes[1];
        if (dst_node.ChildNodes[0] != null)
            dst_node.ChildNodes[0].ParentNode = dst_node;
        if (dst_node.ChildNodes[1] != null)
            dst_node.ChildNodes[1].ParentNode = dst_node;
        dst_node.SplitAxis = src_node.SplitAxis;
        dst_node.SizeRef = src_node.SizeRef;
        src_node.ChildNodes[0] = src_node.ChildNodes[1] = null;
    }

    // static void ImGui::DockNodeMoveWindows(ImGuiDockNode* dst_node, ImGuiDockNode* src_node)
    static void DockNodeMoveWindows(ImGuiDockNode dst_node, ImGuiDockNode src_node)
    {
        // Insert tabs in the same orders as currently ordered (node->Windows isn't ordered)
        Debug.Assert(src_node != null && dst_node != null && dst_node != src_node);
        ImGuiTabBar src_tab_bar = src_node.TabBar;
        if (src_tab_bar != null)
            Debug.Assert(src_node.Windows.Size <= src_node.TabBar.Tabs.Size);

        // If the dst_node is empty we can just move the entire tab bar (to preserve selection, scrolling, etc.)
        bool move_tab_bar = (src_tab_bar != null) && (dst_node.TabBar == null);
        if (move_tab_bar)
        {
            dst_node.TabBar = src_node.TabBar;
            src_node.TabBar = null;
        }

        // Tab order is not important here, it is preserved by sorting in DockNodeUpdateTabBar().
        for (int window_n = 0; window_n < src_node.Windows.Size; window_n++)
        {
            ImGuiWindow window = src_node.Windows[window_n];
            window.DockNode = null;
            window.DockIsActive = false;
            DockNodeAddWindow(dst_node, window, !move_tab_bar);
        }
        src_node.Windows.clear();

        if (!move_tab_bar && src_node.TabBar != null)
        {
            if (dst_node.TabBar != null)
                dst_node.TabBar.SelectedTabId = src_node.TabBar.SelectedTabId;
            DockNodeRemoveTabBar(src_node);
        }
    }

    // static void ImGui::DockNodeApplyPosSizeToWindows(ImGuiDockNode* node)
    static void DockNodeApplyPosSizeToWindows(ImGuiDockNode node)
    {
        for (int window_n = 0; window_n < node.Windows.Size; window_n++)
        {
            ImGuiWindow window = node.Windows[window_n];
            SetWindowPos(window, node.Pos, ImGuiCond_Always); // We don't assign directly to Pos because it can break the calculation of SizeContents on next frame
            SetWindowSize(window, node.Size, ImGuiCond_Always);
        }
    }

    // static void ImGui::DockNodeHideHostWindow(ImGuiDockNode* node)
    static void DockNodeHideHostWindow(ImGuiDockNode node)
    {
        if (node.HostWindow != null)
        {
            if (node.HostWindow.DockNodeAsHost == node)
                node.HostWindow.DockNodeAsHost = null;
            node.HostWindow = null;
        }

        if (node.Windows.Size == 1)
        {
            node.VisibleWindow = node.Windows[0];
            node.Windows[0].DockIsActive = false;
        }

        if (node.TabBar != null)
            DockNodeRemoveTabBar(node);
    }

    // Search function called once by root node in DockNodeUpdate()
    // struct ImGuiDockNodeTreeInfo
    public class ImGuiDockNodeTreeInfo
    {
        public ImGuiDockNode CentralNode;
        public ImGuiDockNode FirstNodeWithWindows;
        public int CountNodesWithWindows;

        public ImGuiDockNodeTreeInfo() { }
    }

    // static void DockNodeFindInfo(ImGuiDockNode* node, ImGuiDockNodeTreeInfo* info)
    static void DockNodeFindInfo(ImGuiDockNode node, ImGuiDockNodeTreeInfo info)
    {
        if (node.Windows.Size > 0)
        {
            if (info.FirstNodeWithWindows == null)
                info.FirstNodeWithWindows = node;
            info.CountNodesWithWindows++;
        }
        if (node.IsCentralNode())
        {
            Debug.Assert(info.CentralNode == null); // Should be only one
            Debug.Assert(node.IsLeafNode(), "If you get this assert: please submit .ini file + repro of actions leading to this.");
            info.CentralNode = node;
        }
        if (info.CountNodesWithWindows > 1 && info.CentralNode != null)
            return;
        if (node.ChildNodes[0] != null)
            DockNodeFindInfo(node.ChildNodes[0], info);
        if (node.ChildNodes[1] != null)
            DockNodeFindInfo(node.ChildNodes[1], info);
    }

    // static ImGuiWindow* ImGui::DockNodeFindWindowByID(ImGuiDockNode* node, ImGuiID id)
    static ImGuiWindow DockNodeFindWindowByID(ImGuiDockNode node, ImGuiID id)
    {
        Debug.Assert(id != 0);
        for (int window_n = 0; window_n < node.Windows.Size; window_n++)
            if (node.Windows[window_n].ID == id)
                return node.Windows[window_n];
        return null;
    }

    // - Remove inactive windows/nodes.
    // - Update visibility flag.
    // static void ImGui::DockNodeUpdateFlagsAndCollapse(ImGuiDockNode* node)
    static void DockNodeUpdateFlagsAndCollapse(ImGuiDockNode node)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(node.ParentNode == null || node.ParentNode.ChildNodes[0] == node || node.ParentNode.ChildNodes[1] == node);

        // Inherit most flags
        if (node.ParentNode != null)
            node.SharedFlags = node.ParentNode.SharedFlags & ImGuiDockNodeFlags_SharedFlagsInheritMask_;

        // Recurse into children
        // There is the possibility that one of our child becoming empty will delete itself and moving its sibling contents into 'node'.
        // If 'node->ChildNode[0]' delete itself, then 'node->ChildNode[1]->Windows' will be moved into 'node'
        // If 'node->ChildNode[1]' delete itself, then 'node->ChildNode[0]->Windows' will be moved into 'node' and the "remove inactive windows" loop will have run twice on those windows (harmless)
        node.HasCentralNodeChild = false;
        if (node.ChildNodes[0] != null)
            DockNodeUpdateFlagsAndCollapse(node.ChildNodes[0]);
        if (node.ChildNodes[1] != null)
            DockNodeUpdateFlagsAndCollapse(node.ChildNodes[1]);

        // Remove inactive windows, collapse nodes
        // Merge node flags overrides stored in windows
        node.LocalFlagsInWindows = ImGuiDockNodeFlags_None;
        for (int window_n = 0; window_n < node.Windows.Size; window_n++)
        {
            ImGuiWindow window = node.Windows[window_n];
            Debug.Assert(window.DockNode == node);

            bool node_was_active = (node.LastFrameActive + 1 == g.FrameCount);
            bool remove = false;
            remove |= node_was_active && (window.WasActive == false); // Can't use 'window->LastFrameActive + 1 < g.FrameCount'. (see #9151)
            remove |= node_was_active && (node.WantCloseAll || node.WantCloseTabId == window.TabId) && window.HasCloseButton && (window.Flags & ImGuiWindowFlags_UnsavedDocument) == 0;  // Submit all _expected_ closure from last frame
            remove |= (window.DockTabWantClose);
            if (remove)
            {
                window.DockTabWantClose = false;
                if (node.Windows.Size == 1 && !node.IsCentralNode())
                {
                    DockNodeHideHostWindow(node);
                    node.State = ImGuiDockNodeState_HostWindowHiddenBecauseSingleWindow;
                    DockNodeRemoveWindow(node, window, node.ID); // Will delete the node so it'll be invalid on return
                    return;
                }
                DockNodeRemoveWindow(node, window, node.ID);
                window_n--;
                continue;
            }

            // FIXME-DOCKING: Missing policies for conflict resolution, hence the "Experimental" tag on this.
            //node.LocalFlagsInWindow &= ~window.WindowClass.DockNodeFlagsOverrideClear;
            node.LocalFlagsInWindows |= window.WindowClass.DockNodeFlagsOverrideSet;
        }
        node.UpdateMergedFlags();

        // Auto-hide tab bar option
        int node_flags = node.MergedFlags;
        if (node.WantHiddenTabBarUpdate && node.Windows.Size == 1 && (node_flags & ImGuiDockNodeFlags_AutoHideTabBar) != 0 && !node.IsHiddenTabBar())
            node.WantHiddenTabBarToggle = true;
        node.WantHiddenTabBarUpdate = false;

        // Cancel toggling if we know our tab bar is enforced to be hidden at all times
        if (node.WantHiddenTabBarToggle && node.VisibleWindow != null && (node.VisibleWindow.WindowClass.DockNodeFlagsOverrideSet & ImGuiDockNodeFlags_HiddenTabBar) != 0)
            node.WantHiddenTabBarToggle = false;

        // Apply toggles at a single point of the frame (here!)
        int prev_local_flags = node.LocalFlags;
        if (node.Windows.Size > 1)
            node.SetLocalFlags(node.LocalFlags & ~ImGuiDockNodeFlags_HiddenTabBar);
        else if (node.WantHiddenTabBarToggle)
            node.SetLocalFlags(node.LocalFlags ^ ImGuiDockNodeFlags_HiddenTabBar);
        if (((node.LocalFlags ^ prev_local_flags) & ImGuiDockNodeFlags_SavedFlagsMask_) != 0)
            MarkIniSettingsDirty(); // Bit flaky to only do this here. Perhaps compare node flags every frame? #9380
        node.WantHiddenTabBarToggle = false;

        DockNodeUpdateVisibleFlag(node);
    }

    // This is rarely called as DockNodeUpdateForRootNode() generally does it most frames.
    // static void ImGui::DockNodeUpdateHasCentralNodeChild(ImGuiDockNode* node)
    static void DockNodeUpdateHasCentralNodeChild(ImGuiDockNode node)
    {
        node.HasCentralNodeChild = false;
        if (node.ChildNodes[0] != null)
            DockNodeUpdateHasCentralNodeChild(node.ChildNodes[0]);
        if (node.ChildNodes[1] != null)
            DockNodeUpdateHasCentralNodeChild(node.ChildNodes[1]);
        if (node.IsRootNode())
        {
            ImGuiDockNode mark_node = node.CentralNode;
            while (mark_node != null)
            {
                mark_node.HasCentralNodeChild = true;
                mark_node = mark_node.ParentNode;
            }
        }
    }

    // static void ImGui::DockNodeUpdateVisibleFlag(ImGuiDockNode* node)
    static void DockNodeUpdateVisibleFlag(ImGuiDockNode node)
    {
        // Update visibility flag
        bool is_visible = (node.ParentNode == null) ? node.IsDockSpace() : node.IsCentralNode();
        is_visible |= (node.Windows.Size > 0);
        is_visible |= (node.ChildNodes[0] != null && node.ChildNodes[0].IsVisible);
        is_visible |= (node.ChildNodes[1] != null && node.ChildNodes[1].IsVisible);
        node.IsVisible = is_visible;
    }

    // static void ImGui::DockNodeStartMouseMovingWindow(ImGuiDockNode* node, ImGuiWindow* window)
    static void DockNodeStartMouseMovingWindow(ImGuiDockNode node, ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(node.WantMouseMove == true);
        StartMouseMovingWindow(window);
        g.ActiveIdClickOffset = g.IO.MouseClickedPos[0] - node.Pos;
        g.MovingWindow = window; // If we are docked into a non moveable root window, StartMouseMovingWindow() won't set g.MovingWindow. Override that decision.
        node.WantMouseMove = false;
    }

    // Update CentralNode, OnlyNodeWithWindows, LastFocusedNodeID. Copy window class.
    // static void ImGui::DockNodeUpdateForRootNode(ImGuiDockNode* node)
    static void DockNodeUpdateForRootNode(ImGuiDockNode node)
    {
        DockNodeUpdateFlagsAndCollapse(node);

        // - Setup central node pointers
        // - Find if there's only a single visible window in the hierarchy (in which case we need to display a regular title bar -> FIXME-DOCK: that last part is not done yet!)
        // Cannot merge this with DockNodeUpdateFlagsAndCollapse() because FirstNodeWithWindows is found after window removal and child collapsing
        ImGuiDockNodeTreeInfo info = new ImGuiDockNodeTreeInfo();
        DockNodeFindInfo(node, info);
        node.CentralNode = info.CentralNode;
        node.OnlyNodeWithWindows = (info.CountNodesWithWindows == 1) ? info.FirstNodeWithWindows : null;
        node.CountNodeWithWindows = info.CountNodesWithWindows;
        if (node.LastFocusedNodeId == 0 && info.FirstNodeWithWindows != null)
            node.LastFocusedNodeId = info.FirstNodeWithWindows.ID;

        // Copy the window class from of our first window so it can be used for proper dock filtering.
        // When node has mixed windows, prioritize the class with the most constraint (DockingAllowUnclassed = false) as the reference to copy.
        // FIXME-DOCK: We don't recurse properly, this code could be reworked to work from DockNodeUpdateScanRec.
        ImGuiDockNode first_node_with_windows = info.FirstNodeWithWindows;
        if (first_node_with_windows != null)
        {
            node.WindowClass = first_node_with_windows.Windows[0].WindowClass;
            for (int n = 1; n < first_node_with_windows.Windows.Size; n++)
                if (first_node_with_windows.Windows[n].WindowClass.DockingAllowUnclassed == false)
                {
                    node.WindowClass = first_node_with_windows.Windows[n].WindowClass;
                    break;
                }
        }

        ImGuiDockNode mark_node = node.CentralNode;
        while (mark_node != null)
        {
            mark_node.HasCentralNodeChild = true;
            mark_node = mark_node.ParentNode;
        }
    }

    // static void DockNodeSetupHostWindow(ImGuiDockNode* node, ImGuiWindow* host_window)
    static void DockNodeSetupHostWindow(ImGuiDockNode node, ImGuiWindow host_window)
    {
        // Remove ourselves from any previous different host window
        // This can happen if a user mistakenly does (see #4295 for details):
        //  - N+0: DockBuilderAddNode(id, 0)    // missing ImGuiDockNodeFlags_DockSpace
        //  - N+1: NewFrame()                   // will create floating host window for that node
        //  - N+1: DockSpace(id)                // requalify node as dockspace, moving host window
        if (node.HostWindow != null && node.HostWindow != host_window && node.HostWindow.DockNodeAsHost == node)
            node.HostWindow.DockNodeAsHost = null;

        host_window.DockNodeAsHost = node;
        node.HostWindow = host_window;
    }
    // static void ImGui::DockNodeUpdate(ImGuiDockNode* node)
    static void DockNodeUpdate(ImGuiDockNode node)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(node.LastFrameActive != g.FrameCount);
        node.LastFrameAlive = g.FrameCount;
        node.IsBgDrawnThisFrame = false;

        node.CentralNode = node.OnlyNodeWithWindows = null;
        if (node.IsRootNode())
            DockNodeUpdateForRootNode(node);

        // Remove tab bar if not needed
        if (node.TabBar != null && node.IsNoTabBar())
            DockNodeRemoveTabBar(node);

        // Early out for hidden root dock nodes (when all DockId references are in inactive windows, or there is only 1 floating window holding on the DockId)
        bool want_to_hide_host_window = false;
        if (node.IsFloatingNode())
        {
            if (node.Windows.Size <= 1 && node.IsLeafNode())
                if (!g.IO.ConfigDockingAlwaysTabBar && (node.Windows.Size == 0 || !node.Windows[0].WindowClass.DockingAlwaysTabBar))
                    want_to_hide_host_window = true;
            if (node.CountNodeWithWindows == 0)
                want_to_hide_host_window = true;
        }
        if (want_to_hide_host_window)
        {
            if (node.Windows.Size == 1)
            {
                // Floating window pos/size is authoritative
                ImGuiWindow single_window = node.Windows[0];
                node.Pos = single_window.Pos;
                node.Size = single_window.SizeFull;
                node.AuthorityForPos = node.AuthorityForSize = node.AuthorityForViewport = (byte)ImGuiDataAuthority_Window;

                // Transfer focus immediately so when we revert to a regular window it is immediately selected
                if (node.HostWindow != null && g.NavWindow == node.HostWindow)
                    FocusWindow(single_window);
                if (node.HostWindow != null)
                {
                    IMGUI_DEBUG_LOG_VIEWPORT(g, "[viewport] Node {0:X8} transfer Viewport {1:X8}->{2:X8} to Window '{3}'\n", node.ID, node.HostWindow.Viewport.ID, single_window.ID, single_window.Name);
                    single_window.Viewport = node.HostWindow.Viewport;
                    single_window.ViewportId = node.HostWindow.ViewportId;
                    if (node.HostWindow.ViewportOwned)
                    {
                        single_window.Viewport.ID = single_window.ID;
                        single_window.Viewport.Window = single_window;
                        single_window.ViewportOwned = true;
                    }
                }
                node.RefViewportId = single_window.ViewportId;
            }

            DockNodeHideHostWindow(node);
            node.State = ImGuiDockNodeState_HostWindowHiddenBecauseSingleWindow;
            node.WantCloseAll = false;
            node.WantCloseTabId = 0;
            node.HasCloseButton = node.HasWindowMenuButton = false;
            node.LastFrameActive = g.FrameCount;

            if (node.WantMouseMove && node.Windows.Size == 1)
                DockNodeStartMouseMovingWindow(node, node.Windows[0]);
            return;
        }

        // In some circumstance we will defer creating the host window (so everything will be kept hidden),
        // while the expected visible window is resizing itself.
        // This is important for first-time (no ini settings restored) single window when io.ConfigDockingAlwaysTabBar is enabled,
        // otherwise the node ends up using the minimum window size. Effectively those windows will take an extra frame to show up:
        //   N+0: Begin(): window created (with no known size), node is created
        //   N+1: DockNodeUpdate(): node skip creating host window / Begin(): window size applied, not visible
        //   N+2: DockNodeUpdate(): node can create host window / Begin(): window becomes visible
        // We could remove this frame if we could reliably calculate the expected window size during node update, before the Begin() code.
        // It would require a generalization of CalcWindowExpectedSize(), probably extracting code away from Begin().
        // In reality it isn't very important as user quickly ends up with size data in .ini file.
        if (node.IsVisible && node.HostWindow == null && node.IsFloatingNode() && node.IsLeafNode())
        {
            Debug.Assert(node.Windows.Size > 0);
            ImGuiWindow ref_window = null;
            if (node.SelectedTabId != 0) // Note that we prune single-window-node settings on .ini loading, so this is generally 0 for them!
                ref_window = DockNodeFindWindowByID(node, node.SelectedTabId);
            if (ref_window == null)
                ref_window = node.Windows[0];
            if (ref_window.AutoFitFramesX > 0 || ref_window.AutoFitFramesY > 0)
            {
                node.State = ImGuiDockNodeState_HostWindowHiddenBecauseWindowsAreResizing;
                return;
            }
        }

        int node_flags = node.MergedFlags;

        // Decide if the node will have a close button and a window menu button
        node.HasWindowMenuButton = (node.Windows.Size > 0) && (node_flags & ImGuiDockNodeFlags_NoWindowMenuButton) == 0;
        node.HasCloseButton = false;
        for (int window_n = 0; window_n < node.Windows.Size; window_n++)
        {
            // FIXME-DOCK: Setting DockIsActive here means that for single active window in a leaf node, DockIsActive will be cleared until the next Begin() call.
            ImGuiWindow window = node.Windows[window_n];
            node.HasCloseButton |= window.HasCloseButton;
            window.DockIsActive = (node.Windows.Size > 1);
        }
        if ((node_flags & ImGuiDockNodeFlags_NoCloseButton) != 0 || !g.Style.DockingNodeHasCloseButton)
            node.HasCloseButton = false;

        // Bind or create host window
        ImGuiWindow host_window = null;
        bool beginned_into_host_window = false;
        if (node.IsDockSpace())
        {
            // [Explicit root dockspace node]
            Debug.Assert(node.HostWindow != null);
            host_window = node.HostWindow;
        }
        else
        {
            // [Automatic root or child nodes]
            if (node.IsRootNode() && node.IsVisible)
            {
                ImGuiWindow ref_window = (node.Windows.Size > 0) ? node.Windows[0] : null;

                // Sync Pos
                if (node.AuthorityForPos == (byte)ImGuiDataAuthority_Window && ref_window != null)
                    SetNextWindowPos(ref_window.Pos);
                else if (node.AuthorityForPos == (byte)ImGuiDataAuthority_DockNode)
                    SetNextWindowPos(node.Pos);

                // Sync Size
                if (node.AuthorityForSize == (byte)ImGuiDataAuthority_Window && ref_window != null)
                    SetNextWindowSize(ref_window.SizeFull);
                else if (node.AuthorityForSize == (byte)ImGuiDataAuthority_DockNode)
                    SetNextWindowSize(node.Size);

                // Sync Collapsed
                if (node.AuthorityForSize == (byte)ImGuiDataAuthority_Window && ref_window != null)
                    SetNextWindowCollapsed(ref_window.Collapsed);

                // Sync Viewport
                if (node.AuthorityForViewport == (byte)ImGuiDataAuthority_Window && ref_window != null)
                    SetNextWindowViewport(ref_window.ViewportId);
                else if (node.AuthorityForViewport == (byte)ImGuiDataAuthority_Window && node.RefViewportId != 0)
                    SetNextWindowViewport(node.RefViewportId);

                SetNextWindowClass(node.WindowClass);

                // Begin into the host window
                string window_label = DockNodeGetHostWindowTitle(node);
                ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_DockNodeHost;
                window_flags |= ImGuiWindowFlags_NoFocusOnAppearing;
                window_flags |= ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoCollapse;
                window_flags |= ImGuiWindowFlags_NoTitleBar;

                SetNextWindowBgAlpha(0.0f); // Don't set ImGuiWindowFlags_NoBackground because it disables borders
                PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0));
                Begin(window_label, window_flags);
                PopStyleVar();
                beginned_into_host_window = true;

                host_window = g.CurrentWindow;
                DockNodeSetupHostWindow(node, host_window);
                host_window.DC.CursorPos = host_window.Pos;
                node.Pos = host_window.Pos;
                node.Size = host_window.Size;

                // We set ImGuiWindowFlags_NoFocusOnAppearing because we don't want the host window to take full focus (e.g. steal NavWindow)
                // But we still it bring it to the front of display. There's no way to choose this precise behavior via window flags.
                // One simple case to ponder if: window A has a toggle to create windows B/C/D. Dock B/C/D together, clear the toggle and enable it again.
                // When reappearing B/C/D will request focus and be moved to the top of the display pile, but they are not linked to the dock host window
                // during the frame they appear. The dock host window would keep its old display order, and the sorting in EndFrame would move B/C/D back
                // after the dock host window, losing their top-most status.
                if (node.HostWindow.Appearing)
                    BringWindowToDisplayFront(node.HostWindow);

                node.AuthorityForPos = node.AuthorityForSize = node.AuthorityForViewport = (byte)ImGuiDataAuthority_Auto;
            }
            else if (node.ParentNode != null)
            {
                node.HostWindow = host_window = node.ParentNode.HostWindow;
                node.AuthorityForPos = node.AuthorityForSize = node.AuthorityForViewport = (byte)ImGuiDataAuthority_Auto;
            }
            if (node.WantMouseMove && node.HostWindow != null)
                DockNodeStartMouseMovingWindow(node, node.HostWindow);
        }
        node.RefViewportId = 0; // Clear when we have a host window

        // Update focused node (the one whose title bar is highlight) within a node tree
        if (node.IsSplitNode())
            Debug.Assert(node.TabBar == null);
        if (node.IsRootNode())
        {
            ImGuiWindow p_window = (g.NavWindow != null) ? g.NavWindow.RootWindow : null;
            while (p_window != null && p_window.DockNode != null)
            {
                ImGuiDockNode p_node = DockNodeGetRootNode(p_window.DockNode);
                if (p_node == node)
                {
                    node.LastFocusedNodeId = p_window.DockNode.ID; // Note: not using root node ID!
                    break;
                }
                p_window = (p_node.HostWindow != null) ? p_node.HostWindow.RootWindow : null;
            }
        }

        // Register a hit-test hole in the window unless we are currently dragging a window that is compatible with our dockspace
        ImGuiDockNode central_node = node.CentralNode;
        bool central_node_hole = node.IsRootNode() && host_window != null && (node_flags & ImGuiDockNodeFlags_PassthruCentralNode) != 0 && central_node != null && central_node.IsEmpty();
        bool central_node_hole_register_hit_test_hole = central_node_hole;
        if (central_node_hole)
        {
            ImGuiPayload payload = GetDragDropPayload();
            if (payload != null && payload.IsDataType(IMGUI_PAYLOAD_TYPE_WINDOW) && DockNodeIsDropAllowed(host_window, DockNodeGetWindowFromPayload(payload)))
                central_node_hole_register_hit_test_hole = false;
        }
        if (central_node_hole_register_hit_test_hole)
        {
            // We add a little padding to match the "resize from edges" behavior and allow grabbing the splitter easily.
            // (But we only add it if there's something else on the other side of the hole, otherwise for e.g. fullscreen
            // covering passthru node we'd have a gap on the edge not covered by the hole)
            Debug.Assert(node.IsDockSpace()); // We cannot pass this flag without the DockSpace() api. Testing this because we also setup the hole in host_window->ParentNode
            ImGuiDockNode root_node = DockNodeGetRootNode(central_node);
            ImRect root_rect = new ImRect(root_node.Pos, root_node.Pos + root_node.Size);
            ImRect hole_rect = new ImRect(central_node.Pos, central_node.Pos + central_node.Size);
            if (hole_rect.Min.x > root_rect.Min.x) { hole_rect.Min.x += g.WindowsBorderHoverPadding; }
            if (hole_rect.Max.x < root_rect.Max.x) { hole_rect.Max.x -= g.WindowsBorderHoverPadding; }
            if (hole_rect.Min.y > root_rect.Min.y) { hole_rect.Min.y += g.WindowsBorderHoverPadding; }
            if (hole_rect.Max.y < root_rect.Max.y) { hole_rect.Max.y -= g.WindowsBorderHoverPadding; }
            //GetForegroundDrawList().AddRect(hole_rect.Min, hole_rect.Max, IM_COL32(255, 0, 0, 255));
            if (central_node_hole && !hole_rect.IsInverted())
            {
                SetWindowHitTestHole(host_window, hole_rect.Min, hole_rect.Max - hole_rect.Min);
                if (host_window.ParentWindow != null)
                    SetWindowHitTestHole(host_window.ParentWindow, hole_rect.Min, hole_rect.Max - hole_rect.Min);
            }
        }

        // Update position/size, process and draw resizing splitters
        if (node.IsRootNode() && host_window != null)
        {
            DockNodeTreeUpdatePosSize(node, host_window.Pos, host_window.Size, null);
            PushStyleColor(ImGuiCol_Separator, g.Style.Colors[ImGuiCol_Border]);
            PushStyleColor(ImGuiCol_SeparatorActive, g.Style.Colors[ImGuiCol_ResizeGripActive]);
            PushStyleColor(ImGuiCol_SeparatorHovered, g.Style.Colors[ImGuiCol_ResizeGripHovered]);
            DockNodeTreeUpdateSplitter(node);
            PopStyleColor(3);
        }

        // Draw empty node background (currently can only be the Central Node)
        if (host_window != null && node.IsEmpty() && node.IsVisible)
        {
            host_window.DrawList._Splitter.SetCurrentChannel(host_window.DrawList, DOCKING_HOST_DRAW_CHANNEL_BG);
            node.LastBgColor = (node_flags & ImGuiDockNodeFlags_PassthruCentralNode) != 0 ? 0 : GetColorU32(ImGuiCol_DockingEmptyBg);
            if (node.LastBgColor != 0)
                host_window.DrawList.AddRectFilled(node.Pos, node.Pos + node.Size, node.LastBgColor);
            node.IsBgDrawnThisFrame = true;
        }

        // Draw whole dockspace background if ImGuiDockNodeFlags_PassthruCentralNode if set.
        // We need to draw a background at the root level if requested by ImGuiDockNodeFlags_PassthruCentralNode, but we will only know the correct pos/size
        // _after_ processing the resizing splitters. So we are using the DrawList channel splitting facility to submit drawing primitives out of order!
        bool render_dockspace_bg = node.IsRootNode() && host_window != null && (node_flags & ImGuiDockNodeFlags_PassthruCentralNode) != 0;
        if (render_dockspace_bg && node.IsVisible)
        {
            host_window.DrawList._Splitter.SetCurrentChannel(host_window.DrawList, DOCKING_HOST_DRAW_CHANNEL_BG);
            if (central_node_hole)
                RenderRectFilledWithHole(host_window.DrawList, node.Rect(), central_node.Rect(), GetColorU32(ImGuiCol_WindowBg), 0.0f);
            else
                host_window.DrawList.AddRectFilled(node.Pos, node.Pos + node.Size, GetColorU32(ImGuiCol_WindowBg), 0.0f);
        }

        // Draw and populate Tab Bar
        if (host_window != null)
            host_window.DrawList._Splitter.SetCurrentChannel(host_window.DrawList, DOCKING_HOST_DRAW_CHANNEL_FG);
        if (host_window != null && node.Windows.Size > 0)
        {
            DockNodeUpdateTabBar(node, host_window);
        }
        else
        {
            node.WantCloseAll = false;
            node.WantCloseTabId = 0;
            node.IsFocused = false;
        }
        if (node.TabBar != null && node.TabBar.SelectedTabId != 0)
            node.SelectedTabId = node.TabBar.SelectedTabId;
        else if (node.Windows.Size > 0)
            node.SelectedTabId = node.Windows[0].TabId;

        // Draw payload drop target
        if (host_window != null && node.IsVisible)
            if (node.IsRootNode() && (g.MovingWindow == null || g.MovingWindow.RootWindowDockTree != host_window))
                BeginDockableDragDropTarget(host_window);

        // We update this after DockNodeUpdateTabBar()
        node.LastFrameActive = g.FrameCount;

        // Recurse into children
        // FIXME-DOCK FIXME-OPT: Should not need to recurse into children
        if (host_window != null)
        {
            if (node.ChildNodes[0] != null)
                DockNodeUpdate(node.ChildNodes[0]);
            if (node.ChildNodes[1] != null)
                DockNodeUpdate(node.ChildNodes[1]);

            // Render outer borders last (after the tab bar)
            if (node.IsRootNode())
                RenderWindowOuterBorders(host_window);
        }

        // End host window
        if (beginned_into_host_window) //-V1020
            End();
    }

    // Compare TabItem nodes given the last known DockOrder (will persist in .ini file as hint), used to sort tabs when multiple tabs are added on the same frame.
    // static int IMGUI_CDECL TabItemComparerByDockOrder(const void* lhs, const void* rhs)
    static int TabItemComparerByDockOrder(ImGuiTabItem a, ImGuiTabItem b)
    {
        ImGuiWindow wa = a.Window;
        ImGuiWindow wb = b.Window;
        int d = ((wa.DockOrder == -1) ? int.MaxValue : wa.DockOrder) - ((wb.DockOrder == -1) ? int.MaxValue : wb.DockOrder);
        if (d != 0)
            return d;
        return (wa.BeginOrderWithinContext - wb.BeginOrderWithinContext);
    }

    // Default handler for g.DockNodeWindowMenuHandler(): display the list of windows for a given dock-node.
    // This is exceptionally stored in a function pointer to also user applications to tweak this menu (undocumented)
    // Custom overrides may want to decorate, group, sort entries.
    // Please note those are internal structures: if you copy this expect occasional breakage.
    // (if you don't need to modify the "Tabs.Size == 1" behavior/path it is recommend you call this function in your handler)
    // void ImGui::DockNodeWindowMenuHandler_Default(ImGuiContext* ctx, ImGuiDockNode* node, ImGuiTabBar* tab_bar)
    public static void DockNodeWindowMenuHandler_Default(ImGuiContext ctx, ImGuiDockNode node, ImGuiTabBar tab_bar)
    {
        if (tab_bar.Tabs.Size == 1)
        {
            // "Hide tab bar" option. Being one of our rare user-facing string we pull it from a table.
            if (MenuItem(LocalizeGetMsg(ImGuiLocKey_DockingHideTabBar), null, node.IsHiddenTabBar()))
                node.WantHiddenTabBarToggle = true;
        }
        else
        {
            // Display a selectable list of windows in this docking node
            for (int tab_n = 0; tab_n < tab_bar.Tabs.Size; tab_n++)
            {
                ImGuiTabItem tab = tab_bar.Tabs[tab_n];
                if ((tab.Flags & ImGuiTabItemFlags_Button) != 0)
                    continue;
                if (Selectable(TabBarGetTabName(tab_bar, tab), tab.ID == tab_bar.SelectedTabId))
                    TabBarQueueFocus(tab_bar, tab);
                SameLine();
                Text("   ");
            }
        }
    }

    // static bool IsDockNodeTitleBarHighlighted(ImGuiDockNode* node, ImGuiDockNode* root_node)
    static bool IsDockNodeTitleBarHighlighted(ImGuiDockNode node, ImGuiDockNode root_node)
    {
        // CTRL+Tab highlight (only highlighting leaf node, not whole hierarchy)
        ImGuiContext g = GImGui;
        if (g.NavWindowingTarget != null)
            return (g.NavWindowingTarget.DockNode == node);

        // FIXME-DOCKING: May want alternative to treat central node void differently? e.g. if (g.NavWindow == host_window)
        if (g.NavWindow != null && root_node.LastFocusedNodeId == node.ID)
        {
            // FIXME: This could all be backed in RootWindowForTitleBarHighlight? Probably need to reorganize for both dock nodes + other RootWindowForTitleBarHighlight users (not-node)
            ImGuiWindow parent_window = g.NavWindow.RootWindow;
            while ((parent_window.Flags & ImGuiWindowFlags_ChildMenu) != 0)
                parent_window = parent_window.ParentWindow.RootWindow;
            ImGuiDockNode start_parent_node = parent_window.DockNodeAsHost != null ? parent_window.DockNodeAsHost : parent_window.DockNode;
            for (ImGuiDockNode parent_node = start_parent_node; parent_node != null; parent_node = parent_node.HostWindow != null ? parent_node.HostWindow.RootWindow.DockNode : null)
                if ((parent_node = DockNodeGetRootNode(parent_node)) == root_node)
                    return true;
        }
        return false;
    }

    // static const ImGuiCol GWindowDockStyleColors[ImGuiWindowDockStyleCol_COUNT]
    static readonly int[] GWindowDockStyleColors = new int[ImGuiWindowDockStyleCol_COUNT]
    {
        ImGuiCol_Text, ImGuiCol_TabHovered, ImGuiCol_Tab, ImGuiCol_TabSelected, ImGuiCol_TabSelectedOverline, ImGuiCol_TabDimmed, ImGuiCol_TabDimmedSelected, ImGuiCol_TabDimmedSelectedOverline, ImGuiCol_UnsavedMarker,
    };

    // Submit the tab bar corresponding to a dock node and various housekeeping details.
    // static void ImGui::DockNodeCalcTabBarLayout(const ImGuiDockNode* node, ImRect* out_title_rect, ImRect* out_tab_bar_rect, ImVec2* out_window_menu_button_pos, ImVec2* out_close_button_pos)
    public static void DockNodeCalcTabBarLayout(ImGuiDockNode node, out ImRect out_title_rect, out ImRect out_tab_bar_rect, out ImVec2 out_window_menu_button_pos, out ImVec2 out_close_button_pos)
    {
        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;

        out_title_rect = default;
        out_tab_bar_rect = default;
        out_window_menu_button_pos = default;
        out_close_button_pos = default;

        ImRect r = new ImRect(node.Pos.x, node.Pos.y, node.Pos.x + node.Size.x, node.Pos.y + g.FontSize + g.Style.FramePadding.y * 2.0f);
        out_title_rect = r;

        r.Min.x += style.WindowBorderSize;
        r.Max.x -= style.WindowBorderSize;

        float button_sz = g.FontSize;
        r.Min.x += style.FramePadding.x;
        r.Max.x -= style.FramePadding.x;
        ImVec2 window_menu_button_pos = new ImVec2(r.Min.x, r.Min.y + style.FramePadding.y);
        if (node.HasCloseButton)
        {
            out_close_button_pos = new ImVec2(r.Max.x - button_sz, r.Min.y + style.FramePadding.y);
            r.Max.x -= button_sz + style.ItemInnerSpacing.x;
        }
        if (node.HasWindowMenuButton && style.WindowMenuButtonPosition == ImGuiDir_Left)
        {
            r.Min.x += button_sz + style.ItemInnerSpacing.x;
        }
        else if (node.HasWindowMenuButton && style.WindowMenuButtonPosition == ImGuiDir_Right)
        {
            window_menu_button_pos = new ImVec2(r.Max.x - button_sz, r.Min.y + style.FramePadding.y);
            r.Max.x -= button_sz + style.ItemInnerSpacing.x;
        }
        out_tab_bar_rect = r;
        out_window_menu_button_pos = window_menu_button_pos;
    }

    // static void ImGui::DockNodeUpdateTabBar(ImGuiDockNode* node, ImGuiWindow* host_window)
    static unsafe void DockNodeUpdateTabBar(ImGuiDockNode node, ImGuiWindow host_window)
    {
        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;

        bool node_was_active = (node.LastFrameActive + 1 == g.FrameCount);
        node.WantCloseAll = false;
        node.WantCloseTabId = 0;

        // Decide if we should use a focused title bar color
        bool is_focused = false;
        ImGuiDockNode root_node = DockNodeGetRootNode(node);
        if (IsDockNodeTitleBarHighlighted(node, root_node))
            is_focused = true;

        // Hidden tab bar will show a triangle on the upper-left (in Begin)
        if (node.IsHiddenTabBar() || node.IsNoTabBar())
        {
            node.VisibleWindow = (node.Windows.Size > 0) ? node.Windows[0] : null;
            node.IsFocused = is_focused;
            if (is_focused)
                node.LastFrameFocused = g.FrameCount;
            if (node.VisibleWindow != null)
            {
                // Notify root of visible window (used to display title in OS task bar)
                if (is_focused || root_node.VisibleWindow == null)
                    root_node.VisibleWindow = node.VisibleWindow;
                if (node.TabBar != null)
                    node.TabBar.VisibleTabId = node.VisibleWindow.TabId;
            }
            return;
        }

        // Move ourselves to the Menu layer (so we can be accessed by tapping Alt) + undo SkipItems flag in order to draw over the title bar even if the window is collapsed
        bool backup_skip_item = host_window.SkipItems;
        if (!node.IsDockSpace())
        {
            host_window.SkipItems = false;
            host_window.DC.NavLayerCurrent = ImGuiNavLayer_Menu;
        }

        // Use PushOverrideID() instead of PushID() to use the node id _without_ the host window ID.
        // This is to facilitate computing those ID from the outside, and will affect more or less only the ID of the collapse button, popup and tabs,
        // as docked windows themselves will override the stack with their own root ID.
        PushOverrideID(node.ID);
        ImGuiTabBar tab_bar = node.TabBar;
        bool tab_bar_is_recreated = (tab_bar == null); // Tab bar are automatically destroyed when a node gets hidden
        if (tab_bar == null)
        {
            DockNodeAddTabBar(node);
            tab_bar = node.TabBar;
        }

        ImGuiID focus_tab_id = 0;
        node.IsFocused = is_focused;

        int node_flags = node.MergedFlags;
        bool has_window_menu_button = (node_flags & ImGuiDockNodeFlags_NoWindowMenuButton) == 0 && (style.WindowMenuButtonPosition != ImGuiDir_None);

        // In a dock node, the Collapse Button turns into the Window Menu button.
        // FIXME-DOCK FIXME-OPT: Could we recycle popups id across multiple dock nodes?
        if (has_window_menu_button && IsPopupOpen("#WindowMenu"))
        {
            ImGuiID next_selected_tab_id = tab_bar.NextSelectedTabId;
            DockNodeWindowMenuUpdate(node, tab_bar);
            if (tab_bar.NextSelectedTabId != 0 && tab_bar.NextSelectedTabId != next_selected_tab_id)
                focus_tab_id = tab_bar.NextSelectedTabId;
            is_focused |= node.IsFocused;
        }

        // Layout
        ImRect title_bar_rect, tab_bar_rect;
        ImVec2 window_menu_button_pos;
        ImVec2 close_button_pos;
        DockNodeCalcTabBarLayout(node, out title_bar_rect, out tab_bar_rect, out window_menu_button_pos, out close_button_pos);

        // Submit new tabs, they will be added as Unsorted and sorted below based on relative DockOrder value.
        int tabs_count_old = tab_bar.Tabs.Size;
        for (int window_n = 0; window_n < node.Windows.Size; window_n++)
        {
            ImGuiWindow window = node.Windows[window_n];
            if (TabBarFindTabByID(tab_bar, window.TabId) == null)
                TabBarAddTab(tab_bar, ImGuiTabItemFlags_Unsorted, window);
        }

        // Title bar
        if (is_focused)
            node.LastFrameFocused = g.FrameCount;
        ImU32 title_bar_col = GetColorU32(host_window.Collapsed ? ImGuiCol_TitleBgCollapsed : is_focused ? ImGuiCol_TitleBgActive : ImGuiCol_TitleBg);
        ImDrawFlags rounding_flags = CalcRoundingFlagsForRectInRect(title_bar_rect, host_window.Rect(), g.Style.DockingSeparatorSize);
        host_window.DrawList.AddRectFilled(title_bar_rect.Min, title_bar_rect.Max, title_bar_col, host_window.WindowRounding, rounding_flags);

        // Docking/Collapse button
        if (has_window_menu_button)
        {
            if (CollapseButton(host_window.GetID("#COLLAPSE"), window_menu_button_pos, node)) // == DockNodeGetWindowMenuButtonId(node)
                OpenPopup("#WindowMenu");
            if (IsItemActive())
                focus_tab_id = tab_bar.SelectedTabId;
            if (IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_DelayNormal) && g.HoveredIdTimer > 0.5f)
                SetTooltip(LocalizeGetMsg(ImGuiLocKey_DockingDragToUndockOrMoveNode));
        }

        // If multiple tabs are appearing on the same frame, sort them based on their persistent DockOrder value
        int tabs_unsorted_start = tab_bar.Tabs.Size;
        for (int tab_n = tab_bar.Tabs.Size - 1; tab_n >= 0 && (tab_bar.Tabs[tab_n].Flags & ImGuiTabItemFlags_Unsorted) != 0; tab_n--)
        {
            // FIXME-DOCK: Consider only clearing the flag after the tab has been alive for a few consecutive frames, allowing late comers to not break sorting?
            tab_bar.Tabs[tab_n].Flags &= ~ImGuiTabItemFlags_Unsorted;
            tabs_unsorted_start = tab_n;
        }
        if (tab_bar.Tabs.Size > tabs_unsorted_start)
        {
            IMGUI_DEBUG_LOG_DOCKING(g, "[docking] In node 0x{0:X8}: {1} new appearing tabs:{2}\n", node.ID, tab_bar.Tabs.Size - tabs_unsorted_start, (tab_bar.Tabs.Size > tabs_unsorted_start + 1) ? " (will sort)" : "");
            for (int tab_n = tabs_unsorted_start; tab_n < tab_bar.Tabs.Size; tab_n++)
            {
                ImGuiTabItem tab = tab_bar.Tabs[tab_n];
                IMGUI_DEBUG_LOG_DOCKING(g, "[docking] - Tab 0x{0:X8} '{1}' Order {2}\n", tab.ID, TabBarGetTabName(tab_bar, tab), tab.Window != null ? tab.Window.DockOrder : -1);
            }
            IMGUI_DEBUG_LOG_DOCKING(g, "[docking] SelectedTabId = 0x{0:X8}, NavWindow->TabId = 0x{1:X8}\n", node.SelectedTabId, g.NavWindow != null ? g.NavWindow.TabId : -1);
            if (tab_bar.Tabs.Size > tabs_unsorted_start + 1)
                Array.Sort(tab_bar.Tabs.Data, tabs_unsorted_start, tab_bar.Tabs.Size - tabs_unsorted_start, Comparer<ImGuiTabItem>.Create(TabItemComparerByDockOrder));
        }

        // Apply NavWindow focus back to the tab bar
        if (g.NavWindow != null && g.NavWindow.RootWindow.DockNode == node)
            tab_bar.SelectedTabId = g.NavWindow.RootWindow.TabId;

        // Selected newly added tabs, or persistent tab ID if the tab bar was just recreated
        if (tab_bar_is_recreated && TabBarFindTabByID(tab_bar, node.SelectedTabId) != null)
            tab_bar.SelectedTabId = tab_bar.NextSelectedTabId = node.SelectedTabId;
        else if (tab_bar.Tabs.Size > tabs_count_old)
            tab_bar.SelectedTabId = tab_bar.NextSelectedTabId = tab_bar.Tabs.back().Window.TabId;

        // Begin tab bar
        ImGuiTabBarFlags tab_bar_flags = ImGuiTabBarFlags_Reorderable | ImGuiTabBarFlags_AutoSelectNewTabs; // | ImGuiTabBarFlags_NoTabListScrollingButtons);
        tab_bar_flags |= ImGuiTabBarFlags_SaveSettings | ImGuiTabBarFlags_DockNode;
        tab_bar_flags |= ImGuiTabBarFlags_FittingPolicyMixed; // Enforce default policy. Since 1.92.2 this is now reasonable. May expose later if needed. (#8800, #3421)
        tab_bar_flags |= ImGuiTabBarFlags_DrawSelectedOverline;
        if (!host_window.Collapsed && is_focused)
            tab_bar_flags |= ImGuiTabBarFlags_IsFocused;
        tab_bar.ID = node.ID;// GetID("#TabBar");
        tab_bar.SeparatorMinX = node.Pos.x + host_window.WindowBorderSize; // Separator cover the whole node width
        tab_bar.SeparatorMaxX = node.Pos.x + node.Size.x - host_window.WindowBorderSize;
        BeginTabBarEx(tab_bar, tab_bar_rect, tab_bar_flags);
        //host_window.DrawList.AddRect(tab_bar_rect.Min, tab_bar_rect.Max, IM_COL32(255,0,255,255));

        // Backup style colors
        ImVec4[] backup_style_cols = new ImVec4[ImGuiWindowDockStyleCol_COUNT];
        for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
            backup_style_cols[color_n] = g.Style.Colors[GWindowDockStyleColors[color_n]];

        // Submit actual tabs
        node.VisibleWindow = null;
        for (int window_n = 0; window_n < node.Windows.Size; window_n++)
        {
            ImGuiWindow window = node.Windows[window_n];
            if (window.LastFrameActive + 1 < g.FrameCount && node_was_active)
                continue; // FIXME: Not sure if that's still taken/useful, as windows are normally removed in DockNodeUpdateFlagsAndCollapse().

            ImGuiTabItemFlags tab_item_flags = 0;
            tab_item_flags |= window.WindowClass.TabItemFlagsOverrideSet;
            if ((window.Flags & ImGuiWindowFlags_UnsavedDocument) != 0)
                tab_item_flags |= ImGuiTabItemFlags_UnsavedDocument;
            if ((tab_bar.Flags & ImGuiTabBarFlags_NoCloseWithMiddleMouseButton) != 0)
                tab_item_flags |= ImGuiTabItemFlags_NoCloseWithMiddleMouseButton;

            // Apply stored style overrides for the window
            for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
                g.Style.Colors[GWindowDockStyleColors[color_n]] = ColorConvertU32ToFloat4(window.DockStyle.Colors[color_n]);

            // Note that TabItemEx() calls TabBarCalcTabID() so our tab item ID will ignore the current ID stack (rightly so)
            bool tab_open = true;
            bool* p_tab_open = window.HasCloseButton ? &tab_open : null;
            TabItemEx(tab_bar, window.Name, p_tab_open, tab_item_flags, window);
            if (!tab_open)
                node.WantCloseTabId = window.TabId;
            if (tab_bar.VisibleTabId == window.TabId)
                node.VisibleWindow = window;

            // Store last item data so it can be queried with IsItemXXX functions after the user Begin() call
            window.DC.DockTabItemStatusFlags = g.LastItemData.StatusFlags;
            window.DC.DockTabItemRect = g.LastItemData.Rect;

            // Update navigation ID on menu layer
            if (g.NavWindow != null && g.NavWindow.RootWindow == window && (window.DC.NavLayersActiveMask & (1 << ImGuiNavLayer_Menu)) == 0)
                host_window.NavLastIds[1] = window.TabId;
        }

        // Restore style colors
        for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
            g.Style.Colors[GWindowDockStyleColors[color_n]] = backup_style_cols[color_n];

        // Notify root of visible window (used to display title in OS task bar)
        if (node.VisibleWindow != null)
            if (is_focused || root_node.VisibleWindow == null)
                root_node.VisibleWindow = node.VisibleWindow;

        // Close button (after VisibleWindow was updated)
        // Note that VisibleWindow may have been overrided by CTRL+Tabbing, so VisibleWindow->TabId may be != from tab_bar->SelectedTabId
        bool close_button_is_enabled = node.HasCloseButton && node.VisibleWindow != null && node.VisibleWindow.HasCloseButton;
        bool close_button_is_visible = node.HasCloseButton;
        //const bool close_button_is_visible = close_button_is_enabled; // Most people would expect this behavior of not even showing the button (leaving a hole since we can't claim that space as other windows in the tba bar have one)
        if (close_button_is_visible)
        {
            if (!close_button_is_enabled)
            {
                PushItemFlag(ImGuiItemFlags_Disabled, true);
                PushStyleColor(ImGuiCol_Text, new ImVec4(style.Colors[ImGuiCol_Text].x, style.Colors[ImGuiCol_Text].y, style.Colors[ImGuiCol_Text].z, style.Colors[ImGuiCol_Text].w * 0.4f));
            }
            if (CloseButton(host_window.GetID("#CLOSE"), close_button_pos))
            {
                node.WantCloseAll = true;
                for (int n = 0; n < tab_bar.Tabs.Size; n++)
                    TabBarCloseTab(tab_bar, tab_bar.Tabs[n]);
            }
            //if (IsItemActive())
            //    focus_tab_id = tab_bar.SelectedTabId;
            if (!close_button_is_enabled)
            {
                PopStyleColor();
                PopItemFlag();
            }
        }

        // When clicking on the title bar outside of tabs, we still focus the selected tab for that node
        // FIXME: TabItems submitted earlier use AllowItemOverlap so we manually perform a more specific test for now (hovered || held) in order to not cover them.
        ImGuiID title_bar_id = host_window.GetID("#TITLEBAR");
        if (g.HoveredId == 0 || g.HoveredId == title_bar_id || g.ActiveId == title_bar_id)
        {
            // AllowOverlap mode required for appending into dock node tab bar,
            // otherwise dragging window will steal HoveredId and amended tabs cannot get them.
            bool held;
            KeepAliveID(title_bar_id);
            ButtonBehavior(title_bar_rect, title_bar_id, out _, out held, ImGuiButtonFlags_AllowOverlap);
            if (g.HoveredId == title_bar_id)
            {
                g.LastItemData.ID = title_bar_id;
            }
            if (held)
            {
                if (IsMouseClicked(0))
                    focus_tab_id = tab_bar.SelectedTabId;

                // Forward moving request to selected window
                ImGuiTabItem tab = TabBarFindTabByID(tab_bar, tab_bar.SelectedTabId);
                if (tab != null)
                    StartMouseMovingWindowOrNode(tab.Window != null ? tab.Window : node.HostWindow, node, false); // Undock from tab bar empty space
            }
        }

        // Forward focus from host node to selected window
        //if (is_focused && g.NavWindow == host_window && !g.NavWindowingTarget)
        //    focus_tab_id = tab_bar.SelectedTabId;

        // When clicked on a tab we requested focus to the docked child
        // This overrides the value set by "forward focus from host node to selected window".
        if (tab_bar.NextSelectedTabId != 0)
            focus_tab_id = tab_bar.NextSelectedTabId;

        // Apply navigation focus
        if (focus_tab_id != 0)
        {
            ImGuiTabItem tab = TabBarFindTabByID(tab_bar, focus_tab_id);
            if (tab != null && tab.Window != null)
            {
                FocusWindow(tab.Window);
                if (g.NavId == 0) // only init if FocusWindow() didn't restore anything.
                    NavInitWindow(tab.Window, false);
            }
        }

        EndTabBar();
        PopID();

        // Restore SkipItems flag
        if (!node.IsDockSpace())
        {
            host_window.DC.NavLayerCurrent = ImGuiNavLayer_Main;
            host_window.SkipItems = backup_skip_item;
        }
    }


    // Retrieve the drop rectangles for a given direction or for the center + perform hit testing.
    // bool ImGui::DockNodeCalcDropRectsAndTestMousePos(const ImRect& parent, ImGuiDir dir, ImRect& out_r, bool outer_docking, ImVec2* test_mouse_pos)
    static bool DockNodeCalcDropRectsAndTestMousePos(ImRect parent, int dir, ref ImRect out_r, bool outer_docking, ImVec2? test_mouse_pos)
    {
        ImGuiContext g = GImGui;

        float parent_smaller_axis = ImMath.ImMin(parent.GetWidth(), parent.GetHeight());
        float hs_for_central_nodes = ImMath.ImMin(g.FontSize * 1.5f, ImMath.ImMax(g.FontSize * 0.5f, parent_smaller_axis / 8.0f));
        float hs_w; // Half-size, longer axis
        float hs_h; // Half-size, smaller axis
        ImVec2 off; // Distance from edge or center
        if (outer_docking)
        {
            hs_w = ImMath.ImTrunc(hs_for_central_nodes * 1.50f);
            hs_h = ImMath.ImTrunc(hs_for_central_nodes * 0.80f);
            off = ImMath.ImTrunc(new ImVec2(parent.GetWidth() * 0.5f - hs_h, parent.GetHeight() * 0.5f - hs_h));
        }
        else
        {
            hs_w = ImMath.ImTrunc(hs_for_central_nodes);
            hs_h = ImMath.ImTrunc(hs_for_central_nodes * 0.90f);
            off = ImMath.ImTrunc(new ImVec2(hs_w * 2.40f, hs_w * 2.40f));
        }

        ImVec2 c = ImMath.ImTrunc(parent.GetCenter());
        if (dir == ImGuiDir_None) { out_r = new ImRect(c.x - hs_w, c.y - hs_w, c.x + hs_w, c.y + hs_w); }
        else if (dir == ImGuiDir_Up) { out_r = new ImRect(c.x - hs_w, c.y - off.y - hs_h, c.x + hs_w, c.y - off.y + hs_h); }
        else if (dir == ImGuiDir_Down) { out_r = new ImRect(c.x - hs_w, c.y + off.y - hs_h, c.x + hs_w, c.y + off.y + hs_h); }
        else if (dir == ImGuiDir_Left) { out_r = new ImRect(c.x - off.x - hs_h, c.y - hs_w, c.x - off.x + hs_h, c.y + hs_w); }
        else if (dir == ImGuiDir_Right) { out_r = new ImRect(c.x + off.x - hs_h, c.y - hs_w, c.x + off.x + hs_h, c.y + hs_w); }

        if (test_mouse_pos == null)
            return false;

        ImRect hit_r = out_r;
        if (!outer_docking)
        {
            // Custom hit testing for the 5-way selection, designed to reduce flickering when moving diagonally between sides
            hit_r.Expand(ImMath.ImTrunc(hs_w * 0.30f));
            ImVec2 mouse_delta = (test_mouse_pos.Value - c);
            float mouse_delta_len2 = ImUtil.ImLengthSqr(mouse_delta);
            float r_threshold_center = hs_w * 1.4f;
            float r_threshold_sides = hs_w * (1.4f + 1.2f);
            if (mouse_delta_len2 < r_threshold_center * r_threshold_center)
                return (dir == ImGuiDir_None);
            if (mouse_delta_len2 < r_threshold_sides * r_threshold_sides)
                return (dir == ImGetDirQuadrantFromDelta(mouse_delta.x, mouse_delta.y));
        }
        return hit_r.Contains(test_mouse_pos.Value);
    }

    // host_node may be NULL if the window doesn't have a DockNode already.
    // FIXME-DOCK: This is misnamed since it's also doing the filtering.
    // static void ImGui::DockNodePreviewDockSetup(ImGuiWindow* host_window, ImGuiDockNode* host_node, ImGuiWindow* payload_window, ImGuiDockNode* payload_node, ImGuiDockPreviewData* data, bool is_explicit_target, bool is_outer_docking)
    static void DockNodePreviewDockSetup(ImGuiWindow host_window, ImGuiDockNode host_node, ImGuiWindow payload_window, ImGuiDockNode payload_node, ImGuiDockPreviewData data, bool is_explicit_target, bool is_outer_docking)
    {
        ImGuiContext g = GImGui;

        // There is an edge case when docking into a dockspace which only has inactive nodes.
        // In this case DockNodeTreeFindNodeByPos() will have selected a leaf node which is inactive.
        // Because the inactive leaf node doesn't have proper pos/size yet, we'll use the root node as reference.
        if (payload_node == null)
            payload_node = payload_window.DockNodeAsHost;
        ImGuiDockNode ref_node_for_rect = (host_node != null && !host_node.IsVisible) ? DockNodeGetRootNode(host_node) : host_node;
        if (ref_node_for_rect != null)
            Debug.Assert(ref_node_for_rect.IsVisible == true);

        // Filter, figure out where we are allowed to dock
        int src_node_flags = payload_node != null ? payload_node.MergedFlags : payload_window.WindowClass.DockNodeFlagsOverrideSet;
        int dst_node_flags = host_node != null ? host_node.MergedFlags : host_window.WindowClass.DockNodeFlagsOverrideSet;
        data.IsCenterAvailable = true;
        if (is_outer_docking)
            data.IsCenterAvailable = false;
        else if (g.IO.ConfigDockingNoDockingOver)
            data.IsCenterAvailable = false;
        else if ((dst_node_flags & ImGuiDockNodeFlags_NoDockingOverMe) != 0)
            data.IsCenterAvailable = false;
        else if (host_node != null && (dst_node_flags & ImGuiDockNodeFlags_NoDockingOverCentralNode) != 0 && host_node.IsCentralNode())
            data.IsCenterAvailable = false;
        else if ((host_node == null || !host_node.IsEmpty()) && payload_node != null && payload_node.IsSplitNode() && (payload_node.OnlyNodeWithWindows == null)) // Is _visibly_ split?
            data.IsCenterAvailable = false;
        else if ((src_node_flags & ImGuiDockNodeFlags_NoDockingOverOther) != 0 && (host_node == null || !host_node.IsEmpty()))
            data.IsCenterAvailable = false;
        else if ((src_node_flags & ImGuiDockNodeFlags_NoDockingOverEmpty) != 0 && host_node != null && host_node.IsEmpty())
            data.IsCenterAvailable = false;

        data.IsSidesAvailable = true;
        if ((dst_node_flags & ImGuiDockNodeFlags_NoDockingSplit) != 0 || g.IO.ConfigDockingNoSplit)
            data.IsSidesAvailable = false;
        else if (!is_outer_docking && host_node != null && host_node.ParentNode == null && host_node.IsCentralNode())
            data.IsSidesAvailable = false;
        else if ((src_node_flags & ImGuiDockNodeFlags_NoDockingSplitOther) != 0)
            data.IsSidesAvailable = false;

        // Build a tentative future node (reuse same structure because it is practical. Shape will be readjusted when previewing a split)
        data.FutureNode.HasCloseButton = (host_node != null ? host_node.HasCloseButton : host_window.HasCloseButton) || (payload_window.HasCloseButton);
        data.FutureNode.HasWindowMenuButton = host_node != null ? true : ((host_window.Flags & ImGuiWindowFlags_NoCollapse) == 0);
        data.FutureNode.Pos = ref_node_for_rect != null ? ref_node_for_rect.Pos : host_window.Pos;
        data.FutureNode.Size = ref_node_for_rect != null ? ref_node_for_rect.Size : host_window.Size;

        // Calculate drop shapes geometry for allowed splitting directions
        Debug.Assert(ImGuiDir_None == -1);
        data.SplitNode = host_node;
        data.SplitDir = ImGuiDir_None;
        data.IsSplitDirExplicit = false;
        if (!host_window.Collapsed)
            for (int dir = ImGuiDir_None; dir < ImGuiDir_COUNT; dir++)
            {
                if (dir == ImGuiDir_None && !data.IsCenterAvailable)
                    continue;
                if (dir != ImGuiDir_None && !data.IsSidesAvailable)
                    continue;
                ImRect drop_r = data.DropRectsDraw[dir + 1];
                if (DockNodeCalcDropRectsAndTestMousePos(data.FutureNode.Rect(), dir, ref drop_r, is_outer_docking, g.IO.MousePos))
                {
                    data.DropRectsDraw[dir + 1] = drop_r;
                    data.SplitDir = dir;
                    data.IsSplitDirExplicit = true;
                }
                else
                {
                    data.DropRectsDraw[dir + 1] = drop_r;
                }
            }

        // When docking without holding Shift, we only allow and preview docking when hovering over a drop rect or over the title bar
        data.IsDropAllowed = (data.SplitDir != ImGuiDir_None) || (data.IsCenterAvailable);
        if (!is_explicit_target && !data.IsSplitDirExplicit && !g.IO.ConfigDockingWithShift)
            data.IsDropAllowed = false;

        // Calculate split area
        data.SplitRatio = 0.0f;
        if (data.SplitDir != ImGuiDir_None)
        {
            int split_dir = data.SplitDir;
            int split_axis = (split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Right) ? ImGuiAxis_X : ImGuiAxis_Y;
            ImVec2 pos_new = default;
            ImVec2 pos_old = data.FutureNode.Pos;
            ImVec2 size_new = default;
            ImVec2 size_old = data.FutureNode.Size;
            DockNodeCalcSplitRects(ref pos_old, ref size_old, ref pos_new, ref size_new, split_dir, payload_window.Size);

            // Calculate split ratio so we can pass it down the docking request
            float split_ratio = ImMath.ImSaturate(size_new[split_axis] / data.FutureNode.Size[split_axis]);
            data.FutureNode.Pos = pos_new;
            data.FutureNode.Size = size_new;
            data.SplitRatio = (split_dir == ImGuiDir_Right || split_dir == ImGuiDir_Down) ? (1.0f - split_ratio) : (split_ratio);
        }
    }

    // static void ImGui::DockNodePreviewDockRender(ImGuiWindow* host_window, ImGuiDockNode* host_node, ImGuiWindow* root_payload, const ImGuiDockPreviewData* data)
    static void DockNodePreviewDockRender(ImGuiWindow host_window, ImGuiDockNode host_node, ImGuiWindow root_payload, ImGuiDockPreviewData data)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.CurrentWindow == host_window);   // Because we rely on font size to calculate tab sizes

        // With this option, we only display the preview on the target viewport, and the payload viewport is made transparent.
        // To compensate for the single layer obstructed by the payload, we'll increase the alpha of the preview nodes.
        bool is_transparent_payload = g.IO.ConfigDockingTransparentPayload;

        // In case the two windows involved are on different viewports, we will draw the overlay on each of them.
        int overlay_draw_lists_count = 0;
        ImDrawList[] overlay_draw_lists = new ImDrawList[2];
        overlay_draw_lists[overlay_draw_lists_count++] = GetForegroundDrawList(host_window.Viewport);
        if (host_window.Viewport != root_payload.Viewport && !is_transparent_payload)
            overlay_draw_lists[overlay_draw_lists_count++] = GetForegroundDrawList(root_payload.Viewport);

        // Draw main preview rectangle
        ImU32 overlay_col_main = GetColorU32(ImGuiCol_DockingPreview, is_transparent_payload ? 0.60f : 0.40f);
        ImU32 overlay_col_drop = GetColorU32(ImGuiCol_DockingPreview, is_transparent_payload ? 0.90f : 0.70f);
        ImU32 overlay_col_drop_hovered = GetColorU32(ImGuiCol_DockingPreview, is_transparent_payload ? 1.20f : 1.00f);
        ImU32 overlay_col_lines = GetColorU32(ImGuiCol_NavWindowingHighlight, is_transparent_payload ? 0.80f : 0.60f);

        // Display area preview
        bool can_preview_tabs = (root_payload.DockNodeAsHost == null || root_payload.DockNodeAsHost.Windows.Size > 0);
        if (data.IsDropAllowed)
        {
            ImRect overlay_rect = data.FutureNode.Rect();
            if (data.SplitDir == ImGuiDir_None && can_preview_tabs)
                overlay_rect.Min.y += GetFrameHeight();
            if (data.SplitDir != ImGuiDir_None || data.IsCenterAvailable)
                for (int overlay_n = 0; overlay_n < overlay_draw_lists_count; overlay_n++)
                    overlay_draw_lists[overlay_n].AddRectFilled(overlay_rect.Min, overlay_rect.Max, overlay_col_main, host_window.WindowRounding, CalcRoundingFlagsForRectInRect(overlay_rect, host_window.Rect(), g.Style.DockingSeparatorSize));
        }

        // Display tab shape/label preview unless we are splitting node (it generally makes the situation harder to read)
        if (data.IsDropAllowed && can_preview_tabs && data.SplitDir == ImGuiDir_None && data.IsCenterAvailable)
        {
            // Compute target tab bar geometry so we can locate our preview tabs
            ImRect tab_bar_rect;
            DockNodeCalcTabBarLayout(data.FutureNode, out _, out tab_bar_rect, out _, out _);
            ImVec2 tab_pos = tab_bar_rect.Min;
            if (host_node != null && host_node.TabBar != null)
            {
                if (!host_node.IsHiddenTabBar() && !host_node.IsNoTabBar())
                    tab_pos.x += host_node.TabBar.WidthAllTabs + g.Style.ItemInnerSpacing.x; // We don't use OffsetNewTab because when using non-persistent-order tab bar it is incremented with each Tab submission.
                else
                    tab_pos.x += g.Style.ItemInnerSpacing.x + TabItemCalcSize(host_node.Windows[0]).x;
            }
            else if ((host_window.Flags & ImGuiWindowFlags_DockNodeHost) == 0)
            {
                tab_pos.x += g.Style.ItemInnerSpacing.x + TabItemCalcSize(host_window).x; // Account for slight offset which will be added when changing from title bar to tab bar
            }

            // Draw tab shape/label preview (payload may be a loose window or a host window carrying multiple tabbed windows)
            if (root_payload.DockNodeAsHost != null)
                Debug.Assert(root_payload.DockNodeAsHost.Windows.Size <= root_payload.DockNodeAsHost.TabBar.Tabs.Size);
            ImGuiTabBar tab_bar_with_payload = root_payload.DockNodeAsHost != null ? root_payload.DockNodeAsHost.TabBar : null;
            int payload_count = tab_bar_with_payload != null ? tab_bar_with_payload.Tabs.Size : 1;
            for (int payload_n = 0; payload_n < payload_count; payload_n++)
            {
                // DockNode's TabBar may have non-window Tabs manually appended by user
                ImGuiWindow payload_window = tab_bar_with_payload != null ? tab_bar_with_payload.Tabs[payload_n].Window : root_payload;
                if (tab_bar_with_payload != null && payload_window == null)
                    continue;
                if (!DockNodeIsDropAllowedOne(payload_window, host_window))
                    continue;

                // Calculate the tab bounding box for each payload window
                ImVec2 tab_size = TabItemCalcSize(payload_window);
                ImRect tab_bb = new ImRect(tab_pos.x, tab_pos.y, tab_pos.x + tab_size.x, tab_pos.y + tab_size.y);
                tab_pos.x += tab_size.x + g.Style.ItemInnerSpacing.x;
                ImU32 overlay_col_text = GetColorU32(payload_window.DockStyle.Colors[ImGuiWindowDockStyleCol_Text]);
                ImU32 overlay_col_tabs = GetColorU32(payload_window.DockStyle.Colors[ImGuiWindowDockStyleCol_TabSelected]);
                ImU32 overlay_col_unsaved_marker = GetColorU32(payload_window.DockStyle.Colors[ImGuiWindowDockStyleCol_UnsavedMarker]);
                PushStyleColor(ImGuiCol_Text, overlay_col_text);
                PushStyleColor(ImGuiCol_UnsavedMarker, overlay_col_unsaved_marker);
                for (int overlay_n = 0; overlay_n < overlay_draw_lists_count; overlay_n++)
                {
                    ImGuiTabItemFlags tab_flags = (payload_window.Flags & ImGuiWindowFlags_UnsavedDocument) != 0 ? ImGuiTabItemFlags_UnsavedDocument : 0;
                    if (!tab_bar_rect.Contains(tab_bb))
                        overlay_draw_lists[overlay_n].PushClipRect(tab_bar_rect.Min, tab_bar_rect.Max);
                    TabItemBackground(overlay_draw_lists[overlay_n], tab_bb, tab_flags, overlay_col_tabs);
                    TabItemLabelAndCloseButton(overlay_draw_lists[overlay_n], tab_bb, tab_flags, g.Style.FramePadding, payload_window.Name, 0, 0, false, out _, out _);
                    if (!tab_bar_rect.Contains(tab_bb))
                        overlay_draw_lists[overlay_n].PopClipRect();
                }
                PopStyleColor(2);
            }
        }

        // Display drop boxes
        float scale = g.Style._MainScale; // FIXME-DPI
        float overlay_rounding = ImMath.ImMax((float)(int)(3.0f * scale), g.Style.FrameRounding);
        for (int dir = ImGuiDir_None; dir < ImGuiDir_COUNT; dir++)
        {
            if (!data.DropRectsDraw[dir + 1].IsInverted())
            {
                ImRect draw_r = data.DropRectsDraw[dir + 1];
                ImRect draw_r_in = draw_r;
                draw_r_in.Expand((float)(int)(-2.0f * scale));
                ImU32 overlay_col = (data.SplitDir == dir && data.IsSplitDirExplicit) ? overlay_col_drop_hovered : overlay_col_drop;
                float thickness = (float)(int)(1.0f * scale);
                for (int overlay_n = 0; overlay_n < overlay_draw_lists_count; overlay_n++)
                {
                    ImVec2 center = ImMath.ImFloor(draw_r_in.GetCenter());
                    overlay_draw_lists[overlay_n].AddRectFilled(draw_r.Min, draw_r.Max, overlay_col, overlay_rounding);
                    overlay_draw_lists[overlay_n].AddRect(draw_r_in.Min, draw_r_in.Max, overlay_col_lines, overlay_rounding, thickness, ImDrawFlags_None);
                    if (dir == ImGuiDir_Left || dir == ImGuiDir_Right)
                        overlay_draw_lists[overlay_n].AddLineV(center.x, draw_r_in.Min.y, draw_r_in.Max.y, overlay_col_lines, thickness);
                    if (dir == ImGuiDir_Up || dir == ImGuiDir_Down)
                        overlay_draw_lists[overlay_n].AddLineH(draw_r_in.Min.x, draw_r_in.Max.x, center.y, overlay_col_lines, thickness);
                }
            }

            // Stop after ImGuiDir_None
            if ((host_node != null && (host_node.MergedFlags & ImGuiDockNodeFlags_NoDockingSplit) != 0) || g.IO.ConfigDockingNoSplit)
                return;
        }
    }

    //-----------------------------------------------------------------------------
    // Docking: ImGuiDockNode Tree manipulation functions
    //-----------------------------------------------------------------------------

    // void ImGui::DockNodeTreeSplit(ImGuiContext* ctx, ImGuiDockNode* parent_node, ImGuiAxis split_axis, int split_inheritor_child_idx, float split_ratio, ImGuiDockNode* new_node)
    static void DockNodeTreeSplit(ImGuiContext ctx, ImGuiDockNode parent_node, int split_axis, int split_inheritor_child_idx, float split_ratio, ImGuiDockNode new_node)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(split_axis != ImGuiAxis_None);

        ImGuiDockNode child_0 = (new_node != null && split_inheritor_child_idx != 0) ? new_node : DockContextAddNode(ctx, 0);
        child_0.ParentNode = parent_node;

        ImGuiDockNode child_1 = (new_node != null && split_inheritor_child_idx != 1) ? new_node : DockContextAddNode(ctx, 0);
        child_1.ParentNode = parent_node;

        ImGuiDockNode child_inheritor = (split_inheritor_child_idx == 0) ? child_0 : child_1;
        DockNodeMoveChildNodes(child_inheritor, parent_node);
        parent_node.ChildNodes[0] = child_0;
        parent_node.ChildNodes[1] = child_1;
        parent_node.ChildNodes[split_inheritor_child_idx].VisibleWindow = parent_node.VisibleWindow;
        parent_node.SplitAxis = split_axis;
        parent_node.VisibleWindow = null;
        parent_node.AuthorityForPos = parent_node.AuthorityForSize = (byte)ImGuiDataAuthority_DockNode;

        float size_avail = (parent_node.Size[split_axis] - g.Style.DockingSeparatorSize);
        size_avail = ImMath.ImMax(size_avail, g.Style.WindowMinSize[split_axis] * 2.0f);
        Debug.Assert(size_avail > 0.0f, "If you created a node manually with DockBuilderAddNode(), you need to also call DockBuilderSetNodeSize() before splitting.");
        child_0.SizeRef = child_1.SizeRef = parent_node.Size;
        child_0.SizeRef[split_axis] = ImMath.ImTrunc(size_avail * split_ratio);
        child_1.SizeRef[split_axis] = ImMath.ImTrunc(size_avail - child_0.SizeRef[split_axis]);

        DockNodeMoveWindows(parent_node.ChildNodes[split_inheritor_child_idx], parent_node);
        DockSettingsRenameNodeReferences(parent_node.ID, parent_node.ChildNodes[split_inheritor_child_idx].ID);
        DockNodeUpdateHasCentralNodeChild(DockNodeGetRootNode(parent_node));
        DockNodeTreeUpdatePosSize(parent_node, parent_node.Pos, parent_node.Size, null);

        // Flags transfer (e.g. this is where we transfer the ImGuiDockNodeFlags_CentralNode property)
        child_0.SharedFlags = parent_node.SharedFlags & ImGuiDockNodeFlags_SharedFlagsInheritMask_;
        child_1.SharedFlags = parent_node.SharedFlags & ImGuiDockNodeFlags_SharedFlagsInheritMask_;
        child_inheritor.LocalFlags = parent_node.LocalFlags & ImGuiDockNodeFlags_LocalFlagsTransferMask_;
        parent_node.LocalFlags &= ~ImGuiDockNodeFlags_LocalFlagsTransferMask_;
        child_0.UpdateMergedFlags();
        child_1.UpdateMergedFlags();
        parent_node.UpdateMergedFlags();
        if (child_inheritor.IsCentralNode())
            DockNodeGetRootNode(parent_node).CentralNode = child_inheritor;
    }

    // void ImGui::DockNodeTreeMerge(ImGuiContext* ctx, ImGuiDockNode* parent_node, ImGuiDockNode* merge_lead_child)
    static void DockNodeTreeMerge(ImGuiContext ctx, ImGuiDockNode parent_node, ImGuiDockNode merge_lead_child)
    {
        // When called from DockContextProcessUndockNode() it is possible that one of the child is NULL.
        ImGuiContext g = GImGui;
        ImGuiDockNode child_0 = parent_node.ChildNodes[0];
        ImGuiDockNode child_1 = parent_node.ChildNodes[1];
        Debug.Assert(child_0 != null || child_1 != null);
        Debug.Assert(merge_lead_child == child_0 || merge_lead_child == child_1);
        if ((child_0 != null && child_0.Windows.Size > 0) || (child_1 != null && child_1.Windows.Size > 0))
        {
            Debug.Assert(parent_node.TabBar == null);
            Debug.Assert(parent_node.Windows.Size == 0);
        }
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockNodeTreeMerge: 0x{0:X8} + 0x{1:X8} back into parent 0x{2:X8}\n", child_0 != null ? child_0.ID : 0, child_1 != null ? child_1.ID : 0, parent_node.ID);

        ImVec2 backup_last_explicit_size = parent_node.SizeRef;
        DockNodeMoveChildNodes(parent_node, merge_lead_child);
        if (child_0 != null)
        {
            DockNodeMoveWindows(parent_node, child_0); // Generally only 1 of the 2 child node will have windows
            DockSettingsRenameNodeReferences(child_0.ID, parent_node.ID);
        }
        if (child_1 != null)
        {
            DockNodeMoveWindows(parent_node, child_1);
            DockSettingsRenameNodeReferences(child_1.ID, parent_node.ID);
        }
        DockNodeApplyPosSizeToWindows(parent_node);
        parent_node.AuthorityForPos = parent_node.AuthorityForSize = parent_node.AuthorityForViewport = (byte)ImGuiDataAuthority_Auto;
        parent_node.VisibleWindow = merge_lead_child.VisibleWindow;
        parent_node.SizeRef = backup_last_explicit_size;

        // Flags transfer
        parent_node.LocalFlags &= ~ImGuiDockNodeFlags_LocalFlagsTransferMask_; // Preserve Dockspace flag
        parent_node.LocalFlags |= (child_0 != null ? child_0.LocalFlags : 0) & ImGuiDockNodeFlags_LocalFlagsTransferMask_;
        parent_node.LocalFlags |= (child_1 != null ? child_1.LocalFlags : 0) & ImGuiDockNodeFlags_LocalFlagsTransferMask_;
        parent_node.LocalFlagsInWindows = (child_0 != null ? child_0.LocalFlagsInWindows : 0) | (child_1 != null ? child_1.LocalFlagsInWindows : 0); // FIXME: Would be more consistent to update from actual windows
        parent_node.UpdateMergedFlags();

        if (child_0 != null)
            DockContextDeleteNode(ctx, child_0);
        if (child_1 != null)
            DockContextDeleteNode(ctx, child_1);
    }

    // Update Pos/Size for a node hierarchy (don't affect child Windows yet)
    // (Depth-first, Pre-Order)
    // void ImGui::DockNodeTreeUpdatePosSize(ImGuiDockNode* node, ImVec2 pos, ImVec2 size, ImGuiDockNode* only_write_to_single_node)
    static void DockNodeTreeUpdatePosSize(ImGuiDockNode node, ImVec2 pos, ImVec2 size, ImGuiDockNode only_write_to_single_node)
    {
        // During the regular dock node update we write to all nodes.
        // 'only_write_to_single_node' is only set when turning a node visible mid-frame and we need its size right-away.
        ImGuiContext g = GImGui;
        bool write_to_node = only_write_to_single_node == null || only_write_to_single_node == node;
        if (write_to_node)
        {
            node.Pos = pos;
            node.Size = size;
        }

        if (node.IsLeafNode())
            return;

        ImGuiDockNode child_0 = node.ChildNodes[0];
        ImGuiDockNode child_1 = node.ChildNodes[1];
        ImVec2 child_0_pos = pos, child_1_pos = pos;
        ImVec2 child_0_size = size, child_1_size = size;

        bool child_0_is_toward_single_node = (only_write_to_single_node != null && DockNodeIsInHierarchyOf(only_write_to_single_node, child_0));
        bool child_1_is_toward_single_node = (only_write_to_single_node != null && DockNodeIsInHierarchyOf(only_write_to_single_node, child_1));
        bool child_0_is_or_will_be_visible = child_0.IsVisible || child_0_is_toward_single_node;
        bool child_1_is_or_will_be_visible = child_1.IsVisible || child_1_is_toward_single_node;

        if (child_0_is_or_will_be_visible && child_1_is_or_will_be_visible)
        {
            float spacing = g.Style.DockingSeparatorSize;
            int axis = node.SplitAxis;
            float size_avail = ImMath.ImMax(size[axis] - spacing, 0.0f);

            // Size allocation policy
            // 1) The first 0..WindowMinSize[axis]*2 are allocated evenly to both windows.
            float size_min_each = ImMath.ImTrunc(ImMath.ImMin(size_avail, g.Style.WindowMinSize[axis] * 2.0f) * 0.5f);

            // FIXME: Blocks 2) and 3) are essentially doing nearly the same thing.
            // Difference are: write-back to SizeRef; application of a minimum size; rounding before ImTrunc()
            // Clarify and rework differences between Size & SizeRef and purpose of WantLockSizeOnce

            // 2) Process locked absolute size (during a splitter resize we preserve the child of nodes not touching the splitter edge)
            if (child_0.WantLockSizeOnce && !child_1.WantLockSizeOnce)
            {
                child_0_size[axis] = child_0.SizeRef[axis] = ImMath.ImMin(size_avail - 1.0f, child_0.Size[axis]);
                child_1_size[axis] = child_1.SizeRef[axis] = (size_avail - child_0_size[axis]);
                Debug.Assert(child_0.SizeRef[axis] > 0.0f && child_1.SizeRef[axis] > 0.0f);
            }
            else if (child_1.WantLockSizeOnce && !child_0.WantLockSizeOnce)
            {
                child_1_size[axis] = child_1.SizeRef[axis] = ImMath.ImMin(size_avail - 1.0f, child_1.Size[axis]);
                child_0_size[axis] = child_0.SizeRef[axis] = (size_avail - child_1_size[axis]);
                Debug.Assert(child_0.SizeRef[axis] > 0.0f && child_1.SizeRef[axis] > 0.0f);
            }
            else if (child_0.WantLockSizeOnce && child_1.WantLockSizeOnce)
            {
                // FIXME-DOCK: We cannot honor the requested size, so apply ratio.
                // Currently this path will only be taken if code programmatically sets WantLockSizeOnce
                float split_ratio = child_0_size[axis] / (child_0_size[axis] + child_1_size[axis]);
                child_0_size[axis] = child_0.SizeRef[axis] = ImMath.ImTrunc(size_avail * split_ratio);
                child_1_size[axis] = child_1.SizeRef[axis] = (size_avail - child_0_size[axis]);
                Debug.Assert(child_0.SizeRef[axis] > 0.0f && child_1.SizeRef[axis] > 0.0f);
            }

            // 3) If one window is the central node (~ use remaining space, should be made explicit!), use explicit size from the other, and remainder for the central node
            else if (child_0.SizeRef[axis] != 0.0f && child_1.HasCentralNodeChild)
            {
                child_0_size[axis] = ImMath.ImMin(size_avail - size_min_each, child_0.SizeRef[axis]);
                child_1_size[axis] = (size_avail - child_0_size[axis]);
            }
            else if (child_1.SizeRef[axis] != 0.0f && child_0.HasCentralNodeChild)
            {
                child_1_size[axis] = ImMath.ImMin(size_avail - size_min_each, child_1.SizeRef[axis]);
                child_0_size[axis] = (size_avail - child_1_size[axis]);
            }
            else
            {
                // 4) Otherwise distribute according to the relative ratio of each SizeRef value
                float split_ratio = child_0.SizeRef[axis] / (child_0.SizeRef[axis] + child_1.SizeRef[axis]);
                child_0_size[axis] = ImMath.ImMax(size_min_each, ImMath.ImTrunc(size_avail * split_ratio + 0.5f));
                child_1_size[axis] = (size_avail - child_0_size[axis]);
            }

            child_1_pos[axis] += spacing + child_0_size[axis];
        }

        if (only_write_to_single_node == null)
            child_0.WantLockSizeOnce = child_1.WantLockSizeOnce = false;

        bool child_0_recurse = only_write_to_single_node != null ? child_0_is_toward_single_node : child_0.IsVisible;
        bool child_1_recurse = only_write_to_single_node != null ? child_1_is_toward_single_node : child_1.IsVisible;
        if (child_0_recurse)
            DockNodeTreeUpdatePosSize(child_0, child_0_pos, child_0_size, null);
        if (child_1_recurse)
            DockNodeTreeUpdatePosSize(child_1, child_1_pos, child_1_size, null);
    }

    // static void DockNodeTreeUpdateSplitterFindTouchingNode(ImGuiDockNode* node, ImGuiAxis axis, int side, ImVector<ImGuiDockNode*>* touching_nodes)
    static void DockNodeTreeUpdateSplitterFindTouchingNode(ImGuiDockNode node, int axis, int side, ImVector<ImGuiDockNode> touching_nodes)
    {
        if (node.IsLeafNode())
        {
            touching_nodes.push_back(node);
            return;
        }
        if (node.ChildNodes[0].IsVisible)
            if (node.SplitAxis != axis || side == 0 || !node.ChildNodes[1].IsVisible)
                DockNodeTreeUpdateSplitterFindTouchingNode(node.ChildNodes[0], axis, side, touching_nodes);
        if (node.ChildNodes[1].IsVisible)
            if (node.SplitAxis != axis || side == 1 || !node.ChildNodes[0].IsVisible)
                DockNodeTreeUpdateSplitterFindTouchingNode(node.ChildNodes[1], axis, side, touching_nodes);
    }

    // (Depth-First, Pre-Order)
    // void ImGui::DockNodeTreeUpdateSplitter(ImGuiDockNode* node)
    public static void DockNodeTreeUpdateSplitter(ImGuiDockNode node)
    {
        if (node.IsLeafNode())
            return;

        ImGuiContext g = GImGui;

        ImGuiDockNode child_0 = node.ChildNodes[0];
        ImGuiDockNode child_1 = node.ChildNodes[1];
        if (child_0.IsVisible && child_1.IsVisible)
        {
            // Bounding box of the splitter cover the space between both nodes (w = Spacing, h = Size[xy^1] for when splitting horizontally)
            int axis = node.SplitAxis;
            Debug.Assert(axis != ImGuiAxis_None);
            ImRect bb = new ImRect();
            bb.Min = child_0.Pos;
            bb.Max = child_1.Pos;
            bb.Min[axis] += child_0.Size[axis];
            bb.Max[axis ^ 1] += child_1.Size[axis ^ 1];
            //if (g.IO.KeyCtrl) GetForegroundDrawList(g.CurrentWindow.Viewport).AddRect(bb.Min, bb.Max, IM_COL32(255,0,255,255));

            int merged_flags = child_0.MergedFlags | child_1.MergedFlags; // Merged flags for BOTH childs
            int no_resize_axis_flag = (axis == ImGuiAxis_X) ? ImGuiDockNodeFlags_NoResizeX : ImGuiDockNodeFlags_NoResizeY;
            if ((merged_flags & ImGuiDockNodeFlags_NoResize) != 0 || (merged_flags & no_resize_axis_flag) != 0)
            {
                ImGuiWindow window = g.CurrentWindow;
                window.DrawList.AddRectFilled(bb.Min, bb.Max, GetColorU32(ImGuiCol_Separator), g.Style.FrameRounding);
            }
            else
            {
                //bb.Min[axis] += 1; // Display a little inward so highlight doesn't connect with nearby tabs on the neighbor node.
                //bb.Max[axis] -= 1;
                PushID(node.ID);

                // Find resizing limits by gathering list of nodes that are touching the splitter line.
                ImVector<ImGuiDockNode> touching_nodes_0 = new ImVector<ImGuiDockNode>();
                ImVector<ImGuiDockNode> touching_nodes_1 = new ImVector<ImGuiDockNode>();
                float min_size = g.Style.WindowMinSize[axis];
                float[] resize_limits = new float[2];
                resize_limits[0] = node.ChildNodes[0].Pos[axis] + min_size;
                resize_limits[1] = node.ChildNodes[1].Pos[axis] + node.ChildNodes[1].Size[axis] - min_size;

                ImGuiID splitter_id = GetID("##Splitter");
                if (g.ActiveId == splitter_id) // Only process when splitter is active
                {
                    DockNodeTreeUpdateSplitterFindTouchingNode(child_0, axis, 1, touching_nodes_0);
                    DockNodeTreeUpdateSplitterFindTouchingNode(child_1, axis, 0, touching_nodes_1);
                    for (int touching_node_n = 0; touching_node_n < touching_nodes_0.Size; touching_node_n++)
                        resize_limits[0] = ImMath.ImMax(resize_limits[0], touching_nodes_0[touching_node_n].Rect().Min[axis] + min_size);
                    for (int touching_node_n = 0; touching_node_n < touching_nodes_1.Size; touching_node_n++)
                        resize_limits[1] = ImMath.ImMin(resize_limits[1], touching_nodes_1[touching_node_n].Rect().Max[axis] - min_size);
                }

                // Use a short delay before highlighting the splitter (and changing the mouse cursor) in order for regular mouse movement to not highlight many splitters
                float cur_size_0 = child_0.Size[axis];
                float cur_size_1 = child_1.Size[axis];
                float min_size_0 = resize_limits[0] - child_0.Pos[axis];
                float min_size_1 = child_1.Pos[axis] + child_1.Size[axis] - resize_limits[1];
                ImU32 bg_col = GetColorU32(ImGuiCol_WindowBg);
                if (SplitterBehavior(bb, GetID("##Splitter"), (ImGuiAxis)axis, ref cur_size_0, ref cur_size_1, min_size_0, min_size_1, g.WindowsBorderHoverPadding, WINDOWS_RESIZE_FROM_EDGES_FEEDBACK_TIMER, bg_col))
                {
                    if (touching_nodes_0.Size > 0 && touching_nodes_1.Size > 0)
                    {
                        child_0.Size[axis] = child_0.SizeRef[axis] = cur_size_0;
                        child_1.Pos[axis] -= cur_size_1 - child_1.Size[axis];
                        child_1.Size[axis] = child_1.SizeRef[axis] = cur_size_1;

                        // Lock the size of every node that is a sibling of the node we are touching
                        // This might be less desirable if we can merge sibling of a same axis into the same parental level.
                        for (int side_n = 0; side_n < 2; side_n++)
                        {
                            ImVector<ImGuiDockNode> touching_nodes_side = side_n == 0 ? touching_nodes_0 : touching_nodes_1;
                            for (int touching_node_n = 0; touching_node_n < touching_nodes_side.Size; touching_node_n++)
                            {
                                ImGuiDockNode touching_node = touching_nodes_side[touching_node_n];
                                while (touching_node.ParentNode != node)
                                {
                                    if (touching_node.ParentNode.SplitAxis == axis)
                                    {
                                        // Mark other node so its size will be preserved during the upcoming call to DockNodeTreeUpdatePosSize().
                                        ImGuiDockNode node_to_preserve = touching_node.ParentNode.ChildNodes[side_n];
                                        node_to_preserve.WantLockSizeOnce = true;
                                    }
                                    touching_node = touching_node.ParentNode;
                                }
                            }
                        }

                        DockNodeTreeUpdatePosSize(child_0, child_0.Pos, child_0.Size, null);
                        DockNodeTreeUpdatePosSize(child_1, child_1.Pos, child_1.Size, null);
                        MarkIniSettingsDirty();
                    }
                }
                PopID();
            }
        }

        if (child_0.IsVisible)
            DockNodeTreeUpdateSplitter(child_0);
        if (child_1.IsVisible)
            DockNodeTreeUpdateSplitter(child_1);
    }

    // ImGuiDockNode* ImGui::DockNodeTreeFindFallbackLeafNode(ImGuiDockNode* node)
    public static ImGuiDockNode DockNodeTreeFindFallbackLeafNode(ImGuiDockNode node)
    {
        if (node.IsLeafNode())
            return node;
        ImGuiDockNode leaf_node = DockNodeTreeFindFallbackLeafNode(node.ChildNodes[0]);
        if (leaf_node != null)
            return leaf_node;
        leaf_node = DockNodeTreeFindFallbackLeafNode(node.ChildNodes[1]);
        if (leaf_node != null)
            return leaf_node;
        return null;
    }

    // ImGuiDockNode* ImGui::DockNodeTreeFindVisibleNodeByPos(ImGuiDockNode* node, ImVec2 pos)
    public static ImGuiDockNode DockNodeTreeFindVisibleNodeByPos(ImGuiDockNode node, ImVec2 pos)
    {
        if (!node.IsVisible)
            return null;

        float dock_spacing = 0.0f;// g.Style.ItemInnerSpacing.x; // FIXME: Relation to DOCKING_SPLITTER_SIZE?
        ImRect r = new ImRect(node.Pos, node.Pos + node.Size);
        r.Expand(dock_spacing * 0.5f);
        bool inside = r.Contains(pos);
        if (!inside)
            return null;

        if (node.IsLeafNode())
            return node;
        ImGuiDockNode hovered_node = DockNodeTreeFindVisibleNodeByPos(node.ChildNodes[0], pos);
        if (hovered_node != null)
            return hovered_node;
        hovered_node = DockNodeTreeFindVisibleNodeByPos(node.ChildNodes[1], pos);
        if (hovered_node != null)
            return hovered_node;

        // This means we are hovering over the splitter/spacing of a parent node
        return node;
    }

    //-----------------------------------------------------------------------------
    // Docking: Public Functions (SetWindowDock, DockSpace, DockSpaceOverViewport)
    //-----------------------------------------------------------------------------

    // [Internal] Called via SetNextWindowDockID()
    // void ImGui::SetWindowDock(ImGuiWindow* window, ImGuiID dock_id, ImGuiCond cond)
    public static void SetWindowDock(ImGuiWindow window, ImGuiID dock_id, ImGuiCond cond)
    {
        // Test condition (NB: bit 0 is always true) and clear flags for next time
        if (cond != 0 && (window.SetWindowDockAllowFlags & cond) == 0)
            return;
        window.SetWindowDockAllowFlags &= (byte)(~(ImGuiCond_Once | ImGuiCond_FirstUseEver | ImGuiCond_Appearing) & 0xFF);

        if (window.DockId == dock_id)
            return;

        // If the user attempt to set a dock id that is a split node, we'll dig within to find a suitable docking spot
        ImGuiContext g = GImGui;
        ImGuiDockNode new_node = DockContextFindNodeByID(g, dock_id);
        if (new_node != null)
            if (new_node.IsSplitNode())
            {
                // Policy: Find central node or latest focused node. We first move back to our root node.
                new_node = DockNodeGetRootNode(new_node);
                if (new_node.CentralNode != null)
                {
                    Debug.Assert(new_node.CentralNode.IsCentralNode());
                    dock_id = new_node.CentralNode.ID;
                }
                else
                {
                    dock_id = new_node.LastFocusedNodeId;
                }
            }

        if (window.DockId == dock_id)
            return;

        if (window.DockNode != null)
            DockNodeRemoveWindow(window.DockNode, window, 0);
        window.DockId = dock_id;
    }

    // Create an explicit dockspace node within an existing window. Also expose dock node flags and creates a CentralNode by default.
    // The Central Node is always displayed even when empty and shrink/extend according to the requested size of its neighbors.
    // DockSpace() needs to be submitted _before_ any window they can host. If you use a dockspace, submit it early in your app.
    // When ImGuiDockNodeFlags_KeepAliveOnly is set, nothing is submitted in the current window (function may be called from any location).
    // ImGuiID ImGui::DockSpace(ImGuiID dockspace_id, const ImVec2& size_arg, ImGuiDockNodeFlags flags, const ImGuiWindowClass* window_class)
    public static ImGuiID DockSpace(ImGuiID dockspace_id, ImVec2? size_arg = null, ImGuiDockNodeFlags flags = 0, ImGuiWindowClass window_class = null)
    {
        ImVec2 size_val = size_arg ?? new ImVec2(0, 0);
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindowRead();
        if ((g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable) == 0)
            return 0;

        // Early out if parent window is hidden/collapsed
        // This is faster but also DockNodeUpdateTabBar() relies on TabBarLayout() running (which won't if SkipItems=true) to set NextSelectedTabId = 0). See #2960.
        // If for whichever reason this is causing problem we would need to ensure that DockNodeUpdateTabBar() ends up clearing NextSelectedTabId even if SkipItems=true.
        if (window.SkipItems)
            flags |= ImGuiDockNodeFlags_KeepAliveOnly;
        if ((flags & ImGuiDockNodeFlags_KeepAliveOnly) == 0)
            window = GetCurrentWindow(); // call to set window->WriteAccessed = true;

        Debug.Assert((flags & ImGuiDockNodeFlags_DockSpace) == 0, "Flag is automatically set by DockSpace() as LocalFlags, not SharedFlags!");
        Debug.Assert((flags & ImGuiDockNodeFlags_CentralNode) == 0); // Flag is automatically set by DockSpace() as LocalFlags, not SharedFlags! (#8145)

        Debug.Assert(dockspace_id != 0);
        ImGuiDockNode node = DockContextFindNodeByID(g, dockspace_id);
        if (node == null)
        {
            IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockSpace: dockspace node 0x{0:X8} created\n", dockspace_id);
            node = DockContextAddNode(g, dockspace_id);
            node.SetLocalFlags(ImGuiDockNodeFlags_CentralNode);
        }
        if (window_class != null && window_class.ClassId != node.WindowClass.ClassId)
            IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockSpace: dockspace node 0x{0:X8}: setup WindowClass 0x{1:X8} -> 0x{2:X8}\n", dockspace_id, node.WindowClass.ClassId, window_class.ClassId);
        node.SharedFlags = flags;
        node.WindowClass = window_class ?? new ImGuiWindowClass();

        // When a DockSpace transitioned form implicit to explicit this may be called a second time
        // It is possible that the node has already been claimed by a docked window which appeared before the DockSpace() node, so we overwrite IsDockSpace again.
        if (node.LastFrameActive == g.FrameCount && (flags & ImGuiDockNodeFlags_KeepAliveOnly) == 0)
        {
            Debug.Assert(node.IsDockSpace() == false, "Cannot call DockSpace() twice a frame with the same ID");
            node.SetLocalFlags(node.LocalFlags | ImGuiDockNodeFlags_DockSpace);
            return dockspace_id;
        }
        node.SetLocalFlags(node.LocalFlags | ImGuiDockNodeFlags_DockSpace);

        // Keep alive mode, this is allow windows docked into this node so stay docked even if they are not visible
        if ((flags & ImGuiDockNodeFlags_KeepAliveOnly) != 0)
        {
            node.LastFrameAlive = g.FrameCount;
            return dockspace_id;
        }

        ImVec2 content_avail = GetContentRegionAvail();
        ImVec2 size = ImMath.ImTrunc(size_val);
        if (size.x <= 0.0f)
            size.x = ImMath.ImMax(content_avail.x + size.x, 4.0f); // Arbitrary minimum child size (0.0f causing too much issues)
        if (size.y <= 0.0f)
            size.y = ImMath.ImMax(content_avail.y + size.y, 4.0f);
        Debug.Assert(size.x > 0.0f && size.y > 0.0f);

        node.Pos = window.DC.CursorPos;
        node.Size = node.SizeRef = size;
        SetNextWindowPos(node.Pos);
        SetNextWindowSize(node.Size);
        g.NextWindowData.PosUndock = false;

        // FIXME-DOCK: Why do we need a child window to host a dockspace, could we host it in the existing window?
        // FIXME-DOCK: What is the reason for not simply calling BeginChild()? (OK to have a reason but should be commented)
        int window_flags = ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_DockNodeHost;
        window_flags |= ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoTitleBar;
        window_flags |= ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse;
        window_flags |= ImGuiWindowFlags_NoBackground;

        string title = $"{window.Name}/DockSpace_{dockspace_id:X8}";

        PushStyleVar(ImGuiStyleVar_ChildBorderSize, 0.0f);
        Begin(title, window_flags);
        PopStyleVar();

        ImGuiWindow host_window = g.CurrentWindow;
        DockNodeSetupHostWindow(node, host_window);
        host_window.ChildId = window.GetID(title);
        node.OnlyNodeWithWindows = null;

        Debug.Assert(node.IsRootNode());

        // We need to handle the rare case were a central node is missing.
        // This can happen if the node was first created manually with DockBuilderAddNode() but _without_ the ImGuiDockNodeFlags_Dockspace.
        // Doing it correctly would set the _CentralNode flags, which would then propagate according to subsequent split.
        // It would also be ambiguous to attempt to assign a central node while there are split nodes, so we wait until there's a single node remaining.
        // The specific sub-property of _CentralNode we are interested in recovering here is the "Don't delete when empty" property,
        // as it doesn't make sense for an empty dockspace to not have this property.
        if (node.IsLeafNode() && !node.IsCentralNode())
            node.SetLocalFlags(node.LocalFlags | ImGuiDockNodeFlags_CentralNode);

        // Update the node
        DockNodeUpdate(node);

        End();

        ImRect bb = new ImRect(node.Pos, node.Pos + size);
        ItemSize(size);
        ItemAdd(bb, dockspace_id, null, ImGuiItemFlags_NoNav); // Not a nav point (could be, would need to draw the nav rect and replicate/refactor activation from BeginChild(), but seems like CTRL+Tab works better here?)
        if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HoveredRect) != 0 && IsWindowChildOf(g.HoveredWindow, host_window, false, true)) // To fullfill IsItemHovered(), similar to EndChild()
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredWindow;

        return dockspace_id;
    }

    // Tips: Use with ImGuiDockNodeFlags_PassthruCentralNode!
    // The limitation with this call is that your window won't have a local menu bar, but you can also use BeginMainMenuBar().
    // Even though we could pass window flags, it would also require the user to be able to call BeginMenuBar() somehow meaning we can't Begin/End in a single function.
    // If you really want a menu bar inside the same window as the one hosting the dockspace, you will need to copy this code somewhere and tweak it.
    // ImGuiID ImGui::DockSpaceOverViewport(ImGuiID dockspace_id, const ImGuiViewport* viewport, ImGuiDockNodeFlags dockspace_flags, const ImGuiWindowClass* window_class)
    public static ImGuiID DockSpaceOverViewport(ImGuiID dockspace_id = 0, ImGuiViewport viewport = null, ImGuiDockNodeFlags dockspace_flags = 0, ImGuiWindowClass window_class = null)
    {
        if (viewport == null)
            viewport = GetMainViewport();

        // Submit a window filling the entire viewport
        SetNextWindowPos(viewport.WorkPos);
        SetNextWindowSize(viewport.WorkSize);
        SetNextWindowViewport(viewport.ID);

        int host_window_flags = 0;
        host_window_flags |= ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoDocking;
        host_window_flags |= ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus;
        if ((dockspace_flags & ImGuiDockNodeFlags_PassthruCentralNode) != 0)
            host_window_flags |= ImGuiWindowFlags_NoBackground;

        // FIXME-OPT: When using ImGuiDockNodeFlags_KeepAliveOnly with DockSpaceOverViewport() we might be able to spare submitting the window,
        // since DockSpace() with that flag doesn't need a window. We'd only need to compute the default ID accordingly.
        if ((dockspace_flags & ImGuiDockNodeFlags_KeepAliveOnly) != 0)
            host_window_flags |= ImGuiWindowFlags_NoMouseInputs;

        string label = $"WindowOverViewport_{viewport.ID:X8}";

        PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
        PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
        PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0.0f, 0.0f));
        Begin(label, host_window_flags);
        PopStyleVar(3);

        // Submit the dockspace
        if (dockspace_id == 0)
            dockspace_id = GetID("DockSpace");
        DockSpace(dockspace_id, new ImVec2(0.0f, 0.0f), dockspace_flags, window_class);

        End();

        return dockspace_id;
    }

    //-----------------------------------------------------------------------------
    // Docking: Builder Functions
    //-----------------------------------------------------------------------------
    // Very early end-user API to manipulate dock nodes.
    // Only available in imgui_internal.h. Expect this API to change/break!
    // It is expected that those functions are all called _before_ the dockspace node submission.
    //-----------------------------------------------------------------------------

    // void ImGui::DockBuilderDockWindow(const char* window_name, ImGuiID node_id)
    public static void DockBuilderDockWindow(string window_name, ImGuiID node_id)
    {
        // We don't preserve relative order of multiple docked windows (by clearing DockOrder back to -1)
        ImGuiContext g = GImGui;
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockBuilderDockWindow '{0}' to node 0x{1:X8}\n", window_name, node_id);
        ImGuiID window_id = ImGuiHash.ImHashStr(window_name, 0);
        ImGuiWindow window = FindWindowByID(window_id);
        if (window != null)
        {
            // Apply to created window
            ImGuiID prev_node_id = window.DockId;
            SetWindowDock(window, node_id, ImGuiCond_Always);
            if (window.DockId != prev_node_id)
                window.DockOrder = -1;
        }
        else
        {
            // Apply to settings
            ImGuiWindowSettings settings = FindWindowSettingsByID(window_id);
            if (settings == null)
                settings = CreateNewWindowSettings(window_name);
            if (settings.DockId != node_id)
                settings.DockOrder = -1;
            settings.DockId = node_id;
        }
    }

    // ImGuiDockNode* ImGui::DockBuilderGetNode(ImGuiID node_id)
    public static ImGuiDockNode DockBuilderGetNode(ImGuiID node_id)
    {
        ImGuiContext g = GImGui;
        return DockContextFindNodeByID(g, node_id);
    }

    // void ImGui::DockBuilderSetNodePos(ImGuiID node_id, ImVec2 pos)
    public static void DockBuilderSetNodePos(ImGuiID node_id, ImVec2 pos)
    {
        ImGuiContext g = GImGui;
        ImGuiDockNode node = DockContextFindNodeByID(g, node_id);
        if (node == null)
            return;
        node.Pos = pos;
        node.AuthorityForPos = (byte)ImGuiDataAuthority_DockNode;
    }

    // void ImGui::DockBuilderSetNodeSize(ImGuiID node_id, ImVec2 size)
    public static void DockBuilderSetNodeSize(ImGuiID node_id, ImVec2 size)
    {
        ImGuiContext g = GImGui;
        ImGuiDockNode node = DockContextFindNodeByID(g, node_id);
        if (node == null)
            return;
        Debug.Assert(size.x > 0.0f && size.y > 0.0f);
        node.Size = node.SizeRef = size;
        node.AuthorityForSize = (byte)ImGuiDataAuthority_DockNode;
    }

    // Make sure to use the ImGuiDockNodeFlags_DockSpace flag to create a dockspace node! Otherwise this will create a floating node!
    // - Floating node: you can then call DockBuilderSetNodePos()/DockBuilderSetNodeSize() to position and size the floating node.
    // - Dockspace node: calling DockBuilderSetNodePos() is unnecessary.
    // - If you intend to split a node immediately after creation using DockBuilderSplitNode(), make sure to call DockBuilderSetNodeSize() beforehand!
    //   For various reason, the splitting code currently needs a base size otherwise space may not be allocated as precisely as you would expect.
    // - Use (id == 0) to let the system allocate a node identifier.
    // - Existing node with a same id will be removed.
    // ImGuiID ImGui::DockBuilderAddNode(ImGuiID node_id, ImGuiDockNodeFlags flags)
    public static ImGuiID DockBuilderAddNode(ImGuiID node_id = 0, ImGuiDockNodeFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockBuilderAddNode 0x{0:X8} flags={1:X8}\n", node_id, flags);

        if (node_id != 0)
            DockBuilderRemoveNode(node_id);

        ImGuiDockNode node = null;
        if ((flags & ImGuiDockNodeFlags_DockSpace) != 0)
        {
            DockSpace(node_id, new ImVec2(0, 0), (flags & ~ImGuiDockNodeFlags_DockSpace) | ImGuiDockNodeFlags_KeepAliveOnly);
            node = DockContextFindNodeByID(g, node_id);
        }
        else
        {
            node = DockContextAddNode(g, node_id);
            node.SetLocalFlags(flags);
        }
        node.LastFrameAlive = g.FrameCount;   // Set this otherwise BeginDocked will undock during the same frame.
        return node.ID;
    }

    // void ImGui::DockBuilderRemoveNode(ImGuiID node_id)
    public static void DockBuilderRemoveNode(ImGuiID node_id)
    {
        ImGuiContext g = GImGui;
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockBuilderRemoveNode 0x{0:X8}\n", node_id);

        ImGuiDockNode node = DockContextFindNodeByID(g, node_id);
        if (node == null)
            return;
        DockBuilderRemoveNodeDockedWindows(node_id, true);
        DockBuilderRemoveNodeChildNodes(node_id);
        // Node may have moved or deleted if e.g. any merge happened
        node = DockContextFindNodeByID(g, node_id);
        if (node == null)
            return;
        if (node.IsCentralNode() && node.ParentNode != null)
            node.ParentNode.SetLocalFlags(node.ParentNode.LocalFlags | ImGuiDockNodeFlags_CentralNode);
        DockContextRemoveNode(g, node, true);
    }

    // root_id = 0 to remove all, root_id != 0 to remove child of given node.
    // void ImGui::DockBuilderRemoveNodeChildNodes(ImGuiID root_id)
    public static void DockBuilderRemoveNodeChildNodes(ImGuiID root_id)
    {
        ImGuiContext g = GImGui;
        ImGuiDockContext dc = g.DockContext;

        ImGuiDockNode root_node = root_id != 0 ? DockContextFindNodeByID(g, root_id) : null;
        if (root_id != 0 && root_node == null)
            return;
        bool has_central_node = false;

        int backup_root_node_authority_for_pos = root_node != null ? root_node.AuthorityForPos : (byte)ImGuiDataAuthority_Auto;
        int backup_root_node_authority_for_size = root_node != null ? root_node.AuthorityForSize : (byte)ImGuiDataAuthority_Auto;

        // Process active windows
        ImVector<ImGuiDockNode> nodes_to_remove = new ImVector<ImGuiDockNode>();
        for (int n = 0; n < dc.Nodes.Data.Size; n++)
            if (dc.Nodes.Data.Data[n].val_p is ImGuiDockNode node)
            {
                bool want_removal = (root_id == 0) || (node.ID != root_id && DockNodeGetRootNode(node).ID == root_id);
                if (want_removal)
                {
                    if (node.IsCentralNode())
                        has_central_node = true;
                    if (root_id != 0)
                        DockContextQueueNotifyRemovedNode(g, node);
                    if (root_node != null)
                    {
                        DockNodeMoveWindows(root_node, node);
                        DockSettingsRenameNodeReferences(node.ID, root_node.ID);
                    }
                    nodes_to_remove.push_back(node);
                }
            }

        // DockNodeMoveWindows->DockNodeAddWindow will normally set those when reaching two windows (which is only adequate during interactive merge)
        // Make sure we don't lose our current pos/size. (FIXME-DOCK: Consider tidying up that code in DockNodeAddWindow instead)
        if (root_node != null)
        {
            root_node.AuthorityForPos = (byte)backup_root_node_authority_for_pos;
            root_node.AuthorityForSize = (byte)backup_root_node_authority_for_size;
        }

        // Apply to settings
        for (int settings_i = 0; settings_i < g.SettingsWindows.size(); settings_i++)
        {
            ImGuiWindowSettings settings = g.SettingsWindows[settings_i];
            ImGuiID window_settings_dock_id = settings.DockId;
            if (window_settings_dock_id == 0)
                continue;
            for (int n = 0; n < nodes_to_remove.Size; n++)
                if (nodes_to_remove[n].ID == window_settings_dock_id)
                {
                    settings.DockId = root_id;
                    break;
                }
        }

        // Not really efficient, but easier to destroy a whole hierarchy considering DockContextRemoveNode is attempting to merge nodes
        if (nodes_to_remove.Size > 1)
            Array.Sort(nodes_to_remove.Data, 0, nodes_to_remove.Size, Comparer<ImGuiDockNode>.Create(DockNodeComparerDepthMostFirst));
        for (int n = 0; n < nodes_to_remove.Size; n++)
            DockContextRemoveNode(g, nodes_to_remove[n], false);

        if (root_id == 0)
        {
            dc.Nodes.Clear();
            dc.Requests.clear();
        }
        else if (has_central_node)
        {
            root_node.CentralNode = root_node;
            root_node.SetLocalFlags(root_node.LocalFlags | ImGuiDockNodeFlags_CentralNode);
        }
    }

    // void ImGui::DockBuilderRemoveNodeDockedWindows(ImGuiID root_id, bool clear_settings_refs)
    public static void DockBuilderRemoveNodeDockedWindows(ImGuiID root_id, bool clear_settings_refs = true)
    {
        // Clear references in settings
        ImGuiContext g = GImGui;
        if (clear_settings_refs)
        {
            for (int settings_i = 0; settings_i < g.SettingsWindows.size(); settings_i++)
            {
                ImGuiWindowSettings settings = g.SettingsWindows[settings_i];
                bool want_removal = (root_id == 0) || (settings.DockId == root_id);
                if (!want_removal && settings.DockId != 0)
                {
                    ImGuiDockNode node = DockContextFindNodeByID(g, settings.DockId);
                    if (node != null && DockNodeGetRootNode(node).ID == root_id)
                        want_removal = true;
                }
                if (want_removal)
                    settings.DockId = 0;
            }
        }

        // Clear references in windows
        for (int n = 0; n < g.Windows.Size; n++)
        {
            ImGuiWindow window = g.Windows[n];
            bool want_removal = (root_id == 0) || (window.DockNode != null && DockNodeGetRootNode(window.DockNode).ID == root_id) || (window.DockNodeAsHost != null && window.DockNodeAsHost.ID == root_id);
            if (want_removal)
            {
                ImGuiID backup_dock_id = window.DockId;
                DockContextProcessUndockWindow(g, window, clear_settings_refs);
                if (!clear_settings_refs)
                    Debug.Assert(window.DockId == backup_dock_id);
            }
        }
    }

    // If 'out_id_at_dir' or 'out_id_at_opposite_dir' are non NULL, the function will write out the ID of the two new nodes created.
    // Return value is ID of the node at the specified direction, so same as (*out_id_at_dir) if that pointer is set.
    // FIXME-DOCK: We are not exposing nor using split_outer.
    // ImGuiID ImGui::DockBuilderSplitNode(ImGuiID id, ImGuiDir split_dir, float size_ratio_for_node_at_dir, ImGuiID* out_id_at_dir, ImGuiID* out_id_at_opposite_dir)
    public static ImGuiID DockBuilderSplitNode(ImGuiID id, int split_dir, float size_ratio_for_node_at_dir, out ImGuiID out_id_at_dir, out ImGuiID out_id_at_opposite_dir)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(split_dir != ImGuiDir_None);
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockBuilderSplitNode: node 0x{0:X8}, split_dir {1}\n", id, split_dir);

        out_id_at_dir = 0;
        out_id_at_opposite_dir = 0;

        ImGuiDockNode node = DockContextFindNodeByID(g, id);
        if (node == null)
        {
            Debug.Assert(node != null);
            return 0;
        }

        ImGuiDockRequest req = new ImGuiDockRequest();
        req.Type = ImGuiDockRequestType.ImGuiDockRequestType_Split;
        req.DockTargetWindow = null;
        req.DockTargetNode = node;
        req.DockPayload = null;
        req.DockSplitDir = split_dir;
        req.DockSplitRatio = ImMath.ImSaturate((split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? size_ratio_for_node_at_dir : 1.0f - size_ratio_for_node_at_dir);
        req.DockSplitOuter = false;
        DockContextProcessDock(g, req);

        ImGuiID id_at_dir = node.ChildNodes[(split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? 0 : 1].ID;
        ImGuiID id_at_opposite_dir = node.ChildNodes[(split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? 1 : 0].ID;
        out_id_at_dir = id_at_dir;
        out_id_at_opposite_dir = id_at_opposite_dir;
        return id_at_dir;
    }

    // static ImGuiDockNode* DockBuilderCopyNodeRec(ImGuiDockNode* src_node, ImGuiID dst_node_id_if_known, ImVector<ImGuiID>* out_node_remap_pairs)
    static ImGuiDockNode DockBuilderCopyNodeRec(ImGuiDockNode src_node, ImGuiID dst_node_id_if_known, ImVector<ImGuiID> out_node_remap_pairs)
    {
        ImGuiContext g = GImGui;
        ImGuiDockNode dst_node = DockContextAddNode(g, dst_node_id_if_known);
        dst_node.SharedFlags = src_node.SharedFlags;
        dst_node.LocalFlags = src_node.LocalFlags;
        dst_node.LocalFlagsInWindows = ImGuiDockNodeFlags_None;
        dst_node.Pos = src_node.Pos;
        dst_node.Size = src_node.Size;
        dst_node.SizeRef = src_node.SizeRef;
        dst_node.SplitAxis = src_node.SplitAxis;
        dst_node.UpdateMergedFlags();

        out_node_remap_pairs.push_back(src_node.ID);
        out_node_remap_pairs.push_back(dst_node.ID);

        for (int child_n = 0; child_n < src_node.ChildNodes.Length; child_n++)
            if (src_node.ChildNodes[child_n] != null)
            {
                dst_node.ChildNodes[child_n] = DockBuilderCopyNodeRec(src_node.ChildNodes[child_n], 0, out_node_remap_pairs);
                dst_node.ChildNodes[child_n].ParentNode = dst_node;
            }

        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] Fork node {0:X8} -> {1:X8} ({2} childs)\n", src_node.ID, dst_node.ID, dst_node.IsSplitNode() ? 2 : 0);
        return dst_node;
    }

    // void ImGui::DockBuilderCopyNode(ImGuiID src_node_id, ImGuiID dst_node_id, ImVector<ImGuiID>* out_node_remap_pairs)
    public static void DockBuilderCopyNode(ImGuiID src_node_id, ImGuiID dst_node_id, ImVector<ImGuiID> out_node_remap_pairs)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(src_node_id != 0);
        Debug.Assert(dst_node_id != 0);
        Debug.Assert(out_node_remap_pairs != null);

        DockBuilderRemoveNode(dst_node_id);

        ImGuiDockNode src_node = DockContextFindNodeByID(g, src_node_id);
        Debug.Assert(src_node != null);

        out_node_remap_pairs.clear();
        DockBuilderCopyNodeRec(src_node, dst_node_id, out_node_remap_pairs);

        Debug.Assert((out_node_remap_pairs.Size % 2) == 0);
    }

    // void ImGui::DockBuilderCopyWindowSettings(const char* src_name, const char* dst_name)
    public static void DockBuilderCopyWindowSettings(string src_name, string dst_name)
    {
        ImGuiWindow src_window = FindWindowByName(src_name);
        if (src_window == null)
            return;
        ImGuiWindow dst_window = FindWindowByName(dst_name);
        if (dst_window != null)
        {
            dst_window.Pos = src_window.Pos;
            dst_window.Size = src_window.Size;
            dst_window.SizeFull = src_window.SizeFull;
            dst_window.Collapsed = src_window.Collapsed;
        }
        else
        {
            ImGuiWindowSettings dst_settings = FindWindowSettingsByID(ImGuiHash.ImHashStr(dst_name, 0));
            if (dst_settings == null)
                dst_settings = CreateNewWindowSettings(dst_name);
            ImVec2ih window_pos_2ih = new ImVec2ih((int)src_window.Pos.x, (int)src_window.Pos.y);
            if (src_window.ViewportId != 0 && src_window.ViewportId != IMGUI_VIEWPORT_DEFAULT_ID)
            {
                dst_settings.ViewportPos = window_pos_2ih;
                dst_settings.ViewportId = src_window.ViewportId;
                dst_settings.Pos = new ImVec2ih(0, 0);
            }
            else
            {
                dst_settings.Pos = window_pos_2ih;
            }
            dst_settings.Size = new ImVec2ih((int)src_window.SizeFull.x, (int)src_window.SizeFull.y);
            dst_settings.Collapsed = src_window.Collapsed;
        }
    }

    // FIXME: Will probably want to change this signature, in particular how the window remapping pairs are passed.
    // void ImGui::DockBuilderCopyDockSpace(ImGuiID src_dockspace_id, ImGuiID dst_dockspace_id, ImVector<const char*>* in_window_remap_pairs)
    public static void DockBuilderCopyDockSpace(ImGuiID src_dockspace_id, ImGuiID dst_dockspace_id, ImVector<string> in_window_remap_pairs)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(src_dockspace_id != 0);
        Debug.Assert(dst_dockspace_id != 0);
        Debug.Assert(in_window_remap_pairs != null);
        Debug.Assert((in_window_remap_pairs.Size % 2) == 0);

        // Duplicate entire dock
        // FIXME: When overwriting dst_dockspace_id, windows that aren't part of our dockspace window class but that are docked in a same node will be split apart,
        // whereas we could attempt to at least keep them together in a new, same floating node.
        ImVector<ImGuiID> node_remap_pairs = new ImVector<ImGuiID>();
        DockBuilderCopyNode(src_dockspace_id, dst_dockspace_id, node_remap_pairs);

        // Attempt to transition all the upcoming windows associated to dst_dockspace_id into the newly created hierarchy of dock nodes
        // (The windows associated to src_dockspace_id are staying in place)
        ImVector<ImGuiID> src_windows = new ImVector<ImGuiID>();
        for (int remap_window_n = 0; remap_window_n < in_window_remap_pairs.Size; remap_window_n += 2)
        {
            string src_window_name = in_window_remap_pairs[remap_window_n];
            string dst_window_name = in_window_remap_pairs[remap_window_n + 1];
            ImGuiID src_window_id = ImGuiHash.ImHashStr(src_window_name, 0);
            src_windows.push_back(src_window_id);

            // Search in the remapping tables
            ImGuiID src_dock_id = 0;
            ImGuiWindow src_window = FindWindowByID(src_window_id);
            if (src_window != null)
                src_dock_id = src_window.DockId;
            else
            {
                ImGuiWindowSettings src_window_settings = FindWindowSettingsByID(src_window_id);
                if (src_window_settings != null)
                    src_dock_id = src_window_settings.DockId;
            }
            ImGuiID dst_dock_id = 0;
            for (int dock_remap_n = 0; dock_remap_n < node_remap_pairs.Size; dock_remap_n += 2)
                if (node_remap_pairs[dock_remap_n] == src_dock_id)
                {
                    dst_dock_id = node_remap_pairs[dock_remap_n + 1];
                    //node_remap_pairs[dock_remap_n] = node_remap_pairs[dock_remap_n + 1] = 0; // Clear
                    break;
                }

            if (dst_dock_id != 0)
            {
                // Docked windows gets redocked into the new node hierarchy.
                IMGUI_DEBUG_LOG_DOCKING(g, "[docking] Remap live window '{0}' 0x{1:X8} -> '{2}' 0x{3:X8}\n", src_window_name, src_dock_id, dst_window_name, dst_dock_id);
                DockBuilderDockWindow(dst_window_name, dst_dock_id);
            }
            else
            {
                // Floating windows gets their settings transferred (regardless of whether the new window already exist or not)
                // When this is leading to a Copy and not a Move, we would get two overlapping floating windows. Could we possibly dock them together?
                IMGUI_DEBUG_LOG_DOCKING(g, "[docking] Remap window settings '{0}' -> '{1}'\n", src_window_name, dst_window_name);
                DockBuilderCopyWindowSettings(src_window_name, dst_window_name);
            }
        }

        // Anything else in the source nodes of 'node_remap_pairs' are windows that are not included in the remapping list.
        // Find those windows and move to them to the cloned dock node. This may be optional?
        // Dock those are a second step as undocking would invalidate source dock nodes.
        // struct DockRemainingWindowTask { ImGuiWindow* Window; ImGuiID DockId; }
        ImVector<DockRemainingWindowTask> dock_remaining_windows = new ImVector<DockRemainingWindowTask>();
        for (int dock_remap_n = 0; dock_remap_n < node_remap_pairs.Size; dock_remap_n += 2)
            if (node_remap_pairs[dock_remap_n] != 0)
            {
                ImGuiID src_dock_id = node_remap_pairs[dock_remap_n];
                ImGuiID dst_dock_id = node_remap_pairs[dock_remap_n + 1];
                ImGuiDockNode node = DockBuilderGetNode(src_dock_id);
                for (int window_n = 0; window_n < node.Windows.Size; window_n++)
                {
                    ImGuiWindow window = node.Windows[window_n];
                    if (src_windows.contains(window.ID))
                        continue;

                    // Docked windows gets redocked into the new node hierarchy.
                    IMGUI_DEBUG_LOG_DOCKING(g, "[docking] Remap window '{0}' {1:X8} -> {2:X8}\n", window.Name, src_dock_id, dst_dock_id);
                    dock_remaining_windows.push_back(new DockRemainingWindowTask(window, dst_dock_id));
                }
            }
        for (int task_n = 0; task_n < dock_remaining_windows.Size; task_n++)
            DockBuilderDockWindow(dock_remaining_windows[task_n].Window.Name, dock_remaining_windows[task_n].DockId);
    }

    // C++ local struct DockRemainingWindowTask (in DockBuilderCopyDockSpace)
    private class DockRemainingWindowTask
    {
        public ImGuiWindow Window;
        public ImGuiID DockId;
        public DockRemainingWindowTask(ImGuiWindow window, ImGuiID dock_id) { Window = window; DockId = dock_id; }
    }

    // FIXME-DOCK: This is awkward because in series of split user is likely to loose access to its root node.
    // void ImGui::DockBuilderFinish(ImGuiID root_id)
    public static void DockBuilderFinish(ImGuiID root_id)
    {
        ImGuiContext g = GImGui;
        //DockContextRebuild(g);
        DockContextBuildAddWindowsToNodes(g, root_id);
    }

    // void ImGui::DockContextProcessUndockWindow(ImGuiContext* ctx, ImGuiWindow* window, bool clear_persistent_docking_ref)
    public static void DockContextProcessUndockWindow(ImGuiContext ctx, ImGuiWindow window, bool clear_persistent_docking_ref = true)
    {
        ImGuiContext g = ctx;
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextProcessUndockWindow window '{0}', clear_persistent_docking_ref = {1}\n", window.Name, clear_persistent_docking_ref);
        if (window.DockNode != null)
            DockNodeRemoveWindow(window.DockNode, window, clear_persistent_docking_ref ? 0 : window.DockId);
        else
            window.DockId = 0;
        window.Collapsed = false;
        window.DockIsActive = false;
        window.DockNodeIsVisible = window.DockTabIsVisible = false;
        window.Size = window.SizeFull = FixLargeWindowsWhenUndocking(window.SizeFull, window.Viewport);

        MarkIniSettingsDirty();
    }

    // void ImGui::DockContextProcessUndockNode(ImGuiContext* ctx, ImGuiDockNode* node)
    public static void DockContextProcessUndockNode(ImGuiContext ctx, ImGuiDockNode node)
    {
        ImGuiContext g = ctx;
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockContextProcessUndockNode node {0:X8}\n", node.ID);
        Debug.Assert(node.IsLeafNode());
        Debug.Assert(node.Windows.Size >= 1);

        if (node.IsRootNode() || node.IsCentralNode())
        {
            // In the case of a root node or central node, the node will have to stay in place. Create a new node to receive the payload.
            ImGuiDockNode new_node = DockContextAddNode(ctx, 0);
            new_node.Pos = node.Pos;
            new_node.Size = node.Size;
            new_node.SizeRef = node.SizeRef;
            DockNodeMoveWindows(new_node, node);
            DockSettingsRenameNodeReferences(node.ID, new_node.ID);
            node = new_node;
        }
        else
        {
            // Otherwise extract our node and merge our sibling back into the parent node.
            Debug.Assert(node.ParentNode.ChildNodes[0] == node || node.ParentNode.ChildNodes[1] == node);
            int index_in_parent = (node.ParentNode.ChildNodes[0] == node) ? 0 : 1;
            node.ParentNode.ChildNodes[index_in_parent] = null;
            DockNodeTreeMerge(ctx, node.ParentNode, node.ParentNode.ChildNodes[index_in_parent ^ 1]);
            node.ParentNode.AuthorityForViewport = (byte)ImGuiDataAuthority_Window; // The node that stays in place keeps the viewport, so our newly dragged out node will create a new viewport
            node.ParentNode = null;
        }
        for (int window_n = 0; window_n < node.Windows.Size; window_n++)
        {
            ImGuiWindow window = node.Windows[window_n];
            window.Flags &= ~ImGuiWindowFlags_ChildWindow;
            if (window.ParentWindow != null)
                FindEraseWindow(window.ParentWindow.DC.ChildWindows, window);
            UpdateWindowParentAndRootLinks(window, window.Flags, null);
        }
        node.AuthorityForPos = node.AuthorityForSize = (byte)ImGuiDataAuthority_DockNode;
        node.Size = FixLargeWindowsWhenUndocking(node.Size, node.Windows[0].Viewport);
        node.WantMouseMove = true;
        MarkIniSettingsDirty();
    }

    // static bool ImGui::DockNodeIsDropAllowed(ImGuiWindow* host_window, ImGuiWindow* root_payload)
    static bool DockNodeIsDropAllowed(ImGuiWindow host_window, ImGuiWindow root_payload)
    {
        if (root_payload.DockNodeAsHost != null && root_payload.DockNodeAsHost.IsSplitNode()) // FIXME-DOCK: Missing filtering
            return true;

        int payload_count = root_payload.DockNodeAsHost != null ? root_payload.DockNodeAsHost.Windows.Size : 1;
        for (int payload_n = 0; payload_n < payload_count; payload_n++)
        {
            ImGuiWindow payload = root_payload.DockNodeAsHost != null ? root_payload.DockNodeAsHost.Windows[payload_n] : root_payload;
            if (DockNodeIsDropAllowedOne(payload, host_window))
                return true;
        }
        return false;
    }

    //-----------------------------------------------------------------------------
    // Docking: Begin/End Support Functions (called from Begin/End)
    //-----------------------------------------------------------------------------

    // bool ImGui::GetWindowAlwaysWantOwnTabBar(ImGuiWindow* window)
    public static bool GetWindowAlwaysWantOwnTabBar(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        if (g.IO.ConfigDockingAlwaysTabBar || window.WindowClass.DockingAlwaysTabBar)
            if ((window.Flags & (ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoDocking)) == 0)
                if (!window.IsFallbackWindow)    // We don't support AlwaysTabBar on the fallback/implicit window to avoid unused dock-node overhead/noise
                    return true;
        return false;
    }

    // static ImGuiDockNode* ImGui::DockContextBindNodeToWindow(ImGuiContext* ctx, ImGuiWindow* window)
    static ImGuiDockNode DockContextBindNodeToWindow(ImGuiContext ctx, ImGuiWindow window)
    {
        ImGuiContext g = ctx;
        ImGuiDockNode node = DockContextFindNodeByID(ctx, window.DockId);
        Debug.Assert(window.DockNode == null);

        // We should not be docking into a split node (SetWindowDock should avoid this)
        if (node != null && node.IsSplitNode())
        {
            DockContextProcessUndockWindow(ctx, window);
            return null;
        }

        // Create node
        if (node == null)
        {
            node = DockContextAddNode(ctx, window.DockId);
            node.AuthorityForPos = node.AuthorityForSize = node.AuthorityForViewport = (byte)ImGuiDataAuthority_Window;
            node.LastFrameAlive = g.FrameCount;
        }

        // If the node just turned visible and is part of a hierarchy, it doesn't have a Size assigned by DockNodeTreeUpdatePosSize() yet,
        // so we're forcing a Pos/Size update from the first ancestor that is already visible (often it will be the root node).
        // If we don't do this, the window will be assigned a zero-size on its first frame, which won't ideally warm up the layout.
        // This is a little wonky because we don't normally update the Pos/Size of visible node mid-frame.
        if (!node.IsVisible)
        {
            ImGuiDockNode ancestor_node = node;
            while (!ancestor_node.IsVisible && ancestor_node.ParentNode != null)
                ancestor_node = ancestor_node.ParentNode;
            Debug.Assert(ancestor_node.Size.x > 0.0f && ancestor_node.Size.y > 0.0f);
            DockNodeUpdateHasCentralNodeChild(DockNodeGetRootNode(ancestor_node));
            DockNodeTreeUpdatePosSize(ancestor_node, ancestor_node.Pos, ancestor_node.Size, node);
        }

        // Add window to node
        bool node_was_visible = node.IsVisible;
        DockNodeAddWindow(node, window, true);
        node.IsVisible = node_was_visible; // Don't mark visible right away (so DockContextEndFrame() doesn't render it, maybe other side effects? will see)
        Debug.Assert(node == window.DockNode);
        return node;
    }

    // static void StoreDockStyleForWindow(ImGuiWindow* window)
    static void StoreDockStyleForWindow(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
            window.DockStyle.Colors[color_n] = ColorConvertFloat4ToU32(g.Style.Colors[GWindowDockStyleColors[color_n]]);
    }

    // void ImGui::BeginDocked(ImGuiWindow* window, bool* p_open)
    public static unsafe void BeginDocked(ImGuiWindow window, bool* p_open)
    {
        ImGuiContext g = GImGui;

        // Specific extra processing for fallback window (#9151), could be in Begin() as well.
        if (window.IsFallbackWindow && !window.WasActive)
        {
            DockNodeHideWindowDuringHostWindowCreation(window);
            return;
        }

        bool auto_dock_node = GetWindowAlwaysWantOwnTabBar(window);
        if (auto_dock_node)
        {
            if (window.DockId == 0)
            {
                Debug.Assert(window.DockNode == null);
                window.DockId = DockContextGenNodeID(g);
            }
        }
        else
        {
            // Calling SetNextWindowPos() undock windows by default (by setting PosUndock)
            bool want_undock = false;
            want_undock |= (window.Flags & ImGuiWindowFlags_NoDocking) != 0;
            want_undock |= (g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasPos) != 0 && (window.SetWindowPosAllowFlags & g.NextWindowData.PosCond) != 0 && g.NextWindowData.PosUndock;
            if (want_undock)
            {
                DockContextProcessUndockWindow(g, window);
                return;
            }
        }

        // Bind to our dock node
        ImGuiDockNode node = window.DockNode;
        if (node != null)
            Debug.Assert(window.DockId == node.ID);
        if (window.DockId != 0 && node == null)
        {
            node = DockContextBindNodeToWindow(g, window);
            if (node == null)
                return;
        }

        // Undock if our dockspace node disappeared
        // Note how we are testing for LastFrameAlive and NOT LastFrameActive. A DockSpace node can be maintained alive while being inactive with ImGuiDockNodeFlags_KeepAliveOnly.
        if (node.LastFrameAlive < g.FrameCount)
        {
            // If the window has been orphaned, transition the docknode to an implicit node processed in DockContextNewFrameUpdateDocking()
            ImGuiDockNode root_node = DockNodeGetRootNode(node);
            if (root_node.LastFrameAlive < g.FrameCount)
                DockContextProcessUndockWindow(g, window);
            else
                window.DockIsActive = true;
            return;
        }

        // Store style overrides
        StoreDockStyleForWindow(window);

        // Fast path return. It is common for windows to hold on a persistent DockId but be the only visible window,
        // and never create neither a host window neither a tab bar.
        // FIXME-DOCK: replace ->HostWindow NULL compare with something more explicit (~was initially intended as a first frame test)
        if (node.HostWindow == null)
        {
            if (node.State == ImGuiDockNodeState_HostWindowHiddenBecauseWindowsAreResizing)
                window.DockIsActive = true;
            if (node.Windows.Size > 1 && window.Appearing) // Only hide appearing window
                DockNodeHideWindowDuringHostWindowCreation(window);
            return;
        }

        // We can have zero-sized nodes (e.g. children of a small-size dockspace)
        Debug.Assert(node.HostWindow != null);
        Debug.Assert(node.IsLeafNode());
        Debug.Assert(node.Size.x >= 0.0f && node.Size.y >= 0.0f);
        node.State = ImGuiDockNodeState_HostWindowVisible;

        // Undock if we are submitted earlier than the host window
        if ((node.MergedFlags & ImGuiDockNodeFlags_KeepAliveOnly) == 0 && window.BeginOrderWithinContext < node.HostWindow.BeginOrderWithinContext)
        {
            DockContextProcessUndockWindow(g, window);
            return;
        }

        // Position/Size window
        SetNextWindowPos(node.Pos);
        SetNextWindowSize(node.Size);
        g.NextWindowData.PosUndock = false; // Cancel implicit undocking of SetNextWindowPos()
        window.DockIsActive = true;
        window.DockNodeIsVisible = true;
        window.DockTabIsVisible = false;
        if ((node.MergedFlags & ImGuiDockNodeFlags_KeepAliveOnly) != 0)
            return;

        // When the window is selected we mark it as visible.
        if (node.VisibleWindow == window)
            window.DockTabIsVisible = true;

        // Update window flag
        Debug.Assert((window.Flags & ImGuiWindowFlags_ChildWindow) == 0);
        window.Flags |= ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_NoResize;
        window.ChildFlags |= ImGuiChildFlags_AlwaysUseWindowPadding;
        if (node.IsHiddenTabBar() || node.IsNoTabBar())
            window.Flags |= ImGuiWindowFlags_NoTitleBar;
        else
            window.Flags &= ~ImGuiWindowFlags_NoTitleBar;      // Clear the NoTitleBar flag in case the user set it: confusingly enough we need a title bar height so we are correctly offset, but it won't be displayed!

        // Save new dock order only if the window has been visible once already
        // This allows multiple windows to be created in the same frame and have their respective dock orders preserved.
        if (node.TabBar != null && window.WasActive)
            window.DockOrder = (short)DockNodeGetTabOrder(window);

        if ((node.WantCloseAll || node.WantCloseTabId == window.TabId) && p_open != null)
            *p_open = false;

        // Update ChildId to allow returning from Child to Parent with Escape
        ImGuiWindow parent_window = window.DockNode.HostWindow;
        window.ChildId = parent_window.GetID(window.Name);
    }

    // void ImGui::BeginDockableDragDropSource(ImGuiWindow* window)
    public static void BeginDockableDragDropSource(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.ActiveId == window.MoveId);
        Debug.Assert(g.MovingWindow == window);
        Debug.Assert(g.CurrentWindow == window);

        // 0: Hold SHIFT to disable docking, 1: Hold SHIFT to enable docking.
        if (g.IO.ConfigDockingWithShift != g.IO.KeyShift)
        {
            // When ConfigDockingWithShift is set, display a tooltip to increase UI affordance.
            // We cannot set for HoveredWindowUnderMovingWindow != NULL here, as it is only valid/useful when drag and drop is already active
            // (because of the 'is_mouse_dragging_with_an_expected_destination' logic in UpdateViewportsNewFrame() function)
            Debug.Assert(g.NextWindowData.HasFlags == 0);
            if (g.IO.ConfigDockingWithShift && g.MouseStationaryTimer >= 1.0f && g.ActiveId >= 1.0f)
                SetTooltip(LocalizeGetMsg(ImGuiLocKey_DockingHoldShiftToDock));
            return;
        }

        g.LastItemData.ID = window.MoveId;
        window = window.RootWindowDockTree;
        Debug.Assert((window.Flags & ImGuiWindowFlags_NoDocking) == 0);
        bool is_drag_docking = (g.IO.ConfigDockingWithShift) || new ImRect(0, 0, window.SizeFull.x, GetFrameHeight()).Contains(g.ActiveIdClickOffset); // FIXME-DOCKING: Need to make this stateful and explicit
        ImGuiDragDropFlags drag_drop_flags = ImGuiDragDropFlags_SourceNoPreviewTooltip | ImGuiDragDropFlags_SourceNoHoldToOpenOthers | ImGuiDragDropFlags_PayloadAutoExpire | ImGuiDragDropFlags_PayloadNoCrossContext | ImGuiDragDropFlags_PayloadNoCrossProcess;
        if (is_drag_docking && BeginDragDropSource(drag_drop_flags))
        {
            // C# adaptation: payload stores the window ID (C++ stores a ImGuiWindow*; retrieved via DockNodeGetWindowFromPayload()).
            SetDragDropPayload(IMGUI_PAYLOAD_TYPE_WINDOW, BitConverter.GetBytes(window.ID), sizeof(uint));
            EndDragDropSource();
            StoreDockStyleForWindow(window); // Store style overrides while dragging (even when not docked) because docking preview may need it.
        }
    }

    // void ImGui::BeginDockableDragDropTarget(ImGuiWindow* window)
    public static void BeginDockableDragDropTarget(ImGuiWindow window)
    {
        ImGuiContext g = GImGui;

        //IM_ASSERT(window.RootWindowDockTree == window); // May also be a DockSpace
        Debug.Assert((window.Flags & ImGuiWindowFlags_NoDocking) == 0);
        if (!g.DragDropActive)
            return;
        //GetForegroundDrawList(window).AddRect(window.Pos, window.Pos + window.Size, IM_COL32(255, 255, 0, 255));
        if (!BeginDragDropTargetCustom(window.Rect(), window.ID))
            return;

        // Peek into the payload before calling AcceptDragDropPayload() so we can handle overlapping dock nodes with filtering
        // (this is a little unusual pattern, normally most code would call AcceptDragDropPayload directly)
        ImGuiPayload payload = g.DragDropPayload;
        ImGuiWindow peek_payload_window = DockNodeGetWindowFromPayload(payload);
        if (!payload.IsDataType(IMGUI_PAYLOAD_TYPE_WINDOW) || !DockNodeIsDropAllowed(window, peek_payload_window))
        {
            EndDragDropTarget();
            return;
        }

        ImGuiWindow payload_window = peek_payload_window;
        if (AcceptDragDropPayload(IMGUI_PAYLOAD_TYPE_WINDOW, ImGuiDragDropFlags_AcceptBeforeDelivery | ImGuiDragDropFlags_AcceptNoDrawDefaultRect) != null)
        {
            // Select target node
            // (Important: we cannot use g.HoveredDockNode here! Because each of our target node have filters based on payload, each candidate drop target will do its own evaluation)
            bool dock_into_floating_window = false;
            ImGuiDockNode node = null;
            if (window.DockNodeAsHost != null)
            {
                // Cannot assume that node will != NULL even though we passed the rectangle test: it depends on padding/spacing handled by DockNodeTreeFindVisibleNodeByPos().
                node = DockNodeTreeFindVisibleNodeByPos(window.DockNodeAsHost, g.IO.MousePos);

                // There is an edge case when docking into a dockspace which only has _inactive_ nodes (because none of the windows are active)
                // In this case we need to fallback into any leaf mode, possibly the central node.
                // FIXME-20181220: We should not have to test for IsLeafNode() here but we have another bug to fix first.
                if (node != null && node.IsDockSpace() && node.IsRootNode())
                    node = (node.CentralNode != null && node.IsLeafNode()) ? node.CentralNode : DockNodeTreeFindFallbackLeafNode(node);
            }
            else
            {
                if (window.DockNode != null)
                    node = window.DockNode;
                else
                    dock_into_floating_window = true; // Dock into a regular window
            }

            ImRect explicit_target_rect = (node != null && node.TabBar != null && !node.IsHiddenTabBar() && !node.IsNoTabBar()) ? node.TabBar.BarRect : new ImRect(window.Pos, window.Pos + new ImVec2(window.Size.x, GetFrameHeight()));
            bool is_explicit_target = g.IO.ConfigDockingWithShift || IsMouseHoveringRect(explicit_target_rect.Min, explicit_target_rect.Max);

            // Preview docking request and find out split direction/ratio
            //const bool do_preview = true;     // Ignore testing for payload->IsPreview() which removes one frame of delay, but breaks overlapping drop targets within the same window.
            bool do_preview = payload.IsPreview() || payload.IsDelivery();
            if (do_preview && (node != null || dock_into_floating_window))
            {
                // If we have a non-leaf node it means we are hovering the border of a parent node, in which case only outer markers will appear.
                ImGuiDockPreviewData split_inner = new ImGuiDockPreviewData();
                ImGuiDockPreviewData split_outer = new ImGuiDockPreviewData();
                ImGuiDockPreviewData split_data = split_inner;
                if (node != null && (node.ParentNode != null || node.IsCentralNode() || !node.IsLeafNode()))
                {
                    ImGuiDockNode root_node = DockNodeGetRootNode(node);
                    if (root_node != null)
                    {
                        DockNodePreviewDockSetup(window, root_node, payload_window, null, split_outer, is_explicit_target, true);
                        if (split_outer.IsSplitDirExplicit)
                            split_data = split_outer;
                    }
                }
                if (node == null || node.IsLeafNode())
                    DockNodePreviewDockSetup(window, node, payload_window, null, split_inner, is_explicit_target, false);
                if (split_data == split_outer)
                    split_inner.IsDropAllowed = false;

                // Draw inner then outer, so that previewed tab (in inner data) will be behind the outer drop boxes
                DockNodePreviewDockRender(window, node, payload_window, split_inner);
                DockNodePreviewDockRender(window, node, payload_window, split_outer);

                // Queue docking request
                if (split_data.IsDropAllowed && payload.IsDelivery())
                    DockContextQueueDock(g, window, split_data.SplitNode, payload_window, split_data.SplitDir, split_data.SplitRatio, split_data == split_outer);
            }
        }
        EndDragDropTarget();
    }

    //-----------------------------------------------------------------------------
    // Docking: Settings
    //-----------------------------------------------------------------------------

    // static void ImGui::DockSettingsRenameNodeReferences(ImGuiID old_node_id, ImGuiID new_node_id)
    static void DockSettingsRenameNodeReferences(ImGuiID old_node_id, ImGuiID new_node_id)
    {
        ImGuiContext g = GImGui;
        IMGUI_DEBUG_LOG_DOCKING(g, "[docking] DockSettingsRenameNodeReferences: from 0x{0:X8} -> to 0x{1:X8}\n", old_node_id, new_node_id);
        for (int window_n = 0; window_n < g.Windows.Size; window_n++)
        {
            ImGuiWindow window = g.Windows[window_n];
            if (window.DockId == old_node_id && window.DockNode == null)
                window.DockId = new_node_id;
        }
        //// FIXME-OPT: We could remove this loop by storing the index in the map
        for (int i = 0; i < g.SettingsWindows.size(); i++)
        {
            ImGuiWindowSettings settings = g.SettingsWindows[i];
            if (settings.DockId == old_node_id)
                settings.DockId = new_node_id;
        }
    }

    // Remove references stored in ImGuiWindowSettings to the given ImGuiDockNodeSettings
    // static void ImGui::DockSettingsRemoveNodeReferences(ImGuiID* node_ids, int node_ids_count)
    static void DockSettingsRemoveNodeReferences(ImGuiID[] node_ids, int node_ids_count)
    {
        ImGuiContext g = GImGui;
        int found = 0;
        //// FIXME-OPT: We could remove this loop by storing the index in the map
        for (int i = 0; i < g.SettingsWindows.size(); i++)
        {
            ImGuiWindowSettings settings = g.SettingsWindows[i];
            for (int node_n = 0; node_n < node_ids_count; node_n++)
                if (settings.DockId == node_ids[node_n])
                {
                    settings.DockId = 0;
                    settings.DockOrder = -1;
                    if (++found < node_ids_count)
                        break;
                    return;
                }
        }
    }

    // static ImGuiDockNodeSettings* ImGui::DockSettingsFindNodeSettings(ImGuiContext* ctx, ImGuiID id)
    static ImGuiDockNodeSettings DockSettingsFindNodeSettings(ImGuiContext ctx, ImGuiID id)
    {
        // FIXME-OPT
        ImGuiDockContext dc = ctx.DockContext;
        for (int n = 0; n < dc.NodesSettings.Size; n++)
            if (dc.NodesSettings[n].ID == id)
                return dc.NodesSettings[n];
        return null;
    }

    // C# helper: C++-style starts-with at an explicit offset (string.StartsWith(string, int) does not exist in .NET)
    static bool StartsWithAt(string line, int i, string name)
    {
        return i >= 0 && i + name.Length <= line.Length && string.CompareOrdinal(line, i, name, 0, name.Length) == 0;
    }

    static int DockSettingsSkipBlank(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i;
    }

    // sscanf("%n")-style sequential scanners: on success advance i past the field, on failure leave i unchanged.
    static bool DockSettingsScanHex(string line, ref int i, string name, out ImGuiID out_value)
    {
        out_value = 0;
        int j = DockSettingsSkipBlank(line, i);
        if (!StartsWithAt(line, j, name)) return false;
        j += name.Length;
        if (j + 1 < line.Length && line[j] == '0' && (line[j + 1] == 'x' || line[j + 1] == 'X')) j += 2;
        uint v = 0;
        int digits = 0;
        while (j < line.Length && digits < 8)
        {
            char c = line[j];
            int d = (c >= '0' && c <= '9') ? c - '0' : (c >= 'a' && c <= 'f') ? c - 'a' + 10 : (c >= 'A' && c <= 'F') ? c - 'A' + 10 : -1;
            if (d == -1) break;
            v = v * 16u + (uint)d;
            j++; digits++;
        }
        if (digits == 0) return false;
        out_value = v;
        i = j;
        return true;
    }

    static bool DockSettingsScanInt(string line, ref int i, string name, out int out_value)
    {
        out_value = 0;
        int j = DockSettingsSkipBlank(line, i);
        if (!StartsWithAt(line, j, name)) return false;
        j = ScanInt(line, j + name.Length, out out_value);
        if (j == -1) { out_value = 0; return false; }
        i = j;
        return true;
    }

    static bool DockSettingsScanTwoInts(string line, ref int i, string name, out int a, out int b)
    {
        a = b = 0;
        int j = DockSettingsSkipBlank(line, i);
        if (!StartsWithAt(line, j, name)) return false;
        j = ScanInt(line, j + name.Length, out a);
        if (j == -1 || j >= line.Length || line[j] != ',') return false;
        j = ScanInt(line, j + 1, out b);
        if (j == -1) return false;
        i = j;
        return true;
    }

    static bool DockSettingsScanChar(string line, ref int i, string name, out char out_c)
    {
        out_c = '\0';
        int j = DockSettingsSkipBlank(line, i);
        if (!StartsWithAt(line, j, name)) return false;
        j += name.Length;
        if (j >= line.Length) return false;
        out_c = line[j];
        i = j + 1;
        return true;
    }

    // Clear settings data
    // static void ImGui::DockSettingsHandler_ClearAll(ImGuiContext* ctx, ImGuiSettingsHandler*)
    static void DockSettingsHandler_ClearAll(ImGuiContext ctx, ImGuiSettingsHandler handler)
    {
        ImGuiDockContext dc = ctx.DockContext;
        dc.NodesSettings.clear();
        DockContextClearNodes(ctx, 0, true);
    }

    // Recreate nodes based on settings data
    // static void ImGui::DockSettingsHandler_ApplyAll(ImGuiContext* ctx, ImGuiSettingsHandler*)
    static void DockSettingsHandler_ApplyAll(ImGuiContext ctx, ImGuiSettingsHandler handler)
    {
        // Prune settings at boot time only
        ImGuiDockContext dc = ctx.DockContext;
        if (ctx.Windows.Size == 0)
            DockContextPruneUnusedSettingsNodes(ctx);
        DockContextBuildNodesFromSettings(ctx, dc.NodesSettings);
        DockContextBuildAddWindowsToNodes(ctx, 0);
    }

    // static void* ImGui::DockSettingsHandler_ReadOpen(ImGuiContext*, ImGuiSettingsHandler*, const char* name)
    static object DockSettingsHandler_ReadOpen(ImGuiContext ctx, ImGuiSettingsHandler handler, string name)
    {
        if (name != "Data")
            return null;
        return (object)1;
    }

    // static void ImGui::DockSettingsHandler_ReadLine(ImGuiContext* ctx, ImGuiSettingsHandler*, void*, const char* line)
    static void DockSettingsHandler_ReadLine(ImGuiContext ctx, ImGuiSettingsHandler handler, object entry, string line)
    {
        char c;
        int x = 0, y = 0;

        // Parsing, e.g.
        // " DockNode   ID=0x00000001 Pos=383,193 Size=201,322 Split=Y "
        // "   DockNode ID=0x00000002 Parent=0x00000001 "
        // Important: this code expect currently fields in a fixed order.
        ImGuiDockNodeSettings node = new ImGuiDockNodeSettings();
        int i = DockSettingsSkipBlank(line, 0);
        if (StartsWithAt(line, i, "DockNode")) { i = DockSettingsSkipBlank(line, i + 8); }
        else if (StartsWithAt(line, i, "DockSpace")) { i = DockSettingsSkipBlank(line, i + 9); node.Flags |= ImGuiDockNodeFlags_DockSpace; }
        else return;
        if (DockSettingsScanHex(line, ref i, "ID=", out ImGuiID id)) { node.ID = id; } else return;
        if (DockSettingsScanHex(line, ref i, "Parent=", out ImGuiID parent_id)) { node.ParentNodeId = parent_id; if (node.ParentNodeId == 0) return; }
        if (DockSettingsScanHex(line, ref i, "Window=", out ImGuiID window_id)) { node.ParentWindowId = window_id; if (node.ParentWindowId == 0) return; }
        if (node.ParentNodeId == 0)
        {
            if (DockSettingsScanTwoInts(line, ref i, "Pos=", out x, out y)) { node.Pos = new ImVec2ih(x, y); } else return;
            if (DockSettingsScanTwoInts(line, ref i, "Size=", out x, out y)) { node.Size = new ImVec2ih(x, y); } else return;
        }
        else
        {
            if (DockSettingsScanTwoInts(line, ref i, "SizeRef=", out x, out y)) { node.SizeRef = new ImVec2ih(x, y); }
        }
        if (DockSettingsScanChar(line, ref i, "Split=", out c)) { if (c == 'X') node.SplitAxis = (sbyte)ImGuiAxis_X; else if (c == 'Y') node.SplitAxis = (sbyte)ImGuiAxis_Y; }
        if (DockSettingsScanInt(line, ref i, "NoResize=", out x)) { if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoResize; }
        if (DockSettingsScanInt(line, ref i, "CentralNode=", out x)) { if (x != 0) node.Flags |= ImGuiDockNodeFlags_CentralNode; }
        if (DockSettingsScanInt(line, ref i, "NoTabBar=", out x)) { if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoTabBar; }
        if (DockSettingsScanInt(line, ref i, "HiddenTabBar=", out x)) { if (x != 0) node.Flags |= ImGuiDockNodeFlags_HiddenTabBar; }
        if (DockSettingsScanInt(line, ref i, "NoWindowMenuButton=", out x)) { if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoWindowMenuButton; }
        if (DockSettingsScanInt(line, ref i, "NoCloseButton=", out x)) { if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoCloseButton; }
        if (DockSettingsScanHex(line, ref i, "Selected=", out ImGuiID selected_tab_id)) { node.SelectedTabId = selected_tab_id; }
        if (node.ParentNodeId != 0)
        {
            ImGuiDockNodeSettings parent_settings = DockSettingsFindNodeSettings(ctx, node.ParentNodeId);
            if (parent_settings != null)
                node.Depth = (char)(parent_settings.Depth + 1);
        }
        ctx.DockContext.NodesSettings.push_back(node);
    }

    // static void DockSettingsHandler_DockNodeToSettings(ImGuiDockContext* dc, ImGuiDockNode* node, int depth)
    static void DockSettingsHandler_DockNodeToSettings(ImGuiDockContext dc, ImGuiDockNode node, int depth)
    {
        ImGuiDockNodeSettings node_settings = new ImGuiDockNodeSettings();
        Debug.Assert(depth < (1 << 8));
        node_settings.ID = node.ID;
        node_settings.ParentNodeId = node.ParentNode != null ? node.ParentNode.ID : 0;
        node_settings.ParentWindowId = (node.IsDockSpace() && node.HostWindow != null && node.HostWindow.ParentWindow != null) ? node.HostWindow.ParentWindow.ID : 0;
        node_settings.SelectedTabId = node.SelectedTabId;
        node_settings.SplitAxis = (sbyte)(node.IsSplitNode() ? node.SplitAxis : ImGuiAxis_None);
        node_settings.Depth = (char)depth;
        node_settings.Flags = node.LocalFlags & ImGuiDockNodeFlags_SavedFlagsMask_;
        node_settings.Pos = new ImVec2ih((short)node.Pos.x, (short)node.Pos.y);
        node_settings.Size = new ImVec2ih((short)node.Size.x, (short)node.Size.y);
        node_settings.SizeRef = new ImVec2ih((short)node.SizeRef.x, (short)node.SizeRef.y);
        dc.NodesSettings.push_back(node_settings);
        if (node.ChildNodes[0] != null)
            DockSettingsHandler_DockNodeToSettings(dc, node.ChildNodes[0], depth + 1);
        if (node.ChildNodes[1] != null)
            DockSettingsHandler_DockNodeToSettings(dc, node.ChildNodes[1], depth + 1);
    }

    // static void ImGui::DockSettingsHandler_WriteAll(ImGuiContext* ctx, ImGuiSettingsHandler* handler, ImGuiTextBuffer* buf)
    static void DockSettingsHandler_WriteAll(ImGuiContext ctx, ImGuiSettingsHandler handler, ImGuiTextBuffer buf)
    {
        ImGuiContext g = ctx;
        ImGuiDockContext dc = ctx.DockContext;
        if ((g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable) == 0)
            return;

        // Gather settings data
        // (unlike our windows settings, because nodes are always built we can do a full rewrite of the SettingsNode buffer)
        dc.NodesSettings.resize(0);
        dc.NodesSettings.reserve(dc.Nodes.Data.Size);
        for (int n = 0; n < dc.Nodes.Data.Size; n++)
            if (dc.Nodes.Data.Data[n].val_p is ImGuiDockNode node)
                if (node.IsRootNode())
                    DockSettingsHandler_DockNodeToSettings(dc, node, 0);

        int max_depth = 0;
        for (int node_n = 0; node_n < dc.NodesSettings.Size; node_n++)
            max_depth = System.Math.Max((int)dc.NodesSettings[node_n].Depth, max_depth);

        // Write to text buffer
        buf.appendf("[%s][Data]\n", handler.TypeName);
        for (int node_n = 0; node_n < dc.NodesSettings.Size; node_n++)
        {
            int line_start_pos = buf.size();
            ImGuiDockNodeSettings node_settings = dc.NodesSettings[node_n];
            buf.append(new string(' ', node_settings.Depth * 2));
            buf.append((node_settings.Flags & ImGuiDockNodeFlags_DockSpace) != 0 ? "DockSpace" : "DockNode ");  // Text align nodes to facilitate looking at .ini file
            buf.append(new string(' ', (max_depth - node_settings.Depth) * 2));
            buf.appendf(" ID=0x%08X", node_settings.ID);
            if (node_settings.ParentNodeId != 0)
            {
                buf.appendf(" Parent=0x%08X SizeRef=%d,%d", node_settings.ParentNodeId, node_settings.SizeRef.x, node_settings.SizeRef.y);
            }
            else
            {
                if (node_settings.ParentWindowId != 0)
                    buf.appendf(" Window=0x%08X", node_settings.ParentWindowId);
                buf.appendf(" Pos=%d,%d Size=%d,%d", node_settings.Pos.x, node_settings.Pos.y, node_settings.Size.x, node_settings.Size.y);
            }
            if (node_settings.SplitAxis != ImGuiAxis_None)
                buf.appendf(" Split=%c", node_settings.SplitAxis == ImGuiAxis_X ? 'X' : 'Y');
            if ((node_settings.Flags & ImGuiDockNodeFlags_NoResize) != 0)
                buf.appendf(" NoResize=1");
            if ((node_settings.Flags & ImGuiDockNodeFlags_CentralNode) != 0)
                buf.appendf(" CentralNode=1");
            if ((node_settings.Flags & ImGuiDockNodeFlags_NoTabBar) != 0)
                buf.appendf(" NoTabBar=1");
            if ((node_settings.Flags & ImGuiDockNodeFlags_HiddenTabBar) != 0)
                buf.appendf(" HiddenTabBar=1");
            if ((node_settings.Flags & ImGuiDockNodeFlags_NoWindowMenuButton) != 0)
                buf.appendf(" NoWindowMenuButton=1");
            if ((node_settings.Flags & ImGuiDockNodeFlags_NoCloseButton) != 0)
                buf.appendf(" NoCloseButton=1");
            if (node_settings.SelectedTabId != 0)
                buf.appendf(" Selected=0x%08X", node_settings.SelectedTabId);

            // [DEBUG] Include comments in the .ini file to ease debugging (this makes saving slower!)
            if (g.IO.ConfigDebugIniSettings)
            {
                ImGuiDockNode node = DockContextFindNodeByID(ctx, node_settings.ID);
                if (node != null)
                {
                    buf.append(new string(' ', System.Math.Max(2, (line_start_pos + 92) - buf.size())));     // Align everything
                    if (node.IsDockSpace() && node.HostWindow != null && node.HostWindow.ParentWindow != null)
                        buf.appendf(" ; in '%s'", node.HostWindow.ParentWindow.Name);
                    // Iterate settings so we can give info about windows that didn't exist during the session.
                    int contains_window = 0;
                    for (int i = 0; i < g.SettingsWindows.size(); i++)
                    {
                        ImGuiWindowSettings settings = g.SettingsWindows[i];
                        if (settings.DockId == node_settings.ID)
                        {
                            if (contains_window++ == 0)
                                buf.append(" ; contains ");
                            buf.appendf("'%s' ", settings.Name);
                        }
                    }
                }
            }

            buf.append("\n");
        }
        buf.append("\n");
    }

}
