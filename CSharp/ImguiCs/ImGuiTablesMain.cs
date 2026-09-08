// Port of imgui_tables.cpp lines 250-2615:
//   [SECTION] Tables: Main code (TableFixFlags, TableFindByID, BeginTable/BeginTableEx, TableBeginInitMemory,
//     TableApplyQueuedRequests, TableSetColumnDisplayOrder, TableSetupColumnFlags, TableUpdateLayout,
//     TableUpdateBorders, EndTable, TableInitColumnDefaults, TableSetupColumn, TableReconcileColumns,
//     TableSetupScrollFreeze)
//   [SECTION] Tables: Simple accessors
//   [SECTION] Tables: Row changes (incl. TableBeginRow/TableEndRow)
//   [SECTION] Tables: Columns changes (TableGetColumnIndex/TableSetColumnIndex/TableNextColumn/TableBeginCell/TableEndCell)
//   [SECTION] Tables: Columns width management
// C# adaptation notes:
//   - C++ packs Columns[]+DisplayOrderToIndex[]+RowCellData[]+bitmasks into table->RawData (single allocation);
//     this port keeps managed arrays (see ImGuiTable) and only uses RawData as a non-null "allocated" marker.
//   - EnabledMaskByIndex/EnabledMaskByDisplayOrder/VisibleMaskByIndex replace C++ ImBitArray (ImU32*) with bool[].
//   - ImGuiTableColumn is a class: pointer-style mutation works directly; C++ struct assignment is done via TableColumnAssign().
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    // Configuration
    public const int TABLE_DRAW_CHANNEL_BG0 = 0;
    public const int TABLE_DRAW_CHANNEL_BG2_FROZEN = 1;
    public const int TABLE_DRAW_CHANNEL_NOCLIP = 2;                     // When using ImGuiTableFlags_NoClip (this becomes the last visible channel)
    public const float TABLE_BORDER_SIZE = 1.0f;                        // FIXME-TABLE: Currently hard-coded because of clipping assumptions with outer borders rendering.
    public const float TABLE_RESIZE_SEPARATOR_HALF_THICKNESS = 4.0f;    // Extend outside inner borders.
    public const float TABLE_RESIZE_SEPARATOR_FEEDBACK_TIMER = 0.06f;   // Delay/timer before making the hover feedback (color+cursor) visible because tables/columns tends to be more cramped.

    // C++ struct assignment for ImGuiTableColumn (class in C#): copy every field, in declaration order.
    static void TableColumnAssign(ImGuiTableColumn dst, ImGuiTableColumn src)
    {
        dst.Flags = src.Flags;
        dst.WidthGiven = src.WidthGiven;
        dst.MinX = src.MinX;
        dst.MaxX = src.MaxX;
        dst.WidthRequest = src.WidthRequest;
        dst.WidthAuto = src.WidthAuto;
        dst.WidthMax = src.WidthMax;
        dst.StretchWeight = src.StretchWeight;
        dst.InitStretchWeightOrWidth = src.InitStretchWeightOrWidth;
        dst.ClipRect = src.ClipRect;
        dst.ID = src.ID;
        dst.UserData = src.UserData;
        dst.WorkMinX = src.WorkMinX;
        dst.WorkMaxX = src.WorkMaxX;
        dst.ItemWidth = src.ItemWidth;
        dst.ContentMaxXFrozen = src.ContentMaxXFrozen;
        dst.ContentMaxXUnfrozen = src.ContentMaxXUnfrozen;
        dst.ContentMaxXHeadersUsed = src.ContentMaxXHeadersUsed;
        dst.ContentMaxXHeadersIdeal = src.ContentMaxXHeadersIdeal;
        dst.NameOffset = src.NameOffset;
        dst.DisplayOrder = src.DisplayOrder;
        dst.IndexWithinEnabledSet = src.IndexWithinEnabledSet;
        dst.PrevEnabledColumn = src.PrevEnabledColumn;
        dst.NextEnabledColumn = src.NextEnabledColumn;
        dst.SortOrder = src.SortOrder;
        dst.DrawChannelCurrent = src.DrawChannelCurrent;
        dst.DrawChannelFrozen = src.DrawChannelFrozen;
        dst.DrawChannelUnfrozen = src.DrawChannelUnfrozen;
        dst.IsEnabled = src.IsEnabled;
        dst.IsUserEnabled = src.IsUserEnabled;
        dst.IsUserEnabledNextFrame = src.IsUserEnabledNextFrame;
        dst.IsVisibleX = src.IsVisibleX;
        dst.IsVisibleY = src.IsVisibleY;
        dst.IsRequestOutput = src.IsRequestOutput;
        dst.IsSkipItems = src.IsSkipItems;
        dst.IsPreserveWidthAuto = src.IsPreserveWidthAuto;
        dst.IsJustCreated = src.IsJustCreated;
        dst.IsLoadedSettings = src.IsLoadedSettings;
        dst.IsNeedReconcileSrc = src.IsNeedReconcileSrc;
        dst.IsNeedReconcileDst = src.IsNeedReconcileDst;
        dst.NavLayerCurrent = src.NavLayerCurrent;
        dst.AutoFitQueue = src.AutoFitQueue;
        dst.CannotSkipItemsQueue = src.CannotSkipItemsQueue;
        dst.SortDirection = src.SortDirection;
        dst.SortDirectionsAvailCount = src.SortDirectionsAvailCount;
        dst.SortDirectionsAvailMask = src.SortDirectionsAvailMask;
        dst.SortDirectionsAvailList = src.SortDirectionsAvailList;
    }

    // Helper
    // C++: inline ImGuiTableFlags TableFixFlags(ImGuiTableFlags flags, ImGuiWindow* outer_window)   [imgui_tables.cpp]
    public static int TableFixFlags(int flags, ImGuiWindow outer_window)
    {
        // Adjust flags: set default sizing policy
        if ((flags & ImGuiTableFlags_SizingMask_) == 0)
            flags |= ((flags & ImGuiTableFlags_ScrollX) != 0 || (outer_window.Flags & ImGuiWindowFlags_AlwaysAutoResize) != 0) ? ImGuiTableFlags_SizingFixedFit : ImGuiTableFlags_SizingStretchSame;

        // Adjust flags: enable NoKeepColumnsVisible when using ImGuiTableFlags_SizingFixedSame
        if ((flags & ImGuiTableFlags_SizingMask_) == ImGuiTableFlags_SizingFixedSame)
            flags |= ImGuiTableFlags_NoKeepColumnsVisible;

        // Adjust flags: enforce borders when resizable
        if ((flags & ImGuiTableFlags_Resizable) != 0)
            flags |= ImGuiTableFlags_BordersInnerV;

        // Adjust flags: disable NoHostExtendX/NoHostExtendY if we have any scrolling going on
        if ((flags & (ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY)) != 0)
            flags &= ~(ImGuiTableFlags_NoHostExtendX | ImGuiTableFlags_NoHostExtendY);

        // Adjust flags: NoBordersInBodyUntilResize takes priority over NoBordersInBody
        if ((flags & ImGuiTableFlags_NoBordersInBodyUntilResize) != 0)
            flags &= ~ImGuiTableFlags_NoBordersInBody;

        // Adjust flags: disable saved settings if there's nothing to save
        if ((flags & (ImGuiTableFlags_Resizable | ImGuiTableFlags_Hideable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Sortable)) == 0)
            flags |= ImGuiTableFlags_NoSavedSettings;

        // Inherit _NoSavedSettings from top-level window (child windows always have _NoSavedSettings set)
        if ((outer_window.RootWindow.Flags & ImGuiWindowFlags_NoSavedSettings) != 0)
            flags |= ImGuiTableFlags_NoSavedSettings;

        return flags;
    }

    // imgui_internal.h inline: ImGuiTableInstanceData* ImGui::TableGetInstanceData(ImGuiTable* table, int instance_no)
    public static ImGuiTableInstanceData TableGetInstanceData(ImGuiTable table, int instance_no)
    {
        if (instance_no == 0)
            return table.InstanceDataFirst;
        return table.InstanceDataExtra[instance_no - 1];
    }

    // imgui_internal.h inline: ImGuiID ImGui::TableGetInstanceID(ImGuiTable* table, int instance_no)
    public static ImGuiID TableGetInstanceID(ImGuiTable table, int instance_no)
    {
        return TableGetInstanceData(table, instance_no).TableInstanceID;
    }

    // C++: ImGuiTable* ImGui::TableFindByID(ImGuiID id)
    public static ImGuiTable TableFindByID(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        return g.Tables.GetByKey(id);
    }

    // Read about "TABLE SIZING" at the top of imgui_tables.cpp.
    // C++: bool ImGui::BeginTable(const char* str_id, int columns_count, ImGuiTableFlags flags, const ImVec2& outer_size, float inner_width)
    public static bool BeginTable(string str_id, int columns_count, ImGuiTableFlags flags = 0, ImVec2 outer_size = default, float inner_width = 0.0f)
    {
        ImGuiID id = GetID(str_id);
        return BeginTableEx(str_id, id, columns_count, flags, outer_size, inner_width);
    }

    // C++: bool ImGui::BeginTableEx(const char* name, ImGuiID id, int columns_count, ImGuiTableFlags flags, const ImVec2& outer_size, float inner_width)
    public static bool BeginTableEx(string name, ImGuiID id, int columns_count, ImGuiTableFlags flags, ImVec2 outer_size, float inner_width)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow outer_window = GetCurrentWindow();
        if (outer_window.SkipItems) // Consistent with other tables + beneficial side effect that assert on miscalling EndTable() will be more visible.
            return false;

        // Sanity checks
        Debug.Assert(columns_count > 0 && columns_count < IMGUI_TABLE_MAX_COLUMNS);
        if ((flags & ImGuiTableFlags_ScrollX) != 0)
            Debug.Assert(inner_width >= 0.0f);

        // If an outer size is specified ahead we will be able to early out when not visible. Exact clipping criteria may evolve.
        // FIXME: coarse clipping because access to table data causes two issues:
        // - instance numbers varying/unstable. may not be a direct problem for users, but could make outside access broken or confusing, e.g. TestEngine.
        // - can't implement support for ImGuiChildFlags_ResizeY as we need to somehow pull the height data from somewhere. this also needs stable instance numbers.
        // The side-effects of accessing table data on coarse clip would be:
        // - always reserving the pooled ImGuiTable data ahead for a fully clipped table (minor IMHO). Also the 'outer_window_is_measuring_size' criteria may already be defeating this in some situations.
        // - always performing the GetOrAddByKey() O(log N) query in g.Tables.Map[].
        bool use_child_window = (flags & (ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY)) != 0;
        ImVec2 avail_size = GetContentRegionAvail();
        ImVec2 actual_outer_size = ImMath.ImTrunc(CalcItemSize(outer_size, ImMath.ImMax(avail_size.x, IMGUI_WINDOW_HARD_MIN_SIZE), use_child_window ? ImMath.ImMax(avail_size.y, IMGUI_WINDOW_HARD_MIN_SIZE) : 0.0f));
        ImRect outer_rect = new ImRect(outer_window.DC.CursorPos, outer_window.DC.CursorPos + actual_outer_size);
        bool outer_window_is_measuring_size = (outer_window.AutoFitFramesX > 0) || (outer_window.AutoFitFramesY > 0); // Doesn't apply to AlwaysAutoResize windows!
        if (use_child_window && IsClippedEx(outer_rect, 0) && !outer_window_is_measuring_size)
        {
            ItemSize(outer_rect.GetSize());
            ItemAdd(outer_rect, id);
            g.NextWindowData.ClearFlags();
            return false;
        }

        // [DEBUG] Debug break requested by user
        if (g.DebugBreakInTable == id)
            Debugger.Break(); // IM_DEBUG_BREAK()

        // Acquire storage for the table
        ImGuiTable table = g.Tables.GetOrAddByKey(id);

        // Acquire temporary buffers
        int table_idx = g.Tables.GetIndex(table);
        if (++g.TablesTempDataStacked > g.TablesTempData.Size)
        {
            int old_size = g.TablesTempData.Size;
            g.TablesTempData.resize(g.TablesTempDataStacked);
            for (int i = old_size; i < g.TablesTempDataStacked; i++)
                g.TablesTempData.Data[i] = new ImGuiTableTempData(); // C++: resize(n, ImGuiTableTempData())
        }
        ImGuiTableTempData temp_data = table.TempData = g.TablesTempData[g.TablesTempDataStacked - 1];
        temp_data.TableIndex = table_idx;
        temp_data.ReconcileColumnsRequests.resize(0); // FIXME: Use shrink(0) everywhere?
        table.DrawSplitter = table.TempData.DrawSplitter;
        table.DrawSplitter.Clear();

        // Fix flags
        table.IsDefaultSizingPolicy = (flags & ImGuiTableFlags_SizingMask_) == 0;
        flags = TableFixFlags(flags, outer_window);

        // Initialize
        int previous_frame_active = table.LastFrameActive;
        int instance_no = (previous_frame_active != g.FrameCount) ? 0 : table.InstanceCurrent + 1;
        int previous_flags = table.Flags;
        bool is_new_table = (previous_frame_active == -1);
        table.ID = id;
        table.Flags = flags;
        table.LastFrameActive = g.FrameCount;
        table.OuterWindow = table.InnerWindow = outer_window;
        table.ColumnsCount = columns_count;
        table.IsLayoutLocked = false;
        table.InnerWidth = inner_width;
        table.NavLayer = (sbyte)outer_window.DC.NavLayerCurrent;
        temp_data.UserOuterSize = outer_size;

        // Instance data (for instance 0, TableID == TableInstanceID)
        ImGuiID instance_id;
        table.InstanceCurrent = (short)instance_no;
        if (instance_no > 0)
        {
            Debug.Assert(table.ColumnsCount == columns_count, "BeginTable(): Cannot change columns count mid-frame while preserving same ID");
            if (table.InstanceDataExtra.Size < instance_no)
                table.InstanceDataExtra.push_back(new ImGuiTableInstanceData());
            instance_id = GetIDWithSeed(instance_no, GetIDWithSeed("##Instances", null, id)); // Push "##Instances" followed by (int)instance_no in ID stack.
        }
        else
        {
            instance_id = id;
        }
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        table_instance.TableInstanceID = instance_id;

        // When not using a child window, WorkRect.Max will grow as we append contents.
        if (use_child_window)
        {
            // Ensure no vertical scrollbar appears if we only want horizontal one, to make flag consistent
            // (we have no other way to disable vertical scrollbar of a window while keeping the horizontal one showing)
            ImVec2 override_content_size = new ImVec2(float.MaxValue, float.MaxValue);
            if ((flags & ImGuiTableFlags_ScrollX) != 0 && (flags & ImGuiTableFlags_ScrollY) == 0)
                override_content_size.y = 1.17549435e-38f; // FLT_MIN

            // Ensure specified width (when not specified, Stretched columns will act as if the width == OuterWidth and
            // never lead to any scrolling). We don't handle inner_width < 0.0f, we could potentially use it to right-align
            // based on the right side of the child window work rect, which would require knowing ahead if we are going to
            // have decoration taking horizontal spaces (typically a vertical scrollbar).
            if ((flags & ImGuiTableFlags_ScrollX) != 0 && inner_width > 0.0f)
                override_content_size.x = inner_width;

            if (override_content_size.x != float.MaxValue || override_content_size.y != float.MaxValue)
                SetNextWindowContentSize(new ImVec2(override_content_size.x != float.MaxValue ? override_content_size.x : 0.0f, override_content_size.y != float.MaxValue ? override_content_size.y : 0.0f));

            // Reset scroll if we are reactivating it
            if ((previous_flags & (ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY)) == 0)
                if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasScroll) == 0)
                    SetNextWindowScroll(new ImVec2(0.0f, 0.0f));

            // Create scrolling region (without border and zero window padding)
            ImGuiWindowFlags child_window_flags = (flags & ImGuiTableFlags_ScrollX) != 0 ? ImGuiWindowFlags_HorizontalScrollbar : ImGuiWindowFlags_None;
            BeginChildEx(name, instance_id, outer_rect.GetSize(), ImGuiChildFlags_None, child_window_flags);
            table.InnerWindow = g.CurrentWindow;
            table.WorkRect = table.InnerWindow.WorkRect;
            table.OuterRect = table.InnerWindow.Rect();
            table.InnerRect = table.InnerWindow.InnerRect;
            Debug.Assert(table.InnerWindow.WindowPadding.x == 0.0f && table.InnerWindow.WindowPadding.y == 0.0f && table.InnerWindow.WindowBorderSize == 0.0f);

            // Allow submitting when host is measuring
            if (table.InnerWindow.SkipItems && outer_window_is_measuring_size)
                table.InnerWindow.SkipItems = false;

            // When using multiple instances, ensure they have the same amount of horizontal decorations (aka vertical scrollbar) so stretched columns can be aligned
            if (instance_no == 0)
            {
                table.HasScrollbarYPrev = table.HasScrollbarYCurr;
                table.HasScrollbarYCurr = false;
            }
            table.HasScrollbarYCurr |= table.InnerWindow.ScrollbarY;
        }
        else
        {
            // For non-scrolling tables, WorkRect == OuterRect == InnerRect.
            // But at this point we do NOT have a correct value for .Max.y (unless a height has been explicitly passed in). It will only be updated in EndTable().
            table.WorkRect = table.OuterRect = table.InnerRect = outer_rect;
            table.HasScrollbarYPrev = table.HasScrollbarYCurr = false;
            table.InnerWindow.DC.TreeDepth++; // This is designed to always linking ImGuiTreeNodeFlags_DrawLines linking across a table
        }

        // Push a standardized ID for both child-using and not-child-using tables
        PushOverrideID(id);
        if (instance_no > 0)
            PushOverrideID(instance_id); // FIXME: Somehow this is not resolved by stack-tool, even tho GetIDWithSeed() submitted the symbol.

        // Backup a copy of host window members we will modify
        ImGuiWindow inner_window = table.InnerWindow;
        table.HostIndentX = inner_window.DC.Indent.x;
        table.HostClipRect = inner_window.ClipRect;
        table.HostSkipItems = inner_window.SkipItems;
        temp_data.WindowID = inner_window.ID;
        temp_data.HostBackupWorkRect = inner_window.WorkRect;
        temp_data.HostBackupParentWorkRect = inner_window.ParentWorkRect;
        temp_data.HostBackupColumnsOffset = outer_window.DC.ColumnsOffset;
        temp_data.HostBackupPrevLineSize = inner_window.DC.PrevLineSize;
        temp_data.HostBackupCurrLineSize = inner_window.DC.CurrLineSize;
        temp_data.HostBackupCursorMaxPos = inner_window.DC.CursorMaxPos;
        temp_data.HostBackupItemWidth = outer_window.DC.ItemWidth;
        temp_data.HostBackupItemWidthStackSize = outer_window.DC.ItemWidthStack.Size;
        inner_window.DC.PrevLineSize = inner_window.DC.CurrLineSize = new ImVec2(0.0f, 0.0f);

        // Make borders not overlap our contents by offsetting HostClipRect (#6765, #7428, #3752)
        // (we normally shouldn't alter HostClipRect as we rely on TableMergeDrawChannels() expanding non-clipped column toward the
        // limits of that rectangle, in order for ImDrawListSplitter::Merge() to merge the draw commands. However since the overlap
        // problem only affect scrolling tables in this case we can get away with doing it without extra cost).
        if (inner_window != outer_window)
        {
            // FIXME: Because inner_window's Scrollbar doesn't know about border size, since it's not encoded in window->WindowBorderSize,
            // it already overlaps it and doesn't need an extra offset. Ideally we should be able to pass custom border size with
            // different x/y values to BeginChild().
            float border_size = TABLE_BORDER_SIZE;
            if ((flags & ImGuiTableFlags_BordersOuterV) != 0)
            {
                table.HostClipRect.Min.x = ImMath.ImMin(table.HostClipRect.Min.x + border_size, table.HostClipRect.Max.x);
                if (inner_window.DecoOuterSizeX2 == 0.0f)
                    table.HostClipRect.Max.x = ImMath.ImMax(table.HostClipRect.Max.x - border_size, table.HostClipRect.Min.x);
            }
            if ((flags & ImGuiTableFlags_BordersOuterH) != 0)
            {
                table.HostClipRect.Min.y = ImMath.ImMin(table.HostClipRect.Min.y + border_size, table.HostClipRect.Max.y);
                if (inner_window.DecoOuterSizeY2 == 0.0f)
                    table.HostClipRect.Max.y = ImMath.ImMax(table.HostClipRect.Max.y - border_size, table.HostClipRect.Min.y);
            }
        }

        // Padding and Spacing
        // - None               ........Content..... Pad .....Content........
        // - PadOuter           | Pad ..Content..... Pad .....Content.. Pad |
        // - PadInner           ........Content.. Pad | Pad ..Content........
        // - PadOuter+PadInner  | Pad ..Content.. Pad | Pad ..Content.. Pad |
        bool pad_outer_x = (flags & ImGuiTableFlags_NoPadOuterX) != 0 ? false : (flags & ImGuiTableFlags_PadOuterX) != 0 ? true : (flags & ImGuiTableFlags_BordersOuterV) != 0;
        bool pad_inner_x = (flags & ImGuiTableFlags_NoPadInnerX) != 0 ? false : true;
        float inner_spacing_for_border = (flags & ImGuiTableFlags_BordersInnerV) != 0 ? TABLE_BORDER_SIZE : 0.0f;
        float inner_spacing_explicit = (pad_inner_x && (flags & ImGuiTableFlags_BordersInnerV) == 0) ? g.Style.CellPadding.x : 0.0f;
        float inner_padding_explicit = (pad_inner_x && (flags & ImGuiTableFlags_BordersInnerV) != 0) ? g.Style.CellPadding.x : 0.0f;
        table.CellSpacingX1 = inner_spacing_explicit + inner_spacing_for_border;
        table.CellSpacingX2 = inner_spacing_explicit;
        table.CellPaddingX = inner_padding_explicit;

        float outer_padding_for_border = (flags & ImGuiTableFlags_BordersOuterV) != 0 ? TABLE_BORDER_SIZE : 0.0f;
        float outer_padding_explicit = pad_outer_x ? g.Style.CellPadding.x : 0.0f;
        table.OuterPaddingX = (outer_padding_for_border + outer_padding_explicit) - table.CellPaddingX;

        table.CurrentColumn = -1;
        table.CurrentRow = -1;
        table.RowBgColorCounter = 0;
        table.LastRowFlags = ImGuiTableRowFlags_None;
        table.InnerClipRect = (inner_window == outer_window) ? table.WorkRect : inner_window.ClipRect;
        table.InnerClipRect.ClipWith(table.WorkRect);        // We need this to honor inner_width
        table.InnerClipRect.ClipWithFull(table.HostClipRect);
        table.InnerClipRect.Max.y = (flags & ImGuiTableFlags_NoHostExtendY) != 0 ? ImMath.ImMin(table.InnerClipRect.Max.y, inner_window.WorkRect.Max.y) : table.HostClipRect.Max.y;

        table.RowPosY1 = table.RowPosY2 = table.WorkRect.Min.y; // This is needed somehow
        table.RowTextBaseline = 0.0f; // This will be cleared again by TableBeginRow()
        table.RowCellPaddingY = 0.0f;
        table.FreezeRowsRequest = table.FreezeRowsCount = 0; // This will be setup by TableSetupScrollFreeze(), if any
        table.FreezeColumnsRequest = table.FreezeColumnsCount = 0;
        table.IsUnfrozenRows = true;
        table.DeclColumnsCount = table.AngledHeadersCount = 0;
        if (previous_frame_active + 1 < g.FrameCount)
            table.IsActiveIdInTable = false;
        table.AngledHeadersHeight = 0.0f;
        temp_data.AngledHeadersExtraWidth = 0.0f;

        // Using opaque colors facilitate overlapping lines of the grid, otherwise we'd need to improve TableDrawBorders()
        table.BorderColorStrong = GetColorU32(ImGuiCol_TableBorderStrong);
        table.BorderColorLight = GetColorU32(ImGuiCol_TableBorderLight);

        // Make table current
        g.CurrentTable = table;
        inner_window.DC.NavIsScrollPushableX = false; // Shortcut for NavUpdateCurrentWindowIsScrollPushableX();
        outer_window.DC.CurrentTableIdx = table_idx;
        if (inner_window != outer_window) // So EndChild() within the inner window can restore the table properly.
            inner_window.DC.CurrentTableIdx = table_idx;

        if ((previous_flags & ImGuiTableFlags_Reorderable) != 0 && (flags & ImGuiTableFlags_Reorderable) == 0)
            table.IsResetDisplayOrderRequest = true;

        // Mark as used to avoid GC
        if (table_idx >= g.TablesLastTimeActive.Size)
            g.TablesLastTimeActive.resize(table_idx + 1, -1.0f);
        g.TablesLastTimeActive[table_idx] = (float)g.Time;
        temp_data.LastTimeActive = (float)g.Time;
        table.MemoryCompacted = false;

        // Setup memory buffer (clear data if columns count changed)
        int old_columns_count = table.Columns.Length;
        if (old_columns_count != 0 && old_columns_count != columns_count)
        {
            // Attempt to preserve width and other settings on column count/specs change (#4046, #9108)
            Debug.Assert(temp_data.OldColumnsRawData == null);
            temp_data.OldColumnsRawData = table.RawData; // Freed during layout
            temp_data.OldColumnsData = table.Columns;
            for (int i = 0; i < table.TempData.OldColumnsData.Length; i++)
                table.TempData.OldColumnsData[i].IsNeedReconcileSrc = true;
            table.RawData = null;
        }
        if (table.RawData == null)
        {
            TableBeginInitMemory(table, columns_count);
            table.IsInitializing = true;
        }
        if (table.IsResetAllRequest)
            TableResetSettings(table);
        if (table.IsInitializing)
        {
            // Initialize
            if (is_new_table)
            {
                table.SettingsOffset = -1;
                table.IsSettingsRequestLoad = true;
            }
            table.IsSortSpecsDirty = true;
            table.IsSettingsDirty = true; // Records itself into .ini file even when in default state (#7934)
            table.IsReconcileMode = false;
            table.InstanceInteracted = -1;
            table.ContextPopupColumn = -1;
            table.ReorderColumn = table.ReorderColumnDstOrder = table.ResizedColumn = table.LastResizedColumn = -1;
            table.AutoFitSingleColumn = -1;
            table.HoveredColumnBody = table.HoveredColumnBorder = -1;
            for (int n = 0; n < columns_count; n++)
            {
                ImGuiTableColumn column = table.Columns[n];
                if (temp_data.OldColumnsData.Length > 0 && n < temp_data.OldColumnsData.Length)
                {
                    TableColumnAssign(column, temp_data.OldColumnsData[n]);
                }
                else
                {
                    float width_auto = column.WidthAuto;
                    TableColumnAssign(column, new ImGuiTableColumn());
                    column.WidthAuto = width_auto;
                    column.IsPreserveWidthAuto = true; // Preserve WidthAuto when reinitializing a live table: not technically necessary but remove a visible flicker
                    column.IsEnabled = column.IsUserEnabled = column.IsUserEnabledNextFrame = true;
                    column.DisplayOrder = (short)n;
                }
                table.DisplayOrderToIndex[n] = column.DisplayOrder;
            }
        }

        // Load settings
        if (table.IsSettingsRequestLoad)
            TableLoadSettings(table);

        // Disable output until user calls TableNextRow() or TableNextColumn() leading to the TableUpdateLayout() call..
        // This is not strictly necessary but will reduce cases were "out of table" output will be misleading to the user.
        // Because we cannot safely assert in EndTable() when no rows have been created, this seems like our best option.
        inner_window.SkipItems = true;

        // Clear names
        // At this point the ->NameOffset field of each column will be invalid until TableUpdateLayout() or the first call to TableSetupColumn()
        if (table.ColumnsNames.Buf.Size > 0)
            table.ColumnsNames.Buf.resize(0);

        return true;
    }

    // For reference, the average total _allocation count_ for a table is:
    // + 0 (for ImGuiTable instance, we are pooling allocations in g.Tables[])
    // + 1 (for table->RawData allocated below)
    // + 1 (for table->ColumnsNames, if names are used)
    // Shared allocations for the maximum number of simultaneously nested tables (generally a very small number)
    // + 1 (for table->Splitter._Channels)
    // + 2 * active_channels_count (for ImDrawCmd and ImDrawIdx buffers inside channels)
    // Where active_channels_count is variable but often == columns_count or == columns_count + 1, see TableSetupDrawChannels() for details.
    // Unused channels don't perform their +2 allocations.
    // C++: void ImGui::TableBeginInitMemory(ImGuiTable* table, int columns_count)
    // C# note: C++ packs Columns[]+DisplayOrderToIndex[]+RowCellData[]+3 bitmasks into a single RawData buffer.
    // This port uses managed arrays (see ImGuiTable); RawData is only kept as a non-null "allocated" marker
    // so the "columns count changed" detection in BeginTableEx() works the same way.
    public static void TableBeginInitMemory(ImGuiTable table, int columns_count)
    {
        // Allocate single buffer for our arrays (C#: one managed array each)
        table.Columns = new ImGuiTableColumn[columns_count];
        for (int n = 0; n < columns_count; n++)
            table.Columns[n] = new ImGuiTableColumn();
        table.DisplayOrderToIndex = new short[columns_count];
        table.RowCellData = new ImGuiTableCellData[columns_count];
        table.EnabledMaskByDisplayOrder = new bool[columns_count];
        table.EnabledMaskByIndex = new bool[columns_count];
        table.VisibleMaskByIndex = new bool[columns_count];
        table.RawData = new byte[1]; // Non-null marker (C++: single arena allocation for the arrays above)
    }

    // Apply queued resizing/reordering/hiding requests
    // C++: void ImGui::TableApplyQueuedRequests(ImGuiTable* table)
    public static void TableApplyQueuedRequests(ImGuiTable table)
    {
        // Handle resizing request
        // (We process this in the TableBegin() of the first instance of each table)
        // FIXME-TABLE: Contains columns if our work area doesn't allow for scrolling?
        if (table.InstanceCurrent == 0)
        {
            if (table.ResizedColumn != -1 && table.ResizedColumnNextWidth != float.MaxValue)
                TableSetColumnWidth(table.ResizedColumn, table.ResizedColumnNextWidth);
            table.LastResizedColumn = table.ResizedColumn;
            table.ResizedColumnNextWidth = float.MaxValue;
            table.ResizedColumn = -1;

            // Process auto-fit for single column, which is a special case for stretch columns and fixed columns with FixedSame policy.
            // FIXME-TABLE: Would be nice to redistribute available stretch space accordingly to other weights, instead of giving it all to siblings.
            if (table.AutoFitSingleColumn != -1)
            {
                TableSetColumnWidth(table.AutoFitSingleColumn, table.Columns[table.AutoFitSingleColumn].WidthAuto);
                table.AutoFitSingleColumn = -1;
            }
        }

        // Handle reordering request
        //// Note: we don't clear ReorderColumn after handling the request (FIXME: clarify why or add a test).
        if (table.InstanceCurrent == 0)
        {
            table.LastHeldHeaderColumn = table.HeldHeaderColumn;
            table.HeldHeaderColumn = -1;
            if (table.ReorderColumn != -1 && table.ReorderColumnDstOrder != -1)
            {
                TableSetColumnDisplayOrder(table, table.ReorderColumn, table.ReorderColumnDstOrder);
                table.ReorderColumnDstOrder = -1;
            }

            // Release
            ImGuiContext g = GImGui;
            if (g.ActiveId == 0) // FIXME: Need to revisit. See 38f5e5a.
                table.ReorderColumn = -1;
            //if (table->HeldHeaderColumn == -1 && table->ReorderColumn != -1)
            //    table->ReorderColumn = -1;
        }

        // Handle display order / visibility reset requests
        if (table.IsResetDisplayOrderRequest)
        {
            for (int n = 0; n < table.ColumnsCount; n++)
            {
                table.DisplayOrderToIndex[n] = (short)n;
                table.Columns[n].DisplayOrder = (short)n;
            }
            table.IsResetDisplayOrderRequest = false;
            table.IsSettingsDirty = true;
        }
        if (table.IsResetVisibilityRequest)
        {
            for (int n = 0; n < table.ColumnsCount; n++)
            {
                ImGuiTableColumn column = table.Columns[n];
                bool enabled = (column.Flags & ImGuiTableColumnFlags_DefaultHide) == 0;
                column.IsUserEnabled = column.IsUserEnabledNextFrame = enabled;
            }
            table.IsResetVisibilityRequest = false;
            table.IsSettingsDirty = true;
        }
    }

    // Apply immediately. See TableQueueSetColumnDisplayOrder() for additional checks/constraints.
    // C++: void ImGui::TableSetColumnDisplayOrder(ImGuiTable* table, int column_n, int dst_order)
    public static void TableSetColumnDisplayOrder(ImGuiTable table, int column_n, int dst_order)
    {
        Debug.Assert(column_n >= 0 && column_n < table.ColumnsCount);
        Debug.Assert(dst_order >= 0 && dst_order < table.ColumnsCount);

        ImGuiTableColumn src_column = table.Columns[column_n];
        int src_order = src_column.DisplayOrder;
        if (src_order == dst_order)
            return;
        int reorder_dir = (dst_order < src_order) ? -1 : +1;

        src_column.DisplayOrder = (short)dst_order;
        for (int order_n = src_order + reorder_dir; order_n != dst_order + reorder_dir; order_n += reorder_dir)
            table.Columns[table.DisplayOrderToIndex[order_n]].DisplayOrder -= (short)reorder_dir;
        //Debug.Assert(dst_column->DisplayOrder == dst_order - reorder_dir);

        // Display order is stored in both columns->IndexDisplayOrder and table->DisplayOrder[]. Rebuild later from the former.
        // FIXME-OPT: If this is called multiple times we'd effectively have a O(N^2) thing going on.
        for (int n = 0; n < table.ColumnsCount; n++)
            table.DisplayOrderToIndex[table.Columns[n].DisplayOrder] = (short)n;
        table.IsSettingsDirty = true;
    }

    // C++: static int TableGetMaxDisplayOrderAllowed(ImGuiTable* table, int src_order, int dst_order)
    static int TableGetMaxDisplayOrderAllowed(ImGuiTable table, int src_order, int dst_order)
    {
        dst_order = ImMath.ImClamp(dst_order, 0, table.ColumnsCount - 1);
        if (src_order == dst_order)
            return dst_order;

        // Cannot cross over the frozen column limit when interactively reordering.
        // TableSetupScrollFreeze() enforce a display order range for frozen columns. Reordering across the frozen column barrier is illegal and will be undone.
        if (table.FreezeColumnsRequest > 0)
            dst_order = (src_order < table.FreezeColumnsRequest) ? ImMath.ImMin(dst_order, table.FreezeColumnsRequest - 1) : ImMath.ImMax(dst_order, table.FreezeColumnsRequest);

        // Cannot cross over a column with the ImGuiTableColumnFlags_NoReorder flag.
        int reorder_dir = (src_order < dst_order) ? +1 : -1;
        for (int order_n = src_order; (src_order < dst_order && order_n <= dst_order) || (dst_order < src_order && order_n >= dst_order); order_n += reorder_dir)
            if ((table.Columns[table.DisplayOrderToIndex[order_n]].Flags & ImGuiTableColumnFlags_NoReorder) != 0)
            {
                dst_order = (order_n == src_order) ? src_order : order_n - reorder_dir;
                break;
            }
        return dst_order;
    }

    // Reorder requested by user interaction.
    // C++: void ImGui::TableQueueSetColumnDisplayOrder(ImGuiTable* table, int column_n, int dst_order)
    public static void TableQueueSetColumnDisplayOrder(ImGuiTable table, int column_n, int dst_order)
    {
        int src_order = table.Columns[column_n].DisplayOrder;
        table.ReorderColumn = (short)column_n;
        table.ReorderColumnDstOrder = -1;
        dst_order = TableGetMaxDisplayOrderAllowed(table, src_order, dst_order);
        if (table.IsLayoutLocked && dst_order == src_order) // We allow calling the function before layout w/ reconcile so don't early out.
            return;
        table.ReorderColumnDstOrder = (short)dst_order;
    }

    // Adjust flags: default width mode + stretch columns are not allowed when auto extending
    // C++: static void TableSetupColumnFlags(ImGuiTable* table, ImGuiTableColumn* column, ImGuiTableColumnFlags flags_in)
    static void TableSetupColumnFlags(ImGuiTable table, ImGuiTableColumn column, ImGuiTableColumnFlags flags_in)
    {
        ImGuiTableColumnFlags flags = flags_in;

        // Sizing Policy
        if ((flags & ImGuiTableColumnFlags_WidthMask_) == 0)
        {
            int table_sizing_policy = table.Flags & ImGuiTableFlags_SizingMask_;
            if (table_sizing_policy == ImGuiTableFlags_SizingFixedFit || table_sizing_policy == ImGuiTableFlags_SizingFixedSame)
                flags |= ImGuiTableColumnFlags_WidthFixed;
            else
                flags |= ImGuiTableColumnFlags_WidthStretch;
        }
        else
        {
            Debug.Assert(ImMath.ImIsPowerOfTwo((int)(flags & ImGuiTableColumnFlags_WidthMask_))); // Check that only 1 of each set is used.
        }

        // Resize
        if ((table.Flags & ImGuiTableFlags_Resizable) == 0)
            flags |= ImGuiTableColumnFlags_NoResize;

        // Sorting
        if ((flags & ImGuiTableColumnFlags_NoSortAscending) != 0 && (flags & ImGuiTableColumnFlags_NoSortDescending) != 0)
            flags |= ImGuiTableColumnFlags_NoSort;

        // Indentation
        if ((flags & ImGuiTableColumnFlags_IndentMask_) == 0)
            flags |= (Array.IndexOf(table.Columns, column) == 0) ? ImGuiTableColumnFlags_IndentEnable : ImGuiTableColumnFlags_IndentDisable;

        // Alignment
        //if ((flags & ImGuiTableColumnFlags_AlignMask_) == 0)
        //    flags |= ImGuiTableColumnFlags_AlignCenter;
        //IM_ASSERT(ImIsPowerOfTwo(flags & ImGuiTableColumnFlags_AlignMask_)); // Check that only 1 of each set is used.

        // Preserve status flags
        column.Flags = (int)flags | (column.Flags & ImGuiTableColumnFlags_StatusMask_);

        // Build an ordered list of available sort directions
        column.SortDirectionsAvailCount = column.SortDirectionsAvailMask = column.SortDirectionsAvailList = 0;
        if ((table.Flags & ImGuiTableFlags_Sortable) != 0)
        {
            int count = 0, mask = 0, list = 0;
            if ((flags & ImGuiTableColumnFlags_PreferSortAscending) != 0 && (flags & ImGuiTableColumnFlags_NoSortAscending) == 0) { mask |= 1 << ImGuiSortDirection_Ascending; list |= ImGuiSortDirection_Ascending << (count << 1); count++; }
            if ((flags & ImGuiTableColumnFlags_PreferSortDescending) != 0 && (flags & ImGuiTableColumnFlags_NoSortDescending) == 0) { mask |= 1 << ImGuiSortDirection_Descending; list |= ImGuiSortDirection_Descending << (count << 1); count++; }
            if ((flags & ImGuiTableColumnFlags_PreferSortAscending) == 0 && (flags & ImGuiTableColumnFlags_NoSortAscending) == 0) { mask |= 1 << ImGuiSortDirection_Ascending; list |= ImGuiSortDirection_Ascending << (count << 1); count++; }
            if ((flags & ImGuiTableColumnFlags_PreferSortDescending) == 0 && (flags & ImGuiTableColumnFlags_NoSortDescending) == 0) { mask |= 1 << ImGuiSortDirection_Descending; list |= ImGuiSortDirection_Descending << (count << 1); count++; }
            if ((table.Flags & ImGuiTableFlags_SortTristate) != 0 || count == 0) { mask |= 1 << ImGuiSortDirection_None; count++; }
            column.SortDirectionsAvailList = (byte)list;
            column.SortDirectionsAvailMask = (byte)mask;
            column.SortDirectionsAvailCount = (byte)count;
            TableFixColumnSortDirection(table, column);
        }
    }

    // Layout columns for the frame. This is in essence the followup to BeginTable() and this is our largest function.
    // Runs on the first call to TableNextRow(), to give a chance for TableSetupColumn() and other TableSetupXXXXX() functions to be called first.
    // FIXME-TABLE: Our width (and therefore our WorkRect) will be minimal in the first frame for _WidthAuto columns.
    // Increase feedback side-effect with widgets relying on WorkRect.Max.x... Maybe provide a default distribution for _WidthAuto columns?
    // C++: void ImGui::TableUpdateLayout(ImGuiTable* table)
    public static void TableUpdateLayout(ImGuiTable table)
    {
        ImGuiContext g = GImGui;
        ImGuiTableTempData temp_data = table.TempData;
        Debug.Assert(table.IsLayoutLocked == false);
        int columns_count = table.ColumnsCount;

        // Reconcile moved columns
        if (temp_data.ReconcileColumnsRequests.Size > 0)
            TableReconcileColumns(table);
        if (temp_data.OldColumnsRawData != null)
        {
            temp_data.OldColumnsRawData = null; // C#: freed by GC (C++: IM_FREE)
            temp_data.OldColumnsData = Array.Empty<ImGuiTableColumn>();
        }

        // Apply columns settings
        if (table.IsSettingsRequestLoad)
            TableLoadSettingsForColumns(table);
        if (table.IsInitializing || table.IsSettingsRequestLoad)
        {
            for (int column_n = 0; column_n < columns_count; column_n++)
            {
                ImGuiTableColumn column = table.Columns[column_n];
                int init_flags;
                if (table.IsSettingsRequestLoad)
                    init_flags = column.IsLoadedSettings ? ~table.SettingsLoadedFlags : ~0;
                else
                    init_flags = column.IsJustCreated ? ~0 : 0;
                TableInitColumnDefaults(table, column, init_flags);
            }
            TableFixDisplayOrder(table); // Call even for non _Reorderable table as we loaded .ini data.
            table.IsSettingsRequestLoad = false;
        }

        // Apply queued resizing/reordering/hiding requests
        TableApplyQueuedRequests(table);

        // Handle DPI/font resize
        // This is designed to facilitate DPI changes with the assumption that e.g. style.CellPadding has been scaled as well.
        // It will also react to changing fonts with mixed results. It doesn't need to be perfect but merely provide a decent transition.
        // FIXME-DPI: Provide consistent standards for reference size. Perhaps using g.CurrentDpiScale would be more self explanatory.
        // This is will lead us to non-rounded WidthRequest in columns, which should work but is a poorly tested path.
        float new_ref_scale_unit = g.FontSize; // g.Font->GetCharAdvance('A') ?
        if (table.RefScale != 0.0f && table.RefScale != new_ref_scale_unit)
        {
            float scale_factor = new_ref_scale_unit / table.RefScale;
            for (int n = 0; n < columns_count; n++)
                table.Columns[n].WidthRequest = table.Columns[n].WidthRequest * scale_factor;
        }
        table.RefScale = new_ref_scale_unit;

        int table_sizing_policy = table.Flags & ImGuiTableFlags_SizingMask_;
        table.IsDefaultDisplayOrder = table.IsDefaultVisibility = true;
        table.ColumnsEnabledCount = 0;
        Array.Clear(table.EnabledMaskByIndex, 0, columns_count);
        Array.Clear(table.EnabledMaskByDisplayOrder, 0, columns_count);
        table.LeftMostEnabledColumn = -1;
        table.MinColumnWidth = ImMath.ImMax(1.0f, g.Style.FramePadding.x * 1.0f); // g.Style.ColumnsMinSpacing; // FIXME-TABLE

        // [Part 1] Apply/lock Enabled and Order states. Calculate auto/ideal width for columns. Count fixed/stretch columns.
        // Process columns in their visible orders as we are building the Prev/Next indices.
        int count_fixed = 0;                // Number of columns that have fixed sizing policies
        int count_stretch = 0;              // Number of columns that have stretch sizing policies
        int prev_visible_column_idx = -1;
        bool has_auto_fit_request = false;
        bool has_resizable = false;
        float stretch_sum_width_auto = 0.0f;
        float fixed_max_width_auto = 0.0f;
        for (int order_n = 0; order_n < columns_count; order_n++)
        {
            int column_n = table.DisplayOrderToIndex[order_n];
            ImGuiTableColumn column = table.Columns[column_n];

            // Clear column setup if not submitted by user. Currently we make it mandatory to call TableSetupColumn() every frame.
            // It would easily work without but we're not ready to guarantee it since e.g. names need resubmission anyway.
            // We take a slight shortcut but in theory we could be calling TableSetupColumn() here with dummy values, it should yield the same effect.
            if (table.DeclColumnsCount <= column_n)
            {
                TableSetupColumnFlags(table, column, ImGuiTableColumnFlags_None);
                column.NameOffset = -1;
                column.ID = column.UserData = 0;
                column.InitStretchWeightOrWidth = -1.0f;
            }

            // Update Enabled state, mark settings and sort specs dirty
            if ((table.Flags & ImGuiTableFlags_Hideable) == 0 || (column.Flags & ImGuiTableColumnFlags_NoHide) != 0)
                column.IsUserEnabledNextFrame = true;
            if (column.IsUserEnabled != column.IsUserEnabledNextFrame)
            {
                column.IsUserEnabled = column.IsUserEnabledNextFrame;
                table.IsSettingsDirty = true;
            }
            column.IsEnabled = column.IsUserEnabled && (column.Flags & ImGuiTableColumnFlags_Disabled) == 0;
            column.IsJustCreated = false;

            if (column.IsEnabled != ((column.Flags & ImGuiTableColumnFlags_DefaultHide) == 0))
                table.IsDefaultVisibility = false;
            if (column_n != order_n)
                table.IsDefaultDisplayOrder = false;

            if (column.SortOrder != -1 && !column.IsEnabled)
                table.IsSortSpecsDirty = true;
            if (column.SortOrder > 0 && (table.Flags & ImGuiTableFlags_SortMulti) == 0)
                table.IsSortSpecsDirty = true;

            // Auto-fit unsized columns
            bool start_auto_fit = (column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0 ? (column.WidthRequest < 0.0f) : (column.StretchWeight < 0.0f);
            if (start_auto_fit)
                column.AutoFitQueue = column.CannotSkipItemsQueue = (1 << 3) - 1; // Fit for three frames

            if (!column.IsEnabled)
            {
                column.IndexWithinEnabledSet = -1;
                continue;
            }

            // Mark as enabled and link to previous/next enabled column
            column.PrevEnabledColumn = (short)prev_visible_column_idx;
            column.NextEnabledColumn = -1;
            if (prev_visible_column_idx != -1)
                table.Columns[prev_visible_column_idx].NextEnabledColumn = (short)column_n;
            else
                table.LeftMostEnabledColumn = (short)column_n;
            column.IndexWithinEnabledSet = table.ColumnsEnabledCount++;
            table.EnabledMaskByIndex[column_n] = true;
            table.EnabledMaskByDisplayOrder[column.DisplayOrder] = true;
            prev_visible_column_idx = column_n;
            Debug.Assert(column.IndexWithinEnabledSet <= column.DisplayOrder);

            // Calculate ideal/auto column width (that's the width required for all contents to be visible without clipping)
            // Combine width from regular rows + width from headers unless requested not to.
            if (!column.IsPreserveWidthAuto && table.InstanceCurrent == 0)
                column.WidthAuto = TableGetColumnWidthAuto(table, column);

            // Non-resizable columns keep their requested width (apply user value regardless of IsPreserveWidthAuto)
            bool column_is_resizable = (column.Flags & ImGuiTableColumnFlags_NoResize) == 0;
            if (column_is_resizable)
                has_resizable = true;
            if ((column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0 && column.InitStretchWeightOrWidth > 0.0f && !column_is_resizable)
                column.WidthAuto = column.InitStretchWeightOrWidth;

            if (column.AutoFitQueue != 0x00)
                has_auto_fit_request = true;
            if ((column.Flags & ImGuiTableColumnFlags_WidthStretch) != 0)
            {
                stretch_sum_width_auto += column.WidthAuto;
                count_stretch++;
            }
            else
            {
                fixed_max_width_auto = ImMath.ImMax(fixed_max_width_auto, column.WidthAuto);
                count_fixed++;
            }
        }
        if ((table.Flags & ImGuiTableFlags_Sortable) != 0 && table.SortSpecsCount == 0 && (table.Flags & ImGuiTableFlags_SortTristate) == 0)
            table.IsSortSpecsDirty = true;
        table.RightMostEnabledColumn = (short)prev_visible_column_idx;
        Debug.Assert(table.LeftMostEnabledColumn >= 0 && table.RightMostEnabledColumn >= 0);

        // [Part 2] Disable child window clipping while fitting columns. This is not strictly necessary but makes it possible to avoid
        // the column fitting having to wait until the first visible frame of the child container (may or not be a good thing). Also see #6510.
        // FIXME-TABLE: for always auto-resizing columns may not want to do that all the time.
        if (has_auto_fit_request && table.OuterWindow != table.InnerWindow)
            table.InnerWindow.SkipItems = false;
        if (has_auto_fit_request)
            table.IsSettingsDirty = true;

        // [Part 3] Fix column flags and record a few extra information.
        float sum_width_requests = 0.0f;     // Sum of all width for fixed and auto-resize columns, excluding width contributed by Stretch columns but including spacing/padding.
        float stretch_sum_weights = 0.0f;    // Sum of all weights for stretch columns.
        table.LeftMostStretchedColumn = table.RightMostStretchedColumn = -1;
        for (int column_n = 0; column_n < columns_count; column_n++)
        {
            if (!table.EnabledMaskByIndex[column_n])
                continue;
            ImGuiTableColumn column = table.Columns[column_n];

            bool column_is_resizable = (column.Flags & ImGuiTableColumnFlags_NoResize) == 0;
            if ((column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0)
            {
                // Apply same widths policy
                float width_auto = column.WidthAuto;
                if (table_sizing_policy == ImGuiTableFlags_SizingFixedSame && (column.AutoFitQueue != 0x00 || !column_is_resizable))
                    width_auto = fixed_max_width_auto;

                // Apply automatic width
                // Latch initial size for fixed columns and update it constantly for auto-resizing column (unless clipped!)
                if (column.AutoFitQueue != 0x00)
                    column.WidthRequest = width_auto;
                else if ((column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0 && !column_is_resizable && column.IsRequestOutput)
                    column.WidthRequest = width_auto;

                // FIXME-TABLE: Increase minimum size during init frame to avoid biasing auto-fitting widgets
                // (e.g. TextWrapped) too much. Otherwise what tends to happen is that TextWrapped would output a very
                // large height (= first frame scrollbar display very off + clipper would skip lots of items).
                // This is merely making the side-effect less extreme, but doesn't properly fixes it.
                // FIXME: Move this to ->WidthGiven to avoid temporary lossyness?
                // FIXME: This break IsPreserveWidthAuto from not flickering if the stored WidthAuto was smaller.
                if (column.AutoFitQueue > 0x01 && table.IsInitializing && !column.IsPreserveWidthAuto)
                    column.WidthRequest = ImMath.ImMax(column.WidthRequest, table.MinColumnWidth * 4.0f); // FIXME-TABLE: Another constant/scale?
                sum_width_requests += column.WidthRequest;
            }
            else
            {
                // Initialize stretch weight
                if (column.AutoFitQueue != 0x00 || column.StretchWeight < 0.0f || !column_is_resizable)
                {
                    if (column.InitStretchWeightOrWidth > 0.0f)
                        column.StretchWeight = column.InitStretchWeightOrWidth;
                    else if (table_sizing_policy == ImGuiTableFlags_SizingStretchProp)
                        column.StretchWeight = (column.WidthAuto / stretch_sum_width_auto) * count_stretch;
                    else
                        column.StretchWeight = 1.0f;
                }

                stretch_sum_weights += column.StretchWeight;
                if (table.LeftMostStretchedColumn == -1 || table.Columns[table.LeftMostStretchedColumn].DisplayOrder > column.DisplayOrder)
                    table.LeftMostStretchedColumn = (short)column_n;
                if (table.RightMostStretchedColumn == -1 || table.Columns[table.RightMostStretchedColumn].DisplayOrder < column.DisplayOrder)
                    table.RightMostStretchedColumn = (short)column_n;
            }
            column.IsPreserveWidthAuto = false;
            sum_width_requests += table.CellPaddingX * 2.0f;
        }
        table.ColumnsEnabledFixedCount = (short)count_fixed;
        table.ColumnsStretchSumWeights = stretch_sum_weights;

        // [Part 4] Apply final widths based on requested widths
        ImRect work_rect = table.WorkRect;
        float width_spacings = (table.OuterPaddingX * 2.0f) + (table.CellSpacingX1 + table.CellSpacingX2) * (table.ColumnsEnabledCount - 1);
        float width_removed = (table.HasScrollbarYPrev && !table.InnerWindow.ScrollbarY) ? g.Style.ScrollbarSize : 0.0f; // To synchronize decoration width of synced tables with mismatching scrollbar state (#5920)
        float width_avail = ImMath.ImMax(1.0f, (((table.Flags & ImGuiTableFlags_ScrollX) != 0 && table.InnerWidth == 0.0f) ? table.InnerClipRect.GetWidth() : work_rect.GetWidth()) - width_removed);
        float width_avail_for_stretched_columns = width_avail - width_spacings - sum_width_requests;
        float width_remaining_for_stretched_columns = width_avail_for_stretched_columns;
        table.ColumnsGivenWidth = width_spacings + (table.CellPaddingX * 2.0f) * table.ColumnsEnabledCount;
        for (int column_n = 0; column_n < columns_count; column_n++)
        {
            if (!table.EnabledMaskByIndex[column_n])
                continue;
            ImGuiTableColumn column = table.Columns[column_n];

            // Allocate width for stretched/weighted columns (StretchWeight gets converted into WidthRequest)
            if ((column.Flags & ImGuiTableColumnFlags_WidthStretch) != 0)
            {
                float weight_ratio = column.StretchWeight / stretch_sum_weights;
                column.WidthRequest = ImMath.ImTrunc(ImMath.ImMax(width_avail_for_stretched_columns * weight_ratio, table.MinColumnWidth) + 0.01f); // IM_TRUNC
                width_remaining_for_stretched_columns -= column.WidthRequest;
            }

            // [Resize Rule 1] The right-most Visible column is not resizable if there is at least one Stretch column
            // See additional comments in TableSetColumnWidth().
            if (column.NextEnabledColumn == -1 && table.LeftMostStretchedColumn != -1)
                column.Flags |= ImGuiTableColumnFlags_NoDirectResize_;

            // Assign final width, record width in case we will need to shrink
            column.WidthGiven = ImMath.ImTrunc(ImMath.ImMax(column.WidthRequest, table.MinColumnWidth));
            table.ColumnsGivenWidth += column.WidthGiven;
        }

        // [Part 5] Redistribute stretch remainder width due to rounding (remainder width is < 1.0f * number of Stretch column).
        // Using right-to-left distribution (more likely to match resizing cursor).
        if (width_remaining_for_stretched_columns >= 1.0f && (table.Flags & ImGuiTableFlags_PreciseWidths) == 0)
            for (int order_n = columns_count - 1; stretch_sum_weights > 0.0f && width_remaining_for_stretched_columns >= 1.0f && order_n >= 0; order_n--)
            {
                if (!table.EnabledMaskByDisplayOrder[order_n])
                    continue;
                ImGuiTableColumn column = table.Columns[table.DisplayOrderToIndex[order_n]];
                if ((column.Flags & ImGuiTableColumnFlags_WidthStretch) == 0)
                    continue;
                column.WidthRequest += 1.0f;
                column.WidthGiven += 1.0f;
                width_remaining_for_stretched_columns -= 1.0f;
            }

        // Determine if table is hovered which will be used to flag columns as hovered.
        // - In principle we'd like to use the equivalent of IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem),
        //   but because our item is partially submitted at this point we use ItemHoverable() and a workaround (temporarily
        //   clear ActiveId, which is equivalent to the change provided by _AllowWhenBLockedByActiveItem).
        // - This allows columns to be marked as hovered when e.g. clicking a button inside the column, or using drag and drop.
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        table_instance.HoveredRowLast = table_instance.HoveredRowNext;
        table_instance.HoveredRowNext = -1;
        table.HoveredColumnBody = table.HoveredColumnBorder = -1;
        ImRect mouse_hit_rect = new ImRect(table.OuterRect.Min.x, table.OuterRect.Min.y, table.OuterRect.Max.x, ImMath.ImMax(table.OuterRect.Max.y, table.OuterRect.Min.y + table_instance.LastOuterHeight));
        ImGuiID backup_active_id = g.ActiveId;
        g.ActiveId = 0;
        bool is_hovering_table = ItemHoverable(mouse_hit_rect, 0, ImGuiItemFlags_None);
        g.ActiveId = backup_active_id;

        // Determine skewed MousePos.x to support angled headers.
        float mouse_skewed_x = g.IO.MousePos.x;
        if (table.AngledHeadersHeight > 0.0f)
            if (g.IO.MousePos.y >= table.OuterRect.Min.y && g.IO.MousePos.y <= table.OuterRect.Min.y + table.AngledHeadersHeight)
                mouse_skewed_x += ImMath.ImTrunc((table.OuterRect.Min.y + table.AngledHeadersHeight - g.IO.MousePos.y) * table.AngledHeadersSlope);

        // [Part 6] Setup final position, offset, skip/clip states and clipping rectangles, detect hovered column
        // Process columns in their visible orders as we are comparing the visible order and adjusting host_clip_rect while looping.
        bool has_at_least_one_column_requesting_output = false;
        bool offset_x_frozen = (table.FreezeColumnsCount > 0);
        float offset_x = ((table.FreezeColumnsCount > 0) ? table.OuterRect.Min.x : work_rect.Min.x) + table.OuterPaddingX - table.CellSpacingX1;
        ImRect host_clip_rect = table.InnerClipRect;
        //host_clip_rect.Max.x += table->CellPaddingX + table->CellSpacingX2;
        Array.Clear(table.VisibleMaskByIndex, 0, columns_count);
        for (int order_n = 0; order_n < columns_count; order_n++)
        {
            int column_n = table.DisplayOrderToIndex[order_n];
            ImGuiTableColumn column = table.Columns[column_n];

            // Initial nav layer: using FreezeRowsCount, NOT FreezeRowsRequest, so Header line changes layer when frozen
            column.NavLayerCurrent = (sbyte)(table.FreezeRowsCount > 0 ? ImGuiNavLayer_Menu : table.NavLayer);

            if (offset_x_frozen && table.FreezeColumnsCount == order_n)
            {
                offset_x += work_rect.Min.x - table.OuterRect.Min.x;
                offset_x_frozen = false;
            }

            // Clear status flags
            column.Flags &= ~ImGuiTableColumnFlags_StatusMask_;

            if (!table.EnabledMaskByDisplayOrder[order_n])
            {
                // Hidden column: clear a few fields and we are done with it for the remainder of the function.
                // We set a zero-width clip rect but set Min.y/Max.y properly to not interfere with the clipper.
                column.MinX = offset_x;
                column.MaxX = offset_x;
                column.WorkMinX = offset_x;
                column.ClipRect.Min.x = offset_x;
                column.ClipRect.Max.x = offset_x;
                column.WidthGiven = 0.0f;
                column.ClipRect.Min.y = work_rect.Min.y;
                column.ClipRect.Max.y = float.MaxValue;
                column.ClipRect.ClipWithFull(host_clip_rect);
                column.IsVisibleX = column.IsVisibleY = column.IsRequestOutput = false;
                column.IsSkipItems = true;
                column.ItemWidth = 1.0f;
                continue;
            }

            // Lock start position
            column.MinX = offset_x;

            // Lock width based on start position and minimum/maximum width for this position
            column.WidthMax = TableCalcMaxColumnWidth(table, column_n);
            column.WidthGiven = ImMath.ImMin(column.WidthGiven, column.WidthMax);
            column.WidthGiven = ImMath.ImMax(column.WidthGiven, ImMath.ImMin(column.WidthRequest, table.MinColumnWidth));
            column.MaxX = offset_x + column.WidthGiven + table.CellSpacingX1 + table.CellSpacingX2 + table.CellPaddingX * 2.0f;

            // Lock other positions
            // - ClipRect.Min.x: Because merging draw commands doesn't compare min boundaries, we make ClipRect.Min.x match left bounds to be consistent regardless of merging.
            // - ClipRect.Max.x: using WorkMaxX instead of MaxX (aka including padding) makes things more consistent when resizing down, tho slightly detrimental to visibility in very-small column.
            // - ClipRect.Max.x: using MaxX makes it easier for header to receive hover highlight with no discontinuity and display sorting arrow.
            // - FIXME-TABLE: We want equal width columns to have equal (ClipRect.Max.x - WorkMinX) width, which means ClipRect.max.x cannot stray off host_clip_rect.Max.x else right-most column may appear shorter.
            float previous_instance_work_min_x = column.WorkMinX;
            column.WorkMinX = column.MinX + table.CellPaddingX + table.CellSpacingX1;
            column.WorkMaxX = column.MaxX - table.CellPaddingX - table.CellSpacingX2; // Expected max
            column.ItemWidth = ImMath.ImTrunc(column.WidthGiven * 0.65f);
            column.ClipRect.Min.x = column.MinX;
            column.ClipRect.Min.y = work_rect.Min.y;
            column.ClipRect.Max.x = column.MaxX; //column->WorkMaxX;
            column.ClipRect.Max.y = float.MaxValue;
            column.ClipRect.ClipWithFull(host_clip_rect);

            // Mark column as Clipped (not in sight)
            // Note that scrolling tables (where inner_window != outer_window) handle Y clipped earlier in BeginTable() so IsVisibleY really only applies to non-scrolling tables.
            // FIXME-TABLE: Because InnerClipRect.Max.y is conservatively ==outer_window->ClipRect.Max.y, we never can mark columns _Above_ the scroll line as not IsVisibleY.
            // Taking advantage of LastOuterHeight would yield good results there...
            // FIXME-TABLE: Y clipping is disabled because it effectively means not submitting will reduce contents width which is fed to outer_window->DC.CursorMaxPos.x,
            // and this may be used (e.g. typically by outer_window using AlwaysAutoResize or outer_window's horizontal scrollbar, but could be something else).
            // Possible solution to preserve last known content width for clipped column. Test 'table_reported_size' fails when enabling Y clipping and window is resized small.
            column.IsVisibleX = (column.ClipRect.Max.x > column.ClipRect.Min.x);
            column.IsVisibleY = true; // (column->ClipRect.Max.y > column->ClipRect.Min.y);
            bool is_visible = column.IsVisibleX; //&& column->IsVisibleY;
            if (is_visible)
                table.VisibleMaskByIndex[column_n] = true;

            // Mark column as requesting output from user. Note that fixed + non-resizable sets are auto-fitting at all times and therefore always request output.
            column.IsRequestOutput = is_visible || column.AutoFitQueue != 0 || column.CannotSkipItemsQueue != 0;

            // Mark column as SkipItems (ignoring all items/layout)
            // (table->HostSkipItems is a copy of inner_window->SkipItems before we cleared it above in Part 2)
            column.IsSkipItems = !column.IsEnabled || table.HostSkipItems;
            if (column.IsSkipItems)
                Debug.Assert(!is_visible);
            if (column.IsRequestOutput && !column.IsSkipItems)
                has_at_least_one_column_requesting_output = true;

            // Update status flags
            column.Flags |= ImGuiTableColumnFlags_IsEnabled;
            if (is_visible)
                column.Flags |= ImGuiTableColumnFlags_IsVisible;
            if (column.SortOrder != -1)
                column.Flags |= ImGuiTableColumnFlags_IsSorted;

            // Detect hovered column
            if (is_hovering_table && mouse_skewed_x >= column.ClipRect.Min.x && mouse_skewed_x < column.ClipRect.Max.x)
            {
                column.Flags |= ImGuiTableColumnFlags_IsHovered;
                table.HoveredColumnBody = (short)column_n;
            }

            // Alignment
            // FIXME-TABLE: This align based on the whole column width, not per-cell, and therefore isn't useful in
            // many cases (to be able to honor this we might be able to store a log of cells width, per row, for
            // visible rows, but nav/programmatic scroll would have visible artifacts.)
            //if (column->Flags & ImGuiTableColumnFlags_AlignRight)
            //    column->WorkMinX = ImMax(column->WorkMinX, column->MaxX - column->ContentWidthRowsUnfrozen);
            //else if (column->Flags & ImGuiTableColumnFlags_AlignCenter)
            //    column->WorkMinX = ImLerp(column->WorkMinX, ImMax(column->StartX, column->MaxX - column->ContentWidthRowsUnfrozen), 0.5f);

            // Reset content width variables
            if (table.InstanceCurrent == 0)
            {
                column.ContentMaxXFrozen = column.WorkMinX;
                column.ContentMaxXUnfrozen = column.WorkMinX;
                column.ContentMaxXHeadersUsed = column.WorkMinX;
                column.ContentMaxXHeadersIdeal = column.WorkMinX;
            }
            else
            {
                // As we store an absolute value to make per-cell updates faster, we need to offset values used for width computation.
                float offset_from_previous_instance = column.WorkMinX - previous_instance_work_min_x;
                column.ContentMaxXFrozen += offset_from_previous_instance;
                column.ContentMaxXUnfrozen += offset_from_previous_instance;
                column.ContentMaxXHeadersUsed += offset_from_previous_instance;
                column.ContentMaxXHeadersIdeal += offset_from_previous_instance;
            }

            // Don't decrement auto-fit counters until container window got a chance to submit its items
            if (table.HostSkipItems == false && table.InstanceCurrent == 0)
            {
                column.AutoFitQueue >>= 1;
                column.CannotSkipItemsQueue >>= 1;
            }

            if (order_n < table.FreezeColumnsCount)
                host_clip_rect.Min.x = ImMath.ImClamp(column.MaxX + TABLE_BORDER_SIZE, host_clip_rect.Min.x, host_clip_rect.Max.x);

            offset_x += column.WidthGiven + table.CellSpacingX1 + table.CellSpacingX2 + table.CellPaddingX * 2.0f;
        }

        // In case the table is visible (e.g. decorations) but all columns clipped, we keep a column visible.
        // Else if give no chance to a clipper-savvy user to submit rows and therefore total contents height used by scrollbar.
        if (has_at_least_one_column_requesting_output == false)
        {
            table.Columns[table.LeftMostEnabledColumn].IsRequestOutput = true;
            table.Columns[table.LeftMostEnabledColumn].IsSkipItems = false;
        }

        // [Part 7] Detect/store when we are hovering the unused space after the right-most column (so e.g. context menus can react on it)
        // Clear Resizable flag if none of our column are actually resizable (either via an explicit _NoResize flag, either
        // because of using _WidthAuto/_WidthStretch). This will hide the resizing option from the context menu.
        float unused_x1 = ImMath.ImMax(table.WorkRect.Min.x, table.Columns[table.RightMostEnabledColumn].ClipRect.Max.x);
        if (is_hovering_table && table.HoveredColumnBody == -1)
            if (mouse_skewed_x >= unused_x1)
                table.HoveredColumnBody = (short)columns_count;
        if (has_resizable == false && (table.Flags & ImGuiTableFlags_Resizable) != 0)
            table.Flags &= ~ImGuiTableFlags_Resizable;

        table.IsActiveIdAliveBeforeTable = (g.ActiveIdIsAlive != 0);

        // [Part 8] Lock actual OuterRect/WorkRect right-most position.
        // This is done late to handle the case of fixed-columns tables not claiming more widths that they need.
        // Because of this we are careful with uses of WorkRect and InnerClipRect before this point.
        if (table.RightMostStretchedColumn != -1)
            table.Flags &= ~ImGuiTableFlags_NoHostExtendX;
        if ((table.Flags & ImGuiTableFlags_NoHostExtendX) != 0)
        {
            table.OuterRect.Max.x = table.WorkRect.Max.x = unused_x1;
            table.InnerClipRect.Max.x = ImMath.ImMin(table.InnerClipRect.Max.x, unused_x1);
        }
        table.InnerWindow.ParentWorkRect = table.WorkRect;
        table.BorderX1 = table.InnerClipRect.Min.x;
        table.BorderX2 = table.InnerClipRect.Max.x;

        // Setup window's WorkRect.Max.y for GetContentRegionAvail(). Other values will be updated in each TableBeginCell() call.
        float window_content_max_y;
        if ((table.Flags & ImGuiTableFlags_NoHostExtendY) != 0)
            window_content_max_y = table.OuterRect.Max.y;
        else
            window_content_max_y = ImMath.ImMax(table.InnerWindow.ContentRegionRect.Max.y, (table.Flags & ImGuiTableFlags_ScrollY) != 0 ? 0.0f : table.OuterRect.Max.y);
        table.InnerWindow.WorkRect.Max.y = ImMath.ImClamp(window_content_max_y - g.Style.CellPadding.y, table.InnerWindow.WorkRect.Min.y, table.InnerWindow.WorkRect.Max.y);

        // [Part 9] Allocate draw channels and setup background cliprect
        TableSetupDrawChannels(table);

        // [Part 10] Hit testing on borders
        if ((table.Flags & ImGuiTableFlags_Resizable) != 0)
            TableUpdateBorders(table);
        table_instance.LastTopHeadersRowHeight = 0.0f;
        table.IsLayoutLocked = true;
        table.IsUsingHeaders = false;

        // Highlight header
        table.HighlightColumnHeader = -1;
        if (table.IsContextPopupOpen && table.ContextPopupColumn != -1 && table.InstanceInteracted == table.InstanceCurrent)
            table.HighlightColumnHeader = table.ContextPopupColumn;
        else if ((table.Flags & ImGuiTableFlags_HighlightHoveredColumn) != 0 && table.HoveredColumnBody != -1 && table.HoveredColumnBody != columns_count && table.HoveredColumnBorder == -1)
            if (g.ActiveId == 0 || (table.IsActiveIdInTable || g.DragDropActive))
                table.HighlightColumnHeader = table.HoveredColumnBody;

        // [Part 11] Default context menu
        // - To append to this menu: you can call TableBeginContextMenuPopup()/.../EndPopup().
        // - To modify or replace this: set table->DisableDefaultContextMenu = true, then call TableBeginContextMenuPopup()/.../EndPopup().
        // - You may call TableDrawDefaultContextMenu() with selected flags to display specific sections of the default menu,
        //   e.g. TableDrawDefaultContextMenu(table, table->Flags & ~ImGuiTableFlags_Hideable) will display everything EXCEPT columns visibility options.
        if (table.DisableDefaultContextMenu == false && TableBeginContextMenuPopup(table))
        {
            TableDrawDefaultContextMenu(table, table.Flags);
            EndPopup();
        }

        // [Part 12] Sanitize and build sort specs before we have a chance to use them for display.
        // This path will only be exercised when sort specs are modified before header rows (e.g. init or visibility change)
        if (table.IsSortSpecsDirty && (table.Flags & ImGuiTableFlags_Sortable) != 0)
            TableSortSpecsBuild(table);

        // [Part 13] Setup inner window decoration size (for scrolling / nav tracking to properly take account of frozen rows/columns)
        if (table.FreezeColumnsRequest > 0)
            table.InnerWindow.DecoInnerSizeX1 = table.Columns[table.DisplayOrderToIndex[table.FreezeColumnsRequest - 1]].MaxX - table.OuterRect.Min.x; // FIXME-FROZEN
        if (table.FreezeRowsRequest > 0)
            table.InnerWindow.DecoInnerSizeY1 = table_instance.LastFrozenHeight;
        table_instance.LastFrozenHeight = 0.0f;

        ImGuiWindow inner_window = table.InnerWindow;
        ImGuiBoxSelectState bs = g.BoxSelectState;
        if (bs.Window == inner_window && bs.UnclipMode)
            TableApplyExternalUnclipRect(table, bs.UnclipRect);

        // Initial state
        if ((table.Flags & ImGuiTableFlags_NoClip) != 0)
            table.DrawSplitter.SetCurrentChannel(inner_window.DrawList, TABLE_DRAW_CHANNEL_NOCLIP);
        else
            inner_window.DrawList.PushClipRect(inner_window.InnerClipRect.Min, inner_window.InnerClipRect.Max, false); // FIXME: use table->InnerClipRect?
    }

    // When starting a BeginMultiSelect() after table has been layout we update IsRequestOutput fields.
    // C++: void ImGui::TableApplyExternalUnclipRect(ImGuiTable* table, ImRect& rect)
    public static void TableApplyExternalUnclipRect(ImGuiTable table, ImRect rect)
    {
        if (rect.IsInverted())
            return;
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
        {
            ImGuiTableColumn column = table.Columns[column_n];
            if (!column.IsRequestOutput)
                if (rect.Overlaps(new ImRect(column.MinX, table.WorkRect.Min.y, column.MaxX, float.MaxValue)))
                    column.IsRequestOutput = true;
        }
    }

    // Process hit-testing on resizing borders. Actual size change will be applied in EndTable()
    // - Set table->HoveredColumnBorder with a short delay/timer to reduce visual feedback noise.
    // C++: void ImGui::TableUpdateBorders(ImGuiTable* table)
    public static void TableUpdateBorders(ImGuiTable table)
    {
        ImGuiContext g = GImGui;
        Debug.Assert((table.Flags & ImGuiTableFlags_Resizable) != 0);

        // At this point OuterRect height may be zero or under actual final height, so we rely on temporal coherency and
        // use the final height from last frame. Because this is only affecting _interaction_ with columns, it is not
        // really problematic (whereas the actual visual will be displayed in EndTable() and using the current frame height).
        // Actual columns highlight/render will be performed in EndTable() and not be affected.
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        float hit_half_width = ImMath.ImTrunc(TABLE_RESIZE_SEPARATOR_HALF_THICKNESS * g.CurrentDpiScale);
        float hit_y1 = (table.FreezeRowsCount >= 1 ? table.OuterRect.Min.y : table.WorkRect.Min.y) + table.AngledHeadersHeight;
        float hit_y2_body = ImMath.ImMax(table.OuterRect.Max.y, hit_y1 + table_instance.LastOuterHeight - table.AngledHeadersHeight);
        float hit_y2_head = hit_y1 + table_instance.LastTopHeadersRowHeight;

        for (int order_n = 0; order_n < table.ColumnsCount; order_n++)
        {
            if (!table.EnabledMaskByDisplayOrder[order_n])
                continue;

            int column_n = table.DisplayOrderToIndex[order_n];
            ImGuiTableColumn column = table.Columns[column_n];
            if ((column.Flags & (ImGuiTableColumnFlags_NoResize | ImGuiTableColumnFlags_NoDirectResize_)) != 0)
                continue;

            // ImGuiTableFlags_NoBordersInBodyUntilResize will be honored in TableDrawBorders()
            float border_y2_hit = (table.Flags & ImGuiTableFlags_NoBordersInBody) != 0 ? hit_y2_head : hit_y2_body;
            if ((table.Flags & ImGuiTableFlags_NoBordersInBody) != 0 && table.IsUsingHeaders == false)
                continue;

            if (!column.IsVisibleX && table.LastResizedColumn != column_n)
                continue;

            ImGuiID column_id = TableGetColumnResizeID(table, column_n, table.InstanceCurrent);
            ImRect hit_rect = new ImRect(column.MaxX - hit_half_width, hit_y1, column.MaxX + hit_half_width, border_y2_hit);
            ItemAdd(hit_rect, column_id, null, ImGuiItemFlags_NoNav);
            //GetForegroundDrawList()->AddRect(hit_rect.Min, hit_rect.Max, IM_COL32(255, 0, 0, 100));

            bool hovered = false, held = false;
            bool pressed = ButtonBehavior(hit_rect, column_id, out hovered, out held, ImGuiButtonFlags_FlattenChildren | ImGuiButtonFlags_PressedOnClick | ImGuiButtonFlags_PressedOnDoubleClick | ImGuiButtonFlags_NoNavFocus);
            if (pressed && IsMouseDoubleClicked(0))
            {
                TableSetColumnWidthAutoSingle(table, column_n);
                ClearActiveID();
                held = false;
            }
            if (held)
            {
                if (table.LastResizedColumn == -1)
                    table.ResizeLockMinContentsX2 = table.RightMostEnabledColumn != -1 ? table.Columns[table.RightMostEnabledColumn].MaxX : -float.MaxValue;
                table.ResizedColumn = (short)column_n;
                table.InstanceInteracted = table.InstanceCurrent;
            }
            if ((hovered && g.HoveredIdTimer > TABLE_RESIZE_SEPARATOR_FEEDBACK_TIMER) || held)
            {
                table.HoveredColumnBorder = (short)column_n;
                SetMouseCursor(ImGuiMouseCursor_ResizeEW);
            }
        }
    }

    // C++: void ImGui::EndTable()
    public static void EndTable()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImUtil.ImAssertUserError(table != null, "EndTable() call should only be done while in BeginTable() scope!");
        if (table == null)
            return;

        // This assert would be very useful to catch a common error... unfortunately it would probably trigger in some
        // cases, and for consistency user may sometimes output empty tables (and still benefit from e.g. outer border)
        //Debug.Assert(table.IsLayoutLocked, "Table unused: never called TableNextRow(), is that the intent?");

        // If the user never got to call TableNextRow() or TableNextColumn(), we call layout ourselves to ensure all our
        // code paths are consistent (instead of just hoping that TableBegin/TableEnd will work), get borders drawn, etc.
        if (!table.IsLayoutLocked)
            TableUpdateLayout(table);

        int flags = table.Flags;
        ImGuiWindow inner_window = table.InnerWindow;
        ImGuiWindow outer_window = table.OuterWindow;
        ImGuiTableTempData temp_data = table.TempData;
        Debug.Assert(inner_window == g.CurrentWindow && inner_window.ID == temp_data.WindowID);
        Debug.Assert(outer_window == inner_window || outer_window == inner_window.ParentWindow);

        if (table.IsInsideRow)
            TableEndRow(table);

        // Context menu in columns body
        if ((flags & ImGuiTableFlags_ContextMenuInBody) != 0)
            if (table.HoveredColumnBody != -1 && !IsAnyItemHovered() && IsMouseReleased(ImGuiMouseButton_Right))
                TableOpenContextMenu(table.HoveredColumnBody);

        // Finalize table height
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        inner_window.DC.PrevLineSize = temp_data.HostBackupPrevLineSize;
        inner_window.DC.CurrLineSize = temp_data.HostBackupCurrLineSize;
        inner_window.DC.CursorMaxPos = temp_data.HostBackupCursorMaxPos;
        float inner_content_max_y = ImMath.ImCeil(table.RowPosY2); // Rounding final position is important as we currently don't round row height ('Demo->Tables->Outer Size' demo uses non-integer heights)
        Debug.Assert(table.RowPosY2 == inner_window.DC.CursorPos.y);
        if (inner_window != outer_window)
            inner_window.DC.CursorMaxPos.y = inner_content_max_y;
        else if ((flags & ImGuiTableFlags_NoHostExtendY) == 0)
            table.OuterRect.Max.y = table.InnerRect.Max.y = ImMath.ImMax(table.OuterRect.Max.y, inner_content_max_y); // Patch OuterRect/InnerRect height
        table.WorkRect.Max.y = ImMath.ImMax(table.WorkRect.Max.y, table.OuterRect.Max.y);
        table_instance.LastOuterHeight = table.OuterRect.GetHeight();

        // Setup inner scrolling range
        // FIXME: This ideally should be done earlier, in BeginTable() SetNextWindowContentSize call, just like writing to inner_window->DC.CursorMaxPos.y,
        // but since the later is likely to be impossible to do we'd rather update both axes together.
        if ((table.Flags & ImGuiTableFlags_ScrollX) != 0)
        {
            float outer_padding_for_border = (table.Flags & ImGuiTableFlags_BordersOuterV) != 0 ? TABLE_BORDER_SIZE : 0.0f;
            float max_pos_x = inner_window.DC.CursorMaxPos.x;
            if (table.RightMostEnabledColumn != -1)
                max_pos_x = ImMath.ImMax(max_pos_x, table.Columns[table.RightMostEnabledColumn].WorkMaxX + table.CellPaddingX + table.OuterPaddingX - outer_padding_for_border);
            if (table.ResizedColumn != -1)
                max_pos_x = ImMath.ImMax(max_pos_x, table.ResizeLockMinContentsX2);
            inner_window.DC.CursorMaxPos.x = max_pos_x + table.TempData.AngledHeadersExtraWidth;
        }

        // Pop clipping rect
        if ((flags & ImGuiTableFlags_NoClip) == 0)
            inner_window.DrawList.PopClipRect();
        {
            ImVec4 cr = inner_window.DrawList._ClipRectStack.back();
            inner_window.ClipRect = new ImRect(cr.x, cr.y, cr.z, cr.w); // C++: inner_window->ClipRect = inner_window->DrawList->_ClipRectStack.back();
        }

        // Draw borders
        if ((flags & ImGuiTableFlags_Borders) != 0)
            TableDrawBorders(table);

#if false
        // Strip out dummy channel draw calls
        // We have no way to prevent user submitting direct ImDrawList calls into a hidden column (but ImGui:: calls will be clipped out)
        // Pros: remove draw calls which will have no effect. since they'll have zero-size cliprect they may be early out anyway.
        // Cons: making it harder for users watching metrics/debugger to spot the wasted vertices.
        if (table->DummyDrawChannel != (ImGuiTableColumnIdx)-1)
        {
            ImDrawChannel* dummy_channel = &table->DrawSplitter._Channels[table->DummyDrawChannel];
            dummy_channel->_CmdBuffer.resize(0);
            dummy_channel->_IdxBuffer.resize(0);
        }
#endif

        // Flatten channels and merge draw calls
        ImDrawListSplitter splitter = table.DrawSplitter;
        splitter.SetCurrentChannel(inner_window.DrawList, 0);
        if ((table.Flags & ImGuiTableFlags_NoClip) == 0)
            TableMergeDrawChannels(table);
        splitter.Merge(inner_window.DrawList);

        // Update ColumnsAutoFitWidth to get us ahead for host using our size to auto-resize without waiting for next BeginTable()
        float auto_fit_width_for_fixed = 0.0f;
        float auto_fit_width_for_stretched = 0.0f;
        float auto_fit_width_for_stretched_min = 0.0f;
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
            if (table.EnabledMaskByIndex[column_n])
            {
                ImGuiTableColumn column = table.Columns[column_n];
                float column_width_request = ((column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0 && (column.Flags & ImGuiTableColumnFlags_NoResize) == 0) ? column.WidthRequest : TableGetColumnWidthAuto(table, column);
                if ((column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0)
                    auto_fit_width_for_fixed += column_width_request;
                else
                    auto_fit_width_for_stretched += column_width_request;
                if ((column.Flags & ImGuiTableColumnFlags_WidthStretch) != 0 && (column.Flags & ImGuiTableColumnFlags_NoResize) != 0)
                    auto_fit_width_for_stretched_min = ImMath.ImMax(auto_fit_width_for_stretched_min, column_width_request / (column.StretchWeight / table.ColumnsStretchSumWeights));
            }
        float width_spacings = (table.OuterPaddingX * 2.0f) + (table.CellSpacingX1 + table.CellSpacingX2) * (table.ColumnsEnabledCount - 1);
        table.ColumnsAutoFitWidth = width_spacings + (table.CellPaddingX * 2.0f) * table.ColumnsEnabledCount + auto_fit_width_for_fixed + ImMath.ImMax(auto_fit_width_for_stretched, auto_fit_width_for_stretched_min);

        // Update scroll
        if ((table.Flags & ImGuiTableFlags_ScrollX) == 0 && inner_window != outer_window)
        {
            inner_window.Scroll.x = 0.0f;
        }
        else if (table.LastResizedColumn != -1 && table.ResizedColumn == -1 && inner_window.ScrollbarX && table.InstanceInteracted == table.InstanceCurrent)
        {
            // When releasing a column being resized, scroll to keep the resulting column in sight
            float neighbor_width_to_keep_visible = table.MinColumnWidth + table.CellPaddingX * 2.0f;
            ImGuiTableColumn column = table.Columns[table.LastResizedColumn];
            if (column.MaxX < table.InnerClipRect.Min.x)
                SetScrollFromPosX(inner_window, column.MaxX - inner_window.Pos.x - neighbor_width_to_keep_visible, 1.0f);
            else if (column.MaxX > table.InnerClipRect.Max.x)
                SetScrollFromPosX(inner_window, column.MaxX - inner_window.Pos.x + neighbor_width_to_keep_visible, 1.0f);
        }

        // Apply resizing/dragging at the end of the frame
        if (table.ResizedColumn != -1 && table.InstanceCurrent == table.InstanceInteracted)
        {
            ImGuiTableColumn column = table.Columns[table.ResizedColumn];
            float new_x2 = g.IO.MousePos.x - g.ActiveIdClickOffset.x + ImMath.ImTrunc(TABLE_RESIZE_SEPARATOR_HALF_THICKNESS * g.CurrentDpiScale);
            float new_width = ImMath.ImTrunc(new_x2 - column.MinX - table.CellSpacingX1 - table.CellPaddingX * 2.0f);
            table.ResizedColumnNextWidth = new_width;
        }

        table.IsActiveIdInTable = (g.ActiveIdIsAlive != 0 && table.IsActiveIdAliveBeforeTable == false);

        // Pop from id stack
        ImUtil.ImAssertUserError(inner_window.IDStack.back() == table_instance.TableInstanceID, "Mismatching PushID/PopID!");
        ImUtil.ImAssertUserError(outer_window.DC.ItemWidthStack.Size >= temp_data.HostBackupItemWidthStackSize, "Too many PopItemWidth!");
        if (table.InstanceCurrent > 0)
            PopID();
        PopID();

        // Restore window data that we modified
        ImVec2 backup_outer_max_pos = outer_window.DC.CursorMaxPos;
        inner_window.WorkRect = temp_data.HostBackupWorkRect;
        inner_window.ParentWorkRect = temp_data.HostBackupParentWorkRect;
        inner_window.SkipItems = table.HostSkipItems;
        outer_window.DC.CursorPos = table.OuterRect.Min;
        outer_window.DC.ItemWidth = temp_data.HostBackupItemWidth;
        outer_window.DC.ItemWidthStack.Size = temp_data.HostBackupItemWidthStackSize;
        outer_window.DC.ColumnsOffset = temp_data.HostBackupColumnsOffset;

        // Layout in outer window
        // (FIXME: To allow auto-fit and allow desirable effect of SameLine() we dissociate 'used' vs 'ideal' size by overriding
        // CursorPosPrevLine and CursorMaxPos manually. That should be a more general layout feature, see same problem e.g. #3414)
        if (inner_window != outer_window)
        {
            short backup_nav_layers_active_mask = inner_window.DC.NavLayersActiveMask;
            inner_window.DC.NavLayersActiveMask |= (short)(1 << table.NavLayer); // So empty table don't appear to navigate differently.
            g.CurrentTable = null; // To avoid error recovery recursing
            EndChild();
            g.CurrentTable = table;
            inner_window.DC.NavLayersActiveMask = backup_nav_layers_active_mask;
        }
        else
        {
            inner_window.DC.TreeDepth--;
            ItemSize(table.OuterRect.GetSize());
            ItemAdd(table.OuterRect, 0);
        }

        // Override declared contents width/height to enable auto-resize while not needlessly adding a scrollbar
        if ((table.Flags & ImGuiTableFlags_NoHostExtendX) != 0)
        {
            // FIXME-TABLE: Could we remove this section?
            // ColumnsAutoFitWidth may be one frame ahead here since for Fixed+NoResize is calculated from latest contents
            Debug.Assert((table.Flags & ImGuiTableFlags_ScrollX) == 0);
            outer_window.DC.CursorMaxPos.x = ImMath.ImMax(backup_outer_max_pos.x, table.OuterRect.Min.x + table.ColumnsAutoFitWidth);
        }
        else if (temp_data.UserOuterSize.x <= 0.0f)
        {
            // Some references for this: #7651 + tests "table_reported_size", "table_reported_size_outer" equivalent Y block, #9352
            // - FIXME-TABLE: Would make sense to pre-compute expected scrollbar visibility/sizes to generally save a frame of feedback? See broken test in 'table_reported_size_outer'
            float outer_content_max_x = table.OuterRect.Min.x + table.ColumnsAutoFitWidth;
            float decoration_size = table.TempData.AngledHeadersExtraWidth + ((inner_window != outer_window) ? inner_window.ScrollbarSizes.x : 0.0f);
            outer_window.DC.IdealMaxPos.x = ImMath.ImMax(outer_window.DC.IdealMaxPos.x, outer_content_max_x + decoration_size - temp_data.UserOuterSize.x);
            outer_window.DC.CursorMaxPos.x = ImMath.ImMax(backup_outer_max_pos.x, ImMath.ImMin(table.OuterRect.Max.x, outer_content_max_x + decoration_size));
        }
        else
        {
            outer_window.DC.CursorMaxPos.x = ImMath.ImMax(backup_outer_max_pos.x, table.OuterRect.Max.x);
        }
        if (temp_data.UserOuterSize.y <= 0.0f)
        {
            // (same comment as above)
            float outer_content_size_y = (inner_window == outer_window) ? (inner_content_max_y - table.InnerRect.Min.y) : (inner_content_max_y - inner_window.DC.CursorStartPos.y);
            float outer_content_max_y = table.OuterRect.Min.y + outer_content_size_y;
            float decoration_size = (inner_window != outer_window ? inner_window.ScrollbarSizes.y : 0.0f);
            outer_window.DC.IdealMaxPos.y = ImMath.ImMax(outer_window.DC.IdealMaxPos.y, outer_content_max_y + decoration_size - temp_data.UserOuterSize.y);
            outer_window.DC.CursorMaxPos.y = ImMath.ImMax(backup_outer_max_pos.y, ImMath.ImMin(table.OuterRect.Max.y, outer_content_max_y + decoration_size));
        }
        else
        {
            // OuterRect.Max.y may already have been pushed downward from the initial value (unless ImGuiTableFlags_NoHostExtendY is set)
            outer_window.DC.CursorMaxPos.y = ImMath.ImMax(backup_outer_max_pos.y, table.OuterRect.Max.y);
        }

        // Save settings
        if (table.IsSettingsDirty)
            TableSaveSettings(table);
        table.IsInitializing = false;

        // Clear or restore current table, if any
        Debug.Assert(g.CurrentWindow == outer_window && g.CurrentTable == table);
        Debug.Assert(g.TablesTempDataStacked > 0);
        temp_data = (--g.TablesTempDataStacked > 0) ? g.TablesTempData[g.TablesTempDataStacked - 1] : null;
        g.CurrentTable = (temp_data != null && temp_data.WindowID == outer_window.ID) ? g.Tables.GetByIndex(temp_data.TableIndex) : null;
        if (g.CurrentTable != null)
        {
            g.CurrentTable.TempData = temp_data;
            g.CurrentTable.DrawSplitter = temp_data.DrawSplitter;
        }
        outer_window.DC.CurrentTableIdx = g.CurrentTable != null ? g.Tables.GetIndex(g.CurrentTable) : -1;
        NavUpdateCurrentWindowIsScrollPushableX();
    }

    // Called in TableUpdateLayout() when initializing/after loading settings.
    // 'init_mask' specify which fields to initialize.
    // C++: void ImGui::TableInitColumnDefaults(ImGuiTable* table, ImGuiTableColumn* column, ImGuiTableColumnFlags init_mask)
    public static void TableInitColumnDefaults(ImGuiTable table, ImGuiTableColumn column, ImGuiTableColumnFlags init_mask)
    {
        int flags = column.Flags;
        if ((init_mask & ImGuiTableFlags_Resizable) != 0)
        {
            float init_width_or_weight = column.InitStretchWeightOrWidth;
            column.WidthRequest = ((flags & ImGuiTableColumnFlags_WidthFixed) != 0 && init_width_or_weight > 0.0f) ? init_width_or_weight : -1.0f;
            column.StretchWeight = (init_width_or_weight > 0.0f && (flags & ImGuiTableColumnFlags_WidthStretch) != 0) ? init_width_or_weight : -1.0f;
            if (init_width_or_weight > 0.0f) // Disable auto-fit if an explicit width/weight has been specified
                column.AutoFitQueue = 0x00;
        }
        if ((init_mask & ImGuiTableFlags_Reorderable) != 0)
            column.DisplayOrder = (short)((table.Flags & ImGuiTableFlags_Reorderable) != 0 ? -1 : Array.IndexOf(table.Columns, column));
        if ((init_mask & ImGuiTableFlags_Hideable) != 0)
        {
            bool enabled = (flags & ImGuiTableColumnFlags_DefaultHide) == 0;
            column.IsUserEnabled = column.IsUserEnabledNextFrame = enabled;
        }
        if ((init_mask & ImGuiTableFlags_Sortable) != 0)
        {
            // Multiple columns using _DefaultSort will be reassigned unique SortOrder values when building the sort specs.
            column.SortOrder = (short)((flags & ImGuiTableColumnFlags_DefaultSort) != 0 ? 0 : -1);
            column.SortDirection = (byte)((flags & ImGuiTableColumnFlags_DefaultSort) != 0 ? (((flags & ImGuiTableColumnFlags_PreferSortDescending) != 0) ? ImGuiSortDirection_Descending : ImGuiSortDirection_Ascending) : ImGuiSortDirection_None);
        }
    }

    // See "COLUMNS SIZING POLICIES" comments at the top of imgui_tables.cpp
    // If (init_width_or_weight <= 0.0f) it is ignored
    // C++: static void TableSetupColumnApply(ImGuiTable* table, int idx, ImGuiID id, ImS16 name_offset, ImGuiTableColumnFlags flags, float init_width_or_weight, ImGuiID user_data)
    static void TableSetupColumnApply(ImGuiTable table, int idx, ImGuiID id, short name_offset, ImGuiTableColumnFlags flags, float init_width_or_weight, ImGuiID user_data)
    {
        ImGuiTableColumn column = table.Columns[idx];

        // Assert when passing a width or weight if policy is entirely left to default, to avoid storing width into weight and vice-versa.
        // Give a grace to users of ImGuiTableFlags_ScrollX.
        if (table.IsDefaultSizingPolicy && (flags & ImGuiTableColumnFlags_WidthMask_) == 0 && (flags & ImGuiTableFlags_ScrollX) == 0)
        {
            bool ok = init_width_or_weight <= 0.0f;
            ImUtil.ImAssertUserError(ok, "TableSetupColumn(): can only specify width/weight if sizing policy is set explicitly in either Table or Column.");
            if (!ok)
                return;
        }

        // When passing a width automatically enforce WidthFixed policy
        // (whereas TableSetupColumnFlags would default to WidthAuto if table is not resizable)
        if ((flags & ImGuiTableColumnFlags_WidthMask_) == 0 && init_width_or_weight > 0.0f)
            if ((table.Flags & ImGuiTableFlags_SizingMask_) == ImGuiTableFlags_SizingFixedFit || (table.Flags & ImGuiTableFlags_SizingMask_) == ImGuiTableFlags_SizingFixedSame)
                flags |= ImGuiTableColumnFlags_WidthFixed;
        if ((flags & ImGuiTableColumnFlags_AngledHeader) != 0)
        {
            flags |= ImGuiTableColumnFlags_NoHeaderLabel;
            table.AngledHeadersCount++;
        }

        TableSetupColumnFlags(table, column, flags);
        column.ID = id;
        column.UserData = user_data;
        column.NameOffset = name_offset;
        column.InitStretchWeightOrWidth = init_width_or_weight;
    }

    // C++: void ImGui::TableSetupColumn(const char* label, ImGuiTableColumnFlags flags, float init_width_or_weight, ImGuiID user_data)
    public static void TableSetupColumn(string label, ImGuiTableColumnFlags flags = 0, float init_width_or_weight = 0.0f, ImGuiID user_data = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImUtil.ImAssertUserError(table != null, "Call should only be done while in BeginTable() scope!");
        if (table == null)
            return;
        ImUtil.ImAssertUserError(table.DeclColumnsCount < table.ColumnsCount, "TableSetupColumn(): called too many times!");
        if (table.DeclColumnsCount >= table.ColumnsCount)
            return;
        ImUtil.ImAssertUserError(table.IsLayoutLocked == false, "TableSetupColumn(): need to call before first row!"); // Table layout is locked when submitting a row or when calling BeginMultiSelect() with box-select.
        if (table.IsLayoutLocked)
            return;
        Debug.Assert((flags & ImGuiTableColumnFlags_StatusMask_) == 0, "Illegal to pass StatusMask values to TableSetupColumn()");

        // Store name (append with zero-terminator in contiguous buffer)
        // FIXME: If we recorded the number of \n in names we could compute header row height
        short name_offset = -1;
        if (label != null && label.Length > 0)
        {
            name_offset = (short)table.ColumnsNames.size();
            table.ColumnsNames.append(label, label.Length + 1); // C++: table->ColumnsNames.append(label, label + ImStrlen(label) + 1)
        }
        ImGuiID column_id = (label != null && label.Length > 0) ? ImGuiHash.ImHashStr(label, 0) : 0;

        // When ID changed or a column moved: defer the request until layout where we will process full reconcile.
        int column_idx = table.DeclColumnsCount++;
        ImGuiTableColumn column = table.Columns[column_idx];

        // If topology change goes into reconcile mode
        if (table.IsReconcileMode == false && column.ID != column_id)
        {
            table.IsReconcileMode = true;
            table.TempData.ReconcileColumnsRequests.reserve(table.ColumnsCount - column_idx);
        }

        // Fast/common path
        if (table.IsReconcileMode == false)
        {
            TableSetupColumnApply(table, column_idx, column_id, name_offset, flags, init_width_or_weight, user_data);
            column.IsNeedReconcileSrc = column.IsNeedReconcileDst = false;
            if (column_idx < table.TempData.OldColumnsData.Length)
                table.TempData.OldColumnsData[column_idx].IsNeedReconcileSrc = false;
            return;
        }

        // Reconcile path: defer applying data to TableUpdateLayout() -> TableReconcileMovedColumns() -> TableSetupColumnApply().
        // On column count change: search the previous columns:
        // - the live Columns[] array is truncated on shrink, so a moved column may only exist in old data.
        // - the live Columns[] array may already be partially reset.
        table.TempData.ReconcileColumnsRequests.push_back(new ImGuiTableReconcileColumnData());
        ImGuiTableReconcileColumnData reconcile_data = table.TempData.ReconcileColumnsRequests.back();
        reconcile_data.ID = column_id;
        reconcile_data.NameOffset = name_offset;
        reconcile_data.Flags = flags;
        reconcile_data.InitWidthOrWeight = init_width_or_weight;
        reconcile_data.UserData = user_data;
        reconcile_data.ColumnNewIdx = (short)column_idx;
        reconcile_data.ColumnOldIdx = -1;
        column.NameOffset = name_offset; // Allow TableGetColumnName() to work before layout
        column.IsNeedReconcileSrc = column.IsNeedReconcileDst = true;
    }

    // NB: This was written to be similar to the logic in TableLoadSettingsForColumns().
    // C++: void ImGui::TableReconcileColumns(ImGuiTable* table)
    public static void TableReconcileColumns(ImGuiTable table)
    {
        ImGuiTableTempData temp_data = table.TempData;
        // IMGUI_DEBUG_LOG_TABLE("[table] Reconcile columns for table 0x%08X\n", table->ID);  // omitted

        ImGuiTableColumn[] dst_columns = table.Columns;
        ImGuiTableColumn[] src_columns = (temp_data.OldColumnsData.Length == 0) ? table.Columns : temp_data.OldColumnsData;

        // Find matches for named columns.
        int matches = 0;
        ImVector<ImGuiTableReconcileColumnData> reconcile_requests = temp_data.ReconcileColumnsRequests;
        for (int req_i = 0; req_i < reconcile_requests.Size; req_i++)
        {
            ImGuiTableReconcileColumnData reconcile_data = reconcile_requests[req_i];
            if (reconcile_data.ID != 0)
            {
                for (int src_i = 0; src_i < src_columns.Length; src_i++)
                {
                    ImGuiTableColumn src_column = src_columns[src_i];
                    if (src_column.ID == reconcile_data.ID && src_column.IsNeedReconcileSrc)
                    {
                        ImGuiTableColumn dst_column = dst_columns[reconcile_data.ColumnNewIdx];
                        Debug.Assert(src_column.IsNeedReconcileSrc && dst_column.IsNeedReconcileDst);
                        src_column.IsNeedReconcileSrc = false;
                        dst_column.IsNeedReconcileDst = false;
                        reconcile_data.ColumnOldIdx = (short)src_i;     // C++: src_columns.index_from_ptr(&src_column)
                        TableColumnAssign(reconcile_data.ColumnOldData, src_column); // C++: reconcile_data.ColumnOldData = src_column (by value)
                        matches++;
                        break;
                    }
                }
            }
        }

        // Remaining entries are matched sequentially.
        int dst_idx = 0; // index in reconcile array
        if (matches != reconcile_requests.Size)
        {
            for (int src_i = 0; src_i < src_columns.Length; src_i++)
            {
                ImGuiTableColumn src_column = src_columns[src_i];
                if (!src_column.IsNeedReconcileSrc)
                    continue;
                while (dst_idx < reconcile_requests.Size && reconcile_requests[dst_idx].ColumnOldIdx != -1)
                    dst_idx++;
                if (dst_idx == reconcile_requests.Size)
                    break;
                ImGuiTableReconcileColumnData reconcile_data = reconcile_requests[dst_idx];
                ImGuiTableColumn dst_column = dst_columns[reconcile_data.ColumnNewIdx];
                Debug.Assert(src_column.IsNeedReconcileSrc && dst_column.IsNeedReconcileDst);
                src_column.IsNeedReconcileSrc = false;
                dst_column.IsNeedReconcileDst = false;
                reconcile_data.ColumnOldIdx = (short)src_i;     // C++: src_columns.index_from_ptr(&src_column)
                TableColumnAssign(reconcile_data.ColumnOldData, src_column); // C++: reconcile_data.ColumnOldData = src_column (by value)
            }
        }

        // Apply in the final pass. Because it is possible that src_columns == table->Columns we went through a temporary copy.
        for (int req_i = 0; req_i < reconcile_requests.Size; req_i++)
        {
            ImGuiTableReconcileColumnData reconcile_data = reconcile_requests[req_i];
            TableColumnAssign(table.Columns[reconcile_data.ColumnNewIdx], reconcile_data.ColumnOldData); // When old column was not found clear anyway with default-constructed data (which will set IsJustCreated=true)
            TableSetupColumnApply(table, reconcile_data.ColumnNewIdx, reconcile_data.ID, reconcile_data.NameOffset, reconcile_data.Flags, reconcile_data.InitWidthOrWeight, reconcile_data.UserData);
            // IMGUI_DEBUG_LOG_TABLE("[table] - old %d -> new %d \"%s\"\n", ...) omitted // Log at the end so NameOffset was copied.
        }
        TableFixDisplayOrder(table);
        table.IsSettingsDirty = true; // FIXME-RECONCILE: Necessary?
        table.IsReconcileMode = false;
        reconcile_requests.resize(0); // GC-ed once in NewFrame()
    }

    // [Public]
    // C++: void ImGui::TableSetupScrollFreeze(int columns, int rows)
    public static void TableSetupScrollFreeze(int columns, int rows)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImUtil.ImAssertUserError(table != null, "Call should only be done while in BeginTable() scope!");
        if (table == null)
            return;
        Debug.Assert(table.IsLayoutLocked == false, "TableSetupColumn(): need to call before first row!");
        Debug.Assert(columns >= 0 && columns < IMGUI_TABLE_MAX_COLUMNS);
        Debug.Assert(rows >= 0 && rows < 128); // Arbitrary limit

        table.FreezeColumnsRequest = (short)((table.Flags & ImGuiTableFlags_ScrollX) != 0 ? ImMath.ImMin(columns, table.ColumnsCount) : 0);
        table.FreezeColumnsCount = (short)((table.InnerWindow.Scroll.x != 0.0f) ? table.FreezeColumnsRequest : 0);
        table.FreezeRowsRequest = (short)((table.Flags & ImGuiTableFlags_ScrollY) != 0 ? rows : 0);
        table.FreezeRowsCount = (short)((table.InnerWindow.Scroll.y != 0.0f) ? table.FreezeRowsRequest : 0);
        table.IsUnfrozenRows = (table.FreezeRowsCount == 0); // Make sure this is set before TableUpdateLayout() so ImGuiListClipper can benefit from it.b
    }

    //-----------------------------------------------------------------------------
    // [SECTION] Tables: Simple accessors (imgui_tables.cpp 1870-2040)
    //-----------------------------------------------------------------------------

    // C++: int ImGui::TableGetColumnCount()
    public static int TableGetColumnCount()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        return table != null ? table.ColumnsCount : 0;
    }

    // C++: const char* ImGui::TableGetColumnName(int column_n)
    public static string TableGetColumnName(int column_n = -1)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return null;
        if (column_n < 0)
            column_n = table.CurrentColumn;
        return TableGetColumnName(table, column_n);
    }

    // C++: const char* ImGui::TableGetColumnName(const ImGuiTable* table, int column_n)
    public static string TableGetColumnName(ImGuiTable table, int column_n)
    {
        if (table.IsLayoutLocked == false && column_n >= table.DeclColumnsCount)
            return ""; // NameOffset is invalid at this point
        ImGuiTableColumn column = table.Columns[column_n];
        if (column.NameOffset == -1)
            return "";
        // C++: return &table->ColumnsNames.Buf[column->NameOffset];
        int end = column.NameOffset;
        while (table.ColumnsNames.Buf.Data[end] != 0)
            end++;
        return System.Text.Encoding.UTF8.GetString(table.ColumnsNames.Buf.Data, column.NameOffset, end - column.NameOffset);
    }

    // Change user accessible enabled/disabled state of a column (often perceived as "showing/hiding" from users point of view)
    // Note that end-user can use the context menu to change this themselves (right-click in headers, or right-click in columns body with ImGuiTableFlags_ContextMenuInBody)
    // - Require table to have the ImGuiTableFlags_Hideable flag because we are manipulating user accessible state.
    // - Request will be applied during next layout, which happens on the first call to TableNextRow() after BeginTable().
    // - For the getter you can test (TableGetColumnFlags() & ImGuiTableColumnFlags_IsEnabled) != 0.
    // - Alternative: the ImGuiTableColumnFlags_Disabled is an overriding/master disable flag which will also hide the column from context menu.
    // C++: void ImGui::TableSetColumnEnabled(int column_n, bool enabled)
    public static void TableSetColumnEnabled(int column_n, bool enabled)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImUtil.ImAssertUserError(table != null, "Call should only be done while in BeginTable() scope!");
        if (table == null)
            return;
        Debug.Assert((table.Flags & ImGuiTableFlags_Hideable) != 0); // See comments above
        if (column_n < 0)
            column_n = table.CurrentColumn;
        Debug.Assert(column_n >= 0 && column_n < table.ColumnsCount);
        ImGuiTableColumn column = table.Columns[column_n];
        column.IsUserEnabledNextFrame = enabled;
    }

    // We allow querying for an extra column in order to poll the IsHovered state of the right-most section
    // C++: ImGuiTableColumnFlags ImGui::TableGetColumnFlags(int column_n)
    public static ImGuiTableColumnFlags TableGetColumnFlags(int column_n = -1)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return ImGuiTableColumnFlags_None;
        if (column_n < 0)
            column_n = table.CurrentColumn;
        if (column_n == table.ColumnsCount)
            return (table.HoveredColumnBody == column_n) ? ImGuiTableColumnFlags_IsHovered : ImGuiTableColumnFlags_None;
        return table.Columns[column_n].Flags;
    }

    // Return the cell rectangle based on currently known height.
    // - Important: we generally don't know our row height until the end of the row, so Max.y will be incorrect in many situations.
    //   The only case where this is correct is if we provided a min_row_height to TableNextRow() and don't go below it, or in TableEndRow() when we locked that height.
    // - Important: if ImGuiTableFlags_PadOuterX is set but ImGuiTableFlags_PadInnerX is not set, the outer-most left and right
    //   columns report a small offset so their CellBgRect can extend up to the outer border.
    //   FIXME: But the rendering code in TableEndRow() nullifies that with clamping required for scrolling.
    // C++: ImRect ImGui::TableGetCellBgRect(const ImGuiTable* table, int column_n)
    public static ImRect TableGetCellBgRect(ImGuiTable table, int column_n)
    {
        ImGuiTableColumn column = table.Columns[column_n];
        float x1 = column.MinX;
        float x2 = column.MaxX;
        //if (column->PrevEnabledColumn == -1)
        //    x1 -= table->OuterPaddingX;
        //if (column->NextEnabledColumn == -1)
        //    x2 += table->OuterPaddingX;
        x1 = ImMath.ImMax(x1, table.WorkRect.Min.x);
        x2 = ImMath.ImMin(x2, table.WorkRect.Max.x);
        return new ImRect(x1, table.RowPosY1, x2, table.RowPosY2);
    }

    // Return the resizing ID for the right-side of the given column.
    // C++: ImGuiID ImGui::TableGetColumnResizeID(ImGuiTable* table, int column_n, int instance_no)
    public static ImGuiID TableGetColumnResizeID(ImGuiTable table, int column_n, int instance_no = 0)
    {
        Debug.Assert(column_n >= 0 && column_n < table.ColumnsCount);
        ImGuiID instance_id = TableGetInstanceID(table, instance_no);
        return instance_id + 1 + (uint)column_n; // FIXME: #6140: still not ideal
    }

    // Return -1 when table is not hovered. return columns_count if hovering the unused space at the right of the right-most visible column.
    // C++: int ImGui::TableGetHoveredColumn()
    public static int TableGetHoveredColumn()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return -1;
        return table.HoveredColumnBody;
    }

    // Return -1 when table is not hovered. Return maxrow+1 if in table but below last submitted row.
    // *IMPORTANT* Unlike TableGetHoveredColumn(), this has a one frame latency in updating the value.
    // This difference with is the reason why this is not public yet.
    // C++: int ImGui::TableGetHoveredRow()
    public static int TableGetHoveredRow()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return -1;
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        return table_instance.HoveredRowLast;
    }

    // C++: void ImGui::TableSetBgColor(ImGuiTableBgTarget target, ImU32 color, int column_n)
    public static void TableSetBgColor(ImGuiTableBgTarget target, ImU32 color, int column_n = -1)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImUtil.ImAssertUserError(table != null, "Call should only be done while in BeginTable() scope!");
        if (table == null)
            return;
        Debug.Assert(target != ImGuiTableBgTarget_None);

        if (color == ImGuiCore.IM_COL32_DISABLE)
            color = 0;

        // We cannot draw neither the cell or row background immediately as we don't know the row height at this point in time.
        switch (target)
        {
            case ImGuiTableBgTarget_CellBg:
            {
                if (table.RowPosY1 > table.InnerClipRect.Max.y) // Discard
                    return;
                if (column_n == -1)
                    column_n = table.CurrentColumn;
                if (!table.VisibleMaskByIndex[column_n])
                    return;
                if (table.RowCellDataCurrent < 0 || table.RowCellData[table.RowCellDataCurrent].Column != column_n)
                    table.RowCellDataCurrent++;
                ImGuiTableCellData cell_data = table.RowCellData[table.RowCellDataCurrent];
                cell_data.BgColor = color;
                cell_data.Column = (short)column_n;
                table.RowCellData[table.RowCellDataCurrent] = cell_data;
                break;
            }
            case ImGuiTableBgTarget_RowBg0:
            case ImGuiTableBgTarget_RowBg1:
            {
                if (table.RowPosY1 > table.InnerClipRect.Max.y) // Discard
                    return;
                Debug.Assert(column_n == -1);
                int bg_idx = (target == ImGuiTableBgTarget_RowBg1) ? 1 : 0;
                table.RowBgColor[bg_idx] = color;
                break;
            }
            default:
                Debug.Assert(false);
                break;
        }
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Row changes (imgui_tables.cpp 2040-2269)
    //-------------------------------------------------------------------------

    // [Public] Note: for row coloring we use ->RowBgColorCounter which is the same value without counting header rows
    // C++: int ImGui::TableGetRowIndex()
    public static int TableGetRowIndex()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return 0;
        return table.CurrentRow;
    }

    // [Public] Starts into the first cell of a new row
    // C++: void ImGui::TableNextRow(ImGuiTableRowFlags row_flags, float row_min_height)
    public static void TableNextRow(ImGuiTableRowFlags row_flags = 0, float row_min_height = 0.0f)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;

        if (!table.IsLayoutLocked)
            TableUpdateLayout(table);
        if (table.IsInsideRow)
            TableEndRow(table);

        table.LastRowFlags = table.RowFlags;
        table.RowFlags = row_flags;
        table.RowCellPaddingY = g.Style.CellPadding.y;
        table.RowMinHeight = row_min_height;
        TableBeginRow(table);

        // We honor min_row_height requested by user, but cannot guarantee per-row maximum height,
        // because that would essentially require a unique clipping rectangle per-cell.
        table.RowPosY2 += table.RowCellPaddingY * 2.0f;
        table.RowPosY2 = ImMath.ImMax(table.RowPosY2, table.RowPosY1 + row_min_height);

        // Disable output until user calls TableNextColumn()
        table.InnerWindow.SkipItems = true;
    }

    // [Internal] Only called by TableNextRow()
    // C++: void ImGui::TableBeginRow(ImGuiTable* table)
    public static void TableBeginRow(ImGuiTable table)
    {
        ImGuiWindow window = table.InnerWindow;
        Debug.Assert(!table.IsInsideRow);

        // New row
        table.CurrentRow++;
        table.CurrentColumn = -1;
        table.RowBgColor[0] = table.RowBgColor[1] = ImGuiCore.IM_COL32_DISABLE;
        table.RowCellDataCurrent = -1;
        table.IsInsideRow = true;

        // Begin frozen rows
        float next_y1 = table.RowPosY2;
        if (table.CurrentRow == 0 && table.FreezeRowsCount > 0)
        {
            window.DC.CursorPos.y = table.OuterRect.Min.y;
            next_y1 = window.DC.CursorPos.y;
        }

        table.RowPosY1 = table.RowPosY2 = next_y1;
        table.RowTextBaseline = 0.0f;
        table.RowIndentOffsetX = window.DC.Indent.x - table.HostIndentX; // Lock indent

        window.DC.PrevLineTextBaseOffset = 0.0f;
        window.DC.CursorPosPrevLine = new ImVec2(window.DC.CursorPos.x, window.DC.CursorPos.y + table.RowCellPaddingY); // This allows users to call SameLine() to share LineSize between columns.
        window.DC.PrevLineSize = window.DC.CurrLineSize = new ImVec2(0.0f, 0.0f); // This allows users to call SameLine() to share LineSize between columns, and to call it from first column too.
        window.DC.IsSameLine = window.DC.IsSetPos = false;
        window.DC.CursorMaxPos.y = next_y1;

        // Making the header BG color non-transparent will allow us to overlay it multiple times when handling smooth dragging.
        if ((table.RowFlags & ImGuiTableRowFlags_Headers) != 0)
        {
            TableSetBgColor(ImGuiTableBgTarget_RowBg0, GetColorU32(ImGuiCol_TableHeaderBg));
            if (table.CurrentRow == 0)
                table.IsUsingHeaders = true;
        }
    }

    // [Internal] Called by TableNextRow()
    // C++: void ImGui::TableEndRow(ImGuiTable* table)
    public static void TableEndRow(ImGuiTable table)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert(window == table.InnerWindow);
        Debug.Assert(table.IsInsideRow);

        if (table.CurrentColumn != -1)
        {
            TableEndCell(table);
            table.CurrentColumn = -1;
        }

        // Logging
        if (g.LogEnabled)
            LogRenderedText(window.DC.CursorPos, "|"); // C++: LogRenderedText(NULL, "|")

        // Position cursor at the bottom of our row so it can be used for e.g. clipping calculation. However it is
        // likely that the next call to TableBeginCell() will reposition the cursor to take account of vertical padding.
        window.DC.CursorPos.y = table.RowPosY2;

        // Row background fill
        float bg_y1 = table.RowPosY1;
        float bg_y2 = table.RowPosY2;
        bool unfreeze_rows_actual = (table.CurrentRow + 1 == table.FreezeRowsCount);
        bool unfreeze_rows_request = (table.CurrentRow + 1 == table.FreezeRowsRequest);
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        if ((table.RowFlags & ImGuiTableRowFlags_Headers) != 0 && (table.CurrentRow == 0 || (table.LastRowFlags & ImGuiTableRowFlags_Headers) != 0))
            table_instance.LastTopHeadersRowHeight += bg_y2 - bg_y1;

        bool is_visible = (bg_y2 >= table.InnerClipRect.Min.y && bg_y1 <= table.InnerClipRect.Max.y);
        if (is_visible)
        {
            // Update data for TableGetHoveredRow()
            if (table.HoveredColumnBody != -1 && g.IO.MousePos.y >= bg_y1 && g.IO.MousePos.y < bg_y2 && table_instance.HoveredRowNext < 0)
                table_instance.HoveredRowNext = table.CurrentRow;

            // Decide of background color for the row
            ImU32 bg_col0 = 0;
            ImU32 bg_col1 = 0;
            if (table.RowBgColor[0] != ImGuiCore.IM_COL32_DISABLE)
                bg_col0 = table.RowBgColor[0];
            else if ((table.Flags & ImGuiTableFlags_RowBg) != 0)
                bg_col0 = GetColorU32((table.RowBgColorCounter & 1) != 0 ? ImGuiCol_TableRowBgAlt : ImGuiCol_TableRowBg);
            if (table.RowBgColor[1] != ImGuiCore.IM_COL32_DISABLE)
                bg_col1 = table.RowBgColor[1];

            // Decide of top border color
            ImU32 top_border_col = 0;
            float border_size = TABLE_BORDER_SIZE;
            if (table.CurrentRow > 0 && (table.Flags & ImGuiTableFlags_BordersInnerH) != 0)
                top_border_col = (table.LastRowFlags & ImGuiTableRowFlags_Headers) != 0 ? table.BorderColorStrong : table.BorderColorLight;

            bool draw_cell_bg_color = table.RowCellDataCurrent >= 0;
            bool draw_strong_bottom_border = unfreeze_rows_actual;
            if ((bg_col0 | bg_col1 | top_border_col) != 0 || draw_strong_bottom_border || draw_cell_bg_color)
            {
                // In theory we could call SetWindowClipRectBeforeSetChannel() but since we know TableEndRow() is
                // always followed by a change of clipping rectangle we perform the smallest overwrite possible here.
                if ((table.Flags & ImGuiTableFlags_NoClip) == 0)
                    window.DrawList._CmdHeader.ClipRect = new ImVec4(table.Bg0ClipRectForDrawCmd.Min.x, table.Bg0ClipRectForDrawCmd.Min.y, table.Bg0ClipRectForDrawCmd.Max.x, table.Bg0ClipRectForDrawCmd.Max.y); // C++: table->Bg0ClipRectForDrawCmd.ToVec4()
                table.DrawSplitter.SetCurrentChannel(window.DrawList, TABLE_DRAW_CHANNEL_BG0);
            }

            // Draw row background
            // We soft/cpu clip this so all backgrounds and borders can share the same clipping rectangle
            if (bg_col0 != 0 || bg_col1 != 0)
            {
                ImRect row_rect = new ImRect(table.WorkRect.Min.x, bg_y1, table.WorkRect.Max.x, bg_y2);
                row_rect.ClipWith(table.BgClipRect);
                if (bg_col0 != 0 && row_rect.Min.y < row_rect.Max.y)
                    window.DrawList.AddRectFilled(row_rect.Min, row_rect.Max, bg_col0);
                if (bg_col1 != 0 && row_rect.Min.y < row_rect.Max.y)
                    window.DrawList.AddRectFilled(row_rect.Min, row_rect.Max, bg_col1);
            }

            // Draw cell background color
            if (draw_cell_bg_color)
            {
                for (int cell_i = 0; cell_i <= table.RowCellDataCurrent; cell_i++) // C++: for (ImGuiTableCellData* cell_data = &table->RowCellData[0]; cell_data <= cell_data_end; cell_data++)
                {
                    ImGuiTableCellData cell_data = table.RowCellData[cell_i];
                    // As we render the BG here we need to clip things (for layout we would not)
                    // FIXME: This cancels the OuterPadding addition done by TableGetCellBgRect(), need to keep it while rendering correctly while scrolling.
                    ImGuiTableColumn column = table.Columns[cell_data.Column];
                    ImRect cell_bg_rect = TableGetCellBgRect(table, cell_data.Column);
                    cell_bg_rect.ClipWith(table.BgClipRect);
                    cell_bg_rect.Min.x = ImMath.ImMax(cell_bg_rect.Min.x, column.ClipRect.Min.x);  // So that first column after frozen one gets clipped when scrolling
                    cell_bg_rect.Max.x = ImMath.ImMin(cell_bg_rect.Max.x, column.MaxX);
                    if (cell_bg_rect.Min.y < cell_bg_rect.Max.y)
                        window.DrawList.AddRectFilled(cell_bg_rect.Min, cell_bg_rect.Max, cell_data.BgColor);
                }
            }

            // Draw top border
            if (top_border_col != 0 && bg_y1 >= table.BgClipRect.Min.y && bg_y1 < table.BgClipRect.Max.y)
                window.DrawList.AddLineH(table.BorderX1, table.BorderX2, bg_y1, top_border_col, border_size);

            // Draw bottom border at the row unfreezing mark (always strong)
            if (draw_strong_bottom_border && bg_y2 >= table.BgClipRect.Min.y && bg_y2 < table.BgClipRect.Max.y)
                window.DrawList.AddLineH(table.BorderX1, table.BorderX2, bg_y2, table.BorderColorStrong, border_size);
        }

        // End frozen rows (when we are past the last frozen row line, teleport cursor and alter clipping rectangle)
        // - We need to do that in TableEndRow() instead of TableBeginRow() so the list clipper can mark
        //   end of row and get the new cursor position.
        if (unfreeze_rows_request)
        {
            Debug.Assert(table.FreezeRowsRequest > 0);
            for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
                table.Columns[column_n].NavLayerCurrent = table.NavLayer;
            float y0 = ImMath.ImMax(table.RowPosY2 + 1, table.InnerClipRect.Min.y);
            table_instance.LastFrozenHeight = y0 - table.OuterRect.Min.y;

            if (unfreeze_rows_actual)
            {
                Debug.Assert(table.IsUnfrozenRows == false);
                table.IsUnfrozenRows = true;

                // BgClipRect starts as table->InnerClipRect, reduce it now and make BgClipRectForDrawCmd == BgClipRect
                table.BgClipRect.Min.y = table.Bg2ClipRectForDrawCmd.Min.y = ImMath.ImMin(y0, table.InnerClipRect.Max.y);
                table.BgClipRect.Max.y = table.Bg2ClipRectForDrawCmd.Max.y = table.InnerClipRect.Max.y;
                table.Bg2DrawChannelCurrent = table.Bg2DrawChannelUnfrozen;
                Debug.Assert(table.Bg2ClipRectForDrawCmd.Min.y <= table.Bg2ClipRectForDrawCmd.Max.y);

                float row_height = table.RowPosY2 - table.RowPosY1;
                window.DC.CursorPos.y = table.WorkRect.Min.y + table.RowPosY2 - table.OuterRect.Min.y;
                table.RowPosY2 = window.DC.CursorPos.y;
                table.RowPosY1 = table.RowPosY2 - row_height;
                for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
                {
                    ImGuiTableColumn column = table.Columns[column_n];
                    column.DrawChannelCurrent = column.DrawChannelUnfrozen;
                    column.ClipRect.Min.y = table.Bg2ClipRectForDrawCmd.Min.y;
                }

                // Update cliprect ahead of TableBeginCell() so clipper can access to new ClipRect->Min.y
                SetWindowClipRectBeforeSetChannel(window, table.Columns[0].ClipRect);
                table.DrawSplitter.SetCurrentChannel(window.DrawList, table.Columns[0].DrawChannelCurrent);
            }
        }

        if ((table.RowFlags & ImGuiTableRowFlags_Headers) == 0)
            table.RowBgColorCounter++;
        table.IsInsideRow = false;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Columns changes (imgui_tables.cpp 2269-2418)
    //-------------------------------------------------------------------------

    // C++: int ImGui::TableGetColumnIndex()
    public static int TableGetColumnIndex()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return 0;
        return table.CurrentColumn;
    }

    // [Public] Append into a specific column
    // C++: bool ImGui::TableSetColumnIndex(int column_n)
    public static bool TableSetColumnIndex(int column_n)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return false;

        if (table.CurrentColumn != column_n)
        {
            if (table.CurrentColumn != -1)
                TableEndCell(table);
            bool valid = column_n >= 0 && column_n < table.ColumnsCount;
            ImUtil.ImAssertUserError(valid, "TableSetColumnIndex() invalid column index!");
            if (!valid)
                return false;
            TableBeginCell(table, column_n);
        }

        // Return whether the column is visible. User may choose to skip submitting items based on this return value,
        // however they shouldn't skip submitting for columns that may have the tallest contribution to row height.
        return table.Columns[column_n].IsRequestOutput;
    }

    // [Public] Append into the next column, wrap and create a new row when already on last column
    // C++: bool ImGui::TableNextColumn()
    public static bool TableNextColumn()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null)
            return false;

        if (table.IsInsideRow && table.CurrentColumn + 1 < table.ColumnsCount)
        {
            if (table.CurrentColumn != -1)
                TableEndCell(table);
            TableBeginCell(table, table.CurrentColumn + 1);
        }
        else
        {
            TableNextRow();
            TableBeginCell(table, 0);
        }

        // Return whether the column is visible. User may choose to skip submitting items based on this return value,
        // however they shouldn't skip submitting for columns that may have the tallest contribution to row height.
        return table.Columns[table.CurrentColumn].IsRequestOutput;
    }

    // [Internal] Called by TableSetColumnIndex()/TableNextColumn()
    // This is called very frequently, so we need to be mindful of unnecessary overhead.
    // FIXME-TABLE FIXME-OPT: Could probably shortcut some things for non-active or clipped columns.
    // C++: void ImGui::TableBeginCell(ImGuiTable* table, int column_n)
    public static void TableBeginCell(ImGuiTable table, int column_n)
    {
        ImGuiContext g = GImGui;
        ImGuiTableColumn column = table.Columns[column_n];
        ImGuiWindow window = table.InnerWindow;
        table.CurrentColumn = column_n;

        // Start position is roughly ~~ CellRect.Min + CellPadding + Indent
        float start_x = column.WorkMinX;
        if ((column.Flags & ImGuiTableColumnFlags_IndentEnable) != 0)
            start_x += table.RowIndentOffsetX; // ~~ += window.DC.Indent.x - table->HostIndentX, except we locked it for the row.

        window.DC.CursorPos.x = start_x;
        window.DC.CursorPos.y = table.RowPosY1 + table.RowCellPaddingY;
        window.DC.CursorMaxPos.x = window.DC.CursorPos.x;
        window.DC.ColumnsOffset.x = start_x - window.Pos.x - window.DC.Indent.x; // FIXME-WORKRECT
        window.DC.CursorPosPrevLine.x = window.DC.CursorPos.x; // PrevLine.y is preserved. This allows users to call SameLine() to share LineSize between columns.
        window.DC.CurrLineTextBaseOffset = table.RowTextBaseline;
        window.DC.NavLayerCurrent = column.NavLayerCurrent;

        // Note how WorkRect.Max.y is only set once during layout
        window.WorkRect.Min.y = window.DC.CursorPos.y;
        window.WorkRect.Min.x = column.WorkMinX;
        window.WorkRect.Max.x = column.WorkMaxX;
        window.DC.ItemWidth = column.ItemWidth;

        window.SkipItems = column.IsSkipItems;
        if (column.IsSkipItems)
        {
            g.LastItemData.ID = 0;
            g.LastItemData.StatusFlags = 0;
        }

        // Also see TablePushColumnChannel()
        if ((table.Flags & ImGuiTableFlags_NoClip) != 0)
        {
            // FIXME: if we end up drawing all borders/bg in EndTable, could remove this and just assert that channel hasn't changed.
            table.DrawSplitter.SetCurrentChannel(window.DrawList, TABLE_DRAW_CHANNEL_NOCLIP);
            //Debug.Assert(table.DrawSplitter._Current == TABLE_DRAW_CHANNEL_NOCLIP);
        }
        else
        {
            // FIXME-TABLE: Could avoid this if draw channel is dummy channel?
            SetWindowClipRectBeforeSetChannel(window, column.ClipRect);
            table.DrawSplitter.SetCurrentChannel(window.DrawList, column.DrawChannelCurrent);
        }

        // Logging
        if (g.LogEnabled && !column.IsSkipItems)
        {
            LogRenderedText(window.DC.CursorPos, "|");
            g.LogLinePosY = float.MaxValue;
        }
    }

    // [Internal] Called by TableNextRow()/TableSetColumnIndex()/TableNextColumn()
    // C++: void ImGui::TableEndCell(ImGuiTable* table)
    public static void TableEndCell(ImGuiTable table)
    {
        ImGuiTableColumn column = table.Columns[table.CurrentColumn];
        ImGuiWindow window = table.InnerWindow;

        if (window.DC.IsSetPos)
            ErrorCheckUsingSetCursorPosToExtendParentBoundaries();

        // Report maximum position so we can infer content size per column.
        float p_max_pos_x; // C++: float* p_max_pos_x
        if ((table.RowFlags & ImGuiTableRowFlags_Headers) != 0)
        {
            p_max_pos_x = column.ContentMaxXHeadersUsed;    // Useful in case user submit contents in header row that is not a TableHeader() call
            column.ContentMaxXHeadersUsed = ImMath.ImMax(p_max_pos_x, window.DC.CursorMaxPos.x);
        }
        else if (table.IsUnfrozenRows)
        {
            column.ContentMaxXUnfrozen = ImMath.ImMax(column.ContentMaxXUnfrozen, window.DC.CursorMaxPos.x);
        }
        else
        {
            column.ContentMaxXFrozen = ImMath.ImMax(column.ContentMaxXFrozen, window.DC.CursorMaxPos.x);
        }
        if (column.IsEnabled)
            table.RowPosY2 = ImMath.ImMax(table.RowPosY2, window.DC.CursorMaxPos.y + table.RowCellPaddingY);
        column.ItemWidth = window.DC.ItemWidth;

        // Propagate text baseline for the entire row
        // FIXME-TABLE: Here we propagate text baseline from the last line of the cell.. instead of the first one.
        table.RowTextBaseline = ImMath.ImMax(table.RowTextBaseline, window.DC.PrevLineTextBaseOffset);
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Columns width management (imgui_tables.cpp 2418-2615)
    //-------------------------------------------------------------------------
    // Note that actual columns widths are computed in TableUpdateLayout().
    //-------------------------------------------------------------------------

    // Maximum column content width given current layout. Use column->MinX so this value differs on a per-column basis.
    // C++: float ImGui::TableCalcMaxColumnWidth(const ImGuiTable* table, int column_n)
    public static float TableCalcMaxColumnWidth(ImGuiTable table, int column_n)
    {
        ImGuiTableColumn column = table.Columns[column_n];
        float max_width = float.MaxValue;
        float min_column_distance = table.MinColumnWidth + table.CellPaddingX * 2.0f + table.CellSpacingX1 + table.CellSpacingX2;
        if ((table.Flags & ImGuiTableFlags_ScrollX) != 0)
        {
            // Frozen columns can't reach beyond visible width else scrolling will naturally break.
            // (we use DisplayOrder as within a set of multiple frozen column reordering is possible)
            if (column.DisplayOrder < table.FreezeColumnsRequest)
            {
                max_width = (table.InnerClipRect.Max.x - (table.FreezeColumnsRequest - column.DisplayOrder) * min_column_distance) - column.MinX;
                max_width = max_width - table.OuterPaddingX - table.CellPaddingX - table.CellSpacingX2;
            }
        }
        else if ((table.Flags & ImGuiTableFlags_NoKeepColumnsVisible) == 0)
        {
            // If horizontal scrolling if disabled, we apply a final lossless shrinking of columns in order to make
            // sure they are all visible. Because of this we also know that all of the columns will always fit in
            // table->WorkRect and therefore in table->InnerRect (because ScrollX is off)
            // FIXME-TABLE: This is solved incorrectly but also quite a difficult problem to fix as we also want ClipRect width to match.
            // See "table_width_distrib" and "table_width_keep_visible" tests
            max_width = table.WorkRect.Max.x - (table.ColumnsEnabledCount - column.IndexWithinEnabledSet - 1) * min_column_distance - column.MinX;
            //max_width -= table->CellSpacingX1;
            max_width -= table.CellSpacingX2;
            max_width -= table.CellPaddingX * 2.0f;
            max_width -= table.OuterPaddingX;
        }
        return max_width;
    }

    // Note this is meant to be stored in column->WidthAuto, please generally use the WidthAuto field
    // C++: float ImGui::TableGetColumnWidthAuto(ImGuiTable* table, ImGuiTableColumn* column)
    public static float TableGetColumnWidthAuto(ImGuiTable table, ImGuiTableColumn column)
    {
        float content_width_body = ImMath.ImMax(column.ContentMaxXFrozen, column.ContentMaxXUnfrozen) - column.WorkMinX;
        float content_width_headers = column.ContentMaxXHeadersIdeal - column.WorkMinX;
        float width_auto = content_width_body;
        if ((column.Flags & ImGuiTableColumnFlags_NoHeaderWidth) == 0)
            width_auto = ImMath.ImMax(width_auto, content_width_headers);

        // Non-resizable fixed columns preserve their requested width
        if ((column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0 && column.InitStretchWeightOrWidth > 0.0f)
            if ((table.Flags & ImGuiTableFlags_Resizable) == 0 || (column.Flags & ImGuiTableColumnFlags_NoResize) != 0)
                width_auto = column.InitStretchWeightOrWidth;

        return ImMath.ImMax(width_auto, table.MinColumnWidth);
    }

    // 'width' = inner column width, without padding
    // C++: void ImGui::TableSetColumnWidth(int column_n, float width)
    public static void TableSetColumnWidth(int column_n, float width)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        Debug.Assert(table != null && table.IsLayoutLocked == false);
        Debug.Assert(column_n >= 0 && column_n < table.ColumnsCount);
        ImGuiTableColumn column_0 = table.Columns[column_n];
        float column_0_width = width;

        // Apply constraints early
        // Compare both requested and actual given width to avoid overwriting requested width when column is stuck (minimum size, bounded)
        Debug.Assert(table.MinColumnWidth > 0.0f);
        float min_width = table.MinColumnWidth;
        float max_width = ImMath.ImMax(min_width, column_0.WidthMax); // Don't use TableCalcMaxColumnWidth() here as it would rely on MinX from last instance (#7933)
        column_0_width = ImMath.ImClamp(column_0_width, min_width, max_width);
        if (column_0.WidthGiven == column_0_width || column_0.WidthRequest == column_0_width)
            return;

        //IMGUI_DEBUG_PRINT("TableSetColumnWidth(%d, %.1f->%.1f)\n", column_0_idx, column_0->WidthGiven, column_0_width);
        ImGuiTableColumn column_1 = (column_0.NextEnabledColumn != -1) ? table.Columns[column_0.NextEnabledColumn] : null;

        // In this surprisingly not simple because of how we support mixing Fixed and multiple Stretch columns.
        // - All fixed: easy.
        // - All stretch: easy.
        // - One or more fixed + one stretch: easy.
        // - One or more fixed + more than one stretch: tricky.
        // Qt when manual resize is enabled only supports a single _trailing_ stretch column, we support more cases here.

        // When forwarding resize from Wn| to Fn+1| we need to be considerate of the _NoResize flag on Fn+1.
        // FIXME-TABLE: Find a way to rewrite all of this so interactions feel more consistent for the user.
        // Scenarios:
        // - F1 F2 F3  resize from F1| or F2|   --> ok: alter ->WidthRequested of Fixed column. Subsequent columns will be offset.
        // - F1 F2 F3  resize from F3|          --> ok: alter ->WidthRequested of Fixed column. If active, ScrollX extent can be altered.
        // - F1 F2 W3  resize from F1| or F2|   --> ok: alter ->WidthRequested of Fixed column. If active, ScrollX extent can be altered, but it doesn't make much sense as the Stretch column will always be minimal size.
        // - F1 F2 W3  resize from W3|          --> ok: no-op (disabled by Resize Rule 1)
        // - W1 W2 W3  resize from W1| or W2|   --> ok
        // - W1 W2 W3  resize from W3|          --> ok: no-op (disabled by Resize Rule 1)
        // - W1 F2 F3  resize from F3|          --> ok: no-op (disabled by Resize Rule 1)
        // - W1 F2     resize from F2|          --> ok: no-op (disabled by Resize Rule 1)
        // - W1 W2 F3  resize from W1| or W2|   --> ok
        // - W1 F2 W3  resize from W1| or F2|   --> ok
        // - F1 W2 F3  resize from W2|          --> ok
        // - F1 W3 F2  resize from W3|          --> ok
        // - W1 F2 F3  resize from W1|          --> ok: equivalent to resizing |F2. F3 will not move.
        // - W1 F2 F3  resize from F2|          --> ok
        // All resizes from a Wx columns are locking other columns.

        // Possible improvements:
        // - W1 W2 W3  resize W1|               --> to not be stuck, both W2 and W3 would stretch down. Seems possible to fix. Would be most beneficial to simplify resize of all-weighted columns.
        // - W3 F1 F2  resize W3|               --> to not be stuck past F1|, both F1 and F2 would need to stretch down, which would be lossy or ambiguous. Seems hard to fix.

        // [Resize Rule 1] Can't resize from right of right-most visible column if there is any Stretch column. Implemented in TableUpdateLayout().

        // If we have all Fixed columns OR resizing a Fixed column that doesn't come after a Stretch one, we can do an offsetting resize.
        // This is the preferred resize path
        if ((column_0.Flags & ImGuiTableColumnFlags_WidthFixed) != 0)
            if (column_1 == null || table.LeftMostStretchedColumn == -1 || table.Columns[table.LeftMostStretchedColumn].DisplayOrder >= column_0.DisplayOrder)
            {
                column_0.WidthRequest = column_0_width;
                table.IsSettingsDirty = true;
                return;
            }

        // We can also use previous column if there's no next one (this is used when doing an auto-fit on the right-most stretch column)
        if (column_1 == null)
            column_1 = (column_0.PrevEnabledColumn != -1) ? table.Columns[column_0.PrevEnabledColumn] : null;
        if (column_1 == null)
            return;

        // Resizing from right-side of a Stretch column before a Fixed column forward sizing to left-side of fixed column.
        // (old_a + old_b == new_a + new_b) --> (new_a == old_a + old_b - new_b)
        float column_1_width = ImMath.ImMax(column_1.WidthRequest - (column_0_width - column_0.WidthRequest), min_width);
        column_0_width = column_0.WidthRequest + column_1.WidthRequest - column_1_width;
        Debug.Assert(column_0_width > 0.0f && column_1_width > 0.0f);
        column_0.WidthRequest = column_0_width;
        column_1.WidthRequest = column_1_width;
        if (((column_0.Flags | column_1.Flags) & ImGuiTableColumnFlags_WidthStretch) != 0)
            TableUpdateColumnsWeightFromWidth(table);
        table.IsSettingsDirty = true;
    }

    // Disable clipping then auto-fit, will take 2 frames
    // (we don't take a shortcut for unclipped columns to reduce inconsistencies when e.g. resizing multiple columns)
    // C++: void ImGui::TableSetColumnWidthAutoSingle(ImGuiTable* table, int column_n)
    public static void TableSetColumnWidthAutoSingle(ImGuiTable table, int column_n)
    {
        // Single auto width uses auto-fit
        ImGuiTableColumn column = table.Columns[column_n];
        if (!column.IsEnabled)
            return;
        column.CannotSkipItemsQueue = (1 << 0);
        table.AutoFitSingleColumn = (short)column_n;
    }

    // C++: void ImGui::TableSetColumnWidthAutoAll(ImGuiTable* table)
    public static void TableSetColumnWidthAutoAll(ImGuiTable table)
    {
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
        {
            ImGuiTableColumn column = table.Columns[column_n];
            if (!column.IsEnabled && (column.Flags & ImGuiTableColumnFlags_WidthStretch) == 0) // Cannot reset weight of hidden stretch column
                continue;
            column.CannotSkipItemsQueue = (1 << 0);
            column.AutoFitQueue = (1 << 1);
        }
    }

    // C++: void ImGui::TableUpdateColumnsWeightFromWidth(ImGuiTable* table)
    public static void TableUpdateColumnsWeightFromWidth(ImGuiTable table)
    {
        Debug.Assert(table.LeftMostStretchedColumn != -1 && table.RightMostStretchedColumn != -1);

        // Measure existing quantities
        float visible_weight = 0.0f;
        float visible_width = 0.0f;
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
        {
            ImGuiTableColumn column = table.Columns[column_n];
            if (!column.IsEnabled || (column.Flags & ImGuiTableColumnFlags_WidthStretch) == 0)
                continue;
            Debug.Assert(column.StretchWeight > 0.0f);
            visible_weight += column.StretchWeight;
            visible_width += column.WidthRequest;
        }
        Debug.Assert(visible_weight > 0.0f && visible_width > 0.0f);

        // Apply new weights
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
        {
            ImGuiTableColumn column = table.Columns[column_n];
            if (!column.IsEnabled || (column.Flags & ImGuiTableColumnFlags_WidthStretch) == 0)
                continue;
            column.StretchWeight = (column.WidthRequest / visible_width) * visible_weight;
            Debug.Assert(column.StretchWeight > 0.0f);
        }
    }
}
