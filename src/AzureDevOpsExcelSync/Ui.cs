partial class Program
{
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
        Console.WriteLine("==============================================================");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@$"    ____  ____  _____
   / _  || ___||  ___|
  / /_| ||  _|  \__ \
 /_/  |_||____|/____/ Azure DevOps Excel Sync {Assembly.GetExecutingAssembly().GetName().Version}
");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  /help for commands   /exit to quit");
        Console.WriteLine("==============================================================");
        Console.ResetColor();

        Console.WriteLine();
    }

    static void PrintPrompt()
    {
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
        Console.Write($"]8;;{url}\\{text}]8;;\\");

    static string Truncate(string s) => s.Length > 60 ? s[..57] + "..." : s;
    static string Truncate(string s, int max) => s.Length > max ? s[..(max - 1)] + "…" : s;
}
