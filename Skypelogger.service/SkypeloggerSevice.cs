using System;
using System.ServiceProcess;
using Microsoft.Lync.Model;
using Polly;

namespace Skypelogger.service
{
    public class SkypeloggerSevice : ServiceBase
    {
        static LyncClient _client;
        private readonly core.SkypeloggerManager _skypeloggerManager;

        public SkypeloggerSevice()
        {
            _client = null;
            _skypeloggerManager = new core.SkypeloggerManager();
        }

        protected override void OnStart(string[] args)
        {
            var policy = Policy.Handle<LyncClientException>()
                .WaitAndRetryForever(retryAttempt =>
                    TimeSpan.FromSeconds(2)
                );

            try
            {
                policy.Execute(() =>
                {
                    _client = LyncClient.GetClient();
                    _client.ConversationManager.ConversationAdded += _skypeloggerManager.AddConversation;
                    _client.ConversationManager.ConversationRemoved += _skypeloggerManager.RemoveConversation;
                });
            }
            catch (Exception e)
            {
                Stop();
            }

        }

        protected override void OnStop()
        {
        }
    }
}