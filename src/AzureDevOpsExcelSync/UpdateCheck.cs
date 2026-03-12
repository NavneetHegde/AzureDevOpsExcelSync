using System.Text.Json;

partial class Program
{
    static async Task<string?> GetLatestVersionAsync(string packageId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var http = new HttpClient();
            var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
            var json = await http.GetStringAsync(url, cts.Token);
            using var doc = JsonDocument.Parse(json);
            var versions = doc.RootElement.GetProperty("versions");
            int count = versions.GetArrayLength();
            return count > 0 ? versions[count - 1].GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
