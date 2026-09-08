// Parity tests: ImFmt.FormatInto (non-allocating generic core) must produce
// byte-identical output to the original Sprintf for every conversion/flag combo.
using System;
using System.Globalization;
using Xunit;

namespace ImGui.Tests;

public class ImFmtParityXunitTest
{
    [Fact]
    public void FormatInto_matches_Sprintf()
    {
        Assert.Equal(0, ImFmtParityTest.RunAll());
    }
}

public static class ImFmtParityTest
{
    static int _failed;

    static void Check(string fmt, params object[] args)
    {
        string expected = ImFmt.Sprintf(fmt, args);

        Span<char> buf = stackalloc char[512];
        int n = FormatDispatch(buf, fmt, args);
        string actual = n > buf.Length
            ? new string(ImFmt.RentScratch(n), 0, n)
            : new string(buf.Slice(0, n));

        if (expected != actual)
        {
            _failed++;
            Console.WriteLine($"FAIL {fmt}: Sprintf='{expected}' FormatInto='{actual}'");
        }
    }

    // routes through the 1..4-arity FormatInto overloads with typed args
    static int FormatDispatch(Span<char> buf, string fmt, object[] args)
    {
        object a0 = args.Length > 0 ? args[0] : null;
        object a1 = args.Length > 1 ? args[1] : null;
        object a2 = args.Length > 2 ? args[2] : null;
        object a3 = args.Length > 3 ? args[3] : null;
        return args.Length switch
        {
            0 => ImFmt.FormatInto(buf, fmt),
            1 => ImFmt.FormatInto(buf, fmt, a0),
            2 => ImFmt.FormatInto(buf, fmt, a0, a1),
            3 => ImFmt.FormatInto(buf, fmt, a0, a1, a2),
            _ => ImFmt.FormatInto(buf, fmt, a0, a1, a2, a3),
        };
    }

    public static int RunAll()
    {
        _failed = 0;
        // plain text / escapes
        Check("hello");
        Check("100%%");
        Check("a%db %dc", 5, -42);
        // %d/%i
        Check("%d", 0);
        Check("%d", int.MaxValue);
        Check("%d", int.MinValue);
        Check("%d", 42L);
        Check("%d %d %d", 1, -2, 3);
        Check("[%5d]", 42);
        Check("[%-5d]", 42);
        Check("[%05d]", -42);
        Check("[%+.0d]", 0);
        Check("[% .0d]", 0);
        Check("[%.5d]", 42);
        Check("[%.0d]", 0);
        Check("[%8.5d]", 42);
        // %u / %x / %X / %o
        Check("%u", 4294967295u);
        Check("%u", 0u);
        Check("[%.0u]", 0u);
        Check("%x", 0xdeadbeefu);
        Check("%X", 0xdeadbeefu);
        Check("[%#x]", 255u);
        Check("[%#X]", 255u);
        Check("[%#08x]", 255u);
        Check("[%#.0x]", 255u);
        Check("[%#.0x]", 0u);
        Check("[%8.4x]", 0xabu);
        Check("%o", 64u);
        Check("%o", unchecked((uint)-1));
        // %f / %e / %g
        Check("%f", 1.5);
        Check("%f", 1.5f);
        Check("%.0f", 0.5); // banker's rounding
        Check("%.1f", -0.05);
        Check("%.3f", 1234.5678);
        Check("%.10f", 1.0 / 3.0);
        Check("%+.2f", 3.14159);
        Check("% .2f", 3.14159);
        Check("%8.2f", 3.14159);
        Check("%-8.2f|", 3.14159);
        Check("%08.2f", -3.14159);
        Check("%.300f", 1e10);
        Check("%f", 1e300);
        Check("%f", double.NaN);
        Check("%f", double.PositiveInfinity);
        Check("%f", double.NegativeInfinity);
        Check("%e", 12345.6789);
        Check("%E", 12345.6789);
        Check("%.2e", 0.000123);
        Check("%+.1e", 9.99);
        Check("%.0e", 9.99);
        Check("%g", 123456789.0);
        Check("%G", 0.000123456);
        Check("%g", 0.5);
        Check("%g", 100000.0);
        Check("%g", 1000000.0);
        Check("%.3g", 0.0001);
        Check("%.3g", 0.00001);
        Check("%.10g", 1.0 / 3.0);
        Check("%g", 0.0);
        Check("%g", -0.0);
        Check("%+g", -0.0);
        Check("%.0e", 0.0);
        Check("%.0e", -0.0);
        Check("%e", double.MaxValue);
        Check("%.15e", double.Epsilon);
        // %s / %c / %p
        Check("%s", "hello");
        Check("[%8s]", "ab");
        Check("[%-8s]", "ab");
        Check("[%.2s]", "abcdef");
        Check("%s", (string)null); // "(null)"
        Check("%c", 'A');
        Check("[%4c]", 'B');
        Check("%p", new ImFmt.pointer_t(0x1234abcd));
        // combos through one format string (the 4-slot generic core covers arities 0..4;
        // Sprintf keeps supporting arbitrary arg counts)
        Check("Pos (%.1f, %.1f) id %d [%s]", 3.5f, -2.25f, 7, "tag");
        Check("%04d:%02d:%02d %.3f", 1, 2, 3, 4.5);
        Check("%5d|%-5d|%05d|%+d", 7, 7, 7, 7);
        Check("%x %X %u %o", 48879u, 48879u, 4000000000u, 511u);
        Check("%d %f %e %g", -11, 2.5, 2500.0, 0.25);
        Check("%s %c %%", "mix", 'Z');

        Console.WriteLine(_failed == 0 ? $"ImFmt parity: all passed" : $"ImFmt parity: {_failed} FAILURES");
        return _failed;
    }
}
