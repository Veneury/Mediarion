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
    /// An open generic handler. No container can register one directly, so it is skipped unless
    /// <c>RegisterGenericHandlers</c> asks for it to be closed at registration instead.
    /// </summary>
#pragma warning disable MDR0004 // Deliberate: the tests below check what the scan does with it.
    public sealed class WrappedHandler<T> : IRequestHandler<Wrapped<T>, T>
    {
        public Task<T> Handle(Wrapped<T> request, CancellationToken cancellationToken) =>
            Task.FromResult(default(T)!);
    }
#pragma warning restore MDR0004

    /// <summary>A type of this assembly's own, so it is a candidate to close over.</summary>
    public sealed class Parcel
    {
    }

    public sealed class OpenGenericTests
    {
        private static ServiceProvider Build(bool generic)
        {
            var services = new ServiceCollection();
            services.AddMediarion(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<OpenGenericTests>();
                configuration.RegisterGenericHandlers = generic;
            });

            return services.BuildServiceProvider();
        }

        /// <remarks>
        /// <para>
        /// A container closes an open implementation against an open service type by matching
        /// type parameters position for position, and a handler's do not line up — the request
        /// argument of IRequestHandler&lt;Wrapped&lt;T&gt;, T&gt; is Wrapped&lt;T&gt; and not T.
        /// So one is skipped by the scan, and the send says which request has no handler.
        /// </para>
        /// <para>
        /// Refusing it at registration was tried and reverted: the other library registers it
        /// without complaint and fails on the first send, so refusing would stop an application
        /// starting that starts today, over a request that may never be sent.
        /// </para>
        /// </remarks>
        [Fact]
        public async Task An_open_generic_handler_is_not_found_and_the_send_says_so()
        {
            using ServiceProvider provider = Build(generic: false);

            MediarionException error = await Should.ThrowAsync<MediarionException>(
                () => provider.GetRequiredService<ISender>().Send(new Wrapped<Parcel>()));

            error.Message.ShouldContain("Wrapped");
        }

        /// <remarks>
        /// With the flag, the closing is done at registration rather than left to the container.
        /// Both libraries were run side by side to settle what the flag does: this one answers
        /// there too.
        /// </remarks>
        [Fact]
        public async Task An_open_generic_handler_is_closed_when_it_is_asked_for()
        {
            using ServiceProvider provider = Build(generic: true);

            Parcel answer = await provider.GetRequiredService<ISender>().Send(new Wrapped<Parcel>());

            answer.ShouldBeNull();
        }

        /// <remarks>
        /// The limit of the idea, and the other library has it too: candidates are the concrete
        /// types of the assemblies being scanned, so a handler is closed over Parcel and never
        /// over int. Anything else would mean closing over every type the runtime can name.
        /// </remarks>
        [Fact]
        public async Task A_handler_is_not_closed_over_a_type_from_outside_the_scan()
        {
            using ServiceProvider provider = Build(generic: true);

            MediarionException error = await Should.ThrowAsync<MediarionException>(
                () => provider.GetRequiredService<ISender>().Send(new Wrapped<int>()));

            error.Message.ShouldContain("Wrapped");
        }
    }
}
