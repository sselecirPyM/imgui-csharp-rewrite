// Regression tests for the input event queue (ImGuiInputEventType values must
// stay distinct: a past generation bug had Text == Key == 5, so every text
// event was processed as a Key event with key=0 and crashed UpdateInputEvents).
using System.Text;
using Xunit;
namespace ImGui.Tests;

// Shares the static ImGuiContext with the golden harness: must not run in parallel with it.
[Collection("ImGui.Tests.GoldenXunitTest")]
public class InputEventTest
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
    public void InputEventType_values_are_distinct()
    {
        Assert.NotEqual(ImGuiInputEventType_Key, ImGuiInputEventType_Text);
        Assert.NotEqual(ImGuiInputEventType_Text, ImGuiInputEventType_Focus);
        Assert.Equal(ImGuiInputEventType_Focus + 1, ImGuiInputEventType_COUNT);
    }

    [Fact]
    public void Text_events_reach_input_queue_and_do_not_crash()
    {
        NewContext();
        ImGuiIO io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey_A, true);
        io.AddInputCharacter('a');
        ImGui.NewFrame();
        Assert.Equal(0, ImGui.GImGui.InputEventsQueue.Size); // queue processed by NewFrame
        Assert.Equal(1, io.InputQueueCharacters.Size);
        Assert.Equal((ImWchar)'a', io.InputQueueCharacters[0]);
        ImGui.EndFrame();
        ImGui.DestroyContext();
    }

    [Fact]
    public void Typing_into_inputtext_appends_characters()
    {
        NewContext();
        ImGuiIO io = ImGui.GetIO();
        byte[] buf = new byte[64];
        Encoding.UTF8.GetBytes("hi").CopyTo(buf, 0);
        // Frame 1: create the window (hovered-window tracking lags one frame)
        io.AddMousePosEvent(40.0f, 16.0f);
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
        ImGui.Begin("win", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_AlwaysAutoResize);
        ImGui.InputText("input", buf, buf.Length);
        ImGui.End();
        ImGui.Render();
        // Frame 2: window now hoverable
        io.AddMousePosEvent(40.0f, 16.0f);
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
        ImGui.Begin("win", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_AlwaysAutoResize);
        ImGui.InputText("input", buf, buf.Length);
        ImGui.End();
        ImGui.Render();
        // Frame 2+: press mouse button over the item, then type
        io.AddMousePosEvent(40.0f, 16.0f);
        io.AddMouseButtonEvent(0, true);
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
        ImGui.Begin("win", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_AlwaysAutoResize);
        ImGui.InputText("input", buf, buf.Length);
        var rmin = ImGui.GetItemRectMin(); var rmax = ImGui.GetItemRectMax();
        System.Console.WriteLine($"[typing-test] click frame: rect=({rmin.x},{rmin.y})-({rmax.x},{rmax.y}) winHovered={ImGui.IsWindowHovered()} itemHovered={ImGui.IsItemHovered()} mouseDown={io.MouseDown[0]} capture={io.WantCaptureMouse} MouseClicked0={ImGui.IsMouseClicked(0)}");
        ImGui.End();
        ImGui.Render();
        for (int f = 0; f < 6; f++)
        {
            io.AddMousePosEvent(40.0f, 16.0f);
            io.AddMouseButtonEvent(0, f < 3); // release after 3 frames
            io.AddInputCharacter(f == 2 ? 'a' : 'b');
            io.AddKeyEvent(ImGuiKey_Backspace, false);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new ImVec2(0, 0), ImGuiCond_Always);
            ImGui.Begin("win", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.InputText("input", buf, buf.Length);
            ImGui.End();
            ImGui.Render();
            System.Console.WriteLine($"[typing-test] f={f} ActiveId={ImGui.GImGui.ActiveId} frameCount={ImGui.GImGui.FrameCount} buf={Encoding.UTF8.GetString(buf, 0, System.Array.IndexOf(buf, (byte)0) is int n2 && n2 >= 0 ? n2 : buf.Length)}");
        }
        string text = Encoding.UTF8.GetString(buf, 0, System.Array.IndexOf(buf, (byte)0) is int n && n >= 0 ? n : buf.Length);
        Assert.Contains("a", text);
        Assert.Contains("b", text);
        Assert.NotEqual("hi", text); // characters were actually inserted
        ImGui.DestroyContext();
    }
}
