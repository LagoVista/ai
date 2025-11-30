using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using LagoVista.Core.Utils;
using LagoVista.IoT.Logging.Loggers;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    /// <summary>
    /// Smoke tests that exercise CodeDescriptionService, call BuildSections()
    /// on all supported description types, and dump the results to the console.
    ///
    /// These are intentionally coarse-grained and aimed at making it easy
    /// to visually inspect the normalized SummarySection output.
    /// </summary>
    [TestFixture]
    public class CodeDescriptionServiceSummarySectionSmokeTests
    {
        private const string ModelPath = "./Content/AgentContextTest.txt";
        private const string ManagerPath = "./Content/AgentContextTestManager.txt";
        private const string RepoPath = "./Content/AgentContextTestRepository.txt";
        private const string ControllerPath = "./Content/AgentContextTestController.txt";
        private const string ResourcePath = "./Content/resources.resx";
        private const string SummaryPath = "./Content/SummaryModel.txt";

        private CodeDescriptionService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new CodeDescriptionService();
        }

        private static void EnsureFixturesExist()
        {
            Assert.That(File.Exists(ModelPath), Is.True, $"Model content file not found at {ModelPath}");
            Assert.That(File.Exists(ResourcePath), Is.True, $"Resource content file not found at {ResourcePath}");
            Assert.That(File.Exists(ManagerPath), Is.True, $"Manager content file not found at {ManagerPath}");
            Assert.That(File.Exists(ControllerPath), Is.True, $"Controller content file not found at {ControllerPath}");
            Assert.That(File.Exists(RepoPath), Is.True, $"Repository content file not found at {RepoPath}");
            Assert.That(File.Exists(SummaryPath), Is.True, $"Repository content file not found at {SummaryPath}");
        }

        private static IReadOnlyDictionary<string, string> LoadResources()
        {
            // Uses ResxLabelScanner helper to load a single resource dictionary
            // from the current directory (expects resources.resx in ./Content).
            var scanner = new ResxLabelScanner(new Mock<IAdminLogger>().Object);
            return scanner.GetSingleResourceDictionary(".");
        }

        private static void DumpSections(string label, IEnumerable<SummarySection> sections)
        {
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"SUMMARY SECTIONS: {label}");
            Console.WriteLine(new string('=', 80));

            foreach (var section in sections)
            {
                Console.WriteLine($"-- SectionKey: {section.SectionKey}");
                Console.WriteLine($"   Symbol: {section.Symbol}  (SymbolType: {section.SymbolType})");
                Console.WriteLine();
                Console.WriteLine(section.SectionNormalizedText ?? string.Empty);
                Console.WriteLine();
                Console.WriteLine(new string('-', 80));
            }
        }

        private static void DumpSectionsToFile(string label, IEnumerable<SummarySection> sections)
        {
            var file = System.IO.File.CreateText(@$"x:\{label}.txt");

            file.WriteLine(new string('=', 80));
            file.WriteLine($"SUMMARY SECTIONS: {label}");
            file.WriteLine(new string('=', 80));

            foreach (var section in sections)
            {
                file.WriteLine("-- START Section Summary Fields:");
                file.WriteLine($"   SectionKey: {section.SectionKey}, SectionType: {section.SectionType}");
                file.WriteLine($"   Symbol: {section.Symbol}  (SymbolType: {section.SymbolType})");
                file.WriteLine($"   Flavor: {section.Flavor}");
                file.WriteLine($"   DomainKey: {section.DomainKey}  (ModelName: {section.ModelName}) (ModelClassName:{section.ModelClassName})");
                file.WriteLine("-- END Section Summary Fields:");
                file.WriteLine();
                file.WriteLine(section.SectionNormalizedText ?? string.Empty);
                file.WriteLine();
                file.WriteLine(new string('-', 80));
            }

            file.Close();
        }

        private IndexFileContext GetIndexFileContext()
        {
            return new IndexFileContext()
            {
                DocumentIdentity = new DocumentIdentity()
                {

                },
                GitRepoInfo = new GitRepoInfo()
                {

                }
            };
        }


        public DomainModelHeaderInformation headerInfo = new DomainModelHeaderInformation()
        {
            DomainName = "AI Services",
            DomainKey = "aidomain",
            DomainTagLine = "The AI services provide both a mechanism to interact with an LLM and create inferences on models.",
            ModelClassName = "AgentContextTest",
            ModelName = "Agent Context",
            ModelTagLine = "The Agent Context model provides context information about an AI agent's operational environment."
        };

        [Test]
        public void SummaryDescription_BuildSections_Smoke()
        {
            EnsureFixturesExist();

            var modelSource = File.ReadAllText(SummaryPath);
            var resources = LoadResources();

            var description = _service.BuildSummaryDescription(GetIndexFileContext(), modelSource, resources).Result;
            Assert.That(description, Is.Not.Null);

            var sections = description.BuildSections(headerInfo).ToList();
            Assert.That(sections.Count, Is.GreaterThan(0), "Expected at least one SummarySection for ModelStructureDescription.");

            DumpSections("SummaryListDescription", sections);
            DumpSectionsToFile("SummaryListDescription", sections);
        }


        [Test]
        public void ModelStructureDescription_BuildSections_Smoke()
        {
            EnsureFixturesExist();

            var modelSource = File.ReadAllText(ModelPath);
            var resources = LoadResources();

            var description = _service.BuildModelStructureDescription(GetIndexFileContext(),modelSource, resources).Result;
            Assert.That(description, Is.Not.Null);

            var sections = description.BuildSections(headerInfo).ToList();
            Assert.That(sections.Count, Is.GreaterThan(0), "Expected at least one SummarySection for ModelStructureDescription.");

            DumpSections("ModelStructureDescription", sections);
            DumpSectionsToFile("ModelStructureDescription", sections);
        }

        [Test]
        public void ModelMetadataDescription_BuildSections_Smoke()
        {
            EnsureFixturesExist();

            var modelSource = File.ReadAllText(ModelPath);
            var resources = LoadResources();

            var description = _service.BuildModelMetadataDescription(GetIndexFileContext(), modelSource, resources).Result;
            Assert.That(description, Is.Not.Null);

            var sections = description.BuildSections(headerInfo).ToList();
            Assert.That(sections.Count, Is.GreaterThan(0), "Expected at least one SummarySection for ModelMetadataDescription.");

            DumpSections("ModelMetadataDescription", sections);
            DumpSectionsToFile("ModelMetadataDescription", sections);
        }

        [Test]
        public void ManagerDescription_BuildSections_Smoke()
        {
            EnsureFixturesExist();

            var managerSource = File.ReadAllText(ManagerPath);

            var description = _service.BuildManagerDescription(GetIndexFileContext(), managerSource).Result;
            Assert.That(description, Is.Not.Null);

            var sections = description.BuildSections(headerInfo).ToList();
            Assert.That(sections.Count, Is.GreaterThan(0), "Expected at least one SummarySection for ManagerDescription.");

            DumpSections("ManagerDescription", sections);
            DumpSectionsToFile("ManagerDescription", sections);
        }

        [Test]
        public void RepositoryDescription_BuildSections_Smoke()
        {
            EnsureFixturesExist();

            var repoSource = File.ReadAllText(RepoPath);

            var description = _service.BuildRepositoryDescription(GetIndexFileContext(), repoSource).Result;
            Assert.That(description, Is.Not.Null);

            var sections = description.BuildSections(headerInfo).ToList();
            Assert.That(sections.Count, Is.GreaterThan(0), "Expected at least one SummarySection for RepositoryDescription.");

            DumpSections("RepositoryDescription", sections);
            DumpSectionsToFile("RepositoryDescription", sections);
        }

        [Test]
        public void EndpointDescription_BuildSections_Smoke()
        {
            EnsureFixturesExist();

            var controllerSource = File.ReadAllText(ControllerPath);

            var endpoints = _service.BuildEndpointDescriptions(GetIndexFileContext(), controllerSource).Result;
            Assert.That(endpoints, Is.Not.Null);
            Assert.That(endpoints.Count, Is.GreaterThan(0), "Expected at least one EndpointDescription.");

            var index = 0;
            foreach (var endpoint in endpoints)
            {
                var sections = endpoint.BuildSections(headerInfo).ToList();
                Assert.That(sections.Count, Is.GreaterThan(0), $"Expected at least one SummarySection for EndpointDescription #{index}.");
                DumpSections($"EndpointDescription[{index}]", sections);
                DumpSectionsToFile($"EndpointDescription[{index}]", sections);
                index++;
            }
        }

        [Test]
        public void InterfaceDescription_BuildSections_Smoke()
        {
            const string interfacePath = "./Content/IAgentContextManager.txt";
            Assert.That(File.Exists(interfacePath), Is.True, $"Interface content file not found at {interfacePath}");

            var interfaceSource = File.ReadAllText(interfacePath);

            var description = _service.BuildInterfaceDescription(GetIndexFileContext(), interfaceSource).Result;
            Assert.That(description, Is.Not.Null);

            var sections = description.BuildSections(headerInfo).ToList();
            Assert.That(sections.Count, Is.GreaterThan(0), "Expected at least one SummarySection for InterfaceDescription.");

            DumpSections("InterfaceDescription", sections);
            DumpSectionsToFile("InterfaceDescription", sections);
        }
    }
}
