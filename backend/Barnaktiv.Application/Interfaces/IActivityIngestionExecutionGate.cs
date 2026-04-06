namespace Barnaktiv.Application.Interfaces;

public interface IActivityIngestionExecutionGate
{
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken);
}
