using Microsoft.Extensions.Logging.EventLog;

namespace LLDP.Service.Core
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public static class LldpServiceExtensions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HostBuilder"/> class with pre-configured defaults.
        /// </summary>
        /// <param name="args">The command line args.</param>
        /// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureLldpService(this IHostBuilder hostBuilder, string[] args = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<LldpService>();
                services.AddSingleton<LldpClient>();
                services.Configure<LldpOptions>(hostContext.Configuration.GetSection(nameof(LldpOptions)));
            });

            return hostBuilder;
        }
    }
}