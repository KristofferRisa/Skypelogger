using System;
using System.Collections.Generic;
using Microsoft.Lync.Model.Conversation;

namespace Skypelogger.Core
{ 
    public class ConversationData
    {
        public Guid Id { get; set; }
        public Conversation Conversation { get; set; }
        public DateTime Created { get; set; }
        public List<string> Metadata { get; set; }
    }
}
