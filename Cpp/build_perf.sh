#!/bin/bash
# Build the C++ perf probe. --wrap hooks count heap allocations (bytes + count)
# so steady-state per-frame allocations can be compared against the C# PerfProbe.
cd "$(dirname "$0")"
g++ -O2 -I imgui-docking perf_probe.cpp imgui-docking/imgui.cpp imgui-docking/imgui_draw.cpp \
    imgui-docking/imgui_tables.cpp imgui-docking/imgui_widgets.cpp \
    -Wl,--wrap=malloc,--wrap=calloc,--wrap=realloc,--wrap=free -o perf_probe.exe || exit 1
./perf_probe.exe
