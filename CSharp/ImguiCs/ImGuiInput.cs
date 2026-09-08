// Port of Dear ImGui imgui.cpp sections:
// - [SECTION] FONTS, TEXTURES (lines 9058-9326; SetCurrentFont/UpdateCurrentFontSize/RegisterFontAtlas/
//   UnregisterFontAtlas/SetPixelDensity already ported elsewhere)
// - [SECTION] INPUTS (lines 9489-11030)
// - [SECTION] ERROR CHECKING, STATE RECOVERY (lines 11030-11469)
// - [SECTION] VIEWPORTS: GetMainViewport() + UpdateViewportsNewFrame() (lines ~16113-16175)
// - [SECTION] LOGGING/CAPTURING (lines 15427-15672)
using System;
using System.Diagnostics;
using System.Text;

namespace ImGui;

public static unsafe partial class ImGui
{
    // ---- imgui.cpp top-of-file statics (referenced by this file) ----
    const float FONT_DEFAULT_SIZE_BASE = 20.0f;
    const float WINDOWS_MOUSE_WHEEL_SCROLL_LOCK_TIMER = 0.70f;  // Lock scrolled window (so it doesn't pick child windows that are scrolling through) for a certain time, unless mouse moved.
    const string IM_NEWLINE = "\n";
    const uint IM_UNICODE_CODEPOINT_MAX = 0xFFFF;               // ImWchar is 16-bit in this port
    const uint IM_UNICODE_CODEPOINT_INVALID = 0xFFFD;

    // Those names are provided for debugging purpose and are not meant to be saved persistently nor compared.
    static readonly string[] GKeyNames =
    {
        "Tab", "LeftArrow", "RightArrow", "UpArrow", "DownArrow", "PageUp", "PageDown",
        "Home", "End", "Insert", "Delete", "Backspace", "Space", "Enter", "Escape",
        "LeftCtrl", "LeftShift", "LeftAlt", "LeftSuper", "RightCtrl", "RightShift", "RightAlt", "RightSuper", "Menu",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F", "G", "H",
        "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24",
        "Apostrophe", "Comma", "Minus", "Period", "Slash", "Semicolon", "Equal", "LeftBracket",
        "Backslash", "RightBracket", "GraveAccent", "CapsLock", "ScrollLock", "NumLock", "PrintScreen",
        "Pause", "Keypad0", "Keypad1", "Keypad2", "Keypad3", "Keypad4", "Keypad5", "Keypad6",
        "Keypad7", "Keypad8", "Keypad9", "KeypadDecimal", "KeypadDivide", "KeypadMultiply",
        "KeypadSubtract", "KeypadAdd", "KeypadEnter", "KeypadEqual",
        "AppBack", "AppForward", "Oem102",
        "GamepadStart", "GamepadBack",
        "GamepadFaceLeft", "GamepadFaceRight", "GamepadFaceUp", "GamepadFaceDown",
        "GamepadDpadLeft", "GamepadDpadRight", "GamepadDpadUp", "GamepadDpadDown",
        "GamepadL1", "GamepadR1", "GamepadL2", "GamepadR2", "GamepadL3", "GamepadR3",
        "GamepadLStickLeft", "GamepadLStickRight", "GamepadLStickUp", "GamepadLStickDown",
        "GamepadRStickLeft", "GamepadRStickRight", "GamepadRStickUp", "GamepadRStickDown",
        "MouseLeft", "MouseRight", "MouseMiddle", "MouseX1", "MouseX2", "MouseWheelX", "MouseWheelY",
        "ModCtrl", "ModShift", "ModAlt", "ModSuper", // ReservedForModXXX are showing the ModXXX names.
    };

    // -------------------------------------------------------------------------
    // [SECTION] INPUTS
    // -------------------------------------------------------------------------

    static ImGuiKeyChord GetModForLRModKey(ImGuiKey key)
    {
        if (key == ImGuiKey_LeftCtrl || key == ImGuiKey_RightCtrl)
            return ImGuiMod_Ctrl;
        if (key == ImGuiKey_LeftShift || key == ImGuiKey_RightShift)
            return ImGuiMod_Shift;
        if (key == ImGuiKey_LeftAlt || key == ImGuiKey_RightAlt)
            return ImGuiMod_Alt;
        if (key == ImGuiKey_LeftSuper || key == ImGuiKey_RightSuper)
            return ImGuiMod_Super;
        return ImGuiMod_None;
    }

    // imgui_internal.h: inline bool IsLRModKey(ImGuiKey key)
    public static bool IsLRModKey(ImGuiKey key) => key >= ImGuiKey_LeftCtrl && key <= ImGuiKey_RightSuper;

    public static ImGuiKeyChord FixupKeyChord(ImGuiKeyChord key_chord)
    {
        // Add ImGuiMod_XXXX when a corresponding ImGuiKey_LeftXXX/ImGuiKey_RightXXX is specified.
        ImGuiKey key = (ImGuiKey)(key_chord & ~ImGuiMod_Mask_);
        if (IsLRModKey(key))
            key_chord |= GetModForLRModKey(key);
        return key_chord;
    }

    // imgui_internal.h: inline ImGuiKeyOwnerData* GetKeyOwnerData(ImGuiContext* ctx, ImGuiKey key)
    public static ref ImGuiKeyOwnerData GetKeyOwnerData(ImGuiContext ctx, ImGuiKey key)
    {
        ImGuiContext g = ctx;
        if ((key & ImGuiMod_Mask_) != 0)
            key = ImGuiUtil.ConvertSingleModFlagToKey(ctx, key);
        Debug.Assert(IsNamedKey(key));
        return ref g.KeysOwnerData[key - ImGuiKey_NamedKey_BEGIN];
    }

    // imgui_internal.h: inline ImGuiKey MouseButtonToKey(ImGuiMouseButton button)
    public static ImGuiKey MouseButtonToKey(ImGuiMouseButton button)
    {
        Debug.Assert(button >= 0 && button < ImGuiMouseButton_COUNT);
        return (ImGuiKey)(ImGuiKey_MouseLeft + button);
    }

    public static string GetKeyName(ImGuiKey key)
    {
        if (key == ImGuiKey_None)
            return "None";
        Debug.Assert(IsNamedKeyOrMod(key), "Support for user key indices was dropped in favor of ImGuiKey. Please update backend and user code.");
        if ((key & ImGuiMod_Mask_) != 0)
            key = ImGuiUtil.ConvertSingleModFlagToKey(GImGui, key);
        if (!IsNamedKey(key))
            return "Unknown";

        return GKeyNames[key - ImGuiKey_NamedKey_BEGIN];
    }

    // Return untranslated names: on macOS, Cmd key will show as Ctrl, Ctrl key will show as super.
    // Lifetime of return value: valid until next call to same function.
    public static string GetKeyChordName(ImGuiKeyChord key_chord)
    {
        ImGuiContext g = GImGui;

        ImGuiKey key = (ImGuiKey)(key_chord & ~ImGuiMod_Mask_);
        if (IsLRModKey(key))
            key_chord &= ~GetModForLRModKey(key); // Return "Ctrl+LeftShift" instead of "Ctrl+Shift+LeftShift"
        string name = (key != ImGuiKey_None || key_chord == ImGuiKey_None) ? GetKeyName(key) : "";
        string s = string.Format("{0}{1}{2}{3}{4}",
            (key_chord & ImGuiMod_Ctrl) != 0 ? "Ctrl+" : "",
            (key_chord & ImGuiMod_Shift) != 0 ? "Shift+" : "",
            (key_chord & ImGuiMod_Alt) != 0 ? "Alt+" : "",
            (key_chord & ImGuiMod_Super) != 0 ? "Super+" : "",
            name);
        if (key == ImGuiKey_None && key_chord != 0 && s.Length > 0)
            s = s.Substring(0, s.Length - 1); // Remove trailing '+'
        g.TempKeychordName = s;
        return s;
    }

    // t0 = previous time (e.g.: g.Time - g.IO.DeltaTime)
    // t1 = current time (e.g.: g.Time)
    // An event is triggered at:
    //  t = 0.0f     t = repeat_delay,    t = repeat_delay + repeat_rate*N
    public static int CalcTypematicRepeatAmount(float t0, float t1, float repeat_delay, float repeat_rate)
    {
        if (t1 == 0.0f)
            return 1;
        if (t0 >= t1)
            return 0;
        if (repeat_rate <= 0.0f)
            return (t0 < repeat_delay && t1 >= repeat_delay) ? 1 : 0;
        int count_t0 = (t0 < repeat_delay) ? -1 : (int)((t0 - repeat_delay) / repeat_rate);
        int count_t1 = (t1 < repeat_delay) ? -1 : (int)((t1 - repeat_delay) / repeat_rate);
        int count = count_t1 - count_t0;
        return count;
    }

    public static void GetTypematicRepeatRate(ImGuiInputFlags flags, ref float repeat_delay, ref float repeat_rate)
    {
        ImGuiContext g = GImGui;
        switch (flags & ImGuiInputFlags_RepeatRateMask_)
        {
            case ImGuiInputFlags_RepeatRateNavMove: repeat_delay = g.IO.KeyRepeatDelay * 0.72f; repeat_rate = g.IO.KeyRepeatRate * 0.80f; return;
            case ImGuiInputFlags_RepeatRateNavTweak: repeat_delay = g.IO.KeyRepeatDelay * 0.72f; repeat_rate = g.IO.KeyRepeatRate * 0.30f; return;
            case ImGuiInputFlags_RepeatRateDefault:
            default: repeat_delay = g.IO.KeyRepeatDelay * 1.00f; repeat_rate = g.IO.KeyRepeatRate * 1.00f; return;
        }
    }

    // Return value representing the number of presses in the last time period, for the given repeat rate
    // (most often returns 0 or 1. The result is generally only >1 when RepeatRate is smaller than DeltaTime, aka large DeltaTime or fast RepeatRate)
    public static int GetKeyPressedAmount(ImGuiKey key, float repeat_delay, float repeat_rate)
    {
        ImGuiContext g = GImGui;
        ImGuiKeyData key_data = GetKeyData(key);
        if (!key_data.Down) // In theory this should already be encoded as (DownDuration < 0.0f), but testing this facilitates eating mechanism (until we finish work on key ownership)
            return 0;
        float t = key_data.DownDuration;
        return CalcTypematicRepeatAmount(t - g.IO.DeltaTime, t, repeat_delay, repeat_rate);
    }

    // Return 2D vector representing the combination of four cardinal direction, with analog value support (for e.g. ImGuiKey_GamepadLStick* values).
    public static ImVec2 GetKeyMagnitude2d(ImGuiKey key_left, ImGuiKey key_right, ImGuiKey key_up, ImGuiKey key_down)
    {
        return new ImVec2(
            GetKeyData(key_right).AnalogValue - GetKeyData(key_left).AnalogValue,
            GetKeyData(key_down).AnalogValue - GetKeyData(key_up).AnalogValue);
    }

    // Rewrite routing data buffers to strip old entries + sort by key to make queries not touch scattered data.
    //   Entries   D,A,B,B,A,C,B     --> A,A,B,B,B,C,D
    //   Index     A:1 B:2 C:5 D:0   --> A:0 B:2 C:5 D:6
    // See 'Metrics->Key Owners & Shortcut Routing' to visualize the result of that operation.
    static void UpdateKeyRoutingTable(ImGuiKeyRoutingTable rt)
    {
        ImGuiContext g = GImGui;
        rt.EntriesNext.resize(0);
        for (ImGuiKey key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key = (ImGuiKey)(key + 1))
        {
            int new_routing_start_idx = rt.EntriesNext.Size;
            for (int old_routing_idx = rt.Index[key - ImGuiKey_NamedKey_BEGIN]; old_routing_idx != -1; old_routing_idx = rt.Entries.Data[old_routing_idx].NextEntryIndex)
            {
                ImGuiKeyRoutingData routing_entry = rt.Entries[old_routing_idx];
                routing_entry.RoutingCurrScore = routing_entry.RoutingNextScore;
                routing_entry.RoutingCurr = routing_entry.RoutingNext; // Update entry
                routing_entry.RoutingNext = ImGuiKeyOwner_NoOwner;
                routing_entry.RoutingNextScore = 0;
                if (routing_entry.RoutingCurr != ImGuiKeyOwner_NoOwner)
                {
                    rt.EntriesNext.push_back(routing_entry); // Write alive ones into new buffer

                    // Apply routing to owner if there's no owner already (RoutingCurr == None at this point)
                    // This is the result of previous frame's SetShortcutRouting() call.
                    if (routing_entry.Mods == g.IO.KeyMods)
                    {
                        ref ImGuiKeyOwnerData owner_data = ref GetKeyOwnerData(g, key);
                        if (owner_data.OwnerCurr == ImGuiKeyOwner_NoOwner)
                        {
                            owner_data.OwnerCurr = routing_entry.RoutingCurr;
                            //IMGUI_DEBUG_LOG("SetKeyOwner(%s, owner_id=0x%08X) via Routing\n", GetKeyName(key), routing_entry.RoutingCurr);
                        }
                    }
                }
                rt.Entries.Data[old_routing_idx] = routing_entry;
            }

            // Rewrite linked-list
            rt.Index[key - ImGuiKey_NamedKey_BEGIN] = (short)(new_routing_start_idx < rt.EntriesNext.Size ? new_routing_start_idx : -1);
            for (int n = new_routing_start_idx; n < rt.EntriesNext.Size; n++)
            {
                ImGuiKeyRoutingData e = rt.EntriesNext[n];
                e.NextEntryIndex = (short)((n + 1 < rt.EntriesNext.Size) ? n + 1 : -1);
                rt.EntriesNext[n] = e;
            }
        }
        (rt.Entries, rt.EntriesNext) = (rt.EntriesNext, rt.Entries); // Swap new and old indexes
    }

    // owner_id may be None/Any, but routing_id needs to be always be set, so we default to GetCurrentFocusScope().
    static ImGuiID GetRoutingIdFromOwnerId(ImGuiID owner_id)
    {
        ImGuiContext g = GImGui;
        return (owner_id != ImGuiKeyOwner_NoOwner && owner_id != ImGuiKeyOwner_Any) ? owner_id : g.CurrentFocusScopeId;
    }

    static ref ImGuiKeyRoutingData GetShortcutRoutingData(ImGuiKeyChord key_chord)
    {
        // Majority of shortcuts will be Key + any number of Mods
        // We accept _Single_ mod with ImGuiKey_None.
        //  - Shortcut(ImGuiKey_S | ImGuiMod_Ctrl);                    // Legal
        //  - Shortcut(ImGuiKey_S | ImGuiMod_Ctrl | ImGuiMod_Shift);   // Legal
        //  - Shortcut(ImGuiMod_Ctrl);                                 // Legal
        //  - Shortcut(ImGuiMod_Ctrl | ImGuiMod_Shift);                // Not legal
        ImGuiContext g = GImGui;
        ImGuiKeyRoutingTable rt = g.KeysRoutingTable;
        ImGuiKey key = (ImGuiKey)(key_chord & ~ImGuiMod_Mask_);
        ImGuiKey mods = (ImGuiKey)(key_chord & ImGuiMod_Mask_);
        if (key == ImGuiKey_None)
            key = ImGuiUtil.ConvertSingleModFlagToKey(g, mods);
        Debug.Assert(IsNamedKey(key));

        // Get (in the majority of case, the linked list will have one element so this should be 2 reads.
        // Subsequent elements will be contiguous in memory as list is sorted/rebuilt in NewFrame).
        for (int idx = rt.Index[key - ImGuiKey_NamedKey_BEGIN]; idx != -1; idx = rt.Entries.Data[idx].NextEntryIndex)
        {
            if (rt.Entries.Data[idx].Mods == mods)
                return ref rt.Entries.Data[idx];
        }

        // Add to linked-list
        int routing_data_idx = rt.Entries.Size;
        rt.Entries.push_back(new ImGuiKeyRoutingData());
        rt.Entries.Data[routing_data_idx].Mods = (ImU16)mods;
        rt.Entries.Data[routing_data_idx].NextEntryIndex = rt.Index[key - ImGuiKey_NamedKey_BEGIN]; // Setup linked list
        rt.Index[key - ImGuiKey_NamedKey_BEGIN] = (short)routing_data_idx;
        return ref rt.Entries.Data[routing_data_idx];
    }

    // Current score encoding
    //  -        0: Never route
    //  -        1: ImGuiInputFlags_RouteGlobal    (lower priority)
    //  - 100..199: ImGuiInputFlags_RouteFocused   (if window in focus-stack)
    //         200: ImGuiInputFlags_RouteGlobal  | ImGuiInputFlags_RouteOverFocused
    //         300: ImGuiInputFlags_RouteActive or ImGuiInputFlags_RouteFocused (if item active)
    //         400: ImGuiInputFlags_RouteGlobal  | ImGuiInputFlags_RouteOverActive
    //  - 500..599: ImGuiInputFlags_RouteFocused | ImGuiInputFlags_RouteOverActive (if window in focus-stack) (higher priority)
    // 'flags' should include an explicit routing policy
    static int CalcRoutingScore(ImGuiID focus_scope_id, ImGuiID owner_id, ImGuiInputFlags flags)
    {
        ImGuiContext g = GImGui;
        if ((flags & ImGuiInputFlags_RouteFocused) != 0)
        {
            // ActiveID gets high priority
            // (we don't check g.ActiveIdUsingAllKeys here. Routing is applied but if input ownership is tested later it may discard it)
            if (owner_id != 0 && g.ActiveId == owner_id)
                return 300;

            // Score based on distance to focused window (lower is better)
            // Assuming both windows are submitting a routing request,
            // - When Window....... is focused -> Window scores 3 (best), Window/ChildB scores 255 (no match)
            // - When Window/ChildB is focused -> Window scores 4,        Window/ChildB scores 3 (best)
            // Assuming only WindowA is submitting a routing request,
            // - When Window/ChildB is focused -> Window scores 4 (best), Window/ChildB doesn't have a score.
            // This essentially follow the window->ParentWindowForFocusRoute chain.
            if (focus_scope_id == 0)
                return 0;
            for (int index_in_focus_path = 0; index_in_focus_path < g.NavFocusRoute.Size; index_in_focus_path++)
                if (g.NavFocusRoute.Data[index_in_focus_path].ID == focus_scope_id)
                {
                    if ((flags & ImGuiInputFlags_RouteOverActive) != 0) // && g.ActiveId != 0 && g.ActiveId != owner_id)
                        return 599 - index_in_focus_path;
                    else
                        return 199 - index_in_focus_path;
                }
            return 0;
        }
        else if ((flags & ImGuiInputFlags_RouteActive) != 0)
        {
            if (owner_id != 0 && g.ActiveId == owner_id)
                return 300;
            return 0;
        }
        else if ((flags & ImGuiInputFlags_RouteGlobal) != 0)
        {
            if ((flags & ImGuiInputFlags_RouteOverActive) != 0)
                return 400;
            if (owner_id != 0 && g.ActiveId == owner_id)
                return 300;
            if ((flags & ImGuiInputFlags_RouteOverFocused) != 0)
                return 200;
            return 1;
        }
        Debug.Assert(false);
        return 0;
    }

    // - We need this to filter some Shortcut() routes when an item e.g. an InputText() is active
    //   e.g. ImGuiKey_G won't be considered a shortcut when item is active, but ImGuiMod|ImGuiKey_G can be.
    // - This is also used by UpdateInputEvents() to avoid trickling in the most common case of e.g. pressing ImGuiKey_G also emitting a G character.
    static bool IsKeyChordPotentiallyCharInput(ImGuiKeyChord key_chord)
    {
        // Mimic 'ignore_char_inputs' logic in InputText()
        ImGuiContext g = GImGui;

        // When the right mods are pressed it cannot be a char input so we won't filter the shortcut out.
        ImGuiKey mods = (ImGuiKey)(key_chord & ImGuiMod_Mask_);
        bool ignore_char_inputs = ((mods & ImGuiMod_Ctrl) != 0 && (mods & ImGuiMod_Alt) == 0) || (g.IO.ConfigMacOSXBehaviors && (mods & ImGuiMod_Ctrl) != 0);
        if (ignore_char_inputs)
            return false;

        // Return true for A-Z, 0-9 and other keys associated to char inputs. Other keys such as F1-F12 won't be filtered.
        ImGuiKey key = (ImGuiKey)(key_chord & ~ImGuiMod_Mask_);
        if (key == ImGuiKey_None)
            return false;
        if (!IsNamedKey(key))
            return false;
        return g.KeysMayBeCharInput[key - ImGuiKey_NamedKey_BEGIN];
    }

    // Request a desired route for an input chord (key + mods).
    // Return true if the route is available this frame.
    // - Routes and key ownership are attributed at the beginning of next frame based on best score and mod state.
    //   (Conceptually this does a "Submit for next frame" + "Test for current frame".
    //   As such, it could be called TrySetXXX or SubmitXXX, or the Submit and Test operations should be separate.)
    public static bool SetShortcutRouting(ImGuiKeyChord key_chord, ImGuiInputFlags flags, ImGuiID owner_id)
    {
        ImGuiContext g = GImGui;
        if ((flags & ImGuiInputFlags_RouteTypeMask_) == 0)
            flags |= ImGuiInputFlags_RouteGlobal | ImGuiInputFlags_RouteOverFocused | ImGuiInputFlags_RouteOverActive; // IMPORTANT: This is the default for SetShortcutRouting() but NOT Shortcut()
        else
            Debug.Assert(ImMath.ImIsPowerOfTwo(flags & ImGuiInputFlags_RouteTypeMask_)); // Check that only 1 routing flag is used
        Debug.Assert(owner_id != ImGuiKeyOwner_Any && owner_id != ImGuiKeyOwner_NoOwner);
        if ((flags & (ImGuiInputFlags_RouteOverFocused | ImGuiInputFlags_RouteUnlessBgFocused)) != 0)
            Debug.Assert((flags & ImGuiInputFlags_RouteGlobal) != 0);
        if ((flags & ImGuiInputFlags_RouteOverActive) != 0)
            Debug.Assert((flags & (ImGuiInputFlags_RouteGlobal | ImGuiInputFlags_RouteFocused)) != 0);

        // Add ImGuiMod_XXXX when a corresponding ImGuiKey_LeftXXX/ImGuiKey_RightXXX is specified.
        key_chord = FixupKeyChord(key_chord);

        // [DEBUG] Debug break requested by user
        if (g.DebugBreakInShortcutRouting == key_chord && System.Diagnostics.Debugger.IsAttached)
            System.Diagnostics.Debugger.Break();

        if ((flags & ImGuiInputFlags_RouteUnlessBgFocused) != 0)
            if (g.NavWindow == null)
                return false;

        // Note how ImGuiInputFlags_RouteAlways won't set routing and thus won't set owner. May want to rework this?
        if ((flags & ImGuiInputFlags_RouteAlways) != 0)
        {
            //IMGUI_DEBUG_LOG_INPUTROUTING("SetShortcutRouting(%s, flags=%04X, owner_id=0x%08X) -> always, no register\n", GetKeyChordName(key_chord), flags, owner_id);
            return true;
        }

        // Specific culling when there's an active item.
        if (g.ActiveId != 0 && g.ActiveId != owner_id)
        {
            if ((flags & ImGuiInputFlags_RouteActive) != 0)
                return false;

            // Cull shortcuts with no modifiers when it could generate a character.
            // e.g. Shortcut(ImGuiKey_G) also generates 'g' character, should not trigger when InputText() is active.
            // but  Shortcut(Ctrl+G) should generally trigger when InputText() is active.
            // TL;DR: lettered shortcut with no mods or with only Alt mod will not trigger while an item reading text input is active.
            // (We cannot filter based on io.InputQueueCharacters[] contents because of trickling and key<>chars submission order are undefined)
            if (g.IO.WantTextInput && IsKeyChordPotentiallyCharInput(key_chord))
            {
                //IMGUI_DEBUG_LOG_INPUTROUTING("SetShortcutRouting(%s, flags=%04X, owner_id=0x%08X) -> filtered as potential char input\n", GetKeyChordName(key_chord), flags, owner_id);
                return false;
            }

            // ActiveIdUsingAllKeyboardKeys trumps all for ActiveId
            if ((flags & ImGuiInputFlags_RouteOverActive) == 0 && g.ActiveIdUsingAllKeyboardKeys)
            {
                ImGuiKey key = (ImGuiKey)(key_chord & ~ImGuiMod_Mask_);
                if (key == ImGuiKey_None)
                    key = ImGuiUtil.ConvertSingleModFlagToKey(g, (ImGuiKey)(key_chord & ImGuiMod_Mask_));
                if (key >= ImGuiUtil.ImGuiKey_Keyboard_BEGIN && key < ImGuiUtil.ImGuiKey_Keyboard_END)
                    return false;
            }
        }

        // Where do we evaluate route for?
        ImGuiID focus_scope_id = g.CurrentFocusScopeId;
        if ((flags & ImGuiInputFlags_RouteFromRootWindow) != 0)
            focus_scope_id = g.CurrentWindow.RootWindow.ID; // See PushFocusScope() call in Begin()

        int score = CalcRoutingScore(focus_scope_id, owner_id, flags);
        //IMGUI_DEBUG_LOG_INPUTROUTING("SetShortcutRouting(%s, flags=%04X, owner_id=0x%08X) -> score %d\n", GetKeyChordName(key_chord), flags, owner_id, score);
        if (score == 0)
            return false;

        // Submit routing for NEXT frame (assuming score is sufficient)
        // FIXME: Could expose a way to use a "serve last" policy for same score resolution (using >= instead of >).
        ref ImGuiKeyRoutingData routing_data = ref GetShortcutRoutingData(key_chord);
        //const bool set_route = (flags & ImGuiInputFlags_ServeLast) ? (score >= routing_data->RoutingNextScore) : (score > routing_data->RoutingNextScore);
        if (score > routing_data.RoutingNextScore)
        {
            routing_data.RoutingNext = owner_id;
            routing_data.RoutingNextScore = (ImU16)score;
        }

        // Return routing state for CURRENT frame
        //if (routing_data.RoutingCurr == owner_id)
        //    IMGUI_DEBUG_LOG_INPUTROUTING("--> granting current route\n");
        return routing_data.RoutingCurr == owner_id;
    }

    // Currently unused by core (but used by tests)
    // Note: this cannot be turned into GetShortcutRouting() because we do the owner_id->routing_id translation, name would be more misleading.
    public static bool TestShortcutRouting(ImGuiKeyChord key_chord, ImGuiID owner_id)
    {
        ImGuiID routing_id = GetRoutingIdFromOwnerId(owner_id);
        key_chord = FixupKeyChord(key_chord);
        ref ImGuiKeyRoutingData routing_data = ref GetShortcutRoutingData(key_chord); // FIXME: Could avoid creating entry.
        return routing_data.RoutingCurr == routing_id;
    }

    // Note that Dear ImGui doesn't know the meaning/semantic of ImGuiKey from 0..511: they are legacy native keycodes.
    // Consider transitioning from 'IsKeyDown(MY_ENGINE_KEY_A)' (<1.87) to IsKeyDown(ImGuiKey_A) (>= 1.87)
    public static bool IsKeyDown(ImGuiKey key, ImGuiID owner_id = ImGuiKeyOwner_Any)
    {
        ImGuiKeyData key_data = GetKeyData(key);
        if (!key_data.Down)
            return false;
        if (!TestKeyOwner(key, owner_id))
            return false;
        return true;
    }

    public static bool IsKeyPressed(ImGuiKey key) => IsKeyPressed(key, true);

    public static bool IsKeyPressed(ImGuiKey key, bool repeat)
    {
        return IsKeyPressed(key, repeat ? ImGuiInputFlags_Repeat : ImGuiInputFlags_None, ImGuiKeyOwner_Any);
    }

    // Important: unlike legacy IsKeyPressed(ImGuiKey, bool repeat=true) which DEFAULT to repeat, this requires EXPLICIT repeat.
    public static bool IsKeyPressed(ImGuiKey key, ImGuiInputFlags flags, ImGuiID owner_id)
    {
        ImGuiKeyData key_data = GetKeyData(key);
        if (!key_data.Down) // In theory this should already be encoded as (DownDuration < 0.0f), but testing this facilitates eating mechanism (until we finish work on key ownership)
            return false;
        float t = key_data.DownDuration;
        if (t < 0.0f)
            return false;
        Debug.Assert((flags & ~ImGuiInputFlags_SupportedByIsKeyPressed) == 0); // Passing flags not supported by this function!
        if ((flags & (ImGuiInputFlags_RepeatRateMask_ | ImGuiInputFlags_RepeatUntilMask_)) != 0) // Setting any _RepeatXXX option enables _Repeat
            flags |= ImGuiInputFlags_Repeat;

        bool pressed = (t == 0.0f);
        if (!pressed && (flags & ImGuiInputFlags_Repeat) != 0)
        {
            float repeat_delay = 0.0f, repeat_rate = 0.0f;
            GetTypematicRepeatRate(flags, ref repeat_delay, ref repeat_rate);
            pressed = (t > repeat_delay) && GetKeyPressedAmount(key, repeat_delay, repeat_rate) > 0;
            if (pressed && (flags & ImGuiInputFlags_RepeatUntilMask_) != 0)
            {
                // Slightly bias 'key_pressed_time' as DownDuration is an accumulation of DeltaTime which we compare to an absolute time value.
                // Ideally we'd replace DownDuration with KeyPressedTime but it would break user's code.
                ImGuiContext g = GImGui;
                double key_pressed_time = g.Time - t + 0.00001f;
                if ((flags & ImGuiInputFlags_RepeatUntilKeyModsChange) != 0 && (g.LastKeyModsChangeTime > key_pressed_time))
                    pressed = false;
                if ((flags & ImGuiInputFlags_RepeatUntilKeyModsChangeFromNone) != 0 && (g.LastKeyModsChangeFromNoneTime > key_pressed_time))
                    pressed = false;
                if ((flags & ImGuiInputFlags_RepeatUntilOtherKeyPress) != 0 && (g.LastKeyboardKeyPressTime > key_pressed_time))
                    pressed = false;
            }
        }
        if (!pressed)
            return false;
        if (!TestKeyOwner(key, owner_id))
            return false;
        return true;
    }

    public static bool IsKeyReleased(ImGuiKey key, ImGuiID owner_id = ImGuiKeyOwner_Any)
    {
        ImGuiKeyData key_data = GetKeyData(key);
        if (key_data.DownDurationPrev < 0.0f || key_data.Down)
            return false;
        if (!TestKeyOwner(key, owner_id))
            return false;
        return true;
    }

    public static bool IsMouseDown(ImGuiMouseButton button, ImGuiID owner_id = ImGuiKeyOwner_Any)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        return g.IO.MouseDown[button] && TestKeyOwner(MouseButtonToKey(button), owner_id); // should be same as IsKeyDown(MouseButtonToKey(button), owner_id), but this allows legacy code hijacking the io.Mousedown[] array.
    }

    public static bool IsMouseClicked(ImGuiMouseButton button, bool repeat = false)
    {
        return IsMouseClicked(button, repeat ? ImGuiInputFlags_Repeat : ImGuiInputFlags_None, ImGuiKeyOwner_Any);
    }

    public static bool IsMouseClicked(ImGuiMouseButton button, ImGuiInputFlags flags, ImGuiID owner_id)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        if (!g.IO.MouseDown[button]) // In theory this should already be encoded as (DownDuration < 0.0f), but testing this facilitates eating mechanism (until we finish work on key ownership)
            return false;
        float t = g.IO.MouseDownDuration[button];
        if (t < 0.0f)
            return false;
        Debug.Assert((flags & ~ImGuiInputFlags_SupportedByIsMouseClicked) == 0); // Passing flags not supported by this function! // FIXME: Could support RepeatRate and RepeatUntil flags here.

        bool repeat = (flags & ImGuiInputFlags_Repeat) != 0;
        bool pressed = (t == 0.0f) || (repeat && t > g.IO.KeyRepeatDelay && CalcTypematicRepeatAmount(t - g.IO.DeltaTime, t, g.IO.KeyRepeatDelay, g.IO.KeyRepeatRate) > 0);
        if (!pressed)
            return false;

        if (!TestKeyOwner(MouseButtonToKey(button), owner_id))
            return false;

        return true;
    }

    public static bool IsMouseReleased(ImGuiMouseButton button, ImGuiID owner_id = ImGuiKeyOwner_Any)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        return g.IO.MouseReleased[button] && TestKeyOwner(MouseButtonToKey(button), owner_id); // Should be same as IsKeyReleased(MouseButtonToKey(button), owner_id)
    }

    // Prefer higher-level helper GetItemClickedCountWithSingleClickDelay()
    // Use if you absolutely need to distinguish single-click from double-click by introducing a delay.
    // Generally use with 'delay >= io.MouseDoubleClickTime' + combined with a 'io.MouseClickedLastCount == 1' test.
    // This is a very rarely used UI idiom, but some apps use this: e.g. MS Explorer single click on an icon to rename.
    public static bool IsMouseReleasedWithDelay(ImGuiMouseButton button, float delay)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        if (IsMouseDown(button))
            return false;
        if (delay < 0.0f)
            delay = g.IO.MouseSingleClickDelay;
        float time_since_release = (float)(g.Time - g.IO.MouseReleasedTime[button]);
        return (time_since_release - g.IO.DeltaTime < delay) && (time_since_release >= delay);
    }

    public static bool IsMouseDoubleClicked(ImGuiMouseButton button, ImGuiID owner_id = ImGuiKeyOwner_Any)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        return g.IO.MouseClickedCount[button] == 2 && TestKeyOwner(MouseButtonToKey(button), owner_id);
    }

    public static int GetMouseClickedCount(ImGuiMouseButton button)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        return g.IO.MouseClickedCount[button];
    }

    // FIXME: This is close to what BeginDragDropSource() is doing, maybe rework.
    static ImGuiID LastItemOverlayButtonForNullId(ImGuiMouseButton mouse_button)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(g.LastItemData.ID == 0);
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = ImGuiUtil.WindowGetIDFromRectangle(window, g.LastItemData.Rect);
        if (g.IO.MouseClicked[mouse_button] && ItemHoverable(g.LastItemData.Rect, id, g.LastItemData.ItemFlags))
        {
            SetActiveID(id, window);
            FocusWindow(window);
        }
        else if (g.ActiveId == id)
        {
            KeepAliveID(id);
            if (!g.IO.MouseDown[mouse_button])
                ClearActiveID();
        }
        return id;
    }

    // [BETA] Building block for disambiguation between single-click and double-click.
    // - Returns 1 on single-click but delayed by io.MouseSingleClickDelay (which is always > io.MouseDoubleClickTime) after mouse release.
    // - Returns 2+ on double-click and subsequent repeated clicks.
    // In order use that to replicate Windows Explorer's "click on label to rename after a delay",
    // When the function returns 1 for a delayed single click, you can add with further tests:
    // - If you want to test that the mouse position AT THE TIME of the click (before the delay): you can use the 'io.MouseClickedPos[mouse_button]' position.
    // - If you want to test that the mouse position is still over the item at the end of delay: you can use '&& IsItemHovered()'.
    // - If you want to test that the item was selected or the sole selection AT THE TIME of the click (before the delay): you can test for 'g.LastActiveIdWasSelected' or 'g.LastActiveIdWasSoleSelected'.
    public static int GetItemClickedCountWithSingleClickDelay(ImGuiMouseButton mouse_button = 0, float delay = -1.0f)
    {
        // Action: double-click and subsequent clicks
        ImGuiContext g = GImGui;
        if (g.IO.MouseClickedCount[mouse_button] >= 2 && IsItemClicked(mouse_button))
            return g.IO.MouseClickedCount[mouse_button];

        // Action: second click, delayed
        ImGuiID id = g.LastItemData.ID;
        if (id == 0)
            id = LastItemOverlayButtonForNullId(mouse_button);
        if (g.LastActiveId == id)
        {
            if (delay >= 0.0f)
                delay = ImMath.ImMax(delay, g.IO.MouseDoubleClickTime + 0.01f);
            if (IsMouseReleasedWithDelay(mouse_button, delay) && g.IO.MouseClickedLastCount[mouse_button] == 1)
                return 1;
        }
        return 0;
    }

    // Test if mouse cursor is hovering given rectangle
    // NB- Rectangle is clipped by our current clip setting
    // NB- Expand the rectangle to be generous on imprecise inputs systems (g.Style.TouchExtraPadding)
    public static bool IsMouseHoveringRect(ImVec2 r_min, ImVec2 r_max, bool clip = true)
    {
        ImGuiContext g = GImGui;

        // Clip
        ImRect rect_clipped = new ImRect(r_min, r_max);
        if (clip)
            rect_clipped.ClipWith(g.CurrentWindow.ClipRect);

        // Hit testing, expanded for touch input (ImRect::ContainsWithPad)
        ImVec2 pad = g.Style.TouchExtraPadding;
        ImVec2 p = g.IO.MousePos;
        if (!(p.x >= rect_clipped.Min.x - pad.x && p.y >= rect_clipped.Min.y - pad.y && p.x < rect_clipped.Max.x + pad.x && p.y < rect_clipped.Max.y + pad.y))
            return false;
        return true;
    }

    // Return if a mouse click/drag went past the given threshold. Valid to call during the MouseReleased frame.
    // [Internal] This doesn't test if the button is pressed
    public static bool IsMouseDragPastThreshold(ImGuiMouseButton button, float lock_threshold = -1.0f)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        if (lock_threshold < 0.0f)
            lock_threshold = g.IO.MouseDragThreshold;
        return g.IO.MouseDragMaxDistanceSqr[button] >= lock_threshold * lock_threshold;
    }

    public static bool IsMouseDragging(ImGuiMouseButton button, float lock_threshold = -1.0f)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        if (!g.IO.MouseDown[button])
            return false;
        return IsMouseDragPastThreshold(button, lock_threshold);
    }

    public static ImVec2 GetMousePos()
    {
        ImGuiContext g = GImGui;
        return g.IO.MousePos;
    }

    // This is called TeleportMousePos() and not SetMousePos() to emphasis that setting MousePosPrev will effectively clear mouse delta as well.
    // It is expected you only call this if (io.BackendFlags & ImGuiBackendFlags_HasSetMousePos) is set and supported by backend.
    public static void TeleportMousePos(ImVec2 pos)
    {
        ImGuiContext g = GImGui;
        g.IO.MousePos = g.IO.MousePosPrev = pos;
        g.IO.MouseDelta = new ImVec2(0.0f, 0.0f);
        g.IO.WantSetMousePos = true;
        //IMGUI_DEBUG_LOG_IO("TeleportMousePos: (%.1f,%.1f)\n", io.MousePos.x, io.MousePos.y);
    }

    // NB: prefer to call right after BeginPopup(). At the time Selectable/MenuItem is activated, the popup is already closed!
    public static ImVec2 GetMousePosOnOpeningCurrentPopup()
    {
        ImGuiContext g = GImGui;
        if (g.BeginPopupStack.Size > 0)
            return g.OpenPopupStack[g.BeginPopupStack.Size - 1].OpenMousePos;
        return g.IO.MousePos;
    }

    // We typically use ImVec2(-FLT_MAX,-FLT_MAX) to denote an invalid mouse position.
    public static bool IsMousePosValid(ref ImVec2 mouse_pos)
    {
        // The assert is only to silence a false-positive in XCode Static Analysis.
        // Because GImGui is not dereferenced in every code path, the static analyzer assume that it may be NULL (which it doesn't for other functions).
        Debug.Assert(GImGui != null);
        const float MOUSE_INVALID = -256000.0f;
        ImVec2 p = mouse_pos;
        return p.x >= MOUSE_INVALID && p.y >= MOUSE_INVALID;
    }

    public static bool IsMousePosValid() => IsMousePosValid(ref GImGui.IO.MousePos);

    // [WILL OBSOLETE] This was designed for backends, but prefer having backend maintain a mask of held mouse buttons, because upcoming input queue system will make this invalid.
    public static bool IsAnyMouseDown()
    {
        ImGuiContext g = GImGui;
        for (int n = 0; n < g.IO.MouseDown.Length; n++)
            if (g.IO.MouseDown[n])
                return true;
        return false;
    }

    // Return the delta from the initial clicking position while the mouse button is clicked or was just released.
    // This is locked and return 0.0f until the mouse moves past a distance threshold at least once.
    // NB: This is only valid if IsMousePosValid(). backends in theory should always keep mouse position valid when dragging even outside the client window.
    public static ImVec2 GetMouseDragDelta(ImGuiMouseButton button = 0, float lock_threshold = -1.0f)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        if (lock_threshold < 0.0f)
            lock_threshold = g.IO.MouseDragThreshold;
        if (g.IO.MouseDown[button] || g.IO.MouseReleased[button])
            if (g.IO.MouseDragMaxDistanceSqr[button] >= lock_threshold * lock_threshold)
            {
                ImVec2 mouse_pos = g.IO.MousePos;
                ImVec2 clicked_pos = g.IO.MouseClickedPos[button];
                if (IsMousePosValid(ref mouse_pos) && IsMousePosValid(ref clicked_pos))
                    return mouse_pos - clicked_pos;
            }
        return new ImVec2(0.0f, 0.0f);
    }

    public static void ResetMouseDragDelta(ImGuiMouseButton button = 0)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(button >= 0 && button < g.IO.MouseDown.Length);
        // NB: We don't need to reset g.IO.MouseDragMaxDistanceSqr
        g.IO.MouseClickedPos[button] = g.IO.MousePos;
    }

    // Get desired mouse cursor shape.
    // Important: this is meant to be used by a platform backend, it is reset in ImGui::NewFrame(),
    // updated during the frame, and locked in EndFrame()/Render().
    // If you use software rendering by setting io.MouseDrawCursor then Dear ImGui will render those for you
    public static ImGuiMouseCursor GetMouseCursor()
    {
        ImGuiContext g = GImGui;
        return g.MouseCursor;
    }

    // We intentionally accept values of ImGuiMouseCursor that are outside our bounds, in case users needs to hack-in a custom cursor value.
    // Custom cursors may be handled by custom backends. If you are using a standard backend and want to hack in a custom cursor, you may
    // handle it before the backend _NewFrame() call and temporarily set ImGuiConfigFlags_NoMouseCursorChange during the backend _NewFrame() call.
    public static void SetMouseCursor(ImGuiMouseCursor cursor_type)
    {
        ImGuiContext g = GImGui;
        g.MouseCursor = cursor_type;
    }

    static void UpdateAliasKey(ImGuiKey key, bool v, float analog_value)
    {
        Debug.Assert(IsAliasKey(key));
        // (alias keys are named keys: index directly into KeysData storage)
        ref ImGuiKeyData key_data = ref GImGui.IO.KeysData[key - ImGuiKey_NamedKey_BEGIN];
        key_data.Down = v;
        key_data.AnalogValue = analog_value;
    }

    // [Internal] Do not use directly
    static ImGuiKeyChord GetMergedModsFromKeys()
    {
        // Bypass IsKeyDown() for the unlikely case where user used a ImGuiInputFlags_LockXXXX on those.
        ImGuiKeyChord mods = 0;
        if (GetKeyData(GImGui, (ImGuiKey)ImGuiMod_Ctrl).Down) { mods |= ImGuiMod_Ctrl; }
        if (GetKeyData(GImGui, (ImGuiKey)ImGuiMod_Shift).Down) { mods |= ImGuiMod_Shift; }
        if (GetKeyData(GImGui, (ImGuiKey)ImGuiMod_Alt).Down) { mods |= ImGuiMod_Alt; }
        if (GetKeyData(GImGui, (ImGuiKey)ImGuiMod_Super).Down) { mods |= ImGuiMod_Super; }
        return mods;
    }

    public static void UpdateKeyboardInputs()
    {
        ImGuiContext g = GImGui;
        ImGuiIO io = g.IO;

        if ((io.ConfigFlags & ImGuiConfigFlags_NoKeyboard) != 0)
            io.ClearInputKeys();

        // Update aliases
        for (int n = 0; n < ImGuiMouseButton_COUNT; n++)
            UpdateAliasKey(MouseButtonToKey(n), io.MouseDown[n], io.MouseDown[n] ? 1.0f : 0.0f);
        UpdateAliasKey(ImGuiKey_MouseWheelX, io.MouseWheelH != 0.0f, io.MouseWheelH);
        UpdateAliasKey(ImGuiKey_MouseWheelY, io.MouseWheel != 0.0f, io.MouseWheel);

        // Synchronize io.KeyMods and io.KeyCtrl/io.KeyShift/etc. values.
        // - New backends (1.87+): send io.AddKeyEvent(ImGuiMod_XXX) ->                                      -> (here) deriving io.KeyMods + io.KeyXXX from key array.
        // - Legacy backends:      set io.KeyXXX bools               -> (above) set key array from io.KeyXXX -> (here) deriving io.KeyMods + io.KeyXXX from key array.
        // So with legacy backends the 4 values will do a unnecessary back-and-forth but it makes the code simpler and future facing.
        ImGuiKeyChord prev_key_mods = io.KeyMods;
        io.KeyMods = GetMergedModsFromKeys();
        io.KeyCtrl = (io.KeyMods & ImGuiMod_Ctrl) != 0;
        io.KeyShift = (io.KeyMods & ImGuiMod_Shift) != 0;
        io.KeyAlt = (io.KeyMods & ImGuiMod_Alt) != 0;
        io.KeySuper = (io.KeyMods & ImGuiMod_Super) != 0;
        if (prev_key_mods != io.KeyMods)
            g.LastKeyModsChangeTime = g.Time;
        if (prev_key_mods != io.KeyMods && prev_key_mods == 0)
            g.LastKeyModsChangeFromNoneTime = g.Time;

        // Clear gamepad data if disabled
        if ((io.BackendFlags & ImGuiBackendFlags_HasGamepad) == 0)
            for (int key = ImGuiUtil.ImGuiKey_Gamepad_BEGIN; key < ImGuiUtil.ImGuiKey_Gamepad_END; key++)
            {
                io.KeysData[key - ImGuiKey_NamedKey_BEGIN].Down = false;
                io.KeysData[key - ImGuiKey_NamedKey_BEGIN].AnalogValue = 0.0f;
            }

        // Update keys
        for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++)
        {
            ref ImGuiKeyData key_data = ref io.KeysData[key - ImGuiKey_NamedKey_BEGIN];
            key_data.DownDurationPrev = key_data.DownDuration;
            key_data.DownDuration = key_data.Down ? (key_data.DownDuration < 0.0f ? 0.0f : key_data.DownDuration + io.DeltaTime) : -1.0f;
            if (key_data.DownDuration == 0.0f)
            {
                if (IsKeyboardKey((ImGuiKey)key))
                    g.LastKeyboardKeyPressTime = g.Time;
                else if (key == ImGuiKey_ReservedForModCtrl || key == ImGuiKey_ReservedForModShift || key == ImGuiKey_ReservedForModAlt || key == ImGuiKey_ReservedForModSuper)
                    g.LastKeyboardKeyPressTime = g.Time;
            }
        }

        // Update keys/input owner (named keys only): one entry per key
        for (ImGuiKey key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key = (ImGuiKey)(key + 1))
        {
            ref ImGuiKeyData key_data = ref io.KeysData[key - ImGuiKey_NamedKey_BEGIN];
            ref ImGuiKeyOwnerData owner_data = ref g.KeysOwnerData[key - ImGuiKey_NamedKey_BEGIN];
            owner_data.OwnerCurr = owner_data.OwnerNext;
            if (!key_data.Down) // Important: ownership is released on the frame after a release. Ensure a 'MouseDown -> CloseWindow -> MouseUp' chain doesn't lead to someone else seeing the MouseUp.
                owner_data.OwnerNext = ImGuiKeyOwner_NoOwner;
            owner_data.LockThisFrame = owner_data.LockUntilRelease = owner_data.LockUntilRelease && key_data.Down;  // Clear LockUntilRelease when key is not Down anymore
        }

        // Update key routing (for e.g. shortcuts)
        UpdateKeyRoutingTable(g.KeysRoutingTable);
    }

    public static void UpdateMouseInputs()
    {
        ImGuiContext g = GImGui;
        ImGuiIO io = g.IO;

        // Mouse Wheel swapping flag
        // As a standard behavior holding Shift while using Vertical Mouse Wheel triggers Horizontal scroll instead
        // - We avoid doing it on OSX as it the OS input layer handles this already.
        // - FIXME: However this means when running on OSX over Emscripten, Shift+WheelY will incur two swapping (1 in OS, 1 here), canceling the feature.
        // - FIXME: When we can distinguish e.g. touchpad scroll events from mouse ones, we'll set this accordingly based on input source.
        io.MouseWheelRequestAxisSwap = io.KeyShift && !io.ConfigMacOSXBehaviors;

        // Round mouse position to avoid spreading non-rounded position (e.g. UpdateManualResize doesn't support them well)
        if (IsMousePosValid(ref io.MousePos))
            io.MousePos = g.MouseLastValidPos = ImMath.ImFloor(io.MousePos);

        // If mouse just appeared or disappeared (usually denoted by -FLT_MAX components) we cancel out movement in MouseDelta
        if (IsMousePosValid(ref io.MousePos) && IsMousePosValid(ref io.MousePosPrev))
            io.MouseDelta = io.MousePos - io.MousePosPrev;
        else
            io.MouseDelta = new ImVec2(0.0f, 0.0f);

        // Update stationary timer.
        // FIXME: May need to rework again to have some tolerance for occasional small movement, while being functional on high-framerates.
        float mouse_stationary_threshold = (io.MouseSource == ImGuiMouseSource_Mouse) ? 2.0f : 3.0f; // Slightly higher threshold for ImGuiMouseSource_TouchScreen/ImGuiMouseSource_Pen, may need rework.
        bool mouse_stationary = (ImUtil.ImLengthSqr(io.MouseDelta) <= mouse_stationary_threshold * mouse_stationary_threshold);
        g.MouseStationaryTimer = mouse_stationary ? (g.MouseStationaryTimer + io.DeltaTime) : 0.0f;
        //IMGUI_DEBUG_LOG("%.4f\n", g.MouseStationaryTimer);

        // If mouse moved we re-enable mouse hovering in case it was disabled by keyboard/gamepad. In theory should use a >0.0f threshold but would need to reset in everywhere we set this to true.
        if (io.MouseDelta.x != 0.0f || io.MouseDelta.y != 0.0f)
            g.NavHighlightItemUnderNav = false;

        for (int i = 0; i < io.MouseDown.Length; i++)
        {
            io.MouseClicked[i] = io.MouseDown[i] && io.MouseDownDuration[i] < 0.0f;
            io.MouseClickedCount[i] = 0; // Will be filled below
            io.MouseReleased[i] = !io.MouseDown[i] && io.MouseDownDuration[i] >= 0.0f;
            if (io.MouseReleased[i])
                io.MouseReleasedTime[i] = g.Time;
            io.MouseDownDurationPrev[i] = io.MouseDownDuration[i];
            io.MouseDownDuration[i] = io.MouseDown[i] ? (io.MouseDownDuration[i] < 0.0f ? 0.0f : io.MouseDownDuration[i] + io.DeltaTime) : -1.0f;
            if (io.MouseClicked[i])
            {
                bool is_repeated_click = false;
                if ((float)(g.Time - io.MouseClickedTime[i]) < io.MouseDoubleClickTime)
                {
                    ImVec2 delta_from_click_pos = IsMousePosValid(ref io.MousePos) ? (io.MousePos - io.MouseClickedPos[i]) : new ImVec2(0.0f, 0.0f);
                    if (ImUtil.ImLengthSqr(delta_from_click_pos) < io.MouseDoubleClickMaxDist * io.MouseDoubleClickMaxDist)
                        is_repeated_click = true;
                }
                if (is_repeated_click)
                    io.MouseClickedLastCount[i]++;
                else
                    io.MouseClickedLastCount[i] = 1;
                io.MouseClickedTime[i] = g.Time;
                io.MouseClickedPos[i] = io.MousePos;
                io.MouseClickedCount[i] = io.MouseClickedLastCount[i];
                io.MouseDragMaxDistanceSqr[i] = 0.0f;
            }
            else if (io.MouseDown[i])
            {
                // Maintain the maximum distance we reaching from the initial click position, which is used with dragging threshold
                float delta_sqr_click_pos = IsMousePosValid(ref io.MousePos) ? ImUtil.ImLengthSqr(io.MousePos - io.MouseClickedPos[i]) : 0.0f;
                io.MouseDragMaxDistanceSqr[i] = ImMath.ImMax(io.MouseDragMaxDistanceSqr[i], delta_sqr_click_pos);
            }

            // We provide io.MouseDoubleClicked[] as a legacy service
            io.MouseDoubleClicked[i] = (io.MouseClickedCount[i] == 2);

            // Clicking any mouse button reactivate mouse hovering which may have been deactivated by keyboard/gamepad navigation
            if (io.MouseClicked[i])
                g.NavHighlightItemUnderNav = false;
        }
    }

    static void LockWheelingWindow(ImGuiWindow window, float wheel_amount)
    {
        ImGuiContext g = GImGui;
        if (window != null)
            g.WheelingWindowReleaseTimer = ImMath.ImMin(g.WheelingWindowReleaseTimer + MathF.Abs(wheel_amount) * WINDOWS_MOUSE_WHEEL_SCROLL_LOCK_TIMER, WINDOWS_MOUSE_WHEEL_SCROLL_LOCK_TIMER);
        else
            g.WheelingWindowReleaseTimer = 0.0f;
        if (g.WheelingWindow == window)
            return;
        //IMGUI_DEBUG_LOG_IO("[io] LockWheelingWindow() \"%s\"\n", window ? window.Name : "NULL");
        g.WheelingWindow = window;
        g.WheelingWindowRefMousePos = g.IO.MousePos;
        if (window == null)
        {
            g.WheelingWindowStartFrame = -1;
            g.WheelingAxisAvg = new ImVec2(0.0f, 0.0f);
        }
    }

    // imgui_internal.h: static inline float ImExponentialMovingAverage(float avg, float sample, int n)
    static float ImExponentialMovingAverage(float avg, float sample, int n)
    {
        avg -= avg / (float)n;
        avg += sample / (float)n;
        return avg;
    }

    static ImGuiWindow FindBestWheelingWindow(ImVec2 wheel)
    {
        // For each axis, find window in the hierarchy that may want to use scrolling
        ImGuiContext g = GImGui;
        ImGuiWindow[] windows = new ImGuiWindow[2];
        for (int axis = 0; axis < 2; axis++)
            if (wheel[axis] != 0.0f)
                for (ImGuiWindow window = windows[axis] = g.HoveredWindow; window != null && (window.Flags & ImGuiWindowFlags_ChildWindow) != 0; window = windows[axis] = window.ParentWindow)
                {
                    // Bubble up into parent window if:
                    // - a child window doesn't allow any scrolling.
                    // - a child window has the ImGuiWindowFlags_NoScrollWithMouse flag.
                    //// - a child window doesn't need scrolling because it is already at the edge for the direction we are going in (FIXME-WIP)
                    bool has_scrolling = (window.ScrollMax[axis] != 0.0f);
                    bool inputs_disabled = (window.Flags & ImGuiWindowFlags_NoScrollWithMouse) != 0 && (window.Flags & ImGuiWindowFlags_NoMouseInputs) == 0;
                    //const bool scrolling_past_limit = (wheel_v < 0.0f) ? (window->Scroll[axis] <= 0.0f) : (window->Scroll[axis] >= window->ScrollMax[axis]);
                    if (has_scrolling && !inputs_disabled) // && !scrolling_past_limits)
                        break; // select this window
                }
        if (windows[0] == null && windows[1] == null)
            return null;

        // If there's only one window or only one axis then there's no ambiguity
        if (windows[0] == windows[1] || windows[0] == null || windows[1] == null)
            return windows[1] != null ? windows[1] : windows[0];

        // If candidate are different windows we need to decide which one to prioritize
        // - First frame: only find a winner if one axis is zero.
        // - Subsequent frames: only find a winner when one is more than the other.
        if (g.WheelingWindowStartFrame == -1)
            g.WheelingWindowStartFrame = g.FrameCount;
        if ((g.WheelingWindowStartFrame == g.FrameCount && wheel.x != 0.0f && wheel.y != 0.0f) || (g.WheelingAxisAvg.x == g.WheelingAxisAvg.y))
        {
            g.WheelingWindowWheelRemainder = wheel;
            return null;
        }
        return (g.WheelingAxisAvg.x > g.WheelingAxisAvg.y) ? windows[0] : windows[1];
    }

    // Called by NewFrame()
    public static void UpdateMouseWheel()
    {
        // Reset the locked window if we move the mouse or after the timer elapses.
        // FIXME: Ideally we could refactor to have one timer for "changing window w/ same axis" and a shorter timer for "changing window or axis w/ other axis" (#3795)
        ImGuiContext g = GImGui;
        if (g.WheelingWindow != null)
        {
            g.WheelingWindowReleaseTimer -= g.IO.DeltaTime;
            ImVec2 mouse_pos = g.IO.MousePos;
            if (IsMousePosValid(ref mouse_pos) && ImUtil.ImLengthSqr(g.IO.MousePos - g.WheelingWindowRefMousePos) > g.IO.MouseDragThreshold * g.IO.MouseDragThreshold)
                g.WheelingWindowReleaseTimer = 0.0f;
            if (g.WheelingWindowReleaseTimer <= 0.0f)
                LockWheelingWindow(null, 0.0f);
        }

        ImGuiWindow mouse_window = g.WheelingWindow != null ? g.WheelingWindow : g.HoveredWindow;
        if (mouse_window == null || mouse_window.Collapsed)
            return;

        ImGuiID owner_id = mouse_window.ID;
        ImVec2 wheel;
        wheel.x = TestKeyOwner(ImGuiKey_MouseWheelX, owner_id) ? g.IO.MouseWheelH : 0.0f;
        wheel.y = TestKeyOwner(ImGuiKey_MouseWheelY, owner_id) ? g.IO.MouseWheel : 0.0f;
        //IMGUI_DEBUG_LOG("MouseWheel X:%.3f Y:%.3f\n", wheel.x, wheel.y);
        if (g.WheelingWindow != null)
        {
            SetKeyOwner(ImGuiKey_MouseWheelX, owner_id);
            SetKeyOwner(ImGuiKey_MouseWheelY, owner_id);
        }

        // Zoom / Scale window
        // FIXME-OBSOLETE: This is an old feature, it still works but pretty much nobody is using it and may be best redesigned.
        if (wheel.y != 0.0f && g.IO.KeyCtrl && g.IO.FontAllowUserScaling)
        {
            LockWheelingWindow(mouse_window, wheel.y);
            ImGuiWindow window = mouse_window;
            float new_font_scale = ImMath.ImClamp(window.FontWindowScale + g.IO.MouseWheel * 0.10f, 0.50f, 2.50f);
            float scale = new_font_scale / window.FontWindowScale;
            window.FontWindowScale = new_font_scale;
            if (window == window.RootWindow)
            {
                ImVec2 t1 = window.Size * (1.0f - scale);
                ImVec2 t2 = ImMath.ImMul(t1, g.IO.MousePos - window.Pos);
                ImVec2 offset = new ImVec2(t2.x / window.Size.x, t2.y / window.Size.y);
                SetWindowPos(window, window.Pos + offset, 0);
                window.Size = ImMath.ImTrunc(window.Size * scale); // FIXME: Legacy-ish code, call SetWindowSize()?
                window.SizeFull = ImMath.ImTrunc(window.SizeFull * scale);
                MarkIniSettingsDirty(window);
            }
            return;
        }
        if (g.IO.KeyCtrl)
            return;

        // Mouse wheel scrolling
        // Read about io.MouseWheelRequestAxisSwap and its issue on Mac+Emscripten in UpdateMouseInputs()
        if (g.IO.MouseWheelRequestAxisSwap)
            wheel = new ImVec2(wheel.y, 0.0f);

        // Maintain a rough average of moving magnitude on both axes
        // FIXME: should by based on wall clock time rather than frame-counter
        g.WheelingAxisAvg.x = ImExponentialMovingAverage(g.WheelingAxisAvg.x, MathF.Abs(wheel.x), 30);
        g.WheelingAxisAvg.y = ImExponentialMovingAverage(g.WheelingAxisAvg.y, MathF.Abs(wheel.y), 30);

        // In the rare situation where FindBestWheelingWindow() had to defer first frame of wheeling due to ambiguous main axis, reinject it now.
        wheel += g.WheelingWindowWheelRemainder;
        g.WheelingWindowWheelRemainder = new ImVec2(0.0f, 0.0f);
        if (wheel.x == 0.0f && wheel.y == 0.0f)
            return;

        // Mouse wheel scrolling: find target and apply
        // - don't renew lock if axis doesn't apply on the window.
        // - select a main axis when both axes are being moved.
        ImGuiWindow target_window = g.WheelingWindow != null ? g.WheelingWindow : FindBestWheelingWindow(wheel);
        if (target_window != null)
            if ((target_window.Flags & ImGuiWindowFlags_NoScrollWithMouse) == 0 && (target_window.Flags & ImGuiWindowFlags_NoMouseInputs) == 0)
            {
                Span<bool> do_scroll = stackalloc bool[] { wheel.x != 0.0f && target_window.ScrollMax.x != 0.0f, wheel.y != 0.0f && target_window.ScrollMax.y != 0.0f };
                if (do_scroll[ImGuiAxis_X] && do_scroll[ImGuiAxis_Y])
                    do_scroll[(g.WheelingAxisAvg.x > g.WheelingAxisAvg.y) ? ImGuiAxis_Y : ImGuiAxis_X] = false;
                if (do_scroll[ImGuiAxis_X])
                {
                    LockWheelingWindow(target_window, wheel.x);
                    float max_step = target_window.InnerRect.GetWidth() * 0.67f;
                    float scroll_step = ImMath.ImTrunc(ImMath.ImMin(2 * target_window.FontRefSize, max_step));
                    SetScrollX(target_window, target_window.Scroll.x - wheel.x * scroll_step);
                    g.WheelingWindowScrolledFrame = g.FrameCount;
                }
                if (do_scroll[ImGuiAxis_Y])
                {
                    LockWheelingWindow(target_window, wheel.y);
                    float max_step = target_window.InnerRect.GetHeight() * 0.67f;
                    float scroll_step = ImMath.ImTrunc(ImMath.ImMin(5 * target_window.FontRefSize, max_step));
                    SetScrollY(target_window, target_window.Scroll.y - wheel.y * scroll_step);
                    g.WheelingWindowScrolledFrame = g.FrameCount;
                }
            }
    }

    public static void SetNextFrameWantCaptureKeyboard(bool want_capture_keyboard)
    {
        ImGuiContext g = GImGui;
        g.WantCaptureKeyboardNextFrame = want_capture_keyboard ? 1 : 0;
    }

    public static void SetNextFrameWantCaptureMouse(bool want_capture_mouse)
    {
        ImGuiContext g = GImGui;
        g.WantCaptureMouseNextFrame = want_capture_mouse ? 1 : 0;
    }

    // Process input queue
    // We always call this with the value of 'bool g.IO.ConfigInputTrickleEventQueue'.
    // - trickle_fast_inputs = false : process all events, turn into flattened input state (e.g. successive down/up/down/up will be lost)
    // - trickle_fast_inputs = true  : process as many events as possible (successive down/up/down/up will be trickled over several frames so nothing is lost) (new feature in 1.87)
    public static void UpdateInputEvents(bool trickle_fast_inputs)
    {
        ImGuiContext g = GImGui;
        ImGuiIO io = g.IO;

        // Only trickle chars<>key when working with InputText()
        // FIXME: InputText() could parse event trail?
        // FIXME: Could specialize chars<>keys trickling rules for control keys (those not typically associated to characters)
        bool trickle_interleaved_nonchar_keys_and_text = (trickle_fast_inputs && g.WantTextInputNextFrame == 1);

        bool mouse_moved = false, mouse_wheeled = false, key_changed = false, key_changed_nonchar = false, text_inputted = false;
        int mouse_button_changed = 0x00;
        bool[] key_changed_mask = new bool[ImGuiKey_NamedKey_COUNT]; // (ImBitArray<ImGuiKey_NamedKey_COUNT> in C++)

        int event_n = 0;
        for (; event_n < g.InputEventsQueue.Size; event_n++)
        {
            ImGuiInputEvent e = g.InputEventsQueue[event_n];
            if (e.Type == ImGuiInputEventType_MousePos)
            {
                if (g.IO.WantSetMousePos)
                    continue;
                // Trickling Rule: Stop processing queued events if we already handled a mouse button change
                ImVec2 event_pos = new ImVec2(e.MousePos_PosX, e.MousePos_PosY);
                if (trickle_fast_inputs && (mouse_button_changed != 0 || mouse_wheeled || key_changed || text_inputted))
                    break;
                io.MousePos = event_pos;
                io.MouseSource = e.MousePos_MouseSource;
                mouse_moved = true;
            }
            else if (e.Type == ImGuiInputEventType_MouseButton)
            {
                // Trickling Rule: Stop processing queued events if we got multiple action on the same button
                ImGuiMouseButton button = e.MouseButton_Button;
                Debug.Assert(button >= 0 && button < ImGuiMouseButton_COUNT);
                if (trickle_fast_inputs && ((mouse_button_changed & (1 << button)) != 0 || mouse_wheeled))
                    break;
                if (trickle_fast_inputs && e.MouseButton_MouseSource == ImGuiMouseSource_TouchScreen && mouse_moved) // #2702: TouchScreen have no initial hover.
                    break;
                io.MouseDown[button] = e.MouseButton_Down;
                io.MouseSource = e.MouseButton_MouseSource;
                mouse_button_changed |= (1 << button);
            }
            else if (e.Type == ImGuiInputEventType_MouseWheel)
            {
                // Trickling Rule: Stop processing queued events if we got multiple action on the event
                if (trickle_fast_inputs && (mouse_moved || mouse_button_changed != 0))
                    break;
                io.MouseWheelH += e.MouseWheel_WheelX;
                io.MouseWheel += e.MouseWheel_WheelY;
                io.MouseSource = e.MouseWheel_MouseSource;
                mouse_wheeled = true;
            }
            else if (e.Type == ImGuiInputEventType_Key)
            {
                // Trickling Rule: Stop processing queued events if we got multiple action on the same button
                if ((io.ConfigFlags & ImGuiConfigFlags_NoKeyboard) != 0)
                    continue;
                ImGuiKey key = e.Key_Key;
                Debug.Assert(key != ImGuiKey_None);
                // (GetKeyData(): special storage location for mods)
                ImGuiKey key_storage = key;
                if ((key & ImGuiMod_Mask_) != 0)
                    key_storage = ImGuiUtil.ConvertSingleModFlagToKey(g, key);
                int key_data_index = key_storage - ImGuiKey_NamedKey_BEGIN;
                ImGuiKeyData key_data = io.KeysData[key_data_index];
                if (trickle_fast_inputs && key_data.Down != e.Key_Down && (key_changed_mask[key_data_index] || mouse_button_changed != 0))
                    break;

                bool key_is_potentially_for_char_input = IsKeyChordPotentiallyCharInput(GetMergedModsFromKeys() | key);
                if (trickle_interleaved_nonchar_keys_and_text && (text_inputted && !key_is_potentially_for_char_input))
                    break;

                if (key_data.Down != e.Key_Down) // Analog change only do not trigger this, so it won't block e.g. further mouse pos events testing key_changed.
                {
                    key_changed = true;
                    key_changed_mask[key_data_index] = true;
                    if (trickle_interleaved_nonchar_keys_and_text && !key_is_potentially_for_char_input)
                        key_changed_nonchar = true;
                }

                key_data.Down = e.Key_Down;
                key_data.AnalogValue = e.Key_AnalogValue;
                io.KeysData[key_data_index] = key_data;
            }
            else if (e.Type == ImGuiInputEventType_Text)
            {
                if ((io.ConfigFlags & ImGuiConfigFlags_NoKeyboard) != 0)
                    continue;
                // Trickling Rule: Stop processing queued events if keys/mouse have been interacted with
                if (trickle_fast_inputs && (mouse_button_changed != 0 || mouse_moved || mouse_wheeled))
                    break;
                if (trickle_interleaved_nonchar_keys_and_text && key_changed_nonchar)
                    break;
                uint c = e.Text_Char;
                io.InputQueueCharacters.push_back(c <= IM_UNICODE_CODEPOINT_MAX ? (ImWchar)c : (ImWchar)IM_UNICODE_CODEPOINT_INVALID);
                if (trickle_interleaved_nonchar_keys_and_text)
                    text_inputted = true;
            }
            else if (e.Type == ImGuiInputEventType_Focus)
            {
                // We intentionally overwrite this and process in NewFrame(), in order to give a chance
                // to multi-viewports backends to queue AddFocusEvent(false) + AddFocusEvent(true) in same frame.
                bool focus_lost = !e.AppFocused_Focused;
                io.AppFocusLost = focus_lost;
            }
            else
            {
                Debug.Assert(false, "Unknown event!");
            }
        }

        // Record trail (for domain-specific applications wanting to access a precise trail)
        //if (event_n != 0) IMGUI_DEBUG_LOG_IO("Processed: %d / Remaining: %d\n", event_n, g.InputEventsQueue.Size - event_n);
        for (int n = 0; n < event_n; n++)
            g.InputEventsTrail.push_back(g.InputEventsQueue[n]);

        // Remaining events will be processed on the next frame
        // FIXME-MULTITHREADING: io.AddKeyEvent() etc. calls are mostly thread-safe apart from the fact they push to this
        // queue which may be resized here. Could potentially rework this to narrow down the section needing a mutex? (#5772)
        if (event_n == g.InputEventsQueue.Size)
        {
            g.InputEventsQueue.resize(0);
        }
        else
        {
            // (C++: g.InputEventsQueue.erase(g.InputEventsQueue.Data, g.InputEventsQueue.Data + event_n))
            int remaining = g.InputEventsQueue.Size - event_n;
            Array.Copy(g.InputEventsQueue.Data, event_n, g.InputEventsQueue.Data, 0, remaining);
            g.InputEventsQueue.resize(remaining);
        }

        // Clear buttons state when focus is lost
        // - this is useful so e.g. releasing Alt after focus loss on Alt-Tab doesn't trigger the Alt menu toggle.
        // - we clear in EndFrame() and not now in order allow application/user code polling this flag
        //   (e.g. custom backend may want to clear additional data, custom widgets may want to react with a "canceling" event).
        if (g.IO.AppFocusLost)
        {
            g.IO.ClearInputKeys();
            g.IO.ClearInputMouse();
        }
    }

    public static ImGuiID GetKeyOwner(ImGuiKey key)
    {
        if (!IsNamedKeyOrMod(key))
            return ImGuiKeyOwner_NoOwner;

        ImGuiContext g = GImGui;
        ref ImGuiKeyOwnerData owner_data = ref GetKeyOwnerData(g, key);
        ImGuiID owner_id = owner_data.OwnerCurr;

        if (g.ActiveIdUsingAllKeyboardKeys && owner_id != g.ActiveId && owner_id != ImGuiKeyOwner_Any)
            if (key >= ImGuiUtil.ImGuiKey_Keyboard_BEGIN && key < ImGuiUtil.ImGuiKey_Keyboard_END)
                return ImGuiKeyOwner_NoOwner;

        return owner_id;
    }

    // TestKeyOwner(..., ID)   : (owner == None || owner == ID)
    // TestKeyOwner(..., None) : (owner == None)
    // TestKeyOwner(..., Any)  : no owner test
    // All paths are also testing for key not being locked, for the rare cases that key have been locked with using ImGuiInputFlags_LockXXX flags.
    public static bool TestKeyOwner(ImGuiKey key, ImGuiID owner_id)
    {
        if (!IsNamedKeyOrMod(key))
            return true;

        ImGuiContext g = GImGui;
        if (g.ActiveIdUsingAllKeyboardKeys && owner_id != g.ActiveId && owner_id != ImGuiKeyOwner_Any)
            if (key >= ImGuiUtil.ImGuiKey_Keyboard_BEGIN && key < ImGuiUtil.ImGuiKey_Keyboard_END)
                return false;

        ref ImGuiKeyOwnerData owner_data = ref GetKeyOwnerData(g, key);
        if (owner_id == ImGuiKeyOwner_Any)
            return owner_data.LockThisFrame == false;

        // Note: SetKeyOwner() sets OwnerCurr. It is not strictly required for most mouse routing overlap (because of ActiveId/HoveredId
        // are acting as filter before this has a chance to filter), but sane as soon as user tries to look into things.
        // Setting OwnerCurr in SetKeyOwner() is more consistent than testing OwnerNext here: would be inconsistent with getter and other functions.
        if (owner_data.OwnerCurr != owner_id)
        {
            if (owner_data.LockThisFrame)
                return false;
            if (owner_data.OwnerCurr != ImGuiKeyOwner_NoOwner)
                return false;
        }

        return true;
    }

    // _LockXXX flags are useful to lock keys away from code which is not input-owner aware.
    // When using _LockXXX flags, you can use ImGuiKeyOwner_Any to lock keys from everyone.
    // - SetKeyOwner(..., None)              : clears owner
    // - SetKeyOwner(..., Any, !Lock)        : illegal (assert)
    // - SetKeyOwner(..., Any or None, Lock) : set lock
    // Ownership is automatically released on the frame after a release, see code in UpdateKeyboardInputs().
    public static void SetKeyOwner(ImGuiKey key, ImGuiID owner_id, ImGuiInputFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(IsNamedKeyOrMod(key) && (owner_id != ImGuiKeyOwner_Any || (flags & (ImGuiInputFlags_LockThisFrame | ImGuiInputFlags_LockUntilRelease)) != 0)); // Can only use _Any with _LockXXX flags (to eat a key away without an ID to retrieve it)
        Debug.Assert((flags & ~ImGuiInputFlags_SupportedBySetKeyOwner) == 0); // Passing flags not supported by this function!
        //IMGUI_DEBUG_LOG("SetKeyOwner(%s, owner_id=0x%08X, flags=%08X)\n", GetKeyName(key), owner_id, flags);

        ref ImGuiKeyOwnerData owner_data = ref GetKeyOwnerData(g, key);
        owner_data.OwnerCurr = owner_data.OwnerNext = owner_id;

        // We cannot lock by default as it would likely break lots of legacy code.
        // In the case of using LockUntilRelease while key is not down we still lock during the frame (no key_data->Down test)
        owner_data.LockUntilRelease = (flags & ImGuiInputFlags_LockUntilRelease) != 0;
        owner_data.LockThisFrame = (flags & ImGuiInputFlags_LockThisFrame) != 0 || (owner_data.LockUntilRelease);
    }

    // Rarely used helper
    public static void SetKeyOwnersForKeyChord(ImGuiKeyChord key_chord, ImGuiID owner_id, ImGuiInputFlags flags = 0)
    {
        if ((key_chord & ImGuiMod_Ctrl) != 0) { SetKeyOwner((ImGuiKey)ImGuiMod_Ctrl, owner_id, flags); }
        if ((key_chord & ImGuiMod_Shift) != 0) { SetKeyOwner((ImGuiKey)ImGuiMod_Shift, owner_id, flags); }
        if ((key_chord & ImGuiMod_Alt) != 0) { SetKeyOwner((ImGuiKey)ImGuiMod_Alt, owner_id, flags); }
        if ((key_chord & ImGuiMod_Super) != 0) { SetKeyOwner((ImGuiKey)ImGuiMod_Super, owner_id, flags); }
        if ((key_chord & ~ImGuiMod_Mask_) != 0) { SetKeyOwner((ImGuiKey)(key_chord & ~ImGuiMod_Mask_), owner_id, flags); }
    }

    // This is more or less equivalent to a fancier version of:
    //   if (IsItemHovered() || IsItemActive())
    //       SetKeyOwner(key, GetItemID());
    // Extensive uses of that (e.g. many calls for a single item) may want to manually perform the tests once and then call SetKeyOwner() multiple times.
    // More advanced usage scenarios may want to call SetKeyOwner() manually based on different condition.
    // Worth noting is that only one item can be hovered and only one item can be active, therefore this usage pattern doesn't need to bother with routing and priority.
    public static bool SetItemKeyOwner(ImGuiKey key, ImGuiInputFlags flags)
    {
        ImGuiContext g = GImGui;
        ImGuiID id = g.LastItemData.ID;
        if (id == 0 || (g.HoveredId != id && g.ActiveId != id))
            return false;
        if ((flags & ImGuiInputFlags_CondMask_) == 0)
            flags |= ImGuiInputFlags_CondDefault_;
        if ((g.HoveredId == id && (flags & ImGuiInputFlags_CondHovered) != 0) || (g.ActiveId == id && (flags & ImGuiInputFlags_CondActive) != 0))
        {
            Debug.Assert((flags & ~ImGuiInputFlags_SupportedBySetItemKeyOwner) == 0); // Passing flags not supported by this function!
            if (!TestKeyOwner(key, id))
                return false;
            SetKeyOwner(key, id, flags & ~ImGuiInputFlags_CondMask_);
            return true;
        }
        return false;
    }

    public static bool SetItemKeyOwner(ImGuiKey key)
    {
        return SetItemKeyOwner(key, ImGuiInputFlags_None);
    }

    // This is the only public API until we expose owner_id versions of the API as replacements.
    public static bool IsKeyChordPressed(ImGuiKeyChord key_chord)
    {
        return IsKeyChordPressed(key_chord, ImGuiInputFlags_None, ImGuiKeyOwner_Any);
    }

    // This is equivalent to comparing KeyMods + doing a IsKeyPressed()
    public static bool IsKeyChordPressed(ImGuiKeyChord key_chord, ImGuiInputFlags flags, ImGuiID owner_id)
    {
        ImGuiContext g = GImGui;
        key_chord = FixupKeyChord(key_chord);
        ImGuiKey mods = (ImGuiKey)(key_chord & ImGuiMod_Mask_);
        if (g.IO.KeyMods != mods)
            return false;

        // Special storage location for mods
        ImGuiKey key = (ImGuiKey)(key_chord & ~ImGuiMod_Mask_);
        if (key == ImGuiKey_None)
            key = ImGuiUtil.ConvertSingleModFlagToKey(g, mods);
        if (!IsKeyPressed(key, (ImGuiInputFlags)(flags & ImGuiInputFlags_RepeatMask_), owner_id))
            return false;
        return true;
    }

    public static void SetNextItemShortcut(ImGuiKeyChord key_chord, ImGuiInputFlags flags = 0)
    {
        ImGuiContext g = GImGui;
        g.NextItemData.HasFlags |= ImGuiNextItemDataFlags_HasShortcut;
        g.NextItemData.Shortcut = key_chord;
        g.NextItemData.ShortcutFlags = flags;
    }

    // Called from within ItemAdd: at this point we can read from NextItemData and write to LastItemData
    public static void ItemHandleShortcut(ImGuiID id)
    {
        ImGuiContext g = GImGui;
        ImGuiInputFlags flags = g.NextItemData.ShortcutFlags;
        Debug.Assert((flags & ~ImGuiInputFlags_SupportedBySetNextItemShortcut) == 0); // Passing flags not supported by SetNextItemShortcut()!

        if ((g.LastItemData.ItemFlags & ImGuiItemFlags_Disabled) != 0)
            return;
        if ((flags & ImGuiInputFlags_Tooltip) != 0)
        {
            g.LastItemData.StatusFlags |= ImGuiItemStatusFlags_HasShortcut;
            g.LastItemData.Shortcut = g.NextItemData.Shortcut;
        }
        if (!Shortcut(g.NextItemData.Shortcut, (ImGuiInputFlags)(flags & ImGuiInputFlags_SupportedByShortcut), id) || g.NavActivateId != 0)
            return;

        // FIXME: Generalize Activation queue?
        g.NavActivateId = id; // Will effectively disable clipping.
        g.NavActivateFlags = ImGuiActivateFlags_PreferInput | ImGuiActivateFlags_FromShortcut;
        //if (g.ActiveId == 0 || g.ActiveId == id)
        g.NavActivateDownId = g.NavActivatePressedId = id;
        NavHighlightActivated(id);
    }

    public static bool Shortcut(ImGuiKeyChord key_chord, ImGuiInputFlags flags = 0)
    {
        return Shortcut(key_chord, flags, ImGuiKeyOwner_Any);
    }

    public static bool Shortcut(ImGuiKeyChord key_chord, ImGuiInputFlags flags, ImGuiID owner_id)
    {
        ImGuiContext g = GImGui;
        //IMGUI_DEBUG_LOG("Shortcut(%s, flags=%X, owner_id=0x%08X)\n", GetKeyChordName(key_chord), flags, owner_id);

        // When using (owner_id == 0/Any): SetShortcutRouting() will use CurrentFocusScopeId and filter with this, so IsKeyPressed() is fine with he 0/Any.
        if ((flags & ImGuiInputFlags_RouteTypeMask_) == 0)
            flags |= ImGuiInputFlags_RouteFocused;

        // Using 'owner_id == ImGuiKeyOwner_Any/0': auto-assign an owner based on current focus scope (each window has its focus scope by default)
        // Effectively makes Shortcut() always input-owner aware.
        if (owner_id == ImGuiKeyOwner_Any || owner_id == ImGuiKeyOwner_NoOwner)
            owner_id = GetRoutingIdFromOwnerId(owner_id);

        if ((g.CurrentItemFlags & ImGuiItemFlags_Disabled) != 0)
            return false;

        // Submit route
        if (!SetShortcutRouting(key_chord, flags, owner_id))
            return false;

        // Default repeat behavior for Shortcut()
        // So e.g. pressing Ctrl+W and releasing Ctrl while holding W will not trigger the W shortcut.
        if ((flags & ImGuiInputFlags_Repeat) != 0 && (flags & ImGuiInputFlags_RepeatUntilMask_) == 0)
            flags |= ImGuiInputFlags_RepeatUntilKeyModsChange;

        if (!IsKeyChordPressed(key_chord, flags, owner_id))
            return false;

        // Claim mods during the press
        SetKeyOwnersForKeyChord(key_chord & ImGuiMod_Mask_, owner_id);

        Debug.Assert((flags & ~ImGuiInputFlags_SupportedByShortcut) == 0); // Passing flags not supported by this function!
        return true;
    }

    // -------------------------------------------------------------------------
    // [SECTION] ERROR CHECKING, STATE RECOVERY
    // -------------------------------------------------------------------------

    // Verify ABI compatibility between caller code and compiled version of Dear ImGui. This helps detects some build issues.
    // Called by IMGUI_CHECKVERSION().
    // (Struct size checks are meaningless in C#: only the version string is verified.)
    public static bool DebugCheckVersionAndDataLayout(string version, ulong sz_io, ulong sz_style, ulong sz_vec2, ulong sz_vec4, ulong sz_vert, ulong sz_idx)
    {
        bool error = false;
        if (version != ImGuiVersion)
        {
            error = true;
            Debug.Assert(version == ImGuiVersion, "Mismatched version string!");
        }
        return !error;
    }

    // Until 1.89 (August 2022, IMGUI_VERSION_NUM < 18814) it was legal to use SetCursorPos()/SetCursorScreenPos()
    // to extend contents size of our parent container (e.g. window contents size, which is used for auto-resizing
    // windows, table column contents size used for auto-resizing columns, group size).
    // This was causing issues and ambiguities and we needed to retire that.
    // 2022/08/05 (1.89): extending contents size boundaries REQUIRES AN ITEM TO BE SUBMITTED. However we gated the new logic behind a '#ifndef IMGUI_DISABLE_OBSOLETE_FUNCTIONS' block.
    // 2025/06/25 (1.92): removed the legacy path and turned into an assert. It was a mistake that there was a #ifndef before: our obsolescence schedule gets pushed back a bit more :(
    //
    // The assert below detects when the _last_ call in a window was a SetCursorPos() not followed by an Item,
    // and with a position that would grow the parent contents size.
    public static void ErrorCheckUsingSetCursorPosToExtendParentBoundaries()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert(window.DC.IsSetPos);
        window.DC.IsSetPos = false;
        if (window.DC.CursorPos.x <= window.DC.CursorMaxPos.x && window.DC.CursorPos.y <= window.DC.CursorMaxPos.y)
            return;
        if (window.SkipItems)
            return;
        ImUtil.ImAssertUserError(false, "Code uses SetCursorPos()/SetCursorScreenPos() to extend window/parent boundaries.\nPlease submit an item e.g. Dummy() afterwards in order to grow window/parent boundaries.");

        // For reference, the old behavior was essentially:
        //window.DC.CursorMaxPos = ImMath.ImMax(window.DC.CursorMaxPos, window.DC.CursorPos);
    }

    public static void ErrorCheckNewFrameSanityChecks()
    {
        ImGuiContext g = GImGui;

        // Check user IM_ASSERT macro
        // (IF YOU GET A WARNING OR COMPILE ERROR HERE: it means your assert macro is incorrectly defined!)
        Debug.Assert(true);

        // Emscripten backends are often imprecise in their submission of DeltaTime. (#6114, #3644)
        // Ideally the Emscripten app/backend should aim to fix or smooth this value and avoid feeding zero, but we tolerate it.

        // Check user data
        // (We pass an error message in the assert expression to make it visible to programmers who are not using a debugger, as most assert handlers display their argument)
        Debug.Assert(g.Initialized);
        Debug.Assert(g.IO.DeltaTime > 0.0f || g.FrameCount == 0, "Need a positive DeltaTime!");
        Debug.Assert(g.FrameCount == 0 || g.FrameCountEnded == g.FrameCount, "Forgot to call Render() or EndFrame() at the end of the previous frame?");
        Debug.Assert(g.IO.DisplaySize.x >= 0.0f && g.IO.DisplaySize.y >= 0.0f, "Invalid DisplaySize value!");
        Debug.Assert(g.Style.CurveTessellationMaxError > 0.0f, "Invalid style setting!");
        Debug.Assert(g.Style.CircleTessellationMaxError > 0.0f, "Invalid style setting!");
        Debug.Assert(g.Style.Alpha >= 0.0f && g.Style.Alpha <= 1.0f, "Invalid style setting!"); // Allows us to avoid a few clamps in color computations
        Debug.Assert(g.Style.WindowMinSize.x >= 1.0f && g.Style.WindowMinSize.y >= 1.0f, "Invalid style setting!");
        Debug.Assert(g.Style.WindowBorderHoverPadding > 0.0f, "Invalid style setting!"); // Required otherwise cannot resize from borders.
        Debug.Assert(g.Style.WindowMenuButtonPosition == ImGuiDir_None || g.Style.WindowMenuButtonPosition == ImGuiDir_Left || g.Style.WindowMenuButtonPosition == ImGuiDir_Right);
        Debug.Assert(g.Style.ColorButtonPosition == ImGuiDir_Left || g.Style.ColorButtonPosition == ImGuiDir_Right);
        Debug.Assert(ImMath.ImIsPowerOfTwo(g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_DisplayMask_));  // Check only 1 option is selected
        Debug.Assert(ImMath.ImIsPowerOfTwo(g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_DataTypeMask_)); // "
        Debug.Assert(ImMath.ImIsPowerOfTwo(g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_PickerMask_));   // "
        Debug.Assert(ImMath.ImIsPowerOfTwo(g.IO.ConfigColorEditFlags & ImGuiColorEditFlags_InputMask_));    // "

        Debug.Assert(g.Style.TreeLinesFlags == ImGuiTreeNodeFlags_DrawLinesNone || g.Style.TreeLinesFlags == ImGuiTreeNodeFlags_DrawLinesFull || g.Style.TreeLinesFlags == ImGuiTreeNodeFlags_DrawLinesToNodes);
        Debug.Assert(g.IO.MouseSingleClickDelay > g.IO.MouseDoubleClickTime);

        // Error handling: we do not accept 100% silent recovery! Please contact me if you feel this is getting in your way.
        if (g.IO.ConfigErrorRecovery)
            Debug.Assert(g.IO.ConfigErrorRecoveryEnableAssert || g.IO.ConfigErrorRecoveryEnableDebugLog || g.IO.ConfigErrorRecoveryEnableTooltip || g.ErrorCallback != null);

        if (g.IO.FontGlobalScale > 1.0f)
            Debug.Assert(g.Style.FontScaleMain == 1.0f, "Since 1.92: use style.FontScaleMain instead of g.IO.FontGlobalScale!");

        // Remap legacy names
        if ((g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableSetMousePos) != 0)
        {
            g.IO.ConfigNavMoveSetMousePos = true;
            g.IO.ConfigFlags &= ~ImGuiConfigFlags_NavEnableSetMousePos;
        }
        if ((g.IO.ConfigFlags & ImGuiConfigFlags_NavNoCaptureKeyboard) != 0)
        {
            g.IO.ConfigNavCaptureKeyboard = false;
            g.IO.ConfigFlags &= ~ImGuiConfigFlags_NavNoCaptureKeyboard;
        }

        // (Remap legacy clipboard handlers: not applicable in this port, io.GetClipboardTextFn is managed-only via PlatformIO.)

        // Remap legacy CurveTessellationTol into CurveTessellationMaxError (OBSOLETED in 1.93.0, August 2026)
        if (g.Style.CurveTessellationTol != 0.0f)
            g.Style.CurveTessellationMaxError = ImMath.ImSqrt(g.Style.CurveTessellationTol);
        g.Style.CurveTessellationTol = 0.0f;
    }

    public static void ErrorCheckEndFrameSanityChecks()
    {
        // Verify that io.KeyXXX fields haven't been tampered with. Key mods should not be modified between NewFrame() and EndFrame()
        // One possible reason leading to this assert is that your backends update inputs _AFTER_ NewFrame().
        // It is known that when some modal native windows called mid-frame takes focus away, some backends such as GLFW will
        // send key release events mid-frame. This would normally trigger this assertion and lead to sheared inputs.
        // We silently accommodate for this case by ignoring the case where all io.KeyXXX modifiers were released (aka key_mod_flags == 0),
        // while still correctly asserting on mid-frame key press events.
        ImGuiContext g = GImGui;
        ImGuiKeyChord key_mods = GetMergedModsFromKeys();
        Debug.Assert(key_mods == 0 || g.IO.KeyMods == key_mods, "Mismatching io.KeyCtrl/io.KeyShift/io.KeyAlt/io.KeySuper vs io.KeyMods");

        Debug.Assert(g.CurrentWindowStack.Size == 1);
        Debug.Assert(g.CurrentWindowStack[0].Window.IsFallbackWindow);
    }

    // Save current stack sizes. Called e.g. by NewFrame() and by Begin() but may be called for manual recovery.
    public static void ErrorRecoveryStoreState(ref ImGuiErrorRecoveryState state_out)
    {
        ImGuiContext g = GImGui;
        state_out.SizeOfWindowStack = (short)g.CurrentWindowStack.Size;
        state_out.SizeOfIDStack = (short)g.CurrentWindow.IDStack.Size;
        state_out.SizeOfTreeStack = (short)g.CurrentWindow.DC.TreeDepth; // NOT g.TreeNodeStack.Size which is a partial stack!
        state_out.SizeOfColorStack = (short)g.ColorStack.Size;
        state_out.SizeOfStyleVarStack = (short)g.StyleVarStack.Size;
        state_out.SizeOfFontStack = (short)g.FontStack.Size;
        state_out.SizeOfFocusScopeStack = (short)g.FocusScopeStack.Size;
        state_out.SizeOfGroupStack = (short)g.GroupStack.Size;
        state_out.SizeOfItemFlagsStack = (short)g.ItemFlagsStack.Size;
        state_out.SizeOfBeginPopupStack = (short)g.BeginPopupStack.Size;
        state_out.SizeOfDisabledStack = (short)g.DisabledStackSize;
    }

    // Chosen name "Try to recover" over e.g. "Restore" to suggest this is not a 100% guaranteed recovery.
    // Called by e.g. EndFrame() but may be called for manual recovery.
    // Attempt to recover full window stack.
    public static void ErrorRecoveryTryToRecoverState(ref ImGuiErrorRecoveryState state_in)
    {
        // PVS-Studio V1044 is "Loop break conditions do not depend on the number of iterations"
        ImGuiContext g = GImGui;
        while (g.CurrentWindowStack.Size > state_in.SizeOfWindowStack) //-V1044
        {
            // Recap:
            // - Begin()/BeginChild() return false to indicate the window is collapsed or fully clipped.
            // - Always call a matching End() for each Begin() call, regardless of its return value!
            // - Begin/End and BeginChild/EndChild logic is KNOWN TO BE INCONSISTENT WITH ALL OTHER BEGIN/END FUNCTIONS.
            // - We will fix that in a future major update.
            ImGuiWindow window = g.CurrentWindow;
            if ((window.Flags & ImGuiWindowFlags_ChildWindow) != 0)
            {
                if (g.CurrentTable != null && g.CurrentTable.InnerWindow == g.CurrentWindow)
                {
                    ImUtil.ImAssertUserError(false, "Missing EndTable()");
                    EndTable();
                }
                else
                {
                    ImUtil.ImAssertUserError(false, "Missing EndChild()");
                    EndChild();
                }
            }
            else
            {
                ImUtil.ImAssertUserError(false, "Missing End()");
                End();
            }
        }
        if (g.CurrentWindowStack.Size == state_in.SizeOfWindowStack)
            ErrorRecoveryTryToRecoverWindowState(state_in);
    }

    // Called by e.g. End() but may be called for manual recovery.
    // Read '// Error Handling [BETA]' block in imgui_internal.h for details.
    // Attempt to recover from incorrect usage of BeginXXX/EndXXX/PushXXX/PopXXX calls.
    public static void ErrorRecoveryTryToRecoverWindowState(ImGuiErrorRecoveryState state_in)
    {
        ImGuiContext g = GImGui;

        while (g.CurrentTable != null && g.CurrentTable.InnerWindow == g.CurrentWindow) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing EndTable()");
            EndTable();
        }

        ImGuiWindow window = g.CurrentWindow;

        // FIXME: Can't recover from inside BeginTabItem/EndTabItem yet.
        while (g.CurrentTabBar != null && g.CurrentTabBar.Window == window) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing EndTabBar()");
            EndTabBar();
        }
        while (g.CurrentMultiSelect != null && g.CurrentMultiSelect.Storage != null && g.CurrentMultiSelect.Storage.Window == window) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing EndMultiSelect()");
            EndMultiSelect();
        }
        if (window.DC.MenuBarAppending) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing EndMenuBar()");
            EndMenuBar();
        }
        while (window.DC.TreeDepth > state_in.SizeOfTreeStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing TreePop()");
            TreePop();
        }
        while (g.GroupStack.Size > state_in.SizeOfGroupStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing EndGroup()");
            EndGroup();
        }
        Debug.Assert(g.GroupStack.Size == state_in.SizeOfGroupStack);
        while (window.IDStack.Size > state_in.SizeOfIDStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing PopID()");
            PopID();
        }
        while (g.DisabledStackSize > state_in.SizeOfDisabledStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing EndDisabled()");
            if ((g.CurrentItemFlags & ImGuiItemFlags_Disabled) != 0)
                EndDisabled();
            else
            {
                EndDisabledOverrideReenable();
                ImGuiWindowStackData back = g.CurrentWindowStack.back();
                back.DisabledOverrideReenable = false;
                g.CurrentWindowStack.Data[g.CurrentWindowStack.Size - 1] = back;
            }
        }
        Debug.Assert(g.DisabledStackSize == state_in.SizeOfDisabledStack);
        while (g.ColorStack.Size > state_in.SizeOfColorStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing PopStyleColor()");
            PopStyleColor();
        }
        while (g.ItemFlagsStack.Size > state_in.SizeOfItemFlagsStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing PopItemFlag()");
            PopItemFlag();
        }
        while (g.StyleVarStack.Size > state_in.SizeOfStyleVarStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing PopStyleVar()");
            PopStyleVar();
        }
        while (g.FontStack.Size > state_in.SizeOfFontStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing PopFont()");
            PopFont();
        }
        while (g.FocusScopeStack.Size > state_in.SizeOfFocusScopeStack) //-V1044
        {
            ImUtil.ImAssertUserError(false, "Missing PopFocusScope()");
            PopFocusScope();
        }
        //Debug.Assert(g.FocusScopeStack.Size == state_in.SizeOfFocusScopeStack);
    }

    public static bool ErrorLog(string msg)
    {
        ImGuiContext g = GImGui;

        // Output to debug log
        ImGuiWindow window = g.CurrentWindow;

        if (g.IO.ConfigErrorRecoveryEnableDebugLog)
        {
            // (IMGUI_DEBUG_LOG_ERROR omitted in this port)
        }
        g.ErrorFirst = false;

        // Output to tooltip
        if (g.IO.ConfigErrorRecoveryEnableTooltip)
        {
            bool tooltip_open = g.WithinFrameScope && BeginErrorTooltip();
            if (tooltip_open)
            {
                if (g.ErrorCountCurrentFrame < 20)
                {
                    Text("In window '%s': %s", window != null ? window.Name : "NULL", msg);
                    if (window != null && (!window.IsFallbackWindow || window.WasActive))
                        GetForegroundDrawList(window).AddRect(window.Pos, window.Pos + window.Size, ImGuiCore.IM_COL32(255, 0, 0, 255));
                }
                if (g.ErrorCountCurrentFrame == 20)
                    Text("(and more errors)");
                // EndFrame() will amend debug buttons to this window, after all errors have been submitted.
                EndErrorTooltip();
            }
            g.ErrorCountCurrentFrame++;
        }

        // Output to callback
        if (g.ErrorCallback != null)
            g.ErrorCallback(g, g.ErrorCallbackUserData, msg);

        // Return whether we should assert
        return g.IO.ConfigErrorRecoveryEnableAssert;
    }

    // Display an error tooltip when same ID as HoveredId was submitted multiple times.
    // See code in ItemHoverable() for an explanation of why we associate this error to HoveredId + code drawing of rectangles over individual items instances.
    public static void ErrorCheckEndFrameFinalizeErrorTooltip()
    {
        ImGuiContext g = GImGui;
        if (g.DebugDrawIdConflictsId != 0 && g.IO.KeyCtrl == false)
            g.DebugDrawIdConflictsCount = g.HoveredIdPreviousFrameItemCount;
        if (g.DebugDrawIdConflictsId != 0 && g.DebugItemPickerActive == false && BeginErrorTooltip())
        {
            Text("Programmer error: %d visible items with conflicting ID!", g.DebugDrawIdConflictsCount);
            BulletText("Code should use PushID()/PopID() in loops, or append \"##xx\" to same-label identifiers!");
            BulletText("Empty label e.g. Button(\"\") == same ID as parent widget/node. Use Button(\"##xx\") instead!");
            //BulletText("Code intending to use duplicate ID may use e.g. PushItemFlag(ImGuiItemFlags_AllowDuplicateId, true); ... PopItemFlag()"); // Not making this too visible for fear of it being abused.
            BulletText("Set io.ConfigDebugHighlightIdConflicts=false to disable this warning in non-programmers builds.");
            Separator();
            if (g.IO.ConfigDebugHighlightIdConflictsShowItemPicker)
            {
                Text("(Hold Ctrl to: use ");
                SameLine(0.0f, 0.0f);
                if (SmallButton("Item Picker"))
                    DebugStartItemPicker();
                SameLine(0.0f, 0.0f);
                Text(" to break in item call-stack, or ");
            }
            else
            {
                Text("(Hold Ctrl to: ");
            }
            SameLine(0.0f, 0.0f);
            TextLinkOpenURL("read FAQ \"About ID Stack System\"", "https://github.com/ocornut/imgui/blob/master/docs/FAQ.md#qa-usage");
            SameLine(0.0f, 0.0f);
            Text(")");
            EndErrorTooltip();
        }

        if (g.ErrorCountCurrentFrame > 0 && BeginErrorTooltip()) // Amend at end of frame
        {
            Separator();
            Text("(Hold Ctrl to: ");
            SameLine(0.0f, 0.0f);
            if (SmallButton("Enable Asserts"))
                g.IO.ConfigErrorRecoveryEnableAssert = true;
            //SameLine();
            //if (SmallButton("Hide Error Tooltips"))
            //    g.IO.ConfigErrorRecoveryEnableTooltip = false; // Too dangerous
            SameLine(0, 0);
            Text(")");
            EndErrorTooltip();
        }
    }

    // Pseudo-tooltip. Follow mouse until Ctrl is held. When Ctrl is held we lock position, allowing to click it.
    public static bool BeginErrorTooltip()
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = FindWindowByName("##Tooltip_Error");
        bool use_locked_pos = (g.IO.KeyCtrl && window != null && window.WasActive);
        PushStyleColor(ImGuiCol_PopupBg, ImMath.ImLerp(g.Style.Colors[ImGuiCol_PopupBg], new ImVec4(1.0f, 0.0f, 0.0f, 1.0f), 0.15f));
        if (use_locked_pos)
            SetNextWindowPos(g.ErrorTooltipLockedPos);
        bool is_visible = Begin("##Tooltip_Error", ImGuiWindowFlags_Tooltip | ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_AlwaysAutoResize);
        PopStyleColor();
        if (is_visible && g.CurrentWindow.BeginCount == 1)
        {
            SeparatorText("MESSAGE FROM DEAR IMGUI");
            BringWindowToDisplayFront(g.CurrentWindow);
            BringWindowToFocusFront(g.CurrentWindow);
            g.ErrorTooltipLockedPos = GetWindowPos();
        }
        else if (!is_visible)
        {
            End();
        }
        return is_visible;
    }

    public static void EndErrorTooltip()
    {
        End();
    }

    // -------------------------------------------------------------------------
    // [SECTION] FONTS, TEXTURES (subset)
    // -------------------------------------------------------------------------

    public static void UpdateTexturesNewFrame()
    {
        // Cannot update every atlases based on atlas's FrameCount < g.FrameCount, because an atlas may be shared by multiple contexts with different frame count.
        ImGuiContext g = GImGui;
        bool has_textures = (g.IO.BackendFlags & ImGuiBackendFlags_RendererHasTextures) != 0;
        for (int atlas_n = 0; atlas_n < g.FontAtlases.Size; atlas_n++)
        {
            ImFontAtlas atlas = g.FontAtlases[atlas_n];
            if (atlas.OwnerContext == g)
            {
                ImFontAtlas.ImFontAtlasUpdateNewFrame(atlas, g.FrameCount, has_textures);
            }
            else
            {
                // (1) If you manage font atlases yourself, e.g. create a ImFontAtlas yourself you need to call ImFontAtlasUpdateNewFrame() on it.
                // Otherwise, calling ImGui.CreateContext() without parameter will create an atlas owned by the context.
                // (2) If you have multiple font atlases, make sure the 'atlas->RendererHasTextures' as specified in the ImFontAtlasUpdateNewFrame() call matches for that.
                // (3) If you have multiple imgui contexts, they also need to have a matching value for ImGuiBackendFlags_RendererHasTextures.
                Debug.Assert(atlas.Builder != null && atlas.Builder.FrameCount != -1);
                Debug.Assert(atlas.RendererHasTextures == has_textures);
            }
        }
        for (int tex_n = 0; tex_n < g.UserTextures.Size; tex_n++)
            ImUtil.ImTextureDataUpdateNewFrame(g.UserTextures[tex_n]);
    }

    // Build a single texture list
    public static void UpdateTexturesEndFrame()
    {
        ImGuiContext g = GImGui;
        g.PlatformIO.Textures.resize(0);
        for (int atlas_n = 0; atlas_n < g.FontAtlases.Size; atlas_n++)
        {
            ImFontAtlas atlas = g.FontAtlases[atlas_n];
            for (int tex_n = 0; tex_n < atlas.TexList.Size; tex_n++)
            {
                ImTextureData tex = atlas.TexList[tex_n];
                // We provide this information so backends can decide whether to destroy textures.
                // This means in practice that if N imgui contexts are created with a shared atlas, we assume all of them have a backend initialized.
                tex.RefCount = (ushort)atlas.RefCount;
                g.PlatformIO.Textures.push_back(tex);
            }
        }
        for (int tex_n = 0; tex_n < g.UserTextures.Size; tex_n++)
            g.PlatformIO.Textures.push_back(g.UserTextures[tex_n]);
    }

    public static void UpdateFontsNewFrame()
    {
        ImGuiContext g = GImGui;
        if ((g.IO.BackendFlags & ImGuiBackendFlags_RendererHasTextures) == 0)
            for (int atlas_n = 0; atlas_n < g.FontAtlases.Size; atlas_n++)
                g.FontAtlases[atlas_n].Locked = true;

        if (g.Style._NextFrameFontSizeBase != 0.0f)
        {
            g.Style.FontSizeBase = g.Style._NextFrameFontSizeBase;
            g.Style._NextFrameFontSizeBase = 0.0f;
        }

        // Apply default font size the first time
        ImFont font = GetDefaultFont();
        if (g.Style.FontSizeBase <= 0.0f)
            g.Style.FontSizeBase = (font.LegacySize > 0.0f ? font.LegacySize : FONT_DEFAULT_SIZE_BASE);

        // Set initial font
        g.Font = font;
        g.FontSizeBase = g.Style.FontSizeBase;
        g.FontSize = 0.0f;
        ImFontStackData font_stack_data = new ImFontStackData { Font = font, FontSizeBeforeScaling = g.Style.FontSizeBase, FontSizeAfterScaling = g.Style.FontSizeBase };  // <--- Will restore FontSize
        SetCurrentFont(font_stack_data.Font, font_stack_data.FontSizeBeforeScaling, 0.0f); // <--- but use 0.0f to enable scale
        g.FontStack.push_back(font_stack_data);
        Debug.Assert(g.Font.IsLoaded());
    }

    public static void UpdateFontsEndFrame()
    {
        PopFont();
    }

    public static ImFont GetDefaultFont()
    {
        ImGuiContext g = GImGui;
        ImFontAtlas atlas = g.IO.Fonts;
        if (atlas.Builder == null || atlas.Fonts.Size == 0)
            ImFontAtlas.ImFontAtlasBuildMain(atlas);
        return g.IO.FontDefault != null ? g.IO.FontDefault : atlas.Fonts[0];
    }

    // EXPERIMENTAL. Use ImTextureDataQueueUpload() to queue updates. Textures logic will be automatically be updated in NewFrame().
    public static void RegisterUserTexture(ImTextureData tex)
    {
        ImGuiContext g = GImGui;
        tex.RefCount++;
        g.UserTextures.push_back(tex);
    }

    public static void UnregisterUserTexture(ImTextureData tex)
    {
        ImGuiContext g = GImGui;
        Debug.Assert(tex.RefCount > 0);
        tex.RefCount--;
        int idx = g.UserTextures.index_of_ref(tex);
        if (idx != -1)
            g.UserTextures.erase_unsorted(idx); // (C++: find_erase())
    }

    public static void PushFont(ImFont font, float font_size_base_unscaled)
    {
        ImGuiContext g = GImGui;
        if (font == null) // Before 1.92 (June 2025), PushFont(NULL) == PushFont(GetDefaultFont())
            font = g.Font;
        Debug.Assert(font != null);
        Debug.Assert(font_size_base_unscaled >= 0.0f);

        g.FontStack.push_back(new ImFontStackData { Font = g.Font, FontSizeBeforeScaling = g.FontSizeBase, FontSizeAfterScaling = g.FontSize });
        if (font_size_base_unscaled == 0.0f)
            font_size_base_unscaled = g.FontSizeBase; // Keep current font size
        SetCurrentFont(font, font_size_base_unscaled, 0.0f);
    }

    public static void PopFont()
    {
        ImGuiContext g = GImGui;
        ImUtil.ImAssertUserError(g.FontStack.Size > 0, "Calling PopFont() too many times!");
        ImFontStackData font_stack_data = g.FontStack.back();
        SetCurrentFont(font_stack_data.Font, font_stack_data.FontSizeBeforeScaling, font_stack_data.FontSizeAfterScaling);
        g.FontStack.pop_back();
    }

    // -------------------------------------------------------------------------
    // [SECTION] VIEWPORTS, PLATFORM WINDOWS (subset)
    // -------------------------------------------------------------------------

    public static ImGuiViewport GetMainViewport()
    {
        ImGuiContext g = GImGui;
        return g.Viewports[0];
    }

    // -------------------------------------------------------------------------
    // [SECTION] LOGGING/CAPTURING
    // -------------------------------------------------------------------------

    // Pass text data straight to log (without being displayed)
    static void LogTextV(ImGuiContext g, string fmt, object[] args)
    {
        if (g.LogFile != null)
        {
            g.LogBuffer.clear();
            g.LogBuffer.appendf(fmt, args);
            byte[] bytes = Encoding.UTF8.GetBytes(g.LogBuffer.c_str());
            g.LogFile.Write(bytes, 0, bytes.Length);
        }
        else
        {
            g.LogBuffer.appendf(fmt, args);
        }
    }

    public static void LogText(string fmt, params object[] args)
    {
        ImGuiContext g = GImGui;
        if (!g.LogEnabled)
            return;

        LogTextV(g, fmt, args);
    }

    // Internal version that takes a position to decide on newline placement and pad items according to their depth.
    // We split text into individual lines to add current tree level padding
    // FIXME: This code is a little complicated perhaps, considering simplifying the whole system.
    public static void LogRenderedText(ImVec2? ref_pos, string text, int text_end = -1)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;

        string prefix = g.LogNextPrefix;
        string suffix = g.LogNextSuffix;
        g.LogNextPrefix = g.LogNextSuffix = null;

        if (text_end < 0)
            text_end = FindRenderedTextEnd(text, text_end);

        bool log_new_line = ref_pos.HasValue && (ref_pos.Value.y > g.LogLinePosY + ImMath.ImMax(g.Style.FramePadding.y, g.Style.ItemSpacing.y) + 1);
        if (ref_pos.HasValue)
            g.LogLinePosY = ref_pos.Value.y;
        if (log_new_line)
        {
            LogText(IM_NEWLINE);
            g.LogLineFirstItem = true;
        }

        if (prefix != null)
            LogRenderedText(ref_pos, prefix, prefix.Length); // Calculate end ourself to ensure "##" are included here.

        // Re-adjust padding if we have popped out of our starting depth
        if (g.LogDepthRef > window.DC.TreeDepth)
            g.LogDepthRef = window.DC.TreeDepth;
        int tree_depth = (window.DC.TreeDepth - g.LogDepthRef);

        int text_remaining = 0;
        for (; ; )
        {
            // Split the string. Each new line (after a '\n') is followed by indentation corresponding to the current depth of our log entry.
            // We don't add a trailing \n yet to allow a subsequent item on the same line to be captured.
            int line_start = text_remaining;
            int line_end = line_start;
            while (line_end < text_end && text[line_end] != '\n')
                line_end++;
            bool is_last_line = (line_end == text_end);
            if (line_start != line_end || !is_last_line)
            {
                int line_length = line_end - line_start;
                int indentation = g.LogLineFirstItem ? tree_depth * 4 : 1;
                LogText("%s%s", new string(' ', indentation), text.Substring(line_start, line_length));
                g.LogLineFirstItem = false;
                if (line_end < text.Length && text[line_end] == '\n')
                {
                    LogText(IM_NEWLINE);
                    g.LogLineFirstItem = true;
                }
            }
            if (is_last_line)
                break;
            text_remaining = line_end + 1;
        }

        if (suffix != null)
            LogRenderedText(ref_pos, suffix, suffix.Length);
    }

    // Start logging/capturing text output
    public static void LogBegin(ImGuiLogFlags flags, int auto_open_depth)
    {
        ImGuiContext g = GImGui;
        ImGuiWindow window = g.CurrentWindow;
        Debug.Assert(g.LogEnabled == false);
        Debug.Assert(g.LogFile == null && g.LogBuffer.empty());
        Debug.Assert(ImMath.ImIsPowerOfTwo(flags & ImGuiLogFlags_OutputMask_)); // Check that only 1 type flag is used

        g.LogEnabled = g.ItemUnclipByLog = true;
        g.LogFlags = flags;
        g.LogWindow = window;
        g.LogNextPrefix = g.LogNextSuffix = null;
        g.LogDepthRef = window.DC.TreeDepth;
        g.LogDepthToExpand = ((auto_open_depth >= 0) ? auto_open_depth : g.LogDepthToExpandDefault);
        g.LogLinePosY = float.MaxValue;
        g.LogLineFirstItem = true;
    }

    // Important: doesn't copy underlying data, use carefully (prefix/suffix must be in scope at the time of the next LogRenderedText)
    public static void LogSetNextTextDecoration(string prefix, string suffix)
    {
        ImGuiContext g = GImGui;
        g.LogNextPrefix = prefix;
        g.LogNextSuffix = suffix;
    }

    public static void LogToTTY(int auto_open_depth = -1)
    {
        ImGuiContext g = GImGui;
        if (g.LogEnabled)
            return;
        LogBegin(ImGuiLogFlags_OutputTTY, auto_open_depth);
        g.LogFile = Console.OpenStandardOutput();
    }

    // Start logging/capturing text output to given file
    public static void LogToFile(int auto_open_depth = -1, string filename = null)
    {
        ImGuiContext g = GImGui;
        if (g.LogEnabled)
            return;

        // FIXME: We could probably open the file in text mode "at", however note that clipboard/buffer logging will still
        // be subject to outputting OS-incompatible carriage return if within strings the user doesn't use IM_NEWLINE.
        // By opening the file in binary mode "ab" we have consistent output everywhere.
        if (filename == null)
            filename = g.IO.LogFilename;
        if (string.IsNullOrEmpty(filename))
            return;
        System.IO.FileStream f;
        try
        {
            f = new System.IO.FileStream(filename, System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.Read);
        }
        catch (Exception)
        {
            Debug.Assert(false, "LogToFile(): Couldn't open file for writing");
            return;
        }

        LogBegin(ImGuiLogFlags_OutputFile, auto_open_depth);
        g.LogFile = f;
    }

    // Start logging/capturing text output to clipboard
    public static void LogToClipboard(int auto_open_depth = -1)
    {
        ImGuiContext g = GImGui;
        if (g.LogEnabled)
            return;
        LogBegin(ImGuiLogFlags_OutputClipboard, auto_open_depth);
    }

    public static void LogToBuffer(int auto_open_depth = -1)
    {
        ImGuiContext g = GImGui;
        if (g.LogEnabled)
            return;
        LogBegin(ImGuiLogFlags_OutputBuffer, auto_open_depth);
    }

    public static void LogFinish()
    {
        ImGuiContext g = GImGui;
        if (!g.LogEnabled)
            return;

        LogText(IM_NEWLINE);
        switch (g.LogFlags & ImGuiLogFlags_OutputMask_)
        {
            case ImGuiLogFlags_OutputTTY:
                g.LogFile.Flush();
                break;
            case ImGuiLogFlags_OutputFile:
                g.LogFile.Close();
                break;
            case ImGuiLogFlags_OutputBuffer:
                break;
            case ImGuiLogFlags_OutputClipboard:
                if (!g.LogBuffer.empty())
                    SetClipboardText(g.LogBuffer.c_str());
                break;
            default:
                Debug.Assert(false);
                break;
        }

        g.LogEnabled = g.ItemUnclipByLog = false;
        g.LogFlags = ImGuiLogFlags_None;
        g.LogFile = null;
        g.LogBuffer.clear();
    }

    // Helper to display logging buttons
    // FIXME-OBSOLETE: We should probably obsolete this and let the user have their own helper (this is one of the oldest function alive!)
    public static void LogButtons()
    {
        ImGuiContext g = GImGui;

        PushID("LogButtons");
        bool log_to_tty = Button("Log To TTY"); SameLine();
        bool log_to_file = Button("Log To File"); SameLine();
        bool log_to_clipboard = Button("Log To Clipboard"); SameLine();
        PushItemFlag(ImGuiItemFlags_NoTabStop, true);
        SetNextItemWidth(CalcTextSize("999").x);
        SliderInt("Default Depth", ref g.LogDepthToExpandDefault, 0, 9, null);
        PopItemFlag();
        PopID();

        // Start logging at the end of the function so that the buttons don't appear in the log
        if (log_to_tty)
            LogToTTY();
        if (log_to_file)
            LogToFile();
        if (log_to_clipboard)
            LogToClipboard();
    }
}
