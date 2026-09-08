using System.Text;

namespace ImGui.Probe;

public static class Program
{
    static StringBuilder log = new();
    static ImGuiID g_dockspaceId;
    static ImGuiID g_leftId;

    static void DumpNode(ImGuiContext g, string tag, ImGuiID id)
    {
        var node = ImGui.DockContextFindNodeByID(g, id);
        if (node == null) { log.AppendLine($"{tag}: node 0x{id:X8} MISSING"); return; }
        log.AppendLine($"{tag}: node 0x{id:X8} local={node.LocalFlags} shared={node.SharedFlags} merged={node.MergedFlags} " +
            $"isDockSpace={node.IsDockSpace()} isCentral={node.IsCentralNode()} pos=({node.Pos.x:0},{node.Pos.y:0}) size=({node.Size.x:0},{node.Size.y:0}) " +
            $"windows={node.Windows.Size} hostWindow={(node.HostWindow?.Name ?? "null")} alive={node.LastFrameAlive} active={node.LastFrameActive}");
    }

    static void DumpWindow(string tag, string name)
    {
        var w = ImGui.FindWindowByName(name);
        if (w == null) { log.AppendLine($"{tag}: window '{name}' MISSING"); return; }
        log.AppendLine($"{tag}: window '{name}' dockId=0x{w.DockId:X8} dockNode={(w.DockNode != null ? $"0x{w.DockNode.ID:X8}" : "null")} " +
            $"dockIsActive={w.DockIsActive} dockTabIsVisible={w.DockTabIsVisible} pos=({w.Pos.x:0},{w.Pos.y:0}) size=({w.Size.x:0},{w.Size.y:0}) hidden={w.Hidden}");
    }

    static void Frame(bool withTool, bool rebuild = false)
    {
        ImGui.NewFrame();

        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize
            | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoBackground;
        ImGui.PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
        var viewPort = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond_Always);
        ImGui.SetNextWindowSize(viewPort.Size - new ImVec2(0, 14), ImGuiCond_Always);
        ImGui.SetNextWindowViewport(viewPort.ID);
        if (ImGui.Begin("Dockspace", window_flags))
        {
            g_dockspaceId = ImGui.GetID("MyDockSpace");

            if (rebuild)
            {
                // Programmatically dock 'Tool' the way a saved layout would.
                ImGui.DockBuilderRemoveNode(g_dockspaceId);
                ImGui.DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace);
                ImGui.DockBuilderSetNodeSize(g_dockspaceId, new ImVec2(1280, 706));
                g_leftId = ImGui.DockBuilderSplitNode(g_dockspaceId, ImGuiDir_.ImGuiDir_Left, 0.25f, out _, out ImGuiID right);
                ImGui.DockBuilderDockWindow("Tool", g_leftId);
                ImGui.DockBuilderFinish(g_dockspaceId);
            }

            ImGui.DockSpace(g_dockspaceId, null, ImGuiDockNodeFlags_PassthruCentralNode);
        }
        ImGui.End();
        ImGui.PopStyleVar(3);

        if (withTool)
        {
            ImGui.SetNextWindowSize(new ImVec2(300, 200), ImGuiCond_FirstUseEver);
            ImGui.Begin("Tool");
            ImGui.Text("tool content");
            ImGui.End();
        }

        ImGui.Render();
    }

    // Interactive drag simulation: click the Tool title bar, drag onto the dockspace, release.
    static void DragFrame(ImVec2 mousePos, bool mouseDown, bool debug = false)
    {
        ImGui.NewFrame();

        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize
            | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoBackground;
        ImGui.PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0));
        var viewPort = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond_Always);
        ImGui.SetNextWindowSize(viewPort.Size - new ImVec2(0, 14), ImGuiCond_Always);
        ImGui.SetNextWindowViewport(viewPort.ID);
        if (ImGui.Begin("Dockspace", window_flags))
        {
            g_dockspaceId = ImGui.GetID("MyDockSpace");
            ImGui.DockSpace(g_dockspaceId, null, ImGuiDockNodeFlags_PassthruCentralNode);
        }
        ImGui.End();
        ImGui.PopStyleVar(1);

        ImGui.SetNextWindowSize(new ImVec2(300, 200), ImGuiCond_FirstUseEver);
        ImGui.Begin("Tool");
        ImGui.Text("tool content");
        ImGui.End();

        ImGui.Render();

        var io = ImGui.GetIO();
        io.MousePos = mousePos;
        io.MouseDown[0] = mouseDown;

        if (debug)
        {
            var g2 = ImGui.GetCurrentContext();
            var payloadStr = g2.DragDropPayload != null && g2.DragDropPayload.IsDataType(ImGuiConst.IMGUI_PAYLOAD_TYPE_WINDOW) ? "WIN" : "-";
            log.AppendLine($"f{g2.FrameCount}: mouse=({mousePos.x:0},{mousePos.y:0}) down={mouseDown} activeId=0x{g2.ActiveId:X8} " +
                $"moving={(g2.MovingWindow?.Name ?? "-")} hovered={(g2.HoveredWindow?.Name ?? "-")} ddActive={ImGui.IsDragDropActive()} payload={payloadStr} " +
                $"hoveredUnderMoving={(g2.HoveredWindowUnderMovingWindow?.Name ?? "-")}");
        }
    }

    static bool g_prevMouseDown;

    static ImGuiWindowFlags s_toolFlags;
    static bool s_toolOpen = true, s_tool2Open = true;

    // Frame with the dockspace + dockable window(s) that have close buttons.
    static void TwoToolFrame(bool twoTabs = true, bool dockToCentral = false)
    {
        ImGui.NewFrame();

        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize
            | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoBackground;
        ImGui.PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0));
        var viewPort = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond_Always);
        ImGui.SetNextWindowSize(viewPort.Size - new ImVec2(0, 14), ImGuiCond_Always);
        ImGui.SetNextWindowViewport(viewPort.ID);
        if (ImGui.Begin("Dockspace", window_flags))
        {
            g_dockspaceId = ImGui.GetID("MyDockSpace");

            if (dockToCentral)
            {
                ImGui.DockBuilderRemoveNode(g_dockspaceId);
                ImGui.DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace);
                ImGui.DockBuilderSetNodeSize(g_dockspaceId, new ImVec2(1280, 706));
                ImGui.DockBuilderDockWindow("Tool", g_dockspaceId);
                ImGui.DockBuilderFinish(g_dockspaceId);
            }
            else
            {
                ImGui.DockBuilderRemoveNode(g_dockspaceId);
                ImGui.DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace);
                ImGui.DockBuilderSetNodeSize(g_dockspaceId, new ImVec2(1280, 706));
                g_leftId = ImGui.DockBuilderSplitNode(g_dockspaceId, ImGuiDir_.ImGuiDir_Left, 0.25f, out _, out ImGuiID right);
                ImGui.DockBuilderDockWindow("Tool", g_leftId);
                if (twoTabs)
                    ImGui.DockBuilderDockWindow("Tool2", g_leftId);
                ImGui.DockBuilderFinish(g_dockspaceId);
            }

            ImGui.DockSpace(g_dockspaceId, null, ImGuiDockNodeFlags_PassthruCentralNode);
        }
        ImGui.End();
        ImGui.PopStyleVar(1);

        ImGui.Begin("Tool", ref s_toolOpen, s_toolFlags);
        ImGui.Text("tool content");
        ImGui.End();

        if (twoTabs)
        {
            ImGui.Begin("Tool2", ref s_tool2Open, s_toolFlags);
            ImGui.Text("tool2 content");
            ImGui.End();
        }

        ImGui.Render();
    }

    // === Phase 4: hover-sweep the dock tab bar looking for ID conflicts ===
    static void RunConflictPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        s_toolFlags = 0;

        for (int frame = 0; frame < 3; frame++)
        {
            TwoToolFrame();
            var io2 = ImGui.GetIO();
            io2.MousePos = new ImVec2(640, 400);
        }

        var g = ImGui.GetCurrentContext();
        var node = ImGui.DockContextFindNodeByID(g, g_leftId);
        var bar = node?.TabBar;
        log.AppendLine($"=== phase4: left node TabBar={(bar != null ? "yes" : "no")} node={g_leftId:X8} ===");
        if (bar != null)
            log.AppendLine($"barRect=({bar.BarRect.Min.x:0},{bar.BarRect.Min.y:0})-({bar.BarRect.Max.x:0},{bar.BarRect.Max.y:0})");

        // sweep the tab bar right end (X button area); hold each spot 2 frames; report conflicts
        for (int x = (int)(bar?.BarRect.Max.x ?? 320) + 18; x >= 210; x -= 2)
        {
            foreach (int y in new[] { 5, 9, 13 })
            {
                for (int hold = 0; hold < 2; hold++)
                {
                    TwoToolFrame();
                    var io2 = ImGui.GetIO();
                    io2.MousePos = new ImVec2(x, y);
                }

                g = ImGui.GetCurrentContext();
                if (g.HoveredIdPreviousFrameItemCount > 1)
                    log.AppendLine($"CONFLICT at ({x},{y}): count={g.HoveredIdPreviousFrameItemCount} id=0x{g.HoveredIdPreviousFrame:X8}");
            }
        }
        log.AppendLine("=== phase4 sweep done ===");

        ImGui.DestroyContext();
    }

    // === Phase 5: hover + click the dock tab bar X button (host_window #CLOSE) ===
    static void RunClickPhase(StringBuilder log, bool twoTabs)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        s_toolFlags = 0;
        s_toolOpen = true;
        s_tool2Open = true;

        for (int frame = 0; frame < 3; frame++)
        {
            TwoToolFrame(twoTabs);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }

        var g = ImGui.GetCurrentContext();
        var node = ImGui.DockContextFindNodeByID(g, g_leftId);
        var host = node?.HostWindow;
        var bar = node?.TabBar;
        ImGuiID closeId = host != null ? host.GetID("#CLOSE") : 0;
        ImGuiID titleBarId = host != null ? host.GetID("#TITLEBAR") : 0;
        ImGui.DockNodeCalcTabBarLayout(node, out _, out _, out _, out ImVec2 closePos);
        log.AppendLine($"=== phase5 click ({(twoTabs ? "2 tabs" : "1 tab")}): node={g_leftId:X8} host={(host?.Name ?? "null")} " +
            $"closeId=0x{closeId:X8} titleBarId=0x{titleBarId:X8} closePos=({closePos.x:0},{closePos.y:0}) " +
            $"nodeHasClose={(node != null ? node.HasCloseButton.ToString() : "?")} " +
            $"visHasClose={(node?.VisibleWindow != null ? node.VisibleWindow.HasCloseButton.ToString() : "?")} " +
            $"barMaxX={(bar != null ? bar.BarRect.Max.x.ToString("0") : "?")} ===");

        // capture live close id: hover the X spot; the stable hover there is the close button
        TwoToolFrame(twoTabs);
        ImGui.GetIO().MousePos = new ImVec2(309, 9);
        TwoToolFrame(twoTabs);
        ImGui.GetIO().MousePos = new ImVec2(309, 9);
        g = ImGui.GetCurrentContext();
        ImGuiID liveCloseId = g.HoveredId;
        log.AppendLine($"liveCloseId=0x{liveCloseId:X8} (between-frames GetID=0x{closeId:X8})");

        // sweep the right end of the tab bar (past the tab list) for the close button hover
        int foundX = -1, foundY = -1;
        if (bar != null)
        {
            for (int x = (int)bar.BarRect.Max.x + 20; x >= (int)bar.BarRect.Max.x - 10 && foundX < 0; x--)
            {
                foreach (int y in new[] { 3, 5, 7, 9, 11, 13, 15 })
                {
                    TwoToolFrame(twoTabs);
                    ImGui.GetIO().MousePos = new ImVec2(x, y);
                    TwoToolFrame(twoTabs); // consume the position set above (mouse is applied at next NewFrame)
                    g = ImGui.GetCurrentContext();
                    log.AppendLine($"  spot ({x},{y}): hoveredId=0x{g.HoveredId:X8}");
                    if (liveCloseId != 0 && g.HoveredId == liveCloseId)
                    {
                        foundX = x;
                        foundY = y;
                        break;
                    }
                }
            }
        }
        if (bar != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < bar.Tabs.Size; i++)
                sb.Append($"0x{bar.Tabs[i].ID:X8},");
            log.AppendLine($"  hostId=0x{host.ID:X8} toolId=0x{ImGui.FindWindowByName("Tool")?.ID ?? 0:X8} tool2Id=0x{ImGui.FindWindowByName("Tool2")?.ID ?? 0:X8} tabs=[{sb}]");
            log.AppendLine($"  IDStack depth={host.IDStack.Size} back=0x{host.IDStack.back():X8} windowId=0x{host.ID:X8}");
            // recompute GetID during a live frame (inside Begin/End of host window context is different; this is between frames)
            log.AppendLine($"  probeGetIdClose=0x{host.GetID("#CLOSE"):X8} probeGetIdTitle=0x{host.GetID("#TITLEBAR"):X8}");
        }
        log.AppendLine($"hover scan: {(foundX >= 0 ? $"FOUND at ({foundX},{foundY})" : "NOT FOUND; lastHoveredId=0x" + g.HoveredId.ToString("X8") + $" (titleBar=0x{titleBarId:X8}) hoveredWin=" + (g.HoveredWindow?.Name ?? "null"))}");

        if (foundX >= 0)
        {
            for (int hold = 0; hold < 2; hold++)
            {
                TwoToolFrame(twoTabs);
                ImGui.GetIO().MousePos = new ImVec2(foundX, foundY);
                ImGui.GetIO().MouseDown[0] = true;
            }
            g = ImGui.GetCurrentContext();
            log.AppendLine($"press held: activeId=0x{g.ActiveId:X8} (expect 0x{liveCloseId:X8}) wantCloseAll={(node != null ? node.WantCloseAll.ToString() : "?")} open={s_toolOpen}/{s_tool2Open}");

            TwoToolFrame(twoTabs);
            ImGui.GetIO().MousePos = new ImVec2(foundX, foundY);
            ImGui.GetIO().MouseDown[0] = false;

            for (int frame = 0; frame < 4; frame++)
            {
                TwoToolFrame(twoTabs);
                ImGui.GetIO().MousePos = new ImVec2(640, 400);
            }
            g = ImGui.GetCurrentContext();
            log.AppendLine($"after click: open={s_toolOpen}/{s_tool2Open}");
            node = ImGui.DockContextFindNodeByID(g, g_leftId);
            log.AppendLine($"result: node windows={(node != null ? node.Windows.Size.ToString() : "node gone")} wantCloseAll={(node != null ? node.WantCloseAll.ToString() : "?")}");
            DumpWindow("click", "Tool");
            DumpWindow("click", "Tool2");
        }

        ImGui.DestroyContext();
    }

    // === Phase 6: dump title-bar draw data (window menu button '<>' presence) ===
    static void RunMenuButtonPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        s_toolFlags = 0;
        s_toolOpen = true;

        for (int frame = 0; frame < 4; frame++)
        {
            TwoToolFrame(twoTabs: false);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }

        var g = ImGui.GetCurrentContext();
        var node = ImGui.DockContextFindNodeByID(g, g_leftId);
        var host = node?.HostWindow;
        log.AppendLine($"=== phase6 menu button: nodeHasWindowMenu={(node != null ? node.HasWindowMenuButton.ToString() : "?")} " +
            $"styleWinMenuPos={g.Style.WindowMenuButtonPosition} mergedFlags={node?.MergedFlags} ===");

        if (host != null)
        {
            var dl = host.DrawList;
            int vtx = dl.VtxBuffer.Size;
            log.AppendLine($"host draw list: vtx={vtx} idx={dl.IdxBuffer.Size} cmds={dl.CmdBuffer.Size}");
            // dump vertices in the top-left title bar corner (menu button zone)
            var seen = new HashSet<string>();
            for (int i = 0; i < vtx; i++)
            {
                var v = dl.VtxBuffer[i];
                if (v.pos.x < 40 && v.pos.y < 20)
                    seen.Add($"({v.pos.x:0.0},{v.pos.y:0.0})");
            }
            log.AppendLine($"titlebar corner vertices: {string.Join(" ", seen.OrderBy(s => s))}");
        }
        DumpWindow("menu", "Tool");

        ImGui.DestroyContext();
    }

    // === Phase 7: click the '<>' window menu button, check the #WindowMenu popup ===
    static void RunMenuClickPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        s_toolFlags = 0;
        s_toolOpen = true;

        for (int frame = 0; frame < 4; frame++)
        {
            TwoToolFrame(twoTabs: false);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }

        // menu button center ~ (r.Min.x + fontsize/2, pad + fontsize/2) = (15,12); hover then click
        for (int frame = 0; frame < 2; frame++)
        {
            TwoToolFrame(false);
            ImGui.GetIO().MousePos = new ImVec2(15, 12);
        }
        var g = ImGui.GetCurrentContext();
        log.AppendLine($"=== phase7 menu click: hoveredId=0x{g.HoveredId:X8} (collapse expect 0x{ImGui.FindWindowByName("Dockspace/DockSpace_382E4429")?.GetID("#COLLAPSE") ?? 0:X8}) ===");

        TwoToolFrame(false);
        ImGui.GetIO().MousePos = new ImVec2(15, 12);
        ImGui.GetIO().MouseDown[0] = true;
        TwoToolFrame(false);
        ImGui.GetIO().MousePos = new ImVec2(15, 12);
        ImGui.GetIO().MouseDown[0] = true;
        TwoToolFrame(false);
        ImGui.GetIO().MousePos = new ImVec2(15, 12);
        ImGui.GetIO().MouseDown[0] = false;
        for (int frame = 0; frame < 3; frame++)
        {
            TwoToolFrame(false);
            ImGui.GetIO().MousePos = new ImVec2(15, 12);
        }

        g = ImGui.GetCurrentContext();
        bool popupOpen = g.OpenPopupStack.Size > g.BeginPopupStack.Size;
        log.AppendLine($"popup stack: open={g.OpenPopupStack.Size} begin={g.BeginPopupStack.Size} popupOpen={popupOpen}");
        for (int i = 0; i < g.OpenPopupStack.Size; i++)
            log.AppendLine($"  openPopup[{i}] popupId=0x{g.OpenPopupStack[i].PopupId:X8} window={(g.OpenPopupStack[i].Window?.Name ?? "null")}");
        for (int i = 0; i < g.Windows.Size; i++)
        {
            var w = g.Windows[i];
            if (w.Name != null && w.Name.Contains("Popup") && w.LastFrameActive >= g.FrameCount - 1)
            {
                int vtx = w.DrawList.VtxBuffer.Size;
                log.AppendLine($"popup window '{w.Name}' hidden={w.Hidden} activeFrame={w.LastFrameActive} (now {g.FrameCount}) pos=({w.Pos.x:0},{w.Pos.y:0}) size=({w.Size.x:0},{w.Size.y:0}) drawVtx={vtx}");
            }
        }
        var popupWin = ImGui.FindWindowByName("#WindowMenu"); // popup windows are named "Popupxxxxxxxx##WindowMenu"? dump matching
        for (int i = 0; i < g.Windows.Size; i++)
        {
            var w = g.Windows[i];
            if (w.Name != null && w.Name.Contains("WindowMenu"))
                log.AppendLine($"popup window '{w.Name}' hidden={w.Hidden} active={w.LastFrameActive} pos=({w.Pos.x:0},{w.Pos.y:0}) size=({w.Size.x:0},{w.Size.y:0})");
        }
        if (popupWin != null)
            log.AppendLine($"'#WindowMenu' window exists: hidden={popupWin.Hidden} active={popupWin.LastFrameActive}");

        ImGui.DestroyContext();
    }

    // === Phase 8: window docked into the CENTRAL node of a PassthruCentralNode dockspace ===
    static void RunCentralDockPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        s_toolFlags = 0;
        s_toolOpen = true;

        for (int frame = 0; frame < 4; frame++)
        {
            TwoToolFrame(twoTabs: false, dockToCentral: true);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }

        var g = ImGui.GetCurrentContext();
        var node = ImGui.DockContextFindNodeByID(g, g_dockspaceId);
        var host = node?.HostWindow;
        var tool = ImGui.FindWindowByName("Tool");
        log.AppendLine($"=== phase8 central dock: root node windows={(node != null ? node.Windows.Size.ToString() : "?")} " +
            $"tabBar={(node?.TabBar != null ? "yes" : "no")} hiddenTabBar={(node != null ? node.IsHiddenTabBar().ToString() : "?")} " +
            $"tool.dockIsActive={(tool != null ? tool.DockIsActive.ToString() : "?")} tool.dockTabIsVisible={(tool != null ? tool.DockTabIsVisible.ToString() : "?")} ===");
        if (node?.TabBar != null)
            log.AppendLine($"tabBar rect=({node.TabBar.BarRect.Min.x:0},{node.TabBar.BarRect.Min.y:0})-({node.TabBar.BarRect.Max.x:0},{node.TabBar.BarRect.Max.y:0})");

        if (host != null)
        {
            var dl = host.DrawList;
            int corner = 0;
            for (int i = 0; i < dl.VtxBuffer.Size; i++)
            {
                var v = dl.VtxBuffer[i];
                if (v.pos.x < 40 && v.pos.y < 20)
                    corner++;
            }
            log.AppendLine($"host draw list vtx={dl.VtxBuffer.Size} corner(<40,<20) vtx={corner}");
        }

        ImGui.DestroyContext();
    }

    // === Phase 9: ini round-trip — dock, save, reload, verify dock tree/tab bar/menu button restored ===
    static void RunIniRoundTripPhase(StringBuilder log)
    {
        string ini;
        ImGui.CreateContext();
        {
            ImGuiIO io = ImGui.GetIO();
            io.DisplaySize = new ImVec2(1280, 720);
            io.DeltaTime = 1.0f / 60.0f;
            io.IniFilename = null;
            io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
            io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
            io.Fonts.SetTexID(1);
            s_toolFlags = 0;
            s_toolOpen = true;

            for (int frame = 0; frame < 4; frame++)
            {
                TwoToolFrame(twoTabs: false);
                ImGui.GetIO().MousePos = new ImVec2(640, 400);
            }
            ini = ImGui.SaveIniSettingsToMemory(out int _);
        }
        ImGui.DestroyContext();

        // reload in a fresh context
        ImGui.CreateContext();
        {
            ImGuiIO io = ImGui.GetIO();
            io.DisplaySize = new ImVec2(1280, 720);
            io.DeltaTime = 1.0f / 60.0f;
            io.IniFilename = null;
            io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
            io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
            io.Fonts.SetTexID(1);
            ImGui.LoadIniSettingsFromMemory(ini);

            for (int frame = 0; frame < 4; frame++)
            {
                TwoToolFrame(twoTabs: false);
                ImGui.GetIO().MousePos = new ImVec2(640, 400);
            }

            var g = ImGui.GetCurrentContext();
            var node = ImGui.DockContextFindNodeByID(g, g_leftId);
            var tool = ImGui.FindWindowByName("Tool");
            log.AppendLine($"=== phase9 ini round-trip ===");
            log.AppendLine($"loaded ini contains DockId={ini.Contains("DockId=")} SplitX={ini.Contains("Split=X")}");
            log.AppendLine($"tool.dockId=0x{tool?.DockId ?? 0:X8} dockIsActive={(tool != null ? tool.DockIsActive.ToString() : "?")} dockTabIsVisible={(tool != null ? tool.DockTabIsVisible.ToString() : "?")}");
            log.AppendLine($"left node: windows={(node != null ? node.Windows.Size.ToString() : "?")} tabBar={(node?.TabBar != null)} hasWindowMenu={(node != null ? node.HasWindowMenuButton.ToString() : "?")}");

            // does the tab bar still show the menu button + X after reload?
            if (node?.TabBar != null && node.HostWindow != null)
            {
                var dl = node.HostWindow.DrawList;
                int corner = 0;
                for (int v = 0; v < dl.VtxBuffer.Size; v++)
                    if (dl.VtxBuffer[v].pos.x < 40 && dl.VtxBuffer[v].pos.y < 20) corner++;
                log.AppendLine($"host cornerVtx={corner} (menu button region drawn)");
            }
            DumpWindow("ini", "Tool");
        }
        ImGui.DestroyContext();
    }

    static bool s_menuBarFrameContents; // menus actually submitted?

    static void AppReplicaFrame(bool setup, bool withWindowA = true, bool withWindowB = true)
    {
        ImGui.NewFrame();

        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize
            | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoBackground;
        ImGui.PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
        var viewPort = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond_Always);
        ImGui.SetNextWindowSize(viewPort.Size - new ImVec2(0, 14), ImGuiCond_Always);
        ImGui.SetNextWindowViewport(viewPort.ID);
        if (ImGui.Begin("Dockspace", window_flags))
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("命令"))
                    ImGui.EndMenu();
                if (ImGui.BeginMenu("窗口"))
                    ImGui.EndMenu();
                ImGui.EndMainMenuBar();
            }

            g_dockspaceId = ImGui.GetID("MyDockSpace");
            if (setup)
            {
                ImGui.DockBuilderRemoveNode(g_dockspaceId);
                ImGui.DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace);
                ImGui.DockBuilderSetNodeSize(g_dockspaceId, new ImVec2(1280, 706));
                g_leftId = ImGui.DockBuilderSplitNode(g_dockspaceId, ImGuiDir_Left, 0.25f, out _, out ImGuiID right);
                ImGui.DockBuilderDockWindow("A", g_leftId);
                ImGui.DockBuilderFinish(g_dockspaceId);
            }
            ImGui.DockSpace(g_dockspaceId, null, ImGuiDockNodeFlags_PassthruCentralNode);
        }
        ImGui.End();
        ImGui.PopStyleVar(3);

        if (withWindowA)
        {
            ImGui.SetNextWindowSize(new ImVec2(248, 754), ImGuiCond_FirstUseEver);
            ImGui.Begin("A");
            ImGui.Text("A content");
            ImGui.End();
        }
        if (withWindowB)
        {
            ImGui.SetNextWindowPos(new ImVec2(600, 300), ImGuiCond_FirstUseEver);
            ImGui.SetNextWindowSize(new ImVec2(300, 200), ImGuiCond_FirstUseEver);
            ImGui.Begin("B");
            ImGui.Text("B content");
            ImGui.End();
        }

        ImGui.Render();
    }

    // App-replica frame with injected mouse state, logged each frame.
    static void AppDragFrame(ImVec2 mousePos, bool mouseDown, bool debug = false)
    {
        AppReplicaFrame(setup: false);
        var io = ImGui.GetIO();
        io.MousePos = mousePos;
        io.MouseDown[0] = mouseDown;

        if (debug)
        {
            var g2 = ImGui.GetCurrentContext();
            var payloadStr = g2.DragDropPayload != null && g2.DragDropPayload.IsDataType(ImGuiConst.IMGUI_PAYLOAD_TYPE_WINDOW) ? "WIN" : "-";
            log.AppendLine($"f{g2.FrameCount}: mouse=({mousePos.x:0},{mousePos.y:0}) down={mouseDown} activeId=0x{g2.ActiveId:X8} " +
                $"moving={(g2.MovingWindow?.Name ?? "-")} hovered={(g2.HoveredWindow?.Name ?? "-")} ddActive={ImGui.IsDragDropActive()} payload={payloadStr} " +
                $"hoveredUnderMoving={(g2.HoveredWindowUnderMovingWindow?.Name ?? "-")} " +
                $"acceptPrev=0x{g2.DragDropAcceptIdPrev:X8} acceptCurr=0x{g2.DragDropAcceptIdCurr:X8}");
        }
    }

    // === Phase 10: drag floating window B onto the node that already hosts docked window A ===
    static void RunAppReplicaDragPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        ImGui.GetCurrentContext().DebugLogFlags = ImGuiDebugLogFlags_.ImGuiDebugLogFlags_EventDocking
                         | ImGuiDebugLogFlags_.ImGuiDebugLogFlags_OutputToTTY;

        for (int frame = 0; frame < 3; frame++)
            AppReplicaFrame(setup: true);

        var g = ImGui.GetCurrentContext();
        var nodeA = ImGui.DockContextFindNodeByID(g, g_leftId);
        log.AppendLine($"=== phase10 setup: nodeA=0x{g_leftId:X8} windows={nodeA?.Windows.Size} hostA={(ImGui.FindWindowByName("A")?.DockNode?.HostWindow?.Name ?? "null")} ===");

        // grab B's title bar (B floating at 600,300 size 300x200)
        AppDragFrame(new ImVec2(700, 310), false);
        AppDragFrame(new ImVec2(700, 310), true, debug: true);
        AppDragFrame(new ImVec2(700, 310), true, debug: true);
        // drag in steps toward the middle of A's node (x 0..320, y 20..720)
        for (int step = 1; step <= 8; step++)
        {
            float x = 700 - step * 68;
            float y = 310 + step * 12;
            AppDragFrame(new ImVec2(x, y), true, debug: true);
        }
        // hover A's node interior for several frames — preview should persist here
        for (int hold = 0; hold < 4; hold++)
            AppDragFrame(new ImVec2(160, 400), true, debug: true);
        // release
        AppDragFrame(new ImVec2(160, 400), false, debug: true);
        for (int frame = 0; frame < 3; frame++)
            AppDragFrame(new ImVec2(160, 400), false);

        g = ImGui.GetCurrentContext();
        nodeA = ImGui.DockContextFindNodeByID(g, g_leftId);
        log.AppendLine($"=== phase10 result: nodeA windows={nodeA?.Windows.Size} root children dump ===");
        DumpNode(g, "p10-root", g_dockspaceId);
        DumpNode(g, "p10-left", g_leftId);
        DumpWindow("p10", "B");
        DumpWindow("p10", "A");

        ImGui.DestroyContext();
    }

    // === Phase 11: click the '<>' menu button of node A in the app replica (with main menu bar above) ===
    static void RunAppReplicaMenuPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);

        for (int frame = 0; frame < 4; frame++)
        {
            AppReplicaFrame(setup: true);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }

        var g = ImGui.GetCurrentContext();
        var nodeA = ImGui.DockContextFindNodeByID(g, g_leftId);
        var host = nodeA?.HostWindow;
        log.AppendLine($"=== phase11 menu: nodeA=0x{g_leftId:X8} host={(host?.Name ?? "null")} hasWindowMenu={(nodeA != null ? nodeA.HasWindowMenuButton.ToString() : "?")} " +
            $"hiddenTabBar={(nodeA != null ? nodeA.IsHiddenTabBar().ToString() : "?")} ===");
        if (nodeA?.TabBar != null)
            log.AppendLine($"tabBar rect=({nodeA.TabBar.BarRect.Min.x:0},{nodeA.TabBar.BarRect.Min.y:0})-({nodeA.TabBar.BarRect.Max.x:0},{nodeA.TabBar.BarRect.Max.y:0})");

        // menu button: use the layout-computed position
        ImGui.DockNodeCalcTabBarLayout(nodeA, out _, out _, out ImVec2 menuBtnPos, out ImVec2 closeBtnPos);
        float fs = ImGui.GetFontSize();
        float bx = menuBtnPos.x + fs / 2, by = menuBtnPos.y + fs / 2;
        log.AppendLine($"menuBtnPos=({menuBtnPos.x:0},{menuBtnPos.y:0}) fontSize={fs:0} closeBtnPos=({closeBtnPos.x:0},{closeBtnPos.y:0})");
        log.AppendLine($"clicking at ({bx:0},{by:0})");

        // hover sweep across the tab bar row to see which item owns each x
        if (Environment.GetEnvironmentVariable("PROBE_SWEEP") == "1")
        {
            for (int x = 2; x <= 40; x += 4)
            {
                AppReplicaFrame(setup: false);
                ImGui.GetIO().MousePos = new ImVec2(x, menuBtnPos.y + fs / 2);
                AppReplicaFrame(setup: false);
                ImGui.GetIO().MousePos = new ImVec2(x, menuBtnPos.y + fs / 2);
                g = ImGui.GetCurrentContext();
                log.AppendLine($"  sweep x={x}: hoveredId=0x{g.HoveredId:X8} hoveredWin={(g.HoveredWindow?.Name ?? "null")}");
            }
        }

        for (int frame = 0; frame < 2; frame++)
        {
            AppReplicaFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(bx, by);
        }
        g = ImGui.GetCurrentContext();
        log.AppendLine($"hover: hoveredId=0x{g.HoveredId:X8} hoveredWin={(g.HoveredWindow?.Name ?? "null")} underMoving={(g.HoveredWindowUnderMovingWindow?.Name ?? "null")}");

        AppReplicaFrame(setup: false);
        ImGui.GetIO().MousePos = new ImVec2(bx, by);
        ImGui.GetIO().MouseDown[0] = true;
        g = ImGui.GetCurrentContext();
        log.AppendLine($"press1: activeId=0x{g.ActiveId:X8} hoveredId=0x{g.HoveredId:X8} openPopup={g.OpenPopupStack.Size} activeWin={(g.ActiveIdWindow?.Name ?? "null")}");
        AppReplicaFrame(setup: false);
        ImGui.GetIO().MousePos = new ImVec2(bx, by);
        ImGui.GetIO().MouseDown[0] = true;
        g = ImGui.GetCurrentContext();
        var nodeA2 = ImGui.DockContextFindNodeByID(g, g_leftId);
        log.AppendLine($"press2: activeId=0x{g.ActiveId:X8} hoveredId=0x{g.HoveredId:X8} openPopup={g.OpenPopupStack.Size} activeWin={(g.ActiveIdWindow?.Name ?? "null")} " +
            $"mouseClicked0={g.IO.MouseClicked[0]} mouseDown0={g.IO.MouseDown[0]} hoveredH={g.HoveredWindow?.Name} clickPos=({g.IO.MouseClickedPos[0].x:0},{g.IO.MouseClickedPos[0].y:0}) " +
            $"hostActive={nodeA2?.HostWindow?.LastFrameActive}(now {g.FrameCount}) hostSkip={nodeA2?.HostWindow?.SkipItems} nodeActive={nodeA2?.LastFrameActive} tabBar={(nodeA2?.TabBar != null)}");
        AppReplicaFrame(setup: false);
        ImGui.GetIO().MousePos = new ImVec2(bx, by);
        ImGui.GetIO().MouseDown[0] = false;
        g = ImGui.GetCurrentContext();
        log.AppendLine($"release: activeId=0x{g.ActiveId:X8} hoveredId=0x{g.HoveredId:X8} openPopup={g.OpenPopupStack.Size}");
        for (int i = 0; i < g.OpenPopupStack.Size; i++)
            log.AppendLine($"  openPopup[{i}] popupId=0x{g.OpenPopupStack[i].PopupId:X8} window={(g.OpenPopupStack[i].Window?.Name ?? "null")}");
        for (int frame = 0; frame < 3; frame++)
        {
            AppReplicaFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(bx, by);
            g = ImGui.GetCurrentContext();
            log.AppendLine($"settle{frame}: openPopup={g.OpenPopupStack.Size} beginPopup={g.BeginPopupStack.Size} hoveredWin={(g.HoveredWindow?.Name ?? "null")} navWin={(g.NavWindow?.Name ?? "null")}");
        }

        g = ImGui.GetCurrentContext();
        bool popupOpen = g.OpenPopupStack.Size > g.BeginPopupStack.Size;
        log.AppendLine($"popup stack: open={g.OpenPopupStack.Size} begin={g.BeginPopupStack.Size} popupOpen={popupOpen}");
        for (int i = 0; i < g.OpenPopupStack.Size; i++)
            log.AppendLine($"  openPopup[{i}] popupId=0x{g.OpenPopupStack[i].PopupId:X8} window={(g.OpenPopupStack[i].Window?.Name ?? "null")}");
        for (int i = 0; i < g.Windows.Size; i++)
        {
            var w = g.Windows[i];
            if (w.Name != null && w.Name.Contains("WindowMenu"))
                log.AppendLine($"popup window '{w.Name}' hidden={w.Hidden} active={w.LastFrameActive} (now {g.FrameCount}) pos=({w.Pos.x:0},{w.Pos.y:0}) size=({w.Size.x:0},{w.Size.y:0}) drawVtx={w.DrawList.VtxBuffer.Size}");
        }

        ImGui.DestroyContext();
    }

    // === Phase 12: drag the splitter between the docked-left node and the rest ===
    static void RunSplitterResizePhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);

        for (int frame = 0; frame < 4; frame++)
            AppReplicaFrame(setup: true);

        var g = ImGui.GetCurrentContext();
        var root = ImGui.DockContextFindNodeByID(g, g_dockspaceId);
        log.AppendLine($"=== phase12 splitter resize: root=0x{g_dockspaceId:X8} splitAxis={root?.SplitAxis} " +
            $"c0=({root?.ChildNodes[0].Pos.x:0},{root?.ChildNodes[0].Pos.y:0} {root?.ChildNodes[0].Size.x:0}x{root?.ChildNodes[0].Size.y:0}) " +
            $"c1=({root?.ChildNodes[1].Pos.x:0},{root?.ChildNodes[1].Pos.y:0} {root?.ChildNodes[1].Size.x:0}x{root?.ChildNodes[1].Size.y:0}) " +
            $"winBorderHoverPad={g.WindowsBorderHoverPadding} dockingSeparatorSize={g.Style.DockingSeparatorSize} ===");
        // splitter bb for a Left split: x from c0.Max.x to c1.Min.x (2px wide), full height
        float splitX = root.ChildNodes[0].Pos.x + root.ChildNodes[0].Size.x + 1;

        void Snap(string tag)
        {
            g = ImGui.GetCurrentContext();
            var r = ImGui.DockContextFindNodeByID(g, g_dockspaceId);
            var host2 = r?.HostWindow;
            var sbIds = new StringBuilder();
            if (host2 != null)
                for (int i = 0; i < host2.IDStack.Size; i++)
                    sbIds.Append($"0x{host2.IDStack[i]:X8},");
            ImGuiID splitterExpect = ImGuiHash.ImHashStr("##Splitter", g_dockspaceId);
            log.AppendLine($"{tag}: hovered={(g.HoveredWindow?.Name ?? "-")} hoveredId=0x{g.HoveredId:X8} activeId=0x{g.ActiveId:X8} " +
                $"activeWin={(g.ActiveIdWindow?.Name ?? "-")} " +
                $"c0.w={r?.ChildNodes[0].Size.x:0} c1.w={r?.ChildNodes[1].Size.x:0} c0.pos.x={r?.ChildNodes[0].Pos.x:0} c1.pos.x={r?.ChildNodes[1].Pos.x:0} " +
                $"hostIdStack=[{sbIds}] splitterExpect=0x{splitterExpect:X8} hoveredIsSplitter={(host2 != null && g.HoveredId == ImGuiHash.ImHashStr("##Splitter", host2.IDStack.Size > 0 ? host2.IDStack.back() : g_dockspaceId))}");
        }

        // hover the splitter (x = 320..322) without pressing
        for (int frame = 0; frame < 3; frame++)
        {
            AppReplicaFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(splitX, 400);
        }
        Snap("hover");

        // press and hold
        AppReplicaFrame(setup: false);
        ImGui.GetIO().MousePos = new ImVec2(splitX, 400);
        ImGui.GetIO().MouseDown[0] = true;
        Snap("press1");
        AppReplicaFrame(setup: false);
        ImGui.GetIO().MousePos = new ImVec2(splitX, 400);
        ImGui.GetIO().MouseDown[0] = true;
        Snap("press2");

        // drag right in steps, then hold at the end position
        for (int step = 1; step <= 5; step++)
        {
            AppReplicaFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(splitX + step * 20, 400);
            ImGui.GetIO().MouseDown[0] = true;
        }
        Snap("dragged");
        for (int hold = 0; hold < 2; hold++)
        {
            AppReplicaFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(splitX + 100, 400);
            ImGui.GetIO().MouseDown[0] = true;
        }
        Snap("held");

        // release
        AppReplicaFrame(setup: false);
        ImGui.GetIO().MousePos = new ImVec2(splitX + 100, 400);
        ImGui.GetIO().MouseDown[0] = false;
        for (int frame = 0; frame < 3; frame++)
        {
            AppReplicaFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(splitX + 100, 400);
        }
        Snap("released");

        var rootAfter = ImGui.DockContextFindNodeByID(g, g_dockspaceId);
        bool resizeOk = rootAfter != null && rootAfter.ChildNodes[0].Size.x > 410 && rootAfter.ChildNodes[1].Size.x < 870;
        log.AppendLine($"phase12 verdict: {(resizeOk ? "PASS (splitter dragged, c0.w 319→" + rootAfter.ChildNodes[0].Size.x.ToString("0") + ")" : "FAIL (no resize applied)")}");

        DumpWindow("p12", "A");
        DumpNode(g, "p12-root", g_dockspaceId);
        ImGui.DestroyContext();
    }

    // App-replica frame with two windows tabbed into the SAME node (like 设置/录制 sharing one dock slot).
    static bool s_setOpen = true, s_recOpen = true;
    static ImGuiID s_tabbedId;

    static void TabbedFrame(bool setup)
    {
        ImGui.NewFrame();

        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize
            | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoBackground;
        ImGui.PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
        var viewPort = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond_Always);
        ImGui.SetNextWindowSize(viewPort.Size - new ImVec2(0, 14), ImGuiCond_Always);
        ImGui.SetNextWindowViewport(viewPort.ID);
        if (ImGui.Begin("Dockspace", window_flags))
        {
            g_dockspaceId = ImGui.GetID("MyDockSpace");
            if (setup)
            {
                ImGui.DockBuilderRemoveNode(g_dockspaceId);
                ImGui.DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace);
                ImGui.DockBuilderSetNodeSize(g_dockspaceId, new ImVec2(1280, 706));
                s_tabbedId = ImGui.DockBuilderSplitNode(g_dockspaceId, ImGuiDir_Left, 0.35f, out _, out ImGuiID right);
                ImGui.DockBuilderDockWindow("设置", s_tabbedId);
                ImGui.DockBuilderDockWindow("录制", s_tabbedId);
                ImGui.DockBuilderFinish(g_dockspaceId);
            }
            ImGui.DockSpace(g_dockspaceId, null, ImGuiDockNodeFlags_PassthruCentralNode);
        }
        ImGui.End();
        ImGui.PopStyleVar(3);

        ImGui.Begin("设置", ref s_setOpen);
        ImGui.Text("设置 content");
        ImGui.End();

        ImGui.Begin("录制", ref s_recOpen);
        ImGui.Text("录制 content");
        ImGui.End();

        ImGui.Render();
    }

    // === Phase 13: two windows docked into the same node (tabbed) — the unselected one must be hidden ===
    static void RunTabbedHiddenPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        s_setOpen = true;
        s_recOpen = true;

        for (int frame = 0; frame < 5; frame++)
        {
            TabbedFrame(setup: true);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }

        var g = ImGui.GetCurrentContext();
        var node = ImGui.DockContextFindNodeByID(g, s_tabbedId);
        log.AppendLine($"=== phase13 tabbed hidden: node={s_tabbedId:X8} windows={node?.Windows.Size} selected={(node?.TabBar != null ? $"0x{node.TabBar.SelectedTabId:X8}" : "?")} ===");
        foreach (string name in new[] { "设置", "录制" })
        {
            var w = ImGui.FindWindowByName(name);
            if (w == null) { log.AppendLine($"{name}: MISSING"); continue; }
            log.AppendLine($"{name}: dockId=0x{w.DockId:X8} dockIsActive={w.DockIsActive} dockTabIsVisible={w.DockTabIsVisible} hidden={w.Hidden} skipItems={w.SkipItems} " +
                $"canSkip={w.HiddenFramesCanSkipItems} cannotSkip={w.HiddenFramesCannotSkipItems} lastFrameActive={w.LastFrameActive} active={w.Active} " +
                $"pos=({w.Pos.x:0},{w.Pos.y:0}) size=({w.Size.x:0}x{w.Size.y:0}) vtx={w.DrawList.VtxBuffer.Size} idx={w.DrawList.IdxBuffer.Size}");
        }
        var setW = ImGui.FindWindowByName("设置");
        var recW = ImGui.FindWindowByName("录制");
        bool oneHidden = setW != null && recW != null && (setW.Hidden != recW.Hidden) && (setW.Hidden ? setW.DrawList.VtxBuffer.Size <= 0 : recW.DrawList.VtxBuffer.Size <= 0);
        log.AppendLine($"phase13 verdict: {(oneHidden ? "PASS (exactly one window hidden)" : "FAIL (both windows rendering — overlap)")}");

        // click the other tab (the non-selected one) — hidden relationship must flip
        ImGui.DockNodeCalcTabBarLayout(node, out _, out _, out _, out _);
        var tabs = node.TabBar.Tabs;
        int clickIdx = 0;
        if (tabs.Size >= 2)
        {
            if (node.TabBar.SelectedTabId == tabs[0].ID)
                clickIdx = 1;
            var tab = tabs[clickIdx];
            float cx = node.TabBar.BarRect.Min.x + tab.Offset + tab.Width * 0.5f;
            float cy = (node.TabBar.BarRect.Min.y + node.TabBar.BarRect.Max.y) * 0.5f;
            log.AppendLine($"tab[0]: id=0x{tabs[0].ID:X8} offset={tabs[0].Offset:0} width={tabs[0].Width:0} | tab[1]: id=0x{tabs[1].ID:X8} offset={tabs[1].Offset:0} width={tabs[1].Width:0} | barRect=({node.TabBar.BarRect.Min.x:0},{node.TabBar.BarRect.Min.y:0})-({node.TabBar.BarRect.Max.x:0},{node.TabBar.BarRect.Max.y:0})");
            for (int frame = 0; frame < 2; frame++)
            {
                TabbedFrame(setup: false);
                ImGui.GetIO().MousePos = new ImVec2(cx, cy);
            }
            g = ImGui.GetCurrentContext();
            log.AppendLine($"hover at ({cx:0},{cy:0}): hoveredWin={(g.HoveredWindow?.Name ?? "-")} hoveredId=0x{g.HoveredId:X8} (tab0=0x{tabs[0].ID:X8} tab1=0x{tabs[1].ID:X8})");
            TabbedFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(cx, cy);
            ImGui.GetIO().MouseDown[0] = true;
            TabbedFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(cx, cy);
            ImGui.GetIO().MouseDown[0] = true;
            TabbedFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(cx, cy);
            ImGui.GetIO().MouseDown[0] = false;
            for (int frame = 0; frame < 3; frame++)
            {
                TabbedFrame(setup: false);
                ImGui.GetIO().MousePos = new ImVec2(640, 400);
            }
            g = ImGui.GetCurrentContext();
            node = ImGui.DockContextFindNodeByID(g, s_tabbedId);
            setW = ImGui.FindWindowByName("设置");
            recW = ImGui.FindWindowByName("录制");
            bool nowSetSelected = node.TabBar.SelectedTabId == tabs[0].ID;
            bool flipped = setW != null && recW != null && (setW.Hidden != recW.Hidden) && (!setW.Hidden == nowSetSelected)
                && (setW.Hidden ? setW.DrawList.VtxBuffer.Size == 0 : recW.DrawList.VtxBuffer.Size == 0);
            log.AppendLine($"after tab[{clickIdx}] click at ({cx:0},{cy:0}): selected=0x{node?.TabBar?.SelectedTabId ?? 0:X8} tab0=0x{tabs[0].ID:X8} " +
                $"设置.hidden={setW?.Hidden} vtx={setW?.DrawList.VtxBuffer.Size} 录制.hidden={recW?.Hidden} vtx={recW?.DrawList.VtxBuffer.Size}");
            log.AppendLine($"phase13 tab-switch verdict: {(flipped ? "PASS (hidden state flipped)" : "FAIL (hidden state did not flip)")}");
        };

        ImGui.DestroyContext();
    }

    // === Phase 14: one-shot DockBuilder build (setup only on the first frame), windows must attach on subsequent frames ===
    static void RunOneShotBuilderPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);
        s_setOpen = true;
        s_recOpen = true;

        for (int frame = 0; frame < 6; frame++)
        {
            TabbedFrame(setup: frame == 0);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }

        var g = ImGui.GetCurrentContext();
        var node = ImGui.DockContextFindNodeByID(g, s_tabbedId);
        log.AppendLine($"=== phase14 one-shot builder: node={s_tabbedId:X8} windows={node?.Windows.Size} ===");
        foreach (string name in new[] { "设置", "录制" })
        {
            var w = ImGui.FindWindowByName(name);
            if (w == null) { log.AppendLine($"{name}: MISSING"); continue; }
            log.AppendLine($"{name}: dockId=0x{w.DockId:X8} dockIsActive={w.DockIsActive} dockTabIsVisible={w.DockTabIsVisible} hidden={w.Hidden} " +
                $"pos=({w.Pos.x:0},{w.Pos.y:0}) size=({w.Size.x:0}x{w.Size.y:0})");
        }
        bool attached = node != null && node.Windows.Size == 2;
        log.AppendLine($"phase14 verdict: {(attached ? "PASS (windows attached)" : "FAIL (windows never attached to node)")}");

        ImGui.DestroyContext();
    }

    // === Phase 15: mouse cursor (GetMouseCursor) over input text / resize grip / border / dock splitter ===
    static string s_cursorText = "hello";
    static ImRect s_inputRect;

    static void CursorAppFrame(bool setup)
    {
        ImGui.NewFrame();

        ImGuiWindowFlags window_flags = ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize
            | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNavFocus | ImGuiWindowFlags_NoBackground;
        ImGui.PushStyleVar(ImGuiStyleVar_WindowPadding, new ImVec2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
        var viewPort = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewPort.WorkPos, ImGuiCond_Always);
        ImGui.SetNextWindowSize(viewPort.Size - new ImVec2(0, 14), ImGuiCond_Always);
        ImGui.SetNextWindowViewport(viewPort.ID);
        if (ImGui.Begin("Dockspace", window_flags))
        {
            g_dockspaceId = ImGui.GetID("MyDockSpace");
            if (setup)
            {
                ImGui.DockBuilderRemoveNode(g_dockspaceId);
                ImGui.DockBuilderAddNode(g_dockspaceId, ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace);
                ImGui.DockBuilderSetNodeSize(g_dockspaceId, new ImVec2(1280, 706));
                g_leftId = ImGui.DockBuilderSplitNode(g_dockspaceId, ImGuiDir_Left, 0.25f, out _, out ImGuiID right);
                ImGui.DockBuilderDockWindow("A", g_leftId);
                ImGui.DockBuilderFinish(g_dockspaceId);
            }
            ImGui.DockSpace(g_dockspaceId, null, ImGuiDockNodeFlags_PassthruCentralNode);
        }
        ImGui.End();
        ImGui.PopStyleVar(3);

        ImGui.Begin("A");
        ImGui.Text("A content");
        ImGui.End();

        ImGui.SetNextWindowPos(new ImVec2(600, 300), ImGuiCond_FirstUseEver);
        ImGui.SetNextWindowSize(new ImVec2(300, 200), ImGuiCond_FirstUseEver);
        ImGui.Begin("B");
        ImGui.InputText("##probe", ref s_cursorText, 128);
        s_inputRect = ImGui.GetCurrentContext().LastItemData.Rect;
        ImGui.End();

        ImGui.Render();
    }

    static void RunCursorPhase(StringBuilder log)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);

        void Snap(string tag)
        {
            // i.e. right after the previous frame's Render() — it must still carry that frame's value.
            int cursor = ImGui.GetMouseCursor();
            var g = ImGui.GetCurrentContext();
            log.AppendLine($"{tag}: cursor={cursor} (0=Arrow 1=TextInput 4=EW 5=NESW 6=NWSE) hovered={(g.HoveredWindow?.Name ?? "-")} hoveredId=0x{g.HoveredId:X8}");
        }

        for (int frame = 0; frame < 4; frame++)
        {
            CursorAppFrame(setup: true);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }
        Snap("baseline");

        // --- input text hover (C++ sets this immediately) ---
        var inputRect = s_inputRect; // captured inside the frame, right after InputText()
        log.AppendLine($"inputRect=({inputRect.Min.x:0},{inputRect.Min.y:0})-({inputRect.Max.x:0},{inputRect.Max.y:0})");
        float ix = (inputRect.Min.x + inputRect.Max.x) * 0.5f, iy = (inputRect.Min.y + inputRect.Max.y) * 0.5f;
        for (int frame = 0; frame < 3; frame++)
        {
            CursorAppFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(ix, iy);
        }
        Snap("inputtext");

        // --- bottom-right resize grip (immediate in C++) ---
        var winB = ImGui.FindWindowByName("B");
        float gx = winB.Pos.x + winB.Size.x - 2, gy = winB.Pos.y + winB.Size.y - 2;
        for (int frame = 0; frame < 3; frame++)
        {
            CursorAppFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(gx, gy);
        }
        Snap("grip");

        // --- right border (C++ requires HoveredIdTimer > 0.15s → hold ~20 frames) ---
        float bx = winB.Pos.x + winB.Size.x - 1, by = winB.Pos.y + winB.Size.y * 0.5f;
        for (int frame = 0; frame < 25; frame++)
        {
            CursorAppFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(bx, by);
        }
        Snap("border(25f)");

        // --- dock splitter between node A and the rest (SplitterBehavior: visible after hover_visibility_delay) ---
        var root = ImGui.DockContextFindNodeByID(ImGui.GetCurrentContext(), g_dockspaceId);
        float sx = root.ChildNodes[0].Pos.x + root.ChildNodes[0].Size.x + 1;
        for (int frame = 0; frame < 5; frame++)
        {
            CursorAppFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(sx, 400);
        }
        Snap("docksplitter");

        // --- move back to empty space: must return to Arrow on the NEXT sample ---
        for (int frame = 0; frame < 3; frame++)
        {
            CursorAppFrame(setup: false);
            ImGui.GetIO().MousePos = new ImVec2(640, 400);
        }
        Snap("back-to-arrow");

        ImGui.DestroyContext();
    }

    public static void Main(string[] args)
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);

        for (int frame = 0; frame < 3; frame++)
            Frame(withTool: true);

        ImGuiContext g = ImGui.GetCurrentContext();
        log.AppendLine($"=== after 3 plain frames, FrameCount={g.FrameCount} ===");
        DumpNode(g, "plain", g_dockspaceId);
        DumpWindow("plain", "Dockspace");
        DumpWindow("plain", "Tool");

        for (int frame = 0; frame < 3; frame++)
            Frame(withTool: true, rebuild: true);

        log.AppendLine($"=== after DockBuilder dock, FrameCount={g.FrameCount} ===");
        DumpNode(g, "builder-root", g_dockspaceId);
        DumpNode(g, "builder-left", g_leftId);
        DumpWindow("builder", "Dockspace");
        DumpWindow("builder", "Tool");

        // draw list summary: does anything render?
        var drawData = ImGui.GetDrawData();
        int totalCmds = 0;
        for (int i = 0; i < drawData.CmdLists.Size; i++)
            totalCmds += drawData.CmdLists[i].CmdBuffer.Size;
        log.AppendLine($"draw lists={drawData.CmdLists.Size} total cmds={totalCmds}");

        // === Phase 3: simulate an interactive drag of the Tool title bar onto the dockspace ===
        ImGui.DestroyContext();
        ImGui.CreateContext();
        io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280, 720);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
        io.Fonts.GetTexDataAsRGBA32(out px, out tw, out th, out tbpp);
        io.Fonts.SetTexID(1);
        ImGui.GetCurrentContext().DebugLogFlags = ImGuiDebugLogFlags_.ImGuiDebugLogFlags_EventDocking
                         | ImGuiDebugLogFlags_.ImGuiDebugLogFlags_OutputToTTY;

        for (int frame = 0; frame < 2; frame++)
            Frame(withTool: true);

        // settle mouse over Tool title bar (window at 60,60, title bar ~26px)
        DragFrame(new ImVec2(200, 70), false, debug: true);
        DragFrame(new ImVec2(200, 70), true, debug: true);   // click on title bar
        // drag toward dockspace central drop square (node center 640,353)
        for (int step = 1; step <= 6; step++)
            DragFrame(new ImVec2(200 + step * 73, 70 + step * 47), true, debug: true);
        DragFrame(new ImVec2(642, 355), true, debug: true);  // hover the central drop square
        DragFrame(new ImVec2(642, 355), false, debug: true); // release

        for (int frame = 0; frame < 2; frame++)
            DragFrame(new ImVec2(642, 355), false);

        g = ImGui.GetCurrentContext();
        log.AppendLine($"=== after simulated drag-drop, FrameCount={g.FrameCount} dragDropActive={ImGui.IsDragDropActive()} ===");
        DumpNode(g, "drag", g_dockspaceId);
        DumpWindow("drag", "Tool");

        ImGui.DestroyContext();
        RunConflictPhase(log);
        RunClickPhase(log, twoTabs: true);
        RunClickPhase(log, twoTabs: false);
        RunMenuButtonPhase(log);
        RunMenuClickPhase(log);
        RunCentralDockPhase(log);
        RunIniRoundTripPhase(log);
        RunAppReplicaDragPhase(log);
        RunAppReplicaMenuPhase(log);
        RunSplitterResizePhase(log);
        RunTabbedHiddenPhase(log);
        RunOneShotBuilderPhase(log);
        RunCursorPhase(log);
        Console.WriteLine(log.ToString());
    }
}
