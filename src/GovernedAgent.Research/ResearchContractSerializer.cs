using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GovernedAgent.Research;

public static class ResearchContractSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static IResearchRoot DeserializeRoot(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return DeserializeRoot(Encoding.UTF8.GetBytes(json));
    }

    public static IResearchRoot DeserializeRoot(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length > ResearchEngineeringLimits.MaximumInputSnapshotBytes)
        {
            throw new ResearchContractException(
                "engineering-limit-exceeded",
                "The serialized research record exceeds the maximum input size.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json.ToArray());
        }
        catch (JsonException exception)
        {
            throw new ResearchContractException(
                "invalid-json",
                "The research record is not valid JSON.",
                exception);
        }

        using (document)
        {
            EnsureNoDuplicateProperties(document.RootElement, "$");

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ResearchContractException(
                    "invalid-shape",
                    "A research contract root must be a JSON object.");
            }

            var schemaVersion = GetRequiredString(document.RootElement, "schemaVersion");
            if (!string.Equals(
                    schemaVersion,
                    ResearchContractVersions.SchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new ResearchContractException(
                    "unsupported-schema",
                    $"Unsupported research schema version '{schemaVersion}'.");
            }

            var recordType = GetRequiredString(document.RootElement, "recordType");
            EnforceRecordSize(recordType, utf8Json.Length);

            IResearchRoot root = recordType switch
            {
                "actor-decision" => (IResearchRoot)Deserialize<ActorDecision>(utf8Json),
                "supervisor-output" => Deserialize<SupervisorOutput>(utf8Json),
                "observation" => Deserialize<Observation>(utf8Json),
                "input-snapshot" => Deserialize<InputSnapshot>(utf8Json),
                "invocation-result" => Deserialize<InvocationResult>(utf8Json),
                "belief" => Deserialize<Belief>(utf8Json),
                "direction" => Deserialize<Direction>(utf8Json),
                "working-memory-snapshot" => Deserialize<WorkingMemorySnapshot>(utf8Json),
                "memory-update" => Deserialize<MemoryUpdate>(utf8Json),
                "reconsideration-trigger" => Deserialize<ReconsiderationTrigger>(utf8Json),
                "research-event" => Deserialize<ResearchEvent>(utf8Json),
                "run-manifest" => Deserialize<RunManifest>(utf8Json),
                "termination" => Deserialize<Termination>(utf8Json),
                "run-closure" => Deserialize<RunClosure>(utf8Json),
                _ => throw new ResearchContractException(
                    "unknown-record-type",
                    $"Unknown research record type '{recordType}'.")
            };

            var nullIssue = ResearchObjectGraphValidator.FindExplicitNull(root);
            if (nullIssue is not null)
            {
                throw new ResearchContractException(nullIssue.Code, nullIssue.Message);
            }

            ResearchContractValidator.ValidateAndThrow(root);
            return root;
        }
    }

    public static string Serialize(IResearchRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ResearchContractValidator.ValidateAndThrow(root);

        var json = root switch
        {
            ActorDecision value => JsonSerializer.Serialize(value, Options),
            SupervisorOutput value => JsonSerializer.Serialize(value, Options),
            _ => JsonSerializer.Serialize(root, root.GetType(), Options)
        };
        EnforceRecordSize(root.RecordType, Encoding.UTF8.GetByteCount(json));
        return json;
    }

    private static T Deserialize<T>(ReadOnlySpan<byte> utf8Json)
        where T : IResearchRoot
    {
        try
        {
            return JsonSerializer.Deserialize<T>(utf8Json, Options)
                ?? throw new ResearchContractException(
                    "invalid-shape",
                    $"The '{typeof(T).Name}' record cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new ResearchContractException(
                "invalid-shape",
                "The research record does not match its closed wire shape.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new ResearchContractException(
                "invalid-shape",
                "The research record does not match its discriminated wire shape.",
                exception);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowOutOfOrderMetadataProperties = true,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            WriteIndented = false
        };

        options.Converters.Add(new StrictStringEnumConverterFactory());
        return options;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw new ResearchContractException(
                "missing-field",
                $"Required string field '{propertyName}' is missing.");
        }

        return property.GetString()!;
    }

    private static void EnforceRecordSize(string recordType, int byteCount)
    {
        var maximum = recordType switch
        {
            "actor-decision" or "supervisor-output" =>
                ResearchEngineeringLimits.MaximumComponentOutputBytes,
            "input-snapshot" => ResearchEngineeringLimits.MaximumInputSnapshotBytes,
            "research-event" => ResearchEngineeringLimits.MaximumEventBytes,
            _ => ResearchEngineeringLimits.MaximumInputSnapshotBytes
        };

        if (byteCount > maximum)
        {
            throw new ResearchContractException(
                "engineering-limit-exceeded",
                $"The '{recordType}' record exceeds its approved byte limit of {maximum}.");
        }
    }

    private static void EnsureNoDuplicateProperties(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new ResearchContractException(
                            "duplicate-property",
                            $"Duplicate property '{property.Name}' at '{path}'.");
                    }

                    EnsureNoDuplicateProperties(
                        property.Value,
                        $"{path}.{property.Name}");
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    EnsureNoDuplicateProperties(item, $"{path}[{index}]");
                    index++;
                }

                break;
        }
    }
}

public sealed class ResearchContractException : Exception
{
    public ResearchContractException(
        string code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

internal sealed class StrictStringEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var converterType = typeof(StrictStringEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class StrictStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, TEnum> FromWire =
        Enum.GetValues<TEnum>().ToDictionary(ToWireName, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<TEnum, string> ToWire =
        FromWire.ToDictionary(pair => pair.Value, pair => pair.Key);

    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            !FromWire.TryGetValue(reader.GetString()!, out var value))
        {
            throw new JsonException(
                $"Expected an exact string value for enum '{typeof(TEnum).Name}'.");
        }

        return value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        if (!ToWire.TryGetValue(value, out var wireName))
        {
            throw new JsonException(
                $"Unsupported enum value '{value}' for '{typeof(TEnum).Name}'.");
        }

        writer.WriteStringValue(wireName);
    }

    private static string ToWireName(TEnum value)
    {
        var name = Enum.GetName(value)
            ?? throw new InvalidOperationException(
                $"Unnamed enum value '{value}' is not supported.");
        var field = typeof(TEnum).GetField(name)!;
        var explicitName = field
            .GetCustomAttributes(typeof(JsonStringEnumMemberNameAttribute), false)
            .Cast<JsonStringEnumMemberNameAttribute>()
            .SingleOrDefault();
        return explicitName?.Name ?? JsonNamingPolicy.KebabCaseLower.ConvertName(name);
    }
}
