using Microsoft.Extensions.DependencyInjection;

namespace NordAPI.Swish.Webhooks
{
    /// <summary>
    /// Builder used to continue configuring Swish webhook services.
    /// </summary>
    public interface ISwishWebhookBuilder
    {
        /// <summary>
        /// The underlying service collection.
        /// </summary>
        IServiceCollection Services { get; }
    }

    internal sealed class SwishWebhookBuilder : ISwishWebhookBuilder
    {
        public SwishWebhookBuilder(IServiceCollection services) => Services = services;

        public IServiceCollection Services { get; }
    }
}
