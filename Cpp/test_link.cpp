#include "imgui.h"
#include <cstdio>
int main() {
    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2(1280.0f, 720.0f);
    unsigned char* pixels; int w, h;
    io.Fonts->GetTexDataAsRGBA32(&pixels, &w, &h);
    for (int frame = 0; frame < 3; frame++) {
        ImGui::NewFrame();
        ImGui::Begin("Hello");
        ImGui::Text("ImGui %s build OK", ImGui::GetVersion());
        ImGui::Button("Click");
        ImGui::End();
        ImGui::Render();
        ImDrawData* dd = ImGui::GetDrawData();
        printf("frame %d: valid=%d vtx=%d idx=%d\n", frame, dd->Valid, dd->TotalVtxCount, dd->TotalIdxCount);
    }
    printf("ImGui %s OK\n", ImGui::GetVersion());
    ImGui::DestroyContext();
    return 0;
}
