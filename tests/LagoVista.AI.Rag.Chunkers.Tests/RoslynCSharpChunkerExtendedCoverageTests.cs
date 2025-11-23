using System;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class RoslynCSharpChunkerExtendedCoverageTests
    {
        [Test]
        public void Chunk_CRLF_And_LF_Produce_Equivalent_Structure()
        {
            var sourceLf = @"using System;

public class Newlines
{
    public void DoWork()
    {
        Console.WriteLine(""hi"");
    }
}
";

            // Same code, but with CRLF instead of LF.
            var sourceCrLf = sourceLf.Replace("\n", "\r\n");

            var lfResult = RoslynCSharpChunker.Chunk(sourceLf, "NewlinesLF.cs", maxTokensPerChunk: 256, overlapLines: 2);
            var crlfResult = RoslynCSharpChunker.Chunk(sourceCrLf, "NewlinesCRLF.cs", maxTokensPerChunk: 256, overlapLines: 2);

            Assert.That(lfResult.Successful, Is.True, "LF chunking should succeed.");
            Assert.That(crlfResult.Successful, Is.True, "CRLF chunking should succeed.");
            Assert.That(lfResult.Result, Is.Not.Null);
            Assert.That(crlfResult.Result, Is.Not.Null);

            var lfChunks = lfResult.Result.ToList();
            var crlfChunks = crlfResult.Result.ToList();

            Assert.That(lfChunks.Count, Is.EqualTo(crlfChunks.Count),
                "LF and CRLF versions should produce the same number of chunks.");

            for (int i = 0; i < lfChunks.Count; i++)
            {
                var lf = lfChunks[i];
                var crlf = crlfChunks[i];
                if (lf.SymbolKind != "file")
                {
                    Assert.That(crlf.SymbolName, Is.EqualTo(lf.SymbolName),
                        "SymbolName should match for non-file chunks.");
                }
                Assert.That(crlf.SymbolKind, Is.EqualTo(lf.SymbolKind), "SymbolKind should match between LF and CRLF.");
                Assert.That(crlf.SectionKey, Is.EqualTo(lf.SectionKey), "SectionKey should match between LF and CRLF.");

                // Line ranges should be identical regardless of LF vs CRLF.
                Assert.That(crlf.LineStart, Is.EqualTo(lf.LineStart), "LineStart should be the same for LF and CRLF.");
                Assert.That(crlf.LineEnd, Is.EqualTo(lf.LineEnd), "LineEnd should be the same for LF and CRLF.");
            }
        }

        [Test]
        public void Chunk_Handles_Unicode_Identifiers_And_Literals()
        {
            var source = @"using System;

public class UnicødeClass
{
    public void EmojiLog()
    {
        var message = ""Hello 🚀 世界""; // rocket + Chinese characters
        Console.WriteLine(message);
    }
}
";

            var result = RoslynCSharpChunker.Chunk(source, "UnicødeClass.cs", maxTokensPerChunk: 256, overlapLines: 2);

            Assert.That(result.Successful, Is.True, "Chunking should succeed for Unicode-heavy source.");
            Assert.That(result.Result, Is.Not.Null);

            var chunks = result.Result.ToList();

            var typeChunk = chunks.FirstOrDefault(c => c.SymbolKind == "type" && c.SymbolName == "UnicødeClass");
            Assert.That(typeChunk, Is.Not.Null, "Expected type chunk for UnicødeClass.");

            var methodChunk = chunks.FirstOrDefault(c => c.SymbolKind == "method" && c.SymbolName == "EmojiLog");
            Assert.That(methodChunk, Is.Not.Null, "Expected method chunk for EmojiLog.");

            // Ensure non-ASCII characters flow through the normalized text.
            Assert.That(methodChunk.Text, Does.Contain("🚀"), "Method chunk should include the rocket emoji.");
            Assert.That(methodChunk.Text, Does.Contain("世界"), "Method chunk should include the Chinese characters.");

            Assert.That(methodChunk.LineStart, Is.GreaterThan(0));
            Assert.That(methodChunk.LineEnd, Is.GreaterThanOrEqualTo(methodChunk.LineStart));
            Assert.That(methodChunk.StartCharacter, Is.GreaterThanOrEqualTo(0));
            Assert.That(methodChunk.EndCharacter, Is.GreaterThan(methodChunk.StartCharacter));
        }

        [Test]
        public void Chunk_Giant_Switch_Is_Split_Into_Multiple_Method_Chunks()
        {
            // Build a large switch statement to stress the chunker.
            var cases = string.Join("\n\n", Enumerable.Range(0, 120).Select(i =>
                $"        case {i}:\n            Console.WriteLine(\"Case {i}\");\n            break;"));

            var source = @"using System;

public class SwitchClass
{
    public void BigSwitch(int value)
    {
        switch (value)
        {
" + cases + @"
        }
    }
}
";

            var result = RoslynCSharpChunker.Chunk(source, "SwitchClass.cs", maxTokensPerChunk: 80, overlapLines: 4);

            Assert.That(result.Successful, Is.True, "Chunking should succeed for large switch method.");
            Assert.That(result.Result, Is.Not.Null);

            var chunks = result.Result.ToList();

            var methodChunks = chunks
                .Where(c => c.SymbolKind == "method" && c.SymbolName == "BigSwitch")
                .OrderBy(c => c.LineStart)
                .ToList();

            Assert.That(methodChunks.Count, Is.GreaterThan(1),
                "Expected BigSwitch to be split into multiple chunks under a small token budget.");

            var sourceLength = source.Length;
            var lastStart = -1;

            foreach (var chunk in methodChunks)
            {
                Assert.That(chunk.Text, Is.Not.Null.And.Not.Empty);
                Assert.That(chunk.LineStart, Is.GreaterThan(0));
                Assert.That(chunk.LineEnd, Is.GreaterThanOrEqualTo(chunk.LineStart));

                Assert.That(chunk.StartCharacter, Is.GreaterThanOrEqualTo(lastStart));
                Assert.That(chunk.EndCharacter, Is.GreaterThan(chunk.StartCharacter));
                Assert.That(chunk.EndCharacter, Is.LessThanOrEqualTo(sourceLength));

                Assert.That(chunk.EstimatedTokens, Is.LessThan(300),
                    "Each BigSwitch chunk should remain reasonably small.");

                lastStart = chunk.StartCharacter;
            }
        }

        [Test]
        public void Chunk_Includes_Attributes_And_DocComments_With_Method()
        {
            var source = @"using System;

public class AttributedClass
{
    /// <summary>
    /// This method is obsolete.
    /// </summary>
    [Obsolete(""Use NewMethod instead"")]
    public void OldMethod()
    {
        Console.WriteLine(""Old"");
    }
}
";

            var result = RoslynCSharpChunker.Chunk(source, "AttributedClass.cs", maxTokensPerChunk: 256, overlapLines: 0);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            var chunks = result.Result.ToList();
            var methodChunk = chunks.FirstOrDefault(c => c.SymbolKind == "method" && c.SymbolName == "OldMethod");

            Assert.That(methodChunk, Is.Not.Null, "Expected method chunk for OldMethod.");
            Assert.That(methodChunk.Text, Is.Not.Null.And.Not.Empty);

            // Should have method summary header.
            Assert.That(methodChunk.Text, Does.Contain("/// <summary>"),
                "Method chunk should include XML doc comments.");

            Assert.That(methodChunk.Text, Does.Contain("[Obsolete"),
                "Method chunk should include attributes attached to the method.");
        }
    }
}
