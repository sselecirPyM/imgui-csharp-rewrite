// Auto-generated from Dear ImGui imgui.h by tools/extract_enums.py.
// All enumerations are emitted as const-int classes to mirror the C++ usage
// of flag values as plain ints.

namespace ImGui;

public static unsafe partial class ImGuiWindowFlags_
{
    public const int ImGuiWindowFlags_None = 0;
    public const int ImGuiWindowFlags_NoTitleBar = 1 << 0;
    public const int ImGuiWindowFlags_NoResize = 1 << 1;
    public const int ImGuiWindowFlags_NoMove = 1 << 2;
    public const int ImGuiWindowFlags_NoScrollbar = 1 << 3;
    public const int ImGuiWindowFlags_NoScrollWithMouse = 1 << 4;
    public const int ImGuiWindowFlags_NoCollapse = 1 << 5;
    public const int ImGuiWindowFlags_AlwaysAutoResize = 1 << 6;
    public const int ImGuiWindowFlags_NoBackground = 1 << 7;
    public const int ImGuiWindowFlags_NoSavedSettings = 1 << 8;
    public const int ImGuiWindowFlags_NoMouseInputs = 1 << 9;
    public const int ImGuiWindowFlags_MenuBar = 1 << 10;
    public const int ImGuiWindowFlags_HorizontalScrollbar = 1 << 11;
    public const int ImGuiWindowFlags_NoFocusOnAppearing = 1 << 12;
    public const int ImGuiWindowFlags_NoBringToFrontOnFocus = 1 << 13;
    public const int ImGuiWindowFlags_AlwaysVerticalScrollbar = 1 << 14;
    public const int ImGuiWindowFlags_AlwaysHorizontalScrollbar = 1<< 15;
    public const int ImGuiWindowFlags_NoNavInputs = 1 << 16;
    public const int ImGuiWindowFlags_NoNavFocus = 1 << 17;
    public const int ImGuiWindowFlags_UnsavedDocument = 1 << 18;
    public const int ImGuiWindowFlags_NoDocking = 1 << 19;
    public const int ImGuiWindowFlags_NoNav = ImGuiWindowFlags_NoNavInputs | ImGuiWindowFlags_NoNavFocus;
    public const int ImGuiWindowFlags_NoDecoration = ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoCollapse;
    public const int ImGuiWindowFlags_NoInputs = ImGuiWindowFlags_NoMouseInputs | ImGuiWindowFlags_NoNavInputs | ImGuiWindowFlags_NoNavFocus;
    public const int ImGuiWindowFlags_DockNodeHost = 1 << 23;
    public const int ImGuiWindowFlags_ChildWindow = 1 << 24;
    public const int ImGuiWindowFlags_Tooltip = 1 << 25;
    public const int ImGuiWindowFlags_Popup = 1 << 26;
    public const int ImGuiWindowFlags_Modal = 1 << 27;
    public const int ImGuiWindowFlags_ChildMenu = 1 << 28;
}

public static unsafe partial class ImGuiChildFlags_
{
    public const int ImGuiChildFlags_None = 0;
    public const int ImGuiChildFlags_Borders = 1 << 0;
    public const int ImGuiChildFlags_AlwaysUseWindowPadding = 1 << 1;
    public const int ImGuiChildFlags_ResizeX = 1 << 2;
    public const int ImGuiChildFlags_ResizeY = 1 << 3;
    public const int ImGuiChildFlags_AutoResizeX = 1 << 4;
    public const int ImGuiChildFlags_AutoResizeY = 1 << 5;
    public const int ImGuiChildFlags_AlwaysAutoResize = 1 << 6;
    public const int ImGuiChildFlags_FrameStyle = 1 << 7;
    public const int ImGuiChildFlags_NavFlattened = 1 << 8;
}

public static unsafe partial class ImGuiItemFlags_
{
    public const int ImGuiItemFlags_None = 0;
    public const int ImGuiItemFlags_NoTabStop = 1 << 0;
    public const int ImGuiItemFlags_NoNav = 1 << 1;
    public const int ImGuiItemFlags_NoNavDefaultFocus = 1 << 2;
    public const int ImGuiItemFlags_ButtonRepeat = 1 << 3;
    public const int ImGuiItemFlags_AutoClosePopups = 1 << 4;
    public const int ImGuiItemFlags_AllowDuplicateId = 1 << 5;
    public const int ImGuiItemFlags_Disabled = 1 << 6;
    public const int ImGuiItemFlags_LiveEditOnInputText = 1 << 7;
    public const int ImGuiItemFlags_LiveEditOnInputScalar = 1 << 8;
    public const int ImGuiItemFlags_LiveEditOnInput = ImGuiItemFlags_LiveEditOnInputText | ImGuiItemFlags_LiveEditOnInputScalar;
}

public static unsafe partial class ImGuiInputTextFlags_
{
    public const int ImGuiInputTextFlags_None = 0;
    public const int ImGuiInputTextFlags_CharsDecimal = 1 << 0;
    public const int ImGuiInputTextFlags_CharsHexadecimal = 1 << 1;
    public const int ImGuiInputTextFlags_CharsScientific = 1 << 2;
    public const int ImGuiInputTextFlags_CharsUppercase = 1 << 3;
    public const int ImGuiInputTextFlags_CharsNoBlank = 1 << 4;
    public const int ImGuiInputTextFlags_AllowTabInput = 1 << 5;
    public const int ImGuiInputTextFlags_EnterReturnsTrue = 1 << 6;
    public const int ImGuiInputTextFlags_EscapeClearsAll = 1 << 7;
    public const int ImGuiInputTextFlags_CtrlEnterForNewLine = 1 << 8;
    public const int ImGuiInputTextFlags_ReadOnly = 1 << 9;
    public const int ImGuiInputTextFlags_Password = 1 << 10;
    public const int ImGuiInputTextFlags_AlwaysOverwrite = 1 << 11;
    public const int ImGuiInputTextFlags_AutoSelectAll = 1 << 12;
    public const int ImGuiInputTextFlags_ParseEmptyRefVal = 1 << 13;
    public const int ImGuiInputTextFlags_DisplayEmptyRefVal = 1 << 14;
    public const int ImGuiInputTextFlags_NoHorizontalScroll = 1 << 15;
    public const int ImGuiInputTextFlags_NoUndoRedo = 1 << 16;
    public const int ImGuiInputTextFlags_ElideLeft = 1 << 17;
    public const int ImGuiInputTextFlags_CallbackCompletion = 1 << 18;
    public const int ImGuiInputTextFlags_CallbackHistory = 1 << 19;
    public const int ImGuiInputTextFlags_CallbackAlways = 1 << 20;
    public const int ImGuiInputTextFlags_CallbackCharFilter = 1 << 21;
    public const int ImGuiInputTextFlags_CallbackResize = 1 << 22;
    public const int ImGuiInputTextFlags_CallbackEdit = 1 << 23;
    public const int ImGuiInputTextFlags_WordWrap = 1 << 24;
}

public static unsafe partial class ImGuiTreeNodeFlags_
{
    public const int ImGuiTreeNodeFlags_None = 0;
    public const int ImGuiTreeNodeFlags_Selected = 1 << 0;
    public const int ImGuiTreeNodeFlags_Framed = 1 << 1;
    public const int ImGuiTreeNodeFlags_AllowOverlap = 1 << 2;
    public const int ImGuiTreeNodeFlags_NoTreePushOnOpen = 1 << 3;
    public const int ImGuiTreeNodeFlags_NoAutoOpenOnLog = 1 << 4;
    public const int ImGuiTreeNodeFlags_DefaultOpen = 1 << 5;
    public const int ImGuiTreeNodeFlags_OpenOnDoubleClick = 1 << 6;
    public const int ImGuiTreeNodeFlags_OpenOnArrow = 1 << 7;
    public const int ImGuiTreeNodeFlags_Leaf = 1 << 8;
    public const int ImGuiTreeNodeFlags_Bullet = 1 << 9;
    public const int ImGuiTreeNodeFlags_FramePadding = 1 << 10;
    public const int ImGuiTreeNodeFlags_SpanAvailWidth = 1 << 11;
    public const int ImGuiTreeNodeFlags_SpanFullWidth = 1 << 12;
    public const int ImGuiTreeNodeFlags_SpanLabelWidth = 1 << 13;
    public const int ImGuiTreeNodeFlags_SpanAllColumns = 1 << 14;
    public const int ImGuiTreeNodeFlags_LabelSpanAllColumns = 1 << 15;
    public const int ImGuiTreeNodeFlags_NavLeftJumpsToParent = 1 << 17;
    public const int ImGuiTreeNodeFlags_CollapsingHeader = ImGuiTreeNodeFlags_Framed | ImGuiTreeNodeFlags_NoTreePushOnOpen | ImGuiTreeNodeFlags_NoAutoOpenOnLog;
    public const int ImGuiTreeNodeFlags_DrawLinesNone = 1 << 18;
    public const int ImGuiTreeNodeFlags_DrawLinesFull = 1 << 19;
    public const int ImGuiTreeNodeFlags_DrawLinesToNodes = 1 << 20;
    public const int ImGuiTreeNodeFlags_NavLeftJumpsBackHere = ImGuiTreeNodeFlags_NavLeftJumpsToParent;
}

public static unsafe partial class ImGuiPopupFlags_
{
    public const int ImGuiPopupFlags_None = 0;
    public const int ImGuiPopupFlags_MouseButtonLeft = 1 << 2;
    public const int ImGuiPopupFlags_MouseButtonRight = 2 << 2;
    public const int ImGuiPopupFlags_MouseButtonMiddle = 3 << 2;
    public const int ImGuiPopupFlags_NoReopen = 1 << 5;
    public const int ImGuiPopupFlags_NoOpenOverExistingPopup = 1 << 7;
    public const int ImGuiPopupFlags_NoOpenOverItems = 1 << 8;
    public const int ImGuiPopupFlags_AnyPopupId = 1 << 10;
    public const int ImGuiPopupFlags_AnyPopupLevel = 1 << 11;
    public const int ImGuiPopupFlags_AnyPopup = ImGuiPopupFlags_AnyPopupId | ImGuiPopupFlags_AnyPopupLevel;
    public const int ImGuiPopupFlags_MouseButtonShift_ = 2;
    public const int ImGuiPopupFlags_MouseButtonMask_ = 0x0C;
    public const int ImGuiPopupFlags_InvalidMask_ = 0x03;
}

public static unsafe partial class ImGuiSelectableFlags_
{
    public const int ImGuiSelectableFlags_None = 0;
    public const int ImGuiSelectableFlags_NoAutoClosePopups = 1 << 0;
    public const int ImGuiSelectableFlags_SpanAllColumns = 1 << 1;
    public const int ImGuiSelectableFlags_AllowDoubleClick = 1 << 2;
    public const int ImGuiSelectableFlags_Disabled = 1 << 3;
    public const int ImGuiSelectableFlags_AllowOverlap = 1 << 4;
    public const int ImGuiSelectableFlags_Highlight = 1 << 5;
    public const int ImGuiSelectableFlags_SelectOnNav = 1 << 6;
    public const int ImGuiSelectableFlags_DontClosePopups = ImGuiSelectableFlags_NoAutoClosePopups;
}

public static unsafe partial class ImGuiComboFlags_
{
    public const int ImGuiComboFlags_None = 0;
    public const int ImGuiComboFlags_PopupAlignLeft = 1 << 0;
    public const int ImGuiComboFlags_HeightSmall = 1 << 1;
    public const int ImGuiComboFlags_HeightRegular = 1 << 2;
    public const int ImGuiComboFlags_HeightLarge = 1 << 3;
    public const int ImGuiComboFlags_HeightLargest = 1 << 4;
    public const int ImGuiComboFlags_NoArrowButton = 1 << 5;
    public const int ImGuiComboFlags_NoPreview = 1 << 6;
    public const int ImGuiComboFlags_WidthFitPreview = 1 << 7;
    public const int ImGuiComboFlags_HeightMask_ = ImGuiComboFlags_HeightSmall | ImGuiComboFlags_HeightRegular | ImGuiComboFlags_HeightLarge | ImGuiComboFlags_HeightLargest;
}

public static unsafe partial class ImGuiTabBarFlags_
{
    public const int ImGuiTabBarFlags_None = 0;
    public const int ImGuiTabBarFlags_Reorderable = 1 << 0;
    public const int ImGuiTabBarFlags_AutoSelectNewTabs = 1 << 1;
    public const int ImGuiTabBarFlags_TabListPopupButton = 1 << 2;
    public const int ImGuiTabBarFlags_NoCloseWithMiddleMouseButton = 1 << 3;
    public const int ImGuiTabBarFlags_NoTabListScrollingButtons = 1 << 4;
    public const int ImGuiTabBarFlags_NoTooltip = 1 << 5;
    public const int ImGuiTabBarFlags_DrawSelectedOverline = 1 << 6;
    public const int ImGuiTabBarFlags_FittingPolicyMixed = 1 << 7;
    public const int ImGuiTabBarFlags_FittingPolicyShrink = 1 << 8;
    public const int ImGuiTabBarFlags_FittingPolicyScroll = 1 << 9;
    public const int ImGuiTabBarFlags_FittingPolicyMask_ = ImGuiTabBarFlags_FittingPolicyMixed | ImGuiTabBarFlags_FittingPolicyShrink | ImGuiTabBarFlags_FittingPolicyScroll;
    public const int ImGuiTabBarFlags_FittingPolicyDefault_ = ImGuiTabBarFlags_FittingPolicyMixed;
    public const int ImGuiTabBarFlags_FittingPolicyResizeDown = ImGuiTabBarFlags_FittingPolicyShrink;
}

public static unsafe partial class ImGuiTabItemFlags_
{
    public const int ImGuiTabItemFlags_None = 0;
    public const int ImGuiTabItemFlags_UnsavedDocument = 1 << 0;
    public const int ImGuiTabItemFlags_SetSelected = 1 << 1;
    public const int ImGuiTabItemFlags_NoCloseWithMiddleMouseButton = 1 << 2;
    public const int ImGuiTabItemFlags_NoPushId = 1 << 3;
    public const int ImGuiTabItemFlags_NoTooltip = 1 << 4;
    public const int ImGuiTabItemFlags_NoReorder = 1 << 5;
    public const int ImGuiTabItemFlags_Leading = 1 << 6;
    public const int ImGuiTabItemFlags_Trailing = 1 << 7;
    public const int ImGuiTabItemFlags_NoAssumedClosure = 1 << 8;
}

public static unsafe partial class ImGuiFocusedFlags_
{
    public const int ImGuiFocusedFlags_None = 0;
    public const int ImGuiFocusedFlags_ChildWindows = 1 << 0;
    public const int ImGuiFocusedFlags_RootWindow = 1 << 1;
    public const int ImGuiFocusedFlags_AnyWindow = 1 << 2;
    public const int ImGuiFocusedFlags_NoPopupHierarchy = 1 << 3;
    public const int ImGuiFocusedFlags_DockHierarchy = 1 << 4;
    public const int ImGuiFocusedFlags_RootAndChildWindows = ImGuiFocusedFlags_RootWindow | ImGuiFocusedFlags_ChildWindows;
}

public static unsafe partial class ImGuiHoveredFlags_
{
    public const int ImGuiHoveredFlags_None = 0;
    public const int ImGuiHoveredFlags_ChildWindows = 1 << 0;
    public const int ImGuiHoveredFlags_RootWindow = 1 << 1;
    public const int ImGuiHoveredFlags_AnyWindow = 1 << 2;
    public const int ImGuiHoveredFlags_NoPopupHierarchy = 1 << 3;
    public const int ImGuiHoveredFlags_DockHierarchy = 1 << 4;
    public const int ImGuiHoveredFlags_AllowWhenBlockedByPopup = 1 << 5;
    public const int ImGuiHoveredFlags_AllowWhenBlockedByActiveItem = 1 << 7;
    public const int ImGuiHoveredFlags_AllowWhenOverlappedByItem = 1 << 8;
    public const int ImGuiHoveredFlags_AllowWhenOverlappedByWindow = 1 << 9;
    public const int ImGuiHoveredFlags_AllowWhenDisabled = 1 << 10;
    public const int ImGuiHoveredFlags_NoNavOverride = 1 << 11;
    public const int ImGuiHoveredFlags_AllowWhenOverlapped = ImGuiHoveredFlags_AllowWhenOverlappedByItem | ImGuiHoveredFlags_AllowWhenOverlappedByWindow;
    public const int ImGuiHoveredFlags_RectOnly = ImGuiHoveredFlags_AllowWhenBlockedByPopup | ImGuiHoveredFlags_AllowWhenBlockedByActiveItem | ImGuiHoveredFlags_AllowWhenOverlapped;
    public const int ImGuiHoveredFlags_RootAndChildWindows = ImGuiHoveredFlags_RootWindow | ImGuiHoveredFlags_ChildWindows;
    public const int ImGuiHoveredFlags_ForTooltip = 1 << 12;
    public const int ImGuiHoveredFlags_Stationary = 1 << 13;
    public const int ImGuiHoveredFlags_DelayNone = 1 << 14;
    public const int ImGuiHoveredFlags_DelayShort = 1 << 15;
    public const int ImGuiHoveredFlags_DelayNormal = 1 << 16;
    public const int ImGuiHoveredFlags_NoSharedDelay = 1 << 17;
}

public static unsafe partial class ImGuiDockNodeFlags_
{
    public const int ImGuiDockNodeFlags_None = 0;
    public const int ImGuiDockNodeFlags_KeepAliveOnly = 1 << 0;   // Don't display the dockspace node but keep it alive. Windows docked into this dockspace node won't be undocked.
    //ImGuiDockNodeFlags_NoCentralNode              = 1 << 1,   // Disable Central Node (the node which can stay empty)
    public const int ImGuiDockNodeFlags_NoDockingOverCentralNode = 1 << 2;   // Disable docking over the Central Node, which will be always kept empty.
    public const int ImGuiDockNodeFlags_PassthruCentralNode = 1 << 3;   // Enable passthru dockspace: 1) DockSpace() will render a ImGuiCol_WindowBg background covering everything excepted the Central Node when empty. Meaning the host window should probably use SetNextWindowBgAlpha(0.0f) prior to Begin() when using this. 2) When Central Node is empty: let inputs pass-through + won't display a DockingEmptyBg background. See demo for details.
    public const int ImGuiDockNodeFlags_NoDockingSplit = 1 << 4;   // Disable other windows/nodes from splitting this node.
    public const int ImGuiDockNodeFlags_NoResize = 1 << 5;   // Saved // Disable resizing node using the splitter/separators. Useful with programmatically setup dockspaces.
    public const int ImGuiDockNodeFlags_AutoHideTabBar = 1 << 6;   // Tab bar will automatically hide when there is a single window in the dock node.
    public const int ImGuiDockNodeFlags_NoUndocking = 1 << 7;   // Disable undocking this node.

    public const int ImGuiDockNodeFlags_NoSplit = ImGuiDockNodeFlags_NoDockingSplit; // Renamed in 1.90
    public const int ImGuiDockNodeFlags_NoDockingInCentralNode = ImGuiDockNodeFlags_NoDockingOverCentralNode; // Renamed in 1.90
}

public static unsafe partial class ImGuiDragDropFlags_
{
    public const int ImGuiDragDropFlags_None = 0;
    public const int ImGuiDragDropFlags_SourceNoPreviewTooltip = 1 << 0;
    public const int ImGuiDragDropFlags_SourceNoDisableHover = 1 << 1;
    public const int ImGuiDragDropFlags_SourceNoHoldToOpenOthers = 1 << 2;
    public const int ImGuiDragDropFlags_SourceAllowNullID = 1 << 3;
    public const int ImGuiDragDropFlags_SourceExtern = 1 << 4;
    public const int ImGuiDragDropFlags_PayloadAutoExpire = 1 << 5;
    public const int ImGuiDragDropFlags_PayloadNoCrossContext = 1 << 6;
    public const int ImGuiDragDropFlags_PayloadNoCrossProcess = 1 << 7;
    public const int ImGuiDragDropFlags_AcceptBeforeDelivery = 1 << 10;
    public const int ImGuiDragDropFlags_AcceptNoDrawDefaultRect = 1 << 11;
    public const int ImGuiDragDropFlags_AcceptNoPreviewTooltip = 1 << 12;
    public const int ImGuiDragDropFlags_AcceptDrawAsHovered = 1 << 13;
    public const int ImGuiDragDropFlags_AcceptPeekOnly = ImGuiDragDropFlags_AcceptBeforeDelivery | ImGuiDragDropFlags_AcceptNoDrawDefaultRect;
}

public static unsafe partial class ImGuiDataType_
{
    public const int ImGuiDataType_S8 = 0;
    public const int ImGuiDataType_U8 = 1;
    public const int ImGuiDataType_S16 = 2;
    public const int ImGuiDataType_U16 = 3;
    public const int ImGuiDataType_S32 = 4;
    public const int ImGuiDataType_U32 = 5;
    public const int ImGuiDataType_S64 = 6;
    public const int ImGuiDataType_U64 = 7;
    public const int ImGuiDataType_Float = 8;
    public const int ImGuiDataType_Double = 9;
    public const int ImGuiDataType_Bool = 10;
    public const int ImGuiDataType_String = 11;
    public const int ImGuiDataType_COUNT = 12;
}

public static unsafe partial class ImGuiDir_
{
    public const int ImGuiDir_None = -1;
    public const int ImGuiDir_Left = 0;
    public const int ImGuiDir_Right = 1;
    public const int ImGuiDir_Up = 2;
    public const int ImGuiDir_Down = 3;
    public const int ImGuiDir_COUNT = 4;
}

public static unsafe partial class ImGuiSortDirection_
{
    public const int ImGuiSortDirection_None = 0;
    public const int ImGuiSortDirection_Ascending = 1;
    public const int ImGuiSortDirection_Descending = 2;
}

public static unsafe partial class ImGuiKey_
{
    public const int ImGuiKey_None = 0;
    public const int ImGuiKey_NamedKey_BEGIN = 512;
    public const int ImGuiKey_Tab = 512;
    public const int ImGuiKey_LeftArrow = 513;
    public const int ImGuiKey_RightArrow = 514;
    public const int ImGuiKey_UpArrow = 515;
    public const int ImGuiKey_DownArrow = 516;
    public const int ImGuiKey_PageUp = 517;
    public const int ImGuiKey_PageDown = 518;
    public const int ImGuiKey_Home = 519;
    public const int ImGuiKey_End = 520;
    public const int ImGuiKey_Insert = 521;
    public const int ImGuiKey_Delete = 522;
    public const int ImGuiKey_Backspace = 523;
    public const int ImGuiKey_Space = 524;
    public const int ImGuiKey_Enter = 525;
    public const int ImGuiKey_Escape = 526;
    public const int ImGuiKey_LeftCtrl = 527;
    public const int ImGuiKey_LeftShift = 528;
    public const int ImGuiKey_LeftAlt = 529;
    public const int ImGuiKey_LeftSuper = 530;
    public const int ImGuiKey_RightCtrl = 531;
    public const int ImGuiKey_RightShift = 532;
    public const int ImGuiKey_RightAlt = 533;
    public const int ImGuiKey_RightSuper = 534;
    public const int ImGuiKey_Menu = 535;
    public const int ImGuiKey_0 = 536;
    public const int ImGuiKey_1 = 537;
    public const int ImGuiKey_2 = 538;
    public const int ImGuiKey_3 = 539;
    public const int ImGuiKey_4 = 540;
    public const int ImGuiKey_5 = 541;
    public const int ImGuiKey_6 = 542;
    public const int ImGuiKey_7 = 543;
    public const int ImGuiKey_8 = 544;
    public const int ImGuiKey_9 = 545;
    public const int ImGuiKey_A = 546;
    public const int ImGuiKey_B = 547;
    public const int ImGuiKey_C = 548;
    public const int ImGuiKey_D = 549;
    public const int ImGuiKey_E = 550;
    public const int ImGuiKey_F = 551;
    public const int ImGuiKey_G = 552;
    public const int ImGuiKey_H = 553;
    public const int ImGuiKey_I = 554;
    public const int ImGuiKey_J = 555;
    public const int ImGuiKey_K = 556;
    public const int ImGuiKey_L = 557;
    public const int ImGuiKey_M = 558;
    public const int ImGuiKey_N = 559;
    public const int ImGuiKey_O = 560;
    public const int ImGuiKey_P = 561;
    public const int ImGuiKey_Q = 562;
    public const int ImGuiKey_R = 563;
    public const int ImGuiKey_S = 564;
    public const int ImGuiKey_T = 565;
    public const int ImGuiKey_U = 566;
    public const int ImGuiKey_V = 567;
    public const int ImGuiKey_W = 568;
    public const int ImGuiKey_X = 569;
    public const int ImGuiKey_Y = 570;
    public const int ImGuiKey_Z = 571;
    public const int ImGuiKey_F1 = 572;
    public const int ImGuiKey_F2 = 573;
    public const int ImGuiKey_F3 = 574;
    public const int ImGuiKey_F4 = 575;
    public const int ImGuiKey_F5 = 576;
    public const int ImGuiKey_F6 = 577;
    public const int ImGuiKey_F7 = 578;
    public const int ImGuiKey_F8 = 579;
    public const int ImGuiKey_F9 = 580;
    public const int ImGuiKey_F10 = 581;
    public const int ImGuiKey_F11 = 582;
    public const int ImGuiKey_F12 = 583;
    public const int ImGuiKey_F13 = 584;
    public const int ImGuiKey_F14 = 585;
    public const int ImGuiKey_F15 = 586;
    public const int ImGuiKey_F16 = 587;
    public const int ImGuiKey_F17 = 588;
    public const int ImGuiKey_F18 = 589;
    public const int ImGuiKey_F19 = 590;
    public const int ImGuiKey_F20 = 591;
    public const int ImGuiKey_F21 = 592;
    public const int ImGuiKey_F22 = 593;
    public const int ImGuiKey_F23 = 594;
    public const int ImGuiKey_F24 = 595;
    public const int ImGuiKey_Apostrophe = 596;
    public const int ImGuiKey_Comma = 597;
    public const int ImGuiKey_Minus = 598;
    public const int ImGuiKey_Period = 599;
    public const int ImGuiKey_Slash = 600;
    public const int ImGuiKey_Semicolon = 601;
    public const int ImGuiKey_Equal = 602;
    public const int ImGuiKey_LeftBracket = 603;
    public const int ImGuiKey_Backslash = 604;
    public const int ImGuiKey_RightBracket = 605;
    public const int ImGuiKey_GraveAccent = 606;
    public const int ImGuiKey_CapsLock = 607;
    public const int ImGuiKey_ScrollLock = 608;
    public const int ImGuiKey_NumLock = 609;
    public const int ImGuiKey_PrintScreen = 610;
    public const int ImGuiKey_Pause = 611;
    public const int ImGuiKey_Keypad0 = 612;
    public const int ImGuiKey_Keypad1 = 613;
    public const int ImGuiKey_Keypad2 = 614;
    public const int ImGuiKey_Keypad3 = 615;
    public const int ImGuiKey_Keypad4 = 616;
    public const int ImGuiKey_Keypad5 = 617;
    public const int ImGuiKey_Keypad6 = 618;
    public const int ImGuiKey_Keypad7 = 619;
    public const int ImGuiKey_Keypad8 = 620;
    public const int ImGuiKey_Keypad9 = 621;
    public const int ImGuiKey_KeypadDecimal = 622;
    public const int ImGuiKey_KeypadDivide = 623;
    public const int ImGuiKey_KeypadMultiply = 624;
    public const int ImGuiKey_KeypadSubtract = 625;
    public const int ImGuiKey_KeypadAdd = 626;
    public const int ImGuiKey_KeypadEnter = 627;
    public const int ImGuiKey_KeypadEqual = 628;
    public const int ImGuiKey_AppBack = 629;
    public const int ImGuiKey_AppForward = 630;
    public const int ImGuiKey_Oem102 = 631;
    public const int ImGuiKey_GamepadStart = 632;
    public const int ImGuiKey_GamepadBack = 633;
    public const int ImGuiKey_GamepadFaceLeft = 634;
    public const int ImGuiKey_GamepadFaceRight = 635;
    public const int ImGuiKey_GamepadFaceUp = 636;
    public const int ImGuiKey_GamepadFaceDown = 637;
    public const int ImGuiKey_GamepadDpadLeft = 638;
    public const int ImGuiKey_GamepadDpadRight = 639;
    public const int ImGuiKey_GamepadDpadUp = 640;
    public const int ImGuiKey_GamepadDpadDown = 641;
    public const int ImGuiKey_GamepadL1 = 642;
    public const int ImGuiKey_GamepadR1 = 643;
    public const int ImGuiKey_GamepadL2 = 644;
    public const int ImGuiKey_GamepadR2 = 645;
    public const int ImGuiKey_GamepadL3 = 646;
    public const int ImGuiKey_GamepadR3 = 647;
    public const int ImGuiKey_GamepadLStickLeft = 648;
    public const int ImGuiKey_GamepadLStickRight = 649;
    public const int ImGuiKey_GamepadLStickUp = 650;
    public const int ImGuiKey_GamepadLStickDown = 651;
    public const int ImGuiKey_GamepadRStickLeft = 652;
    public const int ImGuiKey_GamepadRStickRight = 653;
    public const int ImGuiKey_GamepadRStickUp = 654;
    public const int ImGuiKey_GamepadRStickDown = 655;
    public const int ImGuiKey_MouseLeft = 656;
    public const int ImGuiKey_MouseRight = 657;
    public const int ImGuiKey_MouseMiddle = 658;
    public const int ImGuiKey_MouseX1 = 659;
    public const int ImGuiKey_MouseX2 = 660;
    public const int ImGuiKey_MouseWheelX = 661;
    public const int ImGuiKey_MouseWheelY = 662;
    public const int ImGuiKey_ReservedForModCtrl = 663;
    public const int ImGuiKey_ReservedForModShift = 664;
    public const int ImGuiKey_ReservedForModAlt = 665;
    public const int ImGuiKey_ReservedForModSuper = 666;
    public const int ImGuiKey_NamedKey_END = 667;
    public const int ImGuiKey_NamedKey_COUNT = ImGuiKey_NamedKey_END - ImGuiKey_NamedKey_BEGIN;
    public const int ImGuiMod_None = 0;
    public const int ImGuiMod_Ctrl = 1 << 12;
    public const int ImGuiMod_Shift = 1 << 13;
    public const int ImGuiMod_Alt = 1 << 14;
    public const int ImGuiMod_Super = 1 << 15;
    public const int ImGuiMod_Mask_ = 0xF000;
    public const int ImGuiKey_COUNT = ImGuiKey_NamedKey_END;
    public const int ImGuiMod_Shortcut = ImGuiMod_Ctrl;
}

public static unsafe partial class ImGuiInputFlags_
{
    public const int ImGuiInputFlags_None = 0;
    public const int ImGuiInputFlags_Repeat = 1 << 0;
    public const int ImGuiInputFlags_RouteActive = 1 << 10;
    public const int ImGuiInputFlags_RouteFocused = 1 << 11;
    public const int ImGuiInputFlags_RouteGlobal = 1 << 12;
    public const int ImGuiInputFlags_RouteAlways = 1 << 13;
    public const int ImGuiInputFlags_RouteOverFocused = 1 << 14;
    public const int ImGuiInputFlags_RouteOverActive = 1 << 15;
    public const int ImGuiInputFlags_RouteUnlessBgFocused = 1 << 16;
    public const int ImGuiInputFlags_RouteFromRootWindow = 1 << 17;
    public const int ImGuiInputFlags_Tooltip = 1 << 18;
}

public static unsafe partial class ImGuiConfigFlags_
{
    public const int ImGuiConfigFlags_None = 0;
    public const int ImGuiConfigFlags_NavEnableKeyboard = 1 << 0;
    public const int ImGuiConfigFlags_NavEnableGamepad = 1 << 1;
    public const int ImGuiConfigFlags_NoMouse = 1 << 4;
    public const int ImGuiConfigFlags_NoMouseCursorChange = 1 << 5;
    public const int ImGuiConfigFlags_NoKeyboard = 1 << 6;
    public const int ImGuiConfigFlags_DockingEnable = 1 << 7;
    public const int ImGuiConfigFlags_ViewportsEnable = 1 << 10;
    public const int ImGuiConfigFlags_DpiEnableScaleFonts = 1 << 14;
    public const int ImGuiConfigFlags_DpiEnableScaleViewports = 1 << 15;
    public const int ImGuiConfigFlags_IsSRGB = 1 << 20;
    public const int ImGuiConfigFlags_IsTouchScreen = 1 << 21;
    public const int ImGuiConfigFlags_NavEnableSetMousePos = 1 << 2;
    public const int ImGuiConfigFlags_NavNoCaptureKeyboard = 1 << 3;
}

public static unsafe partial class ImGuiBackendFlags_
{
    public const int ImGuiBackendFlags_None = 0;
    public const int ImGuiBackendFlags_HasGamepad = 1 << 0;
    public const int ImGuiBackendFlags_HasMouseCursors = 1 << 1;
    public const int ImGuiBackendFlags_HasSetMousePos = 1 << 2;
    public const int ImGuiBackendFlags_RendererHasVtxOffset = 1 << 3;
    public const int ImGuiBackendFlags_RendererHasTextures = 1 << 4;
    public const int ImGuiBackendFlags_RendererHasViewports = 1 << 10;
    public const int ImGuiBackendFlags_PlatformHasViewports = 1 << 11;
    public const int ImGuiBackendFlags_HasMouseHoveredViewport = 1 << 12;
    public const int ImGuiBackendFlags_HasParentViewport = 1 << 13;
}

public static unsafe partial class ImGuiCol_
{
    public const int ImGuiCol_Text = 0;
    public const int ImGuiCol_TextDisabled = 1;
    public const int ImGuiCol_WindowBg = 2;
    public const int ImGuiCol_ChildBg = 3;
    public const int ImGuiCol_PopupBg = 4;
    public const int ImGuiCol_Border = 5;
    public const int ImGuiCol_BorderShadow = 6;
    public const int ImGuiCol_FrameBg = 7;
    public const int ImGuiCol_FrameBgHovered = 8;
    public const int ImGuiCol_FrameBgActive = 9;
    public const int ImGuiCol_TitleBg = 10;
    public const int ImGuiCol_TitleBgActive = 11;
    public const int ImGuiCol_TitleBgCollapsed = 12;
    public const int ImGuiCol_MenuBarBg = 13;
    public const int ImGuiCol_ScrollbarBg = 14;
    public const int ImGuiCol_ScrollbarGrab = 15;
    public const int ImGuiCol_ScrollbarGrabHovered = 16;
    public const int ImGuiCol_ScrollbarGrabActive = 17;
    public const int ImGuiCol_CheckMark = 18;
    public const int ImGuiCol_CheckboxSelectedBg = 19;
    public const int ImGuiCol_SliderGrab = 20;
    public const int ImGuiCol_SliderGrabActive = 21;
    public const int ImGuiCol_Button = 22;
    public const int ImGuiCol_ButtonHovered = 23;
    public const int ImGuiCol_ButtonActive = 24;
    public const int ImGuiCol_Header = 25;
    public const int ImGuiCol_HeaderHovered = 26;
    public const int ImGuiCol_HeaderActive = 27;
    public const int ImGuiCol_Separator = 28;
    public const int ImGuiCol_SeparatorHovered = 29;
    public const int ImGuiCol_SeparatorActive = 30;
    public const int ImGuiCol_ResizeGrip = 31;
    public const int ImGuiCol_ResizeGripHovered = 32;
    public const int ImGuiCol_ResizeGripActive = 33;
    public const int ImGuiCol_InputTextCursor = 34;
    public const int ImGuiCol_TabHovered = 35;
    public const int ImGuiCol_Tab = 36;
    public const int ImGuiCol_TabSelected = 37;
    public const int ImGuiCol_TabSelectedOverline = 38;
    public const int ImGuiCol_TabDimmed = 39;
    public const int ImGuiCol_TabDimmedSelected = 40;
    public const int ImGuiCol_TabDimmedSelectedOverline = 41;
    public const int ImGuiCol_DockingPreview = 42;
    public const int ImGuiCol_DockingEmptyBg = 43;
    public const int ImGuiCol_PlotLines = 44;
    public const int ImGuiCol_PlotLinesHovered = 45;
    public const int ImGuiCol_PlotHistogram = 46;
    public const int ImGuiCol_PlotHistogramHovered = 47;
    public const int ImGuiCol_TableHeaderBg = 48;
    public const int ImGuiCol_TableBorderStrong = 49;
    public const int ImGuiCol_TableBorderLight = 50;
    public const int ImGuiCol_TableRowBg = 51;
    public const int ImGuiCol_TableRowBgAlt = 52;
    public const int ImGuiCol_TextLink = 53;
    public const int ImGuiCol_TextSelectedBg = 54;
    public const int ImGuiCol_TreeLines = 55;
    public const int ImGuiCol_DragDropTarget = 56;
    public const int ImGuiCol_DragDropTargetBg = 57;
    public const int ImGuiCol_UnsavedMarker = 58;
    public const int ImGuiCol_NavCursor = 59;
    public const int ImGuiCol_NavWindowingHighlight = 60;
    public const int ImGuiCol_NavWindowingDimBg = 61;
    public const int ImGuiCol_ModalWindowDimBg = 62;
    public const int ImGuiCol_COUNT = 63;
    public const int ImGuiCol_TabActive = ImGuiCol_TabSelected;
    public const int ImGuiCol_TabUnfocused = ImGuiCol_TabDimmed;
    public const int ImGuiCol_TabUnfocusedActive = ImGuiCol_TabDimmedSelected;
    public const int ImGuiCol_NavHighlight = ImGuiCol_NavCursor;
}

public static unsafe partial class ImGuiStyleVar_
{
    public const int ImGuiStyleVar_Alpha = 0;
    public const int ImGuiStyleVar_DisabledAlpha = 1;
    public const int ImGuiStyleVar_WindowPadding = 2;
    public const int ImGuiStyleVar_WindowRounding = 3;
    public const int ImGuiStyleVar_WindowBorderSize = 4;
    public const int ImGuiStyleVar_WindowMinSize = 5;
    public const int ImGuiStyleVar_WindowTitleAlign = 6;
    public const int ImGuiStyleVar_ChildRounding = 7;
    public const int ImGuiStyleVar_ChildBorderSize = 8;
    public const int ImGuiStyleVar_PopupRounding = 9;
    public const int ImGuiStyleVar_PopupBorderSize = 10;
    public const int ImGuiStyleVar_FramePadding = 11;
    public const int ImGuiStyleVar_FrameRounding = 12;
    public const int ImGuiStyleVar_FrameBorderSize = 13;
    public const int ImGuiStyleVar_ItemSpacing = 14;
    public const int ImGuiStyleVar_ItemInnerSpacing = 15;
    public const int ImGuiStyleVar_IndentSpacing = 16;
    public const int ImGuiStyleVar_CellPadding = 17;
    public const int ImGuiStyleVar_ScrollbarSize = 18;
    public const int ImGuiStyleVar_ScrollbarRounding = 19;
    public const int ImGuiStyleVar_ScrollbarPadding = 20;
    public const int ImGuiStyleVar_GrabMinSize = 21;
    public const int ImGuiStyleVar_GrabRounding = 22;
    public const int ImGuiStyleVar_ImageRounding = 23;
    public const int ImGuiStyleVar_ImageBorderSize = 24;
    public const int ImGuiStyleVar_TabRounding = 25;
    public const int ImGuiStyleVar_TabBorderSize = 26;
    public const int ImGuiStyleVar_TabMinWidthBase = 27;
    public const int ImGuiStyleVar_TabMinWidthShrink = 28;
    public const int ImGuiStyleVar_TabBarBorderSize = 29;
    public const int ImGuiStyleVar_TabBarOverlineSize = 30;
    public const int ImGuiStyleVar_TableAngledHeadersAngle = 31;
    public const int ImGuiStyleVar_TableAngledHeadersTextAlign = 32;
    public const int ImGuiStyleVar_TreeLinesSize = 33;
    public const int ImGuiStyleVar_TreeLinesRounding = 34;
    public const int ImGuiStyleVar_MenuItemRounding = 35;
    public const int ImGuiStyleVar_SelectableRounding = 36;
    public const int ImGuiStyleVar_DragDropTargetRounding = 37;
    public const int ImGuiStyleVar_ButtonTextAlign = 38;
    public const int ImGuiStyleVar_SelectableTextAlign = 39;
    public const int ImGuiStyleVar_SeparatorSize = 40;
    public const int ImGuiStyleVar_SeparatorTextBorderSize = 41;
    public const int ImGuiStyleVar_SeparatorTextAlign = 42;
    public const int ImGuiStyleVar_SeparatorTextPadding = 43;
    public const int ImGuiStyleVar_DockingSeparatorSize = 44;
    public const int ImGuiStyleVar_COUNT = 45;
}

public static unsafe partial class ImGuiButtonFlags_
{
    public const int ImGuiButtonFlags_None = 0;
    public const int ImGuiButtonFlags_MouseButtonLeft = 1 << 0;
    public const int ImGuiButtonFlags_MouseButtonRight = 1 << 1;
    public const int ImGuiButtonFlags_MouseButtonMiddle = 1 << 2;
    public const int ImGuiButtonFlags_MouseButtonMask_ = ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight | ImGuiButtonFlags_MouseButtonMiddle;
    public const int ImGuiButtonFlags_EnableNav = 1 << 3;
    public const int ImGuiButtonFlags_AllowOverlap = 1 << 12;
}

public static unsafe partial class ImGuiColorEditFlags_
{
    public const int ImGuiColorEditFlags_None = 0;
    public const int ImGuiColorEditFlags_NoAlpha = 1 << 1;
    public const int ImGuiColorEditFlags_NoPicker = 1 << 2;
    public const int ImGuiColorEditFlags_NoOptions = 1 << 3;
    public const int ImGuiColorEditFlags_NoSmallPreview = 1 << 4;
    public const int ImGuiColorEditFlags_NoInputs = 1 << 5;
    public const int ImGuiColorEditFlags_NoTooltip = 1 << 6;
    public const int ImGuiColorEditFlags_NoLabel = 1 << 7;
    public const int ImGuiColorEditFlags_NoSidePreview = 1 << 8;
    public const int ImGuiColorEditFlags_NoDragDrop = 1 << 9;
    public const int ImGuiColorEditFlags_NoBorder = 1 << 10;
    public const int ImGuiColorEditFlags_NoColorMarkers = 1 << 11;
    public const int ImGuiColorEditFlags_AlphaOpaque = 1 << 12;
    public const int ImGuiColorEditFlags_AlphaNoBg = 1 << 13;
    public const int ImGuiColorEditFlags_AlphaPreviewHalf = 1 << 14;
    public const int ImGuiColorEditFlags_AlphaBar = 1 << 18;
    public const int ImGuiColorEditFlags_HDR = 1 << 19;
    public const int ImGuiColorEditFlags_DisplayRGB = 1 << 20;
    public const int ImGuiColorEditFlags_DisplayHSV = 1 << 21;
    public const int ImGuiColorEditFlags_DisplayHex = 1 << 22;
    public const int ImGuiColorEditFlags_Uint8 = 1 << 23;
    public const int ImGuiColorEditFlags_Float = 1 << 24;
    public const int ImGuiColorEditFlags_PickerHueBar = 1 << 25;
    public const int ImGuiColorEditFlags_PickerHueWheel = 1 << 26;
    public const int ImGuiColorEditFlags_PickerNoRotate = 1 << 27;
    public const int ImGuiColorEditFlags_InputRGB = 1 << 28;
    public const int ImGuiColorEditFlags_InputHSV = 1 << 29;
    public const int ImGuiColorEditFlags_DefaultOptions_ = ImGuiColorEditFlags_Uint8 | ImGuiColorEditFlags_DisplayRGB | ImGuiColorEditFlags_InputRGB | ImGuiColorEditFlags_PickerHueBar;
    public const int ImGuiColorEditFlags_AlphaMask_ = ImGuiColorEditFlags_NoAlpha | ImGuiColorEditFlags_AlphaOpaque | ImGuiColorEditFlags_AlphaNoBg | ImGuiColorEditFlags_AlphaPreviewHalf;
    public const int ImGuiColorEditFlags_DisplayMask_ = ImGuiColorEditFlags_DisplayRGB | ImGuiColorEditFlags_DisplayHSV | ImGuiColorEditFlags_DisplayHex;
    public const int ImGuiColorEditFlags_DataTypeMask_ = ImGuiColorEditFlags_Uint8 | ImGuiColorEditFlags_Float;
    public const int ImGuiColorEditFlags_PickerMask_ = ImGuiColorEditFlags_PickerHueWheel | ImGuiColorEditFlags_PickerHueBar;
    public const int ImGuiColorEditFlags_InputMask_ = ImGuiColorEditFlags_InputRGB | ImGuiColorEditFlags_InputHSV;
    public const int ImGuiColorEditFlags_AlphaPreview = 0;
}

public static unsafe partial class ImGuiSliderFlags_
{
    public const int ImGuiSliderFlags_None = 0;
    public const int ImGuiSliderFlags_Logarithmic = 1 << 5;
    public const int ImGuiSliderFlags_NoRoundToFormat = 1 << 6;
    public const int ImGuiSliderFlags_NoInput = 1 << 7;
    public const int ImGuiSliderFlags_WrapAround = 1 << 8;
    public const int ImGuiSliderFlags_ClampOnInput = 1 << 9;
    public const int ImGuiSliderFlags_ClampZeroRange = 1 << 10;
    public const int ImGuiSliderFlags_NoSpeedTweaks = 1 << 11;
    public const int ImGuiSliderFlags_ColorMarkers = 1 << 12;
    public const int ImGuiSliderFlags_AlwaysClamp = ImGuiSliderFlags_ClampOnInput | ImGuiSliderFlags_ClampZeroRange;
    public const int ImGuiSliderFlags_InvalidMask_ = 0x7000000F;
}

public static unsafe partial class ImGuiMouseButton_
{
    public const int ImGuiMouseButton_Left = 0;
    public const int ImGuiMouseButton_Right = 1;
    public const int ImGuiMouseButton_Middle = 2;
    public const int ImGuiMouseButton_COUNT = 5;
}

public static unsafe partial class ImGuiMouseCursor_
{
    public const int ImGuiMouseCursor_None = -1;
    public const int ImGuiMouseCursor_Arrow = 0;
    public const int ImGuiMouseCursor_TextInput = 1;
    public const int ImGuiMouseCursor_ResizeAll = 2;
    public const int ImGuiMouseCursor_ResizeNS = 3;
    public const int ImGuiMouseCursor_ResizeEW = 4;
    public const int ImGuiMouseCursor_ResizeNESW = 5;
    public const int ImGuiMouseCursor_ResizeNWSE = 6;
    public const int ImGuiMouseCursor_Hand = 7;
    public const int ImGuiMouseCursor_Wait = 8;
    public const int ImGuiMouseCursor_Progress = 9;
    public const int ImGuiMouseCursor_NotAllowed = 10;
    public const int ImGuiMouseCursor_COUNT = 11;
}

public static unsafe partial class ImGuiMouseSource_
{
    public const int ImGuiMouseSource_Mouse = 0;
    public const int ImGuiMouseSource_TouchScreen = 1;
    public const int ImGuiMouseSource_Pen = 2;
    public const int ImGuiMouseSource_COUNT = 3;
}

public static unsafe partial class ImGuiCond_
{
    public const int ImGuiCond_None = 0;
    public const int ImGuiCond_Always = 1 << 0;
    public const int ImGuiCond_Once = 1 << 1;
    public const int ImGuiCond_FirstUseEver = 1 << 2;
    public const int ImGuiCond_Appearing = 1 << 3;
}

public static unsafe partial class ImGuiTableFlags_
{
    public const int ImGuiTableFlags_None = 0;
    public const int ImGuiTableFlags_Resizable = 1 << 0;
    public const int ImGuiTableFlags_Reorderable = 1 << 1;
    public const int ImGuiTableFlags_Hideable = 1 << 2;
    public const int ImGuiTableFlags_Sortable = 1 << 3;
    public const int ImGuiTableFlags_NoSavedSettings = 1 << 4;
    public const int ImGuiTableFlags_ContextMenuInBody = 1 << 5;
    public const int ImGuiTableFlags_RowBg = 1 << 6;
    public const int ImGuiTableFlags_BordersInnerH = 1 << 7;
    public const int ImGuiTableFlags_BordersOuterH = 1 << 8;
    public const int ImGuiTableFlags_BordersInnerV = 1 << 9;
    public const int ImGuiTableFlags_BordersOuterV = 1 << 10;
    public const int ImGuiTableFlags_BordersH = ImGuiTableFlags_BordersInnerH | ImGuiTableFlags_BordersOuterH;
    public const int ImGuiTableFlags_BordersV = ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_BordersOuterV;
    public const int ImGuiTableFlags_BordersInner = ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_BordersInnerH;
    public const int ImGuiTableFlags_BordersOuter = ImGuiTableFlags_BordersOuterV | ImGuiTableFlags_BordersOuterH;
    public const int ImGuiTableFlags_Borders = ImGuiTableFlags_BordersInner | ImGuiTableFlags_BordersOuter;
    public const int ImGuiTableFlags_NoBordersInBody = 1 << 11;
    public const int ImGuiTableFlags_NoBordersInBodyUntilResize = 1 << 12;
    public const int ImGuiTableFlags_SizingFixedFit = 1 << 13;
    public const int ImGuiTableFlags_SizingFixedSame = 2 << 13;
    public const int ImGuiTableFlags_SizingStretchProp = 3 << 13;
    public const int ImGuiTableFlags_SizingStretchSame = 4 << 13;
    public const int ImGuiTableFlags_NoHostExtendX = 1 << 16;
    public const int ImGuiTableFlags_NoHostExtendY = 1 << 17;
    public const int ImGuiTableFlags_NoKeepColumnsVisible = 1 << 18;
    public const int ImGuiTableFlags_PreciseWidths = 1 << 19;
    public const int ImGuiTableFlags_NoClip = 1 << 20;
    public const int ImGuiTableFlags_PadOuterX = 1 << 21;
    public const int ImGuiTableFlags_NoPadOuterX = 1 << 22;
    public const int ImGuiTableFlags_NoPadInnerX = 1 << 23;
    public const int ImGuiTableFlags_ScrollX = 1 << 24;
    public const int ImGuiTableFlags_ScrollY = 1 << 25;
    public const int ImGuiTableFlags_SortMulti = 1 << 26;
    public const int ImGuiTableFlags_SortTristate = 1 << 27;
    public const int ImGuiTableFlags_HighlightHoveredColumn = 1 << 28;
    public const int ImGuiTableFlags_SizingMask_ = ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_SizingFixedSame | ImGuiTableFlags_SizingStretchProp | ImGuiTableFlags_SizingStretchSame;
}

public static unsafe partial class ImGuiTableColumnFlags_
{
    public const int ImGuiTableColumnFlags_None = 0;
    public const int ImGuiTableColumnFlags_Disabled = 1 << 0;
    public const int ImGuiTableColumnFlags_DefaultHide = 1 << 1;
    public const int ImGuiTableColumnFlags_DefaultSort = 1 << 2;
    public const int ImGuiTableColumnFlags_WidthStretch = 1 << 3;
    public const int ImGuiTableColumnFlags_WidthFixed = 1 << 4;
    public const int ImGuiTableColumnFlags_NoResize = 1 << 5;
    public const int ImGuiTableColumnFlags_NoReorder = 1 << 6;
    public const int ImGuiTableColumnFlags_NoHide = 1 << 7;
    public const int ImGuiTableColumnFlags_NoClip = 1 << 8;
    public const int ImGuiTableColumnFlags_NoSort = 1 << 9;
    public const int ImGuiTableColumnFlags_NoSortAscending = 1 << 10;
    public const int ImGuiTableColumnFlags_NoSortDescending = 1 << 11;
    public const int ImGuiTableColumnFlags_NoHeaderLabel = 1 << 12;
    public const int ImGuiTableColumnFlags_NoHeaderWidth = 1 << 13;
    public const int ImGuiTableColumnFlags_PreferSortAscending = 1 << 14;
    public const int ImGuiTableColumnFlags_PreferSortDescending = 1 << 15;
    public const int ImGuiTableColumnFlags_IndentEnable = 1 << 16;
    public const int ImGuiTableColumnFlags_IndentDisable = 1 << 17;
    public const int ImGuiTableColumnFlags_AngledHeader = 1 << 18;
    public const int ImGuiTableColumnFlags_IsEnabled = 1 << 24;
    public const int ImGuiTableColumnFlags_IsVisible = 1 << 25;
    public const int ImGuiTableColumnFlags_IsSorted = 1 << 26;
    public const int ImGuiTableColumnFlags_IsHovered = 1 << 27;
    public const int ImGuiTableColumnFlags_WidthMask_ = ImGuiTableColumnFlags_WidthStretch | ImGuiTableColumnFlags_WidthFixed;
    public const int ImGuiTableColumnFlags_IndentMask_ = ImGuiTableColumnFlags_IndentEnable | ImGuiTableColumnFlags_IndentDisable;
    public const int ImGuiTableColumnFlags_StatusMask_ = ImGuiTableColumnFlags_IsEnabled | ImGuiTableColumnFlags_IsVisible | ImGuiTableColumnFlags_IsSorted | ImGuiTableColumnFlags_IsHovered;
    public const int ImGuiTableColumnFlags_NoDirectResize_ = 1 << 30;
}

public static unsafe partial class ImGuiTableRowFlags_
{
    public const int ImGuiTableRowFlags_None = 0;
    public const int ImGuiTableRowFlags_Headers = 1 << 0;
}

public static unsafe partial class ImGuiTableBgTarget_
{
    public const int ImGuiTableBgTarget_None = 0;
    public const int ImGuiTableBgTarget_RowBg0 = 1;
    public const int ImGuiTableBgTarget_RowBg1 = 2;
    public const int ImGuiTableBgTarget_CellBg = 3;
}

public static unsafe partial class ImGuiListClipperFlags_
{
    public const int ImGuiListClipperFlags_None = 0;
    public const int ImGuiListClipperFlags_NoSetTableRowCounters = 1 << 0;
}

public static unsafe partial class ImGuiMultiSelectFlags_
{
    public const int ImGuiMultiSelectFlags_None = 0;
    public const int ImGuiMultiSelectFlags_SingleSelect = 1 << 0;
    public const int ImGuiMultiSelectFlags_NoSelectAll = 1 << 1;
    public const int ImGuiMultiSelectFlags_NoRangeSelect = 1 << 2;
    public const int ImGuiMultiSelectFlags_NoAutoSelect = 1 << 3;
    public const int ImGuiMultiSelectFlags_NoAutoClear = 1 << 4;
    public const int ImGuiMultiSelectFlags_NoAutoClearOnReselect = 1 << 5;
    public const int ImGuiMultiSelectFlags_BoxSelect1d = 1 << 6;
    public const int ImGuiMultiSelectFlags_BoxSelect2d = 1 << 7;
    public const int ImGuiMultiSelectFlags_BoxSelectNoScroll = 1 << 8;
    public const int ImGuiMultiSelectFlags_ClearOnEscape = 1 << 9;
    public const int ImGuiMultiSelectFlags_ClearOnClickVoid = 1 << 10;
    public const int ImGuiMultiSelectFlags_ScopeWindow = 1 << 11;
    public const int ImGuiMultiSelectFlags_ScopeRect = 1 << 12;
    public const int ImGuiMultiSelectFlags_SelectOnAuto = 1 << 13;
    public const int ImGuiMultiSelectFlags_SelectOnClickAlways = 1 << 14;
    public const int ImGuiMultiSelectFlags_SelectOnClickRelease = 1 << 15;
    public const int ImGuiMultiSelectFlags_NavWrapX = 1 << 16;
    public const int ImGuiMultiSelectFlags_NoSelectOnRightClick = 1 << 17;
    public const int ImGuiMultiSelectFlags_SelectOnMask_ = ImGuiMultiSelectFlags_SelectOnAuto | ImGuiMultiSelectFlags_SelectOnClickAlways | ImGuiMultiSelectFlags_SelectOnClickRelease;
    public const int ImGuiMultiSelectFlags_CheckboxMode_ = 1 << 20;
    public const int ImGuiMultiSelectFlags_SelectOnClick = ImGuiMultiSelectFlags_SelectOnAuto;
}

public static unsafe partial class ImGuiSelectionRequestType_
{
    public const int ImGuiSelectionRequestType_None = 0;
    public const int ImGuiSelectionRequestType_SetAll = 1;
    public const int ImGuiSelectionRequestType_SetRange = 2;
}

public static unsafe partial class ImDrawFlags_
{
    public const int ImDrawFlags_None = 0;
    public const int ImDrawFlags_RoundCornersTopLeft = 1 << 4;
    public const int ImDrawFlags_RoundCornersTopRight = 1 << 5;
    public const int ImDrawFlags_RoundCornersBottomLeft = 1 << 6;
    public const int ImDrawFlags_RoundCornersBottomRight = 1 << 7;
    public const int ImDrawFlags_RoundCornersNone = 1 << 8;
    public const int ImDrawFlags_RoundCornersAll = ImDrawFlags_RoundCornersTopLeft | ImDrawFlags_RoundCornersTopRight | ImDrawFlags_RoundCornersBottomLeft | ImDrawFlags_RoundCornersBottomRight;
    public const int ImDrawFlags_RoundCornersDefault_ = ImDrawFlags_RoundCornersAll;
    public const int ImDrawFlags_RoundCornersTop = ImDrawFlags_RoundCornersTopLeft | ImDrawFlags_RoundCornersTopRight;
    public const int ImDrawFlags_RoundCornersBottom = ImDrawFlags_RoundCornersBottomLeft | ImDrawFlags_RoundCornersBottomRight;
    public const int ImDrawFlags_RoundCornersLeft = ImDrawFlags_RoundCornersBottomLeft | ImDrawFlags_RoundCornersTopLeft;
    public const int ImDrawFlags_RoundCornersRight = ImDrawFlags_RoundCornersBottomRight | ImDrawFlags_RoundCornersTopRight;
    public const int ImDrawFlags_RoundCornersMask_ = ImDrawFlags_RoundCornersAll | ImDrawFlags_RoundCornersNone;
    public const int ImDrawFlags_Closed = 1 << 9;
    public const int ImDrawFlags_InvalidMask_ = ~0x7FFFFFF0;
}

public static unsafe partial class ImDrawListFlags_
{
    public const int ImDrawListFlags_None = 0;
    public const int ImDrawListFlags_AntiAliasedLines = 1 << 0;
    public const int ImDrawListFlags_AntiAliasedLinesUseTex = 1 << 1;
    public const int ImDrawListFlags_AntiAliasedFill = 1 << 2;
    public const int ImDrawListFlags_AllowVtxOffset = 1 << 3;
    public const int ImDrawListFlags_TextNoPixelSnap = 1 << 4;
}

public static unsafe partial class ImTextureFormat_
{
    public const int ImTextureFormat_RGBA32 = 0;
    public const int ImTextureFormat_Alpha8 = 1;
}

public static unsafe partial class ImTextureStatus_
{
    public const int ImTextureStatus_OK = 0;
    public const int ImTextureStatus_Destroyed = 1;
    public const int ImTextureStatus_WantCreate = 2;
    public const int ImTextureStatus_WantUpdates = 3;
    public const int ImTextureStatus_WantDestroy = 4;
}

public static unsafe partial class ImFontAtlasFlags_
{
    public const int ImFontAtlasFlags_None = 0;
    public const int ImFontAtlasFlags_NoPowerOfTwoHeight = 1 << 0;
    public const int ImFontAtlasFlags_NoMouseCursors = 1 << 1;
    public const int ImFontAtlasFlags_NoBakedLines = 1 << 2;
}

public static unsafe partial class ImFontFlags_
{
    public const int ImFontFlags_None = 0;
    public const int ImFontFlags_NoLoadError = 1 << 1;
    public const int ImFontFlags_NoLoadGlyphs = 1 << 2;
    public const int ImFontFlags_LockBakedSizes = 1 << 3;
    public const int ImFontFlags_ImplicitRefSize = 1 << 4;
}

public static unsafe partial class ImGuiViewportFlags_
{
    public const int ImGuiViewportFlags_None = 0;
    public const int ImGuiViewportFlags_IsPlatformWindow = 1 << 0;
    public const int ImGuiViewportFlags_IsPlatformMonitor = 1 << 1;
    public const int ImGuiViewportFlags_OwnedByApp = 1 << 2;
    public const int ImGuiViewportFlags_NoDecoration = 1 << 3;
    public const int ImGuiViewportFlags_NoTaskBarIcon = 1 << 4;
    public const int ImGuiViewportFlags_NoFocusOnAppearing = 1 << 5;
    public const int ImGuiViewportFlags_NoFocusOnClick = 1 << 6;
    public const int ImGuiViewportFlags_NoInputs = 1 << 7;
    public const int ImGuiViewportFlags_NoRendererClear = 1 << 8;
    public const int ImGuiViewportFlags_NoAutoMerge = 1 << 9;
    public const int ImGuiViewportFlags_TopMost = 1 << 10;
    public const int ImGuiViewportFlags_CanHostOtherWindows = 1 << 11;
    public const int ImGuiViewportFlags_IsMinimized = 1 << 12;
    public const int ImGuiViewportFlags_IsFocused = 1 << 13;
}

public static unsafe partial class ImGuiModFlags_
{
    public const int ImGuiModFlags_None = 0;
    public const int ImGuiModFlags_Ctrl = ImGuiKey_.ImGuiMod_Ctrl;
    public const int ImGuiModFlags_Shift = ImGuiKey_.ImGuiMod_Shift;
    public const int ImGuiModFlags_Alt = ImGuiKey_.ImGuiMod_Alt;
    public const int ImGuiModFlags_Super = ImGuiKey_.ImGuiMod_Super;
}

public static unsafe partial class ImGuiKeyModFlags_
{
    public const int ImGuiKeyModFlags_None = 0;
    public const int ImGuiKeyModFlags_Ctrl = ImGuiKey_.ImGuiMod_Ctrl;
    public const int ImGuiKeyModFlags_Shift = ImGuiKey_.ImGuiMod_Shift;
    public const int ImGuiKeyModFlags_Alt = ImGuiKey_.ImGuiMod_Alt;
    public const int ImGuiKeyModFlags_Super = ImGuiKey_.ImGuiMod_Super;
}

public static unsafe partial class ImDrawTextFlags_
{
    public const int ImDrawTextFlags_None = 0;
    public const int ImDrawTextFlags_CpuFineClip = 1 << 0;
    public const int ImDrawTextFlags_WrapKeepBlanks = 1 << 1;
    public const int ImDrawTextFlags_StopOnNewLine = 1 << 2;
}

public static unsafe partial class ImWcharClass_
{
    public const int ImWcharClass_Blank = 0;
    public const int ImWcharClass_Punct = 1;
    public const int ImWcharClass_Other = 2;
}

public static unsafe partial class ImGuiDataTypePrivate_
{
    public const int ImGuiDataType_Pointer = ImGuiDataType_.ImGuiDataType_COUNT;
    public const int ImGuiDataType_ID = (ImGuiDataType_.ImGuiDataType_COUNT) + 1;
}

public static unsafe partial class ImGuiItemFlagsPrivate_
{
    public const int ImGuiItemFlags_ReadOnly = 1 << 11;
    public const int ImGuiItemFlags_MixedValue = 1 << 12;
    public const int ImGuiItemFlags_NoWindowHoverableCheck = 1 << 13;
    public const int ImGuiItemFlags_AllowOverlap = 1 << 14;
    public const int ImGuiItemFlags_NoNavDisableMouseHover = 1 << 15;
    public const int ImGuiItemFlags_NoMarkEdited = 1 << 16;
    public const int ImGuiItemFlags_NoFocus = 1 << 17;
    public const int ImGuiItemFlags_Inputable = 1 << 20;
    public const int ImGuiItemFlags_HasSelectionUserData = 1 << 21;
    public const int ImGuiItemFlags_IsMultiSelect = 1 << 22;
    public const int ImGuiItemFlags_Default_ = ImGuiItemFlags_.ImGuiItemFlags_AutoClosePopups | ImGuiItemFlags_.ImGuiItemFlags_LiveEditOnInputText;
}

public static unsafe partial class ImGuiItemStatusFlags_
{
    public const int ImGuiItemStatusFlags_None = 0;
    public const int ImGuiItemStatusFlags_HoveredRect = 1 << 0;
    public const int ImGuiItemStatusFlags_HasDisplayRect = 1 << 1;
    public const int ImGuiItemStatusFlags_Edited = 1 << 2;
    public const int ImGuiItemStatusFlags_ToggledSelection = 1 << 3;
    public const int ImGuiItemStatusFlags_ToggledOpen = 1 << 4;
    public const int ImGuiItemStatusFlags_HasDeactivated = 1 << 5;
    public const int ImGuiItemStatusFlags_Deactivated = 1 << 6;
    public const int ImGuiItemStatusFlags_HoveredWindow = 1 << 7;
    public const int ImGuiItemStatusFlags_Visible = 1 << 8;
    public const int ImGuiItemStatusFlags_HasClipRect = 1 << 9;
    public const int ImGuiItemStatusFlags_HasShortcut = 1 << 10;
    public const int ImGuiItemStatusFlags_EditedInternal = 1 << 11;
    public const int ImGuiItemStatusFlags_Openable = 1 << 20;
    public const int ImGuiItemStatusFlags_Opened = 1 << 21;
    public const int ImGuiItemStatusFlags_Checkable = 1 << 22;
    public const int ImGuiItemStatusFlags_Checked = 1 << 23;
    public const int ImGuiItemStatusFlags_Inputable = 1 << 24;
}

public static unsafe partial class ImGuiHoveredFlagsPrivate_
{
    public const int ImGuiHoveredFlags_DelayMask_ = ImGuiHoveredFlags_.ImGuiHoveredFlags_DelayNone | ImGuiHoveredFlags_.ImGuiHoveredFlags_DelayShort | ImGuiHoveredFlags_.ImGuiHoveredFlags_DelayNormal | ImGuiHoveredFlags_.ImGuiHoveredFlags_NoSharedDelay;
    public const int ImGuiHoveredFlags_AllowedMaskForIsWindowHovered = ImGuiHoveredFlags_.ImGuiHoveredFlags_ChildWindows | ImGuiHoveredFlags_.ImGuiHoveredFlags_RootWindow | ImGuiHoveredFlags_.ImGuiHoveredFlags_AnyWindow | ImGuiHoveredFlags_.ImGuiHoveredFlags_NoPopupHierarchy | ImGuiHoveredFlags_.ImGuiHoveredFlags_DockHierarchy | ImGuiHoveredFlags_.ImGuiHoveredFlags_AllowWhenBlockedByPopup | ImGuiHoveredFlags_.ImGuiHoveredFlags_AllowWhenBlockedByActiveItem | ImGuiHoveredFlags_.ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_.ImGuiHoveredFlags_Stationary;
    public const int ImGuiHoveredFlags_AllowedMaskForIsItemHovered = ImGuiHoveredFlags_.ImGuiHoveredFlags_AllowWhenBlockedByPopup | ImGuiHoveredFlags_.ImGuiHoveredFlags_AllowWhenBlockedByActiveItem | ImGuiHoveredFlags_.ImGuiHoveredFlags_AllowWhenOverlapped | ImGuiHoveredFlags_.ImGuiHoveredFlags_AllowWhenDisabled | ImGuiHoveredFlags_.ImGuiHoveredFlags_NoNavOverride | ImGuiHoveredFlags_.ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_.ImGuiHoveredFlags_Stationary | ImGuiHoveredFlags_DelayMask_;
}

public static unsafe partial class ImGuiInputTextFlagsPrivate_
{
    public const int ImGuiInputTextFlags_Multiline = 1 << 26;
    public const int ImGuiInputTextFlags_TempInput = 1 << 27;
    public const int ImGuiInputTextFlags_LocalizeDecimalPoint = 1 << 28;
}

public static unsafe partial class ImGuiButtonFlagsPrivate_
{
    public const int ImGuiButtonFlags_PressedOnClick = 1 << 4;
    public const int ImGuiButtonFlags_PressedOnClickRelease = 1 << 5;
    public const int ImGuiButtonFlags_PressedOnClickReleaseAnywhere = 1 << 6;
    public const int ImGuiButtonFlags_PressedOnRelease = 1 << 7;
    public const int ImGuiButtonFlags_PressedOnDoubleClick = 1 << 8;
    public const int ImGuiButtonFlags_PressedOnDragDropHold = 1 << 9;
    public const int ImGuiButtonFlags_FlattenChildren = 1 << 11;
    public const int ImGuiButtonFlags_AlignTextBaseLine = 1 << 15;
    public const int ImGuiButtonFlags_NoKeyModsAllowed = 1 << 16;
    public const int ImGuiButtonFlags_NoHoldingActiveId = 1 << 17;
    public const int ImGuiButtonFlags_NoNavFocus = 1 << 18;
    public const int ImGuiButtonFlags_NoHoveredOnFocus = 1 << 19;
    public const int ImGuiButtonFlags_NoSetKeyOwner = 1 << 20;
    public const int ImGuiButtonFlags_NoTestKeyOwner = 1 << 21;
    public const int ImGuiButtonFlags_NoFocus = 1 << 22;
    public const int ImGuiButtonFlags_PressedOnMask_ = ImGuiButtonFlags_PressedOnClick | ImGuiButtonFlags_PressedOnClickRelease | ImGuiButtonFlags_PressedOnClickReleaseAnywhere | ImGuiButtonFlags_PressedOnRelease | ImGuiButtonFlags_PressedOnDoubleClick | ImGuiButtonFlags_PressedOnDragDropHold;
    public const int ImGuiButtonFlags_PressedOnDefault_ = ImGuiButtonFlags_PressedOnClickRelease;
}

public static unsafe partial class ImGuiComboFlagsPrivate_
{
    public const int ImGuiComboFlags_CustomPreview = 1 << 20;
}

public static unsafe partial class ImGuiSliderFlagsPrivate_
{
    public const int ImGuiSliderFlags_Vertical = 1 << 20;
    public const int ImGuiSliderFlags_ReadOnly = 1 << 21;
}

public static unsafe partial class ImGuiSelectableFlagsPrivate_
{
    public const int ImGuiSelectableFlags_NoHoldingActiveID = 1 << 20;
    public const int ImGuiSelectableFlags_SelectOnClick = 1 << 22;
    public const int ImGuiSelectableFlags_SelectOnRelease = 1 << 23;
    public const int ImGuiSelectableFlags_SpanAvailWidth = 1 << 24;
    public const int ImGuiSelectableFlags_SetNavIdOnHover = 1 << 25;
    public const int ImGuiSelectableFlags_NoPadWithHalfSpacing = 1 << 26;
    public const int ImGuiSelectableFlags_NoSetKeyOwner = 1 << 27;
}

public static unsafe partial class ImGuiTreeNodeFlagsPrivate_
{
    public const int ImGuiTreeNodeFlags_NoNavFocus = 1 << 27;
    public const int ImGuiTreeNodeFlags_ClipLabelForTrailingButton = 1 << 28;
    public const int ImGuiTreeNodeFlags_UpsideDownArrow = 1 << 29;
    public const int ImGuiTreeNodeFlags_OpenOnMask_ = ImGuiTreeNodeFlags_.ImGuiTreeNodeFlags_OpenOnDoubleClick | ImGuiTreeNodeFlags_.ImGuiTreeNodeFlags_OpenOnArrow;
    public const int ImGuiTreeNodeFlags_DrawLinesMask_ = ImGuiTreeNodeFlags_.ImGuiTreeNodeFlags_DrawLinesNone | ImGuiTreeNodeFlags_.ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_.ImGuiTreeNodeFlags_DrawLinesToNodes;
}

public static unsafe partial class ImGuiSeparatorFlags_
{
    public const int ImGuiSeparatorFlags_None = 0;
    public const int ImGuiSeparatorFlags_Horizontal = 1 << 0;
    public const int ImGuiSeparatorFlags_Vertical = 1 << 1;
    public const int ImGuiSeparatorFlags_SpanAllColumns = 1 << 2;
}

public static unsafe partial class ImGuiFocusRequestFlags_
{
    public const int ImGuiFocusRequestFlags_None = 0;
    public const int ImGuiFocusRequestFlags_RestoreFocusedChild = 1 << 0;
    public const int ImGuiFocusRequestFlags_UnlessBelowModal = 1 << 1;
}

public static unsafe partial class ImGuiTextFlags_
{
    public const int ImGuiTextFlags_None = 0;
    public const int ImGuiTextFlags_NoWidthForLargeClippedText = 1 << 0;
}

public static unsafe partial class ImGuiTooltipFlags_
{
    public const int ImGuiTooltipFlags_None = 0;
    public const int ImGuiTooltipFlags_OverridePrevious = 1 << 1;
}

public static unsafe partial class ImGuiLayoutType_
{
    public const int ImGuiLayoutType_Horizontal = 0;
    public const int ImGuiLayoutType_Vertical = 1;
}

public static unsafe partial class ImGuiLogFlags_
{
    public const int ImGuiLogFlags_None = 0;
    public const int ImGuiLogFlags_OutputTTY = 1 << 0;
    public const int ImGuiLogFlags_OutputFile = 1 << 1;
    public const int ImGuiLogFlags_OutputBuffer = 1 << 2;
    public const int ImGuiLogFlags_OutputClipboard = 1 << 3;
    public const int ImGuiLogFlags_OutputMask_ = ImGuiLogFlags_OutputTTY | ImGuiLogFlags_OutputFile | ImGuiLogFlags_OutputBuffer | ImGuiLogFlags_OutputClipboard;
}

public static unsafe partial class ImGuiAxis_
{
    public const int ImGuiAxis_None = -1;
    public const int ImGuiAxis_X = 0;
    public const int ImGuiAxis_Y = 1;
}

public static unsafe partial class ImGuiPlotType_
{
    public const int ImGuiPlotType_Lines = 0;
    public const int ImGuiPlotType_Histogram = 1;
}

public static unsafe partial class ImGuiWindowRefreshFlags_
{
    public const int ImGuiWindowRefreshFlags_None = 0;
    public const int ImGuiWindowRefreshFlags_TryToAvoidRefresh = 1 << 0;
    public const int ImGuiWindowRefreshFlags_RefreshOnHover = 1 << 1;
    public const int ImGuiWindowRefreshFlags_RefreshOnFocus = 1 << 2;
}

public static unsafe partial class ImGuiWindowBgClickFlags_
{
    public const int ImGuiWindowBgClickFlags_None = 0;
    public const int ImGuiWindowBgClickFlags_Move = 1 << 0;
}

public static unsafe partial class ImGuiNextWindowDataFlags_
{
    public const int ImGuiNextWindowDataFlags_None = 0;
    public const int ImGuiNextWindowDataFlags_HasPos = 1 << 0;
    public const int ImGuiNextWindowDataFlags_HasSize = 1 << 1;
    public const int ImGuiNextWindowDataFlags_HasContentSize = 1 << 2;
    public const int ImGuiNextWindowDataFlags_HasCollapsed = 1 << 3;
    public const int ImGuiNextWindowDataFlags_HasSizeConstraint = 1 << 4;
    public const int ImGuiNextWindowDataFlags_HasFocus = 1 << 5;
    public const int ImGuiNextWindowDataFlags_HasBgAlpha = 1 << 6;
    public const int ImGuiNextWindowDataFlags_HasScroll = 1 << 7;
    public const int ImGuiNextWindowDataFlags_HasWindowFlags = 1 << 8;
    public const int ImGuiNextWindowDataFlags_HasChildFlags = 1 << 9;
    public const int ImGuiNextWindowDataFlags_HasRefreshPolicy = 1 << 10;
    public const int ImGuiNextWindowDataFlags_HasViewport = 1 << 11;
    public const int ImGuiNextWindowDataFlags_HasDock = 1 << 12;
    public const int ImGuiNextWindowDataFlags_HasWindowClass = 1 << 13;
}

public static unsafe partial class ImGuiNextItemDataFlags_
{
    public const int ImGuiNextItemDataFlags_None = 0;
    public const int ImGuiNextItemDataFlags_HasWidth = 1 << 0;
    public const int ImGuiNextItemDataFlags_HasOpen = 1 << 1;
    public const int ImGuiNextItemDataFlags_HasShortcut = 1 << 2;
    public const int ImGuiNextItemDataFlags_HasRefVal = 1 << 3;
    public const int ImGuiNextItemDataFlags_HasStorageID = 1 << 4;
    public const int ImGuiNextItemDataFlags_HasColorMarker = 1 << 5;
}

public static unsafe partial class ImGuiPopupPositionPolicy_
{
    public const int ImGuiPopupPositionPolicy_Default = 0;
    public const int ImGuiPopupPositionPolicy_ComboBox = 1;
    public const int ImGuiPopupPositionPolicy_Tooltip = 2;
}

public static unsafe partial class ImGuiInputEventType_
{
    public const int ImGuiInputEventType_None = 0;
    public const int ImGuiInputEventType_MousePos = 1;
    public const int ImGuiInputEventType_MouseWheel = 2;
    public const int ImGuiInputEventType_MouseButton = 3;
    public const int ImGuiInputEventType_MouseViewport = 4;
    public const int ImGuiInputEventType_Key = 5;
    public const int ImGuiInputEventType_Text = 6;
    public const int ImGuiInputEventType_Focus = 7;
    public const int ImGuiInputEventType_COUNT = 8;
}

public static unsafe partial class ImGuiInputSource_
{
    public const int ImGuiInputSource_None = 0;
    public const int ImGuiInputSource_Mouse = 1;
    public const int ImGuiInputSource_Keyboard = 2;
    public const int ImGuiInputSource_Gamepad = 3;
    public const int ImGuiInputSource_COUNT = 4;
}

public static unsafe partial class ImGuiInputFlagsPrivate_
{
    public const int ImGuiInputFlags_RepeatRateDefault = 1 << 1;
    public const int ImGuiInputFlags_RepeatRateNavMove = 1 << 2;
    public const int ImGuiInputFlags_RepeatRateNavTweak = 1 << 3;
    public const int ImGuiInputFlags_RepeatUntilRelease = 1 << 4;
    public const int ImGuiInputFlags_RepeatUntilKeyModsChange = 1 << 5;
    public const int ImGuiInputFlags_RepeatUntilKeyModsChangeFromNone = 1 << 6;
    public const int ImGuiInputFlags_RepeatUntilOtherKeyPress = 1 << 7;
    public const int ImGuiInputFlags_LockThisFrame = 1 << 20;
    public const int ImGuiInputFlags_LockUntilRelease = 1 << 21;
    public const int ImGuiInputFlags_CondHovered = 1 << 22;
    public const int ImGuiInputFlags_CondActive = 1 << 23;
    public const int ImGuiInputFlags_CondDefault_ = ImGuiInputFlags_CondHovered | ImGuiInputFlags_CondActive;
    public const int ImGuiInputFlags_RepeatRateMask_ = ImGuiInputFlags_RepeatRateDefault | ImGuiInputFlags_RepeatRateNavMove | ImGuiInputFlags_RepeatRateNavTweak;
    public const int ImGuiInputFlags_RepeatUntilMask_ = ImGuiInputFlags_RepeatUntilRelease | ImGuiInputFlags_RepeatUntilKeyModsChange | ImGuiInputFlags_RepeatUntilKeyModsChangeFromNone | ImGuiInputFlags_RepeatUntilOtherKeyPress;
    public const int ImGuiInputFlags_RepeatMask_ = ImGuiInputFlags_.ImGuiInputFlags_Repeat | ImGuiInputFlags_RepeatRateMask_ | ImGuiInputFlags_RepeatUntilMask_;
    public const int ImGuiInputFlags_CondMask_ = ImGuiInputFlags_CondHovered | ImGuiInputFlags_CondActive;
    public const int ImGuiInputFlags_RouteTypeMask_ = ImGuiInputFlags_.ImGuiInputFlags_RouteActive | ImGuiInputFlags_.ImGuiInputFlags_RouteFocused | ImGuiInputFlags_.ImGuiInputFlags_RouteGlobal | ImGuiInputFlags_.ImGuiInputFlags_RouteAlways;
    public const int ImGuiInputFlags_RouteOptionsMask_ = ImGuiInputFlags_.ImGuiInputFlags_RouteOverFocused | ImGuiInputFlags_.ImGuiInputFlags_RouteOverActive | ImGuiInputFlags_.ImGuiInputFlags_RouteUnlessBgFocused | ImGuiInputFlags_.ImGuiInputFlags_RouteFromRootWindow;
    public const int ImGuiInputFlags_SupportedByIsKeyPressed = ImGuiInputFlags_RepeatMask_;
    public const int ImGuiInputFlags_SupportedByIsMouseClicked = ImGuiInputFlags_.ImGuiInputFlags_Repeat;
    public const int ImGuiInputFlags_SupportedByShortcut = ImGuiInputFlags_RepeatMask_ | ImGuiInputFlags_RouteTypeMask_ | ImGuiInputFlags_RouteOptionsMask_;
    public const int ImGuiInputFlags_SupportedBySetNextItemShortcut = ImGuiInputFlags_RepeatMask_ | ImGuiInputFlags_RouteTypeMask_ | ImGuiInputFlags_RouteOptionsMask_ | ImGuiInputFlags_.ImGuiInputFlags_Tooltip;
    public const int ImGuiInputFlags_SupportedBySetKeyOwner = ImGuiInputFlags_LockThisFrame | ImGuiInputFlags_LockUntilRelease;
    public const int ImGuiInputFlags_SupportedBySetItemKeyOwner = ImGuiInputFlags_SupportedBySetKeyOwner | ImGuiInputFlags_CondMask_;
}

public static unsafe partial class ImGuiActivateFlags_
{
    public const int ImGuiActivateFlags_None = 0;
    public const int ImGuiActivateFlags_PreferInput = 1 << 0;
    public const int ImGuiActivateFlags_PreferTweak = 1 << 1;
    public const int ImGuiActivateFlags_TryToPreserveState = 1 << 2;
    public const int ImGuiActivateFlags_FromTabbing = 1 << 3;
    public const int ImGuiActivateFlags_FromShortcut = 1 << 4;
    public const int ImGuiActivateFlags_FromFocusApi = 1 << 5;
}

public static unsafe partial class ImGuiScrollFlags_
{
    public const int ImGuiScrollFlags_None = 0;
    public const int ImGuiScrollFlags_KeepVisibleEdgeX = 1 << 0;
    public const int ImGuiScrollFlags_KeepVisibleEdgeY = 1 << 1;
    public const int ImGuiScrollFlags_KeepVisibleCenterX = 1 << 2;
    public const int ImGuiScrollFlags_KeepVisibleCenterY = 1 << 3;
    public const int ImGuiScrollFlags_AlwaysCenterX = 1 << 4;
    public const int ImGuiScrollFlags_AlwaysCenterY = 1 << 5;
    public const int ImGuiScrollFlags_NoScrollParent = 1 << 6;
    public const int ImGuiScrollFlags_MaskX_ = ImGuiScrollFlags_KeepVisibleEdgeX | ImGuiScrollFlags_KeepVisibleCenterX | ImGuiScrollFlags_AlwaysCenterX;
    public const int ImGuiScrollFlags_MaskY_ = ImGuiScrollFlags_KeepVisibleEdgeY | ImGuiScrollFlags_KeepVisibleCenterY | ImGuiScrollFlags_AlwaysCenterY;
}

public static unsafe partial class ImGuiNavRenderCursorFlags_
{
    public const int ImGuiNavRenderCursorFlags_None = 0;
    public const int ImGuiNavRenderCursorFlags_Compact = 1 << 1;
    public const int ImGuiNavRenderCursorFlags_AlwaysDraw = 1 << 2;
    public const int ImGuiNavRenderCursorFlags_NoRounding = 1 << 3;
    public const int ImGuiNavHighlightFlags_None = ImGuiNavRenderCursorFlags_None;
    public const int ImGuiNavHighlightFlags_Compact = ImGuiNavRenderCursorFlags_Compact;
    public const int ImGuiNavHighlightFlags_AlwaysDraw = ImGuiNavRenderCursorFlags_AlwaysDraw;
    public const int ImGuiNavHighlightFlags_NoRounding = ImGuiNavRenderCursorFlags_NoRounding;
}

public static unsafe partial class ImGuiNavMoveFlags_
{
    public const int ImGuiNavMoveFlags_None = 0;
    public const int ImGuiNavMoveFlags_LoopX = 1 << 0;
    public const int ImGuiNavMoveFlags_LoopY = 1 << 1;
    public const int ImGuiNavMoveFlags_WrapX = 1 << 2;
    public const int ImGuiNavMoveFlags_WrapY = 1 << 3;
    public const int ImGuiNavMoveFlags_WrapMask_ = ImGuiNavMoveFlags_LoopX | ImGuiNavMoveFlags_LoopY | ImGuiNavMoveFlags_WrapX | ImGuiNavMoveFlags_WrapY;
    public const int ImGuiNavMoveFlags_AllowCurrentNavId = 1 << 4;
    public const int ImGuiNavMoveFlags_AlsoScoreVisibleSet = 1 << 5;
    public const int ImGuiNavMoveFlags_ScrollToEdgeY = 1 << 6;
    public const int ImGuiNavMoveFlags_Forwarded = 1 << 7;
    public const int ImGuiNavMoveFlags_DebugNoResult = 1 << 8;
    public const int ImGuiNavMoveFlags_FocusApi = 1 << 9;
    public const int ImGuiNavMoveFlags_IsTabbing = 1 << 10;
    public const int ImGuiNavMoveFlags_IsPageMove = 1 << 11;
    public const int ImGuiNavMoveFlags_Activate = 1 << 12;
    public const int ImGuiNavMoveFlags_NoSelect = 1 << 13;
    public const int ImGuiNavMoveFlags_NoSetNavCursorVisible = 1 << 14;
    public const int ImGuiNavMoveFlags_NoClearActiveId = 1 << 15;
}

public static unsafe partial class ImGuiNavLayer_
{
    public const int ImGuiNavLayer_Main = 0;
    public const int ImGuiNavLayer_Menu = 1;
    public const int ImGuiNavLayer_COUNT = 2;
}

public static unsafe partial class ImGuiTypingSelectFlags_
{
    public const int ImGuiTypingSelectFlags_None = 0;
    public const int ImGuiTypingSelectFlags_AllowBackspace = 1 << 0;
    public const int ImGuiTypingSelectFlags_AllowSingleCharMode = 1 << 1;
}

public static unsafe partial class ImGuiOldColumnFlags_
{
    public const int ImGuiOldColumnFlags_None = 0;
    public const int ImGuiOldColumnFlags_NoBorder = 1 << 0;
    public const int ImGuiOldColumnFlags_NoResize = 1 << 1;
    public const int ImGuiOldColumnFlags_NoPreserveWidths = 1 << 2;
    public const int ImGuiOldColumnFlags_NoForceWithinWindow = 1 << 3;
    public const int ImGuiOldColumnFlags_GrowParentContentsSize = 1 << 4;
}

public static unsafe partial class ImGuiLocKey_
{
    public const int ImGuiLocKey_VersionStr = 0;
    public const int ImGuiLocKey_TableSizeOne = 1;
    public const int ImGuiLocKey_TableSizeAllFit = 2;
    public const int ImGuiLocKey_TableSizeAllDefault = 3;
    public const int ImGuiLocKey_TableReset = 4;
    public const int ImGuiLocKey_TableResetOrder = 5;
    public const int ImGuiLocKey_TableResetVisibility = 6;
    public const int ImGuiLocKey_WindowingMainMenuBar = 7;
    public const int ImGuiLocKey_WindowingPopup = 8;
    public const int ImGuiLocKey_WindowingUntitled = 9;
    public const int ImGuiLocKey_OpenLink_s = 10;
    public const int ImGuiLocKey_CopyLink = 11;
    public const int ImGuiLocKey_DockingHideTabBar = 12;
    public const int ImGuiLocKey_DockingHoldShiftToDock = 13;
    public const int ImGuiLocKey_DockingDragToUndockOrMoveNode = 14;
    public const int ImGuiLocKey_COUNT = 15;
}

public static unsafe partial class ImGuiDebugLogFlags_
{
    public const int ImGuiDebugLogFlags_None = 0;
    public const int ImGuiDebugLogFlags_EventError = 1 << 0;
    public const int ImGuiDebugLogFlags_EventActiveId = 1 << 1;
    public const int ImGuiDebugLogFlags_EventFocus = 1 << 2;
    public const int ImGuiDebugLogFlags_EventPopup = 1 << 3;
    public const int ImGuiDebugLogFlags_EventNav = 1 << 4;
    public const int ImGuiDebugLogFlags_EventClipper = 1 << 5;
    public const int ImGuiDebugLogFlags_EventSelection = 1 << 6;
    public const int ImGuiDebugLogFlags_EventIO = 1 << 7;
    public const int ImGuiDebugLogFlags_EventFont = 1 << 8;
    public const int ImGuiDebugLogFlags_EventInputRouting = 1 << 9;
    public const int ImGuiDebugLogFlags_EventDocking = 1 << 10;
    public const int ImGuiDebugLogFlags_EventViewport = 1 << 11;
    public const int ImGuiDebugLogFlags_EventTable = 1 << 12;
    public const int ImGuiDebugLogFlags_EventMask_ = ImGuiDebugLogFlags_EventError | ImGuiDebugLogFlags_EventActiveId | ImGuiDebugLogFlags_EventFocus | ImGuiDebugLogFlags_EventPopup | ImGuiDebugLogFlags_EventNav | ImGuiDebugLogFlags_EventClipper | ImGuiDebugLogFlags_EventSelection | ImGuiDebugLogFlags_EventTable | ImGuiDebugLogFlags_EventIO | ImGuiDebugLogFlags_EventFont | ImGuiDebugLogFlags_EventInputRouting | ImGuiDebugLogFlags_EventDocking | ImGuiDebugLogFlags_EventViewport;
    public const int ImGuiDebugLogFlags_OutputToTTY = 1 << 20;
    public const int ImGuiDebugLogFlags_OutputToDebugger = 1 << 21;
    public const int ImGuiDebugLogFlags_OutputToTestEngine = 1 << 22;
}

public static unsafe partial class ImGuiContextHookType_
{
    public const int ImGuiContextHookType_NewFramePre = 0;
    public const int ImGuiContextHookType_NewFramePost = 1;
    public const int ImGuiContextHookType_EndFramePre = 2;
    public const int ImGuiContextHookType_EndFramePost = 3;
    public const int ImGuiContextHookType_RenderPre = 4;
    public const int ImGuiContextHookType_RenderPost = 5;
    public const int ImGuiContextHookType_Shutdown = 6;
    public const int ImGuiContextHookType_PendingRemoval_ = 7;
}

public static unsafe partial class ImGuiTabBarFlagsPrivate_
{
    public const int ImGuiTabBarFlags_DockNode = 1 << 20;
    public const int ImGuiTabBarFlags_IsFocused = 1 << 21;
    public const int ImGuiTabBarFlags_SaveSettings = 1 << 22;
}

public static unsafe partial class ImGuiTabItemFlagsPrivate_
{
    public const int ImGuiTabItemFlags_SectionMask_ = ImGuiTabItemFlags_.ImGuiTabItemFlags_Leading | ImGuiTabItemFlags_.ImGuiTabItemFlags_Trailing;
    public const int ImGuiTabItemFlags_NoCloseButton = 1 << 20;
    public const int ImGuiTabItemFlags_Button = 1 << 21;
    public const int ImGuiTabItemFlags_Invisible = 1 << 22;
    public const int ImGuiTabItemFlags_Unsorted = 1 << 23;
}

