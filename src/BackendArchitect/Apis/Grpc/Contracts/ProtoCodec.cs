using System.Globalization;
using System.Text.Json;

namespace BackendArchitect.Apis.Grpc.Contracts;

// Serialises by NUMBER and deserialises by NUMBER — which is exactly why a rename costs nothing and a
// renumber is fatal. Also lets us compare the wire size against the equivalent JSON.
public static class ProtoCodec
{
    /// <summary>Write values (given by field name in the writer's own schema) onto the wire by number.</summary>
    public static WireMessage Serialize(ProtoSchema schema, IDictionary<string, object> values)
    {
        var wire = new WireMessage();
        foreach (var (name, value) in values)
        {
            var field = schema.FieldNamed(name)
                        ?? throw new InvalidOperationException($"'{name}' is not in {schema.MessageName}");
            wire.Set(field.Number, value);
        }

        return wire;
    }

    /// <summary>
    /// Read the wire using the READER's schema. Unknown field numbers are ignored (that's why adding
    /// fields is backward compatible), and known numbers are surfaced under the reader's own names.
    /// </summary>
    public static Dictionary<string, object> Deserialize(ProtoSchema schema, WireMessage wire)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (number, value) in wire.Values)
        {
            var field = schema.FieldNumbered(number);
            if (field is null)
                continue;                      // unknown number -> ignored, not an error

            result[field.Name] = value;
        }

        return result;
    }

    /// <summary>The equivalent JSON, for a size comparison: names and punctuation travel too.</summary>
    public static string ToJson(ProtoSchema schema, WireMessage wire)
    {
        var named = Deserialize(schema, wire);
        return JsonSerializer.Serialize(named);
    }

    public static string FormatValue(object value) =>
        value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value.ToString() ?? "";
}
