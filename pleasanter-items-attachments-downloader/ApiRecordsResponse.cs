using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

internal class ApiRecordsResponse
{
    public RecordsResponse Response { get; set; } = new RecordsResponse();
}

internal class RecordsResponse
{
    public List<RecordData> Data { get; set; } = new List<RecordData>();
    public long PageSize { get; set; }
    public long TotalCount { get; set; }
    public long Offset { get; set; }
}

internal class RecordData
{
    public long? ResultId { get; set; }
    public long? IssueId { get; set; }
    public long? ReferenceId => ResultId ?? IssueId;
    public string ItemTitle { get; set; }
    public string ItemTitleFormated => Regex.Replace(ItemTitle ?? "", $"[{string.Join("", Path.GetInvalidFileNameChars())}]", "_");
    public Dictionary<string, List<RecordAttachment>> AttachmentsHash { get; set; } = new Dictionary<string, List<RecordAttachment>>();
    public Dictionary<string, string> DescriptionHash { get; set; } = new Dictionary<string, string>();
    public string Body { get; set; }
    [JsonConverter(typeof(JsonJsonConverter<List<RecordComment>>))]
    public List<RecordComment> Comments { get; set; } = new List<RecordComment>();
}

internal class RecordAttachment
{
    public string Guid { get; set; }
    public string Name { get; set; }
    public string NameFormated => Regex.Replace(Name ?? "", $"[{string.Join("", Path.GetInvalidFileNameChars())}]", "_");
    public string Size { get; set; }
    public string HashCode { get; set; }
}

internal class RecordComment
{
    public long CommentId { get; set; }
    public string Body { get; set; }
}

internal class JsonJsonConverter<T> : JsonConverter where T : class, new()
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(T);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, JsonConvert.SerializeObject(value));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return default(T);
        }

        string jsonString = (string)reader.Value;

        if (string.IsNullOrEmpty(jsonString))
        {
            return default(T);
        }

        return JsonConvert.DeserializeObject<T>(jsonString) ?? new T();
    }
}
