using LagoVista.AI.Interfaces;
using LagoVista.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace LagoVista.DependencyInjection
{
    public sealed class AgentToolDescriptor
    {
        public Type ToolType { get; }

        public AgentToolDescriptor(Type toolType)
        {
            ToolType = toolType ?? throw new ArgumentNullException(nameof(toolType));
        }
    }

    public static class AgentToolServiceCollectionExtensions
    {
        public static IServiceCollection AddAgentTools(this IServiceCollection services)
        {
            services.TryAddSingleton<IAgentToolRegistry, AgentToolRegistry>();

            return services;
        }

        public static IServiceCollection AddAgentTool<T>(this IServiceCollection services)
            where T : class, IAgentTool
        {
            services.AddAgentTools();

            services.AddTransient<T>();

            services.AddSingleton(new AgentToolDescriptor(typeof(T)));

            return services;
        }
    }
}