using System;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Chunkers
{
    [TestFixture]
    public class SymbolSplitterTests
    {
        [Test]
        public void Split_Single_Class_ReturnsOneSymbol()
        {
            var source = @"
using System;
namespace TestSpace
{
    public class Customer
    {
        public string Name { get; set; }
    }
}";

            var results = SymbolSplitter.Split(source);

            Assert.That(results.Result.Count, Is.EqualTo(1));
            Assert.That(results.Result[0].SymbolName, Is.EqualTo("Customer"));
            Assert.That(results.Result[0].Text, Does.Contain("class Customer"));
            Assert.That(results.Result[0].Text, Does.Contain("namespace TestSpace"));
        }

        [Test]
        public void Split_Multiple_Types_ReturnsMultipleSymbols()
        {
            var source = @"
namespace TestSpace
{
    public class A { }
    public interface IB { }
    public enum C { One, Two }
}";

            var results = SymbolSplitter.Split(source);

            Assert.That(results.Result.Count, Is.EqualTo(3));
            Assert.That(results.Result.Any(r => r.SymbolName == "A"));
            Assert.That(results.Result.Any(r => r.SymbolName == "IB"));
            Assert.That(results.Result.Any(r => r.SymbolName == "C"));
        }

        [Test]
        public void Split_Preserves_Using_Statements()
        {
            var source = @"
using System;
using System.Collections.Generic;

namespace TestSpace
{
    public class Device { }
}";

            var result = SymbolSplitter.Split(source).Result.First();

            Assert.That(result.Text, Does.Contain("using System;"));
            Assert.That(result.Text, Does.Contain("using System.Collections.Generic;"));
            Assert.That(result.Text, Does.Contain("namespace TestSpace"));
        }

        private const string ModelPath = "./Content/MultipleModel.txt";
        private const string NichelPath = "./Content/IndustryNiche.txt";
        private const string YearEndPath = "./Content/YearEndTaxes.txt";

        [Test]
        public void ShouldSplitAgentContext()
        {
            var content = System.IO.File.ReadAllText(ModelPath);
            var result = SymbolSplitter.Split(content);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Count, Is.EqualTo(3));
        }


        [Test]
        public void ShouldSplitIndustryNicheInTwo()
        {
            var content = System.IO.File.ReadAllText(NichelPath);
            var result = SymbolSplitter.Split(content);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Count, Is.EqualTo(3));

            foreach(var split in result.Result)
            {
                Console.WriteLine($"{split.SymbolKind} - {split.SymbolName}");
            }
        }

        [Test]
        public void Should_Split_And_Extract_YearEndPath()
        {
            var content = System.IO.File.ReadAllText(YearEndPath);
            var result = SymbolSplitter.Split(content);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result.Count, Is.EqualTo(4));

            foreach (var split in result.Result)
            {
                Console.WriteLine($"{split.SymbolKind} - {split.SymbolName}");
                Console.WriteLine(split.Text);
            }
        }


        [Test]
        public void Split_No_Classes_Returns_Default()
        {
            var source = "// no types here";

            var results = SymbolSplitter.Split(source);

            Assert.That(results.Successful, Is.False);

            Assert.That(results.ErrorMessage, Does.Contain("Did not identify any symbols within source code text"));
        }
    }
}
