using LagoVista.AI.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.Core.Interfaces;

namespace LagoVista.AI.ACP.Commands
{
    internal class Startup
    {

        private static IAcpCommandRegistry _acpCommandRegistry;

        public static void RegisterCommand<T>() where T : IAcpCommand
        {
            _acpCommandRegistry.RegisterCommand<T>();
        }

        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {
            var commandRegistry = new AcpCommandRegistry(adminLogger);
            _acpCommandRegistry = commandRegistry;
            _acpCommandRegistry.RegisterCommand<ChangeModeCommand>();

            services.AddSingleton<IAcpCommandRegistry>(commandRegistry);
        }
    }
}
