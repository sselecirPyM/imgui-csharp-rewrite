// Port of imgui_demo.cpp example apps (part 1), C++ lines 8947-9899:
//   ShowExampleAppMainMenuBar / ShowExampleMenuFile / ExampleAppConsole + ShowExampleAppConsole /
//   ShowExampleAppImageViewer / ExampleAppLog + ShowExampleAppLog / ShowExampleAppLayout
// (struct ExampleAppPropertyEditor at C++ 9678 is ported in ImGuiDemoWidgets2.cs.)
using System;
using System.Collections.Generic;
using System.Text;
using static ImGui.ImGui; // for unqualified ImGui.* calls inside namespace-level Example classes

namespace ImGui;

// Function-local 'static' variables of the demo functions in this file (C++ function-local statics).
static class DemoApps1Locals
{
    // ShowExampleAppConsole()
    public static ExampleAppConsole console = new ExampleAppConsole();      // C++: static ExampleAppConsole console

    // ShowExampleAppImageViewer()
    public static ExampleImageViewerData image_viewer = new ExampleImageViewerData(); // C++: static ExampleImageViewerData image_viewer

    // ShowExampleMenuFile()
    public static bool menu_file_enabled = true;                            // C++: static bool enabled = true
    public static float menu_file_f = 0.5f;                                 // C++: static float f = 0.5f
    public static int menu_file_n = 0;                                      // C++: static int n = 0
    public static bool menu_file_b = true;                                  // C++: static bool b = true

    // ShowExampleAppLog()
    public static ExampleAppLog log = new ExampleAppLog();                  // C++: static ExampleAppLog log
    public static int log_counter = 0;                                      // C++: static int counter = 0

    // ShowExampleAppLayout()
    public static int layout_selected = 0;                                  // C++: static int selected = 0
}

//-----------------------------------------------------------------------------
// [SECTION] Example App: Debug Console / ShowExampleAppConsole()
//-----------------------------------------------------------------------------

// Demonstrate creating a simple console window, with scrolling, filtering, completion and history.
// For the console example, we are using a more C++ like approach of declaring a class to hold both data and functions.
// C++: struct ExampleAppConsole (imgui_demo.cpp 9061-9410)
public class ExampleAppConsole
{
    public byte[] InputBuf = new byte[256];
    public List<string> Items = new List<string>();
    // "CLASSIFY" is here to provide the test case where "C"+[tab] completes to "CL" and display multiple matches.
    public static readonly string[] Commands = { "HELP", "HISTORY", "CLEAR", "CLASSIFY" };
    public List<string> History = new List<string>();
    public int HistoryPos = -1;    // -1: new line, 0..History.Count-1 browsing history.
    public ImGuiTextFilter Filter = new ImGuiTextFilter();
    public bool AutoScroll;
    public bool ScrollToBottom;

    public ExampleAppConsole()
    {
        ClearLog();
        // C++: memset(InputBuf, 0, sizeof(InputBuf)); — a byte[] is already zero-initialized in C#.
        HistoryPos = -1;

        AutoScroll = true;
        ScrollToBottom = false;
        AddLog("Welcome to Dear ImGui!");
    }

    // C++ destructor freed the Items/History buffers; C# garbage collection makes it unnecessary.

    // Portable helpers
    // C++ walks NUL-terminated char pointers; a read past the end of a string yields 0, like the C NUL.
    static int UpperAt(string s, int index) => index < s.Length ? (int)char.ToUpperInvariant(s[index]) : 0;
    public static int Stricmp(string s1, string s2)
    {
        int d, i1 = 0, i2 = 0;
        while ((d = UpperAt(s2, i2) - UpperAt(s1, i1)) == 0 && i1 < s1.Length) { i1++; i2++; }
        return d;
    }
    public static int Strnicmp(string s1, string s2, int n)
    {
        int d = 0, i1 = 0, i2 = 0;
        while (n > 0 && (d = UpperAt(s2, i2) - UpperAt(s1, i1)) == 0 && i1 < s1.Length) { i1++; i2++; n--; }
        return d;
    }
    // C++: static char* Strdup(const char* s) — unnecessary in C# (garbage-collected strings).
    // C++ trims trailing blanks (' ') in-place; here we return the trimmed copy.
    public static string Strtrimblanks(string s) => s.TrimEnd(' ');

    // Reads the NUL-terminated C string out of a byte buffer (e.g. InputBuf after InputText()).
    static string GetCString(byte[] buf)
    {
        int len = 0;
        while (len < buf.Length && buf[len] != 0)
            len++;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    public void ClearLog()
    {
        Items.Clear();
    }

    public void AddLog(string fmt, params object[] args)
    {
        // FIXME-OPT
        // C++: vsnprintf() into a 1024-byte buffer; ImFmt.Sprintf is the printf-style equivalent.
        Items.Add(ImFmt.Sprintf(fmt, args));
    }

    public void Draw(string title, ref bool p_open)
    {
        SetNextWindowSize(new ImVec2(520, 600), ImGuiCond_FirstUseEver);
        if (!Begin(title, ref p_open))
        {
            End();
            return;
        }

        // As a specific feature guaranteed by the library, after calling Begin() the last Item represent the title bar.
        // So e.g. IsItemHovered() will return true when hovering the title bar.
        // Here we create a context menu only available from the title bar.
        if (BeginPopupContextItem())
        {
            if (MenuItem("Close Console"))
                p_open = false;
            EndPopup();
        }

        TextWrapped(
            "This example implements a console with basic coloring, completion (TAB key) and history (Up/Down keys). A more elaborate " +
            "implementation may want to store entries along with extra data such as timestamp, emitter, etc.");
        TextWrapped("Enter 'HELP' for help.");

        // TODO: display items starting from the bottom

        if (SmallButton("Add Debug Text")) { AddLog("%d some text", Items.Count); AddLog("some more text"); AddLog("display very important message here!"); }
        SameLine();
        if (SmallButton("Add Debug Error")) { AddLog("[error] something went wrong"); }
        SameLine();
        if (SmallButton("Clear")) { ClearLog(); }
        SameLine();
        bool copy_to_clipboard = SmallButton("Copy");
        //static float t = 0.0f; if (ImGui::GetTime() - t > 0.02f) { t = ImGui::GetTime(); AddLog("Spam %f", t); }

        Separator();

        // Options menu
        if (BeginPopup("Options"))
        {
            Checkbox("Auto-scroll", ref AutoScroll);
            EndPopup();
        }

        // Options, Filter
        SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_O, ImGuiInputFlags_Tooltip);
        if (Button("Options"))
            OpenPopup("Options");
        SameLine();
        Filter.Draw("Filter (\"incl,-excl\") (\"error\")", 180);
        Separator();

        // Reserve enough left-over height for 1 separator + 1 input text
        ImGuiStyle style = GetStyle();
        float footer_height_to_reserve = style.SeparatorSize + style.ItemSpacing.y + GetFrameHeightWithSpacing();
        if (BeginChild("ScrollingRegion", new ImVec2(0, -footer_height_to_reserve), ImGuiChildFlags_NavFlattened, ImGuiWindowFlags_HorizontalScrollbar))
        {
            if (BeginPopupContextWindow())
            {
                if (Selectable("Clear")) ClearLog();
                EndPopup();
            }

            // Display every line as a separate entry so we can change their color or add custom widgets.
            // If you only want raw text you can use ImGui::TextUnformatted(log.begin(), log.end());
            // NB- if you have thousands of entries this approach may be too inefficient and may require user-side clipping
            // to only process visible items. The clipper will automatically measure the height of your first item and then
            // "seek" to display only items in the visible area.
            // To use the clipper we can replace your standard loop:
            //      for (int i = 0; i < Items.Size; i++)
            //   With:
            //      ImGuiListClipper clipper;
            //      clipper.Begin(Items.Size);
            //      while (clipper.Step())
            //         for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            // - That your items are evenly spaced (same height)
            // - That you have cheap random access to your elements (you can access them given their index,
            //   without processing all the ones before)
            // You cannot this code as-is if a filter is active because it breaks the 'cheap random-access' property.
            // We would need random-access on the post-filtered list.
            // A typical application wanting coarse clipping and filtering may want to pre-compute an array of indices
            // or offsets of items that passed the filtering test, recomputing this array when user changes the filter,
            // and appending newly elements as they are inserted. This is left as a task to the user until we can manage
            // to improve this example code!
            // If your items are of variable height:
            // - Split them into same height items would be simpler and facilitate random-seeking into your list.
            // - Consider using manual call to IsRectVisible() and skipping extraneous decoration from your items.
            PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(4, 1)); // Tighten spacing
            if (copy_to_clipboard)
                LogToClipboard();
            foreach (string item in Items)
            {
                if (!Filter.PassFilter(item))
                    continue;

                // Normally you would store more information in your item than just a string.
                // (e.g. make Items[] an array of structure, store color/type etc.)
                ImVec4 color;
                bool has_color = false;
                if (item.Contains("[error]")) { color = new ImVec4(1.0f, 0.4f, 0.4f, 1.0f); has_color = true; }
                else if (item.StartsWith("# ", StringComparison.Ordinal)) { color = new ImVec4(1.0f, 0.8f, 0.6f, 1.0f); has_color = true; }
                else { color = default; }
                if (has_color)
                    PushStyleColor(ImGuiCol_Text, color);
                TextUnformatted(item);
                if (has_color)
                    PopStyleColor();
            }
            if (copy_to_clipboard)
                LogFinish();

            // Keep up at the bottom of the scroll region if we were already at the bottom at the beginning of the frame.
            // Using a scrollbar or mouse-wheel will take away from the bottom edge.
            if (ScrollToBottom || (AutoScroll && GetScrollY() >= GetScrollMaxY()))
                SetScrollHereY(1.0f);
            ScrollToBottom = false;

            PopStyleVar();
        }
        EndChild();
        Separator();

        // Command-line
        bool reclaim_focus = false;
        ImGuiInputTextFlags input_text_flags = ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_EscapeClearsAll | ImGuiInputTextFlags_CallbackCompletion | ImGuiInputTextFlags_CallbackHistory;
        if (InputText("Input", InputBuf, InputBuf.Length, input_text_flags, TextEditCallbackStub, this))
        {
            // C++: char* s = InputBuf;
            string s = GetCString(InputBuf);
            s = Strtrimblanks(s);
            if (s.Length != 0)
                ExecCommand(s);
            InputBuf[0] = 0;
            reclaim_focus = true;
        }

        // Auto-focus on window apparition
        SetItemDefaultFocus();
        if (reclaim_focus)
            SetKeyboardFocusHere(-1); // Auto focus previous widget

        End();
    }

    public void ExecCommand(string command_line)
    {
        AddLog("# %s\n", command_line);

        // Insert into history. First find match and delete it so it can be pushed to the back.
        // This isn't trying to be smart or optimal.
        HistoryPos = -1;
        for (int i = History.Count - 1; i >= 0; i--)
            if (Stricmp(History[i], command_line) == 0)
            {
                History.RemoveAt(i);
                break;
            }
        History.Add(command_line);

        // Process command
        if (Stricmp(command_line, "CLEAR") == 0)
        {
            ClearLog();
        }
        else if (Stricmp(command_line, "HELP") == 0)
        {
            AddLog("Commands:");
            for (int i = 0; i < Commands.Length; i++)
                AddLog("- %s", Commands[i]);
        }
        else if (Stricmp(command_line, "HISTORY") == 0)
        {
            int first = History.Count - 10;
            for (int i = first > 0 ? first : 0; i < History.Count; i++)
                AddLog("%3d: %s\n", i, History[i]);
        }
        else
        {
            AddLog("Unknown command: '%s'\n", command_line);
        }

        // On command input, we scroll to bottom even if AutoScroll==false
        ScrollToBottom = true;
    }

    // In C++11 you'd be better off using lambdas for this sort of forwarding callbacks
    public static int TextEditCallbackStub(ImGuiInputTextCallbackData data)
    {
        ExampleAppConsole console = (ExampleAppConsole)data.UserData;
        return console.TextEditCallback(data);
    }

    public int TextEditCallback(ImGuiInputTextCallbackData data)
    {
        //AddLog("cursor: %d, selection: %d-%d", data->CursorPos, data->SelectionStart, data->SelectionEnd);
        switch (data.EventFlag)
        {
            case ImGuiInputTextFlags_CallbackCompletion:
                {
                    // Example of TEXT COMPLETION

                    // Locate beginning of current word
                    // (C++ uses pointers into data->Buf; here we use byte offsets + Encoding.UTF8.GetString.)
                    int word_end = data.CursorPos;
                    int word_start = word_end;
                    while (word_start > 0)
                    {
                        char c = (char)data.Buf[word_start - 1];
                        if (c == ' ' || c == '\t' || c == ',' || c == ';')
                            break;
                        word_start--;
                    }

                    // Build a list of candidates
                    string word = Encoding.UTF8.GetString(data.Buf, word_start, word_end - word_start);
                    List<string> candidates = new List<string>();
                    for (int i = 0; i < Commands.Length; i++)
                        if (Strnicmp(Commands[i], word, word_end - word_start) == 0)
                            candidates.Add(Commands[i]);

                    if (candidates.Count == 0)
                    {
                        // No match
                        // C++: AddLog("No match for \"%.*s\"!\n", (int)(word_end - word_start), word_start);
                        // (the %.*s precision is applied by pre-slicing 'word' above)
                        AddLog("No match for \"%s\"!\n", word);
                    }
                    else if (candidates.Count == 1)
                    {
                        // Single match. Delete the beginning of the word and replace it entirely so we've got nice casing.
                        data.DeleteChars(word_start, word_end - word_start);
                        data.InsertChars(data.CursorPos, candidates[0]);
                        data.InsertChars(data.CursorPos, " ");
                    }
                    else
                    {
                        // Multiple matches. Complete as much as we can..
                        // So inputting "C"+Tab will complete to "CL" then display "CLEAR" and "CLASSIFY" as matches.
                        int match_len = word_end - word_start;
                        for (; ; )
                        {
                            int c = 0;
                            bool all_candidates_match = true;
                            for (int i = 0; i < candidates.Count && all_candidates_match; i++)
                                if (i == 0)
                                    c = UpperAt(candidates[i], match_len);
                                else if (c == 0 || c != UpperAt(candidates[i], match_len))
                                    all_candidates_match = false;
                            if (!all_candidates_match)
                                break;
                            match_len++;
                        }

                        if (match_len > 0)
                        {
                            data.DeleteChars(word_start, word_end - word_start);
                            // C++: data->InsertChars(data->CursorPos, candidates[0], candidates[0] + match_len);
                            data.InsertChars(data.CursorPos, candidates[0], match_len);
                        }

                        // List matches
                        AddLog("Possible matches:\n");
                        for (int i = 0; i < candidates.Count; i++)
                            AddLog("- %s\n", candidates[i]);
                    }

                    break;
                }
            case ImGuiInputTextFlags_CallbackHistory:
                {
                    // Example of HISTORY
                    int prev_history_pos = HistoryPos;
                    if (data.EventKey == ImGuiKey_UpArrow)
                    {
                        if (HistoryPos == -1)
                            HistoryPos = History.Count - 1;
                        else if (HistoryPos > 0)
                            HistoryPos--;
                    }
                    else if (data.EventKey == ImGuiKey_DownArrow)
                    {
                        if (HistoryPos != -1)
                            if (++HistoryPos >= History.Count)
                                HistoryPos = -1;
                    }

                    // A better implementation would preserve the data on the current input line along with cursor position.
                    if (prev_history_pos != HistoryPos)
                    {
                        string history_str = (HistoryPos >= 0) ? History[HistoryPos] : "";
                        data.DeleteChars(0, data.BufTextLen);
                        data.InsertChars(0, history_str);
                    }
                    break;
                }
        }
        return 0;
    }
}

//-----------------------------------------------------------------------------
// [SECTION] Example App: Debug Log / ShowExampleAppLog()
//-----------------------------------------------------------------------------

// Usage:
//  static ExampleAppLog my_log;
//  my_log.AddLog("Hello %d world\n", 123);
//  my_log.Draw("title");
//
// C++: struct ExampleAppLog (imgui_demo.cpp 9449-9570)
public class ExampleAppLog
{
    public ImGuiTextBuffer Buf = new ImGuiTextBuffer();
    public ImGuiTextFilter Filter = new ImGuiTextFilter();
    public List<int> LineOffsets = new List<int>(); // Index to lines offset. We maintain this with AddLog() calls.
    public bool AutoScroll;  // Keep scrolling if already at the bottom.

    public ExampleAppLog()
    {
        AutoScroll = true;
        Clear();
    }

    public void Clear()
    {
        Buf.clear();
        LineOffsets.Clear();
        LineOffsets.Add(0);
    }

    public void AddLog(string fmt, params object[] args)
    {
        int old_size = Buf.size();
        Buf.appendf(fmt, args);
        for (int new_size = Buf.size(); old_size < new_size; old_size++)
            if (Buf[old_size] == '\n')
                LineOffsets.Add(old_size + 1);
    }

    // C++: line_start = buf + LineOffsets[line_no];
    //      line_end = (line_no + 1 < LineOffsets.Size) ? (buf + LineOffsets[line_no + 1] - 1) : buf_end;
    // (the [line_start, line_end) range excludes the trailing '\n'). All text this demo logs is ASCII,
    // so the byte offsets stored in LineOffsets are also char offsets into the decoded string.
    string SliceLine(string buf, int line_no)
    {
        int line_start = Math.Min(LineOffsets[line_no], buf.Length);
        int line_end = (line_no + 1 < LineOffsets.Count) ? Math.Min(LineOffsets[line_no + 1] - 1, buf.Length) : buf.Length;
        if (line_end < line_start)
            line_end = line_start;
        return buf.Substring(line_start, line_end - line_start);
    }

    public void Draw(string title, ref bool p_open)
    {
        if (!Begin(title, ref p_open))
        {
            End();
            return;
        }

        // Options menu
        if (BeginPopup("Options"))
        {
            Checkbox("Auto-scroll", ref AutoScroll);
            EndPopup();
        }

        // Main window
        if (Button("Options"))
            OpenPopup("Options");
        SameLine();
        bool clear = Button("Clear");
        SameLine();
        bool copy = Button("Copy");
        SameLine();
        Filter.Draw("Filter", -100.0f);

        Separator();

        if (BeginChild("scrolling", new ImVec2(0, 0), ImGuiChildFlags_None, ImGuiWindowFlags_HorizontalScrollbar))
        {
            if (clear)
                Clear();
            if (copy)
                LogToClipboard();

            PushStyleVar(ImGuiStyleVar_ItemSpacing, new ImVec2(0, 0));
            // C++: const char* buf = Buf.begin(); const char* buf_end = Buf.end();
            string buf = Buf.c_str();
            if (Filter.IsActive())
            {
                // In this example we don't use the clipper when Filter is enabled.
                // This is because we don't have random access to the result of our filter.
                // A real application processing logs with ten of thousands of entries may want to store the result of
                // search/filter.. especially if the filtering function is not trivial (e.g. reg-exp).
                for (int line_no = 0; line_no < LineOffsets.Count; line_no++)
                {
                    string line = SliceLine(buf, line_no);
                    if (Filter.PassFilter(line))
                        TextUnformatted(line);
                }
            }
            else
            {
                // The simplest and easy way to display the entire buffer:
                //   ImGui::TextUnformatted(buf_begin, buf_end);
                // And it'll just work. TextUnformatted() has specialization for large blob of text and will fast-forward
                // to skip non-visible lines. Here we instead demonstrate using the clipper to only process lines that are
                // within the visible area.
                // If you have tens of thousands of items and their processing cost is non-negligible, coarse clipping them
                // on your side is recommended. Using ImGuiListClipper requires
                // - A) random access into your data
                // - B) items all being the  same height,
                // both of which we can handle since we have an array pointing to the beginning of each line of text.
                // When using the filter (in the block of code above) we don't have random access into the data to display
                // anymore, which is why we don't use the clipper. Storing or skimming through the search result would make
                // it possible (and would be recommended if you want to search through tens of thousands of entries).
                ImGuiListClipper clipper = new ImGuiListClipper();
                clipper.Begin(LineOffsets.Count);
                while (clipper.Step())
                {
                    for (int line_no = clipper.DisplayStart; line_no < clipper.DisplayEnd; line_no++)
                    {
                        string line = SliceLine(buf, line_no);
                        TextUnformatted(line);
                    }
                }
                clipper.End();
            }
            PopStyleVar();

            // Keep up at the bottom of the scroll region if we were already at the bottom at the beginning of the frame.
            // Using a scrollbar or mouse-wheel will take away from the bottom edge.
            if (AutoScroll && GetScrollY() >= GetScrollMaxY())
                SetScrollHereY(1.0f);
        }
        EndChild();
        End();
    }
}

public static partial class ImGui
{
    // C++: static void ShowExampleAppMainMenuBar()   (imgui_demo.cpp 8947)
    public static void ShowExampleAppMainMenuBar()
    {
        if (BeginMainMenuBar())
        {
            if (BeginMenu("File"))
            {
                ShowExampleMenuFile();
                EndMenu();
            }
            if (BeginMenu("Edit"))
            {
                if (MenuItem("Undo", "Ctrl+Z")) { }
                if (MenuItem("Redo", "Ctrl+Y", false, false)) { } // Disabled item
                Separator();
                if (MenuItem("Cut", "Ctrl+X")) { }
                if (MenuItem("Copy", "Ctrl+C")) { }
                if (MenuItem("Paste", "Ctrl+V")) { }
                EndMenu();
            }
            EndMainMenuBar();
        }
    }

    // Note that shortcuts are currently provided for display only
    // (future version will add explicit flags to BeginMenu() to request processing shortcuts)
    // C++: static void ShowExampleMenuFile()   (imgui_demo.cpp 8974)
    public static void ShowExampleMenuFile()
    {
        MenuItem("(demo menu)", null, false, false);
        if (MenuItem("New")) { }
        if (MenuItem("Open", "Ctrl+O")) { }
        if (BeginMenu("Open Recent"))
        {
            MenuItem("fish_hat.c");
            MenuItem("fish_hat.inl");
            MenuItem("fish_hat.h");
            if (BeginMenu("More.."))
            {
                MenuItem("Hello");
                MenuItem("Sailor");
                if (BeginMenu("Recurse.."))
                {
                    ShowExampleMenuFile();
                    EndMenu();
                }
                EndMenu();
            }
            EndMenu();
        }
        if (MenuItem("Save", "Ctrl+S")) { }
        if (MenuItem("Save As..")) { }

        Separator();
        if (BeginMenu("Options"))
        {
            MenuItem("Enabled", "", ref DemoApps1Locals.menu_file_enabled);
            BeginChild("child", new ImVec2(0, GetTextLineHeightWithSpacing() * 5.0f), ImGuiChildFlags_Borders);
            for (int i = 0; i < 10; i++)
                Text("Scrolling Text %d", i);
            EndChild();
            SliderFloat("Value", ref DemoApps1Locals.menu_file_f, 0.0f, 1.0f);
            InputFloat("Input", ref DemoApps1Locals.menu_file_f, 0.1f);
            Combo("Combo", ref DemoApps1Locals.menu_file_n, "Yes\0No\0Maybe\0\0");
            EndMenu();
        }

        if (BeginMenu("Colors"))
        {
            float sz = GetTextLineHeight();
            for (int i = 0; i < ImGuiCol_COUNT; i++)
            {
                string name = GetStyleColorName((ImGuiCol)i);
                ImVec2 p = GetCursorScreenPos();
                GetWindowDrawList().AddRectFilled(p, new ImVec2(p.x + sz, p.y + sz), GetColorU32((ImGuiCol)i));
                Dummy(new ImVec2(sz, sz));
                SameLine();
                MenuItem(name);
            }
            EndMenu();
        }

        // Here we demonstrate appending again to the "Options" menu (which we already created above)
        // Of course in this demo it is a little bit silly that this function calls BeginMenu("Options") twice.
        // In a real code-base using it would make senses to use this feature from very different code locations.
        if (BeginMenu("Options")) // <-- Append!
        {
            Checkbox("SomeOption", ref DemoApps1Locals.menu_file_b);
            EndMenu();
        }

        if (BeginMenu("Disabled", false)) // Disabled
        {
            System.Diagnostics.Debug.Assert(false); // IM_ASSERT(0)
        }
        if (MenuItem("Checked", null, true)) { }
        Separator();
        if (MenuItem("Quit", "Alt+F4")) { }
    }

    // Demonstrate creating a simple console window, with scrolling, filtering, completion and history.
    // C++: static void ShowExampleAppConsole(bool* p_open)   (imgui_demo.cpp 9413)
    public static void ShowExampleAppConsole(ref bool p_open)
    {
        DemoApps1Locals.console.Draw("Example: Console", ref p_open);
    }

    //-----------------------------------------------------------------------------
    // [SECTION] Example App: Image Viewer / ShowExampleAppImageViewer()
    //-----------------------------------------------------------------------------

    // C++: static void ShowExampleAppImageViewer(bool* p_open)   (imgui_demo.cpp 9423)
    public static void ShowExampleAppImageViewer(ref bool p_open)
    {
        ImFontAtlas atlas = GetIO().Fonts;
        ImTextureRef tex_ref = atlas.TexRef; // We don't have access to other textures in this demo!
        int tex_w = atlas.TexData.Width;
        int tex_h = atlas.TexData.Height;
        if (Begin("Example: Image Viewer", ref p_open))
        {
            ExampleImageViewerData image_viewer = DemoApps1Locals.image_viewer;
            ExampleImageViewer_DrawOptions(image_viewer);
            ImVec2 canvas_size = GetContentRegionAvail();
            ImVec2 canvas_min_size = IsWindowAppearing() ? new ImVec2(3.0f * tex_w, 4.0f * tex_h) : new ImVec2(1.0f, 1.0f);
            canvas_size = new ImVec2(ImMath.ImMax(canvas_size.x, canvas_min_size.x), ImMath.ImMax(canvas_size.y, canvas_min_size.y));
            ExampleImageViewer_DrawCanvas(image_viewer, canvas_size, tex_ref, tex_w, tex_h);
        }
        End();
    }

    // Demonstrate creating a simple log window with basic filtering.
    // C++: static void ShowExampleAppLog(bool* p_open)   (imgui_demo.cpp 9573)
    public static void ShowExampleAppLog(ref bool p_open)
    {
        ExampleAppLog log = DemoApps1Locals.log;

        // For the demo: add a debug button _BEFORE_ the normal log window contents
        // We take advantage of a rarely used feature: multiple calls to Begin()/End() are appending to the _same_ window.
        // Most of the contents of the window will be added by the log.Draw() call.
        SetNextWindowSize(new ImVec2(500, 400), ImGuiCond_FirstUseEver);
        Begin("Example: Log", ref p_open);
        if (SmallButton("[Debug] Add 5 entries"))
        {
            string[] categories = { "info", "warn", "error" };
            string[] words = { "Bumfuzzled", "Cattywampus", "Snickersnee", "Abibliophobia", "Absquatulate", "Nincompoop", "Pauciloquent" };
            for (int n = 0; n < 5; n++)
            {
                string category = categories[DemoApps1Locals.log_counter % categories.Length];
                string word = words[DemoApps1Locals.log_counter % words.Length];
                log.AddLog("[%05d] [%s] Hello, current time is %.1f, here's a word: '%s'\n",
                    GetFrameCount(), category, GetTime(), word);
                DemoApps1Locals.log_counter++;
            }
        }
        End();

        // Actually call in the regular Log helper (which will Begin() into the same window as we just did)
        log.Draw("Example: Log", ref p_open);
    }

    //-----------------------------------------------------------------------------
    // [SECTION] Example App: Simple Layout / ShowExampleAppLayout()
    //-----------------------------------------------------------------------------

    // Demonstrate create a window with multiple child windows.
    // C++: static void ShowExampleAppLayout(bool* p_open)   (imgui_demo.cpp 9608)
    public static void ShowExampleAppLayout(ref bool p_open)
    {
        SetNextWindowSize(new ImVec2(500, 440), ImGuiCond_FirstUseEver);
        if (Begin("Example: Simple layout", ref p_open, ImGuiWindowFlags_MenuBar))
        {
            if (BeginMenuBar())
            {
                if (BeginMenu("File"))
                {
                    if (MenuItem("Close", "Ctrl+W")) { p_open = false; }
                    EndMenu();
                }
                EndMenuBar();
            }

            // Left
            {
                BeginChild("left pane", new ImVec2(150, 0), ImGuiChildFlags_Borders | ImGuiChildFlags_ResizeX);
                for (int i = 0; i < 100; i++)
                {
                    string label = ImFmt.Sprintf("MyObject %d", i); // C++: sprintf(label, "MyObject %d", i)
                    if (Selectable(label, DemoApps1Locals.layout_selected == i, ImGuiSelectableFlags_SelectOnNav))
                        DemoApps1Locals.layout_selected = i;
                }
                EndChild();
            }
            SameLine();

            // Right
            {
                BeginGroup();
                BeginChild("item view", new ImVec2(0, -GetFrameHeightWithSpacing())); // Leave room for 1 line below us
                Text("MyObject: %d", DemoApps1Locals.layout_selected);
                Separator();
                if (BeginTabBar("##Tabs", ImGuiTabBarFlags_None))
                {
                    if (BeginTabItem("Description"))
                    {
                        TextWrapped("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. ");
                        EndTabItem();
                    }
                    if (BeginTabItem("Details"))
                    {
                        Text("ID: 0123456789");
                        EndTabItem();
                    }
                    EndTabBar();
                }
                EndChild();
                if (Button("Revert")) { }
                SameLine();
                if (Button("Save")) { }
                EndGroup();
            }
        }
        End();
    }
}
