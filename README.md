# AES — Azure DevOps Excel Sync

An interactive CLI tool that syncs Azure DevOps work items with Excel. Pull a work item hierarchy into a spreadsheet, edit it locally, then push changes back — with a dry-run preview before anything is written.

Works with any Azure DevOps process template: **Agile, Scrum, CMMI, or custom**. No configuration needed — hierarchy structure is detected automatically at runtime.

## Installation

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Clone and pack
git clone <repo-url>
cd AzureDevOpsExcelSync
dotnet pack src/AzureDevOpsExcelSync/AzureDevOpsExcelSync.csproj

# Install as a global tool
dotnet tool install --global --add-source ./src/AzureDevOpsExcelSync/nupkg AzureDevOpsExcelSync
```

To update an existing installation:

```bash
dotnet tool update --global --add-source ./src/AzureDevOpsExcelSync/nupkg AzureDevOpsExcelSync
```

## Quick Start

```
AES
```

On first run, configure your Azure DevOps connection:

```
/config org     https://dev.azure.com/your-org
/config project YourProjectName
/config pat     your-personal-access-token
```

Settings are stored in `~/.aes/config/settings`. The PAT is stored separately — see [PAT Storage & Security](#pat-storage--security) below.

## Commands

| Command | Description |
|---------|-------------|
| `/pull <id> [--out file.xlsx]` | Fetch a work item and its full hierarchy into an Excel file |
| `/push <file.xlsx> [--force]` | Preview and push Excel changes back to Azure DevOps |
| `/clone <id>` | Duplicate a work item and all its children |
| `/cost <id>` | Display an estimates table (original / remaining / completed hours) |
| `/config <key> <value>` | Set a configuration value (see [Configuration](#configuration)) |
| `/status` | Show all current configuration values |
| `/help` | List all commands (including loaded plugins) |
| `/clear` | Clear the screen |
| `/exit` | Exit the tool |

## Workflow Example

```bash
# Pull Epic #1234 and all children into Excel
/pull 1234

# Open ~/.aes/excel/workitem_1234.xlsx, edit the yellow cells, save

# Preview what will change, then confirm to push
/push ~/.aes/excel/workitem_1234.xlsx
```

The Excel file has **yellow cells** for editable fields and **grey cells** for read-only metadata. Editable fields are:

- Title, Description, State, Assigned To
- Area Path, Iteration Path, Tags, Priority
- Story Points, Original Estimate, Remaining Work, Completed Work

## Supported Hierarchy

`/pull` works with any two-level hierarchy. It detects structure automatically — no type names are assumed:

```
Epic
└── Feature
    └── <Parent Item>        ← e.g. User Story, Product Backlog Item, Requirement
        └── <Child Item>     ← e.g. Task
```

If the work item you pull has no children, it is treated as a standalone item. If a child has its own children, AES recurses deeper until it reaches the two-level boundary.

The resulting Excel workbook has one sheet per parent item, plus a combined **Work Items** sheet used by `/push`.

## Configuration

All settings are stored in `~/.aes/config/settings` and can be viewed with `/status`.

| Key | Description | Default |
|-----|-------------|---------|
| `org` | Azure DevOps organisation URL | _(not set)_ |
| `project` | Project name | _(not set)_ |
| `pat` | Personal access token (stored securely) | _(not set)_ |
| `estimate` | Field name used as Original Estimate in `/cost` | `Microsoft.VSTS.Scheduling.OriginalEstimate` |
| `remaining` | Field name used as Remaining Work in `/cost` | `Microsoft.VSTS.Scheduling.RemainingWork` |
| `completed` | Field name used as Completed Work in `/cost` | `Microsoft.VSTS.Scheduling.CompletedWork` |

### Customising `/cost` fields

If your process template uses different field names for hour tracking, override the defaults:

```
/config estimate  My.Custom.EstimateField
/config remaining My.Custom.RemainingField
/config completed My.Custom.CompletedField
```

Run `/status` at any time to confirm what fields are in use.

## PAT Requirements

Your Personal Access Token needs **Work Items (Read & Write)** scope in Azure DevOps.

To create one: Azure DevOps → User Settings → Personal Access Tokens → New Token.

## PAT Storage & Security

The PAT is stored separately from other settings and never written to the general config file.

**Windows** — encrypted using [Windows DPAPI](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection) (`DataProtectionScope.CurrentUser`). The encrypted bytes are saved to `~/.aes/config/.pat`. Only the Windows user account that encrypted it can decrypt it — other users on the same machine, or the same file copied to another machine, cannot be used to recover the PAT.

**macOS / Linux** — stored as plain text in `~/.aes/config/.pat` with file permissions set to `600` (owner read/write only), so other OS users cannot read it.

In both cases, the PAT is held in memory only for the duration of the session and is never logged or echoed to the terminal.

## Plugin System

AES supports third-party plugins. Create a .NET class library that references `AES.dll`, implement `IAesPlugin`, and drop the compiled `.dll` into `~/.aes/plugins/`. AES picks it up automatically on the next launch — no recompilation of AES required.

```csharp
public class ReportPlugin : IAesPlugin
{
    public string Command     => "report";
    public string Description => "Generate custom HTML report";

    public async Task ExecuteAsync(string[] args, AesContext ctx)
    {
        var client = ctx.Connect();   // WorkItemTrackingHttpClient
        // use ADO client freely
    }
}
```

```bash
# Build your plugin
dotnet build MyPlugin.csproj -o ~/.aes/plugins/

# Launch AES — plugin loads automatically
AES
  🔌  Plugin loaded: /report  — Generate custom HTML report
```

Loaded plugins appear under a **Plugins** section in `/help`. Each plugin receives an `AesContext` with the configured `Org`, `Project`, `Pat`, and a `Connect()` helper that returns a ready-to-use `WorkItemTrackingHttpClient`.

## License

MIT — © Navneet Hegde
