using System;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace Convey.MessageBrokers.RabbitMQ.Plugins
{
    internal sealed class RabbitMqPluginsExecutor : IRabbitMqPluginsExecutor
    {
        private readonly IRabbitMqPluginsRegistryAccessor _registry;
        private readonly IServiceProvider _serviceProvider;

        public RabbitMqPluginsExecutor(IRabbitMqPluginsRegistryAccessor registry, IServiceProvider serviceProvider)
        {
            _registry = registry;
            _serviceProvider = serviceProvider;
        }

        public async Task ExecuteAsync(Func<object, object, BasicDeliverEventArgs, Task> successor, 
            object message, object correlationContext, BasicDeliverEventArgs args)
        {
            var chains = _registry.Get();

            if (!chains.Any())
            {
                await successor(message, correlationContext, args);
            }

            foreach (var chain in chains)
            {
                chain.Plugin = _serviceProvider.GetService(chain.PluginType) as IRabbitMqPlugin;
            }

            var current = chains.Last;

            while (current != null)
            {
                ((IRabbitMqPluginAccessor) current.Value.Plugin).SetSuccessor(current.Next is null
                    ? successor
                    : current.Next.Value.Plugin.HandleAsync);

                current = current.Previous;
            }

            await chains.First.Value.Plugin.HandleAsync(message, correlationContext, args);
        }
    }
}