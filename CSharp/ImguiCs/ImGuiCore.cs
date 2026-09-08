// ImGuiCore: shared constants and helpers of the ImGui core (mirrors ImGui:: static helpers
// of imgui_internal.h / imgui.cpp / imgui_draw.cpp). The public API class is ImGui.
using System;

namespace ImGui;

public static partial class ImGuiCore
{
    // Color macros (imgui.h IM_COL32)
    public const int IM_COL32_R_SHIFT = 0;
    public const int IM_COL32_G_SHIFT = 8;
    public const int IM_COL32_B_SHIFT = 16;
    public const int IM_COL32_A_SHIFT = 24;
    public const ImU32 IM_COL32_A_MASK = 0xFF000000u;

    public static ImU32 IM_COL32(int r, int g, int b, int a)
        => (uint)((a << IM_COL32_A_SHIFT) | (b << IM_COL32_B_SHIFT) | (g << IM_COL32_G_SHIFT) | (r << IM_COL32_R_SHIFT));

    public static readonly ImU32 IM_COL32_WHITE = IM_COL32(255, 255, 255, 255);     // Opaque white = 0xFFFFFFFF
    public static readonly ImU32 IM_COL32_BLACK = IM_COL32(0, 0, 0, 255);           // Opaque black
    public static readonly ImU32 IM_COL32_BLACK_TRANS = IM_COL32(0, 0, 0, 0);       // Transparent black = 0x00000000
    public const int IM_COL32_DISABLE = 0x00000001;                                 // Special sentinel code which cannot be used as a regular color.

    public const float IMGUI_FONT_SIZE_THRESHOLD_FOR_LOADADVANCEXONLYMODE = 128.0f;

    public static float GetRoundedFontSize(float size) => ImMath.ImRounded(size);

    public static void AddDrawListToDrawDataEx(ImDrawData draw_data, ImVector<ImDrawList> out_list, ImDrawList draw_list)
    {
        if (draw_list.CmdBuffer.Size == 0)
            return;
        if (draw_list.CmdBuffer.Size == 1 && draw_list.CmdBuffer[0].ElemCount == 0 && draw_list.CmdBuffer[0].UserCallback == null)
            return;

        // Draw list sanity check. Detect mismatch between PrimReserve() calls and incrementing _VtxCurrentIdx, _VtxWritePtr etc.
        System.Diagnostics.Debug.Assert(draw_list.VtxBuffer.Size == 0 || draw_list._VtxWritePos == draw_list.VtxBuffer.Size);
        System.Diagnostics.Debug.Assert(draw_list.IdxBuffer.Size == 0 || draw_list._IdxWritePos == draw_list.IdxBuffer.Size);
        if ((draw_list.Flags & ImDrawListFlags_.ImDrawListFlags_AllowVtxOffset) == 0)
            System.Diagnostics.Debug.Assert((int)draw_list._VtxCurrentIdx == draw_list.VtxBuffer.Size);

        // Add to output list + records state in ImDrawData
        out_list.push_back(draw_list);
        draw_data.TotalVtxCount += draw_list.VtxBuffer.Size;
        draw_data.TotalIdxCount += draw_list.IdxBuffer.Size;
    }

    public static bool ImTriangleContainsPoint(ImVec2 a, ImVec2 b, ImVec2 c, ImVec2 p)
    {
        bool b1 = ((p.x - b.x) * (a.y - b.y) - (p.y - b.y) * (a.x - b.x)) < 0.0f;
        bool b2 = ((p.x - c.x) * (b.y - c.y) - (p.y - c.y) * (b.x - c.x)) < 0.0f;
        bool b3 = ((p.x - a.x) * (c.y - a.y) - (p.y - a.y) * (c.x - a.x)) < 0.0f;
        return (b1 == b2) && (b2 == b3);
    }

    public static bool ImTriangleIsClockwise(ImVec2 a, ImVec2 b, ImVec2 c)
        => ((b.x - a.x) * (c.y - b.y)) - ((c.x - b.x) * (b.y - a.y)) > 0.0f;

    // Linearly uv mapping from a..b to uv_a..uv_b over the vertices in [vert_start_idx, vert_end_idx)
    public static void ShadeVertsLinearUV(ImDrawList draw_list, int vert_start_idx, int vert_end_idx, ImVec2 a, ImVec2 b, ImVec2 uv_a, ImVec2 uv_b, bool clamp)
    {
        ImVec2 size = b - a;
        ImVec2 uv_size = uv_b - uv_a;
        ImVec2 scale = new ImVec2(
            size.x != 0.0f ? (uv_size.x / size.x) : 0.0f,
            size.y != 0.0f ? (uv_size.y / size.y) : 0.0f);

        if (clamp)
        {
            ImVec2 min = ImMath.ImMin(uv_a, uv_b);
            ImVec2 max = ImMath.ImMax(uv_a, uv_b);
            for (int i = vert_start_idx; i < vert_end_idx; i++)
            {
                var v = draw_list.VtxBuffer[i];
                v.uv = ImMath.ImClamp(uv_a + (new ImVec2(v.pos.x, v.pos.y) - a) * scale, min, max);
                draw_list.VtxBuffer[i] = v;
            }
        }
        else
        {
            for (int i = vert_start_idx; i < vert_end_idx; i++)
            {
                var v = draw_list.VtxBuffer[i];
                v.uv = uv_a + (new ImVec2(v.pos.x, v.pos.y) - a) * scale;
                draw_list.VtxBuffer[i] = v;
            }
        }
    }
}
