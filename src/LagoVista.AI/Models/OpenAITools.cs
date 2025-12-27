using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI.Models
{
    public sealed class OpenAiToolDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("function")]
        public OpenAiFunctionDefinition Function { get; set; } = default!;
    }

    public sealed class OpenAiFunctionDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; } = default!;

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("parameters")]
        public JsonSchemaObject Parameters { get; set; } = default!;
    }

    public sealed class JsonSchemaObject
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properties")]
        public Dictionary<string, JsonSchemaProperty> Properties { get; set; } = new Dictionary<string, JsonSchemaProperty>();

        [JsonProperty("required")]
        public IReadOnlyList<string>? Required { get; set; }

        [JsonProperty("additionalProperties")]
        public bool AdditionalProperties { get; set; } = false;
    }

    public sealed class JsonSchemaProperty
    {
        [JsonProperty("type")]
        public string Type { get; set; } = default!;

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("enum")]
        public IReadOnlyList<string>? Enum { get; set; }
    }

    public static class JsonSchemaExtensions
    {
        public static void String(
            this JsonSchemaObject schema,
            string name,
            string description,
            bool required = false)
        {
            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = "string",
                Description = description
            };

            if (required)
                schema.Require(name);
        }

        public static class ToolSchema
        {
            public static OpenAiToolDefinition Function(
                string name,
                string description,
                Action<JsonSchemaObject> parameters)
            {
                var schema = new JsonSchemaObject();
                parameters(schema);

                return new OpenAiToolDefinition
                {
                    Function = new OpenAiFunctionDefinition
                    {
                        Name = name,
                        Description = description,
                        Parameters = schema
                    }
                };
            }
        }

        public static void Boolean(
            this JsonSchemaObject schema,
            string name,
            string description,
            bool required = false)
        {
            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = "boolean",
                Description = description
            };

            if (required)
                schema.Require(name);
        }

        public static void Require(this JsonSchemaObject schema, string name)
        {
            var list = schema.Required?.ToList() ?? new List<string>();
            if (!list.Contains(name))
                list.Add(name);

            schema.Required = list;
        }
    }
}
