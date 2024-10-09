using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Net;
using api_process_mp_pdfs.Models;

namespace api_process_mp_pdfs.Function
{
    public class EnrichAddressesScheduled
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly string? _connectionString;
        private readonly string? _mapsApiKey;

        public EnrichAddressesScheduled(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
        {
            _logger = loggerFactory.CreateLogger<EnrichAddressesScheduled>();
            _httpClient = httpClientFactory.CreateClient();
            _connectionString = Environment.GetEnvironmentVariable("DatabaseConnection");
            _mapsApiKey = Environment.GetEnvironmentVariable("AzureMapsApiKey");
        }

        [Function("EnrichAddressesScheduled")]
        public async Task RunScheduled([TimerTrigger("0 0 */48 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Scheduled address enrichment function executed at: {DateTime.Now}");
            await EnrichAddresses();
        }


        [Function("EnrichAddressesManual")]
        [OpenApiOperation(operationId: "EnrichAddresses", tags: new[] { "Address Enrichment" },
            Summary = "Enrich addresses manually",
            Description = "This function allows you to enrich either a specific address by providing an ID, or all non-enriched addresses if no ID is provided.")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(EnrichmentRequest), Required = true, Description = "The request body can either contain an 'id' for a specific address or be empty to process all addresses.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response with a description of the enrichment process result.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "Bad request if the input is invalid.")]
        public async Task<HttpResponseData> RunManual([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            // If you want to enrich a specific address instead of all addresses, you can pass the address in the request body. For example:
            // send the following JSON in the request body to enrich a specific address:
            // {
            // "id": 123
            // }
            // If you want the enrich all records in the database, send an empty request body.
            // {}

            _logger.LogInformation("Manual address enrichment function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody) || requestBody == "{}")
            {
                return await ProcessAllAddresses(req);
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(requestBody);
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.Number)
                {
                    // Pass the entire requestBody to ProcessSpecificAddress
                    return await ProcessSpecificAddress(req, requestBody);
                }
                else
                {
                    return await ProcessAllAddresses(req);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Invalid JSON in request body: {ex.Message}");
                HttpResponseData response = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Invalid JSON in request body");
                return response;
            }
            

        }

        private async Task<HttpResponseData> ProcessAllAddresses(HttpRequestData req)
        {
            await EnrichAddresses();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Address enrichment process completed for all non-enriched addresses.");
            return response;
        }

         private async Task<HttpResponseData> ProcessSpecificAddress(HttpRequestData req, string requestBody)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(requestBody);
                if (data.TryGetProperty("id", out JsonElement idElement) && idElement.TryGetInt32(out int id))
                {
                    await EnrichSpecificAddress(id);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteStringAsync($"Address enrichment process completed for ID: {id}");
                    return response;
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("Invalid request body. Please provide a valid 'id'.");
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

        private async Task EnrichAddresses()
        {
            _logger.LogInformation("Starting address enrichment process for all non-enriched addresses.");

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = @"
                        SELECT ID, MissingFrom 
                        FROM MissingPersons 
                        WHERE IsEnriched = 0 OR IsEnriched IS NULL";

                    using (SqlCommand command = new SqlCommand(selectQuery, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            List<(int Id, string Address)> addressesToEnrich = new List<(int, string)>();
                            while (await reader.ReadAsync())
                            {
                                addressesToEnrich.Add((reader.GetInt32(0), reader.GetString(1)));
                            }
                            reader.Close();

                            _logger.LogInformation($"Found {addressesToEnrich.Count} addresses to enrich.");

                            foreach (var (id, address) in addressesToEnrich)
                            {
                                await ProcessSingleAddress(connection, id, address);
                            }
                        }
                    }
                }

                _logger.LogInformation("Address enrichment process completed for all addresses.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the address enrichment process.");
                throw; // Re-throw the exception if you want it to propagate up the call stack
            }
        }

        private async Task EnrichSpecificAddress(int id)
        {
            _logger.LogInformation($"Starting address enrichment process for ID: {id}");
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string selectQuery = @"
                        SELECT MissingFrom 
                        FROM MissingPersons 
                        WHERE ID = @Id AND (IsEnriched = 0 OR IsEnriched IS NULL)";
                    using (SqlCommand command = new SqlCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        var result = await command.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            // Use null-coalescing operator with empty string as default
                            string address = result?.ToString() ?? string.Empty;

                            if (!string.IsNullOrWhiteSpace(address))
                            {
                                await ProcessSingleAddress(connection, id, address);
                                _logger.LogInformation($"Address enrichment completed for ID: {id}");
                            }
                            else
                            {
                                _logger.LogWarning($"Empty address found for ID {id}");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"No non-enriched address found for ID {id}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred during the address enrichment process for ID: {id}");
                throw; // Re-throw the exception if you want it to propagate up the call stack
            }
        }

        private async Task ProcessSingleAddress(SqlConnection connection, int id, string address)
        {
            try
            {
                EnrichedAddress? enrichedAddress = await EnrichAddressWithAzureMaps(address);
                if (enrichedAddress != null)
                {
                    await UpdateAddressInDatabase(connection, id, enrichedAddress);
                    _logger.LogInformation($"Successfully enriched and updated address for ID {id}");
                }
                else
                {
                    await MarkAddressEnrichmentFailed(connection, id);
                    _logger.LogWarning($"Failed to enrich address for ID {id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing address for ID {id}: {ex.Message}");
                await MarkAddressEnrichmentFailed(connection, id);
            }
        }

        private async Task MarkAddressEnrichmentFailed(SqlConnection connection, int id)
        {
            string updateQuery = @"
                UPDATE MissingPersons 
                SET IsEnriched = 1, 
                    MatchConfidence = 0
                WHERE ID = @Id";

            using (SqlCommand command = new SqlCommand(updateQuery, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                try
                {
                    await command.ExecuteNonQueryAsync();
                    _logger.LogWarning($"Marked address enrichment as failed for MissingPerson with ID {id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error marking address enrichment as failed in database for ID {id}: {ex.Message}");
                    // Depending on your error handling strategy, you might want to re-throw the exception here
                    // throw;
                }
            }
        }

        private async Task<EnrichedAddress?> EnrichAddressWithAzureMaps(string address)
        {
            string encodedAddress = Uri.EscapeDataString(address);
            // URL could be read from a configuration file or environment variable
            string url = $"https://atlas.microsoft.com/search/address/json?subscription-key={_mapsApiKey}&api-version=1.0&language=en-US&query={encodedAddress}, Detroit";

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Azure Maps API request failed: {response.StatusCode}");
                return null;
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            using (JsonDocument document = JsonDocument.Parse(responseBody))
            {
                JsonElement root = document.RootElement;
                JsonElement results = root.GetProperty("results");
                
                if (results.GetArrayLength() > 0)
                {
                    JsonElement firstResult = results[0];
                    return new EnrichedAddress
                    {
                        MatchConfidence = firstResult.GetProperty("score").GetDouble(),
                        StreetNumber = GetJsonPropertyString(firstResult, "address", "streetNumber"),
                        StreetName = GetJsonPropertyString(firstResult, "address", "streetName"),
                        Municipality = GetJsonPropertyString(firstResult, "address", "municipality"),
                        Neighbourhood = GetJsonPropertyString(firstResult, "address", "neighbourhood"),
                        CountrySecondarySubdivision = GetJsonPropertyString(firstResult, "address", "countrySecondarySubdivision"),
                        CountrySubdivisionName = GetJsonPropertyString(firstResult, "address", "countrySubdivisionName"),
                        PostalCode = GetJsonPropertyString(firstResult, "address", "postalCode"),
                        ExtendedPostalCode = GetJsonPropertyString(firstResult, "address", "extendedPostalCode"),
                        Latitude = firstResult.GetProperty("position").GetProperty("lat").GetDouble(),
                        Longitude = firstResult.GetProperty("position").GetProperty("lon").GetDouble()
                    };
                }
            }

            return null;
        }

        private string? GetJsonPropertyString(JsonElement element, params string[] propertyNames)
        {
            foreach (var prop in propertyNames)
            {
                if (!element.TryGetProperty(prop, out var childElement))
                    return null;
                element = childElement;
            }
            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        }
        private async Task UpdateAddressInDatabase(SqlConnection connection, int id, EnrichedAddress enrichedAddress)
        {
            string updateQuery = @"
                UPDATE MissingPersons 
                SET StreetNumber = @StreetNumber, 
                    StreetName = @StreetName, 
                    Municipality = @Municipality, 
                    Neighbourhood = @Neighbourhood, 
                    CountrySecondarySubdivision = @CountrySecondarySubdivision, 
                    CountrySubdivisionName = @CountrySubdivisionName, 
                    PostalCode = @PostalCode, 
                    ExtendedPostalCode = @ExtendedPostalCode, 
                    Latitude = @Latitude, 
                    Longitude = @Longitude, 
                    MatchConfidence = @MatchConfidence, 
                    IsEnriched = 1
                WHERE ID = @Id";

            using (SqlCommand command = new SqlCommand(updateQuery, connection))
            {
                command.Parameters.AddWithValue("@StreetNumber", enrichedAddress.StreetNumber ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@StreetName", enrichedAddress.StreetName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Municipality", enrichedAddress.Municipality ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Neighbourhood", enrichedAddress.Neighbourhood ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CountrySecondarySubdivision", enrichedAddress.CountrySecondarySubdivision ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CountrySubdivisionName", enrichedAddress.CountrySubdivisionName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@PostalCode", enrichedAddress.PostalCode ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ExtendedPostalCode", enrichedAddress.ExtendedPostalCode ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Latitude", enrichedAddress.Latitude);
                command.Parameters.AddWithValue("@Longitude", enrichedAddress.Longitude);
                command.Parameters.AddWithValue("@MatchConfidence", enrichedAddress.MatchConfidence);
                command.Parameters.AddWithValue("@Id", id);

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
