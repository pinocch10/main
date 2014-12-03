
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MovieSchedule.Core;
using MovieSchedule.Data;

namespace MovieSchedule.Tests.Data
{
    [TestClass]
    public class ParseRunTest
    {
        [TestMethod]
        public void GetParseRunTest()
        {
            var pr = ParseRunProvider.Instance.GetParseRun();
            Assert.IsNotNull(pr);
            Assert.AreNotEqual(0, pr.Id);
            Assert.AreNotEqual(DateTime.MinValue, pr.Started);
            Assert.IsFalse(pr.Completed.HasValue);
            pr = ParseRunProvider.Instance.CloseParseRun();
            Assert.IsTrue(pr.Completed.HasValue);
            Assert.AreNotEqual(DateTime.MinValue, pr.Completed.Value);
        }

        [TestMethod]
        public void IncrementParseRunInfoTest()
        {
            ParseRunProvider.Instance.GetParseRun();
            ParseRunProvider.Instance.IncrementNewShowtimesCount(1);
            ParseRunProvider.Instance.IncrementNewShowtimesCount(2);
            ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(1);
            ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(2);
            ParseRunProvider.Instance.CloseParseRun();
        }

        [TestMethod]
        public void IncrementParseRunInfoTestWithoutInit()
        {
            ParseRunProvider.Instance.IncrementNewShowtimesCount(1);
            ParseRunProvider.Instance.IncrementNewShowtimesCount(1);
            ParseRunProvider.Instance.IncrementNewShowtimesCount(2);
            ParseRunProvider.Instance.IncrementNewShowtimesCount(2);
            ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(1);
            ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(1);
            ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(2);
            ParseRunProvider.Instance.IncrementUpdatedShowtimesCount(2);
            ParseRunProvider.Instance.CloseParseRun();
        }
    }
}
