using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace ServerlessFuncs
{
    public static class BlobTrigger
    {
        [FunctionName("BlobTrigger")]
        public static async Task Run([BlobTrigger("todo-images/{name}", Connection = "AzureWebJobsStorage")]CloudBlockBlob myBlob,
             [Table("todos", Connection = "AzureWebJobsStorage")]CloudTable todoTable,
             [Blob("todo-images-copy/{name}",FileAccess.Write, Connection ="AzureWebJobsStorage")]Stream copy,
            string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Blob properties: {JsonConvert.SerializeObject(myBlob.Properties)}");
            string id = Path.GetFileNameWithoutExtension(name);
            try
            {
                //find
                var findQuery = TableOperation.Retrieve<TodoTableEntity>("TODO", id);
                var findResult = await todoTable.ExecuteAsync(findQuery);
                if (findResult.Result == null)
                {
                    log.LogInformation($"item {id} not found");
                    return;
                }
                //update
                var existingRecord = (TodoTableEntity)findResult.Result;
                existingRecord.IsCompleted = true;
                await todoTable.ExecuteAsync(TableOperation.Replace(existingRecord));

                //copy.Position = 0;
                await myBlob.DownloadToStreamAsync(copy);
            }  
            
            catch (StorageException ex)
            {
                log.LogError($"Blob Trigger: Error in updating todo item {id}", ex);
            }
        }
    }
}
