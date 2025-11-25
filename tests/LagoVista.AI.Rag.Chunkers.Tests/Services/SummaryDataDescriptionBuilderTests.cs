using System.Collections.Generic;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests.Services
{
    [TestFixture]
    public class SummaryDataDescriptionBuilderTests
    {
        [Test]
        public void FromSource_Finds_SummaryData_Class_And_Builds_Description()
        {
            // Arrange
            const string sourceText = @"using LagoVista.Core.Attributes;
using LagoVista.Core.Models;

namespace Demo.Summaries
{
    public class DeviceSummary : SummaryData
    {
        [ListColumn(Visible: false)]
        public bool IsDeleted { get; set; }

        public bool IsDraft { get; set; }

        public double? Stars { get; set; }

        public int RatingsCount { get; set; }

        public int? DiscussionsTotal { get; set; }

        public string LastUpdatedDate { get; set; }
    }
}";

            // Act
            var result = SummaryDataDescriptionBuilder.FromSource(sourceText, new Dictionary<string, string>());

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.True, "Builder should succeed when a SummaryData-derived class is present.");
            Assert.That(result.Result, Is.Not.Null);

            var description = result.Result;

            // Identity
            Assert.That(description.SummaryTypeName, Is.EqualTo("Demo.Summaries.DeviceSummary"));
            Assert.That(description.UnderlyingEntityTypeName, Is.EqualTo("Device"));
            Assert.That(description.ListName, Is.EqualTo("Device List"));

            // Fields
            Assert.That(description.Fields, Is.Not.Null);
            Assert.That(description.Fields.Count, Is.GreaterThanOrEqualTo(1));

            var isDeletedField = description.Fields.FirstOrDefault(f => f.Name == "IsDeleted");
            Assert.That(isDeletedField, Is.Not.Null, "IsDeleted field should be discovered.");

            // IMPORTANT: Visible:false should be respected
            Assert.That(isDeletedField.IsVisible, Is.False, "ListColumn(Visible:false) should override the default visibility.");

            // Behavior text should reflect expected flags
            Assert.That(description.BehaviorDescription, Does.Contain("Soft-delete"));
            Assert.That(description.BehaviorDescription, Does.Contain("Draft versus published"));
            Assert.That(description.BehaviorDescription, Does.Contain("Rating information"));
            Assert.That(description.BehaviorDescription, Does.Contain("Discussion activity"));
            Assert.That(description.BehaviorDescription, Does.Contain("Recency"));
        }

        [Test]
        public void FromSource_Returns_Error_When_No_SummaryData_Class_Found()
        {
            // Arrange
            const string sourceText = @"namespace Demo.Summaries
{
    public class NotASummary
    {
        public string Name { get; set; }
    }
}";

            // Act
            var result = SummaryDataDescriptionBuilder.FromSource(sourceText, new Dictionary<string, string>());

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Successful, Is.False, "Builder should not succeed when no SummaryData-derived class is present.");
            Assert.That(result.Errors.Any(), Is.True);
        }
    }
}
