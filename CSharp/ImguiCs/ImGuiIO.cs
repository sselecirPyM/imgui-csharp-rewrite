// Port of ImGuiIO (imgui.h) and its input-event methods (imgui.cpp).
using System;

namespace ImGui;

// [Internal] Storage used by IsKeyDown(), IsKeyPressed() etc functions.
public struct ImGuiKeyData
{
    public bool Down;
    public float DownDuration;
    public float DownDurationPrev;
    public float AnalogValue;
}

public class ImGuiIO
{
    // Configuration
    public int ConfigFlags;                 // ImGuiConfigFlags
    public int BackendFlags;                // ImGuiBackendFlags
    public ImVec2 DisplaySize;
    public ImVec2 DisplayFramebufferScale;
    public float DeltaTime;
    public float IniSavingRate;
    public string IniFilename;
    public string LogFilename;
    public object UserData;

    // Font system
    public ImFontAtlas Fonts;
    public ImFont FontDefault;
    public bool FontAllowUserScaling;

    // Keyboard/Gamepad Navigation options
    public bool ConfigNavSwapGamepadButtons;
    public bool ConfigNavMoveSetMousePos;
    public bool ConfigNavCaptureKeyboard;
    public bool ConfigNavEscapeClearFocusItem;
    public bool ConfigNavEscapeClearFocusWindow;
    public bool ConfigNavCursorVisibleAuto;
    public bool ConfigNavCursorVisibleAlways;

    // Docking options (when ImGuiConfigFlags_DockingEnable is set)
    public bool ConfigDockingNoSplit;               // = false          // Simplified docking mode: disable window splitting, so docking is limited to merging multiple windows together into tab-bars.
    public bool ConfigDockingNoDockingOver;         // = false          // Simplified docking mode: disable window merging into a same tab-bar, so docking is limited to splitting windows.
    public bool ConfigDockingWithShift;             // = false          // Enable docking with holding Shift key (reduce visual noise, allows dropping in wider space)
    public bool ConfigDockingAlwaysTabBar;          // = false          // [BETA] [FIXME: This currently creates regression with auto-sizing and general overhead] Make every single floating window display within a docking node.
    public bool ConfigDockingTransparentPayload;    // = false          // [BETA] Make window or viewport transparent when docking and only display docking boxes on the target viewport. Useful if rendering of multiple viewport cannot be synced. Best used with ConfigViewportsNoAutoMerge.

    // Viewport options (when ImGuiConfigFlags_ViewportsEnable is set)
    public bool ConfigViewportsNoAutoMerge;         // = false;         // Set to make all floating imgui windows always create their own viewport. Otherwise, they are merged into the main host viewports when overlapping it. May also set ImGuiViewportFlags_NoAutoMerge on individual viewport.
    public bool ConfigViewportsNoTaskBarIcon;       // = false          // Disable default OS task bar icon flag for secondary viewports. When a viewport doesn't want a task bar icon, ImGuiViewportFlags_NoTaskBarIcon will be set on it.
    public bool ConfigViewportsNoDecoration;        // = true           // Disable default OS window decoration flag for secondary viewports. When a viewport doesn't want window decorations, ImGuiViewportFlags_NoDecoration will be set on it.
    public bool ConfigViewportsNoDefaultParent;     // = true           // Disable setting OS window parent to main viewport by default.
    public bool ConfigViewportsPlatformFocusSetsImGuiFocus; // = true   // When a platform window is focused (e.g. using Alt+Tab, clicking Platform Title Bar), apply corresponding focus on imgui windows.

    // DPI/Scaling options
    public bool ConfigDpiScaleFonts;                // = false          // [EXPERIMENTAL] Automatically overwrite style.FontScaleDpi when Monitor DPI changes. This will scale fonts but _NOT_ scale sizes/padding for now.
    public bool ConfigDpiScaleViewports;            // = false          // [EXPERIMENTAL] Scale Dear ImGui and Platform Windows when Monitor DPI changes.

    // Widget options
    public bool ConfigMacOSXBehaviors;
    public bool ConfigInputTrickleEventQueue;
    public bool ConfigInputTextCursorBlink;
    public bool ConfigInputTextEnterKeepActive;
    public int ConfigColorEditFlags;        // ImGuiColorEditFlags
    public bool ConfigDragClickToInputText;
    public bool ConfigWindowsResizeFromEdges;
    public bool ConfigWindowsMoveFromTitleBarOnly;
    public bool ConfigWindowsCopyContentsWithCtrlC;
    public bool ConfigScrollbarScrollByPage;

    // Ini Settings options
    public bool ConfigIniSettingsSaveLastUsedDate;
    public int ConfigIniSettingsAutoDiscardMonths;
    public bool ConfigDebugIniSettings;

    // Miscellaneous options
    public bool MouseDrawCursor;
    public float ConfigMemoryCompactTimer;

    // Inputs Behaviors
    public float MouseDoubleClickTime;
    public float MouseDoubleClickMaxDist;
    public float MouseSingleClickDelay;
    public float MouseDragThreshold;
    public float KeyRepeatDelay;
    public float KeyRepeatRate;

    // Debug options: Error Handling
    public bool ConfigErrorRecovery;
    public bool ConfigErrorRecoveryEnableAssert;
    public bool ConfigErrorRecoveryEnableDebugLog;
    public bool ConfigErrorRecoveryEnableTooltip;

    public bool ConfigDebugIsDebuggerPresent;

    public bool ConfigDebugHighlightIdConflicts;
    public bool ConfigDebugHighlightIdConflictsShowItemPicker;

    public bool ConfigDebugBeginReturnValueOnce;
    public bool ConfigDebugBeginReturnValueLoop;

    public bool ConfigDebugIgnoreFocusLoss;

    // Platform Identifiers
    public string BackendPlatformName;
    public string BackendRendererName;
    public object BackendPlatformUserData;
    public object BackendRendererUserData;
    public object BackendLanguageUserData;

    // Output - Updated by NewFrame() or EndFrame()/Render()
    public bool WantCaptureMouse;
    public bool WantCaptureKeyboard;
    public bool WantTextInput;
    public bool WantSetMousePos;
    public bool WantSaveIniSettings;
    public bool NavActive;
    public bool NavVisible;
    public float Framerate;
    public int MetricsRenderVertices;
    public int MetricsRenderIndices;
    public int MetricsRenderWindows;
    public int MetricsActiveWindows;
    public ImVec2 MouseDelta;

    // [Internal]
    public ImGuiContext Ctx;

    // Main Input State
    public ImVec2 MousePos;
    public bool[] MouseDown = new bool[5];
    public float MouseWheel;
    public float MouseWheelH;
    public int MouseSource;                 // ImGuiMouseSource
    public ImGuiID MouseHoveredViewport;    // (Optional) Modify using io.AddMouseViewportEvent(). With multi-viewports: viewport the OS mouse is hovering.
    public bool KeyCtrl;
    public bool KeyShift;
    public bool KeyAlt;
    public bool KeySuper;

    // Other state maintained from data above + IO function calls
    public ImGuiKeyChord KeyMods;
    public ImGuiKeyData[] KeysData = new ImGuiKeyData[ImGuiKey_NamedKey_COUNT];
    public bool WantCaptureMouseUnlessPopupClose;
    public ImVec2 MousePosPrev;
    public ImVec2[] MouseClickedPos = new ImVec2[5];
    public double[] MouseClickedTime = new double[5];
    public bool[] MouseClicked = new bool[5];
    public bool[] MouseDoubleClicked = new bool[5];
    public ushort[] MouseClickedCount = new ushort[5];
    public ushort[] MouseClickedLastCount = new ushort[5];
    public bool[] MouseReleased = new bool[5];
    public double[] MouseReleasedTime = new double[5];
    public bool[] MouseDownOwned = new bool[5];
    public bool[] MouseDownOwnedUnlessPopupClose = new bool[5];
    public bool MouseWheelRequestAxisSwap;
    public bool MouseCtrlLeftAsRightClick;
    public float[] MouseDownDuration = new float[5];
    public float[] MouseDownDurationPrev = new float[5];
    public ImVec2[] MouseDragMaxDistanceAbs = new ImVec2[5];
    public float[] MouseDragMaxDistanceSqr = new float[5];
    public float PenPressure;
    public bool AppFocusLost;
    public bool AppAcceptingEvents;
    public ImWchar16 InputQueueSurrogate;
    public ImVector<ImWchar> InputQueueCharacters = new ImVector<ImWchar>();

    // [OBSOLETE]
    public float FontGlobalScale;

    public ImGuiIO()
    {
        ConfigFlags = ImGuiConfigFlags_None;
        BackendFlags = ImGuiBackendFlags_None;
        DisplaySize = new ImVec2(-1.0f, -1.0f);
        DeltaTime = 1.0f / 60.0f;
        IniSavingRate = 5.0f;
        IniFilename = "imgui.ini";
        LogFilename = "imgui_log.txt";

        FontAllowUserScaling = false;
        FontGlobalScale = 1.0f;
        DisplayFramebufferScale = new ImVec2(1.0f, 1.0f);

        ConfigNavCaptureKeyboard = true;
        ConfigNavEscapeClearFocusItem = true;
        ConfigNavCursorVisibleAuto = true;

        // Docking options (when ImGuiConfigFlags_DockingEnable is set)
        ConfigDockingNoSplit = false;
        ConfigDockingNoDockingOver = false;
        ConfigDockingWithShift = false;
        ConfigDockingAlwaysTabBar = false;
        ConfigDockingTransparentPayload = false;

        // Viewport options (when ImGuiConfigFlags_ViewportsEnable is set)
        ConfigViewportsNoAutoMerge = false;
        ConfigViewportsNoTaskBarIcon = false;
        ConfigViewportsNoDecoration = true;
        ConfigViewportsNoDefaultParent = true;
        ConfigViewportsPlatformFocusSetsImGuiFocus = true;

        ConfigMacOSXBehaviors = false;
        ConfigInputTrickleEventQueue = true;
        ConfigInputTextCursorBlink = true;
        ConfigColorEditFlags = ImGuiColorEditFlags_DefaultOptions_;
        ConfigWindowsResizeFromEdges = true;
        ConfigScrollbarScrollByPage = true;

        ConfigIniSettingsSaveLastUsedDate = true;

        ConfigMemoryCompactTimer = 60.0f;

        ConfigDebugHighlightIdConflicts = true;
        ConfigDebugHighlightIdConflictsShowItemPicker = true;

        ConfigErrorRecovery = true;
        ConfigErrorRecoveryEnableAssert = true;
        ConfigErrorRecoveryEnableDebugLog = true;
        ConfigErrorRecoveryEnableTooltip = true;

        MouseDoubleClickTime = 0.30f;
        MouseDoubleClickMaxDist = 6.0f;
        MouseSingleClickDelay = 0.50f;
        MouseDragThreshold = 6.0f;
        KeyRepeatDelay = 0.275f;
        KeyRepeatRate = 0.050f;

        MousePos = new ImVec2(-float.MaxValue, -float.MaxValue);
        MousePosPrev = new ImVec2(-float.MaxValue, -float.MaxValue);
        MouseSource = ImGuiMouseSource_Mouse;
        for (int i = 0; i < MouseDownDuration.Length; i++) MouseDownDuration[i] = MouseDownDurationPrev[i] = -1.0f;
        for (int i = 0; i < KeysData.Length; i++) { KeysData[i] = new ImGuiKeyData(); KeysData[i].DownDuration = KeysData[i].DownDurationPrev = -1.0f; }
        AppAcceptingEvents = true;
    }

    public void AddInputCharacter(uint c)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        ImGuiContext g = Ctx;
        if (c == 0 || !AppAcceptingEvents)
            return;

        var e = new ImGuiInputEvent();
        e.Type = ImGuiInputEventType_Text;
        e.Source = ImGuiInputSource_Keyboard;
        e.EventId = g.InputEventsNextEventId++;
        e.Text_Char = c;
        g.InputEventsQueue.push_back(e);
    }

    public void AddInputCharacterUTF16(ImWchar16 c)
    {
        if ((c == 0 && InputQueueSurrogate == 0) || !AppAcceptingEvents)
            return;

        if ((c & 0xFC00) == 0xD800) // High surrogate, must save
        {
            if (InputQueueSurrogate != 0)
                AddInputCharacter(0xFFFD);
            InputQueueSurrogate = c;
            return;
        }

        ImWchar cp = c;
        if (InputQueueSurrogate != 0)
        {
            if ((c & 0xFC00) != 0xDC00) // Invalid low surrogate
            {
                AddInputCharacter(0xFFFD);
            }
            else
            {
                // ImWchar is 16-bit in this port (IMGUI_USE_WCHAR32 not defined)
                cp = 0xFFFD; // Codepoint will not fit in ImWchar
            }
            InputQueueSurrogate = 0;
        }
        AddInputCharacter((uint)cp);
    }

    public void AddInputCharactersUTF8(string str)
    {
        if (!AppAcceptingEvents)
            return;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str ?? "");
        int i = 0;
        while (i < bytes.Length)
        {
            uint c;
            i += ImText.ImTextCharFromUtf8(out c, bytes, i, bytes.Length);
            AddInputCharacter(c);
        }
    }

    // Clear all incoming events.
    public void ClearEventsQueue()
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        Ctx.InputEventsQueue.clear();
    }

    // Clear current keyboard/gamepad state + current frame text input buffer.
    public void ClearInputKeys()
    {
        ImGuiContext g = Ctx;
        for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++)
        {
            if (ImGui.IsMouseKey((ImGuiKey)key))
                continue;
            var key_data = g.IO.KeysData[key - ImGuiKey_NamedKey_BEGIN];
            key_data.Down = false;
            key_data.DownDuration = -1.0f;
            key_data.DownDurationPrev = -1.0f;
        }
        KeyCtrl = KeyShift = KeyAlt = KeySuper = false;
        KeyMods = ImGuiMod_None;
        InputQueueCharacters.resize(0);
    }

    public void ClearInputMouse()
    {
        for (int key = ImGuiUtil.ImGuiKey_Mouse_BEGIN; key < ImGuiUtil.ImGuiKey_Mouse_END; key++)
        {
            var key_data = KeysData[key - ImGuiKey_NamedKey_BEGIN];
            key_data.Down = false;
            key_data.DownDuration = -1.0f;
            key_data.DownDurationPrev = -1.0f;
        }
        MousePos = new ImVec2(-float.MaxValue, -float.MaxValue);
        for (int n = 0; n < MouseDown.Length; n++)
        {
            MouseDown[n] = false;
            MouseDownDuration[n] = MouseDownDurationPrev[n] = -1.0f;
        }
        MouseWheel = MouseWheelH = 0.0f;
    }

    public void AddKeyAnalogEvent(ImGuiKey key, bool down, float analog_value)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        if (key == ImGuiKey_None || !AppAcceptingEvents)
            return;
        ImGuiContext g = Ctx;
        System.Diagnostics.Debug.Assert(ImGui.IsNamedKeyOrMod(key));
        System.Diagnostics.Debug.Assert(ImGui.IsAliasKey(key) == false);

        if (g.IO.ConfigMacOSXBehaviors)
        {
            if (key == (ImGuiKey)ImGuiMod_Super)          { key = (ImGuiKey)ImGuiMod_Ctrl; }
            else if (key == (ImGuiKey)ImGuiMod_Ctrl)      { key = (ImGuiKey)ImGuiMod_Super; }
            else if (key == ImGuiKey_LeftSuper) { key = ImGuiKey_LeftCtrl; }
            else if (key == ImGuiKey_RightSuper){ key = ImGuiKey_RightCtrl; }
            else if (key == ImGuiKey_LeftCtrl)  { key = ImGuiKey_LeftSuper; }
            else if (key == ImGuiKey_RightCtrl) { key = ImGuiKey_RightSuper; }
        }

        var latest_event = ImGuiUtil.FindLatestInputEvent(g, ImGuiInputEventType_Key, (int)key);
        var key_data = ImGui.GetKeyData(g, key);
        bool latest_key_down = latest_event.HasValue ? latest_event.Value.Key_Down : key_data.Down;
        float latest_key_analog = latest_event.HasValue ? latest_event.Value.Key_AnalogValue : key_data.AnalogValue;
        if (latest_key_down == down && latest_key_analog == analog_value)
            return;

        var e = new ImGuiInputEvent();
        e.Type = ImGuiInputEventType_Key;
        e.Source = ImGui.IsGamepadKey(key) ? ImGuiInputSource_Gamepad : ImGuiInputSource_Keyboard;
        e.EventId = g.InputEventsNextEventId++;
        e.Key_Key = key;
        e.Key_Down = down;
        e.Key_AnalogValue = analog_value;
        g.InputEventsQueue.push_back(e);
    }

    public void AddKeyEvent(ImGuiKey key, bool down)
    {
        if (!AppAcceptingEvents)
            return;
        AddKeyAnalogEvent(key, down, down ? 1.0f : 0.0f);
    }

    public void SetKeyEventNativeData(ImGuiKey key, int native_keycode, int native_scancode, int native_legacy_index = -1)
    {
        if (key == ImGuiKey_None)
            return;
        System.Diagnostics.Debug.Assert(ImGui.IsNamedKey(key));
    }

    public void SetAppAcceptingEvents(bool accepting_events)
    {
        AppAcceptingEvents = accepting_events;
    }

    public void AddMousePosEvent(float x, float y)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        ImGuiContext g = Ctx;
        if (!AppAcceptingEvents)
            return;

        ImVec2 pos = new ImVec2((x > -float.MaxValue) ? ImMath.ImFloor(x) : x, (y > -float.MaxValue) ? ImMath.ImFloor(y) : y);

        var latest_event = ImGuiUtil.FindLatestInputEvent(g, ImGuiInputEventType_MousePos);
        ImVec2 latest_pos = latest_event.HasValue ? new ImVec2(latest_event.Value.MousePos_PosX, latest_event.Value.MousePos_PosY) : g.IO.MousePos;
        if (latest_pos.x == pos.x && latest_pos.y == pos.y)
            return;

        var e = new ImGuiInputEvent();
        e.Type = ImGuiInputEventType_MousePos;
        e.Source = ImGuiInputSource_Mouse;
        e.EventId = g.InputEventsNextEventId++;
        e.MousePos_PosX = pos.x;
        e.MousePos_PosY = pos.y;
        e.MousePos_MouseSource = g.InputEventsNextMouseSource;
        g.InputEventsQueue.push_back(e);
    }

    public void AddMouseButtonEvent(int mouse_button, bool down)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        ImGuiContext g = Ctx;
        System.Diagnostics.Debug.Assert(mouse_button >= 0 && mouse_button < ImGuiMouseButton_COUNT);
        if (!AppAcceptingEvents)
            return;

        if (ConfigMacOSXBehaviors && mouse_button == 0 && MouseCtrlLeftAsRightClick)
        {
            mouse_button = 1;
            if (!down)
                MouseCtrlLeftAsRightClick = false;
        }

        var latest_event = ImGuiUtil.FindLatestInputEvent(g, ImGuiInputEventType_MouseButton, (int)mouse_button);
        bool latest_button_down = latest_event.HasValue ? latest_event.Value.MouseButton_Down : g.IO.MouseDown[mouse_button];
        if (latest_button_down == down)
            return;

        if (ConfigMacOSXBehaviors && mouse_button == 0 && down)
        {
            var latest_super_event = ImGuiUtil.FindLatestInputEvent(g, ImGuiInputEventType_Key, (int)ImGuiMod_Super);
            bool super_down = latest_super_event.HasValue ? latest_super_event.Value.Key_Down : g.IO.KeySuper;
            if (super_down)
            {
                MouseCtrlLeftAsRightClick = true;
                AddMouseButtonEvent(1, true);
                return;
            }
        }

        var e = new ImGuiInputEvent();
        e.Type = ImGuiInputEventType_MouseButton;
        e.Source = ImGuiInputSource_Mouse;
        e.EventId = g.InputEventsNextEventId++;
        e.MouseButton_Button = mouse_button;
        e.MouseButton_Down = down;
        e.MouseButton_MouseSource = g.InputEventsNextMouseSource;
        g.InputEventsQueue.push_back(e);
    }

    public void AddMouseWheelEvent(float wheel_x, float wheel_y)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        ImGuiContext g = Ctx;

        if (!AppAcceptingEvents || (wheel_x == 0.0f && wheel_y == 0.0f))
            return;

        var e = new ImGuiInputEvent();
        e.Type = ImGuiInputEventType_MouseWheel;
        e.Source = ImGuiInputSource_Mouse;
        e.EventId = g.InputEventsNextEventId++;
        e.MouseWheel_WheelX = wheel_x;
        e.MouseWheel_WheelY = wheel_y;
        e.MouseWheel_MouseSource = g.InputEventsNextMouseSource;
        g.InputEventsQueue.push_back(e);
    }

    public void AddMouseSourceEvent(int source)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        ImGuiContext g = Ctx;
        g.InputEventsNextMouseSource = source;
    }

    public void AddMouseViewportEvent(ImGuiID id)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        ImGuiContext g = Ctx;

        // If multiple applications are running at the same time, we should avoid accessing to null pointer.
        if (!AppAcceptingEvents)
            return;

        var e = new ImGuiInputEvent();
        e.Type = ImGuiInputEventType_MouseViewport;
        e.Source = ImGuiInputSource_Mouse;
        e.EventId = g.InputEventsNextEventId++;
        e.MouseViewport_HoveredViewportID = id;
        g.InputEventsQueue.push_back(e);
    }

    public void AddFocusEvent(bool focused)
    {
        System.Diagnostics.Debug.Assert(Ctx != null);
        ImGuiContext g = Ctx;

        var latest_event = ImGuiUtil.FindLatestInputEvent(g, ImGuiInputEventType_Focus);
        bool latest_focused = latest_event.HasValue ? latest_event.Value.AppFocused_Focused : !g.IO.AppFocusLost;
        if (latest_focused == focused || (ConfigDebugIgnoreFocusLoss && !focused))
            return;

        var e = new ImGuiInputEvent();
        e.Type = ImGuiInputEventType_Focus;
        e.EventId = g.InputEventsNextEventId++;
        e.AppFocused_Focused = focused;
        g.InputEventsQueue.push_back(e);
    }
}
