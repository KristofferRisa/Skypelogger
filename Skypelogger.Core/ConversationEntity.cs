using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Skypelogger.Core
{
    public class ConversationEntity
    {
        public string Machine { get; set; }
        public string Created { get; set; }
        public string Sender { get; set; }
        public string Message{ get; set; }

    }
}
