using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Configuration;

namespace FileCreationFuncApp
{
    public static class BlobFileCreation
    {
        const string blobStorageConnection = "BlobStorageConnection";

        [FunctionName("FileCreationFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string containerName = "testcontainer";
            bool isTriggerFile = false;
            string triggerFilePath = string.Empty;

            //string name = req.Query["fileName"];
            string triggerFile = req.Query["fileName"];
            string path = req.Query["filePath"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            //name = name ?? data?.name;
            triggerFile ??= data?.fileName;
            path ??= data?.filePath;

            var sc = new StorageClient(Helper.GetEnvironmentVariable(blobStorageConnection));

            //Get blob container client
            var bcc = sc.blobServiceClient.GetBlobContainerClient(containerName);
            await bcc.CreateIfNotExistsAsync();

            var blobs = bcc.GetBlobsAsync(BlobTraits.All, BlobStates.None, prefix: path);

            await foreach (var blobItem in blobs)
            {
                Console.WriteLine(blobItem.Name);
                
                int lastIndex = blobItem.Name.LastIndexOf('/');
                string filePath = blobItem.Name[..lastIndex];

                triggerFilePath = $"{filePath}/{triggerFile}";
                var blob = bcc.UploadBlob(triggerFilePath, Stream.Null);
                isTriggerFile=true;
            }

            string responseMessage = isTriggerFile
                ? $"Trigger file created - {triggerFilePath}"
                : $"FAIL. No trigger file generated. Check the params";

            return new OkObjectResult(responseMessage);
        }        
    }

    public static class Helper
    {
        internal static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }

    public class StorageClient
    {
        private BlobServiceClient _blobServiceClient;
        private readonly string? _blobConnectionString;

        public StorageClient(string blobConnectionString)
        {
            _blobConnectionString = blobConnectionString;
        }

        public BlobServiceClient blobServiceClient
        {
            get
            {
                _blobServiceClient = new BlobServiceClient(_blobConnectionString);
                return _blobServiceClient;
            }
        }
    }
}
