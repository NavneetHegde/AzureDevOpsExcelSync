partial class Program
{
    static string PluginDir => Path.Combine(AesRoot, "plugins");
    static readonly Dictionary<string, IAesPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);

    static void LoadPlugins()
    {
        if (!Directory.Exists(PluginDir)) return;

        foreach (var dll in Directory.GetFiles(PluginDir, "*.dll"))
        {
            try
            {
                var ctx = new AssemblyLoadContext(dll, isCollectible: false);
                var asm = ctx.LoadFromAssemblyPath(dll);
                foreach (var type in asm.GetTypes()
                    .Where(t => typeof(IAesPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
                {
                    var instance = Activator.CreateInstance(type);
                    if (instance is not IAesPlugin plugin) continue;
                    _plugins[plugin.Command.ToLower()] = plugin;
                    Hint($"  🔌  Plugin loaded: /{plugin.Command}  — {plugin.Description}");
                }
            }
            catch (Exception ex)
            {
                Warn($"  ⚠️  Could not load plugin '{Path.GetFileName(dll)}': {ex.Message}");
            }
        }
    }

    static AesContext BuildContext() => new()
    {
        Org     = _org!,
        Project = _project!,
        Pat     = _pat!
    };
}
