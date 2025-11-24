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
    public class ResourceChunkerTests
    {
        private const string ResourcePath = "./Content/resources.resx";

        private static void EnsureFixturesExist()
        {
            Assert.That(File.Exists(ResourcePath), Is.True, $"Model content file not found at {ResourcePath}");
        }

        [Test]
        public void ResourceChunker_BuildsSections_FromResxFile()
        {
            // Arrange
            EnsureFixturesExist();
            var content = File.ReadAllText(ResourcePath);
            // Act

            var file = System.IO.File.CreateText(@"X:\Resource.txt");

            var extractor = new ResxResourceExtractor(); 
            var chunks = extractor.Extract(content, ResourcePath);
            Assert.That(chunks, Is.Not.Null);
            Assert.That(chunks.Count, Is.GreaterThan(0));
            // Optional: Output sections for visual inspection
            file.WriteLine(new string('=', 80));
            file.WriteLine("RESOURCE CHUNKER SUMMARY SECTIONS");
            file.WriteLine(new string('=', 80));
            foreach (var chunk in chunks)
            {
                file.WriteLine(ResxResourceExtractor.BuildEmbeddingText(chunk));
                file.WriteLine(new string('-', 40));
            }

            file.Close();
        }
    }
}