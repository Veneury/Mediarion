using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mediarion;
using Mediarion.Pipeline;

namespace Helpdesk
{
    public enum Priority
    {
        Low,
        Normal,
        Urgent,
    }

    public sealed class Ticket
    {
        public int Id { get; set; }

        public string Subject { get; set; } = string.Empty;

        public string Reporter { get; set; } = string.Empty;

        public Priority Priority { get; set; }

        public string? AssignedTo { get; set; }

        public bool Closed { get; set; }
    }

    /// <summary>The store, which is a list, because this is a sample and not a product.</summary>
    public sealed class Tickets
    {
        private readonly List<Ticket> tickets = new List<Ticket>();
        private int next = 1;

        public Ticket Add(string subject, string reporter, Priority priority)
        {
            var ticket = new Ticket
            {
                Id = next++,
                Subject = subject,
                Reporter = reporter,
                Priority = priority,
            };

            tickets.Add(ticket);
            return ticket;
        }

        public Ticket? Find(int id) => tickets.FirstOrDefault(t => t.Id == id);

        public IReadOnlyList<Ticket> Open() => tickets.Where(t => !t.Closed).ToList();
    }

    /// <summary>Everything the application did, in order, so the run can check itself.</summary>
    public sealed class Journal
    {
        public List<string> Lines { get; } = new List<string>();

        public void Write(string line) => Lines.Add(line);
    }

    // ---- requests -----------------------------------------------------------------------

    public sealed class OpenTicket : IRequest<int>
    {
        public string Subject { get; set; } = string.Empty;

        public string Reporter { get; set; } = string.Empty;

        public Priority Priority { get; set; }
    }

    public sealed class AssignTicket : IRequest
    {
        public int Ticket { get; set; }

        public string To { get; set; } = string.Empty;
    }

    public sealed class CloseTicket : IRequest<bool>
    {
        public int Ticket { get; set; }
    }

    public sealed class CountOpenTickets : IRequest<int>
    {
    }

    // ---- notifications ------------------------------------------------------------------

    public sealed class TicketOpened : INotification
    {
        public int Ticket { get; set; }

        public Priority Priority { get; set; }
    }

    // ---- handlers -----------------------------------------------------------------------

    public sealed class OpenTicketHandler : IRequestHandler<OpenTicket, int>
    {
        private readonly Tickets tickets;
        private readonly IPublisher publisher;

        public OpenTicketHandler(Tickets tickets, IPublisher publisher)
        {
            this.tickets = tickets;
            this.publisher = publisher;
        }

        public async Task<int> Handle(OpenTicket request, CancellationToken cancellationToken)
        {
            Ticket ticket = tickets.Add(request.Subject, request.Reporter, request.Priority);

            await publisher.Publish(
                new TicketOpened { Ticket = ticket.Id, Priority = ticket.Priority },
                cancellationToken);

            return ticket.Id;
        }
    }

    public sealed class AssignTicketHandler : IRequestHandler<AssignTicket>
    {
        private readonly Tickets tickets;
        private readonly Journal journal;

        public AssignTicketHandler(Tickets tickets, Journal journal)
        {
            this.tickets = tickets;
            this.journal = journal;
        }

        public Task Handle(AssignTicket request, CancellationToken cancellationToken)
        {
            Ticket ticket = tickets.Find(request.Ticket)
                ?? throw new InvalidOperationException("Ticket " + request.Ticket + " does not exist.");

            ticket.AssignedTo = request.To;
            journal.Write("assigned " + ticket.Id + " to " + request.To);

            return Task.CompletedTask;
        }
    }

    public sealed class CloseTicketHandler : IRequestHandler<CloseTicket, bool>
    {
        private readonly Tickets tickets;
        private readonly Journal journal;

        public CloseTicketHandler(Tickets tickets, Journal journal)
        {
            this.tickets = tickets;
            this.journal = journal;
        }

        public Task<bool> Handle(CloseTicket request, CancellationToken cancellationToken)
        {
            Ticket? ticket = tickets.Find(request.Ticket);

            if (ticket is null || ticket.AssignedTo is null)
            {
                journal.Write("refused to close " + request.Ticket);
                return Task.FromResult(false);
            }

            ticket.Closed = true;
            journal.Write("closed " + ticket.Id);

            return Task.FromResult(true);
        }
    }

    public sealed class CountOpenTicketsHandler : IRequestHandler<CountOpenTickets, int>
    {
        private readonly Tickets tickets;

        public CountOpenTicketsHandler(Tickets tickets)
        {
            this.tickets = tickets;
        }

        public Task<int> Handle(CountOpenTickets request, CancellationToken cancellationToken) =>
            Task.FromResult(tickets.Open().Count);
    }

    // ---- notification handlers ----------------------------------------------------------

    public sealed class NotifyReporter : INotificationHandler<TicketOpened>
    {
        private readonly Journal journal;

        public NotifyReporter(Journal journal)
        {
            this.journal = journal;
        }

        public Task Handle(TicketOpened notification, CancellationToken cancellationToken)
        {
            journal.Write("emailed about " + notification.Ticket);
            return Task.CompletedTask;
        }
    }

    public sealed class PageOnCall : INotificationHandler<TicketOpened>
    {
        private readonly Journal journal;

        public PageOnCall(Journal journal)
        {
            this.journal = journal;
        }

        public Task Handle(TicketOpened notification, CancellationToken cancellationToken)
        {
            if (notification.Priority == Priority.Urgent)
            {
                journal.Write("paged for " + notification.Ticket);
            }

            return Task.CompletedTask;
        }
    }

    // ---- the pipeline -------------------------------------------------------------------

    /// <summary>
    /// Refuses a request before it reaches its handler, which is what a behaviour that does not
    /// call the next step is for.
    /// </summary>
    public sealed class Validation : IPipelineBehavior<OpenTicket, int>
    {
        private readonly Journal journal;

        public Validation(Journal journal)
        {
            this.journal = journal;
        }

        public Task<int> Handle(
            OpenTicket request,
            RequestHandlerDelegate<int> next,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                journal.Write("rejected a ticket with no subject");
                return Task.FromResult(0);
            }

            return next(cancellationToken);
        }
    }

    /// <summary>Wraps every request, which is what an open generic behaviour is for.</summary>
    public sealed class Logging<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Journal journal;

        public Logging(Journal journal)
        {
            this.journal = journal;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            journal.Write("-> " + typeof(TRequest).Name);
            TResponse response = await next(cancellationToken);
            journal.Write("<- " + typeof(TRequest).Name);

            return response;
        }
    }

    /// <summary>Runs before every handler, without having to remember to call the next step.</summary>
    public sealed class StampArrival : IRequestPreProcessor<OpenTicket>
    {
        private readonly Journal journal;

        public StampArrival(Journal journal)
        {
            this.journal = journal;
        }

        public Task Process(OpenTicket request, CancellationToken cancellationToken)
        {
            journal.Write("received from " + request.Reporter);
            return Task.CompletedTask;
        }
    }
}
