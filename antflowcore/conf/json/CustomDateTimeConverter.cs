﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace antflowcore.conf.json;

public class CustomDateTimeConverter:JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.ParseExact(reader.GetString() ?? string.Empty, "yyyy-MM-dd HH:mm:ss", null);
    }
 
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}