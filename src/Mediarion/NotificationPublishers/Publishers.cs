using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion.NotificationPublishers
{
    /// <summary>
    /// Runs the handlers one after another, awaiting each.
    /// </summary>
    /// <remarks>
    /// The default. The first handler to throw stops the rest, which is usually what a caller
    /// expects and always what makes a failure easy to read.
    /// </remarks>
    public sealed class ForeachAwaitPublisher : INotificationPublisher
    {
        /// <inheritdoc/>
        public async Task Publish(
            IReadOnlyList<NotificationHandlerExecutor> handlers,
            object notification,
            CancellationToken cancellationToken)
        {
            Guard.NotNull(handlers, nameof(handlers));

            for (int i = 0; i < handlers.Count; i++)
            {
                await handlers[i](notification, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Starts every handler and waits for all of them.
    /// </summary>
    /// <remarks>
    /// Every handler runs even if one throws, and what comes out is the first exception of
    /// however many there were. Worth it when the handlers are independent and slow; not worth
    /// it when one of them failing means the others should not have run.
    /// </remarks>
    public sealed class TaskWhenAllPublisher : INotificationPublisher
    {
        /// <inheritdoc/>
        public Task Publish(
            IReadOnlyList<NotificationHandlerExecutor> handlers,
            object notification,
            CancellationToken cancellationToken)
        {
            Guard.NotNull(handlers, nameof(handlers));

            var running = new Task[handlers.Count];

            for (int i = 0; i < handlers.Count; i++)
            {
                running[i] = handlers[i](notification, cancellationToken);
            }

            return Task.WhenAll(running);
        }
    }
}
