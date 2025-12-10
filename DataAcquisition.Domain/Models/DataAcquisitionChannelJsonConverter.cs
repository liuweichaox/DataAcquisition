using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataAcquisition.Domain.Models;

/// <summary>
/// 数据采集通道 JSON 转换器
/// 仅支持 ConditionalAcquisition 命名，统一命名风格
/// </summary>
public class DataAcquisitionChannelJsonConverter : JsonConverter<DataAcquisitionChannel>
{
    public override DataAcquisitionChannel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var channel = new DataAcquisitionChannel();
        ConditionalAcquisition? conditionalAcquisition = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();

            reader.Read();

            switch (propertyName)
            {
                case "ConditionalAcquisition":
                    // 仅支持 ConditionalAcquisition 命名
                    conditionalAcquisition = JsonSerializer.Deserialize<ConditionalAcquisition>(ref reader, options);
                    break;
                case "EnableBatchRead":
                    channel.EnableBatchRead = reader.GetBoolean();
                    break;
                case "BatchReadRegister":
                    channel.BatchReadRegister = reader.GetString() ?? string.Empty;
                    break;
                case "BatchReadLength":
                    channel.BatchReadLength = reader.GetUInt16();
                    break;
                case "TableName":
                case "Measurement":
                    channel.Measurement = reader.GetString() ?? string.Empty;
                    break;
                case "BatchSize":
                    channel.BatchSize = reader.GetInt32();
                    break;
                case "AcquisitionInterval":
                    channel.AcquisitionInterval = reader.GetInt32();
                    break;
                case "DataPoints":
                    channel.DataPoints = JsonSerializer.Deserialize<List<DataPoint>>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        // 将 conditionalAcquisition 赋值给 ConditionalAcquisition 属性
        channel.ConditionalAcquisition = conditionalAcquisition;

        return channel;
    }

    public override void Write(Utf8JsonWriter writer, DataAcquisitionChannel value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // 序列化时使用 ConditionalAcquisition
        if (value.ConditionalAcquisition != null)
        {
            writer.WritePropertyName("ConditionalAcquisition");
            JsonSerializer.Serialize(writer, value.ConditionalAcquisition, options);
        }

        writer.WriteBoolean("EnableBatchRead", value.EnableBatchRead);
        writer.WriteString("BatchReadRegister", value.BatchReadRegister);
        writer.WriteNumber("BatchReadLength", value.BatchReadLength);
        writer.WriteString("Measurement", value.Measurement);
        writer.WriteNumber("BatchSize", value.BatchSize);
        writer.WriteNumber("AcquisitionInterval", value.AcquisitionInterval);

        if (value.DataPoints != null)
        {
            writer.WritePropertyName("DataPoints");
            JsonSerializer.Serialize(writer, value.DataPoints, options);
        }

        writer.WriteEndObject();
    }
}
