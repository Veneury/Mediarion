using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mediarion;
using Microsoft.Extensions.DependencyInjection;

namespace MigrationSample
{
    /// <summary>
    /// One layer, configured twice: once on MediatR 12.5.0 and once on Mediarion, over a domain
    /// that knows about neither. Both are driven through the same script and the run is a pass
    /// only when they agree line for line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The readme says a migration is mostly a change of namespace. This is that claim written
    /// so a machine can fail it: the two layers are compared as source as well as by what they
    /// do, and the only lines allowed to differ are the <c>using</c> and the namespace.
    /// </para>
    /// <para>
    /// What it does not prove, said plainly: matching shapes is a smaller claim than "your
    /// migration will be easy". It says nothing about your build, your container, or thirty
    /// handlers written by somebody who does not know both libraries.
    /// </para>
    /// </remarks>
    public static class Program
    {
        public static async Task<int> Main()
        {
            var failures = new List<string>();

            IReadOnlyList<string> original = await RunOriginal();
            IReadOnlyList<string> migrated = await RunMigrated();

            Compare(failures, original, migrated);
            CompareSource(failures);

            if (failures.Count > 0)
            {
                Console.Error.WriteLine();

                foreach (string failure in failures)
                {
                    Console.Error.WriteLine("  " + failure);
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine("The two configurations do not agree.");
                return 1;
            }

            Console.WriteLine("Both configurations agree, and the layers differ by two lines.");
            return 0;
        }

        private static async Task<IReadOnlyList<string>> RunOriginal()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Prices>();
            services.AddSingleton<Trace>();

            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(typeof(Program).Assembly);
                configuration.AddOpenBehavior(typeof(Original.Timing<,>));
                configuration.AddBehavior<Original.Guarding>();
            });

            ServiceProvider provider = services.BuildServiceProvider();

            return await Original.Script.Run(
                provider.GetRequiredService<MediatR.ISender>(),
                provider.GetRequiredService<Trace>());
        }

        private static async Task<IReadOnlyList<string>> RunMigrated()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Prices>();
            services.AddSingleton<Trace>();

            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssembly(typeof(Program).Assembly);
                configuration.AddOpenBehavior(typeof(Migrated.Timing<,>));
                configuration.AddBehavior<Migrated.Guarding>();
            });

            ServiceProvider provider = services.BuildServiceProvider();

            return await Migrated.Script.Run(
                provider.GetRequiredService<Mediarion.ISender>(),
                provider.GetRequiredService<Trace>());
        }

        private static void Compare(
            List<string> failures,
            IReadOnlyList<string> original,
            IReadOnlyList<string> migrated)
        {
            Console.WriteLine("What the layer did, on each library:");
            Console.WriteLine();

            int lines = Math.Max(original.Count, migrated.Count);

            for (int i = 0; i < lines; i++)
            {
                string left = i < original.Count ? original[i] : "(nothing)";
                string right = i < migrated.Count ? migrated[i] : "(nothing)";
                bool same = string.Equals(left, right, StringComparison.Ordinal);

                Console.WriteLine("  " + (same ? " " : "!") + " " + left.PadRight(34) + right);

                if (!same)
                {
                    failures.Add("step " + i.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        ": MediatR said '" + left + "' and Mediarion said '" + right + "'");
                }
            }

            Console.WriteLine();
        }

        /// <remarks>
        /// The source of both layers is embedded in the assembly so the comparison can be made
        /// here rather than asserted in a readme. Two lines are allowed to differ, and they are
        /// named: anything else is a failure, including a difference that would make the
        /// behavioural comparison above pass for the wrong reason.
        /// </remarks>
        private static void CompareSource(List<string> failures)
        {
            string[] original = Read("Original").Split('\n');
            string[] migrated = Read("Migrated").Split('\n');

            if (original.Length != migrated.Length)
            {
                failures.Add("the two layers are not the same length: " +
                    original.Length + " lines against " + migrated.Length);
                return;
            }

            var differing = new List<string>();

            for (int i = 0; i < original.Length; i++)
            {
                if (!string.Equals(original[i], migrated[i], StringComparison.Ordinal))
                {
                    differing.Add(original[i].Trim() + "  ->  " + migrated[i].Trim());
                }
            }

            Console.WriteLine("Every line that differs between the two layers:");
            Console.WriteLine();

            foreach (string line in differing)
            {
                Console.WriteLine("  " + line);
            }

            Console.WriteLine();

            if (differing.Count != 2)
            {
                failures.Add("expected the two layers to differ by two lines, found " +
                    differing.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static string Read(string which)
        {
            Assembly assembly = typeof(Program).Assembly;
            string name = assembly.GetManifestResourceNames()
                .Single(candidate => candidate.EndsWith(which + ".Layer.cs", StringComparison.Ordinal));

            using Stream stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd().Replace("\r\n", "\n");
        }
    }
}
