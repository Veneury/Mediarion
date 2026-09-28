using Mediarion;
using Mediarion.Pipeline;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MigrationSample.Migrated
{
    public sealed class PlaceOrder : IRequest<Receipt>
    {
        public Basket Basket { get; set; } = new Basket();
    }

    public sealed class CancelOrder : IRequest
    {
        public string Reference { get; set; } = string.Empty;
    }

    public sealed class OrderPlaced : INotification
    {
        public string Reference { get; set; } = string.Empty;

        public decimal Total { get; set; }
    }

    public sealed class PlaceOrderHandler : IRequestHandler<PlaceOrder, Receipt>
    {
        private readonly Prices prices;
        private readonly Trace trace;
        private readonly IPublisher publisher;

        public PlaceOrderHandler(Prices prices, Trace trace, IPublisher publisher)
        {
            this.prices = prices;
            this.trace = trace;
            this.publisher = publisher;
        }

        public async Task<Receipt> Handle(PlaceOrder request, CancellationToken cancellationToken)
        {
            decimal total = 0m;

            foreach (BasketLine line in request.Basket.Lines)
            {
                total += prices.Of(line);
            }

            var receipt = new Receipt
            {
                Reference = Reference.For(request.Basket),
                Total = total,
                Lines = request.Basket.Lines.Count,
            };

            trace.Add("placed " + receipt.Reference);

            await publisher.Publish(
                new OrderPlaced { Reference = receipt.Reference, Total = receipt.Total },
                cancellationToken);

            return receipt;
        }
    }

    public sealed class CancelOrderHandler : IRequestHandler<CancelOrder>
    {
        private readonly Trace trace;

        public CancelOrderHandler(Trace trace)
        {
            this.trace = trace;
        }

        public Task Handle(CancelOrder request, CancellationToken cancellationToken)
        {
            trace.Add("cancelled " + request.Reference);
            return Task.CompletedTask;
        }
    }

    public sealed class SendConfirmation : INotificationHandler<OrderPlaced>
    {
        private readonly Trace trace;

        public SendConfirmation(Trace trace)
        {
            this.trace = trace;
        }

        public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
        {
            trace.Add("confirmed " + notification.Reference);
            return Task.CompletedTask;
        }
    }

    public sealed class UpdateLedger : INotificationHandler<OrderPlaced>
    {
        private readonly Trace trace;

        public UpdateLedger(Trace trace)
        {
            this.trace = trace;
        }

        public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
        {
            trace.Add("ledgered " + notification.Total.ToString("0.00", CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        }
    }

    public sealed class StampArrival : IRequestPreProcessor<PlaceOrder>
    {
        private readonly Trace trace;

        public StampArrival(Trace trace)
        {
            this.trace = trace;
        }

        public Task Process(PlaceOrder request, CancellationToken cancellationToken)
        {
            trace.Add("stamped " + request.Basket.Customer);
            return Task.CompletedTask;
        }
    }

    public sealed class FileReceipt : IRequestPostProcessor<PlaceOrder, Receipt>
    {
        private readonly Trace trace;

        public FileReceipt(Trace trace)
        {
            this.trace = trace;
        }

        public Task Process(PlaceOrder request, Receipt response, CancellationToken cancellationToken)
        {
            trace.Add("filed " + (response.Reference.Length == 0 ? "nothing" : response.Reference));
            return Task.CompletedTask;
        }
    }

    public sealed class Impossible : IRequest<Receipt>
    {
    }

    public sealed class ImpossibleHandler : IRequestHandler<Impossible, Receipt>
    {
        public Task<Receipt> Handle(Impossible request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("nothing to invoice");
    }

    public sealed class Salvage : IRequestExceptionHandler<Impossible, Receipt, InvalidOperationException>
    {
        private readonly Trace trace;

        public Salvage(Trace trace)
        {
            this.trace = trace;
        }

        public Task Handle(
            Impossible request,
            InvalidOperationException exception,
            RequestExceptionHandlerState<Receipt> state,
            CancellationToken cancellationToken)
        {
            trace.Add("salvaged " + exception.Message);
            state.SetHandled(new Receipt { Reference = "SALVAGED" });

            return Task.CompletedTask;
        }
    }

    public sealed class Doomed : IRequest<Receipt>
    {
    }

    public sealed class DoomedHandler : IRequestHandler<Doomed, Receipt>
    {
        public Task<Receipt> Handle(Doomed request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("no");
    }

    public sealed class Complain : IRequestExceptionAction<Doomed, NotSupportedException>
    {
        private readonly Trace trace;

        public Complain(Trace trace)
        {
            this.trace = trace;
        }

        public Task Execute(Doomed request, NotSupportedException exception, CancellationToken cancellationToken)
        {
            trace.Add("complained about " + exception.Message);
            return Task.CompletedTask;
        }
    }

    public sealed class Timing<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Trace trace;

        public Timing(Trace trace)
        {
            this.trace = trace;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            trace.Add("entered " + typeof(TRequest).Name);
            TResponse response = await next(cancellationToken);
            trace.Add("left " + typeof(TRequest).Name);

            return response;
        }
    }

    public sealed class Guarding : IPipelineBehavior<PlaceOrder, Receipt>
    {
        private readonly Trace trace;

        public Guarding(Trace trace)
        {
            this.trace = trace;
        }

        public Task<Receipt> Handle(
            PlaceOrder request,
            RequestHandlerDelegate<Receipt> next,
            CancellationToken cancellationToken)
        {
            if (request.Basket.Lines.Count == 0)
            {
                trace.Add("refused an empty basket");
                return Task.FromResult(new Receipt());
            }

            return next(cancellationToken);
        }
    }

    /// <summary>
    /// The one thing the two versions do not share: the script that drives the layer, because it
    /// names the sender. Everything above this is the layer itself.
    /// </summary>
    public static class Script
    {
        public static async Task<IReadOnlyList<string>> Run(ISender sender, Trace trace)
        {
            Receipt receipt = await sender.Send(new PlaceOrder
            {
                Basket = new Basket
                {
                    Customer = "ada",
                    Lines =
                    {
                        new BasketLine { Sku = "A", Quantity = 2, UnitPrice = 1.5m },
                        new BasketLine { Sku = "B", Quantity = 1, UnitPrice = 4m },
                    },
                },
            });

            trace.Add("receipt " + receipt.Reference + " " +
                receipt.Total.ToString("0.00", CultureInfo.InvariantCulture) + " " +
                receipt.Lines.ToString(CultureInfo.InvariantCulture));

            await sender.Send(new PlaceOrder { Basket = new Basket { Customer = "bob" } });

            await sender.Send(new CancelOrder { Reference = receipt.Reference });

            object? boxed = await sender.Send((object)new CancelOrder { Reference = "loose" });
            trace.Add("boxed response " + (boxed is null ? "none" : boxed.ToString()));

            Receipt salvaged = await sender.Send(new Impossible());
            trace.Add("impossible gave " + salvaged.Reference);

            try
            {
                await sender.Send(new Doomed());
                trace.Add("doomed did not throw");
            }
            catch (NotSupportedException error)
            {
                trace.Add("doomed threw " + error.Message);
            }

            return trace.Lines;
        }
    }
}
