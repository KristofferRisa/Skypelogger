using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using System.Text;
using Skypelogger.Core;

//Here is the once-per-application setup information
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace Skypelogger
{
    class Program : Form
    {
        Program()
        {
            InitializeComponent();
            Resize += Form_Resize;
            labelProgramFolder.Text = mydocpath + programFolder;
            
            labelProgramFolder.Click += new EventHandler(OpenFolder);
        }     

        private void OpenFolder(object sender, EventArgs e)
        {
             Process.Start(mydocpath + programFolder);
            
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static Dictionary<Conversation, ConversationContainer> ActiveConversations =
            new Dictionary<Conversation, ConversationContainer>();

        /**
         * user (participant) using the lync
         */
        static Self myself;

        static int nextConvId = 0;
        private TextBox consoleBox;

        static string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        static string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        static string programFolder = @"\Skypelog";
        static LyncClient client;

        static Program ProgramRef;
        static Timer keepAliveTimer = new Timer();

        #region private fields
        private NotifyIcon notifyIcon;
        private System.ComponentModel.IContainer components;

        private const int BALLOON_POPUP_TIMEOUT_MS = 3000;
        private const int KEEP_ALIVE_INTERVAL_MS = 5000;
        private const int CONNECT_RETRY_WAIT_TIME_MS = 5000;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem configToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private Label label2;
        private Label label1;
        private Label labelProgramFolder;
        private ToolStripMenuItem reconnectToolStripMenuItem;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private const int CONNECT_RETRY_MAX = -1; // -1 to retry indefinitely
        #endregion

        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            ProgramRef = new Program();
            ProgramRef.Shown += ApplicationShown;
            Application.Run(ProgramRef);
            ProgramRef.Shown -= ApplicationShown;
        }

        static void ApplicationShown(object sender, EventArgs args)
        {
            (sender as Program).Prepare();
            (sender as Program).Connect();
        }

        private void KeepAliveTimerProcessor(Object myObject, EventArgs myEventArgs)
        {
            if (client != null && client.State != ClientState.Invalid)
            {
                return;
            }

            keepAliveTimer.Stop();
            log.Warn("Lost connection to Lync client; retrying connection");
            Connect();
        }
                
        private void Prepare()
        {
            try
            {
                //read previous conversation ID
                StreamReader idFile = new StreamReader(appDataPath + programFolder + @"\nextConvId.txt");
                nextConvId = int.Parse(idFile.ReadLine());
                log.Info("Last conversation number found: " + nextConvId);
            }
            catch (Exception)
            {
                nextConvId = 1;
                log.Info("No previous conversation number found. Using default.");
            }

            keepAliveTimer.Tick += new EventHandler(KeepAliveTimerProcessor);
            keepAliveTimer.Interval = KEEP_ALIVE_INTERVAL_MS;
        }

        private async void Connect()
        {
            client = null;
            bool tryAgain = false;
            int attempts = 0;
            do
            {
                tryAgain = false;
                attempts++;
                try
                {
                    if (attempts > 1)
                    {
                        log.Info($"Connecting to Skype. Attempt {attempts}...");
                    }                        
                    else
                    {
                        log.Info("Connecting to Skype...");
                    }   
                    client = LyncClient.GetClient();
                }
                catch (LyncClientException _exception)
                {
                    log.Warn(_exception);
                    tryAgain = true;
                    if (CONNECT_RETRY_MAX < 0 || attempts <= CONNECT_RETRY_MAX)
                    {
                        log.Warn($"Client not found. Trying again in {CONNECT_RETRY_WAIT_TIME_MS / 1000} seconds.");
                        await Task.Delay(CONNECT_RETRY_WAIT_TIME_MS);
                    }
                    else
                    {
                        log.Error("Client not found. Too many attempts. Giving up.");
                        Console.ReadLine();
                        return;
                    }
                }
            } while (tryAgain);
            myself = client.Self;

            if (!Directory.Exists(mydocpath + programFolder))
                Directory.CreateDirectory(mydocpath + programFolder);

            if (!Directory.Exists(appDataPath + programFolder))
                Directory.CreateDirectory(appDataPath + programFolder);

            client.ConversationManager.ConversationAdded += ConversationManager_ConversationAdded;
            client.ConversationManager.ConversationRemoved += ConversationManager_ConversationRemoved;

            log.Info("Ready!");
            Console.ReadLine();

            keepAliveTimer.Enabled = true;
        }

        private void ConversationManager_ConversationAdded(object sender, ConversationManagerEventArgs e)
        {
            ConversationContainer container = new ConversationContainer()
            {
                Conversation = e.Conversation,
                ConversationCreated = DateTime.Now,
                m_convId = nextConvId++
            };
            ActiveConversations.Add(e.Conversation, container);
            try
            {
                using (StreamWriter outfile = new StreamWriter(appDataPath + programFolder + @"\nextConvId.txt", false))
                {
                    outfile.WriteLine(nextConvId);
                    outfile.Close();
                }
            }
            catch(Exception)
            {
                //ignore
            }
            e.Conversation.ParticipantAdded += Conversation_ParticipantAdded;
            e.Conversation.ParticipantRemoved += Conversation_ParticipantRemoved;
            var msg = $"Conversation #{container.m_convId} started.";
            log.Info(msg);
            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon.BalloonTipText = msg;
                notifyIcon.ShowBalloonTip(BALLOON_POPUP_TIMEOUT_MS);
            }
        }

        private void Conversation_ParticipantRemoved(object sender, ParticipantCollectionChangedEventArgs args)
        {
             (args.Participant.Modalities[ModalityTypes.InstantMessage] as InstantMessageModality).InstantMessageReceived -= InstantMessageModality_InstantMessageReceived;
            if (args.Participant.Contact == myself.Contact)
            {
                log.Info("You were removed.");
            }                
            else
            {
                log.Info($"Participant was removed: {args.Participant.Contact.GetContactInformation(ContactInformationType.DisplayName)} .");
            }                
        }

        private void Conversation_ParticipantAdded(object sender, ParticipantCollectionChangedEventArgs args)
        {
            (args.Participant.Modalities[ModalityTypes.InstantMessage] as InstantMessageModality).InstantMessageReceived += InstantMessageModality_InstantMessageReceived;
            if (args.Participant.Contact == myself.Contact)
            {
                log.Info("You were added.");
            }                
            else
            {
                log.Info($"Participant was added: {args.Participant.Contact.GetContactInformation(ContactInformationType.DisplayName)}.");
            }                
        }

        void InstantMessageModality_InstantMessageReceived(object sender, MessageSentEventArgs args)
        {
            InstantMessageModality imm = (sender as InstantMessageModality);
            ConversationContainer container = ActiveConversations[imm.Conversation];
            DateTime now = DateTime.Now;
            String convlog = "[" + now + "] (" + imm.Participant.Contact.GetContactInformation(ContactInformationType.DisplayName) + ") > " + args.Text;
            using (StreamWriter outfile = new StreamWriter(mydocpath + programFolder +@"\AllLyncIMHistory.txt", true))
            {
                outfile.WriteLine(convlog);
                outfile.Close();
            }
            foreach (Participant participant in container.Conversation.Participants)
            {
                if (participant.Contact == myself.Contact)
                    continue;
                String directory = mydocpath + programFolder + @"\" + participant.Contact.GetContactInformation(ContactInformationType.DisplayName);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string dateString = now.ToString("yyyy-MM-dd");
                String filename = directory + @"\" + dateString + ".txt";
                //log.Info(filename);

                using (StreamWriter partfile = new StreamWriter(filename, true))
                {
                    partfile.WriteLine(convlog);
                    partfile.Close();
                }

                //AzureFucntions azure = new AzureFucntions("https://functionappskypelogger.azurewebsites.net/api/AddConversation_v1?code=xlUxifmiAlp84Cqkgx2bdnOaw11EpVbetHlOR84Z5zWmxdq07DOlHA==");
                AzureFucntions azure = new AzureFucntions("https://functionappskypelogger.azurewebsites.net/api/AddConversation_v2?code=lUZ4KdCT1oblL2u1zc6pgvCsRWRNpCqEFnMDOdyV62ralONkgmGALg==");
                azure.SendMessage(
                    convlog,
                    participant.Contact.GetContactInformation(ContactInformationType.DisplayName).ToString()
                        );
                
            }

            log.Info(convlog);
        }

        private void ConversationManager_ConversationRemoved(object sender, ConversationManagerEventArgs e)
        {
            string ConversationID = e.Conversation.Properties[ConversationProperty.Id].ToString();
            e.Conversation.ParticipantAdded -= Conversation_ParticipantAdded;
            e.Conversation.ParticipantRemoved -= Conversation_ParticipantRemoved;
            if (ActiveConversations.ContainsKey(e.Conversation))
            {
                ConversationContainer container = ActiveConversations[e.Conversation];
                TimeSpan conversationLength = DateTime.Now.Subtract(container.ConversationCreated);
                log.Info(String.Format("Conversation #{0} ended. It lasted {1} seconds", container.m_convId, conversationLength.ToString(@"hh\:mm\:ss")));
                ActiveConversations.Remove(e.Conversation);

                String s = String.Format("Conversation #{0} ended.", container.m_convId);
                if (WindowState == FormWindowState.Minimized)
                {
                    notifyIcon.BalloonTipText = s;
                    notifyIcon.ShowBalloonTip(BALLOON_POPUP_TIMEOUT_MS);
                }
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.consoleBox = new System.Windows.Forms.TextBox();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reconnectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.configToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.labelProgramFolder = new System.Windows.Forms.Label();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // consoleBox
            // 
            this.consoleBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.consoleBox.Location = new System.Drawing.Point(12, 216);
            this.consoleBox.Multiline = true;
            this.consoleBox.Name = "consoleBox";
            this.consoleBox.ReadOnly = true;
            this.consoleBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.consoleBox.Size = new System.Drawing.Size(669, 270);
            this.consoleBox.TabIndex = 1;
            this.consoleBox.TabStop = false;
            // 
            // notifyIcon
            // 
            this.notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.notifyIcon.BalloonTipText = "Skypelogger minimized";
            this.notifyIcon.BalloonTipTitle = "Skypelogger";
            this.notifyIcon.Icon = this.Icon;
            this.notifyIcon.Text = "Skypelogger";
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(693, 42);
            this.menuStrip1.TabIndex = 2;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reconnectToolStripMenuItem,
            this.configToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(64, 38);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // reconnectToolStripMenuItem
            // 
            this.reconnectToolStripMenuItem.Name = "reconnectToolStripMenuItem";
            this.reconnectToolStripMenuItem.Size = new System.Drawing.Size(268, 38);
            this.reconnectToolStripMenuItem.Text = "Reconnect";
            this.reconnectToolStripMenuItem.Click += new System.EventHandler(this.reconnectToolStripMenuItem_Click);
            // 
            // configToolStripMenuItem
            // 
            this.configToolStripMenuItem.Enabled = false;
            this.configToolStripMenuItem.Name = "configToolStripMenuItem";
            this.configToolStripMenuItem.Size = new System.Drawing.Size(268, 38);
            this.configToolStripMenuItem.Text = "Hide";
            this.configToolStripMenuItem.Click += new System.EventHandler(this.configToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(268, 38);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(92, 38);
            this.aboutToolStripMenuItem.Text = "About";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 49);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(125, 39);
            this.label1.TabIndex = 3;
            this.label1.Text = "Folder:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(12, 90);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(257, 39);
            this.label2.TabIndex = 4;
            this.label2.Text = "Program folder:";
            // 
            // labelProgramFolder
            // 
            this.labelProgramFolder.AutoSize = true;
            this.labelProgramFolder.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelProgramFolder.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Italic | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelProgramFolder.Location = new System.Drawing.Point(280, 95);
            this.labelProgramFolder.Name = "labelProgramFolder";
            this.labelProgramFolder.Size = new System.Drawing.Size(86, 31);
            this.labelProgramFolder.TabIndex = 5;
            this.labelProgramFolder.Text = "label3";
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            this.settingsToolStripMenuItem.Size = new System.Drawing.Size(268, 38);
            this.settingsToolStripMenuItem.Text = "Settings";
            this.settingsToolStripMenuItem.Click += new System.EventHandler(this.settingsToolStripMenuItem_Click);
            // 
            // Program
            // 
            this.ClientSize = new System.Drawing.Size(693, 532);
            this.Controls.Add(this.labelProgramFolder);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.consoleBox);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Program";
            this.Text = "Skypelogger";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void Form_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon.Visible = true;
                notifyIcon.BalloonTipText = "Skypelogger minimized";
                notifyIcon.ShowBalloonTip(BALLOON_POPUP_TIMEOUT_MS);
                ShowInTaskbar = false;
            }
        }

        private void notifyIcon_MouseDoubleClick(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            notifyIcon.Visible = false;
        }

        private void configToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Hide();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void reconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Connect();
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Hide();
            var settings = new SettingsForm();
            settings.ShowDialog();
            this.Show();
        }
    }

    delegate void SetTextCallback(string text);
}
