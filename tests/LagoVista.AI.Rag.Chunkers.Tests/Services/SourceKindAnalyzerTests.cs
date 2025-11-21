using System;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Chunkers.Tests.Services
{
    [TestFixture]
    public class SourceKindAnalyzerTests
    {
        [Test]
        public void AnalyzeFile_ResourceFile_ByExtension()
        {
            var result = SourceKindAnalyzer.AnalyzeFile("dummy", "Resources/MyResources.resx");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.ResourceFile));
                Assert.That(result.PrimaryTypeName, Is.Null);
                Assert.That(result.IsMixed, Is.False);
                Assert.That(result.Reason, Does.Contain("ResourceFile"));
            });
        }

        [Test]
        public void AnalyzeFile_Model_From_EntityDescription_Attribute()
        {
            var source = @"using LagoVista.Core.Models;

[EntityDescription]
public class Device
{
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Models/Device.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Model));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("Device"));
                Assert.That(result.IsMixed, Is.False);
                Assert.That(result.Reason, Does.Contain("EntityDescription"));
            });
        }

        [Test]
        public void AnalyzeFile_DomainDescription_From_Attribute()
        {
            var source = @"using LagoVista.Core.Models.UIMetaData;

[DomainDescriptor]
public class AIDomain
{
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Domain/AIDomain.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.DomainDescription));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("AIDomain"));
                Assert.That(result.Reason, Does.Contain("DomainDescriptor"));
            });
        }

        [Test]
        public void AnalyzeFile_Manager_From_BaseType_And_Namespace()
        {
            var source = @"namespace LagoVista.AI.Managers
{
    public class AgentContextManager : ManagerBase
    {
    }
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Managers/AgentContextManager.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Manager));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("AgentContextManager"));
                Assert.That(result.Reason, Does.Contain("ManagerBase").Or.Contain("Managers"));
            });
        }

        [Test]
        public void AnalyzeFile_Repository_From_BaseType_And_Path()
        {
            var source = @"namespace LagoVista.AI.Repositories
{
    public class AgentContextRepository : DocumentDBRepoBase
    {
    }
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Repositories/AgentContextRepository.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Repository));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("AgentContextRepository"));
                Assert.That(result.Reason, Does.Contain("repository base type").Or.Contain("Repositories"));
            });
        }

        [Test]
        public void AnalyzeFile_Controller_From_Attribute_And_BaseType()
        {
            var source = @"using Microsoft.AspNetCore.Mvc;

namespace LagoVista.AI.Controllers
{
    [ApiController]
    public class AgentContextController : ControllerBase
    {
    }
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Controllers/AgentContextController.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Controller));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("AgentContextController"));
                Assert.That(result.Reason, Does.Contain("ApiController").Or.Contain("controller base type"));
            });
        }

        [Test]
        public void AnalyzeFile_Service_From_Interface_Name_And_Namespace()
        {
            var source = @"namespace LagoVista.AI.Services
{
   
    public class AgentContextService : IAgentContextService
    {
    }
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Services/AgentContextService.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Service));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("AgentContextService"));
                Assert.That(result.Reason, Does.Contain("Service"));
            });
        }


        [Test]
        public void Two_Items_Throw_Exception()
        {
            var source = @"namespace LagoVista.AI.Services
{
    public interface IAgentContextService
    {
    }

    public class AgentContextService : IAgentContextService
    {
    }
}
";

            // SourceKindAnalyzer is intentionally single-type only; multiple types is a bug.
            Assert.Throws<InvalidOperationException>(() =>
            {
                SourceKindAnalyzer.AnalyzeFile(source, "src/Services/AgentContextService.cs");
            });
        }

        [Test]
        public void AnalyzeFile_Interface_SubKind_Interface()
        {
            var source = @"public interface IAgentContext
{
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Interfaces/IAgentContext.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Interface));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("IAgentContext"));
                Assert.That(result.Reason, Does.Contain("interface"));
            });
        }

        [Test]
        public void AnalyzeFile_Startup_By_ClassName()
        {
            var source = @"public class Startup
{
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Startup.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Startup));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("Startup"));
            });
        }

        [Test]
        public void AnalyzeFile_Exception_From_BaseType()
        {
            var source = @"using System;

public class AgentContextException : Exception
{
}
";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Exceptions/AgentContextException.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Exception));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("AgentContextException"));
                Assert.That(result.Reason, Does.Contain("inherits from Exception"));
            });
        }

        [Test]
        public void AnalyzeFile_NoTypes_Defaults_To_Other()
        {
            var source = @"// Just a comment, no types";

            var result = SourceKindAnalyzer.AnalyzeFile(source, "src/Misc/NoTypesHere.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Other));
                Assert.That(result.PrimaryTypeName, Is.Null);
                Assert.That(result.Reason, Does.Contain("No top-level types"));
            });
        }

        [Test]
        public void AnalyzeFile_MultipleTypes_Throws()
        {
            var source = @"public class First
{
}

public class Second
{
}
";

            Assert.Throws<InvalidOperationException>(() =>
            {
                SourceKindAnalyzer.AnalyzeFile(source, "src/Models/Multiple.cs");
            });
        }
    }
}
