partial class Program
{
    // ════════════════════════════════════════════════════════════
    // /config  <org|project|pat|estimate|remaining|completed> <value>
    // ════════════════════════════════════════════════════════════
    static void CmdConfig(string[] args)
    {
        if (args.Length == 0) { CmdStatus(); return; }
        if (args.Length < 2) { Warn("  Usage: /config <org|project|pat|estimate|remaining|completed> <value>"); return; }

        switch (args[0].ToLower())
        {
            case "org":       _org = args[1]; SaveConfig(); break;
            case "project":   _project = string.Join(" ", args.Skip(1)); SaveConfig(); break;
            case "pat":       SavePat(args[1]); break;
            case "estimate":  _fieldEstimate  = string.Join(" ", args.Skip(1)); SaveConfig(); break;
            case "remaining": _fieldRemaining = string.Join(" ", args.Skip(1)); SaveConfig(); break;
            case "completed": _fieldCompleted = string.Join(" ", args.Skip(1)); SaveConfig(); break;
            default: Warn($"  Unknown key '{args[0]}'. Use: org | project | pat | estimate | remaining | completed"); return;
        }
        Ok($"  ✅  {args[0]} saved{(args[0].ToLower() == "pat" ? " securely in OS credential store" : "")}.");
    }

    static void CmdStatus()
    {
        Console.WriteLine();
        Console.WriteLine($"  {"Org",-10}  {(_org ?? DimText("(not set)"))}");
        Console.WriteLine($"  {"Project",-10}  {(_project ?? DimText("(not set)"))}");
        Console.WriteLine($"  {"PAT",-10}  {(_pat != null ? "●●●●●●●●" : DimText("(not set)"))}");
        Console.WriteLine($"  {"Estimate",-10}  {_fieldEstimate}");
        Console.WriteLine($"  {"Remaining",-10}  {_fieldRemaining}");
        Console.WriteLine($"  {"Completed",-10}  {_fieldCompleted}");
        Console.WriteLine();
    }

    // ════════════════════════════════════════════════════════════
    // Config persistence  ~/.aes/config
    // ════════════════════════════════════════════════════════════
    static readonly string PatFile = Path.Combine(AesRoot, "config", ".pat");

    static void SaveConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        // PAT is stored in OS credential store — never written to disk
        File.WriteAllLines(ConfigPath,
        [
            $"org={_org ?? ""}",
            $"project={_project ?? ""}",
            $"estimate={_fieldEstimate}",
            $"remaining={_fieldRemaining}",
            $"completed={_fieldCompleted}"
        ]);
    }

    static void SavePat(string pat)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PatFile)!);

        if (OperatingSystem.IsWindows())
        {
            // Encrypt with DPAPI — only your Windows account can decrypt it
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(pat);
            var encryptedBytes = System.Security.Cryptography.ProtectedData.Protect(
                plainBytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            File.WriteAllBytes(PatFile, encryptedBytes);
        }
        else
        {
            // macOS/Linux — chmod 600 so only owner can read
            File.WriteAllText(PatFile, pat);
            File.SetUnixFileMode(PatFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        _pat = pat;
    }

    static string? LoadPat()
    {
        if (!File.Exists(PatFile)) return null;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var encryptedBytes = File.ReadAllBytes(PatFile);
                var plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                    encryptedBytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return System.Text.Encoding.UTF8.GetString(plainBytes);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                Warn("  ⚠️  Could not decrypt PAT — please run /config pat <your-pat> again.");
                return null;
            }
        }
        else
        {
            return File.ReadAllText(PatFile).Trim();
        }
    }

    static void LoadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;
                switch (parts[0])
                {
                    case "org":       _org = parts[1]; break;
                    case "project":   _project = parts[1]; break;
                    case "estimate":  if (!string.IsNullOrWhiteSpace(parts[1])) _fieldEstimate  = parts[1]; break;
                    case "remaining": if (!string.IsNullOrWhiteSpace(parts[1])) _fieldRemaining = parts[1]; break;
                    case "completed": if (!string.IsNullOrWhiteSpace(parts[1])) _fieldCompleted = parts[1]; break;
                }
            }
        }
        _pat = LoadPat();
    }

    static bool EnsureConfig()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_org)) missing.Add("org");
        if (string.IsNullOrWhiteSpace(_project)) missing.Add("project");
        if (string.IsNullOrWhiteSpace(_pat)) missing.Add("pat");
        if (missing.Count == 0) return true;

        Warn($"  ⚠️  Missing config: {string.Join(", ", missing)}");
        Hint("  Set with:  /config org https://dev.azure.com/myorg");
        Hint("             /config project MyProject");
        Hint("             /config pat YOUR_PAT");
        return false;
    }
}
