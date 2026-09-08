// Regression tests for the backend-facing mouse cursor contract (the value
// read by GetMouseCursor() after Render(), which platform backends apply):
// NewFrame() resets g.MouseCursor to Arrow, widgets call SetMouseCursor() while
// hovered, and the final value must stay readable until the next NewFrame().
//
// Timing notes matching C++ Dear ImGui behavior:
// - A newly created window is hidden for one frame ("Hide new windows for one
//   frame until they calculate their size": HiddenFramesCannotSkipItems is left
//   set when NewFrame runs FindHoveredWindowEx), so hover can latch no earlier
//   than frame 3.
// - Window borders only report hover after WINDOWS_RESIZE_FROM_EDGES_FEEDBACK_TIMER
//   (0.04s) of stationary hover, to reduce visual noise; corner grips have no delay.
using System;
using System.Text;
using Xunit;
namespace ImGui.Tests;

// Shares the static ImGuiContext with the golden harness: must not run in parallel with it.
[Collection("ImGui.Tests.GoldenXunitTest")]
public class MouseCursorTest
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

    [Fact]
    public void Hovering_inputtext_sets_textinput_cursor()
    {
        NewContext();
        ImGuiIO io = ImGui.GetIO();
        byte[] buf = new byte[64];
        Encoding.UTF8.GetBytes("hi").CopyTo(buf, 0);
        // Frame 1: create the window
        io.AddMousePosEvent(40.0f, 16.0f);
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
        ImGui.Begin("win", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_AlwaysAutoResize);
        ImGui.InputText("input", buf, buf.Length);
        ImGui.End();
        ImGui.Render();
        Assert.Equal(ImGuiMouseCursor_Arrow, ImGui.GetMouseCursor()); // window not hoverable yet
        // Frames 2-4: cursor over the input field
        for (int f = 2; f <= 4; f++)
        {
            io.AddMousePosEvent(40.0f, 16.0f);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
            ImGui.Begin("win", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.InputText("input", buf, buf.Length);
            ImGui.End();
            ImGui.Render();
            if (f == 2)
                Assert.Equal(ImGuiMouseCursor_Arrow, ImGui.GetMouseCursor()); // hidden-window frame
            if (f == 3)
                Assert.Equal(ImGuiMouseCursor_TextInput, ImGui.GetMouseCursor());
        }
        Assert.Equal(ImGuiMouseCursor_TextInput, ImGui.GetMouseCursor());
        io.AddMousePosEvent(0.0f, 0.0f);
        ImGui.NewFrame();
        ImGui.Render();
        ImGui.DestroyContext();
    }

    [Fact]
    public void Hovering_window_border_and_corner_sets_resize_cursor()
    {
        NewContext();
        ImGuiIO io = ImGui.GetIO();
        // Frame 1: create the window
        io.AddMousePosEvent(100.0f, 100.0f);
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
        ImGui.SetNextWindowSize(new ImVec2(200, 200), ImGuiCond_Always);
        ImGui.Begin("resizable");
        ImGui.End();
        ImGui.Render();
        // Frames 2-10: cursor on the right border (0.04s feedback delay ≈ 3 frames)
        for (int f = 2; f <= 10; f++)
        {
            io.AddMousePosEvent(200.0f, 100.0f);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
            ImGui.SetNextWindowSize(new ImVec2(200, 200), ImGuiCond_Always);
            ImGui.Begin("resizable");
            ImGui.End();
            ImGui.Render();
            if (f <= 4)
                Assert.Equal(ImGuiMouseCursor_Arrow, ImGui.GetMouseCursor()); // within feedback delay
            if (f == 5)
                Assert.Equal(ImGuiMouseCursor_ResizeEW, ImGui.GetMouseCursor());
        }
        Assert.Equal(ImGuiMouseCursor_ResizeEW, ImGui.GetMouseCursor());
        // Frames 11-13: cursor on the bottom-right corner grip (no delay)
        for (int f = 11; f <= 13; f++)
        {
            io.AddMousePosEvent(200.0f, 200.0f);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
            ImGui.SetNextWindowSize(new ImVec2(200, 200), ImGuiCond_Always);
            ImGui.Begin("resizable");
            ImGui.End();
            ImGui.Render();
        }
        Assert.Equal(ImGuiMouseCursor_ResizeNWSE, ImGui.GetMouseCursor());
        io.AddMousePosEvent(0.0f, 0.0f);
        ImGui.NewFrame();
        ImGui.Render();
        ImGui.DestroyContext();
    }
}
