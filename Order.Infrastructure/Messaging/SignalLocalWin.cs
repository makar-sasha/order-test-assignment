using Order.Core.Messaging;

namespace Order.Infrastructure.Messaging;

public class SignalLocalWin(string semaphoreName = "OrderProcessingSemaphore") : ISignal
{
    private readonly Semaphore _semaphore = OperatingSystem.IsWindows() ? 
        new(0, 1, semaphoreName) : new(0 , 1);

    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Wait(TimeSpan timeout, CancellationToken cancellationToken)
    {
        _semaphore.WaitOne(timeout);
    }

    public void Make()
    {
        _semaphore.Release(); 
    }
    
    public Task MakeAsync()
    {
        throw new NotImplementedException();
    }
}