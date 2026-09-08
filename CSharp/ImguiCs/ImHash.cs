// Port of Dear ImGui hashing (imgui.cpp): CRC32c-based ImHashData / ImHashStr.
using System;
using System.Text;

namespace ImGui;

public static unsafe partial class ImGuiHash
{
    // CRC32c table compatible with SSE 4.2 instructions (see ImCrc32Table.gen.cs)
    // GCrc32LookupTable

    public static uint ImHashData(byte[] data, int offset, ulong data_size, uint seed = 0)
    {
        uint crc = ~seed;
        int p = offset;
        int end = offset + (int)data_size;
        uint[] lut = GCrc32LookupTable;
        while (p < end)
            crc = (crc >> 8) ^ lut[(crc & 0xFF) ^ data[p++]];
        return ~crc;
    }

    // Hash a C# string's UTF-8 representation. Mirrors ImHashStr(NULL-terminated path).
    // - 'data_size' 0 => read until NUL (C# strings have no NUL: hash whole string).
    // - Returns ~crc.
    public static uint ImHashStr(string data, ulong data_size, uint seed = 0)
    {
        if (data == null)
            return ImHashStrUtf8(null, 0, 0, seed);
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        return ImHashData(bytes, 0, data_size == 0 ? (ulong)bytes.Length : Math.Min((ulong)bytes.Length, data_size), seed);
    }

    // ImHashSkipUncontributingPrefix (imgui.cpp): skip past '###' contrib-marker section of a label.
    public static string ImHashSkipUncontributingPrefix(string label)
    {
        int result = 0;
        for (int i = 0; i < label.Length; i++)
            if (label[i] == '#' && i + 2 < label.Length && label[i + 1] == '#' && label[i + 2] == '#')
                result = i + 3;
        return label.Substring(result);
    }

    public static uint ImHashStrUtf8(byte[] bytes, int offset, ulong data_size, uint seed = 0)
    {
        seed = ~seed;
        uint crc = seed;
        uint[] lut = GCrc32LookupTable;
        if (bytes != null)
        {
            if (data_size != 0)
            {
                int p = offset;
                long remaining = (long)data_size;
                while (remaining-- > 0)
                {
                    byte c = bytes[p++];
                    if (c == '#' && remaining >= 2 && bytes[p] == '#' && bytes[p + 1] == '#')
                    {
                        crc = seed;
                        p += 2;
                        remaining -= 2;
                        continue;
                    }
                    crc = (crc >> 8) ^ lut[(crc & 0xFF) ^ c];
                }
            }
            else
            {
                // NUL-terminated variant not used with managed arrays; hash entire array.
                int p = offset;
                while (p < bytes.Length)
                {
                    byte c = bytes[p++];
                    if (c == '#' && p + 1 < bytes.Length && bytes[p] == '#' && bytes[p + 1] == '#')
                    {
                        crc = seed;
                        p += 2;
                        continue;
                    }
                    crc = (crc >> 8) ^ lut[(crc & 0xFF) ^ c];
                }
            }
        }
        return ~crc;
    }
}
