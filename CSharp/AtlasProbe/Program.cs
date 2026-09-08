// C# counterpart of Cpp/atlas_dump.cpp: builds the same atlases and dumps raw
// texture bytes so the rasterizer port can be compared byte-for-byte.
using System;
using System.IO;

namespace ImGui.Probe;

public static class Program
{
    public static void Main(string[] args)
    {
        string tag = args.Length > 0 ? args[0] : "default";
        string ttf = args.Length > 1 ? args[1] : null;
        float size = args.Length > 2 ? float.Parse(args[2]) : 14.0f;

        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(640.0f, 480.0f);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;

        if (ttf != null)
            io.Fonts.AddFontFromFileTTF(ttf, size, null, io.Fonts.GetGlyphRangesChineseFull());
        else
            io.Fonts.AddFontDefault();

        // Legacy backend path: fully bake the atlas up front (like golden_dump).
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);

        ImGui.NewFrame();
        if (ttf != null)
            ImGui.Text("Hello, 世界! ABCxyz 123 中文字形测试。");
        else
            ImGui.Text("Hello, World! ABCxyz 123");
        ImGui.Render();

        using (FileStream fs = new FileStream($"atlas_{tag}.bin", FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            for (int i = 0; i < io.Fonts.TexList.Size; i++)
            {
                ImTextureData tex = io.Fonts.TexList[i];
                w.Write(System.Text.Encoding.ASCII.GetBytes($"TEX {i} w={tex.Width} h={tex.Height} bpp={ImUtil.ImTextureDataGetFormatBytesPerPixel(tex.Format)}\n"));
                w.Write(tex.Pixels, 0, tex.GetSizeInBytes());
                w.Write(System.Text.Encoding.ASCII.GetBytes("\n"));
            }
        }
        ImGui.DestroyContext();
        Console.WriteLine($"dumped {tag}");
    }
}
