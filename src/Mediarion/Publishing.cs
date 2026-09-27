using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// One handler of a notification, ready to be called.
    /// </summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    public delegate Task NotificationHandlerExecutor(object notification, CancellationToken cancellationToken);

    /// <summary>
    /// Decides how the handlers of one notification are run.
    /// </summary>
    /// <remarks>
    /// The default runs them one after another and stops at the first failure, which is the
    /// behaviour that surprises nobody. Running them together is faster and changes what a
    /// failure means, so it is a choice rather than the default.
    /// </remarks>
    public interface INotificationPublisher
    {
        /// <summary>Runs the handlers.</summary>
        /// <param name="handlers">The handlers to run.</param>
        /// <param name="notification">The notification.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when the handlers are done.</returns>
        Task Publish(
            IReadOnlyList<NotificationHandlerExecutor> handlers,
            object notification,
            CancellationToken cancellationToken);
    }
}
