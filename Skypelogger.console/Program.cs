using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kurukuru;
using Microsoft.Lync.Model;
using Polly;

namespace Skypelogger.console
{
    class Program
    {
        private static LyncClient _client;
        private static core.SkypeloggerManager _skypeloggerManager;
        static void Main(string[] args)
        {
            _client = null;
            _skypeloggerManager = new core.SkypeloggerManager();

            Console.WriteLine("Starter SkypeloggerManager client");
            //https://github.com/mayuki/Kurukuru
            Spinner.Start("Connecting to Skype client", spinner => {

                var policy = Policy.Handle<ClientNotFoundException>()
                .WaitAndRetryForever(retryAttempt =>
                        TimeSpan.FromSeconds(2),
                    onRetry: (exception, calculatedWaitDuration) => // Capture some info for logging!
                    {
                        spinner.Text = "Waiting on Skype client";

                    });
                try
                {
                    policy.Execute(() =>
                    {
                        _client = LyncClient.GetClient();
                        _skypeloggerManager.SetClient(_client);
                        //Console.WriteLine("Koplet til Skype");
                        
                        spinner.Text = "Connect to Skype";
                        
                    });


                }
                catch (Exception e)
                {
                    spinner.Fail("Not connected");
                    //Console.WriteLine("Feilet!");
                    Console.WriteLine(e.Message);
                }
            }, Patterns.Dots2);
           

            Console.WriteLine("Trykk en tast for å avslutte...");
            Console.ReadLine();
        }
    }
}
