// Port of imgui_demo.cpp [SECTION] ShowDemoWindow + ImGuiDemoWindowData + DemoWindowMenuBar +
// ExampleTreeNode/ExampleTree helpers + ExampleImageViewer helpers.
// Section bodies (Widgets/Layout/Popups/Tables/Inputs) are in ImGuiDemoSection*.cs files.
// Example apps (Console, Documents...) are in ImGuiDemoApps*.cs files.
using System;
using System.Text;

namespace ImGui;

// Data to be shared across different functions of the demo.
// C++: struct ImGuiDemoWindowData (imgui_demo.cpp 306-344)
public class ImGuiDemoWindowData
{
    // Examples Apps (accessible from the "Examples" menu)
    public bool ShowMainMenuBar = false;
    public bool ShowAppAssetsBrowser = false;
    public bool ShowAppConsole = false;
    public bool ShowAppCustomRendering = false;
    public bool ShowAppDocuments = false;
    public bool ShowAppImageViewer = false;
    public bool ShowAppLog = false;
    public bool ShowAppLayout = false;
    public bool ShowAppPropertyEditor = false;
    public bool ShowAppSimpleOverlay = false;
    public bool ShowAppAutoResize = false;
    public bool ShowAppConstrainedResize = false;
    public bool ShowAppFullscreen = false;
    public bool ShowAppLongText = false;
    public bool ShowAppWindowTitles = false;

    // Dear ImGui Tools (accessible from the "Tools" menu)
    public bool ShowMetrics = false;
    public bool ShowDebugLog = false;
    public bool ShowIDStackTool = false;
    public bool ShowStyleEditor = false;
    public bool ShowAbout = false;

    // Other data
    public bool DisableSections = false;
    public bool LiveEditOverride = false;
    public int LiveEditFlags = ImGuiItemFlags_LiveEditOnInputText; // ImGuiItemFlags
    public ExampleTreeNode DemoTree; // ExampleTreeNode*
}

// Simple representation for a tree
// (this is designed to be simple to understand for our demos, not to be fancy or efficient etc.)
// C++: struct ExampleTreeNode (imgui_demo.cpp 748-765)
public class ExampleTreeNode
{
    public const int NAME_CAPACITY = 28;

    // Tree structure
    public string Name = "";
    public int UID = 0;
    public ExampleTreeNode Parent;
    public ImVector<ExampleTreeNode> Childs = new ImVector<ExampleTreeNode>();
    public int IndexInParent = 0;  // Maintaining this allows us to implement linear traversal more easily

    // Leaf Data
    public bool HasData = false;    // All leaves have data
    public bool DataMyBool = true;
    public int DataMyInt = 128;
    public ImVec2 DataMyVec2 = new ImVec2(0.0f, 3.141592f);
}

// Simple representation of struct metadata/serialization data.
// (this is a minimal version of what a typical advanced application may provide)
// C++: struct ExampleMemberInfo (imgui_demo.cpp 768-775) — 'Offset' has no meaning in C#; accessors are used instead.
public class ExampleMemberInfo
{
    public string Name;             // Member name
    public int DataType;            // ImGuiDataType
    public int DataCount;           // Member count (1 when scalar)
    public Func<ExampleTreeNode, object> Get;   // Member getter (C# replacement for offsetof)
    public Action<ExampleTreeNode, object> Set; // Member setter
}

// Metadata description of ExampleTreeNode struct.
// C++: static const ExampleMemberInfo ExampleTreeNodeMemberInfos[] (imgui_demo.cpp 778-784)
public static class ExampleTree
{
    public static readonly ExampleMemberInfo[] TreeNodeMemberInfos =
    {
        new ExampleMemberInfo { Name = "MyName", DataType = ImGuiDataType_String, DataCount = 1,
            Get = (n) => n.Name, Set = (n, v) => n.Name = (string)v },
        new ExampleMemberInfo { Name = "MyBool", DataType = ImGuiDataType_Bool, DataCount = 1,
            Get = (n) => n.DataMyBool, Set = (n, v) => n.DataMyBool = (bool)v },
        new ExampleMemberInfo { Name = "MyInt", DataType = ImGuiDataType_S32, DataCount = 1,
            Get = (n) => n.DataMyInt, Set = (n, v) => n.DataMyInt = (int)v },
        new ExampleMemberInfo { Name = "MyVec2", DataType = ImGuiDataType_Float, DataCount = 2,
            Get = (n) => n.DataMyVec2, Set = (n, v) => n.DataMyVec2 = (ImVec2)v },
    };

    // C++: static ExampleTreeNode* ExampleTree_CreateNode(const char* name, int uid, ExampleTreeNode* parent)
    public static ExampleTreeNode CreateNode(string name, int uid, ExampleTreeNode parent)
    {
        var node = new ExampleTreeNode();
        node.Name = name.Length > ExampleTreeNode.NAME_CAPACITY ? name.Substring(0, ExampleTreeNode.NAME_CAPACITY - 1) : name;
        node.UID = uid;
        node.Parent = parent;
        node.IndexInParent = parent != null ? parent.Childs.Size : 0;
        if (parent != null)
            parent.Childs.push_back(node);
        return node;
    }

    // C++: static void ExampleTree_DestroyNode(ExampleTreeNode* node) — GC handles freeing; kept for parity.
    public static void DestroyNode(ExampleTreeNode node)
    {
        for (int i = 0; i < node.Childs.Size; i++)
            DestroyNode(node.Childs[i]);
    }

    // Create example tree data
    // (warning: this can allocate MANY MANY more times than other code in all of Dear ImGui + demo combined)
    // C++: static ExampleTreeNode* ExampleTree_CreateDemoTree()
    public static ExampleTreeNode CreateDemoTree()
    {
        //     20 root nodes ->    211 total nodes,   ~261 allocs.
        //   1000 root nodes ->   ~11K total nodes,   ~14K allocs.
        //  10000 root nodes ->  ~123K total nodes,  ~154K allocs.
        // 100000 root nodes -> ~1338K total nodes, ~1666K allocs.
        const int ROOT_ITEMS_COUNT = 20;

        string[] category_names = { "Apple", "Banana", "Cherry", "Kiwi", "Mango", "Orange", "Pear", "Pineapple", "Strawberry", "Watermelon" };
        int category_count = category_names.Length;
        int uid = 0;
        ExampleTreeNode node_L0 = CreateNode("<ROOT>", ++uid, null);
        for (int idx_L0 = 0; idx_L0 < ROOT_ITEMS_COUNT; idx_L0++)
        {
            string name_buf = string.Format("{0} {1}", category_names[idx_L0 / (ROOT_ITEMS_COUNT / category_count)], idx_L0 % (ROOT_ITEMS_COUNT / category_count));
            ExampleTreeNode node_L1 = CreateNode(name_buf, ++uid, node_L0);
            int number_of_childs = node_L1.Name.Length;
            for (int idx_L1 = 0; idx_L1 < number_of_childs; idx_L1++)
            {
                name_buf = string.Format("Child {0}", idx_L1);
                ExampleTreeNode node_L2 = CreateNode(name_buf, ++uid, node_L1);
                node_L2.HasData = true;
                if (idx_L1 == 0)
                {
                    name_buf = string.Format("Sub-child {0}", 0);
                    ExampleTreeNode node_L3 = CreateNode(name_buf, ++uid, node_L2);
                    node_L3.HasData = true;
                }
            }
        }
        return node_L0;
    }
}

// C++: struct ExampleImageViewerData (imgui_demo.cpp 844-855)
public class ExampleImageViewerData
{
    public ImU32 ImageBgColor = ImGuiCore.IM_COL32(100, 100, 100, 255);
    public ImU32 GridColor = ImGuiCore.IM_COL32(255, 255, 255, 100);
    public bool GridEnabled = true;
    public bool ViewReset = true;
    public ImVec2 ViewOffset; // in image space
    public float Zoom = 10.0f;
    public float ZoomMin = 1.0f;
    public float ZoomMax = 10000.0f;
}

public static partial class ImGui
{
    // C++: static void ExampleImageViewer_DrawOptions(ExampleImageViewerData* data)
    public static void ExampleImageViewer_DrawOptions(ExampleImageViewerData data)
    {
        SetNextItemShortcut(ImGuiKey_G, ImGuiInputFlags_Tooltip); // | ImGuiInputFlags_RouteGlobal
        Checkbox("Grid", ref data.GridEnabled);
        SameLine();
        SetNextItemWidth(GetFontSize() * 10.0f);
        float zoom_100 = data.Zoom * 100.0f;
        if (DragFloat("##Zoom", ref zoom_100, 5.0f, data.ZoomMin * 100.0f, data.ZoomMax * 100.0f, "%.0f%%", ImGuiSliderFlags_AlwaysClamp))
            data.Zoom = zoom_100 / 100.0f;
    }

    // C++: static void ExampleImageViewer_DrawCanvas(ExampleImageViewerData* data, ImVec2 canvas_size, ImTextureRef image_tex_ref, int image_w, int image_h)
    public static void ExampleImageViewer_DrawCanvas(ExampleImageViewerData data, ImVec2 canvas_size, ImTextureRef image_tex_ref, int image_w, int image_h)
    {
        ImGuiIO io = GetIO();
        ImGuiPlatformIO platform_io = GetPlatformIO();
        ImDrawList draw_list = GetWindowDrawList();
        System.Diagnostics.Debug.Assert(canvas_size.x >= 0.0f && canvas_size.y >= 0.0f);

        // Layout canvas
        InvisibleButton("##Canvas", canvas_size);
        ImVec2 canvas_min = GetItemRectMin();
        ImVec2 canvas_max = GetItemRectMax();

        if (data.ViewReset)
            data.ViewOffset = new ImVec2((canvas_size.x * 0.5f / data.Zoom) - 0.5f, (canvas_size.y * 0.5f / data.Zoom) - 0.5f); // Add half a pixel padding
        data.ViewReset = false;

        // Handle inputs
        if (SetItemKeyOwner(ImGuiKey_MouseWheelY))
            if (io.MouseWheel != 0.0f)
                data.Zoom = ImMath.ImClamp(data.Zoom * (1.0f + io.MouseWheel * 0.10f), data.ZoomMin, data.ZoomMax);
        float zoom = data.Zoom; // (float)(int)ViewZoom
        if (IsItemActive() && IsMouseDragging(0))
        {
            data.ViewOffset.x -= io.MouseDelta.x / zoom;
            data.ViewOffset.y -= io.MouseDelta.y / zoom;
        }

        // Display image
        ImVec2 image_min, image_max;
        image_min.x = (float)(int)((canvas_min.x - (data.ViewOffset.x * zoom)) + (canvas_size.x * 0.5f));
        image_min.y = (float)(int)((canvas_min.y - (data.ViewOffset.y * zoom)) + (canvas_size.y * 0.5f));
        image_max.x = (float)(int)(image_min.x + image_w * zoom);
        image_max.y = (float)(int)(image_min.y + image_h * zoom);
        draw_list.AddRect(new ImVec2(canvas_min.x - 1.0f, canvas_min.y - 1.0f), new ImVec2(canvas_max.x + 1.0f, canvas_max.y + 1.0f), ImGuiCore.IM_COL32(255, 255, 255, 255));
        draw_list.PushClipRect(canvas_min, canvas_max, true);
        draw_list.AddRectFilled(image_min, image_max, data.ImageBgColor);
        if (platform_io.DrawCallback_SetSamplerNearest != null)
            draw_list.AddCallback(platform_io.DrawCallback_SetSamplerNearest, null);
        draw_list.AddImage(image_tex_ref, image_min, image_max);
        if (platform_io.DrawCallback_SetSamplerLinear != null)
            draw_list.AddCallback(GetPlatformIO().DrawCallback_SetSamplerLinear, null);

        // Display grid lines for visible pixels
        if (data.GridEnabled && zoom > 6.0f)
        {
            float step = (float)zoom;
            for (int px = (int)((canvas_min.x - image_min.x) / step); px <= (int)((canvas_max.x - image_min.x) / step); px++)
                draw_list.AddLineV(image_min.x + px * step, canvas_min.y, canvas_max.y, data.GridColor, 1.0f);
            for (int py = (int)((canvas_min.y - image_min.y) / step); py <= (int)((canvas_max.y - image_min.y) / step); py++)
                draw_list.AddLineH(canvas_min.x, canvas_max.x, image_min.y + py * step, data.GridColor, 1.0f);
        }
        draw_list.PopClipRect();
    }

    // imgui_demo.cpp function-local statics for ShowDemoWindow
    static ImGuiDemoWindowData _demoData;
    static bool _demoNoTitlebar = false;
    static bool _demoNoScrollbar = false;
    static bool _demoNoMenu = false;
    static bool _demoNoMove = false;
    static bool _demoNoResize = false;
    static bool _demoNoCollapse = false;
    static bool _demoNoClose = false;
    static bool _demoNoNav = false;
    static bool _demoNoBackground = false;
    static bool _demoNoBringToFront = false;
    static bool _demoUnsavedDocument = false;

    // Demonstrate most Dear ImGui features (this is big function!)
    // C++: void ImGui::ShowDemoWindow(bool* p_open)  (imgui_demo.cpp 345-665)
    public static void ShowDemoWindow(ref bool p_open) => ShowDemoWindow(ref p_open, null);
    public static void ShowDemoWindow() => ShowDemoWindow(ref _demoP_openDummy, null);
    static bool _demoP_openDummy;
    public static void ShowDemoWindow(string label) => ShowDemoWindow(ref _demoP_openDummy, label);

    public static void ShowDemoWindow(ref bool p_open, string label)
    {
        // Exceptionally add an extra assert here for people confused about initial Dear ImGui setup
        System.Diagnostics.Debug.Assert(GetCurrentContext() != null, "Missing Dear ImGui context. Refer to examples app!");

        // Stored data
        if (_demoData == null)
            _demoData = new ImGuiDemoWindowData();
        ImGuiDemoWindowData demo_data = _demoData;

        // Examples Apps (accessible from the "Examples" menu)
        if (demo_data.ShowMainMenuBar) { ShowExampleAppMainMenuBar(); }
        if (demo_data.ShowAppDocuments) { ShowExampleAppDocuments(ref demo_data.ShowAppDocuments); }
        if (demo_data.ShowAppAssetsBrowser) { ShowExampleAppAssetsBrowser(ref demo_data.ShowAppAssetsBrowser); }
        if (demo_data.ShowAppConsole) { ShowExampleAppConsole(ref demo_data.ShowAppConsole); }
        if (demo_data.ShowAppCustomRendering) { ShowExampleAppCustomRendering(ref demo_data.ShowAppCustomRendering); }
        if (demo_data.ShowAppImageViewer) { ShowExampleAppImageViewer(ref demo_data.ShowAppImageViewer); }
        if (demo_data.ShowAppLog) { ShowExampleAppLog(ref demo_data.ShowAppLog); }
        if (demo_data.ShowAppLayout) { ShowExampleAppLayout(ref demo_data.ShowAppLayout); }
        if (demo_data.ShowAppPropertyEditor) { ShowExampleAppPropertyEditor(ref demo_data.ShowAppPropertyEditor, demo_data); }
        if (demo_data.ShowAppSimpleOverlay) { ShowExampleAppSimpleOverlay(ref demo_data.ShowAppSimpleOverlay); }
        if (demo_data.ShowAppAutoResize) { ShowExampleAppAutoResize(ref demo_data.ShowAppAutoResize); }
        if (demo_data.ShowAppConstrainedResize) { ShowExampleAppConstrainedResize(ref demo_data.ShowAppConstrainedResize); }
        if (demo_data.ShowAppFullscreen) { ShowExampleAppFullscreen(ref demo_data.ShowAppFullscreen); }
        if (demo_data.ShowAppLongText) { ShowExampleAppLongText(ref demo_data.ShowAppLongText); }
        if (demo_data.ShowAppWindowTitles) { ShowExampleAppWindowTitles(ref demo_data.ShowAppWindowTitles); }

        // Dear ImGui Tools (accessible from the "Tools" menu)
        if (demo_data.ShowMetrics) { ShowMetricsWindow(ref demo_data.ShowMetrics); }
        if (demo_data.ShowDebugLog) { ShowDebugLogWindow(ref demo_data.ShowDebugLog); }
        if (demo_data.ShowIDStackTool) { ShowIDStackToolWindow(ref demo_data.ShowIDStackTool); }
        if (demo_data.ShowAbout) { ShowAboutWindow(ref demo_data.ShowAbout); }
        if (demo_data.ShowStyleEditor)
        {
            Begin("Dear ImGui Style Editor", ref demo_data.ShowStyleEditor);
            ShowStyleEditor();
            End();
        }

        // Demonstrate the various window flags. Typically you would just use the default!
        bool no_titlebar = _demoNoTitlebar, no_scrollbar = _demoNoScrollbar, no_menu = _demoNoMenu;
        bool no_move = _demoNoMove, no_resize = _demoNoResize, no_collapse = _demoNoCollapse;
        bool no_close = _demoNoClose, no_nav = _demoNoNav, no_background = _demoNoBackground;
        bool no_bring_to_front = _demoNoBringToFront, unsaved_document = _demoUnsavedDocument;

        ImGuiWindowFlags window_flags = 0;
        if (no_titlebar) window_flags |= ImGuiWindowFlags_NoTitleBar;
        if (no_scrollbar) window_flags |= ImGuiWindowFlags_NoScrollbar;
        if (!no_menu) window_flags |= ImGuiWindowFlags_MenuBar;
        if (no_move) window_flags |= ImGuiWindowFlags_NoMove;
        if (no_resize) window_flags |= ImGuiWindowFlags_NoResize;
        if (no_collapse) window_flags |= ImGuiWindowFlags_NoCollapse;
        if (no_nav) window_flags |= ImGuiWindowFlags_NoNav;
        if (no_background) window_flags |= ImGuiWindowFlags_NoBackground;
        if (no_bring_to_front) window_flags |= ImGuiWindowFlags_NoBringToFrontOnFocus;
        if (unsaved_document) window_flags |= ImGuiWindowFlags_UnsavedDocument;
        if (no_close) p_open = false; // Don't pass our bool* to Begin

        // We specify a default position/size in case there's no data in the .ini file.
        ImGuiViewport main_viewport = GetMainViewport();
        SetNextWindowPos(new ImVec2(main_viewport.WorkPos.x + 650, main_viewport.WorkPos.y + 20), ImGuiCond_FirstUseEver);
        SetNextWindowSize(new ImVec2(550, 680), ImGuiCond_FirstUseEver);

        // Main body of the Demo window starts here.
        if (!Begin(label ?? "Dear ImGui Demo", ref p_open, window_flags))
        {
            // Early out if the window is collapsed, as an optimization.
            End();
            return;
        }

        // Most framed widgets share a common width settings. Remaining width is used for the label.
        float label_width_base = GetFontSize() * 12;               // Some amount of width for label, based on font size.
        float label_width_max = GetContentRegionAvail().x * 0.40f; // ...but always leave some room for framed widgets.
        float label_width = ImMath.ImMin(label_width_base, label_width_max);
        PushItemWidth(-label_width);                               // Right-align: framed items will leave 'label_width' available for the label.

        // Menu Bar
        DemoWindowMenuBar(demo_data);

        Text("dear imgui says hello! (%s) (%d)", ImGuiVersion, ImGuiVersionNum);
        Spacing();

        if (CollapsingHeader("Help"))
        {
            SeparatorText("ABOUT THIS DEMO:");
            BulletText("Sections below are demonstrating many aspects of the library.");
            BulletText("The \"Examples\" menu above leads to more demo contents.");
            BulletText("The \"Tools\" menu above gives access to: About Box, Style Editor,\nand Metrics/Debugger (general purpose Dear ImGui debugging tool).");
            BulletText("Web demo (w/ source code browser): ");
            SameLine(0, 0);
            TextLinkOpenURL("https://pthom.github.io/imgui_explorer");

            SeparatorText("PROGRAMMER GUIDE:");
            BulletText("See the ShowDemoWindow() code in imgui_demo.cpp. <- you are here!");
            BulletText("See comments in imgui.cpp.");
            BulletText("See example applications in the examples/ folder.");
            BulletText("Read the FAQ at ");
            SameLine(0, 0);
            TextLinkOpenURL("https://www.dearimgui.com/faq/");
            BulletText("Set 'io.ConfigFlags |= NavEnableKeyboard' for keyboard controls.");
            BulletText("Set 'io.ConfigFlags |= NavEnableGamepad' for gamepad controls.");

            SeparatorText("USER GUIDE:");
            ShowUserGuide();
        }

        if (CollapsingHeader("Configuration"))
        {
            ImGuiIO io = GetIO();

            if (TreeNode("Configuration##2"))
            {
                SeparatorText("General");
                CheckboxFlags("io.ConfigFlags: NavEnableKeyboard", ref io.ConfigFlags, ImGuiConfigFlags_NavEnableKeyboard);
                SameLine(); HelpMarker("Enable keyboard controls.");
                CheckboxFlags("io.ConfigFlags: NavEnableGamepad", ref io.ConfigFlags, ImGuiConfigFlags_NavEnableGamepad);
                SameLine(); HelpMarker("Enable gamepad controls. Require backend to set io.BackendFlags |= ImGuiBackendFlags_HasGamepad.\n\nRead instructions in imgui.cpp for details.");
                CheckboxFlags("io.ConfigFlags: NoMouse", ref io.ConfigFlags, ImGuiConfigFlags_NoMouse);
                SameLine(); HelpMarker("Instruct dear imgui to disable mouse inputs and interactions.");

                // The "NoMouse" option can get us stuck with a disabled mouse! Let's provide an alternative way to fix it:
                if ((io.ConfigFlags & ImGuiConfigFlags_NoMouse) != 0)
                {
                    if (ImMath.ImFmod((float)GetTime(), 0.40f) < 0.20f)
                    {
                        SameLine();
                        Text("<<PRESS SPACE TO DISABLE>>");
                    }
                    // Prevent both being checked
                    if (IsKeyPressed(ImGuiKey_Space) || (io.ConfigFlags & ImGuiConfigFlags_NoKeyboard) != 0)
                        io.ConfigFlags &= ~ImGuiConfigFlags_NoMouse;
                }

                CheckboxFlags("io.ConfigFlags: NoMouseCursorChange", ref io.ConfigFlags, ImGuiConfigFlags_NoMouseCursorChange);
                SameLine(); HelpMarker("Instruct backend to not alter mouse cursor shape and visibility.");
                CheckboxFlags("io.ConfigFlags: NoKeyboard", ref io.ConfigFlags, ImGuiConfigFlags_NoKeyboard);
                SameLine(); HelpMarker("Instruct dear imgui to disable keyboard inputs and interactions.");

                Checkbox("io.ConfigInputTrickleEventQueue", ref io.ConfigInputTrickleEventQueue);
                SameLine(); HelpMarker("Enable input queue trickling: some types of events submitted during the same frame (e.g. button down + up) will be spread over multiple frames, improving interactions with low framerates.");
                Checkbox("io.MouseDrawCursor", ref io.MouseDrawCursor);
                SameLine(); HelpMarker("Instruct Dear ImGui to render a mouse cursor itself. Note that a mouse cursor rendered via your application GPU rendering path will feel more laggy than hardware cursor, but will be more in sync with your other visuals.\n\nSome desktop applications may use both kinds of cursors (e.g. enable software cursor only when resizing/dragging something).");

                SeparatorText("Keyboard/Gamepad Navigation");
                Checkbox("io.ConfigNavSwapGamepadButtons", ref io.ConfigNavSwapGamepadButtons);
                Checkbox("io.ConfigNavMoveSetMousePos", ref io.ConfigNavMoveSetMousePos);
                SameLine(); HelpMarker("Directional/tabbing navigation teleports the mouse cursor. May be useful on TV/console systems where moving a virtual mouse is difficult");
                Checkbox("io.ConfigNavCaptureKeyboard", ref io.ConfigNavCaptureKeyboard);
                Checkbox("io.ConfigNavEscapeClearFocusItem", ref io.ConfigNavEscapeClearFocusItem);
                SameLine(); HelpMarker("Pressing Escape clears focused item.");
                Checkbox("io.ConfigNavEscapeClearFocusWindow", ref io.ConfigNavEscapeClearFocusWindow);
                SameLine(); HelpMarker("Pressing Escape clears focused window.");
                Checkbox("io.ConfigNavCursorVisibleAuto", ref io.ConfigNavCursorVisibleAuto);
                SameLine(); HelpMarker("Using directional navigation key makes the cursor visible. Mouse click hides the cursor.");
                Checkbox("io.ConfigNavCursorVisibleAlways", ref io.ConfigNavCursorVisibleAlways);
                SameLine(); HelpMarker("Navigation cursor is always visible.");

                SeparatorText("Windows");
                Checkbox("io.ConfigWindowsResizeFromEdges", ref io.ConfigWindowsResizeFromEdges);
                SameLine(); HelpMarker("Enable resizing of windows from their edges and from the lower-left corner.\nThis requires ImGuiBackendFlags_HasMouseCursors for better mouse cursor feedback.");
                Checkbox("io.ConfigWindowsMoveFromTitleBarOnly", ref io.ConfigWindowsMoveFromTitleBarOnly);
                Checkbox("io.ConfigWindowsCopyContentsWithCtrlC", ref io.ConfigWindowsCopyContentsWithCtrlC); // [EXPERIMENTAL]
                SameLine(); HelpMarker("*EXPERIMENTAL* Ctrl+C copy the contents of focused window into the clipboard.\n\nExperimental because:\n- (1) has known issues with nested Begin/End pairs.\n- (2) text output quality varies.\n- (3) text output is in submission order rather than spatial order.");
                Checkbox("io.ConfigScrollbarScrollByPage", ref io.ConfigScrollbarScrollByPage);
                SameLine(); HelpMarker("Enable scrolling page by page when clicking outside the scrollbar grab.\nWhen disabled, always scroll to clicked location.\nWhen enabled, Shift+Click scrolls to clicked location.");

                SeparatorText("Widgets");
                Checkbox("io.ConfigInputTextCursorBlink", ref io.ConfigInputTextCursorBlink);
                SameLine(); HelpMarker("Enable blinking cursor (optional as some users consider it to be distracting).");
                Checkbox("io.ConfigInputTextEnterKeepActive", ref io.ConfigInputTextEnterKeepActive);
                SameLine(); HelpMarker("Pressing Enter will reactivate item and select all text (single-line only).");
                Checkbox("io.ConfigDragClickToInputText", ref io.ConfigDragClickToInputText);
                SameLine(); HelpMarker("Enable turning DragXXX widgets into text input with a simple mouse click-release (without moving).");
                Checkbox("io.ConfigMacOSXBehaviors", ref io.ConfigMacOSXBehaviors);
                SameLine(); HelpMarker("Swap Cmd<>Ctrl keys, enable various MacOS style behaviors.");
                Text("Also see Style->Rendering for rendering options.");

                SeparatorText("Settings");
                Checkbox("io.ConfigIniSettingsSaveLastUsedDate", ref io.ConfigIniSettingsSaveLastUsedDate);

                // Also read: https://github.com/ocornut/imgui/wiki/Error-Handling
                SeparatorText("Error Handling");

                Checkbox("io.ConfigErrorRecovery", ref io.ConfigErrorRecovery);
                SameLine(); HelpMarker(
                    "Options to configure how we handle recoverable errors.\n" +
                    "- Error recovery is not perfect nor guaranteed! It is a feature to ease development.\n" +
                    "- You not are not supposed to rely on it in the course of a normal application run.\n" +
                    "- Possible usage: facilitate recovery from errors triggered from a scripting language or after specific exceptions handlers.\n" +
                    "- Always ensure that on programmers seat you have at minimum Asserts or Tooltips enabled when making direct imgui API call! " +
                    "Otherwise it would severely hinder your ability to catch and correct mistakes!");
                Checkbox("io.ConfigErrorRecoveryEnableAssert", ref io.ConfigErrorRecoveryEnableAssert);
                Checkbox("io.ConfigErrorRecoveryEnableDebugLog", ref io.ConfigErrorRecoveryEnableDebugLog);
                Checkbox("io.ConfigErrorRecoveryEnableTooltip", ref io.ConfigErrorRecoveryEnableTooltip);
                if (!io.ConfigErrorRecoveryEnableAssert && !io.ConfigErrorRecoveryEnableDebugLog && !io.ConfigErrorRecoveryEnableTooltip)
                    io.ConfigErrorRecoveryEnableAssert = io.ConfigErrorRecoveryEnableDebugLog = io.ConfigErrorRecoveryEnableTooltip = true;

                // Also read: https://github.com/ocornut/imgui/wiki/Debug-Tools
                SeparatorText("Debug");
                Checkbox("io.ConfigDebugIsDebuggerPresent", ref io.ConfigDebugIsDebuggerPresent);
                SameLine(); HelpMarker("Enable various tools calling IM_DEBUG_BREAK().\n\nRequires a debugger being attached, otherwise IM_DEBUG_BREAK() options will appear to crash your application.");
                Checkbox("io.ConfigDebugHighlightIdConflicts", ref io.ConfigDebugHighlightIdConflicts);
                SameLine(); HelpMarker("Highlight and show an error message when multiple items have conflicting identifiers.");
                BeginDisabled(true);
                Checkbox("io.ConfigDebugBeginReturnValueOnce", ref io.ConfigDebugBeginReturnValueOnce);
                EndDisabled();
                SameLine(); HelpMarker("First calls to Begin()/BeginChild() will return false.\n\nTHIS OPTION IS DISABLED because it needs to be set at application boot-time to make sense. Showing the disabled option is a way to make this feature easier to discover.");
                Checkbox("io.ConfigDebugBeginReturnValueLoop", ref io.ConfigDebugBeginReturnValueLoop);
                SameLine(); HelpMarker("Some calls to Begin()/BeginChild() will return false.\n\nWill cycle through window depths then repeat. Windows should be flickering while running.");
                Checkbox("io.ConfigDebugIgnoreFocusLoss", ref io.ConfigDebugIgnoreFocusLoss);
                SameLine(); HelpMarker("Option to deactivate io.AddFocusEvent(false) handling. May facilitate interactions with a debugger when focus loss leads to clearing inputs data.");
                Checkbox("io.ConfigDebugIniSettings", ref io.ConfigDebugIniSettings);
                SameLine(); HelpMarker("Option to save .ini data with extra comments (particularly helpful for Docking, but makes saving slower).");

                TreePop();
                Spacing();
            }

            if (TreeNode("Backend Flags"))
            {
                HelpMarker(
                    "Those flags are set by the backends (imgui_impl_xxx files) to specify their capabilities.\n" +
                    "Here we expose them as read-only fields to avoid breaking interactions with your backend.");

                BeginDisabled(true);
                CheckboxFlags("io.BackendFlags: HasGamepad", ref io.BackendFlags, ImGuiBackendFlags_HasGamepad);
                CheckboxFlags("io.BackendFlags: HasMouseCursors", ref io.BackendFlags, ImGuiBackendFlags_HasMouseCursors);
                CheckboxFlags("io.BackendFlags: HasSetMousePos", ref io.BackendFlags, ImGuiBackendFlags_HasSetMousePos);
                CheckboxFlags("io.BackendFlags: RendererHasVtxOffset", ref io.BackendFlags, ImGuiBackendFlags_RendererHasVtxOffset);
                CheckboxFlags("io.BackendFlags: RendererHasTextures", ref io.BackendFlags, ImGuiBackendFlags_RendererHasTextures);
                EndDisabled();

                TreePop();
                Spacing();
            }

            if (TreeNode("Style, Fonts"))
            {
                Checkbox("Style Editor", ref demo_data.ShowStyleEditor);
                SameLine();
                HelpMarker("The same contents can be accessed in 'Tools->Style Editor' or by calling the ShowStyleEditor() function.");
                TreePop();
                Spacing();
            }

            if (TreeNode("Capture/Logging"))
            {
                HelpMarker(
                    "The logging API redirects all text output so you can easily capture the content of " +
                    "a window or a block. Tree nodes can be automatically expanded.\n" +
                    "Try opening any of the contents below in this window and then click one of the \"Log To\" button.");
                LogButtons();

                HelpMarker("You can also call ImGui::LogText() to output directly to the log without a visual output.");
                if (Button("Copy \"Hello, world!\" to clipboard"))
                {
                    LogToClipboard();
                    LogText("Hello, world!");
                    LogFinish();
                }
                TreePop();
            }
        }

        if (CollapsingHeader("Window options"))
        {
            if (BeginTable("split", 3))
            {
                TableNextColumn(); Checkbox("No titlebar", ref no_titlebar);
                TableNextColumn(); Checkbox("No scrollbar", ref no_scrollbar);
                TableNextColumn(); Checkbox("No menu", ref no_menu);
                TableNextColumn(); Checkbox("No move", ref no_move);
                TableNextColumn(); Checkbox("No resize", ref no_resize);
                TableNextColumn(); Checkbox("No collapse", ref no_collapse);
                TableNextColumn(); Checkbox("No close", ref no_close);
                TableNextColumn(); Checkbox("No nav", ref no_nav);
                TableNextColumn(); Checkbox("No background", ref no_background);
                TableNextColumn(); Checkbox("No bring to front", ref no_bring_to_front);
                TableNextColumn(); Checkbox("Unsaved document", ref unsaved_document);
                EndTable();
            }
            _demoNoTitlebar = no_titlebar; _demoNoScrollbar = no_scrollbar; _demoNoMenu = no_menu;
            _demoNoMove = no_move; _demoNoResize = no_resize; _demoNoCollapse = no_collapse;
            _demoNoClose = no_close; _demoNoNav = no_nav; _demoNoBackground = no_background;
            _demoNoBringToFront = no_bring_to_front; _demoUnsavedDocument = unsaved_document;
        }

        // All demo contents
        DemoWindowWidgets(demo_data);
        DemoWindowLayout();
        DemoWindowPopups();
        DemoWindowTables();
        DemoWindowInputs();

        // End of ShowDemoWindow()
        PopItemWidth();
        End();
    }

    // C++: static void DemoWindowMenuBar(ImGuiDemoWindowData* demo_data)  (imgui_demo.cpp 671-740)
    static void DemoWindowMenuBar(ImGuiDemoWindowData demo_data)
    {
        if (BeginMenuBar())
        {
            if (BeginMenu("Menu"))
            {
                ShowExampleMenuFile();
                EndMenu();
            }
            if (BeginMenu("Examples"))
            {
                MenuItem("Main menu bar", null, ref demo_data.ShowMainMenuBar);

                SeparatorText("Mini apps");
                MenuItem("Assets Browser", null, ref demo_data.ShowAppAssetsBrowser);
                MenuItem("Console", null, ref demo_data.ShowAppConsole);
                MenuItem("Custom rendering", null, ref demo_data.ShowAppCustomRendering);
                MenuItem("Documents", null, ref demo_data.ShowAppDocuments);
                MenuItem("Image Viewer", null, ref demo_data.ShowAppImageViewer);
                MenuItem("Log", null, ref demo_data.ShowAppLog);
                MenuItem("Property editor", null, ref demo_data.ShowAppPropertyEditor);
                MenuItem("Simple layout", null, ref demo_data.ShowAppLayout);
                MenuItem("Simple overlay", null, ref demo_data.ShowAppSimpleOverlay);

                SeparatorText("Concepts");
                MenuItem("Auto-resizing window", null, ref demo_data.ShowAppAutoResize);
                MenuItem("Constrained-resizing window", null, ref demo_data.ShowAppConstrainedResize);
                MenuItem("Fullscreen window", null, ref demo_data.ShowAppFullscreen);
                MenuItem("Long text display", null, ref demo_data.ShowAppLongText);
                MenuItem("Manipulating window titles", null, ref demo_data.ShowAppWindowTitles);

                EndMenu();
            }
            //if (ImGui::MenuItem("MenuItem")) {} // You can also use MenuItem() inside a menu bar!
            if (BeginMenu("Tools"))
            {
                ImGuiIO io = GetIO();
                bool has_debug_tools = true;
                MenuItem("Metrics/Debugger", null, ref demo_data.ShowMetrics, has_debug_tools);
                if (BeginMenu("Debug Options"))
                {
                    BeginDisabled(!has_debug_tools);
                    Checkbox("Highlight ID Conflicts", ref io.ConfigDebugHighlightIdConflicts);
                    EndDisabled();
                    Checkbox("Assert on error recovery", ref io.ConfigErrorRecoveryEnableAssert);
                    TextDisabled("(see Demo->Configuration for more)");
                    EndMenu();
                }
                MenuItem("Debug Log", null, ref demo_data.ShowDebugLog, has_debug_tools);
                MenuItem("ID Stack Tool", null, ref demo_data.ShowIDStackTool, has_debug_tools);
                bool is_debugger_present = io.ConfigDebugIsDebuggerPresent;
                if (MenuItem("Item Picker", null, false, has_debug_tools))// && is_debugger_present))
                    DebugStartItemPicker();
                if (!is_debugger_present)
                    SetItemTooltip("Requires io.ConfigDebugIsDebuggerPresent=true to be set.\n\nWe otherwise disable some extra features to avoid casual users crashing the application.");
                MenuItem("Style Editor", null, ref demo_data.ShowStyleEditor);
                MenuItem("About Dear ImGui", null, ref demo_data.ShowAbout);

                EndMenu();
            }
            EndMenuBar();
        }
    }
}
