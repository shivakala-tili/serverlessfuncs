using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Linq;

namespace ServerlessFuncs
{
    public static class TodoApi
    {
        [FunctionName("CreateTodo")]
        public static async Task<IActionResult> CreateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo")] HttpRequest req,
            [Table("todos",Connection ="AzureWebJobsStorage")]IAsyncCollector<TodoTableEntity> todoTable,
            ILogger log)
        {
            log.LogInformation("Creating a new todo item");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if(string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("Todo object is empty");
            }
            TodoCreateModel data = JsonConvert.DeserializeObject<TodoCreateModel>(requestBody);
            try
            {
                Todo model = new Todo() { TaskDescription = data.TaskDescription };
                await todoTable.AddAsync(model.ToTableEntity());

                return new OkObjectResult(model);
            }
            catch (StorageException ex)
            {

                log.LogError("Error in creating todo item", ex);
                return new BadRequestObjectResult("Error in creating todo item");
            }
        }

        [FunctionName("GetTodos")]
        public static async Task<IActionResult> GetTodos(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo")] HttpRequest req,
            [Table("todos", Connection = "AzureWebJobsStorage")]CloudTable todoTable,
            ILogger log)
        {
            log.LogInformation("Getting todo items");

            try
            {
                var query = new TableQuery<TodoTableEntity>();
                var segment = await todoTable.ExecuteQuerySegmentedAsync(query, null);
                return new OkObjectResult(segment.Select(Mappings.ToTodo));
            }
            catch (StorageException ex)
            {

                log.LogError("Error in getting todo item", ex);
                return new BadRequestObjectResult("Error in getting todo item");
            }
        }

        [FunctionName("GetTodoById")]
        public static IActionResult GetTodoById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo/{id}")] HttpRequest req,
            [Table("todos","TODO","{id}", Connection = "AzureWebJobsStorage")]TodoTableEntity todoTable,
            ILogger log,string id)
        {
            log.LogInformation($"Getting todo item by {id}");

            try
            {
                if(todoTable==null)
                {
                    log.LogInformation($"item {id} not found");
                    return new NotFoundResult();
                }
                return new OkObjectResult(todoTable.ToTodo());
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode==404)
            {
                log.LogError($"Error in getting todo item {id}", ex);
                return new BadRequestObjectResult($"Error in getting todo item {id}");
            }
        }

        [FunctionName("UpdateTodo")]
        public static async Task<IActionResult> UpdateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todo/{id}")] HttpRequest req,
            [Table("todos", Connection = "AzureWebJobsStorage")]CloudTable todoTable,
            ILogger log, string id)
        {
            log.LogInformation($"Updating todo item for {id}");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updated = JsonConvert.DeserializeObject<TodoUpdateModel>(requestBody);
            try
            {
                //find
                var findQuery = TableOperation.Retrieve<TodoTableEntity>("TODO", id);
                var findResult = await todoTable.ExecuteAsync(findQuery);
                if (findResult.Result == null)
                {
                    log.LogInformation($"item {id} not found");
                    return new NotFoundResult();
                }
                //update
                var existingRecord = (TodoTableEntity)findResult.Result;
                existingRecord.IsCompleted = updated.IsCompleted;
                if (!string.IsNullOrEmpty(updated.TaskDescription))
                {
                    existingRecord.TaskDescription = updated.TaskDescription;
                }
                await todoTable.ExecuteAsync(TableOperation.Replace(existingRecord));
                return new OkObjectResult(existingRecord.ToTodo());
            }
            catch (StorageException ex)
            {
                log.LogError($"Error in updating todo item {id}", ex);
                return new BadRequestObjectResult($"Error in updating todo item {id}");
            }
        }

        [FunctionName("DeleteTodo")]
        public static async Task<IActionResult> DeleteTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todo/{id}")] HttpRequest req,
            [Table("todos", Connection = "AzureWebJobsStorage")]CloudTable todoTable,
            ILogger log, string id)
        {
            log.LogInformation($"Deleting todo item: {id}");

            try
            {
                await todoTable.ExecuteAsync(TableOperation.Delete(new TableEntity()
                                { PartitionKey = "TODO", RowKey = id, ETag = "*" }));
                return new OkResult();
            }
            catch (StorageException ex) when(ex.RequestInformation.HttpStatusCode==404)
            {
                log.LogError($"item {id} not found", ex);
                return new NotFoundResult();
            }
            
        }
    }
}
