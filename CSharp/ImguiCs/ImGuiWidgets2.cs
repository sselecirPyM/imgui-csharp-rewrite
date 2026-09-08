// Port of Dear ImGui imgui_widgets.cpp lines 1921-3600:
//   [SECTION] Widgets: ComboBox
//   [SECTION] Data Type and Data Formatting Helpers [Internal]
//   [SECTION] Widgets: DragScalar, DragFloat, DragInt, etc.
//   [SECTION] Widgets: SliderScalar, SliderFloat, SliderInt, etc.
//
// Porting notes:
// - C++ `void* p_data` scalar cores are ported as (IntPtr p_data + ImGuiDataType data_type) raw-pointer
//   APIs (DragScalar/DragScalarN/DragBehavior/SliderScalar/SliderBehavior...); deref is done through typed
//   pointers inside unsafe methods. The C++ DragBehaviorT<>/SliderBehaviorT<> templates are ported as
//   generic DragBehaviorT<T,TS,TF> using System.Numerics.INumber<T> so each instantiation keeps the exact
//   C++ math type (TYPE/SIGNEDTYPE/FLOATTYPE): S8/S16 widen to ImS32/ImU32, Float uses float math,
//   S64/U64/Double use double math, etc. - exactly like the C++ template instantiations.
// - Public typed wrappers (DragFloat/DragInt/SliderFloat/...) take `ref`/array values and pass &value via
//   unsafe interior pointers, mirroring the C++ `&v_min` call sites.
using System;
using System.Diagnostics;
using System.Numerics;

namespace ImGui;

// C++: struct ImGuiDataTypeInfo (imgui_internal.h)
public struct ImGuiDataTypeInfo
{
    public int Size;            // Size in bytes (C++: size_t)
    public string Name;         // Short descriptive name for the type, for debugging
    public string PrintFmt;     // Default printf format for the type
    public string ScanFmt;      // Default scanf format for the type

    public ImGuiDataTypeInfo(int size, string name, string print_fmt, string scan_fmt)
    {
        Size = size; Name = name; PrintFmt = print_fmt; ScanFmt = scan_fmt;
    }
}

public static unsafe partial class ImGui
{
    //-------------------------------------------------------------------------
    // [SECTION] Widgets: ComboBox
    //-------------------------------------------------------------------------

    // C++ imgui_internal.h integer limits (IM_S8_MIN etc.)
    private const int IM_S8_MIN = -128;
    private const int IM_S8_MAX = 127;
    private const int IM_U8_MIN = 0;
    private const int IM_U8_MAX = 255;
    private const int IM_S16_MIN = -32768;
    private const int IM_S16_MAX = 32767;
    private const int IM_U16_MIN = 0;
    private const int IM_U16_MAX = 65535;
    private const int IM_S32_MIN = int.MinValue;
    private const int IM_S32_MAX = int.MaxValue;
    private const uint IM_U32_MIN = 0;
    private const uint IM_U32_MAX = uint.MaxValue;
    private const long IM_S64_MIN = long.MinValue;
    private const long IM_S64_MAX = long.MaxValue;
    private const ulong IM_U64_MIN = 0;
    private const ulong IM_U64_MAX = ulong.MaxValue;

    // C++: #define FLT_MIN (imgui_float.h): smallest positive normalized float
    private const float FLT_MIN = 1.17549435e-38f;

    // C++: #define DRAG_MOUSE_THRESHOLD_FACTOR 0.50f (imgui_internal.h)
    private const float DRAG_MOUSE_THRESHOLD_FACTOR = 0.50f;

    // imgui_internal.h macros turned into local accessors (same as ImGuiNav.cs)
    static ImGuiKey ImGuiKey_NavKeyboardTweakSlow => ImGuiMod_Ctrl;
    static ImGuiKey ImGuiKey_NavKeyboardTweakFast => ImGuiMod_Shift;

    // C++ imgui_widgets.cpp top-of-file static (in the Data Type section)
    private static readonly ImU32[] GDefaultRgbaColorMarkers =
    {
        ImGuiCore.IM_COL32(240, 20, 20, 255), ImGuiCore.IM_COL32(20, 240, 20, 255), ImGuiCore.IM_COL32(20, 20, 240, 255), ImGuiCore.IM_COL32(140, 140, 140, 255),
    };

    private static float CalcMaxPopupHeightFromItemCount(int items_count)
    {
        ImGuiContext g = GImGui;
        if (items_count <= 0)
            return float.MaxValue;
        return (g.FontSize + g.Style.ItemSpacing.y) * items_count - g.Style.ItemSpacing.y + (g.Style.WindowPadding.y * 2);
    }

    public static bool BeginCombo(string label, string preview_value, ImGuiComboFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = GetCurrentWindow();

        ImGuiNextWindowDataFlags backup_next_window_data_flags = g.NextWindowData.HasFlags;
        g.NextWindowData.ClearFlags(); // We behave like Begin() and need to consume those values
        if (window.SkipItems)
            return false;

        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);
        Debug.Assert((flags & (ImGuiComboFlags_NoArrowButton | ImGuiComboFlags_NoPreview)) != (ImGuiComboFlags_NoArrowButton | ImGuiComboFlags_NoPreview)); // Can't use both flags together
        if ((flags & ImGuiComboFlags_WidthFitPreview) != 0)
            Debug.Assert((flags & (ImGuiComboFlags_NoPreview | ImGuiComboFlags_CustomPreview)) == 0);

        float arrow_size = (flags & ImGuiComboFlags_NoArrowButton) != 0 ? 0.0f : GetFrameHeight();
        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        float preview_width = ((flags & ImGuiComboFlags_WidthFitPreview) != 0 && (preview_value != null)) ? CalcTextSize(preview_value, -1, false).x : 0.0f;
        float w = (flags & ImGuiComboFlags_NoPreview) != 0 ? arrow_size : ((flags & ImGuiComboFlags_WidthFitPreview) != 0 ? (arrow_size + preview_width + style.FramePadding.x * 2.0f) : CalcItemWidth());
        ImRect bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + new ImVec2(w, label_size.y + style.FramePadding.y * 2.0f));
        ImRect total_bb = new ImRect(bb.Min, bb.Max + new ImVec2(label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f, 0.0f));
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(total_bb, id, bb))
            return false;

        // Open on click
        bool pressed = ButtonBehavior(bb, id, out bool hovered, out bool held);
        ImGuiID popup_id = ImGuiHash.ImHashStr("##ComboPopup", 0, id);
        bool popup_open = IsPopupOpen(popup_id, ImGuiPopupFlags_None);
        if (pressed && !popup_open)
        {
            OpenPopupEx(popup_id, ImGuiPopupFlags_None);
            popup_open = true;
        }

        // Render shape
        ImU32 frame_col = GetColorU32(hovered ? ImGuiCol_FrameBgHovered : ImGuiCol_FrameBg);
        float value_x2 = ImMath.ImMax(bb.Min.x, bb.Max.x - arrow_size);
        RenderNavCursor(bb, id);
        if ((flags & ImGuiComboFlags_NoPreview) == 0)
            window.DrawList.AddRectFilled(bb.Min, new ImVec2(value_x2, bb.Max.y), frame_col, style.FrameRounding, (flags & ImGuiComboFlags_NoArrowButton) != 0 ? ImDrawFlags_RoundCornersAll : ImDrawFlags_RoundCornersLeft);
        if ((flags & ImGuiComboFlags_NoArrowButton) == 0)
        {
            ImU32 bg_col = GetColorU32((popup_open || hovered) ? ImGuiCol_ButtonHovered : ImGuiCol_Button);
            ImU32 text_col = GetColorU32(ImGuiCol_Text);
            window.DrawList.AddRectFilled(new ImVec2(value_x2, bb.Min.y), bb.Max, bg_col, style.FrameRounding, (w <= arrow_size) ? ImDrawFlags_RoundCornersAll : ImDrawFlags_RoundCornersRight);
            if (value_x2 + arrow_size - style.FramePadding.x <= bb.Max.x)
                RenderArrow(window.DrawList, new ImVec2(value_x2 + style.FramePadding.y, bb.Min.y + style.FramePadding.y), text_col, ImGuiDir_Down, 1.0f);
        }
        RenderFrameBorder(bb.Min, bb.Max, style.FrameRounding);

        // Custom preview
        if ((flags & ImGuiComboFlags_CustomPreview) != 0)
        {
            g.ComboPreviewData.PreviewRect = new ImRect(bb.Min.x, bb.Min.y, value_x2, bb.Max.y);
            Debug.Assert(preview_value == null || preview_value.Length == 0);
            preview_value = null;
        }

        // Render preview and label
        if (preview_value != null && (flags & ImGuiComboFlags_NoPreview) == 0)
        {
            if (g.LogEnabled)
                LogSetNextTextDecoration("{", "}");
            RenderTextClipped(bb.Min + style.FramePadding, new ImVec2(value_x2, bb.Max.y), preview_value);
        }
        if (label_size.x > 0)
            RenderText(new ImVec2(bb.Max.x + style.ItemInnerSpacing.x, bb.Min.y + style.FramePadding.y), label, label_end, false);

        if (!popup_open)
            return false;

        g.NextWindowData.HasFlags = backup_next_window_data_flags;
        return BeginComboPopup(popup_id, bb, flags);
    }

    public static bool BeginComboPopup(ImGuiID popup_id, ImRect bb, ImGuiComboFlags flags)
    {
        ImGuiContext g = GImGui;
        if (!IsPopupOpen(popup_id, ImGuiPopupFlags_None))
        {
            g.NextWindowData.ClearFlags();
            return false;
        }

        // Set popup size
        float w = bb.GetWidth();
        if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSizeConstraint) != 0)
        {
            g.NextWindowData.SizeConstraintRect.Min.x = ImMath.ImMax(g.NextWindowData.SizeConstraintRect.Min.x, w);
        }
        else
        {
            if ((flags & ImGuiComboFlags_HeightMask_) == 0)
                flags |= ImGuiComboFlags_HeightRegular;
            Debug.Assert(ImMath.ImIsPowerOfTwo(flags & ImGuiComboFlags_HeightMask_)); // Only one
            int popup_max_height_in_items = -1;
            if ((flags & ImGuiComboFlags_HeightRegular) != 0) popup_max_height_in_items = 8;
            else if ((flags & ImGuiComboFlags_HeightSmall) != 0) popup_max_height_in_items = 4;
            else if ((flags & ImGuiComboFlags_HeightLarge) != 0) popup_max_height_in_items = 20;
            ImVec2 constraint_min = new ImVec2(0.0f, 0.0f), constraint_max = new ImVec2(float.MaxValue, float.MaxValue);
            if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSize) == 0 || g.NextWindowData.SizeVal.x <= 0.0f) // Don't apply constraints if user specified a size
                constraint_min.x = w;
            if ((g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSize) == 0 || g.NextWindowData.SizeVal.y <= 0.0f)
                constraint_max.y = CalcMaxPopupHeightFromItemCount(popup_max_height_in_items);
            SetNextWindowSizeConstraints(constraint_min, constraint_max);
        }

        // This is essentially a specialized version of BeginPopupEx()
        string name = ImFmt.Sprintf("##Combo_%02d", g.BeginComboDepth); // Recycle windows based on depth

        // Set position given a custom constraint (peak into expected window size so we can position it)
        // FIXME: This might be easier to express with an hypothetical SetNextWindowPosConstraints() function?
        // FIXME: This might be moved to Begin() or at least around the same spot where Tooltips and other Popups are calling FindBestWindowPosForPopupEx()?
        ImGuiWindow popup_window = FindWindowByName(name);
        if (popup_window != null)
            if (popup_window.WasActive)
            {
                // Always override 'AutoPosLastDirection' to not leave a chance for a past value to affect us.
                ImVec2 size_expected = CalcWindowNextAutoFitSize(popup_window);
                popup_window.AutoPosLastDirection = (flags & ImGuiComboFlags_PopupAlignLeft) != 0 ? ImGuiDir_Left : ImGuiDir_Down; // Left = "Below, Toward Left", Down = "Below, Toward Right (default)"
                ImRect r_outer = GetPopupAllowedExtentRect(popup_window);
                ImVec2 pos = FindBestWindowPosForPopupEx(bb.GetBL(), size_expected, ref popup_window.AutoPosLastDirection, r_outer, bb, ImGuiPopupPositionPolicy_ComboBox);
                SetNextWindowPos(pos);
            }

        // We don't use BeginPopupEx() solely because we have a custom name string, which we could make an argument to BeginPopupEx()
        ImGuiWindowFlags window_flags = ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_Popup | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoMove;
        PushStyleVarX(ImGuiStyleVar_WindowPadding, g.Style.FramePadding.x); // Horizontally align ourselves with the framed text
        bool ret = Begin(name, window_flags);
        PopStyleVar();
        if (!ret)
        {
            EndPopup();
            if (!g.IO.ConfigDebugBeginReturnValueOnce && !g.IO.ConfigDebugBeginReturnValueLoop) // Begin may only return false with those debug tools activated.
                Debug.Assert(false); // This should never happen as we tested for IsPopupOpen() above
            return false;
        }
        g.BeginComboDepth++;
        return true;
    }

    public static void EndCombo()
    {
        ImGuiContext g = GImGui;
        g.BeginComboDepth--;
        string name = ImFmt.Sprintf("##Combo_%02d", g.BeginComboDepth); // FIXME: Move those to helpers?
        if (g.CurrentWindow.Name != name)
        {
            Debug.Assert(false, "Calling EndCombo() in wrong window!"); // IM_ASSERT_USER_ERROR_RET
            return;
        }
        EndPopup();
    }

    // Call directly after the BeginCombo/EndCombo block. The preview is designed to only host non-interactive elements
    // (Experimental, see GitHub issues: #1658, #4168)
    public static bool BeginComboPreview()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        // C++: ImGuiComboPreviewData* preview_data = &g.ComboPreviewData;
        ref ImGuiComboPreviewData preview_data = ref g.ComboPreviewData;

        if (window.SkipItems || (g.LastItemData.StatusFlags & ImGuiItemStatusFlags_Visible) == 0)
            return false;
        Debug.Assert(g.LastItemData.Rect.Min.x == preview_data.PreviewRect.Min.x && g.LastItemData.Rect.Min.y == preview_data.PreviewRect.Min.y); // Didn't call after BeginCombo/EndCombo block or forgot to pass ImGuiComboFlags_CustomPreview flag?
        if (!window.ClipRect.Overlaps(preview_data.PreviewRect)) // Narrower test (optional)
            return false;

        // FIXME: This could be contained in a PushWorkRect() api
        preview_data.BackupCursorPos = window.DC.CursorPos;
        preview_data.BackupCursorMaxPos = window.DC.CursorMaxPos;
        preview_data.BackupCursorPosPrevLine = window.DC.CursorPosPrevLine;
        preview_data.BackupPrevLineTextBaseOffset = window.DC.PrevLineTextBaseOffset;
        preview_data.BackupLayout = window.DC.LayoutType;
        window.DC.CursorPos = preview_data.PreviewRect.Min + g.Style.FramePadding;
        window.DC.CursorMaxPos = window.DC.CursorPos;
        window.DC.LayoutType = ImGuiLayoutType_Horizontal;
        window.DC.IsSameLine = false;
        PushClipRect(preview_data.PreviewRect.Min, preview_data.PreviewRect.Max, true);

        return true;
    }

    public static void EndComboPreview()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ref ImGuiComboPreviewData preview_data = ref g.ComboPreviewData;

        // FIXME: Using CursorMaxPos approximation instead of correct AABB which we will store in ImDrawCmd in the future
        ImDrawList draw_list = window.DrawList;
        if (window.DC.CursorMaxPos.x < preview_data.PreviewRect.Max.x && window.DC.CursorMaxPos.y < preview_data.PreviewRect.Max.y)
            if (draw_list.CmdBuffer.Size > 1) // Unlikely case that the PushClipRect() didn't create a command
            {
                ImVec4 clip = draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 2].ClipRect;
                ImDrawCmd last_cmd = draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1];
                last_cmd.ClipRect = clip;
                draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1] = last_cmd;
                draw_list._CmdHeader.ClipRect = clip;
                // C++: draw_list->_TryMergeDrawCmds(); (private in the C# ImDrawList - inlined here)
                {
                    int last = draw_list.CmdBuffer.Size - 1;
                    ImDrawCmd curr_cmd = draw_list.CmdBuffer.Data[last];
                    ImDrawCmd prev_cmd = draw_list.CmdBuffer.Data[last - 1];
                    if (ImDrawListUtil.ImDrawCmd_HeaderCompare(curr_cmd, prev_cmd) == 0 && ImDrawListUtil.ImDrawCmd_AreSequentialIdxOffset(prev_cmd, curr_cmd) && curr_cmd.UserCallback == null && prev_cmd.UserCallback == null)
                    {
                        prev_cmd.ElemCount += curr_cmd.ElemCount;
                        draw_list.CmdBuffer.Data[last - 1] = prev_cmd;
                        draw_list.CmdBuffer.pop_back();
                    }
                }
            }
        PopClipRect();
        window.DC.CursorPos = preview_data.BackupCursorPos;
        window.DC.CursorMaxPos = ImMath.ImMax(window.DC.CursorMaxPos, preview_data.BackupCursorMaxPos);
        window.DC.CursorPosPrevLine = preview_data.BackupCursorPosPrevLine;
        window.DC.PrevLineTextBaseOffset = preview_data.BackupPrevLineTextBaseOffset;
        window.DC.LayoutType = preview_data.BackupLayout;
        window.DC.IsSameLine = false;
        preview_data.PreviewRect = default;
    }

    // Getter for the old Combo() API: const char*[]
    private static string Items_ArrayGetter(object data, int idx)
    {
        string[] items = (string[])data;
        return items[idx];
    }

    // Getter for the old Combo() API: "item1\0item2\0item3\0"
    private static string Items_SingleStringGetter(object data, int idx)
    {
        string items_separated_by_zeros = (string)data;
        int items_count = 0;
        int p = 0;
        while (p < items_separated_by_zeros.Length && items_separated_by_zeros[p] != '\0')
        {
            if (idx == items_count)
                break;
            while (p < items_separated_by_zeros.Length && items_separated_by_zeros[p] != '\0') // C++: p += ImStrlen(p) + 1;
                p++;
            p += 1;
            items_count++;
        }
        if (p >= items_separated_by_zeros.Length || items_separated_by_zeros[p] == '\0')
            return null;
        int e = p;
        while (e < items_separated_by_zeros.Length && items_separated_by_zeros[e] != '\0')
            e++;
        return items_separated_by_zeros.Substring(p, e - p);
    }

    // Old API, prefer using BeginCombo() nowadays if you can.
    public static bool Combo(string label, ref int current_item, Func<object, int, string> getter, object user_data, int items_count, int popup_max_height_in_items = -1)
    {
        ImGuiContext g = GImGui;

        // Call the getter to obtain the preview string which is a parameter to BeginCombo()
        string preview_value = null;
        if (current_item >= 0 && current_item < items_count)
            preview_value = getter(user_data, current_item);

        // The old Combo() API exposed "popup_max_height_in_items". The new more general BeginCombo() API doesn't have/need it, but we emulate it here.
        if (popup_max_height_in_items != -1 && (g.NextWindowData.HasFlags & ImGuiNextWindowDataFlags_HasSizeConstraint) == 0)
            SetNextWindowSizeConstraints(new ImVec2(0, 0), new ImVec2(float.MaxValue, CalcMaxPopupHeightFromItemCount(popup_max_height_in_items)));

        if (!BeginCombo(label, preview_value, ImGuiComboFlags_None))
            return false;

        // Display items
        bool value_changed = false;
        ImGuiListClipper clipper = new ImGuiListClipper();
        clipper.Begin(items_count);
        clipper.IncludeItemByIndex(current_item);
        while (clipper.Step())
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                string item_text = getter(user_data, i);
                if (item_text == null)
                    item_text = "*Unknown item*";

                PushID(i);
                bool item_selected = (i == current_item);
                if (Selectable(item_text, item_selected) && current_item != i)
                {
                    value_changed = true;
                    current_item = i;
                }
                if (item_selected)
                    SetItemDefaultFocus();
                PopID();
            }

        EndCombo();
        if (value_changed)
            MarkItemEdited(g.LastItemData.ID);

        return value_changed;
    }

    // Combo box helper allowing to pass an array of strings.
    public static bool Combo(string label, ref int current_item, string[] items, int items_count, int height_in_items = -1)
    {
        bool value_changed = Combo(label, ref current_item, Items_ArrayGetter, items, items_count, height_in_items);
        return value_changed;
    }

    // Combo box helper allowing to pass all items in a single string literal holding multiple zero-terminated items "item1\0item2\0"
    public static bool Combo(string label, ref int current_item, string items_separated_by_zeros, int height_in_items = -1)
    {
        int items_count = 0;
        int p = 0; // FIXME-OPT: Avoid computing this, or at least only when combo is open
        while (p < items_separated_by_zeros.Length && items_separated_by_zeros[p] != '\0')
        {
            while (p < items_separated_by_zeros.Length && items_separated_by_zeros[p] != '\0') // C++: p += ImStrlen(p) + 1;
                p++;
            p++;
            items_count++;
        }
        bool value_changed = Combo(label, ref current_item, Items_SingleStringGetter, items_separated_by_zeros, items_count, height_in_items);
        return value_changed;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Data Type and Data Formatting Helpers [Internal]
    //-------------------------------------------------------------------------

    // C++: static const ImGuiDataTypeInfo GDataTypeInfo[]
    // (MSVC/non-MSVC variants: we keep the C99 "%lld"/"%llu" forms which ImFmt supports.)
    public static readonly ImGuiDataTypeInfo[] GDataTypesInfo =
    {
        new(sizeof(byte),    "S8",     "%d",   "%d"   ),  // ImGuiDataType_S8
        new(sizeof(byte),    "U8",     "%u",   "%u"   ),
        new(sizeof(short),   "S16",    "%d",   "%d"   ),  // ImGuiDataType_S16
        new(sizeof(ushort),  "U16",    "%u",   "%u"   ),
        new(sizeof(int),     "S32",    "%d",   "%d"   ),  // ImGuiDataType_S32
        new(sizeof(uint),    "U32",    "%u",   "%u"   ),
        new(sizeof(long),    "S64",    "%lld", "%lld" ),  // ImGuiDataType_S64
        new(sizeof(ulong),   "U64",    "%llu", "%llu" ),
        new(sizeof(float),   "float",  "%.3f", "%f"   ),  // ImGuiDataType_Float (float are promoted to double in va_arg)
        new(sizeof(double),  "double", "%f",   "%lf"  ),  // ImGuiDataType_Double
        new(sizeof(bool),    "bool",   "%d",   "%d"   ),  // ImGuiDataType_Bool
        new(0,               "char*",  "%s",   "%s"   ),  // ImGuiDataType_String
    };
    // IM_STATIC_ASSERT(IM_COUNTOF(GDataTypeInfo) == ImGuiDataType_COUNT);

    public static ImGuiDataTypeInfo DataTypeGetInfo(ImGuiDataType data_type)
    {
        Debug.Assert(data_type >= 0 && data_type < ImGuiDataType_COUNT);
        return GDataTypesInfo[data_type];
    }

    // C++: int ImGui::DataTypeFormatString(char* buf, int buf_size, ImGuiDataType data_type, const void* p_data, const char* format)
    // Port note: C# strings are immutable, so this returns the formatted string instead of writing into a char buffer.
    public static string DataTypeFormatString(ImGuiDataType data_type, IntPtr p_data, string format)
    {
        void* p = (void*)p_data;
        // Signedness doesn't matter when pushing integer arguments
        if (data_type == ImGuiDataType_S32 || data_type == ImGuiDataType_U32)
            return ImFmt.Sprintf(format, *(int*)p);   // C++ passes the ImU32 bit-pattern; pass signed storage so both %d and %u format identically
        if (data_type == ImGuiDataType_S64 || data_type == ImGuiDataType_U64)
            return ImFmt.Sprintf(format, *(long*)p);
        if (data_type == ImGuiDataType_Float)
            return ImFmt.Sprintf(format, *(float*)p);
        if (data_type == ImGuiDataType_Double)
            return ImFmt.Sprintf(format, *(double*)p);
        if (data_type == ImGuiDataType_S8)
            return ImFmt.Sprintf(format, *(sbyte*)p);
        if (data_type == ImGuiDataType_U8)
            return ImFmt.Sprintf(format, *(byte*)p);
        if (data_type == ImGuiDataType_S16)
            return ImFmt.Sprintf(format, *(short*)p);
        if (data_type == ImGuiDataType_U16)
            return ImFmt.Sprintf(format, *(ushort*)p);
        Debug.Assert(false);
        return null;
    }

    // Non-allocating variant of DataTypeFormatString(): formats into dst and returns the
    // number of characters needed (caller falls back when this exceeds dst.Length).
    public static int DataTypeFormatStringInto(ImGuiDataType data_type, IntPtr p_data, string format, Span<char> dst)
    {
        void* p = (void*)p_data;
        if (data_type == ImGuiDataType_S32 || data_type == ImGuiDataType_U32)
            return ImFmt.FormatInto(dst, format, *(int*)p);
        if (data_type == ImGuiDataType_S64 || data_type == ImGuiDataType_U64)
            return ImFmt.FormatInto(dst, format, *(long*)p);
        if (data_type == ImGuiDataType_Float)
            return ImFmt.FormatInto(dst, format, *(float*)p);
        if (data_type == ImGuiDataType_Double)
            return ImFmt.FormatInto(dst, format, *(double*)p);
        if (data_type == ImGuiDataType_S8)
            return ImFmt.FormatInto(dst, format, *(sbyte*)p);
        if (data_type == ImGuiDataType_U8)
            return ImFmt.FormatInto(dst, format, *(byte*)p);
        if (data_type == ImGuiDataType_S16)
            return ImFmt.FormatInto(dst, format, *(short*)p);
        if (data_type == ImGuiDataType_U16)
            return ImFmt.FormatInto(dst, format, *(ushort*)p);
        Debug.Assert(false);
        return -1;
    }

    // Renders the formatted slider/drag value without a per-frame string allocation
    // (C++ formats into a stack buffer).
    static void RenderValueTextClipped(ImGuiDataType data_type, IntPtr p_data, string format, ImVec2 pos_min, ImVec2 pos_max, ImVec2 align)
    {
        Span<char> buf = stackalloc char[64];
        int n = DataTypeFormatStringInto(data_type, p_data, format, buf);
        if (n < 0 || n > buf.Length)
        {
            string value_buf = DataTypeFormatString(data_type, p_data, format);
            RenderTextClipped(pos_min, pos_max, value_buf, align: align);
            return;
        }
        RenderTextClipped(pos_min, pos_max, buf.Slice(0, n), align: align);
    }

    // C++ imgui_internal.h: template<typename T> T ImAddClampOverflow(const T a, const T b, const T mn, const T mx)
    private static T ImAddClampOverflow<T>(T a, T b, T mn, T mx) where T : INumber<T>
    {
        if (b < T.Zero && a < mn - b) return mn;
        if (b > T.Zero && a > mx - b) return mx;
        return a + b;
    }

    // C++ imgui_internal.h: template<typename T> T ImSubClampOverflow(const T a, const T b, const T mn, const T mx)
    private static T ImSubClampOverflow<T>(T a, T b, T mn, T mx) where T : INumber<T>
    {
        if (b > T.Zero && a < mn + b) return mn;
        if (b < T.Zero && a > mx + b) return mx;
        return a - b;
    }

    public static void DataTypeApplyOp(ImGuiDataType data_type, int op, IntPtr output, IntPtr arg1, IntPtr arg2)
    {
        Debug.Assert(op == '+' || op == '-');
        void* o = (void*)output;
        void* a1 = (void*)arg1;
        void* a2 = (void*)arg2;
        switch (data_type)
        {
            case ImGuiDataType_S8:
                if (op == '+') { *(sbyte*)o = ImAddClampOverflow(*(sbyte*)a1, *(sbyte*)a2, (sbyte)IM_S8_MIN, (sbyte)IM_S8_MAX); }
                if (op == '-') { *(sbyte*)o = ImSubClampOverflow(*(sbyte*)a1, *(sbyte*)a2, (sbyte)IM_S8_MIN, (sbyte)IM_S8_MAX); }
                return;
            case ImGuiDataType_U8:
                if (op == '+') { *(byte*)o = ImAddClampOverflow(*(byte*)a1, *(byte*)a2, (byte)IM_U8_MIN, (byte)IM_U8_MAX); }
                if (op == '-') { *(byte*)o = ImSubClampOverflow(*(byte*)a1, *(byte*)a2, (byte)IM_U8_MIN, (byte)IM_U8_MAX); }
                return;
            case ImGuiDataType_S16:
                if (op == '+') { *(short*)o = ImAddClampOverflow(*(short*)a1, *(short*)a2, (short)IM_S16_MIN, (short)IM_S16_MAX); }
                if (op == '-') { *(short*)o = ImSubClampOverflow(*(short*)a1, *(short*)a2, (short)IM_S16_MIN, (short)IM_S16_MAX); }
                return;
            case ImGuiDataType_U16:
                if (op == '+') { *(ushort*)o = ImAddClampOverflow(*(ushort*)a1, *(ushort*)a2, (ushort)IM_U16_MIN, (ushort)IM_U16_MAX); }
                if (op == '-') { *(ushort*)o = ImSubClampOverflow(*(ushort*)a1, *(ushort*)a2, (ushort)IM_U16_MIN, (ushort)IM_U16_MAX); }
                return;
            case ImGuiDataType_S32:
                if (op == '+') { *(int*)o = ImAddClampOverflow(*(int*)a1, *(int*)a2, IM_S32_MIN, IM_S32_MAX); }
                if (op == '-') { *(int*)o = ImSubClampOverflow(*(int*)a1, *(int*)a2, IM_S32_MIN, IM_S32_MAX); }
                return;
            case ImGuiDataType_U32:
                if (op == '+') { *(uint*)o = ImAddClampOverflow(*(uint*)a1, *(uint*)a2, IM_U32_MIN, IM_U32_MAX); }
                if (op == '-') { *(uint*)o = ImSubClampOverflow(*(uint*)a1, *(uint*)a2, IM_U32_MIN, IM_U32_MAX); }
                return;
            case ImGuiDataType_S64:
                if (op == '+') { *(long*)o = ImAddClampOverflow(*(long*)a1, *(long*)a2, IM_S64_MIN, IM_S64_MAX); }
                if (op == '-') { *(long*)o = ImSubClampOverflow(*(long*)a1, *(long*)a2, IM_S64_MIN, IM_S64_MAX); }
                return;
            case ImGuiDataType_U64:
                if (op == '+') { *(ulong*)o = ImAddClampOverflow(*(ulong*)a1, *(ulong*)a2, IM_U64_MIN, IM_U64_MAX); }
                if (op == '-') { *(ulong*)o = ImSubClampOverflow(*(ulong*)a1, *(ulong*)a2, IM_U64_MIN, IM_U64_MAX); }
                return;
            case ImGuiDataType_Float:
                if (op == '+') { *(float*)o = *(float*)a1 + *(float*)a2; }
                if (op == '-') { *(float*)o = *(float*)a1 - *(float*)a2; }
                return;
            case ImGuiDataType_Double:
                if (op == '+') { *(double*)o = *(double*)a1 + *(double*)a2; }
                if (op == '-') { *(double*)o = *(double*)a1 - *(double*)a2; }
                return;
            case ImGuiDataType_COUNT: break;
        }
        Debug.Assert(false);
    }

    // User can input math operators (e.g. +100) to edit a numerical values.
    // NB: This is _not_ a full expression evaluator. We should probably add one and replace this dumb mess..
    public static bool DataTypeApplyFromText(string buf, ImGuiDataType data_type, IntPtr p_data, string format, IntPtr p_data_when_empty = default)
    {
        // Copy the value in an opaque buffer so we can compare at the end of the function if it changed at all.
        ImGuiDataTypeInfo type_info = DataTypeGetInfo(data_type);
        ImGuiDataTypeStorage data_backup;
        data_backup.Data = DataTypeStorageLoad(p_data, type_info.Size);

        int buf_pos = 0;
        while (buf_pos < buf.Length && ImText.ImCharIsBlankA(buf[buf_pos]))
            buf_pos++;
        if (buf_pos >= buf.Length) // C++: !buf[0]
        {
            if (p_data_when_empty != default)
            {
                DataTypeStorageStore(p_data, type_info.Size, DataTypeStorageLoad(p_data_when_empty, type_info.Size));
                return data_backup.Data != DataTypeStorageLoad(p_data, type_info.Size);
            }
            return false;
        }
        string text = buf.Substring(buf_pos);

        // Sanitize format
        // - For float/double we have to ignore format with precision (e.g. "%.2f") because sscanf doesn't take them in, so force them into %f and %lf
        if (data_type == ImGuiDataType_Float || data_type == ImGuiDataType_Double)
        {
            format = type_info.ScanFmt;
        }
        else
        {
            format = ImParseFormatSanitizeForScanning(format, out string format_sanitized);
            if (format.Length == 0)
                format = type_info.ScanFmt; // Format doesn't want us to show the number currently, but we still need to parse the resulting input
        }

        // Small types need a 32-bit buffer to receive the result from scanf()
        int v32 = 0;
        bool dest_is_data = type_info.Size >= 4;
        if (SscanfScalar(text, format, dest_is_data ? (void*)p_data : (void*)(&v32), dest_is_data ? type_info.Size : sizeof(int)) < 1)
            return false;
        if (type_info.Size < 4)
        {
            void* p = (void*)p_data;
            if (data_type == ImGuiDataType_S8)
                *(sbyte*)p = (sbyte)ImMath.ImClamp(v32, IM_S8_MIN, IM_S8_MAX);
            else if (data_type == ImGuiDataType_U8)
                *(byte*)p = (byte)ImMath.ImClamp(v32, IM_U8_MIN, IM_U8_MAX);
            else if (data_type == ImGuiDataType_S16)
                *(short*)p = (short)ImMath.ImClamp(v32, IM_S16_MIN, IM_S16_MAX);
            else if (data_type == ImGuiDataType_U16)
                *(ushort*)p = (ushort)ImMath.ImClamp(v32, IM_U16_MIN, IM_U16_MAX);
            else
                Debug.Assert(false);
        }

        return data_backup.Data != DataTypeStorageLoad(p_data, type_info.Size);
    }

    // C++: template<typename T> static int DataTypeCompareT(const T* lhs, const T* rhs)
    private static int DataTypeCompareT<T>(T lhs, T rhs) where T : INumber<T>
    {
        if (lhs < rhs) return -1;
        if (lhs > rhs) return +1;
        return 0;
    }

    public static int DataTypeCompare(ImGuiDataType data_type, IntPtr arg_1, IntPtr arg_2)
    {
        void* a1 = (void*)arg_1;
        void* a2 = (void*)arg_2;
        switch (data_type)
        {
            case ImGuiDataType_S8:     return DataTypeCompareT(*(sbyte*)a1, *(sbyte*)a2);
            case ImGuiDataType_U8:     return DataTypeCompareT(*(byte*)a1, *(byte*)a2);
            case ImGuiDataType_S16:    return DataTypeCompareT(*(short*)a1, *(short*)a2);
            case ImGuiDataType_U16:    return DataTypeCompareT(*(ushort*)a1, *(ushort*)a2);
            case ImGuiDataType_S32:    return DataTypeCompareT(*(int*)a1, *(int*)a2);
            case ImGuiDataType_U32:    return DataTypeCompareT(*(uint*)a1, *(uint*)a2);
            case ImGuiDataType_S64:    return DataTypeCompareT(*(long*)a1, *(long*)a2);
            case ImGuiDataType_U64:    return DataTypeCompareT(*(ulong*)a1, *(ulong*)a2);
            case ImGuiDataType_Float:  return DataTypeCompareT(*(float*)a1, *(float*)a2);
            case ImGuiDataType_Double: return DataTypeCompareT(*(double*)a1, *(double*)a2);
            case ImGuiDataType_COUNT:  break;
        }
        Debug.Assert(false);
        return 0;
    }

    // C++: template<typename T> static bool DataTypeClampT(T* v, const T* v_min, const T* v_max)
    private static bool DataTypeClampT<T>(void* v, void* v_min, void* v_max) where T : unmanaged, INumber<T>
    {
        // Clamp, both sides are optional, return true if modified
        if (v_min != null && *(T*)v < *(T*)v_min) { *(T*)v = *(T*)v_min; return true; }
        if (v_max != null && *(T*)v > *(T*)v_max) { *(T*)v = *(T*)v_max; return true; }
        return false;
    }

    public static bool DataTypeClamp(ImGuiDataType data_type, IntPtr p_data, IntPtr p_min, IntPtr p_max)
    {
        void* v = (void*)p_data;
        void* vmin = (void*)p_min;
        void* vmax = (void*)p_max;
        switch (data_type)
        {
            case ImGuiDataType_S8:     return DataTypeClampT<sbyte >(v, vmin, vmax);
            case ImGuiDataType_U8:     return DataTypeClampT<byte  >(v, vmin, vmax);
            case ImGuiDataType_S16:    return DataTypeClampT<short >(v, vmin, vmax);
            case ImGuiDataType_U16:    return DataTypeClampT<ushort>(v, vmin, vmax);
            case ImGuiDataType_S32:    return DataTypeClampT<int   >(v, vmin, vmax);
            case ImGuiDataType_U32:    return DataTypeClampT<uint  >(v, vmin, vmax);
            case ImGuiDataType_S64:    return DataTypeClampT<long  >(v, vmin, vmax);
            case ImGuiDataType_U64:    return DataTypeClampT<ulong >(v, vmin, vmax);
            case ImGuiDataType_Float:  return DataTypeClampT<float >(v, vmin, vmax);
            case ImGuiDataType_Double: return DataTypeClampT<double>(v, vmin, vmax);
            case ImGuiDataType_COUNT:  break;
        }
        Debug.Assert(false);
        return false;
    }

    public static bool DataTypeIsZero(ImGuiDataType data_type, IntPtr p_data)
    {
        ImGuiContext g = GImGui;
        ImGuiDataTypeStorage zero_value = g.DataTypeZeroValue; // (local copy: C++ takes &g.DataTypeZeroValue)
        return DataTypeCompare(data_type, p_data, (IntPtr)(&zero_value.Data)) == 0;
    }

    // Load the first 'size' bytes of the scalar at 'p' as an ImGuiDataTypeStorage (zero-extended ulong).
    // (C++ uses memcpy/memcmp on raw bytes; little-endian low-N-bytes match that behavior.)
    private static ulong DataTypeStorageLoad(IntPtr p, int size)
    {
        void* ptr = (void*)p;
        if (size >= 8) return *(ulong*)ptr;
        if (size >= 4) return *(uint*)ptr;
        if (size >= 2) return *(ushort*)ptr;
        return *(byte*)ptr;
    }

    private static void DataTypeStorageStore(IntPtr p, int size, ulong v)
    {
        void* ptr = (void*)p;
        if (size >= 8) *(ulong*)ptr = v;
        else if (size >= 4) *(uint*)ptr = (uint)v;
        else if (size >= 2) *(ushort*)ptr = (ushort)v;
        else *(byte*)ptr = (byte)v;
    }

    // C: atof()/strtod() equivalent used by ImAtof(STR) macro. Parses a leading float literal.
    private static double ImAtof(string s)
    {
        int e = 0;
        if (e < s.Length && (s[e] == '+' || s[e] == '-')) e++;
        bool has_digits = false;
        while (e < s.Length && s[e] >= '0' && s[e] <= '9') { e++; has_digits = true; }
        if (e < s.Length && s[e] == '.')
        {
            int dot = e;
            e++;
            while (e < s.Length && s[e] >= '0' && s[e] <= '9') { e++; has_digits = true; }
            if (!has_digits) e = dot; // ".e" with no digits
        }
        if (has_digits && e < s.Length && (s[e] == 'e' || s[e] == 'E'))
        {
            int exp = e;
            e++;
            if (e < s.Length && (s[e] == '+' || s[e] == '-')) e++;
            bool has_exp_digits = false;
            while (e < s.Length && s[e] >= '0' && s[e] <= '9') { e++; has_exp_digits = true; }
            if (!has_exp_digits) e = exp;
        }
        double.TryParse(s.Substring(0, e), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v);
        return v;
    }

    // Minimal C sscanf() equivalent covering the scalar scan formats ImGui uses
    // ("%d", "%u", "%lld", "%llu", "%f", "%lf", with optional width), matching sscanf() semantics
    // (skip leading whitespace, parse a leading literal, return the number of successful conversions).
    private static int SscanfScalar(string s, string format, void* dest, int dest_size)
    {
        // Locate conversion specifier in format (any literal prefix is skipped)
        int fi = 0;
        while (fi < format.Length && format[fi] != '%') fi++;
        if (fi >= format.Length) return 0;
        fi++; // skip '%'
        while (fi < format.Length && (format[fi] == '-' || format[fi] == '+' || format[fi] == ' ' || format[fi] == '#' || format[fi] == '0' || (format[fi] >= '0' && format[fi] <= '9'))) fi++; // flags/width
        while (fi < format.Length && "hljztL".IndexOf(format[fi]) >= 0) fi++; // length modifiers
        if (fi >= format.Length) return 0;
        char conv = format[fi];

        int si = 0;
        // C sscanf skips leading whitespace for numeric conversions
        while (si < s.Length && (s[si] == ' ' || s[si] == '\t' || s[si] == '\n' || s[si] == '\r' || s[si] == '\f' || s[si] == '\v')) si++;

        switch (conv)
        {
            case 'd':
            case 'i':
            {
                bool neg = false;
                if (si < s.Length && (s[si] == '+' || s[si] == '-')) { neg = s[si] == '-'; si++; }
                long v = 0;
                int digits = 0;
                while (si < s.Length && s[si] >= '0' && s[si] <= '9') { v = v * 10 + (s[si] - '0'); si++; digits++; }
                if (digits == 0) return 0;
                if (neg) v = -v;
                if (dest_size >= 8) *(long*)dest = v;   // %lld into a 64-bit target
                else *(int*)dest = (int)v;              // %d into a 32-bit target
                return 1;
            }
            case 'u':
            {
                bool neg = false;
                if (si < s.Length && (s[si] == '+' || s[si] == '-')) { neg = s[si] == '-'; si++; }
                ulong v = 0;
                int digits = 0;
                while (si < s.Length && s[si] >= '0' && s[si] <= '9') { v = v * 10 + (ulong)(s[si] - '0'); si++; digits++; }
                if (digits == 0) return 0;
                if (neg) v = (ulong)(-(long)v); // sscanf("%u") accepts a minus sign (wraps around)
                if (dest_size >= 8) *(ulong*)dest = v;  // %llu into a 64-bit target
                else *(uint*)dest = (uint)v;            // %u into a 32-bit target
                return 1;
            }
            case 'a': case 'A': case 'e': case 'E': case 'f': case 'F': case 'g': case 'G':
            {
                if (si >= s.Length) return 0;
                char c0 = s[si];
                if (!char.IsAsciiDigit(c0) && c0 != '.' && c0 != '+' && c0 != '-') return 0;
                double v = ImAtof(s.Substring(si));
                if (dest_size >= 8) *(double*)dest = v; // %lf into a double
                else *(float*)dest = (float)v;          // %f into a float
                return 1;
            }
            default:
                return 0; // (%s for ImGuiDataType_String is not supported by this port)
        }
    }

    private static float GetMinimumStepAtDecimalPrecision(int decimal_precision)
    {
        ReadOnlySpan<float> min_steps = stackalloc float[] { 1.0f, 0.1f, 0.01f, 0.001f, 0.0001f, 0.00001f, 0.000001f, 0.0000001f, 0.00000001f, 0.000000001f };
        if (decimal_precision < 0)
            return FLT_MIN;
        return (decimal_precision < min_steps.Length) ? min_steps[decimal_precision] : ImMath.ImPow(10.0f, (float)-decimal_precision);
    }

    // C++: template<typename TYPE> TYPE ImGui::RoundScalarWithFormatT(const char* format, ImGuiDataType data_type, TYPE v)
    private static T RoundScalarWithFormatT<T>(string format, ImGuiDataType data_type, T v) where T : unmanaged, INumber<T>
    {
        Debug.Assert(data_type == ImGuiDataType_Float || data_type == ImGuiDataType_Double);
        string fmt_start = ImParseFormatFindStart(format);
        if (fmt_start.Length == 0 || fmt_start[0] != '%' || (fmt_start.Length > 1 && fmt_start[1] == '%')) // Don't apply if the value is not visible in the format string
            return v;

        // Sanitize format
        ImParseFormatSanitizeForPrinting(fmt_start, out string fmt_sanitized);
        fmt_start = fmt_sanitized;

        // Format value with our rounding, and read back
        string v_str = ImFmt.Sprintf(fmt_start, (object)v);
        int p = 0;
        while (p < v_str.Length && v_str[p] == ' ')
            p++;
        v = T.CreateTruncating(ImAtof(v_str.Substring(p)));

        return v;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: DragScalar, DragFloat, DragInt, etc.
    //-------------------------------------------------------------------------

    // This is called by DragBehavior() when the widget is active (held by mouse or being manipulated with Nav controls)
    // C++: template<typename TYPE, typename SIGNEDTYPE, typename FLOATTYPE> bool ImGui::DragBehaviorT(...)
    private static bool DragBehaviorT<T, TS, TF>(ImGuiDataType data_type, T* v, float v_speed, T v_min, T v_max, string format, ImGuiSliderFlags flags)
        where T : unmanaged, INumber<T>
        where TS : unmanaged, INumber<TS>
        where TF : unmanaged, INumber<TF>
    {
        ImGuiContext g = GImGui;
        ImGuiAxis axis = (flags & ImGuiSliderFlags_Vertical) != 0 ? ImGuiAxis_Y : ImGuiAxis_X;
        bool is_bounded = (v_min < v_max) || ((v_min == v_max) && (v_min != T.Zero || (flags & ImGuiSliderFlags_ClampZeroRange) != 0));
        bool is_wrapped = is_bounded && (flags & ImGuiSliderFlags_WrapAround) != 0;
        bool is_logarithmic = (flags & ImGuiSliderFlags_Logarithmic) != 0;
        bool is_floating_point = (data_type == ImGuiDataType_Float) || (data_type == ImGuiDataType_Double);

        // Default tweak speed
        if (v_speed == 0.0f && is_bounded && TF.CreateTruncating(v_max - v_min) < TF.CreateTruncating(float.MaxValue))
            v_speed = float.CreateTruncating(TF.CreateTruncating(v_max - v_min) * TF.CreateTruncating(g.DragSpeedDefaultRatio));

        // Inputs accumulates into g.DragCurrentAccum, which is flushed into the current value as soon as it makes a difference with our precision settings
        float adjust_delta = 0.0f;
        if (g.ActiveIdSource == ImGuiInputSource_Mouse && IsMousePosValid(ref g.IO.MousePos) && IsMouseDragPastThreshold(0, g.IO.MouseDragThreshold * DRAG_MOUSE_THRESHOLD_FACTOR))
        {
            adjust_delta = g.IO.MouseDelta[axis];
            if (g.IO.KeyAlt && (flags & ImGuiSliderFlags_NoSpeedTweaks) == 0)
                adjust_delta *= 1.0f / 100.0f;
            if (g.IO.KeyShift && (flags & ImGuiSliderFlags_NoSpeedTweaks) == 0)
                adjust_delta *= 10.0f;
        }
        else if (g.ActiveIdSource == ImGuiInputSource_Keyboard || g.ActiveIdSource == ImGuiInputSource_Gamepad)
        {
            int decimal_precision = is_floating_point ? ImParseFormatPrecision(format, 3) : 0;
            bool tweak_slow = IsKeyDown(g.NavInputSource == ImGuiInputSource_Gamepad ? ImGuiKey_NavGamepadTweakSlow : ImGuiKey_NavKeyboardTweakSlow);
            bool tweak_fast = IsKeyDown(g.NavInputSource == ImGuiInputSource_Gamepad ? ImGuiKey_NavGamepadTweakFast : ImGuiKey_NavKeyboardTweakFast);
            float tweak_factor = (flags & ImGuiSliderFlags_NoSpeedTweaks) != 0 ? 1.0f : tweak_slow ? 1.0f / 10.0f : tweak_fast ? 10.0f : 1.0f;
            adjust_delta = GetNavTweakPressedAmount(axis) * tweak_factor;
            v_speed = ImMath.ImMax(v_speed, GetMinimumStepAtDecimalPrecision(decimal_precision));
        }
        adjust_delta *= v_speed;

        // For vertical drag we currently assume that Up=higher value (like we do with vertical sliders). This may become a parameter.
        if (axis == ImGuiAxis_Y)
            adjust_delta = -adjust_delta;

        // For logarithmic use our range is effectively 0..1 so scale the delta into that range
        if (is_logarithmic && (TF.CreateTruncating(v_max - v_min) < TF.CreateTruncating(float.MaxValue)) && (TF.CreateTruncating(v_max - v_min) > TF.CreateTruncating(0.000001f))) // Epsilon to avoid /0
            adjust_delta /= float.CreateTruncating(v_max - v_min);

        // Clear current value on activation
        // Avoid altering values and clamping when we are _already_ past the limits and heading in the same direction, so e.g. if range is 0..255, current value is 300 and we are pushing to the right side, keep the 300.
        bool is_just_activated = g.ActiveIdIsJustActivated;
        bool is_already_past_limits_and_pushing_outward = is_bounded && !is_wrapped && ((*v >= v_max && adjust_delta > 0.0f) || (*v <= v_min && adjust_delta < 0.0f));
        if (is_just_activated || is_already_past_limits_and_pushing_outward)
        {
            g.DragCurrentAccum = 0.0f;
            g.DragCurrentAccumDirty = false;
        }
        else if (adjust_delta != 0.0f)
        {
            g.DragCurrentAccum += adjust_delta;
            g.DragCurrentAccumDirty = true;
        }

        if (!g.DragCurrentAccumDirty)
            return false;

        T v_cur = *v;
        TF v_old_ref_for_accum_remainder = TF.Zero;

        float logarithmic_zero_epsilon = 0.0f; // Only valid when is_logarithmic is true
        const float zero_deadzone_halfsize = 0.0f; // Drag widgets have no deadzone (as it doesn't make sense)
        if (is_logarithmic)
        {
            // When using logarithmic sliders, we need to clamp to avoid hitting zero, but our choice of clamp value greatly affects slider precision. We attempt to use the specified precision to estimate a good lower bound.
            int decimal_precision = is_floating_point ? ImParseFormatPrecision(format, 3) : 1;
            logarithmic_zero_epsilon = ImMath.ImPow(0.1f, (float)decimal_precision);

            // Convert to parametric space, apply delta, convert back
            float v_old_parametric = ScaleRatioFromValueT<T, TS, TF>(data_type, v_cur, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);
            float v_new_parametric = v_old_parametric + g.DragCurrentAccum;
            v_cur = ScaleValueFromRatioT<T, TS, TF>(data_type, v_new_parametric, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);
            v_old_ref_for_accum_remainder = TF.CreateTruncating(v_old_parametric);
        }
        else
        {
            v_cur += T.CreateTruncating(TS.CreateTruncating(g.DragCurrentAccum));
        }

        // Round to user desired precision based on format string
        if (is_floating_point && (flags & ImGuiSliderFlags_NoRoundToFormat) == 0)
            v_cur = RoundScalarWithFormatT<T>(format, data_type, v_cur);

        // Preserve remainder after rounding has been applied. This also allow slow tweaking of values.
        g.DragCurrentAccumDirty = false;
        if (is_logarithmic)
        {
            // Convert to parametric space, apply delta, convert back
            float v_new_parametric = ScaleRatioFromValueT<T, TS, TF>(data_type, v_cur, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);
            g.DragCurrentAccum -= float.CreateTruncating(TF.CreateTruncating(v_new_parametric) - v_old_ref_for_accum_remainder);
        }
        else
        {
            g.DragCurrentAccum -= float.CreateTruncating(TS.CreateTruncating(v_cur) - TS.CreateTruncating(*v));
        }

        // Lose zero sign for float/double (C++: v_cur == (TYPE)-0 -> v_cur = (TYPE)0; -0 == +0 so this catches both)
        if (v_cur == T.Zero)
            v_cur = T.Zero;

        if (*v != v_cur && is_bounded)
        {
            if (is_wrapped)
            {
                // Wrap values
                if (v_cur < v_min)
                    v_cur += (v_max - v_min) + (is_floating_point ? T.Zero : T.One);
                if (v_cur > v_max)
                    v_cur -= (v_max - v_min) + (is_floating_point ? T.Zero : T.One);
            }
            else
            {
                // Clamp values + handle overflow/wrap-around for integer types.
                if (v_cur < v_min || (v_cur > *v && adjust_delta < 0.0f && !is_floating_point))
                    v_cur = v_min;
                if (v_cur > v_max || (v_cur < *v && adjust_delta > 0.0f && !is_floating_point))
                    v_cur = v_max;
            }
        }

        // Apply result
        if (*v == v_cur)
            return false;
        *v = v_cur;
        return true;
    }

    private static bool ShortcutsForCancel(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        bool is_cancel_with_keyboard = Shortcut(ImGuiKey_Escape, ImGuiInputFlags_None, id);
        bool is_cancel_with_gamepad = (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0 && (g.IO.BackendFlags & ImGuiBackendFlags_HasGamepad) != 0 && Shortcut(ImGuiKey_NavGamepadCancel, ImGuiInputFlags_None, id);
        //bool is_cancel_with_mouse = ImGui::IsMouseClicked(ImGuiMouseButton_Right, ImGuiInputFlags_None, id);
        //if (is_cancel_with_mouse)
        //    ImGui::SetKeyOwner(ImGuiKey_MouseRight, id);
        return is_cancel_with_keyboard || is_cancel_with_gamepad; //|| is_cancel_with_mouse
    }

    public static bool DragBehavior(ImGuiID id, ImGuiDataType data_type, IntPtr p_v, float v_speed, IntPtr p_min, IntPtr p_max, string format, ImGuiSliderFlags flags)
    {
        // Read imgui.cpp "API BREAKING CHANGES" section for 1.78 if you hit this assert.
        Debug.Assert((flags == 1 || (flags & ImGuiSliderFlags_InvalidMask_) == 0), "Invalid ImGuiSliderFlags flags! Has the legacy 'float power' argument been mistakenly cast to flags? Call function with ImGuiSliderFlags_Logarithmic flags instead.");

        // Things we can do easily outside the DragBehaviorT<> template, saves code generation.
        ImGuiContext g = GImGui;
        if (g.ActiveId == id)
        {
            if (g.ActiveIdSource == ImGuiInputSource_Mouse && !g.IO.MouseDown[0])
                ClearActiveID();
            else if ((g.ActiveIdSource == ImGuiInputSource_Keyboard || g.ActiveIdSource == ImGuiInputSource_Gamepad) && g.NavActivatePressedId == id && !g.ActiveIdIsJustActivated)
                ClearActiveID();
            if (ShortcutsForCancel(id) && g.ActiveId == id) //-V560
            {
                // Canceling action reverts to initial value
                int data_size = DataTypeGetInfo(data_type).Size;
                bool value_changed = DataTypeStorageLoad(p_v, data_size) != g.ActiveIdValueOnActivation.Data;
                DataTypeStorageStore(p_v, data_size, g.ActiveIdValueOnActivation.Data);
                ClearActiveID();
                return value_changed;
            }
        }
        if (g.ActiveId != id)
            return false;
        if ((g.LastItemData.ItemFlags & ImGuiItemFlags_ReadOnly) != 0 || (flags & ImGuiSliderFlags_ReadOnly) != 0)
            return false;

        void* pv = (void*)p_v;
        void* pmin = (void*)p_min;
        void* pmax = (void*)p_max;
        switch (data_type)
        {
            case ImGuiDataType_S8:
            {
                ImS32 v32 = *(sbyte*)pv;
                bool r = DragBehaviorT<ImS32, ImS32, float>(ImGuiDataType_S32, &v32, v_speed, pmin != null ? *(sbyte*)pmin : IM_S8_MIN, pmax != null ? *(sbyte*)pmax : IM_S8_MAX, format, flags);
                if (r) *(sbyte*)pv = (sbyte)v32;
                return r;
            }
            case ImGuiDataType_U8:
            {
                ImU32 v32 = *(byte*)pv;
                bool r = DragBehaviorT<ImU32, ImS32, float>(ImGuiDataType_U32, &v32, v_speed, pmin != null ? (uint)*(byte*)pmin : IM_U32_MIN, pmax != null ? (uint)*(byte*)pmax : IM_U32_MAX, format, flags);
                if (r) *(byte*)pv = (byte)v32;
                return r;
            }
            case ImGuiDataType_S16:
            {
                ImS32 v32 = *(short*)pv;
                bool r = DragBehaviorT<ImS32, ImS32, float>(ImGuiDataType_S32, &v32, v_speed, pmin != null ? *(short*)pmin : IM_S16_MIN, pmax != null ? *(short*)pmax : IM_S16_MAX, format, flags);
                if (r) *(short*)pv = (short)v32;
                return r;
            }
            case ImGuiDataType_U16:
            {
                ImU32 v32 = *(ushort*)pv;
                bool r = DragBehaviorT<ImU32, ImS32, float>(ImGuiDataType_U32, &v32, v_speed, pmin != null ? (uint)*(ushort*)pmin : IM_U16_MIN, pmax != null ? (uint)*(ushort*)pmax : IM_U16_MAX, format, flags);
                if (r) *(ushort*)pv = (ushort)v32;
                return r;
            }
            case ImGuiDataType_S32:    return DragBehaviorT<ImS32, ImS32, float >(data_type, (int*)pv,   v_speed, pmin != null ? *(int*)pmin   : IM_S32_MIN, pmax != null ? *(int*)pmax   : IM_S32_MAX, format, flags);
            case ImGuiDataType_U32:    return DragBehaviorT<ImU32, ImS32, float >(data_type, (uint*)pv,  v_speed, pmin != null ? *(uint*)pmin  : IM_U32_MIN, pmax != null ? *(uint*)pmax  : IM_U32_MAX, format, flags);
            case ImGuiDataType_S64:    return DragBehaviorT<ImS64, ImS64, double>(data_type, (long*)pv,  v_speed, pmin != null ? *(long*)pmin  : IM_S64_MIN, pmax != null ? *(long*)pmax  : IM_S64_MAX, format, flags);
            case ImGuiDataType_U64:    return DragBehaviorT<ImU64, ImS64, double>(data_type, (ulong*)pv, v_speed, pmin != null ? *(ulong*)pmin : IM_U64_MIN, pmax != null ? *(ulong*)pmax : IM_U64_MAX, format, flags);
            case ImGuiDataType_Float:  return DragBehaviorT<float, float, float  >(data_type, (float*)pv,  v_speed, pmin != null ? *(float*)pmin  : -float.MaxValue, pmax != null ? *(float*)pmax  : float.MaxValue,   format, flags);
            case ImGuiDataType_Double: return DragBehaviorT<double, double, double>(data_type, (double*)pv, v_speed, pmin != null ? *(double*)pmin : -double.MaxValue, pmax != null ? *(double*)pmax : double.MaxValue, format, flags);
            case ImGuiDataType_COUNT: break;
        }
        Debug.Assert(false);
        return false;
    }

    // Only clamp Ctrl+Click input when ImGuiSliderFlags_ClampOnInput is set (generally via ImGuiSliderFlags_AlwaysClamp)
    private static bool TempInputIsClampEnabled(ImGuiSliderFlags flags, ImGuiDataType data_type, IntPtr p_min, IntPtr p_max)
    {
        if ((flags & ImGuiSliderFlags_ClampOnInput) != 0 && (p_min != default || p_max != default))
        {
            int clamp_range_dir = (p_min != default && p_max != default) ? DataTypeCompare(data_type, p_min, p_max) : 0; // -1 when *p_min < *p_max, == 0 when *p_min == *p_max
            if (p_min == default || p_max == default || clamp_range_dir < 0)
                return true;
            if (clamp_range_dir == 0)
                return DataTypeIsZero(data_type, p_min) ? ((flags & ImGuiSliderFlags_ClampZeroRange) != 0) : true;
        }
        return false;
    }

    // Note: p_data, p_min and p_max are _pointers_ to a memory address holding the data. For a Drag widget, p_min and p_max are optional.
    // Read code of e.g. DragFloat(), DragInt() etc. or examples in 'Demo->Widgets->Data Types' to understand how to use this function directly.
    public static bool DragScalar(string label, ImGuiDataType data_type, IntPtr p_data, float v_speed = 1.0f, IntPtr p_min = default, IntPtr p_max = default, string format = null, ImGuiSliderFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);
        float w = CalcItemWidth();
        ImU32 color_marker = (g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasColorMarker) != 0 ? g.NextItemData.ColorMarker : 0;

        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImRect frame_bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + new ImVec2(w, label_size.y + style.FramePadding.y * 2.0f));
        ImRect total_bb = new ImRect(frame_bb.Min, frame_bb.Max + new ImVec2(label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f, 0.0f));

        bool temp_input_allowed = (flags & ImGuiSliderFlags_NoInput) == 0;
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(total_bb, id, frame_bb, temp_input_allowed ? ImGuiItemFlags_Inputable : 0))
            return false;

        // Default format string when passing NULL
        if (format == null)
            format = DataTypeGetInfo(data_type).PrintFmt;

        bool hovered = ItemHoverable(frame_bb, id, g.LastItemData.ItemFlags);
        bool temp_input_is_active = temp_input_allowed && TempInputIsActive(id);
        if (!temp_input_is_active)
        {
            // Tabbing or Ctrl+Click on Drag turns it into an InputText
            bool clicked = hovered && IsMouseClicked(0, ImGuiInputFlags_None, id);
            bool double_clicked = (hovered && g.IO.MouseClickedCount[0] == 2 && TestKeyOwner(ImGuiKey_MouseLeft, id));
            bool make_active = (clicked || double_clicked || g.NavActivateId == id);
            if (make_active && (clicked || double_clicked))
                SetKeyOwner(ImGuiKey_MouseLeft, id);
            if (make_active && temp_input_allowed)
                if ((clicked && g.IO.KeyCtrl) || double_clicked || (g.NavActivateId == id && (g.NavActivateFlags & ImGuiActivateFlags_PreferInput) != 0))
                    temp_input_is_active = true;

            // (Optional) simple click (without moving) turns Drag into an InputText
            if (g.IO.ConfigDragClickToInputText && temp_input_allowed && !temp_input_is_active)
                if (g.ActiveId == id && hovered && g.IO.MouseReleased[0] && !IsMouseDragPastThreshold(0, g.IO.MouseDragThreshold * DRAG_MOUSE_THRESHOLD_FACTOR))
                {
                    g.NavActivateId = id;
                    g.NavActivateFlags = ImGuiActivateFlags_PreferInput;
                    temp_input_is_active = true;
                }

            // Store initial value (not used by main lib but available as a convenience but some mods e.g. to revert)
            if (make_active)
                g.ActiveIdValueOnActivation.Data = DataTypeStorageLoad(p_data, DataTypeGetInfo(data_type).Size);

            if (make_active && !temp_input_is_active)
            {
                SetActiveID(id, window);
                SetFocusID(id, window);
                FocusWindow(window);
                g.ActiveIdUsingNavDirMask = (ImU32)((1 << ImGuiDir_Left) | (1 << ImGuiDir_Right));
            }
        }

        if (temp_input_is_active)
        {
            bool clamp_enabled = TempInputIsClampEnabled(flags, data_type, p_min, p_max);
            return TempInputScalar(frame_bb, id, label, data_type, p_data, format, clamp_enabled ? p_min : default, clamp_enabled ? p_max : default);
        }

        // Draw frame
        ImU32 frame_col = GetColorU32(g.ActiveId == id ? ImGuiCol_FrameBgActive : hovered ? ImGuiCol_FrameBgHovered : ImGuiCol_FrameBg);
        RenderNavCursor(frame_bb, id);
        RenderFrame(frame_bb.Min, frame_bb.Max, frame_col, false, style.FrameRounding);
        if (color_marker != 0 && style.ColorMarkerSize > 0.0f)
            RenderColorComponentMarker(frame_bb, GetColorU32(color_marker), style.FrameRounding);
        RenderFrameBorder(frame_bb.Min, frame_bb.Max, g.Style.FrameRounding);

        // Drag behavior
        bool value_changed = DragBehavior(id, data_type, p_data, v_speed, p_min, p_max, format, flags);
        if (value_changed)
            MarkItemEdited(id);

        // Display value using user-provided display format so user can add prefix/suffix/decorations to the value.
        if (g.LogEnabled)
            LogSetNextTextDecoration("{", "}");
        RenderValueTextClipped(data_type, p_data, format, frame_bb.Min, frame_bb.Max, new ImVec2(0.5f, 0.5f));

        if (label_size.x > 0.0f)
            RenderText(new ImVec2(frame_bb.Max.x + style.ItemInnerSpacing.x, frame_bb.Min.y + style.FramePadding.y), label, label_end, false);

        // IMGUI_TEST_ENGINE_ITEM_INFO(id, label, g.LastItemData.StatusFlags | (temp_input_allowed ? ImGuiItemStatusFlags_Inputable : 0)); // (omitted)
        return value_changed;
    }

    public static bool DragScalarN(string label, ImGuiDataType data_type, IntPtr p_data, int components, float v_speed = 1.0f, IntPtr p_min = default, IntPtr p_max = default, string format = null, ImGuiSliderFlags flags = 0)
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
            if ((flags & ImGuiSliderFlags_ColorMarkers) != 0)
                SetNextItemColorMarker(GDefaultRgbaColorMarkers[i]);
            value_changed |= DragScalar("", data_type, p_data, v_speed, p_min, p_max, format, flags);
            PopID();
            PopItemWidth();
            p_data = (IntPtr)((byte*)p_data + type_size);
        }
        PopID();

        int label_end = FindRenderedTextEnd(label);
        if (label_end != 0) // C++: if (label != label_end)
        {
            SameLine(0, g.Style.ItemInnerSpacing.x);
            TextEx(label, label_end);
        }

        EndGroup();
        return value_changed;
    }

    public static bool DragFloat(string label, ref float v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = &v)
            return DragScalar(label, ImGuiDataType_Float, (IntPtr)pv, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool DragFloat2(string label, float[] v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = v)
            return DragScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 2, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool DragFloat3(string label, float[] v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = v)
            return DragScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 3, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool DragFloat4(string label, float[] v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = v)
            return DragScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 4, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    // NB: You likely want to specify the ImGuiSliderFlags_AlwaysClamp when using this.
    public static bool DragFloatRange2(string label, ref float v_current_min, ref float v_current_max, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", string format_max = null, ImGuiSliderFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        PushID(label);
        BeginGroup();
        PushMultiItemsWidths(2, CalcItemWidth());

        float min_min = (v_min >= v_max) ? -float.MaxValue : v_min;
        float min_max = (v_min >= v_max) ? v_current_max : ImMath.ImMin(v_max, v_current_max);
        ImGuiSliderFlags min_flags = flags | ((min_min == min_max) ? ImGuiSliderFlags_ReadOnly : 0);
        bool value_changed;
        fixed (float* pv_min = &v_current_min, pv_max = &v_current_max)
        {
            value_changed = DragScalar("##min", ImGuiDataType_Float, (IntPtr)pv_min, v_speed, (IntPtr)(&min_min), (IntPtr)(&min_max), format, min_flags);
            PopItemWidth();
            SameLine(0, g.Style.ItemInnerSpacing.x);

            float max_min = (v_min >= v_max) ? v_current_min : ImMath.ImMax(v_min, v_current_min);
            float max_max = (v_min >= v_max) ? float.MaxValue : v_max;
            ImGuiSliderFlags max_flags = flags | ((max_min == max_max) ? ImGuiSliderFlags_ReadOnly : 0);
            value_changed |= DragScalar("##max", ImGuiDataType_Float, (IntPtr)pv_max, v_speed, (IntPtr)(&max_min), (IntPtr)(&max_max), format_max ?? format, max_flags);
        }
        PopItemWidth();
        SameLine(0, g.Style.ItemInnerSpacing.x);

        TextEx(label, FindRenderedTextEnd(label));
        EndGroup();
        PopID();

        return value_changed;
    }

    // NB: v_speed is float to allow adjusting the drag speed with more precision
    public static bool DragInt(string label, ref int v, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = &v)
            return DragScalar(label, ImGuiDataType_S32, (IntPtr)pv, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool DragInt2(string label, int[] v, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = v)
            return DragScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 2, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool DragInt3(string label, int[] v, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = v)
            return DragScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 3, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool DragInt4(string label, int[] v, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = v)
            return DragScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 4, v_speed, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    // NB: You likely want to specify the ImGuiSliderFlags_AlwaysClamp when using this.
    public static bool DragIntRange2(string label, ref int v_current_min, ref int v_current_max, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", string format_max = null, ImGuiSliderFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        PushID(label);
        BeginGroup();
        PushMultiItemsWidths(2, CalcItemWidth());

        int min_min = (v_min >= v_max) ? int.MinValue : v_min;
        int min_max = (v_min >= v_max) ? v_current_max : ImMath.ImMin(v_max, v_current_max);
        ImGuiSliderFlags min_flags = flags | ((min_min == min_max) ? ImGuiSliderFlags_ReadOnly : 0);
        bool value_changed = DragInt("##min", ref v_current_min, v_speed, min_min, min_max, format, min_flags);
        PopItemWidth();
        SameLine(0, g.Style.ItemInnerSpacing.x);

        int max_min = (v_min >= v_max) ? v_current_min : ImMath.ImMax(v_min, v_current_min);
        int max_max = (v_min >= v_max) ? int.MaxValue : v_max;
        ImGuiSliderFlags max_flags = flags | ((max_min == max_max) ? ImGuiSliderFlags_ReadOnly : 0);
        value_changed |= DragInt("##max", ref v_current_max, v_speed, max_min, max_max, format_max ?? format, max_flags);
        PopItemWidth();
        SameLine(0, g.Style.ItemInnerSpacing.x);

        TextEx(label, FindRenderedTextEnd(label));
        EndGroup();
        PopID();

        return value_changed;
    }

    //-------------------------------------------------------------------------
    // [SECTION] Widgets: SliderScalar, SliderFloat, SliderInt, etc.
    //-------------------------------------------------------------------------

    // Generic math helpers used by the Scale/Behavior template ports (match C++ Im* semantics per FLOATTYPE)
    private static TF ImAbsTF<TF>(TF v) where TF : INumber<TF> => TF.Abs(v);
    private static TF ImPowTF<TF>(TF x, TF y) where TF : INumber<TF>
        => typeof(TF) == typeof(float)
            ? TF.CreateTruncating(MathF.Pow(float.CreateTruncating(x), float.CreateTruncating(y)))
            : TF.CreateTruncating(Math.Pow(double.CreateTruncating(x), double.CreateTruncating(y)));
    private static TF ImLogTF<TF>(TF x) where TF : INumber<TF>
        => typeof(TF) == typeof(float)
            ? TF.CreateTruncating(MathF.Log(float.CreateTruncating(x)))
            : TF.CreateTruncating(Math.Log(double.CreateTruncating(x)));
    private static T ImClampT<T>(T v, T mn, T mx) where T : INumber<T> => v < mn ? mn : (v > mx ? mx : v);
    private static T ImMinT<T>(T a, T b) where T : INumber<T> => a < b ? a : b;
    private static void ImSwapT<T>(ref T a, ref T b) where T : INumber<T> { T tmp = a; a = b; b = tmp; }

    // Convert a value v in the output space of a slider into a parametric position on the slider itself (the logical opposite of ScaleValueFromRatioT)
    // C++: template<typename TYPE, typename SIGNEDTYPE, typename FLOATTYPE> float ImGui::ScaleRatioFromValueT(...)
    private static float ScaleRatioFromValueT<T, TS, TF>(ImGuiDataType data_type, T v, T v_min, T v_max, float logarithmic_zero_epsilon, float zero_deadzone_halfsize)
        where T : unmanaged, INumber<T>
        where TS : unmanaged, INumber<TS>
        where TF : unmanaged, INumber<TF>
    {
        if (v_min == v_max)
            return 0.0f;
        // IM_UNUSED(data_type);

        T v_clamped = (v_min < v_max) ? ImClampT(v, v_min, v_max) : ImClampT(v, v_max, v_min);
        if (logarithmic_zero_epsilon > 0.0f) // == is_logarithmic from caller
        {
            bool flipped = v_max < v_min;

            if (flipped) // Handle the case where the range is backwards
                ImSwapT(ref v_min, ref v_max);

            // Fudge min/max to avoid getting close to log(0)
            TF v_min_fudged = (ImAbsTF(TF.CreateTruncating(v_min)) < TF.CreateTruncating(logarithmic_zero_epsilon)) ? ((v_min < T.Zero) ? TF.CreateTruncating(-logarithmic_zero_epsilon) : TF.CreateTruncating(logarithmic_zero_epsilon)) : TF.CreateTruncating(v_min);
            TF v_max_fudged = (ImAbsTF(TF.CreateTruncating(v_max)) < TF.CreateTruncating(logarithmic_zero_epsilon)) ? ((v_max < T.Zero) ? TF.CreateTruncating(-logarithmic_zero_epsilon) : TF.CreateTruncating(logarithmic_zero_epsilon)) : TF.CreateTruncating(v_max);

            // Awkward special cases - we need ranges of the form (-100 .. 0) to convert to (-100 .. -epsilon), not (-100 .. epsilon)
            if ((v_min == T.Zero) && (v_max < T.Zero))
                v_min_fudged = TF.CreateTruncating(-logarithmic_zero_epsilon);
            else if ((v_max == T.Zero) && (v_min < T.Zero))
                v_max_fudged = TF.CreateTruncating(-logarithmic_zero_epsilon);

            float result;
            if (TF.CreateTruncating(v_clamped) <= v_min_fudged)
                result = 0.0f; // Workaround for values that are in-range but below our fudge
            else if (TF.CreateTruncating(v_clamped) >= v_max_fudged)
                result = 1.0f; // Workaround for values that are in-range but above our fudge
            else if (TF.CreateTruncating(v_min) * TF.CreateTruncating(v_max) < TF.Zero) // Range crosses zero, so split into two portions
            {
                float zero_point_center = (-float.CreateTruncating(v_min)) / (float.CreateTruncating(v_max) - float.CreateTruncating(v_min)); // The zero point in parametric space.  There's an argument we should take the logarithmic nature into account when calculating this, but for now this should do (and the most common case of a symmetrical range works fine)
                float zero_point_snap_L = zero_point_center - zero_deadzone_halfsize;
                float zero_point_snap_R = zero_point_center + zero_deadzone_halfsize;
                if (v == T.Zero)
                    result = zero_point_center; // Special case for exactly zero
                else if (v < T.Zero)
                    result = (1.0f - float.CreateTruncating(ImLogTF(-TF.CreateTruncating(v_clamped) / TF.CreateTruncating(logarithmic_zero_epsilon)) / ImLogTF(-v_min_fudged / TF.CreateTruncating(logarithmic_zero_epsilon)))) * zero_point_snap_L;
                else
                    result = zero_point_snap_R + (float.CreateTruncating(ImLogTF(TF.CreateTruncating(v_clamped) / TF.CreateTruncating(logarithmic_zero_epsilon)) / ImLogTF(v_max_fudged / TF.CreateTruncating(logarithmic_zero_epsilon))) * (1.0f - zero_point_snap_R));
            }
            else if ((v_min < T.Zero) || (v_max < T.Zero)) // Entirely negative slider
                result = 1.0f - float.CreateTruncating(ImLogTF(-TF.CreateTruncating(v_clamped) / -v_max_fudged) / ImLogTF(-v_min_fudged / -v_max_fudged));
            else
                result = float.CreateTruncating(ImLogTF(TF.CreateTruncating(v_clamped) / v_min_fudged) / ImLogTF(v_max_fudged / v_min_fudged));

            return flipped ? (1.0f - result) : result;
        }
        else
        {
            // Linear slider
            return float.CreateTruncating(TF.CreateTruncating(TS.CreateTruncating(v_clamped - v_min)) / TF.CreateTruncating(TS.CreateTruncating(v_max - v_min)));
        }
    }

    // Convert a parametric position on a slider into a value v in the output space (the logical opposite of ScaleRatioFromValueT)
    // C++: template<typename TYPE, typename SIGNEDTYPE, typename FLOATTYPE> TYPE ImGui::ScaleValueFromRatioT(...)
    private static T ScaleValueFromRatioT<T, TS, TF>(ImGuiDataType data_type, float t, T v_min, T v_max, float logarithmic_zero_epsilon, float zero_deadzone_halfsize)
        where T : unmanaged, INumber<T>
        where TS : unmanaged, INumber<TS>
        where TF : unmanaged, INumber<TF>
    {
        // We special-case the extents because otherwise our logarithmic fudging can lead to "mathematically correct"
        // but non-intuitive behaviors like a fully-left slider not actually reaching the minimum value. Also generally simpler.
        if (t <= 0.0f || v_min == v_max)
            return v_min;
        if (t >= 1.0f)
            return v_max;

        T result = T.Zero;
        if (logarithmic_zero_epsilon > 0.0f) // == is_logarithmic from caller
        {
            // Fudge min/max to avoid getting silly results close to zero
            TF v_min_fudged = (ImAbsTF(TF.CreateTruncating(v_min)) < TF.CreateTruncating(logarithmic_zero_epsilon)) ? ((v_min < T.Zero) ? TF.CreateTruncating(-logarithmic_zero_epsilon) : TF.CreateTruncating(logarithmic_zero_epsilon)) : TF.CreateTruncating(v_min);
            TF v_max_fudged = (ImAbsTF(TF.CreateTruncating(v_max)) < TF.CreateTruncating(logarithmic_zero_epsilon)) ? ((v_max < T.Zero) ? TF.CreateTruncating(-logarithmic_zero_epsilon) : TF.CreateTruncating(logarithmic_zero_epsilon)) : TF.CreateTruncating(v_max);

            bool flipped = v_max < v_min; // Check if range is "backwards"
            if (flipped)
                ImSwapT(ref v_min_fudged, ref v_max_fudged);

            // Awkward special case - we need ranges of the form (-100 .. 0) to convert to (-100 .. -epsilon), not (-100 .. epsilon)
            if ((v_max == T.Zero) && (v_min < T.Zero))
                v_max_fudged = TF.CreateTruncating(-logarithmic_zero_epsilon);

            float t_with_flip = flipped ? (1.0f - t) : t; // t, but flipped if necessary to account for us flipping the range

            if (TF.CreateTruncating(v_min) * TF.CreateTruncating(v_max) < TF.Zero) // Range crosses zero, so we have to do this in two parts
            {
                float zero_point_center = (-float.CreateTruncating(ImMinT(v_min, v_max))) / MathF.Abs(float.CreateTruncating(v_max) - float.CreateTruncating(v_min)); // The zero point in parametric space
                float zero_point_snap_L = zero_point_center - zero_deadzone_halfsize;
                float zero_point_snap_R = zero_point_center + zero_deadzone_halfsize;
                if (t_with_flip >= zero_point_snap_L && t_with_flip <= zero_point_snap_R)
                    result = T.Zero; // Special case to make getting exactly zero possible (the epsilon prevents it otherwise)
                else if (t_with_flip < zero_point_center)
                    result = T.CreateTruncating(-(TF.CreateTruncating(logarithmic_zero_epsilon) * ImPowTF(-v_min_fudged / TF.CreateTruncating(logarithmic_zero_epsilon), TF.CreateTruncating(1.0f - (t_with_flip / zero_point_snap_L)))));
                else
                    result = T.CreateTruncating(TF.CreateTruncating(logarithmic_zero_epsilon) * ImPowTF(v_max_fudged / TF.CreateTruncating(logarithmic_zero_epsilon), TF.CreateTruncating((t_with_flip - zero_point_snap_R) / (1.0f - zero_point_snap_R))));
            }
            else if ((v_min < T.Zero) || (v_max < T.Zero)) // Entirely negative slider
                result = T.CreateTruncating(-(-v_max_fudged * ImPowTF(-v_min_fudged / -v_max_fudged, TF.CreateTruncating(1.0f - t_with_flip))));
            else
                result = T.CreateTruncating(v_min_fudged * ImPowTF(v_max_fudged / v_min_fudged, TF.CreateTruncating(t_with_flip)));
        }
        else
        {
            // Linear slider
            bool is_floating_point = (data_type == ImGuiDataType_Float) || (data_type == ImGuiDataType_Double);
            if (is_floating_point)
            {
                result = v_min + (v_max - v_min) * T.CreateTruncating(t);
            }
            else // t is already < 1.0 here
            {
                // - For integer values we want the clicking position to match the grab box so we round above
                //   This code is carefully tuned to work with large values (e.g. high ranges of U64) while preserving this property..
                // - Not doing a *1.0 multiply at the end of a range as it tends to be lossy. While absolute aiming at a large s64/u64
                //   range is going to be imprecise anyway, with this check we at least make the edge values matches expected limits.
                TF v_new_off_f = TF.CreateTruncating(TS.CreateTruncating(v_max - v_min)) * TF.CreateTruncating(t);
                result = T.CreateTruncating(TS.CreateTruncating(v_min) + TS.CreateTruncating(v_new_off_f + (v_min > v_max ? TF.CreateTruncating(-0.5f) : TF.CreateTruncating(0.5f))));
            }
        }

        return result;
    }

    // FIXME: Try to move more of the code into shared SliderBehavior()
    // C++: template<typename TYPE, typename SIGNEDTYPE, typename FLOATTYPE> bool ImGui::SliderBehaviorT(...)
    private static bool SliderBehaviorT<T, TS, TF>(ImRect bb, ImGuiID id, ImGuiDataType data_type, T* v, T v_min, T v_max, string format, ImGuiSliderFlags flags, out ImRect out_grab_bb)
        where T : unmanaged, INumber<T>
        where TS : unmanaged, INumber<TS>
        where TF : unmanaged, INumber<TF>
    {
        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;

        ImGuiAxis axis = (flags & ImGuiSliderFlags_Vertical) != 0 ? ImGuiAxis_Y : ImGuiAxis_X;
        bool is_logarithmic = (flags & ImGuiSliderFlags_Logarithmic) != 0;
        bool is_floating_point = (data_type == ImGuiDataType_Float) || (data_type == ImGuiDataType_Double);
        float v_range_f = float.CreateTruncating(v_min < v_max ? v_max - v_min : v_min - v_max); // We don't need high precision for what we do with it.

        // Calculate bounds
        const float grab_padding = 2.0f; // FIXME: Should be part of style.
        float slider_sz = (bb.Max[axis] - bb.Min[axis]) - grab_padding * 2.0f;
        float grab_sz = style.GrabMinSize;
        if (!is_floating_point && v_range_f >= 0.0f)                         // v_range_f < 0 may happen on integer overflows
            grab_sz = ImMath.ImMax(slider_sz / (v_range_f + 1), style.GrabMinSize); // For integer sliders: if possible have the grab size represent 1 unit
        grab_sz = ImMath.ImMin(grab_sz, slider_sz);
        float slider_usable_sz = slider_sz - grab_sz;
        float slider_usable_pos_min = bb.Min[axis] + grab_padding + grab_sz * 0.5f;
        float slider_usable_pos_max = bb.Max[axis] - grab_padding - grab_sz * 0.5f;

        float logarithmic_zero_epsilon = 0.0f; // Only valid when is_logarithmic is true
        float zero_deadzone_halfsize = 0.0f; // Only valid when is_logarithmic is true
        if (is_logarithmic)
        {
            // When using logarithmic sliders, we need to clamp to avoid hitting zero, but our choice of clamp value greatly affects slider precision. We attempt to use the specified precision to estimate a good lower bound.
            int decimal_precision = is_floating_point ? ImParseFormatPrecision(format, 3) : 1;
            logarithmic_zero_epsilon = ImMath.ImPow(0.1f, (float)decimal_precision);
            zero_deadzone_halfsize = (style.LogSliderDeadzone * 0.5f) / ImMath.ImMax(slider_usable_sz, 1.0f);
        }

        // Process interacting with the slider
        bool value_changed = false;
        if (g.ActiveId == id)
        {
            bool set_new_value = false;
            float clicked_t = 0.0f;
            if (g.ActiveIdSource == ImGuiInputSource_Mouse)
            {
                float mouse_abs_pos = g.IO.MousePos[axis];
                if (g.ActiveIdIsJustActivated)
                {
                    float grab_t = ScaleRatioFromValueT<T, TS, TF>(data_type, *v, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);
                    if (axis == ImGuiAxis_Y)
                        grab_t = 1.0f - grab_t;
                    float grab_pos = ImMath.ImLerp(slider_usable_pos_min, slider_usable_pos_max, grab_t);
                    bool clicked_around_grab = (mouse_abs_pos >= grab_pos - grab_sz * 0.5f - 1.0f) && (mouse_abs_pos <= grab_pos + grab_sz * 0.5f + 1.0f); // No harm being extra generous here.
                    g.SliderGrabClickOffset = (clicked_around_grab && is_floating_point) ? mouse_abs_pos - grab_pos : 0.0f;
                }
                if (slider_usable_sz > 0.0f)
                    clicked_t = ImMath.ImSaturate((mouse_abs_pos - g.SliderGrabClickOffset - slider_usable_pos_min) / slider_usable_sz);
                if (axis == ImGuiAxis_Y)
                    clicked_t = 1.0f - clicked_t;
                set_new_value = true;
            }
            else if (g.ActiveIdSource == ImGuiInputSource_Keyboard || g.ActiveIdSource == ImGuiInputSource_Gamepad)
            {
                float input_delta = (axis == ImGuiAxis_X) ? GetNavTweakPressedAmount(axis) : -GetNavTweakPressedAmount(axis);
                if (input_delta != 0.0f)
                {
                    bool tweak_slow = IsKeyDown(g.NavInputSource == ImGuiInputSource_Gamepad ? ImGuiKey_NavGamepadTweakSlow : ImGuiKey_NavKeyboardTweakSlow);
                    bool tweak_fast = IsKeyDown(g.NavInputSource == ImGuiInputSource_Gamepad ? ImGuiKey_NavGamepadTweakFast : ImGuiKey_NavKeyboardTweakFast);
                    int decimal_precision = is_floating_point ? ImParseFormatPrecision(format, 3) : 0;
                    if (decimal_precision > 0)
                    {
                        input_delta /= 100.0f; // Keyboard/Gamepad tweak speeds in % of slider bounds
                        if (tweak_slow)
                            input_delta /= 10.0f;
                    }
                    else
                    {
                        if ((v_range_f >= -100.0f && v_range_f <= 100.0f && v_range_f != 0.0f) || tweak_slow)
                            input_delta = ((input_delta < 0.0f) ? -1.0f : +1.0f) / v_range_f; // Keyboard/Gamepad tweak speeds in integer steps
                        else
                            input_delta /= 100.0f;
                    }
                    if (tweak_fast)
                        input_delta *= 10.0f;

                    g.SliderCurrentAccum += input_delta;
                    g.SliderCurrentAccumDirty = true;
                }

                float delta = g.SliderCurrentAccum;
                if (g.SliderCurrentAccumDirty)
                {
                    clicked_t = ScaleRatioFromValueT<T, TS, TF>(data_type, *v, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);

                    if ((clicked_t >= 1.0f && delta > 0.0f) || (clicked_t <= 0.0f && delta < 0.0f)) // This is to avoid applying the saturation when already past the limits
                    {
                        set_new_value = false;
                        g.SliderCurrentAccum = 0.0f; // If pushing up against the limits, don't continue to accumulate
                    }
                    else
                    {
                        set_new_value = true;
                        float old_clicked_t = clicked_t;
                        clicked_t = ImMath.ImSaturate(clicked_t + delta);

                        // Calculate what our "new" clicked_t will be, and thus how far we actually moved the slider, and subtract this from the accumulator
                        T v_new = ScaleValueFromRatioT<T, TS, TF>(data_type, clicked_t, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);
                        if (is_floating_point && (flags & ImGuiSliderFlags_NoRoundToFormat) == 0)
                            v_new = RoundScalarWithFormatT<T>(format, data_type, v_new);
                        float new_clicked_t = ScaleRatioFromValueT<T, TS, TF>(data_type, v_new, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);

                        if (delta > 0)
                            g.SliderCurrentAccum -= ImMath.ImMin(new_clicked_t - old_clicked_t, delta);
                        else
                            g.SliderCurrentAccum -= ImMath.ImMax(new_clicked_t - old_clicked_t, delta);
                    }
                    g.SliderCurrentAccumDirty = false;
                }
            }

            if (set_new_value)
                if ((g.LastItemData.ItemFlags & ImGuiItemFlags_ReadOnly) != 0 || (flags & ImGuiSliderFlags_ReadOnly) != 0)
                    set_new_value = false;

            if (set_new_value)
            {
                T v_new = ScaleValueFromRatioT<T, TS, TF>(data_type, clicked_t, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);

                // Round to user desired precision based on format string
                if (is_floating_point && (flags & ImGuiSliderFlags_NoRoundToFormat) == 0)
                    v_new = RoundScalarWithFormatT<T>(format, data_type, v_new);

                // Apply result
                if (*v != v_new)
                {
                    *v = v_new;
                    value_changed = true;
                }
            }
        }

        if (slider_sz < 1.0f)
        {
            out_grab_bb = new ImRect(bb.Min, bb.Min);
        }
        else
        {
            // Output grab position so it can be displayed by the caller
            float grab_t = ScaleRatioFromValueT<T, TS, TF>(data_type, *v, v_min, v_max, logarithmic_zero_epsilon, zero_deadzone_halfsize);
            if (axis == ImGuiAxis_Y)
                grab_t = 1.0f - grab_t;
            float grab_pos = ImMath.ImLerp(slider_usable_pos_min, slider_usable_pos_max, grab_t);
            if (axis == ImGuiAxis_X)
                out_grab_bb = new ImRect(grab_pos - grab_sz * 0.5f, bb.Min.y + grab_padding, grab_pos + grab_sz * 0.5f, bb.Max.y - grab_padding);
            else
                out_grab_bb = new ImRect(bb.Min.x + grab_padding, grab_pos - grab_sz * 0.5f, bb.Max.x - grab_padding, grab_pos + grab_sz * 0.5f);
        }

        return value_changed;
    }

    // For 32-bit and larger types, slider bounds are limited to half the natural type range.
    // So e.g. an integer Slider between INT_MAX-10 and INT_MAX will fail, but an integer Slider between INT_MAX/2-10 and INT_MAX/2 will be ok.
    // It would be possible to lift that limitation with some work but it doesn't seem to be worth it for sliders.
    public static bool SliderBehavior(ImRect bb, ImGuiID id, ImGuiDataType data_type, IntPtr p_v, IntPtr p_min, IntPtr p_max, string format, ImGuiSliderFlags flags, out ImRect out_grab_bb)
    {
        // Read imgui.cpp "API BREAKING CHANGES" section for 1.78 if you hit this assert.
        Debug.Assert((flags == 1 || (flags & ImGuiSliderFlags_InvalidMask_) == 0), "Invalid ImGuiSliderFlags flags! Has the legacy 'float power' argument been mistakenly cast to flags? Call function with ImGuiSliderFlags_Logarithmic flags instead.");
        Debug.Assert((flags & ImGuiSliderFlags_WrapAround) == 0); // Not supported by SliderXXX(), only by DragXXX()

        // Things we can do easily outside the SliderBehaviorT<> template, saves code generation.
        ImGuiContext g = GImGui;
        if (g.ActiveId == id)
        {
            if (g.ActiveIdSource == ImGuiInputSource_Mouse && !g.IO.MouseDown[0])
                ClearActiveID();
            if (g.ActiveIdSource == ImGuiInputSource_Keyboard || g.ActiveIdSource == ImGuiInputSource_Gamepad)
            {
                if (g.ActiveIdIsJustActivated)
                {
                    g.SliderCurrentAccum = 0.0f; // Reset any stored nav delta upon activation
                    g.SliderCurrentAccumDirty = false;
                }
                if (g.NavActivatePressedId == id && !g.ActiveIdIsJustActivated)
                    ClearActiveID();
            }
            if (ShortcutsForCancel(id) && g.ActiveId == id) //-V560
            {
                // Canceling action reverts to initial value
                int data_size = DataTypeGetInfo(data_type).Size;
                bool value_changed = DataTypeStorageLoad(p_v, data_size) != g.ActiveIdValueOnActivation.Data;
                DataTypeStorageStore(p_v, data_size, g.ActiveIdValueOnActivation.Data);
                ClearActiveID();
                out_grab_bb = default;
                return value_changed;
            }
        }

        void* pv = (void*)p_v;
        void* pmin = (void*)p_min;
        void* pmax = (void*)p_max;
        switch (data_type)
        {
            case ImGuiDataType_S8:
            {
                ImS32 v32 = *(sbyte*)pv;
                bool r = SliderBehaviorT<ImS32, ImS32, float>(bb, id, ImGuiDataType_S32, &v32, *(sbyte*)pmin, *(sbyte*)pmax, format, flags, out out_grab_bb);
                if (r) *(sbyte*)pv = (sbyte)v32;
                return r;
            }
            case ImGuiDataType_U8:
            {
                ImU32 v32 = *(byte*)pv;
                bool r = SliderBehaviorT<ImU32, ImS32, float>(bb, id, ImGuiDataType_U32, &v32, *(byte*)pmin, *(byte*)pmax, format, flags, out out_grab_bb);
                if (r) *(byte*)pv = (byte)v32;
                return r;
            }
            case ImGuiDataType_S16:
            {
                ImS32 v32 = *(short*)pv;
                bool r = SliderBehaviorT<ImS32, ImS32, float>(bb, id, ImGuiDataType_S32, &v32, *(short*)pmin, *(short*)pmax, format, flags, out out_grab_bb);
                if (r) *(short*)pv = (short)v32;
                return r;
            }
            case ImGuiDataType_U16:
            {
                ImU32 v32 = *(ushort*)pv;
                bool r = SliderBehaviorT<ImU32, ImS32, float>(bb, id, ImGuiDataType_U32, &v32, *(ushort*)pmin, *(ushort*)pmax, format, flags, out out_grab_bb);
                if (r) *(ushort*)pv = (ushort)v32;
                return r;
            }
            case ImGuiDataType_S32:
                Debug.Assert(*(int*)pmin >= IM_S32_MIN / 2 && *(int*)pmax <= IM_S32_MAX / 2);
                return SliderBehaviorT<ImS32, ImS32, float >(bb, id, data_type, (int*)pv,   *(int*)pmin,   *(int*)pmax,   format, flags, out out_grab_bb);
            case ImGuiDataType_U32:
                Debug.Assert(*(uint*)pmax <= IM_U32_MAX / 2);
                return SliderBehaviorT<ImU32, ImS32, float >(bb, id, data_type, (uint*)pv,  *(uint*)pmin,  *(uint*)pmax,  format, flags, out out_grab_bb);
            case ImGuiDataType_S64:
                Debug.Assert(*(long*)pmin >= IM_S64_MIN / 2 && *(long*)pmax <= IM_S64_MAX / 2);
                return SliderBehaviorT<ImS64, ImS64, double>(bb, id, data_type, (long*)pv,  *(long*)pmin,  *(long*)pmax,  format, flags, out out_grab_bb);
            case ImGuiDataType_U64:
                Debug.Assert(*(ulong*)pmax <= IM_U64_MAX / 2);
                return SliderBehaviorT<ImU64, ImS64, double>(bb, id, data_type, (ulong*)pv, *(ulong*)pmin, *(ulong*)pmax, format, flags, out out_grab_bb);
            case ImGuiDataType_Float:
                Debug.Assert(*(float*)pmin >= -float.MaxValue / 2.0f && *(float*)pmax <= float.MaxValue / 2.0f);
                return SliderBehaviorT<float, float, float  >(bb, id, data_type, (float*)pv,  *(float*)pmin,  *(float*)pmax,  format, flags, out out_grab_bb);
            case ImGuiDataType_Double:
                Debug.Assert(*(double*)pmin >= -double.MaxValue / 2.0f && *(double*)pmax <= double.MaxValue / 2.0f);
                return SliderBehaviorT<double, double, double>(bb, id, data_type, (double*)pv, *(double*)pmin, *(double*)pmax, format, flags, out out_grab_bb);
            case ImGuiDataType_COUNT: break;
        }
        Debug.Assert(false);
        out_grab_bb = default;
        return false;
    }

    // Note: p_data, p_min and p_max are _pointers_ to a memory address holding the data. For a slider, they are all required.
    // Read code of e.g. SliderFloat(), SliderInt() etc. or examples in 'Demo->Widgets->Data Types' to understand how to use this function directly.
    public static bool SliderScalar(string label, ImGuiDataType data_type, IntPtr p_data, IntPtr p_min, IntPtr p_max, string format = null, ImGuiSliderFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);
        float w = CalcItemWidth();
        ImU32 color_marker = (g.NextItemData.HasFlags & ImGuiNextItemDataFlags_HasColorMarker) != 0 ? g.NextItemData.ColorMarker : 0;

        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImRect frame_bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + new ImVec2(w, label_size.y + style.FramePadding.y * 2.0f));
        ImRect total_bb = new ImRect(frame_bb.Min, frame_bb.Max + new ImVec2(label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f, 0.0f));

        bool temp_input_allowed = (flags & ImGuiSliderFlags_NoInput) == 0;
        ItemSize(total_bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(total_bb, id, frame_bb, temp_input_allowed ? ImGuiItemFlags_Inputable : 0))
            return false;

        // Default format string when passing NULL
        if (format == null)
            format = DataTypeGetInfo(data_type).PrintFmt;

        bool hovered = ItemHoverable(frame_bb, id, g.LastItemData.ItemFlags);
        bool temp_input_is_active = temp_input_allowed && TempInputIsActive(id);
        if (!temp_input_is_active)
        {
            // Tabbing or Ctrl+Click on Slider turns it into an input box
            bool clicked = hovered && IsMouseClicked(0, ImGuiInputFlags_None, id);
            bool make_active = (clicked || g.NavActivateId == id);
            if (make_active && clicked)
                SetKeyOwner(ImGuiKey_MouseLeft, id);
            if (make_active && temp_input_allowed)
                if ((clicked && g.IO.KeyCtrl) || (g.NavActivateId == id && (g.NavActivateFlags & ImGuiActivateFlags_PreferInput) != 0))
                    temp_input_is_active = true;

            // Store initial value (not used by main lib but available as a convenience but some mods e.g. to revert)
            if (make_active)
                g.ActiveIdValueOnActivation.Data = DataTypeStorageLoad(p_data, DataTypeGetInfo(data_type).Size);

            if (make_active && !temp_input_is_active)
            {
                SetActiveID(id, window);
                SetFocusID(id, window);
                FocusWindow(window);
                g.ActiveIdUsingNavDirMask |= (ImU32)((1 << ImGuiDir_Left) | (1 << ImGuiDir_Right));
            }
        }

        if (temp_input_is_active)
        {
            // Only clamp Ctrl+Click input when ImGuiSliderFlags_ClampOnInput is set (generally via ImGuiSliderFlags_AlwaysClamp)
            bool clamp_enabled = (flags & ImGuiSliderFlags_ClampOnInput) != 0; // Don't use TempInputIsClampEnabled()
            return TempInputScalar(frame_bb, id, label, data_type, p_data, format, clamp_enabled ? p_min : default, clamp_enabled ? p_max : default);
        }

        // Draw frame
        ImU32 frame_col = GetColorU32(g.ActiveId == id ? ImGuiCol_FrameBgActive : hovered ? ImGuiCol_FrameBgHovered : ImGuiCol_FrameBg);
        RenderNavCursor(frame_bb, id);
        RenderFrame(frame_bb.Min, frame_bb.Max, frame_col, false, style.FrameRounding);
        if (color_marker != 0 && style.ColorMarkerSize > 0.0f)
            RenderColorComponentMarker(frame_bb, GetColorU32(color_marker), style.FrameRounding);
        RenderFrameBorder(frame_bb.Min, frame_bb.Max, g.Style.FrameRounding);

        // Slider behavior
        ImRect grab_bb;
        bool value_changed = SliderBehavior(frame_bb, id, data_type, p_data, p_min, p_max, format, flags, out grab_bb);
        if (value_changed)
            MarkItemEdited(id);

        // Render grab
        if (grab_bb.Max.x > grab_bb.Min.x)
            window.DrawList.AddRectFilled(grab_bb.Min, grab_bb.Max, GetColorU32(g.ActiveId == id ? ImGuiCol_SliderGrabActive : ImGuiCol_SliderGrab), style.GrabRounding);

        // Display value using user-provided display format so user can add prefix/suffix/decorations to the value.
        if (g.LogEnabled)
            LogSetNextTextDecoration("{", "}");
        RenderValueTextClipped(data_type, p_data, format, frame_bb.Min, frame_bb.Max, new ImVec2(0.5f, 0.5f));

        if (label_size.x > 0.0f)
            RenderText(new ImVec2(frame_bb.Max.x + style.ItemInnerSpacing.x, frame_bb.Min.y + style.FramePadding.y), label, label_end, false);

        // IMGUI_TEST_ENGINE_ITEM_INFO(id, label, g.LastItemData.StatusFlags | (temp_input_allowed ? ImGuiItemStatusFlags_Inputable : 0)); // (omitted)
        return value_changed;
    }

    // Add multiple sliders on 1 line for compact edition of multiple components
    public static bool SliderScalarN(string label, ImGuiDataType data_type, IntPtr p_data, int components, IntPtr p_min, IntPtr p_max, string format = null, ImGuiSliderFlags flags = 0)
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
            if ((flags & ImGuiSliderFlags_ColorMarkers) != 0)
                SetNextItemColorMarker(GDefaultRgbaColorMarkers[i]);
            value_changed |= SliderScalar("", data_type, p_data, p_min, p_max, format, flags);
            PopID();
            PopItemWidth();
            p_data = (IntPtr)((byte*)p_data + type_size);
        }
        PopID();

        int label_end = FindRenderedTextEnd(label);
        if (label_end != 0) // C++: if (label != label_end)
        {
            SameLine(0, g.Style.ItemInnerSpacing.x);
            TextEx(label, label_end);
        }

        EndGroup();
        return value_changed;
    }

    public static bool SliderFloat(string label, ref float v, float v_min, float v_max, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = &v)
            return SliderScalar(label, ImGuiDataType_Float, (IntPtr)pv, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool SliderFloat2(string label, float[] v, float v_min, float v_max, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = v)
            return SliderScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 2, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool SliderFloat3(string label, float[] v, float v_min, float v_max, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = v)
            return SliderScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 3, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool SliderFloat4(string label, float[] v, float v_min, float v_max, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = v)
            return SliderScalarN(label, ImGuiDataType_Float, (IntPtr)pv, 4, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool SliderAngle(string label, ref float v_rad, float v_degrees_min = -360.0f, float v_degrees_max = +360.0f, string format = null, ImGuiSliderFlags flags = 0)
    {
        if (format == null)
            format = "%.0f deg";
        float v_deg = (v_rad) * 360.0f / (2 * ImMath.IM_PI);
        bool value_changed = SliderFloat(label, ref v_deg, v_degrees_min, v_degrees_max, format, flags);
        if (value_changed)
            v_rad = v_deg * (2 * ImMath.IM_PI) / 360.0f;
        return value_changed;
    }

    public static bool SliderInt(string label, ref int v, int v_min, int v_max, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = &v)
            return SliderScalar(label, ImGuiDataType_S32, (IntPtr)pv, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool SliderInt2(string label, int[] v, int v_min, int v_max, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = v)
            return SliderScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 2, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool SliderInt3(string label, int[] v, int v_min, int v_max, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = v)
            return SliderScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 3, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool SliderInt4(string label, int[] v, int v_min, int v_max, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = v)
            return SliderScalarN(label, ImGuiDataType_S32, (IntPtr)pv, 4, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool VSliderScalar(string label, ImVec2 size, ImGuiDataType data_type, IntPtr p_data, IntPtr p_min, IntPtr p_max, string format = null, ImGuiSliderFlags flags = 0)
    {
        ImGuiWindow window = GetCurrentWindow();
        if (window.SkipItems)
            return false;

        ImGuiContext g = GImGui;
        ImGuiStyle style = g.Style;
        ImGuiID id = window.GetID(label);

        int label_end = FindRenderedTextEnd(label);
        ImVec2 label_size = CalcTextSize(label, label_end, false);
        ImRect frame_bb = new ImRect(window.DC.CursorPos, window.DC.CursorPos + size);
        ImRect bb = new ImRect(frame_bb.Min, frame_bb.Max + new ImVec2(label_size.x > 0.0f ? style.ItemInnerSpacing.x + label_size.x : 0.0f, 0.0f));

        ItemSize(bb.GetSize(), style.FramePadding.y);
        if (!ItemAdd(frame_bb, id))
            return false;

        // Default format string when passing NULL
        if (format == null)
            format = DataTypeGetInfo(data_type).PrintFmt;

        bool hovered = ItemHoverable(frame_bb, id, g.LastItemData.ItemFlags);
        bool clicked = hovered && IsMouseClicked(0, ImGuiInputFlags_None, id);
        if (clicked || g.NavActivateId == id)
        {
            if (clicked)
                SetKeyOwner(ImGuiKey_MouseLeft, id);
            SetActiveID(id, window);
            SetFocusID(id, window);
            FocusWindow(window);
            g.ActiveIdUsingNavDirMask |= (ImU32)((1 << ImGuiDir_Up) | (1 << ImGuiDir_Down));
        }

        // Draw frame
        ImU32 frame_col = GetColorU32(g.ActiveId == id ? ImGuiCol_FrameBgActive : hovered ? ImGuiCol_FrameBgHovered : ImGuiCol_FrameBg);
        RenderNavCursor(frame_bb, id);
        RenderFrame(frame_bb.Min, frame_bb.Max, frame_col, true, g.Style.FrameRounding);

        // Slider behavior
        ImRect grab_bb;
        bool value_changed = SliderBehavior(frame_bb, id, data_type, p_data, p_min, p_max, format, flags | ImGuiSliderFlags_Vertical, out grab_bb);
        if (value_changed)
            MarkItemEdited(id);

        // Render grab
        if (grab_bb.Max.y > grab_bb.Min.y)
            window.DrawList.AddRectFilled(grab_bb.Min, grab_bb.Max, GetColorU32(g.ActiveId == id ? ImGuiCol_SliderGrabActive : ImGuiCol_SliderGrab), style.GrabRounding);

        // Display value using user-provided display format so user can add prefix/suffix/decorations to the value.
        // For the vertical slider we allow centered text to overlap the frame padding
        RenderValueTextClipped(data_type, p_data, format, new ImVec2(frame_bb.Min.x, frame_bb.Min.y + style.FramePadding.y), frame_bb.Max, new ImVec2(0.5f, 0.0f));
        if (label_size.x > 0.0f)
            RenderText(new ImVec2(frame_bb.Max.x + style.ItemInnerSpacing.x, frame_bb.Min.y + style.FramePadding.y), label, label_end, false);

        // IMGUI_TEST_ENGINE_ITEM_INFO(id, label, g.LastItemData.StatusFlags); // (omitted)
        return value_changed;
    }

    public static bool VSliderFloat(string label, ImVec2 size, ref float v, float v_min, float v_max, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        fixed (float* pv = &v)
            return VSliderScalar(label, size, ImGuiDataType_Float, (IntPtr)pv, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    public static bool VSliderInt(string label, ImVec2 size, ref int v, int v_min, int v_max, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        fixed (int* pv = &v)
            return VSliderScalar(label, size, ImGuiDataType_S32, (IntPtr)pv, (IntPtr)(&v_min), (IntPtr)(&v_max), format, flags);
    }

    // imgui_internal.h inline: void ImGui::SetNextItemColorMarker(ImU32 col)
    public static void SetNextItemColorMarker(ImU32 col)
    {
        ImGuiContext g = GImGui;
        g.NextItemData.HasFlags |= ImGuiNextItemDataFlags_HasColorMarker;
        g.NextItemData.ColorMarker = col;
    }
}
