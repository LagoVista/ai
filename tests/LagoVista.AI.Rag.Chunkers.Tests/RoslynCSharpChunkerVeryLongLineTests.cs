using System;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class RoslynCSharpChunkerVeryLongLineTests
    {
        [Test]
        public void Very_Long_Single_Line_Method_Is_Sliced_Into_Multiple_Chunks()
        {
            // Arrange: build a source file with a single extremely long line inside a method.
            var longLiteral = new string('a', 10000);

            var source = @"using System;

public class LongLineClass
{
    public void LongMethod()
    {
        var x = """ + longLiteral + @""";
    }
}";

            // Use a very small token budget to force the slice logic to kick in.
            var result = RoslynCSharpChunker.Chunk(source, "LongLineClass.cs", maxTokensPerChunk: 50, overlapLines: 0);

            Assert.That(result.Successful, Is.True, "Chunking should succeed for valid C# source.");
            Assert.That(result.Result, Is.Not.Null, "Chunk result should not be null.");

            var chunks = result.Result;
            var methodChunks = chunks
                .Where(c => c.SymbolName == "LongMethod")
                .ToList();

            Assert.That(methodChunks.Count, Is.GreaterThan(1),
                "Expected LongMethod to be split into multiple chunks due to very long line.");

            // In the very-long-line case, we expect multiple chunks that each represent
            // a slice of the same physical line (LineStart == LineEnd), produced by
            // the SliceVeryLongLine helper.
            var longLineChunks = methodChunks
                .Where(c => c.LineStart == c.LineEnd)
                .OrderBy(c => c.StartCharacter)
                .ToList();

            Assert.That(longLineChunks.Count, Is.GreaterThan(1),
                "Expected multiple single-line chunks for the very long line.");

            // Character ranges should be strictly increasing and within the source length.
            var sourceLength = source.Length;
            var lastEnd = -1;

            foreach (var chunk in longLineChunks)
            {
                Assert.That(chunk.Text, Is.Not.Null.And.Not.Empty,
                    "Each sliced chunk should contain non-empty text.");

                Assert.That(chunk.StartCharacter, Is.LessThan(chunk.EndCharacter),
                    "Chunk StartCharacter should be less than EndCharacter.");

                Assert.That(chunk.StartCharacter, Is.GreaterThanOrEqualTo(lastEnd),
    "Slice character ranges should advance without decreasing and without overlapping previous text.");

                Assert.That(chunk.EndCharacter, Is.LessThanOrEqualTo(sourceLength),
                    "Chunk EndCharacter should not exceed the source length.");

                Assert.That(chunk.EstimatedTokens, Is.LessThanOrEqualTo(50),
                    "Estimated token count should respect the maxTokensPerChunk budget.");

                lastEnd = chunk.EndCharacter;
            }
        }

        [Test]
        public void Extremely_Small_Token_Budget_Does_Not_Hang_And_Produces_NonEmpty_Segments()
        {
            // Arrange: similar structure but with a different long literal and a tiny token budget.
            var longLiteral = new string('b', 2000);

            var source = @"public class TinyBudget
{
    public void LongMethod()
    {
        var x = """ + longLiteral + @""";
    }
    }
}
";

            // Very small budget to stress the safety logic inside SliceVeryLongLine.
            var result = RoslynCSharpChunker.Chunk(source, "TinyBudget.cs", maxTokensPerChunk: 5, overlapLines: 0);

            Assert.That(result.Successful, Is.True, "Chunking should succeed even with tiny token budgets.");
            Assert.That(result.Result, Is.Not.Null, "Chunk result should not be null.");

            var chunks = result.Result;
            var methodChunks = chunks
                .Where(c => c.SymbolName == "LongMethod")
                .ToList();

            Assert.That(methodChunks.Count, Is.GreaterThan(1),
                "Expected LongMethod to be represented by multiple chunks under a tiny token budget.");

            var singleLineSegments = methodChunks
                .Where(c => c.LineStart == c.LineEnd)
                .ToList();

            Assert.That(singleLineSegments.Count, Is.GreaterThan(0),
                "Expected at least one single-line segment for the long line.");

            foreach (var chunk in singleLineSegments)
            {
                Assert.That(chunk.Text, Is.Not.Null.And.Not.Empty,
                    "Each sliced chunk should contain non-empty text.");

                Assert.That(chunk.EstimatedTokens, Is.LessThanOrEqualTo(50),
                    "Estimated token count should respect the tiny maxTokensPerChunk budget.");

                Assert.That(chunk.StartCharacter, Is.LessThan(chunk.EndCharacter),
                    "Chunk StartCharacter should be less than EndCharacter.");
            }
        }
    }
}
