using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LagoVista.AI.Rag.Models;     // ModelClassEntry
using LagoVista.AI.Rag.Services;   // DomainCatalogService
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    /// <summary>
    /// Tests for the private model extraction helper on DomainCatalogService.
    ///
    /// These tests cover the helper's presence/shape plus a real-world sanity
    /// test using AgentContextTestData and resources.resx to verify that
    /// EntityDescription resource values are resolved.
    /// </summary>
    [TestFixture]
    public class DomainCatalogServiceModelExtractionTests
    {
        private static MethodInfo GetHelperMethod()
        {
            var method = typeof(DomainCatalogService).GetMethod(
                "ExtractModelFromSnippet",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "Expected private static ExtractModelFromSnippet method to exist.");
            return method;
        }

        private static ModelClassEntry ExtractModel(
            string source,
            string relativePath,
            IReadOnlyDictionary<string, string> resources)
        {
            var method = GetHelperMethod();

            var result = method.Invoke(null, new object[] { source, relativePath, resources });
            return result as ModelClassEntry;
        }

        [Test]
        public void ExtractModelFromSnippet_MethodExists()
        {
            var method = GetHelperMethod();
            Assert.That(method.Name, Is.EqualTo("ExtractModelFromSnippet"));
        }

        [Test]
        public void ExtractModelFromSnippet_IsPrivateStatic()
        {
            var method = GetHelperMethod();
            Assert.That(method.IsPrivate, Is.True, "Helper should be private.");
            Assert.That(method.IsStatic, Is.True, "Helper should be static.");
        }

        [Test]
        public void ExtractModelFromSnippet_HasExpectedParameterCount()
        {
            var method = GetHelperMethod();
            var parameters = method.GetParameters();

            Assert.That(parameters.Length, Is.EqualTo(3), "Expected three parameters: source, relativePath, resources.");
        }

        [Test]
        public void ExtractModelFromSnippet_HasExpectedParameterTypes()
        {
            var method = GetHelperMethod();
            var parameters = method.GetParameters();

            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(string)), "First parameter should be source (string).");
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(string)), "Second parameter should be relativePath (string).");

            var dictType = typeof(System.Collections.Generic.IReadOnlyDictionary<string, string>);
            Assert.That(dictType.IsAssignableFrom(parameters[2].ParameterType), Is.True,
                "Third parameter should be an IReadOnlyDictionary<string,string> or compatible type.");
        }

        [Test]
        public void ExtractModelFromSnippet_ReturnType_IsModelClassEntry()
        {
            var method = GetHelperMethod();
            Assert.That(method.ReturnType, Is.EqualTo(typeof(ModelClassEntry)),
                "Helper should return ModelClassEntry.");
        }

        [Test]
        public void ExtractModelFromSnippet_RealWorld_AgentContextTestData_UsesResourcesForStrings()
        {
            // Arrange
            var modelPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Content", "AgentContextTest.txt");
            Assert.That(File.Exists(modelPath), Is.True, $"Expected real-world model file at '{modelPath}'.");

            var resxPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Content", "resources.resx");
            Assert.That(File.Exists(resxPath), Is.True, $"Expected resource file at '{resxPath}'.");

            var source = File.ReadAllText(modelPath);

            // Build resources dictionary directly from the .resx so we can
            // assert that resolved strings come from this library.
            var resources = LoadResources(resxPath);
            Assert.That(resources.Count, Is.GreaterThan(0), "Expected resources.resx to contain entries.");

            const string relativePath = "Content/AgentContextTest.text";

            // Act
            var model = ExtractModel(source, relativePath, resources);

            // Assert
            Assert.That(model, Is.Not.Null, "Expected AgentContextTestData to be recognized as a catalog-worthy model.");

            Assert.That(model.ClassName, Is.EqualTo("AgentContextTestData"));
            Assert.That(model.QualifiedClassName, Does.Contain("AgentContextTestData"));
            Assert.That(model.RelativePath, Is.EqualTo(relativePath));

            // Verify that title/description/help are not raw resource keys and
            // that they correspond to values present in resources.resx.
            var valueSet = resources.Values.ToHashSet(StringComparer.Ordinal);

            Assert.That(string.IsNullOrWhiteSpace(model.Title), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(model.Description), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(model.HelpText), Is.False);

            Assert.That(model.Title, Is.EqualTo("Agent Context"));
            Assert.That(model.Description, Is.EqualTo("An agent context is how a generative AI should be implemented to answer questions and create context.  It consists of a vector database, a content database, LLM provide and model as well as basic information about how prompts should be created such as user, role and system contexts."));
            Assert.That(model.HelpText, Is.EqualTo("An agent context is how a generative AI should be implemented to answer questions and create context.  It consists of a vector database, a content database, LLM provide and model as well as basic information about how prompts should be created such as user, role and system contexts."));
        }

        private static Dictionary<string, string> LoadResources(string resxPath)
        {
            var doc = XDocument.Load(resxPath);
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var data in doc.Root.Elements("data"))
            {
                var nameAttr = data.Attribute("name");
                var valueElem = data.Element("value");

                if (nameAttr == null || valueElem == null)
                    continue;

                var name = nameAttr.Value;
                var value = valueElem.Value;

                if (!string.IsNullOrWhiteSpace(name) && value != null)
                {
                    map[name] = value;
                }
            }

            return map;
        }
    }
}
