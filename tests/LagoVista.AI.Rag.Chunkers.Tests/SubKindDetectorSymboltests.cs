using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class SubKindDetectorSymbolTextTests
    {
        [Test]
        public void SymbolText_For_Multiple_Types_In_Same_Namespace_Is_Isolated()
        {
            var source = @"
using System;
using Foo.Domain;

namespace Foo.Domain.Models
{
    public class Customer
    {
        public string Name { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
    }
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Domain/Models/CustomerOrder.cs");

            Assert.That(results.Count, Is.EqualTo(2), "Expected one result per type.");

            var customer = results.Single(r => r.PrimaryTypeName == "Customer");
            var order = results.Single(r => r.PrimaryTypeName == "Order");

            // Both snippets should carry the using directives.
            Assert.Multiple(() =>
            {
                Assert.That(customer.SymbolText, Does.Contain("using System;"));
                Assert.That(customer.SymbolText, Does.Contain("using Foo.Domain;"));
                Assert.That(order.SymbolText, Does.Contain("using System;"));
                Assert.That(order.SymbolText, Does.Contain("using Foo.Domain;"));
            });

            // Both snippets should be wrapped in the correct namespace.
            Assert.Multiple(() =>
            {
                Assert.That(customer.SymbolText, Does.Contain("namespace Foo.Domain.Models"));
                Assert.That(order.SymbolText, Does.Contain("namespace Foo.Domain.Models"));
            });

            // Each snippet should only contain its own type.
            Assert.Multiple(() =>
            {
                Assert.That(customer.SymbolText, Does.Contain("public class Customer"));
                Assert.That(customer.SymbolText, Does.Not.Contain("public class Order"));

                Assert.That(order.SymbolText, Does.Contain("public class Order"));
                Assert.That(order.SymbolText, Does.Not.Contain("public class Customer"));
            });
        }

        [Test]
        public void SymbolText_For_FileScoped_Namespace_Is_Wrapped_In_Block_Namespace()
        {
            var source = @"
using System;

namespace Foo.Domain.Models;

public class Product
{
    public string Name { get; set; }
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Domain/Models/Product.cs");

            Assert.That(results.Count, Is.EqualTo(1));
            var product = results.Single();

            Assert.Multiple(() =>
            {
                // Usings preserved
                Assert.That(product.SymbolText, Does.Contain("using System;"));

                // File-scoped namespace should be normalized to a block namespace in the snippet
                Assert.That(product.SymbolText, Does.Contain("namespace Foo.Domain.Models"));

                // Type body present
                Assert.That(product.SymbolText, Does.Contain("public class Product"));
            });
        }

        [Test]
        public void SymbolText_For_Global_Type_Has_Usings_But_No_Namespace()
        {
            var source = @"
using System;
using System.Collections.Generic;

public class GlobalThing
{
    public int Id { get; set; }
}
";

            var results = SubKindDetector.DetectForFile(source, "src/GlobalThing.cs");

            Assert.That(results.Count, Is.EqualTo(1));
            var global = results.Single();

            Assert.Multiple(() =>
            {
                // Usings preserved
                Assert.That(global.SymbolText, Does.Contain("using System;"));
                Assert.That(global.SymbolText, Does.Contain("using System.Collections.Generic;"));

                // No namespace wrapper
                Assert.That(global.SymbolText, Does.Not.Contain("namespace "));

                // Type present
                Assert.That(global.SymbolText, Does.Contain("public class GlobalThing"));
            });
        }
    }
}
