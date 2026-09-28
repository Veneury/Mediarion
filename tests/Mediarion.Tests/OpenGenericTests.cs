using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Mediarion.Tests
{
    public sealed class Wrapped<T> : IRequest<T>
    {
    }

    /// <summary>
    /// An open generic handler, which the library refuses. It lives in its own assembly-free
    /// corner of the tests and is only reached by the test below, which scans this assembly.
    /// </summary>
#pragma warning disable MDR0004 // Deliberate: the test below checks that it is refused.
    public sealed class WrappedHandler<T> : IRequestHandler<Wrapped<T>, T>
    {
        public Task<T> Handle(Wrapped<T> request, CancellationToken cancellationToken) =>
            Task.FromResult(default(T)!);
    }
#pragma warning restore MDR0004

    public sealed class OpenGenericTests
    {
        /// <remarks>
        /// <para>
        /// An open generic handler cannot be registered: the container closes an open
        /// implementation against an open service type by matching type parameters position for
        /// position, and a handler's do not line up — the request argument of
        /// IRequestHandler&lt;Wrapped&lt;T&gt;, T&gt; is Wrapped&lt;T&gt; and not T.
        /// </para>
        /// <para>
        /// The scan skips it and the send says so. Refusing it at registration was tried and
        /// reverted: the other library registers it without complaint and fails on the first
        /// send with a container message about a missing service, so refusing would stop an
        /// application starting that starts today, over a request that may never be sent.
        /// MDR0004 catches it properly, at the declaration, while the project compiles.
        /// </para>
        /// </remarks>
        [Fact]
        public async Task An_open_generic_handler_is_not_found_and_the_send_says_so()
        {
            var services = new ServiceCollection();
            services.AddMediarion(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<OpenGenericTests>());

            using ServiceProvider provider = services.BuildServiceProvider();

            MediarionException error = await Should.ThrowAsync<MediarionException>(
                () => provider.GetRequiredService<ISender>().Send(new Wrapped<string>()));

            error.Message.ShouldContain("Wrapped");
        }
    }
}
