// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 6485a20fee374602b2022f9e3b455b9c8f7524faf6b664442055097bdfac3f61
// IndexVersion: 2
// --- END CODE INDEX META ---
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
