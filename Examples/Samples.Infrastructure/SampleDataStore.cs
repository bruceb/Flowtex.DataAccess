using Flowtex.DataAccess.Infrastructure;
using Flowtex.DataAccess.Abstractions;
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
}