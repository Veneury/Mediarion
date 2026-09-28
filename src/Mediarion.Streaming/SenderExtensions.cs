using System.Collections.Generic;
using System.Threading;

namespace Mediarion
{
    /// <summary>
    /// Streaming, reached through the sender the application already has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the library this one is a drop-in for, <c>CreateStream</c> is a method on
    /// <c>ISender</c>. It cannot be one here, because <c>ISender</c> lives in a package that has
    /// no dependencies and this one needs <c>IAsyncEnumerable</c>. An extension method makes the
    /// call site identical anyway: <c>sender.CreateStream(request)</c> compiles the same either
    /// way.
    /// </para>
    /// <para>
    /// What it does not survive is a test double. Mocking <c>ISender.CreateStream</c> works over
    /// there and cannot work here, because a mocking library cannot intercept an extension
    /// method. Take <see cref="IStreamSender"/> in the code under test and mock that instead.
    /// </para>
    /// </remarks>
    public static class SenderExtensions
    {
        /// <summary>Sends a streaming request.</summary>
        /// <typeparam name="TResponse">What each value is.</typeparam>
        /// <param name="sender">The sender.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values, as they are produced.</returns>
        public static IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            this ISender sender,
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(sender, nameof(sender));

            return Streaming(sender).CreateStream(request, cancellationToken);
        }

        /// <summary>Sends a streaming request whose type is only known at run time.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Stops the stream.</param>
        /// <returns>The values, boxed, as they are produced.</returns>
        public static IAsyncEnumerable<object?> CreateStream(
            this ISender sender,
            object request,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(sender, nameof(sender));

            return Streaming(sender).CreateStream(request, cancellationToken);
        }

        private static IStreamSender Streaming(ISender sender) =>
            sender as IStreamSender
            ?? throw new MediarionException(
                sender.GetType().Name + " cannot stream. Call AddMediarionStreaming() after " +
                "AddMediarion(), which replaces the mediator with one that can, or take an " +
                "IStreamSender where you need to stream.");
    }
}
