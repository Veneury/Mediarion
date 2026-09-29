using System.Threading;
using System.Threading.Tasks;

namespace Mediarion
{
    /// <summary>
    /// A notification handler with nothing to await.
    /// </summary>
    /// <typeparam name="TNotification">The notification.</typeparam>
    /// <remarks>
    /// <para>
    /// Plenty of handlers do something synchronous — add a line to a list, bump a counter, raise
    /// an event — and writing <c>return Task.CompletedTask</c> at the end of each is noise.
    /// Derive from this and override the void <c>Handle</c> instead.
    /// </para>
    /// <para>
    /// Nothing else changes: it implements <see cref="INotificationHandler{TNotification}"/>, so
    /// the scan finds it and the publisher runs it like any other. The interface method is
    /// explicit so that a caller holding one of these cannot reach the asynchronous one by
    /// accident.
    /// </para>
    /// </remarks>
    public abstract class NotificationHandler<TNotification> : INotificationHandler<TNotification>
        where TNotification : INotification
    {
        /// <inheritdoc/>
        Task INotificationHandler<TNotification>.Handle(
            TNotification notification,
            CancellationToken cancellationToken)
        {
            Handle(notification);

            return Task.CompletedTask;
        }

        /// <summary>Handles the notification.</summary>
        /// <param name="notification">The notification.</param>
        protected abstract void Handle(TNotification notification);
    }
}
