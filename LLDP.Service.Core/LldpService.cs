using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LLDP.Service
{
    public class LldpService : BackgroundService
    {
        private readonly IOptionsMonitor<LldpOptions> lldpOptions;
        private readonly LldpClient lldpClient;
        private readonly ILogger<LldpService> logger;
        private readonly IHostApplicationLifetime hostApplication;

        public LldpService(
            IOptionsMonitor<LldpOptions> lldpOptions,
            LldpClient lldpClient,
            IHostApplicationLifetime hostApplication,
            ILogger<LldpService> logger)
        {
            this.lldpOptions = lldpOptions;
            this.lldpClient = lldpClient;
            this.hostApplication = hostApplication;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                this.logger.LogInformation("Execution directory: {0}", AppContext.BaseDirectory);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Run(() =>
                    {
                        lldpClient.Send(this.lldpOptions.CurrentValue);
                        Thread.Sleep(30 * 1000); // run every 30 seconds.
                    }, stoppingToken);
                }
            }
            catch (Exception e)
            {
                Environment.ExitCode = -1;

                this.logger.LogError(e, "Failed during execution.");
                this.hostApplication.StopApplication();
            }
        }
    }
}