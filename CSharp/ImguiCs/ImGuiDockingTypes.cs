// Port of Dear ImGui docking internal types (imgui_internal.h [SECTION] Docking support
// and imgui.cpp 'Docking: Internal Types'). Requires the docking branch API surface.
using System;

namespace ImGui;

// Internal Drag and Drop payload types. String starting with '_' are reserved for Dear ImGui.
public static partial class ImGuiConst
{
    public const string IMGUI_PAYLOAD_TYPE_WINDOW = "_IMWINDOW";     // Payload == ImGuiWindow*

    public const float DOCKING_TRANSPARENT_PAYLOAD_ALPHA = 0.50f;    // For use with io.ConfigDockingTransparentPayload. Apply to Viewport _or_ WindowBg in host viewport.

    public const int DOCKING_HOST_DRAW_CHANNEL_BG = 0;  // Dock host: background fill
    public const int DOCKING_HOST_DRAW_CHANNEL_FG = 1;  // Dock host: decorations and contents
}

// Extend ImGuiDockNodeFlags_
public static unsafe partial class ImGuiDockNodeFlagsPrivate_
{
    // [Internal]
    public const int ImGuiDockNodeFlags_DockSpace = 1 << 10;   // Saved // A dockspace is a node that occupy space within an existing user window. Otherwise the node is floating and create its own window.
    public const int ImGuiDockNodeFlags_CentralNode = 1 << 11; // Saved // The central node has 2 main properties: stay visible when empty, only use "remaining" spaces from its neighbor.
    public const int ImGuiDockNodeFlags_NoTabBar = 1 << 12;    // Saved // Tab bar is completely unavailable. No triangle in the corner to enable it back.
    public const int ImGuiDockNodeFlags_HiddenTabBar = 1 << 13;   // Saved // Tab bar is hidden, with a triangle in the corner to show it back (NB: actual tab-bar instance may be destroyed as this is only used for single-window tab bar)
    public const int ImGuiDockNodeFlags_NoWindowMenuButton = 1 << 14; // Saved // Disable window/docking menu (that one that appears instead of the collapse button)
    public const int ImGuiDockNodeFlags_NoCloseButton = 1 << 15;  // Saved // Disable close button
    public const int ImGuiDockNodeFlags_NoResizeX = 1 << 16;   //       //
    public const int ImGuiDockNodeFlags_NoResizeY = 1 << 17;   //       //
    public const int ImGuiDockNodeFlags_DockedWindowsInFocusRoute = 1 << 18; // // Any docked window will be automatically be focus-route chained (window->ParentWindowForFocusRoute set to this) so Shortcut() in this window can run when any docked window is focused.

    // Disable docking/undocking actions in this dockspace or individual node (existing docked nodes will be preserved)
    public const int ImGuiDockNodeFlags_NoDockingSplitOther = 1 << 19; // // Disable this node from splitting other windows/nodes.
    public const int ImGuiDockNodeFlags_NoDockingOverMe = 1 << 20;     // // Disable other windows/nodes from being docked over this node.
    public const int ImGuiDockNodeFlags_NoDockingOverOther = 1 << 21;  // // Disable this node from being docked over another window or non-empty node.
    public const int ImGuiDockNodeFlags_NoDockingOverEmpty = 1 << 22;  // // Disable this node from being docked over an empty node (e.g. DockSpace with no other windows)
    public const int ImGuiDockNodeFlags_NoDocking = ImGuiDockNodeFlags_NoDockingOverMe | ImGuiDockNodeFlags_NoDockingOverOther | ImGuiDockNodeFlags_NoDockingOverEmpty | ImGuiDockNodeFlags_NoDockingSplit | ImGuiDockNodeFlags_NoDockingSplitOther;

    // Masks
    public const int ImGuiDockNodeFlags_SharedFlagsInheritMask_ = ~0;
    public const int ImGuiDockNodeFlags_NoResizeFlagsMask_ = ImGuiDockNodeFlags_NoResize | ImGuiDockNodeFlags_NoResizeX | ImGuiDockNodeFlags_NoResizeY;

    // When splitting, those local flags are moved to the inheriting child, never duplicated
    public const int ImGuiDockNodeFlags_LocalFlagsTransferMask_ = ImGuiDockNodeFlags_NoDockingSplit | ImGuiDockNodeFlags_NoResizeFlagsMask_ | ImGuiDockNodeFlags_AutoHideTabBar | ImGuiDockNodeFlags_CentralNode | ImGuiDockNodeFlags_NoTabBar | ImGuiDockNodeFlags_HiddenTabBar | ImGuiDockNodeFlags_NoWindowMenuButton | ImGuiDockNodeFlags_NoCloseButton;
    public const int ImGuiDockNodeFlags_SavedFlagsMask_ = ImGuiDockNodeFlags_NoResizeFlagsMask_ | ImGuiDockNodeFlags_DockSpace | ImGuiDockNodeFlags_CentralNode | ImGuiDockNodeFlags_NoTabBar | ImGuiDockNodeFlags_HiddenTabBar | ImGuiDockNodeFlags_NoWindowMenuButton | ImGuiDockNodeFlags_NoCloseButton;
}

// Store the source authority (dock node vs window) of a field
public static unsafe partial class ImGuiDataAuthority_
{
    public const int ImGuiDataAuthority_Auto = 0;
    public const int ImGuiDataAuthority_DockNode = 1;
    public const int ImGuiDataAuthority_Window = 2;
}

public static unsafe partial class ImGuiDockNodeState_
{
    public const int ImGuiDockNodeState_Unknown = 0;
    public const int ImGuiDockNodeState_HostWindowHiddenBecauseSingleWindow = 1;
    public const int ImGuiDockNodeState_HostWindowHiddenBecauseWindowsAreResizing = 2;
    public const int ImGuiDockNodeState_HostWindowVisible = 3;
}

// ImGuiDockNode
public class ImGuiDockNode
{
    public ImGuiID ID;
    public int SharedFlags;                 // ImGuiDockNodeFlags // (Write) Flags shared by all nodes of a same dockspace hierarchy (inherited from the root node)
    public int LocalFlags;                  // ImGuiDockNodeFlags // (Write) Flags specific to this node
    public int LocalFlagsInWindows;         // ImGuiDockNodeFlags // (Write) Flags specific to this node, applied from windows
    public int MergedFlags;                 // ImGuiDockNodeFlags // (Read)  Effective flags (== SharedFlags | LocalFlagsInNode | LocalFlagsInWindows)
    public int State;                       // ImGuiDockNodeState
    public ImGuiDockNode ParentNode;
    public ImGuiDockNode[] ChildNodes = new ImGuiDockNode[2];   // [Split node only] Child nodes (left/right or top/bottom).
    public ImVector<ImGuiWindow> Windows = new ImVector<ImGuiWindow>();     // Note: unordered list! Iterate TabBar->Tabs for user-order.
    public ImGuiTabBar TabBar;
    public ImVec2 Pos;                      // Current position
    public ImVec2 Size;                     // Current size
    public ImVec2 SizeRef;                  // [Split node only] Last explicitly written-to size (overridden when using a splitter affecting the node), used to calculate Size.
    public int SplitAxis;                   // ImGuiAxis // [Split node only] Split axis (X or Y)
    public ImU32 LastBgColor;
    public ImGuiWindowClass WindowClass = new ImGuiWindowClass();   // [Root node only]

    public ImGuiWindow HostWindow;
    public ImGuiWindow VisibleWindow;       // Generally point to window which is ID is == SelectedTabID, but when CTRL+Tabbing this can be a different window.
    public ImGuiDockNode CentralNode;       // [Root node only] Pointer to central node.
    public ImGuiDockNode OnlyNodeWithWindows;   // [Root node only] Set when there is a single visible node within the hierarchy.
    public int CountNodeWithWindows;        // [Root node only]
    public int LastFrameAlive;              // Last frame number the node was updated or kept alive explicitly with DockSpace() + ImGuiDockNodeFlags_KeepAliveOnly
    public int LastFrameActive;             // Last frame number the node was updated.
    public int LastFrameFocused;            // Last frame number the node was focused.
    public ImGuiID LastFocusedNodeId;       // [Root node only] Which of our child docking node (any ancestor in the hierarchy) was last focused.
    public ImGuiID SelectedTabId;           // [Leaf node only] Which of our tab/window is selected.
    public ImGuiID WantCloseTabId;          // [Leaf node only] Set when closing a specific tab/window.
    public ImGuiID RefViewportId;           // Reference viewport ID from visible window when HostWindow == NULL.
    public byte AuthorityForPos;            // ImGuiDataAuthority (3 bits in C++)
    public byte AuthorityForSize;           // ImGuiDataAuthority
    public byte AuthorityForViewport;       // ImGuiDataAuthority
    public bool IsVisible;                  // Set to false when the node is hidden (usually disabled as it has no active window)
    public bool IsFocused;
    public bool IsBgDrawnThisFrame;
    public bool HasCloseButton;             // Provide space for a close button (if any of the docked window has one). Note that button may be hidden on window without one.
    public bool HasWindowMenuButton;
    public bool HasCentralNodeChild;
    public bool WantCloseAll;               // Set when closing all tabs at once.
    public bool WantLockSizeOnce;
    public bool WantMouseMove;              // After a node extraction we need to transition toward moving the newly created host window
    public bool WantHiddenTabBarUpdate;
    public bool WantHiddenTabBarToggle;

    public ImGuiDockNode(ImGuiID id)
    {
        ID = id;
        SharedFlags = LocalFlags = LocalFlagsInWindows = MergedFlags = ImGuiDockNodeFlags_None;
        SplitAxis = ImGuiAxis_None;

        State = ImGuiDockNodeState_Unknown;
        LastBgColor = ImGuiCore.IM_COL32(255, 255, 255, 255);
        CountNodeWithWindows = 0;
        LastFrameAlive = LastFrameActive = LastFrameFocused = -1;
        LastFocusedNodeId = 0;
        SelectedTabId = 0;
        WantCloseTabId = 0;
        RefViewportId = 0;
        AuthorityForPos = AuthorityForSize = (byte)ImGuiDataAuthority_.ImGuiDataAuthority_DockNode;
        AuthorityForViewport = (byte)ImGuiDataAuthority_.ImGuiDataAuthority_Auto;
        IsVisible = true;
        IsFocused = HasCloseButton = HasWindowMenuButton = HasCentralNodeChild = false;
        IsBgDrawnThisFrame = false;
        WantCloseAll = WantLockSizeOnce = WantMouseMove = WantHiddenTabBarUpdate = WantHiddenTabBarToggle = false;
    }

    public bool IsRootNode() => ParentNode == null;
    public bool IsDockSpace() => (MergedFlags & ImGuiDockNodeFlags_DockSpace) != 0;
    public bool IsFloatingNode() => ParentNode == null && (MergedFlags & ImGuiDockNodeFlags_DockSpace) == 0;
    public bool IsCentralNode() => (MergedFlags & ImGuiDockNodeFlags_CentralNode) != 0;
    public bool IsHiddenTabBar() => (MergedFlags & ImGuiDockNodeFlags_HiddenTabBar) != 0;   // Hidden tab bar can be shown back by clicking the small triangle
    public bool IsNoTabBar() => (MergedFlags & ImGuiDockNodeFlags_NoTabBar) != 0;           // Never show a tab bar
    public bool IsSplitNode() => ChildNodes[0] != null;
    public bool IsLeafNode() => ChildNodes[0] == null;
    public bool IsEmpty() => ChildNodes[0] == null && Windows.Size == 0;
    public ImRect Rect() => new ImRect(Pos.x, Pos.y, Pos.x + Size.x, Pos.y + Size.y);

    public void SetLocalFlags(int flags) { LocalFlags = flags; UpdateMergedFlags(); }
    public void UpdateMergedFlags() { MergedFlags = SharedFlags | LocalFlags | LocalFlagsInWindows; }
}

// List of colors that are stored at the time of Begin() into Docked Windows.
public static unsafe partial class ImGuiWindowDockStyleCol_
{
    public const int ImGuiWindowDockStyleCol_Text = 0;
    public const int ImGuiWindowDockStyleCol_TabHovered = 1;
    public const int ImGuiWindowDockStyleCol_TabFocused = 2;
    public const int ImGuiWindowDockStyleCol_TabSelected = 3;
    public const int ImGuiWindowDockStyleCol_TabSelectedOverline = 4;
    public const int ImGuiWindowDockStyleCol_TabDimmed = 5;
    public const int ImGuiWindowDockStyleCol_TabDimmedSelected = 6;
    public const int ImGuiWindowDockStyleCol_TabDimmedSelectedOverline = 7;
    public const int ImGuiWindowDockStyleCol_UnsavedMarker = 8;
    public const int ImGuiWindowDockStyleCol_COUNT = 9;
}

// We don't store style.Alpha: dock_node->LastBgColor embeds it and otherwise it would only affect the docking tab.
public class ImGuiWindowDockStyle
{
    public ImU32[] Colors = new ImU32[ImGuiWindowDockStyleCol_.ImGuiWindowDockStyleCol_COUNT];
}

public enum ImGuiDockRequestType
{
    ImGuiDockRequestType_None = 0,
    ImGuiDockRequestType_Dock,
    ImGuiDockRequestType_Undock,
    ImGuiDockRequestType_Split              // Split is the same as Dock but without a DockPayload
}

public class ImGuiDockRequest
{
    public ImGuiDockRequestType Type;
    public ImGuiWindow DockTargetWindow;    // Destination/Target Window to dock into (may be a loose window or a DockNode, might be NULL in which case DockTargetNode cannot be NULL)
    public ImGuiDockNode DockTargetNode;    // Destination/Target Node to dock into
    public ImGuiWindow DockPayload;         // Source/Payload window to dock (may be a loose window or a DockNode), [Optional]
    public int DockSplitDir;                // ImGuiDir
    public float DockSplitRatio;
    public bool DockSplitOuter;
    public ImGuiWindow UndockTargetWindow;
    public ImGuiDockNode UndockTargetNode;

    public ImGuiDockRequest()
    {
        Type = ImGuiDockRequestType.ImGuiDockRequestType_None;
        DockTargetWindow = DockPayload = UndockTargetWindow = null;
        DockTargetNode = UndockTargetNode = null;
        DockSplitDir = ImGuiDir_None;
        DockSplitRatio = 0.5f;
        DockSplitOuter = false;
    }
}

public class ImGuiDockPreviewData
{
    public ImGuiDockNode FutureNode;
    public bool IsDropAllowed;
    public bool IsCenterAvailable;
    public bool IsSidesAvailable;               // Hold your breath, grammar freaks..
    public bool IsSplitDirExplicit;             // Set when hovered the drop rect (vs. implicit SplitDir==None when hovered the window)
    public ImGuiDockNode SplitNode;
    public int SplitDir;                        // ImGuiDir
    public float SplitRatio;
    public ImRect[] DropRectsDraw;              // May be slightly different from hit-testing drop rects used in DockNodeCalcDropRects()

    public ImGuiDockPreviewData()
    {
        FutureNode = new ImGuiDockNode(0);
        IsDropAllowed = IsCenterAvailable = IsSidesAvailable = IsSplitDirExplicit = false;
        SplitNode = null;
        SplitDir = ImGuiDir_None;
        SplitRatio = 0.0f;
        DropRectsDraw = new ImRect[ImGuiDir_COUNT + 1];
        for (int n = 0; n < DropRectsDraw.Length; n++)
            DropRectsDraw[n] = new ImRect(+float.MaxValue, +float.MaxValue, -float.MaxValue, -float.MaxValue);
    }
}

// Persistent Settings data, stored contiguously in SettingsNodes (sizeof() ~32 bytes)
public class ImGuiDockNodeSettings
{
    public ImGuiID ID;
    public ImGuiID ParentNodeId;
    public ImGuiID ParentWindowId;
    public ImGuiID SelectedTabId;
    public sbyte SplitAxis;                 // ImGuiAxis
    public char Depth;
    public int Flags;                       // ImGuiDockNodeFlags // NB: We save individual flags one by one in ascii format (ImGuiDockNodeFlags_SavedFlagsMask_)
    public ImVec2ih Pos;
    public ImVec2ih Size;
    public ImVec2ih SizeRef;

    public ImGuiDockNodeSettings() { SplitAxis = ImGuiAxis_None; }
}

public class ImGuiDockContext
{
    public ImGuiStorage Nodes = new ImGuiStorage();             // Map ID -> ImGuiDockNode*: Active nodes
    public ImVector<ImGuiDockRequest> Requests = new ImVector<ImGuiDockRequest>();
    public ImVector<ImGuiDockNodeSettings> NodesSettings = new ImVector<ImGuiDockNodeSettings>();
    public bool WantFullRebuild;

    public ImGuiDockContext() { }
}

// [Internal] ImGuiDockContextPruneNodeData (used by DockContextPruneUnusedSettingsNodes)
public class ImGuiDockContextPruneNodeData
{
    public int CountWindows;
    public int CountChildWindows;
    public int CountChildNodes;
    public ImGuiID RootId;
}
