using Microsoft.Lync.Model.Conversation;
using System;

//Here is the once-per-application setup information
namespace Skypelogger
{
    public class ConversationContainer
    {
        public Conversation Conversation { get; set; }
        public DateTime ConversationCreated { get; set; }
        public int m_convId;
    }
}
