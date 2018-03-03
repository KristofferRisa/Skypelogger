using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Skypelogger.Core;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace Skypelogger.test.functions
{
    public class AddNewMessageUnitTest
    {
        [Fact]
        public void AddNewMessage()
        {
            string sender = "Kenneth.Risa@test";
            String convlog = "[" + DateTime.Now + "] (" 
                + sender 
                + ") > " 
                + "Test message" ;
            
            AzureFucntions azure = new AzureFucntions("http://localhost:7071/api/AddConversation");
            var result = azure.SendMessage(convlog, sender);
            Assert.NotNull(azure);
            Assert.NotEqual(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,result.StatusCode);
        }
    }
}
