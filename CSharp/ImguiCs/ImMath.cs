// Port of Dear ImGui math helpers (imgui_internal.h + imgui.cpp helpers).
namespace ImGui;

using System;

public static unsafe partial class ImMath
{
    // ImMin/ImMax/ImClamp/ImLerp/ImSwap (float overloads used most; generic where convenient)
    public static float ImMin(float lhs, float rhs) => lhs < rhs ? lhs : rhs;
    public static float ImMax(float lhs, float rhs) => lhs >= rhs ? lhs : rhs;
    public static int ImMin(int lhs, int rhs) => lhs < rhs ? lhs : rhs;
    public static int ImMax(int lhs, int rhs) => lhs >= rhs ? lhs : rhs;
    public static double ImMin(double lhs, double rhs) => lhs < rhs ? lhs : rhs;
    public static double ImMax(double lhs, double rhs) => lhs >= rhs ? lhs : rhs;

    public static float ImClamp(float v, float mn, float mx) => v < mn ? mn : (v > mx ? mx : v);
    public static int ImClamp(int v, int mn, int mx) => v < mn ? mn : (v > mx ? mx : v);

    public static ImVec2 ImMin(ImVec2 lhs, ImVec2 rhs) => new ImVec2(lhs.x < rhs.x ? lhs.x : rhs.x, lhs.y < rhs.y ? lhs.y : rhs.y);
    public static ImVec2 ImMax(ImVec2 lhs, ImVec2 rhs) => new ImVec2(lhs.x >= rhs.x ? lhs.x : rhs.x, lhs.y >= rhs.y ? lhs.y : rhs.y);
    public static ImVec2 ImClamp(ImVec2 v, ImVec2 mn, ImVec2 mx) => new ImVec2(ImClamp(v.x, mn.x, mx.x), ImClamp(v.y, mn.y, mx.y));

    public static float ImLerp(float a, float b, float t) => a + (b - a) * t;
    public static double ImLerp(double a, double b, float t) => a + (b - a) * (double)t;
    public static ImVec2 ImLerp(ImVec2 a, ImVec2 b, float t) => new ImVec2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    public static ImVec2 ImLerp(ImVec2 a, ImVec2 b, ImVec2 t) => new ImVec2(a.x + (b.x - a.x) * t.x, a.y + (b.y - a.y) * t.y);
    public static ImVec4 ImLerp(ImVec4 a, ImVec4 b, float t) => new ImVec4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);

    public static float ImSaturate(float f) => (f < 0.0f) ? 0.0f : (f > 1.0f) ? 1.0f : f;

    // The C-style trunc casts: positive values only assumed in most call sites (IM_TRUNC semantics)
    public static float ImTrunc(float f) => (float)(int)(f);
    public static ImVec2 ImTrunc(ImVec2 v) => new ImVec2((float)(int)(v.x), (float)(int)(v.y));
    public static float ImFabs(float f) => MathF.Abs(f);
    public static float ImFloor(float f) => (float)((f >= 0 || (float)(int)f == f) ? (int)f : (int)f - 1); // Decent replacement for floorf()
    public static ImVec2 ImFloor(ImVec2 v) => new ImVec2(ImFloor(v.x), ImFloor(v.y));
    public static float ImTrunc64(float f) => (float)(long)(f);
    public static float ImRound(float value) => (float)(int)((value >= 0) ? (value + 0.5f) : (value - 0.5f));

    public static float ImInvLength(ImVec2 lhs, float fail_value) { float d = lhs.x * lhs.x + lhs.y * lhs.y; if (d > 0.0f) return 1.0f / MathF.Sqrt(d); return fail_value; }
    public static float ImFloorSafe(float f) => MathF.Floor(f);
    public static float ImDot(ImVec2 a, ImVec2 b) => a.x * b.x + a.y * b.y;
    public static ImVec2 ImRotate(ImVec2 v, float cos_a, float sin_a) => new ImVec2(v.x * cos_a - v.y * sin_a, v.x * sin_a + v.y * cos_a);
    public static float ImLinearSweep(float current, float target, float speed) { if (current < target) return ImMin(current + speed, target); if (current > target) return ImMax(current - speed, target); return current; }
    public static float ImLinearRemapClamp(float s0, float s1, float d0, float d1, float x) => ImSaturate((x - s0) / (s1 - s0)) * (d1 - d0) + d0;

    public static ImVec2 ImMul(ImVec2 lhs, ImVec2 rhs) => new ImVec2(lhs.x * rhs.x, lhs.y * rhs.y);

    // We use C9911 powf()/fmodf() semantics via MathF. Values in ImGui flows are tame.
    public static float ImPow(float x, float y) => MathF.Pow(x, y);
    public static double ImPow(double x, double y) => Math.Pow(x, y);
    public static float ImFmod(float x, float y) { var r = x % y; return r; } // C# % on floats == C fmodf semantics
    public static float ImCos(float x) => MathF.Cos(x);
    public static float ImSin(float x) => MathF.Sin(x);
    public static float ImAcos(float x) => MathF.Acos(x);
    public static float ImSqrt(float x) => MathF.Sqrt(x);
    public static float ImFloorStd(float x) => MathF.Floor(x); // we often use ImFloor() internally and it is not inlined

    // Im_isPowerOfTwo / ImUpperPowerOfTwo (imgui_internal.h)
    public static bool ImIsPowerOfTwo(int v) => (v & (v - 1)) == 0 && v != 0;
    public static bool ImIsPowerOfTwo(ImS64 v) => (v & (v - 1)) == 0 && v != 0;
    public static int ImUpperPowerOfTwo(int v) { v--; v |= v >> 1; v |= v >> 2; v |= v >> 4; v |= v >> 8; v |= v >> 16; v++; return v; }

    // strtod-ish helpers used by ImGui text inputs
    public static int ImAtoi(string s, ref int out_v) { /* minimal: parse optional sign + digits */ int i = 0; bool neg = false; while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++; if (i < s.Length && (s[i] == '-' || s[i] == '+')) { neg = s[i] == '-'; i++; } long v = 0; while (i < s.Length && s[i] >= '0' && s[i] <= '9') { v = v * 10 + (s[i] - '0'); i++; if (v > int.MaxValue) v = int.MaxValue; } out_v = (int)(neg ? -v : v); return i; }

    // Approximate equality helper (imgui_internal.h)
    public static bool ImIsFloatAboveGuaranteedIntegerPrecision(float f) { return f <= 16777216; }
    public static float ImExponentialMoveTowards(float current, float target, float lambda, float dt) { return target + (current - target) * MathF.Exp(-lambda * dt); }

    // C/C++ behavior: division truncates toward zero (C# does too for int).
}

public static partial class ImMath
{
    public const float IM_PI = 3.14159265358979323846f;
    public const float IM_FLT_MAX = 3.402823466e+38F;

    public static float ImCeil(float x) => MathF.Ceiling(x);
    public static float ImRounded(float f) => MathF.Round(f);
    public static void ImSwap(ref float a, ref float b) { float tmp = a; a = b; b = tmp; }
    public static void ImSwap(ref int a, ref int b) { int tmp = a; a = b; b = tmp; }
    public static ImVec2 ImSwap(ImVec2 a, ImVec2 b) => new ImVec2(b.x, b.y); // (unused form)
}
