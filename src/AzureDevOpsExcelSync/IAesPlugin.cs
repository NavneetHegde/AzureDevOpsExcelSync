public interface IAesPlugin
{
    string Command     { get; }
    string Description { get; }
    Task ExecuteAsync(string[] args, AesContext context);
}
