namespace Order.Handler;

public class OrderFilesSettings
{
    public int MaxDegreeOfParallelism { get; set; }
    public int SignalTimeoutSeconds { get; set; }
    public int HttpClientTimeoutSeconds { get; set; }
}