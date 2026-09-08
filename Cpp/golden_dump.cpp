// Golden-data generator: runs deterministic ImGui scenarios headlessly and
// dumps every vertex / index / draw command with bit-exact float encoding,
// so the C# port can be diffed against the C++ reference output.
#include "imgui.h"
#include "imgui_internal.h"
#include <cstdio>
#include <cstring>
#include <cstdlib>
#include <string>
#include <vector>

static const char* g_current = NULL;
static FILE* g_out = NULL;
static bool g_compact = false; // demo mode: skip per-vertex dump, emit FNV-1a hash of all vtx+idx bytes instead

// FNV-1a over raw bytes, continuing from h (running hash across draw lists).
static unsigned int FnvContinue(unsigned int h, const void* data, size_t n) {
    const unsigned char* p = (const unsigned char*)data;
    for (size_t i = 0; i < n; i++) { h ^= p[i]; h *= 16777619u; }
    return h;
}

static void HexFloat(float f, char* buf) {
    unsigned int u; memcpy(&u, &f, 4);
    sprintf(buf, "%08x", u);
}
static void DumpVec2(const char* name, const ImVec2& v) {
    char a[16], b[16]; HexFloat(v.x, a); HexFloat(v.y, b);
    fprintf(g_out, "%s=%s,%s\n", name, a, b);
}
static void DumpVec4(const char* name, const ImVec4& v) {
    char a[16], b[16], c[16], d[16];
    HexFloat(v.x, a); HexFloat(v.y, b); HexFloat(v.z, c); HexFloat(v.w, d);
    fprintf(g_out, "%s=%s,%s,%s,%s\n", name, a, b, c, d);
}

static void BeginScenario(const char* name) {
    char path[512];
    snprintf(path, sizeof(path), "../CSharp/ImguiCs.Tests/GoldenData/%s.txt", name);
    g_out = fopen(path, "w");
    IM_ASSERT(g_out != NULL);
    g_current = name;
    fprintf(g_out, "# scenario=%s imgui=%s num=%d\n", name, ImGui::GetVersion(), IMGUI_VERSION_NUM);
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2(1280.0f, 720.0f);
    io.DeltaTime = 1.0f / 60.0f;
    io.IniFilename = NULL; // full determinism: never load/save window layout
    io.MousePos = ImVec2(64.0f, 48.0f); // fixed, over the first window's first items
    // Legacy backend path: fully bake the atlas up front (deterministic glyph UVs).
    unsigned char* px; int tw, th, tbpp;
    io.Fonts->GetTexDataAsRGBA32(&px, &tw, &th, &tbpp);
    io.Fonts->SetTexID((ImTextureID)1); // emulate backend upload
    fprintf(g_out, "ATLAS w=%d h=%d bpp=%d\n", tw, th, tbpp);
    if (getenv("GOLDEN_ATLAS")) {
        ImFontAtlas* atlas = io.Fonts;
        ImFontAtlasBuilder* b = atlas->Builder;
        fprintf(stderr, "ATLAS-DUMP rects=%d packed=%d\n", b->Rects.Size, b->RectsPackedCount);
        for (int i = 0; i < b->RectsIndex.Size; i++) {
            const ImFontAtlasRectEntry& e = b->RectsIndex[i];
            if (!e.IsUsed) continue;
            const ImTextureRect& r = b->Rects[e.TargetIndex];
            const char* tag = "";
            if (i == (b->PackIdMouseCursors & ImFontAtlasRectId_IndexMask_)) tag = " MOUSE";
            if (b->PackIdLinesTexData >= 0 && i == (b->PackIdLinesTexData & ImFontAtlasRectId_IndexMask_)) tag = " LINES";
            fprintf(stderr, "R idx=%d x=%d y=%d w=%d h=%d%s\n", i, r.x, r.y, r.w, r.h, tag);
        }
    }
}

static void EndScenario() {
    ImGui::DestroyContext();
    fclose(g_out);
    g_out = NULL;
    g_current = NULL;
}

static void DumpFrame(int frame, bool compact) {
    ImGuiIO& io = ImGui::GetIO();
    unsigned int h = 2166136261u;
    fprintf(g_out, "FRAME %d\n", frame);
    // Font atlas textures
    ImFontAtlas* fonts = io.Fonts;
    ImVector<ImTextureData*>& texs = fonts->TexList;
    fprintf(g_out, "TEXCOUNT %d\n", texs.Size);
    for (int i = 0; i < texs.Size; i++) {
        ImTextureData* tex = texs[i];
        fprintf(g_out, "TEX %d w=%d h=%d bpp=%d status=%d texid=%llu\n", i, tex->Width, tex->Height, tex->BytesPerPixel, (int)tex->Status, (unsigned long long)tex->TexID);
    }
    ImDrawData* dd = ImGui::GetDrawData();
    fprintf(g_out, "DD valid=%d lists=%d totalvtx=%d totalidx=%d\n", dd->Valid ? 1 : 0, dd->CmdListsCount, dd->TotalVtxCount, dd->TotalIdxCount);
    DumpVec2("DD.displaysize", dd->DisplaySize);
    DumpVec2("DD.displaypos", dd->DisplayPos);
    DumpVec2("DD.fbscale", dd->FramebufferScale);
    for (int li = 0; li < dd->CmdListsCount; li++) {
        const ImDrawList* dl = dd->CmdLists[li];
        fprintf(g_out, "LIST %d vtx=%d idx=%d\n", li, dl->VtxBuffer.Size, dl->IdxBuffer.Size);
        for (int ci = 0; ci < dl->CmdBuffer.Size; ci++) {
            const ImDrawCmd& cmd = dl->CmdBuffer[ci];
            DumpVec4("CMD.clip", cmd.ClipRect);
            fprintf(g_out, "CMD texid=%llu elem=%d idxoff=%d vtxoff=%d\n",
                (unsigned long long)cmd.GetTexID(), cmd.ElemCount, cmd.IdxOffset, cmd.VtxOffset);
        }
        if (!compact) {
            const ImDrawVert* vtx = dl->VtxBuffer.Data;
            for (int vi = 0; vi < dl->VtxBuffer.Size; vi++) {
                char px[16], py[16], u[16], v[16];
                HexFloat(vtx[vi].pos.x, px); HexFloat(vtx[vi].pos.y, py);
                HexFloat(vtx[vi].uv.x, u); HexFloat(vtx[vi].uv.y, v);
                fprintf(g_out, "V %s %s %s %s %08x\n", px, py, u, v, vtx[vi].col);
            }
            const ImDrawIdx* idx = dl->IdxBuffer.Data;
            for (int ii = 0; ii < dl->IdxBuffer.Size; ii += 12) {
                fprintf(g_out, "I");
                for (int j = ii; j < ii + 12 && j < dl->IdxBuffer.Size; j++)
                    fprintf(g_out, " %u", (unsigned)idx[j]);
                fprintf(g_out, "\n");
            }
        } else {
            // FNV-1a over the raw vertex bytes (pos.xy, uv.xy, col, LE) then index bytes (LE).
            h = FnvContinue(h, dl->VtxBuffer.Data, (size_t)dl->VtxBuffer.Size * sizeof(ImDrawVert));
            h = FnvContinue(h, dl->IdxBuffer.Data, (size_t)dl->IdxBuffer.Size * sizeof(ImDrawIdx));
        }
    }
    if (compact)
        fprintf(g_out, "HASH %08x\n", h);
}

static void RunFrames(void (*build_ui)(), int frames = 3) {
    for (int f = 0; f < frames; f++) {
        ImGui::NewFrame();
        build_ui();
        ImGui::Render();
        DumpFrame(f, g_compact);
        if (getenv("GOLDEN_DEBUG")) {
            ImGuiContext& g = *ImGui::GetCurrentContext();
            for (int wi = 0; wi < g.Windows.Size; wi++) {
                ImGuiWindow* w = g.Windows[wi];
                fprintf(stderr, "DBG f=%d win='%s' active=%d pos=(%g,%g) size=(%g,%g) sizefull=(%g,%g) content=(%g,%g) cursor=(%g,%g) maxpos=(%g,%g) startpos=(%g,%g) ideal=(%g,%g) scrollbar=(%d,%d)",
                    f, w->Name ? w->Name : "?", w->Active ? 1 : 0, w->Pos.x, w->Pos.y, w->Size.x, w->Size.y,
                    w->SizeFull.x, w->SizeFull.y, w->ContentSize.x, w->ContentSize.y,
                    w->DC.CursorPos.x, w->DC.CursorPos.y, w->DC.CursorMaxPos.x, w->DC.CursorMaxPos.y,
                    w->DC.CursorStartPos.x, w->DC.CursorStartPos.y,
                    w->DC.IdealMaxPos.x, w->DC.IdealMaxPos.y, w->ScrollbarX ? 1 : 0, w->ScrollbarY ? 1 : 0);
                fprintf(stderr, "\n");
            }
        }
    }
}

//--------------------------------------------------------------------------
// Scenarios
//--------------------------------------------------------------------------

static void ScEmpty() {
    RunFrames([]() {});
}

static void ScBasic() {
    RunFrames([]() {
        ImGui::Begin("Hello");
        ImGui::Text("ImGui %s build OK", ImGui::GetVersion());
        ImGui::Button("Click");
        ImGui::SameLine();
        ImGui::SmallButton("Small");
        ImGui::End();
    });
}

static void ScWidgetsBasic() {
    RunFrames([]() {
        ImGui::Begin("Widgets", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        static bool c1 = true, c2 = false;
        ImGui::Checkbox("check1", &c1);
        ImGui::Checkbox("check2", &c2);
        static int radio = 1;
        ImGui::RadioButton("one", &radio, 0);
        ImGui::RadioButton("two", &radio, 1);
        ImGui::ProgressBar(0.42f, ImVec2(-1, 0), "42%");
        ImGui::ProgressBar(-0.1f, ImVec2(0, 0));
        ImGui::BulletText("bullet line one");
        ImGui::BulletText("bullet line two");
        ImGui::ArrowButton("left", ImGuiDir_Left);
        ImGui::SameLine();
        ImGui::ArrowButton("right", ImGuiDir_Right);
        ImGui::SmallButton("tiny");
        ImGui::Dummy(ImVec2(40, 10));
        ImGui::AlignTextToFramePadding();
        ImGui::Text("aligned");
        ImGui::End();
    });
}

static void ScSliders() {
    RunFrames([]() {
        ImGui::Begin("Sliders", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        static float f1 = 0.5f, f2 = 123.456f;
        static int i1 = 42;
        ImGui::SliderFloat("s1", &f1, 0.0f, 1.0f);
        ImGui::SliderFloat("s2", &f2, 0.0f, 1000.0f, "%.2f");
        ImGui::SliderInt("s3", &i1, 0, 100);
        ImGui::DragFloat("d1", &f1, 0.01f);
        ImGui::DragInt("d2", &i1, 1);
        ImGui::VSliderFloat("vs", ImVec2(18, 80), &f1, 1.0f, 0.0f);
        ImGui::SameLine();
        ImGui::VSliderInt("vi", ImVec2(18, 80), &i1, 100, 0);
        ImGui::End();
    });
}

static void ScTrees() {
    RunFrames([]() {
        ImGui::Begin("Trees", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::SetNextItemOpen(true, ImGuiCond_Appearing);
        if (ImGui::CollapsingHeader("header1")) {
            ImGui::Text("inside header");
        }
        ImGui::SetNextItemOpen(true, ImGuiCond_Appearing);
        if (ImGui::TreeNode("node1")) {
            ImGui::Text("child text");
            ImGui::TreePop();
        }
        if (ImGui::TreeNode("node2")) { // closed (no SetNextItemOpen)
            ImGui::Text("hidden");
            ImGui::TreePop();
        }
        if (ImGui::TreeNodeEx("node3", ImGuiTreeNodeFlags_Leaf | ImGuiTreeNodeFlags_Bullet))
            ImGui::TreePop();
        ImGui::End();
    });
}

static void ScTabs() {
    RunFrames([]() {
        ImGui::Begin("Tabs", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        if (ImGui::BeginTabBar("tabbar")) {
            if (ImGui::BeginTabItem("Alpha")) { ImGui::Text("alpha content"); ImGui::EndTabItem(); }
            if (ImGui::BeginTabItem("Beta")) { ImGui::Text("beta content"); ImGui::EndTabItem(); }
            if (ImGui::BeginTabItem("Gamma")) { ImGui::EndTabItem(); }
            ImGui::EndTabBar();
        }
        ImGui::End();
    });
}

static void ScTables() {
    RunFrames([]() {
        ImGui::Begin("Tables", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGuiTableFlags flags = ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_Resizable | ImGuiTableFlags_SizingFixedFit;
        if (ImGui::BeginTable("t1", 3, flags)) {
            ImGui::TableSetupColumn("A");
            ImGui::TableSetupColumn("B");
            ImGui::TableSetupColumn("C");
            ImGui::TableHeadersRow();
            for (int row = 0; row < 3; row++) {
                ImGui::TableNextRow();
                ImGui::TableNextColumn(); ImGui::Text("r%d c0", row);
                ImGui::TableNextColumn(); ImGui::Button("b");
                ImGui::TableNextColumn(); ImGui::Text("value %d", row * 11);
            }
            ImGui::EndTable();
        }
        ImGui::End();
    });
}

static void ScChild() {
    RunFrames([]() {
        ImGui::Begin("Parent");
        ImGui::Text("above child");
        ImGui::BeginChild("child1", ImVec2(200, 100), ImGuiChildFlags_Borders);
        ImGui::Text("in child");
        ImGui::Button("child button");
        ImGui::EndChild();
        ImGui::BeginChild("child2", ImVec2(0, 60));
        ImGui::Text("child two");
        ImGui::EndChild();
        ImGui::End();
    });
}

static void ScComboList() {
    RunFrames([]() {
        ImGui::Begin("ComboList", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        static int item = 1;
        if (ImGui::BeginCombo("combo", "Item Two"))
            ImGui::EndCombo();
        const char* items[] = { "Apple", "Banana", "Cherry", "Durian" };
        ImGui::ListBox("list", &item, items, 4, 4);
        ImGui::End();
    });
}

static void ScPlots() {
    static float data[16];
    for (int i = 0; i < 16; i++)
        data[i] = (i % 4 == 0) ? 0.1f * i : 0.35f + 0.02f * i;
    RunFrames([]() {
        ImGui::Begin("Plots", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::PlotLines("lines", data, 16, 0, "avg", 0.0f, 1.5f, ImVec2(120, 50));
        ImGui::PlotHistogram("hist", data, 16, 0, NULL, 0.0f, 1.5f, ImVec2(120, 50));
        ImGui::End();
    });
}

static void ScColors() {
    RunFrames([]() {
        ImGui::Begin("Colors", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        static float c3[3] = { 0.25f, 0.5f, 0.75f };
        static float c4[4] = { 1.0f, 0.0f, 0.5f, 0.8f };
        ImGui::ColorEdit3("color3", c3);
        ImGui::ColorEdit4("color4", c4);
        ImGui::ColorButton("btn", ImVec4(0.2f, 0.4f, 0.6f, 1.0f));
        ImGui::End();
    });
}

static void ScImages() {
    RunFrames([]() {
        ImGui::Begin("Images", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::Image((ImTextureID)(uintptr_t)0x1234, ImVec2(32, 32));
        ImGui::ImageButton("ib1", (ImTextureID)(uintptr_t)0x5678, ImVec2(24, 24));
        ImGui::End();
    });
}

static void ScTextFmt() {
    RunFrames([]() {
        ImGui::Begin("TextFmt", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::TextDisabled("disabled text");
        ImGui::LabelText("label", "value text");
        ImGui::SeparatorText("Section");
        ImGui::BulletText("fmt %d and %s", 42, "str");
        ImGui::End();
    });
}

static void ScTextWrap() {
    RunFrames([]() {
        ImGui::SetNextWindowSize(ImVec2(200, 150), ImGuiCond_Always);
        ImGui::Begin("Wrapped");
        ImGui::TextWrapped("The quick brown fox jumps over the lazy dog again and again until it wraps around.");
        ImGui::End();
    });
}

static void ScGroups() {
    RunFrames([]() {
        ImGui::Begin("Groups", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::BeginGroup();
        ImGui::Text("g1");
        ImGui::Button("g1button");
        ImGui::EndGroup();
        ImGui::SameLine();
        ImGui::BeginGroup();
        ImGui::Text("g2");
        ImGui::Text("g2b");
        ImGui::EndGroup();
        ImGui::Separator();
        ImGui::Text("after");
        ImGui::End();
    });
}

static void ScScroll() {
    RunFrames([]() {
        ImGui::SetNextWindowSize(ImVec2(180, 120), ImGuiCond_Always);
        ImGui::SetNextWindowContentSize(ImVec2(0, 400));
        ImGui::Begin("Scroll");
        if (ImGui::GetScrollY() == 0.0f)
            ImGui::SetScrollY(50.0f);
        for (int i = 0; i < 20; i++)
            ImGui::Text("line %d", i);
        ImGui::End();
    });
}

static void ScMenus() {
    RunFrames([]() {
        ImGui::SetNextWindowSize(ImVec2(300, 150), ImGuiCond_Always);
        ImGui::Begin("Menus");
        if (ImGui::BeginMenuBar()) {
            if (ImGui::BeginMenu("File")) {
                ImGui::MenuItem("New", "Ctrl+N");
                ImGui::MenuItem("Open", "Ctrl+O");
                ImGui::EndMenu();
            }
            ImGui::MenuItem("Help");
            ImGui::EndMenuBar();
        }
        ImGui::Text("menu window body");
        ImGui::End();
    });
}

static void ScSelectable() {
    RunFrames([]() {
        ImGui::Begin("Selectable", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        static bool sel[4] = { true, false, true, false };
        ImGui::Selectable("item zero", &sel[0]);
        ImGui::Selectable("item one", &sel[1]);
        ImGui::Selectable("item two", &sel[2]);
        if (ImGui::Selectable("span", false, ImGuiSelectableFlags_SpanAllColumns))
            {}
        ImGui::End();
    });
}

static void ScInputNum() {
    RunFrames([]() {
        ImGui::Begin("InputNum", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        static float f = 3.14f;
        static int i = 7;
        ImGui::InputFloat("if1", &f);
        ImGui::InputFloat("if2", &f, 0.1f, 1.0f, "%.2f");
        ImGui::InputInt("ii1", &i);
        ImGui::InputInt("ii2", &i, 5, 10);
        ImGui::End();
    });
}

static void ScMultiWins() {
    RunFrames([]() {
        ImGui::SetNextWindowPos(ImVec2(40, 40), ImGuiCond_Always);
        ImGui::SetNextWindowSize(ImVec2(200, 100), ImGuiCond_Always);
        ImGui::Begin("WinA", NULL, ImGuiWindowFlags_NoCollapse);
        ImGui::Text("window a");
        ImGui::End();
        ImGui::SetNextWindowPos(ImVec2(280, 60), ImGuiCond_Always);
        ImGui::SetNextWindowSize(ImVec2(220, 120), ImGuiCond_Always);
        ImGui::Begin("WinB", NULL, ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize);
        ImGui::Text("window b");
        ImGui::Button("in b");
        ImGui::End();
    });
}

static void ScStylePush() {
    RunFrames([]() {
        ImGui::Begin("Style", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(1.0f, 0.0f, 0.0f, 1.0f));
        ImGui::PushStyleVar(ImGuiStyleVar_FrameRounding, 6.0f);
        ImGui::Button("red round");
        ImGui::PopStyleVar();
        ImGui::PopStyleColor();
        ImGui::PushItemWidth(120.0f);
        static float v = 0;
        ImGui::SliderFloat("narrow", &v, 0, 1);
        ImGui::PopItemWidth();
        ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(2, 2));
        ImGui::Button("tight");
        ImGui::PopStyleVar();
        ImGui::End();
    });
}

static void ScProgressBar2() {
    RunFrames([]() {
        ImGui::Begin("PB", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::ProgressBar(0.0f);
        ImGui::ProgressBar(1.0f);
        ImGui::ProgressBar(0.5f, ImVec2(150, 20), "half");
        ImGui::ProgressBar(0.33f, ImVec2(0, 0), NULL);
        ImGui::End();
    });
}

static void ScDebugLog() {
    RunFrames([]() {
        ImGui::Begin("LogWin");
        ImGui::LogText("some %s text", "fmt");
        ImGui::Text("plain");
        ImGui::End();
    }, 2);
}

static void ScPopup() {
    RunFrames([]() {
        ImGui::Begin("Popups", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::Button("open target");
        ImGui::OpenPopup("pop1");
        if (ImGui::BeginPopup("pop1")) {
            ImGui::MenuItem("item one", "Ctrl+1");
            ImGui::MenuItem("item two");
            ImGui::Separator();
            if (ImGui::BeginMenu("submenu")) {
                ImGui::MenuItem("nested");
                ImGui::EndMenu();
            }
            ImGui::EndPopup();
        }
        if (ImGui::BeginPopupContextItem("ctx")) {
            ImGui::Selectable("ctx select");
            ImGui::EndPopup();
        }
        ImGui::End();
    });
}

static void ScTooltip() {
    RunFrames([]() {
        ImGui::Begin("Tooltips", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::Button("hover me");
        ImGui::SetItemTooltip("tooltip %d", 42);
        ImGui::BeginTooltip();
        ImGui::Text("always tip line 1");
        ImGui::TextUnformatted("always tip line 2");
        ImGui::EndTooltip();
        ImGui::End();
    });
}

static void ScInputText() {
    static char buf1[64] = "hello world";
    static char buf2[64] = "";
    static float w[3] = { 0.0f, 0.0f, 0.0f };
    RunFrames([]() {
        ImGui::Begin("InputText", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::InputText("it1", buf1, 64);
        ImGui::InputTextWithHint("it2", "hint here", buf2, 64);
        ImGui::InputFloat3("vec", w);
        ImGui::End();
    });
}

static void ScScroll2() {
    RunFrames([]() {
        ImGui::SetNextWindowSize(ImVec2(200, 120), ImGuiCond_Always);
        ImGui::Begin("Scroll2", NULL, ImGuiWindowFlags_AlwaysVerticalScrollbar);
        for (int i = 0; i < 8; i++)
            ImGui::Text("scroll line %d", i);
        ImGui::End();
        ImGui::SetNextWindowSize(ImVec2(200, 90), ImGuiCond_Always);
        ImGui::Begin("HScroll", NULL, ImGuiWindowFlags_AlwaysHorizontalScrollbar);
        ImGui::SetNextWindowContentSize(ImVec2(500, 0));
        ImGui::Text("wide content");
        ImGui::End();
    });
}

static void ScCollapsed() {
    RunFrames([]() {
        ImGui::SetNextWindowCollapsed(true, ImGuiCond_Always);
        ImGui::Begin("CollapsedWin");
        ImGui::Text("never seen");
        ImGui::End();
        ImGui::SetNextWindowPos(ImVec2(300, 300), ImGuiCond_Always);
        ImGui::Begin("NormalWin", NULL, ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoScrollbar);
        ImGui::Text("normal");
        ImGui::End();
    });
}

static void ScTables2() {
    RunFrames([]() {
        ImGui::Begin("Tables2", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGuiTableFlags flags = ImGuiTableFlags_Resizable | ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders
            | ImGuiTableFlags_Sortable | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
        if (ImGui::BeginTable("t2", 3, flags, ImVec2(0, 100))) {
            ImGui::TableSetupScrollFreeze(0, 1);
            ImGui::TableSetupColumn("col0", ImGuiTableColumnFlags_WidthFixed, 60.0f);
            ImGui::TableSetupColumn("col1", ImGuiTableColumnFlags_WidthFixed, 80.0f);
            ImGui::TableSetupColumn("col2", ImGuiTableColumnFlags_WidthStretch);
            ImGui::TableHeadersRow();
            ImGui::TableSetColumnSortDirection(0, ImGuiSortDirection_Ascending, false);
            for (int row = 0; row < 6; row++) {
                ImGui::TableNextRow();
                ImGui::TableNextColumn(); ImGui::Text("cell %d", row);
                ImGui::TableNextColumn(); ImGui::Button("btn", ImVec2(40, 0));
                ImGui::TableNextColumn(); ImGui::Text("x%d", row * 7);
            }
            ImGui::EndTable();
        }
        ImGui::End();
    });
}

static void ScDemo() {
    // Full demo window (imgui_demo.cpp ShowDemoWindow). Compact dump: structure +
    // FNV-1a hash of all vertex/index bytes (a full vertex dump would be many MB).
    static bool show = true;
    g_compact = getenv("GOLDEN_FULL") ? false : true;
    RunFrames([]() { ImGui::ShowDemoWindow(&show); }, 3);
    g_compact = false;
}

static void ScTreeLines() {
    RunFrames([]() {
        ImGui::Begin("TreeLines", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_OpenOnArrow | ImGuiTreeNodeFlags_SpanAvailWidth;
        ImGui::SetNextItemOpen(true, ImGuiCond_Always);
        if (ImGui::TreeNodeEx("root", flags)) {
            ImGui::Text("root text");
            ImGui::SetNextItemOpen(true, ImGuiCond_Always);
            if (ImGui::TreeNodeEx("child", flags)) {
                ImGui::Text("child text");
                ImGui::TreePop();
            }
            ImGui::TreeNodeEx("leaf", ImGuiTreeNodeFlags_Leaf | ImGuiTreeNodeFlags_DrawLinesFull | ImGuiTreeNodeFlags_Bullet);
            ImGui::TreePop();
            ImGui::TreePop();
        }
        ImGui::End();
    });
}

static void ScListClipper() {
    RunFrames([]() {
        ImGui::Begin("ClipList", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        ImGui::BeginChild("list", ImVec2(200, 150));
        ImGuiListClipper clipper;
        clipper.Begin(50);
        while (clipper.Step())
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                ImGui::Text("line %d", i);
        clipper.End();
        ImGui::EndChild();
        ImGui::End();
    }, 2);
}

static void ScTextFilter() {
    RunFrames([]() {
        ImGui::Begin("FilterWin", NULL, ImGuiWindowFlags_AlwaysAutoResize);
        static ImGuiTextFilter filter("ap,-ze");
        filter.Draw("##filter", 120);
        ImGui::Text("pass1=%d", filter.PassFilter("apple pie"));
        ImGui::Text("pass2=%d", filter.PassFilter("zebra"));
        ImGui::Text("pass3=%d", filter.PassFilter("grape"));
        ImGui::Text("pass4=%d", filter.PassFilter("apricot", "apricot" + 3));
        ImGui::End();
    }, 2);
}

int main(int argc, char** argv) {
    IMGUI_CHECKVERSION();
    struct { const char* name; void (*fn)(); } scen[] = {
        { "empty",       ScEmpty },
        { "basic",       ScBasic },
        { "widgets",     ScWidgetsBasic },
        { "sliders",     ScSliders },
        { "trees",       ScTrees },
        { "tabs",        ScTabs },
        { "tables",      ScTables },
        { "child",       ScChild },
        { "combolist",   ScComboList },
        { "plots",       ScPlots },
        { "colors",      ScColors },
        { "images",      ScImages },
        { "textfmt",     ScTextFmt },
        { "textwrap",    ScTextWrap },
        { "groups",      ScGroups },
        { "scroll",      ScScroll },
        { "menus",       ScMenus },
        { "selectable",  ScSelectable },
        { "inputnum",    ScInputNum },
        { "multiwins",   ScMultiWins },
        { "stylepush",   ScStylePush },
        { "progress",    ScProgressBar2 },
        { "debuglog",    ScDebugLog },
        { "popup",       ScPopup },
        { "tooltip",     ScTooltip },
        { "inputtext",   ScInputText },
        { "scroll2",     ScScroll2 },
        { "collapsed",   ScCollapsed },
        { "tables2",     ScTables2 },
        { "treelines",   ScTreeLines },
        { "listclipper", ScListClipper },
        { "textfilter",  ScTextFilter },
        { "demo",        ScDemo },
    };
    int only_argc = argc > 1 ? argc - 1 : 0;
    for (int s = 0; s < (int)(sizeof(scen) / sizeof(scen[0])); s++) {
        if (only_argc > 0) {
            bool match = false;
            for (int a = 1; a < argc; a++)
                if (strcmp(argv[a], scen[s].name) == 0) match = true;
            if (!match) continue;
        }
        BeginScenario(scen[s].name);
        scen[s].fn();
        EndScenario();
        printf("dumped %s\n", scen[s].name);
    }
    return 0;
}
