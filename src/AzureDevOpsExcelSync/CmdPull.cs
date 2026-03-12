partial class Program
{
    // ════════════════════════════════════════════════════════════
    // /pull <id> [--out filename.xlsx]
    // Supports any process template — hierarchy detected structurally
    // ════════════════════════════════════════════════════════════
    static async Task CmdPull(string[] args)
    {
        if (!EnsureConfig()) return;
        if (args.Length == 0) { Warn("  Usage: /pull <work-item-id> [--out filename.xlsx]"); return; }
        if (!int.TryParse(args[0], out int id)) { Warn("  Work item ID must be a number."); return; }

        string defaultFile = Path.Combine(ExcelDir, $"workitem_{id}.xlsx");
        string outFile = Flag(args, "--out") ?? defaultFile;
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);

        var client = Connect();
        var root = await client.GetWorkItemAsync(id, expand: WorkItemExpand.Relations);
        var wiType = root.Fields.TryGetValue("System.WorkItemType", out var t) ? t?.ToString() ?? "" : "";
        var title = root.Fields["System.Title"]?.ToString() ?? "(no title)";

        Info($"  📥  Fetching {wiType} #{id}: \"{title}\"");

        var stories = new List<(WorkItem Story, List<WorkItem> Tasks)>();
        await CollectStories(client, root, stories);

        int totalTasks = stories.Sum(s => s.Tasks.Count);
        Info($"  📋  Found {stories.Count} parent item(s), {totalTasks} child item(s) total.");

        BuildExcelMulti(stories, outFile, _org!, _project!, wiType, title);

        string fullPath = Path.GetFullPath(outFile);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("\n  ✅  Saved → ");
        WriteLink(fullPath, new Uri(fullPath).AbsoluteUri);
        Console.WriteLine();
        Console.ResetColor();
        Hint($"  Edit the yellow cells, then run:  /push {outFile}");
    }

    // Recursively walks the hierarchy and collects (parent, children) pairs.
    // Detection is structural — no work item type names are assumed.
    static async Task CollectStories(WorkItemTrackingHttpClient client, WorkItem item,
        List<(WorkItem, List<WorkItem>)> stories)
    {
        var childRels = item.Relations?
            .Where(r => r.Rel == "System.LinkTypes.Hierarchy-Forward").ToList() ?? [];

        if (childRels.Count == 0)
        {
            var wiType = item.Fields.TryGetValue("System.WorkItemType", out var wt) ? wt?.ToString() ?? "Work Item" : "Work Item";
            stories.Add((item, new List<WorkItem>()));
            Info($"     📋  {wiType} #{item.Id}: \"{item.Fields["System.Title"]}\" (0 child item(s))");
            return;
        }

        // Fetch children WITH relations so we can inspect grandchildren in-memory
        var childrenFull = new List<WorkItem>();
        foreach (var rel in childRels)
        {
            var childId = int.Parse(rel.Url.Split('/').Last());
            childrenFull.Add(await client.GetWorkItemAsync(childId, expand: WorkItemExpand.Relations));
        }

        bool anyChildHasChildren = childrenFull.Any(c =>
            c.Relations?.Any(r => r.Rel == "System.LinkTypes.Hierarchy-Forward") == true);

        if (!anyChildHasChildren)
        {
            var wiType = item.Fields.TryGetValue("System.WorkItemType", out var wt) ? wt?.ToString() ?? "Work Item" : "Work Item";
            stories.Add((item, childrenFull));
            Info($"     📋  {wiType} #{item.Id}: \"{item.Fields["System.Title"]}\" ({childrenFull.Count} child item(s))");
        }
        else
        {
            foreach (var childFull in childrenFull)
                await CollectStories(client, childFull, stories);
        }
    }
}
