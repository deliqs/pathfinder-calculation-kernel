using System.Text.Json;
using System.Text.RegularExpressions;

namespace PathFinder.AccuracyBenchmark.Tests;

internal static class TestJsonSchemaValidator
{
    internal static void Validate(JsonElement instance, JsonElement schema) =>
        Validate(instance, schema, schema, "$" );

    private static void Validate(
        JsonElement instance,
        JsonElement schema,
        JsonElement root,
        string location)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            Validate(instance, Resolve(root, reference.GetString()!), root, location);
            return;
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(instance, constant))
        {
            throw new InvalidDataException($"{location} differs from schema const {constant}.");
        }

        if (schema.TryGetProperty("enum", out var allowed) &&
            !allowed.EnumerateArray().Any(value => JsonElement.DeepEquals(instance, value)))
        {
            throw new InvalidDataException($"{location} is outside the schema enum.");
        }

        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(value => value.GetString()!).ToArray()
                : [type.GetString()!];
            if (!types.Any(value => MatchesType(instance, value)))
            {
                throw new InvalidDataException($"{location} does not match schema type {type}.");
            }
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(instance, schema, root, location);
        }
        else if (instance.ValueKind == JsonValueKind.Array &&
                 schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var value in instance.EnumerateArray())
            {
                Validate(value, items, root, $"{location}[{index++}]");
            }
        }
        else if (instance.ValueKind == JsonValueKind.String &&
                 schema.TryGetProperty("pattern", out var pattern) &&
                 !Regex.IsMatch(instance.GetString()!, pattern.GetString()!, RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException($"{location} does not match its schema pattern.");
        }

        if (instance.ValueKind == JsonValueKind.Number)
        {
            ValidateNumber(instance, schema, location);
        }
    }

    private static void ValidateObject(
        JsonElement instance,
        JsonElement schema,
        JsonElement root,
        string location)
    {
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray().Select(value => value.GetString()!))
            {
                if (!instance.TryGetProperty(name, out _))
                {
                    throw new InvalidDataException($"{location} is missing required property {name}.");
                }
            }
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            return;
        }

        foreach (var property in instance.EnumerateObject())
        {
            if (!properties.TryGetProperty(property.Name, out var propertySchema))
            {
                if (schema.TryGetProperty("additionalProperties", out var additional) &&
                    additional.ValueKind == JsonValueKind.False)
                {
                    throw new InvalidDataException($"{location} contains unknown property {property.Name}.");
                }

                continue;
            }

            Validate(property.Value, propertySchema, root, $"{location}.{property.Name}");
        }
    }

    private static void ValidateNumber(JsonElement instance, JsonElement schema, string location)
    {
        var value = instance.GetDouble();
        if (schema.TryGetProperty("minimum", out var minimum) && value < minimum.GetDouble())
        {
            throw new InvalidDataException($"{location} is below its schema minimum.");
        }

        if (schema.TryGetProperty("maximum", out var maximum) && value > maximum.GetDouble())
        {
            throw new InvalidDataException($"{location} is above its schema maximum.");
        }

        if (schema.TryGetProperty("multipleOf", out var multipleOf) &&
            instance.GetDecimal() % multipleOf.GetDecimal() != 0)
        {
            throw new InvalidDataException($"{location} is not a multiple of its schema precision.");
        }
    }

    private static bool MatchesType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number &&
                     value.TryGetInt64(out _),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => throw new InvalidDataException($"Unsupported test schema type: {type}")
    };

    private static JsonElement Resolve(JsonElement root, string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported test schema reference: {reference}");
        }

        var current = root;
        foreach (var segment in reference[2..].Split('/'))
        {
            current = current.GetProperty(segment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal));
        }

        return current;
    }
}
