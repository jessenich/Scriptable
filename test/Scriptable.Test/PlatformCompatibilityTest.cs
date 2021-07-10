using System;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Scriptable.Test
{
    using static UnitTestHelpers;

    public class PlatformCompatibilityTest
    {
        [Test]
        public void TestReadAfterExit() => RunTest(() => PlatformCompatibilityTests.TestReadAfterExit());

        [Test]
        public void TestWriteAfterExit() => RunTest(() => PlatformCompatibilityTests.TestWriteAfterExit());

        [Test]
        public void TestFlushAfterExit() => RunTest(() => PlatformCompatibilityTests.TestFlushAfterExit());

        [Test]
        public void TestExitWithMinusOne() => RunTest(() => PlatformCompatibilityTests.TestExitWithMinusOne());

        [Test]
        public void TestExitWithOne() => RunTest(() => PlatformCompatibilityTests.TestExitWithOne());

        [Test]
        public void TestBadProcessFile() => RunTest(() => PlatformCompatibilityTests.TestBadProcessFile());

        [Test]
        public void TestAttaching() => RunTest(() => PlatformCompatibilityTests.TestAttaching());

        [Test]
        public void TestWriteToStandardInput() => RunTest(() => PlatformCompatibilityTests.TestWriteToStandardInput());

        [Test]
        public void TestArgumentsRoundTrip() => RunTest(() => PlatformCompatibilityTests.TestArgumentsRoundTrip());

        [Test]
        public void TestKill() => RunTest(() => PlatformCompatibilityTests.TestKill());

        private static void RunTest(Expression<Action> testMethod)
        {
            var compiled = testMethod.Compile();
            Assert.DoesNotThrow(() => compiled(), "should run on current platform");
        }
    }
}
