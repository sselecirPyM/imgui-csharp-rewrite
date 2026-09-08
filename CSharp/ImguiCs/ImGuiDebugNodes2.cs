// Port of imgui.cpp [SECTION] DEBUG TOOLS: DebugNodeXXX() functions (part 2 of the
// Metrics/Debugger tree: columns, draw lists, fonts, storage, tab bars, windows...).
using System;
using System.Text;

namespace ImGui;

public static partial class ImGui
{
    // C++: float ImTriangleArea(const ImVec2& a, const ImVec2& b, const ImVec2& c)  (imgui.cpp, internal)
    static float ImTriangleArea(ImVec2 a, ImVec2 b, ImVec2 c)
        => ImMath.ImFabs((a.x * (b.y - c.y)) + (b.x * (c.y - a.y)) + (c.x * (a.y - b.y))) * 0.5f;

    // Internal helpers used by the Metrics window (C++: imgui.cpp internal functions with tables in .rodata)
    internal static string DebugGetInputSourceName(int source)
    {
        string[] names = { "None", "Mouse", "Keyboard", "Gamepad", "Clipboard" };
        return (uint)source < names.Length ? names[source] : "N/A";
    }

    internal static string DebugGetMouseSourceName(int source)
    {
        string[] names = { "Mouse", "TouchScreen", "Pen" };
        return (uint)source < names.Length ? names[source] : "N/A";
    }

    // C++: void ImGui::DebugNodeColumns(ImGuiOldColumns* columns)
    public static void DebugNodeColumns(ImGuiOldColumns columns)
    {
        if (!TreeNode((object)columns.ID, "Columns Id: 0x%08X, Count: %d, Flags: 0x%04X", columns.ID, columns.Count, columns.Flags))
            return;
        BulletText("Width: %.1f (MinX: %.1f, MaxX: %.1f)", columns.OffMaxX - columns.OffMinX, columns.OffMinX, columns.OffMaxX);
        for (int col_i = 0; col_i < columns.Columns.Size; col_i++)
        {
            ImGuiOldColumnData column = columns.Columns[col_i];
            BulletText("Column %02d: OffsetNorm %.3f (= %.1f px)", col_i, column.OffsetNorm, GetColumnOffsetFromNorm(columns, column.OffsetNorm));
        }
        TreePop();
    }

    // [DEBUG] Display contents of ImDrawList
    // C++: void ImGui::DebugNodeDrawList(ImGuiWindow* window, ImGuiViewportP* viewport, const ImDrawList* draw_list, const char* label)
    public static void DebugNodeDrawList(ImGuiWindow window, ImGuiViewportP viewport, ImDrawList draw_list, string label)
    {
        ImGuiContext g = GImGui;
        ImGuiMetricsConfig cfg = g.DebugMetricsConfig;
        int cmd_count = draw_list.CmdBuffer.Size;
        if (cmd_count > 0 && draw_list.CmdBuffer.Data[cmd_count - 1].ElemCount == 0 && draw_list.CmdBuffer.Data[cmd_count - 1].UserCallback == null)
            cmd_count--;
        bool node_open = TreeNode((object)draw_list, "%s: '%s' %d vtx, %d indices, %d cmds", label, draw_list._OwnerName ?? "", draw_list.VtxBuffer.Size, draw_list.IdxBuffer.Size, cmd_count);
        if (window != null && draw_list == window.DrawList)
        {
            SameLine();
            TextColored(new ImVec4(1.0f, 0.4f, 0.4f, 1.0f), "CURRENTLY APPENDING"); // Can't display stats for active draw list! (we don't have the data double-buffered)
            if (node_open)
                TreePop();
            return;
        }

        ImDrawList fg_draw_list = GetForegroundDrawList(window); // Render additional visuals into the top-most draw list
        if (window != null && IsItemHovered() && fg_draw_list != null)
            fg_draw_list.AddRect(window.Pos, window.Pos + window.Size, ImGuiCore.IM_COL32(255, 255, 0, 255));
        if (!node_open)
            return;

        if (window != null && !window.WasActive)
            TextDisabled("Warning: owning Window is inactive. This DrawList is not being rendered!");

        for (int pcmd_i = 0; pcmd_i < cmd_count; pcmd_i++)
        {
            ImDrawCmd pcmd = draw_list.CmdBuffer.Data[pcmd_i];
            if (pcmd.UserCallback != null)
            {
                BulletText("Callback %p, user_data %p", pcmd.UserCallback, pcmd.UserCallbackDataBuf);
                continue;
            }

            string texid_desc = FormatTextureRefForDebugDisplay(pcmd.TexRef);
            string buf = string.Format("DrawCmd:{0,5} tris, Tex {1}, ClipRect ({2,4:F0},{3,4:F0})-({4,4:F0},{5,4:F0})",
                pcmd.ElemCount / 3, texid_desc, pcmd.ClipRect.x, pcmd.ClipRect.y, pcmd.ClipRect.z, pcmd.ClipRect.w);
            bool pcmd_node_open = TreeNode((object)pcmd_i, "%s", buf);
            if (IsItemHovered() && (cfg.ShowDrawCmdMesh || cfg.ShowDrawCmdBoundingBoxes) && fg_draw_list != null)
                DebugNodeDrawCmdShowMeshAndBoundingBox(fg_draw_list, draw_list, pcmd, cfg.ShowDrawCmdMesh, cfg.ShowDrawCmdBoundingBoxes);
            if (!pcmd_node_open)
                continue;

            // Calculate approximate coverage area (touched pixel count)
            // This will be in pixels squared as long there's no post-scaling happening to the renderer output.
            bool has_idx = draw_list.IdxBuffer.Size > 0;
            int vtx_base = (int)pcmd.VtxOffset;
            float total_area = 0.0f;
            for (uint idx_n = pcmd.IdxOffset; idx_n < pcmd.IdxOffset + pcmd.ElemCount; )
            {
                ImVec2[] triangle = new ImVec2[3];
                for (int n = 0; n < 3; n++, idx_n++)
                {
                    int vtx_idx = has_idx ? draw_list.IdxBuffer.Data[(int)idx_n] : (int)idx_n;
                    triangle[n] = draw_list.VtxBuffer.Data[vtx_base + vtx_idx].pos;
                }
                total_area += ImTriangleArea(triangle[0], triangle[1], triangle[2]);
            }

            // Display vertex information summary. Hover to get all triangles drawn in wire-frame
            buf = string.Format("Mesh: ElemCount: {0}, VtxOffset: +{1}, IdxOffset: +{2}, Area: ~{3:F0} px", pcmd.ElemCount, pcmd.VtxOffset, pcmd.IdxOffset, total_area);
            Selectable(buf);
            if (IsItemHovered() && fg_draw_list != null)
                DebugNodeDrawCmdShowMeshAndBoundingBox(fg_draw_list, draw_list, pcmd, true, false);

            // Display individual triangles/vertices. Hover on to get the corresponding triangle highlighted.
            var clipper = new ImGuiListClipper();
            clipper.Begin((int)pcmd.ElemCount / 3); // Manually coarse clip our print out of individual vertices to save CPU, only items that may be visible.
            while (clipper.Step())
                for (int prim = clipper.DisplayStart, idx_i = (int)pcmd.IdxOffset + clipper.DisplayStart * 3; prim < clipper.DisplayEnd; prim++)
                {
                    StringBuilder sb = new StringBuilder();
                    ImVec2[] triangle = new ImVec2[3];
                    for (int n = 0; n < 3; n++, idx_i++)
                    {
                        int vtx_idx = has_idx ? draw_list.IdxBuffer.Data[idx_i] : idx_i;
                        ImDrawVert v = draw_list.VtxBuffer.Data[vtx_base + vtx_idx];
                        triangle[n] = v.pos;
                        sb.AppendFormat("{0} {1,4:D4}: pos ({2,8:F2},{3,8:F2}), uv ({4:F6},{5:F6}), col {6:X8}\n",
                            (n == 0) ? "Vert:" : "     ", idx_i, v.pos.x, v.pos.y, v.uv.x, v.uv.y, v.col);
                    }

                    Selectable(sb.ToString(), false);
                    if (fg_draw_list != null && IsItemHovered())
                    {
                        ImDrawListFlags backup_flags = fg_draw_list.Flags;
                        fg_draw_list.Flags &= ~ImDrawListFlags_AntiAliasedLines; // Disable AA on triangle outlines is more readable for very large and thin triangles.
                        fg_draw_list.AddPolyline(triangle, 3, ImGuiCore.IM_COL32(255, 255, 0, 255), 1.0f, ImDrawFlags_Closed);
                        fg_draw_list.Flags = backup_flags;
                    }
                }
            TreePop();
        }
        TreePop();
    }

    // [DEBUG] Display mesh/aabb of a ImDrawCmd
    // C++: void ImGui::DebugNodeDrawCmdShowMeshAndBoundingBox(...)
    public static void DebugNodeDrawCmdShowMeshAndBoundingBox(ImDrawList out_draw_list, ImDrawList draw_list, ImDrawCmd draw_cmd, bool show_mesh, bool show_aabb)
    {
        System.Diagnostics.Debug.Assert(show_mesh || show_aabb);

        // Draw wire-frame version of all triangles
        ImRect clip_rect = new ImRect(draw_cmd.ClipRect.x, draw_cmd.ClipRect.y, draw_cmd.ClipRect.z, draw_cmd.ClipRect.w);
        ImRect vtxs_rect = new ImRect(float.MaxValue, float.MaxValue, -float.MaxValue, -float.MaxValue);
        ImDrawListFlags backup_flags = out_draw_list.Flags;
        out_draw_list.Flags &= ~ImDrawListFlags_AntiAliasedLines; // Disable AA on triangle outlines is more readable for very large and thin triangles.
        bool has_idx = draw_list.IdxBuffer.Size > 0;
        int vtx_base = (int)draw_cmd.VtxOffset;
        for (uint idx_n = draw_cmd.IdxOffset, idx_end = draw_cmd.IdxOffset + draw_cmd.ElemCount; idx_n < idx_end; )
        {
            ImVec2[] triangle = new ImVec2[3];
            for (int n = 0; n < 3; n++, idx_n++)
            {
                int vtx_idx = has_idx ? draw_list.IdxBuffer.Data[(int)idx_n] : (int)idx_n;
                vtxs_rect.Add(triangle[n] = draw_list.VtxBuffer.Data[vtx_base + vtx_idx].pos);
            }
            if (show_mesh)
                out_draw_list.AddPolyline(triangle, 3, ImGuiCore.IM_COL32(255, 255, 0, 255), 1.0f, ImDrawFlags_Closed); // In yellow: mesh triangles
        }
        // Draw bounding boxes
        if (show_aabb)
        {
            out_draw_list.AddRect(ImMath.ImTrunc(clip_rect.Min), ImMath.ImTrunc(clip_rect.Max), ImGuiCore.IM_COL32(255, 0, 255, 255)); // In pink: clipping rectangle submitted to GPU
            out_draw_list.AddRect(ImMath.ImTrunc(vtxs_rect.Min), ImMath.ImTrunc(vtxs_rect.Max), ImGuiCore.IM_COL32(0, 255, 255, 255)); // In cyan: bounding box of triangles
        }
        out_draw_list.Flags = backup_flags;
    }

    // [DEBUG] Compute mask of inputs with the same codepoint.
    // C++: static int CalcFontGlyphSrcOverlapMask(ImFontAtlas* atlas, ImFont* font, unsigned int codepoint)
    static int CalcFontGlyphSrcOverlapMask(ImFontAtlas atlas, ImFont font, uint codepoint)
    {
        int mask = 0, count = 0;
        for (int src_n = 0; src_n < font.Sources.Size; src_n++)
        {
            ImFontConfig src = font.Sources[src_n];
            var loader = src.FontLoader ?? atlas.FontLoader;
            if (loader == null || !loader.FontSrcContainsGlyph(atlas, src, (ushort)codepoint))
                continue;
            mask |= (1 << src_n);
            count++;
        }
        return count > 1 ? mask : 0;
    }

    // [DEBUG] Display details for a single font, called by ShowStyleEditor().
    // C++: void ImGui::DebugNodeFont(ImFont* font)
    public static void DebugNodeFont(ImFont font)
    {
        ImGuiContext g = GImGui;
        ImGuiMetricsConfig cfg = g.DebugMetricsConfig;
        ImFontAtlas atlas = font.OwnerAtlas;
        bool opened = TreeNode((object)font, "Font: \"%s\": %d sources(s)", font.GetDebugName(), font.Sources.Size);

        // Display preview text
        if (!opened)
            Indent();
        Indent();
        if (cfg.ShowFontPreview)
        {
            PushFont(font, 0.0f);
            Text("The quick brown fox jumps over the lazy dog");
            PopFont();
        }
        if (!opened)
        {
            Unindent();
            Unindent();
            return;
        }
        if (SmallButton("Set as default"))
            GetIO().FontDefault = font;
        SameLine();
        BeginDisabled(atlas.Fonts.Size <= 1 || atlas.Locked);
        if (SmallButton("Remove"))
            atlas.RemoveFont(font);
        EndDisabled();
        SameLine();
        if (SmallButton("Clear bakes"))
            ImFontAtlas.ImFontAtlasFontDiscardBakes(atlas, font, 0);
        SameLine();
        if (SmallButton("Clear unused"))
            ImFontAtlas.ImFontAtlasFontDiscardBakes(atlas, font, 2);

        // Display details
        SetNextItemWidth(GetFontSize() * 8);
        DragFloat("Font scale", ref font.LegacySize, 0.005f, 0.3f, 2.0f, "%.1f");

        Text("Fallback character: '%s' (U+%04X)", CpToUtf8(font.FallbackChar), font.FallbackChar);
        Text("Ellipsis character: '%s' (U+%04X)", CpToUtf8(font.EllipsisChar), font.EllipsisChar);

        for (int src_n = 0; src_n < font.Sources.Size; src_n++)
        {
            ImFontConfig src = font.Sources[src_n];
            if (TreeNode((object)src, "Input %d: '%s' [%d], Oversample: %d,%d, PixelSnapH: %d, Offset: (%.1f,%.1f)",
                src_n, src.Name, src.FontNo, src.OversampleH, src.OversampleV, src.PixelSnapH ? 1 : 0, src.GlyphOffset.x, src.GlyphOffset.y))
            {
                var loader = src.FontLoader ?? atlas.FontLoader;
                Text("Loader: '%s'", loader != null && loader.Name != null ? loader.Name : "N/A");
                TreePop();
            }
        }
        if (font.Sources.Size > 1 && TreeNode("Input Glyphs Overlap Detection Tool"))
        {
            TextWrapped("- First Input that contains the glyph is used.\n" +
                "- Use ImFontConfig::GlyphExcludeRanges[] to specify ranges to ignore glyph in given Input.\n- Prefer using a small number of ranges as the list is scanned every time a new glyph is loaded,\n  - e.g. GlyphExcludeRanges[] = { ICON_MIN_FA, ICON_MAX_FA, 0 };\n- This tool doesn't cache results and is slow, don't keep it open!");
            if (BeginTable("table", 2))
            {
                for (uint c = 0; c < 0x10000; c++)
                {
                    int overlap_mask = CalcFontGlyphSrcOverlapMask(atlas, font, c);
                    if (overlap_mask == 0)
                        continue;
                    uint c_end = c + 1;
                    while (c_end < 0x10000 && CalcFontGlyphSrcOverlapMask(atlas, font, c_end) == overlap_mask)
                        c_end++;
                    bool col_open = TableNextColumn() && TreeNode((object)(int)c, "U+%04X-U+%04X: %d codepoints in %d inputs", c, c_end - 1, c_end - c, ImUtil.ImCountSetBits((uint)overlap_mask));
                    if (col_open)
                    {
                        for (uint n = c; n < c_end; n++)
                            BulletText("Codepoint U+%04X (%s)", n, CpToUtf8(n));
                        TreePop();
                    }
                    TableNextColumn();
                    for (int src_n = 0; src_n < font.Sources.Size; src_n++)
                    {
                        if ((overlap_mask & (1 << src_n)) != 0)
                        {
                            Text("%d ", src_n);
                            SameLine();
                        }
                    }
                    c = c_end - 1;
                }
                EndTable();
            }
            TreePop();
        }

        // Display all glyphs of the fonts in separate pages of 256 characters
        for (int baked_n = 0; baked_n < atlas.Builder.BakedPool.Size; baked_n++)
        {
            ImFontBaked baked = atlas.Builder.BakedPool[baked_n];
            if (baked.OwnerFont != font)
                continue;
            PushID((int)baked.BakedId);
            if (TreeNode("Glyphs", "Baked at { %.2fpx, d.%.2f }: %d glyphs%s", baked.Size, baked.RasterizerDensity, baked.Glyphs.Size, (baked.LastUsedFrame < atlas.Builder.FrameCount - 1) ? " *Unused*" : ""))
            {
                if (SmallButton("Load all"))
                    for (uint cp_base = 0; cp_base <= ImUtil.IM_UNICODE_CODEPOINT_MAX; cp_base++)
                        baked.FindGlyph((ushort)cp_base);

                int surface_sqrt = (int)ImMath.ImSqrt((float)baked.MetricsTotalSurface);
                Text("Ascent: %f, Descent: %f, Ascent-Descent: %f", baked.Ascent, baked.Descent, baked.Ascent - baked.Descent);
                Text("Texture Area: about %d px ~%dx%d px", baked.MetricsTotalSurface, surface_sqrt, surface_sqrt);
                for (int src_n = 0; src_n < font.Sources.Size; src_n++)
                {
                    ImFontConfig src = font.Sources[src_n];
                    ImFontAtlas.ImFontAtlasBuildGetOversampleFactors(src, baked, out int oversample_h, out int oversample_v);
                    BulletText("Input %d: '%s', Oversample: (%d=>%d,%d=>%d), PixelSnapH: %d, Offset: (%.1f,%.1f)",
                        src_n, src.Name, src.OversampleH, oversample_h, src.OversampleV, oversample_v, src.PixelSnapH ? 1 : 0, src.GlyphOffset.x, src.GlyphOffset.y);
                }

                DebugNodeFontGlyphsForSrcMask(font, baked, ~0);
                TreePop();
            }
            PopID();
        }
        TreePop();
        Unindent();
    }

    // C++: void ImGui::DebugNodeFontGlyphsForSrcMask(ImFont* font, ImFontBaked* baked, int src_mask)
    public static void DebugNodeFontGlyphsForSrcMask(ImFont font, ImFontBaked baked, int src_mask)
    {
        ImDrawList draw_list = GetWindowDrawList();
        ImU32 glyph_col = GetColorU32(ImGuiCol_Text);
        float cell_size = baked.Size * 1;
        float cell_spacing = GetStyle().ItemSpacing.y;
        for (uint cp_base = 0; cp_base <= ImUtil.IM_UNICODE_CODEPOINT_MAX; cp_base += 256)
        {
            // Skip ahead if a large bunch of glyphs are not present in the font (test in chunks of 4k)
            if ((cp_base & 8191) == 0 && font.IsGlyphRangeUnused(cp_base, cp_base + 8191))
            {
                cp_base += 8192 - 256;
                continue;
            }

            int count = 0;
            for (uint n = 0; n < 256; n++)
            {
                ushort cp = (ushort)(cp_base + n);
                if (baked.IsGlyphLoaded(cp))
                {
                    ImFontGlyph glyph = baked.FindGlyph(cp);
                    if ((src_mask & (1 << (int)glyph.SourceIdx)) != 0)
                        count++;
                }
            }
            if (count <= 0)
                continue;
            if (!TreeNode((object)(int)cp_base, "U+%04X..U+%04X (%d %s)", cp_base, cp_base + 255, count, count > 1 ? "glyphs" : "glyph"))
                continue;

            // Draw a 16x16 grid of glyphs
            ImVec2 base_pos = GetCursorScreenPos();
            for (uint n = 0; n < 256; n++)
            {
                // We use ImFont::RenderChar as a shortcut because we don't have UTF-8 conversion functions
                // available here and thus cannot easily generate a zero-terminated UTF-8 encoded string.
                ImVec2 cell_p1 = new ImVec2(base_pos.x + (n % 16) * (cell_size + cell_spacing), base_pos.y + (n / 16) * (cell_size + cell_spacing));
                ImVec2 cell_p2 = new ImVec2(cell_p1.x + cell_size, cell_p1.y + cell_size);
                ushort cp = (ushort)(cp_base + n);
                ImFontGlyph glyph = baked.IsGlyphLoaded(cp) ? baked.FindGlyph(cp) : null;
                draw_list.AddRect(cell_p1, cell_p2, glyph != null ? ImGuiCore.IM_COL32(255, 255, 255, 100) : ImGuiCore.IM_COL32(255, 255, 255, 50));
                if (glyph == null || (src_mask & (1 << (int)glyph.SourceIdx)) == 0)
                    continue;
                font.RenderChar(draw_list, cell_size, cell_p1, glyph_col, cp);
                if (IsMouseHoveringRect(cell_p1, cell_p2) && BeginTooltip())
                {
                    DebugNodeFontGlyph(font, glyph);
                    EndTooltip();
                }
            }
            Dummy(new ImVec2((cell_size + cell_spacing) * 16, (cell_size + cell_spacing) * 16));
            TreePop();
        }
    }

    // C++: void ImGui::DebugNodeFontGlyph(ImFont* font, const ImFontGlyph* glyph)
    public static void DebugNodeFontGlyph(ImFont font, ImFontGlyph glyph)
    {
        Text("Codepoint: U+%04X", glyph.Codepoint);
        Separator();
        Text("Visible: %d", glyph.Visible);
        Text("AdvanceX: %.1f", glyph.AdvanceX);
        Text("Pos: (%.2f,%.2f)->(%.2f,%.2f)", glyph.X0, glyph.Y0, glyph.X1, glyph.Y1);
        Text("UV: (%.3f,%.3f)->(%.3f,%.3f)", glyph.U0, glyph.V0, glyph.U1, glyph.V1);
        if (glyph.PackId >= 0)
        {
            var r = ImFontAtlas.ImFontAtlasPackGetRect(font.OwnerAtlas, glyph.PackId);
            if (r.HasValue)
                Text("PackId: 0x%X (%dx%d rect at %d,%d)", glyph.PackId, r.Value.w, r.Value.h, r.Value.x, r.Value.y);
        }
        Text("SourceIdx: %d", glyph.SourceIdx);
    }

    // [DEBUG] Display contents of ImGuiStorage
    // C++: void ImGui::DebugNodeStorage(ImGuiStorage* storage, const char* label)
    public static void DebugNodeStorage(ImGuiStorage storage, string label)
    {
        if (!TreeNode(label, "%s: %d entries, %d bytes", label, storage.Data.Size, storage.Data.Size * 4))
            return;
        for (int p_i = 0; p_i < storage.Data.Size; p_i++)
        {
            ImGuiStoragePair p = storage.Data[p_i];
            BulletText("Key 0x%08X Value { i: %d }", p.key, p.val_i); // Important: we currently don't store a type, real value may not be integer.
            DebugLocateItemOnHover(p.key);
        }
        TreePop();
    }

    // [DEBUG] Display contents of ImGuiTabBar
    // C++: void ImGui::DebugNodeTabBar(ImGuiTabBar* tab_bar, const char* label)
    public static void DebugNodeTabBar(ImGuiTabBar tab_bar, string label)
    {
        // Standalone tab bars (not associated to docking/windows functionality) currently hold no discernible strings.
        string buf;
        bool is_active = (tab_bar.PrevFrameVisible >= GetFrameCount() - 2);
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} 0x{1:X8} ({2} tabs){3}  {{", label, tab_bar.ID, tab_bar.Tabs.Size, is_active ? "" : " *Inactive*");
            for (int tab_n = 0; tab_n < Math.Min(tab_bar.Tabs.Size, 3); tab_n++)
            {
                ImGuiTabItem tab = tab_bar.Tabs[tab_n];
                sb.AppendFormat("{0}'{1}'", tab_n > 0 ? ", " : "", TabBarGetTabName(tab_bar, tab));
            }
            sb.Append((tab_bar.Tabs.Size > 3) ? " ... }" : " } ");
            buf = sb.ToString();
        }
        if (!is_active) { PushStyleColor(ImGuiCol_Text, GetStyleColorVec4(ImGuiCol_TextDisabled)); }
        bool open = TreeNode(label, "%s", buf);
        if (!is_active) { PopStyleColor(); }
        if (is_active && IsItemHovered())
        {
            ImDrawList draw_list = GetForegroundDrawList(tab_bar.Window);
            draw_list.AddRect(tab_bar.BarRect.Min, tab_bar.BarRect.Max, ImGuiCore.IM_COL32(255, 255, 0, 255));
            draw_list.AddLineV(tab_bar.ScrollingRectMinX, tab_bar.BarRect.Min.y, tab_bar.BarRect.Max.y, ImGuiCore.IM_COL32(0, 255, 0, 255));
            draw_list.AddLineV(tab_bar.ScrollingRectMaxX, tab_bar.BarRect.Min.y, tab_bar.BarRect.Max.y, ImGuiCore.IM_COL32(0, 255, 0, 255));
        }
        if (open)
        {
            for (int tab_n = 0; tab_n < tab_bar.Tabs.Size; tab_n++)
            {
                ImGuiTabItem tab = tab_bar.Tabs[tab_n];
                PushID(tab);
                if (SmallButton("<")) { TabBarQueueReorder(tab_bar, tab, -1); } SameLine(0, 2);
                if (SmallButton(">")) { TabBarQueueReorder(tab_bar, tab, +1); } SameLine();
                Text("%02d%c Tab 0x%08X '%s' Offset: %.2f, Width: %.2f/%.2f",
                    tab_n, (tab.ID == tab_bar.SelectedTabId) ? '*' : ' ', tab.ID, TabBarGetTabName(tab_bar, tab), tab.Offset, tab.Width, tab.ContentWidth);
                PopID();
            }
            TreePop();
        }
    }

    // C++: void ImGui::DebugNodeViewport(ImGuiViewportP* viewport)
    public static void DebugNodeViewport(ImGuiViewportP viewport)
    {
        ImGuiContext g = GImGui;
        SetNextItemOpen(true, ImGuiCond_Once);
        bool open = TreeNode("viewport0", "Viewport #%d", 0);
        if (IsItemHovered())
            g.DebugMetricsConfig.HighlightViewportID = viewport.ID;
        if (open)
        {
            int flags = viewport.Flags;
            BulletText("Main Pos: (%.0f,%.0f), Size: (%.0f,%.0f)\nWorkArea Inset Left: %.0f Top: %.0f, Right: %.0f, Bottom: %.0f",
                viewport.Pos.x, viewport.Pos.y, viewport.Size.x, viewport.Size.y,
                viewport.WorkInsetMin.x, viewport.WorkInsetMin.y, viewport.WorkInsetMax.x, viewport.WorkInsetMax.y);
            BulletText("Flags: 0x%04X =%s%s%s", flags,
                (flags & ImGuiViewportFlags_IsPlatformWindow) != 0 ? " IsPlatformWindow" : "",
                (flags & ImGuiViewportFlags_IsPlatformMonitor) != 0 ? " IsPlatformMonitor" : "",
                (flags & ImGuiViewportFlags_OwnedByApp) != 0 ? " OwnedByApp" : "");
            for (int dl_i = 0; dl_i < viewport.DrawDataP.CmdLists.Size; dl_i++)
                DebugNodeDrawList(null, viewport, viewport.DrawDataP.CmdLists[dl_i], "DrawList");
            TreePop();
        }
    }

    // C++: void ImGui::DebugNodeWindow(ImGuiWindow* window, const char* label)
    public static void DebugNodeWindow(ImGuiWindow window, string label)
    {
        if (window == null)
        {
            BulletText("%s: NULL", label);
            return;
        }

        ImGuiContext g = GImGui;
        bool is_active = window.WasActive;
        ImGuiTreeNodeFlags tree_node_flags = (window == g.NavWindow) ? ImGuiTreeNodeFlags_Selected : ImGuiTreeNodeFlags_None;
        if (!is_active) { PushStyleColor(ImGuiCol_Text, GetStyleColorVec4(ImGuiCol_TextDisabled)); }
        bool open = TreeNodeExV(label, tree_node_flags, "%s '%s'%s", new object[] { label, window.Name, is_active ? "" : " *Inactive*" });
        if (!is_active) { PopStyleColor(); }
        if (IsItemHovered() && is_active)
            GetForegroundDrawList(window).AddRect(window.Pos, window.Pos + window.Size, ImGuiCore.IM_COL32(255, 255, 0, 255));
        if (!open)
            return;

        if (window.MemoryCompacted)
            TextDisabled("Note: some memory buffers have been compacted/freed.");

        if (g.IO.ConfigDebugIsDebuggerPresent && DebugBreakButton("**DebugBreak**", "in Begin()"))
            g.DebugBreakInWindow = window.ID;

        int flags = window.Flags;
        DebugNodeDrawList(window, (ImGuiViewportP)window.Viewport, window.DrawList, "DrawList");
        BulletText("Pos: (%.1f,%.1f), Size: (%.1f,%.1f), ContentSize (%.1f,%.1f) Ideal (%.1f,%.1f)", window.Pos.x, window.Pos.y, window.Size.x, window.Size.y, window.ContentSize.x, window.ContentSize.y, window.ContentSizeIdeal.x, window.ContentSizeIdeal.y);
        BulletText("Flags: 0x%08X (%s%s%s%s%s%s%s%s%s..)", flags,
            (flags & ImGuiWindowFlags_ChildWindow) != 0 ? "Child " : "", (flags & ImGuiWindowFlags_Tooltip) != 0 ? "Tooltip " : "", (flags & ImGuiWindowFlags_Popup) != 0 ? "Popup " : "",
            (flags & ImGuiWindowFlags_Modal) != 0 ? "Modal " : "", (flags & ImGuiWindowFlags_ChildMenu) != 0 ? "ChildMenu " : "", (flags & ImGuiWindowFlags_NoSavedSettings) != 0 ? "NoSavedSettings " : "",
            (flags & ImGuiWindowFlags_NoMouseInputs) != 0 ? "NoMouseInputs" : "", (flags & ImGuiWindowFlags_NoNavInputs) != 0 ? "NoNavInputs" : "", (flags & ImGuiWindowFlags_AlwaysAutoResize) != 0 ? "AlwaysAutoResize" : "");
        if ((flags & ImGuiWindowFlags_ChildWindow) != 0)
            BulletText("ChildFlags: 0x%08X (%s%s%s%s..)", window.ChildFlags,
                (window.ChildFlags & ImGuiChildFlags_Borders) != 0 ? "Borders " : "",
                (window.ChildFlags & ImGuiChildFlags_ResizeX) != 0 ? "ResizeX " : "",
                (window.ChildFlags & ImGuiChildFlags_ResizeY) != 0 ? "ResizeY " : "",
                (window.ChildFlags & ImGuiChildFlags_NavFlattened) != 0 ? "NavFlattened " : "");
        BulletText("Scroll: (%.2f/%.2f,%.2f/%.2f) Scrollbar:%s%s", window.Scroll.x, window.ScrollMax.x, window.Scroll.y, window.ScrollMax.y, window.ScrollbarX ? "X" : "", window.ScrollbarY ? "Y" : "");
        BulletText("Active: %d/%d, WriteAccessed: %d, BeginOrderWithinContext: %d", window.Active ? 1 : 0, window.WasActive ? 1 : 0, window.WriteAccessed ? 1 : 0, (window.Active || window.WasActive) ? window.BeginOrderWithinContext : -1);
        BulletText("Appearing: %d, Hidden: %d (CanSkip %d Cannot %d), SkipItems: %d", window.Appearing ? 1 : 0, window.Hidden ? 1 : 0, window.HiddenFramesCanSkipItems, window.HiddenFramesCannotSkipItems, window.SkipItems ? 1 : 0);
        for (int layer = 0; layer < ImGuiNavLayer_COUNT; layer++)
        {
            ImRect r = window.NavRectRel[layer];
            if (r.Min.x >= r.Max.x && r.Min.y >= r.Max.y)
                BulletText("NavLastIds[%d]: 0x%08X", layer, window.NavLastIds[layer]);
            else
                BulletText("NavLastIds[%d]: 0x%08X at +(%.1f,%.1f)(%.1f,%.1f)", layer, window.NavLastIds[layer], r.Min.x, r.Min.y, r.Max.x, r.Max.y);
            DebugLocateItemOnHover(window.NavLastIds[layer]);
        }
        ImVec2[] pr = window.NavPreferredScoringPosRel;
        for (int layer = 0; layer < ImGuiNavLayer_COUNT; layer++)
            BulletText("NavPreferredScoringPosRel[%d] = (%.1f,%.1f)", layer, (pr[layer].x == float.MaxValue ? -99999.0f : pr[layer].x), (pr[layer].y == float.MaxValue ? -99999.0f : pr[layer].y)); // Display as 99999.0f so it looks neater.
        BulletText("NavLayersActiveMask: %X, NavLastChildNavWindow: %s", window.DC.NavLayersActiveMask, window.NavLastChildNavWindow != null ? window.NavLastChildNavWindow.Name : "NULL");
        if (window.RootWindow != window) { DebugNodeWindow(window.RootWindow, "RootWindow"); }
        if (window.ParentWindow != null) { DebugNodeWindow(window.ParentWindow, "ParentWindow"); }
        if (window.ParentWindowForFocusRoute != null) { DebugNodeWindow(window.ParentWindowForFocusRoute, "ParentWindowForFocusRoute"); }
        if (window.DC.ChildWindows.Size > 0) { DebugNodeWindowsList(window.DC.ChildWindows, "ChildWindows"); }
        if (window.ColumnsStorage.Size > 0 && TreeNode("Columns", "Columns sets (%d)", window.ColumnsStorage.Size))
        {
            for (int col_i = 0; col_i < window.ColumnsStorage.Size; col_i++)
                DebugNodeColumns(window.ColumnsStorage[col_i]);
            TreePop();
        }
        DebugNodeStorage(window.StateStorage, "Storage");
        TreePop();
    }

    // C++: void ImGui::DebugNodeWindowSettings(ImGuiWindowSettings* settings)
    public static void DebugNodeWindowSettings(ImGuiWindowSettings settings)
    {
        if (settings.WantDelete)
            BeginDisabled(true);
        BulletText("0x%08X \"%s\" Pos (%d,%d) Size (%d,%d) Collapsed=%d",
            settings.ID, settings.Name, settings.Pos.x, settings.Pos.y, settings.Size.x, settings.Size.y, settings.Collapsed ? 1 : 0);
        if (settings.WantDelete)
            EndDisabled();
    }

    // C++: void ImGui::DebugNodeWindowsList(ImVector<ImGuiWindow*>* windows, const char* label)
    public static void DebugNodeWindowsList(ImVector<ImGuiWindow> windows, string label)
    {
        if (!TreeNode(label, "%s (%d)", label, windows.Size))
            return;
        for (int i = windows.Size - 1; i >= 0; i--) // Iterate front to back
        {
            PushID(windows[i]);
            DebugNodeWindow(windows[i], "Window");
            PopID();
        }
        TreePop();
    }

    // FIXME-OPT: This is technically suboptimal, but it is simpler this way.
    // C++: void ImGui::DebugNodeWindowsListByBeginStackParent(ImGuiWindow** windows, int windows_size, ImGuiWindow* parent_in_begin_stack)
    public static void DebugNodeWindowsListByBeginStackParent(ImVector<ImGuiWindow> windows, int windows_size, ImGuiWindow parent_in_begin_stack)
    {
        for (int i = 0; i < windows_size; i++)
        {
            ImGuiWindow window = windows[i];
            if (window.ParentWindowInBeginStack != parent_in_begin_stack)
                continue;
            string buf = string.Format("[{0:D4}] Window", window.BeginOrderWithinContext);
            //BulletText("[%04d] Window '%s'", window.BeginOrderWithinContext, window.Name);
            DebugNodeWindow(window, buf);
            TreePush(buf);
            // C++ slices the array (windows + i + 1); emulate with a temp copy of the tail.
            var tail = new ImVector<ImGuiWindow>();
            for (int j = i + 1; j < windows_size; j++)
                tail.push_back(windows[j]);
            DebugNodeWindowsListByBeginStackParent(tail, tail.Size, window);
            TreePop();
        }
    }

    // C++: ImTextCharToUtf8 into a temp buffer; C# returns a managed string.
    static string CpToUtf8(uint c)
    {
        byte[] buf = new byte[5];
        int n = ImText.ImTextCharToUtf8(buf, 0, c);
        return System.Text.Encoding.UTF8.GetString(buf, 0, n);
    }
}
