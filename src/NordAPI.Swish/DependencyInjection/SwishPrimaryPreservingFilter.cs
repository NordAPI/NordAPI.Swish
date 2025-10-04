using System.Net.Http;
using Microsoft.Extensions.Http;

namespace NordAPI.Swish.DependencyInjection
{
    /// <summary>
    /// Ensures a Primary handler exists for our clients *only if* the host/tests didn't set one.
    /// This preserves test/host-provided Primary while allowing us to supply our mTLS/default Primary otherwise.
    /// </summary>
    internal sealed class SwishPrimaryPreservingFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly Func<HttpMessageHandler> _primaryFactory;

        public SwishPrimaryPreservingFilter(Func<HttpMessageHandler> primaryFactory)
        {
            _primaryFactory = primaryFactory ?? throw new ArgumentNullException(nameof(primaryFactory));
        }

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                // Run any previously-registered filters first
                next(builder);

                // If host/tests already set Primary -> respect it
                if (builder.PrimaryHandler is null)
                {
                    builder.PrimaryHandler = _primaryFactory();
                }
            };
        }
    }
}
