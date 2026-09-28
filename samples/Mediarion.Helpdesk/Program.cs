using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mediarion;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk
{
    /// <summary>
    /// A small application that uses the library the way an application would, rather than the
    /// way a test does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A unit test calls one thing and checks one answer. What a sample is for is the join
    /// between the parts — a container, a pipeline with an open-generic behaviour and a closed
    /// one that refuses, a pre-processor, a handler that publishes a notification two handlers
    /// take, a request with nothing to give back — over one configuration, in one run.
    /// </para>
    /// <para>
    /// It checks its own journal and exits non-zero when a line is wrong, so CI running it is
    /// worth something. Everything it prints, it also asserts.
    /// </para>
    /// </remarks>
    public static class Program
    {
        public static async Task<int> Main()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Tickets>();
            services.AddSingleton<Journal>();

            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<Tickets>();
                configuration.AutoRegisterRequestProcessors = true;

                // Outermost first: the log wraps everything, and validation sits inside it so a
                // refused request still shows up as having arrived.
                configuration.AddOpenBehavior(typeof(Logging<,>));
                configuration.AddBehavior<Validation>();
            });

            using ServiceProvider provider = services.BuildServiceProvider();

            ISender sender = provider.GetRequiredService<ISender>();
            Journal journal = provider.GetRequiredService<Journal>();

            int urgent = await sender.Send(new OpenTicket
            {
                Subject = "Printer is on fire",
                Reporter = "ada",
                Priority = Priority.Urgent,
            });

            int ordinary = await sender.Send(new OpenTicket
            {
                Subject = "Mouse is slow",
                Reporter = "bob",
                Priority = Priority.Normal,
            });

            int refused = await sender.Send(new OpenTicket { Reporter = "eve" });

            await sender.Send(new AssignTicket { Ticket = urgent, To = "grace" });

            bool closedUrgent = await sender.Send(new CloseTicket { Ticket = urgent });
            bool closedOrdinary = await sender.Send(new CloseTicket { Ticket = ordinary });

            int open = await sender.Send(new CountOpenTickets());

            Console.WriteLine("What the application did:");
            Console.WriteLine();

            foreach (string line in journal.Lines)
            {
                Console.WriteLine("  " + line);
            }

            Console.WriteLine();

            var failures = new List<string>();

            Check(failures, "the urgent ticket was opened", urgent, 1);
            Check(failures, "the ordinary ticket was opened", ordinary, 2);
            Check(failures, "the ticket with no subject was refused", refused, 0);
            Check(failures, "an assigned ticket closes", closedUrgent, true);
            Check(failures, "an unassigned ticket does not", closedOrdinary, false);
            Check(failures, "one ticket is left open", open, 1);

            Check(
                failures,
                "the journal reads as expected",
                string.Join(" | ", journal.Lines),
                string.Join(" | ", Expected));

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            if (failures.Count > 0)
            {
                return 1;
            }

            Console.WriteLine(journal.Lines.Count + " steps, all of them where they should be.");
            return 0;
        }

        /// <remarks>
        /// <para>
        /// Every step the run should take, in order. A journal is a better assertion than six
        /// separate ones: it catches a behaviour that stopped wrapping, a pre-processor that
        /// stopped running and a notification handler that quietly did nothing, none of which
        /// change any of the answers above.
        /// </para>
        /// <para>
        /// Note where the pre-processor sits: "received from" comes before the log opens, not
        /// inside it. Processors run outside the behaviours the application adds, which is the
        /// documented order and is easier to believe written out than described.
        /// </para>
        /// </remarks>
        private static readonly string[] Expected =
        {
            "received from ada",
            "-> OpenTicket",
            "emailed about 1",
            "paged for 1",
            "<- OpenTicket",
            "received from bob",
            "-> OpenTicket",
            "emailed about 2",
            "<- OpenTicket",
            "received from eve",
            "-> OpenTicket",
            "rejected a ticket with no subject",
            "<- OpenTicket",
            "-> AssignTicket",
            "assigned 1 to grace",
            "<- AssignTicket",
            "-> CloseTicket",
            "closed 1",
            "<- CloseTicket",
            "-> CloseTicket",
            "refused to close 2",
            "<- CloseTicket",
            "-> CountOpenTickets",
            "<- CountOpenTickets",
        };

        private static void Check<T>(List<string> failures, string what, T actual, T expected)
        {
            if (!Equals(actual, expected))
            {
                failures.Add(what + ": expected '" + expected + "' and got '" + actual + "'");
            }
        }
    }
}
