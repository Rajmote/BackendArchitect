namespace BackendArchitect.Apis.Grpc.Contracts;

public enum ProtoType { Int32, String, Double, Bool }

/// <summary>One field in a .proto message. The NUMBER is the contract; the name is local to your code.</summary>
public sealed record ProtoField(int Number, string Name, ProtoType Type);

// A simplified protobuf message schema.
//
// The whole point of this model: when a message is serialised, only FIELD NUMBERS and values go on the
// wire — never the names. That single fact inverts the JSON intuition:
//
//   JSON     -> the NAME is the contract   => renaming is breaking
//   protobuf -> the NUMBER is the contract => renaming is FREE, renumbering is breaking
public sealed class ProtoSchema
{
    private readonly Dictionary<int, ProtoField> _byNumber;

    public ProtoSchema(string messageName, IEnumerable<ProtoField> fields, IEnumerable<int>? reserved = null)
    {
        MessageName = messageName;
        _byNumber = fields.ToDictionary(f => f.Number);
        Reserved = new HashSet<int>(reserved ?? []);

        var clash = _byNumber.Keys.FirstOrDefault(Reserved.Contains);
        if (clash != 0)
            throw new InvalidOperationException($"field number {clash} is reserved and must never be reused");
    }

    public string MessageName { get; }

    /// <summary>Numbers retired by deleted fields. The compiler must refuse to reuse them.</summary>
    public IReadOnlySet<int> Reserved { get; }

    public IReadOnlyCollection<ProtoField> Fields => _byNumber.Values;

    public ProtoField? FieldNumbered(int number) => _byNumber.GetValueOrDefault(number);

    public ProtoField? FieldNamed(string name) =>
        _byNumber.Values.FirstOrDefault(f => f.Name == name);
}

/// <summary>What actually travels: field number -> value. No names, anywhere.</summary>
public sealed class WireMessage
{
    private readonly Dictionary<int, object> _values = [];

    public IReadOnlyDictionary<int, object> Values => _values;

    public void Set(int fieldNumber, object value) => _values[fieldNumber] = value;

    /// <summary>Rough size: a tag byte per field plus the payload — no field names, no punctuation.</summary>
    public int ApproximateBytes() =>
        _values.Sum(pair => 1 + pair.Value switch
        {
            int => 4,
            double => 8,
            bool => 1,
            string s => s.Length,
            _ => 8,
        });
}
