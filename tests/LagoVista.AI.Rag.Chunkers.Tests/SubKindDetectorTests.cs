using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers;

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

            var result = SubKindDetector.DetectForFile(source, "src/Models/Device.cs");

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

            var result = SubKindDetector.DetectForFile(source, "src/Domain/DeviceDomain.cs");

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

            var result = SubKindDetector.DetectForFile(source, "src/Managers/DeviceManager.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Manager));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceManager"));
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

            var result = SubKindDetector.DetectForFile(source, "src/Repositories/DeviceRepository.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Repository));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceRepository"));
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

            var result = SubKindDetector.DetectForFile(source, "src/Controllers/DeviceController.cs");

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

            var result = SubKindDetector.DetectForFile(source, "src/Services/DeviceService.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Service));
                Assert.That(result.PrimaryTypeName, Is.EqualTo("DeviceService"));
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

            var result = SubKindDetector.DetectForFile(source, "src/Managers/IDeviceManager.cs");

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

            var result = SubKindDetector.DetectForFile(source, "src/Mixed/Device.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsMixed, Is.True);
                Assert.That(result.SubKind, Is.Not.EqualTo(CodeSubKind.Other));
            });
        }

        [Test]
        public void Falls_Back_To_Other_When_No_Signals_Present()
        {
            var source = @"
using System;
namespace Foo { }
";

            var result = SubKindDetector.DetectForFile(source, "src/Misc/Random.cs");

            Assert.Multiple(() =>
            {
                Assert.That(result.SubKind, Is.EqualTo(CodeSubKind.Other));
                Assert.That(result.PrimaryTypeName, Is.Null);
            });
        }
    }
}
