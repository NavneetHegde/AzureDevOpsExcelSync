// AES — Azure DevOps Excel Sync
// Interactive CLI: type 'AES' to open a session, then use /pull /push /clone /config /cost /help /exit

partial class Program
{
    // ── State ────────────────────────────────────────────────────
    static string? _org;
    static string? _project;
    static string? _pat;
    static string _fieldEstimate  = "Microsoft.VSTS.Scheduling.OriginalEstimate";
    static string _fieldRemaining = "Microsoft.VSTS.Scheduling.RemainingWork";
    static string _fieldCompleted = "Microsoft.VSTS.Scheduling.CompletedWork";

    // ── Paths ────────────────────────────────────────────────────
    static readonly string AesRoot    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aes");
    static readonly string ConfigPath = Path.Combine(AesRoot, "config", "settings");
    static readonly string ExcelDir   = Path.Combine(AesRoot, "excel");

    // ── Field definitions ────────────────────────────────────────
    static readonly string[] EditableFields =
    [
        "System.Title", "System.Description", "System.State", "System.AssignedTo",
        "System.AreaPath", "System.IterationPath", "Microsoft.VSTS.Common.Priority",
        "Microsoft.VSTS.Scheduling.StoryPoints", "Microsoft.VSTS.Scheduling.OriginalEstimate",
        "Microsoft.VSTS.Scheduling.RemainingWork", "Microsoft.VSTS.Scheduling.CompletedWork", "System.Tags"
    ];

    static readonly string[] FriendlyHeaders =
    [
        "Title", "Description", "State", "Assigned To", "Area Path", "Iteration Path",
        "Priority", "Story Points", "Original Estimate", "Remaining Work", "Completed Work", "Tags"
    ];

    static readonly HashSet<string> SkipOnCopy = new(StringComparer.OrdinalIgnoreCase)
    {
        // System-assigned — ADO generates these automatically
        "System.Id", "System.Rev", "System.Watermark", "System.PersonId",
        "System.TeamProject", "System.NodeName",
        "System.AreaId",       // numeric ID — AreaPath is copied instead
        "System.IterationId",  // numeric ID — IterationPath is copied instead

        // Audit/timestamps — should reflect clone time, not original
        "System.CreatedDate", "System.CreatedBy",
        "System.ChangedDate", "System.ChangedBy",
        "System.AuthorizedDate", "System.AuthorizedAs",
        "System.RevisedDate",

        // Link & attachment counts — ADO recalculates automatically
        "System.AttachedFileCount", "System.HyperLinkCount",
        "System.ExternalLinkCount", "System.RelatedLinkCount",
        "System.CommentCount",

        // State-transition — ADO sets based on State, we force State to New
        "System.State", "System.Reason",

        // Board-managed — controlled by the board engine, read-only via API
        "System.BoardColumn", "System.BoardColumnDone", "System.BoardLane",

        // History/activity — not relevant on a new item
        "Microsoft.VSTS.Common.StateChangeDate",
        "Microsoft.VSTS.Common.ActivatedDate", "Microsoft.VSTS.Common.ActivatedBy",
        "Microsoft.VSTS.Common.ResolvedDate",  "Microsoft.VSTS.Common.ResolvedBy",
        "Microsoft.VSTS.Common.ClosedDate",    "Microsoft.VSTS.Common.ClosedBy",
    };

    // ════════════════════════════════════════════════════════════
    // ENTRY POINT — interactive REPL loop
    // ════════════════════════════════════════════════════════════
    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        LoadConfig();
        LoadPlugins();

        var updateTask = GetLatestVersionAsync("AzureDevOpsExcelSync");

        PrintBanner();

        string? latestStr = null;
        try { latestStr = await updateTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* timeout or offline */ }
        if (latestStr != null && Version.TryParse(latestStr, out var latest))
        {
            var current = Assembly.GetExecutingAssembly().GetName().Version;
            if (current != null && latest > current)
                Warn($"  Update available: v{latest}  →  dotnet tool update -g AzureDevOpsExcelSync");
        }

        while (true)
        {
            PrintPrompt();
            var line = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var (cmd, args) = ParseLine(line);

            try
            {
                switch (cmd.ToLower())
                {
                    case "/pull":   await CmdPull(args); break;
                    case "/push":   await CmdPush(args); break;
                    case "/clone":  await CmdClone(args); break;
                    case "/cost":   await CmdCost(args); break;
                    case "/config": CmdConfig(args); break;
                    case "/status": CmdStatus(); break;
                    case "/help":   PrintHelp(); break;
                    case "/clear":  Console.Clear(); PrintBanner(); break;
                    case "/exit":
                    case "/quit":
                        Cyan("\n  Bye! 👋\n"); return;
                    default:
                        if (_plugins.TryGetValue(cmd.ToLower(), out var plugin))
                        {
                            if (!EnsureConfig()) break;
                            await plugin.ExecuteAsync(args, BuildContext());
                        }
                        else
                            Warn($"  Unknown command '{cmd}'. Type /help for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Err($"  ❌  {ex.Message}");
            }

            Console.WriteLine();
        }
    }
}
