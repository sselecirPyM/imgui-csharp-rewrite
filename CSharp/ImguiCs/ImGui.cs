// Port of the Dear ImGui public API (imgui.h namespace ImGui) and core helpers.
// Context management + helpers here; lifecycle (NewFrame/Render/EndFrame), windows,
// widgets etc. are in partial class files ported from imgui.cpp / imgui_widgets.cpp / imgui_tables.cpp.
using System;

namespace ImGui;

// C++: typedef void* (*ImGuiMemAllocFunc)(size_t sz, void* user_data);
public delegate IntPtr ImGuiMemAllocFunc(nuint sz, object user_data);
// C++: typedef void (*ImGuiMemFreeFunc)(void* ptr, void* user_data);
public delegate void ImGuiMemFreeFunc(IntPtr ptr, object user_data);

public static partial class ImGui
{
    public static ImGuiContext GImGui;

    public const string ImGuiVersion = "1.93.0 WIP";
    public const int ImGuiVersionNum = 19295;

    // ---- Custom memory allocators ----
    // C++: typedef void* (*ImGuiMemAllocFunc)(size_t sz, void* user_data); typedef void (*ImGuiMemFreeFunc)(void* ptr, void* user_data);
    // The C# port does not route its own allocations through these hooks (the runtime owns memory),
    // but the API is provided so hosting code can share its allocator configuration the same way.
    public static ImGuiMemAllocFunc GImAllocatorAllocFunc;
    public static ImGuiMemFreeFunc GImAllocatorFreeFunc;
    public static object GImAllocatorUserData;

    // C++: void ImGui::SetAllocatorFunctions(ImGuiMemAllocFunc alloc_func, ImGuiMemFreeFunc free_func, void* user_data)
    public static void SetAllocatorFunctions(ImGuiMemAllocFunc alloc_func, ImGuiMemFreeFunc free_func, object user_data = null)
    {
        GImAllocatorAllocFunc = alloc_func;
        GImAllocatorFreeFunc = free_func;
        GImAllocatorUserData = user_data;
    }

    // C++: void ImGui::GetAllocatorFunctions(ImGuiMemAllocFunc* p_alloc_func, ImGuiMemFreeFunc* p_free_func, void** p_user_data)
    // (provided to facilitate copying allocators from one static/DLL boundary to another)
    public static void GetAllocatorFunctions(out ImGuiMemAllocFunc p_alloc_func, out ImGuiMemFreeFunc p_free_func, out object p_user_data)
    {
        p_alloc_func = GImAllocatorAllocFunc;
        p_free_func = GImAllocatorFreeFunc;
        p_user_data = GImAllocatorUserData;
    }

    // ---- Context creation / access ----
    public static ImGuiContext CreateContext(ImFontAtlas shared_font_atlas = null)
    {
        ImGuiContext ctx = new ImGuiContext(shared_font_atlas);
        SetCurrentContext(ctx);
        if (GImGui.LogFile == null) // (placeholder for Initialize()-side setup performed in Initialize())
            Initialize(ctx);
        return ctx;
    }

    public static void DestroyContext(ImGuiContext ctx = null)
    {
        ImGuiContext prev_gimgui = GImGui;
        if (ctx == null)
            ctx = GImGui;
        SetCurrentContext(ctx);
        Shutdown(ctx);
        SetCurrentContext(prev_gimgui);
    }

    public static ImGuiContext GetCurrentContext() => GImGui;
    public static void SetCurrentContext(ImGuiContext ctx) => GImGui = ctx;

    // Port of ImGui::Initialize() (subset: ini handlers, localization, default viewport)
    static void Initialize(ImGuiContext ctx)
    {
        ImGuiContext g = ctx ?? GImGui;
        System.Diagnostics.Debug.Assert(!g.Initialized && !g.SettingsLoaded);

        // Add .ini handle for ImGuiWindow and ImGuiTable types
        {
            var ini_handler = new ImGuiSettingsHandler();
            ini_handler.TypeName = "Window";
            ini_handler.TypeHash = ImGuiHash.ImHashStr("Window", 0);
            ini_handler.ClearAllFn = SettingsHandlerWindow_ClearAll;
            ini_handler.ReadOpenFn = SettingsHandlerWindow_ReadOpen;
            ini_handler.ReadLineFn = SettingsHandlerWindow_ReadLine;
            ini_handler.ApplyAllFn = SettingsHandlerWindow_ApplyAll;
            ini_handler.WriteAllFn = SettingsHandlerWindow_WriteAll;
            g.SettingsHandlers.push_back(ini_handler);
        }
        TableSettingsAddSettingsHandler(g);

        // Setup session starting date (deterministic builds don't depend on it)
        // g.PlatformIO.Platform_SessionDate left 0.

        // Create default viewport
        ImGuiViewportP viewport = new ImGuiViewportP();
        viewport.ID = IMGUI_VIEWPORT_DEFAULT_ID;
        viewport.Idx = 0;
        viewport.PlatformWindowCreated = true;
        viewport.Flags = ImGuiViewportFlags_OwnedByApp;
        g.Viewports.push_back(viewport);
        g.TempBuffer.resize(1024 * 3 + 1);
        g.ViewportCreatedCount++;
        g.PlatformIO.Viewports.push_back(g.Viewports[0]);

        // Build KeysMayBeCharInput[] lookup table (1 bit per named key)
        for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++)
            if ((key >= ImGuiKey_0 && key <= ImGuiKey_9) || (key >= ImGuiKey_A && key <= ImGuiKey_Z) || (key >= ImGuiKey_Keypad0 && key <= ImGuiKey_Keypad9)
                || key == ImGuiKey_Tab || key == ImGuiKey_Space || key == ImGuiKey_Apostrophe || key == ImGuiKey_Comma || key == ImGuiKey_Minus || key == ImGuiKey_Period
                || key == ImGuiKey_Slash || key == ImGuiKey_Semicolon || key == ImGuiKey_Equal || key == ImGuiKey_LeftBracket || key == ImGuiKey_RightBracket || key == ImGuiKey_GraveAccent
                || key == ImGuiKey_KeypadDecimal || key == ImGuiKey_KeypadDivide || key == ImGuiKey_KeypadMultiply || key == ImGuiKey_KeypadSubtract || key == ImGuiKey_KeypadAdd || key == ImGuiKey_KeypadEqual)
                g.KeysMayBeCharInput[key - ImGuiKey_NamedKey_BEGIN] = true;

        // ImDrawList/ImFontAtlas are designed to function without ImGui, and 99% of it works without an ImGui context.
        ImFontAtlas atlas = g.IO.Fonts;
        g.DrawListSharedData.Context = g;
        RegisterFontAtlas(atlas);

        // Initialize Docking
        DockContextInitialize(g);

        g.Initialized = true;
    }

    public const ImGuiID IMGUI_VIEWPORT_DEFAULT_ID = 0x11111111;

    public static void RegisterFontAtlas(ImFontAtlas atlas)
    {
        ImGuiContext g = GImGui;
        if (g.FontAtlases.Size == 0)
            System.Diagnostics.Debug.Assert(atlas == g.IO.Fonts);
        atlas.RefCount++;
        g.FontAtlases.push_back(atlas);
        ImFontAtlas.ImFontAtlasAddDrawListSharedData(atlas, g.DrawListSharedData);
        for (int i = 0; i < atlas.TexList.Size; i++)
            atlas.TexList[i].RefCount = (ushort)atlas.RefCount;
    }

    // ---- Helpers used across the port ----
    public static ImGuiStyle GetStyle() => GImGui.Style;

    // Key data helpers (imgui_internal.h free functions)
    public static bool IsNamedKey(ImGuiKey key) => ImGuiUtil.IsNamedKey(key);
    public static bool IsNamedKeyOrMod(ImGuiKey key) => ImGuiUtil.IsNamedKeyOrMod(key);
    public static bool IsLegacyKey(ImGuiKey key) => ImGuiUtil.IsLegacyKey(key);
    public static bool IsKeyboardKey(ImGuiKey key) => ImGuiUtil.IsKeyboardKey(key);
    public static bool IsGamepadKey(ImGuiKey key) => ImGuiUtil.IsGamepadKey(key);
    public static bool IsMouseKey(ImGuiKey key) => ImGuiUtil.IsMouseKey(key);
    public static bool IsAliasKey(ImGuiKey key) => ImGuiUtil.IsAliasKey(key);
    public static ImGuiKeyData GetKeyData(ImGuiContext ctx, ImGuiKey key) => ImGuiUtil.GetKeyData(ctx, key);
    public static ImGuiKeyData GetKeyData(ImGuiKey key) => ImGuiUtil.GetKeyData(GImGui, key);

    // Window coordinate conversions (imgui.cpp)
    public static ImVec2 WindowPosAbsToRel(ImGuiWindow window, ImVec2 p_abs) => new ImVec2(p_abs.x - window.Pos.x, p_abs.y - window.Pos.y);
    public static ImVec2 WindowPosRelToAbs(ImGuiWindow window, ImVec2 p_rel) => new ImVec2(p_rel.x + window.Pos.x, p_rel.y + window.Pos.y);
    public static ImRect WindowRectAbsToRel(ImGuiWindow window, ImRect r) => new ImRect(WindowPosAbsToRel(window, r.Min), WindowPosAbsToRel(window, r.Max));
    public static ImRect WindowRectRelToAbs(ImGuiWindow window, ImRect r) => new ImRect(WindowPosRelToAbs(window, r.Min), WindowPosRelToAbs(window, r.Max));
}
