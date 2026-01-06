using LagoVista.AI.Interfaces;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.ACP
{
    internal class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {

            Commands.Startup.ConfigureServices(services, adminLogger);

            services.AddSingleton<IAcpCommandRouter, AcpCommandRouter>();
            services.AddSingleton<IAcpCommandExecutor, AcpCommandExecutor>();
            services.AddSingleton<IAcpCommandRouter, AcpCommandRouter>();
            services.AddSingleton<IAcpCommandFactory, AcpCommandFactory>();
        }
    }
}
