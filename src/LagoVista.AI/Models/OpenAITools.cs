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

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; } = new Dictionary<string, JToken>();
    }

    public sealed class JsonSchemaProperty
    {
        [JsonProperty("type")]
        public string Type { get; set; } = default!;

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string> Enum { get; set; }

        [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
        public JsonSchemaNode Items { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; } = new Dictionary<string, JToken>();
    }



    public sealed class JsonSchemaNode
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string> Enum { get; set; }

        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JsonSchemaProperty> Properties { get; set; }

        [JsonProperty("required", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string> Required { get; set; }

        [JsonProperty("additionalProperties", NullValueHandling = NullValueHandling.Ignore)]
        public bool? AdditionalProperties { get; set; }

        [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
        public JsonSchemaNode Items { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; } = new Dictionary<string, JToken>();
    }

    public sealed class JsonScheamArray
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properties")]
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

        public static void ObjectArray(
          this JsonSchemaObject schema,
          string name,
          string description,
          params JsonScheamArrayEntry[] args)
        {
            var itemProps = new Dictionary<string, JsonSchemaProperty>();
            foreach (var f in args)
            {
                itemProps[f.Name] = new JsonSchemaProperty
                {
                    Type = f.Type,
                    Description = f.Description
                };
            }

            schema.Properties[name] = new JsonSchemaProperty
            {
                Type = "array",
                Description = description,
                Items = new JsonSchemaNode
                {
                    Type = "object",
                    AdditionalProperties = false,
                    Properties = itemProps
                }
            };

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
                Items = new JsonSchemaNode { Type = "string" }
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

        public static void AddKeyword(this JsonSchemaObject schema, string keyword, JToken value)
       => schema.ExtensionData[keyword] = value;

        public static void AddKeyword(this JsonSchemaProperty prop, string keyword, JToken value)
            => prop.ExtensionData[keyword] = value;

        public static void MinItems(this JsonSchemaObject schema, string arrayPropName, int minItems)
        {
            if (!schema.Properties.TryGetValue(arrayPropName, out var prop))
                throw new InvalidOperationException($"Property '{arrayPropName}' not found.");

            prop.AddKeyword("minItems", minItems);
        }

        public static void Const(this JsonSchemaProperty prop, string value)
            => prop.AddKeyword("const", value);

        public sealed class OneOfBuilder
        {
            private readonly List<JObject> _branches = new List<JObject>();

            internal IReadOnlyList<JObject> Build() => _branches;

            public OneOfBuilder Operation(
                string op,
                IEnumerable<string> required = null,
                params (string prop, int minItems)[] minItems)
            {
                var props = new JObject
                {
                    ["operation"] = new JObject { ["const"] = op }
                };

                // Add branch-only constraints (e.g., minItems) for existing top-level props
                if (minItems != null)
                {
                    foreach (var (prop, n) in minItems)
                        props[prop] = new JObject { ["minItems"] = n };
                }

                var branch = new JObject
                {
                    ["properties"] = props
                };

                var req = (required ?? Enumerable.Empty<string>()).Distinct().ToList();
                if (!req.Contains("operation"))
                    req.Insert(0, "operation");

                branch["required"] = new JArray(req);

                _branches.Add(branch);
                return this;
            }
        }

        public static void OneOf(this JsonSchemaObject schema, Action<OneOfBuilder> build)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (build == null) throw new ArgumentNullException(nameof(build));

            var b = new OneOfBuilder();
            build(b);

            schema.ExtensionData["oneOf"] = new JArray(b.Build());
        }
    }
}
