// Port of Dear ImGui public helper types: ImGuiViewport, ImGuiPlatformIO, ImGuiPlatformImeData,
// ImGuiPayload, ImGuiTextBuffer, ImGuiTextIndex, ImGuiListClipper, size/callback data (imgui.h).
using System;
using System.Text;

namespace ImGui;

// A Platform Window (always 1 unless multi-viewport are enabled. One per platform window to output to)
public class ImGuiViewport
{
    public ImGuiID ID;
    public int Flags;                       // ImGuiViewportFlags
    public ImVec2 Pos;
    public ImVec2 Size;
    public ImVec2 FramebufferScale;         // Density of the viewport for Retina display (always 1,1 on Windows, may be 2,2 etc on macOS/iOS). This will affect font rasterizer density.
    public ImVec2 WorkPos;                  // Work Area: Position of the viewport minus task bars, menus bars, status bars (>= Pos)
    public ImVec2 WorkSize;                 // Work Area: Size of the viewport minus task bars, menu bars, status bars (<= Size)
    public float DpiScale;                  // 1.0f = 96 DPI = No extra scale.
    public ImGuiID ParentViewportId;        // (Advanced) 0: no parent. Instruct the platform backend to setup a parent/child relationship between platform windows.
    public ImGuiViewport ParentViewport;    // (Advanced) Direct shortcut to ImGui.FindViewportByID(ParentViewportId). null: no parent.
    public ImDrawData DrawData;             // The ImDrawData corresponding to this viewport. Valid after Render() and until the next call to NewFrame().

    // Platform/Backend Dependent Data
    // The library never uses those fields, they are merely storage to facilitate backend implementation.
    public object RendererUserData;         // void* to hold custom data structure for the renderer (e.g. swap chain, framebuffers etc.). generally set by your Renderer_CreateWindow function.
    public object PlatformUserData;         // void* to hold custom data structure for the OS / platform (e.g. windowing info, render context). generally set by your Platform_CreateWindow function.
    public object PlatformIconData;         // void* to hold custom data structure for the OS / platform to specify an icon.
    public object PlatformHandle;           // void* to hold higher-level, platform window handle (e.g. HWND for Win32 backend, Uint32 WindowID for SDL, GLFWWindow* for GLFW), for FindViewportByPlatformHandle().
    public object PlatformHandleRaw;        // void* to hold lower-level, platform-native window handle (always HWND on Win32 platform, unused for other platforms).
    public bool PlatformWindowCreated;      // Platform window has been created (Platform_CreateWindow() has been called). This is false during the first frame where a viewport is being created.
    public bool PlatformRequestMove;        // Platform window requested move (e.g. window was moved by the OS / host window manager, authoritative position will be OS window position)
    public bool PlatformRequestResize;      // Platform window requested resize (e.g. window was resized by the OS / host window manager, authoritative size will be OS window size)
    public bool PlatformRequestClose;       // Platform window requested closure (e.g. window was moved by the OS / host window manager, e.g. pressing ALT-F4)

    public ImVec2 GetCenter() => new ImVec2(Pos.x + Size.x * 0.5f, Pos.y + Size.y * 0.5f);
    public ImVec2 GetWorkCenter() => new ImVec2(WorkPos.x + WorkSize.x * 0.5f, WorkPos.y + WorkSize.y * 0.5f);
    public string GetDebugName() => ImGuiUtil.ViewportGetDebugName(this);
}

// [ALPHA] Rarely used / very advanced uses only. Use with SetNextWindowClass() and DockSpace() functions.
public class ImGuiWindowClass
{
    public ImGuiID ClassId;                     // User data. 0 = Default class (unclassed). Windows of different classes cannot be docked with each others.
    public ImGuiID ParentViewportId;            // Hint for the platform backend. -1: use default. 0: request platform backend to not parent the platform. != 0: request platform backend to create a parent<>child relationship between the platform windows.
    public ImGuiID FocusRouteParentWindowId;    // ID of parent window for shortcut focus route evaluation, e.g. Shortcut() call from Parent Window will succeed when this window is focused.
    public int ViewportFlagsOverrideSet;        // ImGuiViewportFlags: Viewport flags to set when a window of this class owns a viewport.
    public int ViewportFlagsOverrideClear;      // ImGuiViewportFlags: Viewport flags to clear when a window of this class owns a viewport.
    public int TabItemFlagsOverrideSet;         // ImGuiTabItemFlags: [EXPERIMENTAL] TabItem flags to set when a window of this class gets submitted into a dock node tab bar.
    public int DockNodeFlagsOverrideSet;        // ImGuiDockNodeFlags: [EXPERIMENTAL] Dock node flags to set when a window of this class is hosted by a dock node (it doesn't have to be selected!)
    public bool DockingAlwaysTabBar;            // Set to true to enforce single floating windows of this class always having their own docking node (equivalent of setting the global io.ConfigDockingAlwaysTabBar)
    public bool DockingAllowUnclassed;          // Set to true to allow windows of this class to be docked/merged with an unclassed window.
    public object PlatformIconData;             // [EXPERIMENTAL] Pass opaque data for Platform backend to handle.

    public ImGuiWindowClass() { ParentViewportId = unchecked((ImGuiID)(-1)); DockingAllowUnclassed = true; }
}

// (Optional) This is required when enabling multi-viewport. Represent the bounds of each connected monitor/display and their DPI.
public class ImGuiPlatformMonitor
{
    public ImVec2 MainPos, MainSize;        // Coordinates of the area displayed on this monitor (Min = upper left, Max = bottom right)
    public ImVec2 WorkPos, WorkSize;        // Coordinates without task bars / side bars / menu bars. Used to avoid positioning popups/tooltips inside this region. If you don't have this info, please copy the value for MainPos/MainSize.
    public float DpiScale;                  // 1.0f = 96 DPI
    public object PlatformHandle;           // Backend dependant data (e.g. HMONITOR, GLFWmonitor*, SDL Display Index, NSScreen*)

    public ImGuiPlatformMonitor()
    {
        MainPos = MainSize = WorkPos = WorkSize = new ImVec2(0, 0);
        DpiScale = 1.0f;
        PlatformHandle = null;
    }
}

// Access via ImGui::GetPlatformIO()
public class ImGuiPlatformIO
{
    // Platform: clipboard (managed delegates; default private clipboard)
    public Func<ImGuiContext, string> Platform_GetClipboardTextFn;
    public Action<ImGuiContext, string> Platform_SetClipboardTextFn;
    public object Platform_ClipboardUserData;

    public Func<ImGuiContext, string, bool> Platform_OpenInShellFn;
    public object Platform_OpenInShellUserData;

    public Action<ImGuiContext, ImGuiViewport, ImGuiPlatformImeData> Platform_SetImeDataFn;
    public object Platform_ImeUserData;

    public ImWchar Platform_LocaleDecimalPoint;
    public int Platform_SessionDate;

    // Renderer
    public int Renderer_TextureMaxWidth;
    public int Renderer_TextureMaxHeight;
    public object Renderer_RenderState;

    public ImDrawCallback DrawCallback_ResetRenderState;
    public ImDrawCallback DrawCallback_SetSamplerLinear;
    public ImDrawCallback DrawCallback_SetSamplerNearest;

    //------------------------------------------------------------------
    // Input - Interface with Platform & Renderer backends for Multi-Viewport support
    // (C# port: handlers are managed delegates; unused ones may stay null.)
    //------------------------------------------------------------------

    // Platform Backend functions (e.g. Win32, GLFW, SDL)
    public Action<ImGuiViewport> Platform_CreateWindow;                    // Create a new platform window for the given viewport
    public Action<ImGuiViewport> Platform_DestroyWindow;
    public Action<ImGuiViewport> Platform_ShowWindow;
    public Action<ImGuiViewport, ImVec2> Platform_SetWindowPos;
    public Func<ImGuiViewport, ImVec2> Platform_GetWindowPos;
    public Action<ImGuiViewport, ImVec2> Platform_SetWindowSize;
    public Func<ImGuiViewport, ImVec2> Platform_GetWindowSize;
    public Func<ImGuiViewport, ImVec2> Platform_GetWindowFramebufferScale;
    public Action<ImGuiViewport> Platform_SetWindowFocus;
    public Func<ImGuiViewport, bool> Platform_GetWindowFocus;
    public Func<ImGuiViewport, bool> Platform_GetWindowMinimized;
    public Action<ImGuiViewport, string> Platform_SetWindowTitle;
    public Action<ImGuiViewport, float> Platform_SetWindowAlpha;
    public Action<ImGuiViewport> Platform_UpdateWindow;
    public Action<ImGuiViewport, object> Platform_RenderWindow;
    public Action<ImGuiViewport, object> Platform_SwapBuffers;
    public Func<ImGuiViewport, float> Platform_GetWindowDpiScale;
    public Action<ImGuiViewport> Platform_OnChangedViewport;
    public Func<ImGuiViewport, ImVec4> Platform_GetWindowWorkAreaInsets;
    public Func<ImGuiViewport, ulong, object, ulong[], int> Platform_CreateVkSurface; // (Optional) For a Vulkan Renderer to call into Platform code. C# port: (vp, vk_instance, vk_allocators, out_surface_array) -> result.
    public object Platform_ViewportUserData; // C# adaptation: platform backend storage (C++: Platform_CreateWindow uses vp->PlatformUserData)

    // Renderer Backend functions (e.g. DirectX, OpenGL, Vulkan)
    public Action<ImGuiViewport> Renderer_CreateWindow;
    public Action<ImGuiViewport> Renderer_DestroyWindow;
    public Action<ImGuiViewport, ImVec2> Renderer_SetWindowSize;
    public Action<ImGuiViewport, object> Renderer_RenderWindow;
    public Action<ImGuiViewport, object> Renderer_SwapBuffers;

    // (Optional) Monitor list - updated by app/backend every frame to support changing monitor or DPI configuration.
    public ImVector<ImGuiPlatformMonitor> Monitors = new ImVector<ImGuiPlatformMonitor>();

    // Output
    // The ImGui_ImplXXXX_RenderDrawData() function of each backend generally access this via ImDrawData::Textures which points to this. The array is available here mostly because backends will want to destroy textures on shutdown.
    public ImVector<ImTextureData> Textures = new ImVector<ImTextureData>();

    // Viewports list (the list is updated by calling ImGui.EndFrame or ImGui.Render)
    // (in the future we will attempt to organize this feature to remove the need for a "main viewport")
    public ImVector<ImGuiViewport> Viewports = new ImVector<ImGuiViewport>();

    public ImGuiPlatformIO()
    {
        Platform_LocaleDecimalPoint = (ImWchar)'.';
    }

    public void ClearPlatformHandlers()
    {
        Platform_GetClipboardTextFn = null;
        Platform_SetClipboardTextFn = null;
        Platform_ClipboardUserData = null;
        Platform_OpenInShellFn = null;
        Platform_OpenInShellUserData = null;
        Platform_SetImeDataFn = null;
        Platform_ImeUserData = null;
        Platform_CreateWindow = null;
        Platform_DestroyWindow = null;
        Platform_ShowWindow = null;
        Platform_SetWindowPos = null;
        Platform_GetWindowPos = null;
        Platform_SetWindowSize = null;
        Platform_GetWindowSize = null;
        Platform_GetWindowFramebufferScale = null;
        Platform_SetWindowFocus = null;
        Platform_GetWindowFocus = null;
        Platform_GetWindowMinimized = null;
        Platform_SetWindowTitle = null;
        Platform_SetWindowAlpha = null;
        Platform_UpdateWindow = null;
        Platform_RenderWindow = null;
        Platform_SwapBuffers = null;
        Platform_GetWindowDpiScale = null;
        Platform_OnChangedViewport = null;
        Platform_GetWindowWorkAreaInsets = null;
        Platform_CreateVkSurface = null;
        Monitors.resize(0);
    }

    public void ClearRendererHandlers()
    {
        Renderer_TextureMaxWidth = 0;
        Renderer_TextureMaxHeight = 0;
        Renderer_RenderState = null;
        DrawCallback_ResetRenderState = null;
        DrawCallback_SetSamplerLinear = null;
        DrawCallback_SetSamplerNearest = null;
        Renderer_CreateWindow = null;
        Renderer_DestroyWindow = null;
        Renderer_SetWindowSize = null;
        Renderer_RenderWindow = null;
        Renderer_SwapBuffers = null;
    }
}

// (Optional) Support for IME via platform_io.Platform_SetImeDataFn
public class ImGuiPlatformImeData
{
    public bool WantVisible;
    public bool WantTextInput;
    public ImVec2 InputPos;
    public float InputLineHeight;
    public ImGuiID ViewportId;
}

// Shared state of InputText(), passed as an argument to your callback
public class ImGuiInputTextCallbackData
{
    public ImGuiContext Ctx;
    public int EventFlag;                   // ImGuiInputTextFlags (one of ImGuiInputTextFlags_Callback*)
    public int Flags;                       // ImGuiInputTextFlags
    public object UserData;
    public ImGuiID ID;

    // Arguments for the different callback events
    public ImGuiKey EventKey;               // Key pressed (Up/Down/TAB)            // [Completion,History]
    public ImWchar EventChar;               // Character input                      // [CharFilter]
    public bool EventActivated;             // Input field just got activated       // [Always]

    public byte[] Buf;
    public int BufTextLen;
    public int BufSize;
    public bool BufDirty;
    public int CursorPos;
    public int SelectionStart;
    public int SelectionEnd;

    public bool DeleteChars(int pos, int bytes_count) => ImGuiUtil.InputTextCallbackDataDeleteChars(this, pos, bytes_count);
    public bool InsertChars(int pos, string text, int text_end = -1) => ImGuiUtil.InputTextCallbackDataInsertChars(this, pos, text, text_end);
    public void SelectAll() { SelectionStart = 0; SelectionEnd = BufTextLen; if (CursorPos != SelectionEnd) CursorPos = SelectionEnd; BufDirty = true; }
    public bool HasSelection() => SelectionStart != SelectionEnd;
}

// Resizing callback data to apply custom constraint
public class ImGuiSizeCallbackData
{
    public object UserData;
    public ImVec2 Pos;
    public ImVec2 CurrentSize;
    public ImVec2 DesiredSize;
}

// Data payload for Drag and Drop operations
public class ImGuiPayload
{
    public byte[] Data;
    public int DataSize;

    public ImGuiID SourceId;
    public ImGuiID SourceParentId;
    public int DataFrameCount;
    public string DataType = "";
    public bool Preview;
    public bool Delivery;

    public ImGuiPayload() { Clear(); }
    public void Clear()
    {
        SourceId = SourceParentId = 0; Data = null; DataSize = 0; DataType = ""; DataFrameCount = -1; Preview = Delivery = false;
    }
    public bool IsDataType(string type) => DataFrameCount != -1 && type == DataType;
    public bool IsPreview() => Preview;
    public bool IsDelivery() => Delivery;
}

// Helper: Growable text buffer for logging/accumulating text
public class ImGuiTextBuffer
{
    public ImVector<byte> Buf = new ImVector<byte>();

    public char this[int i] => (char)Buf[i];
    public int size() => Buf.Size != 0 ? Buf.Size - 1 : 0;
    public bool empty() => Buf.Size <= 1;
    public void clear() { Buf.clear(); }
    public string c_str() => Buf.Data != null ? Encoding.UTF8.GetString(Buf.Data, 0, Buf.Size - 1) : "";

    public void append(string str, int str_end = -1)
    {
        // C++ semantics: the copied range may include str's own zero-terminator
        // (callers pass strlen+1 so the terminator acts as the separator between
        // entries, e.g. TabBar->TabsNames or table->ColumnsNames). C# strings
        // carry no NUL byte and callers express the length in CHARS, so slice by
        // chars first; the terminator is emulated when str_end extends past the
        // payload. (Treating str_end as a UTF-8 byte count truncated every
        // multi-byte entry — table headers with CJK names rendered as '?'.)
        int char_len = Math.Min(str_end >= 0 ? str_end : str.Length, str.Length);
        int byte_len = Encoding.UTF8.GetByteCount(str, 0, char_len);
        int len = byte_len + (str_end > str.Length ? 1 : 0);
        int write_off = Buf.Size != 0 ? Buf.Size : 1;
        int needed_sz = write_off + len;
        Buf.resize(needed_sz);
        int copy_pos = write_off - 1;
        if (byte_len > 0) Encoding.UTF8.GetBytes(str, 0, char_len, Buf.Data, copy_pos);
        if (str_end > str.Length) Buf.Data[copy_pos + byte_len] = 0;
        Buf.Data[copy_pos + len] = 0;
    }

    public void appendf(string fmt, params object[] args) => append(ImFmt.Sprintf(fmt, args));

    // C++: void ImGuiTextBuffer::appendfv(const char* fmt, va_list args) — args is the pre-packed argument list.
    public void appendfv(string fmt, object[] args) => append(ImFmt.Sprintf(fmt, args));

    public void append(byte[] bytes) => append(bytes, bytes.Length);
    public void append(byte[] bytes, int len)
    {
        int old_size = Buf.Size;
        Buf.resize(old_size + len + 1);
        if (len > 0) Array.Copy(bytes, 0, Buf.Data, old_size, len);
        Buf.Data[Buf.Size - 1] = 0;
    }

    public static string EmptyString = "";
}

// [Internal] Maintain a line index for a text buffer.
// C++ imgui_internal.h (1.93): Offsets stores the BEGINNING offset of each line; EndOffset is the total text length.
public class ImGuiTextIndex
{
    public ImVector<int> Offsets = new ImVector<int>();
    public int EndOffset = 0;                           // Because we don't own text buffer we need to maintain EndOffset (may bake in LineOffsets?)

    public void clear() { Offsets.clear(); EndOffset = 0; }
    public int size() => Offsets.Size;
    public int get_line_begin(int n) => Offsets.Size != 0 ? Offsets[n] : 0;
    public int get_line_end(int n) => n + 1 < Offsets.Size ? Offsets[n + 1] - 1 : EndOffset;

    // C++: void ImGuiTextIndex::append(const char* base, int old_size, int new_size)
    // 'base_buf' is the UTF-8 text buffer we index into.
    public void append(byte[] base_buf, int old_size, int new_size)
    {
        System.Diagnostics.Debug.Assert(old_size >= 0 && new_size >= old_size && new_size >= EndOffset);
        if (old_size == new_size)
            return;
        if (EndOffset == 0 || base_buf[EndOffset - 1] == '\n')
            Offsets.push_back(EndOffset);
        int base_end = new_size;
        for (int p = old_size; p < base_end && (p = Array.IndexOf(base_buf, (byte)'\n', p, base_end - p)) >= 0; )
        {
            p++;
            if (p < base_end) // Don't push a trailing offset on last \n
                Offsets.push_back(p);
        }
        EndOffset = ImMath.ImMax(EndOffset, new_size);
    }
}

// Helper: ImGuiListClipper
public class ImGuiListClipper
{
    public ImGuiContext Ctx;
    public int DisplayStart;
    public int DisplayEnd;
    public int UserIndex;                   // Helper storage for user convenience/code. Optional, and otherwise unused if you don't use it.

    // [Internal]
    public int Flags;                       // ImGuiListClipperFlags
    public int ItemsCount;
    public int StepNo;
    public int ItemsFrozen;
    public float ItemsHeight;
    public double StartPosY;                // C++ 1.93: double StartPosY
    public double StartSeekOffsetY;
    public ImGuiListClipperData TempData = new ImGuiListClipperData();

    public ImGuiListClipper() { Ctx = null; memset(); }
    private void memset() { DisplayStart = DisplayEnd = -1; ItemsCount = StepNo = ItemsFrozen = 0; ItemsHeight = -1.0f; StartPosY = 0.0; StartSeekOffsetY = 0.0; }

    public void Begin(int items_count, float items_height = -1.0f) => ImGuiUtil.ListClipperBegin(this, items_count, items_height);
    public void End() => ImGuiUtil.ListClipperEnd(this);
    public bool Step() => ImGuiUtil.ListClipperStep(this);
    public void SeekCursorAndSetupPrevLine(float pos_y, float line_height) => ImGuiUtil.ListClipperSeekCursorAndSetupPrevLine(this, pos_y, line_height);

    // C++: void ImGuiListClipper::SeekCursorForItem(int item_n) — seek cursor toward given item.
    // Automatically called while stepping; the only reason to call this manually is when using
    // Begin(INT_MAX) (unknown item count), where you'll want to call it with the final item count.
    public void SeekCursorForItem(int item_n) => ImGuiUtil.ListClipperSeekCursorForItem(this, item_n);

    // C++ imgui.h inline: void ImGuiListClipper::IncludeItemByIndex(int item_index)
    // Call IncludeItemByIndex() *BEFORE* first call to Step() if you need this item to not be clipped, regardless of its visibility.
    public void IncludeItemByIndex(int item_index) => IncludeItemsByIndex(item_index, item_index + 1);

    // C++: void ImGuiListClipper::IncludeItemsByIndex(int item_begin, int item_end)  [imgui.cpp]
    public void IncludeItemsByIndex(int item_begin, int item_end)
    {
        ImGuiListClipperData data = TempData;
        System.Diagnostics.Debug.Assert(DisplayStart < 0); // Only allowed after Begin() and if there has not been a specified range yet.
        System.Diagnostics.Debug.Assert(item_begin <= item_end);
        if (item_begin < item_end)
            data.Ranges.push_back(ImGuiListClipperRange.FromIndices(item_begin, item_end));
    }
}

// (The old simplified 'ImGuiSelectionBasicStorage' placeholder struct was removed: the real helper
//  (imgui.h ImGuiSelectionBasicStorage) is ported for real in ImGuiWidgets5.cs, Multi-Select helpers section.)

// Helper to build glyph ranges from text/string data.
// C++: struct ImFontGlyphRangesBuilder (imgui.h), methods in imgui_draw.cpp.
public class ImFontGlyphRangesBuilder
{
    public ImVector<uint> UsedChars = new ImVector<uint>(); // Store 1-bit per Unicode code point (0=unused, 1=used)

    public ImFontGlyphRangesBuilder() { Clear(); }

    public void Clear()
    {
        int size_in_bytes = (ImUtil.IM_UNICODE_CODEPOINT_MAX + 1) / 8;
        UsedChars.resize(size_in_bytes / sizeof(uint));
        Array.Clear(UsedChars.Data, 0, UsedChars.Size);
    }

    public bool GetBit(long n)
    {
        int off = (int)(n >> 5);
        uint mask = 1u << (int)(n & 31);
        return (UsedChars[off] & mask) != 0;
    }

    public void SetBit(long n)
    {
        int off = (int)(n >> 5);
        uint mask = 1u << (int)(n & 31);
        UsedChars.Data[off] |= mask;
    }

    public void AddChar(uint c) => SetBit(c); // Add character (C++: AddChar(ImWchar c))

    // Add string (each character of the UTF-8 string is added)
    // C++: void AddText(const char* text, const char* text_end = NULL)
    public void AddText(string text, int text_end = -1)
    {
        if (text_end < 0 || text_end > text.Length)
            text_end = text.Length;
        byte[] bytes = ImText.Utf8FromUtf16(text_end == text.Length ? text : text.Substring(0, text_end));
        int off = 0;
        while (off < bytes.Length)
        {
            int c_len = ImText.ImTextCharFromUtf8(out uint c, bytes, off, bytes.Length);
            if (c_len == 0)
                break;
            off += c_len;
            AddChar(c);
        }
    }

    // Add ranges, e.g. builder.AddRanges(font.GetGlyphRangesDefault()) to force add all of ASCII/Latin+Ext
    // C++: void AddRanges(const ImWchar* ranges) — ranges is a zero-terminated list of [min,max] pairs
    public void AddRanges(ushort[] ranges)
    {
        for (int i = 0; i + 1 < ranges.Length && ranges[i] != 0; i += 2)
            for (uint c = ranges[i]; c <= ranges[i + 1] && c <= ImUtil.IM_UNICODE_CODEPOINT_MAX; c++)
                AddChar(c);
    }

    // Output new ranges (zero-terminated list of [min,max] pairs)
    // C++: void BuildRanges(ImVector<ImWchar>* out_ranges)
    public void BuildRanges(ImVector<ushort> out_ranges)
    {
        int max_codepoint = ImUtil.IM_UNICODE_CODEPOINT_MAX;
        for (int n = 0; n <= max_codepoint; n++)
            if (GetBit(n))
            {
                out_ranges.push_back((ushort)n);
                while (n < max_codepoint && GetBit(n + 1))
                    n++;
                out_ranges.push_back((ushort)n);
            }
        out_ranges.push_back(0);
    }
}

// Helper: Parse and apply text filters. In format "aaaaa[,bbbb][,ccccc]"
// C++: struct ImGuiTextFilter (imgui.h), methods in imgui.cpp.
public class ImGuiTextFilter
{
    // C++: struct ImGuiTextFilter::ImGuiTextRange — a [b,e) range into a shared byte buffer.
    // C# keeps the backing buffer plus offsets (C++ uses raw pointers). All offsets are
    // byte offsets into InputBuf, matching C++ semantics for multi-byte UTF-8 input.
    public class ImGuiTextRange
    {
        public byte[] Source;               // backing buffer
        public int b;                       // start offset into Source
        public int e;                       // end offset into Source (exclusive)

        public ImGuiTextRange() { Source = null; b = e = 0; }
        public ImGuiTextRange(byte[] src, int _b, int _e) { Source = src; b = _b; e = _e; }
        public bool empty() => b == e;

        // C++: void ImGuiTextRange::split(char separator, ImVector<ImGuiTextRange>* out) const
        public void split(char separator, ImVector<ImGuiTextRange> @out)
        {
            @out.resize(0);
            int wb = b;
            int we = wb;
            while (we < e)
            {
                if (Source[we] == separator)
                {
                    @out.push_back(new ImGuiTextRange(Source, wb, we));
                    wb = we + 1;
                }
                we++;
            }
            if (wb != we)
                @out.push_back(new ImGuiTextRange(Source, wb, we));
        }
    }

    public byte[] InputBuf = new byte[256];                     // utf-8, NUL-terminated (C++: char[256])
    public ImVector<ImGuiTextRange> Filters = new ImVector<ImGuiTextRange>();
    public int CountGrep;

    // C++: ImGuiTextFilter::ImGuiTextFilter(const char* default_filter)
    public ImGuiTextFilter(string default_filter = null)
    {
        InputBuf[0] = 0;
        CountGrep = 0;
        if (default_filter != null)
        {
            ImTextUtilCopyStringToUtf8(InputBuf, default_filter);
            Build();
        }
    }

    // Helper calling InputText+Build
    // C++: bool ImGuiTextFilter::Draw(const char* label, float width)
    public bool Draw(string label = "Filter (inc,-exc)", float width = 0.0f)
    {
        if (width != 0.0f)
            ImGui.SetNextItemWidth(width);
        bool value_changed = ImGui.InputText(label, InputBuf, InputBuf.Length);
        if (value_changed)
            Build();
        return value_changed;
    }

    public void Clear() { InputBuf[0] = 0; Build(); }
    public bool IsActive() => Filters.Size > 0;

    // C++: void ImGuiTextFilter::Build()
    public void Build()
    {
        Filters.resize(0);
        int input_len = 0;
        while (input_len < InputBuf.Length && InputBuf[input_len] != 0)
            input_len++;
        var input_range = new ImGuiTextRange(InputBuf, 0, input_len);
        input_range.split(',', Filters);

        CountGrep = 0;
        for (int i = 0; i < Filters.Size; i++)
        {
            ImGuiTextRange f = Filters[i];
            while (f.b < f.e && ImText.ImCharIsBlankA((char)f.Source[f.b]))
                f.b++;
            while (f.e > f.b && ImText.ImCharIsBlankA((char)f.Source[f.e - 1]))
                f.e--;
            if (f.empty())
                continue;
            if (f.Source[f.b] != (byte)'-')
                CountGrep += 1;
        }
    }

    // C++: bool ImGuiTextFilter::PassFilter(const char* text, const char* text_end) const
    public bool PassFilter(string text, int text_end = -1)
    {
        if (Filters.Size == 0)
            return true;

        if (text == null)
            text = "";
        if (text_end < 0 || text_end > text.Length)
            text_end = text.Length;

        byte[] hay = ImText.Utf8FromUtf16(text_end == text.Length ? text : text.Substring(0, text_end));

        for (int i = 0; i < Filters.Size; i++)
        {
            ImGuiTextRange f = Filters[i];
            if (f.b == f.e)
                continue;
            if (f.Source[f.b] == (byte)'-')
            {
                // Subtract
                if (ImStristr(hay, ImTextUtilSlice(InputBuf, f.b + 1, f.e)) >= 0)
                    return false;
            }
            else
            {
                // Grep
                if (ImStristr(hay, ImTextUtilSlice(InputBuf, f.b, f.e)) >= 0)
                    return true;
            }
        }

        // Implicit * grep
        if (CountGrep == 0)
            return true;

        return false;
    }

    // C++: const char* ImStristr(...) — case-insensitive substring search; returns match start index or -1
    static int ImStristr(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0)
            return -1;
        byte un0 = ImTextUtilToUpper(needle[0]);
        for (int i = 0; i < haystack.Length; i++)
        {
            if (ImTextUtilToUpper(haystack[i]) == un0)
            {
                int bn = 1;
                int a = i + 1;
                for (; bn < needle.Length && a < haystack.Length; a++, bn++)
                    if (ImTextUtilToUpper(haystack[a]) != ImTextUtilToUpper(needle[bn]))
                        break;
                if (bn == needle.Length)
                    return i;
            }
        }
        return -1;
    }

    // Extract InputBuf[b..e) as a byte array (C++ passes pointer ranges to ImStristr)
    static byte[] ImTextUtilSlice(byte[] buf, int b, int e)
    {
        int len = e - b;
        byte[] outp = new byte[len];
        Array.Copy(buf, b, outp, 0, len);
        return outp;
    }

    static void ImTextUtilCopyStringToUtf8(byte[] buf, string s)
    {
        byte[] src = ImText.Utf8FromUtf16(s);
        int n = Math.Min(src.Length, buf.Length - 1);
        Array.Copy(src, 0, buf, 0, n);
        buf[n] = 0;
    }

    static byte ImTextUtilToUpper(byte c) => (byte)((c >= 'a' && c <= 'z') ? c - 32 : c);
}
