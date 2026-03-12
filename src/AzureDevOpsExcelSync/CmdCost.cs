partial class Program
{
    // ════════════════════════════════════════════════════════════
    // /cost <id>  — works at any level: Epic → Feature → Story → Task
    // Hour fields are configurable via /config estimate|remaining|completed
    // ════════════════════════════════════════════════════════════
    static async Task CmdCost(string[] args)
    {
        if (!EnsureConfig()) return;
        if (args.Length == 0) { Warn("  Usage: /cost <work-item-id>"); return; }
        if (!int.TryParse(args[0], out int id)) { Warn("  Work item ID must be a number."); return; }

        var client = Connect();
        var root = await client.GetWorkItemAsync(id, expand: WorkItemExpand.Relations);
        var wiType = root.Fields.TryGetValue("System.WorkItemType", out var t) ? t?.ToString() ?? "" : "";
        var title = root.Fields["System.Title"]?.ToString() ?? "(no title)";

        Info($"  📊  Fetching estimates for {wiType} #{id}: \"{title}\"");

        var stories = new List<(WorkItem Story, List<WorkItem> Tasks)>();
        await CollectStories(client, root, stories);

        if (stories.Count == 0) { Warn("  No work items found."); return; }

        const string Sep = "  ─────────────────────────────────────────────────────────────-─────-";
        const int TCol = 30;  // title column width

        double grandOrig = 0, grandRemain = 0, grandDone = 0;

        Console.WriteLine();

        foreach (var (story, tasks) in stories)
        {
            // ── Parent item header ─────────────────────────────
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"  #{story.Id}  {Truncate(story.Fields["System.Title"]?.ToString() ?? "", 55)}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(Sep);
            Console.WriteLine("  " + "Item".PadRight(8) + "  " + "Title".PadRight(TCol) + "  " + "Orig".PadLeft(6) + "  " + "Remain".PadLeft(7) + "  " + "Done".PadLeft(6));
            Console.WriteLine(Sep);
            Console.ResetColor();

            if (tasks.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  (no tasks)");
                Console.ResetColor();
            }

            double storyOrig = 0, storyRemain = 0, storyDone = 0;

            foreach (var task in tasks)
            {
                int tid = task.Id!.Value;
                string ttitle = task.Fields["System.Title"]?.ToString() ?? "(no title)";
                double orig   = GetDouble(task, _fieldEstimate);
                double remain = GetDouble(task, _fieldRemaining);
                double done   = GetDouble(task, _fieldCompleted);

                storyOrig += orig;
                storyRemain += remain;
                storyDone += done;

                ConsoleColor remainColor = remain <= 0 ? ConsoleColor.Green
                                         : done > 0 ? ConsoleColor.Yellow
                                         : ConsoleColor.White;

                Console.ForegroundColor = ConsoleColor.DarkGray;
                var taskUrl = $"{_org!.TrimEnd('/')}/{Uri.EscapeDataString(_project!)}/_workitems/edit/{tid}";
                Console.Write("  ");
                WriteLink($"#{tid,-7}", taskUrl);
                Console.Write("  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(Truncate(ttitle, TCol).PadRight(TCol) + "  ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"{orig,5:0.#}h  ");
                Console.ForegroundColor = remainColor;
                Console.Write($"{remain,5:0.#}h  ");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"{done,4:0.#}h");
                Console.ResetColor();
            }

            // ── Subtotal ───────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(Sep);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  " + "Subtotal".PadRight(TCol + 12));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"{storyOrig,5:0.#}h  ");
            Console.ForegroundColor = storyRemain <= 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.Write($"{storyRemain,5:0.#}h  ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"{storyDone,4:0.#}h");
            Console.ResetColor();

            // ── Progress bar ───────────────────────────────────
            double storyPct = storyOrig > 0 ? (storyDone / storyOrig) * 100 : 0;
            int filled = (int)Math.Clamp(storyPct / 5, 0, 20);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Progress  ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("[" + new string('█', filled) + new string('░', 20 - filled) + "]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {storyPct:0}%");
            Console.ResetColor();
            Console.WriteLine();

            grandOrig += storyOrig;
            grandRemain += storyRemain;
            grandDone += storyDone;
        }

        // ── Grand total (only shown if more than one parent) ───
        if (stories.Count > 1)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {"═",61}".Replace('═', '═'));
            Console.WriteLine($"  GRAND TOTAL  ({stories.Count} parent item(s), {stories.Sum(s => s.Tasks.Count)} child item(s))");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(Sep);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  " + "".PadRight(TCol + 12));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"{grandOrig,5:0.#}h  ");
            Console.ForegroundColor = grandRemain <= 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.Write($"{grandRemain,5:0.#}h  ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"{grandDone,4:0.#}h");
            Console.ResetColor();

            double grandPct = grandOrig > 0 ? (grandDone / grandOrig) * 100 : 0;
            int gFilled = (int)Math.Clamp(grandPct / 5, 0, 20);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Overall    ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("[" + new string('█', gFilled) + new string('░', 20 - gFilled) + "]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {grandPct:0}%");
            Console.ResetColor();
        }
    }

    static double GetDouble(WorkItem wi, string field) =>
        wi.Fields.TryGetValue(field, out var v) && v != null &&
        double.TryParse(v.ToString(), out double d) ? d : 0;
}
