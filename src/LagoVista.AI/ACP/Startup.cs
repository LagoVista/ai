using LagoVista.AI.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using Microsoft.Extensions.DependencyInjection;

namespace LagoVista.AI.ACP
{
    internal class Startup
    {
        public static void ConfigureServices(IServiceCollection services, IAdminLogger adminLogger)
        {

            Commands.Startup.ConfigureServices(services, adminLogger);

            services.AddTransient<IAcpCommandRouter, AcpCommandRouter>();
            services.AddTransient<IAcpCommandExecutor, AcpCommandExecutor>();
            services.AddTransient<IAcpCommandRouter, AcpCommandRouter>();
            services.AddTransient<IAcpCommandFactory, AcpCommandFactory>();
        }
    }
}
