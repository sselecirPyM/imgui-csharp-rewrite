// Port of ImGuiContext (imgui_internal.h / imgui.cpp).
using System;

namespace ImGui;

// Main Dear ImGui context
public class ImGuiContext
{
    public bool Initialized;
    public bool WithinFrameScope;
    public bool WithinFrameScopeWithImplicitWindow;
    public bool TestEngineHookItems;
    public int FrameCount;
    public int FrameCountEnded;
    public int FrameCountPlatformEnded;
    public int FrameCountRendered;
    public double Time;
    public string ContextName = "";
    public ImGuiIO IO = new ImGuiIO();
    public ImGuiPlatformIO PlatformIO = new ImGuiPlatformIO();
    public ImGuiStyle Style = new ImGuiStyle();
    public int ConfigFlagsCurrFrame;        // ImGuiConfigFlags // = g.IO.ConfigFlags at the time of NewFrame()
    public int ConfigFlagsLastFrame;        // ImGuiConfigFlags
    public ImVector<ImFontAtlas> FontAtlases = new ImVector<ImFontAtlas>();
    public ImFont Font;
    public ImFontBaked FontBaked;
    public float FontSize;
    public float FontSizeBase;
    public float FontBakedScale;
    public float CurrentPixelDensity;
    public float CurrentDpiScale;
    public ImDrawListSharedData DrawListSharedData = new ImDrawListSharedData();
    public ImGuiID WithinEndChildID;
    public ImGuiID WithinEndPopupID;
    public object TestEngine;

    // Inputs
    public ImVector<ImGuiInputEvent> InputEventsQueue = new ImVector<ImGuiInputEvent>();
    public ImVector<ImGuiInputEvent> InputEventsTrail = new ImVector<ImGuiInputEvent>();
    public int InputEventsNextMouseSource;  // ImGuiMouseSource
    public ImU32 InputEventsNextEventId;

    // Windows state
    public ImVector<ImGuiWindow> Windows = new ImVector<ImGuiWindow>();
    public ImVector<ImGuiWindow> WindowsFocusOrder = new ImVector<ImGuiWindow>();
    public ImVector<ImGuiWindow> WindowsTempSortBuffer = new ImVector<ImGuiWindow>();
    public ImVector<ImGuiWindowStackData> CurrentWindowStack = new ImVector<ImGuiWindowStackData>();
    public ImGuiStorage WindowsById = new ImGuiStorage();
    public int WindowsActiveCount;
    public float WindowsBorderHoverPadding;
    public ImGuiID DebugBreakInWindow;
    public ImGuiWindow CurrentWindow;
    public ImGuiWindow HoveredWindow;
    public ImGuiWindow HoveredWindowUnderMovingWindow;
    public ImGuiWindow HoveredWindowBeforeClear;
    public ImGuiWindow MovingWindow;
    public ImGuiWindow WheelingWindow;
    public ImVec2 WheelingWindowRefMousePos;
    public int WheelingWindowStartFrame;
    public int WheelingWindowScrolledFrame;
    public float WheelingWindowReleaseTimer;
    public ImVec2 WheelingWindowWheelRemainder;
    public ImVec2 WheelingAxisAvg;

    // Item/widgets state and tracking information
    public ImGuiID DebugDrawIdConflictsId;
    public ImGuiID DebugHookIdInfoId;
    public ImGuiID HoveredId;
    public ImGuiID HoveredIdPreviousFrame;
    public int HoveredIdPreviousFrameItemCount;
    public float HoveredIdTimer;
    public float HoveredIdNotActiveTimer;
    public bool HoveredIdAllowOverlap;
    public bool HoveredIdIsDisabled;
    public bool ItemUnclipByLog;
    public bool AnyIdHasBeenEditedThisFrame;
    public ImGuiID ActiveId;
    public ImGuiID ActiveIdIsAlive;
    public float ActiveIdTimer;
    public bool ActiveIdIsJustActivated;
    public bool ActiveIdWasSelected;
    public bool ActiveIdWasSoleSelected;
    public bool ActiveIdAllowOverlap;
    public bool ActiveIdNoClearOnFocusLoss;
    public bool ActiveIdHasBeenPressedBefore;
    public bool ActiveIdHasBeenEditedBefore;
    public bool ActiveIdHasBeenEditedThisFrame;
    public bool ActiveIdFromShortcut;
    public sbyte ActiveIdMouseButton;
    public ImGuiID ActiveIdDisabledId;
    public ImVec2 ActiveIdClickOffset;
    public int ActiveIdSource;              // ImGuiInputSource
    public ImGuiWindow ActiveIdWindow;
    public ImGuiID ActiveIdPreviousFrame;
    public ImGuiDeactivatedItemData DeactivatedItemData;
    public ImGuiDataTypeStorage ActiveIdValueOnActivation;
    public ImGuiID LastActiveId;
    public float LastActiveIdTimer;
    public bool LastActiveIdWasSelected;
    public bool LastActiveIdWasSoleSelected;

    // Key/Input Ownership + Shortcut Routing system
    public double LastKeyModsChangeTime;
    public double LastKeyModsChangeFromNoneTime;
    public double LastKeyboardKeyPressTime;
    public bool[] KeysMayBeCharInput = new bool[ImGuiKey_NamedKey_COUNT];
    public ImGuiKeyOwnerData[] KeysOwnerData = new ImGuiKeyOwnerData[ImGuiKey_NamedKey_COUNT];
    public ImGuiKeyRoutingTable KeysRoutingTable = new ImGuiKeyRoutingTable();
    public ImU32 ActiveIdUsingNavDirMask;
    public bool ActiveIdUsingAllKeyboardKeys;
    public ImGuiKeyChord DebugBreakInShortcutRouting;

    // Next window/item data
    public ImGuiID CurrentFocusScopeId;
    public int CurrentItemFlags;            // ImGuiItemFlags
    public ImGuiID DebugLocateId;
    public ImGuiNextItemData NextItemData;
    public ImGuiLastItemData LastItemData;
    public ImGuiNextWindowData NextWindowData;
    public bool DebugShowGroupRects;
    public bool GcCompactAll;

    // Shared stacks
    public int DebugFlashStyleColorIdx;    // ImGuiCol
    public ImVector<ImGuiColorMod> ColorStack = new ImVector<ImGuiColorMod>();
    public ImVector<ImGuiStyleMod> StyleVarStack = new ImVector<ImGuiStyleMod>();
    public ImVector<ImFontStackData> FontStack = new ImVector<ImFontStackData>();
    public ImVector<ImGuiFocusScopeData> FocusScopeStack = new ImVector<ImGuiFocusScopeData>();
    public ImVector<int> ItemFlagsStack = new ImVector<int>();
    public ImVector<ImGuiGroupData> GroupStack = new ImVector<ImGuiGroupData>();
    public ImVector<ImGuiPopupData> OpenPopupStack = new ImVector<ImGuiPopupData>();
    public ImVector<ImGuiPopupData> BeginPopupStack = new ImVector<ImGuiPopupData>();
    public ImVector<ImGuiTreeNodeStackData> TreeNodeStack = new ImVector<ImGuiTreeNodeStackData>();

    // Viewports
    public ImVector<ImGuiViewportP> Viewports = new ImVector<ImGuiViewportP>();
    public ImGuiViewportP CurrentViewport;              // We track changes of viewport (happening in Begin) so we can call Platform_OnChangedViewport()
    public ImGuiViewportP MouseViewport;
    public ImGuiViewportP MouseLastHoveredViewport;     // Last known viewport that was hovered by mouse (even if we are not hovering any viewport any more) + honoring the _NoInputs flag.
    public ImGuiID PlatformLastFocusedViewportId;
    public ImGuiPlatformMonitor FallbackMonitor = new ImGuiPlatformMonitor();   // Virtual monitor used as fallback if backend doesn't provide monitor information.
    public ImRect PlatformMonitorsFullWorkRect = new ImRect();  // Bounding box of all platform monitors
    public int ViewportCreatedCount;                    // Unique sequential creation counter (mostly for testing/debugging)
    public int PlatformWindowsCreatedCount;             // Unique sequential creation counter (mostly for testing/debugging)
    public int ViewportFocusedStampCount;               // Every time the front-most window changes, we stamp its viewport with an incrementing counter

    // Keyboard/Gamepad Navigation
    public bool NavCursorVisible;
    public bool NavHighlightItemUnderNav;
    public bool NavMousePosDirty;
    public bool NavIdIsAlive;
    public ImGuiID NavId;
    public ImGuiWindow NavWindow;
    public ImGuiID NavFocusScopeId;
    public int NavLayer;                    // ImGuiNavLayer
    public int NavIdItemFlags;              // ImGuiItemFlags
    public ImGuiID NavActivateId;
    public ImGuiID NavActivateDownId;
    public ImGuiID NavActivatePressedId;
    public int NavActivateFlags;            // ImGuiActivateFlags
    public ImVector<ImGuiFocusScopeData> NavFocusRoute = new ImVector<ImGuiFocusScopeData>();
    public ImGuiID NavHighlightActivatedId;
    public float NavHighlightActivatedTimer;
    public ImGuiID NavOpenContextMenuItemId;
    public ImGuiID NavOpenContextMenuWindowId;
    public ImGuiID NavNextActivateId;
    public int NavNextActivateFlags;        // ImGuiActivateFlags
    public int NavInputSource;              // ImGuiInputSource
    public long NavLastValidSelectionUserData; // ImGuiSelectionUserData
    public sbyte NavCursorHideFrames;

    // Navigation: Init & Move Requests
    public bool NavAnyRequest;
    public bool NavInitRequest;
    public bool NavInitRequestFromMove;
    public ImGuiNavItemData NavInitResult = new ImGuiNavItemData();
    public bool NavMoveSubmitted;
    public bool NavMoveScoringItems;
    public bool NavMoveForwardToNextFrame;
    public int NavMoveFlags;                // ImGuiNavMoveFlags
    public int NavMoveScrollFlags;          // ImGuiScrollFlags
    public ImGuiKeyChord NavMoveKeyMods;
    public int NavMoveDir;                  // ImGuiDir
    public int NavMoveDirForDebug;          // ImGuiDir
    public int NavMoveClipDir;              // ImGuiDir
    public ImRect NavScoringRect = new ImRect();
    public ImRect NavScoringNoClipRect = new ImRect();
    public int NavScoringDebugCount;
    public int NavTabbingDir;
    public int NavTabbingCounter;
    public ImGuiNavItemData NavMoveResultLocal = new ImGuiNavItemData();
    public ImGuiNavItemData NavMoveResultLocalVisible = new ImGuiNavItemData();
    public ImGuiNavItemData NavMoveResultOther = new ImGuiNavItemData();
    public ImGuiNavItemData NavTabbingResultFirst = new ImGuiNavItemData();

    // Navigation: record of last move request
    public ImGuiID NavJustMovedFromFocusScopeId;
    public ImGuiID NavJustMovedToId;
    public ImGuiID NavJustMovedToFocusScopeId;
    public ImGuiKeyChord NavJustMovedToKeyMods;
    public bool NavJustMovedToIsTabbing;
    public bool NavJustMovedToHasSelectionData;

    // Navigation: extra config options
    public bool ConfigNavEnableTabbing;
    public bool ConfigNavWindowingWithGamepad;
    public ImGuiKeyChord ConfigNavWindowingKeyNext;
    public ImGuiKeyChord ConfigNavWindowingKeyPrev;

    // Navigation: Windowing
    public ImGuiWindow NavWindowingTarget;
    public ImGuiWindow NavWindowingTargetAnim;
    public ImGuiWindow NavWindowingListWindow;
    public float NavWindowingTimer;
    public float NavWindowingHighlightAlpha;
    public int NavWindowingInputSource;     // ImGuiInputSource
    public bool NavWindowingToggleLayer;
    public ImGuiKey NavWindowingToggleKey;
    public ImVec2 NavWindowingAccumDeltaPos;
    public ImVec2 NavWindowingAccumDeltaSize;

    // Render
    public float DimBgRatio;

    // Drag and Drop
    public bool DragDropActive;
    public bool DragDropWithinSource;
    public bool DragDropWithinTarget;
    public int DragDropSourceFlags;         // ImGuiDragDropFlags
    public int DragDropSourceFrameCount;
    public int DragDropMouseButton;
    public ImGuiPayload DragDropPayload = new ImGuiPayload();
    public ImRect DragDropTargetRect = new ImRect();
    public ImRect DragDropTargetClipRect = new ImRect();
    public ImGuiID DragDropTargetId;
    public ImGuiID DragDropTargetFullViewport;
    public int DragDropAcceptFlagsCurr;     // ImGuiDragDropFlags
    public int DragDropAcceptFlagsPrev;     // ImGuiDragDropFlags
    public float DragDropAcceptIdCurrRectSurface;
    public ImGuiID DragDropAcceptIdCurr;
    public ImGuiID DragDropAcceptIdPrev;
    public int DragDropAcceptFrameCount;
    public ImGuiID DragDropHoldJustPressedId;
    public ImVector<byte> DragDropPayloadBufHeap = new ImVector<byte>();
    public byte[] DragDropPayloadBufLocal = new byte[16];

    // Clipper
    public int ClipperTempDataStacked;
    public ImVector<ImGuiListClipperData> ClipperTempData = new ImVector<ImGuiListClipperData>();

    // Tables
    public ImGuiTable CurrentTable;
    public ImGuiID DebugBreakInTable;
    public int TablesTempDataStacked;
    public ImVector<ImGuiTableTempData> TablesTempData = new ImVector<ImGuiTableTempData>();
    public ImPool<ImGuiTable> Tables = new ImPool<ImGuiTable>();
    public ImVector<float> TablesLastTimeActive = new ImVector<float>();
    public ImVector<ImDrawChannel> DrawChannelsTempMergeBuffer = new ImVector<ImDrawChannel>();

    // Tab bars
    public ImGuiTabBar CurrentTabBar;
    public ImPool<ImGuiTabBar> TabBars = new ImPool<ImGuiTabBar>();
    public ImVector<ImGuiPtrOrIndex> CurrentTabBarStack = new ImVector<ImGuiPtrOrIndex>();
    public ImVector<ImGuiShrinkWidthItem> ShrinkWidthBuffer = new ImVector<ImGuiShrinkWidthItem>();

    // Multi-Select state
    public ImGuiBoxSelectState BoxSelectState = new ImGuiBoxSelectState();
    public ImGuiMultiSelectTempData CurrentMultiSelect;
    public int MultiSelectTempDataStacked;
    public ImVector<ImGuiMultiSelectTempData> MultiSelectTempData = new ImVector<ImGuiMultiSelectTempData>();
    public ImPool<ImGuiMultiSelectState> MultiSelectStorage = new ImPool<ImGuiMultiSelectState>();

    // Hover Delay system
    public ImGuiID HoverItemDelayId;
    public ImGuiID HoverItemDelayIdPreviousFrame;
    public float HoverItemDelayTimer;
    public float HoverItemDelayClearTimer;
    public ImGuiID HoverItemUnlockedStationaryId;
    public ImGuiID HoverWindowUnlockedStationaryId;

    // Mouse state
    public int MouseCursor;                 // ImGuiMouseCursor
    public float MouseStationaryTimer;
    public ImVec2 MouseLastValidPos;

    // Widget state
    public ImGuiInputTextState InputTextState = new ImGuiInputTextState();
    public ImGuiTextIndex InputTextLineIndex = new ImGuiTextIndex();
    public ImGuiInputTextDeactivatedState InputTextDeactivatedState = new ImGuiInputTextDeactivatedState();
    public ImFontBaked InputTextPasswordFontBackupBaked;
    public int InputTextPasswordFontBackupFlags; // ImFontFlags
    public ImGuiID InputTextReactivateId;
    public ImGuiID TempInputId;
    public ImGuiDataTypeStorage DataTypeZeroValue;
    public int BeginMenuDepth;
    public int BeginComboDepth;
    public ImGuiID ColorEditCurrentID;
    public ImGuiID ColorEditSavedID;
    public float ColorEditSavedHue;
    public float ColorEditSavedSat;
    public ImU32 ColorEditSavedColor;
    public ImVec4 ColorPickerRef;
    public ImGuiComboPreviewData ComboPreviewData;
    public ImRect WindowResizeBorderExpectedRect = new ImRect();
    public bool WindowResizeRelativeMode;
    public short ScrollbarSeekMode;
    public float ScrollbarClickDeltaToGrabCenter;
    public float SliderGrabClickOffset;
    public float SliderCurrentAccum;
    public bool SliderCurrentAccumDirty;
    public bool DragCurrentAccumDirty;
    public float DragCurrentAccum;
    public float DragSpeedDefaultRatio;
    public float DisabledAlphaBackup;
    public short DisabledStackSize;
    public short TooltipOverrideCount;
    public ImGuiWindow TooltipPreviousWindow;
    public ImVector<byte> ClipboardHandlerData = new ImVector<byte>();
    public ImVector<ImGuiID> MenusIdSubmittedThisFrame = new ImVector<ImGuiID>();
    public ImGuiTypingSelectState TypingSelectState = new ImGuiTypingSelectState();

    // Platform support
    public ImGuiPlatformImeData PlatformImeData = new ImGuiPlatformImeData();
    public ImGuiPlatformImeData PlatformImeDataPrev = new ImGuiPlatformImeData();

    // Extensions
    public ImVector<ImTextureData> UserTextures = new ImVector<ImTextureData>();    // List of textures created/managed by user or third-party extension. Automatically appended into platform_io.Textures[].
    public ImGuiDockContext DockContext = new ImGuiDockContext();
    public Action<ImGuiContext, ImGuiDockNode, ImGuiTabBar> DockNodeWindowMenuHandler;

    // Settings
    public ImGuiPackedDate SessionDate;
    public bool SettingsLoaded;
    public float SettingsDirtyTimer;
    public ImGuiTextBuffer SettingsIniData = new ImGuiTextBuffer();
    public ImVector<ImGuiSettingsHandler> SettingsHandlers = new ImVector<ImGuiSettingsHandler>();
    public ImChunkStream<ImGuiWindowSettings> SettingsWindows = new ImChunkStream<ImGuiWindowSettings>();
    public ImChunkStream<ImGuiTableSettings> SettingsTables = new ImChunkStream<ImGuiTableSettings>(); // C# adaptation: object-based chunk stream (C++: raw ImChunkStream<ImGuiTableSettings> memory chunks); SettingsOffset is an index. See ImGuiTablesDraw.cs.

    // Hooks
    public ImVector<ImGuiContextHook> Hooks = new ImVector<ImGuiContextHook>();
    public ImGuiID HookIdNext;
    public Action<string, int, string> DemoMarkerCallback;

    // Localization
    public string[] LocalizationTable = new string[ImGuiLocKey_COUNT];

    // Capture/Logging
    public bool LogEnabled;
    public bool LogLineFirstItem;
    public int LogFlags;                    // ImGuiLogFlags
    public ImGuiWindow LogWindow;
    public ImFileHandle LogFile;
    public ImGuiTextBuffer LogBuffer = new ImGuiTextBuffer();
    public string LogNextPrefix;
    public string LogNextSuffix;
    public float LogLinePosY;
    public int LogDepthRef;
    public int LogDepthToExpand;
    public int LogDepthToExpandDefault;

    // Error Handling
    public Action<ImGuiContext, object, string> ErrorCallback;
    public object ErrorCallbackUserData;
    public ImVec2 ErrorTooltipLockedPos;
    public bool ErrorFirst;
    public int ErrorCountCurrentFrame;
    public ImGuiErrorRecoveryState StackSizesInNewFrame;
    public ImGuiErrorRecoveryState StackSizesInBeginForCurrentWindow;

    // Debug Tools
    public int DebugDrawIdConflictsCount;
    public int DebugLogFlags;               // ImGuiDebugLogFlags
    public ImGuiTextBuffer DebugLogBuf = new ImGuiTextBuffer();
    public ImGuiTextIndex DebugLogIndex = new ImGuiTextIndex();
    public int DebugLogSkippedErrors;
    public int DebugLogAutoDisableFlags;    // ImGuiDebugLogFlags
    public byte DebugLogAutoDisableFrames;
    public byte DebugLocateFrames;
    public bool DebugBreakInLocateId;
    public ImGuiKeyChord DebugBreakKeyChord;
    public sbyte DebugBeginReturnValueCullDepth;
    public bool DebugItemPickerActive;
    public byte DebugItemPickerMouseButton;
    public ImGuiID DebugItemPickerBreakId;
    public float DebugFlashStyleColorTime;
    public ImVec4 DebugFlashStyleColorBackup;
    public ImGuiMetricsConfig DebugMetricsConfig = new ImGuiMetricsConfig();
    public ImGuiDebugItemPathQuery DebugItemPathQuery = new ImGuiDebugItemPathQuery();
    public ImGuiIDStackTool DebugIDStackTool = new ImGuiIDStackTool();
    public ImGuiDebugAllocInfo DebugAllocInfo = new ImGuiDebugAllocInfo();
    public ImGuiDockNode DebugHoveredDockNode;          // Hovered dock node.

    // Misc
    public float[] FramerateSecPerFrame = new float[60];
    public int FramerateSecPerFrameIdx;
    public int FramerateSecPerFrameCount;
    public float FramerateSecPerFrameAccum;
    public int WantCaptureMouseNextFrame;
    public int WantCaptureKeyboardNextFrame;
    public int WantTextInputNextFrame;
    public ImVector<byte> TempBuffer = new ImVector<byte>();
    public string TempKeychordName = "";

    public ImGuiContext(ImFontAtlas shared_font_atlas)
    {
        IO.Ctx = this;
        InputTextState.Initialize(this);

        FrameCount = 0;
        FrameCountEnded = FrameCountPlatformEnded = FrameCountRendered = -1;
        Time = 0.0f;
        ConfigFlagsCurrFrame = ConfigFlagsLastFrame = ImGuiConfigFlags_None;

        // Init base localization (C++: LocalizeRegisterEntries(GLocalizationEntriesEnUS, ...))
        LocalizationTable[ImGuiLocKey_VersionStr] = "Dear ImGui " + ImGui.ImGuiVersion + " (" + ImGui.ImGuiVersionNum + ")";
        LocalizationTable[ImGuiLocKey_TableSizeOne] = "Size column to fit###SizeOne";
        LocalizationTable[ImGuiLocKey_TableSizeAllFit] = "Size all columns to fit###SizeAll";
        LocalizationTable[ImGuiLocKey_TableSizeAllDefault] = "Size all columns to default###SizeAll";
        LocalizationTable[ImGuiLocKey_TableReset] = "Reset";
        LocalizationTable[ImGuiLocKey_TableResetOrder] = "Reset order###ResetOrder";
        LocalizationTable[ImGuiLocKey_TableResetVisibility] = "Reset visibility###ResetVisibility";
        LocalizationTable[ImGuiLocKey_WindowingMainMenuBar] = "(Main menu bar)";
        LocalizationTable[ImGuiLocKey_WindowingPopup] = "(Popup)";
        LocalizationTable[ImGuiLocKey_WindowingUntitled] = "(Untitled)";
        LocalizationTable[ImGuiLocKey_OpenLink_s] = "Open '%s'";
        LocalizationTable[ImGuiLocKey_CopyLink] = "Copy Link###CopyLink";
        LocalizationTable[ImGuiLocKey_DockingHideTabBar] = "Hide tab bar###HideTabBar";
        LocalizationTable[ImGuiLocKey_DockingHoldShiftToDock] = "Hold SHIFT to enable Docking window.";
        LocalizationTable[ImGuiLocKey_DockingDragToUndockOrMoveNode] = "Click and drag to move or undock whole node.";

        FontSize = FontSizeBase = FontBakedScale = CurrentDpiScale = 0.0f;
        CurrentPixelDensity = 1.0f;
        IO.Fonts = shared_font_atlas ?? new ImFontAtlas();
        if (shared_font_atlas == null)
            IO.Fonts.OwnerContext = this;

        InputEventsNextMouseSource = ImGuiMouseSource_Mouse;
        InputEventsNextEventId = 1;

        ActiveIdClickOffset = new ImVec2(-1, -1);
        ActiveIdSource = ImGuiInputSource_None;
        ActiveIdMouseButton = -1;

        LastKeyboardKeyPressTime = LastKeyModsChangeTime = LastKeyModsChangeFromNoneTime = -1.0;

        NavLayer = ImGuiNavLayer_Main;
        NavMoveDir = NavMoveDirForDebug = NavMoveClipDir = ImGuiDir_None;
        NavInputSource = ImGuiInputSource_Keyboard;
        NavLastValidSelectionUserData = -1;

        ConfigNavEnableTabbing = true;
        ConfigNavWindowingWithGamepad = true;
        ConfigNavWindowingKeyNext = IO.ConfigMacOSXBehaviors ? (ImGuiMod_Super | ImGuiKey_Tab) : (ImGuiMod_Ctrl | ImGuiKey_Tab);
        ConfigNavWindowingKeyPrev = IO.ConfigMacOSXBehaviors ? (ImGuiMod_Super | ImGuiMod_Shift | ImGuiKey_Tab) : (ImGuiMod_Ctrl | ImGuiMod_Shift | ImGuiKey_Tab);
        NavWindowingInputSource = ImGuiInputSource_None;
        NavWindowingToggleKey = ImGuiKey_None;

        DragDropSourceFrameCount = -1;
        DragDropMouseButton = -1;
        DragDropAcceptFrameCount = -1;

        MouseCursor = ImGuiMouseCursor_Arrow;

        InputTextPasswordFontBackupFlags = ImFontFlags_None;

        DragSpeedDefaultRatio = 1.0f / 100.0f;

        PlatformImeData.InputPos = new ImVec2(0.0f, 0.0f);
        PlatformImeDataPrev.InputPos = new ImVec2(-1.0f, -1.0f); // Different to ensure initial submission

        LogLinePosY = float.MaxValue;
        LogDepthToExpand = LogDepthToExpandDefault = 2;

        ErrorFirst = true;

        DebugLogFlags = ImGuiDebugLogFlags_EventError | ImGuiDebugLogFlags_OutputToTTY;
        DebugBeginReturnValueCullDepth = -1;
        DebugItemPickerMouseButton = ImGuiMouseButton_Left;
        DebugFlashStyleColorIdx = ImGuiCol_COUNT;

        DebugBreakKeyChord = ImGuiKey_Pause;
        DebugBreakInShortcutRouting = ImGuiKey_None;

        WantCaptureMouseNextFrame = WantCaptureKeyboardNextFrame = WantTextInputNextFrame = -1;
    }
}
