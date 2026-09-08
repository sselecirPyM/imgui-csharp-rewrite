// Port of imgui_demo.cpp [SECTION] DemoWindowWidgetsPlotting() (2107-2177),
// DemoWindowWidgetsProgressBars() (2183-2214), DemoWindowWidgetsQueryingStatuses() (2220-2438),
// DemoWindowWidgetsSelectables() (2444-2608), DemoWindowWidgetsSelectionAndMultiSelect() (2842-3576)
// and DemoWindowWidgetsTabs() (3594-3748), including the ExampleNames[] array (2617),
// ExampleSelectionWithDeletion (2625), ExampleDualListBox (2688) and
// EditTabBarFittingPolicyFlags (3582) helpers.
// C++ function-local 'static' variables are hosted in the file-local DemoWidgets2Locals class below.
using System;
using System.Text;
using static ImGui.ImGui; // for unqualified ImGui.* calls inside namespace-level Example classes

namespace ImGui;

// C++: static const char* ExampleNames[] (imgui_demo.cpp 2617-2620)
// C++: struct ExampleSelectionWithDeletion : ImGuiSelectionBasicStorage (imgui_demo.cpp 2625-2686)
// C++: struct ExampleDualListBox (imgui_demo.cpp 2688-2840) are defined after DemoWidgets2Locals.

// Function-local statics of the DemoWindowWidgets* functions ported in this file.
file static class DemoWidgets2Locals
{
    // C++: static const char* ExampleNames[] (imgui_demo.cpp 2617-2620)
    public static readonly string[] ExampleNames =
    {
        "Artichoke", "Arugula", "Asparagus", "Avocado", "Bamboo Shoots", "Bean Sprouts", "Beans", "Beet", "Belgian Endive", "Bell Pepper",
        "Bitter Gourd", "Bok Choy", "Broccoli", "Brussels Sprouts", "Burdock Root", "Cabbage", "Calabash", "Capers", "Carrot", "Cassava",
        "Cauliflower", "Celery", "Celery Root", "Celcuce", "Chayote", "Chinese Broccoli", "Corn", "Cucumber"
    };

    // DemoWindowWidgetsPlotting()
    public static bool Animate = true;
    public static float[] Arr = { 0.6f, 0.1f, 1.0f, 0.5f, 0.92f, 0.1f, 0.2f };   // C++: static float arr[]
    public static float[] Values = new float[90];                                 // C++: static float values[90]
    public static int ValuesOffset = 0;
    public static double RefreshTime = 0.0;
    public static float Phase = 0.0f;                                             // C++: 'static float phase' inside the while loop
    public static int FuncType = 0;
    public static int DisplayCount = 70;

    // DemoWindowWidgetsProgressBars()
    public static float ProgressAccum = 0.0f;
    public static float ProgressDir = 1.0f;

    // DemoWindowWidgetsQueryingStatuses()
    public static int ItemType = 4;
    public static bool ItemDisabled = false;
    public static bool LiveEditFlagsOverride = false;
    public static ImGuiItemFlags LiveEditFlags = 0;                               // ImGuiItemFlags
    public static bool B = false;                                                 // C++: static bool b
    public static float[] Col4f = { 1.0f, 0.5f, 0.0f, 1.0f };                     // C++: static float col4f[4]
    public static byte[] Str = new byte[16];                                      // C++: static char str[16]
    public static int ComboCurrent = 1;                                           // C++: (item_type == 14) 'static int current'
    public static int ListBoxCurrent = 1;                                         // C++: (item_type == 15) 'static int current'
    public static bool EmbedAllInsideAChildWindow = false;
    public static bool TestWindow = false;

    // DemoWindowWidgetsSelectables()
    public static bool[] BasicSelection = { false, true, false, false, false };   // C++: static bool selection[5] = { false, true, false, false };
    public static bool[] SameLineSelected = new bool[3];
    public static bool[] SameLineChecked = new bool[5];
    public static int SameLineSelectedN = 0;
    public static bool[] TableSelected = new bool[10];
    public static byte[,] GridSelected = { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }; // C++: static char selected[4][4]
    public static bool[] AlignSelected = { true, false, true, false, true, false, true, false, true };

    // DemoWindowWidgetsSelectionAndMultiSelect()
    public static int SingleSelectSelected = -1;
    public static bool[] ManualSelection = new bool[5];
    public static ImGuiSelectionBasicStorage MultiSelectSelection = new ImGuiSelectionBasicStorage();
    public static ImGuiSelectionBasicStorage ClipperSelection = new ImGuiSelectionBasicStorage();
    public static ImVector<ImGuiID> DeletionItems = new ImVector<ImGuiID>();
    public static ExampleSelectionWithDeletion DeletionSelection = new ExampleSelectionWithDeletion();
    public static ImGuiID DeletionItemsNextId = 0;
    public static ExampleDualListBox Dlb = new ExampleDualListBox();
    public static ImGuiSelectionBasicStorage TableSelection = new ImGuiSelectionBasicStorage();
    public static bool[] CheckboxItems = new bool[20];
    public static ImGuiMultiSelectFlags CheckboxFlags = ImGuiMultiSelectFlags_NoAutoSelect | ImGuiMultiSelectFlags_NoAutoClear | ImGuiMultiSelectFlags_ClearOnEscape;
    public static ImGuiSelectionBasicStorage[] ScopesSelectionsData =
    {
        new ImGuiSelectionBasicStorage(), new ImGuiSelectionBasicStorage(), new ImGuiSelectionBasicStorage()
    };
    public static ImGuiMultiSelectFlags ScopesFlags = ImGuiMultiSelectFlags_ScopeRect | ImGuiMultiSelectFlags_ClearOnEscape; // | ImGuiMultiSelectFlags_ClearOnClickVoid;
    public static ImGuiSelectionBasicStorage TreesSelection = new ImGuiSelectionBasicStorage();

    // DemoWindowWidgetsSelectionAndMultiSelect() - "Multi-Select (advanced)"
    // C++: enum WidgetType { WidgetType_Selectable, WidgetType_TreeNode };
    public const int WidgetType_Selectable = 0;
    public const int WidgetType_TreeNode = 1;
    public static bool UseClipper = true;
    public static bool UseDeletion = true;
    public static bool UseDragDrop = true;
    public static bool ShowInTable = false;
    public static bool ShowColorButton = true;
    public static ImGuiMultiSelectFlags AdvancedFlags = ImGuiMultiSelectFlags_ClearOnEscape | ImGuiMultiSelectFlags_BoxSelect1d;
    public static int WidgetType = WidgetType_Selectable;
    public static ImVector<int> AdvancedItems = new ImVector<int>();
    public static int AdvancedItemsNextId = 0;
    public static ExampleSelectionWithDeletion AdvancedSelection = new ExampleSelectionWithDeletion();
    public static bool RequestDeletionFromMenu = false; // Queue deletion triggered from context menu

    // DemoWindowWidgetsTabs() - "Advanced & Close Button"
    public static ImGuiTabBarFlags AdvancedTabBarFlags = ImGuiTabBarFlags_Reorderable;
    public static bool[] AdvancedOpened = { true, true, true, true }; // Persistent user state

    // DemoWindowWidgetsTabs() - "TabItemButton & Leading/Trailing flags"
    public static ImVector<int> ActiveTabs = new ImVector<int>();
    public static int NextTabId = 0;
    public static bool ShowLeadingButton = true;
    public static bool ShowTrailingButton = true;
    public static ImGuiTabBarFlags LeadingTrailingTabBarFlags = ImGuiTabBarFlags_AutoSelectNewTabs | ImGuiTabBarFlags_Reorderable | ImGuiTabBarFlags_FittingPolicyShrink;
}

// Extra functions to add deletion support to ImGuiSelectionBasicStorage
// C++: struct ExampleSelectionWithDeletion : ImGuiSelectionBasicStorage (imgui_demo.cpp 2625-2686)
public class ExampleSelectionWithDeletion : ImGuiSelectionBasicStorage
{
    // Find which item should be Focused after deletion.
    // Call _before_ item submission. Return an index in the before-deletion item list, your item loop should call SetKeyboardFocusHere() on it.
    // The subsequent ApplyDeletionPostLoop() code will use it to apply Selection.
    // - We cannot provide this logic in core Dear ImGui because we don't have access to selection data.
    // - We don't actually manipulate the ImVector<> here, only in ApplyDeletionPostLoop(), but using similar API for consistency and flexibility.
    // - Important: Deletion only works if the underlying ImGuiID for your items are stable: aka not depend on their index, but on e.g. item id/ptr.
    // FIXME-MULTISELECT: Doesn't take account of the possibility focus target will be moved during deletion. Need refocus or scroll offset.
    // C++: int ApplyDeletionPreLoop(ImGuiMultiSelectIO* ms_io, int items_count)
    public int ApplyDeletionPreLoop(ImGuiMultiSelectIO ms_io, int items_count)
    {
        if (Size == 0)
            return -1;

        // If focused item is not selected...
        int focused_idx = (int)ms_io.NavIdItem;  // Index of currently focused item
        if (ms_io.NavIdSelected == false)        // This is merely a shortcut, == Contains(adapter->IndexToStorage(items, focused_idx))
        {
            ms_io.RangeSrcReset = true;          // Request to recover RangeSrc from NavId next frame. Would be ok to reset even when NavIdSelected==true, but it would take an extra frame to recover RangeSrc when deleting a selected item.
            return focused_idx;                  // Request to focus same item after deletion.
        }

        // If focused item is selected: land on first unselected item after focused item.
        for (int idx = focused_idx + 1; idx < items_count; idx++)
            if (!Contains(GetStorageIdFromIndex(idx)))
                return idx;

        // If focused item is selected: otherwise return last unselected item before focused item.
        for (int idx = ImMath.ImMin(focused_idx, items_count) - 1; idx >= 0; idx--)
            if (!Contains(GetStorageIdFromIndex(idx)))
                return idx;

        return -1;
    }

    // Rewrite item list (delete items) + update selection.
    // - Call after EndMultiSelect()
    // - We cannot provide this logic in core Dear ImGui because we don't have access to your items, nor to selection data.
    // C++: template<typename ITEM_TYPE> void ApplyDeletionPostLoop(ImGuiMultiSelectIO* ms_io, ImVector<ITEM_TYPE>& items, int item_curr_idx_to_select)
    // (C# adaptation: a generic method over ImVector<T>; works for both ImVector<int> and ImVector<ImGuiID> users.)
    public void ApplyDeletionPostLoop<T>(ImGuiMultiSelectIO ms_io, ImVector<T> items, int item_curr_idx_to_select)
    {
        // Rewrite item list (delete items) + convert old selection index (before deletion) to new selection index (after selection).
        // If NavId was not part of selection, we will stay on same item.
        ImVector<T> new_items = new ImVector<T>();
        new_items.reserve(items.Size - Size);
        int item_next_idx_to_select = -1;
        for (int idx = 0; idx < items.Size; idx++)
        {
            if (!Contains(GetStorageIdFromIndex(idx)))
                new_items.push_back(items[idx]);
            if (item_curr_idx_to_select == idx)
                item_next_idx_to_select = new_items.Size - 1;
        }
        // C++: items.swap(new_items);
        T[] swap_data = items.Data;
        int swap_size = items.Size;
        items.Data = new_items.Data;
        items.Size = new_items.Size;
        new_items.Data = swap_data;
        new_items.Size = swap_size;

        // Update selection
        Clear();
        if (item_next_idx_to_select != -1 && ms_io.NavIdSelected)
            SetItemSelected(GetStorageIdFromIndex(item_next_idx_to_select), true);
    }
}

// Example: Implement dual list box storage and interface
// C++: struct ExampleDualListBox (imgui_demo.cpp 2688-2840)
public class ExampleDualListBox
{
    public ImVector<ImGuiID>[] Items = { new ImVector<ImGuiID>(), new ImVector<ImGuiID>() }; // ID is index into ExampleName[]
    public ImGuiSelectionBasicStorage[] Selections = { new ImGuiSelectionBasicStorage(), new ImGuiSelectionBasicStorage() }; // Store ExampleItemId into selection
    public bool OptKeepSorted = true;

    // C++: void MoveAll(int src, int dst)
    public void MoveAll(int src, int dst)
    {
        System.Diagnostics.Debug.Assert((src == 0 && dst == 1) || (src == 1 && dst == 0));
        for (int n = 0; n < Items[src].Size; n++)
            Items[dst].push_back(Items[src][n]);
        Items[src].clear();
        SortItems(dst);
        Selections[src].Swap(Selections[dst]);
        Selections[src].Clear();
    }

    // C++: void MoveSelected(int src, int dst)
    public void MoveSelected(int src, int dst)
    {
        for (int src_n = 0; src_n < Items[src].Size; src_n++)
        {
            ImGuiID item_id = Items[src][src_n];
            if (!Selections[src].Contains(item_id))
                continue;
            Items[src].erase(src_n); // FIXME-OPT: Could be implemented more optimally (rebuild src items and swap)
            Items[dst].push_back(item_id);
            src_n--;
        }
        if (OptKeepSorted)
            SortItems(dst);
        Selections[src].Swap(Selections[dst]);
        Selections[src].Clear();
    }

    // C++: void ApplySelectionRequests(ImGuiMultiSelectIO* ms_io, int side)
    public void ApplySelectionRequests(ImGuiMultiSelectIO ms_io, int side)
    {
        // In this example we store item id in selection (instead of item index)
        Selections[side].UserData = Items[side].Data;
        Selections[side].AdapterIndexToStorageId = (ImGuiSelectionBasicStorage self, int idx) => ((ImGuiID[])self.UserData)[idx];
        Selections[side].ApplyRequests(ms_io);
    }

    // C++: static int IMGUI_CDECL CompareItemsByValue(const void* lhs, const void* rhs) + qsort in SortItems()
    // (C# adaptation: Array.Sort on the used prefix sorts ImGuiID values ascending, same as CompareItemsByValue.)
    public void SortItems(int n)
    {
        Array.Sort(Items[n].Data, 0, Items[n].Size);
    }

    // C++: void Show()
    public void Show()
    {
        //if (ImGui::Checkbox("Sorted", &OptKeepSorted) && OptKeepSorted) { SortItems(0); SortItems(1); }
        if (ImGui.BeginTable("split", 3, ImGuiTableFlags_None))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags_WidthStretch);    // Left side
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed);      // Buttons
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags_WidthStretch);    // Right side
            ImGui.TableNextRow();

            int request_move_selected = -1;
            int request_move_all = -1;
            float child_height_0 = 0.0f;
            for (int side = 0; side < 2; side++)
            {
                // FIXME-MULTISELECT: Dual List Box: Add context menus
                // FIXME-NAV: Using ImGuiWindowFlags_NavFlattened exhibit many issues.
                ImVector<ImGuiID> items = Items[side];
                ImGuiSelectionBasicStorage selection = Selections[side];

                ImGui.TableSetColumnIndex((side == 0) ? 0 : 2);
                ImGui.Text("%s (%d)", (side == 0) ? "Available" : "Basket", items.Size);

                // Submit scrolling range to avoid glitches on moving/deletion
                float items_height = ImGui.GetTextLineHeightWithSpacing();
                ImGui.SetNextWindowContentSize(new ImVec2(0.0f, items.Size * items_height));

                bool child_visible;
                if (side == 0)
                {
                    // Left child is resizable
                    ImGui.SetNextWindowSizeConstraints(new ImVec2(0.0f, ImGui.GetFrameHeightWithSpacing() * 4), new ImVec2(float.MaxValue /* FLT_MAX */, float.MaxValue /* FLT_MAX */));
                    child_visible = ImGui.BeginChild("0", new ImVec2(-float.Epsilon /* -FLT_MIN */, ImGui.GetFontSize() * 20), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY);
                    child_height_0 = ImGui.GetWindowSize().y;
                }
                else
                {
                    // Right child use same height as left one
                    child_visible = ImGui.BeginChild("1", new ImVec2(-float.Epsilon /* -FLT_MIN */, child_height_0), ImGuiChildFlags_FrameStyle);
                }
                if (child_visible)
                {
                    ImGuiMultiSelectFlags flags = ImGuiMultiSelectFlags_BoxSelect1d;
                    ImGuiMultiSelectIO ms_io = ImGui.BeginMultiSelect(flags, selection.Size, items.Size);
                    ApplySelectionRequests(ms_io, side);

                    for (int item_n = 0; item_n < items.Size; item_n++)
                    {
                        ImGuiID item_id = items[item_n];
                        bool item_is_selected = selection.Contains(item_id);
                        ImGui.SetNextItemSelectionUserData(item_n);
                        ImGui.Selectable(DemoWidgets2Locals.ExampleNames[(int)item_id], item_is_selected, ImGuiSelectableFlags_AllowDoubleClick);
                        if (ImGui.IsItemFocused())
                        {
                            // FIXME-MULTISELECT: Dual List Box: Transfer focus
                            if (ImGui.IsKeyPressed(ImGuiKey_Enter) || ImGui.IsKeyPressed(ImGuiKey_KeypadEnter))
                                request_move_selected = side;
                            if (ImGui.IsMouseDoubleClicked(0)) // FIXME-MULTISELECT: Double-click on multi-selection?
                                request_move_selected = side;
                        }
                    }

                    ms_io = ImGui.EndMultiSelect();
                    ApplySelectionRequests(ms_io, side);
                }
                ImGui.EndChild();
            }

            // Buttons columns
            ImGui.TableSetColumnIndex(1);
            ImGui.NewLine();
            //ImVec2 button_sz = { ImGui::CalcTextSize(">>").x + ImGui::GetStyle().FramePadding.x * 2.0f, ImGui::GetFrameHeight() + padding.y * 2.0f };
            ImVec2 button_sz = new ImVec2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());

            // (Using BeginDisabled()/EndDisabled() works but feels distracting given how it is currently visualized)
            if (ImGui.Button(">>", button_sz))
                request_move_all = 0;
            if (ImGui.Button(">", button_sz))
                request_move_selected = 0;
            if (ImGui.Button("<", button_sz))
                request_move_selected = 1;
            if (ImGui.Button("<<", button_sz))
                request_move_all = 1;

            // Process requests
            if (request_move_all != -1)
                MoveAll(request_move_all, request_move_all ^ 1);
            if (request_move_selected != -1)
                MoveSelected(request_move_selected, request_move_selected ^ 1);

            // FIXME-MULTISELECT: Support action from outside
            /*
            if (OptKeepSorted == false)
            {
                ImGui::NewLine();
                if (ImGui::ArrowButton("MoveUp", ImGuiDir_Up)) {}
                if (ImGui::ArrowButton("MoveDown", ImGuiDir_Down)) {}
            }
            */

            ImGui.EndTable();
        }
    }
}

public static partial class ImGui
{
    // C++: static void DemoWindowWidgetsPlotting()   [imgui_demo.cpp 2107-2177]
    public static void DemoWindowWidgetsPlotting()
    {
        // Plot/Graph widgets are not very good.
        // Consider using a third-party library such as ImPlot: https://github.com/epezent/implot
        // (see others https://github.com/ocornut/imgui/wiki/Useful-Extensions)
        if (TreeNode("Plotting"))
        {
            Text("Need better plotting and graphing? Consider using ImPlot:");
            TextLinkOpenURL("https://github.com/epezent/implot");
            Separator();

            Checkbox("Animate", ref DemoWidgets2Locals.Animate);

            // Plot as lines and plot as histogram
            float[] arr = DemoWidgets2Locals.Arr;
            PlotLines("Frame Times", arr, arr.Length);
            PlotHistogram("Histogram", arr, arr.Length, 0, null, 0.0f, 1.0f, new ImVec2(0, 80.0f));
            //ImGui::SameLine(); HelpMarker("Consider using ImPlot instead!");

            // Fill an array of contiguous float values to plot
            // Tip: If your float aren't contiguous but part of a structure, you can pass a pointer to your first float
            // and the sizeof() of your structure in the "stride" parameter.
            float[] values = DemoWidgets2Locals.Values;
            if (!DemoWidgets2Locals.Animate || DemoWidgets2Locals.RefreshTime == 0.0)
                DemoWidgets2Locals.RefreshTime = GetTime();
            while (DemoWidgets2Locals.RefreshTime < GetTime()) // Create data at fixed 60 Hz rate for the demo
            {
                values[DemoWidgets2Locals.ValuesOffset] = MathF.Cos(DemoWidgets2Locals.Phase);
                DemoWidgets2Locals.ValuesOffset = (DemoWidgets2Locals.ValuesOffset + 1) % values.Length;
                DemoWidgets2Locals.Phase += 0.10f * DemoWidgets2Locals.ValuesOffset;
                DemoWidgets2Locals.RefreshTime += 1.0f / 60.0f;
            }

            // Plots can display overlay texts
            // (in this example, we will display an average value)
            {
                float average = 0.0f;
                for (int n = 0; n < values.Length; n++)
                    average += values[n];
                average /= (float)values.Length;
                string overlay = ImFmt.Sprintf("avg %f", average);
                PlotLines("Lines", values, values.Length, DemoWidgets2Locals.ValuesOffset, overlay, -1.0f, 1.0f, new ImVec2(0, 80.0f));
            }

            // Use functions to generate output
            // FIXME: This is actually VERY awkward because current plot API only pass in indices.
            // We probably want an API passing floats and user provide sample rate/count.
            // (C++: local 'struct Funcs' with static Sin/Saw, ported as the DemoW2_PlotFuncSin/DemoW2_PlotFuncSaw helpers below.)
            SeparatorText("Functions");
            SetNextItemWidth(GetFontSize() * 8);
            Combo("func", ref DemoWidgets2Locals.FuncType, "Sin\0Saw\0");
            SameLine();
            SliderInt("Sample count", ref DemoWidgets2Locals.DisplayCount, 1, 400);
            Func<object, int, float> func = (DemoWidgets2Locals.FuncType == 0) ? (Func<object, int, float>)DemoW2_PlotFuncSin : DemoW2_PlotFuncSaw;
            PlotLines("Lines##2", func, null, DemoWidgets2Locals.DisplayCount, 0, null, -1.0f, 1.0f, new ImVec2(0, 80));
            PlotHistogram("Histogram##2", func, null, DemoWidgets2Locals.DisplayCount, 0, null, -1.0f, 1.0f, new ImVec2(0, 80));

            TreePop();
        }
    }

    // C++: 'struct Funcs { static float Sin(void*, int i) { return sinf(i * 0.1f); } }'
    private static float DemoW2_PlotFuncSin(object data, int i) => MathF.Sin(i * 0.1f);

    // C++: 'struct Funcs { static float Saw(void*, int i) { return (i & 1) ? 1.0f : -1.0f; } }'
    private static float DemoW2_PlotFuncSaw(object data, int i) => (i & 1) != 0 ? 1.0f : -1.0f;

    // C++: static void DemoWindowWidgetsProgressBars()   [imgui_demo.cpp 2183-2214]
    public static void DemoWindowWidgetsProgressBars()
    {
        if (TreeNode("Progress Bars"))
        {
            // Animate a simple progress bar
            DemoWidgets2Locals.ProgressAccum += DemoWidgets2Locals.ProgressDir * 0.4f * GetIO().DeltaTime;
            if (DemoWidgets2Locals.ProgressAccum >= +1.1f) { DemoWidgets2Locals.ProgressAccum = +1.1f; DemoWidgets2Locals.ProgressDir *= -1.0f; }
            if (DemoWidgets2Locals.ProgressAccum <= -0.1f) { DemoWidgets2Locals.ProgressAccum = -0.1f; DemoWidgets2Locals.ProgressDir *= -1.0f; }

            float progress = ImMath.ImClamp(DemoWidgets2Locals.ProgressAccum, 0.0f, 1.0f);

            // Typically we would use ImVec2(-1.0f,0.0f) or ImVec2(-FLT_MIN,0.0f) to use all available width,
            // or ImVec2(width,0.0f) for a specified width. ImVec2(0.0f,0.0f) uses ItemWidth.
            ProgressBar(progress, new ImVec2(0.0f, 0.0f));
            SameLine(0.0f, GetStyle().ItemInnerSpacing.x);
            Text("Progress Bar");

            string buf = ImFmt.Sprintf("%d/%d", (int)(progress * 1753), 1753);
            ProgressBar(progress, new ImVec2(0.0f, 0.0f), buf);

            // Pass an animated negative value, e.g. -1.0f * (float)ImGui::GetTime() is the recommended value.
            // Adjust the factor if you want to adjust the animation speed.
            ProgressBar(-1.0f * (float)GetTime(), new ImVec2(0.0f, 0.0f), "Searching..");
            SameLine(0.0f, GetStyle().ItemInnerSpacing.x);
            Text("Indeterminate");

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsQueryingStatuses()   [imgui_demo.cpp 2220-2438]
    public static void DemoWindowWidgetsQueryingStatuses()
    {
        if (TreeNode("Querying Item Status (Edited/Active/Hovered etc.)"))
        {
            // Select an item type
            string[] item_names =
            {
                "Text", "Button", "Button (w/ repeat)", "Checkbox", "SliderFloat", "InputText", "InputTextMultiline", "InputFloat",
                "InputFloat3", "ColorEdit4", "Selectable", "MenuItem", "TreeNode", "TreeNode (w/ double-click)", "Combo", "ListBox"
            };
            Combo("Item Type", ref DemoWidgets2Locals.ItemType, item_names, item_names.Length, item_names.Length);
            SameLine();
            HelpMarker("Testing how various types of items are interacting with the IsItemXXX functions. Note that the bool return value of most ImGui function is generally equivalent to calling ImGui::IsItemHovered().");
            Checkbox("Item Disabled", ref DemoWidgets2Locals.ItemDisabled);
            Checkbox("Override LiveEdit:", ref DemoWidgets2Locals.LiveEditFlagsOverride);
            SameLine();
            if (!DemoWidgets2Locals.LiveEditFlagsOverride)
                DemoWidgets2Locals.LiveEditFlags = GetItemFlags();
            BeginDisabled(DemoWidgets2Locals.LiveEditFlagsOverride == false);
            CheckboxFlags("_LiveEditOnInput", ref DemoWidgets2Locals.LiveEditFlags, ImGuiItemFlags_LiveEditOnInput);
            SameLine();
            CheckboxFlags("_LiveEditOnInputText", ref DemoWidgets2Locals.LiveEditFlags, ImGuiItemFlags_LiveEditOnInputText);
            SameLine();
            CheckboxFlags("_LiveEditOnInputScalar", ref DemoWidgets2Locals.LiveEditFlags, ImGuiItemFlags_LiveEditOnInputScalar);
            EndDisabled();
            if (DemoWidgets2Locals.LiveEditFlagsOverride)
            {
                PushItemFlag(ImGuiItemFlags_LiveEditOnInputText, (DemoWidgets2Locals.LiveEditFlags & ImGuiItemFlags_LiveEditOnInputText) != 0);
                PushItemFlag(ImGuiItemFlags_LiveEditOnInputScalar, (DemoWidgets2Locals.LiveEditFlags & ImGuiItemFlags_LiveEditOnInputScalar) != 0);
            }

            // Submit selected items so we can query their status in the code following it.
            bool ret = false;
            bool item_disabled = DemoWidgets2Locals.ItemDisabled;
            int item_type = DemoWidgets2Locals.ItemType;
            if (item_disabled)
                BeginDisabled(true);
            if (item_type == 0) { Text("ITEM: Text"); }                                              // Testing text items with no identifier/interaction
            if (item_type == 1) { ret = Button("ITEM: Button"); }                                    // Testing button
            if (item_type == 2) { PushItemFlag(ImGuiItemFlags_ButtonRepeat, true); ret = Button("ITEM: Button"); PopItemFlag(); } // Testing button (with repeater)
            if (item_type == 3) { ret = Checkbox("ITEM: Checkbox", ref DemoWidgets2Locals.B); }                            // Testing checkbox
            if (item_type == 4) { ret = SliderFloat("ITEM: SliderFloat", ref DemoWidgets2Locals.Col4f[0], 0.0f, 1.0f); }   // Testing basic item
            if (item_type == 5) { ret = InputText("ITEM: InputText", DemoWidgets2Locals.Str, DemoWidgets2Locals.Str.Length); }  // Testing input text (which handles tabbing)
            if (item_type == 6) { ret = InputTextMultiline("ITEM: InputTextMultiline", DemoWidgets2Locals.Str, DemoWidgets2Locals.Str.Length); } // Testing input text (which uses a child window)
            if (item_type == 7) { ret = InputFloat("ITEM: InputFloat", ref DemoWidgets2Locals.Col4f[0], 1.0f); }           // Testing +/- buttons on scalar input
            if (item_type == 8) { ret = InputFloat3("ITEM: InputFloat3", DemoWidgets2Locals.Col4f); }                       // Testing multi-component items (IsItemXXX flags are reported merged)
            if (item_type == 9) { ret = ColorEdit4("ITEM: ColorEdit4", DemoWidgets2Locals.Col4f); }                         // Testing multi-component items (IsItemXXX flags are reported merged)
            if (item_type == 10) { ret = Selectable("ITEM: Selectable"); }                            // Testing selectable item
            if (item_type == 11) { ret = MenuItem("ITEM: MenuItem"); }                                // Testing menu item (they use ImGuiButtonFlags_PressedOnRelease button policy)
            if (item_type == 12) { ret = TreeNode("ITEM: TreeNode"); if (ret) TreePop(); }     // Testing tree node
            if (item_type == 13) { ret = TreeNodeEx("ITEM: TreeNode w/ ImGuiTreeNodeFlags_OpenOnDoubleClick", ImGuiTreeNodeFlags_OpenOnDoubleClick | ImGuiTreeNodeFlags_NoTreePushOnOpen); } // Testing tree node with ImGuiButtonFlags_PressedOnDoubleClick button policy.
            if (item_type == 14) { string[] items = { "Apple", "Banana", "Cherry", "Kiwi" }; ret = Combo("ITEM: Combo", ref DemoWidgets2Locals.ComboCurrent, items, items.Length); }
            if (item_type == 15) { string[] items = { "Apple", "Banana", "Cherry", "Kiwi" }; ret = ListBox("ITEM: ListBox", ref DemoWidgets2Locals.ListBoxCurrent, items, items.Length, items.Length); }

            bool hovered_delay_none = IsItemHovered();
            bool hovered_delay_stationary = IsItemHovered(ImGuiHoveredFlags_Stationary);
            bool hovered_delay_short = IsItemHovered(ImGuiHoveredFlags_DelayShort);
            bool hovered_delay_normal = IsItemHovered(ImGuiHoveredFlags_DelayNormal);
            bool hovered_delay_tooltip = IsItemHovered(ImGuiHoveredFlags_ForTooltip); // = Normal + Stationary

            // Display the values of IsItemHovered() and other common item state functions.
            // Note that the ImGuiHoveredFlags_XXX flags can be combined.
            // Because BulletText is an item itself and that would affect the output of IsItemXXX functions,
            // we query every state in a single call to avoid storing them and to simplify the code.
            BulletText(
                "Return value = %d\n" +
                "IsItemFocused() = %d\n" +
                "IsItemHovered() = %d\n" +
                "IsItemHovered(_AllowWhenBlockedByPopup) = %d\n" +
                "IsItemHovered(_AllowWhenBlockedByActiveItem) = %d\n" +
                "IsItemHovered(_AllowWhenOverlappedByItem) = %d\n" +
                "IsItemHovered(_AllowWhenOverlappedByWindow) = %d\n" +
                "IsItemHovered(_AllowWhenDisabled) = %d\n" +
                "IsItemHovered(_RectOnly) = %d\n" +
                "IsItemActive() = %d\n" +
                "IsItemEdited() = %d\n" +
                "IsItemActivated() = %d\n" +
                "IsItemDeactivated() = %d\n" +
                "IsItemDeactivatedAfterEdit() = %d\n" +
                "IsItemVisible() = %d\n" +
                "IsItemClicked() = %d\n" +
                "IsItemToggledOpen() = %d\n" +
                "GetItemRectMin() = (%.1f, %.1f)\n" +
                "GetItemRectMax() = (%.1f, %.1f)\n" +
                "GetItemRectSize() = (%.1f, %.1f)",
                ret,
                IsItemFocused(),
                IsItemHovered(),
                IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup),
                IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem),
                IsItemHovered(ImGuiHoveredFlags_AllowWhenOverlappedByItem),
                IsItemHovered(ImGuiHoveredFlags_AllowWhenOverlappedByWindow),
                IsItemHovered(ImGuiHoveredFlags_AllowWhenDisabled),
                IsItemHovered(ImGuiHoveredFlags_RectOnly),
                IsItemActive(),
                IsItemEdited(),
                IsItemActivated(),
                IsItemDeactivated(),
                IsItemDeactivatedAfterEdit(),
                IsItemVisible(),
                IsItemClicked(0),
                IsItemToggledOpen(),
                GetItemRectMin().x, GetItemRectMin().y,
                GetItemRectMax().x, GetItemRectMax().y,
                GetItemRectSize().x, GetItemRectSize().y
            );
            BulletText(
                "with Hovering Delay or Stationary test:\n" +
                "IsItemHovered() = %d\n" +
                "IsItemHovered(_Stationary) = %d\n" +
                "IsItemHovered(_DelayShort) = %d\n" +
                "IsItemHovered(_DelayNormal) = %d\n" +
                "IsItemHovered(_Tooltip) = %d",
                hovered_delay_none, hovered_delay_stationary, hovered_delay_short, hovered_delay_normal, hovered_delay_tooltip);

            if (DemoWidgets2Locals.LiveEditFlagsOverride)
            {
                PopItemFlag();
                PopItemFlag();
            }
            if (item_disabled)
                EndDisabled();

            byte[] buf = new byte[1]; // C++: char buf[1] = "";
            InputText("unused", buf, buf.Length, ImGuiInputTextFlags_ReadOnly);
            SameLine();
            HelpMarker("This widget is only here to be able to tab-out of the widgets above and see e.g. Deactivated() status.");

            TreePop();
        }

        if (TreeNode("Querying Window Status (Focused/Hovered etc.)"))
        {
            Checkbox("Embed everything inside a child window for testing _RootWindow flag.", ref DemoWidgets2Locals.EmbedAllInsideAChildWindow);
            if (DemoWidgets2Locals.EmbedAllInsideAChildWindow)
                BeginChild("outer_child", new ImVec2(0, GetFontSize() * 20.0f), ImGuiChildFlags_Borders);

            // Testing IsWindowFocused() function with its various flags.
            BulletText(
                "IsWindowFocused() = %d\n" +
                "IsWindowFocused(_ChildWindows) = %d\n" +
                "IsWindowFocused(_ChildWindows|_NoPopupHierarchy) = %d\n" +
                "IsWindowFocused(_ChildWindows|_RootWindow) = %d\n" +
                "IsWindowFocused(_ChildWindows|_RootWindow|_NoPopupHierarchy) = %d\n" +
                "IsWindowFocused(_RootWindow) = %d\n" +
                "IsWindowFocused(_RootWindow|_NoPopupHierarchy) = %d\n" +
                "IsWindowFocused(_AnyWindow) = %d\n",
                IsWindowFocused(),
                IsWindowFocused(ImGuiFocusedFlags_ChildWindows),
                IsWindowFocused(ImGuiFocusedFlags_ChildWindows | ImGuiFocusedFlags_NoPopupHierarchy),
                IsWindowFocused(ImGuiFocusedFlags_ChildWindows | ImGuiFocusedFlags_RootWindow),
                IsWindowFocused(ImGuiFocusedFlags_ChildWindows | ImGuiFocusedFlags_RootWindow | ImGuiFocusedFlags_NoPopupHierarchy),
                IsWindowFocused(ImGuiFocusedFlags_RootWindow),
                IsWindowFocused(ImGuiFocusedFlags_RootWindow | ImGuiFocusedFlags_NoPopupHierarchy),
                IsWindowFocused(ImGuiFocusedFlags_AnyWindow));

            // Testing IsWindowHovered() function with its various flags.
            BulletText(
                "IsWindowHovered() = %d\n" +
                "IsWindowHovered(_AllowWhenBlockedByPopup) = %d\n" +
                "IsWindowHovered(_AllowWhenBlockedByActiveItem) = %d\n" +
                "IsWindowHovered(_ChildWindows) = %d\n" +
                "IsWindowHovered(_ChildWindows|_NoPopupHierarchy) = %d\n" +
                "IsWindowHovered(_ChildWindows|_RootWindow) = %d\n" +
                "IsWindowHovered(_ChildWindows|_RootWindow|_NoPopupHierarchy) = %d\n" +
                "IsWindowHovered(_RootWindow) = %d\n" +
                "IsWindowHovered(_RootWindow|_NoPopupHierarchy) = %d\n" +
                "IsWindowHovered(_ChildWindows|_AllowWhenBlockedByPopup) = %d\n" +
                "IsWindowHovered(_AnyWindow) = %d\n" +
                "IsWindowHovered(_Stationary) = %d\n",
                IsWindowHovered(),
                IsWindowHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup),
                IsWindowHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem),
                IsWindowHovered(ImGuiHoveredFlags_ChildWindows),
                IsWindowHovered(ImGuiHoveredFlags_ChildWindows | ImGuiHoveredFlags_NoPopupHierarchy),
                IsWindowHovered(ImGuiHoveredFlags_ChildWindows | ImGuiHoveredFlags_RootWindow),
                IsWindowHovered(ImGuiHoveredFlags_ChildWindows | ImGuiHoveredFlags_RootWindow | ImGuiHoveredFlags_NoPopupHierarchy),
                IsWindowHovered(ImGuiHoveredFlags_RootWindow),
                IsWindowHovered(ImGuiHoveredFlags_RootWindow | ImGuiHoveredFlags_NoPopupHierarchy),
                IsWindowHovered(ImGuiHoveredFlags_ChildWindows | ImGuiHoveredFlags_AllowWhenBlockedByPopup),
                IsWindowHovered(ImGuiHoveredFlags_AnyWindow),
                IsWindowHovered(ImGuiHoveredFlags_Stationary));

            BeginChild("child", new ImVec2(0, 50), ImGuiChildFlags_Borders);
            Text("This is another child window for testing the _ChildWindows flag.");
            EndChild();
            if (DemoWidgets2Locals.EmbedAllInsideAChildWindow)
                EndChild();

            // Calling IsItemHovered() after begin returns the hovered status of the title bar.
            // This is useful in particular if you want to create a context menu associated to the title bar of a window.
            Checkbox("Hovered/Active tests after Begin() for title bar testing", ref DemoWidgets2Locals.TestWindow);
            if (DemoWidgets2Locals.TestWindow)
            {
                Begin("Title bar Hovered/Active tests", ref DemoWidgets2Locals.TestWindow);
                if (BeginPopupContextItem()) // <-- This is using IsItemHovered()
                {
                    if (MenuItem("Close")) { DemoWidgets2Locals.TestWindow = false; }
                    EndPopup();
                }
                Text(
                    "IsItemHovered() after begin = %d (== is title bar hovered)\n" +
                    "IsItemActive() after begin = %d (== is window being clicked/moved)\n",
                    IsItemHovered(), IsItemActive());
                End();
            }

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsSelectables()   [imgui_demo.cpp 2444-2608]
    public static void DemoWindowWidgetsSelectables()
    {
        //ImGui::SetNextItemOpen(true, ImGuiCond_Once);
        if (TreeNode("Selectables"))
        {
            // Selectable() has 2 overloads:
            // - The one taking "bool selected" as a read-only selection information.
            //   When Selectable() has been clicked it returns true and you can alter selection state accordingly.
            // - The one taking "bool* p_selected" as a read-write selection information (convenient in some cases)
            // The earlier is more flexible, as in real application your selection may be stored in many different ways
            // and not necessarily inside a bool value (e.g. in flags within objects, as an external list, etc).
            if (TreeNode("Basic"))
            {
                bool[] selection = DemoWidgets2Locals.BasicSelection;
                Selectable("1. I am selectable", ref selection[0]);
                Selectable("2. I am selectable", ref selection[1]);
                Selectable("3. I am selectable", ref selection[2]);
                if (Selectable("4. I am double clickable", selection[3], ImGuiSelectableFlags_AllowDoubleClick))
                    if (IsMouseDoubleClicked(0))
                        selection[3] = !selection[3];
                TreePop();
            }

            if (TreeNode("Multiple items on the same line"))
            {
                // - Using SetNextItemAllowOverlap()
                // - Using the Selectable() override that takes "bool* p_selected" parameter, the bool value is toggled automatically.
                {
                    bool[] selected = DemoWidgets2Locals.SameLineSelected;
                    SetNextItemAllowOverlap(); Selectable("main.c", ref selected[0]); SameLine(); SmallButton("Link 1");
                    SetNextItemAllowOverlap(); Selectable("hello.cpp", ref selected[1]); SameLine(); SmallButton("Link 2");
                    SetNextItemAllowOverlap(); Selectable("hello.h", ref selected[2]); SameLine(); SmallButton("Link 3");
                }

                // (2)
                // - Using ImGuiSelectableFlags_AllowOverlap is a shortcut for calling SetNextItemAllowOverlap()
                // - No visible label, display contents inside the selectable bounds.
                // - We don't maintain actual selection in this example to keep things simple.
                Spacing();
                {
                    bool[] checked_arr = DemoWidgets2Locals.SameLineChecked;
                    bool[] selected = DemoWidgets2Locals.SameLineSelected;
                    int selected_n = DemoWidgets2Locals.SameLineSelectedN;
                    float color_marker_w = CalcTextSize("x").x;
                    for (int n = 0; n < 5; n++)
                    {
                        PushID(n);
                        AlignTextToFramePadding();
                        if (Selectable("##selectable", selected_n == n, ImGuiSelectableFlags_AllowOverlap))
                            selected_n = n;
                        SameLine(0, 0);
                        Checkbox("##check", ref checked_arr[n]);
                        SameLine();
                        ImVec4 color = new ImVec4((n & 1) != 0 ? 1.0f : 0.2f, (n & 2) != 0 ? 1.0f : 0.2f, 0.2f, 1.0f);
                        ColorButton("##color", color, ImGuiColorEditFlags_NoTooltip, new ImVec2(color_marker_w, 0));
                        SameLine();
                        Text("Some label");
                        PopID();
                    }
                    DemoWidgets2Locals.SameLineSelectedN = selected_n;
                }

                TreePop();
            }

            if (TreeNode("In Tables"))
            {
                bool[] selected = DemoWidgets2Locals.TableSelected;

                if (BeginTable("split1", 3, ImGuiTableFlags_Resizable | ImGuiTableFlags_NoSavedSettings | ImGuiTableFlags_Borders))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        string label = ImFmt.Sprintf("Item %d", i);
                        TableNextColumn();
                        Selectable(label, ref selected[i]); // FIXME-TABLE: Selection overlap
                    }
                    EndTable();
                }
                Spacing();
                if (BeginTable("split2", 3, ImGuiTableFlags_Resizable | ImGuiTableFlags_NoSavedSettings | ImGuiTableFlags_Borders))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        string label = ImFmt.Sprintf("Item %d", i);
                        TableNextRow();
                        TableNextColumn();
                        Selectable(label, ref selected[i], ImGuiSelectableFlags_SpanAllColumns);
                        TableNextColumn();
                        Text("Some other contents");
                        TableNextColumn();
                        Text("123456");
                    }
                    EndTable();
                }
                TreePop();
            }

            if (TreeNode("Grid"))
            {
                byte[,] selected = DemoWidgets2Locals.GridSelected; // C++: static char selected[4][4]

                // Add in a bit of silly fun...
                float time = (float)GetTime();
                bool winning_state = true; // C++: memchr(selected, 0, sizeof(selected)) == NULL, i.e. if all cells are selected...
                for (int i = 0; i < 16; i++)
                    if (selected[i / 4, i % 4] == 0)
                        winning_state = false;
                if (winning_state)
                    PushStyleVar(ImGuiStyleVar_SelectableTextAlign, new ImVec2(0.5f + 0.5f * MathF.Cos(time * 2.0f), 0.5f + 0.5f * MathF.Sin(time * 3.0f)));

                float size = CalcTextSize("Sailor").x;
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        if (x > 0)
                            SameLine();
                        PushID(y * 4 + x);
                        if (Selectable("Sailor", selected[y, x] != 0, 0, new ImVec2(size, size)))
                        {
                            // Toggle clicked cell + toggle neighbors
                            selected[y, x] ^= 1;
                            if (x > 0) { selected[y, x - 1] ^= 1; }
                            if (x < 3) { selected[y, x + 1] ^= 1; }
                            if (y > 0) { selected[y - 1, x] ^= 1; }
                            if (y < 3) { selected[y + 1, x] ^= 1; }
                        }
                        PopID();
                    }

                if (winning_state)
                    PopStyleVar();
                TreePop();
            }
            if (TreeNode("Alignment"))
            {
                HelpMarker(
                    "By default, Selectables uses style.SelectableTextAlign but it can be overridden on a per-item " +
                    "basis using PushStyleVar(). You'll probably want to always keep your default situation to " +
                    "left-align otherwise it becomes difficult to layout multiple items on a same line");

                bool[] selected = DemoWidgets2Locals.AlignSelected;
                float size = CalcTextSize("(1.0,1.0)").x;
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        ImVec2 alignment = new ImVec2((float)x / 2.0f, (float)y / 2.0f);
                        string name = ImFmt.Sprintf("(%.1f,%.1f)", alignment.x, alignment.y);
                        if (x > 0) SameLine();
                        PushStyleVar(ImGuiStyleVar_SelectableTextAlign, alignment);
                        Selectable(name, ref selected[3 * y + x], ImGuiSelectableFlags_None, new ImVec2(size, size));
                        PopStyleVar();
                    }
                }
                TreePop();
            }
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsSelectionAndMultiSelect(ImGuiDemoWindowData* demo_data)   [imgui_demo.cpp 2842-3576]
    public static void DemoWindowWidgetsSelectionAndMultiSelect(ImGuiDemoWindowData demo_data)
    {
        if (TreeNode("Selection State & Multi-Select"))
        {
            HelpMarker("Selections can be built using Selectable(), TreeNode() or other widgets. Selection state is owned by application code/data.");

            BulletText("Wiki page:");
            SameLine();
            TextLinkOpenURL("imgui/wiki/Multi-Select", "https://github.com/ocornut/imgui/wiki/Multi-Select");

            // Without any fancy API: manage single-selection yourself.
            if (TreeNode("Single-Select"))
            {
                for (int n = 0; n < 5; n++)
                {
                    string buf = ImFmt.Sprintf("Object %d", n);
                    if (Selectable(buf, DemoWidgets2Locals.SingleSelectSelected == n))
                        DemoWidgets2Locals.SingleSelectSelected = n;
                }
                TreePop();
            }

            // Demonstrate implementation a most-basic form of multi-selection manually
            // This doesn't support the Shift modifier which requires BeginMultiSelect()!
            if (TreeNode("Multi-Select (manual/simplified, without BeginMultiSelect)"))
            {
                HelpMarker("Hold Ctrl and Click to select multiple items.");
                bool[] selection = DemoWidgets2Locals.ManualSelection;
                for (int n = 0; n < 5; n++)
                {
                    string buf = ImFmt.Sprintf("Object %d", n);
                    if (Selectable(buf, selection[n]))
                    {
                        if (!GetIO().KeyCtrl) // Clear selection when Ctrl is not held
                            Array.Clear(selection, 0, selection.Length); // C++: memset(selection, 0, sizeof(selection));
                        selection[n] = !selection[n]; // C++: selection[n] ^= 1; Toggle current item
                    }
                }
                TreePop();
            }

            // Demonstrate handling proper multi-selection using the BeginMultiSelect/EndMultiSelect API.
            // Shift+Click w/ Ctrl and other standard features are supported.
            // We use the ImGuiSelectionBasicStorage helper which you may freely reimplement.
            if (TreeNode("Multi-Select"))
            {
                Text("Supported features:");
                BulletText("Keyboard navigation (arrows, page up/down, home/end, space).");
                BulletText("Ctrl modifier to preserve and toggle selection.");
                BulletText("Shift modifier for range selection.");
                BulletText("Ctrl+A to select all.");
                BulletText("Escape to clear selection.");
                BulletText("Click and drag to box-select.");
                Text("Tip: Use 'Demo->Tools->Debug Log->Selection' to see selection requests as they happen.");

                // Use default selection.Adapter: Pass index to SetNextItemSelectionUserData(), store index in Selection
                const int ITEMS_COUNT = 50;
                ImGuiSelectionBasicStorage selection = DemoWidgets2Locals.MultiSelectSelection;
                Text("Selection: %d/%d", selection.Size, ITEMS_COUNT);

                // The BeginChild() has no purpose for selection logic, other that offering a scrolling region.
                if (BeginChild("##Basket", new ImVec2(-float.Epsilon /* -FLT_MIN */, GetFontSize() * 20), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY))
                {
                    ImGuiMultiSelectFlags flags = ImGuiMultiSelectFlags_ClearOnEscape | ImGuiMultiSelectFlags_BoxSelect1d;
                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(flags, selection.Size, ITEMS_COUNT);
                    selection.ApplyRequests(ms_io);

                    for (int n = 0; n < ITEMS_COUNT; n++)
                    {
                        string label = ImFmt.Sprintf("Object %05d: %s", n, DemoWidgets2Locals.ExampleNames[n % DemoWidgets2Locals.ExampleNames.Length]);
                        bool item_is_selected = selection.Contains((ImGuiID)n);
                        SetNextItemSelectionUserData(n);
                        Selectable(label, item_is_selected);
                    }

                    ms_io = EndMultiSelect();
                    selection.ApplyRequests(ms_io);
                }
                EndChild();
                TreePop();
            }

            // Demonstrate using the clipper with BeginMultiSelect()/EndMultiSelect()
            if (TreeNode("Multi-Select (with clipper)"))
            {
                // Use default selection.Adapter: Pass index to SetNextItemSelectionUserData(), store index in Selection
                ImGuiSelectionBasicStorage selection = DemoWidgets2Locals.ClipperSelection;

                Text("Added features:");
                BulletText("Using ImGuiListClipper.");

                const int ITEMS_COUNT = 10000;
                Text("Selection: %d/%d", selection.Size, ITEMS_COUNT);
                if (BeginChild("##Basket", new ImVec2(-float.Epsilon /* -FLT_MIN */, GetFontSize() * 20), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY))
                {
                    ImGuiMultiSelectFlags flags = ImGuiMultiSelectFlags_ClearOnEscape | ImGuiMultiSelectFlags_BoxSelect1d;
                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(flags, selection.Size, ITEMS_COUNT);
                    selection.ApplyRequests(ms_io);

                    ImGuiListClipper clipper = new ImGuiListClipper();
                    clipper.Begin(ITEMS_COUNT);
                    if (ms_io.RangeSrcItem != -1)
                        clipper.IncludeItemByIndex((int)ms_io.RangeSrcItem); // Ensure RangeSrc item is not clipped.
                    while (clipper.Step())
                    {
                        for (int n = clipper.DisplayStart; n < clipper.DisplayEnd; n++)
                        {
                            string label = ImFmt.Sprintf("Object %05d: %s", n, DemoWidgets2Locals.ExampleNames[n % DemoWidgets2Locals.ExampleNames.Length]);
                            bool item_is_selected = selection.Contains((ImGuiID)n);
                            SetNextItemSelectionUserData(n);
                            Selectable(label, item_is_selected);
                        }
                    }

                    ms_io = EndMultiSelect();
                    selection.ApplyRequests(ms_io);
                }
                EndChild();
                TreePop();
            }

            // Demonstrate dynamic item list + deletion support using the BeginMultiSelect/EndMultiSelect API.
            // In order to support Deletion without any glitches you need to:
            // - (1) If items are submitted in their own scrolling area, submit contents size SetNextWindowContentSize() ahead of time to prevent one-frame readjustment of scrolling.
            // - (2) Items needs to have persistent ID Stack identifier = ID needs to not depends on their index. PushID(index) = KO. PushID(item_id) = OK. This is in order to focus items reliably after a selection.
            // - (3) BeginXXXX process
            // - (4) Focus process
            // - (5) EndXXXX process
            if (TreeNode("Multi-Select (with deletion)"))
            {
                // Storing items data separately from selection data.
                // (you may decide to store selection data inside your item (aka intrusive storage) if you don't need multiple views over same items)
                // Use a custom selection.Adapter: store item identifier in Selection (instead of index)
                ImVector<ImGuiID> items = DemoWidgets2Locals.DeletionItems;
                ExampleSelectionWithDeletion selection = DemoWidgets2Locals.DeletionSelection;
                selection.UserData = items; // C++: (void*)&items
                selection.AdapterIndexToStorageId = (ImGuiSelectionBasicStorage self, int idx) => ((ImVector<ImGuiID>)self.UserData)[idx]; // Index -> ID

                Text("Added features:");
                BulletText("Dynamic list with Delete key support.");
                Text("Selection size: %d/%d", selection.Size, items.Size);

                // Initialize default list with 50 items + button to add/remove items.
                if (DemoWidgets2Locals.DeletionItemsNextId == 0)
                    for (ImGuiID n = 0; n < 50; n++)
                        items.push_back(DemoWidgets2Locals.DeletionItemsNextId++);
                if (SmallButton("Add 20 items")) { for (int n = 0; n < 20; n++) { items.push_back(DemoWidgets2Locals.DeletionItemsNextId++); } }
                SameLine();
                if (SmallButton("Remove 20 items")) { for (int n = ImMath.ImMin(20, items.Size); n > 0; n--) { selection.SetItemSelected(items.back(), false); items.pop_back(); } }

                // (1) Extra to support deletion: Submit scrolling range to avoid glitches on deletion
                float items_height = GetTextLineHeightWithSpacing();
                SetNextWindowContentSize(new ImVec2(0.0f, items.Size * items_height));

                if (BeginChild("##Basket", new ImVec2(-float.Epsilon /* -FLT_MIN */, GetFontSize() * 20), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY))
                {
                    ImGuiMultiSelectFlags flags = ImGuiMultiSelectFlags_ClearOnEscape | ImGuiMultiSelectFlags_BoxSelect1d;
                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(flags, selection.Size, items.Size);
                    selection.ApplyRequests(ms_io);

                    bool want_delete = Shortcut(ImGuiKey_Delete, ImGuiInputFlags_Repeat) && (selection.Size > 0);
                    int item_curr_idx_to_focus = want_delete ? selection.ApplyDeletionPreLoop(ms_io, items.Size) : -1;

                    for (int n = 0; n < items.Size; n++)
                    {
                        ImGuiID item_id = items[n];
                        string label = ImFmt.Sprintf("Object %05u: %s", item_id, DemoWidgets2Locals.ExampleNames[(int)(item_id % (ImGuiID)DemoWidgets2Locals.ExampleNames.Length)]);

                        bool item_is_selected = selection.Contains(item_id);
                        SetNextItemSelectionUserData(n);
                        Selectable(label, item_is_selected);
                        if (item_curr_idx_to_focus == n)
                            SetKeyboardFocusHere(-1);
                    }

                    // Apply multi-select requests
                    ms_io = EndMultiSelect();
                    selection.ApplyRequests(ms_io);
                    if (want_delete)
                        selection.ApplyDeletionPostLoop(ms_io, items, item_curr_idx_to_focus);
                }
                EndChild();
                TreePop();
            }

            // Implement a Dual List Box (#6648)
            if (TreeNode("Multi-Select (dual list box)"))
            {
                // Init default state
                ExampleDualListBox dlb = DemoWidgets2Locals.Dlb;
                if (dlb.Items[0].Size == 0 && dlb.Items[1].Size == 0)
                    for (int item_id = 0; item_id < DemoWidgets2Locals.ExampleNames.Length; item_id++)
                        dlb.Items[0].push_back((ImGuiID)item_id);

                // Show
                dlb.Show();

                TreePop();
            }

            // Demonstrate using the clipper with BeginMultiSelect()/EndMultiSelect()
            if (TreeNode("Multi-Select (in a table)"))
            {
                ImGuiSelectionBasicStorage selection = DemoWidgets2Locals.TableSelection;

                const int ITEMS_COUNT = 10000;
                Text("Selection: %d/%d", selection.Size, ITEMS_COUNT);
                if (BeginTable("##Basket", 2, ImGuiTableFlags_ScrollY | ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersOuter, new ImVec2(0.0f, GetFontSize() * 20)))
                {
                    TableSetupColumn("Object");
                    TableSetupColumn("Action");
                    TableSetupScrollFreeze(0, 1);
                    TableHeadersRow();

                    ImGuiMultiSelectFlags flags = ImGuiMultiSelectFlags_ClearOnEscape | ImGuiMultiSelectFlags_BoxSelect1d;
                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(flags, selection.Size, ITEMS_COUNT);
                    selection.ApplyRequests(ms_io);

                    ImGuiListClipper clipper = new ImGuiListClipper();
                    clipper.Begin(ITEMS_COUNT);
                    if (ms_io.RangeSrcItem != -1)
                        clipper.IncludeItemByIndex((int)ms_io.RangeSrcItem); // Ensure RangeSrc item is not clipped.
                    while (clipper.Step())
                    {
                        for (int n = clipper.DisplayStart; n < clipper.DisplayEnd; n++)
                        {
                            TableNextRow();
                            TableNextColumn();
                            PushID(n);
                            string label = ImFmt.Sprintf("Object %05d: %s", n, DemoWidgets2Locals.ExampleNames[n % DemoWidgets2Locals.ExampleNames.Length]);
                            bool item_is_selected = selection.Contains((ImGuiID)n);
                            SetNextItemSelectionUserData(n);
                            Selectable(label, item_is_selected, ImGuiSelectableFlags_SpanAllColumns | ImGuiSelectableFlags_AllowOverlap);
                            TableNextColumn();
                            SmallButton("hello");
                            PopID();
                        }
                    }

                    ms_io = EndMultiSelect();
                    selection.ApplyRequests(ms_io);
                    EndTable();
                }
                TreePop();
            }

            if (TreeNode("Multi-Select (checkboxes)"))
            {
                Text("In a list of checkboxes (not selectable):");
                BulletText("Using _NoAutoSelect + _NoAutoClear flags.");
                BulletText("Shift+Click to check multiple boxes.");
                BulletText("Shift+Keyboard to copy current value to other boxes.");

                // If you have an array of checkboxes, you may want to use NoAutoSelect + NoAutoClear and the ImGuiSelectionExternalStorage helper.
                bool[] items = DemoWidgets2Locals.CheckboxItems;
                ImGuiMultiSelectFlags flags = DemoWidgets2Locals.CheckboxFlags;
                CheckboxFlags("ImGuiMultiSelectFlags_NoAutoSelect", ref flags, ImGuiMultiSelectFlags_NoAutoSelect);
                CheckboxFlags("ImGuiMultiSelectFlags_NoAutoClear", ref flags, ImGuiMultiSelectFlags_NoAutoClear);
                CheckboxFlags("ImGuiMultiSelectFlags_BoxSelect2d", ref flags, ImGuiMultiSelectFlags_BoxSelect2d); // Cannot use ImGuiMultiSelectFlags_BoxSelect1d as checkboxes are varying width.
                DemoWidgets2Locals.CheckboxFlags = flags;

                if (BeginChild("##Basket", new ImVec2(-float.Epsilon /* -FLT_MIN */, GetFontSize() * 20), ImGuiChildFlags_Borders | ImGuiChildFlags_ResizeY))
                {
                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(flags, -1, items.Length);
                    ImGuiSelectionExternalStorage storage_wrapper = new ImGuiSelectionExternalStorage();
                    storage_wrapper.UserData = items;
                    storage_wrapper.AdapterSetItemSelected = (ImGuiSelectionExternalStorage self, int n, bool selected) => { ((bool[])self.UserData)[n] = selected; };
                    storage_wrapper.ApplyRequests(ms_io);
                    for (int n = 0; n < 20; n++)
                    {
                        string label = ImFmt.Sprintf("Item %d", n);
                        SetNextItemSelectionUserData(n);
                        Checkbox(label, ref items[n]);
                    }
                    ms_io = EndMultiSelect();
                    storage_wrapper.ApplyRequests(ms_io);
                }
                EndChild();

                TreePop();
            }

            // Demonstrate individual selection scopes in same window
            if (TreeNode("Multi-Select (multiple scopes)"))
            {
                // Use default select: Pass index to SetNextItemSelectionUserData(), store index in Selection
                const int SCOPES_COUNT = 3;
                const int ITEMS_COUNT = 8; // Per scope
                ImGuiSelectionBasicStorage[] selections_data = DemoWidgets2Locals.ScopesSelectionsData;

                // Use ImGuiMultiSelectFlags_ScopeRect to not affect other selections in same window.
                ImGuiMultiSelectFlags flags = DemoWidgets2Locals.ScopesFlags;
                if (CheckboxFlags("ImGuiMultiSelectFlags_ScopeWindow", ref flags, ImGuiMultiSelectFlags_ScopeWindow) && (flags & ImGuiMultiSelectFlags_ScopeWindow) != 0)
                    flags &= ~ImGuiMultiSelectFlags_ScopeRect;
                if (CheckboxFlags("ImGuiMultiSelectFlags_ScopeRect", ref flags, ImGuiMultiSelectFlags_ScopeRect) && (flags & ImGuiMultiSelectFlags_ScopeRect) != 0)
                    flags &= ~ImGuiMultiSelectFlags_ScopeWindow;
                CheckboxFlags("ImGuiMultiSelectFlags_ClearOnClickVoid", ref flags, ImGuiMultiSelectFlags_ClearOnClickVoid);
                CheckboxFlags("ImGuiMultiSelectFlags_BoxSelect1d", ref flags, ImGuiMultiSelectFlags_BoxSelect1d);
                DemoWidgets2Locals.ScopesFlags = flags;

                for (int selection_scope_n = 0; selection_scope_n < SCOPES_COUNT; selection_scope_n++)
                {
                    PushID(selection_scope_n);
                    ImGuiSelectionBasicStorage selection = selections_data[selection_scope_n];
                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(flags, selection.Size, ITEMS_COUNT);
                    selection.ApplyRequests(ms_io);

                    SeparatorText("Selection scope");
                    Text("Selection size: %d/%d", selection.Size, ITEMS_COUNT);

                    for (int n = 0; n < ITEMS_COUNT; n++)
                    {
                        string label = ImFmt.Sprintf("Object %05d: %s", n, DemoWidgets2Locals.ExampleNames[n % DemoWidgets2Locals.ExampleNames.Length]);
                        bool item_is_selected = selection.Contains((ImGuiID)n);
                        SetNextItemSelectionUserData(n);
                        Selectable(label, item_is_selected);
                    }

                    // Apply multi-select requests
                    ms_io = EndMultiSelect();
                    selection.ApplyRequests(ms_io);
                    PopID();
                }
                TreePop();
            }

            // See ShowExampleAppAssetsBrowser()
            if (TreeNode("Multi-Select (tiled assets browser)"))
            {
                Checkbox("Assets Browser", ref demo_data.ShowAppAssetsBrowser);
                Text("(also access from 'Examples->Assets Browser' in menu)");
                TreePop();
            }

            // Demonstrate supporting multiple-selection in a tree.
            // - We don't use linear indices for selection user data, but our ExampleTreeNode UID directly!
            //   (C++ passes the ExampleTreeNode* pointer casted to ImGuiSelectionUserData; in C# we pass the node's
            //   stable UID and DemoW2_TreeApplySelectionRequests() maps it back to a node.)
            //   This showcases how SetNextItemSelectionUserData() never assume indices!
            // - The difficulty here is to "interpolate" from RangeSrcItem to RangeDstItem in the SetAll/SetRange request.
            //   We want this interpolation to match what the user sees: in visible order, skipping closed nodes.
            //   This is implemented by our TreeGetNextNodeInVisibleOrder() user-space helper.
            // - Important: In a real codebase aiming to implement full-featured selectable tree with custom filtering, you
            //   are more likely to build an array mapping sequential indices to visible tree nodes, since your
            //   filtering/search + clipping process will benefit from it. Having this will make this interpolation much easier.
            // - Consider this a prototype: we are working toward simplifying some of it.
            if (TreeNode("Multi-Select (trees)"))
            {
                HelpMarker(
                    "This is rather advanced and experimental. If you are getting started with multi-select, " +
                    "please don't start by looking at how to use it for a tree!\n\n" +
                    "Future versions will try to simplify and formalize some of this.");

                // C++: 'struct ExampleTreeFuncs' — ported as the DemoW2_Tree* helpers below.
                ImGuiSelectionBasicStorage selection = DemoWidgets2Locals.TreesSelection;
                if (demo_data.DemoTree == null)
                    demo_data.DemoTree = ExampleTree.CreateDemoTree(); // Create tree once
                Text("Selection size: %d", selection.Size);

                if (BeginChild("##Tree", new ImVec2(-float.Epsilon /* -FLT_MIN */, GetFontSize() * 20), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY))
                {
                    ExampleTreeNode tree = demo_data.DemoTree;
                    ImGuiMultiSelectFlags ms_flags = ImGuiMultiSelectFlags_ClearOnEscape | ImGuiMultiSelectFlags_BoxSelect2d;
                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(ms_flags, selection.Size, -1);
                    DemoW2_TreeApplySelectionRequests(ms_io, tree, selection);
                    for (int node_n = 0; node_n < tree.Childs.Size; node_n++)
                        DemoW2_TreeDrawNode(tree.Childs[node_n], selection);
                    ms_io = EndMultiSelect();
                    DemoW2_TreeApplySelectionRequests(ms_io, tree, selection);
                }
                EndChild();

                TreePop();
            }

            // Advanced demonstration of BeginMultiSelect()
            // - Showcase clipping.
            // - Showcase deletion.
            // - Showcase basic drag and drop.
            // - Showcase TreeNode variant (note that tree node don't expand in the demo: supporting expanding tree nodes + clipping a separate thing).
            // - Showcase using inside a table.
            //ImGui::SetNextItemOpen(true, ImGuiCond_Once);
            if (TreeNode("Multi-Select (advanced)"))
            {
                // Options
                // C++: enum WidgetType { WidgetType_Selectable, WidgetType_TreeNode };
                bool use_clipper = DemoWidgets2Locals.UseClipper;
                bool use_deletion = DemoWidgets2Locals.UseDeletion;
                bool use_drag_drop = DemoWidgets2Locals.UseDragDrop;
                bool show_in_table = DemoWidgets2Locals.ShowInTable;
                bool show_color_button = DemoWidgets2Locals.ShowColorButton;
                ImGuiMultiSelectFlags flags = DemoWidgets2Locals.AdvancedFlags;
                int widget_type = DemoWidgets2Locals.WidgetType;

                if (TreeNode("Options"))
                {
                    if (RadioButton("Selectables", widget_type == DemoWidgets2Locals.WidgetType_Selectable)) { widget_type = DemoWidgets2Locals.WidgetType_Selectable; }
                    SameLine();
                    if (RadioButton("Tree nodes", widget_type == DemoWidgets2Locals.WidgetType_TreeNode)) { widget_type = DemoWidgets2Locals.WidgetType_TreeNode; }
                    SameLine();
                    HelpMarker("TreeNode() is technically supported but... using this correctly is more complicated (you need some sort of linear/random access to your tree, which is suited to advanced trees setups already implementing filters and clipper. We will work toward simplifying and demoing this.\n\nFor now the tree demo is actually a little bit meaningless because it is an empty tree with only root nodes.");
                    Checkbox("Enable clipper", ref use_clipper);
                    Checkbox("Enable deletion", ref use_deletion);
                    Checkbox("Enable drag & drop", ref use_drag_drop);
                    Checkbox("Show in a table", ref show_in_table);
                    Checkbox("Show color button", ref show_color_button);
                    CheckboxFlags("ImGuiMultiSelectFlags_SingleSelect", ref flags, ImGuiMultiSelectFlags_SingleSelect);
                    CheckboxFlags("ImGuiMultiSelectFlags_NoSelectAll", ref flags, ImGuiMultiSelectFlags_NoSelectAll);
                    CheckboxFlags("ImGuiMultiSelectFlags_NoRangeSelect", ref flags, ImGuiMultiSelectFlags_NoRangeSelect);
                    CheckboxFlags("ImGuiMultiSelectFlags_NoAutoSelect", ref flags, ImGuiMultiSelectFlags_NoAutoSelect);
                    CheckboxFlags("ImGuiMultiSelectFlags_NoAutoClear", ref flags, ImGuiMultiSelectFlags_NoAutoClear);
                    CheckboxFlags("ImGuiMultiSelectFlags_NoAutoClearOnReselect", ref flags, ImGuiMultiSelectFlags_NoAutoClearOnReselect);
                    CheckboxFlags("ImGuiMultiSelectFlags_NoSelectOnRightClick", ref flags, ImGuiMultiSelectFlags_NoSelectOnRightClick);
                    CheckboxFlags("ImGuiMultiSelectFlags_BoxSelect1d", ref flags, ImGuiMultiSelectFlags_BoxSelect1d);
                    CheckboxFlags("ImGuiMultiSelectFlags_BoxSelect2d", ref flags, ImGuiMultiSelectFlags_BoxSelect2d);
                    CheckboxFlags("ImGuiMultiSelectFlags_BoxSelectNoScroll", ref flags, ImGuiMultiSelectFlags_BoxSelectNoScroll);
                    CheckboxFlags("ImGuiMultiSelectFlags_ClearOnEscape", ref flags, ImGuiMultiSelectFlags_ClearOnEscape);
                    CheckboxFlags("ImGuiMultiSelectFlags_ClearOnClickVoid", ref flags, ImGuiMultiSelectFlags_ClearOnClickVoid);
                    if (CheckboxFlags("ImGuiMultiSelectFlags_ScopeWindow", ref flags, ImGuiMultiSelectFlags_ScopeWindow) && (flags & ImGuiMultiSelectFlags_ScopeWindow) != 0)
                        flags &= ~ImGuiMultiSelectFlags_ScopeRect;
                    if (CheckboxFlags("ImGuiMultiSelectFlags_ScopeRect", ref flags, ImGuiMultiSelectFlags_ScopeRect) && (flags & ImGuiMultiSelectFlags_ScopeRect) != 0)
                        flags &= ~ImGuiMultiSelectFlags_ScopeWindow;
                    if (CheckboxFlags("ImGuiMultiSelectFlags_SelectOnAuto", ref flags, ImGuiMultiSelectFlags_SelectOnAuto))
                        flags &= ~(ImGuiMultiSelectFlags_SelectOnMask_ ^ ImGuiMultiSelectFlags_SelectOnAuto);
                    SameLine(); HelpMarker("Apply selection on mouse down when clicking on unselected item, on mouse up when clicking on selected item. (Default)");
                    if (CheckboxFlags("ImGuiMultiSelectFlags_SelectOnClickAlways", ref flags, ImGuiMultiSelectFlags_SelectOnClickAlways))
                        flags &= ~(ImGuiMultiSelectFlags_SelectOnMask_ ^ ImGuiMultiSelectFlags_SelectOnClickAlways);
                    SameLine(); HelpMarker("Prevents Drag and Drop from being used on multi-selection, but allows e.g. BoxSelect to always reselect even when clicking inside an existing selection. (Excel style behavior)");
                    if (CheckboxFlags("ImGuiMultiSelectFlags_SelectOnClickRelease", ref flags, ImGuiMultiSelectFlags_SelectOnClickRelease))
                        flags &= ~(ImGuiMultiSelectFlags_SelectOnMask_ ^ ImGuiMultiSelectFlags_SelectOnClickRelease);
                    SameLine(); HelpMarker("Allow dragging an unselected item without altering selection.");
                    TreePop();
                }
                DemoWidgets2Locals.UseClipper = use_clipper;
                DemoWidgets2Locals.UseDeletion = use_deletion;
                DemoWidgets2Locals.UseDragDrop = use_drag_drop;
                DemoWidgets2Locals.ShowInTable = show_in_table;
                DemoWidgets2Locals.ShowColorButton = show_color_button;
                DemoWidgets2Locals.AdvancedFlags = flags;
                DemoWidgets2Locals.WidgetType = widget_type;

                // Initialize default list with 1000 items.
                // Use default selection.Adapter: Pass index to SetNextItemSelectionUserData(), store index in Selection
                ImVector<int> items = DemoWidgets2Locals.AdvancedItems;
                if (DemoWidgets2Locals.AdvancedItemsNextId == 0) { for (int n = 0; n < 1000; n++) { items.push_back(DemoWidgets2Locals.AdvancedItemsNextId++); } }
                ExampleSelectionWithDeletion selection = DemoWidgets2Locals.AdvancedSelection;
                bool request_deletion_from_menu = DemoWidgets2Locals.RequestDeletionFromMenu;

                Text("Selection size: %d/%d", selection.Size, items.Size);

                float items_height = (widget_type == DemoWidgets2Locals.WidgetType_TreeNode) ? GetTextLineHeight() : GetTextLineHeightWithSpacing();
                SetNextWindowContentSize(new ImVec2(0.0f, items.Size * items_height));
                if (BeginChild("##Basket", new ImVec2(-float.Epsilon /* -FLT_MIN */, GetFontSize() * 20), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY))
                {
                    ImVec2 color_button_sz = new ImVec2(GetFontSize(), GetFontSize());
                    if (widget_type == DemoWidgets2Locals.WidgetType_TreeNode)
                        PushStyleVarY(ImGuiStyleVar_ItemSpacing, 0.0f);

                    ImGuiMultiSelectIO ms_io = BeginMultiSelect(flags, selection.Size, items.Size);
                    selection.ApplyRequests(ms_io);

                    bool want_delete = (Shortcut(ImGuiKey_Delete, ImGuiInputFlags_Repeat) && (selection.Size > 0)) || request_deletion_from_menu;
                    int item_curr_idx_to_focus = want_delete ? selection.ApplyDeletionPreLoop(ms_io, items.Size) : -1;
                    DemoWidgets2Locals.RequestDeletionFromMenu = false; // C++: request_deletion_from_menu = false;

                    if (show_in_table)
                    {
                        if (widget_type == DemoWidgets2Locals.WidgetType_TreeNode)
                            PushStyleVar(ImGuiStyleVar_CellPadding, new ImVec2(0.0f, 0.0f));
                        BeginTable("##Split", 2, ImGuiTableFlags_Resizable | ImGuiTableFlags_NoSavedSettings | ImGuiTableFlags_NoPadOuterX);
                        TableSetupColumn("", ImGuiTableColumnFlags_WidthStretch, 0.70f);
                        TableSetupColumn("", ImGuiTableColumnFlags_WidthStretch, 0.30f);
                        //ImGui::PushStyleVarY(ImGuiStyleVar_ItemSpacing, 0.0f);
                    }

                    ImGuiListClipper clipper = new ImGuiListClipper();
                    if (use_clipper)
                    {
                        clipper.Begin(items.Size);
                        if (item_curr_idx_to_focus != -1)
                            clipper.IncludeItemByIndex(item_curr_idx_to_focus); // Ensure focused item is not clipped.
                        if (ms_io.RangeSrcItem != -1)
                            clipper.IncludeItemByIndex((int)ms_io.RangeSrcItem); // Ensure RangeSrc item is not clipped.
                    }

                    while (!use_clipper || clipper.Step())
                    {
                        int item_begin = use_clipper ? clipper.DisplayStart : 0;
                        int item_end = use_clipper ? clipper.DisplayEnd : items.Size;
                        for (int n = item_begin; n < item_end; n++)
                        {
                            if (show_in_table)
                                TableNextColumn();

                            int item_id = items[n];
                            string item_category = DemoWidgets2Locals.ExampleNames[item_id % DemoWidgets2Locals.ExampleNames.Length];
                            string label = ImFmt.Sprintf("Object %05d: %s", item_id, item_category);

                            // IMPORTANT: for deletion refocus to work we need object ID to be stable,
                            // aka not depend on their index in the list. Here we use our persistent item_id
                            // instead of index to build a unique ID that will persist.
                            // (If we used PushID(index) instead, focus wouldn't be restored correctly after deletion).
                            PushID(item_id);

                            // Emit a color button, to test that Shift+LeftArrow landing on an item that is not part
                            // of the selection scope doesn't erroneously alter our selection.
                            if (show_color_button)
                            {
                                ImU32 dummy_col = (ImU32)((uint)n * 0xC250B74B) | ImGuiCore.IM_COL32_A_MASK;
                                ColorButton("##", ColorConvertU32ToFloat4(dummy_col), ImGuiColorEditFlags_NoTooltip, color_button_sz); // C++: ImColor(dummy_col)
                                SameLine();
                            }

                            // Submit item
                            bool item_is_selected = selection.Contains((ImGuiID)n);
                            bool item_is_open = false;
                            SetNextItemSelectionUserData(n);
                            if (widget_type == DemoWidgets2Locals.WidgetType_Selectable)
                            {
                                Selectable(label, item_is_selected, ImGuiSelectableFlags_None);
                            }
                            else if (widget_type == DemoWidgets2Locals.WidgetType_TreeNode)
                            {
                                ImGuiTreeNodeFlags tree_node_flags = ImGuiTreeNodeFlags_SpanAvailWidth | ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_OpenOnDoubleClick;
                                if (item_is_selected)
                                    tree_node_flags |= ImGuiTreeNodeFlags_Selected;
                                item_is_open = TreeNodeEx(label, tree_node_flags);
                            }

                            // Focus (for after deletion)
                            if (item_curr_idx_to_focus == n)
                                SetKeyboardFocusHere(-1);

                            // Drag and Drop
                            if (use_drag_drop && BeginDragDropSource())
                            {
                                // Create payload with full selection OR single unselected item.
                                // (the later is only possible when using ImGuiMultiSelectFlags_SelectOnClickRelease)
                                if (GetDragDropPayload() == null)
                                {
                                    ImVector<int> payload_items = new ImVector<int>();
                                    if (!item_is_selected)
                                    {
                                        payload_items.push_back(item_id);
                                    }
                                    else
                                    {
                                        int it = -1; // C++: void* it = NULL;
                                        while (selection.GetNextSelectedItem(ref it, out ImGuiID id))
                                            payload_items.push_back((int)id);
                                    }
                                    byte[] payload_bytes = new byte[payload_items.Size * sizeof(int)]; // C++: payload_items.size_in_bytes()
                                    Buffer.BlockCopy(payload_items.Data, 0, payload_bytes, 0, payload_bytes.Length);
                                    SetDragDropPayload("MULTISELECT_DEMO_ITEMS", payload_bytes, (ulong)payload_bytes.Length);
                                }

                                // Display payload content in tooltip
                                ImGuiPayload payload = GetDragDropPayload();
                                int payload_count = payload.DataSize / sizeof(int);
                                int[] payload_items_arr = new int[payload_count];
                                Buffer.BlockCopy(payload.Data, 0, payload_items_arr, 0, payload_count * sizeof(int));
                                if (payload_count == 1)
                                    Text("Object %05d: %s", payload_items_arr[0], DemoWidgets2Locals.ExampleNames[payload_items_arr[0] % DemoWidgets2Locals.ExampleNames.Length]);
                                else
                                    Text("Dragging %d objects", payload_count);

                                EndDragDropSource();
                            }

                            if (widget_type == DemoWidgets2Locals.WidgetType_TreeNode && item_is_open)
                                TreePop();

                            // Right-click: context menu
                            if (BeginPopupContextItem())
                            {
                                BeginDisabled(!use_deletion || selection.Size == 0);
                                label = ImFmt.Sprintf("Delete %d item(s)###DeleteSelected", selection.Size);
                                if (Selectable(label))
                                    DemoWidgets2Locals.RequestDeletionFromMenu = true;
                                EndDisabled();
                                Selectable("Close");
                                EndPopup();
                            }

                            // Demo content within a table
                            if (show_in_table)
                            {
                                TableNextColumn();
                                SetNextItemWidth(-float.Epsilon /* -FLT_MIN */);
                                PushStyleVar(ImGuiStyleVar_FramePadding, new ImVec2(0, 0));
                                string category_buf = item_category; // C++ passes the read-only item_category buffer to InputText()
                                InputText("##NoLabel", ref category_buf, item_category.Length, ImGuiInputTextFlags_ReadOnly);
                                PopStyleVar();
                            }

                            PopID();
                        }
                        if (!use_clipper)
                            break;
                    }

                    if (show_in_table)
                    {
                        EndTable();
                        if (widget_type == DemoWidgets2Locals.WidgetType_TreeNode)
                            PopStyleVar();
                    }

                    // Apply multi-select requests
                    ms_io = EndMultiSelect();
                    selection.ApplyRequests(ms_io);
                    if (want_delete)
                        selection.ApplyDeletionPostLoop(ms_io, items, item_curr_idx_to_focus);

                    if (widget_type == DemoWidgets2Locals.WidgetType_TreeNode)
                        PopStyleVar();
                }
                EndChild();
                TreePop();
            }
            TreePop();
        }
    }

    // C++: ExampleTreeFuncs::DrawNode(ExampleTreeNode* node, ImGuiSelectionBasicStorage* selection)
    private static void DemoW2_TreeDrawNode(ExampleTreeNode node, ImGuiSelectionBasicStorage selection)
    {
        ImGuiTreeNodeFlags tree_node_flags = ImGuiTreeNodeFlags_SpanAvailWidth | ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_OpenOnDoubleClick;
        tree_node_flags |= ImGuiTreeNodeFlags_NavLeftJumpsToParent; // Enable pressing left to jump to parent
        if (node.Childs.Size == 0)
            tree_node_flags |= ImGuiTreeNodeFlags_Bullet | ImGuiTreeNodeFlags_Leaf;
        if (selection.Contains((ImGuiID)node.UID))
            tree_node_flags |= ImGuiTreeNodeFlags_Selected;

        // Using SetNextItemStorageID() to specify storage id, so we can easily peek into
        // the storage holding open/close stage, using our TreeNodeGetOpen/TreeNodeSetOpen() functions.
        // (C# adaptation: C++ casts the node pointer into ImGuiSelectionUserData; we pass the node's stable UID
        // and DemoW2_TreeApplySelectionRequests() maps it back to a node.)
        SetNextItemSelectionUserData((ImGuiSelectionUserData)node.UID);
        SetNextItemStorageID((ImGuiID)node.UID);
        if (TreeNodeEx(node.Name, tree_node_flags))
        {
            for (int child_n = 0; child_n < node.Childs.Size; child_n++)
                DemoW2_TreeDrawNode(node.Childs[child_n], selection);
            TreePop();
        }
        else if (IsItemToggledOpen())
        {
            DemoW2_TreeCloseAndUnselectChildNodes(node, selection);
        }
    }

    // When closing a node: 1) close and unselect all child nodes, 2) select parent if any child was selected.
    // FIXME: This is currently handled by user logic but I'm hoping to eventually provide tree node
    // features to do this automatically, e.g. a ImGuiTreeNodeFlags_AutoCloseChildNodes etc.
    // C++: ExampleTreeFuncs::TreeCloseAndUnselectChildNodes(ExampleTreeNode* node, ImGuiSelectionBasicStorage* selection, int depth = 0)
    private static int DemoW2_TreeCloseAndUnselectChildNodes(ExampleTreeNode node, ImGuiSelectionBasicStorage selection, int depth = 0)
    {
        // Recursive close (the test for depth == 0 is because we call this on a node that was just closed!)
        int unselected_count = selection.Contains((ImGuiID)node.UID) ? 1 : 0;
        if (depth == 0 || TreeNodeGetOpen((ImGuiID)node.UID))
        {
            for (int child_n = 0; child_n < node.Childs.Size; child_n++)
                unselected_count += DemoW2_TreeCloseAndUnselectChildNodes(node.Childs[child_n], selection, depth + 1);
            TreeNodeSetOpen((ImGuiID)node.UID, false);
        }

        // Select root node if any of its child was selected, otherwise unselect
        selection.SetItemSelected((ImGuiID)node.UID, (depth == 0 && unselected_count > 0));
        return unselected_count;
    }

    // Apply multi-selection requests
    // C++: ExampleTreeFuncs::ApplySelectionRequests(ImGuiMultiSelectIO* ms_io, ExampleTreeNode* tree, ImGuiSelectionBasicStorage* selection)
    private static void DemoW2_TreeApplySelectionRequests(ImGuiMultiSelectIO ms_io, ExampleTreeNode tree, ImGuiSelectionBasicStorage selection)
    {
        for (int req_n = 0; req_n < ms_io.Requests.Size; req_n++)
        {
            ImGuiSelectionRequest req = ms_io.Requests[req_n];
            if (req.Type == ImGuiSelectionRequestType_SetAll)
            {
                if (req.Selected)
                    DemoW2_TreeSetAllInOpenNodes(tree, selection, req.Selected);
                else
                    selection.Clear();
            }
            else if (req.Type == ImGuiSelectionRequestType_SetRange)
            {
                // C++ casts the user data back to ExampleTreeNode*; in C# the user data is the node's stable UID
                // (see DemoW2_TreeDrawNode), so we resolve both bounds back to nodes.
                ExampleTreeNode first_node = DemoW2_TreeFindNodeByUid(tree, (int)req.RangeFirstItem);
                ExampleTreeNode last_node = DemoW2_TreeFindNodeByUid(tree, (int)req.RangeLastItem);
                for (ExampleTreeNode node = first_node; node != null; node = DemoW2_TreeGetNextNodeInVisibleOrder(node, last_node))
                    selection.SetItemSelected((ImGuiID)node.UID, req.Selected);
            }
        }
    }

    // C++: ExampleTreeFuncs::TreeSetAllInOpenNodes(ExampleTreeNode* node, ImGuiSelectionBasicStorage* selection, bool selected)
    private static void DemoW2_TreeSetAllInOpenNodes(ExampleTreeNode node, ImGuiSelectionBasicStorage selection, bool selected)
    {
        if (node.Parent != null) // Root node isn't visible nor selectable in our scheme
            selection.SetItemSelected((ImGuiID)node.UID, selected);
        if (node.Parent == null || TreeNodeGetOpen((ImGuiID)node.UID))
            for (int child_n = 0; child_n < node.Childs.Size; child_n++)
                DemoW2_TreeSetAllInOpenNodes(node.Childs[child_n], selection, selected);
    }

    // Interpolate in *user-visible order* AND only *over opened nodes*.
    // If you have a sequential mapping tables (e.g. generated after a filter/search pass) this would be simpler.
    // Here the tricks are that:
    // - we store/maintain ExampleTreeNode::IndexInParent which allows implementing a linear iterator easily, without searches, without recursion.
    //   this could be replaced by a search in parent, aka 'int index_in_parent = curr_node->Parent->Childs.find_index(curr_node)'
    //   which would only be called when crossing from child to a parent, aka not too much.
    // - we call SetNextItemStorageID() before our TreeNode() calls with an ID which doesn't relate to UI stack,
    //   making it easier to call TreeNodeGetOpen()/TreeNodeSetOpen() from any location.
    // C++: ExampleTreeFuncs::TreeGetNextNodeInVisibleOrder(ExampleTreeNode* curr_node, ExampleTreeNode* last_node)
    private static ExampleTreeNode DemoW2_TreeGetNextNodeInVisibleOrder(ExampleTreeNode curr_node, ExampleTreeNode last_node)
    {
        // Reached last node
        if (curr_node == last_node)
            return null;

        // Recurse into childs. Query storage to tell if the node is open.
        if (curr_node.Childs.Size > 0 && TreeNodeGetOpen((ImGuiID)curr_node.UID))
            return curr_node.Childs[0];

        // Next sibling, then into our own parent
        while (curr_node.Parent != null)
        {
            if (curr_node.IndexInParent + 1 < curr_node.Parent.Childs.Size)
                return curr_node.Parent.Childs[curr_node.IndexInParent + 1];
            curr_node = curr_node.Parent;
        }
        return null;
    }

    // C#-only helper (replaces C++ pointer round-trip through ImGuiSelectionUserData):
    // resolve an ExampleTreeNode from its stable UID, used by DemoW2_TreeApplySelectionRequests().
    private static ExampleTreeNode DemoW2_TreeFindNodeByUid(ExampleTreeNode node, int uid)
    {
        if (node == null)
            return null;
        if (node.UID == uid)
            return node;
        for (int child_n = 0; child_n < node.Childs.Size; child_n++)
        {
            ExampleTreeNode found = DemoW2_TreeFindNodeByUid(node.Childs[child_n], uid);
            if (found != null)
                return found;
        }
        return null;
    }

    // C++: static void EditTabBarFittingPolicyFlags(ImGuiTabBarFlags* p_flags)   [imgui_demo.cpp 3582-3592]
    private static void EditTabBarFittingPolicyFlags(ref ImGuiTabBarFlags p_flags)
    {
        if ((p_flags & ImGuiTabBarFlags_FittingPolicyMask_) == 0)
            p_flags |= ImGuiTabBarFlags_FittingPolicyDefault_;
        if (CheckboxFlags("ImGuiTabBarFlags_FittingPolicyMixed", ref p_flags, ImGuiTabBarFlags_FittingPolicyMixed))
            p_flags &= ~(ImGuiTabBarFlags_FittingPolicyMask_ ^ ImGuiTabBarFlags_FittingPolicyMixed);
        if (CheckboxFlags("ImGuiTabBarFlags_FittingPolicyShrink", ref p_flags, ImGuiTabBarFlags_FittingPolicyShrink))
            p_flags &= ~(ImGuiTabBarFlags_FittingPolicyMask_ ^ ImGuiTabBarFlags_FittingPolicyShrink);
        if (CheckboxFlags("ImGuiTabBarFlags_FittingPolicyScroll", ref p_flags, ImGuiTabBarFlags_FittingPolicyScroll))
            p_flags &= ~(ImGuiTabBarFlags_FittingPolicyMask_ ^ ImGuiTabBarFlags_FittingPolicyScroll);
    }

    // C++: static void DemoWindowWidgetsTabs()   [imgui_demo.cpp 3594-3748]
    public static void DemoWindowWidgetsTabs()
    {
        if (TreeNode("Tabs"))
        {
            if (TreeNode("Basic"))
            {
                ImGuiTabBarFlags tab_bar_flags = ImGuiTabBarFlags_None;
                if (BeginTabBar("MyTabBar", tab_bar_flags))
                {
                    if (BeginTabItem("Avocado"))
                    {
                        Text("This is the Avocado tab!\nblah blah blah blah blah");
                        EndTabItem();
                    }
                    if (BeginTabItem("Broccoli"))
                    {
                        Text("This is the Broccoli tab!\nblah blah blah blah blah");
                        EndTabItem();
                    }
                    if (BeginTabItem("Cucumber"))
                    {
                        Text("This is the Cucumber tab!\nblah blah blah blah blah");
                        EndTabItem();
                    }
                    EndTabBar();
                }
                Separator();
                TreePop();
            }

            if (TreeNode("Advanced & Close Button"))
            {
                // Expose a couple of the available flags. In most cases you may just call BeginTabBar() with no flags (0).
                ImGuiTabBarFlags tab_bar_flags = DemoWidgets2Locals.AdvancedTabBarFlags;
                CheckboxFlags("ImGuiTabBarFlags_Reorderable", ref tab_bar_flags, ImGuiTabBarFlags_Reorderable);
                CheckboxFlags("ImGuiTabBarFlags_AutoSelectNewTabs", ref tab_bar_flags, ImGuiTabBarFlags_AutoSelectNewTabs);
                CheckboxFlags("ImGuiTabBarFlags_TabListPopupButton", ref tab_bar_flags, ImGuiTabBarFlags_TabListPopupButton);
                CheckboxFlags("ImGuiTabBarFlags_NoCloseWithMiddleMouseButton", ref tab_bar_flags, ImGuiTabBarFlags_NoCloseWithMiddleMouseButton);
                CheckboxFlags("ImGuiTabBarFlags_DrawSelectedOverline", ref tab_bar_flags, ImGuiTabBarFlags_DrawSelectedOverline);
                EditTabBarFittingPolicyFlags(ref tab_bar_flags);
                DemoWidgets2Locals.AdvancedTabBarFlags = tab_bar_flags;

                // Tab Bar
                AlignTextToFramePadding();
                Text("Opened:");
                string[] names = { "Artichoke", "Beetroot", "Celery", "Daikon" }; // C++: const char* names[4]
                bool[] opened = DemoWidgets2Locals.AdvancedOpened;
                for (int n = 0; n < opened.Length; n++)
                {
                    SameLine();
                    Checkbox(names[n], ref opened[n]);
                }

                // Passing a bool* to BeginTabItem() is similar to passing one to Begin():
                // the underlying bool will be set to false when the tab is closed.
                if (BeginTabBar("MyTabBar", tab_bar_flags))
                {
                    for (int n = 0; n < opened.Length; n++)
                        if (opened[n] && BeginTabItem(names[n], ref opened[n], ImGuiTabItemFlags_None))
                        {
                            Text("This is the %s tab!", names[n]);
                            if ((n & 1) != 0)
                                Text("I am an odd tab.");
                            EndTabItem();
                        }
                    EndTabBar();
                }
                Separator();
                TreePop();
            }

            if (TreeNode("TabItemButton & Leading/Trailing flags"))
            {
                ImVector<int> active_tabs = DemoWidgets2Locals.ActiveTabs;
                if (DemoWidgets2Locals.NextTabId == 0) // Initialize with some default tabs
                    for (int i = 0; i < 3; i++)
                        active_tabs.push_back(DemoWidgets2Locals.NextTabId++);

                // TabItemButton() and Leading/Trailing flags are distinct features which we will demo together.
                // (It is possible to submit regular tabs with Leading/Trailing flags, or TabItemButton tabs without Leading/Trailing flags...
                // but they tend to make more sense together)
                bool show_leading_button = DemoWidgets2Locals.ShowLeadingButton;
                bool show_trailing_button = DemoWidgets2Locals.ShowTrailingButton;
                Checkbox("Show Leading TabItemButton()", ref show_leading_button);
                Checkbox("Show Trailing TabItemButton()", ref show_trailing_button);
                DemoWidgets2Locals.ShowLeadingButton = show_leading_button;
                DemoWidgets2Locals.ShowTrailingButton = show_trailing_button;

                // Expose some other flags which are useful to showcase how they interact with Leading/Trailing tabs
                ImGuiTabBarFlags tab_bar_flags = DemoWidgets2Locals.LeadingTrailingTabBarFlags;
                EditTabBarFittingPolicyFlags(ref tab_bar_flags);
                DemoWidgets2Locals.LeadingTrailingTabBarFlags = tab_bar_flags;

                if (BeginTabBar("MyTabBar", tab_bar_flags))
                {
                    // Demo a Leading TabItemButton(): click the "?" button to open a menu
                    if (show_leading_button)
                        if (TabItemButton("?", ImGuiTabItemFlags_Leading | ImGuiTabItemFlags_NoTooltip))
                            OpenPopup("MyHelpMenu");
                    if (BeginPopup("MyHelpMenu"))
                    {
                        Selectable("Hello!");
                        EndPopup();
                    }

                    // Demo Trailing Tabs: click the "+" button to add a new tab.
                    // (In your app you may want to use a font icon instead of the "+")
                    // We submit it before the regular tabs, but thanks to the ImGuiTabItemFlags_Trailing flag it will always appear at the end.
                    if (show_trailing_button)
                        if (TabItemButton("+", ImGuiTabItemFlags_Trailing | ImGuiTabItemFlags_NoTooltip))
                            active_tabs.push_back(DemoWidgets2Locals.NextTabId++); // Add new tab

                    // Submit our regular tabs
                    for (int n = 0; n < active_tabs.Size; )
                    {
                        bool open = true;
                        string name = ImFmt.Sprintf("%04d", active_tabs[n]); // C++: snprintf(name, IM_COUNTOF(name), "%04d", active_tabs[n]);
                        if (BeginTabItem(name, ref open, ImGuiTabItemFlags_None))
                        {
                            Text("This is the %s tab!", name);
                            EndTabItem();
                        }

                        if (!open)
                            active_tabs.erase(n); // C++: active_tabs.erase(active_tabs.Data + n);
                        else
                            n++;
                    }

                    EndTabBar();
                }
                Separator();
                TreePop();
            }
            TreePop();
        }
    }
}
