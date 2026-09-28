using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Mediarion.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Mediarion.SourceGenerator.Tests
{
    /// <summary>
    /// Runs the generator over a snippet of source in memory, so a test can look at what it
    /// wrote and at what it complained about.
    /// </summary>
    internal static class GeneratorHarness
    {
        private static readonly MetadataReference[] References = LoadReferences();

        internal static GeneratorOutcome Run(string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "GeneratorTest",
                new[] { CSharpSyntaxTree.ParseText(source) },
                References,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));

            GeneratorDriver driver = CSharpGeneratorDriver.Create(new MediatorGenerator());

            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out Compilation updated,
                out ImmutableArray<Diagnostic> _);

            GeneratorDriverRunResult result = driver.GetRunResult();

            string generated = string.Join(
                Environment.NewLine,
                result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

            var problems = new List<Diagnostic>(result.Diagnostics);

            // The generated source has to compile as well as exist. A generator that writes
            // something plausible and invalid is the failure worth catching, and a test that
            // only looks at the text it produced would miss it.
            problems.AddRange(updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            return new GeneratorOutcome(generated, problems);
        }

        private static MetadataReference[] LoadReferences()
        {
            var assemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

            return assemblies
                .Split(Path.PathSeparator)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToArray();
        }
    }

    internal sealed class GeneratorOutcome
    {
        internal GeneratorOutcome(string generated, List<Diagnostic> problems)
        {
            Generated = generated;
            Problems = problems;
        }

        internal string Generated { get; }

        internal List<Diagnostic> Problems { get; }

        internal bool Reported(string id) => Problems.Any(p => p.Id == id);

        internal string Report() => string.Join(Environment.NewLine, Problems.Select(p => p.ToString()));
    }
}
