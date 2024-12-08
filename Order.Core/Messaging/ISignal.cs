namespace Order.Core.Messaging;

public interface ISignal
{
    void Wait(TimeSpan timeout, CancellationToken cancellationToken);
    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
    void Make();
    Task MakeAsync();
}