# AES Plugin Guide — Sample: Tag Search

This guide walks through building, installing, and using a third-party AES plugin. The sample plugin adds a `/tag` command that queries Azure DevOps and prints every work item that carries a given tag.

---

## How Plugins Work

1. You write a .NET class library that implements `IAesPlugin`.
2. You build it and drop the `.dll` into `~/.aes/plugins/`.
3. AES picks it up automatically on the next launch — no recompilation of AES required.
4. The command appears under the **Plugins** section in `/help`.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- AES installed as a global tool (so `AES.dll` is available to reference)
- Azure DevOps PAT already configured in AES (`/config pat <token>`)

---

## Step 1 — Create the project

```bash
mkdir TagPlugin
cd TagPlugin
dotnet new classlib -n TagPlugin -f net10.0
```

---

## Step 2 — Add NuGet references

The plugin needs the same Azure DevOps client packages AES uses, plus a reference to `AES.dll` so the compiler knows about `IAesPlugin` and `AesContext`.

Edit `TagPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Same ADO packages AES itself uses -->
    <PackageReference Include="Microsoft.TeamFoundationServer.Client" Version="19.*" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference AES.dll for IAesPlugin and AesContext -->
    <!-- Adjust the path to match where dotnet tools are installed on your machine -->
    <!-- Windows: %USERPROFILE%\.dotnet\tools\.store\azuredevopsexcelsync\...\AES.dll -->
    <!-- macOS/Linux: ~/.dotnet/tools/.store/azuredevopsexcelsync/.../AES.dll         -->
    <Reference Include="AES">
      <HintPath>$(USERPROFILE)\.dotnet\tools\.store\azuredevopsexcelsync\1.0.0\azuredevopsexcelsync\1.0.0\tools\net10.0\any\AES.dll</HintPath>
    </Reference>
  </ItemGroup>

</Project>
```

> Tip: run `dotnet tool list -g` to find the exact version folder, then locate `AES.dll` inside it.

---

## Step 3 — Write the plugin

Replace `Class1.cs` with `TagPlugin.cs`:

```csharp
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;

public class TagPlugin : IAesPlugin
{
    public string Command     => "tag";
    public string Description => "List all work items that carry a specific tag";

    // Usage: /tag <tag-name> [--type Task] [--state Active]
    public async Task ExecuteAsync(string[] args, AesContext ctx)
    {
        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Usage: /tag <tag-name> [--type <WorkItemType>] [--state <State>]");
            Console.ResetColor();
            return;
        }

        string tag       = args[0];
        string? typeFilter  = Flag(args, "--type");
        string? stateFilter = Flag(args, "--state");

        // Build WIQL query
        // Tags in ADO are semicolon-separated; Contains works for substring match
        var wiql = new StringBuilder();
        wiql.Append($"SELECT [System.Id], [System.Title], [System.WorkItemType], [System.State], [System.AssignedTo], [System.Tags]");
        wiql.Append($" FROM WorkItems");
        wiql.Append($" WHERE [System.TeamProject] = '{ctx.Project}'");
        wiql.Append($" AND [System.Tags] Contains '{tag}'");

        if (typeFilter  is not null) wiql.Append($" AND [System.WorkItemType] = '{typeFilter}'");
        if (stateFilter is not null) wiql.Append($" AND [System.State] = '{stateFilter}'");

        wiql.Append(" ORDER BY [System.Id]");

        var client = ctx.Connect();
        var result = await client.QueryByWiqlAsync(new Wiql { Query = wiql.ToString() });

        if (result.WorkItems is null || !result.WorkItems.Any())
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  No work items found with tag '{tag}'.");
            Console.ResetColor();
            return;
        }

        // Fetch full details in batches of 200 (ADO limit)
        var ids = result.WorkItems.Select(r => r.Id).ToList();
        var fields = new[]
        {
            "System.Id", "System.Title", "System.WorkItemType",
            "System.State", "System.AssignedTo", "System.Tags"
        };

        var items = new List<WorkItem>();
        for (int i = 0; i < ids.Count; i += 200)
        {
            var batch = ids.Skip(i).Take(200).ToList();
            var fetched = await client.GetWorkItemsAsync(batch, fields);
            items.AddRange(fetched);
        }

        // Print header
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Work items tagged '{tag}'  ({items.Count} found)");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {"ID",-8} {"Type",-18} {"State",-14} {"Assigned To",-22} Title");
        Console.WriteLine($"  {new string('─', 90)}");
        Console.ResetColor();

        // Print rows
        foreach (var item in items)
        {
            int    id       = (int)item.Id!;
            string type     = item.Fields.TryGetValue("System.WorkItemType", out var t)  ? t?.ToString() ?? "" : "";
            string state    = item.Fields.TryGetValue("System.State",        out var s)  ? s?.ToString() ?? "" : "";
            string title    = item.Fields.TryGetValue("System.Title",        out var ti) ? ti?.ToString() ?? "" : "";
            string assigned = item.Fields.TryGetValue("System.AssignedTo",   out var a)
                              ? (a as IdentityRef)?.DisplayName ?? a?.ToString() ?? ""
                              : "";

            // Colour by state
            Console.ForegroundColor = state switch
            {
                "Active"   or "In Progress" => ConsoleColor.Cyan,
                "Closed"   or "Done"        => ConsoleColor.DarkGray,
                "Resolved"                  => ConsoleColor.Green,
                _                           => ConsoleColor.White,
            };

            string shortTitle    = title.Length    > 45 ? title[..42]    + "..." : title;
            string shortAssigned = assigned.Length > 20 ? assigned[..17] + "..." : assigned;

            Console.WriteLine($"  {id,-8} {type,-18} {state,-14} {shortAssigned,-22} {shortTitle}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    // Minimal flag parser (mirrors AES internals)
    static string? Flag(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
    }
}
```

---

## Step 4 — Build and install

```bash
# From inside the TagPlugin folder
dotnet build -c Release -o ~/.aes/plugins/
```

This compiles `TagPlugin.dll` directly into the plugins directory.

---

## Step 5 — Use it

```
AES
  🔌  Plugin loaded: /tag  — List all work items that carry a specific tag

AES [MyProject] › /tag regression

  Work items tagged 'regression'  (3 found)
  ID       Type               State          Assigned To            Title
  ──────────────────────────────────────────────────────────────────────────────────────────
  4821     Bug                Active         Alice Johnson          Login fails after timeout
  4834     Task               In Progress    Bob Smith              Write regression test suite
  4901     Bug                Resolved       Alice Johnson          Export drops last row
```

Filter by type or state:

```
/tag regression --type Bug
/tag regression --state Active
/tag regression --type Bug --state Active
```

---

## AesContext Reference

| Member | Type | Description |
|--------|------|-------------|
| `Org` | `string` | Azure DevOps org URL, e.g. `https://dev.azure.com/myorg` |
| `Project` | `string` | Project name as configured in AES |
| `Pat` | `string` | Personal Access Token (decrypted for the session) |
| `Connect()` | `WorkItemTrackingHttpClient` | Opens a connection and returns the ADO work item client |

---

## IAesPlugin Contract

```csharp
public interface IAesPlugin
{
    string Command     { get; }   // without leading "/", e.g. "tag"
    string Description { get; }   // shown in /help under Plugins section
    Task ExecuteAsync(string[] args, AesContext context);
}
```

- `args` — everything after the command name, split on spaces (same as built-in commands)
- Throw any exception freely — AES catches it and prints an error without crashing

---

## Tips

- **Multiple plugins per DLL** — one assembly can contain as many `IAesPlugin` implementations as you like; AES registers them all.
- **Dependencies** — if your plugin depends on other NuGet packages, publish it as a self-contained folder (`dotnet publish -o ~/.aes/plugins/`) so all dependency DLLs land in the plugins directory.
- **Isolation** — each DLL loads in its own `AssemblyLoadContext`, so version conflicts between plugins are avoided.
- **No restart needed** — just copy the updated DLL and relaunch AES.
