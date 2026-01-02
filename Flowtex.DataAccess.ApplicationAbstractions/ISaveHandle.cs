
namespace Flowtex.DataAccess.Application.Abstractions;

public interface ISaveHandle
{
    Task<int> SaveAsync(CancellationToken ct = default);
}