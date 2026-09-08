#!/bin/bash
# Build the C++ golden-data generator and regenerate all golden files.
# IMGUI_EXACT_RSQRT: rsqrtss results are CPU-implementation-specific (differ
# between vendors/generations), so the reference build uses exact 1/sqrtf and
# the C# port matches that. See imgui_internal.h ImRsqrt().
cd "$(dirname "$0")"
g++ -O1 -DIMGUI_EXACT_RSQRT -I imgui golden_dump.cpp imgui/imgui.cpp imgui/imgui_draw.cpp imgui/imgui_tables.cpp imgui/imgui_widgets.cpp imgui/imgui_demo.cpp -o golden_dump.exe || exit 1
./golden_dump.exe
rm -f imgui.ini
