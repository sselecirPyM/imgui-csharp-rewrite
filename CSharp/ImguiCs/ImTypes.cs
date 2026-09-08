// Port of Dear ImGui basic types (imgui.h [SECTION] Forward declarations and basic types).
using System;

namespace ImGui;

public struct ImVec2
{
    public float x, y;
    public ImVec2(float _x, float _y) { x = _x; y = _y; }
    public static readonly ImVec2 Zero = new ImVec2(0f, 0f);

    public static ImVec2 operator +(ImVec2 a, ImVec2 b) => new ImVec2(a.x + b.x, a.y + b.y);
    public static ImVec2 operator -(ImVec2 a, ImVec2 b) => new ImVec2(a.x - b.x, a.y - b.y);
    public static ImVec2 operator -(ImVec2 a) => new ImVec2(-a.x, -a.y);
    public static ImVec2 operator *(ImVec2 a, ImVec2 b) => new ImVec2(a.x * b.x, a.y * b.y);
    public static ImVec2 operator *(ImVec2 a, float f) => new ImVec2(a.x * f, a.y * f);
    public static ImVec2 operator /(ImVec2 a, float f) => new ImVec2(a.x / f, a.y / f);
    public static bool operator ==(ImVec2 a, ImVec2 b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(ImVec2 a, ImVec2 b) => !(a == b);
    public override bool Equals(object o) => o is ImVec2 v && this == v;
    public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
    public float this[int idx] { get { return idx == 0 ? x : y; } set { if (idx == 0) x = value; else y = value; } }

    // ImGui.NET compatibility: interoperate with System.Numerics.Vector2 without conversion noise at call sites.
    public static implicit operator ImVec2(System.Numerics.Vector2 v) => new ImVec2(v.X, v.Y);
    public static implicit operator System.Numerics.Vector2(ImVec2 v) => new System.Numerics.Vector2(v.x, v.y);
}

public struct ImVec4
{
    public float x, y, z, w;
    public ImVec4(float _x, float _y, float _z, float _w) { x = _x; y = _y; z = _z; w = _w; }
    public static readonly ImVec4 Zero = new ImVec4(0f, 0f, 0f, 0f);
    public static bool operator ==(ImVec4 a, ImVec4 b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
    public static bool operator !=(ImVec4 a, ImVec4 b) => !(a == b);
    public override bool Equals(object o) => o is ImVec4 v && this == v;
    public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode();

    // ImGui.NET compatibility: interoperate with System.Numerics.Vector4.
    public static implicit operator ImVec4(System.Numerics.Vector4 v) => new ImVec4(v.X, v.Y, v.Z, v.W);
    public static implicit operator System.Numerics.Vector4(ImVec4 v) => new System.Numerics.Vector4(v.x, v.y, v.z, v.w);
}

// ImVec2ih: 2D vector often half-word sized, used to store table cell data
public struct ImVec2ih
{
    public short x, y;
    public ImVec2ih(short _x, short _y) { x = _x; y = _y; }
    public ImVec2ih(int _x, int _y) { x = (short)_x; y = (short)_y; }
}

// ImVec1: 1D vector
public struct ImVec1
{
    public float x;
    public ImVec1(float _x) { x = _x; }
}

// ImRect: 2D axis aligned rectangle (imgui_internal.h)
public struct ImRect
{
    public ImVec2 Min, Max;

    public ImRect(ImVec2 min, ImVec2 max) { Min = min; Max = max; }
    public ImRect(float x1, float y1, float x2, float y2) { Min.x = x1; Min.y = y1; Max.x = x2; Max.y = y2; }

    public ImVec2 GetCenter() => new ImVec2((Min.x + Max.x) * 0.5f, (Min.y + Max.y) * 0.5f);
    public ImVec2 GetTL() => Min;                                   // Top-left
    public ImVec2 GetTR() => new ImVec2(Max.x, Min.y);              // Top-right
    public ImVec2 GetBL() => new ImVec2(Min.x, Max.y);              // Bottom-left
    public ImVec2 GetBR() => Max;                                   // Bottom-right
    public ImVec2 GetSize() => new ImVec2(Max.x - Min.x, Max.y - Min.y);
    public float GetWidth() => Max.x - Min.x;
    public float GetHeight() => Max.y - Min.y;
    public float GetArea() => (Max.x - Min.x) * (Max.y - Min.y);
    public bool IsEmpty() => Min.x >= Max.x || Min.y >= Max.y;
    public bool IsInverted() => Min.x > Max.x || Min.y > Max.y;

    public void Expand(float amount) { Min.x -= amount; Min.y -= amount; Max.x += amount; Max.y += amount; }
    public void Expand(ImVec2 amount) { Min.x -= amount.x; Min.y -= amount.y; Max.x += amount.x; Max.y += amount.y; }
    public void Translate(ImVec2 d) { Min.x += d.x; Min.y += d.y; Max.x += d.x; Max.y += d.y; }
    public void TranslateX(float dx) { Min.x += dx; Max.x += dx; }
    public void TranslateY(float dy) { Min.y += dy; Max.y += dy; }
    public void ClipWith(ImRect r) { Min = ImMath.ImMax(Min, r.Min); Max = ImMath.ImMin(Max, r.Max); }   // Simple version, may lead to an inverted rectangle
    public void ClipWithFull(ImRect r) { Min = ImMath.ImClamp(Min, r.Min, r.Max); Max = ImMath.ImClamp(Max, r.Min, r.Max); }
    public void Floor() { Min.x = (float)(int)Min.x; Min.y = (float)(int)Min.y; Max.x = (float)(int)Max.x; Max.y = (float)(int)Max.y; }

    public bool Contains(ImVec2 p) => p.x >= Min.x && p.y >= Min.y && p.x < Max.x && p.y < Max.y;
    public bool Contains(ImRect r) => r.Min.x >= Min.x && r.Min.y >= Min.y && r.Max.x <= Max.x && r.Max.y <= Max.y;
    public bool Overlaps(ImRect r) => r.Min.y < Max.y && r.Max.y > Min.y && r.Min.x < Max.x && r.Max.x > Min.x;
    public void Add(ImVec2 p) { if (Min.x > p.x) Min.x = p.x; if (Min.y > p.y) Min.y = p.y; if (Max.x < p.x) Max.x = p.x; if (Max.y < p.y) Max.y = p.y; }
    public void Add(ImRect r) { if (Min.x > r.Min.x) Min.x = r.Min.x; if (Min.y > r.Min.y) Min.y = r.Min.y; if (Max.x < r.Max.x) Max.x = r.Max.x; if (Max.y < r.Max.y) Max.y = r.Max.y; }

    // imgui_internal.h ImRect helpers (used by Box-Select support)
    public void AddX(float x) { if (Min.x > x) Min.x = x; if (Max.x < x) Max.x = x; }
    public void AddY(float y) { if (Min.y > y) Min.y = y; if (Max.y < y) Max.y = y; }
}

// ImColor: helper for color conversion to u32/float4 (obsolete in C++, kept for API parity)
public struct ImColor
{
    public ImVec4 Value;

    public ImColor(int r, int g, int b) { Value.x = r / 255.0f; Value.y = g / 255.0f; Value.z = b / 255.0f; Value.w = 1.0f; }
    public ImColor(int r, int g, int b, int a) { Value.x = r / 255.0f; Value.y = g / 255.0f; Value.z = b / 255.0f; Value.w = a / 255.0f; }
    public ImColor(float r, float g, float b, float a = 1.0f) { Value.x = r; Value.y = g; Value.z = b; Value.w = a; }
    public ImColor(ImVec4 col) { Value = col; }

    public static implicit operator ImVec4(ImColor c) => c.Value;
}
