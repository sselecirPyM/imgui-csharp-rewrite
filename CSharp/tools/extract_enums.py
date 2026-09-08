#!/usr/bin/env python3
"""Extract C enums from ImGui headers and emit C# const-int classes.

Keeps original enumerator names and values (including expressions like `1 << 12`,
`A | B` and function-like references to other constants) verbatim where possible.
Expression-side identifiers are assumed to exist in the same or an earlier C#
class, so we re-emit them with their original spelling.
"""
import re
import sys

def extract_enums(text):
    out = []
    i = 0
    n = len(text)
    while True:
        m = re.compile(r'\benum\s+(\w+)\s*(?::\s*(?:int|ImS8|ImU8|ImS16|ImU16|ImS32|ImU32|ImS64|ImU64))?\s*\{').search(text, i)
        if not m:
            break
        name = m.group(1)
        start = m.end()
        depth = 1
        j = start
        while depth > 0:
            if text[j] == '{': depth += 1
            elif text[j] == '}': depth -= 1
            j += 1
        body = text[start:j-1]
        # strip line comments but keep them to preserve docs? drop for compactness
        entries = []
        # split top-level by commas (parens/brackets are rare)
        parts = []
        cur = []
        k = 0
        in_comment = False
        for line in body.split('\n'):
            ls = line.strip()
            if ls.startswith('#'):
                continue  # preprocessor line
            line_nc = re.sub(r'//.*', '', line)
            for piece in re.split(r',(?![^()]*\))', line_nc):
                p = piece.strip()
                if p:
                    entries.append(p)
        last_has_eq = None
        exprs = []
        for e in entries:
            if '=' in e:
                nm, val = e.split('=', 1)
                nm = nm.strip(); val = val.strip()
                exprs.append((nm, val))
                last_has_eq = True
            else:
                # enumerator without '=' gets previous value + 1: emit as previous_expr + 1 when we can,
                # else renumber manually
                nm = e.strip()
                if last_has_eq is None:
                    exprs.append((nm, '0'))
                else:
                    prev_nm, prev_val = exprs[-1]
                    # only valid if previous was a plain int literal
                    if re.fullmatch(r'-?\d+', prev_val):
                        exprs.append((nm, str(int(prev_val) + 1)))
                    else:
                        # e.g. previous was an expression; C# const can't do +1 without parens — wrap
                        exprs.append((nm, '(' + prev_val + ') + 1'))
                last_has_eq = False
        out.append((name, exprs))
        i = j
    return out

def emit(name, exprs, f):
    # Convert ImGui naming: enum ImGuiWindowFlags_ -> class ImGuiWindowFlags_
    cls = name if name.endswith('_') else name + '_'
    f.write(f'public static unsafe partial class {cls}\n{{\n')
    for nm, val in exprs:
        val = val.replace('ImU64(', '((ulong)(').replace('IM_ARRAYSIZE', '0 /*IM_ARRAYSIZE*/')
        f.write(f'    public const int {nm} = {val};\n')
    f.write('}\n\n')

def main():
    hdr = '''// Auto-generated from Dear ImGui %s by tools/extract_enums.py.
// All enumerations are emitted as const-int classes to mirror the C++ usage
// of flag values as plain ints.

namespace ImGui;

'''
    files = sys.argv[1:]
    all_enums = []  # (path, name, exprs)
    for path in files:
        text = open(path, encoding='utf-8', errors='replace').read()
        text_nc = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
        for name, exprs in extract_enums(text_nc):
            if not name.endswith('_'):
                name = name + '_'
            all_enums.append((path, name, exprs))

    # map constant name -> owning class (first definition wins)
    owner = {}
    for path, name, exprs in all_enums:
        for nm, val in exprs:
            if nm not in owner:
                owner[nm] = name

    ident_re = re.compile(r'\b[A-Za-z_]\w*\b')

    def qualify(val, my_class):
        def rep(m):
            w = m.group(0)
            if w in ('0',):
                return w
            if w in owner and owner[w] != my_class:
                return owner[w] + '.' + w
            return w
        return ident_re.sub(rep, val)

    f = sys.stdout
    seen = set()
    first = True
    for path, name, exprs in all_enums:
        if name in seen:
            continue
        seen.add(name)
        if first:
            f.write(hdr % path.split('/')[-1])
            first = False
        f.write(f'public static unsafe partial class {name}\n{{\n')
        for nm, val in exprs:
            val = qualify(val, name)
            f.write(f'    public const int {nm} = {val};\n')
        f.write('}\n\n')

if __name__ == '__main__':
    main()

