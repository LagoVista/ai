using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Utils.Types;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class RoslynChunkerSmokeTests
    {
        private const string ModelPath = "./Content/AgentContextTest.txt";
        private const string ManagerPath = "./Content/AgentContextTestManager.txt";
        private const string RepoPath = "./Content/AgentContextTestRepository.txt";
        private const string ControllerPath = "./Content/AgentContextTestController.txt";
        private const string ResourcePath = "./Content/resources.resx";
        private const string interfacePath = "./Content/IAgentContextManager.txt";



        private static void EnsureFixturesExist()
        {
            Assert.That(File.Exists(ModelPath), Is.True, $"Model content file not found at {ModelPath}");
            Assert.That(File.Exists(ResourcePath), Is.True, $"Resource content file not found at {ResourcePath}");
            Assert.That(File.Exists(ManagerPath), Is.True, $"Manager content file not found at {ManagerPath}");
            Assert.That(File.Exists(ControllerPath), Is.True, $"Controller content file not found at {ControllerPath}");
            Assert.That(File.Exists(RepoPath), Is.True, $"Repository content file not found at {RepoPath}");
        }

        private void WriteChunks(IReadOnlyList<CSharpComponentChunk> chunks, string label)
        {
            var file = System.IO.File.CreateText(@$"x:\{label}.txt");

            file.WriteLine(new string('=', 80));
            file.WriteLine($"SUMMARY SECTIONS: {label}");
            file.WriteLine(new string('=', 80));

            foreach (var chunk in chunks)
            {
                file.WriteLine("-- START Section Summary Fields:");
                file.WriteLine($"   SectionKey: {chunk.SectionKey}");
                file.WriteLine($"   Estimated Tokens: {chunk.EstimatedTokens}");
                file.WriteLine($"   Part: {chunk.PartIndex} of {chunk.PartTotal}");
                file.WriteLine($"   Symbol: {chunk.SymbolName}  (SymbolType: {chunk.SymbolKind})");
                file.WriteLine($"   Line Start: {chunk.LineStart}, Line End: {chunk.LineEnd}");
                file.WriteLine($"   Char Start: {chunk.StartCharacter}, Line End: {chunk.EndCharacter}");
                file.WriteLine($"   Text Length: {chunk.Text?.Length ?? 0}");
                file.WriteLine("-- END Section Summary Fields:");
                file.WriteLine(chunk.Text);

            }
        }
        [Test]
        public void Model_CSharpChunk_Smoke()
        {
            EnsureFixturesExist();

            var modelSource = File.ReadAllText(ModelPath);

            var plan = RoslynCSharpChunker.Chunk(modelSource, "AgentContextTest.cs");
            WriteChunks(plan.Result, "AgentContextTest.Raw");
        }


        [Test]
        public void Interface_CSharpChunk_Smoke()
        {
            EnsureFixturesExist();

            var interfaceSource = File.ReadAllText(interfacePath);

            var plan = RoslynCSharpChunker.Chunk(interfaceSource, "IAgentContextManager.cs");
            WriteChunks(plan.Result, "IAgentContextManager.Raw");
        }

        [Test]
        public void Controller_CSharpChunk_Smoke()
        {
            EnsureFixturesExist();

            var controllerSource = File.ReadAllText(ControllerPath);

            var plan = RoslynCSharpChunker.Chunk(controllerSource, "AgentContextTestController.cs");
            WriteChunks(plan.Result, "AgentContextTestController.Raw");
        }

        [Test]
        public void Manager_CSharpChunk_Smoke()
        {
            EnsureFixturesExist();

            var managerSource = File.ReadAllText(ManagerPath);

            var plan = RoslynCSharpChunker.Chunk(managerSource, "AgentContextTestManager.cs");
            WriteChunks(plan.Result, "AgentContextTestManager.Raw");
        }


        [Test]
        public void Repository_CSharpChunk_Smoke()
        {
            EnsureFixturesExist();

            var repoSource = File.ReadAllText(RepoPath);

            var plan = RoslynCSharpChunker.Chunk(repoSource, "AgentContextTestRepository.cs");
            WriteChunks(plan.Result, "AgentContextTestRepository.Raw");
        }


    }
}
