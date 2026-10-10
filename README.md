# ImguiCs

**A pure managed C# rewrite of [Dear ImGui](https://github.com/ocornut/imgui), verified bit-for-bit against the C++ original.**

[English](README.md) | [简体中文](README.zh-CN.md)

---

## Overview

ImguiCs is not a binding. The Dear ImGui C++ sources were translated file by file,
function by function, into idiomatic C#: the core framework, every standard widget,
tables, the docking branch's docking system and multi-viewport support, the complete
`ShowDemoWindow`, and the stb_truetype / stb_rect_pack / stb_textedit code the font
atlas and text input depend on.

The library contains **no native code and zero P/Invoke**. It runs headless on any
platform with a .NET runtime, and its rendering boundary is exactly the same as
upstream: `ImDrawList` vertex/index buffers plus font atlas textures.

| | |
| --- | --- |
| Language | C# (pure managed, single `ImGui` namespace) |
| Target frameworks | .NET 8 / .NET 10 |
| Scale | 70 source files, ~60,000 lines, file layout mirrors the C++ sources one-to-one |
| Upstream baseline (master) | `v1.92.9b-44-g96b6eb772` (reports `1.93.0 WIP`), pinned as `Cpp/imgui` submodule |
| Upstream baseline (docking) | `v1.92.9b-docking-52-g7e1b65d26`, branch `features/drawlist_193`, pinned as `Cpp/imgui-docking` submodule |
| License | MIT (derivative work; retains upstream copyright) |

## Feature overview

- **Core** — NewFrame/EndFrame pipeline, windows and clipping, ID/hashing, keyboard
  and gamepad navigation, focus system, `.ini` settings serialization, style and
  color system.
- **Widgets** — the whole of `imgui_widgets.cpp`: buttons, sliders, drag inputs,
  `InputText` (full stb_textedit port with undo stack and IME support), trees, tabs,
  list boxes, menus, popups, color pickers, and more.
- **Tables** — complete port of `imgui_tables.cpp`: sorting, frozen rows/columns,
  row clipping, borders, sizing policies.
- **Docking & multi-viewport** — the docking branch's dock tree, tab bars, splitters,
  dock-node host windows and platform-viewport logic.
- **DrawList** — all primitives, anti-aliased fills and polylines, Bézier curves,
  and the `ImTriangulator` concave-polygon triangulator.
- **Fonts & atlas** — full C# ports of stb_truetype and stb_rect_pack. Atlas build
  output is **byte-for-byte identical** to C++. ProggyClean is embedded as the
  default font.
- **Text & formatting** — UTF-8 codec and an `ImFmt` implementation bit-compatible
  with C `printf` semantics.
- **Demo & debug tooling** — `ShowDemoWindow`, Debug Log, Stack Tool, Metrics/Debugger
  and Debug Nodes windows, fully functional.

## Correctness verification

The heart of this repository is a **bit-exact golden-data loop**:

1. `Cpp/golden_dump.cpp` runs **33 deterministic scenarios** under fixed conditions
   (1280×720 display, `dt = 1/60`, `.ini` disabled, fixed mouse position, pre-baked
   atlas) and dumps every vertex, index and draw command of every frame, line by
   line, as hex-encoded IEEE-754 bit patterns into `CSharp/ImguiCs.Tests/GoldenData/`.
2. The C# side replays the same scenarios with the identical setup and output format
   (`CSharp/ImguiCs.Tests/GoldenTest.cs`).
3. The xUnit suite requires the C# output to match the committed C++ golden files
   **line for line and bit for bit** — a single mantissa-bit drift in any float
   fails the build.

Additional cross-checks surround this main loop:

| Verified area | C++ side | C# side |
| --- | --- | --- |
| Font atlas rasterization | `Cpp/atlas_dump.cpp` | `CSharp/AtlasProbe` (byte-for-byte texture comparison) |
| Docking interaction | `Cpp/dock_click_probe.cpp` | `CSharp/DockProbe` (staged PASS/FAIL: tab click/close, central docking, `.ini` round-trip, splitter drag, …) |
| Demo window geometry | `Cpp/demo_probe.cpp` | golden scenario `demo` (structure + FNV-1a hash of all vertex/index bytes) |
| Per-frame memory & timing | `Cpp/perf_probe.cpp` (`--wrap=malloc` counting) | `CSharp/PerfProbe` (`GC.GetTotalAllocatedBytes`) |
| Public API coverage | `Cpp/api_coverage.py` (scans `imgui.h`) | compared against `cpp_api.txt` / `cs_api.txt` |

> **Floating-point convention.** The C++ reference build uses `-DIMGUI_EXACT_RSQRT`
> (exact `1/sqrtf`) because `rsqrtss` results differ between CPU vendors; the C#
> side matches that reference behavior. See `Cpp/build_golden.sh`.

## Repository layout

```
CSharp/
  ImguiCs.slnx        Solution (library + tests + AtlasProbe + DockProbe)
  ImguiCs/            The port itself — pure managed library, net8.0 / net10.0
  ImguiCs.Tests/      xUnit suite: 33 golden scenarios + unit tests
                      (scenarios defined in GoldenTest.cs)
  GoldenRunner/       Console golden differ — per-scenario PASS/FAIL, handy for debugging
  AtlasProbe/         Byte-level font-atlas comparison (C# side)
  DockProbe/          Staged docking-behavior tests (C# side)
  PerfProbe/          Per-frame managed allocations and timing (C# side)
  tools/              Code-generation and one-off porting scripts
                      (extract_enums.py, patch_docking_begin.py)
Cpp/
  imgui/              Upstream master submodule (reference baseline)
  imgui-docking/      Upstream docking-branch submodule
  golden_dump.cpp     Golden-data generator (33 deterministic scenarios)
  build_golden.sh     Build golden_dump and regenerate all golden files
  build_perf.sh       Build and run the C++ perf probe
  atlas_dump.cpp / dock_click_probe.cpp / demo_probe.cpp / perf_probe.cpp
                      Atlas / docking / demo / performance reference probes
  ref_docking.cpp / ref_viewports.cpp
                      Upstream docking/viewport reference extracts used during the port
  difftool.py         Decodes golden-file diffs into readable floats
  patch_trace.py      Temporary C++ instrumentation for tracing draw internals
  api_coverage.py     Public API coverage statistics
  dock_*.diff, docking_full.diff
                      Docking branch vs. master baseline (review aids)
cpp_api.txt, cs_api.txt   C++ / C# public API listings (coverage artifacts)
```

## Getting started

**Prerequisites**

- .NET 10 SDK (the library multi-targets `net8.0`/`net10.0`; tests and probes target `net10.0`)
- Git with submodule support
- Optional: g++ (MSYS2/MinGW on Windows) — only needed to regenerate the C++ golden data
- Optional: Python 3 — only needed for the scripts under `CSharp/tools/` and `Cpp/`

**Clone and test**

```bash
git clone --recurse-submodules <this repository>
cd Imgui-porting/CSharp

# Full suite: 33 golden scenarios + unit tests
dotnet test ImguiCs.Tests

# Console per-scenario golden comparison
dotnet run --project GoldenRunner

# Staged docking-behavior tests
dotnet run --project DockProbe

# Font atlas byte comparison
dotnet run --project AtlasProbe
```

**Regenerating the golden data** (C++ side; overwrites the expected files under
`CSharp/ImguiCs.Tests/GoldenData/`):

```bash
cd Cpp
bash build_golden.sh
```

## Scope and limitations

- **No renderer or platform backends.** The output boundary is `ImDrawList`
  vertex/index data and atlas textures. To display a UI, an application must
  consume that data with its own renderer/input layer (or a future backend port).
- **Behavioral parity, not source-level compatibility.** The goal is identical
  visible behavior and draw output for identical input. Internal data structures
  are rewritten in equivalent C# idioms; public API names and signatures follow
  upstream conventions but use C# types (`ref` parameters, arrays, spans).

## Development notes

The C# code in this repository was written by **ZCode**, an AI coding agent powered
by the **GLM 5.3 Flash** model, translating line by line against the Dear ImGui C++
sources as the sole reference. The complete port — including the golden-data
verification system and all debugging round-trips needed to bring every scenario to
bit-exact parity — was produced in **two days** and roughly **600 million tokens**.

## License

Released under the [MIT License](LICENSE). As a derivative work of Dear ImGui, the
copyright notice retains both the upstream authors (Omar Cornut and the Dear ImGui
contributors) and the port author.
