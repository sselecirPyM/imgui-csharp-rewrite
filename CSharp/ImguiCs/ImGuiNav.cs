// Port of Dear ImGui imgui.cpp lines 13304-15026: [SECTION] KEYBOARD/GAMEPAD NAVIGATION.
using System;

namespace ImGui;

// Internal constants local to imgui.cpp (imgui.cpp top-of-file statics + imgui_internal.h defines)
// used by the navigation code below.
public static partial class ImGuiConst
{
    public const float NAV_WINDOWING_HIGHLIGHT_DELAY = 0.20f;           // Time before the highlight and screen dimming starts fading in
    public const float NAV_WINDOWING_LIST_APPEAR_DELAY = 0.15f;         // Time before the window list starts to appear
    public const float NAV_ACTIVATE_HIGHLIGHT_TIMER = 0.10f;            // Time to highlight an item activated by a shortcut.
    public const float NAV_ACTIVATE_INPUT_WITH_GAMEPAD_DELAY = 0.60f;   // Time to hold activation button (e.g. FaceDown) to turn the activation into a text input.

    public const ImGuiID ImGuiKeyOwner_Any = 0u;                        // Accept key that have an owner, UNLESS a call to SetKeyOwner() explicitly used ImGuiInputFlags_LockThisFrame or ImGuiInputFlags_LockUntilRelease.
    public const ImGuiID ImGuiKeyOwner_NoOwner = 0xFFFFFFFFu;           // Require key to have no owner. (C++: (ImGuiID)-1)

    public const ImGuiSelectionUserData ImGuiSelectionUserData_Invalid = -1;
}

// [SECTION] KEYBOARD/GAMEPAD NAVIGATION (imgui.cpp)
public static partial class ImGui
{
    // imgui_internal.h ImGuiKey_NavGamepad*/ImGuiKey_NavKeyboard* macros (context-dependent)
    static ImGuiKey ImGuiKey_NavGamepadTweakSlow => ImGuiKey_GamepadL1;
    static ImGuiKey ImGuiKey_NavGamepadTweakFast => ImGuiKey_GamepadR1;
    static ImGuiKey ImGuiKey_NavGamepadActivate => GImGui.IO.ConfigNavSwapGamepadButtons ? ImGuiKey_GamepadFaceRight : ImGuiKey_GamepadFaceDown;
    static ImGuiKey ImGuiKey_NavGamepadCancel => GImGui.IO.ConfigNavSwapGamepadButtons ? ImGuiKey_GamepadFaceDown : ImGuiKey_GamepadFaceRight;
    static ImGuiKey ImGuiKey_NavGamepadMenu => ImGuiKey_GamepadFaceLeft;
    static ImGuiKey ImGuiKey_NavGamepadContextMenu => ImGuiKey_GamepadFaceUp;

    // FIXME-NAV: The existence of SetNavID vs SetFocusID vs FocusWindow() needs to be clarified/reworked.
    // In our terminology those should be interchangeable, yet right now this is super confusing.
    // Those two functions are merely a legacy artifact, so at minimum naming should be clarified.

    public static void SetNavCursorVisible(bool visible)
    {
        ImGuiContext g = ImGui.GImGui;
        if (g.NavWindow != null && (g.NavWindow.Flags & ImGuiWindowFlags_NoNavInputs) != 0)
            visible = false;
        else if (g.IO.ConfigNavCursorVisibleAlways)
            visible = true;
        g.NavCursorVisible = visible;
    }

    // (was called NavRestoreHighlightAfterMove() before 1.91.4)
    public static void SetNavCursorVisibleAfterMove()
    {
        ImGuiContext g = ImGui.GImGui;
        if (g.NavWindow != null && (g.NavWindow.Flags & ImGuiWindowFlags_NoNavInputs) != 0)
            g.NavCursorVisible = false;
        else if (g.NavInputSource == ImGuiInputSource_Keyboard && (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) == 0)
            g.NavCursorVisible = false;
        else if (g.NavInputSource == ImGuiInputSource_Gamepad && (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) == 0)
            g.NavCursorVisible = false;
        else if (g.IO.ConfigNavCursorVisibleAuto)
            g.NavCursorVisible = true;
        g.NavHighlightItemUnderNav = g.NavMousePosDirty = true;
    }

    public static void SetNavWindow(ImGuiWindow window)
    {
        ImGuiContext g = ImGui.GImGui;
        if (g.NavWindow != window)
        {
            g.NavWindow = window;
            g.NavLastValidSelectionUserData = ImGuiSelectionUserData_Invalid;
        }
        g.NavInitRequest = g.NavMoveSubmitted = g.NavMoveScoringItems = false;
        NavUpdateAnyRequestFlag();
    }

    public static void NavHighlightActivated(ImGuiID id)
    {
        ImGuiContext g = ImGui.GImGui;
        g.NavHighlightActivatedId = id;
        g.NavHighlightActivatedTimer = NAV_ACTIVATE_HIGHLIGHT_TIMER;
    }

    public static void NavClearPreferredPosForAxis(ImGuiAxis axis)
    {
        ImGuiContext g = ImGui.GImGui;
        g.NavWindow.RootWindowForNav.NavPreferredScoringPosRel[g.NavLayer][axis] = float.MaxValue;
    }

    public static void SetNavID(ImGuiID id, ImGuiNavLayer nav_layer, ImGuiID focus_scope_id, ImRect rect_rel)
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert(g.NavWindow != null);
        System.Diagnostics.Debug.Assert(nav_layer == ImGuiNavLayer_Main || nav_layer == ImGuiNavLayer_Menu);
        g.NavId = id;
        g.NavLayer = nav_layer;
        SetNavFocusScope(focus_scope_id);
        g.NavWindow.NavLastIds[nav_layer] = id;
        g.NavWindow.NavRectRel[nav_layer] = rect_rel;

        // Clear preferred scoring position (NavMoveRequestApplyResult() will tend to restore it)
        NavClearPreferredPosForAxis(ImGuiAxis_X);
        NavClearPreferredPosForAxis(ImGuiAxis_Y);
    }

    public static void SetFocusID(ImGuiID id, ImGuiWindow window)
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert(id != 0);

        if (g.NavWindow != window)
            SetNavWindow(window);

        // Assume that SetFocusID() is called in the context where its window->DC.NavLayerCurrent and g.CurrentFocusScopeId are valid.
        // Note that window may be != g.CurrentWindow (e.g. SetFocusID call in InputTextEx for multi-line text)
        ImGuiNavLayer nav_layer = window.DC.NavLayerCurrent;
        g.NavId = id;
        g.NavLayer = nav_layer;
        SetNavFocusScope(g.CurrentFocusScopeId);
        window.NavLastIds[nav_layer] = id;
        if (g.LastItemData.ID == id)
            window.NavRectRel[nav_layer] = WindowRectAbsToRel(window, g.LastItemData.NavRect);
        g.NavIdItemFlags = (g.LastItemData.ID == id) ? g.LastItemData.ItemFlags : ImGuiItemFlags_None;
        if (id == g.ActiveIdIsAlive)
            g.NavIdIsAlive = true;

        if (g.ActiveIdSource == ImGuiInputSource_Keyboard || g.ActiveIdSource == ImGuiInputSource_Gamepad)
            g.NavHighlightItemUnderNav = true;
        else if (g.IO.ConfigNavCursorVisibleAuto)
            g.NavCursorVisible = false;

        // Clear preferred scoring position (NavMoveRequestApplyResult() will tend to restore it)
        NavClearPreferredPosForAxis(ImGuiAxis_X);
        NavClearPreferredPosForAxis(ImGuiAxis_Y);
    }

    public static ImGuiDir ImGetDirQuadrantFromDelta(float dx, float dy)
    {
        if (MathF.Abs(dx) > MathF.Abs(dy))
            return (dx > 0.0f) ? ImGuiDir_Right : ImGuiDir_Left;
        return (dy > 0.0f) ? ImGuiDir_Down : ImGuiDir_Up;
    }

    public static float NavScoreItemDistInterval(float cand_min, float cand_max, float curr_min, float curr_max)
    {
        if (cand_max < curr_min)
            return cand_max - curr_min;
        if (curr_max < cand_min)
            return cand_min - curr_max;
        return 0.0f;
    }

    // Scoring function for keyboard/gamepad directional navigation. Based on https://gist.github.com/rygorous/6981057
    public static bool NavScoreItem(ref ImGuiNavItemData result, ImRect nav_bb)
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.CurrentWindow;
        if (g.NavLayer != window.DC.NavLayerCurrent)
            return false;

        // FIXME: Those are not good variables names
        ImRect cand = nav_bb;                   // Current item nav rectangle
        ImRect curr = g.NavScoringRect;         // Current modified source rect (NB: we've applied Max.x = Min.x in NavUpdate() to inhibit the effect of having varied item width)
        g.NavScoringDebugCount++;

        // When entering through a NavFlattened border, we consider child window items as fully clipped for scoring
        if (window.ParentWindow == g.NavWindow)
        {
            System.Diagnostics.Debug.Assert(((window.ChildFlags | g.NavWindow.ChildFlags) & ImGuiChildFlags_NavFlattened) != 0);
            if (!window.ClipRect.Overlaps(cand))
                return false;
            cand.ClipWithFull(window.ClipRect); // This allows the scored item to not overlap other candidates in the parent window
        }

        // Compute distance between boxes
        // FIXME-NAV: Introducing biases for vertical navigation, needs to be removed.
        float dbx = NavScoreItemDistInterval(cand.Min.x, cand.Max.x, curr.Min.x, curr.Max.x);
        float dby = NavScoreItemDistInterval(ImMath.ImLerp(cand.Min.y, cand.Max.y, 0.2f), ImMath.ImLerp(cand.Min.y, cand.Max.y, 0.8f), ImMath.ImLerp(curr.Min.y, curr.Max.y, 0.2f), ImMath.ImLerp(curr.Min.y, curr.Max.y, 0.8f)); // Scale down on Y to keep using box-distance for vertically touching items
        if (dby != 0.0f && dbx != 0.0f)
            dbx = (dbx / 1000.0f) + ((dbx > 0.0f) ? +1.0f : -1.0f);
        float dist_box = MathF.Abs(dbx) + MathF.Abs(dby);

        // Compute distance between centers (this is off by a factor of 2, but we only compare center distances with each other so it doesn't matter)
        float dcx = (cand.Min.x + cand.Max.x) - (curr.Min.x + curr.Max.x);
        float dcy = (cand.Min.y + cand.Max.y) - (curr.Min.y + curr.Max.y);
        float dist_center = MathF.Abs(dcx) + MathF.Abs(dcy); // L1 metric (need this for our connectedness guarantee)

        // Determine which quadrant of 'curr' our candidate item 'cand' lies in based on distance
        ImGuiDir quadrant;
        float dax = 0.0f, day = 0.0f, dist_axial = 0.0f;
        if (dbx != 0.0f || dby != 0.0f)
        {
            // For non-overlapping boxes, use distance between boxes
            // FIXME-NAV: Quadrant may be incorrect because of (1) dbx bias and (2) curr.Max.y bias applied by NavBiasScoringRect() where typically curr.Max.y==curr.Min.y
            // One typical case where this happens, with style.WindowMenuButtonPosition == ImGuiDir_Right, pressing Left to navigate from Close to Collapse tends to fail.
            // Also see #6344. Calling ImGetDirQuadrantFromDelta() with unbiased values may be good but side-effects are plenty.
            dax = dbx;
            day = dby;
            dist_axial = dist_box;
            quadrant = ImGetDirQuadrantFromDelta(dbx, dby);
        }
        else if (dcx != 0.0f || dcy != 0.0f)
        {
            // For overlapping boxes with different centers, use distance between centers
            dax = dcx;
            day = dcy;
            dist_axial = dist_center;
            quadrant = ImGetDirQuadrantFromDelta(dcx, dcy);
        }
        else
        {
            // Degenerate case: two overlapping buttons with same center, break ties arbitrarily (note that LastItemId here is really the _previous_ item order, but it doesn't matter)
            quadrant = (g.LastItemData.ID < g.NavId) ? ImGuiDir_Left : ImGuiDir_Right;
        }

        ImGuiDir move_dir = g.NavMoveDir;

        // Is it in the quadrant we're interested in moving to?
        bool new_best = false;
        if (quadrant == move_dir)
        {
            // Does it beat the current best candidate?
            if (dist_box < result.DistBox)
            {
                result.DistBox = dist_box;
                result.DistCenter = dist_center;
                return true;
            }
            if (dist_box == result.DistBox)
            {
                // Try using distance between center points to break ties
                if (dist_center < result.DistCenter)
                {
                    result.DistCenter = dist_center;
                    new_best = true;
                }
                else if (dist_center == result.DistCenter)
                {
                    // Still tied! we need to be extra-careful to make sure everything gets linked properly. We consistently break ties by symbolically moving "later" items
                    // (with higher index) to the right/downwards by an infinitesimal amount since we the current "best" button already (so it must have a lower index),
                    // this is fairly easy. This rule ensures that all buttons with dx==dy==0 will end up being linked in order of appearance along the x axis.
                    if (((move_dir == ImGuiDir_Up || move_dir == ImGuiDir_Down) ? dby : dbx) < 0.0f) // moving bj to the right/down decreases distance
                        new_best = true;
                }
            }
        }

        // Axial check: if 'curr' has no link at all in some direction and 'cand' lies roughly in that direction, add a tentative link. This will only be kept if no "real" matches
        // are found, so it only augments the graph produced by the above method using extra links. (important, since it doesn't guarantee strong connectedness)
        // This is just to avoid buttons having no links in a particular direction when there's a suitable neighbor. you get good graphs without this too.
        // 2017/09/29: FIXME: This now currently only enabled inside menu bars, ideally we'd disable it everywhere. Menus in particular need to catch failure. For general navigation it feels awkward.
        // Disabling it may lead to disconnected graphs when nodes are very spaced out on different axis. Perhaps consider offering this as an option?
        if (result.DistBox == float.MaxValue && dist_axial < result.DistAxial)  // Check axial match
            if (g.NavLayer == ImGuiNavLayer_Menu && (g.NavWindow.Flags & ImGuiWindowFlags_ChildMenu) == 0)
                if ((move_dir == ImGuiDir_Left && dax < 0.0f) || (move_dir == ImGuiDir_Right && dax > 0.0f) || (move_dir == ImGuiDir_Up && day < 0.0f) || (move_dir == ImGuiDir_Down && day > 0.0f))
                {
                    result.DistAxial = dist_axial;
                    new_best = true;
                }

        return new_best;
    }

    public static void NavApplyItemToResult(ref ImGuiNavItemData result)
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.CurrentWindow;
        result.Window = window;
        result.ID = g.LastItemData.ID;
        result.FocusScopeId = g.CurrentFocusScopeId;
        result.ItemFlags = g.LastItemData.ItemFlags;
        result.RectRel = WindowRectAbsToRel(window, g.LastItemData.NavRect);
        if ((result.ItemFlags & ImGuiItemFlags_HasSelectionUserData) != 0)
        {
            System.Diagnostics.Debug.Assert(g.NextItemData.SelectionUserData != ImGuiSelectionUserData_Invalid);
            result.SelectionUserData = g.NextItemData.SelectionUserData; // INTENTIONAL: At this point this field is not cleared in NextItemData. Avoid unnecessary copy to LastItemData.
        }
    }

    // True when current work location may be scrolled horizontally when moving left / right.
    // This is generally always true UNLESS within a column. We don't have a vertical equivalent.
    public static void NavUpdateCurrentWindowIsScrollPushableX()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.CurrentWindow;
        window.DC.NavIsScrollPushableX = (g.CurrentTable == null && window.DC.CurrentColumns == null);
    }

    // We get there when either NavId == id, or when g.NavAnyRequest is set (which is updated by NavUpdateAnyRequestFlag above)
    // This is called after LastItemData is set, but NextItemData is also still valid.
    public static void NavProcessItem()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.CurrentWindow;
        ImGuiID id = g.LastItemData.ID;
        ImGuiItemFlags item_flags = g.LastItemData.ItemFlags;

        // When inside a container that isn't scrollable with Left<>Right, clip NavRect accordingly (#2221, #8816, #7994)
        ImRect nav_bb = g.LastItemData.NavRect;
        if (window.DC.NavIsScrollPushableX == false)
        {
            nav_bb.Min.x = ImMath.ImClamp(nav_bb.Min.x, window.ClipRect.Min.x, window.ClipRect.Max.x);
            nav_bb.Max.x = ImMath.ImClamp(nav_bb.Max.x, window.ClipRect.Min.x, window.ClipRect.Max.x);
        }

        // Process Init Request
        if (g.NavInitRequest && g.NavLayer == window.DC.NavLayerCurrent && (item_flags & ImGuiItemFlags_Disabled) == 0)
        {
            // Even if 'ImGuiItemFlags_NoNavDefaultFocus' is on (typically collapse/close button) we record the first ResultId so they can be used as a fallback
            bool candidate_for_nav_default_focus = (item_flags & ImGuiItemFlags_NoNavDefaultFocus) == 0;
            if (candidate_for_nav_default_focus || g.NavInitResult.ID == 0)
            {
                NavApplyItemToResult(ref g.NavInitResult);
            }
            if (candidate_for_nav_default_focus)
            {
                g.NavInitRequest = false; // Found a match, clear request
                NavUpdateAnyRequestFlag();
            }
        }

        // Process Move Request (scoring for navigation)
        // FIXME-NAV: Consider policy for double scoring (scoring from NavScoringRect + scoring from a rect wrapped according to current wrapping policy)
        if (g.NavMoveScoringItems && (item_flags & ImGuiItemFlags_Disabled) == 0)
        {
            if ((g.NavMoveFlags & ImGuiNavMoveFlags_FocusApi) != 0 || (window.Flags & ImGuiWindowFlags_NoNavInputs) == 0)
            {
                bool is_tabbing = (g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) != 0;
                if (is_tabbing)
                {
                    NavProcessItemForTabbingRequest(id, item_flags, g.NavMoveFlags);
                }
                else if (g.NavId != id || (g.NavMoveFlags & ImGuiNavMoveFlags_AllowCurrentNavId) != 0)
                {
                    ref ImGuiNavItemData result = ref (window == g.NavWindow ? ref g.NavMoveResultLocal : ref g.NavMoveResultOther);
                    if (NavScoreItem(ref result, nav_bb))
                        NavApplyItemToResult(ref result);

                    // Features like PageUp/PageDown need to maintain a separate score for the visible set of items.
                    const float VISIBLE_RATIO = 0.70f;
                    if ((g.NavMoveFlags & ImGuiNavMoveFlags_AlsoScoreVisibleSet) != 0)
                    {
                        ImRect r = window.InnerRect; // window->ClipRect
                        if (r.Overlaps(nav_bb))
                            if (ImMath.ImClamp(nav_bb.Max.y, r.Min.y, r.Max.y) - ImMath.ImClamp(nav_bb.Min.y, r.Min.y, r.Max.y) >= (nav_bb.Max.y - nav_bb.Min.y) * VISIBLE_RATIO)
                                if (NavScoreItem(ref g.NavMoveResultLocalVisible, nav_bb))
                                    NavApplyItemToResult(ref g.NavMoveResultLocalVisible);
                    }
                }
            }
        }

        // Update information for currently focused/navigated item
        if (g.NavId == id)
        {
            if (g.NavWindow != window)
                SetNavWindow(window); // Always refresh g.NavWindow, because some operations such as FocusItem() may not have a window.
            g.NavLayer = window.DC.NavLayerCurrent;
            SetNavFocusScope(g.CurrentFocusScopeId); // Will set g.NavFocusScopeId AND store g.NavFocusScopePath
            g.NavFocusScopeId = g.CurrentFocusScopeId;
            g.NavIdIsAlive = true;
            g.NavIdItemFlags = item_flags;
            if ((g.LastItemData.ItemFlags & ImGuiItemFlags_HasSelectionUserData) != 0)
            {
                System.Diagnostics.Debug.Assert(g.NextItemData.SelectionUserData != ImGuiSelectionUserData_Invalid);
                g.NavLastValidSelectionUserData = g.NextItemData.SelectionUserData; // INTENTIONAL: At this point this field is not cleared in NextItemData. Avoid unnecessary copy to LastItemData.
            }
            window.NavRectRel[window.DC.NavLayerCurrent] = WindowRectAbsToRel(window, nav_bb); // Store item bounding box (relative to window position)
        }
    }

    // Handle "scoring" of an item for a tabbing/focusing request initiated by NavUpdateCreateTabbingRequest().
    // Note that SetKeyboardFocusHere() API calls are considered tabbing requests!
    // - Case 1: no nav/active id:    set result to first eligible item, stop storing.
    // - Case 2: tab forward:         on ref id set counter, on counter elapse store result
    // - Case 3: tab forward wrap:    set result to first eligible item (preemptively), on ref id set counter, on next frame if counter hasn't elapsed store result. // FIXME-TABBING: Could be done as a next-frame forwarded request
    // - Case 4: tab backward:        store all results, on ref id pick prev, stop storing
    // - Case 5: tab backward wrap:   store all results, on ref id if no result keep storing until last // FIXME-TABBING: Could be done as next-frame forwarded requested
    public static void NavProcessItemForTabbingRequest(ImGuiID id, ImGuiItemFlags item_flags, ImGuiNavMoveFlags move_flags)
    {
        ImGuiContext g = ImGui.GImGui;

        if ((move_flags & ImGuiNavMoveFlags_FocusApi) == 0)
        {
            if (g.NavLayer != g.CurrentWindow.DC.NavLayerCurrent)
                return;
            if (g.NavFocusScopeId != g.CurrentFocusScopeId)
                return;
        }

        // - Can always land on an item when using API call.
        // - Tabbing with _NavEnableKeyboard (space/enter/arrows): goes through every item.
        // - Tabbing without _NavEnableKeyboard: goes through inputable items only.
        bool can_stop;
        if ((move_flags & ImGuiNavMoveFlags_FocusApi) != 0)
            can_stop = true;
        else
            can_stop = (item_flags & ImGuiItemFlags_NoTabStop) == 0 && ((g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0 || (item_flags & ImGuiItemFlags_Inputable) != 0);

        // Always store in NavMoveResultLocal (unlike directional request which uses NavMoveResultOther on sibling/flattened windows)
        ref ImGuiNavItemData result = ref g.NavMoveResultLocal;
        if (g.NavTabbingDir == +1)
        {
            // Tab Forward or SetKeyboardFocusHere() with >= 0
            if (can_stop && g.NavTabbingResultFirst.ID == 0)
                NavApplyItemToResult(ref g.NavTabbingResultFirst);
            if (can_stop && g.NavTabbingCounter > 0 && --g.NavTabbingCounter == 0)
                NavMoveRequestResolveWithLastItem(ref result);
            else if (g.NavId == id)
                g.NavTabbingCounter = 1;
        }
        else if (g.NavTabbingDir == -1)
        {
            // Tab Backward
            if (g.NavId == id)
            {
                if (result.ID != 0)
                {
                    g.NavMoveScoringItems = false;
                    NavUpdateAnyRequestFlag();
                }
            }
            else if (can_stop)
            {
                // Keep applying until reaching NavId
                NavApplyItemToResult(ref result);
            }
        }
        else if (g.NavTabbingDir == 0)
        {
            if (can_stop && g.NavId == id)
                NavMoveRequestResolveWithLastItem(ref result);
            if (can_stop && g.NavTabbingResultFirst.ID == 0) // Tab init
                NavApplyItemToResult(ref g.NavTabbingResultFirst);
        }
    }

    public static bool NavMoveRequestButNoResultYet()
    {
        ImGuiContext g = ImGui.GImGui;
        return g.NavMoveScoringItems && g.NavMoveResultLocal.ID == 0 && g.NavMoveResultOther.ID == 0;
    }

    // FIXME: ScoringRect is not set
    public static void NavMoveRequestSubmit(ImGuiDir move_dir, ImGuiDir clip_dir, ImGuiNavMoveFlags move_flags, ImGuiScrollFlags scroll_flags)
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert(g.NavWindow != null);

        if ((move_flags & ImGuiNavMoveFlags_IsTabbing) != 0)
            move_flags |= ImGuiNavMoveFlags_AllowCurrentNavId;

        g.NavMoveSubmitted = g.NavMoveScoringItems = true;
        g.NavMoveDir = move_dir;
        g.NavMoveDirForDebug = move_dir;
        g.NavMoveClipDir = clip_dir;
        g.NavMoveFlags = move_flags;
        g.NavMoveScrollFlags = scroll_flags;
        g.NavMoveForwardToNextFrame = false;
        g.NavMoveKeyMods = (move_flags & ImGuiNavMoveFlags_FocusApi) != 0 ? 0 : g.IO.KeyMods;
        g.NavMoveResultLocal.Clear();
        g.NavMoveResultLocalVisible.Clear();
        g.NavMoveResultOther.Clear();
        g.NavTabbingCounter = 0;
        g.NavTabbingResultFirst.Clear();
        NavUpdateAnyRequestFlag();
    }

    public static void NavMoveRequestResolveWithLastItem(ref ImGuiNavItemData result)
    {
        ImGuiContext g = ImGui.GImGui;
        g.NavMoveScoringItems = false; // Ensure request doesn't need more processing
        NavApplyItemToResult(ref result);
        NavUpdateAnyRequestFlag();
    }

    // Called by TreePop() to implement ImGuiTreeNodeFlags_NavLeftJumpsToParent
    public static void NavMoveRequestResolveWithPastTreeNode(ref ImGuiNavItemData result, ImGuiTreeNodeStackData tree_node_data)
    {
        ImGuiContext g = ImGui.GImGui;
        g.NavMoveScoringItems = false;
        g.LastItemData.ID = tree_node_data.ID;
        g.LastItemData.ItemFlags = tree_node_data.ItemFlags & ~ImGuiItemFlags_HasSelectionUserData; // Losing SelectionUserData, recovered next-frame (cheaper).
        g.LastItemData.NavRect = tree_node_data.NavRect;
        NavApplyItemToResult(ref result); // Result this instead of implementing a NavApplyPastTreeNodeToResult()
        NavClearPreferredPosForAxis(ImGuiAxis_Y);
        NavUpdateAnyRequestFlag();
    }

    public static void NavMoveRequestCancel()
    {
        ImGuiContext g = ImGui.GImGui;
        g.NavMoveSubmitted = g.NavMoveScoringItems = false;
        NavUpdateAnyRequestFlag();
    }

    // Forward will reuse the move request again on the next frame (generally with modifications done to it)
    public static void NavMoveRequestForward(ImGuiDir move_dir, ImGuiDir clip_dir, ImGuiNavMoveFlags move_flags, ImGuiScrollFlags scroll_flags)
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert(g.NavMoveForwardToNextFrame == false);
        NavMoveRequestCancel();
        g.NavMoveForwardToNextFrame = true;
        g.NavMoveDir = move_dir;
        g.NavMoveClipDir = clip_dir;
        g.NavMoveFlags = move_flags | ImGuiNavMoveFlags_Forwarded;
        g.NavMoveScrollFlags = scroll_flags;
    }

    // Navigation wrap-around logic is delayed to the end of the frame because this operation is only valid after entire
    // popup is assembled and in case of appended popups it is not clear which EndPopup() call is final.
    public static void NavMoveRequestTryWrapping(ImGuiWindow window, ImGuiNavMoveFlags wrap_flags)
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert((wrap_flags & ImGuiNavMoveFlags_WrapMask_) != 0 && (wrap_flags & ~ImGuiNavMoveFlags_WrapMask_) == 0); // Call with _WrapX, _WrapY, _LoopX, _LoopY

        // In theory we should test for NavMoveRequestButNoResultYet() but there's no point doing it:
        // as NavEndFrame() will do the same test. It will end up calling NavUpdateCreateWrappingRequest().
        if (g.NavWindow == window && g.NavMoveScoringItems && g.NavLayer == window.DC.NavLayerCurrent)
            g.NavMoveFlags = (g.NavMoveFlags & ~ImGuiNavMoveFlags_WrapMask_) | wrap_flags;
    }

    // FIXME: This could be replaced by updating a frame number in each window when (window == NavWindow) and (NavLayer == 0).
    // This way we could find the last focused window among our children. It would be much less confusing this way?
    public static void NavSaveLastChildNavWindowIntoParent(ImGuiWindow nav_window)
    {
        ImGuiWindow parent = nav_window;
        while (parent != null && parent.RootWindow != parent && (parent.Flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_ChildMenu)) == 0)
            parent = parent.ParentWindow;
        if (parent != null && parent != nav_window)
            parent.NavLastChildNavWindow = nav_window;
    }

    // Restore the last focused child.
    // Call when we are expected to land on the Main Layer (0) after FocusWindow()
    public static ImGuiWindow NavRestoreLastChildNavWindow(ImGuiWindow window)
    {
        if (window.NavLastChildNavWindow != null && window.NavLastChildNavWindow.WasActive)
            return window.NavLastChildNavWindow;
        return window;
    }

    public static void NavRestoreLayer(ImGuiNavLayer layer)
    {
        ImGuiContext g = ImGui.GImGui;
        if (layer == ImGuiNavLayer_Main)
        {
            ImGuiWindow prev_nav_window = g.NavWindow;
            g.NavWindow = NavRestoreLastChildNavWindow(g.NavWindow);    // FIXME-NAV: Should clear ongoing nav requests?
            g.NavLastValidSelectionUserData = ImGuiSelectionUserData_Invalid;
        }
        ImGuiWindow window = g.NavWindow;
        if (window.NavLastIds[layer] != 0)
        {
            SetNavID(window.NavLastIds[layer], layer, 0, window.NavRectRel[layer]);
        }
        else
        {
            g.NavLayer = layer;
            NavInitWindow(window, true);
        }
    }

    public static void NavUpdateAnyRequestFlag()
    {
        ImGuiContext g = ImGui.GImGui;
        g.NavAnyRequest = g.NavMoveScoringItems || g.NavInitRequest;
        if (g.NavAnyRequest)
            System.Diagnostics.Debug.Assert(g.NavWindow != null);
    }

    // This needs to be called before we submit any widget (aka in or before Begin)
    public static void NavInitWindow(ImGuiWindow window, bool force_reinit)
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert(window == g.NavWindow);

        if ((window.Flags & ImGuiWindowFlags_NoNavInputs) != 0)
        {
            g.NavId = 0;
            SetNavFocusScope(window.NavRootFocusScopeId);
            return;
        }

        bool init_for_nav = false;
        if (window == window.RootWindow || (window.Flags & ImGuiWindowFlags_Popup) != 0 || (window.NavLastIds[0] == 0) || force_reinit)
            init_for_nav = true;
        if (init_for_nav)
        {
            SetNavID(0, g.NavLayer, window.NavRootFocusScopeId, new ImRect());
            g.NavInitRequest = true;
            g.NavInitRequestFromMove = false;
            g.NavInitResult.ID = 0;
            NavUpdateAnyRequestFlag();
        }
        else
        {
            g.NavId = window.NavLastIds[0];
            SetNavFocusScope(window.NavRootFocusScopeId);
        }
    }

    // Positioning logic altered slightly for remote activation: for Popup we want to use item rect, for Tooltip we leave things alone. (#9138)
    // When calling for ImGuiWindowFlags_Popup we use LastItemData.
    public static ImGuiInputSource NavCalcPreferredRefPosSource(ImGuiWindowFlags window_type)
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.NavWindow;

        bool activated_shortcut = g.ActiveId != 0 && g.ActiveIdFromShortcut && g.ActiveId == g.LastItemData.ID;
        if ((window_type & ImGuiWindowFlags_Popup) != 0 && activated_shortcut)
            return ImGuiInputSource_Keyboard;

        if (!g.NavCursorVisible || !g.NavHighlightItemUnderNav || window == null)
            return ImGuiInputSource_Mouse;
        else
            return ImGuiInputSource_Keyboard; // or Nav in general
    }

    public static ImVec2 NavCalcPreferredRefPos(ImGuiWindowFlags window_type)
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.NavWindow;
        ImGuiInputSource source = NavCalcPreferredRefPosSource(window_type);

        if (source == ImGuiInputSource_Mouse)
        {
            // Mouse (we need a fallback in case the mouse becomes invalid after being used)
            // The +1.0f offset when stored by OpenPopupEx() allows reopening this or another popup (same or another mouse button) while not moving the mouse, it is pretty standard.
            // In theory we could move that +1.0f offset in OpenPopupEx()
            ImVec2 p = IsMousePosValid(ref g.IO.MousePos) ? g.IO.MousePos : g.MouseLastValidPos;
            return new ImVec2(p.x + 1.0f, p.y);
        }
        else
        {
            // When navigation is active and mouse is disabled, pick a position around the bottom left of the currently navigated item
            bool activated_shortcut = g.ActiveId != 0 && g.ActiveIdFromShortcut && g.ActiveId == g.LastItemData.ID;
            ImRect ref_rect = default;
            if (activated_shortcut && (window_type & ImGuiWindowFlags_Popup) != 0)
                ref_rect = g.LastItemData.NavRect;
            else if (window != null)
                ref_rect = WindowRectRelToAbs(window, window.NavRectRel[g.NavLayer]);

            // Take account of upcoming scrolling (maybe set mouse pos should be done in EndFrame?)
            if (window != null && window.LastFrameActive != g.FrameCount && (window.ScrollTarget.x != float.MaxValue || window.ScrollTarget.y != float.MaxValue))
            {
                ImVec2 next_scroll = CalcNextScrollFromScrollTargetAndClamp(window);
                ref_rect.Translate(window.Scroll - next_scroll);
            }
            ImVec2 pos = new ImVec2(ref_rect.Min.x + ImMath.ImMin(g.Style.FramePadding.x * 4, ref_rect.GetWidth()), ref_rect.Max.y - ImMath.ImMin(g.Style.FramePadding.y, ref_rect.GetHeight()));
            ImGuiViewport viewport = GetMainViewport();
            return ImMath.ImTrunc(ImMath.ImClamp(pos, viewport.Pos, viewport.Pos + viewport.Size)); // ImTrunc() is important because non-integer mouse position application in backend might be lossy and result in undesirable non-zero delta.
        }
    }

    public static float GetNavTweakPressedAmount(ImGuiAxis axis)
    {
        ImGuiContext g = ImGui.GImGui;
        float repeat_delay = 0.0f, repeat_rate = 0.0f;
        GetTypematicRepeatRate(ImGuiInputFlags_RepeatRateNavTweak, ref repeat_delay, ref repeat_rate);

        ImGuiKey key_less, key_more;
        if (g.NavInputSource == ImGuiInputSource_Gamepad)
        {
            key_less = (axis == ImGuiAxis_X) ? ImGuiKey_GamepadDpadLeft : ImGuiKey_GamepadDpadUp;
            key_more = (axis == ImGuiAxis_X) ? ImGuiKey_GamepadDpadRight : ImGuiKey_GamepadDpadDown;
        }
        else
        {
            key_less = (axis == ImGuiAxis_X) ? ImGuiKey_LeftArrow : ImGuiKey_UpArrow;
            key_more = (axis == ImGuiAxis_X) ? ImGuiKey_RightArrow : ImGuiKey_DownArrow;
        }
        float amount = (float)GetKeyPressedAmount(key_more, repeat_delay, repeat_rate) - (float)GetKeyPressedAmount(key_less, repeat_delay, repeat_rate);
        if (amount != 0.0f && IsKeyDown(key_less) && IsKeyDown(key_more)) // Cancel when opposite directions are held, regardless of repeat phase
            amount = 0.0f;
        return amount;
    }

    public static void NavUpdate()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiIO io = g.IO;

        io.WantSetMousePos = false;

        // Set input source based on which keys are last pressed (as some features differs when used with Gamepad vs Keyboard)
        // FIXME-NAV: Now that keys are separated maybe we can get rid of NavInputSource?
        bool nav_gamepad_active = (io.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0 && (io.BackendFlags & ImGuiBackendFlags_HasGamepad) != 0;
        ReadOnlySpan<ImGuiKey> nav_gamepad_keys_to_change_source = stackalloc ImGuiKey[] { ImGuiKey_GamepadFaceRight, ImGuiKey_GamepadFaceLeft, ImGuiKey_GamepadFaceUp, ImGuiKey_GamepadFaceDown, ImGuiKey_GamepadDpadRight, ImGuiKey_GamepadDpadLeft, ImGuiKey_GamepadDpadUp, ImGuiKey_GamepadDpadDown };
        if (nav_gamepad_active && g.NavInputSource != ImGuiInputSource_Gamepad)
            for (int ki = 0; ki < nav_gamepad_keys_to_change_source.Length; ki++)
            {
                ImGuiKey key = nav_gamepad_keys_to_change_source[ki];
                if (IsKeyDown(key))
                    g.NavInputSource = ImGuiInputSource_Gamepad;
            }
        bool nav_keyboard_active = (io.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0;
        ReadOnlySpan<ImGuiKey> nav_keyboard_keys_to_change_source = stackalloc ImGuiKey[] { ImGuiKey_Space, ImGuiKey_Enter, ImGuiKey_Escape, ImGuiKey_RightArrow, ImGuiKey_LeftArrow, ImGuiKey_UpArrow, ImGuiKey_DownArrow };
        if (nav_keyboard_active && g.NavInputSource != ImGuiInputSource_Keyboard)
            for (int ki = 0; ki < nav_keyboard_keys_to_change_source.Length; ki++)
            {
                ImGuiKey key = nav_keyboard_keys_to_change_source[ki];
                if (IsKeyDown(key))
                    g.NavInputSource = ImGuiInputSource_Keyboard;
            }

        // Process navigation init request (select first/default focus)
        g.NavJustMovedToId = 0;
        g.NavJustMovedToFocusScopeId = g.NavJustMovedFromFocusScopeId = 0;
        if (g.NavInitResult.ID != 0)
            NavInitRequestApplyResult();
        g.NavInitRequest = false;
        g.NavInitRequestFromMove = false;
        g.NavInitResult.ID = 0;

        // Process navigation move request
        if (g.NavMoveSubmitted)
            NavMoveRequestApplyResult();
        g.NavTabbingCounter = 0;
        g.NavMoveSubmitted = g.NavMoveScoringItems = false;
        if (g.NavCursorHideFrames > 0)
            if (--g.NavCursorHideFrames == 0)
                g.NavCursorVisible = true;

        // Schedule mouse position update (will be done at the bottom of this function, after 1) processing all move requests and 2) updating scrolling)
        bool set_mouse_pos = false;
        if (g.NavMousePosDirty && g.NavIdIsAlive)
            if (g.NavCursorVisible && g.NavHighlightItemUnderNav && g.NavWindow != null)
                set_mouse_pos = true;
        g.NavMousePosDirty = false;
        System.Diagnostics.Debug.Assert(g.NavLayer == ImGuiNavLayer_Main || g.NavLayer == ImGuiNavLayer_Menu);

        // Store our return window (for returning from Menu Layer to Main Layer) and clear it as soon as we step back in our own Layer 0
        if (g.NavWindow != null)
            NavSaveLastChildNavWindowIntoParent(g.NavWindow);
        if (g.NavWindow != null && g.NavWindow.NavLastChildNavWindow != null && g.NavLayer == ImGuiNavLayer_Main)
            g.NavWindow.NavLastChildNavWindow = null;

        // Update Ctrl+Tab and Windowing features (hold Square to move/resize/etc.)
        NavUpdateWindowing();

        // Set output flags for user application
        io.NavActive = (nav_keyboard_active || nav_gamepad_active) && g.NavWindow != null && (g.NavWindow.Flags & ImGuiWindowFlags_NoNavInputs) == 0;
        io.NavVisible = (io.NavActive && g.NavId != 0 && g.NavCursorVisible) || (g.NavWindowingTarget != null);

        // Process NavCancel input (to close a popup, get back to parent, clear focus)
        NavUpdateCancelRequest();
        NavUpdateContextMenuRequest();

        // Process manual activation request
        g.NavActivateId = g.NavActivateDownId = g.NavActivatePressedId = 0;
        g.NavActivateFlags = ImGuiActivateFlags_None;
        if (g.NavId != 0 && g.NavCursorVisible && g.NavWindowingTarget == null && g.NavWindow != null && (g.NavWindow.Flags & ImGuiWindowFlags_NoNavInputs) == 0)
        {
            bool activate_down = (nav_keyboard_active && IsKeyDown(ImGuiKey_Space, ImGuiKeyOwner_NoOwner)) || (nav_gamepad_active && IsKeyDown(ImGuiKey_NavGamepadActivate, ImGuiKeyOwner_NoOwner));
            bool activate_pressed = activate_down && ((nav_keyboard_active && IsKeyPressed(ImGuiKey_Space, 0, ImGuiKeyOwner_NoOwner)) || (nav_gamepad_active && IsKeyPressed(ImGuiKey_NavGamepadActivate, 0, ImGuiKeyOwner_NoOwner)));
            bool input_pressed_keyboard = nav_keyboard_active && (IsKeyPressed(ImGuiKey_Enter, 0, ImGuiKeyOwner_NoOwner) || IsKeyPressed(ImGuiKey_KeypadEnter, 0, ImGuiKeyOwner_NoOwner));
            bool input_pressed_gamepad = false;
            if (activate_down && nav_gamepad_active && IsKeyDown(ImGuiKey_NavGamepadActivate, ImGuiKeyOwner_NoOwner) && (g.NavIdItemFlags & ImGuiItemFlags_Inputable) != 0) // requires ImGuiItemFlags_Inputable to avoid retriggering regular buttons.
                if (GetKeyData(ImGuiKey_NavGamepadActivate).DownDurationPrev < NAV_ACTIVATE_INPUT_WITH_GAMEPAD_DELAY && GetKeyData(ImGuiKey_NavGamepadActivate).DownDuration >= NAV_ACTIVATE_INPUT_WITH_GAMEPAD_DELAY)
                    input_pressed_gamepad = true;

            if (g.ActiveId == 0 && activate_pressed)
            {
                g.NavActivateId = g.NavId;
                g.NavActivateFlags = ImGuiActivateFlags_PreferTweak;
            }
            if ((g.ActiveId == 0 || g.ActiveId == g.NavId) && (input_pressed_keyboard || input_pressed_gamepad))
            {
                g.NavActivateId = g.NavId;
                g.NavActivateFlags = ImGuiActivateFlags_PreferInput;
            }
            if ((g.ActiveId == 0 || g.ActiveId == g.NavId) && (activate_down || input_pressed_keyboard || input_pressed_gamepad)) // FIXME-NAV: Unsure why input_pressed_xxx (migrated from input_down which was already dubious)
                g.NavActivateDownId = g.NavId;
            if ((g.ActiveId == 0 || g.ActiveId == g.NavId) && (activate_pressed || input_pressed_keyboard || input_pressed_gamepad))
            {
                g.NavActivatePressedId = g.NavId;
                NavHighlightActivated(g.NavId);
            }
        }
        if (g.NavWindow != null && (g.NavWindow.Flags & ImGuiWindowFlags_NoNavInputs) != 0)
            g.NavCursorVisible = false;
        else if (g.IO.ConfigNavCursorVisibleAlways && g.NavCursorHideFrames == 0)
            g.NavCursorVisible = true;
        if (g.NavActivateId != 0)
            System.Diagnostics.Debug.Assert(g.NavActivateDownId == g.NavActivateId);

        // Highlight
        if (g.NavHighlightActivatedTimer > 0.0f)
            g.NavHighlightActivatedTimer = ImMath.ImMax(0.0f, g.NavHighlightActivatedTimer - io.DeltaTime);
        if (g.NavHighlightActivatedTimer == 0.0f)
            g.NavHighlightActivatedId = 0;

        // Process programmatic activation request
        // FIXME-NAV: Those should eventually be queued (unlike focus they don't cancel each others)
        if (g.NavNextActivateId != 0)
        {
            g.NavActivateId = g.NavActivateDownId = g.NavActivatePressedId = g.NavNextActivateId;
            g.NavActivateFlags = g.NavNextActivateFlags;
        }
        g.NavNextActivateId = 0;

        // Process move requests
        NavUpdateCreateMoveRequest();
        if (g.NavMoveDir == ImGuiDir_None)
            NavUpdateCreateTabbingRequest();
        NavUpdateAnyRequestFlag();
        g.NavIdIsAlive = false;

        // Scrolling
        if (g.NavWindow != null && (g.NavWindow.Flags & ImGuiWindowFlags_NoNavInputs) == 0 && g.NavWindowingTarget == null)
        {
            // *Fallback* manual-scroll with Nav directional keys when window has no navigable item
            ImGuiWindow window = g.NavWindow;
            float scroll_speed = ImMath.ImRound(window.FontRefSize * 100 * io.DeltaTime); // We need round the scrolling speed because sub-pixel scroll isn't reliably supported.
            ImGuiDir move_dir = g.NavMoveDir;
            if (window.DC.NavLayersActiveMask == 0x00 && window.DC.NavWindowHasScrollY && move_dir != ImGuiDir_None)
            {
                if (move_dir == ImGuiDir_Left || move_dir == ImGuiDir_Right)
                    SetScrollX(window, ImMath.ImTrunc(window.Scroll.x + ((move_dir == ImGuiDir_Left) ? -1.0f : +1.0f) * scroll_speed));
                if (move_dir == ImGuiDir_Up || move_dir == ImGuiDir_Down)
                    SetScrollY(window, ImMath.ImTrunc(window.Scroll.y + ((move_dir == ImGuiDir_Up) ? -1.0f : +1.0f) * scroll_speed));
            }

            // *Normal* Manual scroll with LStick
            // Next movement request will clamp the NavId reference rectangle to the visible area, so navigation will resume within those bounds.
            if (nav_gamepad_active)
            {
                ImVec2 scroll_dir = GetKeyMagnitude2d(ImGuiKey_GamepadLStickLeft, ImGuiKey_GamepadLStickRight, ImGuiKey_GamepadLStickUp, ImGuiKey_GamepadLStickDown);
                float tweak_factor = IsKeyDown(ImGuiKey_NavGamepadTweakSlow) ? 1.0f / 10.0f : IsKeyDown(ImGuiKey_NavGamepadTweakFast) ? 10.0f : 1.0f;
                if (scroll_dir.x != 0.0f && window.ScrollbarX)
                    SetScrollX(window, ImMath.ImTrunc(window.Scroll.x + scroll_dir.x * scroll_speed * tweak_factor));
                if (scroll_dir.y != 0.0f)
                    SetScrollY(window, ImMath.ImTrunc(window.Scroll.y + scroll_dir.y * scroll_speed * tweak_factor));
            }
        }

        // Always prioritize mouse highlight if navigation is disabled
        if (!nav_keyboard_active && !nav_gamepad_active)
        {
            g.NavCursorVisible = false;
            g.NavHighlightItemUnderNav = set_mouse_pos = false;
        }

        // Update mouse position if requested
        // (This will take into account the possibility that a Scroll was queued in the window to offset our absolute mouse position before scroll has been applied)
        if (set_mouse_pos && io.ConfigNavMoveSetMousePos && (io.BackendFlags & ImGuiBackendFlags_HasSetMousePos) != 0)
            TeleportMousePos(NavCalcPreferredRefPos(ImGuiWindowFlags_Popup));

        // [DEBUG]
        g.NavScoringDebugCount = 0;
    }

    public static void NavInitRequestApplyResult()
    {
        // In very rare cases g.NavWindow may be null (e.g. clearing focus after requesting an init request, which does happen when releasing Alt while clicking on void)
        ImGuiContext g = ImGui.GImGui;
        if (g.NavWindow == null)
            return;

        ref ImGuiNavItemData result = ref g.NavInitResult;
        if (g.NavId != result.ID)
        {
            g.NavJustMovedFromFocusScopeId = g.NavFocusScopeId;
            g.NavJustMovedToId = result.ID;
            g.NavJustMovedToFocusScopeId = result.FocusScopeId;
            g.NavJustMovedToKeyMods = 0;
            g.NavJustMovedToIsTabbing = false;
            g.NavJustMovedToHasSelectionData = (result.ItemFlags & ImGuiItemFlags_HasSelectionUserData) != 0;
        }

        // Apply result from previous navigation init request (will typically select the first item, unless SetItemDefaultFocus() has been called)
        // FIXME-NAV: On _NavFlattened windows, g.NavWindow will only be updated during subsequent frame. Not a problem currently.
        SetNavID(result.ID, g.NavLayer, result.FocusScopeId, result.RectRel);
        g.NavIdIsAlive = true; // Mark as alive from previous frame as we got a result
        if (result.SelectionUserData != ImGuiSelectionUserData_Invalid)
            g.NavLastValidSelectionUserData = result.SelectionUserData;
        if (g.NavInitRequestFromMove)
            SetNavCursorVisibleAfterMove();
    }

    // Bias scoring rect ahead of scoring + update preferred pos (if missing) using source position
    public static void NavBiasScoringRect(ref ImRect r, ref ImVec2 preferred_pos_rel, ImGuiDir move_dir, ImGuiNavMoveFlags move_flags)
    {
        // Bias initial rect
        ImGuiContext g = ImGui.GImGui;
        ImVec2 rel_to_abs_offset = g.NavWindow.DC.CursorStartPos;

        // Initialize bias on departure if we don't have any. So mouse-click + arrow will record bias.
        // - We default to L/U bias, so moving down from a large source item into several columns will land on left-most column.
        // - But each successful move sets new bias on one axis, only cleared when using mouse.
        if ((move_flags & ImGuiNavMoveFlags_Forwarded) == 0)
        {
            if (preferred_pos_rel.x == float.MaxValue)
                preferred_pos_rel.x = ImMath.ImMin(r.Min.x + 1.0f, r.Max.x) - rel_to_abs_offset.x;
            if (preferred_pos_rel.y == float.MaxValue)
                preferred_pos_rel.y = r.GetCenter().y - rel_to_abs_offset.y;
        }

        // Apply general bias on the other axis
        if ((move_dir == ImGuiDir_Up || move_dir == ImGuiDir_Down) && preferred_pos_rel.x != float.MaxValue)
            r.Min.x = r.Max.x = preferred_pos_rel.x + rel_to_abs_offset.x;
        else if ((move_dir == ImGuiDir_Left || move_dir == ImGuiDir_Right) && preferred_pos_rel.y != float.MaxValue)
            r.Min.y = r.Max.y = preferred_pos_rel.y + rel_to_abs_offset.y;
    }

    public static void NavUpdateCreateMoveRequest()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiIO io = g.IO;
        ImGuiWindow window = g.NavWindow;
        bool nav_gamepad_active = (io.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0 && (io.BackendFlags & ImGuiBackendFlags_HasGamepad) != 0;
        bool nav_keyboard_active = (io.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0;

        if (g.NavMoveForwardToNextFrame && window != null)
        {
            // Forwarding previous request (which has been modified, e.g. wrap around menus rewrite the requests with a starting rectangle at the other side of the window)
            // (preserve most state, which were already set by the NavMoveRequestForward() function)
            System.Diagnostics.Debug.Assert(g.NavMoveDir != ImGuiDir_None && g.NavMoveClipDir != ImGuiDir_None);
            System.Diagnostics.Debug.Assert((g.NavMoveFlags & ImGuiNavMoveFlags_Forwarded) != 0);
        }
        else
        {
            // Initiate directional inputs request
            g.NavMoveDir = ImGuiDir_None;
            g.NavMoveFlags = ImGuiNavMoveFlags_None;
            g.NavMoveScrollFlags = ImGuiScrollFlags_None;
            if (window != null && g.NavWindowingTarget == null && (window.Flags & ImGuiWindowFlags_NoNavInputs) == 0)
            {
                ImGuiInputFlags repeat_mode = ImGuiInputFlags_Repeat | (ImGuiInputFlags)ImGuiInputFlags_RepeatRateNavMove;
                if (!IsActiveIdUsingNavDir(ImGuiDir_Left) && ((nav_gamepad_active && IsKeyPressed(ImGuiKey_GamepadDpadLeft, repeat_mode, ImGuiKeyOwner_NoOwner)) || (nav_keyboard_active && IsKeyPressed(ImGuiKey_LeftArrow, repeat_mode, ImGuiKeyOwner_NoOwner)))) { g.NavMoveDir = ImGuiDir_Left; }
                if (!IsActiveIdUsingNavDir(ImGuiDir_Right) && ((nav_gamepad_active && IsKeyPressed(ImGuiKey_GamepadDpadRight, repeat_mode, ImGuiKeyOwner_NoOwner)) || (nav_keyboard_active && IsKeyPressed(ImGuiKey_RightArrow, repeat_mode, ImGuiKeyOwner_NoOwner)))) { g.NavMoveDir = ImGuiDir_Right; }
                if (!IsActiveIdUsingNavDir(ImGuiDir_Up) && ((nav_gamepad_active && IsKeyPressed(ImGuiKey_GamepadDpadUp, repeat_mode, ImGuiKeyOwner_NoOwner)) || (nav_keyboard_active && IsKeyPressed(ImGuiKey_UpArrow, repeat_mode, ImGuiKeyOwner_NoOwner)))) { g.NavMoveDir = ImGuiDir_Up; }
                if (!IsActiveIdUsingNavDir(ImGuiDir_Down) && ((nav_gamepad_active && IsKeyPressed(ImGuiKey_GamepadDpadDown, repeat_mode, ImGuiKeyOwner_NoOwner)) || (nav_keyboard_active && IsKeyPressed(ImGuiKey_DownArrow, repeat_mode, ImGuiKeyOwner_NoOwner)))) { g.NavMoveDir = ImGuiDir_Down; }
            }
            g.NavMoveClipDir = g.NavMoveDir;
            g.NavScoringNoClipRect = new ImRect(+float.MaxValue, +float.MaxValue, -float.MaxValue, -float.MaxValue);
        }

        // Update PageUp/PageDown/Home/End scroll
        // FIXME-NAV: Consider enabling those keys even without the master ImGuiConfigFlags_NavEnableKeyboard flag?
        float scoring_page_offset_y = 0.0f;
        if (window != null && g.NavMoveDir == ImGuiDir_None && nav_keyboard_active)
            scoring_page_offset_y = NavUpdatePageUpPageDown();

        // Submit
        g.NavMoveForwardToNextFrame = false;
        if (g.NavMoveDir != ImGuiDir_None)
            NavMoveRequestSubmit(g.NavMoveDir, g.NavMoveClipDir, g.NavMoveFlags, g.NavMoveScrollFlags);

        // Moving with no reference triggers an init request (will be used as a fallback if the direction fails to find a match)
        if (g.NavMoveSubmitted && g.NavId == 0)
        {
            g.NavInitRequest = g.NavInitRequestFromMove = true;
            g.NavInitResult.ID = 0;
            if (g.IO.ConfigNavCursorVisibleAuto) // NO check for _NoNavInputs here as we assume MoveRequests cannot be created.
                g.NavCursorVisible = true;
        }

        // When using gamepad, we project the reference nav bounding box into window visible area.
        // This is to allow resuming navigation inside the visible area after doing a large amount of scrolling,
        // since with gamepad all movements are relative (can't focus a visible object like we can with the mouse).
        if (g.NavMoveSubmitted && g.NavInputSource == ImGuiInputSource_Gamepad && g.NavLayer == ImGuiNavLayer_Main && window != null)// && (g.NavMoveFlags & ImGuiNavMoveFlags_Forwarded))
        {
            bool clamp_x = (g.NavMoveFlags & (ImGuiNavMoveFlags_LoopX | ImGuiNavMoveFlags_WrapX)) == 0;
            bool clamp_y = (g.NavMoveFlags & (ImGuiNavMoveFlags_LoopY | ImGuiNavMoveFlags_WrapY)) == 0;
            ImRect inner_rect_rel = WindowRectAbsToRel(window, new ImRect(window.InnerRect.Min - new ImVec2(1, 1), window.InnerRect.Max + new ImVec2(1, 1)));

            // Take account of changing scroll to handle triggering a new move request on a scrolling frame. (#6171)
            // Otherwise 'inner_rect_rel' would be off on the move result frame.
            inner_rect_rel.Translate(CalcNextScrollFromScrollTargetAndClamp(window) - window.Scroll);

            if ((clamp_x || clamp_y) && !inner_rect_rel.Contains(window.NavRectRel[g.NavLayer]))
            {
                float pad_x = ImMath.ImMin(inner_rect_rel.GetWidth(), window.FontRefSize * 0.5f);
                float pad_y = ImMath.ImMin(inner_rect_rel.GetHeight(), window.FontRefSize * 0.5f); // Terrible approximation for the intent of starting navigation from first fully visible item
                inner_rect_rel.Min.x = clamp_x ? (inner_rect_rel.Min.x + pad_x) : -float.MaxValue;
                inner_rect_rel.Max.x = clamp_x ? (inner_rect_rel.Max.x - pad_x) : +float.MaxValue;
                inner_rect_rel.Min.y = clamp_y ? (inner_rect_rel.Min.y + pad_y) : -float.MaxValue;
                inner_rect_rel.Max.y = clamp_y ? (inner_rect_rel.Max.y - pad_y) : +float.MaxValue;
                window.NavRectRel[g.NavLayer].ClipWithFull(inner_rect_rel);
                g.NavId = 0;
            }
        }

        // Prepare scoring rectangle.
        // For scoring we use a single segment on the left side our current item bounding box (not touching the edge to avoid box overlap with zero-spaced items)
        ImRect scoring_rect = default;
        if (window != null)
        {
            ImRect nav_rect_rel = !window.NavRectRel[g.NavLayer].IsInverted() ? window.NavRectRel[g.NavLayer] : new ImRect(0, 0, 0, 0);
            scoring_rect = WindowRectRelToAbs(window, nav_rect_rel);

            if ((g.NavMoveFlags & ImGuiNavMoveFlags_IsPageMove) != 0)
            {
                // When we start from a visible location, score visible items and prioritize this result.
                if (window.InnerRect.Contains(scoring_rect))
                    g.NavMoveFlags |= ImGuiNavMoveFlags_AlsoScoreVisibleSet;
                g.NavScoringNoClipRect = scoring_rect;
                scoring_rect.TranslateY(scoring_page_offset_y);
                g.NavScoringNoClipRect.Add(scoring_rect);
            }

            if (g.NavMoveSubmitted)
                NavBiasScoringRect(ref scoring_rect, ref window.RootWindowForNav.NavPreferredScoringPosRel[g.NavLayer], g.NavMoveDir, g.NavMoveFlags);
            System.Diagnostics.Debug.Assert(!scoring_rect.IsInverted()); // Ensure we have a non-inverted bounding box here will allow us to remove extraneous ImFabs() calls in NavScoreItem().
        }
        g.NavScoringRect = scoring_rect;
    }

    public static void NavUpdateCreateTabbingRequest()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.NavWindow;
        System.Diagnostics.Debug.Assert(g.NavMoveDir == ImGuiDir_None);
        if (window == null || g.NavWindowingTarget != null || (window.Flags & ImGuiWindowFlags_NoNavInputs) != 0 || !g.ConfigNavEnableTabbing)
            return;

        bool tab_pressed = IsKeyPressed(ImGuiKey_Tab, ImGuiInputFlags_Repeat, ImGuiKeyOwner_NoOwner) && !g.IO.KeyCtrl && !g.IO.KeyAlt;
        if (!tab_pressed)
            return;

        // Initiate tabbing request
        // (this is ALWAYS ENABLED, regardless of ImGuiConfigFlags_NavEnableKeyboard flag!)
        // See NavProcessItemForTabbingRequest() for a description of the various forward/backward tabbing cases with and without wrapping.
        bool nav_keyboard_active = (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0;
        if (nav_keyboard_active)
            g.NavTabbingDir = g.IO.KeyShift ? -1 : (g.NavCursorVisible == false && g.ActiveId == 0) ? 0 : +1;
        else
            g.NavTabbingDir = g.IO.KeyShift ? -1 : (g.ActiveId == 0) ? 0 : +1;
        ImGuiNavMoveFlags move_flags = ImGuiNavMoveFlags_IsTabbing | ImGuiNavMoveFlags_Activate;
        ImGuiScrollFlags scroll_flags = window.Appearing ? ImGuiScrollFlags_KeepVisibleEdgeX | ImGuiScrollFlags_AlwaysCenterY : ImGuiScrollFlags_KeepVisibleEdgeX | ImGuiScrollFlags_KeepVisibleEdgeY;
        ImGuiDir clip_dir = (g.NavTabbingDir < 0) ? ImGuiDir_Up : ImGuiDir_Down;
        NavMoveRequestSubmit(ImGuiDir_None, clip_dir, move_flags, scroll_flags); // FIXME-NAV: Once we refactor tabbing, add LegacyApi flag to not activate non-inputable.
        g.NavTabbingCounter = -1;
    }

    // Apply result from previous frame navigation directional move request. Always called from NavUpdate()
    public static void NavMoveRequestApplyResult()
    {
        ImGuiContext g = ImGui.GImGui;

        // Select which result to use.
        // (C++ uses a nullable ImGuiNavItemData* pointer; mirrored here with a ref local + a kind tag: 0 = NULL, 1 = Local, 2 = Other, 3 = TabbingResultFirst, 4 = LocalVisible)
        ref ImGuiNavItemData result = ref g.NavMoveResultLocal;
        int result_kind = (g.NavMoveResultLocal.ID != 0) ? 1 : (g.NavMoveResultOther.ID != 0) ? 2 : 0;

        // Tabbing forward wrap
        if ((g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) != 0 && result_kind == 0)
            if ((g.NavTabbingCounter == 1 || g.NavTabbingDir == 0) && g.NavTabbingResultFirst.ID != 0)
            {
                result = ref g.NavTabbingResultFirst;
                result_kind = 3;
            }

        // In a situation when there are no results but NavId != 0, re-enable the Navigation highlight (because g.NavId is not considered as a possible result)
        ImGuiAxis axis = (g.NavMoveDir == ImGuiDir_Up || g.NavMoveDir == ImGuiDir_Down) ? ImGuiAxis_Y : ImGuiAxis_X;
        if (result_kind == 0)
        {
            if ((g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) != 0)
                g.NavMoveFlags |= ImGuiNavMoveFlags_NoSetNavCursorVisible;
            if (g.NavId != 0 && (g.NavMoveFlags & ImGuiNavMoveFlags_NoSetNavCursorVisible) == 0)
                SetNavCursorVisibleAfterMove();
            NavClearPreferredPosForAxis(axis); // On a failed move, clear preferred pos for this axis.
            return;
        }

        // PageUp/PageDown behavior first jumps to the bottom/top mostly visible item, _otherwise_ use the result from the previous/next page.
        if ((g.NavMoveFlags & ImGuiNavMoveFlags_AlsoScoreVisibleSet) != 0)
            if (g.NavMoveResultLocalVisible.ID != 0 && g.NavMoveResultLocalVisible.ID != g.NavId)
            {
                result = ref g.NavMoveResultLocalVisible;
                result_kind = 4;
            }

        // Maybe entering a flattened child from the outside? In this case solve the tie using the regular scoring rules.
        if (result_kind != 2 && g.NavMoveResultOther.ID != 0 && g.NavMoveResultOther.Window.ParentWindow == g.NavWindow)
            if ((g.NavMoveResultOther.DistBox < result.DistBox) || (g.NavMoveResultOther.DistBox == result.DistBox && g.NavMoveResultOther.DistCenter < result.DistCenter))
            {
                result = ref g.NavMoveResultOther;
                result_kind = 2;
            }
        System.Diagnostics.Debug.Assert(g.NavWindow != null && result.Window != null);

        // Scroll to keep newly navigated item fully into view.
        if (g.NavLayer == ImGuiNavLayer_Main)
        {
            ImRect rect_abs = WindowRectRelToAbs(result.Window, result.RectRel);
            ScrollToRectEx(result.Window, rect_abs, g.NavMoveScrollFlags);

            if ((g.NavMoveFlags & ImGuiNavMoveFlags_ScrollToEdgeY) != 0)
            {
                // FIXME: Should remove this? Or make more precise: use ScrollToRectEx() with edge?
                float scroll_target = (g.NavMoveDir == ImGuiDir_Up) ? result.Window.ScrollMax.y : 0.0f;
                SetScrollY(result.Window, scroll_target);
            }
        }

        if (g.NavWindow != result.Window)
        {
            g.NavWindow = result.Window;
            g.NavLastValidSelectionUserData = ImGuiSelectionUserData_Invalid;
        }

        // Clear active id unless requested not to
        // FIXME: ImGuiNavMoveFlags_NoClearActiveId is currently unused as we don't have a clear strategy to preserve active id after interaction,
        // so this is mostly provided as a gateway for further experiments (see #1418, #2890)
        if (g.ActiveId != result.ID && (g.NavMoveFlags & ImGuiNavMoveFlags_NoClearActiveId) == 0)
            ClearActiveID();

        // Don't set NavJustMovedToId if just landed on the same spot (which may happen with ImGuiNavMoveFlags_AllowCurrentNavId)
        // PageUp/PageDown however sets always set NavJustMovedTo (vs Home/End which doesn't) mimicking Windows behavior.
        if ((g.NavId != result.ID || (g.NavMoveFlags & ImGuiNavMoveFlags_IsPageMove) != 0) && (g.NavMoveFlags & ImGuiNavMoveFlags_NoSelect) == 0)
        {
            g.NavJustMovedFromFocusScopeId = g.NavFocusScopeId;
            g.NavJustMovedToId = result.ID;
            g.NavJustMovedToFocusScopeId = result.FocusScopeId;
            g.NavJustMovedToKeyMods = g.NavMoveKeyMods;
            g.NavJustMovedToIsTabbing = (g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) != 0;
            g.NavJustMovedToHasSelectionData = (result.ItemFlags & ImGuiItemFlags_HasSelectionUserData) != 0;
        }

        // Apply new NavID/Focus
        ImVec2 preferred_scoring_pos_rel = g.NavWindow.RootWindowForNav.NavPreferredScoringPosRel[g.NavLayer];
        SetNavID(result.ID, g.NavLayer, result.FocusScopeId, result.RectRel);
        if (result.SelectionUserData != ImGuiSelectionUserData_Invalid)
            g.NavLastValidSelectionUserData = result.SelectionUserData;

        // Restore last preferred position for current axis
        // (storing in RootWindowForNav-> as the info is desirable at the beginning of a Move Request. In theory all storage should use RootWindowForNav..)
        if ((g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) == 0)
        {
            preferred_scoring_pos_rel[axis] = result.RectRel.GetCenter()[axis];
            g.NavWindow.RootWindowForNav.NavPreferredScoringPosRel[g.NavLayer] = preferred_scoring_pos_rel;
        }

        // Tabbing: Activates Inputable, otherwise only Focus
        if ((g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) != 0 && (result.ItemFlags & ImGuiItemFlags_Inputable) == 0)
            g.NavMoveFlags &= ~ImGuiNavMoveFlags_Activate;

        // Activate
        if ((g.NavMoveFlags & ImGuiNavMoveFlags_Activate) != 0)
        {
            g.NavNextActivateId = result.ID;
            g.NavNextActivateFlags = ImGuiActivateFlags_None;
            if ((g.NavMoveFlags & ImGuiNavMoveFlags_FocusApi) != 0)
                g.NavNextActivateFlags |= ImGuiActivateFlags_FromFocusApi;
            if ((g.NavMoveFlags & ImGuiNavMoveFlags_IsTabbing) != 0)
                g.NavNextActivateFlags |= ImGuiActivateFlags_PreferInput | ImGuiActivateFlags_TryToPreserveState | ImGuiActivateFlags_FromTabbing;
        }

        // Make nav cursor visible
        if ((g.NavMoveFlags & ImGuiNavMoveFlags_NoSetNavCursorVisible) == 0)
            SetNavCursorVisibleAfterMove();
    }

    // Process Escape/NavCancel input (to close a popup, get back to parent, clear focus)
    // FIXME: In order to support e.g. Escape to clear a selection we'll need:
    // - either to store the equivalent of ActiveIdUsingKeyInputMask for a FocusScope and test for it.
    // - either to move most/all of those tests to the epilogue/end functions of the scope they are dealing with (e.g. exit child window in EndChild()) or in EndFrame(), to allow an earlier intercept
    public static void NavUpdateCancelRequest()
    {
        ImGuiContext g = ImGui.GImGui;
        bool nav_gamepad_active = (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0 && (g.IO.BackendFlags & ImGuiBackendFlags_HasGamepad) != 0;
        bool nav_keyboard_active = (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0;
        if (!(nav_keyboard_active && IsKeyPressed(ImGuiKey_Escape, 0, ImGuiKeyOwner_NoOwner)) && !(nav_gamepad_active && IsKeyPressed(ImGuiKey_NavGamepadCancel, 0, ImGuiKeyOwner_NoOwner)))
            return;

        if (g.ActiveId != 0)
        {
            ClearActiveID();
        }
        else if (g.NavLayer != ImGuiNavLayer_Main)
        {
            // Leave the "menu" layer
            NavRestoreLayer(ImGuiNavLayer_Main);
            SetNavCursorVisibleAfterMove();
        }
        else if (g.NavWindow != null && g.NavWindow != g.NavWindow.RootWindow && (g.NavWindow.RootWindowForNav.Flags & ImGuiWindowFlags_Popup) == 0 && g.NavWindow.RootWindowForNav.ParentWindow != null)
        {
            // Exit child window
            ImGuiWindow child_window = g.NavWindow.RootWindowForNav;
            ImGuiWindow parent_window = child_window.ParentWindow;
            System.Diagnostics.Debug.Assert(child_window.ChildId != 0);
            FocusWindow(parent_window);
            SetNavID(child_window.ChildId, ImGuiNavLayer_Main, 0, WindowRectAbsToRel(parent_window, child_window.Rect()));
            SetNavCursorVisibleAfterMove();
        }
        else if (g.OpenPopupStack.Size > 0 && g.OpenPopupStack.back().Window != null && (g.OpenPopupStack.back().Window.Flags & ImGuiWindowFlags_Modal) == 0)
        {
            // Close open popup/menu
            ClosePopupToLevel(g.OpenPopupStack.Size - 1, true);
        }
        else
        {
            // Clear NavLastId for popups but keep it for regular child window so we can leave one and come back where we were
            // FIXME-NAV: This should happen on window appearing.
            if (g.IO.ConfigNavEscapeClearFocusItem || g.IO.ConfigNavEscapeClearFocusWindow)
                if (g.NavWindow != null && ((g.NavWindow.Flags & ImGuiWindowFlags_Popup) != 0))// || !(g.NavWindow->Flags & ImGuiWindowFlags_ChildWindow)))
                    g.NavWindow.NavLastIds[0] = 0;

            // Clear nav focus
            if (g.IO.ConfigNavEscapeClearFocusItem || g.IO.ConfigNavEscapeClearFocusWindow)
                g.NavId = 0;
            if (g.IO.ConfigNavEscapeClearFocusWindow)
                FocusWindow(null);
        }
    }

    public static void NavUpdateContextMenuRequest()
    {
        ImGuiContext g = ImGui.GImGui;
        g.NavOpenContextMenuItemId = g.NavOpenContextMenuWindowId = 0;
        bool nav_keyboard_active = (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0;
        bool nav_gamepad_active = (g.IO.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0;
        if ((!nav_keyboard_active && !nav_gamepad_active) || g.NavWindow == null)
            return;

        bool request = false;
        request |= nav_keyboard_active && (IsKeyReleased(ImGuiKey_Menu, ImGuiKeyOwner_NoOwner) || (IsKeyPressed(ImGuiKey_F10, ImGuiInputFlags_None, ImGuiKeyOwner_NoOwner) && g.IO.KeyMods == ImGuiMod_Shift));
        request |= nav_gamepad_active && IsKeyPressed(ImGuiKey_NavGamepadContextMenu, ImGuiInputFlags_None, ImGuiKeyOwner_NoOwner);
        if (!request)
            return;
        g.NavOpenContextMenuItemId = g.NavId;
        g.NavOpenContextMenuWindowId = g.NavWindow.ID;

        // Allow triggering for Begin()..BeginPopupContextItem(). A possible alternative would be to use g.NavLayer == ImGuiNavLayer_Menu.
        if (g.NavId == g.NavWindow.GetID("#CLOSE") || g.NavId == g.NavWindow.GetID("#COLLAPSE"))
            g.NavOpenContextMenuItemId = g.NavWindow.MoveId;

        g.NavInputSource = ImGuiInputSource_Keyboard;
        SetNavCursorVisibleAfterMove();
    }

    // Handle PageUp/PageDown/Home/End keys
    // Called from NavUpdateCreateMoveRequest() which will use our output to create a move request
    // FIXME-NAV: This doesn't work properly with NavFlattened siblings as we use NavWindow rectangle for reference
    // FIXME-NAV: how to get Home/End to aim at the beginning/end of a 2D grid?
    public static float NavUpdatePageUpPageDown()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.NavWindow;
        if ((window.Flags & ImGuiWindowFlags_NoNavInputs) != 0 || g.NavWindowingTarget != null)
            return 0.0f;

        bool page_up_held = IsKeyDown(ImGuiKey_PageUp, ImGuiKeyOwner_NoOwner);
        bool page_down_held = IsKeyDown(ImGuiKey_PageDown, ImGuiKeyOwner_NoOwner);
        bool home_pressed = IsKeyPressed(ImGuiKey_Home, ImGuiInputFlags_Repeat, ImGuiKeyOwner_NoOwner);
        bool end_pressed = IsKeyPressed(ImGuiKey_End, ImGuiInputFlags_Repeat, ImGuiKeyOwner_NoOwner);
        if (page_up_held == page_down_held && home_pressed == end_pressed) // Proceed if either (not both) are pressed, otherwise early out
            return 0.0f;

        if (g.NavLayer != ImGuiNavLayer_Main)
            NavRestoreLayer(ImGuiNavLayer_Main);

        if ((window.DC.NavLayersActiveMask & (1 << ImGuiNavLayer_Main)) == 0 && window.DC.NavWindowHasScrollY)
        {
            // Fallback manual-scroll when window has no navigable item
            if (IsKeyPressed(ImGuiKey_PageUp, ImGuiInputFlags_Repeat, ImGuiKeyOwner_NoOwner))
                SetScrollY(window, window.Scroll.y - window.InnerRect.GetHeight());
            else if (IsKeyPressed(ImGuiKey_PageDown, ImGuiInputFlags_Repeat, ImGuiKeyOwner_NoOwner))
                SetScrollY(window, window.Scroll.y + window.InnerRect.GetHeight());
            else if (home_pressed)
                SetScrollY(window, 0.0f);
            else if (end_pressed)
                SetScrollY(window, window.ScrollMax.y);
        }
        else
        {
            ref ImRect nav_rect_rel = ref window.NavRectRel[g.NavLayer];
            float page_offset_y = ImMath.ImMax(0.0f, window.InnerRect.GetHeight() - window.FontRefSize * 1.0f + nav_rect_rel.GetHeight());
            float nav_scoring_rect_offset_y = 0.0f;
            if (IsKeyPressed(ImGuiKey_PageUp, true))
            {
                nav_scoring_rect_offset_y = -page_offset_y;
                g.NavMoveDir = ImGuiDir_Down; // Because our scoring rect is offset up, we request the down direction (so we can always land on the last item)
                g.NavMoveClipDir = ImGuiDir_Up;
                g.NavMoveFlags = ImGuiNavMoveFlags_AllowCurrentNavId | ImGuiNavMoveFlags_IsPageMove; // ImGuiNavMoveFlags_AlsoScoreVisibleSet may be added later
            }
            else if (IsKeyPressed(ImGuiKey_PageDown, true))
            {
                nav_scoring_rect_offset_y = +page_offset_y;
                g.NavMoveDir = ImGuiDir_Up; // Because our scoring rect is offset down, we request the up direction (so we can always land on the last item)
                g.NavMoveClipDir = ImGuiDir_Down;
                g.NavMoveFlags = ImGuiNavMoveFlags_AllowCurrentNavId | ImGuiNavMoveFlags_IsPageMove; // ImGuiNavMoveFlags_AlsoScoreVisibleSet may be added later
            }
            else if (home_pressed)
            {
                // FIXME-NAV: handling of Home/End is assuming that the top/bottom most item will be visible with Scroll.y == 0/ScrollMax.y
                // Scrolling will be handled via the ImGuiNavMoveFlags_ScrollToEdgeY flag, we don't scroll immediately to avoid scrolling happening before nav result.
                // Preserve current horizontal position if we have any.
                nav_rect_rel.Min.y = nav_rect_rel.Max.y = 0.0f;
                if (nav_rect_rel.IsInverted())
                    nav_rect_rel.Min.x = nav_rect_rel.Max.x = 0.0f;
                g.NavMoveDir = ImGuiDir_Down;
                g.NavMoveFlags = ImGuiNavMoveFlags_AllowCurrentNavId | ImGuiNavMoveFlags_ScrollToEdgeY;
                // FIXME-NAV: MoveClipDir left to _None, intentional?
            }
            else if (end_pressed)
            {
                nav_rect_rel.Min.y = nav_rect_rel.Max.y = window.ContentSize.y;
                if (nav_rect_rel.IsInverted())
                    nav_rect_rel.Min.x = nav_rect_rel.Max.x = 0.0f;
                g.NavMoveDir = ImGuiDir_Up;
                g.NavMoveFlags = ImGuiNavMoveFlags_AllowCurrentNavId | ImGuiNavMoveFlags_ScrollToEdgeY;
                // FIXME-NAV: MoveClipDir left to _None, intentional?
            }
            return nav_scoring_rect_offset_y;
        }
        return 0.0f;
    }

    public static void NavEndFrame()
    {
        ImGuiContext g = ImGui.GImGui;

        // Show Ctrl+Tab list window
        if (g.NavWindowingTarget != null)
            NavUpdateWindowingOverlay();

        // Perform wrap-around in menus
        // FIXME-NAV: Wrap may need to apply a weight bias on the other axis. e.g. 4x4 grid with 2 last items missing on last item won't handle LoopY/WrapY correctly.
        // FIXME-NAV: Wrap (not Loop) support could be handled by the scoring function and then WrapX would function without an extra frame.
        if (g.NavWindow != null && NavMoveRequestButNoResultYet() && (g.NavMoveFlags & ImGuiNavMoveFlags_WrapMask_) != 0 && (g.NavMoveFlags & ImGuiNavMoveFlags_Forwarded) == 0)
            NavUpdateCreateWrappingRequest();
    }

    public static void NavUpdateCreateWrappingRequest()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiWindow window = g.NavWindow;

        bool do_forward = false;
        ImRect bb_rel = window.NavRectRel[g.NavLayer];
        ImGuiDir clip_dir = g.NavMoveDir;

        ImGuiNavMoveFlags move_flags = g.NavMoveFlags;
        //const ImGuiAxis move_axis = (g.NavMoveDir == ImGuiDir_Up || g.NavMoveDir == ImGuiDir_Down) ? ImGuiAxis_Y : ImGuiAxis_X;

        // Menu layer does not maintain scrolling / content size (#9178)
        ImVec2 wrap_size = (g.NavLayer == ImGuiNavLayer_Menu) ? window.Size : window.ContentSize + window.WindowPadding;

        if (g.NavMoveDir == ImGuiDir_Left && (move_flags & (ImGuiNavMoveFlags_WrapX | ImGuiNavMoveFlags_LoopX)) != 0)
        {
            bb_rel.Min.x = bb_rel.Max.x = wrap_size.x;
            if ((move_flags & ImGuiNavMoveFlags_WrapX) != 0)
            {
                bb_rel.TranslateY(-bb_rel.GetHeight()); // Previous row
                clip_dir = ImGuiDir_Up;
            }
            do_forward = true;
        }
        if (g.NavMoveDir == ImGuiDir_Right && (move_flags & (ImGuiNavMoveFlags_WrapX | ImGuiNavMoveFlags_LoopX)) != 0)
        {
            bb_rel.Min.x = bb_rel.Max.x = -window.WindowPadding.x;
            if ((move_flags & ImGuiNavMoveFlags_WrapX) != 0)
            {
                bb_rel.TranslateY(+bb_rel.GetHeight()); // Next row
                clip_dir = ImGuiDir_Down;
            }
            do_forward = true;
        }
        if (g.NavMoveDir == ImGuiDir_Up && (move_flags & (ImGuiNavMoveFlags_WrapY | ImGuiNavMoveFlags_LoopY)) != 0)
        {
            bb_rel.Min.y = bb_rel.Max.y = wrap_size.y;
            if ((move_flags & ImGuiNavMoveFlags_WrapY) != 0)
            {
                bb_rel.TranslateX(-bb_rel.GetWidth()); // Previous column
                clip_dir = ImGuiDir_Left;
            }
            do_forward = true;
        }
        if (g.NavMoveDir == ImGuiDir_Down && (move_flags & (ImGuiNavMoveFlags_WrapY | ImGuiNavMoveFlags_LoopY)) != 0)
        {
            bb_rel.Min.y = bb_rel.Max.y = -window.WindowPadding.y;
            if ((move_flags & ImGuiNavMoveFlags_WrapY) != 0)
            {
                bb_rel.TranslateX(+bb_rel.GetWidth()); // Next column
                clip_dir = ImGuiDir_Right;
            }
            do_forward = true;
        }
        if (!do_forward)
            return;
        window.NavRectRel[g.NavLayer] = bb_rel;
        NavClearPreferredPosForAxis(ImGuiAxis_X);
        NavClearPreferredPosForAxis(ImGuiAxis_Y);
        NavMoveRequestForward(g.NavMoveDir, clip_dir, move_flags, g.NavMoveScrollFlags);
    }

    // Can we focus this window with Ctrl+Tab (or PadMenu + PadFocusPrev/PadFocusNext)
    // Note that NoNavFocus makes the window not reachable with Ctrl+Tab but it can still be focused with mouse or programmatically.
    // If you want a window to never be focused, you may use the e.g. NoInputs flag.
    public static bool IsWindowNavFocusable(ImGuiWindow window)
    {
        return window.WasActive && window == window.RootWindow && (window.Flags & ImGuiWindowFlags_NoNavFocus) == 0;
    }

    public static ImGuiWindow FindWindowNavFocusable(int i_start, int i_stop, int dir) // FIXME-OPT O(N)
    {
        ImGuiContext g = ImGui.GImGui;
        for (int i = i_start; i >= 0 && i < g.WindowsFocusOrder.Size && i != i_stop; i += dir)
            if (IsWindowNavFocusable(g.WindowsFocusOrder[i]))
                return g.WindowsFocusOrder[i];
        return null;
    }

    public static void NavUpdateWindowingTarget(int focus_change_dir)
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert(g.NavWindowingTarget != null);
        if ((g.NavWindowingTarget.Flags & ImGuiWindowFlags_Modal) != 0)
            return;

        int i_current = FindWindowFocusIndex(g.NavWindowingTarget);
        ImGuiWindow window_target = FindWindowNavFocusable(i_current + focus_change_dir, -int.MaxValue, focus_change_dir);
        if (window_target == null)
            window_target = FindWindowNavFocusable((focus_change_dir < 0) ? (g.WindowsFocusOrder.Size - 1) : 0, i_current, focus_change_dir);
        if (window_target != null) // Don't reset windowing target if there's a single window in the list
        {
            g.NavWindowingTarget = g.NavWindowingTargetAnim = window_target;
            g.NavWindowingAccumDeltaPos = g.NavWindowingAccumDeltaSize = new ImVec2(0.0f, 0.0f);
        }
        g.NavWindowingToggleLayer = false;
    }

    // Apply focus and close overlay
    public static void NavUpdateWindowingApplyFocus(ImGuiWindow apply_focus_window)
    {
        ImGuiContext g = ImGui.GImGui;
        if (g.NavWindow == null || apply_focus_window != g.NavWindow.RootWindow)
        {
            ClearActiveID();
            SetNavCursorVisibleAfterMove();
            ClosePopupsOverWindow(apply_focus_window, false);
            FocusWindow(apply_focus_window, ImGuiFocusRequestFlags_RestoreFocusedChild);
            System.Diagnostics.Debug.Assert(g.NavWindow != null);
            apply_focus_window = g.NavWindow;
            if (apply_focus_window.NavLastIds[0] == 0)
                NavInitWindow(apply_focus_window, false);

            // If the window has ONLY a menu layer (no main layer), select it directly
            // Use NavLayersActiveMaskNext since windows didn't have a chance to be Begin()-ed on this frame,
            // so Ctrl+Tab where the keys are only held for 1 frame will be able to use correct layers mask since
            // the target window as already been previewed once.
            // FIXME-NAV: This should be done in NavInit.. or in FocusWindow... However in both of those cases,
            // we won't have a guarantee that windows has been visible before and therefore NavLayersActiveMask*
            // won't be valid.
            if (apply_focus_window.DC.NavLayersActiveMaskNext == (1 << ImGuiNavLayer_Menu))
                g.NavLayer = ImGuiNavLayer_Menu;
        }
        g.NavWindowingTarget = null;
    }

    // Windowing management mode
    // Keyboard: Ctrl+Tab (change focus/move/resize), Alt (toggle menu layer)
    // Gamepad:  Hold Menu/Square (change focus/move/resize), Tap Menu/Square (toggle menu layer)
    public static void NavUpdateWindowing()
    {
        ImGuiContext g = ImGui.GImGui;
        ImGuiIO io = g.IO;

        ImGuiWindow apply_focus_window = null;
        bool apply_toggle_layer = false;

        ImGuiWindow modal_window = GetTopMostPopupModal();
        bool allow_windowing = (modal_window == null); // FIXME: This prevent Ctrl+Tab from being usable with windows that are inside the Begin-stack of that modal.
        if (!allow_windowing)
            g.NavWindowingTarget = null;

        // Fade out
        if (g.NavWindowingTargetAnim != null && g.NavWindowingTarget == null)
        {
            g.NavWindowingHighlightAlpha = ImMath.ImMax(g.NavWindowingHighlightAlpha - io.DeltaTime * 10.0f, 0.0f);
            if (g.DimBgRatio <= 0.0f && g.NavWindowingHighlightAlpha <= 0.0f)
                g.NavWindowingTargetAnim = null;
        }

        // Start Ctrl+Tab or Square+L/R window selection
        // (g.ConfigNavWindowingKeyNext/g.ConfigNavWindowingKeyPrev defaults are ImGuiMod_Ctrl|ImGuiKey_Tab and ImGuiMod_Ctrl|ImGuiMod_Shift|ImGuiKey_Tab)
        ImGuiID owner_id = ImGuiHash.ImHashStr("##NavUpdateWindowing", 0);
        bool nav_gamepad_active = (io.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) != 0 && (io.BackendFlags & ImGuiBackendFlags_HasGamepad) != 0;
        bool nav_keyboard_active = (io.ConfigFlags & ImGuiConfigFlags_NavEnableKeyboard) != 0;
        bool keyboard_next_window = allow_windowing && g.ConfigNavWindowingKeyNext != 0 && Shortcut(g.ConfigNavWindowingKeyNext, ImGuiInputFlags_Repeat | ImGuiInputFlags_RouteAlways, owner_id);
        bool keyboard_prev_window = allow_windowing && g.ConfigNavWindowingKeyPrev != 0 && Shortcut(g.ConfigNavWindowingKeyPrev, ImGuiInputFlags_Repeat | ImGuiInputFlags_RouteAlways, owner_id);
        bool start_toggling_with_gamepad = nav_gamepad_active && g.NavWindowingTarget == null && Shortcut(ImGuiKey_NavGamepadMenu, ImGuiInputFlags_RouteAlways, owner_id);
        bool start_windowing_with_gamepad = allow_windowing && start_toggling_with_gamepad;
        bool start_windowing_with_keyboard = allow_windowing && g.NavWindowingTarget == null && (keyboard_next_window || keyboard_prev_window); // Note: enabled even without NavEnableKeyboard!
        bool just_started_windowing_from_null_focus = false;
        if (start_toggling_with_gamepad)
        {
            g.NavWindowingToggleLayer = true; // Gamepad starts toggling layer
            g.NavWindowingToggleKey = ImGuiKey_NavGamepadMenu;
            g.NavWindowingInputSource = g.NavInputSource = ImGuiInputSource_Gamepad;
        }
        if (start_windowing_with_gamepad || start_windowing_with_keyboard)
        {
            ImGuiWindow window = (g.NavWindow != null && IsWindowNavFocusable(g.NavWindow)) ? g.NavWindow : FindWindowNavFocusable(g.WindowsFocusOrder.Size - 1, -int.MaxValue, -1);
            if (window != null)
            {
                if (start_windowing_with_keyboard || g.ConfigNavWindowingWithGamepad)
                    g.NavWindowingTarget = g.NavWindowingTargetAnim = window.RootWindow; // Current location
                g.NavWindowingTimer = g.NavWindowingHighlightAlpha = 0.0f;
                g.NavWindowingAccumDeltaPos = g.NavWindowingAccumDeltaSize = new ImVec2(0.0f, 0.0f);
                g.NavWindowingInputSource = g.NavInputSource = start_windowing_with_keyboard ? ImGuiInputSource_Keyboard : ImGuiInputSource_Gamepad;
                if (g.NavWindow == null)
                    just_started_windowing_from_null_focus = true;

                // Manually register ownership of our mods. Using a global route in the Shortcut() calls instead would probably be correct but may have more side-effects.
                if (keyboard_next_window || keyboard_prev_window)
                    SetKeyOwnersForKeyChord((g.ConfigNavWindowingKeyNext | g.ConfigNavWindowingKeyPrev) & ImGuiMod_Mask_, owner_id);
            }
        }

        // Gamepad update
        if ((g.NavWindowingTarget != null || g.NavWindowingToggleLayer) && g.NavWindowingInputSource == ImGuiInputSource_Gamepad)
        {
            if (g.NavWindowingTarget != null)
            {
                // Highlight only appears after a brief time holding the button, so that a fast tap on ImGuiKey_NavGamepadMenu (to toggle NavLayer) doesn't add visual noise
                // However inputs are accepted immediately, so you press ImGuiKey_NavGamepadMenu + L1/R1 fast.
                g.NavWindowingTimer += io.DeltaTime;
                g.NavWindowingHighlightAlpha = ImMath.ImMax(g.NavWindowingHighlightAlpha, ImMath.ImSaturate((g.NavWindowingTimer - NAV_WINDOWING_HIGHLIGHT_DELAY) / 0.05f));

                // Select window to focus
                int focus_change_dir = (int)(IsKeyPressed(ImGuiKey_GamepadL1) ? 1 : 0) - (int)(IsKeyPressed(ImGuiKey_GamepadR1) ? 1 : 0);
                if (focus_change_dir != 0 && !just_started_windowing_from_null_focus)
                {
                    NavUpdateWindowingTarget(focus_change_dir);
                    g.NavWindowingHighlightAlpha = 1.0f;
                }
            }

            // Single press toggles NavLayer, long press with L/R apply actual focus on release (until then the window was merely rendered top-most)
            if (!IsKeyDown(ImGuiKey_NavGamepadMenu))
            {
                g.NavWindowingToggleLayer &= (g.NavWindowingHighlightAlpha < 1.0f); // Once button was held long enough we don't consider it a tap-to-toggle-layer press anymore.
                if (g.NavWindowingToggleLayer && g.NavWindow != null)
                    apply_toggle_layer = true;
                else if (!g.NavWindowingToggleLayer)
                    apply_focus_window = g.NavWindowingTarget;
                g.NavWindowingTarget = null;
                g.NavWindowingToggleLayer = false;
            }
        }

        // Keyboard: Focus
        if (g.NavWindowingTarget != null && g.NavWindowingInputSource == ImGuiInputSource_Keyboard)
        {
            // Visuals only appears after a brief time after pressing TAB the first time, so that a fast Ctrl+Tab doesn't add visual noise
            ImGuiKeyChord shared_mods = ((g.ConfigNavWindowingKeyNext != 0 ? g.ConfigNavWindowingKeyNext : ImGuiMod_Mask_) & (g.ConfigNavWindowingKeyPrev != 0 ? g.ConfigNavWindowingKeyPrev : ImGuiMod_Mask_)) & ImGuiMod_Mask_;
            System.Diagnostics.Debug.Assert(shared_mods != 0); // Next/Prev shortcut currently needs a shared modifier to "hold", otherwise Prev actions would keep cycling between two windows.
            g.NavWindowingTimer += io.DeltaTime;
            g.NavWindowingHighlightAlpha = ImMath.ImMax(g.NavWindowingHighlightAlpha, ImMath.ImSaturate((g.NavWindowingTimer - NAV_WINDOWING_HIGHLIGHT_DELAY) / 0.05f)); // 1.0f
            if ((keyboard_next_window || keyboard_prev_window) && !just_started_windowing_from_null_focus)
                NavUpdateWindowingTarget(keyboard_next_window ? -1 : +1);
            else if ((io.KeyMods & shared_mods) != shared_mods)
                apply_focus_window = g.NavWindowingTarget;
        }

        // Keyboard: Press and Release Alt to toggle menu layer
        ReadOnlySpan<ImGuiKey> windowing_toggle_keys = stackalloc ImGuiKey[] { ImGuiKey_LeftAlt, ImGuiKey_RightAlt };
        bool windowing_toggle_layer_start = false;
        if (g.NavWindow != null && (g.NavWindow.Flags & ImGuiWindowFlags_NoNavInputs) == 0)
            for (int tki = 0; tki < windowing_toggle_keys.Length; tki++)
            {
                ImGuiKey windowing_toggle_key = windowing_toggle_keys[tki];
                if (nav_keyboard_active && IsKeyPressed(windowing_toggle_key, 0, ImGuiKeyOwner_NoOwner))
                {
                    windowing_toggle_layer_start = true;
                    g.NavWindowingToggleLayer = true;
                    g.NavWindowingToggleKey = windowing_toggle_key;
                    g.NavWindowingInputSource = g.NavInputSource = ImGuiInputSource_Keyboard;
                    break;
                }
            }
        if (g.NavWindowingToggleLayer && g.NavWindowingInputSource == ImGuiInputSource_Keyboard)
        {
            // We cancel toggling nav layer when any text has been typed (generally while holding Alt). (See #370)
            // We cancel toggling nav layer when other modifiers are pressed. (See #4439)
            // - AltGR is Alt+Ctrl on some layout but we can't reliably detect it (not all backends/systems/layout emit it as Alt+Ctrl).
            // We cancel toggling nav layer if an owner has claimed the key.
            if (io.InputQueueCharacters.Size > 0 || io.KeyCtrl || io.KeyShift || io.KeySuper)
                g.NavWindowingToggleLayer = false;
            else if (windowing_toggle_layer_start == false && g.LastKeyboardKeyPressTime == g.Time)
                g.NavWindowingToggleLayer = false;
            else if (TestKeyOwner(g.NavWindowingToggleKey, ImGuiKeyOwner_NoOwner) == false || TestKeyOwner(ImGuiMod_Alt, ImGuiKeyOwner_NoOwner) == false)
                g.NavWindowingToggleLayer = false;

            // Apply layer toggle on Alt release
            // Important: as before version <18314 we lacked an explicit IO event for focus gain/loss, we also compare mouse validity to detect old backends clearing mouse pos on focus loss.
            if (IsKeyReleased(g.NavWindowingToggleKey) && g.NavWindowingToggleLayer)
                if (g.ActiveId == 0 || g.ActiveIdAllowOverlap)
                    if (IsMousePosValid(ref io.MousePos) == IsMousePosValid(ref io.MousePosPrev))
                        apply_toggle_layer = true;
            if (!IsKeyDown(g.NavWindowingToggleKey))
                g.NavWindowingToggleLayer = false;
        }

        // Move window
        if (g.NavWindowingTarget != null && (g.NavWindowingTarget.Flags & ImGuiWindowFlags_NoMove) == 0)
        {
            ImVec2 nav_move_dir = default;
            if (g.NavInputSource == ImGuiInputSource_Keyboard && !io.KeyShift)
                nav_move_dir = GetKeyMagnitude2d(ImGuiKey_LeftArrow, ImGuiKey_RightArrow, ImGuiKey_UpArrow, ImGuiKey_DownArrow);
            if (g.NavInputSource == ImGuiInputSource_Gamepad)
                nav_move_dir = GetKeyMagnitude2d(ImGuiKey_GamepadLStickLeft, ImGuiKey_GamepadLStickRight, ImGuiKey_GamepadLStickUp, ImGuiKey_GamepadLStickDown);
            if (nav_move_dir.x != 0.0f || nav_move_dir.y != 0.0f)
            {
                const float NAV_MOVE_SPEED = 800.0f;
                float move_step = NAV_MOVE_SPEED * io.DeltaTime * GetScale();
                g.NavWindowingAccumDeltaPos += nav_move_dir * move_step;
                g.NavHighlightItemUnderNav = true;
                ImVec2 accum_floored = ImMath.ImTrunc(g.NavWindowingAccumDeltaPos);
                if (accum_floored.x != 0.0f || accum_floored.y != 0.0f)
                {
                    ImGuiWindow moving_window = g.NavWindowingTarget.RootWindow;
                    SetWindowPos(moving_window, moving_window.Pos + accum_floored, ImGuiCond_Always);
                    g.NavWindowingAccumDeltaPos -= accum_floored;
                }
            }
        }

        // Apply final focus
        if (apply_focus_window != null)
            NavUpdateWindowingApplyFocus(apply_focus_window);

        // Apply menu/layer toggle
        if (apply_toggle_layer && g.NavWindow != null)
        {
            ClearActiveID();

            // Move to parent menu if necessary
            ImGuiWindow new_nav_window = g.NavWindow;
            while (new_nav_window.ParentWindow != null
                && (new_nav_window.DC.NavLayersActiveMask & (1 << ImGuiNavLayer_Menu)) == 0
                && (new_nav_window.Flags & ImGuiWindowFlags_ChildWindow) != 0
                && (new_nav_window.Flags & (ImGuiWindowFlags_Popup | ImGuiWindowFlags_ChildMenu)) == 0)
                new_nav_window = new_nav_window.ParentWindow;
            if (new_nav_window != g.NavWindow)
            {
                ImGuiWindow old_nav_window = g.NavWindow;
                FocusWindow(new_nav_window);
                new_nav_window.NavLastChildNavWindow = old_nav_window;
            }

            // Toggle layer
            ImGuiNavLayer new_nav_layer = (g.NavWindow.DC.NavLayersActiveMask & (1 << ImGuiNavLayer_Menu)) != 0 ? (ImGuiNavLayer)(g.NavLayer ^ 1) : ImGuiNavLayer_Main;
            if (new_nav_layer != g.NavLayer)
            {
                // Reinitialize navigation when entering menu bar with the Alt key (FIXME: could be a properly of the layer?)
                if (new_nav_layer == ImGuiNavLayer_Menu)
                    g.NavWindow.NavLastIds[new_nav_layer] = 0;
                NavRestoreLayer(new_nav_layer);
                SetNavCursorVisibleAfterMove();
            }
        }
    }

    // Window has already passed the IsWindowNavFocusable()
    public static string GetFallbackWindowNameForWindowingList(ImGuiWindow window)
    {
        if ((window.Flags & ImGuiWindowFlags_Popup) != 0)
            return LocalizeGetMsg(ImGuiLocKey_WindowingPopup);
        if ((window.Flags & ImGuiWindowFlags_MenuBar) != 0 && string.CompareOrdinal(window.Name, "##MainMenuBar") == 0)
            return LocalizeGetMsg(ImGuiLocKey_WindowingMainMenuBar);
        return LocalizeGetMsg(ImGuiLocKey_WindowingUntitled);
    }

    // Overlay displayed when using Ctrl+Tab. Called by EndFrame().
    public static unsafe void NavUpdateWindowingOverlay()
    {
        ImGuiContext g = ImGui.GImGui;
        System.Diagnostics.Debug.Assert(g.NavWindowingTarget != null);

        if (g.NavWindowingTimer < NAV_WINDOWING_LIST_APPEAR_DELAY)
            return;

        ImGuiViewport viewport = GetMainViewport();
        SetNextWindowSizeConstraints(new ImVec2(viewport.Size.x * 0.20f, viewport.Size.y * 0.20f), new ImVec2(float.MaxValue, float.MaxValue));
        SetNextWindowPos(viewport.GetCenter(), ImGuiCond_Always, new ImVec2(0.5f, 0.5f));
        PushStyleVar(ImGuiStyleVar_WindowPadding, g.Style.WindowPadding * 2.0f);
        Begin("##NavWindowingOverlay", ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoFocusOnAppearing | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoInputs | ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoSavedSettings);
        g.NavWindowingListWindow = g.CurrentWindow;
        if (g.ContextName.Length != 0)
            SeparatorText(g.ContextName);
        for (int n = g.WindowsFocusOrder.Size - 1; n >= 0; n--)
        {
            ImGuiWindow window = g.WindowsFocusOrder[n];
            System.Diagnostics.Debug.Assert(window != null); // Fix static analyzers
            if (!IsWindowNavFocusable(window))
                continue;
            string label = window.Name;
            if (FindRenderedTextEnd(label) == 0) // C++: label == FindRenderedTextEnd(label) (rendered text is empty)
                label = GetFallbackWindowNameForWindowingList(window);
            Selectable(label, g.NavWindowingTarget == window);
        }
        End();
        PopStyleVar();
    }
}
