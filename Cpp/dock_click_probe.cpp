// Ground-truth probe: dock tab bar X button hover + click, mirrors CSharp/DockProbe phase5.
#include "imgui.h"
#include "imgui_internal.h"
#include <stdio.h>

static ImGuiID g_dockspaceId;
static ImGuiID g_leftId;
static bool s_toolOpen = true, s_tool2Open = true;

static void TwoToolFrame(bool two_tabs, bool dock_to_central = false)
{
    ImGui::NewFrame();

    ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize
        | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoBackground;
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
    const ImGuiViewport* viewPort = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos(viewPort->WorkPos, ImGuiCond_Always);
    ImGui::SetNextWindowSize(ImVec2(viewPort->Size.x, viewPort->Size.y - 14), ImGuiCond_Always);
    ImGui::SetNextWindowViewport(viewPort->ID);
    if (ImGui::Begin("Dockspace", nullptr, window_flags))
    {
        g_dockspaceId = ImGui::GetID("MyDockSpace");

        if (dock_to_central)
        {
            ImGui::DockBuilderRemoveNode(g_dockspaceId);
            ImGui::DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlags_DockSpace);
            ImGui::DockBuilderSetNodeSize(g_dockspaceId, ImVec2(1280, 706));
            ImGui::DockBuilderDockWindow("Tool", g_dockspaceId);
            ImGui::DockBuilderFinish(g_dockspaceId);
        }
        else
        {
            ImGui::DockBuilderRemoveNode(g_dockspaceId);
            ImGui::DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlags_DockSpace);
            ImGui::DockBuilderSetNodeSize(g_dockspaceId, ImVec2(1280, 706));
            ImGuiID right;
            g_leftId = ImGui::DockBuilderSplitNode(g_dockspaceId, ImGuiDir_Left, 0.25f, nullptr, &right);
            ImGui::DockBuilderDockWindow("Tool", g_leftId);
            if (two_tabs)
                ImGui::DockBuilderDockWindow("Tool2", g_leftId);
            ImGui::DockBuilderFinish(g_dockspaceId);
        }

        ImGui::DockSpace(g_dockspaceId, ImVec2(0, 0), ImGuiDockNodeFlags_PassthruCentralNode);
    }
    ImGui::End();
    ImGui::PopStyleVar(1);

    ImGui::Begin("Tool", &s_toolOpen);
    ImGui::Text("tool content");
    ImGui::End();

    if (two_tabs)
    {
        ImGui::Begin("Tool2", &s_tool2Open);
        ImGui::Text("tool2 content");
        ImGui::End();
    }

    ImGui::Render();
}

static void RunClickPhase(bool two_tabs)
{
    printf("=== phase5 click (%s) ===\n", two_tabs ? "2 tabs" : "1 tab");
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2(1280, 720);
    io.DeltaTime = 1.0f / 60.0f;
    io.IniFilename = nullptr;
    io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
    unsigned char* px; int tw, th, tbpp;
    io.Fonts->GetTexDataAsRGBA32(&px, &tw, &th, &tbpp);
    io.Fonts->SetTexID((ImTextureID)(intptr_t)1);
    s_toolOpen = true; s_tool2Open = true;

    for (int frame = 0; frame < 3; frame++)
    {
        TwoToolFrame(two_tabs);
        ImGui::GetIO().MousePos = ImVec2(640, 400);
    }

    ImGuiContext* g = ImGui::GetCurrentContext();
    ImGuiDockNode* node = ImGui::DockContextFindNodeByID(g, g_leftId);
    ImGuiWindow* host = node ? node->HostWindow : nullptr;
    ImGuiTabBar* bar = node ? node->TabBar : nullptr;
    ImGuiID closeId = host ? host->GetID("#CLOSE") : 0;
    ImGuiID titleBarId = host ? host->GetID("#TITLEBAR") : 0;
    // close button pos per DockNodeCalcTabBarLayout: (title.Max.x - border - pad - fontsize, min.y + pad.y)
    ImVec2 close_pos(node ? node->Pos.x + node->Size.x - 1.0f - 8.0f - ImGui::GetFontSize() : 0.0f,
                     node ? node->Pos.y + ImGui::GetStyle().FramePadding.y : 0.0f);
    printf("host=%s id=0x%08X closeId=0x%08X titleBarId=0x%08X closePos=(%.0f,%.0f) nodeHasClose=%d visHasClose=%d barMaxX=%.0f\n",
        host ? host->Name : "null", host ? host->ID : 0, closeId, titleBarId, close_pos.x, close_pos.y,
        node ? node->HasCloseButton : -1,
        (node && node->VisibleWindow) ? node->VisibleWindow->HasCloseButton : -1,
        bar ? bar->BarRect.Max.x : -1.0f);
    if (host) printf("IDStack depth=%d back=0x%08X\n", host->IDStack.Size, host->IDStack.back());
    if (bar)
    {
        printf("tabs=[");
        for (int i = 0; i < bar->Tabs.size(); i++) printf("0x%08X,", bar->Tabs[i].ID);
        printf("] toolId=0x%08X tool2Id=0x%08X\n", ImGui::FindWindowByName("Tool")->ID,
            ImGui::FindWindowByName("Tool2") ? ImGui::FindWindowByName("Tool2")->ID : 0);
    }

    // sweep
    // capture live close id: hover the X spot; the stable hover there is the close button
    TwoToolFrame(two_tabs);
    ImGui::GetIO().MousePos = ImVec2(309.0f, 9.0f);
    TwoToolFrame(two_tabs);
    ImGui::GetIO().MousePos = ImVec2(309.0f, 9.0f);
    g = ImGui::GetCurrentContext();
    ImGuiID liveCloseId = g->HoveredId;
    printf("liveCloseId=0x%08X (between-frames GetID=0x%08X)\n", liveCloseId, closeId);

    int foundX = -1, foundY = -1;
    for (int x = (int)bar->BarRect.Max.x + 20; x >= (int)bar->BarRect.Max.x - 10 && foundX < 0; x--)
    {
        for (int y = 3; y <= 15; y += 2)
        {
            TwoToolFrame(two_tabs);
            ImGui::GetIO().MousePos = ImVec2((float)x, (float)y);
            TwoToolFrame(two_tabs); // consume the position set above (mouse is applied at next NewFrame)
            g = ImGui::GetCurrentContext();
            printf("  spot (%d,%d): hoveredId=0x%08X win=%s\n", x, y, g->HoveredId, g->HoveredWindow ? g->HoveredWindow->Name : "null");
            if (liveCloseId != 0 && g->HoveredId == liveCloseId) { foundX = x; foundY = y; break; }
        }
    }
    printf("hover scan: %s at (%d,%d)\n", foundX >= 0 ? "FOUND" : "NOT FOUND", foundX, foundY);

    if (foundX >= 0)
    {
        for (int hold = 0; hold < 2; hold++)
        {
            TwoToolFrame(two_tabs);
            ImGui::GetIO().MousePos = ImVec2((float)foundX, (float)foundY);
            ImGui::GetIO().MouseDown[0] = true;
        }
        g = ImGui::GetCurrentContext();
        printf("press held: activeId=0x%08X (expect 0x%08X) open=%d/%d\n", g->ActiveId, liveCloseId, s_toolOpen, s_tool2Open);

        TwoToolFrame(two_tabs);
        ImGui::GetIO().MousePos = ImVec2((float)foundX, (float)foundY);
        ImGui::GetIO().MouseDown[0] = false;

        for (int frame = 0; frame < 4; frame++)
        {
            TwoToolFrame(two_tabs);
            ImGui::GetIO().MousePos = ImVec2(640, 400);
        }
        g = ImGui::GetCurrentContext();
        printf("after click: open=%d/%d\n", s_toolOpen, s_tool2Open);
        node = ImGui::DockContextFindNodeByID(g, g_leftId);
        printf("result: node windows=%d\n", node ? node->Windows.Size : -1);
    }

    ImGui::DestroyContext();
}

static void RunMenuButtonPhase(bool dock_to_central)
{
    printf("=== phase6 menu button (dock_to_central=%d) ===\n", dock_to_central ? 1 : 0);
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2(1280, 720);
    io.DeltaTime = 1.0f / 60.0f;
    io.IniFilename = nullptr;
    io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
    unsigned char* px; int tw, th, tbpp;
    io.Fonts->GetTexDataAsRGBA32(&px, &tw, &th, &tbpp);
    io.Fonts->SetTexID((ImTextureID)(intptr_t)1);
    s_toolOpen = true;

    for (int frame = 0; frame < 4; frame++)
    {
        TwoToolFrame(false, dock_to_central);
        ImGui::GetIO().MousePos = ImVec2(640, 400);
    }

    ImGuiContext* g = ImGui::GetCurrentContext();
    ImGuiDockNode* node = dock_to_central ? ImGui::DockContextFindNodeByID(g, g_dockspaceId) : ImGui::DockContextFindNodeByID(g, g_leftId);
    ImGuiWindow* host = node ? node->HostWindow : nullptr;
    printf("nodeHasWindowMenu=%d styleWinMenuPos=%d mergedFlags=%d tabBar=%d\n",
        node ? node->HasWindowMenuButton : -1, (int)ImGui::GetStyle().WindowMenuButtonPosition, node ? node->MergedFlags : -1,
        node && node->TabBar ? 1 : 0);
    if (host)
    {
        ImDrawList* dl = host->DrawList;
        int corner = 0;
        for (int i = 0; i < dl->VtxBuffer.size(); i++)
        {
            const ImDrawVert& v = dl->VtxBuffer[i];
            if (v.pos.x < 40 && v.pos.y < 20)
                corner++;
        }
        printf("host draw list: vtx=%d idx=%d cmds=%d cornerVtx=%d\n", dl->VtxBuffer.size(), dl->IdxBuffer.size(), dl->CmdBuffer.size(), corner);
    }
    ImGui::DestroyContext();
}

int main()
{
    RunClickPhase(true);
    RunClickPhase(false);
    RunMenuButtonPhase(false);
    RunMenuButtonPhase(true);
    return 0;
}
