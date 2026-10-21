using System;
using Launcher.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ntools.Tests
{
    [TestClass]
    public class BoxBuilderTests
    {
        [TestMethod]
        public void Build_FormatsHeadersAndRowsIntoAlignedTable()
        {
            var box = new BoxBuilder("Metric", "Count");
            box.AddRow("Copied", "12");
            box.AddRow("Failed", "0");

            var result = box.Build();

            Assert.AreEqual(
                "┌──────┬─────┐" + Environment.NewLine +
                "│Metric│Count│" + Environment.NewLine +
                "├──────┼─────┤" + Environment.NewLine +
                "│Copied│12   │" + Environment.NewLine +
                "│Failed│0    │" + Environment.NewLine +
                "└──────┴─────┘",
                result);
        }
    }
}
