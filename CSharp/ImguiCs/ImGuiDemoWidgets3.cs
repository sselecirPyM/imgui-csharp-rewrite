// Port of imgui_demo.cpp [SECTION] Widgets (part 3), C++ lines 3738-4529:
//   DemoWindowWidgetsText (3738) / DemoWindowWidgetsTextFilter (3857) / DemoWindowWidgetsTextInput (3884) /
//   DemoWindowWidgetsTooltips (4104) / DemoWindowWidgetsTreeNodes (4203) / DemoWindowWidgetsVerticalSliders (4400) /
//   DemoWindowWidgets (4472)
// C++ function-local 'static' variables are hosted in the file-local DemoWidgets3Locals class below.
using System;
using System.Text;

namespace ImGui;

// Function-local statics of the DemoWindowWidgets* functions ported in this file.
// Fields are prefixed by their host function to keep them apart (Text/TextFilter/TextInput/Tooltips/TreeNodes/VerticalSliders).
file static class DemoWidgets3Locals
{
    // DemoWindowWidgetsText()
    public static float t_custom_size = 16.0f;                               // C++: static float custom_size
    public static float t_custom_scale = 1.0f;                               // C++: static float custom_scale
    public static float t_wrap_width = 200.0f;                               // C++: static float wrap_width
    // C++: static char buf[32] = "\xe6\x97\xa5\xe6\x9c\xac\xe8\xaa\x9e" (kanjis for "nihongo")
    //static char buf[32] = u8"NIHONGO"; // <- this is how you would write it with C++11, using real kanjis
    public static byte[] t_utf8_buf = DemoW3_BytesFromUtf8("日本語", 32);

    // DemoWindowWidgetsTextFilter()
    public static ImGuiTextFilter filter = new ImGuiTextFilter();            // C++: static ImGuiTextFilter filter

    // DemoWindowWidgetsTextInput()
    // C++: static char text[1024 * 16] = "/*\n The Pentium F00F bug..."
    public static byte[] ti_text = DemoW3_BytesFromUtf8(
        "/*\n" +
        " The Pentium F00F bug, shorthand for F0 0F C7 C8,\n" +
        " the hexadecimal encoding of one offending instruction,\n" +
        " more formally, the invalid operand with locked CMPXCHG8B\n" +
        " instruction bug, is a design flaw in the majority of\n" +
        " Intel Pentium, Pentium MMX, and Pentium OverDrive\n" +
        " processors (all in the P5 microarchitecture).\n" +
        "*/\n\n" +
        "label:\n" +
        "\tlock cmpxchg8b eax\n", 1024 * 16);
    public static ImGuiInputTextFlags ti_ml_flags = ImGuiInputTextFlags_AllowTabInput; // C++: static ImGuiInputTextFlags flags
    public static byte[] ti_buf1 = new byte[32];                             // C++: static char buf1[32] = ""
    public static byte[] ti_buf2 = new byte[32];                             // C++: static char buf2[32] = ""
    public static byte[] ti_buf3 = new byte[32];                             // C++: static char buf3[32] = ""
    public static byte[] ti_buf4 = new byte[32];                             // C++: static char buf4[32] = ""
    public static byte[] ti_buf5 = new byte[32];                             // C++: static char buf5[32] = ""
    public static byte[] ti_buf6 = new byte[32];                             // C++: static char buf6[32] = ""
    public static byte[] ti_buf7 = new byte[32];                             // C++: static char buf7[32] = ""
    public static byte[] ti_password = DemoW3_BytesFromUtf8("password123", 64); // C++: static char password[64] = "password123"
    public static byte[] ti_cb_buf1 = new byte[64];                          // C++: static char buf1[64]
    public static byte[] ti_cb_buf2 = new byte[64];                          // C++: static char buf2[64]
    public static byte[] ti_cb_buf3 = new byte[64];                          // C++: static char buf3[64]
    public static int[] ti_edit_count = new int[1];                          // C++: static int edit_count (shared with the callback via UserData)
    public static ImGuiInputTextFlags ti_rs_flags = ImGuiInputTextFlags_None; // C++: static ImGuiInputTextFlags flags
    public static ImVector<byte> ti_my_str = new ImVector<byte>();           // C++: static ImVector<char> my_str
    public static byte[] ti_elide_buf1 = DemoW3_BytesFromUtf8("/path/to/some/folder/with/long/filename.cpp", 128); // C++: static char buf1[128] = "..."
    public static ImGuiInputTextFlags ti_elide_flags = ImGuiInputTextFlags_ElideLeft; // C++: static ImGuiInputTextFlags flags
    public static byte[] ti_misc_buf1 = new byte[16];                        // C++: static char buf1[16]
    public static ImGuiInputTextFlags ti_misc_flags = ImGuiInputTextFlags_EscapeClearsAll; // C++: static ImGuiInputTextFlags flags

    // DemoWindowWidgetsTooltips()
    public static int tt_always_on = 0;                                      // C++: static int always_on

    // DemoWindowWidgetsTreeNodes()
    public static ImGuiTreeNodeFlags tn_hier_flags = ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_DefaultOpen; // C++: static ImGuiTreeNodeFlags base_flags
    public static int tn_selection_mask = 0x00;                              // C++: static int selection_mask
    public static ImGuiTreeNodeFlags tn_adv_flags = ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_OpenOnDoubleClick | ImGuiTreeNodeFlags_SpanAvailWidth; // C++: static ImGuiTreeNodeFlags base_flags
    public static bool tn_align_label_with_current_x_position = false;       // C++: static bool align_label_with_current_x_position
    public static bool tn_use_drag_and_drop = false;                         // C++: static bool use_drag_and_drop
    // C++: TreeNodeEx((void*)(intptr_t)node_n, ...) hashes a small integer value. GetID(object) in this port
    // hashes reference identity, so we keep one stable boxed object per node index (a freshly boxed int
    // would get a new identity every frame and break the tree open-state storage).
    public static readonly object[] tn_node_ids = { (object)0, (object)1, (object)2, (object)3, (object)4, (object)5 };

    // DemoWindowWidgetsVerticalSliders()
    public static int vs_int_value = 0;                                      // C++: static int int_value
    public static float[] vs_values = { 0.0f, 0.60f, 0.35f, 0.9f, 0.70f, 0.20f, 0.0f }; // C++: static float values[7]
    public static float[] vs_values2 = { 0.20f, 0.80f, 0.40f, 0.25f };       // C++: static float values2[4]

    // C++ NUL-terminated char[] initializer -> C# byte[size] holding the UTF-8 bytes + terminating zero.
    private static byte[] DemoW3_BytesFromUtf8(string s, int size)
    {
        byte[] buf = new byte[size]; // zero-filled, like C++ static storage
        byte[] utf8 = Encoding.UTF8.GetBytes(s);
        Array.Copy(utf8, buf, Math.Min(utf8.Length, size - 1));
        return buf;
    }
}

public static partial class ImGui
{
    // C++: static void DemoWindowWidgetsText()   [imgui_demo.cpp 3738-3851]
    public static void DemoWindowWidgetsText()
    {
        if (TreeNode("Text"))
        {
            if (TreeNode("Colorful Text"))
            {
                // Using shortcut. You can use PushStyleColor()/PopStyleColor() for more flexibility.
                TextColored(new ImVec4(1.0f, 0.0f, 1.0f, 1.0f), "Pink");
                TextColored(new ImVec4(1.0f, 1.0f, 0.0f, 1.0f), "Yellow");
                TextDisabled("Disabled");
                SameLine(); HelpMarker("The TextDisabled color is stored in ImGuiStyle.");
                TreePop();
            }

            if (TreeNode("Font Size"))
            {
                ImGuiStyle style = GetStyle();
                float global_scale = style.FontScaleMain * style.FontScaleDpi;
                Text("style.FontScaleMain = %0.2f", style.FontScaleMain);
                Text("style.FontScaleDpi = %0.2f", style.FontScaleDpi);
                Text("global_scale = ~%0.2f", global_scale); // This is not technically accurate as internal scales may apply, but conceptually let's pretend it is.
                Text("FontSize = %0.2f", GetFontSize());

                SeparatorText("");
                SliderFloat("custom_size", ref DemoWidgets3Locals.t_custom_size, 10.0f, 100.0f, "%.0f");
                Text("ImGui::PushFont(nullptr, custom_size);");
                PushFont(null, DemoWidgets3Locals.t_custom_size);
                Text("FontSize = %.2f (== %.2f * global_scale)", GetFontSize(), DemoWidgets3Locals.t_custom_size);
                PopFont();

                SeparatorText("");
                SliderFloat("custom_scale", ref DemoWidgets3Locals.t_custom_scale, 0.5f, 4.0f, "%.2f");
                Text("ImGui::PushFont(nullptr, style.FontSizeBase * custom_scale);");
                PushFont(null, style.FontSizeBase * DemoWidgets3Locals.t_custom_scale);
                Text("FontSize = %.2f (== style.FontSizeBase * %.2f * global_scale)", GetFontSize(), DemoWidgets3Locals.t_custom_scale);
                PopFont();

                SeparatorText("");
                for (float scaling = 0.5f; scaling <= 4.0f; scaling += 0.5f)
                {
                    PushFont(null, style.FontSizeBase * scaling);
                    Text("FontSize = %.2f (== style.FontSizeBase * %.2f * global_scale)", GetFontSize(), scaling);
                    PopFont();
                }

                TreePop();
            }

            if (TreeNode("Word Wrapping"))
            {
                // Using shortcut. You can use PushTextWrapPos()/PopTextWrapPos() for more flexibility.
                TextWrapped(
                    "This text should automatically wrap on the edge of the window. The current implementation " +
                    "for text wrapping follows simple rules suitable for English and possibly other languages.");
                Spacing();

                SliderFloat("Wrap width", ref DemoWidgets3Locals.t_wrap_width, -20.0f, 600.0f, "%.0f");

                ImDrawList draw_list = GetWindowDrawList();
                for (int n = 0; n < 2; n++)
                {
                    Text("Test paragraph %d:", n);
                    ImVec2 pos = GetCursorScreenPos();
                    ImVec2 marker_min = new ImVec2(pos.x + DemoWidgets3Locals.t_wrap_width, pos.y);
                    ImVec2 marker_max = new ImVec2(pos.x + DemoWidgets3Locals.t_wrap_width + 10, pos.y + GetTextLineHeight());
                    PushTextWrapPos(GetCursorPos().x + DemoWidgets3Locals.t_wrap_width);
                    if (n == 0)
                        Text("The lazy dog is a good dog. This paragraph should fit within %.0f pixels. Testing a 1 character word. The quick brown fox jumps over the lazy dog.", DemoWidgets3Locals.t_wrap_width);
                    else
                        Text("aaaaaaaa bbbbbbbb, c cccccccc,dddddddd. d eeeeeeee   ffffffff. gggggggg!hhhhhhhh");

                    // Draw actual text bounding box, following by marker of our expected limit (should not overlap!)
                    draw_list.AddRect(GetItemRectMin(), GetItemRectMax(), ImGuiCore.IM_COL32(255, 255, 0, 255));
                    draw_list.AddRectFilled(marker_min, marker_max, ImGuiCore.IM_COL32(255, 0, 255, 255));
                    PopTextWrapPos();
                }

                TreePop();
            }

            if (TreeNode("UTF-8 Text"))
            {
                // UTF-8 test with Japanese characters
                // (Needs a suitable font? Try "Google Noto" or "Arial Unicode". See docs/FONTS.md for details.)
                // - In C++ the demo encodes a few strings with hexadecimal constants to support old compilers;
                //   C# sources are always UTF-8 so the raw characters are used below.
                // Note that characters values are preserved even by InputText() if the font cannot be displayed,
                // so you can safely copy & paste garbled characters into another application.
                TextWrapped(
                    "CJK text will only appear if the font was loaded with the appropriate CJK character ranges. " +
                    "Call io.Fonts->AddFontFromFileTTF() manually to load extra character ranges. " +
                    "Read docs/FONTS.md for details.");
                Text("Hiragana: かきくけこ (kakikukeko)");
                Text("Kanjis: 日本語 (nihongo)");
                InputText("UTF-8 input", DemoWidgets3Locals.t_utf8_buf, DemoWidgets3Locals.t_utf8_buf.Length);
                TreePop();
            }
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsTextFilter()   [imgui_demo.cpp 3857-3878]
    public static void DemoWindowWidgetsTextFilter()
    {
        if (TreeNode("Text Filter"))
        {
            // Helper class to easy setup a text filter.
            // You may want to implement a more feature-full filtering scheme in your own application.
            HelpMarker("Not a widget per-se, but ImGuiTextFilter is a helper to perform simple filtering on text strings.");
            ImGuiTextFilter filter = DemoWidgets3Locals.filter;
            Text("Filter usage:\n" +
                "  \"\"         display all lines\n" +
                "  \"xxx\"      display lines containing \"xxx\"\n" +
                "  \"xxx,yyy\"  display lines containing \"xxx\" or \"yyy\"\n" +
                "  \"-xxx\"     hide lines containing \"xxx\"");
            filter.Draw();
            string[] lines = { "aaa1.c", "bbb1.c", "ccc1.c", "aaa2.cpp", "bbb2.cpp", "ccc2.cpp", "abc.h", "hello, world" };
            for (int i = 0; i < lines.Length; i++)
                if (filter.PassFilter(lines[i]))
                    BulletText("%s", lines[i]);
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsTextInput()   [imgui_demo.cpp 3884-4098]
    public static void DemoWindowWidgetsTextInput()
    {
        // To wire InputText() with std::string or any other custom string type,
        // see the "Text Input > Resize Callback" section of this demo, and the misc/cpp/imgui_stdlib.h file.
        if (TreeNode("Text Input"))
        {
            if (TreeNode("Multi-line Text Input"))
            {
                // WE ARE USING A FIXED-SIZE BUFFER FOR SIMPLICITY HERE.
                // If you want to use InputText() with std::string or any custom dynamic string type:
                // - For std::string: use the wrapper in misc/cpp/imgui_stdlib.h/.cpp
                // - Otherwise, see the 'Dear ImGui Demo->Widgets->Text Input->Resize Callback' for using ImGuiInputTextFlags_CallbackResize.
                HelpMarker("You can use the ImGuiInputTextFlags_CallbackResize facility if you need to wire InputTextMultiline() to a dynamic string type. See misc/cpp/imgui_stdlib.h for an example. (This is not demonstrated in imgui_demo.cpp because we don't want to include <string> in here)");
                CheckboxFlags("ImGuiInputTextFlags_ReadOnly", ref DemoWidgets3Locals.ti_ml_flags, ImGuiInputTextFlags_ReadOnly);
                CheckboxFlags("ImGuiInputTextFlags_WordWrap", ref DemoWidgets3Locals.ti_ml_flags, ImGuiInputTextFlags_WordWrap);
                SameLine(); HelpMarker("Feature is currently in Beta. Please read comments in imgui.h");
                CheckboxFlags("ImGuiInputTextFlags_AllowTabInput", ref DemoWidgets3Locals.ti_ml_flags, ImGuiInputTextFlags_AllowTabInput);
                SameLine(); HelpMarker("When _AllowTabInput is set, passing through the widget with Tabbing doesn't automatically activate it, in order to also cycling through subsequent widgets.");
                CheckboxFlags("ImGuiInputTextFlags_CtrlEnterForNewLine", ref DemoWidgets3Locals.ti_ml_flags, ImGuiInputTextFlags_CtrlEnterForNewLine);
                InputTextMultiline("##source", DemoWidgets3Locals.ti_text, DemoWidgets3Locals.ti_text.Length, new ImVec2(-float.Epsilon /* -FLT_MIN */, GetTextLineHeight() * 16), DemoWidgets3Locals.ti_ml_flags);
                TreePop();
            }

            if (TreeNode("Filtered Text Input"))
            {
                // (C++: local 'struct TextFilters' with FilterCasingSwap/FilterImGuiLetters, ported as the
                // DemoW3_FilterCasingSwap/DemoW3_FilterImGuiLetters helpers below.)
                InputText("default", DemoWidgets3Locals.ti_buf1, DemoWidgets3Locals.ti_buf1.Length);
                InputText("decimal", DemoWidgets3Locals.ti_buf2, DemoWidgets3Locals.ti_buf2.Length, ImGuiInputTextFlags_CharsDecimal);
                InputText("hexadecimal", DemoWidgets3Locals.ti_buf3, DemoWidgets3Locals.ti_buf3.Length, ImGuiInputTextFlags_CharsHexadecimal | ImGuiInputTextFlags_CharsUppercase);
                InputText("uppercase", DemoWidgets3Locals.ti_buf4, DemoWidgets3Locals.ti_buf4.Length, ImGuiInputTextFlags_CharsUppercase);
                InputText("no blank", DemoWidgets3Locals.ti_buf5, DemoWidgets3Locals.ti_buf5.Length, ImGuiInputTextFlags_CharsNoBlank);
                InputText("casing swap", DemoWidgets3Locals.ti_buf6, DemoWidgets3Locals.ti_buf6.Length, ImGuiInputTextFlags_CallbackCharFilter, DemoW3_FilterCasingSwap); // Use CharFilter callback to replace characters.
                InputText("\"imgui\"", DemoWidgets3Locals.ti_buf7, DemoWidgets3Locals.ti_buf7.Length, ImGuiInputTextFlags_CallbackCharFilter, DemoW3_FilterImGuiLetters); // Use CharFilter callback to disable some characters.
                TreePop();
            }

            if (TreeNode("Password Input"))
            {
                InputText("password", DemoWidgets3Locals.ti_password, DemoWidgets3Locals.ti_password.Length, ImGuiInputTextFlags_Password);
                SameLine(); HelpMarker("Display all characters as '*'.\nDisable clipboard cut and copy.\nDisable logging.\n");
                InputTextWithHint("password (w/ hint)", "<password>", DemoWidgets3Locals.ti_password, DemoWidgets3Locals.ti_password.Length, ImGuiInputTextFlags_Password);
                InputText("password (clear)", DemoWidgets3Locals.ti_password, DemoWidgets3Locals.ti_password.Length);
                TreePop();
            }

            if (TreeNode("Completion, History, Edit Callbacks"))
            {
                // (C++: local 'struct Funcs' with MyCallback, ported as the DemoW3_MyCallback helper below.)
                InputText("Completion", DemoWidgets3Locals.ti_cb_buf1, DemoWidgets3Locals.ti_cb_buf1.Length, ImGuiInputTextFlags_CallbackCompletion, DemoW3_MyCallback);
                SameLine(); HelpMarker(
                    "Here we append \"..\" each time Tab is pressed. " +
                    "See 'Examples>Console' for a more meaningful demonstration of using this callback.");

                InputText("History", DemoWidgets3Locals.ti_cb_buf2, DemoWidgets3Locals.ti_cb_buf2.Length, ImGuiInputTextFlags_CallbackHistory, DemoW3_MyCallback);
                SameLine(); HelpMarker(
                    "Here we replace and select text each time Up/Down are pressed. " +
                    "See 'Examples>Console' for a more meaningful demonstration of using this callback.");

                InputText("Edit", DemoWidgets3Locals.ti_cb_buf3, DemoWidgets3Locals.ti_cb_buf3.Length, ImGuiInputTextFlags_CallbackEdit, DemoW3_MyCallback, DemoWidgets3Locals.ti_edit_count);
                SameLine(); HelpMarker(
                    "Here we toggle the casing of the first character on every edit + count edits.");
                SameLine(); Text("(%d)", DemoWidgets3Locals.ti_edit_count[0]);

                TreePop();
            }

            if (TreeNode("Resize Callback"))
            {
                // To wire InputText() with std::string or any other custom string type,
                // you can use the ImGuiInputTextFlags_CallbackResize flag + create a custom ImGui::InputText() wrapper
                // using your preferred type. See misc/cpp/imgui_stdlib.h for an implementation of this using std::string.
                HelpMarker(
                    "Using ImGuiInputTextFlags_CallbackResize to wire your custom string type to InputText().\n\n" +
                    "See misc/cpp/imgui_stdlib.h for an implementation of this for std::string.");
                // (C++: local 'struct Funcs' with MyResizeCallback + MyInputTextMultiline, ported as the
                // DemoW3_MyResizeCallback/DemoW3_MyInputTextMultiline helpers below.)

                // For this demo we are using ImVector as a string container.
                // Note that because we need to store a terminating zero character, our size/capacity are 1 more
                // than usually reported by a typical string class.
                CheckboxFlags("ImGuiInputTextFlags_WordWrap", ref DemoWidgets3Locals.ti_rs_flags, ImGuiInputTextFlags_WordWrap);

                ImVector<byte> my_str = DemoWidgets3Locals.ti_my_str;
                if (my_str.empty())
                    my_str.push_back(0);
                DemoW3_MyInputTextMultiline("##MyStr", my_str, new ImVec2(-float.Epsilon /* -FLT_MIN */, GetTextLineHeight() * 16), DemoWidgets3Locals.ti_rs_flags);
                // C++ prints the buffer pointer: Text("Data: %p\nSize: %d\nCapacity: %d", (void*)my_str.begin(), ...)
                Text("Data: %p\nSize: %d\nCapacity: %d", new ImFmt.pointer_t((ulong)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(my_str.Data)), my_str.Size, my_str.Capacity);
                TreePop();
            }

            if (TreeNode("Eliding, Alignment"))
            {
                CheckboxFlags("ImGuiInputTextFlags_ElideLeft", ref DemoWidgets3Locals.ti_elide_flags, ImGuiInputTextFlags_ElideLeft);
                InputText("Path", DemoWidgets3Locals.ti_elide_buf1, DemoWidgets3Locals.ti_elide_buf1.Length, DemoWidgets3Locals.ti_elide_flags);
                TreePop();
            }

            if (TreeNode("Miscellaneous"))
            {
                CheckboxFlags("ImGuiInputTextFlags_EscapeClearsAll", ref DemoWidgets3Locals.ti_misc_flags, ImGuiInputTextFlags_EscapeClearsAll);
                CheckboxFlags("ImGuiInputTextFlags_ReadOnly", ref DemoWidgets3Locals.ti_misc_flags, ImGuiInputTextFlags_ReadOnly);
                CheckboxFlags("ImGuiInputTextFlags_NoUndoRedo", ref DemoWidgets3Locals.ti_misc_flags, ImGuiInputTextFlags_NoUndoRedo);
                InputText("Hello", DemoWidgets3Locals.ti_misc_buf1, DemoWidgets3Locals.ti_misc_buf1.Length, DemoWidgets3Locals.ti_misc_flags);
                TreePop();
            }

            TreePop();
        }

    }

    // C++: static void DemoWindowWidgetsTooltips()   [imgui_demo.cpp 4104-4197]
    public static void DemoWindowWidgetsTooltips()
    {
        if (TreeNode("Tooltips"))
        {
            // Tooltips are windows following the mouse. They do not take focus away.
            SeparatorText("General");

            // Typical use cases:
            // - Short-form (text only):      SetItemTooltip("Hello");
            // - Short-form (any contents):   if (BeginItemTooltip()) { Text("Hello"); EndTooltip(); }

            // - Full-form (text only):       if (IsItemHovered(...)) { SetTooltip("Hello"); }
            // - Full-form (any contents):    if (IsItemHovered(...) && BeginTooltip()) { Text("Hello"); EndTooltip(); }

            HelpMarker(
                "Tooltip are typically created by using a IsItemHovered() + SetTooltip() sequence.\n\n" +
                "We provide a helper SetItemTooltip() function to perform the two with standards flags.");

            ImVec2 sz = new ImVec2(-float.Epsilon /* -FLT_MIN */, 0.0f);

            Button("Basic", sz);
            SetItemTooltip("I am a tooltip");

            Button("Fancy", sz);
            if (BeginItemTooltip())
            {
                Text("I am a fancy tooltip");
                float[] arr = { 0.6f, 0.1f, 1.0f, 0.5f, 0.92f, 0.1f, 0.2f };
                PlotLines("Curve", arr, arr.Length);
                Text("Sin(time) = %f", MathF.Sin((float)GetTime()));
                EndTooltip();
            }

            SeparatorText("Always On");

            // Showcase NOT relying on a IsItemHovered() to emit a tooltip.
            // Here the tooltip is always emitted when 'always_on == true'.
            RadioButton("Off", ref DemoWidgets3Locals.tt_always_on, 0);
            SameLine();
            RadioButton("Always On (Simple)", ref DemoWidgets3Locals.tt_always_on, 1);
            SameLine();
            RadioButton("Always On (Advanced)", ref DemoWidgets3Locals.tt_always_on, 2);
            if (DemoWidgets3Locals.tt_always_on == 1)
                SetTooltip("I am following you around.");
            else if (DemoWidgets3Locals.tt_always_on == 2 && BeginTooltip())
            {
                ProgressBar(MathF.Sin((float)GetTime()) * 0.5f + 0.5f, new ImVec2(GetFontSize() * 25, 0.0f));
                EndTooltip();
            }

            SeparatorText("Custom");

            HelpMarker(
                "Passing ImGuiHoveredFlags_ForTooltip to IsItemHovered() is the preferred way to standardize " +
                "tooltip activation details across your application. You may however decide to use custom " +
                "flags for a specific tooltip instance.");

            // The following examples are passed for documentation purpose but may not be useful to most users.
            // Passing ImGuiHoveredFlags_ForTooltip to IsItemHovered() will pull ImGuiHoveredFlags flags values from
            // 'style.HoverFlagsForTooltipMouse' or 'style.HoverFlagsForTooltipNav' depending on whether mouse or keyboard/gamepad is being used.
            // With default settings, ImGuiHoveredFlags_ForTooltip is equivalent to ImGuiHoveredFlags_DelayShort + ImGuiHoveredFlags_Stationary.
            Button("Manual", sz);
            if (IsItemHovered(ImGuiHoveredFlags_ForTooltip))
                SetTooltip("I am a manually emitted tooltip.");

            Button("DelayNone", sz);
            if (IsItemHovered(ImGuiHoveredFlags_DelayNone))
                SetTooltip("I am a tooltip with no delay.");

            Button("DelayShort", sz);
            if (IsItemHovered(ImGuiHoveredFlags_DelayShort | ImGuiHoveredFlags_NoSharedDelay))
                SetTooltipV("I am a tooltip with a short delay (%0.2f sec).", GetStyle().HoverDelayShort);

            Button("DelayLong", sz);
            if (IsItemHovered(ImGuiHoveredFlags_DelayNormal | ImGuiHoveredFlags_NoSharedDelay))
                SetTooltipV("I am a tooltip with a long delay (%0.2f sec).", GetStyle().HoverDelayNormal);

            Button("Stationary", sz);
            if (IsItemHovered(ImGuiHoveredFlags_Stationary))
                SetTooltip("I am a tooltip requiring mouse to be stationary before activating.");

            // Using ImGuiHoveredFlags_ForTooltip will pull flags from 'style.HoverFlagsForTooltipMouse' or 'style.HoverFlagsForTooltipNav',
            // which default value include the ImGuiHoveredFlags_AllowWhenDisabled flag.
            BeginDisabled(true);
            Button("Disabled item", sz);
            if (IsItemHovered(ImGuiHoveredFlags_ForTooltip))
                SetTooltip("I am a tooltip for a disabled item.");
            EndDisabled();

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsTreeNodes()   [imgui_demo.cpp 4203-4394]
    public static void DemoWindowWidgetsTreeNodes()
    {
        if (TreeNode("Tree Nodes"))
        {
            // See see "Examples -> Property Editor" (ShowExampleAppPropertyEditor() function) for a fancier, data-driven tree.
            if (TreeNode("Basic Trees"))
            {
                for (int i = 0; i < 5; i++)
                {
                    // Use SetNextItemOpen() so set the default state of a node to be open. We could
                    // also use TreeNodeEx() with the ImGuiTreeNodeFlags_DefaultOpen flag to achieve the same thing!
                    if (i == 0)
                        SetNextItemOpen(true, ImGuiCond_Once);

                    // Here we use PushID() to generate a unique base ID, and then the "" used as TreeNode id won't conflict.
                    // An alternative to using 'PushID() + TreeNode("", ...)' to generate a unique ID is to use 'TreeNode((void*)(intptr_t)i, ...)',
                    // aka generate a dummy pointer-sized value to be hashed. The demo below uses that technique. Both are fine.
                    PushID(i);
                    if (TreeNode("", "Child %d", i))
                    {
                        Text("blah blah");
                        SameLine();
                        if (SmallButton("button")) { }
                        TreePop();
                    }
                    PopID();
                }
                TreePop();
            }

            if (TreeNode("Hierarchy Lines"))
            {
                HelpMarker("Default option for DrawLinesXXX is stored in style.TreeLinesFlags");
                CheckboxFlags("ImGuiTreeNodeFlags_DrawLinesNone", ref DemoWidgets3Locals.tn_hier_flags, ImGuiTreeNodeFlags_DrawLinesNone);
                CheckboxFlags("ImGuiTreeNodeFlags_DrawLinesFull", ref DemoWidgets3Locals.tn_hier_flags, ImGuiTreeNodeFlags_DrawLinesFull);
                CheckboxFlags("ImGuiTreeNodeFlags_DrawLinesToNodes", ref DemoWidgets3Locals.tn_hier_flags, ImGuiTreeNodeFlags_DrawLinesToNodes);

                if (TreeNodeEx("Parent", DemoWidgets3Locals.tn_hier_flags))
                {
                    if (TreeNodeEx("Child 1", DemoWidgets3Locals.tn_hier_flags))
                    {
                        Button("Button for Child 1");
                        TreePop();
                    }
                    if (TreeNodeEx("Child 2", DemoWidgets3Locals.tn_hier_flags))
                    {
                        Button("Button for Child 2");
                        TreePop();
                    }
                    Text("Remaining contents");
                    Text("Remaining contents");
                    TreePop();
                }

                TreePop();
            }

            if (TreeNode("Clipping Large Trees"))
            {
                TextWrapped(
                    "- Using ImGuiListClipper with trees is a less easy than on arrays or grids.\n" +
                    "- Refer to 'Demo->Examples->Property Editor' for an example of how to do that.\n" +
                    "- Discuss in #3823");
                TreePop();
            }

            if (TreeNode("Selectable Nodes"))
            {
                HelpMarker(
                    "Manually implemented selectable nodes.\n" +
                    "Click to select, Ctrl+Click to toggle, click on arrows or double-click to open.\n\n" +
                    "You may also use the multi-select API (see 'Demo->Widgets->Selection State & Multi-Select') for more advanced multi-selection features.");

                // Hold in 'selection_mask' a simple representation of what may be user-side selection state.
                // - You may retain selection state inside or outside your objects in whatever format you see fit.
                //   You may use ImGuiSelectionBasicStorage which is conceptually close to a set<> of identifiers.
                // - We record which node was clicked and then apply selection at the end of the loop.
                // - This is a manual and simplified reimplementation of multi-selection, which the full
                //   BeginMultiSelect() API implements better, but which is not trivial to wire for trees.
                int node_clicked_idx = -1;
                for (int node_n = 0; node_n < 6; node_n++)
                {
                    // Disable the default "open on single-click behavior" + set Selected flag according to our selection.
                    // To alter selection we use if 'IsItemClicked() && !IsItemToggledOpen()', so clicking on an arrow doesn't alter selection.
                    // In a BeginMultiSelect()/EndMultiSelect() we could use IsItemToggledSelection() but here we reimplement and use our own logic.
                    ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_OpenOnDoubleClick | ImGuiTreeNodeFlags_SpanAvailWidth;
                    if ((DemoWidgets3Locals.tn_selection_mask & (1 << node_n)) != 0)
                        flags |= ImGuiTreeNodeFlags_Selected;

                    bool is_open = TreeNodeEx(DemoWidgets3Locals.tn_node_ids[node_n], flags, "Selectable Node %d", node_n);
                    if (IsItemClicked(ImGuiMouseButton_Left) && !IsItemToggledOpen())
                        node_clicked_idx = node_n;
                    if (is_open)
                    {
                        BulletText("<Node contents here>");
                        TreePop();
                    }
                }
                if (node_clicked_idx != -1)
                {
                    // Update selection state (process outside of tree loop to avoid visual inconsistencies during the clicking frame)
                    if (GetIO().KeyCtrl)
                        DemoWidgets3Locals.tn_selection_mask ^= (1 << node_clicked_idx);   // Ctrl+Click to toggle
                    else //if (!(selection_mask & (1 << node_clicked_idx))) // Depending on selection behavior you want, may want to preserve selection when clicking on item that is part of the selection
                        DemoWidgets3Locals.tn_selection_mask = (1 << node_clicked_idx);    // Click to single-select
                }
                TreePop();
            }

            if (TreeNode("Advanced"))
            {
                CheckboxFlags("ImGuiTreeNodeFlags_OpenOnArrow", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_OpenOnArrow);
                CheckboxFlags("ImGuiTreeNodeFlags_OpenOnDoubleClick", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_OpenOnDoubleClick);
                CheckboxFlags("ImGuiTreeNodeFlags_SpanAvailWidth", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_SpanAvailWidth); SameLine(); HelpMarker("Extend hit area to all available width instead of allowing more items to be laid out after the node.");
                CheckboxFlags("ImGuiTreeNodeFlags_SpanFullWidth", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_SpanFullWidth);
                CheckboxFlags("ImGuiTreeNodeFlags_SpanLabelWidth", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_SpanLabelWidth); SameLine(); HelpMarker("Reduce hit area to the text label and a bit of margin.");
                CheckboxFlags("ImGuiTreeNodeFlags_SpanAllColumns", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_SpanAllColumns); SameLine(); HelpMarker("For use in Tables only.");
                CheckboxFlags("ImGuiTreeNodeFlags_AllowOverlap", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_AllowOverlap);
                CheckboxFlags("ImGuiTreeNodeFlags_Framed", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_Framed); SameLine(); HelpMarker("Draw frame with background (e.g. for CollapsingHeader)");
                CheckboxFlags("ImGuiTreeNodeFlags_FramePadding", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_FramePadding);
                CheckboxFlags("ImGuiTreeNodeFlags_NavLeftJumpsToParent", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_NavLeftJumpsToParent);

                HelpMarker("Default option for DrawLinesXXX is stored in style.TreeLinesFlags");
                CheckboxFlags("ImGuiTreeNodeFlags_DrawLinesNone", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_DrawLinesNone);
                CheckboxFlags("ImGuiTreeNodeFlags_DrawLinesFull", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_DrawLinesFull);
                CheckboxFlags("ImGuiTreeNodeFlags_DrawLinesToNodes", ref DemoWidgets3Locals.tn_adv_flags, ImGuiTreeNodeFlags_DrawLinesToNodes);

                Checkbox("Align label with current X position", ref DemoWidgets3Locals.tn_align_label_with_current_x_position);
                Checkbox("Make Tree Nodes as drag & drop sources", ref DemoWidgets3Locals.tn_use_drag_and_drop);
                if (DemoWidgets3Locals.tn_align_label_with_current_x_position)
                    Unindent(GetTreeNodeToLabelSpacing());

                for (int node_n = 0; node_n < 6; node_n++)
                {
                    ImGuiTreeNodeFlags node_flags = DemoWidgets3Locals.tn_adv_flags;
                    if (node_n < 3)
                    {
                        // Items 0..2 are Tree Node
                        bool is_open = TreeNodeEx(DemoWidgets3Locals.tn_node_ids[node_n], node_flags, "Selectable Node %d", node_n);
                        if (DemoWidgets3Locals.tn_use_drag_and_drop && BeginDragDropSource())
                        {
                            SetDragDropPayload("MY_TREENODE_PAYLOAD_TYPE", null, 0);
                            Text("This is a drag and drop source");
                            EndDragDropSource();
                        }
                        if (node_n == 2 && (DemoWidgets3Locals.tn_adv_flags & ImGuiTreeNodeFlags_SpanLabelWidth) != 0)
                        {
                            // Item 2 has an additional inline button to help demonstrate SpanLabelWidth.
                            SameLine();
                            if (SmallButton("button")) { }
                        }
                        if (is_open)
                        {
                            BulletText("Blah blah\nBlah Blah");
                            SameLine();
                            SmallButton("Button");
                            TreePop();
                        }
                    }
                    else
                    {
                        // Items 3..5 are Tree Leaves
                        // The only reason we use TreeNode at all is to allow selection of the leaf. Otherwise we can
                        // use BulletText() or advance the cursor by GetTreeNodeToLabelSpacing() and call Text().
                        node_flags |= ImGuiTreeNodeFlags_Leaf | ImGuiTreeNodeFlags_NoTreePushOnOpen; // ImGuiTreeNodeFlags_Bullet
                        TreeNodeEx(DemoWidgets3Locals.tn_node_ids[node_n], node_flags, "Selectable Leaf %d", node_n);
                        if (DemoWidgets3Locals.tn_use_drag_and_drop && BeginDragDropSource())
                        {
                            SetDragDropPayload("MY_TREENODE_PAYLOAD_TYPE", null, 0);
                            Text("This is a drag and drop source");
                            EndDragDropSource();
                        }
                    }
                }
                if (DemoWidgets3Locals.tn_align_label_with_current_x_position)
                    Indent(GetTreeNodeToLabelSpacing());
                TreePop();
            }
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsVerticalSliders()   [imgui_demo.cpp 4400-4466]
    public static void DemoWindowWidgetsVerticalSliders()
    {
        if (TreeNode("Vertical Sliders"))
        {
            const float spacing = 4;
            PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(spacing, spacing));

            VSliderInt("##int", new ImVec2(18, 160), ref DemoWidgets3Locals.vs_int_value, 0, 5);
            SameLine();

            float[] values = DemoWidgets3Locals.vs_values;
            PushID("set1");
            for (int i = 0; i < 7; i++)
            {
                if (i > 0) SameLine();
                PushID(i);
                PushStyleColor(ImGuiCol_FrameBg, DemoW3_ImColorHSV(i / 7.0f, 0.5f, 0.5f));
                PushStyleColor(ImGuiCol_FrameBgHovered, DemoW3_ImColorHSV(i / 7.0f, 0.6f, 0.5f));
                PushStyleColor(ImGuiCol_FrameBgActive, DemoW3_ImColorHSV(i / 7.0f, 0.7f, 0.5f));
                PushStyleColor(ImGuiCol_SliderGrab, DemoW3_ImColorHSV(i / 7.0f, 0.9f, 0.9f));
                VSliderFloat("##v", new ImVec2(18, 160), ref values[i], 0.0f, 1.0f, "");
                if (IsItemActive() || IsItemHovered())
                    SetTooltipV("%.3f", values[i]);
                PopStyleColor(4);
                PopID();
            }
            PopID();

            SameLine();
            PushID("set2");
            float[] values2 = DemoWidgets3Locals.vs_values2;
            const int rows = 3;
            ImVec2 small_slider_size = new ImVec2(18, (float)(int)((160.0f - (rows - 1) * spacing) / rows));
            for (int nx = 0; nx < 4; nx++)
            {
                if (nx > 0) SameLine();
                BeginGroup();
                for (int ny = 0; ny < rows; ny++)
                {
                    PushID(nx * rows + ny);
                    VSliderFloat("##v", small_slider_size, ref values2[nx], 0.0f, 1.0f, "");
                    if (IsItemActive() || IsItemHovered())
                        SetTooltipV("%.3f", values2[nx]);
                    PopID();
                }
                EndGroup();
            }
            PopID();

            SameLine();
            PushID("set3");
            for (int i = 0; i < 4; i++)
            {
                if (i > 0) SameLine();
                PushID(i);
                PushStyleVar(ImGuiStyleVar_GrabMinSize, 40.0f);
                VSliderFloat("##v", new ImVec2(40, 160), ref values[i], 0.0f, 1.0f, "%.2f\nsec");
                PopStyleVar();
                PopID();
            }
            PopID();
            PopStyleVar();
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgets(ImGuiDemoWindowData* demo_data)   [imgui_demo.cpp 4472-4529]
    public static void DemoWindowWidgets(ImGuiDemoWindowData demo_data)
    {
        //ImGui::SetNextItemOpen(true, ImGuiCond_Once);
        if (!CollapsingHeader("Widgets"))
            return;
        // IMGUI_DEMO_MARKER("Widgets");

        bool disable_all = demo_data.DisableSections; // The Checkbox for that is inside the "Disabled" section at the bottom
        bool override_liveedit = demo_data.LiveEditOverride;
        if (disable_all)
            BeginDisabled(true);
        if (override_liveedit)
        {
            PushItemFlag(ImGuiItemFlags_LiveEditOnInputText, (demo_data.LiveEditFlags & ImGuiItemFlags_LiveEditOnInputText) != 0);
            PushItemFlag(ImGuiItemFlags_LiveEditOnInputScalar, (demo_data.LiveEditFlags & ImGuiItemFlags_LiveEditOnInputScalar) != 0);
        }

        DemoWindowWidgetsBasic();
        DemoWindowWidgetsBullets();
        DemoWindowWidgetsCollapsingHeaders();
        DemoWindowWidgetsComboBoxes();
        DemoWindowWidgetsColorAndPickers();
        DemoWindowWidgetsDataTypes();

        if (disable_all)
            EndDisabled();
        DemoWindowWidgetsDisableBlocks(demo_data);
        if (disable_all)
            BeginDisabled(true);

        DemoWindowWidgetsDragAndDrop();
        DemoWindowWidgetsDragsAndSliders();
        DemoWindowWidgetsFonts();
        DemoWindowWidgetsImages();
        DemoWindowWidgetsListBoxes();
        DemoWindowWidgetsLiveEdit(demo_data);
        DemoWindowWidgetsMultiComponents();
        DemoWindowWidgetsPlotting();
        DemoWindowWidgetsProgressBars();
        DemoWindowWidgetsQueryingStatuses();
        DemoWindowWidgetsSelectables();
        DemoWindowWidgetsSelectionAndMultiSelect(demo_data);
        DemoWindowWidgetsTabs();
        DemoWindowWidgetsText();
        DemoWindowWidgetsTextFilter();
        DemoWindowWidgetsTextInput();
        DemoWindowWidgetsTooltips();
        DemoWindowWidgetsTreeNodes();
        DemoWindowWidgetsVerticalSliders();

        if (override_liveedit)
        {
            PopItemFlag();
            PopItemFlag();
        }
        if (disable_all)
            EndDisabled();
    }

    // -------------------------------------------------------------------------
    // File-private helpers (prefixed DemoW3_ so they cannot collide with other files)
    // -------------------------------------------------------------------------

    // C++: ImVec4 ImColor::HSV(float h, float s, float v, float a) — the port's ImColor helper has no HSV factory.
    private static ImVec4 DemoW3_ImColorHSV(float h, float s, float v, float a = 1.0f)
    {
        ColorConvertHSVtoRGB(h, s, v, out float r, out float g, out float b);
        return new ImVec4(r, g, b, a);
    }

    // C++: 'struct TextFilters { static int FilterCasingSwap(ImGuiInputTextCallbackData* data) }'
    // (local to DemoWindowWidgetsTextInput(), "Filtered Text Input" section)
    // Modify character input by altering 'data.EventChar' (ImGuiInputTextFlags_CallbackCharFilter callback)
    private static int DemoW3_FilterCasingSwap(ImGuiInputTextCallbackData data)
    {
        if (data.EventChar >= 'a' && data.EventChar <= 'z') { data.EventChar -= 'a' - 'A'; } // Lowercase becomes uppercase
        else if (data.EventChar >= 'A' && data.EventChar <= 'Z') { data.EventChar += 'a' - 'A'; } // Uppercase becomes lowercase
        return 0;
    }

    // C++: 'struct TextFilters { static int FilterImGuiLetters(ImGuiInputTextCallbackData* data) }'
    // Return 0 (pass) if the character is 'i' or 'm' or 'g' or 'u' or 'i', otherwise return 1 (filter out)
    private static int DemoW3_FilterImGuiLetters(ImGuiInputTextCallbackData data)
    {
        if (data.EventChar < 256 && "imgui".Contains((char)data.EventChar))
            return 0;
        return 1;
    }

    // C++: 'struct Funcs { static int MyCallback(ImGuiInputTextCallbackData* data) }'
    // (local to DemoWindowWidgetsTextInput(), "Completion, History, Edit Callbacks" section)
    private static int DemoW3_MyCallback(ImGuiInputTextCallbackData data)
    {
        if (data.EventFlag == ImGuiInputTextFlags_CallbackCompletion)
        {
            data.InsertChars(data.CursorPos, "..");
        }
        else if (data.EventFlag == ImGuiInputTextFlags_CallbackHistory)
        {
            if (data.EventKey == ImGuiKey_UpArrow)
            {
                data.DeleteChars(0, data.BufTextLen);
                data.InsertChars(0, "Pressed Up!");
                data.SelectAll();
            }
            else if (data.EventKey == ImGuiKey_DownArrow)
            {
                data.DeleteChars(0, data.BufTextLen);
                data.InsertChars(0, "Pressed Down!");
                data.SelectAll();
            }
        }
        else if (data.EventFlag == ImGuiInputTextFlags_CallbackEdit)
        {
            // Toggle casing of first character
            char c = (char)data.Buf[0];
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) data.Buf[0] ^= 32;
            data.BufDirty = true;

            // Increment a counter (C++: 'int* p_int = (int*)data->UserData; *p_int = *p_int + 1;'
            // the port passes the shared int[] box DemoWidgets3Locals.ti_edit_count as UserData).
            int[] p_int = (int[])data.UserData;
            p_int[0] = p_int[0] + 1;
        }
        return 0;
    }

    // C++: 'struct Funcs { static int MyResizeCallback(ImGuiInputTextCallbackData* data) }'
    // (local to DemoWindowWidgetsTextInput(), "Resize Callback" section)
    private static int DemoW3_MyResizeCallback(ImGuiInputTextCallbackData data)
    {
        if (data.EventFlag == ImGuiInputTextFlags_CallbackResize)
        {
            ImVector<byte> my_str = (ImVector<byte>)data.UserData;
            // C++: IM_ASSERT(my_str->begin() == data->Buf) — the ImVector owns data.Buf in the port too.
            my_str.resize(data.BufSize); // NB: On resizing calls, generally data.BufSize == data.BufTextLen + 1
            data.Buf = my_str.Data;
        }
        return 0;
    }

    // C++: 'struct Funcs { static bool MyInputTextMultiline(const char* label, ImVector<char>* my_str, const ImVec2& size = ImVec2(0,0), ImGuiInputTextFlags flags = 0) }'
    // Note: Because ImGui:: is a namespace you would typically add your own function into the namespace.
    // For example, you code may declare a function 'ImGui::InputText(const char* label, MyString* my_str)'
    private static bool DemoW3_MyInputTextMultiline(string label, ImVector<byte> my_str, ImVec2 size, ImGuiInputTextFlags flags)
    {
        //IM_ASSERT((flags & ImGuiInputTextFlags_CallbackResize) == 0);
        return InputTextMultiline(label, my_str.Data, my_str.Size, size, flags | ImGuiInputTextFlags_CallbackResize, DemoW3_MyResizeCallback, my_str);
    }
}
