// Port of imgui_demo.cpp DemoWindowLayout() (imgui_demo.cpp 4535-5415) and
// DemoWindowPopups() (imgui_demo.cpp 5421-5716).
// C++ function-local 'static' variables are hosted in the file-local DemoLayoutLocals /
// DemoPopupsLocals classes below.
using System;
using System.Text;

namespace ImGui;

// Function-local statics of DemoWindowLayout() (C++: imgui_demo.cpp 4536-5414).
file static class DemoLayoutLocals
{
    // Child windows
    public static bool DisableMouseWheel = false;
    public static bool DisableMenu = false;

    // Auto-resize with constraints
    public static int DrawLines = 3;
    public static int MaxHeightInLines = 10;

    // Misc/Advanced
    public static int OffsetX = 0;
    public static bool OverrideBgColor = true;
    public static int ChildFlags = ImGuiChildFlags_Borders | ImGuiChildFlags_ResizeX | ImGuiChildFlags_ResizeY;

    // Widgets Width
    public static float F = 0.0f;
    public static bool ShowIndentedItems = true;

    // Basic Horizontal Layout
    public static bool C1 = false, C2 = false, C3 = false, C4 = false;
    public static float F0 = 1.0f, F1 = 2.0f, F2 = 3.0f;
    public static int Item = -1;
    public static int[] Selection = { 0, 1, 2, 3 };

    // Scrolling
    public static int TrackItem = 50;
    public static bool EnableTrack = true;
    public static bool EnableExtraDecorations = false;
    public static float ScrollToOffPx = 0.0f;
    public static float ScrollToPosPx = 200.0f;
    public static int Lines = 7;

    // Horizontal contents size demo window
    public static bool ShowHorizontalContentsSizeDemoWindow = false;
    public static bool ShowHScrollbar = true;
    public static bool ShowButton = true;
    public static bool ShowTreeNodes = true;
    public static bool ShowTextWrapped = false;
    public static bool ShowColumns = true;
    public static bool ShowTabBar = true;
    public static bool ShowChild = false;
    public static bool ExplicitContentSize = false;
    public static float ContentsSizeX = 300.0f;

    // Text Clipping (C++: static ImVec2 size(100.0f, 100.0f); static ImVec2 offset(30.0f, 30.0f);)
    public static float[] DragSize = { 100.0f, 100.0f }; // edited via DragFloat2()
    public static ImVec2 DragOffset = new ImVec2(30.0f, 30.0f);

    // Overlap Mode
    public static bool EnableAllowOverlap = true;
}

// Function-local statics of DemoWindowPopups() (C++: imgui_demo.cpp 5422-5715).
file static class DemoPopupsLocals
{
    public static int SelectedFish = -1;
    public static bool[] Toggles = { true, false, false, false, false };

    // Context menus
    public static int Selected = -1;        // Example 1 (C++: static int selected = -1;)
    public static float Value = 0.5f;       // Example 2 (C++: static float value = 0.5f;)
    public static string Name = "Label1";   // Example 3 (C++: static char name[32] = "Label1";)

    // Modals
    public static bool DontAskMeNextTime = false;
    public static int Item = 1;                          // C++: static int item = 1;
    public static float[] Color = { 0.4f, 0.7f, 0.0f, 0.5f }; // C++: static float color[4]
}

public static partial class ImGui
{
    // C++: static void DemoWindowLayout() (imgui_demo.cpp 4535)
    public static void DemoWindowLayout()
    {
        if (!CollapsingHeader("Layout & Scrolling"))
            return;

        if (TreeNode("Child windows"))
        {
            SeparatorText("Child windows");

            HelpMarker("Use child windows to begin into a self-contained independent scrolling/clipping regions within a host window.");
            Checkbox("Disable Mouse Wheel", ref DemoLayoutLocals.DisableMouseWheel);
            Checkbox("Disable Menu", ref DemoLayoutLocals.DisableMenu);

            // Child 1: no border, enable horizontal scrollbar
            {
                ImGuiWindowFlags window_flags = ImGuiWindowFlags_HorizontalScrollbar;
                if (DemoLayoutLocals.DisableMouseWheel)
                    window_flags |= ImGuiWindowFlags_NoScrollWithMouse;
                BeginChild("ChildL", new ImVec2(GetContentRegionAvail().x * 0.5f, 260), ImGuiChildFlags_None, window_flags);
                for (int i = 0; i < 100; i++)
                    Text("%04d: scrollable region", i);
                EndChild();
            }

            SameLine();

            // Child 2: rounded border
            {
                ImGuiWindowFlags window_flags = ImGuiWindowFlags_None;
                if (DemoLayoutLocals.DisableMouseWheel)
                    window_flags |= ImGuiWindowFlags_NoScrollWithMouse;
                if (!DemoLayoutLocals.DisableMenu)
                    window_flags |= ImGuiWindowFlags_MenuBar;
                PushStyleVar(ImGuiStyleVar_ChildRounding, 5.0f);
                BeginChild("ChildR", new ImVec2(0, 260), ImGuiChildFlags_Borders, window_flags);
                if (!DemoLayoutLocals.DisableMenu && BeginMenuBar())
                {
                    if (BeginMenu("Menu"))
                    {
                        ShowExampleMenuFile();
                        EndMenu();
                    }
                    EndMenuBar();
                }
                if (BeginTable("split", 2, ImGuiTableFlags_Resizable | ImGuiTableFlags_NoSavedSettings))
                {
                    for (int i = 0; i < 100; i++)
                    {
                        // C++: char buf[32]; sprintf(buf, "%03d", i);
                        TableNextColumn();
                        Button(ImFmt.Sprintf("%03d", i), new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f));
                    }
                    EndTable();
                }
                EndChild();
                PopStyleVar();
            }

            // Child 3: manual-resize
            SeparatorText("Manual-resize");
            {
                HelpMarker("Drag bottom border to resize. Double-click bottom border to auto-fit to vertical contents.");
                //if (Button("Set Height to 200"))
                //    SetNextWindowSize(new ImVec2(-FLT_MIN, 200.0f));

                PushStyleColor(ImGuiCol_ChildBg, GetStyleColorVec4(ImGuiCol_FrameBg));
                if (BeginChild("ResizableChild", new ImVec2(-float.Epsilon /* -FLT_MIN */, GetTextLineHeightWithSpacing() * 8), ImGuiChildFlags_Borders | ImGuiChildFlags_ResizeY))
                    for (int n = 0; n < 10; n++)
                        Text("Line %04d", n);
                PopStyleColor();
                EndChild();
            }

            // Child 4: auto-resizing height with a limit
            SeparatorText("Auto-resize with constraints");
            {
                SetNextItemWidth(GetFontSize() * 8);
                DragInt("Lines Count", ref DemoLayoutLocals.DrawLines, 0.2f);
                SetNextItemWidth(GetFontSize() * 8);
                DragInt("Max Height (in Lines)", ref DemoLayoutLocals.MaxHeightInLines, 0.2f);

                SetNextWindowSizeConstraints(new ImVec2(0.0f, GetTextLineHeightWithSpacing() * 1), new ImVec2(float.MaxValue /* FLT_MAX */, GetTextLineHeightWithSpacing() * DemoLayoutLocals.MaxHeightInLines));
                if (BeginChild("ConstrainedChild", new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AutoResizeY))
                    for (int n = 0; n < DemoLayoutLocals.DrawLines; n++)
                        Text("Line %04d", n);
                EndChild();
            }

            SeparatorText("Misc/Advanced");

            // Demonstrate a few extra things
            // - Changing ImGuiCol_ChildBg (which is transparent black in default styles)
            // - Using SetCursorPos() to position child window (the child window is an item from the POV of parent window)
            //   You can also call SetNextWindowPos() to position the child window. The parent window will effectively
            //   layout from this position.
            // - Using ImGui::GetItemRectMin/Max() to query the "item" state (because the child window is an item from
            //   the POV of the parent window). See 'Demo->Querying Status (Edited/Active/Hovered etc.)' for details.
            {
                SetNextItemWidth(GetFontSize() * 8);
                DragInt("Offset X", ref DemoLayoutLocals.OffsetX, 1.0f, -1000, 1000);
                Checkbox("Override ChildBg color", ref DemoLayoutLocals.OverrideBgColor);
                CheckboxFlags("ImGuiChildFlags_Borders", ref DemoLayoutLocals.ChildFlags, ImGuiChildFlags_Borders);
                CheckboxFlags("ImGuiChildFlags_AlwaysUseWindowPadding", ref DemoLayoutLocals.ChildFlags, ImGuiChildFlags_AlwaysUseWindowPadding);
                CheckboxFlags("ImGuiChildFlags_ResizeX", ref DemoLayoutLocals.ChildFlags, ImGuiChildFlags_ResizeX);
                CheckboxFlags("ImGuiChildFlags_ResizeY", ref DemoLayoutLocals.ChildFlags, ImGuiChildFlags_ResizeY);
                CheckboxFlags("ImGuiChildFlags_FrameStyle", ref DemoLayoutLocals.ChildFlags, ImGuiChildFlags_FrameStyle);
                SameLine(); HelpMarker("Style the child window like a framed item: use FrameBg, FrameRounding, FrameBorderSize, FramePadding instead of ChildBg, ChildRounding, ChildBorderSize, WindowPadding.");
                if ((DemoLayoutLocals.ChildFlags & ImGuiChildFlags_FrameStyle) != 0)
                    DemoLayoutLocals.OverrideBgColor = false;

                SetCursorPosX(GetCursorPosX() + DemoLayoutLocals.OffsetX);
                if (DemoLayoutLocals.OverrideBgColor)
                    PushStyleColor(ImGuiCol_ChildBg, ImGuiCore.IM_COL32(255, 0, 0, 100));
                BeginChild("Red", new ImVec2(200, 100), DemoLayoutLocals.ChildFlags, ImGuiWindowFlags_None);
                if (DemoLayoutLocals.OverrideBgColor)
                    PopStyleColor();

                for (int n = 0; n < 50; n++)
                    Text("Some test %d", n);
                EndChild();
                bool child_is_hovered = IsItemHovered();
                ImVec2 child_rect_min = GetItemRectMin();
                ImVec2 child_rect_max = GetItemRectMax();
                Text("Hovered: %d", child_is_hovered);
                Text("Rect of child window is: (%.0f,%.0f) (%.0f,%.0f)", child_rect_min.x, child_rect_min.y, child_rect_max.x, child_rect_max.y);
            }

            TreePop();
        }

        if (TreeNode("Widgets Width"))
        {
            // Use SetNextItemWidth() to set the width of a single upcoming item.
            // Use PushItemWidth()/PopItemWidth() to set the width of a group of items.
            // In real code use you'll probably want to choose width values that are proportional to your font size
            // e.g. Using '20.0f * GetFontSize()' as width instead of '200.0f', etc.

            Checkbox("Show indented items", ref DemoLayoutLocals.ShowIndentedItems);

            Text("SetNextItemWidth/PushItemWidth(100)");
            SameLine(); HelpMarker("Fixed width.");
            PushItemWidth(100);
            DragFloat("float##1b", ref DemoLayoutLocals.F);
            if (DemoLayoutLocals.ShowIndentedItems)
            {
                Indent();
                DragFloat("float (indented)##1b", ref DemoLayoutLocals.F);
                Unindent();
            }
            PopItemWidth();

            Text("SetNextItemWidth/PushItemWidth(-100)");
            SameLine(); HelpMarker("Align to right edge minus 100");
            PushItemWidth(-100);
            DragFloat("float##2a", ref DemoLayoutLocals.F);
            if (DemoLayoutLocals.ShowIndentedItems)
            {
                Indent();
                DragFloat("float (indented)##2b", ref DemoLayoutLocals.F);
                Unindent();
            }
            PopItemWidth();

            Text("SetNextItemWidth/PushItemWidth(GetContentRegionAvail().x * 0.5f)");
            SameLine(); HelpMarker("Half of available width.\n(~ right-cursor_pos)\n(works within a column set)");
            PushItemWidth(GetContentRegionAvail().x * 0.5f);
            DragFloat("float##3a", ref DemoLayoutLocals.F);
            if (DemoLayoutLocals.ShowIndentedItems)
            {
                Indent();
                DragFloat("float (indented)##3b", ref DemoLayoutLocals.F);
                Unindent();
            }
            PopItemWidth();

            Text("SetNextItemWidth/PushItemWidth(-GetContentRegionAvail().x * 0.5f)");
            SameLine(); HelpMarker("Align to right edge minus half");
            PushItemWidth(-GetContentRegionAvail().x * 0.5f);
            DragFloat("float##4a", ref DemoLayoutLocals.F);
            if (DemoLayoutLocals.ShowIndentedItems)
            {
                Indent();
                DragFloat("float (indented)##4b", ref DemoLayoutLocals.F);
                Unindent();
            }
            PopItemWidth();

            Text("SetNextItemWidth/PushItemWidth(-Min(GetContentRegionAvail().x * 0.40f, GetFontSize() * 12))");
            PushItemWidth(-ImMath.ImMin(GetFontSize() * 12, GetContentRegionAvail().x * 0.40f));
            DragFloat("float##5a", ref DemoLayoutLocals.F);
            if (DemoLayoutLocals.ShowIndentedItems)
            {
                Indent();
                DragFloat("float (indented)##5b", ref DemoLayoutLocals.F);
                Unindent();
            }
            PopItemWidth();

            // Demonstrate using PushItemWidth to surround three items.
            // Calling SetNextItemWidth() before each of them would have the same effect.
            Text("SetNextItemWidth/PushItemWidth(-FLT_MIN)");
            SameLine(); HelpMarker("Align to right edge");
            PushItemWidth(-float.Epsilon /* -FLT_MIN */);
            DragFloat("##float6a", ref DemoLayoutLocals.F);
            if (DemoLayoutLocals.ShowIndentedItems)
            {
                Indent();
                DragFloat("float (indented)##6b", ref DemoLayoutLocals.F);
                Unindent();
            }
            PopItemWidth();

            TreePop();
        }

        if (TreeNode("Basic Horizontal Layout"))
        {
            TextWrapped("(Use ImGui::SameLine() to keep adding items to the right of the preceding item)");

            // Text
            Text("Two items: Hello"); SameLine();
            TextColored(new ImVec4(1, 1, 0, 1), "Sailor");

            // Adjust spacing
            Text("More spacing: Hello"); SameLine(0, 20);
            TextColored(new ImVec4(1, 1, 0, 1), "Sailor");

            // Button
            AlignTextToFramePadding();
            Text("Normal buttons"); SameLine();
            Button("Banana"); SameLine();
            Button("Apple"); SameLine();
            Button("Corniflower");

            // Button
            Text("Small buttons"); SameLine();
            SmallButton("Like this one"); SameLine();
            Text("can fit within a text block.");

            // Aligned to arbitrary position. Easy/cheap column.
            Text("Aligned");
            SameLine(150); Text("x=150");
            SameLine(300); Text("x=300");
            Text("Aligned");
            SameLine(150); SmallButton("x=150");
            SameLine(300); SmallButton("x=300");

            // Checkbox
            Checkbox("My", ref DemoLayoutLocals.C1); SameLine();
            Checkbox("Tailor", ref DemoLayoutLocals.C2); SameLine();
            Checkbox("Is", ref DemoLayoutLocals.C3); SameLine();
            Checkbox("Rich", ref DemoLayoutLocals.C4);

            // Various
            PushItemWidth(CalcTextSize("AAAAAAA").x);
            string[] items = { "AAAA", "BBBB", "CCCC", "DDDD" };
            Combo("Combo", ref DemoLayoutLocals.Item, items, items.Length); SameLine();
            SliderFloat("X", ref DemoLayoutLocals.F0, 0.0f, 5.0f); SameLine();
            SliderFloat("Y", ref DemoLayoutLocals.F1, 0.0f, 5.0f); SameLine();
            SliderFloat("Z", ref DemoLayoutLocals.F2, 0.0f, 5.0f);

            Text("Lists:");
            for (int i = 0; i < 4; i++)
            {
                if (i > 0) SameLine();
                PushID(i);
                ListBox("", ref DemoLayoutLocals.Selection[i], items, items.Length);
                PopID();
                //SetItemTooltip("ListBox %d hovered", i);
            }
            PopItemWidth();

            // Dummy
            ImVec2 button_sz = new ImVec2(40, 40);
            Button("A", button_sz); SameLine();
            Dummy(button_sz); SameLine();
            Button("B", button_sz);

            // Manually wrapping
            // (we should eventually provide this as an automatic layout feature, but for now you can do it manually)
            Text("Manual wrapping:");
            ImGuiStyle style = GetStyle();
            int buttons_count = 20;
            float window_visible_x2 = GetCursorScreenPos().x + GetContentRegionAvail().x;
            for (int n = 0; n < buttons_count; n++)
            {
                PushID(n);
                Button("Box", button_sz);
                float last_button_x2 = GetItemRectMax().x;
                float next_button_x2 = last_button_x2 + style.ItemSpacing.x + button_sz.x; // Expected position if next button was on same line
                if (n + 1 < buttons_count && next_button_x2 < window_visible_x2)
                    SameLine();
                PopID();
            }

            TreePop();
        }

        if (TreeNode("Groups"))
        {
            HelpMarker(
                "BeginGroup() basically locks the horizontal position for new line. " +
                "EndGroup() bundles the whole group so that you can use \"item\" functions such as " +
                "IsItemHovered()/IsItemActive() or SameLine() etc. on the whole group.");
            BeginGroup();
            {
                BeginGroup();
                Button("AAA");
                SameLine();
                Button("BBB");
                SameLine();
                BeginGroup();
                Button("CCC");
                Button("DDD");
                EndGroup();
                SameLine();
                Button("EEE");
                EndGroup();
                SetItemTooltip("First group hovered");
            }
            // Capture the group size and create widgets using the same size
            ImVec2 size = GetItemRectSize();
            float[] values = { 0.5f, 0.20f, 0.80f, 0.60f, 0.25f };
            PlotHistogram("##values", values, values.Length, 0, null, 0.0f, 1.0f, size);

            Button("ACTION", new ImVec2((size.x - GetStyle().ItemSpacing.x) * 0.5f, size.y));
            SameLine();
            Button("REACTION", new ImVec2((size.x - GetStyle().ItemSpacing.x) * 0.5f, size.y));
            EndGroup();
            SameLine();

            Button("LEVERAGE\nBUZZWORD", size);
            SameLine();

            if (BeginListBox("List", size))
            {
                Selectable("Selected", true);
                Selectable("Not Selected", false);
                EndListBox();
            }

            TreePop();
        }

        if (TreeNode("Text Baseline Alignment"))
        {
            {
                BulletText("Text baseline:");
                SameLine(); HelpMarker(
                    "This is testing the vertical alignment that gets applied on text to keep it aligned with widgets. " +
                    "Lines only composed of text or \"small\" widgets use less vertical space than lines with framed widgets.");
                Indent();

                Text("KO Blahblah"); SameLine();
                Button("Some framed item"); SameLine();
                HelpMarker("Baseline of button will look misaligned with text..");

                // If your line starts with text, call AlignTextToFramePadding() to align text to upcoming widgets.
                // (because we don't know what's coming after the Text() statement, we need to move the text baseline
                // down by FramePadding.y ahead of time)
                AlignTextToFramePadding();
                Text("OK Blahblah"); SameLine();
                Button("Some framed item##2"); SameLine();
                HelpMarker("We call AlignTextToFramePadding() to vertically align the text baseline by +FramePadding.y");

                // SmallButton() uses the same vertical padding as Text
                Button("TEST##1"); SameLine();
                Text("TEST"); SameLine();
                SmallButton("TEST##2");

                // If your line starts with text, call AlignTextToFramePadding() to align text to upcoming widgets.
                AlignTextToFramePadding();
                Text("Text aligned to framed item"); SameLine();
                Button("Item##1"); SameLine();
                Text("Item"); SameLine();
                SmallButton("Item##2"); SameLine();
                Button("Item##3");

                Unindent();
            }

            Spacing();

            {
                BulletText("Multi-line text:");
                Indent();
                Text("One\nTwo\nThree"); SameLine();
                Text("Hello\nWorld"); SameLine();
                Text("Banana");

                Text("Banana"); SameLine();
                Text("Hello\nWorld"); SameLine();
                Text("One\nTwo\nThree");

                Button("HOP##1"); SameLine();
                Text("Banana"); SameLine();
                Text("Hello\nWorld"); SameLine();
                Text("Banana");

                Button("HOP##2"); SameLine();
                Text("Hello\nWorld"); SameLine();
                Text("Banana");
                Unindent();
            }

            Spacing();

            {
                BulletText("Misc items:");
                Indent();

                // SmallButton() sets FramePadding to zero. Text baseline is aligned to match baseline of previous Button.
                Button("80x80", new ImVec2(80, 80));
                SameLine();
                Button("50x50", new ImVec2(50, 50));
                SameLine();
                Button("Button()");
                SameLine();
                SmallButton("SmallButton()");

                // Tree
                // (here the node appears after a button and has odd intent, so we use ImGuiTreeNodeFlags_DrawLinesNone to disable hierarchy outline)
                float spacing = GetStyle().ItemInnerSpacing.x;
                Button("Button##1"); // Will make line higher
                SameLine(0.0f, spacing);
                if (TreeNodeEx("Node##1", ImGuiTreeNodeFlags_DrawLinesNone))
                {
                    // Placeholder tree data
                    for (int i = 0; i < 6; i++)
                        BulletText("Item %d..", i);
                    TreePop();
                }

                float padding = (float)(int)(GetFontSize() * 1.20f); // Large padding
                PushStyleVarY(ImGuiStyleVar_FramePadding, padding);
                Button("Button##2");
                PopStyleVar();
                SameLine(0.0f, spacing);
                if (TreeNodeEx("Node##2", ImGuiTreeNodeFlags_DrawLinesNone))
                    TreePop();

                // Vertically align text node a bit lower so it'll be vertically centered with upcoming widget.
                // Otherwise you can use SmallButton() (smaller fit).
                AlignTextToFramePadding();

                // Common mistake to avoid: if we want to SameLine after TreeNode we need to do it before we add
                // other contents "inside" the node.
                bool node_open = TreeNode("Node##3");
                SameLine(0.0f, spacing); Button("Button##3");
                if (node_open)
                {
                    // Placeholder tree data
                    for (int i = 0; i < 6; i++)
                        BulletText("Item %d..", i);
                    TreePop();
                }

                // Bullet
                Button("Button##4");
                SameLine(0.0f, spacing);
                BulletText("Bullet text");

                AlignTextToFramePadding();
                BulletText("Node");
                SameLine(0.0f, spacing); Button("Button##5");
                Unindent();
            }

            TreePop();
        }

        if (TreeNode("Scrolling"))
        {
            // Vertical scroll functions
            HelpMarker("Use SetScrollHereY() or SetScrollFromPosY() to scroll to a given vertical position.");

            Checkbox("Decoration", ref DemoLayoutLocals.EnableExtraDecorations);

            PushItemWidth(GetFontSize() * 10);
            DemoLayoutLocals.EnableTrack |= DragInt("##item", ref DemoLayoutLocals.TrackItem, 0.25f, 0, 99, "Item = %d");
            SameLine();
            Checkbox("Track", ref DemoLayoutLocals.EnableTrack);

            bool scroll_to_off = DragFloat("##off", ref DemoLayoutLocals.ScrollToOffPx, 1.00f, 0.0f, float.MaxValue /* FLT_MAX */, "+%.0f px");
            SameLine();
            scroll_to_off |= Button("Scroll Offset");

            bool scroll_to_pos = DragFloat("##pos", ref DemoLayoutLocals.ScrollToPosPx, 1.00f, -10.0f, float.MaxValue /* FLT_MAX */, "X/Y = %.0f px");
            SameLine();
            scroll_to_pos |= Button("Scroll To Pos");
            PopItemWidth();

            if (scroll_to_off || scroll_to_pos)
                DemoLayoutLocals.EnableTrack = false;

            ImGuiStyle style = GetStyle();
            float child_w = (GetContentRegionAvail().x - 4 * style.ItemSpacing.x) / 5;
            if (child_w < 1.0f)
                child_w = 1.0f;
            PushID("##VerticalScrolling");
            for (int i = 0; i < 5; i++)
            {
                if (i > 0) SameLine();
                BeginGroup();
                string[] names = { "Top", "25%", "Center", "75%", "Bottom" };
                TextUnformatted(names[i]);

                ImGuiWindowFlags child_flags = DemoLayoutLocals.EnableExtraDecorations ? ImGuiWindowFlags_MenuBar : 0;
                ImGuiID child_id = GetID(i); // C++: ImGui::GetID((void*)(intptr_t)i)
                bool child_is_visible = BeginChild(child_id, new ImVec2(child_w, 200.0f), ImGuiChildFlags_Borders, child_flags);
                if (BeginMenuBar())
                {
                    TextUnformatted("abc");
                    EndMenuBar();
                }
                if (scroll_to_off)
                    SetScrollY(DemoLayoutLocals.ScrollToOffPx);
                if (scroll_to_pos)
                    SetScrollFromPosY(GetCursorStartPos().y + DemoLayoutLocals.ScrollToPosPx, i * 0.25f);
                if (child_is_visible) // Avoid calling SetScrollHereY when running with culled items
                {
                    for (int item = 0; item < 100; item++)
                    {
                        if (DemoLayoutLocals.EnableTrack && item == DemoLayoutLocals.TrackItem)
                        {
                            TextColored(new ImVec4(1, 1, 0, 1), "Item %d", item);
                            SetScrollHereY(i * 0.25f); // 0.0f:top, 0.5f:center, 1.0f:bottom
                        }
                        else
                        {
                            Text("Item %d", item);
                        }
                    }
                }
                float scroll_y = GetScrollY();
                float scroll_max_y = GetScrollMaxY();
                EndChild();
                Text("%.0f/%.0f", scroll_y, scroll_max_y);
                EndGroup();
            }
            PopID();

            // Horizontal scroll functions
            Spacing();
            HelpMarker(
                "Use SetScrollHereX() or SetScrollFromPosX() to scroll to a given horizontal position.\n\n" +
                "Because the clipping rectangle of most window hides half worth of WindowPadding on the " +
                "left/right, using SetScrollFromPosX(+1) will usually result in clipped text whereas the " +
                "equivalent SetScrollFromPosY(+1) wouldn't.");
            PushID("##HorizontalScrolling");
            for (int i = 0; i < 5; i++)
            {
                float child_height = GetTextLineHeight() + style.ScrollbarSize + style.WindowPadding.y * 2.0f;
                ImGuiWindowFlags child_flags = ImGuiWindowFlags_HorizontalScrollbar | (DemoLayoutLocals.EnableExtraDecorations ? ImGuiWindowFlags_AlwaysVerticalScrollbar : 0);
                ImGuiID child_id = GetID(i); // C++: ImGui::GetID((void*)(intptr_t)i)
                bool child_is_visible = BeginChild(child_id, new ImVec2(-100, child_height), ImGuiChildFlags_Borders, child_flags);
                if (scroll_to_off)
                    SetScrollX(DemoLayoutLocals.ScrollToOffPx);
                if (scroll_to_pos)
                    SetScrollFromPosX(GetCursorStartPos().x + DemoLayoutLocals.ScrollToPosPx, i * 0.25f);
                if (child_is_visible) // Avoid calling SetScrollHereY when running with culled items
                {
                    for (int item = 0; item < 100; item++)
                    {
                        if (item > 0)
                            SameLine();
                        if (DemoLayoutLocals.EnableTrack && item == DemoLayoutLocals.TrackItem)
                        {
                            TextColored(new ImVec4(1, 1, 0, 1), "Item %d", item);
                            SetScrollHereX(i * 0.25f); // 0.0f:left, 0.5f:center, 1.0f:right
                        }
                        else
                        {
                            Text("Item %d", item);
                        }
                    }
                }
                float scroll_x = GetScrollX();
                float scroll_max_x = GetScrollMaxX();
                EndChild();
                SameLine();
                string[] names = { "Left", "25%", "Center", "75%", "Right" };
                Text("%s\n%.0f/%.0f", names[i], scroll_x, scroll_max_x);
                Spacing();
            }
            PopID();

            // Miscellaneous Horizontal Scrolling Demo
            HelpMarker(
                "Horizontal scrolling for a window is enabled via the ImGuiWindowFlags_HorizontalScrollbar flag.\n\n" +
                "You may want to also explicitly specify content width by using SetNextWindowContentWidth() before Begin().");
            SliderInt("Lines", ref DemoLayoutLocals.Lines, 1, 15);
            PushStyleVar(ImGuiStyleVar_FrameRounding, 3.0f);
            PushStyleVar(ImGuiStyleVar_FramePadding, new ImVec2(2.0f, 1.0f));
            ImVec2 scrolling_child_size = new ImVec2(0, GetFrameHeightWithSpacing() * 7 + 30);
            BeginChild("scrolling", scrolling_child_size, ImGuiChildFlags_Borders, ImGuiWindowFlags_HorizontalScrollbar);
            for (int line = 0; line < DemoLayoutLocals.Lines; line++)
            {
                // Display random stuff. For the sake of this trivial demo we are using basic Button() + SameLine()
                // If you want to create your own time line for a real application you may be better off manipulating
                // the cursor position yourself, aka using SetCursorPos/SetCursorScreenPos to position the widgets
                // yourself. You may also want to use the lower-level ImDrawList API.
                int num_buttons = 10 + ((line & 1) != 0 ? line * 9 : line * 3);
                float base_w = GetFontSize() * 3;
                for (int n = 0; n < num_buttons; n++)
                {
                    if (n > 0) SameLine();
                    PushID(n + line * 1000);
                    string num_buf = ImFmt.Sprintf("%d", n);
                    string label = (n % 15) == 0 ? "FizzBuzz" : (n % 3) == 0 ? "Fizz" : (n % 5) == 0 ? "Buzz" : num_buf;
                    float hue = n * 0.05f;
                    PushStyleColor(ImGuiCol_Button, DemoLP_ImColorHSV(hue, 0.6f, 0.6f));
                    PushStyleColor(ImGuiCol_ButtonHovered, DemoLP_ImColorHSV(hue, 0.7f, 0.7f));
                    PushStyleColor(ImGuiCol_ButtonActive, DemoLP_ImColorHSV(hue, 0.8f, 0.8f));
                    Button(label, new ImVec2(base_w + ImMath.ImSin((float)(line + n)) * base_w * 0.5f, 0.0f));
                    PopStyleColor(3);
                    PopID();
                }
            }
            float scrolling_scroll_x = GetScrollX();
            float scrolling_scroll_max_x = GetScrollMaxX();
            EndChild();
            PopStyleVar(2);
            float scroll_x_delta = 0.0f;
            SmallButton("<<");
            if (IsItemActive())
                scroll_x_delta = -GetIO().DeltaTime * 1000.0f;
            SameLine();
            Text("Scroll from code"); SameLine();
            SmallButton(">>");
            if (IsItemActive())
                scroll_x_delta = +GetIO().DeltaTime * 1000.0f;
            SameLine();
            Text("%.0f/%.0f", scrolling_scroll_x, scrolling_scroll_max_x);
            if (scroll_x_delta != 0.0f)
            {
                // Demonstrate a trick: you can use Begin to set yourself in the context of another window
                // (here we are already out of your child window)
                BeginChild("scrolling", new ImVec2(0, 0), 0, 0);
                SetScrollX(GetScrollX() + scroll_x_delta);
                EndChild();
            }
            Spacing();

            Checkbox("Show Horizontal contents size demo window", ref DemoLayoutLocals.ShowHorizontalContentsSizeDemoWindow);

            if (DemoLayoutLocals.ShowHorizontalContentsSizeDemoWindow)
            {
                if (DemoLayoutLocals.ExplicitContentSize)
                    SetNextWindowContentSize(new ImVec2(DemoLayoutLocals.ContentsSizeX, 0.0f));
                Begin("Horizontal contents size demo window", ref DemoLayoutLocals.ShowHorizontalContentsSizeDemoWindow, DemoLayoutLocals.ShowHScrollbar ? ImGuiWindowFlags_HorizontalScrollbar : 0);
                PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(2, 0));
                PushStyleVar(ImGuiStyleVar_FramePadding, new ImVec2(2, 0));
                HelpMarker(
                    "Test how different widgets react and impact the work rectangle growing when horizontal scrolling is enabled.\n\n" +
                    "Use 'Metrics->Tools->Show windows rectangles' to visualize rectangles.");
                Checkbox("H-scrollbar", ref DemoLayoutLocals.ShowHScrollbar);
                Checkbox("Button", ref DemoLayoutLocals.ShowButton);            // Will grow contents size (unless explicitly overwritten)
                Checkbox("Tree nodes", ref DemoLayoutLocals.ShowTreeNodes);    // Will grow contents size and display highlight over full width
                Checkbox("Text wrapped", ref DemoLayoutLocals.ShowTextWrapped);// Will grow and use contents size
                Checkbox("Columns", ref DemoLayoutLocals.ShowColumns);          // Will use contents size
                Checkbox("Tab bar", ref DemoLayoutLocals.ShowTabBar);          // Will use contents size
                Checkbox("Child", ref DemoLayoutLocals.ShowChild);              // Will grow and use contents size
                Checkbox("Explicit content size", ref DemoLayoutLocals.ExplicitContentSize);
                Text("Scroll %.1f/%.1f %.1f/%.1f", GetScrollX(), GetScrollMaxX(), GetScrollY(), GetScrollMaxY());
                if (DemoLayoutLocals.ExplicitContentSize)
                {
                    SameLine();
                    SetNextItemWidth(CalcTextSize("123456").x);
                    DragFloat("##csx", ref DemoLayoutLocals.ContentsSizeX);
                    ImVec2 p = GetCursorScreenPos();
                    GetWindowDrawList().AddRectFilled(p, new ImVec2(p.x + 10, p.y + 10), ImGuiCore.IM_COL32_WHITE);
                    GetWindowDrawList().AddRectFilled(new ImVec2(p.x + DemoLayoutLocals.ContentsSizeX - 10, p.y), new ImVec2(p.x + DemoLayoutLocals.ContentsSizeX, p.y + 10), ImGuiCore.IM_COL32_WHITE);
                    Dummy(new ImVec2(0, 10));
                }
                PopStyleVar(2);
                Separator();
                if (DemoLayoutLocals.ShowButton)
                {
                    Button("this is a 300-wide button", new ImVec2(300, 0));
                }
                if (DemoLayoutLocals.ShowTreeNodes)
                {
                    bool open = true;
                    if (TreeNode("this is a tree node"))
                    {
                        if (TreeNode("another one of those tree node..."))
                        {
                            Text("Some tree contents");
                            TreePop();
                        }
                        TreePop();
                    }
                    CollapsingHeader("CollapsingHeader", ref open);
                }
                if (DemoLayoutLocals.ShowTextWrapped)
                {
                    TextWrapped("This text should automatically wrap on the edge of the work rectangle.");
                }
                if (DemoLayoutLocals.ShowColumns)
                {
                    Text("Tables:");
                    if (BeginTable("table", 4, ImGuiTableFlags_Borders))
                    {
                        for (int n = 0; n < 4; n++)
                        {
                            TableNextColumn();
                            Text("Width %.2f", GetContentRegionAvail().x);
                        }
                        EndTable();
                    }
                    Text("Columns:");
                    Columns(4);
                    for (int n = 0; n < 4; n++)
                    {
                        Text("Width %.2f", GetColumnWidth());
                        NextColumn();
                    }
                    Columns(1);
                }
                if (DemoLayoutLocals.ShowTabBar && BeginTabBar("Hello"))
                {
                    if (BeginTabItem("OneOneOne")) { EndTabItem(); }
                    if (BeginTabItem("TwoTwoTwo")) { EndTabItem(); }
                    if (BeginTabItem("ThreeThreeThree")) { EndTabItem(); }
                    if (BeginTabItem("FourFourFour")) { EndTabItem(); }
                    EndTabBar();
                }
                if (DemoLayoutLocals.ShowChild)
                {
                    BeginChild("child", new ImVec2(0, 0), ImGuiChildFlags_Borders);
                    EndChild();
                }
                End();
            }

            TreePop();
        }

        if (TreeNode("Text Clipping"))
        {
            DragFloat2("size", DemoLayoutLocals.DragSize, 0.5f, 1.0f, 200.0f, "%.0f");
            TextWrapped("(Click and drag to scroll)");

            HelpMarker(
                "(Left) Using ImGui::PushClipRect():\n" +
                "Will alter ImGui hit-testing logic + ImDrawList rendering.\n" +
                "(use this if you want your clipping rectangle to affect interactions)\n\n" +
                "(Center) Using ImDrawList::PushClipRect():\n" +
                "Will alter ImDrawList rendering only.\n" +
                "(use this as a shortcut if you are only using ImDrawList calls)\n\n" +
                "(Right) Using ImDrawList::AddText() with a fine ClipRect:\n" +
                "Will alter only this specific ImDrawList::AddText() rendering.\n" +
                "This is often used internally to avoid altering the clipping rectangle and minimize draw calls.");

            for (int n = 0; n < 3; n++)
            {
                if (n > 0)
                    SameLine();

                PushID(n);
                ImVec2 size = new ImVec2(DemoLayoutLocals.DragSize[0], DemoLayoutLocals.DragSize[1]);
                InvisibleButton("##canvas", size);
                if (IsItemActive() && IsMouseDragging(ImGuiMouseButton_Left))
                {
                    DemoLayoutLocals.DragOffset.x += GetIO().MouseDelta.x;
                    DemoLayoutLocals.DragOffset.y += GetIO().MouseDelta.y;
                }
                PopID();
                if (!IsItemVisible()) // Skip rendering as ImDrawList elements are not clipped.
                    continue;

                ImVec2 p0 = GetItemRectMin();
                ImVec2 p1 = GetItemRectMax();
                string text_str = "Line 1 hello\nLine 2 clip me!";
                ImVec2 text_pos = new ImVec2(p0.x + DemoLayoutLocals.DragOffset.x, p0.y + DemoLayoutLocals.DragOffset.y);
                ImDrawList draw_list = GetWindowDrawList();
                switch (n)
                {
                    case 0:
                        PushClipRect(p0, p1, true);
                        draw_list.AddRectFilled(p0, p1, ImGuiCore.IM_COL32(90, 90, 120, 255));
                        draw_list.AddText(text_pos, ImGuiCore.IM_COL32_WHITE, text_str);
                        PopClipRect();
                        break;
                    case 1:
                        draw_list.PushClipRect(p0, p1, true);
                        draw_list.AddRectFilled(p0, p1, ImGuiCore.IM_COL32(90, 90, 120, 255));
                        draw_list.AddText(text_pos, ImGuiCore.IM_COL32_WHITE, text_str);
                        draw_list.PopClipRect();
                        break;
                    case 2:
                        {
                            // C++: ImVec4 clip_rect(p0.x, p0.y, p1.x, p1.y); AddText() takes a ImVec4* here so let's convert.
                            ImVec4 clip_rect = new ImVec4(p0.x, p0.y, p1.x, p1.y);
                            draw_list.AddRectFilled(p0, p1, ImGuiCore.IM_COL32(90, 90, 120, 255));
                            draw_list.AddText(GetFont(), GetFontSize(), text_pos, ImGuiCore.IM_COL32_WHITE, text_str, 0, -1, 0.0f, clip_rect);
                            break;
                        }
                }
            }

            TreePop();
        }

        if (TreeNode("Overlap Mode"))
        {
            HelpMarker(
                "Hit-testing is by default performed in item submission order, which generally is perceived as 'back-to-front'.\n\n" +
                "By using SetNextItemAllowOverlap() you can notify that an item may be overlapped by another. " +
                "Doing so alters the hovering logic: items using AllowOverlap mode requires an extra frame to accept hovered state.");
            Checkbox("Enable AllowOverlap", ref DemoLayoutLocals.EnableAllowOverlap);

            ImVec2 button1_pos = GetCursorScreenPos();
            ImVec2 button2_pos = new ImVec2(button1_pos.x + 50.0f, button1_pos.y + 50.0f);
            if (DemoLayoutLocals.EnableAllowOverlap)
                SetNextItemAllowOverlap();
            Button("Button 1", new ImVec2(80, 80));
            SetCursorScreenPos(button2_pos);
            Button("Button 2", new ImVec2(80, 80));

            // This is typically used with width-spanning items.
            // (note that Selectable() has a dedicated flag ImGuiSelectableFlags_AllowOverlap, which is a shortcut
            // for using SetNextItemAllowOverlap(). For demo purpose we use SetNextItemAllowOverlap() here.)
            if (DemoLayoutLocals.EnableAllowOverlap)
                SetNextItemAllowOverlap();
            Selectable("Some Selectable", false);
            SameLine();
            SmallButton("++");

            TreePop();
        }
    }

    // C++: static void DemoWindowPopups() (imgui_demo.cpp 5421)
    public static void DemoWindowPopups()
    {
        if (!CollapsingHeader("Popups & Modal windows"))
            return;

        // The properties of popups windows are:
        // - They block normal mouse hovering detection outside them. (*)
        // - Unless modal, they can be closed by clicking anywhere outside them, or by pressing ESCAPE.
        // - Their visibility state (~bool) is held internally by Dear ImGui instead of being held by the programmer as
        //   we are used to with regular Begin() calls. User can manipulate the visibility state by calling OpenPopup().
        // (*) One can use IsItemHovered(ImGuiHoveredFlags_AllowWhenBlockedByPopup) to bypass it and detect hovering even
        //     when normally blocked by a popup.
        // Those three properties are connected. The library needs to hold their visibility state BECAUSE it can close
        // popups at any time.

        // Typical use for regular windows:
        //   bool my_tool_is_active = false; if (ImGui::Button("Open")) my_tool_is_active = true; [...] if (my_tool_is_active) Begin("My Tool", &my_tool_is_active) { [...] } End();
        // Typical use for popups:
        //   if (ImGui::Button("Open")) ImGui::OpenPopup("MyPopup"); if (ImGui::BeginPopup("MyPopup")) { [...] EndPopup(); }

        // With popups we have to go through a library call (here OpenPopup) to manipulate the visibility state.
        // This may be a bit confusing at first but it should quickly make sense. Follow on the examples below.

        if (TreeNode("Popups"))
        {
            TextWrapped(
                "When a popup is active, it inhibits interacting with windows that are behind the popup. " +
                "Clicking outside the popup closes it.");

            string[] names = { "Bream", "Haddock", "Mackerel", "Pollock", "Tilefish" };

            // Simple selection popup (if you want to show the current selection inside the Button itself,
            // you may want to build a string using the "###" operator to preserve a constant ID with a variable label)
            if (Button("Select.."))
                OpenPopup("my_select_popup");
            SameLine();
            TextUnformatted(DemoPopupsLocals.SelectedFish == -1 ? "<None>" : names[DemoPopupsLocals.SelectedFish]);
            if (BeginPopup("my_select_popup"))
            {
                SeparatorText("Aquarium");
                for (int i = 0; i < names.Length; i++)
                    if (Selectable(names[i]))
                        DemoPopupsLocals.SelectedFish = i;
                EndPopup();
            }

            // Showing a menu with toggles
            if (Button("Toggle.."))
                OpenPopup("my_toggle_popup");
            if (BeginPopup("my_toggle_popup"))
            {
                for (int i = 0; i < names.Length; i++)
                    MenuItem(names[i], "", ref DemoPopupsLocals.Toggles[i]);
                if (BeginMenu("Sub-menu"))
                {
                    MenuItem("Click me");
                    EndMenu();
                }

                Separator();
                Text("Tooltip here");
                SetItemTooltip("I am a tooltip over a popup");

                if (Button("Stacked Popup"))
                    OpenPopup("another popup");
                if (BeginPopup("another popup"))
                {
                    for (int i = 0; i < names.Length; i++)
                        MenuItem(names[i], "", ref DemoPopupsLocals.Toggles[i]);
                    if (BeginMenu("Sub-menu"))
                    {
                        MenuItem("Click me");
                        if (Button("Stacked Popup"))
                            OpenPopup("another popup");
                        if (BeginPopup("another popup"))
                        {
                            Text("I am the last one here.");
                            EndPopup();
                        }
                        EndMenu();
                    }
                    EndPopup();
                }
                EndPopup();
            }

            // Call the more complete ShowExampleMenuFile which we use in various places of this demo
            if (Button("With a menu.."))
                OpenPopup("my_file_popup");
            if (BeginPopup("my_file_popup", ImGuiWindowFlags_MenuBar))
            {
                if (BeginMenuBar())
                {
                    if (BeginMenu("File"))
                    {
                        ShowExampleMenuFile();
                        EndMenu();
                    }
                    if (BeginMenu("Edit"))
                    {
                        MenuItem("Dummy");
                        EndMenu();
                    }
                    EndMenuBar();
                }
                Text("Hello from popup!");
                Button("This is a dummy button..");
                EndPopup();
            }

            TreePop();
        }

        if (TreeNode("Context menus"))
        {
            HelpMarker("\"Context\" functions are simple helpers to associate a Popup to a given Item or Window identifier.");

            // BeginPopupContextItem() is a helper to provide common/simple popup behavior of essentially doing:
            //     if (id == 0)
            //         id = GetItemID(); // Use last item id
            //     if (IsItemHovered() && IsMouseReleased(ImGuiMouseButton_Right))
            //         OpenPopup(id);
            //     return BeginPopup(id);
            // For advanced uses you may want to replicate and customize this code.
            // See more details in BeginPopupContextItem().

            // Example 1
            // When used after an item that has an ID (e.g. Button), we can skip providing an ID to BeginPopupContextItem(),
            // and BeginPopupContextItem() will use the last item ID as the popup ID.
            {
                string[] names = { "Label1", "Label2", "Label3", "Label4", "Label5" };
                for (int n = 0; n < 5; n++)
                {
                    if (Selectable(names[n], DemoPopupsLocals.Selected == n))
                        DemoPopupsLocals.Selected = n;
                    if (BeginPopupContextItem()) // <-- use last item id as popup id
                    {
                        DemoPopupsLocals.Selected = n;
                        Text("This is a popup for \"%s\"!", names[n]);
                        if (Button("Close"))
                            CloseCurrentPopup();
                        EndPopup();
                    }
                    SetItemTooltip("Right-click to open popup");
                }
            }

            // Example 2
            // Popup on a Text() element which doesn't have an identifier: we need to provide an identifier to BeginPopupContextItem().
            // Using an explicit identifier is also convenient if you want to activate the popups from different locations.
            {
                HelpMarker("Text() elements don't have stable identifiers so we need to provide one.");
                Text("Value = %.3f <-- (1) right-click this text", DemoPopupsLocals.Value);
                if (BeginPopupContextItem("my popup"))
                {
                    if (Selectable("Set to zero")) DemoPopupsLocals.Value = 0.0f;
                    if (Selectable("Set to PI")) DemoPopupsLocals.Value = 3.1415f;
                    SetNextItemWidth(-float.Epsilon /* -FLT_MIN */);
                    DragFloat("##Value", ref DemoPopupsLocals.Value, 0.1f, 0.0f, 0.0f);
                    EndPopup();
                }

                // We can also use OpenPopupOnItemClick() to toggle the visibility of a given popup.
                // Here we make it that right-clicking this other text element opens the same popup as above.
                // The popup itself will be submitted by the code above.
                Text("(2) Or right-click this text");
                OpenPopupOnItemClick("my popup", ImGuiPopupFlags_MouseButtonRight);

                // Back to square one: manually open the same popup.
                if (Button("(3) Or click this button"))
                    OpenPopup("my popup");
            }

            // Example 3
            // When using BeginPopupContextItem() with an implicit identifier (NULL == use last item ID),
            // we need to make sure your item identifier is stable.
            // In this example we showcase altering the item label while preserving its identifier, using the ### operator (see FAQ).
            {
                HelpMarker("Showcase using a popup ID linked to item ID, with the item having a changing label + stable ID using the ### operator.");
                // C++: char buf[64]; sprintf(buf, "Button: %s###Button", name); // ### operator override ID ignoring the preceding label
                Button(ImFmt.Sprintf("Button: %s###Button", DemoPopupsLocals.Name));
                if (BeginPopupContextItem())
                {
                    Text("Edit name:");
                    InputText("##edit", ref DemoPopupsLocals.Name, 32); // C++: IM_COUNTOF(name) == 32
                    if (Button("Close"))
                        CloseCurrentPopup();
                    EndPopup();
                }
                SameLine(); Text("(<-- right-click here)");
            }

            TreePop();
        }

        if (TreeNode("Modals"))
        {
            TextWrapped("Modal windows are like popups but the user cannot close them by clicking outside.");

            if (Button("Delete.."))
                OpenPopup("Delete?");

            // Always center this window when appearing
            ImVec2 center = GetMainViewport().GetCenter();
            SetNextWindowPos(center, ImGuiCond_Appearing, new ImVec2(0.5f, 0.5f));

            if (BeginPopupModal("Delete?", ImGuiWindowFlags_AlwaysAutoResize))
            {
                Text("All those beautiful files will be deleted.\nThis operation cannot be undone!");
                Separator();

                //static int unused_i = 0;
                //Combo("Combo", ref unused_i, "Delete\0Delete harder\0");

                PushStyleVar(ImGuiStyleVar_FramePadding, new ImVec2(0, 0));
                Checkbox("Don't ask me next time", ref DemoPopupsLocals.DontAskMeNextTime);
                PopStyleVar();

                if (Button("OK", new ImVec2(120, 0))) { CloseCurrentPopup(); }
                SetItemDefaultFocus();
                SameLine();
                if (Button("Cancel", new ImVec2(120, 0))) { CloseCurrentPopup(); }
                EndPopup();
            }

            if (Button("Stacked modals.."))
                OpenPopup("Stacked 1");
            if (BeginPopupModal("Stacked 1", ImGuiWindowFlags_MenuBar))
            {
                if (BeginMenuBar())
                {
                    if (BeginMenu("File"))
                    {
                        if (MenuItem("Some menu item")) { }
                        EndMenu();
                    }
                    EndMenuBar();
                }
                Text("Hello from Stacked The First\nUsing style.Colors[ImGuiCol_ModalWindowDimBg] behind it.");

                // Testing behavior of widgets stacking their own regular popups over the modal.
                Combo("Combo", ref DemoPopupsLocals.Item, "aaaa\0bbbb\0cccc\0dddd\0eeee\0\0");
                ColorEdit4("Color", DemoPopupsLocals.Color);

                if (Button("Add another modal.."))
                    OpenPopup("Stacked 2");

                // Also demonstrate passing a bool* to BeginPopupModal(), this will create a regular close button which
                // will close the popup. Note that the visibility state of popups is owned by imgui, so the input value
                // of the bool actually doesn't matter here.
                bool unused_open = true;
                if (BeginPopupModal("Stacked 2", ref unused_open))
                {
                    Text("Hello from Stacked The Second!");
                    ColorEdit4("Color", DemoPopupsLocals.Color); // Allow opening another nested popup
                    if (Button("Close"))
                        CloseCurrentPopup();
                    EndPopup();
                }

                if (Button("Close"))
                    CloseCurrentPopup();
                EndPopup();
            }

            TreePop();
        }

        if (TreeNode("Menus inside a regular window"))
        {
            TextWrapped("Below we are testing adding menu items to a regular window. It's rather unusual but should work!");
            Separator();

            MenuItem("Menu item", "Ctrl+M");
            if (BeginMenu("Menu inside a regular window"))
            {
                ShowExampleMenuFile();
                EndMenu();
            }
            Separator();
            TreePop();
        }
    }

    // C++: ImColor::HSV(h, s, v, a = 1.0f) (imgui.h) — helper missing from the C# port.
    // Converts to an ImVec4 so it can be fed to PushStyleColor(ImGuiCol, ImVec4).
    private static ImVec4 DemoLP_ImColorHSV(float h, float s, float v)
    {
        ColorConvertHSVtoRGB(h, s, v, out float r, out float g, out float b);
        return new ImVec4(r, g, b, 1.0f);
    }
}
