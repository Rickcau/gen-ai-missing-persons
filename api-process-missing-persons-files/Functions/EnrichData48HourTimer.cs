using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using api_process_missing_persons_files.Helpers;
using System.Net.Http;

namespace api_process_missing_persons_files.Functions
{
    public class EnrichData48HourTimer
    {
        private readonly ILogger _logger;
        private readonly AddressEnrichmentHelper _enrichmentHelper;

        public EnrichData48HourTimer(
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<EnrichData48HourTimer>();
            var httpClient = httpClientFactory.CreateClient();
            var connectionString = configuration["DatabaseConnection"] ?? throw new InvalidOperationException("DatabaseConnection is not configured.");
            var mapsApiKey = configuration["AzureMapsApiKey"] ?? throw new InvalidOperationException("AzureMapsApiKey is not configured.");
            _enrichmentHelper = new AddressEnrichmentHelper(_logger, httpClient, connectionString, mapsApiKey);
        }

        [Function("EnrichData48HourTimer")]
        public async Task RunAsync([TimerTrigger("0 0 */48 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            try
            {
                await _enrichmentHelper.EnrichAddresses();
                _logger.LogInformation("Address enrichment process completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the scheduled address enrichment process.");
            }

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}