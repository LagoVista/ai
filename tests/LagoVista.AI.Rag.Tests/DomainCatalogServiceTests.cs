using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LagoVista.AI.Rag.Models;
using LagoVista.AI.Rag.Services;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.IoT.Logging.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LagoVista.AI.Rag.Tests
{
    /// <summary>
    /// NUnit 4-style tests for DomainCatalogService.
    ///
    /// These tests focus on:
    /// - Persistence path & JSON round-trip
    /// - Query surface behavior over a pre-seeded catalog
    ///
    /// The discovery pipeline itself is intentionally not exercised here,
    /// because we do not yet have the concrete discovery contracts in scope.
    /// </summary>
    [TestFixture]
    public class DomainCatalogServiceTests
    {
    
        private static IngestionConfig CreateIngestionConfig(string root)
        {
            return new IngestionConfig
            {
                Ingestion = new FileIngestionConfig
                {
                    SourceRoot = root
                }
            };
        }

        [Test]
        public async Task Save_And_Load_Catalog_RoundTrips_Correctly()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var logger = new AdminLogger(new ConsoleLogWriter());
                var config = CreateIngestionConfig(tempRoot);

                var service = new DomainCatalogService(logger, config);

                // Build a simple catalog in memory and persist it via the service.
                var domainKey = "SampleDomain";

                var model = new ModelClassEntry(
                    domainKey: domainKey,
                    className: "SampleModel",
                    qualifiedClassName: "LagoVista.Sample.SampleModel",
                    title: "Sample Model",
                    description: "Sample model description.",
                    helpText: "Sample help.",
                    relativePath: "src/Sample/SampleModel.cs");

                var domain = new DomainEntry(
                    domainKey: domainKey,
                    title: "Sample Domain",
                    description: "Sample domain description.",
                    classes: new[] { model });

                // Use reflection to set the private _catalog field so we can exercise persistence
                // without coupling tests to the discovery pipeline.
                var catalog = new DomainCatalog(new[] { domain }, new[] { model });
                var field = typeof(DomainCatalogService).GetField("_catalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(field, Is.Not.Null, "Expected _catalog field to exist on DomainCatalogService.");
                field.SetValue(service, catalog);

                var saveResult = await InvokePrivateSaveAsync(service).ConfigureAwait(false);
                Assert.That(saveResult.Successful, Is.True, "Expected save to succeed.");

                // Now create a new service instance and load from disk.
                var service2 = new DomainCatalogService(logger, config);
                var loadResult = await service2.LoadCatalogAsync().ConfigureAwait(false);
                Assert.That(loadResult.Successful, Is.True, "Expected load to succeed.");

                var domains = service2.GetAllDomains();
                Assert.That(domains.Count, Is.EqualTo(1));
                Assert.That(domains[0].DomainKey, Is.EqualTo(domainKey));

                var classes = service2.GetClassesForDomain(domainKey);
                Assert.That(classes.Count, Is.EqualTo(1));
                Assert.That(classes[0].ClassName, Is.EqualTo("SampleModel"));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try
                    {
                        Directory.Delete(tempRoot, true);
                    }
                    catch
                    {
                        // ignore cleanup errors in tests
                    }
                }
            }
        }

        [Test]
        public void FindDomainForClass_Returns_NotFound_When_Class_Is_Missing()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var logger = new AdminLogger(new ConsoleLogWriter());
                var config = CreateIngestionConfig(tempRoot);
                var service = new DomainCatalogService(logger, config);

                // No catalog loaded; should return an error.
                var result = service.FindDomainForClass("NotThere");

                Assert.That(result.Successful, Is.False);
                Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try
                    {
                        Directory.Delete(tempRoot, true);
                    }
                    catch
                    {
                        // ignore cleanup errors
                    }
                }
            }
        }

        private static Task<InvokeResult> InvokePrivateSaveAsync(DomainCatalogService service)
        {
            var method = typeof(DomainCatalogService).GetMethod("SaveCatalogAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Expected SaveCatalogAsync helper method to exist.");

            var task = (Task<InvokeResult>)method.Invoke(service, new object[] { CancellationToken.None });
            return task;
        }
    }
}
