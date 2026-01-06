using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LagoVista.AI.Interfaces;
using LagoVista.IoT.Logging.Loggers;

namespace LagoVista.AI.ACP
{
 
    public class AcpCommandRegistry : IAcpCommandRegistry
    {
        private readonly Dictionary<string, Type> _commandsById;
        private readonly Dictionary<string, AcpCommandDescriptor> _descriptorsById;
        private readonly List<AcpCommandSummary> _allCommands;
        private readonly IAdminLogger _logger;

        // Optional: keep parity with your existing registry pattern
        public static AcpCommandRegistry Instance { get; private set; }

        // Reuse tool-name-ish pattern (adjust if you want dots allowed like "acp.change_mode")
        // If you want dots, use: ^[a-zA-Z0-9_.-]+$
        private static readonly Regex CommandIdPattern =
            new Regex("^[a-zA-Z0-9_.-]+$", RegexOptions.Compiled);

        public AcpCommandRegistry(IAdminLogger logger)
        {
            Instance = this;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _commandsById = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            _descriptorsById = new Dictionary<string, AcpCommandDescriptor>(StringComparer.OrdinalIgnoreCase);
            _allCommands = new List<AcpCommandSummary>();
        }

        public void RegisterCommand<T>() where T : IAcpCommand
        {
            var commandType = typeof(T);

            // CONTRACT #1: must have [AcpCommand]
            var cmdAttr = commandType.GetCustomAttribute<AcpCommandAttribute>(inherit: false);
            if (cmdAttr == null)
            {
                var msg = $"ACP Command '{commandType.FullName}' must declare [AcpCommand(commandId, displayName, description)].";
                _logger.AddError("[AcpCommandRegistry_RegisterCommand__MissingAcpCommandAttribute]", msg);
                throw new InvalidOperationException(msg);
            }

            // CONTRACT #2: must have [AcpTriggers]
            var triggersAttr = commandType.GetCustomAttribute<AcpTriggersAttribute>(inherit: false);
            if (triggersAttr == null || triggersAttr.Triggers == null || triggersAttr.Triggers.Length == 0)
            {
                var msg = $"ACP Command '{commandType.FullName}' must declare [AcpTriggers(...)] with at least one trigger.";
                _logger.AddError("[AcpCommandRegistry_RegisterCommand__MissingTriggers]", msg);
                throw new InvalidOperationException(msg);
            }

            // CONTRACT #3: must have [AcpArgs]
            var argsAttr = commandType.GetCustomAttribute<AcpArgsAttribute>(inherit: false);
            if (argsAttr == null)
            {
                var msg = $"ACP Command '{commandType.FullName}' must declare [AcpArgs(min,max)].";
                _logger.AddError("[AcpCommandRegistry_RegisterCommand__MissingArgs]", msg);
                throw new InvalidOperationException(msg);
            }

            var commandId = cmdAttr.CommandId;

            // CONTRACT #4: validate commandId
            if (String.IsNullOrWhiteSpace(commandId))
            {
                var msg = $"ACP Command '{commandType.FullName}' declares an empty CommandId.";
                _logger.AddError("[AcpCommandRegistry_RegisterCommand__EmptyCommandId]", msg);
                throw new InvalidOperationException(msg);
            }

            if (!CommandIdPattern.IsMatch(commandId))
            {
                var msg = $"ACP Command '{commandType.FullName}' declares CommandId='{commandId}', which does not match pattern '{CommandIdPattern}'.";
                _logger.AddError("[AcpCommandRegistry_RegisterCommand__InvalidCommandIdPattern]", msg);
                throw new InvalidOperationException(msg);
            }

            // CONTRACT #5: prevent duplicates
            if (_commandsById.ContainsKey(commandId))
            {
                var existingType = _commandsById[commandId];
                var msg = $"Duplicate ACP CommandId '{commandId}'. Existing type: '{existingType.FullName}', Duplicate type: '{commandType.FullName}'.";
                _logger.AddError("[AcpCommandRegistry_RegisterCommand__DuplicateCommandId]", msg);
                throw new InvalidOperationException(msg);
            }

            // Normalize triggers (trim)
            var triggers = triggersAttr.Triggers.Select(t => t?.Trim())
                                                .Where(t => !String.IsNullOrWhiteSpace(t))
                                                .ToArray();

            if (triggers.Length == 0)
            {
                var msg = $"ACP Command '{commandType.FullName}' has no usable triggers after trimming.";
                _logger.AddError("[AcpCommandRegistry_RegisterCommand__EmptyTriggersAfterTrim]", msg);
                throw new InvalidOperationException(msg);
            }

            // Optional: validate regex attributes compile
            var argRegexAttrs = commandType.GetCustomAttributes<AcpArgRegexAttribute>(inherit: false).ToArray();
            foreach (var rx in argRegexAttrs)
            {
                try
                {
                    var options = rx.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                    _ = new Regex(rx.Pattern, options);
                }
                catch (Exception ex)
                {
                    var msg = $"ACP Command '{commandType.FullName}' has invalid AcpArgRegex pattern at index {rx.Index}: '{rx.Pattern}'. {ex.Message}";
                    _logger.AddError("[AcpCommandRegistry_RegisterCommand__InvalidArgRegex]", msg);
                    throw new InvalidOperationException(msg, ex);
                }
            }

            var safetyAttr = commandType.GetCustomAttribute<AcpSafetyAttribute>(inherit: false);

            var descriptor = new AcpCommandDescriptor
            {
                CommandId = commandId,
                DisplayName = cmdAttr.DisplayName,
                Description = cmdAttr.Description,
                CommandType = commandType,
                Triggers = triggers,
                TriggerCaseInsensitive = triggersAttr.CaseInsensitive,
                MinArgs = argsAttr.Min,
                MaxArgs = argsAttr.Max,
                RequiresConfirmation = safetyAttr?.RequiresConfirmation ?? false,
                ProducesSideEffects = safetyAttr?.ProducesSideEffects ?? false,
                ArgRegexRules = argRegexAttrs
                    .Select(a => new AcpArgRegexRule
                    {
                        Index = a.Index,
                        Pattern = a.Pattern,
                        IgnoreCase = a.IgnoreCase
                    })
                    .ToList()
            };

            // REGISTER
            _commandsById.Add(commandId, commandType);
            _descriptorsById.Add(commandId, descriptor);

            _allCommands.Add(new AcpCommandSummary
            {
                Id = commandId,
                Key = commandId,
                Name = cmdAttr.DisplayName,
                Summary = cmdAttr.Description
            });

            _logger.Trace($"[AcpCommandRegistry_RegisterCommand] Registered ACP command '{commandId}' -> '{commandType.FullName}'.");
        }

        public bool HasCommand(string commandId)
        {
            if (String.IsNullOrWhiteSpace(commandId)) return false;
            return _commandsById.ContainsKey(commandId);
        }

        public Type GetCommandType(string commandId)
        {
            if (String.IsNullOrWhiteSpace(commandId)) throw new ArgumentNullException(nameof(commandId));
            if (!_commandsById.TryGetValue(commandId, out var type))
                throw new KeyNotFoundException($"ACP Command '{commandId}' is not registered.");
            return type;
        }

        public AcpCommandDescriptor GetDescriptor(string commandId)
        {
            if (String.IsNullOrWhiteSpace(commandId)) throw new ArgumentNullException(nameof(commandId));
            if (!_descriptorsById.TryGetValue(commandId, out var desc))
                throw new KeyNotFoundException($"ACP Command descriptor '{commandId}' is not registered.");
            return desc;
        }

        public IReadOnlyDictionary<string, Type> GetRegisteredCommands()
        {
            return new ReadOnlyDictionary<string, Type>(_commandsById);
        }

        public IEnumerable<AcpCommandSummary> GetAllCommands()
        {
            return _allCommands.OrderBy(c => c.Key);
        }
    }

    public class AcpCommandSummary
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
    }

    public class AcpCommandDescriptor
    {
        public string CommandId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        public Type CommandType { get; set; }

        public string[] Triggers { get; set; }
        public bool TriggerCaseInsensitive { get; set; }

        public int MinArgs { get; set; }
        public int MaxArgs { get; set; }

        public bool RequiresConfirmation { get; set; }
        public bool ProducesSideEffects { get; set; }

        public AcpCommandPriority Priority { get; set; } = AcpCommandPriority.Normal;

        public List<AcpArgRegexRule> ArgRegexRules { get; set; } = new List<AcpArgRegexRule>();
    }

    public class AcpArgRegexRule
    {
        public int Index { get; set; }
        public string Pattern { get; set; }
        public bool IgnoreCase { get; set; }
    }
}