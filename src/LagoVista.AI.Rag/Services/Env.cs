using System;

namespace LagoVista.AI.Rag.Services
{
    public static class Env
    {
        public static string Get(string name, string fallback = null)
        {
            var val = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(val))
            {
                if (string.IsNullOrWhiteSpace(fallback))
                    throw new InvalidOperationException($"Environment variable '{name}' is not set and no fallback was provided.");
                return fallback!;
            }
            return val;
        }
    }
}
