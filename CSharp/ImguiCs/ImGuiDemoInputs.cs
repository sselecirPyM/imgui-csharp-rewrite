// Port of imgui_demo.cpp [SECTION] DemoWindowInputs() (imgui_demo.cpp lines 7998-8308):
// "Inputs & Focus" collapsing header of the Demo window: Inputs, Outputs, Shortcuts,
// Mouse Cursors, Tabbing, Focus from code, Dragging.
using System;
using System.Text;

namespace ImGui;

public static partial class ImGui
{
    // C++: static void DemoWindowInputs()   [imgui_demo.cpp 7998]
    public static void DemoWindowInputs()
    {
        if (CollapsingHeader("Inputs & Focus"))
        {
            ImGuiIO io = GetIO();

            // Display inputs submitted to ImGuiIO
            SetNextItemOpen(true, ImGuiCond_Once);
            bool inputs_opened = TreeNode("Inputs");
            SameLine();
            HelpMarker(
                "This is a simplified view. See more detailed input state:\n" +
                "- in 'Tools->Metrics/Debugger->Inputs'.\n" +
                "- in 'Tools->Debug Log->IO'.");
            if (inputs_opened)
            {
                if (IsMousePosValid())
                    Text("Mouse pos: (%g, %g)", io.MousePos.x, io.MousePos.y);
                else
                    Text("Mouse pos: <INVALID>");
                Text("Mouse delta: (%g, %g)", io.MouseDelta.x, io.MouseDelta.y);
                Text("Mouse down:");
                for (int i = 0; i < io.MouseDown.Length; i++) if (IsMouseDown(i)) { SameLine(); Text("b%d (%.02f secs)", i, io.MouseDownDuration[i]); }
                Text("Mouse wheel: %.1f", io.MouseWheel);
                Text("Mouse clicked count:");
                for (int i = 0; i < io.MouseClickedCount.Length; i++) if (io.MouseClickedCount[i] > 0) { SameLine(); Text("b%d: %d", i, io.MouseClickedCount[i]); }

                // We iterate both legacy native range and named ImGuiKey ranges. This is a little unusual/odd but this allows
                // displaying the data for old/new backends.
                // User code should never have to go through such hoops!
                // You can generally iterate between ImGuiKey_NamedKey_BEGIN and ImGuiKey_NamedKey_END.
                ImGuiKey start_key = ImGuiKey_NamedKey_BEGIN;
                Text("Keys down:");         for (ImGuiKey key = start_key; key < ImGuiKey_NamedKey_END; key = key + 1) { if (DemoIn_IsLegacyNativeDupe(key) || !IsKeyDown(key)) continue; SameLine(); Text((key < ImGuiKey_NamedKey_BEGIN) ? "\"%s\"" : "\"%s\" %d", GetKeyName(key), key); }
                Text("Keys mods: %s%s%s%s", io.KeyCtrl ? "CTRL " : "", io.KeyShift ? "SHIFT " : "", io.KeyAlt ? "ALT " : "", io.KeySuper ? "SUPER " : "");
                Text("Chars queue:");       for (int i = 0; i < io.InputQueueCharacters.Size; i++) { ushort c = io.InputQueueCharacters[i]; SameLine();  Text("\'%c\' (0x%04X)", (c > ' ' && c <= 255) ? (char)c : '?', c); } // FIXME: We should convert 'c' to UTF-8 here but the functions are not public.

                TreePop();
            }

            // Display ImGuiIO output flags
            SetNextItemOpen(true, ImGuiCond_Once);
            bool outputs_opened = TreeNode("Outputs");
            SameLine();
            HelpMarker(
                "The value of io.WantCaptureMouse and io.WantCaptureKeyboard are normally set by Dear ImGui " +
                "to instruct your application of how to route inputs. Typically, when a value is true, it means " +
                "Dear ImGui wants the corresponding inputs and we expect the underlying application to ignore them.\n\n" +
                "The most typical case is: when hovering a window, Dear ImGui set io.WantCaptureMouse to true, " +
                "and underlying application should ignore mouse inputs (in practice there are many and more subtle " +
                "rules leading to how those flags are set).");
            if (outputs_opened)
            {
                Text("io.WantCaptureMouse: %d", io.WantCaptureMouse);
                Text("io.WantCaptureMouseUnlessPopupClose: %d", io.WantCaptureMouseUnlessPopupClose);
                Text("io.WantCaptureKeyboard: %d", io.WantCaptureKeyboard);
                Text("io.WantTextInput: %d", io.WantTextInput);
                Text("io.WantSetMousePos: %d", io.WantSetMousePos);
                Text("io.NavActive: %d, io.NavVisible: %d", io.NavActive, io.NavVisible);

                if (TreeNode("WantCapture override"))
                {
                    HelpMarker(
                        "Hovering the colored canvas will override io.WantCaptureXXX fields.\n" +
                        "Notice how normally (when set to none), the value of io.WantCaptureKeyboard would be false when hovering " +
                        "and true when clicking.");
                    ref int capture_override_mouse = ref DemoInputsLocals.capture_override_mouse;
                    ref int capture_override_keyboard = ref DemoInputsLocals.capture_override_keyboard;
                    string[] capture_override_desc = { "None", "Set to false", "Set to true" };
                    SetNextItemWidth(GetFontSize() * 15);
                    SliderInt("SetNextFrameWantCaptureMouse() on hover", ref capture_override_mouse, -1, +1, capture_override_desc[capture_override_mouse + 1], ImGuiSliderFlags_AlwaysClamp);
                    SetNextItemWidth(GetFontSize() * 15);
                    SliderInt("SetNextFrameWantCaptureKeyboard() on hover", ref capture_override_keyboard, -1, +1, capture_override_desc[capture_override_keyboard + 1], ImGuiSliderFlags_AlwaysClamp);

                    ColorButton("##panel", new ImVec4(0.7f, 0.1f, 0.7f, 1.0f), ImGuiColorEditFlags_NoTooltip | ImGuiColorEditFlags_NoDragDrop, new ImVec2(128.0f, 96.0f)); // Dummy item
                    if (IsItemHovered() && capture_override_mouse != -1)
                        SetNextFrameWantCaptureMouse(capture_override_mouse == 1);
                    if (IsItemHovered() && capture_override_keyboard != -1)
                        SetNextFrameWantCaptureKeyboard(capture_override_keyboard == 1);

                    TreePop();
                }
                TreePop();
            }

            // Demonstrate using Shortcut() and Routing Policies.
            // The general flow is:
            // - Code interested in a chord (e.g. "Ctrl+A") declares their intent.
            // - Multiple locations may be interested in same chord! Routing helps find a winner.
            // - Every frame, we resolve all claims and assign one owner if the modifiers are matching.
            // - The lower-level function is 'bool SetShortcutRouting()', returns true when caller got the route.
            // - Most of the times, SetShortcutRouting() is not called directly. User mostly calls Shortcut() with routing flags.
            // - If you call Shortcut() WITHOUT any routing option, it uses ImGuiInputFlags_RouteFocused.
            // TL;DR: Most uses will simply be:
            // - Shortcut(ImGuiMod_Ctrl | ImGuiKey_A); // Use ImGuiInputFlags_RouteFocused policy.
            if (TreeNode("Shortcuts"))
            {
                ref ImGuiInputFlags route_options = ref DemoInputsLocals.route_options;
                ref ImGuiInputFlags route_type = ref DemoInputsLocals.route_type;
                CheckboxFlags("ImGuiInputFlags_Repeat", ref route_options, ImGuiInputFlags_Repeat);
                RadioButton("ImGuiInputFlags_RouteActive", ref route_type, ImGuiInputFlags_RouteActive);
                RadioButton("ImGuiInputFlags_RouteFocused (default)", ref route_type, ImGuiInputFlags_RouteFocused);
                Indent();
                BeginDisabled(route_type != ImGuiInputFlags_RouteFocused);
                CheckboxFlags("ImGuiInputFlags_RouteOverActive##0", ref route_options, ImGuiInputFlags_RouteOverActive);
                EndDisabled();
                Unindent();
                RadioButton("ImGuiInputFlags_RouteGlobal", ref route_type, ImGuiInputFlags_RouteGlobal);
                Indent();
                BeginDisabled(route_type != ImGuiInputFlags_RouteGlobal);
                CheckboxFlags("ImGuiInputFlags_RouteOverFocused", ref route_options, ImGuiInputFlags_RouteOverFocused);
                CheckboxFlags("ImGuiInputFlags_RouteOverActive", ref route_options, ImGuiInputFlags_RouteOverActive);
                CheckboxFlags("ImGuiInputFlags_RouteUnlessBgFocused", ref route_options, ImGuiInputFlags_RouteUnlessBgFocused);
                EndDisabled();
                Unindent();
                RadioButton("ImGuiInputFlags_RouteAlways", ref route_type, ImGuiInputFlags_RouteAlways);
                ImGuiInputFlags flags = route_type | route_options; // Merged flags
                if (route_type != ImGuiInputFlags_RouteGlobal)
                    flags &= ~(ImGuiInputFlags_RouteOverFocused | ImGuiInputFlags_RouteOverActive | ImGuiInputFlags_RouteUnlessBgFocused);

                SeparatorText("Using SetNextItemShortcut()");
                Text("Ctrl+S");
                SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_S, flags | ImGuiInputFlags_Tooltip);
                Button("Save");
                Text("Alt+F");
                SetNextItemShortcut(ImGuiMod_Alt | ImGuiKey_F, flags | ImGuiInputFlags_Tooltip);
                SliderFloat("Factor", ref DemoInputsLocals.f, 0.0f, 1.0f);

                SeparatorText("Using Shortcut()");
                float line_height = GetTextLineHeightWithSpacing();
                ImGuiKeyChord key_chord = ImGuiMod_Ctrl | ImGuiKey_A;

                Text("Ctrl+A");
                Text("IsWindowFocused: %d, Shortcut: %s", IsWindowFocused(), Shortcut(key_chord, flags) ? "PRESSED" : "...");

                PushStyleColor(ImGuiCol_ChildBg, new ImVec4(1.0f, 0.0f, 1.0f, 0.1f));

                BeginChild("WindowA", new ImVec2(-float.MinValue, line_height * 14), ImGuiChildFlags_Borders);
                Text("Press Ctrl+A and see who receives it!");
                Separator();

                // 1: Window polling for Ctrl+A
                Text("(in WindowA)");
                Text("IsWindowFocused: %d, Shortcut: %s", IsWindowFocused(), Shortcut(key_chord, flags) ? "PRESSED" : "...");

                // 2: InputText also polling for Ctrl+A: it always uses _RouteFocused internally (gets priority when active)
                // (Commented because the owner-aware version of Shortcut() is still in imgui_internal.h)
                //byte[] str = DemoIn_TextBuffer(16, "Press Ctrl+A");
                //Spacing();
                //InputText("InputTextB", str, str.Length, ImGuiInputTextFlags_ReadOnly);
                //ImGuiID item_id = GetItemID();
                //SameLine(); HelpMarker("Internal widgets always use _RouteFocused");
                //Text("IsWindowFocused: %d, Shortcut: %s", IsWindowFocused(), Shortcut(key_chord, flags, item_id) ? "PRESSED" : "...");

                // 3: Dummy child is not claiming the route: focusing them shouldn't steal route away from WindowA
                BeginChild("ChildD", new ImVec2(-float.MinValue, line_height * 4), ImGuiChildFlags_Borders);
                Text("(in ChildD: not using same Shortcut)");
                Text("IsWindowFocused: %d", IsWindowFocused());
                EndChild();

                // 4: Child window polling for Ctrl+A. It is deeper than WindowA and gets priority when focused.
                BeginChild("ChildE", new ImVec2(-float.MinValue, line_height * 4), ImGuiChildFlags_Borders);
                Text("(in ChildE: using same Shortcut)");
                Text("IsWindowFocused: %d, Shortcut: %s", IsWindowFocused(), Shortcut(key_chord, flags) ? "PRESSED" : "...");
                EndChild();

                // 5: In a popup
                if (Button("Open Popup"))
                    OpenPopup("PopupF");
                if (BeginPopup("PopupF"))
                {
                    Text("(in PopupF)");
                    Text("IsWindowFocused: %d, Shortcut: %s", IsWindowFocused(), Shortcut(key_chord, flags) ? "PRESSED" : "...");
                    // (Commented because the owner-aware version of Shortcut() is still in imgui_internal.h)
                    //InputText("InputTextG", str, str.Length, ImGuiInputTextFlags_ReadOnly);
                    //Text("IsWindowFocused: %d, Shortcut: %s", IsWindowFocused(), Shortcut(key_chord, flags, GetItemID()) ? "PRESSED" : "...");
                    EndPopup();
                }
                EndChild();
                PopStyleColor();

                TreePop();
            }

            // Display mouse cursors
            if (TreeNode("Mouse Cursors"))
            {
                string[] mouse_cursors_names = { "Arrow", "TextInput", "ResizeAll", "ResizeNS", "ResizeEW", "ResizeNESW", "ResizeNWSE", "Hand", "Wait", "Progress", "NotAllowed" };
                System.Diagnostics.Debug.Assert(mouse_cursors_names.Length == ImGuiMouseCursor_COUNT);

                ImGuiMouseCursor current = GetMouseCursor();
                string cursor_name = (current >= ImGuiMouseCursor_Arrow) && (current < ImGuiMouseCursor_COUNT) ? mouse_cursors_names[current] : "N/A";
                Text("Current mouse cursor = %d: %s", current, cursor_name);
                BeginDisabled(true);
                CheckboxFlags("io.BackendFlags: HasMouseCursors", ref io.BackendFlags, ImGuiBackendFlags_HasMouseCursors);
                EndDisabled();

                Text("Hover to see mouse cursors:");
                SameLine(); HelpMarker(
                    "Your application can render a different mouse cursor based on what ImGui::GetMouseCursor() returns. " +
                    "If software cursor rendering (io.MouseDrawCursor) is set ImGui will draw the right cursor for you, " +
                    "otherwise your backend needs to handle it.");
                for (int i = 0; i < ImGuiMouseCursor_COUNT; i++)
                {
                    string label = ImFmt.Sprintf("Mouse cursor %d: %s", i, mouse_cursors_names[i]);
                    Bullet(); Selectable(label, false);
                    if (IsItemHovered())
                        SetMouseCursor(i);
                }
                TreePop();
            }

            if (TreeNode("Tabbing"))
            {
                Text("Use Tab/Shift+Tab to cycle through keyboard editable fields.");
                byte[] buf = DemoInputsLocals.tabbing_buf;
                InputText("1", buf, buf.Length);
                InputText("2", buf, buf.Length);
                InputText("3", buf, buf.Length);
                PushItemFlag(ImGuiItemFlags_NoTabStop, true);
                InputText("4 (tab skip)", buf, buf.Length);
                SameLine(); HelpMarker("Item won't be cycled through when using TAB or Shift+Tab.");
                PopItemFlag();
                InputText("5", buf, buf.Length);
                TreePop();
            }

            if (TreeNode("Focus from code"))
            {
                bool focus_1 = Button("Focus on 1"); SameLine();
                bool focus_2 = Button("Focus on 2"); SameLine();
                bool focus_3 = Button("Focus on 3");
                int has_focus = 0;
                byte[] buf = DemoInputsLocals.focus_buf;

                if (focus_1) SetKeyboardFocusHere();
                InputText("1", buf, buf.Length);
                if (IsItemActive()) has_focus = 1;

                if (focus_2) SetKeyboardFocusHere();
                InputText("2", buf, buf.Length);
                if (IsItemActive()) has_focus = 2;

                PushItemFlag(ImGuiItemFlags_NoTabStop, true);
                if (focus_3) SetKeyboardFocusHere();
                InputText("3 (tab skip)", buf, buf.Length);
                if (IsItemActive()) has_focus = 3;
                SameLine(); HelpMarker("Item won't be cycled through when using TAB or Shift+Tab.");
                PopItemFlag();

                if (has_focus != 0)
                    Text("Item with focus: %d", has_focus);
                else
                    Text("Item with focus: <none>");

                // Use >= 0 parameter to SetKeyboardFocusHere() to focus an upcoming item
                int focus_ahead = -1;
                if (Button("Focus on X")) { focus_ahead = 0; } SameLine();
                if (Button("Focus on Y")) { focus_ahead = 1; } SameLine();
                if (Button("Focus on Z")) { focus_ahead = 2; }
                if (focus_ahead != -1) SetKeyboardFocusHere(focus_ahead);
                SliderFloat3("Float3", DemoInputsLocals.f3, 0.0f, 1.0f);

                TextWrapped("NB: Cursor & selection are preserved when refocusing last used item in code.");
                TreePop();
            }

            if (TreeNode("Dragging"))
            {
                TextWrapped("You can use ImGui::GetMouseDragDelta(0) to query for the dragged amount on any widget.");
                for (int button = 0; button < 3; button++)
                {
                    Text("IsMouseDragging(%d):", button);
                    Text("  w/ default threshold: %d,", IsMouseDragging(button));
                    Text("  w/ zero threshold: %d,", IsMouseDragging(button, 0.0f));
                    Text("  w/ large threshold: %d,", IsMouseDragging(button, 20.0f));
                }

                Button("Drag Me");
                if (IsItemActive())
                    GetForegroundDrawList().AddLine(io.MouseClickedPos[0], io.MousePos, GetColorU32(ImGuiCol_Button), 4.0f); // Draw a line between the button and the mouse cursor

                // Drag operations gets "unlocked" when the mouse has moved past a certain threshold
                // (the default threshold is stored in io.MouseDragThreshold). You can request a lower or higher
                // threshold using the second parameter of IsMouseDragging() and GetMouseDragDelta().
                ImVec2 value_raw = GetMouseDragDelta(0, 0.0f);
                ImVec2 value_with_lock_threshold = GetMouseDragDelta(0);
                ImVec2 mouse_delta = io.MouseDelta;
                Text("GetMouseDragDelta(0):");
                Text("  w/ default threshold: (%.1f, %.1f)", value_with_lock_threshold.x, value_with_lock_threshold.y);
                Text("  w/ zero threshold: (%.1f, %.1f)", value_raw.x, value_raw.y);
                Text("io.MouseDelta: (%.1f, %.1f)", mouse_delta.x, mouse_delta.y);
                TreePop();
            }
        }
    }

    // C++: struct funcs { static bool IsLegacyNativeDupe(ImGuiKey) { return false; } };
    private static bool DemoIn_IsLegacyNativeDupe(ImGuiKey key) => false;

    // C++: char buf[N] = "..." initializer -> NUL-terminated UTF-8 byte buffer for InputText().
    private static byte[] DemoIn_TextBuffer(int size, string init)
    {
        byte[] buf = new byte[size];
        ImStrncpyBytes(buf, 0, init, size);
        return buf;
    }

    // Function-local static variables of C++ DemoWindowInputs().
    private static class DemoInputsLocals
    {
        public static int capture_override_mouse = -1;                 // C++: static int capture_override_mouse = -1;
        public static int capture_override_keyboard = -1;              // C++: static int capture_override_keyboard = -1;
        public static ImGuiInputFlags route_options = ImGuiInputFlags_Repeat;     // C++: static ImGuiInputFlags route_options = ImGuiInputFlags_Repeat;
        public static ImGuiInputFlags route_type = ImGuiInputFlags_RouteFocused;  // C++: static ImGuiInputFlags route_type = ImGuiInputFlags_RouteFocused;
        public static float f = 0.5f;                                  // C++: static float f = 0.5f;
        public static byte[] tabbing_buf = DemoIn_TextBuffer(32, "hello");                             // C++: static char buf[32] = "hello";
        public static byte[] focus_buf = DemoIn_TextBuffer(128, "click on a button to set focus");     // C++: static char buf[128] = "click on a button to set focus";
        public static float[] f3 = new float[] { 0.0f, 0.0f, 0.0f };   // C++: static float f3[3] = { 0.0f, 0.0f, 0.0f };
    }
}
