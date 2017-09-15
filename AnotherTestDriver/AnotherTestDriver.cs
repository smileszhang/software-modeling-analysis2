/////////////////////////////////////////////////////////////////////
// AnotherTestDriver.cs - defines testing process                  //
//                                                                 //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestHarness;

namespace TestHarness
{
  public class AnotherTestDriver : ITest
  {
    public bool test()
    {
      TestHarness.AnotherTested tested = new TestHarness.AnotherTested();
      return tested.myWackyFunction();
    }
    public string getLog()
    {
      return "demo test that always fails";
    }
#if (TEST_ANOTHERTESTDRIVER)
    static void Main(string[] args)
    {
    }
#endif
  }
}
