using System;
using System.Linq;
using NUnit.Framework;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.AI.Rag.Chunkers.Services;

namespace LagoVista.AI.Rag.Chunkers.Tests
{
    [TestFixture]
    public class EndpointDescriptionBuilderTests
    {
        private string SourceText;

        [SetUp]
        public void Setup()
        {
            var path = "./Content/AgentContextTestController.txt";
            Assert.That(System.IO.File.Exists(path), Is.True, $"Controller content file not found at {path}");

            SourceText = System.IO.File.ReadAllText(path);
            Assert.That(!string.IsNullOrWhiteSpace(SourceText), Is.True, "Controller source was empty.");
        }

        [Test]
        public void Creates_All_Endpoint_Descriptions()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            Assert.That(endpoints, Is.Not.Null);
            Assert.That(endpoints.Count, Is.EqualTo(8), "Expected 8 endpoints to be discovered.");
        }

        [Test]
        public void Extracts_Basic_Identity_Correctly()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            var get = endpoints.Single(e => e.ActionName == "GetVectorDatabase");

            Assert.Multiple(() =>
            {
                Assert.That(get.ControllerName, Is.EqualTo("VectorDatabaseController"));
                Assert.That(get.ActionName, Is.EqualTo("GetVectorDatabase"));
                Assert.That(get.EndpointKey, Is.EqualTo("VectorDatabaseController.GetVectorDatabase"));
                Assert.That(get.RouteTemplate, Is.EqualTo("/api/ai/agentContextTest/{id}"));
                Assert.That(get.HttpMethods.First(), Is.EqualTo("GET"));
            });
        }

        [Test]
        public void Detects_Primary_Entity()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            var entityEndpoints = endpoints.Where(e => e.PrimaryEntity != null).ToList();

            Assert.That(entityEndpoints.Count, Is.GreaterThan(0));

            foreach (var ep in entityEndpoints)
            {
                Assert.That(ep.PrimaryEntity, Is.EqualTo("VectorDatabase"));
            }
        }

        [Test]
        public void Extracts_Handler_Metadata()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            var get = endpoints.Single(e => e.ActionName == "GetVectorDatabase");
            var add = endpoints.Single(e => e.ActionName == "AddAgentContextTest");
            var update = endpoints.Single(e => e.ActionName == "UpdateAgentContextTest");

            Assert.Multiple(() =>
            {
                Assert.That(get.Handler, Is.Not.Null);
                Assert.That(add.Handler, Is.Not.Null);
                Assert.That(update.Handler, Is.Not.Null);

                Assert.That(get.Handler.Interface, Is.EqualTo("IAgentContextTestManager"));
                Assert.That(get.Handler.Method, Is.EqualTo("GetAgentContextTestAsync"));

                Assert.That(add.Handler.Method, Is.EqualTo("AddAgentContextTestAsync"));
                Assert.That(update.Handler.Method, Is.EqualTo("UpdateAgentContextTestAsync"));
            });
        }

        [Test]
        public void Groups_Parameters_And_RequestBody_Correctly()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            var get = endpoints.Single(e => e.ActionName == "GetVectorDatabase");
            var create = endpoints.Single(e => e.ActionName == "AddAgentContextTest");
            var update = endpoints.Single(e => e.ActionName == "UpdateAgentContextTest");

            Assert.Multiple(() =>
            {
                // GET: id comes from route
                Assert.That(get.Parameters.Count, Is.EqualTo(1));
                Assert.That(get.Parameters[0].Name, Is.EqualTo("id"));
                Assert.That(get.Parameters[0].Source, Is.EqualTo(EndpointParameterSource.Route));

                // POST: body only
                Assert.That(create.Parameters.Count, Is.EqualTo(0));
                Assert.That(create.RequestBody, Is.Not.Null);
                Assert.That(create.RequestBody.ModelType, Is.EqualTo("AgentContextTest"));

                // PUT: body only
                Assert.That(update.Parameters.Count, Is.EqualTo(0));
                Assert.That(update.RequestBody, Is.Not.Null);
                Assert.That(update.RequestBody.ModelType, Is.EqualTo("AgentContextTest"));
            });
        }

        [Test]
        public void Detects_Response_Shape()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            var get = endpoints.Single(e => e.ActionName == "GetVectorDatabase");
            var create = endpoints.Single(e => e.ActionName == "AddAgentContextTest");

            Assert.Multiple(() =>
            {
                Assert.That(get.Responses, Is.Not.Null);
                Assert.That(get.Responses.Any(r => r.StatusCode == 200));

                var ok = get.Responses.First(r => r.StatusCode == 200);
                Assert.That(ok.ModelType, Is.EqualTo("AgentContextTest"));

                Assert.That(create.Responses.Any(r => r.StatusCode == 201));
                Assert.That(create.Responses.Any(r => r.StatusCode == 400));
            });
        }

        [Test]
        public void Captures_Authorization_Context()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            foreach (var endpoint in endpoints)
            {
                Assert.That(endpoint.Authorization, Is.Not.Null);
                Assert.That(endpoint.Authorization.RequiresAuthentication, Is.True);
                Assert.That(endpoint.Authorization.AllowAnonymous, Is.False);
                Assert.That(endpoint.Authorization.Tenancy, Is.EqualTo("OrgScoped"));
            }
        }

        [Test]
        public void Captures_Line_Positions()
        {
            var endpoints = EndpointDescriptionBuilder.CreateEndpointDescriptions(SourceText);

            foreach (var endpoint in endpoints)
            {
                Assert.That(endpoint.LineStart, Is.Not.Null, $"{endpoint.ActionName} missing LineStart");
                Assert.That(endpoint.LineEnd, Is.Not.Null, $"{endpoint.ActionName} missing LineEnd");

                Assert.That(endpoint.LineStart, Is.GreaterThan(0), $"{endpoint.ActionName} LineStart is not 1-based");
                Assert.That(endpoint.LineEnd, Is.GreaterThan(0), $"{endpoint.ActionName} LineEnd is not 1-based");

                Assert.That(endpoint.LineEnd.Value, Is.GreaterThanOrEqualTo(endpoint.LineStart.Value));
            }
        }
    }
}
