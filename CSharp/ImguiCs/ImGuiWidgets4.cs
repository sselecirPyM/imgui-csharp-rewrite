// Port of imgui_widgets.cpp ranges:
//   [SECTION] Widgets: ColorEdit, ColorPicker, ColorButton, etc.   (lines 5821-6760)
//   [SECTION] Widgets: TreeNode, CollapsingHeader, etc.            (lines 6760-7431)
//   [SECTION] Widgets: Selectable                                  (lines 7431-7644)
//   [SECTION] Widgets: Typing-Select support                       (lines 7644-7834)
// Also ported here (per assignment):
//   - SeparatorTextEx/SeparatorText (C++ imgui_widgets.cpp 1740-1803, left out of ImGuiWidgets1.cs for this range)
//   - Color conversion helpers ColorConvertRGBtoHSV/HSVtoRGB/U32ToFloat4/Float4ToU32 (C++ imgui.cpp 2878-2956).
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    // Drag and drop payload types (imgui.h)
    private const string IMGUI_PAYLOAD_TYPE_COLOR_3F = "_COL3F";    // float[3]
    private const string IMGUI_PAYLOAD_TYPE_COLOR_4F = "_COL4F";    // float[4]

    // imgui_internal.h: #define IM_F32_TO_INT8_UNBOUND(_VAL) ((int)((_VAL) * 255.0f + ((_VAL)>=0 ? 0.5f : -0.5f)))
    private static int IM_F32_TO_INT8_UNBOUND(float v) => (int)(v * 255.0f + (v >= 0 ? 0.5f : -0.5f));

    // imgui_internal.h: #define IM_F32_TO_INT8_SAT(_VAL) ((int)(ImSaturate(_VAL) * 255.0f + 0.5f))
    private static int IM_F32_TO_INT8_SAT(float v) => (int)(ImMath.ImSaturate(v) * 255.0f + 0.5f);

    //-------------------------------------------------------------------------
    // Color conversion helpers (C++ imgui.cpp [SECTION] MISC HELPERS/UTILITIES)
    //-------------------------------------------------------------------------

    // C++: ImVec4 ImGui::ColorConvertU32ToFloat4(ImU32 in)
    public static ImVec4 ColorConvertU32ToFloat4(ImU32 @in)
    {
        float s = 1.0f / 255.0f;
        return new ImVec4(
            ((@in >> ImGuiCore.IM_COL32_R_SHIFT) & 0xFF) * s,
            ((@in >> ImGuiCore.IM_COL32_G_SHIFT) & 0xFF) * s,
            ((@in >> ImGuiCore.IM_COL32_B_SHIFT) & 0xFF) * s,
            ((@in >> ImGuiCore.IM_COL32_A_SHIFT) & 0xFF) * s);
    }

    // C++: ImU32 ImGui::ColorConvertFloat4ToU32(const ImVec4& in)
    public static ImU32 ColorConvertFloat4ToU32(ImVec4 @in)
    {
        ImU32 @out;
        @out = ((uint)IM_F32_TO_INT8_SAT(@in.x)) << ImGuiCore.IM_COL32_R_SHIFT;
        @out |= ((uint)IM_F32_TO_INT8_SAT(@in.y)) << ImGuiCore.IM_COL32_G_SHIFT;
        @out |= ((uint)IM_F32_TO_INT8_SAT(@in.z)) << ImGuiCore.IM_COL32_B_SHIFT;
        @out |= ((uint)IM_F32_TO_INT8_SAT(@in.w)) << ImGuiCore.IM_COL32_A_SHIFT;
        return @out;
    }

    // Convert rgb floats ([0-1],[0-1],[0-1]) to hsv floats ([0-1],[0-1],[0-1]), from Foley & van Dam p592
    // Optimized http://lolengine.net/blog/2013/01/13/fast-rgb-to-hsv
    // C++: void ImGui::ColorConvertRGBtoHSV(float r, float g, float b, float& out_h, float& out_s, float& out_v)
    public static void ColorConvertRGBtoHSV(float r, float g, float b, out float out_h, out float out_s, out float out_v)
    {
        float K = 0.0f;
        if (g < b)
        {
            (g, b) = (b, g); // ImSwap(g, b)
            K = -1.0f;
        }
        if (r < g)
        {
            (r, g) = (g, r); // ImSwap(r, g)
            K = -2.0f / 6.0f - K;
        }

        float chroma = r - (g < b ? g : b);
        out_h = ImMath.ImFabs(K + (g - b) / (6.0f * chroma + 1e-20f));
        out_s = chroma / (r + 1e-20f);
        out_v = r;
    }

    // Convert hsv floats ([0-1],[0-1],[0-1]) to rgb floats ([0-1],[0-1],[0-1]), from Foley & van Dam p593
    // also http://en.wikipedia.org/wiki/HSL_and_HSV
    // C++: void ImGui::ColorConvertHSVtoRGB(float h, float s, float v, float& out_r, float& out_g, float& out_b)
    public static void ColorConvertHSVtoRGB(float h, float s, float v, out float out_r, out float out_g, out float out_b)
    {
        if (s == 0.0f)
        {
            // gray
            out_r = out_g = out_b = v;
            return;
        }

        h = ImMath.ImFmod(h, 1.0f) / (60.0f / 360.0f);
        int i = (int)h;
        float f = h - i;
        float p = v * (1.0f - s);
        float q = v * (1.0f - s * f);
        float t = v * (1.0f - s * (1.0f - f));

        switch (i)
        {
            case 0: out_r = v; out_g = t; out_b = p; break;
            case 1: out_r = q; out_g = v; out_b = p; break;
            case 2: out_r = p; out_g = v; out_b = t; break;
            case 3: out_r = p; out_g = q; out_b = v; break;
            case 4: out_r = t; out_g = p; out_b = v; break;
            case 5: default: out_r = v; out_g = p; out_b = q; break;
        }
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: ColorEdit, ColorPicker, ColorButton, etc.
    //-------------------------------------------------------------------------

    // C++: bool ImGui::ColorEdit3(const char* label, float col[3], ImGuiColorEditFlags flags)
    public static bool ColorEdit3(string label, float[] col, ImGuiColorEditFlags flags = 0)
    {
        return ColorEdit4(label, col, flags | ImGuiColorEditFlags_NoAlpha);
    }

    // C++: static void ColorEditRestoreH(const float* col, float* H)   [imgui_widgets.cpp]
    private static void ColorEditRestoreH(float[] col, ref float H)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.ColorEditCurrentID != 0);
        if (g.ColorEditSavedID != g.ColorEditCurrentID || g.ColorEditSavedColor != ColorConvertFloat4ToU32(new ImVec4(col[0], col[1], col[2], 0)))
            return;
        H = g.ColorEditSavedHue;
    }

    // ColorEdit supports RGB and HSV inputs. In case of RGB input resulting color may have undefined hue and/or saturation.
    // Since widget displays both RGB and HSV values we must preserve hue and saturation to prevent these values resetting.
    // C++: static void ColorEditRestoreHS(const float* col, float* H, float* S, float* V)
    private static void ColorEditRestoreHS(float[] col, ref float H, ref float S, ref float V)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.ColorEditCurrentID != 0);
        if (g.ColorEditSavedID != g.ColorEditCurrentID || g.ColorEditSavedColor != ColorConvertFloat4ToU32(new ImVec4(col[0], col[1], col[2], 0)))
            return;

        // When S == 0, H is undefined.
        // When H == 1 it wraps around to 0.
        if (S == 0.0f || (H == 0.0f && g.ColorEditSavedHue == 1))
            H = g.ColorEditSavedHue;

        // When V == 0, S is undefined.
        if (V == 0.0f)
            S = g.ColorEditSavedSat;
    }

    // Edit colors components (each component in 0.0f..1.0f range).
    // See enum ImGuiColorEditFlags_ for available options. e.g. Only access 3 floats if ImGuiColorEditFlags_NoAlpha flag is set.
    // With typical options: Left-click on color square to open color picker. Right-click to open option menu. Ctrl+Click over input fields to edit them and TAB to go to next item.
    // C++: bool ImGui::ColorEdit4(const char* label, float col[4], ImGuiColorEditFlags flags)
    public static bool ColorEdit4(string label, float[] col, ImGuiColorEditFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        float square_sz = GetFrameHeight();
        int label_display_end = FindRenderedTextEnd(label);
        float w_full = CalcItemWidth();
        g.NextItemData.ClearFlags();

        BeginGroup();
        PushID(label);
        bool set_current_color_edit_id = (g.ColorEditCurrentID == 0);
        if (set_current_color_edit_id)
            g.ColorEditCurrentID = window.IDStack.back();

        // If we're not showing any slider there's no point in doing any HSV conversions
        ImGuiColorEditFlags flags_untouched = flags;
        if ((flags & ImGuiColorEditFlags_NoInputs) != 0)
            flags = (flags & (~ImGuiColorEditFlags_DisplayMask_)) | ImGuiColorEditFlags_DisplayRGB | ImGuiColorEditFlags_NoOptions;

        // Context menu: display and modify options (before defaults are applied)
        if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
            ColorEditOptionsPopup(col, flags);

        // Read stored options
        if ((flags & ImGuiColorEditFlags_DisplayMask_) == 0)
            flags |= (g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_DisplayMask_);
        if ((flags & ImGuiColorEditFlags_DataTypeMask_) == 0)
            flags |= (g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_DataTypeMask_);
        if ((flags & ImGuiColorEditFlags_PickerMask_) == 0)
            flags |= (g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_PickerMask_);
        if ((flags & ImGuiColorEditFlags_InputMask_) == 0)
            flags |= (g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_InputMask_);
        flags |= (g.IO.ConfigColorEditFlags & ~(ImGuiColorEditFlags_DisplayMask_ | ImGuiColorEditFlags_DataTypeMask_ | ImGuiColorEditFlags_PickerMask_ | ImGuiColorEditFlags_InputMask_));
        Debug.Assert(ImMath.ImIsPowerOfTwo(flags & ImGuiColorEditFlags_DisplayMask_)); // Check that only 1 is selected
        Debug.Assert(ImMath.ImIsPowerOfTwo(flags & ImGuiColorEditFlags_InputMask_));   // Check that only 1 is selected

        bool alpha = (flags & ImGuiColorEditFlags_NoAlpha) == 0;
        bool hdr = (flags & ImGuiColorEditFlags_HDR) != 0;
        int components = alpha ? 4 : 3;
        float w_button = (flags & ImGuiColorEditFlags_NoSmallPreview) != 0 ? 0.0f : (square_sz + style.ItemInnerSpacing.x);
        float w_inputs = ImMath.ImMax(w_full - w_button, 1.0f);
        w_full = w_inputs + w_button;

        // Convert to the formats we need
        float[] f = { col[0], col[1], col[2], alpha ? col[3] : 1.0f };
        if ((flags & ImGuiColorEditFlags_InputHSV) != 0 && (flags & ImGuiColorEditFlags_DisplayRGB) != 0)
        {
            // C++: ColorConvertHSVtoRGB(f[0], f[1], f[2], f[0], f[1], f[2])
            ColorConvertHSVtoRGB(f[0], f[1], f[2], out float r, out float gg, out float b);
            f[0] = r; f[1] = gg; f[2] = b;
        }
        else if ((flags & ImGuiColorEditFlags_InputRGB) != 0 && (flags & ImGuiColorEditFlags_DisplayHSV) != 0)
        {
            // Hue is lost when converting from grayscale rgb (saturation=0). Restore it.
            // C++: ColorConvertRGBtoHSV(f[0], f[1], f[2], f[0], f[1], f[2])
            ColorConvertRGBtoHSV(f[0], f[1], f[2], out float h, out float s, out float v);
            f[0] = h; f[1] = s; f[2] = v;
            ColorEditRestoreHS(col, ref f[0], ref f[1], ref f[2]);
        }
        int[] i = { IM_F32_TO_INT8_UNBOUND(f[0]), IM_F32_TO_INT8_UNBOUND(f[1]), IM_F32_TO_INT8_UNBOUND(f[2]), IM_F32_TO_INT8_UNBOUND(f[3]) };

        bool value_changed = false;
        bool value_changed_as_float = false;

        ImVec2 pos = window.DC.CursorPos;
        float inputs_offset_x = (style.ColorButtonPosition == ImGuiDir_Left) ? w_button : 0.0f;
        window.DC.CursorPos.x = pos.x + inputs_offset_x;

        if ((flags & (ImGuiColorEditFlags_DisplayRGB | ImGuiColorEditFlags_DisplayHSV)) != 0 && (flags & ImGuiColorEditFlags_NoInputs) == 0)
        {
            // RGB/HSV 0..255 Sliders
            float w_items = w_inputs - style.ItemInnerSpacing.x * (components - 1);
            float w_per_component = ImMath.ImTrunc(w_items / components);
            bool draw_color_marker = (flags & (ImGuiColorEditFlags_DisplayHSV | ImGuiColorEditFlags_NoColorMarkers)) == 0;

            bool hide_prefix = draw_color_marker || (w_per_component <= CalcTextSize((flags & ImGuiColorEditFlags_Float) != 0 ? "M:0.000" : "M:000").x);
            string[] ids = { "##X", "##Y", "##Z", "##W" };
            string[][] fmt_table_int =
            {
                new[] {   "%3d",   "%3d",   "%3d",   "%3d" }, // Short display
                new[] { "R:%3d", "G:%3d", "B:%3d", "A:%3d" }, // Long display for RGBA
                new[] { "H:%3d", "S:%3d", "V:%3d", "A:%3d" }, // Long display for HSVA
            };
            string[][] fmt_table_float =
            {
                new[] {   "%0.3f",   "%0.3f",   "%0.3f",   "%0.3f" }, // Short display
                new[] { "R:%0.3f", "G:%0.3f", "B:%0.3f", "A:%0.3f" }, // Long display for RGBA
                new[] { "H:%0.3f", "S:%0.3f", "V:%0.3f", "A:%0.3f" }, // Long display for HSVA
            };
            int fmt_idx = hide_prefix ? 0 : (flags & ImGuiColorEditFlags_DisplayHSV) != 0 ? 2 : 1;
            ImGuiSliderFlags drag_flags = draw_color_marker ? ImGuiSliderFlags_ColorMarkers : ImGuiSliderFlags_None;

            float prev_split = 0.0f;
            for (int n = 0; n < components; n++)
            {
                if (n > 0)
                    SameLine(0, style.ItemInnerSpacing.x);
                float next_split = ImMath.ImTrunc(w_items * (n + 1) / components);
                SetNextItemWidth(ImMath.ImMax(next_split - prev_split, 1.0f));
                prev_split = next_split;
                if (draw_color_marker)
                    SetNextItemColorMarker(GDefaultRgbaColorMarkers[n]);

                // FIXME: When ImGuiColorEditFlags_HDR flag is passed HS values snap in weird ways when SV values go below 0.
                if ((flags & ImGuiColorEditFlags_Float) != 0)
                {
                    value_changed |= DragFloat(ids[n], ref f[n], 1.0f / 255.0f, 0.0f, hdr ? 0.0f : 1.0f, fmt_table_float[fmt_idx][n], drag_flags);
                    value_changed_as_float |= value_changed;
                }
                else
                {
                    value_changed |= DragInt(ids[n], ref i[n], 1.0f, 0, hdr ? 0 : 255, fmt_table_int[fmt_idx][n], drag_flags);
                }
                if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
                    OpenPopupOnItemClick("context", ImGuiPopupFlags_MouseButtonRight);
            }
        }
        else if ((flags & ImGuiColorEditFlags_DisplayHex) != 0 && (flags & ImGuiColorEditFlags_NoInputs) == 0)
        {
            // RGB Hexadecimal Input
            byte[] buf = new byte[64];
            string hex = alpha
                ? ImFmt.Sprintf("#%02X%02X%02X%02X", ImMath.ImClamp(i[0], 0, 255), ImMath.ImClamp(i[1], 0, 255), ImMath.ImClamp(i[2], 0, 255), ImMath.ImClamp(i[3], 0, 255))
                : ImFmt.Sprintf("#%02X%02X%02X", ImMath.ImClamp(i[0], 0, 255), ImMath.ImClamp(i[1], 0, 255), ImMath.ImClamp(i[2], 0, 255));
            // C++: ImFormatString(buf, IM_COUNTOF(buf), ...) -> copy into the NUL-terminated byte buffer edited by InputText().
            int hex_len = System.Text.Encoding.UTF8.GetBytes(hex, 0, hex.Length, buf, 0);
            buf[hex_len] = 0;
            SetNextItemWidth(w_inputs);
            if (InputText("##Text", buf, buf.Length, ImGuiInputTextFlags_CharsUppercase))
            {
                value_changed = true;
                // C++: char* p = buf; while (*p == '#' || ImCharIsBlankA(*p)) p++;
                int buf_len = 0;
                while (buf_len < buf.Length && buf[buf_len] != 0) buf_len++;
                string text = System.Text.Encoding.UTF8.GetString(buf, 0, buf_len);
                int p = 0;
                while (p < text.Length && (text[p] == '#' || ImText.ImCharIsBlankA(text[p])))
                    p++;
                i[0] = i[1] = i[2] = 0;
                i[3] = 0xFF; // alpha default to 255 is not parsed by scanf (e.g. inputting #FFFFFF omitting alpha)
                // C++: sscanf(p, "%02X%02X%02X[%02X]", ...) - Treat as unsigned (%X is unsigned)
                int r = ParseHexGroups(text, p, i, components);
                _ = r; // Fixes C6031: Return value ignored: 'sscanf'. (IM_UNUSED)
            }
            if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
                OpenPopupOnItemClick("context", ImGuiPopupFlags_MouseButtonRight);
        }

        ImGuiWindow picker_active_window = null;
        if ((flags & ImGuiColorEditFlags_NoSmallPreview) == 0)
        {
            float button_offset_x = ((flags & ImGuiColorEditFlags_NoInputs) != 0 || (style.ColorButtonPosition == ImGuiDir_Left)) ? 0.0f : w_inputs + style.ItemInnerSpacing.x;
            window.DC.CursorPos = new ImVec2(pos.x + button_offset_x, pos.y);

            ImVec4 col_v4 = new ImVec4(col[0], col[1], col[2], alpha ? col[3] : 1.0f);
            if (ColorButton("##ColorButton", col_v4, flags))
            {
                if ((flags & ImGuiColorEditFlags_NoPicker) == 0)
                {
                    // Store current color and open a picker
                    g.ColorPickerRef = col_v4;
                    OpenPopup("picker");
                    SetNextWindowPos(g.LastItemData.Rect.GetBL() + new ImVec2(0.0f, style.ItemSpacing.y));
                }
            }
            if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
                OpenPopupOnItemClick("context", ImGuiPopupFlags_MouseButtonRight);

            if (BeginPopup("picker"))
            {
                if (g.CurrentWindow.BeginCount == 1)
                {
                    picker_active_window = g.CurrentWindow;
                    if (label_display_end != 0) // C++: if (label != label_display_end)
                    {
                        TextEx(label, label_display_end);
                        Spacing();
                    }
                    ImGuiColorEditFlags picker_flags_to_forward = ImGuiColorEditFlags_DataTypeMask_ | ImGuiColorEditFlags_PickerMask_ | ImGuiColorEditFlags_InputMask_ | ImGuiColorEditFlags_HDR | ImGuiColorEditFlags_NoAlpha | ImGuiColorEditFlags_AlphaBar;
                    ImGuiColorEditFlags picker_flags = (flags_untouched & picker_flags_to_forward) | ImGuiColorEditFlags_DisplayMask_ | ImGuiColorEditFlags_NoLabel | ImGuiColorEditFlags_AlphaPreviewHalf;
                    SetNextItemWidth(square_sz * 12.0f); // Use 256 + bar sizes?
                    float[] ref_col_arr = { g.ColorPickerRef.x, g.ColorPickerRef.y, g.ColorPickerRef.z, g.ColorPickerRef.w }; // C++: &g.ColorPickerRef.x (read-only for the picker)
                    value_changed |= ColorPicker4("##picker", col, picker_flags, ref_col_arr);
                }
                EndPopup();
            }
        }

        if (label_display_end != 0 && (flags & ImGuiColorEditFlags_NoLabel) == 0) // C++: if (label != label_display_end && ...)
        {
            // Position not necessarily next to last submitted button (e.g. if style.ColorButtonPosition == ImGuiDir_Left),
            // but we need to use SameLine() to setup baseline correctly. Might want to refactor SameLine() to simplify this.
            SameLine(0.0f, style.ItemInnerSpacing.x);
            window.DC.CursorPos.x = pos.x + ((flags & ImGuiColorEditFlags_NoInputs) != 0 ? w_button : w_full + style.ItemInnerSpacing.x);
            TextEx(label, label_display_end);
        }

        // Convert back
        if (value_changed && picker_active_window == null)
        {
            if (!value_changed_as_float)
                for (int n = 0; n < 4; n++)
                    f[n] = i[n] / 255.0f;
            if ((flags & ImGuiColorEditFlags_DisplayHSV) != 0 && (flags & ImGuiColorEditFlags_InputRGB) != 0)
            {
                g.ColorEditSavedHue = f[0];
                g.ColorEditSavedSat = f[1];
                // C++: ColorConvertHSVtoRGB(f[0], f[1], f[2], f[0], f[1], f[2])
                ColorConvertHSVtoRGB(f[0], f[1], f[2], out float r, out float gg, out float b);
                f[0] = r; f[1] = gg; f[2] = b;
                g.ColorEditSavedID = g.ColorEditCurrentID;
                g.ColorEditSavedColor = ColorConvertFloat4ToU32(new ImVec4(f[0], f[1], f[2], 0));
            }
            if ((flags & ImGuiColorEditFlags_DisplayRGB) != 0 && (flags & ImGuiColorEditFlags_InputHSV) != 0)
            {
                // C++: ColorConvertRGBtoHSV(f[0], f[1], f[2], f[0], f[1], f[2])
                ColorConvertRGBtoHSV(f[0], f[1], f[2], out float h, out float s, out float v);
                f[0] = h; f[1] = s; f[2] = v;
            }

            col[0] = f[0];
            col[1] = f[1];
            col[2] = f[2];
            if (alpha)
                col[3] = f[3];
        }

        if (set_current_color_edit_id)
            g.ColorEditCurrentID = 0;
        PopID();
        EndGroup();

        // Drag and Drop Target
        // NB: The flag test is merely an optional micro-optimization, BeginDragDropTarget() does the same test.
        if ((g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HoveredRect) != 0 && (g.LastItemData.ItemFlags & ImGuiItemFlags_ReadOnly) == 0 && (flags & ImGuiColorEditFlags_NoDragDrop) == 0 && BeginDragDropTarget())
        {
            bool accepted_drag_drop = false;
            ImGuiPayload payload3 = AcceptDragDropPayload(IMGUI_PAYLOAD_TYPE_COLOR_3F);
            if (payload3 != null)
            {
                for (int m = 0; m < 3; m++) // memcpy((float*)col, payload->Data, sizeof(float) * 3); // Preserve alpha if any
                    col[m] = BitConverter.ToSingle(payload3.Data, m * 4);
                value_changed = accepted_drag_drop = true;
            }
            ImGuiPayload payload4 = AcceptDragDropPayload(IMGUI_PAYLOAD_TYPE_COLOR_4F);
            if (payload4 != null)
            {
                for (int m = 0; m < components; m++) // memcpy((float*)col, payload->Data, sizeof(float) * components);
                    col[m] = BitConverter.ToSingle(payload4.Data, m * 4);
                value_changed = accepted_drag_drop = true;
            }

            // Drag-drop payloads are always RGB
            if (accepted_drag_drop && (flags & ImGuiColorEditFlags_InputHSV) != 0)
            {
                // C++: ColorConvertRGBtoHSV(col[0], col[1], col[2], col[0], col[1], col[2])
                ColorConvertRGBtoHSV(col[0], col[1], col[2], out float h, out float s, out float v);
                col[0] = h; col[1] = s; col[2] = v;
            }
            EndDragDropTarget();
        }

        // When picker is being actively used, use its active id so IsItemActive() will function on ColorEdit4().
        if (picker_active_window != null && g.ActiveId != 0 && g.ActiveIdWindow == picker_active_window)
            g.LastItemData.ID = g.ActiveId;

        if (value_changed && g.LastItemData.ID != 0) // In case of ID collision, the second EndGroup() won't catch g.ActiveId
            MarkItemEdited(g.LastItemData.ID);

        return value_changed;
    }

    // C++: sscanf(p, "%02X%02X%02X[%02X]", ...) helper. Parses up to 'count' hex fields, each field reads
    // at most 2 hex digits (scanf width), skipping whitespace between fields. Only assigned fields are stored.
    private static int ParseHexGroups(string s, int start, int[] out_vals, int count)
    {
        int parsed = 0;
        int pos = start;
        while (parsed < count)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t')) // scanf skips leading whitespace
                pos++;
            int v = 0, digits = 0;
            while (pos < s.Length && digits < 2)
            {
                char c = s[pos];
                int d;
                if (c >= '0' && c <= '9') d = c - '0';
                else if (c >= 'a' && c <= 'f') d = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') d = c - 'A' + 10;
                else break;
                v = v * 16 + d;
                pos++;
                digits++;
            }
            if (digits == 0)
                break;
            out_vals[parsed++] = v;
        }
        return parsed;
    }

    // C++: bool ImGui::ColorPicker3(const char* label, float col[3], ImGuiColorEditFlags flags = 0)
    public static bool ColorPicker3(string label, float[] col, ImGuiColorEditFlags flags = 0)
    {
        float[] col4 = { col[0], col[1], col[2], 1.0f };
        if (!ColorPicker4(label, col4, flags | ImGuiColorEditFlags_NoAlpha))
            return false;
        col[0] = col4[0]; col[1] = col4[1]; col[2] = col4[2];
        return true;
    }

    // C++: static void RenderArrowsForVerticalBar(ImDrawList* draw_list, ImVec2 pos, ImVec2 half_sz, float bar_w, float alpha)
    private static void RenderArrowsForVerticalBar(ImDrawList draw_list, ImVec2 pos, ImVec2 half_sz, float bar_w, float alpha)
    {
        ImU32 alpha8 = (ImU32)IM_F32_TO_INT8_SAT(alpha);
        RenderArrowPointingAt(draw_list, new ImVec2(pos.x + half_sz.x + 1, pos.y), new ImVec2(half_sz.x + 2, half_sz.y + 1), ImGuiDir_Right, ImGuiCore.IM_COL32(0, 0, 0, (int)alpha8));
        RenderArrowPointingAt(draw_list, new ImVec2(pos.x + half_sz.x, pos.y), half_sz, ImGuiDir_Right, ImGuiCore.IM_COL32(255, 255, 255, (int)alpha8));
        RenderArrowPointingAt(draw_list, new ImVec2(pos.x + bar_w - half_sz.x - 1, pos.y), new ImVec2(half_sz.x + 2, half_sz.y + 1), ImGuiDir_Left, ImGuiCore.IM_COL32(0, 0, 0, (int)alpha8));
        RenderArrowPointingAt(draw_list, new ImVec2(pos.x + bar_w - half_sz.x, pos.y), half_sz, ImGuiDir_Left, ImGuiCore.IM_COL32(255, 255, 255, (int)alpha8));
    }

    // Note: ColorPicker4() only accesses 3 floats if ImGuiColorEditFlags_NoAlpha flag is set.
    // (In C++ the 'float col[4]' notation for a function argument is equivalent to 'float* col', we only specify a size to facilitate understanding of the code.)
    // FIXME: we adjust the big color square height based on item width, which may cause a flickering feedback loop (if automatic height makes a vertical scrollbar appears, affecting automatic width..)
    // FIXME: this is trying to be aware of style.Alpha but not fully correct. Also, the color wheel will have overlapping glitches with (style.Alpha < 1.0)
    // C++: bool ImGui::ColorPicker4(const char* label, float col[4], ImGuiColorEditFlags flags = 0, const float* ref_col = NULL)
    public static bool ColorPicker4(string label, float[] col, ImGuiColorEditFlags flags = 0, float[] ref_col = null)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImDrawList draw_list = window.DrawList;
        ImGuiStyle style = g.Style;
        ImGuiIO io = g.IO;

        float width = CalcItemWidth();
        bool is_readonly = ((g.NextItemData.ItemFlagsSet | g.CurrentItemFlags) & ImGuiItemFlags_ReadOnly) != 0;
        g.NextItemData.ClearFlags();

        float backup_alpha = g.Style.Alpha;
        if (g.DisabledStackSize > 0)
            g.Style.Alpha = g.DisabledAlphaBackup; // Cancel out effect of BeginDisabled() for color swatches.

        PushID(label);
        bool set_current_color_edit_id = (g.ColorEditCurrentID == 0);
        if (set_current_color_edit_id)
            g.ColorEditCurrentID = window.IDStack.back();
        BeginGroup();

        if ((flags & ImGuiColorEditFlags_NoSidePreview) == 0)
            flags |= ImGuiColorEditFlags_NoSmallPreview;

        // Context menu: display and store options.
        if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
            ColorPickerOptionsPopup(col, flags);

        // Read stored options
        if ((flags & ImGuiColorEditFlags_PickerMask_) == 0)
            flags |= ((g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_PickerMask_) != 0 ? g.IO.ConfigColorEditFlags : ImGuiColorEditFlags_DefaultOptions_) & ImGuiColorEditFlags_PickerMask_;
        if ((flags & ImGuiColorEditFlags_InputMask_) == 0)
            flags |= ((g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_InputMask_) != 0 ? g.IO.ConfigColorEditFlags : ImGuiColorEditFlags_DefaultOptions_) & ImGuiColorEditFlags_InputMask_;
        Debug.Assert(ImMath.ImIsPowerOfTwo(flags & ImGuiColorEditFlags_PickerMask_)); // Check that only 1 is selected
        Debug.Assert(ImMath.ImIsPowerOfTwo(flags & ImGuiColorEditFlags_InputMask_));  // Check that only 1 is selected
        if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
            flags |= (g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_AlphaBar);

        // Setup
        int components = (flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 3 : 4;
        bool alpha_bar = (flags & ImGuiColorEditFlags_AlphaBar) != 0 && (flags & ImGuiColorEditFlags_NoAlpha) == 0;
        ImVec2 picker_pos = window.DC.CursorPos;
        float square_sz = GetFrameHeight();
        float bars_width = square_sz; // Arbitrary smallish width of Hue/Alpha picking bars
        float sv_picker_size = ImMath.ImMax(bars_width * 1, width - (alpha_bar ? 2 : 1) * (bars_width + style.ItemInnerSpacing.x)); // Saturation/Value picking box
        float bar0_pos_x = picker_pos.x + sv_picker_size + style.ItemInnerSpacing.x;
        float bar1_pos_x = bar0_pos_x + bars_width + style.ItemInnerSpacing.x;
        float bars_triangles_half_sz = ImMath.ImTrunc(bars_width * 0.20f);

        float[] backup_initial_col = new float[4];
        Array.Copy(col, backup_initial_col, components);

        float wheel_thickness = sv_picker_size * 0.08f;
        float wheel_r_outer = sv_picker_size * 0.50f;
        float wheel_r_inner = wheel_r_outer - wheel_thickness;
        ImVec2 wheel_center = new ImVec2(picker_pos.x + (sv_picker_size + bars_width) * 0.5f, picker_pos.y + sv_picker_size * 0.5f);

        // Note: the triangle is displayed rotated with triangle_pa pointing to Hue, but most coordinates stays unrotated for logic.
        bool triangle_rotate = (flags & ImGuiColorEditFlags_PickerNoRotate) == 0;
        float triangle_r = wheel_r_inner - (int)(sv_picker_size * 0.027f);
        ImVec2 triangle_pa = triangle_rotate ? new ImVec2(triangle_r, 0.0f) : new ImVec2(triangle_r * +0.866f, triangle_r * +0.5f); // Hue point.
        ImVec2 triangle_pb = triangle_rotate ? new ImVec2(triangle_r * -0.5f, triangle_r * -0.866f) : new ImVec2(0.0f, -triangle_r); // Black point.
        ImVec2 triangle_pc = triangle_rotate ? new ImVec2(triangle_r * -0.5f, triangle_r * +0.866f) : new ImVec2(triangle_r * -0.866f, triangle_r * +0.5f); // White point.

        float H = col[0], S = col[1], V = col[2];
        float R = col[0], G = col[1], B = col[2];
        if ((flags & ImGuiColorEditFlags_InputRGB) != 0)
        {
            // Hue is lost when converting from grayscale rgb (saturation=0). Restore it.
            ColorConvertRGBtoHSV(R, G, B, out float h, out float s, out float v);
            H = h; S = s; V = v;
            ColorEditRestoreHS(col, ref H, ref S, ref V);
        }
        else if ((flags & ImGuiColorEditFlags_InputHSV) != 0)
        {
            ColorConvertHSVtoRGB(H, S, V, out float r, out float gg, out float b);
            R = r; G = gg; B = b;
        }

        bool value_changed = false, value_changed_h = false, value_changed_sv = false;

        PushItemFlag(ImGuiItemFlags_NoNav, true);
        if ((flags & ImGuiColorEditFlags_PickerHueWheel) != 0)
        {
            // Hue wheel + SV triangle logic
            InvisibleButton("hsv", new ImVec2(sv_picker_size + style.ItemInnerSpacing.x + bars_width, sv_picker_size));
            if (IsItemActive() && !is_readonly)
            {
                ImVec2 initial_off = g.IO.MouseClickedPos[0] - wheel_center;
                ImVec2 current_off = g.IO.MousePos - wheel_center;
                float initial_dist2 = ImUtil.ImLengthSqr(initial_off);
                if (initial_dist2 >= (wheel_r_inner - 1) * (wheel_r_inner - 1) && initial_dist2 <= (wheel_r_outer + 1) * (wheel_r_outer + 1))
                {
                    // Interactive with Hue wheel
                    H = MathF.Atan2(current_off.y, current_off.x) / ImMath.IM_PI * 0.5f; // ImAtan2(current_off.y, current_off.x)
                    if (H < 0.0f)
                        H += 1.0f;
                    value_changed = value_changed_h = true;
                }
                float cos_hue_angle = triangle_rotate ? ImMath.ImCos(-H * 2.0f * ImMath.IM_PI) : 1.0f;
                float sin_hue_angle = triangle_rotate ? ImMath.ImSin(-H * 2.0f * ImMath.IM_PI) : 0.0f;
                if (ImGuiCore.ImTriangleContainsPoint(triangle_pa, triangle_pb, triangle_pc, ImMath.ImRotate(initial_off, cos_hue_angle, sin_hue_angle)))
                {
                    // Interacting with SV triangle
                    ImVec2 current_off_unrotated = ImMath.ImRotate(current_off, cos_hue_angle, sin_hue_angle);
                    if (!ImGuiCore.ImTriangleContainsPoint(triangle_pa, triangle_pb, triangle_pc, current_off_unrotated))
                        current_off_unrotated = ImGuiCore.ImTriangleClosestPoint(triangle_pa, triangle_pb, triangle_pc, current_off_unrotated);
                    ImGuiCore.ImTriangleBarycentricCoords(triangle_pa, triangle_pb, triangle_pc, current_off_unrotated, out float uu, out float vv, out float ww);
                    V = ImMath.ImClamp(1.0f - vv, 0.0001f, 1.0f);
                    S = ImMath.ImClamp(uu / V, 0.0001f, 1.0f);
                    value_changed = value_changed_sv = true;
                }
            }
            if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
                OpenPopupOnItemClick("context", ImGuiPopupFlags_MouseButtonRight);
        }
        else if ((flags & ImGuiColorEditFlags_PickerHueBar) != 0)
        {
            // SV rectangle logic
            InvisibleButton("sv", new ImVec2(sv_picker_size, sv_picker_size));
            if (IsItemActive() && !is_readonly)
            {
                S = ImMath.ImSaturate((io.MousePos.x - picker_pos.x) / (sv_picker_size - 1));
                V = 1.0f - ImMath.ImSaturate((io.MousePos.y - picker_pos.y) / (sv_picker_size - 1));
                ColorEditRestoreH(col, ref H); // Greatly reduces hue jitter and reset to 0 when hue == 255 and color is rapidly modified using SV square.
                value_changed = value_changed_sv = true;
            }
            if ((flags & ImGuiColorEditFlags_NoOptions) == 0)
                OpenPopupOnItemClick("context", ImGuiPopupFlags_MouseButtonRight);

            // Hue bar logic
            SetCursorScreenPos(new ImVec2(bar0_pos_x, picker_pos.y));
            InvisibleButton("hue", new ImVec2(bars_width, sv_picker_size));
            if (IsItemActive() && !is_readonly)
            {
                H = ImMath.ImSaturate((io.MousePos.y - picker_pos.y) / (sv_picker_size - 1));
                value_changed = value_changed_h = true;
            }
        }

        // Alpha bar logic
        if (alpha_bar)
        {
            SetCursorScreenPos(new ImVec2(bar1_pos_x, picker_pos.y));
            InvisibleButton("alpha", new ImVec2(bars_width, sv_picker_size));
            if (IsItemActive())
            {
                col[3] = 1.0f - ImMath.ImSaturate((io.MousePos.y - picker_pos.y) / (sv_picker_size - 1));
                value_changed = true;
            }
        }
        PopItemFlag(); // ImGuiItemFlags_NoNav

        if ((flags & ImGuiColorEditFlags_NoSidePreview) == 0)
        {
            SameLine(0, style.ItemInnerSpacing.x);
            BeginGroup();
        }

        if ((flags & ImGuiColorEditFlags_NoLabel) == 0)
        {
            int label_display_end = FindRenderedTextEnd(label);
            if (label_display_end != 0) // C++: if (label != label_display_end)
            {
                if ((flags & ImGuiColorEditFlags_NoSidePreview) != 0)
                    SameLine(0, style.ItemInnerSpacing.x);
                TextEx(label, label_display_end);
            }
        }

        if ((flags & ImGuiColorEditFlags_NoSidePreview) == 0)
        {
            PushItemFlag(ImGuiItemFlags_NoNavDefaultFocus, true);
            ImVec4 col_v4 = new ImVec4(col[0], col[1], col[2], (flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 1.0f : col[3]);
            if ((flags & ImGuiColorEditFlags_NoLabel) != 0)
                Text("Current");

            ImGuiColorEditFlags sub_flags_to_forward = ImGuiColorEditFlags_InputMask_ | ImGuiColorEditFlags_HDR | ImGuiColorEditFlags_AlphaMask_ | ImGuiColorEditFlags_NoTooltip;
            ColorButton("##current", col_v4, (flags & sub_flags_to_forward), new ImVec2(square_sz * 3, square_sz * 2));
            if (ref_col != null)
            {
                Text("Original");
                ImVec4 ref_col_v4 = new ImVec4(ref_col[0], ref_col[1], ref_col[2], (flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 1.0f : ref_col[3]);
                if (ColorButton("##original", ref_col_v4, (flags & sub_flags_to_forward), new ImVec2(square_sz * 3, square_sz * 2)))
                {
                    Array.Copy(ref_col, col, components); // memcpy(col, ref_col, components * sizeof(float))
                    value_changed = true;
                }
            }
            PopItemFlag();
            EndGroup();
        }

        // Convert back color to RGB
        if (value_changed_h || value_changed_sv)
        {
            if ((flags & ImGuiColorEditFlags_InputRGB) != 0)
            {
                ColorConvertHSVtoRGB(H, S, V, out float r, out float gg, out float b);
                col[0] = r; col[1] = gg; col[2] = b;
                g.ColorEditSavedHue = H;
                g.ColorEditSavedSat = S;
                g.ColorEditSavedID = g.ColorEditCurrentID;
                g.ColorEditSavedColor = ColorConvertFloat4ToU32(new ImVec4(col[0], col[1], col[2], 0));
            }
            else if ((flags & ImGuiColorEditFlags_InputHSV) != 0)
            {
                col[0] = H;
                col[1] = S;
                col[2] = V;
            }
        }

        // R,G,B and H,S,V slider color editor
        bool value_changed_fix_hue_wrap = false;
        if ((flags & ImGuiColorEditFlags_NoInputs) == 0)
        {
            PushItemWidth((alpha_bar ? bar1_pos_x : bar0_pos_x) + bars_width - picker_pos.x);
            ImGuiColorEditFlags sub_flags_to_forward = ImGuiColorEditFlags_DataTypeMask_ | ImGuiColorEditFlags_InputMask_ | ImGuiColorEditFlags_HDR | ImGuiColorEditFlags_AlphaMask_ | ImGuiColorEditFlags_NoOptions | ImGuiColorEditFlags_NoTooltip | ImGuiColorEditFlags_NoSmallPreview;
            ImGuiColorEditFlags sub_flags = (flags & sub_flags_to_forward) | ImGuiColorEditFlags_NoPicker;
            if ((flags & ImGuiColorEditFlags_DisplayRGB) != 0 || (flags & ImGuiColorEditFlags_DisplayMask_) == 0)
                if (ColorEdit4("##rgb", col, sub_flags | ImGuiColorEditFlags_DisplayRGB))
                {
                    // FIXME: Hackily differentiating using the DragInt (ActiveId != 0 && !ActiveIdAllowOverlap) vs. using the InputText or DropTarget.
                    // For the later we don't want to run the hue-wrap canceling code. If you are well versed in HSV picker please provide your input! (See #2050)
                    value_changed_fix_hue_wrap = (g.ActiveId != 0 && !g.ActiveIdAllowOverlap);
                    value_changed = true;
                }
            if ((flags & ImGuiColorEditFlags_DisplayHSV) != 0 || (flags & ImGuiColorEditFlags_DisplayMask_) == 0)
                value_changed |= ColorEdit4("##hsv", col, sub_flags | ImGuiColorEditFlags_DisplayHSV);
            if ((flags & ImGuiColorEditFlags_DisplayHex) != 0 || (flags & ImGuiColorEditFlags_DisplayMask_) == 0)
                value_changed |= ColorEdit4("##hex", col, sub_flags | ImGuiColorEditFlags_DisplayHex);
            PopItemWidth();
        }

        // Try to cancel hue wrap (after ColorEdit4 call), if any
        if (value_changed_fix_hue_wrap && (flags & ImGuiColorEditFlags_InputRGB) != 0)
        {
            ColorConvertRGBtoHSV(col[0], col[1], col[2], out float new_H, out float new_S, out float new_V);
            if (new_H <= 0 && H > 0)
            {
                if (new_V <= 0 && V != new_V)
                {
                    // C++: ColorConvertHSVtoRGB(H, S, new_V <= 0 ? V * 0.5f : new_V, col[0], col[1], col[2])
                    ColorConvertHSVtoRGB(H, S, new_V <= 0 ? V * 0.5f : new_V, out float r, out float gg, out float b);
                    col[0] = r; col[1] = gg; col[2] = b;
                }
                else if (new_S <= 0)
                {
                    // C++: ColorConvertHSVtoRGB(H, new_S <= 0 ? S * 0.5f : new_S, new_V, col[0], col[1], col[2])
                    ColorConvertHSVtoRGB(H, new_S <= 0 ? S * 0.5f : new_S, new_V, out float r, out float gg, out float b);
                    col[0] = r; col[1] = gg; col[2] = b;
                }
            }
        }

        if (value_changed)
        {
            if ((flags & ImGuiColorEditFlags_InputRGB) != 0)
            {
                R = col[0];
                G = col[1];
                B = col[2];
                ColorConvertRGBtoHSV(R, G, B, out float h, out float s, out float v);
                H = h; S = s; V = v;
                ColorEditRestoreHS(col, ref H, ref S, ref V); // Fix local Hue as display below will use it immediately.
            }
            else if ((flags & ImGuiColorEditFlags_InputHSV) != 0)
            {
                H = col[0];
                S = col[1];
                V = col[2];
                ColorConvertHSVtoRGB(H, S, V, out float r, out float gg, out float b);
                R = r; G = gg; B = b;
            }
        }

        int style_alpha8 = IM_F32_TO_INT8_SAT(style.Alpha);
        ImU32 col_black = ImGuiCore.IM_COL32(0, 0, 0, style_alpha8);
        ImU32 col_white = ImGuiCore.IM_COL32(255, 255, 255, style_alpha8);
        ImU32 col_midgrey = ImGuiCore.IM_COL32(128, 128, 128, style_alpha8);
        ImU32[] col_hues = new ImU32[6 + 1]
        {
            ImGuiCore.IM_COL32(255, 0, 0, style_alpha8), ImGuiCore.IM_COL32(255, 255, 0, style_alpha8), ImGuiCore.IM_COL32(0, 255, 0, style_alpha8),
            ImGuiCore.IM_COL32(0, 255, 255, style_alpha8), ImGuiCore.IM_COL32(0, 0, 255, style_alpha8), ImGuiCore.IM_COL32(255, 0, 255, style_alpha8),
            ImGuiCore.IM_COL32(255, 0, 0, style_alpha8),
        };

        ImVec4 hue_color_f = new ImVec4(1, 1, 1, style.Alpha);
        {
            // C++: ColorConvertHSVtoRGB(H, 1, 1, hue_color_f.x, hue_color_f.y, hue_color_f.z)
            ColorConvertHSVtoRGB(H, 1, 1, out float r, out float gg, out float b);
            hue_color_f.x = r; hue_color_f.y = gg; hue_color_f.z = b;
        }
        ImU32 hue_color32 = ColorConvertFloat4ToU32(hue_color_f);
        ImU32 user_col32_striped_of_alpha = ColorConvertFloat4ToU32(new ImVec4(R, G, B, style.Alpha)); // Important: this is still including the main rendering/style alpha!!

        ImVec2 sv_cursor_pos = default;

        if ((flags & ImGuiColorEditFlags_PickerHueWheel) != 0)
        {
            // Render Hue Wheel
            float aeps = 0.5f / wheel_r_outer; // Half a pixel arc length in radians (2pi cancels out).
            int segment_per_arc = ImMath.ImMax(4, (int)wheel_r_outer / 12);
            for (int n = 0; n < 6; n++)
            {
                float a0 = (n) / 6.0f * 2.0f * ImMath.IM_PI - aeps;
                float a1 = (n + 1.0f) / 6.0f * 2.0f * ImMath.IM_PI + aeps;
                int vert_start_idx = draw_list.VtxBuffer.Size;
                draw_list.PathArcTo(wheel_center, (wheel_r_inner + wheel_r_outer) * 0.5f, a0, a1, segment_per_arc);
                draw_list.PathStroke(col_white, wheel_thickness);
                int vert_end_idx = draw_list.VtxBuffer.Size;

                // Paint colors over existing vertices
                ImVec2 gradient_p0 = new ImVec2(wheel_center.x + ImMath.ImCos(a0) * wheel_r_inner, wheel_center.y + ImMath.ImSin(a0) * wheel_r_inner);
                ImVec2 gradient_p1 = new ImVec2(wheel_center.x + ImMath.ImCos(a1) * wheel_r_inner, wheel_center.y + ImMath.ImSin(a1) * wheel_r_inner);
                ShadeVertsLinearColorGradientKeepAlpha(draw_list, vert_start_idx, vert_end_idx, gradient_p0, gradient_p1, col_hues[n], col_hues[n + 1]);
            }

            // Render Cursor + preview on Hue Wheel
            float cos_hue_angle = ImMath.ImCos(H * 2.0f * ImMath.IM_PI);
            float sin_hue_angle = ImMath.ImSin(H * 2.0f * ImMath.IM_PI);
            ImVec2 hue_cursor_pos = new ImVec2(wheel_center.x + cos_hue_angle * (wheel_r_inner + wheel_r_outer) * 0.5f, wheel_center.y + sin_hue_angle * (wheel_r_inner + wheel_r_outer) * 0.5f);
            float hue_cursor_rad = value_changed_h ? wheel_thickness * 0.65f : wheel_thickness * 0.55f;
            int hue_cursor_segments = draw_list._CalcCircleAutoSegmentCount(hue_cursor_rad); // Lock segment count so the +1 one matches others.
            draw_list.AddCircleFilled(hue_cursor_pos, hue_cursor_rad, hue_color32, hue_cursor_segments);
            draw_list.AddCircle(hue_cursor_pos, hue_cursor_rad + 1, col_midgrey, hue_cursor_segments);
            draw_list.AddCircle(hue_cursor_pos, hue_cursor_rad, col_white, hue_cursor_segments);
            if (triangle_rotate == false)
            {
                cos_hue_angle = 1.0f;
                sin_hue_angle = 0.0f;
            }

            // Render SV triangle (rotated according to hue)
            ImVec2 tra = wheel_center + ImMath.ImRotate(triangle_pa, cos_hue_angle, sin_hue_angle);
            ImVec2 trb = wheel_center + ImMath.ImRotate(triangle_pb, cos_hue_angle, sin_hue_angle);
            ImVec2 trc = wheel_center + ImMath.ImRotate(triangle_pc, cos_hue_angle, sin_hue_angle);
            ImVec2 uv_white = GetFontTexUvWhitePixel();
            draw_list.PrimReserve(3, 3);
            draw_list.PrimVtx(tra, uv_white, hue_color32);
            draw_list.PrimVtx(trb, uv_white, col_black);
            draw_list.PrimVtx(trc, uv_white, col_white);
            draw_list.AddTriangle(tra, trb, trc, col_midgrey, 1.5f);
            sv_cursor_pos = ImMath.ImLerp(ImMath.ImLerp(trc, tra, ImMath.ImSaturate(S)), trb, ImMath.ImSaturate(1 - V));
        }
        else if ((flags & ImGuiColorEditFlags_PickerHueBar) != 0)
        {
            // Render SV Square
            draw_list.AddRectFilledMultiColor(picker_pos, picker_pos + new ImVec2(sv_picker_size, sv_picker_size), col_white, hue_color32, hue_color32, col_white);
            draw_list.AddRectFilledMultiColor(picker_pos, picker_pos + new ImVec2(sv_picker_size, sv_picker_size), 0, 0, col_black, col_black);
            RenderFrameBorder(picker_pos, picker_pos + new ImVec2(sv_picker_size, sv_picker_size), 0.0f);
            sv_cursor_pos.x = ImMath.ImClamp(ImMath.ImRound(picker_pos.x + ImMath.ImSaturate(S) * sv_picker_size), picker_pos.x + 2, picker_pos.x + sv_picker_size - 2); // Sneakily prevent the circle to stick out too much
            sv_cursor_pos.y = ImMath.ImClamp(ImMath.ImRound(picker_pos.y + ImMath.ImSaturate(1 - V) * sv_picker_size), picker_pos.y + 2, picker_pos.y + sv_picker_size - 2);

            // Render Hue Bar
            for (int ii = 0; ii < 6; ++ii)
                draw_list.AddRectFilledMultiColor(new ImVec2(bar0_pos_x, picker_pos.y + ii * (sv_picker_size / 6)), new ImVec2(bar0_pos_x + bars_width, picker_pos.y + (ii + 1) * (sv_picker_size / 6)), col_hues[ii], col_hues[ii], col_hues[ii + 1], col_hues[ii + 1]);
            float bar0_line_y = ImMath.ImRound(picker_pos.y + H * sv_picker_size);
            RenderFrameBorder(new ImVec2(bar0_pos_x, picker_pos.y), new ImVec2(bar0_pos_x + bars_width, picker_pos.y + sv_picker_size), 0.0f);
            RenderArrowsForVerticalBar(draw_list, new ImVec2(bar0_pos_x - 1, bar0_line_y), new ImVec2(bars_triangles_half_sz + 1, bars_triangles_half_sz), bars_width + 2.0f, style.Alpha);
        }

        // Render cursor/preview circle (clamp S/V within 0..1 range because floating points colors may lead HSV values to be out of range)
        float sv_cursor_rad = value_changed_sv ? wheel_thickness * 0.55f : wheel_thickness * 0.40f;
        int sv_cursor_segments = draw_list._CalcCircleAutoSegmentCount(sv_cursor_rad); // Lock segment count so the +1 one matches others.
        draw_list.AddCircleFilled(sv_cursor_pos, sv_cursor_rad, user_col32_striped_of_alpha, sv_cursor_segments);
        draw_list.AddCircle(sv_cursor_pos, sv_cursor_rad + 1, col_midgrey, sv_cursor_segments);
        draw_list.AddCircle(sv_cursor_pos, sv_cursor_rad, col_white, sv_cursor_segments);

        // Render alpha bar
        if (alpha_bar)
        {
            float alpha = ImMath.ImSaturate(col[3]);
            ImRect bar1_bb = new ImRect(bar1_pos_x, picker_pos.y, bar1_pos_x + bars_width, picker_pos.y + sv_picker_size);
            RenderColorRectWithAlphaCheckerboard(draw_list, bar1_bb.Min, bar1_bb.Max, ImGuiCore.IM_COL32(0, 0, 0, 0), style.Alpha, bar1_bb.GetWidth() / 2.0f, new ImVec2(0.0f, 0.0f));
            draw_list.AddRectFilledMultiColor(bar1_bb.Min, bar1_bb.Max, user_col32_striped_of_alpha, user_col32_striped_of_alpha, user_col32_striped_of_alpha & ~ImGuiCore.IM_COL32_A_MASK, user_col32_striped_of_alpha & ~ImGuiCore.IM_COL32_A_MASK);
            float bar1_line_y = ImMath.ImRound(picker_pos.y + (1.0f - alpha) * sv_picker_size);
            RenderFrameBorder(bar1_bb.Min, bar1_bb.Max, 0.0f);
            RenderArrowsForVerticalBar(draw_list, new ImVec2(bar1_pos_x - 1, bar1_line_y), new ImVec2(bars_triangles_half_sz + 1, bars_triangles_half_sz), bars_width + 2.0f, style.Alpha);
        }

        EndGroup();

        if (value_changed && CompareFloatSpan(backup_initial_col, col, components))
            value_changed = false;
        if (value_changed && g.LastItemData.ID != 0) // In case of ID collision, the second EndGroup() won't catch g.ActiveId
            MarkItemEdited(g.LastItemData.ID);

        if (set_current_color_edit_id)
            g.ColorEditCurrentID = 0;
        PopID();

        if (g.DisabledStackSize > 0)
            g.Style.Alpha = backup_alpha;

        return value_changed;
    }

    // memcmp(backup_initial_col, col, components * sizeof(float)) == 0 helper (bitwise-equality intent)
    private static bool CompareFloatSpan(float[] a, float[] b, int count)
    {
        for (int n = 0; n < count; n++)
            if (a[n] != b[n])
                return false;
        return true;
    }

    // A little color square. Return true when clicked.
    // FIXME: May want to display/ignore the alpha component in the color display? Yet show it in the tooltip.
    // 'desc_id' is not called 'label' because we don't display it next to the button, but only in the tooltip.
    // Note that 'col' may be encoded in HSV if ImGuiColorEditFlags_InputHSV is set.
    // C++: bool ImGui::ColorButton(const char* desc_id, const ImVec4& col, ImGuiColorEditFlags flags = 0, const ImVec2& size = ImVec2(0, 0))
    public static bool ColorButton(string desc_id, ImVec4 col, ImGuiColorEditFlags flags = 0, ImVec2? size_arg = null)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImVec2 size_arg_v = size_arg ?? new ImVec2(0.0f, 0.0f);
        ImGuiID id = window.GetID(desc_id);
        float default_size = GetFrameHeight();
        ImVec2 size = new ImVec2(size_arg_v.x == 0.0f ? default_size : size_arg_v.x, size_arg_v.y == 0.0f ? default_size : size_arg_v.y);
        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + size);
        ItemSize(bb.GetSize(), (size.y >= default_size) ? g.Style.FramePadding.y : 0.0f);
        if (!ItemAdd(bb, id))
            return false;

        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held);

        if ((flags & (ImGuiColorEditFlags_NoAlpha | ImGuiColorEditFlags_AlphaOpaque)) != 0)
            flags &= ~(ImGuiColorEditFlags_AlphaNoBg | ImGuiColorEditFlags_AlphaPreviewHalf);

        ImVec4 col_rgb = col;
        if ((flags & ImGuiColorEditFlags_InputHSV) != 0)
        {
            // C++: ColorConvertHSVtoRGB(col_rgb.x, col_rgb.y, col_rgb.z, col_rgb.x, col_rgb.y, col_rgb.z)
            ColorConvertHSVtoRGB(col_rgb.x, col_rgb.y, col_rgb.z, out float r, out float gg, out float b);
            col_rgb.x = r; col_rgb.y = gg; col_rgb.z = b;
        }

        ImVec4 col_rgb_without_alpha = new ImVec4(col_rgb.x, col_rgb.y, col_rgb.z, 1.0f);
        float grid_step = ImMath.ImMin(size.x, size.y) / 2.99f;
        float rounding = ImMath.ImMin(g.Style.FrameRounding, grid_step * 0.5f);

        float backup_alpha = g.Style.Alpha;
        if (g.DisabledStackSize > 0)
            g.Style.Alpha = g.DisabledAlphaBackup; // Cancel out effect of BeginDisabled() for color swatches.
        if ((flags & ImGuiColorEditFlags_AlphaPreviewHalf) != 0 && col_rgb.w < 1.0f)
        {
            float mid_x = ImMath.ImRound((bb.Min.x + bb.Max.x) * 0.5f);
            if ((flags & ImGuiColorEditFlags_AlphaNoBg) == 0)
                RenderColorRectWithAlphaCheckerboard(window.DrawList, new ImVec2(bb.Min.x + grid_step, bb.Min.y), bb.Max, ColorConvertFloat4ToU32(col_rgb), g.Style.Alpha, grid_step, new ImVec2(-grid_step, 0.0f), rounding, ImDrawFlags_RoundCornersRight);
            else
                window.DrawList.AddRectFilled(new ImVec2(bb.Min.x + grid_step, bb.Min.y), bb.Max, GetColorU32(col_rgb), rounding, ImDrawFlags_RoundCornersRight);
            window.DrawList.AddRectFilled(bb.Min, new ImVec2(mid_x, bb.Max.y), GetColorU32(col_rgb_without_alpha), rounding, ImDrawFlags_RoundCornersLeft);
        }
        else
        {
            // Because GetColorU32() multiplies by the global style Alpha and we don't want to display a checkerboard if the source code had no alpha
            ImVec4 col_source = (flags & ImGuiColorEditFlags_AlphaOpaque) != 0 ? col_rgb_without_alpha : col_rgb;
            if (col_source.w < 1.0f && (flags & ImGuiColorEditFlags_AlphaNoBg) == 0)
                RenderColorRectWithAlphaCheckerboard(window.DrawList, bb.Min, bb.Max, ColorConvertFloat4ToU32(col_source), g.Style.Alpha, grid_step, new ImVec2(0.0f, 0.0f), rounding);
            else
                window.DrawList.AddRectFilled(bb.Min, bb.Max, GetColorU32(col_source), rounding);
        }
        if (g.DisabledStackSize > 0)
            g.Style.Alpha = backup_alpha;
        RenderNavCursor(bb, id);
        if ((flags & ImGuiColorEditFlags_NoBorder) == 0)
        {
            if (g.Style.FrameBorderSize > 0.0f)
                RenderFrameBorder(bb.Min, bb.Max, rounding);
            else
                window.DrawList.AddRect(bb.Min, bb.Max, GetColorU32(ImGuiCol_FrameBg), rounding, 1.0f * (float)(int)g.Style._MainScale); // Color buttons are often in need of some sort of border // FIXME-DPI
        }

        // Drag and Drop Source
        // NB: The ActiveId test is merely an optional micro-optimization, BeginDragDropSource() does the same test.
        if (g.ActiveId == id && (flags & ImGuiColorEditFlags_NoDragDrop) == 0 && BeginDragDropSource())
        {
            byte[] payload_data = new byte[16];
            BitConverter.GetBytes(col_rgb.x).CopyTo(payload_data, 0);
            BitConverter.GetBytes(col_rgb.y).CopyTo(payload_data, 4);
            BitConverter.GetBytes(col_rgb.z).CopyTo(payload_data, 8);
            BitConverter.GetBytes(col_rgb.w).CopyTo(payload_data, 12);
            if ((flags & ImGuiColorEditFlags_NoAlpha) != 0)
                SetDragDropPayload(IMGUI_PAYLOAD_TYPE_COLOR_3F, payload_data, sizeof(float) * 3, ImGuiCond_Once);
            else
                SetDragDropPayload(IMGUI_PAYLOAD_TYPE_COLOR_4F, payload_data, sizeof(float) * 4, ImGuiCond_Once);
            ColorButton(desc_id, col, flags);
            SameLine();
            TextEx("Color");
            EndDragDropSource();
        }

        // Tooltip
        if ((flags & ImGuiColorEditFlags_NoTooltip) == 0 && hovered && IsItemHovered(ImGuiHoveredFlags_ForTooltip))
        {
            float[] col_arr = { col.x, col.y, col.z, col.w }; // C++: ColorTooltip(desc_id, &col.x, ...)
            ColorTooltip(desc_id, col_arr, flags & (ImGuiColorEditFlags_InputMask_ | ImGuiColorEditFlags_AlphaMask_));
        }

        return pressed;
    }

    // This allowed passing 0 to set default, whereas NewFrame() will assert if one of missing. Abide to old logic.
    // (C++: defined under #ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS)
    // C++: void ImGui::SetColorEditOptions(ImGuiColorEditFlags flags)
    public static void SetColorEditOptions(ImGuiColorEditFlags flags)
    {
        ImGuiContext g = GImGui;
        if ((flags & ImGuiColorEditFlags_DisplayMask_) == 0) { flags |= ImGuiColorEditFlags_DefaultOptions_ & ImGuiColorEditFlags_DisplayMask_; }
        if ((flags & ImGuiColorEditFlags_DataTypeMask_) == 0) { flags |= ImGuiColorEditFlags_DefaultOptions_ & ImGuiColorEditFlags_DataTypeMask_; }
        if ((flags & ImGuiColorEditFlags_PickerMask_) == 0) { flags |= ImGuiColorEditFlags_DefaultOptions_ & ImGuiColorEditFlags_PickerMask_; }
        if ((flags & ImGuiColorEditFlags_InputMask_) == 0) { flags |= ImGuiColorEditFlags_DefaultOptions_ & ImGuiColorEditFlags_InputMask_; }
        g.IO.ConfigColorEditFlags = flags;
    }

    // Note: only access 3 floats if ImGuiColorEditFlags_NoAlpha flag is set.
    // C++: void ImGui::ColorTooltip(const char* text, const float* col, ImGuiColorEditFlags flags)
    public static void ColorTooltip(string text, float[] col, ImGuiColorEditFlags flags)
    {
        ImGuiContext g = GImGui;

        if (!BeginTooltipEx(ImGuiTooltipFlags_OverridePrevious, ImGuiWindowFlags_None))
            return;
        int text_end = text != null ? FindRenderedTextEnd(text) : 0; // C++: text ? FindRenderedTextEnd(text, NULL) : text
        if (text != null && text_end > 0)
        {
            TextEx(text, text_end);
            Separator();
        }

        ImVec2 sz = new ImVec2(g.FontSize * 3 + g.Style.FramePadding.y * 2, g.FontSize * 3 + g.Style.FramePadding.y * 2);
        ImVec4 cf = new ImVec4(col[0], col[1], col[2], (flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 1.0f : col[3]);
        int cr = IM_F32_TO_INT8_SAT(col[0]), cg = IM_F32_TO_INT8_SAT(col[1]), cb = IM_F32_TO_INT8_SAT(col[2]), ca = (flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 255 : IM_F32_TO_INT8_SAT(col[3]);
        ImGuiColorEditFlags flags_to_forward = ImGuiColorEditFlags_InputMask_ | ImGuiColorEditFlags_AlphaMask_;
        ColorButton("##preview", cf, (flags & flags_to_forward) | ImGuiColorEditFlags_NoTooltip, sz);
        SameLine();
        if ((flags & ImGuiColorEditFlags_InputRGB) != 0 || (flags & ImGuiColorEditFlags_InputMask_) == 0)
        {
            if ((flags & ImGuiColorEditFlags_NoAlpha) != 0)
                Text("#%02X%02X%02X\nR: %d, G: %d, B: %d\n(%.3f, %.3f, %.3f)", cr, cg, cb, cr, cg, cb, col[0], col[1], col[2]);
            else
                Text("#%02X%02X%02X%02X\nR:%d, G:%d, B:%d, A:%d\n(%.3f, %.3f, %.3f, %.3f)", cr, cg, cb, ca, cr, cg, cb, ca, col[0], col[1], col[2], col[3]);
        }
        else if ((flags & ImGuiColorEditFlags_InputHSV) != 0)
        {
            if ((flags & ImGuiColorEditFlags_NoAlpha) != 0)
                Text("H: %.3f, S: %.3f, V: %.3f", col[0], col[1], col[2]);
            else
                Text("H: %.3f, S: %.3f, V: %.3f, A: %.3f", col[0], col[1], col[2], col[3]);
        }
        EndTooltip();
    }

    // C++: void ImGui::ColorEditOptionsPopup(const float* col, ImGuiColorEditFlags flags)
    public static void ColorEditOptionsPopup(float[] col, ImGuiColorEditFlags flags)
    {
        bool allow_opt_inputs = (flags & ImGuiColorEditFlags_DisplayMask_) == 0;
        bool allow_opt_datatype = (flags & ImGuiColorEditFlags_DataTypeMask_) == 0;
        if ((!allow_opt_inputs && !allow_opt_datatype) || !BeginPopup("context"))
            return;

        ImGuiContext g = GImGui;
        PushItemFlag(ImGuiItemFlags_NoMarkEdited, true);
        ImGuiColorEditFlags opts = g.IO.ConfigColorEditFlags;
        if (allow_opt_inputs)
        {
            if (RadioButton("RGB", (opts & ImGuiColorEditFlags_DisplayRGB) != 0)) opts = (opts & ~ImGuiColorEditFlags_DisplayMask_) | ImGuiColorEditFlags_DisplayRGB;
            if (RadioButton("HSV", (opts & ImGuiColorEditFlags_DisplayHSV) != 0)) opts = (opts & ~ImGuiColorEditFlags_DisplayMask_) | ImGuiColorEditFlags_DisplayHSV;
            if (RadioButton("Hex", (opts & ImGuiColorEditFlags_DisplayHex) != 0)) opts = (opts & ~ImGuiColorEditFlags_DisplayMask_) | ImGuiColorEditFlags_DisplayHex;
        }
        if (allow_opt_datatype)
        {
            if (allow_opt_inputs) Separator();
            if (RadioButton("0..255", (opts & ImGuiColorEditFlags_Uint8) != 0)) opts = (opts & ~ImGuiColorEditFlags_DataTypeMask_) | ImGuiColorEditFlags_Uint8;
            if (RadioButton("0.00..1.00", (opts & ImGuiColorEditFlags_Float) != 0)) opts = (opts & ~ImGuiColorEditFlags_DataTypeMask_) | ImGuiColorEditFlags_Float;
        }

        if (allow_opt_inputs || allow_opt_datatype)
            Separator();
        if (Button("Copy as..", new ImVec2(-1, 0)))
            OpenPopup("Copy");
        if (BeginPopup("Copy"))
        {
            int cr = IM_F32_TO_INT8_SAT(col[0]), cg = IM_F32_TO_INT8_SAT(col[1]), cb = IM_F32_TO_INT8_SAT(col[2]), ca = (flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 255 : IM_F32_TO_INT8_SAT(col[3]);
            string buf;
            buf = ImFmt.Sprintf("(%.3ff, %.3ff, %.3ff, %.3ff)", col[0], col[1], col[2], (flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 1.0f : col[3]);
            if (Selectable(buf))
                SetClipboardText(buf);
            buf = ImFmt.Sprintf("(%d,%d,%d,%d)", cr, cg, cb, ca);
            if (Selectable(buf))
                SetClipboardText(buf);
            buf = ImFmt.Sprintf("#%02X%02X%02X", cr, cg, cb);
            if (Selectable(buf))
                SetClipboardText(buf);
            if ((flags & ImGuiColorEditFlags_NoAlpha) == 0)
            {
                buf = ImFmt.Sprintf("#%02X%02X%02X%02X", cr, cg, cb, ca);
                if (Selectable(buf))
                    SetClipboardText(buf);
            }
            EndPopup();
        }

        g.IO.ConfigColorEditFlags = opts;
        PopItemFlag();
        EndPopup();
    }

    // C++: void ImGui::ColorPickerOptionsPopup(const float* ref_col, ImGuiColorEditFlags flags)
    public static void ColorPickerOptionsPopup(float[] ref_col, ImGuiColorEditFlags flags)
    {
        bool allow_opt_picker = (flags & ImGuiColorEditFlags_PickerMask_) == 0;
        bool allow_opt_alpha_bar = (flags & ImGuiColorEditFlags_NoAlpha) == 0 && (flags & ImGuiColorEditFlags_AlphaBar) == 0;
        if ((!allow_opt_picker && !allow_opt_alpha_bar) || !BeginPopup("context"))
            return;

        ImGuiContext g = GImGui;
        PushItemFlag(ImGuiItemFlags_NoMarkEdited, true);
        if (allow_opt_picker)
        {
            ImVec2 picker_size = new ImVec2(g.FontSize * 8, ImMath.ImMax(g.FontSize * 8 - (GetFrameHeight() + g.Style.ItemInnerSpacing.x), 1.0f)); // FIXME: Picker size copied from main picker function
            PushItemWidth(picker_size.x);
            for (int picker_type = 0; picker_type < 2; picker_type++)
            {
                // Draw small/thumbnail version of each picker type (over an invisible button for selection)
                if (picker_type > 0) Separator();
                PushID(picker_type);
                ImGuiColorEditFlags picker_flags = ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_NoOptions | ImGuiColorEditFlags_NoLabel | ImGuiColorEditFlags_NoSidePreview | (flags & ImGuiColorEditFlags_NoAlpha);
                if (picker_type == 0) picker_flags |= ImGuiColorEditFlags_PickerHueBar;
                if (picker_type == 1) picker_flags |= ImGuiColorEditFlags_PickerHueWheel;
                ImVec2 backup_pos = GetCursorScreenPos();
                if (Selectable("##selectable", false, 0, picker_size)) // By default, Selectable() is closing popup
                    g.IO.ConfigColorEditFlags = (g.IO.ConfigColorEditFlags & ~ImGuiColorEditFlags_PickerMask_) | (picker_flags & ImGuiColorEditFlags_PickerMask_);
                SetCursorScreenPos(backup_pos);
                float[] previewing_ref_col = new float[4];
                Array.Copy(ref_col, previewing_ref_col, (picker_flags & ImGuiColorEditFlags_NoAlpha) != 0 ? 3 : 4); // memcpy(&previewing_ref_col, ref_col, sizeof(float) * N)
                ColorPicker4("##previewing_picker", previewing_ref_col, picker_flags);
                PopID();
            }
            PopItemWidth();
        }
        if (allow_opt_alpha_bar)
        {
            if (allow_opt_picker) Separator();
            CheckboxFlags("Alpha Bar", ref g.IO.ConfigColorEditFlags, ImGuiColorEditFlags_AlphaBar);
        }
        PopItemFlag();
        EndPopup();
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Separator Text (C++ imgui_widgets.cpp 1740-1803, ported with the TreeNode range)
    //-------------------------------------------------------------------------

    // C++ (imgui_internal.h): void ImGui::SeparatorTextEx(ImGuiID id, const char* label, const char* label_end, float extra_w)
    public static void SeparatorTextEx(ImGuiID id, string label, int label_end, float extra_w)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiStyle style = g.Style;

        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImVec2 pos = window.DC.CursorPos;
        ImVec2 padding = style.SeparatorTextPadding;

        float separator_thickness = style.SeparatorTextBorderSize;
        ImVec2 min_size = new ImVec2(label_size.x + extra_w + padding.x * 2.0f, ImMath.ImMax(label_size.y + padding.y * 2.0f, separator_thickness));
        ImRect bb = new ImRect(pos, new ImVec2(window.WorkRect.Max.x, pos.y + min_size.y));
        float text_baseline_y = ImMath.ImTrunc((bb.GetHeight() - label_size.y) * style.SeparatorTextAlign.y + 0.999f); //ImMax(padding.y, ImTrunc((style.SeparatorTextSize - label_size.y) * 0.5f));
        ItemSize(min_size, text_baseline_y);
        if (!ItemAdd(bb, id))
            return;

        float sep1_x1 = pos.x;
        float sep2_x2 = bb.Max.x;
        float seps_y = ImMath.ImTrunc((bb.Min.y + bb.Max.y) * 0.5f + 0.999f);

        float label_avail_w = ImMath.ImMax(0.0f, sep2_x2 - sep1_x1 - padding.x * 2.0f);
        ImVec2 label_pos = new ImVec2(pos.x + padding.x + ImMath.ImMax(0.0f, (label_avail_w - label_size.x - extra_w) * style.SeparatorTextAlign.x), pos.y + text_baseline_y); // FIXME-ALIGN

        // This allows using SameLine() to position something in the 'extra_w'
        window.DC.CursorPosPrevLine.x = label_pos.x + label_size.x;

        ImU32 separator_col = GetColorU32(ImGuiCol_Separator);
        if (label_size.x > 0.0f)
        {
            float sep1_x2 = label_pos.x - style.ItemSpacing.x;
            float sep2_x1 = label_pos.x + label_size.x + extra_w + style.ItemSpacing.x;
            if (sep1_x2 > sep1_x1 && separator_thickness > 0.0f)
                window.DrawList.AddLineH(sep1_x1, sep1_x2, seps_y, separator_col, separator_thickness);
            if (sep2_x2 > sep2_x1 && separator_thickness > 0.0f)
                window.DrawList.AddLineH(sep2_x1, sep2_x2, seps_y, separator_col, separator_thickness);
            if (g.LogEnabled)
                LogSetNextTextDecoration("---", null);
            RenderTextEllipsis(window.DrawList, label_pos, new ImVec2(bb.Max.x, bb.Max.y + style.ItemSpacing.y), bb.Max.x, label, label_end, label_size);
        }
        else
        {
            if (g.LogEnabled)
                LogText("---");
            if (separator_thickness > 0.0f)
                window.DrawList.AddLineH(sep1_x1, sep2_x2, seps_y, separator_col, separator_thickness);
        }
    }

    // C++: void ImGui::SeparatorText(const char* label)
    public static void SeparatorText(string label)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return;

        // The SeparatorText() vs SeparatorTextEx() distinction is designed to be considerate that we may want:
        // - allow separator-text to be draggable items (would require a stable ID + a noticeable highlight)
        // - this high-level entry point to allow formatting? (which in turns may require ID separate from formatted string)
        // - because of this we probably can't turn 'const char* label' into 'const char* fmt, ...'
        // Otherwise, we can decide that users wanting to drag this would layout a dedicated drag-item,
        // and then we can turn this into a format function.
        SeparatorTextEx(0, label, FindRenderedTextEnd(label), 0.0f);
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: TreeNode, CollapsingHeader, etc.
    //-------------------------------------------------------------------------

    // C++: bool ImGui::TreeNode(const char* str_id, const char* fmt, ...)
    public static bool TreeNode(string str_id, string fmt, params object[] args)
    {
        return TreeNodeExV(str_id, 0, fmt, args);
    }

    // C++: bool ImGui::TreeNode(const void* ptr_id, const char* fmt, ...)
    public static bool TreeNode(object ptr_id, string fmt, params object[] args)
    {
        return TreeNodeExV(ptr_id, 0, fmt, args);
    }

    // C++: bool ImGui::TreeNode(const char* label)
    public static bool TreeNode(string label)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;
        ImGuiID id = window.GetID(label);
        return TreeNodeBehavior(id, ImGuiTreeNodeFlags_None, label, -1);
    }

    // C++: bool ImGui::TreeNodeV(const char* str_id, const char* fmt, va_list args)
    public static bool TreeNodeV(string str_id, string fmt, params object[] args)
    {
        return TreeNodeExV(str_id, 0, fmt, args);
    }

    // C++: bool ImGui::TreeNodeV(const void* ptr_id, const char* fmt, va_list args)
    public static bool TreeNodeV(object ptr_id, string fmt, params object[] args)
    {
        return TreeNodeExV(ptr_id, 0, fmt, args);
    }

    // C++: bool ImGui::TreeNodeEx(const char* label, ImGuiTreeNodeFlags flags = 0)
    public static bool TreeNodeEx(string label, ImGuiTreeNodeFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;
        ImGuiID id = window.GetID(label);
        return TreeNodeBehavior(id, flags, label, -1);
    }

    // C++: bool ImGui::TreeNodeEx(const char* str_id, ImGuiTreeNodeFlags flags, const char* fmt, ...)
    public static bool TreeNodeEx(string str_id, ImGuiTreeNodeFlags flags, string fmt, params object[] args)
    {
        return TreeNodeExV(str_id, flags, fmt, args);
    }

    // C++: bool ImGui::TreeNodeEx(const void* ptr_id, ImGuiTreeNodeFlags flags, const char* fmt, ...)
    public static bool TreeNodeEx(object ptr_id, ImGuiTreeNodeFlags flags, string fmt, params object[] args)
    {
        return TreeNodeExV(ptr_id, flags, fmt, args);
    }

    // C++: bool ImGui::TreeNodeExV(const char* str_id, ImGuiTreeNodeFlags flags, const char* fmt, va_list args)
    public static bool TreeNodeExV(string str_id, ImGuiTreeNodeFlags flags, string fmt, params object[] args)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiID id = window.GetID(str_id);
        // C++: ImFormatStringToTempBufferV(&label, &label_end, fmt, args);
        string label = ImFmt.Sprintf(fmt, args);
        int label_end = label.Length;
        return TreeNodeBehavior(id, flags, label, label_end);
    }

    // C++: bool ImGui::TreeNodeExV(const void* ptr_id, ImGuiTreeNodeFlags flags, const char* fmt, va_list args)
    public static bool TreeNodeExV(object ptr_id, ImGuiTreeNodeFlags flags, string fmt, params object[] args)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiID id = window.GetID(ptr_id);
        string label = ImFmt.Sprintf(fmt, args);
        int label_end = label.Length;
        return TreeNodeBehavior(id, flags, label, label_end);
    }

    // The reason those two functions are not yet in public API is because I would like to design a more feature-full and generic API for this.
    // They are otherwise function (cc: #3823, #9251, #7553, #6754, #5423, #2958, #2079, #1947, #1131, #722)
    // C++: bool ImGui::TreeNodeGetOpen(ImGuiID storage_id)
    public static bool TreeNodeGetOpen(ImGuiID storage_id)
    {
        ImGuiContext g = GImGui;
        ImGuiStorage storage = g.CurrentWindow.DC.StateStorage;
        return storage.GetInt(storage_id, 0) != 0;
    }

    // C++: void ImGui::TreeNodeSetOpen(ImGuiID storage_id, bool is_open)
    public static void TreeNodeSetOpen(ImGuiID storage_id, bool is_open)
    {
        ImGuiContext g = GImGui;
        ImGuiStorage storage = g.CurrentWindow.DC.StateStorage;
        storage.SetInt(storage_id, is_open ? 1 : 0);
    }

    // C++: bool ImGui::TreeNodeUpdateNextOpen(ImGuiID storage_id, ImGuiTreeNodeFlags flags)
    public static bool TreeNodeUpdateNextOpen(ImGuiID storage_id, ImGuiTreeNodeFlags flags)
    {
        // Leaf node always open a new tree/id scope. If you never use it, add ImGuiTreeNodeFlags_NoTreePushOnOpen.
        if ((flags & ImGuiTreeNodeFlags_Leaf) != 0)
            return true;

        // We only write to the tree storage if the user clicks, or explicitly use the SetNextItemOpen function
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiStorage storage = window.DC.StateStorage;

        bool is_open;
        if ((g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasOpen) != 0)
        {
            if ((g.NextItemData.OpenCond & ImGuiCond_Always) != 0)
            {
                is_open = g.NextItemData.OpenVal;
                TreeNodeSetOpen(storage_id, is_open);
            }
            else
            {
                // We treat ImGuiCond_Once and ImGuiCond_FirstUseEver the same because tree node state are not saved persistently.
                int stored_value = storage.GetInt(storage_id, -1);
                if (stored_value == -1)
                {
                    is_open = g.NextItemData.OpenVal;
                    TreeNodeSetOpen(storage_id, is_open);
                }
                else
                {
                    is_open = stored_value != 0;
                }
            }
        }
        else
        {
            is_open = storage.GetInt(storage_id, (flags & ImGuiTreeNodeFlags_DefaultOpen) != 0 ? 1 : 0) != 0;
        }

        // When logging is enabled, we automatically expand tree nodes (but *NOT* collapsing headers.. seems like sensible behavior).
        // NB- If we are above max depth we still allow manually opened nodes to be logged.
        if (g.LogEnabled && (flags & ImGuiTreeNodeFlags_NoAutoOpenOnLog) == 0 && (window.DC.TreeDepth - g.LogDepthRef) < g.LogDepthToExpand)
            is_open = true;

        return is_open;
    }

    // Store ImGuiTreeNodeStackData for just submitted node.
    // Currently only supports 32 level deep. Easy to increase to e.g. 64.
    // C++: static void TreeNodeStoreStackData(ImGuiTreeNodeFlags flags, float x1)
    private static void TreeNodeStoreStackData(ImGuiTreeNodeFlags flags, float x1)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        g.TreeNodeStack.resize(g.TreeNodeStack.Size + 1);
        ref ImGuiTreeNodeStackData tree_node_data = ref g.TreeNodeStack.Data[g.TreeNodeStack.Size - 1];
        tree_node_data.ID = g.LastItemData.ID;
        tree_node_data.TreeFlags = flags;
        tree_node_data.ItemFlags = g.LastItemData.ItemFlags;
        tree_node_data.NavRect = g.LastItemData.NavRect;

        // Initially I tried to latch value for GetColorU32(ImGuiCol_TreeLines) but it's not a good trade-off for very large trees.
        bool draw_lines = (flags & (ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_DrawLinesToNodes)) != 0;
        tree_node_data.DrawLinesX1 = draw_lines ? (x1 + g.FontSize * 0.5f + g.Style.FramePadding.x) : float.MaxValue;
        tree_node_data.DrawLinesTableColumn = (draw_lines && g.CurrentTable != null) ? (short)g.CurrentTable.CurrentColumn : (short)-1;
        tree_node_data.DrawLinesToNodesY2 = float.MinValue;
        window.DC.TreeHasStackDataDepthMask |= (ImU32)(1 << window.DC.TreeDepth);
        if ((flags & ImGuiTreeNodeFlags_DrawLinesToNodes) != 0)
            window.DC.TreeRecordsClippedNodesY2Mask |= (ImU32)(1 << window.DC.TreeDepth);
    }

    // C++: #define TREE_NODE_MAX_DEPTH 32
    // C++: #define TREE_NODE_GET_DEPTH_MASK(_DEPTH) (((ImU32)(_DEPTH) < TREE_NODE_MAX_DEPTH) ? (1u << (_DEPTH)) : 0u)
    private const int TREE_NODE_MAX_DEPTH = 32;
    private static ImU32 TREE_NODE_GET_DEPTH_MASK(int depth) => ((uint)depth < TREE_NODE_MAX_DEPTH) ? (1u << depth) : 0u;

    // C++: bool ImGui::TreeNodeBehavior(ImGuiID id, ImGuiTreeNodeFlags flags, const char* label, const char* label_end = NULL)
    public static bool TreeNodeBehavior(ImGuiID id, ImGuiTreeNodeFlags flags, string label, int label_end = -1)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;

        // When not framed, we vertically increase height up to typical framed widget height
        bool display_frame = (flags & ImGuiTreeNodeFlags_Framed) != 0;
        bool use_frame_padding = display_frame || (flags & ImGuiTreeNodeFlags_FramePadding) != 0;
        ImVec2 padding = use_frame_padding ? style.FramePadding : new ImVec2(style.FramePadding.x, ImMath.ImMin(window.DC.CurrLineTextBaseOffset, style.FramePadding.y));

        if (label_end < 0) // C++: if (!label_end)
            label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);

        float text_offset_x = g.FontSize + (display_frame ? padding.x * 3 : padding.x * 2); // Collapsing arrow width + Spacing
        float text_offset_y = use_frame_padding ? ImMath.ImMax(style.FramePadding.y, window.DC.CurrLineTextBaseOffset) : window.DC.CurrLineTextBaseOffset; // Latch before ItemSize changes it
        float text_width = g.FontSize + label_size.x + padding.x * 2; // Include collapsing arrow

        float frame_height = label_size.y + padding.y * 2;
        bool span_all_columns = (flags & ImGuiTreeNodeFlags_SpanAllColumns) != 0 && (g.CurrentTable != null);
        bool span_all_columns_label = (flags & ImGuiTreeNodeFlags_LabelSpanAllColumns) != 0 && (g.CurrentTable != null);
        ImRect frame_bb = new ImRect();
        frame_bb.Min.x = span_all_columns ? window.ParentWorkRect.Min.x : (flags & ImGuiTreeNodeFlags_SpanFullWidth) != 0 ? window.WorkRect.Min.x : window.DC.CursorPos.x;
        frame_bb.Min.y = window.DC.CursorPos.y + (text_offset_y - padding.y);
        frame_bb.Max.x = span_all_columns ? window.ParentWorkRect.Max.x : (flags & ImGuiTreeNodeFlags_SpanLabelWidth) != 0 ? window.DC.CursorPos.x + text_width + padding.x : window.WorkRect.Max.x;
        frame_bb.Max.y = window.DC.CursorPos.y + (text_offset_y - padding.y) + frame_height;
        if (display_frame)
        {
            float outer_extend = ImMath.ImTrunc(window.WindowPadding.x * 0.5f); // Framed header expand a little outside of current limits
            frame_bb.Min.x -= outer_extend;
            frame_bb.Max.x += outer_extend;
        }

        ImVec2 text_pos = new ImVec2(window.DC.CursorPos.x + text_offset_x, window.DC.CursorPos.y + text_offset_y);
        ItemSize(new ImVec2(text_width, frame_height), padding.y);

        // For regular tree nodes, we arbitrary allow to click past 2 worth of ItemSpacing
        ImRect interact_bb = frame_bb;
        if ((flags & (ImGuiTreeNodeFlags_Framed | ImGuiTreeNodeFlags_SpanAvailWidth | ImGuiTreeNodeFlags_SpanFullWidth | ImGuiTreeNodeFlags_SpanLabelWidth | ImGuiTreeNodeFlags_SpanAllColumns)) == 0)
            interact_bb.Max.x = frame_bb.Min.x + text_width + (label_size.x > 0.0f ? style.ItemSpacing.x * 2.0f : 0.0f);

        // Compute open and multi-select states before ItemAdd() as it clear NextItem data.
        ImGuiID storage_id = (g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasStorageID) != 0 ? g.NextItemData.StorageId : id;
        bool is_open = TreeNodeUpdateNextOpen(storage_id, flags);

        bool is_visible;
        if (span_all_columns || span_all_columns_label)
        {
            // Modify ClipRect for the ItemAdd(), faster than doing a PushColumnsBackground/PushTableBackgroundChannel for every Selectable..
            float backup_clip_rect_min_x = window.ClipRect.Min.x;
            float backup_clip_rect_max_x = window.ClipRect.Max.x;
            window.ClipRect.Min.x = window.ParentWorkRect.Min.x;
            window.ClipRect.Max.x = window.ParentWorkRect.Max.x;
            is_visible = ItemAdd(interact_bb, id);
            window.ClipRect.Min.x = backup_clip_rect_min_x;
            window.ClipRect.Max.x = backup_clip_rect_max_x;
        }
        else
        {
            is_visible = ItemAdd(interact_bb, id);
        }
        g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HasDisplayRect;
        g.LastItemData.DisplayRect = frame_bb;

        // If a NavLeft request is happening and ImGuiTreeNodeFlags_NavLeftJumpsToParent enabled:
        // Store data for the current depth to allow returning to this node from any child item.
        // For this purpose we essentially compare if g.NavIdIsAlive went from 0 to 1 between TreeNode() and TreePop().
        // It will become tempting to enable ImGuiTreeNodeFlags_NavLeftJumpsToParent by default or move it to ImGuiStyle.
        bool store_tree_node_stack_data = false;
        if ((flags & ImGuiTreeNodeFlags_DrawLinesMask_) == 0)
            flags |= g.Style.TreeLinesFlags;
        bool draw_tree_lines = (flags & (ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_DrawLinesToNodes)) != 0 && (frame_bb.Min.y < window.ClipRect.Max.y) && (g.Style.TreeLinesSize > 0.0f);
        if ((flags & ImGuiTreeNodeFlags_NoTreePushOnOpen) == 0 && window.DC.TreeDepth < TREE_NODE_MAX_DEPTH)
        {
            store_tree_node_stack_data = draw_tree_lines;
            if ((flags & ImGuiTreeNodeFlags_NavLeftJumpsToParent) != 0 && !g.NavIdIsAlive)
                if (g.NavMoveDir == ImGuiDir_Left && g.NavWindow == window && NavMoveRequestButNoResultYet())
                    store_tree_node_stack_data = true;
        }

        bool is_leaf = (flags & ImGuiTreeNodeFlags_Leaf) != 0;
        if (!is_visible)
        {
            if ((flags & ImGuiTreeNodeFlags_DrawLinesToNodes) != 0)
            {
                ImU32 tree_depth_mask = TREE_NODE_GET_DEPTH_MASK(window.DC.TreeDepth - 1);
                if ((window.DC.TreeRecordsClippedNodesY2Mask & tree_depth_mask) != 0)
                {
                    ref ImGuiTreeNodeStackData parent_data = ref g.TreeNodeStack.Data[g.TreeNodeStack.Size - 1];
                    parent_data.DrawLinesToNodesY2 = ImMath.ImMax(parent_data.DrawLinesToNodesY2, window.DC.CursorPos.y); // Don't need to aim to mid Y position as we are clipped anyway.
                    if (frame_bb.Min.y >= window.ClipRect.Max.y)
                        window.DC.TreeRecordsClippedNodesY2Mask &= ~tree_depth_mask; // Done
                }
            }
            if (is_open && store_tree_node_stack_data)
                TreeNodeStoreStackData(flags, text_pos.x - text_offset_x); // Call before TreePushOverrideID()
            if (is_open && (flags & ImGuiTreeNodeFlags_NoTreePushOnOpen) == 0)
                TreePushOverrideID(id);
            // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
            return is_open;
        }

        if (span_all_columns || span_all_columns_label)
        {
            TablePushBackgroundChannel();
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HasClipRect;
            g.LastItemData.ClipRect = window.ClipRect;
        }

        ImGuiButtonFlags button_flags = ImGuiTreeNodeFlags_None;
        if ((flags & ImGuiTreeNodeFlags_AllowOverlap) != 0 || (g.LastItemData.ItemFlags & ImGuiItemFlags_AllowOverlap) != 0)
            button_flags |= ImGuiButtonFlags_AllowOverlap;
        if (!is_leaf)
            button_flags |= ImGuiButtonFlags_PressedOnDragDropHold;

        // We allow clicking on the arrow section with keyboard modifiers held, in order to easily
        // allow browsing a tree while preserving selection with code implementing multi-selection patterns.
        // When clicking on the rest of the tree node we always disallow keyboard modifiers.
        float arrow_hit_x1 = (text_pos.x - text_offset_x) - style.TouchExtraPadding.x;
        float arrow_hit_x2 = (text_pos.x - text_offset_x) + (g.FontSize + padding.x * 2.0f) + style.TouchExtraPadding.x;
        bool is_mouse_x_over_arrow = (g.IO.MousePos.x >= arrow_hit_x1 && g.IO.MousePos.x < arrow_hit_x2);

        bool is_multi_select = (g.LastItemData.ItemFlags & ImGuiItemFlags_IsMultiSelect) != 0;
        if (is_multi_select) // We absolutely need to distinguish open vs select so _OpenOnArrow comes by default
            flags |= (flags & ImGuiTreeNodeFlags_OpenOnMask_) == 0 ? ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_OpenOnDoubleClick : ImGuiTreeNodeFlags_OpenOnArrow;

        // Open behaviors can be altered with the _OpenOnArrow and _OnOnDoubleClick flags.
        // Some alteration have subtle effects (e.g. toggle on MouseUp vs MouseDown events) due to requirements for multi-selection and drag and drop support.
        // - Single-click on label = Toggle on MouseUp (default, when _OpenOnArrow=0)
        // - Single-click on arrow = Toggle on MouseDown (when _OpenOnArrow=0)
        // - Single-click on arrow = Toggle on MouseDown (when _OpenOnArrow=1)
        // - Double-click on label = Toggle on MouseDoubleClick (when _OpenOnDoubleClick=1)
        // - Double-click on arrow = Toggle on MouseDoubleClick (when _OpenOnDoubleClick=1 and _OpenOnArrow=0)
        // It is rather standard that arrow click react on Down rather than Up.
        // We set ImGuiButtonFlags_PressedOnClickRelease on OpenOnDoubleClick because we want the item to be active on the initial MouseDown in order for drag and drop to work.
        if (is_mouse_x_over_arrow)
            button_flags |= ImGuiButtonFlags_PressedOnClick;
        else if ((flags & ImGuiTreeNodeFlags_OpenOnDoubleClick) != 0)
            button_flags |= ImGuiButtonFlags_PressedOnClickRelease | ImGuiButtonFlags_PressedOnDoubleClick;
        else
            button_flags |= ImGuiButtonFlags_PressedOnClickRelease;
        if ((flags & ImGuiTreeNodeFlags_NoNavFocus) != 0)
            button_flags |= ImGuiButtonFlags_NoNavFocus;

        bool selected = (flags & ImGuiTreeNodeFlags_Selected) != 0;
        bool was_selected = selected;

        // Multi-selection support (header)
        if (is_multi_select)
        {
            // Handle multi-select + alter button flags for it
            MultiSelectItemHeader(id, ref selected, ref button_flags);
            if (is_mouse_x_over_arrow)
                button_flags = (button_flags | ImGuiButtonFlags_PressedOnClick) & ~ImGuiButtonFlags_PressedOnClickRelease;
        }
        else
        {
            if (window != g.HoveredWindow || !is_mouse_x_over_arrow)
                button_flags |= ImGuiButtonFlags_NoKeyModsAllowed;
        }

        bool hovered, held;
        bool pressed = ButtonBehavior(interact_bb, id, out hovered, out held, button_flags);
        bool toggled = false;
        if (!is_leaf)
        {
            if (pressed && g.DragDropHoldJustPressedId != id)
            {
                if ((flags & ImGuiTreeNodeFlags_OpenOnMask_) == 0 || (g.NavActivateId == id && !is_multi_select))
                    toggled = true; // Single click
                if ((flags & ImGuiTreeNodeFlags_OpenOnArrow) != 0)
                    toggled |= is_mouse_x_over_arrow && !g.NavHighlightItemUnderNav; // Lightweight equivalent of IsMouseHoveringRect() since ButtonBehavior() already did the job
                if ((flags & ImGuiTreeNodeFlags_OpenOnDoubleClick) != 0 && g.IO.MouseClickedCount[0] == 2)
                    toggled = true; // Double click
            }
            else if (pressed && g.DragDropHoldJustPressedId == id)
            {
                Debug.Assert((button_flags & ImGuiButtonFlags_PressedOnDragDropHold) != 0);
                if (!is_open) // When using Drag and Drop "hold to open" we keep the node highlighted after opening, but never close it again.
                    toggled = true;
                else
                    pressed = false; // Cancel press so it doesn't trigger selection.
            }

            if (g.NavId == id && g.NavMoveDir == ImGuiDir_Left && is_open)
            {
                toggled = true;
                NavClearPreferredPosForAxis(ImGuiAxis_X);
                NavMoveRequestCancel();
            }
            if (g.NavId == id && g.NavMoveDir == ImGuiDir_Right && !is_open) // If there's something upcoming on the line we may want to give it the priority?
            {
                toggled = true;
                NavClearPreferredPosForAxis(ImGuiAxis_X);
                NavMoveRequestCancel();
            }

            if (toggled)
            {
                is_open = !is_open;
                window.DC.StateStorage.SetInt(storage_id, is_open ? 1 : 0);
                g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_ToggledOpen;
            }
        }

        // Multi-selection support (footer)
        if (is_multi_select)
        {
            bool pressed_copy = pressed && !toggled;
            MultiSelectItemFooter(id, ref selected, ref pressed_copy);
            if (pressed)
                SetNavID(id, window.DC.NavLayerCurrent, g.CurrentFocusScopeId, interact_bb);
        }

        if (selected != was_selected)
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_ToggledSelection;

        // Render
        {
            ImU32 text_col = GetColorU32(ImGuiCol_Text);
            ImGuiNavRenderCursorFlags nav_render_cursor_flags = ImGuiNavRenderCursorFlags_Compact;
            if (is_multi_select)
                nav_render_cursor_flags |= ImGuiNavRenderCursorFlags_AlwaysDraw; // Always show the nav rectangle
            if (display_frame)
            {
                // Framed type
                ImU32 bg_col = GetColorU32((held && hovered) ? ImGuiCol_HeaderActive : hovered ? ImGuiCol_HeaderHovered : ImGuiCol_Header);
                RenderFrame(frame_bb.Min, frame_bb.Max, bg_col, true, style.FrameRounding);
                RenderNavCursor(frame_bb, id, nav_render_cursor_flags);
                if (span_all_columns && !span_all_columns_label)
                    TablePopBackgroundChannel();
                if ((flags & ImGuiTreeNodeFlags_Bullet) != 0)
                    RenderBullet(window.DrawList, new ImVec2(text_pos.x - text_offset_x * 0.60f, text_pos.y + g.FontSize * 0.5f), text_col);
                else if (!is_leaf)
                    RenderArrow(window.DrawList, new ImVec2(text_pos.x - text_offset_x + padding.x, text_pos.y), text_col, is_open ? ((flags & ImGuiTreeNodeFlags_UpsideDownArrow) != 0 ? ImGuiDir_Up : ImGuiDir_Down) : ImGuiDir_Right, 1.0f);
                else // Leaf without bullet, left-adjusted text
                    text_pos.x -= text_offset_x - padding.x;
                if ((flags & ImGuiTreeNodeFlags_ClipLabelForTrailingButton) != 0)
                    frame_bb.Max.x -= g.FontSize + style.FramePadding.x;
                if (g.LogEnabled)
                    LogSetNextTextDecoration("###", "###");
            }
            else
            {
                // Unframed typed for tree nodes
                if (hovered || selected)
                {
                    ImU32 bg_col = GetColorU32((held && hovered) ? ImGuiCol_HeaderActive : hovered ? ImGuiCol_HeaderHovered : ImGuiCol_Header);
                    RenderFrame(frame_bb.Min, frame_bb.Max, bg_col, false, 0.0f);
                }
                RenderNavCursor(frame_bb, id, nav_render_cursor_flags, 0.0f);
                if (span_all_columns && !span_all_columns_label)
                    TablePopBackgroundChannel();
                if ((flags & ImGuiTreeNodeFlags_Bullet) != 0)
                    RenderBullet(window.DrawList, new ImVec2(text_pos.x - text_offset_x * 0.5f, text_pos.y + g.FontSize * 0.5f), text_col);
                else if (!is_leaf)
                    RenderArrow(window.DrawList, new ImVec2(text_pos.x - text_offset_x + padding.x, text_pos.y + g.FontSize * 0.15f), text_col, is_open ? ((flags & ImGuiTreeNodeFlags_UpsideDownArrow) != 0 ? ImGuiDir_Up : ImGuiDir_Down) : ImGuiDir_Right, 0.70f);
                if (g.LogEnabled)
                    LogSetNextTextDecoration(">", null);
            }

            if (draw_tree_lines)
                TreeNodeDrawLineToChildNode(new ImVec2(text_pos.x - text_offset_x + padding.x, text_pos.y + g.FontSize * 0.5f));

            // Label
            if (display_frame)
                RenderTextClipped(text_pos, frame_bb.Max, label, label_end, label_size);
            else
                RenderText(text_pos, label, label_end, false);

            if (span_all_columns_label)
                TablePopBackgroundChannel();
        }

        if (is_open && store_tree_node_stack_data)
            TreeNodeStoreStackData(flags, text_pos.x - text_offset_x); // Call before TreePushOverrideID()
        if (is_open && (flags & ImGuiTreeNodeFlags_NoTreePushOnOpen) == 0)
            TreePushOverrideID(id); // Could use TreePush(label) but this avoid computing twice

        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        return is_open;
    }

    // Draw horizontal line from our parent node
    // This is only called for visible child nodes so we are not too fussy anymore about performances
    // C++: void ImGui::TreeNodeDrawLineToChildNode(const ImVec2& target_pos)
    public static void TreeNodeDrawLineToChildNode(ImVec2 target_pos)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if ((window.DC.TreeHasStackDataDepthMask & TREE_NODE_GET_DEPTH_MASK(window.DC.TreeDepth - 1)) == 0)
            return;

        ref ImGuiTreeNodeStackData parent_data = ref g.TreeNodeStack.Data[g.TreeNodeStack.Size - 1];
        float x1 = ImMath.ImTrunc(parent_data.DrawLinesX1);
        float x2 = ImMath.ImTrunc(target_pos.x - g.Style.ItemInnerSpacing.x);
        float y = ImMath.ImTrunc(target_pos.y);
        float rounding = (g.Style.TreeLinesRounding > 0.0f) ? ImMath.ImMin(x2 - x1, g.Style.TreeLinesRounding) : 0.0f;
        parent_data.DrawLinesToNodesY2 = ImMath.ImMax(parent_data.DrawLinesToNodesY2, y - rounding);
        if (x1 >= x2)
            return;
        if (rounding > 0.0f)
        {
            x1 += 0.5f + rounding;
            window.DrawList.PathArcToFast(new ImVec2(x1, y - rounding), rounding, 6, 3);
            if (x1 < x2)
                window.DrawList.PathLineTo(new ImVec2(x2, y));
            window.DrawList.PathStroke(GetColorU32(ImGuiCol_TreeLines), g.Style.TreeLinesSize);
        }
        else
        {
            window.DrawList.AddLineH(x1, x2, y, GetColorU32(ImGuiCol_TreeLines), g.Style.TreeLinesSize);
        }
    }

    // Draw vertical line of the hierarchy
    // C++: void ImGui::TreeNodeDrawLineToTreePop(const ImGuiTreeNodeStackData* data)
    public static void TreeNodeDrawLineToTreePop(ImGuiTreeNodeStackData data)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        float y1 = ImMath.ImMax(data.NavRect.Max.y, window.ClipRect.Min.y);
        float y2 = data.DrawLinesToNodesY2;
        if ((data.TreeFlags & ImGuiTreeNodeFlags_DrawLinesFull) != 0)
        {
            float y2_full = window.DC.CursorPos.y;
            if (g.CurrentTable != null)
                y2_full = ImMath.ImMax(g.CurrentTable.RowPosY2, y2_full);
            y2_full = ImMath.ImTrunc(y2_full - g.Style.ItemSpacing.y - g.FontSize * 0.5f);
            if (y2 + (g.Style.ItemSpacing.y + g.Style.TreeLinesRounding) < y2_full) // FIXME: threshold to use ToNodes Y2 instead of Full Y2 when close by ItemSpacing.y
                y2 = y2_full;
        }
        y2 = ImMath.ImMin(y2, window.ClipRect.Max.y);
        if (y2 <= y1)
            return;
        float x = ImMath.ImTrunc(data.DrawLinesX1);
        if (data.DrawLinesTableColumn != -1)
            TablePushColumnChannel(data.DrawLinesTableColumn);
        window.DrawList.AddLineV(x, y1, y2, GetColorU32(ImGuiCol_TreeLines), g.Style.TreeLinesSize);
        if (data.DrawLinesTableColumn != -1)
            TablePopColumnChannel();
    }

    // C++: void ImGui::TreePush(const char* str_id)
    public static void TreePush(string str_id)
    {
        ImGuiWindow window = GetCurrentWindow();
        Indent();
        window.DC.TreeDepth++;
        PushID(str_id);
    }

    // C++: void ImGui::TreePush(const void* ptr_id)
    public static void TreePush(object ptr_id)
    {
        ImGuiWindow window = GetCurrentWindow();
        Indent();
        window.DC.TreeDepth++;
        PushID(ptr_id);
    }

    // C++: void ImGui::TreePushOverrideID(ImGuiID id)
    public static void TreePushOverrideID(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Indent();
        window.DC.TreeDepth++;
        PushOverrideID(id);
    }

    // C++: void ImGui::TreePop()
    public static void TreePop()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Unindent();

        window.DC.TreeDepth--;
        ImU32 tree_depth_mask = TREE_NODE_GET_DEPTH_MASK(window.DC.TreeDepth);
        if ((window.DC.TreeHasStackDataDepthMask & tree_depth_mask) != 0)
        {
            ref ImGuiTreeNodeStackData data = ref g.TreeNodeStack.Data[g.TreeNodeStack.Size - 1];
            Debug.Assert(data.ID == window.IDStack.back());

            // Handle Left arrow to move to parent tree node (when ImGuiTreeNodeFlags_NavLeftJumpsToParent is enabled)
            if ((data.TreeFlags & ImGuiTreeNodeFlags_NavLeftJumpsToParent) != 0)
                if (g.NavIdIsAlive && g.NavMoveDir == ImGuiDir_Left && g.NavWindow == window && NavMoveRequestButNoResultYet())
                    NavMoveRequestResolveWithPastTreeNode(ref g.NavMoveResultLocal, data);

            // Draw hierarchy lines
            if (data.DrawLinesX1 != float.MaxValue && window.DC.CursorPos.y >= window.ClipRect.Min.y)
                TreeNodeDrawLineToTreePop(data);

            g.TreeNodeStack.pop_back();
            window.DC.TreeHasStackDataDepthMask &= ~tree_depth_mask;
            window.DC.TreeRecordsClippedNodesY2Mask &= ~tree_depth_mask;
        }

        Debug.Assert(window.IDStack.Size > 1); // There should always be 1 element in the IDStack (pushed during window creation). If this triggers you called TreePop/PopID too much.
        PopID();
    }

    // Horizontal distance preceding label when using TreeNode() or Bullet()
    // C++: float ImGui::GetTreeNodeToLabelSpacing()
    public static float GetTreeNodeToLabelSpacing()
    {
        ImGuiContext g = GImGui;
        return g.FontSize + (g.Style.FramePadding.x * 2.0f);
    }

    // Set next TreeNode/CollapsingHeader open state.
    // C++: void ImGui::SetNextItemOpen(bool is_open, ImGuiCond cond = 0)
    public static void SetNextItemOpen(bool is_open, ImGuiCond cond = 0)
    {
        ImGuiContext g = GImGui;
        if (g.CurrentWindow.SkipItems)
            return;
        g.NextItemData.HasFlags |= ImGuiNextItemDataFlags_HasOpen;
        g.NextItemData.OpenVal = is_open;
        g.NextItemData.OpenCond = (byte)(cond != 0 ? cond : ImGuiCond_Always);
    }

    // Set next TreeNode/CollapsingHeader storage id.
    // C++: void ImGui::SetNextItemStorageID(ImGuiID storage_id)
    public static void SetNextItemStorageID(ImGuiID storage_id)
    {
        ImGuiContext g = GImGui;
        if (g.CurrentWindow.SkipItems)
            return;
        g.NextItemData.HasFlags |= ImGuiNextItemDataFlags_HasStorageID;
        g.NextItemData.StorageId = storage_id;
    }

    // CollapsingHeader returns true when opened but do not indent nor push into the ID stack (because of the ImGuiTreeNodeFlags_NoTreePushOnOpen flag).
    // This is basically the same as calling TreeNodeEx(label, ImGuiTreeNodeFlags_CollapsingHeader). You can remove the _NoTreePushOnOpen flag if you want behavior closer to normal TreeNode().
    // C++: bool ImGui::CollapsingHeader(const char* label, ImGuiTreeNodeFlags flags = 0)
    public static bool CollapsingHeader(string label, ImGuiTreeNodeFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;
        ImGuiID id = window.GetID(label);
        return TreeNodeBehavior(id, flags | ImGuiTreeNodeFlags_CollapsingHeader, label, -1);
    }

    // p_visible == NULL                        : regular collapsing header
    // p_visible != NULL && *p_visible == true  : show a small close button on the corner of the header, clicking the button will set *p_visible = false
    // p_visible != NULL && *p_visible == false : do not show the header at all
    // Do not mistake this with the Open state of the header itself, which you can adjust with SetNextItemOpen() or ImGuiTreeNodeFlags_DefaultOpen.
    // C++: bool ImGui::CollapsingHeader(const char* label, bool* p_visible = NULL, ImGuiTreeNodeFlags flags = 0)
    public static bool CollapsingHeader(string label, ref bool p_visible, ImGuiTreeNodeFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        if (!p_visible)
            return false;

        ImGuiID id = window.GetID(label);
        flags |= ImGuiTreeNodeFlags_CollapsingHeader;
        flags |= ImGuiTreeNodeFlags_AllowOverlap | (ImGuiTreeNodeFlags)ImGuiTreeNodeFlags_ClipLabelForTrailingButton;
        bool is_open = TreeNodeBehavior(id, flags, label, -1);
        {
            // Create a small overlapping close button
            // FIXME: We can evolve this into user accessible helpers to add extra buttons on title bars, headers, etc.
            // FIXME: CloseButton can overlap into text, need find a way to clip the text somehow.
            ImGuiContext g = GImGui;
            ImGuiLastItemData last_item_backup = g.LastItemData;
            float button_size = g.FontSize;
            float button_x = ImMath.ImMax(g.LastItemData.Rect.Min.x, g.LastItemData.Rect.Max.x - g.Style.FramePadding.x - button_size);
            float button_y = g.LastItemData.Rect.Min.y + g.Style.FramePadding.y;
            ImGuiID close_button_id = GetIDWithSeed("#CLOSE", null, id);
            if (CloseButton(close_button_id, new ImVec2(button_x, button_y)))
                p_visible = false;
            g.LastItemData = last_item_backup;
        }

        return is_open;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Selectable
    //-------------------------------------------------------------------------

    // Tip: pass a non-visible label (e.g. "##hello") then you can use the space to draw other text or image.
    // But you need to make sure the ID is unique, e.g. enclose calls in PushID/PopID or use ##unique_id.
    // With this scheme, ImGuiSelectableFlags_SpanAllColumns and ImGuiSelectableFlags_AllowOverlap are also frequently used flags.
    // FIXME: Selectable() with (size.x == 0.0f) and (SelectableTextAlign.x > 0.0f) followed by SameLine() is currently not supported.
    // C++: bool ImGui::Selectable(const char* label, bool selected = false, ImGuiSelectableFlags flags = 0, const ImVec2& size = ImVec2(0, 0))
    public static bool Selectable(string label, bool selected = false, ImGuiSelectableFlags flags = 0, ImVec2? size_arg = null)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImVec2 size_arg_v = size_arg ?? new ImVec2(0.0f, 0.0f);

        // Submit label or explicit size to ItemSize(), whereas ItemAdd() will submit a larger/spanning rectangle.
        ImGuiID id = window.GetID(label);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImVec2 size = new ImVec2(size_arg_v.x != 0.0f ? size_arg_v.x : label_size.x, size_arg_v.y != 0.0f ? size_arg_v.y : label_size.y);
        ImVec2 pos = window.DC.CursorPos;
        pos.y += window.DC.CurrLineTextBaseOffset;
        ItemSize(size, 0.0f);

        // Fill horizontal space
        // We don't support (size < 0.0f) in Selectable() because the ItemSpacing extension would make explicitly right-aligned sizes not visibly match other widgets.
        bool span_all_columns = (flags & ImGuiSelectableFlags_SpanAllColumns) != 0;
        float min_x = span_all_columns ? window.ParentWorkRect.Min.x : pos.x;
        float max_x = span_all_columns ? window.ParentWorkRect.Max.x : window.WorkRect.Max.x;
        if (size_arg_v.x == 0.0f || (flags & ImGuiSelectableFlags_SpanAvailWidth) != 0)
            size.x = ImMath.ImMax(label_size.x, max_x - min_x);

        // Selectables are meant to be tightly packed together with no click-gap, so we extend their box to cover spacing between selectable.
        // FIXME: Not part of layout so not included in clipper calculation, but ItemSize currently doesn't allow offsetting CursorPos.
        ImRect bb = new ImRect(min_x, pos.y, min_x + size.x, pos.y + size.y);
        if ((flags & ImGuiSelectableFlags_NoPadWithHalfSpacing) == 0)
        {
            float spacing_x = span_all_columns ? 0.0f : style.ItemSpacing.x;
            float spacing_y = style.ItemSpacing.y;
            float spacing_L = ImMath.ImTrunc(spacing_x * 0.50f);
            float spacing_U = ImMath.ImTrunc(spacing_y * 0.50f);
            bb.Min.x -= spacing_L;
            bb.Min.y -= spacing_U;
            bb.Max.x += (spacing_x - spacing_L);
            bb.Max.y += (spacing_y - spacing_U);
        }
        //if (g.IO.KeyCtrl) { GetForegroundDrawList()->AddRect(bb.Min, bb.Max, IM_COL32(0, 255, 0, 255)); }

        bool disabled_item = (flags & ImGuiSelectableFlags_Disabled) != 0;
        ImGuiItemFlags extra_item_flags = disabled_item ? (ImGuiItemFlags)ImGuiItemFlags_Disabled : ImGuiItemFlags_None;
        bool is_visible;
        if (span_all_columns)
        {
            // Modify ClipRect for the ItemAdd(), faster than doing a PushColumnsBackground/PushTableBackgroundChannel for every Selectable..
            float backup_clip_rect_min_x = window.ClipRect.Min.x;
            float backup_clip_rect_max_x = window.ClipRect.Max.x;
            window.ClipRect.Min.x = window.ParentWorkRect.Min.x;
            window.ClipRect.Max.x = window.ParentWorkRect.Max.x;
            is_visible = ItemAdd(bb, id, null, extra_item_flags);
            window.ClipRect.Min.x = backup_clip_rect_min_x;
            window.ClipRect.Max.x = backup_clip_rect_max_x;
        }
        else
        {
            is_visible = ItemAdd(bb, id, null, extra_item_flags);
        }

        bool is_multi_select = (g.LastItemData.ItemFlags & ImGuiItemFlags_IsMultiSelect) != 0;
        if (!is_visible)
            if (!is_multi_select || !g.BoxSelectState.UnclipMode || !g.BoxSelectState.UnclipRect.Overlaps(bb)) // Extra layer of "no logic clip" for box-select support (would be more overhead to add to ItemAdd)
                return false;

        bool disabled_global = (g.CurrentItemFlags & ImGuiItemFlags_Disabled) != 0;
        if (disabled_item && !disabled_global) // Only testing this as an optimization
            BeginDisabled(true);

        // FIXME: We can standardize the behavior of those two, we could also keep the fast path of override ClipRect + full push on render only,
        // which would be advantageous since most selectable are not selected.
        if (span_all_columns)
        {
            if (g.CurrentTable != null)
                TablePushBackgroundChannel();
            else if (window.DC.CurrentColumns != null)
                PushColumnsBackground();
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HasClipRect;
            g.LastItemData.ClipRect = window.ClipRect;
        }

        // We use NoHoldingActiveID on menus so user can click and _hold_ on a menu then drag to browse child entries
        ImGuiButtonFlags button_flags = 0;
        if ((flags & ImGuiSelectableFlags_NoHoldingActiveID) != 0) { button_flags |= ImGuiButtonFlags_NoHoldingActiveId; }
        if ((flags & ImGuiSelectableFlags_NoSetKeyOwner) != 0) { button_flags |= ImGuiButtonFlags_NoSetKeyOwner; }
        if ((flags & ImGuiSelectableFlags_SelectOnClick) != 0) { button_flags |= ImGuiButtonFlags_PressedOnClick; }
        if ((flags & ImGuiSelectableFlags_SelectOnRelease) != 0) { button_flags |= ImGuiButtonFlags_PressedOnRelease; }
        if ((flags & ImGuiSelectableFlags_AllowDoubleClick) != 0) { button_flags |= ImGuiButtonFlags_PressedOnClickRelease | ImGuiButtonFlags_PressedOnDoubleClick; }
        if ((flags & ImGuiSelectableFlags_AllowOverlap) != 0 || (g.LastItemData.ItemFlags & ImGuiItemFlags_AllowOverlap) != 0) { button_flags |= ImGuiButtonFlags_AllowOverlap; }

        // Multi-selection support (header)
        bool was_selected = selected;
        if (is_multi_select)
        {
            // Handle multi-select + alter button flags for it
            MultiSelectItemHeader(id, ref selected, ref button_flags);
        }

        bool hovered, held;
        bool pressed = ButtonBehavior(bb, id, out hovered, out held, button_flags);
        bool auto_selected = false;

        // Multi-selection support (footer)
        if (is_multi_select)
        {
            MultiSelectItemFooter(id, ref selected, ref pressed);
        }
        else
        {
            // Auto-select when moved into
            // - This will be more fully fleshed in the range-select branch
            // - This is not exposed as it won't nicely work with some user side handling of shift/control
            // - We cannot do 'if (g.NavJustMovedToId != id) { selected = false; pressed = was_selected; }' for two reasons
            //   - (1) it would require focus scope to be set, need exposing PushFocusScope() or equivalent (e.g. BeginSelection() calling PushFocusScope())
            //   - (2) usage will fail with clipped items
            //   The multi-select API aim to fix those issues, e.g. may be replaced with a BeginSelection() API.
            if ((flags & ImGuiSelectableFlags_SelectOnNav) != 0 && g.NavJustMovedToId != 0 && g.NavJustMovedToFocusScopeId == g.CurrentFocusScopeId)
                if (g.NavJustMovedToId == id && (g.NavJustMovedToKeyMods & ImGuiMod_Ctrl) == 0)
                    selected = pressed = auto_selected = true;
        }

        // Update NavId when clicking or when Hovering (this doesn't happen on most widgets), so navigation can be resumed with keyboard/gamepad
        if (pressed || (hovered && (flags & ImGuiSelectableFlags_SetNavIdOnHover) != 0))
        {
            if (!g.NavHighlightItemUnderNav && g.NavWindow == window && g.NavLayer == window.DC.NavLayerCurrent)
            {
                SetNavID(id, window.DC.NavLayerCurrent, g.CurrentFocusScopeId, WindowRectAbsToRel(window, bb)); // (bb == NavRect)
                if (g.IO.ConfigNavCursorVisibleAuto)
                    g.NavCursorVisible = false;
            }
        }
        if (pressed)
            MarkItemEdited(id);

        if (selected != was_selected)
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_ToggledSelection;
        if (g.ActiveId == id && g.ActiveIdIsJustActivated)
        {
            g.ActiveIdWasSelected = was_selected;
            g.ActiveIdWasSoleSelected = was_selected && (!is_multi_select || g.CurrentMultiSelect.IsSoleOrUnknownSelectionSize);
        }

        // Render
        if (is_visible)
        {
            bool highlighted = hovered || (flags & ImGuiSelectableFlags_Highlight) != 0;
            if (highlighted || selected)
            {
                // Between 1.91.0 and 1.91.4 we made selected Selectable use an arbitrary lerp between _Header and _HeaderHovered. Removed that now. (#8106)
                ImU32 col = GetColorU32((held && highlighted) ? ImGuiCol_HeaderActive : highlighted ? ImGuiCol_HeaderHovered : ImGuiCol_Header);
                RenderFrame(bb.Min, bb.Max, col, false, style.SelectableRounding);
            }
            if (g.NavId == id)
            {
                ImGuiNavRenderCursorFlags nav_render_cursor_flags = ImGuiNavRenderCursorFlags_Compact;
                if (is_multi_select)
                    nav_render_cursor_flags |= ImGuiNavRenderCursorFlags_AlwaysDraw; // Always show the nav rectangle
                RenderNavCursor(bb, id, nav_render_cursor_flags, style.SelectableRounding);
            }
        }

        if (span_all_columns)
        {
            if (g.CurrentTable != null)
                TablePopBackgroundChannel();
            else if (window.DC.CurrentColumns != null)
                PopColumnsBackground();
        }

        // Text stays at the submission position. Alignment/clipping extents ignore SpanAllColumns.
        if (is_visible)
            RenderTextClipped(pos, new ImVec2(ImMath.ImMin(pos.x + size.x, window.WorkRect.Max.x), pos.y + size.y), label, label_end, label_size, style.SelectableTextAlign, bb);

        // (IMGUI_DEBUG_BOXSELECT block omitted)

        // Automatically close popups
        if (pressed && !auto_selected && (window.Flags & ImGuiWindowFlags_Popup) != 0 && (flags & ImGuiSelectableFlags_NoAutoClosePopups) == 0 && (g.LastItemData.ItemFlags & ImGuiItemFlags_AutoClosePopups) != 0)
            CloseCurrentPopup();

        if (disabled_item && !disabled_global)
            EndDisabled();

        // Selectable() always returns a pressed state!
        // Users of BeginMultiSelect()/EndMultiSelect() scope: you may call ImGui::IsItemToggledSelection() to retrieve
        // selection toggle, only useful if you need that state updated (e.g. for rendering purpose) before reaching EndMultiSelect().
        // (IMGUI_TEST_ENGINE_ITEM_INFO omitted)
        return pressed; //-V1020
    }

    // C++: bool ImGui::Selectable(const char* label, bool* p_selected, ImGuiSelectableFlags flags = 0, const ImVec2& size = ImVec2(0, 0))
    public static bool Selectable(string label, ref bool p_selected, ImGuiSelectableFlags flags = 0, ImVec2? size_arg = null)
    {
        if (Selectable(label, p_selected, flags, size_arg))
        {
            p_selected = !p_selected;
            return true;
        }
        return false;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: Typing-Select support
    //-------------------------------------------------------------------------

    // C++ imgui_internal.h: static inline bool ImCharIsBlankW(unsigned int c)
    private static bool ImCharIsBlankW(uint c) => c == ' ' || c == '\t' || c == 0x3000;

    // C++ imgui_internal.h: #define ImToUpper(c) ((c) >= 'a' && (c) <= 'z' ? (c) - 'a' + 'A' : (c))
    private static char ImToUpper(char c) => (char)(c >= 'a' && c <= 'z' ? c - 'a' + 'A' : c);

    // [Experimental] Currently not exposed in public API.
    // Consume character inputs and return search request, if any.
    // This would typically only be called on the focused window or location you want to grab inputs for, e.g.
    //   if (ImGui.IsWindowFocused(...))
    //       if (ImGuiTypingSelectRequest req = ImGui.GetTypingSelectRequest())
    //           focus_idx = ImGui.TypingSelectFindMatch(req, my_items.Count, (ud, n) => my_items[n].Name, my_items, -1);
    // However the code is written in a way where calling it from multiple locations is safe (e.g. to obtain buffer).
    // C++: ImGuiTypingSelectRequest* ImGui::GetTypingSelectRequest(ImGuiTypingSelectFlags flags = 0)
    // [PORT] C++ stores a char[64] UTF-8 buffer; this port stores the equivalent search string directly
    // (lengths below are in chars rather than UTF-8 bytes for that reason).
    public static ImGuiTypingSelectRequest? GetTypingSelectRequest(ImGuiTypingSelectFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiTypingSelectState data = g.TypingSelectState;
        ref ImGuiTypingSelectRequest out_request = ref data.Request;

        // Clear buffer
        const float TYPING_SELECT_RESET_TIMER = 1.80f;          // FIXME: Potentially move to IO config.
        const int TYPING_SELECT_SINGLE_CHAR_COUNT_FOR_LOCK = 4; // Lock single char matching when repeating same char 4 times
        if (data.SearchBuffer.Length != 0) // C++: data->SearchBuffer[0] != 0
        {
            bool clear_buffer = false;
            clear_buffer |= (g.NavFocusScopeId != data.FocusScope);
            clear_buffer |= (data.LastRequestTime + TYPING_SELECT_RESET_TIMER < g.Time);
            clear_buffer |= g.NavAnyRequest;
            clear_buffer |= g.ActiveId != 0 && g.NavActivateId == 0; // Allow temporary SPACE activation to not interfere
            clear_buffer |= IsKeyPressed(ImGuiKey_Escape) || IsKeyPressed(ImGuiKey_Enter);
            clear_buffer |= IsKeyPressed(ImGuiKey_Backspace) && (flags & ImGuiTypingSelectFlags_AllowBackspace) == 0;
            //if (clear_buffer) { IMGUI_DEBUG_LOG("GetTypingSelectRequest(): Clear SearchBuffer.\n"); }
            if (clear_buffer)
                data.Clear();
        }

        // Append to buffer
        const int buffer_max_len = 64 - 1; // C++: IM_COUNTOF(data->SearchBuffer) - 1
        int buffer_len = data.SearchBuffer.Length; // C++: (int)ImStrlen(data->SearchBuffer)
        bool select_request = false;
        for (int q = 0; q < g.IO.InputQueueCharacters.Size; q++)
        {
            ImWchar w = g.IO.InputQueueCharacters[q];
            const int w_len = 1; // C++: ImTextCountUtf8BytesFromStr(&w, &w + 1); (one UTF-16 code unit in string storage)
            if (w < 32 || (buffer_len == 0 && ImCharIsBlankW(w)) || (buffer_len + w_len > buffer_max_len)) // Ignore leading blanks
                continue;
            string w_buf = ((char)w).ToString(); // C++: ImTextCharToUtf8(w_buf, (unsigned int)w)
            if (data.SingleCharModeLock && w_len == out_request.SingleCharSize && string.CompareOrdinal(w_buf, 0, data.SearchBuffer, 0, w_len) == 0) // C++: memcmp(w_buf, data->SearchBuffer, w_len) == 0
            {
                select_request = true; // Same character: don't need to append to buffer.
                continue;
            }
            if (data.SingleCharModeLock)
            {
                data.Clear(); // Different character: clear
                buffer_len = 0;
            }
            data.SearchBuffer += w_buf; // Append (C++: memcpy(data->SearchBuffer + buffer_len, w_buf, w_len + 1))
            buffer_len += w_len;
            select_request = true;
        }
        g.IO.InputQueueCharacters.resize(0);

        // Handle backspace
        if ((flags & ImGuiTypingSelectFlags_AllowBackspace) != 0 && IsKeyPressed(ImGuiKey_Backspace, ImGuiInputFlags_Repeat, 0))
        {
            // C++: char* p = (char*)(void*)ImTextFindPreviousUtf8Codepoint(data->SearchBuffer, data->SearchBuffer + buffer_len); *p = 0;
            // (remove previous codepoint: strip one UTF-16 code unit, two when part of a surrogate pair)
            int remove_len = 1;
            if (buffer_len >= 2 && char.IsLowSurrogate(data.SearchBuffer[buffer_len - 1]) && char.IsHighSurrogate(data.SearchBuffer[buffer_len - 2]))
                remove_len = 2;
            data.SearchBuffer = data.SearchBuffer.Substring(0, buffer_len - remove_len);
            buffer_len -= remove_len;
        }

        // Return request if any
        if (buffer_len == 0)
            return null;
        if (select_request)
        {
            data.FocusScope = g.NavFocusScopeId;
            data.LastRequestFrame = g.FrameCount;
            data.LastRequestTime = (float)g.Time;
        }
        out_request.Flags = flags;
        out_request.SearchBufferLen = buffer_len;
        out_request.SearchBuffer = data.SearchBuffer;
        out_request.SelectRequest = (data.LastRequestFrame == g.FrameCount);
        out_request.SingleCharMode = false;
        out_request.SingleCharSize = 0;

        // Calculate if buffer contains the same character repeated.
        // - This can be used to implement a special search mode on first character.
        // - Performed on UTF-8 codepoint for correctness.
        // - SingleCharMode is always set for first input character, because it usually leads to a "next".
        if ((flags & ImGuiTypingSelectFlags_AllowSingleCharMode) != 0)
        {
            string buf_begin = out_request.SearchBuffer;
            int buf_len = out_request.SearchBufferLen;
            int c0_len = (buf_len >= 2 && char.IsHighSurrogate(buf_begin[0]) && char.IsLowSurrogate(buf_begin[1])) ? 2 : 1; // C++: ImTextCountUtf8BytesFromChar(buf_begin, buf_end)
            int p;
            for (p = c0_len; p < buf_len; p += c0_len)
                if (string.CompareOrdinal(buf_begin, 0, buf_begin, p, c0_len) != 0) // C++: memcmp(buf_begin, p, (size_t)c0_len) != 0
                    break;
            int single_char_count = (p == buf_len) ? (buf_len / c0_len) : 0;
            out_request.SingleCharMode = (single_char_count > 0 || data.SingleCharModeLock);
            out_request.SingleCharSize = (sbyte)c0_len;
            data.SingleCharModeLock |= (single_char_count >= TYPING_SELECT_SINGLE_CHAR_COUNT_FOR_LOCK); // From now on we stop search matching to lock to single char mode.
        }

        return out_request;
    }

    // C++: static int ImStrimatchlen(const char* s1, const char* s1_end, const char* s2)
    private static int ImStrimatchlen(string s1, int s1_len, string s2)
    {
        int match_len = 0;
        while (match_len < s1_len && match_len < s2.Length && ImToUpper(s1[match_len]) == ImToUpper(s2[match_len]))
            match_len++;
        return match_len;
    }

    // Default handler for finding a result for typing-select. You may implement your own.
    // You might want to display a tooltip to visualize the current request SearchBuffer
    // When SingleCharMode is set:
    // - it is better to NOT display a tooltip of other on-screen display indicator.
    // - the index of the currently focused item is required.
    //   if your SetNextItemSelectionUserData() values are indices, you can obtain it from ImGuiMultiSelectIO::NavIdItem, otherwise from g.NavLastValidSelectionUserData.
    // C++: int ImGui::TypingSelectFindMatch(ImGuiTypingSelectRequest* req, int items_count, const char* (*get_item_name_func)(void*, int), void* user_data, int nav_item_idx = -1)
    public static int TypingSelectFindMatch(ImGuiTypingSelectRequest? req, int items_count, Func<object, int, string> get_item_name_func, object user_data, int nav_item_idx = -1)
    {
        if (req == null || req.GetValueOrDefault().SelectRequest == false) // Support NULL parameter so both calls can be done from same spot.
            return -1;
        int idx;
        if (req.GetValueOrDefault().SingleCharMode && (req.GetValueOrDefault().Flags & ImGuiTypingSelectFlags_AllowSingleCharMode) != 0)
            idx = TypingSelectFindNextSingleCharMatch(req.GetValueOrDefault(), items_count, get_item_name_func, user_data, nav_item_idx);
        else
            idx = TypingSelectFindBestLeadingMatch(req.GetValueOrDefault(), items_count, get_item_name_func, user_data);
        if (idx != -1)
            SetNavCursorVisibleAfterMove();
        return idx;
    }

    // Special handling when a single character is repeated: perform search on a single letter and goes to next.
    // C++: int ImGui::TypingSelectFindNextSingleCharMatch(ImGuiTypingSelectRequest* req, int items_count, const char* (*get_item_name_func)(void*, int), void* user_data, int nav_item_idx)
    public static int TypingSelectFindNextSingleCharMatch(ImGuiTypingSelectRequest req, int items_count, Func<object, int, string> get_item_name_func, object user_data, int nav_item_idx)
    {
        // FIXME: Assume selection user data is index. Would be extremely practical.
        //if (nav_item_idx == -1)
        //    nav_item_idx = (int)g.NavLastValidSelectionUserData;

        int first_match_idx = -1;
        bool return_next_match = false;
        for (int idx = 0; idx < items_count; idx++)
        {
            string item_name = get_item_name_func(user_data, idx);
            if (ImStrimatchlen(req.SearchBuffer, req.SingleCharSize, item_name) < req.SingleCharSize)
                continue;
            if (return_next_match)                           // Return next matching item after current item.
                return idx;
            if (first_match_idx == -1 && nav_item_idx == -1) // Return first match immediately if we don't have a nav_item_idx value.
                return idx;
            if (first_match_idx == -1)                       // Record first match for wrapping.
                first_match_idx = idx;
            if (nav_item_idx == idx)                         // Record that we encountering nav_item so we can return next match.
                return_next_match = true;
        }
        return first_match_idx; // First result
    }

    // C++: int ImGui::TypingSelectFindBestLeadingMatch(ImGuiTypingSelectRequest* req, int items_count, const char* (*get_item_name_func)(void*, int), void* user_data)
    public static int TypingSelectFindBestLeadingMatch(ImGuiTypingSelectRequest req, int items_count, Func<object, int, string> get_item_name_func, object user_data)
    {
        int longest_match_idx = -1;
        int longest_match_len = 0;
        for (int idx = 0; idx < items_count; idx++)
        {
            string item_name = get_item_name_func(user_data, idx);
            int match_len = ImStrimatchlen(req.SearchBuffer, req.SearchBufferLen, item_name);
            if (match_len <= longest_match_len)
                continue;
            longest_match_idx = idx;
            longest_match_len = match_len;
            if (match_len == req.SearchBufferLen)
                break;
        }
        return longest_match_idx;
    }

    // C++: void ImGui::DebugNodeTypingSelectState(ImGuiTypingSelectState* data)   (#ifndef IMGUI_DISABLE_DEBUG_TOOLS)
    public static void DebugNodeTypingSelectState(ImGuiTypingSelectState data)
    {
        Text("SearchBuffer = \"%s\"", data.SearchBuffer);
        Text("SingleCharMode = %d, Size = %d, Lock = %d", data.Request.SingleCharMode ? 1 : 0, (int)data.Request.SingleCharSize, data.SingleCharModeLock ? 1 : 0);
        Text("LastRequest = time: %.2f, frame: %d", data.LastRequestTime, data.LastRequestFrame);
    }
}
