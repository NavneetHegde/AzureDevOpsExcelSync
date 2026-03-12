# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build src/AzureDevOpsExcelSync/AzureDevOpsExcelSync.csproj

# Run directly
dotnet run --project src/AzureDevOpsExcelSync/AzureDevOpsExcelSync.csproj

# Pack as global tool
dotnet pack src/AzureDevOpsExcelSync/AzureDevOpsExcelSync.csproj

# Install as global tool (after packing)
dotnet tool install --global --add-source ./src/AzureDevOpsExcelSync/nupkg AzureDevOpsExcelSync

# Update installed global tool
dotnet tool update --global --add-source ./src/AzureDevOpsExcelSync/nupkg AzureDevOpsExcelSync
```

There are no automated tests in this project.

## Architecture

This is a **multi-file .NET 10 console app** packaged as a dotnet global tool with command name `AES`. All logic is split across `partial class Program` files.

The app is an interactive REPL that syncs Azure DevOps work items with Excel spreadsheets.

### Commands

| Command | Function | Description |
|---------|----------|-------------|
| `/pull <id>` | `CmdPull()` | ADO work item hierarchy → formatted Excel file |
| `/push <file.xlsx>` | `CmdPush()` | Excel changes → Azure DevOps (dry-run first) |
| `/clone <id>` | `CmdClone()` | Duplicate work item + children in ADO |
| `/cost <id>` | `CmdCost()` | Display task estimates table in console |
| `/config <key> <value>` | `CmdConfig()` | Persist org URL, project, or PAT |
| `/status` | `CmdStatus()` | Show current configuration |
| `/<plugin>` | `IAesPlugin.ExecuteAsync()` | Any plugin command loaded from `~/.aes/plugins/` |

### Data Flow

- **Pull:** `CmdPull()` → `CollectStories()` → `BuildExcelMulti()` → `.xlsx` file with yellow editable / grey read-only cells
- **Push:** `CmdPush()` → `RunPush(dryRun=true)` (preview) → user confirmation → `RunPush(dryRun=false)` → `JsonPatchDocument` updates to ADO
- **Clone:** Copies all non-system-managed fields; retries with field skipping if ADO validation fails; prefixes title with `[COPY]`

### Key Dependencies

- **Microsoft.TeamFoundationServer.Client** — Azure DevOps REST API (`VssConnection`, `WorkItemTrackingHttpClient`)
- **ClosedXML** — Excel read/write
- **System.Security.Cryptography.ProtectedData** — PAT encrypted via Windows DPAPI; Unix uses chmod 600

### Plugin System

Plugins are .NET class libraries that implement `IAesPlugin` (defined in `AES.dll`). Drop compiled `.dll` files into `~/.aes/plugins/` and AES picks them up at startup.

```csharp
public class MyPlugin : IAesPlugin
{
    public string Command     => "report";
    public string Description => "Generate custom HTML report";
    public async Task ExecuteAsync(string[] args, AesContext ctx)
    {
        var client = ctx.Connect();   // WorkItemTrackingHttpClient
        // ...
    }
}
```

Key files:
- `IAesPlugin.cs` — plugin contract
- `AesContext.cs` — context with `Org`, `Project`, `Pat`, and `Connect()`
- `PluginLoader.cs` — scans `~/.aes/plugins/*.dll` at startup; each assembly loads in its own `AssemblyLoadContext`

### Configuration

Stored in `~/.aes/config/` (settings file + encrypted PAT file). Keys: `org`, `project`, `pat`.

### Editable Excel Fields

`System.Title`, `System.Description`, `System.State`, `System.AssignedTo`, `System.AreaPath`, `System.IterationPath`, `System.Tags`, `Microsoft.VSTS.Common.Priority`, and scheduling fields (StoryPoints, OriginalEstimate, RemainingWork, CompletedWork). All other fields are read-only.
