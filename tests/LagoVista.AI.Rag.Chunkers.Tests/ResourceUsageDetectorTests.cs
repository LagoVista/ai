using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.AI.Rag.Chunkers.Tests
{

    [TestFixture]
    public class ResourceUsageDetectorTests
    {
        private const string ModelPath = "./Content/AgentContextTest.txt";
        private const string Model2Path_WithEnums = "./Content/Model_AIModel.txt";

        private static void EnsureFixturesExist()
        {
            Assert.That(File.Exists(ModelPath), Is.True, $"Model content file not found at {ModelPath}");
            Assert.That(File.Exists(Model2Path_WithEnums), Is.True, $"Model content file not found at {Model2Path_WithEnums}");
        }

        [Test]
        public void ResourceUsageDetector1_DetectUsages()
        {
            // Arrange
            EnsureFixturesExist();
            var content = File.ReadAllText(ModelPath);

            var file = System.IO.File.CreateText(@"X:\ModelResourceUage.txt");

            var chunks = LagoVista.AI.Rag.Chunkers.Services.ResourceUsageDetector.DetectUsages(content, "ORGID", "PROJID","REPOID", ModelPath);
            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.GreaterThan(0));
            // Optional: Output sections for visual inspection
            file.WriteLine(ResourceUsageRecord.CsvHeader);
            foreach (var chunk in chunks)
            {
                //file.WriteLine(ResxResourceExtractor.BuildEmbeddingText(chunk));
                file.WriteLine(chunk);
            }

            file.Close();
        }


        [Test]
        public void ResourceUsageDetector2_DetectUsages_NullSemanticModel_Throws()
        {
            // Arrange
            EnsureFixturesExist();
            var content = File.ReadAllText(Model2Path_WithEnums);

            var file = System.IO.File.CreateText(@"X:\ModelResourceUage_WithEnums.txt");

            var chunks = LagoVista.AI.Rag.Chunkers.Services.ResourceUsageDetector.DetectUsages(content, "ORGID", "PROJID", "REPOID", Model2Path_WithEnums);
            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.GreaterThan(0));
            // Optional: Output sections for visual inspection
            file.WriteLine(ResourceUsageRecord.CsvHeader);
            foreach (var chunk in chunks)
            {
                //file.WriteLine(ResxResourceExtractor.BuildEmbeddingText(chunk));
                file.WriteLine(chunk);
            }

            file.Close();
        }

    }
}
