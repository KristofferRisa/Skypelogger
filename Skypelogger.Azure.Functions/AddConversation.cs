using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;

namespace Skypelogger.Azure.Functions
{
    public static class AddConversation
    {
        public const string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=kristofferrisa;AccountKey=1+7DV0MZO9Ih14Qh2PSjFhz1deRmc1QYJjBOybVa7v5qHVoE3j9Db5SwKGzG2p+CINa0HCr64alC3KAbgPou9g==;EndpointSuffix=core.windows.net;";

        [FunctionName("AddConversation")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]HttpRequestMessage req
            , ICollector<ConversationEntity> outTable
            , TraceWriter log)
        {
            dynamic data = await req.Content.ReadAsAsync<object>();
            string message = data?.message;

            if (message == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name in the request body");
            }

            outTable.Add(new ConversationEntity()
            {
                PartitionKey = "Functions",
                RowKey = Guid.NewGuid().ToString(),
                Message = message
            });
            return req.CreateResponse(HttpStatusCode.Created, "Created");
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
}
