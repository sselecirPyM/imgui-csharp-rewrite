// [SECTION] DOCKING
//-----------------------------------------------------------------------------
// Docking: Internal Types
// Docking: Forward Declarations
// Docking: ImGuiDockContext
// Docking: ImGuiDockContext Docking/Undocking functions
// Docking: ImGuiDockNode
// Docking: ImGuiDockNode Tree manipulation functions
// Docking: Public Functions (SetWindowDock, DockSpace, DockSpaceOverViewport)
// Docking: Builder Functions
// Docking: Begin/End Support Functions (called from Begin/End)
// Docking: Settings
//-----------------------------------------------------------------------------

//-----------------------------------------------------------------------------
// Typical Docking call flow: (root level is generally public API):
//-----------------------------------------------------------------------------
// - NewFrame()                               new dear imgui frame
//    | DockContextNewFrameUpdateUndocking()  - process queued undocking requests
//    | - DockContextProcessUndockWindow()    - process one window undocking request
//    | - DockContextProcessUndockNode()      - process one whole node undocking request
//    | DockContextNewFrameUpdateUndocking()  - process queue docking requests, create floating dock nodes
//    | - update g.HoveredDockNode            - [debug] update node hovered by mouse
//    | - DockContextProcessDock()            - process one docking request
//    | - DockNodeUpdate()
//    |   - DockNodeUpdateForRootNode()
//    |     - DockNodeUpdateFlagsAndCollapse()
//    |     - DockNodeFindInfo()
//    |   - destroy unused node or tab bar
//    |   - create dock node host window
//    |      - Begin() etc.
//    |   - DockNodeStartMouseMovingWindow()
//    |   - DockNodeTreeUpdatePosSize()
//    |   - DockNodeTreeUpdateSplitter()
//    |   - draw node background
//    |   - DockNodeUpdateTabBar()            - create/update tab bar for a docking node
//    |     - DockNodeAddTabBar()
//    |     - DockNodeWindowMenuUpdate()
//    |     - DockNodeCalcTabBarLayout()
//    |     - BeginTabBarEx()
//    |     - TabItemEx() calls
//    |     - EndTabBar()
//    |   - BeginDockableDragDropTarget()
//    |      - DockNodeUpdate()               - recurse into child nodes...
//-----------------------------------------------------------------------------
// - DockSpace()                              user submit a dockspace into a window
//    | Begin(Child)                          - create a child window
//    | DockNodeUpdate()                      - call main dock node update function
//    | End(Child)
//    | ItemSize()
//-----------------------------------------------------------------------------
// - Begin()
//    | BeginDocked()
//    | BeginDockableDragDropSource()
//    | BeginDockableDragDropTarget()
//    | - DockNodePreviewDockRender()
//-----------------------------------------------------------------------------
// - EndFrame()
//    | DockContextEndFrame()
//-----------------------------------------------------------------------------

//-----------------------------------------------------------------------------
// Docking: Internal Types
//-----------------------------------------------------------------------------
// - ImGuiDockRequestType
// - ImGuiDockRequest
// - ImGuiDockPreviewData
// - ImGuiDockNodeSettings
// - ImGuiDockContext
//-----------------------------------------------------------------------------

enum ImGuiDockRequestType
{
    ImGuiDockRequestType_None = 0,
    ImGuiDockRequestType_Dock,
    ImGuiDockRequestType_Undock,
    ImGuiDockRequestType_Split                  // Split is the same as Dock but without a DockPayload
};

struct ImGuiDockRequest
{
    ImGuiDockRequestType    Type;
    ImGuiWindow*            DockTargetWindow;   // Destination/Target Window to dock into (may be a loose window or a DockNode, might be NULL in which case DockTargetNode cannot be NULL)
    ImGuiDockNode*          DockTargetNode;     // Destination/Target Node to dock into
    ImGuiWindow*            DockPayload;        // Source/Payload window to dock (may be a loose window or a DockNode), [Optional]
    ImGuiDir                DockSplitDir;
    float                   DockSplitRatio;
    bool                    DockSplitOuter;
    ImGuiWindow*            UndockTargetWindow;
    ImGuiDockNode*          UndockTargetNode;

    ImGuiDockRequest()
    {
        Type = ImGuiDockRequestType_None;
        DockTargetWindow = DockPayload = UndockTargetWindow = NULL;
        DockTargetNode = UndockTargetNode = NULL;
        DockSplitDir = ImGuiDir_None;
        DockSplitRatio = 0.5f;
        DockSplitOuter = false;
    }
};

struct ImGuiDockPreviewData
{
    ImGuiDockNode   FutureNode;
    bool            IsDropAllowed;
    bool            IsCenterAvailable;
    bool            IsSidesAvailable;           // Hold your breath, grammar freaks..
    bool            IsSplitDirExplicit;         // Set when hovered the drop rect (vs. implicit SplitDir==None when hovered the window)
    ImGuiDockNode*  SplitNode;
    ImGuiDir        SplitDir;
    float           SplitRatio;
    ImRect          DropRectsDraw[ImGuiDir_COUNT + 1];  // May be slightly different from hit-testing drop rects used in DockNodeCalcDropRects()

    ImGuiDockPreviewData() : FutureNode(0) { IsDropAllowed = IsCenterAvailable = IsSidesAvailable = IsSplitDirExplicit = false; SplitNode = NULL; SplitDir = ImGuiDir_None; SplitRatio = 0.f; for (int n = 0; n < IM_COUNTOF(DropRectsDraw); n++) DropRectsDraw[n] = ImRect(+FLT_MAX, +FLT_MAX, -FLT_MAX, -FLT_MAX); }
};

// Persistent Settings data, stored contiguously in SettingsNodes (sizeof() ~32 bytes)
struct ImGuiDockNodeSettings
{
    ImGuiID             ID;
    ImGuiID             ParentNodeId;
    ImGuiID             ParentWindowId;
    ImGuiID             SelectedTabId;
    signed char         SplitAxis;
    char                Depth;
    ImGuiDockNodeFlags  Flags;                  // NB: We save individual flags one by one in ascii format (ImGuiDockNodeFlags_SavedFlagsMask_)
    ImVec2ih            Pos;
    ImVec2ih            Size;
    ImVec2ih            SizeRef;
    ImGuiDockNodeSettings() { memset((void*)this, 0, sizeof(*this)); SplitAxis = ImGuiAxis_None; }
};

//-----------------------------------------------------------------------------
// Docking: Forward Declarations
//-----------------------------------------------------------------------------

namespace ImGui
{
    // ImGuiDockContext
    static ImGuiDockNode*   DockContextAddNode(ImGuiContext* ctx, ImGuiID id);
    static void             DockContextRemoveNode(ImGuiContext* ctx, ImGuiDockNode* node, bool merge_sibling_into_parent_node);
    static void             DockContextDeleteNode(ImGuiContext* ctx, ImGuiDockNode* node);
    static void             DockContextQueueNotifyRemovedNode(ImGuiContext* ctx, ImGuiDockNode* node);
    static void             DockContextProcessDock(ImGuiContext* ctx, ImGuiDockRequest* req);
    static void             DockContextPruneUnusedSettingsNodes(ImGuiContext* ctx);
    static ImGuiDockNode*   DockContextBindNodeToWindow(ImGuiContext* ctx, ImGuiWindow* window);
    static void             DockContextBuildNodesFromSettings(ImGuiContext* ctx, ImGuiDockNodeSettings* node_settings_array, int node_settings_count);
    static void             DockContextBuildAddWindowsToNodes(ImGuiContext* ctx, ImGuiID root_id);                            // Use root_id==0 to add all

    // ImGuiDockNode
    static void             DockNodeAddWindow(ImGuiDockNode* node, ImGuiWindow* window, bool add_to_tab_bar);
    static void             DockNodeMoveWindows(ImGuiDockNode* dst_node, ImGuiDockNode* src_node);
    static void             DockNodeMoveChildNodes(ImGuiDockNode* dst_node, ImGuiDockNode* src_node);
    static ImGuiWindow*     DockNodeFindWindowByID(ImGuiDockNode* node, ImGuiID id);
    static void             DockNodeApplyPosSizeToWindows(ImGuiDockNode* node);
    static void             DockNodeRemoveWindow(ImGuiDockNode* node, ImGuiWindow* window, ImGuiID save_dock_id);
    static void             DockNodeHideHostWindow(ImGuiDockNode* node);
    static void             DockNodeUpdate(ImGuiDockNode* node);
    static void             DockNodeUpdateForRootNode(ImGuiDockNode* node);
    static void             DockNodeUpdateFlagsAndCollapse(ImGuiDockNode* node);
    static void             DockNodeUpdateHasCentralNodeChild(ImGuiDockNode* node);
    static void             DockNodeUpdateTabBar(ImGuiDockNode* node, ImGuiWindow* host_window);
    static void             DockNodeAddTabBar(ImGuiDockNode* node);
    static void             DockNodeRemoveTabBar(ImGuiDockNode* node);
    static void             DockNodeWindowMenuUpdate(ImGuiDockNode* node, ImGuiTabBar* tab_bar);
    static void             DockNodeUpdateVisibleFlag(ImGuiDockNode* node);
    static void             DockNodeStartMouseMovingWindow(ImGuiDockNode* node, ImGuiWindow* window);
    static bool             DockNodeIsDropAllowed(ImGuiWindow* host_window, ImGuiWindow* payload_window);
    static void             DockNodePreviewDockSetup(ImGuiWindow* host_window, ImGuiDockNode* host_node, ImGuiWindow* payload_window, ImGuiDockNode* payload_node, ImGuiDockPreviewData* preview_data, bool is_explicit_target, bool is_outer_docking);
    static void             DockNodePreviewDockRender(ImGuiWindow* host_window, ImGuiDockNode* host_node, ImGuiWindow* payload_window, const ImGuiDockPreviewData* preview_data);
    static void             DockNodeCalcTabBarLayout(const ImGuiDockNode* node, ImRect* out_title_rect, ImRect* out_tab_bar_rect, ImVec2* out_window_menu_button_pos, ImVec2* out_close_button_pos);
    static void             DockNodeCalcSplitRects(ImVec2& pos_old, ImVec2& size_old, ImVec2& pos_new, ImVec2& size_new, ImGuiDir dir, ImVec2 size_new_desired);
    static bool             DockNodeCalcDropRectsAndTestMousePos(const ImRect& parent, ImGuiDir dir, ImRect& out_draw, bool outer_docking, ImVec2* test_mouse_pos);
    static const char*      DockNodeGetHostWindowTitle(ImGuiDockNode* node, char* buf, int buf_size) { ImFormatString(buf, buf_size, "##DockNode_%02X", node->ID); return buf; }
    static int              DockNodeGetTabOrder(ImGuiWindow* window);

    // ImGuiDockNode tree manipulations
    static void             DockNodeTreeSplit(ImGuiContext* ctx, ImGuiDockNode* parent_node, ImGuiAxis split_axis, int split_first_child, float split_ratio, ImGuiDockNode* new_node);
    static void             DockNodeTreeMerge(ImGuiContext* ctx, ImGuiDockNode* parent_node, ImGuiDockNode* merge_lead_child);
    static void             DockNodeTreeUpdatePosSize(ImGuiDockNode* node, ImVec2 pos, ImVec2 size, ImGuiDockNode* only_write_to_single_node = NULL);
    static void             DockNodeTreeUpdateSplitter(ImGuiDockNode* node);
    static ImGuiDockNode*   DockNodeTreeFindVisibleNodeByPos(ImGuiDockNode* node, ImVec2 pos);
    static ImGuiDockNode*   DockNodeTreeFindFallbackLeafNode(ImGuiDockNode* node);

    // Settings
    static void             DockSettingsRenameNodeReferences(ImGuiID old_node_id, ImGuiID new_node_id);
    static void             DockSettingsRemoveNodeReferences(ImGuiID* node_ids, int node_ids_count);
    static ImGuiDockNodeSettings*   DockSettingsFindNodeSettings(ImGuiContext* ctx, ImGuiID node_id);
    static void             DockSettingsHandler_ClearAll(ImGuiContext*, ImGuiSettingsHandler*);
    static void             DockSettingsHandler_ApplyAll(ImGuiContext*, ImGuiSettingsHandler*);
    static void*            DockSettingsHandler_ReadOpen(ImGuiContext*, ImGuiSettingsHandler*, const char* name);
    static void             DockSettingsHandler_ReadLine(ImGuiContext*, ImGuiSettingsHandler*, void* entry, const char* line);
    static void             DockSettingsHandler_WriteAll(ImGuiContext* imgui_ctx, ImGuiSettingsHandler* handler, ImGuiTextBuffer* buf);
}

//-----------------------------------------------------------------------------
// Docking: ImGuiDockContext
//-----------------------------------------------------------------------------
// The lifetime model is different from the one of regular windows: we always create a ImGuiDockNode for each ImGuiDockNodeSettings,
// or we always hold the entire docking node tree. Nodes are frequently hidden, e.g. if the window(s) or child nodes they host are not active.
// At boot time only, we run a simple GC to remove nodes that have no references.
// Because dock node settings (which are small, contiguous structures) are always mirrored by their corresponding dock nodes (more complete structures),
// we can also very easily recreate the nodes from scratch given the settings data (this is what DockContextRebuild() does).
// This is convenient as docking reconfiguration can be implemented by mostly poking at the simpler settings data.
//-----------------------------------------------------------------------------
// - DockContextInitialize()
// - DockContextShutdown()
// - DockContextClearNodes()
// - DockContextRebuildNodes()
// - DockContextNewFrameUpdateUndocking()
// - DockContextNewFrameUpdateDocking()
// - DockContextEndFrame()
// - DockContextFindNodeByID()
// - DockContextBindNodeToWindow()
// - DockContextGenNodeID()
// - DockContextAddNode()
// - DockContextRemoveNode()
// - ImGuiDockContextPruneNodeData
// - DockContextPruneUnusedSettingsNodes()
// - DockContextBuildNodesFromSettings()
// - DockContextBuildAddWindowsToNodes()
//-----------------------------------------------------------------------------

void ImGui::DockContextInitialize(ImGuiContext* ctx)
{
    ImGuiContext& g = *ctx;

    // Add .ini handle for persistent docking data
    ImGuiSettingsHandler ini_handler;
    ini_handler.TypeName = "Docking";
    ini_handler.TypeHash = ImHashStr("Docking");
    ini_handler.ClearAllFn = DockSettingsHandler_ClearAll;
    ini_handler.ReadInitFn = DockSettingsHandler_ClearAll; // Also clear on read
    ini_handler.ReadOpenFn = DockSettingsHandler_ReadOpen;
    ini_handler.ReadLineFn = DockSettingsHandler_ReadLine;
    ini_handler.ApplyAllFn = DockSettingsHandler_ApplyAll;
    ini_handler.WriteAllFn = DockSettingsHandler_WriteAll;
    g.SettingsHandlers.push_back(ini_handler);

    g.DockNodeWindowMenuHandler = &DockNodeWindowMenuHandler_Default;
}

void ImGui::DockContextShutdown(ImGuiContext* ctx)
{
    ImGuiDockContext* dc = &ctx->DockContext;
    for (int n = 0; n < dc->Nodes.Data.Size; n++)
        if (ImGuiDockNode* node = (ImGuiDockNode*)dc->Nodes.Data[n].val_p)
            DockContextDeleteNode(ctx, node);
}

void ImGui::DockContextClearNodes(ImGuiContext* ctx, ImGuiID root_id, bool clear_settings_refs)
{
    IM_UNUSED(ctx);
    IM_ASSERT(ctx == GImGui);
    DockBuilderRemoveNodeDockedWindows(root_id, clear_settings_refs);
    DockBuilderRemoveNodeChildNodes(root_id);
}

// [DEBUG] This function also acts as a defacto test to make sure we can rebuild from scratch without a glitch
// (Different from DockSettingsHandler_ClearAll() + DockSettingsHandler_ApplyAll() because this reuses current settings!)
void ImGui::DockContextRebuildNodes(ImGuiContext* ctx)
{
    ImGuiContext& g = *ctx;
    ImGuiDockContext* dc = &ctx->DockContext;
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextRebuildNodes\n");
    SaveIniSettingsToMemory();
    ImGuiID root_id = 0; // Rebuild all
    DockContextClearNodes(ctx, root_id, false);
    DockContextBuildNodesFromSettings(ctx, dc->NodesSettings.Data, dc->NodesSettings.Size);
    DockContextBuildAddWindowsToNodes(ctx, root_id);
}

// Docking context update function, called by NewFrame()
void ImGui::DockContextNewFrameUpdateUndocking(ImGuiContext* ctx)
{
    ImGuiContext& g = *ctx;
    ImGuiDockContext* dc = &ctx->DockContext;
    if (!(g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable))
    {
        if (dc->Nodes.Data.Size > 0 || dc->Requests.Size > 0)
            DockContextClearNodes(ctx, 0, true);
        return;
    }

    // Setting NoSplit at runtime merges all nodes
    if (g.IO.ConfigDockingNoSplit)
        for (int n = 0; n < dc->Nodes.Data.Size; n++)
            if (ImGuiDockNode* node = (ImGuiDockNode*)dc->Nodes.Data[n].val_p)
                if (node->IsRootNode() && node->IsSplitNode())
                {
                    DockBuilderRemoveNodeChildNodes(node->ID);
                    //dc->WantFullRebuild = true;
                }

    // Process full rebuild
#if 0
    if (ImGui::IsKeyPressed(ImGui::GetKeyIndex(ImGuiKey_C)))
        dc->WantFullRebuild = true;
#endif
    if (dc->WantFullRebuild)
    {
        DockContextRebuildNodes(ctx);
        dc->WantFullRebuild = false;
    }

    // Process Undocking requests (we need to process them _before_ the UpdateMouseMovingWindowNewFrame call in NewFrame)
    for (ImGuiDockRequest& req : dc->Requests)
    {
        if (req.Type == ImGuiDockRequestType_Undock && req.UndockTargetWindow)
            DockContextProcessUndockWindow(ctx, req.UndockTargetWindow);
        else if (req.Type == ImGuiDockRequestType_Undock && req.UndockTargetNode)
            DockContextProcessUndockNode(ctx, req.UndockTargetNode);
    }
}

// Docking context update function, called by NewFrame()
void ImGui::DockContextNewFrameUpdateDocking(ImGuiContext* ctx)
{
    ImGuiContext& g = *ctx;
    ImGuiDockContext* dc = &ctx->DockContext;
    if (!(g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable))
        return;

    // [DEBUG] Store hovered dock node.
    // We could in theory use DockNodeTreeFindVisibleNodeByPos() on the root host dock node, but using ->DockNode is a good shortcut.
    // Note this is mostly a debug thing and isn't actually used for docking target, because docking involve more detailed filtering.
    g.DebugHoveredDockNode = NULL;
    if (ImGuiWindow* hovered_window = g.HoveredWindowUnderMovingWindow)
    {
        if (hovered_window->DockNodeAsHost)
            g.DebugHoveredDockNode = DockNodeTreeFindVisibleNodeByPos(hovered_window->DockNodeAsHost, g.IO.MousePos);
        else if (hovered_window->RootWindow->DockNode)
            g.DebugHoveredDockNode = hovered_window->RootWindow->DockNode;
    }

    // Process Docking requests
    for (ImGuiDockRequest& req : dc->Requests)
        if (req.Type == ImGuiDockRequestType_Dock)
            DockContextProcessDock(ctx, &req);
    dc->Requests.resize(0);

    // Create windows for each automatic docking nodes
    // We can have NULL pointers when we delete nodes, but because ID are recycled this should amortize nicely (and our node count will never be very high)
    for (int n = 0; n < dc->Nodes.Data.Size; n++)
        if (ImGuiDockNode* node = (ImGuiDockNode*)dc->Nodes.Data[n].val_p)
            if (node->IsFloatingNode())
                DockNodeUpdate(node);
}

void ImGui::DockContextEndFrame(ImGuiContext* ctx)
{
    // Draw backgrounds of node missing their window
    ImGuiContext& g = *ctx;
    ImGuiDockContext* dc = &g.DockContext;
    for (int n = 0; n < dc->Nodes.Data.Size; n++)
        if (ImGuiDockNode* node = (ImGuiDockNode*)dc->Nodes.Data[n].val_p)
            if (node->LastFrameActive == g.FrameCount && node->IsVisible && node->HostWindow && node->IsLeafNode() && !node->IsBgDrawnThisFrame)
            {
                ImRect bg_rect(node->Pos + ImVec2(0.0f, GetFrameHeight()), node->Pos + node->Size);
                ImDrawFlags bg_rounding_flags = CalcRoundingFlagsForRectInRect(bg_rect, node->HostWindow->Rect(), g.Style.DockingSeparatorSize);
                node->HostWindow->DrawList->ChannelsSetCurrent(DOCKING_HOST_DRAW_CHANNEL_BG);
                node->HostWindow->DrawList->AddRectFilled(bg_rect.Min, bg_rect.Max, node->LastBgColor, node->HostWindow->WindowRounding, bg_rounding_flags);
            }
}

ImGuiDockNode* ImGui::DockContextFindNodeByID(ImGuiContext* ctx, ImGuiID id)
{
    return (ImGuiDockNode*)ctx->DockContext.Nodes.GetVoidPtr(id);
}

ImGuiID ImGui::DockContextGenNodeID(ImGuiContext* ctx)
{
    // Generate an ID for new node (the exact ID value doesn't matter as long as it is not already used)
    // FIXME-OPT FIXME-DOCK: This is suboptimal, even if the node count is small enough not to be a worry.0
    // We should poke in ctx->Nodes to find a suitable ID faster. Even more so trivial that ctx->Nodes lookup is already sorted.
    ImGuiID id = 0x0001;
    while (DockContextFindNodeByID(ctx, id) != NULL)
        id++;
    return id;
}

static ImGuiDockNode* ImGui::DockContextAddNode(ImGuiContext* ctx, ImGuiID id)
{
    // Generate an ID for the new node (the exact ID value doesn't matter as long as it is not already used) and add the first window.
    ImGuiContext& g = *ctx;
    if (id == 0)
        id = DockContextGenNodeID(ctx);
    else
        IM_ASSERT(DockContextFindNodeByID(ctx, id) == NULL);

    // We don't set node->LastFrameAlive on construction. Nodes are always created at all time to reflect .ini settings!
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextAddNode 0x%08X\n", id);
    ImGuiDockNode* node = IM_NEW(ImGuiDockNode)(id);
    ctx->DockContext.Nodes.SetVoidPtr(node->ID, node);
    return node;
}

static void ImGui::DockContextRemoveNode(ImGuiContext* ctx, ImGuiDockNode* node, bool merge_sibling_into_parent_node)
{
    ImGuiContext& g = *ctx;

    IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextRemoveNode 0x%08X\n", node->ID);
    IM_ASSERT(DockContextFindNodeByID(ctx, node->ID) == node);
    IM_ASSERT(node->ChildNodes[0] == NULL && node->ChildNodes[1] == NULL);
    IM_ASSERT(node->Windows.Size == 0);

    if (node->HostWindow)
        node->HostWindow->DockNodeAsHost = NULL;

    ImGuiDockNode* parent_node = node->ParentNode;
    const bool merge = (merge_sibling_into_parent_node && parent_node != NULL);
    if (merge)
    {
        IM_ASSERT(parent_node->ChildNodes[0] == node || parent_node->ChildNodes[1] == node);
        ImGuiDockNode* sibling_node = (parent_node->ChildNodes[0] == node ? parent_node->ChildNodes[1] : parent_node->ChildNodes[0]);
        DockNodeTreeMerge(&g, parent_node, sibling_node);
    }
    else
    {
        for (int n = 0; parent_node && n < IM_COUNTOF(parent_node->ChildNodes); n++)
            if (parent_node->ChildNodes[n] == node)
                node->ParentNode->ChildNodes[n] = NULL;
        DockContextDeleteNode(ctx, node);
    }
}

// Raw-ish delete
static void ImGui::DockContextDeleteNode(ImGuiContext* ctx, ImGuiDockNode* node)
{
    ImGuiDockContext* dc = &ctx->DockContext;
    if (node->TabBar)
        IM_DELETE(node->TabBar);
    node->TabBar = NULL;
    dc->Nodes.SetVoidPtr(node->ID, NULL);
    IM_DELETE(node);
}

static int IMGUI_CDECL DockNodeComparerDepthMostFirst(const void* lhs, const void* rhs)
{
    const ImGuiDockNode* a = *(const ImGuiDockNode* const*)lhs;
    const ImGuiDockNode* b = *(const ImGuiDockNode* const*)rhs;
    return ImGui::DockNodeGetDepth(b) - ImGui::DockNodeGetDepth(a);
}

// Pre C++0x doesn't allow us to use a function-local type (without linkage) as template parameter, so we moved this here.
struct ImGuiDockContextPruneNodeData
{
    int         CountWindows, CountChildWindows, CountChildNodes;
    ImGuiID     RootId;
    ImGuiDockContextPruneNodeData() { CountWindows = CountChildWindows = CountChildNodes = 0; RootId = 0; }
};

// Garbage collect unused nodes (run once at init time)
static void ImGui::DockContextPruneUnusedSettingsNodes(ImGuiContext* ctx)
{
    ImGuiContext& g = *ctx;
    ImGuiDockContext* dc = &ctx->DockContext;
    IM_ASSERT(g.Windows.Size == 0);

    ImPool<ImGuiDockContextPruneNodeData> pool;
    pool.Reserve(dc->NodesSettings.Size);

    // Count child nodes and compute RootID
    for (int settings_n = 0; settings_n < dc->NodesSettings.Size; settings_n++)
    {
        ImGuiDockNodeSettings* settings = &dc->NodesSettings[settings_n];
        if (pool.GetByKey(settings->ID) != 0)
        {
            settings->ID = 0; // Duplicate
            continue;
        }
        ImGuiDockContextPruneNodeData* parent_data = settings->ParentNodeId ? pool.GetByKey(settings->ParentNodeId) : 0;
        pool.GetOrAddByKey(settings->ID)->RootId = parent_data ? parent_data->RootId : settings->ID;
        if (settings->ParentNodeId)
            pool.GetOrAddByKey(settings->ParentNodeId)->CountChildNodes++;
    }

    // Count reference to dock ids from dockspaces
    // We track the 'auto-DockNode <- manual-Window <- manual-DockSpace' in order to avoid 'auto-DockNode' being ditched by DockContextPruneUnusedSettingsNodes()
    for (int settings_n = 0; settings_n < dc->NodesSettings.Size; settings_n++)
    {
        ImGuiDockNodeSettings* settings = &dc->NodesSettings[settings_n];
        if (settings->ParentWindowId != 0)
            if (ImGuiWindowSettings* window_settings = FindWindowSettingsByID(settings->ParentWindowId))
                if (window_settings->DockId)
                    if (ImGuiDockContextPruneNodeData* data = pool.GetByKey(window_settings->DockId))
                        data->CountChildNodes++;
    }

    // Count reference to dock ids from window settings
    // We guard against the possibility of an invalid .ini file (RootID may point to a missing node)
    for (ImGuiWindowSettings* settings = g.SettingsWindows.begin(); settings != NULL; settings = g.SettingsWindows.next_chunk(settings))
        if (ImGuiID dock_id = settings->DockId)
            if (ImGuiDockContextPruneNodeData* data = pool.GetByKey(dock_id))
            {
                data->CountWindows++;
                if (ImGuiDockContextPruneNodeData* data_root = (data->RootId == dock_id) ? data : pool.GetByKey(data->RootId))
                    data_root->CountChildWindows++;
            }

    // Prune
    for (int settings_n = 0; settings_n < dc->NodesSettings.Size; settings_n++)
    {
        ImGuiDockNodeSettings* settings = &dc->NodesSettings[settings_n];
        ImGuiDockContextPruneNodeData* data = pool.GetByKey(settings->ID);
        if (data == NULL || data->CountWindows > 1)
            continue;
        ImGuiDockContextPruneNodeData* data_root = (settings->ID == data->RootId) ? data : pool.GetByKey(data->RootId);
        ImGuiDockContextPruneNodeData* data_parent = settings->ParentNodeId ? pool.GetByKey(settings->ParentNodeId) : NULL;

        bool remove = false;
        remove |= (data->CountWindows == 1 && settings->ParentNodeId == 0 && data->CountChildNodes == 0 && !(settings->Flags & ImGuiDockNodeFlags_CentralNode));  // Floating root node with only 1 window
        remove |= (data->CountWindows == 0 && settings->ParentNodeId == 0 && data->CountChildNodes == 0); // Leaf nodes with 0 window
        remove |= (data_root == NULL || data_root->CountChildWindows == 0);
        if (remove)
        {
            IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextPruneUnusedSettingsNodes: Prune 0x%08X\n", settings->ID);
            DockSettingsRemoveNodeReferences(&settings->ID, 1);
            settings->ID = 0;
        }
        else if (data_parent && data_parent->CountChildNodes == 1)
        {
            IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextPruneUnusedSettingsNodes: Merge 0x%08X->0X%08X\n", settings->ID, settings->ParentNodeId);
            DockSettingsRenameNodeReferences(settings->ID, settings->ParentNodeId);
            settings->ID = 0;
        }
    }
}

static void ImGui::DockContextBuildNodesFromSettings(ImGuiContext* ctx, ImGuiDockNodeSettings* node_settings_array, int node_settings_count)
{
    // Build nodes
    ImGuiContext& g = *ctx; IM_UNUSED(g);
    for (int node_n = 0; node_n < node_settings_count; node_n++)
    {
        ImGuiDockNodeSettings* settings = &node_settings_array[node_n];
        if (settings->ID == 0)
            continue;
        if (DockContextFindNodeByID(ctx, settings->ID) != NULL)
        {
            IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextBuildNodesFromSettings: skip duplicate node 0x%08X\n", settings->ID);
            continue;
        }
        ImGuiDockNode* node = DockContextAddNode(ctx, settings->ID);
        node->ParentNode = settings->ParentNodeId ? DockContextFindNodeByID(ctx, settings->ParentNodeId) : NULL;
        node->Pos = ImVec2(settings->Pos.x, settings->Pos.y);
        node->Size = ImVec2(settings->Size.x, settings->Size.y);
        node->SizeRef = ImVec2(settings->SizeRef.x, settings->SizeRef.y);
        node->AuthorityForPos = node->AuthorityForSize = node->AuthorityForViewport = ImGuiDataAuthority_DockNode;
        if (node->ParentNode && node->ParentNode->ChildNodes[0] == NULL)
            node->ParentNode->ChildNodes[0] = node;
        else if (node->ParentNode && node->ParentNode->ChildNodes[1] == NULL)
            node->ParentNode->ChildNodes[1] = node;
        node->SelectedTabId = settings->SelectedTabId;
        node->SplitAxis = (ImGuiAxis)settings->SplitAxis;
        node->SetLocalFlags(settings->Flags & ImGuiDockNodeFlags_SavedFlagsMask_);

        // Bind host window immediately if it already exist (in case of a rebuild)
        // This is useful as the RootWindowForTitleBarHighlight links necessary to highlight the currently focused node requires node->HostWindow to be set.
        char host_window_title[20];
        ImGuiDockNode* root_node = DockNodeGetRootNode(node);
        node->HostWindow = FindWindowByName(DockNodeGetHostWindowTitle(root_node, host_window_title, IM_COUNTOF(host_window_title)));
    }
}

void ImGui::DockContextBuildAddWindowsToNodes(ImGuiContext* ctx, ImGuiID root_id)
{
    // Rebind all windows to nodes (they can also lazily rebind but we'll have a visible glitch during the first frame)
    ImGuiContext& g = *ctx;
    for (ImGuiWindow* window : g.Windows)
    {
        if (window->DockId == 0 || window->LastFrameActive < g.FrameCount - 1)
            continue;
        if (window->DockNode != NULL)
            continue;

        ImGuiDockNode* node = DockContextFindNodeByID(ctx, window->DockId);
        IM_ASSERT(node != NULL);   // This should have been called after DockContextBuildNodesFromSettings()
        if (root_id == 0 || DockNodeGetRootNode(node)->ID == root_id)
            DockNodeAddWindow(node, window, true);
    }
}

//-----------------------------------------------------------------------------
// Docking: ImGuiDockContext Docking/Undocking functions
//-----------------------------------------------------------------------------
// - DockContextQueueDock()
// - DockContextQueueUndockWindow()
// - DockContextQueueUndockNode()
// - DockContextQueueNotifyRemovedNode()
// - DockContextProcessDock()
// - DockContextProcessUndockWindow()
// - DockContextProcessUndockNode()
// - DockContextCalcDropPosForDocking()
//-----------------------------------------------------------------------------

void ImGui::DockContextQueueDock(ImGuiContext* ctx, ImGuiWindow* target, ImGuiDockNode* target_node, ImGuiWindow* payload, ImGuiDir split_dir, float split_ratio, bool split_outer)
{
    IM_ASSERT(target != payload);
    ImGuiDockRequest req;
    req.Type = ImGuiDockRequestType_Dock;
    req.DockTargetWindow = target;
    req.DockTargetNode = target_node;
    req.DockPayload = payload;
    req.DockSplitDir = split_dir;
    req.DockSplitRatio = split_ratio;
    req.DockSplitOuter = split_outer;
    ctx->DockContext.Requests.push_back(req);
}

void ImGui::DockContextQueueUndockWindow(ImGuiContext* ctx, ImGuiWindow* window)
{
    ImGuiDockRequest req;
    req.Type = ImGuiDockRequestType_Undock;
    req.UndockTargetWindow = window;
    ctx->DockContext.Requests.push_back(req);
}

void ImGui::DockContextQueueUndockNode(ImGuiContext* ctx, ImGuiDockNode* node)
{
    ImGuiDockRequest req;
    req.Type = ImGuiDockRequestType_Undock;
    req.UndockTargetNode = node;
    ctx->DockContext.Requests.push_back(req);
}

void ImGui::DockContextQueueNotifyRemovedNode(ImGuiContext* ctx, ImGuiDockNode* node)
{
    ImGuiDockContext* dc = &ctx->DockContext;
    for (ImGuiDockRequest& req : dc->Requests)
        if (req.DockTargetNode == node)
            req.Type = ImGuiDockRequestType_None;
}

void ImGui::DockContextProcessDock(ImGuiContext* ctx, ImGuiDockRequest* req)
{
    IM_ASSERT((req->Type == ImGuiDockRequestType_Dock && req->DockPayload != NULL) || (req->Type == ImGuiDockRequestType_Split && req->DockPayload == NULL));
    IM_ASSERT(req->DockTargetWindow != NULL || req->DockTargetNode != NULL);

    ImGuiContext& g = *ctx;
    IM_UNUSED(g);

    ImGuiWindow* payload_window = req->DockPayload;     // Optional
    ImGuiWindow* target_window = req->DockTargetWindow;
    ImGuiDockNode* node = req->DockTargetNode;
    if (payload_window)
        IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextProcessDock node 0x%08X target '%s' dock window '%s', split_dir %d\n", node ? node->ID : 0, target_window ? target_window->Name : "NULL", payload_window->Name, req->DockSplitDir);
    else
        IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextProcessDock node 0x%08X, split_dir %d\n", node ? node->ID : 0, req->DockSplitDir);

    // Decide which Tab will be selected at the end of the operation
    ImGuiID next_selected_id = 0;
    ImGuiDockNode* payload_node = NULL;
    if (payload_window)
    {
        payload_node = payload_window->DockNodeAsHost;
        payload_window->DockNodeAsHost = NULL; // Important to clear this as the node will have its life as a child which might be merged/deleted later.
        if (payload_node && payload_node->IsLeafNode())
            next_selected_id = payload_node->TabBar->NextSelectedTabId ? payload_node->TabBar->NextSelectedTabId : payload_node->TabBar->SelectedTabId;
        if (payload_node == NULL)
            next_selected_id = payload_window->TabId;
    }

    // FIXME-DOCK: When we are trying to dock an existing single-window node into a loose window, transfer Node ID as well
    // When processing an interactive split, usually LastFrameAlive will be < g.FrameCount. But DockBuilder operations can make it ==.
    if (node)
        IM_ASSERT(node->LastFrameAlive <= g.FrameCount);
    if (node && target_window && node == target_window->DockNodeAsHost)
        IM_ASSERT(node->Windows.Size > 0 || node->IsSplitNode() || node->IsCentralNode());

    // Create new node and add existing window to it
    if (node == NULL)
    {
        node = DockContextAddNode(ctx, 0);
        node->Pos = target_window->Pos;
        node->Size = target_window->Size;
        if (target_window->DockNodeAsHost == NULL)
        {
            DockNodeAddWindow(node, target_window, true);
            node->TabBar->Tabs[0].Flags &= ~ImGuiTabItemFlags_Unsorted;
            target_window->DockIsActive = true;
        }
    }

    ImGuiDir split_dir = req->DockSplitDir;
    if (split_dir != ImGuiDir_None)
    {
        // Split into two, one side will be our payload node unless we are dropping a loose window
        const ImGuiAxis split_axis = (split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Right) ? ImGuiAxis_X : ImGuiAxis_Y;
        const int split_inheritor_child_idx = (split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? 1 : 0; // Current contents will be moved to the opposite side
        const float split_ratio = req->DockSplitRatio;
        DockNodeTreeSplit(ctx, node, split_axis, split_inheritor_child_idx, split_ratio, payload_node);  // payload_node may be NULL here!
        ImGuiDockNode* new_node = node->ChildNodes[split_inheritor_child_idx ^ 1];
        new_node->HostWindow = node->HostWindow;
        node = new_node;
    }
    node->SetLocalFlags(node->LocalFlags & ~ImGuiDockNodeFlags_HiddenTabBar);

    if (node != payload_node)
    {
        // Create tab bar before we call DockNodeMoveWindows (which would attempt to move the old tab-bar, which would lead us to payload tabs wrongly appearing before target tabs!)
        if (node->Windows.Size > 0 && node->TabBar == NULL)
        {
            DockNodeAddTabBar(node);
            for (int n = 0; n < node->Windows.Size; n++)
                TabBarAddTab(node->TabBar, ImGuiTabItemFlags_None, node->Windows[n]);
        }

        if (payload_node != NULL)
        {
            // Transfer full payload node (with 1+ child windows or child nodes)
            if (payload_node->IsSplitNode())
            {
                if (node->Windows.Size > 0)
                {
                    // We can dock a split payload into a node that already has windows _only_ if our payload is a node tree with a single visible node.
                    // In this situation, we move the windows of the target node into the currently visible node of the payload.
                    // This allows us to preserve some of the underlying dock tree settings nicely.
                    IM_ASSERT(payload_node->OnlyNodeWithWindows != NULL); // The docking should have been blocked by DockNodePreviewDockSetup() early on and never submitted.
                    ImGuiDockNode* visible_node = payload_node->OnlyNodeWithWindows;
                    if (visible_node->TabBar)
                        IM_ASSERT(visible_node->TabBar->Tabs.Size > 0);
                    DockNodeMoveWindows(node, visible_node);
                    DockNodeMoveWindows(visible_node, node);
                    DockSettingsRenameNodeReferences(node->ID, visible_node->ID);
                }
                if (node->IsCentralNode())
                {
                    // Central node property needs to be moved to a leaf node, pick the last focused one.
                    // FIXME-DOCK: If we had to transfer other flags here, what would the policy be?
                    ImGuiDockNode* last_focused_node = DockContextFindNodeByID(ctx, payload_node->LastFocusedNodeId);
                    IM_ASSERT(last_focused_node != NULL);
                    ImGuiDockNode* last_focused_root_node = DockNodeGetRootNode(last_focused_node);
                    IM_ASSERT(last_focused_root_node == DockNodeGetRootNode(payload_node));
                    last_focused_node->SetLocalFlags(last_focused_node->LocalFlags | ImGuiDockNodeFlags_CentralNode);
                    node->SetLocalFlags(node->LocalFlags & ~ImGuiDockNodeFlags_CentralNode);
                    last_focused_root_node->CentralNode = last_focused_node;
                }

                IM_ASSERT(node->Windows.Size == 0);
                DockNodeMoveChildNodes(node, payload_node);
            }
            else
            {
                const ImGuiID payload_dock_id = payload_node->ID;
                DockNodeMoveWindows(node, payload_node);
                DockSettingsRenameNodeReferences(payload_dock_id, node->ID);
            }
            DockContextRemoveNode(ctx, payload_node, true);
        }
        else if (payload_window)
        {
            // Transfer single window
            const ImGuiID payload_dock_id = payload_window->DockId;
            node->VisibleWindow = payload_window;
            DockNodeAddWindow(node, payload_window, true);
            if (payload_dock_id != 0)
                DockSettingsRenameNodeReferences(payload_dock_id, node->ID);
        }
    }
    else
    {
        // When docking a floating single window node we want to reevaluate auto-hiding of the tab bar
        node->WantHiddenTabBarUpdate = true;
    }

    // Update selection immediately
    if (ImGuiTabBar* tab_bar = node->TabBar)
        tab_bar->NextSelectedTabId = next_selected_id;
    MarkIniSettingsDirty();
}

// Problem:
//   Undocking a large (~full screen) window would leave it so large that the bottom right sizing corner would more
//   than likely be off the screen and the window would be hard to resize to fit on screen. This can be particularly problematic
//   with 'ConfigWindowsMoveFromTitleBarOnly=true' and/or with 'ConfigWindowsResizeFromEdges=false' as well (the later can be
//   due to missing ImGuiBackendFlags_HasMouseCursors backend flag).
// Solution:
//   When undocking a window we currently force its maximum size to 90% of the host viewport or monitor.
// Reevaluate this when we implement preserving docked/undocked size ("docking_wip/undocked_size" branch).
static ImVec2 FixLargeWindowsWhenUndocking(const ImVec2& size, ImGuiViewport* ref_viewport)
{
    if (ref_viewport == NULL)
        return size;

    ImGuiContext& g = *GImGui;
    ImVec2 max_size = ImTrunc(ref_viewport->WorkSize * 0.90f);
    if (g.ConfigFlagsCurrFrame & ImGuiConfigFlags_ViewportsEnable)
    {
        const ImGuiPlatformMonitor* monitor = ImGui::GetViewportPlatformMonitor(ref_viewport);
        max_size = ImTrunc(monitor->WorkSize * 0.90f);
    }
    return ImMin(size, max_size);
}

void ImGui::DockContextProcessUndockWindow(ImGuiContext* ctx, ImGuiWindow* window, bool clear_persistent_docking_ref)
{
    ImGuiContext& g = *ctx;
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextProcessUndockWindow window '%s', clear_persistent_docking_ref = %d\n", window->Name, clear_persistent_docking_ref);
    if (window->DockNode)
        DockNodeRemoveWindow(window->DockNode, window, clear_persistent_docking_ref ? 0 : window->DockId);
    else
        window->DockId = 0;
    window->Collapsed = false;
    window->DockIsActive = false;
    window->DockNodeIsVisible = window->DockTabIsVisible = false;
    window->Size = window->SizeFull = FixLargeWindowsWhenUndocking(window->SizeFull, window->Viewport);

    MarkIniSettingsDirty();
}

void ImGui::DockContextProcessUndockNode(ImGuiContext* ctx, ImGuiDockNode* node)
{
    ImGuiContext& g = *ctx;
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockContextProcessUndockNode node %08X\n", node->ID);
    IM_ASSERT(node->IsLeafNode());
    IM_ASSERT(node->Windows.Size >= 1);

    if (node->IsRootNode() || node->IsCentralNode())
    {
        // In the case of a root node or central node, the node will have to stay in place. Create a new node to receive the payload.
        ImGuiDockNode* new_node = DockContextAddNode(ctx, 0);
        new_node->Pos = node->Pos;
        new_node->Size = node->Size;
        new_node->SizeRef = node->SizeRef;
        DockNodeMoveWindows(new_node, node);
        DockSettingsRenameNodeReferences(node->ID, new_node->ID);
        node = new_node;
    }
    else
    {
        // Otherwise extract our node and merge our sibling back into the parent node.
        IM_ASSERT(node->ParentNode->ChildNodes[0] == node || node->ParentNode->ChildNodes[1] == node);
        int index_in_parent = (node->ParentNode->ChildNodes[0] == node) ? 0 : 1;
        node->ParentNode->ChildNodes[index_in_parent] = NULL;
        DockNodeTreeMerge(ctx, node->ParentNode, node->ParentNode->ChildNodes[index_in_parent ^ 1]);
        node->ParentNode->AuthorityForViewport = ImGuiDataAuthority_Window; // The node that stays in place keeps the viewport, so our newly dragged out node will create a new viewport
        node->ParentNode = NULL;
    }
    for (ImGuiWindow* window : node->Windows)
    {
        window->Flags &= ~ImGuiWindowFlags_ChildWindow;
        if (window->ParentWindow)
            window->ParentWindow->DC.ChildWindows.find_erase(window);
        UpdateWindowParentAndRootLinks(window, window->Flags, NULL);
    }
    node->AuthorityForPos = node->AuthorityForSize = ImGuiDataAuthority_DockNode;
    node->Size = FixLargeWindowsWhenUndocking(node->Size, node->Windows[0]->Viewport);
    node->WantMouseMove = true;
    MarkIniSettingsDirty();
}

// This is mostly used for automation.
bool ImGui::DockContextCalcDropPosForDocking(ImGuiWindow* target, ImGuiDockNode* target_node, ImGuiWindow* payload_window, ImGuiDockNode* payload_node, ImGuiDir split_dir, bool split_outer, ImVec2* out_pos)
{
    if (target != NULL && target_node == NULL)
        target_node = target->DockNode;

    // In DockNodePreviewDockSetup() for a root central node instead of showing both "inner" and "outer" drop rects
    // (which would be functionally identical) we only show the outer one. Reflect this here.
    if (target_node && target_node->ParentNode == NULL && target_node->IsCentralNode() && split_dir != ImGuiDir_None)
        split_outer = true;
    ImGuiDockPreviewData split_data;
    DockNodePreviewDockSetup(target, target_node, payload_window, payload_node, &split_data, false, split_outer);
    if (split_data.DropRectsDraw[split_dir+1].IsInverted())
        return false;
    *out_pos = split_data.DropRectsDraw[split_dir+1].GetCenter();
    return true;
}

//-----------------------------------------------------------------------------
// Docking: ImGuiDockNode
//-----------------------------------------------------------------------------
// - DockNodeGetTabOrder()
// - DockNodeAddWindow()
// - DockNodeRemoveWindow()
// - DockNodeMoveChildNodes()
// - DockNodeMoveWindows()
// - DockNodeApplyPosSizeToWindows()
// - DockNodeHideHostWindow()
// - ImGuiDockNodeFindInfoResults
// - DockNodeFindInfo()
// - DockNodeFindWindowByID()
// - DockNodeUpdateFlagsAndCollapse()
// - DockNodeUpdateHasCentralNodeFlag()
// - DockNodeUpdateVisibleFlag()
// - DockNodeStartMouseMovingWindow()
// - DockNodeUpdate()
// - DockNodeUpdateWindowMenu()
// - DockNodeBeginAmendTabBar()
// - DockNodeEndAmendTabBar()
// - DockNodeUpdateTabBar()
// - DockNodeAddTabBar()
// - DockNodeRemoveTabBar()
// - DockNodeIsDropAllowedOne()
// - DockNodeIsDropAllowed()
// - DockNodeCalcTabBarLayout()
// - DockNodeCalcSplitRects()
// - DockNodeCalcDropRectsAndTestMousePos()
// - DockNodePreviewDockSetup()
// - DockNodePreviewDockRender()
//-----------------------------------------------------------------------------

ImGuiDockNode::ImGuiDockNode(ImGuiID id)
{
    ID = id;
    SharedFlags = LocalFlags = LocalFlagsInWindows = MergedFlags = ImGuiDockNodeFlags_None;
    ParentNode = ChildNodes[0] = ChildNodes[1] = NULL;
    TabBar = NULL;
    SplitAxis = ImGuiAxis_None;

    State = ImGuiDockNodeState_Unknown;
    LastBgColor = IM_COL32_WHITE;
    HostWindow = VisibleWindow = NULL;
    CentralNode = OnlyNodeWithWindows = NULL;
    CountNodeWithWindows = 0;
    LastFrameAlive = LastFrameActive = LastFrameFocused = -1;
    LastFocusedNodeId = 0;
    SelectedTabId = 0;
    WantCloseTabId = 0;
    RefViewportId = 0;
    AuthorityForPos = AuthorityForSize = ImGuiDataAuthority_DockNode;
    AuthorityForViewport = ImGuiDataAuthority_Auto;
    IsVisible = true;
    IsFocused = HasCloseButton = HasWindowMenuButton = HasCentralNodeChild = false;
    IsBgDrawnThisFrame = false;
    WantCloseAll = WantLockSizeOnce = WantMouseMove = WantHiddenTabBarUpdate = WantHiddenTabBarToggle = false;
}

ImGuiDockNode::~ImGuiDockNode()
{
    IM_ASSERT(TabBar == NULL);
    ChildNodes[0] = ChildNodes[1] = NULL;
}

int ImGui::DockNodeGetTabOrder(ImGuiWindow* window)
{
    ImGuiTabBar* tab_bar = window->DockNode->TabBar;
    if (tab_bar == NULL)
        return -1;
    ImGuiTabItem* tab = TabBarFindTabByID(tab_bar, window->TabId);
    return tab ? TabBarGetTabOrder(tab_bar, tab) : -1;
}

static void DockNodeHideWindowDuringHostWindowCreation(ImGuiWindow* window)
{
    window->Hidden = true;
    window->HiddenFramesCanSkipItems = window->Active ? 1 : 2;
}

static void ImGui::DockNodeAddWindow(ImGuiDockNode* node, ImGuiWindow* window, bool add_to_tab_bar)
{
    ImGuiContext& g = *GImGui; (void)g;
    if (window->DockNode)
    {
        // Can overwrite an existing window->DockNode (e.g. pointing to a disabled DockSpace node)
        IM_ASSERT(window->DockNode->ID != node->ID);
        DockNodeRemoveWindow(window->DockNode, window, 0);
    }
    IM_ASSERT(window->DockNode == NULL || window->DockNodeAsHost == NULL);
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockNodeAddWindow node 0x%08X window '%s'\n", node->ID, window->Name);

    // If more than 2 windows appeared on the same frame leading to the creation of a new hosting window,
    // we'll hide windows until the host window is ready. Hide the 1st window after its been output (so it is not visible for one frame).
    // We will call DockNodeHideWindowDuringHostWindowCreation() on ourselves in Begin()
    if (node->HostWindow == NULL && node->Windows.Size == 1 && node->Windows[0]->WasActive == false)
        DockNodeHideWindowDuringHostWindowCreation(node->Windows[0]);

    node->Windows.push_back(window);
    node->WantHiddenTabBarUpdate = true;
    window->DockNode = node;
    window->DockId = node->ID;
    window->DockIsActive = (node->Windows.Size > 1);
    window->DockTabWantClose = false;

    // When reactivating a node with one or two loose window, the window pos/size/viewport are authoritative over the node storage.
    // In particular it is important we init the viewport from the first window so we don't create two viewports and drop one.
    if (node->HostWindow == NULL && node->IsFloatingNode())
    {
        if (node->AuthorityForPos == ImGuiDataAuthority_Auto)
            node->AuthorityForPos = ImGuiDataAuthority_Window;
        if (node->AuthorityForSize == ImGuiDataAuthority_Auto)
            node->AuthorityForSize = ImGuiDataAuthority_Window;
        if (node->AuthorityForViewport == ImGuiDataAuthority_Auto)
            node->AuthorityForViewport = ImGuiDataAuthority_Window;
    }

    // Add to tab bar if requested
    if (add_to_tab_bar)
    {
        if (node->TabBar == NULL)
        {
            DockNodeAddTabBar(node);
            node->TabBar->SelectedTabId = node->TabBar->NextSelectedTabId = node->SelectedTabId;

            // Add existing windows
            for (int n = 0; n < node->Windows.Size - 1; n++)
                TabBarAddTab(node->TabBar, ImGuiTabItemFlags_None, node->Windows[n]);
        }
        TabBarAddTab(node->TabBar, ImGuiTabItemFlags_Unsorted, window);
    }

    DockNodeUpdateVisibleFlag(node);

    // Update this without waiting for the next time we Begin() in the window, so our host window will have the proper title bar color on its first frame.
    if (node->HostWindow)
        UpdateWindowParentAndRootLinks(window, window->Flags | ImGuiWindowFlags_ChildWindow, node->HostWindow);
}

static void ImGui::DockNodeRemoveWindow(ImGuiDockNode* node, ImGuiWindow* window, ImGuiID save_dock_id)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(window->DockNode == node);
    //IM_ASSERT(window->RootWindowDockTree == node->HostWindow);
    //IM_ASSERT(window->LastFrameActive < g.FrameCount);    // We may call this from Begin()
    IM_ASSERT(save_dock_id == 0 || save_dock_id == node->ID);
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockNodeRemoveWindow node 0x%08X window '%s'\n", node->ID, window->Name);

    window->DockNode = NULL;
    window->DockIsActive = window->DockTabWantClose = false;
    window->DockId = save_dock_id;
    window->Flags &= ~ImGuiWindowFlags_ChildWindow;
    if (window->ParentWindow)
        window->ParentWindow->DC.ChildWindows.find_erase(window);
    UpdateWindowParentAndRootLinks(window, window->Flags, NULL); // Update immediately

    if (node->HostWindow && node->HostWindow->ViewportOwned)
    {
        // When undocking from a user interaction this will always run in NewFrame() and have not much effect.
        // But mid-frame, if we clear viewport we need to mark window as hidden as well.
        window->Viewport = NULL;
        window->ViewportId = 0;
        window->ViewportOwned = false;
        window->Hidden = true;
    }

    // Remove window
    bool erased = false;
    for (int n = 0; n < node->Windows.Size; n++)
        if (node->Windows[n] == window)
        {
            node->Windows.erase(node->Windows.Data + n);
            erased = true;
            break;
        }
    if (!erased)
        IM_ASSERT(erased);
    if (node->VisibleWindow == window)
        node->VisibleWindow = NULL;

    // Remove tab and possibly tab bar
    node->WantHiddenTabBarUpdate = true;
    if (node->TabBar)
    {
        TabBarRemoveTab(node->TabBar, window->TabId);
        const int tab_count_threshold_for_tab_bar = node->IsCentralNode() ? 1 : 2;
        if (node->Windows.Size < tab_count_threshold_for_tab_bar)
            DockNodeRemoveTabBar(node);
    }

    if (node->Windows.Size == 0 && !node->IsCentralNode() && !node->IsDockSpace() && window->DockId != node->ID)
    {
        // Automatic dock node delete themselves if they are not holding at least one tab
        DockContextRemoveNode(&g, node, true);
        return;
    }

    if (node->Windows.Size == 1 && !node->IsCentralNode() && node->HostWindow)
    {
        ImGuiWindow* remaining_window = node->Windows[0];
        // Note: we used to transport viewport ownership here.
        remaining_window->Collapsed = node->HostWindow->Collapsed;
    }

    // Update visibility immediately is required so the DockNodeUpdateRemoveInactiveChilds() processing can reflect changes up the tree
    DockNodeUpdateVisibleFlag(node);
}

static void ImGui::DockNodeMoveChildNodes(ImGuiDockNode* dst_node, ImGuiDockNode* src_node)
{
    IM_ASSERT(dst_node->Windows.Size == 0);
    dst_node->ChildNodes[0] = src_node->ChildNodes[0];
    dst_node->ChildNodes[1] = src_node->ChildNodes[1];
    if (dst_node->ChildNodes[0])
        dst_node->ChildNodes[0]->ParentNode = dst_node;
    if (dst_node->ChildNodes[1])
        dst_node->ChildNodes[1]->ParentNode = dst_node;
    dst_node->SplitAxis = src_node->SplitAxis;
    dst_node->SizeRef = src_node->SizeRef;
    src_node->ChildNodes[0] = src_node->ChildNodes[1] = NULL;
}

static void ImGui::DockNodeMoveWindows(ImGuiDockNode* dst_node, ImGuiDockNode* src_node)
{
    // Insert tabs in the same orders as currently ordered (node->Windows isn't ordered)
    IM_ASSERT(src_node && dst_node && dst_node != src_node);
    ImGuiTabBar* src_tab_bar = src_node->TabBar;
    if (src_tab_bar != NULL)
        IM_ASSERT(src_node->Windows.Size <= src_node->TabBar->Tabs.Size);

    // If the dst_node is empty we can just move the entire tab bar (to preserve selection, scrolling, etc.)
    bool move_tab_bar = (src_tab_bar != NULL) && (dst_node->TabBar == NULL);
    if (move_tab_bar)
    {
        dst_node->TabBar = src_node->TabBar;
        src_node->TabBar = NULL;
    }

    // Tab order is not important here, it is preserved by sorting in DockNodeUpdateTabBar().
    for (ImGuiWindow* window : src_node->Windows)
    {
        window->DockNode = NULL;
        window->DockIsActive = false;
        DockNodeAddWindow(dst_node, window, !move_tab_bar);
    }
    src_node->Windows.clear();

    if (!move_tab_bar && src_node->TabBar)
    {
        if (dst_node->TabBar)
            dst_node->TabBar->SelectedTabId = src_node->TabBar->SelectedTabId;
        DockNodeRemoveTabBar(src_node);
    }
}

static void ImGui::DockNodeApplyPosSizeToWindows(ImGuiDockNode* node)
{
    for (ImGuiWindow* window : node->Windows)
    {
        SetWindowPos(window, node->Pos, ImGuiCond_Always); // We don't assign directly to Pos because it can break the calculation of SizeContents on next frame
        SetWindowSize(window, node->Size, ImGuiCond_Always);
    }
}

static void ImGui::DockNodeHideHostWindow(ImGuiDockNode* node)
{
    if (node->HostWindow)
    {
        if (node->HostWindow->DockNodeAsHost == node)
            node->HostWindow->DockNodeAsHost = NULL;
        node->HostWindow = NULL;
    }

    if (node->Windows.Size == 1)
    {
        node->VisibleWindow = node->Windows[0];
        node->Windows[0]->DockIsActive = false;
    }

    if (node->TabBar)
        DockNodeRemoveTabBar(node);
}

// Search function called once by root node in DockNodeUpdate()
struct ImGuiDockNodeTreeInfo
{
    ImGuiDockNode*      CentralNode;
    ImGuiDockNode*      FirstNodeWithWindows;
    int                 CountNodesWithWindows;
    //ImGuiWindowClass  WindowClassForMerges;

    ImGuiDockNodeTreeInfo() { memset((void*)this, 0, sizeof(*this)); }
};

static void DockNodeFindInfo(ImGuiDockNode* node, ImGuiDockNodeTreeInfo* info)
{
    if (node->Windows.Size > 0)
    {
        if (info->FirstNodeWithWindows == NULL)
            info->FirstNodeWithWindows = node;
        info->CountNodesWithWindows++;
    }
    if (node->IsCentralNode())
    {
        IM_ASSERT(info->CentralNode == NULL); // Should be only one
        IM_ASSERT(node->IsLeafNode() && "If you get this assert: please submit .ini file + repro of actions leading to this.");
        info->CentralNode = node;
    }
    if (info->CountNodesWithWindows > 1 && info->CentralNode != NULL)
        return;
    if (node->ChildNodes[0])
        DockNodeFindInfo(node->ChildNodes[0], info);
    if (node->ChildNodes[1])
        DockNodeFindInfo(node->ChildNodes[1], info);
}

static ImGuiWindow* ImGui::DockNodeFindWindowByID(ImGuiDockNode* node, ImGuiID id)
{
    IM_ASSERT(id != 0);
    for (ImGuiWindow* window : node->Windows)
        if (window->ID == id)
            return window;
    return NULL;
}

// - Remove inactive windows/nodes.
// - Update visibility flag.
static void ImGui::DockNodeUpdateFlagsAndCollapse(ImGuiDockNode* node)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(node->ParentNode == NULL || node->ParentNode->ChildNodes[0] == node || node->ParentNode->ChildNodes[1] == node);

    // Inherit most flags
    if (node->ParentNode)
        node->SharedFlags = node->ParentNode->SharedFlags & ImGuiDockNodeFlags_SharedFlagsInheritMask_;

    // Recurse into children
    // There is the possibility that one of our child becoming empty will delete itself and moving its sibling contents into 'node'.
    // If 'node->ChildNode[0]' delete itself, then 'node->ChildNode[1]->Windows' will be moved into 'node'
    // If 'node->ChildNode[1]' delete itself, then 'node->ChildNode[0]->Windows' will be moved into 'node' and the "remove inactive windows" loop will have run twice on those windows (harmless)
    node->HasCentralNodeChild = false;
    if (node->ChildNodes[0])
        DockNodeUpdateFlagsAndCollapse(node->ChildNodes[0]);
    if (node->ChildNodes[1])
        DockNodeUpdateFlagsAndCollapse(node->ChildNodes[1]);

    // Remove inactive windows, collapse nodes
    // Merge node flags overrides stored in windows
    node->LocalFlagsInWindows = ImGuiDockNodeFlags_None;
    for (int window_n = 0; window_n < node->Windows.Size; window_n++)
    {
        ImGuiWindow* window = node->Windows[window_n];
        IM_ASSERT(window->DockNode == node);

        bool node_was_active = (node->LastFrameActive + 1 == g.FrameCount);
        bool remove = false;
        remove |= node_was_active && (window->WasActive == false); // Can't use 'window->LastFrameActive + 1 < g.FrameCount'. (see #9151)
        remove |= node_was_active && (node->WantCloseAll || node->WantCloseTabId == window->TabId) && window->HasCloseButton && !(window->Flags & ImGuiWindowFlags_UnsavedDocument);  // Submit all _expected_ closure from last frame
        remove |= (window->DockTabWantClose);
        if (remove)
        {
            window->DockTabWantClose = false;
            if (node->Windows.Size == 1 && !node->IsCentralNode())
            {
                DockNodeHideHostWindow(node);
                node->State = ImGuiDockNodeState_HostWindowHiddenBecauseSingleWindow;
                DockNodeRemoveWindow(node, window, node->ID); // Will delete the node so it'll be invalid on return
                return;
            }
            DockNodeRemoveWindow(node, window, node->ID);
            window_n--;
            continue;
        }

        // FIXME-DOCKING: Missing policies for conflict resolution, hence the "Experimental" tag on this.
        //node->LocalFlagsInWindow &= ~window->WindowClass.DockNodeFlagsOverrideClear;
        node->LocalFlagsInWindows |= window->WindowClass.DockNodeFlagsOverrideSet;
    }
    node->UpdateMergedFlags();

    // Auto-hide tab bar option
    ImGuiDockNodeFlags node_flags = node->MergedFlags;
    if (node->WantHiddenTabBarUpdate && node->Windows.Size == 1 && (node_flags & ImGuiDockNodeFlags_AutoHideTabBar) && !node->IsHiddenTabBar())
        node->WantHiddenTabBarToggle = true;
    node->WantHiddenTabBarUpdate = false;

    // Cancel toggling if we know our tab bar is enforced to be hidden at all times
    if (node->WantHiddenTabBarToggle && node->VisibleWindow && (node->VisibleWindow->WindowClass.DockNodeFlagsOverrideSet & ImGuiDockNodeFlags_HiddenTabBar))
        node->WantHiddenTabBarToggle = false;

    // Apply toggles at a single point of the frame (here!)
    const ImGuiDockNodeFlags prev_local_flags = node->LocalFlags;
    if (node->Windows.Size > 1)
        node->SetLocalFlags(node->LocalFlags & ~ImGuiDockNodeFlags_HiddenTabBar);
    else if (node->WantHiddenTabBarToggle)
        node->SetLocalFlags(node->LocalFlags ^ ImGuiDockNodeFlags_HiddenTabBar);
    if ((node->LocalFlags ^ prev_local_flags) & ImGuiDockNodeFlags_SavedFlagsMask_)
        MarkIniSettingsDirty(); // Bit flaky to only do this here. Perhaps compare node flags every frame? #9380
    node->WantHiddenTabBarToggle = false;

    DockNodeUpdateVisibleFlag(node);
}

// This is rarely called as DockNodeUpdateForRootNode() generally does it most frames.
static void ImGui::DockNodeUpdateHasCentralNodeChild(ImGuiDockNode* node)
{
    node->HasCentralNodeChild = false;
    if (node->ChildNodes[0])
        DockNodeUpdateHasCentralNodeChild(node->ChildNodes[0]);
    if (node->ChildNodes[1])
        DockNodeUpdateHasCentralNodeChild(node->ChildNodes[1]);
    if (node->IsRootNode())
    {
        ImGuiDockNode* mark_node = node->CentralNode;
        while (mark_node)
        {
            mark_node->HasCentralNodeChild = true;
            mark_node = mark_node->ParentNode;
        }
    }
}

static void ImGui::DockNodeUpdateVisibleFlag(ImGuiDockNode* node)
{
    // Update visibility flag
    bool is_visible = (node->ParentNode == NULL) ? node->IsDockSpace() : node->IsCentralNode();
    is_visible |= (node->Windows.Size > 0);
    is_visible |= (node->ChildNodes[0] && node->ChildNodes[0]->IsVisible);
    is_visible |= (node->ChildNodes[1] && node->ChildNodes[1]->IsVisible);
    node->IsVisible = is_visible;
}

static void ImGui::DockNodeStartMouseMovingWindow(ImGuiDockNode* node, ImGuiWindow* window)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(node->WantMouseMove == true);
    StartMouseMovingWindow(window);
    g.ActiveIdClickOffset = g.IO.MouseClickedPos[0] - node->Pos;
    g.MovingWindow = window; // If we are docked into a non moveable root window, StartMouseMovingWindow() won't set g.MovingWindow. Override that decision.
    node->WantMouseMove = false;
}

// Update CentralNode, OnlyNodeWithWindows, LastFocusedNodeID. Copy window class.
static void ImGui::DockNodeUpdateForRootNode(ImGuiDockNode* node)
{
    DockNodeUpdateFlagsAndCollapse(node);

    // - Setup central node pointers
    // - Find if there's only a single visible window in the hierarchy (in which case we need to display a regular title bar -> FIXME-DOCK: that last part is not done yet!)
    // Cannot merge this with DockNodeUpdateFlagsAndCollapse() because FirstNodeWithWindows is found after window removal and child collapsing
    ImGuiDockNodeTreeInfo info;
    DockNodeFindInfo(node, &info);
    node->CentralNode = info.CentralNode;
    node->OnlyNodeWithWindows = (info.CountNodesWithWindows == 1) ? info.FirstNodeWithWindows : NULL;
    node->CountNodeWithWindows = info.CountNodesWithWindows;
    if (node->LastFocusedNodeId == 0 && info.FirstNodeWithWindows != NULL)
        node->LastFocusedNodeId = info.FirstNodeWithWindows->ID;

    // Copy the window class from of our first window so it can be used for proper dock filtering.
    // When node has mixed windows, prioritize the class with the most constraint (DockingAllowUnclassed = false) as the reference to copy.
    // FIXME-DOCK: We don't recurse properly, this code could be reworked to work from DockNodeUpdateScanRec.
    if (ImGuiDockNode* first_node_with_windows = info.FirstNodeWithWindows)
    {
        node->WindowClass = first_node_with_windows->Windows[0]->WindowClass;
        for (int n = 1; n < first_node_with_windows->Windows.Size; n++)
            if (first_node_with_windows->Windows[n]->WindowClass.DockingAllowUnclassed == false)
            {
                node->WindowClass = first_node_with_windows->Windows[n]->WindowClass;
                break;
            }
    }

    ImGuiDockNode* mark_node = node->CentralNode;
    while (mark_node)
    {
        mark_node->HasCentralNodeChild = true;
        mark_node = mark_node->ParentNode;
    }
}

static void DockNodeSetupHostWindow(ImGuiDockNode* node, ImGuiWindow* host_window)
{
    // Remove ourselves from any previous different host window
    // This can happen if a user mistakenly does (see #4295 for details):
    //  - N+0: DockBuilderAddNode(id, 0)    // missing ImGuiDockNodeFlags_DockSpace
    //  - N+1: NewFrame()                   // will create floating host window for that node
    //  - N+1: DockSpace(id)                // requalify node as dockspace, moving host window
    if (node->HostWindow && node->HostWindow != host_window && node->HostWindow->DockNodeAsHost == node)
        node->HostWindow->DockNodeAsHost = NULL;

    host_window->DockNodeAsHost = node;
    node->HostWindow = host_window;
}

static void ImGui::DockNodeUpdate(ImGuiDockNode* node)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(node->LastFrameActive != g.FrameCount);
    node->LastFrameAlive = g.FrameCount;
    node->IsBgDrawnThisFrame = false;

    node->CentralNode = node->OnlyNodeWithWindows = NULL;
    if (node->IsRootNode())
        DockNodeUpdateForRootNode(node);

    // Remove tab bar if not needed
    if (node->TabBar && node->IsNoTabBar())
        DockNodeRemoveTabBar(node);

    // Early out for hidden root dock nodes (when all DockId references are in inactive windows, or there is only 1 floating window holding on the DockId)
    bool want_to_hide_host_window = false;
    if (node->IsFloatingNode())
    {
        if (node->Windows.Size <= 1 && node->IsLeafNode())
            if (!g.IO.ConfigDockingAlwaysTabBar && (node->Windows.Size == 0 || !node->Windows[0]->WindowClass.DockingAlwaysTabBar))
                want_to_hide_host_window = true;
        if (node->CountNodeWithWindows == 0)
            want_to_hide_host_window = true;
    }
    if (want_to_hide_host_window)
    {
        if (node->Windows.Size == 1)
        {
            // Floating window pos/size is authoritative
            ImGuiWindow* single_window = node->Windows[0];
            node->Pos = single_window->Pos;
            node->Size = single_window->SizeFull;
            node->AuthorityForPos = node->AuthorityForSize = node->AuthorityForViewport = ImGuiDataAuthority_Window;

            // Transfer focus immediately so when we revert to a regular window it is immediately selected
            if (node->HostWindow && g.NavWindow == node->HostWindow)
                FocusWindow(single_window);
            if (node->HostWindow)
            {
                IMGUI_DEBUG_LOG_VIEWPORT("[viewport] Node %08X transfer Viewport %08X->%08X to Window '%s'\n", node->ID, node->HostWindow->Viewport->ID, single_window->ID, single_window->Name);
                single_window->Viewport = node->HostWindow->Viewport;
                single_window->ViewportId = node->HostWindow->ViewportId;
                if (node->HostWindow->ViewportOwned)
                {
                    single_window->Viewport->ID = single_window->ID;
                    single_window->Viewport->Window = single_window;
                    single_window->ViewportOwned = true;
                }
            }
            node->RefViewportId = single_window->ViewportId;
        }

        DockNodeHideHostWindow(node);
        node->State = ImGuiDockNodeState_HostWindowHiddenBecauseSingleWindow;
        node->WantCloseAll = false;
        node->WantCloseTabId = 0;
        node->HasCloseButton = node->HasWindowMenuButton = false;
        node->LastFrameActive = g.FrameCount;

        if (node->WantMouseMove && node->Windows.Size == 1)
            DockNodeStartMouseMovingWindow(node, node->Windows[0]);
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
    if (node->IsVisible && node->HostWindow == NULL && node->IsFloatingNode() && node->IsLeafNode())
    {
        IM_ASSERT(node->Windows.Size > 0);
        ImGuiWindow* ref_window = NULL;
        if (node->SelectedTabId != 0) // Note that we prune single-window-node settings on .ini loading, so this is generally 0 for them!
            ref_window = DockNodeFindWindowByID(node, node->SelectedTabId);
        if (ref_window == NULL)
            ref_window = node->Windows[0];
        if (ref_window->AutoFitFramesX > 0 || ref_window->AutoFitFramesY > 0)
        {
            node->State = ImGuiDockNodeState_HostWindowHiddenBecauseWindowsAreResizing;
            return;
        }
    }

    const ImGuiDockNodeFlags node_flags = node->MergedFlags;

    // Decide if the node will have a close button and a window menu button
    node->HasWindowMenuButton = (node->Windows.Size > 0) && (node_flags & ImGuiDockNodeFlags_NoWindowMenuButton) == 0;
    node->HasCloseButton = false;
    for (ImGuiWindow* window : node->Windows)
    {
        // FIXME-DOCK: Setting DockIsActive here means that for single active window in a leaf node, DockIsActive will be cleared until the next Begin() call.
        node->HasCloseButton |= window->HasCloseButton;
        window->DockIsActive = (node->Windows.Size > 1);
    }
    if ((node_flags & ImGuiDockNodeFlags_NoCloseButton) || !g.Style.DockingNodeHasCloseButton)
        node->HasCloseButton = false;

    // Bind or create host window
    ImGuiWindow* host_window = NULL;
    bool beginned_into_host_window = false;
    if (node->IsDockSpace())
    {
        // [Explicit root dockspace node]
        IM_ASSERT(node->HostWindow);
        host_window = node->HostWindow;
    }
    else
    {
        // [Automatic root or child nodes]
        if (node->IsRootNode() && node->IsVisible)
        {
            ImGuiWindow* ref_window = (node->Windows.Size > 0) ? node->Windows[0] : NULL;

            // Sync Pos
            if (node->AuthorityForPos == ImGuiDataAuthority_Window && ref_window)
                SetNextWindowPos(ref_window->Pos);
            else if (node->AuthorityForPos == ImGuiDataAuthority_DockNode)
                SetNextWindowPos(node->Pos);

            // Sync Size
            if (node->AuthorityForSize == ImGuiDataAuthority_Window && ref_window)
                SetNextWindowSize(ref_window->SizeFull);
            else if (node->AuthorityForSize == ImGuiDataAuthority_DockNode)
                SetNextWindowSize(node->Size);

            // Sync Collapsed
            if (node->AuthorityForSize == ImGuiDataAuthority_Window && ref_window)
                SetNextWindowCollapsed(ref_window->Collapsed);

            // Sync Viewport
            if (node->AuthorityForViewport == ImGuiDataAuthority_Window && ref_window)
                SetNextWindowViewport(ref_window->ViewportId);
            else if (node->AuthorityForViewport == ImGuiDataAuthority_Window && node->RefViewportId != 0)
                SetNextWindowViewport(node->RefViewportId);

            SetNextWindowClass(&node->WindowClass);

            // Begin into the host window
            char window_label[20];
            DockNodeGetHostWindowTitle(node, window_label, IM_COUNTOF(window_label));
            ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_DockNodeHost;
            window_flags |= ImGuiWindowFlags_NoFocusOnAppearing;
            window_flags |= ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoCollapse;
            window_flags |= ImGuiWindowFlags_NoTitleBar;

            SetNextWindowBgAlpha(0.0f); // Don't set ImGuiWindowFlags_NoBackground because it disables borders
            PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
            Begin(window_label, NULL, window_flags);
            PopStyleVar();
            beginned_into_host_window = true;

            host_window = g.CurrentWindow;
            DockNodeSetupHostWindow(node, host_window);
            host_window->DC.CursorPos = host_window->Pos;
            node->Pos = host_window->Pos;
            node->Size = host_window->Size;

            // We set ImGuiWindowFlags_NoFocusOnAppearing because we don't want the host window to take full focus (e.g. steal NavWindow)
            // But we still it bring it to the front of display. There's no way to choose this precise behavior via window flags.
            // One simple case to ponder if: window A has a toggle to create windows B/C/D. Dock B/C/D together, clear the toggle and enable it again.
            // When reappearing B/C/D will request focus and be moved to the top of the display pile, but they are not linked to the dock host window
            // during the frame they appear. The dock host window would keep its old display order, and the sorting in EndFrame would move B/C/D back
            // after the dock host window, losing their top-most status.
            if (node->HostWindow->Appearing)
                BringWindowToDisplayFront(node->HostWindow);

            node->AuthorityForPos = node->AuthorityForSize = node->AuthorityForViewport = ImGuiDataAuthority_Auto;
        }
        else if (node->ParentNode)
        {
            node->HostWindow = host_window = node->ParentNode->HostWindow;
            node->AuthorityForPos = node->AuthorityForSize = node->AuthorityForViewport = ImGuiDataAuthority_Auto;
        }
        if (node->WantMouseMove && node->HostWindow)
            DockNodeStartMouseMovingWindow(node, node->HostWindow);
    }
    node->RefViewportId = 0; // Clear when we have a host window

    // Update focused node (the one whose title bar is highlight) within a node tree
    if (node->IsSplitNode())
        IM_ASSERT(node->TabBar == NULL);
    if (node->IsRootNode())
        if (ImGuiWindow* p_window = g.NavWindow ? g.NavWindow->RootWindow : NULL)
            while (p_window != NULL && p_window->DockNode != NULL)
            {
                ImGuiDockNode* p_node = DockNodeGetRootNode(p_window->DockNode);
                if (p_node == node)
                {
                    node->LastFocusedNodeId = p_window->DockNode->ID; // Note: not using root node ID!
                    break;
                }
                p_window = p_node->HostWindow ? p_node->HostWindow->RootWindow : NULL;
            }

    // Register a hit-test hole in the window unless we are currently dragging a window that is compatible with our dockspace
    ImGuiDockNode* central_node = node->CentralNode;
    const bool central_node_hole = node->IsRootNode() && host_window && (node_flags & ImGuiDockNodeFlags_PassthruCentralNode) != 0 && central_node != NULL && central_node->IsEmpty();
    bool central_node_hole_register_hit_test_hole = central_node_hole;
    if (central_node_hole)
        if (const ImGuiPayload* payload = ImGui::GetDragDropPayload())
            if (payload->IsDataType(IMGUI_PAYLOAD_TYPE_WINDOW) && DockNodeIsDropAllowed(host_window, *(ImGuiWindow**)payload->Data))
                central_node_hole_register_hit_test_hole = false;
    if (central_node_hole_register_hit_test_hole)
    {
        // We add a little padding to match the "resize from edges" behavior and allow grabbing the splitter easily.
        // (But we only add it if there's something else on the other side of the hole, otherwise for e.g. fullscreen
        // covering passthru node we'd have a gap on the edge not covered by the hole)
        IM_ASSERT(node->IsDockSpace()); // We cannot pass this flag without the DockSpace() api. Testing this because we also setup the hole in host_window->ParentNode
        ImGuiDockNode* root_node = DockNodeGetRootNode(central_node);
        ImRect root_rect(root_node->Pos, root_node->Pos + root_node->Size);
        ImRect hole_rect(central_node->Pos, central_node->Pos + central_node->Size);
        if (hole_rect.Min.x > root_rect.Min.x) { hole_rect.Min.x += g.WindowsBorderHoverPadding; }
        if (hole_rect.Max.x < root_rect.Max.x) { hole_rect.Max.x -= g.WindowsBorderHoverPadding; }
        if (hole_rect.Min.y > root_rect.Min.y) { hole_rect.Min.y += g.WindowsBorderHoverPadding; }
        if (hole_rect.Max.y < root_rect.Max.y) { hole_rect.Max.y -= g.WindowsBorderHoverPadding; }
        //GetForegroundDrawList()->AddRect(hole_rect.Min, hole_rect.Max, IM_COL32(255, 0, 0, 255));
        if (central_node_hole && !hole_rect.IsInverted())
        {
            SetWindowHitTestHole(host_window, hole_rect.Min, hole_rect.Max - hole_rect.Min);
            if (host_window->ParentWindow)
                SetWindowHitTestHole(host_window->ParentWindow, hole_rect.Min, hole_rect.Max - hole_rect.Min);
        }
    }

    // Update position/size, process and draw resizing splitters
    if (node->IsRootNode() && host_window)
    {
        DockNodeTreeUpdatePosSize(node, host_window->Pos, host_window->Size);
        PushStyleColor(ImGuiCol_Separator, g.Style.Colors[ImGuiCol_Border]);
        PushStyleColor(ImGuiCol_SeparatorActive, g.Style.Colors[ImGuiCol_ResizeGripActive]);
        PushStyleColor(ImGuiCol_SeparatorHovered, g.Style.Colors[ImGuiCol_ResizeGripHovered]);
        DockNodeTreeUpdateSplitter(node);
        PopStyleColor(3);
    }

    // Draw empty node background (currently can only be the Central Node)
    if (host_window && node->IsEmpty() && node->IsVisible)
    {
        host_window->DrawList->ChannelsSetCurrent(DOCKING_HOST_DRAW_CHANNEL_BG);
        node->LastBgColor = (node_flags & ImGuiDockNodeFlags_PassthruCentralNode) ? 0 : GetColorU32(ImGuiCol_DockingEmptyBg);
        if (node->LastBgColor != 0)
            host_window->DrawList->AddRectFilled(node->Pos, node->Pos + node->Size, node->LastBgColor);
        node->IsBgDrawnThisFrame = true;
    }

    // Draw whole dockspace background if ImGuiDockNodeFlags_PassthruCentralNode if set.
    // We need to draw a background at the root level if requested by ImGuiDockNodeFlags_PassthruCentralNode, but we will only know the correct pos/size
    // _after_ processing the resizing splitters. So we are using the DrawList channel splitting facility to submit drawing primitives out of order!
    const bool render_dockspace_bg = node->IsRootNode() && host_window && (node_flags & ImGuiDockNodeFlags_PassthruCentralNode) != 0;
    if (render_dockspace_bg && node->IsVisible)
    {
        host_window->DrawList->ChannelsSetCurrent(DOCKING_HOST_DRAW_CHANNEL_BG);
        if (central_node_hole)
            RenderRectFilledWithHole(host_window->DrawList, node->Rect(), central_node->Rect(), GetColorU32(ImGuiCol_WindowBg), 0.0f);
        else
            host_window->DrawList->AddRectFilled(node->Pos, node->Pos + node->Size, GetColorU32(ImGuiCol_WindowBg), 0.0f);
    }

    // Draw and populate Tab Bar
    if (host_window)
        host_window->DrawList->ChannelsSetCurrent(DOCKING_HOST_DRAW_CHANNEL_FG);
    if (host_window && node->Windows.Size > 0)
    {
        DockNodeUpdateTabBar(node, host_window);
    }
    else
    {
        node->WantCloseAll = false;
        node->WantCloseTabId = 0;
        node->IsFocused = false;
    }
    if (node->TabBar && node->TabBar->SelectedTabId)
        node->SelectedTabId = node->TabBar->SelectedTabId;
    else if (node->Windows.Size > 0)
        node->SelectedTabId = node->Windows[0]->TabId;

    // Draw payload drop target
    if (host_window && node->IsVisible)
        if (node->IsRootNode() && (g.MovingWindow == NULL || g.MovingWindow->RootWindowDockTree != host_window))
            BeginDockableDragDropTarget(host_window);

    // We update this after DockNodeUpdateTabBar()
    node->LastFrameActive = g.FrameCount;

    // Recurse into children
    // FIXME-DOCK FIXME-OPT: Should not need to recurse into children
    if (host_window)
    {
        if (node->ChildNodes[0])
            DockNodeUpdate(node->ChildNodes[0]);
        if (node->ChildNodes[1])
            DockNodeUpdate(node->ChildNodes[1]);

        // Render outer borders last (after the tab bar)
        if (node->IsRootNode())
            RenderWindowOuterBorders(host_window);
    }

    // End host window
    if (beginned_into_host_window) //-V1020
        End();
}

// Compare TabItem nodes given the last known DockOrder (will persist in .ini file as hint), used to sort tabs when multiple tabs are added on the same frame.
static int IMGUI_CDECL TabItemComparerByDockOrder(const void* lhs, const void* rhs)
{
    ImGuiWindow* a = ((const ImGuiTabItem*)lhs)->Window;
    ImGuiWindow* b = ((const ImGuiTabItem*)rhs)->Window;
    if (int d = ((a->DockOrder == -1) ? INT_MAX : a->DockOrder) - ((b->DockOrder == -1) ? INT_MAX : b->DockOrder))
        return d;
    return (a->BeginOrderWithinContext - b->BeginOrderWithinContext);
}

// Default handler for g.DockNodeWindowMenuHandler(): display the list of windows for a given dock-node.
// This is exceptionally stored in a function pointer to also user applications to tweak this menu (undocumented)
// Custom overrides may want to decorate, group, sort entries.
// Please note those are internal structures: if you copy this expect occasional breakage.
// (if you don't need to modify the "Tabs.Size == 1" behavior/path it is recommend you call this function in your handler)
void ImGui::DockNodeWindowMenuHandler_Default(ImGuiContext* ctx, ImGuiDockNode* node, ImGuiTabBar* tab_bar)
{
    IM_UNUSED(ctx);
    if (tab_bar->Tabs.Size == 1)
    {
        // "Hide tab bar" option. Being one of our rare user-facing string we pull it from a table.
        if (MenuItem(LocalizeGetMsg(ImGuiLocKey_DockingHideTabBar), NULL, node->IsHiddenTabBar()))
            node->WantHiddenTabBarToggle = true;
    }
    else
    {
        // Display a selectable list of windows in this docking node
        for (int tab_n = 0; tab_n < tab_bar->Tabs.Size; tab_n++)
        {
            ImGuiTabItem* tab = &tab_bar->Tabs[tab_n];
            if (tab->Flags & ImGuiTabItemFlags_Button)
                continue;
            if (Selectable(TabBarGetTabName(tab_bar, tab), tab->ID == tab_bar->SelectedTabId))
                TabBarQueueFocus(tab_bar, tab);
            SameLine();
            Text("   ");
        }
    }
}

static void ImGui::DockNodeWindowMenuUpdate(ImGuiDockNode* node, ImGuiTabBar* tab_bar)
{
    // Try to position the menu so it is more likely to stays within the same viewport
    ImGuiContext& g = *GImGui;
    if (g.Style.WindowMenuButtonPosition == ImGuiDir_Left)
        SetNextWindowPos(ImVec2(node->Pos.x, node->Pos.y + GetFrameHeight()), ImGuiCond_Always, ImVec2(0.0f, 0.0f));
    else
        SetNextWindowPos(ImVec2(node->Pos.x + node->Size.x, node->Pos.y + GetFrameHeight()), ImGuiCond_Always, ImVec2(1.0f, 0.0f));
    if (BeginPopup("#WindowMenu"))
    {
        node->IsFocused = true;
        g.DockNodeWindowMenuHandler(&g, node, tab_bar);
        EndPopup();
    }
}

// User helper to append/amend into a dock node tab bar. Most commonly used to add e.g. a "+" button.
bool ImGui::DockNodeBeginAmendTabBar(ImGuiDockNode* node)
{
    if (node->TabBar == NULL || node->HostWindow == NULL)
        return false;
    if (node->MergedFlags & ImGuiDockNodeFlags_KeepAliveOnly)
        return false;
    if (node->TabBar->ID == 0)
        return false;
    Begin(node->HostWindow->Name);
    PushOverrideID(node->ID);
    bool ret = BeginTabBarEx(node->TabBar, node->TabBar->BarRect, node->TabBar->Flags);
    IM_UNUSED(ret);
    IM_ASSERT(ret);
    return true;
}

void ImGui::DockNodeEndAmendTabBar()
{
    EndTabBar();
    PopID();
    End();
}

static bool IsDockNodeTitleBarHighlighted(ImGuiDockNode* node, ImGuiDockNode* root_node)
{
    // CTRL+Tab highlight (only highlighting leaf node, not whole hierarchy)
    ImGuiContext& g = *GImGui;
    if (g.NavWindowingTarget)
        return (g.NavWindowingTarget->DockNode == node);

    // FIXME-DOCKING: May want alternative to treat central node void differently? e.g. if (g.NavWindow == host_window)
    if (g.NavWindow && root_node->LastFocusedNodeId == node->ID)
    {
        // FIXME: This could all be backed in RootWindowForTitleBarHighlight? Probably need to reorganize for both dock nodes + other RootWindowForTitleBarHighlight users (not-node)
        ImGuiWindow* parent_window = g.NavWindow->RootWindow;
        while (parent_window->Flags & ImGuiWindowFlags_ChildMenu)
            parent_window = parent_window->ParentWindow->RootWindow;
        ImGuiDockNode* start_parent_node = parent_window->DockNodeAsHost ? parent_window->DockNodeAsHost : parent_window->DockNode;
        for (ImGuiDockNode* parent_node = start_parent_node; parent_node != NULL; parent_node = parent_node->HostWindow ? parent_node->HostWindow->RootWindow->DockNode : NULL)
            if ((parent_node = ImGui::DockNodeGetRootNode(parent_node)) == root_node)
                return true;
    }
    return false;
}

// Submit the tab bar corresponding to a dock node and various housekeeping details.
static void ImGui::DockNodeUpdateTabBar(ImGuiDockNode* node, ImGuiWindow* host_window)
{
    ImGuiContext& g = *GImGui;
    ImGuiStyle& style = g.Style;

    const bool node_was_active = (node->LastFrameActive + 1 == g.FrameCount);
    node->WantCloseAll = false;
    node->WantCloseTabId = 0;

    // Decide if we should use a focused title bar color
    bool is_focused = false;
    ImGuiDockNode* root_node = DockNodeGetRootNode(node);
    if (IsDockNodeTitleBarHighlighted(node, root_node))
        is_focused = true;

    // Hidden tab bar will show a triangle on the upper-left (in Begin)
    if (node->IsHiddenTabBar() || node->IsNoTabBar())
    {
        node->VisibleWindow = (node->Windows.Size > 0) ? node->Windows[0] : NULL;
        node->IsFocused = is_focused;
        if (is_focused)
            node->LastFrameFocused = g.FrameCount;
        if (node->VisibleWindow)
        {
            // Notify root of visible window (used to display title in OS task bar)
            if (is_focused || root_node->VisibleWindow == NULL)
                root_node->VisibleWindow = node->VisibleWindow;
            if (node->TabBar)
                node->TabBar->VisibleTabId = node->VisibleWindow->TabId;
        }
        return;
    }

    // Move ourselves to the Menu layer (so we can be accessed by tapping Alt) + undo SkipItems flag in order to draw over the title bar even if the window is collapsed
    bool backup_skip_item = host_window->SkipItems;
    if (!node->IsDockSpace())
    {
        host_window->SkipItems = false;
        host_window->DC.NavLayerCurrent = ImGuiNavLayer_Menu;
    }

    // Use PushOverrideID() instead of PushID() to use the node id _without_ the host window ID.
    // This is to facilitate computing those ID from the outside, and will affect more or less only the ID of the collapse button, popup and tabs,
    // as docked windows themselves will override the stack with their own root ID.
    PushOverrideID(node->ID);
    ImGuiTabBar* tab_bar = node->TabBar;
    bool tab_bar_is_recreated = (tab_bar == NULL); // Tab bar are automatically destroyed when a node gets hidden
    if (tab_bar == NULL)
    {
        DockNodeAddTabBar(node);
        tab_bar = node->TabBar;
    }

    ImGuiID focus_tab_id = 0;
    node->IsFocused = is_focused;

    const ImGuiDockNodeFlags node_flags = node->MergedFlags;
    const bool has_window_menu_button = (node_flags & ImGuiDockNodeFlags_NoWindowMenuButton) == 0 && (style.WindowMenuButtonPosition != ImGuiDir_None);

    // In a dock node, the Collapse Button turns into the Window Menu button.
    // FIXME-DOCK FIXME-OPT: Could we recycle popups id across multiple dock nodes?
    if (has_window_menu_button && IsPopupOpen("#WindowMenu"))
    {
        ImGuiID next_selected_tab_id = tab_bar->NextSelectedTabId;
        DockNodeWindowMenuUpdate(node, tab_bar);
        if (tab_bar->NextSelectedTabId != 0 && tab_bar->NextSelectedTabId != next_selected_tab_id)
            focus_tab_id = tab_bar->NextSelectedTabId;
        is_focused |= node->IsFocused;
    }

    // Layout
    ImRect title_bar_rect, tab_bar_rect;
    ImVec2 window_menu_button_pos;
    ImVec2 close_button_pos;
    DockNodeCalcTabBarLayout(node, &title_bar_rect, &tab_bar_rect, &window_menu_button_pos, &close_button_pos);

    // Submit new tabs, they will be added as Unsorted and sorted below based on relative DockOrder value.
    const int tabs_count_old = tab_bar->Tabs.Size;
    for (int window_n = 0; window_n < node->Windows.Size; window_n++)
    {
        ImGuiWindow* window = node->Windows[window_n];
        if (TabBarFindTabByID(tab_bar, window->TabId) == NULL)
            TabBarAddTab(tab_bar, ImGuiTabItemFlags_Unsorted, window);
    }

    // Title bar
    if (is_focused)
        node->LastFrameFocused = g.FrameCount;
    ImU32 title_bar_col = GetColorU32(host_window->Collapsed ? ImGuiCol_TitleBgCollapsed : is_focused ? ImGuiCol_TitleBgActive : ImGuiCol_TitleBg);
    ImDrawFlags rounding_flags = CalcRoundingFlagsForRectInRect(title_bar_rect, host_window->Rect(), g.Style.DockingSeparatorSize);
    host_window->DrawList->AddRectFilled(title_bar_rect.Min, title_bar_rect.Max, title_bar_col, host_window->WindowRounding, rounding_flags);

    // Docking/Collapse button
    if (has_window_menu_button)
    {
        if (CollapseButton(host_window->GetID("#COLLAPSE"), window_menu_button_pos, node)) // == DockNodeGetWindowMenuButtonId(node)
            OpenPopup("#WindowMenu");
        if (IsItemActive())
            focus_tab_id = tab_bar->SelectedTabId;
        if (IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_DelayNormal) && g.HoveredIdTimer > 0.5f)
            SetTooltip("%s", LocalizeGetMsg(ImGuiLocKey_DockingDragToUndockOrMoveNode));
    }

    // If multiple tabs are appearing on the same frame, sort them based on their persistent DockOrder value
    int tabs_unsorted_start = tab_bar->Tabs.Size;
    for (int tab_n = tab_bar->Tabs.Size - 1; tab_n >= 0 && (tab_bar->Tabs[tab_n].Flags & ImGuiTabItemFlags_Unsorted); tab_n--)
    {
        // FIXME-DOCK: Consider only clearing the flag after the tab has been alive for a few consecutive frames, allowing late comers to not break sorting?
        tab_bar->Tabs[tab_n].Flags &= ~ImGuiTabItemFlags_Unsorted;
        tabs_unsorted_start = tab_n;
    }
    if (tab_bar->Tabs.Size > tabs_unsorted_start)
    {
        IMGUI_DEBUG_LOG_DOCKING("[docking] In node 0x%08X: %d new appearing tabs:%s\n", node->ID, tab_bar->Tabs.Size - tabs_unsorted_start, (tab_bar->Tabs.Size > tabs_unsorted_start + 1) ? " (will sort)" : "");
        for (int tab_n = tabs_unsorted_start; tab_n < tab_bar->Tabs.Size; tab_n++)
        {
            ImGuiTabItem* tab = &tab_bar->Tabs[tab_n];
            IM_UNUSED(tab);
            IMGUI_DEBUG_LOG_DOCKING("[docking] - Tab 0x%08X '%s' Order %d\n", tab->ID, TabBarGetTabName(tab_bar, tab), tab->Window ? tab->Window->DockOrder : -1);
        }
        IMGUI_DEBUG_LOG_DOCKING("[docking] SelectedTabId = 0x%08X, NavWindow->TabId = 0x%08X\n", node->SelectedTabId, g.NavWindow ? g.NavWindow->TabId : -1);
        if (tab_bar->Tabs.Size > tabs_unsorted_start + 1)
            ImQsort(tab_bar->Tabs.Data + tabs_unsorted_start, tab_bar->Tabs.Size - tabs_unsorted_start, sizeof(ImGuiTabItem), TabItemComparerByDockOrder);
    }

    // Apply NavWindow focus back to the tab bar
    if (g.NavWindow && g.NavWindow->RootWindow->DockNode == node)
        tab_bar->SelectedTabId = g.NavWindow->RootWindow->TabId;

    // Selected newly added tabs, or persistent tab ID if the tab bar was just recreated
    if (tab_bar_is_recreated && TabBarFindTabByID(tab_bar, node->SelectedTabId) != NULL)
        tab_bar->SelectedTabId = tab_bar->NextSelectedTabId = node->SelectedTabId;
    else if (tab_bar->Tabs.Size > tabs_count_old)
        tab_bar->SelectedTabId = tab_bar->NextSelectedTabId = tab_bar->Tabs.back().Window->TabId;

    // Begin tab bar
    ImGuiTabBarFlags tab_bar_flags = ImGuiTabBarFlags_Reorderable | ImGuiTabBarFlags_AutoSelectNewTabs; // | ImGuiTabBarFlags_NoTabListScrollingButtons);
    tab_bar_flags |= ImGuiTabBarFlags_SaveSettings | ImGuiTabBarFlags_DockNode;
    tab_bar_flags |= ImGuiTabBarFlags_FittingPolicyMixed; // Enforce default policy. Since 1.92.2 this is now reasonable. May expose later if needed. (#8800, #3421)
    tab_bar_flags |= ImGuiTabBarFlags_DrawSelectedOverline;
    if (!host_window->Collapsed && is_focused)
        tab_bar_flags |= ImGuiTabBarFlags_IsFocused;
    tab_bar->ID = node->ID;// GetID("#TabBar");
    tab_bar->SeparatorMinX = node->Pos.x + host_window->WindowBorderSize; // Separator cover the whole node width
    tab_bar->SeparatorMaxX = node->Pos.x + node->Size.x - host_window->WindowBorderSize;
    BeginTabBarEx(tab_bar, tab_bar_rect, tab_bar_flags);
    //host_window->DrawList->AddRect(tab_bar_rect.Min, tab_bar_rect.Max, IM_COL32(255,0,255,255));

    // Backup style colors
    ImVec4 backup_style_cols[ImGuiWindowDockStyleCol_COUNT];
    for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
        backup_style_cols[color_n] = g.Style.Colors[GWindowDockStyleColors[color_n]];

    // Submit actual tabs
    node->VisibleWindow = NULL;
    for (int window_n = 0; window_n < node->Windows.Size; window_n++)
    {
        ImGuiWindow* window = node->Windows[window_n];
        if (window->LastFrameActive + 1 < g.FrameCount && node_was_active)
            continue; // FIXME: Not sure if that's still taken/useful, as windows are normally removed in DockNodeUpdateFlagsAndCollapse().

        ImGuiTabItemFlags tab_item_flags = 0;
        tab_item_flags |= window->WindowClass.TabItemFlagsOverrideSet;
        if (window->Flags & ImGuiWindowFlags_UnsavedDocument)
            tab_item_flags |= ImGuiTabItemFlags_UnsavedDocument;
        if (tab_bar->Flags & ImGuiTabBarFlags_NoCloseWithMiddleMouseButton)
            tab_item_flags |= ImGuiTabItemFlags_NoCloseWithMiddleMouseButton;

        // Apply stored style overrides for the window
        for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
            g.Style.Colors[GWindowDockStyleColors[color_n]] = ColorConvertU32ToFloat4(window->DockStyle.Colors[color_n]);

        // Note that TabItemEx() calls TabBarCalcTabID() so our tab item ID will ignore the current ID stack (rightly so)
        bool tab_open = true;
        TabItemEx(tab_bar, window->Name, window->HasCloseButton ? &tab_open : NULL, tab_item_flags, window);
        if (!tab_open)
            node->WantCloseTabId = window->TabId;
        if (tab_bar->VisibleTabId == window->TabId)
            node->VisibleWindow = window;

        // Store last item data so it can be queried with IsItemXXX functions after the user Begin() call
        window->DC.DockTabItemStatusFlags = g.LastItemData.StatusFlags;
        window->DC.DockTabItemRect = g.LastItemData.Rect;

        // Update navigation ID on menu layer
        if (g.NavWindow && g.NavWindow->RootWindow == window && (window->DC.NavLayersActiveMask & (1 << ImGuiNavLayer_Menu)) == 0)
            host_window->NavLastIds[1] = window->TabId;
    }

    // Restore style colors
    for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
        g.Style.Colors[GWindowDockStyleColors[color_n]] = backup_style_cols[color_n];

    // Notify root of visible window (used to display title in OS task bar)
    if (node->VisibleWindow)
        if (is_focused || root_node->VisibleWindow == NULL)
            root_node->VisibleWindow = node->VisibleWindow;

    // Close button (after VisibleWindow was updated)
    // Note that VisibleWindow may have been overrided by CTRL+Tabbing, so VisibleWindow->TabId may be != from tab_bar->SelectedTabId
    const bool close_button_is_enabled = node->HasCloseButton && node->VisibleWindow && node->VisibleWindow->HasCloseButton;
    const bool close_button_is_visible = node->HasCloseButton;
    //const bool close_button_is_visible = close_button_is_enabled; // Most people would expect this behavior of not even showing the button (leaving a hole since we can't claim that space as other windows in the tba bar have one)
    if (close_button_is_visible)
    {
        if (!close_button_is_enabled)
        {
            PushItemFlag(ImGuiItemFlags_Disabled, true);
            PushStyleColor(ImGuiCol_Text, style.Colors[ImGuiCol_Text] * ImVec4(1.0f,1.0f,1.0f,0.4f));
        }
        if (CloseButton(host_window->GetID("#CLOSE"), close_button_pos))
        {
            node->WantCloseAll = true;
            for (int n = 0; n < tab_bar->Tabs.Size; n++)
                TabBarCloseTab(tab_bar, &tab_bar->Tabs[n]);
        }
        //if (IsItemActive())
        //    focus_tab_id = tab_bar->SelectedTabId;
        if (!close_button_is_enabled)
        {
            PopStyleColor();
            PopItemFlag();
        }
    }

    // When clicking on the title bar outside of tabs, we still focus the selected tab for that node
    // FIXME: TabItems submitted earlier use AllowItemOverlap so we manually perform a more specific test for now (hovered || held) in order to not cover them.
    ImGuiID title_bar_id = host_window->GetID("#TITLEBAR");
    if (g.HoveredId == 0 || g.HoveredId == title_bar_id || g.ActiveId == title_bar_id)
    {
        // AllowOverlap mode required for appending into dock node tab bar,
        // otherwise dragging window will steal HoveredId and amended tabs cannot get them.
        bool held;
        KeepAliveID(title_bar_id);
        ButtonBehavior(title_bar_rect, title_bar_id, NULL, &held, ImGuiButtonFlags_AllowOverlap);
        if (g.HoveredId == title_bar_id)
        {
            g.LastItemData.ID = title_bar_id;
        }
        if (held)
        {
            if (IsMouseClicked(0))
                focus_tab_id = tab_bar->SelectedTabId;

            // Forward moving request to selected window
            if (ImGuiTabItem* tab = TabBarFindTabByID(tab_bar, tab_bar->SelectedTabId))
                StartMouseMovingWindowOrNode(tab->Window ? tab->Window : node->HostWindow, node, false); // Undock from tab bar empty space
        }
    }

    // Forward focus from host node to selected window
    //if (is_focused && g.NavWindow == host_window && !g.NavWindowingTarget)
    //    focus_tab_id = tab_bar->SelectedTabId;

    // When clicked on a tab we requested focus to the docked child
    // This overrides the value set by "forward focus from host node to selected window".
    if (tab_bar->NextSelectedTabId)
        focus_tab_id = tab_bar->NextSelectedTabId;

    // Apply navigation focus
    if (focus_tab_id != 0)
        if (ImGuiTabItem* tab = TabBarFindTabByID(tab_bar, focus_tab_id))
            if (tab->Window)
            {
                FocusWindow(tab->Window);
                if (g.NavId == 0) // only init if FocusWindow() didn't restore anything.
                    NavInitWindow(tab->Window, false);
            }

    EndTabBar();
    PopID();

    // Restore SkipItems flag
    if (!node->IsDockSpace())
    {
        host_window->DC.NavLayerCurrent = ImGuiNavLayer_Main;
        host_window->SkipItems = backup_skip_item;
    }
}

static void ImGui::DockNodeAddTabBar(ImGuiDockNode* node)
{
    IM_ASSERT(node->TabBar == NULL);
    node->TabBar = IM_NEW(ImGuiTabBar);
}

static void ImGui::DockNodeRemoveTabBar(ImGuiDockNode* node)
{
    if (node->TabBar == NULL)
        return;
    IM_DELETE(node->TabBar);
    node->TabBar = NULL;
}

static bool DockNodeIsDropAllowedOne(ImGuiWindow* payload, ImGuiWindow* host_window)
{
    if (host_window->DockNodeAsHost && host_window->DockNodeAsHost->IsDockSpace() && payload->BeginOrderWithinContext < host_window->BeginOrderWithinContext)
        return false;

    ImGuiWindowClass* host_class = host_window->DockNodeAsHost ? &host_window->DockNodeAsHost->WindowClass : &host_window->WindowClass;
    ImGuiWindowClass* payload_class = &payload->WindowClass;
    if (host_class->ClassId != payload_class->ClassId)
    {
        bool pass = false;
        if (host_class->ClassId != 0 && host_class->DockingAllowUnclassed && payload_class->ClassId == 0)
            pass = true;
        if (payload_class->ClassId != 0 && payload_class->DockingAllowUnclassed && host_class->ClassId == 0)
            pass = true;
        if (!pass)
            return false;
    }

    // Prevent docking any window created above a popup
    // Technically we should support it (e.g. in the case of a long-lived modal window that had fancy docking features),
    // by e.g. adding a 'if (!ImGui::IsWindowWithinBeginStackOf(host_window, popup_window))' test.
    // But it would requires more work on our end because the dock host windows is technically created in NewFrame()
    // and our ->ParentXXX and ->RootXXX pointers inside windows are currently mislading or lacking.
    ImGuiContext& g = *GImGui;
    for (int i = g.OpenPopupStack.Size - 1; i >= 0; i--)
        if (ImGuiWindow* popup_window = g.OpenPopupStack[i].Window)
            if (ImGui::IsWindowWithinBeginStackOf(payload, popup_window))   // Payload is created from within a popup begin stack.
                return false;

    return true;
}

static bool ImGui::DockNodeIsDropAllowed(ImGuiWindow* host_window, ImGuiWindow* root_payload)
{
    if (root_payload->DockNodeAsHost && root_payload->DockNodeAsHost->IsSplitNode()) // FIXME-DOCK: Missing filtering
        return true;

    const int payload_count = root_payload->DockNodeAsHost ? root_payload->DockNodeAsHost->Windows.Size : 1;
    for (int payload_n = 0; payload_n < payload_count; payload_n++)
    {
        ImGuiWindow* payload = root_payload->DockNodeAsHost ? root_payload->DockNodeAsHost->Windows[payload_n] : root_payload;
        if (DockNodeIsDropAllowedOne(payload, host_window))
            return true;
    }
    return false;
}

// window menu button == collapse button when not in a dock node.
// FIXME: This is similar to RenderWindowTitleBarContents(), may want to share code.
static void ImGui::DockNodeCalcTabBarLayout(const ImGuiDockNode* node, ImRect* out_title_rect, ImRect* out_tab_bar_rect, ImVec2* out_window_menu_button_pos, ImVec2* out_close_button_pos)
{
    ImGuiContext& g = *GImGui;
    ImGuiStyle& style = g.Style;

    ImRect r = ImRect(node->Pos.x, node->Pos.y, node->Pos.x + node->Size.x, node->Pos.y + g.FontSize + g.Style.FramePadding.y * 2.0f);
    if (out_title_rect) { *out_title_rect = r; }

    r.Min.x += style.WindowBorderSize;
    r.Max.x -= style.WindowBorderSize;

    float button_sz = g.FontSize;
    r.Min.x += style.FramePadding.x;
    r.Max.x -= style.FramePadding.x;
    ImVec2 window_menu_button_pos = ImVec2(r.Min.x, r.Min.y + style.FramePadding.y);
    if (node->HasCloseButton)
    {
        if (out_close_button_pos) *out_close_button_pos = ImVec2(r.Max.x - button_sz, r.Min.y + style.FramePadding.y);
        r.Max.x -= button_sz + style.ItemInnerSpacing.x;
    }
    if (node->HasWindowMenuButton && style.WindowMenuButtonPosition == ImGuiDir_Left)
    {
        r.Min.x += button_sz + style.ItemInnerSpacing.x;
    }
    else if (node->HasWindowMenuButton && style.WindowMenuButtonPosition == ImGuiDir_Right)
    {
        window_menu_button_pos = ImVec2(r.Max.x - button_sz, r.Min.y + style.FramePadding.y);
        r.Max.x -= button_sz + style.ItemInnerSpacing.x;
    }
    if (out_tab_bar_rect) { *out_tab_bar_rect = r; }
    if (out_window_menu_button_pos) { *out_window_menu_button_pos = window_menu_button_pos; }
}

void ImGui::DockNodeCalcSplitRects(ImVec2& pos_old, ImVec2& size_old, ImVec2& pos_new, ImVec2& size_new, ImGuiDir dir, ImVec2 size_new_desired)
{
    ImGuiContext& g = *GImGui;
    const float dock_spacing = g.Style.ItemInnerSpacing.x;
    const ImGuiAxis axis = (dir == ImGuiDir_Left || dir == ImGuiDir_Right) ? ImGuiAxis_X : ImGuiAxis_Y;
    pos_new[axis ^ 1] = pos_old[axis ^ 1];
    size_new[axis ^ 1] = size_old[axis ^ 1];

    // Distribute size on given axis (with a desired size or equally)
    const float w_avail = size_old[axis] - dock_spacing;
    if (size_new_desired[axis] > 0.0f && size_new_desired[axis] <= w_avail * 0.5f)
    {
        size_new[axis] = size_new_desired[axis];
        size_old[axis] = IM_TRUNC(w_avail - size_new[axis]);
    }
    else
    {
        size_new[axis] = IM_TRUNC(w_avail * 0.5f);
        size_old[axis] = IM_TRUNC(w_avail - size_new[axis]);
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

// Retrieve the drop rectangles for a given direction or for the center + perform hit testing.
bool ImGui::DockNodeCalcDropRectsAndTestMousePos(const ImRect& parent, ImGuiDir dir, ImRect& out_r, bool outer_docking, ImVec2* test_mouse_pos)
{
    ImGuiContext& g = *GImGui;

    const float parent_smaller_axis = ImMin(parent.GetWidth(), parent.GetHeight());
    const float hs_for_central_nodes = ImMin(g.FontSize * 1.5f, ImMax(g.FontSize * 0.5f, parent_smaller_axis / 8.0f));
    float hs_w; // Half-size, longer axis
    float hs_h; // Half-size, smaller axis
    ImVec2 off; // Distance from edge or center
    if (outer_docking)
    {
        //hs_w = ImTrunc(ImClamp(parent_smaller_axis - hs_for_central_nodes * 4.0f, g.FontSize * 0.5f, g.FontSize * 8.0f));
        //hs_h = ImTrunc(hs_w * 0.15f);
        //off = ImVec2(ImTrunc(parent.GetWidth() * 0.5f - GetFrameHeightWithSpacing() * 1.4f - hs_h), ImTrunc(parent.GetHeight() * 0.5f - GetFrameHeightWithSpacing() * 1.4f - hs_h));
        hs_w = ImTrunc(hs_for_central_nodes * 1.50f);
        hs_h = ImTrunc(hs_for_central_nodes * 0.80f);
        off = ImTrunc(ImVec2(parent.GetWidth() * 0.5f - hs_h, parent.GetHeight() * 0.5f - hs_h));
    }
    else
    {
        hs_w = ImTrunc(hs_for_central_nodes);
        hs_h = ImTrunc(hs_for_central_nodes * 0.90f);
        off = ImTrunc(ImVec2(hs_w * 2.40f, hs_w * 2.40f));
    }

    ImVec2 c = ImTrunc(parent.GetCenter());
    if      (dir == ImGuiDir_None)  { out_r = ImRect(c.x - hs_w, c.y - hs_w,         c.x + hs_w, c.y + hs_w);         }
    else if (dir == ImGuiDir_Up)    { out_r = ImRect(c.x - hs_w, c.y - off.y - hs_h, c.x + hs_w, c.y - off.y + hs_h); }
    else if (dir == ImGuiDir_Down)  { out_r = ImRect(c.x - hs_w, c.y + off.y - hs_h, c.x + hs_w, c.y + off.y + hs_h); }
    else if (dir == ImGuiDir_Left)  { out_r = ImRect(c.x - off.x - hs_h, c.y - hs_w, c.x - off.x + hs_h, c.y + hs_w); }
    else if (dir == ImGuiDir_Right) { out_r = ImRect(c.x + off.x - hs_h, c.y - hs_w, c.x + off.x + hs_h, c.y + hs_w); }

    if (test_mouse_pos == NULL)
        return false;

    ImRect hit_r = out_r;
    if (!outer_docking)
    {
        // Custom hit testing for the 5-way selection, designed to reduce flickering when moving diagonally between sides
        hit_r.Expand(ImTrunc(hs_w * 0.30f));
        ImVec2 mouse_delta = (*test_mouse_pos - c);
        float mouse_delta_len2 = ImLengthSqr(mouse_delta);
        float r_threshold_center = hs_w * 1.4f;
        float r_threshold_sides = hs_w * (1.4f + 1.2f);
        if (mouse_delta_len2 < r_threshold_center * r_threshold_center)
            return (dir == ImGuiDir_None);
        if (mouse_delta_len2 < r_threshold_sides * r_threshold_sides)
            return (dir == ImGetDirQuadrantFromDelta(mouse_delta.x, mouse_delta.y));
    }
    return hit_r.Contains(*test_mouse_pos);
}

// host_node may be NULL if the window doesn't have a DockNode already.
// FIXME-DOCK: This is misnamed since it's also doing the filtering.
static void ImGui::DockNodePreviewDockSetup(ImGuiWindow* host_window, ImGuiDockNode* host_node, ImGuiWindow* payload_window, ImGuiDockNode* payload_node, ImGuiDockPreviewData* data, bool is_explicit_target, bool is_outer_docking)
{
    ImGuiContext& g = *GImGui;

    // There is an edge case when docking into a dockspace which only has inactive nodes.
    // In this case DockNodeTreeFindNodeByPos() will have selected a leaf node which is inactive.
    // Because the inactive leaf node doesn't have proper pos/size yet, we'll use the root node as reference.
    if (payload_node == NULL)
        payload_node = payload_window->DockNodeAsHost;
    ImGuiDockNode* ref_node_for_rect = (host_node && !host_node->IsVisible) ? DockNodeGetRootNode(host_node) : host_node;
    if (ref_node_for_rect)
        IM_ASSERT(ref_node_for_rect->IsVisible == true);

    // Filter, figure out where we are allowed to dock
    ImGuiDockNodeFlags src_node_flags = payload_node ? payload_node->MergedFlags : payload_window->WindowClass.DockNodeFlagsOverrideSet;
    ImGuiDockNodeFlags dst_node_flags = host_node ? host_node->MergedFlags : host_window->WindowClass.DockNodeFlagsOverrideSet;
    data->IsCenterAvailable = true;
    if (is_outer_docking)
        data->IsCenterAvailable = false;
    else if (g.IO.ConfigDockingNoDockingOver)
        data->IsCenterAvailable = false;
    else if (dst_node_flags & ImGuiDockNodeFlags_NoDockingOverMe)
        data->IsCenterAvailable = false;
    else if (host_node && (dst_node_flags & ImGuiDockNodeFlags_NoDockingOverCentralNode) && host_node->IsCentralNode())
        data->IsCenterAvailable = false;
    else if ((!host_node || !host_node->IsEmpty()) && payload_node && payload_node->IsSplitNode() && (payload_node->OnlyNodeWithWindows == NULL)) // Is _visibly_ split?
        data->IsCenterAvailable = false;
    else if ((src_node_flags & ImGuiDockNodeFlags_NoDockingOverOther) && (!host_node || !host_node->IsEmpty()))
        data->IsCenterAvailable = false;
    else if ((src_node_flags & ImGuiDockNodeFlags_NoDockingOverEmpty) && host_node && host_node->IsEmpty())
        data->IsCenterAvailable = false;

    data->IsSidesAvailable = true;
    if ((dst_node_flags & ImGuiDockNodeFlags_NoDockingSplit) || g.IO.ConfigDockingNoSplit)
        data->IsSidesAvailable = false;
    else if (!is_outer_docking && host_node && host_node->ParentNode == NULL && host_node->IsCentralNode())
        data->IsSidesAvailable = false;
    else if (src_node_flags & ImGuiDockNodeFlags_NoDockingSplitOther)
        data->IsSidesAvailable = false;

    // Build a tentative future node (reuse same structure because it is practical. Shape will be readjusted when previewing a split)
    data->FutureNode.HasCloseButton = (host_node ? host_node->HasCloseButton : host_window->HasCloseButton) || (payload_window->HasCloseButton);
    data->FutureNode.HasWindowMenuButton = host_node ? true : ((host_window->Flags & ImGuiWindowFlags_NoCollapse) == 0);
    data->FutureNode.Pos = ref_node_for_rect ? ref_node_for_rect->Pos : host_window->Pos;
    data->FutureNode.Size = ref_node_for_rect ? ref_node_for_rect->Size : host_window->Size;

    // Calculate drop shapes geometry for allowed splitting directions
    IM_ASSERT(ImGuiDir_None == -1);
    data->SplitNode = host_node;
    data->SplitDir = ImGuiDir_None;
    data->IsSplitDirExplicit = false;
    if (!host_window->Collapsed)
        for (int dir = ImGuiDir_None; dir < ImGuiDir_COUNT; dir++)
        {
            if (dir == ImGuiDir_None && !data->IsCenterAvailable)
                continue;
            if (dir != ImGuiDir_None && !data->IsSidesAvailable)
                continue;
            if (DockNodeCalcDropRectsAndTestMousePos(data->FutureNode.Rect(), (ImGuiDir)dir, data->DropRectsDraw[dir+1], is_outer_docking, &g.IO.MousePos))
            {
                data->SplitDir = (ImGuiDir)dir;
                data->IsSplitDirExplicit = true;
            }
        }

    // When docking without holding Shift, we only allow and preview docking when hovering over a drop rect or over the title bar
    data->IsDropAllowed = (data->SplitDir != ImGuiDir_None) || (data->IsCenterAvailable);
    if (!is_explicit_target && !data->IsSplitDirExplicit && !g.IO.ConfigDockingWithShift)
        data->IsDropAllowed = false;

    // Calculate split area
    data->SplitRatio = 0.0f;
    if (data->SplitDir != ImGuiDir_None)
    {
        ImGuiDir split_dir = data->SplitDir;
        ImGuiAxis split_axis = (split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Right) ? ImGuiAxis_X : ImGuiAxis_Y;
        ImVec2 pos_new, pos_old = data->FutureNode.Pos;
        ImVec2 size_new, size_old = data->FutureNode.Size;
        DockNodeCalcSplitRects(pos_old, size_old, pos_new, size_new, split_dir, payload_window->Size);

        // Calculate split ratio so we can pass it down the docking request
        float split_ratio = ImSaturate(size_new[split_axis] / data->FutureNode.Size[split_axis]);
        data->FutureNode.Pos = pos_new;
        data->FutureNode.Size = size_new;
        data->SplitRatio = (split_dir == ImGuiDir_Right || split_dir == ImGuiDir_Down) ? (1.0f - split_ratio) : (split_ratio);
    }
}

static void ImGui::DockNodePreviewDockRender(ImGuiWindow* host_window, ImGuiDockNode* host_node, ImGuiWindow* root_payload, const ImGuiDockPreviewData* data)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(g.CurrentWindow == host_window);   // Because we rely on font size to calculate tab sizes

    // With this option, we only display the preview on the target viewport, and the payload viewport is made transparent.
    // To compensate for the single layer obstructed by the payload, we'll increase the alpha of the preview nodes.
    const bool is_transparent_payload = g.IO.ConfigDockingTransparentPayload;

    // In case the two windows involved are on different viewports, we will draw the overlay on each of them.
    int overlay_draw_lists_count = 0;
    ImDrawList* overlay_draw_lists[2];
    overlay_draw_lists[overlay_draw_lists_count++] = GetForegroundDrawList(host_window->Viewport);
    if (host_window->Viewport != root_payload->Viewport && !is_transparent_payload)
        overlay_draw_lists[overlay_draw_lists_count++] = GetForegroundDrawList(root_payload->Viewport);

    // Draw main preview rectangle
    const ImU32 overlay_col_main = GetColorU32(ImGuiCol_DockingPreview, is_transparent_payload ? 0.60f : 0.40f);
    const ImU32 overlay_col_drop = GetColorU32(ImGuiCol_DockingPreview, is_transparent_payload ? 0.90f : 0.70f);
    const ImU32 overlay_col_drop_hovered = GetColorU32(ImGuiCol_DockingPreview, is_transparent_payload ? 1.20f : 1.00f);
    const ImU32 overlay_col_lines = GetColorU32(ImGuiCol_NavWindowingHighlight, is_transparent_payload ? 0.80f : 0.60f);

    // Display area preview
    const bool can_preview_tabs = (root_payload->DockNodeAsHost == NULL || root_payload->DockNodeAsHost->Windows.Size > 0);
    if (data->IsDropAllowed)
    {
        ImRect overlay_rect = data->FutureNode.Rect();
        if (data->SplitDir == ImGuiDir_None && can_preview_tabs)
            overlay_rect.Min.y += GetFrameHeight();
        if (data->SplitDir != ImGuiDir_None || data->IsCenterAvailable)
            for (int overlay_n = 0; overlay_n < overlay_draw_lists_count; overlay_n++)
                overlay_draw_lists[overlay_n]->AddRectFilled(overlay_rect.Min, overlay_rect.Max, overlay_col_main, host_window->WindowRounding, CalcRoundingFlagsForRectInRect(overlay_rect, host_window->Rect(), g.Style.DockingSeparatorSize));
    }

    // Display tab shape/label preview unless we are splitting node (it generally makes the situation harder to read)
    if (data->IsDropAllowed && can_preview_tabs && data->SplitDir == ImGuiDir_None && data->IsCenterAvailable)
    {
        // Compute target tab bar geometry so we can locate our preview tabs
        ImRect tab_bar_rect;
        DockNodeCalcTabBarLayout(&data->FutureNode, NULL, &tab_bar_rect, NULL, NULL);
        ImVec2 tab_pos = tab_bar_rect.Min;
        if (host_node && host_node->TabBar)
        {
            if (!host_node->IsHiddenTabBar() && !host_node->IsNoTabBar())
                tab_pos.x += host_node->TabBar->WidthAllTabs + g.Style.ItemInnerSpacing.x; // We don't use OffsetNewTab because when using non-persistent-order tab bar it is incremented with each Tab submission.
            else
                tab_pos.x += g.Style.ItemInnerSpacing.x + TabItemCalcSize(host_node->Windows[0]).x;
        }
        else if (!(host_window->Flags & ImGuiWindowFlags_DockNodeHost))
        {
            tab_pos.x += g.Style.ItemInnerSpacing.x + TabItemCalcSize(host_window).x; // Account for slight offset which will be added when changing from title bar to tab bar
        }

        // Draw tab shape/label preview (payload may be a loose window or a host window carrying multiple tabbed windows)
        if (root_payload->DockNodeAsHost)
            IM_ASSERT(root_payload->DockNodeAsHost->Windows.Size <= root_payload->DockNodeAsHost->TabBar->Tabs.Size);
        ImGuiTabBar* tab_bar_with_payload = root_payload->DockNodeAsHost ? root_payload->DockNodeAsHost->TabBar : NULL;
        const int payload_count = tab_bar_with_payload ? tab_bar_with_payload->Tabs.Size : 1;
        for (int payload_n = 0; payload_n < payload_count; payload_n++)
        {
            // DockNode's TabBar may have non-window Tabs manually appended by user
            ImGuiWindow* payload_window = tab_bar_with_payload ? tab_bar_with_payload->Tabs[payload_n].Window : root_payload;
            if (tab_bar_with_payload && payload_window == NULL)
                continue;
            if (!DockNodeIsDropAllowedOne(payload_window, host_window))
                continue;

            // Calculate the tab bounding box for each payload window
            ImVec2 tab_size = TabItemCalcSize(payload_window);
            ImRect tab_bb(tab_pos.x, tab_pos.y, tab_pos.x + tab_size.x, tab_pos.y + tab_size.y);
            tab_pos.x += tab_size.x + g.Style.ItemInnerSpacing.x;
            const ImU32 overlay_col_text = GetColorU32(payload_window->DockStyle.Colors[ImGuiWindowDockStyleCol_Text]);
            const ImU32 overlay_col_tabs = GetColorU32(payload_window->DockStyle.Colors[ImGuiWindowDockStyleCol_TabSelected]);
            const ImU32 overlay_col_unsaved_marker = GetColorU32(payload_window->DockStyle.Colors[ImGuiWindowDockStyleCol_UnsavedMarker]);
            PushStyleColor(ImGuiCol_Text, overlay_col_text);
            PushStyleColor(ImGuiCol_UnsavedMarker, overlay_col_unsaved_marker);
            for (int overlay_n = 0; overlay_n < overlay_draw_lists_count; overlay_n++)
            {
                ImGuiTabItemFlags tab_flags = (payload_window->Flags & ImGuiWindowFlags_UnsavedDocument) ? ImGuiTabItemFlags_UnsavedDocument : 0;
                if (!tab_bar_rect.Contains(tab_bb))
                    overlay_draw_lists[overlay_n]->PushClipRect(tab_bar_rect.Min, tab_bar_rect.Max);
                TabItemBackground(overlay_draw_lists[overlay_n], tab_bb, tab_flags, overlay_col_tabs);
                TabItemLabelAndCloseButton(overlay_draw_lists[overlay_n], tab_bb, tab_flags, g.Style.FramePadding, payload_window->Name, 0, 0, false, NULL, NULL);
                if (!tab_bar_rect.Contains(tab_bb))
                    overlay_draw_lists[overlay_n]->PopClipRect();
            }
            PopStyleColor(2);
        }
    }

    // Display drop boxes
    const float scale = g.Style._MainScale; // FIXME-DPI
    const float overlay_rounding = ImMax((float)(int)(3.0f * scale), g.Style.FrameRounding);
    for (int dir = ImGuiDir_None; dir < ImGuiDir_COUNT; dir++)
    {
        if (!data->DropRectsDraw[dir + 1].IsInverted())
        {
            ImRect draw_r = data->DropRectsDraw[dir + 1];
            ImRect draw_r_in = draw_r;
            draw_r_in.Expand((float)(int)(-2.0f * scale));
            ImU32 overlay_col = (data->SplitDir == (ImGuiDir)dir && data->IsSplitDirExplicit) ? overlay_col_drop_hovered : overlay_col_drop;
            float thickness = (float)(int)(1.0f * scale);
            for (int overlay_n = 0; overlay_n < overlay_draw_lists_count; overlay_n++)
            {
                ImVec2 center = ImFloor(draw_r_in.GetCenter());
                overlay_draw_lists[overlay_n]->AddRectFilled(draw_r.Min, draw_r.Max, overlay_col, overlay_rounding);
                overlay_draw_lists[overlay_n]->AddRect(draw_r_in.Min, draw_r_in.Max, overlay_col_lines, overlay_rounding, thickness);
                if (dir == ImGuiDir_Left || dir == ImGuiDir_Right)
                    overlay_draw_lists[overlay_n]->AddLineV(center.x, draw_r_in.Min.y, draw_r_in.Max.y, overlay_col_lines, thickness);
                if (dir == ImGuiDir_Up || dir == ImGuiDir_Down)
                    overlay_draw_lists[overlay_n]->AddLineH(draw_r_in.Min.x, draw_r_in.Max.x, center.y, overlay_col_lines, thickness);
            }
        }

        // Stop after ImGuiDir_None
        if ((host_node && (host_node->MergedFlags & ImGuiDockNodeFlags_NoDockingSplit)) || g.IO.ConfigDockingNoSplit)
            return;
    }
}

//-----------------------------------------------------------------------------
// Docking: ImGuiDockNode Tree manipulation functions
//-----------------------------------------------------------------------------
// - DockNodeTreeSplit()
// - DockNodeTreeMerge()
// - DockNodeTreeUpdatePosSize()
// - DockNodeTreeUpdateSplitterFindTouchingNode()
// - DockNodeTreeUpdateSplitter()
// - DockNodeTreeFindFallbackLeafNode()
// - DockNodeTreeFindNodeByPos()
//-----------------------------------------------------------------------------

void ImGui::DockNodeTreeSplit(ImGuiContext* ctx, ImGuiDockNode* parent_node, ImGuiAxis split_axis, int split_inheritor_child_idx, float split_ratio, ImGuiDockNode* new_node)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(split_axis != ImGuiAxis_None);

    ImGuiDockNode* child_0 = (new_node && split_inheritor_child_idx != 0) ? new_node : DockContextAddNode(ctx, 0);
    child_0->ParentNode = parent_node;

    ImGuiDockNode* child_1 = (new_node && split_inheritor_child_idx != 1) ? new_node : DockContextAddNode(ctx, 0);
    child_1->ParentNode = parent_node;

    ImGuiDockNode* child_inheritor = (split_inheritor_child_idx == 0) ? child_0 : child_1;
    DockNodeMoveChildNodes(child_inheritor, parent_node);
    parent_node->ChildNodes[0] = child_0;
    parent_node->ChildNodes[1] = child_1;
    parent_node->ChildNodes[split_inheritor_child_idx]->VisibleWindow = parent_node->VisibleWindow;
    parent_node->SplitAxis = split_axis;
    parent_node->VisibleWindow = NULL;
    parent_node->AuthorityForPos = parent_node->AuthorityForSize = ImGuiDataAuthority_DockNode;

    float size_avail = (parent_node->Size[split_axis] - g.Style.DockingSeparatorSize);
    size_avail = ImMax(size_avail, g.Style.WindowMinSize[split_axis] * 2.0f);
    IM_ASSERT(size_avail > 0.0f); // If you created a node manually with DockBuilderAddNode(), you need to also call DockBuilderSetNodeSize() before splitting.
    child_0->SizeRef = child_1->SizeRef = parent_node->Size;
    child_0->SizeRef[split_axis] = ImTrunc(size_avail * split_ratio);
    child_1->SizeRef[split_axis] = ImTrunc(size_avail - child_0->SizeRef[split_axis]);

    DockNodeMoveWindows(parent_node->ChildNodes[split_inheritor_child_idx], parent_node);
    DockSettingsRenameNodeReferences(parent_node->ID, parent_node->ChildNodes[split_inheritor_child_idx]->ID);
    DockNodeUpdateHasCentralNodeChild(DockNodeGetRootNode(parent_node));
    DockNodeTreeUpdatePosSize(parent_node, parent_node->Pos, parent_node->Size);

    // Flags transfer (e.g. this is where we transfer the ImGuiDockNodeFlags_CentralNode property)
    child_0->SharedFlags = parent_node->SharedFlags & ImGuiDockNodeFlags_SharedFlagsInheritMask_;
    child_1->SharedFlags = parent_node->SharedFlags & ImGuiDockNodeFlags_SharedFlagsInheritMask_;
    child_inheritor->LocalFlags = parent_node->LocalFlags & ImGuiDockNodeFlags_LocalFlagsTransferMask_;
    parent_node->LocalFlags &= ~ImGuiDockNodeFlags_LocalFlagsTransferMask_;
    child_0->UpdateMergedFlags();
    child_1->UpdateMergedFlags();
    parent_node->UpdateMergedFlags();
    if (child_inheritor->IsCentralNode())
        DockNodeGetRootNode(parent_node)->CentralNode = child_inheritor;
}

void ImGui::DockNodeTreeMerge(ImGuiContext* ctx, ImGuiDockNode* parent_node, ImGuiDockNode* merge_lead_child)
{
    // When called from DockContextProcessUndockNode() it is possible that one of the child is NULL.
    ImGuiContext& g = *GImGui;
    ImGuiDockNode* child_0 = parent_node->ChildNodes[0];
    ImGuiDockNode* child_1 = parent_node->ChildNodes[1];
    IM_ASSERT(child_0 || child_1);
    IM_ASSERT(merge_lead_child == child_0 || merge_lead_child == child_1);
    if ((child_0 && child_0->Windows.Size > 0) || (child_1 && child_1->Windows.Size > 0))
    {
        IM_ASSERT(parent_node->TabBar == NULL);
        IM_ASSERT(parent_node->Windows.Size == 0);
    }
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockNodeTreeMerge: 0x%08X + 0x%08X back into parent 0x%08X\n", child_0 ? child_0->ID : 0, child_1 ? child_1->ID : 0, parent_node->ID);

    ImVec2 backup_last_explicit_size = parent_node->SizeRef;
    DockNodeMoveChildNodes(parent_node, merge_lead_child);
    if (child_0)
    {
        DockNodeMoveWindows(parent_node, child_0); // Generally only 1 of the 2 child node will have windows
        DockSettingsRenameNodeReferences(child_0->ID, parent_node->ID);
    }
    if (child_1)
    {
        DockNodeMoveWindows(parent_node, child_1);
        DockSettingsRenameNodeReferences(child_1->ID, parent_node->ID);
    }
    DockNodeApplyPosSizeToWindows(parent_node);
    parent_node->AuthorityForPos = parent_node->AuthorityForSize = parent_node->AuthorityForViewport = ImGuiDataAuthority_Auto;
    parent_node->VisibleWindow = merge_lead_child->VisibleWindow;
    parent_node->SizeRef = backup_last_explicit_size;

    // Flags transfer
    parent_node->LocalFlags &= ~ImGuiDockNodeFlags_LocalFlagsTransferMask_; // Preserve Dockspace flag
    parent_node->LocalFlags |= (child_0 ? child_0->LocalFlags : 0) & ImGuiDockNodeFlags_LocalFlagsTransferMask_;
    parent_node->LocalFlags |= (child_1 ? child_1->LocalFlags : 0) & ImGuiDockNodeFlags_LocalFlagsTransferMask_;
    parent_node->LocalFlagsInWindows = (child_0 ? child_0->LocalFlagsInWindows : 0) | (child_1 ? child_1->LocalFlagsInWindows : 0); // FIXME: Would be more consistent to update from actual windows
    parent_node->UpdateMergedFlags();

    if (child_0)
        DockContextDeleteNode(ctx, child_0);
    if (child_1)
        DockContextDeleteNode(ctx, child_1);
}

// Update Pos/Size for a node hierarchy (don't affect child Windows yet)
// (Depth-first, Pre-Order)
void ImGui::DockNodeTreeUpdatePosSize(ImGuiDockNode* node, ImVec2 pos, ImVec2 size, ImGuiDockNode* only_write_to_single_node)
{
    // During the regular dock node update we write to all nodes.
    // 'only_write_to_single_node' is only set when turning a node visible mid-frame and we need its size right-away.
    ImGuiContext& g = *GImGui;
    const bool write_to_node = only_write_to_single_node == NULL || only_write_to_single_node == node;
    if (write_to_node)
    {
        node->Pos = pos;
        node->Size = size;
    }

    if (node->IsLeafNode())
        return;

    ImGuiDockNode* child_0 = node->ChildNodes[0];
    ImGuiDockNode* child_1 = node->ChildNodes[1];
    ImVec2 child_0_pos = pos, child_1_pos = pos;
    ImVec2 child_0_size = size, child_1_size = size;

    const bool child_0_is_toward_single_node = (only_write_to_single_node != NULL && DockNodeIsInHierarchyOf(only_write_to_single_node, child_0));
    const bool child_1_is_toward_single_node = (only_write_to_single_node != NULL && DockNodeIsInHierarchyOf(only_write_to_single_node, child_1));
    const bool child_0_is_or_will_be_visible = child_0->IsVisible || child_0_is_toward_single_node;
    const bool child_1_is_or_will_be_visible = child_1->IsVisible || child_1_is_toward_single_node;

    if (child_0_is_or_will_be_visible && child_1_is_or_will_be_visible)
    {
        const float spacing = g.Style.DockingSeparatorSize;
        const ImGuiAxis axis = (ImGuiAxis)node->SplitAxis;
        const float size_avail = ImMax(size[axis] - spacing, 0.0f);

        // Size allocation policy
        // 1) The first 0..WindowMinSize[axis]*2 are allocated evenly to both windows.
        const float size_min_each = ImTrunc(ImMin(size_avail, g.Style.WindowMinSize[axis] * 2.0f) * 0.5f);

        // FIXME: Blocks 2) and 3) are essentially doing nearly the same thing.
        // Difference are: write-back to SizeRef; application of a minimum size; rounding before ImTrunc()
        // Clarify and rework differences between Size & SizeRef and purpose of WantLockSizeOnce

        // 2) Process locked absolute size (during a splitter resize we preserve the child of nodes not touching the splitter edge)
        if (child_0->WantLockSizeOnce && !child_1->WantLockSizeOnce)
        {
            child_0_size[axis] = child_0->SizeRef[axis] = ImMin(size_avail - 1.0f, child_0->Size[axis]);
            child_1_size[axis] = child_1->SizeRef[axis] = (size_avail - child_0_size[axis]);
            IM_ASSERT(child_0->SizeRef[axis] > 0.0f && child_1->SizeRef[axis] > 0.0f);
        }
        else if (child_1->WantLockSizeOnce && !child_0->WantLockSizeOnce)
        {
            child_1_size[axis] = child_1->SizeRef[axis] = ImMin(size_avail - 1.0f, child_1->Size[axis]);
            child_0_size[axis] = child_0->SizeRef[axis] = (size_avail - child_1_size[axis]);
            IM_ASSERT(child_0->SizeRef[axis] > 0.0f && child_1->SizeRef[axis] > 0.0f);
        }
        else if (child_0->WantLockSizeOnce && child_1->WantLockSizeOnce)
        {
            // FIXME-DOCK: We cannot honor the requested size, so apply ratio.
            // Currently this path will only be taken if code programmatically sets WantLockSizeOnce
            float split_ratio = child_0_size[axis] / (child_0_size[axis] + child_1_size[axis]);
            child_0_size[axis] = child_0->SizeRef[axis] = ImTrunc(size_avail * split_ratio);
            child_1_size[axis] = child_1->SizeRef[axis] = (size_avail - child_0_size[axis]);
            IM_ASSERT(child_0->SizeRef[axis] > 0.0f && child_1->SizeRef[axis] > 0.0f);
        }

        // 3) If one window is the central node (~ use remaining space, should be made explicit!), use explicit size from the other, and remainder for the central node
        else if (child_0->SizeRef[axis] != 0.0f && child_1->HasCentralNodeChild)
        {
            child_0_size[axis] = ImMin(size_avail - size_min_each, child_0->SizeRef[axis]);
            child_1_size[axis] = (size_avail - child_0_size[axis]);
        }
        else if (child_1->SizeRef[axis] != 0.0f && child_0->HasCentralNodeChild)
        {
            child_1_size[axis] = ImMin(size_avail - size_min_each, child_1->SizeRef[axis]);
            child_0_size[axis] = (size_avail - child_1_size[axis]);
        }
        else
        {
            // 4) Otherwise distribute according to the relative ratio of each SizeRef value
            float split_ratio = child_0->SizeRef[axis] / (child_0->SizeRef[axis] + child_1->SizeRef[axis]);
            child_0_size[axis] = ImMax(size_min_each, ImTrunc(size_avail * split_ratio + 0.5f));
            child_1_size[axis] = (size_avail - child_0_size[axis]);
        }

        child_1_pos[axis] += spacing + child_0_size[axis];
    }

    if (only_write_to_single_node == NULL)
        child_0->WantLockSizeOnce = child_1->WantLockSizeOnce = false;

    const bool child_0_recurse = only_write_to_single_node ? child_0_is_toward_single_node : child_0->IsVisible;
    const bool child_1_recurse = only_write_to_single_node ? child_1_is_toward_single_node : child_1->IsVisible;
    if (child_0_recurse)
        DockNodeTreeUpdatePosSize(child_0, child_0_pos, child_0_size);
    if (child_1_recurse)
        DockNodeTreeUpdatePosSize(child_1, child_1_pos, child_1_size);
}

static void DockNodeTreeUpdateSplitterFindTouchingNode(ImGuiDockNode* node, ImGuiAxis axis, int side, ImVector<ImGuiDockNode*>* touching_nodes)
{
    if (node->IsLeafNode())
    {
        touching_nodes->push_back(node);
        return;
    }
    if (node->ChildNodes[0]->IsVisible)
        if (node->SplitAxis != axis || side == 0 || !node->ChildNodes[1]->IsVisible)
            DockNodeTreeUpdateSplitterFindTouchingNode(node->ChildNodes[0], axis, side, touching_nodes);
    if (node->ChildNodes[1]->IsVisible)
        if (node->SplitAxis != axis || side == 1 || !node->ChildNodes[0]->IsVisible)
            DockNodeTreeUpdateSplitterFindTouchingNode(node->ChildNodes[1], axis, side, touching_nodes);
}

// (Depth-First, Pre-Order)
void ImGui::DockNodeTreeUpdateSplitter(ImGuiDockNode* node)
{
    if (node->IsLeafNode())
        return;

    ImGuiContext& g = *GImGui;

    ImGuiDockNode* child_0 = node->ChildNodes[0];
    ImGuiDockNode* child_1 = node->ChildNodes[1];
    if (child_0->IsVisible && child_1->IsVisible)
    {
        // Bounding box of the splitter cover the space between both nodes (w = Spacing, h = Size[xy^1] for when splitting horizontally)
        const ImGuiAxis axis = (ImGuiAxis)node->SplitAxis;
        IM_ASSERT(axis != ImGuiAxis_None);
        ImRect bb;
        bb.Min = child_0->Pos;
        bb.Max = child_1->Pos;
        bb.Min[axis] += child_0->Size[axis];
        bb.Max[axis ^ 1] += child_1->Size[axis ^ 1];
        //if (g.IO.KeyCtrl) GetForegroundDrawList(g.CurrentWindow->Viewport)->AddRect(bb.Min, bb.Max, IM_COL32(255,0,255,255));

        const ImGuiDockNodeFlags merged_flags = child_0->MergedFlags | child_1->MergedFlags; // Merged flags for BOTH childs
        const ImGuiDockNodeFlags no_resize_axis_flag = (axis == ImGuiAxis_X) ? ImGuiDockNodeFlags_NoResizeX : ImGuiDockNodeFlags_NoResizeY;
        if ((merged_flags & ImGuiDockNodeFlags_NoResize) || (merged_flags & no_resize_axis_flag))
        {
            ImGuiWindow* window = g.CurrentWindow;
            window->DrawList->AddRectFilled(bb.Min, bb.Max, GetColorU32(ImGuiCol_Separator), g.Style.FrameRounding);
        }
        else
        {
            //bb.Min[axis] += 1; // Display a little inward so highlight doesn't connect with nearby tabs on the neighbor node.
            //bb.Max[axis] -= 1;
            PushID(node->ID);

            // Find resizing limits by gathering list of nodes that are touching the splitter line.
            ImVector<ImGuiDockNode*> touching_nodes[2];
            float min_size = g.Style.WindowMinSize[axis];
            float resize_limits[2];
            resize_limits[0] = node->ChildNodes[0]->Pos[axis] + min_size;
            resize_limits[1] = node->ChildNodes[1]->Pos[axis] + node->ChildNodes[1]->Size[axis] - min_size;

            ImGuiID splitter_id = GetID("##Splitter");
            if (g.ActiveId == splitter_id) // Only process when splitter is active
            {
                DockNodeTreeUpdateSplitterFindTouchingNode(child_0, axis, 1, &touching_nodes[0]);
                DockNodeTreeUpdateSplitterFindTouchingNode(child_1, axis, 0, &touching_nodes[1]);
                for (int touching_node_n = 0; touching_node_n < touching_nodes[0].Size; touching_node_n++)
                    resize_limits[0] = ImMax(resize_limits[0], touching_nodes[0][touching_node_n]->Rect().Min[axis] + min_size);
                for (int touching_node_n = 0; touching_node_n < touching_nodes[1].Size; touching_node_n++)
                    resize_limits[1] = ImMin(resize_limits[1], touching_nodes[1][touching_node_n]->Rect().Max[axis] - min_size);

                // [DEBUG] Render touching nodes & limits
                /*
                ImDrawList* draw_list = node->HostWindow ? GetForegroundDrawList(node->HostWindow) : GetForegroundDrawList(GetMainViewport());
                for (int n = 0; n < 2; n++)
                {
                    for (int touching_node_n = 0; touching_node_n < touching_nodes[n].Size; touching_node_n++)
                        draw_list->AddRect(touching_nodes[n][touching_node_n]->Pos, touching_nodes[n][touching_node_n]->Pos + touching_nodes[n][touching_node_n]->Size, IM_COL32(0, 255, 0, 255));
                    if (axis == ImGuiAxis_X)
                        draw_list->AddLine(ImVec2(resize_limits[n], node->ChildNodes[n]->Pos.y), ImVec2(resize_limits[n], node->ChildNodes[n]->Pos.y + node->ChildNodes[n]->Size.y), IM_COL32(255, 0, 255, 255), 3.0f);
                    else
                        draw_list->AddLine(ImVec2(node->ChildNodes[n]->Pos.x, resize_limits[n]), ImVec2(node->ChildNodes[n]->Pos.x + node->ChildNodes[n]->Size.x, resize_limits[n]), IM_COL32(255, 0, 255, 255), 3.0f);
                }
                */
            }

            // Use a short delay before highlighting the splitter (and changing the mouse cursor) in order for regular mouse movement to not highlight many splitters
            float cur_size_0 = child_0->Size[axis];
            float cur_size_1 = child_1->Size[axis];
            float min_size_0 = resize_limits[0] - child_0->Pos[axis];
            float min_size_1 = child_1->Pos[axis] + child_1->Size[axis] - resize_limits[1];
            ImU32 bg_col = GetColorU32(ImGuiCol_WindowBg);
            if (SplitterBehavior(bb, GetID("##Splitter"), axis, &cur_size_0, &cur_size_1, min_size_0, min_size_1, g.WindowsBorderHoverPadding, WINDOWS_RESIZE_FROM_EDGES_FEEDBACK_TIMER, bg_col))
            {
                if (touching_nodes[0].Size > 0 && touching_nodes[1].Size > 0)
                {
                    child_0->Size[axis] = child_0->SizeRef[axis] = cur_size_0;
                    child_1->Pos[axis] -= cur_size_1 - child_1->Size[axis];
                    child_1->Size[axis] = child_1->SizeRef[axis] = cur_size_1;

                    // Lock the size of every node that is a sibling of the node we are touching
                    // This might be less desirable if we can merge sibling of a same axis into the same parental level.
                    for (int side_n = 0; side_n < 2; side_n++)
                        for (int touching_node_n = 0; touching_node_n < touching_nodes[side_n].Size; touching_node_n++)
                        {
                            ImGuiDockNode* touching_node = touching_nodes[side_n][touching_node_n];
                            //ImDrawList* draw_list = node->HostWindow ? GetForegroundDrawList(node->HostWindow) : GetForegroundDrawList(GetMainViewport());
                            //draw_list->AddRect(touching_node->Pos, touching_node->Pos + touching_node->Size, IM_COL32(255, 128, 0, 255));
                            while (touching_node->ParentNode != node)
                            {
                                if (touching_node->ParentNode->SplitAxis == axis)
                                {
                                    // Mark other node so its size will be preserved during the upcoming call to DockNodeTreeUpdatePosSize().
                                    ImGuiDockNode* node_to_preserve = touching_node->ParentNode->ChildNodes[side_n];
                                    node_to_preserve->WantLockSizeOnce = true;
                                    //draw_list->AddRect(touching_node->Pos, touching_node->Rect().Max, IM_COL32(255, 0, 0, 255));
                                    //draw_list->AddRectFilled(node_to_preserve->Pos, node_to_preserve->Rect().Max, IM_COL32(0, 255, 0, 100));
                                }
                                touching_node = touching_node->ParentNode;
                            }
                        }

                    DockNodeTreeUpdatePosSize(child_0, child_0->Pos, child_0->Size);
                    DockNodeTreeUpdatePosSize(child_1, child_1->Pos, child_1->Size);
                    MarkIniSettingsDirty();
                }
            }
            PopID();
        }
    }

    if (child_0->IsVisible)
        DockNodeTreeUpdateSplitter(child_0);
    if (child_1->IsVisible)
        DockNodeTreeUpdateSplitter(child_1);
}

ImGuiDockNode* ImGui::DockNodeTreeFindFallbackLeafNode(ImGuiDockNode* node)
{
    if (node->IsLeafNode())
        return node;
    if (ImGuiDockNode* leaf_node = DockNodeTreeFindFallbackLeafNode(node->ChildNodes[0]))
        return leaf_node;
    if (ImGuiDockNode* leaf_node = DockNodeTreeFindFallbackLeafNode(node->ChildNodes[1]))
        return leaf_node;
    return NULL;
}

ImGuiDockNode* ImGui::DockNodeTreeFindVisibleNodeByPos(ImGuiDockNode* node, ImVec2 pos)
{
    if (!node->IsVisible)
        return NULL;

    const float dock_spacing = 0.0f;// g.Style.ItemInnerSpacing.x; // FIXME: Relation to DOCKING_SPLITTER_SIZE?
    ImRect r(node->Pos, node->Pos + node->Size);
    r.Expand(dock_spacing * 0.5f);
    bool inside = r.Contains(pos);
    if (!inside)
        return NULL;

    if (node->IsLeafNode())
        return node;
    if (ImGuiDockNode* hovered_node = DockNodeTreeFindVisibleNodeByPos(node->ChildNodes[0], pos))
        return hovered_node;
    if (ImGuiDockNode* hovered_node = DockNodeTreeFindVisibleNodeByPos(node->ChildNodes[1], pos))
        return hovered_node;

    // This means we are hovering over the splitter/spacing of a parent node
    return node;
}

//-----------------------------------------------------------------------------
// Docking: Public Functions (SetWindowDock, DockSpace, DockSpaceOverViewport)
//-----------------------------------------------------------------------------
// - SetWindowDock() [Internal]
// - DockSpace()
// - DockSpaceOverViewport()
//-----------------------------------------------------------------------------

// [Internal] Called via SetNextWindowDockID()
void ImGui::SetWindowDock(ImGuiWindow* window, ImGuiID dock_id, ImGuiCond cond)
{
    // Test condition (NB: bit 0 is always true) and clear flags for next time
    if (cond && (window->SetWindowDockAllowFlags & cond) == 0)
        return;
    window->SetWindowDockAllowFlags &= ~(ImGuiCond_Once | ImGuiCond_FirstUseEver | ImGuiCond_Appearing);

    if (window->DockId == dock_id)
        return;

    // If the user attempt to set a dock id that is a split node, we'll dig within to find a suitable docking spot
    ImGuiContext& g = *GImGui;
    if (ImGuiDockNode* new_node = DockContextFindNodeByID(&g, dock_id))
        if (new_node->IsSplitNode())
        {
            // Policy: Find central node or latest focused node. We first move back to our root node.
            new_node = DockNodeGetRootNode(new_node);
            if (new_node->CentralNode)
            {
                IM_ASSERT(new_node->CentralNode->IsCentralNode());
                dock_id = new_node->CentralNode->ID;
            }
            else
            {
                dock_id = new_node->LastFocusedNodeId;
            }
        }

    if (window->DockId == dock_id)
        return;

    if (window->DockNode)
        DockNodeRemoveWindow(window->DockNode, window, 0);
    window->DockId = dock_id;
}

// Create an explicit dockspace node within an existing window. Also expose dock node flags and creates a CentralNode by default.
// The Central Node is always displayed even when empty and shrink/extend according to the requested size of its neighbors.
// DockSpace() needs to be submitted _before_ any window they can host. If you use a dockspace, submit it early in your app.
// When ImGuiDockNodeFlags_KeepAliveOnly is set, nothing is submitted in the current window (function may be called from any location).
ImGuiID ImGui::DockSpace(ImGuiID dockspace_id, const ImVec2& size_arg, ImGuiDockNodeFlags flags, const ImGuiWindowClass* window_class)
{
    ImGuiContext& g = *GImGui;
    ImGuiWindow* window = GetCurrentWindowRead();
    if (!(g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable))
        return 0;

    // Early out if parent window is hidden/collapsed
    // This is faster but also DockNodeUpdateTabBar() relies on TabBarLayout() running (which won't if SkipItems=true) to set NextSelectedTabId = 0). See #2960.
    // If for whichever reason this is causing problem we would need to ensure that DockNodeUpdateTabBar() ends up clearing NextSelectedTabId even if SkipItems=true.
    if (window->SkipItems)
        flags |= ImGuiDockNodeFlags_KeepAliveOnly;
    if ((flags & ImGuiDockNodeFlags_KeepAliveOnly) == 0)
        window = GetCurrentWindow(); // call to set window->WriteAccessed = true;

    IM_ASSERT((flags & ImGuiDockNodeFlags_DockSpace) == 0); // Flag is automatically set by DockSpace() as LocalFlags, not SharedFlags!
    IM_ASSERT((flags & ImGuiDockNodeFlags_CentralNode) == 0); // Flag is automatically set by DockSpace() as LocalFlags, not SharedFlags! (#8145)

    IM_ASSERT(dockspace_id != 0);
    ImGuiDockNode* node = DockContextFindNodeByID(&g, dockspace_id);
    if (node == NULL)
    {
        IMGUI_DEBUG_LOG_DOCKING("[docking] DockSpace: dockspace node 0x%08X created\n", dockspace_id);
        node = DockContextAddNode(&g, dockspace_id);
        node->SetLocalFlags(ImGuiDockNodeFlags_CentralNode);
    }
    if (window_class && window_class->ClassId != node->WindowClass.ClassId)
        IMGUI_DEBUG_LOG_DOCKING("[docking] DockSpace: dockspace node 0x%08X: setup WindowClass 0x%08X -> 0x%08X\n", dockspace_id, node->WindowClass.ClassId, window_class->ClassId);
    node->SharedFlags = flags;
    node->WindowClass = window_class ? *window_class : ImGuiWindowClass();

    // When a DockSpace transitioned form implicit to explicit this may be called a second time
    // It is possible that the node has already been claimed by a docked window which appeared before the DockSpace() node, so we overwrite IsDockSpace again.
    if (node->LastFrameActive == g.FrameCount && !(flags & ImGuiDockNodeFlags_KeepAliveOnly))
    {
        IM_ASSERT(node->IsDockSpace() == false && "Cannot call DockSpace() twice a frame with the same ID");
        node->SetLocalFlags(node->LocalFlags | ImGuiDockNodeFlags_DockSpace);
        return dockspace_id;
    }
    node->SetLocalFlags(node->LocalFlags | ImGuiDockNodeFlags_DockSpace);

    // Keep alive mode, this is allow windows docked into this node so stay docked even if they are not visible
    if (flags & ImGuiDockNodeFlags_KeepAliveOnly)
    {
        node->LastFrameAlive = g.FrameCount;
        return dockspace_id;
    }

    const ImVec2 content_avail = GetContentRegionAvail();
    ImVec2 size = ImTrunc(size_arg);
    if (size.x <= 0.0f)
        size.x = ImMax(content_avail.x + size.x, 4.0f); // Arbitrary minimum child size (0.0f causing too much issues)
    if (size.y <= 0.0f)
        size.y = ImMax(content_avail.y + size.y, 4.0f);
    IM_ASSERT(size.x > 0.0f && size.y > 0.0f);

    node->Pos = window->DC.CursorPos;
    node->Size = node->SizeRef = size;
    SetNextWindowPos(node->Pos);
    SetNextWindowSize(node->Size);
    g.NextWindowData.PosUndock = false;

    // FIXME-DOCK: Why do we need a child window to host a dockspace, could we host it in the existing window?
    // FIXME-DOCK: What is the reason for not simply calling BeginChild()? (OK to have a reason but should be commented)
    ImGuiWindowFlags window_flags = ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_DockNodeHost;
    window_flags |= ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoTitleBar;
    window_flags |= ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse;
    window_flags |= ImGuiWindowFlags_NoBackground;

    char title[256];
    ImFormatString(title, IM_COUNTOF(title), "%s/DockSpace_%08X", window->Name, dockspace_id);

    PushStyleVar(ImGuiStyleVar_ChildBorderSize, 0.0f);
    Begin(title, NULL, window_flags);
    PopStyleVar();

    ImGuiWindow* host_window = g.CurrentWindow;
    DockNodeSetupHostWindow(node, host_window);
    host_window->ChildId = window->GetID(title);
    node->OnlyNodeWithWindows = NULL;

    IM_ASSERT(node->IsRootNode());

    // We need to handle the rare case were a central node is missing.
    // This can happen if the node was first created manually with DockBuilderAddNode() but _without_ the ImGuiDockNodeFlags_Dockspace.
    // Doing it correctly would set the _CentralNode flags, which would then propagate according to subsequent split.
    // It would also be ambiguous to attempt to assign a central node while there are split nodes, so we wait until there's a single node remaining.
    // The specific sub-property of _CentralNode we are interested in recovering here is the "Don't delete when empty" property,
    // as it doesn't make sense for an empty dockspace to not have this property.
    if (node->IsLeafNode() && !node->IsCentralNode())
        node->SetLocalFlags(node->LocalFlags | ImGuiDockNodeFlags_CentralNode);

    // Update the node
    DockNodeUpdate(node);

    End();

    ImRect bb(node->Pos, node->Pos + size);
    ItemSize(size);
    ItemAdd(bb, dockspace_id, NULL, ImGuiItemFlags_NoNav); // Not a nav point (could be, would need to draw the nav rect and replicate/refactor activation from BeginChild(), but seems like CTRL+Tab works better here?)
    if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HoveredRect) && IsWindowChildOf(g.HoveredWindow, host_window, false, true)) // To fullfill IsItemHovered(), similar to EndChild()
        g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredWindow;

    return dockspace_id;
}

// Tips: Use with ImGuiDockNodeFlags_PassthruCentralNode!
// The limitation with this call is that your window won't have a local menu bar, but you can also use BeginMainMenuBar().
// Even though we could pass window flags, it would also require the user to be able to call BeginMenuBar() somehow meaning we can't Begin/End in a single function.
// If you really want a menu bar inside the same window as the one hosting the dockspace, you will need to copy this code somewhere and tweak it.
ImGuiID ImGui::DockSpaceOverViewport(ImGuiID dockspace_id, const ImGuiViewport* viewport, ImGuiDockNodeFlags dockspace_flags, const ImGuiWindowClass* window_class)
{
    if (viewport == NULL)
        viewport = GetMainViewport();

    // Submit a window filling the entire viewport
    SetNextWindowPos(viewport->WorkPos);
    SetNextWindowSize(viewport->WorkSize);
    SetNextWindowViewport(viewport->ID);

    ImGuiWindowFlags host_window_flags = 0;
    host_window_flags |= ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoDocking;
    host_window_flags |= ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus;
    if (dockspace_flags & ImGuiDockNodeFlags_PassthruCentralNode)
        host_window_flags |= ImGuiWindowFlags_NoBackground;

    // FIXME-OPT: When using ImGuiDockNodeFlags_KeepAliveOnly with DockSpaceOverViewport() we might be able to spare submitting the window,
    // since DockSpace() with that flag doesn't need a window. We'd only need to compute the default ID accordingly.
    if (dockspace_flags & ImGuiDockNodeFlags_KeepAliveOnly)
        host_window_flags |= ImGuiWindowFlags_NoMouseInputs;

    char label[32];
    ImFormatString(label, IM_COUNTOF(label), "WindowOverViewport_%08X", viewport->ID);

    PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
    PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
    PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0.0f, 0.0f));
    Begin(label, NULL, host_window_flags);
    PopStyleVar(3);

    // Submit the dockspace
    if (dockspace_id == 0)
        dockspace_id = GetID("DockSpace");
    DockSpace(dockspace_id, ImVec2(0.0f, 0.0f), dockspace_flags, window_class);

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
// - DockBuilderDockWindow()
// - DockBuilderGetNode()
// - DockBuilderSetNodePos()
// - DockBuilderSetNodeSize()
// - DockBuilderAddNode()
// - DockBuilderRemoveNode()
// - DockBuilderRemoveNodeChildNodes()
// - DockBuilderRemoveNodeDockedWindows()
// - DockBuilderSplitNode()
// - DockBuilderCopyNodeRec()
// - DockBuilderCopyNode()
// - DockBuilderCopyWindowSettings()
// - DockBuilderCopyDockSpace()
// - DockBuilderFinish()
//-----------------------------------------------------------------------------

void ImGui::DockBuilderDockWindow(const char* window_name, ImGuiID node_id)
{
    // We don't preserve relative order of multiple docked windows (by clearing DockOrder back to -1)
    ImGuiContext& g = *GImGui; IM_UNUSED(g);
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockBuilderDockWindow '%s' to node 0x%08X\n", window_name, node_id);
    ImGuiID window_id = ImHashStr(window_name);
    if (ImGuiWindow* window = FindWindowByID(window_id))
    {
        // Apply to created window
        ImGuiID prev_node_id = window->DockId;
        SetWindowDock(window, node_id, ImGuiCond_Always);
        if (window->DockId != prev_node_id)
            window->DockOrder = -1;
    }
    else
    {
        // Apply to settings
        ImGuiWindowSettings* settings = FindWindowSettingsByID(window_id);
        if (settings == NULL)
            settings = CreateNewWindowSettings(window_name);
        if (settings->DockId != node_id)
            settings->DockOrder = -1;
        settings->DockId = node_id;
    }
}

ImGuiDockNode* ImGui::DockBuilderGetNode(ImGuiID node_id)
{
    ImGuiContext& g = *GImGui;
    return DockContextFindNodeByID(&g, node_id);
}

void ImGui::DockBuilderSetNodePos(ImGuiID node_id, ImVec2 pos)
{
    ImGuiContext& g = *GImGui;
    ImGuiDockNode* node = DockContextFindNodeByID(&g, node_id);
    if (node == NULL)
        return;
    node->Pos = pos;
    node->AuthorityForPos = ImGuiDataAuthority_DockNode;
}

void ImGui::DockBuilderSetNodeSize(ImGuiID node_id, ImVec2 size)
{
    ImGuiContext& g = *GImGui;
    ImGuiDockNode* node = DockContextFindNodeByID(&g, node_id);
    if (node == NULL)
        return;
    IM_ASSERT(size.x > 0.0f && size.y > 0.0f);
    node->Size = node->SizeRef = size;
    node->AuthorityForSize = ImGuiDataAuthority_DockNode;
}

// Make sure to use the ImGuiDockNodeFlags_DockSpace flag to create a dockspace node! Otherwise this will create a floating node!
// - Floating node: you can then call DockBuilderSetNodePos()/DockBuilderSetNodeSize() to position and size the floating node.
// - Dockspace node: calling DockBuilderSetNodePos() is unnecessary.
// - If you intend to split a node immediately after creation using DockBuilderSplitNode(), make sure to call DockBuilderSetNodeSize() beforehand!
//   For various reason, the splitting code currently needs a base size otherwise space may not be allocated as precisely as you would expect.
// - Use (id == 0) to let the system allocate a node identifier.
// - Existing node with a same id will be removed.
ImGuiID ImGui::DockBuilderAddNode(ImGuiID node_id, ImGuiDockNodeFlags flags)
{
    ImGuiContext& g = *GImGui; IM_UNUSED(g);
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockBuilderAddNode 0x%08X flags=%08X\n", node_id, flags);

    if (node_id != 0)
        DockBuilderRemoveNode(node_id);

    ImGuiDockNode* node = NULL;
    if (flags & ImGuiDockNodeFlags_DockSpace)
    {
        DockSpace(node_id, ImVec2(0, 0), (flags & ~ImGuiDockNodeFlags_DockSpace) | ImGuiDockNodeFlags_KeepAliveOnly);
        node = DockContextFindNodeByID(&g, node_id);
    }
    else
    {
        node = DockContextAddNode(&g, node_id);
        node->SetLocalFlags(flags);
    }
    node->LastFrameAlive = g.FrameCount;   // Set this otherwise BeginDocked will undock during the same frame.
    return node->ID;
}

void ImGui::DockBuilderRemoveNode(ImGuiID node_id)
{
    ImGuiContext& g = *GImGui; IM_UNUSED(g);
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockBuilderRemoveNode 0x%08X\n", node_id);

    ImGuiDockNode* node = DockContextFindNodeByID(&g, node_id);
    if (node == NULL)
        return;
    DockBuilderRemoveNodeDockedWindows(node_id, true);
    DockBuilderRemoveNodeChildNodes(node_id);
    // Node may have moved or deleted if e.g. any merge happened
    node = DockContextFindNodeByID(&g, node_id);
    if (node == NULL)
        return;
    if (node->IsCentralNode() && node->ParentNode)
        node->ParentNode->SetLocalFlags(node->ParentNode->LocalFlags | ImGuiDockNodeFlags_CentralNode);
    DockContextRemoveNode(&g, node, true);
}

// root_id = 0 to remove all, root_id != 0 to remove child of given node.
void ImGui::DockBuilderRemoveNodeChildNodes(ImGuiID root_id)
{
    ImGuiContext& g = *GImGui;
    ImGuiDockContext* dc = &g.DockContext;

    ImGuiDockNode* root_node = root_id ? DockContextFindNodeByID(&g, root_id) : NULL;
    if (root_id && root_node == NULL)
        return;
    bool has_central_node = false;

    ImGuiDataAuthority backup_root_node_authority_for_pos = root_node ? (ImGuiDataAuthority_)root_node->AuthorityForPos : ImGuiDataAuthority_Auto;
    ImGuiDataAuthority backup_root_node_authority_for_size = root_node ? (ImGuiDataAuthority_)root_node->AuthorityForSize : ImGuiDataAuthority_Auto;

    // Process active windows
    ImVector<ImGuiDockNode*> nodes_to_remove;
    for (int n = 0; n < dc->Nodes.Data.Size; n++)
        if (ImGuiDockNode* node = (ImGuiDockNode*)dc->Nodes.Data[n].val_p)
        {
            bool want_removal = (root_id == 0) || (node->ID != root_id && DockNodeGetRootNode(node)->ID == root_id);
            if (want_removal)
            {
                if (node->IsCentralNode())
                    has_central_node = true;
                if (root_id != 0)
                    DockContextQueueNotifyRemovedNode(&g, node);
                if (root_node)
                {
                    DockNodeMoveWindows(root_node, node);
                    DockSettingsRenameNodeReferences(node->ID, root_node->ID);
                }
                nodes_to_remove.push_back(node);
            }
        }

    // DockNodeMoveWindows->DockNodeAddWindow will normally set those when reaching two windows (which is only adequate during interactive merge)
    // Make sure we don't lose our current pos/size. (FIXME-DOCK: Consider tidying up that code in DockNodeAddWindow instead)
    if (root_node)
    {
        root_node->AuthorityForPos = (ImU8)backup_root_node_authority_for_pos;
        root_node->AuthorityForSize = (ImU8)backup_root_node_authority_for_size;
    }

    // Apply to settings
    for (ImGuiWindowSettings* settings = g.SettingsWindows.begin(); settings != NULL; settings = g.SettingsWindows.next_chunk(settings))
        if (ImGuiID window_settings_dock_id = settings->DockId)
            for (int n = 0; n < nodes_to_remove.Size; n++)
                if (nodes_to_remove[n]->ID == window_settings_dock_id)
                {
                    settings->DockId = root_id;
                    break;
                }

    // Not really efficient, but easier to destroy a whole hierarchy considering DockContextRemoveNode is attempting to merge nodes
    if (nodes_to_remove.Size > 1)
        ImQsort(nodes_to_remove.Data, nodes_to_remove.Size, sizeof(ImGuiDockNode*), DockNodeComparerDepthMostFirst);
    for (int n = 0; n < nodes_to_remove.Size; n++)
        DockContextRemoveNode(&g, nodes_to_remove[n], false);

    if (root_id == 0)
    {
        dc->Nodes.Clear();
        dc->Requests.clear();
    }
    else if (has_central_node)
    {
        root_node->CentralNode = root_node;
        root_node->SetLocalFlags(root_node->LocalFlags | ImGuiDockNodeFlags_CentralNode);
    }
}

void ImGui::DockBuilderRemoveNodeDockedWindows(ImGuiID root_id, bool clear_settings_refs)
{
    // Clear references in settings
    ImGuiContext& g = *GImGui;
    if (clear_settings_refs)
    {
        for (ImGuiWindowSettings* settings = g.SettingsWindows.begin(); settings != NULL; settings = g.SettingsWindows.next_chunk(settings))
        {
            bool want_removal = (root_id == 0) || (settings->DockId == root_id);
            if (!want_removal && settings->DockId != 0)
                if (ImGuiDockNode* node = DockContextFindNodeByID(&g, settings->DockId))
                    if (DockNodeGetRootNode(node)->ID == root_id)
                        want_removal = true;
            if (want_removal)
                settings->DockId = 0;
        }
    }

    // Clear references in windows
    for (int n = 0; n < g.Windows.Size; n++)
    {
        ImGuiWindow* window = g.Windows[n];
        bool want_removal = (root_id == 0) || (window->DockNode && DockNodeGetRootNode(window->DockNode)->ID == root_id) || (window->DockNodeAsHost && window->DockNodeAsHost->ID == root_id);
        if (want_removal)
        {
            const ImGuiID backup_dock_id = window->DockId;
            IM_UNUSED(backup_dock_id);
            DockContextProcessUndockWindow(&g, window, clear_settings_refs);
            if (!clear_settings_refs)
                IM_ASSERT(window->DockId == backup_dock_id);
        }
    }
}

// If 'out_id_at_dir' or 'out_id_at_opposite_dir' are non NULL, the function will write out the ID of the two new nodes created.
// Return value is ID of the node at the specified direction, so same as (*out_id_at_dir) if that pointer is set.
// FIXME-DOCK: We are not exposing nor using split_outer.
ImGuiID ImGui::DockBuilderSplitNode(ImGuiID id, ImGuiDir split_dir, float size_ratio_for_node_at_dir, ImGuiID* out_id_at_dir, ImGuiID* out_id_at_opposite_dir)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(split_dir != ImGuiDir_None);
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockBuilderSplitNode: node 0x%08X, split_dir %d\n", id, split_dir);

    ImGuiDockNode* node = DockContextFindNodeByID(&g, id);
    if (node == NULL)
    {
        IM_ASSERT(node != NULL);
        return 0;
    }

    ImGuiDockRequest req;
    req.Type = ImGuiDockRequestType_Split;
    req.DockTargetWindow = NULL;
    req.DockTargetNode = node;
    req.DockPayload = NULL;
    req.DockSplitDir = split_dir;
    req.DockSplitRatio = ImSaturate((split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? size_ratio_for_node_at_dir : 1.0f - size_ratio_for_node_at_dir);
    req.DockSplitOuter = false;
    DockContextProcessDock(&g, &req);

    ImGuiID id_at_dir = node->ChildNodes[(split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? 0 : 1]->ID;
    ImGuiID id_at_opposite_dir = node->ChildNodes[(split_dir == ImGuiDir_Left || split_dir == ImGuiDir_Up) ? 1 : 0]->ID;
    if (out_id_at_dir)
        *out_id_at_dir = id_at_dir;
    if (out_id_at_opposite_dir)
        *out_id_at_opposite_dir = id_at_opposite_dir;
    return id_at_dir;
}

static ImGuiDockNode* DockBuilderCopyNodeRec(ImGuiDockNode* src_node, ImGuiID dst_node_id_if_known, ImVector<ImGuiID>* out_node_remap_pairs)
{
    ImGuiContext& g = *GImGui;
    ImGuiDockNode* dst_node = ImGui::DockContextAddNode(&g, dst_node_id_if_known);
    dst_node->SharedFlags = src_node->SharedFlags;
    dst_node->LocalFlags = src_node->LocalFlags;
    dst_node->LocalFlagsInWindows = ImGuiDockNodeFlags_None;
    dst_node->Pos = src_node->Pos;
    dst_node->Size = src_node->Size;
    dst_node->SizeRef = src_node->SizeRef;
    dst_node->SplitAxis = src_node->SplitAxis;
    dst_node->UpdateMergedFlags();

    out_node_remap_pairs->push_back(src_node->ID);
    out_node_remap_pairs->push_back(dst_node->ID);

    for (int child_n = 0; child_n < IM_COUNTOF(src_node->ChildNodes); child_n++)
        if (src_node->ChildNodes[child_n])
        {
            dst_node->ChildNodes[child_n] = DockBuilderCopyNodeRec(src_node->ChildNodes[child_n], 0, out_node_remap_pairs);
            dst_node->ChildNodes[child_n]->ParentNode = dst_node;
        }

    IMGUI_DEBUG_LOG_DOCKING("[docking] Fork node %08X -> %08X (%d childs)\n", src_node->ID, dst_node->ID, dst_node->IsSplitNode() ? 2 : 0);
    return dst_node;
}

void ImGui::DockBuilderCopyNode(ImGuiID src_node_id, ImGuiID dst_node_id, ImVector<ImGuiID>* out_node_remap_pairs)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(src_node_id != 0);
    IM_ASSERT(dst_node_id != 0);
    IM_ASSERT(out_node_remap_pairs != NULL);

    DockBuilderRemoveNode(dst_node_id);

    ImGuiDockNode* src_node = DockContextFindNodeByID(&g, src_node_id);
    IM_ASSERT(src_node != NULL);

    out_node_remap_pairs->clear();
    DockBuilderCopyNodeRec(src_node, dst_node_id, out_node_remap_pairs);

    IM_ASSERT((out_node_remap_pairs->Size % 2) == 0);
}

void ImGui::DockBuilderCopyWindowSettings(const char* src_name, const char* dst_name)
{
    ImGuiWindow* src_window = FindWindowByName(src_name);
    if (src_window == NULL)
        return;
    if (ImGuiWindow* dst_window = FindWindowByName(dst_name))
    {
        dst_window->Pos = src_window->Pos;
        dst_window->Size = src_window->Size;
        dst_window->SizeFull = src_window->SizeFull;
        dst_window->Collapsed = src_window->Collapsed;
    }
    else
    {
        ImGuiWindowSettings* dst_settings = FindWindowSettingsByID(ImHashStr(dst_name));
        if (!dst_settings)
            dst_settings = CreateNewWindowSettings(dst_name);
        ImVec2ih window_pos_2ih = ImVec2ih(src_window->Pos);
        if (src_window->ViewportId != 0 && src_window->ViewportId != IMGUI_VIEWPORT_DEFAULT_ID)
        {
            dst_settings->ViewportPos = window_pos_2ih;
            dst_settings->ViewportId = src_window->ViewportId;
            dst_settings->Pos = ImVec2ih(0, 0);
        }
        else
        {
            dst_settings->Pos = window_pos_2ih;
        }
        dst_settings->Size = ImVec2ih(src_window->SizeFull);
        dst_settings->Collapsed = src_window->Collapsed;
    }
}

// FIXME: Will probably want to change this signature, in particular how the window remapping pairs are passed.
void ImGui::DockBuilderCopyDockSpace(ImGuiID src_dockspace_id, ImGuiID dst_dockspace_id, ImVector<const char*>* in_window_remap_pairs)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(src_dockspace_id != 0);
    IM_ASSERT(dst_dockspace_id != 0);
    IM_ASSERT(in_window_remap_pairs != NULL);
    IM_ASSERT((in_window_remap_pairs->Size % 2) == 0);

    // Duplicate entire dock
    // FIXME: When overwriting dst_dockspace_id, windows that aren't part of our dockspace window class but that are docked in a same node will be split apart,
    // whereas we could attempt to at least keep them together in a new, same floating node.
    ImVector<ImGuiID> node_remap_pairs;
    DockBuilderCopyNode(src_dockspace_id, dst_dockspace_id, &node_remap_pairs);

    // Attempt to transition all the upcoming windows associated to dst_dockspace_id into the newly created hierarchy of dock nodes
    // (The windows associated to src_dockspace_id are staying in place)
    ImVector<ImGuiID> src_windows;
    for (int remap_window_n = 0; remap_window_n < in_window_remap_pairs->Size; remap_window_n += 2)
    {
        const char* src_window_name = (*in_window_remap_pairs)[remap_window_n];
        const char* dst_window_name = (*in_window_remap_pairs)[remap_window_n + 1];
        ImGuiID src_window_id = ImHashStr(src_window_name);
        src_windows.push_back(src_window_id);

        // Search in the remapping tables
        ImGuiID src_dock_id = 0;
        if (ImGuiWindow* src_window = FindWindowByID(src_window_id))
            src_dock_id = src_window->DockId;
        else if (ImGuiWindowSettings* src_window_settings = FindWindowSettingsByID(src_window_id))
            src_dock_id = src_window_settings->DockId;
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
            IMGUI_DEBUG_LOG_DOCKING("[docking] Remap live window '%s' 0x%08X -> '%s' 0x%08X\n", src_window_name, src_dock_id, dst_window_name, dst_dock_id);
            DockBuilderDockWindow(dst_window_name, dst_dock_id);
        }
        else
        {
            // Floating windows gets their settings transferred (regardless of whether the new window already exist or not)
            // When this is leading to a Copy and not a Move, we would get two overlapping floating windows. Could we possibly dock them together?
            IMGUI_DEBUG_LOG_DOCKING("[docking] Remap window settings '%s' -> '%s'\n", src_window_name, dst_window_name);
            DockBuilderCopyWindowSettings(src_window_name, dst_window_name);
        }
    }

    // Anything else in the source nodes of 'node_remap_pairs' are windows that are not included in the remapping list.
    // Find those windows and move to them to the cloned dock node. This may be optional?
    // Dock those are a second step as undocking would invalidate source dock nodes.
    struct DockRemainingWindowTask { ImGuiWindow* Window; ImGuiID DockId; DockRemainingWindowTask(ImGuiWindow* window, ImGuiID dock_id) { Window = window; DockId = dock_id; } };
    ImVector<DockRemainingWindowTask> dock_remaining_windows;
    for (int dock_remap_n = 0; dock_remap_n < node_remap_pairs.Size; dock_remap_n += 2)
        if (ImGuiID src_dock_id = node_remap_pairs[dock_remap_n])
        {
            ImGuiID dst_dock_id = node_remap_pairs[dock_remap_n + 1];
            ImGuiDockNode* node = DockBuilderGetNode(src_dock_id);
            for (int window_n = 0; window_n < node->Windows.Size; window_n++)
            {
                ImGuiWindow* window = node->Windows[window_n];
                if (src_windows.contains(window->ID))
                    continue;

                // Docked windows gets redocked into the new node hierarchy.
                IMGUI_DEBUG_LOG_DOCKING("[docking] Remap window '%s' %08X -> %08X\n", window->Name, src_dock_id, dst_dock_id);
                dock_remaining_windows.push_back(DockRemainingWindowTask(window, dst_dock_id));
            }
        }
    for (const DockRemainingWindowTask& task : dock_remaining_windows)
        DockBuilderDockWindow(task.Window->Name, task.DockId);
}

// FIXME-DOCK: This is awkward because in series of split user is likely to loose access to its root node.
void ImGui::DockBuilderFinish(ImGuiID root_id)
{
    ImGuiContext& g = *GImGui;
    //DockContextRebuild(&g);
    DockContextBuildAddWindowsToNodes(&g, root_id);
}

//-----------------------------------------------------------------------------
// Docking: Begin/End Support Functions (called from Begin/End)
//-----------------------------------------------------------------------------
// - GetWindowAlwaysWantOwnTabBar()
// - DockContextBindNodeToWindow()
// - BeginDocked()
// - BeginDockableDragDropSource()
// - BeginDockableDragDropTarget()
//-----------------------------------------------------------------------------

bool ImGui::GetWindowAlwaysWantOwnTabBar(ImGuiWindow* window)
{
    ImGuiContext& g = *GImGui;
    if (g.IO.ConfigDockingAlwaysTabBar || window->WindowClass.DockingAlwaysTabBar)
        if ((window->Flags & (ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoDocking)) == 0)
            if (!window->IsFallbackWindow)    // We don't support AlwaysTabBar on the fallback/implicit window to avoid unused dock-node overhead/noise
                return true;
    return false;
}

static ImGuiDockNode* ImGui::DockContextBindNodeToWindow(ImGuiContext* ctx, ImGuiWindow* window)
{
    ImGuiContext& g = *ctx;
    ImGuiDockNode* node = DockContextFindNodeByID(ctx, window->DockId);
    IM_ASSERT(window->DockNode == NULL);

    // We should not be docking into a split node (SetWindowDock should avoid this)
    if (node && node->IsSplitNode())
    {
        DockContextProcessUndockWindow(ctx, window);
        return NULL;
    }

    // Create node
    if (node == NULL)
    {
        node = DockContextAddNode(ctx, window->DockId);
        node->AuthorityForPos = node->AuthorityForSize = node->AuthorityForViewport = ImGuiDataAuthority_Window;
        node->LastFrameAlive = g.FrameCount;
    }

    // If the node just turned visible and is part of a hierarchy, it doesn't have a Size assigned by DockNodeTreeUpdatePosSize() yet,
    // so we're forcing a Pos/Size update from the first ancestor that is already visible (often it will be the root node).
    // If we don't do this, the window will be assigned a zero-size on its first frame, which won't ideally warm up the layout.
    // This is a little wonky because we don't normally update the Pos/Size of visible node mid-frame.
    if (!node->IsVisible)
    {
        ImGuiDockNode* ancestor_node = node;
        while (!ancestor_node->IsVisible && ancestor_node->ParentNode)
            ancestor_node = ancestor_node->ParentNode;
        IM_ASSERT(ancestor_node->Size.x > 0.0f && ancestor_node->Size.y > 0.0f);
        DockNodeUpdateHasCentralNodeChild(DockNodeGetRootNode(ancestor_node));
        DockNodeTreeUpdatePosSize(ancestor_node, ancestor_node->Pos, ancestor_node->Size, node);
    }

    // Add window to node
    bool node_was_visible = node->IsVisible;
    DockNodeAddWindow(node, window, true);
    node->IsVisible = node_was_visible; // Don't mark visible right away (so DockContextEndFrame() doesn't render it, maybe other side effects? will see)
    IM_ASSERT(node == window->DockNode);
    return node;
}

static void StoreDockStyleForWindow(ImGuiWindow* window)
{
    ImGuiContext& g = *GImGui;
    for (int color_n = 0; color_n < ImGuiWindowDockStyleCol_COUNT; color_n++)
        window->DockStyle.Colors[color_n] = ImGui::ColorConvertFloat4ToU32(g.Style.Colors[GWindowDockStyleColors[color_n]]);
}

void ImGui::BeginDocked(ImGuiWindow* window, bool* p_open)
{
    ImGuiContext& g = *GImGui;

    // Specific extra processing for fallback window (#9151), could be in Begin() as well.
    if (window->IsFallbackWindow && !window->WasActive)
    {
        DockNodeHideWindowDuringHostWindowCreation(window);
        return;
    }

    const bool auto_dock_node = GetWindowAlwaysWantOwnTabBar(window);
    if (auto_dock_node)
    {
        if (window->DockId == 0)
        {
            IM_ASSERT(window->DockNode == NULL);
            window->DockId = DockContextGenNodeID(&g);
        }
    }
    else
    {
        // Calling SetNextWindowPos() undock windows by default (by setting PosUndock)
        bool want_undock = false;
        want_undock |= (window->Flags & ImGuiWindowFlags_NoDocking) != 0;
        want_undock |= (g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasPos) && (window->SetWindowPosAllowFlags & g.NextWindowData.PosCond) && g.NextWindowData.PosUndock;
        if (want_undock)
        {
            DockContextProcessUndockWindow(&g, window);
            return;
        }
    }

    // Bind to our dock node
    ImGuiDockNode* node = window->DockNode;
    if (node != NULL)
        IM_ASSERT(window->DockId == node->ID);
    if (window->DockId != 0 && node == NULL)
    {
        node = DockContextBindNodeToWindow(&g, window);
        if (node == NULL)
            return;
    }

#if 0
    // Undock if the ImGuiDockNodeFlags_NoDockingInCentralNode got set
    if (node->IsCentralNode && (node->Flags & ImGuiDockNodeFlags_NoDockingInCentralNode))
    {
        DockContextProcessUndockWindow(ctx, window);
        return;
    }
#endif

    // Undock if our dockspace node disappeared
    // Note how we are testing for LastFrameAlive and NOT LastFrameActive. A DockSpace node can be maintained alive while being inactive with ImGuiDockNodeFlags_KeepAliveOnly.
    if (node->LastFrameAlive < g.FrameCount)
    {
        // If the window has been orphaned, transition the docknode to an implicit node processed in DockContextNewFrameUpdateDocking()
        ImGuiDockNode* root_node = DockNodeGetRootNode(node);
        if (root_node->LastFrameAlive < g.FrameCount)
            DockContextProcessUndockWindow(&g, window);
        else
            window->DockIsActive = true;
        return;
    }

    // Store style overrides
    StoreDockStyleForWindow(window);

    // Fast path return. It is common for windows to hold on a persistent DockId but be the only visible window,
    // and never create neither a host window neither a tab bar.
    // FIXME-DOCK: replace ->HostWindow NULL compare with something more explicit (~was initially intended as a first frame test)
    if (node->HostWindow == NULL)
    {
        if (node->State == ImGuiDockNodeState_HostWindowHiddenBecauseWindowsAreResizing)
            window->DockIsActive = true;
        if (node->Windows.Size > 1 && window->Appearing) // Only hide appearing window
            DockNodeHideWindowDuringHostWindowCreation(window);
        return;
    }

    // We can have zero-sized nodes (e.g. children of a small-size dockspace)
    IM_ASSERT(node->HostWindow);
    IM_ASSERT(node->IsLeafNode());
    IM_ASSERT(node->Size.x >= 0.0f && node->Size.y >= 0.0f);
    node->State = ImGuiDockNodeState_HostWindowVisible;

    // Undock if we are submitted earlier than the host window
    if (!(node->MergedFlags & ImGuiDockNodeFlags_KeepAliveOnly) && window->BeginOrderWithinContext < node->HostWindow->BeginOrderWithinContext)
    {
        DockContextProcessUndockWindow(&g, window);
        return;
    }

    // Position/Size window
    SetNextWindowPos(node->Pos);
    SetNextWindowSize(node->Size);
    g.NextWindowData.PosUndock = false; // Cancel implicit undocking of SetNextWindowPos()
    window->DockIsActive = true;
    window->DockNodeIsVisible = true;
    window->DockTabIsVisible = false;
    if (node->MergedFlags & ImGuiDockNodeFlags_KeepAliveOnly)
        return;

    // When the window is selected we mark it as visible.
    if (node->VisibleWindow == window)
        window->DockTabIsVisible = true;

    // Update window flag
    IM_ASSERT((window->Flags & ImGuiWindowFlags_ChildWindow) == 0);
    window->Flags |= ImGuiWindowFlags_ChildWindow | ImGuiWindowFlags_NoResize;
    window->ChildFlags |= ImGuiChildFlags_AlwaysUseWindowPadding;
    if (node->IsHiddenTabBar() || node->IsNoTabBar())
        window->Flags |= ImGuiWindowFlags_NoTitleBar;
    else
        window->Flags &= ~ImGuiWindowFlags_NoTitleBar;      // Clear the NoTitleBar flag in case the user set it: confusingly enough we need a title bar height so we are correctly offset, but it won't be displayed!

    // Save new dock order only if the window has been visible once already
    // This allows multiple windows to be created in the same frame and have their respective dock orders preserved.
    if (node->TabBar && window->WasActive)
        window->DockOrder = (short)DockNodeGetTabOrder(window);

    if ((node->WantCloseAll || node->WantCloseTabId == window->TabId) && p_open != NULL)
        *p_open = false;

    // Update ChildId to allow returning from Child to Parent with Escape
    ImGuiWindow* parent_window = window->DockNode->HostWindow;
    window->ChildId = parent_window->GetID(window->Name);
}

void ImGui::BeginDockableDragDropSource(ImGuiWindow* window)
{
    ImGuiContext& g = *GImGui;
    IM_ASSERT(g.ActiveId == window->MoveId);
    IM_ASSERT(g.MovingWindow == window);
    IM_ASSERT(g.CurrentWindow == window);

    // 0: Hold SHIFT to disable docking, 1: Hold SHIFT to enable docking.
    if (g.IO.ConfigDockingWithShift != g.IO.KeyShift)
    {
        // When ConfigDockingWithShift is set, display a tooltip to increase UI affordance.
        // We cannot set for HoveredWindowUnderMovingWindow != NULL here, as it is only valid/useful when drag and drop is already active
        // (because of the 'is_mouse_dragging_with_an_expected_destination' logic in UpdateViewportsNewFrame() function)
        IM_ASSERT(g.NextWindowData.HasFlags == 0);
        if (g.IO.ConfigDockingWithShift && g.MouseStationaryTimer >= 1.0f && g.ActiveId >= 1.0f)
            SetTooltip("%s", LocalizeGetMsg(ImGuiLocKey_DockingHoldShiftToDock));
        return;
    }

    g.LastItemData.ID = window->MoveId;
    window = window->RootWindowDockTree;
    IM_ASSERT((window->Flags & ImGuiWindowFlags_NoDocking) == 0);
    bool is_drag_docking = (g.IO.ConfigDockingWithShift) || ImRect(0, 0, window->SizeFull.x, GetFrameHeight()).Contains(g.ActiveIdClickOffset); // FIXME-DOCKING: Need to make this stateful and explicit
    ImGuiDragDropFlags drag_drop_flags = ImGuiDragDropFlags_SourceNoPreviewTooltip | ImGuiDragDropFlags_SourceNoHoldToOpenOthers | ImGuiDragDropFlags_PayloadAutoExpire | ImGuiDragDropFlags_PayloadNoCrossContext | ImGuiDragDropFlags_PayloadNoCrossProcess;
    if (is_drag_docking && BeginDragDropSource(drag_drop_flags))
    {
        SetDragDropPayload(IMGUI_PAYLOAD_TYPE_WINDOW, &window, sizeof(window));
        EndDragDropSource();
        StoreDockStyleForWindow(window); // Store style overrides while dragging (even when not docked) because docking preview may need it.
    }
}

void ImGui::BeginDockableDragDropTarget(ImGuiWindow* window)
{
    ImGuiContext& g = *GImGui;

    //IM_ASSERT(window->RootWindowDockTree == window); // May also be a DockSpace
    IM_ASSERT((window->Flags & ImGuiWindowFlags_NoDocking) == 0);
    if (!g.DragDropActive)
        return;
    //GetForegroundDrawList(window)->AddRect(window->Pos, window->Pos + window->Size, IM_COL32(255, 255, 0, 255));
    if (!BeginDragDropTargetCustom(window->Rect(), window->ID))
        return;

    // Peek into the payload before calling AcceptDragDropPayload() so we can handle overlapping dock nodes with filtering
    // (this is a little unusual pattern, normally most code would call AcceptDragDropPayload directly)
    const ImGuiPayload* payload = &g.DragDropPayload;
    if (!payload->IsDataType(IMGUI_PAYLOAD_TYPE_WINDOW) || !DockNodeIsDropAllowed(window, *(ImGuiWindow**)payload->Data))
    {
        EndDragDropTarget();
        return;
    }

    ImGuiWindow* payload_window = *(ImGuiWindow**)payload->Data;
    if (AcceptDragDropPayload(IMGUI_PAYLOAD_TYPE_WINDOW, ImGuiDragDropFlags_AcceptBeforeDelivery | ImGuiDragDropFlags_AcceptNoDrawDefaultRect))
    {
        // Select target node
        // (Important: we cannot use g.HoveredDockNode here! Because each of our target node have filters based on payload, each candidate drop target will do its own evaluation)
        bool dock_into_floating_window = false;
        ImGuiDockNode* node = NULL;
        if (window->DockNodeAsHost)
        {
            // Cannot assume that node will != NULL even though we passed the rectangle test: it depends on padding/spacing handled by DockNodeTreeFindVisibleNodeByPos().
            node = DockNodeTreeFindVisibleNodeByPos(window->DockNodeAsHost, g.IO.MousePos);

            // There is an edge case when docking into a dockspace which only has _inactive_ nodes (because none of the windows are active)
            // In this case we need to fallback into any leaf mode, possibly the central node.
            // FIXME-20181220: We should not have to test for IsLeafNode() here but we have another bug to fix first.
            if (node && node->IsDockSpace() && node->IsRootNode())
                node = (node->CentralNode && node->IsLeafNode()) ? node->CentralNode : DockNodeTreeFindFallbackLeafNode(node);
        }
        else
        {
            if (window->DockNode)
                node = window->DockNode;
            else
                dock_into_floating_window = true; // Dock into a regular window
        }

        const ImRect explicit_target_rect = (node && node->TabBar && !node->IsHiddenTabBar() && !node->IsNoTabBar()) ? node->TabBar->BarRect : ImRect(window->Pos, window->Pos + ImVec2(window->Size.x, GetFrameHeight()));
        const bool is_explicit_target = g.IO.ConfigDockingWithShift || IsMouseHoveringRect(explicit_target_rect.Min, explicit_target_rect.Max);

        // Preview docking request and find out split direction/ratio
        //const bool do_preview = true;     // Ignore testing for payload->IsPreview() which removes one frame of delay, but breaks overlapping drop targets within the same window.
        const bool do_preview = payload->IsPreview() || payload->IsDelivery();
        if (do_preview && (node != NULL || dock_into_floating_window))
        {
            // If we have a non-leaf node it means we are hovering the border of a parent node, in which case only outer markers will appear.
            ImGuiDockPreviewData split_inner;
            ImGuiDockPreviewData split_outer;
            ImGuiDockPreviewData* split_data = &split_inner;
            if (node && (node->ParentNode || node->IsCentralNode() || !node->IsLeafNode()))
                if (ImGuiDockNode* root_node = DockNodeGetRootNode(node))
                {
                    DockNodePreviewDockSetup(window, root_node, payload_window, NULL, &split_outer, is_explicit_target, true);
                    if (split_outer.IsSplitDirExplicit)
                        split_data = &split_outer;
                }
            if (!node || node->IsLeafNode())
                DockNodePreviewDockSetup(window, node, payload_window, NULL, &split_inner, is_explicit_target, false);
            if (split_data == &split_outer)
                split_inner.IsDropAllowed = false;

            // Draw inner then outer, so that previewed tab (in inner data) will be behind the outer drop boxes
            DockNodePreviewDockRender(window, node, payload_window, &split_inner);
            DockNodePreviewDockRender(window, node, payload_window, &split_outer);

            // Queue docking request
            if (split_data->IsDropAllowed && payload->IsDelivery())
                DockContextQueueDock(&g, window, split_data->SplitNode, payload_window, split_data->SplitDir, split_data->SplitRatio, split_data == &split_outer);
        }
    }
    EndDragDropTarget();
}

//-----------------------------------------------------------------------------
// Docking: Settings
//-----------------------------------------------------------------------------
// - DockSettingsRenameNodeReferences()
// - DockSettingsRemoveNodeReferences()
// - DockSettingsFindNodeSettings()
// - DockSettingsHandler_ApplyAll()
// - DockSettingsHandler_ReadOpen()
// - DockSettingsHandler_ReadLine()
// - DockSettingsHandler_DockNodeToSettings()
// - DockSettingsHandler_WriteAll()
//-----------------------------------------------------------------------------

static void ImGui::DockSettingsRenameNodeReferences(ImGuiID old_node_id, ImGuiID new_node_id)
{
    ImGuiContext& g = *GImGui;
    IMGUI_DEBUG_LOG_DOCKING("[docking] DockSettingsRenameNodeReferences: from 0x%08X -> to 0x%08X\n", old_node_id, new_node_id);
    for (int window_n = 0; window_n < g.Windows.Size; window_n++)
    {
        ImGuiWindow* window = g.Windows[window_n];
        if (window->DockId == old_node_id && window->DockNode == NULL)
            window->DockId = new_node_id;
    }
    //// FIXME-OPT: We could remove this loop by storing the index in the map
    for (ImGuiWindowSettings* settings = g.SettingsWindows.begin(); settings != NULL; settings = g.SettingsWindows.next_chunk(settings))
        if (settings->DockId == old_node_id)
            settings->DockId = new_node_id;
}

// Remove references stored in ImGuiWindowSettings to the given ImGuiDockNodeSettings
static void ImGui::DockSettingsRemoveNodeReferences(ImGuiID* node_ids, int node_ids_count)
{
    ImGuiContext& g = *GImGui;
    int found = 0;
    //// FIXME-OPT: We could remove this loop by storing the index in the map
    for (ImGuiWindowSettings* settings = g.SettingsWindows.begin(); settings != NULL; settings = g.SettingsWindows.next_chunk(settings))
        for (int node_n = 0; node_n < node_ids_count; node_n++)
            if (settings->DockId == node_ids[node_n])
            {
                settings->DockId = 0;
                settings->DockOrder = -1;
                if (++found < node_ids_count)
                    break;
                return;
            }
}

static ImGuiDockNodeSettings* ImGui::DockSettingsFindNodeSettings(ImGuiContext* ctx, ImGuiID id)
{
    // FIXME-OPT
    ImGuiDockContext* dc = &ctx->DockContext;
    for (int n = 0; n < dc->NodesSettings.Size; n++)
        if (dc->NodesSettings[n].ID == id)
            return &dc->NodesSettings[n];
    return NULL;
}

// Clear settings data
static void ImGui::DockSettingsHandler_ClearAll(ImGuiContext* ctx, ImGuiSettingsHandler*)
{
    ImGuiDockContext* dc = &ctx->DockContext;
    dc->NodesSettings.clear();
    DockContextClearNodes(ctx, 0, true);
}

// Recreate nodes based on settings data
static void ImGui::DockSettingsHandler_ApplyAll(ImGuiContext* ctx, ImGuiSettingsHandler*)
{
    // Prune settings at boot time only
    ImGuiDockContext* dc = &ctx->DockContext;
    if (ctx->Windows.Size == 0)
        DockContextPruneUnusedSettingsNodes(ctx);
    DockContextBuildNodesFromSettings(ctx, dc->NodesSettings.Data, dc->NodesSettings.Size);
    DockContextBuildAddWindowsToNodes(ctx, 0);
}

static void* ImGui::DockSettingsHandler_ReadOpen(ImGuiContext*, ImGuiSettingsHandler*, const char* name)
{
    if (strcmp(name, "Data") != 0)
        return NULL;
    return (void*)1;
}

static void ImGui::DockSettingsHandler_ReadLine(ImGuiContext* ctx, ImGuiSettingsHandler*, void*, const char* line)
{
    char c = 0;
    int x = 0, y = 0;
    int r = 0;

    // Parsing, e.g.
    // " DockNode   ID=0x00000001 Pos=383,193 Size=201,322 Split=Y,0.506 "
    // "   DockNode ID=0x00000002 Parent=0x00000001 "
    // Important: this code expect currently fields in a fixed order.
    ImGuiDockNodeSettings node;
    line = ImStrSkipBlank(line);
    if      (strncmp(line, "DockNode", 8) == 0)  { line = ImStrSkipBlank(line + strlen("DockNode")); }
    else if (strncmp(line, "DockSpace", 9) == 0) { line = ImStrSkipBlank(line + strlen("DockSpace")); node.Flags |= ImGuiDockNodeFlags_DockSpace; }
    else return;
    if (sscanf(line, "ID=0x%08X%n",      &node.ID, &r) == 1)            { line += r; } else return;
    if (sscanf(line, " Parent=0x%08X%n", &node.ParentNodeId, &r) == 1)  { line += r; if (node.ParentNodeId == 0) return; }
    if (sscanf(line, " Window=0x%08X%n", &node.ParentWindowId, &r) ==1) { line += r; if (node.ParentWindowId == 0) return; }
    if (node.ParentNodeId == 0)
    {
        if (sscanf(line, " Pos=%i,%i%n",  &x, &y, &r) == 2)         { line += r; node.Pos = ImVec2ih((short)x, (short)y); } else return;
        if (sscanf(line, " Size=%i,%i%n", &x, &y, &r) == 2)         { line += r; node.Size = ImVec2ih((short)x, (short)y); } else return;
    }
    else
    {
        if (sscanf(line, " SizeRef=%i,%i%n", &x, &y, &r) == 2)      { line += r; node.SizeRef = ImVec2ih((short)x, (short)y); }
    }
    if (sscanf(line, " Split=%c%n", &c, &r) == 1)                   { line += r; if (c == 'X') node.SplitAxis = ImGuiAxis_X; else if (c == 'Y') node.SplitAxis = ImGuiAxis_Y; }
    if (sscanf(line, " NoResize=%d%n", &x, &r) == 1)                { line += r; if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoResize; }
    if (sscanf(line, " CentralNode=%d%n", &x, &r) == 1)             { line += r; if (x != 0) node.Flags |= ImGuiDockNodeFlags_CentralNode; }
    if (sscanf(line, " NoTabBar=%d%n", &x, &r) == 1)                { line += r; if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoTabBar; }
    if (sscanf(line, " HiddenTabBar=%d%n", &x, &r) == 1)            { line += r; if (x != 0) node.Flags |= ImGuiDockNodeFlags_HiddenTabBar; }
    if (sscanf(line, " NoWindowMenuButton=%d%n", &x, &r) == 1)      { line += r; if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoWindowMenuButton; }
    if (sscanf(line, " NoCloseButton=%d%n", &x, &r) == 1)           { line += r; if (x != 0) node.Flags |= ImGuiDockNodeFlags_NoCloseButton; }
    if (sscanf(line, " Selected=0x%08X%n", &node.SelectedTabId,&r) == 1) { line += r; }
    if (node.ParentNodeId != 0)
        if (ImGuiDockNodeSettings* parent_settings = DockSettingsFindNodeSettings(ctx, node.ParentNodeId))
            node.Depth = parent_settings->Depth + 1;
    ctx->DockContext.NodesSettings.push_back(node);
}

static void DockSettingsHandler_DockNodeToSettings(ImGuiDockContext* dc, ImGuiDockNode* node, int depth)
{
    ImGuiDockNodeSettings node_settings;
    IM_ASSERT(depth < (1 << (sizeof(node_settings.Depth) << 3)));
    node_settings.ID = node->ID;
    node_settings.ParentNodeId = node->ParentNode ? node->ParentNode->ID : 0;
    node_settings.ParentWindowId = (node->IsDockSpace() && node->HostWindow && node->HostWindow->ParentWindow) ? node->HostWindow->ParentWindow->ID : 0;
    node_settings.SelectedTabId = node->SelectedTabId;
    node_settings.SplitAxis = (signed char)(node->IsSplitNode() ? node->SplitAxis : ImGuiAxis_None);
    node_settings.Depth = (char)depth;
    node_settings.Flags = (node->LocalFlags & ImGuiDockNodeFlags_SavedFlagsMask_);
    node_settings.Pos = ImVec2ih(node->Pos);
    node_settings.Size = ImVec2ih(node->Size);
    node_settings.SizeRef = ImVec2ih(node->SizeRef);
    dc->NodesSettings.push_back(node_settings);
    if (node->ChildNodes[0])
        DockSettingsHandler_DockNodeToSettings(dc, node->ChildNodes[0], depth + 1);
    if (node->ChildNodes[1])
        DockSettingsHandler_DockNodeToSettings(dc, node->ChildNodes[1], depth + 1);
}

static void ImGui::DockSettingsHandler_WriteAll(ImGuiContext* ctx, ImGuiSettingsHandler* handler, ImGuiTextBuffer* buf)
{
    ImGuiContext& g = *ctx;
    ImGuiDockContext* dc = &ctx->DockContext;
    if (!(g.IO.ConfigFlags & ImGuiConfigFlags_DockingEnable))
        return;

    // Gather settings data
    // (unlike our windows settings, because nodes are always built we can do a full rewrite of the SettingsNode buffer)
    dc->NodesSettings.resize(0);
    dc->NodesSettings.reserve(dc->Nodes.Data.Size);
    for (int n = 0; n < dc->Nodes.Data.Size; n++)
        if (ImGuiDockNode* node = (ImGuiDockNode*)dc->Nodes.Data[n].val_p)
            if (node->IsRootNode())
                DockSettingsHandler_DockNodeToSettings(dc, node, 0);

    int max_depth = 0;
    for (int node_n = 0; node_n < dc->NodesSettings.Size; node_n++)
        max_depth = ImMax((int)dc->NodesSettings[node_n].Depth, max_depth);

    // Write to text buffer
    buf->appendf("[%s][Data]\n", handler->TypeName);
    for (int node_n = 0; node_n < dc->NodesSettings.Size; node_n++)
    {
        const int line_start_pos = buf->size(); (void)line_start_pos;
        const ImGuiDockNodeSettings* node_settings = &dc->NodesSettings[node_n];
        buf->appendf("%*s%s%*s", node_settings->Depth * 2, "", (node_settings->Flags & ImGuiDockNodeFlags_DockSpace) ? "DockSpace" : "DockNode ", (max_depth - node_settings->Depth) * 2, "");  // Text align nodes to facilitate looking at .ini file
        buf->appendf(" ID=0x%08X", node_settings->ID);
        if (node_settings->ParentNodeId)
        {
            buf->appendf(" Parent=0x%08X SizeRef=%d,%d", node_settings->ParentNodeId, node_settings->SizeRef.x, node_settings->SizeRef.y);
        }
        else
        {
            if (node_settings->ParentWindowId)
                buf->appendf(" Window=0x%08X", node_settings->ParentWindowId);
            buf->appendf(" Pos=%d,%d Size=%d,%d", node_settings->Pos.x, node_settings->Pos.y, node_settings->Size.x, node_settings->Size.y);
        }
        if (node_settings->SplitAxis != ImGuiAxis_None)
            buf->appendf(" Split=%c", (node_settings->SplitAxis == ImGuiAxis_X) ? 'X' : 'Y');
        if (node_settings->Flags & ImGuiDockNodeFlags_NoResize)
            buf->appendf(" NoResize=1");
        if (node_settings->Flags & ImGuiDockNodeFlags_CentralNode)
            buf->appendf(" CentralNode=1");
        if (node_settings->Flags & ImGuiDockNodeFlags_NoTabBar)
            buf->appendf(" NoTabBar=1");
        if (node_settings->Flags & ImGuiDockNodeFlags_HiddenTabBar)
            buf->appendf(" HiddenTabBar=1");
        if (node_settings->Flags & ImGuiDockNodeFlags_NoWindowMenuButton)
            buf->appendf(" NoWindowMenuButton=1");
        if (node_settings->Flags & ImGuiDockNodeFlags_NoCloseButton)
            buf->appendf(" NoCloseButton=1");
        if (node_settings->SelectedTabId)
            buf->appendf(" Selected=0x%08X", node_settings->SelectedTabId);

        // [DEBUG] Include comments in the .ini file to ease debugging (this makes saving slower!)
        if (g.IO.ConfigDebugIniSettings)
            if (ImGuiDockNode* node = DockContextFindNodeByID(ctx, node_settings->ID))
            {
                buf->appendf("%*s", ImMax(2, (line_start_pos + 92) - buf->size()), "");     // Align everything
                if (node->IsDockSpace() && node->HostWindow && node->HostWindow->ParentWindow)
                    buf->appendf(" ; in '%s'", node->HostWindow->ParentWindow->Name);
                // Iterate settings so we can give info about windows that didn't exist during the session.
                int contains_window = 0;
                for (ImGuiWindowSettings* settings = g.SettingsWindows.begin(); settings != NULL; settings = g.SettingsWindows.next_chunk(settings))
                    if (settings->DockId == node_settings->ID)
                    {
                        if (contains_window++ == 0)
                            buf->appendf(" ; contains ");
                        buf->appendf("'%s' ", settings->GetName());
                    }
            }

        buf->appendf("\n");
    }
    buf->appendf("\n");
}


//-----------------------------------------------------------------------------
