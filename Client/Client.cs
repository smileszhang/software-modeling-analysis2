/////////////////////////////////////////////////////////////////////
// Client.cs - sends TestRequests, displays results                //
//                                                                 //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * Almost no functionality now.  Will be expanded to make 
 * Queries into Repository for Logs and Libraries.
 * 
 * Required Files:
 * - Client.cs, ITest.cs, Logger.cs
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 20 Oct 2016
 * - first release
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace TestHarness
{
    public class Client : IClient
    {
        public IStreamService channel;
        public string ToSendPath = "..\\..\\toSend";
        public string SavePath = "..\\..\\saveFiles";
        int BlockSize = 1024;
        byte[] block;
        HiResTimer hrt = null;

        public SWTools.BlockingQueue<string> inQ_ { get; set; }
        private ITestHarness th_ = null;
        private IRepository repo_ = null;

        public Client(ITestHarness th)
        {
            Console.Write("\n  Creating instance of Client");
            th_ = th;
        }
        public void setRepository(IRepository repo)
        {
            repo_ = repo;
        }

        public void sendTestRequest(Message testRequest)
        {
            th_.sendTestRequest(testRequest);
        }
        public void sendResults(Message results)
        {
            //RLog.write("\n  Client received results message:");
            //RLog.write("\n  " + results.ToString());
            //RLog.putLine();
            Console.Write("\n  Client received results message:");
            Console.Write("\n  " + results.ToString());
            Console.WriteLine();
        }
        public List<string> makeQuery(string queryText)
        {
            Console.Write("\n  Results of client query for \"" + queryText + "\"");
            if (repo_ == null)
                return null;
            List<string> files = repo_.queryLogs(queryText);
            Console.Write("\n  first 10 reponses to query \"" + queryText + "\"");
            for (int i = 0; i < 10; ++i)
            {
                if (i == files.Count())
                    break;
                Console.Write("\n  " + files[i]);
            }
            return files;

        }
        public Client()
        {
            block = new byte[BlockSize];
            hrt = new HiResTimer();
        }

        static public IStreamService CreateServiceChannel(string url)
        {
            BasicHttpSecurityMode securityMode = BasicHttpSecurityMode.None;

            BasicHttpBinding binding = new BasicHttpBinding(securityMode);
            binding.TransferMode = TransferMode.Streamed;
            binding.MaxReceivedMessageSize = 500000000;
            EndpointAddress address = new EndpointAddress(url);

            ChannelFactory<IStreamService> factory
              = new ChannelFactory<IStreamService>(binding, address);
            return factory.CreateChannel();
        }

        public void uploadFile(string filename)
        {
            string fqname = Path.Combine(ToSendPath, filename);
            try
            {
                hrt.Start();
                using (var inputStream = new FileStream(fqname, FileMode.Open))
                {
                    FileTransferMessage msg = new FileTransferMessage();
                    msg.filename = filename;
                    msg.transferStream = inputStream;
                    channel.upLoadFile(msg);
                }
                hrt.Stop();
                Console.Write("\n  Uploaded file \"{0}\" in {1} microsec.", filename, hrt.ElapsedMicroseconds);
            }
            catch
            {
                Console.Write("\n  can't find \"{0}\"", fqname);
            }
        }

        public void download(string filename)
        {
            int totalBytes = 0;
            try
            {
                hrt.Start();
                Stream strm = channel.downLoadFile(filename);
                string rfilename = Path.Combine(SavePath, filename);
                if (!Directory.Exists(SavePath))
                    Directory.CreateDirectory(SavePath);
                using (var outputStream = new FileStream(rfilename, FileMode.Create))
                {
                    while (true)
                    {
                        int bytesRead = strm.Read(block, 0, BlockSize);
                        totalBytes += bytesRead;
                        if (bytesRead > 0)
                            outputStream.Write(block, 0, bytesRead);
                        else
                            break;
                    }
                }
                hrt.Stop();
                ulong time = hrt.ElapsedMicroseconds;
                Console.Write("\n  Received file \"{0}\" of {1} bytes in {2} microsec.", filename, totalBytes, time);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
        }
#if (TEST_CLIENT)
        static void Main(string[] args)
        {
            /*
             * ToDo: add code to test 
             * - Add code in Client class to make queries into Repository for
             *   information about libraries and logs.
             * - Add code in Client class to sent files to Repository.
             * - Add ClientTest class that implements ITest so Client
             *   functionality can be tested in TestHarness.
             */
            Client clnt = new Client();
            clnt.channel = CreateServiceChannel("http://localhost:8000/StreamService");
            HiResTimer hrt = new HiResTimer();

          

            hrt.Start();
            clnt.uploadFile("AnotherTestDriver.dll");
            clnt.uploadFile("AnotherTestedCode.dll");
            clnt.uploadFile("ITest.dll");
            clnt.uploadFile("Logger.dll");
            clnt.uploadFile("TestDriver.dll");
            clnt.uploadFile("TestedCode.dll");
            hrt.Stop();
        }
#endif
    }
}
