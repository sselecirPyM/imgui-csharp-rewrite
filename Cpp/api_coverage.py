#!/usr/bin/env python3
# Compare public IMGUI_API functions declared in imgui.h against the C# port.
# Comments and string literals are stripped with a small state machine so a
# function that only appears inside a comment counts as missing.
import os

HERE = os.path.dirname(os.path.abspath(__file__))
hdr = open(os.path.join(HERE, "imgui", "imgui.h"), encoding="utf-8").read()


def strip_comments(text):
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if c == "/" and nxt == "/":
            while i < n and text[i] != "\n":
                i += 1
        elif c == "/" and nxt == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                i += 1
            i += 2
            out.append(" ")
        elif c == '"':
            i += 1
            while i < n and text[i] != '"':
                if text[i] == "\\":
                    i += 1
                i += 1
            i += 1
            out.append('""')
        else:
            out.append(c)
            i += 1
    return "".join(out)


hdr_nc = strip_comments(hdr)
funcs = set()
i = hdr_nc.find("IMGUI_API")
while i != -1:
    # Skip the empty "#define IMGUI_API" fallback: it is followed directly by a
    # newline, while real declarations have a space + return type. Scanning it
    # would otherwise pick up the next macro's '(' (e.g. IM_ASSERT) as a function.
    if hdr_nc[i + len("IMGUI_API")] in " \t":
        # take up to the first '(' after IMGUI_API and grab the identifier before it
        j = hdr_nc.find("(", i)
        if j != -1:
            seg = hdr_nc[i + len("IMGUI_API"):j]
            name = ""
            for ch in reversed(seg):
                if ch.isalnum() or ch == "_":
                    name = ch + name
                elif name:
                    break
            if name:
                funcs.add(name)
    i = hdr_nc.find("IMGUI_API", i + 1)

cs_dir = os.path.join(HERE, "..", "CSharp", "ImguiCs")
blob = ""
for f in os.listdir(cs_dir):
    if f.endswith(".cs"):
        blob += open(os.path.join(cs_dir, f), encoding="utf-8").read()

blob = strip_comments(blob)

import re
missing = []
for fn in sorted(funcs):
    if not re.search(r"\b" + re.escape(fn) + r"\s*\(", blob):
        missing.append(fn)

print(f"imgui.h IMGUI_API functions: {len(funcs)}")
print(f"missing in C# port: {len(missing)}")
for fn in missing:
    print("  -", fn)
