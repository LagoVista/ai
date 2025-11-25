using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class DdrSmokeTests
    {

        private const string DdrPath = "./Content/LargeDdr.md";



        private static void EnsureFixturesExist()
        {
  
            Assert.That(File.Exists(DdrPath), Is.True, $"Repository content file not found at {DdrPath}");
        }

        private void WriteChunks(IEnumerable<SummarySection> sections, string label)
        {
            var file = System.IO.File.CreateText(@$"x:\{label}.txt");

            var chunkIndex = 1;

            foreach (var chunk in sections)
            {
                file.WriteLine($"Start Chunk {chunkIndex}");


                file.WriteLine($"{chunk.SectionKey} - Part {chunk.PartIndex} of {chunk.PartTotal}");
                file.WriteLine(chunk.SectionNormalizedText);

                file.WriteLine($"End Chunk {chunkIndex++}");
                file.WriteLine();
            }

            file.Flush();
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


        [Test]
        public void Model_CSharpChunk_Smoke()
        {
            EnsureFixturesExist();

            var modelSource = File.ReadAllText(DdrPath);

            var plan = DdrDescriptionBuilder.FromSource(GetIndexFileContext(), modelSource);
            var sections = plan.Result.BuildSections(new DomainModelHeaderInformation()
            {

            });
            
            WriteChunks(sections, "Ddr");
        }
    }
}
