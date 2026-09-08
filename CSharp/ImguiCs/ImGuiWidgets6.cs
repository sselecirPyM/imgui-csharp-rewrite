// Port of:
//   imgui_widgets.cpp [SECTION] Widgets: BeginTabBar, EndTabBar, etc.   (lines 9793-10608)
//   imgui_widgets.cpp [SECTION] Widgets: BeginTabItem, EndTabItem, etc. (lines 10608-11085, to end of file)
//   imgui_tables.cpp  [SECTION] Columns, BeginColumns, EndColumns, etc. (lines 4459-4904, legacy API)
// RenderBullet/Selectable are stubbed elsewhere; TabBarGetTabOrder is an imgui_internal.h inline.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    //-------------------------------------------------------------------------
    // [SECTION] Widgets: BeginTabBar, EndTabBar, etc.
    //-------------------------------------------------------------------------

    // C++: struct ImGuiTabBarSection (layout sections: Leading, Central, Trailing)
    public struct ImGuiTabBarSection
    {
        public int TabCount;                    // Number of tabs in this section.
        public float Width;                     // Sum of width of tabs in this section (after shrinking down)
        public float WidthAfterShrinkMinWidth;
        public float Spacing;                   // Horizontal spacing at the end of the section.
                                                // (C++ ctor memsets: default(T) is already zeroed)
    }

    // C++: static inline int TabItemGetSectionIdx(const ImGuiTabItem* tab)
    public static int TabItemGetSectionIdx(ImGuiTabItem tab)
    {
        return (tab.Flags & ImGuiTabItemFlags_Leading) != 0 ? 0 : (tab.Flags & ImGuiTabItemFlags_Trailing) != 0 ? 2 : 1;
    }

    // C++: static int IMGUI_CDECL TabItemComparerBySection(const void* lhs, const void* rhs)
    public static int TabItemComparerBySection(ImGuiTabItem a, ImGuiTabItem b)
    {
        int a_section = TabItemGetSectionIdx(a);
        int b_section = TabItemGetSectionIdx(b);
        if (a_section != b_section)
            return a_section - b_section;
        return (int)(a.IndexDuringLayout - b.IndexDuringLayout);
    }

    // C++: static int IMGUI_CDECL TabItemComparerByBeginOrder(const void* lhs, const void* rhs)
    public static int TabItemComparerByBeginOrder(ImGuiTabItem a, ImGuiTabItem b)
    {
        return (int)(a.BeginOrder - b.BeginOrder);
    }

    // C++: static ImGuiTabBar* GetTabBarFromTabBarRef(const ImGuiPtrOrIndex& ref)
    public static ImGuiTabBar GetTabBarFromTabBarRef(ImGuiPtrOrIndex ref_)
    {
        ImGuiContext g = GImGui;
        return ref_.Ptr != null ? (ImGuiTabBar)ref_.Ptr : g.TabBars.GetByIndex(ref_.Index);
    }

    // C++: static ImGuiPtrOrIndex GetTabBarRefFromTabBar(ImGuiTabBar* tab_bar)
    public static ImGuiPtrOrIndex GetTabBarRefFromTabBar(ImGuiTabBar tab_bar)
    {
        ImGuiContext g = GImGui;
        if (g.TabBars.Contains(tab_bar))
            return new ImGuiPtrOrIndex(g.TabBars.GetIndex(tab_bar));
        return new ImGuiPtrOrIndex(tab_bar);
    }

    // C++: ImGuiTabBar* ImGui::TabBarFindByID(ImGuiID id)
    public static ImGuiTabBar TabBarFindByID(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        return g.TabBars.GetByKey(id);
    }

    // Remove TabBar data (currently only used by TestEngine)
    // C++: void ImGui::TabBarRemove(ImGuiTabBar* tab_bar)
    public static void TabBarRemove(ImGuiTabBar tab_bar)
    {
        ImGuiContext g = GImGui;
        g.TabBars.Remove(tab_bar.ID, tab_bar);
    }

    // C++: bool ImGui::BeginTabBar(const char* str_id, ImGuiTabBarFlags flags)
    public static bool BeginTabBar(string str_id, ImGuiTabBarFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;

        ImGuiID id = window.GetID(str_id);
        ImGuiTabBar tab_bar = g.TabBars.GetOrAddByKey(id);
        ImRect tab_bar_bb = new ImRect(window.DC.CursorPos.x, window.DC.CursorPos.y, window.WorkRect.Max.x, window.DC.CursorPos.y + g.FontSize + g.Style.FramePadding.y * 2);
        tab_bar.ID = id;
        tab_bar.SeparatorMinX = tab_bar_bb.Min.x - ImMath.ImTrunc(window.WindowPadding.x * 0.5f);
        tab_bar.SeparatorMaxX = tab_bar_bb.Max.x + ImMath.ImTrunc(window.WindowPadding.x * 0.5f);
        //if (g.NavWindow && IsWindowChildOf(g.NavWindow, window, false, false))
        flags |= ImGuiTabBarFlags_IsFocused;
        return BeginTabBarEx(tab_bar, tab_bar_bb, flags);
    }

    // C++: bool ImGui::BeginTabBarEx(ImGuiTabBar* tab_bar, const ImRect& tab_bar_bb, ImGuiTabBarFlags flags)
    public static bool BeginTabBarEx(ImGuiTabBar tab_bar, ImRect tab_bar_bb, ImGuiTabBarFlags flags)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;

        Debug.Assert(tab_bar.ID != 0);
        if ((flags & ImGuiTabBarFlags_DockNode) == 0)
            PushOverrideID(tab_bar.ID);

        // Add to stack
        g.CurrentTabBarStack.push_back(GetTabBarRefFromTabBar(tab_bar));
        g.CurrentTabBar = tab_bar;
        tab_bar.Window = window;

        // Append with multiple BeginTabBar()/EndTabBar() pairs.
        tab_bar.BackupCursorPos = window.DC.CursorPos;
        if (tab_bar.CurrFrameVisible == g.FrameCount)
        {
            window.DC.CursorPos = new ImVec2(tab_bar.BarRect.Min.x, tab_bar.BarRect.Max.y + tab_bar.ItemSpacingY);
            tab_bar.BeginCount++;
            return true;
        }

        // Ensure correct ordering when toggling ImGuiTabBarFlags_Reorderable flag, or when a new tab was added while being not reorderable
        if ((flags & ImGuiTabBarFlags_Reorderable) != (tab_bar.Flags & ImGuiTabBarFlags_Reorderable) || (tab_bar.TabsAddedNew && (flags & ImGuiTabBarFlags_Reorderable) == 0))
        {
            // C++: ImQsort(tab_bar->Tabs.Data, tab_bar->Tabs.Size, sizeof(ImGuiTabItem), TabItemComparerByBeginOrder)
            if ((flags & ImGuiTabBarFlags_DockNode) == 0) // FIXME: TabBar with DockNode can now be hybrid
                Array.Sort(tab_bar.Tabs.Data, 0, tab_bar.Tabs.Size, Comparer<ImGuiTabItem>.Create(TabItemComparerByBeginOrder));
        }
        tab_bar.TabsAddedNew = false;

        // Flags
        if ((flags & ImGuiTabBarFlags_FittingPolicyMask_) == 0)
            flags |= ImGuiTabBarFlags_FittingPolicyDefault_;

        tab_bar.Flags = flags;
        tab_bar.BarRect = tab_bar_bb;
        tab_bar.WantLayout = true; // Layout will be done on the first call to ItemTab()
        tab_bar.PrevFrameVisible = tab_bar.CurrFrameVisible;
        tab_bar.CurrFrameVisible = g.FrameCount;
        tab_bar.PrevTabsContentsHeight = tab_bar.CurrTabsContentsHeight;
        tab_bar.CurrTabsContentsHeight = 0.0f;
        tab_bar.ItemSpacingY = g.Style.ItemSpacing.y;
        tab_bar.FramePadding = g.Style.FramePadding;
        tab_bar.TabsActiveCount = 0;
        tab_bar.LastTabItemIdx = -1;
        tab_bar.BeginCount = 1;

        // Set cursor pos in a way which only be used in the off-chance the user erroneously submits item before BeginTabItem(): items will overlap
        window.DC.CursorPos = new ImVec2(tab_bar.BarRect.Min.x, tab_bar.BarRect.Max.y + tab_bar.ItemSpacingY);

        // Draw separator
        // (it would be misleading to draw this in EndTabBar() suggesting that it may be drawn over tabs, as tab bar are appendable)
        ImU32 col = GetColorU32((flags & ImGuiTabBarFlags_IsFocused) != 0 ? ImGuiCol_TabSelected : ImGuiCol_TabDimmedSelected);
        if (g.Style.TabBarBorderSize > 0.0f)
        {
            float y = tab_bar.BarRect.Max.y;
            window.DrawList.AddRectFilled(new ImVec2(tab_bar.SeparatorMinX, y - g.Style.TabBarBorderSize), new ImVec2(tab_bar.SeparatorMaxX, y), col);
        }
        return true;
    }

    // C++: void ImGui::EndTabBar()
    public static void EndTabBar()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        ImGuiTabBar tab_bar = g.CurrentTabBar;
        if (tab_bar == null)
        {
            Debug.Assert(false, "Mismatched BeginTabBar()/EndTabBar()!"); // IM_ASSERT_USER_ERROR_RET
            return;
        }

        // Fallback in case no TabItem have been submitted
        if (tab_bar.WantLayout)
            TabBarLayout(tab_bar);

        // Restore the last visible height if no tab is visible, this reduce vertical flicker/movement when a tabs gets removed without calling SetTabItemClosed().
        bool tab_bar_appearing = (tab_bar.PrevFrameVisible + 1 < g.FrameCount);
        if (tab_bar.VisibleTabWasSubmitted || tab_bar.VisibleTabId == 0 || tab_bar_appearing)
        {
            tab_bar.CurrTabsContentsHeight = ImMath.ImMax(window.DC.CursorPos.y - tab_bar.BarRect.Max.y, tab_bar.CurrTabsContentsHeight);
            window.DC.CursorPos.y = tab_bar.BarRect.Max.y + tab_bar.CurrTabsContentsHeight;
        }
        else
        {
            window.DC.CursorPos.y = tab_bar.BarRect.Max.y + tab_bar.PrevTabsContentsHeight;
        }
        if (tab_bar.BeginCount > 1)
            window.DC.CursorPos = tab_bar.BackupCursorPos;

        tab_bar.LastTabItemIdx = -1;
        if ((tab_bar.Flags & ImGuiTabBarFlags_DockNode) == 0)
            PopID();

        g.CurrentTabBarStack.pop_back();
        g.CurrentTabBar = g.CurrentTabBarStack.empty() ? null : GetTabBarFromTabBarRef(g.CurrentTabBarStack.back());
    }

    // Scrolling happens only in the central section (leading/trailing sections are not scrolling)
    // C++: static float TabBarCalcScrollableWidth(ImGuiTabBar* tab_bar, ImGuiTabBarSection* sections)
    public static float TabBarCalcScrollableWidth(ImGuiTabBar tab_bar, ImGuiTabBarSection[] sections)
    {
        return tab_bar.BarRect.GetWidth() - sections[0].Width - sections[2].Width - sections[1].Spacing;
    }

    // This is called only once a frame before by the first call to ItemTab()
    // The reason we're not calling it in BeginTabBar() is to leave a chance to the user to call the SetTabItemClosed() functions.
    // C++: static void ImGui::TabBarLayout(ImGuiTabBar* tab_bar)
    public static void TabBarLayout(ImGuiTabBar tab_bar)
    {
        ImGuiContext g = GImGui;
        tab_bar.WantLayout = false;

        // Track selected tab when resizing our parent down
        bool scroll_to_selected_tab = (tab_bar.BarRectPrevWidth > tab_bar.BarRect.GetWidth());
        tab_bar.BarRectPrevWidth = tab_bar.BarRect.GetWidth();

        // Garbage collect by compacting list
        // Detect if we need to sort out tab list (e.g. in rare case where a tab changed section)
        int tab_dst_n = 0;
        bool need_sort_by_section = false;
        ImGuiTabBarSection[] sections = new ImGuiTabBarSection[3]; // Layout sections: Leading, Central, Trailing
        for (int tab_src_n = 0; tab_src_n < tab_bar.Tabs.Size; tab_src_n++)
        {
            ImGuiTabItem tab = tab_bar.Tabs[tab_src_n];
            if (tab.LastFrameVisible < tab_bar.PrevFrameVisible || tab.WantClose)
            {
                // Remove tab
                if (tab_bar.VisibleTabId == tab.ID) { tab_bar.VisibleTabId = 0; }
                if (tab_bar.SelectedTabId == tab.ID) { tab_bar.SelectedTabId = 0; }
                if (tab_bar.NextSelectedTabId == tab.ID) { tab_bar.NextSelectedTabId = 0; }
                continue;
            }
            if (tab_dst_n != tab_src_n)
                tab_bar.Tabs[tab_dst_n] = tab_bar.Tabs[tab_src_n];

            tab = tab_bar.Tabs[tab_dst_n];
            tab.IndexDuringLayout = (short)tab_dst_n;

            // We will need sorting if tabs have changed section (e.g. moved from one of Leading/Central/Trailing to another)
            int curr_tab_section_n = TabItemGetSectionIdx(tab);
            if (tab_dst_n > 0)
            {
                ImGuiTabItem prev_tab = tab_bar.Tabs[tab_dst_n - 1];
                int prev_tab_section_n = TabItemGetSectionIdx(prev_tab);
                if (curr_tab_section_n == 0 && prev_tab_section_n != 0)
                    need_sort_by_section = true;
                if (prev_tab_section_n == 2 && curr_tab_section_n != 2)
                    need_sort_by_section = true;
            }

            sections[curr_tab_section_n].TabCount++;
            tab_dst_n++;
        }
        if (tab_bar.Tabs.Size != tab_dst_n)
            tab_bar.Tabs.resize(tab_dst_n);

        if (need_sort_by_section)
        {
            // C++: ImQsort(tab_bar->Tabs.Data, tab_bar->Tabs.Size, sizeof(ImGuiTabItem), TabItemComparerBySection);
            Array.Sort(tab_bar.Tabs.Data, 0, tab_bar.Tabs.Size, Comparer<ImGuiTabItem>.Create(TabItemComparerBySection));
        }

        // Calculate spacing between sections
        float tab_spacing = g.Style.ItemInnerSpacing.x;
        sections[0].Spacing = sections[0].TabCount > 0 && (sections[1].TabCount + sections[2].TabCount) > 0 ? tab_spacing : 0.0f;
        sections[1].Spacing = sections[1].TabCount > 0 && sections[2].TabCount > 0 ? tab_spacing : 0.0f;

        // Setup next selected tab
        ImGuiID scroll_to_tab_id = 0;
        if (tab_bar.NextScrollToTabId != 0)
        {
            scroll_to_tab_id = tab_bar.NextScrollToTabId;
            tab_bar.NextScrollToTabId = 0;
        }
        if (tab_bar.NextSelectedTabId != 0)
        {
            tab_bar.SelectedTabId = tab_bar.NextSelectedTabId;
            tab_bar.NextSelectedTabId = 0;
            scroll_to_tab_id = tab_bar.SelectedTabId;
        }

        // Process order change request (we could probably process it when requested but it's just saner to do it in a single spot).
        if (tab_bar.ReorderRequestTabId != 0)
        {
            if (TabBarProcessReorder(tab_bar))
                if (tab_bar.ReorderRequestTabId == tab_bar.SelectedTabId)
                    scroll_to_tab_id = tab_bar.ReorderRequestTabId;
            tab_bar.ReorderRequestTabId = 0;
        }

        // Tab List Popup (will alter tab_bar->BarRect and therefore the available width!)
        bool tab_list_popup_button = (tab_bar.Flags & ImGuiTabBarFlags_TabListPopupButton) != 0;
        if (tab_list_popup_button)
        {
            ImGuiTabItem tab_to_select = TabBarTabListPopupButton(tab_bar); // NB: Will alter BarRect.Min.x!
            if (tab_to_select != null)
                scroll_to_tab_id = tab_bar.SelectedTabId = tab_to_select.ID;
        }

        // Leading/Trailing tabs will be shrink only if central one aren't visible anymore, so layout the shrink data as: leading, trailing, central
        // (whereas our tabs are stored as: leading, central, trailing)
        int[] shrink_buffer_indexes = { 0, sections[0].TabCount + sections[2].TabCount, sections[0].TabCount };
        g.ShrinkWidthBuffer.resize(tab_bar.Tabs.Size);

        // Minimum shrink width
        float shrink_min_width = (tab_bar.Flags & ImGuiTabBarFlags_FittingPolicyMixed) != 0 ? g.Style.TabMinWidthShrink : 1.0f;

        // Compute ideal tabs widths + store them into shrink buffer
        ImGuiTabItem most_recently_selected_tab = null;
        int curr_section_n = -1;
        bool found_selected_tab_id = false;
        for (int tab_n = 0; tab_n < tab_bar.Tabs.Size; tab_n++)
        {
            ImGuiTabItem tab = tab_bar.Tabs[tab_n];
            Debug.Assert(tab.LastFrameVisible >= tab_bar.PrevFrameVisible);

            if ((most_recently_selected_tab == null || most_recently_selected_tab.LastFrameSelected < tab.LastFrameSelected) && (tab.Flags & ImGuiTabItemFlags_Button) == 0)
                most_recently_selected_tab = tab;
            if (tab.ID == tab_bar.SelectedTabId)
                found_selected_tab_id = true;
            if (scroll_to_tab_id == 0 && g.NavJustMovedToId == tab.ID)
                scroll_to_tab_id = tab.ID;

            // Refresh tab width immediately, otherwise changes of style e.g. style.FramePadding.x would noticeably lag in the tab bar.
            // Additionally, when using TabBarAddTab() to manipulate tab bar order we occasionally insert new tabs that don't have a width yet,
            // and we cannot wait for the next BeginTabItem() call. We cannot compute this width within TabBarAddTab() because font size depends on the active window.
            string tab_name = TabBarGetTabName(tab_bar, tab);
            bool has_close_button_or_unsaved_marker = (tab.Flags & ImGuiTabItemFlags_NoCloseButton) == 0 || (tab.Flags & ImGuiTabItemFlags_UnsavedDocument) != 0;
            tab.ContentWidth = (tab.RequestedWidth >= 0.0f) ? tab.RequestedWidth : TabItemCalcSize(tab_name, has_close_button_or_unsaved_marker).x;
            if ((tab.Flags & ImGuiTabItemFlags_Button) == 0)
                tab.ContentWidth = ImMath.ImMax(tab.ContentWidth, g.Style.TabMinWidthBase);

            int section_n = TabItemGetSectionIdx(tab);
            ImGuiTabBarSection section = sections[section_n];
            section.Width += tab.ContentWidth + (section_n == curr_section_n ? tab_spacing : 0.0f);
            section.WidthAfterShrinkMinWidth += ImMath.ImMin(tab.ContentWidth, shrink_min_width) + (section_n == curr_section_n ? tab_spacing : 0.0f);
            sections[section_n] = section;
            curr_section_n = section_n;

            // Store data so we can build an array sorted by width if we need to shrink tabs down
            ImGuiShrinkWidthItem shrink_width_item = g.ShrinkWidthBuffer[shrink_buffer_indexes[section_n]];
            shrink_width_item.Index = tab_n;
            shrink_width_item.Width = shrink_width_item.InitialWidth = tab.ContentWidth;
            g.ShrinkWidthBuffer[shrink_buffer_indexes[section_n]] = shrink_width_item;
            shrink_buffer_indexes[section_n]++;
            tab.Width = ImMath.ImMax(tab.ContentWidth, 1.0f);
        }

        // Compute total ideal width (used for e.g. auto-resizing a window)
        float width_all_tabs_after_min_width_shrink = 0.0f;
        tab_bar.WidthAllTabsIdeal = 0.0f;
        for (int section_n = 0; section_n < 3; section_n++)
        {
            tab_bar.WidthAllTabsIdeal += sections[section_n].Width + sections[section_n].Spacing;
            width_all_tabs_after_min_width_shrink += sections[section_n].WidthAfterShrinkMinWidth + sections[section_n].Spacing;
        }

        // Horizontal scrolling buttons
        // Important: note that TabBarScrollButtons() will alter BarRect.Max.x.
        bool can_scroll = (tab_bar.Flags & ImGuiTabBarFlags_FittingPolicyScroll) != 0 || (tab_bar.Flags & ImGuiTabBarFlags_FittingPolicyMixed) != 0;
        float width_all_tabs_to_use_for_scroll = (tab_bar.Flags & ImGuiTabBarFlags_FittingPolicyScroll) != 0 ? tab_bar.WidthAllTabs : width_all_tabs_after_min_width_shrink;
        tab_bar.ScrollButtonEnabled = ((width_all_tabs_to_use_for_scroll > tab_bar.BarRect.GetWidth() && tab_bar.Tabs.Size > 1) && (tab_bar.Flags & ImGuiTabBarFlags_NoTabListScrollingButtons) == 0 && can_scroll);
        if (tab_bar.ScrollButtonEnabled)
        {
            ImGuiTabItem scroll_and_select_tab = TabBarScrollingButtons(tab_bar);
            if (scroll_and_select_tab != null)
            {
                scroll_to_tab_id = scroll_and_select_tab.ID;
                if ((scroll_and_select_tab.Flags & ImGuiTabItemFlags_Button) == 0)
                    tab_bar.SelectedTabId = scroll_to_tab_id;
            }
        }
        if (scroll_to_tab_id == 0 && scroll_to_selected_tab)
            scroll_to_tab_id = tab_bar.SelectedTabId;

        // Shrink widths if full tabs don't fit in their allocated space
        float section_0_w = sections[0].Width + sections[0].Spacing;
        float section_1_w = sections[1].Width + sections[1].Spacing;
        float section_2_w = sections[2].Width + sections[2].Spacing;
        bool central_section_is_visible = (section_0_w + section_2_w) < tab_bar.BarRect.GetWidth();
        float width_excess;
        if (central_section_is_visible)
            width_excess = ImMath.ImMax(section_1_w - (tab_bar.BarRect.GetWidth() - section_0_w - section_2_w), 0.0f); // Excess used to shrink central section
        else
            width_excess = (section_0_w + section_2_w) - tab_bar.BarRect.GetWidth(); // Excess used to shrink leading/trailing section

        // With ImGuiTabBarFlags_FittingPolicyScroll policy, we will only shrink leading/trailing if the central section is not visible anymore
        bool can_shrink = (tab_bar.Flags & ImGuiTabBarFlags_FittingPolicyShrink) != 0 || (tab_bar.Flags & ImGuiTabBarFlags_FittingPolicyMixed) != 0;
        if (width_excess >= 1.0f && (can_shrink || !central_section_is_visible))
        {
            int shrink_data_count = (central_section_is_visible ? sections[1].TabCount : sections[0].TabCount + sections[2].TabCount);
            int shrink_data_offset = (central_section_is_visible ? sections[0].TabCount + sections[2].TabCount : 0);
            // C++: ShrinkWidths(g.ShrinkWidthBuffer.Data + shrink_data_offset, shrink_data_count, width_excess, shrink_min_width);
            // (C# ShrinkWidths() takes an array: copy the slice in, mutate, copy results back out)
            ImGuiShrinkWidthItem[] shrink_slice = new ImGuiShrinkWidthItem[shrink_data_count];
            Array.Copy(g.ShrinkWidthBuffer.Data, shrink_data_offset, shrink_slice, 0, shrink_data_count);
            ShrinkWidths(shrink_slice, shrink_data_count, width_excess, shrink_min_width);
            Array.Copy(shrink_slice, 0, g.ShrinkWidthBuffer.Data, shrink_data_offset, shrink_data_count);

            // Apply shrunk values into tabs and sections
            for (int tab_n = shrink_data_offset; tab_n < shrink_data_offset + shrink_data_count; tab_n++)
            {
                ImGuiTabItem tab = tab_bar.Tabs[g.ShrinkWidthBuffer[tab_n].Index];
                float shrinked_width = ImMath.ImTrunc(g.ShrinkWidthBuffer[tab_n].Width);
                if (shrinked_width < 0.0f)
                    continue;

                shrinked_width = ImMath.ImMax(1.0f, shrinked_width);
                int section_n = TabItemGetSectionIdx(tab);
                sections[section_n].Width -= (tab.Width - shrinked_width);
                tab.Width = shrinked_width;
            }
        }

        // Layout all active tabs
        int section_tab_index = 0;
        float tab_offset = 0.0f;
        tab_bar.WidthAllTabs = 0.0f;
        for (int section_n = 0; section_n < 3; section_n++)
        {
            ImGuiTabBarSection section = sections[section_n];
            if (section_n == 2)
                tab_offset = ImMath.ImMin(ImMath.ImMax(0.0f, tab_bar.BarRect.GetWidth() - section.Width), tab_offset);

            for (int tab_n = 0; tab_n < section.TabCount; tab_n++)
            {
                ImGuiTabItem tab = tab_bar.Tabs[section_tab_index + tab_n];
                tab.Offset = tab_offset;
                tab.NameOffset = -1;
                tab_offset += tab.Width + (tab_n < section.TabCount - 1 ? g.Style.ItemInnerSpacing.x : 0.0f);
            }
            tab_bar.WidthAllTabs += ImMath.ImMax(section.Width + section.Spacing, 0.0f);
            tab_offset += section.Spacing;
            section_tab_index += section.TabCount;
        }

        // Clear name buffers
        tab_bar.TabsNames.Buf.resize(0);

        // If we have lost the selected tab, select the next most recently active one
        bool tab_bar_appearing = (tab_bar.PrevFrameVisible + 1 < g.FrameCount);
        if (found_selected_tab_id == false && !tab_bar_appearing)
            tab_bar.SelectedTabId = 0;
        if (tab_bar.SelectedTabId == 0 && tab_bar.NextSelectedTabId == 0 && most_recently_selected_tab != null)
            scroll_to_tab_id = tab_bar.SelectedTabId = most_recently_selected_tab.ID;

        // Lock in visible tab
        tab_bar.VisibleTabId = tab_bar.SelectedTabId;
        tab_bar.VisibleTabWasSubmitted = false;

        // CTRL+TAB can override visible tab temporarily
        if (g.NavWindowingTarget != null && g.NavWindowingTarget.DockNode != null && g.NavWindowingTarget.DockNode.TabBar == tab_bar)
            tab_bar.VisibleTabId = scroll_to_tab_id = g.NavWindowingTarget.TabId;

        // Apply request requests
        if (scroll_to_tab_id != 0)
        {
            TabBarScrollToTab(tab_bar, scroll_to_tab_id, sections);
        }
        else if (tab_bar.ScrollButtonEnabled && IsMouseHoveringRect(tab_bar.BarRect.Min, tab_bar.BarRect.Max, true) && IsWindowContentHoverable(g.CurrentWindow, ImGuiHoveredFlags_None))
        {
            float wheel = g.IO.MouseWheelRequestAxisSwap ? g.IO.MouseWheel : g.IO.MouseWheelH;
            ImGuiKey wheel_key = g.IO.MouseWheelRequestAxisSwap ? ImGuiKey_MouseWheelY : ImGuiKey_MouseWheelX;
            if (TestKeyOwner(wheel_key, tab_bar.ID) && wheel != 0.0f)
            {
                float scroll_step = wheel * TabBarCalcScrollableWidth(tab_bar, sections) / 3.0f;
                tab_bar.ScrollingTargetDistToVisibility = 0.0f;
                tab_bar.ScrollingTarget = TabBarScrollClamp(tab_bar, tab_bar.ScrollingTarget - scroll_step);
            }
            SetKeyOwner(wheel_key, tab_bar.ID);
        }

        // Update scrolling
        tab_bar.ScrollingAnim = TabBarScrollClamp(tab_bar, tab_bar.ScrollingAnim);
        tab_bar.ScrollingTarget = TabBarScrollClamp(tab_bar, tab_bar.ScrollingTarget);
        if (tab_bar.ScrollingAnim != tab_bar.ScrollingTarget)
        {
            // Scrolling speed adjust itself so we can always reach our target in 1/3 seconds.
            // Teleport if we are aiming far off the visible line
            tab_bar.ScrollingSpeed = ImMath.ImMax(tab_bar.ScrollingSpeed, 70.0f * g.FontSize);
            tab_bar.ScrollingSpeed = ImMath.ImMax(tab_bar.ScrollingSpeed, MathF.Abs(tab_bar.ScrollingTarget - tab_bar.ScrollingAnim) / 0.3f);
            bool teleport = (tab_bar.PrevFrameVisible + 1 < g.FrameCount) || (tab_bar.ScrollingTargetDistToVisibility > 10.0f * g.FontSize);
            tab_bar.ScrollingAnim = teleport ? tab_bar.ScrollingTarget : ImMath.ImLinearSweep(tab_bar.ScrollingAnim, tab_bar.ScrollingTarget, g.IO.DeltaTime * tab_bar.ScrollingSpeed);
        }
        else
        {
            tab_bar.ScrollingSpeed = 0.0f;
        }
        tab_bar.ScrollingRectMinX = tab_bar.BarRect.Min.x + sections[0].Width + sections[0].Spacing;
        tab_bar.ScrollingRectMaxX = tab_bar.BarRect.Max.x - sections[2].Width - sections[1].Spacing;

        // Actual layout in host window (we don't do it in BeginTabBar() so as not to waste an extra frame)
        ImGuiWindow window = g.CurrentWindow;
        window.DC.CursorPos = tab_bar.BarRect.Min;
        ItemSize(new ImVec2(tab_bar.WidthAllTabs, tab_bar.BarRect.GetHeight()), tab_bar.FramePadding.y);
        window.DC.IdealMaxPos.x = ImMath.ImMax(window.DC.IdealMaxPos.x, tab_bar.BarRect.Min.x + tab_bar.WidthAllTabsIdeal);
    }

    // Dockable windows uses Name/ID in the global namespace. Non-dockable items use the ID stack.
    // C++: static ImU32 ImGui::TabBarCalcTabID(ImGuiTabBar* tab_bar, const char* label, ImGuiWindow* docked_window)
    public static ImGuiID TabBarCalcTabID(ImGuiTabBar tab_bar, string label, ImGuiWindow docked_window = null)
    {
        if (docked_window != null)
        {
            // FIXME-DOCKING: Assert on valid tab_bar->Flags
            ImGuiID id = docked_window.TabId;
            KeepAliveID(id);
            return id;
        }
        if ((tab_bar.Flags & ImGuiTabBarFlags_DockNode) != 0)
        {
            ImGuiID id = ImGuiHash.ImHashStr(label, 0);
            KeepAliveID(id);
            return id;
        }
        else
        {
            ImGuiWindow window = GImGui.CurrentWindow;
            return window.GetID(label);
        }
    }

    // C++: static float ImGui::TabBarCalcMaxTabWidth()
    public static float TabBarCalcMaxTabWidth()
    {
        ImGuiContext g = GImGui;
        return g.FontSize * 20.0f;
    }

    // imgui_internal.h: inline int TabBarGetTabOrder(ImGuiTabBar* tab_bar, ImGuiTabItem* tab) { return tab_bar->Tabs.index_from_ptr(tab); }
    public static int TabBarGetTabOrder(ImGuiTabBar tab_bar, ImGuiTabItem tab)
    {
        return tab_bar.Tabs.index_of_ref(tab);
    }

    // C++: ImGuiTabItem* ImGui::TabBarFindTabByID(ImGuiTabBar* tab_bar, ImGuiID tab_id)
    public static ImGuiTabItem TabBarFindTabByID(ImGuiTabBar tab_bar, ImGuiID tab_id)
    {
        if (tab_id != 0)
            for (int n = 0; n < tab_bar.Tabs.Size; n++)
                if (tab_bar.Tabs[n].ID == tab_id)
                    return tab_bar.Tabs[n];
        return null;
    }

    // Order = visible order, not submission order! (which is tab->BeginOrder)
    // C++: ImGuiTabItem* ImGui::TabBarFindTabByOrder(ImGuiTabBar* tab_bar, int order)
    public static ImGuiTabItem TabBarFindTabByOrder(ImGuiTabBar tab_bar, int order)
    {
        if (order < 0 || order >= tab_bar.Tabs.Size)
            return null;
        return tab_bar.Tabs[order];
    }

    // FIXME: See references to #2304 in TODO.txt
    // C++: ImGuiTabItem* ImGui::TabBarFindMostRecentlySelectedTabForActiveWindow(ImGuiTabBar* tab_bar)
    public static ImGuiTabItem TabBarFindMostRecentlySelectedTabForActiveWindow(ImGuiTabBar tab_bar)
    {
        ImGuiTabItem most_recently_selected_tab = null;
        for (int tab_n = 0; tab_n < tab_bar.Tabs.Size; tab_n++)
        {
            ImGuiTabItem tab = tab_bar.Tabs[tab_n];
            if (most_recently_selected_tab == null || most_recently_selected_tab.LastFrameSelected < tab.LastFrameSelected)
                if (tab.Window != null && tab.Window.WasActive)
                    most_recently_selected_tab = tab;
        }
        return most_recently_selected_tab;
    }

    // The purpose of this call is to register tab in advance so we can control their order at the time they appear.
    // Otherwise calling this is unnecessary as tabs are appending as needed by the BeginTabItem() function.
    // C++: void ImGui::TabBarAddTab(ImGuiTabBar* tab_bar, ImGuiTabItemFlags tab_flags, ImGuiWindow* window)
    public static unsafe void TabBarAddTab(ImGuiTabBar tab_bar, ImGuiTabItemFlags tab_flags, ImGuiWindow window)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(TabBarFindTabByID(tab_bar, window.TabId) == null);
        Debug.Assert(g.CurrentTabBar != tab_bar);  // Can't work while the tab bar is active as our tab doesn't have an X offset yet, in theory we could/should test something like (tab_bar.CurrFrameVisible < g.FrameCount) but we'd need to solve why triggers the commented early-out assert in BeginTabBarEx() (probably dock node going from implicit to explicit in same frame)

        if (!window.HasCloseButton)
            tab_flags |= ImGuiTabItemFlags_NoCloseButton;       // Set _NoCloseButton immediately because it will be used for first-frame width calculation.

        ImGuiTabItem new_tab = new ImGuiTabItem();
        new_tab.ID = window.TabId;
        new_tab.Flags = tab_flags;
        new_tab.LastFrameVisible = tab_bar.CurrFrameVisible;   // Required so BeginTabBar() doesn't ditch the tab
        if (new_tab.LastFrameVisible == -1)
            new_tab.LastFrameVisible = g.FrameCount - 1;
        new_tab.Window = window;                                // Required so tab bar layout can compute the tab width before tab submission
        tab_bar.Tabs.push_back(new_tab);
    }

    // C++: ImGuiTabItem* ImGui::TabBarGetCurrentTab(ImGuiTabBar* tab_bar)
    public static ImGuiTabItem TabBarGetCurrentTab(ImGuiTabBar tab_bar)
    {
        if (tab_bar.LastTabItemIdx < 0 || tab_bar.LastTabItemIdx >= tab_bar.Tabs.Size)
            return null;
        return tab_bar.Tabs[tab_bar.LastTabItemIdx];
    }

    // C++: const char* ImGui::TabBarGetTabName(ImGuiTabBar* tab_bar, ImGuiTabItem* tab)
    public static string TabBarGetTabName(ImGuiTabBar tab_bar, ImGuiTabItem tab)
    {
        if (tab.Window != null)
            return tab.Window.Name;
        if (tab.NameOffset == -1)
            return "N/A";
        Debug.Assert(tab.NameOffset < tab_bar.TabsNames.Buf.Size);
        // C++: return tab_bar->TabsNames.Buf.Data + tab->NameOffset;  (NUL-terminated byte buffer)
        byte[] data = tab_bar.TabsNames.Buf.Data;
        int end = tab.NameOffset;
        while (end < tab_bar.TabsNames.Buf.Size && data[end] != 0)
            end++;
        return System.Text.Encoding.UTF8.GetString(data, tab.NameOffset, end - tab.NameOffset);
    }

    // The *TabId fields are already set by the docking system _before_ the actual TabItem was created, so we clear them regardless.
    // C++: void ImGui::TabBarRemoveTab(ImGuiTabBar* tab_bar, ImGuiID tab_id)
    public static void TabBarRemoveTab(ImGuiTabBar tab_bar, ImGuiID tab_id)
    {
        ImGuiTabItem tab = TabBarFindTabByID(tab_bar, tab_id);
        if (tab != null)
            tab_bar.Tabs.erase(tab_bar.Tabs.index_of_ref(tab)); // C++: tab_bar->Tabs.erase(tab)
        if (tab_bar.VisibleTabId == tab_id) { tab_bar.VisibleTabId = 0; }
        if (tab_bar.SelectedTabId == tab_id) { tab_bar.SelectedTabId = 0; }
        if (tab_bar.NextSelectedTabId == tab_id) { tab_bar.NextSelectedTabId = 0; }
    }

    // Called on manual closure attempt
    // C++: void ImGui::TabBarCloseTab(ImGuiTabBar* tab_bar, ImGuiTabItem* tab)
    public static void TabBarCloseTab(ImGuiTabBar tab_bar, ImGuiTabItem tab)
    {
        if ((tab.Flags & ImGuiTabItemFlags_Button) != 0)
            return; // A button appended with TabItemButton().

        if ((tab.Flags & (ImGuiTabItemFlags_UnsavedDocument | ImGuiTabItemFlags_NoAssumedClosure)) == 0)
        {
            // This will remove a frame of lag for selecting another tab on closure.
            // However we don't run it in the case where the 'Unsaved' flag is set, so user gets a chance to fully undo the closure
            tab.WantClose = true;
            if (tab_bar.VisibleTabId == tab.ID)
            {
                tab.LastFrameVisible = -1;
                tab_bar.SelectedTabId = tab_bar.NextSelectedTabId = 0;
            }
        }
        else
        {
            // Actually select before expecting closure attempt (on an UnsavedDocument tab user is expect to e.g. show a popup)
            if (tab_bar.VisibleTabId != tab.ID)
                TabBarQueueFocus(tab_bar, tab);
        }
    }

    // C++: static float ImGui::TabBarScrollClamp(ImGuiTabBar* tab_bar, float scrolling)
    public static float TabBarScrollClamp(ImGuiTabBar tab_bar, float scrolling)
    {
        scrolling = ImMath.ImMin(scrolling, tab_bar.WidthAllTabs - tab_bar.BarRect.GetWidth());
        return ImMath.ImMax(scrolling, 0.0f);
    }

    // Note: we may scroll to tab that are not selected! e.g. using keyboard arrow keys
    // C++: static void ImGui::TabBarScrollToTab(ImGuiTabBar* tab_bar, ImGuiID tab_id, ImGuiTabBarSection* sections)
    public static void TabBarScrollToTab(ImGuiTabBar tab_bar, ImGuiID tab_id, ImGuiTabBarSection[] sections)
    {
        ImGuiTabItem tab = TabBarFindTabByID(tab_bar, tab_id);
        if (tab == null)
            return;
        if ((tab.Flags & ImGuiTabItemFlags_SectionMask_) != 0)
            return;

        ImGuiContext g = GImGui;
        float margin = g.FontSize * 1.0f; // When to scroll to make Tab N+1 visible always make a bit of N visible to suggest more scrolling area (since we don't have a scrollbar)
        int order = TabBarGetTabOrder(tab_bar, tab);

        // Scrolling happens only in the central section (leading/trailing sections are not scrolling)
        float scrollable_width = TabBarCalcScrollableWidth(tab_bar, sections);

        // We make all tabs positions all relative Sections[0].Width to make code simpler
        float tab_x1 = tab.Offset - sections[0].Width + (order > sections[0].TabCount - 1 ? -margin : 0.0f);
        float tab_x2 = tab.Offset - sections[0].Width + tab.Width + (order + 1 < tab_bar.Tabs.Size - sections[2].TabCount ? margin : 1.0f);
        tab_bar.ScrollingTargetDistToVisibility = 0.0f;
        if (tab_bar.ScrollingTarget > tab_x1 || (tab_x2 - tab_x1 >= scrollable_width))
        {
            // Scroll to the left
            tab_bar.ScrollingTargetDistToVisibility = ImMath.ImMax(tab_bar.ScrollingAnim - tab_x2, 0.0f);
            tab_bar.ScrollingTarget = tab_x1;
        }
        else if (tab_bar.ScrollingTarget < tab_x2 - scrollable_width)
        {
            // Scroll to the right
            tab_bar.ScrollingTargetDistToVisibility = ImMath.ImMax((tab_x1 - scrollable_width) - tab_bar.ScrollingAnim, 0.0f);
            tab_bar.ScrollingTarget = tab_x2 - scrollable_width;
        }
    }

    // C++: void ImGui::TabBarQueueFocus(ImGuiTabBar* tab_bar, ImGuiTabItem* tab)
    public static void TabBarQueueFocus(ImGuiTabBar tab_bar, ImGuiTabItem tab)
    {
        tab_bar.NextSelectedTabId = tab.ID;
    }

    // C++: void ImGui::TabBarQueueFocus(ImGuiTabBar* tab_bar, const char* tab_name)
    public static void TabBarQueueFocus(ImGuiTabBar tab_bar, string tab_name)
    {
        Debug.Assert((tab_bar.Flags & ImGuiTabBarFlags_DockNode) == 0); // Only supported for manual/explicit tab bars
        ImGuiID tab_id = TabBarCalcTabID(tab_bar, tab_name, null);
        tab_bar.NextSelectedTabId = tab_id;
    }

    // C++: void ImGui::TabBarQueueReorder(ImGuiTabBar* tab_bar, ImGuiTabItem* tab, int offset)
    public static void TabBarQueueReorder(ImGuiTabBar tab_bar, ImGuiTabItem tab, int offset)
    {
        Debug.Assert(offset != 0);
        Debug.Assert(tab_bar.ReorderRequestTabId == 0);
        tab_bar.ReorderRequestTabId = tab.ID;
        tab_bar.ReorderRequestOffset = (short)offset;
    }

    // C++: void ImGui::TabBarQueueReorderFromMousePos(ImGuiTabBar* tab_bar, ImGuiTabItem* src_tab, ImVec2 mouse_pos)
    public static void TabBarQueueReorderFromMousePos(ImGuiTabBar tab_bar, ImGuiTabItem src_tab, ImVec2 mouse_pos)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(tab_bar.ReorderRequestTabId == 0);
        if ((tab_bar.Flags & ImGuiTabBarFlags_Reorderable) == 0)
            return;

        float tab_spacing = g.Style.ItemInnerSpacing.x;
        bool is_central_section = (src_tab.Flags & ImGuiTabItemFlags_SectionMask_) == 0;
        float bar_offset = tab_bar.BarRect.Min.x - (is_central_section ? tab_bar.ScrollingTarget : 0);

        // Count number of contiguous tabs we are crossing over
        int dir = (bar_offset + src_tab.Offset) > mouse_pos.x ? -1 : +1;
        int src_idx = tab_bar.Tabs.index_of_ref(src_tab); // C++: tab_bar->Tabs.index_from_ptr(src_tab)
        int dst_idx = src_idx;
        for (int i = src_idx; i >= 0 && i < tab_bar.Tabs.Size; i += dir)
        {
            // Reordered tabs must share the same section
            ImGuiTabItem dst_tab = tab_bar.Tabs[i];
            if ((dst_tab.Flags & ImGuiTabItemFlags_NoReorder) != 0)
                break;
            if ((dst_tab.Flags & ImGuiTabItemFlags_SectionMask_) != (src_tab.Flags & ImGuiTabItemFlags_SectionMask_))
                break;
            dst_idx = i;

            // Include spacing after tab, so when mouse cursor is between tabs we would not continue checking further tabs that are not hovered.
            float x1 = bar_offset + dst_tab.Offset - tab_spacing;
            float x2 = bar_offset + dst_tab.Offset + dst_tab.Width + tab_spacing;
            //GetForegroundDrawList()->AddRect(ImVec2(x1, tab_bar->BarRect.Min.y), ImVec2(x2, tab_bar->BarRect.Max.y), IM_COL32(255, 0, 0, 255));
            if ((dir < 0 && mouse_pos.x > x1) || (dir > 0 && mouse_pos.x < x2))
                break;
        }

        if (dst_idx != src_idx)
            TabBarQueueReorder(tab_bar, src_tab, dst_idx - src_idx);
    }

    // C++: bool ImGui::TabBarProcessReorder(ImGuiTabBar* tab_bar)
    public static bool TabBarProcessReorder(ImGuiTabBar tab_bar)
    {
        ImGuiTabItem tab1 = TabBarFindTabByID(tab_bar, tab_bar.ReorderRequestTabId);
        if (tab1 == null || (tab1.Flags & ImGuiTabItemFlags_NoReorder) != 0)
            return false;

        //IM_ASSERT(tab_bar->Flags & ImGuiTabBarFlags_Reorderable); // <- this may happen when using debug tools
        int tab2_order = TabBarGetTabOrder(tab_bar, tab1) + tab_bar.ReorderRequestOffset;
        if (tab2_order < 0 || tab2_order >= tab_bar.Tabs.Size)
            return false;

        // Reordered tabs must share the same section
        // (Note: TabBarQueueReorderFromMousePos() also has a similar test but since we allow direct calls to TabBarQueueReorder() we do it here too)
        ImGuiTabItem tab2 = tab_bar.Tabs[tab2_order];
        if ((tab2.Flags & ImGuiTabItemFlags_NoReorder) != 0)
            return false;
        if ((tab1.Flags & ImGuiTabItemFlags_SectionMask_) != (tab2.Flags & ImGuiTabItemFlags_SectionMask_))
            return false;

        // C++: ImGuiTabItem item_tmp = *tab1; ... memmove(dst_tab, src_tab, move_count * sizeof(ImGuiTabItem)); *tab2 = item_tmp;
        // (ImGuiTabItem is a class here: rotating element references reproduces the exact same reordering)
        ImGuiTabItem item_tmp = tab1;
        int tab1_index = tab_bar.Tabs.index_of_ref(tab1);
        int move_count = (tab_bar.ReorderRequestOffset > 0) ? tab_bar.ReorderRequestOffset : -tab_bar.ReorderRequestOffset;
        if (tab_bar.ReorderRequestOffset > 0)
        {
            // src_tab = tab1 + 1, dst_tab = tab1: shift elements left by one
            Array.Copy(tab_bar.Tabs.Data, tab1_index + 1, tab_bar.Tabs.Data, tab1_index, move_count);
        }
        else
        {
            // src_tab = tab2, dst_tab = tab2 + 1: shift elements right by one
            Array.Copy(tab_bar.Tabs.Data, tab2_order, tab_bar.Tabs.Data, tab2_order + 1, move_count);
        }
        tab_bar.Tabs.Data[tab2_order] = item_tmp;

        if ((tab_bar.Flags & ImGuiTabBarFlags_SaveSettings) != 0)
            MarkIniSettingsDirty();
        return true;
    }

    // C++: static ImGuiTabItem* ImGui::TabBarScrollingButtons(ImGuiTabBar* tab_bar)
    public static ImGuiTabItem TabBarScrollingButtons(ImGuiTabBar tab_bar)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        ImVec2 arrow_button_size = new ImVec2(g.FontSize - 2.0f, g.FontSize + g.Style.FramePadding.y * 2.0f);
        float scrolling_buttons_width = arrow_button_size.x * 2.0f;

        ImVec2 backup_cursor_pos = window.DC.CursorPos;
        //window->DrawList->AddRect(ImVec2(tab_bar->BarRect.Max.x - scrolling_buttons_width, tab_bar->BarRect.Min.y), ImVec2(tab_bar->BarRect.Max.x, tab_bar->BarRect.Max.y), IM_COL32(255,0,0,255));

        int select_dir = 0;
        ImVec4 arrow_col = g.Style.Colors[ImGuiCol_Text];
        arrow_col.w *= 0.5f;

        PushStyleColor(ImGuiCol_Text, arrow_col);
        PushStyleColor(ImGuiCol_Button, new ImVec4(0, 0, 0, 0));
        PushItemFlag(ImGuiItemFlags_ButtonRepeat | ImGuiItemFlags_NoNav, true);
        float backup_repeat_delay = g.IO.KeyRepeatDelay;
        float backup_repeat_rate = g.IO.KeyRepeatRate;
        g.IO.KeyRepeatDelay = 0.250f;
        g.IO.KeyRepeatRate = 0.200f;
        float x = ImMath.ImMax(tab_bar.BarRect.Min.x, tab_bar.BarRect.Max.x - scrolling_buttons_width);
        window.DC.CursorPos = new ImVec2(x, tab_bar.BarRect.Min.y);
        if (ArrowButtonEx("##<", ImGuiDir_Left, arrow_button_size, ImGuiButtonFlags_PressedOnClick))
            select_dir = -1;
        window.DC.CursorPos = new ImVec2(x + arrow_button_size.x, tab_bar.BarRect.Min.y);
        if (ArrowButtonEx("##>", ImGuiDir_Right, arrow_button_size, ImGuiButtonFlags_PressedOnClick))
            select_dir = +1;
        PopItemFlag();
        PopStyleColor(2);
        g.IO.KeyRepeatRate = backup_repeat_rate;
        g.IO.KeyRepeatDelay = backup_repeat_delay;

        ImGuiTabItem tab_to_scroll_to = null;
        if (select_dir != 0)
        {
            ImGuiTabItem tab_item = TabBarFindTabByID(tab_bar, tab_bar.SelectedTabId);
            if (tab_item != null)
            {
                int selected_order = TabBarGetTabOrder(tab_bar, tab_item);
                int target_order = selected_order + select_dir;

                // Skip tab item buttons until another tab item is found or end is reached
                while (tab_to_scroll_to == null)
                {
                    // If we are at the end of the list, still scroll to make our tab visible
                    tab_to_scroll_to = tab_bar.Tabs[(target_order >= 0 && target_order < tab_bar.Tabs.Size) ? target_order : selected_order];

                    // Cross through buttons
                    // (even if first/last item is a button, return it so we can update the scroll)
                    if ((tab_to_scroll_to.Flags & ImGuiTabItemFlags_Button) != 0)
                    {
                        target_order += select_dir;
                        selected_order += select_dir;
                        tab_to_scroll_to = (target_order < 0 || target_order >= tab_bar.Tabs.Size) ? tab_to_scroll_to : null;
                    }
                }
            }
        }
        window.DC.CursorPos = backup_cursor_pos;
        tab_bar.BarRect.Max.x -= scrolling_buttons_width + 1.0f;

        return tab_to_scroll_to;
    }

    // C++: static ImGuiTabItem* ImGui::TabBarTabListPopupButton(ImGuiTabBar* tab_bar)
    public static ImGuiTabItem TabBarTabListPopupButton(ImGuiTabBar tab_bar)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        // We use g.Style.FramePadding.y to match the square ArrowButton size
        float tab_list_popup_button_width = g.FontSize + g.Style.FramePadding.y;
        ImVec2 backup_cursor_pos = window.DC.CursorPos;
        window.DC.CursorPos = new ImVec2(tab_bar.BarRect.Min.x - g.Style.FramePadding.y, tab_bar.BarRect.Min.y);
        tab_bar.BarRect.Min.x += tab_list_popup_button_width;

        ImVec4 arrow_col = g.Style.Colors[ImGuiCol_Text];
        arrow_col.w *= 0.5f;
        PushStyleColor(ImGuiCol_Text, arrow_col);
        PushStyleColor(ImGuiCol_Button, new ImVec4(0, 0, 0, 0));
        bool open = BeginCombo("##v", null, ImGuiComboFlags_NoPreview | ImGuiComboFlags_HeightLargest);
        PopStyleColor(2);

        ImGuiTabItem tab_to_select = null;
        if (open)
        {
            for (int tab_n = 0; tab_n < tab_bar.Tabs.Size; tab_n++)
            {
                ImGuiTabItem tab = tab_bar.Tabs[tab_n];
                if ((tab.Flags & ImGuiTabItemFlags_Button) != 0)
                    continue;

                string tab_name = TabBarGetTabName(tab_bar, tab);
                if (Selectable(tab_name, tab_bar.SelectedTabId == tab.ID))
                    tab_to_select = tab;
            }
            EndCombo();
        }

        window.DC.CursorPos = backup_cursor_pos;
        return tab_to_select;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: BeginTabItem, EndTabItem, etc.
    //-------------------------------------------------------------------------

    // C++: bool ImGui::BeginTabItem(const char* label, bool* p_open, ImGuiTabItemFlags flags)
        public static unsafe bool BeginTabItem(string label, ImGuiTabItemFlags flags = 0) => BeginTabItemEx(label, null, flags);

    public static unsafe bool BeginTabItem(string label, ref bool p_open, ImGuiTabItemFlags flags = 0)
    {
        fixed (bool* p = &p_open)
            return BeginTabItemEx(label, p, flags);
    }

private static unsafe bool BeginTabItemEx(string label, bool* p_open, ImGuiTabItemFlags flags)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;

        ImGuiTabBar tab_bar = g.CurrentTabBar;
        if (tab_bar == null)
        {
            Debug.Assert(false, "Needs to be called between BeginTabBar() and EndTabBar()!"); // IM_ASSERT_USER_ERROR_RETV
            return false;
        }
        Debug.Assert((flags & ImGuiTabItemFlags_Button) == 0); // BeginTabItem() Can't be used with button flags, use TabItemButton() instead!

        bool ret = TabItemEx(tab_bar, label, p_open, flags, null);
        if (ret && (flags & ImGuiTabItemFlags_NoPushId) == 0)
        {
            ImGuiTabItem tab = tab_bar.Tabs[tab_bar.LastTabItemIdx];
            PushOverrideID(tab.ID); // We already hashed 'label' so push into the ID stack directly instead of doing another hash through PushID(label)
        }
        return ret;
    }

    // C++: void ImGui::EndTabItem()
    public static void EndTabItem()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        ImGuiTabBar tab_bar = g.CurrentTabBar;
        if (tab_bar == null)
        {
            Debug.Assert(false, "Needs to be called between BeginTabBar() and EndTabBar()!"); // IM_ASSERT_USER_ERROR_RET
            return;
        }
        Debug.Assert(tab_bar.LastTabItemIdx >= 0);
        ImGuiTabItem tab = tab_bar.Tabs[tab_bar.LastTabItemIdx];
        if ((tab.Flags & ImGuiTabItemFlags_NoPushId) == 0)
            PopID();
    }

    // C++: bool ImGui::TabItemButton(const char* label, ImGuiTabItemFlags flags)
    public static unsafe bool TabItemButton(string label, ImGuiTabItemFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;

        ImGuiTabBar tab_bar = g.CurrentTabBar;
        if (tab_bar == null)
        {
            Debug.Assert(false, "Needs to be called between BeginTabBar() and EndTabBar()!"); // IM_ASSERT_USER_ERROR_RETV
            return false;
        }
        return TabItemEx(tab_bar, label, null, flags | ImGuiTabItemFlags_Button | ImGuiTabItemFlags_NoReorder, null);
    }

    // C++: void ImGui::TabItemSpacing(const char* str_id, ImGuiTabItemFlags flags, float width)
    public static unsafe void TabItemSpacing(string str_id, ImGuiTabItemFlags flags, float width)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return;

        ImGuiTabBar tab_bar = g.CurrentTabBar;
        if (tab_bar == null)
        {
            Debug.Assert(false, "Needs to be called between BeginTabBar() and EndTabBar()!"); // IM_ASSERT_USER_ERROR_RET
            return;
        }
        SetNextItemWidth(width);
        TabItemEx(tab_bar, str_id, null, flags | ImGuiTabItemFlags_Button | ImGuiTabItemFlags_NoReorder | ImGuiTabItemFlags_Invisible, null);
    }

    // C++: bool ImGui::TabItemEx(ImGuiTabBar* tab_bar, const char* label, bool* p_open, ImGuiTabItemFlags flags, ImGuiWindow* docked_window)
    public static unsafe bool TabItemEx(ImGuiTabBar tab_bar, string label, bool* p_open, ImGuiTabItemFlags flags, ImGuiWindow docked_window)
    {
        // Layout whole tab bar if not already done
        ImGuiContext g = GImGui;
        if (tab_bar.WantLayout)
        {
            ImGuiNextItemData backup_next_item_data = g.NextItemData;
            TabBarLayout(tab_bar);
            g.NextItemData = backup_next_item_data;
        }
        ImGuiWindow window = g.CurrentWindow;
        if (window.SkipItems)
            return false;

        ImGuiStyle style = g.Style;
        ImGuiID id = TabBarCalcTabID(tab_bar, label, docked_window);

        // If the user called us with *p_open == false, we early out and don't render.
        // We make a call to ItemAdd() so that attempts to use a contextual popup menu with an implicit ID won't use an older ID.
        // (C++: IMGUI_TEST_ENGINE_ITEM_INFO(id, label, g.LastItemData.StatusFlags); -- test engine hook omitted)
        if (p_open != null && !*p_open)
        {
            ItemAdd(new ImRect(), id, null, ImGuiItemFlags_NoNav);
            return false;
        }

        Debug.Assert(p_open == null || (flags & ImGuiTabItemFlags_Button) == 0);
        Debug.Assert((flags & (ImGuiTabItemFlags_Leading | ImGuiTabItemFlags_Trailing)) != (ImGuiTabItemFlags_Leading | ImGuiTabItemFlags_Trailing)); // Can't use both Leading and Trailing

        // Store into ImGuiTabItemFlags_NoCloseButton, also honor ImGuiTabItemFlags_NoCloseButton passed by user (although not documented)
        if ((flags & ImGuiTabItemFlags_NoCloseButton) != 0)
            p_open = null;
        else if (p_open == null)
            flags |= ImGuiTabItemFlags_NoCloseButton;

        // Acquire tab data
        ImGuiTabItem tab = TabBarFindTabByID(tab_bar, id);
        bool tab_is_new = false;
        if (tab == null)
        {
            tab_bar.Tabs.push_back(new ImGuiTabItem());
            tab = tab_bar.Tabs.back();
            tab.ID = id;
            tab_bar.TabsAddedNew = tab_is_new = true;
        }
        tab_bar.LastTabItemIdx = (short)tab_bar.Tabs.index_of_ref(tab); // C++: (ImS16)tab_bar->Tabs.index_from_ptr(tab)

        // Calculate tab contents size
        ImVec2 size = TabItemCalcSize(label, (p_open != null) || (flags & ImGuiTabItemFlags_UnsavedDocument) != 0);
        tab.RequestedWidth = -1.0f;
        if ((g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasWidth) != 0)
            size.x = tab.RequestedWidth = g.NextItemData.Width;
        if (tab_is_new)
            tab.Width = ImMath.ImMax(1.0f, size.x);
        tab.ContentWidth = size.x;
        tab.BeginOrder = tab_bar.TabsActiveCount++;

        bool tab_bar_appearing = (tab_bar.PrevFrameVisible + 1 < g.FrameCount);
        bool tab_bar_focused = (tab_bar.Flags & ImGuiTabBarFlags_IsFocused) != 0;
        bool tab_appearing = (tab.LastFrameVisible + 1 < g.FrameCount);
        bool tab_just_unsaved = (flags & ImGuiTabItemFlags_UnsavedDocument) != 0 && (tab.Flags & ImGuiTabItemFlags_UnsavedDocument) == 0;
        bool is_tab_button = (flags & ImGuiTabItemFlags_Button) != 0;
        tab.LastFrameVisible = g.FrameCount;
        tab.Flags = flags;
        tab.Window = docked_window;

        // Append name _WITH_ the zero-terminator
        // (regular tabs are permitted in a DockNode tab bar, but window tabs not permitted in a non-DockNode tab bar)
        if (docked_window != null)
        {
            Debug.Assert((tab_bar.Flags & ImGuiTabBarFlags_DockNode) != 0);
            tab.NameOffset = -1;
        }
        else
        {
            tab.NameOffset = tab_bar.TabsNames.size();
            tab_bar.TabsNames.append(label, label.Length + 1); // C++: tab_bar->TabsNames.append(label, label + ImStrlen(label) + 1)
        }

        // Update selected tab
        if (!is_tab_button)
        {
            if (tab_appearing && (tab_bar.Flags & ImGuiTabBarFlags_AutoSelectNewTabs) != 0 && tab_bar.NextSelectedTabId == 0)
                if (!tab_bar_appearing || tab_bar.SelectedTabId == 0)
                    TabBarQueueFocus(tab_bar, tab); // New tabs gets activated
            if ((flags & ImGuiTabItemFlags_SetSelected) != 0 && (tab_bar.SelectedTabId != id)) // _SetSelected can only be passed on explicit tab bar
                TabBarQueueFocus(tab_bar, tab);
        }

        // Lock visibility
        // (Note: tab_contents_visible != tab_selected... because Ctrl+Tab operations may preview some tabs without selecting them!)
        bool tab_contents_visible = (tab_bar.VisibleTabId == id);
        if (tab_contents_visible)
            tab_bar.VisibleTabWasSubmitted = true;

        // On the very first frame of a tab bar we let first tab contents be visible to minimize appearing glitches
        if (!tab_contents_visible && tab_bar.SelectedTabId == 0 && tab_bar_appearing && docked_window == null)
            if (tab_bar.Tabs.Size == 1 && (tab_bar.Flags & ImGuiTabBarFlags_AutoSelectNewTabs) == 0)
                tab_contents_visible = true;

        // Note that tab_is_new is not necessarily the same as tab_appearing! When a tab bar stops being submitted
        // and then gets submitted again, the tabs will have 'tab_appearing=true' but 'tab_is_new=false'.
        if (tab_appearing && (!tab_bar_appearing || tab_is_new))
        {
            ItemAdd(new ImRect(), id, null, ImGuiItemFlags_NoNav);
            if (is_tab_button)
                return false;
            return tab_contents_visible;
        }

        if (tab_bar.SelectedTabId == id)
            tab.LastFrameSelected = g.FrameCount;

        // Backup current layout position
        ImVec2 backup_main_cursor_pos = window.DC.CursorPos;

        // Layout
        bool is_central_section = (tab.Flags & ImGuiTabItemFlags_SectionMask_) == 0;
        size.x = tab.Width;
        if (is_central_section)
            window.DC.CursorPos = tab_bar.BarRect.Min + new ImVec2(ImMath.ImTrunc(tab.Offset - tab_bar.ScrollingAnim), 0.0f);
        else
            window.DC.CursorPos = tab_bar.BarRect.Min + new ImVec2(tab.Offset, 0.0f);
        ImVec2 pos = window.DC.CursorPos;
        ImRect bb = new ImRect(pos, pos + size);

        // We don't have CPU clipping primitives to clip the CloseButton (until it becomes a texture), so need to add an extra draw call (temporary in the case of vertical animation)
        bool want_clip_rect = is_central_section && (bb.Min.x < tab_bar.ScrollingRectMinX || bb.Max.x > tab_bar.ScrollingRectMaxX);
        if (want_clip_rect)
            PushClipRect(new ImVec2(ImMath.ImClamp(bb.Min.x, tab_bar.ScrollingRectMinX, tab_bar.ScrollingRectMaxX), bb.Min.y - 1), new ImVec2(tab_bar.ScrollingRectMaxX, bb.Max.y), true);

        ImVec2 backup_cursor_max_pos = window.DC.CursorMaxPos;
        ItemSize(bb.GetSize(), style.FramePadding.y);
        window.DC.CursorMaxPos = backup_cursor_max_pos;

        if (!ItemAdd(bb, id))
        {
            if (want_clip_rect)
                PopClipRect();
            window.DC.CursorPos = backup_main_cursor_pos;
            return tab_contents_visible;
        }

        // Click to Select a tab
        ImGuiButtonFlags button_flags = (is_tab_button ? ImGuiButtonFlags_PressedOnClickRelease : ImGuiButtonFlags_PressedOnClick) | ImGuiButtonFlags_AllowOverlap;
        if (g.DragDropActive && !g.DragDropPayload.IsDataType(IMGUI_PAYLOAD_TYPE_WINDOW)) // FIXME: May be an opt-in property of the payload to disable this
            button_flags |= ImGuiButtonFlags_PressedOnDragDropHold;
        bool hovered, held, pressed;
        if ((flags & ImGuiTabItemFlags_Invisible) != 0)
        {
            hovered = held = pressed = false;
        }
        else
        {
            pressed = ButtonBehavior(bb, id, out hovered, out held, button_flags);
        }
        if (pressed && !is_tab_button)
            TabBarQueueFocus(tab_bar, tab);

        // Transfer active id window so the active id is not owned by the dock host (as StartMouseMovingWindow()
        // will only do it on the drag). This allows FocusWindow() to be more conservative in how it clears active id.
        if (held && docked_window != null && g.ActiveId == id && g.ActiveIdIsJustActivated)
            g.ActiveIdWindow = docked_window;

        // Drag and drop a single floating window node moves it
        ImGuiDockNode node = docked_window != null ? docked_window.DockNode : null;
        bool single_floating_window_node = node != null && node.IsFloatingNode() && (node.Windows.Size == 1);
        if (held && single_floating_window_node && IsMouseDragging(ImGuiMouseButton_Left, 0.0f))
        {
            // Move
            StartMouseMovingWindow(docked_window);
        }
        else if (held && !tab_appearing && IsMouseDragging(ImGuiMouseButton_Left))
        {
            // Drag and drop: re-order tabs
            int drag_dir = 0;
            float drag_distance_from_edge_x = 0.0f;
            if (!g.DragDropActive && ((tab_bar.Flags & ImGuiTabBarFlags_Reorderable) != 0 || docked_window != null))
            {
                // While moving a tab it will jump on the other side of the mouse, so we also test for MouseDelta.x
                if (g.IO.MouseDelta.x < 0.0f && g.IO.MousePos.x < bb.Min.x)
                {
                    drag_dir = -1;
                    drag_distance_from_edge_x = bb.Min.x - g.IO.MousePos.x;
                    TabBarQueueReorderFromMousePos(tab_bar, tab, g.IO.MousePos);
                }
                else if (g.IO.MouseDelta.x > 0.0f && g.IO.MousePos.x > bb.Max.x)
                {
                    drag_dir = +1;
                    drag_distance_from_edge_x = g.IO.MousePos.x - bb.Max.x;
                    TabBarQueueReorderFromMousePos(tab_bar, tab, g.IO.MousePos);
                }
            }

            // Extract a Dockable window out of it's tab bar
            bool can_undock = docked_window != null && (docked_window.Flags & ImGuiWindowFlags_NoMove) == 0 && (node.MergedFlags & ImGuiDockNodeFlags_NoUndocking) == 0;
            if (can_undock)
            {
                // We use a variable threshold to distinguish dragging tabs within a tab bar and extracting them out of the tab bar
                bool undocking_tab = (g.DragDropActive && g.DragDropPayload.SourceId == id);
                if (!undocking_tab) //&& (!g.IO.ConfigDockingWithShift || g.IO.KeyShift)
                {
                    float threshold_base = g.FontSize;
                    float threshold_x = (threshold_base * 2.2f);
                    float threshold_y = (threshold_base * 1.5f) + ImMath.ImClamp((ImMath.ImFabs(g.IO.MouseDragMaxDistanceAbs[0].x) - threshold_base * 2.0f) * 0.20f, 0.0f, threshold_base * 4.0f);
                    //GetForegroundDrawList()->AddRect(ImVec2(bb.Min.x - threshold_x, bb.Min.y - threshold_y), ImVec2(bb.Max.x + threshold_x, bb.Max.y + threshold_y), IM_COL32_WHITE); // [DEBUG]

                    float distance_from_edge_y = ImMath.ImMax(bb.Min.y - g.IO.MousePos.y, g.IO.MousePos.y - bb.Max.y);
                    if (distance_from_edge_y >= threshold_y)
                        undocking_tab = true;
                    if (drag_distance_from_edge_x > threshold_x)
                        if ((drag_dir < 0 && TabBarGetTabOrder(tab_bar, tab) == 0) || (drag_dir > 0 && TabBarGetTabOrder(tab_bar, tab) == tab_bar.Tabs.Size - 1))
                            undocking_tab = true;
                }

                if (undocking_tab)
                {
                    // Undock
                    // FIXME: refactor to share more code with e.g. StartMouseMovingWindow
                    DockContextQueueUndockWindow(g, docked_window);
                    g.MovingWindow = docked_window;
                    SetActiveID(g.MovingWindow.MoveId, g.MovingWindow);
                    g.ActiveIdClickOffset -= g.MovingWindow.Pos - bb.Min;
                    g.ActiveIdNoClearOnFocusLoss = true;
                    SetActiveIdUsingAllKeyboardKeys();
                }
            }
        }

#if false
        if (hovered && g.HoveredIdNotActiveTimer > TOOLTIP_DELAY && bb.GetWidth() < tab->ContentWidth)
        {
            // Enlarge tab display when hovering
            bb.Max.x = bb.Min.x + IM_TRUNC(ImLerp(bb.GetWidth(), tab->ContentWidth, ImSaturate((g.HoveredIdNotActiveTimer - 0.40f) * 6.0f)));
            display_draw_list = GetForegroundDrawList(window);
            TabItemBackground(display_draw_list, bb, flags, GetColorU32(ImGuiCol_TitleBgActive));
        }
#endif

        // Render tab shape
        bool is_visible = (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Visible) != 0 && (flags & ImGuiTabItemFlags_Invisible) == 0;
        if (is_visible)
        {
            ImDrawList display_draw_list = window.DrawList;
            ImU32 tab_col = GetColorU32((held || hovered) ? ImGuiCol_TabHovered : tab_contents_visible ? (tab_bar_focused ? ImGuiCol_TabSelected : ImGuiCol_TabDimmedSelected) : (tab_bar_focused ? ImGuiCol_Tab : ImGuiCol_TabDimmed));
            TabItemBackground(display_draw_list, bb, flags, tab_col);
            if (tab_contents_visible && (tab_bar.Flags & ImGuiTabBarFlags_DrawSelectedOverline) != 0 && style.TabBarOverlineSize > 0.0f)
            {
                // Might be moved to TabItemBackground() ?
                ImVec2 tl = bb.GetTL() + new ImVec2(0, 1.0f * g.CurrentDpiScale);
                ImVec2 tr = bb.GetTR() + new ImVec2(0, 1.0f * g.CurrentDpiScale);
                ImU32 overline_col = GetColorU32(tab_bar_focused ? ImGuiCol_TabSelectedOverline : ImGuiCol_TabDimmedSelectedOverline);
                if (style.TabRounding > 0.0f)
                {
                    float rounding = style.TabRounding;
                    display_draw_list.PathArcToFast(tl + new ImVec2(+rounding, +rounding), rounding, 7, 9);
                    display_draw_list.PathArcToFast(tr + new ImVec2(-rounding, +rounding), rounding, 9, 11);
                    display_draw_list.PathStroke(overline_col, style.TabBarOverlineSize);
                }
                else
                {
                    display_draw_list.AddLine(tl - new ImVec2(0.5f, 0.5f), tr - new ImVec2(0.5f, 0.5f), overline_col, style.TabBarOverlineSize);
                }
            }
            RenderNavCursor(bb, id);

            // Select with right mouse button. This is so the common idiom for context menu automatically highlight the current widget.
            bool hovered_unblocked = IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup);
            if (tab_bar.SelectedTabId != tab.ID && hovered_unblocked && (IsMouseClicked(ImGuiMouseButton_Right) || IsMouseReleased(ImGuiMouseButton_Right)) && !is_tab_button)
                TabBarQueueFocus(tab_bar, tab);

            if ((tab_bar.Flags & ImGuiTabBarFlags_NoCloseWithMiddleMouseButton) != 0)
                flags |= ImGuiTabItemFlags_NoCloseWithMiddleMouseButton;

            // Render tab label, process close button
            ImGuiID close_button_id = p_open != null ? GetIDWithSeed("#CLOSE", null, docked_window != null ? docked_window.ID : id) : 0;
            bool just_closed;
            bool text_clipped;
            TabItemLabelAndCloseButton(display_draw_list, bb, tab_just_unsaved ? (flags & ~ImGuiTabItemFlags_UnsavedDocument) : flags, tab_bar.FramePadding, label, id, close_button_id, tab_contents_visible, out just_closed, out text_clipped);
            if (just_closed && p_open != null)
            {
                *p_open = false;
                TabBarCloseTab(tab_bar, tab);
            }

            // Forward Hovered state so IsItemHovered() after Begin() can work (even though we are technically hovering our parent)
            // That state is copied to window->DockTabItemStatusFlags by our caller.
            if (docked_window != null && (hovered || g.HoveredId == close_button_id))
                g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HoveredWindow;

            // Tooltip
            // (Won't work over the close button because ItemOverlap systems messes up with HoveredIdTimer-> seems ok)
            // (We test IsItemHovered() to discard e.g. when another item is active or drag and drop over the tab bar, which g.HoveredId ignores)
            // FIXME: This is a mess.
            // FIXME: We may want disabled tab to still display the tooltip?
            if (text_clipped && g.HoveredId == id && !held)
                if ((tab_bar.Flags & ImGuiTabBarFlags_NoTooltip) == 0 && (tab.Flags & ImGuiTabItemFlags_NoTooltip) == 0)
                    SetItemTooltip("%s", label.Substring(0, FindRenderedTextEnd(label))); // C++: SetItemTooltip("%.*s", (int)(FindRenderedTextEnd(label) - label), label)
        }

        // Restore main window position so user can draw there
        if (want_clip_rect)
            PopClipRect();
        window.DC.CursorPos = backup_main_cursor_pos;

        Debug.Assert(!is_tab_button || !(tab_bar.SelectedTabId == tab.ID && is_tab_button)); // TabItemButton should not be selected
        if (is_tab_button)
            return pressed;
        return tab_contents_visible;
    }

    // [Public] This is call is 100% optional but it allows to remove some one-frame glitches when a tab has been unexpectedly removed.
    // To use it to need to call the function SetTabItemClosed() between BeginTabBar() and EndTabBar().
    // Tabs closed by the close button will automatically be flagged to avoid this issue.
    // C++: void ImGui::SetTabItemClosed(const char* label)
    public static void SetTabItemClosed(string label)
    {
        ImGuiContext g = GImGui;
        bool is_within_manual_tab_bar = g.CurrentTabBar != null && (g.CurrentTabBar.Flags & ImGuiTabBarFlags_DockNode) == 0;
        if (is_within_manual_tab_bar)
        {
            ImGuiTabBar tab_bar = g.CurrentTabBar;
            ImGuiID tab_id = TabBarCalcTabID(tab_bar, label, null);
            ImGuiTabItem tab = TabBarFindTabByID(tab_bar, tab_id);
            if (tab != null)
                tab.WantClose = true; // Will be processed by next call to TabBarLayout()
        }
        else if (FindWindowByName(label) is ImGuiWindow window)
        {
            if (window.DockIsActive)
                if (window.DockNode is ImGuiDockNode node)
                {
                    ImGuiID tab_id = TabBarCalcTabID(node.TabBar, label, window);
                    TabBarRemoveTab(node.TabBar, tab_id);
                    window.DockTabWantClose = true;
                }
        }
    }

    // C++: ImVec2 ImGui::TabItemCalcSize(const char* label, bool has_close_button_or_unsaved_marker)
    public static ImVec2 TabItemCalcSize(string label, bool has_close_button_or_unsaved_marker)
    {
        ImGuiContext g = GImGui;
        ImVec2 label_size = CalcTextSize(label, -1, true);
        ImVec2 size = new ImVec2(label_size.x + g.Style.FramePadding.x, label_size.y + g.Style.FramePadding.y * 2.0f);
        if (has_close_button_or_unsaved_marker)
            size.x += g.Style.FramePadding.x + (g.Style.ItemInnerSpacing.x + g.FontSize); // We use Y intentionally to fit the close button circle.
        else
            size.x += g.Style.FramePadding.x + 1.0f;
        return new ImVec2(ImMath.ImMin(size.x, TabBarCalcMaxTabWidth()), size.y);
    }

    // C++: ImVec2 ImGui::TabItemCalcSize(ImGuiWindow* window)
    public static ImVec2 TabItemCalcSize(ImGuiWindow window)
    {
        return TabItemCalcSize(window.Name, window.HasCloseButton || (window.Flags & ImGuiWindowFlags_UnsavedDocument) != 0);
    }

    // C++: void ImGui::TabItemBackground(ImDrawList* draw_list, const ImRect& bb, ImGuiTabItemFlags flags, ImU32 col)
    public static void TabItemBackground(ImDrawList draw_list, ImRect bb, ImGuiTabItemFlags flags, ImU32 col)
    {
        // While rendering tabs, we trim 1 pixel off the top of our bounding box so they can fit within a regular frame height while looking "detached" from it.
        ImGuiContext g = GImGui;
        float width = bb.GetWidth();
        Debug.Assert(width > 0.0f);
        float rounding = ImMath.ImMax(0.0f, ImMath.ImMin((flags & ImGuiTabItemFlags_Button) != 0 ? g.Style.FrameRounding : g.Style.TabRounding, width * 0.5f - 1.0f));
        float y1 = bb.Min.y + 1.0f; // Leave a bit of room in title bars.
        float y2 = bb.Max.y - g.Style.TabBarBorderSize;
        draw_list.AddRectFilled(bb.Min, new ImVec2(bb.Max.x, y2), col, rounding, ImDrawFlags_RoundCornersTop);
        if (g.Style.TabBorderSize > 0.0f)
        {
            draw_list.PathLineTo(new ImVec2(bb.Min.x + 0.5f, y2));
            draw_list.PathArcToFast(new ImVec2(bb.Min.x + rounding + 0.5f, y1 + rounding + 0.5f), rounding, 6, 9);
            draw_list.PathArcToFast(new ImVec2(bb.Max.x - rounding - 0.5f, y1 + rounding + 0.5f), rounding, 9, 12);
            draw_list.PathLineTo(new ImVec2(bb.Max.x - 0.5f, y2));
            draw_list.PathStroke(GetColorU32(ImGuiCol_Border), g.Style.TabBorderSize);
        }
    }

    // Render text label (with custom clipping) + Unsaved Document marker + Close Button logic
    // We tend to lock style.FramePadding for a given tab-bar, hence the 'frame_padding' parameter.
    // C++: void ImGui::TabItemLabelAndCloseButton(ImDrawList* draw_list, const ImRect& bb, ImGuiTabItemFlags flags, ImVec2 frame_padding, const char* label, ImGuiID tab_id, ImGuiID close_button_id, bool is_contents_visible, bool* out_just_closed, bool* out_text_clipped)
    public static void TabItemLabelAndCloseButton(ImDrawList draw_list, ImRect bb, ImGuiTabItemFlags flags, ImVec2 frame_padding, string label, ImGuiID tab_id, ImGuiID close_button_id, bool is_contents_visible, out bool out_just_closed, out bool out_text_clipped)
    {
        ImGuiContext g = GImGui;
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        out_just_closed = false;
        out_text_clipped = false;

        if (bb.GetWidth() <= 1.0f)
            return;

        // In Style V2 we'll have full override of all colors per state (e.g. focused, selected)
        // But right now if you want to alter text color of tabs this is what you need to do.
#if false
        const float backup_alpha = g.Style.Alpha;
        if (!is_contents_visible)
            g.Style.Alpha *= 0.7f;
#endif

        // Render text label (with clipping + alpha gradient) + unsaved marker
        ImRect text_ellipsis_clip_bb = new ImRect(bb.Min.x + frame_padding.x, bb.Min.y + frame_padding.y, bb.Max.x - frame_padding.x, bb.Max.y);

        // Return clipped state ignoring the close button
        {
            out_text_clipped = (text_ellipsis_clip_bb.Min.x + label_size.x) > text_ellipsis_clip_bb.Max.x;
            //draw_list->AddCircle(text_ellipsis_clip_bb.Min, 3.0f, *out_text_clipped ? IM_COL32(255, 0, 0, 255) : IM_COL32(0, 255, 0, 255));
        }

        float button_sz = g.FontSize;
        ImVec2 button_pos = new ImVec2(ImMath.ImMax(bb.Min.x, bb.Max.x - frame_padding.x - button_sz), bb.Min.y + frame_padding.y);

        // Close Button & Unsaved Marker
        // We are relying on a subtle and confusing distinction between 'hovered' and 'g.HoveredId' which happens because we are using ImGuiButtonFlags_AllowOverlapMode + SetItemAllowOverlap()
        //  'hovered' will be true when hovering the Tab but NOT when hovering the close button
        //  'g.HoveredId==id' will be true when hovering the Tab including when hovering the close button
        //  'g.ActiveId==close_button_id' will be true when we are holding on the close button, in which case both hovered booleans are false
        bool close_button_pressed = false;
        bool close_button_visible = false;
        bool is_hovered = g.HoveredId == tab_id || g.HoveredId == close_button_id || g.ActiveId == tab_id || g.ActiveId == close_button_id; // Any interaction account for this too.

        if (close_button_id != 0)
        {
            if (is_contents_visible)
                close_button_visible = (g.Style.TabCloseButtonMinWidthSelected < 0.0f) ? true : (is_hovered && bb.GetWidth() >= ImMath.ImMax(button_sz, g.Style.TabCloseButtonMinWidthSelected));
            else
                close_button_visible = (g.Style.TabCloseButtonMinWidthUnselected < 0.0f) ? true : (is_hovered && bb.GetWidth() >= ImMath.ImMax(button_sz, g.Style.TabCloseButtonMinWidthUnselected));
        }

        // When tabs/document is unsaved, the unsaved marker takes priority over the close button.
        bool unsaved_marker_visible = (flags & ImGuiTabItemFlags_UnsavedDocument) != 0 && (button_pos.x + button_sz <= bb.Max.x) && (!close_button_visible || !is_hovered);
        if (unsaved_marker_visible)
        {
            ImVec2 bullet_pos = button_pos + new ImVec2(button_sz, button_sz) * 0.5f;
            RenderBullet(draw_list, bullet_pos, GetColorU32(ImGuiCol_UnsavedMarker));
        }
        else if (close_button_visible)
        {
            ImGuiLastItemData last_item_backup = g.LastItemData;
            if (CloseButton(close_button_id, button_pos))
                close_button_pressed = true;
            g.LastItemData = last_item_backup;

            // Close with middle mouse button
            if (is_hovered && (flags & ImGuiTabItemFlags_NoCloseWithMiddleMouseButton) == 0 && IsMouseClicked(ImGuiMouseButton_Middle))
                close_button_pressed = true;
        }

        // This is all rather complicated
        // (the main idea is that because the close button only appears on hover, we don't want it to alter the ellipsis position)
        // FIXME: if FramePadding is noticeably large, ellipsis_max_x will be wrong here (e.g. #3497), maybe for consistency that parameter of RenderTextEllipsis() shouldn't exist..
        float ellipsis_max_x = text_ellipsis_clip_bb.Max.x;
        if (close_button_visible || unsaved_marker_visible)
        {
            bool visible_without_hover = unsaved_marker_visible || (is_contents_visible ? g.Style.TabCloseButtonMinWidthSelected : g.Style.TabCloseButtonMinWidthUnselected) < 0.0f;
            if (visible_without_hover)
            {
                text_ellipsis_clip_bb.Max.x -= button_sz * 0.90f;
                ellipsis_max_x -= button_sz * 0.90f;
            }
            else
            {
                text_ellipsis_clip_bb.Max.x -= button_sz * 1.00f;
            }
        }
        LogSetNextTextDecoration("/", "\\");
        RenderTextEllipsis(draw_list, text_ellipsis_clip_bb.Min, text_ellipsis_clip_bb.Max, ellipsis_max_x, label, label_end, label_size);

#if false
        if (!is_contents_visible)
            g.Style.Alpha = backup_alpha;
#endif

        out_just_closed = close_button_pressed;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Columns, BeginColumns, EndColumns, etc.
    // (This is a legacy API, prefer using BeginTable/EndTable!)
    //-------------------------------------------------------------------------
    // FIXME: sizing is lossy when columns width is very small (default width may turn negative etc.)
    //-------------------------------------------------------------------------

    // [Internal] Small optimization to avoid calls to PopClipRect/SetCurrentChannel/PushClipRect in sequences,
    // they would meddle many times with the underlying ImDrawCmd.
    // Instead, we do a preemptive overwrite of clipping rectangle _without_ altering the command-buffer and let
    // the subsequent single call to SetCurrentChannel() does it things once.
    // C++: void ImGui::SetWindowClipRectBeforeSetChannel(ImGuiWindow* window, const ImRect& clip_rect)   [imgui_tables.cpp 4486]
    public static void SetWindowClipRectBeforeSetChannel(ImGuiWindow window, ImRect clip_rect)
    {
        ImVec4 clip_rect_vec4 = new ImVec4(clip_rect.Min.x, clip_rect.Min.y, clip_rect.Max.x, clip_rect.Max.y); // C++: clip_rect.ToVec4()
        window.ClipRect = clip_rect;
        window.DrawList._CmdHeader.ClipRect = clip_rect_vec4;
        window.DrawList._ClipRectStack.Data[window.DrawList._ClipRectStack.Size - 1] = clip_rect_vec4;
    }

    // C++: int ImGui::GetColumnIndex()
    public static int GetColumnIndex()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.DC.CurrentColumns != null ? window.DC.CurrentColumns.Current : 0;
    }

    // C++: int ImGui::GetColumnsCount()
    public static int GetColumnsCount()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        return window.DC.CurrentColumns != null ? window.DC.CurrentColumns.Count : 1;
    }

    // C++: float ImGui::GetColumnOffsetFromNorm(const ImGuiOldColumns* columns, float offset_norm)
    public static float GetColumnOffsetFromNorm(ImGuiOldColumns columns, float offset_norm)
    {
        return offset_norm * (columns.OffMaxX - columns.OffMinX);
    }

    // C++: float ImGui::GetColumnNormFromOffset(const ImGuiOldColumns* columns, float offset)
    public static float GetColumnNormFromOffset(ImGuiOldColumns columns, float offset)
    {
        return offset / (columns.OffMaxX - columns.OffMinX);
    }

    // C++: static const float COLUMNS_HIT_RECT_HALF_THICKNESS = 4.0f;
    private const float COLUMNS_HIT_RECT_HALF_THICKNESS = 4.0f;

    // C++: static float GetDraggedColumnOffset(ImGuiOldColumns* columns, int column_index)
    public static float GetDraggedColumnOffset(ImGuiOldColumns columns, int column_index)
    {
        // Active (dragged) column always follow mouse. The reason we need this is that dragging a column to the right edge of an auto-resizing
        // window creates a feedback loop because we store normalized positions. So while dragging we enforce absolute positioning.
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert(column_index > 0); // We are not supposed to drag column 0.
        Debug.Assert(g.ActiveId == columns.ID + (ImGuiID)column_index);

        float x = g.IO.MousePos.x - g.ActiveIdClickOffset.x + ImMath.ImTrunc(COLUMNS_HIT_RECT_HALF_THICKNESS * g.CurrentDpiScale) - window.Pos.x;
        x = ImMath.ImMax(x, GetColumnOffset(column_index - 1) + g.Style.ColumnsMinSpacing);
        if ((columns.Flags & ImGuiOldColumnFlags_NoPreserveWidths) != 0)
            x = ImMath.ImMin(x, GetColumnOffset(column_index + 1) - g.Style.ColumnsMinSpacing);

        return x;
    }

    // C++: float ImGui::GetColumnOffset(int column_index)
    public static float GetColumnOffset(int column_index = -1)
    {
        ImGuiWindow window = GetCurrentWindowRead();
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        if (columns == null)
            return 0.0f;

        if (column_index < 0)
            column_index = columns.Current;
        Debug.Assert(column_index < columns.Columns.Size);

        float t = columns.Columns[column_index].OffsetNorm;
        float x_offset = ImMath.ImLerp(columns.OffMinX, columns.OffMaxX, t);
        return x_offset;
    }

    // C++: static float GetColumnWidthEx(ImGuiOldColumns* columns, int column_index, bool before_resize = false)
    public static float GetColumnWidthEx(ImGuiOldColumns columns, int column_index, bool before_resize = false)
    {
        if (column_index < 0)
            column_index = columns.Current;

        float offset_norm;
        if (before_resize)
            offset_norm = columns.Columns[column_index + 1].OffsetNormBeforeResize - columns.Columns[column_index].OffsetNormBeforeResize;
        else
            offset_norm = columns.Columns[column_index + 1].OffsetNorm - columns.Columns[column_index].OffsetNorm;
        return GetColumnOffsetFromNorm(columns, offset_norm);
    }

    // C++: float ImGui::GetColumnWidth(int column_index)
    public static float GetColumnWidth(int column_index = -1)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        if (columns == null)
            return GetContentRegionAvail().x;

        if (column_index < 0)
            column_index = columns.Current;
        return GetColumnOffsetFromNorm(columns, columns.Columns[column_index + 1].OffsetNorm - columns.Columns[column_index].OffsetNorm);
    }

    // C++: void ImGui::SetColumnOffset(int column_index, float offset)
    public static void SetColumnOffset(int column_index, float offset)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        Debug.Assert(columns != null);

        if (column_index < 0)
            column_index = columns.Current;
        Debug.Assert(column_index < columns.Columns.Size);

        bool preserve_width = (columns.Flags & ImGuiOldColumnFlags_NoPreserveWidths) == 0 && (column_index < columns.Count - 1);
        float width = preserve_width ? GetColumnWidthEx(columns, column_index, columns.IsBeingResized) : 0.0f;

        if ((columns.Flags & ImGuiOldColumnFlags_NoForceWithinWindow) == 0)
            offset = ImMath.ImMin(offset, columns.OffMaxX - g.Style.ColumnsMinSpacing * (columns.Count - column_index));
        {
            ImGuiOldColumnData column = columns.Columns[column_index]; // copy-modify-write (struct element)
            column.OffsetNorm = GetColumnNormFromOffset(columns, offset - columns.OffMinX);
            columns.Columns[column_index] = column;
        }

        if (preserve_width)
            SetColumnOffset(column_index + 1, offset + ImMath.ImMax(g.Style.ColumnsMinSpacing, width));
    }

    // C++: void ImGui::SetColumnWidth(int column_index, float width)
    public static void SetColumnWidth(int column_index, float width)
    {
        ImGuiWindow window = GetCurrentWindowRead();
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        Debug.Assert(columns != null);

        if (column_index < 0)
            column_index = columns.Current;
        SetColumnOffset(column_index + 1, GetColumnOffset(column_index) + width);
    }

    // C++: void ImGui::PushColumnClipRect(int column_index)
    public static void PushColumnClipRect(int column_index = -1)
    {
        ImGuiWindow window = GetCurrentWindowRead();
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        if (column_index < 0)
            column_index = columns.Current;

        ImGuiOldColumnData column = columns.Columns[column_index];
        PushClipRect(column.ClipRect.Min, column.ClipRect.Max, false);
    }

    // Get into the columns background draw command (which is generally the same draw command as before we called BeginColumns)
    // C++: void ImGui::PushColumnsBackground()
    public static void PushColumnsBackground()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        if (columns.Count == 1)
            return;

        // Optimization: avoid SetCurrentChannel() + PushClipRect()
        columns.HostBackupClipRect = window.ClipRect;
        SetWindowClipRectBeforeSetChannel(window, columns.HostInitialClipRect);
        columns.Splitter.SetCurrentChannel(window.DrawList, 0);
    }

    // C++: void ImGui::PopColumnsBackground()
    public static void PopColumnsBackground()
    {
        ImGuiWindow window = GetCurrentWindowRead();
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        if (columns.Count == 1)
            return;

        // Optimization: avoid PopClipRect() + SetCurrentChannel()
        SetWindowClipRectBeforeSetChannel(window, columns.HostBackupClipRect);
        columns.Splitter.SetCurrentChannel(window.DrawList, columns.Current + 1);
    }

    // C++: ImGuiOldColumns* ImGui::FindOrCreateColumns(ImGuiWindow* window, ImGuiID id)
    public static ImGuiOldColumns FindOrCreateColumns(ImGuiWindow window, ImGuiID id)
    {
        // We have few columns per window so for now we don't need bother much with turning this into a faster lookup.
        for (int n = 0; n < window.ColumnsStorage.Size; n++)
            if (window.ColumnsStorage[n].ID == id)
                return window.ColumnsStorage[n];

        window.ColumnsStorage.push_back(new ImGuiOldColumns());
        ImGuiOldColumns columns = window.ColumnsStorage.back();
        columns.ID = id;
        return columns;
    }

    // C++: ImGuiID ImGui::GetColumnsID(const char* str_id, int columns_count)
    public static ImGuiID GetColumnsID(string str_id, int columns_count)
    {
        ImGuiWindow window = GetCurrentWindow();

        // Differentiate column ID with an arbitrary prefix for cases where users name their columns set the same as another widget.
        // In addition, when an identifier isn't explicitly provided we include the number of columns in the hash to make it uniquer.
        PushID(0x11223347 + (str_id != null ? 0 : columns_count));
        ImGuiID id = window.GetID(str_id != null ? str_id : "columns");
        PopID();

        return id;
    }

    // C++: void ImGui::BeginColumns(const char* str_id, int columns_count, ImGuiOldColumnFlags flags)
    public static void BeginColumns(string str_id, int columns_count, ImGuiOldColumnFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();

        Debug.Assert(columns_count >= 1);
        Debug.Assert(window.DC.CurrentColumns == null);   // Nested columns are currently not supported

        // Acquire storage for the columns set
        ImGuiID id = GetColumnsID(str_id, columns_count);
        ImGuiOldColumns columns = FindOrCreateColumns(window, id);
        Debug.Assert(columns.ID == id);
        columns.Current = 0;
        columns.Count = columns_count;
        columns.Flags = flags;
        window.DC.CurrentColumns = columns;
        window.DC.NavIsScrollPushableX = false; // Shortcut for NavUpdateCurrentWindowIsScrollPushableX();

        columns.HostCursorPosY = window.DC.CursorPos.y;
        columns.HostCursorMaxPosX = window.DC.CursorMaxPos.x;
        columns.HostInitialClipRect = window.ClipRect;
        columns.HostBackupParentWorkRect = window.ParentWorkRect;
        window.ParentWorkRect = window.WorkRect;

        // Set state for first column
        // We aim so that the right-most column will have the same clipping width as other after being clipped by parent ClipRect
        float column_padding = g.Style.ItemSpacing.x;
        float half_clip_extend_x = ImMath.ImTrunc(ImMath.ImMax(window.WindowPadding.x * 0.5f, window.WindowBorderSize));
        float max_1 = window.WorkRect.Max.x + column_padding - ImMath.ImMax(column_padding - window.WindowPadding.x, 0.0f);
        float max_2 = window.WorkRect.Max.x + half_clip_extend_x;
        columns.OffMinX = window.DC.Indent.x - column_padding + ImMath.ImMax(column_padding - window.WindowPadding.x, 0.0f);
        columns.OffMaxX = ImMath.ImMax(ImMath.ImMin(max_1, max_2) - window.Pos.x, columns.OffMinX + 1.0f);
        columns.LineMinY = columns.LineMaxY = window.DC.CursorPos.y;

        // Clear data if columns count changed
        if (columns.Columns.Size != 0 && columns.Columns.Size != columns_count + 1)
            columns.Columns.resize(0);

        // Initialize default widths
        columns.IsFirstFrame = (columns.Columns.Size == 0);
        if (columns.Columns.Size == 0)
        {
            columns.Columns.reserve(columns_count + 1);
            for (int n = 0; n < columns_count + 1; n++)
            {
                ImGuiOldColumnData column = default; // C++ ctor memsets to zero
                column.OffsetNorm = n / (float)columns_count;
                columns.Columns.push_back(column);
            }
        }

        for (int n = 0; n < columns_count; n++)
        {
            // Compute clipping rectangle
            ImGuiOldColumnData column = columns.Columns[n];
            float clip_x1 = ImMath.ImRound(window.Pos.x + GetColumnOffset(n));
            float clip_x2 = ImMath.ImRound(window.Pos.x + GetColumnOffset(n + 1) - 1.0f);
            column.ClipRect = new ImRect(clip_x1, -float.MaxValue, clip_x2, +float.MaxValue);
            column.ClipRect.ClipWithFull(window.ClipRect);
            columns.Columns[n] = column;
        }

        if (columns.Count > 1)
        {
            columns.Splitter.Split(window.DrawList, 1 + columns.Count);
            columns.Splitter.SetCurrentChannel(window.DrawList, 1);
            PushColumnClipRect(0);
        }

        // We don't generally store Indent.x inside ColumnsOffset because it may be manipulated by the user.
        float offset_0 = GetColumnOffset(columns.Current);
        float offset_1 = GetColumnOffset(columns.Current + 1);
        float width = offset_1 - offset_0;
        PushItemWidth(width * 0.65f);
        window.DC.ColumnsOffset.x = ImMath.ImMax(column_padding - window.WindowPadding.x, 0.0f);
        window.DC.CursorPos.x = ImMath.ImTrunc(window.Pos.x + window.DC.Indent.x + window.DC.ColumnsOffset.x);
        window.WorkRect.Max.x = window.Pos.x + offset_1 - column_padding;
        window.WorkRect.Max.y = window.ContentRegionRect.Max.y;
    }

    // C++: void ImGui::NextColumn()
    public static void NextColumn()
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems || window.DC.CurrentColumns == null)
            return;

        ImGuiContext g = GImGui;
        ImGuiOldColumns columns = window.DC.CurrentColumns;

        if (columns.Count == 1)
        {
            window.DC.CursorPos.x = ImMath.ImTrunc(window.Pos.x + window.DC.Indent.x + window.DC.ColumnsOffset.x);
            Debug.Assert(columns.Current == 0);
            return;
        }

        // Next column
        if (++columns.Current == columns.Count)
            columns.Current = 0;

        PopItemWidth();

        // Optimization: avoid PopClipRect() + SetCurrentChannel() + PushClipRect()
        // (which would needlessly attempt to update commands in the wrong channel, then pop or overwrite them),
        ImRect column_clip_rect = columns.Columns[columns.Current].ClipRect;
        SetWindowClipRectBeforeSetChannel(window, column_clip_rect);
        columns.Splitter.SetCurrentChannel(window.DrawList, columns.Current + 1);

        float column_padding = g.Style.ItemSpacing.x;
        columns.LineMaxY = ImMath.ImMax(columns.LineMaxY, window.DC.CursorPos.y);
        if (columns.Current > 0)
        {
            // Columns 1+ ignore IndentX (by canceling it out)
            // FIXME-COLUMNS: Unnecessary, could be locked?
            window.DC.ColumnsOffset.x = GetColumnOffset(columns.Current) - window.DC.Indent.x + column_padding;
        }
        else
        {
            // New row/line: column 0 honor IndentX.
            window.DC.ColumnsOffset.x = ImMath.ImMax(column_padding - window.WindowPadding.x, 0.0f);
            window.DC.IsSameLine = false;
            columns.LineMinY = columns.LineMaxY;
        }
        window.DC.CursorPos.x = ImMath.ImTrunc(window.Pos.x + window.DC.Indent.x + window.DC.ColumnsOffset.x);
        window.DC.CursorPos.y = columns.LineMinY;
        window.DC.CurrLineSize = new ImVec2(0.0f, 0.0f);
        window.DC.CurrLineTextBaseOffset = 0.0f;

        // FIXME-COLUMNS: Share code with BeginColumns() - move code on columns setup.
        float offset_0 = GetColumnOffset(columns.Current);
        float offset_1 = GetColumnOffset(columns.Current + 1);
        float width = offset_1 - offset_0;
        PushItemWidth(width * 0.65f);
        window.WorkRect.Max.x = window.Pos.x + offset_1 - column_padding;
    }

    // C++: void ImGui::EndColumns()
    public static void EndColumns()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        Debug.Assert(columns != null);

        PopItemWidth();
        if (columns.Count > 1)
        {
            PopClipRect();
            columns.Splitter.Merge(window.DrawList);
        }

        ImGuiOldColumnFlags flags = columns.Flags;
        columns.LineMaxY = ImMath.ImMax(columns.LineMaxY, window.DC.CursorPos.y);
        window.DC.CursorPos.y = columns.LineMaxY;
        if ((flags & ImGuiOldColumnFlags_GrowParentContentsSize) == 0)
            window.DC.CursorMaxPos.x = columns.HostCursorMaxPosX;  // Restore cursor max pos, as columns don't grow parent

        // Draw columns borders and handle resize
        // The IsBeingResized flag ensure we preserve pre-resize columns width so back-and-forth are not lossy
        bool is_being_resized = false;
        if ((flags & ImGuiOldColumnFlags_NoBorder) == 0 && !window.SkipItems)
        {
            // We clip Y boundaries CPU side because very long triangles are mishandled by some GPU drivers.
            float y1 = ImMath.ImMax(columns.HostCursorPosY, window.ClipRect.Min.y);
            float y2 = ImMath.ImMin(window.DC.CursorPos.y, window.ClipRect.Max.y);
            int dragging_column = -1;
            for (int n = 1; n < columns.Count; n++)
            {
                ImGuiOldColumnData column = columns.Columns[n];
                float x = window.Pos.x + GetColumnOffset(n);
                ImGuiID column_id = columns.ID + (ImGuiID)n;
                float column_hit_hw = ImMath.ImTrunc(COLUMNS_HIT_RECT_HALF_THICKNESS * g.CurrentDpiScale);
                ImRect column_hit_rect = new ImRect(new ImVec2(x - column_hit_hw, y1), new ImVec2(x + column_hit_hw, y2));
                if (!ItemAdd(column_hit_rect, column_id, null, ImGuiItemFlags_NoNav))
                    continue;

                bool hovered = false, held = false;
                if ((flags & ImGuiOldColumnFlags_NoResize) == 0)
                {
                    ButtonBehavior(column_hit_rect, column_id, out hovered, out held);
                    if (hovered || held)
                        SetMouseCursor(ImGuiMouseCursor_ResizeEW);
                    if (held && (column.Flags & ImGuiOldColumnFlags_NoResize) == 0)
                        dragging_column = n;
                }

                // Draw column
                ImU32 col = GetColorU32(held ? ImGuiCol_SeparatorActive : hovered ? ImGuiCol_SeparatorHovered : ImGuiCol_Separator);
                float xi = ImMath.ImTrunc(x);
                window.DrawList.AddLineV(xi, y1 + 1.0f, y2, col);
            }

            // Apply dragging after drawing the column lines, so our rendered lines are in sync with how items were displayed during the frame.
            if (dragging_column != -1)
            {
                if (!columns.IsBeingResized)
                    for (int n = 0; n < columns.Count + 1; n++)
                    {
                        ImGuiOldColumnData column = columns.Columns[n];
                        column.OffsetNormBeforeResize = column.OffsetNorm;
                        columns.Columns[n] = column;
                    }
                columns.IsBeingResized = is_being_resized = true;
                float x = GetDraggedColumnOffset(columns, dragging_column);
                SetColumnOffset(dragging_column, x);
            }
        }
        columns.IsBeingResized = is_being_resized;

        window.WorkRect = window.ParentWorkRect;
        window.ParentWorkRect = columns.HostBackupParentWorkRect;
        window.DC.CurrentColumns = null;
        window.DC.ColumnsOffset.x = 0.0f;
        window.DC.CursorPos.x = ImMath.ImTrunc(window.Pos.x + window.DC.Indent.x + window.DC.ColumnsOffset.x);
        NavUpdateCurrentWindowIsScrollPushableX();
    }

    // C++: void ImGui::Columns(int columns_count, const char* id, bool borders)
    public static void Columns(int columns_count = 1, string id = null, bool borders = true)
    {
        ImGuiWindow window = GetCurrentWindow();
        Debug.Assert(columns_count >= 1);

        ImGuiOldColumnFlags flags = borders ? 0 : ImGuiOldColumnFlags_NoBorder;
        //flags |= ImGuiOldColumnFlags_NoPreserveWidths; // NB: Legacy behavior
        ImGuiOldColumns columns = window.DC.CurrentColumns;
        if (columns != null && columns.Count == columns_count && columns.Flags == flags)
            return;

        if (columns != null)
            EndColumns();

        if (columns_count != 1)
            BeginColumns(id, columns_count, flags);
    }
}
