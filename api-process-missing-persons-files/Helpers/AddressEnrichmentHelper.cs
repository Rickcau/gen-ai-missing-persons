using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.IO;
using System.Data.SqlClient;
using System.Net.Http;
using api_process_missing_persons_files.Models;

namespace api_process_missing_persons_files.Helpers
{
    public class AddressEnrichmentHelper
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly string _connectionString;
        private readonly string _mapsApiKey;

        public AddressEnrichmentHelper(
            ILogger logger,
            HttpClient httpclient,
            string connectionString,
            string mapsApiKey)
        {
            _logger = logger;
            _httpClient = httpclient;
            _connectionString = connectionString;
            _mapsApiKey = mapsApiKey;
        }

        public async Task<HttpResponseData?> HandleEnrichmentRequest(HttpRequestData? req, string? requestBody)
        {
            _logger.LogInformation("Manual address enrichment function processed a request.");

            if (req == null)
            {
                await EnrichAddresses();
                return null; // This is for the scheduled task which doesn't need a response
            }

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
                    return await ProcessSpecificAddress(req, idElement.GetInt32());
                }
                else
                {
                    return await ProcessAllAddresses(req);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Invalid JSON in request body: {ex.Message}");
                HttpResponseData response = req.CreateResponse(HttpStatusCode.BadRequest);
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

        private async Task<HttpResponseData> ProcessSpecificAddress(HttpRequestData req, int id)
        {
            await EnrichSpecificAddress(id);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Address enrichment process completed for ID: {id}");
            return response;
        }

        public async Task EnrichAddresses()
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
                throw;
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
                            _logger.LogWarning($"Either a records ID {id} was not found or the IsEnriched = True for this record");
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
                }
            }
        }

        //private async Task<EnrichedAddress?> EnrichAddressWithAzureMaps(string address)
        //{
        //    string encodedAddress = Uri.EscapeDataString(address);
        //    string url = $"https://atlas.microsoft.com/search/address/json?subscription-key={_mapsApiKey}&api-version=1.0&language=en-US&query={encodedAddress}, Detroit";

        //    HttpResponseMessage response = await _httpClient.GetAsync(url);
        //    if (!response.IsSuccessStatusCode)
        //    {
        //        _logger.LogError($"Azure Maps API request failed: {response.StatusCode}");
        //        return null;
        //    }

        //    string responseBody = await response.Content.ReadAsStringAsync();
        //    using (JsonDocument document = JsonDocument.Parse(responseBody))
        //    {
        //        JsonElement root = document.RootElement;
        //        JsonElement results = root.GetProperty("results");

        //        if (results.GetArrayLength() > 0)
        //        {
        //            JsonElement firstResult = results[0];
        //            return new EnrichedAddress
        //            {
        //                MatchConfidence = firstResult.GetProperty("score").GetDouble(),
        //                StreetNumber = GetJsonPropertyString(firstResult, "address", "streetNumber"),
        //                StreetName = GetJsonPropertyString(firstResult, "address", "streetName"),
        //                Municipality = GetJsonPropertyString(firstResult, "address", "municipality"),
        //                Neighbourhood = GetJsonPropertyString(firstResult, "address", "neighbourhood"),
        //                CountrySecondarySubdivision = GetJsonPropertyString(firstResult, "address", "countrySecondarySubdivision"),
        //                CountrySubdivisionName = GetJsonPropertyString(firstResult, "address", "countrySubdivisionName"),
        //                PostalCode = GetJsonPropertyString(firstResult, "address", "postalCode"),
        //                ExtendedPostalCode = GetJsonPropertyString(firstResult, "address", "extendedPostalCode"),
        //                Latitude = firstResult.GetProperty("position").GetProperty("lat").GetDouble(),
        //                Longitude = firstResult.GetProperty("position").GetProperty("lon").GetDouble()
        //            };
        //        }
        //    }

        //    return null;
        //}

        private async Task<EnrichedAddress?> EnrichAddressWithAzureMaps(string address)
        {
            string encodedAddress = Uri.EscapeDataString(address);
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
                    // Sort the results by score in descending order
                    var sortedResults = results.EnumerateArray()
                        .Select(result => new
                        {
                            Score = result.GetProperty("score").GetDouble(),
                            Result = result
                        })
                        .OrderByDescending(item => item.Score)
                        .ToList();

                    // Get the result with the highest score
                    var highestScoredResult = sortedResults.First().Result;

                    return new EnrichedAddress
                    {
                        MatchConfidence = highestScoredResult.GetProperty("score").GetDouble(),
                        StreetNumber = GetJsonPropertyString(highestScoredResult, "address", "streetNumber"),
                        StreetName = GetJsonPropertyString(highestScoredResult, "address", "streetName"),
                        Municipality = GetJsonPropertyString(highestScoredResult, "address", "municipality"),
                        Neighbourhood = GetJsonPropertyString(highestScoredResult, "address", "neighbourhood"),
                        CountrySecondarySubdivision = GetJsonPropertyString(highestScoredResult, "address", "countrySecondarySubdivision"),
                        CountrySubdivisionName = GetJsonPropertyString(highestScoredResult, "address", "countrySubdivisionName"),
                        PostalCode = GetJsonPropertyString(highestScoredResult, "address", "postalCode"),
                        ExtendedPostalCode = GetJsonPropertyString(highestScoredResult, "address", "extendedPostalCode")?.Split(',').FirstOrDefault()?.Trim(),
                        Latitude = highestScoredResult.GetProperty("position").GetProperty("lat").GetDouble(),
                        Longitude = highestScoredResult.GetProperty("position").GetProperty("lon").GetDouble()
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