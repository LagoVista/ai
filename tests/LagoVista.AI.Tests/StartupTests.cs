using System;
using LagoVista.AI;
using LagoVista.Core.Interfaces;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using Moq;
using NUnit.Framework;

namespace LagoVista.AI.Tests
{
    [TestFixture]
    public class StartupTests
    {
        [Test]
        public void ConfigureServices_WithValidTools_DoesNotThrow()
        {
            // Arrange
            var services = new Mock<IServiceCollection>().Object;
            var serviceProvider = new Mock<IServiceProvider>().Object;
            var adminLogger = new AdminLogger(new ConsoleLogWriter());

            // Act + Assert
            Assert.DoesNotThrow(() =>
                Startup.ConfigureServices(services, adminLogger));
        }
    }
}
