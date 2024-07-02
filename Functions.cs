using System.Diagnostics;
using System.Numerics;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace WebJobsServiceBusScalingDemo
{
    public class Functions
    {
        private readonly ILogger<Functions> _logger;
        private bool _cancellationRequested = false;
        private bool _cancellationRequestLogged = false;

        public Functions(ILogger<Functions> logger)
        {
            _logger = logger;
        }


        //e.g. if the SB message is "c,00:15:00,50" it will run for 15 minutes with ~50% CPU load
        [FunctionName("ProcessJob")]
        public async Task ProcessMessage([ServiceBusTrigger("jobs")] ServiceBusReceivedMessage sbMessage, CancellationToken cancellationToken)
        {
            var message = sbMessage.Body.ToString();
            _logger.LogInformation($"Message received: {message}");
            var parts = message.ToString().Split(",");
            if (parts.Length >= 2)
            {
                var delay = TimeSpan.Parse(parts[1]);

                cancellationToken.Register(() =>
                {
                    _logger.LogWarning("Cancellation requested for {sbSequenceNumber}", sbMessage.SequenceNumber);
                    _cancellationRequested = true;
                });
                
                if (parts[0] == "c")
                {
                    long counter = 0;
                    var cpuPercentage = int.Parse(parts[2]);
                    _logger.LogInformation("Export start: {delay} {sbMessageText} ({sbSequenceNumber})", delay.ToString(),
                        message, sbMessage.SequenceNumber);

                    var sw = Stopwatch.StartNew();
                    var idleTimeMultiplier = (100.0 - cpuPercentage) / cpuPercentage;
                    while (sw.Elapsed < delay)
                    {
                        var durationSw = Stopwatch.StartNew();
                        Factorial(50);
                        durationSw.Stop();
                        await Task.Delay(durationSw.Elapsed * idleTimeMultiplier);
                        counter++;
                        if ((_cancellationRequested || cancellationToken.IsCancellationRequested) && !_cancellationRequestLogged)
                        {
                            _logger.LogWarning($"Cancellation requested for {{sbSequenceNumber}}. Callback called: {_cancellationRequested}", sbMessage.SequenceNumber);
                            _cancellationRequestLogged = true;
                        }
                        if (counter % 100000 == 0)
                        {
                            _logger.LogInformation($"Proc progress {{sbMessageText}} ({{sbSequenceNumber}}) ({GetShutdownFileInfo()}): {{counter}}", message, sbMessage.SequenceNumber, counter);
                        }
                    }
                    sw.Stop();
                    _logger.LogInformation("Export end: {delay} {sbMessageText} ({sbSequenceNumber})", delay.ToString(),
                        message, sbMessage.SequenceNumber);
                }
               
            }
        }

        private BigInteger Factorial(int number)
        {
            if (number == 1)
            {
                return 1;
            }
            else
            {
                return number * Factorial(number - 1);
            }
        }

        private string GetShutdownFileInfo()
        {
            var fileName = Environment.GetEnvironmentVariable("WEBJOBS_SHUTDOWN_FILE");
            var fileExists = fileName !=null && File.Exists(fileName);
            return $"File:{fileName};File exists:{fileExists}";
        }
    }
}
