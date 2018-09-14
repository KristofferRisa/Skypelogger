using System;
using System.Collections.Generic;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Skypelogger.Core.Models;

//[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace Skypelogger.Core
{
    public class SkypeloggerManager
    {
        private static readonly Dictionary<Conversation, ConversationData> ActiveConversations = new Dictionary<Conversation, ConversationData>();
        private Self _myself;
        //private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        string GetConversationId()
        {
            throw new NotImplementedException();
        }

        public void InstantMessageReceived(object sender, MessageSentEventArgs args)
        {
            var imm = ((InstantMessageModality)sender);
            if (imm != null)
            {
                var container = ActiveConversations[imm.Conversation];

                //String convlog = "[" + now + "] (Conv. #" + container.m_convId + ") <" + imm.Participant.Contact.GetContactInformation(ContactInformationType.DisplayName) + ">";
                //convlog += Environment.NewLine + args.Text;
                //using (StreamWriter outfile = new StreamWriter(mydocpath + programFolder + @"\AllLyncIMHistory.txt", true))
                //{
                //    outfile.WriteLine(convlog);
                //    outfile.Close();
                //}
                foreach (Participant participant in container.Conversation.Participants)
                {
                    if (participant.Contact == _myself.Contact)
                        continue;
                    //String directory = mydocpath + programFolder + @"\" + participant.Contact.GetContactInformation(ContactInformationType.DisplayName);
                    //if (!Directory.Exists(directory))
                    //    Directory.CreateDirectory(directory);

                    //TODO: Save data

                    //log.Info(filename);
                    //using (StreamWriter partfile = new StreamWriter(filename, true))
                    //{
                    //    partfile.WriteLine(convlog);
                    //    partfile.Close();
                    //}
                }
            }
        }

        public void AddConversation(object sender, ConversationManagerEventArgs e)
        {
            Console.WriteLine($"Ny samtale med {e.Conversation.Participants[1].Contact.GetContactInformation(ContactInformationType.DisplayName)}");
            var conversationData = new ConversationData()
            {
                Conversation = e.Conversation,
                Created = DateTime.Now,
                Id =  Guid.NewGuid()
            };

            ActiveConversations.Add(e.Conversation, conversationData);
            try
            {
                //log.Info($"{e.Conversation.Participants.FirstOrDefault().Contact.GetContactInformation(ContactInformationType.DisplayName)}");
                //log.Info($"{e.Participant.Contact.GetContactInformation(ContactInformationType.DisplayName)}");
                //using (StreamWriter outfile = new StreamWriter(appDataPath + programFolder + @"\nextConvId.txt", false))
                //{
                //    outfile.WriteLine(nextConvId);
                //    outfile.Close();
                //}
            }
            catch (Exception)
            {
                //ignore
            }
            e.Conversation.ParticipantAdded += AddParticipant;
            e.Conversation.ParticipantRemoved += RemoveParticipant;
           // var msg = $"Conversation #{container.m_convId} started.";
           
        }
        
        public void RemoveConversation(object sender, ConversationManagerEventArgs e)
        {
            Console.WriteLine($"Samtale lukket {e.Conversation.Participants[1].Contact.GetContactInformation(ContactInformationType.DisplayName)}");
            string ConversationID = e.Conversation.Properties[ConversationProperty.Id].ToString();
            e.Conversation.ParticipantAdded -= AddParticipant;
            e.Conversation.ParticipantRemoved -= RemoveParticipant;
            if (ActiveConversations.ContainsKey(e.Conversation))
            {
                ConversationData conversationData = ActiveConversations[e.Conversation];
                TimeSpan conversationLength = DateTime.Now.Subtract(conversationData.Created);
                //log.Info(String.Format("Conversation #{0} ended. It lasted {1} seconds", conversationData.m_convId, conversationLength.ToString(@"hh\:mm\:ss")));
                ActiveConversations.Remove(e.Conversation);

                //String s = String.Format("Conversation #{0} ended.", container.m_convId);
            
            }
        }

        private void RemoveParticipant(object sender, ParticipantCollectionChangedEventArgs args)
        {
            //((InstantMessageModality) args.Participant.Modalities[ModalityTypes.InstantMessage]).InstantMessageReceived -= InstantMessageReceived;
            if (args.Participant.Contact == _myself.Contact)
            {
                //log.Info("You were removed.");
            }
            else
            {
                //log.Info($"Participant was removed: {args.Participant.Contact.GetContactInformation(ContactInformationType.DisplayName)} .");
            }
        }


        public void AddParticipant(object sender, ParticipantCollectionChangedEventArgs args)
        {
            if (args != null)
            {
                ((InstantMessageModality) args.Participant.Modalities[ModalityTypes.InstantMessage])
                    .InstantMessageReceived += InstantMessageReceived;
                if (args.Participant.Contact == _myself.Contact)
                {
                    //TODO: Lagre meldingene
                    //log.Info($"Messages recevied: {}");
                }
            }
        }

        public void SetClient(LyncClient client)
        {
            _myself = client.Self;
            client.ConversationManager.ConversationAdded += this.AddConversation;
            client.ConversationManager.ConversationRemoved += this.RemoveConversation;

        }
    }

}
