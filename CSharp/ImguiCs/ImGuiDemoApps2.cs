// Port of imgui_demo.cpp [SECTION] Example App: Property Editor / Long Text / Auto Resize /
// Constrained Resize / Simple Overlay / Fullscreen window / Manipulating Window Titles
// (imgui_demo.cpp 9900-10248, plus the ExampleAppPropertyEditor struct it depends on at 9678-9901).
using System;
using System.Text;
using static ImGui.ImGui; // for unqualified ImGui.* calls inside namespace-level Example classes

namespace ImGui;

// C++: struct ExampleAppPropertyEditor (imgui_demo.cpp 9678-9901)
// A single instance lives in DemoApps2Locals (C++ function-local static in ShowExampleAppPropertyEditor()).
file class DemoA2_PropertyEditor
{
    public ImGuiTextFilter Filter = new ImGuiTextFilter();
    public ExampleTreeNode SelectedNode = null;
    public bool UseClipper = false;

    public void Draw(ExampleTreeNode root_node)
    {
        // Left side: draw tree
        // - Currently using a table to benefit from RowBg feature
        // - Our tree node are all of equal height, facilitating the use of a clipper.
        if (BeginChild("##tree", new ImVec2(300, 0), ImGuiChildFlags_ResizeX | ImGuiChildFlags_Borders | ImGuiChildFlags_NavFlattened))
        {
            PushItemFlag(ImGuiItemFlags_NoNavDefaultFocus, true);
            Checkbox("Use Clipper", ref UseClipper);
            SameLine();
            Text("(%d root nodes)", root_node.Childs.Size);
            SetNextItemWidth(-float.Epsilon /* -FLT_MIN */);
            SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_F, ImGuiInputFlags_Tooltip);
            if (InputTextWithHint("##Filter", "incl,-excl", Filter.InputBuf, Filter.InputBuf.Length, ImGuiInputTextFlags_EscapeClearsAll))
                Filter.Build();
            PopItemFlag();

            if (BeginTable("##list", 1, ImGuiTableFlags_RowBg))
            {
                if (UseClipper)
                    DrawClippedTree(root_node);
                else
                    DrawTree(root_node);
                EndTable();
            }
        }
        EndChild();

        // Right side: draw properties
        SameLine();

        BeginGroup(); // Lock X position
        ExampleTreeNode node = SelectedNode;
        if (node != null)
        {
            Text("%s", node.Name);
            TextDisabled("UID: 0x%08X", node.UID);
            Separator();
            if (BeginTable("##properties", 2, ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollY))
            {
                // Push object ID after we entered the table, so table is shared for all objects
                PushID((int)node.UID);
                TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed);
                TableSetupColumn("", ImGuiTableColumnFlags_WidthStretch, 2.0f); // Default twice larger
                if (node.HasData)
                {
                    // In a typical application, the structure description would be derived from a data-driven system.
                    // - We try to mimic this with our ExampleMemberInfo structure and the TreeNodeMemberInfos[] array.
                    // - Limits and some details are hard-coded to simplify the demo.
                    // (C# adaptation: C++ resolves fields via 'offsetof + data_type switch'; we use the
                    //  TreeNodeMemberInfos Get/Set accessors. Behavior is identical.)
                    for (int field_i = 0; field_i < ExampleTree.TreeNodeMemberInfos.Length; field_i++)
                    {
                        ExampleMemberInfo field_desc = ExampleTree.TreeNodeMemberInfos[field_i];
                        TableNextRow();
                        PushID(field_desc.Name);
                        TableNextColumn();
                        AlignTextToFramePadding();
                        TextUnformatted(field_desc.Name);
                        TableNextColumn();
                        switch (field_desc.DataType)
                        {
                        case ImGuiDataType_Bool:
                        {
                            bool v = (bool)field_desc.Get(node);
                            Checkbox("##Editor", ref v);
                            field_desc.Set(node, v);
                            break;
                        }
                        case ImGuiDataType_S32:
                        {
                            int[] v = { (int)field_desc.Get(node) };
                            int[] v_min = { int.MinValue }, v_max = { int.MaxValue };
                            SetNextItemWidth(-float.Epsilon /* -FLT_MIN */);
                            DemoA2_DragScalarN("##Editor", field_desc.DataType, v, field_desc.DataCount, 1.0f, v_min, v_max);
                            field_desc.Set(node, v[0]);
                            break;
                        }
                        case ImGuiDataType_Float:
                        {
                            ImVec2 vec = (ImVec2)field_desc.Get(node);
                            float[] v = { vec.x, vec.y };
                            float[] v_min = { 0.0f, 0.0f }, v_max = { 1.0f, 1.0f };
                            SetNextItemWidth(-float.Epsilon /* -FLT_MIN */);
                            DemoA2_SliderScalarN("##Editor", field_desc.DataType, v, field_desc.DataCount, v_min, v_max);
                            field_desc.Set(node, new ImVec2(v[0], v[1]));
                            break;
                        }
                        case ImGuiDataType_String:
                        {
                            string v = (string)field_desc.Get(node);
                            InputText("##Editor", ref v, ExampleTreeNode.NAME_CAPACITY);
                            field_desc.Set(node, v);
                            break;
                        }
                        }
                        PopID();
                    }
                }
                PopID();
                EndTable();
            }
        }
        EndGroup();
    }

    // Custom search filter
    // - Here we apply on root node only.
    // - This does a case insensitive stristr which is pretty heavy. In a real large-scale app you would likely store a filtered list which in turns would be trivial to linearize.
    bool IsNodePassingFilter(ExampleTreeNode node)
    {
        return node.Parent.Parent != null || Filter.PassFilter(node.Name);
    }

    // Basic version, recursive. This is how you would generally draw a tree.
    // - Simple but going to be noticeably costly if you have a large amount of nodes as DrawTreeNode() is called for all of them.
    // - On my desktop PC (2020), for 10K nodes in an optimized build this takes ~1.2 ms
    // - Unlike arrays or grids which are very easy to clip, trees are currently more difficult to clip.
    void DrawTree(ExampleTreeNode node)
    {
        for (int child_n = 0; child_n < node.Childs.Size; child_n++)
        {
            ExampleTreeNode child = node.Childs[child_n];
            if (IsNodePassingFilter(child) && DrawTreeNode(child))
            {
                DrawTree(child);
                TreePop();
            }
        }
    }

    // More advanced version. Use a alternative clipping technique: fast-forwarding through non-visible chunks.
    // - On my desktop PC (2020), for 10K nodes in an optimized build this takes ~0.1 ms
    //   (in ExampleTree_CreateDemoTree(), change 'int ROOT_ITEMS_COUNT = 10000' to try with this amount of root nodes).
    // - 1. Use clipper with indeterminate count (items_count = INT_MAX): we need to call SeekCursorForItem() at the end once we know the count.
    // - 2. Use SetNextItemStorageID() to specify ID used for open/close storage, making it easy to call TreeNodeGetOpen() on any arbitrary node.
    // - 3. Linearize tree during traversal: our tree data structure makes it easy to access sibling and parents.
    // - Unlike clipping for a regular array or grid which may be done using random access limited to visible areas,
    //   this technique requires traversing most accessible nodes. This could be made more optimal with extra work,
    //   but this is a decent simplicity<>speed trade-off.
    // See https://github.com/ocornut/imgui/issues/3823 for discussions about this.
    void DrawClippedTree(ExampleTreeNode root_node)
    {
        ExampleTreeNode node = root_node.Childs[0]; // First node
        ImGuiListClipper clipper = new ImGuiListClipper();
        clipper.Begin(int.MaxValue);
        while (clipper.Step())
            while (clipper.UserIndex < clipper.DisplayEnd && node != null)
                node = DrawClippedTreeNodeAndAdvanceToNext(clipper, node);

        // Keep going to count nodes and submit final count so we have a reliable scrollbar.
        // - One could consider caching this value and only refreshing it occasionally e.g. window is focused and an action occurs.
        // - Incorrect but cheap approximation would be to use 'clipper_current_idx = IM_MAX(clipper_current_idx, root_node->Childs.Size)' instead.
        // - If either of those is implemented, the general cost will approach zero when scrolling is at the top of the tree.
        while (node != null)
            node = DrawClippedTreeNodeAndAdvanceToNext(clipper, node);
        //clipper.UserIndex = ImMath.ImMax(clipper.UserIndex, root_node.Childs.Size); // <-- Cheap approximation instead of while() loop above.
        clipper.SeekCursorForItem(clipper.UserIndex);
    }

    ExampleTreeNode DrawClippedTreeNodeAndAdvanceToNext(ImGuiListClipper clipper, ExampleTreeNode node)
    {
        if (IsNodePassingFilter(node))
        {
            // Draw node if within visible range
            bool is_open = false;
            if (clipper.UserIndex >= clipper.DisplayStart && clipper.UserIndex < clipper.DisplayEnd)
            {
                is_open = DrawTreeNode(node);
            }
            else
            {
                is_open = (node.Childs.Size > 0 && TreeNodeGetOpen((ImGuiID)node.UID));
                if (is_open)
                    TreePush(node.Name);
            }
            clipper.UserIndex++;

            // Next node: recurse into childs
            if (is_open)
                return node.Childs[0];
        }

        // Next node: next sibling, otherwise move back to parent
        while (node != null)
        {
            if (node.IndexInParent + 1 < node.Parent.Childs.Size)
                return node.Parent.Childs[node.IndexInParent + 1];
            node = node.Parent;
            if (node.Parent == null)
                break;
            TreePop();
        }
        return null;
    }

    // To support node with same name we incorporate node->UID into the item ID.
    // (this would more naturally be done using PushID(node->UID) + TreeNodeEx(node->Name, tree_flags),
    //   but it would require in DrawClippedTreeNodeAndAdvanceToNext() to add PushID() before TreePush(), and PopID() after TreePop(),
    //   so instead we use TreeNodeEx(node->UID, tree_flags, "%s", node->Name) here)
    // (C# adaptation: ImGui's GetID(object) hashes by object identity, which is unstable for boxed values,
    //   so we use the PushID(uid) + TreeNodeEx(name) form the C++ comment describes; IDs remain stable
    //   and unique per node, which is what the demo relies on.)
    bool DrawTreeNode(ExampleTreeNode node)
    {
        TableNextRow();
        TableNextColumn();
        ImGuiTreeNodeFlags tree_flags = ImGuiTreeNodeFlags_None;
        tree_flags |= ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_OpenOnDoubleClick; // Standard opening mode as we are likely to want to add selection afterwards
        tree_flags |= ImGuiTreeNodeFlags_NavLeftJumpsToParent;  // Left arrow support
        tree_flags |= ImGuiTreeNodeFlags_SpanFullWidth;         // Span full width for easier mouse reach
        tree_flags |= ImGuiTreeNodeFlags_DrawLinesToNodes;      // Always draw hierarchy outlines
        if (node == SelectedNode)
            tree_flags |= ImGuiTreeNodeFlags_Selected;          // Draw selection highlight
        if (node.Childs.Size == 0)
            tree_flags |= ImGuiTreeNodeFlags_Leaf | ImGuiTreeNodeFlags_Bullet | ImGuiTreeNodeFlags_NoTreePushOnOpen; // Use _NoTreePushOnOpen + set is_open=false to avoid unnecessarily push/pop on leaves.
        if (node.DataMyBool == false)
            PushStyleColor(ImGuiCol_Text, GetStyle().Colors[ImGuiCol_TextDisabled]);
        SetNextItemStorageID((ImGuiID)node.UID);        // Use node->UID as storage id
        PushID((int)node.UID);
        bool is_open = TreeNodeEx(node.Name, tree_flags);
        PopID();
        if (node.Childs.Size == 0)
            is_open = false;
        if (node.DataMyBool == false)
            PopStyleColor();
        if (IsItemFocused())
            SelectedNode = node;
        return is_open;
    }
}

// imgui_demo.cpp function-local statics for this file's example apps.
file static class DemoApps2Locals
{
    // ShowExampleAppPropertyEditor()
    public static DemoA2_PropertyEditor property_editor; // C++: static ExampleAppPropertyEditor property_editor

    // ShowExampleAppLongText()
    public static int long_text_test_type = 0;           // C++: static int test_type
    public static ImGuiTextBuffer long_text_log = new ImGuiTextBuffer(); // C++: static ImGuiTextBuffer log
    public static int long_text_lines = 0;               // C++: static int lines

    // ShowExampleAppAutoResize()
    public static int auto_resize_lines = 10;            // C++: static int lines

    // ShowExampleAppConstrainedResize()
    public static bool cr_auto_resize = false;           // C++: static bool auto_resize
    public static bool cr_window_padding = true;         // C++: static bool window_padding
    public static int cr_type = 6;                       // C++: static int type (Aspect Ratio)
    public static int cr_display_lines = 10;             // C++: static int display_lines

    // ShowExampleAppSimpleOverlay()
    public static int overlay_location = 0;              // C++: static int location

    // ShowExampleAppFullscreen()
    public static bool fs_use_work_area = true;          // C++: static bool use_work_area
    public static int fs_flags = ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoSavedSettings; // C++: static ImGuiWindowFlags flags
}

public static partial class ImGui
{
    // Demonstrate creating a simple property editor.
    // C++: static void ShowExampleAppPropertyEditor(bool* p_open, ImGuiDemoWindowData* demo_data)  (imgui_demo.cpp 9900)
    public static void ShowExampleAppPropertyEditor(ref bool p_open, ImGuiDemoWindowData demo_data)
    {
        SetNextWindowSize(new ImVec2(430, 450), ImGuiCond_FirstUseEver);
        if (!Begin("Example: Property editor", ref p_open))
        {
            End();
            return;
        }

        if (DemoApps2Locals.property_editor == null)
            DemoApps2Locals.property_editor = new DemoA2_PropertyEditor();
        if (demo_data.DemoTree == null)
            demo_data.DemoTree = ExampleTree.CreateDemoTree();
        DemoApps2Locals.property_editor.Draw(demo_data.DemoTree);

        End();
    }

    // Demonstrate/test rendering huge amount of text, and the incidence of clipping.
    // C++: static void ShowExampleAppLongText(bool* p_open)  (imgui_demo.cpp 9923)
    public static void ShowExampleAppLongText(ref bool p_open)
    {
        SetNextWindowSize(new ImVec2(520, 600), ImGuiCond_FirstUseEver);
        if (!Begin("Example: Long text display", ref p_open))
        {
            End();
            return;
        }

        int test_type = DemoApps2Locals.long_text_test_type;
        ImGuiTextBuffer log = DemoApps2Locals.long_text_log;
        int lines = DemoApps2Locals.long_text_lines;
        Text("Printing unusually long amount of text.");
        Combo("Test type", ref test_type,
            "Single call to TextUnformatted()\0" +
            "Multiple calls to Text(), clipped\0" +
            "Multiple calls to Text(), not clipped (slow)\0");
        Text("Buffer contents: %d lines, %d bytes", lines, log.size());
        if (Button("Clear")) { log.clear(); lines = 0; }
        SameLine();
        if (Button("Add 1000 lines"))
        {
            for (int i = 0; i < 1000; i++)
                log.appendf("%i The quick brown fox jumps over the lazy dog\n", lines + i);
            lines += 1000;
        }
        BeginChild("Log", new ImVec2(0, 0));
        switch (test_type)
        {
        case 0:
            // Single call to TextUnformatted() with a big buffer
            TextUnformatted(log.c_str());
            break;
        case 1:
            {
                // Multiple calls to Text(), manually coarsely clipped - demonstrate how to use the ImGuiListClipper helper.
                PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(0, 0));
                ImGuiListClipper clipper = new ImGuiListClipper();
                clipper.Begin(lines);
                while (clipper.Step())
                    for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        Text("%i The quick brown fox jumps over the lazy dog", i);
                PopStyleVar();
                break;
            }
        case 2:
            // Multiple calls to Text(), not clipped (slow)
            PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(0, 0));
            for (int i = 0; i < lines; i++)
                Text("%i The quick brown fox jumps over the lazy dog", i);
            PopStyleVar();
            break;
        }
        EndChild();
        DemoApps2Locals.long_text_test_type = test_type;
        DemoApps2Locals.long_text_lines = lines;
        End();
    }

    // Demonstrate creating a window which gets auto-resized according to its content.
    // C++: static void ShowExampleAppAutoResize(bool* p_open)  (imgui_demo.cpp 9986)
    public static void ShowExampleAppAutoResize(ref bool p_open)
    {
        if (!Begin("Example: Auto-resizing window", ref p_open, ImGuiWindowFlags_AlwaysAutoResize))
        {
            End();
            return;
        }

        int lines = DemoApps2Locals.auto_resize_lines;
        TextUnformatted(
            "Window will resize every-frame to the size of its content.\n" +
            "Note that you probably don't want to query the window size to\n" +
            "output your content because that would create a feedback loop.");
        SliderInt("Number of lines", ref lines, 1, 20);
        for (int i = 0; i < lines; i++)
            Text("".PadLeft(i * 4) + "This is line %d", i); // Pad with space to extend size horizontally (C++: "%*sThis is line %d")
        DemoApps2Locals.auto_resize_lines = lines;
        End();
    }

    // Helper for ShowExampleAppConstrainedResize(): C++ CustomConstraints::AspectRatio (imgui_demo.cpp 10023)
    // FIXME: This doesn't take account of decoration size (e.g. title bar), library should make this easier.
    // FIXME: None of the three demos works consistently when resizing from borders.
    // (C# adaptation: the port's size callback type is Func<ImGuiSizeCallbackData, float>; the return value is unused.)
    static float DemoA2_ConstraintAspectRatio(ImGuiSizeCallbackData data)
    {
        float aspect_ratio = (float)data.UserData;
        data.DesiredSize.y = (float)(int)(data.DesiredSize.x / aspect_ratio);
        return 0.0f;
    }

    // Helper for ShowExampleAppConstrainedResize(): C++ CustomConstraints::Square (imgui_demo.cpp 10027)
    static float DemoA2_ConstraintSquare(ImGuiSizeCallbackData data)
    {
        float sz = ImMath.ImMax(data.DesiredSize.x, data.DesiredSize.y);
        data.DesiredSize.x = sz;
        data.DesiredSize.y = sz;
        return 0.0f;
    }

    // Helper for ShowExampleAppConstrainedResize(): C++ CustomConstraints::Step (imgui_demo.cpp 10031)
    static float DemoA2_ConstraintStep(ImGuiSizeCallbackData data)
    {
        float step = (float)data.UserData;
        data.DesiredSize = new ImVec2((int)(data.DesiredSize.x / step + 0.5f) * step, (int)(data.DesiredSize.y / step + 0.5f) * step);
        return 0.0f;
    }

    // Demonstrate creating a window with custom resize constraints.
    // Note that size constraints currently don't work on a docked window (when in 'docking' branch)
    // C++: static void ShowExampleAppConstrainedResize(bool* p_open)  (imgui_demo.cpp 10012)
    public static void ShowExampleAppConstrainedResize(ref bool p_open)
    {
        string[] test_desc =
        {
            "Between 100x100 and 500x500",
            "At least 100x100",
            "Resize vertical + lock current width",
            "Resize horizontal + lock current height",
            "Width Between 400 and 500",
            "Height at least 400",
            "Custom: Aspect Ratio 16:9",
            "Custom: Always Square",
            "Custom: Fixed Steps (100)",
        };

        // Options
        bool auto_resize = DemoApps2Locals.cr_auto_resize;
        bool window_padding = DemoApps2Locals.cr_window_padding;
        int type = DemoApps2Locals.cr_type;
        int display_lines = DemoApps2Locals.cr_display_lines;

        // Submit constraint
        float aspect_ratio = 16.0f / 9.0f;
        float fixed_step = 100.0f;
        if (type == 0) SetNextWindowSizeConstraints(new ImVec2(100, 100), new ImVec2(500, 500));                     // Between 100x100 and 500x500
        if (type == 1) SetNextWindowSizeConstraints(new ImVec2(100, 100), new ImVec2(float.MaxValue, float.MaxValue)); // Width > 100, Height > 100
        if (type == 2) SetNextWindowSizeConstraints(new ImVec2(-1, 0),    new ImVec2(-1, float.MaxValue));          // Resize vertical + lock current width
        if (type == 3) SetNextWindowSizeConstraints(new ImVec2(0, -1),    new ImVec2(float.MaxValue, -1));          // Resize horizontal + lock current height
        if (type == 4) SetNextWindowSizeConstraints(new ImVec2(400, -1),  new ImVec2(500, -1));                     // Width Between and 400 and 500
        if (type == 5) SetNextWindowSizeConstraints(new ImVec2(-1, 400),  new ImVec2(-1, float.MaxValue));          // Height at least 400
        if (type == 6) SetNextWindowSizeConstraints(new ImVec2(0, 0),     new ImVec2(float.MaxValue, float.MaxValue), DemoA2_ConstraintAspectRatio, aspect_ratio); // Aspect ratio
        if (type == 7) SetNextWindowSizeConstraints(new ImVec2(0, 0),     new ImVec2(float.MaxValue, float.MaxValue), DemoA2_ConstraintSquare);                    // Always Square
        if (type == 8) SetNextWindowSizeConstraints(new ImVec2(0, 0),     new ImVec2(float.MaxValue, float.MaxValue), DemoA2_ConstraintStep, fixed_step);           // Fixed Step

        // Submit window
        if (!window_padding)
            PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0.0f, 0.0f));
        ImGuiWindowFlags window_flags = auto_resize ? ImGuiWindowFlags_AlwaysAutoResize : 0;
        bool window_open = Begin("Example: Constrained Resize", ref p_open, window_flags);
        if (!window_padding)
            PopStyleVar();
        if (window_open)
        {
            if (GetIO().KeyShift)
            {
                // Display a dummy viewport (in your real app you would likely use ImageButton() to display a texture)
                ImVec2 avail_size = GetContentRegionAvail();
                ImVec2 pos = GetCursorScreenPos();
                ColorButton("viewport", new ImVec4(0.5f, 0.2f, 0.5f, 1.0f), ImGuiColorEditFlags_NoTooltip | ImGuiColorEditFlags_NoDragDrop, avail_size);
                SetCursorScreenPos(new ImVec2(pos.x + 10, pos.y + 10));
                Text("%.2f x %.2f", avail_size.x, avail_size.y);
            }
            else
            {
                Text("(Hold Shift to display a dummy viewport)");
                if (Button("Set 200x200")) { SetWindowSize(new ImVec2(200, 200)); } SameLine();
                if (Button("Set 500x500")) { SetWindowSize(new ImVec2(500, 500)); } SameLine();
                if (Button("Set 800x200")) { SetWindowSize(new ImVec2(800, 200)); }
                SetNextItemWidth(GetFontSize() * 20);
                Combo("Constraint", ref type, test_desc, test_desc.Length);
                SetNextItemWidth(GetFontSize() * 20);
                DragInt("Lines", ref display_lines, 0.2f, 1, 100);
                Checkbox("Auto-resize", ref auto_resize);
                Checkbox("Window padding", ref window_padding);
                for (int i = 0; i < display_lines; i++)
                    Text("".PadLeft(i * 4) + "Hello, sailor! Making this line long enough for the example."); // (C++: "%*sHello, sailor! ...")
            }
        }
        DemoApps2Locals.cr_auto_resize = auto_resize;
        DemoApps2Locals.cr_window_padding = window_padding;
        DemoApps2Locals.cr_type = type;
        DemoApps2Locals.cr_display_lines = display_lines;
        End();
    }

    // Demonstrate creating a simple static window with no decoration
    // + a context-menu to choose which corner of the screen to use.
    // C++: static void ShowExampleAppSimpleOverlay(bool* p_open)  (imgui_demo.cpp 10111)
    public static void ShowExampleAppSimpleOverlay(ref bool p_open)
    {
        int location = DemoApps2Locals.overlay_location;
        ImGuiIO io = GetIO();
        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoFocusOnAppearing | ImGuiWindowFlags_NoNav;
        if (location >= 0)
        {
            const float PAD = 10.0f;
            ImGuiViewport viewport = GetMainViewport();
            ImVec2 work_pos = viewport.WorkPos; // Use work area to avoid menu-bar/task-bar, if any!
            ImVec2 work_size = viewport.WorkSize;
            ImVec2 window_pos, window_pos_pivot;
            window_pos = new ImVec2();
            window_pos_pivot = new ImVec2();
            window_pos.x = (location & 1) != 0 ? (work_pos.x + work_size.x - PAD) : (work_pos.x + PAD);
            window_pos.y = (location & 2) != 0 ? (work_pos.y + work_size.y - PAD) : (work_pos.y + PAD);
            window_pos_pivot.x = (location & 1) != 0 ? 1.0f : 0.0f;
            window_pos_pivot.y = (location & 2) != 0 ? 1.0f : 0.0f;
            SetNextWindowPos(window_pos, ImGuiCond_Always, window_pos_pivot);
            window_flags |= ImGuiWindowFlags_NoMove;
        }
        else if (location == -2)
        {
            // Center window
            SetNextWindowPos(GetMainViewport().GetCenter(), ImGuiCond_Always, new ImVec2(0.5f, 0.5f));
            window_flags |= ImGuiWindowFlags_NoMove;
        }
        SetNextWindowBgAlpha(0.35f); // Transparent background
        if (Begin("Example: Simple overlay", ref p_open, window_flags))
        {
            // Scroll up to the beginning of this function to see overlay flags
            Text("Simple overlay\n" + "(right-click to change position)");
            Separator();
            if (IsMousePosValid())
                Text("Mouse Position: (%.1f,%.1f)", io.MousePos.x, io.MousePos.y);
            else
                Text("Mouse Position: <invalid>");
            if (BeginPopupContextWindow())
            {
                if (MenuItem("Custom",       null, location == -1)) location = -1;
                if (MenuItem("Center",       null, location == -2)) location = -2;
                if (MenuItem("Top-left",     null, location == 0))  location = 0;
                if (MenuItem("Top-right",    null, location == 1))  location = 1;
                if (MenuItem("Bottom-left",  null, location == 2))  location = 2;
                if (MenuItem("Bottom-right", null, location == 3))  location = 3;
                // C++: 'if (p_open && ...)' — p_open is a non-null ref in C#
                if (MenuItem("Close")) p_open = false;
                EndPopup();
            }
        }
        DemoApps2Locals.overlay_location = location;
        End();
    }

    // Demonstrate creating a window covering the entire screen/viewport
    // C++: static void ShowExampleAppFullscreen(bool* p_open)  (imgui_demo.cpp 10166)
    public static void ShowExampleAppFullscreen(ref bool p_open)
    {
        bool use_work_area = DemoApps2Locals.fs_use_work_area;
        ImGuiWindowFlags flags = DemoApps2Locals.fs_flags;

        // We demonstrate using the full viewport area or the work area (without menu-bars, task-bars etc.)
        // Based on your use case you may want one or the other.
        ImGuiViewport viewport = GetMainViewport();
        SetNextWindowPos(use_work_area ? viewport.WorkPos : viewport.Pos);
        SetNextWindowSize(use_work_area ? viewport.WorkSize : viewport.Size);

        if (Begin("Example: Fullscreen window", ref p_open, flags))
        {
            Checkbox("Use work area instead of main area", ref use_work_area);
            SameLine();
            HelpMarker("Main Area = entire viewport,\nWork Area = entire viewport minus sections used by the main menu bars, task bars etc.\n\nEnable the main-menu bar in Examples menu to see the difference.");

            CheckboxFlags("ImGuiWindowFlags_NoBackground", ref flags, ImGuiWindowFlags_NoBackground);
            CheckboxFlags("ImGuiWindowFlags_NoDecoration", ref flags, ImGuiWindowFlags_NoDecoration);
            Indent();
            CheckboxFlags("ImGuiWindowFlags_NoTitleBar", ref flags, ImGuiWindowFlags_NoTitleBar);
            CheckboxFlags("ImGuiWindowFlags_NoCollapse", ref flags, ImGuiWindowFlags_NoCollapse);
            CheckboxFlags("ImGuiWindowFlags_NoScrollbar", ref flags, ImGuiWindowFlags_NoScrollbar);
            Unindent();

            // C++: 'if (p_open && ...)' — p_open is a non-null ref in C#
            if (Button("Close this window"))
                p_open = false;
        }
        DemoApps2Locals.fs_use_work_area = use_work_area;
        DemoApps2Locals.fs_flags = flags;
        End();
    }

    // Demonstrate the use of "##" and "###" in identifiers to manipulate ID generation.
    // This applies to all regular items as well.
    // Read FAQ section "How can I have multiple widgets with the same label?" for details.
    // C++: static void ShowExampleAppWindowTitles(bool*)  (imgui_demo.cpp 10205)
    public static void ShowExampleAppWindowTitles(ref bool p_open)
    {
        ImGuiViewport viewport = GetMainViewport();
        ImVec2 base_pos = viewport.Pos;

        // By default, Windows are uniquely identified by their title.
        // You can use the "##" and "###" markers to manipulate the display/ID.

        // Using "##" to display same title but have unique identifier.
        SetNextWindowPos(new ImVec2(base_pos.x + 100, base_pos.y + 100), ImGuiCond_FirstUseEver);
        Begin("Same title as another window##1");
        Text("This is window 1.\nMy title is the same as window 2, but my identifier is unique.");
        End();

        SetNextWindowPos(new ImVec2(base_pos.x + 100, base_pos.y + 200), ImGuiCond_FirstUseEver);
        Begin("Same title as another window##2");
        Text("This is window 2.\nMy title is the same as window 1, but my identifier is unique.");
        End();

        // Using "###" to display a changing title but keep a static identifier "AnimatedTitle"
        string buf = ImFmt.Sprintf("Animated title %c %d###AnimatedTitle", "|/-\\"[(int)(GetTime() / 0.25f) & 3], GetFrameCount());
        SetNextWindowPos(new ImVec2(base_pos.x + 100, base_pos.y + 300), ImGuiCond_FirstUseEver);
        Begin(buf);
        Text("This window has a changing title.");
        End();
    }

    // C++: ImGui::DragScalarN(label, type, field_ptr, components, 1.0f, &v_min, &v_max) — unsafe fixed-pointer
    // wrapper mirroring the DemoW1_DragScalar<T>() pattern in ImGuiDemoWidgets1.cs.
    internal static unsafe bool DemoA2_DragScalarN<T>(string label, ImGuiDataType data_type, T[] p_data, int components, float v_speed, T[] p_min, T[] p_max) where T : unmanaged
    {
        fixed (T* p_data_ptr = p_data)
        fixed (T* p_min_ptr = p_min)
        fixed (T* p_max_ptr = p_max)
            return DragScalarN(label, data_type, (IntPtr)p_data_ptr, components, v_speed, (IntPtr)p_min_ptr, (IntPtr)p_max_ptr);
    }

    // C++: ImGui::SliderScalarN(label, type, field_ptr, components, &v_min, &v_max) — unsafe fixed-pointer
    // wrapper mirroring the DemoW1_SliderScalar<T>() pattern in ImGuiDemoWidgets1.cs.
    internal static unsafe bool DemoA2_SliderScalarN<T>(string label, ImGuiDataType data_type, T[] p_data, int components, T[] p_min, T[] p_max) where T : unmanaged
    {
        fixed (T* p_data_ptr = p_data)
        fixed (T* p_min_ptr = p_min)
        fixed (T* p_max_ptr = p_max)
            return SliderScalarN(label, data_type, (IntPtr)p_data_ptr, components, (IntPtr)p_min_ptr, (IntPtr)p_max_ptr);
    }
}
