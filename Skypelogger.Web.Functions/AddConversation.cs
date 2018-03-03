using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Net;
using System.Net.Http;
using System;
using System.Threading.Tasks;

namespace Skypelogger.Web.Functions
{
    public static class AddConversation
    {
        public const string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=kristofferrisa;AccountKey=1+7DV0MZO9Ih14Qh2PSjFhz1deRmc1QYJjBOybVa7v5qHVoE3j9Db5SwKGzG2p+CINa0HCr64alC3KAbgPou9g==;EndpointSuffix=core.windows.net;";
        public const string TableName = "Skypelogger";

        [FunctionName("AddConversation")]
        public static async Task<HttpResponseMessage> Run(
                [HttpTrigger(AuthorizationLevel.Anonymous,"get", "post", Route = null)]
            HttpRequestMessage req
            //, [Table(TableName, Connection = ConnectionString)]ICollector<ConversationEntity> outTable
            , TraceWriter log
            )
        {
            //log.Info("C# HTTP trigger function processed a request.");

            //string name = req.Query["name"];

            //string requestBody = req.Content.ReadAsStringAsync().Result;
            //dynamic data = JsonConvert.DeserializeObject<ConversationEntity>(requestBody);
            //dynamic data = JsonConvert.DeserializeObject(requestBody);

            //log.Info($"Tries to save from machine: {data.Machine}");

            // Create account, client and table
            var account = CloudStorageAccount.Parse(ConnectionString);
            var tableClient = account.CreateCloudTableClient();
            var table = tableClient.GetTableReference(TableName);
            await table.CreateIfNotExistsAsync();

            var data = new ConversationEntity()
            {
                PartitionKey = "Skypelogg",
                RowKey = Guid.NewGuid().ToString(),
                Id = new Guid(),
                Created = DateTime.Now,
                Machine = System.Environment.MachineName,
                Message = "test melding",
                Sender = "Kristian Risa"
            };

            // Insert new value in table
            var result = await table.ExecuteAsync(TableOperation.InsertOrMerge(data));

            //outTable.Add(data);
            //outTable.Add(
            //    new ConversationEntity()
            //    {
            //        PartitionKey = "Skypelogg",
            //        RowKey = Guid.NewGuid().ToString(),
            //        Id = new Guid(),
            //        Created = DateTime.Now,
            //        Machine = System.Environment.MachineName,
            //        Message = "test melding",
            //        Sender = "Kristian Risa"
            //    });

            //log.Info($"Azure table storage: {result.HttpStatusCode}");

            //if (result.HttpStatusCode == HttpStatusCode.OK)
            return req.CreateResponse(HttpStatusCode.Created);
            //else
            //    return req.CreateResponse(HttpStatusCode.BadRequest);
        }
    }
    public class ConversationEntity : TableEntity
    {
        public Guid Id { get; set; }
        public string Machine { get; set; }
        public DateTime Created { get; set; }
        public string Sender { get; set; }
        public string Message { get; set; }

    }
}
