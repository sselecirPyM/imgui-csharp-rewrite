// Port of tab bar and table data structures (imgui_internal.h).
using System;

namespace ImGui;

public static partial class ImGuiConst
{
    public const int IMGUI_TABLE_MAX_COLUMNS = 512;
    public const int IMGUI_WINDOW_HARD_MIN_SIZE = 4;
}

// Storage for one active tab item
public class ImGuiTabItem
{
    public ImGuiID ID;
    public int Flags;                       // ImGuiTabItemFlags
    public ImGuiWindow Window;              // When TabItem is part of a DockNode's TabBar, we hold on to a window.
    public int LastFrameVisible;
    public int LastFrameSelected;
    public float Offset;
    public float Width;
    public float ContentWidth;
    public float RequestedWidth;
    public int NameOffset;                  // ImS32
    public short BeginOrder;
    public short IndexDuringLayout;
    public bool WantClose;

    public ImGuiTabItem()
    {
        LastFrameVisible = LastFrameSelected = -1;
        RequestedWidth = -1.0f;
        NameOffset = -1;
        BeginOrder = IndexDuringLayout = -1;
    }
}

// Storage for a tab bar
public class ImGuiTabBar
{
    public ImGuiWindow Window;
    public ImVector<ImGuiTabItem> Tabs = new ImVector<ImGuiTabItem>();
    public int Flags;                       // ImGuiTabBarFlags
    public ImGuiID ID;
    public ImGuiID SelectedTabId;
    public ImGuiID NextSelectedTabId;
    public ImGuiID NextScrollToTabId;
    public ImGuiID VisibleTabId;
    public int CurrFrameVisible;
    public int PrevFrameVisible;
    public ImRect BarRect = new ImRect();
    public float BarRectPrevWidth;
    public float CurrTabsContentsHeight;
    public float PrevTabsContentsHeight;
    public float WidthAllTabs;
    public float WidthAllTabsIdeal;
    public float ScrollingAnim;
    public float ScrollingTarget;
    public float ScrollingTargetDistToVisibility;
    public float ScrollingSpeed;
    public float ScrollingRectMinX;
    public float ScrollingRectMaxX;
    public float SeparatorMinX;
    public float SeparatorMaxX;
    public ImGuiID ReorderRequestTabId;
    public short ReorderRequestOffset;
    public sbyte BeginCount;
    public bool WantLayout;
    public bool VisibleTabWasSubmitted;
    public bool TabsAddedNew;
    public bool ScrollButtonEnabled;
    public short TabsActiveCount;
    public short LastTabItemIdx;
    public float ItemSpacingY;
    public ImVec2 FramePadding;
    public ImVec2 BackupCursorPos;
    public ImGuiTextBuffer TabsNames = new ImGuiTextBuffer();

    public ImGuiTabBar()
    {
        CurrFrameVisible = PrevFrameVisible = -1;
        LastTabItemIdx = -1;
    }
}

// Storage for one column of a table
public class ImGuiTableColumn
{
    public int Flags;                       // ImGuiTableColumnFlags
    public float WidthGiven;
    public float MinX;
    public float MaxX;
    public float WidthRequest;
    public float WidthAuto;
    public float WidthMax;
    public float StretchWeight;
    public float InitStretchWeightOrWidth;
    public ImRect ClipRect = new ImRect();
    public ImGuiID ID;
    public ImGuiID UserData;
    public float WorkMinX;
    public float WorkMaxX;
    public float ItemWidth;
    public float ContentMaxXFrozen;
    public float ContentMaxXUnfrozen;
    public float ContentMaxXHeadersUsed;
    public float ContentMaxXHeadersIdeal;
    public short NameOffset;
    public short DisplayOrder;              // ImGuiTableColumnIdx
    public short IndexWithinEnabledSet;     // ImGuiTableColumnIdx
    public short PrevEnabledColumn;         // ImGuiTableColumnIdx
    public short NextEnabledColumn;         // ImGuiTableColumnIdx
    public short SortOrder;                 // ImGuiTableColumnIdx
    public byte DrawChannelCurrent;         // ImGuiTableDrawChannelIdx
    public byte DrawChannelFrozen;          // ImGuiTableDrawChannelIdx
    public byte DrawChannelUnfrozen;        // ImGuiTableDrawChannelIdx
    public bool IsEnabled;
    public bool IsUserEnabled;
    public bool IsUserEnabledNextFrame;
    public bool IsVisibleX;
    public bool IsVisibleY;
    public bool IsRequestOutput;
    public bool IsSkipItems;
    public bool IsPreserveWidthAuto;
    public bool IsJustCreated;
    public bool IsLoadedSettings;
    public bool IsNeedReconcileSrc;
    public bool IsNeedReconcileDst;
    public sbyte NavLayerCurrent;
    public byte AutoFitQueue;               // 4 bits
    public byte CannotSkipItemsQueue;       // 4 bits
    public byte SortDirection;              // 2 bits
    public byte SortDirectionsAvailCount;   // 2 bits
    public byte SortDirectionsAvailMask;    // 4 bits
    public byte SortDirectionsAvailList;

    public ImGuiTableColumn()
    {
        StretchWeight = WidthRequest = -1.0f;
        NameOffset = -1;
        DisplayOrder = IndexWithinEnabledSet = -1;
        PrevEnabledColumn = NextEnabledColumn = -1;
        SortOrder = -1;
        SortDirection = ImGuiSortDirection_None;
        IsJustCreated = true;
        DrawChannelCurrent = DrawChannelFrozen = DrawChannelUnfrozen = 0xFF;
    }
}

// Passed to TableSetupColumn() + reconcile data
public class ImGuiTableReconcileColumnData
{
    public ImGuiID ID;
    public short NameOffset;
    public int Flags;                       // ImGuiTableColumnFlags
    public float InitWidthOrWeight;
    public ImGuiID UserData;

    public short ColumnNewIdx;
    public short ColumnOldIdx;
    public ImGuiTableColumn ColumnOldData = new ImGuiTableColumn();
}

// Transient cell data stored per row.
public struct ImGuiTableCellData
{
    public ImU32 BgColor;
    public short Column;                    // ImGuiTableColumnIdx
}

// Parameters for TableAngledHeadersRowEx()
public struct ImGuiTableHeaderData
{
    public short Index;                     // ImGuiTableColumnIdx
    public ImU32 TextColor;
    public ImU32 BgColor0;
    public ImU32 BgColor1;
}

// Per-instance data that needs preserving across frames
public class ImGuiTableInstanceData
{
    public ImGuiID TableInstanceID;
    public float LastOuterHeight;
    public float LastTopHeadersRowHeight;
    public float LastFrozenHeight;
    public int HoveredRowLast;
    public int HoveredRowNext;

    public ImGuiTableInstanceData()
    {
        TableInstanceID = 0;
        LastOuterHeight = LastTopHeadersRowHeight = LastFrozenHeight = 0.0f;
        HoveredRowLast = HoveredRowNext = -1;
    }
}

// Public sort specs (imgui.h)
public class ImGuiTableColumnSortSpecs
{
    public ImGuiID ColumnUserID;
    public short ColumnIndex;               // ImGuiTableColumnIdx
    public short SortOrder;                 // ImGuiTableColumnIdx
    public byte SortDirection;              // ImGuiSortDirection
}

public class ImGuiTableSortSpecs
{
    public ImGuiTableColumnSortSpecs[] Specs;
    public int SpecsCount;
    public bool SpecsDirty;
}

// Storage for one table
public class ImGuiTable
{
    public ImGuiID ID;
    public int Flags;                       // ImGuiTableFlags
    public byte[] RawData;                  // Single allocation to hold Columns[], DisplayOrderToIndex[], and RowCellData[]
    public ImGuiTableTempData TempData;
    public ImGuiTableColumn[] Columns = Array.Empty<ImGuiTableColumn>();
    public short[] DisplayOrderToIndex = Array.Empty<short>();
    public ImGuiTableCellData[] RowCellData = Array.Empty<ImGuiTableCellData>();
    public bool[] EnabledMaskByDisplayOrder;    // Column DisplayOrder -> IsEnabled map
    public bool[] EnabledMaskByIndex;           // Column Index -> IsEnabled map
    public bool[] VisibleMaskByIndex;           // Column Index -> IsVisibleX|IsVisibleY map
    public int SettingsLoadedFlags;         // ImGuiTableFlags
    public int SettingsOffset;
    public int LastFrameActive;
    public int ColumnsCount;
    public int CurrentRow;
    public int CurrentColumn;
    public short InstanceCurrent;
    public short InstanceInteracted;
    public float RowPosY1;
    public float RowPosY2;
    public float RowMinHeight;
    public float RowCellPaddingY;
    public float RowTextBaseline;
    public float RowIndentOffsetX;
    public int RowFlags;                    // ImGuiTableRowFlags
    public int LastRowFlags;                // ImGuiTableRowFlags
    public int RowBgColorCounter;
    public ImU32[] RowBgColor = new ImU32[2];
    public ImU32 BorderColorStrong;
    public ImU32 BorderColorLight;
    public float BorderX1;
    public float BorderX2;
    public float HostIndentX;
    public float MinColumnWidth;
    public float OuterPaddingX;
    public float CellPaddingX;
    public float CellSpacingX1;
    public float CellSpacingX2;
    public float InnerWidth;
    public float ColumnsGivenWidth;
    public float ColumnsAutoFitWidth;
    public float ColumnsStretchSumWeights;
    public float ResizedColumnNextWidth;
    public float ResizeLockMinContentsX2;
    public float RefScale;
    public float AngledHeadersHeight;
    public float AngledHeadersSlope;
    public ImRect OuterRect = new ImRect();
    public ImRect InnerRect = new ImRect();
    public ImRect WorkRect = new ImRect();
    public ImRect InnerClipRect = new ImRect();
    public ImRect BgClipRect = new ImRect();
    public ImRect Bg0ClipRectForDrawCmd = new ImRect();
    public ImRect Bg2ClipRectForDrawCmd = new ImRect();
    public ImRect HostClipRect = new ImRect();
    public ImRect HostBackupInnerClipRect = new ImRect();
    public ImGuiWindow OuterWindow;
    public ImGuiWindow InnerWindow;
    public ImGuiTextBuffer ColumnsNames = new ImGuiTextBuffer();
    public ImDrawListSplitter DrawSplitter;
    public ImGuiTableInstanceData InstanceDataFirst = new ImGuiTableInstanceData();
    public ImVector<ImGuiTableInstanceData> InstanceDataExtra = new ImVector<ImGuiTableInstanceData>();
    public ImGuiTableColumnSortSpecs SortSpecsSingle = new ImGuiTableColumnSortSpecs();
    public ImVector<ImGuiTableColumnSortSpecs> SortSpecsMulti = new ImVector<ImGuiTableColumnSortSpecs>();
    public ImGuiTableSortSpecs SortSpecs = new ImGuiTableSortSpecs();
    public short SortSpecsCount;                // ImGuiTableColumnIdx
    public short ColumnsEnabledCount;           // ImGuiTableColumnIdx
    public short ColumnsEnabledFixedCount;      // ImGuiTableColumnIdx
    public short DeclColumnsCount;              // ImGuiTableColumnIdx
    public short AngledHeadersCount;            // ImGuiTableColumnIdx
    public short HoveredColumnBody;             // ImGuiTableColumnIdx
    public short HoveredColumnBorder;           // ImGuiTableColumnIdx
    public short HighlightColumnHeader;         // ImGuiTableColumnIdx
    public short AutoFitSingleColumn;           // ImGuiTableColumnIdx
    public short ResizedColumn;                 // ImGuiTableColumnIdx
    public short LastResizedColumn;             // ImGuiTableColumnIdx
    public short HeldHeaderColumn;              // ImGuiTableColumnIdx
    public short LastHeldHeaderColumn;          // ImGuiTableColumnIdx
    public short ReorderColumn;                 // ImGuiTableColumnIdx
    public short ReorderColumnDstOrder;         // ImGuiTableColumnIdx
    public short LeftMostEnabledColumn;         // ImGuiTableColumnIdx
    public short RightMostEnabledColumn;        // ImGuiTableColumnIdx
    public short LeftMostStretchedColumn;       // ImGuiTableColumnIdx
    public short RightMostStretchedColumn;      // ImGuiTableColumnIdx
    public short ContextPopupColumn;            // ImGuiTableColumnIdx
    public short FreezeRowsRequest;             // ImGuiTableColumnIdx
    public short FreezeRowsCount;               // ImGuiTableColumnIdx
    public short FreezeColumnsRequest;          // ImGuiTableColumnIdx
    public short FreezeColumnsCount;            // ImGuiTableColumnIdx
    public short RowCellDataCurrent;            // ImGuiTableColumnIdx
    public byte DummyDrawChannel;               // ImGuiTableDrawChannelIdx
    public byte Bg2DrawChannelCurrent;          // ImGuiTableDrawChannelIdx
    public byte Bg2DrawChannelUnfrozen;         // ImGuiTableDrawChannelIdx
    public sbyte NavLayer;
    public bool IsLayoutLocked;
    public bool IsInsideRow;
    public bool IsInitializing;
    public bool IsReconcileMode;
    public bool IsSortSpecsDirty;
    public bool IsUsingHeaders;
    public bool IsContextPopupOpen;
    public bool DisableDefaultContextMenu;
    public bool IsSettingsRequestLoad;
    public bool IsSettingsDirty;
    public bool IsDefaultDisplayOrder;
    public bool IsDefaultVisibility;
    public bool IsResetAllRequest;
    public bool IsResetDisplayOrderRequest;
    public bool IsResetVisibilityRequest;
    public bool IsUnfrozenRows;
    public bool IsDefaultSizingPolicy;
    public bool IsActiveIdAliveBeforeTable;
    public bool IsActiveIdInTable;
    public bool HasScrollbarYCurr;
    public bool HasScrollbarYPrev;
    public bool MemoryCompacted;
    public bool HostSkipItems;

    public ImGuiTable()
    {
        LastFrameActive = -1;
        SortSpecsCount = ColumnsEnabledCount = ColumnsEnabledFixedCount = DeclColumnsCount = AngledHeadersCount = 0;
        HoveredColumnBody = HoveredColumnBorder = HighlightColumnHeader = AutoFitSingleColumn = -1;
        ResizedColumn = LastResizedColumn = HeldHeaderColumn = LastHeldHeaderColumn = -1;
        ReorderColumn = -1; ReorderColumnDstOrder = -1;
        LeftMostEnabledColumn = RightMostEnabledColumn = -1;
        LeftMostStretchedColumn = RightMostStretchedColumn = -1;
        ContextPopupColumn = -1;
        FreezeRowsRequest = FreezeRowsCount = FreezeColumnsRequest = FreezeColumnsCount = -1;
        RowCellDataCurrent = -1;
        DrawSplitter = null;
    }
}

// Transient data that are only needed between BeginTable() and EndTable()
public class ImGuiTableTempData
{
    public ImGuiID WindowID;
    public int TableIndex;
    public float LastTimeActive;
    public float AngledHeadersExtraWidth;
    public ImVector<ImGuiTableHeaderData> AngledHeadersRequests = new ImVector<ImGuiTableHeaderData>();

    public ImVector<ImGuiTableReconcileColumnData> ReconcileColumnsRequests = new ImVector<ImGuiTableReconcileColumnData>();
    public byte[] OldColumnsRawData;
    public ImGuiTableColumn[] OldColumnsData = Array.Empty<ImGuiTableColumn>();

    public ImVec2 UserOuterSize;
    public ImDrawListSplitter DrawSplitter = new ImDrawListSplitter();

    public ImRect HostBackupWorkRect = new ImRect();
    public ImRect HostBackupParentWorkRect = new ImRect();
    public ImVec2 HostBackupPrevLineSize;
    public ImVec2 HostBackupCurrLineSize;
    public ImVec2 HostBackupCursorMaxPos;
    public ImVec1 HostBackupColumnsOffset;
    public float HostBackupItemWidth;
    public int HostBackupItemWidthStackSize;

    public ImGuiTableTempData() { LastTimeActive = -1.0f; }
}

// Column settings for .ini (1 per column, stored in ImChunkStream)
public class ImGuiTableColumnSettings
{
    public float WidthOrWeight;
    public ImGuiID ID;
    public short Index;                     // ImGuiTableColumnIdx
    public short DisplayOrder;              // ImGuiTableColumnIdx
    public short SortOrder;                 // ImGuiTableColumnIdx
    public byte SortDirection;              // 2 bits
    public sbyte IsEnabled;                 // 2 bits ("Visible" in .ini file)
    public bool IsStretch;
    public bool IsLoaded;

    public ImGuiTableColumnSettings()
    {
        WidthOrWeight = 0.0f;
        ID = 0;
        Index = -1;
        DisplayOrder = SortOrder = -1;
        SortDirection = ImGuiSortDirection_None;
        IsEnabled = -1;
        IsStretch = false;
        IsLoaded = false;
    }
}

// Table settings for .ini
public class ImGuiTableSettings
{
    public ImGuiID ID;
    public int SaveFlags;                   // ImGuiTableFlags
    public float RefScale;
    public short ColumnsCount;              // ImGuiTableColumnIdx
    public short ColumnsCountMax;           // ImGuiTableColumnIdx
    public ImGuiPackedDate LastUsedDate;
    public bool WantApply;
    public ImGuiTableColumnSettings[] GetColumnSettings() => _columns;
    public ImGuiTableColumnSettings[] _columns = Array.Empty<ImGuiTableColumnSettings>();
}
