// Port of imgui_demo.cpp [SECTION] DemoWindowTables() (lines 5884-7785) + [SECTION] DemoWindowColumns() (lines 7789-7992).
// Also ports the helpers those functions depend on, which live just before line 5884 in C++:
// MyItem / MyItemColumnID (5720-5789), PushStyleCompact/PopStyleCompact (5793-5803), EditTableSizingFlags (5806-5845),
// EditTableColumnsFlags (5847-5870), ShowTableColumnsStatusFlags (5872-5878) — the latter get a DemoT_ prefix.
// NOTE: ImGuiDemoStubs.cs still carries a `public static void DemoWindowTables()` stub that must be removed
// alongside this file being added (same partial class => duplicate definition otherwise).
using System;
using System.Text;
using static ImGui.ImGui; // for unqualified ImGui.* calls inside namespace-level Example classes

namespace ImGui;

// We are passing our own identifier to TableSetupColumn() to facilitate identifying columns in the sorting code.
// This identifier will be passed down into ImGuiTableSortSpec::ColumnUserID.
// But it is possible to omit the user id parameter of TableSetupColumn() and just use the column index instead! (ImGuiTableSortSpec::ColumnIndex)
// If you don't use sorting, you will generally never care about giving column an ID!
// C++: enum MyItemColumnID (imgui_demo.cpp 5726-5733)
public static class MyItemColumnID
{
    public const int MyItemColumnID_ID = 0;
    public const int MyItemColumnID_Name = 1;
    public const int MyItemColumnID_Action = 2;
    public const int MyItemColumnID_Quantity = 3;
    public const int MyItemColumnID_Description = 4;
}

// C++: struct MyItem (imgui_demo.cpp 5735-5788) — demo data for the Tables/Sorting and Tables/Advanced demos.
public class MyItem
{
    public int ID;
    public string Name;
    public int Quantity;
}

// C++ function-local statics of DemoWindowTables() (imgui_demo.cpp 5884-7785).
// C++ statics are zero-initialized; C# field defaults match (bool[] default false, etc.).
file static class DemoTablesLocals
{
    // Top of function: Options
    public static bool disable_indent = false;

    // "Borders, background"
    public const int BordersBg_CT_Text = 0;
    public const int BordersBg_CT_FillButton = 1;
    public static ImGuiTableFlags borders_bg_flags = ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg;
    public static bool borders_bg_display_headers = false;
    public static int borders_bg_contents_type = BordersBg_CT_Text;

    // "Resizable, stretch"
    public static ImGuiTableFlags stretch_flags = ImGuiTableFlags_SizingStretchSame | ImGuiTableFlags_Resizable | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersV | ImGuiTableFlags_ContextMenuInBody;

    // "Resizable, fixed"
    public static ImGuiTableFlags fixed_flags = ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_Resizable | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersV | ImGuiTableFlags_ContextMenuInBody;

    // "Resizable, mixed"
    public static ImGuiTableFlags mixed_flags = ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable;

    // "Reorderable, hideable, with headers"
    public static ImGuiTableFlags reorderable_flags = ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersV;

    // "Padding"
    public static ImGuiTableFlags padding_flags1 = ImGuiTableFlags_BordersV;
    public static bool padding_show_headers = false;
    public static ImGuiTableFlags padding_flags2 = ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg;
    public static ImVec2 padding_cell_padding = new ImVec2(0.0f, 0.0f);
    public static bool padding_show_widget_frame_bg = true;
    public static string[] padding_text_bufs = new string[3 * 5]; // Mini text storage for 3x5 cells
    public static bool padding_init = true;

    // "Sizing policies"
    public static ImGuiTableFlags sizing_flags1 = ImGuiTableFlags_BordersV | ImGuiTableFlags_BordersOuterH | ImGuiTableFlags_RowBg | ImGuiTableFlags_ContextMenuInBody;
    public static ImGuiTableFlags[] sizing_policy_flags = new ImGuiTableFlags[] { ImGuiTableFlags_SizingFixedFit, ImGuiTableFlags_SizingFixedSame, ImGuiTableFlags_SizingStretchProp, ImGuiTableFlags_SizingStretchSame };
    public const int Sizing_CT_ShowWidth = 0;
    public const int Sizing_CT_ShortText = 1;
    public const int Sizing_CT_LongText = 2;
    public const int Sizing_CT_Button = 3;
    public const int Sizing_CT_FillButton = 4;
    public const int Sizing_CT_InputText = 5;
    public static ImGuiTableFlags sizing_flags = ImGuiTableFlags_ScrollY | ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_Resizable;
    public static int sizing_contents_type = Sizing_CT_ShowWidth;
    public static int sizing_column_count = 3;
    public static string sizing_text_buf = "";

    // "Vertical scrolling, with clipping"
    public static ImGuiTableFlags scrolly_flags = ImGuiTableFlags_ScrollY | ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersV | ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable;

    // "Horizontal scrolling"
    public static ImGuiTableFlags scrollx_flags = ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersV | ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable;
    public static int scrollx_freeze_cols = 1;
    public static int scrollx_freeze_rows = 1;
    public static ImGuiTableFlags scrollx_flags2 = ImGuiTableFlags_SizingStretchSame | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_RowBg | ImGuiTableFlags_ContextMenuInBody;
    public static float scrollx_inner_width = 1000.0f;

    // "Columns flags"
    public static ImGuiTableColumnFlags[] colflags_column_flags = new ImGuiTableColumnFlags[] { ImGuiTableColumnFlags_DefaultSort, ImGuiTableColumnFlags_None, ImGuiTableColumnFlags_DefaultHide };
    public static ImGuiTableColumnFlags[] colflags_column_flags_out = new ImGuiTableColumnFlags[] { 0, 0, 0 }; // Output from TableGetColumnFlags()

    // "Columns widths" (flags1) + "Outer size" (flags) — separate C++ function-scope statics, kept as separate fields.
    public static ImGuiTableFlags colwidths_flags1 = ImGuiTableFlags_Borders | ImGuiTableFlags_NoBordersInBodyUntilResize;
    public static ImGuiTableFlags outersize_flags = ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ContextMenuInBody | ImGuiTableFlags_RowBg | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_NoHostExtendX;

    // "Background color"
    public static ImGuiTableFlags bg_flags = ImGuiTableFlags_RowBg;
    public static int bg_row_bg_type = 1;
    public static int bg_row_bg_target = 1;
    public static int bg_cell_bg_type = 1;

    // "Tree view"
    public static ImGuiTableFlags tree_table_flags = ImGuiTableFlags_BordersV | ImGuiTableFlags_BordersOuterH | ImGuiTableFlags_Resizable | ImGuiTableFlags_RowBg | ImGuiTableFlags_NoBordersInBody;
    public static ImGuiTreeNodeFlags tree_node_flags_base = ImGuiTreeNodeFlags_SpanAllColumns | ImGuiTreeNodeFlags_DefaultOpen | ImGuiTreeNodeFlags_DrawLinesFull;
    public static readonly DemoT_MyTreeNode[] tree_nodes = new DemoT_MyTreeNode[]
    {
        new DemoT_MyTreeNode { Name = "Root with Long Name",           Type = "Folder",      Size = -1,     ChildIdx = 1,  ChildCount = 3 },  // 0
        new DemoT_MyTreeNode { Name = "Music",                         Type = "Folder",      Size = -1,     ChildIdx = 4,  ChildCount = 2 },  // 1
        new DemoT_MyTreeNode { Name = "Textures",                      Type = "Folder",      Size = -1,     ChildIdx = 6,  ChildCount = 3 },  // 2
        new DemoT_MyTreeNode { Name = "desktop.ini",                   Type = "System file", Size = 1024,   ChildIdx = -1, ChildCount = -1 }, // 3
        new DemoT_MyTreeNode { Name = "File1_a.wav",                   Type = "Audio file",  Size = 123000, ChildIdx = -1, ChildCount = -1 }, // 4
        new DemoT_MyTreeNode { Name = "File1_b.wav",                   Type = "Audio file",  Size = 456000, ChildIdx = -1, ChildCount = -1 }, // 5
        new DemoT_MyTreeNode { Name = "Image001.png",                  Type = "Image file",  Size = 203128, ChildIdx = -1, ChildCount = -1 }, // 6
        new DemoT_MyTreeNode { Name = "Copy of Image001.png",          Type = "Image file",  Size = 203256, ChildIdx = -1, ChildCount = -1 }, // 7
        new DemoT_MyTreeNode { Name = "Copy of Image001 (Final2).png", Type = "Image file",  Size = 203512, ChildIdx = -1, ChildCount = -1 }, // 8
    };

    // "Item width"
    public static float itemwidth_dummy_f = 0.0f;

    // "Custom headers"
    public static bool[] customheaders_column_selected = new bool[3]; // Dummy entire-column selection storage

    // "Angled headers"
    public static ImGuiTableFlags angled_table_flags = ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersInnerH | ImGuiTableFlags_Hideable | ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_HighlightHoveredColumn;
    public static ImGuiTableColumnFlags angled_column_flags = ImGuiTableColumnFlags_AngledHeader | ImGuiTableColumnFlags_WidthFixed;
    public static bool[] angled_bools = new bool[14 * 12]; // Dummy storage selection storage
    public static int angled_frozen_cols = 1;
    public static int angled_frozen_rows = 2;

    // "Context menus"
    public static ImGuiTableFlags ctxmenu_flags1 = ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable | ImGuiTableFlags_Borders | ImGuiTableFlags_ContextMenuInBody;

    // "Synced instances"
    public static ImGuiTableFlags synced_flags = ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable | ImGuiTableFlags_Borders | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_NoSavedSettings;

    // "Sorting" + "Advanced" share this name list.
    public static readonly string[] template_items_names = new string[]
    {
        "Banana", "Apple", "Cherry", "Watermelon", "Grapefruit", "Strawberry", "Mango",
        "Kiwi", "Orange", "Pineapple", "Blueberry", "Plum", "Coconut", "Pear", "Apricot"
    };

    // "Sorting"
    public static ImVector<MyItem> sorting_items = new ImVector<MyItem>();
    public static ImGuiTableFlags sorting_flags =
        ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable | ImGuiTableFlags_Sortable | ImGuiTableFlags_SortMulti
        | ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersV | ImGuiTableFlags_NoBordersInBody
        | ImGuiTableFlags_ScrollY;

    // We have a problem which is affecting _only this demo_ and should not affect your code:
    // As we don't rely on std:: or other third-party library to compile dear imgui, we only have reliable access to qsort(),
    // however qsort doesn't allow passing user data to comparing function.
    // As a workaround, we are storing the sort specs in a static/global for the comparing function to access.
    // C++: const ImGuiTableSortSpecs* MyItem::s_current_sort_specs
    public static ImGuiTableSortSpecs s_current_sort_specs;

    // C++: void MyItem::SortWithSortSpecs(ImGuiTableSortSpecs* sort_specs, MyItem* items, int items_count)
    public static void SortWithSortSpecs(ImGuiTableSortSpecs sort_specs, MyItem[] items, int items_count)
    {
        s_current_sort_specs = sort_specs; // Store in variable accessible by the sort function.
        if (items_count > 1)
            Array.Sort(items, 0, items_count, System.Collections.Generic.Comparer<MyItem>.Create(CompareWithSortSpecs));
        s_current_sort_specs = null;
    }

    // Compare function to be used by Array.Sort() (C++: MyItem::CompareWithSortSpecs used by qsort()).
    public static int CompareWithSortSpecs(MyItem a, MyItem b)
    {
        for (int n = 0; n < s_current_sort_specs.SpecsCount; n++)
        {
            // Here we identify columns using the ColumnUserID value that we ourselves passed to TableSetupColumn()
            // We could also choose to identify columns based on their index (sort_spec->ColumnIndex), which is simpler!
            ImGuiTableColumnSortSpecs sort_spec = s_current_sort_specs.Specs[n];
            int delta = 0;
            switch ((int)sort_spec.ColumnUserID)
            {
            case MyItemColumnID.MyItemColumnID_ID: delta = (a.ID - b.ID); break;
            case MyItemColumnID.MyItemColumnID_Name: delta = string.CompareOrdinal(a.Name, b.Name); break;
            case MyItemColumnID.MyItemColumnID_Quantity: delta = (a.Quantity - b.Quantity); break;
            case MyItemColumnID.MyItemColumnID_Description: delta = string.CompareOrdinal(a.Name, b.Name); break;
            default: System.Diagnostics.Debug.Assert(false); break;
            }
            if (delta > 0)
                return (sort_spec.SortDirection == ImGuiSortDirection_Ascending) ? +1 : -1;
            if (delta < 0)
                return (sort_spec.SortDirection == ImGuiSortDirection_Ascending) ? -1 : +1;
        }

        // qsort() is instable so always return a way to differentiate items.
        // Your own compare function may want to avoid fallback on implicit sort specs.
        // e.g. a Name compare if it wasn't already part of the sort specs.
        return a.ID - b.ID;
    }

    // "Advanced"
    public const int Adv_CT_Text = 0;
    public const int Adv_CT_Button = 1;
    public const int Adv_CT_SmallButton = 2;
    public const int Adv_CT_FillButton = 3;
    public const int Adv_CT_Selectable = 4;
    public const int Adv_CT_SelectableSpanRow = 5;
    public static ImGuiTableFlags adv_flags =
        ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable
        | ImGuiTableFlags_Sortable | ImGuiTableFlags_SortMulti
        | ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_NoBordersInBody
        | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY
        | ImGuiTableFlags_SizingFixedFit;
    public static ImGuiTableColumnFlags adv_columns_base_flags = ImGuiTableColumnFlags_None;
    public static int adv_contents_type = Adv_CT_SelectableSpanRow;
    public static int adv_freeze_cols = 1;
    public static int adv_freeze_rows = 1;
    public static int adv_items_count = template_items_names.Length * 2;
    public static ImVec2 adv_outer_size_value = default; // Lazily initialized to ImVec2(0, TEXT_BASE_HEIGHT * 12) on first execution (C++ function-local static initializer).
    public static bool adv_outer_size_value_init = false;
    public static float adv_row_min_height = 0.0f; // Auto
    public static float adv_inner_width_with_scroll = 0.0f; // Auto-extend
    public static bool adv_outer_size_enabled = true;
    public static bool adv_show_headers = true;
    public static bool adv_show_wrapped_text = false;
    public static ImVector<MyItem> adv_items = new ImVector<MyItem>();
    public static ImVector<int> adv_selection = new ImVector<int>();
    public static bool adv_items_need_sort = false;
    public static bool adv_show_debug_details = false;
}

// C++: struct MyTreeNode (imgui_demo.cpp 6993-7036) — local struct used by the "Tree view" demo.
file class DemoT_MyTreeNode
{
    public string Name;
    public string Type;
    public int Size;
    public int ChildIdx;
    public int ChildCount;

    public static void DisplayNode(DemoT_MyTreeNode node, DemoT_MyTreeNode[] all_nodes)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        bool is_folder = (node.ChildCount > 0);

        ImGuiTreeNodeFlags node_flags = DemoTablesLocals.tree_node_flags_base;
        if (node != all_nodes[0])
            node_flags &= ~ImGuiTreeNodeFlags_LabelSpanAllColumns; // Only demonstrate this on the root node.

        if (is_folder)
        {
            bool open = ImGui.TreeNodeEx(node.Name, node_flags);
            if ((node_flags & ImGuiTreeNodeFlags_LabelSpanAllColumns) == 0)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("--");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(node.Type);
            }
            if (open)
            {
                for (int child_n = 0; child_n < node.ChildCount; child_n++)
                    DisplayNode(all_nodes[node.ChildIdx + child_n], all_nodes);
                ImGui.TreePop();
            }
        }
        else
        {
            ImGui.TreeNodeEx(node.Name, node_flags | ImGuiTreeNodeFlags_Leaf | ImGuiTreeNodeFlags_Bullet | ImGuiTreeNodeFlags_NoTreePushOnOpen);
            ImGui.TableNextColumn();
            ImGui.Text("%d", node.Size);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(node.Type);
        }
    }
}

// C++ function-local statics of DemoWindowColumns() (imgui_demo.cpp 7789-7992).
file static class DemoColumnsLocals
{
    public static int selected = -1;
    public static bool h_borders = true;
    public static bool v_borders = true;
    public static int columns_count = 4;
    public static float foo = 1.0f;
    public static float bar = 1.0f;
}

public static partial class ImGui
{
    // Make the UI compact because there are so many fields
    // C++: static void PushStyleCompact() (imgui_demo.cpp 5793-5798)
    private static void DemoT_PushStyleCompact()
    {
        ImGuiStyle style = GetStyle();
        PushStyleVarY(ImGuiStyleVar_FramePadding, (float)(int)(style.FramePadding.y * 0.60f));
        PushStyleVarY(ImGuiStyleVar_ItemSpacing, (float)(int)(style.ItemSpacing.y * 0.60f));
    }

    // C++: static void PopStyleCompact() (imgui_demo.cpp 5800-5803)
    private static void DemoT_PopStyleCompact()
    {
        PopStyleVar(2);
    }

    // Show a combo box with a choice of sizing policies
    // C++: static void EditTableSizingFlags(ImGuiTableFlags* p_flags) (imgui_demo.cpp 5806-5845)
    private class DemoT_EnumDesc
    {
        public ImGuiTableFlags Value;
        public string Name;
        public string Tooltip;
        public DemoT_EnumDesc(ImGuiTableFlags value, string name, string tooltip) { Value = value; Name = name; Tooltip = tooltip; }
    }

    private static readonly DemoT_EnumDesc[] DemoT_SizingPolicies = new DemoT_EnumDesc[]
    {
        new DemoT_EnumDesc(ImGuiTableFlags_None,              "Default",                            "Use default sizing policy:\n- ImGuiTableFlags_SizingFixedFit if ScrollX is on or if host window has ImGuiWindowFlags_AlwaysAutoResize.\n- ImGuiTableFlags_SizingStretchSame otherwise."),
        new DemoT_EnumDesc(ImGuiTableFlags_SizingFixedFit,    "ImGuiTableFlags_SizingFixedFit",     "Columns default to _WidthFixed (if resizable) or _WidthAuto (if not resizable), matching contents width."),
        new DemoT_EnumDesc(ImGuiTableFlags_SizingFixedSame,   "ImGuiTableFlags_SizingFixedSame",    "Columns are all the same width, matching the maximum contents width.\nImplicitly disable ImGuiTableFlags_Resizable and enable ImGuiTableFlags_NoKeepColumnsVisible."),
        new DemoT_EnumDesc(ImGuiTableFlags_SizingStretchProp, "ImGuiTableFlags_SizingStretchProp",  "Columns default to _WidthStretch with weights proportional to their widths."),
        new DemoT_EnumDesc(ImGuiTableFlags_SizingStretchSame, "ImGuiTableFlags_SizingStretchSame",  "Columns default to _WidthStretch with same weights.")
    };

    private static void DemoT_EditTableSizingFlags(ref ImGuiTableFlags p_flags)
    {
        int idx;
        for (idx = 0; idx < DemoT_SizingPolicies.Length; idx++)
            if (DemoT_SizingPolicies[idx].Value == (p_flags & ImGuiTableFlags_SizingMask_))
                break;
        string preview_text = (idx < DemoT_SizingPolicies.Length) ? DemoT_SizingPolicies[idx].Name.Substring(idx > 0 ? "ImGuiTableFlags".Length : 0) : "";
        if (BeginCombo("Sizing Policy", preview_text))
        {
            for (int n = 0; n < DemoT_SizingPolicies.Length; n++)
                if (Selectable(DemoT_SizingPolicies[n].Name, idx == n))
                    p_flags = (p_flags & ~ImGuiTableFlags_SizingMask_) | DemoT_SizingPolicies[n].Value;
            EndCombo();
        }
        SameLine();
        TextDisabled("(?)");
        if (BeginItemTooltip())
        {
            PushTextWrapPos(GetFontSize() * 50.0f);
            for (int m = 0; m < DemoT_SizingPolicies.Length; m++)
            {
                Separator();
                Text("%s:", DemoT_SizingPolicies[m].Name);
                Separator();
                SetCursorPosX(GetCursorPosX() + GetStyle().IndentSpacing * 0.5f);
                TextUnformatted(DemoT_SizingPolicies[m].Tooltip);
            }
            PopTextWrapPos();
            EndTooltip();
        }
    }

    // C++: static void EditTableColumnsFlags(ImGuiTableColumnFlags* p_flags) (imgui_demo.cpp 5847-5870)
    private static void DemoT_EditTableColumnsFlags(ref ImGuiTableColumnFlags p_flags)
    {
        CheckboxFlags("_Disabled", ref p_flags, ImGuiTableColumnFlags_Disabled); SameLine(); HelpMarker("Master disable flag (also hide from context menu)");
        CheckboxFlags("_DefaultHide", ref p_flags, ImGuiTableColumnFlags_DefaultHide);
        CheckboxFlags("_DefaultSort", ref p_flags, ImGuiTableColumnFlags_DefaultSort);
        if (CheckboxFlags("_WidthStretch", ref p_flags, ImGuiTableColumnFlags_WidthStretch))
            p_flags &= ~(ImGuiTableColumnFlags_WidthMask_ ^ ImGuiTableColumnFlags_WidthStretch);
        if (CheckboxFlags("_WidthFixed", ref p_flags, ImGuiTableColumnFlags_WidthFixed))
            p_flags &= ~(ImGuiTableColumnFlags_WidthMask_ ^ ImGuiTableColumnFlags_WidthFixed);
        CheckboxFlags("_NoResize", ref p_flags, ImGuiTableColumnFlags_NoResize);
        CheckboxFlags("_NoReorder", ref p_flags, ImGuiTableColumnFlags_NoReorder);
        CheckboxFlags("_NoHide", ref p_flags, ImGuiTableColumnFlags_NoHide);
        CheckboxFlags("_NoClip", ref p_flags, ImGuiTableColumnFlags_NoClip);
        CheckboxFlags("_NoSort", ref p_flags, ImGuiTableColumnFlags_NoSort);
        CheckboxFlags("_NoSortAscending", ref p_flags, ImGuiTableColumnFlags_NoSortAscending);
        CheckboxFlags("_NoSortDescending", ref p_flags, ImGuiTableColumnFlags_NoSortDescending);
        CheckboxFlags("_NoHeaderLabel", ref p_flags, ImGuiTableColumnFlags_NoHeaderLabel);
        CheckboxFlags("_NoHeaderWidth", ref p_flags, ImGuiTableColumnFlags_NoHeaderWidth);
        CheckboxFlags("_PreferSortAscending", ref p_flags, ImGuiTableColumnFlags_PreferSortAscending);
        CheckboxFlags("_PreferSortDescending", ref p_flags, ImGuiTableColumnFlags_PreferSortDescending);
        CheckboxFlags("_IndentEnable", ref p_flags, ImGuiTableColumnFlags_IndentEnable); SameLine(); HelpMarker("Default for column 0");
        CheckboxFlags("_IndentDisable", ref p_flags, ImGuiTableColumnFlags_IndentDisable); SameLine(); HelpMarker("Default for column >0");
        CheckboxFlags("_AngledHeader", ref p_flags, ImGuiTableColumnFlags_AngledHeader);
    }

    // C++: static void ShowTableColumnsStatusFlags(ImGuiTableColumnFlags flags) (imgui_demo.cpp 5872-5878)
    private static void DemoT_ShowTableColumnsStatusFlags(ImGuiTableColumnFlags flags)
    {
        CheckboxFlags("_IsEnabled", ref flags, ImGuiTableColumnFlags_IsEnabled);
        CheckboxFlags("_IsVisible", ref flags, ImGuiTableColumnFlags_IsVisible);
        CheckboxFlags("_IsSorted", ref flags, ImGuiTableColumnFlags_IsSorted);
        CheckboxFlags("_IsHovered", ref flags, ImGuiTableColumnFlags_IsHovered);
    }

    // C++: static void DemoWindowTables() (imgui_demo.cpp 5884-7785)
    public static void DemoWindowTables()
    {
        //ImGui::SetNextItemOpen(true, ImGuiCond_Once);
        if (!CollapsingHeader("Tables & Columns"))
            return;

        // Using those as a base value to create width/height that are factor of the size of our font
        float TEXT_BASE_WIDTH = CalcTextSize("A").x;
        float TEXT_BASE_HEIGHT = GetTextLineHeightWithSpacing();

        PushID("Tables");

        int open_action = -1;
        if (Button("Expand all"))
            open_action = 1;
        SameLine();
        if (Button("Collapse all"))
            open_action = 0;
        SameLine();

        // Options
        ImGui.Checkbox("Disable tree indentation", ref DemoTablesLocals.disable_indent);
        SameLine();
        HelpMarker("Disable the indenting of tree nodes so demo tables can use the full window width.");
        Separator();
        if (DemoTablesLocals.disable_indent)
            PushStyleVar(ImGuiStyleVar_IndentSpacing, 0.0f);

        // About Styling of tables
        // Most settings are configured on a per-table basis via the flags passed to BeginTable() and TableSetupColumns APIs.
        // There are however a few settings that a shared and part of the ImGuiStyle structure:
        //   style.CellPadding                          // Padding within each cell
        //   style.Colors[ImGuiCol_TableHeaderBg]       // Table header background
        //   style.Colors[ImGuiCol_TableBorderStrong]   // Table outer and header borders
        //   style.Colors[ImGuiCol_TableBorderLight]    // Table inner borders
        //   style.Colors[ImGuiCol_TableRowBg]          // Table row background when ImGuiTableFlags_RowBg is enabled (even rows)
        //   style.Colors[ImGuiCol_TableRowBgAlt]       // Table row background when ImGuiTableFlags_RowBg is enabled (odds rows)

        // Demos
        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Basic"))
        {
            // Here we will showcase three different ways to output a table.
            // They are very simple variations of a same thing!

            // [Method 1] Using TableNextRow() to create a new row, and TableSetColumnIndex() to select the column.
            // In many situations, this is the most flexible and easy to use pattern.
            HelpMarker("Using TableNextRow() + calling TableSetColumnIndex() _before_ each cell, in a loop.");
            if (BeginTable("table1", 3))
            {
                for (int row = 0; row < 4; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("Row %d Column %d", row, column);
                    }
                }
                EndTable();
            }

            // [Method 2] Using TableNextColumn() called multiple times, instead of using a for loop + TableSetColumnIndex().
            // This is generally more convenient when you have code manually submitting the contents of each column.
            HelpMarker("Using TableNextRow() + calling TableNextColumn() _before_ each cell, manually.");
            if (BeginTable("table2", 3))
            {
                for (int row = 0; row < 4; row++)
                {
                    TableNextRow();
                    TableNextColumn();
                    Text("Row %d", row);
                    TableNextColumn();
                    Text("Some contents");
                    TableNextColumn();
                    Text("123.456");
                }
                EndTable();
            }

            // [Method 3] We call TableNextColumn() _before_ each cell. We never call TableNextRow(),
            // as TableNextColumn() will automatically wrap around and create new rows as needed.
            // This is generally more convenient when your cells all contains the same type of data.
            HelpMarker(
                "Only using TableNextColumn(), which tends to be convenient for tables where every cell contains " +
                "the same type of contents.\n This is also more similar to the old NextColumn() function of the " +
                "Columns API, and provided to facilitate the Columns->Tables API transition.");
            if (BeginTable("table3", 3))
            {
                for (int item = 0; item < 14; item++)
                {
                    TableNextColumn();
                    Text("Item %d", item);
                }
                EndTable();
            }

            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Borders, background"))
        {
            // Expose a few Borders related flags interactively
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_RowBg", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_RowBg);
            ImGui.CheckboxFlags("ImGuiTableFlags_Borders", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_Borders);
            SameLine(); HelpMarker("ImGuiTableFlags_Borders\n = ImGuiTableFlags_BordersInnerV\n | ImGuiTableFlags_BordersOuterV\n | ImGuiTableFlags_BordersInnerH\n | ImGuiTableFlags_BordersOuterH");
            Indent();

            ImGui.CheckboxFlags("ImGuiTableFlags_BordersH", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersH);
            Indent();
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuterH", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersOuterH);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersInnerH", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersInnerH);
            Unindent();

            ImGui.CheckboxFlags("ImGuiTableFlags_BordersV", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersV);
            Indent();
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuterV", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersOuterV);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersInnerV", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersInnerV);
            Unindent();

            ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuter", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersOuter);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersInner", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_BordersInner);
            Unindent();

            AlignTextToFramePadding(); Text("Cell contents:");
            SameLine(); ImGui.RadioButton("Text", ref DemoTablesLocals.borders_bg_contents_type, DemoTablesLocals.BordersBg_CT_Text);
            SameLine(); ImGui.RadioButton("FillButton", ref DemoTablesLocals.borders_bg_contents_type, DemoTablesLocals.BordersBg_CT_FillButton);
            ImGui.Checkbox("Display headers", ref DemoTablesLocals.borders_bg_display_headers);
            ImGui.CheckboxFlags("ImGuiTableFlags_NoBordersInBody", ref DemoTablesLocals.borders_bg_flags, ImGuiTableFlags_NoBordersInBody); SameLine(); HelpMarker("Disable vertical borders in columns Body (borders will always appear in Headers)");
            DemoT_PopStyleCompact();

            if (BeginTable("table1", 3, DemoTablesLocals.borders_bg_flags))
            {
                // Display headers so we can inspect their interaction with borders
                // (Headers are not the main purpose of this section of the demo, so we are not elaborating on them now. See other sections for details)
                if (DemoTablesLocals.borders_bg_display_headers)
                {
                    TableSetupColumn("One");
                    TableSetupColumn("Two");
                    TableSetupColumn("Three");
                    TableHeadersRow();
                }

                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        string buf = ImFmt.Sprintf("Hello %d,%d", column, row);
                        if (DemoTablesLocals.borders_bg_contents_type == DemoTablesLocals.BordersBg_CT_Text)
                            TextUnformatted(buf);
                        else if (DemoTablesLocals.borders_bg_contents_type == DemoTablesLocals.BordersBg_CT_FillButton)
                            Button(buf, new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f));
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Resizable, stretch"))
        {
            // By default, if we don't enable ScrollX the sizing policy for each column is "Stretch"
            // All columns maintain a sizing weight, and they will occupy all available width.
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.stretch_flags, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersV", ref DemoTablesLocals.stretch_flags, ImGuiTableFlags_BordersV);
            SameLine(); HelpMarker(
                "Using the _Resizable flag automatically enables the _BordersInnerV flag as well, " +
                "this is why the resize borders are still showing when unchecking this.");
            DemoT_PopStyleCompact();

            if (BeginTable("table1", 3, DemoTablesLocals.stretch_flags))
            {
                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("Hello %d,%d", column, row);
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Resizable, fixed"))
        {
            // Here we use ImGuiTableFlags_SizingFixedFit (even though _ScrollX is not set)
            // So columns will adopt the "Fixed" policy and will maintain a fixed width regardless of the whole available width (unless table is small)
            // If there is not enough available width to fit all columns, they will however be resized down.
            // FIXME-TABLE: Providing a stretch-on-init would make sense especially for tables which don't have saved settings
            HelpMarker(
                "Using _Resizable + _SizingFixedFit flags.\n" +
                "Fixed-width columns generally makes more sense if you want to use horizontal scrolling.\n\n" +
                "Double-click a column border to auto-fit the column to its contents.");
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_NoHostExtendX", ref DemoTablesLocals.fixed_flags, ImGuiTableFlags_NoHostExtendX);
            DemoT_PopStyleCompact();

            if (BeginTable("table1", 3, DemoTablesLocals.fixed_flags))
            {
                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("Hello %d,%d", column, row);
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Resizable, mixed"))
        {
            HelpMarker(
                "Using TableSetupColumn() to alter resizing policy on a per-column basis.\n\n" +
                "When combining Fixed and Stretch columns, generally you only want one, maybe two trailing columns to use _WidthStretch.");

            if (BeginTable("table1", 3, DemoTablesLocals.mixed_flags))
            {
                TableSetupColumn("AAA", ImGuiTableColumnFlags_WidthFixed);
                TableSetupColumn("BBB", ImGuiTableColumnFlags_WidthFixed);
                TableSetupColumn("CCC", ImGuiTableColumnFlags_WidthStretch);
                TableHeadersRow();
                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("%s %d,%d", (column == 2) ? "Stretch" : "Fixed", column, row);
                    }
                }
                EndTable();
            }
            if (BeginTable("table2", 6, DemoTablesLocals.mixed_flags))
            {
                TableSetupColumn("AAA", ImGuiTableColumnFlags_WidthFixed);
                TableSetupColumn("BBB", ImGuiTableColumnFlags_WidthFixed);
                TableSetupColumn("CCC", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_DefaultHide);
                TableSetupColumn("DDD", ImGuiTableColumnFlags_WidthStretch);
                TableSetupColumn("EEE", ImGuiTableColumnFlags_WidthStretch);
                TableSetupColumn("FFF", ImGuiTableColumnFlags_WidthStretch | ImGuiTableColumnFlags_DefaultHide);
                TableHeadersRow();
                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 6; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("%s %d,%d", (column >= 3) ? "Stretch" : "Fixed", column, row);
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Reorderable, hideable, with headers"))
        {
            HelpMarker(
                "Click and drag column headers to reorder columns.\n\n" +
                "Right-click on a header to open a context menu.");
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.reorderable_flags, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("ImGuiTableFlags_Reorderable", ref DemoTablesLocals.reorderable_flags, ImGuiTableFlags_Reorderable);
            ImGui.CheckboxFlags("ImGuiTableFlags_Hideable", ref DemoTablesLocals.reorderable_flags, ImGuiTableFlags_Hideable);
            ImGui.CheckboxFlags("ImGuiTableFlags_NoBordersInBody", ref DemoTablesLocals.reorderable_flags, ImGuiTableFlags_NoBordersInBody);
            ImGui.CheckboxFlags("ImGuiTableFlags_NoBordersInBodyUntilResize", ref DemoTablesLocals.reorderable_flags, ImGuiTableFlags_NoBordersInBodyUntilResize); SameLine(); HelpMarker("Disable vertical borders in columns Body until hovered for resize (borders will always appear in Headers)");
            ImGui.CheckboxFlags("ImGuiTableFlags_HighlightHoveredColumn", ref DemoTablesLocals.reorderable_flags, ImGuiTableFlags_HighlightHoveredColumn);
            DemoT_PopStyleCompact();

            if (BeginTable("table1", 3, DemoTablesLocals.reorderable_flags))
            {
                // Submit columns name with TableSetupColumn() and call TableHeadersRow() to create a row with a header in each column.
                // (Later we will show how TableSetupColumn() has other uses, optional flags, sizing weight etc.)
                TableSetupColumn("One");
                TableSetupColumn("Two");
                TableSetupColumn("Three");
                TableHeadersRow();
                for (int row = 0; row < 6; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("Hello %d,%d", column, row);
                    }
                }
                EndTable();
            }

            // Use outer_size.x == 0.0f instead of default to make the table as tight as possible
            // (only valid when no scrolling and no stretch column)
            if (BeginTable("table2", 3, DemoTablesLocals.reorderable_flags | ImGuiTableFlags_SizingFixedFit, new ImVec2(0.0f, 0.0f)))
            {
                TableSetupColumn("One");
                TableSetupColumn("Two");
                TableSetupColumn("Three");
                TableHeadersRow();
                for (int row = 0; row < 6; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("Fixed %d,%d", column, row);
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Padding"))
        {
            // First example: showcase use of padding flags and effect of BorderOuterV/BorderInnerV on X padding.
            // We don't expose BorderOuterH/BorderInnerH here because they have no effect on X padding.
            HelpMarker(
                "We often want outer padding activated when any using features which makes the edges of a column visible:\n" +
                "e.g.:\n" +
                "- BorderOuterV\n" +
                "- any form of row selection\n" +
                "Because of this, activating BorderOuterV sets the default to PadOuterX. " +
                "Using PadOuterX or NoPadOuterX you can override the default.\n\n" +
                "Actual padding values are using style.CellPadding.\n\n" +
                "In this demo we don't show horizontal borders to emphasize how they don't affect default horizontal padding.");

            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_PadOuterX", ref DemoTablesLocals.padding_flags1, ImGuiTableFlags_PadOuterX);
            SameLine(); HelpMarker("Enable outer-most padding (default if ImGuiTableFlags_BordersOuterV is set)");
            ImGui.CheckboxFlags("ImGuiTableFlags_NoPadOuterX", ref DemoTablesLocals.padding_flags1, ImGuiTableFlags_NoPadOuterX);
            SameLine(); HelpMarker("Disable outer-most padding (default if ImGuiTableFlags_BordersOuterV is not set)");
            ImGui.CheckboxFlags("ImGuiTableFlags_NoPadInnerX", ref DemoTablesLocals.padding_flags1, ImGuiTableFlags_NoPadInnerX);
            SameLine(); HelpMarker("Disable inner padding between columns (double inner padding if BordersOuterV is on, single inner padding if BordersOuterV is off)");
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuterV", ref DemoTablesLocals.padding_flags1, ImGuiTableFlags_BordersOuterV);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersInnerV", ref DemoTablesLocals.padding_flags1, ImGuiTableFlags_BordersInnerV);
            ImGui.Checkbox("show_headers", ref DemoTablesLocals.padding_show_headers);
            DemoT_PopStyleCompact();

            if (BeginTable("table_padding", 3, DemoTablesLocals.padding_flags1))
            {
                if (DemoTablesLocals.padding_show_headers)
                {
                    TableSetupColumn("One");
                    TableSetupColumn("Two");
                    TableSetupColumn("Three");
                    TableHeadersRow();
                }

                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        if (row == 0)
                        {
                            Text("Avail %.2f", GetContentRegionAvail().x);
                        }
                        else
                        {
                            string buf = ImFmt.Sprintf("Hello %d,%d", column, row);
                            Button(buf, new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f));
                        }
                        //if ((TableGetColumnFlags() & ImGuiTableColumnFlags_IsHovered) != 0)
                        //    TableSetBgColor(ImGuiTableBgTarget_CellBg, IM_COL32(0, 100, 0, 255));
                    }
                }
                EndTable();
            }

            // Second example: set style.CellPadding to (0.0) or a custom value.
            // FIXME-TABLE: Vertical border effectively not displayed the same way as horizontal one...
            HelpMarker("Setting style.CellPadding to (0,0) or a custom value.");

            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_Borders", ref DemoTablesLocals.padding_flags2, ImGuiTableFlags_Borders);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersH", ref DemoTablesLocals.padding_flags2, ImGuiTableFlags_BordersH);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersV", ref DemoTablesLocals.padding_flags2, ImGuiTableFlags_BordersV);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersInner", ref DemoTablesLocals.padding_flags2, ImGuiTableFlags_BordersInner);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuter", ref DemoTablesLocals.padding_flags2, ImGuiTableFlags_BordersOuter);
            ImGui.CheckboxFlags("ImGuiTableFlags_RowBg", ref DemoTablesLocals.padding_flags2, ImGuiTableFlags_RowBg);
            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.padding_flags2, ImGuiTableFlags_Resizable);
            ImGui.Checkbox("show_widget_frame_bg", ref DemoTablesLocals.padding_show_widget_frame_bg);
            {
                float[] cell_padding_arr = { DemoTablesLocals.padding_cell_padding.x, DemoTablesLocals.padding_cell_padding.y };
                ImGui.SliderFloat2("CellPadding", cell_padding_arr, 0.0f, 10.0f, "%.0f");
                DemoTablesLocals.padding_cell_padding = new ImVec2(cell_padding_arr[0], cell_padding_arr[1]);
            }
            DemoT_PopStyleCompact();

            PushStyleVar(ImGuiStyleVar_CellPadding, DemoTablesLocals.padding_cell_padding);
            if (BeginTable("table_padding_2", 3, DemoTablesLocals.padding_flags2))
            {
                if (!DemoTablesLocals.padding_show_widget_frame_bg)
                    PushStyleColor(ImGuiCol_FrameBg, 0);
                for (int cell = 0; cell < 3 * 5; cell++)
                {
                    TableNextColumn();
                    if (DemoTablesLocals.padding_init)
                        DemoTablesLocals.padding_text_bufs[cell] = "edit me";
                    SetNextItemWidth(-float.Epsilon /* -FLT_MIN */);
                    PushID(cell);
                    InputText("##cell", ref DemoTablesLocals.padding_text_bufs[cell], 16);
                    PopID();
                }
                if (!DemoTablesLocals.padding_show_widget_frame_bg)
                    PopStyleColor();
                DemoTablesLocals.padding_init = false;
                EndTable();
            }
            PopStyleVar();

            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Sizing policies"))
        {
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.sizing_flags1, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("ImGuiTableFlags_NoHostExtendX", ref DemoTablesLocals.sizing_flags1, ImGuiTableFlags_NoHostExtendX);
            DemoT_PopStyleCompact();

            for (int table_n = 0; table_n < 4; table_n++)
            {
                PushID(table_n);
                SetNextItemWidth(TEXT_BASE_WIDTH * 30);
                DemoT_EditTableSizingFlags(ref DemoTablesLocals.sizing_policy_flags[table_n]);

                // To make it easier to understand the different sizing policy,
                // For each policy: we display one table where the columns have equal contents width,
                // and one where the columns have different contents width.
                if (BeginTable("table1", 3, DemoTablesLocals.sizing_policy_flags[table_n] | DemoTablesLocals.sizing_flags1))
                {
                    for (int row = 0; row < 3; row++)
                    {
                        TableNextRow();
                        TableNextColumn(); Text("Oh dear");
                        TableNextColumn(); Text("Oh dear");
                        TableNextColumn(); Text("Oh dear");
                    }
                    EndTable();
                }
                if (BeginTable("table2", 3, DemoTablesLocals.sizing_policy_flags[table_n] | DemoTablesLocals.sizing_flags1))
                {
                    for (int row = 0; row < 3; row++)
                    {
                        TableNextRow();
                        TableNextColumn(); Text("AAAA");
                        TableNextColumn(); Text("BBBBBBBB");
                        TableNextColumn(); Text("CCCCCCCCCCCC");
                    }
                    EndTable();
                }
                PopID();
            }

            Spacing();
            TextUnformatted("Advanced");
            SameLine();
            HelpMarker(
                "This section allows you to interact and see the effect of various sizing policies " +
                "depending on whether Scroll is enabled and the contents of your columns.");

            DemoT_PushStyleCompact();
            PushID("Advanced");
            PushItemWidth(TEXT_BASE_WIDTH * 30);
            DemoT_EditTableSizingFlags(ref DemoTablesLocals.sizing_flags);
            ImGui.Combo("Contents", ref DemoTablesLocals.sizing_contents_type, "Show width\0Short Text\0Long Text\0Button\0Fill Button\0InputText\0");
            if (DemoTablesLocals.sizing_contents_type == DemoTablesLocals.Sizing_CT_FillButton)
            {
                SameLine();
                HelpMarker(
                    "Be mindful that using right-alignment (e.g. size.x = -FLT_MIN) creates a feedback loop " +
                    "where contents width can feed into auto-column width can feed into contents width.");
            }
            ImGui.DragInt("Columns", ref DemoTablesLocals.sizing_column_count, 0.1f, 1, 64, "%d", ImGuiSliderFlags_AlwaysClamp);
            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.sizing_flags, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("ImGuiTableFlags_PreciseWidths", ref DemoTablesLocals.sizing_flags, ImGuiTableFlags_PreciseWidths);
            SameLine(); HelpMarker("Disable distributing remainder width to stretched columns (width allocation on a 100-wide table with 3 columns: Without this flag: 33,33,34. With this flag: 33,33,33). With larger number of columns, resizing will appear to be less smooth.");
            ImGui.CheckboxFlags("ImGuiTableFlags_ScrollX", ref DemoTablesLocals.sizing_flags, ImGuiTableFlags_ScrollX);
            ImGui.CheckboxFlags("ImGuiTableFlags_ScrollY", ref DemoTablesLocals.sizing_flags, ImGuiTableFlags_ScrollY);
            ImGui.CheckboxFlags("ImGuiTableFlags_NoClip", ref DemoTablesLocals.sizing_flags, ImGuiTableFlags_NoClip);
            PopItemWidth();
            PopID();
            DemoT_PopStyleCompact();

            if (BeginTable("table2", DemoTablesLocals.sizing_column_count, DemoTablesLocals.sizing_flags, new ImVec2(0.0f, TEXT_BASE_HEIGHT * 7)))
            {
                for (int cell = 0; cell < 10 * DemoTablesLocals.sizing_column_count; cell++)
                {
                    TableNextColumn();
                    int column = TableGetColumnIndex();
                    int row = TableGetRowIndex();

                    PushID(cell);
                    string label = ImFmt.Sprintf("Hello %d,%d", column, row);
                    switch (DemoTablesLocals.sizing_contents_type)
                    {
                    case DemoTablesLocals.Sizing_CT_ShortText: TextUnformatted(label); break;
                    case DemoTablesLocals.Sizing_CT_LongText: Text("Some %s text %d,%d\nOver two lines..", column == 0 ? "long" : "longeeer", column, row); break;
                    case DemoTablesLocals.Sizing_CT_ShowWidth: Text("W: %.1f", GetContentRegionAvail().x); break;
                    case DemoTablesLocals.Sizing_CT_Button: Button(label); break;
                    case DemoTablesLocals.Sizing_CT_FillButton: Button(label, new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f)); break;
                    case DemoTablesLocals.Sizing_CT_InputText: SetNextItemWidth(-float.Epsilon /* -FLT_MIN */); InputText("##", ref DemoTablesLocals.sizing_text_buf, 32); break;
                    }
                    PopID();
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Vertical scrolling, with clipping"))
        {
            HelpMarker(
                "Here we activate ScrollY, which will create a child window container to allow hosting scrollable contents.\n\n" +
                "We also demonstrate using ImGuiListClipper to virtualize the submission of many items.");

            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_ScrollY", ref DemoTablesLocals.scrolly_flags, ImGuiTableFlags_ScrollY);
            DemoT_PopStyleCompact();

            // When using ScrollX or ScrollY we need to specify a size for our table container!
            // Otherwise by default the table will fit all available space, like a BeginChild() call.
            ImVec2 outer_size = new ImVec2(0.0f, TEXT_BASE_HEIGHT * 8);
            if (BeginTable("table_scrolly", 3, DemoTablesLocals.scrolly_flags, outer_size))
            {
                TableSetupScrollFreeze(0, 1); // Make top row always visible
                TableSetupColumn("One", ImGuiTableColumnFlags_None);
                TableSetupColumn("Two", ImGuiTableColumnFlags_None);
                TableSetupColumn("Three", ImGuiTableColumnFlags_None);
                TableHeadersRow();

                // Demonstrate using clipper for large vertical lists
                ImGuiListClipper clipper = new ImGuiListClipper();
                clipper.Begin(1000);
                while (clipper.Step())
                {
                    for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                    {
                        TableNextRow();
                        for (int column = 0; column < 3; column++)
                        {
                            TableSetColumnIndex(column);
                            Text("Hello %d,%d", column, row);
                        }
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Horizontal scrolling"))
        {
            HelpMarker(
                "When ScrollX is enabled, the default sizing policy becomes ImGuiTableFlags_SizingFixedFit, " +
                "as automatically stretching columns doesn't make much sense with horizontal scrolling.\n\n" +
                "Also note that as of the current version, you will almost always want to enable ScrollY along with ScrollX, " +
                "because the container window won't automatically extend vertically to fix contents " +
                "(this may be improved in future versions).");

            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.scrollx_flags, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("ImGuiTableFlags_ScrollX", ref DemoTablesLocals.scrollx_flags, ImGuiTableFlags_ScrollX);
            ImGui.CheckboxFlags("ImGuiTableFlags_ScrollY", ref DemoTablesLocals.scrollx_flags, ImGuiTableFlags_ScrollY);
            SetNextItemWidth(GetFrameHeight());
            ImGui.DragInt("freeze_cols", ref DemoTablesLocals.scrollx_freeze_cols, 0.2f, 0, 9, null, ImGuiSliderFlags_NoInput);
            SetNextItemWidth(GetFrameHeight());
            ImGui.DragInt("freeze_rows", ref DemoTablesLocals.scrollx_freeze_rows, 0.2f, 0, 9, null, ImGuiSliderFlags_NoInput);
            DemoT_PopStyleCompact();

            // When using ScrollX or ScrollY we need to specify a size for our table container!
            // Otherwise by default the table will fit all available space, like a BeginChild() call.
            ImVec2 outer_size = new ImVec2(0.0f, TEXT_BASE_HEIGHT * 8);
            if (BeginTable("table_scrollx", 7, DemoTablesLocals.scrollx_flags, outer_size))
            {
                TableSetupScrollFreeze(DemoTablesLocals.scrollx_freeze_cols, DemoTablesLocals.scrollx_freeze_rows);
                TableSetupColumn("Line #", ImGuiTableColumnFlags_NoHide); // Make the first column not hideable to match our use of TableSetupScrollFreeze()
                TableSetupColumn("One");
                TableSetupColumn("Two");
                TableSetupColumn("Three");
                TableSetupColumn("Four");
                TableSetupColumn("Five");
                TableSetupColumn("Six");
                TableHeadersRow();
                for (int row = 0; row < 20; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 7; column++)
                    {
                        // Both TableNextColumn() and TableSetColumnIndex() return true when a column is visible or performing width measurement.
                        // Because here we know that:
                        // - A) all our columns are contributing the same to row height
                        // - B) column 0 is always visible,
                        // We only always submit this one column and can skip others.
                        // More advanced per-column clipping behaviors may benefit from polling the status flags via TableGetColumnFlags().
                        if (!TableSetColumnIndex(column) && column > 0)
                            continue;
                        if (column == 0)
                            Text("Line %d", row);
                        else
                            Text("Hello world %d,%d", column, row);
                    }
                }
                EndTable();
            }

            Spacing();
            TextUnformatted("Stretch + ScrollX");
            SameLine();
            HelpMarker(
                "Showcase using Stretch columns + ScrollX together: " +
                "this is rather unusual and only makes sense when specifying an 'inner_width' for the table!\n" +
                "Without an explicit value, inner_width is == outer_size.x and therefore using Stretch columns " +
                "along with ScrollX doesn't make sense.");
            DemoT_PushStyleCompact();
            PushID("flags3");
            PushItemWidth(TEXT_BASE_WIDTH * 30);
            ImGui.CheckboxFlags("ImGuiTableFlags_ScrollX", ref DemoTablesLocals.scrollx_flags2, ImGuiTableFlags_ScrollX);
            ImGui.DragFloat("inner_width", ref DemoTablesLocals.scrollx_inner_width, 1.0f, 0.0f, float.MaxValue /* FLT_MAX */, "%.1f");
            PopItemWidth();
            PopID();
            DemoT_PopStyleCompact();
            if (BeginTable("table2", 7, DemoTablesLocals.scrollx_flags2, outer_size, DemoTablesLocals.scrollx_inner_width))
            {
                for (int cell = 0; cell < 20 * 7; cell++)
                {
                    TableNextColumn();
                    Text("Hello world %d,%d", TableGetColumnIndex(), TableGetRowIndex());
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Columns flags"))
        {
            // Create a first table just to show all the options/flags we want to make visible in our example!
            const int column_count = 3;
            string[] column_names = { "One", "Two", "Three" };

            if (BeginTable("table_columns_flags_checkboxes", column_count, ImGuiTableFlags_None))
            {
                DemoT_PushStyleCompact();
                for (int column = 0; column < column_count; column++)
                {
                    TableNextColumn();
                    PushID(column);
                    AlignTextToFramePadding(); // FIXME-TABLE: Workaround for wrong text baseline propagation across columns
                    Text("'%s'", column_names[column]);
                    Spacing();
                    Text("Input flags:");
                    DemoT_EditTableColumnsFlags(ref DemoTablesLocals.colflags_column_flags[column]);
                    Spacing();
                    Text("Output flags:");
                    BeginDisabled(true);
                    DemoT_ShowTableColumnsStatusFlags(DemoTablesLocals.colflags_column_flags_out[column]);
                    EndDisabled();
                    PopID();
                }
                DemoT_PopStyleCompact();
                EndTable();
            }

            // Create the real table we care about for the example!
            // We use a scrolling table to be able to showcase the difference between the _IsEnabled and _IsVisible flags above,
            // otherwise in a non-scrolling table columns are always visible (unless using ImGuiTableFlags_NoKeepColumnsVisible
            // + resizing the parent window down).
            ImGuiTableFlags flags =
                ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY
                | ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersOuter | ImGuiTableFlags_BordersV
                | ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable | ImGuiTableFlags_Sortable;
            ImVec2 outer_size = new ImVec2(0.0f, TEXT_BASE_HEIGHT * 9);
            if (BeginTable("table_columns_flags", column_count, flags, outer_size))
            {
                bool has_angled_header = false;
                for (int column = 0; column < column_count; column++)
                {
                    has_angled_header |= (DemoTablesLocals.colflags_column_flags[column] & ImGuiTableColumnFlags_AngledHeader) != 0;
                    TableSetupColumn(column_names[column], DemoTablesLocals.colflags_column_flags[column]);
                }
                if (has_angled_header)
                    TableAngledHeadersRow();
                TableHeadersRow();
                for (int column = 0; column < column_count; column++)
                    DemoTablesLocals.colflags_column_flags_out[column] = TableGetColumnFlags(column);
                float indent_step = (float)((int)TEXT_BASE_WIDTH / 2);
                for (int row = 0; row < 8; row++)
                {
                    // Add some indentation to demonstrate usage of per-column IndentEnable/IndentDisable flags.
                    Indent(indent_step);
                    TableNextRow();
                    for (int column = 0; column < column_count; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("%s %s", (column == 0) ? "Indented" : "Hello", TableGetColumnName(column));
                    }
                }
                Unindent(indent_step * 8.0f);

                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Columns widths"))
        {
            HelpMarker("Using TableSetupColumn() to setup default width.");

            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.colwidths_flags1, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("ImGuiTableFlags_NoBordersInBodyUntilResize", ref DemoTablesLocals.colwidths_flags1, ImGuiTableFlags_NoBordersInBodyUntilResize);
            DemoT_PopStyleCompact();
            if (BeginTable("table1", 3, DemoTablesLocals.colwidths_flags1))
            {
                // We could also set ImGuiTableFlags_SizingFixedFit on the table and all columns will default to ImGuiTableColumnFlags_WidthFixed.
                TableSetupColumn("one", ImGuiTableColumnFlags_WidthFixed, 100.0f); // Default to 100.0f
                TableSetupColumn("two", ImGuiTableColumnFlags_WidthFixed, 200.0f); // Default to 200.0f
                TableSetupColumn("three", ImGuiTableColumnFlags_WidthFixed);       // Default to auto
                TableHeadersRow();
                for (int row = 0; row < 4; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableSetColumnIndex(column);
                        if (row == 0)
                            Text("(w: %5.1f)", GetContentRegionAvail().x);
                        else
                            Text("Hello %d,%d", column, row);
                    }
                }
                EndTable();
            }

            HelpMarker(
                "Using TableSetupColumn() to setup explicit width.\n\nUnless _NoKeepColumnsVisible is set, " +
                "fixed columns with set width may still be shrunk down if there's not enough space in the host.");

            ImGuiTableFlags flags2 = ImGuiTableFlags_None;
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_NoKeepColumnsVisible", ref flags2, ImGuiTableFlags_NoKeepColumnsVisible);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersInnerV", ref flags2, ImGuiTableFlags_BordersInnerV);
            ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuterV", ref flags2, ImGuiTableFlags_BordersOuterV);
            DemoT_PopStyleCompact();
            if (BeginTable("table2", 4, flags2))
            {
                // We could also set ImGuiTableFlags_SizingFixedFit on the table and then all columns
                // will default to ImGuiTableColumnFlags_WidthFixed.
                TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, 100.0f);
                TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, TEXT_BASE_WIDTH * 15.0f);
                TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, TEXT_BASE_WIDTH * 30.0f);
                TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, TEXT_BASE_WIDTH * 15.0f);
                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 4; column++)
                    {
                        TableSetColumnIndex(column);
                        if (row == 0)
                            Text("(w: %5.1f)", GetContentRegionAvail().x);
                        else
                            Text("Hello %d,%d", column, row);
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Nested tables"))
        {
            HelpMarker("This demonstrates embedding a table into another table cell.");

            if (BeginTable("table_nested1", 2, ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable))
            {
                TableSetupColumn("A0");
                TableSetupColumn("A1");
                TableHeadersRow();

                TableNextColumn();
                Text("A0 Row 0");
                {
                    float rows_height = (TEXT_BASE_HEIGHT * 2.0f) + (GetStyle().CellPadding.y * 2.0f);
                    if (BeginTable("table_nested2", 2, ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable))
                    {
                        TableSetupColumn("B0");
                        TableSetupColumn("B1");
                        TableHeadersRow();

                        TableNextRow(ImGuiTableRowFlags_None, rows_height);
                        TableNextColumn();
                        Text("B0 Row 0");
                        TableNextColumn();
                        Text("B1 Row 0");
                        TableNextRow(ImGuiTableRowFlags_None, rows_height);
                        TableNextColumn();
                        Text("B0 Row 1");
                        TableNextColumn();
                        Text("B1 Row 1");

                        EndTable();
                    }
                }
                TableNextColumn(); Text("A1 Row 0");
                TableNextColumn(); Text("A0 Row 1");
                TableNextColumn(); Text("A1 Row 1");
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Row height"))
        {
            HelpMarker(
                "You can pass a 'min_row_height' to TableNextRow().\n\nRows are padded with 'style.CellPadding.y' on top and bottom, " +
                "so effectively the minimum row height will always be >= 'style.CellPadding.y * 2.0f'.\n\n" +
                "We cannot honor a _maximum_ row height as that would require a unique clipping rectangle per row.");
            if (BeginTable("table_row_height", 1, ImGuiTableFlags_Borders))
            {
                for (int row = 0; row < 8; row++)
                {
                    float min_row_height = (float)(int)(TEXT_BASE_HEIGHT * 0.30f * row + GetStyle().CellPadding.y * 2.0f);
                    TableNextRow(ImGuiTableRowFlags_None, min_row_height);
                    TableNextColumn();
                    Text("min_row_height = %.2f", min_row_height);
                }
                EndTable();
            }

            HelpMarker(
                "Showcase using SameLine(0,0) to share Current Line Height between cells.\n\n" +
                "Please note that Tables Row Height is not the same thing as Current Line Height, " +
                "as a table cell may contains multiple lines.");
            if (BeginTable("table_share_lineheight", 2, ImGuiTableFlags_Borders))
            {
                TableNextRow();
                TableNextColumn();
                ColorButton("##1", new ImVec4(0.13f, 0.26f, 0.40f, 1.0f), ImGuiColorEditFlags_None, new ImVec2(40, 40));
                TableNextColumn();
                Text("Line 1");
                Text("Line 2");

                TableNextRow();
                TableNextColumn();
                ColorButton("##2", new ImVec4(0.13f, 0.26f, 0.40f, 1.0f), ImGuiColorEditFlags_None, new ImVec2(40, 40));
                TableNextColumn();
                SameLine(0.0f, 0.0f); // Reuse line height from previous column
                Text("Line 1, with SameLine(0,0)");
                Text("Line 2");

                EndTable();
            }

            HelpMarker("Showcase altering CellPadding.y between rows. Note that CellPadding.x is locked for the entire table.");
            if (BeginTable("table_changing_cellpadding_y", 1, ImGuiTableFlags_Borders))
            {
                ImGuiStyle style = GetStyle();
                for (int row = 0; row < 8; row++)
                {
                    if ((row % 3) == 2)
                        PushStyleVarY(ImGuiStyleVar_CellPadding, 20.0f);
                    TableNextRow(ImGuiTableRowFlags_None);
                    TableNextColumn();
                    Text("CellPadding.y = %.2f", style.CellPadding.y);
                    if ((row % 3) == 2)
                        PopStyleVar();
                }
                EndTable();
            }

            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Outer size"))
        {
            // Showcasing use of ImGuiTableFlags_NoHostExtendX and ImGuiTableFlags_NoHostExtendY
            // Important to that note how the two flags have slightly different behaviors!
            Text("Using NoHostExtendX and NoHostExtendY:");
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_NoHostExtendX", ref DemoTablesLocals.outersize_flags, ImGuiTableFlags_NoHostExtendX);
            SameLine(); HelpMarker("Make outer width auto-fit to columns, overriding outer_size.x value.\n\nOnly available when ScrollX/ScrollY are disabled and Stretch columns are not used.");
            ImGui.CheckboxFlags("ImGuiTableFlags_NoHostExtendY", ref DemoTablesLocals.outersize_flags, ImGuiTableFlags_NoHostExtendY);
            SameLine(); HelpMarker("Make outer height stop exactly at outer_size.y (prevent auto-extending table past the limit).\n\nOnly available when ScrollX/ScrollY are disabled. Data below the limit will be clipped and not visible.");
            DemoT_PopStyleCompact();

            ImVec2 outer_size = new ImVec2(0.0f, TEXT_BASE_HEIGHT * 5.5f);
            if (BeginTable("table1", 3, DemoTablesLocals.outersize_flags, outer_size))
            {
                for (int row = 0; row < 10; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableNextColumn();
                        Text("Cell %d,%d", column, row);
                    }
                }
                EndTable();
            }
            SameLine();
            Text("Hello!");

            Spacing();

            Text("Using explicit size:");
            if (BeginTable("table2", 3, ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg, new ImVec2(TEXT_BASE_WIDTH * 30, 0.0f)))
            {
                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        TableNextColumn();
                        Text("Cell %d,%d", column, row);
                    }
                }
                EndTable();
            }
            SameLine();
            if (BeginTable("table3", 3, ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg, new ImVec2(TEXT_BASE_WIDTH * 30, 0.0f)))
            {
                float rows_height = TEXT_BASE_HEIGHT * 1.5f + GetStyle().CellPadding.y * 2.0f;
                for (int row = 0; row < 3; row++)
                {
                    TableNextRow(0, rows_height);
                    for (int column = 0; column < 3; column++)
                    {
                        TableNextColumn();
                        Text("Cell %d,%d", column, row);
                    }
                }
                EndTable();
            }

            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Background color"))
        {
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_Borders", ref DemoTablesLocals.bg_flags, ImGuiTableFlags_Borders);
            ImGui.CheckboxFlags("ImGuiTableFlags_RowBg", ref DemoTablesLocals.bg_flags, ImGuiTableFlags_RowBg);
            SameLine(); HelpMarker("ImGuiTableFlags_RowBg automatically sets RowBg0 to alternative colors pulled from the Style.");
            ImGui.Combo("row bg type", ref DemoTablesLocals.bg_row_bg_type, "None\0Red\0Gradient\0");
            ImGui.Combo("row bg target", ref DemoTablesLocals.bg_row_bg_target, "RowBg0\0RowBg1\0"); SameLine(); HelpMarker("Target RowBg0 to override the alternating odd/even colors,\nTarget RowBg1 to blend with them.");
            ImGui.Combo("cell bg type", ref DemoTablesLocals.bg_cell_bg_type, "None\0Blue\0"); SameLine(); HelpMarker("We are colorizing cells to B1->C2 here.");
            System.Diagnostics.Debug.Assert(DemoTablesLocals.bg_row_bg_type >= 0 && DemoTablesLocals.bg_row_bg_type <= 2);
            System.Diagnostics.Debug.Assert(DemoTablesLocals.bg_row_bg_target >= 0 && DemoTablesLocals.bg_row_bg_target <= 1);
            System.Diagnostics.Debug.Assert(DemoTablesLocals.bg_cell_bg_type >= 0 && DemoTablesLocals.bg_cell_bg_type <= 1);
            DemoT_PopStyleCompact();

            if (BeginTable("table1", 5, DemoTablesLocals.bg_flags))
            {
                for (int row = 0; row < 6; row++)
                {
                    TableNextRow();

                    // Demonstrate setting a row background color with 'ImGui::TableSetBgColor(ImGuiTableBgTarget_RowBgX, ...)'
                    // We use a transparent color so we can see the one behind in case our target is RowBg1 and RowBg0 was already targeted by the ImGuiTableFlags_RowBg flag.
                    if (DemoTablesLocals.bg_row_bg_type != 0)
                    {
                        ImU32 row_bg_color = GetColorU32(DemoTablesLocals.bg_row_bg_type == 1 ? new ImVec4(0.7f, 0.3f, 0.3f, 0.65f) : new ImVec4(0.2f + row * 0.1f, 0.2f, 0.2f, 0.65f)); // Flat or Gradient?
                        TableSetBgColor(ImGuiTableBgTarget_RowBg0 + DemoTablesLocals.bg_row_bg_target, row_bg_color);
                    }

                    // Fill cells
                    for (int column = 0; column < 5; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("%c%c", 'A' + row, '0' + column);

                        // Change background of Cells B1->C2
                        // Demonstrate setting a cell background color with 'ImGui::TableSetBgColor(ImGuiTableBgTarget_CellBg, ...)'
                        // (the CellBg color will be blended over the RowBg and ColumnBg colors)
                        // We can also pass a column number as a third parameter to TableSetBgColor() and do this outside the column loop.
                        if (row >= 1 && row <= 2 && column >= 1 && column <= 2 && DemoTablesLocals.bg_cell_bg_type == 1)
                        {
                            ImU32 cell_bg_color = GetColorU32(new ImVec4(0.3f, 0.3f, 0.7f, 0.65f));
                            TableSetBgColor(ImGuiTableBgTarget_CellBg, cell_bg_color);
                        }
                    }
                }
                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Tree view"))
        {
            ImGui.CheckboxFlags("ImGuiTreeNodeFlags_SpanFullWidth",  ref DemoTablesLocals.tree_node_flags_base, ImGuiTreeNodeFlags_SpanFullWidth);
            ImGui.CheckboxFlags("ImGuiTreeNodeFlags_SpanLabelWidth",  ref DemoTablesLocals.tree_node_flags_base, ImGuiTreeNodeFlags_SpanLabelWidth);
            ImGui.CheckboxFlags("ImGuiTreeNodeFlags_SpanAllColumns", ref DemoTablesLocals.tree_node_flags_base, ImGuiTreeNodeFlags_SpanAllColumns);
            ImGui.CheckboxFlags("ImGuiTreeNodeFlags_LabelSpanAllColumns", ref DemoTablesLocals.tree_node_flags_base, ImGuiTreeNodeFlags_LabelSpanAllColumns);
            SameLine(); HelpMarker("Useful if you know that you aren't displaying contents in other columns");

            HelpMarker("See \"Columns flags\" section to configure how indentation is applied to individual columns.");
            if (BeginTable("3ways", 3, DemoTablesLocals.tree_table_flags))
            {
                // The first column will use the default _WidthStretch when ScrollX is Off and _WidthFixed when ScrollX is On
                TableSetupColumn("Name", ImGuiTableColumnFlags_NoHide);
                TableSetupColumn("Size", ImGuiTableColumnFlags_WidthFixed, TEXT_BASE_WIDTH * 12.0f);
                TableSetupColumn("Type", ImGuiTableColumnFlags_WidthFixed, TEXT_BASE_WIDTH * 18.0f);
                TableHeadersRow();

                // Simple storage to output a dummy file-system is in DemoT_MyTreeNode/DemoTablesLocals.tree_nodes.
                DemoT_MyTreeNode.DisplayNode(DemoTablesLocals.tree_nodes[0], DemoTablesLocals.tree_nodes);

                EndTable();
            }
            TreePop();
        }

        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Item width"))
        {
            HelpMarker(
                "Showcase using PushItemWidth() and how it is preserved on a per-column basis.\n\n" +
                "Note that on auto-resizing non-resizable fixed columns, querying the content width for " +
                "e.g. right-alignment doesn't make sense.");
            if (BeginTable("table_item_width", 3, ImGuiTableFlags_Borders))
            {
                TableSetupColumn("small");
                TableSetupColumn("half");
                TableSetupColumn("right-align");
                TableHeadersRow();

                for (int row = 0; row < 3; row++)
                {
                    TableNextRow();
                    if (row == 0)
                    {
                        // Setup ItemWidth once (instead of setting up every time, which is also possible but less efficient)
                        TableSetColumnIndex(0);
                        PushItemWidth(TEXT_BASE_WIDTH * 3.0f); // Small
                        TableSetColumnIndex(1);
                        PushItemWidth(-GetContentRegionAvail().x * 0.5f);
                        TableSetColumnIndex(2);
                        PushItemWidth(-float.Epsilon); // Right-aligned (-FLT_MIN)
                    }

                    // Draw our contents
                    PushID(row);
                    TableSetColumnIndex(0);
                    ImGui.SliderFloat("float0", ref DemoTablesLocals.itemwidth_dummy_f, 0.0f, 1.0f);
                    TableSetColumnIndex(1);
                    ImGui.SliderFloat("float1", ref DemoTablesLocals.itemwidth_dummy_f, 0.0f, 1.0f);
                    TableSetColumnIndex(2);
                    ImGui.SliderFloat("##float2", ref DemoTablesLocals.itemwidth_dummy_f, 0.0f, 1.0f); // No visible label since right-aligned
                    PopID();
                }
                EndTable();
            }
            TreePop();
        }

        // Demonstrate using TableHeader() calls instead of TableHeadersRow()
        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Custom headers"))
        {
            const int COLUMNS_COUNT = 3;
            if (BeginTable("table_custom_headers", COLUMNS_COUNT, ImGuiTableFlags_Borders | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable))
            {
                TableSetupColumn("Apricot");
                TableSetupColumn("Banana");
                TableSetupColumn("Cherry");

                // Instead of calling TableHeadersRow() we'll submit custom headers ourselves.
                // (A different approach is also possible:
                //    - Specify ImGuiTableColumnFlags_NoHeaderLabel in some TableSetupColumn() call.
                //    - Call TableHeadersRow() normally. This will submit TableHeader() with no name.
                //    - Then call TableSetColumnIndex() to position yourself in the column and submit your stuff e.g. Checkbox().)
                TableNextRow(ImGuiTableRowFlags_Headers);
                for (int column = 0; column < COLUMNS_COUNT; column++)
                {
                    TableSetColumnIndex(column);
                    string column_name = TableGetColumnName(column); // Retrieve name passed to TableSetupColumn()
                    PushID(column);
                    PushStyleVar(ImGuiStyleVar_FramePadding, new ImVec2(0, 0));
                    ImGui.Checkbox("##checkall", ref DemoTablesLocals.customheaders_column_selected[column]);
                    PopStyleVar();
                    SameLine(0.0f, GetStyle().ItemInnerSpacing.x);
                    TableHeader(column_name);
                    PopID();
                }

                // Submit table contents
                for (int row = 0; row < 5; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        string buf = ImFmt.Sprintf("Cell %d,%d", column, row);
                        TableSetColumnIndex(column);
                        Selectable(buf, DemoTablesLocals.customheaders_column_selected[column]);
                    }
                }
                EndTable();
            }
            TreePop();
        }

        // Demonstrate using ImGuiTableColumnFlags_AngledHeader flag to create angled headers
        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Angled headers"))
        {
            string[] column_names = { "Track", "cabasa", "ride", "smash", "tom-hi", "tom-mid", "tom-low", "hihat-o", "hihat-c", "snare-s", "snare-c", "clap", "rim", "kick" };
            int columns_count = column_names.Length;
            const int rows_count = 12;

            ImGui.CheckboxFlags("_ScrollX", ref DemoTablesLocals.angled_table_flags, ImGuiTableFlags_ScrollX);
            ImGui.CheckboxFlags("_ScrollY", ref DemoTablesLocals.angled_table_flags, ImGuiTableFlags_ScrollY);
            ImGui.CheckboxFlags("_Resizable", ref DemoTablesLocals.angled_table_flags, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("_Sortable", ref DemoTablesLocals.angled_table_flags, ImGuiTableFlags_Sortable);
            ImGui.CheckboxFlags("_NoBordersInBody", ref DemoTablesLocals.angled_table_flags, ImGuiTableFlags_NoBordersInBody);
            ImGui.CheckboxFlags("_HighlightHoveredColumn", ref DemoTablesLocals.angled_table_flags, ImGuiTableFlags_HighlightHoveredColumn);
            SetNextItemWidth(GetFontSize() * 8);
            ImGui.SliderInt("Frozen columns", ref DemoTablesLocals.angled_frozen_cols, 0, 2);
            SetNextItemWidth(GetFontSize() * 8);
            ImGui.SliderInt("Frozen rows", ref DemoTablesLocals.angled_frozen_rows, 0, 2);
            ImGui.CheckboxFlags("Disable header contributing to column width", ref DemoTablesLocals.angled_column_flags, ImGuiTableColumnFlags_NoHeaderWidth);

            if (TreeNode("Style settings"))
            {
                SameLine();
                HelpMarker("Giving access to some ImGuiStyle value in this demo for convenience.");
                SetNextItemWidth(GetFontSize() * 8);
                {
                    ImGuiStyle style = GetStyle();
                    ImGui.SliderAngle("style.TableAngledHeadersAngle", ref style.TableAngledHeadersAngle, -50.0f, +50.0f);
                }
                SetNextItemWidth(GetFontSize() * 8);
                {
                    ImGuiStyle style = GetStyle();
                    float[] angled_text_align = { style.TableAngledHeadersTextAlign.x, style.TableAngledHeadersTextAlign.y };
                    ImGui.SliderFloat2("style.TableAngledHeadersTextAlign", angled_text_align, 0.0f, 1.0f, "%.2f");
                    style.TableAngledHeadersTextAlign = new ImVec2(angled_text_align[0], angled_text_align[1]);
                }
                TreePop();
            }

            if (BeginTable("table_angled_headers", columns_count, DemoTablesLocals.angled_table_flags, new ImVec2(0.0f, TEXT_BASE_HEIGHT * 12)))
            {
                TableSetupColumn(column_names[0], ImGuiTableColumnFlags_NoHide | ImGuiTableColumnFlags_NoReorder);
                for (int n = 1; n < columns_count; n++)
                    TableSetupColumn(column_names[n], DemoTablesLocals.angled_column_flags);
                TableSetupScrollFreeze(DemoTablesLocals.angled_frozen_cols, DemoTablesLocals.angled_frozen_rows);

                TableAngledHeadersRow(); // Draw angled headers for all columns with the ImGuiTableColumnFlags_AngledHeader flag.
                TableHeadersRow();       // Draw remaining headers and allow access to context-menu and other functions.
                for (int row = 0; row < rows_count; row++)
                {
                    PushID(row);
                    TableNextRow();
                    TableSetColumnIndex(0);
                    AlignTextToFramePadding();
                    Text("Track %d", row);
                    for (int column = 1; column < columns_count; column++)
                        if (TableSetColumnIndex(column))
                        {
                            PushID(column);
                            ImGui.Checkbox("", ref DemoTablesLocals.angled_bools[row * columns_count + column]);
                            PopID();
                        }
                    PopID();
                }
                EndTable();
            }
            TreePop();
        }

        // Demonstrate creating custom context menus inside columns,
        // while playing it nice with context menus provided by TableHeadersRow()/TableHeader()
        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Context menus"))
        {
            HelpMarker(
                "By default, right-clicking over a TableHeadersRow()/TableHeader() line will open the default context-menu.\n" +
                "Using ImGuiTableFlags_ContextMenuInBody we also allow right-clicking over columns body.");

            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_ContextMenuInBody", ref DemoTablesLocals.ctxmenu_flags1, ImGuiTableFlags_ContextMenuInBody);
            DemoT_PopStyleCompact();

            // Context Menus: first example
            // [1.1] Right-click on the TableHeadersRow() line to open the default table context menu.
            // [1.2] Right-click in columns also open the default table context menu (if ImGuiTableFlags_ContextMenuInBody is set)
            const int COLUMNS_COUNT = 3;
            if (BeginTable("table_context_menu", COLUMNS_COUNT, DemoTablesLocals.ctxmenu_flags1))
            {
                TableSetupColumn("One");
                TableSetupColumn("Two");
                TableSetupColumn("Three");

                // [1.1]] Right-click on the TableHeadersRow() line to open the default table context menu.
                TableHeadersRow();

                // Submit dummy contents
                for (int row = 0; row < 4; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < COLUMNS_COUNT; column++)
                    {
                        TableSetColumnIndex(column);
                        Text("Cell %d,%d", column, row);
                    }
                }
                EndTable();
            }

            // Context Menus: second example
            // [2.1] Right-click on the TableHeadersRow() line to open the default table context menu.
            // [2.2] Right-click on the ".." to open a custom popup
            // [2.3] Right-click in columns to open another custom popup
            HelpMarker(
                "Demonstrate mixing table context menu (over header), item context button (over button) " +
                "and custom per-column context menu (over column body).");
            ImGuiTableFlags flags2 = ImGuiTableFlags_Resizable | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_Reorderable | ImGuiTableFlags_Hideable | ImGuiTableFlags_Borders;
            if (BeginTable("table_context_menu_2", COLUMNS_COUNT, flags2))
            {
                TableSetupColumn("One");
                TableSetupColumn("Two");
                TableSetupColumn("Three");

                // [2.1] Right-click on the TableHeadersRow() line to open the default table context menu.
                TableHeadersRow();
                for (int row = 0; row < 4; row++)
                {
                    TableNextRow();
                    for (int column = 0; column < COLUMNS_COUNT; column++)
                    {
                        // Submit dummy contents
                        TableSetColumnIndex(column);
                        Text("Cell %d,%d", column, row);
                        SameLine();

                        // [2.2] Right-click on the ".." to open a custom popup
                        PushID(row * COLUMNS_COUNT + column);
                        SmallButton("..");
                        if (BeginPopupContextItem())
                        {
                            Text("This is the popup for Button(\"..\") in Cell %d,%d", column, row);
                            if (Button("Close"))
                                CloseCurrentPopup();
                            EndPopup();
                        }
                        PopID();
                    }
                }

                // [2.3] Right-click anywhere in columns to open another custom popup
                // (instead of testing for !IsAnyItemHovered() we could also call OpenPopup() with ImGuiPopupFlags_NoOpenOverExistingPopup
                // to manage popup priority as the popups triggers, here "are we hovering a column" are overlapping)
                int hovered_column = -1;
                for (int column = 0; column < COLUMNS_COUNT + 1; column++)
                {
                    PushID(column);
                    if ((TableGetColumnFlags(column) & ImGuiTableColumnFlags_IsHovered) != 0)
                        hovered_column = column;
                    if (hovered_column == column && !IsAnyItemHovered() && IsMouseReleased(1))
                        OpenPopup("MyPopup");
                    if (BeginPopup("MyPopup"))
                    {
                        if (column == COLUMNS_COUNT)
                            Text("This is a custom popup for unused space after the last column.");
                        else
                            Text("This is a custom popup for Column %d", column);
                        if (Button("Close"))
                            CloseCurrentPopup();
                        EndPopup();
                    }
                    PopID();
                }

                EndTable();
                Text("Hovered column: %d", hovered_column);
            }
            TreePop();
        }

        // Demonstrate creating multiple tables with the same ID
        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Synced instances"))
        {
            HelpMarker("Multiple tables with the same identifier will share their settings, width, visibility, order etc.");

            ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.synced_flags, ImGuiTableFlags_Resizable);
            ImGui.CheckboxFlags("ImGuiTableFlags_ScrollY", ref DemoTablesLocals.synced_flags, ImGuiTableFlags_ScrollY);
            ImGui.CheckboxFlags("ImGuiTableFlags_SizingFixedFit", ref DemoTablesLocals.synced_flags, ImGuiTableFlags_SizingFixedFit);
            ImGui.CheckboxFlags("ImGuiTableFlags_HighlightHoveredColumn", ref DemoTablesLocals.synced_flags, ImGuiTableFlags_HighlightHoveredColumn);
            for (int n = 0; n < 3; n++)
            {
                string buf = ImFmt.Sprintf("Synced Table %d", n);
                bool open = CollapsingHeader(buf, ImGuiTreeNodeFlags_DefaultOpen);
                if (open && BeginTable("Table", 3, DemoTablesLocals.synced_flags, new ImVec2(0.0f, GetTextLineHeightWithSpacing() * 5)))
                {
                    TableSetupColumn("One");
                    TableSetupColumn("Two");
                    TableSetupColumn("Three");
                    TableHeadersRow();
                    int cell_count = (n == 1) ? 27 : 9; // Make second table have a scrollbar to verify that additional decoration is not affecting column positions.
                    for (int cell = 0; cell < cell_count; cell++)
                    {
                        TableNextColumn();
                        Text("this cell %d", cell);
                    }
                    EndTable();
                }
            }
            TreePop();
        }

        // Demonstrate using Sorting facilities
        // This is a simplified version of the "Advanced" example, where we mostly focus on the code necessary to handle sorting.
        // Note that the "Advanced" example also showcase manually triggering a sort (e.g. if item quantities have been modified)
        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Sorting"))
        {
            // Create item list
            if (DemoTablesLocals.sorting_items.Size == 0)
            {
                for (int n = 0; n < 50; n++)
                    DemoTablesLocals.sorting_items.push_back(new MyItem());
                for (int n = 0; n < DemoTablesLocals.sorting_items.Size; n++)
                {
                    int template_n = n % DemoTablesLocals.template_items_names.Length;
                    MyItem item = DemoTablesLocals.sorting_items[n];
                    item.ID = n;
                    item.Name = DemoTablesLocals.template_items_names[template_n];
                    item.Quantity = (n * n - n) % 20; // Assign default quantities
                }
            }

            // Options
            DemoT_PushStyleCompact();
            ImGui.CheckboxFlags("ImGuiTableFlags_SortMulti", ref DemoTablesLocals.sorting_flags, ImGuiTableFlags_SortMulti);
            SameLine(); HelpMarker("When sorting is enabled: hold shift when clicking headers to sort on multiple column. TableGetSortSpecs() may return specs where (SpecsCount > 1).");
            ImGui.CheckboxFlags("ImGuiTableFlags_SortTristate", ref DemoTablesLocals.sorting_flags, ImGuiTableFlags_SortTristate);
            SameLine(); HelpMarker("When sorting is enabled: allow no sorting, disable default sorting. TableGetSortSpecs() may return specs where (SpecsCount == 0).");
            DemoT_PopStyleCompact();

            if (BeginTable("table_sorting", 4, DemoTablesLocals.sorting_flags, new ImVec2(0.0f, TEXT_BASE_HEIGHT * 15), 0.0f))
            {
                // Declare columns
                // We use the "user_id" parameter of TableSetupColumn() to specify a user id that will be stored in the sort specifications.
                // This is so our sort function can identify a column given our own identifier. We could also identify them based on their index!
                // Demonstrate using a mixture of flags among available sort-related flags:
                // - ImGuiTableColumnFlags_DefaultSort
                // - ImGuiTableColumnFlags_NoSort / ImGuiTableColumnFlags_NoSortAscending / ImGuiTableColumnFlags_NoSortDescending
                // - ImGuiTableColumnFlags_PreferSortAscending / ImGuiTableColumnFlags_PreferSortDescending
                TableSetupColumn("ID",       ImGuiTableColumnFlags_DefaultSort          | ImGuiTableColumnFlags_WidthFixed,   0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_ID);
                TableSetupColumn("Name",                                                  ImGuiTableColumnFlags_WidthFixed,   0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_Name);
                TableSetupColumn("Action",   ImGuiTableColumnFlags_NoSort               | ImGuiTableColumnFlags_WidthFixed,   0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_Action);
                TableSetupColumn("Quantity", ImGuiTableColumnFlags_PreferSortDescending | ImGuiTableColumnFlags_WidthStretch, 0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_Quantity);
                TableSetupScrollFreeze(0, 1); // Make row always visible
                TableHeadersRow();

                // Sort our data if sort specs have been changed!
                ImGuiTableSortSpecs sort_specs = TableGetSortSpecs();
                if (sort_specs != null)
                    if (sort_specs.SpecsDirty)
                    {
                        DemoTablesLocals.SortWithSortSpecs(sort_specs, DemoTablesLocals.sorting_items.Data, DemoTablesLocals.sorting_items.Size);
                        sort_specs.SpecsDirty = false;
                    }

                // Demonstrate using clipper for large vertical lists
                ImGuiListClipper clipper = new ImGuiListClipper();
                clipper.Begin(DemoTablesLocals.sorting_items.Size);
                while (clipper.Step())
                    for (int row_n = clipper.DisplayStart; row_n < clipper.DisplayEnd; row_n++)
                    {
                        // Display a data item
                        MyItem item = DemoTablesLocals.sorting_items[row_n];
                        PushID(item.ID);
                        TableNextRow();
                        TableNextColumn();
                        Text("%04d", item.ID);
                        TableNextColumn();
                        TextUnformatted(item.Name);
                        TableNextColumn();
                        SmallButton("None");
                        TableNextColumn();
                        Text("%d", item.Quantity);
                        PopID();
                    }
                EndTable();
            }
            TreePop();
        }

        // In this example we'll expose most table flags and settings.
        // For specific flags and settings refer to the corresponding section for more detailed explanation.
        // This section is mostly useful to experiment with combining certain flags or settings with each others.
        //ImGui::SetNextItemOpen(true, ImGuiCond_Once); // [DEBUG]
        if (open_action != -1)
            SetNextItemOpen(open_action != 0);
        if (TreeNode("Advanced"))
        {
            // C++: static ImVec2 outer_size_value = ImVec2(0.0f, TEXT_BASE_HEIGHT * 12); — initialized on first execution of this block.
            if (!DemoTablesLocals.adv_outer_size_value_init)
            {
                DemoTablesLocals.adv_outer_size_value = new ImVec2(0.0f, TEXT_BASE_HEIGHT * 12);
                DemoTablesLocals.adv_outer_size_value_init = true;
            }

            if (TreeNode("Options"))
            {
                // Make the UI compact because there are so many fields
                DemoT_PushStyleCompact();
                PushItemWidth(TEXT_BASE_WIDTH * 28.0f);

                if (TreeNodeEx("Features:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    ImGui.CheckboxFlags("ImGuiTableFlags_Resizable", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_Resizable);
                    ImGui.CheckboxFlags("ImGuiTableFlags_Reorderable", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_Reorderable);
                    ImGui.CheckboxFlags("ImGuiTableFlags_Hideable", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_Hideable);
                    ImGui.CheckboxFlags("ImGuiTableFlags_Sortable", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_Sortable);
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoSavedSettings", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoSavedSettings);
                    ImGui.CheckboxFlags("ImGuiTableFlags_ContextMenuInBody", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_ContextMenuInBody);
                    TreePop();
                }

                if (TreeNodeEx("Decorations:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    ImGui.CheckboxFlags("ImGuiTableFlags_RowBg", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_RowBg);
                    ImGui.CheckboxFlags("ImGuiTableFlags_BordersV", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_BordersV);
                    ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuterV", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_BordersOuterV);
                    ImGui.CheckboxFlags("ImGuiTableFlags_BordersInnerV", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_BordersInnerV);
                    ImGui.CheckboxFlags("ImGuiTableFlags_BordersH", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_BordersH);
                    ImGui.CheckboxFlags("ImGuiTableFlags_BordersOuterH", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_BordersOuterH);
                    ImGui.CheckboxFlags("ImGuiTableFlags_BordersInnerH", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_BordersInnerH);
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoBordersInBody", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoBordersInBody); SameLine(); HelpMarker("Disable vertical borders in columns Body (borders will always appear in Headers)");
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoBordersInBodyUntilResize", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoBordersInBodyUntilResize); SameLine(); HelpMarker("Disable vertical borders in columns Body until hovered for resize (borders will always appear in Headers)");
                    TreePop();
                }

                if (TreeNodeEx("Sizing:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    DemoT_EditTableSizingFlags(ref DemoTablesLocals.adv_flags);
                    SameLine(); HelpMarker("In the Advanced demo we override the policy of each column so those table-wide settings have less effect that typical.");
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoHostExtendX", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoHostExtendX);
                    SameLine(); HelpMarker("Make outer width auto-fit to columns, overriding outer_size.x value.\n\nOnly available when ScrollX/ScrollY are disabled and Stretch columns are not used.");
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoHostExtendY", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoHostExtendY);
                    SameLine(); HelpMarker("Make outer height stop exactly at outer_size.y (prevent auto-extending table past the limit).\n\nOnly available when ScrollX/ScrollY are disabled. Data below the limit will be clipped and not visible.");
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoKeepColumnsVisible", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoKeepColumnsVisible);
                    SameLine(); HelpMarker("Only available if ScrollX is disabled.");
                    ImGui.CheckboxFlags("ImGuiTableFlags_PreciseWidths", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_PreciseWidths);
                    SameLine(); HelpMarker("Disable distributing remainder width to stretched columns (width allocation on a 100-wide table with 3 columns: Without this flag: 33,33,34. With this flag: 33,33,33). With larger number of columns, resizing will appear to be less smooth.");
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoClip", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoClip);
                    SameLine(); HelpMarker("Disable clipping rectangle for every individual columns (reduce draw command count, items will be able to overflow into other columns). Generally incompatible with ScrollFreeze options.");
                    TreePop();
                }

                if (TreeNodeEx("Padding:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    ImGui.CheckboxFlags("ImGuiTableFlags_PadOuterX", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_PadOuterX);
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoPadOuterX", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoPadOuterX);
                    ImGui.CheckboxFlags("ImGuiTableFlags_NoPadInnerX", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_NoPadInnerX);
                    TreePop();
                }

                if (TreeNodeEx("Scrolling:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    ImGui.CheckboxFlags("ImGuiTableFlags_ScrollX", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_ScrollX);
                    SameLine();
                    SetNextItemWidth(GetFrameHeight());
                    ImGui.DragInt("freeze_cols", ref DemoTablesLocals.adv_freeze_cols, 0.2f, 0, 9, null, ImGuiSliderFlags_NoInput);
                    ImGui.CheckboxFlags("ImGuiTableFlags_ScrollY", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_ScrollY);
                    SameLine();
                    SetNextItemWidth(GetFrameHeight());
                    ImGui.DragInt("freeze_rows", ref DemoTablesLocals.adv_freeze_rows, 0.2f, 0, 9, null, ImGuiSliderFlags_NoInput);
                    TreePop();
                }

                if (TreeNodeEx("Sorting:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    ImGui.CheckboxFlags("ImGuiTableFlags_SortMulti", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_SortMulti);
                    SameLine(); HelpMarker("When sorting is enabled: hold shift when clicking headers to sort on multiple column. TableGetSortSpecs() may return specs where (SpecsCount > 1).");
                    ImGui.CheckboxFlags("ImGuiTableFlags_SortTristate", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_SortTristate);
                    SameLine(); HelpMarker("When sorting is enabled: allow no sorting, disable default sorting. TableGetSortSpecs() may return specs where (SpecsCount == 0).");
                    TreePop();
                }

                if (TreeNodeEx("Headers:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    ImGui.Checkbox("show_headers", ref DemoTablesLocals.adv_show_headers);
                    ImGui.CheckboxFlags("ImGuiTableFlags_HighlightHoveredColumn", ref DemoTablesLocals.adv_flags, ImGuiTableFlags_HighlightHoveredColumn);
                    ImGui.CheckboxFlags("ImGuiTableColumnFlags_AngledHeader", ref DemoTablesLocals.adv_columns_base_flags, ImGuiTableColumnFlags_AngledHeader);
                    SameLine(); HelpMarker("Enable AngledHeader on all columns. Best enabled on selected narrow columns (see \"Angled headers\" section of the demo).");
                    TreePop();
                }

                if (TreeNodeEx("Other:", ImGuiTreeNodeFlags_DefaultOpen))
                {
                    ImGui.Checkbox("show_wrapped_text", ref DemoTablesLocals.adv_show_wrapped_text);

                    {
                        float[] outer_size_arr = { DemoTablesLocals.adv_outer_size_value.x, DemoTablesLocals.adv_outer_size_value.y };
                        ImGui.DragFloat2("##OuterSize", outer_size_arr);
                        DemoTablesLocals.adv_outer_size_value = new ImVec2(outer_size_arr[0], outer_size_arr[1]);
                    }
                    SameLine(0.0f, GetStyle().ItemInnerSpacing.x);
                    ImGui.Checkbox("outer_size", ref DemoTablesLocals.adv_outer_size_enabled);
                    SameLine();
                    HelpMarker("If scrolling is disabled (ScrollX and ScrollY not set):\n" +
                        "- The table is output directly in the parent window.\n" +
                        "- OuterSize.x < 0.0f will right-align the table.\n" +
                        "- OuterSize.x = 0.0f will narrow fit the table unless there are any Stretch columns.\n" +
                        "- OuterSize.y then becomes the minimum size for the table, which will extend vertically if there are more rows (unless NoHostExtendY is set).");

                    // From a user point of view we will tend to use 'inner_width' differently depending on whether our table is embedding scrolling.
                    // To facilitate toying with this demo we will actually pass 0.0f to the BeginTable() when ScrollX is disabled.
                    ImGui.DragFloat("inner_width (when ScrollX active)", ref DemoTablesLocals.adv_inner_width_with_scroll, 1.0f, 0.0f, float.MaxValue /* FLT_MAX */);

                    ImGui.DragFloat("row_min_height", ref DemoTablesLocals.adv_row_min_height, 1.0f, 0.0f, float.MaxValue /* FLT_MAX */);
                    SameLine(); HelpMarker("Specify height of the Selectable item.");

                    ImGui.DragInt("items_count", ref DemoTablesLocals.adv_items_count, 0.1f, 0, 9999);
                    string[] contents_type_names = { "Text", "Button", "SmallButton", "FillButton", "Selectable", "Selectable (span row)" };
                    ImGui.Combo("items_type (first column)", ref DemoTablesLocals.adv_contents_type, contents_type_names, contents_type_names.Length);
                    //filter.Draw("filter");
                    TreePop();
                }

                PopItemWidth();
                DemoT_PopStyleCompact();
                Spacing();
                TreePop();
            }

            // Update item list if we changed the number of items
            if (DemoTablesLocals.adv_items.Size != DemoTablesLocals.adv_items_count)
            {
                while (DemoTablesLocals.adv_items.Size < DemoTablesLocals.adv_items_count)
                    DemoTablesLocals.adv_items.push_back(new MyItem());
                DemoTablesLocals.adv_items.resize(DemoTablesLocals.adv_items_count);
                for (int n = 0; n < DemoTablesLocals.adv_items_count; n++)
                {
                    int template_n = n % DemoTablesLocals.template_items_names.Length;
                    MyItem item = DemoTablesLocals.adv_items[n];
                    item.ID = n;
                    item.Name = DemoTablesLocals.template_items_names[template_n];
                    item.Quantity = (template_n == 3) ? 10 : (template_n == 4) ? 20 : 0; // Assign default quantities
                }
            }

            ImDrawList parent_draw_list = GetWindowDrawList();
            int parent_draw_list_draw_cmd_count = parent_draw_list.CmdBuffer.Size;
            ImVec2 table_scroll_cur = default, table_scroll_max = default; // For debug display
            ImDrawList table_draw_list = null;  // "

            // Submit table
            float inner_width_to_use = (DemoTablesLocals.adv_flags & ImGuiTableFlags_ScrollX) != 0 ? DemoTablesLocals.adv_inner_width_with_scroll : 0.0f;
            if (BeginTable("table_advanced", 6, DemoTablesLocals.adv_flags, DemoTablesLocals.adv_outer_size_enabled ? DemoTablesLocals.adv_outer_size_value : new ImVec2(0, 0), inner_width_to_use))
            {
                // Declare columns
                // We use the "user_id" parameter of TableSetupColumn() to specify a user id that will be stored in the sort specifications.
                // This is so our sort function can identify a column given our own identifier. We could also identify them based on their index!
                TableSetupColumn("ID",           DemoTablesLocals.adv_columns_base_flags | ImGuiTableColumnFlags_DefaultSort | ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide, 0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_ID);
                TableSetupColumn("Name",         DemoTablesLocals.adv_columns_base_flags | ImGuiTableColumnFlags_WidthFixed, 0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_Name);
                TableSetupColumn("Action",       DemoTablesLocals.adv_columns_base_flags | ImGuiTableColumnFlags_NoSort | ImGuiTableColumnFlags_WidthFixed, 0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_Action);
                TableSetupColumn("Quantity",     DemoTablesLocals.adv_columns_base_flags | ImGuiTableColumnFlags_PreferSortDescending, 0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_Quantity);
                TableSetupColumn("Description",  DemoTablesLocals.adv_columns_base_flags | (((DemoTablesLocals.adv_flags & ImGuiTableFlags_NoHostExtendX) != 0) ? 0 : ImGuiTableColumnFlags_WidthStretch), 0.0f, (ImGuiID)MyItemColumnID.MyItemColumnID_Description);
                TableSetupColumn("Hidden",       DemoTablesLocals.adv_columns_base_flags |  ImGuiTableColumnFlags_DefaultHide | ImGuiTableColumnFlags_NoSort);
                TableSetupScrollFreeze(DemoTablesLocals.adv_freeze_cols, DemoTablesLocals.adv_freeze_rows);

                // Sort our data if sort specs have been changed!
                ImGuiTableSortSpecs sort_specs = TableGetSortSpecs();
                if (sort_specs != null && sort_specs.SpecsDirty)
                    DemoTablesLocals.adv_items_need_sort = true;
                if (sort_specs != null && DemoTablesLocals.adv_items_need_sort && DemoTablesLocals.adv_items.Size > 1)
                {
                    DemoTablesLocals.SortWithSortSpecs(sort_specs, DemoTablesLocals.adv_items.Data, DemoTablesLocals.adv_items.Size);
                    sort_specs.SpecsDirty = false;
                }
                DemoTablesLocals.adv_items_need_sort = false;

                // Take note of whether we are currently sorting based on the Quantity field,
                // we will use this to trigger sorting when we know the data of this column has been modified.
                bool sorts_specs_using_quantity = (TableGetColumnFlags(3) & ImGuiTableColumnFlags_IsSorted) != 0;

                // Show headers
                if (DemoTablesLocals.adv_show_headers && (DemoTablesLocals.adv_columns_base_flags & ImGuiTableColumnFlags_AngledHeader) != 0)
                    TableAngledHeadersRow();
                if (DemoTablesLocals.adv_show_headers)
                    TableHeadersRow();

                // Show data
                // FIXME-TABLE FIXME-NAV: How we can get decent up/down even though we have the buttons here?
                // Demonstrate using clipper for large vertical lists
                ImGuiListClipper clipper = new ImGuiListClipper();
                clipper.Begin(DemoTablesLocals.adv_items.Size);
                while (clipper.Step())
                {
                    for (int row_n = clipper.DisplayStart; row_n < clipper.DisplayEnd; row_n++)
                    {
                        MyItem item = DemoTablesLocals.adv_items[row_n];
                        //if (!filter.PassFilter(item->Name))
                        //    continue;

                        bool item_is_selected = DemoTablesLocals.adv_selection.contains(item.ID);
                        PushID(item.ID);
                        TableNextRow(ImGuiTableRowFlags_None, DemoTablesLocals.adv_row_min_height);

                        // For the demo purpose we can select among different type of items submitted in the first column
                        TableSetColumnIndex(0);
                        string label = ImFmt.Sprintf("%04d", item.ID);
                        if (DemoTablesLocals.adv_contents_type == DemoTablesLocals.Adv_CT_Text)
                            TextUnformatted(label);
                        else if (DemoTablesLocals.adv_contents_type == DemoTablesLocals.Adv_CT_Button)
                            Button(label);
                        else if (DemoTablesLocals.adv_contents_type == DemoTablesLocals.Adv_CT_SmallButton)
                            SmallButton(label);
                        else if (DemoTablesLocals.adv_contents_type == DemoTablesLocals.Adv_CT_FillButton)
                            Button(label, new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f));
                        else if (DemoTablesLocals.adv_contents_type == DemoTablesLocals.Adv_CT_Selectable || DemoTablesLocals.adv_contents_type == DemoTablesLocals.Adv_CT_SelectableSpanRow)
                        {
                            ImGuiSelectableFlags selectable_flags = (DemoTablesLocals.adv_contents_type == DemoTablesLocals.Adv_CT_SelectableSpanRow) ? ImGuiSelectableFlags_SpanAllColumns | ImGuiSelectableFlags_AllowOverlap : ImGuiSelectableFlags_None;
                            if (Selectable(label, item_is_selected, selectable_flags, new ImVec2(0, DemoTablesLocals.adv_row_min_height)))
                            {
                                if (GetIO().KeyCtrl)
                                {
                                    if (item_is_selected)
                                    {
                                        int sel_idx = DemoTablesLocals.adv_selection.find_index(item.ID);
                                        if (sel_idx >= 0)
                                            DemoTablesLocals.adv_selection.erase_unsorted(sel_idx);
                                    }
                                    else
                                        DemoTablesLocals.adv_selection.push_back(item.ID);
                                }
                                else
                                {
                                    DemoTablesLocals.adv_selection.clear();
                                    DemoTablesLocals.adv_selection.push_back(item.ID);
                                }
                            }
                        }

                        if (TableSetColumnIndex(1))
                            TextUnformatted(item.Name);

                        // Here we demonstrate marking our data set as needing to be sorted again if we modified a quantity,
                        // and we are currently sorting on the column showing the Quantity.
                        // To avoid triggering a sort while holding the button, we only trigger it when the button has been released.
                        // You will probably need some extra logic if you want to automatically sort when a specific entry changes.
                        if (TableSetColumnIndex(2))
                        {
                            if (SmallButton("Chop")) { item.Quantity += 1; }
                            if (sorts_specs_using_quantity && IsItemDeactivated()) { DemoTablesLocals.adv_items_need_sort = true; }
                            SameLine();
                            if (SmallButton("Eat")) { item.Quantity -= 1; }
                            if (sorts_specs_using_quantity && IsItemDeactivated()) { DemoTablesLocals.adv_items_need_sort = true; }
                        }

                        if (TableSetColumnIndex(3))
                            Text("%d", item.Quantity);

                        TableSetColumnIndex(4);
                        if (DemoTablesLocals.adv_show_wrapped_text)
                            TextWrapped("Lorem ipsum dolor sit amet");
                        else
                            Text("Lorem ipsum dolor sit amet");

                        if (TableSetColumnIndex(5))
                            Text("1234");

                        PopID();
                    }
                }

                // Store some info to display debug details below
                table_scroll_cur = new ImVec2(GetScrollX(), GetScrollY());
                table_scroll_max = new ImVec2(GetScrollMaxX(), GetScrollMaxY());
                table_draw_list = GetWindowDrawList();
                EndTable();
            }
            ImGui.Checkbox("Debug details", ref DemoTablesLocals.adv_show_debug_details);
            if (DemoTablesLocals.adv_show_debug_details && table_draw_list != null)
            {
                SameLine(0.0f, 0.0f);
                int table_draw_list_draw_cmd_count = table_draw_list.CmdBuffer.Size;
                if (table_draw_list == parent_draw_list)
                    Text(": DrawCmd: +%d (in same window)",
                        table_draw_list_draw_cmd_count - parent_draw_list_draw_cmd_count);
                else
                    Text(": DrawCmd: +%d (in child window), Scroll: (%.f/%.f) (%.f/%.f)",
                        table_draw_list_draw_cmd_count - 1, table_scroll_cur.x, table_scroll_max.x, table_scroll_cur.y, table_scroll_max.y);
            }
            TreePop();
        }

        PopID();

        DemoWindowColumns();

        if (DemoTablesLocals.disable_indent)
            PopStyleVar();
    }

    // Demonstrate old/legacy Columns API!
    // [2020: Columns are under-featured and not maintained. Prefer using the more flexible and powerful BeginTable() API!]
    // C++: static void DemoWindowColumns() (imgui_demo.cpp 7789-7992)
    public static void DemoWindowColumns()
    {
        bool open = TreeNode("Legacy Columns API");
        SameLine();
        HelpMarker("Columns() is an old API! Prefer using the more flexible and powerful BeginTable() API!");
        if (!open)
            return;

        // Basic columns
        if (TreeNode("Basic"))
        {
            Text("Without border:");
            Columns(3, "mycolumns3", false);  // 3-ways, no border
            Separator();
            for (int n = 0; n < 14; n++)
            {
                string label = ImFmt.Sprintf("Item %d", n);
                if (Selectable(label)) {}
                //if (ImGui::Button(label, ImVec2(-FLT_MIN,0.0f))) {}
                NextColumn();
            }
            Columns(1);
            Separator();

            Text("With border:");
            Columns(4, "mycolumns"); // 4-ways, with border
            Separator();
            Text("ID"); NextColumn();
            Text("Name"); NextColumn();
            Text("Path"); NextColumn();
            Text("Hovered"); NextColumn();
            Separator();
            string[] names = { "One", "Two", "Three" };
            string[] paths = { "/path/one", "/path/two", "/path/three" };
            for (int i = 0; i < 3; i++)
            {
                string label = ImFmt.Sprintf("%04d", i);
                if (Selectable(label, DemoColumnsLocals.selected == i, ImGuiSelectableFlags_SpanAllColumns))
                    DemoColumnsLocals.selected = i;
                bool hovered = IsItemHovered();
                NextColumn();
                Text(names[i]); NextColumn();
                Text(paths[i]); NextColumn();
                Text("%d", hovered ? 1 : 0); NextColumn();
            }
            Columns(1);
            Separator();
            TreePop();
        }

        if (TreeNode("Borders"))
        {
            // NB: Future columns API should allow automatic horizontal borders.
            const int lines_count = 3;
            SetNextItemWidth(GetFontSize() * 8);
            ImGui.DragInt("##columns_count", ref DemoColumnsLocals.columns_count, 0.1f, 2, 10, "%d columns");
            if (DemoColumnsLocals.columns_count < 2)
                DemoColumnsLocals.columns_count = 2;
            SameLine();
            ImGui.Checkbox("horizontal", ref DemoColumnsLocals.h_borders);
            SameLine();
            ImGui.Checkbox("vertical", ref DemoColumnsLocals.v_borders);
            Columns(DemoColumnsLocals.columns_count, null, DemoColumnsLocals.v_borders);
            for (int i = 0; i < DemoColumnsLocals.columns_count * lines_count; i++)
            {
                if (DemoColumnsLocals.h_borders && GetColumnIndex() == 0)
                    Separator();
                PushID(i);
                Text("%c%c%c", 'a' + i, 'a' + i, 'a' + i);
                Text("Width %.2f", GetColumnWidth());
                Text("Avail %.2f", GetContentRegionAvail().x);
                Text("Offset %.2f", GetColumnOffset());
                Text("Long text that is likely to clip");
                Button("Button", new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f));
                PopID();
                NextColumn();
            }
            Columns(1);
            if (DemoColumnsLocals.h_borders)
                Separator();
            TreePop();
        }

        // Create multiple items in a same cell before switching to next column
        if (TreeNode("Mixed items"))
        {
            Columns(3, "mixed");
            Separator();

            Text("Hello");
            Button("Banana");
            NextColumn();

            Text("ImGui");
            Button("Apple");
            ImGui.InputFloat("red", ref DemoColumnsLocals.foo, 0.05f, 0.0f, "%.3f");
            Text("An extra line here.");
            NextColumn();

            Text("Sailor");
            Button("Corniflower");
            ImGui.InputFloat("blue", ref DemoColumnsLocals.bar, 0.05f, 0.0f, "%.3f");
            NextColumn();

            if (CollapsingHeader("Category A")) { Text("Blah blah blah"); } NextColumn();
            if (CollapsingHeader("Category B")) { Text("Blah blah blah"); } NextColumn();
            if (CollapsingHeader("Category C")) { Text("Blah blah blah"); } NextColumn();
            Columns(1);
            Separator();
            TreePop();
        }

        // Word wrapping
        if (TreeNode("Word-wrapping"))
        {
            Columns(2, "word-wrapping");
            Separator();
            TextWrapped("The quick brown fox jumps over the lazy dog.");
            TextWrapped("Hello Left");
            NextColumn();
            TextWrapped("The quick brown fox jumps over the lazy dog.");
            TextWrapped("Hello Right");
            Columns(1);
            Separator();
            TreePop();
        }

        if (TreeNode("Horizontal Scrolling"))
        {
            SetNextWindowContentSize(new ImVec2(1500.0f, 0.0f));
            ImVec2 child_size = new ImVec2(0, GetFontSize() * 20.0f);
            BeginChild("##ScrollingRegion", child_size, ImGuiChildFlags_None, ImGuiWindowFlags_HorizontalScrollbar);
            Columns(10);

            // Also demonstrate using clipper for large vertical lists
            int ITEMS_COUNT = 2000;
            ImGuiListClipper clipper = new ImGuiListClipper();
            clipper.Begin(ITEMS_COUNT);
            while (clipper.Step())
            {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    for (int j = 0; j < 10; j++)
                    {
                        Text("Line %d Column %d...", i, j);
                        NextColumn();
                    }
            }
            Columns(1);
            EndChild();
            TreePop();
        }

        if (TreeNode("Tree"))
        {
            Columns(2, "tree", true);
            for (int x = 0; x < 3; x++)
            {
                // C++: TreeNode((void*)(intptr_t)x, "Node%d", x) — the C# TreeNode(object) hashes reference
                // identity (unstable for boxed ints), so the stable string str_id overload is used instead.
                bool open1 = TreeNode(x.ToString(), "Node%d", x);
                NextColumn();
                Text("Node contents");
                NextColumn();
                if (open1)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        bool open2 = TreeNode(y.ToString(), "Node%d.%d", x, y);
                        NextColumn();
                        Text("Node contents");
                        if (open2)
                        {
                            Text("Even more contents");
                            if (TreeNode("Tree in column"))
                            {
                                Text("The quick brown fox jumps over the lazy dog");
                                TreePop();
                            }
                        }
                        NextColumn();
                        if (open2)
                            TreePop();
                    }
                    TreePop();
                }
            }
            Columns(1);
            TreePop();
        }

        TreePop();
    }
}
