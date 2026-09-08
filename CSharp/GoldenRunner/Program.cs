using System;
using System.IO;
namespace ImGui.Tests;
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "probe")
        {
            ImGui.CreateContext();
            var io = ImGui.GetIO();
            io.DisplaySize = new ImVec2(1280, 720);
            io.DeltaTime = 1f / 60f;
            io.IniFilename = null;
            io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
            io.Fonts.SetTexID(1);
            if (Environment.GetEnvironmentVariable("GOLDEN_ATLAS") != null)
            {
                var atlas = io.Fonts;
                var b = atlas.Builder;
                Console.Error.WriteLine($"ATLAS-DUMP rects={b.Rects.Size} packed={b.RectsPackedCount}");
                for (int i = 0; i < b.RectsIndex.Size; i++)
                {
                    var e = b.RectsIndex.Data[i];
                    if (!e.IsUsed) continue;
                    var r = b.Rects.Data[e.TargetIndex];
                    string tag = "";
                    if (i == (b.PackIdMouseCursors & ImUtil.ImFontAtlasRectId_IndexMask_)) tag = " MOUSE";
                    if (b.PackIdLinesTexData >= 0 && i == (b.PackIdLinesTexData & ImUtil.ImFontAtlasRectId_IndexMask_)) tag = " LINES";
                    Console.Error.WriteLine($"R idx={i} x={r.x} y={r.y} w={r.w} h={r.h}{tag}");
                }
            }
            ImGui.NewFrame();
            System.Console.Error.WriteLine($"FontSize={ImGui.GetFontSize()} FontSizeBase={ImGui.GImGui.FontSizeBase} Style.FontSizeBase={ImGui.GImGui.Style.FontSizeBase} FontWindowScale={ImGui.GImGui.CurrentWindow?.FontWindowScale}");
            var ts = ImGui.CalcTextSize("Hello");
            System.Console.Error.WriteLine($"CalcTextSize(Hello)=({ts.x},{ts.y})");
            ImGui.EndFrame();
            return 0;
        }
        string goldenDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ImguiCs.Tests", "GoldenData");
        var scenarios = GoldenTest.Scenarios;
        int only = args.Length;
        int pass = 0, fail = 0;
        foreach (var (name, fn) in scenarios)
        {
            if (only > 0 && Array.IndexOf(args, name) == -1) continue;
            string actual = GoldenTest.RunScenarioPublic(name, fn);
            string expPath = Path.Combine(goldenDir, name + ".txt");
            string actPath = Path.Combine(goldenDir, "actual", name + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(actPath));
            File.WriteAllText(actPath, actual);
            if (!File.Exists(expPath)) { Console.WriteLine($"MISSING {name}"); fail++; continue; }
            string expected = File.ReadAllText(expPath).Replace("\r\n", "\n");
            string[] e = expected.Split('\n'); string[] a = actual.Split('\n');
            int n = Math.Max(e.Length, a.Length); int diffs = 0;
            for (int i = 0; i < n; i++)
            {
                string el = i < e.Length ? e[i] : "<eof>";
                string al = i < a.Length ? a[i] : "<eof>";
                if (el != al)
                {
                    if (diffs < 12) Console.WriteLine($"  {name}:{i + 1}\n    exp: {el}\n    act: {al}");
                    diffs++;
                }
            }
            if (diffs == 0) { pass++; Console.WriteLine($"PASS {name}"); }
            else { fail++; Console.WriteLine($"FAIL {name} ({diffs} lines)"); }
        }
        Console.WriteLine($"pass={pass} fail={fail}");
        return fail == 0 ? 0 : 1;
    }
}
