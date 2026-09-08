// Small self-contained helpers from imgui.cpp / imgui_draw.cpp / imgui_internal.h that live
// outside the main ported ranges but are needed at runtime by them.
using System;
using System.Diagnostics;

namespace ImGui;

public static partial class ImGui
{
    // C++: bool ImGui::IsMouseDoubleClicked(ImGuiMouseButton button)   [imgui.cpp 10115, INPUTS]
    public static bool IsMouseDoubleClicked(ImGuiMouseButton button)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseClickedCount.Length);
        return g.IO.MouseClickedCount[button] == 2 && TestKeyOwner(MouseButtonToKey(button), ImGuiKeyOwner_Any);
    }

    // C++: void ImGui::CalcClipRectVisibleItemsY(const ImRect& clip_rect, const ImVec2& pos, float items_height, int* out_visible_start, int* out_visible_end)  [imgui.cpp, CalcListClipping section]
    // Generic helper, equivalent to old ImGui::CalcListClipping() but stateless
    public static void CalcClipRectVisibleItemsY(ImRect clip_rect, ImVec2 pos, float items_height, out int out_visible_start, out int out_visible_end)
    {
        out_visible_start = ImMath.ImMax((int)((clip_rect.Min.y - pos.y) / items_height), 0);
        out_visible_end = ImMath.ImMax((int)ImMath.ImCeil((clip_rect.Max.y - pos.y) / items_height), out_visible_start);
    }

    // C++: void ImGui::RenderArrowPointingAt(ImDrawList* draw_list, ImVec2 pos, ImVec2 half_sz, ImGuiDir direction, ImU32 col)   [imgui_draw.cpp]
    // Render an arrow. 'pos' is position of the arrow tip. half_sz.x is length from base to tip. half_sz.y is length on each side.
    public static void RenderArrowPointingAt(ImDrawList draw_list, ImVec2 pos, ImVec2 half_sz, ImGuiDir direction, ImU32 col)
    {
        switch (direction)
        {
            case ImGuiDir_Left: draw_list.AddTriangleFilled(new ImVec2(pos.x + half_sz.x, pos.y - half_sz.y), new ImVec2(pos.x + half_sz.x, pos.y + half_sz.y), pos, col); return;
            case ImGuiDir_Right: draw_list.AddTriangleFilled(new ImVec2(pos.x - half_sz.x, pos.y + half_sz.y), new ImVec2(pos.x - half_sz.x, pos.y - half_sz.y), pos, col); return;
            case ImGuiDir_Up: draw_list.AddTriangleFilled(new ImVec2(pos.x + half_sz.x, pos.y + half_sz.y), new ImVec2(pos.x - half_sz.x, pos.y + half_sz.y), pos, col); return;
            case ImGuiDir_Down: draw_list.AddTriangleFilled(new ImVec2(pos.x - half_sz.x, pos.y - half_sz.y), new ImVec2(pos.x + half_sz.x, pos.y - half_sz.y), pos, col); return;
            case ImGuiDir_None:
            case ImGuiDir_COUNT: break; // Fix warnings
        }
    }

    // C++: void ImGui::RenderColorRectWithAlphaCheckerboard(ImDrawList* draw_list, ImVec2 p_min, ImVec2 p_max, ImU32 col, float alpha, float grid_step, ImVec2 grid_off, float rounding = 0.0f, ImDrawFlags flags = 0)   [imgui_draw.cpp]
    // FIXME: Cleanup and move code to ImDrawList.
    public static void RenderColorRectWithAlphaCheckerboard(ImDrawList draw_list, ImVec2 p_min, ImVec2 p_max, ImU32 col, float alpha, float grid_step, ImVec2 grid_off, float rounding = 0.0f, ImDrawFlags flags = 0)
    {
        if ((flags & ImDrawFlags_RoundCornersMask_) == 0)
            flags = ImDrawFlags_RoundCornersAll;
        if (((col & ImGuiCore.IM_COL32_A_MASK) >> ImGuiCore.IM_COL32_A_SHIFT) < 0xFF)
        {
            System.Diagnostics.Debug.Assert(alpha >= 0.0f && alpha <= 1.0f);
            ImU32 a_mask = (ImU32)(alpha * 255.0f) << ImGuiCore.IM_COL32_A_SHIFT;
            ImU32 col_bg1 = (ImGuiCore.ImAlphaBlendColors(ImGuiCore.IM_COL32(128, 128, 128, 255), col) & ~ImGuiCore.IM_COL32_A_MASK) | a_mask;
            ImU32 col_bg2 = (ImGuiCore.ImAlphaBlendColors(ImGuiCore.IM_COL32(204, 204, 204, 255), col) & ~ImGuiCore.IM_COL32_A_MASK) | a_mask;
            bool dual_layer = (alpha == 1.0f); // Dual layer is faster but incorrect if blending.
            if (dual_layer)
                draw_list.AddRectFilled(p_min, p_max, col_bg1, rounding, flags);

            int yi = 0;
            for (float y = p_min.y + grid_off.y; y < p_max.y; y += grid_step, yi++)
            {
                float y1 = ImMath.ImClamp((float)(int)y, p_min.y, p_max.y), y2 = ImMath.ImMin((float)(int)(y + grid_step), p_max.y);
                if (y2 <= y1)
                    continue;

                float x_start_off = dual_layer ? (((yi ^ 1) & 1) * grid_step) : 0.0f;
                float x_step = dual_layer ? (grid_step * 2.0f) : (grid_step);
                int xi = 0;
                for (float x = p_min.x + grid_off.x + x_start_off; x < p_max.x; x += x_step, xi++)
                {
                    float x1 = ImMath.ImClamp((float)(int)x, p_min.x, p_max.x), x2 = ImMath.ImMin((float)(int)(x + grid_step), p_max.x);
                    if (x2 <= x1)
                        continue;
                    ImDrawFlags cell_flags = ImDrawFlags_RoundCornersNone; // FIXME: Could use CalcRoundingFlagsForRectInRect()
                    if (y1 <= p_min.y) { if (x1 <= p_min.x) cell_flags |= ImDrawFlags_RoundCornersTopLeft; if (x2 >= p_max.x) cell_flags |= ImDrawFlags_RoundCornersTopRight; }
                    if (y2 >= p_max.y) { if (x1 <= p_min.x) cell_flags |= ImDrawFlags_RoundCornersBottomLeft; if (x2 >= p_max.x) cell_flags |= ImDrawFlags_RoundCornersBottomRight; }

                    // Combine flags
                    cell_flags = (flags == ImDrawFlags_RoundCornersNone || cell_flags == ImDrawFlags_RoundCornersNone) ? ImDrawFlags_RoundCornersNone : (cell_flags & flags);
                    ImU32 col_bg = dual_layer ? col_bg2 : (((yi + xi) & 1) != 0 ? col_bg2 : col_bg1);
                    draw_list.AddRectFilled(new ImVec2(x1, y1), new ImVec2(x2, y2), col_bg, rounding, cell_flags);
                }
            }
        }
        else
        {
            draw_list.AddRectFilled(p_min, p_max, col, rounding, flags);
        }
    }

    // C++: void ImGui::ShadeVertsLinearColorGradientKeepAlpha(ImDrawList* draw_list, int vert_start_idx, int vert_end_idx, ImVec2 gradient_p0, ImVec2 gradient_p1, ImU32 col0, ImU32 col1)   [imgui_draw.cpp]
    public static void ShadeVertsLinearColorGradientKeepAlpha(ImDrawList draw_list, int vert_start_idx, int vert_end_idx, ImVec2 gradient_p0, ImVec2 gradient_p1, ImU32 col0, ImU32 col1)
    {
        ImVec2 gradient_extent = gradient_p1 - gradient_p0;
        float gradient_inv_length2 = 1.0f / ImUtil.ImLengthSqr(gradient_extent);
        int col0_r = (int)(col0 >> ImGuiCore.IM_COL32_R_SHIFT) & 0xFF;
        int col0_g = (int)(col0 >> ImGuiCore.IM_COL32_G_SHIFT) & 0xFF;
        int col0_b = (int)(col0 >> ImGuiCore.IM_COL32_B_SHIFT) & 0xFF;
        int col_delta_r = ((int)(col1 >> ImGuiCore.IM_COL32_R_SHIFT) & 0xFF) - col0_r;
        int col_delta_g = ((int)(col1 >> ImGuiCore.IM_COL32_G_SHIFT) & 0xFF) - col0_g;
        int col_delta_b = ((int)(col1 >> ImGuiCore.IM_COL32_B_SHIFT) & 0xFF) - col0_b;
        for (int i = vert_start_idx; i < vert_end_idx; i++)
        {
            ImDrawVert vert = draw_list.VtxBuffer[i];
            float d = ImMath.ImDot(new ImVec2(vert.pos.x - gradient_p0.x, vert.pos.y - gradient_p0.y), gradient_extent);
            float t = ImMath.ImClamp(d * gradient_inv_length2, 0.0f, 1.0f);
            int r = (int)(col0_r + col_delta_r * t);
            int gg = (int)(col0_g + col_delta_g * t);
            int b = (int)(col0_b + col_delta_b * t);
            vert.col = (uint)((r << ImGuiCore.IM_COL32_R_SHIFT) | (gg << ImGuiCore.IM_COL32_G_SHIFT) | (b << ImGuiCore.IM_COL32_B_SHIFT)) | (vert.col & ImGuiCore.IM_COL32_A_MASK);
            draw_list.VtxBuffer[i] = vert;
        }
    }
}

public static partial class ImText
{
    // C++: int ImTextCountLines(const char* in_text, const char* in_text_end)   [imgui.cpp 2848, TEXT UTILITIES]
    public static int ImTextCountLines(string in_text, int in_text_end)
    {
        if (in_text_end == -1) // C++: NULL means end of string
            in_text_end = in_text.Length;
        int count = 0;
        int pos = 0;
        while (pos < in_text_end)
        {
            int line_end = in_text.IndexOf('\n', pos);
            if (line_end == -1 || line_end >= in_text_end) // C++: ImMemchr bounded by in_text_end; no line_end -> consume the rest and count it
            {
                count++;
                break;
            }
            pos = line_end + 1;
            count++;
        }
        return count;
    }
}

public static partial class ImGuiCore
{
    // C++: void ImGui::ShadeVertsTransformPos(ImDrawList*, int, int, const ImVec2&, float, float, const ImVec2&)   [imgui_draw.cpp 2435, RENDERERS]
    public static void ShadeVertsTransformPos(ImDrawList draw_list, int vert_start_idx, int vert_end_idx, ImVec2 pivot_in, float cos_a, float sin_a, ImVec2 pivot_out)
    {
        for (int vertex_n = vert_start_idx; vertex_n < vert_end_idx; vertex_n++)
        {
            ImDrawVert vertex = draw_list.VtxBuffer[vertex_n];
            vertex.pos = ImMath.ImRotate(vertex.pos - pivot_in, cos_a, sin_a) + pivot_out;
            draw_list.VtxBuffer[vertex_n] = vertex;
        }
    }

    // --- Geometry/color helpers (C++ imgui.cpp [SECTION] MISC HELPERS/UTILITIES (Geometry functions)), used by ColorPicker4()'s HSV triangle ---

    // C++: ImVec2 ImLineClosestPoint(const ImVec2& a, const ImVec2& b, const ImVec2& p)
    public static ImVec2 ImLineClosestPoint(ImVec2 a, ImVec2 b, ImVec2 p)
    {
        ImVec2 ap = p - a;
        ImVec2 ab_dir = b - a;
        float dot = ap.x * ab_dir.x + ap.y * ab_dir.y;
        if (dot < 0.0f)
            return a;
        float ab_len_sqr = ab_dir.x * ab_dir.x + ab_dir.y * ab_dir.y;
        if (dot > ab_len_sqr)
            return b;
        return a + ab_dir * dot / ab_len_sqr;
    }

    // C++: ImVec2 ImTriangleClosestPoint(const ImVec2& a, const ImVec2& b, const ImVec2& c, const ImVec2& p)
    public static ImVec2 ImTriangleClosestPoint(ImVec2 a, ImVec2 b, ImVec2 c, ImVec2 p)
    {
        ImVec2 proj_ab = ImLineClosestPoint(a, b, p);
        ImVec2 proj_bc = ImLineClosestPoint(b, c, p);
        ImVec2 proj_ca = ImLineClosestPoint(c, a, p);
        float dist2_ab = ImUtil.ImLengthSqr(p - proj_ab);
        float dist2_bc = ImUtil.ImLengthSqr(p - proj_bc);
        float dist2_ca = ImUtil.ImLengthSqr(p - proj_ca);
        float m = ImMath.ImMin(dist2_ab, ImMath.ImMin(dist2_bc, dist2_ca));
        if (m == dist2_ab)
            return proj_ab;
        if (m == dist2_bc)
            return proj_bc;
        return proj_ca;
    }

    // C++: void ImTriangleBarycentricCoords(const ImVec2& a, const ImVec2& b, const ImVec2& c, const ImVec2& p, float& out_u, float& out_v, float& out_w)
    public static void ImTriangleBarycentricCoords(ImVec2 a, ImVec2 b, ImVec2 c, ImVec2 p, out float out_u, out float out_v, out float out_w)
    {
        ImVec2 v0 = b - a;
        ImVec2 v1 = c - a;
        ImVec2 v2 = p - a;
        float denom = v0.x * v1.y - v1.x * v0.y;
        out_v = (v2.x * v1.y - v1.x * v2.y) / denom;
        out_w = (v0.x * v2.y - v2.x * v0.y) / denom;
        out_u = 1.0f - out_v - out_w;
    }

    // C++: ImU32 ImAlphaBlendColors(ImU32 col_a, ImU32 col_b)   [imgui.cpp]
    public static ImU32 ImAlphaBlendColors(ImU32 col_a, ImU32 col_b)
    {
        float t = ((col_b >> ImGuiCore.IM_COL32_A_SHIFT) & 0xFF) / 255.0f;
        int r = (int)ImMath.ImLerp((int)(col_a >> ImGuiCore.IM_COL32_R_SHIFT) & 0xFF, (int)(col_b >> ImGuiCore.IM_COL32_R_SHIFT) & 0xFF, t);
        int g = (int)ImMath.ImLerp((int)(col_a >> ImGuiCore.IM_COL32_G_SHIFT) & 0xFF, (int)(col_b >> ImGuiCore.IM_COL32_G_SHIFT) & 0xFF, t);
        int b = (int)ImMath.ImLerp((int)(col_a >> ImGuiCore.IM_COL32_B_SHIFT) & 0xFF, (int)(col_b >> ImGuiCore.IM_COL32_B_SHIFT) & 0xFF, t);
        return ImGuiCore.IM_COL32(r, g, b, 0xFF);
    }
}

public static partial class ImUtil
{
    // C++: static inline int ImCountSetBits(unsigned int v)   [imgui_internal.h]
    public static int ImCountSetBits(uint v)
    {
        int count = 0;
        while (v != 0) { v = v & (v - 1); count++; }
        return count;
    }

    // C++: static inline float ImLengthSqr(const ImVec2& lhs)   [imgui_internal.h]
    public static float ImLengthSqr(ImVec2 lhs) { return lhs.x * lhs.x + lhs.y * lhs.y; }
}
