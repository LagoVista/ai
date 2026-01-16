using LagoVista.Core.Attributes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LagoVista.AI
{
    public  static class ToolHelpers
    {
        public static bool IsEntityHeaderOfEnum(this Type t, out Type enumType)
        {
            enumType = null;
            if (t == null || !t.IsGenericType) return false;

            var def = t.GetGenericTypeDefinition();
            if (def.Name != "EntityHeader`1") return false;

            enumType = t.GenericTypeArguments[0];
            return enumType.IsEnum;
        }


        public static object ParseEnumValue(this Type enumType, JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            // If numeric, deserialize directly
            if (token.Type == JTokenType.Integer)
                return Enum.ToObject(enumType, token.Value<int>());

            var raw = token.ToString()?.Trim();
            if (string.IsNullOrEmpty(raw))
                return null;

            // First try direct parse (e.g., "Moderate", "moderate")
            if (Enum.TryParse(enumType, raw, ignoreCase: true, out var parsed))
                return parsed;

            // Next try kebab-case -> PascalCase (e.g., "very-high" -> "VeryHigh")
            var pascal = string.Concat(
                raw.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1))
            );

            if (Enum.TryParse(enumType, pascal, ignoreCase: true, out parsed))
                return parsed;

            throw new InvalidOperationException($"Could not parse '{raw}' into enum '{enumType.Name}'.");
        }

        public static string GetEnumStableValue(this Type enumType, object enumValue, Type owningEntityType)
        {
            var member = enumType.GetMember(enumValue.ToString()).FirstOrDefault();
            var enumLabel = member?.GetCustomAttribute<EnumLabelAttribute>();
            if (enumLabel == null)
                return enumValue.ToString().ToLowerInvariant();

            // enumLabel.Key is the string constant name on owning entity (per your pattern)
            var constName = enumLabel.Key; // e.g., "Persona_RiskToleranceLevels_VeryHigh"
            var field = owningEntityType.GetField(constName, BindingFlags.Public | BindingFlags.Static);
            if (field?.FieldType == typeof(string))
                return (string)field.GetValue(null);

            // fallback
            return enumValue.ToString().ToLowerInvariant();
        }

        public static string GetEnumDisplayText(this Type enumType, object enumValue)
        {
            var member = enumType.GetMember(enumValue.ToString()).FirstOrDefault();
            var enumLabel = member?.GetCustomAttribute<EnumLabelAttribute>();
            if (enumLabel == null)
                return enumValue.ToString();

            // EnumDescription.Create already does localization in your stack,
            // but we don’t have it here. So best-effort:
            // - If you can call EnumDescription.Create(enumLabel, value, idx) do that instead.
            return enumValue.ToString();
        }

        public static object BuildEntityHeaderOfEnum(this Type propType, JToken valueToken, Type owningEntityType)
        {
            if (valueToken == null || valueToken.Type == JTokenType.Null)
                return null;

            if (!propType.IsEntityHeaderOfEnum(out var enumType))
                throw new InvalidOperationException($"Type '{propType.Name}' is not EntityHeader<Enum>.");

            object enumValue = null;

            if (valueToken.Type == JTokenType.Object)
            {
                var obj = (JObject)valueToken;

                if (obj.TryGetValue("values", StringComparison.OrdinalIgnoreCase, out var valuesTok) ||
                    obj.TryGetValue("value", StringComparison.OrdinalIgnoreCase, out valuesTok))
                {
                    enumValue = enumType.ParseEnumValue(valuesTok);
                }
                else if (obj.TryGetValue("id", StringComparison.OrdinalIgnoreCase, out var idTok) ||
                         obj.TryGetValue("key", StringComparison.OrdinalIgnoreCase, out idTok))
                {
                    enumValue = enumType.ParseEnumValue(idTok);
                }
            }
            else
            {
                enumValue = enumType.ParseEnumValue(valueToken);
            }

            Console.WriteLine("enum value: " + enumValue);


            if (enumValue == null)
                return null;

            var createMethod = propType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Create" &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == enumType);

            if (createMethod == null)
                throw new InvalidOperationException($"EntityHeader<{enumType.Name}> does not have Create({enumType.Name}).");

            var header = createMethod.Invoke(null, new[] { enumValue });

            //owningEntityType ??= enumType; // last-ditch fallback
            //var stable = enumType.GetEnumStableValue(enumValue, owningEntityType);

            //Console.WriteLine("Stable:" + stable);

            //propType.GetProperty("Id")?.SetValue(header, stable);
            //propType.GetProperty("Key")?.SetValue(header, stable);

            //var display = enumType.GetEnumDisplayText(enumValue);

            //Console.WriteLine(display);

            //propType.GetProperty("Text")?.SetValue(header, display);

            //propType.GetProperty("Values")?.SetValue(header, enumValue);

            return header;
        }

    }
}
