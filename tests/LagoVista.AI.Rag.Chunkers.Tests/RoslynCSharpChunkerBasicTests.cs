using System;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class RoslynCSharpChunkerBasicTests
    {
        [Test]
        public void Chunk_Simple_Class_Produces_File_Type_And_Method_Chunks()
        {
            var source = @"using System;

public class MyClass
{
    public void MyMethod()
    {
        Console.WriteLine(""Hello"");
    }
}
";

            var result = RoslynCSharpChunker.Chunk(source, "MyClass.cs", maxTokensPerChunk: 256, overlapLines: 3);

            Assert.That(result.Successful, Is.True, "Chunking should succeed for valid C# source.");
            Assert.That(result.Result, Is.Not.Null);

            var chunks = result.Result;

            var fileChunk = chunks.FirstOrDefault(c => c.SymbolKind == "file");
            Assert.That(fileChunk, Is.Not.Null, "Expected a file-level chunk.");
            Assert.That(fileChunk.SymbolName, Is.EqualTo("MyClass.cs"));

            var typeChunks = chunks.Where(c => c.SymbolKind == "type" && c.SymbolName == "MyClass").ToList();
            Assert.That(typeChunks.Count, Is.GreaterThanOrEqualTo(1), "Expected at least one type chunk for MyClass.");

            var methodChunks = chunks.Where(c => c.SymbolKind == "method" && c.SymbolName == "MyMethod").ToList();
            Assert.That(methodChunks.Count, Is.GreaterThanOrEqualTo(1), "Expected at least one method chunk for MyMethod.");

            foreach (var chunk in chunks)
            {
                Assert.That(chunk.LineStart, Is.GreaterThan(0));
                Assert.That(chunk.LineEnd, Is.GreaterThanOrEqualTo(chunk.LineStart));
                Assert.That(chunk.StartCharacter, Is.GreaterThanOrEqualTo(0));
                Assert.That(chunk.EndCharacter, Is.GreaterThan(chunk.StartCharacter));
                Assert.That(chunk.Text, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void Chunk_Method_Header_Comment_Is_Prepended_For_Method_Chunks()
        {
            var source = @"using System;

public class HeaderClass
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}
";

            var result = RoslynCSharpChunker.Chunk(source, "HeaderClass.cs", maxTokensPerChunk: 256, overlapLines: 0);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            var chunks = result.Result;
            var addChunk = chunks.FirstOrDefault(c => c.SymbolKind == "method" && c.SymbolName == "Add");

            Assert.That(addChunk, Is.Not.Null, "Expected a method chunk for Add.");
            Assert.That(addChunk.Text, Is.Not.Null.And.Not.Empty);

            // MethodSummaryBuilder should prepend a header comment like:
            // // Method Add. Signature: int Add(int a, int b).
            Assert.That(addChunk.Text, Does.StartWith("// Method Add."),
                "Method chunks should start with a generated header comment.");
        }

        [Test]
        public void Chunk_Long_Type_Produces_Overlapped_Type_Chunks_When_Token_Budget_Is_Small()
        {
            // Create a class with many similar methods to force multiple type chunks.
            var methods = string.Join("\n\n", Enumerable.Range(0, 20).Select(i =>
                $"    public void M{i}()\n    {{\n        var x = {i};\n    }}"));

            var source = "public class BigClass\n{\n" + methods + "\n}\n";

            // Small token budget to force multiple chunks for the type node.
            var result = RoslynCSharpChunker.Chunk(source, "BigClass.cs", maxTokensPerChunk: 80, overlapLines: 4);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            var chunks = result.Result;
            var typeChunks = chunks
                .Where(c => c.SymbolKind == "type" && c.SymbolName == "BigClass")
                .OrderBy(c => c.LineStart)
                .ToList();

            Assert.That(typeChunks.Count, Is.GreaterThan(1),
                "Expected multiple type chunks for BigClass under a small token budget.");

            // Overlap: later chunk should start on or before the end line of a previous one.
            for (int i = 1; i < typeChunks.Count; i++)
            {
                Assert.That(typeChunks[i].LineStart, Is.LessThanOrEqualTo(typeChunks[i - 1].LineEnd),
                    "Expected overlapping line ranges between consecutive type chunks.");
                Assert.That(typeChunks[i].StartCharacter, Is.GreaterThanOrEqualTo(typeChunks[i - 1].StartCharacter),
                    "Character ranges should not move backwards.");
            }
        }

        [Test]
        public void Chunk_Long_Method_Produces_Overlapped_Method_Chunks_With_OverlapLines()
        {
            // Build a method with many lines so it must be chunked.
            var bodyLines = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"        var v{i} = {i};"));

            var source = @"public class OverlapClass
{
    public void LongMethod()
    {
" + bodyLines + @"
    }
}
";

            var result = RoslynCSharpChunker.Chunk(source, "OverlapClass.cs", maxTokensPerChunk: 40, overlapLines: 3);

            Assert.That(result.Successful, Is.True);
            Assert.That(result.Result, Is.Not.Null);

            var chunks = result.Result;
            var methodChunks = chunks
                .Where(c => c.SymbolKind == "method" && c.SymbolName == "LongMethod")
                .OrderBy(c => c.LineStart)
                .ToList();

            Assert.That(methodChunks.Count, Is.GreaterThan(1),
                "Expected multiple chunks for LongMethod under a small token budget.");

            for (int i = 1; i < methodChunks.Count; i++)
            {
                // Overlap should exist on lines.
                Assert.That(methodChunks[i].LineStart, Is.LessThanOrEqualTo(methodChunks[i - 1].LineEnd),
                    "Expected overlapping line ranges between method chunks.");

                // Char ranges should not move backwards.
                Assert.That(methodChunks[i].StartCharacter, Is.GreaterThanOrEqualTo(methodChunks[i - 1].StartCharacter));
                Assert.That(methodChunks[i].EndCharacter, Is.GreaterThan(methodChunks[i].StartCharacter));

                // Chunks should be reasonably small.
                Assert.That(methodChunks[i].EstimatedTokens, Is.LessThan(200),
                    "Method chunks should remain reasonably small when split.");
            }
        }

        [Test]
        public void Chunk_Empty_Text_Returns_Error()
        {
            var result = RoslynCSharpChunker.Chunk(string.Empty, "Empty.cs");

            Assert.That(result.Successful, Is.False, "Empty text should produce an error result.");
            Assert.That(result.Result, Is.Null);
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty,
                "Expected at least one error message for empty text.");
        }
    }
}
