// Port of imgui_demo.cpp [SECTION] Example App: Custom Rendering using ImDrawList API (10223-10526),
// [SECTION] Example App: Documents Handling / ShowExampleAppDocuments() (10528-10804) including
// MyDocument (10534-10560) and ExampleAppDocuments (10562-10650),
// [SECTION] Example App: Assets Browser / ShowExampleAppAssetsBrowser() (10806-11284) including
// ExampleAsset (10819-10855) and ExampleAssetsBrowser (10857-11275).
// C++ function-local 'static' variables are hosted in the file-local DemoApps3Locals class below.
// NOTE: the temporary stubs ShowExampleAppCustomRendering/ShowExampleAppDocuments/ShowExampleAppAssetsBrowser
// in ImGuiDemoStubs.cs must be removed for this file to compile (CS0111 duplicates otherwise).
using System;
using System.Collections.Generic;
using System.Text;
using static ImGui.ImGui; // for unqualified ImGui.* calls inside namespace-level Example classes

namespace ImGui;

// Function-local statics of the example app functions ported in this file.
file static class DemoApps3Locals
{
    // ShowExampleAppCustomRendering() - "Primitives" tab
    public static float Sz = 42.0f;                                   // C++: static float sz = 42.0f
    public static float Thickness = 3.0f;                             // C++: static float thickness = 3.0f
    public static int NgonSides = 6;                                  // C++: static int ngon_sides = 6
    public static bool CircleSegmentsOverride = false;                // C++: static bool circle_segments_override
    public static int CircleSegmentsOverrideV = 12;                   // C++: static int circle_segments_override_v = 12
    public static bool CurveSegmentsOverride = false;                 // C++: static bool curve_segments_override
    public static int CurveSegmentsOverrideV = 8;                     // C++: static int curve_segments_override_v = 8
    public static float[] Colf = { 1.0f, 1.0f, 0.4f, 1.0f };          // C++: static ImVec4 colf = ImVec4(1.0f, 1.0f, 0.4f, 1.0f) (float[] so ColorEdit4() can edit it live)

    // ShowExampleAppCustomRendering() - "Canvas" tab
    public static ImVector<ImVec2> Points = new ImVector<ImVec2>();   // C++: static ImVector<ImVec2> points
    public static ImVec2 Scrolling = new ImVec2(0.0f, 0.0f);          // C++: static ImVec2 scrolling(0.0f, 0.0f)
    public static bool OptEnableGrid = true;                          // C++: static bool opt_enable_grid = true
    public static bool OptEnableContextMenu = true;                   // C++: static bool opt_enable_context_menu = true
    public static bool AddingLine = false;                            // C++: static bool adding_line = false

    // ShowExampleAppCustomRendering() - "BG/FG draw lists" tab
    public static bool DrawBg = true;                                 // C++: static bool draw_bg = true
    public static bool DrawFg = true;                                 // C++: static bool draw_fg = true

    // ShowExampleAppDocuments()
    public static ExampleAppDocuments App = new ExampleAppDocuments(); // C++: static ExampleAppDocuments app
    public static bool OptReorderable = true;                          // C++: static bool opt_reorderable = true
    public static ImGuiTabBarFlags OptFittingFlags = ImGuiTabBarFlags_FittingPolicyDefault_; // C++: static ImGuiTabBarFlags opt_fitting_flags

    // ShowExampleAppAssetsBrowser()
    public static ExampleAssetsBrowser AssetsBrowser = new ExampleAssetsBrowser(); // C++: static ExampleAssetsBrowser assets_browser
}

// Simplified structure to mimic a Document model
// C++: struct MyDocument (imgui_demo.cpp 10534-10560)
public class MyDocument
{
    public const int NameSize = 32;                          // C++: char Name[32]
    public byte[] Name = new byte[NameSize];                 // Document title
    public int UID;                                          // Unique ID (necessary as we can change title)
    public bool Open;                                        // Set when open (we keep an array of all available documents to simplify demo code!)
    public bool OpenPrev;                                    // Copy of Open from last update.
    public bool Dirty;                                       // Set when the document has been modified
    public float[] Color = new float[4];                     // An arbitrary variable associated to the document (C++ ImVec4; float[] so ColorEdit3() can edit it live)

    // C++: MyDocument(int uid, const char* name, bool open = true, const ImVec4& color = ImVec4(1.0f, 1.0f, 1.0f, 1.0f))
    public MyDocument(int uid, string name, bool open = true, ImVec4? color = null)
    {
        UID = uid;
        SetName(name); // C++: snprintf(Name, sizeof(Name), "%s", name)
        Open = OpenPrev = open;
        Dirty = false;
        ImVec4 c = color ?? new ImVec4(1.0f, 1.0f, 1.0f, 1.0f);
        Color[0] = c.x; Color[1] = c.y; Color[2] = c.z; Color[3] = c.w;
    }

    // C++ snprintf(Name, sizeof(Name), "%s", name) equivalent (zero-terminated, truncating).
    public void SetName(string name)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        int copy = Math.Min(bytes.Length, NameSize - 1);
        Array.Clear(Name, 0, NameSize);
        Array.Copy(bytes, 0, Name, 0, copy);
    }

    // Read back the zero-terminated title (C++ reads char Name[32] as a C string).
    public string NameStr
    {
        get
        {
            int len = Array.IndexOf(Name, (byte)0);
            if (len < 0) len = NameSize;
            return Encoding.UTF8.GetString(Name, 0, len);
        }
    }

    public void DoOpen() { Open = true; }
    public void DoForceClose() { Open = false; Dirty = false; }
    public void DoSave() { Dirty = false; }
}

// C++: struct ExampleAppDocuments (imgui_demo.cpp 10562-10650)
public class ExampleAppDocuments
{
    public ImVector<MyDocument> Documents = new ImVector<MyDocument>();
    public ImVector<MyDocument> CloseQueue = new ImVector<MyDocument>();
    public MyDocument RenamingDoc = null;
    public bool RenamingStarted = false;

    public ExampleAppDocuments()
    {
        Documents.push_back(new MyDocument(0, "Lettuce", true, new ImVec4(0.4f, 0.8f, 0.4f, 1.0f)));
        Documents.push_back(new MyDocument(1, "Eggplant", true, new ImVec4(0.8f, 0.5f, 1.0f, 1.0f)));
        Documents.push_back(new MyDocument(2, "Carrot", true, new ImVec4(1.0f, 0.8f, 0.5f, 1.0f)));
        Documents.push_back(new MyDocument(3, "Tomato", false, new ImVec4(1.0f, 0.3f, 0.4f, 1.0f)));
        Documents.push_back(new MyDocument(4, "A Rather Long Title", false, new ImVec4(0.4f, 0.8f, 0.8f, 1.0f)));
        Documents.push_back(new MyDocument(5, "Some Document", false, new ImVec4(0.8f, 0.8f, 1.0f, 1.0f)));
    }

    // As we allow to change document name, we append a never-changing document ID so tabs are stable
    // C++: void GetTabName(MyDocument* doc, char* out_buf, size_t out_buf_size) -> snprintf("%s###doc%d", doc->Name, doc->UID)
    public string GetTabName(MyDocument doc)
    {
        return ImFmt.Sprintf("%s###doc%d", doc.NameStr, doc.UID);
    }

    // Display placeholder contents for the Document
    public void DisplayDocContents(MyDocument doc)
    {
        ImGui.PushID(doc);
        ImGui.Text("Document \"%s\"", doc.NameStr);
        ImGui.PushStyleColor(ImGuiCol_Text, new ImVec4(doc.Color[0], doc.Color[1], doc.Color[2], doc.Color[3]));
        ImGui.TextWrapped("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.");
        ImGui.PopStyleColor();

        ImGui.SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_R, ImGuiInputFlags_Tooltip);
        if (ImGui.Button("Rename.."))
        {
            RenamingDoc = doc;
            RenamingStarted = true;
        }
        ImGui.SameLine();

        ImGui.SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_M, ImGuiInputFlags_Tooltip);
        if (ImGui.Button("Modify"))
            doc.Dirty = true;

        ImGui.SameLine();
        ImGui.SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_S, ImGuiInputFlags_Tooltip);
        if (ImGui.Button("Save"))
            doc.DoSave();

        ImGui.SameLine();
        ImGui.SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_W, ImGuiInputFlags_Tooltip);
        if (ImGui.Button("Close"))
            CloseQueue.push_back(doc);
        ImGui.ColorEdit3("color", doc.Color);  // Useful to test drag and drop and hold-dragged-to-open-tab behavior.
        ImGui.PopID();
    }

    // Display context menu for the Document
    public void DisplayDocContextMenu(MyDocument doc)
    {
        if (!ImGui.BeginPopupContextItem())
            return;

        // C++: char buf[256]; sprintf(buf, "Save %s", doc->Name);
        if (ImGui.MenuItem(ImFmt.Sprintf("Save %s", doc.NameStr), "Ctrl+S", false, doc.Open))
            doc.DoSave();
        if (ImGui.MenuItem("Rename...", "Ctrl+R", false, doc.Open))
            RenamingDoc = doc;
        if (ImGui.MenuItem("Close", "Ctrl+W", false, doc.Open))
            CloseQueue.push_back(doc);
        ImGui.EndPopup();
    }

    // [Optional] Notify the system of Tabs/Windows closure that happened outside the regular tab interface.
    // If a tab has been closed programmatically (aka closed from another source such as the Checkbox() in the demo,
    // as opposed to clicking on the regular tab closing button) and stops being submitted, it will take a frame for
    // the tab bar to notice its absence. During this frame there will be a gap in the tab bar, and if the tab that has
    // disappeared was the selected one, the tab bar will report no selected tab during the frame. This will effectively
    // give the impression of a flicker for one frame.
    // We call SetTabItemClosed() to manually notify the Tab Bar or Docking system of removed tabs to avoid this glitch.
    // Note that this completely optional, and only affect tab bars with the ImGuiTabBarFlags_Reorderable flag.
    public void NotifyOfDocumentsClosedElsewhere()
    {
        for (int doc_n = 0; doc_n < Documents.Size; doc_n++)
        {
            MyDocument doc = Documents[doc_n];
            if (!doc.Open && doc.OpenPrev)
                ImGui.SetTabItemClosed(doc.NameStr);
            doc.OpenPrev = doc.Open;
        }
    }
}

// C++: struct ExampleAsset (imgui_demo.cpp 10819-10855)
public class ExampleAsset
{
    public ImGuiID ID;
    public int Type;

    public ExampleAsset(ImGuiID id, int type) { ID = id; Type = type; }

    public static ImGuiTableSortSpecs s_current_sort_specs = null;

    // C++: static void SortWithSortSpecs(ImGuiTableSortSpecs* sort_specs, ExampleAsset* items, int items_count)
    public static void SortWithSortSpecs(ImGuiTableSortSpecs sort_specs, ExampleAsset[] items, int items_count)
    {
        s_current_sort_specs = sort_specs; // Store in variable accessible by the sort function.
        if (items_count > 1)
            Array.Sort(items, 0, items_count, CompareWithSortSpecs); // C++: qsort(items, ..., ExampleAsset::CompareWithSortSpecs)
        s_current_sort_specs = null;
    }

    // Compare function to be used by qsort()
    // C++: static int IMGUI_CDECL CompareWithSortSpecs(const void* lhs, const void* rhs)
    public static readonly Comparer<ExampleAsset> CompareWithSortSpecs = Comparer<ExampleAsset>.Create((ExampleAsset a, ExampleAsset b) =>
    {
        for (int n = 0; n < s_current_sort_specs.SpecsCount; n++)
        {
            ImGuiTableColumnSortSpecs sort_spec = s_current_sort_specs.Specs[n];
            int delta = 0;
            if (sort_spec.ColumnIndex == 0)
                delta = ((int)a.ID - (int)b.ID);
            else if (sort_spec.ColumnIndex == 1)
                delta = (a.Type - b.Type);
            if (delta > 0)
                return (sort_spec.SortDirection == ImGuiSortDirection_Ascending) ? +1 : -1;
            if (delta < 0)
                return (sort_spec.SortDirection == ImGuiSortDirection_Ascending) ? -1 : +1;
        }
        return (int)a.ID - (int)b.ID;
    });
}

// C++: struct ExampleAssetsBrowser (imgui_demo.cpp 10857-11275)
public class ExampleAssetsBrowser
{
    // Options
    public bool ShowTypeOverlay = true;
    public bool AllowSorting = true;
    public bool AllowBoxSelect = true;                  // Will set ImGuiMultiSelectFlags_BoxSelect2d
    public bool AllowBoxSelectInsideSelection = false;  // Will set ImGuiMultiSelectFlags_SelectOnClickAlways
    public bool AllowDragUnselected = false;            // Will set ImGuiMultiSelectFlags_SelectOnClickRelease
    public float IconSize = 0;
    public int IconSpacing = 10;
    public int IconHitSpacing = 4;                      // Increase hit-spacing if you want to make it possible to clear or box-select from gaps. Some spacing is required to able to amend with Shift+box-select. Value is small in Explorer.
    public bool StretchSpacing = true;
    public bool UseScrollX = false;                     // Debug: submit twice the number of items per line (overflow horizontally to exercise ScrollX + box-select)

    // State
    public ImVector<ExampleAsset> Items = new ImVector<ExampleAsset>(); // Our items
    public ExampleSelectionWithDeletion Selection = new ExampleSelectionWithDeletion(); // Our selection (ImGuiSelectionBasicStorage + helper funcs to handle deletion)
    public ImGuiID NextItemId = 0;                      // Unique identifier when creating new items
    public bool RequestDelete = false;                  // Deferred deletion request
    public bool RequestSort = false;                    // Deferred sort request
    public float ZoomWheelAccum = 0.0f;                 // Mouse wheel accumulator to handle smooth wheels better

    // Calculated sizes for layout, output of UpdateLayoutSizes(). Could be locals but our code is simpler this way.
    public ImVec2 LayoutItemSize;
    public ImVec2 LayoutItemStep;                       // == LayoutItemSize + LayoutItemSpacing
    public float LayoutItemSpacing = 0.0f;
    public float LayoutSelectableSpacing = 0.0f;
    public float LayoutOuterPadding = 0.0f;
    public int LayoutColumnCount = 0;
    public int LayoutLineCount = 0;

    // Functions
    public ExampleAssetsBrowser()
    {
        AddItems(10000);
    }

    public void AddItems(int count)
    {
        if (Items.Size == 0)
            NextItemId = 0;
        Items.reserve(Items.Size + count);
        for (int n = 0; n < count; n++, NextItemId++)
            Items.push_back(new ExampleAsset(NextItemId, (NextItemId % 20) < 15 ? 0 : ((NextItemId % 20) < 18 ? 1 : 2)));
        RequestSort = true;
    }

    public void ClearItems()
    {
        Items.clear();
        Selection.Clear();
    }

    // Logic would be written in the main code BeginChild() and outputting to local variables.
    // We extracted it into a function so we can call it easily from multiple places.
    public void UpdateLayoutSizes(float avail_width)
    {
        // Layout: when not stretching: allow extending into right-most spacing.
        LayoutItemSpacing = (float)IconSpacing;
        if (StretchSpacing == false)
            avail_width += ImMath.ImFloor(LayoutItemSpacing * 0.5f);

        // Layout: calculate number of icon per line and number of lines
        LayoutItemSize = new ImVec2(ImMath.ImFloor(IconSize), ImMath.ImFloor(IconSize));
        LayoutColumnCount = ImMath.ImMax((int)(avail_width / (LayoutItemSize.x + LayoutItemSpacing)), 1);

        // Layout: when stretching: allocate remaining space to more spacing. Round before division, so item_spacing may be non-integer.
        if (StretchSpacing && LayoutColumnCount > 1)
            LayoutItemSpacing = ImMath.ImFloor(avail_width - LayoutItemSize.x * LayoutColumnCount) / LayoutColumnCount;

        if (UseScrollX)
            LayoutColumnCount *= 2;
        LayoutLineCount = (Items.Size + LayoutColumnCount - 1) / LayoutColumnCount;

        LayoutItemStep = new ImVec2(LayoutItemSize.x + LayoutItemSpacing, LayoutItemSize.y + LayoutItemSpacing);
        LayoutSelectableSpacing = ImMath.ImMax(ImMath.ImFloor(LayoutItemSpacing) - IconHitSpacing, 0.0f);
        LayoutOuterPadding = ImMath.ImFloor(LayoutItemSpacing * 0.5f);
    }

    // C++: void Draw(const char* title, bool* p_open)
    public void Draw(string title, ref bool p_open)
    {
        if (IconSize <= 0.0f)
            IconSize = ImGui.CalcTextSize("99999").x;

        ImGui.SetNextWindowSize(new ImVec2(IconSize * 25, IconSize * 15), ImGuiCond_FirstUseEver);
        if (!ImGui.Begin(title, ref p_open, ImGuiWindowFlags_MenuBar))
        {
            ImGui.End();
            return;
        }

        // Menu bar
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Add 10000 items"))
                    AddItems(10000);
                if (ImGui.MenuItem("Clear items"))
                    ClearItems();
                ImGui.Separator();
                if (ImGui.MenuItem("Close", null, false, true)) // C++: p_open != NULL (always true with a ref parameter)
                    p_open = false;
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Delete", "Del", false, Selection.Size > 0))
                    RequestDelete = true;
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Options"))
            {
                ImGui.PushItemWidth(ImGui.GetFontSize() * 10);

                ImGui.SeparatorText("Contents");
                ImGui.Checkbox("Show Type Overlay", ref ShowTypeOverlay);
                ImGui.Checkbox("Allow Sorting", ref AllowSorting);

                ImGui.SeparatorText("Selection Behavior");
                ImGui.Checkbox("Allow box-selection", ref AllowBoxSelect);
                if (ImGui.Checkbox("Allow box-selection from selected items", ref AllowBoxSelectInsideSelection) && AllowBoxSelectInsideSelection)
                    AllowDragUnselected = false;
                if (ImGui.Checkbox("Allow dragging unselected item", ref AllowDragUnselected) && AllowDragUnselected)
                    AllowBoxSelectInsideSelection = false;

                ImGui.SeparatorText("Layout");
                ImGui.SliderFloat("Icon Size", ref IconSize, 16.0f, 128.0f, "%.0f");
                ImGui.SameLine(); ImGui.HelpMarker("Use Ctrl+Wheel to zoom");
                ImGui.SliderInt("Icon Spacing", ref IconSpacing, 0, 32);
                ImGui.SliderInt("Icon Hit Spacing", ref IconHitSpacing, 0, 32);
                ImGui.Checkbox("Stretch Spacing", ref StretchSpacing);
                ImGui.Checkbox("Use ScrollX", ref UseScrollX);
                ImGui.PopItemWidth();
                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }

        // Show a table with ONLY one header row to showcase the idea/possibility of using this to provide a sorting UI
        if (AllowSorting)
        {
            ImGui.PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(0, 0));
            ImGuiTableFlags table_flags_for_sort_specs = ImGuiTableFlags_Sortable | ImGuiTableFlags_SortMulti | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_Borders;
            if (ImGui.BeginTable("for_sort_specs_only", 2, table_flags_for_sort_specs, new ImVec2(0.0f, ImGui.GetFrameHeight())))
            {
                ImGui.TableSetupColumn("Index");
                ImGui.TableSetupColumn("Type");
                ImGui.TableHeadersRow();
                ImGuiTableSortSpecs sort_specs = ImGui.TableGetSortSpecs();
                if (sort_specs != null)
                    if (sort_specs.SpecsDirty || RequestSort)
                    {
                        ExampleAsset.SortWithSortSpecs(sort_specs, Items.Data, Items.Size);
                        sort_specs.SpecsDirty = false;
                        RequestSort = false;
                    }
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
        }

        ImGuiIO io = ImGui.GetIO();
        ImGui.SetNextWindowContentSize(new ImVec2(0.0f, LayoutOuterPadding + LayoutLineCount * (LayoutItemSize.y + LayoutItemSpacing)));
        if (ImGui.BeginChild("Assets", new ImVec2(0.0f, -ImGui.GetTextLineHeightWithSpacing()), ImGuiChildFlags_Borders, ImGuiWindowFlags_NoMove | ImGuiWindowFlags_HorizontalScrollbar))
        {
            ImDrawList draw_list = ImGui.GetWindowDrawList();

            float avail_width = ImGui.GetContentRegionAvail().x;
            UpdateLayoutSizes(avail_width);

            // Calculate and store start position.
            ImVec2 start_pos = ImGui.GetCursorScreenPos();
            start_pos = new ImVec2(start_pos.x + LayoutOuterPadding, start_pos.y + LayoutOuterPadding);
            ImGui.SetCursorScreenPos(start_pos);

            // Multi-select
            ImGuiMultiSelectFlags ms_flags = ImGuiMultiSelectFlags_ClearOnEscape | ImGuiMultiSelectFlags_ClearOnClickVoid;

            // - Enable box-select (in 2D mode, so that changing box-select rectangle X1/X2 boundaries will affect clipped items)
            if (AllowBoxSelect)
                ms_flags |= ImGuiMultiSelectFlags_BoxSelect2d;

            // - Selection mode
            if (AllowDragUnselected)
                ms_flags |= ImGuiMultiSelectFlags_SelectOnClickRelease; // Rarely used: Allows dragging an unselected item without selecting it(rarely used)
            else if (AllowBoxSelectInsideSelection)
                ms_flags |= ImGuiMultiSelectFlags_SelectOnClickAlways; // Rarely used: Prevents Drag and Drop from being used on multiple-selection, but allows e.g. BoxSelect to always reselect even when clicking inside an existing selection.

            // - Enable keyboard wrapping on X axis
            // (FIXME-MULTISELECT: We haven't designed/exposed a general nav wrapping api yet, so this flag is provided as a courtesy to avoid doing:
            //    ImGui::NavMoveRequestTryWrapping(ImGui::GetCurrentWindow(), ImGuiNavMoveFlags_WrapX);
            // When we finish implementing a more general API for this, we will obsolete this flag in favor of the new system)
            ms_flags |= ImGuiMultiSelectFlags_NavWrapX;

            ImGuiMultiSelectIO ms_io = ImGui.BeginMultiSelect(ms_flags, Selection.Size, Items.Size);

            // Use custom selection adapter: store ID in selection (recommended)
            Selection.UserData = this;
            Selection.AdapterIndexToStorageId = (ImGuiSelectionBasicStorage self_, int idx) => ((ExampleAssetsBrowser)self_.UserData).Items[idx].ID;
            Selection.ApplyRequests(ms_io);

            bool want_delete = (ImGui.Shortcut(ImGuiKey_Delete, ImGuiInputFlags_Repeat) && (Selection.Size > 0)) || RequestDelete;
            int item_curr_idx_to_focus = want_delete ? Selection.ApplyDeletionPreLoop(ms_io, Items.Size) : -1;
            RequestDelete = false;

            // Push LayoutSelectableSpacing (which is LayoutItemSpacing minus hit-spacing, if we decide to have hit gaps between items)
            // Altering style ItemSpacing may seem unnecessary as we position every items using SetCursorScreenPos()...
            // But it is necessary for two reasons:
            // - Selectables uses it by default to visually fill the space between two items.
            // - The vertical spacing would be measured by Clipper to calculate line height if we didn't provide it explicitly (here we do).
            ImGui.PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(LayoutSelectableSpacing, LayoutSelectableSpacing));

            // Rendering parameters
            ImU32[] icon_type_overlay_colors = { 0, ImGuiCore.IM_COL32(200, 70, 70, 255), ImGuiCore.IM_COL32(70, 170, 70, 255) };
            ImU32 icon_bg_color = ImGui.GetColorU32(ImGuiCore.IM_COL32(35, 35, 35, 220));
            ImVec2 icon_type_overlay_size = new ImVec2(4.0f, 4.0f);
            bool display_label = (LayoutItemSize.x >= ImGui.CalcTextSize("999").x);

            int column_count = LayoutColumnCount;
            ImGuiListClipper clipper = new ImGuiListClipper();
            clipper.Begin(LayoutLineCount, LayoutItemStep.y);
            if (item_curr_idx_to_focus != -1)
                clipper.IncludeItemByIndex(item_curr_idx_to_focus / column_count); // Ensure focused item line is not clipped.
            if (ms_io.RangeSrcItem != -1)
                clipper.IncludeItemByIndex((int)ms_io.RangeSrcItem / column_count); // Ensure RangeSrc item line is not clipped.
            while (clipper.Step())
            {
                for (int line_idx = clipper.DisplayStart; line_idx < clipper.DisplayEnd; line_idx++)
                {
                    int item_min_idx_for_current_line = line_idx * column_count;
                    int item_max_idx_for_current_line = ImMath.ImMin((line_idx + 1) * column_count, Items.Size);
                    for (int item_idx = item_min_idx_for_current_line; item_idx < item_max_idx_for_current_line; ++item_idx)
                    {
                        ExampleAsset item_data = Items[item_idx];
                        ImGui.PushID((int)item_data.ID);

                        // Position item
                        ImVec2 pos = new ImVec2(start_pos.x + (item_idx % column_count) * LayoutItemStep.x, start_pos.y + line_idx * LayoutItemStep.y);
                        ImGui.SetCursorScreenPos(pos);

                        ImGui.SetNextItemSelectionUserData(item_idx);
                        bool item_is_selected = Selection.Contains(item_data.ID);
                        bool item_is_visible = ImGui.IsRectVisible(LayoutItemSize);
                        ImGui.Selectable("", item_is_selected, ImGuiSelectableFlags_None, LayoutItemSize);

                        // Update our selection state immediately (without waiting for EndMultiSelect() requests)
                        // because we use this to alter the color of our text/icon.
                        if (ImGui.IsItemToggledSelection())
                            item_is_selected = !item_is_selected;

                        // Focus (for after deletion)
                        if (item_curr_idx_to_focus == item_idx)
                            ImGui.SetKeyboardFocusHere(-1);

                        // Drag and drop
                        if (ImGui.BeginDragDropSource())
                        {
                            // Create payload with full selection OR single unselected item.
                            // (the later is only possible when using ImGuiMultiSelectFlags_SelectOnClickRelease)
                            if (ImGui.GetDragDropPayload() == null)
                            {
                                ImVector<ImGuiID> payload_items = new ImVector<ImGuiID>();
                                int it = -1; // C++: void* it = NULL
                                ImGuiID id = 0;
                                if (!item_is_selected)
                                    payload_items.push_back(item_data.ID);
                                else
                                    while (Selection.GetNextSelectedItem(ref it, out id))
                                        payload_items.push_back(id);
                                byte[] payload_bytes = new byte[payload_items.Size * sizeof(ImGuiID)];
                                Buffer.BlockCopy(payload_items.Data, 0, payload_bytes, 0, payload_bytes.Length);
                                ImGui.SetDragDropPayload("ASSETS_BROWSER_ITEMS", payload_bytes, (ulong)payload_bytes.Length);
                            }

                            // Display payload content in tooltip, by extracting it from the payload data
                            // (we could read from selection, but it is more correct and reusable to read from payload)
                            ImGuiPayload payload = ImGui.GetDragDropPayload();
                            int payload_count = (int)payload.DataSize / (int)sizeof(ImGuiID);
                            ImGui.Text("%d assets", payload_count);

                            ImGui.EndDragDropSource();
                        }

                        // Render icon (a real app would likely display an image/thumbnail here)
                        // Because we use ImGuiMultiSelectFlags_BoxSelect2d, clipping vertical may occasionally be larger, so we coarse-clip our rendering as well.
                        if (item_is_visible)
                        {
                            ImVec2 box_min = new ImVec2(pos.x - 1, pos.y - 1);
                            ImVec2 box_max = new ImVec2(box_min.x + LayoutItemSize.x + 2, box_min.y + LayoutItemSize.y + 2); // Dubious
                            draw_list.AddRectFilled(box_min, box_max, icon_bg_color); // Background color
                            if (ShowTypeOverlay && item_data.Type != 0)
                            {
                                ImU32 type_col = icon_type_overlay_colors[item_data.Type % icon_type_overlay_colors.Length];
                                draw_list.AddRectFilled(new ImVec2(box_max.x - 2 - icon_type_overlay_size.x, box_min.y + 2), new ImVec2(box_max.x - 2, box_min.y + 2 + icon_type_overlay_size.y), type_col);
                            }
                            if (display_label)
                            {
                                ImU32 label_col = ImGui.GetColorU32(item_is_selected ? ImGuiCol_Text : ImGuiCol_TextDisabled);
                                string label = ImFmt.Sprintf("%d", item_data.ID);
                                draw_list.AddText(new ImVec2(box_min.x, box_max.y - ImGui.GetFontSize()), label_col, label);
                            }
                        }

                        ImGui.PopID();
                    }
                }
            }
            clipper.End();
            if (Items.Size == 0)
                ImGui.Dummy(new ImVec2(0, 0));
            ImGui.PopStyleVar(); // ImGuiStyleVar_ItemSpacing

            // Context menu
            if (ImGui.BeginPopupContextWindow())
            {
                ImGui.Text("Selection: %d items", Selection.Size);
                ImGui.Separator();
                if (ImGui.MenuItem("Delete", "Del", false, Selection.Size > 0))
                    RequestDelete = true;
                ImGui.EndPopup();
            }

            ms_io = ImGui.EndMultiSelect();
            Selection.ApplyRequests(ms_io);
            if (want_delete)
                Selection.ApplyDeletionPostLoop(ms_io, Items, item_curr_idx_to_focus);

            // Zooming with Ctrl+Wheel
            if (ImGui.IsWindowAppearing())
                ZoomWheelAccum = 0.0f;
            if (ImGui.IsWindowHovered() && io.MouseWheel != 0.0f && ImGui.IsKeyDown(ImGuiMod_Ctrl) && ImGui.IsAnyItemActive() == false)
            {
                ZoomWheelAccum += io.MouseWheel;
                if (MathF.Abs(ZoomWheelAccum) >= 1.0f)
                {
                    // Calculate hovered item index from mouse location
                    // FIXME: Locking aiming on 'hovered_item_idx' (with a cool-down timer) would ensure zoom keeps on it.
                    float hovered_item_nx = (io.MousePos.x - start_pos.x + LayoutItemSpacing * 0.5f) / LayoutItemStep.x;
                    float hovered_item_ny = (io.MousePos.y - start_pos.y + LayoutItemSpacing * 0.5f) / LayoutItemStep.y;
                    int hovered_item_idx = ((int)hovered_item_ny * LayoutColumnCount) + (int)hovered_item_nx;
                    //ImGui.SetTooltip("%f,%f -> item %d", hovered_item_nx, hovered_item_ny, hovered_item_idx); // Move those 4 lines in block above for easy debugging

                    // Zoom
                    IconSize *= MathF.Pow(1.1f, (int)ZoomWheelAccum);
                    IconSize = ImMath.ImClamp(IconSize, 16.0f, 128.0f);
                    ZoomWheelAccum -= (int)ZoomWheelAccum;
                    UpdateLayoutSizes(avail_width);

                    // Manipulate scroll to that we will land at the same Y location of currently hovered item.
                    // - Calculate next frame position of item under mouse
                    // - Set new scroll position to be used in next ImGui::BeginChild() call.
                    float hovered_item_rel_pos_y = ((float)(hovered_item_idx / LayoutColumnCount) + ImMath.ImFmod(hovered_item_ny, 1.0f)) * LayoutItemStep.y;
                    hovered_item_rel_pos_y += ImGui.GetStyle().WindowPadding.y;
                    float mouse_local_y = io.MousePos.y - ImGui.GetWindowPos().y;
                    ImGui.SetScrollY(hovered_item_rel_pos_y - mouse_local_y);
                }
            }
        }
        ImGui.EndChild();

        ImGui.Text("Selected: %d/%d items", Selection.Size, Items.Size);
        ImGui.End();
    }
}

public static partial class ImGui
{
    // Add a |_| looking shape
    // C++: static void PathConcaveShape(ImDrawList* draw_list, float x, float y, float sz) (imgui_demo.cpp 10231-10236)
    private static void DemoA3_PathConcaveShape(ImDrawList draw_list, float x, float y, float sz)
    {
        ImVec2[] pos_norms =
        {
            new ImVec2(0.0f, 0.0f), new ImVec2(0.3f, 0.0f), new ImVec2(0.3f, 0.7f), new ImVec2(0.7f, 0.7f),
            new ImVec2(0.7f, 0.0f), new ImVec2(1.0f, 0.0f), new ImVec2(1.0f, 1.0f), new ImVec2(0.0f, 1.0f)
        };
        for (int i = 0; i < pos_norms.Length; i++)
        {
            ImVec2 pn = pos_norms[i];
            draw_list.PathLineTo(new ImVec2(x + 0.5f + (int)(sz * pn.x), y + 0.5f + (int)(sz * pn.y)));
        }
    }

    // Demonstrate using the low-level ImDrawList to draw custom shapes.
    // C++: static void ShowExampleAppCustomRendering(bool* p_open) (imgui_demo.cpp 10249-10526)
    public static void ShowExampleAppCustomRendering(ref bool p_open)
    {
        if (!ImGui.Begin("Example: Custom rendering", ref p_open))
        {
            ImGui.End();
            return;
        }

        // Tip: If you do a lot of custom rendering, you probably want to use your own geometrical types and benefit of
        // overloaded operators, etc. Define IM_VEC2_CLASS_EXTRA in imconfig.h to create implicit conversions between your
        // types and ImVec2/ImVec4. Dear ImGui defines overloaded operators but they are internal to imgui.cpp and not
        // exposed outside (to avoid messing with your types) In this example we are not using the maths operators!

        if (ImGui.BeginTabBar("##TabBar"))
        {
            if (ImGui.BeginTabItem("Primitives"))
            {
                ImGui.PushItemWidth(-ImGui.GetFontSize() * 15);
                ImGui.PushItemFlag(ImGuiItemFlags_LiveEditOnInput, true);
                ImDrawList draw_list = ImGui.GetWindowDrawList();

                // Draw gradients
                // (note that those are currently exacerbating our sRGB/Linear issues)
                // Calling ImGui::GetColorU32() multiplies the given colors by the current Style Alpha, but you may pass the IM_COL32() directly as well..
                ImGui.Text("Gradients");
                ImVec2 gradient_size = new ImVec2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());
                {
                    ImVec2 p0 = ImGui.GetCursorScreenPos();
                    ImVec2 p1 = new ImVec2(p0.x + gradient_size.x, p0.y + gradient_size.y);
                    ImU32 col_a = ImGui.GetColorU32(ImGuiCore.IM_COL32(0, 0, 0, 255));
                    ImU32 col_b = ImGui.GetColorU32(ImGuiCore.IM_COL32(255, 255, 255, 255));
                    draw_list.AddRectFilledMultiColor(p0, p1, col_a, col_b, col_b, col_a);
                    ImGui.InvisibleButton("##gradient1", gradient_size);
                }
                {
                    ImVec2 p0 = ImGui.GetCursorScreenPos();
                    ImVec2 p1 = new ImVec2(p0.x + gradient_size.x, p0.y + gradient_size.y);
                    ImU32 col_a = ImGui.GetColorU32(ImGuiCore.IM_COL32(0, 255, 0, 255));
                    ImU32 col_b = ImGui.GetColorU32(ImGuiCore.IM_COL32(255, 0, 0, 255));
                    draw_list.AddRectFilledMultiColor(p0, p1, col_a, col_b, col_b, col_a);
                    ImGui.InvisibleButton("##gradient2", gradient_size);
                }

                // Draw a bunch of primitives
                ImGui.Text("All primitives");
                ImGui.DragFloat("Size", ref DemoApps3Locals.Sz, 0.2f, 2.0f, 100.0f, "%.0f");
                ImGui.DragFloat("Thickness", ref DemoApps3Locals.Thickness, 0.05f, 1.0f, 8.0f, "%.02f");
                ImGui.SliderInt("N-gon sides", ref DemoApps3Locals.NgonSides, 3, 12);
                ImGui.Checkbox("##circlesegmentoverride", ref DemoApps3Locals.CircleSegmentsOverride);
                ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.x);
                DemoApps3Locals.CircleSegmentsOverride |= ImGui.SliderInt("Circle segments override", ref DemoApps3Locals.CircleSegmentsOverrideV, 3, 40);
                ImGui.Checkbox("##curvessegmentoverride", ref DemoApps3Locals.CurveSegmentsOverride);
                ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.x);
                DemoApps3Locals.CurveSegmentsOverride |= ImGui.SliderInt("Curves segments override", ref DemoApps3Locals.CurveSegmentsOverrideV, 3, 40);
                ImGui.ColorEdit4("Color", DemoApps3Locals.Colf);

                float sz = DemoApps3Locals.Sz;
                float thickness = DemoApps3Locals.Thickness;
                int ngon_sides = DemoApps3Locals.NgonSides;
                ImVec2 p = ImGui.GetCursorScreenPos();
                ImU32 col = ImGui.ColorConvertFloat4ToU32(new ImVec4(DemoApps3Locals.Colf[0], DemoApps3Locals.Colf[1], DemoApps3Locals.Colf[2], DemoApps3Locals.Colf[3])); // C++: const ImU32 col = ImColor(colf);
                float spacing = 10.0f;
                int corners_tl_br = ImDrawFlags_RoundCornersTopLeft | ImDrawFlags_RoundCornersBottomRight;
                float rounding = sz / 5.0f;
                int circle_segments = DemoApps3Locals.CircleSegmentsOverride ? DemoApps3Locals.CircleSegmentsOverrideV : 0;
                int curve_segments = DemoApps3Locals.CurveSegmentsOverride ? DemoApps3Locals.CurveSegmentsOverrideV : 0;
                ImVec2[] cp3 = { new ImVec2(0.0f, sz * 0.6f), new ImVec2(sz * 0.5f, -sz * 0.4f), new ImVec2(sz, sz) }; // Control points for curves
                ImVec2[] cp4 = { new ImVec2(0.0f, 0.0f), new ImVec2(sz * 1.3f, sz * 0.3f), new ImVec2(sz - sz * 1.3f, sz - sz * 0.3f), new ImVec2(sz, sz) };

                float x = p.x + 4.0f;
                float y = p.y + 4.0f;
                for (int n = 0; n < 2; n++)
                {
                    // First line uses a thickness of 1.0f, second line uses the configurable thickness
                    float th = (n == 0) ? 1.0f : thickness;
                    draw_list.AddNgon(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), sz * 0.5f, col, ngon_sides, th);                                    x += sz + spacing;  // N-gon
                    draw_list.AddCircle(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), sz * 0.5f, col, circle_segments, th);                             x += sz + spacing;  // Circle
                    draw_list.AddEllipse(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), new ImVec2(sz * 0.5f, sz * 0.3f), col, -0.3f, circle_segments, th); x += sz + spacing;  // Ellipse
                    draw_list.AddRect(new ImVec2(x, y), new ImVec2(x + sz, y + sz), col, 0.0f, th);                                                 x += sz + spacing;  // Square
                    draw_list.AddRect(new ImVec2(x, y), new ImVec2(x + sz, y + sz), col, rounding, th);                                             x += sz + spacing;  // Square with all rounded corners
                    draw_list.AddRect(new ImVec2(x, y), new ImVec2(x + sz, y + sz), col, rounding, th, corners_tl_br);                              x += sz + spacing;  // Square with two rounded corners
                    draw_list.AddTriangle(new ImVec2(x + sz * 0.5f, y), new ImVec2(x + sz, y + sz - 0.5f), new ImVec2(x, y + sz - 0.5f), col, th);  x += sz + spacing;  // Triangle
                    DemoA3_PathConcaveShape(draw_list, x, y, sz); draw_list.PathStroke(col, th, ImDrawFlags_Closed);                                x += sz + spacing;  // Concave Shape
                    draw_list.AddLineH(x, x + sz, y, col, th);                                                                                      x += sz + spacing;  // Horizontal line (note: drawing a filled rectangle will be faster!)
                    draw_list.AddLineV(x, y, y + sz, col, th);                                                                                      x += spacing;       // Vertical line (note: drawing a filled rectangle will be faster!)
                    draw_list.AddLine(new ImVec2(x, y), new ImVec2(x + sz, y + sz), col, th);                                                       x += sz + spacing;  // Diagonal line

                    // Path
                    draw_list.PathArcTo(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), sz * 0.5f, 3.141592f, 3.141592f * -0.5f, 0);
                    draw_list.PathStroke(col, th);
                    x += sz + spacing;

                    // Quadratic Bezier Curve (3 control points)
                    draw_list.AddBezierQuadratic(new ImVec2(x + cp3[0].x, y + cp3[0].y), new ImVec2(x + cp3[1].x, y + cp3[1].y), new ImVec2(x + cp3[2].x, y + cp3[2].y), col, th, curve_segments);
                    x += sz + spacing;

                    // Cubic Bezier Curve (4 control points)
                    draw_list.AddBezierCubic(new ImVec2(x + cp4[0].x, y + cp4[0].y), new ImVec2(x + cp4[1].x, y + cp4[1].y), new ImVec2(x + cp4[2].x, y + cp4[2].y), new ImVec2(x + cp4[3].x, y + cp4[3].y), col, th, curve_segments);

                    x = p.x + 4;
                    y += sz + spacing;
                }

                // Filled shapes
                draw_list.AddNgonFilled(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), sz * 0.5f, col, ngon_sides);                                      x += sz + spacing;  // N-gon
                draw_list.AddCircleFilled(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), sz * 0.5f, col, circle_segments);                               x += sz + spacing;  // Circle
                draw_list.AddEllipseFilled(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), new ImVec2(sz * 0.5f, sz * 0.3f), col, -0.3f, circle_segments); x += sz + spacing;  // Ellipse
                draw_list.AddRectFilled(new ImVec2(x, y), new ImVec2(x + sz, y + sz), col);                                                         x += sz + spacing;  // Square
                draw_list.AddRectFilled(new ImVec2(x, y), new ImVec2(x + sz, y + sz), col, 10.0f);                                                  x += sz + spacing;  // Square with all rounded corners
                draw_list.AddRectFilled(new ImVec2(x, y), new ImVec2(x + sz, y + sz), col, 10.0f, corners_tl_br);                                   x += sz + spacing;  // Square with two rounded corners
                draw_list.AddTriangleFilled(new ImVec2(x + sz * 0.5f, y), new ImVec2(x + sz, y + sz - 0.5f), new ImVec2(x, y + sz - 0.5f), col);    x += sz + spacing;  // Triangle
                DemoA3_PathConcaveShape(draw_list, x, y, sz); draw_list.PathFillConcave(col);                                                       x += sz + spacing;  // Concave shape
                draw_list.AddRectFilled(new ImVec2(x, y), new ImVec2(x + sz, y + thickness), col);                                                  x += sz + spacing;  // Horizontal line (faster than AddLine, but only handle integer thickness)
                draw_list.AddRectFilled(new ImVec2(x, y), new ImVec2(x + thickness, y + sz), col);                                                  x += spacing * 2.0f;// Vertical line (faster than AddLine, but only handle integer thickness)
                draw_list.AddRectFilled(new ImVec2(x, y), new ImVec2(x + 1, y + 1), col);                                                           x += sz;            // Pixel (faster than AddLine)

                // Path
                draw_list.PathArcTo(new ImVec2(x + sz * 0.5f, y + sz * 0.5f), sz * 0.5f, 3.141592f * -0.5f, 3.141592f, 0);
                draw_list.PathFillConvex(col);
                x += sz + spacing;

                // Quadratic Bezier Curve (3 control points)
                draw_list.PathLineTo(new ImVec2(x + cp3[0].x, y + cp3[0].y));
                draw_list.PathBezierQuadraticCurveTo(new ImVec2(x + cp3[1].x, y + cp3[1].y), new ImVec2(x + cp3[2].x, y + cp3[2].y), curve_segments);
                draw_list.PathFillConvex(col);
                x += sz + spacing;

                draw_list.AddRectFilledMultiColor(new ImVec2(x, y), new ImVec2(x + sz, y + sz), ImGuiCore.IM_COL32(0, 0, 0, 255), ImGuiCore.IM_COL32(255, 0, 0, 255), ImGuiCore.IM_COL32(255, 255, 0, 255), ImGuiCore.IM_COL32(0, 255, 0, 255));
                x += sz + spacing;

                ImGui.Dummy(new ImVec2((sz + spacing) * 13.2f, (sz + spacing) * 3.0f));
                ImGui.PopItemFlag();
                ImGui.PopItemWidth();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Canvas"))
            {
                ImGui.Checkbox("Enable grid", ref DemoApps3Locals.OptEnableGrid);
                ImGui.Checkbox("Enable context menu", ref DemoApps3Locals.OptEnableContextMenu);
                ImGui.Text("Mouse Left: drag to add lines,\nMouse Right: drag to scroll, click for context menu.");

                // Typically you would use a BeginChild()/EndChild() pair to benefit from a clipping region + own scrolling.
                // Here we demonstrate that this can be replaced by simple offsetting + custom drawing + PushClipRect/PopClipRect() calls.
                // To use a child window instead we could use, e.g:
                //      ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));      // Disable padding
                //      ImGui::PushStyleColor(ImGuiCol_ChildBg, IM_COL32(50, 50, 50, 255));  // Set a background color
                //      ImGui::BeginChild("canvas", ImVec2(0.0f, 0.0f), ImGuiChildFlags_Borders, ImGuiWindowFlags_NoMove);
                //      ImGui::PopStyleColor();
                //      ImGui::PopStyleVar();
                //      [...]
                //      ImGui::EndChild();

                ImVector<ImVec2> points = DemoApps3Locals.Points;

                // Using InvisibleButton() as a convenience 1) it will advance the layout cursor and 2) allows us to use IsItemHovered()/IsItemActive()
                ImVec2 canvas_p0 = ImGui.GetCursorScreenPos();      // ImDrawList API uses screen coordinates!
                ImVec2 canvas_sz = ImGui.GetContentRegionAvail();   // Resize canvas to what's available
                if (canvas_sz.x < 50.0f) canvas_sz.x = 50.0f;
                if (canvas_sz.y < 50.0f) canvas_sz.y = 50.0f;
                ImVec2 canvas_p1 = new ImVec2(canvas_p0.x + canvas_sz.x, canvas_p0.y + canvas_sz.y);

                // Draw border and background color
                ImGuiIO io = ImGui.GetIO();
                ImDrawList draw_list = ImGui.GetWindowDrawList();
                draw_list.AddRectFilled(canvas_p0, canvas_p1, ImGuiCore.IM_COL32(50, 50, 50, 255));
                draw_list.AddRect(canvas_p0, canvas_p1, ImGuiCore.IM_COL32(255, 255, 255, 255));

                // This will catch our interactions
                ImGui.InvisibleButton("canvas", canvas_sz, ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight);
                bool is_hovered = ImGui.IsItemHovered(); // Hovered
                bool is_active = ImGui.IsItemActive();   // Held
                ImVec2 origin = new ImVec2(canvas_p0.x + DemoApps3Locals.Scrolling.x, canvas_p0.y + DemoApps3Locals.Scrolling.y); // Lock scrolled origin
                ImVec2 mouse_pos_in_canvas = new ImVec2(io.MousePos.x - origin.x, io.MousePos.y - origin.y);

                // Add first and second point
                if (is_hovered && !DemoApps3Locals.AddingLine && ImGui.IsMouseClicked(ImGuiMouseButton_Left))
                {
                    points.push_back(mouse_pos_in_canvas);
                    points.push_back(mouse_pos_in_canvas);
                    DemoApps3Locals.AddingLine = true;
                }
                if (DemoApps3Locals.AddingLine)
                {
                    points.back_ref() = mouse_pos_in_canvas;
                    if (!ImGui.IsMouseDown(ImGuiMouseButton_Left))
                        DemoApps3Locals.AddingLine = false;
                }

                // Pan (we use a zero mouse threshold when there's no context menu)
                // You may decide to make that threshold dynamic based on whether the mouse is hovering something etc.
                float mouse_threshold_for_pan = DemoApps3Locals.OptEnableContextMenu ? -1.0f : 0.0f;
                if (is_active && ImGui.IsMouseDragging(ImGuiMouseButton_Right, mouse_threshold_for_pan))
                {
                    DemoApps3Locals.Scrolling.x += io.MouseDelta.x;
                    DemoApps3Locals.Scrolling.y += io.MouseDelta.y;
                }

                // Context menu (under default mouse threshold)
                ImVec2 drag_delta = ImGui.GetMouseDragDelta(ImGuiMouseButton_Right);
                if (DemoApps3Locals.OptEnableContextMenu && drag_delta.x == 0.0f && drag_delta.y == 0.0f)
                    ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags_MouseButtonRight);
                if (ImGui.BeginPopup("context"))
                {
                    if (DemoApps3Locals.AddingLine)
                        points.resize(points.Size - 2);
                    DemoApps3Locals.AddingLine = false;
                    if (ImGui.MenuItem("Remove one", null, false, points.Size > 0)) { points.resize(points.Size - 2); }
                    if (ImGui.MenuItem("Remove all", null, false, points.Size > 0)) { points.clear(); }
                    ImGui.EndPopup();
                }

                // Draw grid + all lines in the canvas
                draw_list.PushClipRect(canvas_p0, canvas_p1, true);
                if (DemoApps3Locals.OptEnableGrid)
                {
                    const float GRID_STEP = 64.0f;
                    for (float x = ImMath.ImFmod(DemoApps3Locals.Scrolling.x, GRID_STEP); x < canvas_sz.x; x += GRID_STEP)
                        draw_list.AddLineV(canvas_p0.x + x, canvas_p0.y, canvas_p1.y, ImGuiCore.IM_COL32(200, 200, 200, 40));
                    for (float y = ImMath.ImFmod(DemoApps3Locals.Scrolling.y, GRID_STEP); y < canvas_sz.y; y += GRID_STEP)
                        draw_list.AddLineH(canvas_p0.x, canvas_p1.x, canvas_p0.y + y, ImGuiCore.IM_COL32(200, 200, 200, 40));
                }
                for (int n = 0; n < points.Size; n += 2)
                    draw_list.AddLine(new ImVec2(origin.x + points[n].x, origin.y + points[n].y), new ImVec2(origin.x + points[n + 1].x, origin.y + points[n + 1].y), ImGuiCore.IM_COL32(255, 255, 0, 255), 2.0f);
                draw_list.PopClipRect();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("BG/FG draw lists"))
            {
                ImGui.Checkbox("Draw in Background draw list", ref DemoApps3Locals.DrawBg);
                ImGui.SameLine(); HelpMarker("The Background draw list will be rendered below every Dear ImGui windows.");
                ImGui.Checkbox("Draw in Foreground draw list", ref DemoApps3Locals.DrawFg);
                ImGui.SameLine(); HelpMarker("The Foreground draw list will be rendered over every Dear ImGui windows.");
                ImVec2 window_pos = ImGui.GetWindowPos();
                ImVec2 window_size = ImGui.GetWindowSize();
                ImVec2 window_center = new ImVec2(window_pos.x + window_size.x * 0.5f, window_pos.y + window_size.y * 0.5f);
                if (DemoApps3Locals.DrawBg)
                    ImGui.GetBackgroundDrawList().AddCircle(window_center, window_size.x * 0.6f, ImGuiCore.IM_COL32(255, 0, 0, 200), 0, 10 + 4);
                if (DemoApps3Locals.DrawFg)
                    ImGui.GetForegroundDrawList().AddCircle(window_center, window_size.y * 0.6f, ImGuiCore.IM_COL32(0, 255, 0, 200), 0, 10);
                ImGui.EndTabItem();
            }

            // Demonstrate out-of-order rendering via channels splitting
            // We use functions in ImDrawList as each draw list contains a convenience splitter,
            // but you can also instantiate your own ImDrawListSplitter if you need to nest them.
            if (ImGui.BeginTabItem("Draw Channels"))
            {
                ImDrawList draw_list = ImGui.GetWindowDrawList();
                {
                    ImGui.Text("Blue shape is drawn first: appears in back");
                    ImGui.Text("Red shape is drawn after: appears in front");
                    ImVec2 p0 = ImGui.GetCursorScreenPos();
                    draw_list.AddRectFilled(new ImVec2(p0.x, p0.y), new ImVec2(p0.x + 50, p0.y + 50), ImGuiCore.IM_COL32(0, 0, 255, 255)); // Blue
                    draw_list.AddRectFilled(new ImVec2(p0.x + 25, p0.y + 25), new ImVec2(p0.x + 75, p0.y + 75), ImGuiCore.IM_COL32(255, 0, 0, 255)); // Red
                    ImGui.Dummy(new ImVec2(75, 75));
                }
                ImGui.Separator();
                {
                    ImGui.Text("Blue shape is drawn first, into channel 1: appears in front");
                    ImGui.Text("Red shape is drawn after, into channel 0: appears in back");
                    ImVec2 p1 = ImGui.GetCursorScreenPos();

                    // Create 2 channels and draw a Blue shape THEN a Red shape.
                    // You can create any number of channels. Tables API use 1 channel per column in order to better batch draw calls.
                    draw_list._Splitter.Split(draw_list, 2);             // C++: draw_list->ChannelsSplit(2)
                    draw_list._Splitter.SetCurrentChannel(draw_list, 1); // C++: draw_list->ChannelsSetCurrent(1)
                    draw_list.AddRectFilled(new ImVec2(p1.x, p1.y), new ImVec2(p1.x + 50, p1.y + 50), ImGuiCore.IM_COL32(0, 0, 255, 255)); // Blue
                    draw_list._Splitter.SetCurrentChannel(draw_list, 0); // C++: draw_list->ChannelsSetCurrent(0)
                    draw_list.AddRectFilled(new ImVec2(p1.x + 25, p1.y + 25), new ImVec2(p1.x + 75, p1.y + 75), ImGuiCore.IM_COL32(255, 0, 0, 255)); // Red

                    // Flatten/reorder channels. Red shape is in channel 0 and it appears below the Blue shape in channel 1.
                    // This works by copying draw indices only (vertices are not copied).
                    draw_list._Splitter.Merge(draw_list);                // C++: draw_list->ChannelsMerge()
                    ImGui.Dummy(new ImVec2(75, 75));
                    ImGui.Text("After reordering, contents of channel 0 appears below channel 1.");
                }
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    // C++: void ShowExampleAppDocuments(bool* p_open) (imgui_demo.cpp 10651-10804)
    public static void ShowExampleAppDocuments(ref bool p_open)
    {
        ExampleAppDocuments app = DemoApps3Locals.App;

        // Options
        bool opt_reorderable = DemoApps3Locals.OptReorderable;
        ImGuiTabBarFlags opt_fitting_flags = DemoApps3Locals.OptFittingFlags;

        bool window_contents_visible = ImGui.Begin("Example: Documents", ref p_open, ImGuiWindowFlags_MenuBar);
        if (!window_contents_visible)
        {
            ImGui.End();
            return;
        }

        // Menu
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                int open_count = 0;
                for (int doc_n = 0; doc_n < app.Documents.Size; doc_n++)
                    open_count += app.Documents[doc_n].Open ? 1 : 0;

                if (ImGui.BeginMenu("Open", open_count < app.Documents.Size))
                {
                    for (int doc_n = 0; doc_n < app.Documents.Size; doc_n++)
                    {
                        MyDocument doc = app.Documents[doc_n];
                        if (!doc.Open && ImGui.MenuItem(doc.NameStr))
                            doc.DoOpen();
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.MenuItem("Close All Documents", null, false, open_count > 0))
                    for (int doc_n = 0; doc_n < app.Documents.Size; doc_n++)
                        app.CloseQueue.push_back(app.Documents[doc_n]);
                if (ImGui.MenuItem("Exit"))
                    p_open = false;
                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }

        // [Debug] List documents with one checkbox for each
        for (int doc_n = 0; doc_n < app.Documents.Size; doc_n++)
        {
            MyDocument doc = app.Documents[doc_n];
            if (doc_n > 0)
                ImGui.SameLine();
            ImGui.PushID(doc);
            if (ImGui.Checkbox(doc.NameStr, ref doc.Open))
                if (!doc.Open)
                    doc.DoForceClose();
            ImGui.PopID();
        }

        ImGui.Separator();

        // About the ImGuiWindowFlags_UnsavedDocument / ImGuiTabItemFlags_UnsavedDocument flags.
        // They have multiple effects:
        // - Display a dot next to the title.
        // - Tab is selected when clicking the X close button.
        // - Closure is not assumed (will wait for user to stop submitting the tab).
        //   Otherwise closure is assumed when pressing the X, so if you keep submitting the tab may reappear at end of tab bar.
        //   We need to assume closure by default otherwise waiting for "lack of submission" on the next frame would leave an empty
        //   hole for one-frame, both in the tab-bar and in tab-contents when closing a tab/window.
        //   The rarely used SetTabItemClosed() function is a way to notify of programmatic closure to avoid the one-frame hole.

        // Submit Tab Bar and Tabs
        {
            ImGuiTabBarFlags tab_bar_flags = (opt_fitting_flags) | (opt_reorderable ? ImGuiTabBarFlags_Reorderable : 0);
            tab_bar_flags |= ImGuiTabBarFlags_DrawSelectedOverline;
            if (ImGui.BeginTabBar("##tabs", tab_bar_flags))
            {
                if (opt_reorderable)
                    app.NotifyOfDocumentsClosedElsewhere();

                // [DEBUG] Stress tests
                //if ((ImGui::GetFrameCount() % 30) == 0) docs[1].Open ^= 1;            // [DEBUG] Automatically show/hide a tab. Test various interactions e.g. dragging with this on.
                //if (ImGui::GetIO().KeyCtrl) ImGui::SetTabItemSelected(docs[1].Name);  // [DEBUG] Test SetTabItemSelected(), probably not very useful as-is anyway..

                // Submit Tabs
                for (int doc_n = 0; doc_n < app.Documents.Size; doc_n++)
                {
                    MyDocument doc = app.Documents[doc_n];
                    if (!doc.Open)
                        continue;

                    // As we allow to change document name, we append a never-changing document id so tabs are stable
                    string doc_name = app.GetTabName(doc);
                    ImGuiTabItemFlags tab_flags = (doc.Dirty ? ImGuiTabItemFlags_UnsavedDocument : 0);
                    bool visible = ImGui.BeginTabItem(doc_name, ref doc.Open, tab_flags);

                    // Cancel attempt to close when unsaved add to save queue so we can display a popup.
                    if (!doc.Open && doc.Dirty)
                    {
                        doc.Open = true;
                        app.CloseQueue.push_back(doc);
                    }

                    app.DisplayDocContextMenu(doc);
                    if (visible)
                    {
                        app.DisplayDocContents(doc);
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }
        }

        // Display renaming UI
        if (app.RenamingDoc != null)
        {
            if (app.RenamingStarted)
                ImGui.OpenPopup("Rename");
            if (ImGui.BeginPopup("Rename"))
            {
                ImGui.SetNextItemWidth(ImGui.GetFontSize() * 30);
                if (ImGui.InputText("###Name", app.RenamingDoc.Name, app.RenamingDoc.Name.Length, ImGuiInputTextFlags_EnterReturnsTrue))
                {
                    ImGui.CloseCurrentPopup();
                    app.RenamingDoc = null;
                }
                if (app.RenamingStarted)
                    ImGui.SetKeyboardFocusHere(-1);
                ImGui.EndPopup();
            }
            else
            {
                app.RenamingDoc = null;
            }
            app.RenamingStarted = false;
        }

        // Display closing confirmation UI
        if (app.CloseQueue.Size > 0) // C++: !app.CloseQueue.empty()
        {
            int close_queue_unsaved_documents = 0;
            for (int n = 0; n < app.CloseQueue.Size; n++)
                if (app.CloseQueue[n].Dirty)
                    close_queue_unsaved_documents++;

            if (close_queue_unsaved_documents == 0)
            {
                // Close documents when all are unsaved
                for (int n = 0; n < app.CloseQueue.Size; n++)
                    app.CloseQueue[n].DoForceClose();
                app.CloseQueue.clear();
            }
            else
            {
                if (!ImGui.IsPopupOpen("Save?"))
                    ImGui.OpenPopup("Save?");
                if (ImGui.BeginPopupModal("Save?", ImGuiWindowFlags_AlwaysAutoResize))
                {
                    ImGui.Text("Save change to the following items?");
                    float item_height = ImGui.GetTextLineHeightWithSpacing();
                    if (ImGui.BeginChild(ImGui.GetID("frame"), new ImVec2(-float.Epsilon /* -FLT_MIN */, 6.25f * item_height), ImGuiChildFlags_FrameStyle))
                        for (int n = 0; n < app.CloseQueue.Size; n++)
                        {
                            MyDocument doc = app.CloseQueue[n];
                            if (doc.Dirty)
                                ImGui.Text("%s", doc.NameStr);
                        }
                    ImGui.EndChild();

                    ImVec2 button_size = new ImVec2(ImGui.GetFontSize() * 7.0f, 0.0f);
                    if (ImGui.Button("Yes", button_size))
                    {
                        for (int n = 0; n < app.CloseQueue.Size; n++)
                        {
                            MyDocument doc = app.CloseQueue[n];
                            if (doc.Dirty)
                                doc.DoSave();
                            doc.DoForceClose();
                        }
                        app.CloseQueue.clear();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("No", button_size))
                    {
                        for (int n = 0; n < app.CloseQueue.Size; n++)
                            app.CloseQueue[n].DoForceClose();
                        app.CloseQueue.clear();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", button_size))
                    {
                        app.CloseQueue.clear();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }
        }

        ImGui.End();
    }

    // C++: void ShowExampleAppAssetsBrowser(bool* p_open) (imgui_demo.cpp 11277-11284)
    public static void ShowExampleAppAssetsBrowser(ref bool p_open)
    {
        ExampleAssetsBrowser assets_browser = DemoApps3Locals.AssetsBrowser;
        assets_browser.Draw("Example: Assets Browser", ref p_open);
    }
}
