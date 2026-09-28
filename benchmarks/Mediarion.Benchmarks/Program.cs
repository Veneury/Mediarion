using BenchmarkDotNet.Running;

namespace Mediarion.Benchmarks
{
    /// <summary>
    /// Entry point. Run one scenario with <c>--filter *PipelineBenchmarks*</c>, or all of them
    /// with <c>--filter *</c>. Numbers meant for publication come from a dedicated machine, never
    /// from a shared runner.
    /// </summary>
    public static class Program
    {
        /// <remarks>
        /// Two modes. Without arguments, or with BenchmarkDotNet's own, it runs benchmarks. With
        /// <c>--budget</c> it reads a finished run instead and checks it against
        /// <c>benchmarks/baseline.json</c>, which is what CI does after running the pipeline
        /// scenario.
        /// </remarks>
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--budget")
            {
                string baseline = args.Length > 1 ? args[1] : "benchmarks/baseline.json";
                string results = args.Length > 2 ? args[2] : "BenchmarkDotNet.Artifacts/results";

                return Budget.Check(baseline, results);
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            return 0;
        }
    }
}
