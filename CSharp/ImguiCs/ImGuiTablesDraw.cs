// Port of imgui_tables.cpp lines 2615-4459:
//   [SECTION] Tables: Drawing (TablePushBackgroundChannel/TablePopBackgroundChannel/TablePushColumnChannel/
//     TablePopColumnChannel/TableSetupDrawChannels/TableMergeDrawChannels/TableGetColumnBorderCol/TableDrawBorders)
//   [SECTION] Tables: Sorting (TableGetSortSpecs/TableFixColumnSortDirection/TableGetColumnNextSortDirection/
//     TableSetColumnSortDirection/TableSortSpecsSanitize/TableSortSpecsBuild)
//   [SECTION] Tables: Headers (TableGetHeaderRowHeight/TableGetHeaderAngledMaxLabelWidth/TableHeadersRow/
//     TableHeader/TableAngledHeadersRow/TableAngledHeadersRowEx)
//   [SECTION] Tables: Context Menu (TableOpenContextMenu/TableBeginContextMenuPopup/TableDrawDefaultContextMenu)
//   [SECTION] Tables: Settings (.ini data) (TableSettingsInit/TableSettingsCreate/TableSettingsFindByID/
//     TableGetBoundSettings/TableResetSettings/TableSaveSettings/TableLoadSettings/TableLoadSettingsForColumns/
//     TableLoadSettingsForColumn/TableFixDisplayOrder/TableSettingsHandler_*/TableSettingsAddSettingsHandler)
//   [SECTION] Tables: Garbage Collection (TableRemove/TableGcCompactTransientBuffers x2/TableGcCompactSettings)
//   [SECTION] Tables: Debugging (DebugNodeTable/DebugNodeTableSettings)
// C# adaptation notes:
//   - EnabledMaskByIndex/EnabledMaskByDisplayOrder/VisibleMaskByIndex replace C++ ImBitArray (ImU32*) with bool[]
//     (same adaptation as ImGuiTablesMain.cs).
//   - TableMergeDrawChannels: C++ packs the merge masks into g.TempBuffer (raw bytes); C# allocates bool[] per call.
//     ImDrawChannel is a class so C++ struct memcpy becomes reference copies.
//   - ImGuiTableSortSpecs.Specs: C++ stores a pointer to SortSpecsSingle or SortSpecsMulti.Data; C# publishes arrays
//     (a cached 1-element array wrapping SortSpecsSingle, or SortSpecsMulti.Data). SpecsCount stays authoritative.
//   - g.SettingsTables (C++ ImChunkStream<ImGuiTableSettings> with raw memory chunks) is the object-based
//     ImChunkStream<ImGuiTableSettings> (see ImContainers.cs): SettingsOffset is an index, alloc_chunk() stores a
//     class instance. TableSettingsCalcChunkSize() is kept for fidelity but no longer drives allocation.
//   - ImFormatString/sscanf are done with ImFmt.Sprintf / local scanning helpers.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    //-------------------------------------------------------------------------
    // [SECTION] Tables: Drawing (imgui_tables.cpp 2615-3041)
    //-------------------------------------------------------------------------

    // FIXME: This could be abstracted and merged with PushColumnsBackground(), by creating a generic struct
    // with storage for backup cliprect + backup channel + storage for splitter pointer, new clip rect.
    // This would slightly simplify caller code.

    // Bg2 is used by Selectable (and possibly other widgets) to render to the background.
    // Unlike our Bg0/1 channel which we uses for RowBg/CellBg/Borders and where we guarantee all shapes to be CPU-clipped, the Bg2 channel being widgets-facing will rely on regular ClipRect.
    // C++: void ImGui::TablePushBackgroundChannel()
    public static void TablePushBackgroundChannel()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiTable table = g.CurrentTable;

        // Optimization: avoid SetCurrentChannel() + PushClipRect()
        table.HostBackupInnerClipRect = window.ClipRect;
        SetWindowClipRectBeforeSetChannel(window, table.Bg2ClipRectForDrawCmd);
        table.DrawSplitter.SetCurrentChannel(window.DrawList, table.Bg2DrawChannelCurrent);
    }

    // C++: void ImGui::TablePopBackgroundChannel()
    public static void TablePopBackgroundChannel()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiTable table = g.CurrentTable;

        // Optimization: avoid PopClipRect() + SetCurrentChannel()
        SetWindowClipRectBeforeSetChannel(window, table.HostBackupInnerClipRect);
        table.DrawSplitter.SetCurrentChannel(window.DrawList, table.Columns[table.CurrentColumn].DrawChannelCurrent);
    }

    // Also see TableBeginCell()
    // C++: void ImGui::TablePushColumnChannel(int column_n)
    public static void TablePushColumnChannel(int column_n)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;

        // Optimization: avoid SetCurrentChannel() + PushClipRect()
        if ((table.Flags & ImGuiTableFlags_NoClip) != 0)
            return;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiTableColumn column = table.Columns[column_n];
        SetWindowClipRectBeforeSetChannel(window, column.ClipRect);
        table.DrawSplitter.SetCurrentChannel(window.DrawList, column.DrawChannelCurrent);
    }

    // C++: void ImGui::TablePopColumnChannel()
    public static void TablePopColumnChannel()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;

        // Optimization: avoid PopClipRect() + SetCurrentChannel()
        if ((table.Flags & ImGuiTableFlags_NoClip) != 0 || (table.CurrentColumn == -1)) // Calling TreePop() after TableNextRow() is supported.
            return;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiTableColumn column = table.Columns[table.CurrentColumn];
        SetWindowClipRectBeforeSetChannel(window, column.ClipRect);
        table.DrawSplitter.SetCurrentChannel(window.DrawList, column.DrawChannelCurrent);
    }

    // Allocate draw channels. Called by TableUpdateLayout()
    // - We allocate them following storage order instead of display order so reordering columns won't needlessly
    //   increase overall dormant memory cost.
    // - We isolate headers draw commands in their own channels instead of just altering clip rects.
    //   This is in order to facilitate merging of draw commands.
    // - After crossing FreezeRowsCount, all columns see their current draw channel changed to a second set of channels.
    // - We only use the dummy draw channel so we can push a null clipping rectangle into it without affecting other
    //   channels, while simplifying per-row/per-cell overhead. It will be empty and discarded when merged.
    // - We allocate 1 or 2 background draw channels. This is because we know TablePushBackgroundChannel() is only used for
    //   horizontal spanning. If we allowed vertical spanning we'd need one background draw channel per merge group (1-4).
    // Draw channel allocation (before merging):
    // - NoClip                       --> 2+D+1 channels: bg0/1 + bg2 + foreground (same clip rect == always 1 draw call)
    // - Clip                         --> 2+D+N channels
    // - FreezeRows                   --> 2+D+N*2 (unless scrolling value is zero)
    // - FreezeRows || FreezeColumns  --> 3+D+N*2 (unless scrolling value is zero)
    // Where D is 1 if any column is clipped or hidden (dummy channel) otherwise 0.
    // C++: void ImGui::TableSetupDrawChannels(ImGuiTable* table)
    public static void TableSetupDrawChannels(ImGuiTable table)
    {
        int freeze_row_multiplier = (table.FreezeRowsCount > 0) ? 2 : 1;
        int channels_for_row = (table.Flags & ImGuiTableFlags_NoClip) != 0 ? 1 : table.ColumnsEnabledCount;
        int channels_for_bg = 1 + 1 * freeze_row_multiplier;
        // C++: channels_for_dummy = (ColumnsEnabledCount < ColumnsCount || memcmp(VisibleMaskByIndex, EnabledMaskByIndex, ImBitArrayGetStorageSizeInBytes(ColumnsCount)) != 0) ? +1 : 0
        bool visible_mask_equals_enabled = true;
        for (int n = 0; n < table.ColumnsCount; n++)
            if (table.VisibleMaskByIndex[n] != table.EnabledMaskByIndex[n]) { visible_mask_equals_enabled = false; break; }
        int channels_for_dummy = (table.ColumnsEnabledCount < table.ColumnsCount || !visible_mask_equals_enabled) ? +1 : 0;
        int channels_total = channels_for_bg + (channels_for_row * freeze_row_multiplier) + channels_for_dummy;
        table.DrawSplitter.Split(table.InnerWindow.DrawList, channels_total);
        table.DummyDrawChannel = (byte)((channels_for_dummy > 0) ? channels_total - 1 : -1); // (ImGuiTableDrawChannelIdx)-1 == 0xFF
        table.Bg2DrawChannelCurrent = TABLE_DRAW_CHANNEL_BG2_FROZEN;
        table.Bg2DrawChannelUnfrozen = (byte)((table.FreezeRowsCount > 0) ? 2 + channels_for_row : TABLE_DRAW_CHANNEL_BG2_FROZEN);

        int draw_channel_current = 2;
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
        {
            ImGuiTableColumn column = table.Columns[column_n];
            if (column.IsVisibleX && column.IsVisibleY)
            {
                column.DrawChannelFrozen = (byte)draw_channel_current;
                column.DrawChannelUnfrozen = (byte)(draw_channel_current + (table.FreezeRowsCount > 0 ? channels_for_row + 1 : 0));
                if ((table.Flags & ImGuiTableFlags_NoClip) == 0)
                    draw_channel_current++;
            }
            else
            {
                column.DrawChannelFrozen = column.DrawChannelUnfrozen = table.DummyDrawChannel;
            }
            column.DrawChannelCurrent = column.DrawChannelFrozen;
        }

        // Initial draw cmd starts with a BgClipRect that matches the one of its host, to facilitate merge draw commands by default.
        // All our cell highlight are manually clipped with BgClipRect. When unfreezing it will be made smaller to fit scrolling rect.
        // (This technically isn't part of setting up draw channels, but is reasonably related to be done here)
        table.BgClipRect = table.InnerClipRect;
        table.Bg0ClipRectForDrawCmd = table.OuterWindow.ClipRect;
        table.Bg2ClipRectForDrawCmd = table.HostClipRect;
        Debug.Assert(table.BgClipRect.Min.y <= table.BgClipRect.Max.y);
    }

    // Local storage for TableMergeDrawChannels() merge groups.
    // C++ local struct: struct MergeGroup { ImRect ClipRect; int ChannelsCount = 0; ImBitArrayPtr ChannelsMask = NULL; };
    private sealed class TableMergeGroup
    {
        public ImRect ClipRect = new ImRect();
        public int ChannelsCount;
        public bool[] ChannelsMask;      // C++: ImBitArrayPtr (bool[] per port-wide ImBitArray adaptation)
    }

    // This function reorder draw channels based on matching clip rectangle, to facilitate merging them. Called by EndTable().
    // For simplicity we call it TableMergeDrawChannels() but in fact it only reorder channels + overwrite ClipRect,
    // actual merging is done by table->DrawSplitter.Merge() which is called right after TableMergeDrawChannels().
    //
    // Columns where the contents didn't stray off their local clip rectangle can be merged. To achieve
    // this we merge their clip rect and make them contiguous in the channel list, so they can be merged
    // by the call to DrawSplitter.Merge() following to the call to this function.
    // We reorder draw commands by arranging them into a maximum of 4 distinct groups:
    //
    //   1 group:               2 groups:              2 groups:              4 groups:
    //   [ 0. ] no freeze       [ 0. ] row freeze      [ 01 ] col freeze      [ 01 ] row+col freeze
    //   [ .. ]  or no scroll   [ 2. ]  and v-scroll   [ .. ]  and h-scroll   [ 23 ]  and v+h-scroll
    //
    // Each column itself can use 1 channel (row freeze disabled) or 2 channels (row freeze enabled).
    // When the contents of a column didn't stray off its limit, we move its channels into the corresponding group
    // based on its position (within frozen rows/columns groups or not).
    // At the end of the operation our 1-4 groups will each have a ImDrawCmd using the same ClipRect.
    // This function assume that each column are pointing to a distinct draw channel,
    // otherwise merge_group->ChannelsCount will not match set bit count of merge_group->ChannelsMask.
    //
    // Column channels will not be merged into one of the 1-4 groups in the following cases:
    // - The contents stray off its clipping rectangle (we only compare the MaxX value, not the MinX value).
    //   Direct ImDrawList calls won't be taken into account by default, if you use them make sure the ImGui:: bounds
    //   matches, by e.g. calling SetCursorScreenPos().
    // - The channel uses more than one draw command itself. We drop all our attempt at merging stuff here..
    //   we could do better but it's going to be rare and probably not worth the hassle.
    // Columns for which the draw channel(s) haven't been merged with other will use their own ImDrawCmd.
    //
    // This function is particularly tricky to understand.. take a breath.
    // C++: void ImGui::TableMergeDrawChannels(ImGuiTable* table)
    public static void TableMergeDrawChannels(ImGuiTable table)
    {
        ImGuiContext g = GImGui;
        ImDrawListSplitter splitter = table.DrawSplitter;
        bool has_freeze_v = (table.FreezeRowsCount > 0);
        bool has_freeze_h = (table.FreezeColumnsCount > 0);
        Debug.Assert(splitter._Current == 0);

        // Track which groups we are going to attempt to merge, and which channels goes into each group.
        int merge_group_mask = 0x00;
        TableMergeGroup[] merge_groups = new TableMergeGroup[4];
        for (int n = 0; n < merge_groups.Length; n++)
            merge_groups[n] = new TableMergeGroup();

        // C++ uses a reusable temp buffer (g.TempBuffer) for the merge masks as they are dynamically sized.
        int max_draw_channels = 4 + table.ColumnsCount * 2;
        for (int n = 0; n < merge_groups.Length; n++)
            merge_groups[n].ChannelsMask = new bool[max_draw_channels];
        bool[] remaining_mask = new bool[max_draw_channels];

        // 1. Scan channels and take note of those which can be merged
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
        {
            if (!table.VisibleMaskByIndex[column_n]) // C++: IM_BITARRAY_TESTBIT(table->VisibleMaskByIndex, column_n)
                continue;
            ImGuiTableColumn column = table.Columns[column_n];

            int merge_group_sub_count = has_freeze_v ? 2 : 1;
            for (int merge_group_sub_n = 0; merge_group_sub_n < merge_group_sub_count; merge_group_sub_n++)
            {
                int channel_no = (merge_group_sub_n == 0) ? column.DrawChannelFrozen : column.DrawChannelUnfrozen;

                // Don't attempt to merge if there are multiple draw calls within the column
                ImDrawChannel src_channel = splitter._Channels[channel_no];
                if (src_channel._CmdBuffer.Size > 0 && src_channel._CmdBuffer.back().ElemCount == 0 && src_channel._CmdBuffer.back().UserCallback == null) // Equivalent of PopUnusedDrawCmd()
                    src_channel._CmdBuffer.pop_back();
                if (src_channel._CmdBuffer.Size != 1)
                    continue;

                // Find out the width of this merge group and check if it will fit in our column
                // (note that we assume that rendering didn't stray on the left direction. we should need a CursorMinPos to detect it)
                if ((column.Flags & ImGuiTableColumnFlags_NoClip) == 0)
                {
                    float content_max_x;
                    if (!has_freeze_v)
                        content_max_x = ImMath.ImMax(column.ContentMaxXUnfrozen, column.ContentMaxXHeadersUsed); // No row freeze
                    else if (merge_group_sub_n == 0)
                        content_max_x = ImMath.ImMax(column.ContentMaxXFrozen, column.ContentMaxXHeadersUsed);   // Row freeze: use width before freeze
                    else
                        content_max_x = column.ContentMaxXUnfrozen;                                              // Row freeze: use width after freeze
                    if (content_max_x > column.ClipRect.Max.x)
                        continue;
                }

                int merge_group_n = (has_freeze_h && column_n < table.FreezeColumnsCount ? 0 : 1) + (has_freeze_v && merge_group_sub_n == 0 ? 0 : 2);
                Debug.Assert(channel_no < max_draw_channels);
                TableMergeGroup merge_group = merge_groups[merge_group_n];
                if (merge_group.ChannelsCount == 0)
                    merge_group.ClipRect = new ImRect(+float.MaxValue, +float.MaxValue, -float.MaxValue, -float.MaxValue);
                merge_group.ChannelsMask[channel_no] = true; // C++: ImBitArraySetBit(merge_group->ChannelsMask, channel_no)
                merge_group.ChannelsCount++;
                ImDrawCmd src_cmd = src_channel._CmdBuffer[0];
                merge_group.ClipRect.Add(new ImRect(src_cmd.ClipRect.x, src_cmd.ClipRect.y, src_cmd.ClipRect.z, src_cmd.ClipRect.w)); // C++: merge_group->ClipRect.Add(src_channel->_CmdBuffer[0].ClipRect)
                merge_group_mask |= (1 << merge_group_n);
            }

            // Invalidate current draw channel
            // (we don't clear DrawChannelFrozen/DrawChannelUnfrozen solely to facilitate debugging/later inspection of data)
            column.DrawChannelCurrent = 0xFF; // (ImGuiTableDrawChannelIdx)-1
        }

        // [DEBUG] Display merge groups (C++ #if 0 block omitted)

        // 2. Rewrite channel list in our preferred order
        if (merge_group_mask != 0)
        {
            // We skip channel 0 (Bg0/Bg1) and 1 (Bg2 frozen) from the shuffling since they won't move - see channels allocation in TableSetupDrawChannels().
            const int LEADING_DRAW_CHANNELS = 2;
            g.DrawChannelsTempMergeBuffer.resize(splitter._Count - LEADING_DRAW_CHANNELS); // Use shared temporary storage so the allocation gets amortized
            int dst_tmp_n = 0; // C++: ImDrawChannel* dst_tmp walking g.DrawChannelsTempMergeBuffer.Data
            for (int n = LEADING_DRAW_CHANNELS; n < splitter._Count; n++) // C++: ImBitArraySetBitRange(remaining_mask, LEADING_DRAW_CHANNELS, splitter->_Count)
                remaining_mask[n] = true;
            remaining_mask[table.Bg2DrawChannelUnfrozen] = false; // C++: ImBitArrayClearBit(remaining_mask, table->Bg2DrawChannelUnfrozen)
            Debug.Assert(has_freeze_v == false || table.Bg2DrawChannelUnfrozen != TABLE_DRAW_CHANNEL_BG2_FROZEN);
            int remaining_count = splitter._Count - (has_freeze_v ? LEADING_DRAW_CHANNELS + 1 : LEADING_DRAW_CHANNELS);
            //ImRect host_rect = (table->InnerWindow == table->OuterWindow) ? table->InnerClipRect : table->HostClipRect;
            ImRect host_rect = table.HostClipRect;
            for (int merge_group_n = 0; merge_group_n < merge_groups.Length; merge_group_n++)
            {
                int merge_channels_count = merge_groups[merge_group_n].ChannelsCount;
                if (merge_channels_count != 0)
                {
                    TableMergeGroup merge_group = merge_groups[merge_group_n];
                    ImRect merge_clip_rect = merge_group.ClipRect;

                    // Extend outer-most clip limits to match those of host, so draw calls can be merged even if
                    // outer-most columns have some outer padding offsetting them from their parent ClipRect.
                    // The principal cases this is dealing with are:
                    // - On a same-window table (not scrolling = single group), all fitting columns ClipRect -> will extend and match host ClipRect -> will merge
                    // - Columns can use padding and have left-most ClipRect.Min.x and right-most ClipRect.Max.x != from host ClipRect -> will extend and match host ClipRect -> will merge
                    // FIXME-TABLE FIXME-WORKRECT: We are wasting a merge opportunity on tables without scrolling if column doesn't fit
                    // within host clip rect, solely because of the half-padding difference between window->WorkRect and window->InnerClipRect.
                    if ((merge_group_n & 1) == 0 || !has_freeze_h)
                        merge_clip_rect.Min.x = ImMath.ImMin(merge_clip_rect.Min.x, host_rect.Min.x);
                    if ((merge_group_n & 2) == 0 || !has_freeze_v)
                        merge_clip_rect.Min.y = ImMath.ImMin(merge_clip_rect.Min.y, host_rect.Min.y);
                    if ((merge_group_n & 1) != 0)
                        merge_clip_rect.Max.x = ImMath.ImMax(merge_clip_rect.Max.x, host_rect.Max.x);
                    if ((merge_group_n & 2) != 0 && (table.Flags & ImGuiTableFlags_NoHostExtendY) == 0)
                        merge_clip_rect.Max.y = ImMath.ImMax(merge_clip_rect.Max.y, host_rect.Max.y);
                    remaining_count -= merge_group.ChannelsCount;
                    for (int n = 0; n < max_draw_channels; n++) // C++: remaining_mask[n] &= ~merge_group->ChannelsMask[n]
                        if (merge_group.ChannelsMask[n])
                            remaining_mask[n] = false;
                    for (int n = 0; n < splitter._Count && merge_channels_count != 0; n++)
                    {
                        // Copy + overwrite new clip rect
                        if (!merge_group.ChannelsMask[n]) // C++: IM_BITARRAY_TESTBIT(merge_group->ChannelsMask, n)
                            continue;
                        merge_group.ChannelsMask[n] = false; // C++: IM_BITARRAY_CLEARBIT(merge_group->ChannelsMask, n)
                        merge_channels_count--;

                        ImDrawChannel channel = splitter._Channels[n];
                        ImDrawCmd channel_cmd = channel._CmdBuffer[0];
                        Debug.Assert(channel._CmdBuffer.Size == 1 && merge_clip_rect.Contains(new ImRect(channel_cmd.ClipRect.x, channel_cmd.ClipRect.y, channel_cmd.ClipRect.z, channel_cmd.ClipRect.w)));
                        channel_cmd.ClipRect = new ImVec4(merge_clip_rect.Min.x, merge_clip_rect.Min.y, merge_clip_rect.Max.x, merge_clip_rect.Max.y); // C++: merge_clip_rect.ToVec4()
                        channel._CmdBuffer[0] = channel_cmd;
                        g.DrawChannelsTempMergeBuffer[dst_tmp_n++] = channel; // C++: memcpy(dst_tmp++, channel, sizeof(ImDrawChannel)) (reference copy)
                    }
                }

                // Make sure Bg2DrawChannelUnfrozen appears in the middle of our groups (whereas Bg0/Bg1 and Bg2 frozen are fixed to 0 and 1)
                if (merge_group_n == 1 && has_freeze_v)
                    g.DrawChannelsTempMergeBuffer[dst_tmp_n++] = splitter._Channels[table.Bg2DrawChannelUnfrozen];
            }

            // Append unmergeable channels that we didn't reorder at the end of the list
            for (int n = 0; n < splitter._Count && remaining_count != 0; n++)
            {
                if (!remaining_mask[n]) // C++: IM_BITARRAY_TESTBIT(remaining_mask, n)
                    continue;
                ImDrawChannel channel = splitter._Channels[n];
                g.DrawChannelsTempMergeBuffer[dst_tmp_n++] = channel;
                remaining_count--;
            }
            Debug.Assert(dst_tmp_n == g.DrawChannelsTempMergeBuffer.Size);
            for (int n = 0; n < splitter._Count - LEADING_DRAW_CHANNELS; n++) // C++: memcpy(splitter->_Channels.Data + LEADING_DRAW_CHANNELS, ...)
                splitter._Channels.Data[LEADING_DRAW_CHANNELS + n] = g.DrawChannelsTempMergeBuffer.Data[n];
        }
    }

    // C++: static ImU32 TableGetColumnBorderCol(ImGuiTable* table, int order_n, int column_n)
    static ImU32 TableGetColumnBorderCol(ImGuiTable table, int order_n, int column_n)
    {
        bool is_hovered = (table.HoveredColumnBorder == column_n);
        bool is_resized = (table.ResizedColumn == column_n) && (table.InstanceInteracted == table.InstanceCurrent);
        bool is_frozen_separator = (table.FreezeColumnsCount == order_n + 1);
        if (is_resized || is_hovered)
            return GetColorU32(is_resized ? ImGuiCol_SeparatorActive : ImGuiCol_SeparatorHovered);
        if (is_frozen_separator || (table.Flags & (ImGuiTableFlags_NoBordersInBody | ImGuiTableFlags_NoBordersInBodyUntilResize)) != 0)
            return table.BorderColorStrong;
        return table.BorderColorLight;
    }

    // FIXME-TABLE: This is a mess, need to redesign how we render borders (as some are also done in TableEndRow)
    // C++: void ImGui::TableDrawBorders(ImGuiTable* table)
    public static void TableDrawBorders(ImGuiTable table)
    {
        ImGuiWindow inner_window = table.InnerWindow;
        if (!table.OuterWindow.ClipRect.Overlaps(table.OuterRect))
            return;

        ImDrawList inner_drawlist = inner_window.DrawList;
        table.DrawSplitter.SetCurrentChannel(inner_drawlist, TABLE_DRAW_CHANNEL_BG0);
        inner_drawlist.PushClipRect(table.Bg0ClipRectForDrawCmd.Min, table.Bg0ClipRectForDrawCmd.Max, false);

        // Draw inner border and resizing feedback
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        float border_size = TABLE_BORDER_SIZE;
        float draw_y1 = ImMath.ImMax(table.InnerRect.Min.y, (table.FreezeRowsCount >= 1 ? table.InnerRect.Min.y : table.WorkRect.Min.y) + table.AngledHeadersHeight) + ((table.Flags & ImGuiTableFlags_BordersOuterH) != 0 ? 1.0f : 0.0f);
        float draw_y2_body = table.InnerRect.Max.y;
        float draw_y2_head = table.IsUsingHeaders ? ImMath.ImMin(table.InnerRect.Max.y, (table.FreezeRowsCount >= 1 ? table.InnerRect.Min.y : table.WorkRect.Min.y) + table_instance.LastTopHeadersRowHeight) : draw_y1;
        if ((table.Flags & ImGuiTableFlags_BordersInnerV) != 0)
        {
            for (int order_n = 0; order_n < table.ColumnsCount; order_n++)
            {
                if (!table.EnabledMaskByDisplayOrder[order_n]) // C++: IM_BITARRAY_TESTBIT(table->EnabledMaskByDisplayOrder, order_n)
                    continue;

                int column_n = table.DisplayOrderToIndex[order_n];
                ImGuiTableColumn column = table.Columns[column_n];
                bool is_hovered = (table.HoveredColumnBorder == column_n);
                bool is_resized = (table.ResizedColumn == column_n) && (table.InstanceInteracted == table.InstanceCurrent);
                bool is_resizable = (column.Flags & (ImGuiTableColumnFlags_NoResize | ImGuiTableColumnFlags_NoDirectResize_)) == 0;
                bool is_frozen_separator = (table.FreezeColumnsCount == order_n + 1);
                if (column.MaxX > table.InnerClipRect.Max.x && !is_resized)
                    continue;

                // Decide whether right-most column is visible
                if (column.NextEnabledColumn == -1 && !is_resizable)
                    if ((table.Flags & ImGuiTableFlags_SizingMask_) != ImGuiTableFlags_SizingFixedSame || (table.Flags & ImGuiTableFlags_NoHostExtendX) != 0)
                        continue;
                if (column.MaxX <= column.ClipRect.Min.x) // FIXME-TABLE FIXME-STYLE: Assume BorderSize==1, this is problematic if we want to increase the border size..
                    continue;

                // Draw in outer window so right-most column won't be clipped
                float draw_y2 = draw_y2_head;
                if (is_frozen_separator)
                    draw_y2 = draw_y2_body;
                else if ((table.Flags & ImGuiTableFlags_NoBordersInBodyUntilResize) != 0 && (is_hovered || is_resized))
                    draw_y2 = draw_y2_body;
                else if ((table.Flags & (ImGuiTableFlags_NoBordersInBodyUntilResize | ImGuiTableFlags_NoBordersInBody)) == 0)
                    draw_y2 = draw_y2_body;
                if (draw_y2 > draw_y1)
                    inner_drawlist.AddLineV(column.MaxX, draw_y1, draw_y2, TableGetColumnBorderCol(table, order_n, column_n), border_size);
            }
        }

        // Draw outer border
        // FIXME: could use AddRect or explicit VLine/HLine helper?
        if ((table.Flags & ImGuiTableFlags_BordersOuter) != 0)
        {
            // Display outer border offset by 1 which is a simple way to display it without adding an extra draw call
            // (Without the offset, in outer_window it would be rendered behind cells, because child windows are above their
            // parent. In inner_window, it won't reach out over scrollbars. Another weird solution would be to display part
            // of it in inner window, and the part that's over scrollbars in the outer window..)
            // Either solution currently won't allow us to use a larger border size: the border would clipped.
            ImRect outer_border = table.OuterRect;
            ImU32 outer_col = table.BorderColorStrong;
            if ((table.Flags & ImGuiTableFlags_BordersOuter) == ImGuiTableFlags_BordersOuter)
            {
                inner_drawlist.AddRect(outer_border.Min, outer_border.Max, outer_col, 0.0f, border_size);
            }
            else if ((table.Flags & ImGuiTableFlags_BordersOuterV) != 0)
            {
                inner_drawlist.AddLineV(outer_border.Min.x, outer_border.Min.y, outer_border.Max.y, outer_col, border_size);
                inner_drawlist.AddLineV(outer_border.Max.x, outer_border.Min.y, outer_border.Max.y, outer_col, border_size);
            }
            else if ((table.Flags & ImGuiTableFlags_BordersOuterH) != 0)
            {
                inner_drawlist.AddLineH(outer_border.Min.x, outer_border.Max.x, outer_border.Min.y, outer_col, border_size);
                inner_drawlist.AddLineH(outer_border.Min.x, outer_border.Max.x, outer_border.Max.y - border_size, outer_col, border_size);
            }
        }
        if ((table.Flags & ImGuiTableFlags_BordersInnerH) != 0 && table.RowPosY2 < table.OuterRect.Max.y)
        {
            // Draw bottom-most row border between it is above outer border.
            float border_y = table.RowPosY2;
            if (border_y >= table.BgClipRect.Min.y && border_y < table.BgClipRect.Max.y)
                inner_drawlist.AddLineH(table.BorderX1, table.BorderX2, border_y, table.BorderColorLight, border_size);
        }

        inner_drawlist.PopClipRect();
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Sorting (imgui_tables.cpp 3041-3236)
    //-------------------------------------------------------------------------

    // Return NULL if no sort specs (most often when ImGuiTableFlags_Sortable is not set)
    // When 'sort_specs->SpecsDirty == true' you should sort your data. It will be true when sorting specs have
    // changed since last call, or the first time. Make sure to set 'SpecsDirty = false' after sorting,
    // else you may wastefully sort your data every frame!
    // Lifetime: don't hold on this pointer over multiple frames or past any subsequent call to BeginTable()!
    // C++: ImGuiTableSortSpecs* ImGui::TableGetSortSpecs()
    public static ImGuiTableSortSpecs TableGetSortSpecs()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (table == null || (table.Flags & ImGuiTableFlags_Sortable) == 0)
            return null;

        // Require layout (in case TableHeadersRow() hasn't been called) as it may alter IsSortSpecsDirty in some paths.
        if (!table.IsLayoutLocked)
            TableUpdateLayout(table);

        TableSortSpecsBuild(table);
        return table.SortSpecs;
    }

    // C++: static inline ImGuiSortDirection TableGetColumnAvailSortDirection(ImGuiTableColumn* column, int n)
    static int TableGetColumnAvailSortDirection(ImGuiTableColumn column, int n)
    {
        Debug.Assert(n < column.SortDirectionsAvailCount);
        return (column.SortDirectionsAvailList >> (n << 1)) & 0x03;
    }

    // Fix sort direction if currently set on a value which is unavailable (e.g. activating NoSortAscending/NoSortDescending)
    // C++: void ImGui::TableFixColumnSortDirection(ImGuiTable* table, ImGuiTableColumn* column)
    public static void TableFixColumnSortDirection(ImGuiTable table, ImGuiTableColumn column)
    {
        if (column.SortOrder == -1 || (column.SortDirectionsAvailMask & (1 << column.SortDirection)) != 0)
            return;
        column.SortDirection = (byte)TableGetColumnAvailSortDirection(column, 0);
        table.IsSortSpecsDirty = true;
    }

    // Calculate next sort direction that would be set after clicking the column
    // - If the PreferSortDescending flag is set, we will default to a Descending direction on the first click.
    // - Note that the PreferSortAscending flag is never checked, it is essentially the default and therefore a no-op.
    // C++ IM_STATIC_ASSERT(ImGuiSortDirection_None == 0 && ImGuiSortDirection_Ascending == 1 && ImGuiSortDirection_Descending == 2) -- omitted
    // C++: ImGuiSortDirection ImGui::TableGetColumnNextSortDirection(ImGuiTableColumn* column)
    public static int TableGetColumnNextSortDirection(ImGuiTableColumn column)
    {
        Debug.Assert(column.SortDirectionsAvailCount > 0);
        if (column.SortOrder == -1)
            return TableGetColumnAvailSortDirection(column, 0);
        for (int n = 0; n < 3; n++)
            if (column.SortDirection == TableGetColumnAvailSortDirection(column, n))
                return TableGetColumnAvailSortDirection(column, (n + 1) % column.SortDirectionsAvailCount);
        Debug.Assert(false);
        return ImGuiSortDirection_None;
    }

    // Note that the NoSortAscending/NoSortDescending flags are processed in TableSortSpecsSanitize(), and they may change/revert
    // the value of SortDirection. We could technically also do it here but it would be unnecessary and duplicate code.
    // C++: void ImGui::TableSetColumnSortDirection(int column_n, ImGuiSortDirection sort_direction, bool append_to_sort_specs)
    public static void TableSetColumnSortDirection(int column_n, ImGuiSortDirection sort_direction, bool append_to_sort_specs)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;

        if ((table.Flags & ImGuiTableFlags_SortMulti) == 0)
            append_to_sort_specs = false;
        if ((table.Flags & ImGuiTableFlags_SortTristate) == 0)
            Debug.Assert(sort_direction != ImGuiSortDirection_None);

        int sort_order_max = 0; // C++: ImGuiTableColumnIdx sort_order_max
        if (append_to_sort_specs)
            for (int other_column_n = 0; other_column_n < table.ColumnsCount; other_column_n++)
                sort_order_max = ImMath.ImMax(sort_order_max, (int)table.Columns[other_column_n].SortOrder);

        ImGuiTableColumn column = table.Columns[column_n];
        column.SortDirection = (byte)sort_direction;
        if (column.SortDirection == ImGuiSortDirection_None)
            column.SortOrder = -1;
        else if (column.SortOrder == -1 || !append_to_sort_specs)
            column.SortOrder = (short)(append_to_sort_specs ? sort_order_max + 1 : 0);

        for (int other_column_n = 0; other_column_n < table.ColumnsCount; other_column_n++)
        {
            ImGuiTableColumn other_column = table.Columns[other_column_n];
            if (other_column != column && !append_to_sort_specs)
                other_column.SortOrder = -1;
            TableFixColumnSortDirection(table, other_column);
        }
        table.IsSettingsDirty = true;
        table.IsSortSpecsDirty = true;
    }

    // C++: void ImGui::TableSortSpecsSanitize(ImGuiTable* table)
    public static void TableSortSpecsSanitize(ImGuiTable table)
    {
        Debug.Assert((table.Flags & ImGuiTableFlags_Sortable) != 0);

        // Clear SortOrder from hidden column and verify that there's no gap or duplicate.
        int sort_order_count = 0;
        ulong sort_order_mask = 0x00; // C++: ImU64
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
        {
            ImGuiTableColumn column = table.Columns[column_n];
            if (column.SortOrder != -1 && !column.IsEnabled)
                column.SortOrder = -1;
            if (column.SortOrder == -1)
                continue;
            sort_order_count++;
            sort_order_mask |= (ulong)1 << column.SortOrder;
            Debug.Assert(sort_order_count < sizeof(ulong) * 8);
        }

        bool need_fix_linearize = ((ulong)1 << sort_order_count) != (sort_order_mask + 1);
        bool need_fix_single_sort_order = (sort_order_count > 1) && (table.Flags & ImGuiTableFlags_SortMulti) == 0;
        if (need_fix_linearize || need_fix_single_sort_order)
        {
            ulong fixed_mask = 0x00;
            for (int sort_n = 0; sort_n < sort_order_count; sort_n++)
            {
                // Fix: Rewrite sort order fields if needed so they have no gap or duplicate.
                // (e.g. SortOrder 0 disappeared, SortOrder 1..2 exists --> rewrite then as SortOrder 0..1)
                int column_with_smallest_sort_order = -1;
                for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
                    if ((fixed_mask & ((ulong)1 << column_n)) == 0 && table.Columns[column_n].SortOrder != -1)
                        if (column_with_smallest_sort_order == -1 || table.Columns[column_n].SortOrder < table.Columns[column_with_smallest_sort_order].SortOrder)
                            column_with_smallest_sort_order = column_n;
                Debug.Assert(column_with_smallest_sort_order != -1);
                fixed_mask |= (ulong)1 << column_with_smallest_sort_order;
                table.Columns[column_with_smallest_sort_order].SortOrder = (short)sort_n;

                // Fix: Make sure only one column has a SortOrder if ImGuiTableFlags_MultiSortable is not set.
                if (need_fix_single_sort_order)
                {
                    sort_order_count = 1;
                    for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
                        if (column_n != column_with_smallest_sort_order)
                            table.Columns[column_n].SortOrder = -1;
                    break;
                }
            }
        }

        // Fallback default sort order (if no column with the ImGuiTableColumnFlags_DefaultSort flag)
        if (sort_order_count == 0 && (table.Flags & ImGuiTableFlags_SortTristate) == 0)
            for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
            {
                ImGuiTableColumn column = table.Columns[column_n];
                if (column.IsEnabled && (column.Flags & ImGuiTableColumnFlags_NoSort) == 0)
                {
                    sort_order_count = 1;
                    column.SortOrder = 0;
                    column.SortDirection = (byte)TableGetColumnAvailSortDirection(column, 0);
                    break;
                }
            }

        table.SortSpecsCount = (short)sort_order_count;
    }

    // C++: void ImGui::TableSortSpecsBuild(ImGuiTable* table)
    public static void TableSortSpecsBuild(ImGuiTable table)
    {
        bool dirty = table.IsSortSpecsDirty;
        if (dirty)
        {
            TableSortSpecsSanitize(table);
            table.SortSpecsMulti.resize(table.SortSpecsCount <= 1 ? 0 : table.SortSpecsCount);
            for (int n = 0; n < table.SortSpecsMulti.Size; n++) // C# note: C++ resize default-constructs ImGuiTableColumnSortSpecs elements
                if (table.SortSpecsMulti.Data[n] == null)
                    table.SortSpecsMulti.Data[n] = new ImGuiTableColumnSortSpecs();
            table.SortSpecs.SpecsDirty = true;  // Mark as dirty for user
            table.IsSortSpecsDirty = false;     // Mark as not dirty for us
        }

        // Write output
        // May be able to move all SortSpecs data from table (48 bytes) to ImGuiTableTempData if we decide to write it back on every BeginTable()
        // C++: sort_specs points at SortSpecsSingle or SortSpecsMulti.Data; we write through the object/array directly.
        if (dirty && table.SortSpecsCount != 0)
            for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
            {
                ImGuiTableColumn column = table.Columns[column_n];
                if (column.SortOrder == -1)
                    continue;
                Debug.Assert(column.SortOrder < table.SortSpecsCount);
                ImGuiTableColumnSortSpecs sort_spec = (table.SortSpecsCount == 1) ? table.SortSpecsSingle : table.SortSpecsMulti[column.SortOrder];
                sort_spec.ColumnUserID = column.UserData;
                sort_spec.ColumnIndex = (short)column_n;
                sort_spec.SortOrder = column.SortOrder;
                sort_spec.SortDirection = (byte)column.SortDirection;
            }

        // Publish output. C++ stores a raw pointer; C# publishes arrays (SpecsCount remains authoritative).
        if (table.SortSpecsCount == 0)
        {
            table.SortSpecs.Specs = null;
        }
        else if (table.SortSpecsCount == 1)
        {
            // Wrap SortSpecsSingle in a cached 1-element array (C++: &table->SortSpecsSingle)
            if (table.SortSpecs.Specs == null || table.SortSpecs.Specs.Length != 1 || !ReferenceEquals(table.SortSpecs.Specs[0], table.SortSpecsSingle))
                table.SortSpecs.Specs = new ImGuiTableColumnSortSpecs[] { table.SortSpecsSingle };
        }
        else
        {
            table.SortSpecs.Specs = table.SortSpecsMulti.Data; // C++: table->SortSpecsMulti.Data
        }
        table.SortSpecs.SpecsCount = table.SortSpecsCount;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Headers (imgui_tables.cpp 3236-3636)
    //-------------------------------------------------------------------------

    // C++: float ImGui::TableGetHeaderRowHeight()
    public static float TableGetHeaderRowHeight()
    {
        // Caring for a minor edge case:
        // Calculate row height, for the unlikely case that some labels may be taller than others.
        // If we didn't do that, uneven header height would highlight but smaller one before the tallest wouldn't catch input for all height.
        // In your custom header row you may omit this all together and just call TableNextRow() without a height...
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        float row_height = g.FontSize;
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
            if (table.EnabledMaskByIndex[column_n]) // C++: IM_BITARRAY_TESTBIT(table->EnabledMaskByIndex, column_n)
                if ((table.Columns[column_n].Flags & ImGuiTableColumnFlags_NoHeaderLabel) == 0)
                    row_height = ImMath.ImMax(row_height, CalcTextSize(TableGetColumnName(table, column_n)).y);
        return row_height + g.Style.CellPadding.y * 2.0f;
    }

    // C++: float ImGui::TableGetHeaderAngledMaxLabelWidth()
    public static float TableGetHeaderAngledMaxLabelWidth()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        float width = 0.0f;
        for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
            if (table.EnabledMaskByIndex[column_n]) // C++: IM_BITARRAY_TESTBIT(table->EnabledMaskByIndex, column_n)
                if ((table.Columns[column_n].Flags & ImGuiTableColumnFlags_AngledHeader) != 0)
                    width = ImMath.ImMax(width, CalcTextSize(TableGetColumnName(table, column_n), -1, true).x);
        return width + g.Style.CellPadding.y * 2.0f; // Swap padding
    }

    // [Public] This is a helper to output TableHeader() calls based on the column names declared in TableSetupColumn().
    // The intent is that advanced users willing to create customized headers would not need to use this helper
    // and can create their own! For example: TableHeader() may be preceded by Checkbox() or other custom widgets.
    // See 'Demo->Tables->Custom headers' for a demonstration of implementing a custom version of this.
    // This code is intentionally written to not make much use of internal functions, to give you better direction
    // if you need to write your own.
    // FIXME-TABLE: TableOpenContextMenu() and TableGetHeaderRowHeight() are not public.
    // C++: void ImGui::TableHeadersRow()
    public static void TableHeadersRow()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImUtil.ImAssertUserError(table != null, "Call should only be done while in BeginTable() scope!");
        if (table == null)
            return;

        // Call layout if not already done. This is automatically done by TableNextRow: we do it here _only_ to make
        // it easier to debug-step in TableUpdateLayout(). Your own version of this function doesn't need this.
        if (!table.IsLayoutLocked)
            TableUpdateLayout(table);

        // Open row
        float row_height = TableGetHeaderRowHeight();
        TableNextRow(ImGuiTableRowFlags_Headers, row_height);
        float row_y1 = GetCursorScreenPos().y;
        if (table.HostSkipItems) // Merely an optimization, you may skip in your own code.
            return;

        int columns_count = TableGetColumnCount();
        for (int column_n = 0; column_n < columns_count; column_n++)
        {
            if (!TableSetColumnIndex(column_n) && table.LastHeldHeaderColumn != column_n)
                continue;

            // Push an id to allow empty/unnamed headers. This is also idiomatic as it ensure there is a consistent ID path to access columns (for e.g. automation)
            string name = (TableGetColumnFlags(column_n) & ImGuiTableColumnFlags_NoHeaderLabel) != 0 ? "" : TableGetColumnName(column_n);
            PushID(column_n);
            TableHeader(name);
            PopID();
        }

        // Allow opening popup from the right-most section after the last column.
        ImVec2 mouse_pos = GetMousePos();
        if (IsMouseReleased(ImGuiMouseButton_Right) && TableGetHoveredColumn() == columns_count)
            if (mouse_pos.y >= row_y1 && mouse_pos.y < row_y1 + row_height)
                TableOpenContextMenu(columns_count); // Will open a non-column-specific popup.
    }

    // Emit a column header (text + optional sort order)
    // We cpu-clip text here so that all columns headers can be merged into a same draw call.
    // Note that because of how we cpu-clip and display sorting indicators, you _cannot_ use SameLine() after a TableHeader()
    // C++: void ImGui::TableHeader(const char* label)
    public static void TableHeader(string label)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        ImGuiTable table = g.CurrentTable;
        ImUtil.ImAssertUserError(table != null, "Call should only be done while in BeginTable() scope!");
        if (table == null)
            return;
        Debug.Assert(table.CurrentColumn != -1);
        int column_n = table.CurrentColumn;
        ImGuiTableColumn column = table.Columns[column_n];

        // Label
        if (label == null)
            label = "";
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImVec2 label_pos = window.DC.CursorPos;

        // If we already got a row height, there's use that.
        // FIXME-TABLE: Padding problem if the correct outer-padding CellBgRect strays off our ClipRect?
        ImRect cell_r = TableGetCellBgRect(table, column_n);
        float label_height = ImMath.ImMax(label_size.y, table.RowMinHeight - table.RowCellPaddingY * 2.0f);

        // Calculate ideal size for sort order arrow
        float w_arrow = 0.0f;
        float w_sort_text = 0.0f;
        bool sort_arrow = false;
        string sort_order_suf = ""; // C++: char sort_order_suf[4]
        const float ARROW_SCALE = 0.65f;
        if ((table.Flags & ImGuiTableFlags_Sortable) != 0 && (column.Flags & ImGuiTableColumnFlags_NoSort) == 0)
        {
            w_arrow = ImMath.ImTrunc(g.FontSize * ARROW_SCALE + g.Style.FramePadding.x);
            if (column.SortOrder != -1)
                sort_arrow = true;
            if (column.SortOrder > 0)
            {
                sort_order_suf = ImFmt.Sprintf("%d", column.SortOrder + 1); // C++: ImFormatString(sort_order_suf, IM_COUNTOF(sort_order_suf), "%d", column->SortOrder + 1)
                w_sort_text = g.Style.ItemInnerSpacing.x + CalcTextSize(sort_order_suf).x;
            }
        }

        // We feed our unclipped width to the column without writing on CursorMaxPos, so that column is still considered for merging.
        float max_pos_x = label_pos.x + label_size.x + w_sort_text + w_arrow;
        column.ContentMaxXHeadersUsed = ImMath.ImMax(column.ContentMaxXHeadersUsed, sort_arrow ? cell_r.Max.x : ImMath.ImMin(max_pos_x, cell_r.Max.x));
        column.ContentMaxXHeadersIdeal = ImMath.ImMax(column.ContentMaxXHeadersIdeal, max_pos_x);

        // Keep header highlighted when context menu is open.
        ImGuiID id = window.GetID(label);
        ImRect bb = new ImRect(cell_r.Min.x, cell_r.Min.y, cell_r.Max.x, ImMath.ImMax(cell_r.Max.y, cell_r.Min.y + label_height + g.Style.CellPadding.y * 2.0f));
        ItemSize(new ImVec2(0.0f, label_height)); // Don't declare unclipped width, it'll be fed ContentMaxPosHeadersIdeal
        if (!ItemAdd(bb, id))
            return;

        //GetForegroundDrawList()->AddRect(cell_r.Min, cell_r.Max, IM_COL32(255, 0, 0, 255)); // [DEBUG]
        //GetForegroundDrawList()->AddRect(bb.Min, bb.Max, IM_COL32(255, 0, 0, 255)); // [DEBUG]

        // Using AllowOverlap mode because we cover the whole cell, and we want user to be able to submit subsequent items.
        bool highlight = (table.HighlightColumnHeader == column_n);
        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held, ImGuiButtonFlags_AllowOverlap);
        if (held || hovered || highlight)
        {
            ImU32 col = GetColorU32(held ? ImGuiCol_HeaderActive : hovered ? ImGuiCol_HeaderHovered : ImGuiCol_Header);
            //RenderFrame(bb.Min, bb.Max, col, false, 0.0f);
            TableSetBgColor(ImGuiTableBgTarget_CellBg, col, table.CurrentColumn);
        }
        else
        {
            // Submit single cell bg color in the case we didn't submit a full header row
            if ((table.RowFlags & ImGuiTableRowFlags_Headers) == 0)
                TableSetBgColor(ImGuiTableBgTarget_CellBg, GetColorU32(ImGuiCol_TableHeaderBg), table.CurrentColumn);
        }
        RenderNavCursor(bb, id, ImGuiNavRenderCursorFlags_Compact, 0.0f);
        if (held)
            table.HeldHeaderColumn = (short)column_n;
        window.DC.CursorPos.y -= g.Style.ItemSpacing.y * 0.5f;

        // Drag and drop to re-order columns.
        // FIXME-TABLE: Scroll request while reordering a column and it lands out of the scrolling zone.
        if (held && (table.Flags & ImGuiTableFlags_Reorderable) != 0 && IsMouseDragging(ImGuiMouseButton_Left) && !g.DragDropActive)
        {
            // - While moving a column it will jump on the other side of the mouse, so we also test for MouseDelta.x
            // - We need to handle reordering across hidden columns.
            //   In the configuration below, moving C to the right of E will lead to:
            //      ... C [D] E  --->  ... [D] E  C   (Column name/index)
            //      ... 2  3  4        ...  2  3  4   (Display order)
            // - The other constraints are enforced by TableQueueSetColumnDisplayOrder() which might early out.
            table.InstanceInteracted = table.InstanceCurrent;
            if (g.IO.MouseDelta.x < 0.0f && g.IO.MousePos.x < cell_r.Min.x)
                if (column.PrevEnabledColumn != -1) // C++: if (ImGuiTableColumn* prev_column = ...)
                    TableQueueSetColumnDisplayOrder(table, column_n, table.Columns[column.PrevEnabledColumn].DisplayOrder);
            if (g.IO.MouseDelta.x > 0.0f && g.IO.MousePos.x > cell_r.Max.x)
                if (column.NextEnabledColumn != -1) // C++: if (ImGuiTableColumn* next_column = ...)
                    TableQueueSetColumnDisplayOrder(table, column_n, table.Columns[column.NextEnabledColumn].DisplayOrder);
        }

        // Sort order arrow
        // - Only clip label for a visible arrow. Auto-fit still accounts for a possible arrow, but when
        //   manually sized smaller we don't clip the label for the sake of an arrow that isn't displayed.
        float ellipsis_max = ImMath.ImMax(cell_r.Max.x - (sort_arrow ? w_arrow + w_sort_text : 0.0f), label_pos.x);
        if ((table.Flags & ImGuiTableFlags_Sortable) != 0 && (column.Flags & ImGuiTableColumnFlags_NoSort) == 0)
        {
            if (column.SortOrder != -1)
            {
                float x = ImMath.ImMax(cell_r.Min.x, cell_r.Max.x - w_arrow - w_sort_text);
                float y = label_pos.y;
                if (column.SortOrder > 0)
                {
                    PushStyleColor(ImGuiCol_Text, GetColorU32(ImGuiCol_Text, 0.70f));
                    RenderText(new ImVec2(x + g.Style.ItemInnerSpacing.x, y), sort_order_suf);
                    PopStyleColor();
                    x += w_sort_text;
                }
                RenderArrow(window.DrawList, new ImVec2(x, y), GetColorU32(ImGuiCol_Text), column.SortDirection == ImGuiSortDirection_Ascending ? ImGuiDir_Up : ImGuiDir_Down, ARROW_SCALE);
            }

            // Handle clicking on column header to adjust Sort Order
            if (pressed && table.ReorderColumn != column_n)
            {
                ImGuiSortDirection sort_direction = TableGetColumnNextSortDirection(column);
                TableSetColumnSortDirection(column_n, sort_direction, g.IO.KeyShift);
            }
        }

        // Render clipped label. Clipping here ensure that in the majority of situations, all our header cells will
        // be merged into a single draw call.
        //window->DrawList->AddCircleFilled(ImVec2(ellipsis_max, label_pos.y), 40, IM_COL32_WHITE);
        RenderTextEllipsis(window.DrawList, label_pos, new ImVec2(ellipsis_max, bb.Max.y), ellipsis_max, label, label_end, label_size);

        bool text_clipped = label_size.x > (ellipsis_max - label_pos.x);
        if (text_clipped && hovered && g.ActiveId == 0)
            SetItemTooltip("%s", label.Substring(0, label_end)); // C++: SetItemTooltip("%.*s", (int)(label_end - label), label)

        // We don't use BeginPopupContextItem() because we want the popup to stay up even after the column is hidden
        if (IsPopupOpenRequestForItem(ImGuiPopupFlags_None, id))
            TableOpenContextMenu(column_n);

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
    }

    // Unlike TableHeadersRow() it is not expected that you can reimplement or customize this with custom widgets.
    // FIXME: No hit-testing/button on the angled header.
    // C++: void ImGui::TableAngledHeadersRow()
    public static void TableAngledHeadersRow()
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImGuiTableTempData temp_data = table.TempData;
        temp_data.AngledHeadersRequests.resize(0);
        temp_data.AngledHeadersRequests.reserve(table.ColumnsEnabledCount);

        // Which column needs highlight?
        ImGuiID row_id = GetID("##AngledHeaders");
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent);
        int highlight_column_n = (table.LastHeldHeaderColumn != -1) ? table.LastHeldHeaderColumn : table.HighlightColumnHeader;
        if (highlight_column_n == -1 && table.HoveredColumnBody != -1)
            if (table_instance.HoveredRowLast == 0 && table.HoveredColumnBorder == -1 && (g.ActiveId == 0 || g.ActiveId == row_id || (table.IsActiveIdInTable || g.DragDropActive)))
                highlight_column_n = table.HoveredColumnBody;

        // Build up request
        ImU32 col_header_bg = GetColorU32(ImGuiCol_TableHeaderBg);
        ImU32 col_text = GetColorU32(ImGuiCol_Text);
        for (int order_n = 0; order_n < table.ColumnsCount; order_n++)
            if (table.EnabledMaskByDisplayOrder[order_n]) // C++: IM_BITARRAY_TESTBIT(table->EnabledMaskByDisplayOrder, order_n)
            {
                int column_n = table.DisplayOrderToIndex[order_n];
                ImGuiTableColumn column = table.Columns[column_n];
                if ((column.Flags & ImGuiTableColumnFlags_AngledHeader) == 0) // Note: can't rely on ImGuiTableColumnFlags_IsVisible test here.
                    continue;
                ImGuiTableHeaderData request = new ImGuiTableHeaderData { Index = (short)column_n, TextColor = col_text, BgColor0 = col_header_bg, BgColor1 = (column_n == highlight_column_n) ? GetColorU32(ImGuiCol_Header) : 0 };
                temp_data.AngledHeadersRequests.push_back(request);
            }

        // Render row
        TableAngledHeadersRowEx(row_id, g.Style.TableAngledHeadersAngle, 0.0f, temp_data.AngledHeadersRequests.Data, temp_data.AngledHeadersRequests.Size);
    }

    // Important: data must be fed left to right
    // C++: void ImGui::TableAngledHeadersRowEx(ImGuiID row_id, float angle, float max_label_width, const ImGuiTableHeaderData* data, int data_count)
    public static void TableAngledHeadersRowEx(ImGuiID row_id, float angle, float max_label_width, ImGuiTableHeaderData[] data, int data_count)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        ImGuiWindow window = g.CurrentWindow;
        ImDrawList draw_list = window.DrawList;
        ImUtil.ImAssertUserError(table != null, "Call should only be done while in BeginTable() scope!");
        if (table == null)
            return;
        Debug.Assert(table.CurrentRow == -1, "Must be first row");

        if (max_label_width == 0.0f)
            max_label_width = TableGetHeaderAngledMaxLabelWidth();

        // Angle argument expressed in (-IM_PI/2 .. +IM_PI/2) as it is easier to think about for user.
        bool flip_label = (angle < 0.0f);
        angle -= ImMath.IM_PI * 0.5f;
        float cos_a = ImMath.ImCos(angle);
        float sin_a = ImMath.ImSin(angle);
        float label_cos_a = flip_label ? ImMath.ImCos(angle + ImMath.IM_PI) : cos_a;
        float label_sin_a = flip_label ? ImMath.ImSin(angle + ImMath.IM_PI) : sin_a;
        ImVec2 unit_right = new ImVec2(cos_a, sin_a);

        // Calculate our base metrics and set angled headers data _before_ the first call to TableNextRow()
        // FIXME-STYLE: Would it be better for user to submit 'max_label_width' or 'row_height' ? One can be derived from the other.
        float header_height = g.FontSize + g.Style.CellPadding.x * 2.0f;
        float row_height = ImMath.ImTrunc(MathF.Abs(ImMath.ImRotate(new ImVec2(max_label_width, flip_label ? +header_height : -header_height), cos_a, sin_a).y)); // C++: ImFabs()
        table.AngledHeadersHeight = row_height;
        table.AngledHeadersSlope = (sin_a != 0.0f) ? (cos_a / sin_a) : 0.0f;
        ImVec2 header_angled_vector = unit_right * (row_height / -sin_a); // vector from bottom-left to top-left, and from bottom-right to top-right

        // Declare row, override and draw our own background
        // FIXME-TABLE: Generally broken when overlapping frozen columns limit.
        TableNextRow(ImGuiTableRowFlags_Headers, row_height);
        TableNextColumn();
        ImRect row_r = new ImRect(table.WorkRect.Min.x, table.BgClipRect.Min.y, table.WorkRect.Max.x, table.RowPosY2);
        table.DrawSplitter.SetCurrentChannel(draw_list, TABLE_DRAW_CHANNEL_BG0);
        float clip_rect_min_x = table.BgClipRect.Min.x;
        if (table.FreezeColumnsCount > 0)
            clip_rect_min_x = ImMath.ImMax(clip_rect_min_x, table.Columns[table.DisplayOrderToIndex[table.FreezeColumnsCount - 1]].MaxX);
        TableSetBgColor(ImGuiTableBgTarget_RowBg0, 0); // Cancel
        PushClipRect(table.BgClipRect.Min, table.BgClipRect.Max, false); // Span all columns
        draw_list.AddRectFilled(new ImVec2(table.BgClipRect.Min.x, row_r.Min.y), new ImVec2(table.BgClipRect.Max.x, row_r.Max.y), GetColorU32(ImGuiCol_TableHeaderBg, 0.25f)); // FIXME-STYLE: Change row background with an arbitrary color.
        PushClipRect(new ImVec2(clip_rect_min_x, table.BgClipRect.Min.y), table.BgClipRect.Max, true); // Span all columns

        ButtonBehavior(row_r, row_id, out _, out _);
        KeepAliveID(row_id);

        float ascent_scaled = g.FontBaked.Ascent * g.FontBakedScale; // FIXME: Standardize those scaling factors better
        float line_off_for_ascent_x = (ImMath.ImMax((g.FontSize - ascent_scaled) * 0.5f, 0.0f) / -sin_a) * (flip_label ? -1.0f : 1.0f);
        ImVec2 padding = g.Style.CellPadding; // We will always use swapped component
        ImVec2 align = g.Style.TableAngledHeadersTextAlign;

        // Draw background and labels in first pass, then all borders.
        float max_x = -float.MaxValue;
        for (int pass = 0; pass < 2; pass++)
            for (int order_n = 0; order_n < data_count; order_n++)
            {
                ImGuiTableHeaderData request = data[order_n];
                int column_n = request.Index;
                ImGuiTableColumn column = table.Columns[column_n];

                ImVec2[] bg_shape = new ImVec2[4];
                bg_shape[0] = new ImVec2(column.MaxX, row_r.Max.y);
                bg_shape[1] = new ImVec2(column.MinX, row_r.Max.y);
                bg_shape[2] = bg_shape[1] + header_angled_vector;
                bg_shape[3] = bg_shape[0] + header_angled_vector;
                if (pass == 0)
                {
                    // Draw shape
                    draw_list.AddQuadFilled(bg_shape[0], bg_shape[1], bg_shape[2], bg_shape[3], request.BgColor0);
                    draw_list.AddQuadFilled(bg_shape[0], bg_shape[1], bg_shape[2], bg_shape[3], request.BgColor1); // Optional highlight
                    max_x = ImMath.ImMax(max_x, bg_shape[3].x);

                    // Draw label
                    // - First draw at an offset where RenderTextXXX() function won't meddle with applying current ClipRect, then transform to final offset.
                    // - Handle multiple lines manually, as we want each lines to follow on the horizontal border, rather than see a whole block rotated.
                    // C#: the C++ walks the label with char pointers; we walk with indices into the full label string.
                    string label_name_full = TableGetColumnName(table, column_n);
                    int label_name_pos = 0;
                    int label_name_end = FindRenderedTextEnd(label_name_full);
                    float line_off_step_x = (g.FontSize / -sin_a);
                    int label_lines = ImText.ImTextCountLines(label_name_full, label_name_end);

                    // Left<>Right alignment
                    float line_off_curr_x = flip_label ? (label_lines - 1) * line_off_step_x : 0.0f;
                    float line_off_for_align_x = ImMath.ImFloor(ImMath.ImMax((((column.MaxX - column.MinX) - padding.x * 2.0f) - (label_lines * line_off_step_x)), 0.0f) * align.x);
                    line_off_curr_x += line_off_for_align_x - line_off_for_ascent_x;

                    // Register header width
                    column.ContentMaxXHeadersUsed = column.ContentMaxXHeadersIdeal = column.WorkMinX + ImMath.ImCeil(label_lines * line_off_step_x - line_off_for_align_x);

                    while (label_name_pos < label_name_end)
                    {
                        int label_name_eol = label_name_full.IndexOf('\n', label_name_pos); // C++: strchr(label_name, '\n')
                        if (label_name_eol == -1)
                            label_name_eol = label_name_end;
                        string label_name = label_name_full.Substring(label_name_pos, label_name_eol - label_name_pos);

                        // FIXME: Individual line clipping for right-most column is broken for negative angles.
                        ImVec2 label_size = CalcTextSize(label_name);
                        float clip_width = max_label_width - padding.y; // Using padding.y*2.0f would be symmetrical but hide more text.
                        float clip_height = ImMath.ImMin(label_size.y, column.ClipRect.Max.x - column.WorkMinX - line_off_curr_x);
                        ImRect clip_r = new ImRect(window.ClipRect.Min, window.ClipRect.Min + new ImVec2(clip_width, clip_height));
                        uint vtx_idx_begin = draw_list._VtxCurrentIdx;
                        PushStyleColor(ImGuiCol_Text, request.TextColor);
                        RenderTextEllipsis(draw_list, clip_r.Min, clip_r.Max, clip_r.Max.x, label_name, -1, label_size);
                        PopStyleColor();
                        uint vtx_idx_end = draw_list._VtxCurrentIdx;

                        // Up<>Down alignment
                        float available_space = ImMath.ImMax(clip_width - label_size.x + MathF.Abs(padding.x * cos_a) * 2.0f - MathF.Abs(padding.y * sin_a) * 2.0f, 0.0f); // C++: ImAbs()
                        float vertical_offset = available_space * align.y * (flip_label ? -1.0f : 1.0f);

                        // Rotate and offset label
                        ImVec2 pivot_in = new ImVec2(window.ClipRect.Min.x - vertical_offset, window.ClipRect.Min.y + label_size.y);
                        ImVec2 pivot_out = new ImVec2(column.WorkMinX, row_r.Max.y);
                        line_off_curr_x += flip_label ? -line_off_step_x : line_off_step_x;
                        pivot_out += unit_right * padding.y;
                        if (flip_label)
                            pivot_out += unit_right * (clip_width - ImMath.ImMax(0.0f, clip_width - label_size.x));
                        pivot_out.x += flip_label ? line_off_curr_x + line_off_step_x : line_off_curr_x;
                        ImGuiCore.ShadeVertsTransformPos(draw_list, (int)vtx_idx_begin, (int)vtx_idx_end, pivot_in, label_cos_a, label_sin_a, pivot_out); // Rotate and offset
                        //if (g.IO.KeyShift) { ... } // [DEBUG]

                        label_name_pos = label_name_eol + 1;
                    }
                }
                if (pass == 1)
                {
                    // Draw border
                    draw_list.AddLine(bg_shape[0] - new ImVec2(TABLE_BORDER_SIZE * 0.5f, 0.0f), bg_shape[3] - new ImVec2(TABLE_BORDER_SIZE * 0.5f, 0.0f), TableGetColumnBorderCol(table, order_n, column_n), TABLE_BORDER_SIZE);
                }
            }
        PopClipRect();
        PopClipRect();
        table.TempData.AngledHeadersExtraWidth = ImMath.ImMax(0.0f, max_x - table.Columns[table.RightMostEnabledColumn].MaxX);
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Context Menu (imgui_tables.cpp 3636-3829)
    //-------------------------------------------------------------------------

    // Use -1 to open menu not specific to a given column.
    // C++: void ImGui::TableOpenContextMenu(int column_n)
    public static void TableOpenContextMenu(int column_n = -1)
    {
        ImGuiContext g = GImGui;
        ImGuiTable table = g.CurrentTable;
        if (column_n == -1 && table.CurrentColumn != -1)    // When called within a column automatically use this one (for consistency)
            column_n = table.CurrentColumn;
        if (column_n == table.ColumnsCount)                 // To facilitate using with TableGetHoveredColumn()
            column_n = -1;
        Debug.Assert(column_n >= -1 && column_n < table.ColumnsCount);
        if ((table.Flags & (ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable)) != 0)
        {
            table.IsContextPopupOpen = true;
            table.ContextPopupColumn = (short)column_n;
            table.InstanceInteracted = table.InstanceCurrent;
            ImGuiID context_menu_id = ImGuiHash.ImHashStr("##ContextMenu", 0, table.ID);
            OpenPopupEx(context_menu_id, ImGuiPopupFlags_None);
        }
    }

    // C++: bool ImGui::TableBeginContextMenuPopup(ImGuiTable* table)
    public static bool TableBeginContextMenuPopup(ImGuiTable table)
    {
        if (!table.IsContextPopupOpen || table.InstanceCurrent != table.InstanceInteracted)
            return false;
        ImGuiID context_menu_id = ImGuiHash.ImHashStr("##ContextMenu", 0, table.ID);
        if (BeginPopupEx(context_menu_id, ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoSavedSettings))
            return true;
        table.IsContextPopupOpen = false;
        return false;
    }

    // FIXME: Copied from MenuItem() for the purpose of being able to pass _SelectOnRelease (#9312)
    // C++: static bool MenuItemForColumnReorder(const char* label, bool selected, bool enabled)
    static bool MenuItemForColumnReorder(string label, bool selected, bool enabled)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        ImVec2 label_size = CalcTextSize(label, -1, true);
        ImGuiMenuColumns offsets = window.DC.MenuColumns;
        float checkmark_w = ImMath.ImTrunc(g.FontSize * 1.20f);
        float min_w = offsets.DeclColumns(0.0f, label_size.x, 0.0f, checkmark_w); // Feedback for next frame
        float stretch_w = ImMath.ImMax(0.0f, GetContentRegionAvail().x - min_w);
        ImVec2 text_pos = new ImVec2(window.DC.CursorPos.x, window.DC.CursorPos.y + window.DC.CurrLineTextBaseOffset);

        ImGuiID id = GetID(label);
        ImGuiSelectableFlags selectable_flags = ImGuiSelectableFlags_SelectOnRelease | ImGuiSelectableFlags_SpanAvailWidth;
        if (g.ActiveId == id)
            selectable_flags |= ImGuiSelectableFlags_Highlight; // Stays highlighted while dragging.
        bool has_been_moved = (g.ActiveId == id) && g.ActiveIdHasBeenEditedBefore; // But disable toggling once moved.

        BeginDisabled(!enabled); // Don't use ImGuiSelectableFlags_Disabled so that Check mark is also affected.
        bool ret = Selectable(label, false, selectable_flags, new ImVec2(min_w, label_size.y)) && !has_been_moved; // Can't use IsMouseDragging(0) as button is released already.
        if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Visible) != 0 && selected)
            RenderCheckMark(window.DrawList, text_pos + new ImVec2(offsets.OffsetMark + stretch_w + g.FontSize * 0.40f, g.FontSize * 0.134f * 0.5f), GetColorU32(ImGuiCol_Text), g.FontSize * 0.866f);
        EndDisabled();

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        return ret;
    }

    // Output context menu into current window (generally a popup)
    // FIXME-TABLE: Ideally this should be writable by the user. Full programmatic access to that data?
    // Sections to display are pulled from 'flags_for_section_to_display', which is typically == table->Flags.
    // - ImGuiTableFlags_Resizable   -> display Sizing menu items
    // - ImGuiTableFlags_Reorderable -> display "Reset Order"
    ////- ImGuiTableFlags_Sortable   -> display sorting options (disabled)
    // - ImGuiTableFlags_Hideable    -> display columns visibility menu items
    // It means if you have a custom context menus you can call this section and omit some sections, and add your own.
    // C++: void ImGui::TableDrawDefaultContextMenu(ImGuiTable* table, ImGuiTableFlags flags_for_section_to_display)
    public static void TableDrawDefaultContextMenu(ImGuiTable table, ImGuiTableFlags flags_for_section_to_display)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        bool want_separator = false;
        int context_column_n = (table.ContextPopupColumn >= 0 && table.ContextPopupColumn < table.ColumnsCount) ? table.ContextPopupColumn : -1;
        ImGuiTableColumn context_column = (context_column_n != -1) ? table.Columns[context_column_n] : null;

        // Sizing
        if ((flags_for_section_to_display & ImGuiTableFlags_Resizable) != 0)
        {
            if (context_column != null)
            {
                bool can_resize = (context_column.Flags & ImGuiTableColumnFlags_NoResize) == 0 && context_column.IsEnabled;
                if (MenuItem(LocalizeGetMsg(ImGuiLocKey_TableSizeOne), null, false, can_resize)) // "###SizeOne"
                    TableSetColumnWidthAutoSingle(table, context_column_n);
            }

            string size_all_desc;
            if (table.ColumnsEnabledFixedCount == table.ColumnsEnabledCount && (table.Flags & ImGuiTableFlags_SizingMask_) != ImGuiTableFlags_SizingFixedSame)
                size_all_desc = LocalizeGetMsg(ImGuiLocKey_TableSizeAllFit);        // "###SizeAll" All fixed
            else
                size_all_desc = LocalizeGetMsg(ImGuiLocKey_TableSizeAllDefault);    // "###SizeAll" All stretch or mixed
            if (MenuItem(size_all_desc, null))
                TableSetColumnWidthAutoAll(table);
            want_separator = true;
        }

        // Reset Order/Visibility etc.
        if ((flags_for_section_to_display & (ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable)) != 0)
            if (BeginMenu(LocalizeGetMsg(ImGuiLocKey_TableReset)))
            {
                //if (MenuItem(LocalizeGetMsg(ImGuiLocKey_TableResetAll))) // FIXME: Hiding because altering Sort Order + requesting auto-fit simultaneously has a tendency to exhibit a sizing glitch.
                //    table->IsResetAllRequest = true;
                if ((flags_for_section_to_display & ImGuiTableFlags_Reorderable) != 0)
                    if (MenuItem(LocalizeGetMsg(ImGuiLocKey_TableResetOrder), null, false, !table.IsDefaultDisplayOrder)) // PS: cannot be hidden because it would mess with drag reordering.
                        table.IsResetDisplayOrderRequest = true;
                if ((flags_for_section_to_display & ImGuiTableFlags_Hideable) != 0)
                    if (MenuItem(LocalizeGetMsg(ImGuiLocKey_TableResetVisibility), null, false, !table.IsDefaultVisibility))
                        table.IsResetVisibilityRequest = true;
                EndMenu();
            }

        // Sorting
        // (modify TableOpenContextMenu() to add _Sortable flag if enabling this)
        // C++ #if 0 block omitted

        // Hiding / Visibility
        if ((flags_for_section_to_display & ImGuiTableFlags_Hideable) != 0)
        {
            if (want_separator)
                Separator();
            want_separator = true;

            // While reordering: we calculate min/max allowed range once here so we can avoid a O(N log N) in the loop (because the query itself does a sweep scan).
            // This assume that reordering constraints output a single range, otherwise would need to either call TableGetMaxDisplayOrderAllowed() for each item below, or cache this once per frame into columns.
            bool is_reordering = (g.ActiveId != 0 && g.ActiveIdWindow == g.CurrentWindow && table.ReorderColumn != -1 && g.ActiveIdHasBeenEditedBefore); // FIXME: This is a bit of a hack.
            int reorder_src_order = is_reordering ? table.Columns[table.ReorderColumn].DisplayOrder : -1;
            int reorder_min_order = is_reordering ? TableGetMaxDisplayOrderAllowed(table, reorder_src_order, 0) : 0;
            int reorder_max_order = is_reordering ? TableGetMaxDisplayOrderAllowed(table, reorder_src_order, table.ColumnsCount - 1) : table.ColumnsCount - 1;
            PushItemFlag(ImGuiItemFlags_AutoClosePopups, false);
            for (int order_n = 0; order_n < table.ColumnsCount; order_n++)
            {
                int column_n = table.DisplayOrderToIndex[order_n];
                ImGuiTableColumn column = table.Columns[column_n];
                if ((column.Flags & ImGuiTableColumnFlags_Disabled) != 0)
                    continue;

                string name = TableGetColumnName(table, column_n);
                if (name == null || name.Length == 0)
                    name = "<Unknown>";

                // Make sure we can't hide the last active column
                bool menu_item_enabled = (column.Flags & ImGuiTableColumnFlags_NoHide) == 0;
                if (column.IsUserEnabled && table.ColumnsEnabledCount <= 1)
                    menu_item_enabled = false;
                if (is_reordering && (column.DisplayOrder < reorder_min_order || column.DisplayOrder > reorder_max_order))
                    menu_item_enabled = false;
                if (MenuItemForColumnReorder(name, column.IsUserEnabled, menu_item_enabled))
                    column.IsUserEnabledNextFrame = !column.IsUserEnabled;

                // Drag to reorder
                // FIXME: It is currently not possible to reorder columns marked with ImGuiTableColumnFlags_NoHide.
                if (IsItemActive() && IsMouseDragging(ImGuiMouseButton_Left) && g.ActiveIdSource == ImGuiInputSource_Mouse && (table.Flags & ImGuiTableFlags_Reorderable) != 0)
                {
                    g.ActiveIdHasBeenEditedBefore = true; // Disable toggle in MenuItemForColumnReorder() + start dimming to display allowed reorder targets.
                    table.ReorderColumn = (short)column_n;
                    if (!IsItemHovered())
                    {
                        int reorder_dir = (g.IO.MousePos.y < (g.LastItemData.Rect.Min.y + g.LastItemData.Rect.Max.y) * 0.5f) ? -1 : +1;
                        float reorder_amount = (reorder_dir < 0 ? g.LastItemData.Rect.Min.y - g.IO.MousePos.y : g.IO.MousePos.y - g.LastItemData.Rect.Max.y) / g.LastItemData.Rect.GetHeight();
                        int dst_order = column.DisplayOrder + (int)ImMath.ImCeil(reorder_amount) * reorder_dir; // Estimated target order, will be validated and clamped.
                        TableQueueSetColumnDisplayOrder(table, column_n, dst_order);
                    }
                }
            }
            PopItemFlag();
        }
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Settings (.ini data) (imgui_tables.cpp 3829-4264)
    //-------------------------------------------------------------------------
    // FIXME: The binding/finding/creating flow are too confusing.
    //-------------------------------------------------------------------------
    // [Init] 1: TableSettingsHandler_ReadXXXX()   Load and parse .ini file into TableSettings.
    // [Main] 2: TableLoadSettings()               When table is created, bind Table to TableSettings, serialize TableSettings data into Table.
    // [Main] 3: TableSaveSettings()               When table properties are modified, serialize Table data into bound or new TableSettings, mark .ini as dirty.
    // [Main] 4: TableSettingsHandler_WriteAll()   When .ini file is dirty (which can come from other source), save TableSettings into .ini file.
    //-------------------------------------------------------------------------

    // Clear and initialize empty settings instance
    // C++: static void TableSettingsInit(ImGuiTableSettings* settings, ImGuiID id, int columns_count, int columns_count_max)
    // C# note: C++ IM_PLACEMENT_NEW re-inits the chunk in place; we reset fields and (re)create the column settings entries.
    static void TableSettingsInit(ImGuiTableSettings settings, ImGuiID id, int columns_count, int columns_count_max)
    {
        settings.ID = 0;
        settings.SaveFlags = 0;
        settings.RefScale = 0.0f;
        settings.ColumnsCount = 0;
        settings.ColumnsCountMax = 0;
        settings.LastUsedDate = default;
        settings.WantApply = false;
        if (settings._columns.Length != columns_count_max) // C# note: the column settings storage is owned by the settings object (C++: trailing chunk memory)
        {
            settings._columns = new ImGuiTableColumnSettings[columns_count_max];
            for (int n = 0; n < columns_count_max; n++)
                settings._columns[n] = new ImGuiTableColumnSettings();
        }
        else
        {
            for (int n = 0; n < columns_count_max; n++)
                settings._columns[n] = new ImGuiTableColumnSettings(); // C++: IM_PLACEMENT_NEW(settings_column) ImGuiTableColumnSettings()
        }
        settings.ID = id;
        settings.ColumnsCount = (short)columns_count;
        settings.ColumnsCountMax = (short)columns_count_max;
        settings.WantApply = true;
    }

    // C# note: with the object-based ImChunkStream<ImGuiTableSettings> the byte chunk size no longer drives
    // allocation (kept for fidelity with the C++ code, TableGcCompactSettings() uses counts instead).
    // C++: static size_t TableSettingsCalcChunkSize(int columns_count)
    internal static int TableSettingsCalcChunkSize(int columns_count)
    {
        return 0 + columns_count; // C++: sizeof(ImGuiTableSettings) + (size_t)columns_count * sizeof(ImGuiTableColumnSettings)
    }

    // C++: ImGuiTableSettings* ImGui::TableSettingsCreate(ImGuiID id, int columns_count)
    public static ImGuiTableSettings TableSettingsCreate(ImGuiID id, int columns_count)
    {
        ImGuiContext g = GImGui;
        ImGuiTableSettings settings = TableSettingsFindByID(id);
        if (settings != null)
        {
            if (settings.ColumnsCountMax >= columns_count)
            {
                TableSettingsInit(settings, id, columns_count, settings.ColumnsCountMax); // Recycle
                return settings;
            }
            settings.ID = 0; // Invalidate storage, we won't fit because of a count change
        }
        settings = new ImGuiTableSettings();
        g.SettingsTables.alloc_chunk(settings); // C++: settings = g.SettingsTables.alloc_chunk(TableSettingsCalcChunkSize(columns_count))
        TableSettingsInit(settings, id, columns_count, columns_count);
        return settings;
    }

    // Find existing settings
    // C++: ImGuiTableSettings* ImGui::TableSettingsFindByID(ImGuiID id)
    public static ImGuiTableSettings TableSettingsFindByID(ImGuiID id)
    {
        // FIXME-OPT: Might want to store a lookup map for this?
        ImGuiContext g = GImGui;
        for (int i = 0; i < g.SettingsTables.size(); i++) // C++: for (settings = g.SettingsTables.begin(); settings != NULL; settings = g.SettingsTables.next_chunk(settings))
        {
            ImGuiTableSettings settings = g.SettingsTables[i];
            if (settings.ID == id)
                return settings;
        }
        return null;
    }

    // Get settings for a given table, NULL if none
    // C++: ImGuiTableSettings* ImGui::TableGetBoundSettings(ImGuiTable* table)
    public static ImGuiTableSettings TableGetBoundSettings(ImGuiTable table)
    {
        if (table.SettingsOffset == -1)
            return null;
        ImGuiContext g = GImGui;
        ImGuiTableSettings settings = g.SettingsTables.ptr_from_offset(table.SettingsOffset);
        Debug.Assert(settings.ID == table.ID);
        return settings;
    }

    // Restore initial state of table (with or without saved settings)
    // C++: void ImGui::TableResetSettings(ImGuiTable* table)
    public static void TableResetSettings(ImGuiTable table)
    {
        table.IsInitializing = table.IsSettingsDirty = true;
        table.IsResetAllRequest = false;
        table.IsSettingsRequestLoad = false;               // Don't reload from ini
        table.SettingsLoadedFlags = ImGuiTableFlags_None;  // Mark as nothing loaded so our initialized data becomes authoritative
    }

    // C++: void ImGui::TableSaveSettings(ImGuiTable* table)
    public static void TableSaveSettings(ImGuiTable table)
    {
        table.IsSettingsDirty = false;
        if ((table.Flags & ImGuiTableFlags_NoSavedSettings) != 0)
            return;

        // Bind or create settings data
        ImGuiContext g = GImGui;
        ImGuiTableSettings settings = TableGetBoundSettings(table);
        if (settings != null && table.ColumnsCount > settings.ColumnsCountMax)
        {
            settings.ID = 0; // Invalidate storage, we won't fit because of a count change
            settings = null;
        }
        if (settings == null)
        {
            settings = TableSettingsCreate(table.ID, table.ColumnsCount);
            table.SettingsOffset = g.SettingsTables.offset_from_ptr(settings);
        }
        settings.ColumnsCount = (short)table.ColumnsCount;
        settings.LastUsedDate = g.SessionDate;

        // Serialize ImGuiTable/ImGuiTableColumn into ImGuiTableSettings/ImGuiTableColumnSettings
        Debug.Assert(settings.ID == table.ID);
        Debug.Assert(settings.ColumnsCount == table.ColumnsCount && settings.ColumnsCountMax >= settings.ColumnsCount);
        ImGuiTableColumnSettings[] column_settings = settings.GetColumnSettings();

        bool save_ref_scale = false;
        settings.SaveFlags = ImGuiTableFlags_None;
        for (int n = 0; n < table.ColumnsCount; n++)
        {
            ImGuiTableColumn column = table.Columns[n];
            ImGuiTableColumnSettings column_setting = column_settings[n];
            float width_or_weight = (column.Flags & ImGuiTableColumnFlags_WidthStretch) != 0 ? column.StretchWeight : column.WidthRequest;
            column_setting.ID = column.ID;
            column_setting.WidthOrWeight = width_or_weight;
            column_setting.Index = (short)n;
            column_setting.DisplayOrder = column.DisplayOrder;
            column_setting.SortOrder = column.SortOrder;
            column_setting.SortDirection = column.SortDirection;
            column_setting.IsEnabled = (sbyte)(column.IsUserEnabled ? 1 : 0);
            column_setting.IsStretch = (column.Flags & ImGuiTableColumnFlags_WidthStretch) != 0;
            if ((column.Flags & ImGuiTableColumnFlags_WidthStretch) == 0)
                save_ref_scale = true;

            // We skip saving some data in the .ini file when they are unnecessary to restore our state.
            // Note that fixed width where initial width was derived from auto-fit will always be saved as InitStretchWeightOrWidth will be 0.0f.
            // FIXME-TABLE: We don't have logic to easily compare SortOrder to DefaultSortOrder yet so it's always saved when present.
            if (width_or_weight != column.InitStretchWeightOrWidth)
                settings.SaveFlags |= ImGuiTableFlags_Resizable;
            if (column.DisplayOrder != n)
                settings.SaveFlags |= ImGuiTableFlags_Reorderable;
            if (column.SortOrder != -1)
                settings.SaveFlags |= ImGuiTableFlags_Sortable;
            if (column.IsUserEnabled != ((column.Flags & ImGuiTableColumnFlags_DefaultHide) == 0))
                settings.SaveFlags |= ImGuiTableFlags_Hideable;
        }
        if ((table.Flags & ImGuiTableFlags_Sortable) != 0)
            settings.SaveFlags |= ImGuiTableFlags_Sortable | ImGuiTableFlags_Reorderable;
        settings.SaveFlags &= table.Flags;
        settings.RefScale = save_ref_scale ? table.RefScale : 0.0f;

        MarkIniSettingsDirty();
    }

    // C++: void ImGui::TableLoadSettings(ImGuiTable* table)
    public static void TableLoadSettings(ImGuiTable table)
    {
        ImGuiContext g = GImGui;
        if ((table.Flags & ImGuiTableFlags_NoSavedSettings) != 0)
        {
            table.IsSettingsRequestLoad = false; // Done
            return;
        }

        // Bind settings
        ImGuiTableSettings settings;
        if (table.SettingsOffset == -1)
        {
            settings = TableSettingsFindByID(table.ID);
            if (settings == null)
                return;
            if (settings.ColumnsCount != table.ColumnsCount) // Allow settings if columns count changed. We could otherwise decide to return...
                table.IsSettingsDirty = true;
            table.SettingsOffset = g.SettingsTables.offset_from_ptr(settings);
        }
        else
        {
            settings = TableGetBoundSettings(table);
        }

        table.SettingsLoadedFlags = settings.SaveFlags;
        table.RefScale = settings.RefScale;
        settings.LastUsedDate = g.SessionDate;
        // TableUpdateLayout() will then call TableLoadSettingsForColumns() to apply the data.
    }

    // NB: This was written to be similar to the logic in TableReconcileColumns().
    // C++: void ImGui::TableLoadSettingsForColumns(ImGuiTable* table)
    public static void TableLoadSettingsForColumns(ImGuiTable table)
    {
        for (int n = 0; n < table.Columns.Length; n++) // C++: for (ImGuiTableColumn& column : table->Columns)
            table.Columns[n].IsLoadedSettings = false;
        ImGuiTableSettings settings = TableGetBoundSettings(table);
        if (settings == null)
            return;

        table.SettingsLoadedFlags |= ImGuiTableFlags_Reorderable; // We handle above in code above.

        // Serialize ImGuiTableSettings/ImGuiTableColumnSettings into ImGuiTable/ImGuiTableColumn
        ImGuiTableColumnSettings[] column_settings = settings.GetColumnSettings();

        // Fast path
        int matches = 0;
        for (int n = 0; n < table.ColumnsCount; n++)
        {
            if (n >= settings.ColumnsCount || column_settings[n].ID != table.Columns[n].ID)
                break;
            TableLoadSettingsForColumn(table.Columns[n], column_settings[n], settings.SaveFlags);
            matches++;
        }
        if (matches == settings.ColumnsCount)
            return;
        int settings_start_n = matches; // Small optimization
        for (int n = settings_start_n; n < settings.ColumnsCount; n++)
            column_settings[n].IsLoaded = false;

        // Find matches for named columns
        for (int column_i = 0; column_i < table.Columns.Length; column_i++) // C++: for (ImGuiTableColumn& column : table->Columns)
        {
            ImGuiTableColumn column = table.Columns[column_i];
            if (column.ID != 0 && !column.IsLoadedSettings)
                for (int n = settings_start_n; n < settings.ColumnsCount; n++)
                    if (column_settings[n].ID == column.ID && !column_settings[n].IsLoaded)
                    {
                        TableLoadSettingsForColumn(column, column_settings[n], settings.SaveFlags);
                        matches++;
                        break;
                    }
        }

        // Remaining entries are matched sequentially
        int dst_idx = 0;
        for (int n = settings_start_n; n < settings.ColumnsCount; n++)
            if (!column_settings[n].IsLoaded)
            {
                while (dst_idx < table.ColumnsCount && table.Columns[dst_idx].IsLoadedSettings)
                    dst_idx++;
                if (dst_idx >= table.ColumnsCount)
                    break;
                TableLoadSettingsForColumn(table.Columns[dst_idx], column_settings[n], settings.SaveFlags);
                dst_idx++;
            }
    }

    // C++: void ImGui::TableLoadSettingsForColumn(ImGuiTableColumn* column, ImGuiTableColumnSettings* column_settings, ImGuiTableFlags load_flags)
    static void TableLoadSettingsForColumn(ImGuiTableColumn column, ImGuiTableColumnSettings column_settings, ImGuiTableFlags load_flags)
    {
        column.IsLoadedSettings = true;
        column_settings.IsLoaded = true;
        if ((load_flags & ImGuiTableFlags_Resizable) != 0)
        {
            if (column_settings.IsStretch)
                column.StretchWeight = column_settings.WidthOrWeight;
            else
                column.WidthRequest = column_settings.WidthOrWeight;
            column.AutoFitQueue = 0x00;
        }
        if ((load_flags & ImGuiTableFlags_Reorderable) != 0)
            column.DisplayOrder = column_settings.DisplayOrder;
        else
            column.DisplayOrder = column_settings.Index; // Because default depends on previous Index, we need to set that up and cannot rely on TableInitColumnDefaults()
        if ((load_flags & ImGuiTableFlags_Hideable) != 0 && column_settings.IsEnabled != -1)
            column.IsUserEnabled = column.IsUserEnabledNextFrame = (column_settings.IsEnabled == 1);
        column.SortOrder = column_settings.SortOrder;
        column.SortDirection = column_settings.SortDirection;
    }

    // Local storage for TableFixDisplayOrder(). C++ local struct:
    //   struct ImGuiTableFixDisplayOrderColumnData { ImGuiTableColumnIdx Idx; ImGuiTable* Table; };
    private struct TableFixDisplayOrderColumnData
    {
        public short Idx;          // ImGuiTableColumnIdx
        public ImGuiTable Table;   // This is unfortunate but we don't have userdata in qsort api.
    }

    // Sort by DisplayOrder and then Index
    // C++: static int IMGUI_CDECL TableFixDisplayOrderComparer(const void* lhs, const void* rhs)
    static int TableFixDisplayOrderComparer(TableFixDisplayOrderColumnData lhs, TableFixDisplayOrderColumnData rhs)
    {
        ImGuiTable table = lhs.Table;
        short lhs_idx = lhs.Idx;
        short rhs_idx = rhs.Idx;
        // Assume ImGuiTableColumnIdx == signed short, to turn -1 into a large value so that it always sort after.
        int order_delta = (int)(ushort)table.Columns[lhs_idx].DisplayOrder - (int)(ushort)table.Columns[rhs_idx].DisplayOrder;
        return (order_delta > 0) ? +1 : (order_delta < 0) ? -1 : (lhs_idx > rhs_idx) ? +1 : -1;
    }

    // Fix invalid display order data: compact values (0,1,3 -> 0,1,2); preserve relative order (0,3,1 -> 0,2,1); deduplicate (0,4,1,1 -> 0,3,1,2)
    // C++: void ImGui::TableFixDisplayOrder(ImGuiTable* table)
    public static void TableFixDisplayOrder(ImGuiTable table)
    {
        ImGuiContext g = GImGui;
        // C++: stashes ImGuiTableFixDisplayOrderColumnData array into g.TempBuffer + ImQsort(); C# uses a local array + Array.Sort.
        TableFixDisplayOrderColumnData[] fdo_columns = new TableFixDisplayOrderColumnData[table.ColumnsCount];
        for (int n = 0; n < table.ColumnsCount; n++)
        {
            fdo_columns[n].Idx = (short)n;
            fdo_columns[n].Table = table;
        }
        Array.Sort(fdo_columns, TableFixDisplayOrderComparer); // C++: ImQsort(..., TableFixDisplayOrderComparer)
        for (int n = 0; n < table.ColumnsCount; n++)
            table.Columns[fdo_columns[n].Idx].DisplayOrder = (short)n;
        for (int n = 0; n < table.ColumnsCount; n++)
            table.DisplayOrderToIndex[table.Columns[n].DisplayOrder] = (short)n;
    }

    // ---- TableSettingsHandler_*** (imgui_tables.cpp) ----
    // C# note: the g.SettingsTables chunk stream is the object-based ImChunkStream<ImGuiTableSettings>.

    // C++: static void TableSettingsHandler_ClearAll(ImGuiContext* ctx, ImGuiSettingsHandler*)
    static void TableSettingsHandler_ClearAll(ImGuiContext ctx, ImGuiSettingsHandler handler)
    {
        ImGuiContext g = ctx;
        for (int i = 0; i != g.Tables.GetMapSize(); i++)
            if (g.Tables.TryGetMapData(i) is ImGuiTable table)
                table.SettingsOffset = -1;
        g.SettingsTables.clear();
    }

    // C++: static void TableSettingsHandler_Cleanup(ImGuiContext* ctx, ImGuiSettingsHandler*, ImGuiSettingsCleanupArgs* args)
    static void TableSettingsHandler_Cleanup(ImGuiContext ctx, ImGuiSettingsHandler handler, ImGuiSettingsCleanupArgs args)
    {
        ImGuiContext g = ctx;
        for (int i = 0; i != g.Tables.GetMapSize(); i++)
            if (g.Tables.TryGetMapData(i) is ImGuiTable table)
                table.SettingsOffset = -1;
        for (int i = 0; i < g.SettingsTables.size(); i++) // C++: begin()/next_chunk() iteration
        {
            ImGuiTableSettings settings = g.SettingsTables[i];
            bool is_valid = settings.LastUsedDate.IsValid();
            if (args._DiscardOlderThanDate != 0 && settings.LastUsedDate.ToYYYYMMDD() < args._DiscardOlderThanDate) // C++: settings->LastUsedDate.Unpack()
                settings.ID = 0;
            else if (args.DiscardWhenMissingDate && !is_valid)
                settings.ID = 0;
            else if (args.SetCurrentSessionDateToAll || (args.SetCurrentSessionDateWhenMissingDate && !is_valid))
                settings.LastUsedDate = g.SessionDate;
        }
    }

    // Apply to existing windows (if any)
    // C++: static void TableSettingsHandler_ApplyAll(ImGuiContext* ctx, ImGuiSettingsHandler*)
    static void TableSettingsHandler_ApplyAll(ImGuiContext ctx, ImGuiSettingsHandler handler)
    {
        ImGuiContext g = ctx;
        for (int i = 0; i != g.Tables.GetMapSize(); i++)
            if (g.Tables.TryGetMapData(i) is ImGuiTable table)
            {
                table.IsSettingsRequestLoad = true;
                table.SettingsOffset = -1;
            }
    }

    // ---- sscanf()-style scanning helpers for the [Table] .ini handler ----
    // (mirrors sscanf(name, "0x%08X,%d", ...) / sscanf(line, "Width=%d%n", ...) usage; local to this handler)

    static int TableSettingsSkipBlank(string s, int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
            i++;
        return i;
    }

    static int TableSettingsMatchPrefix(string s, int i, string prefix)
    {
        if (i + prefix.Length > s.Length)
            return -1;
        return string.CompareOrdinal(s, i, prefix, 0, prefix.Length) == 0 ? i + prefix.Length : -1;
    }

    // sscanf %X (hex, up to 8 digits like "%08X")
    static int TableSettingsScanHexU32(string s, int i, out uint value)
    {
        value = 0;
        int digits = 0;
        while (i < s.Length && digits < 8)
        {
            char c = s[i];
            uint d;
            if (c >= '0' && c <= '9') d = (uint)(c - '0');
            else if (c >= 'A' && c <= 'F') d = (uint)(c - 'A' + 10);
            else if (c >= 'a' && c <= 'f') d = (uint)(c - 'a' + 10);
            else break;
            value = value * 16 + d;
            i++; digits++;
        }
        return digits > 0 ? i : -1;
    }

    // sscanf %d (signed decimal)
    static int TableSettingsScanInt(string s, int i, out int value)
    {
        value = 0;
        int sign = 1;
        if (i < s.Length && (s[i] == '-' || s[i] == '+')) { if (s[i] == '-') sign = -1; i++; }
        int digits = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = value * 10 + (s[i] - '0');
            i++; digits++;
        }
        value *= sign;
        return digits > 0 ? i : -1;
    }

    // sscanf %f (decimal float)
    static int TableSettingsScanFloat(string s, int i, out float value)
    {
        value = 0.0f;
        int start = i;
        int end = i;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '-' || s[end] == '+' || s[end] == '.' || s[end] == 'e' || s[end] == 'E'))
            end++;
        if (end == start || !float.TryParse(s.Substring(start, end - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
            return -1;
        return end;
    }

    // C++: static void* TableSettingsHandler_ReadOpen(ImGuiContext*, ImGuiSettingsHandler*, const char* name)
    static object TableSettingsHandler_ReadOpen(ImGuiContext ctx, ImGuiSettingsHandler handler, string name)
    {
        // FIXME: As topology changes are allowed, strictly speaking a >= IMGUI_TABLE_MAX_COLUMNS tables stored in .ini file
        // that was emitted with a higher max count could still be meaningful in some unlikely cases.
        // We might want to use another MAX defined as (1<<(sizeof(ImGuiTableColumnIdx)*8-1))-1.
        uint id = 0;
        int columns_count = 0;
        // sscanf(name, "0x%08X,%d", &id, &columns_count) < 2
        int pos = TableSettingsMatchPrefix(name, 0, "0x");
        bool ok = pos != -1;
        if (ok) { pos = TableSettingsScanHexU32(name, pos, out id); ok = pos != -1; }
        if (ok) ok = pos < name.Length && name[pos] == ',';
        if (ok) { pos = TableSettingsScanInt(name, pos + 1, out columns_count); ok = pos != -1; }
        if (!ok || columns_count <= 0 || columns_count >= IMGUI_TABLE_MAX_COLUMNS)
            return null;
        return TableSettingsCreate(id, columns_count);
    }

    // C++: static void TableSettingsHandler_ReadLine(ImGuiContext*, ImGuiSettingsHandler*, void* entry, const char* line)
    static void TableSettingsHandler_ReadLine(ImGuiContext ctx, ImGuiSettingsHandler handler, object entry, string line)
    {
        // "Column 0  UserID=0x42AD2D21 Width=100 Visible=1 Order=0 Sort=0v"
        var settings = (ImGuiTableSettings)entry;

        int pos = TableSettingsMatchPrefix(line, 0, "RefScale=");
        if (pos != -1 && TableSettingsScanFloat(line, pos, out float f) != -1) { settings.RefScale = f; return; } // sscanf(line, "RefScale=%f", &f) == 1
        pos = TableSettingsMatchPrefix(line, 0, "LastUsed=");
        if (pos != -1 && TableSettingsScanInt(line, pos, out int date_n) != -1) { settings.LastUsedDate = ImGuiPackedDate.FromYYYYMMDD(date_n); return; } // sscanf(line, "LastUsed=%d", &n) == 1

        int column_n = 0;
        int r = -1;
        pos = TableSettingsMatchPrefix(line, 0, "Column ");
        if (pos != -1 && (r = TableSettingsScanInt(line, pos, out column_n)) != -1) // sscanf(line, "Column %d%n", &column_n, &r) == 1
        {
            if (column_n < 0 || column_n >= settings.ColumnsCount)
                return;
            int p = TableSettingsSkipBlank(line, r); // C++: line = ImStrSkipBlank(line + r)
            ImGuiTableColumnSettings column = settings.GetColumnSettings()[column_n];
            column.Index = (short)column_n;
            int r2;
            int q = TableSettingsMatchPrefix(line, p, "UserID=0x");
            if (q != -1 && (r2 = TableSettingsScanHexU32(line, q, out _)) != -1) { p = TableSettingsSkipBlank(line, r2); } // FIXME-LEGACY: Removed 2025/11/12, was never properly set.
            q = TableSettingsMatchPrefix(line, p, "Width=");
            if (q != -1 && (r2 = TableSettingsScanInt(line, q, out int w_n)) != -1) { p = TableSettingsSkipBlank(line, r2); column.WidthOrWeight = w_n; column.IsStretch = false; settings.SaveFlags |= ImGuiTableFlags_Resizable; }
            q = TableSettingsMatchPrefix(line, p, "Weight=");
            if (q != -1 && (r2 = TableSettingsScanFloat(line, q, out float w_f)) != -1) { p = TableSettingsSkipBlank(line, r2); column.WidthOrWeight = w_f; column.IsStretch = true; settings.SaveFlags |= ImGuiTableFlags_Resizable; }
            q = TableSettingsMatchPrefix(line, p, "Visible=");
            if (q != -1 && (r2 = TableSettingsScanInt(line, q, out int v_n)) != -1) { p = TableSettingsSkipBlank(line, r2); column.IsEnabled = (sbyte)v_n; settings.SaveFlags |= ImGuiTableFlags_Hideable; }
            q = TableSettingsMatchPrefix(line, p, "Order=");
            if (q != -1 && (r2 = TableSettingsScanInt(line, q, out int o_n)) != -1) { p = TableSettingsSkipBlank(line, r2); column.DisplayOrder = (short)o_n; settings.SaveFlags |= ImGuiTableFlags_Reorderable; }
            q = TableSettingsMatchPrefix(line, p, "Sort=");
            if (q != -1) // sscanf(line, "Sort=%d%c%n", &n, &c, &r) == 2
            {
                int r3 = TableSettingsScanInt(line, q, out int s_n);
                if (r3 != -1 && r3 < line.Length)
                {
                    char c = line[r3];
                    p = TableSettingsSkipBlank(line, r3 + 1);
                    column.SortOrder = (short)s_n;
                    column.SortDirection = (byte)((c == '^') ? ImGuiSortDirection_Descending : ImGuiSortDirection_Ascending);
                    settings.SaveFlags |= ImGuiTableFlags_Sortable;
                }
            }
            q = TableSettingsMatchPrefix(line, p, "ID=0x");
            if (q != -1 && (r2 = TableSettingsScanHexU32(line, q, out uint id_n)) != -1) { column.ID = id_n; }
        }
    }

    // C++: static void TableSettingsHandler_WriteAll(ImGuiContext* ctx, ImGuiSettingsHandler* handler, ImGuiTextBuffer* buf)
    static void TableSettingsHandler_WriteAll(ImGuiContext ctx, ImGuiSettingsHandler handler, ImGuiTextBuffer buf)
    {
        ImGuiContext g = ctx;
        for (int i = 0; i < g.SettingsTables.size(); i++) // C++: begin()/next_chunk() iteration
        {
            ImGuiTableSettings settings = g.SettingsTables[i];
            if (settings.ID == 0) // Skip ditched settings
                continue;

            // TableSaveSettings() may clear some of those flags when we establish that the data can be stripped
            // (e.g. Order was unchanged)
            bool save_size = (settings.SaveFlags & ImGuiTableFlags_Resizable) != 0;
            bool save_visible = (settings.SaveFlags & ImGuiTableFlags_Hideable) != 0;
            bool save_order = (settings.SaveFlags & ImGuiTableFlags_Reorderable) != 0;
            bool save_sort = (settings.SaveFlags & ImGuiTableFlags_Sortable) != 0;
            // We need to save the [Table] entry even if all the bools are false, since this records a table with "default settings".

            //buf->reserve(buf->size() + 30 + settings->ColumnsCount * 50); // ballpark reserve -- omitted (C# append grows dynamically)
            buf.appendf("[%s][0x%08X,%d]\n", handler.TypeName, settings.ID, settings.ColumnsCount);
            if (settings.RefScale != 0.0f)
                buf.appendf("RefScale=%g\n", settings.RefScale);
            ImGuiTableColumnSettings[] column_settings = settings.GetColumnSettings();
            for (int column_n = 0; column_n < settings.ColumnsCount; column_n++)
            {
                ImGuiTableColumnSettings column = column_settings[column_n];
                // "Column 0  UserID=0x42AD2D21 Width=100 Visible=1 Order=0 Sort=0v"
                bool save_column = save_size || save_visible || save_order || (save_sort && column.SortOrder != -1);
                if (!save_column)
                    continue;
                buf.appendf("Column %-2d", column_n);
                if (save_size && column.IsStretch)        { buf.appendf(" Weight=%.4f", column.WidthOrWeight); }
                if (save_size && !column.IsStretch)       { buf.appendf(" Width=%d", (int)column.WidthOrWeight); }
                if (save_visible)                         { buf.appendf(" Visible=%d", column.IsEnabled); }
                if (save_order)                           { buf.appendf(" Order=%d", column.DisplayOrder); }
                if (save_sort && column.SortOrder != -1)  { buf.appendf(" Sort=%d%c", column.SortOrder, (column.SortDirection == ImGuiSortDirection_Ascending) ? 'v' : '^'); }
                if (column.ID != 0)                       { buf.appendf(" ID=0x%08X", column.ID); }
                buf.append("\n");
            }
            if (g.IO.ConfigIniSettingsSaveLastUsedDate)
            {
                int last_used_date = settings.LastUsedDate.ToYYYYMMDD(); // C++: settings->LastUsedDate.Unpack()
                if (last_used_date != 0)
                    buf.appendf("LastUsed=%08d\n", last_used_date);
            }
            buf.append("\n");
        }
    }

    // C++: void ImGui::TableSettingsAddSettingsHandler()   (no params in C++; the C# port keeps the ctx param for call-compat, mirrors the previous stub)
    public static void TableSettingsAddSettingsHandler(ImGuiContext ctx = null)
    {
        ImGuiSettingsHandler ini_handler = new ImGuiSettingsHandler();
        ini_handler.TypeName = "Table";
        ini_handler.TypeHash = ImGuiHash.ImHashStr("Table", 0);
        ini_handler.ClearAllFn = TableSettingsHandler_ClearAll;
        ini_handler.CleanupFn = TableSettingsHandler_Cleanup;
        ini_handler.ReadOpenFn = TableSettingsHandler_ReadOpen;
        ini_handler.ReadLineFn = TableSettingsHandler_ReadLine;
        ini_handler.ApplyAllFn = TableSettingsHandler_ApplyAll;
        ini_handler.WriteAllFn = TableSettingsHandler_WriteAll;
        AddSettingsHandler(ini_handler);
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Garbage Collection (imgui_tables.cpp 4264-4326)
    //-------------------------------------------------------------------------

    // Remove Table data (currently only used by TestEngine)
    // C++: void ImGui::TableRemove(ImGuiTable* table)
    public static void TableRemove(ImGuiTable table)
    {
        //IMGUI_DEBUG_PRINT("TableRemove() id=0x%08X\n", table->ID); -- omitted
        ImGuiContext g = GImGui;
        int table_idx = g.Tables.GetIndex(table);
        //memset(table->RawData.Data, 0, table->RawData.size_in_bytes());
        //memset(table, 0, sizeof(ImGuiTable));
        g.Tables.Remove(table.ID, table);
        g.TablesLastTimeActive[table_idx] = -1.0f;
    }

    // Free up/compact internal Table buffers for when it gets unused
    // C++: void ImGui::TableGcCompactTransientBuffers(ImGuiTable* table)
    public static void TableGcCompactTransientBuffers(ImGuiTable table)
    {
        //IMGUI_DEBUG_PRINT("TableGcCompactTransientBuffers() id=0x%08X\n", table->ID); -- omitted
        ImGuiContext g = GImGui;
        Debug.Assert(table.MemoryCompacted == false);
        table.SortSpecs.Specs = null;
        table.SortSpecsMulti.clear();
        table.IsSortSpecsDirty = true; // FIXME: In theory shouldn't have to leak into user performing a sort on resume.
        table.ColumnsNames.clear();
        table.MemoryCompacted = true;
        for (int n = 0; n < table.ColumnsCount; n++)
            table.Columns[n].NameOffset = -1;
        g.TablesLastTimeActive[g.Tables.GetIndex(table)] = -1.0f;
    }

    // C++: void ImGui::TableGcCompactTransientBuffers(ImGuiTableTempData* temp_data)
    public static void TableGcCompactTransientBuffers(ImGuiTableTempData temp_data)
    {
        temp_data.AngledHeadersRequests.clear();
        temp_data.DrawSplitter.ClearFreeMemory();
        temp_data.LastTimeActive = -1.0f;
    }

    // Compact and remove unused or resize settings data (only TestEngine mark unused, but resizing table down can lead to compaction)
    // C++: void ImGui::TableGcCompactSettings()
    // C# note: with the object-based chunk stream we compact by counting live chunks instead of bytes.
    public static void TableGcCompactSettings()
    {
        ImGuiContext g = GImGui;
        int required_count = 0; // C++: int required_memory = 0
        for (int i = 0; i < g.SettingsTables.size(); i++)
            if (g.SettingsTables[i].ID != 0)
                required_count++; // C++: required_memory += (int)TableSettingsCalcChunkSize(settings->ColumnsCount)
        if (required_count == g.SettingsTables.size()) // C++: if (required_memory == g.SettingsTables.Buf.Size)
            return;
        ImChunkStream<ImGuiTableSettings> new_chunk_stream = new ImChunkStream<ImGuiTableSettings>();
        new_chunk_stream.Buf.reserve(required_count);
        for (int i = 0; i < g.SettingsTables.size(); i++)
            if (g.SettingsTables[i].ID != 0)
                new_chunk_stream.alloc_chunk(g.SettingsTables[i]); // C++: memcpy(new_chunk_stream.alloc_chunk(...), settings, ...)
        g.SettingsTables = new_chunk_stream; // C++: g.SettingsTables.swap(new_chunk_stream)
        for (int table_n = 0; table_n < g.Tables.GetMapSize(); table_n++)
            if (g.Tables.TryGetMapData(table_n) is ImGuiTable table)
                table.SettingsOffset = -1;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Tables: Debugging (imgui_tables.cpp 4328-4459)
    //-------------------------------------------------------------------------

    // C++: static const char* DebugNodeTableGetSizingPolicyDesc(ImGuiTableFlags sizing_policy)
    static string DebugNodeTableGetSizingPolicyDesc(ImGuiTableFlags sizing_policy)
    {
        sizing_policy &= ImGuiTableFlags_SizingMask_;
        if (sizing_policy == ImGuiTableFlags_SizingFixedFit)    { return "FixedFit"; }
        if (sizing_policy == ImGuiTableFlags_SizingFixedSame)   { return "FixedSame"; }
        if (sizing_policy == ImGuiTableFlags_SizingStretchProp) { return "StretchProp"; }
        if (sizing_policy == ImGuiTableFlags_SizingStretchSame) { return "StretchSame"; }
        return "N/A";
    }

    // C++: void ImGui::DebugNodeTable(ImGuiTable* table)
    public static void DebugNodeTable(ImGuiTable table)
    {
        ImGuiContext g = GImGui;
        bool is_active = (table.LastFrameActive >= g.FrameCount - 2); // Note that fully clipped early out scrolling tables will appear as inactive here.
        if (!is_active) { PushStyleColor(ImGuiCol_Text, GetStyleColorVec4(ImGuiCol_TextDisabled)); }
        bool open = TreeNode(table, "Table 0x%08X (%d columns, in '%s')%s", table.ID, table.ColumnsCount, table.OuterWindow.Name, is_active ? "" : " *Inactive*");
        if (!is_active) { PopStyleColor(); }
        if (IsItemHovered())
            GetForegroundDrawList(table.OuterWindow).AddRect(table.OuterRect.Min, table.OuterRect.Max, 0xFF00FFFF); // IM_COL32(255, 255, 0, 255)
        if (IsItemVisible() && table.HoveredColumnBody != -1)
            GetForegroundDrawList(table.OuterWindow).AddRect(GetItemRectMin(), GetItemRectMax(), 0xFF00FFFF); // IM_COL32(255, 255, 0, 255)
        if (!open)
            return;
        if (table.InstanceCurrent > 0)
            Text("** %d instances of same table! Some data below will refer to last instance.", table.InstanceCurrent + 1);
        if (g.IO.ConfigDebugIsDebuggerPresent)
        {
            if (DebugBreakButton("**DebugBreak**", "in BeginTable()"))
                g.DebugBreakInTable = table.ID;
            SameLine();
        }

        bool clear_settings = SmallButton("Clear settings");
        BulletText("OuterRect: Pos: (%.1f,%.1f) Size: (%.1f,%.1f) Sizing: '%s'", table.OuterRect.Min.x, table.OuterRect.Min.y, table.OuterRect.GetWidth(), table.OuterRect.GetHeight(), DebugNodeTableGetSizingPolicyDesc(table.Flags));
        BulletText("ColumnsGivenWidth: %.1f, ColumnsAutoFitWidth: %.1f, InnerWidth: %.1f%s", table.ColumnsGivenWidth, table.ColumnsAutoFitWidth, table.InnerWidth, table.InnerWidth == 0.0f ? " (auto)" : "");
        BulletText("CellPaddingX: %.1f, CellSpacingX: %.1f/%.1f, OuterPaddingX: %.1f", table.CellPaddingX, table.CellSpacingX1, table.CellSpacingX2, table.OuterPaddingX);
        BulletText("HoveredColumnBody: %d, HoveredColumnBorder: %d", table.HoveredColumnBody, table.HoveredColumnBorder);
        BulletText("ResizedColumn: %d, HeldHeaderColumn: %d, ReorderColumn: %d", table.LastResizedColumn, table.LastHeldHeaderColumn, table.ReorderColumn);
        for (int n = 0; n <= table.InstanceCurrent; n++)
        {
            ImGuiTableInstanceData table_instance = TableGetInstanceData(table, n);
            BulletText("Instance %d: HoveredRow: %d, LastOuterHeight: %.2f", n, table_instance.HoveredRowLast, table_instance.LastOuterHeight);
        }
        //BulletText("BgDrawChannels: %d/%d", 0, table->BgDrawChannelUnfrozen);
        float sum_weights = 0.0f;
        for (int n = 0; n < table.ColumnsCount; n++)
            if ((table.Columns[n].Flags & ImGuiTableColumnFlags_WidthStretch) != 0)
                sum_weights += table.Columns[n].StretchWeight;
        for (int n = 0; n < table.ColumnsCount; n++)
        {
            ImGuiTableColumn column = table.Columns[n];
            string name = TableGetColumnName(table, n);
            string buf = ImFmt.Sprintf(
                "Column %d order %d '%s': offset %+.2f to %+.2f%s\n" +
                "Enabled: %d, VisibleX/Y: %d/%d, RequestOutput: %d, SkipItems: %d, DrawChannels: %d,%d\n" +
                "WidthGiven: %.1f, Request/Auto: %.1f/%.1f, StretchWeight: %.3f (%.1f%%)\n" +
                "MinX: %.1f, MaxX: %.1f (%+.1f), ClipRect: %.1f to %.1f (+%.1f)\n" +
                "ContentWidth: %.1f,%.1f, HeadersUsed/Ideal %.1f/%.1f\n" +
                "Sort: %d%s, UserData: 0x%08X, Flags: 0x%04X: %s%s%s..",
                n, column.DisplayOrder, name, column.MinX - table.WorkRect.Min.x, column.MaxX - table.WorkRect.Min.x, (n < table.FreezeColumnsRequest) ? " (Frozen)" : "",
                column.IsEnabled ? 1 : 0, column.IsVisibleX ? 1 : 0, column.IsVisibleY ? 1 : 0, column.IsRequestOutput ? 1 : 0, column.IsSkipItems ? 1 : 0, column.DrawChannelFrozen, column.DrawChannelUnfrozen,
                column.WidthGiven, column.WidthRequest, column.WidthAuto, column.StretchWeight, column.StretchWeight > 0.0f ? (column.StretchWeight / sum_weights) * 100.0f : 0.0f,
                column.MinX, column.MaxX, column.MaxX - column.MinX, column.ClipRect.Min.x, column.ClipRect.Max.x, column.ClipRect.Max.x - column.ClipRect.Min.x,
                column.ContentMaxXFrozen - column.WorkMinX, column.ContentMaxXUnfrozen - column.WorkMinX, column.ContentMaxXHeadersUsed - column.WorkMinX, column.ContentMaxXHeadersIdeal - column.WorkMinX,
                column.SortOrder, (column.SortDirection == ImGuiSortDirection_Ascending) ? " (Asc)" : (column.SortDirection == ImGuiSortDirection_Descending) ? " (Des)" : "", column.UserData, (uint)column.Flags,
                (column.Flags & ImGuiTableColumnFlags_WidthStretch) != 0 ? "WidthStretch " : "",
                (column.Flags & ImGuiTableColumnFlags_WidthFixed) != 0 ? "WidthFixed " : "",
                (column.Flags & ImGuiTableColumnFlags_NoResize) != 0 ? "NoResize " : "");
            Bullet();
            Selectable(buf);
            if (IsItemHovered())
            {
                ImRect r = new ImRect(column.MinX, table.OuterRect.Min.y, column.MaxX, table.OuterRect.Max.y);
                GetForegroundDrawList(table.OuterWindow).AddRect(r.Min, r.Max, 0xFF00FFFF); // IM_COL32(255, 255, 0, 255)
            }
        }
        ImGuiTableSettings settings = TableGetBoundSettings(table);
        if (settings != null)
            DebugNodeTableSettings(settings, table);
        if (clear_settings)
            table.IsResetAllRequest = true; // Queue a call to TableResetSettings()
        TreePop();
    }

    // C++: void ImGui::DebugNodeTableSettings(ImGuiTableSettings* settings, ImGuiTable* table)
    public static void DebugNodeTableSettings(ImGuiTableSettings settings, ImGuiTable table)
    {
        if (settings.ID == 0)
            PushID(settings);
        bool open = TreeNode((IntPtr)settings.ID, "Settings 0x%08X (%d columns)", settings.ID, settings.ColumnsCount); // C++: TreeNode((void*)(intptr_t)settings->ID, ...)
        bool hovered = IsItemHovered();
        if (hovered && table == null && settings.ID != 0)
            table = TableFindByID(settings.ID);
        if (hovered && table != null)
            GetForegroundDrawList(table.OuterWindow).AddRect(table.OuterRect.Min, table.OuterRect.Max, 0xFF00FFFF); // IM_COL32(255, 255, 0, 255)
        if (open)
        {
            BulletText("SaveFlags: 0x%08X", settings.SaveFlags);
            BulletText("ColumnsCount: %d (max %d)", settings.ColumnsCount, settings.ColumnsCountMax);
            for (int n = 0; n < settings.ColumnsCount; n++)
            {
                ImGuiTableColumnSettings column_settings = settings.GetColumnSettings()[n];
                ImGuiSortDirection sort_dir = (column_settings.SortOrder != -1) ? column_settings.SortDirection : ImGuiSortDirection_None;
                BulletText("Column %d Order %d SortOrder %2d %s Vis %d %s %7.3f ID 0x%08X",
                    n, column_settings.DisplayOrder, column_settings.SortOrder,
                    (sort_dir == ImGuiSortDirection_Ascending) ? "Asc" : (sort_dir == ImGuiSortDirection_Descending) ? "Des" : "---",
                    column_settings.IsEnabled, column_settings.IsStretch ? "Weight" : "Width ", column_settings.WidthOrWeight, column_settings.ID);
            }
            TreePop();
        }
        if (settings.ID == 0)
            PopID();
    }
}
