using System;
using System.Collections.Generic;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests.Chunkers
{
    [TestFixture]
    public class ModelMetadataSummaryBuilderTests
    {
        [Test]
        public void BuildSummary_Returns_Empty_For_Null_Metadata()
        {
            var result = ModelMetadataSummaryBuilder.BuildSummary(null);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void BuildSummary_Uses_ModelName_And_Domain_When_Title_Is_Missing()
        {
            var metadata = new ModelMetadataDescription
            {
                ModelName = "Device",
                Domain = "Device Management",
                Description = "Represents a device in the system."
            };

            var result = ModelMetadataSummaryBuilder.BuildSummary(metadata);

            Assert.That(result, Does.Contain("The Device model belongs to the Device Management domain."));
            Assert.That(result, Does.Contain("Represents a device in the system."));
        }

        [Test]
        public void BuildSummary_Prefers_Title_Over_ModelName_For_Display_Name()
        {
            var metadata = new ModelMetadataDescription
            {
                ModelName = "Device",
                Title = "Device Settings",
                Domain = "Device Management",
                Description = "Represents the configuration of a device."
            };

            var result = ModelMetadataSummaryBuilder.BuildSummary(metadata);

            Assert.That(result, Does.Contain("The Device Settings model belongs to the Device Management domain."));
            Assert.That(result, Does.Not.Contain("The Device model belongs"));
        }

        [Test]
        public void BuildSummary_Uses_Help_When_Description_Is_Missing()
        {
            var metadata = new ModelMetadataDescription
            {
                ModelName = "Device",
                Domain = "Device Management",
                Help = "Used to manage devices in the system."
            };

            var result = ModelMetadataSummaryBuilder.BuildSummary(metadata);

            Assert.That(result, Does.Contain("Used to manage devices in the system."));
        }

        [Test]
        public void BuildSummary_Describes_Capabilities_When_Flags_Are_Set()
        {
            var metadata = new ModelMetadataDescription
            {
                ModelName = "Device",
                Domain = "Device Management",
                Description = "Represents a device in the system.",
                Cloneable = true,
                CanImport = true,
                CanExport = true
            };

            var result = ModelMetadataSummaryBuilder.BuildSummary(metadata);

            Assert.That(result, Does.Contain("It "));
            Assert.That(result, Does.Contain("can be cloned to speed setup"));
            Assert.That(result, Does.Contain("supports bulk import"));
            Assert.That(result, Does.Contain("supports bulk export"));
        }

        [Test]
        public void BuildSummary_Emits_Key_Fields_Using_Layouts_When_Available()
        {
            var metadata = new ModelMetadataDescription
            {
                ModelName = "Device",
                Domain = "Device Management",
                Description = "Represents a device in the system.",
                Fields = new List<ModelFieldMetadataDescription>
                {
                    new ModelFieldMetadataDescription
                    {
                        PropertyName = "Name",
                        Label = "Device Name",
                        Help = "Display name of the device.",
                        DataType = "string",
                        FieldType = "Text",
                        IsRequired = true
                    },
                    new ModelFieldMetadataDescription
                    {
                        PropertyName = "DeviceId",
                        Label = "Device Id",
                        Help = "Unique identifier used for integrations.",
                        DataType = "string",
                        FieldType = "Text",
                        IsRequired = true
                    },
                    new ModelFieldMetadataDescription
                    {
                        PropertyName = "Status",
                        Label = "Status",
                        Help = "Current runtime state of the device.",
                        DataType = "enum",
                        FieldType = "Select",
                        IsRequired = false
                    }
                },
                Layouts = new ModelFormLayouts
                {
                    Form = new ModelFormLayoutColumns
                    {
                        Col1Fields = new List<string> { "Name", "DeviceId" },
                        Col2Fields = new List<string> { "Status" }
                    }
                }
            };

            var result = ModelMetadataSummaryBuilder.BuildSummary(metadata);

            Assert.That(result, Does.Contain("Key fields:"));

            Assert.That(result, Does.Contain("Device Name (Name, string, Text, required)"));
            Assert.That(result, Does.Contain("Display name of the device."));

            Assert.That(result, Does.Contain("Device Id (DeviceId, string, Text, required)"));
            Assert.That(result, Does.Contain("Unique identifier used for integrations."));

            Assert.That(result, Does.Contain("Status (Status, enum, Select, optional)"));
            Assert.That(result, Does.Contain("Current runtime state of the device."));
        }

        [Test]
        public void BuildSummary_Uses_Watermark_When_Help_Is_Missing()
        {
            var metadata = new ModelMetadataDescription
            {
                ModelName = "Customer",
                Domain = "Customer Management",
                Description = "Represents a customer in the system.",
                Fields = new List<ModelFieldMetadataDescription>
                {
                    new ModelFieldMetadataDescription
                    {
                        PropertyName = "Email",
                        Label = "Email Address",
                        Help = null,
                        Watermark = "Enter a valid email address.",
                        DataType = "string",
                        FieldType = "Email",
                        IsRequired = true
                    }
                },
                Layouts = new ModelFormLayouts
                {
                    Form = new ModelFormLayoutColumns
                    {
                        Col1Fields = new List<string> { "Email" }
                    }
                }
            };

            var result = ModelMetadataSummaryBuilder.BuildSummary(metadata);

            Assert.That(result, Does.Contain("Email Address (Email, string, Email, required)"));
            Assert.That(result, Does.Contain("Enter a valid email address."));
        }

        [Test]
        public void BuildSummary_Falls_Back_To_First_Fields_When_Layouts_Are_Empty()
        {
            var metadata = new ModelMetadataDescription
            {
                ModelName = "Device",
                Description = "Represents a device in the system.",
                Fields = new List<ModelFieldMetadataDescription>
                {
                    new ModelFieldMetadataDescription
                    {
                        PropertyName = "Name",
                        Label = "Device Name",
                        Help = "Display name of the device.",
                        DataType = "string",
                        IsRequired = true
                    },
                    new ModelFieldMetadataDescription
                    {
                        PropertyName = "Description",
                        Label = "Description",
                        Help = "User-facing description of the device.",
                        DataType = "string",
                        IsRequired = false
                    }
                }
            };

            var result = ModelMetadataSummaryBuilder.BuildSummary(metadata);

            Assert.That(result, Does.Contain("Key fields:"));
            Assert.That(result, Does.Contain("Device Name (Name, string, required)"));
            Assert.That(result, Does.Contain("Description (Description, string, optional)"));
        }
    }
}
