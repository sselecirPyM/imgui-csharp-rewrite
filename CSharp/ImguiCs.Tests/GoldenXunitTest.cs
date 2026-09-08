// xUnit wrapper around the shared golden harness (GoldenTest.cs).
using System;
using System.Text;
using Xunit;

namespace ImGui.Tests;

public class GoldenXunitTest
{
    public static TheoryData<string> ScenarioNames
    {
        get
        {
            var d = new TheoryData<string>();
            foreach (var (name, _) in GoldenTest.Scenarios)
                d.Add(name);
            return d;
        }
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Golden_matches_Cpp_reference(string name)
    {
        var found = Array.Find(GoldenTest.Scenarios, s => s.name == name);
        string actual = GoldenTest.RunScenarioPublic(found.name, found.fn);
        GoldenTest.WriteActual(found.name, actual);

        string expected_path = System.IO.Path.Combine(GoldenTest.GoldenDirPublic, found.name + ".txt");
        Assert.True(System.IO.File.Exists(expected_path), $"Missing golden data: {expected_path}");
        string expected = System.IO.File.ReadAllText(expected_path).Replace("\r\n", "\n");

        string[] exp_lines = expected.Split('\n');
        string[] act_lines = actual.Split('\n');
        int n = Math.Max(exp_lines.Length, act_lines.Length);
        var sb = new StringBuilder();
        int diffs = 0;
        for (int i = 0; i < n; i++)
        {
            string e = i < exp_lines.Length ? exp_lines[i] : "<eof>";
            string a = i < act_lines.Length ? act_lines[i] : "<eof>";
            if (e != a)
            {
                if (diffs < 40)
                    sb.Append("line ").Append(i + 1).Append(":\n  exp: ").Append(e).Append("\n  act: ").Append(a).Append('\n');
                diffs++;
            }
        }
        Assert.True(diffs == 0, $"{found.name}: {diffs} differing lines\n{sb}");
    }
}
