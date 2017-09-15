/////////////////////////////////////////////////////////////////////
// TestHarness.cs - TestHarness Engine: creates child domains      //
// ver 2.0                                                         //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * TestHarness package provides integration testing services.  It:
 * - receives structured test requests
 * - retrieves cited files from a repository
 * - executes tests on all code that implements an ITest interface,
 *   e.g., test drivers.
 * - reports pass or fail status for each test in a test request
 * - stores test logs in the repository
 * It contains classes:
 * - TestHarness that runs all tests in child AppDomains
 * - Callback to support sending messages from a child AppDomain to
 *   the TestHarness primary AppDomain.
 * - Test and RequestInfo to support transferring test information
 *   from TestHarness to child AppDomain
 * 
 * Required Files:
 * ---------------
 * - TestHarness.cs, BlockingQueue.cs
 * - ITest.cs
 * - LoadAndTest, Logger, Messages
 *
 * Maintanence History:
 * --------------------
 * ver 2.0 : 13 Nov 2016
 * - added creation of threads to run tests in ProcessMessages
 * - removed logger statements as they were confusing with multiple threads
 * - added locking in a few places
 * - added more error handling
 * - No longer save temp directory name in member data of TestHarness class.
 *   It's now captured in TestResults data structure.
 * ver 1.1 : 11 Nov 2016
 * - added ability for test harness to pass a load path to
 *   LoadAndTest instance in child AppDomain
 * ver 1.0 : 16 Oct 2016
 * - first release
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Security.Policy;    // defines evidence needed for AppDomain construction
using System.Runtime.Remoting;   // provides remote communication between AppDomains
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.IO;

namespace TestHarness
{
    ///////////////////////////////////////////////////////////////////
    // Callback class is used to receive messages from child AppDomain
    //
     [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class Callback : MarshalByRefObject, ICallback
    {
        public void sendMessage(Message message)
        {
            Console.Write("\n  received msg from childDomain: \"" + message.body + "\"");
        }
    }
    ///////////////////////////////////////////////////////////////////
    // Test and RequestInfo are used to pass test request information
    // to child AppDomain
    //
    [Serializable]
    class Test : ITestInfo
    {
        public string testName { get; set; }
        public List<string> files { get; set; } = new List<string>();
    }
    [Serializable]
    class RequestInfo : IRequestInfo
    {
        public string tempDirName { get; set; }
        public List<ITestInfo> requestInfo { get; set; } = new List<ITestInfo>();
    }
    ///////////////////////////////////////////////////////////////////
    // class TestHarness

    public class TestHarness : ITestHarness
    {
        public SWTools.BlockingQueue<Message> inQ_ { get; set; } = new SWTools.BlockingQueue<Message>();
        private ICallback cb_;
        private IRepository repo_;
        private IClient client_;
        private string repoPath_ = "..\\..\\..\\Repository\\RepositoryStorage";
        private string filePath_;
        object sync_ = new object();
        List<Thread> threads_ = new List<Thread>();
        Sender snd;
        String localport;
     



        IStreamService channel;
        string ToSendPath = "..\\..\\..\\TestHarness\\toSend";
        string SavePath = "..\\..\\..\\TestHarness\\saveFiles";
        int BlockSize = 1024;
        byte[] block;
        HiResTimer hrt = null;


        public TestHarness(IRepository repo)
        {
            Console.Write("\n  creating instance of TestHarness");
            repo_ = repo;
            repoPath_ = System.IO.Path.GetFullPath(repoPath_);
            cb_ = new Callback();
            block = new byte[BlockSize];
            hrt = new HiResTimer();
            
            

        }
        //----< called by TestExecutive >--------------------------------

        public void setClient(IClient client)
        {
            client_ = client;
        }
        //----< called by clients >--------------------------------------

        public void sendTestRequest(Message testRequest)
        {
            Console.Write("\n  TestHarness received a testRequest - Req #2");
            inQ_.enQ(testRequest);
        }
        //----< not used for Project #2 >--------------------------------

        public void getport(string port)
        {
            localport = port;
        }
        public Message sendMessage(Message msg)
        {
            return msg;
        }
        //----< make path name from author and time >--------------------

        string makeKey(string author)
        {
            DateTime now = DateTime.Now;
            string nowDateStr = now.Date.ToString("d");
            string[] dateParts = nowDateStr.Split('/');
            string key = "";
            foreach (string part in dateParts)
                key += part.Trim() + '_';
            string nowTimeStr = now.TimeOfDay.ToString();
            string[] timeParts = nowTimeStr.Split(':');
            for (int i = 0; i < timeParts.Count() - 1; ++i)
                key += timeParts[i].Trim() + '_';
            key += timeParts[timeParts.Count() - 1];
            key = author + "_" + key + "_" + "ThreadID" + Thread.CurrentThread.ManagedThreadId;
            return key;
        }
        //----< retrieve test information from testRequest >-------------

        List<ITestInfo> extractTests(Message testRequest)
        {
          
            Console.Write("\n  parsing test request");
            List<ITestInfo> tests = new List<ITestInfo>();
            XDocument doc = XDocument.Parse(testRequest.body);
            DateTime now = DateTime.Now;
            string s = makeKey(testRequest.author)+".xml";
            doc.Save(s);
            string path1 = Directory.GetCurrentDirectory();
            string src1 = System.IO.Path.Combine(path1, s);
            if (System.IO.File.Exists(src1))
            {
                string dst = System.IO.Path.Combine(repoPath_, s);
                System.IO.File.Copy(src1, dst, true);
                uploadFile(s);
            }
            foreach (XElement testElem in doc.Descendants("test"))
            {
                Test test = new Test();
                string testDriverName = testElem.Element("testDriver").Value;
                test.testName = testElem.Attribute("name").Value;
                test.files.Add(testDriverName);
                foreach (XElement lib in testElem.Elements("library"))
                {
                    test.files.Add(lib.Value);
                }
                tests.Add(test);
            }
            return tests;
        }
        //----< retrieve test code from testRequest >--------------------

        List<string> extractCode(List<ITestInfo> testInfos)
        {
            Console.Write("\n  retrieving code files from testInfo data structure");
            List<string> codes = new List<string>();
            foreach (ITestInfo testInfo in testInfos)
                codes.AddRange(testInfo.files);
            return codes;
        }
        //----< create local directory and load from Repository >--------

          RequestInfo processRequestAndLoadFiles(Message testRequest)
        {
            localport = testRequest.port;
            channel = CreateServiceChannel("http://localhost:8080/StreamService");
            //string localDir_ = "";
            RequestInfo rqi = new RequestInfo();
            rqi.requestInfo = extractTests(testRequest);
            List<string> files = extractCode(rqi.requestInfo);
            
            SavePath = makeKey(testRequest.author);            // name of temporary dir to hold test files
            rqi.tempDirName = SavePath;
            filePath_ = System.IO.Path.GetFullPath(SavePath);  // LoadAndTest will use this path
            Console.Write("\n  creating local test directory \"" + SavePath + "\"");
            System.IO.Directory.CreateDirectory(SavePath);

            Console.Write("\n  loading code from Repository");
            foreach (string file in files)
            {
                string name = System.IO.Path.GetFileName(file);
                string src = System.IO.Path.Combine(repoPath_, file);
                if (System.IO.File.Exists(src))
                {
                    string dst = System.IO.Path.Combine(SavePath, name);
                    try
                    {
                        download(name);
                    }
                    catch
                    {
                        /* do nothing because file was already copied and is being used */
                    }
                    Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": retrieved file \"" + name + "\"");
                }
                else
                {
                    Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": could not retrieve file \"" + name + "\"");
                }
            }
            Console.WriteLine();
            return rqi;
        }
        //----< save results and logs in Repository >--------------------

        bool saveResultsAndLogs(ITestResults testResults)
        {
            string logName = testResults.testKey + ".txt";
            System.IO.StreamWriter sr = null;
            try
            {
                sr = new System.IO.StreamWriter(System.IO.Path.Combine(repoPath_, logName));
                sr.WriteLine(logName);
                foreach (ITestResult test in testResults.testResults)
                {
                    sr.WriteLine("-----------------------------");
                    sr.WriteLine(test.testName);
                    sr.WriteLine(test.testResult);
                    sr.WriteLine(test.testLog);
                }
                sr.WriteLine("-----------------------------");
            }
            catch
            {
                sr.Close();
                return false;
            }
            sr.Close();
            return true;
        }
        //----< run tests >----------------------------------------------
        /*
         * In Project #4 this function becomes the thread proc for
         * each child AppDomain thread.
         */
        ITestResults runTests(Message testRequest)
        {
            AppDomain ad = null;
            ILoadAndTest ldandtst = null;
            RequestInfo rqi = null;
            ITestResults tr = null;

            try
            {
                lock (sync_)
                {
                    rqi = processRequestAndLoadFiles(testRequest);
                    ad = createChildAppDomain();
                    ldandtst = installLoader(ad);
                }
                if (ldandtst != null)
                {
                    tr = ldandtst.test(rqi);
                }
                // unloading ChildDomain, and so unloading the library

                saveResultsAndLogs(tr);

                lock (sync_)
                {
                    Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": unloading: \"" + ad.FriendlyName + "\"\n");
                    AppDomain.Unload(ad);
                    try
                    {
                        System.IO.Directory.Delete(rqi.tempDirName, true);
                        Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": removed directory " + rqi.tempDirName);
                    }
                    catch (Exception ex)
                    {
                        Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": could not remove directory " + rqi.tempDirName);
                        Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": " + ex.Message);
                    }
                }
                return tr;
            }
            catch (Exception ex)
            {
                Console.Write("\n\n---- {0}\n\n", ex.Message);
                return tr;
            }
        }
        

        //----< make TestResults Message >-------------------------------

        Message makeTestResultsMessage(ITestResults tr)
        {
            Message trMsg = new Message();
            trMsg.port = localport; 
            trMsg.author = "TestHarness";
            trMsg.to = "CL";
            trMsg.from = "TH";
            XDocument doc = new XDocument();
            XElement root = new XElement("testResultsMsg");
            doc.Add(root);
            XElement testKey = new XElement("testKey");
            testKey.Value = tr.testKey;
            root.Add(testKey);
            XElement timeStamp = new XElement("timeStamp");
            timeStamp.Value = tr.dateTime.ToString();
            root.Add(timeStamp);
            XElement testResults = new XElement("testResults");
            root.Add(testResults);
            foreach (ITestResult test in tr.testResults)
            {
                XElement testResult = new XElement("testResult");
                testResults.Add(testResult);
                XElement testName = new XElement("testName");
                testName.Value = test.testName;
                testResult.Add(testName);
                XElement result = new XElement("result");
                result.Value = test.testResult;
                testResult.Add(result);
                XElement log = new XElement("log");
                log.Value = test.testLog;
                testResult.Add(log);
            }
            trMsg.body = doc.ToString();

            string endpoint = "http://localhost:" + localport + "/ICommunicator";
            snd = new Sender(endpoint);
            snd.PostMessage(trMsg);
            return trMsg;

        }
        //----< wait for all threads to finish >-------------------------

        public void wait()
        {
            foreach (Thread t in threads_)
                t.Join();
        }
        //----< main activity of TestHarness >---------------------------

        public void processMessages()
        {
            AppDomain main = AppDomain.CurrentDomain;
            Console.Write("\n  Starting in AppDomain " + main.FriendlyName + "\n");

            ThreadStart doTests = () =>
            {
                Message testRequest = inQ_.deQ();
                if (testRequest.body == "quit")
                {
                    inQ_.enQ(testRequest);
                    return;
                }
                ITestResults testResults = runTests(testRequest);
                lock (sync_)
                {
                    client_.sendResults(makeTestResultsMessage(testResults));
                }
            };

            int numThreads = 8;
            for (int i = 0; i < numThreads; ++i)
            {
                Console.Write("\n  Creating AppDomain thread");
                Thread t = new Thread(doTests);
                threads_.Add(t);
                t.Start();
            }
        }
        //----< was used for debugging >---------------------------------

        void showAssemblies(AppDomain ad)
        {
            Assembly[] arrayOfAssems = ad.GetAssemblies();
            foreach (Assembly assem in arrayOfAssems)
                Console.Write("\n  " + assem.ToString());
        }
        //----< create child AppDomain >---------------------------------

        public AppDomain createChildAppDomain()
        {
            try
            {
                Console.Write("\n  creating child AppDomain - Req #5");

                AppDomainSetup domaininfo = new AppDomainSetup();
                domaininfo.ApplicationBase
                  = "..\\..\\..\\TestExecutive\\bin\\Debug";
                  //"file:///" + System.Environment.CurrentDirectory;  // defines search path for LoadAndTest library

                //Create evidence for the new AppDomain from evidence of current

                Evidence adevidence = AppDomain.CurrentDomain.Evidence;

                // Create Child AppDomain

                AppDomain ad
                  = AppDomain.CreateDomain("ChildDomain", adevidence, domaininfo);

                Console.Write("\n  created AppDomain \"" + ad.FriendlyName + "\"");
                return ad;
            }
            catch (Exception except)
            {
                Console.Write("\n  " + except.Message + "\n\n");
            }
            return null;
        }
        //----< Load and Test is responsible for testing >---------------

        ILoadAndTest installLoader(AppDomain ad)
        {
            ad.Load("LoadAndTest");
            //showAssemblies(ad);
            //Console.WriteLine();

            // create proxy for LoadAndTest object in child AppDomain

            ObjectHandle oh
              = ad.CreateInstance("LoadAndTest", "TestHarness.LoadAndTest");
            object ob = oh.Unwrap();    // unwrap creates proxy to ChildDomain
                                        // Console.Write("\n  {0}", ob);

            // set reference to LoadAndTest object in child

            ILoadAndTest landt = (ILoadAndTest)ob;

            // create Callback object in parent domain and pass reference
            // to LoadAndTest object in child

            landt.setCallback(cb_);
            landt.loadPath(filePath_);  // send file path to LoadAndTest
            return landt;
        }


        static IStreamService CreateServiceChannel(string url)
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

        void uploadFile(string filename)
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

        void download(string filename)
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

#if (TEST_TESTHARNESS)
        static void Main(string[] args)
        {

            Message a = new Message();
            a = Message.buildTestMessage();
            Repository r = new Repository();
            TestHarness t = new TestHarness(r);
            t.channel= CreateServiceChannel("http://localhost:8080/StreamService"); 
            t.processRequestAndLoadFiles(a);
            t.processMessages();
        }
#endif
    }
}
