using System;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Chunkers
{
    [TestFixture]
    public class MethodSummaryBuilderTests
    {
        [Test]
        public void BuildSummary_Throws_On_Null_Context()
        {
            Assert.That(
                () => MethodSummaryBuilder.BuildSummary(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void BuildSummary_Uses_Placeholder_When_All_Fields_Empty()
        {
            var ctx = new MethodSummaryContext();

            var summary = MethodSummaryBuilder.BuildSummary(ctx);

            Assert.That(summary, Is.EqualTo("Method summary placeholder."));
        }

        [Test]
        public void BuildSummary_With_Just_MethodName_Produces_Simple_Sentence()
        {
            var ctx = new MethodSummaryContext
            {
                MethodName = "AddCustomerAsync"
            };

            var summary = MethodSummaryBuilder.BuildSummary(ctx);

            Assert.That(summary, Is.EqualTo("Method AddCustomerAsync."));
        }

        [Test]
        public void BuildSummary_With_Method_Model_And_Domain_Composes_Core_Parts()
        {
            var ctx = new MethodSummaryContext
            {
                MethodName = "AddCustomerAsync",
                SubKind = "Manager",
                ModelName = "Customer",
                DomainName = "Customer Management"
            };

            var summary = MethodSummaryBuilder.BuildSummary(ctx);

            Assert.That(summary, Does.Contain("Method AddCustomerAsync (Manager) operates on the Customer model in the Customer Management domain"));
            Assert.That(summary.EndsWith("."), Is.True);
        }

        [Test]
        public void BuildSummary_Includes_Domain_And_Model_Taglines_And_Signature()
        {
            var ctx = new MethodSummaryContext
            {
                MethodName = "AddCustomerAsync",
                SubKind = "Manager",
                ModelName = "Customer",
                DomainName = "Customer Management",
                DomainTagline = "manages customer accounts and billing",
                ModelTagline = "represents a customer in the system",
                Signature = "AddCustomerAsync(Customer customer, EntityHeader org, EntityHeader user)"
            };

            var summary = MethodSummaryBuilder.BuildSummary(ctx);

            Assert.That(summary, Does.Contain("Method AddCustomerAsync (Manager) operates on the Customer model in the Customer Management domain"));
            Assert.That(summary, Does.Contain("Domain focus: manages customer accounts and billing"));
            Assert.That(summary, Does.Contain("Model focus: represents a customer in the system"));
            Assert.That(summary, Does.Contain("Signature: AddCustomerAsync(Customer customer, EntityHeader org, EntityHeader user)"));
            Assert.That(summary.EndsWith("."), Is.True);
        }

        [Test]
        public void BuildHeaderComment_Uses_BuildSummary_And_Prefixes_Comment()
        {
            var ctx = new MethodSummaryContext
            {
                MethodName = "AddCustomerAsync",
                SubKind = "Manager",
                ModelName = "Customer",
                DomainName = "Customer Management"
            };

            var header = MethodSummaryBuilder.BuildHeaderComment(ctx);

            Assert.That(header.StartsWith("// "), Is.True);

            var summary = MethodSummaryBuilder.BuildSummary(ctx);
            Assert.That(header, Is.EqualTo("// " + summary));
        }

        [Test]
        public void BuildHeaderComment_Uses_Placeholder_When_Context_Is_Empty()
        {
            var ctx = new MethodSummaryContext();

            var header = MethodSummaryBuilder.BuildHeaderComment(ctx);

            Assert.That(header, Is.EqualTo("// Method summary placeholder."));
        }
    }
}
