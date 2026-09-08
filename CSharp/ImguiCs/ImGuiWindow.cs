// Port of ImGuiWindowTempData and ImGuiWindow (imgui_internal.h / imgui.cpp).
using System;

namespace ImGui;


// Transient per-window data, reset at the beginning of the frame. (the "DC" variable name in ImGuiWindow)
public class ImGuiWindowTempData
{
    // Layout
    public ImVec2 CursorPos;
    public ImVec2 CursorPosPrevLine;
    public ImVec2 CursorStartPos;
    public ImVec2 CursorMaxPos;
    public ImVec2 IdealMaxPos;
    public ImVec2 CurrLineSize;
    public ImVec2 PrevLineSize;
    public float CurrLineTextBaseOffset;
    public float PrevLineTextBaseOffset;
    public bool IsSameLine;
    public bool IsSetPos;
    public ImVec1 Indent;
    public ImVec1 ColumnsOffset;
    public ImVec1 GroupOffset;
    public ImVec2 CursorStartPosLossyness;

    // Keyboard/Gamepad navigation
    public int NavLayerCurrent;             // ImGuiNavLayer
    public short NavLayersActiveMask;
    public short NavLayersActiveMaskNext;
    public bool NavIsScrollPushableX;
    public bool NavHideHighlightOneFrame;
    public bool NavWindowHasScrollY;

    // Miscellaneous
    public bool MenuBarAppending;
    public ImVec2 MenuBarOffset;
    public ImGuiMenuColumns MenuColumns = new ImGuiMenuColumns();
    public int TreeDepth;
    public ImU32 TreeHasStackDataDepthMask;
    public ImU32 TreeRecordsClippedNodesY2Mask;
    public ImVector<ImGuiWindow> ChildWindows = new ImVector<ImGuiWindow>();
    public ImGuiStorage StateStorage;       // Current persistent per-window storage
    public ImGuiOldColumns CurrentColumns;
    public int CurrentTableIdx;
    public int LayoutType;                  // ImGuiLayoutType
    public int ParentLayoutType;            // ImGuiLayoutType
    public ImU32 ModalDimBgColor;
    public int WindowItemStatusFlags;       // ImGuiItemStatusFlags
    public int ChildItemStatusFlags;        // ImGuiItemStatusFlags
    public int DockTabItemStatusFlags;      // ImGuiItemStatusFlags
    public ImRect DockTabItemRect = new ImRect();

    // Local parameters stacks
    public float ItemWidth;
    public float ItemWidthDefault;
    public float TextWrapPos;
    public ImVector<float> ItemWidthStack = new ImVector<float>();
    public ImVector<float> TextWrapPosStack = new ImVector<float>();
}

// Storage for one window
public class ImGuiWindow
{
    public ImGuiContext Ctx;
    public string Name;
    public ImGuiID ID;
    public int Flags;                       // ImGuiWindowFlags
    public int FlagsPreviousFrame;          // ImGuiWindowFlags
    public int ChildFlags;                  // ImGuiChildFlags
    public ImGuiWindowClass WindowClass = new ImGuiWindowClass();   // Advanced users only. Set with SetNextWindowClass()
    public ImGuiViewportP Viewport;         // Always set in Begin(). Inactive windows may have a NULL value here if their viewport was discarded.
    public ImGuiID ViewportId;              // We backup the viewport id (since the viewport may disappear or never be created if the window is inactive)
    public ImVec2 ViewportPos;              // We backup the viewport position (since the viewport may disappear or never be created if the window is inactive)
    public int ViewportAllowPlatformMonitorExtend;  // Reset to -1 every frame (index is guaranteed to be valid between NewFrame..EndFrame), only used in the Appearing frame of a tooltip/popup to enforce clamping to a given monitor
    public ImVec2 Pos;
    public ImVec2 Size;
    public ImVec2 SizeFull;
    public ImVec2 ContentSize;
    public ImVec2 ContentSizeIdeal;
    public ImVec2 ContentSizeExplicit;
    public ImVec2 WindowPadding;
    public float WindowRounding;
    public float WindowBorderSize;
    public float TitleBarHeight, MenuBarHeight;
    public float DecoOuterSizeX1, DecoOuterSizeY1;
    public float DecoOuterSizeX2, DecoOuterSizeY2;
    public float DecoInnerSizeX1, DecoInnerSizeY1;
    public int NameBufLen;
    public ImGuiID MoveId;
    public ImGuiID TabId;                   // == window->GetID("#TAB")
    public ImGuiID ChildId;
    public ImGuiID PopupId;
    public ImVec2 Scroll;
    public ImVec2 ScrollMax;
    public ImVec2 ScrollTarget;
    public ImVec2 ScrollTargetCenterRatio;
    public ImVec2 ScrollTargetEdgeSnapDist;
    public ImVec2 ScrollbarSizes;
    public bool ScrollbarX, ScrollbarY;
    public bool ScrollbarXStabilizeEnabled;
    public byte ScrollbarXStabilizeToggledHistory;
    public bool ViewportOwned;              // Viewport is owned by the window
    public bool Active;
    public bool WasActive;
    public bool WriteAccessed;
    public bool Collapsed;
    public bool WantCollapseToggle;
    public bool SkipItems;
    public bool SkipRefresh;
    public bool Appearing;
    public bool Hidden;
    public bool IsFallbackWindow;
    public bool IsExplicitChild;
    public bool HasCloseButton;
    public sbyte ResizeBorderHovered;
    public sbyte ResizeBorderHeld;
    public short BeginCount;
    public short BeginCountPreviousFrame;
    public short BeginOrderWithinParent;
    public short BeginOrderWithinContext;
    public short FocusOrder;
    public int AutoPosLastDirection;        // ImGuiDir
    public sbyte AutoFitFramesX, AutoFitFramesY;
    public bool AutoFitOnlyGrows;
    public sbyte HiddenFramesCanSkipItems;
    public sbyte HiddenFramesCannotSkipItems;
    public sbyte HiddenFramesForRenderOnly;
    public sbyte DisableInputsFrames;
    public byte BgClickFlags;               // ImGuiWindowBgClickFlags
    public byte SetWindowPosAllowFlags;     // ImGuiCond
    public byte SetWindowSizeAllowFlags;    // ImGuiCond
    public byte SetWindowCollapsedAllowFlags; // ImGuiCond
    public byte SetWindowDockAllowFlags;    // ImGuiCond
    public ImVec2 SetWindowPosVal;
    public ImVec2 SetWindowPosPivot;

    public ImVector<ImGuiID> IDStack = new ImVector<ImGuiID>();
    public ImGuiWindowTempData DC = new ImGuiWindowTempData();

    public ImRect OuterRectClipped;
    public ImRect InnerRect;
    public ImRect InnerClipRect;
    public ImRect WorkRect;
    public ImRect ParentWorkRect;
    public ImRect ClipRect;
    public ImRect ContentRegionRect;
    public ImVec2ih HitTestHoleSize;
    public ImVec2ih HitTestHoleOffset;

    public int LastFrameActive;
    public int LastFrameJustFocused;        // Last frame number the window was made Focused.
    public float LastTimeActive;
    public ImGuiStorage StateStorage = new ImGuiStorage();
    public ImVector<ImGuiOldColumns> ColumnsStorage = new ImVector<ImGuiOldColumns>();
    public float FontWindowScale;
    public float FontWindowScaleParents;
    public float FontRefSize;
    public int SettingsOffset;

    public ImDrawList DrawList;
    public ImDrawList DrawListInst;
    public ImGuiWindow ParentWindow;
    public ImGuiWindow ParentWindowInBeginStack;
    public ImGuiWindow RootWindow;                  // Point to ourself or first ancestor that is not a child window. Doesn't cross through popups/dock nodes.
    public ImGuiWindow RootWindowPopupTree;         // Point to ourself or first ancestor that is not a child window. Cross through popups parent<>child.
    public ImGuiWindow RootWindowDockTree;          // Point to ourself or first ancestor that is not a child window. Cross through dock nodes.
    public ImGuiWindow RootWindowForTitleBarHighlight;
    public ImGuiWindow RootWindowForNav;
    public ImGuiWindow ParentWindowForFocusRoute;

    public ImGuiWindow NavLastChildNavWindow;
    public ImGuiID[] NavLastIds = new ImGuiID[ImGuiNavLayer_COUNT];
    public ImRect[] NavRectRel = new ImRect[ImGuiNavLayer_COUNT];
    public ImVec2[] NavPreferredScoringPosRel = new ImVec2[ImGuiNavLayer_COUNT];
    public ImGuiID NavRootFocusScopeId;

    public int MemoryDrawListIdxCapacity;
    public int MemoryDrawListVtxCapacity;
    public bool MemoryCompacted;

    // Docking
    public bool DockIsActive;               // When docking artifacts are actually visible. When this is set, DockNode is guaranteed to be != NULL. ~~ (DockNode != NULL) && (DockNode->Windows.Size > 1).
    public bool DockNodeIsVisible;          //
    public bool DockTabIsVisible;           // Is our window visible this frame? ~~ is the corresponding tab selected?
    public bool DockTabWantClose;           //
    public short DockOrder;                 // Order of the last time the window was visible within its DockNode. This is used to reorder windows that are reappearing on the same frame.
    public ImGuiWindowDockStyle DockStyle = new ImGuiWindowDockStyle();
    public ImGuiDockNode DockNode;          // Which node are we docked into. Important: Prefer testing DockIsActive in many cases as this will still be set when the dock node is hidden.
    public ImGuiDockNode DockNodeAsHost;    // Which node are we owning (for parent windows)
    public ImGuiID DockId;                  // Backup of last valid DockNode->ID, so single window remember their dock node id even when they are not bound any more

    public ImGuiWindow(ImGuiContext context, string name)
    {
        Ctx = context;
        Name = name;
        NameBufLen = name.Length + 1;
        ID = ImGuiHash.ImHashStr(name, 0);
        IDStack.push_back(ID);
        SetWindowPosAllowFlags = SetWindowSizeAllowFlags = SetWindowCollapsedAllowFlags = SetWindowDockAllowFlags = 0;
        SetWindowPosVal = SetWindowPosPivot = new ImVec2(float.MaxValue, float.MaxValue);
        ViewportAllowPlatformMonitorExtend = -1;
        ViewportPos = new ImVec2(float.MaxValue, float.MaxValue);
        MoveId = GetID("#MOVE", null);
        TabId = GetID("#TAB", null);
        ScrollTarget = new ImVec2(float.MaxValue, float.MaxValue);
        ScrollTargetCenterRatio = new ImVec2(0.5f, 0.5f);
        AutoPosLastDirection = ImGuiDir_None;
        AutoFitFramesX = AutoFitFramesY = -1;
        LastFrameActive = -1;
        LastFrameJustFocused = -1;
        LastTimeActive = -1.0f;
        FontRefSize = 0.0f;
        FontWindowScale = FontWindowScaleParents = 1.0f;
        SettingsOffset = -1;
        DockOrder = -1;
        DrawListInst = new ImDrawList(Ctx.DrawListSharedData);
        DrawList = DrawListInst;
        DrawListInst._OwnerName = Name;
        NavPreferredScoringPosRel[0] = NavPreferredScoringPosRel[1] = new ImVec2(float.MaxValue, float.MaxValue);
    }

    public ImGuiID GetID(string str, string str_end) => ImGuiUtil.WindowGetID(this, str, str_end);
    public ImGuiID GetID(string str) => ImGuiUtil.WindowGetID(this, str, null);
    public ImGuiID GetID(int n)
    {
        // C++: ImHashData(&n, sizeof(n), seed) — hash the raw 4 bytes of the value (same as GetIDWithSeed(int)).
        ImGuiID seed = IDStack.back();
        byte[] bytes = BitConverter.GetBytes(n);
        ImGuiID id = ImGuiHash.ImHashData(bytes, 0, sizeof(int), seed);
        if (ImGui.GImGui.DebugHookIdInfoId == id)
            ImGui.DebugHookIdInfo(id, ImGuiDataType_S32, n, null);
        return id;
    }
    // C++ GetID(const void*): hashes the pointer value. The C# port hashes the reference identity.
    public ImGuiID GetID(object ptr_id)
    {
        ImGuiID seed = IDStack.back();
        byte[] bytes = BitConverter.GetBytes(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ptr_id));
        ImGuiID id = ImGuiHash.ImHashData(bytes, 0, sizeof(int), seed);
        if (ImGui.GImGui.DebugHookIdInfoId == id)
            ImGui.DebugHookIdInfo(id, ImGuiDataType_Pointer, ptr_id, null);
        return id;
    }
    public ImGuiID GetIDFromPos(ImVec2 p_abs) => ImGuiUtil.WindowGetIDFromPos(this, p_abs);
    public ImGuiID GetIDFromRectangle(ImRect r_abs) => ImGuiUtil.WindowGetIDFromRectangle(this, r_abs);

    public ImRect Rect() => new ImRect(Pos.x, Pos.y, Pos.x + Size.x, Pos.y + Size.y);
    public ImRect TitleBarRect() => new ImRect(Pos, new ImVec2(Pos.x + SizeFull.x, Pos.y + TitleBarHeight));
    public ImRect MenuBarRect() { float y1 = Pos.y + TitleBarHeight; return new ImRect(Pos.x, y1, Pos.x + SizeFull.x, y1 + MenuBarHeight); }
}
