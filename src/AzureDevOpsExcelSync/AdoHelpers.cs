partial class Program
{
    // ════════════════════════════════════════════════════════════
    // ADO helpers
    // ════════════════════════════════════════════════════════════
    static WorkItemTrackingHttpClient Connect()
    {
        var conn = new VssConnection(new Uri(_org!), new VssBasicCredential(string.Empty, _pat));
        return conn.GetClient<WorkItemTrackingHttpClient>();
    }

    static async Task<List<WorkItem>> GetChildren(WorkItemTrackingHttpClient client, WorkItem parent)
    {
        var rels = parent.Relations?.Where(r => r.Rel == "System.LinkTypes.Hierarchy-Forward").ToList() ?? [];
        var children = new List<WorkItem>();
        foreach (var rel in rels)
            children.Add(await client.GetWorkItemAsync(int.Parse(rel.Url.Split('/').Last())));
        return children;
    }

    static async Task<WorkItem> CopyWorkItem(WorkItemTrackingHttpClient client, WorkItem src, string project)
    {
        var type = src.Fields.TryGetValue("System.WorkItemType", out var wt) ? wt?.ToString() ?? "Task" : "Task";
        var patch = BuildPatch(src);

        // Retry loop — if ADO rejects a field, skip it and try again
        // Handles read-only fields that vary across process templates
        while (true)
        {
            try
            {
                return await client.CreateWorkItemAsync(patch, project, type);
            }
            catch (Microsoft.TeamFoundation.WorkItemTracking.WebApi.RuleValidationException ex)
            {
                // Typical formats:
                //   "Invalid field status 'ReadOnly' for field 'System.BoardColumn'"
                //   "The field 'Reason' contains the value..."
                var field = ExtractFieldFromError(ex.Message);
                if (field == null) throw; // unknown format — rethrow

                SkipOnCopy.Add(field); // remember for subsequent items in this clone
                var removed = patch.RemoveAll(op => op.Path.Equals($"/fields/{field}", StringComparison.OrdinalIgnoreCase));
                if (removed == 0) throw; // field not in patch — would loop forever

                Hint($"  ⚠️  Skipped read-only field '{field}' — retrying...");
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to create work item of type '{type}': {e.Message}", e);
            }
        }
    }

    static string? ExtractFieldFromError(string message)
    {
        // "Invalid field status 'ReadOnly' for field 'System.BoardColumn'."
        var m = System.Text.RegularExpressions.Regex.Match(message, @"field '([^']+)'\.?$");
        if (m.Success) return m.Groups[1].Value;

        // "The field 'Reason' contains the value..."
        m = System.Text.RegularExpressions.Regex.Match(message, @"field '([^']+)' contains");
        if (m.Success) return m.Groups[1].Value;

        return null;
    }

    static JsonPatchDocument BuildPatch(WorkItem src)
    {
        var patch = new JsonPatchDocument();
        foreach (var kvp in src.Fields)
        {
            if (SkipOnCopy.Contains(kvp.Key) || kvp.Value == null) continue;
            string val = kvp.Key == "System.AssignedTo"
                ? (kvp.Value as IdentityRef)?.UniqueName ?? kvp.Value.ToString()!
                : kvp.Value.ToString()!;
            if (string.IsNullOrWhiteSpace(val)) continue;
            patch.Add(new JsonPatchOperation { Operation = Operation.Add, Path = $"/fields/{kvp.Key}", Value = val });
        }

        var titleOp = patch.FirstOrDefault(p => p.Path == "/fields/System.Title");
        if (titleOp != null) titleOp.Value = $"[COPY] {titleOp.Value}";

        // Always start as New
        var stateOp = patch.FirstOrDefault(p => p.Path == "/fields/System.State");
        if (stateOp != null) stateOp.Value = "New";
        else patch.Add(new JsonPatchOperation { Operation = Operation.Add, Path = "/fields/System.State", Value = "New" });

        return patch;
    }

    static async Task LinkChild(WorkItemTrackingHttpClient client, int parentId, int childId)
    {
        var patch = new JsonPatchDocument {
            new JsonPatchOperation {
                Operation = Operation.Add, Path = "/relations/-",
                Value = new WorkItemRelation {
                    Rel = "System.LinkTypes.Hierarchy-Forward",
                    Url = $"{_org!.TrimEnd('/')}/_apis/wit/workItems/{childId}"
                }
            }
        };
        await client.UpdateWorkItemAsync(patch, parentId);
    }
}
