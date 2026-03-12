partial class Program
{
    // ════════════════════════════════════════════════════════════
    // /push <file.xlsx> [--force]
    // ════════════════════════════════════════════════════════════
    static async Task CmdPush(string[] args)
    {
        if (!EnsureConfig()) return;
        if (args.Length == 0) { Warn("  Usage: /push <file.xlsx> [--force]"); return; }

        string file = args[0];
        bool force = args.Contains("--force");

        if (!File.Exists(file)) { Err($"  File not found: {file}"); return; }

        var client = Connect();

        if (!force)
        {
            Info("  🔍  Previewing changes...\n");
            bool hasChanges = await RunPush(client, file, dryRun: true);
            if (!hasChanges) { Ok("  ✅  Nothing to push — already in sync."); return; }

            Console.Write("\n  Push these changes to Azure DevOps? [y/N] ");
            var answer = Console.ReadLine()?.Trim().ToLower();
            if (answer != "y" && answer != "yes") { Warn("  Cancelled."); return; }
        }

        Info("\n  📤  Pushing...\n");
        await RunPush(client, file, dryRun: false);
    }

    static async Task<bool> RunPush(WorkItemTrackingHttpClient client, string file, bool dryRun)
    {
        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheet("Work Items");
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        int changes = 0, errors = 0;

        for (int row = 2; row <= lastRow; row++)
        {
            if (!int.TryParse(ws.Cell(row, 1).GetString(), out int wiId)) continue;

            WorkItem current;
            try { current = await client.GetWorkItemAsync(wiId); }
            catch (Exception ex) { Warn($"  ⚠️  Cannot fetch WI #{wiId}: {ex.Message} — skipping."); errors++; continue; }

            var patch = new JsonPatchDocument();
            var rowChanges = new List<(string Field, string Before, string After)>();

            for (int i = 0; i < EditableFields.Length; i++)
            {
                string field = EditableFields[i];
                string newVal = ws.Cell(row, i + 4).GetString().Trim();
                string currentVal = current.Fields.TryGetValue(field, out var cv) ? cv?.ToString() ?? "" : "";

                if (field == "System.AssignedTo" && current.Fields.TryGetValue(field, out var raw))
                    currentVal = (raw as IdentityRef)?.DisplayName ?? raw?.ToString() ?? "";

                bool changed = field == "System.Tags"
                    ? NormaliseTags(newVal) != NormaliseTags(currentVal)
                    : newVal != currentVal;

                if (!changed) continue;

                rowChanges.Add((FriendlyHeaders[i], currentVal, newVal));

                object? patchValue = field == "System.Tags"
                    ? (string.IsNullOrWhiteSpace(newVal) ? null : (object)NormaliseTags(newVal))
                    : string.IsNullOrWhiteSpace(newVal)
                        ? (IsNumericField(field) ? (object)"0" : null)
                        : (object)newVal;

                patch.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = $"/fields/{field}",
                    Value = patchValue
                });
            }

            if (patch.Count == 0) { Hint($"  WI #{wiId}  — no changes"); continue; }

            PrintChangeTable(wiId, rowChanges);

            if (!dryRun)
            {
                try
                {
                    await client.UpdateWorkItemAsync(patch, wiId);
                    Ok($"  ✅  WI #{wiId} updated ({patch.Count} field(s)).");
                    changes++;
                }
                catch (Exception ex) { Err($"  ❌  WI #{wiId}: {ex.Message}"); errors++; }
            }
            else { changes++; }
        }

        string label = dryRun ? "Preview" : "Done";
        string verb  = dryRun ? "would change" : "updated";
        Console.WriteLine($"\n  {label} — {changes} item(s) {verb}, {errors} error(s).");
        return changes > 0;
    }

    static void PrintChangeTable(int wiId, List<(string Field, string Before, string After)> rowChanges)
    {
        const int fieldW = 20;
        int available = Math.Max(72, Math.Min(Console.WindowWidth - 4, 140));
        int dataW = (available - fieldW - 7) / 2;   // 7 = borders + padding

        string topBar = $"  ┌{Bar(fieldW)}┬{Bar(dataW)}┬{Bar(dataW)}┐";
        string sepBar = $"  ├{Bar(fieldW)}┼{Bar(dataW)}┼{Bar(dataW)}┤";
        string botBar = $"  └{Bar(fieldW)}┴{Bar(dataW)}┴{Bar(dataW)}┘";

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\n  WI #{wiId}");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(topBar);

        // Header row
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  │ {"Field".PadRight(fieldW)} │ {"Before".PadRight(dataW)} │ {"After".PadRight(dataW)} │");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(sepBar);

        foreach (var (field, before, after) in rowChanges)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  │ ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(Truncate(field, fieldW).PadRight(fieldW));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(" │ ");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write(Truncate(before, dataW).PadRight(dataW));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(" │ ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(Truncate(after, dataW).PadRight(dataW));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(" │");
        }

        Console.WriteLine(botBar);
        Console.ResetColor();
    }

    static string Bar(int width) => new string('─', width + 2);
}
