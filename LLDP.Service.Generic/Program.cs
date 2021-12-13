using LLDP.Service.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LLDP.Service.Generic
{
    public class Program
    {
        public static async Task Main(string[] args) =>
            await CreateHostBuilder(args).RunConsoleAsync();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging((context, builder) =>
                {
                    builder
                        .ClearProviders()
                        .AddConsole();
                })
                .ConfigureLldpService()
                .UseSystemd()
            ;
    }
}