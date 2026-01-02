using System;
using System.IO;
using System.Text.Json;

namespace LagoVista.AI.Aptix.Cli
{
    public static class AptixConfigLoader
    {
        public static AptixConfig Load(string path = "aptix.config.json")
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Aptix workspace not found in: {Directory.GetCurrentDirectory()}");
                Console.WriteLine("This directory does not contain an aptix.config.json file.");
                Console.WriteLine("Please run Aptix from the root of an Aptix workspace.");
                return null;
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AptixConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                Console.WriteLine("Unable to parse aptix.config.json in this workspace.");
                return null;
            }

            if (String.IsNullOrWhiteSpace(config.AgentContextId) ||
                String.IsNullOrWhiteSpace(config.RoleId))
            {
                Console.WriteLine("Aptix config is missing 'agentContextId' or 'roleId'.");
                return null;
            }

            if (String.IsNullOrWhiteSpace(config.ClientAppId))
            {
                Console.WriteLine("Aptix config is missing 'clientAppId'.");
                return null;
            }

            return config;
        }
    }
}
