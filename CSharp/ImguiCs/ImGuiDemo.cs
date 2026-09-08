// Port of imgui_demo.cpp [SECTION] DEMO WINDOW HELPERS + small helper windows:
// HelpMarker, ShowUserGuide, ShowStyleSelector, ShowFontSelector, ShowAboutWindow, ShowStyleEditor.
// ShowDemoWindow lands in a later pass.
using System;
using System.Text;

namespace ImGui;

public static partial class ImGui
{
    // imgui_demo.cpp: static void HelpMarker(const char* desc) — demo helper (also used by imgui.cpp via MetricsHelpMarker twin).
    internal static void HelpMarker(string desc)
    {
        TextDisabled("(?)");
        if (BeginItemTooltip())
        {
            PushTextWrapPos(GetFontSize() * 35.0f);
            TextUnformatted(desc);
            PopTextWrapPos();
            EndTooltip();
        }
    }

    // imgui.cpp: static void MetricsHelpMarker(const char* desc) — same shape, used by debug windows.
    internal static void MetricsHelpMarker(string desc) => HelpMarker(desc);

    // C++: void ImGui::ShowUserGuide()
    public static void ShowUserGuide()
    {
        ImGuiIO io = GetIO();
        BulletText("Double-click on title bar to collapse window.");
        BulletText(
            "Click and drag on lower corner or border to resize window.\n" +
            "(double-click to auto fit window to its contents)");
        BulletText("Ctrl+Click on a slider or drag box to input value as text.");
        BulletText("Tab/Shift+Tab to cycle through keyboard editable fields.");
        BulletText("Ctrl+Tab/Ctrl+Shift+Tab to focus windows.");
        if (io.FontAllowUserScaling)
            BulletText("Ctrl+Mouse Wheel to zoom window contents.");
        BulletText("While inputting text:\n");
        Indent();
        BulletText("Ctrl+Left/Right to word jump.");
        BulletText("Ctrl+A or double-click to select all.");
        BulletText("Ctrl+X/C/V to use clipboard cut/copy/paste.");
        BulletText("Ctrl+Z to undo, Ctrl+Y/Ctrl+Shift+Z to redo.");
        BulletText("Escape to revert.");
        Unindent();
        BulletText("With Keyboard controls enabled:");
        Indent();
        BulletText("Arrow keys or Home/End/PageUp/PageDown to navigate.");
        BulletText("Space to activate a widget.");
        BulletText("Return to input text into a widget.");
        BulletText("Escape to deactivate a widget, close popup,\nexit a child window or the menu layer, clear focus.");
        BulletText("Alt to jump to the menu layer of a window.");
        BulletText("Menu or Shift+F10 to open a context menu.");
        Unindent();
        BulletText("With Gamepad controls enabled:");
        Indent();
        BulletText("D-Pad: Navigate / Tweak / Resize (in Windowing mode).");
        BulletText("%s Face button: Activate / Open / Toggle. Hold: activate with text input.", io.ConfigNavSwapGamepadButtons ? "East" : "South");
        BulletText("%s Face button: Cancel / Close / Exit.", io.ConfigNavSwapGamepadButtons ? "South" : "East");
        BulletText("West Face button: Toggle Menu. Hold for Windowing mode (Focus/Move/Resize windows).");
        BulletText("North Face button: Open Context Menu.");
        BulletText("L1/R1: Tweak Slower/Faster, Focus Previous/Next (in Windowing Mode).");
        Unindent();
    }

    // imgui_demo.cpp: static int style_idx = -1 (function-local static)
    static int _styleSelectorIdx = -1;

    // C++: bool ImGui::ShowStyleSelector(const char* label)
    public static bool ShowStyleSelector(string label)
    {
        // FIXME: This is a bit tricky to get right as styles are functions, they don't register a name nor the fact that one is active.
        // So we keep track of last active one among our limited selection.
        string[] style_names = { "Dark", "Light", "Classic" };
        bool ret = false;
        if (BeginCombo(label, (_styleSelectorIdx >= 0 && _styleSelectorIdx < style_names.Length) ? style_names[_styleSelectorIdx] : ""))
        {
            for (int n = 0; n < style_names.Length; n++)
            {
                if (Selectable(style_names[n], _styleSelectorIdx == n, ImGuiSelectableFlags_SelectOnNav))
                {
                    _styleSelectorIdx = n;
                    ret = true;
                    switch (_styleSelectorIdx)
                    {
                        case 0: StyleColorsDark(); break;
                        case 1: StyleColorsLight(); break;
                        case 2: StyleColorsClassic(); break;
                    }
                }
                else if (_styleSelectorIdx == n)
                    SetItemDefaultFocus();
            }
            EndCombo();
        }
        return ret;
    }

    // Demo helper function to select among loaded fonts.
    // Here we use the regular BeginCombo()/EndCombo() api which is the more flexible one.
    // C++: void ImGui::ShowFontSelector(const char* label)
    public static void ShowFontSelector(string label)
    {
        ImGuiIO io = GetIO();
        ImFont font_current = GetFont();
        if (BeginCombo(label, font_current.GetDebugName()))
        {
            for (int font_i = 0; font_i < io.Fonts.Fonts.Size; font_i++)
            {
                ImFont font = io.Fonts.Fonts[font_i];
                PushID(font);
                if (Selectable(font.GetDebugName(), font == font_current, ImGuiSelectableFlags_SelectOnNav))
                    io.FontDefault = font;
                if (font == font_current)
                    SetItemDefaultFocus();
                PopID();
            }
            EndCombo();
        }
        SameLine();
        if ((io.BackendFlags & ImGuiBackendFlags_RendererHasTextures) != 0)
            MetricsHelpMarker(
                "- Load additional fonts with io.Fonts->AddFontXXX() functions.\n" +
                "- Read FAQ and docs/FONTS.md for more details.");
        else
            MetricsHelpMarker(
                "- Load additional fonts with io.Fonts->AddFontXXX() functions.\n" +
                "- The font atlas is built when calling io.Fonts->GetTexDataAsXXXX() or io.Fonts->Build().\n" +
                "- Read FAQ and docs/FONTS.md for more details.\n" +
                "- Legacy backend: if you need to add/remove fonts at runtime (e.g. for DPI change), do it before calling NewFrame().");
    }

    // imgui_demo.cpp: static bool show_config_info = false (function-local static)
    static bool _aboutShowConfigInfo = false;

    // C++: void ImGui::ShowAboutWindow(bool* p_open)
    public static void ShowAboutWindow(ref bool p_open) => ShowAboutWindow(ref p_open, null);
    public static void ShowAboutWindow() => ShowAboutWindow(ref _aboutP_openDummy, null);
    static bool _aboutP_openDummy;
    public static void ShowAboutWindow(string label) => ShowAboutWindow(ref _aboutP_openDummy, label);

    public static void ShowAboutWindow(ref bool p_open, string label)
    {
        if (!Begin(label ?? "About Dear ImGui", ref p_open, ImGuiWindowFlags_AlwaysAutoResize))
        {
            End();
            return;
        }
        Text("Dear ImGui %s (%d)", ImGuiVersion, ImGuiVersionNum);

        TextLinkOpenURL("Homepage", "https://github.com/ocornut/imgui");
        SameLine();
        TextLinkOpenURL("FAQ", "https://github.com/ocornut/imgui/blob/master/docs/FAQ.md");
        SameLine();
        TextLinkOpenURL("Wiki", "https://github.com/ocornut/imgui/wiki");
        SameLine();
        TextLinkOpenURL("Extensions", "https://github.com/ocornut/imgui/wiki/Useful-Extensions");
        SameLine();
        TextLinkOpenURL("Releases", "https://github.com/ocornut/imgui/releases");
        SameLine();
        TextLinkOpenURL("Funding", "https://github.com/ocornut/imgui/wiki/Funding");

        Separator();
        Text("(c) 2014-2026 Omar Cornut");
        Text("Developed by Omar Cornut and all Dear ImGui contributors.");
        Text("Dear ImGui is licensed under the MIT License, see LICENSE for more information.");
        Text("If your company uses this, please consider funding the project.");

        Checkbox("Config/Build Information", ref _aboutShowConfigInfo);
        if (_aboutShowConfigInfo)
        {
            ImGuiIO io = GetIO();
            ImGuiStyle style = GetStyle();

            bool copy_to_clipboard = Button("Copy to clipboard");
            ImVec2 child_size = new ImVec2(0, GetTextLineHeightWithSpacing() * 18);
            BeginChild(GetID("cfg_infos"), child_size, ImGuiChildFlags_FrameStyle);
            if (copy_to_clipboard)
            {
                LogToClipboard();
                LogText("// (Copy from the next line. Keep the ``` markers for formatting.)\n");
                LogText("```cpp\n"); // Back quotes will make text appears without formatting when pasting on GitHub
            }

            Text("Dear ImGui %s (%d)", ImGuiVersion, ImGuiVersionNum);
            Separator();
            Text("sizeof(size_t): %d, sizeof(ImDrawIdx): %d, sizeof(ImDrawVert): %d", 8, 2, 20);
            Text("define: ported to C#/.NET (%s)", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            Text("IM_ASSERT: runs expression: OK. expand size: OK");

            Separator();
            Text("io.BackendPlatformName: %s", io.BackendPlatformName ?? "NULL");
            Text("io.BackendRendererName: %s", io.BackendRendererName ?? "NULL");
            Text("io.ConfigFlags: 0x%08X", io.ConfigFlags);
            if ((io.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0) Text(" NavEnableKeyboard");
            if ((io.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0) Text(" NavEnableGamepad");
            if ((io.ConfigFlags & ImGuiConfigFlags_NoMouse) != 0) Text(" NoMouse");
            if ((io.ConfigFlags & ImGuiConfigFlags_NoMouseCursorChange) != 0) Text(" NoMouseCursorChange");
            if ((io.ConfigFlags & ImGuiConfigFlags_NoKeyboard) != 0) Text(" NoKeyboard");
            if (io.MouseDrawCursor) Text("io.MouseDrawCursor");
            if (io.ConfigMacOSXBehaviors) Text("io.ConfigMacOSXBehaviors");
            if (io.ConfigNavMoveSetMousePos) Text("io.ConfigNavMoveSetMousePos");
            if (io.ConfigNavCaptureKeyboard) Text("io.ConfigNavCaptureKeyboard");
            if (io.ConfigInputTextCursorBlink) Text("io.ConfigInputTextCursorBlink");
            if (io.ConfigWindowsResizeFromEdges) Text("io.ConfigWindowsResizeFromEdges");
            if (io.ConfigWindowsMoveFromTitleBarOnly) Text("io.ConfigWindowsMoveFromTitleBarOnly");
            if (io.ConfigMemoryCompactTimer >= 0.0f) Text("io.ConfigMemoryCompactTimer = %.1f", io.ConfigMemoryCompactTimer);
            Text("io.BackendFlags: 0x%08X", io.BackendFlags);
            if ((io.BackendFlags & ImGuiBackendFlags_HasGamepad) != 0) Text(" HasGamepad");
            if ((io.BackendFlags & ImGuiBackendFlags_HasMouseCursors) != 0) Text(" HasMouseCursors");
            if ((io.BackendFlags & ImGuiBackendFlags_HasSetMousePos) != 0) Text(" HasSetMousePos");
            if ((io.BackendFlags & ImGuiBackendFlags_RendererHasVtxOffset) != 0) Text(" RendererHasVtxOffset");
            if ((io.BackendFlags & ImGuiBackendFlags_RendererHasTextures) != 0) Text(" RendererHasTextures");
            Separator();
            Text("io.Fonts: %d fonts, Flags: 0x%08X, TexSize: %d,%d", io.Fonts.Fonts.Size, (int)io.Fonts.Flags, io.Fonts.TexData.Width, io.Fonts.TexData.Height);
            Text("io.Fonts->FontLoaderName: %s", io.Fonts.FontLoaderName ?? "NULL");
            Text("io.DisplaySize: %.2f,%.2f", io.DisplaySize.x, io.DisplaySize.y);
            Text("io.DisplayFramebufferScale: %.2f,%.2f", io.DisplayFramebufferScale.x, io.DisplayFramebufferScale.y);
            Separator();
            Text("style.WindowPadding: %.2f,%.2f", style.WindowPadding.x, style.WindowPadding.y);
            Text("style.WindowBorderSize: %.2f", style.WindowBorderSize);
            Text("style.FramePadding: %.2f,%.2f", style.FramePadding.x, style.FramePadding.y);
            Text("style.FrameRounding: %.2f", style.FrameRounding);
            Text("style.FrameBorderSize: %.2f", style.FrameBorderSize);
            Text("style.ItemSpacing: %.2f,%.2f", style.ItemSpacing.x, style.ItemSpacing.y);
            Text("style.ItemInnerSpacing: %.2f,%.2f", style.ItemInnerSpacing.x, style.ItemInnerSpacing.y);

            if (copy_to_clipboard)
            {
                LogText("\n```\n");
                LogFinish();
            }
            EndChild();
        }
        End();
    }

    // ---- Style Editor (imgui_demo.cpp 8537-8895) ----

    // C++: static const char* GetTreeLinesFlagsName(ImGuiTreeNodeFlags flags)
    static string GetTreeLinesFlagsName(int flags)
    {
        if (flags == ImGuiTreeNodeFlags_DrawLinesNone) return "DrawLinesNone";
        if (flags == ImGuiTreeNodeFlags_DrawLinesFull) return "DrawLinesFull";
        if (flags == ImGuiTreeNodeFlags_DrawLinesToNodes) return "DrawLinesToNodes";
        return "";
    }

    // C++ ImGuiStyle is a value type in ShowStyleEditor (`style = *ref`); the C# class needs a deep-ish copy.
    static void CopyStyle(ImGuiStyle dst, ImGuiStyle src)
    {
        foreach (var f in typeof(ImGuiStyle).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            f.SetValue(dst, f.GetValue(src));
        dst.Colors = (ImVec4[])src.Colors.Clone(); // avoid sharing the color array
    }

    // imgui_demo.cpp function-local statics
    static ImGuiStyle _styleEditorRefSavedStyle;
    static bool _styleEditorInit = true;
    static int _styleEditorOutputDest = 0;
    static bool _styleEditorOutputOnlyModified = true;
    static ImGuiTextFilter _styleEditorFilter = new ImGuiTextFilter();
    static int _styleEditorAlphaFlags = 0;

    // We omit the ImGui:: prefix in this function, as we don't expect user to be copy and pasting this code.
    // C++: void ImGui::ShowStyleEditor(ImGuiStyle* ref)
    public static void ShowStyleEditor(ImGuiStyle ref_style = null)
    {
        // You can pass in a reference ImGuiStyle structure to compare to, revert to and save to
        // (without a reference style pointer, we will use one compared locally as a reference)
        ImGuiStyle style = GetStyle();
        if (_styleEditorRefSavedStyle == null)
            _styleEditorRefSavedStyle = new ImGuiStyle();

        // Default to using internal storage as reference
        if (_styleEditorInit && ref_style == null)
            CopyStyle(_styleEditorRefSavedStyle, style);
        _styleEditorInit = false;
        if (ref_style == null)
            ref_style = _styleEditorRefSavedStyle;

        // The logic behind dynamically changing 'max_border_size' is to not encourage people to increase border size too much.
        float default_border_size = (float)(int)style._MainScale;
        float max_border_size = ImMath.ImMax(default_border_size, 2.0f);

        PushItemWidth(GetWindowWidth() * 0.50f);

        {
            // General
            SeparatorText("General");
            if ((GetIO().BackendFlags & ImGuiBackendFlags_RendererHasTextures) == 0)
            {
                BulletText("Warning: Font scaling will NOT be smooth, because\nImGuiBackendFlags_RendererHasTextures is not set!");
                BulletText("For instructions, see:");
                SameLine();
                TextLinkOpenURL("docs/BACKENDS.md", "https://github.com/ocornut/imgui/blob/master/docs/BACKENDS.md");
            }

            if (ShowStyleSelector("Colors##Selector"))
                CopyStyle(_styleEditorRefSavedStyle, style);
            ShowFontSelector("Fonts##Selector");
            if (DragFloat("FontSizeBase", ref style.FontSizeBase, 0.20f, 5.0f, 100.0f, "%.0f"))
                style._NextFrameFontSizeBase = style.FontSizeBase; // FIXME: Temporary hack until we finish remaining work.
            SameLine(0.0f, 0.0f); Text(" (out %.2f)", GetFontSize());
            DragFloat("FontScaleMain", ref style.FontScaleMain, 0.02f, 0.5f, 4.0f);
            DragFloat("FontScaleDpi", ref style.FontScaleDpi, 0.02f, 0.5f, 4.0f);

            // Simplified Settings (expose floating-pointer border sizes as boolean representing 0.0f or 1.0f)
            if (SliderFloat("FrameRounding", ref style.FrameRounding, 0.0f, 12.0f, "%.0f"))
                style.GrabRounding = style.FrameRounding; // Make GrabRounding always the same value as FrameRounding
            { bool border = (style.WindowBorderSize > 0.0f); if (Checkbox("WindowBorder", ref border)) { style.WindowBorderSize = border ? default_border_size : 0.0f; } }
            SameLine();
            { bool border = (style.FrameBorderSize > 0.0f); if (Checkbox("FrameBorder", ref border)) { style.FrameBorderSize = border ? default_border_size : 0.0f; } }
            SameLine();
            { bool border = (style.PopupBorderSize > 0.0f); if (Checkbox("PopupBorder", ref border)) { style.PopupBorderSize = border ? default_border_size : 0.0f; } }
        }

        // Save/Revert button
        if (Button("Save Ref"))
        {
            CopyStyle(_styleEditorRefSavedStyle, style);
            CopyStyle(ref_style, _styleEditorRefSavedStyle);
        }
        SameLine();
        if (Button("Revert Ref"))
            CopyStyle(style, ref_style);
        SameLine();
        HelpMarker(
            "Save/Revert in local non-persistent storage. Default Colors definition are not affected. " +
            "Use \"Export\" below to save them somewhere.");

        SeparatorText("Details");
        if (BeginTabBar("##tabs", ImGuiTabBarFlags_None))
        {
            if (BeginTabItem("Sizes"))
            {
                SeparatorText("Main");
                {
                    float[] v = { style.WindowPadding.x, style.WindowPadding.y };
                    if (SliderFloat2("WindowPadding", v, 0.0f, 20.0f, "%.0f")) style.WindowPadding = new ImVec2(v[0], v[1]);
                }
                {
                    float[] v = { style.FramePadding.x, style.FramePadding.y };
                    if (SliderFloat2("FramePadding", v, 0.0f, 20.0f, "%.0f")) style.FramePadding = new ImVec2(v[0], v[1]);
                }
                {
                    float[] v = { style.ItemSpacing.x, style.ItemSpacing.y };
                    if (SliderFloat2("ItemSpacing", v, 0.0f, 20.0f, "%.0f")) style.ItemSpacing = new ImVec2(v[0], v[1]);
                }
                {
                    float[] v = { style.ItemInnerSpacing.x, style.ItemInnerSpacing.y };
                    if (SliderFloat2("ItemInnerSpacing", v, 0.0f, 20.0f, "%.0f")) style.ItemInnerSpacing = new ImVec2(v[0], v[1]);
                }
                {
                    float[] v = { style.TouchExtraPadding.x, style.TouchExtraPadding.y };
                    if (SliderFloat2("TouchExtraPadding", v, 0.0f, 10.0f, "%.0f")) style.TouchExtraPadding = new ImVec2(v[0], v[1]);
                }
                SliderFloat("IndentSpacing", ref style.IndentSpacing, 0.0f, 30.0f, "%.0f");
                SliderFloat("GrabMinSize", ref style.GrabMinSize, 1.0f, 20.0f, "%.0f");

                SeparatorText("Borders");
                SliderFloat("WindowBorderSize", ref style.WindowBorderSize, 0.0f, max_border_size, "%.0f");
                SliderFloat("ChildBorderSize", ref style.ChildBorderSize, 0.0f, max_border_size, "%.0f");
                SliderFloat("PopupBorderSize", ref style.PopupBorderSize, 0.0f, max_border_size, "%.0f");
                SliderFloat("FrameBorderSize", ref style.FrameBorderSize, 0.0f, max_border_size, "%.0f");

                SeparatorText("Rounding");
                SliderFloat("WindowRounding", ref style.WindowRounding, 0.0f, 12.0f, "%.0f");
                SliderFloat("ChildRounding", ref style.ChildRounding, 0.0f, 12.0f, "%.0f");
                SliderFloat("FrameRounding", ref style.FrameRounding, 0.0f, 12.0f, "%.0f");
                SliderFloat("PopupRounding", ref style.PopupRounding, 0.0f, 12.0f, "%.0f");
                SliderFloat("GrabRounding", ref style.GrabRounding, 0.0f, 12.0f, "%.0f");
                SliderFloat("MenuItemRounding", ref style.MenuItemRounding, 0.0f, 12.0f, "%.0f");

                SeparatorText("Scrollbar");
                SliderFloat("ScrollbarSize", ref style.ScrollbarSize, 1.0f, 20.0f, "%.0f");
                SliderFloat("ScrollbarRounding", ref style.ScrollbarRounding, 0.0f, 12.0f, "%.0f");
                SliderFloat("ScrollbarPadding", ref style.ScrollbarPadding, 0.0f, 10.0f, "%.0f");

                SeparatorText("Tabs");
                SliderFloat("TabBorderSize", ref style.TabBorderSize, 0.0f, max_border_size, "%.0f");
                SliderFloat("TabBarBorderSize", ref style.TabBarBorderSize, 0.0f, max_border_size, "%.0f");
                SliderFloat("TabBarOverlineSize", ref style.TabBarOverlineSize, 0.0f, ImMath.ImMax(3.0f, max_border_size), "%.0f");
                SameLine(); HelpMarker("Overline is only drawn over the selected tab when ImGuiTabBarFlags_DrawSelectedOverline is set.");
                DragFloat("TabMinWidthBase", ref style.TabMinWidthBase, 0.5f, 1.0f, 500.0f, "%.0f");
                DragFloat("TabMinWidthShrink", ref style.TabMinWidthShrink, 0.5f, 1.0f, 500.0f, "%0.f");
                DragFloat("TabCloseButtonMinWidthSelected", ref style.TabCloseButtonMinWidthSelected, 0.5f, -1.0f, 100.0f, (style.TabCloseButtonMinWidthSelected < 0.0f) ? "%.0f (Always)" : "%.0f");
                DragFloat("TabCloseButtonMinWidthUnselected", ref style.TabCloseButtonMinWidthUnselected, 0.5f, -1.0f, 100.0f, (style.TabCloseButtonMinWidthUnselected < 0.0f) ? "%.0f (Always)" : "%.0f");
                SliderFloat("TabRounding", ref style.TabRounding, 0.0f, 12.0f, "%.0f");

                SeparatorText("Tables");
                {
                    float[] v = { style.CellPadding.x, style.CellPadding.y };
                    if (SliderFloat2("CellPadding", v, 0.0f, 20.0f, "%.0f")) style.CellPadding = new ImVec2(v[0], v[1]);
                }
                SliderAngle("TableAngledHeadersAngle", ref style.TableAngledHeadersAngle, -50.0f, +50.0f);
                {
                    float[] v = { style.TableAngledHeadersTextAlign.x, style.TableAngledHeadersTextAlign.y };
                    if (SliderFloat2("TableAngledHeadersTextAlign", v, 0.0f, 1.0f, "%.2f")) style.TableAngledHeadersTextAlign = new ImVec2(v[0], v[1]);
                }

                SeparatorText("Trees");
                bool combo_open = BeginCombo("TreeLinesFlags", GetTreeLinesFlagsName(style.TreeLinesFlags));
                SameLine();
                HelpMarker("[Experimental] Tree lines may not work in all situations (e.g. using a clipper) and may incurs slight traversal overhead.\n\nImGuiTreeNodeFlags_DrawLinesFull is faster than ImGuiTreeNodeFlags_DrawLinesToNode.");
                if (combo_open)
                {
                    int[] options = { ImGuiTreeNodeFlags_DrawLinesNone, ImGuiTreeNodeFlags_DrawLinesFull, ImGuiTreeNodeFlags_DrawLinesToNodes };
                    for (int oi = 0; oi < options.Length; oi++)
                        if (Selectable(GetTreeLinesFlagsName(options[oi]), style.TreeLinesFlags == options[oi]))
                            style.TreeLinesFlags = options[oi];
                    EndCombo();
                }
                SliderFloat("TreeLinesSize", ref style.TreeLinesSize, 0.0f, max_border_size, "%.0f");
                SliderFloat("TreeLinesRounding", ref style.TreeLinesRounding, 0.0f, 12.0f, "%.0f");

                SeparatorText("Windows");
                {
                    float[] v = { style.WindowTitleAlign.x, style.WindowTitleAlign.y };
                    if (SliderFloat2("WindowTitleAlign", v, 0.0f, 1.0f, "%.2f")) style.WindowTitleAlign = new ImVec2(v[0], v[1]);
                }
                SliderFloat("WindowBorderHoverPadding", ref style.WindowBorderHoverPadding, 1.0f, 20.0f, "%.0f");
                int window_menu_button_position = style.WindowMenuButtonPosition + 1;
                if (Combo("WindowMenuButtonPosition", ref window_menu_button_position, "None\0Left\0Right\0"))
                    style.WindowMenuButtonPosition = window_menu_button_position - 1;

                SeparatorText("Widgets");
                SliderFloat("ColorMarkerSize", ref style.ColorMarkerSize, 0.0f, 8.0f, "%.0f");
                {
                    int cbp = style.ColorButtonPosition;
                    if (Combo("ColorButtonPosition", ref cbp, "Left\0Right\0")) style.ColorButtonPosition = cbp;
                }
                {
                    float[] v = { style.ButtonTextAlign.x, style.ButtonTextAlign.y };
                    if (SliderFloat2("ButtonTextAlign", v, 0.0f, 1.0f, "%.2f")) style.ButtonTextAlign = new ImVec2(v[0], v[1]);
                }
                SameLine(); HelpMarker("Alignment applies when a button is larger than its text content.");
                {
                    float[] v = { style.SelectableTextAlign.x, style.SelectableTextAlign.y };
                    if (SliderFloat2("SelectableTextAlign", v, 0.0f, 1.0f, "%.2f")) style.SelectableTextAlign = new ImVec2(v[0], v[1]);
                }
                SameLine(); HelpMarker("Alignment applies when a selectable is larger than its text content.");
                SliderFloat("SeparatorSize", ref style.SeparatorSize, 0.0f, 10.0f, "%.0f");
                SliderFloat("SeparatorTextBorderSize", ref style.SeparatorTextBorderSize, 0.0f, 10.0f, "%.0f");
                {
                    float[] v = { style.SeparatorTextAlign.x, style.SeparatorTextAlign.y };
                    if (SliderFloat2("SeparatorTextAlign", v, 0.0f, 1.0f, "%.2f")) style.SeparatorTextAlign = new ImVec2(v[0], v[1]);
                }
                {
                    float[] v = { style.SeparatorTextPadding.x, style.SeparatorTextPadding.y };
                    if (SliderFloat2("SeparatorTextPadding", v, 0.0f, 40.0f, "%.0f")) style.SeparatorTextPadding = new ImVec2(v[0], v[1]);
                }
                SliderFloat("LogSliderDeadzone", ref style.LogSliderDeadzone, 0.0f, 12.0f, "%.0f");
                SliderFloat("ImageRounding", ref style.ImageRounding, 0.0f, 12.0f, "%.0f");
                SliderFloat("ImageBorderSize", ref style.ImageBorderSize, 0.0f, max_border_size, "%.0f");

                SeparatorText("Tooltips");
                for (int n = 0; n < 2; n++)
                    if (TreeNode(n == 0 ? "HoverFlagsForTooltipMouse" : "HoverFlagsForTooltipNav"))
                    {
                        int hovered_flags = (n == 0) ? style.HoverFlagsForTooltipMouse : style.HoverFlagsForTooltipNav;
                        CheckboxFlags("ImGuiHoveredFlags_DelayNone", ref hovered_flags, ImGuiHoveredFlags_DelayNone);
                        CheckboxFlags("ImGuiHoveredFlags_DelayShort", ref hovered_flags, ImGuiHoveredFlags_DelayShort);
                        CheckboxFlags("ImGuiHoveredFlags_DelayNormal", ref hovered_flags, ImGuiHoveredFlags_DelayNormal);
                        CheckboxFlags("ImGuiHoveredFlags_Stationary", ref hovered_flags, ImGuiHoveredFlags_Stationary);
                        CheckboxFlags("ImGuiHoveredFlags_NoSharedDelay", ref hovered_flags, ImGuiHoveredFlags_NoSharedDelay);
                        if (n == 0) style.HoverFlagsForTooltipMouse = hovered_flags; else style.HoverFlagsForTooltipNav = hovered_flags;
                        TreePop();
                    }

                SeparatorText("Misc");
                {
                    float[] v = { style.DisplayWindowPadding.x, style.DisplayWindowPadding.y };
                    if (SliderFloat2("DisplayWindowPadding", v, 0.0f, 30.0f, "%.0f")) style.DisplayWindowPadding = new ImVec2(v[0], v[1]);
                }
                SameLine(); HelpMarker("Apply to regular windows: amount which we enforce to keep visible when moving near edges of your screen.");
                {
                    float[] v = { style.DisplaySafeAreaPadding.x, style.DisplaySafeAreaPadding.y };
                    if (SliderFloat2("DisplaySafeAreaPadding", v, 0.0f, 30.0f, "%.0f")) style.DisplaySafeAreaPadding = new ImVec2(v[0], v[1]);
                }
                SameLine(); HelpMarker("Apply to every windows, menus, popups, tooltips: amount where we avoid displaying contents. Adjust if you cannot see the edges of your screen (e.g. on a TV where scaling has not been configured).");

                EndTabItem();
            }

            if (BeginTabItem("Colors"))
            {
                if (Button("Export"))
                {
                    if (_styleEditorOutputDest == 0)
                        LogToClipboard();
                    else
                        LogToTTY();
                    LogText("ImVec4* colors = GetStyle().Colors;" + Environment.NewLine);
                    for (int i = 0; i < ImGuiCol_COUNT; i++)
                    {
                        ImVec4 col = style.Colors[i];
                        string name = GetStyleColorName(i);
                        if (!_styleEditorOutputOnlyModified || !col.Equals(ref_style.Colors[i]))
                            LogText("colors[ImGuiCol_%s]" + "".PadLeft(Math.Max(0, 23 - name.Length)) + "= ImVec4(%.2ff, %.2ff, %.2ff, %.2ff);" + Environment.NewLine,
                                name, col.x, col.y, col.z, col.w);
                    }
                    LogFinish();
                }
                SameLine(); SetNextItemWidth(GetFontSize() * 10); Combo("##output_type", ref _styleEditorOutputDest, "To Clipboard\0To TTY\0");
                SameLine(); Checkbox("Only Modified Colors", ref _styleEditorOutputOnlyModified);

                _styleEditorFilter.Draw("Filter colors", GetFontSize() * 16);

                if (RadioButton("Opaque", _styleEditorAlphaFlags == ImGuiColorEditFlags_AlphaOpaque)) { _styleEditorAlphaFlags = ImGuiColorEditFlags_AlphaOpaque; } SameLine();
                if (RadioButton("Alpha", _styleEditorAlphaFlags == ImGuiColorEditFlags_None)) { _styleEditorAlphaFlags = ImGuiColorEditFlags_None; } SameLine();
                if (RadioButton("Both", _styleEditorAlphaFlags == ImGuiColorEditFlags_AlphaPreviewHalf)) { _styleEditorAlphaFlags = ImGuiColorEditFlags_AlphaPreviewHalf; } SameLine();
                HelpMarker(
                    "In the color list:\n" +
                    "Left-click on color square to open color picker,\n" +
                    "Right-click to open edit options menu.");

                SetNextWindowSizeConstraints(new ImVec2(0.0f, GetTextLineHeightWithSpacing() * 10), new ImVec2(float.MaxValue, float.MaxValue));
                BeginChild("##colors", new ImVec2(0, 0), ImGuiChildFlags_Borders | ImGuiChildFlags_NavFlattened, ImGuiWindowFlags_AlwaysVerticalScrollbar | ImGuiWindowFlags_AlwaysHorizontalScrollbar);
                PushItemWidth(GetFontSize() * -12);
                for (int i = 0; i < ImGuiCol_COUNT; i++)
                {
                    string name = GetStyleColorName(i);
                    if (!_styleEditorFilter.PassFilter(name))
                        continue;
                    PushID(i);
                    if (Button("?"))
                        DebugFlashStyleColor(i);
                    SetItemTooltip("Flash given color to identify places where it is used.");
                    SameLine();
                    float[] col_arr = { style.Colors[i].x, style.Colors[i].y, style.Colors[i].z, style.Colors[i].w };
                    ColorEdit4("##color", col_arr, ImGuiColorEditFlags_AlphaBar | _styleEditorAlphaFlags);
                    style.Colors[i] = new ImVec4(col_arr[0], col_arr[1], col_arr[2], col_arr[3]);
                    if (!style.Colors[i].Equals(ref_style.Colors[i]))
                    {
                        SameLine(0.0f, style.ItemInnerSpacing.x); if (Button("Save")) { ref_style.Colors[i] = style.Colors[i]; }
                        SameLine(0.0f, style.ItemInnerSpacing.x); if (Button("Revert")) { style.Colors[i] = ref_style.Colors[i]; }
                    }
                    SameLine(0.0f, style.ItemInnerSpacing.x);
                    TextUnformatted(name);
                    PopID();
                }
                PopItemWidth();
                EndChild();

                EndTabItem();
            }

            if (BeginTabItem("Fonts"))
            {
                ImGuiIO io = GetIO();
                ImFontAtlas atlas = io.Fonts;
                ShowFontAtlas(atlas);

                EndTabItem();
            }

            if (BeginTabItem("Rendering"))
            {
                Checkbox("Anti-aliased lines", ref style.AntiAliasedLines);
                SameLine();
                HelpMarker("When disabling anti-aliasing lines, you'll probably want to disable borders in your style as well.");

                Checkbox("Anti-aliased lines use texture", ref style.AntiAliasedLinesUseTex);
                SameLine();
                HelpMarker("Faster lines using texture data. Require backend to render with bilinear filtering (not point/nearest filtering).");

                Checkbox("Anti-aliased fill", ref style.AntiAliasedFill);
                PushItemWidth(GetFontSize() * 8);
                DragFloat("Curve Tessellation Max Error", ref style.CurveTessellationMaxError, 0.02f, 0.10f, 10.0f, "%.2f");
                if (style.CurveTessellationMaxError < 0.10f)
                    style.CurveTessellationMaxError = 0.10f;

                // When editing the "Circle Segment Max Error" value, draw a preview of its effect on auto-tessellated circles.
                DragFloat("Circle Tessellation Max Error", ref style.CircleTessellationMaxError, 0.005f, 0.10f, 5.0f, "%.2f", ImGuiSliderFlags_AlwaysClamp);
                bool show_samples = IsItemActive();
                if (show_samples)
                    SetNextWindowPos(GetCursorScreenPos());
                if (show_samples && BeginTooltip())
                {
                    TextUnformatted("(R = radius, N = approx number of segments)");
                    Spacing();
                    ImDrawList draw_list = GetWindowDrawList();
                    float min_widget_width = CalcTextSize("R: MMM\nN: MMM").x;
                    for (int n = 0; n < 8; n++)
                    {
                        const float RAD_MIN = 5.0f;
                        const float RAD_MAX = 70.0f;
                        float rad = RAD_MIN + (RAD_MAX - RAD_MIN) * (float)n / (8.0f - 1.0f);

                        BeginGroup();

                        // N is not always exact here due to how PathArcTo() function work internally
                        Text("R: %.f\nN: %d", rad, draw_list._CalcCircleAutoSegmentCount(rad));

                        float canvas_width = ImMath.ImMax(min_widget_width, rad * 2.0f);
                        float offset_x = ImMath.ImFloor(canvas_width * 0.5f);
                        float offset_y = ImMath.ImFloor(RAD_MAX);

                        ImVec2 p1 = GetCursorScreenPos();
                        draw_list.AddCircle(new ImVec2(p1.x + offset_x, p1.y + offset_y), rad, GetColorU32(ImGuiCol_Text));
                        Dummy(new ImVec2(canvas_width, RAD_MAX * 2));

                        EndGroup();
                        SameLine();
                    }
                    EndTooltip();
                }
                SameLine();
                HelpMarker("When drawing circle primitives with \"num_segments == 0\" tessellation will be calculated automatically.");

                DragFloat("Global Alpha", ref style.Alpha, 0.005f, 0.20f, 1.0f, "%.2f"); // Not exposing zero here so user doesn't "lose" the UI (zero alpha clips all widgets). But application code could have a toggle to switch between zero and non-zero.
                DragFloat("Disabled Alpha", ref style.DisabledAlpha, 0.005f, 0.0f, 1.0f, "%.2f"); SameLine(); HelpMarker("Additional alpha multiplier for disabled items (multiply over current value of Alpha).");
                PopItemWidth();

                EndTabItem();
            }

            EndTabBar();
        }
        PopItemWidth();
    }
}
