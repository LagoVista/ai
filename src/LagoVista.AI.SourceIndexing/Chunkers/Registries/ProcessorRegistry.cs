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
    public class ProcessorRegistry<TKey, TProcessor> : IProcessorRegistry<TKey, TProcessor>
    {
        private readonly IDictionary<TKey, TProcessor> _processors;

        public ProcessorRegistry(IDictionary<TKey, TProcessor> processors, TProcessor @default)
        {
            _processors = processors ?? throw new ArgumentNullException(nameof(processors));
            Default = @default;
        }

        public TProcessor Default { get; }

        public bool TryGet(TKey subKind, out TProcessor processor)
        {
            processor = default;

            if (_processors.TryGetValue(subKind, out processor))
            {
                return true;
            }

            processor = Default;
            return processor != null;
        }

        public TProcessor GetOrDefault(TKey subKind)
        {
            TryGet(subKind, out var processor);
            return processor;
        }

        public static IDictionary<string, TProcessor> CreateMap(IEnumerable<(TKey key, TProcessor processor)> items)
        {
            var dict = new Dictionary<string, TProcessor>(StringComparer.OrdinalIgnoreCase);
            if (items == null) return dict;

            dict.Add(dict.Keys.ToString(), default);

            return dict;
        }
    }
}
