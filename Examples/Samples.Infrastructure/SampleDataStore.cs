using Flowtex.DataAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Samples.Infrastructure;

public class SampleDataStore : DataStoreBase
{
    private readonly SampleDbContext _context;

    public SampleDataStore(SampleDbContext context)
    {
        _context = context;
    }

    protected override DbContext Context => _context;

    protected override DbSet<T> GetDbSet<T>() => _context.Set<T>();
}