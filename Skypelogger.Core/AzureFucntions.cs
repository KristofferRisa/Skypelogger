using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Skypelogger.Core
{
    public class AzureFucntions
    {
        public AzureFucntions()

        {

        }

        public AzureFucntions(string url)
        {
            Url = url;
        }

        public void SetUrl(string url) => Url = url;

        private string Url = "http://localhost:7071/api/AddConversation";
        
        public HttpResponseMessage SendMessage(string message, string sender)
        {
            //Sending data to Azure functions
            var data = new ConversationEntity()
            {
                Machine = Environment.MachineName,
                Message = message,
                Sender = sender,
                Created = System.DateTime.Now.ToString()
            };

            var serializer = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var jsonData = JsonConvert.SerializeObject(data, serializer);

            HttpResponseMessage result;
            //TODO: Extract out the sending of data
            using (var client = new HttpClient())
            {
                var uri = new Uri(Url);
                var content = new StringContent(
                        jsonData
                        , Encoding.UTF8, "application/json");
                result = client.PostAsync(uri, content).Result;

                //string resultContent = result.Content.ReadAsStringAsync().Result;
                //log.Debug(resultContent);
            }
            return result;
        }
    }
}
