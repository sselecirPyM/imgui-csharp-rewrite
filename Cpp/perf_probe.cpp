// Perf probe: identical UI workload to CSharp/PerfProbe, measures steady-state
// heap allocations per frame (via --wrap=malloc/calloc/realloc/free) and wall time.
// Build: see build_perf.sh
#include "imgui.h"
#include <cstdio>
#include <chrono>
#include <malloc.h>

static long long g_alloc_bytes = 0, g_alloc_count = 0;

extern "C" {
void* __real_malloc(size_t sz);
void* __real_calloc(size_t n, size_t sz);
void* __real_realloc(void* p, size_t sz);
void __real_free(void* p);

void* __wrap_malloc(size_t sz) { g_alloc_bytes += (long long)sz; g_alloc_count++; return __real_malloc(sz); }
void* __wrap_calloc(size_t n, size_t sz) { g_alloc_bytes += (long long)(n * sz); g_alloc_count++; return __real_calloc(n, sz); }
void* __wrap_realloc(void* p, size_t sz) { g_alloc_bytes += (long long)sz; if (p == NULL) g_alloc_count++; return __real_realloc(p, sz); }
void __wrap_free(void* p) { __real_free(p); }
}

// Workload A: full mix — formatted text, buttons, sliders, checkboxes, a table.
static void FrameA(int frame)
{
    ImGui::Begin("Perf window");
    for (int i = 0; i < 8; i++)
        ImGui::Text("Line %d: value %.1f", frame + i, frame * 1.5f + i);
    for (int i = 0; i < 8; i++)
    {
        ImGui::PushID(i);
        ImGui::Button("Button");
        ImGui::PopID();
    }
    for (int i = 0; i < 8; i++)
        ImGui::TextUnformatted("A literal unformatted label string");
    static float f0 = 0.5f, f1 = 0.25f, f2 = 0.75f, f3 = 0.1f;
    ImGui::SliderFloat("Slider A", &f0, 0.0f, 1.0f);
    ImGui::SliderFloat("Slider B", &f1, 0.0f, 1.0f);
    ImGui::SliderFloat("Slider C", &f2, 0.0f, 1.0f);
    ImGui::SliderFloat("Slider D", &f3, 0.0f, 1.0f);
    static bool b0 = false, b1 = true, b2 = false, b3 = true;
    ImGui::Checkbox("Check A", &b0);
    ImGui::Checkbox("Check B", &b1);
    ImGui::Checkbox("Check C", &b2);
    ImGui::Checkbox("Check D", &b3);
    if (ImGui::BeginTable("tbl", 3))
    {
        for (int r = 0; r < 4; r++)
        {
            ImGui::TableNextRow();
            for (int c = 0; c < 3; c++)
            {
                ImGui::TableSetColumnIndex(c);
                ImGui::Text("Cell %d,%d", r, c);
            }
        }
        ImGui::EndTable();
    }
    ImGui::End();
}

// Workload B: only literal-string widgets (no printf-style formatting at all).
static void FrameB(int frame)
{
    (void)frame;
    ImGui::Begin("Perf window B");
    for (int i = 0; i < 8; i++)
        ImGui::TextUnformatted("A literal unformatted label string");
    for (int i = 0; i < 8; i++)
    {
        ImGui::PushID(i);
        ImGui::Button("Button");
        ImGui::PopID();
    }
    static bool b0 = false, b1 = true;
    ImGui::Checkbox("Check A", &b0);
    ImGui::Checkbox("Check B", &b1);
    ImGui::End();
}

typedef void (*FrameFn)(int);

static void Run(const char* name, FrameFn fn)
{
    ImGuiIO& io = ImGui::GetIO();
    // Warmup (also builds font atlas / window pools).
    for (int i = 0; i < 120; i++)
    {
        io.AddMousePosEvent(100 + (i & 63), 100);
        ImGui::NewFrame();
        fn(i);
        ImGui::Render();
    }
    long long b0 = g_alloc_bytes, c0 = g_alloc_count;
    auto t0 = std::chrono::steady_clock::now();
    const int N = 600;
    for (int i = 0; i < N; i++)
    {
        io.AddMousePosEvent(100 + (i & 63), 100);
        ImGui::NewFrame();
        fn(i);
        ImGui::Render();
    }
    auto t1 = std::chrono::steady_clock::now();
    long long bytes = g_alloc_bytes - b0, count = g_alloc_count - c0;
    double us = std::chrono::duration<double, std::micro>(t1 - t0).count() / N;
    printf("[%s] %lld bytes / %lld allocs over %d frames -> %.1f B/frame, %.2f allocs/frame, %.1f us/frame\n",
           name, bytes, count, N, (double)bytes / N, (double)count / N, us);
}

int main()
{
    ImGui::CreateContext();
    ImGui::GetIO().DisplaySize = ImVec2(1280, 720);
    ImGui::GetIO().DeltaTime = 1.0f / 60.0f;
    ImGui::GetIO().IniFilename = NULL; // avoid periodic ini save noise
    unsigned char* px; int w, h;
    ImGui::GetIO().Fonts->GetTexDataAsRGBA32(&px, &w, &h); // no backend: build the atlas up front
    (void)px; (void)w; (void)h;
    Run("A full mix   ", FrameA);
    Run("B no-format   ", FrameB);
    ImGui::DestroyContext();
    return 0;
}
