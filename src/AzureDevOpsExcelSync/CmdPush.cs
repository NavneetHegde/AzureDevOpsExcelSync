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

            for (int i = 0; i < EditableFields.Length; i++)
            {
                string field = EditableFields[i];
                string newVal = ws.Cell(row, i + 4).GetString().Trim();
                string currentVal = current.Fields.TryGetValue(field, out var cv) ? cv?.ToString() ?? "" : "";

                if (field == "System.AssignedTo" && current.Fields.TryGetValue(field, out var raw))
                    currentVal = (raw as IdentityRef)?.DisplayName ?? raw?.ToString() ?? "";

                // Normalise tags for comparison — sort and trim so order doesn't matter
                if (field == "System.Tags")
                {
                    var normNew = NormaliseTags(newVal);
                    var normCurrent = NormaliseTags(currentVal);
                    if (normNew == normCurrent) continue;
                }
                else if (newVal == currentVal) continue;

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  WI #{wiId}  [{FriendlyHeaders[i]}]");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    before: {Truncate(currentVal)}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"    after:  {Truncate(newVal)}");
                Console.ResetColor();

                // Tags: send normalised value so ADO fully replaces (not appends)
                object? patchValue = field == "System.Tags"
                    ? (string.IsNullOrWhiteSpace(newVal) ? null : (object)NormaliseTags(newVal))
                    : string.IsNullOrWhiteSpace(newVal)
                        ? (IsNumericField(field) ? (object)"0" : null)
                        : (object)newVal;

                patch.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,   // Replace (not Add) ensures full overwrite
                    Path = $"/fields/{field}",
                    Value = patchValue
                });
            }

            if (patch.Count == 0) { Hint($"  WI #{wiId}  — no changes"); continue; }

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
        string verb = dryRun ? "would change" : "updated";
        Console.WriteLine($"\n  {label} — {changes} item(s) {verb}, {errors} error(s).");
        return changes > 0;
    }
}
