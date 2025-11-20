using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class SubKindDetectorTests
    {
        [Test]
        public void Detects_Model_From_EntityDescription_Attribute()
        {
            var source = @"
using LagoVista.Core.Models;

[EntityDescription]
public class Device
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Models/Device.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Model));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("Device"));
                Assert.That(result.IsMixed, Is.False);
            });
        }

        [Test]
        public void Detects_DomainDescription_From_Namespace()
        {
            var source = @"
namespace Acme.Project.Domain
{
    public class DeviceDomain
    {
    }
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Domain/DeviceDomain.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.DomainDescription));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceDomain"));
            });
        }

        [Test]
        public void Detects_Manager_From_Interface_Pattern()
        {
            var source = @"
public class DeviceManager : IDeviceManager
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Managers/DeviceManager.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Manager));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceManager"));
            });
        }

        [Test]
        public void Detects_Startup_From_ClassName()
        {
            var source = @"
public class Startup
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Repositoriess/startup.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Startup));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("Startup"));
            });
        }

        [Test]
        public void Detects_Repository_From_BaseClass()
        {
            var source = @"
public class DeviceRepository : DocumentDBRepoBase<Device>
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Repositories/DeviceRepository.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Repository));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceRepository"));
            });
        }

        [Test]
        public void Detects_Repository_From_TableBaseClass()
        {
            var source = @"  public class LabelSampleRepo
                    {
                    }";

            var results = SubKindDetector.DetectForFile(source, "src/Repo/LabelSampleRepo.cs");
            WriteResults(results);

            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Repository));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("LabelSampleRepo"));
            });
        }

        [Test]
        public void Detects_Repository_If_Class_Ends_With_Repo()
        {
            var source = @"   class SampleMediaRepo : ISampleMediaRepo
    {
   
                    }";

            var results = SubKindDetector.DetectForFile(source, "src/Repo/SampleMediaRepo.cs");
            WriteResults(results);

            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Repository));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("SampleMediaRepo"));
            });
        }

        [Test]
        public void Detects_Repository_If_Class_Ends_With_Repository()
        {
            var source = @"  public class LabelSampleRepository
                    {
                    }";

            var results = SubKindDetector.DetectForFile(source, "src/Repo/LabelSampleRepository.cs");
            WriteResults(results);

            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Repository));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("LabelSampleRepository"));
            });
        }

        [Test]
        public void Detects_Controller_From_BaseType()
        {
            var source = @"
public class DeviceController : LagoVistaBaseController
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Controllers/DeviceController.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Controller));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceController"));
            });
        }

        [Test]
        public void Detects_Service_From_Interface()
        {
            var source = @"
public class DeviceService : IDeviceService
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Services/DeviceService.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Service));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceService"));
            });
        }

        [Test]
        public void Detects_Exception_From_Inherits_Exception()
        {
            var source = @"
public class InUseException : Exception
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Services/InUseException.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Exception));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("InUseException"));
            });
        }

        [Test]
        public void Detects_Interface_When_File_Is_Interface()
        {
            var source = @"
public interface IDeviceManager
{
    void Save();
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Managers/IDeviceManager.cs");

            WriteResults(results);

            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Interface));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("IDeviceManager"));
            });
        }

        [Test]
        public void Mixed_File_Is_Detected_And_Flagged()
        {
            var source = @"
[EntityDescription]
public class Device
{
}

public class DeviceRepository : DocumentDBRepoBase<Device>
{
}
";

            var results = SubKindDetector.DetectForFile(source, "src/Mixed/Device.cs");
            WriteResults(results);

            Assert.Multiple(() =>
            {
                // We expect two different SubKinds (Model + Repository) and IsMixed true on both.
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results.All(r => r.IsMixed), Is.True);

                Assert.That(results.Any(r => r.SubKind == CodeSubKind.Model && r.PrimaryTypeName == "Device"), Is.True);
                Assert.That(results.Any(r => r.SubKind == CodeSubKind.Repository && r.PrimaryTypeName == "DeviceRepository"), Is.True);
            });
        }

        [Test]
        public void Falls_Back_To_Other_When_No_Types_Present()
        {
            var source = @"
using System;
namespace Foo { }
";

            var results = SubKindDetector.DetectForFile(source, "src/Misc/Random.cs");
            var result = SingleResult(results);

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Other));
                Assert.That(result.PrimaryTypeName, Is.Null);
            });
        }

        // ---- Helpers ----------------------------------------------------

        private static SubKindDetectionResult SingleResult(IReadOnlyList<SubKindDetectionResult> results)
        {
            Assert.That(results, Is.Not.Null, "results should not be null");
            Assert.That(results.Count, Is.EqualTo(1), "expected exactly one type in this file for this test");
            return results[0];
        }

        private static void WriteResults(IReadOnlyList<SubKindDetectionResult> results)
        {
            if (results == null)
            {
                Console.WriteLine("Results: <null>");
                return;
            }

            foreach (var r in results)
            {
                Console.WriteLine("---- Result ----");
                Console.WriteLine("Path: " + r.Path);
                Console.WriteLine("SubKind: " + r.SubKind);
                Console.WriteLine("PrimaryTypeName: " + r.PrimaryTypeName);
                Console.WriteLine("IsMixed: " + r.IsMixed);
                Console.WriteLine("Reason: " + r.Reason);
                if (r.Evidence != null)
                {
                    foreach (var ev in r.Evidence)
                    {
                        Console.WriteLine("  Evidence: " + ev);
                    }
                }
            }
        }
    }
}
