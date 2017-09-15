/////////////////////////////////////////////////////////////////////
// Repository.cs - holds test code for TestHarness                 //
//                                                                 //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * Almost no functionality now.  Will be expanded to accept
 * Queries for Logs and Libraries.
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

namespace TestHarness
{
    public class Repository : IRepository,IStreamService
    {

        string filename;
        string savePath = "..\\..\\..\\Repository\\RepositoryStorage\\";
        string ToSendPath = "..\\..\\..\\Repository\\RepositoryStorage\\";
        int BlockSize = 1024;
        byte[] block;
        HiResTimer hrt = null;


        string repoStoragePath = "..\\..\\..\\Repository\\RepositoryStorage\\";

        public Repository()
        {
            Console.Write("\n  Creating instance of Repository");
            block = new byte[BlockSize];
            hrt = new HiResTimer();
        }
        //----< search for text in log files >---------------------------
        /*
         * This function should return a message.  I'll do that when I
         * get a chance.
         */
        public List<string> queryLogs(string queryText)
        {
            List<string> queryResults = new List<string>();
            string path = System.IO.Path.GetFullPath(repoStoragePath);
            string[] files = System.IO.Directory.GetFiles(repoStoragePath, "*.txt");
            foreach (string file in files)
            {
                string contents = File.ReadAllText(file);
                if (contents.Contains(queryText))
                {
                    string name = System.IO.Path.GetFileName(file);
                    queryResults.Add(name);
                }
            }
            queryResults.Sort();
            queryResults.Reverse();
            return queryResults;
        }
        //----< send files with names on fileList >----------------------
        /*
         * This function is not currently being used.  It may, with a
         * Message interface, become part of Project #4.
         */
        public bool getFiles(string path, string fileList)
        {
            string[] files = fileList.Split(new char[] { ',' });
            //string repoStoragePath = "..\\..\\RepositoryStorage\\";

            foreach (string file in files)
            {
                string fqSrcFile = repoStoragePath + file;
                string fqDstFile = "";
                try
                {
                    fqDstFile = path + "\\" + file;
                    File.Copy(fqSrcFile, fqDstFile);
                }
                catch
                {
                    Console.Write("\n  could not copy \"" + fqSrcFile + "\" to \"" + fqDstFile);
                    return false;
                }
            }
            return true;
        }
        //----< intended for Project #4 >--------------------------------

        public void sendLog(string Log)
        {

        }
        public void upLoadFile(FileTransferMessage msg)
        {
            int totalBytes = 0;
            hrt.Start();
            filename = msg.filename;
            string rfilename = Path.Combine(savePath, filename);
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            using (var outputStream = new FileStream(rfilename, FileMode.Create))
            {
                while (true)
                {
                    int bytesRead = msg.transferStream.Read(block, 0, BlockSize);
                    totalBytes += bytesRead;
                    if (bytesRead > 0)
                        outputStream.Write(block, 0, bytesRead);
                    else
                        break;
                }
            }
            hrt.Stop();
            Console.Write(
              "\n  Received file \"{0}\" of {1} bytes in {2} microsec.",
              filename, totalBytes, hrt.ElapsedMicroseconds
            );
        }

        public Stream downLoadFile(string filename)
        {
            hrt.Start();
            string sfilename = Path.Combine(ToSendPath, filename);
            FileStream outStream = null;
            if (File.Exists(sfilename))
            {
                outStream = new FileStream(sfilename, FileMode.Open);
            }
            else
                throw new Exception("open failed for \"" + filename + "\"");
            hrt.Stop();
            Console.Write("\n  Sent \"{0}\" in {1} microsec.", filename, hrt.ElapsedMicroseconds);
            return outStream;
        }

        static ServiceHost CreateServiceChannel(string url)
        {
            // Can't configure SecurityMode other than none with streaming.
            // This is the default for BasicHttpBinding.
            //   BasicHttpSecurityMode securityMode = BasicHttpSecurityMode.None;
            //   BasicHttpBinding binding = new BasicHttpBinding(securityMode);

            BasicHttpBinding binding = new BasicHttpBinding();
            binding.TransferMode = TransferMode.Streamed;
            binding.MaxReceivedMessageSize = 50000000;
            Uri baseAddress = new Uri(url);
            Type service = typeof(TestHarness.Repository);
            ServiceHost host = new ServiceHost(service, baseAddress);
            host.AddServiceEndpoint(typeof(IStreamService), binding, baseAddress);
            return host;
        }

#if (TEST_REPOSITORY)
        static void Main(string[] args)
        {
            /*
             * ToDo: add code to test 
             * - Test code in Repository class that sends files to TestHarness.
             * - Modify TestHarness code that now copies files from RepositoryStorage folder
             *   to call Repository.getFiles.
             * - Add code to respond to client queries on files and logs.
             * - Add RepositoryTest class that implements ITest so Repo
             *   functionality can be tested in TestHarness.
             */
            ServiceHost host = CreateServiceChannel("http://localhost:8000/StreamService");
            ServiceHost host2 = CreateServiceChannel("http://laocalhost:8080/StreamService");
            host2.Open();
            host.Open();


            Console.Write("\n  This is Repository");
            Console.Write("\n ========================================\n");
            
            Console.Write("\n  Press key to terminate service:\n");
            Console.ReadKey();
            Console.Write("\n");
            host.Close();
        }
#endif
    }
}
