// Port of Dear ImGui imgui_widgets.cpp lines 3600-5821:
//   [SECTION] Widgets: InputScalar, InputFloat, InputInt, etc.
//   [SECTION] Widgets: InputText, InputTextMultiline, InputTextWithHint
//
// Porting notes:
// - C++ `char* buf` input buffers are ported as zero-terminated byte[] buffers (UTF-8),
//   mirroring the established ImGuiInputTextState.TextA convention.
// - `const char*` values/pointers into text are ported as byte offsets into those buffers;
//   strings are decoded on demand for rendering/measurement via the font (string based) API.
// - The stb_textedit.h machinery (imstb_textedit.h + ImStb glue in imgui_widgets.cpp) lives in ImStbTextEdit.cs.
using System;
using System.Diagnostics;
using System.Text;

namespace ImGui;

// C++: typedef int (*ImGuiInputTextCallback)(ImGuiInputTextCallbackData* data);
public delegate int ImGuiInputTextCallback(ImGuiInputTextCallbackData data);

public static unsafe partial class ImGui
{
    //-------------------------------------------------------------------------
    // [SECTION] Widgets: InputScalar, InputFloat, InputInt, etc.
    //-------------------------------------------------------------------------

    // C++: const char* ImParseFormatFindStart(const char* fmt)
    // We don't use strchr() because our strings are usually very short and often start with '%'
    public static string ImParseFormatFindStart(string fmt)
    {
        int i = 0;
        while (i < fmt.Length)
        {
            char c = fmt[i];
            if (c == '%' && (i + 1 >= fmt.Length || fmt[i + 1] != '%'))
                return fmt.Substring(i);
            else if (c == '%')
                i++;
            i++;
        }
        return string.Empty; // C++ returns pointer to the zero terminator
    }

    // C++: const char* ImParseFormatFindEnd(const char* fmt)
    public static string ImParseFormatFindEnd(string fmt)
    {
        // Printf/scanf types modifiers: I/L/h/j/l/t/w/z. Other uppercase letters qualify as types aka end of the format.
        if (fmt.Length == 0 || fmt[0] != '%')
            return fmt;
        uint ignored_uppercase_mask = (1u << ('I' - 'A')) | (1u << ('L' - 'A'));
        uint ignored_lowercase_mask = (1u << ('h' - 'a')) | (1u << ('j' - 'a')) | (1u << ('l' - 'a')) | (1u << ('t' - 'a')) | (1u << ('w' - 'a')) | (1u << ('z' - 'a'));
        for (int i = 0; i < fmt.Length; i++)
        {
            char c = fmt[i];
            if (c >= 'A' && c <= 'Z' && ((1u << (c - 'A')) & ignored_uppercase_mask) == 0)
                return fmt.Substring(i + 1);
            if (c >= 'a' && c <= 'z' && ((1u << (c - 'a')) & ignored_lowercase_mask) == 0)
                return fmt.Substring(i + 1);
        }
        return string.Empty;
    }

    // C++: const char* ImParseFormatTrimDecorations(const char* fmt, char* buf, size_t buf_size)
    // Extract the format out of a format string with leading or trailing decorations
    //  fmt = "blah blah"  -> return ""
    //  fmt = "%.3f"       -> return "%.3f"
    //  fmt = "hello %.3f" -> return "%.3f"
    //  fmt = "%.3f hello" -> return "%.3f"
    // C# port note: there is no out-buffer; the extracted format is both assigned to 'buf' and returned.
    public static string ImParseFormatTrimDecorations(string fmt, out string buf)
    {
        string fmt_start = ImParseFormatFindStart(fmt);
        if (fmt_start.Length == 0 || fmt_start[0] != '%')
        {
            buf = string.Empty;
            return buf;
        }
        string fmt_end = ImParseFormatFindEnd(fmt_start);
        int len = fmt_start.Length - fmt_end.Length; // C++: fmt_end - fmt_start
        buf = fmt_start.Substring(0, len);
        return buf;
    }

    // C++: void ImParseFormatSanitizeForPrinting(const char* fmt_in, char* fmt_out, size_t fmt_out_size)
    // Sanitize format
    // - Zero terminate so extra characters after format (e.g. "%f123") don't confuse atof/atoi
    // - stb_sprintf.h supports several new modifiers which format numbers in a way that also makes them incompatible atof/atoi.
    public static string ImParseFormatSanitizeForPrinting(string fmt, out string buf)
    {
        string fmt_end = ImParseFormatFindEnd(fmt);
        int len = fmt.Length - fmt_end.Length;
        // IM_ASSERT((size_t)(fmt_end - fmt_in + 1) < fmt_out_size); // Format is too long, let us know if this happens to you!
        Debug.Assert(fmt.Length - len + 1 < 32 || len < fmt.Length); // (buf size checks are irrelevant with C# strings)
        var sb = new StringBuilder(len);
        for (int i = 0; i < len; i++)
        {
            char c = fmt[i];
            if (c != '\'' && c != '$' && c != '_') // Custom flags provided by stb_sprintf.h. POSIX 2008 also supports '.
                sb.Append(c);
        }
        buf = sb.ToString();
        return buf;
    }

    // C++: const char* ImParseFormatSanitizeForScanning(const char* fmt_in, char* fmt_out, size_t fmt_out_size)
    // - For scanning we need to remove all width and precision fields and flags "%+3.7f" -> "%f". BUT don't strip types like "%I64d" which includes digits. ! "%07I64d" -> "%I64d"
    public static string ImParseFormatSanitizeForScanning(string fmt_in, out string fmt_out)
    {
        string fmt_end = ImParseFormatFindEnd(fmt_in);
        int len = fmt_in.Length - fmt_end.Length;
        var sb = new StringBuilder(len);
        bool has_type = false;
        for (int i = 0; i < len; i++)
        {
            char c = fmt_in[i];
            if (!has_type && ((c >= '0' && c <= '9') || c == '.' || c == '+' || c == '#'))
                continue;
            has_type |= ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')); // Stop skipping digits
            if (c != '\'' && c != '$' && c != '_') // Custom flags provided by stb_sprintf.h. POSIX 2008 also supports '.
                sb.Append(c);
        }
        fmt_out = sb.ToString();
        return fmt_out;
    }

    // C++: int ImParseFormatPrecision(const char* fmt, int default_precision)
    // Parse display precision back from the display format string
    // FIXME: This is still used by some navigation code path to infer a minimum tweak step, but we should aim to rework widgets so it isn't needed.
    public static int ImParseFormatPrecision(string fmt, int default_precision)
    {
        fmt = ImParseFormatFindStart(fmt);
        if (fmt.Length == 0 || fmt[0] != '%')
            return default_precision;
        int i = 1;
        while (i < fmt.Length && fmt[i] >= '0' && fmt[i] <= '9')
            i++;
        int precision = int.MaxValue;
        if (i < fmt.Length && fmt[i] == '.')
        {
            // C++: fmt = ImAtoi<int>(fmt + 1, &precision);
            precision = 0;
            i++;
            while (i < fmt.Length && fmt[i] >= '0' && fmt[i] <= '9')
            {
                precision = (precision * 10) + (fmt[i] - '0');
                i++;
            }
            if (precision < 0 || precision > 99)
                precision = default_precision;
        }
        char c = i < fmt.Length ? fmt[i] : '\0';
        if (c == 'e' || c == 'E') // Maximum precision with scientific notation
            precision = -1;
        if ((c == 'g' || c == 'G') && precision == int.MaxValue)
            precision = -1;
        return (precision == int.MaxValue) ? default_precision : precision;
    }

    // C++: bool ImGui::TempInputText(const ImRect& bb, ImGuiID id, const char* label, char* buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data)
    // Create text input in place of another active widget (e.g. used when doing a Ctrl+Click on drag/slider widgets)
    // - This must be submitted right after the item it is overlaying.
    // FIXME: Facilitate using this in variety of other situations.
    public static bool TempInputText(ImRect bb, ImGuiID id, string label, byte[] buf, int buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback = null, object user_data = null)
    {
        // On the first frame, g.TempInputTextId == 0, then on subsequent frames it becomes == id.
        // We clear ActiveID on the first frame to allow the InputText() taking it back.
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        bool is_deactivated = (g.InputTextDeactivatedState.ID == id);
        bool is_active = (g.TempInputId == id);
        if (!is_active && !is_deactivated)
            ClearActiveID();

        ImVec2 backup_pos = window.DC.CursorPos;
        window.DC.CursorPos = bb.Min;
        g.LastItemData.ItemFlags |= ImGuiItemFlags_AllowDuplicateId; // Using ImGuiInputTextFlags_MergedItem above will skip ItemAdd() so we poke here.
        bool value_changed = InputTextEx(label, null, buf, buf_size, bb.GetSize(), flags | ImGuiInputTextFlags_TempInput | ImGuiInputTextFlags_AutoSelectAll, callback, user_data);
        KeepAliveID(id); // Not done because of ImGuiInputTextFlags_TempInput
        if (!is_active && !is_deactivated)
        {
            // First frame we started displaying the InputText widget, we expect it to take the active id.
            Debug.Assert(g.ActiveId == id);
            g.TempInputId = g.ActiveId;
        }
        if (is_active && g.ActiveId != id)
            g.TempInputId = 0;
        window.DC.CursorPos = backup_pos;
        return value_changed;
    }

    // C++ imgui_internal.h: inline bool ImGui::TempInputIsActive(ImGuiID id)
    public static bool TempInputIsActive(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        return (g.TempInputId == id && g.ActiveId == id) || (g.InputTextDeactivatedState.ID == id);
    }

    // C++: bool ImGui::TempInputScalar(const ImRect& bb, ImGuiID id, const char* label, ImGuiDataType data_type, void* p_data, const char* format, const void* p_clamp_min, const void* p_clamp_max)
    // Note that Drag/Slider functions are only forwarding the min/max values clamping values if the ImGuiSliderFlags_AlwaysClamp flag is set!
    // This is intended: this way we allow Ctrl+Click manual input to set a value out of bounds, for maximum flexibility.
    // However this may not be ideal for all uses, as some user code may break on out of bound values.
    public static bool TempInputScalar(ImRect bb, ImGuiID id, string label, ImGuiDataType data_type, IntPtr p_data, string format, IntPtr p_clamp_min = default, IntPtr p_clamp_max = default)
    {
        // FIXME: May need to clarify display behavior if format doesn't contain %.
        // "%d" -> "%d" / "There are %d items" -> "%d" / "items" -> "%d" (fallback). Also see #6405
        ImGuiContext g = GImGui;
        ImGuiDataTypeInfo type_info = DataTypeGetInfo(data_type);
        if (format == null)
            format = string.Empty;
        format = ImParseFormatTrimDecorations(format, out string fmt_buf);
        if (format.Length == 0)
            format = type_info.PrintFmt;
        string data_str = DataTypeFormatString(data_type, p_data, format);
        data_str = data_str.Trim(' ', '\t'); // C++: ImStrTrimBlanks(data_buf)
        byte[] data_buf = new byte[32];
        ImStrncpyBytes(data_buf, 0, data_str, data_buf.Length); // C++: char data_buf[32] with the formatted value

        ImGuiInputTextFlags flags = ImGuiInputTextFlags_AutoSelectAll | ImGuiInputTextFlags_LocalizeDecimalPoint;
        g.LastItemData.ItemFlags |= ImGuiItemFlags_NoMarkEdited; // Because TempInputText() uses ImGuiInputTextFlags_MergedItem it doesn't submit a new item, so we poke LastItemData.
        g.LastItemData.ItemFlags = ((g.LastItemData.ItemFlags & ImGuiItemFlags_LiveEditOnInputScalar) != 0) ? (g.LastItemData.ItemFlags | ImGuiItemFlags_LiveEditOnInputText) : (g.LastItemData.ItemFlags & ~ImGuiItemFlags_LiveEditOnInputText);
        if (!TempInputText(bb, id, label, data_buf, data_buf.Length, flags))
            return false;

        // Backup old value
        int data_type_size = type_info.Size;
        ulong data_backup = DataTypeStorageLoad(p_data, data_type_size);

        // Apply new value (or operations) then clamp
        DataTypeApplyFromText(BytesToString(data_buf, 0, StrlenBytes(data_buf, 0)), data_type, p_data, format, default);
        if (p_clamp_min != default || p_clamp_max != default)
        {
            if (p_clamp_min != default && p_clamp_max != default && DataTypeCompare(data_type, p_clamp_min, p_clamp_max) > 0)
                (p_clamp_min, p_clamp_max) = (p_clamp_max, p_clamp_min); // C++: ImSwap(p_clamp_min, p_clamp_max) (swaps the pointers)
            DataTypeClamp(data_type, p_data, p_clamp_min, p_clamp_max);
        }

        // Only mark as edited if new value is different
        g.LastItemData.ItemFlags &= ~ImGuiItemFlags_NoMarkEdited;
        bool value_changed = data_backup != DataTypeStorageLoad(p_data, data_type_size);
        if (value_changed)
            MarkItemEdited(id);
        return value_changed;
    }

    // C++: void ImGui::SetNextItemRefVal(ImGuiDataType data_type, void* p_data)
    public static void SetNextItemRefVal(ImGuiDataType data_type, IntPtr p_data)
    {
        ImGuiContext g = GImGui;
        g.NextItemData.HasFlags |= ImGuiNextItemDataFlags_HasRefVal;
        g.NextItemData.RefVal.Data = DataTypeStorageLoad(p_data, DataTypeGetInfo(data_type).Size);
    }

    // C++: bool ImGui::InputScalar(const char* label, ImGuiDataType data_type, void* p_data, const void* p_step, const void* p_step_fast, const char* format, ImGuiInputTextFlags flags)
    // Note: p_data, p_step, p_step_fast are _pointers_ to a memory address holding the data. For an Input widget, p_step and p_step_fast are optional.
    // Read code of e.g. InputFloat(), InputInt() etc. or examples in 'Demo->Widgets-> Data Types' to understand how to use this function directly.
    public static bool InputScalar(string label, ImGuiDataType data_type, IntPtr p_data, IntPtr p_step = default, IntPtr p_step_fast = default, string format = null, ImGuiInputTextFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        //IM_ASSERT((flags & ImGuiInputTextFlags_EnterReturnsTrue) == 0); // Not supported by InputScalar(). Please open an issue if you this would be useful to you. Otherwise use IsItemDeactivatedAfterEdit()!

        if (format == null)
            format = DataTypeGetInfo(data_type).PrintFmt;

        // C++: void* p_data_default = (g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasRefVal) ? &g.NextItemData.RefVal : &g.DataTypeZeroValue;
        ulong default_val = ((g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasRefVal) != 0) ? g.NextItemData.RefVal.Data : g.DataTypeZeroValue.Data;

        byte[] buf = new byte[64];
        {
            IntPtr p_data_default = (IntPtr)(&default_val);
            if ((flags & ImGuiInputTextFlags_DisplayEmptyRefVal) != 0 && DataTypeCompare(data_type, p_data, p_data_default) == 0)
                buf[0] = 0;
            else
                ImStrncpyBytes(buf, 0, DataTypeFormatString(data_type, p_data, format), buf.Length);

            // Disable the MarkItemEdited() call in InputText but keep ImGuiItemStatusFlags_Edited.
            // We call MarkItemEdited() ourselves by comparing the actual data rather than the string.
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_NoMarkEdited;
            flags |= ImGuiInputTextFlags_AutoSelectAll | ImGuiInputTextFlags_LocalizeDecimalPoint;

            bool has_step_buttons = p_step != default;
            float button_size = has_step_buttons ? GetFrameHeight() : 0.0f;
            bool ret;
            if (has_step_buttons)
            {
                // With Step Buttons
                BeginGroup(); // The only purpose of the group here is to allow the caller to query item data e.g. IsItemActive()
                PushID(label);
                SetNextItemWidth(ImMath.ImMax(1.0f, CalcItemWidth() - (button_size + style.ItemInnerSpacing.x) * 2));
                ret = InputText("", buf, buf.Length, flags); // PushID(label) + "" gives us the expected ID from outside point of view
                // IMGUI_TEST_ENGINE_ITEM_INFO(g.LastItemData.ID, label, g.LastItemData.StatusFlags | ImGuiItemStatusFlags_Inputable); // (omitted)
            }
            else
            {
                // Without Step Buttons
                ret = InputText(label, buf, buf.Length, flags);
            }

            // Apply
            bool value_changed = false;
            if ((g.LastItemData.ItemFlags & ImGuiItemFlags_LiveEditOnInputScalar) != 0)
            {
                bool input_edited = (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_EditedInternal) != 0; // We would be using 'ret' if ImGuiInputTextFlags_EnterReturnsTrue was not involved.
                if (input_edited)
                    value_changed = DataTypeApplyFromText(BytesToString(buf, 0, StrlenBytes(buf, 0)), data_type, p_data, format, (flags & ImGuiInputTextFlags_ParseEmptyRefVal) != 0 ? p_data_default : default);
            }
            else
            {
                //g.LastItemData.StatusFlags &= ~ImGuiItemStatusFlags_Edited;
                if (g.DeactivatedItemData.ID == g.LastItemData.ID)
                {
                    //g.DeactivatedItemData.HasBeenEditedBefore = false; // Will be set below by MarkItemEdited()
                    //if (IsItemDeactivated()) // Should be unnecessary
                    value_changed = DataTypeApplyFromText(BytesToString(buf, 0, StrlenBytes(buf, 0)), data_type, p_data, format, (flags & ImGuiInputTextFlags_ParseEmptyRefVal) != 0 ? p_data_default : default);
                }
            }

            // Step buttons
            if (has_step_buttons)
            {
                ImVec2 backup_frame_padding = style.FramePadding;
                style.FramePadding.x = style.FramePadding.y;
                if ((flags & ImGuiInputTextFlags_ReadOnly) != 0)
                    BeginDisabled(true);
                PushItemFlag(ImGuiItemFlags_ButtonRepeat, true);
                SameLine(0, style.ItemInnerSpacing.x);
                if (ButtonEx("-", new ImVec2(button_size, button_size)))
                {
                    DataTypeApplyOp(data_type, '-', p_data, p_data, (g.IO.KeyCtrl && p_step_fast != default) ? p_step_fast : p_step);
                    value_changed = ret = true;
                }
                SameLine(0, style.ItemInnerSpacing.x);
                if (ButtonEx("+", new ImVec2(button_size, button_size)))
                {
                    DataTypeApplyOp(data_type, '+', p_data, p_data, (g.IO.KeyCtrl && p_step_fast != default) ? p_step_fast : p_step);
                    value_changed = ret = true;
                }
                PopItemFlag();
                if ((flags & ImGuiInputTextFlags_ReadOnly) != 0)
                    EndDisabled();

                int label_end = FindRenderedTextEnd(label);
                if (label_end != 0) // C++: if (label != label_end)
                {
                    SameLine(0, style.ItemInnerSpacing.x);
                    TextEx(label, label_end);
                }
                style.FramePadding = backup_frame_padding;

                PopID();
                EndGroup();
            }

            g.LastItemData.ItemFlags &= ~ImGuiItemFlags_NoMarkEdited;
            if (value_changed)
                MarkItemEdited(g.LastItemData.ID);

            if ((flags & ImGuiInputTextFlags_EnterReturnsTrue) != 0)
                return ret;
            return value_changed;
        }
    }

    // C++: bool ImGui::InputScalarN(const char* label, ImGuiDataType data_type, void* p_data, int components, const void* p_step, const void* p_step_fast, const char* format, ImGuiInputTextFlags flags)
    public static bool InputScalarN(string label, ImGuiDataType data_type, IntPtr p_data, int components, IntPtr p_step = default, IntPtr p_step_fast = default, string format = null, ImGuiInputTextFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        bool value_changed = false;
        BeginGroup();
        PushID(label);
        PushMultiItemsWidths(components, CalcItemWidth());
        int type_size = GDataTypesInfo[data_type].Size;
        for (int i = 0; i < components; i++)
        {
            PushID(i);
            if (i > 0)
                SameLine(0, g.Style.ItemInnerSpacing.x);
            value_changed |= InputScalar("", data_type, p_data, p_step, p_step_fast, format, flags);
            PopID();
            PopItemWidth();
            p_data = (IntPtr)((byte*)p_data + type_size);
        }
        PopID();

        int label_end = FindRenderedTextEnd(label);
        if (label_end != 0) // C++: if (label != label_end)
        {
            SameLine(0.0f, g.Style.ItemInnerSpacing.x);
            TextEx(label, label_end);
        }

        EndGroup();
        return value_changed;
    }

    public static bool InputFloat(string label, ref float v, float step = 0.0f, float step_fast = 0.0f, string format = null, ImGuiInputTextFlags flags = 0)
    {
        fixed (float* pv = &v)
        {
            return InputScalar(label, ImGuiDataType_Float, (IntPtr)pv, step > 0.0f ? (IntPtr)(&step) : default, step_fast > 0.0f ? (IntPtr)(&step_fast) : default, format, flags);
        }
    }

    public static bool InputFloat2(string label, float[] v, string format = null, ImGuiInputTextFlags flags = 0)
    {
        fixed (float* pv = v)
            return InputScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 2, default, default, format, flags);
    }

    public static bool InputFloat3(string label, float[] v, string format = null, ImGuiInputTextFlags flags = 0)
    {
        fixed (float* pv = v)
            return InputScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 3, default, default, format, flags);
    }

    public static bool InputFloat4(string label, float[] v, string format = null, ImGuiInputTextFlags flags = 0)
    {
        fixed (float* pv = v)
            return InputScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 4, default, default, format, flags);
    }

    public static bool InputInt(string label, ref int v, int step = 1, int step_fast = 100, ImGuiInputTextFlags flags = 0)
    {
        // Hexadecimal input provided as a convenience but the flag name is awkward. Typically you'd use InputText() to parse your own data, if you want to handle prefixes.
        string format = (flags & ImGuiInputTextFlags_CharsHexadecimal) != 0 ? "%08X" : "%d";
        fixed (int* pv = &v)
        {
            return InputScalar(label, ImGuiDataType_S32, (IntPtr)pv, step > 0 ? (IntPtr)(&step) : default, step_fast > 0 ? (IntPtr)(&step_fast) : default, format, flags);
        }
    }

    public static bool InputInt2(string label, int[] v, ImGuiInputTextFlags flags = 0)
    {
        fixed (int* pv = v)
            return InputScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 2, default, default, "%d", flags);
    }

    public static bool InputInt3(string label, int[] v, ImGuiInputTextFlags flags = 0)
    {
        fixed (int* pv = v)
            return InputScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 3, default, default, "%d", flags);
    }

    public static bool InputInt4(string label, int[] v, ImGuiInputTextFlags flags = 0)
    {
        fixed (int* pv = v)
            return InputScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 4, default, default, "%d", flags);
    }

    public static bool InputDouble(string label, ref double v, double step = 0.0, double step_fast = 0.0, string format = null, ImGuiInputTextFlags flags = 0)
    {
        fixed (double* pv = &v)
        {
            return InputScalar(label, ImGuiDataType_Double, (IntPtr)pv, step > 0.0 ? (IntPtr)(&step) : default, step_fast > 0.0 ? (IntPtr)(&step_fast) : default, format, flags);
        }
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: InputText, InputTextMultiline, InputTextWithHint
    //-------------------------------------------------------------------------

    // C++: bool ImGui::InputText(const char* label, char* buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data)
    // If you want to use InputText() with a dynamic string type, use the buf + buf_size convention (byte[] is zero-terminated like the C char* buffer).
    public static bool InputText(string label, byte[] buf, int buf_size, ImGuiInputTextFlags flags = 0, ImGuiInputTextCallback callback = null, object user_data = null)
    {
        Debug.Assert((flags & ImGuiInputTextFlags_Multiline) == 0); // call InputTextMultiline()
        return InputTextEx(label, null, buf, buf_size, new ImVec2(0, 0), flags, callback, user_data);
    }

    // C# convenience overload wrapping a managed string value.
    public static bool InputText(string label, ref string buf, int buf_size, ImGuiInputTextFlags flags = 0, ImGuiInputTextCallback callback = null, object user_data = null)
    {
        Debug.Assert((flags & ImGuiInputTextFlags_Multiline) == 0); // call InputTextMultiline()
        byte[] bytes = new byte[Math.Max(buf_size, buf != null ? Encoding.UTF8.GetByteCount(buf) + 1 : 1)];
        ImStrncpyBytes(bytes, 0, buf ?? "", bytes.Length);
        bool ret = InputTextEx(label, null, bytes, buf_size, new ImVec2(0, 0), flags, callback, user_data);
        buf = BytesToString(bytes, 0, StrlenBytes(bytes, 0));
        return ret;
    }

    public static bool InputTextMultiline(string label, byte[] buf, int buf_size, ImVec2 size = default, ImGuiInputTextFlags flags = 0, ImGuiInputTextCallback callback = null, object user_data = null)
    {
        return InputTextEx(label, null, buf, buf_size, size, flags | ImGuiInputTextFlags_Multiline, callback, user_data);
    }

    public static bool InputTextWithHint(string label, string hint, byte[] buf, int buf_size, ImGuiInputTextFlags flags = 0, ImGuiInputTextCallback callback = null, object user_data = null)
    {
        Debug.Assert((flags & ImGuiInputTextFlags_Multiline) == 0); // call InputTextMultiline() or InputTextEx() manually if you need multi-line + hint.
        return InputTextEx(label, hint, buf, buf_size, new ImVec2(0, 0), flags, callback, user_data);
    }

    // C# convenience overload wrapping a managed string value.
    public static bool InputTextWithHint(string label, string hint, ref string buf, int buf_size, ImGuiInputTextFlags flags = 0, ImGuiInputTextCallback callback = null, object user_data = null)
    {
        Debug.Assert((flags & ImGuiInputTextFlags_Multiline) == 0); // call InputTextMultiline() or InputTextEx() manually if you need multi-line + hint.
        byte[] bytes = new byte[Math.Max(buf_size, buf != null ? Encoding.UTF8.GetByteCount(buf) + 1 : 1)];
        ImStrncpyBytes(bytes, 0, buf ?? "", bytes.Length);
        bool ret = InputTextEx(label, hint, bytes, buf_size, new ImVec2(0, 0), flags, callback, user_data);
        buf = BytesToString(bytes, 0, StrlenBytes(bytes, 0));
        return ret;
    }

    // C++: static ImVec2 InputTextCalcTextSize(ImGuiContext* ctx, const char* text_begin, const char* text_end_display, const char* text_end, const char** out_remaining, ImVec2* out_offset, ImDrawTextFlags flags)
    // C# port: text pointers become byte offsets into 'text'. 'out_remaining' is a byte offset relative to text_begin.
    internal static ImVec2 InputTextCalcTextSize(byte[] text, int text_begin, int text_end_display, int text_end, out int out_remaining, ImVec2[] out_offset, int flags)
    {
        ImGuiContext g = GImGui;
        ImGuiInputTextState obj = g.InputTextState;
        Debug.Assert(text_end_display >= text_begin && text_end_display <= text_end);
        int[] map = BuildByteToUtf16Map(text, text_begin, text_end);
        string s = Encoding.UTF8.GetString(text, text_begin, text_end - text_begin);
        int end_display_u16 = map[text_end_display - text_begin];
        Debug.Assert(end_display_u16 <= s.Length); // Map counts must never exceed the decoded string (ImTextCharFromUtf8 must match Encoding.UTF8)
        ImVec2 size = ImFontAtlas.ImFontCalcTextSizeEx(g.Font, g.FontSize, float.MaxValue, obj.WrapWidth, s, 0, end_display_u16, s.Length, out int remaining_u16, out_offset, flags);
        // Convert the remaining UTF-16 index back to a byte offset (C++ keeps a pointer into the original buffer)
        out_remaining = text_end - text_begin;
        if (remaining_u16 < s.Length)
            for (int i = 0; i < map.Length; i++)
                if (map[i] >= remaining_u16)
                {
                    out_remaining = i;
                    break;
                }
        return size;
    }

    // C# helper: build a map from byte offset (relative to range start) to UTF-16 index for [start,end).
    // Entry i = UTF-16 index of the codepoint starting at byte i; entry[len] = total UTF-16 length.
    internal static int[] BuildByteToUtf16Map(byte[] data, int start, int end)
    {
        int len = end - start;
        int[] map = new int[len + 1];
        int bi = 0, ui = 0;
        while (bi < len)
        {
            map[bi] = ui;
            int c_len = ImText.ImTextCharFromUtf8(out uint c, data, start + bi, end);
            if (c_len <= 0)
                c_len = 1;
            ui += (c >= 0x10000) ? 2 : 1;
            bi += c_len;
        }
        map[len] = ui;
        return map;
    }

    // C# helper: C++ ImFontCalcWordWrapPositionEx() applied to a UTF-8 byte buffer range.
    // Returns a byte offset within [s,text_end) (end-of-line position).
    internal static int ImFontCalcWordWrapPositionExBytes(byte[] buf, int s, int text_end, float wrap_width, int flags)
    {
        if (s >= text_end)
            return text_end;
        ImGuiContext g = GImGui;
        int[] map = BuildByteToUtf16Map(buf, s, text_end);
        string str = Encoding.UTF8.GetString(buf, s, text_end - s);
        int eol_u16 = ImFontAtlas.ImFontCalcWordWrapPositionEx(g.Font, g.FontSize, str, 0, str.Length, wrap_width, flags);
        int byte_off = text_end - s;
        for (int i = 0; i < map.Length; i++)
            if (map[i] >= eol_u16)
            {
                byte_off = i;
                break;
            }
        return s + byte_off;
    }

    // C++: static bool InputTextFilterCharacter(ImGuiContext* ctx, ImGuiInputTextState* state, unsigned int* p_char, ImGuiInputTextCallback callback, void* user_data, bool input_source_is_clipboard)
    // Return false to discard a character.
    private static bool InputTextFilterCharacter(ImGuiContext ctx, ImGuiInputTextState state, ref uint p_char, ImGuiInputTextCallback callback, object user_data, bool input_source_is_clipboard = false)
    {
        Debug.Assert(state != null);
        uint c = p_char;
        ImGuiInputTextFlags flags = state.Flags;

        // Filter non-printable (NB: isprint is unreliable! see #2467)
        bool apply_named_filters = true;
        if (c < 0x20)
        {
            bool pass = false;
            pass |= (c == '\n') && (flags & ImGuiInputTextFlags_Multiline) != 0;    // Note that an Enter KEY will emit \r and be ignored (we poll for KEY in InputText() code)
            if (c == '\n' && input_source_is_clipboard && (flags & ImGuiInputTextFlags_Multiline) == 0) // In single line mode, replace \n with a space
            {
                c = ' ';
                pass = true;
            }
            pass |= (c == '\n') && (flags & ImGuiInputTextFlags_Multiline) != 0;
            pass |= (c == '\t') && (flags & ImGuiInputTextFlags_AllowTabInput) != 0;
            if (!pass)
                return false;
            apply_named_filters = false; // Override named filters below so newline and tabs can still be inserted.
        }

        if (input_source_is_clipboard == false)
        {
            // We ignore Ascii representation of delete (emitted from Backspace on OSX, see #2578, #2817)
            if (c == 127)
                return false;

            // Filter private Unicode range. GLFW on OSX seems to send private characters for special keys like arrow keys (FIXME)
            if (c >= 0xE000 && c <= 0xF8FF)
                return false;
        }

        // Filter Unicode ranges we are not handling in this build
        if (c > ImUtil.IM_UNICODE_CODEPOINT_MAX)
            return false;

        // Generic named filters
        if (apply_named_filters && (flags & (ImGuiInputTextFlags_CharsDecimal | ImGuiInputTextFlags_CharsHexadecimal | ImGuiInputTextFlags_CharsUppercase | ImGuiInputTextFlags_CharsNoBlank | ImGuiInputTextFlags_CharsScientific | ImGuiInputTextFlags_LocalizeDecimalPoint)) != 0)
        {
            // The libc allows overriding locale, with e.g. 'setlocale(LC_NUMERIC, "de_DE.UTF-8");' which affect the output/input of printf/scanf to use e.g. ',' instead of '.'.
            // The standard mandate that programs starts in the "C" locale where the decimal point is '.'.
            // We don't really intend to provide widespread support for it, but out of empathy for people stuck with using odd API, we support the bare minimum aka overriding the decimal point.
            // Change the default decimal_point with:
            //   ImGui::GetPlatformIO()->Platform_LocaleDecimalPoint = *localeconv()->decimal_point;
            // Users of non-default decimal point (in particular ',') may be affected by word-selection logic (is_word_boundary_from_right/is_word_boundary_from_left) functions.
            ImGuiContext g = ctx;
            uint c_decimal_point = g.PlatformIO.Platform_LocaleDecimalPoint;
            if ((flags & (ImGuiInputTextFlags_CharsDecimal | ImGuiInputTextFlags_CharsScientific | ImGuiInputTextFlags_LocalizeDecimalPoint)) != 0)
                if (c == '.' || c == ',')
                    c = c_decimal_point;

            // Full-width -> half-width conversion for numeric fields: https://en.wikipedia.org/wiki/Halfwidth_and_Fullwidth_Forms_(Unicode_block)
            // While this is mostly convenient, this has the side-effect for uninformed users accidentally inputting full-width characters that they may
            // scratch their head as to why it works in numerical fields vs in generic text fields it would require support in the font.
            if ((flags & (ImGuiInputTextFlags_CharsDecimal | ImGuiInputTextFlags_CharsScientific | ImGuiInputTextFlags_CharsHexadecimal)) != 0)
                if (c >= 0xFF01 && c <= 0xFF5E)
                    c = c - 0xFF01 + 0x21;

            // Allow 0-9 . - + * /
            if ((flags & ImGuiInputTextFlags_CharsDecimal) != 0)
                if (!(c >= '0' && c <= '9') && (c != c_decimal_point) && (c != '-') && (c != '+') && (c != '*') && (c != '/'))
                    return false;

            // Allow 0-9 . - + * / e E
            if ((flags & ImGuiInputTextFlags_CharsScientific) != 0)
                if (!(c >= '0' && c <= '9') && (c != c_decimal_point) && (c != '-') && (c != '+') && (c != '*') && (c != '/') && (c != 'e') && (c != 'E'))
                    return false;

            // Allow 0-9 a-F A-F
            if ((flags & ImGuiInputTextFlags_CharsHexadecimal) != 0)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F'))
                    return false;

            // Turn a-z into A-Z
            if ((flags & ImGuiInputTextFlags_CharsUppercase) != 0)
                if (c >= 'a' && c <= 'z')
                    c = c - (uint)'a' + (uint)'A'; // C++: c += (unsigned int)('A' - 'a')

            if ((flags & ImGuiInputTextFlags_CharsNoBlank) != 0)
                if (ImUtil.ImCharIsBlankW(c))
                    return false;

            p_char = c;
        }

        // Custom callback filter
        if ((flags & ImGuiInputTextFlags_CallbackCharFilter) != 0)
        {
            ImGuiContext g = GImGui;
            ImGuiInputTextCallbackData callback_data = new ImGuiInputTextCallbackData();
            callback_data.Ctx = g;
            callback_data.ID = state.ID;
            callback_data.Flags = flags;
            callback_data.EventFlag = ImGuiInputTextFlags_CallbackCharFilter;
            callback_data.EventChar = (ImWchar)c;
            callback_data.EventActivated = (g.ActiveId == state.ID && g.ActiveIdIsJustActivated);
            callback_data.CursorPos = state.Stb.cursor;
            callback_data.SelectionStart = state.Stb.select_start;
            callback_data.SelectionEnd = state.Stb.select_end;
            callback_data.UserData = user_data;
            if (callback(callback_data) != 0)
                return false;
            p_char = callback_data.EventChar;
            if (callback_data.EventChar == 0)
                return false;
        }

        return true;
    }

    // C++: static void InputTextReconcileUndoState(ImGuiInputTextState* state, const char* old_buf, int old_length, const char* new_buf, int new_length)
    // Find the shortest single replacement we can make to get from old_buf to new_buf
    // Note that this doesn't directly alter state->TextA, state->TextLen. They are expected to be made valid separately.
    // FIXME: Ideally we should transition toward (1) making InsertChars()/DeleteChars() update undo-stack (2) discourage (and keep reconcile) or obsolete (and remove reconcile) accessing buffer directly.
    private static void InputTextReconcileUndoState(ImGuiInputTextState state, byte[] old_buf, int old_length, byte[] new_buf, int new_length)
    {
        int shorter_length = ImMath.ImMin(old_length, new_length);
        int first_diff;
        for (first_diff = 0; first_diff < shorter_length; first_diff++)
            if (old_buf[first_diff] != new_buf[first_diff])
                break;
        if (first_diff == old_length && first_diff == new_length)
            return;

        int old_last_diff = old_length - 1;
        int new_last_diff = new_length - 1;
        for (; old_last_diff >= first_diff && new_last_diff >= first_diff; old_last_diff--, new_last_diff--)
            if (old_buf[old_last_diff] != new_buf[new_last_diff])
                break;

        int insert_len = new_last_diff - first_diff + 1;
        int delete_len = old_last_diff - first_diff + 1;
        if (insert_len > 0 || delete_len > 0)
        {
            int p = ImStbTextEdit.StbTextCreateundo(state.Stb, first_diff, delete_len, insert_len); // C++: stb_text_createundo()
            if (p >= 0)
                for (int i = 0; i < delete_len; i++)
                    state.Stb.undo_char[p + i] = old_buf[first_diff + i];
        }
    }

    // C++: void ImGui::InputTextDeactivateHook(ImGuiID id)
    // As InputText() retain textual data and we currently provide a path for user to not retain it (via local variables)
    // we need some form of hook to reapply data back to user buffer on deactivation frame. (#4714)
    // It would be more desirable that we discourage users from taking advantage of the "user not retaining data" trick,
    // but that more likely be attractive when we do have _NoLiveEdit flag available.
    public static void InputTextDeactivateHook(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        ImGuiInputTextState state = g.InputTextState;
        if (id == 0 || state.ID != id || g.ActiveId != id)
            return;
        if (!state.EditedBefore)
            return;
        //IMGUI_DEBUG_LOG_ACTIVEID("InputTextDeactivateHook() id = 0x%08X\n", id);
        state.EditedBefore = false;
        g.InputTextDeactivatedState.ID = state.ID;
        g.InputTextDeactivatedState.ElapseFrame = g.FrameCount + 1;
        if ((state.Flags & ImGuiInputTextFlags_ReadOnly) != 0)
        {
            g.InputTextDeactivatedState.TextA.resize(0); // In theory this data won't be used, but clear to be neat.
        }
        else
        {
            Debug.Assert(state.TextA.Data != null);
            Debug.Assert(state.TextA[state.TextLen] == 0);
            g.InputTextDeactivatedState.TextA.resize(state.TextLen + 1);
            Array.Copy(state.TextA.Data, g.InputTextDeactivatedState.TextA.Data, state.TextLen + 1);
        }
    }

    // C++ imgui_internal.h: inline ImGuiInputTextState* ImGui::GetInputTextState(ImGuiID id)
    // Get input text state if active
    public static ImGuiInputTextState GetInputTextState(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        return (id != 0 && g.InputTextState.ID == id) ? g.InputTextState : null;
    }

    // C++: static int* ImLowerBound(int* in_begin, int* in_end, int v)
    // C# port: binary search over ImVector<int> index range [begin,end). Returns the first index with value >= v.
    private static int ImLowerBound(ImVector<int> v, int begin, int end, int val)
    {
        int p = begin;
        int count = end - p;
        while (count > 0)
        {
            int count2 = count >> 1;
            int mid = p + count2;
            if (v[mid] < val)
            {
                p = mid + 1;
                count -= count2 + 1;
            }
            else
            {
                count = count2;
            }
        }
        return p;
    }

    // C++: static int InputTextLineIndexBuild(ImGuiInputTextFlags flags, ImGuiTextIndex* line_index, const char* buf, const char* buf_end, float wrap_width, int max_output_buffer_size, const char** out_buf_end)
    // C# port: 'buf_end' is a byte offset, or -1 to mean NULL (unknown end; inactive path). 'out_buf_end' receives the scan end (only meaningful to the caller when it passed buf_end == -1).
    // FIXME-WORDWRAP: Bundle some of this into ImGuiTextIndex and/or extract as a different tool?
    // 'max_output_buffer_size' happens to be a meaningful optimization to avoid writing the full line_index when not necessarily needed (e.g. very large buffer, scrolled up, inactive)
    private static int InputTextLineIndexBuild(ImGuiInputTextFlags flags, ImGuiTextIndex line_index, byte[] buf, int buf_end, float wrap_width, int max_output_buffer_size, out int out_buf_end)
    {
        ImGuiContext g = GImGui;
        int size = 0;
        int s = 0;
        bool trailing_line_already_counted = false;
        if ((flags & ImGuiInputTextFlags_WordWrap) != 0)
        {
            for (s = 0; s < buf_end; s = (buf[s] == '\n') ? s + 1 : s)
            {
                if (size++ <= max_output_buffer_size)
                    line_index.Offsets.push_back(s);
                s = ImFontCalcWordWrapPositionExBytes(buf, s, buf_end, wrap_width, ImDrawTextFlags_WrapKeepBlanks);
            }
        }
        else if (buf_end >= 0)
        {
            while (s < buf_end)
            {
                if (size++ <= max_output_buffer_size)
                    line_index.Offsets.push_back(s);
                int eol = Array.IndexOf(buf, (byte)'\n', s, buf_end - s); // C++: s = (const char*)ImMemchr(s, '\n', buf_end - s);
                s = (eol >= 0) ? eol + 1 : buf_end;
            }
        }
        else
        {
            // Inactive path: we don't know buf_end ahead of time.
            while (true)
            {
                if (size++ <= max_output_buffer_size)
                    line_index.Offsets.push_back(s);
                int strlen = StrlenBytes(buf, s);
                int eol = Array.IndexOf(buf, (byte)'\n', s, strlen); // C++: strchr(s, '\n')
                if (eol >= 0)
                {
                    s = eol + 1;
                    continue;
                }
                s += strlen;
                trailing_line_already_counted = true;
                break;
            }
        }
        out_buf_end = s;
        int effective_end = (buf_end >= 0) ? buf_end : s; // C++: buf_end is only overwritten when out_buf_end != NULL
        if (size == 0)
        {
            line_index.Offsets.push_back(0);
            size++;
        }
        if (effective_end > 0 && buf[effective_end - 1] == '\n' && !trailing_line_already_counted && size++ <= max_output_buffer_size)
            line_index.Offsets.push_back(effective_end);
        return size;
    }

    // C++: static ImVec2 InputTextLineIndexGetPosOffset(ImGuiContext& g, ImGuiInputTextState* state, ImGuiTextIndex* line_index, const char* buf, const char* buf_end, int cursor_n)
    private static ImVec2 InputTextLineIndexGetPosOffset(ImGuiContext g, ImGuiInputTextState state, ImGuiTextIndex line_index, byte[] buf, int buf_end, int cursor_n)
    {
        int it_begin = 0;
        int it_end = line_index.Offsets.Size;
        int it = ImLowerBound(line_index.Offsets, it_begin, it_end, cursor_n);
        if (it > it_begin)
            if (it == it_end || line_index.Offsets[it] != cursor_n || (state != null && state.WrapWidth > 0.0f && state.LastMoveDirectionLR == (sbyte)ImGuiDir_Right && cursor_n > 0 && buf[cursor_n - 1] != '\n' && buf[cursor_n - 1] != 0))
                it--;

        int line_no = (it == it_begin) ? 0 : it;
        int line_start = line_index.get_line_begin(line_no);
        ImVec2 offset;
        offset.x = InputTextCalcTextSize(buf, line_start, cursor_n, buf_end, out _, null, ImDrawTextFlags_WrapKeepBlanks).x;
        offset.y = (line_no + 1) * g.FontSize;
        return offset;
    }

    // C++: void ImGui::PushPasswordFont()
    public static void PushPasswordFont()
    {
        ImGuiContext g = GImGui;
        if (g.InputTextPasswordFontBackupBaked == null)
            g.InputTextPasswordFontBackupBaked = new ImFontBaked(); // C++ value member; lazily created here
        ImFontBaked backup = g.InputTextPasswordFontBackupBaked;
        Debug.Assert(backup.IndexAdvanceX.Size == 0 && backup.IndexLookup.Size == 0);
        ImFontBaked baked = g.FontBaked;
        ImFontGlyph glyph = baked.FindGlyph((ushort)'*');
        g.InputTextPasswordFontBackupFlags = g.Font.Flags;
        backup.FallbackGlyphIndex = baked.FallbackGlyphIndex;
        backup.FallbackAdvanceX = baked.FallbackAdvanceX;
        // C++: backup->IndexLookup.swap(g.FontBaked->IndexLookup); (swap the ImVector contents)
        (backup.IndexLookup.Data, baked.IndexLookup.Data) = (baked.IndexLookup.Data, backup.IndexLookup.Data);
        (backup.IndexLookup.Size, baked.IndexLookup.Size) = (baked.IndexLookup.Size, backup.IndexLookup.Size);
        (backup.IndexLookup.Capacity, baked.IndexLookup.Capacity) = (baked.IndexLookup.Capacity, backup.IndexLookup.Capacity);
        (backup.IndexAdvanceX.Data, baked.IndexAdvanceX.Data) = (baked.IndexAdvanceX.Data, backup.IndexAdvanceX.Data);
        (backup.IndexAdvanceX.Size, baked.IndexAdvanceX.Size) = (baked.IndexAdvanceX.Size, backup.IndexAdvanceX.Size);
        (backup.IndexAdvanceX.Capacity, baked.IndexAdvanceX.Capacity) = (baked.IndexAdvanceX.Capacity, backup.IndexAdvanceX.Capacity);
        g.Font.Flags |= ImFontFlags_NoLoadGlyphs;
        // C++: g.FontBaked->FallbackGlyphIndex = g.FontBaked->Glyphs.index_from_ptr(glyph);
        int glyph_index = (glyph != null && (uint)('*') < baked.IndexLookup.Size && baked.IndexLookup[(uint)('*')] != ImUtil.IM_FONTGLYPH_INDEX_UNUSED && baked.IndexLookup[(uint)('*')] != ImUtil.IM_FONTGLYPH_INDEX_NOT_FOUND) ? baked.IndexLookup[(uint)('*')] : baked.FallbackGlyphIndex;
        baked.FallbackGlyphIndex = glyph_index;
        baked.FallbackAdvanceX = glyph != null ? glyph.AdvanceX : 0.0f;
    }

    // C++: void ImGui::PopPasswordFont()
    public static void PopPasswordFont()
    {
        ImGuiContext g = GImGui;
        ImFontBaked backup = g.InputTextPasswordFontBackupBaked;
        ImFontBaked baked = g.FontBaked;
        g.Font.Flags = g.InputTextPasswordFontBackupFlags;
        baked.FallbackGlyphIndex = backup.FallbackGlyphIndex;
        baked.FallbackAdvanceX = backup.FallbackAdvanceX;
        (backup.IndexLookup.Data, baked.IndexLookup.Data) = (baked.IndexLookup.Data, backup.IndexLookup.Data);
        (backup.IndexLookup.Size, baked.IndexLookup.Size) = (baked.IndexLookup.Size, backup.IndexLookup.Size);
        (backup.IndexLookup.Capacity, baked.IndexLookup.Capacity) = (baked.IndexLookup.Capacity, backup.IndexLookup.Capacity);
        (backup.IndexAdvanceX.Data, baked.IndexAdvanceX.Data) = (baked.IndexAdvanceX.Data, backup.IndexAdvanceX.Data);
        (backup.IndexAdvanceX.Size, baked.IndexAdvanceX.Size) = (baked.IndexAdvanceX.Size, backup.IndexAdvanceX.Size);
        (backup.IndexAdvanceX.Capacity, baked.IndexAdvanceX.Capacity) = (baked.IndexAdvanceX.Capacity, backup.IndexAdvanceX.Capacity);
        Debug.Assert(backup.IndexAdvanceX.Size == 0 && backup.IndexLookup.Size == 0);
    }

    // C++: bool ImGui::InputTextEx(const char* label, const char* hint, char* buf, int buf_size, const ImVec2& size_arg, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* callback_user_data)
    // Edit a string of text
    // - buf_size account for the zero-terminator, so a buf_size of 6 can hold "Hello" but not "Hello!".
    //   This is so we can easily call InputText() on static arrays using ARRAYSIZE() and to match
    //   Note that in std::string world, capacity() would omit 1 byte used by the zero-terminator.
    // - When active, hold on a privately held copy of the text (and apply back to 'buf'). So changing 'buf' while the InputText is active has no effect.
    public static bool InputTextEx(string label, string hint, byte[] buf, int buf_size, ImVec2 size_arg, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback = null, object callback_user_data = null)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        Debug.Assert(buf != null && buf_size >= 0);
        Debug.Assert(!((flags & ImGuiInputTextFlags_CallbackHistory) != 0 && (flags & ImGuiInputTextFlags_Multiline) != 0));        // Can't use both together (they both use up/down keys)
        Debug.Assert(!((flags & ImGuiInputTextFlags_CallbackCompletion) != 0 && (flags & ImGuiInputTextFlags_AllowTabInput) != 0)); // Can't use both together (they both use tab key)
        Debug.Assert(!((flags & ImGuiInputTextFlags_ElideLeft) != 0 && (flags & ImGuiInputTextFlags_Multiline) != 0));              // Multiline does not not work with left-trimming
        Debug.Assert((flags & ImGuiInputTextFlags_WordWrap) == 0 || (flags & ImGuiInputTextFlags_Password) == 0);                   // WordWrap does not work with Password mode.
        Debug.Assert((flags & ImGuiInputTextFlags_WordWrap) == 0 || (flags & ImGuiInputTextFlags_Multiline) != 0);                  // WordWrap does not work in single-line mode.

        ImGuiContext g = GImGui;
        ImGuiIO io = g.IO;
        ImGuiStyle style = g.Style;

        bool RENDER_SELECTION_WHEN_INACTIVE = false;
        bool is_multiline = (flags & ImGuiInputTextFlags_Multiline) != 0;

        if (is_multiline) // Open group before calling GetID() because groups tracks id created within their scope (including the scrollbar)
            BeginGroup();
        ImGuiID id = window.GetID(label);
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImVec2 frame_size = CalcItemSize(size_arg, CalcItemWidth(), (is_multiline ? g.FontSize * 8.0f : label_size.y) + style.FramePadding.y * 2.0f); // Arbitrary default of 8 lines high for multi-line
        ImVec2 total_size = new ImVec2(frame_size.x + (label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f), frame_size.y);

        ImRect frame_bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + frame_size);
        ImRect total_bb = new ImRect(frame_bb.Min, frame_bb.Min + total_size);

        ImGuiWindow draw_window = window;
        ImVec2 inner_size = frame_size;
        ImGuiLastItemData item_data_backup = default;
        if (is_multiline)
        {
            ImVec2 backup_pos = window.DC.CursorPos;
            ItemSize(total_bb.GetSize(), style.FramePadding.y);
            bool no_clip = (g.InputTextDeactivatedState.ID == id) || (g.ActiveId == id) || (id == g.NavActivateId); // Mimic some of ItemAdd() logic + add InputTextDeactivatedState.ID check.
            if (!ItemAdd(total_bb, id, frame_bb, ImGuiItemFlags_Inputable) && !no_clip)
            {
                EndGroup();
                return false;
            }
            item_data_backup = g.LastItemData;
            window.DC.CursorPos = backup_pos;

            // Prevent NavActivation from explicit Tabbing when our widget accepts Tab inputs: this allows cycling through widgets without stopping.
            if (g.NavActivateId == id && (g.NavActivateFlags & ImGuiActivateFlags_FromTabbing) != 0 && (g.NavActivateFlags & ImGuiActivateFlags_FromFocusApi) == 0 && (flags & ImGuiInputTextFlags_AllowTabInput) != 0)
                g.NavActivateId = 0;

            // Prevent NavActivate reactivating in BeginChild() when we are already active.
            ImGuiID backup_activate_id = g.NavActivateId;
            if (g.ActiveId == id) // Prevent reactivation
                g.NavActivateId = 0;

            // We reproduce the contents of BeginChildFrame() in order to provide 'label' so our window internal data are easier to read/debug.
            PushStyleColor(ImGuiCol_ChildBg, style.Colors[ImGuiCol_FrameBg]);
            PushStyleVar(ImGuiStyleVar_ChildRounding, style.FrameRounding);
            PushStyleVar(ImGuiStyleVar_ChildBorderSize, style.FrameBorderSize);
            PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0)); // Ensure no clip rect so mouse hover can reach FramePadding edges
            bool child_visible = BeginChildEx(label, id, frame_bb.GetSize(), ImGuiChildFlags_Borders, ImGuiWindowFlags_NoMove);
            g.NavActivateId = backup_activate_id;
            PopStyleVar(3);
            PopStyleColor();
            if (!child_visible && !no_clip)
            {
                EndChild();
                EndGroup();
                return false;
            }
            draw_window = g.CurrentWindow; // Child window
            draw_window.DC.NavLayersActiveMaskNext |= (short)(1 << draw_window.DC.NavLayerCurrent); // This is to ensure that EndChild() will display a navigation highlight so we can "enter" into it.
            draw_window.DC.CursorPos += style.FramePadding;
            inner_size.x -= draw_window.ScrollbarSizes.x;

            // FIXME: Could this be a ImGuiChildFlags to affect the SetLastItemDataForWindow() call?
            g.LastItemData.ID = id;
            g.LastItemData.ItemFlags = item_data_backup.ItemFlags;
            g.LastItemData.StatusFlags = item_data_backup.StatusFlags;
        }
        else
        {
            // Support for internal ImGuiInputTextFlags_MergedItem flag, which could be redesigned as an ItemFlags if needed (with test performed in ItemAdd)
            ItemSize(total_bb.GetSize(), style.FramePadding.y);
            if ((flags & ImGuiInputTextFlags_TempInput) == 0)
                if (!ItemAdd(total_bb, id, frame_bb, ImGuiItemFlags_Inputable))
                    return false;
        }

        // Ensure mouse cursor is set even after switching to keyboard/gamepad mode. May generalize further? (#6417)
        bool hovered = ItemHoverable(frame_bb, id, g.LastItemData.ItemFlags | ImGuiItemFlags_NoNavDisableMouseHover);
        if (hovered)
            SetMouseCursor(ImGuiMouseCursor_TextInput);
        if (hovered && g.NavHighlightItemUnderNav)
            hovered = false;

        // We are only allowed to access the state if we are already the active widget.
        ImGuiInputTextState state = GetInputTextState(id);

        if ((g.LastItemData.ItemFlags & ImGuiItemFlags_ReadOnly) != 0)
            flags |= ImGuiInputTextFlags_ReadOnly;
        bool is_readonly = (flags & ImGuiInputTextFlags_ReadOnly) != 0;
        bool is_password = (flags & ImGuiInputTextFlags_Password) != 0;
        bool is_undoable = (flags & ImGuiInputTextFlags_NoUndoRedo) == 0;
        bool is_resizable = (flags & ImGuiInputTextFlags_CallbackResize) != 0;
        if (is_resizable)
            Debug.Assert(callback != null); // Must provide a callback if you set the ImGuiInputTextFlags_CallbackResize flag!

        // Word-wrapping: enforcing a fixed width not altered by vertical scrollbar makes things easier, notably to track cursor reliably and avoid one-frame glitches.
        // Instead of using ImGuiWindowFlags_AlwaysVerticalScrollbar we account for that space if the scrollbar is not visible.
        bool is_wordwrap = (flags & ImGuiInputTextFlags_WordWrap) != 0;
        float wrap_width = 0.0f;
        if (is_wordwrap)
            wrap_width = ImMath.ImMax(1.0f, GetContentRegionAvail().x + (draw_window.ScrollbarY ? 0.0f : -g.Style.ScrollbarSize));

        bool user_clicked = hovered && io.MouseClicked[0];
        bool input_requested_by_nav = (g.ActiveId != id) && (g.NavActivateId == id);
        bool input_requested_by_reactivate = (g.InputTextReactivateId == id); // for io.ConfigInputTextEnterKeepActive
        bool input_requested_by_user = (user_clicked) || (g.ActiveId == 0 && (flags & ImGuiInputTextFlags_TempInput) != 0 && g.InputTextDeactivatedState.ID != id);
        ImGuiID scrollbar_id = (is_multiline && state != null) ? GetWindowScrollbarID(draw_window, ImGuiAxis_Y) : 0;
        bool user_scroll_finish = is_multiline && state != null && g.ActiveId == 0 && g.ActiveIdPreviousFrame == scrollbar_id;
        bool user_scroll_active = is_multiline && state != null && g.ActiveId == scrollbar_id;
        bool clear_active_id = false;
        bool select_all = false;

        float scroll_y = is_multiline ? draw_window.Scroll.y : float.MaxValue;

        bool init_reload_from_user_buf = (state != null && state.WantReloadUserBuf);
        bool init_changed_specs_multiline = (state != null && (state.Stb.single_line != (is_multiline ? (byte)0 : (byte)1))); // state != NULL means its our state.
        bool init_changed_specs_readonly = (state != null && ((state.Flags ^ flags) & ImGuiInputTextFlags_ReadOnly) != 0); // state != NULL means its our state.
        bool init_make_active = (input_requested_by_user || input_requested_by_nav || input_requested_by_reactivate || user_scroll_finish);
        if (init_reload_from_user_buf)
        {
            int new_len = StrlenBytes(buf, 0); // C++: (int)ImStrlen(buf)
            Debug.Assert(new_len + 1 <= buf_size); // "Is your input buffer properly zero-terminated?"
            state.WantReloadUserBuf = false;
            InputTextReconcileUndoState(state, state.TextA.Data, state.TextLen, buf, new_len);
            state.TextA.resize(buf_size + 1); // we use +1 to make sure that .Data is always pointing to at least an empty string.
            state.TextLen = new_len;
            Array.Copy(buf, state.TextA.Data, state.TextLen + 1);
            state.Stb.select_start = state.ReloadSelectionStart;
            state.Stb.cursor = state.Stb.select_end = state.ReloadSelectionEnd; // will be clamped to bounds below
        }
        else if ((init_make_active && g.ActiveId != id) || init_changed_specs_multiline || init_changed_specs_readonly)
        {
            // Access state even if we don't own it yet.
            state = g.InputTextState;
            state.CursorAnimReset();

            // Backup state of deactivating item so they'll have a chance to do a write to output buffer on the same frame they report IsItemDeactivatedAfterEdit (#4714)
            if (state.ID != id && state.ID == g.ActiveId && (init_make_active && g.ActiveId != id)) //-V560
                InputTextDeactivateHook(state.ID); // <-- this is essentially an earlier call to what SetActiveID() would do below.

            // Take a copy of the initial buffer value.
            // From the moment we focused we are normally ignoring the content of 'buf' (unless we are in read-only mode)
            int buf_len = StrlenBytes(buf, 0);
            Debug.Assert(((buf_len + 1 <= buf_size) || (buf_len == 0 && buf_size == 0))); // "Is your input buffer properly zero-terminated?"
            if (!user_scroll_finish)
            {
                state.TextToRevertTo.resize(buf_len + 1);    // UTF-8. we use +1 to make sure that .Data is always pointing to at least an empty string.
                Array.Copy(buf, state.TextToRevertTo.Data, buf_len + 1);
            }

            // Preserve cursor position and undo/redo stack if we come back to same widget
            // FIXME: Since we reworked this on 2022/06, may want to differentiate recycle_cursor vs recycle_undostate?
            bool recycle_state = (state.ID == id && !init_changed_specs_multiline);
            if (recycle_state && !init_changed_specs_readonly && (state.TextLen != buf_len || (state.TextA.Data == null || !BytesEqualN(state.TextA.Data, buf, buf_len))))
                recycle_state = false;

            // Start edition
            state.ID = id;
            state.TextLen = buf_len;
            state.EditedBefore = false;
            if (!is_readonly)
            {
                state.TextA.resize(buf_size + 1); // we use +1 to make sure that .Data is always pointing to at least an empty string.
                Array.Copy(buf, state.TextA.Data, state.TextLen + 1);
            }

            // Find initial scroll position for right alignment
            state.Scroll = new ImVec2(0.0f, 0.0f);
            if ((flags & ImGuiInputTextFlags_ElideLeft) != 0)
                state.Scroll.x += ImMath.ImMax(0.0f, CalcTextSize(BytesToString(buf, 0, buf_len), -1, true).x - frame_size.x + style.FramePadding.x * 2.0f);

            // Recycle existing cursor/selection/undo stack but clamp position
            // Note a single mouse click will override the cursor/position immediately by calling stb_textedit_click handler.
            if (!recycle_state)
                ImStbTextEdit.StbTexteditInitializeState(state.Stb, !is_multiline); // C++: stb_textedit_initialize_state()

            if (!is_multiline)
            {
                if ((flags & ImGuiInputTextFlags_AutoSelectAll) != 0)
                    select_all = true;
                if (input_requested_by_nav && (!recycle_state || (g.NavActivateFlags & ImGuiActivateFlags_TryToPreserveState) == 0))
                    select_all = true;
                if (user_clicked && io.KeyCtrl)
                    select_all = true;
            }

            if ((flags & ImGuiInputTextFlags_AlwaysOverwrite) != 0)
                state.Stb.insert_mode = 1; // stb field name is indeed incorrect (see #2863)
        }

        bool is_osx = io.ConfigMacOSXBehaviors;
        if (init_make_active && g.ActiveId != id)
        {
            Debug.Assert(state != null && state.ID == id);
            SetActiveID(id, window);
            SetFocusID(id, window);
            FocusWindow(window);
            if (input_requested_by_nav)
                SetNavCursorVisibleAfterMove();
        }
        if (g.ActiveId == id)
        {
            // Declare some inputs, the other are registered and polled via Shortcut() routing system.
            // FIXME: The reason we don't use Shortcut() is we would need a routing flag to specify multiple mods, or to all mods combination into individual shortcuts.
            ImGuiKey[] always_owned_keys = { ImGuiKey_LeftArrow, ImGuiKey_RightArrow, ImGuiKey_Delete, ImGuiKey_Backspace, ImGuiKey_Home, ImGuiKey_End };
            for (int ki = 0; ki < always_owned_keys.Length; ki++)
                SetKeyOwner(always_owned_keys[ki], id);
            if (user_clicked)
                SetKeyOwner(ImGuiKey_MouseLeft, id);
            g.ActiveIdUsingNavDirMask |= (ImU32)((1 << ImGuiDir_Left) | (1 << ImGuiDir_Right));
            if (is_multiline || (flags & ImGuiInputTextFlags_CallbackHistory) != 0)
            {
                g.ActiveIdUsingNavDirMask |= (ImU32)((1 << ImGuiDir_Up) | (1 << ImGuiDir_Down));
                SetKeyOwner(ImGuiKey_UpArrow, id);
                SetKeyOwner(ImGuiKey_DownArrow, id);
            }
            if (is_multiline)
            {
                SetKeyOwner(ImGuiKey_PageUp, id);
                SetKeyOwner(ImGuiKey_PageDown, id);
            }
            // FIXME: May be a problem to always steal Alt on OSX, would ideally still allow an uninterrupted Alt down-up to toggle menu
            if (is_osx)
                SetKeyOwner(ImGuiMod_Alt, id);

            // Expose scroll in a manner that is agnostic to us using a child window
            if (is_multiline && state != null)
                state.Scroll.y = draw_window.Scroll.y;

            // Read-only mode always ever read from source buffer. Refresh TextLen when active.
            if (is_readonly && state != null)
                state.TextLen = StrlenBytes(buf, 0);
            if (state != null)
                state.CursorClamp();
            //if (is_readonly && state != null)
            //    state.TextA.clear(); // Uncomment to facilitate debugging, but we otherwise prefer to keep/amortize th allocation.
        }
        if (state != null)
            state.TextSrc = is_readonly ? buf : state.TextA.Data;

        // We have an edge case if ActiveId was set through another widget (e.g. widget being swapped), clear id immediately (don't wait until the end of the function)
        if (g.ActiveId == id && state == null)
            ClearActiveID();

        // Release focus when we click outside
        if (g.ActiveId == id && io.MouseClicked[0] && !init_make_active) //-V560
            clear_active_id = true;

        // Lock the decision of whether we are going to take the path displaying the cursor or selection
        bool render_cursor = (g.ActiveId == id) || (state != null && user_scroll_active);
        bool render_selection = state != null && (state.HasSelection() || select_all) && (RENDER_SELECTION_WHEN_INACTIVE || render_cursor);
        bool value_changed = false;
        bool validated = false;

        // Select the buffer to render.
        bool buf_display_from_state = (render_cursor || render_selection || g.ActiveId == id) && !is_readonly && state != null;
        bool is_displaying_hint = (hint != null && ((buf_display_from_state ? state.TextA.Data : buf)[0] == 0));

        // Password pushes a temporary font with only a fallback glyph
        if (is_password && !is_displaying_hint)
            PushPasswordFont();

        if (state != null && state.ID == id)
        {
            state.Flags = flags;
            //state->LastFrameActive = g.FrameCount;

            // Word-wrapping: attempt to keep cursor in view while resizing frame/parent (FIXME-WORDWRAP: would be better to preserve same relative offset)
            if (is_wordwrap && state.WrapWidth != wrap_width)
            {
                state.CursorCenterY = true;
                state.WrapWidth = wrap_width;
                render_cursor = true;
            }
        }

        // Process mouse inputs and character inputs
        if (g.ActiveId == id)
        {
            Debug.Assert(state != null);
            state.EditedThisFrame = false;
            state.BufCapacity = buf_size;
            state.WrapWidth = wrap_width;

            // Although we are active we don't prevent mouse from hovering other elements unless we are interacting right now with the widget.
            // Down the line we should have a cleaner library-wide concept of Selected vs Active.
            g.ActiveIdAllowOverlap = !io.MouseDown[0];

            // Edit in progress
            float mouse_x = (io.MousePos.x - frame_bb.Min.x - style.FramePadding.x) + state.Scroll.x;
            float mouse_y = (is_multiline ? (io.MousePos.y - draw_window.DC.CursorPos.y) : (g.FontSize * 0.5f));

            if (select_all)
            {
                state.SelectAll();
                state.SelectedAllMouseLock = true;
            }
            else if (hovered && io.MouseClickedCount[0] >= 2 && !io.KeyShift)
            {
                ImStbTextEdit.StbTexteditClick(state, mouse_x, mouse_y);
                int multiclick_count = (io.MouseClickedCount[0] - 2);
                if ((multiclick_count % 2) == 0)
                {
                    // Double-click: Select word
                    // We always use the "Mac" word advance for double-click select vs Ctrl+Right which use the platform dependent variant:
                    // FIXME: There are likely many ways to improve this behavior, but there's no "right" behavior (depends on use-case, software, OS)
                    bool is_bol = (state.Stb.cursor == 0) || ImStbTextEdit.StbTexteditGetChar(state, state.Stb.cursor - 1) == '\n';
                    if (state.Stb.HasSelection() || !is_bol)
                        state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_WORDLEFT);
                    //state->OnKeyPressed(STB_TEXTEDIT_K_WORDRIGHT | STB_TEXTEDIT_K_SHIFT);
                    if (!state.Stb.HasSelection())
                        ImStbTextEdit.StbTexteditPrepSelectionAtCursor(state.Stb);
                    state.Stb.cursor = ImStbTextEdit.StbTexteditMoveWordRightMac(state, state.Stb.cursor);
                    state.Stb.select_end = state.Stb.cursor;
                    ImStbTextEdit.StbTexteditClamp(state);
                }
                else
                {
                    // Triple-click: Select line
                    bool is_eol = ImStbTextEdit.StbTexteditGetChar(state, state.Stb.cursor) == '\n';
                    state.WrapWidth = 0.0f; // Temporarily disable wrapping so we use real line start.
                    state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_LINESTART);
                    state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_LINEEND | ImStbTextEdit.STB_TEXTEDIT_K_SHIFT);
                    state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_RIGHT | ImStbTextEdit.STB_TEXTEDIT_K_SHIFT);
                    state.WrapWidth = wrap_width;
                    if (!is_eol && is_multiline)
                    {
                        (state.Stb.select_start, state.Stb.select_end) = (state.Stb.select_end, state.Stb.select_start); // C++: ImSwap()
                        state.Stb.cursor = state.Stb.select_end;
                    }
                    state.CursorFollow = false;
                }
                state.CursorAnimReset();
            }
            else if (io.MouseClicked[0] && !state.SelectedAllMouseLock)
            {
                if (hovered)
                {
                    if (io.KeyShift)
                        ImStbTextEdit.StbTexteditDrag(state, mouse_x, mouse_y);
                    else
                        ImStbTextEdit.StbTexteditClick(state, mouse_x, mouse_y);
                    state.CursorAnimReset();
                }
            }
            else if (io.MouseDown[0] && !state.SelectedAllMouseLock && (io.MouseDelta.x != 0.0f || io.MouseDelta.y != 0.0f))
            {
                ImStbTextEdit.StbTexteditDrag(state, mouse_x, mouse_y);
                state.CursorAnimReset();
                state.CursorFollow = true;
            }
            if (state.SelectedAllMouseLock && !io.MouseDown[0])
                state.SelectedAllMouseLock = false;

            // We expect backends to emit a Tab key but some also emit a Tab character which we ignore (#2467, #1336)
            // (For Tab and Enter: Win32/SFML/Allegro are sending both keys and chars, GLFW and SDL are only sending keys. For Space they all send all threes)
            if ((flags & ImGuiInputTextFlags_AllowTabInput) != 0 && !is_readonly)
            {
                if (Shortcut(ImGuiKey_Tab, ImGuiInputFlags_Repeat, id))
                {
                    uint c = (uint)'\t'; // Insert TAB
                    if (InputTextFilterCharacter(g, state, ref c, callback, callback_user_data))
                        state.OnCharPressed(c);
                }
                // FIXME: Implement Shift+Tab
            }

            // Process regular text input (before we check for Return because using some IME will effectively send a Return?)
            // We ignore Ctrl inputs, but need to allow Alt+Ctrl as some keyboards (e.g. German) use AltGR (which _is_ Alt+Ctrl) to input certain characters.
            bool ignore_char_inputs = (io.KeyCtrl && !io.KeyAlt) || (is_osx && io.KeyCtrl);
            if (io.InputQueueCharacters.Size > 0)
            {
                if (!ignore_char_inputs && !is_readonly && !input_requested_by_nav)
                    for (int n = 0; n < io.InputQueueCharacters.Size; n++)
                    {
                        // Insert character if they pass filtering
                        uint c = (uint)io.InputQueueCharacters[n];
                        if (c == '\t') // Skip Tab, see above.
                            continue;
                        if (InputTextFilterCharacter(g, state, ref c, callback, callback_user_data))
                            state.OnCharPressed(c);
                    }

                // Consume characters
                io.InputQueueCharacters.resize(0);
            }
        }

        // Process other shortcuts/key-presses
        bool revert_edit = false;
        if (g.ActiveId == id && !g.ActiveIdIsJustActivated && !clear_active_id)
        {
            Debug.Assert(state != null);

            int row_count_per_page = ImMath.ImMax((int)((inner_size.y - style.FramePadding.y) / g.FontSize), 1);
            state.Stb.row_count_per_page = row_count_per_page;

            int k_mask = (io.KeyShift ? ImStbTextEdit.STB_TEXTEDIT_K_SHIFT : 0);
            bool is_wordmove_key_down = is_osx ? io.KeyAlt : io.KeyCtrl;                     // OS X style: Text editing cursor movement using Alt instead of Ctrl
            bool is_startend_key_down = is_osx && io.KeyCtrl && !io.KeySuper && !io.KeyAlt;  // OS X style: Line/Text Start and End using Cmd+Arrows instead of Home/End

            // Using Shortcut() with ImGuiInputFlags_RouteFocused (default policy) to allow routing operations for other code (e.g. calling window trying to use Ctrl+A and Ctrl+B: former would be handled by InputText)
            // Otherwise we could simply assume that we own the keys as we are active.
            ImGuiInputFlags f_repeat = ImGuiInputFlags_Repeat;
            bool is_cut = (Shortcut(ImGuiMod_Ctrl | ImGuiKey_X, f_repeat, id) || Shortcut(ImGuiMod_Shift | ImGuiKey_Delete, f_repeat, id)) && !is_readonly && !is_password && (!is_multiline || state.HasSelection());
            bool is_copy = (Shortcut(ImGuiMod_Ctrl | ImGuiKey_C, 0, id) || Shortcut(ImGuiMod_Ctrl | ImGuiKey_Insert, 0, id)) && !is_password && (!is_multiline || state.HasSelection());
            bool is_paste = (Shortcut(ImGuiMod_Ctrl | ImGuiKey_V, f_repeat, id) || Shortcut(ImGuiMod_Shift | ImGuiKey_Insert, f_repeat, id)) && !is_readonly;
            bool is_undo = (Shortcut(ImGuiMod_Ctrl | ImGuiKey_Z, f_repeat, id)) && !is_readonly && is_undoable;
            bool is_redo = (Shortcut(ImGuiMod_Ctrl | ImGuiKey_Y, f_repeat, id) || Shortcut(ImGuiMod_Ctrl | ImGuiMod_Shift | ImGuiKey_Z, f_repeat, id)) && !is_readonly && is_undoable;
            bool is_select_all = Shortcut(ImGuiMod_Ctrl | ImGuiKey_A, 0, id);

            // We allow validate/cancel with Nav source (gamepad) to makes it easier to undo an accidental NavInput press with no keyboard wired, but otherwise it isn't very useful.
            bool nav_gamepad_active = (io.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0 && (io.BackendFlags & ImGuiBackendFlags_HasGamepad) != 0;
            bool is_enter = Shortcut(ImGuiKey_Enter, f_repeat, id) || Shortcut(ImGuiKey_KeypadEnter, f_repeat, id);
            bool is_ctrl_enter = Shortcut(ImGuiMod_Ctrl | ImGuiKey_Enter, f_repeat, id) || Shortcut(ImGuiMod_Ctrl | ImGuiKey_KeypadEnter, f_repeat, id);
            bool is_shift_enter = Shortcut(ImGuiMod_Shift | ImGuiKey_Enter, f_repeat, id) || Shortcut(ImGuiMod_Shift | ImGuiKey_KeypadEnter, f_repeat, id);
            bool is_gamepad_validate = nav_gamepad_active && IsKeyPressed(ImGuiKey_NavGamepadActivate, false);
            bool is_cancel = Shortcut(ImGuiKey_Escape, f_repeat, id) || (nav_gamepad_active && Shortcut(ImGuiKey_NavGamepadCancel, f_repeat, id));

            // FIXME: Should use more Shortcut() and reduce IsKeyPressed()+SetKeyOwner(), but requires modifiers combination to be taken account of.
            // FIXME-OSX: Missing support for Alt(option)+Right/Left = go to end of line, or next line if already in end of line.
            if (IsKeyPressed(ImGuiKey_LeftArrow))
            {
                int k = (is_startend_key_down ? ImStbTextEdit.STB_TEXTEDIT_K_LINESTART : is_wordmove_key_down ? ImStbTextEdit.STB_TEXTEDIT_K_WORDLEFT : ImStbTextEdit.STB_TEXTEDIT_K_LEFT) | k_mask;
                state.OnKeyPressed(k);
            }
            else if (IsKeyPressed(ImGuiKey_RightArrow))
            {
                int k = (is_startend_key_down ? ImStbTextEdit.STB_TEXTEDIT_K_LINEEND : is_wordmove_key_down ? ImStbTextEdit.STB_TEXTEDIT_K_WORDRIGHT : ImStbTextEdit.STB_TEXTEDIT_K_RIGHT) | k_mask;
                state.OnKeyPressed(k);
            }
            else if (IsKeyPressed(ImGuiKey_UpArrow) && is_multiline)
            {
                if (io.KeyCtrl) SetScrollY(draw_window, ImMath.ImMax(draw_window.Scroll.y - g.FontSize, 0.0f));
                else state.OnKeyPressed((is_startend_key_down ? ImStbTextEdit.STB_TEXTEDIT_K_TEXTSTART : ImStbTextEdit.STB_TEXTEDIT_K_UP) | k_mask);
            }
            else if (IsKeyPressed(ImGuiKey_DownArrow) && is_multiline)
            {
                if (io.KeyCtrl) SetScrollY(draw_window, ImMath.ImMin(draw_window.Scroll.y + g.FontSize, GetScrollMaxY()));
                else state.OnKeyPressed((is_startend_key_down ? ImStbTextEdit.STB_TEXTEDIT_K_TEXTEND : ImStbTextEdit.STB_TEXTEDIT_K_DOWN) | k_mask);
            }
            else if (IsKeyPressed(ImGuiKey_PageUp) && is_multiline)
            {
                state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_PGUP | k_mask);
                scroll_y -= row_count_per_page * g.FontSize;
            }
            else if (IsKeyPressed(ImGuiKey_PageDown) && is_multiline)
            {
                state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_PGDOWN | k_mask);
                scroll_y += row_count_per_page * g.FontSize;
            }
            else if (IsKeyPressed(ImGuiKey_Home))
            {
                state.OnKeyPressed(io.KeyCtrl ? ImStbTextEdit.STB_TEXTEDIT_K_TEXTSTART | k_mask : ImStbTextEdit.STB_TEXTEDIT_K_LINESTART | k_mask);
            }
            else if (IsKeyPressed(ImGuiKey_End))
            {
                state.OnKeyPressed(io.KeyCtrl ? ImStbTextEdit.STB_TEXTEDIT_K_TEXTEND | k_mask : ImStbTextEdit.STB_TEXTEDIT_K_LINEEND | k_mask);
            }
            else if (IsKeyPressed(ImGuiKey_Delete) && !is_readonly && !is_cut)
            {
                if (!state.HasSelection())
                {
                    // OSX doesn't seem to have Super+Delete to delete until end-of-line, so we don't emulate that (as opposed to Super+Backspace)
                    if (is_wordmove_key_down)
                        state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_WORDRIGHT | ImStbTextEdit.STB_TEXTEDIT_K_SHIFT);
                }
                state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_DELETE | k_mask);
            }
            else if (IsKeyPressed(ImGuiKey_Backspace) && !is_readonly)
            {
                if (!state.HasSelection())
                {
                    if (is_wordmove_key_down)
                        state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_WORDLEFT | ImStbTextEdit.STB_TEXTEDIT_K_SHIFT);
                    else if (is_osx && io.KeyCtrl && !io.KeyAlt && !io.KeySuper)
                        state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_LINESTART | ImStbTextEdit.STB_TEXTEDIT_K_SHIFT);
                }
                state.OnKeyPressed(ImStbTextEdit.STB_TEXTEDIT_K_BACKSPACE | k_mask);
            }
            else if (is_enter || is_ctrl_enter || is_shift_enter || is_gamepad_validate)
            {
                // Determine if we turn Enter into a \n character
                bool ctrl_enter_for_new_line = (flags & ImGuiInputTextFlags_CtrlEnterForNewLine) != 0;
                bool is_new_line = is_multiline && !is_gamepad_validate && (is_shift_enter || (is_enter && !ctrl_enter_for_new_line) || (is_ctrl_enter && ctrl_enter_for_new_line));
                if (!is_new_line)
                {
                    validated = clear_active_id = true;
                    if (io.ConfigInputTextEnterKeepActive && !is_multiline && !is_ctrl_enter && !is_shift_enter)
                    {
                        // Queue reactivation, so that e.g. IsItemDeactivatedAfterEdit() will work. (#9001)
                        state.SelectAll(); // No need to scroll
                        g.InputTextReactivateId = id; // Mark for reactivation on next frame
                    }
                }
                else if (!is_readonly)
                {
                    // Insert new line
                    uint c = (uint)'\n';
                    if (InputTextFilterCharacter(g, state, ref c, callback, callback_user_data))
                        state.OnCharPressed(c);
                }
            }
            else if (is_cancel)
            {
                if ((flags & ImGuiInputTextFlags_EscapeClearsAll) != 0)
                {
                    if (state.TextA.Data[0] != 0)
                    {
                        revert_edit = true;
                    }
                    else
                    {
                        render_cursor = render_selection = false;
                        clear_active_id = true;
                    }
                }
                else
                {
                    clear_active_id = revert_edit = true;
                    render_cursor = render_selection = false;
                }
            }
            else if (is_undo || is_redo)
            {
                state.OnKeyPressed(is_undo ? ImStbTextEdit.STB_TEXTEDIT_K_UNDO : ImStbTextEdit.STB_TEXTEDIT_K_REDO);
                state.ClearSelection();
            }
            else if (is_select_all)
            {
                state.SelectAll();
                state.CursorFollow = true;
            }
            else if (is_cut || is_copy)
            {
                // Cut, Copy
                if (g.PlatformIO.Platform_SetClipboardTextFn != null)
                {
                    // SetClipboardText() only takes null terminated strings + state->TextSrc may point to read-only user buffer, so we need to make a copy.
                    int ib = state.HasSelection() ? ImMath.ImMin(state.Stb.select_start, state.Stb.select_end) : 0;
                    int ie = state.HasSelection() ? ImMath.ImMax(state.Stb.select_start, state.Stb.select_end) : state.TextLen;
                    g.TempBuffer.reserve(ie - ib + 1);
                    g.TempBuffer.resize(ie - ib + 1);
                    Array.Copy(state.TextSrc, ib, g.TempBuffer.Data, 0, ie - ib);
                    g.TempBuffer.Data[ie - ib] = 0;
                    SetClipboardText(BytesToString(g.TempBuffer.Data, 0, ie - ib));
                }
                if (is_cut)
                {
                    if (!state.HasSelection())
                        state.SelectAll();
                    state.CursorFollow = true;
                    ImStbTextEdit.StbTexteditCut(state);
                }
            }
            else if (is_paste)
            {
                string clipboard = GetClipboardText();
                if (clipboard != null)
                {
                    // Filter pasted buffer
                    byte[] clipboard_bytes = Encoding.UTF8.GetBytes(clipboard);
                    int clipboard_len = clipboard_bytes.Length; // C++: (int)ImStrlen(clipboard)
                    int clipboard_end = clipboard_len;
                    ImVector<byte> clipboard_filtered = new ImVector<byte>();
                    clipboard_filtered.reserve(clipboard_len + 1);
                    int s = 0;
                    while (s < clipboard_len && clipboard_bytes[s] != 0)
                    {
                        int in_len = ImText.ImTextCharFromUtf8(out uint c, clipboard_bytes, s, clipboard_end);
                        if (in_len <= 0)
                            in_len = 1; // (C++ would not advance on invalid data; guard against an infinite loop)
                        s += in_len;
                        if (!InputTextFilterCharacter(g, state, ref c, callback, callback_user_data, true))
                            continue;
                        byte[] c_utf8 = new byte[5];
                        int out_len = ImText.ImTextCharToUtf8(c_utf8, 0, c);
                        clipboard_filtered.resize(clipboard_filtered.Size + out_len);
                        Array.Copy(c_utf8, 0, clipboard_filtered.Data, clipboard_filtered.Size - out_len, out_len);
                    }
                    if (clipboard_filtered.Size > 0) // If everything was filtered, ignore the pasting operation
                    {
                        clipboard_filtered.push_back(0);
                        ImStbTextEdit.StbTexteditPaste(state, clipboard_filtered.Data, clipboard_filtered.Size - 1);
                        state.CursorFollow = true;
                    }
                }
            }

            // Update render selection flag after events have been handled, so selection highlight can be displayed during the same frame.
            render_selection |= state.HasSelection() && (RENDER_SELECTION_WHEN_INACTIVE || render_cursor);
        }

        // Process revert and user callbacks
        byte[] apply_new_text = null;
        int apply_new_text_length = 0;
        if (g.ActiveId == id)
        {
            Debug.Assert(state != null);
            if (revert_edit && !is_readonly)
            {
                if ((flags & ImGuiInputTextFlags_EscapeClearsAll) != 0)
                {
                    // Clear input
                    Debug.Assert(state.TextA.Data[0] != 0);
                    apply_new_text = Array.Empty<byte>();
                    apply_new_text_length = 0;
                    value_changed = true;
                    byte[] empty_string = { 0 };
                    ImStbTextEdit.StbTexteditReplace(state, empty_string, 0);
                }
                else if (!BytesEqualN(state.TextA.Data, state.TextToRevertTo.Data, state.TextA.Size)) // C++: strcmp(state->TextA.Data, state->TextToRevertTo.Data) != 0
                {
                    apply_new_text = state.TextToRevertTo.Data;
                    apply_new_text_length = state.TextToRevertTo.Size - 1;

                    // Restore initial value. Only return true if restoring to the initial value changes the current buffer contents.
                    // Push records into the undo stack so we can Ctrl+Z the revert operation itself
                    value_changed = true;
                    ImStbTextEdit.StbTexteditReplace(state, state.TextToRevertTo.Data, state.TextToRevertTo.Size - 1);
                }
            }

            // User callback
            if ((flags & (ImGuiInputTextFlags_CallbackCompletion | ImGuiInputTextFlags_CallbackHistory | ImGuiInputTextFlags_CallbackEdit | ImGuiInputTextFlags_CallbackAlways)) != 0)
            {
                Debug.Assert(callback != null);

                // The reason we specify the usage semantic (Completion/History) is that Completion needs to disable keyboard TABBING at the moment.
                ImGuiInputTextFlags event_flag = 0;
                ImGuiKey event_key = ImGuiKey_None;
                if ((flags & ImGuiInputTextFlags_CallbackCompletion) != 0 && Shortcut(ImGuiKey_Tab, 0, id))
                {
                    event_flag = ImGuiInputTextFlags_CallbackCompletion;
                    event_key = ImGuiKey_Tab;
                }
                else if ((flags & ImGuiInputTextFlags_CallbackHistory) != 0 && IsKeyPressed(ImGuiKey_UpArrow))
                {
                    event_flag = ImGuiInputTextFlags_CallbackHistory;
                    event_key = ImGuiKey_UpArrow;
                }
                else if ((flags & ImGuiInputTextFlags_CallbackHistory) != 0 && IsKeyPressed(ImGuiKey_DownArrow))
                {
                    event_flag = ImGuiInputTextFlags_CallbackHistory;
                    event_key = ImGuiKey_DownArrow;
                }
                else if ((flags & ImGuiInputTextFlags_CallbackEdit) != 0 && state.EditedThisFrame)
                {
                    event_flag = ImGuiInputTextFlags_CallbackEdit;
                }
                else if ((flags & ImGuiInputTextFlags_CallbackAlways) != 0)
                {
                    event_flag = ImGuiInputTextFlags_CallbackAlways;
                }

                if (event_flag != 0)
                {
                    ImGuiInputTextCallbackData callback_data = new ImGuiInputTextCallbackData();
                    callback_data.Ctx = g;
                    callback_data.ID = id;
                    callback_data.Flags = flags;
                    callback_data.EventFlag = event_flag;
                    callback_data.EventActivated = (g.ActiveId == state.ID && g.ActiveIdIsJustActivated);
                    callback_data.UserData = callback_user_data;

                    // FIXME-OPT: Undo stack reconcile needs a backup of the data until we rework API, see #7925
                    byte[] callback_buf = is_readonly ? buf : state.TextA.Data;
                    Debug.Assert(callback_buf == state.TextSrc);
                    state.CallbackTextBackup.resize(state.TextLen + 1);
                    Array.Copy(callback_buf, state.CallbackTextBackup.Data, state.TextLen + 1);

                    callback_data.EventKey = event_key;
                    callback_data.Buf = callback_buf;
                    callback_data.BufTextLen = state.TextLen;
                    callback_data.BufSize = state.BufCapacity;
                    callback_data.BufDirty = false;
                    callback_data.CursorPos = state.Stb.cursor;
                    callback_data.SelectionStart = state.Stb.select_start;
                    callback_data.SelectionEnd = state.Stb.select_end;

                    // Call user code
                    callback(callback_data);

                    // Read back what user may have modified
                    callback_buf = is_readonly ? buf : state.TextA.Data; // Pointer may have been invalidated by a resize callback
                    Debug.Assert(callback_data.Buf == callback_buf);         // Invalid to modify those fields
                    Debug.Assert(callback_data.BufSize == state.BufCapacity);
                    Debug.Assert(callback_data.Flags == flags);
                    if (callback_data.BufDirty || callback_data.CursorPos != state.Stb.cursor)
                        state.CursorFollow = true;
                    state.Stb.cursor = ImMath.ImClamp(callback_data.CursorPos, 0, callback_data.BufTextLen);
                    state.Stb.select_start = ImMath.ImClamp(callback_data.SelectionStart, 0, callback_data.BufTextLen);
                    state.Stb.select_end = ImMath.ImClamp(callback_data.SelectionEnd, 0, callback_data.BufTextLen);
                    if (callback_data.BufDirty)
                    {
                        // Callback may update buffer and thus set buf_dirty even in read-only mode.
                        Debug.Assert(callback_data.BufTextLen == StrlenBytes(callback_data.Buf, 0)); // You need to maintain BufTextLen if you change the text!
                        InputTextReconcileUndoState(state, state.CallbackTextBackup.Data, state.CallbackTextBackup.Size - 1, callback_data.Buf, callback_data.BufTextLen);
                        state.TextLen = callback_data.BufTextLen;  // Assume correct length and valid UTF-8 from user, saves us an extra strlen()
                        state.EditedBefore = state.EditedThisFrame = true;
                        state.CursorAnimReset();
                    }
                }
            }

            // Write back result string if modified.
            // FIXME-OPT: Could mark dirty state from the stb_textedit callbacks
            if (!is_readonly)
            {
                if ((g.LastItemData.ItemFlags & ImGuiItemFlags_LiveEditOnInputText) != 0)
                {
                    // Apply when modified
                    if (!BytesEqualN(state.TextSrc, buf, int.MaxValue))
                    {
                        apply_new_text = state.TextSrc;
                        apply_new_text_length = state.TextLen;
                        value_changed = true;
                    }
                }
                else
                {
                    // Apply on validation/deactivation, otherwise cancel out previous apply attempts (e.g. revert)
                    value_changed = ((validated || clear_active_id || revert_edit) && !BytesEqualN(state.TextSrc, buf, int.MaxValue));
                    apply_new_text = value_changed ? state.TextSrc : null;
                    apply_new_text_length = value_changed ? state.TextLen : 0;
                }
            }
        }

        // Handle reapplying final data on deactivation (see InputTextDeactivateHook() for details)
        // This is used when e.g. losing focus or tabbing out into another InputText() which may already be using the temp buffer.
        if (g.InputTextDeactivatedState.ID == id)
        {
            // The state only exists after an Edit. IsItemDeactivatedAfterEdit() is not valid in every code path (see "widgets_inputtext_status_noliveedit" test).
            if ((g.ActiveId != id && IsItemDeactivated()) || (g.ActiveId == id && (flags & ImGuiInputTextFlags_TempInput) != 0))
                if (!is_readonly && !BytesEqualN(g.InputTextDeactivatedState.TextA.Data, buf, int.MaxValue))
                {
                    apply_new_text = g.InputTextDeactivatedState.TextA.Data;
                    apply_new_text_length = g.InputTextDeactivatedState.TextA.Size - 1;
                    value_changed = true;
                    //IMGUI_DEBUG_LOG("InputText(): apply Deactivated data for 0x%08X: \"%.*s\".\n", id, apply_new_text_length, apply_new_text);
                }
            g.InputTextDeactivatedState.ID = 0;
        }

        // Write back result to user buffer. This can currently only happen when (g.ActiveId == id) or when just deactivated.
        // - As soon as the InputText() is active, our stored in-widget value gets priority over any underlying modification of the user buffer.
        // - Make sure we always reapply the live buffer back to the input/user buffer before clearing ActiveId, even thought strictly speaking
        //   it was not modified on this frame. This allows the user to use InputText() without maintaining any user-side storage.
        //   (PS: if you use this property together with ImGuiInputTextFlags_CallbackResize, you are at the risk of recreating a temporary
        //    allocated/string object every frame. Which in the grand scheme of scheme is nothing, but isn't dear imgui vibe).
        if (apply_new_text != null)
        {
            Debug.Assert(apply_new_text_length >= 0);
            if (is_resizable)
            {
                ImGuiInputTextCallbackData callback_data = new ImGuiInputTextCallbackData();
                callback_data.Ctx = g;
                callback_data.ID = id;
                callback_data.Flags = flags;
                callback_data.EventFlag = ImGuiInputTextFlags_CallbackResize;
                callback_data.EventActivated = (state != null && g.ActiveId == state.ID && g.ActiveIdIsJustActivated);
                callback_data.Buf = buf;
                callback_data.BufTextLen = apply_new_text_length;
                callback_data.BufSize = ImMath.ImMax(buf_size, apply_new_text_length + 1);
                callback_data.UserData = callback_user_data;
                callback(callback_data);
                buf = callback_data.Buf;
                buf_size = callback_data.BufSize;
                apply_new_text_length = ImMath.ImMin(callback_data.BufTextLen, buf_size - 1);
                Debug.Assert(apply_new_text_length <= buf_size);
            }
            //IMGUI_DEBUG_PRINT("InputText(\"%s\"): apply_new_text length %d\n", label, apply_new_text_length);

            // If the underlying buffer resize was denied or not carried to the next frame, apply_new_text_length+1 may be >= buf_size.
            ImStrncpyBytes(buf, 0, BytesToString(apply_new_text, 0, apply_new_text_length), ImMath.ImMin(apply_new_text_length + 1, buf_size));
        }

        // Release active ID at the end of the function (so e.g. pressing Return still does a final application of the value)
        // Otherwise request text input ahead for next frame.
        if (g.ActiveId == id && clear_active_id)
        {
            state.EditedBefore = false; // Data already applied: avoid InputTextDeactivateHook() taking a record now or later if same id is activated again without editing.
            ClearActiveID();
        }

        // Render frame
        if (!is_multiline)
        {
            RenderNavCursor(frame_bb, id);
            RenderFrame(frame_bb.Min, frame_bb.Max, GetColorU32(ImGuiCol_FrameBg), true, style.FrameRounding);
        }

        ImVec2 draw_pos = is_multiline ? draw_window.DC.CursorPos : frame_bb.Min + style.FramePadding;
        ImRect clip_rect = new ImRect(frame_bb.Min.x, frame_bb.Min.y, frame_bb.Min.x + inner_size.x, frame_bb.Min.y + inner_size.y); // Not using frame_bb.Max because we have adjusted size
        if (is_multiline)
            clip_rect.ClipWith(draw_window.ClipRect);

        // Set upper limit of single-line InputTextEx() at 2 million characters strings. The current pathological worst case is a long line
        // without any carriage return, which would makes ImFont::RenderText() reserve too many vertices and probably crash. Avoid it altogether.
        // Note that we only use this limit on single-line InputText(), so a pathologically large line on a InputTextMultiline() would still crash.
        int buf_display_max_length = 2 * 1024 * 1024;
        byte[] buf_display_bytes = buf_display_from_state ? state.TextA.Data : buf; //-V595
        int buf_display_end = -1; // Byte offset of display end (-1 == C++ NULL, specialized paths below set it)

        // Display hint when contents is empty
        // At this point we need to handle the possibility that a callback could have modified the underlying buffer (#8368)
        bool new_is_displaying_hint = (hint != null && ((buf_display_from_state ? state.TextA.Data : buf)[0] == 0));
        if (new_is_displaying_hint != is_displaying_hint)
        {
            if (is_password && !is_displaying_hint)
                PopPasswordFont();
            is_displaying_hint = new_is_displaying_hint;
            if (is_password && !is_displaying_hint)
                PushPasswordFont();
        }
        if (is_displaying_hint)
        {
            byte[] hint_bytes = Encoding.UTF8.GetBytes(hint);
            buf_display_bytes = new byte[hint_bytes.Length + 1];
            Array.Copy(hint_bytes, buf_display_bytes, hint_bytes.Length);
            buf_display_end = hint_bytes.Length; // C++: hint + ImStrlen(hint)
        }
        else
        {
            if (render_cursor || render_selection || g.ActiveId == id)
                buf_display_end = state.TextLen; //-V595
            else if (is_multiline && !is_wordwrap)
                buf_display_end = -1; // Inactive multi-line: end of buffer will be output by InputTextLineIndexBuild() special strchr() path.
            else
                buf_display_end = StrlenBytes(buf_display_bytes, 0);
        }

        // Calculate visibility
        int line_visible_n0 = 0, line_visible_n1 = 1;
        if (is_multiline)
            CalcClipRectVisibleItemsY(clip_rect, draw_pos, g.FontSize, out line_visible_n0, out line_visible_n1);

        // Build line index for easy data access (makes code below simpler and faster)
        ImGuiTextIndex line_index = g.InputTextLineIndex;
        line_index.Offsets.resize(0);
        int line_count = 1;
        if (is_multiline)
        {
            // If scrolling is expected to change build full index.
            // FIXME-OPT: Could append to index when new value of line_visible_n1 becomes bigger, see second call to CalcClipRectVisibleItemsY() below.
            bool will_scroll_y = state != null && ((state.CursorFollow && render_cursor) || (state.CursorCenterY && (render_cursor || render_selection)));
            line_count = InputTextLineIndexBuild(flags, line_index, buf_display_bytes, buf_display_end, wrap_width, will_scroll_y ? int.MaxValue : line_visible_n1 + 1, out int out_buf_display_end);
            if (buf_display_end < 0)
                buf_display_end = out_buf_display_end;
        }
        line_index.EndOffset = buf_display_end;
        line_visible_n1 = ImMath.ImMin(line_visible_n1, line_count);

        // Store text height (we don't need width)
        float text_size_y = line_count * g.FontSize;
        //GetForegroundDrawList()->AddRect(draw_pos + ImVec2(0, line_visible_n0 * g.FontSize), draw_pos + ImVec2(frame_size.x, line_visible_n1 * g.FontSize), IM_COL32(255, 0, 0, 255));

        // Calculate blinking cursor position
        ImVec2 cursor_offset = (render_cursor && state != null) ? InputTextLineIndexGetPosOffset(g, state, line_index, buf_display_bytes, buf_display_end, state.Stb.cursor) : new ImVec2(0.0f, 0.0f);
        ImVec2 draw_scroll = new ImVec2();

        // Render text. We currently only render selection when the widget is active or while scrolling.
        ImU32 text_col = GetColorU32(is_displaying_hint ? ImGuiCol_TextDisabled : ImGuiCol_Text);
        if (render_cursor || render_selection)
        {
            // Render text (with cursor and selection)
            // This is going to be messy. We need to:
            // - Display the text (this alone can be more easily clipped)
            // - Handle scrolling, highlight selection, display cursor (those all requires some form of 1d->2d cursor position calculation)
            // - Measure text height (for scrollbar)
            // We are attempting to do most of that in **one main pass** to minimize the computation cost (non-negligible for large amount of text) + 2nd pass for selection rendering (we could merge them by an extra refactoring effort)
            // FIXME: This should occur on buf_display but we'd need to maintain cursor/select_start/select_end for UTF-8.
            Debug.Assert(state != null);
            state.LineCount = line_count;

            // Scroll
            float new_scroll_y = scroll_y;
            if (render_cursor && state.CursorFollow)
            {
                // Horizontal scroll in chunks of quarter width
                if ((flags & ImGuiInputTextFlags_NoHorizontalScroll) == 0)
                {
                    float scroll_increment_x = inner_size.x * 0.25f;
                    float visible_width = inner_size.x - style.FramePadding.x;
                    if (cursor_offset.x < state.Scroll.x)
                        state.Scroll.x = ImMath.ImTrunc(ImMath.ImMax(0.0f, cursor_offset.x - scroll_increment_x));
                    else if (cursor_offset.x - visible_width >= state.Scroll.x)
                        state.Scroll.x = ImMath.ImTrunc(cursor_offset.x - visible_width + scroll_increment_x);
                }
                else
                {
                    state.Scroll.x = 0.0f;
                }

                // Vertical scroll
                if (is_multiline)
                {
                    // Test if cursor is vertically visible
                    if (cursor_offset.y - g.FontSize < scroll_y)
                        new_scroll_y = ImMath.ImMax(0.0f, cursor_offset.y - g.FontSize);
                    else if (cursor_offset.y - (inner_size.y - style.FramePadding.y * 2.0f) >= scroll_y)
                        new_scroll_y = cursor_offset.y - inner_size.y + style.FramePadding.y * 2.0f;
                }
                state.CursorFollow = false;
            }
            if (state.CursorCenterY)
            {
                if (is_multiline)
                    new_scroll_y = cursor_offset.y - g.FontSize - (inner_size.y * 0.5f - style.FramePadding.y);
                state.CursorCenterY = false;
                render_cursor = false;
            }
            if (new_scroll_y != scroll_y)
            {
                float scroll_max_y = ImMath.ImMax((text_size_y + style.FramePadding.y * 2.0f) - inner_size.y, 0.0f);
                scroll_y = ImMath.ImClamp(new_scroll_y, 0.0f, scroll_max_y);
                draw_pos.y += (draw_window.Scroll.y - scroll_y);   // Manipulate cursor pos immediately avoid a frame of lag
                draw_window.Scroll.y = scroll_y;
                CalcClipRectVisibleItemsY(clip_rect, draw_pos, g.FontSize, out line_visible_n0, out line_visible_n1);
                line_visible_n1 = ImMath.ImMin(line_visible_n1, line_count);
            }

            // Draw selection
            draw_scroll.x = state.Scroll.x;
            if (render_selection)
            {
                ImU32 bg_color = GetColorU32(ImGuiCol_TextSelectedBg, render_cursor ? 1.0f : 0.6f); // FIXME: current code flow mandate that render_cursor is always true here, we are leaving the transparent one for tests.
                float bg_offy_up = is_multiline ? 0.0f : -1.0f; // FIXME-DPI: those offsets should be part of the style? they don't play so well with multi-line selection.
                float bg_offy_dn = is_multiline ? 0.0f : 2.0f;
                float bg_eol_width = ImMath.ImTrunc(g.FontBaked.GetCharAdvance((ImWchar)' ') * 0.50f); // So we can see selected empty lines

                int text_selected_begin = ImMath.ImMin(state.Stb.select_start, state.Stb.select_end);
                int text_selected_end = ImMath.ImMax(state.Stb.select_start, state.Stb.select_end);
                for (int line_n = line_visible_n0; line_n < line_visible_n1; line_n++)
                {
                    int p = line_index.get_line_begin(line_n);
                    int p_eol = line_index.get_line_end(line_n);
                    bool p_eol_is_wrap = (p_eol < buf_display_end && buf_display_bytes[p_eol] != (byte)'\n');
                    if (p_eol_is_wrap)
                        p_eol++;
                    int line_selected_begin = (text_selected_begin > p) ? text_selected_begin : p;
                    int line_selected_end = (text_selected_end < p_eol) ? text_selected_end : p_eol;

                    float rect_width = 0.0f;
                    if (line_selected_begin < line_selected_end)
                        rect_width += CalcTextSize(BytesToString(buf_display_bytes, line_selected_begin, line_selected_end - line_selected_begin), -1, true).x;
                    if (text_selected_begin <= p_eol && text_selected_end > p_eol && !p_eol_is_wrap)
                        rect_width += bg_eol_width; // So we can see selected empty lines
                    if (rect_width == 0.0f)
                        continue;

                    ImRect rect = new ImRect();
                    rect.Min.x = draw_pos.x - draw_scroll.x + CalcTextSize(BytesToString(buf_display_bytes, p, line_selected_begin - p), -1, true).x;
                    rect.Min.y = draw_pos.y - draw_scroll.y + line_n * g.FontSize;
                    rect.Max.x = rect.Min.x + rect_width;
                    rect.Max.y = rect.Min.y + bg_offy_dn + g.FontSize;
                    rect.Min.y += bg_offy_up;
                    rect.ClipWith(clip_rect);
                    draw_window.DrawList.AddRectFilled(rect.Min, rect.Max, bg_color);
                }
            }
        }

        // Find render position for right alignment (single-line only)
        if (g.ActiveId != id && (flags & ImGuiInputTextFlags_ElideLeft) != 0 && !render_cursor && !render_selection)
            draw_pos.x = ImMath.ImMin(draw_pos.x, frame_bb.Max.x - CalcTextSize(BytesToString(buf_display_bytes, 0, buf_display_end > 0 ? buf_display_end : 0), -1, true).x - style.FramePadding.x);
        //draw_scroll.x = state->Scroll.x; // Preserve scroll when inactive?

        // Render text
        if ((is_multiline || (buf_display_end - 0) < buf_display_max_length) && (text_col & ImGuiCore.IM_COL32_A_MASK) != 0 && (line_visible_n0 < line_visible_n1))
        {
            int line_begin = line_index.get_line_begin(line_visible_n0);
            int line_end = line_index.get_line_end(line_visible_n1 - 1);
            string render_str = BytesToString(buf_display_bytes, line_begin, line_end - line_begin);
            g.Font.RenderText(draw_window.DrawList, g.FontSize,
                draw_pos - draw_scroll + new ImVec2(0.0f, line_visible_n0 * g.FontSize),
                text_col, new ImVec4(clip_rect.Min.x, clip_rect.Min.y, clip_rect.Max.x, clip_rect.Max.y),
                render_str, 0, render_str.Length,
                wrap_width, ImDrawTextFlags_WrapKeepBlanks | ImDrawTextFlags_CpuFineClip);
        }

        // Render blinking cursor
        if (render_cursor)
        {
            state.CursorAnim += io.DeltaTime;
            bool cursor_is_visible = (!g.IO.ConfigInputTextCursorBlink) || (state.CursorAnim <= 0.0f) || ImMath.ImFmod(state.CursorAnim, 1.20f) <= 0.80f;
            ImVec2 cursor_screen_pos = ImMath.ImTrunc(draw_pos + cursor_offset - draw_scroll);
            ImRect cursor_screen_rect = new ImRect(cursor_screen_pos.x, cursor_screen_pos.y - g.FontSize + 0.5f, cursor_screen_pos.x + 1.0f, cursor_screen_pos.y - 1.5f);
            if (cursor_is_visible && cursor_screen_rect.Overlaps(clip_rect))
                draw_window.DrawList.AddLineV(cursor_screen_rect.Min.x, cursor_screen_rect.Min.y, cursor_screen_rect.Max.y, GetColorU32(ImGuiCol_InputTextCursor), style.InputTextCursorSize);

            // Notify OS of text input position for advanced IME (-1 x offset so that Windows IME can cover our cursor. Bit of an extra nicety.)
            // This is required for some backends (SDL3) to start emitting character/text inputs.
            // As per #6341, make sure we don't set that on the deactivating frame.
            if (!is_readonly && g.ActiveId == id)
            {
                ImGuiPlatformImeData ime_data = g.PlatformImeData; // (this is a public struct, passed to io.Platform_SetImeDataFn() handler)
                ime_data.WantVisible = true;
                ime_data.WantTextInput = true;
                ime_data.InputPos = new ImVec2(cursor_screen_pos.x - 1.0f, cursor_screen_pos.y - g.FontSize);
                ime_data.InputLineHeight = g.FontSize;
                ime_data.ViewportId = window.Viewport.ID;
            }
        }

        if (is_password && !is_displaying_hint)
            PopPasswordFont();

        if (is_multiline)
        {
            // For focus requests to work on our multiline we need to ensure our child ItemAdd() call specifies the ImGuiItemFlags_Inputable (see #4761, #7870)...
            Dummy(new ImVec2(0.0f, text_size_y + style.FramePadding.y));
            g.NextItemData.ItemFlagsSet |= ImGuiItemFlags_Inputable | ImGuiItemFlags_NoTabStop;
            EndChild();
            item_data_backup.StatusFlags |= (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_HoveredWindow);

            // ...and then we need to undo the group overriding last item data, which gets a bit messy as EndGroup() tries to forward scrollbar being active...
            // FIXME: This quite messy/tricky, should attempt to get rid of the child window.
            EndGroup();
            if (g.LastItemData.ID == 0 || g.LastItemData.ID != GetWindowScrollbarID(draw_window, ImGuiAxis_Y))
            {
                g.LastItemData.ID = id;
                g.LastItemData.ItemFlags = item_data_backup.ItemFlags;
                g.LastItemData.StatusFlags = item_data_backup.StatusFlags;
            }
        }
        if (state != null && is_readonly)
            state.TextSrc = null;

        // Log as text
        if (g.LogEnabled && (!is_password || is_displaying_hint))
        {
            LogSetNextTextDecoration("{", "}");
            LogRenderedText(draw_pos, BytesToString(buf_display_bytes, 0, buf_display_end > 0 ? buf_display_end : 0));
        }

        if (label_size.x > 0)
            RenderText(new ImVec2(frame_bb.Max.x + style.ItemInnerSpacing.x, frame_bb.Min.y + style.FramePadding.y), label, label_end, false);

        if (value_changed)
            MarkItemEdited(id);

        // IMGUI_TEST_ENGINE_ITEM_INFO(id, label, g.LastItemData.StatusFlags | ImGuiItemStatusFlags_Inputable); // (omitted)
        if ((flags & ImGuiInputTextFlags_EnterReturnsTrue) != 0)
            return validated;
        else
            return value_changed;
    }

    // C++: void ImGui::DebugNodeInputTextState(ImGuiInputTextState* state)
    public static void DebugNodeInputTextState(ImGuiInputTextState state)
    {
        ImGuiContext g = GImGui;
        ImStbTexteditState stb_state = state.Stb;
        Text("ID: 0x{0:X8}, ActiveID: 0x{1:X8}", state.ID, g.ActiveId);
        DebugLocateItemOnHover(state.ID);
        Text("TextLen: {0}, Cursor: {1}{2}, Selection: {3}..{4}", state.TextLen, stb_state.cursor,
            (state.Flags & ImGuiInputTextFlags_WordWrap) != 0 ? (state.LastMoveDirectionLR == ImGuiDir_Left ? " (L)" : " (R)") : "",
            stb_state.select_start, stb_state.select_end);
        Text("BufCapacity: {0}, LineCount: {1}", state.BufCapacity, state.LineCount);
        Text("(Internal Buffer: TextA Size: {0}, Capacity: {1})", state.TextA.Size, state.TextA.Capacity);
        Text("has_preferred_x: {0} ({1})", stb_state.has_preferred_x != 0, stb_state.preferred_x);
        Text("undo_point: {0}, redo_point: {1}, undo_char_point: {2}, redo_char_point: {3}", stb_state.undo_point, stb_state.redo_point, stb_state.undo_char_point, stb_state.redo_char_point);
        if (BeginChild("undopoints", new ImVec2(0.0f, GetTextLineHeight() * 10), ImGuiChildFlags_Borders | ImGuiChildFlags_ResizeY)) // Visualize undo state
        {
            PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(0, 0));
            for (int n = 0; n < ImStbTextEdit.IMSTB_TEXTEDIT_UNDOSTATECOUNT; n++)
            {
                StbUndoRecord undo_rec = stb_state.undo_rec[n];
                char undo_rec_type = (n < stb_state.undo_point) ? 'u' : (n >= stb_state.redo_point) ? 'r' : ' ';
                if (undo_rec_type == ' ')
                    BeginDisabled(true);
                int buf_preview_len = (undo_rec_type != ' ' && undo_rec.char_storage != -1) ? undo_rec.insert_length : 0;
                string buf_preview_str = (undo_rec_type != ' ' && undo_rec.char_storage >= 0) ? Encoding.UTF8.GetString(stb_state.undo_char, undo_rec.char_storage, Math.Min(buf_preview_len, stb_state.undo_char.Length - undo_rec.char_storage)) : "";
                Text("{0} [{1:D2}] where {2:D3}, insert {3:D3}, delete {4:D3}, char_storage {5:D3} \"{6}\"",
                    undo_rec_type, n, undo_rec.where, undo_rec.insert_length, undo_rec.delete_length, undo_rec.char_storage, buf_preview_str);
                if (undo_rec_type == ' ')
                    EndDisabled();
            }
            PopStyleVar();
        }
        EndChild();
    }

    //-------------------------------------------------------------------------
    // C# helpers for byte-buffer string semantics (C++ char* equivalents)
    //-------------------------------------------------------------------------

    // C++ strlen() on a zero-terminated byte buffer.
    internal static int StrlenBytes(byte[] b, int start)
    {
        int n = 0;
        while (start + n < b.Length && b[start + n] != 0)
            n++;
        return n;
    }

    // Decode a UTF-8 byte range into a string.
    internal static string BytesToString(byte[] b, int start, int count)
    {
        if (b == null || count <= 0 || start >= b.Length)
            return string.Empty;
        return Encoding.UTF8.GetString(b, start, Math.Min(count, b.Length - start));
    }

    // C++ ImStrncpy(dst, src, count): copy up to count-1 bytes of 'src', zero-terminate and zero-pad (strncpy semantics).
    internal static void ImStrncpyBytes(byte[] dst, int dst_off, string src, int count)
    {
        if (count < 1)
            return;
        byte[] src_bytes = Encoding.UTF8.GetBytes(src ?? "");
        int n = Math.Min(src_bytes.Length, count - 1);
        if (n > 0)
            Array.Copy(src_bytes, 0, dst, dst_off, n);
        for (int i = n; i < count && dst_off + i < dst.Length; i++)
            dst[dst_off + i] = 0;
    }

    // C++ memcmp(a, b, n) != 0 (n == int.MaxValue means "compare zero-terminated contents", i.e. strcmp).
    private static bool BytesEqualN(byte[] a, byte[] b, int n)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null)
            return a == b;
        int limit = Math.Min(a.Length, b.Length);
        for (int i = 0; i < limit; i++)
        {
            if (i >= n)
                return true;
            if (a[i] != b[i])
                return false;
            if (n == int.MaxValue && a[i] == 0)
                return true; // zero-terminators reached simultaneously (strcmp)
        }
        return n == int.MaxValue ? false : true; // one buffer is a prefix of the other
    }
}
