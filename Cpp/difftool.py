#!/usr/bin/env python3
# Compare golden expected vs actual dumps with float decoding for readability.
# Usage: python difftool.py <scenario> [expfile] [actfile]
import struct, sys, os

def hexf(h):
    try:
        return struct.unpack('<f', struct.pack('<I', int(h, 16)))[0]
    except Exception:
        return h

def dec(line):
    # decode V lines and x=value float pairs into decimal-annotated form
    if line.startswith('V '):
        parts = line.split()
        out = ['V']
        for p in parts[1:5]:
            out.append(f"{hexf(p):.6f}")
        if len(parts) >= 5:
            out.append(parts[4])
        return ' '.join(out)
    if '=' in line and not line.startswith('#'):
        name, _, val = line.partition('=')
        seps = val.split(',')
        if len(seps) in (2, 4) and all(len(s) == 8 for s in seps):
            ds = ','.join(f"{hexf(s):.6f}" for s in seps)
            return f"{name}={val} [{ds}]"
    return line

def main():
    name = sys.argv[1]
    base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'CSharp', 'ImguiCs.Tests', 'GoldenData')
    exp = sys.argv[2] if len(sys.argv) > 2 else os.path.join(base, name + '.txt')
    act = sys.argv[3] if len(sys.argv) > 3 else os.path.join(base, 'actual', name + '.txt')
    el = [dec(l) for l in open(exp, encoding='utf-8').read().replace('\r\n', '\n').split('\n')]
    al = [dec(l) for l in open(act, encoding='utf-8').read().replace('\r\n', '\n').split('\n')]
    import difflib
    count = 0
    for line in difflib.unified_diff(el, al, 'exp', 'act', lineterm='', n=2):
        print(line)
        count += 1
        if count > int(os.environ.get('DIFFMAX', '120')):
            print('... (truncated)')
            break

if __name__ == '__main__':
    main()
