#!/usr/bin/env python3
# Apply/revert trace instrumentation to the vendored C++ imgui, to trace draw
# calls during golden scenarios. Usage: python patch_trace.py apply|revert
import re, shutil, sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
DRAW = os.path.join(HERE, "imgui", "imgui_draw.cpp")
CORE = os.path.join(HERE, "imgui", "imgui.cpp")
WIDG = os.path.join(HERE, "imgui", "imgui_widgets.cpp")

def trace_macro():
    return ('#include <cstdio>\n#include <cstdlib>\n'
            'static bool ImProbeTraceOn() { static bool on = getenv("PROBE_TRACE") != NULL; return on; }\n')

def apply():
    # --- imgui_draw.cpp ---
    with open(DRAW, encoding="utf-8") as f:
        src = f.read()
    if "ImProbeTraceOn" in src:
        print("already applied")
        return
    shutil.copyfile(DRAW, DRAW + ".orig")
    # insert helper after first #include block line "//#include <stdio.h>"? Just after first line.
    src = src.replace("#include <stdio.h>", "#include <stdio.h>\n" + trace_macro(), 1)

    def ins(sig, code):
        nonlocal src
        i = src.find(sig)
        assert i >= 0, sig
        j = src.find("{", i)
        assert j >= 0
        src = src[:j+1] + "\n" + code + src[j+1:]

    # PrimRect: log start vtx, rect, col
    ins("void ImDrawList::PrimRect(const ImVec2& a, const ImVec2& c, ImU32 col)",
        'if (ImProbeTraceOn()) fprintf(stderr, "[T] PrimRect v0=%u (%.6f,%.6f)-(%.6f,%.6f) col=%08x\\n", (unsigned)_VtxCurrentIdx, a.x, a.y, c.x, c.y, col);')
    # AddConvexPolyFilled: log points
    ins("void ImDrawList::AddConvexPolyFilled(const ImVec2* points, const int points_count, ImU32 col)",
        'if (ImProbeTraceOn()) { fprintf(stderr, "[T] ConvexFill v0=%u n=%d col=%08x pts:", (unsigned)_VtxCurrentIdx, points_count, col); for (int i = 0; i < points_count && i < 64; i++) fprintf(stderr, " (%.6f,%.6f)", points[i].x, points[i].y); fprintf(stderr, "\\n"); }')
    # AddPolyline: log points
    ins("void ImDrawList::AddPolyline(const ImVec2* points, const int points_count, ImU32 col, float thickness, ImDrawFlags flags)",
        'if (ImProbeTraceOn()) { fprintf(stderr, "[T] Polyline v0=%u n=%d th=%.3f flags=%x col=%08x pts:", (unsigned)_VtxCurrentIdx, points_count, thickness, (unsigned)flags, col); for (int i = 0; i < points_count && i < 64; i++) fprintf(stderr, " (%.6f,%.6f)", points[i].x, points[i].y); fprintf(stderr, "\\n"); }')
    # AddText main entry
    ins("void ImDrawList::AddText(ImFont* font, float font_size, const ImVec2& pos, ImU32 col, const char* text_begin, const char* text_end, float wrap_width, const ImVec4* cpu_fine_clip_rect)",
        'if (ImProbeTraceOn()) fprintf(stderr, "[T] AddText v0=%u pos=(%.6f,%.6f) size=%.3f txt=%.*s\\n", (unsigned)_VtxCurrentIdx, pos.x, pos.y, font_size, text_end ? (int)(text_end - text_begin) : 32, text_begin);')
    with open(DRAW, "w", encoding="utf-8") as f:
        f.write(src)

    # --- imgui.cpp ---
    with open(CORE, encoding="utf-8") as f:
        src = f.read()
    if "ImProbeTraceOn" in src:
        print("core already applied")
        return
    shutil.copyfile(CORE, CORE + ".orig")
    src = src.replace("#include <stdio.h>", "#include <stdio.h>\n" + trace_macro(), 1)
    # RenderWindowDecorations: log bg_rect, title bar, grip info
    ins("void ImGui::RenderWindowDecorations(ImGuiWindow* window, const ImRect& title_bar_rect, bool title_bar_is_highlight, bool handle_borders_and_resize_grips, int resize_grip_count, const ImU32 resize_grip_col[4], float resize_grip_draw_size)",
        'if (ImProbeTraceOn()) fprintf(stderr, "[T] Decorations win=%s pos=(%.6f,%.6f) size=(%.6f,%.6f) tbr=(%.6f,%.6f)-(%.6f,%.6f) grips=%d gds=%.3f round=%.3f border=%.3f\\n", window->Name, window->Pos.x, window->Pos.y, window->Size.x, window->Size.y, title_bar_rect.Min.x, title_bar_rect.Min.y, title_bar_rect.Max.x, title_bar_rect.Max.y, resize_grip_count, resize_grip_draw_size, window->WindowRounding, window->WindowBorderSize);')
    decl = "ImRect bg_rect(window->Pos + ImVec2(0, window->TitleBarHeight), window->Pos + window->Size);"
    trace = 'if (ImProbeTraceOn()) fprintf(stderr, "[T] BG rect=(%.6f,%.6f)-(%.6f,%.6f) col=%08x round=%.3f\\n", bg_rect.Min.x, bg_rect.Min.y, bg_rect.Max.x, bg_rect.Max.y, bg_col, window_rounding);'
    nonlocal_none = src.find(decl)
    assert nonlocal_none >= 0, "bg_rect decl"
    src = src.replace(decl, decl + "\n                " + trace, 1)
    # RenderTextEllipsis entry trace (imgui.cpp)
    sig = "void ImGui::RenderTextEllipsis(ImDrawList* draw_list, const ImVec2& pos_min, const ImVec2& pos_max, float ellipsis_max_x, const char* text, const char* text_end_full, const ImVec2* text_size_if_known)"
    i = src.find(sig)
    assert i >= 0, sig
    j = src.find("{", i)
    rte = 'if (ImProbeTraceOn()) fprintf(stderr, "[T] RenderTextEllipsis pos=(%.4f,%.4f) max=(%.4f,%.4f) ellmax=%.4f txt=%.*s\\n", pos_min.x, pos_min.y, pos_max.x, pos_max.y, ellipsis_max_x, text_end_full ? (int)(text_end_full - text) : 8, text);'
    src = src[:j+1] + "\n    " + rte + src[j+1:]
    with open(CORE, "w", encoding="utf-8") as f:
        f.write(src)

    # --- imgui_tables.cpp (TableBeginRow) ---
    TAB = os.path.join(HERE, "imgui", "imgui_tables.cpp")
    with open(TAB, encoding="utf-8") as f:
        tsrc = f.read()
    if "ImProbeTraceOn" not in tsrc:
        shutil.copyfile(TAB, TAB + ".orig")
        tsrc = ('#include <cstdio>\n#include <cstdlib>\nstatic bool ImProbeTraceOn() { static bool on = getenv("PROBE_TRACE") != NULL; return on; }\n') + tsrc
        sig2 = "void ImGui::TableBeginRow(ImGuiTable* table)"
        i2 = tsrc.find(sig2)
        assert i2 >= 0, sig2
        j2 = tsrc.find("{", i2)
        probe_line = '\n    const float probe_y2 = table->RowPosY2; const float probe_wmin = table->WorkRect.Min.y; const float probe_cursy = table->InnerWindow->DC.CursorPos.y; const float probe_cpy = table->RowCellPaddingY;'
        tsrc = tsrc[:j2+1] + probe_line + tsrc[j2+1:]
        anchor2 = "    table->RowPosY1 = table->RowPosY2 = next_y1;"
        t2 = '    if (ImProbeTraceOn()) fprintf(stderr, "[T] BeginRow row=%d y2_in=%g wmin=%g cursy=%g cpy=%g -> y=%g\\n", table->CurrentRow, probe_y2, probe_wmin, probe_cursy, probe_cpy, table->RowPosY1);'
        assert anchor2 in tsrc
        tsrc = tsrc.replace(anchor2, anchor2 + "\n" + t2, 1)
        with open(TAB, "w", encoding="utf-8") as f:
            f.write(tsrc)

    # --- imgui_widgets.cpp (TabBarLayout) ---
    with open(WIDG, encoding="utf-8") as f:
        wsrc = f.read()
    if "ImProbeTraceOn" not in wsrc:
        shutil.copyfile(WIDG, WIDG + ".orig")
        wsrc = ("#include <cstdio>\n#include <cstdlib>\nstatic bool ImProbeTraceOn() { static bool on = getenv(\"PROBE_TRACE\") != NULL; return on; }\n") + wsrc
        anchor_sig = "static void ImGui::TabBarLayout(ImGuiTabBar* tab_bar)"
        i = wsrc.find(anchor_sig)
        assert i >= 0, anchor_sig
        j = wsrc.find("{", i)
        wsrc = wsrc[:j+1] + "\n    const float probe_w_at_entry = tab_bar->WidthAllTabs; const int probe_tabs_n = tab_bar->Tabs.Size; const unsigned probe_frame = (unsigned)GImGui->FrameCount;" + wsrc[j+1:]
        anchor = "    tab_bar->ScrollingRectMaxX = tab_bar->BarRect.Max.x - sections[2].Width - sections[1].Spacing;"
        tbl_trace = ('    if (ImProbeTraceOn()) fprintf(stderr, "[T] TabBarLayout f=%u tabs=%d barW=%g scrollEn=%d WAll=%g->%g Ideal=%g Sec0=%g/%g Sec1=%g/%g Sec2=%g/%g SRectMax=%g\\n", probe_frame, probe_tabs_n, tab_bar->BarRect.GetWidth(), (int)tab_bar->ScrollButtonEnabled, probe_w_at_entry, tab_bar->WidthAllTabs, tab_bar->WidthAllTabsIdeal, sections[0].Width, sections[0].Spacing, sections[1].Width, sections[1].Spacing, sections[2].Width, sections[2].Spacing, tab_bar->ScrollingRectMaxX);')
        assert anchor in wsrc
        wsrc = wsrc.replace(anchor, anchor + "\n" + tbl_trace, 1)
        with open(WIDG, "w", encoding="utf-8") as f:
            f.write(wsrc)
    print("applied")

def revert():
    for p in (DRAW, CORE, WIDG, os.path.join(HERE, "imgui", "imgui_tables.cpp")):
        if os.path.exists(p + ".orig"):
            shutil.copyfile(p + ".orig", p)
            os.remove(p + ".orig")
            print("reverted", p)
        else:
            print("no backup", p)

if __name__ == "__main__":
    {"apply": apply, "revert": revert}[sys.argv[1]]()
