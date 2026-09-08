// Port of imgui_demo.cpp [SECTION] Widgets (part 1), C++ lines 925-2101:
//   DemoWindowWidgetsBasic / DemoWindowWidgetsBullets / DemoWindowWidgetsCollapsingHeaders /
//   DemoWindowWidgetsColorAndPickers / DemoWindowWidgetsComboBoxes / DemoWindowWidgetsDataTypes /
//   DemoWindowWidgetsDisableBlocks / DemoWindowWidgetsDragAndDrop / DemoWindowWidgetsDragsAndSliders /
//   DemoWindowWidgetsFonts / DemoWindowWidgetsImages / DemoWindowWidgetsListBoxes /
//   DemoWindowWidgetsLiveEdit / DemoWindowWidgetsMultiComponents
using System;
using System.Text;

namespace ImGui;

// Function-local 'static' variables of the demo functions in this file (C++ function-local statics).
// Fields are prefixed by their host function to keep them apart (Basic/CollapsingHeaders/ColorPickers/
// ComboBoxes/DataTypes/DragAndDrop/DragsSliders/Images/ListBoxes/LiveEdit/MultiComponents).
static class DemoWidgets1Locals
{
    // DemoWindowWidgetsBasic()
    public static int basic_clicked = 0;                                    // C++: static int clicked
    public static bool basic_check = true;                                  // C++: static bool check
    public static int basic_e = 0;                                          // C++: static int e
    public static int basic_counter = 0;                                    // C++: static int counter
    public static byte[] basic_str0 = new byte[128];                        // C++: static char str0[128] = "Hello, world!"
    public static byte[] basic_str1 = new byte[128];                        // C++: static char str1[128] = ""
    public static int basic_i0 = 123;                                       // C++: static int i0
    public static float basic_f0 = 0.001f;                                  // C++: static float f0
    public static double basic_d0 = 999999.00000001;                        // C++: static double d0
    public static float basic_f1 = 1.0e10f;                                  // C++: static float f1 (scientific)
    public static float[] basic_vec4a = { 0.10f, 0.20f, 0.30f, 0.44f };     // C++: static float vec4a[4]
    public static int basic_drag_i1 = 50, basic_drag_i2 = 42, basic_drag_i3 = 128; // C++: static int i1, i2, i3
    public static float basic_drag_f1 = 1.00f, basic_drag_f2 = 0.0067f;     // C++: static float f1, f2
    public static int basic_slider_i1 = 0;                                  // C++: static int i1
    public static float basic_slider_f1 = 0.123f, basic_slider_f2 = 0.0f;   // C++: static float f1, f2
    public static float basic_angle = 0.0f;                                 // C++: static float angle
    public static int basic_elem = 0;                                       // C++: static int elem (Element_Fire)
    public static float[] basic_col1 = { 1.0f, 0.0f, 0.2f };                // C++: static float col1[3]
    public static float[] basic_col2 = { 0.4f, 0.7f, 0.0f, 0.5f };          // C++: static float col2[4]
    public static int basic_combo_item_current = 0;                         // C++: static int item_current
    public static int basic_listbox_item_current = 1;                       // C++: static int item_current

    // DemoWindowWidgetsCollapsingHeaders()
    public static bool ch_closable_group = true;                            // C++: static bool closable_group

    // DemoWindowWidgetsColorAndPickers()
    // C++ stores 'color' as an ImVec4 and passes (float*)&color to the edit functions: here we store
    // the same storage as float[4] and build ImVec4 copies where a by-value ImVec4 is required.
    public static float[] cp_color = { 114.0f / 255.0f, 144.0f / 255.0f, 154.0f / 255.0f, 200.0f / 255.0f }; // C++: static ImVec4 color
    public static int cp_base_flags = ImGuiColorEditFlags_None;             // C++: static ImGuiColorEditFlags base_flags
    public static bool cp_saved_palette_init = true;                        // C++: static bool saved_palette_init
    public static float[][] cp_saved_palette = new float[32][];             // C++: static ImVec4 saved_palette[32]
    public static float[] cp_backup_color = new float[4];                   // C++: static ImVec4 backup_color
    public static bool cp_no_border = false;                                // C++: static bool no_border
    public static bool cp_ref_color = false;                                // C++: static bool ref_color
    public static float[] cp_ref_color_v = { 1.0f, 0.0f, 1.0f, 0.5f };      // C++: static ImVec4 ref_color_v
    public static int cp_picker_mode = 0;                                   // C++: static int picker_mode
    public static int cp_display_mode = 0;                                  // C++: static int display_mode
    public static int cp_color_picker_flags = ImGuiColorEditFlags_AlphaBar; // C++: static ImGuiColorEditFlags color_picker_flags
    public static float[] cp_color_hsv = { 0.23f, 1.0f, 1.0f, 1.0f };       // C++: static ImVec4 color_hsv (Stored as HSV!)

    // DemoWindowWidgetsComboBoxes()
    public static int cb_flags = 0;                                         // C++: static ImGuiComboFlags flags
    public static int cb_item_selected_idx = 0;                             // C++: static int item_selected_idx
    public static ImGuiTextFilter cb_filter = new ImGuiTextFilter();        // C++: static ImGuiTextFilter filter
    public static int cb_item_current_2 = 0;                                // C++: static int item_current_2
    public static int cb_item_current_3 = -1;                               // C++: static int item_current_3 (If the selection isn't within 0..count, Combo won't display a preview)
    public static int cb_item_current_4 = 0;                                // C++: static int item_current_4

    // DemoWindowWidgetsDataTypes() — state
    public static sbyte[] dt_s8_v = { 127 };                                // C++: static char s8_v = 127
    public static byte[] dt_u8_v = { 255 };                                 // C++: static ImU8 u8_v = 255
    public static short[] dt_s16_v = { 32767 };                             // C++: static short s16_v = 32767
    public static ushort[] dt_u16_v = { 65535 };                            // C++: static ImU16 u16_v = 65535
    public static int[] dt_s32_v = { -1 };                                  // C++: static ImS32 s32_v = -1
    public static uint[] dt_u32_v = { uint.MaxValue };                      // C++: static ImU32 u32_v = (ImU32)-1
    public static long[] dt_s64_v = { -1 };                                 // C++: static ImS64 s64_v = -1
    public static ulong[] dt_u64_v = { ulong.MaxValue };                    // C++: static ImU64 u64_v = (ImU64)-1
    public static float[] dt_f32_v = { 0.123f };                            // C++: static float f32_v = 0.123f
    public static double[] dt_f64_v = { 90000.01234567890123456789 };       // C++: static double f64_v = ...
    public static bool dt_drag_clamp = false;                               // C++: static bool drag_clamp
    public static bool dt_inputs_step = true;                               // C++: static bool inputs_step
    public static int dt_flags = ImGuiInputTextFlags_None;                  // C++: static ImGuiInputTextFlags flags

    // DemoWindowWidgetsDataTypes() — setup limits (as helper variables so we can take their address, as explained above)
    // Note: SliderScalar() functions have a maximum usable range of half the natural type maximum, hence the /2.
    // (C# port: 1-element arrays emulating &variable.)
    public static readonly sbyte[] dt_s8_zero = { 0 }, dt_s8_one = { 1 }, dt_s8_fifty = { 50 }, dt_s8_min = { -128 }, dt_s8_max = { 127 };
    public static readonly byte[] dt_u8_zero = { 0 }, dt_u8_one = { 1 }, dt_u8_fifty = { 50 }, dt_u8_min = { 0 }, dt_u8_max = { 255 };
    public static readonly short[] dt_s16_zero = { 0 }, dt_s16_one = { 1 }, dt_s16_fifty = { 50 }, dt_s16_min = { -32768 }, dt_s16_max = { 32767 };
    public static readonly ushort[] dt_u16_zero = { 0 }, dt_u16_one = { 1 }, dt_u16_fifty = { 50 }, dt_u16_min = { 0 }, dt_u16_max = { 65535 };
    public static readonly int[] dt_s32_zero = { 0 }, dt_s32_one = { 1 }, dt_s32_fifty = { 50 },
        dt_s32_min = { int.MinValue / 2 }, dt_s32_max = { int.MaxValue / 2 },
        dt_s32_hi_a = { int.MaxValue / 2 - 100 }, dt_s32_hi_b = { int.MaxValue / 2 };
    public static readonly uint[] dt_u32_zero = { 0 }, dt_u32_one = { 1 }, dt_u32_fifty = { 50 },
        dt_u32_min = { 0 }, dt_u32_max = { uint.MaxValue / 2 },
        dt_u32_hi_a = { uint.MaxValue / 2 - 100 }, dt_u32_hi_b = { uint.MaxValue / 2 };
    public static readonly long[] dt_s64_zero = { 0 }, dt_s64_one = { 1 }, dt_s64_fifty = { 50 },
        dt_s64_min = { long.MinValue / 2 }, dt_s64_max = { long.MaxValue / 2 },
        dt_s64_hi_a = { long.MaxValue / 2 - 100 }, dt_s64_hi_b = { long.MaxValue / 2 };
    public static readonly ulong[] dt_u64_zero = { 0 }, dt_u64_one = { 1 }, dt_u64_fifty = { 50 },
        dt_u64_min = { 0 }, dt_u64_max = { ulong.MaxValue / 2 },
        dt_u64_hi_a = { ulong.MaxValue / 2 - 100 }, dt_u64_hi_b = { ulong.MaxValue / 2 };
    public static readonly float[] dt_f32_zero = { 0.0f }, dt_f32_one = { 1.0f }, dt_f32_lo_a = { -10000000000.0f }, dt_f32_hi_a = { +10000000000.0f };
    public static readonly double[] dt_f64_zero = { 0.0 }, dt_f64_one = { 1.0 }, dt_f64_lo_a = { -1000000000000000.0 }, dt_f64_hi_a = { +1000000000000000.0 };

    // DemoWindowWidgetsDragAndDrop()
    public static int dnd_mode = 0;                                         // C++: static int mode (Mode_Copy)
    public static string[] dnd_names =                                      // C++: static const char* names[9]
    {
        "Bobby", "Beatrice", "Betty",
        "Brianna", "Barry", "Bernard",
        "Bibi", "Blaine", "Bryn",
    };
    public static string[] dnd_item_names = { "Item One", "Item Two", "Item Three", "Item Four", "Item Five" }; // C++: static const char* item_names[]
    public static float[] dnd_col1 = { 1.0f, 0.0f, 0.2f };                  // C++: static float col1[3]
    public static float[] dnd_col2 = { 0.4f, 0.7f, 0.0f, 0.5f };            // C++: static float col2[4]
    public static ImVec4 dnd_col4 = new ImVec4(1.0f, 0.0f, 0.2f, 1.0f);     // C++: static ImVec4 col4

    // DemoWindowWidgetsDragsAndSliders()
    public static int ds_flags = ImGuiSliderFlags_None;                     // C++: static ImGuiSliderFlags flags
    public static float ds_drag_f = 0.5f;                                   // C++: static float drag_f
    public static float[] ds_drag_f4 = new float[4];                        // C++: static float drag_f4[4]
    public static int ds_drag_i = 50;                                       // C++: static int drag_i
    public static float ds_slider_f = 0.5f;                                 // C++: static float slider_f
    public static float[] ds_slider_f4 = new float[4];                      // C++: static float slider_f4[4]
    public static int ds_slider_i = 50;                                     // C++: static int slider_i

    // DemoWindowWidgetsImages()
    public static ExampleImageViewerData img_image_viewer = new ExampleImageViewerData(); // C++: static ExampleImageViewerData image_viewer
    public static int img_pressed_count = 0;                                // C++: static int pressed_count

    // DemoWindowWidgetsListBoxes()
    public static int lb_item_selected_idx = 0;                             // C++: static int item_selected_idx
    public static bool lb_item_highlight = false;                           // C++: static bool item_highlight

    // DemoWindowWidgetsLiveEdit()
    public static byte[] le_str = new byte[32];                             // C++: static char str[32] = ""
    public static int le_i = 0;                                             // C++: static int i
    public static float le_f = 0.0f;                                        // C++: static float f

    // DemoWindowWidgetsMultiComponents()
    public static float[] mc_vec4f = { 0.10f, 0.20f, 0.30f, 0.44f };        // C++: static float vec4f[4]
    public static int[] mc_vec4i = { 1, 5, 100, 255 };                      // C++: static int vec4i[4]
    public static int mc_flags = 0;                                         // C++: static ImGuiSliderFlags flags
    public static float mc_begin = 10, mc_end = 90;                         // C++: static float begin, end
    public static int mc_begin_i = 100, mc_end_i = 1000;                    // C++: static int begin_i, end_i

    static DemoWidgets1Locals()
    {
        // basic_str0 = "Hello, world!" (zero-padded, NUL-terminated)
        byte[] hello = Encoding.UTF8.GetBytes("Hello, world!");
        Array.Copy(hello, basic_str0, hello.Length);

        // Allocate palette rows here; the HSV fill happens lazily in DemoWindowWidgetsColorAndPickers()
        // to mirror the C++ 'saved_palette_init' flow (palette will persist and can be edited).
        for (int n = 0; n < cp_saved_palette.Length; n++)
            cp_saved_palette[n] = new float[4];
    }
}

public static partial class ImGui
{
    // C++ FLT_MAX/FLT_MIN (FLT_MIN is the smallest positive *normalized* float, not the most negative value).
    private const float DemoW1_FLT_MAX = float.MaxValue;
    private const float DemoW1_FLT_MIN = 1.17549435e-38f;

    // C++: static void DemoWindowWidgetsBasic()  (imgui_demo.cpp 925-1134)
    public static void DemoWindowWidgetsBasic()
    {
        if (TreeNode("Basic"))
        {
            SeparatorText("General");

            if (Button("Button"))
                DemoWidgets1Locals.basic_clicked++;
            if ((DemoWidgets1Locals.basic_clicked & 1) != 0)
            {
                SameLine();
                Text("Thanks for clicking me!");
            }

            Checkbox("checkbox", ref DemoWidgets1Locals.basic_check);

            RadioButton("radio a", ref DemoWidgets1Locals.basic_e, 0); SameLine();
            RadioButton("radio b", ref DemoWidgets1Locals.basic_e, 1); SameLine();
            RadioButton("radio c", ref DemoWidgets1Locals.basic_e, 2);

            AlignTextToFramePadding();
            TextLinkOpenURL("Hyperlink", "https://github.com/ocornut/imgui/wiki/Error-Handling");

            // Color buttons, demonstrate using PushID() to add unique identifier in the ID stack, and changing style.
            for (int i = 0; i < 7; i++)
            {
                if (i > 0)
                    SameLine();
                PushID(i);
                PushStyleColor(ImGuiCol_Button, DemoW1_ImColorHSV(i / 7.0f, 0.6f, 0.6f));
                PushStyleColor(ImGuiCol_ButtonHovered, DemoW1_ImColorHSV(i / 7.0f, 0.7f, 0.7f));
                PushStyleColor(ImGuiCol_ButtonActive, DemoW1_ImColorHSV(i / 7.0f, 0.8f, 0.8f));
                Button("Click");
                PopStyleColor(3);
                PopID();
            }

            // Use AlignTextToFramePadding() to align text baseline to the baseline of framed widgets elements
            // (otherwise a Text+SameLine+Button sequence will have the text a little too high by default!)
            // See 'Demo->Layout->Text Baseline Alignment' for details.
            AlignTextToFramePadding();
            Text("Hold to repeat:");
            SameLine();

            // Arrow buttons with Repeater
            float spacing = GetStyle().ItemInnerSpacing.x;
            PushItemFlag(ImGuiItemFlags_ButtonRepeat, true);
            if (ArrowButton("##left", ImGuiDir_Left)) { DemoWidgets1Locals.basic_counter--; }
            SameLine(0.0f, spacing);
            if (ArrowButton("##right", ImGuiDir_Right)) { DemoWidgets1Locals.basic_counter++; }
            PopItemFlag();
            SameLine();
            Text("%d", DemoWidgets1Locals.basic_counter);

            Button("Tooltip");
            SetItemTooltip("I am a tooltip");

            LabelText("label", "Value");

            SeparatorText("Inputs");

            // If you want to use InputText() with std::string or any custom dynamic string type:
            // - For std::string: use the wrapper in misc/cpp/imgui_stdlib.h/.cpp
            // - Otherwise, see the 'Dear ImGui Demo->Widgets->Text Input->Resize Callback' for using ImGuiInputTextFlags_CallbackResize.
            // (C# port: InputText() takes a byte[] buffer.)
            InputText("input text", DemoWidgets1Locals.basic_str0, DemoWidgets1Locals.basic_str0.Length);
            SameLine(); HelpMarker(
                "USER:\n" +
                "Hold Shift or use mouse to select text.\n" +
                "Ctrl+Left/Right to word jump.\n" +
                "Ctrl+A or Double-Click to select all.\n" +
                "Ctrl+X,Ctrl+C,Ctrl+V for clipboard.\n" +
                "Ctrl+Z to undo, Ctrl+Y/Ctrl+Shift+Z to redo.\n" +
                "Escape to revert.\n\n" +
                "PROGRAMMER:\n" +
                "You can use the ImGuiInputTextFlags_CallbackResize facility if you need to wire InputText() " +
                "to a dynamic string type. See misc/cpp/imgui_stdlib.h for an example (this is not demonstrated " +
                "in imgui_demo.cpp).");

            InputTextWithHint("input text (w/ hint)", "enter text here", DemoWidgets1Locals.basic_str1, DemoWidgets1Locals.basic_str1.Length);

            InputInt("input int", ref DemoWidgets1Locals.basic_i0);

            InputFloat("input float", ref DemoWidgets1Locals.basic_f0, 0.01f, 1.0f, "%.3f");

            InputDouble("input double", ref DemoWidgets1Locals.basic_d0, 0.01f, 1.0f, "%.8f");

            InputFloat("input scientific", ref DemoWidgets1Locals.basic_f1, 0.0f, 0.0f, "%e");
            SameLine(); HelpMarker(
                "You can input value using the scientific notation,\n" +
                "  e.g. \"1e+8\" becomes \"100000000\".");

            InputFloat3("input float3", DemoWidgets1Locals.basic_vec4a);

            SeparatorText("Drags");

            DragInt("drag int", ref DemoWidgets1Locals.basic_drag_i1, 1);
            SameLine(); HelpMarker(
                "Click and drag to edit value.\n" +
                "Hold Shift/Alt for faster/slower edit.\n" +
                "Double-Click or Ctrl+Click to input value.");
            DragInt("drag int 0..100", ref DemoWidgets1Locals.basic_drag_i2, 1, 0, 100, "%d%%", ImGuiSliderFlags_AlwaysClamp);
            DragInt("drag int wrap 100..200", ref DemoWidgets1Locals.basic_drag_i3, 1, 100, 200, "%d", ImGuiSliderFlags_WrapAround);

            DragFloat("drag float", ref DemoWidgets1Locals.basic_drag_f1, 0.005f);
            DragFloat("drag small float", ref DemoWidgets1Locals.basic_drag_f2, 0.0001f, 0.0f, 0.0f, "%.06f ns");
            //ImGui::DragFloat("drag wrap -1..1", &f3, 0.005f, -1.0f, 1.0f, NULL, ImGuiSliderFlags_WrapAround);

            SeparatorText("Sliders");

            SliderInt("slider int", ref DemoWidgets1Locals.basic_slider_i1, -1, 3);
            SameLine(); HelpMarker("Ctrl+Click to input value.");

            SliderFloat("slider float", ref DemoWidgets1Locals.basic_slider_f1, 0.0f, 1.0f, "ratio = %.3f");
            SliderFloat("slider float (log)", ref DemoWidgets1Locals.basic_slider_f2, -10.0f, 10.0f, "%.4f", ImGuiSliderFlags_Logarithmic);

            SliderAngle("slider angle", ref DemoWidgets1Locals.basic_angle);

            // Using the format string to display a name instead of an integer.
            // Here we completely omit '%d' from the format string, so it'll only display a name.
            // This technique can also be used with DragInt().
            const int Element_Fire = 0, Element_Earth = 1, Element_Air = 2, Element_Water = 3, Element_COUNT = 4;
            int elem = DemoWidgets1Locals.basic_elem;
            string[] elems_names = { "Fire", "Earth", "Air", "Water" };
            string elem_name = (elem >= 0 && elem < Element_COUNT) ? elems_names[elem] : "Unknown";
            SliderInt("slider enum", ref DemoWidgets1Locals.basic_elem, 0, Element_COUNT - 1, elem_name); // Use ImGuiSliderFlags_NoInput flag to disable Ctrl+Click here.
            SameLine(); HelpMarker("Using the format string parameter to display a name instead of the underlying integer.");

            SeparatorText("Selectors/Pickers");

            ColorEdit3("color 1", DemoWidgets1Locals.basic_col1);
            SameLine(); HelpMarker(
                "Click on the color square to open a color picker.\n" +
                "Click and hold to use drag and drop.\n" +
                "Right-Click on the color square to show options.\n" +
                "Ctrl+Click on individual component to input value.\n");

            ColorEdit4("color 2", DemoWidgets1Locals.basic_col2);

            // Using the _simplified_ one-liner Combo() api here
            // See "Combo" section for examples of how to use the more flexible BeginCombo()/EndCombo() api.
            string[] items = { "AAAA", "BBBB", "CCCC", "DDDD", "EEEE", "FFFF", "GGGG", "HHHH", "IIIIIII", "JJJJ", "KKKKKKK" };
            Combo("combo", ref DemoWidgets1Locals.basic_combo_item_current, items, items.Length);
            SameLine(); HelpMarker(
                "Using the simplified one-liner Combo API here.\n" +
                "Refer to the \"Combo\" section below for an explanation of how to use the more flexible and general BeginCombo/EndCombo API.");

            // Using the _simplified_ one-liner ListBox() api here
            // See "List boxes" section for examples of how to use the more flexible BeginListBox()/EndListBox() api.
            string[] items2 = { "Apple", "Banana", "Cherry", "Kiwi", "Mango", "Orange", "Pineapple", "Strawberry", "Watermelon" };
            ListBox("listbox", ref DemoWidgets1Locals.basic_listbox_item_current, items2, items2.Length, 4);
            SameLine(); HelpMarker(
                "Using the simplified one-liner ListBox API here.\n" +
                "Refer to the \"List boxes\" section below for an explanation of how to use the more flexible and general BeginListBox/EndListBox API.");

            // Testing ImGuiOnceUponAFrame helper.
            //static ImGuiOnceUponAFrame once;
            //for (int i = 0; i < 5; i++)
            //    if (once)
            //        ImGui::Text("This will be displayed only once.");

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsBullets()  (imgui_demo.cpp 1140-1156)
    public static void DemoWindowWidgetsBullets()
    {
        if (TreeNode("Bullets"))
        {
            BulletText("Bullet point 1");
            BulletText("Bullet point 2\nOn multiple lines");
            if (TreeNode("Tree node"))
            {
                BulletText("Another bullet point");
                TreePop();
            }
            Bullet(); Text("Bullet point 3 (two calls)");
            Bullet(); SmallButton("Button");
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsCollapsingHeaders()  (imgui_demo.cpp 1162-1187)
    public static void DemoWindowWidgetsCollapsingHeaders()
    {
        if (TreeNode("Collapsing Headers"))
        {
            Checkbox("Show 2nd header", ref DemoWidgets1Locals.ch_closable_group);
            if (CollapsingHeader("Header", ImGuiTreeNodeFlags_None))
            {
                Text("IsItemHovered: %d", IsItemHovered() ? 1 : 0);
                for (int i = 0; i < 5; i++)
                    Text("Some content %d", i);
            }
            if (CollapsingHeader("Header with a close button", ref DemoWidgets1Locals.ch_closable_group))
            {
                Text("IsItemHovered: %d", IsItemHovered() ? 1 : 0);
                for (int i = 0; i < 5; i++)
                    Text("More content %d", i);
            }
            /*
            if (ImGui::CollapsingHeader("Header with a bullet", ImGuiTreeNodeFlags_Bullet))
                ImGui::Text("IsItemHovered: %d", ImGui::IsItemHovered());
            */
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsColorAndPickers()  (imgui_demo.cpp 1193-1389)
    public static void DemoWindowWidgetsColorAndPickers()
    {
        if (TreeNode("Color/Picker Widgets"))
        {
            SeparatorText("Options");
            CheckboxFlags("ImGuiColorEditFlags_NoAlpha", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_NoAlpha);
            CheckboxFlags("ImGuiColorEditFlags_AlphaOpaque", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_AlphaOpaque);
            CheckboxFlags("ImGuiColorEditFlags_AlphaNoBg", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_AlphaNoBg);
            CheckboxFlags("ImGuiColorEditFlags_AlphaPreviewHalf", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_AlphaPreviewHalf);
            CheckboxFlags("ImGuiColorEditFlags_NoOptions", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_NoOptions); SameLine(); HelpMarker("Right-click on the individual color widget to show options.");
            CheckboxFlags("ImGuiColorEditFlags_NoDragDrop", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_NoDragDrop);
            CheckboxFlags("ImGuiColorEditFlags_NoColorMarkers", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_NoColorMarkers);
            CheckboxFlags("ImGuiColorEditFlags_HDR", ref DemoWidgets1Locals.cp_base_flags, ImGuiColorEditFlags_HDR); SameLine(); HelpMarker("Currently all this does is to lift the 0..1 limits on dragging widgets.");

            SeparatorText("Inline color editor");
            Text("Color widget:");
            SameLine(); HelpMarker(
                "Click on the color square to open a color picker.\n" +
                "Ctrl+Click on individual component to input value.\n");
            ColorEdit3("MyColor##1", DemoWidgets1Locals.cp_color, DemoWidgets1Locals.cp_base_flags);

            Text("Color widget HSV with Alpha:");
            ColorEdit4("MyColor##2", DemoWidgets1Locals.cp_color, ImGuiColorEditFlags_DisplayHSV | DemoWidgets1Locals.cp_base_flags);

            Text("Color widget with Float Display:");
            ColorEdit4("MyColor##2f", DemoWidgets1Locals.cp_color, ImGuiColorEditFlags_Float | DemoWidgets1Locals.cp_base_flags);

            Text("Color button with Picker:");
            SameLine(); HelpMarker(
                "With the ImGuiColorEditFlags_NoInputs flag you can hide all the slider/text inputs.\n" +
                "With the ImGuiColorEditFlags_NoLabel flag you can pass a non-empty label which will only " +
                "be used for the tooltip and picker popup.");
            ColorEdit4("MyColor##3", DemoWidgets1Locals.cp_color, ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_NoLabel | DemoWidgets1Locals.cp_base_flags);

            Text("Color button with Custom Picker Popup:");

            // Generate a default palette. The palette will persist and can be edited.
            if (DemoWidgets1Locals.cp_saved_palette_init)
            {
                for (int n = 0; n < DemoWidgets1Locals.cp_saved_palette.Length; n++)
                {
                    float[] pal = DemoWidgets1Locals.cp_saved_palette[n];
                    ColorConvertHSVtoRGB(n / 31.0f, 0.8f, 0.8f, out pal[0], out pal[1], out pal[2]);
                    pal[3] = 1.0f; // Alpha
                }
                DemoWidgets1Locals.cp_saved_palette_init = false;
            }

            bool open_popup = ColorButton("MyColor##3b", DemoW1_ToImVec4(DemoWidgets1Locals.cp_color), DemoWidgets1Locals.cp_base_flags);
            SameLine(0, GetStyle().ItemInnerSpacing.x);
            open_popup |= Button("Palette");
            if (open_popup)
            {
                OpenPopup("mypicker");
                Array.Copy(DemoWidgets1Locals.cp_color, DemoWidgets1Locals.cp_backup_color, 4);
            }
            if (BeginPopup("mypicker"))
            {
                Text("MY CUSTOM COLOR PICKER WITH AN AMAZING PALETTE!");
                Separator();
                ColorPicker4("##picker", DemoWidgets1Locals.cp_color, DemoWidgets1Locals.cp_base_flags | ImGuiColorEditFlags_NoSidePreview | ImGuiColorEditFlags_NoSmallPreview);
                SameLine();

                BeginGroup(); // Lock X position
                Text("Current");
                ColorButton("##current", DemoW1_ToImVec4(DemoWidgets1Locals.cp_color), ImGuiColorEditFlags_NoPicker | ImGuiColorEditFlags_AlphaPreviewHalf, new ImVec2(60, 40));
                Text("Previous");
                if (ColorButton("##previous", DemoW1_ToImVec4(DemoWidgets1Locals.cp_backup_color), ImGuiColorEditFlags_NoPicker | ImGuiColorEditFlags_AlphaPreviewHalf, new ImVec2(60, 40)))
                    Array.Copy(DemoWidgets1Locals.cp_backup_color, DemoWidgets1Locals.cp_color, 4);
                Separator();
                Text("Palette");
                for (int n = 0; n < DemoWidgets1Locals.cp_saved_palette.Length; n++)
                {
                    PushID(n);
                    if ((n % 8) != 0)
                        SameLine(0.0f, GetStyle().ItemSpacing.y);

                    ImGuiColorEditFlags palette_button_flags = ImGuiColorEditFlags_NoAlpha | ImGuiColorEditFlags_NoPicker | ImGuiColorEditFlags_NoTooltip;
                    float[] pal_n = DemoWidgets1Locals.cp_saved_palette[n];
                    if (ColorButton("##palette", DemoW1_ToImVec4(pal_n), palette_button_flags, new ImVec2(20, 20)))
                    {
                        // C++: color = ImVec4(saved_palette[n].x, saved_palette[n].y, saved_palette[n].z, color.w) — Preserve alpha!
                        DemoWidgets1Locals.cp_color[0] = pal_n[0];
                        DemoWidgets1Locals.cp_color[1] = pal_n[1];
                        DemoWidgets1Locals.cp_color[2] = pal_n[2];
                    }

                    // Allow user to drop colors into each palette entry. Note that ColorButton() is already a
                    // drag source by default, unless specifying the ImGuiColorEditFlags_NoDragDrop flag.
                    if (BeginDragDropTarget())
                    {
                        ImGuiPayload payload;
                        if ((payload = AcceptDragDropPayload(IMGUI_PAYLOAD_TYPE_COLOR_3F)) != null)
                            for (int c = 0; c < 3; c++)
                                pal_n[c] = BitConverter.ToSingle(payload.Data, c * 4);
                        if ((payload = AcceptDragDropPayload(IMGUI_PAYLOAD_TYPE_COLOR_4F)) != null)
                            for (int c = 0; c < 4; c++)
                                pal_n[c] = BitConverter.ToSingle(payload.Data, c * 4);
                        EndDragDropTarget();
                    }

                    PopID();
                }
                EndGroup();
                EndPopup();
            }

            Text("Color button only:");
            Checkbox("ImGuiColorEditFlags_NoBorder", ref DemoWidgets1Locals.cp_no_border);
            ColorButton("MyColor##3c", DemoW1_ToImVec4(DemoWidgets1Locals.cp_color), DemoWidgets1Locals.cp_base_flags | (DemoWidgets1Locals.cp_no_border ? ImGuiColorEditFlags_NoBorder : 0), new ImVec2(80, 80));

            SeparatorText("Color picker");

            PushID("Color picker");
            CheckboxFlags("ImGuiColorEditFlags_NoAlpha", ref DemoWidgets1Locals.cp_color_picker_flags, ImGuiColorEditFlags_NoAlpha);
            CheckboxFlags("ImGuiColorEditFlags_AlphaBar", ref DemoWidgets1Locals.cp_color_picker_flags, ImGuiColorEditFlags_AlphaBar);
            CheckboxFlags("ImGuiColorEditFlags_NoSidePreview", ref DemoWidgets1Locals.cp_color_picker_flags, ImGuiColorEditFlags_NoSidePreview);
            if ((DemoWidgets1Locals.cp_color_picker_flags & ImGuiColorEditFlags_NoSidePreview) != 0)
            {
                SameLine();
                Checkbox("With Ref Color", ref DemoWidgets1Locals.cp_ref_color);
                if (DemoWidgets1Locals.cp_ref_color)
                {
                    SameLine();
                    ColorEdit4("##RefColor", DemoWidgets1Locals.cp_ref_color_v, ImGuiColorEditFlags_NoInputs | DemoWidgets1Locals.cp_base_flags);
                }
            }
            CheckboxFlags("ImGuiColorEditFlags_PickerNoRotate", ref DemoWidgets1Locals.cp_color_picker_flags, ImGuiColorEditFlags_PickerNoRotate);

            Combo("Picker Mode", ref DemoWidgets1Locals.cp_picker_mode, "Auto/Current\0ImGuiColorEditFlags_PickerHueBar\0ImGuiColorEditFlags_PickerHueWheel\0");
            SameLine(); HelpMarker("When not specified explicitly, user can right-click the picker to change mode.");

            Combo("Display Mode", ref DemoWidgets1Locals.cp_display_mode, "Auto/Current\0ImGuiColorEditFlags_NoInputs\0ImGuiColorEditFlags_DisplayRGB\0ImGuiColorEditFlags_DisplayHSV\0ImGuiColorEditFlags_DisplayHex\0");
            SameLine(); HelpMarker(
                "ColorEdit defaults to displaying RGB inputs if you don't specify a display mode, " +
                "but the user can change it with a right-click on those inputs.\n\nColorPicker defaults to displaying RGB+HSV+Hex " +
                "if you don't specify a display mode.\n\nYou can change the defaults using io.ConfigColorEditFlags.");

            ImGuiColorEditFlags flags = DemoWidgets1Locals.cp_base_flags | DemoWidgets1Locals.cp_color_picker_flags;
            if (DemoWidgets1Locals.cp_picker_mode == 1) flags |= ImGuiColorEditFlags_PickerHueBar;
            if (DemoWidgets1Locals.cp_picker_mode == 2) flags |= ImGuiColorEditFlags_PickerHueWheel;
            if (DemoWidgets1Locals.cp_display_mode == 1) flags |= ImGuiColorEditFlags_NoInputs;       // Disable all RGB/HSV/Hex displays
            if (DemoWidgets1Locals.cp_display_mode == 2) flags |= ImGuiColorEditFlags_DisplayRGB;     // Override display mode
            if (DemoWidgets1Locals.cp_display_mode == 3) flags |= ImGuiColorEditFlags_DisplayHSV;
            if (DemoWidgets1Locals.cp_display_mode == 4) flags |= ImGuiColorEditFlags_DisplayHex;
            ColorPicker4("MyColor##4", DemoWidgets1Locals.cp_color, flags, DemoWidgets1Locals.cp_ref_color ? DemoWidgets1Locals.cp_ref_color_v : null);

            Text("Set defaults in code:");
            SameLine(); HelpMarker(
                "io.ConfigColorEditFlags is designed to allow you to set boot-time default.\n" +
                "We don't have Push/Pop functions because you can force options on a per-widget basis if needed, " +
                "and the user can change non-forced ones with the options menu.\nWe don't have a getter to avoid " +
                "encouraging you to persistently save values that aren't forward-compatible.");
            if (Button("Overwrite default: Uint8 + HSV + Hue Bar"))
                GetIO().ConfigColorEditFlags = ImGuiColorEditFlags_Uint8 | ImGuiColorEditFlags_DisplayHSV | ImGuiColorEditFlags_PickerHueBar;
            if (Button("Overwrite default: Float + HDR + Hue Wheel"))
                GetIO().ConfigColorEditFlags = ImGuiColorEditFlags_Float | ImGuiColorEditFlags_HDR | ImGuiColorEditFlags_PickerHueWheel;

            // Always display a small version of both types of pickers
            // (that's in order to make it more visible in the demo to people who are skimming quickly through it)
            Text("Both types:");
            float w = (GetContentRegionAvail().x - GetStyle().ItemSpacing.y) * 0.40f;
            SetNextItemWidth(w);
            ColorPicker3("##MyColor##5", DemoWidgets1Locals.cp_color, ImGuiColorEditFlags_PickerHueBar | ImGuiColorEditFlags_NoSidePreview | ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_NoAlpha);
            SameLine();
            SetNextItemWidth(w);
            ColorPicker3("##MyColor##6", DemoWidgets1Locals.cp_color, ImGuiColorEditFlags_PickerHueWheel | ImGuiColorEditFlags_NoSidePreview | ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_NoAlpha);
            PopID();

            // HSV encoded support (to avoid RGB<>HSV round trips and singularities when S==0 or V==0)
            Spacing();
            Text("HSV encoded colors");
            SameLine(); HelpMarker(
                "By default, colors are given to ColorEdit and ColorPicker in RGB, but ImGuiColorEditFlags_InputHSV " +
                "allows you to store colors as HSV and pass them to ColorEdit and ColorPicker as HSV. This comes with the " +
                "added benefit that you can manipulate hue values with the picker even when saturation or value are zero.");
            Text("Color widget with InputHSV:");
            ColorEdit4("HSV shown as RGB##1", DemoWidgets1Locals.cp_color_hsv, ImGuiColorEditFlags_DisplayRGB | ImGuiColorEditFlags_InputHSV | ImGuiColorEditFlags_Float);
            ColorEdit4("HSV shown as HSV##1", DemoWidgets1Locals.cp_color_hsv, ImGuiColorEditFlags_DisplayHSV | ImGuiColorEditFlags_InputHSV | ImGuiColorEditFlags_Float);
            DragFloat4("Raw HSV values", DemoWidgets1Locals.cp_color_hsv, 0.01f, 0.0f, 1.0f);

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsComboBoxes()  (imgui_demo.cpp 1395-1486)
    public static void DemoWindowWidgetsComboBoxes()
    {
        if (TreeNode("Combo"))
        {
            // Combo Boxes are also called "Dropdown" in other systems
            // Expose flags as checkbox for the demo
            CheckboxFlags("ImGuiComboFlags_PopupAlignLeft", ref DemoWidgets1Locals.cb_flags, ImGuiComboFlags_PopupAlignLeft);
            SameLine(); HelpMarker("Only makes a difference if the popup is larger than the combo");
            if (CheckboxFlags("ImGuiComboFlags_NoArrowButton", ref DemoWidgets1Locals.cb_flags, ImGuiComboFlags_NoArrowButton))
                DemoWidgets1Locals.cb_flags &= ~ImGuiComboFlags_NoPreview;     // Clear incompatible flags
            if (CheckboxFlags("ImGuiComboFlags_NoPreview", ref DemoWidgets1Locals.cb_flags, ImGuiComboFlags_NoPreview))
                DemoWidgets1Locals.cb_flags &= ~(ImGuiComboFlags_NoArrowButton | ImGuiComboFlags_WidthFitPreview); // Clear incompatible flags
            if (CheckboxFlags("ImGuiComboFlags_WidthFitPreview", ref DemoWidgets1Locals.cb_flags, ImGuiComboFlags_WidthFitPreview))
                DemoWidgets1Locals.cb_flags &= ~ImGuiComboFlags_NoPreview;

            // Override default popup height
            if (CheckboxFlags("ImGuiComboFlags_HeightSmall", ref DemoWidgets1Locals.cb_flags, ImGuiComboFlags_HeightSmall))
                DemoWidgets1Locals.cb_flags &= ~(ImGuiComboFlags_HeightMask_ & ~ImGuiComboFlags_HeightSmall);
            if (CheckboxFlags("ImGuiComboFlags_HeightRegular", ref DemoWidgets1Locals.cb_flags, ImGuiComboFlags_HeightRegular))
                DemoWidgets1Locals.cb_flags &= ~(ImGuiComboFlags_HeightMask_ & ~ImGuiComboFlags_HeightRegular);
            if (CheckboxFlags("ImGuiComboFlags_HeightLargest", ref DemoWidgets1Locals.cb_flags, ImGuiComboFlags_HeightLargest))
                DemoWidgets1Locals.cb_flags &= ~(ImGuiComboFlags_HeightMask_ & ~ImGuiComboFlags_HeightLargest);

            // Using the generic BeginCombo() API, you have full control over how to display the combo contents.
            // (your selection data could be an index, a pointer to the object, an id for the object, a flag intrusively
            // stored in the object itself, etc.)
            string[] items = { "AAAA", "BBBB", "CCCC", "DDDD", "EEEE", "FFFF", "GGGG", "HHHH", "IIII", "JJJJ", "KKKK", "LLLLLLL", "MMMM", "OOOOOOO" };
            // Here we store our selection data as an index.

            // Pass in the preview value visible before opening the combo (it could technically be different contents or not pulled from items[])
            string combo_preview_value = items[DemoWidgets1Locals.cb_item_selected_idx];
            if (BeginCombo("combo 1", combo_preview_value, DemoWidgets1Locals.cb_flags))
            {
                for (int n = 0; n < items.Length; n++)
                {
                    bool is_selected = (DemoWidgets1Locals.cb_item_selected_idx == n);
                    if (Selectable(items[n], is_selected))
                        DemoWidgets1Locals.cb_item_selected_idx = n;

                    // Set the initial focus when opening the combo (scrolling + keyboard navigation focus)
                    if (is_selected)
                        SetItemDefaultFocus();
                }
                EndCombo();
            }

            // Show case embedding a filter using a simple trick: displaying the filter inside combo contents.
            // See https://github.com/ocornut/imgui/issues/718 for advanced/esoteric alternatives.
            if (BeginCombo("combo 2 (w/ filter)", combo_preview_value, DemoWidgets1Locals.cb_flags))
            {
                ImGuiTextFilter filter = DemoWidgets1Locals.cb_filter;
                if (IsWindowAppearing())
                {
                    SetKeyboardFocusHere();
                    filter.Clear();
                }
                SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_F);
                filter.Draw("##Filter", -DemoW1_FLT_MIN);

                for (int n = 0; n < items.Length; n++)
                {
                    bool is_selected = (DemoWidgets1Locals.cb_item_selected_idx == n);
                    if (filter.PassFilter(items[n]))
                        if (Selectable(items[n], is_selected))
                            DemoWidgets1Locals.cb_item_selected_idx = n;
                }
                EndCombo();
            }

            Spacing();
            SeparatorText("One-liner variants");
            HelpMarker("The Combo() function is not greatly useful apart from cases were you want to embed all options in a single strings.\nFlags above don't apply to this section.");

            // Simplified one-liner Combo() API, using values packed in a single constant string
            // This is a convenience for when the selection set is small and known at compile-time.
            Combo("combo 3 (one-liner)", ref DemoWidgets1Locals.cb_item_current_2, "aaaa\0bbbb\0cccc\0dddd\0eeee\0\0");

            // Simplified one-liner Combo() using an array of const char*
            // This is not very useful (may obsolete): prefer using BeginCombo()/EndCombo() for full control.
            Combo("combo 4 (array)", ref DemoWidgets1Locals.cb_item_current_3, items, items.Length);

            // Simplified one-liner Combo() using an accessor function
            Combo("combo 5 (function)", ref DemoWidgets1Locals.cb_item_current_4, (data, n) => ((string[])data)[n], items, items.Length);

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsDataTypes()  (imgui_demo.cpp 1492-1621)
    public static void DemoWindowWidgetsDataTypes()
    {
        if (TreeNode("Data Types"))
        {
            // DragScalar/InputScalar/SliderScalar functions allow various data types
            // - signed/unsigned
            // - 8/16/32/64-bits
            // - integer/float/double
            // To avoid polluting the public API with all possible combinations, we use the ImGuiDataType enum
            // to pass the type, and passing all arguments by pointer.
            // This is the reason the test code below creates local variables to hold "zero" "one" etc. for each type.
            // In practice, if you frequently use a given type that is not covered by the normal API entry points,
            // you can wrap it yourself inside a 1 line function which can take typed argument as value instead of void*,
            // and then pass their address to the generic function. For example:
            //   bool MySliderU64(const char *label, u64* value, u64 min = 0, u64 max = 0, const char* format = "%lld")
            //   {
            //      return SliderScalar(label, ImGuiDataType_U64, value, &min, &max, format);
            //   }
            //
            // (C# port note: pointers are emulated with 1-element arrays, and the DemoW1_*Scalar() helpers
            //  below pin them via 'fixed' before calling the IntPtr-based DragScalar/SliderScalar/InputScalar.)

            // Setup limits (as helper variables so we can take their address, as explained above)
            // Note: SliderScalar() functions have a maximum usable range of half the natural type maximum, hence the /2.
            // (Stored in DemoWidgets1Locals as dt_* arrays.)

            float drag_speed = 0.2f;
            SeparatorText("Drags");
            Checkbox("Clamp integers to 0..50", ref DemoWidgets1Locals.dt_drag_clamp);
            SameLine(); HelpMarker(
                "As with every widget in dear imgui, we never modify values unless there is a user interaction.\n" +
                "You can override the clamping limits by using Ctrl+Click to input a value.");
            bool drag_clamp = DemoWidgets1Locals.dt_drag_clamp;
            DemoW1_DragScalar("drag s8", ImGuiDataType_S8, DemoWidgets1Locals.dt_s8_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_s8_zero : null, drag_clamp ? DemoWidgets1Locals.dt_s8_fifty : null);
            DemoW1_DragScalar("drag u8", ImGuiDataType_U8, DemoWidgets1Locals.dt_u8_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_u8_zero : null, drag_clamp ? DemoWidgets1Locals.dt_u8_fifty : null, "%u ms");
            DemoW1_DragScalar("drag s16", ImGuiDataType_S16, DemoWidgets1Locals.dt_s16_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_s16_zero : null, drag_clamp ? DemoWidgets1Locals.dt_s16_fifty : null);
            DemoW1_DragScalar("drag u16", ImGuiDataType_U16, DemoWidgets1Locals.dt_u16_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_u16_zero : null, drag_clamp ? DemoWidgets1Locals.dt_u16_fifty : null, "%u ms");
            DemoW1_DragScalar("drag s32", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_s32_zero : null, drag_clamp ? DemoWidgets1Locals.dt_s32_fifty : null);
            DemoW1_DragScalar("drag s32 hex", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_s32_zero : null, drag_clamp ? DemoWidgets1Locals.dt_s32_fifty : null, "0x%08X");
            DemoW1_DragScalar("drag u32", ImGuiDataType_U32, DemoWidgets1Locals.dt_u32_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_u32_zero : null, drag_clamp ? DemoWidgets1Locals.dt_u32_fifty : null, "%u ms");
            DemoW1_DragScalar("drag s64", ImGuiDataType_S64, DemoWidgets1Locals.dt_s64_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_s64_zero : null, drag_clamp ? DemoWidgets1Locals.dt_s64_fifty : null);
            DemoW1_DragScalar("drag u64", ImGuiDataType_U64, DemoWidgets1Locals.dt_u64_v, drag_speed, drag_clamp ? DemoWidgets1Locals.dt_u64_zero : null, drag_clamp ? DemoWidgets1Locals.dt_u64_fifty : null);
            DemoW1_DragScalar("drag float", ImGuiDataType_Float, DemoWidgets1Locals.dt_f32_v, 0.005f, DemoWidgets1Locals.dt_f32_zero, DemoWidgets1Locals.dt_f32_one, "%f");
            DemoW1_DragScalar("drag float log", ImGuiDataType_Float, DemoWidgets1Locals.dt_f32_v, 0.005f, DemoWidgets1Locals.dt_f32_zero, DemoWidgets1Locals.dt_f32_one, "%f", ImGuiSliderFlags_Logarithmic);
            DemoW1_DragScalar("drag double", ImGuiDataType_Double, DemoWidgets1Locals.dt_f64_v, 0.0005f, DemoWidgets1Locals.dt_f64_zero, null, "%.10f grams");
            DemoW1_DragScalar("drag double log", ImGuiDataType_Double, DemoWidgets1Locals.dt_f64_v, 0.0005f, DemoWidgets1Locals.dt_f64_zero, DemoWidgets1Locals.dt_f64_one, "0 < %.10f < 1", ImGuiSliderFlags_Logarithmic);

            SeparatorText("Sliders");
            DemoW1_SliderScalar("slider s8 full", ImGuiDataType_S8, DemoWidgets1Locals.dt_s8_v, DemoWidgets1Locals.dt_s8_min, DemoWidgets1Locals.dt_s8_max, "%d");
            DemoW1_SliderScalar("slider u8 full", ImGuiDataType_U8, DemoWidgets1Locals.dt_u8_v, DemoWidgets1Locals.dt_u8_min, DemoWidgets1Locals.dt_u8_max, "%u");
            DemoW1_SliderScalar("slider s16 full", ImGuiDataType_S16, DemoWidgets1Locals.dt_s16_v, DemoWidgets1Locals.dt_s16_min, DemoWidgets1Locals.dt_s16_max, "%d");
            DemoW1_SliderScalar("slider u16 full", ImGuiDataType_U16, DemoWidgets1Locals.dt_u16_v, DemoWidgets1Locals.dt_u16_min, DemoWidgets1Locals.dt_u16_max, "%u");
            DemoW1_SliderScalar("slider s32 low", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, DemoWidgets1Locals.dt_s32_zero, DemoWidgets1Locals.dt_s32_fifty, "%d");
            DemoW1_SliderScalar("slider s32 high", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, DemoWidgets1Locals.dt_s32_hi_a, DemoWidgets1Locals.dt_s32_hi_b, "%d");
            DemoW1_SliderScalar("slider s32 full", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, DemoWidgets1Locals.dt_s32_min, DemoWidgets1Locals.dt_s32_max, "%d");
            DemoW1_SliderScalar("slider s32 hex", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, DemoWidgets1Locals.dt_s32_zero, DemoWidgets1Locals.dt_s32_fifty, "0x%04X");
            DemoW1_SliderScalar("slider u32 low", ImGuiDataType_U32, DemoWidgets1Locals.dt_u32_v, DemoWidgets1Locals.dt_u32_zero, DemoWidgets1Locals.dt_u32_fifty, "%u");
            DemoW1_SliderScalar("slider u32 high", ImGuiDataType_U32, DemoWidgets1Locals.dt_u32_v, DemoWidgets1Locals.dt_u32_hi_a, DemoWidgets1Locals.dt_u32_hi_b, "%u");
            DemoW1_SliderScalar("slider u32 full", ImGuiDataType_U32, DemoWidgets1Locals.dt_u32_v, DemoWidgets1Locals.dt_u32_min, DemoWidgets1Locals.dt_u32_max, "%u");
            DemoW1_SliderScalar("slider s64 low", ImGuiDataType_S64, DemoWidgets1Locals.dt_s64_v, DemoWidgets1Locals.dt_s64_zero, DemoWidgets1Locals.dt_s64_fifty, "%lld"); // C++: "%" PRId64
            DemoW1_SliderScalar("slider s64 high", ImGuiDataType_S64, DemoWidgets1Locals.dt_s64_v, DemoWidgets1Locals.dt_s64_hi_a, DemoWidgets1Locals.dt_s64_hi_b, "%lld"); // C++: "%" PRId64
            DemoW1_SliderScalar("slider s64 full", ImGuiDataType_S64, DemoWidgets1Locals.dt_s64_v, DemoWidgets1Locals.dt_s64_min, DemoWidgets1Locals.dt_s64_max, "%lld"); // C++: "%" PRId64
            DemoW1_SliderScalar("slider u64 low", ImGuiDataType_U64, DemoWidgets1Locals.dt_u64_v, DemoWidgets1Locals.dt_u64_zero, DemoWidgets1Locals.dt_u64_fifty, "%llu ms"); // C++: "%" PRIu64 " ms"
            DemoW1_SliderScalar("slider u64 high", ImGuiDataType_U64, DemoWidgets1Locals.dt_u64_v, DemoWidgets1Locals.dt_u64_hi_a, DemoWidgets1Locals.dt_u64_hi_b, "%llu ms"); // C++: "%" PRIu64 " ms"
            DemoW1_SliderScalar("slider u64 full", ImGuiDataType_U64, DemoWidgets1Locals.dt_u64_v, DemoWidgets1Locals.dt_u64_min, DemoWidgets1Locals.dt_u64_max, "%llu ms"); // C++: "%" PRIu64 " ms"
            DemoW1_SliderScalar("slider float low", ImGuiDataType_Float, DemoWidgets1Locals.dt_f32_v, DemoWidgets1Locals.dt_f32_zero, DemoWidgets1Locals.dt_f32_one);
            DemoW1_SliderScalar("slider float low log", ImGuiDataType_Float, DemoWidgets1Locals.dt_f32_v, DemoWidgets1Locals.dt_f32_zero, DemoWidgets1Locals.dt_f32_one, "%.10f", ImGuiSliderFlags_Logarithmic);
            DemoW1_SliderScalar("slider float high", ImGuiDataType_Float, DemoWidgets1Locals.dt_f32_v, DemoWidgets1Locals.dt_f32_lo_a, DemoWidgets1Locals.dt_f32_hi_a, "%e");
            DemoW1_SliderScalar("slider double low", ImGuiDataType_Double, DemoWidgets1Locals.dt_f64_v, DemoWidgets1Locals.dt_f64_zero, DemoWidgets1Locals.dt_f64_one, "%.10f grams");
            DemoW1_SliderScalar("slider double low log", ImGuiDataType_Double, DemoWidgets1Locals.dt_f64_v, DemoWidgets1Locals.dt_f64_zero, DemoWidgets1Locals.dt_f64_one, "%.10f", ImGuiSliderFlags_Logarithmic);
            DemoW1_SliderScalar("slider double high", ImGuiDataType_Double, DemoWidgets1Locals.dt_f64_v, DemoWidgets1Locals.dt_f64_lo_a, DemoWidgets1Locals.dt_f64_hi_a, "%e grams");

            SeparatorText("Sliders (reverse)");
            DemoW1_SliderScalar("slider s8 reverse", ImGuiDataType_S8, DemoWidgets1Locals.dt_s8_v, DemoWidgets1Locals.dt_s8_max, DemoWidgets1Locals.dt_s8_min, "%d");
            DemoW1_SliderScalar("slider u8 reverse", ImGuiDataType_U8, DemoWidgets1Locals.dt_u8_v, DemoWidgets1Locals.dt_u8_max, DemoWidgets1Locals.dt_u8_min, "%u");
            DemoW1_SliderScalar("slider s32 reverse", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, DemoWidgets1Locals.dt_s32_fifty, DemoWidgets1Locals.dt_s32_zero, "%d");
            DemoW1_SliderScalar("slider u32 reverse", ImGuiDataType_U32, DemoWidgets1Locals.dt_u32_v, DemoWidgets1Locals.dt_u32_fifty, DemoWidgets1Locals.dt_u32_zero, "%u");
            DemoW1_SliderScalar("slider s64 reverse", ImGuiDataType_S64, DemoWidgets1Locals.dt_s64_v, DemoWidgets1Locals.dt_s64_fifty, DemoWidgets1Locals.dt_s64_zero, "%lld"); // C++: "%" PRId64
            DemoW1_SliderScalar("slider u64 reverse", ImGuiDataType_U64, DemoWidgets1Locals.dt_u64_v, DemoWidgets1Locals.dt_u64_fifty, DemoWidgets1Locals.dt_u64_zero, "%llu ms"); // C++: "%" PRIu64 " ms"

            SeparatorText("Inputs");
            Checkbox("Show step buttons", ref DemoWidgets1Locals.dt_inputs_step);
            CheckboxFlags("ImGuiInputTextFlags_ReadOnly", ref DemoWidgets1Locals.dt_flags, ImGuiInputTextFlags_ReadOnly);
            CheckboxFlags("ImGuiInputTextFlags_ParseEmptyRefVal", ref DemoWidgets1Locals.dt_flags, ImGuiInputTextFlags_ParseEmptyRefVal);
            CheckboxFlags("ImGuiInputTextFlags_DisplayEmptyRefVal", ref DemoWidgets1Locals.dt_flags, ImGuiInputTextFlags_DisplayEmptyRefVal);
            bool inputs_step = DemoWidgets1Locals.dt_inputs_step;
            DemoW1_InputScalar("input s8", ImGuiDataType_S8, DemoWidgets1Locals.dt_s8_v, inputs_step ? DemoWidgets1Locals.dt_s8_one : null, null, "%d", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input u8", ImGuiDataType_U8, DemoWidgets1Locals.dt_u8_v, inputs_step ? DemoWidgets1Locals.dt_u8_one : null, null, "%u", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input s16", ImGuiDataType_S16, DemoWidgets1Locals.dt_s16_v, inputs_step ? DemoWidgets1Locals.dt_s16_one : null, null, "%d", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input u16", ImGuiDataType_U16, DemoWidgets1Locals.dt_u16_v, inputs_step ? DemoWidgets1Locals.dt_u16_one : null, null, "%u", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input s32", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, inputs_step ? DemoWidgets1Locals.dt_s32_one : null, null, "%d", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input s32 hex", ImGuiDataType_S32, DemoWidgets1Locals.dt_s32_v, inputs_step ? DemoWidgets1Locals.dt_s32_one : null, null, "%04X", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input u32", ImGuiDataType_U32, DemoWidgets1Locals.dt_u32_v, inputs_step ? DemoWidgets1Locals.dt_u32_one : null, null, "%u", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input u32 hex", ImGuiDataType_U32, DemoWidgets1Locals.dt_u32_v, inputs_step ? DemoWidgets1Locals.dt_u32_one : null, null, "%08X", DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input s64", ImGuiDataType_S64, DemoWidgets1Locals.dt_s64_v, inputs_step ? DemoWidgets1Locals.dt_s64_one : null, null, null, DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input u64", ImGuiDataType_U64, DemoWidgets1Locals.dt_u64_v, inputs_step ? DemoWidgets1Locals.dt_u64_one : null, null, null, DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input float", ImGuiDataType_Float, DemoWidgets1Locals.dt_f32_v, inputs_step ? DemoWidgets1Locals.dt_f32_one : null, null, null, DemoWidgets1Locals.dt_flags);
            DemoW1_InputScalar("input double", ImGuiDataType_Double, DemoWidgets1Locals.dt_f64_v, inputs_step ? DemoWidgets1Locals.dt_f64_one : null, null, null, DemoWidgets1Locals.dt_flags);

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsDisableBlocks(ImGuiDemoWindowData* demo_data)  (imgui_demo.cpp 1627-1636)
    public static void DemoWindowWidgetsDisableBlocks(ImGuiDemoWindowData demo_data)
    {
        if (TreeNode("Disable Blocks"))
        {
            Checkbox("Disable entire section above", ref demo_data.DisableSections);
            SameLine(); HelpMarker("Demonstrate using BeginDisabled()/EndDisabled() across other sections.");
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsDragAndDrop()  (imgui_demo.cpp 1642-1794)
    public static void DemoWindowWidgetsDragAndDrop()
    {
        if (TreeNode("Drag and Drop"))
        {
            if (TreeNode("Drag and drop in standard widgets"))
            {
                // ColorEdit widgets automatically act as drag source and drag target.
                // They are using standardized payload strings IMGUI_PAYLOAD_TYPE_COLOR_3F and IMGUI_PAYLOAD_TYPE_COLOR_4F
                // to allow your own widgets to use colors in their drag and drop interaction.
                // Also see 'Demo->Widgets->Color/Picker Widgets->Palette' demo.
                HelpMarker("You can drag from the color squares.");
                ColorEdit3("color 1", DemoWidgets1Locals.dnd_col1);
                ColorEdit4("color 2", DemoWidgets1Locals.dnd_col2);
                TreePop();
            }

            if (TreeNode("Drag and drop to copy/swap items"))
            {
                const int Mode_Copy = 0, Mode_Move = 1, Mode_Swap = 2;
                if (RadioButton("Copy", DemoWidgets1Locals.dnd_mode == Mode_Copy)) { DemoWidgets1Locals.dnd_mode = Mode_Copy; } SameLine();
                if (RadioButton("Move", DemoWidgets1Locals.dnd_mode == Mode_Move)) { DemoWidgets1Locals.dnd_mode = Mode_Move; } SameLine();
                if (RadioButton("Swap", DemoWidgets1Locals.dnd_mode == Mode_Swap)) { DemoWidgets1Locals.dnd_mode = Mode_Swap; }
                string[] names = DemoWidgets1Locals.dnd_names;
                for (int n = 0; n < names.Length; n++)
                {
                    PushID(n);
                    if ((n % 3) != 0)
                        SameLine();
                    Button(names[n], new ImVec2(60, 60));

                    // Our buttons are both drag sources and drag targets here!
                    if (BeginDragDropSource(ImGuiDragDropFlags_None))
                    {
                        // Set payload to carry the index of our item (could be anything)
                        SetDragDropPayload("DND_DEMO_CELL", BitConverter.GetBytes(n), (ulong)sizeof(int));

                        // Display preview (could be anything, e.g. when dragging an image we could decide to display
                        // the filename and a small preview of the image, etc.)
                        if (DemoWidgets1Locals.dnd_mode == Mode_Copy) { Text("Copy %s", names[n]); }
                        if (DemoWidgets1Locals.dnd_mode == Mode_Move) { Text("Move %s", names[n]); }
                        if (DemoWidgets1Locals.dnd_mode == Mode_Swap) { Text("Swap %s", names[n]); }
                        EndDragDropSource();
                    }
                    if (BeginDragDropTarget())
                    {
                        ImGuiPayload payload = AcceptDragDropPayload("DND_DEMO_CELL");
                        if (payload != null)
                        {
                            System.Diagnostics.Debug.Assert(payload.DataSize == sizeof(int));
                            int payload_n = BitConverter.ToInt32(payload.Data, 0);
                            if (DemoWidgets1Locals.dnd_mode == Mode_Copy)
                            {
                                names[n] = names[payload_n];
                            }
                            if (DemoWidgets1Locals.dnd_mode == Mode_Move)
                            {
                                names[n] = names[payload_n];
                                names[payload_n] = "";
                            }
                            if (DemoWidgets1Locals.dnd_mode == Mode_Swap)
                            {
                                string tmp = names[n];
                                names[n] = names[payload_n];
                                names[payload_n] = tmp;
                            }
                        }
                        EndDragDropTarget();
                    }
                    PopID();
                }
                TreePop();
            }

            if (TreeNode("Drag to reorder items (simple)"))
            {
                // FIXME: there is temporary (usually single-frame) ID Conflict during reordering as a same item may be submitting twice.
                // This code was always slightly faulty but in a way which was not easily noticeable.
                // Until we fix this, enable ImGuiItemFlags_AllowDuplicateId to disable detecting the issue.
                PushItemFlag(ImGuiItemFlags_AllowDuplicateId, true);

                // Simple reordering
                HelpMarker(
                    "We don't use the drag and drop api at all here! " +
                    "Instead we query when the item is held but not hovered, and order items accordingly.");
                string[] item_names = DemoWidgets1Locals.dnd_item_names;
                for (int n = 0; n < item_names.Length; n++)
                {
                    string item = item_names[n];
                    Selectable(item);

                    if (IsItemActive() && !IsItemHovered())
                    {
                        int n_next = n + (GetMouseDragDelta(0).y < 0.0f ? -1 : 1);
                        if (n_next >= 0 && n_next < item_names.Length)
                        {
                            item_names[n] = item_names[n_next];
                            item_names[n_next] = item;
                            ResetMouseDragDelta();
                        }
                    }
                }

                PopItemFlag();
                TreePop();
            }

            if (TreeNode("Tooltip at target location"))
            {
                for (int n = 0; n < 2; n++)
                {
                    // Drop targets
                    Button(n != 0 ? "drop here##1" : "drop here##0");
                    if (BeginDragDropTarget())
                    {
                        ImGuiDragDropFlags drop_target_flags = ImGuiDragDropFlags_AcceptBeforeDelivery | ImGuiDragDropFlags_AcceptNoPreviewTooltip;
                        if (AcceptDragDropPayload(IMGUI_PAYLOAD_TYPE_COLOR_4F, drop_target_flags) != null)
                        {
                            SetMouseCursor(ImGuiMouseCursor_NotAllowed);
                            SetTooltip("Cannot drop here!");
                        }
                        EndDragDropTarget();
                    }

                    // Drop source
                    if (n == 0)
                        ColorButton("drag me", DemoWidgets1Locals.dnd_col4);
                }
                TreePop();
            }

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsDragsAndSliders()  (imgui_demo.cpp 1800-1850)
    public static void DemoWindowWidgetsDragsAndSliders()
    {
        if (TreeNode("Drag/Slider Flags"))
        {
            // Demonstrate using advanced flags for DragXXX and SliderXXX functions. Note that the flags are the same!
            CheckboxFlags("ImGuiSliderFlags_AlwaysClamp", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_AlwaysClamp);
            CheckboxFlags("ImGuiSliderFlags_ClampOnInput", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_ClampOnInput);
            SameLine(); HelpMarker("Clamp value to min/max bounds when input manually with Ctrl+Click. By default Ctrl+Click allows going out of bounds.");
            CheckboxFlags("ImGuiSliderFlags_ClampZeroRange", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_ClampZeroRange);
            SameLine(); HelpMarker("Clamp even if min==max==0.0f. Otherwise DragXXX functions don't clamp.");
            CheckboxFlags("ImGuiSliderFlags_Logarithmic", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_Logarithmic);
            SameLine(); HelpMarker("Enable logarithmic editing (more precision for small values).");
            CheckboxFlags("ImGuiSliderFlags_NoRoundToFormat", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_NoRoundToFormat);
            SameLine(); HelpMarker("Disable rounding underlying value to match precision of the format string (e.g. %.3f values are rounded to those 3 digits).");
            CheckboxFlags("ImGuiSliderFlags_NoInput", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_NoInput);
            SameLine(); HelpMarker("Disable Ctrl+Click or Enter key allowing to input text directly into the widget.");
            CheckboxFlags("ImGuiSliderFlags_NoSpeedTweaks", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_NoSpeedTweaks);
            SameLine(); HelpMarker("Disable keyboard modifiers altering tweak speed. Useful if you want to alter tweak speed yourself based on your own logic.");
            CheckboxFlags("ImGuiSliderFlags_WrapAround", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_WrapAround);
            SameLine(); HelpMarker("Enable wrapping around from max to min and from min to max (only supported by DragXXX() functions)");
            CheckboxFlags("ImGuiSliderFlags_ColorMarkers", ref DemoWidgets1Locals.ds_flags, ImGuiSliderFlags_ColorMarkers);

            // Drags
            Text("Underlying float value: %f", DemoWidgets1Locals.ds_drag_f);
            DragFloat("DragFloat (0 -> 1)", ref DemoWidgets1Locals.ds_drag_f, 0.005f, 0.0f, 1.0f, "%.3f", DemoWidgets1Locals.ds_flags);
            DragFloat("DragFloat (0 -> +inf)", ref DemoWidgets1Locals.ds_drag_f, 0.005f, 0.0f, DemoW1_FLT_MAX, "%.3f", DemoWidgets1Locals.ds_flags);
            DragFloat("DragFloat (-inf -> 1)", ref DemoWidgets1Locals.ds_drag_f, 0.005f, -DemoW1_FLT_MAX, 1.0f, "%.3f", DemoWidgets1Locals.ds_flags);
            DragFloat("DragFloat (-inf -> +inf)", ref DemoWidgets1Locals.ds_drag_f, 0.005f, -DemoW1_FLT_MAX, +DemoW1_FLT_MAX, "%.3f", DemoWidgets1Locals.ds_flags);
            //ImGui::DragFloat("DragFloat (0 -> 0)", &drag_f, 0.005f, 0.0f, 0.0f, "%.3f", flags);           // To test ClampZeroRange
            //ImGui::DragFloat("DragFloat (100 -> 100)", &drag_f, 0.005f, 100.0f, 100.0f, "%.3f", flags);
            DragInt("DragInt (0 -> 100)", ref DemoWidgets1Locals.ds_drag_i, 0.5f, 0, 100, "%d", DemoWidgets1Locals.ds_flags);
            DragFloat4("DragFloat4 (0 -> 1)", DemoWidgets1Locals.ds_drag_f4, 0.005f, 0.0f, 1.0f, "%.3f", DemoWidgets1Locals.ds_flags); // Multi-component item, mostly here to document the effect of ImGuiSliderFlags_ColorMarkers.

            // Sliders
            ImGuiSliderFlags flags_for_sliders = DemoWidgets1Locals.ds_flags & ~ImGuiSliderFlags_WrapAround;
            Text("Underlying float value: %f", DemoWidgets1Locals.ds_slider_f);
            SliderFloat("SliderFloat (0 -> 1)", ref DemoWidgets1Locals.ds_slider_f, 0.0f, 1.0f, "%.3f", flags_for_sliders);
            SliderInt("SliderInt (0 -> 100)", ref DemoWidgets1Locals.ds_slider_i, 0, 100, "%d", flags_for_sliders);
            SliderFloat4("SliderFloat4 (0 -> 1)", DemoWidgets1Locals.ds_slider_f4, 0.0f, 1.0f, "%.3f", DemoWidgets1Locals.ds_flags); // Multi-component item, mostly here to document the effect of ImGuiSliderFlags_ColorMarkers.

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsFonts()  (imgui_demo.cpp 1856-1866)
    public static void DemoWindowWidgetsFonts()
    {
        if (TreeNode("Fonts"))
        {
            ImFontAtlas atlas = GetIO().Fonts;
            ShowFontAtlas(atlas);
            // FIXME-NEWATLAS: Provide a demo to add/create a procedural font?
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsImages()  (imgui_demo.cpp 1872-1948)
    public static void DemoWindowWidgetsImages()
    {
        if (TreeNode("Images"))
        {
            ImGuiIO io = GetIO();
            TextWrapped(
                "Below we are displaying the font texture (which is the only texture we have access to in this demo). " +
                "Use the 'ImTextureID' type as storage to pass pointers or identifier to your own texture data. " +
                "Hover the texture for a zoomed view!");

            // Below we are displaying the font texture because it is the only texture we have access to inside the demo!
            // Read description about ImTextureID/ImTextureRef and FAQ for details about texture identifiers.
            // If you use one of the default imgui_impl_XXXX.cpp rendering backend, they all have comments at the top
            // of their respective source file to specify what they are using as texture identifier, for example:
            // - The imgui_impl_dx11.cpp renderer expect a 'ID3D11ShaderResourceView*' pointer.
            // - The imgui_impl_opengl3.cpp renderer expect a GLuint OpenGL texture identifier, etc.
            // So with the DirectX11 backend, you call ImGui::Image() with a 'ID3D11ShaderResourceView*' cast to ImTextureID.
            // - If you decided that ImTextureID = MyEngineTexture*, then you can pass your MyEngineTexture* pointers
            //   to ImGui::Image(), and gather width/height through your own functions, etc.
            // - You can use ShowMetricsWindow() to inspect the draw data that are being passed to your renderer,
            //   it will help you debug issues if you are confused about it.
            // - Consider using the lower-level ImDrawList::AddImage() API, via ImGui::GetWindowDrawList()->AddImage().
            // - Read https://github.com/ocornut/imgui/blob/master/docs/FAQ.md
            // - Read https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples

            // Grab the current texture identifier used by the font atlas.
            ImFontAtlas atlas = io.Fonts;
            ImTextureRef my_tex_id = atlas.TexRef;
            float my_tex_w = atlas.TexData.Width; // Regular user code should never have to care about TexData-> fields, but since we want to display the entire texture here, we pull Width/Height from it.
            float my_tex_h = atlas.TexData.Height;
            Text("%.0fx%.0f", my_tex_w, my_tex_h);

            // Basic drawing
            SeparatorText("Image()/ImageWithBg() function");
            ImVec2 uv_min = new ImVec2(0.0f, 0.0f); // Top-left
            ImVec2 uv_max = new ImVec2(1.0f, 1.0f); // Lower-right
            PushStyleVar(ImGuiStyleVar_ImageBorderSize, ImMath.ImMax(1.0f, GetStyle().ImageBorderSize));
            ImageWithBg(my_tex_id, new ImVec2(my_tex_w, my_tex_h), uv_min, uv_max, new ImVec4(0.0f, 0.0f, 0.0f, 1.0f));
            PopStyleVar();

            // Fancy widget
            SeparatorText("Interactive Image Viewer");
            ExampleImageViewerData image_viewer = DemoWidgets1Locals.img_image_viewer;
            ImVec2 canvas_size = new ImVec2(GetContentRegionAvail().x, my_tex_h * 2.0f);
            ExampleImageViewer_DrawOptions(image_viewer);
            ExampleImageViewer_DrawCanvas(image_viewer, canvas_size, my_tex_id, (int)my_tex_w, (int)my_tex_h);

            SeparatorText("Textured Buttons");
            TextWrapped("And now some textured buttons..");
            for (int i = 0; i < 8; i++)
            {
                // UV coordinates are often (0.0f, 0.0f) and (1.0f, 1.0f) to display an entire textures.
                // Here are trying to display only a 32x32 pixels area of the texture, hence the UV computation.
                // Read about UV coordinates here: https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples
                PushID(i);
                if (i > 0)
                    PushStyleVar(ImGuiStyleVar_FramePadding, new ImVec2(i - 1.0f, i - 1.0f));
                ImVec2 size = new ImVec2(32.0f, 32.0f);                         // Size of the image we want to make visible
                ImVec2 uv0 = new ImVec2(0.0f, 0.0f);                            // UV coordinates for lower-left
                ImVec2 uv1 = new ImVec2(32.0f / my_tex_w, 32.0f / my_tex_h);    // UV coordinates for (32,32) in our texture
                ImVec4 bg_col = new ImVec4(0.0f, 0.0f, 0.0f, 1.0f);             // Black background
                ImVec4 tint_col = new ImVec4(1.0f, 1.0f, 1.0f, 1.0f);           // No tint
                if (ImageButton("", my_tex_id, size, uv0, uv1, bg_col, tint_col))
                    DemoWidgets1Locals.img_pressed_count += 1;
                if (i > 0)
                    PopStyleVar();
                PopID();
                SameLine();
            }
            NewLine();
            Text("Pressed %d times.", DemoWidgets1Locals.img_pressed_count);
            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsListBoxes()  (imgui_demo.cpp 1954-2013)
    public static void DemoWindowWidgetsListBoxes()
    {
        if (TreeNode("List Boxes"))
        {
            // BeginListBox() is essentially a thin wrapper to using BeginChild()/EndChild()
            // using the ImGuiChildFlags_FrameStyle flag for stylistic changes + displaying a label.
            // You may be tempted to simply use BeginChild() directly. However note that BeginChild() requires EndChild()
            // to always be called (inconsistent with BeginListBox()/EndListBox()).

            // Using the generic BeginListBox() API, you have full control over how to display the combo contents.
            // (your selection data could be an index, a pointer to the object, an id for the object, a flag intrusively
            // stored in the object itself, etc.)
            string[] items = { "AAAA", "BBBB", "CCCC", "DDDD", "EEEE", "FFFF", "GGGG", "HHHH", "IIII", "JJJJ", "KKKK", "LLLLLLL", "MMMM", "OOOOOOO" };
            // Here we store our selected data as an index.

            int item_highlighted_idx = -1; // Here we store our highlighted data as an index.
            Checkbox("Highlight hovered item in second listbox", ref DemoWidgets1Locals.lb_item_highlight);

            if (BeginListBox("listbox 1", new ImVec2(0.0f, 0.0f)))
            {
                for (int n = 0; n < items.Length; n++)
                {
                    bool is_selected = (DemoWidgets1Locals.lb_item_selected_idx == n);
                    if (Selectable(items[n], is_selected))
                        DemoWidgets1Locals.lb_item_selected_idx = n;

                    if (DemoWidgets1Locals.lb_item_highlight && IsItemHovered())
                        item_highlighted_idx = n;

                    // Set the initial focus when opening the combo (scrolling + keyboard navigation focus)
                    if (is_selected)
                        SetItemDefaultFocus();
                }
                EndListBox();
            }
            SameLine(); HelpMarker("Here we are sharing selection state between both boxes.");

            // Custom size: use all width, 5 items tall
            Text("Full-width:");
            if (BeginListBox("##listbox 2", new ImVec2(-DemoW1_FLT_MIN, 5 * GetTextLineHeightWithSpacing())))
            {
                for (int n = 0; n < items.Length; n++)
                {
                    bool is_selected = (DemoWidgets1Locals.lb_item_selected_idx == n);
                    ImGuiSelectableFlags flags = (item_highlighted_idx == n) ? ImGuiSelectableFlags_Highlight : 0;
                    if (Selectable(items[n], is_selected, flags))
                        DemoWidgets1Locals.lb_item_selected_idx = n;

                    // Set the initial focus when opening the combo (scrolling + keyboard navigation focus)
                    if (is_selected)
                        SetItemDefaultFocus();
                }
                EndListBox();
            }

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsLiveEdit(ImGuiDemoWindowData* demo_data)  (imgui_demo.cpp 2019-2051)
    public static void DemoWindowWidgetsLiveEdit(ImGuiDemoWindowData demo_data)
    {
        if (TreeNode("Live Edit Flags"))
        {
            TextWrapped("Select whether to apply keyboard edits to backing variables _while_ typing.");

            Checkbox("Override Live Edit Flags in Demo Window", ref demo_data.LiveEditOverride);
            if (!demo_data.LiveEditOverride)
                demo_data.LiveEditFlags = GetItemFlags();

            BeginDisabled(demo_data.LiveEditOverride == false);
            Indent();
            CheckboxFlags("ImGuiItemFlags_LiveEditOnInputText", ref demo_data.LiveEditFlags, ImGuiItemFlags_LiveEditOnInputText);
            CheckboxFlags("ImGuiItemFlags_LiveEditOnInputScalar", ref demo_data.LiveEditFlags, ImGuiItemFlags_LiveEditOnInputScalar);
            Unindent();
            EndDisabled();

            Text("Try typing '123' and seeing effect on backing value:");
            byte[] str = DemoWidgets1Locals.le_str;
            InputText("str", str, str.Length);
            Text("Backing value: \"%s\"", DemoW1_StringFromUtf8(str));
            InputInt("int", ref DemoWidgets1Locals.le_i, 0, 0);
            Text("Backing value: %d", DemoWidgets1Locals.le_i);
            SliderFloat("float", ref DemoWidgets1Locals.le_f, 0.0f, 100.0f);
            Text("Backing value: %f", DemoWidgets1Locals.le_f);

            TreePop();
        }
    }

    // C++: static void DemoWindowWidgetsMultiComponents()  (imgui_demo.cpp 2057-2101)
    public static void DemoWindowWidgetsMultiComponents()
    {
        if (TreeNode("Multi-component Widgets"))
        {
            CheckboxFlags("ImGuiSliderFlags_ColorMarkers", ref DemoWidgets1Locals.mc_flags, ImGuiSliderFlags_ColorMarkers); // Only passing this to Drag/Sliders

            SeparatorText("2-wide");
            InputFloat2("input float2", DemoWidgets1Locals.mc_vec4f);
            InputInt2("input int2", DemoWidgets1Locals.mc_vec4i);
            DragFloat2("drag float2", DemoWidgets1Locals.mc_vec4f, 0.01f, 0.0f, 1.0f, null, DemoWidgets1Locals.mc_flags);
            DragInt2("drag int2", DemoWidgets1Locals.mc_vec4i, 1, 0, 255, null, DemoWidgets1Locals.mc_flags);
            SliderFloat2("slider float2", DemoWidgets1Locals.mc_vec4f, 0.0f, 1.0f, null, DemoWidgets1Locals.mc_flags);
            SliderInt2("slider int2", DemoWidgets1Locals.mc_vec4i, 0, 255, null, DemoWidgets1Locals.mc_flags);

            SeparatorText("3-wide");
            InputFloat3("input float3", DemoWidgets1Locals.mc_vec4f);
            InputInt3("input int3", DemoWidgets1Locals.mc_vec4i);
            DragFloat3("drag float3", DemoWidgets1Locals.mc_vec4f, 0.01f, 0.0f, 1.0f, null, DemoWidgets1Locals.mc_flags);
            DragInt3("drag int3", DemoWidgets1Locals.mc_vec4i, 1, 0, 255, null, DemoWidgets1Locals.mc_flags);
            SliderFloat3("slider float3", DemoWidgets1Locals.mc_vec4f, 0.0f, 1.0f, null, DemoWidgets1Locals.mc_flags);
            SliderInt3("slider int3", DemoWidgets1Locals.mc_vec4i, 0, 255, null, DemoWidgets1Locals.mc_flags);

            SeparatorText("4-wide");
            InputFloat4("input float4", DemoWidgets1Locals.mc_vec4f);
            InputInt4("input int4", DemoWidgets1Locals.mc_vec4i);
            DragFloat4("drag float4", DemoWidgets1Locals.mc_vec4f, 0.01f, 0.0f, 1.0f, null, DemoWidgets1Locals.mc_flags);
            DragInt4("drag int4", DemoWidgets1Locals.mc_vec4i, 1, 0, 255, null, DemoWidgets1Locals.mc_flags);
            SliderFloat4("slider float4", DemoWidgets1Locals.mc_vec4f, 0.0f, 1.0f, null, DemoWidgets1Locals.mc_flags);
            SliderInt4("slider int4", DemoWidgets1Locals.mc_vec4i, 0, 255, null, DemoWidgets1Locals.mc_flags);

            SeparatorText("Ranges");
            DragFloatRange2("range float", ref DemoWidgets1Locals.mc_begin, ref DemoWidgets1Locals.mc_end, 0.25f, 0.0f, 100.0f, "Min: %.1f %%", "Max: %.1f %%", ImGuiSliderFlags_AlwaysClamp);
            DragIntRange2("range int", ref DemoWidgets1Locals.mc_begin_i, ref DemoWidgets1Locals.mc_end_i, 5, 0, 1000, "Min: %d units", "Max: %d units");
            DragIntRange2("range int (no bounds)", ref DemoWidgets1Locals.mc_begin_i, ref DemoWidgets1Locals.mc_end_i, 5, 0, 0, "Min: %d units", "Max: %d units");

            TreePop();
        }
    }

    // -------------------------------------------------------------------------
    // File-private helpers (prefixed DemoW1_ so they cannot collide with other files)
    // -------------------------------------------------------------------------

    // C++: ImVec4 ImColor::HSV(float h, float s, float v, float a) — the port's ImColor helper has no HSV factory.
    private static ImVec4 DemoW1_ImColorHSV(float h, float s, float v, float a = 1.0f)
    {
        ColorConvertHSVtoRGB(h, s, v, out float r, out float g, out float b);
        return new ImVec4(r, g, b, a);
    }

    // float[4] RGBA -> ImVec4 (C++ used an ImVec4 directly; we store colors as float[4] so they can be
    // passed to ColorEdit*/ColorPicker*/DragFloat4 which take float[] in this port).
    private static ImVec4 DemoW1_ToImVec4(float[] col) => new ImVec4(col[0], col[1], col[2], col[3]);

    // C++ NUL-terminated char[] -> C# string (for displaying InputText buffers with Text("%s", ...)).
    private static string DemoW1_StringFromUtf8(byte[] buf)
    {
        int len = 0;
        while (len < buf.Length && buf[len] != 0)
            len++;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    // The C++ demo passes typed pointers to DragScalar/SliderScalar/InputScalar; this port exposes those
    // functions with IntPtr parameters. These helpers pin 1-element arrays (null array == NULL pointer)
    // with 'fixed', mirroring what the port's own DragFloat()/InputInt() wrappers do.

    // C++: ImGui::DragScalar(label, type, &v, speed, p_min, p_max, format, flags)
    private static unsafe bool DemoW1_DragScalar<T>(string label, ImGuiDataType data_type, T[] p_data, float v_speed, T[] p_min, T[] p_max, string format = null, ImGuiSliderFlags flags = 0) where T : unmanaged
    {
        fixed (T* p_data_ptr = p_data)
        fixed (T* p_min_ptr = p_min)
        fixed (T* p_max_ptr = p_max)
            return DragScalar(label, data_type, (IntPtr)p_data_ptr, v_speed,
                p_min != null ? (IntPtr)p_min_ptr : default,
                p_max != null ? (IntPtr)p_max_ptr : default, format, flags);
    }

    // C++: ImGui::SliderScalar(label, type, &v, &p_min, &p_max, format, flags)
    private static unsafe bool DemoW1_SliderScalar<T>(string label, ImGuiDataType data_type, T[] p_data, T[] p_min, T[] p_max, string format = null, ImGuiSliderFlags flags = 0) where T : unmanaged
    {
        fixed (T* p_data_ptr = p_data)
        fixed (T* p_min_ptr = p_min)
        fixed (T* p_max_ptr = p_max)
            return SliderScalar(label, data_type, (IntPtr)p_data_ptr, (IntPtr)p_min_ptr, (IntPtr)p_max_ptr, format, flags);
    }

    // C++: ImGui::InputScalar(label, type, &v, p_step, p_step_fast, format, flags)
    private static unsafe bool DemoW1_InputScalar<T>(string label, ImGuiDataType data_type, T[] p_data, T[] p_step, T[] p_step_fast, string format = null, ImGuiInputTextFlags flags = 0) where T : unmanaged
    {
        fixed (T* p_data_ptr = p_data)
        fixed (T* p_step_ptr = p_step)
        fixed (T* p_step_fast_ptr = p_step_fast)
            return InputScalar(label, data_type, (IntPtr)p_data_ptr,
                p_step != null ? (IntPtr)p_step_ptr : default,
                p_step_fast != null ? (IntPtr)p_step_fast_ptr : default, format, flags);
    }
}
