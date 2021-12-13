using LLDP.Service.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace LLDP.Service.Windows
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "VaeLldpService";
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder
                        .AddEventLog()
                        ;
                })
                .ConfigureLldpService();
    }
}