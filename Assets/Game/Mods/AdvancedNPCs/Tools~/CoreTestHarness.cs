using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

public static class CoreTestHarness
{
    public static int Main(string[] args)
    {
        NUnitTestAssemblyRunner runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object>());
        ITestResult result = runner.Run(TestListener.NULL, TestFilter.Empty);
        if (args.Length > 0)
            using (XmlWriter writer = XmlWriter.Create(args[0], new XmlWriterSettings { Indent = true }))
                result.ToXml(true).WriteTo(writer);
        PrintFailures(result);
        Console.WriteLine("Core NUnit: " + result.PassCount + " passed, " + result.FailCount + " failed, " + result.SkipCount + " skipped");
        return result.FailCount == 0 && result.PassCount > 0 ? 0 : 1;
    }
    static void PrintFailures(ITestResult result)
    {
        if (!result.HasChildren && result.ResultState.Status == TestStatus.Failed)
            Console.WriteLine("FAIL " + result.FullName + "\n" + result.Message + "\n" + result.StackTrace);
        foreach (ITestResult child in result.Children) PrintFailures(child);
    }
}
