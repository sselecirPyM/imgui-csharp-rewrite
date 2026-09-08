// Demo window geometry probe: runs ShowDemoWindow and dumps window rects for
// diffing against the C# port.
#include "imgui.h"
#include "imgui_internal.h"
#include <stdio.h>

int g_atlas_probe_clip = 2;

int main()
{
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2(1280.0f, 720.0f);
    io.DeltaTime = 1.0f / 60.0f;
    io.IniFilename = NULL;
    io.MousePos = ImVec2(64.0f, 48.0f);
    unsigned char* px; int tw, th, tbpp;
    io.Fonts->GetTexDataAsRGBA32(&px, &tw, &th, &tbpp);

    static bool show = true;
    for (int frame = 0; frame < 2; frame++) {
        ImGui::NewFrame();
        ImGui::ShowDemoWindow(&show);
        ImGui::Render();
        printf("FRAME %d\n", frame);
        for (int i = 0; i < ImGui::GetCurrentContext()->Windows.Size; i++) {
            ImGuiWindow* w = ImGui::GetCurrentContext()->Windows[i];
            printf("W name=%s pos=%.2f,%.2f size=%.2f,%.2f inner=(%.2f,%.2f)-(%.2f,%.2f) outer=(%.2f,%.2f)-(%.2f,%.2f) sbary=%d\n",
                w->Name, w->Pos.x, w->Pos.y, w->Size.x, w->Size.y,
                w->InnerClipRect.Min.x, w->InnerClipRect.Min.y, w->InnerClipRect.Max.x, w->InnerClipRect.Max.y,
                w->OuterRectClipped.Min.x, w->OuterRectClipped.Min.y, w->OuterRectClipped.Max.x, w->OuterRectClipped.Max.y,
                w->ScrollbarY);
        }
    }
    ImGui::DestroyContext();
    return 0;
}
