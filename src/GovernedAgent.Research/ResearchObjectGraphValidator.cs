using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace GovernedAgent.Research;

internal static class ResearchObjectGraphValidator
{
    private static readonly NullabilityInfoContext Nullability = new();

    public static ResearchValidationIssue? FindExplicitNull(object root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return Visit(root, "$", null, visited);
    }

    private static ResearchValidationIssue? Visit(
        object? value,
        string path,
        NullabilityInfo? nullability,
        ISet<object> visited)
    {
        if (value is null)
        {
            return nullability?.ReadState == NullabilityState.NotNull
                ? new ResearchValidationIssue(
                    "invalid-shape",
                    $"Non-nullable contract value '{path}' cannot be null.")
                : null;
        }

        var type = value.GetType();
        if (type.IsValueType || value is string || value is JsonElement)
        {
            return null;
        }

        if (!visited.Add(value))
        {
            return null;
        }

        if (value is IDictionary dictionary)
        {
            var valueNullability = nullability?.GenericTypeArguments.ElementAtOrDefault(1);
            foreach (DictionaryEntry entry in dictionary)
            {
                var issue = Visit(
                    entry.Value,
                    $"{path}[{entry.Key}]",
                    valueNullability,
                    visited);
                if (issue is not null)
                {
                    return issue;
                }
            }

            return null;
        }

        if (value is IEnumerable enumerable)
        {
            var elementNullability = nullability?.GenericTypeArguments.FirstOrDefault();
            var index = 0;
            foreach (var item in enumerable)
            {
                var issue = Visit(item, $"{path}[{index}]", elementNullability, visited);
                if (issue is not null)
                {
                    return issue;
                }

                index++;
            }

            return null;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var propertyNullability = Nullability.Create(property);
            var issue = Visit(
                property.GetValue(value),
                $"{path}.{property.Name}",
                propertyNullability,
                visited);
            if (issue is not null)
            {
                return issue;
            }
        }

        return null;
    }
}
