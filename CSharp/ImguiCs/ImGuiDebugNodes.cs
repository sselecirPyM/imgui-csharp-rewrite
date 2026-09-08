// Port of imgui.cpp [SECTION] METRICS/DEBUGGER: ShowMetricsWindow(), ShowFontAtlas(),
// DebugNodeXXX() tree, viewport/keyboard debug renderers.
using System;
using System.Text;

namespace ImGui;

public static partial class ImGui
{
    // Windows Rect Type / Tables Rect Type enums (function-local enums in C++)
    const int WRT_OuterRect = 0, WRT_OuterRectClipped = 1, WRT_InnerRect = 2, WRT_InnerClipRect = 3, WRT_WorkRect = 4, WRT_Content = 5, WRT_ContentIdeal = 6, WRT_ContentRegionRect = 7, WRT_Count = 8;
    static readonly string[] wrt_rects_names = { "OuterRect", "OuterRectClipped", "InnerRect", "InnerClipRect", "WorkRect", "Content", "ContentIdeal", "ContentRegionRect" };
    const int TRT_OuterRect = 0, TRT_InnerRect = 1, TRT_WorkRect = 2, TRT_HostClipRect = 3, TRT_InnerClipRect = 4, TRT_BackgroundClipRect = 5, TRT_ColumnsRect = 6, TRT_ColumnsWorkRect = 7, TRT_ColumnsClipRect = 8, TRT_ColumnsContentHeadersUsed = 9, TRT_ColumnsContentHeadersIdeal = 10, TRT_ColumnsContentFrozen = 11, TRT_ColumnsContentUnfrozen = 12, TRT_Count = 13;
    static readonly string[] trt_rects_names = { "OuterRect", "InnerRect", "WorkRect", "HostClipRect", "InnerClipRect", "BackgroundClipRect", "ColumnsRect", "ColumnsWorkRect", "ColumnsClipRect", "ColumnsContentHeadersUsed", "ColumnsContentHeadersIdeal", "ColumnsContentFrozen", "ColumnsContentUnfrozen" };

    // C++: struct Funcs { static ImRect GetTableRect(...) }
    static ImRect Metrics_GetTableRect(ImGuiTable table, int rect_type, int n)
    {
        ImGuiTableInstanceData table_instance = TableGetInstanceData(table, table.InstanceCurrent); // Always using last submitted instance
        switch (rect_type)
        {
            case TRT_OuterRect: return table.OuterRect;
            case TRT_InnerRect: return table.InnerRect;
            case TRT_WorkRect: return table.WorkRect;
            case TRT_HostClipRect: return table.HostClipRect;
            case TRT_InnerClipRect: return table.InnerClipRect;
            case TRT_BackgroundClipRect: return table.BgClipRect;
            case TRT_ColumnsRect: { var c = table.Columns[n]; return new ImRect(c.MinX, table.InnerClipRect.Min.y, c.MaxX, table.InnerClipRect.Min.y + table_instance.LastOuterHeight); }
            case TRT_ColumnsWorkRect: { var c = table.Columns[n]; return new ImRect(c.WorkMinX, table.WorkRect.Min.y, c.WorkMaxX, table.WorkRect.Max.y); }
            case TRT_ColumnsClipRect: { var c = table.Columns[n]; return c.ClipRect; }
            case TRT_ColumnsContentHeadersUsed: { var c = table.Columns[n]; return new ImRect(c.WorkMinX, table.InnerClipRect.Min.y, c.ContentMaxXHeadersUsed, table.InnerClipRect.Min.y + table_instance.LastTopHeadersRowHeight); } // Note: y1/y2 not always accurate
            case TRT_ColumnsContentHeadersIdeal: { var c = table.Columns[n]; return new ImRect(c.WorkMinX, table.InnerClipRect.Min.y, c.ContentMaxXHeadersIdeal, table.InnerClipRect.Min.y + table_instance.LastTopHeadersRowHeight); }
            case TRT_ColumnsContentFrozen: { var c = table.Columns[n]; return new ImRect(c.WorkMinX, table.InnerClipRect.Min.y, c.ContentMaxXFrozen, table.InnerClipRect.Min.y + table_instance.LastFrozenHeight); }
            case TRT_ColumnsContentUnfrozen: { var c = table.Columns[n]; return new ImRect(c.WorkMinX, table.InnerClipRect.Min.y + table_instance.LastFrozenHeight, c.ContentMaxXUnfrozen, table.InnerClipRect.Max.y); }
        }
        System.Diagnostics.Debug.Assert(false);
        return new ImRect();
    }

    // C++: struct Funcs { static ImRect GetWindowRect(...) }
    static ImRect Metrics_GetWindowRect(ImGuiWindow window, int rect_type)
    {
        switch (rect_type)
        {
            case WRT_OuterRect: return window.Rect();
            case WRT_OuterRectClipped: return window.OuterRectClipped;
            case WRT_InnerRect: return window.InnerRect;
            case WRT_InnerClipRect: return window.InnerClipRect;
            case WRT_WorkRect: return window.WorkRect;
            case WRT_Content: { ImVec2 min = window.InnerRect.Min - window.Scroll + window.WindowPadding; return new ImRect(min, min + window.ContentSize); }
            case WRT_ContentIdeal: { ImVec2 min = window.InnerRect.Min - window.Scroll + window.WindowPadding; return new ImRect(min, min + window.ContentSizeIdeal); }
            case WRT_ContentRegionRect: return window.ContentRegionRect;
        }
        System.Diagnostics.Debug.Assert(false);
        return new ImRect();
    }

    // C++: void ImGui::DebugRenderViewportThumbnail(ImDrawList* draw_list, ImGuiViewportP* viewport, const ImRect& bb)
    public static void DebugRenderViewportThumbnail(ImDrawList draw_list, ImGuiViewportP viewport, ImRect bb)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        ImVec2 scale = new ImVec2(bb.GetSize().x / viewport.Size.x, bb.GetSize().y / viewport.Size.y);
        ImVec2 off = bb.Min - viewport.Pos * scale;
        float alpha_mul = 1.0f;
        window.DrawList.AddRectFilled(bb.Min, bb.Max, GetColorU32(ImGuiCol_Border, alpha_mul * 0.40f));
        for (int win_i = 0; win_i < g.Windows.Size; win_i++)
        {
            ImGuiWindow thumb_window = g.Windows[win_i];
            if (!thumb_window.WasActive || (thumb_window.Flags & ImGuiWindowFlags_ChildWindow) != 0)
                continue;

            ImRect thumb_r = thumb_window.Rect();
            ImRect title_r = thumb_window.TitleBarRect();
            thumb_r = new ImRect(ImMath.ImTrunc(off + thumb_r.Min * scale), ImMath.ImTrunc(off + thumb_r.Max * scale));
            title_r = new ImRect(ImMath.ImTrunc(off + title_r.Min * scale), ImMath.ImTrunc(off + new ImVec2(title_r.Max.x, title_r.Min.y + title_r.GetHeight() * 3.0f) * scale)); // Exaggerate title bar height
            thumb_r.ClipWithFull(bb);
            title_r.ClipWithFull(bb);
            bool window_is_focused = (g.NavWindow != null && thumb_window.RootWindowForTitleBarHighlight == g.NavWindow.RootWindowForTitleBarHighlight);
            window.DrawList.AddRectFilled(thumb_r.Min, thumb_r.Max, GetColorU32(ImGuiCol_WindowBg, alpha_mul));
            window.DrawList.AddRectFilled(title_r.Min, title_r.Max, GetColorU32(window_is_focused ? ImGuiCol_TitleBgActive : ImGuiCol_TitleBg, alpha_mul));
            window.DrawList.AddRect(thumb_r.Min, thumb_r.Max, GetColorU32(ImGuiCol_Border, alpha_mul));
            window.DrawList.AddText(g.Font, g.FontSize * 1.0f, title_r.Min, GetColorU32(ImGuiCol_Text, alpha_mul), thumb_window.Name, FindRenderedTextEnd(thumb_window.Name));
        }
        draw_list.AddRect(bb.Min, bb.Max, GetColorU32(ImGuiCol_Border, alpha_mul));
        if (viewport.ID == g.DebugMetricsConfig.HighlightViewportID)
            window.DrawList.AddRect(bb.Min, bb.Max, ImGuiCore.IM_COL32(255, 255, 0, 255));
    }

    // C++: static void RenderViewportsThumbnails()
    static void RenderViewportsThumbnails()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        float SCALE = 1.0f / 8.0f;
        ImRect bb_full = new ImRect(g.Viewports[0].Pos, g.Viewports[0].Pos + g.Viewports[0].Size);
        ImVec2 p = window.DC.CursorPos;
        ImVec2 off = p - bb_full.Min * SCALE;

        // Draw viewports
        for (int vp_i = 0; vp_i < g.Viewports.Size; vp_i++)
        {
            ImGuiViewportP viewport = g.Viewports[vp_i];
            ImRect viewport_draw_bb = new ImRect(off + viewport.Pos * SCALE, off + (viewport.Pos + viewport.Size) * SCALE);
            DebugRenderViewportThumbnail(window.DrawList, viewport, viewport_draw_bb);
        }
        Dummy(bb_full.GetSize() * SCALE);
    }

    // Draw an arbitrary US keyboard layout to visualize translated keys
    // C++: void ImGui::DebugRenderKeyboardPreview(ImDrawList* draw_list)
    public static void DebugRenderKeyboardPreview(ImDrawList draw_list)
    {
        float scale = GetFontSize() / 13.0f;
        ImVec2 key_size = new ImVec2(35.0f, 35.0f) * scale;
        float key_rounding = 3.0f * scale;
        ImVec2 key_face_size = new ImVec2(25.0f, 25.0f) * scale;
        ImVec2 key_face_pos = new ImVec2(5.0f, 3.0f) * scale;
        float key_face_rounding = 2.0f * scale;
        ImVec2 key_label_pos = new ImVec2(7.0f, 4.0f) * scale;
        ImVec2 key_step = new ImVec2(key_size.x - 1.0f, key_size.y - 1.0f);
        float key_row_offset = 9.0f * scale;

        ImVec2 board_min = GetCursorScreenPos();
        ImVec2 board_max = new ImVec2(board_min.x + 3 * key_step.x + 2 * key_row_offset + 10.0f, board_min.y + 3 * key_step.y + 10.0f);
        ImVec2 start_pos = new ImVec2(board_min.x + 5.0f - key_step.x, board_min.y);

        (int Row, int Col, string Label, int Key)[] keys_to_display =
        {
            (0, 0, "", ImGuiKey_Tab),      (0, 1, "Q", ImGuiKey_Q), (0, 2, "W", ImGuiKey_W), (0, 3, "E", ImGuiKey_E), (0, 4, "R", ImGuiKey_R),
            (1, 0, "", ImGuiKey_CapsLock), (1, 1, "A", ImGuiKey_A), (1, 2, "S", ImGuiKey_S), (1, 3, "D", ImGuiKey_D), (1, 4, "F", ImGuiKey_F),
            (2, 0, "", ImGuiKey_LeftShift),(2, 1, "Z", ImGuiKey_Z), (2, 2, "X", ImGuiKey_X), (2, 3, "C", ImGuiKey_C), (2, 4, "V", ImGuiKey_V),
        };

        // Elements rendered manually via ImDrawList API are not clipped automatically.
        // While not strictly necessary, here IsItemVisible() is used to avoid rendering these shapes when they are out of view.
        Dummy(board_max - board_min);
        if (!IsItemVisible())
            return;
        draw_list.PushClipRect(board_min, board_max, true);
        for (int n = 0; n < keys_to_display.Length; n++)
        {
            (int Row, int Col, string Label, int Key) = keys_to_display[n];
            ImVec2 key_min = new ImVec2(start_pos.x + Col * key_step.x + Row * key_row_offset, start_pos.y + Row * key_step.y);
            ImVec2 key_max = key_min + key_size;
            draw_list.AddRectFilled(key_min, key_max, ImGuiCore.IM_COL32(204, 204, 204, 255), key_rounding);
            draw_list.AddRect(key_min, key_max, ImGuiCore.IM_COL32(24, 24, 24, 255), key_rounding);
            ImVec2 face_min = new ImVec2(key_min.x + key_face_pos.x, key_min.y + key_face_pos.y);
            ImVec2 face_max = new ImVec2(face_min.x + key_face_size.x, face_min.y + key_face_size.y);
            draw_list.AddRect(face_min, face_max, ImGuiCore.IM_COL32(193, 193, 193, 255), key_face_rounding, 2.0f);
            draw_list.AddRectFilled(face_min, face_max, ImGuiCore.IM_COL32(252, 252, 252, 255), key_face_rounding);
            ImVec2 label_min = new ImVec2(key_min.x + key_label_pos.x, key_min.y + key_label_pos.y);
            draw_list.AddText(label_min, ImGuiCore.IM_COL32(64, 64, 64, 255), Label);
            if (IsKeyDown(Key))
                draw_list.AddRectFilled(key_min, key_max, ImGuiCore.IM_COL32(255, 0, 0, 128), key_rounding);
        }
        draw_list.PopClipRect();
    }

    // C++: static const char* FormatTextureRefForDebugDisplay(char* buf, int buf_size, ImTextureRef tex_ref)
    static string FormatTextureRefForDebugDisplay(ImTextureRef tex_ref)
    {
        string prefix = (tex_ref._TexData != null) ? string.Format("#{0:D3}: ", tex_ref._TexData.UniqueID) : "";
        return prefix + string.Format("0x{0:X}", DebugTextureIDToU64(tex_ref.GetTexID()));
    }

    // C++: void ImGui::ShowFontAtlas(ImFontAtlas* atlas)   [imgui.cpp 16652]
    public static void ShowFontAtlas(ImFontAtlas atlas)
    {
        ImGuiContext g = GImGui;
        ImGuiIO io = g.IO;
        ImGuiStyle style = g.Style;

        BeginDisabled(true);
        CheckboxFlags("io.BackendFlags: RendererHasTextures", ref io.BackendFlags, ImGuiBackendFlags_RendererHasTextures);
        EndDisabled();
        ShowFontSelector("Font");
        if (DragFloat("FontSizeBase", ref style.FontSizeBase, 0.20f, 5.0f, 100.0f, "%.0f"))
            style._NextFrameFontSizeBase = style.FontSizeBase; // FIXME: Temporary hack until we finish remaining work.
        SameLine(0.0f, 0.0f); Text(" (out %.2f)", GetFontSize());
        SameLine(); MetricsHelpMarker("- This is scaling font only. General scaling will come later.");
        DragFloat("FontScaleMain", ref style.FontScaleMain, 0.02f, 0.5f, 4.0f);
        DragFloat("FontScaleDpi", ref style.FontScaleDpi, 0.02f, 0.5f, 4.0f);
        if ((io.BackendFlags & ImGuiBackendFlags_RendererHasTextures) == 0)
        {
            BulletText("Warning: Font scaling will NOT be smooth, because\nImGuiBackendFlags_RendererHasTextures is not set!");
            BulletText("For instructions, see:");
            SameLine();
            TextLinkOpenURL("docs/BACKENDS.md", "https://github.com/ocornut/imgui/blob/master/docs/BACKENDS.md");
        }
        BulletText("Load a nice font for better results!");
        BulletText("Please submit feedback:");
        SameLine(); TextLinkOpenURL("#8465", "https://github.com/ocornut/imgui/issues/8465");
        BulletText("Read FAQ for more details:");
        SameLine(); TextLinkOpenURL("dearimgui.com/faq", "https://www.dearimgui.com/faq/");

        SeparatorText("Font List");

        ImGuiMetricsConfig cfg = g.DebugMetricsConfig;
        Checkbox("Show font preview", ref cfg.ShowFontPreview);

        // Font loaders
        if (TreeNode("Loader", "Loader: '%s'", atlas.FontLoaderName ?? "NULL"))
        {
            BeginDisabled(!atlas.RendererHasTextures);
            var loader_stbtruetype = ImFontAtlas.ImFontAtlasGetFontLoaderForStbTruetype();
            if (RadioButton("stb_truetype", atlas.FontLoader == loader_stbtruetype))
                atlas.SetFontLoader(loader_stbtruetype);
            SameLine();
            BeginDisabled(true); // IMGUI_ENABLE_FREETYPE is not compiled in the C# port
            RadioButton("FreeType", false);
            SetItemTooltip("Requires #define IMGUI_ENABLE_FREETYPE + imgui_freetype.cpp.");
            EndDisabled();
            EndDisabled();
            TreePop();
        }

        // Font list
        for (int font_i = 0; font_i < atlas.Fonts.Size; font_i++)
        {
            ImFont font = atlas.Fonts[font_i];
            PushID(font);
            DebugNodeFont(font);
            PopID();
        }

        SeparatorText("Font Atlas");
        if (Button("Compact"))
            atlas.CompactCache();
        SameLine();
        if (Button("Grow"))
            ImFontAtlas.ImFontAtlasTextureGrow(atlas);
        SameLine();
        if (Button("Clear All"))
            ImFontAtlas.ImFontAtlasBuildClear(atlas);
        SetItemTooltip("Destroy cache and custom rectangles.");

        for (int tex_n = 0; tex_n < atlas.TexList.Size; tex_n++)
        {
            ImTextureData tex = atlas.TexList[tex_n];
            if (tex_n > 0)
                SameLine();
            Text("Tex: %dx%d", tex.Width, tex.Height);
        }
        int packed_surface_sqrt = (int)Math.Sqrt(atlas.Builder.RectsPackedSurface);
        int discarded_surface_sqrt = (int)Math.Sqrt(atlas.Builder.RectsDiscardedSurface);
        Text("Packed rects: %d, area: about %d px ~%dx%d px", atlas.Builder.RectsPackedCount, atlas.Builder.RectsPackedSurface, packed_surface_sqrt, packed_surface_sqrt);
        Text("incl. Discarded rects: %d, area: about %d px ~%dx%d px", atlas.Builder.RectsDiscardedCount, atlas.Builder.RectsDiscardedSurface, discarded_surface_sqrt, discarded_surface_sqrt);

        int highlight_r_id = ImUtil.ImFontAtlasRectId_Invalid;
        if (TreeNode("Rects Index", "Rects Index (%d)", atlas.Builder.RectsPackedCount)) // <-- Use count of used rectangles
        {
            PushStyleVar(ImGuiStyleVar_ImageBorderSize, 1.0f);
            if (BeginTable("##table", 2, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_ScrollY, new ImVec2(0.0f, GetTextLineHeightWithSpacing() * 12)))
            {
                for (int entry_i = 0; entry_i < atlas.Builder.RectsIndex.Size; entry_i++)
                {
                    ImFontAtlasRectEntry entry = atlas.Builder.RectsIndex[entry_i];
                    if (!entry.IsUsed)
                        continue;
                    int id = ImUtil.ImFontAtlasRectId_Make(entry_i, (int)entry.Generation);
                    var r = new ImFontAtlasRect();
                    atlas.GetCustomRect(id, ref r);
                    string buf = string.Format("ID:{0:X8}, used:{1}, {{ w:{2,3}, h:{3,3} }} {{ x:{4,4}, y:{5,4} }}", id, entry.IsUsed ? 1 : 0, r.w, r.h, r.x, r.y);
                    TableNextColumn();
                    Selectable(buf);
                    if (IsItemHovered())
                        highlight_r_id = id;
                    TableNextColumn();
                    Image(atlas.TexRef, new ImVec2(r.w, r.h), r.uv0, r.uv1);
                }
                EndTable();
            }
            PopStyleVar();
            TreePop();
        }

        // Texture list
        // (ensure the last texture always use the same ID, so we can keep it open neatly)
        var highlight_r = new ImFontAtlasRect();
        bool has_highlight_r = highlight_r_id != ImUtil.ImFontAtlasRectId_Invalid;
        if (has_highlight_r)
            atlas.GetCustomRect(highlight_r_id, ref highlight_r);
        for (int tex_n = 0; tex_n < atlas.TexList.Size; tex_n++)
        {
            if (tex_n == atlas.TexList.Size - 1)
                SetNextItemOpen(true, ImGuiCond_Once);
            DebugNodeTexture(atlas.TexList[tex_n], atlas.TexList.Size - 1 - tex_n, has_highlight_r ? highlight_r : null);
        }
    }

    // C++: void ImGui::DebugNodeTexture(ImTextureData* tex, int int_id, const ImFontAtlasRect* highlight_rect)
    public static void DebugNodeTexture(ImTextureData tex, int int_id, ImFontAtlasRect? highlight_rect)
    {
        ImGuiContext g = GImGui;
        PushID(int_id);
        if (TreeNode("", "Texture #%03d (%dx%d pixels)", tex.UniqueID, tex.Width, tex.Height))
        {
            ImGuiMetricsConfig cfg = g.DebugMetricsConfig;
            Checkbox("Show used rect", ref cfg.ShowTextureUsedRect);
            PushStyleVar(ImGuiStyleVar_ImageBorderSize, ImMath.ImMax(1.0f, g.Style.ImageBorderSize));
            ImVec2 p = GetCursorScreenPos();
            if (tex.Status == ImTextureStatus_WantDestroy || tex.Status == ImTextureStatus_Destroyed)
                Dummy(new ImVec2(tex.Width, tex.Height));
            else
                ImageWithBg(tex.GetTexRef(), new ImVec2(tex.Width, tex.Height), new ImVec2(0.0f, 0.0f), new ImVec2(1.0f, 1.0f), new ImVec4(0.0f, 0.0f, 0.0f, 1.0f));
            if (cfg.ShowTextureUsedRect)
                GetWindowDrawList().AddRect(new ImVec2(p.x + tex.UsedRect.x, p.y + tex.UsedRect.y), new ImVec2(p.x + tex.UsedRect.x + tex.UsedRect.w, p.y + tex.UsedRect.y + tex.UsedRect.h), ImGuiCore.IM_COL32(255, 0, 255, 255));
            if (highlight_rect.HasValue)
            {
                var hr = highlight_rect.Value;
                ImRect r_outer = new ImRect(p.x, p.y, p.x + tex.Width, p.y + tex.Height);
                ImRect r_inner = new ImRect(p.x + hr.x, p.y + hr.y, p.x + hr.x + hr.w, p.y + hr.y + hr.h);
                RenderRectFilledWithHole(GetWindowDrawList(), r_outer, r_inner, ImGuiCore.IM_COL32(0, 0, 0, 100), 0.0f);
                GetWindowDrawList().AddRect(r_inner.Min - new ImVec2(1, 1), r_inner.Max + new ImVec2(1, 1), ImGuiCore.IM_COL32(255, 255, 0, 255));
            }
            PopStyleVar();

            Text("Status = %s (%d), Format = %s (%d), UseColors = %d", ImUtil.ImTextureDataGetStatusName(tex.Status), tex.Status, ImUtil.ImTextureDataGetFormatName(tex.Format), tex.Format, tex.UseColors ? 1 : 0);
            Text("TexRef = %s, BackendUserData = %p", FormatTextureRefForDebugDisplay(tex.GetTexRef()), tex.BackendUserData);
            TreePop();
        }
        PopID();
    }

    // C++: void ImGui::ShowMetricsWindow(bool* p_open)   [imgui.cpp 16838]
    public static void ShowMetricsWindow(ref bool p_open) => ShowMetricsWindow(ref p_open, null);
    public static void ShowMetricsWindow() => ShowMetricsWindow(ref _metricsP_openDummy, null);
    static bool _metricsP_openDummy;
    public static void ShowMetricsWindow(string label) => ShowMetricsWindow(ref _metricsP_openDummy, label);

    static byte[] _metricsTextEncodingBuf = new byte[64];

    public static void ShowMetricsWindow(ref bool p_open, string label)
    {
        ImGuiContext g = GImGui;
        ImGuiIO io = g.IO;
        ImGuiPlatformIO platform_io = g.PlatformIO;
        ImGuiMetricsConfig cfg = g.DebugMetricsConfig;
        if (cfg.ShowDebugLog)
            ShowDebugLogWindow(ref cfg.ShowDebugLog);
        if (cfg.ShowIDStackTool)
            ShowIDStackToolWindow(ref cfg.ShowIDStackTool);

        if (!Begin(label ?? "Dear ImGui Metrics/Debugger", ref p_open) || GetCurrentWindow().BeginCount > 1)
        {
            End();
            return;
        }

        // [DEBUG] Clear debug breaks hooks after exactly one cycle.
        DebugBreakClearData();

        // Basic info
        Text("Dear ImGui %s (%d)", ImGuiVersion, ImGuiVersionNum);
        if (g.ContextName.Length != 0)
        {
            SameLine();
            Text("(Context Name: \"%s\")", g.ContextName);
        }
        Text("Application average %.3f ms/frame (%.1f FPS)", 1000.0f / io.Framerate, io.Framerate);
        Text("%d vertices, %d indices (%d triangles)", io.MetricsRenderVertices, io.MetricsRenderIndices, io.MetricsRenderIndices / 3);
        Text("%d visible windows, %d current allocations", io.MetricsRenderWindows, g.DebugAllocInfo.TotalAllocCount - g.DebugAllocInfo.TotalFreeCount);
        //SameLine(); if (SmallButton("GC")) { g.GcCompactAll = true; }

        Separator();

        if (cfg.ShowWindowsRectsType < 0)
            cfg.ShowWindowsRectsType = WRT_WorkRect;
        if (cfg.ShowTablesRectsType < 0)
            cfg.ShowTablesRectsType = TRT_WorkRect;

        // Tools
        if (TreeNode("Tools"))
        {
            // Debug Break features
            // The Item Picker tool is super useful to visually select an item and break into the call-stack of where it was submitted.
            SeparatorTextEx(0, "Debug breaks", -1, CalcTextSize("(?)").x + g.Style.SeparatorTextPadding.x);
            SameLine();
            MetricsHelpMarker("Will call the IM_DEBUG_BREAK() macro to break in debugger.\nWarning: If you don't have a debugger attached, this will probably crash.");
            if (Checkbox("Show Item Picker", ref g.DebugItemPickerActive) && g.DebugItemPickerActive)
                DebugStartItemPicker();
            Checkbox("Show \"Debug Break\" buttons in other sections (io.ConfigDebugIsDebuggerPresent)", ref g.IO.ConfigDebugIsDebuggerPresent);

            SeparatorText("Visualize");

            Checkbox("Show Debug Log", ref cfg.ShowDebugLog);
            SameLine();
            MetricsHelpMarker("You can also call ImGui::ShowDebugLogWindow() from your code.");

            Checkbox("Show ID Stack Tool", ref cfg.ShowIDStackTool);
            SameLine();
            MetricsHelpMarker("You can also call ImGui::ShowIDStackToolWindow() from your code.");

            Checkbox("Show windows begin order", ref cfg.ShowWindowsBeginOrder);
            Checkbox("Show windows rectangles", ref cfg.ShowWindowsRects);
            SameLine();
            SetNextItemWidth(GetFontSize() * 12);
            cfg.ShowWindowsRects |= Combo("##show_windows_rect_type", ref cfg.ShowWindowsRectsType, wrt_rects_names, WRT_Count, WRT_Count);
            if (cfg.ShowWindowsRects && g.NavWindow != null)
            {
                BulletText("'%s':", g.NavWindow.Name);
                Indent();
                for (int rect_n = 0; rect_n < WRT_Count; rect_n++)
                {
                    ImRect r = Metrics_GetWindowRect(g.NavWindow, rect_n);
                    Text("(%6.1f,%6.1f) (%6.1f,%6.1f) Size (%6.1f,%6.1f) %s", r.Min.x, r.Min.y, r.Max.x, r.Max.y, r.GetWidth(), r.GetHeight(), wrt_rects_names[rect_n]);
                }
                Unindent();
            }

            Checkbox("Show tables rectangles", ref cfg.ShowTablesRects);
            SameLine();
            SetNextItemWidth(GetFontSize() * 12);
            cfg.ShowTablesRects |= Combo("##show_table_rects_type", ref cfg.ShowTablesRectsType, trt_rects_names, TRT_Count, TRT_Count);
            if (cfg.ShowTablesRects && g.NavWindow != null)
            {
                for (int table_n = 0; table_n < g.Tables.GetMapSize(); table_n++)
                {
                    ImGuiTable table = g.Tables.TryGetMapData(table_n);
                    if (table == null || table.LastFrameActive < g.FrameCount - 1 || (table.OuterWindow != g.NavWindow && table.InnerWindow != g.NavWindow))
                        continue;

                    BulletText("Table 0x%08X (%d columns, in '%s')", table.ID, table.ColumnsCount, table.OuterWindow.Name);
                    if (IsItemHovered())
                        GetForegroundDrawList(table.OuterWindow).AddRect(table.OuterRect.Min - new ImVec2(1, 1), table.OuterRect.Max + new ImVec2(1, 1), ImGuiCore.IM_COL32(255, 255, 0, 255), 0.0f, 2.0f);
                    Indent();
                    for (int rect_n = 0; rect_n < TRT_Count; rect_n++)
                    {
                        if (rect_n >= TRT_ColumnsRect)
                        {
                            if (rect_n != TRT_ColumnsRect && rect_n != TRT_ColumnsClipRect)
                                continue;
                            for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
                            {
                                ImRect r = Metrics_GetTableRect(table, rect_n, column_n);
                                string buf = string.Format("({0,6:F1},{1,6:F1}) ({2,6:F1},{3,6:F1}) Size ({4,6:F1},{5,6:F1}) Col {6} {7}", r.Min.x, r.Min.y, r.Max.x, r.Max.y, r.GetWidth(), r.GetHeight(), column_n, trt_rects_names[rect_n]);
                                Selectable(buf);
                                if (IsItemHovered())
                                    GetForegroundDrawList(table.OuterWindow).AddRect(r.Min - new ImVec2(1, 1), r.Max + new ImVec2(1, 1), ImGuiCore.IM_COL32(255, 255, 0, 255), 0.0f, 2.0f);
                            }
                        }
                        else
                        {
                            ImRect r = Metrics_GetTableRect(table, rect_n, -1);
                            string buf = string.Format("({0,6:F1},{1,6:F1}) ({2,6:F1},{3,6:F1}) Size ({4,6:F1},{5,6:F1}) {6}", r.Min.x, r.Min.y, r.Max.x, r.Max.y, r.GetWidth(), r.GetHeight(), trt_rects_names[rect_n]);
                            Selectable(buf);
                            if (IsItemHovered())
                                GetForegroundDrawList(table.OuterWindow).AddRect(r.Min - new ImVec2(1, 1), r.Max + new ImVec2(1, 1), ImGuiCore.IM_COL32(255, 255, 0, 255), 0.0f, 2.0f);
                        }
                    }
                    Unindent();
                }
            }
            Checkbox("Show groups rectangles", ref g.DebugShowGroupRects); // Storing in context as this is used by group code and prefers to be in hot-data

            SeparatorText("Validate");

            Checkbox("Debug Begin/BeginChild return value", ref io.ConfigDebugBeginReturnValueLoop);
            SameLine();
            MetricsHelpMarker("Some calls to Begin()/BeginChild() will return false.\n\nWill cycle through window depths then repeat. Windows should be flickering while running.");

            Checkbox("UTF-8 Encoding viewer", ref cfg.ShowTextEncodingViewer);
            SameLine();
            MetricsHelpMarker("You can also call ImGui::DebugTextEncoding() from your code with a given string to test that your UTF-8 encoding settings are correct.");
            if (cfg.ShowTextEncodingViewer)
            {
                SetNextItemWidth(-float.MinValue);
                InputText("##DebugTextEncodingBuf", _metricsTextEncodingBuf, _metricsTextEncodingBuf.Length);
                if (_metricsTextEncodingBuf[0] != 0)
                {
                    int z = Array.IndexOf(_metricsTextEncodingBuf, (byte)0);
                    DebugTextEncoding(Encoding.UTF8.GetString(_metricsTextEncodingBuf, 0, z < 0 ? _metricsTextEncodingBuf.Length : z));
                }
            }

            TreePop();
        }

        // Windows
        if (TreeNode("Windows", "Windows (%d)", g.Windows.Size))
        {
            //SetNextItemOpen(true, ImGuiCond_Once);
            DebugNodeWindowsList(g.Windows, "By display order");
            DebugNodeWindowsList(g.WindowsFocusOrder, "By focus order (root windows)");
            if (TreeNode("By submission order (begin stack)"))
            {
                // Here we display windows in their submitted order/hierarchy, however note that the Begin stack doesn't constitute a Parent<>Child relationship!
                ImVector<ImGuiWindow> temp_buffer = g.WindowsTempSortBuffer;
                temp_buffer.resize(0);
                for (int win_i = 0; win_i < g.Windows.Size; win_i++)
                {
                    ImGuiWindow window = g.Windows[win_i];
                    if (window.LastFrameActive + 1 >= g.FrameCount)
                        temp_buffer.push_back(window);
                }
                temp_buffer.Data.Sort((a, b) => a.BeginOrderWithinContext - b.BeginOrderWithinContext); // C++: ImQsort(WindowComparerByBeginOrder)
                DebugNodeWindowsListByBeginStackParent(temp_buffer, temp_buffer.Size, null);
                TreePop();
            }

            TreePop();
        }

        // DrawLists
        int drawlist_count = 0;
        for (int vp_i = 0; vp_i < g.Viewports.Size; vp_i++)
            drawlist_count += g.Viewports[vp_i].DrawDataP.CmdLists.Size;
        if (TreeNode("DrawLists", "DrawLists (%d)", drawlist_count))
        {
            Checkbox("Show ImDrawCmd mesh when hovering", ref cfg.ShowDrawCmdMesh);
            Checkbox("Show ImDrawCmd bounding boxes when hovering", ref cfg.ShowDrawCmdBoundingBoxes);
            for (int vp_i = 0; vp_i < g.Viewports.Size; vp_i++)
            {
                ImGuiViewportP viewport = g.Viewports[vp_i];
                for (int dl_i = 0; dl_i < viewport.DrawDataP.CmdLists.Size; dl_i++)
                    DebugNodeDrawList(null, viewport, viewport.DrawDataP.CmdLists[dl_i], "DrawList");
            }
            TreePop();
        }

        // Viewports
        if (TreeNode("Viewports", "Viewports (%d)", g.Viewports.Size))
        {
            SetNextItemOpen(true, ImGuiCond_Once);
            if (TreeNode("Windows Minimap"))
            {
                RenderViewportsThumbnails();
                TreePop();
            }
            cfg.HighlightViewportID = 0;

            for (int vp_i = 0; vp_i < g.Viewports.Size; vp_i++)
                DebugNodeViewport(g.Viewports[vp_i]);
            TreePop();
        }

        // Details for Fonts
        for (int atlas_i = 0; atlas_i < g.FontAtlases.Size; atlas_i++)
        {
            ImFontAtlas atlas = g.FontAtlases[atlas_i];
            if (TreeNode((object)atlas, "Fonts (%d), Textures (%d)", atlas.Fonts.Size, atlas.TexList.Size))
            {
                ShowFontAtlas(atlas);
                TreePop();
            }
        }

        // Details for Popups
        if (TreeNode("Popups", "Popups (%d)", g.OpenPopupStack.Size))
        {
            for (int pd_i = 0; pd_i < g.OpenPopupStack.Size; pd_i++)
            {
                // As it's difficult to interact with tree nodes while popups are open, we display everything inline.
                ImGuiPopupData popup_data = g.OpenPopupStack[pd_i];
                ImGuiWindow window = popup_data.Window;
                BulletText("PopupID: %08x, Window: '%s' (%s%s), RestoreNavWindow '%s', ParentWindow '%s'",
                    popup_data.PopupId, window != null ? window.Name : "NULL", window != null && (window.Flags & ImGuiWindowFlags_ChildWindow) != 0 ? "Child;" : "", window != null && (window.Flags & ImGuiWindowFlags_ChildMenu) != 0 ? "Menu;" : "",
                    popup_data.RestoreNavWindow != null ? popup_data.RestoreNavWindow.Name : "NULL", window != null && window.ParentWindow != null ? window.ParentWindow.Name : "NULL");
            }
            TreePop();
        }

        // Details for TabBars
        if (TreeNode("TabBars", "Tab Bars (%d)", g.TabBars.GetAliveCount()))
        {
            for (int n = 0; n < g.TabBars.GetMapSize(); n++)
            {
                ImGuiTabBar tab_bar = g.TabBars.TryGetMapData(n);
                if (tab_bar != null)
                {
                    PushID(tab_bar);
                    DebugNodeTabBar(tab_bar, "TabBar");
                    PopID();
                }
            }
            TreePop();
        }

        // Details for Tables
        if (TreeNode("Tables", "Tables (%d)", g.Tables.GetAliveCount()))
        {
            for (int n = 0; n < g.Tables.GetMapSize(); n++)
            {
                ImGuiTable table = g.Tables.TryGetMapData(n);
                if (table != null)
                    DebugNodeTable(table);
            }
            TreePop();
        }

        // Details for InputText
        if (TreeNode("InputText"))
        {
            DebugNodeInputTextState(g.InputTextState);
            TreePop();
        }

        // Details for TypingSelect
        if (TreeNode("TypingSelect", "TypingSelect (%d)", g.TypingSelectState.SearchBuffer[0] != 0 ? 1 : 0))
        {
            DebugNodeTypingSelectState(g.TypingSelectState);
            TreePop();
        }

        // Details for MultiSelect
        if (TreeNode("MultiSelect", "MultiSelect (%d)", g.MultiSelectStorage.GetAliveCount()))
        {
            ImGuiBoxSelectState bs = g.BoxSelectState;
            BulletText("BoxSelect ID=0x%08X, Starting = %d, Active %d", bs.ID, bs.IsStarting ? 1 : 0, bs.IsActive ? 1 : 0);
            for (int n = 0; n < g.MultiSelectStorage.GetMapSize(); n++)
            {
                ImGuiMultiSelectState state = g.MultiSelectStorage.TryGetMapData(n);
                if (state != null)
                    DebugNodeMultiSelectState(state);
            }
            TreePop();
        }

        // Settings
        if (TreeNode("Settings"))
        {
            if (SmallButton("Clear"))
                ClearIniSettings();
            SameLine();
            if (SmallButton("Save to memory"))
                SaveIniSettingsToMemory(out _);
            SameLine();
            if (SmallButton("Save to disk"))
                SaveIniSettingsToDisk(g.IO.IniFilename);
            SameLine();
            if (g.IO.IniFilename != null)
                Text("\"%s\"", g.IO.IniFilename);
            else
                TextUnformatted("<NULL>");
            Text("SettingsDirtyTimer %.2f", g.SettingsDirtyTimer);

            int highlight_older_than_date = 0;
            Text("SessionDate: %d", platform_io.Platform_SessionDate);
            BeginDisabled(platform_io.Platform_SessionDate == 0);
            Checkbox("Highlight Entries Older Than", ref cfg.SettingsHighlightOldEntries);
            SetNextItemWidth(GetFontSize() * 8);
            SameLine();
            SliderInt("Months", ref cfg.SettingsDiscardMonths, 1, 24);
            if (cfg.SettingsHighlightOldEntries && cfg.SettingsDiscardMonths > 0)
            {
                ImGuiPackedDate cutoff_date = ImGuiPackedDate.FromYYYYMMDD(platform_io.Platform_SessionDate);
                cutoff_date.SubtractMonths(cfg.SettingsDiscardMonths);
                highlight_older_than_date = cutoff_date.Unpack();
                SameLine();
                var cleanup_args = new ImGuiSettingsCleanupArgs();
                cleanup_args.DiscardOlderThanMonths = cfg.SettingsDiscardMonths;
                if (Button("Discard"))
                    CleanupIniSettings(ref cleanup_args);
            }
            EndDisabled();
            Checkbox("io.ConfigDebugIniSettings", ref io.ConfigDebugIniSettings);

            if (TreeNode("SettingsHandlers", "Settings handlers: (%d)", g.SettingsHandlers.Size))
            {
                for (int h_i = 0; h_i < g.SettingsHandlers.Size; h_i++)
                    BulletText("\"%s\"", g.SettingsHandlers[h_i].TypeName);
                TreePop();
            }
            if (TreeNode("SettingsWindows", "Settings packed data: Windows: %d bytes", g.SettingsWindows.size()))
            {
                for (int i = 0; i < g.SettingsWindows.size(); i++)
                {
                    ImGuiWindowSettings settings = g.SettingsWindows[i];
                    bool highlight = highlight_older_than_date != 0 && settings.LastUsedDate.Unpack() < highlight_older_than_date;
                    if (highlight) PushStyleColor(ImGuiCol_Text, new ImVec4(1.0f, 0.4f, 0.4f, 1.0f));
                    DebugNodeWindowSettings(settings);
                    if (highlight) PopStyleColor();
                }
                TreePop();
            }

            if (TreeNode("SettingsTables", "Settings packed data: Tables: %d bytes", g.SettingsTables.size()))
            {
                for (int i = 0; i < g.SettingsTables.size(); i++)
                {
                    ImGuiTableSettings settings = g.SettingsTables[i];
                    bool highlight = highlight_older_than_date != 0 && settings.LastUsedDate.Unpack() < highlight_older_than_date;
                    if (highlight) PushStyleColor(ImGuiCol_Text, new ImVec4(1.0f, 0.4f, 0.4f, 1.0f));
                    DebugNodeTableSettings(settings, null);
                    if (highlight) PopStyleColor();
                }
                TreePop();
            }

            if (TreeNode("SettingsIniData", "Settings unpacked data (.ini): %d bytes", g.SettingsIniData.size()))
            {
                InputTextMultiline("##Ini", g.SettingsIniData.Buf.Data, g.SettingsIniData.Buf.Size, new ImVec2(-float.MinValue, GetTextLineHeight() * 20), ImGuiInputTextFlags_ReadOnly);
                TreePop();
            }
            TreePop();
        }

        // Memory allocations
        if (TreeNode("Memory allocations"))
        {
            ImGuiDebugAllocInfo info = g.DebugAllocInfo;
            Text("%d current allocations", info.TotalAllocCount - info.TotalFreeCount);
            Text("Releasing selected unused buffers after: %.2f secs", g.IO.ConfigMemoryCompactTimer);
            if (SmallButton("GC now")) { g.GcCompactAll = true; }
            Text("Recent frames with allocations:");
            int buf_size = info.LastEntriesBuf.Length;
            for (int n = buf_size - 1; n >= 0; n--)
            {
                ImGuiDebugAllocEntry entry = info.LastEntriesBuf[(info.LastEntriesIdx - n + buf_size) % buf_size];
                BulletText("Frame %06d: %+3d ( %2d alloc, %2d free )", entry.FrameCount, entry.AllocCount - entry.FreeCount, entry.AllocCount, entry.FreeCount);
                if (n == 0)
                {
                    SameLine();
                    Text("<- %d frames ago", g.FrameCount - entry.FrameCount);
                }
            }
            TreePop();
        }

        if (TreeNode("Inputs"))
        {
            Text("KEYBOARD/GAMEPAD/MOUSE KEYS");
            {
                // User code should never have to go through such hoops! You can generally iterate between ImGuiKey_NamedKey_BEGIN and ImGuiKey_NamedKey_END.
                Indent();
                Text("Keys down:"); for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++) { if (!IsKeyDown(key)) continue; SameLine(); Text(IsNamedKey(key) ? "\"%s\"" : "\"%s\" %d", GetKeyName(key), key); SameLine(); Text("(%.02f)", GetKeyData(key).DownDuration); }
                Text("Keys pressed:"); for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++) { if (!IsKeyPressed(key)) continue; SameLine(); Text(IsNamedKey(key) ? "\"%s\"" : "\"%s\" %d", GetKeyName(key), key); }
                Text("Keys released:"); for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++) { if (!IsKeyReleased(key)) continue; SameLine(); Text(IsNamedKey(key) ? "\"%s\"" : "\"%s\" %d", GetKeyName(key), key); }
                Text("Keys mods: %s%s%s%s", io.KeyCtrl ? "Ctrl " : "", io.KeyShift ? "Shift " : "", io.KeyAlt ? "Alt " : "", io.KeySuper ? "Super " : "");
                Text("Chars queue:"); for (int i = 0; i < io.InputQueueCharacters.Size; i++) { ushort c = io.InputQueueCharacters[i]; SameLine(); Text("\'%c\' (0x%04X)", (c > ' ' && c <= 255) ? (char)c : '?', c); } // FIXME: We should convert 'c' to UTF-8 here but the functions are not public.
                DebugRenderKeyboardPreview(GetWindowDrawList());
                Unindent();
            }

            Text("MOUSE STATE");
            {
                Indent();
                if (IsMousePosValid())
                    Text("Mouse pos: (%g, %g)", io.MousePos.x, io.MousePos.y);
                else
                    Text("Mouse pos: <INVALID>");
                Text("Mouse delta: (%g, %g)", io.MouseDelta.x, io.MouseDelta.y);
                int count = io.MouseDown.Length;
                Text("Mouse down:"); for (int i = 0; i < count; i++) if (IsMouseDown(i)) { SameLine(); Text("b%d (%.02f secs)", i, io.MouseDownDuration[i]); }
                Text("Mouse clicked:"); for (int i = 0; i < count; i++) if (IsMouseClicked(i)) { SameLine(); Text("b%d (%d)", i, io.MouseClickedCount[i]); }
                Text("Mouse released:"); for (int i = 0; i < count; i++) if (IsMouseReleased(i)) { SameLine(); Text("b%d", i); }
                Text("Mouse wheel: %.1f", io.MouseWheel);
                Text("MouseStationaryTimer: %.2f", g.MouseStationaryTimer);
                Text("Mouse source: %s", DebugGetMouseSourceName(io.MouseSource));
                Text("Pen Pressure: %.1f", io.PenPressure); // Note: currently unused
                Unindent();
            }

            Text("MOUSE WHEELING");
            {
                Indent();
                Text("WheelingWindow: '%s'", g.WheelingWindow != null ? g.WheelingWindow.Name : "NULL");
                Text("WheelingWindowReleaseTimer: %.2f", g.WheelingWindowReleaseTimer);
                Text("WheelingAxisAvg[] = { %.3f, %.3f }, Main Axis: %s", g.WheelingAxisAvg.x, g.WheelingAxisAvg.y, (g.WheelingAxisAvg.x > g.WheelingAxisAvg.y) ? "X" : (g.WheelingAxisAvg.x < g.WheelingAxisAvg.y) ? "Y" : "<none>");
                Unindent();
            }

            Text("KEY OWNERS");
            {
                Indent();
                if (BeginChild("##owners", new ImVec2(-float.MinValue, GetTextLineHeightWithSpacing() * 8), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY, ImGuiWindowFlags_NoSavedSettings))
                    for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++)
                    {
                        ref ImGuiKeyOwnerData owner_data = ref GetKeyOwnerData(g, key);
                        if (owner_data.OwnerCurr == ImGuiKeyOwner_NoOwner)
                            continue;
                        Text("%s: 0x%08X%s", GetKeyName(key), owner_data.OwnerCurr,
                            owner_data.LockUntilRelease ? " LockUntilRelease" : owner_data.LockThisFrame ? " LockThisFrame" : "");
                        DebugLocateItemOnHover(owner_data.OwnerCurr);
                    }
                EndChild();
                Unindent();
            }
            Text("SHORTCUT ROUTING");
            SameLine();
            MetricsHelpMarker("Declared shortcut routes automatically set key owner when mods matches.");
            {
                Indent();
                if (BeginChild("##routes", new ImVec2(-float.MinValue, GetTextLineHeightWithSpacing() * 8), ImGuiChildFlags_FrameStyle | ImGuiChildFlags_ResizeY, ImGuiWindowFlags_NoSavedSettings))
                    for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++)
                    {
                        ImGuiKeyRoutingTable rt = g.KeysRoutingTable;
                        for (short idx = rt.Index[key - ImGuiKey_NamedKey_BEGIN]; idx != -1; )
                        {
                            ImGuiKeyRoutingData routing_data = rt.Entries[idx];
                            ImGuiKeyChord key_chord = key | routing_data.Mods;
                            Text("%s: 0x%08X (scored %d)", GetKeyChordName(key_chord), routing_data.RoutingCurr, routing_data.RoutingCurrScore);
                            DebugLocateItemOnHover(routing_data.RoutingCurr);
                            if (g.IO.ConfigDebugIsDebuggerPresent)
                            {
                                SameLine();
                                if (DebugBreakButton("**DebugBreak**", "in SetShortcutRouting() for this KeyChord"))
                                    g.DebugBreakInShortcutRouting = key_chord;
                            }
                            idx = routing_data.NextEntryIndex;
                        }
                    }
                EndChild();
                Text("(ActiveIdUsing: AllKeyboardKeys: %d, NavDirMask: 0x%X)", g.ActiveIdUsingAllKeyboardKeys ? 1 : 0, g.ActiveIdUsingNavDirMask);
                Unindent();
            }
            TreePop();
        }

        if (TreeNode("Internal state"))
        {
            Text("WINDOWING");
            Indent();
            Text("HoveredWindow: '%s'", g.HoveredWindow != null ? g.HoveredWindow.Name : "NULL");
            Text("HoveredWindow->Root: '%s'", g.HoveredWindow != null ? g.HoveredWindow.RootWindow.Name : "NULL");
            Text("HoveredWindowUnderMovingWindow: '%s'", g.HoveredWindowUnderMovingWindow != null ? g.HoveredWindowUnderMovingWindow.Name : "NULL");
            Text("MovingWindow: '%s'", g.MovingWindow != null ? g.MovingWindow.Name : "NULL");
            Unindent();

            Text("ITEMS");
            Indent();
            Text("ActiveId: 0x%08X/0x%08X (%.2f sec), AllowOverlap: %d, Source: %s", g.ActiveId, g.ActiveIdPreviousFrame, g.ActiveIdTimer, g.ActiveIdAllowOverlap ? 1 : 0, DebugGetInputSourceName(g.ActiveIdSource));
            DebugLocateItemOnHover(g.ActiveId);
            Text("ActiveIdWindow: '%s'", g.ActiveIdWindow != null ? g.ActiveIdWindow.Name : "NULL");
            Text("ActiveIdUsing: AllKeyboardKeys: %d, NavDirMask: %X", g.ActiveIdUsingAllKeyboardKeys ? 1 : 0, g.ActiveIdUsingNavDirMask);
            Text("HoveredId: 0x%08X (%.2f sec), AllowOverlap: %d", g.HoveredIdPreviousFrame, g.HoveredIdTimer, g.HoveredIdAllowOverlap ? 1 : 0); // Not displaying g.HoveredId as it is update mid-frame
            Text("HoverItemDelayId: 0x%08X, Timer: %.2f, ClearTimer: %.2f", g.HoverItemDelayId, g.HoverItemDelayTimer, g.HoverItemDelayClearTimer);
            Text("DragDrop: %d, SourceId = 0x%08X, Payload \"%s\" (%d bytes)", g.DragDropActive ? 1 : 0, g.DragDropPayload.SourceId, g.DragDropPayload.DataType, g.DragDropPayload.DataSize);
            DebugLocateItemOnHover(g.DragDropPayload.SourceId);
            Unindent();

            Text("NAV,FOCUS");
            Indent();
            Text("NavWindow: '%s'", g.NavWindow != null ? g.NavWindow.Name : "NULL");
            Text("NavId: 0x%08X, NavLayer: %d", g.NavId, g.NavLayer);
            DebugLocateItemOnHover(g.NavId);
            Text("NavInputSource: %s", DebugGetInputSourceName(g.NavInputSource));
            Text("NavLastValidSelectionUserData = %d (0x%X)", g.NavLastValidSelectionUserData, g.NavLastValidSelectionUserData);
            Text("NavActive: %d, NavVisible: %d", g.IO.NavActive ? 1 : 0, g.IO.NavVisible ? 1 : 0);
            Text("NavActivateId/DownId/PressedId: %08X/%08X/%08X", g.NavActivateId, g.NavActivateDownId, g.NavActivatePressedId);
            Text("NavActivateFlags: %04X", g.NavActivateFlags);
            Text("NavCursorVisible: %d, NavHighlightItemUnderNav: %d", g.NavCursorVisible ? 1 : 0, g.NavHighlightItemUnderNav ? 1 : 0);
            Text("NavFocusScopeId = 0x%08X", g.NavFocusScopeId);
            Text("NavFocusRoute[] = ");
            for (int path_n = g.NavFocusRoute.Size - 1; path_n >= 0; path_n--)
            {
                ImGuiFocusScopeData focus_scope = g.NavFocusRoute[path_n];
                SameLine(0.0f, 0.0f);
                Text("0x%08X/", focus_scope.ID);
                SetItemTooltip("In window \"%s\"", FindWindowByID(focus_scope.WindowID).Name);
            }
            Text("NavWindowingTarget: '%s'", g.NavWindowingTarget != null ? g.NavWindowingTarget.Name : "NULL");
            Unindent();

            TreePop();
        }

        // Overlay: Display windows Rectangles and Begin Order
        if (cfg.ShowWindowsRects || cfg.ShowWindowsBeginOrder)
        {
            for (int win_i = 0; win_i < g.Windows.Size; win_i++)
            {
                ImGuiWindow window = g.Windows[win_i];
                if (!window.WasActive)
                    continue;
                ImDrawList draw_list = GetForegroundDrawList(window);
                if (cfg.ShowWindowsRects)
                {
                    ImRect r = Metrics_GetWindowRect(window, cfg.ShowWindowsRectsType);
                    draw_list.AddRect(r.Min, r.Max, ImGuiCore.IM_COL32(255, 0, 128, 255));
                }
                if (cfg.ShowWindowsBeginOrder && (window.Flags & ImGuiWindowFlags_ChildWindow) == 0)
                {
                    string buf = string.Format("{0}", window.BeginOrderWithinContext);
                    float font_size = GetFontSize();
                    draw_list.AddRectFilled(window.Pos, window.Pos + new ImVec2(font_size, font_size), ImGuiCore.IM_COL32(200, 100, 100, 255));
                    draw_list.AddText(window.Pos, ImGuiCore.IM_COL32(255, 255, 255, 255), buf);
                }
            }
        }

        // Overlay: Display Tables Rectangles
        if (cfg.ShowTablesRects)
        {
            for (int table_n = 0; table_n < g.Tables.GetMapSize(); table_n++)
            {
                ImGuiTable table = g.Tables.TryGetMapData(table_n);
                if (table == null || table.LastFrameActive < g.FrameCount - 1)
                    continue;
                ImDrawList draw_list = GetForegroundDrawList(table.OuterWindow);
                if (cfg.ShowTablesRectsType >= TRT_ColumnsRect)
                {
                    for (int column_n = 0; column_n < table.ColumnsCount; column_n++)
                    {
                        ImRect r = Metrics_GetTableRect(table, cfg.ShowTablesRectsType, column_n);
                        ImU32 col = (table.HoveredColumnBody == column_n) ? ImGuiCore.IM_COL32(255, 255, 128, 255) : ImGuiCore.IM_COL32(255, 0, 128, 255);
                        float thickness = (table.HoveredColumnBody == column_n) ? 3.0f : 1.0f;
                        draw_list.AddRect(r.Min, r.Max, col, 0.0f, thickness);
                    }
                }
                else
                {
                    ImRect r = Metrics_GetTableRect(table, cfg.ShowTablesRectsType, -1);
                    draw_list.AddRect(r.Min, r.Max, ImGuiCore.IM_COL32(255, 0, 128, 255));
                }
            }
        }

        End();
    }

    // imgui_draw.cpp: void ImGui::RenderRectFilledWithHole(...)   [imgui_draw.cpp 6183]
    public static void RenderRectFilledWithHole(ImDrawList draw_list, ImRect outer, ImRect inner, ImU32 col, float rounding)
    {
        bool fill_L = (inner.Min.x > outer.Min.x);
        bool fill_R = (inner.Max.x < outer.Max.x);
        bool fill_U = (inner.Min.y > outer.Min.y);
        bool fill_D = (inner.Max.y < outer.Max.y);
        if (fill_L) draw_list.AddRectFilled(new ImVec2(outer.Min.x, inner.Min.y), new ImVec2(inner.Min.x, inner.Max.y), col, rounding, ImDrawFlags_RoundCornersNone | (fill_U ? 0 : ImDrawFlags_RoundCornersTopLeft)    | (fill_D ? 0 : ImDrawFlags_RoundCornersBottomLeft));
        if (fill_R) draw_list.AddRectFilled(new ImVec2(inner.Max.x, inner.Min.y), new ImVec2(outer.Max.x, inner.Max.y), col, rounding, ImDrawFlags_RoundCornersNone | (fill_U ? 0 : ImDrawFlags_RoundCornersTopRight)   | (fill_D ? 0 : ImDrawFlags_RoundCornersBottomRight));
        if (fill_U) draw_list.AddRectFilled(new ImVec2(inner.Min.x, outer.Min.y), new ImVec2(inner.Max.x, inner.Min.y), col, rounding, ImDrawFlags_RoundCornersNone | (fill_L ? 0 : ImDrawFlags_RoundCornersTopLeft)    | (fill_R ? 0 : ImDrawFlags_RoundCornersTopRight));
        if (fill_D) draw_list.AddRectFilled(new ImVec2(inner.Min.x, inner.Max.y), new ImVec2(inner.Max.x, outer.Max.y), col, rounding, ImDrawFlags_RoundCornersNone | (fill_L ? 0 : ImDrawFlags_RoundCornersBottomLeft) | (fill_R ? 0 : ImDrawFlags_RoundCornersBottomRight));
        if (fill_L && fill_U) draw_list.AddRectFilled(new ImVec2(outer.Min.x, outer.Min.y), new ImVec2(inner.Min.x, inner.Min.y), col, rounding, ImDrawFlags_RoundCornersTopLeft);
        if (fill_R && fill_U) draw_list.AddRectFilled(new ImVec2(inner.Max.x, outer.Min.y), new ImVec2(outer.Max.x, inner.Min.y), col, rounding, ImDrawFlags_RoundCornersTopRight);
        if (fill_L && fill_D) draw_list.AddRectFilled(new ImVec2(outer.Min.x, inner.Max.y), new ImVec2(inner.Min.x, outer.Max.y), col, rounding, ImDrawFlags_RoundCornersBottomLeft);
        if (fill_R && fill_D) draw_list.AddRectFilled(new ImVec2(inner.Max.x, inner.Max.y), new ImVec2(outer.Max.x, outer.Max.y), col, rounding, ImDrawFlags_RoundCornersBottomRight);
    }
}
