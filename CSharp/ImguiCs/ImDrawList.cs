// Port of Dear ImGui drawing API (imgui_draw.cpp [SECTION] ImDrawList, ImDrawListSplitter, ImDrawData, ShadeVerts).
using System;

namespace ImGui;

// ImDrawIdx: 16-bit indices by default
using ImDrawIdx = System.UInt16;

// ImDrawCallback: C# delegate (or any object marker for ResetRenderState)
public delegate void ImDrawCallback(ImDrawList parent_list, ImDrawCmd draw_cmd);

[StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct ImDrawVert
{
    public ImVec2 pos, uv;
    public ImU32 col;
}

// ImDrawCmd: a single draw command within a parent ImDrawList
public struct ImDrawCmd
{
    public ImVec4 ClipRect;
    public ImTextureRef TexRef;
    public uint VtxOffset;
    public uint IdxOffset;
    public uint ElemCount;
    public ImDrawCallback UserCallback;
    public byte[] UserCallbackDataBuf;      // resolved buffer
    public int UserCallbackDataOffset;
    public int UserCallbackDataSize;

    public ImTextureID GetTexID() => (_TexData != null) ? _TexData.TexID : TexRef._TexID;

    // accessor to keep C++-like naming: TexRef is the struct above; _TexData is resolved here
    public ImTextureData _TexData => TexRef._TexData;

    public ImDrawCmd()
    {
        ClipRect = default;
        TexRef = new ImTextureRef();
        VtxOffset = 0; IdxOffset = 0; ElemCount = 0;
        UserCallback = null;
        UserCallbackDataBuf = null;
        UserCallbackDataOffset = -1;
        UserCallbackDataSize = 0;
    }
}

// ImDrawCmdHeader: header fields copied between commands (ClipRect, TexRef, VtxOffset)
public struct ImDrawCmdHeader
{
    public ImVec4 ClipRect;
    public ImTextureRef TexRef;
    public uint VtxOffset;
}

// ImDrawChannel: temporary storage to output draw commands out of order
public class ImDrawChannel
{
    public ImVector<ImDrawCmd> _CmdBuffer = new ImVector<ImDrawCmd>();
    public ImVector<ImDrawIdx> _IdxBuffer = new ImVector<ImDrawIdx>();
}

public class ImDrawListSplitter
{
    public int _Current;
    public int _Count;
    public ImVector<ImDrawChannel> _Channels = new ImVector<ImDrawChannel>();

    public void Clear() { _Current = 0; _Count = 1; } // NB: _Channels may still be old sizes, matching C++ semantics
    public void ClearFreeMemory()
    {
        for (int i = 0; i < _Channels.Size; i++)
        {
            _Channels[i]._CmdBuffer.clear();
            _Channels[i]._IdxBuffer.clear();
        }
        _Current = 0;
        _Count = 1;
        _Channels.clear();
    }

    public void Split(ImDrawList draw_list, int channels_count)
    {
        System.Diagnostics.Debug.Assert(_Current == 0 && _Count <= 1, "Nested channel splitting is not supported.");
        int old_channels_count = _Channels.Size;
        if (old_channels_count < channels_count)
        {
            _Channels.reserve(channels_count);
            _Channels.resize(channels_count);
            // C++ ImVector::resize() value-initializes ImDrawChannel elements; without this, channel 0 stays null.
            for (int i = old_channels_count; i < channels_count; i++)
                _Channels.Data[i] = new ImDrawChannel();
        }
        _Count = channels_count;
        for (int i = 1; i < channels_count; i++)
        {
            if (i >= old_channels_count)
                _Channels[i] = new ImDrawChannel();
            else
            {
                _Channels[i]._CmdBuffer.resize(0);
                _Channels[i]._IdxBuffer.resize(0);
            }
        }
    }

    public void Merge(ImDrawList draw_list)
    {
        if (_Count <= 1)
            return;
        SetCurrentChannel(draw_list, 0);
        draw_list._PopUnusedDrawCmd();

        int new_cmd_buffer_count = 0;
        int new_idx_buffer_count = 0;
        bool has_last_cmd = (_Count > 0 && draw_list.CmdBuffer.Size > 0);
        ImDrawCmd last_cmd = has_last_cmd ? draw_list.CmdBuffer.back() : default;
        ImDrawChannel last_cmd_owner = null; // null => last_cmd lives in draw_list.CmdBuffer (C++ keeps a pointer into either buffer)
        uint idx_offset = has_last_cmd ? last_cmd.IdxOffset + last_cmd.ElemCount : 0;
        for (int i = 1; i < _Count; i++)
        {
            ImDrawChannel ch = _Channels[i];
            if (ch._CmdBuffer.Size > 0 && ch._CmdBuffer.back().ElemCount == 0 && ch._CmdBuffer.back().UserCallback == null)
                ch._CmdBuffer.pop_back();

            if (ch._CmdBuffer.Size > 0 && has_last_cmd)
            {
                ImDrawCmd next_cmd = ch._CmdBuffer[0];
                if (ImDrawListUtil.ImDrawCmd_HeaderCompare(last_cmd, next_cmd) == 0 && last_cmd.UserCallback == null && next_cmd.UserCallback == null)
                {
                    last_cmd.ElemCount += next_cmd.ElemCount;
                    idx_offset += next_cmd.ElemCount;
                    if (last_cmd_owner != null)
                        last_cmd_owner._CmdBuffer.Data[last_cmd_owner._CmdBuffer.Size - 1] = last_cmd;
                    else if (draw_list.CmdBuffer.Size > 0)
                        draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1] = last_cmd;
                    ch._CmdBuffer.erase(0);
                }
            }
            if (ch._CmdBuffer.Size > 0)
            {
                last_cmd = ch._CmdBuffer.back();
                last_cmd_owner = ch;
                has_last_cmd = true;
            }
            new_cmd_buffer_count += ch._CmdBuffer.Size;
            new_idx_buffer_count += ch._IdxBuffer.Size;
            for (int cmd_n = 0; cmd_n < ch._CmdBuffer.Size; cmd_n++)
            {
                var cmd = ch._CmdBuffer[cmd_n];
                cmd.IdxOffset = idx_offset;
                idx_offset += cmd.ElemCount;
                ch._CmdBuffer[cmd_n] = cmd;
            }
            // C++ keeps `last_cmd` as a pointer, so it observes the IdxOffset rewrite above; refresh the copy.
            if (last_cmd_owner == ch && ch._CmdBuffer.Size > 0)
                last_cmd = ch._CmdBuffer.back();
        }
        draw_list.CmdBuffer.resize(draw_list.CmdBuffer.Size + new_cmd_buffer_count);
        draw_list.IdxBuffer.resize(draw_list.IdxBuffer.Size + new_idx_buffer_count);

        int cmd_write = draw_list.CmdBuffer.Size - new_cmd_buffer_count;
        int idx_write = draw_list.IdxBuffer.Size - new_idx_buffer_count;
        for (int i = 1; i < _Count; i++)
        {
            ImDrawChannel ch = _Channels[i];
            for (int c = 0; c < ch._CmdBuffer.Size; c++) draw_list.CmdBuffer[cmd_write++] = ch._CmdBuffer[c];
            for (int c = 0; c < ch._IdxBuffer.Size; c++) draw_list.IdxBuffer[idx_write + c] = ch._IdxBuffer[c];
            idx_write += ch._IdxBuffer.Size;
        }
        draw_list._IdxWritePos = idx_write;

        if (draw_list.CmdBuffer.Size == 0 || draw_list.CmdBuffer.back().UserCallback != null)
            draw_list.AddDrawCmd();

        if (draw_list.CmdBuffer.back().ElemCount == 0)
            draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1] = ImDrawListUtil.ImDrawCmd_HeaderCopy(draw_list.CmdBuffer.back(), ImDrawListUtil.HeaderOf(draw_list._CmdHeader));
        else if (ImDrawListUtil.ImDrawCmd_HeaderCompare(draw_list.CmdBuffer.back(), ImDrawListUtil.HeaderOf(draw_list._CmdHeader)) != 0)
            draw_list.AddDrawCmd();

        _Count = 1;
    }

    public void SetCurrentChannel(ImDrawList draw_list, int idx)
    {
        System.Diagnostics.Debug.Assert(idx >= 0 && idx < _Count);
        if (_Current == idx)
            return;

        // Overwrite channel storage with current draw list buffers
        if (_Channels.Size > 0)
        {
            _Channels.Data[_Current]._CmdBuffer = draw_list.CmdBuffer;
            _Channels.Data[_Current]._IdxBuffer = draw_list.IdxBuffer;
        }
        _Current = idx;
        draw_list.CmdBuffer = _Channels.Data[idx]._CmdBuffer;
        draw_list.IdxBuffer = _Channels.Data[idx]._IdxBuffer;
        draw_list._IdxWritePos = draw_list.IdxBuffer.Size;

        if (draw_list.CmdBuffer.Size == 0)
            draw_list.AddDrawCmd();
        else if (draw_list.CmdBuffer.back().ElemCount == 0)
            draw_list.CmdBuffer.Data[draw_list.CmdBuffer.Size - 1] = ImDrawListUtil.ImDrawCmd_HeaderCopy(draw_list.CmdBuffer.back(), ImDrawListUtil.HeaderOf(draw_list._CmdHeader));
        else if (ImDrawListUtil.ImDrawCmd_HeaderCompare(draw_list.CmdBuffer.back(), ImDrawListUtil.HeaderOf(draw_list._CmdHeader)) != 0)
            draw_list.AddDrawCmd();
    }
}

// Helper comparison/copy functions matching the C macros in imgui_draw.cpp
public static class ImDrawListUtil
{
    public static ImDrawCmd HeaderOf(ImDrawCmdHeader h) => new ImDrawCmd { ClipRect = h.ClipRect, TexRef = h.TexRef, VtxOffset = h.VtxOffset };

    // Compare ClipRect, TexRef and VtxOffset
    public static int ImDrawCmd_HeaderCompare(in ImDrawCmd lhs, in ImDrawCmd rhs)
    {
        if (lhs.ClipRect != rhs.ClipRect) return 1;
        if (!lhs.TexRef.Equals(rhs.TexRef)) return 1;
        if (lhs.VtxOffset != rhs.VtxOffset) return 1;
        return 0;
    }
    public static ImDrawCmd ImDrawCmd_HeaderCopy(in ImDrawCmd dst, in ImDrawCmd src)
    {
        var d = dst;
        d.ClipRect = src.ClipRect;
        d.TexRef = src.TexRef;
        d.VtxOffset = src.VtxOffset;
        return d;
    }
    public static bool ImDrawCmd_AreSequentialIdxOffset(in ImDrawCmd cmd0, in ImDrawCmd cmd1) => cmd0.IdxOffset + cmd0.ElemCount == cmd1.IdxOffset;
}

public class ImDrawListSharedData
{
    public ImVec2 TexUvWhitePixel;                              // UV of white pixel (used by most primitives)
    public ImVec4[] TexUvLines = new ImVec4[ImDrawListConst.IM_DRAWLIST_TEX_LINES_WIDTH_MAX + 1]; // UV of anti-aliased lines (thick lines)
    public ImFont Font;                                          // Current font (used for simplified AddText overload)
    public float FontSize;                                       // Current font size
    public float FontScale;                                      // Current font scale
    public float CurveTessellationMaxError;                      // Tessellation tolerance when using PathBezierCurveTo()
    public float CircleTessellationMaxError;                     // Circle segment max error
    public ImDrawListFlags InitialFlags;                         // Initial flags at beginning of frame
    public ImVec4 ClipRectFullscreen;                            // Value for PushClipRectFullScreen()
    public ImVector<ImVec2> TempBuffer = new ImVector<ImVec2>(); // Temporary write buffer
    public ImVector<ImDrawList> DrawLists = new ImVector<ImDrawList>(); // All draw lists associated to this data
    public ImGuiContext Context;                                 // [OPTIONAL]
    public ImFontAtlas FontAtlas;                                // [OPTIONAL] Font atlas this shared data is attached to

    // Lookup tables
    public ImVec2[] ArcFastVtx = new ImVec2[ImDrawListConst.IM_DRAWLIST_ARCFAST_TABLE_SIZE];
    public float ArcFastRadiusCutoff;
    public byte[] CircleSegmentCounts = new byte[64];
    public float InitialFringeScale = 1.0f;

    public ImDrawListSharedData()
    {
        CurveTessellationMaxError = 0.10f;   // matches ImGuiStyle defaults setup (SetCurveTessellationMaxError called by context)
        CircleTessellationMaxError = 0.10f;
        for (int i = 0; i < ArcFastVtx.Length; i++)
        {
            float a = ((float)i * 2 * ImMath.IM_PI) / (float)ArcFastVtx.Length;
            ArcFastVtx[i] = new ImVec2(ImMath.ImCos(a), ImMath.ImSin(a));
        }
    }

    public void SetCircleTessellationMaxError(float max_error)
    {
        if (CircleTessellationMaxError == max_error)
            return;
        System.Diagnostics.Debug.Assert(max_error > 0.0f);
        CircleTessellationMaxError = max_error;
        for (int i = 0; i < CircleSegmentCounts.Length; i++)
        {
            float radius = (float)i;
            CircleSegmentCounts[i] = (byte)((i > 0) ? ImDrawListConst.IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_CALC(radius, CircleTessellationMaxError) : ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX);
        }
        ArcFastRadiusCutoff = ImDrawListConst.IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_CALC_R(ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX, CircleTessellationMaxError);
    }
}

// Compile-time-like constants
public static class ImDrawListConst
{
    public const int IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_MIN = 4;
    public const int IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_MAX = 512;
    public const int IM_DRAWLIST_ARCFAST_TABLE_SIZE = 48;
    public const int IM_DRAWLIST_ARCFAST_SAMPLE_MAX = IM_DRAWLIST_ARCFAST_TABLE_SIZE;
    public const int IM_DRAWLIST_TEX_LINES_WIDTH_MAX = 32;

    public static int IM_ROUNDUP_TO_EVEN(int v) => ((((v) + 1) / 2) * 2);
    public static int IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_CALC(float rad, float maxerror)
    {
        int v = (int)ImMath.ImCeil(ImMath.IM_PI / ImMath.ImAcos(1 - ImMath.ImMin(maxerror, rad) / rad));
        v = IM_ROUNDUP_TO_EVEN(v);
        return ImMath.ImClamp(v, IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_MIN, IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_MAX);
    }
    public static float IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_CALC_R(int n, float maxerror) => maxerror / (1 - ImMath.ImCos(ImMath.IM_PI / ImMath.ImMax((float)n, ImMath.IM_PI)));
}

// ImDrawData: all draw command lists required to render the frame
public class ImDrawData
{
    public bool Valid;                              // Only valid after Render() is called and before the next NewFrame()
    public int CmdListsCount;                       // [OBSOLETE] Number of command lists
    public int TotalIdxCount;                       // For convenience, sum of all command lists IdxBuffer.Size
    public int TotalVtxCount;                       // For convenience, sum of all command lists VtxBuffer.Size
    public ImVector<ImDrawList> CmdLists = new ImVector<ImDrawList>(); // Array of ImDrawList* to render
    public ImVec2 DisplayPos;                       // Top-left position of the viewport to render (== top-left of the orthogonal projection matrix to use)
    public ImVec2 DisplaySize;                      // Size of the viewport to render (== io.DisplaySize for the main viewport)
    public ImVec2 FramebufferScale;                 // Amount of pixels for each unit of DisplaySize
    public ImGuiViewport OwnerViewport;             // Viewport carrying the ImDrawData instance, might be used by the renderer backend.
    public ImVector<ImTextureData> Textures;        // List of textures to update
    public int FrameCount;

    public void Clear()
    {
        Valid = false;
        FrameCount = TotalIdxCount = TotalVtxCount = 0;
        CmdLists.resize(0);
        DisplayPos = DisplaySize = FramebufferScale = new ImVec2(0.0f, 0.0f);
        OwnerViewport = null;
        Textures = null;
        CmdListsCount = 0;
    }

    public void AddDrawList(ImDrawList draw_list)
    {
        draw_list._PopUnusedDrawCmd();
        ImGuiCore.AddDrawListToDrawDataEx(this, CmdLists, draw_list);
    }

    public void DeIndexAllBuffers()
    {
        var new_vtx_buffer = new ImVector<ImDrawVert>();
        TotalVtxCount = TotalIdxCount = 0;
        for (int li = 0; li < CmdLists.Size; li++)
        {
            var draw_list = CmdLists[li];
            if (draw_list.IdxBuffer.empty())
                continue;
            new_vtx_buffer.resize(draw_list.IdxBuffer.Size);
            for (int j = 0; j < draw_list.IdxBuffer.Size; j++)
                new_vtx_buffer[j] = draw_list.VtxBuffer[draw_list.IdxBuffer[j]];
            draw_list.VtxBuffer = new ImVector<ImDrawVert>();
            var tmp = new ImVector<ImDrawVert>();
            tmp.resize(new_vtx_buffer.Size);
            for (int j = 0; j < new_vtx_buffer.Size; j++) tmp[j] = new_vtx_buffer[j];
            draw_list.VtxBuffer = tmp;
            draw_list.IdxBuffer.resize(0);
            TotalVtxCount += draw_list.VtxBuffer.Size;
        }
    }

    public void ScaleClipRects(ImVec2 fb_scale)
    {
        for (int li = 0; li < CmdLists.Size; li++)
        {
            var draw_list = CmdLists[li];
            for (int ci = 0; ci < draw_list.CmdBuffer.Size; ci++)
            {
                var cmd = draw_list.CmdBuffer[ci];
                cmd.ClipRect = new ImVec4(cmd.ClipRect.x * fb_scale.x, cmd.ClipRect.y * fb_scale.y, cmd.ClipRect.z * fb_scale.x, cmd.ClipRect.w * fb_scale.y);
                draw_list.CmdBuffer[ci] = cmd;
            }
        }
    }
}

// The draw list: a single dynamic "mesh" builder
public class ImDrawList
{
    // Buffers
    public ImVector<ImDrawCmd> CmdBuffer = new ImVector<ImDrawCmd>();   // Draw commands (typically 1 command = 1 GPU draw call)
    public ImVector<ImDrawIdx> IdxBuffer = new ImVector<ImDrawIdx>();   // Index buffer
    public ImVector<ImDrawVert> VtxBuffer = new ImVector<ImDrawVert>(); // Vertex buffer
    public ImDrawListFlags Flags;                                       // Flags, you may set your own

    // [Internal, used while building lists]
    public ImDrawListSharedData _Data;          // Pointer to shared draw data
    public ImDrawCmdHeader _CmdHeader;          // Header of the last command
    public string _OwnerName;                   // Owner (for debugging)
    public uint _VtxCurrentIdx;                 // [Internal] == VtxBuffer.Size or VtxOffset
    public int _VtxWritePos;                    // [Internal] write offset into VtxBuffer (C++: _VtxWritePtr)
    public int _IdxWritePos;                    // [Internal] write offset into IdxBuffer (C++: _IdxWritePtr)
    public ImVector<ImVec4> _ClipRectStack = new ImVector<ImVec4>();
    public ImVector<ImTextureRef> _TextureStack = new ImVector<ImTextureRef>();
    public ImVector<byte> _CallbacksDataBuf = new ImVector<byte>();
    public ImVector<ImVec2> _Path = new ImVector<ImVec2>();             // Path storage
    public ImDrawListSplitter _Splitter = new ImDrawListSplitter();     // Splitter channels
    public float _FringeScale = 1.0f;           // Standard scale factor for AA fringes
    public float _InvFringeScale = 1.0f;

    public ImDrawList(ImDrawListSharedData shared_data)
    {
        _SetDrawListSharedData(shared_data);
    }

    void _SetDrawListSharedData(ImDrawListSharedData data)
    {
        if (_Data != null)
            _Data.DrawLists.erase_unsorted(_Data.DrawLists.index_of_ref(this));
        _Data = data;
        if (_Data != null)
            _Data.DrawLists.push_back(this);
    }

    public void _ResetForNewFrame()
    {
        if (_Splitter._Count > 1)
            _Splitter.Merge(this);
        CmdBuffer.resize(0);
        IdxBuffer.resize(0);
        VtxBuffer.resize(0);
        Flags = _Data.InitialFlags;
        _CmdHeader = default;
        _VtxCurrentIdx = 0;
        _VtxWritePos = 0;
        _IdxWritePos = 0;
        _ClipRectStack.resize(0);
        _TextureStack.resize(0);
        _CallbacksDataBuf.resize(0);
        _Path.resize(0);
        _Splitter.Clear();
        CmdBuffer.push_back(new ImDrawCmd());
    }

    public void _ClearFreeMemory()
    {
        CmdBuffer.clear();
        IdxBuffer.clear();
        VtxBuffer.clear();
        Flags = 0;
        _VtxCurrentIdx = 0;
        _VtxWritePos = 0;
        _IdxWritePos = 0;
        _ClipRectStack.clear();
        _TextureStack.clear();
        _CallbacksDataBuf.clear();
        _Path.clear();
        _Splitter.ClearFreeMemory();
    }

    public ImDrawList CloneOutput()
    {
        var dst = new ImDrawList(null);
        dst.CmdBuffer = ImVectorClone(CmdBuffer);
        dst.IdxBuffer = ImVectorClone(IdxBuffer);
        dst.VtxBuffer = ImVectorClone(VtxBuffer);
        dst.Flags = Flags;
        return dst;
    }
    static ImVector<T> ImVectorClone<T>(ImVector<T> src)
    {
        var v = new ImVector<T>();
        v.resize(src.Size);
        for (int i = 0; i < src.Size; i++) v[i] = src[i];
        return v;
    }

    public void AddDrawCmd()
    {
        var draw_cmd = new ImDrawCmd();
        draw_cmd.ClipRect = _CmdHeader.ClipRect;
        draw_cmd.TexRef = _CmdHeader.TexRef;
        draw_cmd.VtxOffset = _CmdHeader.VtxOffset;
        draw_cmd.IdxOffset = (uint)IdxBuffer.Size;
        CmdBuffer.push_back(draw_cmd);
    }

    public void _PopUnusedDrawCmd()
    {
        while (CmdBuffer.Size > 0)
        {
            var curr_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
            if (curr_cmd.ElemCount != 0 || curr_cmd.UserCallback != null)
                return;
            CmdBuffer.pop_back();
        }
    }

    public void AddCallback(ImDrawCallback callback, object userdata, int userdata_size = 0)
    {
        var curr_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
        if (curr_cmd.ElemCount != 0)
            AddDrawCmd();
        curr_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
        curr_cmd.UserCallback = callback;
        if (userdata_size == 0)
        {
            curr_cmd.UserCallbackDataBuf = null;
            curr_cmd.UserCallbackDataSize = 0;
            curr_cmd.UserCallbackDataOffset = -1;
        }
        else
        {
            curr_cmd.UserCallbackDataBuf = new byte[userdata_size];
            curr_cmd.UserCallbackDataSize = userdata_size;
            curr_cmd.UserCallbackDataOffset = _CallbacksDataBuf.Size;
            _CallbacksDataBuf.resize(_CallbacksDataBuf.Size + userdata_size);
        }
        CmdBuffer.Data[CmdBuffer.Size - 1] = curr_cmd;
        AddDrawCmd();
    }

    // Try to merge two last draw commands
    void _TryMergeDrawCmds()
    {
        int last = CmdBuffer.Size - 1;
        var curr_cmd = CmdBuffer.Data[last];
        var prev_cmd = CmdBuffer.Data[last - 1];
        if (ImDrawListUtil.ImDrawCmd_HeaderCompare(curr_cmd, prev_cmd) == 0 && ImDrawListUtil.ImDrawCmd_AreSequentialIdxOffset(prev_cmd, curr_cmd) && curr_cmd.UserCallback == null && prev_cmd.UserCallback == null)
        {
            prev_cmd.ElemCount += curr_cmd.ElemCount;
            CmdBuffer.Data[last - 1] = prev_cmd;
            CmdBuffer.pop_back();
        }
    }

    void _OnChangedClipRect()
    {
        var curr_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
        if (curr_cmd.ElemCount != 0 && curr_cmd.ClipRect != _CmdHeader.ClipRect)
        {
            AddDrawCmd();
            return;
        }
        // Try to merge with previous command if it matches, else use current command
        if (curr_cmd.ElemCount == 0 && CmdBuffer.Size > 1)
        {
            var prev_cmd = CmdBuffer.Data[CmdBuffer.Size - 2];
            var header = ImDrawListUtil.HeaderOf(_CmdHeader);
            if (ImDrawListUtil.ImDrawCmd_HeaderCompare(header, prev_cmd) == 0 && ImDrawListUtil.ImDrawCmd_AreSequentialIdxOffset(prev_cmd, curr_cmd) && prev_cmd.UserCallback == null)
            {
                CmdBuffer.pop_back();
                return;
            }
        }
        curr_cmd.ClipRect = _CmdHeader.ClipRect;
        CmdBuffer.Data[CmdBuffer.Size - 1] = curr_cmd;
    }

    void _OnChangedTexture()
    {
        var curr_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
        if (curr_cmd.ElemCount != 0 && !curr_cmd.TexRef.Equals(_CmdHeader.TexRef))
        {
            AddDrawCmd();
            return;
        }
        if (curr_cmd.UserCallback != null)
            return;
        if (curr_cmd.ElemCount == 0 && CmdBuffer.Size > 1)
        {
            var prev_cmd = CmdBuffer.Data[CmdBuffer.Size - 2];
            var header = ImDrawListUtil.HeaderOf(_CmdHeader);
            if (ImDrawListUtil.ImDrawCmd_HeaderCompare(header, prev_cmd) == 0 && ImDrawListUtil.ImDrawCmd_AreSequentialIdxOffset(prev_cmd, curr_cmd) && prev_cmd.UserCallback == null)
            {
                CmdBuffer.pop_back();
                return;
            }
        }
        curr_cmd.TexRef = _CmdHeader.TexRef;
        CmdBuffer.Data[CmdBuffer.Size - 1] = curr_cmd;
    }

    void _OnChangedVtxOffset()
    {
        _VtxCurrentIdx = 0;
        var curr_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
        if (curr_cmd.ElemCount != 0)
        {
            AddDrawCmd();
            return;
        }
        curr_cmd.VtxOffset = _CmdHeader.VtxOffset;
        CmdBuffer.Data[CmdBuffer.Size - 1] = curr_cmd;
    }

    // (public so widget code such as ImGui::RadioButton() can call it, as in C++)
    public int _CalcCircleAutoSegmentCount(float radius)
    {
        radius *= _InvFringeScale;
        int radius_idx = (int)(radius + 0.999f);
        if (radius_idx >= 0 && radius_idx < _Data.CircleSegmentCounts.Length)
            return _Data.CircleSegmentCounts[radius_idx];
        return ImDrawListConst.IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_CALC(radius, _Data.CircleTessellationMaxError);
    }

    // Render-level scissoring
    public void PushClipRect(ImVec2 cr_min, ImVec2 cr_max, bool intersect_with_current_clip_rect = false)
    {
        var cr = new ImVec4(cr_min.x, cr_min.y, cr_max.x, cr_max.y);
        if (intersect_with_current_clip_rect)
        {
            ImVec4 current = _CmdHeader.ClipRect;
            if (cr.x < current.x) cr.x = current.x;
            if (cr.y < current.y) cr.y = current.y;
            if (cr.z > current.z) cr.z = current.z;
            if (cr.w > current.w) cr.w = current.w;
        }
        cr.z = ImMath.ImMax(cr.x, cr.z);
        cr.w = ImMath.ImMax(cr.y, cr.w);
        _ClipRectStack.push_back(cr);
        _CmdHeader.ClipRect = cr;
        _OnChangedClipRect();
    }

    public void PushClipRectFullScreen()
    {
        PushClipRect(new ImVec2(_Data.ClipRectFullscreen.x, _Data.ClipRectFullscreen.y), new ImVec2(_Data.ClipRectFullscreen.z, _Data.ClipRectFullscreen.w));
    }

    public void PopClipRect()
    {
        _ClipRectStack.pop_back();
        _CmdHeader.ClipRect = (_ClipRectStack.Size == 0) ? _Data.ClipRectFullscreen : _ClipRectStack.Data[_ClipRectStack.Size - 1];
        _OnChangedClipRect();
    }

    public void PushTexture(ImTextureRef tex_ref)
    {
        _TextureStack.push_back(tex_ref);
        _CmdHeader.TexRef = tex_ref;
        if (tex_ref._TexData != null)
            System.Diagnostics.Debug.Assert(tex_ref._TexData.Status != ImTextureStatus_.ImTextureStatus_WantDestroy && tex_ref._TexData.Status != ImTextureStatus_.ImTextureStatus_Destroyed);
        _OnChangedTexture();
    }

    public void PopTexture()
    {
        _TextureStack.pop_back();
        _CmdHeader.TexRef = (_TextureStack.Size == 0) ? new ImTextureRef() : _TextureStack.Data[_TextureStack.Size - 1];
        _OnChangedTexture();
    }

    public void _SetTexture(ImTextureRef tex_ref)
    {
        if (_CmdHeader.TexRef.Equals(tex_ref))
            return;
        _CmdHeader.TexRef = tex_ref;
        _TextureStack.Data[_TextureStack.Size - 1] = tex_ref;
        _OnChangedTexture();
    }

    public void _SetPixelDensity(float pixel_density)
    {
        _FringeScale = 1.0f / pixel_density;
        _InvFringeScale = pixel_density;
    }

    // Reserve space for a number of vertices and indices
    public void PrimReserve(int idx_count, int vtx_count)
    {
        if ((Flags & ImDrawListFlags_.ImDrawListFlags_AllowVtxOffset) != 0 && (_VtxCurrentIdx + (uint)vtx_count >= (1 << 16)))
        {
            _CmdHeader.VtxOffset = (uint)VtxBuffer.Size;
            _OnChangedVtxOffset();
        }
        var draw_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
        draw_cmd.ElemCount += (uint)idx_count;
        CmdBuffer.Data[CmdBuffer.Size - 1] = draw_cmd;

        int vtx_buffer_old_size = VtxBuffer.Size;
        VtxBuffer.resize(vtx_buffer_old_size + vtx_count);
        _VtxWritePos = vtx_buffer_old_size;

        int idx_buffer_old_size = IdxBuffer.Size;
        IdxBuffer.resize(idx_buffer_old_size + idx_count);
        _IdxWritePos = idx_buffer_old_size;
    }

    public void PrimUnreserve(int idx_count, int vtx_count)
    {
        var draw_cmd = CmdBuffer.Data[CmdBuffer.Size - 1];
        draw_cmd.ElemCount -= (uint)idx_count;
        CmdBuffer.Data[CmdBuffer.Size - 1] = draw_cmd;
        VtxBuffer.resize(VtxBuffer.Size - vtx_count);
        IdxBuffer.resize(IdxBuffer.Size - idx_count);
    }

    public void PrimRect(ImVec2 a, ImVec2 c, ImU32 col)
    {
        var b = new ImVec2(c.x, a.y);
        var d = new ImVec2(a.x, c.y);
        var uv = _Data.TexUvWhitePixel;
        uint idx = _VtxCurrentIdx;
        var vb = VtxBuffer.Data; int vp = _VtxWritePos;
        var ib = IdxBuffer.Data; int ip = _IdxWritePos;
        ib[ip + 0] = (ImDrawIdx)idx; ib[ip + 1] = (ImDrawIdx)(idx + 1); ib[ip + 2] = (ImDrawIdx)(idx + 2);
        ib[ip + 3] = (ImDrawIdx)idx; ib[ip + 4] = (ImDrawIdx)(idx + 2); ib[ip + 5] = (ImDrawIdx)(idx + 3);
        vb[vp + 0].pos = a; vb[vp + 0].uv = uv; vb[vp + 0].col = col;
        vb[vp + 1].pos = b; vb[vp + 1].uv = uv; vb[vp + 1].col = col;
        vb[vp + 2].pos = c; vb[vp + 2].uv = uv; vb[vp + 2].col = col;
        vb[vp + 3].pos = d; vb[vp + 3].uv = uv; vb[vp + 3].col = col;
        _VtxWritePos += 4;
        _VtxCurrentIdx += 4;
        _IdxWritePos += 6;
    }

    public void PrimRectUV(ImVec2 a, ImVec2 c, ImVec2 uv_a, ImVec2 uv_c, ImU32 col)
    {
        var b = new ImVec2(c.x, a.y);
        var d = new ImVec2(a.x, c.y);
        var uv_b = new ImVec2(uv_c.x, uv_a.y);
        var uv_d = new ImVec2(uv_a.x, uv_c.y);
        uint idx = _VtxCurrentIdx;
        var vb = VtxBuffer.Data; int vp = _VtxWritePos;
        var ib = IdxBuffer.Data; int ip = _IdxWritePos;
        ib[ip + 0] = (ImDrawIdx)idx; ib[ip + 1] = (ImDrawIdx)(idx + 1); ib[ip + 2] = (ImDrawIdx)(idx + 2);
        ib[ip + 3] = (ImDrawIdx)idx; ib[ip + 4] = (ImDrawIdx)(idx + 2); ib[ip + 5] = (ImDrawIdx)(idx + 3);
        vb[vp + 0].pos = a; vb[vp + 0].uv = uv_a; vb[vp + 0].col = col;
        vb[vp + 1].pos = b; vb[vp + 1].uv = uv_b; vb[vp + 1].col = col;
        vb[vp + 2].pos = c; vb[vp + 2].uv = uv_c; vb[vp + 2].col = col;
        vb[vp + 3].pos = d; vb[vp + 3].uv = uv_d; vb[vp + 3].col = col;
        _VtxWritePos += 4;
        _VtxCurrentIdx += 4;
        _IdxWritePos += 6;
    }

    public void PrimQuadUV(ImVec2 a, ImVec2 b, ImVec2 c, ImVec2 d, ImVec2 uv_a, ImVec2 uv_b, ImVec2 uv_c, ImVec2 uv_d, ImU32 col)
    {
        uint idx = _VtxCurrentIdx;
        var vb = VtxBuffer.Data; int vp = _VtxWritePos;
        var ib = IdxBuffer.Data; int ip = _IdxWritePos;
        ib[ip + 0] = (ImDrawIdx)idx; ib[ip + 1] = (ImDrawIdx)(idx + 1); ib[ip + 2] = (ImDrawIdx)(idx + 2);
        ib[ip + 3] = (ImDrawIdx)idx; ib[ip + 4] = (ImDrawIdx)(idx + 2); ib[ip + 5] = (ImDrawIdx)(idx + 3);
        vb[vp + 0].pos = a; vb[vp + 0].uv = uv_a; vb[vp + 0].col = col;
        vb[vp + 1].pos = b; vb[vp + 1].uv = uv_b; vb[vp + 1].col = col;
        vb[vp + 2].pos = c; vb[vp + 2].uv = uv_c; vb[vp + 2].col = col;
        vb[vp + 3].pos = d; vb[vp + 3].uv = uv_d; vb[vp + 3].col = col;
        _VtxWritePos += 4;
        _VtxCurrentIdx += 4;
        _IdxWritePos += 6;
    }

    public void PrimWriteVtx(ImVec2 pos, ImVec2 uv, ImU32 col)
    {
        var v = VtxBuffer.Data[_VtxWritePos];
        v.pos = pos; v.uv = uv; v.col = col;
        VtxBuffer.Data[_VtxWritePos] = v;
        _VtxWritePos++;
        _VtxCurrentIdx++;
    }

    public void PrimWriteIdx(ImDrawIdx idx)
    {
        IdxBuffer.Data[_IdxWritePos] = idx;
        _IdxWritePos++;
    }

    public void PrimVtx(ImVec2 pos, ImVec2 uv, ImU32 col)
    {
        PrimWriteIdx((ImDrawIdx)_VtxCurrentIdx);
        PrimWriteVtx(pos, uv, col);
    }

    // --- Polyline/fill ---

    // IM_NORMALIZE2F_OVER_ZERO / IM_FIXNORMAL2F helpers
    static void Normalize2FOverZero(ref float vx, ref float vy)
    {
        float d2 = vx * vx + vy * vy;
        if (d2 > 0.0f)
        {
            float inv_len = 1.0f / MathF.Sqrt(d2);
            vx *= inv_len; vy *= inv_len;
        }
    }
    const float IM_FIXNORMAL2F_MAX_INVLEN2 = 100.0f;
    static void FixNormal2F(ref float vx, ref float vy)
    {
        float d2 = vx * vx + vy * vy;
        if (d2 > 0.000001f)
        {
            float inv_len2 = 1.0f / d2;
            if (inv_len2 > IM_FIXNORMAL2F_MAX_INVLEN2) inv_len2 = IM_FIXNORMAL2F_MAX_INVLEN2;
            vx *= inv_len2; vy *= inv_len2;
        }
    }

    public void AddPolyline(ImVec2[] points, int points_count, ImU32 col, float thickness, ImDrawFlags flags = 0)
    {
        if (points_count < 2 || (col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;

        bool closed = (flags & ImDrawFlags_.ImDrawFlags_Closed) != 0;
        ImVec2 opaque_uv = _Data.TexUvWhitePixel;
        int count = closed ? points_count : points_count - 1;
        bool thick_line = thickness > _FringeScale;

        if ((Flags & ImDrawListFlags_.ImDrawListFlags_AntiAliasedLines) != 0)
        {
            // Anti-aliased stroke
            float AA_SIZE = _FringeScale;
            ImU32 col_trans = col & ~ImGuiCore.IM_COL32_A_MASK;

            thickness = ImMath.ImMax(thickness, 1.0f);
            int integer_thickness = (int)thickness;
            float fractional_thickness = thickness - integer_thickness;

            bool use_texture = (Flags & ImDrawListFlags_.ImDrawListFlags_AntiAliasedLinesUseTex) != 0 && (integer_thickness < ImDrawListConst.IM_DRAWLIST_TEX_LINES_WIDTH_MAX) && (fractional_thickness <= 0.00001f) && (AA_SIZE == 1.0f);

            int idx_count = use_texture ? (count * 6) : (thick_line ? count * 18 : count * 12);
            int vtx_count = use_texture ? (points_count * 2) : (thick_line ? points_count * 4 : points_count * 3);
            PrimReserve(idx_count, vtx_count);

            var temp_normals = new ImVec2[points_count * ((use_texture || !thick_line) ? 3 : 5)];
            var temp_points = new ImVec2[temp_normals.Length - points_count];
            // mimic _Data->TempBuffer layout: temp_points = temp_normals + points_count
            var temp = temp_normals;
            var normals = new SpanView(temp, 0, points_count);
            var points2 = new SpanView(temp, points_count, temp.Length - points_count);

            for (int i1 = 0; i1 < count; i1++)
            {
                int i2 = (i1 + 1) == points_count ? 0 : i1 + 1;
                float dx = points[i2].x - points[i1].x;
                float dy = points[i2].y - points[i1].y;
                Normalize2FOverZero(ref dx, ref dy);
                normals[i1] = new ImVec2(dy, -dx);
            }
            if (!closed)
                normals[points_count - 1] = normals[points_count - 2];

            if (use_texture || !thick_line)
            {
                float half_draw_size = use_texture ? ((thickness * 0.5f) + 1) : AA_SIZE;

                if (!closed)
                {
                    points2[0] = points[0] + normals[0] * half_draw_size;
                    points2[1] = points[0] - normals[0] * half_draw_size;
                    points2[(points_count - 1) * 2 + 0] = points[points_count - 1] + normals[points_count - 1] * half_draw_size;
                    points2[(points_count - 1) * 2 + 1] = points[points_count - 1] - normals[points_count - 1] * half_draw_size;
                }

                uint idx1 = _VtxCurrentIdx;
                for (int i1 = 0; i1 < count; i1++)
                {
                    int i2 = (i1 + 1) == points_count ? 0 : i1 + 1;
                    uint idx2 = ((i1 + 1) == points_count) ? _VtxCurrentIdx : (idx1 + (uint)(use_texture ? 2 : 3));

                    float dm_x = (normals[i1].x + normals[i2].x) * 0.5f;
                    float dm_y = (normals[i1].y + normals[i2].y) * 0.5f;
                    FixNormal2F(ref dm_x, ref dm_y);
                    dm_x *= half_draw_size;
                    dm_y *= half_draw_size;

                    var out_v0 = new ImVec2(points[i2].x + dm_x, points[i2].y + dm_y);
                    var out_v1 = new ImVec2(points[i2].x - dm_x, points[i2].y - dm_y);
                    points2[i2 * 2 + 0] = out_v0;
                    points2[i2 * 2 + 1] = out_v1;

                    var ib = IdxBuffer.Data; int ip = _IdxWritePos;
                    if (use_texture)
                    {
                        ib[ip + 0] = (ImDrawIdx)(idx2 + 0); ib[ip + 1] = (ImDrawIdx)(idx1 + 0); ib[ip + 2] = (ImDrawIdx)(idx1 + 1);
                        ib[ip + 3] = (ImDrawIdx)(idx2 + 1); ib[ip + 4] = (ImDrawIdx)(idx1 + 1); ib[ip + 5] = (ImDrawIdx)(idx2 + 0);
                        _IdxWritePos += 6;
                    }
                    else
                    {
                        ib[ip + 0] = (ImDrawIdx)(idx2 + 0); ib[ip + 1] = (ImDrawIdx)(idx1 + 0); ib[ip + 2] = (ImDrawIdx)(idx1 + 2);
                        ib[ip + 3] = (ImDrawIdx)(idx1 + 2); ib[ip + 4] = (ImDrawIdx)(idx2 + 2); ib[ip + 5] = (ImDrawIdx)(idx2 + 0);
                        ib[ip + 6] = (ImDrawIdx)(idx2 + 1); ib[ip + 7] = (ImDrawIdx)(idx1 + 1); ib[ip + 8] = (ImDrawIdx)(idx1 + 0);
                        ib[ip + 9] = (ImDrawIdx)(idx1 + 0); ib[ip + 10] = (ImDrawIdx)(idx2 + 0); ib[ip + 11] = (ImDrawIdx)(idx2 + 1);
                        _IdxWritePos += 12;
                    }
                    idx1 = idx2;
                }

                var vb = VtxBuffer.Data; int vp = _VtxWritePos;
                if (use_texture)
                {
                    ImVec4 tex_uvs = _Data.TexUvLines[integer_thickness];
                    var tex_uv0 = new ImVec2(tex_uvs.x, tex_uvs.y);
                    var tex_uv1 = new ImVec2(tex_uvs.z, tex_uvs.w);
                    for (int i = 0; i < points_count; i++)
                    {
                        vb[vp + 0].pos = points2[i * 2 + 0]; vb[vp + 0].uv = tex_uv0; vb[vp + 0].col = col;
                        vb[vp + 1].pos = points2[i * 2 + 1]; vb[vp + 1].uv = tex_uv1; vb[vp + 1].col = col;
                        vp += 2;
                    }
                }
                else
                {
                    for (int i = 0; i < points_count; i++)
                    {
                        vb[vp + 0].pos = points[i]; vb[vp + 0].uv = opaque_uv; vb[vp + 0].col = col;
                        vb[vp + 1].pos = points2[i * 2 + 0]; vb[vp + 1].uv = opaque_uv; vb[vp + 1].col = col_trans;
                        vb[vp + 2].pos = points2[i * 2 + 1]; vb[vp + 2].uv = opaque_uv; vb[vp + 2].col = col_trans;
                        vp += 3;
                    }
                }
                _VtxWritePos = vp;
            }
            else
            {
                // Non texture-based lines (thick)
                float half_inner_thickness = (thickness - AA_SIZE) * 0.5f;

                if (!closed)
                {
                    int points_last = points_count - 1;
                    points2[0] = points[0] + normals[0] * (half_inner_thickness + AA_SIZE);
                    points2[1] = points[0] + normals[0] * (half_inner_thickness);
                    points2[2] = points[0] - normals[0] * (half_inner_thickness);
                    points2[3] = points[0] - normals[0] * (half_inner_thickness + AA_SIZE);
                    points2[points_last * 4 + 0] = points[points_last] + normals[points_last] * (half_inner_thickness + AA_SIZE);
                    points2[points_last * 4 + 1] = points[points_last] + normals[points_last] * (half_inner_thickness);
                    points2[points_last * 4 + 2] = points[points_last] - normals[points_last] * (half_inner_thickness);
                    points2[points_last * 4 + 3] = points[points_last] - normals[points_last] * (half_inner_thickness + AA_SIZE);
                }

                uint idx1 = _VtxCurrentIdx;
                for (int i1 = 0; i1 < count; i1++)
                {
                    int i2 = (i1 + 1) == points_count ? 0 : (i1 + 1);
                    uint idx2 = (i1 + 1) == points_count ? _VtxCurrentIdx : (idx1 + 4);

                    float dm_x = (normals[i1].x + normals[i2].x) * 0.5f;
                    float dm_y = (normals[i1].y + normals[i2].y) * 0.5f;
                    FixNormal2F(ref dm_x, ref dm_y);
                    float dm_out_x = dm_x * (half_inner_thickness + AA_SIZE);
                    float dm_out_y = dm_y * (half_inner_thickness + AA_SIZE);
                    float dm_in_x = dm_x * half_inner_thickness;
                    float dm_in_y = dm_y * half_inner_thickness;

                    points2[i2 * 4 + 0] = new ImVec2(points[i2].x + dm_out_x, points[i2].y + dm_out_y);
                    points2[i2 * 4 + 1] = new ImVec2(points[i2].x + dm_in_x, points[i2].y + dm_in_y);
                    points2[i2 * 4 + 2] = new ImVec2(points[i2].x - dm_in_x, points[i2].y - dm_in_y);
                    points2[i2 * 4 + 3] = new ImVec2(points[i2].x - dm_out_x, points[i2].y - dm_out_y);

                    var ib = IdxBuffer.Data; int ip = _IdxWritePos;
                    ib[ip + 0] = (ImDrawIdx)(idx2 + 1); ib[ip + 1] = (ImDrawIdx)(idx1 + 1); ib[ip + 2] = (ImDrawIdx)(idx1 + 2);
                    ib[ip + 3] = (ImDrawIdx)(idx1 + 2); ib[ip + 4] = (ImDrawIdx)(idx2 + 2); ib[ip + 5] = (ImDrawIdx)(idx2 + 1);
                    ib[ip + 6] = (ImDrawIdx)(idx2 + 1); ib[ip + 7] = (ImDrawIdx)(idx1 + 1); ib[ip + 8] = (ImDrawIdx)(idx1 + 0);
                    ib[ip + 9] = (ImDrawIdx)(idx1 + 0); ib[ip + 10] = (ImDrawIdx)(idx2 + 0); ib[ip + 11] = (ImDrawIdx)(idx2 + 1);
                    ib[ip + 12] = (ImDrawIdx)(idx2 + 2); ib[ip + 13] = (ImDrawIdx)(idx1 + 2); ib[ip + 14] = (ImDrawIdx)(idx1 + 3);
                    ib[ip + 15] = (ImDrawIdx)(idx1 + 3); ib[ip + 16] = (ImDrawIdx)(idx2 + 3); ib[ip + 17] = (ImDrawIdx)(idx2 + 2);
                    _IdxWritePos += 18;

                    idx1 = idx2;
                }

                var vb = VtxBuffer.Data; int vp = _VtxWritePos;
                for (int i = 0; i < points_count; i++)
                {
                    vb[vp + 0].pos = points2[i * 4 + 0]; vb[vp + 0].uv = opaque_uv; vb[vp + 0].col = col_trans;
                    vb[vp + 1].pos = points2[i * 4 + 1]; vb[vp + 1].uv = opaque_uv; vb[vp + 1].col = col;
                    vb[vp + 2].pos = points2[i * 4 + 2]; vb[vp + 2].uv = opaque_uv; vb[vp + 2].col = col;
                    vb[vp + 3].pos = points2[i * 4 + 3]; vb[vp + 3].uv = opaque_uv; vb[vp + 3].col = col_trans;
                    vp += 4;
                }
                _VtxWritePos = vp;
            }
            _VtxCurrentIdx += (uint)vtx_count;
        }
        else
        {
            // Non texture-based, Non anti-aliased lines
            int idx_count = count * 6;
            int vtx_count = count * 4;
            PrimReserve(idx_count, vtx_count);

            var vb = VtxBuffer.Data; var ib = IdxBuffer.Data;
            for (int i1 = 0; i1 < count; i1++)
            {
                int i2 = (i1 + 1) == points_count ? 0 : i1 + 1;
                var p1 = points[i1];
                var p2 = points[i2];

                float dx = p2.x - p1.x;
                float dy = p2.y - p1.y;
                Normalize2FOverZero(ref dx, ref dy);
                dx *= (thickness * 0.5f);
                dy *= (thickness * 0.5f);

                int vp = _VtxWritePos; int ip = _IdxWritePos;
                vb[vp + 0].pos = new ImVec2(p1.x + dy, p1.y - dx); vb[vp + 0].uv = opaque_uv; vb[vp + 0].col = col;
                vb[vp + 1].pos = new ImVec2(p2.x + dy, p2.y - dx); vb[vp + 1].uv = opaque_uv; vb[vp + 1].col = col;
                vb[vp + 2].pos = new ImVec2(p2.x - dy, p2.y + dx); vb[vp + 2].uv = opaque_uv; vb[vp + 2].col = col;
                vb[vp + 3].pos = new ImVec2(p1.x - dy, p1.y + dx); vb[vp + 3].uv = opaque_uv; vb[vp + 3].col = col;
                _VtxWritePos += 4;

                ib[ip + 0] = (ImDrawIdx)(_VtxCurrentIdx); ib[ip + 1] = (ImDrawIdx)(_VtxCurrentIdx + 1); ib[ip + 2] = (ImDrawIdx)(_VtxCurrentIdx + 2);
                ib[ip + 3] = (ImDrawIdx)(_VtxCurrentIdx); ib[ip + 4] = (ImDrawIdx)(_VtxCurrentIdx + 2); ib[ip + 5] = (ImDrawIdx)(_VtxCurrentIdx + 3);
                _IdxWritePos += 6;
                _VtxCurrentIdx += 4;
            }
        }
    }

    // small span view helper used by AddPolyline temp buffers
    struct SpanView
    {
        public ImVec2[] Data; public int Off;
        public SpanView(ImVec2[] d, int off, int count) { Data = d; Off = off; }
        public ImVec2 this[int i] { get => Data[Off + i]; set => Data[Off + i] = value; }
    }

    public void AddConvexPolyFilled(ImVec2[] points, int points_count, ImU32 col)
    {
        if (points_count < 3 || (col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;

        var uv = _Data.TexUvWhitePixel;

        if ((Flags & ImDrawListFlags_.ImDrawListFlags_AntiAliasedFill) != 0)
        {
            // Anti-aliased Fill
            float AA_SIZE = _FringeScale;
            ImU32 col_trans = col & ~ImGuiCore.IM_COL32_A_MASK;
            int idx_count = (points_count - 2) * 3 + points_count * 6;
            int vtx_count = (points_count * 2);
            PrimReserve(idx_count, vtx_count);

            // Add indexes for fill
            uint vtx_inner_idx = _VtxCurrentIdx;
            uint vtx_outer_idx = _VtxCurrentIdx + 1;
            var ib = IdxBuffer.Data; int ip = _IdxWritePos;
            for (int i = 2; i < points_count; i++)
            {
                ib[ip + 0] = (ImDrawIdx)(vtx_inner_idx); ib[ip + 1] = (ImDrawIdx)(vtx_inner_idx + ((uint)(i - 1) << 1)); ib[ip + 2] = (ImDrawIdx)(vtx_inner_idx + ((uint)i << 1));
                ip += 3;
            }
            _IdxWritePos = ip;

            // Compute normals
            var temp_normals = new ImVec2[points_count];
            for (int i0 = points_count - 1, i1 = 0; i1 < points_count; i0 = i1++)
            {
                var p0 = points[i0];
                var p1 = points[i1];
                float dx = p1.x - p0.x;
                float dy = p1.y - p0.y;
                Normalize2FOverZero(ref dx, ref dy);
                temp_normals[i0] = new ImVec2(dy, -dx);
            }

            var vb = VtxBuffer.Data; int vp = _VtxWritePos;
            for (int i0 = points_count - 1, i1 = 0; i1 < points_count; i0 = i1++)
            {
                var n0 = temp_normals[i0];
                var n1 = temp_normals[i1];
                float dm_x = (n0.x + n1.x) * 0.5f;
                float dm_y = (n0.y + n1.y) * 0.5f;
                FixNormal2F(ref dm_x, ref dm_y);
                dm_x *= AA_SIZE * 0.5f;
                dm_y *= AA_SIZE * 0.5f;

                vb[vp + 0].pos = new ImVec2(points[i1].x - dm_x, points[i1].y - dm_y); vb[vp + 0].uv = uv; vb[vp + 0].col = col;        // Inner
                vb[vp + 1].pos = new ImVec2(points[i1].x + dm_x, points[i1].y + dm_y); vb[vp + 1].uv = uv; vb[vp + 1].col = col_trans;  // Outer
                vp += 2;

                ib[ip + 0] = (ImDrawIdx)(vtx_inner_idx + ((uint)i1 << 1)); ib[ip + 1] = (ImDrawIdx)(vtx_inner_idx + ((uint)i0 << 1)); ib[ip + 2] = (ImDrawIdx)(vtx_outer_idx + ((uint)i0 << 1));
                ib[ip + 3] = (ImDrawIdx)(vtx_outer_idx + ((uint)i0 << 1)); ib[ip + 4] = (ImDrawIdx)(vtx_outer_idx + ((uint)i1 << 1)); ib[ip + 5] = (ImDrawIdx)(vtx_inner_idx + ((uint)i1 << 1));
                ip += 6;
            }
            _VtxWritePos = vp;
            _IdxWritePos = ip;
            _VtxCurrentIdx += (uint)vtx_count;
        }
        else
        {
            // Non Anti-aliased Fill
            int idx_count = (points_count - 2) * 3;
            int vtx_count = points_count;
            PrimReserve(idx_count, vtx_count);
            var vb = VtxBuffer.Data; int vp = _VtxWritePos;
            for (int i = 0; i < vtx_count; i++)
            {
                vb[vp].pos = points[i]; vb[vp].uv = uv; vb[vp].col = col;
                vp++;
            }
            _VtxWritePos = vp;
            var ib = IdxBuffer.Data; int ip = _IdxWritePos;
            for (int i = 2; i < points_count; i++)
            {
                ib[ip + 0] = (ImDrawIdx)(_VtxCurrentIdx); ib[ip + 1] = (ImDrawIdx)(_VtxCurrentIdx + (uint)(i - 1)); ib[ip + 2] = (ImDrawIdx)(_VtxCurrentIdx + (uint)i);
                ip += 3;
            }
            _IdxWritePos = ip;
            _VtxCurrentIdx += (uint)vtx_count;
        }
    }

    // --- Path ---

    public void _PathArcToFastEx(ImVec2 center, float radius, int a_min_sample, int a_max_sample, int a_step)
    {
        if (radius < 0.5f)
        {
            _Path.push_back(center);
            return;
        }

        if (a_step <= 0)
            a_step = ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX / _CalcCircleAutoSegmentCount(radius);

        a_step = ImMath.ImClamp(a_step, 1, ImDrawListConst.IM_DRAWLIST_ARCFAST_TABLE_SIZE / 4);

        int sample_range = Math.Abs(a_max_sample - a_min_sample);
        int a_next_step = a_step;

        int samples = sample_range + 1;
        bool extra_max_sample = false;
        if (a_step > 1)
        {
            samples = sample_range / a_step + 1;
            int overstep = sample_range % a_step;
            if (overstep > 0)
            {
                extra_max_sample = true;
                samples++;
                if (sample_range > 0)
                    a_step -= (a_step - overstep) / 2;
            }
        }

        _Path.resize(_Path.Size + samples);
        int out_off = _Path.Size - samples;

        int sample_index = a_min_sample;
        if (sample_index < 0 || sample_index >= ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX)
        {
            sample_index = sample_index % ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
            if (sample_index < 0)
                sample_index += ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
        }

        if (a_max_sample >= a_min_sample)
        {
            for (int a = a_min_sample; a <= a_max_sample; a += a_step, sample_index += a_step, a_step = a_next_step)
            {
                if (sample_index >= ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX)
                    sample_index -= ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
                var s = _Data.ArcFastVtx[sample_index];
                _Path.Data[out_off].x = center.x + s.x * radius;
                _Path.Data[out_off].y = center.y + s.y * radius;
                out_off++;
            }
        }
        else
        {
            for (int a = a_min_sample; a >= a_max_sample; a -= a_step, sample_index -= a_step, a_step = a_next_step)
            {
                if (sample_index < 0)
                    sample_index += ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
                var s = _Data.ArcFastVtx[sample_index];
                _Path.Data[out_off].x = center.x + s.x * radius;
                _Path.Data[out_off].y = center.y + s.y * radius;
                out_off++;
            }
        }

        if (extra_max_sample)
        {
            int normalized_max_sample = a_max_sample % ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
            if (normalized_max_sample < 0)
                normalized_max_sample += ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
            var s = _Data.ArcFastVtx[normalized_max_sample];
            _Path.Data[out_off].x = center.x + s.x * radius;
            _Path.Data[out_off].y = center.y + s.y * radius;
            out_off++;
        }
    }

    public void _PathArcToN(ImVec2 center, float radius, float a_min, float a_max, int num_segments)
    {
        if (radius < 0.5f)
        {
            _Path.push_back(center);
            return;
        }
        _Path.reserve(_Path.Size + (num_segments + 1));
        for (int i = 0; i <= num_segments; i++)
        {
            float a = a_min + ((float)i / (float)num_segments) * (a_max - a_min);
            _Path.push_back(new ImVec2(center.x + ImMath.ImCos(a) * radius, center.y + ImMath.ImSin(a) * radius));
        }
    }

    public void PathArcToFast(ImVec2 center, float radius, int a_min_of_12, int a_max_of_12)
    {
        if (radius < 0.5f)
        {
            _Path.push_back(center);
            return;
        }
        _PathArcToFastEx(center, radius, a_min_of_12 * ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX / 12, a_max_of_12 * ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX / 12, 0);
    }

    public void PathArcTo(ImVec2 center, float radius, float a_min, float a_max, int num_segments)
    {
        if (radius < 0.5f)
        {
            _Path.push_back(center);
            return;
        }
        if (num_segments > 0)
        {
            _PathArcToN(center, radius, a_min, a_max, num_segments);
            return;
        }
        if (radius <= _Data.ArcFastRadiusCutoff)
        {
            bool a_is_reverse = a_max < a_min;
            float a_min_sample_f = ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX * a_min / (ImMath.IM_PI * 2.0f);
            float a_max_sample_f = ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX * a_max / (ImMath.IM_PI * 2.0f);

            int a_min_sample = a_is_reverse ? (int)ImMath.ImFloor(a_min_sample_f) : (int)ImMath.ImCeil(a_min_sample_f);
            int a_max_sample = a_is_reverse ? (int)ImMath.ImCeil(a_max_sample_f) : (int)ImMath.ImFloor(a_max_sample_f);
            int a_mid_samples = a_is_reverse ? ImMath.ImMax(a_min_sample - a_max_sample, 0) : ImMath.ImMax(a_max_sample - a_min_sample, 0);

            float a_min_segment_angle = a_min_sample * ImMath.IM_PI * 2.0f / ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
            float a_max_segment_angle = a_max_sample * ImMath.IM_PI * 2.0f / ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX;
            bool a_emit_start = Math.Abs(a_min_segment_angle - a_min) >= 1e-5f;
            bool a_emit_end = Math.Abs(a_max - a_max_segment_angle) >= 1e-5f;

            _Path.reserve(_Path.Size + (a_mid_samples + 1 + (a_emit_start ? 1 : 0) + (a_emit_end ? 1 : 0)));
            if (a_emit_start)
                _Path.push_back(new ImVec2(center.x + ImMath.ImCos(a_min) * radius, center.y + ImMath.ImSin(a_min) * radius));
            if (a_mid_samples > 0)
                _PathArcToFastEx(center, radius, a_min_sample, a_max_sample, 0);
            if (a_emit_end)
                _Path.push_back(new ImVec2(center.x + ImMath.ImCos(a_max) * radius, center.y + ImMath.ImSin(a_max) * radius));
        }
        else
        {
            float arc_length = Math.Abs(a_max - a_min);
            int circle_segment_count = _CalcCircleAutoSegmentCount(radius);
            int arc_segment_count = ImMath.ImMax((int)ImMath.ImCeil(circle_segment_count * arc_length / (ImMath.IM_PI * 2.0f)), 1);
            _PathArcToN(center, radius, a_min, a_max, arc_segment_count);
        }
    }

    public void PathEllipticalArcTo(ImVec2 center, ImVec2 radius, float rot, float a_min, float a_max, int num_segments)
    {
        if (num_segments <= 0)
            num_segments = _CalcCircleAutoSegmentCount(ImMath.ImMax(radius.x, radius.y));
        _Path.reserve(_Path.Size + (num_segments + 1));
        float cos_rot = ImMath.ImCos(rot);
        float sin_rot = ImMath.ImSin(rot);
        for (int i = 0; i <= num_segments; i++)
        {
            float a = a_min + ((float)i / (float)num_segments) * (a_max - a_min);
            var point = new ImVec2(ImMath.ImCos(a) * radius.x, ImMath.ImSin(a) * radius.y);
            var rel = new ImVec2((point.x * cos_rot) - (point.y * sin_rot), (point.x * sin_rot) + (point.y * cos_rot));
            point.x = rel.x + center.x;
            point.y = rel.y + center.y;
            _Path.push_back(point);
        }
    }

    public void PathLineTo(ImVec2 pos) => _Path.push_back(pos);
    public void PathLineToMergeDuplicate(ImVec2 pos)
    {
        if (_Path.Size == 0 || _Path.Data[_Path.Size - 1].x != pos.x || _Path.Data[_Path.Size - 1].y != pos.y)
            _Path.push_back(pos);
    }
    public void PathClear() => _Path.resize(0);
    public ImVec2 PathLast() => _Path.Data[_Path.Size - 1];

    public void PathFillConvex(ImU32 col) { AddConvexPolyFilled(_Path.Data, _Path.Size, col); _Path.resize(0); }
    public void PathFillConcave(ImU32 col) { AddConcavePolyFilled(_Path.Data, _Path.Size, col); _Path.resize(0); }
    public void PathStroke(ImU32 col, float thickness = -1.0f, ImDrawFlags flags = 0) { AddPolyline(_Path.Data, _Path.Size, col, thickness, flags); _Path.resize(0); }

    public static ImVec2 ImBezierCubicCalc(ImVec2 p1, ImVec2 p2, ImVec2 p3, ImVec2 p4, float t)
    {
        float u = 1.0f - t;
        float w1 = u * u * u;
        float w2 = 3 * u * u * t;
        float w3 = 3 * u * t * t;
        float w4 = t * t * t;
        return new ImVec2(w1 * p1.x + w2 * p2.x + w3 * p3.x + w4 * p4.x, w1 * p1.y + w2 * p2.y + w3 * p3.y + w4 * p4.y);
    }
    public static ImVec2 ImBezierQuadraticCalc(ImVec2 p1, ImVec2 p2, ImVec2 p3, float t)
    {
        float u = 1.0f - t;
        float w1 = u * u;
        float w2 = 2 * u * t;
        float w3 = t * t;
        return new ImVec2(w1 * p1.x + w2 * p2.x + w3 * p3.x, w1 * p1.y + w2 * p2.y + w3 * p3.y);
    }

    static void PathBezierCubicCurveToCasteljau(ImVector<ImVec2> path, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4, float max_error_sqr, int level)
    {
        float dx = x4 - x1;
        float dy = y4 - y1;
        float d2 = (x2 - x4) * dy - (y2 - y4) * dx;
        float d3 = (x3 - x4) * dy - (y3 - y4) * dx;
        d2 = (d2 >= 0) ? d2 : -d2;
        d3 = (d3 >= 0) ? d3 : -d3;
        if ((d2 + d3) * (d2 + d3) < max_error_sqr * (dx * dx + dy * dy))
        {
            path.push_back(new ImVec2(x4, y4));
        }
        else if (level < 10)
        {
            float x12 = (x1 + x2) * 0.5f, y12 = (y1 + y2) * 0.5f;
            float x23 = (x2 + x3) * 0.5f, y23 = (y2 + y3) * 0.5f;
            float x34 = (x3 + x4) * 0.5f, y34 = (y3 + y4) * 0.5f;
            float x123 = (x12 + x23) * 0.5f, y123 = (y12 + y23) * 0.5f;
            float x234 = (x23 + x34) * 0.5f, y234 = (y23 + y34) * 0.5f;
            float x1234 = (x123 + x234) * 0.5f, y1234 = (y123 + y234) * 0.5f;
            PathBezierCubicCurveToCasteljau(path, x1, y1, x12, y12, x123, y123, x1234, y1234, max_error_sqr, level + 1);
            PathBezierCubicCurveToCasteljau(path, x1234, y1234, x234, y234, x34, y34, x4, y4, max_error_sqr, level + 1);
        }
    }

    static void PathBezierQuadraticCurveToCasteljau(ImVector<ImVec2> path, float x1, float y1, float x2, float y2, float x3, float y3, float max_error_sqr, int level)
    {
        float dx = x3 - x1, dy = y3 - y1;
        float det = (x2 - x3) * dy - (y2 - y3) * dx;
        if (det * det * 4.0f < max_error_sqr * (dx * dx + dy * dy))
        {
            path.push_back(new ImVec2(x3, y3));
        }
        else if (level < 10)
        {
            float x12 = (x1 + x2) * 0.5f, y12 = (y1 + y2) * 0.5f;
            float x23 = (x2 + x3) * 0.5f, y23 = (y2 + y3) * 0.5f;
            float x123 = (x12 + x23) * 0.5f, y123 = (y12 + y23) * 0.5f;
            PathBezierQuadraticCurveToCasteljau(path, x1, y1, x12, y12, x123, y123, max_error_sqr, level + 1);
            PathBezierQuadraticCurveToCasteljau(path, x123, y123, x23, y23, x3, y3, max_error_sqr, level + 1);
        }
    }

    public void PathBezierCubicCurveTo(ImVec2 p2, ImVec2 p3, ImVec2 p4, int num_segments)
    {
        ImVec2 p1 = _Path.back();
        if (num_segments == 0)
        {
            float max_error_sqr = (_Data.CurveTessellationMaxError * _FringeScale) * (_Data.CurveTessellationMaxError * _FringeScale);
            PathBezierCubicCurveToCasteljau(_Path, p1.x, p1.y, p2.x, p2.y, p3.x, p3.y, p4.x, p4.y, max_error_sqr, 0);
        }
        else
        {
            float t_step = 1.0f / (float)num_segments;
            for (int i_step = 1; i_step <= num_segments; i_step++)
                _Path.push_back(ImBezierCubicCalc(p1, p2, p3, p4, t_step * i_step));
        }
    }

    public void PathBezierQuadraticCurveTo(ImVec2 p2, ImVec2 p3, int num_segments)
    {
        ImVec2 p1 = _Path.back();
        if (num_segments == 0)
        {
            float max_error_sqr = (_Data.CurveTessellationMaxError * _FringeScale) * (_Data.CurveTessellationMaxError * _FringeScale);
            PathBezierQuadraticCurveToCasteljau(_Path, p1.x, p1.y, p2.x, p2.y, p3.x, p3.y, max_error_sqr, 0);
        }
        else
        {
            float t_step = 1.0f / (float)num_segments;
            for (int i_step = 1; i_step <= num_segments; i_step++)
                _Path.push_back(ImBezierQuadraticCalc(p1, p2, p3, t_step * i_step));
        }
    }

    public void PathRect(ImVec2 a, ImVec2 b, float rounding, ImDrawFlags flags)
    {
        if (rounding >= 0.5f)
        {
            if ((flags & ImDrawFlags_.ImDrawFlags_RoundCornersMask_) == 0)
                flags |= ImDrawFlags_.ImDrawFlags_RoundCornersAll;
            rounding = ImMath.ImMin(rounding, Math.Abs(b.x - a.x) * (((flags & ImDrawFlags_.ImDrawFlags_RoundCornersTop) == ImDrawFlags_.ImDrawFlags_RoundCornersTop) || ((flags & ImDrawFlags_.ImDrawFlags_RoundCornersBottom) == ImDrawFlags_.ImDrawFlags_RoundCornersBottom) ? 0.5f : 1.0f) - 1.0f);
            rounding = ImMath.ImMin(rounding, Math.Abs(b.y - a.y) * (((flags & ImDrawFlags_.ImDrawFlags_RoundCornersLeft) == ImDrawFlags_.ImDrawFlags_RoundCornersLeft) || ((flags & ImDrawFlags_.ImDrawFlags_RoundCornersRight) == ImDrawFlags_.ImDrawFlags_RoundCornersRight) ? 0.5f : 1.0f) - 1.0f);
        }
        if (rounding < 0.5f || (flags & ImDrawFlags_.ImDrawFlags_RoundCornersMask_) == ImDrawFlags_.ImDrawFlags_RoundCornersNone)
        {
            PathLineTo(a);
            PathLineTo(new ImVec2(b.x, a.y));
            PathLineTo(b);
            PathLineTo(new ImVec2(a.x, b.y));
        }
        else
        {
            float rounding_tl = (flags & ImDrawFlags_.ImDrawFlags_RoundCornersTopLeft) != 0 ? rounding : 0.0f;
            float rounding_tr = (flags & ImDrawFlags_.ImDrawFlags_RoundCornersTopRight) != 0 ? rounding : 0.0f;
            float rounding_br = (flags & ImDrawFlags_.ImDrawFlags_RoundCornersBottomRight) != 0 ? rounding : 0.0f;
            float rounding_bl = (flags & ImDrawFlags_.ImDrawFlags_RoundCornersBottomLeft) != 0 ? rounding : 0.0f;
            PathArcToFast(new ImVec2(a.x + rounding_tl, a.y + rounding_tl), rounding_tl, 6, 9);
            PathArcToFast(new ImVec2(b.x - rounding_tr, a.y + rounding_tr), rounding_tr, 9, 12);
            PathArcToFast(new ImVec2(b.x - rounding_br, b.y - rounding_br), rounding_br, 0, 3);
            PathArcToFast(new ImVec2(a.x + rounding_bl, b.y - rounding_bl), rounding_bl, 3, 6);
        }
    }

    // --- Shape primitives ---

    public void AddLine(ImVec2 p1, ImVec2 p2, ImU32 col, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        AddPolyline(new[] { new ImVec2(p1.x + 0.5f, p1.y + 0.5f), new ImVec2(p2.x + 0.5f, p2.y + 0.5f) }, 2, col, thickness);
    }

    public void AddLineH(float min_x, float max_x, float y, ImU32 col, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        AddPolyline(new[] { new ImVec2(min_x + 0.5f, y + 0.5f), new ImVec2(max_x + 0.5f, y + 0.5f) }, 2, col, thickness);
    }

    public void AddLineV(float x, float min_y, float max_y, ImU32 col, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        AddPolyline(new[] { new ImVec2(x + 0.5f, min_y + 0.5f), new ImVec2(x + 0.5f, max_y + 0.5f) }, 2, col, thickness);
    }

    public void AddRect(ImVec2 p_min, ImVec2 p_max, ImU32 col, float rounding = 0.0f, float thickness = 1.0f, ImDrawFlags flags = 0)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        if ((Flags & ImDrawListFlags_.ImDrawListFlags_AntiAliasedLines) != 0)
            PathRect(p_min + new ImVec2(0.50f, 0.50f), p_max - new ImVec2(0.50f, 0.50f), rounding, flags);
        else
            PathRect(p_min + new ImVec2(0.50f, 0.50f), p_max - new ImVec2(0.49f, 0.49f), rounding, flags);
        PathStroke(col, thickness, ImDrawFlags_.ImDrawFlags_Closed);
    }

    public void AddRectFilled(ImVec2 p_min, ImVec2 p_max, ImU32 col, float rounding = 0.0f, ImDrawFlags flags = 0)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        if (rounding < 0.5f || (flags & ImDrawFlags_.ImDrawFlags_RoundCornersMask_) == ImDrawFlags_.ImDrawFlags_RoundCornersNone)
        {
            PrimReserve(6, 4);
            PrimRect(p_min, p_max, col);
        }
        else
        {
            PathRect(p_min, p_max, rounding, flags);
            PathFillConvex(col);
        }
    }

    public void AddRectFilledMultiColor(ImVec2 p_min, ImVec2 p_max, ImU32 col_upr_left, ImU32 col_upr_right, ImU32 col_bot_right, ImU32 col_bot_left)
    {
        if (((col_upr_left | col_upr_right | col_bot_right | col_bot_left) & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        var uv = _Data.TexUvWhitePixel;
        PrimReserve(6, 4);
        PrimWriteIdx((ImDrawIdx)(_VtxCurrentIdx)); PrimWriteIdx((ImDrawIdx)(_VtxCurrentIdx + 1)); PrimWriteIdx((ImDrawIdx)(_VtxCurrentIdx + 2));
        PrimWriteIdx((ImDrawIdx)(_VtxCurrentIdx)); PrimWriteIdx((ImDrawIdx)(_VtxCurrentIdx + 2)); PrimWriteIdx((ImDrawIdx)(_VtxCurrentIdx + 3));
        PrimWriteVtx(p_min, uv, col_upr_left);
        PrimWriteVtx(new ImVec2(p_max.x, p_min.y), uv, col_upr_right);
        PrimWriteVtx(p_max, uv, col_bot_right);
        PrimWriteVtx(new ImVec2(p_min.x, p_max.y), uv, col_bot_left);
    }

    public void AddQuad(ImVec2 p1, ImVec2 p2, ImVec2 p3, ImVec2 p4, ImU32 col, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        PathLineTo(p1); PathLineTo(p2); PathLineTo(p3); PathLineTo(p4);
        PathStroke(col, thickness, ImDrawFlags_.ImDrawFlags_Closed);
    }

    public void AddQuadFilled(ImVec2 p1, ImVec2 p2, ImVec2 p3, ImVec2 p4, ImU32 col)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        PathLineTo(p1); PathLineTo(p2); PathLineTo(p3); PathLineTo(p4);
        PathFillConvex(col);
    }

    public void AddTriangle(ImVec2 p1, ImVec2 p2, ImVec2 p3, ImU32 col, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        PathLineTo(p1); PathLineTo(p2); PathLineTo(p3);
        PathStroke(col, thickness, ImDrawFlags_.ImDrawFlags_Closed);
    }

    public void AddTriangleFilled(ImVec2 p1, ImVec2 p2, ImVec2 p3, ImU32 col)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        PathLineTo(p1); PathLineTo(p2); PathLineTo(p3);
        PathFillConvex(col);
    }

    public void AddCircle(ImVec2 center, float radius, ImU32 col, int num_segments = 0, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0 || radius < 0.5f)
            return;
        if (num_segments <= 0)
        {
            _PathArcToFastEx(center, radius - 0.5f, 0, ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX, 0);
            _Path.Size--;
        }
        else
        {
            num_segments = ImMath.ImClamp(num_segments, 3, ImDrawListConst.IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_MAX);
            float a_max = (ImMath.IM_PI * 2.0f) * ((float)num_segments - 1.0f) / (float)num_segments;
            PathArcTo(center, radius - 0.5f, 0.0f, a_max, num_segments - 1);
        }
        PathStroke(col, thickness, ImDrawFlags_.ImDrawFlags_Closed);
    }

    public void AddCircleFilled(ImVec2 center, float radius, ImU32 col, int num_segments = 0)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0 || radius < 0.5f)
            return;
        if (num_segments <= 0)
        {
            _PathArcToFastEx(center, radius, 0, ImDrawListConst.IM_DRAWLIST_ARCFAST_SAMPLE_MAX, 0);
            _Path.Size--;
        }
        else
        {
            num_segments = ImMath.ImClamp(num_segments, 3, ImDrawListConst.IM_DRAWLIST_CIRCLE_AUTO_SEGMENT_MAX);
            float a_max = (ImMath.IM_PI * 2.0f) * ((float)num_segments - 1.0f) / (float)num_segments;
            PathArcTo(center, radius, 0.0f, a_max, num_segments - 1);
        }
        PathFillConvex(col);
    }

    public void AddNgon(ImVec2 center, float radius, ImU32 col, int num_segments, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0 || num_segments <= 2)
            return;
        float a_max = (ImMath.IM_PI * 2.0f) * ((float)num_segments - 1.0f) / (float)num_segments;
        PathArcTo(center, radius - 0.5f, 0.0f, a_max, num_segments - 1);
        PathStroke(col, thickness, ImDrawFlags_.ImDrawFlags_Closed);
    }

    public void AddNgonFilled(ImVec2 center, float radius, ImU32 col, int num_segments)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0 || num_segments <= 2)
            return;
        float a_max = (ImMath.IM_PI * 2.0f) * ((float)num_segments - 1.0f) / (float)num_segments;
        PathArcTo(center, radius, 0.0f, a_max, num_segments - 1);
        PathFillConvex(col);
    }

    public void AddEllipse(ImVec2 center, ImVec2 radius, ImU32 col, float rot = 0.0f, int num_segments = 0, float thickness = 1.0f)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        if (num_segments <= 0)
            num_segments = _CalcCircleAutoSegmentCount(ImMath.ImMax(radius.x, radius.y));
        float a_max = ImMath.IM_PI * 2.0f * ((float)num_segments - 1.0f) / (float)num_segments;
        PathEllipticalArcTo(center, radius, rot, 0.0f, a_max, num_segments - 1);
        PathStroke(col, thickness, ImDrawFlags_.ImDrawFlags_Closed);
    }

    public void AddEllipseFilled(ImVec2 center, ImVec2 radius, ImU32 col, float rot = 0.0f, int num_segments = 0)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        if (num_segments <= 0)
            num_segments = _CalcCircleAutoSegmentCount(ImMath.ImMax(radius.x, radius.y));
        float a_max = ImMath.IM_PI * 2.0f * ((float)num_segments - 1.0f) / (float)num_segments;
        PathEllipticalArcTo(center, radius, rot, 0.0f, a_max, num_segments - 1);
        PathFillConvex(col);
    }

    public void AddBezierCubic(ImVec2 p1, ImVec2 p2, ImVec2 p3, ImVec2 p4, ImU32 col, float thickness = 1.0f, int num_segments = 0)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        PathLineTo(p1);
        PathBezierCubicCurveTo(p2, p3, p4, num_segments);
        PathStroke(col, thickness);
    }

    public void AddBezierQuadratic(ImVec2 p1, ImVec2 p2, ImVec2 p3, ImU32 col, float thickness = 1.0f, int num_segments = 0)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        PathLineTo(p1);
        PathBezierQuadraticCurveTo(p2, p3, num_segments);
        PathStroke(col, thickness);
    }

    public void AddText(ImFont font, float font_size, ImVec2 pos, ImU32 col, string text, int text_begin = 0, int text_end = -1, float wrap_width = 0.0f, ImVec4? cpu_fine_clip_rect = null)
        => AddText(font, font_size, pos, col, text.AsSpan(), text_begin, text_end, wrap_width, cpu_fine_clip_rect);

    public void AddText(ImFont font, float font_size, ImVec2 pos, ImU32 col, ReadOnlySpan<char> text, int text_begin = 0, int text_end = -1, float wrap_width = 0.0f, ImVec4? cpu_fine_clip_rect = null)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        if (text.Length == 0 || text_begin >= (text_end < 0 ? text.Length : text_end))
            return;
        if (font == null)
            font = _Data.Font;
        if (font_size == 0.0f)
            font_size = _Data.FontSize;

        ImVec4 clip_rect = _CmdHeader.ClipRect;
        if (cpu_fine_clip_rect.HasValue)
        {
            var cr = cpu_fine_clip_rect.Value;
            clip_rect.x = ImMath.ImMax(clip_rect.x, cr.x);
            clip_rect.y = ImMath.ImMax(clip_rect.y, cr.y);
            clip_rect.z = ImMath.ImMin(clip_rect.z, cr.z);
            clip_rect.w = ImMath.ImMin(clip_rect.w, cr.w);
        }
        font.RenderText(this, font_size, pos, col, clip_rect, text, text_begin, text_end, wrap_width, cpu_fine_clip_rect.HasValue ? ImDrawTextFlags_.ImDrawTextFlags_CpuFineClip : ImDrawTextFlags_.ImDrawTextFlags_None);
    }

    public void AddText(ImVec2 pos, ImU32 col, string text, int text_begin = 0, int text_end = -1)
        => AddText(_Data.Font, _Data.FontSize, pos, col, text.AsSpan(), text_begin, text_end);

    public void AddText(ImVec2 pos, ImU32 col, ReadOnlySpan<char> text, int text_begin = 0, int text_end = -1)
        => AddText(_Data.Font, _Data.FontSize, pos, col, text, text_begin, text_end);

    public void AddImage(ImTextureRef tex_ref, ImVec2 p_min, ImVec2 p_max, ImVec2? uv_min = null, ImVec2? uv_max = null, ImU32 col = 0xFFFFFFFF)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        var uv0 = uv_min ?? new ImVec2(0, 0);
        var uv1 = uv_max ?? new ImVec2(1, 1);
        bool push_texture_id = !tex_ref.Equals(_CmdHeader.TexRef);
        if (push_texture_id)
            PushTexture(tex_ref);
        PrimReserve(6, 4);
        PrimRectUV(p_min, p_max, uv0, uv1, col);
        if (push_texture_id)
            PopTexture();
    }

    public void AddImageQuad(ImTextureRef tex_ref, ImVec2 p1, ImVec2 p2, ImVec2 p3, ImVec2 p4, ImVec2? uv1 = null, ImVec2? uv2 = null, ImVec2? uv3 = null, ImVec2? uv4 = null, ImU32 col = 0xFFFFFFFF)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        var u1 = uv1 ?? new ImVec2(0, 0); var u2 = uv2 ?? new ImVec2(1, 0); var u3 = uv3 ?? new ImVec2(1, 1); var u4 = uv4 ?? new ImVec2(0, 1);
        bool push_texture_id = !tex_ref.Equals(_CmdHeader.TexRef);
        if (push_texture_id)
            PushTexture(tex_ref);
        PrimReserve(6, 4);
        PrimQuadUV(p1, p2, p3, p4, u1, u2, u3, u4, col);
        if (push_texture_id)
            PopTexture();
    }

    public void AddImageRounded(ImTextureRef tex_ref, ImVec2 p_min, ImVec2 p_max, ImVec2 uv_min, ImVec2 uv_max, ImU32 col, float rounding, ImDrawFlags flags = 0)
    {
        if ((col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;
        if ((flags & ImDrawFlags_.ImDrawFlags_RoundCornersMask_) == 0)
            flags |= ImDrawFlags_.ImDrawFlags_RoundCornersAll;
        if (rounding < 0.5f || (flags & ImDrawFlags_.ImDrawFlags_RoundCornersMask_) == ImDrawFlags_.ImDrawFlags_RoundCornersNone)
        {
            AddImage(tex_ref, p_min, p_max, uv_min, uv_max, col);
            return;
        }
        bool push_texture_id = !tex_ref.Equals(_CmdHeader.TexRef);
        if (push_texture_id)
            PushTexture(tex_ref);
        int vert_start_idx = VtxBuffer.Size;
        PathRect(p_min, p_max, rounding, flags);
        PathFillConvex(col);
        int vert_end_idx = VtxBuffer.Size;
        ImGuiCore.ShadeVertsLinearUV(this, vert_start_idx, vert_end_idx, p_min, p_max, uv_min, uv_max, true);
        if (push_texture_id)
            PopTexture();
    }

    public void AddConcavePolyFilled(ImVec2[] points, int points_count, ImU32 col)
    {
        // Port of ImTriangulator-based concave polygon fill.
        if (points_count < 3 || (col & ImGuiCore.IM_COL32_A_MASK) == 0)
            return;

        var uv = _Data.TexUvWhitePixel;
        var triangulator = new ImTriangulator(points, points_count);
        uint[] triangle = new uint[3];
        if ((Flags & ImDrawListFlags_.ImDrawListFlags_AntiAliasedFill) != 0)
        {
            // Anti-aliased Fill
            float AA_SIZE = _FringeScale;
            ImU32 col_trans = col & ~ImGuiCore.IM_COL32_A_MASK;
            int idx_count = (points_count - 2) * 3 + points_count * 6;
            int vtx_count = (points_count * 2);
            PrimReserve(idx_count, vtx_count);

            uint vtx_inner_idx = _VtxCurrentIdx;
            uint vtx_outer_idx = _VtxCurrentIdx + 1;

            while (triangulator._TrianglesLeft > 0)
            {
                triangulator.GetNextTriangle(triangle);
                var ib = IdxBuffer.Data; int ip = _IdxWritePos;
                ib[ip + 0] = (ImDrawIdx)(vtx_inner_idx + (triangle[0] << 1));
                ib[ip + 1] = (ImDrawIdx)(vtx_inner_idx + (triangle[1] << 1));
                ib[ip + 2] = (ImDrawIdx)(vtx_inner_idx + (triangle[2] << 1));
                _IdxWritePos += 3;
            }

            // Compute normals
            var temp_normals = new ImVec2[points_count];
            for (int i0 = points_count - 1, i1 = 0; i1 < points_count; i0 = i1++)
            {
                var p0 = points[i0];
                var p1 = points[i1];
                float dx = p1.x - p0.x;
                float dy = p1.y - p0.y;
                Normalize2FOverZero(ref dx, ref dy);
                temp_normals[i0] = new ImVec2(dy, -dx);
            }

            var vb = VtxBuffer.Data; int vp = _VtxWritePos;
            var ibf = IdxBuffer.Data; int ipf = _IdxWritePos;
            for (int i0 = points_count - 1, i1 = 0; i1 < points_count; i0 = i1++)
            {
                var n0 = temp_normals[i0];
                var n1 = temp_normals[i1];
                float dm_x = (n0.x + n1.x) * 0.5f;
                float dm_y = (n0.y + n1.y) * 0.5f;
                FixNormal2F(ref dm_x, ref dm_y);
                dm_x *= AA_SIZE * 0.5f;
                dm_y *= AA_SIZE * 0.5f;

                vb[vp + 0].pos = new ImVec2(points[i1].x - dm_x, points[i1].y - dm_y); vb[vp + 0].uv = uv; vb[vp + 0].col = col;
                vb[vp + 1].pos = new ImVec2(points[i1].x + dm_x, points[i1].y + dm_y); vb[vp + 1].uv = uv; vb[vp + 1].col = col_trans;
                vp += 2;

                ibf[ipf + 0] = (ImDrawIdx)(vtx_inner_idx + ((uint)i1 << 1)); ibf[ipf + 1] = (ImDrawIdx)(vtx_inner_idx + ((uint)i0 << 1)); ibf[ipf + 2] = (ImDrawIdx)(vtx_outer_idx + ((uint)i0 << 1));
                ibf[ipf + 3] = (ImDrawIdx)(vtx_outer_idx + ((uint)i0 << 1)); ibf[ipf + 4] = (ImDrawIdx)(vtx_outer_idx + ((uint)i1 << 1)); ibf[ipf + 5] = (ImDrawIdx)(vtx_inner_idx + ((uint)i1 << 1));
                ipf += 6;
            }
            _VtxWritePos = vp;
            _IdxWritePos = ipf;
            _VtxCurrentIdx += (uint)vtx_count;
        }
        else
        {
            // Non Anti-aliased Fill
            int idx_count = (points_count - 2) * 3;
            int vtx_count = points_count;
            PrimReserve(idx_count, vtx_count);
            var vb = VtxBuffer.Data; int vp = _VtxWritePos;
            for (int i = 0; i < vtx_count; i++)
            {
                vb[vp].pos = points[i]; vb[vp].uv = uv; vb[vp].col = col;
                vp++;
            }
            _VtxWritePos = vp;
            var ib = IdxBuffer.Data; int ip = _IdxWritePos;
            while (triangulator._TrianglesLeft > 0)
            {
                triangulator.GetNextTriangle(triangle);
                ib[ip + 0] = (ImDrawIdx)(_VtxCurrentIdx + triangle[0]);
                ib[ip + 1] = (ImDrawIdx)(_VtxCurrentIdx + triangle[1]);
                ib[ip + 2] = (ImDrawIdx)(_VtxCurrentIdx + triangle[2]);
                ip += 3;
            }
            _IdxWritePos = ip;
            _VtxCurrentIdx += (uint)vtx_count;
        }
    }
}
