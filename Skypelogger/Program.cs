using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

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
            String convlog = "[" + now + "] (Conv. #" + container.m_convId + ") <" + imm.Participant.Contact.GetContactInformation(ContactInformationType.DisplayName) + ">";
            convlog += Environment.NewLine + args.Text;
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Program));
            consoleBox = new TextBox();
            notifyIcon = new NotifyIcon(components);
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            reconnectToolStripMenuItem = new ToolStripMenuItem();
            configToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            label1 = new Label();
            label2 = new Label();
            labelProgramFolder = new Label();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // consoleBox
            // 
            consoleBox.Anchor = (((AnchorStyles.Top | AnchorStyles.Bottom)
            | AnchorStyles.Left)
            | AnchorStyles.Right);
            consoleBox.Location = new Point(12, 216);
            consoleBox.Multiline = true;
            consoleBox.Name = "consoleBox";
            consoleBox.ReadOnly = true;
            consoleBox.ScrollBars = ScrollBars.Vertical;
            consoleBox.Size = new Size(669, 270);
            consoleBox.TabIndex = 1;
            consoleBox.TabStop = false;
            // 
            // notifyIcon
            // 
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.BalloonTipText = "Skypelogger minimized";
            notifyIcon.BalloonTipTitle = "Skypelogger";
            notifyIcon.Icon = Icon;
            notifyIcon.Text = "Skypelogger";
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(24, 24);
            menuStrip1.Items.AddRange(new ToolStripItem[] {
            fileToolStripMenuItem,
            aboutToolStripMenuItem});
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(693, 24);
            menuStrip1.TabIndex = 2;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
            reconnectToolStripMenuItem,
            configToolStripMenuItem,
            exitToolStripMenuItem});
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // reconnectToolStripMenuItem
            // 
            reconnectToolStripMenuItem.Name = "reconnectToolStripMenuItem";
            reconnectToolStripMenuItem.Size = new Size(152, 22);
            reconnectToolStripMenuItem.Text = "Reconnect";
            reconnectToolStripMenuItem.Click += new EventHandler(reconnectToolStripMenuItem_Click);
            // 
            // configToolStripMenuItem
            // 
            configToolStripMenuItem.Name = "configToolStripMenuItem";
            configToolStripMenuItem.Size = new Size(152, 22);
            configToolStripMenuItem.Text = "Hide";
            configToolStripMenuItem.Click += new EventHandler(configToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(152, 22);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += new EventHandler(exitToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(52, 20);
            aboutToolStripMenuItem.Text = "About";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 13F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.Location = new Point(12, 49);
            label1.Name = "label1";
            label1.Size = new Size(66, 22);
            label1.TabIndex = 3;
            label1.Text = "Folder:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft Sans Serif", 13F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label2.Location = new Point(12, 90);
            label2.Name = "label2";
            label2.Size = new Size(133, 22);
            label2.TabIndex = 4;
            label2.Text = "Program folder:";
            // 
            // labelProgramFolder
            // 
            labelProgramFolder.AutoSize = true;
            labelProgramFolder.Cursor = Cursors.Hand;
            labelProgramFolder.Font = new Font("Microsoft Sans Serif", 10F, (FontStyle.Italic | FontStyle.Underline), GraphicsUnit.Point, ((byte)(0)));
            labelProgramFolder.Location = new Point(151, 95);
            labelProgramFolder.Name = "labelProgramFolder";
            labelProgramFolder.Size = new Size(46, 17);
            labelProgramFolder.TabIndex = 5;
            labelProgramFolder.Text = "label3";
            // 
            // Program
            // 
            ClientSize = new Size(693, 532);
            Controls.Add(labelProgramFolder);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(consoleBox);
            Controls.Add(menuStrip1);
            Icon = ((Icon)(resources.GetObject("$Icon")));
            MainMenuStrip = menuStrip1;
            Name = "Program";
            Text = "Skypelogger";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

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
        
    }

    delegate void SetTextCallback(string text);
}
