partial class Program
{
    // ════════════════════════════════════════════════════════════
    // Interactive slash-command picker
    // ════════════════════════════════════════════════════════════

    record PickerEntry(string Command, string Usage, string Description);

    const int MaxPickerRows = 8;

    static readonly PickerEntry[] BuiltinCommands =
    [
        new("/pull",   "/pull <id>",            "Fetch work item hierarchy into Excel"),
        new("/push",   "/push <file.xlsx>",     "Push Excel edits back to Azure DevOps"),
        new("/clone",  "/clone <id>",           "Duplicate work item + children"),
        new("/cost",   "/cost <id>",            "Show task estimates table"),
        new("/config", "/config <key> <value>", "Set org / project / PAT"),
        new("/status", "/status",               "Show current configuration"),
        new("/help",   "/help",                 "Show all commands"),
        new("/clear",  "/clear",                "Clear the screen"),
        new("/exit",   "/exit",                 "Exit AES"),
    ];

    static List<PickerEntry> GetFilteredCommands(string input)
    {
        if (string.IsNullOrEmpty(input) || input[0] != '/')
            return [];

        string filter = input.ToLowerInvariant();
        return BuiltinCommands
            .Concat(_plugins.Values.Select(p => new PickerEntry($"/{p.Command}", $"/{p.Command}", p.Description)))
            .Where(e => e.Command.ToLowerInvariant().StartsWith(filter))
            .ToList();
    }

    static void DrawPickerLines(int promptRow, List<PickerEntry> entries, int selIndex)
    {
        int count = Math.Min(entries.Count, MaxPickerRows);
        int width = Math.Max(40, Math.Min(Console.WindowWidth - 1, 72));

        for (int i = 0; i < count; i++)
        {
            Console.SetCursorPosition(0, promptRow + 1 + i);
            var e = entries[i];

            if (i == selIndex)
            {
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }

            string line = $"  {e.Usage,-30} {e.Description}";
            if (line.Length >= width)
                line = line[..(width - 1)] + "…";
            Console.Write(line.PadRight(width));
            Console.ResetColor();
        }
    }

    static void ErasePickerLines(int promptRow, int count)
    {
        if (count <= 0) return;
        int width = Console.WindowWidth;
        for (int i = 0; i < count; i++)
        {
            Console.SetCursorPosition(0, promptRow + 1 + i);
            Console.Write(new string(' ', width));
        }
    }

    // Redraws picker based on current buf; returns new pickerCount.
    static int UpdatePicker(int promptRow, int promptCol, StringBuilder buf, ref int selIndex, int prevCount, bool resetSel)
    {
        var filtered = GetFilteredCommands(buf.ToString());

        if (filtered.Count > 0)
        {
            if (resetSel)
                selIndex = 0;
            else
                selIndex = Math.Clamp(selIndex, 0, filtered.Count - 1);

            int newCount = Math.Min(filtered.Count, MaxPickerRows);
            DrawPickerLines(promptRow, filtered, selIndex);

            // Erase leftover rows from a previously taller picker
            for (int i = newCount; i < prevCount; i++)
            {
                Console.SetCursorPosition(0, promptRow + 1 + i);
                Console.Write(new string(' ', Console.WindowWidth));
            }

            Console.SetCursorPosition(promptCol + buf.Length, promptRow);
            return newCount;
        }
        else
        {
            ErasePickerLines(promptRow, prevCount);
            Console.SetCursorPosition(promptCol + buf.Length, promptRow);
            return 0;
        }
    }

    static string? ReadLineWithPicker()
    {
        // Fall back gracefully when stdin is not an interactive terminal
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var buf = new StringBuilder();
        int promptRow = Console.CursorTop;
        int promptCol = Console.CursorLeft;
        int selIndex = 0;
        int pickerCount = 0;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                {
                    if (pickerCount > 0)
                    {
                        var filtered = GetFilteredCommands(buf.ToString());
                        if (filtered.Count > 0 && selIndex < filtered.Count)
                        {
                            // Auto-complete and submit immediately
                            ErasePickerLines(promptRow, pickerCount);
                            Console.SetCursorPosition(promptCol, promptRow);
                            Console.Write(new string(' ', buf.Length));
                            Console.SetCursorPosition(promptCol, promptRow);
                            var selected = filtered[selIndex].Command;
                            Console.Write(selected);
                            Console.WriteLine();
                            return selected;
                        }
                    }
                    ErasePickerLines(promptRow, pickerCount);
                    Console.WriteLine();
                    return buf.ToString();
                }

                case ConsoleKey.Tab:
                {
                    if (pickerCount > 0)
                    {
                        var filtered = GetFilteredCommands(buf.ToString());
                        if (filtered.Count > 0 && selIndex < filtered.Count)
                        {
                            ErasePickerLines(promptRow, pickerCount);
                            pickerCount = 0;
                            Console.SetCursorPosition(promptCol, promptRow);
                            Console.Write(new string(' ', buf.Length));
                            Console.SetCursorPosition(promptCol, promptRow);
                            buf.Clear();
                            buf.Append(filtered[selIndex].Command).Append(' ');
                            Console.Write(buf.ToString());
                        }
                    }
                    break;
                }

                case ConsoleKey.Backspace:
                {
                    if (buf.Length > 0)
                    {
                        buf.Remove(buf.Length - 1, 1);
                        int curRow = Console.CursorTop;
                        int curCol = Console.CursorLeft;
                        if (curCol > 0)
                        {
                            Console.SetCursorPosition(curCol - 1, curRow);
                            Console.Write(' ');
                            Console.SetCursorPosition(curCol - 1, curRow);
                        }
                        pickerCount = UpdatePicker(promptRow, promptCol, buf, ref selIndex, pickerCount, resetSel: false);
                    }
                    break;
                }

                case ConsoleKey.UpArrow:
                {
                    if (pickerCount > 0)
                    {
                        var filtered = GetFilteredCommands(buf.ToString());
                        if (filtered.Count > 0)
                        {
                            selIndex = (selIndex - 1 + filtered.Count) % filtered.Count;
                            DrawPickerLines(promptRow, filtered, selIndex);
                            Console.SetCursorPosition(promptCol + buf.Length, promptRow);
                        }
                    }
                    break;
                }

                case ConsoleKey.DownArrow:
                {
                    if (pickerCount > 0)
                    {
                        var filtered = GetFilteredCommands(buf.ToString());
                        if (filtered.Count > 0)
                        {
                            selIndex = (selIndex + 1) % filtered.Count;
                            DrawPickerLines(promptRow, filtered, selIndex);
                            Console.SetCursorPosition(promptCol + buf.Length, promptRow);
                        }
                    }
                    break;
                }

                case ConsoleKey.Escape:
                {
                    if (pickerCount > 0)
                    {
                        ErasePickerLines(promptRow, pickerCount);
                        pickerCount = 0;
                        Console.SetCursorPosition(promptCol + buf.Length, promptRow);
                    }
                    break;
                }

                default:
                {
                    if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        ErasePickerLines(promptRow, pickerCount);
                        Console.WriteLine();
                        return null;
                    }

                    if (!char.IsControl(key.KeyChar))
                    {
                        buf.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                        pickerCount = UpdatePicker(promptRow, promptCol, buf, ref selIndex, pickerCount, resetSel: true);
                    }
                    break;
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    // UI & console helpers
    // ════════════════════════════════════════════════════════════
    static (string cmd, string[] args) ParseLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], parts.Skip(1).ToArray());
    }

    static string? Flag(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
    }

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("================================================================");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@$"    ____  ____  _____
   / _  || ___||  ___|
  / /_| ||  _|  \__ \
 /_/  |_||____|/____/ Azure DevOps Excel Sync {SemVer()}
");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  /help for commands   /exit to quit");
        Console.WriteLine("================================================================");
        Console.ResetColor();

        Console.WriteLine();
    }

    static void PrintPrompt()
    {
        string rule = "  " + new string('─', Math.Min(Console.WindowWidth - 3, 62));

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(rule);

        int promptRow = Console.CursorTop;

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  AES");
        if (_project != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" [{_project}]");
        }
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write(" › ");
        Console.ResetColor();

        int promptLen = Console.CursorLeft;

        // Print bottom rule then move cursor back to the prompt line for input
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(rule);
        Console.ResetColor();
        Console.SetCursorPosition(promptLen, promptRow);
    }

    static void PrintHelp()
    {
        Console.WriteLine();
        Section("Work Items");
        HelpLine("/pull  <id> [--out file.xlsx]", "Fetch Epic/Feature/User Story + all tasks into Excel");
        HelpLine("/push  <file.xlsx> [--force]", "Push Excel edits back to Azure DevOps");
        HelpLine("/clone <id>", "Duplicate a work item and its full child hierarchy");
        HelpLine("/cost  <id>", "Show task estimates — original, remaining, done");
        Console.WriteLine();
        Section("Configuration  (saved to ~/.aes/config)");
        HelpLine("/config org     <url>", "Set Azure DevOps org URL");
        HelpLine("/config project <name>", "Set project name");
        HelpLine("/config pat     <token>", "Set Personal Access Token");
        HelpLine("/config", "Show current config");
        HelpLine("/status", "Show connection details");
        Console.WriteLine();
        Section("Session");
        HelpLine("/clear", "Clear the screen");
        HelpLine("/help", "Show this help");
        HelpLine("/exit", "Exit AES");
        if (_plugins.Count > 0)
        {
            Console.WriteLine();
            Section("Plugins");
            foreach (var p in _plugins.Values.OrderBy(p => p.Command))
                HelpLine($"/{p.Command}", p.Description);
        }
        Console.WriteLine();
    }

    static void Section(string title)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  {title}");
        Console.ResetColor();
    }

    static void HelpLine(string cmd, string desc)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"    {cmd,-36}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(desc);
        Console.ResetColor();
    }

    static void Info(string s)  { Console.ForegroundColor = ConsoleColor.White;    Console.WriteLine(s); Console.ResetColor(); }
    static void Ok(string s)    { Console.ForegroundColor = ConsoleColor.Green;    Console.WriteLine(s); Console.ResetColor(); }
    static void Warn(string s)  { Console.ForegroundColor = ConsoleColor.Yellow;   Console.WriteLine(s); Console.ResetColor(); }
    static void Err(string s)   { Console.ForegroundColor = ConsoleColor.Red;      Console.WriteLine(s); Console.ResetColor(); }
    static void Hint(string s)  { Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine(s); Console.ResetColor(); }
    static void Cyan(string s)  { Console.ForegroundColor = ConsoleColor.DarkCyan; Console.WriteLine(s); Console.ResetColor(); }
    static string DimText(string s) => s;

    static readonly HashSet<string> NumericFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.VSTS.Scheduling.OriginalEstimate",
        "Microsoft.VSTS.Scheduling.RemainingWork",
        "Microsoft.VSTS.Scheduling.CompletedWork",
        "Microsoft.VSTS.Scheduling.StoryPoints",
        "Microsoft.VSTS.Common.Priority",
    };

    static bool IsNumericField(string field) => NumericFields.Contains(field);

    // Sort and trim tags so "bug; regression" == "regression; bug"
    static string NormaliseTags(string raw) =>
        string.Join("; ", raw.Split(';')
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase));

    // OSC 8 hyperlink — works in Windows Terminal, iTerm2, modern terminals
    // Falls back gracefully in plain cmd.exe (just prints the text)
    static void WriteLink(string text, string url) =>
        Console.Write($"\x1b]8;;{url}\x1b\\{text}\x1b]8;;\x1b\\");

    static string SemVer()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    static string Truncate(string s) => s.Length > 60 ? s[..57] + "..." : s;
    static string Truncate(string s, int max) => s.Length > max ? s[..(max - 1)] + "…" : s;
}
