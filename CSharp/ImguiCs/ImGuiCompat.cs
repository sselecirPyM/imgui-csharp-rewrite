// ImGui.NET compatibility conveniences: ref System.Numerics overloads for the
// multi-component widgets so code written against ImGui.NET keeps working.
// (ImVec2/ImVec4 additionally have implicit conversions to/from Vector2/Vector4,
// see ImTypes.cs; ImTextureRef has an implicit conversion from ImTextureID.)
using System;
using System.Numerics;

namespace ImGui;

public static partial class ImGui
{
    // C++: bool ImGui::DragFloat2(const char* label, float v[2], ...)
    public static bool DragFloat2(string label, ref Vector2 v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        float[] a = [v.X, v.Y];
        bool changed = DragFloat2(label, a, v_speed, v_min, v_max, format, flags);
        if (changed)
            v = new Vector2(a[0], a[1]);
        return changed;
    }

    // C++: bool ImGui::DragFloat3(const char* label, float v[3], ...)
    public static bool DragFloat3(string label, ref Vector3 v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        float[] a = [v.X, v.Y, v.Z];
        bool changed = DragFloat3(label, a, v_speed, v_min, v_max, format, flags);
        if (changed)
            v = new Vector3(a[0], a[1], a[2]);
        return changed;
    }

    // C++: bool ImGui::DragFloat4(const char* label, float v[4], ...)
    public static bool DragFloat4(string label, ref Vector4 v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = 0)
    {
        float[] a = [v.X, v.Y, v.Z, v.W];
        bool changed = DragFloat4(label, a, v_speed, v_min, v_max, format, flags);
        if (changed)
            v = new Vector4(a[0], a[1], a[2], a[3]);
        return changed;
    }

    // C++: bool ImGui::DragInt2(const char* label, int v[2], ...)
    public static bool DragInt2(string label, ref int v0, ref int v1, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        int[] a = [v0, v1];
        bool changed = DragInt2(label, a, v_speed, v_min, v_max, format, flags);
        if (changed)
        {
            v0 = a[0];
            v1 = a[1];
        }
        return changed;
    }

    // C++: bool ImGui::DragInt3(const char* label, int v[3], ...)
    public static bool DragInt3(string label, ref int v0, ref int v1, ref int v2, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        int[] a = [v0, v1, v2];
        bool changed = DragInt3(label, a, v_speed, v_min, v_max, format, flags);
        if (changed)
        {
            v0 = a[0];
            v1 = a[1];
            v2 = a[2];
        }
        return changed;
    }

    // C++: bool ImGui::DragInt4(const char* label, int v[4], ...)
    public static bool DragInt4(string label, ref int v0, ref int v1, ref int v2, ref int v3, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = 0)
    {
        int[] a = [v0, v1, v2, v3];
        bool changed = DragInt4(label, a, v_speed, v_min, v_max, format, flags);
        if (changed)
        {
            v0 = a[0];
            v1 = a[1];
            v2 = a[2];
            v3 = a[3];
        }
        return changed;
    }

    // C++: bool ImGui::ColorEdit3(const char* label, float col[3], ImGuiColorEditFlags flags)
    public static bool ColorEdit3(string label, ref Vector3 color, ImGuiColorEditFlags flags = 0)
    {
        float[] c = [color.X, color.Y, color.Z];
        bool changed = ColorEdit3(label, c, flags);
        if (changed)
            color = new Vector3(c[0], c[1], c[2]);
        return changed;
    }

    // C++: bool ImGui::ColorEdit4(const char* label, float col[4], ImGuiColorEditFlags flags)
    public static bool ColorEdit4(string label, ref Vector4 color, ImGuiColorEditFlags flags = 0)
    {
        float[] c = [color.X, color.Y, color.Z, color.W];
        bool changed = ColorEdit4(label, c, flags);
        if (changed)
            color = new Vector4(c[0], c[1], c[2], c[3]);
        return changed;
    }
}
