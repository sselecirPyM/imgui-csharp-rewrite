// Port of Dear ImGui internal data structures (imgui_internal.h).
using System;
using System.Collections.Generic;

namespace ImGui;

// Helper to build a ImDrawData instance
public class ImDrawDataBuilder
{
    public ImVector<ImDrawList>[] Layers = new ImVector<ImDrawList>[2]; // Pointers to global layers for: regular, tooltip. Layers[0] is owned by DrawData.
    public ImVector<ImDrawList> LayerData1 = new ImVector<ImDrawList>();

    public ImVector<ImDrawList> GetLayer(int n) => Layers[n] ?? (Layers[n] = new ImVector<ImDrawList>());
}

public struct ImFontStackData
{
    public ImFont Font;
    public float FontSizeBeforeScaling;     // ~~ style.FontSizeBase
    public float FontSizeAfterScaling;      // ~~ g.FontSize
}

// Stacked color modifier, backup of modified data so we can restore it
public struct ImGuiColorMod
{
    public int Col;                         // ImGuiCol
    public ImVec4 BackupValue;
}

// Stacked style modifier, backup of modified data so we can restore it. Data type inferred from the variable.
public struct ImGuiStyleMod
{
    public int VarIdx;                      // ImGuiStyleVar
    public int BackupInt0, BackupInt1;
    public float BackupFloat0, BackupFloat1;

    public ImGuiStyleMod(int idx, int v) { VarIdx = idx; BackupInt0 = v; BackupInt1 = 0; BackupFloat0 = BackupFloat1 = 0; }
    public ImGuiStyleMod(int idx, float v) { VarIdx = idx; BackupFloat0 = v; BackupFloat1 = 0; BackupInt0 = BackupInt1 = 0; }
    public ImGuiStyleMod(int idx, ImVec2 v) { VarIdx = idx; BackupFloat0 = v.x; BackupFloat1 = v.y; BackupInt0 = BackupInt1 = 0; }
}

// Opaque storage to fit any data up to 8 bytes
public struct ImGuiDataTypeStorage
{
    public ulong Data;                      // raw 8-byte storage
}

public struct ImGuiComboPreviewData
{
    public ImRect PreviewRect;
    public ImVec2 BackupCursorPos;
    public ImVec2 BackupCursorMaxPos;
    public ImVec2 BackupCursorPosPrevLine;
    public float BackupPrevLineTextBaseOffset;
    public int BackupLayout;                // ImGuiLayoutType
}

// Stacked storage data for BeginGroup()/EndGroup()
public struct ImGuiGroupData
{
    public ImGuiID WindowID;
    public ImVec2 BackupCursorPos;
    public ImVec2 BackupCursorMaxPos;
    public ImVec2 BackupCursorPosPrevLine;
    public ImVec1 BackupIndent;
    public ImVec1 BackupGroupOffset;
    public ImVec2 BackupCurrLineSize;
    public float BackupCurrLineTextBaseOffset;
    public ImGuiID BackupActiveIdIsAlive;
    public bool BackupAnyIdHasBeenEditedThisFrame;
    public bool BackupDeactivatedIdIsAlive;
    public bool BackupHoveredIdIsAlive;
    public bool BackupIsSameLine;
    public bool EmitItem;
}

// Simple column measurement, currently used for MenuItem() only.
public class ImGuiMenuColumns
{
    public ImU32 TotalWidth;
    public ImU32 NextTotalWidth;
    public ImU16 Spacing;
    public ImU16 OffsetIcon;            // Always zero for now
    public ImU16 OffsetLabel;           // Offsets are locked in Update()
    public ImU16 OffsetShortcut;
    public ImU16 OffsetMark;
    public ImU16[] Widths = new ImU16[4];   // Width of: Icon, Label, Shortcut, Mark (accumulators for current frame)

    public void Update(float spacing, bool window_reappearing) => ImGuiUtil.MenuColumnsUpdate(this, spacing, window_reappearing);
    public float DeclColumns(float w_icon, float w_label, float w_shortcut, float w_mark) => ImGuiUtil.MenuColumnsDeclColumns(this, w_icon, w_label, w_shortcut, w_mark);
    public void CalcNextTotalWidth(bool update_offsets) => ImGuiUtil.MenuColumnsCalcNextTotalWidth(this, update_offsets);
}

// Internal temporary state for deactivating InputText() instances.
public class ImGuiInputTextDeactivatedState
{
    public ImGuiID ID;
    public int ElapseFrame;
    public ImVector<byte> TextA = new ImVector<byte>();

    public void ClearFreeMemory() { ID = 0; ElapseFrame = 0; TextA.clear(); }
}

// Undo record for stb_textedit (imstb_textedit.h StbUndoRecord)
public struct StbUndoRecord
{
    public int where;               // IMSTB_TEXTEDIT_POSITIONTYPE
    public int insert_length;
    public int delete_length;
    public int char_storage;        // -1 when unused
}

// Undo storage for stb_textedit (imstb_textedit.h STB_TexteditState + StbUndoState)
public class ImStbTexteditState
{
    // STB_TexteditState - public data
    public int cursor;
    // position of the text cursor within the string
    public int select_start;                // selection start point
    public int select_end;                  // selection start and end point in characters; if equal, no selection.
    public byte insert_mode;                // each textfield keeps its own insert mode state.
    public int row_count_per_page;          // page size in number of row. this value MUST be set to >0 for pageup or pagedown in multilines documents.

    // STB_TexteditState - private data
    public byte cursor_at_end_of_line;      // not implemented yet
    public byte initialized;
    public byte has_preferred_x;
    public byte single_line;
    public byte padding1, padding2, padding3;
    public float preferred_x;               // this determines where the cursor up/down tries to seek to along x

    // StbUndoState (fixed-size storage, same sizes as the imstb_textedit.h defaults)
    public StbUndoRecord[] undo_rec = new StbUndoRecord[ImStbTextEdit.IMSTB_TEXTEDIT_UNDOSTATECOUNT];
    public byte[] undo_char = new byte[ImStbTextEdit.IMSTB_TEXTEDIT_UNDOCHARCOUNT];
    public int undo_point;
    public int redo_point;
    public int undo_char_point;
    public int redo_char_point;

    public bool HasSelection() => select_start != select_end;
}

// Internal state of the currently focused/edited text input box
public class ImGuiInputTextState
{
    public ImGuiContext Ctx;
    public ImStbTexteditState Stb;          // State for stb_textedit.h
    public int Flags;                       // ImGuiInputTextFlags: copy of InputText() flags
    public ImGuiID ID;
    public int TextLen;                     // UTF-8 length of the string in TextA (in bytes)
    public byte[] TextSrc;                  // == TextA.Data unless read-only
    public ImVector<byte> TextA = new ImVector<byte>();
    public ImVector<byte> TextToRevertTo = new ImVector<byte>();
    public ImVector<byte> CallbackTextBackup = new ImVector<byte>();
    public int BufCapacity;
    public ImVec2 Scroll;
    public int LineCount;
    public float WrapWidth;
    public float CursorAnim;
    public bool CursorFollow;
    public bool CursorCenterY;
    public bool SelectedAllMouseLock;
    public bool EditedBefore;
    public bool EditedThisFrame;
    public bool WantReloadUserBuf;
    public sbyte LastMoveDirectionLR;
    public int ReloadSelectionStart;
    public int ReloadSelectionEnd;

    public ImGuiInputTextState() { }
    public void Initialize(ImGuiContext ctx)
    {
        Ctx = ctx;
        Stb = new ImStbTexteditState();
        Stb.select_start = Stb.select_end = -1;
        Stb.cursor = 0;
        Scroll = new ImVec2(0.0f, 0.0f);
        ID = 0;
    }

    public void ClearText() { TextLen = 0; if (TextA.Size > 0) TextA[0] = 0; CursorClamp(); }
    public void ClearFreeMemory() { TextA.clear(); TextToRevertTo.clear(); }
    public void OnKeyPressed(int key) => ImGuiUtil.InputTextStateOnKeyPressed(this, key);
    public void OnCharPressed(uint c) => ImGuiUtil.InputTextStateOnCharPressed(this, c);
    public float GetPreferredOffsetX() => ImGuiUtil.InputTextStateGetPreferredOffsetX(this);
    public byte[] GetTextBytes() => TextA.Data ?? Array.Empty<byte>();

    public void CursorAnimReset() { CursorAnim = -0.30f; }
    public void CursorClamp() { Stb.cursor = ImMath.ImClamp(Stb.cursor, 0, TextLen); Stb.select_end = ImMath.ImClamp(Stb.select_end, 0, TextLen); Stb.select_start = ImMath.ImClamp(Stb.select_start, 0, TextLen); }
    public bool HasSelection() => Stb.select_start != Stb.select_end;
    public void ClearSelection() { Stb.select_start = Stb.select_end = Stb.cursor; }
    public int GetCursorPos() => Stb.cursor;
    public int GetSelectionStart() => Stb.select_start;
    public int GetSelectionEnd() => Stb.select_end;
    public void SetSelection(int start, int end) { Stb.select_start = start; Stb.select_end = end; Stb.cursor = Stb.select_end; }
    public void SelectAll() { Stb.select_start = 0; Stb.cursor = Stb.select_end = TextLen; }
    public void ReloadUserBufAndSelectAll() => ImGuiUtil.InputTextStateReloadUserBuf(this, 0);
    public void ReloadUserBufAndKeepSelection() => ImGuiUtil.InputTextStateReloadUserBuf(this, 1);
    public void ReloadUserBufAndMoveToEnd() => ImGuiUtil.InputTextStateReloadUserBuf(this, 2);
}

public struct ImGuiNextWindowData
{
    public int HasFlags;                    // ImGuiNextWindowDataFlags

    public int PosCond;                     // ImGuiCond
    public int SizeCond;                    // ImGuiCond
    public int CollapsedCond;               // ImGuiCond
    public int DockCond;                    // ImGuiCond
    public ImVec2 PosVal;
    public ImVec2 PosPivotVal;
    public ImVec2 SizeVal;
    public ImVec2 ContentSizeVal;
    public ImVec2 ScrollVal;
    public int WindowFlagsSet;              // ImGuiWindowFlags
    public int WindowFlagsClear;            // ImGuiWindowFlags
    public int ChildFlagsSet;               // ImGuiChildFlags
    public int ChildFlagsClear;             // ImGuiChildFlags
    public bool PosUndock;
    public bool CollapsedVal;
    public ImRect SizeConstraintRect;
    public Func<ImGuiSizeCallbackData, float> SizeCallback;
    public object SizeCallbackUserData;
    public float BgAlphaVal;
    public ImGuiID ViewportId;
    public ImGuiID DockId;
    public ImGuiWindowClass WindowClass;
    public ImVec2 MenuBarOffsetMinVal;
    public int RefreshFlagsVal;             // ImGuiWindowRefreshFlags

    public void ClearFlags() { HasFlags = ImGuiNextWindowDataFlags_None; }
}

public struct ImGuiNextItemData
{
    public int HasFlags;                    // ImGuiNextItemDataFlags
    public int ItemFlagsSet;                // ImGuiItemFlags

    public ImGuiID FocusScopeId;
    public long SelectionUserData;          // ImGuiSelectionUserData
    public float Width;
    public ImGuiKeyChord Shortcut;
    public int ShortcutFlags;               // ImGuiInputFlags
    public bool OpenVal;
    public byte OpenCond;                   // ImGuiCond
    public ImGuiDataTypeStorage RefVal;
    public ImGuiID StorageId;
    public ImU32 ColorMarker;

    public void ClearFlags() { HasFlags = ImGuiNextItemDataFlags_None; ItemFlagsSet = ImGuiItemFlags_None; }
}

// Status storage for the last submitted item
public struct ImGuiLastItemData
{
    public ImGuiID ID;
    public int ItemFlags;                   // ImGuiItemFlags
    public int StatusFlags;                 // ImGuiItemStatusFlags
    public ImRect Rect;
    public ImRect NavRect;
    public ImRect DisplayRect;
    public ImRect ClipRect;
    public ImGuiKeyChord Shortcut;
}

// Store data emitted by TreeNode() for usage by TreePop()
public struct ImGuiTreeNodeStackData
{
    public ImGuiID ID;
    public int TreeFlags;                   // ImGuiTreeNodeFlags
    public int ItemFlags;                   // ImGuiItemFlags
    public ImRect NavRect;
    public float DrawLinesX1;
    public float DrawLinesToNodesY2;
    public short DrawLinesTableColumn;      // ImGuiTableColumnIdx
}

public struct ImGuiErrorRecoveryState
{
    public short SizeOfWindowStack;
    public short SizeOfIDStack;
    public short SizeOfTreeStack;
    public short SizeOfColorStack;
    public short SizeOfStyleVarStack;
    public short SizeOfFontStack;
    public short SizeOfFocusScopeStack;
    public short SizeOfGroupStack;
    public short SizeOfItemFlagsStack;
    public short SizeOfBeginPopupStack;
    public short SizeOfDisabledStack;
}

// Data saved for each window pushed into the stack
public struct ImGuiWindowStackData
{
    public ImGuiWindow Window;
    public ImGuiLastItemData ParentLastItemDataBackup;
    public ImGuiErrorRecoveryState StackSizesInBegin;
    public bool DisabledOverrideReenable;
    public float DisabledOverrideReenableAlphaBackup;
}

public struct ImGuiShrinkWidthItem
{
    public int Index;
    public float Width;
    public float InitialWidth;
}

public class ImGuiPtrOrIndex
{
    public object Ptr;                      // Either field can be set, not both.
    public int Index;                       // Usually index in a main pool.

    public ImGuiPtrOrIndex(object ptr) { Ptr = ptr; Index = -1; }
    public ImGuiPtrOrIndex(int index) { Ptr = null; Index = index; }
}

// Data used by IsItemDeactivated()/IsItemDeactivatedAfterEdit() functions
public struct ImGuiDeactivatedItemData
{
    public ImGuiID ID;
    public int ElapseFrame;
    public bool HasBeenEditedBefore;
    public bool IsAlive;
}

// Storage for popup stacks (g.OpenPopupStack and g.BeginPopupStack)
public struct ImGuiPopupData
{
    public ImGuiID PopupId;
    public ImGuiWindow Window;
    public ImGuiWindow RestoreNavWindow;
    public int ParentNavLayer;              // ImGuiNavLayer
    public int OpenFrameCount;
    public ImGuiID OpenParentId;
    public ImVec2 OpenPopupPos;
    public ImVec2 OpenMousePos;
}

public struct ImGuiInputEvent
{
    public int Type;                        // ImGuiInputEventType
    public int Source;                      // ImGuiInputSource
    public ImU32 EventId;

    // Union flattened
    public float MousePos_PosX, MousePos_PosY; public int MousePos_MouseSource;
    public float MouseWheel_WheelX, MouseWheel_WheelY; public int MouseWheel_MouseSource;
    public int MouseButton_Button; public bool MouseButton_Down; public int MouseButton_MouseSource;
    public ImGuiID MouseViewport_HoveredViewportID;
    public ImGuiKey Key_Key; public bool Key_Down; public float Key_AnalogValue;
    public uint Text_Char;
    public bool AppFocused_Focused;

    public bool AddedByTestEngine;
}

public struct ImGuiKeyRoutingData
{
    public short NextEntryIndex;            // ImGuiKeyRoutingIndex
    public ImU16 Mods;
    public ImU16 RoutingCurrScore;
    public ImU16 RoutingNextScore;
    public ImGuiID RoutingCurr;
    public ImGuiID RoutingNext;
}

public class ImGuiKeyRoutingTable
{
    public short[] Index = new short[ImGuiKey_NamedKey_COUNT];
    public ImVector<ImGuiKeyRoutingData> Entries = new ImVector<ImGuiKeyRoutingData>();
    public ImVector<ImGuiKeyRoutingData> EntriesNext = new ImVector<ImGuiKeyRoutingData>();

    public ImGuiKeyRoutingTable() { Clear(); }
    public void Clear()
    {
        for (int n = 0; n < Index.Length; n++) Index[n] = -1;
        Entries.clear();
        EntriesNext.clear();
    }
}

public struct ImGuiKeyOwnerData
{
    public ImGuiID OwnerCurr;
    public ImGuiID OwnerNext;
    public bool LockThisFrame;
    public bool LockUntilRelease;
}

public struct ImGuiListClipperRange
{
    public int Min;
    public int Max;
    public bool PosToIndexConvert;
    public sbyte PosToIndexOffsetMin;
    public sbyte PosToIndexOffsetMax;

    public static ImGuiListClipperRange FromIndices(int min, int max)
    {
        ImGuiListClipperRange r = default;
        r.Min = min; r.Max = max; r.PosToIndexConvert = false; r.PosToIndexOffsetMin = 0; r.PosToIndexOffsetMax = 0;
        return r;
    }

    public static ImGuiListClipperRange FromPositions(float y1, float y2, int off_min, int off_max)
    {
        ImGuiListClipperRange r = default;
        r.Min = (int)y1; r.Max = (int)y2; r.PosToIndexConvert = true; r.PosToIndexOffsetMin = (sbyte)off_min; r.PosToIndexOffsetMax = (sbyte)off_max;
        return r;
    }
}

// Temporary clipper data, buffers shared/reused between instances
public class ImGuiListClipperData
{
    public ImGuiListClipper ListClipper;
    public float LossynessOffset;
    public int StepNo;
    public int ItemsFrozen;
    public ImVector<ImGuiListClipperRange> Ranges = new ImVector<ImGuiListClipperRange>();

    public void Reset(ImGuiListClipper clipper) { ListClipper = clipper; StepNo = ItemsFrozen = 0; Ranges.resize(0); }
}

// Result of a keyboard/gamepad directional navigation move query
public struct ImGuiNavItemData
{
    public ImGuiWindow Window;
    public ImGuiID ID;
    public ImGuiID FocusScopeId;
    public ImRect RectRel;
    public int ItemFlags;                   // ImGuiItemFlags
    public float DistBox;
    public float DistCenter;
    public float DistAxial;
    public long SelectionUserData;          // ImGuiSelectionUserData

    public void Clear()
    {
        Window = null; ID = FocusScopeId = 0; ItemFlags = 0; SelectionUserData = -1;
        DistBox = DistCenter = DistAxial = float.MaxValue;
    }
}

// Storage for PushFocusScope(), g.FocusScopeStack[], g.NavFocusRoute[]
public struct ImGuiFocusScopeData
{
    public ImGuiID ID;
    public ImGuiID WindowID;
}

// Returned by GetTypingSelectRequest()
public struct ImGuiTypingSelectRequest
{
    public int Flags;                       // ImGuiTypingSelectFlags
    public int SearchBufferLen;
    public string SearchBuffer;
    public bool SelectRequest;
    public bool SingleCharMode;
    public sbyte SingleCharSize;
}

// Storage for GetTypingSelectRequest()
public class ImGuiTypingSelectState
{
    public ImGuiTypingSelectRequest Request;
    public string SearchBuffer = "";        // up to 64 chars
    public ImGuiID FocusScope;
    public int LastRequestFrame = 0;
    public float LastRequestTime = 0.0f;
    public bool SingleCharModeLock = false;

    public void Clear() { SearchBuffer = ""; SingleCharModeLock = false; }
}

public struct ImGuiOldColumnData
{
    public float OffsetNorm;
    public float OffsetNormBeforeResize;
    public int Flags;                       // ImGuiOldColumnFlags
    public ImRect ClipRect;
}

public class ImGuiOldColumns
{
    public ImGuiID ID;
    public int Flags;                       // ImGuiOldColumnFlags
    public bool IsFirstFrame;
    public bool IsBeingResized;
    public int Current;
    public int Count;
    public float OffMinX, OffMaxX;
    public float LineMinY, LineMaxY;
    public float HostCursorPosY;
    public float HostCursorMaxPosX;
    public ImRect HostInitialClipRect;
    public ImRect HostBackupClipRect;
    public ImRect HostBackupParentWorkRect;
    public ImVector<ImGuiOldColumnData> Columns = new ImVector<ImGuiOldColumnData>();
    public ImDrawListSplitter Splitter = new ImDrawListSplitter();
}

// Box-select support
public class ImGuiBoxSelectState
{
    // Active box-selection data (persistent, 1 active at a time)
    public ImGuiID ID;
    public bool IsActive;
    public bool IsStarting;
    public bool IsStartedFromVoid;
    public bool IsStartedSetNavIdOnce;
    public bool RequestClear;
    public ushort KeyMods;                  // ImGuiKeyChord (latched, 16 bits)
    public ImVec2 StartPosRel;
    public ImVec2 EndPosRel;
    public ImVec2 ScrollAccum;
    public ImGuiWindow Window;

    // Temporary/Transient data
    public bool UnclipMode;
    public ImRect UnclipRect;
    public ImRect[] UnclipRects = new ImRect[2];
    public ImRect BoxSelectRectPrev;
    public ImRect BoxSelectRectCurr;
}

// Multi-select IO (public struct ImGuiMultiSelectIO from imgui.h)
public class ImGuiMultiSelectIO
{
    // Requests are set and returned by BeginMultiSelect()/EndMultiSelect().
    public ImVector<ImGuiSelectionRequest> Requests = new ImVector<ImGuiSelectionRequest>();
    public long RangeSrcItem;               // ImGuiSelectionUserData
    public long NavIdItem;                  // ImGuiSelectionUserData
    public bool NavIdSelected;
    public bool RangeSrcReset;
    public int ItemsCount;                  // 'int items_count' parameter to BeginMultiSelect() is copied here for convenience
}

public struct ImGuiSelectionRequest
{
    public int Type;                        // ImGuiSelectionRequestType
    public bool Selected;
    public ImS8 RangeDirection;             // Parameter for SetRange request: +1 when RangeFirstItem comes before RangeLastItem, -1 otherwise
    public long RangeFirstItem;             // ImGuiSelectionUserData
    public long RangeLastItem;              // ImGuiSelectionUserData
}

// Temporary storage for multi-select
public class ImGuiMultiSelectTempData
{
    public ImGuiMultiSelectIO IO = new ImGuiMultiSelectIO();
    public ImGuiMultiSelectState Storage;
    public ImGuiID FocusScopeId;
    public int Flags;                       // ImGuiMultiSelectFlags
    public ImVec2 ScopeRectMin;
    public ImVec2 BackupCursorMaxPos;
    public ImGuiID BoxSelectId;
    public ImGuiKeyChord KeyMods;
    public sbyte LoopRequestSetAll;
    public bool IsEndIO;
    public bool IsFocused;
    public bool IsKeyboardSetRange;
    public bool NavIdPassedBy;
    public bool RangeSrcPassedBy;
    public bool RangeDstPassedBy;
    public bool IsSoleOrUnknownSelectionSize;

    public void Clear()
    {
        ClearIO();
        Storage = null; FocusScopeId = 0; Flags = 0; ScopeRectMin = default; BackupCursorMaxPos = default;
        BoxSelectId = 0; KeyMods = 0; LoopRequestSetAll = 0; IsEndIO = false; IsFocused = false;
        IsKeyboardSetRange = false; NavIdPassedBy = false; RangeSrcPassedBy = false; RangeDstPassedBy = false;
        IsSoleOrUnknownSelectionSize = false;
    }

    public void ClearIO()
    {
        IO.Requests.resize(0);
        IO.RangeSrcItem = IO.NavIdItem = -1;
        IO.NavIdSelected = IO.RangeSrcReset = false;
    }
}

// Persistent storage for multi-select (as long as selection is alive)
public class ImGuiMultiSelectState
{
    public ImGuiWindow Window;
    public ImGuiID ID;
    public int LastFrameActive;
    public int LastSelectionSize;
    public sbyte RangeSelected;
    public sbyte NavIdSelected;
    public long RangeSrcItem;
    public long NavIdItem;

    public ImGuiMultiSelectState()
    {
        Window = null; ID = 0; LastFrameActive = LastSelectionSize = 0;
        RangeSelected = NavIdSelected = -1; RangeSrcItem = NavIdItem = -1;
    }
}

// ImGuiViewport Private/Internals fields
public class ImGuiViewportP : ImGuiViewport
{
    public ImGuiWindow Window;                  // Set when the viewport is owned by a window (and ImGuiViewportFlags_CanHostOtherWindows is NOT set)
    public int Idx;
    public int LastFrameActive;                 // Last frame number this viewport was activated by a window
    public int LastFocusedStampCount;           // Last stamp number from when a window hosted by this viewport was focused (by comparing this value between two viewport we have an implicit viewport z-order we use as fallback)
    public ImGuiID LastNameHash;
    public ImVec2 LastPos;
    public ImVec2 LastSize;
    public float Alpha;                         // Window opacity (when dragging dockable windows/viewports we make them transparent)
    public float LastAlpha;
    public bool LastFocusedHadNavWindow;        // Instead of maintaining a LastFocusedWindow (which may harder to correctly maintain), we merely store weither NavWindow != NULL last time the viewport was focused.
    public short PlatformMonitor;               // Index into platform_io.Monitors[] (or -1 if not monitor found)
    public ImDrawList[] BgFgDrawLists = new ImDrawList[2];
    public int[] BgFgDrawListsLastFrameActive = new int[2];
    public float[] BgFgDrawListsLastTimeActive = new float[2];
    public ImDrawData DrawDataP = new ImDrawData();
    public ImDrawDataBuilder DrawDataBuilder = new ImDrawDataBuilder();
    public ImVec2 LastPlatformPos;
    public ImVec2 LastPlatformSize;
    public ImVec2 LastRendererSize;

    public ImVec2 WorkInsetMin;
    public ImVec2 WorkInsetMax;
    public ImVec2 BuildWorkInsetMin;
    public ImVec2 BuildWorkInsetMax;

    public ImGuiViewportP()
    {
        Idx = -1;
        LastFrameActive = LastFocusedStampCount = -1;
        Alpha = LastAlpha = 1.0f;
        PlatformMonitor = -1;
        BgFgDrawListsLastFrameActive[0] = BgFgDrawListsLastFrameActive[1] = -1;
        BgFgDrawListsLastTimeActive[0] = BgFgDrawListsLastTimeActive[1] = -1.0f;
        LastPlatformPos = LastPlatformSize = LastRendererSize = new ImVec2(float.MaxValue, float.MaxValue);
    }

    public void ClearRequestFlags() { PlatformRequestClose = PlatformRequestMove = PlatformRequestResize = false; }

    public ImVec2 CalcWorkRectPos(ImVec2 inset_min) => new ImVec2(Pos.x + inset_min.x, Pos.y + inset_min.y);
    public ImVec2 CalcWorkRectSize(ImVec2 inset_min, ImVec2 inset_max) => new ImVec2(ImMath.ImMax(0.0f, Size.x - inset_min.x - inset_max.x), ImMath.ImMax(0.0f, Size.y - inset_min.y - inset_max.y));
    public void UpdateWorkRect() { WorkPos = CalcWorkRectPos(WorkInsetMin); WorkSize = CalcWorkRectSize(WorkInsetMin, WorkInsetMax); }

    public ImRect GetMainRect() => new ImRect(Pos.x, Pos.y, Pos.x + Size.x, Pos.y + Size.y);
    public ImRect GetWorkRect() => new ImRect(WorkPos.x, WorkPos.y, WorkPos.x + WorkSize.x, WorkPos.y + WorkSize.y);
    public ImRect GetBuildWorkRect() { ImVec2 pos = CalcWorkRectPos(BuildWorkInsetMin); ImVec2 size = CalcWorkRectSize(BuildWorkInsetMin, BuildWorkInsetMax); return new ImRect(pos.x, pos.y, pos.x + size.x, pos.y + size.y); }
}

// Windows data saved in imgui.ini file
public class ImGuiWindowSettings
{
    public ImGuiID ID;
    public ImVec2ih Pos;                    // NB: Settings position are stored RELATIVE to the viewport! Whereas runtime ones are absolute positions.
    public ImVec2ih Size;
    public ImVec2ih ViewportPos;
    public ImGuiID ViewportId;
    public ImGuiID DockId;                  // ID of last known DockNode (even if the DockNode is invisible because it has only 1 active window), or 0 if none.
    public ImGuiID ClassId;                 // ID of window class if specified
    public short DockOrder;                 // Order of the last time the window was visible within its DockNode. This is used to reorder windows that are reappearing on the same frame.
    public ImGuiPackedDate LastUsedDate;
    public bool Collapsed;
    public bool IsChild;
    public bool WantApply;
    public bool WantDelete;
    public string Name = "";

    public ImGuiWindowSettings() { DockOrder = -1; }

    // Port of C++ copy-assignment '*settings = ImGuiWindowSettings()' used when recycling an entry.
    public void Clear()
    {
        ID = 0;
        Pos = default;
        Size = default;
        ViewportPos = default;
        ViewportId = 0;
        DockId = 0;
        ClassId = 0;
        DockOrder = -1;
        LastUsedDate = default;
        Collapsed = IsChild = WantApply = WantDelete = false;
        Name = "";
    }
}

public struct ImGuiSettingsCleanupArgs
{
    public ImGuiID TypeHashFilter;
    public int DiscardOlderThanMonths;
    public bool DiscardWhenMissingDate;
    public bool DiscardAll;
    public bool SetCurrentSessionDateToAll;
    public bool SetCurrentSessionDateWhenMissingDate;
    public int _DiscardOlderThanDate;
}

// Storage for one type registered in the .ini file
public class ImGuiSettingsHandler
{
    public string TypeName;
    public ImGuiID TypeHash;

    public Action<ImGuiContext, ImGuiSettingsHandler> ClearAllFn;
    public Action<ImGuiContext, ImGuiSettingsHandler> ReadInitFn;
    public Func<ImGuiContext, ImGuiSettingsHandler, string, object> ReadOpenFn;
    public Action<ImGuiContext, ImGuiSettingsHandler, object, string> ReadLineFn;
    public Action<ImGuiContext, ImGuiSettingsHandler> ApplyAllFn;
    public Action<ImGuiContext, ImGuiSettingsHandler, ImGuiTextBuffer> WriteAllFn;
    public Action<ImGuiContext, ImGuiSettingsHandler, ImGuiSettingsCleanupArgs> CleanupFn;
    public object UserData;
}

public struct ImGuiLocEntry
{
    public int Key;                         // ImGuiLocKey
    public string Text;
}

public struct ImGuiDebugAllocEntry
{
    public int FrameCount;
    public short AllocCount;
    public short FreeCount;
}

public class ImGuiDebugAllocInfo
{
    public int TotalAllocCount;
    public int TotalFreeCount;
    public short LastEntriesIdx;
    public ImGuiDebugAllocEntry[] LastEntriesBuf = new ImGuiDebugAllocEntry[6];
}

// Storage for ShowMetricsWindow() and DebugNodeXXX() functions
public class ImGuiMetricsConfig
{
    public bool ShowDebugLog = false;
    public bool ShowIDStackTool = false;
    public bool ShowWindowsRects = false;
    public bool ShowWindowsBeginOrder = false;
    public bool ShowTablesRects = false;
    public bool ShowDrawCmdMesh = true;
    public bool ShowDrawCmdBoundingBoxes = true;
    public bool ShowTextEncodingViewer = false;
    public bool ShowTextureUsedRect = false;
    public int ShowWindowsRectsType = -1;
    public int ShowTablesRectsType = -1;
    public int HighlightMonitorIdx = -1;
    public ImGuiID HighlightViewportID = 0;
    public int SettingsDiscardMonths = 6;
    public bool SettingsHighlightOldEntries = false;
    public bool ShowFontPreview = true;
}

public struct ImGuiStackLevelInfo
{
    public ImGuiID ID;
    public sbyte QueryFrameCount;
    public bool QuerySuccess;
    public sbyte DataType;                  // ImGuiDataType
    public int DescOffset;
}

public class ImGuiDebugItemPathQuery
{
    public ImGuiID MainID;
    public bool Active;
    public bool Complete;
    public sbyte Step;
    public ImVector<ImGuiStackLevelInfo> Results = new ImVector<ImGuiStackLevelInfo>();
    public ImGuiTextBuffer ResultsDescBuf = new ImGuiTextBuffer();
    public ImGuiTextBuffer ResultPathBuf = new ImGuiTextBuffer();
}

// State for ID Stack tool queries
public class ImGuiIDStackTool
{
    public bool OptHexEncodeNonAsciiChars;
    public bool OptCopyToClipboardOnCtrlC;
    public int LastActiveFrame;
    public float CopyToClipboardLastTime;

    public ImGuiIDStackTool() { LastActiveFrame = -1; OptHexEncodeNonAsciiChars = true; CopyToClipboardLastTime = -float.MaxValue; }
}

// Generic context hooks
public class ImGuiContextHook
{
    public ImGuiID HookId;
    public int Type;                        // ImGuiContextHookType
    public ImGuiID Owner;
    public Action<ImGuiContext, ImGuiContextHook> Callback;
    public object UserData;
}

// A date in YYYYMMDD format packed into 16-bits (Day:5, Month:4, Year:7 relative to 2000)
public struct ImGuiPackedDate
{
    public ushort Packed;

    public ushort Day { get => (ushort)(Packed & 0x1F); set => Packed = (ushort)((Packed & ~0x1F) | (value & 0x1F)); }
    public ushort Month { get => (ushort)((Packed >> 5) & 0xF); set => Packed = (ushort)((Packed & ~(0xF << 5)) | ((value & 0xF) << 5)); }
    public ushort Year { get => (ushort)((Packed >> 9) & 0x7F); set => Packed = (ushort)((Packed & ~(0x7F << 9)) | ((value & 0x7F) << 9)); }

    public bool IsValid() => Day != 0 && Month != 0;
    public int ToYYYYMMDD() => ((Year + 2000) * 10000) + (Month * 100) + Day;
    // C++: int Unpack() const — same as ToYYYYMMDD() but returns 0 for entirely-invalid dates.
    public int Unpack() => (Year != 0 && Month != 0 && Day != 0) ? ((Year + 2000) * 10000) + (Month * 100) + Day : 0;
    public static ImGuiPackedDate FromYYYYMMDD(int date)
    {
        ImGuiPackedDate d = default;
        d.Day = (ushort)(date % 100);
        d.Month = (ushort)((date / 100) % 100);
        int y = (date / 10000) - 2000;
        d.Year = (ushort)(y < 0 ? 0 : y > 127 ? 127 : y);
        return d;
    }

    // C++: void ImGuiPackedDate::SubtractMonths(int m) — FIXME-OPT: Stupid but enough for what we do with it.
    public void SubtractMonths(int m) { while (m > 0) { Year -= (ushort)(Month == 1 ? 1 : 0); Month = (ushort)(Month == 1 ? 12 : Month - 1); m--; } }
}
