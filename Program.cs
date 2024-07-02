using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace WebJobsServiceBusScalingDemo
{
    internal class Program
    {
        static async Task Main()
        {
            var builder = Host.CreateDefaultBuilder();
            
            builder.ConfigureLogging((context, b) =>
            {
                b.AddConsole();
                b.AddApplicationInsights(c =>
                {
                    c.ConnectionString = context.Configuration.GetConnectionString("AppInsightsConnectionString");
                }, o => { });
                b.AddFilter<ApplicationInsightsLoggerProvider>(null, LogLevel.Error);
                b.AddFilter<ApplicationInsightsLoggerProvider>("WebJobsServiceBusScalingDemo", LogLevel.Debug);
            });
            builder.ConfigureWebJobs((context, b) =>
            {
                b.AddAzureStorageCoreServices();
                b.AddServiceBus(c =>
                {
                    c.MaxConcurrentCalls = 1;
                    c.MaxAutoLockRenewalDuration = TimeSpan.FromHours(2);
                });
            });

            var host = builder.Build();
            using (host)
            {
                await host.RunAsync();
            }
        }
    }
}
