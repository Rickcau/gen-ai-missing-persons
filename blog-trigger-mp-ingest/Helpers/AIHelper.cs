using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Configuration;
using blog_trigger_mp_ingest.Prompts;
using blog_trigger_mp_ingest.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace blog_trigger_mp_ingest.Helpers
{
    public class AIHelper
    {
        private Kernel _kernel;
        private readonly ILogger<AIHelper> _logger; // Injected logger

        public string azure_StorageConnectionString { get; private set; }
        public string inbound_MP_PdfContainer { get; private set; }
        public string processed_MP_PdfContainer { get; private set; }
        public string failed_MP_PdfContainer { get; private set; }

        public AIHelper(Kernel kernel, ILogger<AIHelper> logger)
        {
            this._kernel = kernel;
            this._logger = logger;

            // Initialize the container names from environment variables
            azure_StorageConnectionString = Environment.GetEnvironmentVariable("MpBlobConnectionString", EnvironmentVariableTarget.Process) ?? "";
            inbound_MP_PdfContainer = Environment.GetEnvironmentVariable("INBOUND_MP_PDF_CONTAINER", EnvironmentVariableTarget.Process) ?? "";
            processed_MP_PdfContainer = Environment.GetEnvironmentVariable("PROCESSED_MP_PDF_CONTAINER", EnvironmentVariableTarget.Process) ?? "";
            failed_MP_PdfContainer = Environment.GetEnvironmentVariable("FAILED_MP_PDF_CONTAINER", EnvironmentVariableTarget.Process) ?? "";
        }

        public async Task<MissingPerson> GenerateJSONStructureAsync(Stream pdfStream, string name)
        {

            string extractedText = ExtractTextFromPdf(pdfStream);

            // Step 2: Analyze the extracted text using Azure OpenAI
            var missingPersonData = await AnalyzePdfText(extractedText);

            // Check if missingPersonData is null
            if (missingPersonData == null)
            {
                // Log the error or handle the null case as needed
                // For example, you might throw an exception or return a default value
                throw new InvalidOperationException("Failed to analyze PDF text and generate MissingPerson data.");
                // Alternatively, return a default value or an empty object
                // return new MissingPerson();
            }

            return missingPersonData ;
        }

        private string ExtractTextFromPdf(Stream pdfStream)
        {
            StringBuilder text = new StringBuilder();

            using (PdfReader pdfReader = new PdfReader(pdfStream))
            using (PdfDocument pdfDoc = new PdfDocument(pdfReader))
            {
                for (int pageNumber = 1; pageNumber <= pdfDoc.GetNumberOfPages(); pageNumber++)
                {
                    var page = pdfDoc.GetPage(pageNumber);
                    var strategy = new SimpleTextExtractionStrategy();
                    string pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                    text.Append(pageText);
                }
            }

            return text.ToString();
        }

        private async Task<MissingPerson?> AnalyzePdfTextOld(string pdfText)
        {
            try
            {
                JsonSerializerOptions s_options = new() { WriteIndented = true };
                var executionSettings = new OpenAIPromptExecutionSettings
                {
                    ResponseFormat = "json_object"
                };

                var extractionPrompt =  MissingPersonsPluginPrompts.GetMissingPersonsExtractPrompt(pdfText);
                
                var extractionResult = await _kernel.InvokePromptAsync(extractionPrompt);

                var extractionResultString = extractionResult.GetValue<string>();

                MissingPerson? missingPersonData = JsonSerializer.Deserialize<MissingPerson>(extractionResultString   ?? string.Empty, s_options);

                return missingPersonData ;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }

        private async Task<MissingPerson?> AnalyzePdfText(string pdfText)
        {
            try
            {
                // Configure JSON serializer options
                JsonSerializerOptions s_options = new() { WriteIndented = true };

                // Set up execution settings for OpenAI prompt
                var executionSettings = new OpenAIPromptExecutionSettings
                {
                    ResponseFormat = "json_object"
                };

                // Create the extraction prompt
                var extractionPrompt = MissingPersonsPluginPrompts.GetMissingPersonsExtractPrompt(pdfText);

                // Invoke the prompt asynchronously
                var extractionResult = await _kernel.InvokePromptAsync(extractionPrompt);

                // Get the result as a string
                var extractionResultString = extractionResult.GetValue<string>();

                // Log the result for debugging
                _logger.LogDebug("Extraction Result: {ExtractionResult}", extractionResultString);

                // Deserialize the result into the MissingPerson class
                MissingPerson? missingPersonData = JsonSerializer.Deserialize<MissingPerson>(extractionResultString ?? "", s_options);

                var location = await GetLocationAsync(missingPersonData?.MissingFrom ?? "");
                if (missingPersonData != null)
                {
                    missingPersonData.Latitude = location?.Latitude;
                    missingPersonData.Longitude = location?.Longitude;
                }

                return missingPersonData;
            }
            catch (JsonException jsonEx)
            {
                // Log JSON deserialization errors
                _logger.LogError(jsonEx, "Failed to deserialize extraction result.");
                throw; // Rethrow the exception if you want to propagate it
            }
            catch (Exception ex)
            {
                // Log other types of errors
                _logger.LogError(ex, "An error occurred while analyzing PDF text.");
                throw; // Rethrow the exception if you want to propagate it
            }
        }
    

      private async Task<Location?> GetLocationAsync(string missingFrom)
        {
            try
            {
                // Configure JSON serializer options
                JsonSerializerOptions s_options = new() { WriteIndented = true };

                // Set up execution settings for OpenAI prompt
                var executionSettings = new OpenAIPromptExecutionSettings
                {
                    ResponseFormat = "json_object"
                };

                // Create the extraction prompt
                var getLatLongPrompt = MissingPersonsPluginPrompts.GetLatitudeLongitudePrompt(missingFrom);

                // Invoke the prompt asynchronously
                var lagLongResult = await _kernel.InvokePromptAsync(getLatLongPrompt);

                // Get the result as a string
                var latLongResultString = lagLongResult .GetValue<string>();

                // Log the result for debugging
                _logger.LogDebug("Get Latitude and Longitude Result: {extractionResult}", latLongResultString);

                // Deserialize the result into the MissingPerson class
                Location? missingPersonGeocode = JsonSerializer.Deserialize<Location>(latLongResultString ?? "", s_options);

                return missingPersonGeocode;
            }
            catch (JsonException jsonEx)
            {
                // Log JSON deserialization errors
                _logger.LogError(jsonEx, "Failed to deserialize latLong result.");
                throw; // Rethrow the exception if you want to propagate it
            }
            catch (Exception ex)
            {
                // Log other types of errors
                _logger.LogError(ex, "An error occurred while trying to get Latitude and Longitude PDF text.");
                throw; // Rethrow the exception if you want to propagate it
            }
        }
    }
}
