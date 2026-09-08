// C-compatible printf formatting used by ImFormatString/Text(fmt, ...) etc.
// %.Nf delegates to .NET invariant F formatting (verified bit-identical to msvcrt/glibc
// rounding incl. banker's rounding of exact halves); %e/%g implemented by hand since
// .NET G/E differ (case, exponent digits, significant-digit count).
using System;
using System.Globalization;
using System.Text;

namespace ImGui;

public static unsafe partial class ImFmt
{
    public static string Sprintf(string fmt, params object[] args)
    {
        var sb = new StringBuilder();
        int ai = 0;
        int i = 0, n = fmt.Length;
        while (i < n)
        {
            char c = fmt[i++];
            if (c != '%') { sb.Append(c); continue; }
            if (i < n && fmt[i] == '%') { sb.Append('%'); i++; continue; }

            // parse flags
            bool left = false, plus = false, space = false, zero = false, alt = false;
            while (i < n)
            {
                c = fmt[i];
                if (c == '-') left = true;
                else if (c == '+') plus = true;
                else if (c == ' ') space = true;
                else if (c == '0') zero = true;
                else if (c == '#') alt = true;
                else break;
                i++;
            }
            // width
            int width = 0;
            while (i < n && fmt[i] >= '0' && fmt[i] <= '9') { width = width * 10 + (fmt[i] - '0'); i++; }
            // precision
            int prec = -1;
            if (i < n && fmt[i] == '.')
            {
                i++;
                prec = 0;
                while (i < n && fmt[i] >= '0' && fmt[i] <= '9') { prec = prec * 10 + (fmt[i] - '0'); i++; }
            }
            // length modifiers (parsed, ignored: args are typed)
            while (i < n && "hljztL".IndexOf(fmt[i]) >= 0) i++;
            if (i >= n) break;
            char conv = fmt[i++];

            string s;
            switch (conv)
            {
                case 'd':
                case 'i':
                {
                    long v = ToLong(args, ai++);
                    s = System.Math.Abs(v).ToString(CultureInfo.InvariantCulture);
                    if (v == 0 && prec == 0) s = "";
                    else if (prec > s.Length) s = new string('0', prec - s.Length) + s;
                    string sign = v < 0 ? "-" : plus ? "+" : space ? " " : "";
                    s = sign + s;
                    break;
                }
                case 'u':
                {
                    ulong v = ToUlong(args, ai++);
                    s = v.ToString(CultureInfo.InvariantCulture);
                    if (v == 0 && prec == 0) s = "";
                    else if (prec > s.Length) s = new string('0', prec - s.Length) + s;
                    break;
                }
                case 'x':
                case 'X':
                {
                    ulong v = ToUlong(args, ai++);
                    s = conv == 'x' ? v.ToString("x", CultureInfo.InvariantCulture) : v.ToString("X", CultureInfo.InvariantCulture);
                    if (v == 0 && prec == 0) s = "";
                    else if (prec > s.Length) s = new string('0', prec - s.Length) + s;
                    if (alt && v != 0) s = (conv == 'x' ? "0x" : "0X") + s;
                    break;
                }
                case 'o':
                {
                    ulong v = ToUlong(args, ai++);
                    s = Convert.ToString((long)v, 8);
                    break;
                }
                case 'c':
                {
                    object o = args[ai++];
                    s = o is char ch ? ch.ToString() : ((char)(int)Convert.ChangeType(o, typeof(int))).ToString();
                    break;
                }
                case 's':
                {
                    object o = args[ai++];
                    s = o as string;
                    if (s == null) s = o == null ? "(null)" : o.ToString();
                    if (prec >= 0 && s.Length > prec) s = s.Substring(0, prec);
                    break;
                }
                case 'p':
                {
                    object o = args[ai++];
                    ulong v = o is pointer_t pt ? pt.value : Convert.ToUInt64(o);
                    s = "0x" + v.ToString("x", CultureInfo.InvariantCulture);
                    break;
                }
                case 'f':
                case 'F':
                {
                    double v = ToDouble(args, ai++);
                    if (prec < 0) prec = 6;
                    s = FormatF(v, prec);
                    if (v >= 0 && plus) s = "+" + s;
                    else if (v >= 0 && space) s = " " + s;
                    break;
                }
                case 'e':
                case 'E':
                {
                    double v = ToDouble(args, ai++);
                    if (prec < 0) prec = 6;
                    s = FormatE(v, prec, conv == 'E');
                    if (v >= 0 && plus) s = "+" + s;
                    else if (v >= 0 && space) s = " " + s;
                    break;
                }
                case 'g':
                case 'G':
                {
                    double v = ToDouble(args, ai++);
                    s = FormatG(v, prec < 0 ? 6 : (prec == 0 ? 1 : prec), conv == 'G');
                    if (v >= 0 && plus) s = "+" + s;
                    else if (v >= 0 && space) s = " " + s;
                    break;
                }
                case 'v': // imgui extension? not supported: format as float
                {
                    double v = ToDouble(args, ai++);
                    s = FormatF(v, 6);
                    break;
                }
                default:
                    s = conv.ToString();
                    break;
            }

            // apply width
            if (width > s.Length)
            {
                if (left) s = s.PadRight(width);
                else if (zero && "difeEgGuXxo".IndexOf(conv) >= 0 && conv != 's')
                {
                    int sign_len = (s.Length > 0 && (s[0] == '-' || s[0] == '+' || s[0] == ' ')) ? 1 : 0;
                    string head = s.Length >= 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X') ? s.Substring(0, 2) : null;
                    string digits = head != null ? s.Substring(2) : s.Substring(sign_len);
                    s = (head ?? (sign_len > 0 ? s.Substring(0, 1) : "")) + new string('0', width - s.Length + (head != null ? head.Length : sign_len)) + digits;
                }
                else s = s.PadLeft(width);
            }
            sb.Append(s);
        }
        return sb.ToString();
    }

    // marker type for %p
    public struct pointer_t { public ulong value; public pointer_t(ulong v) { value = v; } }

    static long ToLong(object[] args, int i)
    {
        object o = args[i];
        if (o is int ii) return ii;
        if (o is long ll) return ll;
        if (o is uint u) return u;
        if (o is short sh) return sh;
        if (o is ushort us) return us;
        if (o is byte b) return b;
        if (o is sbyte sb) return sb;
        if (o is bool bo) return bo ? 1 : 0;
        if (o is char c) return c;
        return Convert.ToInt64(o);
    }
    static ulong ToUlong(object[] args, int i)
    {
        object o = args[i];
        if (o is uint u) return u;
        if (o is int ii) return unchecked((ulong)(long)ii);
        if (o is ulong ul) return ul;
        if (o is long ll) return unchecked((ulong)ll);
        return Convert.ToUInt64(o);
    }
    static double ToDouble(object[] args, int i)
    {
        object o = args[i];
        if (o is float f) return (double)f; // C varargs promote float -> double
        if (o is double d) return d;
        if (o is decimal m) return (double)m;
        return Convert.ToDouble(o);
    }

    public static string FormatF(double v, int prec)
    {
        if (double.IsNaN(v)) return "nan";
        if (double.IsPositiveInfinity(v)) return "inf";
        if (double.IsNegativeInfinity(v)) return "-inf";
        return v.ToString("F" + prec, CultureInfo.InvariantCulture);
    }

    public static string FormatE(double v, int prec, bool upper)
    {
        if (double.IsNaN(v)) return "nan";
        if (double.IsPositiveInfinity(v)) return upper ? "INF" : "inf";
        if (double.IsNegativeInfinity(v)) return upper ? "-INF" : "-inf";
        string s = v.ToString("E" + prec, CultureInfo.InvariantCulture); // e.g. "1.230000E-005"
        int ei = s.IndexOf('E');
        string mant = s.Substring(0, ei);
        int exp = int.Parse(s.Substring(ei + 1), CultureInfo.InvariantCulture);
        string esign = exp < 0 ? "-" : "+";
        int eabs = System.Math.Abs(exp);
        string estr = eabs.ToString("D2", CultureInfo.InvariantCulture); // C: at least 2 exponent digits
        return (upper ? mant.ToUpperInvariant() : mant.ToLowerInvariant()) + (upper ? "E" : "e") + esign + estr;
    }

    public static string FormatG(double v, int prec, bool upper)
    {
        if (double.IsNaN(v)) return "nan";
        if (double.IsPositiveInfinity(v)) return upper ? "INF" : "inf";
        if (double.IsNegativeInfinity(v)) return upper ? "-INF" : "-inf";
        if (v == 0.0)
        {
            // C: %g of +-0 -> "0" / "-0"
            return BitConverter.DoubleToInt64Bits(v) == BitConverter.DoubleToInt64Bits(-0.0) ? "-0" : "0";
        }
        // Round to `prec` significant digits via E format, use its exponent to decide style.
        string e = FormatE(v, prec - 1, false); // e.g. "1.23457e+06"
        int ei = e.IndexOf('e');
        int exp = int.Parse(e.Substring(ei + 1), CultureInfo.InvariantCulture);
        if (exp < -4 || exp >= prec)
        {
            // %e style with precision prec-1, strip trailing zeros from mantissa
            string mant = e.Substring(0, ei);
            if (mant.IndexOf('.') >= 0)
            {
                mant = mant.TrimEnd('0');
                if (mant.EndsWith(".")) mant = mant.Substring(0, mant.Length - 1);
            }
            string estr = System.Math.Abs(exp).ToString("D2", CultureInfo.InvariantCulture);
            return mant + (upper ? "E" : "e") + (exp < 0 ? "-" : "+") + estr;
        }
        else
        {
            // %f style with precision prec-1-exp, strip trailing zeros
            string f = FormatF(v, prec - 1 - exp);
            if (f.IndexOf('.') >= 0)
            {
                f = f.TrimEnd('0');
                if (f.EndsWith(".")) f = f.Substring(0, f.Length - 1);
            }
            return f;
        }
    }

    //-------------------------------------------------------------------------
    // Non-allocating formatting core.
    //
    // FormatInto<T0..T3>() is the span-writing equivalent of Sprintf(): printf-style
    // conversions with byte-identical output, but no object[] allocation, no boxing and
    // no intermediate strings. This is what the generic Text()/SliderFloat() overloads
    // use, mirroring C++ ImFormatStringToTempBuffer() writing into a stack buffer.
    //
    // Returns the total number of characters the output needs. If this exceeds
    // dst.Length the output was truncated (C++ bounded-buffer semantics); the caller
    // may retry with a larger buffer.
    //-------------------------------------------------------------------------

    private ref struct SpanWriter
    {
        public Span<char> Dst;
        public int Pos;
        public void Ch(char c) { if (Pos < Dst.Length) Dst[Pos] = c; Pos++; }
        public void Str(ReadOnlySpan<char> s)
        {
            int copy = Math.Min(s.Length, Dst.Length - Pos);
            if (copy > 0) s.Slice(0, copy).CopyTo(Dst.Slice(Pos));
            Pos += s.Length;
        }
        public void Rep(char c, int n) { for (int k = 0; k < n; k++) Ch(c); }
    }

    // Scratch for formatted output larger than the caller's stackalloc'd span (Text()
    // etc. retry into this), like C++ g.TempBuffer. Shared sequentially; the library is
    // single-threaded per context.
    static char[] s_scratch = new char[1024];
    public static char[] RentScratch(int minLen)
    {
        if (s_scratch.Length < minLen)
        {
            int cap = s_scratch.Length;
            while (cap < minLen) cap *= 2;
            s_scratch = new char[cap];
        }
        return s_scratch;
    }

    // Intermediate conversion bodies too large for the default stack staging buffer
    // (huge %f precision). Kept separate from s_scratch so a FormatInto call writing
    // into a rented s_scratch never aliases its own body buffer.
    static char[] s_bodyScratch = new char[512];

    // Per-thread default staging buffer handed out by BodyBuffer for the common case.
    [ThreadStatic]
    static char[] s_bodyBuf;

    static Span<char> BodyBuffer(int minLen)
    {
        if (minLen <= 512)
        {
            var buf = s_bodyBuf ??= new char[512];
            return buf;
        }
        if (s_bodyScratch.Length < minLen)
        {
            int cap = s_bodyScratch.Length;
            while (cap < minLen) cap *= 2;
            s_bodyScratch = new char[cap];
        }
        return s_bodyScratch;
    }

    public static int FormatInto(Span<char> dst, string fmt)
        => FormatInto<object, object, object, object>(dst, fmt, null, null, null, null);
    public static int FormatInto<T0>(Span<char> dst, string fmt, T0 a0)
        => FormatInto(dst, fmt, a0, default(T0), default(T0), default(T0));
    public static int FormatInto<T0, T1>(Span<char> dst, string fmt, T0 a0, T1 a1)
        => FormatInto(dst, fmt, a0, a1, default(T1), default(T1));
    public static int FormatInto<T0, T1, T2>(Span<char> dst, string fmt, T0 a0, T1 a1, T2 a2)
        => FormatInto(dst, fmt, a0, a1, a2, default(T2));

    public static int FormatInto<T0, T1, T2, T3>(Span<char> dst, string fmt, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        SpanWriter w = new SpanWriter { Dst = dst };
        int ai = 0;
        int i = 0, n = fmt.Length;
        while (i < n)
        {
            char c = fmt[i++];
            if (c != '%') { w.Ch(c); continue; }
            if (i < n && fmt[i] == '%') { w.Ch('%'); i++; continue; }

            // parse flags
            bool left = false, plus = false, space = false, zero = false, alt = false;
            while (i < n)
            {
                c = fmt[i];
                if (c == '-') left = true;
                else if (c == '+') plus = true;
                else if (c == ' ') space = true;
                else if (c == '0') zero = true;
                else if (c == '#') alt = true;
                else break;
                i++;
            }
            // width
            int width = 0;
            while (i < n && fmt[i] >= '0' && fmt[i] <= '9') { width = width * 10 + (fmt[i] - '0'); i++; }
            // precision
            int prec = -1;
            if (i < n && fmt[i] == '.')
            {
                i++;
                prec = 0;
                while (i < n && fmt[i] >= '0' && fmt[i] <= '9') { prec = prec * 10 + (fmt[i] - '0'); i++; }
            }
            // length modifiers (parsed, ignored: args are typed)
            while (i < n && "hljztL".IndexOf(fmt[i]) >= 0) i++;
            if (i >= n) break;
            char conv = fmt[i++];

            if (conv == 's')
            {
                // %s streams directly (arbitrarily long; excluded from zero-fill)
                string s = ArgString(ai++, a0, a1, a2, a3);
                ReadOnlySpan<char> sv = s != null ? s.AsSpan() : "(null)";
                if (prec >= 0 && sv.Length > prec) sv = sv.Slice(0, prec);
                WritePadded(ref w, sv, width, left, conv, zero: false);
                continue;
            }

            switch (conv)
            {
                case 'd':
                case 'i':
                {
                    long v = ArgLong(ai++, a0, a1, a2, a3);
                    Span<char> body = BodyBuffer((prec > 20 ? prec : 20) + 2);
                    WritePadded(ref w, body.Slice(0, WriteSigned(body, v, prec, plus, space)), width, left, conv, zero);
                    break;
                }
                case 'u':
                {
                    ulong v = ArgUlong(ai++, a0, a1, a2, a3);
                    Span<char> body = BodyBuffer((prec > 20 ? prec : 20) + 1);
                    WritePadded(ref w, body.Slice(0, WriteDigits(body, v, prec, 10, false, false)), width, left, conv, zero);
                    break;
                }
                case 'x':
                case 'X':
                {
                    ulong v = ArgUlong(ai++, a0, a1, a2, a3);
                    Span<char> body = BodyBuffer((prec > 16 ? prec : 16) + 2);
                    WritePadded(ref w, body.Slice(0, WriteDigits(body, v, prec, 16, alt && v != 0, conv == 'X')), width, left, conv, zero);
                    break;
                }
                case 'o':
                {
                    ulong v = ArgUlong(ai++, a0, a1, a2, a3);
                    Span<char> body = BodyBuffer((prec > 22 ? prec : 22) + 2);
                    WritePadded(ref w, body.Slice(0, WriteOctal(body, v, prec)), width, left, conv, zero);
                    break;
                }
                case 'c':
                {
                    char ch = ArgChar(ai++, a0, a1, a2, a3);
                    WritePadded(ref w, stackalloc char[] { ch }, width, left, conv, zero);
                    break;
                }
                case 'p':
                {
                    ulong v = ArgPointer(ai++, a0, a1, a2, a3);
                    Span<char> body = BodyBuffer(20);
                    WritePadded(ref w, body.Slice(0, WritePointer(body, v)), width, left, conv, zero);
                    break;
                }
                case 'f':
                case 'F':
                {
                    double v = ArgDouble(ai++, a0, a1, a2, a3);
                    if (prec < 0) prec = 6;
                    Span<char> body = BodyBuffer(340 + prec);
                    WritePadded(ref w, body.Slice(0, WriteF(body, v, prec, plus, space)), width, left, conv, zero);
                    break;
                }
                case 'e':
                case 'E':
                {
                    double v = ArgDouble(ai++, a0, a1, a2, a3);
                    if (prec < 0) prec = 6;
                    Span<char> body = BodyBuffer(prec + 48);
                    WritePadded(ref w, body.Slice(0, WriteE(body, v, prec, conv == 'E', plus, space)), width, left, conv, zero);
                    break;
                }
                case 'g':
                case 'G':
                {
                    double v = ArgDouble(ai++, a0, a1, a2, a3);
                    int gp = prec < 0 ? 6 : (prec == 0 ? 1 : prec);
                    Span<char> body = BodyBuffer(gp + 64);
                    WritePadded(ref w, body.Slice(0, WriteG(body, v, gp, conv == 'G', plus, space)), width, left, conv, zero);
                    break;
                }
                case 'v': // imgui extension? not supported: format as float
                {
                    double v = ArgDouble(ai++, a0, a1, a2, a3);
                    Span<char> body = BodyBuffer(340 + 6);
                    WritePadded(ref w, body.Slice(0, WriteF(body, v, 6, false, false)), width, left, conv, zero);
                    break;
                }
                default:
                {
                    WritePadded(ref w, stackalloc char[] { conv }, width, left, conv, zero);
                    break;
                }
            }
        }
        return w.Pos;
    }

    // Applies printf width semantics to `s` (identical to the Sprintf string path).
    static void WritePadded(ref SpanWriter w, ReadOnlySpan<char> s, int width, bool left, char conv, bool zero)
    {
        if (width > s.Length)
        {
            if (left)
            {
                w.Str(s);
                w.Rep(' ', width - s.Length);
                return;
            }
            if (zero && "difeEgGuXxo".IndexOf(conv) >= 0 && conv != 's')
            {
                int sign_len = (s.Length > 0 && (s[0] == '-' || s[0] == '+' || s[0] == ' ')) ? 1 : 0;
                bool has_head = s.Length >= 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X');
                if (has_head) { w.Ch('0'); w.Ch(s[1]); }
                else if (sign_len > 0) w.Ch(s[0]);
                w.Rep('0', width - s.Length + (has_head ? 2 : sign_len));
                w.Str(has_head ? s.Slice(2) : s.Slice(sign_len));
                return;
            }
            w.Rep(' ', width - s.Length);
        }
        w.Str(s);
    }

    // sign + (precision zero-fill) + decimal digits, matching the Sprintf 'd'/'i' case
    // (sign prepended after zero-fill; a "+"/" " flag still prints for 0 with %.0d).
    static int WriteSigned(Span<char> body, long v, int prec, bool plus, bool space)
    {
        int pos = 0;
        if (v < 0) body[pos++] = '-';
        else if (plus) body[pos++] = '+';
        else if (space) body[pos++] = ' ';
        if (!(v == 0 && prec == 0))
        {
            ulong mag = v < 0 ? unchecked((ulong)(-(v + 1)) + 1) : (ulong)v; // handles long.MinValue
            Span<char> tmp = stackalloc char[20];
            int len = 0;
            do { tmp[len++] = (char)('0' + (int)(mag % 10)); mag /= 10; } while (mag != 0);
            for (long z = len; z < prec; z++) body[pos++] = '0';
            while (len > 0) body[pos++] = tmp[--len];
        }
        return pos;
    }

    // Decimal/hex digits with optional precision zero-fill and 0x/0X prefix. The Sprintf
    // 'x' case inserts the prefix after zero-fill; %u has neither prefix nor sign.
    static int WriteDigits(Span<char> body, ulong v, int prec, int radix, bool prefix, bool upper)
    {
        int pos = 0;
        if (prefix) { body[pos++] = '0'; body[pos++] = upper ? 'X' : 'x'; }
        if (!(v == 0 && prec == 0))
        {
            Span<char> tmp = stackalloc char[16];
            int len = 0;
            do { int d = (int)(v % (ulong)radix); tmp[len++] = (char)(d < 10 ? '0' + d : (upper ? 'A' : 'a') + d - 10); v /= (ulong)radix; } while (v != 0);
            for (long z = len; z < prec; z++) body[pos++] = '0';
            while (len > 0) body[pos++] = tmp[--len];
        }
        return pos;
    }

    // Sprintf 'o' case: Convert.ToString((long)v, 8) - signed octal.
    static int WriteOctal(Span<char> body, ulong vbits, int prec)
    {
        long v = (long)vbits;
        int pos = 0;
        if (v < 0) body[pos++] = '-';
        ulong mag = v < 0 ? unchecked((ulong)(-(v + 1)) + 1) : vbits;
        if (!(v == 0 && prec == 0))
        {
            Span<char> tmp = stackalloc char[22];
            int len = 0;
            do { tmp[len++] = (char)('0' + (int)(mag % 8)); mag /= 8; } while (mag != 0);
            for (long z = len; z < prec; z++) body[pos++] = '0';
            while (len > 0) body[pos++] = tmp[--len];
        }
        return pos;
    }

    static int WritePointer(Span<char> body, ulong v)
    {
        body[0] = '0';
        body[1] = 'x';
        return 2 + WriteDigits(body.Slice(2), v, -1, 16, false, false);
    }

    // F-format without sign flags; NaN/inf render C-style like FormatF.
    static int WriteFCore(Span<char> body, double v, int prec)
    {
        if (double.IsNaN(v)) { "nan".CopyTo(body); return 3; }
        if (double.IsPositiveInfinity(v)) { "inf".CopyTo(body); return 3; }
        if (double.IsNegativeInfinity(v)) { "-inf".CopyTo(body); return 4; }
        // "F<prec>" via TryFormat is bit-identical to ToString("F"+prec) (see FormatF note).
        Span<char> spec = stackalloc char[12];
        spec[0] = 'F';
        int sl = WriteUInt(spec.Slice(1), (ulong)prec) + 1;
        v.TryFormat(body, out int written, spec.Slice(0, sl), CultureInfo.InvariantCulture);
        return written;
    }

    static int WriteF(Span<char> body, double v, int prec, bool plus, bool space)
    {
        int pos = 0;
        if (v >= 0 && plus) body[pos++] = '+';
        else if (v >= 0 && space) body[pos++] = ' ';
        return pos + WriteFCore(body.Slice(pos), v, prec);
    }

    // Writes `v` in decimal into dst, returns digits written.
    static int WriteUInt(Span<char> dst, ulong v)
    {
        Span<char> tmp = stackalloc char[20];
        int len = 0;
        do { tmp[len++] = (char)('0' + (int)(v % 10)); v /= 10; } while (v != 0);
        for (int k = 0; k < len; k++) dst[k] = tmp[len - 1 - k];
        return len;
    }

    // C-style exponential with sign flags (Sprintf 'e'/'E' case).
    static int WriteE(Span<char> body, double v, int prec, bool upper, bool plus, bool space)
    {
        int len = WriteECore(body, v, prec, upper);
        if (v >= 0 && (plus || space))
        {
            body.Slice(0, len).CopyTo(body.Slice(1));
            body[0] = plus ? '+' : ' ';
            len++;
        }
        return len;
    }

    // C-style exponential without sign flags: mantissa + e/E + sign + at-least-2
    // exponent digits (in-place transform of .NET's "E<prec>" output, same as FormatE).
    static int WriteECore(Span<char> body, double v, int prec, bool upper)
    {
        if (double.IsNaN(v)) { "nan".CopyTo(body); return 3; }
        if (double.IsPositiveInfinity(v)) { (upper ? "INF" : "inf").CopyTo(body); return 3; }
        if (double.IsNegativeInfinity(v)) { (upper ? "-INF" : "-inf").CopyTo(body); return 4; }

        Span<char> spec = stackalloc char[12];
        spec[0] = 'E';
        int sl = WriteUInt(spec.Slice(1), (ulong)prec) + 1;
        v.TryFormat(body, out int n, spec.Slice(0, sl), CultureInfo.InvariantCulture); // e.g. "1.230000E-005"

        int ei = body.Slice(0, n).IndexOf('E');
        char ec = upper ? 'E' : 'e';
        for (int k = 0; k < ei; k++)
            body[k] = upper ? char.ToUpperInvariant(body[k]) : char.ToLowerInvariant(body[k]);
        body[ei] = ec;

        // exponent: [sign][digits...] - normalize to at least 2 digits
        int r = ei + 1;
        while (r < n && (body[r] == '-' || body[r] == '+')) r++;
        int ev = 0;
        while (r < n) { ev = ev * 10 + (body[r] - '0'); r++; }
        int wpos = ei + 1;
        body[wpos++] = body[ei + 1] == '-' ? '-' : '+';
        int edl = WriteUIntAtLeast2(body.Slice(wpos), (ulong)ev);
        return wpos + edl;
    }

    // Sprintf 'g'/'G' case (FormatG): picks e-style or f-style from the E rounding.
    static int WriteG(Span<char> body, double v, int prec, bool upper, bool plus, bool space)
    {
        int pos = 0;
        if (v >= 0 && (plus || space))
            body[pos++] = plus ? '+' : ' ';
        if (double.IsNaN(v)) { "nan".CopyTo(body.Slice(pos)); return pos + 3; }
        if (double.IsPositiveInfinity(v)) { (upper ? "INF" : "inf").CopyTo(body.Slice(pos)); return pos + 3; }
        if (double.IsNegativeInfinity(v)) { (upper ? "-INF" : "-inf").CopyTo(body.Slice(pos)); return pos + 4; }
        if (v == 0.0)
        {
            // C: %g of +-0 -> "0" / "-0"
            if (BitConverter.DoubleToInt64Bits(v) == BitConverter.DoubleToInt64Bits(-0.0)) body[pos++] = '-';
            body[pos++] = '0';
            return pos;
        }

        // Round to `prec` significant digits via E format, use its exponent to decide style.
        // e goes into a dedicated buffer: `body` may itself be a rented scratch that the
        // e-style branch below reads from while writing.
        Span<char> e = EBuffer(prec + 40);
        int elen = WriteECore(e, v, prec - 1, false);

        int ei = e.Slice(0, elen).IndexOf('e');
        bool eneg = e[ei + 1] == '-';
        int r = ei + 2; // skip 'e' + mandatory sign
        int exp = 0;
        while (r < elen) { exp = exp * 10 + (e[r] - '0'); r++; }
        if (eneg) exp = -exp;

        if (exp < -4 || exp >= prec)
        {
            // %e style with precision prec-1, strip trailing zeros from mantissa
            int mend = ei;
            int dot = e.Slice(0, ei).IndexOf('.');
            if (dot >= 0)
            {
                while (mend > dot + 1 && e[mend - 1] == '0') mend--;
                if (mend == dot + 1) mend = dot; // drop trailing '.'
            }
            for (int k = 0; k < mend; k++) body[pos + k] = e[k];
            pos += mend;
            body[pos++] = (char)(upper ? 'E' : 'e');
            body[pos++] = exp < 0 ? '-' : '+';
            pos += WriteUIntAtLeast2(body.Slice(pos), (ulong)System.Math.Abs(exp));
        }
        else
        {
            // %f style with precision prec-1-exp, strip trailing zeros
            int flen = WriteFCore(body.Slice(pos), v, prec - 1 - exp);
            int dot = body.Slice(pos, flen).IndexOf('.');
            if (dot >= 0)
            {
                int mend = flen;
                while (mend > dot + 1 && body[pos + mend - 1] == '0') mend--;
                if (mend == dot + 1) mend = dot;
                flen = mend;
            }
            pos += flen;
        }
        return pos;
    }

    // Writes `v` into dst with at least 2 digits (printf exponent D2 convention).
    // dst must have capacity for the digit count plus one.
    static int WriteUIntAtLeast2(Span<char> dst, ulong v)
    {
        int len = WriteUInt(dst.Slice(1), v); // digits land at dst[1..]
        if (len < 2)
        {
            dst[0] = '0'; // prepend the pad zero; digits already follow it
            return len + 1;
        }
        for (int k = 0; k < len; k++) dst[k] = dst[k + 1]; // shift left: dst[0..] = digits
        return len;
    }

    // Scratch for the %g E-form rounding (kept separate from BodyBuffer: WriteG receives
    // a BodyBuffer span as its output and must not alias its own intermediate).
    static char[] s_eScratch = new char[128];
    static Span<char> EBuffer(int minLen)
    {
        if (s_eScratch.Length < minLen)
        {
            int cap = s_eScratch.Length;
            while (cap < minLen) cap *= 2;
            s_eScratch = new char[cap];
        }
        return s_eScratch;
    }

    static string ArgString<T0, T1, T2, T3>(int i, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        object o;
        switch (i)
        {
            case 0: o = a0; break;
            case 1: o = a1; break;
            case 2: o = a2; break;
            default: o = a3; break;
        }
        return o as string;
    }

    static long ArgLong<T0, T1, T2, T3>(int i, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        switch (i)
        {
            case 0: return ToLongG(a0);
            case 1: return ToLongG(a1);
            case 2: return ToLongG(a2);
            default: return ToLongG(a3);
        }
    }
    static long ToLongG<T>(T v)
    {
        if (v is int i) return i;
        if (v is long l) return l;
        if (v is uint u) return u;
        if (v is short sh) return sh;
        if (v is ushort us) return us;
        if (v is byte b) return b;
        if (v is sbyte sb) return sb;
        if (v is bool bo) return bo ? 1L : 0L;
        if (v is char c) return c;
        return Convert.ToInt64((object)v);
    }

    static ulong ArgUlong<T0, T1, T2, T3>(int i, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        switch (i)
        {
            case 0: return ToUlongG(a0);
            case 1: return ToUlongG(a1);
            case 2: return ToUlongG(a2);
            default: return ToUlongG(a3);
        }
    }
    static ulong ToUlongG<T>(T v)
    {
        if (v is uint u) return u;
        if (v is int i) return unchecked((ulong)(long)i);
        if (v is ulong ul) return ul;
        if (v is long l) return unchecked((ulong)l);
        return Convert.ToUInt64((object)v);
    }

    static double ArgDouble<T0, T1, T2, T3>(int i, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        switch (i)
        {
            case 0: return ToDoubleG(a0);
            case 1: return ToDoubleG(a1);
            case 2: return ToDoubleG(a2);
            default: return ToDoubleG(a3);
        }
    }
    static double ToDoubleG<T>(T v)
    {
        if (v is float f) return (double)f; // C varargs promote float -> double
        if (v is double d) return d;
        if (v is decimal m) return (double)m;
        return Convert.ToDouble((object)v);
    }

    static char ArgChar<T0, T1, T2, T3>(int i, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        switch (i)
        {
            case 0: return ToCharG(a0);
            case 1: return ToCharG(a1);
            case 2: return ToCharG(a2);
            default: return ToCharG(a3);
        }
    }
    static char ToCharG<T>(T v)
    {
        if (v is char ch) return ch;
        return (char)(int)Convert.ChangeType((object)v, typeof(int));
    }

    static ulong ArgPointer<T0, T1, T2, T3>(int i, T0 a0, T1 a1, T2 a2, T3 a3)
    {
        switch (i)
        {
            case 0: return ToPointerG(a0);
            case 1: return ToPointerG(a1);
            case 2: return ToPointerG(a2);
            default: return ToPointerG(a3);
        }
    }
    static ulong ToPointerG<T>(T v)
    {
        if (v is pointer_t pt) return pt.value;
        return Convert.ToUInt64((object)v);
    }
}
