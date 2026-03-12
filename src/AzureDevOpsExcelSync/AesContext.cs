public class AesContext
{
    public required string Org     { get; init; }
    public required string Project { get; init; }
    public required string Pat     { get; init; }

    public WorkItemTrackingHttpClient Connect()
    {
        var conn = new VssConnection(new Uri(Org), new VssBasicCredential(string.Empty, Pat));
        return conn.GetClient<WorkItemTrackingHttpClient>();
    }
}
