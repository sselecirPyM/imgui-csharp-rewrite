// Atlas pixel probe: builds font atlases exactly like a backend would and dumps
// raw texture bytes so the C# port can be compared byte-for-byte.
#include "imgui.h"
#include "imgui_internal.h"
#include <stdio.h>
#include <stdlib.h>

static void DumpTextures(const char* tag)
{
    ImFontAtlas* atlas = ImGui::GetIO().Fonts;
    char path[256];
    snprintf(path, sizeof(path), "atlas_%s.bin", tag);
    FILE* f = fopen(path, "wb");
    for (int i = 0; i < atlas->TexList.Size; i++) {
        ImTextureData* tex = atlas->TexList[i];
        fprintf(f, "TEX %d w=%d h=%d bpp=%d\n", i, tex->Width, tex->Height, tex->BytesPerPixel);
        fwrite(tex->GetPixels(), 1, (size_t)tex->GetSizeInBytes(), f);
        fprintf(f, "\n");
    }
    fclose(f);
}

int main(int argc, char** argv)
{
    const char* tag = argc > 1 ? argv[1] : "default";
    const char* ttf = argc > 2 ? argv[2] : NULL;
    float size = argc > 3 ? (float)atof(argv[3]) : 14.0f;

    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2(640.0f, 480.0f);
    io.DeltaTime = 1.0f / 60.0f;
    io.IniFilename = NULL;

    if (ttf)
        io.Fonts->AddFontFromFileTTF(ttf, size, NULL, io.Fonts->GetGlyphRangesChineseFull());
    else
        io.Fonts->AddFontDefault();

    // Legacy backend path: fully bake the atlas up front (like golden_dump).
    unsigned char* px; int tw, th, tbpp;
    io.Fonts->GetTexDataAsRGBA32(&px, &tw, &th, &tbpp);

    ImGui::NewFrame();
    if (ttf)
        ImGui::Text("Hello, 世界! ABCxyz 123 中文字形测试。");
    else
        ImGui::Text("Hello, World! ABCxyz 123");
    ImGui::Render();

    DumpTextures(tag);
    ImGui::DestroyContext();
    printf("dumped %s\n", tag);
    return 0;
}
