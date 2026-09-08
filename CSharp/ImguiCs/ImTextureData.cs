// Port of Dear ImGui texture API (imgui.h [SECTION] Texture API) + ImVec2i helper.
using System;

namespace ImGui;

public struct ImVec2i
{
    public int x, y;
    public ImVec2i(int _x, int _y) { x = _x; y = _y; }
}

public static partial class ImUtil
{
    public const ImTextureID ImTextureID_Invalid = 0;

    public static int ImTextureDataGetFormatBytesPerPixel(int format) => format == ImTextureFormat_.ImTextureFormat_RGBA32 ? 4 : 1;

    public static string ImTextureDataGetStatusName(int status)
    {
        switch (status)
        {
            case ImTextureStatus_.ImTextureStatus_OK: return "OK";
            case ImTextureStatus_.ImTextureStatus_Destroyed: return "Destroyed";
            case ImTextureStatus_.ImTextureStatus_WantCreate: return "WantCreate";
            case ImTextureStatus_.ImTextureStatus_WantUpdates: return "WantUpdates";
            case ImTextureStatus_.ImTextureStatus_WantDestroy: return "WantDestroy";
        }
        return "N/A";
    }

    public static string ImTextureDataGetFormatName(int format)
    {
        switch (format)
        {
            case ImTextureFormat_.ImTextureFormat_Alpha8: return "Alpha8";
            case ImTextureFormat_.ImTextureFormat_RGBA32: return "RGBA32";
        }
        return "N/A";
    }

    // Port of ImTextureDataUpdateNewFrame (imgui_draw.cpp)
    public static bool ImTextureDataUpdateNewFrame(ImTextureData tex)
    {
        bool remove_from_list = false;
        if (tex.Status == ImTextureStatus_.ImTextureStatus_OK)
        {
            tex.Updates.resize(0);
            tex.UpdateRect = new ImTextureRect(0xffff, 0xffff, 0, 0);
        }

        if (tex.WantDestroyNextFrame && tex.Status != ImTextureStatus_.ImTextureStatus_Destroyed && tex.Status != ImTextureStatus_.ImTextureStatus_WantDestroy)
            tex.Status = ImTextureStatus_.ImTextureStatus_WantDestroy;

        if (tex.Status == ImTextureStatus_.ImTextureStatus_WantDestroy && tex.TexID == ImUtil.ImTextureID_Invalid && tex.BackendUserData == null && tex.QueueUserData == null)
            tex.Status = ImTextureStatus_.ImTextureStatus_Destroyed;

        if (tex.Status == ImTextureStatus_.ImTextureStatus_Destroyed)
        {
            if (tex.WantDestroyNextFrame)
                remove_from_list = true;
            else
                tex.Status = ImTextureStatus_.ImTextureStatus_WantCreate;
        }

        if (tex.Status == ImTextureStatus_.ImTextureStatus_WantDestroy)
            tex.UnusedFrames++;

        return remove_from_list;
    }

    // Queue texture block update for renderer backend (imgui_draw.cpp)
    public static void ImTextureDataQueueUpload(ImTextureData tex, int x, int y, int w, int h)
    {
        var req = new ImTextureRect(x, y, w, h);
        int new_x1 = Math.Max(tex.UpdateRect.w == 0 ? 0 : tex.UpdateRect.x + tex.UpdateRect.w, req.x + req.w);
        int new_y1 = Math.Max(tex.UpdateRect.h == 0 ? 0 : tex.UpdateRect.y + tex.UpdateRect.h, req.y + req.h);
        int urx = Math.Min(tex.UpdateRect.x, req.x);
        int ury = Math.Min(tex.UpdateRect.y, req.y);
        tex.UpdateRect = new ImTextureRect(urx, ury, new_x1 - urx, new_y1 - ury);
        {
            int ux = Math.Min(tex.UsedRect.x, req.x);
            int uy = Math.Min(tex.UsedRect.y, req.y);
            int uw = Math.Max(tex.UsedRect.x + tex.UsedRect.w, req.x + req.w) - ux;
            int uh = Math.Max(tex.UsedRect.y + tex.UsedRect.h, req.y + req.h) - uy;
            tex.UsedRect = new ImTextureRect(ux, uy, uw, uh);
        }

        if (tex.Status == ImTextureStatus_.ImTextureStatus_OK || tex.Status == ImTextureStatus_.ImTextureStatus_WantUpdates)
        {
            tex.Status = ImTextureStatus_.ImTextureStatus_WantUpdates;
            tex.Updates.push_back(req);
        }
    }
}

// ImTextureRef: higher-level identifier for a texture. Store a ImTextureID _or_ a ImTextureData*.
public struct ImTextureRef
{
    public ImTextureData _TexData;      // A texture, generally owned by a ImFontAtlas
    public ImTextureID _TexID;          // _OR_ low-level backend texture identifier

    public ImTextureRef(ImTextureID tex_id) { _TexData = null; _TexID = tex_id; }
    public static implicit operator ImTextureRef(ImTextureID tex_id) => new ImTextureRef(tex_id);

    public ImTextureID GetTexID() => (_TexData != null) ? _TexData.TexID : _TexID;

    public bool Equals(ImTextureRef rhs) => _TexID == rhs._TexID && _TexData == rhs._TexData;
    public override bool Equals(object o) => o is ImTextureRef r && Equals(r);
    public override int GetHashCode() => _TexID.GetHashCode() ^ (_TexData != null ? _TexData.GetHashCode() : 0);
    public static bool operator ==(ImTextureRef l, ImTextureRef r) => l.Equals(r);
    public static bool operator !=(ImTextureRef l, ImTextureRef r) => !l.Equals(r);
}

// ImTextureRect: coordinates of a rectangle within a texture
public struct ImTextureRect
{
    public ushort x, y;
    public ushort w, h;
    public ImTextureRect(int _x, int _y, int _w, int _h) { x = (ushort)_x; y = (ushort)_y; w = (ushort)_w; h = (ushort)_h; }
}

// ImTextureData: specs and pixel storage for a texture used by Dear ImGui.
public class ImTextureData
{
    public int UniqueID;
    public int Status;                  // ImTextureStatus_ values
    public object BackendUserData;
    public object QueueUserData;
    public ImTextureID TexID;
    public int Format;                  // ImTextureFormat_ values
    public int Width, Height;
    public int BytesPerPixel;
    public byte[] Pixels;
    public ImTextureRect UsedRect;
    public ImTextureRect UpdateRect;
    public ImVector<ImTextureRect> Updates = new ImVector<ImTextureRect>();
    public int UnusedFrames;
    public ushort RefCount;
    public bool UseColors;
    public bool WantDestroyNextFrame;

    // C++: ImTextureData() { memset((void*)this, 0, sizeof(*this)); Status = ImTextureStatus_Destroyed; TexID = ImTextureID_Invalid; }
    public ImTextureData()
    {
        Status = ImTextureStatus_.ImTextureStatus_Destroyed;
        TexID = ImUtil.ImTextureID_Invalid;
    }

    public void Create(int format, int w, int h)
    {
        System.Diagnostics.Debug.Assert(Status == ImTextureStatus_.ImTextureStatus_Destroyed);
        DestroyPixels();
        Format = format;
        Status = ImTextureStatus_.ImTextureStatus_WantCreate;
        Width = w;
        Height = h;
        BytesPerPixel = ImUtil.ImTextureDataGetFormatBytesPerPixel(format);
        UseColors = false;
        Pixels = new byte[Width * Height * BytesPerPixel];
        UsedRect = new ImTextureRect(0, 0, 0, 0);
        UpdateRect = new ImTextureRect(0xffff, 0xffff, 0, 0);
    }

    public void DestroyPixels()
    {
        Pixels = null;
        UseColors = false;
    }

    public int GetPitch() => Width * BytesPerPixel;
    public int GetSizeInBytes() => Width * Height * BytesPerPixel;
    public int GetPixelsAt(int x, int y) => (x + y * Width) * BytesPerPixel; // byte offset
    public ImTextureRef GetTexRef() { var r = new ImTextureRef(); r._TexData = this; r._TexID = ImUtil.ImTextureID_Invalid; return r; }
    public ImTextureID GetTexID() => TexID;
    public void SetTexID(ImTextureID id) => TexID = id;
    public void SetStatus(int status) => Status = status;
}
