// Golden-data test harness: mirrors Cpp/golden_dump.cpp byte-for-byte.
// Replays the 33 deterministic scenarios against the C# port and diffs every
// vertex / index / draw command against the C++ reference dumps in GoldenData/.
using System;
using System.Text;

namespace ImGui.Tests;

public static class GoldenTest
{
    public static string GoldenDirPublic => GoldenDir;

    static string GoldenDir => System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "GoldenData");

    static StringBuilder g_out = new StringBuilder();

    static void HexFloat(float f, StringBuilder sb)
    {
        uint u = BitConverter.SingleToUInt32Bits(f);
        sb.Append(u.ToString("x8"));
    }

    static string Hex(float f)
    {
        var sb = new StringBuilder();
        HexFloat(f, sb);
        return sb.ToString();
    }

    static void DumpVec2(string name, ImVec2 v)
    {
        g_out.Append(name).Append('=').Append(Hex(v.x)).Append(',').Append(Hex(v.y)).Append('\n');
    }

    static void DumpVec4(string name, ImVec4 v)
    {
        g_out.Append(name).Append('=').Append(Hex(v.x)).Append(',').Append(Hex(v.y)).Append(',').Append(Hex(v.z)).Append(',').Append(Hex(v.w)).Append('\n');
    }

    static System.IO.StringWriter g_file;
    static string g_current;

    static void BeginScenario(string name)
    {
        g_current = name;
        g_file = new System.IO.StringWriter();
        g_out = new StringBuilder();
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280.0f, 720.0f);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null; // full determinism: never load/save window layout
        io.MousePos = new ImVec2(64.0f, 48.0f); // fixed, over the first window's first items
        // Legacy backend path: fully bake the atlas up front (deterministic glyph UVs).
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1); // emulate backend upload
        g_out.Append("# scenario=").Append(name).Append(" imgui=").Append(ImGui.GetVersion()).Append(" num=").Append(ImGui.ImGuiVersionNum).Append('\n');
        g_out.Append("ATLAS w=").Append(tw).Append(" h=").Append(th).Append(" bpp=").Append(tbpp).Append('\n');
    }

    static void EndScenario()
    {
        ImGui.DestroyContext();
        g_file = null;
        g_current = null;
    }

    static bool g_compact = false; // demo mode: skip per-vertex dump, emit FNV-1a hash of all vtx+idx bytes instead

    // FNV-1a over the bytes of a float/uint (little-endian), continuing from h.
    static uint FnvByte(uint h, uint b)
    {
        h ^= b & 0xffu; h *= 16777619u;
        return h;
    }
    static uint FnvU32(uint h, uint u)
    {
        h = FnvByte(h, u);
        h = FnvByte(h, u >> 8);
        h = FnvByte(h, u >> 16);
        h = FnvByte(h, u >> 24);
        return h;
    }
    static uint FnvFloat(uint h, float f) => FnvU32(h, BitConverter.SingleToUInt32Bits(f));

    static void DumpFrame(int frame, bool compact)
    {
        ImGuiIO io = ImGui.GetIO();
        g_out.Append("FRAME ").Append(frame).Append('\n');
        // Font atlas textures
        ImFontAtlas fonts = io.Fonts;
        ImVector<ImTextureData> texs = fonts.TexList;
        g_out.Append("TEXCOUNT ").Append(texs.Size).Append('\n');
        for (int i = 0; i < texs.Size; i++)
        {
            ImTextureData tex = texs[i];
            g_out.Append("TEX ").Append(i).Append(" w=").Append(tex.Width).Append(" h=").Append(tex.Height)
                 .Append(" bpp=").Append(tex.BytesPerPixel).Append(" status=").Append(tex.Status)
                 .Append(" texid=").Append(tex.TexID).Append('\n');
        }
        ImDrawData dd = ImGui.GetDrawData();
        g_out.Append("DD valid=").Append(dd.Valid ? 1 : 0).Append(" lists=").Append(dd.CmdListsCount)
             .Append(" totalvtx=").Append(dd.TotalVtxCount).Append(" totalidx=").Append(dd.TotalIdxCount).Append('\n');
        DumpVec2("DD.displaysize", dd.DisplaySize);
        DumpVec2("DD.displaypos", dd.DisplayPos);
        DumpVec2("DD.fbscale", dd.FramebufferScale);
        uint hash = 2166136261u;
        for (int li = 0; li < dd.CmdListsCount; li++)
        {
            ImDrawList dl = dd.CmdLists[li];
            g_out.Append("LIST ").Append(li).Append(" vtx=").Append(dl.VtxBuffer.Size).Append(" idx=").Append(dl.IdxBuffer.Size).Append('\n');
            for (int ci = 0; ci < dl.CmdBuffer.Size; ci++)
            {
                ImDrawCmd cmd = dl.CmdBuffer[ci];
                DumpVec4("CMD.clip", cmd.ClipRect);
                g_out.Append("CMD texid=").Append(cmd.GetTexID()).Append(" elem=").Append(cmd.ElemCount)
                     .Append(" idxoff=").Append(cmd.IdxOffset).Append(" vtxoff=").Append(cmd.VtxOffset).Append('\n');
            }
            if (!compact)
            {
                for (int vi = 0; vi < dl.VtxBuffer.Size; vi++)
                {
                    ImDrawVert v = dl.VtxBuffer[vi];
                    g_out.Append("V ").Append(Hex(v.pos.x)).Append(' ').Append(Hex(v.pos.y)).Append(' ')
                         .Append(Hex(v.uv.x)).Append(' ').Append(Hex(v.uv.y)).Append(' ')
                         .Append(v.col.ToString("x8")).Append('\n');
                }
                for (int ii = 0; ii < dl.IdxBuffer.Size; ii += 12)
                {
                    g_out.Append("I");
                    for (int j = ii; j < ii + 12 && j < dl.IdxBuffer.Size; j++)
                        g_out.Append(' ').Append((uint)dl.IdxBuffer[j]);
                    g_out.Append('\n');
                }
            }
            else
            {
                // FNV-1a over the vertex bytes (pos.xy, uv.xy, col, LE) then index bytes (LE),
                // matching the C++ reference hashing raw ImDrawVert/ImDrawIdx memory.
                for (int vi = 0; vi < dl.VtxBuffer.Size; vi++)
                {
                    ImDrawVert v = dl.VtxBuffer[vi];
                    hash = FnvFloat(hash, v.pos.x);
                    hash = FnvFloat(hash, v.pos.y);
                    hash = FnvFloat(hash, v.uv.x);
                    hash = FnvFloat(hash, v.uv.y);
                    hash = FnvU32(hash, v.col);
                }
                for (int ii = 0; ii < dl.IdxBuffer.Size; ii++)
                {
                    ushort idx = dl.IdxBuffer[ii]; // 2 bytes LE, matching raw C++ ImDrawIdx memory
                    hash = FnvByte(hash, idx);
                    hash = FnvByte(hash, (uint)idx >> 8);
                }
            }
        }
        if (compact)
            g_out.Append("HASH ").Append(hash.ToString("x8")).Append('\n');
    }

    delegate void BuildUiFn();

    static void RunFrames(BuildUiFn build_ui, int frames = 3)
    {
        for (int f = 0; f < frames; f++)
        {
            ImGui.NewFrame();
            build_ui();
            ImGui.Render();
            DumpFrame(f, g_compact);
            if (System.Environment.GetEnvironmentVariable("GOLDEN_DEBUG") != null)
            {
                ImGuiContext g = ImGui.GImGui;
                for (int wi = 0; wi < g.Windows.Size; wi++)
                {
                    ImGuiWindow w = g.Windows[wi];
System.Console.Error.WriteLine($"DBG f={f} win='{w.Name}' active={(w.Active ? 1 : 0)} pos=({w.Pos.x:g},{w.Pos.y:g}) size=({w.Size.x:g},{w.Size.y:g}) sizefull=({w.SizeFull.x:g},{w.SizeFull.y:g}) content=({w.ContentSize.x:g},{w.ContentSize.y:g}) cursor=({w.DC.CursorPos.x:g},{w.DC.CursorPos.y:g}) maxpos=({w.DC.CursorMaxPos.x:g},{w.DC.CursorMaxPos.y:g}) startpos=({w.DC.CursorStartPos.x:g},{w.DC.CursorStartPos.y:g}) ideal=({w.DC.IdealMaxPos.x:g},{w.DC.IdealMaxPos.y:g}) scrollbar={(w.ScrollbarX ? 1 : 0)},{(w.ScrollbarY ? 1 : 0)}");
                }
            }
        }
    }

    //--------------------------------------------------------------------------
    // Scenarios (must match Cpp/golden_dump.cpp exactly)
    //--------------------------------------------------------------------------

    static void ScEmpty()
    {
        RunFrames(() => { });
    }

    static void ScBasic()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Hello");
            ImGui.Text("ImGui %s build OK", ImGui.GetVersion());
            ImGui.Button("Click");
            ImGui.SameLine();
            ImGui.SmallButton("Small");
            ImGui.End();
        });
    }

    static void ScWidgetsBasic()
    {
        bool c1 = true, c2 = false;
        int radio = 1;
        RunFrames(() =>
        {
            ImGui.Begin("Widgets", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.Checkbox("check1", ref c1);
            ImGui.Checkbox("check2", ref c2);
            ImGui.RadioButton("one", ref radio, 0);
            ImGui.RadioButton("two", ref radio, 1);
            ImGui.ProgressBar(0.42f, new ImVec2(-1, 0), "42%");
            ImGui.ProgressBar(-0.1f, new ImVec2(0, 0));
            ImGui.BulletText("bullet line one");
            ImGui.BulletText("bullet line two");
            ImGui.ArrowButton("left", ImGuiDir_Left);
            ImGui.SameLine();
            ImGui.ArrowButton("right", ImGuiDir_Right);
            ImGui.SmallButton("tiny");
            ImGui.Dummy(new ImVec2(40, 10));
            ImGui.AlignTextToFramePadding();
            ImGui.Text("aligned");
            ImGui.End();
        });
    }

    static void ScSliders()
    {
        float f1 = 0.5f, f2 = 123.456f;
        int i1 = 42;
        RunFrames(() =>
        {
            ImGui.Begin("Sliders", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.SliderFloat("s1", ref f1, 0.0f, 1.0f);
            ImGui.SliderFloat("s2", ref f2, 0.0f, 1000.0f, "%.2f");
            ImGui.SliderInt("s3", ref i1, 0, 100);
            ImGui.DragFloat("d1", ref f1, 0.01f);
            ImGui.DragInt("d2", ref i1, 1);
            ImGui.VSliderFloat("vs", new ImVec2(18, 80), ref f1, 1.0f, 0.0f);
            ImGui.SameLine();
            ImGui.VSliderInt("vi", new ImVec2(18, 80), ref i1, 100, 0);
            ImGui.End();
        });
    }

    static void ScTrees()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Trees", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.SetNextItemOpen(true, ImGuiCond_Appearing);
            if (ImGui.CollapsingHeader("header1"))
            {
                ImGui.Text("inside header");
            }
            ImGui.SetNextItemOpen(true, ImGuiCond_Appearing);
            if (ImGui.TreeNode("node1"))
            {
                ImGui.Text("child text");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("node2")) // closed (no SetNextItemOpen)
            {
                ImGui.Text("hidden");
                ImGui.TreePop();
            }
            if (ImGui.TreeNodeEx("node3", ImGuiTreeNodeFlags_Leaf | ImGuiTreeNodeFlags_Bullet))
                ImGui.TreePop();
            ImGui.End();
        });
    }

    static void ScTabs()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Tabs", ImGuiWindowFlags_AlwaysAutoResize);
            if (ImGui.BeginTabBar("tabbar"))
            {
                if (ImGui.BeginTabItem("Alpha")) { ImGui.Text("alpha content"); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Beta")) { ImGui.Text("beta content"); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Gamma")) { ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }
            ImGui.End();
        });
    }

    static void ScTables()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Tables", ImGuiWindowFlags_AlwaysAutoResize);
            ImGuiTableFlags flags = ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_Resizable | ImGuiTableFlags_SizingFixedFit;
            if (ImGui.BeginTable("t1", 3, flags))
            {
                ImGui.TableSetupColumn("A");
                ImGui.TableSetupColumn("B");
                ImGui.TableSetupColumn("C");
                ImGui.TableHeadersRow();
                for (int row = 0; row < 3; row++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text("r%d c0", row);
                    ImGui.TableNextColumn(); ImGui.Button("b");
                    ImGui.TableNextColumn(); ImGui.Text("value %d", row * 11);
                }
                ImGui.EndTable();
            }
            ImGui.End();
        });
    }

    static void ScChild()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Parent");
            ImGui.Text("above child");
            ImGui.BeginChild("child1", new ImVec2(200, 100), ImGuiChildFlags_Borders);
            ImGui.Text("in child");
            ImGui.Button("child button");
            ImGui.EndChild();
            ImGui.BeginChild("child2", new ImVec2(0, 60));
            ImGui.Text("child two");
            ImGui.EndChild();
            ImGui.End();
        });
    }

    static void ScComboList()
    {
        int item = 1;
        RunFrames(() =>
        {
            ImGui.Begin("ComboList", ImGuiWindowFlags_AlwaysAutoResize);
            if (ImGui.BeginCombo("combo", "Item Two"))
                ImGui.EndCombo();
            string[] items = { "Apple", "Banana", "Cherry", "Durian" };
            ImGui.ListBox("list", ref item, items, 4, 4);
            ImGui.End();
        });
    }

    static void ScPlots()
    {
        float[] data = new float[16];
        for (int i = 0; i < 16; i++)
            data[i] = (i % 4 == 0) ? 0.1f * i : 0.35f + 0.02f * i;
        RunFrames(() =>
        {
            ImGui.Begin("Plots", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.PlotLines("lines", data, 16, 0, "avg", 0.0f, 1.5f, new ImVec2(120, 50));
            ImGui.PlotHistogram("hist", data, 16, 0, null, 0.0f, 1.5f, new ImVec2(120, 50));
            ImGui.End();
        });
    }

    static void ScColors()
    {
        float[] c3 = { 0.25f, 0.5f, 0.75f };
        float[] c4 = { 1.0f, 0.0f, 0.5f, 0.8f };
        RunFrames(() =>
        {
            ImGui.Begin("Colors", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.ColorEdit3("color3", c3);
            ImGui.ColorEdit4("color4", c4);
            ImGui.ColorButton("btn", new ImVec4(0.2f, 0.4f, 0.6f, 1.0f));
            ImGui.End();
        });
    }

    static void ScImages()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Images", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.Image(new ImTextureRef((ImTextureID)0x1234), new ImVec2(32, 32));
            ImGui.ImageButton("ib1", new ImTextureRef((ImTextureID)0x5678), new ImVec2(24, 24));
            ImGui.End();
        });
    }

    static void ScTextFmt()
    {
        RunFrames(() =>
        {
            ImGui.Begin("TextFmt", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.TextDisabled("disabled text");
            ImGui.LabelText("label", "value text");
            ImGui.SeparatorText("Section");
            ImGui.BulletText("fmt %d and %s", 42, "str");
            ImGui.End();
        });
    }

    static void ScTextWrap()
    {
        RunFrames(() =>
        {
            ImGui.SetNextWindowSize(new ImVec2(200, 150), ImGuiCond_Always);
            ImGui.Begin("Wrapped");
            ImGui.TextWrapped("The quick brown fox jumps over the lazy dog again and again until it wraps around.");
            ImGui.End();
        });
    }

    static void ScGroups()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Groups", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.BeginGroup();
            ImGui.Text("g1");
            ImGui.Button("g1button");
            ImGui.EndGroup();
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.Text("g2");
            ImGui.Text("g2b");
            ImGui.EndGroup();
            ImGui.Separator();
            ImGui.Text("after");
            ImGui.End();
        });
    }

    static void ScScroll()
    {
        RunFrames(() =>
        {
            ImGui.SetNextWindowSize(new ImVec2(180, 120), ImGuiCond_Always);
            ImGui.SetNextWindowContentSize(new ImVec2(0, 400));
            ImGui.Begin("Scroll");
            if (ImGui.GetScrollY() == 0.0f)
                ImGui.SetScrollY(50.0f);
            for (int i = 0; i < 20; i++)
                ImGui.Text("line %d", i);
            ImGui.End();
        });
    }

    static void ScMenus()
    {
        RunFrames(() =>
        {
            ImGui.SetNextWindowSize(new ImVec2(300, 150), ImGuiCond_Always);
            ImGui.Begin("Menus");
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    ImGui.MenuItem("New", "Ctrl+N");
                    ImGui.MenuItem("Open", "Ctrl+O");
                    ImGui.EndMenu();
                }
                ImGui.MenuItem("Help");
                ImGui.EndMenuBar();
            }
            ImGui.Text("menu window body");
            ImGui.End();
        });
    }

    static void ScSelectable()
    {
        bool[] sel = { true, false, true, false };
        RunFrames(() =>
        {
            ImGui.Begin("Selectable", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.Selectable("item zero", ref sel[0]);
            ImGui.Selectable("item one", ref sel[1]);
            ImGui.Selectable("item two", ref sel[2]);
            if (ImGui.Selectable("span", false, ImGuiSelectableFlags_SpanAllColumns))
            { }
            ImGui.End();
        });
    }

    static void ScInputNum()
    {
        float f = 3.14f;
        int i = 7;
        RunFrames(() =>
        {
            ImGui.Begin("InputNum", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.InputFloat("if1", ref f);
            ImGui.InputFloat("if2", ref f, 0.1f, 1.0f, "%.2f");
            ImGui.InputInt("ii1", ref i);
            ImGui.InputInt("ii2", ref i, 5, 10);
            ImGui.End();
        });
    }

    static void ScMultiWins()
    {
        RunFrames(() =>
        {
            ImGui.SetNextWindowPos(new ImVec2(40, 40), ImGuiCond_Always);
            ImGui.SetNextWindowSize(new ImVec2(200, 100), ImGuiCond_Always);
            ImGui.Begin("WinA", ImGuiWindowFlags_NoCollapse);
            ImGui.Text("window a");
            ImGui.End();
            ImGui.SetNextWindowPos(new ImVec2(280, 60), ImGuiCond_Always);
            ImGui.SetNextWindowSize(new ImVec2(220, 120), ImGuiCond_Always);
            ImGui.Begin("WinB", ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize);
            ImGui.Text("window b");
            ImGui.Button("in b");
            ImGui.End();
        });
    }

    static void ScStylePush()
    {
        float v = 0;
        RunFrames(() =>
        {
            ImGui.Begin("Style", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.PushStyleColor(ImGuiCol_Button, new ImVec4(1.0f, 0.0f, 0.0f, 1.0f));
            ImGui.PushStyleVar(ImGuiStyleVar_FrameRounding, 6.0f);
            ImGui.Button("red round");
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            ImGui.PushItemWidth(120.0f);
            ImGui.SliderFloat("narrow", ref v, 0, 1);
            ImGui.PopItemWidth();
            ImGui.PushStyleVar(ImGuiStyleVar_FramePadding, new ImVec2(2, 2));
            ImGui.Button("tight");
            ImGui.PopStyleVar();
            ImGui.End();
        });
    }

    static void ScProgressBar2()
    {
        RunFrames(() =>
        {
            ImGui.Begin("PB", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.ProgressBar(0.0f);
            ImGui.ProgressBar(1.0f);
            ImGui.ProgressBar(0.5f, new ImVec2(150, 20), "half");
            ImGui.ProgressBar(0.33f, new ImVec2(0, 0), null);
            ImGui.End();
        });
    }

    static void ScDebugLog()
    {
        RunFrames(() =>
        {
            ImGui.Begin("LogWin");
            ImGui.LogText("some %s text", "fmt");
            ImGui.Text("plain");
            ImGui.End();
        }, 2);
    }

    static void ScPopup()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Popups", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.Button("open target");
            ImGui.OpenPopup("pop1");
            if (ImGui.BeginPopup("pop1"))
            {
                ImGui.MenuItem("item one", "Ctrl+1");
                ImGui.MenuItem("item two");
                ImGui.Separator();
                if (ImGui.BeginMenu("submenu"))
                {
                    ImGui.MenuItem("nested");
                    ImGui.EndMenu();
                }
                ImGui.EndPopup();
            }
            if (ImGui.BeginPopupContextItem("ctx"))
            {
                ImGui.Selectable("ctx select");
                ImGui.EndPopup();
            }
            ImGui.End();
        });
    }

    static void ScTooltip()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Tooltips", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.Button("hover me");
            ImGui.SetItemTooltip("tooltip %d", 42);
            ImGui.BeginTooltip();
            ImGui.Text("always tip line 1");
            ImGui.TextUnformatted("always tip line 2");
            ImGui.EndTooltip();
            ImGui.End();
        });
    }

    static byte[] g_buf1 = MakeBuf("hello world");
    static byte[] g_buf2 = MakeBuf("");

    static byte[] MakeBuf(string s)
    {
        var b = new byte[64];
        int n = System.Text.Encoding.UTF8.GetBytes(s, 0, s.Length, b, 0);
        b[n] = 0;
        return b;
    }

    static void ScInputText()
    {
        float[] w = new float[3];
        RunFrames(() =>
        {
            ImGui.Begin("InputText", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.InputText("it1", g_buf1, g_buf1.Length);
            ImGui.InputTextWithHint("it2", "hint here", g_buf2, g_buf2.Length);
            ImGui.InputFloat3("vec", w);
            ImGui.End();
        });
    }

    static void ScScroll2()
    {
        RunFrames(() =>
        {
            ImGui.SetNextWindowSize(new ImVec2(200, 120), ImGuiCond_Always);
            ImGui.Begin("Scroll2", ImGuiWindowFlags_AlwaysVerticalScrollbar);
            for (int i = 0; i < 8; i++)
                ImGui.Text("scroll line %d", i);
            ImGui.End();
            ImGui.SetNextWindowSize(new ImVec2(200, 90), ImGuiCond_Always);
            ImGui.Begin("HScroll", ImGuiWindowFlags_AlwaysHorizontalScrollbar);
            ImGui.SetNextWindowContentSize(new ImVec2(500, 0));
            ImGui.Text("wide content");
            ImGui.End();
        });
    }

    static void ScCollapsed()
    {
        RunFrames(() =>
        {
            ImGui.SetNextWindowCollapsed(true, ImGuiCond_Always);
            ImGui.Begin("CollapsedWin");
            ImGui.Text("never seen");
            ImGui.End();
            ImGui.SetNextWindowPos(new ImVec2(300, 300), ImGuiCond_Always);
            ImGui.Begin("NormalWin", ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoScrollbar);
            ImGui.Text("normal");
            ImGui.End();
        });
    }

    static void ScTables2()
    {
        RunFrames(() =>
        {
            ImGui.Begin("Tables2", ImGuiWindowFlags_AlwaysAutoResize);
            ImGuiTableFlags flags = ImGuiTableFlags_Resizable | ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders
                | ImGuiTableFlags_Sortable | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
            if (ImGui.BeginTable("t2", 3, flags, new ImVec2(0, 100)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("col0", ImGuiTableColumnFlags_WidthFixed, 60.0f);
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags_WidthFixed, 80.0f);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags_WidthStretch);
                ImGui.TableHeadersRow();
                ImGui.TableSetColumnSortDirection(0, ImGuiSortDirection_Ascending, false);
                for (int row = 0; row < 6; row++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text("cell %d", row);
                    ImGui.TableNextColumn(); ImGui.Button("btn", new ImVec2(40, 0));
                    ImGui.TableNextColumn(); ImGui.Text("x%d", row * 7);
                }
                ImGui.EndTable();
            }
            ImGui.End();
        });
    }

    static void ScTreeLines()
    {
        RunFrames(() =>
        {
            ImGui.Begin("TreeLines", ImGuiWindowFlags_AlwaysAutoResize);
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_SpanAvailWidth;
            ImGui.SetNextItemOpen(true, ImGuiCond_Always);
            if (ImGui.TreeNodeEx("root", flags))
            {
                ImGui.Text("root text");
                ImGui.SetNextItemOpen(true, ImGuiCond_Always);
                if (ImGui.TreeNodeEx("child", flags))
                {
                    ImGui.Text("child text");
                    ImGui.TreePop();
                }
                ImGui.TreeNodeEx("leaf", ImGuiTreeNodeFlags_Leaf | ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_Bullet);
                ImGui.TreePop();
                ImGui.TreePop();
            }
            ImGui.End();
        });
    }

    static ImGuiTextFilter g_filter;

    static void ScListClipper()
    {
        RunFrames(() =>
        {
            ImGui.Begin("ClipList", ImGuiWindowFlags_AlwaysAutoResize);
            ImGui.BeginChild("list", new ImVec2(200, 150));
            var clipper = new ImGuiListClipper();
            clipper.Begin(50);
            while (clipper.Step())
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    ImGui.Text($"line {i}");
            clipper.End();
            ImGui.EndChild();
            ImGui.End();
        }, 2);
    }

    static void ScTextFilter()
    {
        RunFrames(() =>
        {
            g_filter ??= new ImGuiTextFilter("ap,-ze");
            ImGui.Begin("FilterWin", ImGuiWindowFlags_AlwaysAutoResize);
            g_filter.Draw("##filter", 120);
            ImGui.Text($"pass1={(g_filter.PassFilter("apple pie") ? 1 : 0)}");
            ImGui.Text($"pass2={(g_filter.PassFilter("zebra") ? 1 : 0)}");
            ImGui.Text($"pass3={(g_filter.PassFilter("grape") ? 1 : 0)}");
            ImGui.Text($"pass4={(g_filter.PassFilter("apricot", 3) ? 1 : 0)}");
            ImGui.End();
        }, 2);
    }

    static void ScDemo()
    {
        // Full demo window (imgui_demo.cpp ShowDemoWindow). Compact dump: structure +
        // FNV-1a hash of all vertex/index bytes (a full vertex dump would be many MB).
        bool show = true;
        g_compact = System.Environment.GetEnvironmentVariable("GOLDEN_FULL") == null;
        RunFrames(() => ImGui.ShowDemoWindow(ref show), 3);
        g_compact = false;
    }

    //--------------------------------------------------------------------------
    // Runner
    //--------------------------------------------------------------------------

    public delegate void ScenarioFn();

    public static (string name, ScenarioFn fn)[] Scenarios => new (string, ScenarioFn)[]
    {
        ("empty",       ScEmpty),
        ("basic",       ScBasic),
        ("widgets",     ScWidgetsBasic),
        ("sliders",     ScSliders),
        ("trees",       ScTrees),
        ("tabs",        ScTabs),
        ("tables",      ScTables),
        ("child",       ScChild),
        ("combolist",   ScComboList),
        ("plots",       ScPlots),
        ("colors",      ScColors),
        ("images",      ScImages),
        ("textfmt",     ScTextFmt),
        ("textwrap",    ScTextWrap),
        ("groups",      ScGroups),
        ("scroll",      ScScroll),
        ("menus",       ScMenus),
        ("selectable",  ScSelectable),
        ("inputnum",    ScInputNum),
        ("multiwins",   ScMultiWins),
        ("stylepush",   ScStylePush),
        ("progress",    ScProgressBar2),
        ("debuglog",    ScDebugLog),
        ("popup",       ScPopup),
        ("tooltip",     ScTooltip),
        ("inputtext",   ScInputText),
        ("scroll2",     ScScroll2),
        ("collapsed",   ScCollapsed),
        ("tables2",     ScTables2),
        ("treelines",   ScTreeLines),
        ("listclipper", ScListClipper),
        ("textfilter",  ScTextFilter),
        ("demo",        ScDemo),
    };

    public static string RunScenarioPublic(string name, ScenarioFn fn) => RunScenario(name, fn);

    static string RunScenario(string name, ScenarioFn fn)
    {
        BeginScenario(name);
        fn();
        EndScenario();
        return g_out.ToString();
    }

    public static void WriteActual(string name, string content)
    {
        try
        {
            string dir = System.IO.Path.Combine(GoldenDir, "actual");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".txt"), content);
        }
        catch { }
    }
}
