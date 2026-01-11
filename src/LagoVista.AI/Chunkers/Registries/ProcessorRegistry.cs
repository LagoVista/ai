using LagoVista.AI.Indexing.Interfaces;
using System;
using System.Collections.Generic;

namespace LagoVista.AI.Indexing.Registries
{
    /// <summary>
    /// Case-insensitive registry mapping SubKind -> processor instance.
    ///
    /// Intended DI usage:
    /// - Register all processors (implementations) with DI.
    /// - Construct this registry with a dictionary and a default processor.
    /// </summary>
    public class ProcessorRegistry<TProcessor> : IProcessorRegistry<TProcessor>
    {
        private readonly IDictionary<string, TProcessor> _processors;

        public ProcessorRegistry(IDictionary<string, TProcessor> processors, TProcessor @default)
        {
            _processors = processors ?? throw new ArgumentNullException(nameof(processors));
            Default = @default;
        }

        public TProcessor Default { get; }

        public bool TryGet(string subKind, out TProcessor processor)
        {
            processor = default;

            if (string.IsNullOrWhiteSpace(subKind))
            {
                processor = Default;
                return processor != null;
            }

            if (_processors.TryGetValue(subKind, out processor))
            {
                return true;
            }

            processor = Default;
            return processor != null;
        }

        public TProcessor GetOrDefault(string subKind)
        {
            TryGet(subKind, out var processor);
            return processor;
        }

        public static IDictionary<string, TProcessor> CreateMap(IEnumerable<(string subKind, TProcessor processor)> items)
        {
            var dict = new Dictionary<string, TProcessor>(StringComparer.OrdinalIgnoreCase);
            if (items == null) return dict;

            foreach (var (subKind, processor) in items)
            {
                if (string.IsNullOrWhiteSpace(subKind) || processor == null) continue;
                dict[subKind] = processor;
            }

            return dict;
        }
    }
}
