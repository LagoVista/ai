//using System;
//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;
//using LagoVista.AI.Interfaces;
//using LagoVista.AI.Models;
//using LagoVista.AI.Services;
//using LagoVista.Core.AI.Models;
//using LagoVista.Core.Validation;
//using LagoVista.IoT.Logging.Loggers;
//using LagoVista.IoT.Logging.Utils;
//using NUnit.Framework;

//namespace LagoVista.AI.Tests
//{
//    [TestFixture]
//    public class ModeEntryBootstrapServiceTests
//    {
//        private sealed class FakeToolExecutor : IAgentToolExecutor
//        {
//            public List<AgentToolCall> Calls { get; } = new List<AgentToolCall>();

//            public Func<AgentToolCall, InvokeResult<AgentToolCall>> Handler { get; set; }

//            public Task<InvokeResult<AgentToolCall>> ExecuteServerToolAsync(
//                AgentToolCall call,
//                AgentPipelineContext context)
//            {
//                Calls.Add(call);
//                if (Handler != null)
//                {
//                    return Task.FromResult(Handler(call));
//                }

//                // Default: succeed and mark executed.
//                call.IsServerTool = true;
//                call.WasExecuted = true;
//                call.RequiresClientExecution = false;
//                call.ResultJson = "{}";
//                return Task.FromResult(InvokeResult<AgentToolCall>.Create(call));
//            }
//        }

//        [Test]
//        public async Task ExecuteAsync_NoBootstrapTools_ReturnsSuccess()
//        {
//            var executor = new FakeToolExecutor();
//            var svc = new ModeEntryBootstrapService(executor, new AdminLogger(new ConsoleLogWriter()));

//            var mode = new AgentMode { Key = "DDR", BootStrapTool = Array.Empty<BootStrapTool>() };
//            var req = new ModeEntryBootstrapRequest
//            {
//                Mode = mode,
//                ModeKey = "DDR",
//                ToolContext = new AgentToolExecutionContext()
//            };

//            var result = await svc.ExecuteAsync(req);

//            Assert.That(result.Successful, Is.True);
//            Assert.That(result.Result, Is.Not.Null);
//            Assert.That(result.Result.ToolCount, Is.EqualTo(0));
//            Assert.That(result.Result.ExecutedTools.Count, Is.EqualTo(0));
//            Assert.That(executor.Calls.Count, Is.EqualTo(0));
//        }

//        [Test]
//        public async Task ExecuteAsync_MultipleTools_RunsInOrder()
//        {
//            var executor = new FakeToolExecutor();
//            var svc = new ModeEntryBootstrapService(executor, new AdminLogger(new ConsoleLogWriter()));

//            var mode = new AgentMode
//            {
//                Key = "DDR",
//                BootStrapTool = new[]
//                {
//                    new BootStrapTool { ToolName = "tool_a", Arguments = new [] { "x" } },
//                    new BootStrapTool { ToolName = "tool_b", Arguments = new [] { "y" } },
//                }
//            };

//            var req = new ModeEntryBootstrapRequest
//            {
//                Mode = mode,
//                ModeKey = "DDR",
//                ToolContext = new AgentToolExecutionContext()
//            };

//            var result = await svc.ExecuteAsync(req);

//            Assert.That(result.Successful, Is.True);
//            Assert.That(executor.Calls.Count, Is.EqualTo(2));
//            Assert.That(executor.Calls[0].Name, Is.EqualTo("tool_a"));
//            Assert.That(executor.Calls[1].Name, Is.EqualTo("tool_b"));
//        }

//        [Test]
//        public async Task ExecuteAsync_ToolFailure_FailsFast()
//        {
//            var executor = new FakeToolExecutor();
//            executor.Handler = call =>
//            {
//                if (call.Name == "tool_a")
//                {
//                    call.IsServerTool = true;
//                    call.WasExecuted = true;
//                    call.RequiresClientExecution = false;
//                    call.ResultJson = "{}";
//                    return InvokeResult<AgentToolCall>.Create(call);
//                }

//                return InvokeResult<AgentToolCall>.FromError("boom");
//            };

//            var svc = new ModeEntryBootstrapService(executor, new AdminLogger(new ConsoleLogWriter()));

//            var mode = new AgentMode
//            {
//                Key = "DDR",
//                BootStrapTool = new[]
//                {
//                    new BootStrapTool { ToolName = "tool_a" },
//                    new BootStrapTool { ToolName = "tool_b" },
//                    new BootStrapTool { ToolName = "tool_c" },
//                }
//            };

//            var req = new ModeEntryBootstrapRequest
//            {
//                Mode = mode,
//                ModeKey = "DDR",
//                ToolContext = new AgentToolExecutionContext()
//            };

//            var result = await svc.ExecuteAsync(req);

//            Assert.That(result.Successful, Is.False);
//            // tool_a executed, tool_b attempted and failed, tool_c should not run
//            Assert.That(executor.Calls.Count, Is.EqualTo(2));
//            Assert.That(executor.Calls[0].Name, Is.EqualTo("tool_a"));
//            Assert.That(executor.Calls[1].Name, Is.EqualTo("tool_b"));
//        }

//        [Test]
//        public async Task ExecuteAsync_ModeChangeToolDisallowed_Fails()
//        {
//            var executor = new FakeToolExecutor();
//            var svc = new ModeEntryBootstrapService(executor, new AdminLogger(new ConsoleLogWriter()));

//            var mode = new AgentMode
//            {
//                Key = "DDR",
//                BootStrapTool = new[]
//                {
//                    new BootStrapTool { ToolName = "agent_change_mode" }
//                }
//            };

//            var req = new ModeEntryBootstrapRequest
//            {
//                Mode = mode,
//                ModeKey = "DDR",
//                ToolContext = new AgentToolExecutionContext()
//            };

//            var result = await svc.ExecuteAsync(req);

//            Assert.That(result.Successful, Is.False);
//            Assert.That(executor.Calls.Count, Is.EqualTo(0));
//        }

//        [Test]
//        public async Task ExecuteAsync_RequiresClientExecution_Fails()
//        {
//            var executor = new FakeToolExecutor();
//            executor.Handler = call =>
//            {
//                call.IsServerTool = true;
//                call.WasExecuted = true;
//                call.RequiresClientExecution = true;
//                call.ResultJson = "{}";
//                return InvokeResult<AgentToolCall>.Create(call);
//            };

//            var svc = new ModeEntryBootstrapService(executor, new AdminLogger(new ConsoleLogWriter()));

//            var mode = new AgentMode
//            {
//                Key = "DDR",
//                BootStrapTool = new[]
//                {
//                    new BootStrapTool { ToolName = "tool_a" }
//                }
//            };

//            var req = new ModeEntryBootstrapRequest
//            {
//                Mode = mode,
//                ModeKey = "DDR",
//                ToolContext = new AgentToolExecutionContext()
//            };

//            var result = await svc.ExecuteAsync(req);

//            Assert.That(result.Successful, Is.False);
//            Assert.That(executor.Calls.Count, Is.EqualTo(1));
//        }
//    }
//}
