using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Samples.PerformanceDemo;

class Program
{
    static void Main(string[] args)
    {
        var config = DefaultConfig.Instance;
        BenchmarkRunner.Run<DataAccessBenchmarks>(config);
    }
}