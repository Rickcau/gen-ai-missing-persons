using api_process_missing_persons_files.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Net;

namespace api_process_missing_persons_files.Functions
{
    public class EnrichLocationData
    {
        private readonly ILogger _logger;
        private readonly AddressEnrichmentHelper _enrichmentHelper;

        public EnrichLocationData(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = loggerFactory?.CreateLogger<EnrichLocationData>()
                ?? throw new ArgumentNullException(nameof(loggerFactory));

            var httpClient = httpClientFactory?.CreateClient()
                ?? throw new ArgumentNullException(nameof(httpClientFactory));

            var connectionString = configuration["DatabaseConnection"]
                ?? throw new InvalidOperationException("DatabaseConnection is not configured.");

            var mapsApiKey = configuration["AzureMapsApiKey"]
                ?? throw new InvalidOperationException("AzureMapsApiKey is not configured.");

            _enrichmentHelper = new AddressEnrichmentHelper(_logger, httpClient, connectionString, mapsApiKey);
        }

        [Function("EnrichData")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP EnrichData trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody) || requestBody == "{}")
            {
                return await _enrichmentHelper.HandleEnrichmentRequest(req, requestBody)
                    ?? req.CreateResponse(HttpStatusCode.OK);
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(requestBody);
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.Number)
                {
                    var result = await _enrichmentHelper.HandleEnrichmentRequest(req, requestBody);
                    return result ?? req.CreateResponse(HttpStatusCode.InternalServerError);
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("Invalid request body. Expected {\"id\": number} or empty object.");
                    return response;
                }
            }
            catch (JsonException)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Invalid JSON in request body.");
                return response;
            }
        }
    }
}