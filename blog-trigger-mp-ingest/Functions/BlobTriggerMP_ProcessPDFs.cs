using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using blog_trigger_mp_ingest.Helpers;
using blog_trigger_mp_ingest.Models;
using Azure.Storage.Blobs;

namespace blog_trigger_mp_ingest.Functions
{
    public class BlobTriggerMP_ProcessPDFs
    {
        private readonly ILogger<BlobTriggerMP_ProcessPDFs> _logger;
        private readonly Kernel _kernel;
        private readonly AIHelper _aiHelper;

        public BlobTriggerMP_ProcessPDFs(ILogger<BlobTriggerMP_ProcessPDFs> logger, ILogger<AIHelper> aiLogger, Kernel kernel)
        {
            _logger = logger;
            _kernel = kernel;
            _aiHelper = new AIHelper(_kernel, aiLogger);
        }

        [Function(nameof(BlobTriggerMP_ProcessPDFs))]
        public async Task Run([BlobTrigger("inbound-mp-pdfs/{name}", Connection = "MpBlobConnectionString")] Stream stream, string name)
        {
            try
            {
                using var blobStreamReader = new StreamReader(stream);
                var content = await blobStreamReader.ReadToEndAsync();
                stream.Position = 0;

                MemoryStream memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}");

                MissingPerson result = await _aiHelper.GenerateJSONStructureAsync(memoryStream, name);
                string sqlConnectionString = Environment.GetEnvironmentVariable("DatabaseConnection") ?? "";

                SQLMissingPersonHelper sqlmissingpersonhelper = new SQLMissingPersonHelper(sqlConnectionString);
                await sqlmissingpersonhelper.InsertMissingPersonAsync(result);

                Console.WriteLine($@"Result: {result}");
                _logger.LogInformation($"Result: {result}");

                // If we've reached this point, processing was successful
                await MoveBlobAsync(name, _aiHelper.processed_MP_PdfContainer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing PDF {name}. Moving to failed container.");
                await MoveBlobAsync(name, _aiHelper.failed_MP_PdfContainer);
            }
        }

        private async Task MoveBlobAsync(string blobName, string destinationContainerName)
        {
            var blobServiceClient = new BlobServiceClient(_aiHelper.azure_StorageConnectionString);
            var sourceContainerClient = blobServiceClient.GetBlobContainerClient(_aiHelper.inbound_MP_PdfContainer);
            var destinationContainerClient = blobServiceClient.GetBlobContainerClient(destinationContainerName);

            var sourceBlobClient = sourceContainerClient.GetBlobClient(blobName);
            var destinationBlobClient = destinationContainerClient.GetBlobClient(blobName);

            await destinationBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            await sourceBlobClient.DeleteAsync();

            _logger.LogInformation($"Moved blob {blobName} from {_aiHelper.inbound_MP_PdfContainer} to {destinationContainerName}");
        }
    }
}
