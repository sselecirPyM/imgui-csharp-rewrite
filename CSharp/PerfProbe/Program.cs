// Perf probe: identical UI workload to Cpp/perf_probe.cpp. Measures steady-state
// managed allocations per frame (GC.GetTotalAllocatedBytes) and wall time.
using System.Diagnostics;

namespace ImGui.Probe;

public static class Program
{
    // Workload A: full mix — formatted text, buttons, sliders, checkboxes, a table.
    static void FrameA(int frame)
    {
        ImGui.Begin("Perf window");
        for (int i = 0; i < 8; i++)
            ImGui.Text("Line %d: value %.1f", frame + i, frame * 1.5f + i);
        for (int i = 0; i < 8; i++)
        {
            ImGui.PushID(i);
            ImGui.Button("Button");
            ImGui.PopID();
        }
        for (int i = 0; i < 8; i++)
            ImGui.TextUnformatted("A literal unformatted label string");
        float f0 = 0.5f, f1 = 0.25f, f2 = 0.75f, f3 = 0.1f;
        ImGui.SliderFloat("Slider A", ref f0, 0.0f, 1.0f);
        ImGui.SliderFloat("Slider B", ref f1, 0.0f, 1.0f);
        ImGui.SliderFloat("Slider C", ref f2, 0.0f, 1.0f);
        ImGui.SliderFloat("Slider D", ref f3, 0.0f, 1.0f);
        bool b0 = false, b1 = true, b2 = false, b3 = true;
        ImGui.Checkbox("Check A", ref b0);
        ImGui.Checkbox("Check B", ref b1);
        ImGui.Checkbox("Check C", ref b2);
        ImGui.Checkbox("Check D", ref b3);
        if (ImGui.BeginTable("tbl", 3))
        {
            for (int r = 0; r < 4; r++)
            {
                ImGui.TableNextRow();
                for (int c = 0; c < 3; c++)
                {
                    ImGui.TableSetColumnIndex(c);
                    ImGui.Text("Cell %d,%d", r, c);
                }
            }
            ImGui.EndTable();
        }
        ImGui.End();
    }

    delegate void FrameFn(int frame);

    // Attribution workloads: measure marginal cost of each widget class.
    static void FrameD(int frame)
    {
        ImGui.Begin("Perf window D");
        for (int i = 0; i < 8; i++)
            ImGui.TextUnformatted("A literal unformatted label string");
        ImGui.End();
    }

    static void FrameE(int frame)
    {
        ImGui.Begin("Perf window E");
        for (int i = 0; i < 8; i++)
        {
            ImGui.PushID(i);
            ImGui.Button("Button");
            ImGui.PopID();
        }
        ImGui.End();
    }

    static void FrameF(int frame)
    {
        ImGui.Begin("Perf window F");
        float f = 0.5f;
        ImGui.SliderFloat("Slider A", ref f, 0.0f, 1.0f);
        ImGui.SliderFloat("Slider B", ref f, 0.0f, 1.0f);
        ImGui.SliderFloat("Slider C", ref f, 0.0f, 1.0f);
        ImGui.SliderFloat("Slider D", ref f, 0.0f, 1.0f);
        ImGui.End();
    }

    static void FrameG(int frame)
    {
        ImGui.Begin("Perf window G");
        for (int i = 0; i < 8; i++)
            ImGui.Text("Line %d: value %.1f", frame + i, frame * 1.5f + i);
        ImGui.End();
    }

    static void FrameH(int frame)
    {
        ImGui.Begin("Perf window H");
        if (ImGui.BeginTable("tbl", 3))
        {
            for (int r = 0; r < 4; r++)
            {
                ImGui.TableNextRow();
                for (int c = 0; c < 3; c++)
                {
                    ImGui.TableSetColumnIndex(c);
                    ImGui.Text("Cell %d,%d", r, c);
                }
            }
            ImGui.EndTable();
        }
        ImGui.End();
    }

    static void FrameI(int frame)
    {
        // empty: Run() itself does NewFrame + Render
    }

    static void Run(string name, FrameFn fn, bool feed_mouse = true)
    {
        var io = ImGui.GetIO();
        // Warmup (also builds font atlas / window pools, JITs the paths).
        for (int i = 0; i < 120; i++)
        {
            io.AddMousePosEvent(100 + (i & 63), 100);
            ImGui.NewFrame();
            fn(i);
            ImGui.Render();
        }
        long b0 = GC.GetTotalAllocatedBytes(precise: true);
        int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
        var sw = Stopwatch.StartNew();
        const int N = 600;
        for (int i = 0; i < N; i++)
        {
            if (feed_mouse)
                io.AddMousePosEvent(100 + (i & 63), 100);
            ImGui.NewFrame();
            fn(i);
            ImGui.Render();
        }
        sw.Stop();
        long bytes = GC.GetTotalAllocatedBytes(precise: true) - b0;
        double us = sw.Elapsed.TotalMicroseconds / N;
        Console.WriteLine($"[{name}] {bytes} bytes over {N} frames -> " +
            $"{(double)bytes / N:F1} B/frame, {(double)(GC.CollectionCount(0) - gen0) / N:F3} gen0/frame " +
            $"(gen1 +{GC.CollectionCount(1) - gen1}, gen2 +{GC.CollectionCount(2) - gen2}), {us:F1} us/frame");
    }

    public static int Main()
    {
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null; // avoid periodic ini save noise
        io.Fonts.GetTexDataAsRGBA32(out _, out _, out _, out _); // no backend: build the atlas up front
        Run("I2 NF+R no-mse", FrameI, feed_mouse: false);
        Run("I NF+Render   ", FrameI);
        Run("A full mix   ", FrameA);
        Run("D +8 lit text ", FrameD);
        Run("E +8 buttons  ", FrameE);
        Run("F +4 sliders  ", FrameF);
        Run("G +8 fmt text ", FrameG);
        Run("H table 4x3   ", FrameH);
        ImGui.DestroyContext();
        return 0;
    }
}
