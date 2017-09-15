/////////////////////////////////////////////////////////////////////
// ITest.cs - interfaces for communication between system parts    //
//                                                                 //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestHarness
{
  /////////////////////////////////////////////////////////////
  // used by client to send TestRequests
  // used by child AppDomain to send messages to TestHarness

  public interface ITestHarness
  {
    void sendTestRequest(string testRequest);
    string sendMessage(string msg);
  }
  /////////////////////////////////////////////////////////////
  // used by child AppDomain to invoke test driver's test()

  public interface ITest
  {
    bool test();
  }
  /////////////////////////////////////////////////////////////
  // used by child AppDomain to communicate with Repository
  // via TestHarness Comm

  public interface IRepo
  {
    bool getFiles(string path, string fileList);  // fileList is comma separated list of files
    void sendLog(string log);
  }
  /////////////////////////////////////////////////////////////
  // used by child AppDomain to send results to client
  // via TestHarness Comm

  public interface IClient
  {
    void sendResults(string result);
  }
  /////////////////////////////////////////////////////////////
  // used by TestHarness to communicate with child AppDomain

  public interface ILoadAndTest
  {
    void test(string testRequest);
    void SetRepoCallback(IRepo repoGateway);
    void SetClientCallback(IClient clientGateway);
    void setTestHarnessCallBack(ITestHarness thGateway);
  }
}
