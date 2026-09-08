// Regression tests: ImGuiTextBuffer.append(string, str_end) must treat str_end
// as a UTF-16 CHAR count (what every C# caller passes, mirroring C++ strlen+1),
// not a UTF-8 byte count — byte-count handling truncated multi-byte entries
// (table->ColumnsNames / tab_bar->TabsNames), so CJK table headers rendered '?'.
using System.Text;
using Xunit;

namespace ImGui.Tests;

public class ImGuiTextBufferTest
{
    static string ReadEntry(ImGuiTextBuffer b, int off)
    {
        int end = off;
        while (b.Buf.Data[end] != 0)
            end++;
        return Encoding.UTF8.GetString(b.Buf.Data, off, end - off);
    }

    [Fact]
    public void Append_str_end_is_char_count_not_byte_count()
    {
        var buf = new ImGuiTextBuffer();
        buf.append("设备名称", "设备名称".Length + 1); // C++ strlen+1 pattern

        byte[] utf8 = Encoding.UTF8.GetBytes("设备名称");
        Assert.Equal(utf8.Length + 2, buf.Buf.Size); // payload + emulated NUL + trailing NUL
        for (int i = 0; i < utf8.Length; i++)
            Assert.Equal(utf8[i], buf.Buf.Data[i]);
        Assert.Equal(0, buf.Buf.Data[utf8.Length]);
    }

    [Fact]
    public void Append_nul_separates_multi_byte_entries()
    {
        var buf = new ImGuiTextBuffer();
        buf.append("设备名称", "设备名称".Length + 1);
        int off2 = buf.size(); // offset where the next append lands
        buf.append("温度℃", "温度℃".Length + 1);

        Assert.Equal("设备名称", ReadEntry(buf, 0));
        Assert.Equal("温度℃", ReadEntry(buf, off2));
    }

    [Fact]
    public void Append_ascii_layout_unchanged()
    {
        var buf = new ImGuiTextBuffer();
        buf.append("A", 2);
        Assert.Equal(3, buf.Buf.Size);
        Assert.Equal(0x41, buf.Buf.Data[0]);
        Assert.Equal(0, buf.Buf.Data[1]);
        Assert.Equal(0, buf.Buf.Data[2]);

        var buf2 = new ImGuiTextBuffer();
        buf2.append("A");
        Assert.Equal(2, buf2.Buf.Size);
        Assert.Equal(0x41, buf2.Buf.Data[0]);
        Assert.Equal(0, buf2.Buf.Data[1]);
    }

    [Fact]
    public void TableSetupColumn_cjk_names_roundtrip()
    {
        ImGui.CreateContext();
        ImGuiIO io = ImGui.GetIO();
        io.DisplaySize = new ImVec2(1280.0f, 720.0f);
        io.DeltaTime = 1.0f / 60.0f;
        io.IniFilename = null;
        io.Fonts.GetTexDataAsRGBA32(out byte[] px, out int tw, out int th, out int tbpp);
        io.Fonts.SetTexID(1);

        string n0 = null, n1 = null, n2 = null;
        ImGui.NewFrame();
        ImGui.Begin("测试窗口");
        if (ImGui.BeginTable("t", 3))
        {
            ImGui.TableSetupColumn("设备名称");
            ImGui.TableSetupColumn("数量##qty");
            ImGui.TableSetupColumn("IP地址");
            ImGui.TableHeadersRow();
            n0 = ImGui.TableGetColumnName(0);
            n1 = ImGui.TableGetColumnName(1);
            n2 = ImGui.TableGetColumnName(2);
            ImGui.EndTable();
        }
        ImGui.End();
        ImGui.Render();
        ImGui.DestroyContext();

        Assert.Equal("设备名称", n0);
        Assert.Equal("数量##qty", n1); // TableGetColumnName returns the raw stored name (## stripped only at render)
        Assert.Equal("IP地址", n2);
    }
}
