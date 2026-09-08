// Regression tests for UTF-8 decoding + IME Chinese input into InputText.
//
// Background: ImTextCharFromUtf8() used to mis-decode every multi-byte sequence (it ORed
// continuation bytes into an all-ones mask, yielding garbage codepoints >= 0x10000 with a
// correct byte length). InputTextCalcTextSize()'s byte->UTF-16 map counted such garbage as
// 2 UTF-16 units while the actual string had 1 char, so ImFontCalcTextSizeEx() walked past
// the end of the string and crashed with IndexOutOfRangeException on the first CJK char
// typed via IME ("你好世界" repro'd 100%).
using System;
using System.Text;
using Xunit;
namespace ImGui.Tests;

[Collection("ImGui.Tests.GoldenXunitTest")]
public class ImeInputTest
{
    static void NewContext()
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(640.0f, 480.0f);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
    }

    static string s_applied = "";

    static void Frame(ImGuiIO io)
    {
        io.AddMousePosEvent(40.0f, 16.0f);
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
        ImGui.Begin("win", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_AlwaysAutoResize);
        string buf = s_applied;
        ImGui.InputTextWithHint("input", "hint", ref buf, 256);
        s_applied = buf;
        ImGui.End();
        ImGui.Render();
    }

    [Fact]
    public void Ime_chinese_typing_does_not_crash()
    {
        NewContext();
        ImGuiIO io = ImGui.GetIO();

        // Frame 1-2: window + hover
        Frame(io);
        Frame(io);

        // Frame 3: click to activate
        io.AddMouseButtonEvent(0, true);
        Frame(io);
        io.AddMouseButtonEvent(0, false);

        // Type Chinese chars (BMP, as a real IME commits them)
        foreach (char c in "你好世界")
        {
            io.AddInputCharacter(c);
            Frame(io);
        }
        Assert.Equal("你好世界", s_applied);

        // Cursor moves + editing around multi-byte chars
        io.AddKeyEvent(ImGuiKey_LeftArrow, true); Frame(io);
        io.AddKeyEvent(ImGuiKey_LeftArrow, false); Frame(io);
        io.AddKeyEvent(ImGuiKey_RightArrow, true); Frame(io);
        io.AddKeyEvent(ImGuiKey_RightArrow, false); Frame(io);
        io.AddKeyEvent(ImGuiKey_Home, true); Frame(io);
        io.AddKeyEvent(ImGuiKey_Home, false); Frame(io);
        io.AddKeyEvent(ImGuiKey_End, true); Frame(io);
        io.AddKeyEvent(ImGuiKey_End, false); Frame(io);
        io.AddKeyEvent(ImGuiKey_Backspace, true); Frame(io);
        io.AddKeyEvent(ImGuiKey_Backspace, false); Frame(io);
        Assert.Equal("你好世", s_applied);

        // Insert at the start (cursor is after Home earlier? ensure End then Home then type)
        io.AddKeyEvent(ImGuiKey_Home, true); Frame(io);
        io.AddKeyEvent(ImGuiKey_Home, false); Frame(io);
        io.AddInputCharacter('中'); Frame(io);
        Assert.Equal("中你好世", s_applied);

        // Astral-plane input degrades to U+FFFD like C++ (ImWchar is 16-bit)
        io.AddKeyEvent(ImGuiKey_End, true); Frame(io);
        io.AddKeyEvent(ImGuiKey_End, false); Frame(io);
        io.AddInputCharacter(0x1F600); Frame(io);
        Assert.Equal("中你好世\uFFFD", s_applied);

        ImGui.DestroyContext();
    }

    // C++: ImTextCharFromUtf8() semantics from imgui.cpp (imgui_decode_utf8).
    [Fact]
    public void ImTextCharFromUtf8_matches_cpp_decoder()
    {
        static int Decode(string hexBytes, out uint c)
        {
            byte[] b = new byte[hexBytes.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(hexBytes.Substring(i * 2, 2), 16);
            // Pad so sequences can read up to 4 bytes like a real buffer.
            byte[] buf = new byte[b.Length + 4];
            b.CopyTo(buf, 0);
            return ImText.ImTextCharFromUtf8(out c, buf, 0, b.Length);
        }

        // Valid ASCII / 2-byte / 3-byte
        Assert.Equal((1, 'A'), (Decode("41", out uint c1), (char)c1));
        Assert.Equal((2, 'é'), (Decode("C3A9", out uint c2), (char)c2));
        Assert.Equal((3, '中'), (Decode("E4B8AD", out uint c3), (char)c3));
        Assert.Equal((3, '€'), (Decode("E282AC", out uint c4), (char)c4));

        // Valid 4-byte codepoint: out of ImWchar range -> 0xFFFD consumed in full (C++ 16-bit behavior)
        int n = Decode("F09F9880", out uint c5);
        Assert.Equal((4, 0xFFFDu), (n, c5));

        // Truncated sequences consume the available bytes and yield 0xFFFD
        Assert.Equal((1, 0xFFFDu), (Decode("E4", out uint t1), t1));
        Assert.Equal((2, 0xFFFDu), (Decode("E4B8", out uint t2), t2));
        Assert.Equal((1, 0xFFFDu), (Decode("C3", out uint t3), t3));

        // Invalid lead bytes (continuation byte / 0xF8+): consume 1, 0xFFFD
        Assert.Equal((1, 0xFFFDu), (Decode("80", out uint i1), i1));
        Assert.Equal((1, 0xFFFDu), (Decode("FF", out uint i2), i2));

        // Overlong encoding C0 80: invalid, consumes 2
        Assert.Equal((2, 0xFFFDu), (Decode("C080", out uint o1), o1));

        // UTF-8-encoded surrogate ED A0 80 (D800): invalid, consumes 3
        Assert.Equal((3, 0xFFFDu), (Decode("EDA080", out uint s1), s1));

        // Above 0x10FFFF (F4 90 80 80): invalid, consumes 4
        Assert.Equal((4, 0xFFFDu), (Decode("F4908080", out uint m1), m1));

        // NUL decodes as codepoint 0 consuming 1 (matches C++ 1.92)
        Assert.Equal((1, 0u), (Decode("00", out uint z1), z1));
    }

    // The crash invariant: the byte->UTF-16 map built by InputTextCalcTextSize() must never
    // exceed the actual UTF-16 length of the converted string, for any byte content.
    [Fact]
    public void ByteToUtf16Map_never_exceeds_string_length()
    {
        string[] samples =
        {
            "", "hello", "你好世界", "mix 中en文 text", "€€€", "a\u0301b",
            "😀 emoji", "\uFFFD", "end-tail 你", "\0embedded\0nul",
        };
        byte[] truncated = Convert.FromHexString("E4B8ADE4B8"); // valid char + truncated tail
        foreach (string sample in samples)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sample);
            int[] map = ImGui.BuildByteToUtf16Map(bytes, 0, bytes.Length);
            Assert.True(map[map.Length - 1] <= sample.Length, $"map overrun for '{sample}': {map[map.Length - 1]} > {sample.Length}");
        }
        {
            int[] map = ImGui.BuildByteToUtf16Map(truncated, 0, truncated.Length);
            string s = Encoding.UTF8.GetString(truncated);
            Assert.True(map[map.Length - 1] <= s.Length, $"map overrun for truncated tail: {map[map.Length - 1]} > {s.Length}");
        }
    }
}
