using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Xml.Linq;

namespace WPFTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TestHarness.Client clnt;
        TestHarness.Repository rep;
        TestHarness.Sender sndr, sndr1;
        TestHarness.Receiver recvr, recvr1;
        Thread rcvThrd, rcvThrd1;
        String[] upfiles;
        delegate void NewMessage(string msg);
        event NewMessage OnNewMessage, OnNewMessage1;
        TestHarness.Message TestResuleMessage, TestResuleMessage1;
        string showMsg, showMsg1;
        string sendlocal, sendlocal1;

        public MainWindow()
        {
            InitializeComponent();
            OnNewMessage += new NewMessage(OnNewMessageHandler);
            OnNewMessage1 += new NewMessage(OnNewMessageHandler1);
        }
        void ThreadProc()
        {
            while (true)
            {
                // get message out of receive queue - will block if queue is empty
                TestResuleMessage = recvr.GetMessage();
                showMsg = TestResuleMessage.body.ToString();
                /*
                XDocument doc = XDocument.Parse(TestResuleMessage.body);
                List<string> results = new List<string>();
                
                foreach (XElement testElem in doc.Descendants("testResults"))
                {
                    results.Add(testElem.Element("testResult").Value);
                   // results.Add(testElem.Attribute("testName").Value);
                    results.Add(testElem.Element("result").Value);
                    results.Add(testElem.Attribute("log").Value);
                }
                showMsg = string.Join("\r", results);
                */
            }
        }

        void ThreadProc1()
        {
            while (true)
            {
                // get message out of receive queue - will block if queue is empty
                TestResuleMessage1 = recvr1.GetMessage();
                showMsg1 = TestResuleMessage1.body.ToString();

            }
        }

        void OnNewMessageHandler(string msg)
        {
            textBox.AppendText(showMsg);
        }

        void OnNewMessageHandler1(string msg)
        {
            textBox3.AppendText(showMsg1);
        }

        private void result1(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(
           System.Windows.Threading.DispatcherPriority.Normal,
           OnNewMessage,
           showMsg);
        }
        private void result2(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(
           System.Windows.Threading.DispatcherPriority.Normal,
           OnNewMessage1,
           showMsg1);

        }

        private void connectTH_Click(object sender, RoutedEventArgs e)
        {
            sendlocal = "4003";
            string endpoint = "http://localhost:4001/ICommunicator";
            sndr = new TestHarness.Sender(endpoint);

            string endpoint1 = "http://localhost:" + sendlocal + "/ICommunicator";
            recvr = new TestHarness.Receiver();
            recvr.CreateRecvChannel(endpoint1);

            // create receive thread which calls rcvBlockingQ.deQ() (see ThreadProc above)
            rcvThrd = new Thread(new ThreadStart(this.ThreadProc));
            rcvThrd.IsBackground = true;
            rcvThrd.Start();
            button.IsEnabled = false;

        }
        private void CTConnect2(object sender, RoutedEventArgs e)
        {
            sendlocal1 = "4004";
            string endpoint = "http://localhost:4002/ICommunicator";
            sndr1 = new TestHarness.Sender(endpoint);

            string endpoint1 = "http://localhost:" + sendlocal1 + "/ICommunicator";
            recvr1 = new TestHarness.Receiver();
            recvr1.CreateRecvChannel(endpoint1);

            // create receive thread which calls rcvBlockingQ.deQ() (see ThreadProc above)
            rcvThrd1 = new Thread(new ThreadStart(this.ThreadProc1));
            rcvThrd1.IsBackground = true;
            rcvThrd1.Start();
            button7.IsEnabled = false;
        }
       
        
        private void sendMsg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestHarness.Message msg = TestHarness.Message.buildTestMessage();
                msg.port = sendlocal;
                sndr.PostMessage(msg);
            }
            catch
            {
                Console.Write("please connect again!");
            }

        }
        private void send2(object sender, RoutedEventArgs e)
        {
            try
            {
                TestHarness.Message msg = TestHarness.Message.buildTestMessage();
                msg.port = sendlocal1;
                sndr1.PostMessage(msg);
            }
            catch
            {
                Console.Write("please connect again!");
            }
        }

        private void connectCR_Click(object sender, RoutedEventArgs e)
        {
            clnt = new TestHarness.Client();
            clnt.channel = TestHarness.Client.CreateServiceChannel("http://localhost:8000/StreamService");
            button3.IsEnabled=false;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {

            string[] files = OpenDialog();
            upfiles = files;
            foreach (string file in files)
            {
                string filename = System.IO.Path.GetFileName(file);

                listBox.Items.Insert(0, filename);
                // clnt.uploadFile(file); 
            }

        }

        private string[] OpenDialog()
        {
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Title = "Select Files";
            string[] files = new string[50];
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                files = ofd.SafeFileNames;

                string path = System.IO.Path.GetDirectoryName(ofd.FileName);
                clnt.ToSendPath = path;
                return files;
            }
            else
            {
                return files;
            }
        }

        

        private void upload_Click(object sender, RoutedEventArgs e)
        {
            foreach (string file in upfiles)
            {
                try
                {
                    clnt.uploadFile(file);
                }
                catch
                {
                    Console.Write("Upload failed.");
                }
            }
            listBox.Items.Clear();
        }

        

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            //TestHarness.Repository re = new TestHarness.Repository();
            listBox1.Items.Clear();
            rep = new TestHarness.Repository();
            clnt = new TestHarness.Client();
            clnt.setRepository(rep);
            string querytext = textBox1.Text;
            List<string> files = clnt.makeQuery(querytext);
            foreach (string file in files)
            {
                string filename = System.IO.Path.GetFileName(file);
                string path = System.IO.Path.GetFullPath(file);
                listBox1.Items.Insert(0, filename);
            }



        }

        private string ReadFileContent(string path)
        {
            return "..\\..\\..\\Repository\\RepositoryStorage\\"+path.ToString();
        }

        private void listBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            textBox2.Clear();
            
            for (int i = 0; i < listBox1.SelectedItems.Count; i++)
            {
                string strReadFilePath = ReadFileContent(listBox1.SelectedItems[i].ToString());
                StreamReader srReadFile = new StreamReader(strReadFilePath);
                List<string> s = new List<string>();
                while (!srReadFile.EndOfStream)
                {
                   
                    s.Add(srReadFile.ReadLine());
                   // string strReadLine = srReadFile.ReadLine();
                    
                }
                textBox2.Text = string.Join("\r", s);
                srReadFile.Close();
            }
        }

        
    }
}
