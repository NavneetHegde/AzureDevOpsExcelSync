partial class Program
{
    // ════════════════════════════════════════════════════════════
    // /clone <id>
    // Supports any work item type — recursively clones the full hierarchy
    // ════════════════════════════════════════════════════════════
    static async Task CmdClone(string[] args)
    {
        if (!EnsureConfig()) return;
        if (args.Length == 0) { Warn("  Usage: /clone <work-item-id>"); return; }
        if (!int.TryParse(args[0], out int id)) { Warn("  Work item ID must be a number."); return; }

        var client = Connect();
        var root = await client.GetWorkItemAsync(id, expand: WorkItemExpand.Relations);
        var wiType = root.Fields.TryGetValue("System.WorkItemType", out var wt) ? wt?.ToString() ?? "Work Item" : "Work Item";
        var title = root.Fields["System.Title"]?.ToString() ?? "(no title)";
        var children = await GetChildren(client, root);

        Info($"\n  About to clone:");
        Info($"  📋  {wiType} #{id}  \"{title}\"");
        Info($"  🔗  {children.Count} direct child item(s):\n");
        foreach (var c in children)
        {
            var cType = c.Fields.TryGetValue("System.WorkItemType", out var ct) ? ct?.ToString() ?? "" : "";
            Hint($"       • {cType} #{c.Id}  {c.Fields["System.Title"]}");
        }

        Console.Write("\n  Proceed? [y/N] ");
        var confirm = Console.ReadLine()?.Trim().ToLower();
        if (confirm != "y" && confirm != "yes") { Warn("  Cancelled."); return; }

        Info($"\n  📋  Cloning...");
        var created = new List<WorkItem>();
        var newRoot = await CloneRecursive(client, root, null, created);

        Console.WriteLine();
        Ok($"  🎉  Cloned → new {wiType} #{newRoot.Id} ({created.Count} item(s) total).");
        Hint($"  {_org!.TrimEnd('/')}/{Uri.EscapeDataString(_project!)}/_workitems/edit/{newRoot.Id}");
        Hint($"\n  Tip: /pull {newRoot.Id}  to open it in Excel.");
    }

    static async Task<WorkItem> CloneRecursive(WorkItemTrackingHttpClient client, WorkItem src, int? parentId, List<WorkItem> created)
    {
        var newItem = await CopyWorkItem(client, src, _project!);
        created.Add(newItem);

        var srcType = src.Fields.TryGetValue("System.WorkItemType", out var t) ? t?.ToString() ?? "Work Item" : "Work Item";
        var indent = parentId.HasValue ? "     " : "  ";
        Ok($"{indent}✅  {srcType} #{newItem.Id}: {newItem.Fields["System.Title"]}");

        if (parentId.HasValue)
            await LinkChild(client, parentId.Value, newItem.Id!.Value);

        var children = await GetChildren(client, src);
        foreach (var child in children)
        {
            var childFull = await client.GetWorkItemAsync(child.Id!.Value, expand: WorkItemExpand.Relations);
            await CloneRecursive(client, childFull, newItem.Id!.Value, created);
        }

        return newItem;
    }
}
