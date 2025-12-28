using LagoVista.AI.Models;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LagoVista.AI
{
    public class AvailableTool
    {
        public string Name { get; set; }
        public string Summary { get; set; }
    }

    /// <summary>
    /// Active Tool is a Tool that the LLM 
    /// can run in it's current request
    /// </summary>
    public class ActiveTool
    {
        public string Name { get; set; }
        public string ToolUsageMetaData { get; set; }
    
        public OpenAiToolDefinition Schama { get; set; }
    }

    public sealed class OpenAiToolDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("name")]
        public string Name { get; set; } = default!;
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("parameters")]
        public JsonSchemaObject Parameters { get; set; } = default!;

        [JsonProperty("strict")]
        public bool Strict { get; } = false;
    }

    public sealed class JsonSchemaObject
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properties")]
        public Dictionary<string, JsonSchemaProperty> Properties { get; set; } = new Dictionary<string, JsonSchemaProperty>();

        [JsonProperty("required")]
        public IReadOnlyList<string> Required { get; set; } = new List<string>();

        [JsonProperty("additionalProperties")]
        public bool AdditionalProperties { get; set; } = false;
    }

    public sealed class JsonSchemaProperty
    {
        [JsonProperty("type")]
        public string Type { get; set; } = default!;

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enum", NullValueHandling=NullValueHandling.Ignore)]
        public IReadOnlyList<string> Enum { get; set; }

        [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
        public JsonScheamArray Items { get; set; }
    }


    public sealed class JsonScheamArray
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properites")]
        public List<JObject> Properties { get; set; } = new List<JObject>();
    }


    public sealed class JsonScheamArrayEntry
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
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
                Name = name,
                Description = description,
                Parameters = schema
            };
        }
    }


    public static class JsonSchemaExtensions
    {
        public static void String(
            this JsonSchemaObject schema,
            string name,
            string description,
            IReadOnlyList<string> enumValues = null,
            bool required = false)
        {
            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = "string",
                Description = description,
                Enum = enumValues
            };

            if (required)
                schema.Require(name);
        }


        /// <summary>
        /// Escape hatch for types not yet modeled with dedicated helpers (array, object, integer, etc.).
        /// </summary>
        public static void Any(
            this JsonSchemaObject schema,
            string name,
            string type,
            string description,
            IReadOnlyList<string> enumValues = null,
            bool required = false)
        {
            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = type,
                Description = description,
                Enum = enumValues
            };

            if (required)
                schema.Require(name);
        }

        public static void Array(
          this JsonSchemaObject schema,
          string name,
          string description,
          params JsonScheamArrayEntry[] args)
        {
            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = "array",
                Description = description,
                Items = new JsonScheamArray()
            };

            foreach(var arg in args)
            {
                schema.Properties[name].Items.Properties.Add(new JObject
                {
                    [arg.Name] = new JObject
                    {
                        ["type"] = arg.Type,
                        ["description"] = arg.Description
                    }
                });
            }

            schema.Require(name);
        }

        public static void StringArray(
         this JsonSchemaObject schema,
         string name,
         string description,
         params JsonScheamArrayEntry[] args)
        {
            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = "array",
                Description = description,
                Items = new JsonScheamArray() { Type = "string" }
            };

            //foreach (var arg in args)
            //{
            //    schema.Properties[name].Items.Properties.Add(new JObject
            //    {
            //        [arg.Name] = new JObject
            //        {
            //            ["type"] = arg.Type,
            //            ["description"] = arg.Description
            //        }
            //    });
            //}

            schema.Require(name);
        }

        public static void Integer(
           this JsonSchemaObject schema,
           string name,
           string description,
           bool required = false)
        {
            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = "integer",
                Description = description
            };

            if (required)
                schema.Require(name);
        }

        public static void Number(
            this JsonSchemaObject schema,
            string name,
            string description,
            bool required = false)
                {
                    schema.Properties[name] = new JsonSchemaProperty
                    {
                        Type = "number",
                        Description = description
                    };

                    if (required)
                        schema.Require(name);
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
