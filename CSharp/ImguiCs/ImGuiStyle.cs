// Port of ImGuiStyle (imgui.h) + StyleColorsDark/Classic/Light (imgui_draw.cpp).
using System;

namespace ImGui;

public class ImGuiStyle
{
    // Font scaling
    public float FontSizeBase;
    public float FontScaleMain;
    public float FontScaleDpi;

    public float Alpha;
    public float DisabledAlpha;
    public ImVec2 WindowPadding;
    public float WindowRounding;
    public float WindowBorderSize;
    public float WindowBorderHoverPadding;
    public ImVec2 WindowMinSize;
    public ImVec2 WindowTitleAlign;
    public int WindowMenuButtonPosition;    // ImGuiDir
    public float ChildRounding;
    public float ChildBorderSize;
    public float PopupRounding;
    public float PopupBorderSize;
    public ImVec2 FramePadding;
    public float FrameRounding;
    public float FrameBorderSize;
    public ImVec2 ItemSpacing;
    public ImVec2 ItemInnerSpacing;
    public ImVec2 CellPadding;
    public ImVec2 TouchExtraPadding;
    public float IndentSpacing;
    public float ColumnsMinSpacing;
    public float ScrollbarSize;
    public float ScrollbarRounding;
    public float ScrollbarPadding;
    public float GrabMinSize;
    public float GrabRounding;
    public float LogSliderDeadzone;
    public float ImageRounding;
    public float ImageBorderSize;
    public float TabRounding;
    public float TabBorderSize;
    public float TabMinWidthBase;
    public float TabMinWidthShrink;
    public float TabCloseButtonMinWidthSelected;
    public float TabCloseButtonMinWidthUnselected;
    public float TabBarBorderSize;
    public float TabBarOverlineSize;
    public float TableAngledHeadersAngle;
    public ImVec2 TableAngledHeadersTextAlign;
    public int TreeLinesFlags;              // ImGuiTreeNodeFlags
    public float TreeLinesSize;
    public float TreeLinesRounding;
    public float MenuItemRounding;
    public float SelectableRounding;
    public float DragDropTargetRounding;
    public float DragDropTargetBorderSize;
    public float DragDropTargetPadding;
    public float ColorMarkerSize;
    public int ColorButtonPosition;         // ImGuiDir
    public ImVec2 ButtonTextAlign;
    public ImVec2 SelectableTextAlign;
    public float InputTextCursorSize;
    public float SeparatorSize;
    public float SeparatorTextBorderSize;
    public ImVec2 SeparatorTextAlign;
    public ImVec2 SeparatorTextPadding;
    public ImVec2 DisplayWindowPadding;
    public ImVec2 DisplaySafeAreaPadding;
    public bool DockingNodeHasCloseButton;  // Docking node has their own CloseButton() to close all docked windows.
    public float DockingSeparatorSize;      // Thickness of resizing border between docked windows
    public float MouseCursorScale;

    // Rendering & Tesselation
    public bool AntiAliasedLines;
    public bool AntiAliasedLinesUseTex;
    public bool AntiAliasedFill;
    public float CurveTessellationMaxError;
    public float CircleTessellationMaxError;

    // Colors
    public ImVec4[] Colors = new ImVec4[ImGuiCol_COUNT];

    // Behaviors
    public float HoverStationaryDelay;
    public float HoverDelayShort;
    public float HoverDelayNormal;
    public int HoverFlagsForTooltipMouse;   // ImGuiHoveredFlags
    public int HoverFlagsForTooltipNav;     // ImGuiHoveredFlags

    // [Internal]
    public float _MainScale;
    public float _NextFrameFontSizeBase;

    // [OBSOLETE]
    public float CurveTessellationTol;

    public ImGuiStyle()
    {
        FontSizeBase = 0.0f;
        FontScaleMain = 1.0f;
        FontScaleDpi = 1.0f;

        Alpha = 1.0f;
        DisabledAlpha = 0.60f;
        WindowPadding = new ImVec2(8, 8);
        WindowRounding = 0.0f;
        WindowBorderSize = 1.0f;
        WindowBorderHoverPadding = 4.0f;
        WindowMinSize = new ImVec2(32, 32);
        WindowTitleAlign = new ImVec2(0.0f, 0.5f);
        WindowMenuButtonPosition = ImGuiDir_Left;
        ChildRounding = 0.0f;
        ChildBorderSize = 1.0f;
        PopupRounding = 0.0f;
        PopupBorderSize = 1.0f;
        FramePadding = new ImVec2(4, 3);
        FrameRounding = 0.0f;
        FrameBorderSize = 0.0f;
        ItemSpacing = new ImVec2(8, 4);
        ItemInnerSpacing = new ImVec2(4, 4);
        CellPadding = new ImVec2(4, 2);
        TouchExtraPadding = new ImVec2(0, 0);
        IndentSpacing = 21.0f;
        ColumnsMinSpacing = 6.0f;
        ScrollbarSize = 14.0f;
        ScrollbarRounding = 9.0f;
        ScrollbarPadding = 2.0f;
        GrabMinSize = 12.0f;
        GrabRounding = 0.0f;
        LogSliderDeadzone = 4.0f;
        ImageRounding = 0.0f;
        ImageBorderSize = 0.0f;
        TabRounding = 5.0f;
        TabBorderSize = 0.0f;
        TabMinWidthBase = 1.0f;
        TabMinWidthShrink = 80.0f;
        TabCloseButtonMinWidthSelected = -1.0f;
        TabCloseButtonMinWidthUnselected = 0.0f;
        TabBarBorderSize = 1.0f;
        TabBarOverlineSize = 1.0f;
        TableAngledHeadersAngle = 35.0f * ((float)Math.PI / 180.0f);
        TableAngledHeadersTextAlign = new ImVec2(0.5f, 0.0f);
        TreeLinesFlags = ImGuiTreeNodeFlags_DrawLinesNone;
        TreeLinesSize = 1.0f;
        TreeLinesRounding = 0.0f;
        MenuItemRounding = 0.0f;
        SelectableRounding = 0.0f;
        DragDropTargetRounding = 0.0f;
        DragDropTargetBorderSize = 2.0f;
        DragDropTargetPadding = 3.0f;
        ColorMarkerSize = 3.0f;
        ColorButtonPosition = ImGuiDir_Right;
        ButtonTextAlign = new ImVec2(0.5f, 0.5f);
        SelectableTextAlign = new ImVec2(0.0f, 0.0f);
        InputTextCursorSize = 1.0f;
        SeparatorSize = 1.0f;
        SeparatorTextBorderSize = 3.0f;
        SeparatorTextAlign = new ImVec2(0.0f, 0.5f);
        SeparatorTextPadding = new ImVec2(20.0f, 3.0f);
        DisplayWindowPadding = new ImVec2(19, 19);
        DisplaySafeAreaPadding = new ImVec2(3, 3);
        DockingNodeHasCloseButton = true;
        DockingSeparatorSize = 2.0f;
        MouseCursorScale = 1.0f;

        AntiAliasedLines = true;
        AntiAliasedLinesUseTex = true;
        AntiAliasedFill = true;
        CurveTessellationMaxError = 1.12f;
        CircleTessellationMaxError = 0.30f;

        HoverStationaryDelay = 0.15f;
        HoverDelayShort = 0.15f;
        HoverDelayNormal = 0.40f;
        HoverFlagsForTooltipMouse = ImGuiHoveredFlags_Stationary | ImGuiHoveredFlags_DelayShort | ImGuiHoveredFlags_AllowWhenDisabled;
        HoverFlagsForTooltipNav = ImGuiHoveredFlags_NoSharedDelay | ImGuiHoveredFlags_DelayNormal | ImGuiHoveredFlags_AllowWhenDisabled;

        _MainScale = 1.0f;
        _NextFrameFontSizeBase = 0.0f;
        CurveTessellationTol = 0.0f;

        for (int i = 0; i < Colors.Length; i++)
            Colors[i] = new ImVec4();
        ImGui.StyleColorsDark(this);
    }

    // Scale all spacing/padding/thickness values. Do not scale fonts.
    public void ScaleAllSizes(float scale_factor)
    {
        _MainScale *= scale_factor;
        WindowPadding = ImMath.ImTrunc(WindowPadding * scale_factor);
        WindowRounding = ImMath.ImTrunc(WindowRounding * scale_factor);
        WindowBorderSize = ImMath.ImTrunc(WindowBorderSize * scale_factor);
        WindowMinSize = ImMath.ImTrunc(WindowMinSize * scale_factor);
        WindowBorderHoverPadding = ImMath.ImTrunc(WindowBorderHoverPadding * scale_factor);
        ChildRounding = ImMath.ImTrunc(ChildRounding * scale_factor);
        ChildBorderSize = ImMath.ImTrunc(ChildBorderSize * scale_factor);
        PopupRounding = ImMath.ImTrunc(PopupRounding * scale_factor);
        PopupBorderSize = ImMath.ImTrunc(PopupBorderSize * scale_factor);
        FramePadding = ImMath.ImTrunc(FramePadding * scale_factor);
        FrameBorderSize = ImMath.ImTrunc(FrameBorderSize * scale_factor);
        FrameRounding = ImMath.ImTrunc(FrameRounding * scale_factor);
        ItemSpacing = ImMath.ImTrunc(ItemSpacing * scale_factor);
        ItemInnerSpacing = ImMath.ImTrunc(ItemInnerSpacing * scale_factor);
        CellPadding = ImMath.ImTrunc(CellPadding * scale_factor);
        TouchExtraPadding = ImMath.ImTrunc(TouchExtraPadding * scale_factor);
        IndentSpacing = ImMath.ImTrunc(IndentSpacing * scale_factor);
        ColumnsMinSpacing = ImMath.ImTrunc(ColumnsMinSpacing * scale_factor);
        ScrollbarSize = ImMath.ImTrunc(ScrollbarSize * scale_factor);
        ScrollbarRounding = ImMath.ImTrunc(ScrollbarRounding * scale_factor);
        ScrollbarPadding = ImMath.ImTrunc(ScrollbarPadding * scale_factor);
        GrabMinSize = ImMath.ImTrunc(GrabMinSize * scale_factor);
        GrabRounding = ImMath.ImTrunc(GrabRounding * scale_factor);
        LogSliderDeadzone = ImMath.ImTrunc(LogSliderDeadzone * scale_factor);
        ImageRounding = ImMath.ImTrunc(ImageRounding * scale_factor);
        ImageBorderSize = ImMath.ImTrunc(ImageBorderSize * scale_factor);
        TabRounding = ImMath.ImTrunc(TabRounding * scale_factor);
        TabBorderSize = ImMath.ImTrunc(TabBorderSize * scale_factor);
        TabMinWidthBase = ImMath.ImTrunc(TabMinWidthBase * scale_factor);
        TabMinWidthShrink = ImMath.ImTrunc(TabMinWidthShrink * scale_factor);
        TabCloseButtonMinWidthSelected = (TabCloseButtonMinWidthSelected > 0.0f && TabCloseButtonMinWidthSelected != float.MaxValue) ? ImMath.ImTrunc(TabCloseButtonMinWidthSelected * scale_factor) : TabCloseButtonMinWidthSelected;
        TabCloseButtonMinWidthUnselected = (TabCloseButtonMinWidthUnselected > 0.0f && TabCloseButtonMinWidthUnselected != float.MaxValue) ? ImMath.ImTrunc(TabCloseButtonMinWidthUnselected * scale_factor) : TabCloseButtonMinWidthUnselected;
        TabBarBorderSize = ImMath.ImTrunc(TabBarBorderSize * scale_factor);
        TabBarOverlineSize = ImMath.ImTrunc(TabBarOverlineSize * scale_factor);
        TreeLinesSize = ImMath.ImTrunc(TreeLinesSize * scale_factor);
        TreeLinesRounding = ImMath.ImTrunc(TreeLinesRounding * scale_factor);
        MenuItemRounding = ImMath.ImTrunc(MenuItemRounding * scale_factor);
        SelectableRounding = ImMath.ImTrunc(SelectableRounding * scale_factor);
        DragDropTargetRounding = ImMath.ImTrunc(DragDropTargetRounding * scale_factor);
        DragDropTargetBorderSize = ImMath.ImTrunc(DragDropTargetBorderSize * scale_factor);
        DragDropTargetPadding = ImMath.ImTrunc(DragDropTargetPadding * scale_factor);
        ColorMarkerSize = ImMath.ImTrunc(ColorMarkerSize * scale_factor);
        InputTextCursorSize = ImMath.ImTrunc(InputTextCursorSize * scale_factor);
        SeparatorSize = ImMath.ImTrunc(SeparatorSize * scale_factor);
        SeparatorTextBorderSize = ImMath.ImTrunc(SeparatorTextBorderSize * scale_factor);
        SeparatorTextPadding = ImMath.ImTrunc(SeparatorTextPadding * scale_factor);
        DockingSeparatorSize = ImMath.ImTrunc(DockingSeparatorSize * scale_factor);
        DisplayWindowPadding = ImMath.ImTrunc(DisplayWindowPadding * scale_factor);
        DisplaySafeAreaPadding = ImMath.ImTrunc(DisplaySafeAreaPadding * scale_factor);
        MouseCursorScale = ImMath.ImTrunc(MouseCursorScale * scale_factor);
    }
}

public static partial class ImGui
{
    public static void StyleColorsDark(ImGuiStyle dst = null)
    {
        ImGuiStyle style = dst ?? GetStyle();
        ImVec4[] colors = style.Colors;

        colors[ImGuiCol_Text]                   = new ImVec4(1.00f, 1.00f, 1.00f, 1.00f);
        colors[ImGuiCol_TextDisabled]           = new ImVec4(0.50f, 0.50f, 0.50f, 1.00f);
        colors[ImGuiCol_WindowBg]               = new ImVec4(0.06f, 0.06f, 0.06f, 0.94f);
        colors[ImGuiCol_ChildBg]                = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_PopupBg]                = new ImVec4(0.08f, 0.08f, 0.08f, 0.94f);
        colors[ImGuiCol_Border]                 = new ImVec4(0.43f, 0.43f, 0.50f, 0.50f);
        colors[ImGuiCol_BorderShadow]           = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_FrameBg]                = new ImVec4(0.16f, 0.29f, 0.48f, 0.54f);
        colors[ImGuiCol_FrameBgHovered]         = new ImVec4(0.26f, 0.59f, 0.98f, 0.40f);
        colors[ImGuiCol_FrameBgActive]          = new ImVec4(0.26f, 0.59f, 0.98f, 0.67f);
        colors[ImGuiCol_TitleBg]                = new ImVec4(0.04f, 0.04f, 0.04f, 1.00f);
        colors[ImGuiCol_TitleBgActive]          = new ImVec4(0.16f, 0.29f, 0.48f, 1.00f);
        colors[ImGuiCol_TitleBgCollapsed]       = new ImVec4(0.00f, 0.00f, 0.00f, 0.51f);
        colors[ImGuiCol_MenuBarBg]              = new ImVec4(0.14f, 0.14f, 0.14f, 1.00f);
        colors[ImGuiCol_ScrollbarBg]            = new ImVec4(0.02f, 0.02f, 0.02f, 0.53f);
        colors[ImGuiCol_ScrollbarGrab]          = new ImVec4(0.31f, 0.31f, 0.31f, 1.00f);
        colors[ImGuiCol_ScrollbarGrabHovered]   = new ImVec4(0.41f, 0.41f, 0.41f, 1.00f);
        colors[ImGuiCol_ScrollbarGrabActive]    = new ImVec4(0.51f, 0.51f, 0.51f, 1.00f);
        colors[ImGuiCol_CheckMark]              = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_CheckboxSelectedBg]     = ImMath.ImLerp(colors[ImGuiCol_FrameBg], colors[ImGuiCol_FrameBgHovered], 0.65f);
        colors[ImGuiCol_SliderGrab]             = new ImVec4(0.24f, 0.52f, 0.88f, 1.00f);
        colors[ImGuiCol_SliderGrabActive]       = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_Button]                 = new ImVec4(0.26f, 0.59f, 0.98f, 0.40f);
        colors[ImGuiCol_ButtonHovered]          = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_ButtonActive]           = new ImVec4(0.06f, 0.53f, 0.98f, 1.00f);
        colors[ImGuiCol_Header]                 = new ImVec4(0.26f, 0.59f, 0.98f, 0.31f);
        colors[ImGuiCol_HeaderHovered]          = new ImVec4(0.26f, 0.59f, 0.98f, 0.80f);
        colors[ImGuiCol_HeaderActive]           = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_Separator]              = colors[ImGuiCol_Border];
        colors[ImGuiCol_SeparatorHovered]       = new ImVec4(0.10f, 0.40f, 0.75f, 0.78f);
        colors[ImGuiCol_SeparatorActive]        = new ImVec4(0.10f, 0.40f, 0.75f, 1.00f);
        colors[ImGuiCol_ResizeGrip]             = new ImVec4(0.26f, 0.59f, 0.98f, 0.20f);
        colors[ImGuiCol_ResizeGripHovered]      = new ImVec4(0.26f, 0.59f, 0.98f, 0.67f);
        colors[ImGuiCol_ResizeGripActive]       = new ImVec4(0.26f, 0.59f, 0.98f, 0.95f);
        colors[ImGuiCol_InputTextCursor]        = colors[ImGuiCol_Text];
        colors[ImGuiCol_TabHovered]             = colors[ImGuiCol_HeaderHovered];
        colors[ImGuiCol_Tab]                    = ImMath.ImLerp(colors[ImGuiCol_Header],       colors[ImGuiCol_TitleBgActive], 0.80f);
        colors[ImGuiCol_TabSelected]            = ImMath.ImLerp(colors[ImGuiCol_HeaderActive], colors[ImGuiCol_TitleBgActive], 0.60f);
        colors[ImGuiCol_TabSelectedOverline]    = colors[ImGuiCol_HeaderActive];
        colors[ImGuiCol_TabDimmed]              = ImMath.ImLerp(colors[ImGuiCol_Tab],          colors[ImGuiCol_TitleBg], 0.80f);
        colors[ImGuiCol_TabDimmedSelected]      = ImMath.ImLerp(colors[ImGuiCol_TabSelected],  colors[ImGuiCol_TitleBg], 0.40f);
        colors[ImGuiCol_TabDimmedSelectedOverline] = new ImVec4(0.50f, 0.50f, 0.50f, 0.00f);
        colors[ImGuiCol_DockingPreview]         = new ImVec4(colors[ImGuiCol_HeaderActive].x, colors[ImGuiCol_HeaderActive].y, colors[ImGuiCol_HeaderActive].z, colors[ImGuiCol_HeaderActive].w * 0.7f);
        colors[ImGuiCol_DockingEmptyBg]         = new ImVec4(0.20f, 0.20f, 0.20f, 1.00f);
        colors[ImGuiCol_PlotLines]              = new ImVec4(0.61f, 0.61f, 0.61f, 1.00f);
        colors[ImGuiCol_PlotLinesHovered]       = new ImVec4(1.00f, 0.43f, 0.35f, 1.00f);
        colors[ImGuiCol_PlotHistogram]          = new ImVec4(0.90f, 0.70f, 0.00f, 1.00f);
        colors[ImGuiCol_PlotHistogramHovered]   = new ImVec4(1.00f, 0.60f, 0.00f, 1.00f);
        colors[ImGuiCol_TableHeaderBg]          = new ImVec4(0.19f, 0.19f, 0.20f, 1.00f);
        colors[ImGuiCol_TableBorderStrong]      = new ImVec4(0.31f, 0.31f, 0.35f, 1.00f);
        colors[ImGuiCol_TableBorderLight]       = new ImVec4(0.23f, 0.23f, 0.25f, 1.00f);
        colors[ImGuiCol_TableRowBg]             = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_TableRowBgAlt]          = new ImVec4(1.00f, 1.00f, 1.00f, 0.06f);
        colors[ImGuiCol_TextLink]               = colors[ImGuiCol_HeaderActive];
        colors[ImGuiCol_TextSelectedBg]         = new ImVec4(0.26f, 0.59f, 0.98f, 0.35f);
        colors[ImGuiCol_TreeLines]              = colors[ImGuiCol_Border];
        colors[ImGuiCol_DragDropTarget]         = new ImVec4(1.00f, 1.00f, 0.00f, 0.90f);
        colors[ImGuiCol_DragDropTargetBg]       = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_UnsavedMarker]          = new ImVec4(1.00f, 1.00f, 1.00f, 1.00f);
        colors[ImGuiCol_NavCursor]              = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_NavWindowingHighlight]  = new ImVec4(1.00f, 1.00f, 1.00f, 0.70f);
        colors[ImGuiCol_NavWindowingDimBg]      = new ImVec4(0.80f, 0.80f, 0.80f, 0.20f);
        colors[ImGuiCol_ModalWindowDimBg]       = new ImVec4(0.80f, 0.80f, 0.80f, 0.35f);
    }

    public static void StyleColorsLight(ImGuiStyle dst = null)
    {
        ImGuiStyle style = dst ?? GetStyle();
        ImVec4[] colors = style.Colors;

        colors[ImGuiCol_Text]                   = new ImVec4(0.00f, 0.00f, 0.00f, 1.00f);
        colors[ImGuiCol_TextDisabled]           = new ImVec4(0.60f, 0.60f, 0.60f, 1.00f);
        colors[ImGuiCol_WindowBg]               = new ImVec4(0.94f, 0.94f, 0.94f, 1.00f);
        colors[ImGuiCol_ChildBg]                = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_PopupBg]                = new ImVec4(1.00f, 1.00f, 1.00f, 0.98f);
        colors[ImGuiCol_Border]                 = new ImVec4(0.00f, 0.00f, 0.00f, 0.30f);
        colors[ImGuiCol_BorderShadow]           = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_FrameBg]                = new ImVec4(1.00f, 1.00f, 1.00f, 1.00f);
        colors[ImGuiCol_FrameBgHovered]         = new ImVec4(0.26f, 0.59f, 0.98f, 0.40f);
        colors[ImGuiCol_FrameBgActive]          = new ImVec4(0.26f, 0.59f, 0.98f, 0.67f);
        colors[ImGuiCol_TitleBg]                = new ImVec4(0.96f, 0.96f, 0.96f, 1.00f);
        colors[ImGuiCol_TitleBgActive]          = new ImVec4(0.82f, 0.82f, 0.82f, 1.00f);
        colors[ImGuiCol_TitleBgCollapsed]       = new ImVec4(1.00f, 1.00f, 1.00f, 0.51f);
        colors[ImGuiCol_MenuBarBg]              = new ImVec4(0.86f, 0.86f, 0.86f, 1.00f);
        colors[ImGuiCol_ScrollbarBg]            = new ImVec4(0.98f, 0.98f, 0.98f, 0.53f);
        colors[ImGuiCol_ScrollbarGrab]          = new ImVec4(0.69f, 0.69f, 0.69f, 0.80f);
        colors[ImGuiCol_ScrollbarGrabHovered]   = new ImVec4(0.49f, 0.49f, 0.49f, 0.80f);
        colors[ImGuiCol_ScrollbarGrabActive]    = new ImVec4(0.49f, 0.49f, 0.49f, 1.00f);
        colors[ImGuiCol_CheckMark]              = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_CheckboxSelectedBg]     = ImMath.ImLerp(colors[ImGuiCol_FrameBg], colors[ImGuiCol_FrameBgHovered], 0.65f);
        colors[ImGuiCol_SliderGrab]             = new ImVec4(0.26f, 0.59f, 0.98f, 0.78f);
        colors[ImGuiCol_SliderGrabActive]       = new ImVec4(0.46f, 0.54f, 0.80f, 0.60f);
        colors[ImGuiCol_Button]                 = new ImVec4(0.26f, 0.59f, 0.98f, 0.40f);
        colors[ImGuiCol_ButtonHovered]          = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_ButtonActive]           = new ImVec4(0.06f, 0.53f, 0.98f, 1.00f);
        colors[ImGuiCol_Header]                 = new ImVec4(0.26f, 0.59f, 0.98f, 0.31f);
        colors[ImGuiCol_HeaderHovered]          = new ImVec4(0.26f, 0.59f, 0.98f, 0.80f);
        colors[ImGuiCol_HeaderActive]           = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_Separator]              = new ImVec4(0.39f, 0.39f, 0.39f, 0.62f);
        colors[ImGuiCol_SeparatorHovered]       = new ImVec4(0.14f, 0.44f, 0.80f, 0.78f);
        colors[ImGuiCol_SeparatorActive]        = new ImVec4(0.14f, 0.44f, 0.80f, 1.00f);
        colors[ImGuiCol_ResizeGrip]             = new ImVec4(0.35f, 0.35f, 0.35f, 0.17f);
        colors[ImGuiCol_ResizeGripHovered]      = new ImVec4(0.26f, 0.59f, 0.98f, 0.67f);
        colors[ImGuiCol_ResizeGripActive]       = new ImVec4(0.26f, 0.59f, 0.98f, 0.95f);
        colors[ImGuiCol_InputTextCursor]        = colors[ImGuiCol_Text];
        colors[ImGuiCol_TabHovered]             = colors[ImGuiCol_HeaderHovered];
        colors[ImGuiCol_Tab]                    = ImMath.ImLerp(colors[ImGuiCol_Header],       colors[ImGuiCol_TitleBgActive], 0.90f);
        colors[ImGuiCol_TabSelected]            = ImMath.ImLerp(colors[ImGuiCol_HeaderActive], colors[ImGuiCol_TitleBgActive], 0.60f);
        colors[ImGuiCol_TabSelectedOverline]    = colors[ImGuiCol_HeaderActive];
        colors[ImGuiCol_TabDimmed]              = ImMath.ImLerp(colors[ImGuiCol_Tab],          colors[ImGuiCol_TitleBg], 0.80f);
        colors[ImGuiCol_TabDimmedSelected]      = ImMath.ImLerp(colors[ImGuiCol_TabSelected],  colors[ImGuiCol_TitleBg], 0.40f);
        colors[ImGuiCol_TabDimmedSelectedOverline] = new ImVec4(0.50f, 0.50f, 0.50f, 0.00f);
        colors[ImGuiCol_DockingPreview]         = new ImVec4(colors[ImGuiCol_Header].x, colors[ImGuiCol_Header].y, colors[ImGuiCol_Header].z, colors[ImGuiCol_Header].w * 0.7f);
        colors[ImGuiCol_DockingEmptyBg]         = new ImVec4(0.20f, 0.20f, 0.20f, 1.00f);
        colors[ImGuiCol_PlotLines]              = new ImVec4(0.39f, 0.39f, 0.39f, 1.00f);
        colors[ImGuiCol_PlotLinesHovered]       = new ImVec4(1.00f, 0.43f, 0.35f, 1.00f);
        colors[ImGuiCol_PlotHistogram]          = new ImVec4(0.90f, 0.70f, 0.00f, 1.00f);
        colors[ImGuiCol_PlotHistogramHovered]   = new ImVec4(1.00f, 0.45f, 0.00f, 1.00f);
        colors[ImGuiCol_TableHeaderBg]          = new ImVec4(0.78f, 0.78f, 0.78f, 1.00f);
        colors[ImGuiCol_TableBorderStrong]      = new ImVec4(0.57f, 0.57f, 0.64f, 1.00f);
        colors[ImGuiCol_TableBorderLight]       = new ImVec4(0.68f, 0.68f, 0.74f, 1.00f);
        colors[ImGuiCol_TableRowBg]             = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_TableRowBgAlt]          = new ImVec4(0.98f, 0.98f, 0.98f, 1.00f);
        colors[ImGuiCol_TextLink]               = colors[ImGuiCol_HeaderActive];
        colors[ImGuiCol_TextSelectedBg]         = new ImVec4(0.26f, 0.59f, 0.98f, 0.35f);
        colors[ImGuiCol_TreeLines]              = colors[ImGuiCol_Border];
        colors[ImGuiCol_DragDropTarget]         = new ImVec4(0.26f, 0.59f, 0.98f, 0.95f);
        colors[ImGuiCol_DragDropTargetBg]       = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_UnsavedMarker]          = new ImVec4(0.00f, 0.00f, 0.00f, 1.00f);
        colors[ImGuiCol_NavCursor]              = new ImVec4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[ImGuiCol_NavWindowingHighlight]  = new ImVec4(0.70f, 0.70f, 0.70f, 0.70f);
        colors[ImGuiCol_NavWindowingDimBg]      = new ImVec4(0.20f, 0.20f, 0.20f, 0.20f);
        colors[ImGuiCol_ModalWindowDimBg]       = new ImVec4(0.20f, 0.20f, 0.20f, 0.35f);
    }

    public static void StyleColorsClassic(ImGuiStyle dst = null)
    {
        ImGuiStyle style = dst ?? GetStyle();
        ImVec4[] colors = style.Colors;

        colors[ImGuiCol_Text]                   = new ImVec4(0.90f, 0.90f, 0.90f, 1.00f);
        colors[ImGuiCol_TextDisabled]           = new ImVec4(0.60f, 0.60f, 0.60f, 1.00f);
        colors[ImGuiCol_WindowBg]               = new ImVec4(0.00f, 0.00f, 0.00f, 0.85f);
        colors[ImGuiCol_ChildBg]                = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_PopupBg]                = new ImVec4(0.11f, 0.11f, 0.14f, 0.92f);
        colors[ImGuiCol_Border]                 = new ImVec4(0.50f, 0.50f, 0.50f, 0.50f);
        colors[ImGuiCol_BorderShadow]           = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_FrameBg]                = new ImVec4(0.43f, 0.43f, 0.43f, 0.39f);
        colors[ImGuiCol_FrameBgHovered]         = new ImVec4(0.47f, 0.47f, 0.69f, 0.40f);
        colors[ImGuiCol_FrameBgActive]          = new ImVec4(0.42f, 0.41f, 0.64f, 0.69f);
        colors[ImGuiCol_TitleBg]                = new ImVec4(0.27f, 0.27f, 0.54f, 0.83f);
        colors[ImGuiCol_TitleBgActive]          = new ImVec4(0.32f, 0.32f, 0.63f, 0.87f);
        colors[ImGuiCol_TitleBgCollapsed]       = new ImVec4(0.40f, 0.40f, 0.80f, 0.20f);
        colors[ImGuiCol_MenuBarBg]              = new ImVec4(0.40f, 0.40f, 0.55f, 0.80f);
        colors[ImGuiCol_ScrollbarBg]            = new ImVec4(0.20f, 0.25f, 0.30f, 0.60f);
        colors[ImGuiCol_ScrollbarGrab]          = new ImVec4(0.40f, 0.40f, 0.80f, 0.30f);
        colors[ImGuiCol_ScrollbarGrabHovered]   = new ImVec4(0.40f, 0.40f, 0.80f, 0.40f);
        colors[ImGuiCol_ScrollbarGrabActive]    = new ImVec4(0.41f, 0.39f, 0.80f, 0.60f);
        colors[ImGuiCol_CheckMark]              = new ImVec4(0.90f, 0.90f, 0.90f, 0.50f);
        colors[ImGuiCol_CheckboxSelectedBg]     = ImMath.ImLerp(colors[ImGuiCol_FrameBg], colors[ImGuiCol_FrameBgActive], 0.65f);
        colors[ImGuiCol_SliderGrab]             = new ImVec4(1.00f, 1.00f, 1.00f, 0.30f);
        colors[ImGuiCol_SliderGrabActive]       = new ImVec4(0.41f, 0.39f, 0.80f, 0.60f);
        colors[ImGuiCol_Button]                 = new ImVec4(0.35f, 0.40f, 0.61f, 0.62f);
        colors[ImGuiCol_ButtonHovered]          = new ImVec4(0.40f, 0.48f, 0.71f, 0.79f);
        colors[ImGuiCol_ButtonActive]           = new ImVec4(0.46f, 0.54f, 0.80f, 1.00f);
        colors[ImGuiCol_Header]                 = new ImVec4(0.40f, 0.40f, 0.90f, 0.45f);
        colors[ImGuiCol_HeaderHovered]          = new ImVec4(0.45f, 0.45f, 0.90f, 0.80f);
        colors[ImGuiCol_HeaderActive]           = new ImVec4(0.53f, 0.53f, 0.87f, 0.80f);
        colors[ImGuiCol_Separator]              = new ImVec4(0.50f, 0.50f, 0.50f, 0.60f);
        colors[ImGuiCol_SeparatorHovered]       = new ImVec4(0.60f, 0.60f, 0.70f, 1.00f);
        colors[ImGuiCol_SeparatorActive]        = new ImVec4(0.70f, 0.70f, 0.90f, 1.00f);
        colors[ImGuiCol_ResizeGrip]             = new ImVec4(1.00f, 1.00f, 1.00f, 0.10f);
        colors[ImGuiCol_ResizeGripHovered]      = new ImVec4(0.78f, 0.82f, 1.00f, 0.60f);
        colors[ImGuiCol_ResizeGripActive]       = new ImVec4(0.78f, 0.82f, 1.00f, 0.90f);
        colors[ImGuiCol_InputTextCursor]        = colors[ImGuiCol_Text];
        colors[ImGuiCol_TabHovered]             = colors[ImGuiCol_HeaderHovered];
        colors[ImGuiCol_Tab]                    = ImMath.ImLerp(colors[ImGuiCol_Header],       colors[ImGuiCol_TitleBgActive], 0.80f);
        colors[ImGuiCol_TabSelected]            = ImMath.ImLerp(colors[ImGuiCol_HeaderActive], colors[ImGuiCol_TitleBgActive], 0.60f);
        colors[ImGuiCol_TabSelectedOverline]    = colors[ImGuiCol_HeaderActive];
        colors[ImGuiCol_TabDimmed]              = ImMath.ImLerp(colors[ImGuiCol_Tab],          colors[ImGuiCol_TitleBg], 0.80f);
        colors[ImGuiCol_TabDimmedSelected]      = ImMath.ImLerp(colors[ImGuiCol_TabSelected],  colors[ImGuiCol_TitleBg], 0.40f);
        colors[ImGuiCol_TabDimmedSelectedOverline] = new ImVec4(0.53f, 0.53f, 0.87f, 0.00f);
        colors[ImGuiCol_DockingPreview]         = new ImVec4(colors[ImGuiCol_Header].x, colors[ImGuiCol_Header].y, colors[ImGuiCol_Header].z, colors[ImGuiCol_Header].w * 0.7f);
        colors[ImGuiCol_DockingEmptyBg]         = new ImVec4(0.20f, 0.20f, 0.20f, 1.00f);
        colors[ImGuiCol_PlotLines]              = new ImVec4(1.00f, 1.00f, 1.00f, 1.00f);
        colors[ImGuiCol_PlotLinesHovered]       = new ImVec4(0.90f, 0.70f, 0.00f, 1.00f);
        colors[ImGuiCol_PlotHistogram]          = new ImVec4(0.90f, 0.70f, 0.00f, 1.00f);
        colors[ImGuiCol_PlotHistogramHovered]   = new ImVec4(1.00f, 0.60f, 0.00f, 1.00f);
        colors[ImGuiCol_TableHeaderBg]          = new ImVec4(0.27f, 0.27f, 0.38f, 1.00f);
        colors[ImGuiCol_TableBorderStrong]      = new ImVec4(0.31f, 0.31f, 0.45f, 1.00f);   // Prefer using Alpha=1.0 here
        colors[ImGuiCol_TableBorderLight]       = new ImVec4(0.26f, 0.26f, 0.28f, 1.00f);   // Prefer using Alpha=1.0 here
        colors[ImGuiCol_TableRowBg]             = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_TableRowBgAlt]          = new ImVec4(1.00f, 1.00f, 1.00f, 0.07f);
        colors[ImGuiCol_TextLink]               = colors[ImGuiCol_HeaderActive];
        colors[ImGuiCol_TextSelectedBg]         = new ImVec4(0.00f, 0.00f, 1.00f, 0.35f);
        colors[ImGuiCol_TreeLines]              = colors[ImGuiCol_Border];
        colors[ImGuiCol_DragDropTarget]         = new ImVec4(1.00f, 1.00f, 0.00f, 0.90f);
        colors[ImGuiCol_DragDropTargetBg]       = new ImVec4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[ImGuiCol_UnsavedMarker]          = new ImVec4(0.90f, 0.90f, 0.90f, 1.00f);
        colors[ImGuiCol_NavCursor]              = colors[ImGuiCol_HeaderHovered];
        colors[ImGuiCol_NavWindowingHighlight]  = new ImVec4(1.00f, 1.00f, 1.00f, 0.70f);
        colors[ImGuiCol_NavWindowingDimBg]      = new ImVec4(0.80f, 0.80f, 0.80f, 0.20f);
        colors[ImGuiCol_ModalWindowDimBg]       = new ImVec4(0.20f, 0.20f, 0.20f, 0.35f);
    }
}
