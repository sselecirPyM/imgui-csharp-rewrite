// Integration ports: small helpers from imgui.cpp / imgui_draw.cpp / imgui_internal.h that other
// ported sections call.
using System;

namespace ImGui;

public static partial class ImGui
{
    // imgui_internal.h: GetCurrentWindowRead() / GetCurrentWindow()
    public static ImGuiWindow GetCurrentWindowRead()
    {
        ImGuiContext g = ImGui.GImGui;
        return g.CurrentWindow;
    }

    public static ImGuiWindow GetCurrentWindow()
    {
        ImGuiContext g = ImGui.GImGui;
        g.CurrentWindow.WriteAccessed = true;
        return g.CurrentWindow;
    }

    // imgui_internal.h inline: GetForegroundDrawList(ImGuiWindow*)
    public static ImDrawList GetForegroundDrawList(ImGuiWindow window)
    {
        return GetForegroundDrawList();
    }

    // imgui_internal.h inline: IsActiveIdUsingNavDir()
    public static bool IsActiveIdUsingNavDir(int dir)
    {
        ImGuiContext g = ImGui.GImGui;
        return (g.ActiveIdUsingNavDirMask & (1 << dir)) != 0;
    }

    // imgui_internal.h inline: LocalizeGetMsg()
    public static string LocalizeGetMsg(int key)
    {
        ImGuiContext g = ImGui.GImGui;
        string msg = g.LocalizationTable[key];
        return msg ?? "*Missing Text*";
    }

    // imgui_draw.cpp: RenderArrowDockMenu()
    public static void RenderArrowDockMenu(ImDrawList draw_list, ImVec2 p_min, float sz, ImU32 col)
    {
        draw_list.AddRectFilled(p_min + new ImVec2(sz * 0.20f, sz * 0.15f), p_min + new ImVec2(sz * 0.80f, sz * 0.30f), col);
        // C++: RenderArrowPointingAt(draw_list, ..., ImVec2(sz * 0.30f, sz * 0.40f), ImGuiDir_Down, col)
        ImVec2 pos = p_min + new ImVec2(sz * 0.50f, sz * 0.85f);
        ImVec2 half = new ImVec2(sz * 0.30f, sz * 0.40f);
        draw_list.AddTriangleFilled(new ImVec2(pos.x - half.x, pos.y - half.y), new ImVec2(pos.x + half.x, pos.y - half.y), pos, col);
    }

    // imgui_draw.cpp: RenderArrow / RenderBullet / RenderCheckMark
    public static void RenderArrow(ImDrawList draw_list, ImVec2 pos, ImU32 col, int dir, float scale = 1.0f)
    {
        float h = draw_list._Data.FontSize * 1.00f;
        float r = h * 0.40f * scale;
        ImVec2 center = pos + new ImVec2(h * 0.50f, h * 0.50f * scale);

        ImVec2 a = default, b = default, c = default;
        switch (dir)
        {
            case ImGuiDir_Up:
            case ImGuiDir_Down:
                if (dir == ImGuiDir_Up) r = -r;
                a = new ImVec2(+0.000f, +0.750f) * r;
                b = new ImVec2(-0.866f, -0.750f) * r;
                c = new ImVec2(+0.866f, -0.750f) * r;
                break;
            case ImGuiDir_Left:
            case ImGuiDir_Right:
                if (dir == ImGuiDir_Left) r = -r;
                a = new ImVec2(+0.750f, +0.000f) * r;
                b = new ImVec2(-0.750f, +0.866f) * r;
                c = new ImVec2(-0.750f, -0.866f) * r;
                break;
            case ImGuiDir_None:
            case ImGuiDir_COUNT:
                System.Diagnostics.Debug.Assert(false);
                break;
        }
        draw_list.AddTriangleFilled(center + a, center + b, center + c, col);
    }

    public static void RenderBullet(ImDrawList draw_list, ImVec2 pos, ImU32 col)
    {
        // FIXME-OPT: This should be baked in font now that it's easier.
        float font_size = draw_list._Data.FontSize;
        draw_list.AddCircleFilled(pos, font_size * 0.20f, col, (font_size < 22) ? 8 : (font_size < 40) ? 12 : 0); // Hardcode optimal/nice tessellation threshold
    }

    public static void RenderCheckMark(ImDrawList draw_list, ImVec2 pos, ImU32 col, float sz)
    {
        float thickness = ImMath.ImMax(sz / 5.0f, 1.0f);
        sz -= thickness * 0.5f;
        pos += new ImVec2(thickness * 0.25f, thickness * 0.25f);

        float third = sz / 3.0f;
        float bx = pos.x + third;
        float by = pos.y + sz - third * 0.5f;
        draw_list.PathLineTo(new ImVec2(bx - third, by - third));
        draw_list.PathLineTo(new ImVec2(bx, by));
        draw_list.PathLineTo(new ImVec2(bx + third * 2.0f, by - third * 2.0f));
        draw_list.PathStroke(col, thickness);
    }

    // imgui_draw.cpp: RenderRectFilledInRangeH()
    public static void RenderRectFilledInRangeH(ImDrawList draw_list, ImRect rect, ImU32 col, float fill_x0, float fill_x1, float rounding)
    {
        if (fill_x0 > fill_x1)
            return;

        ImVec2 p0 = new ImVec2(fill_x0, rect.Min.y);
        ImVec2 p1 = new ImVec2(fill_x1, rect.Max.y);
        if (rounding == 0.0f)
        {
            draw_list.AddRectFilled(p0, p1, col, 0.0f);
            return;
        }

        rounding = ImMath.ImClamp(ImMath.ImMin((rect.Max.x - rect.Min.x) * 0.5f, (rect.Max.y - rect.Min.y) * 0.5f) - 1.0f, 0.0f, rounding);
        float inv_rounding = 1.0f / rounding;
        float arc0_b = ImUtil.ImAcos01(1.0f - (p0.x - rect.Min.x) * inv_rounding);
        float arc0_e = ImUtil.ImAcos01(1.0f - (p1.x - rect.Min.x) * inv_rounding);
        float half_pi = ImMath.IM_PI * 0.5f; // We will == compare to this because we know this is the exact value ImAcos01 can return.
        float x0 = ImMath.ImMax(p0.x, rect.Min.x + rounding);
        if (arc0_b == arc0_e)
        {
            draw_list.PathLineTo(new ImVec2(x0, p1.y));
            draw_list.PathLineTo(new ImVec2(x0, p0.y));
        }
        else if (arc0_b == 0.0f && arc0_e == half_pi)
        {
            draw_list.PathArcToFast(new ImVec2(x0, p1.y - rounding), rounding, 3, 6); // BL
            draw_list.PathArcToFast(new ImVec2(x0, p0.y + rounding), rounding, 6, 9); // TR
        }
        else
        {
            draw_list.PathArcTo(new ImVec2(x0, p1.y - rounding), rounding, ImMath.IM_PI - arc0_e, ImMath.IM_PI - arc0_b, 0); // BL
            draw_list.PathArcTo(new ImVec2(x0, p0.y + rounding), rounding, ImMath.IM_PI + arc0_b, ImMath.IM_PI + arc0_e, 0); // TR
        }
        if (p1.x > rect.Min.x + rounding)
        {
            float arc1_b = ImUtil.ImAcos01(1.0f - (rect.Max.x - p1.x) * inv_rounding);
            float arc1_e = ImUtil.ImAcos01(1.0f - (rect.Max.x - p0.x) * inv_rounding);
            float x1 = ImMath.ImMin(p1.x, rect.Max.x - rounding);
            if (arc1_b == arc1_e)
            {
                draw_list.PathLineTo(new ImVec2(x1, p0.y));
                draw_list.PathLineTo(new ImVec2(x1, p1.y));
            }
            else if (arc1_b == 0.0f && arc1_e == half_pi)
            {
                draw_list.PathArcToFast(new ImVec2(x1, p0.y + rounding), rounding, 9, 12); // TR
                draw_list.PathArcToFast(new ImVec2(x1, p1.y - rounding), rounding, 0, 3);  // BR
            }
            else
            {
                draw_list.PathArcTo(new ImVec2(x1, p0.y + rounding), rounding, -arc1_e, -arc1_b, 0); // TR
                draw_list.PathArcTo(new ImVec2(x1, p1.y - rounding), rounding, arc1_b, arc1_e, 0);   // BR
            }
        }
        draw_list.PathFillConvex(col);
    }

    // imgui_draw.cpp: CalcRoundingFlagsForRectInRect()
    public static int CalcRoundingFlagsForRectInRect(ImRect r_in, ImRect r_outer, float threshold)
    {
        bool round_l = r_in.Min.x <= r_outer.Min.x + threshold;
        bool round_r = r_in.Max.x >= r_outer.Max.x - threshold;
        bool round_t = r_in.Min.y <= r_outer.Min.y + threshold;
        bool round_b = r_in.Max.y >= r_outer.Max.y - threshold;
        return ImDrawFlags_RoundCornersNone
            | ((round_t && round_l) ? ImDrawFlags_RoundCornersTopLeft : 0) | ((round_t && round_r) ? ImDrawFlags_RoundCornersTopRight : 0)
            | ((round_b && round_l) ? ImDrawFlags_RoundCornersBottomLeft : 0) | ((round_b && round_r) ? ImDrawFlags_RoundCornersBottomRight : 0);
    }

    // imgui.cpp: UpdateDebugToolItemPicker() — item picker UI only activates via DebugStartItemPicker().
    public static void UpdateDebugToolItemPicker()
    {
        ImGuiContext g = ImGui.GImGui;
        g.DebugItemPickerBreakId = 0;
        if (!g.DebugItemPickerActive)
            return;

        ImGuiID hovered_id = g.HoveredIdPreviousFrame;
        SetMouseCursor(ImGuiMouseCursor_Hand);
        if (IsKeyPressed(ImGuiKey_Escape))
            g.DebugItemPickerActive = false;
        bool change_mapping = g.IO.KeyMods == (ImGuiMod_Ctrl | ImGuiMod_Shift);
        if (!change_mapping && IsMouseClicked(g.DebugItemPickerMouseButton) && hovered_id != 0)
        {
            g.DebugItemPickerBreakId = hovered_id;
            g.DebugItemPickerActive = false;
        }
        for (int mouse_button = 0; mouse_button < 3; mouse_button++)
            if (change_mapping && IsMouseClicked(mouse_button))
                g.DebugItemPickerMouseButton = (byte)mouse_button;
        SetNextWindowBgAlpha(0.70f);
        if (!BeginTooltip())
            return;
        Text("HoveredId: 0x%08X", hovered_id);
        Text("Press ESC to abort picking.");
        string[] mouse_button_names = { "Left", "Right", "Middle" };
        if (change_mapping)
            Text("Remap w/ Ctrl+Shift: click anywhere to select new mouse button.");
        else
            TextColored(GetStyleColorVec4(hovered_id != 0 ? ImGuiCol_Text : ImGuiCol_TextDisabled), "Click %s Button to break in debugger! (remap w/ Ctrl+Shift)", mouse_button_names[g.DebugItemPickerMouseButton]);
        EndTooltip();
    }

    // Debug-tools-only paths (ID stack tool / item locate / flash style color). These only act when the
    // corresponding debug feature was explicitly activated, which the golden scenarios never do.
    // Full ports land with the METRICS/DEBUG TOOLS phase.

    // UpdateDebugToolFlashStyleColor + DebugLocateItemResolveWithLastItem + UpdateDebugToolItemPathQuery
    // + DebugHookIdInfo are ported for real in ImGuiDebugTools.cs (DEBUG TOOLS section).

    public static void DebugStartItemPicker()
    {
        ImGuiContext g = ImGui.GImGui;
        g.DebugItemPickerActive = true;
        g.DebugItemPickerMouseButton = 0;
    }

    // DebugBreakButton is ported for real in ImGuiDebugTools.cs.
}

public static partial class ImUtil
{
    // imgui_draw.cpp: ImAcos01()
    public static float ImAcos01(float x)
    {
        if (x <= 0.0f) return ImMath.IM_PI * 0.5f;
        if (x >= 1.0f) return 0.0f;
        return ImMath.ImAcos(x);
    }
}
