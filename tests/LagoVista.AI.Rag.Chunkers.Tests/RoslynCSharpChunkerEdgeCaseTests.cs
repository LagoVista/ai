using System;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class RoslynCSharpChunkerEdgeCaseTests
    {
        // ------------------------------------------------------------
        // 1. Multiple types per file
        // ------------------------------------------------------------
        [Test]
        public void Chunk_File_With_Multiple_Top_Level_Types()
        {
            var source = @"public class A { public void M1() {} }
public enum B { One, Two }
public interface C { void IM(); }";

            var result = RoslynCSharpChunker.Chunk(source, "MultiTypes.cs");

            Assert.That(result.Successful, Is.True);
            var chunks = result.Result;

            Assert.That(chunks.Any(c => c.SymbolKind == "type" && c.SymbolName == "A"));
            Assert.That(chunks.Any(c => c.SymbolKind == "type" && c.SymbolName == "B"));
            Assert.That(chunks.Any(c => c.SymbolKind == "type" && c.SymbolName == "C"));

            Assert.That(chunks.Any(c => c.SymbolKind == "method" && c.SymbolName == "M1"));
            Assert.That(chunks.Any(c => c.SymbolKind == "method" && c.SymbolName == "IM"));
        }

        // ------------------------------------------------------------
        // 2. Expression-bodied members
        // ------------------------------------------------------------
        [Test]
        public void Chunk_Expression_Bodied_Methods_And_Properties()
        {
            var source = @"public class Expr
{
    public int Foo => 42;
    public int Bar(int x) => x * 2;
}";

            var result = RoslynCSharpChunker.Chunk(source, "Expr.cs");

            Assert.That(result.Successful, Is.True);
            var chunks = result.Result;

            Assert.That(chunks.Any(c => c.SymbolKind == "property" && c.SymbolName == "Foo"));
            Assert.That(chunks.Any(c => c.SymbolKind == "method" && c.SymbolName == "Bar"));
        }

        // ------------------------------------------------------------
        // 3. Local Functions
        // ------------------------------------------------------------
        [Test]
        public void Chunk_Local_Functions_Appear_Inside_Method_Text()
        {
            var source = @"public class Locals
{
    public void Outer()
    {
        int Add(int a, int b) => a + b;
        var z = Add(1, 2);
    }
}";

            var result = RoslynCSharpChunker.Chunk(source, "Locals.cs");

            Assert.That(result.Successful, Is.True);
            var chunks = result.Result;

            var outer = chunks.FirstOrDefault(c => c.SymbolKind == "method" && c.SymbolName == "Outer");
            Assert.That(outer, Is.Not.Null);

            Assert.That(outer.Text, Does.Contain("Add("));
        }

        // ------------------------------------------------------------
        // 4. Records + Record Structs
        // ------------------------------------------------------------
        [Test]
        public void Chunk_Records_And_RecordStructs()
        {
            var source = @"public record Person(string Name, int Age);
public record struct Point(int X, int Y);";

            var result = RoslynCSharpChunker.Chunk(source, "Records.cs");

            Assert.That(result.Successful, Is.True);
            var chunks = result.Result;

            Assert.That(chunks.Any(c => c.SymbolKind == "type" && c.SymbolName == "Person"));
            Assert.That(chunks.Any(c => c.SymbolKind == "type" && c.SymbolName == "Point"));
        }

        // ------------------------------------------------------------
        // 5. Preprocessor directives + regions
        // ------------------------------------------------------------
        [Test]
        public void Chunk_With_Preprocessor_Directives_And_Regions()
        {
            var source = @"#region Header
public class Pre
{
#if DEBUG
    public void DebugMethod() {}
#endif
    public void Normal() {}
}
#endregion";

            var result = RoslynCSharpChunker.Chunk(source, "Pre.cs");

            Assert.That(result.Successful, Is.True);
            var chunks = result.Result;

            Assert.That(chunks.Any(c => c.SymbolKind == "type" && c.SymbolName == "Pre"));
            Assert.That(chunks.Any(c => c.SymbolKind == "method" && c.SymbolName == "Normal"));
        }

        // ------------------------------------------------------------
        // 6. No trailing newline + single-line file
        // ------------------------------------------------------------
        [Test]
        public void Chunk_No_Trailing_Newline_Produces_Valid_Char_Ranges()
        {
            var source = "public class OneLine { public void M() {} }"; // no newline

            var result = RoslynCSharpChunker.Chunk(source, "OneLine.cs");

            Assert.That(result.Successful, Is.True);
            var chunks = result.Result;

            foreach (var c in chunks)
            {
                Assert.That(c.EndCharacter, Is.LessThanOrEqualTo(source.Length));
                Assert.That(c.StartCharacter, Is.LessThanOrEqualTo(c.EndCharacter));
                Assert.That(c.Text, Is.Not.Null.And.Not.Empty);
            }
        }

        // ------------------------------------------------------------
        // 7. Attribute-heavy method
        // ------------------------------------------------------------
        [Test]
        public void Chunk_Attribute_Heavy_Method_Includes_Attributes_In_Text()
        {
            var source = @"using System;
public class AttrHeavy
{
    [Obsolete(""Old""), CLSCompliant(false)]
    [System.Diagnostics.DebuggerStepThrough]
    public void Old() {}
}";

            var result = RoslynCSharpChunker.Chunk(source, "AttrHeavy.cs");

            Assert.That(result.Successful, Is.True);
            var chunks = result.Result;

            var m = chunks.FirstOrDefault(c => c.SymbolKind == "method" && c.SymbolName == "Old");
            Assert.That(m, Is.Not.Null);

            Assert.That(m.Text, Does.Contain("Obsolete"));
            Assert.That(m.Text, Does.Contain("DebuggerStepThrough"));
        }
    }
}
