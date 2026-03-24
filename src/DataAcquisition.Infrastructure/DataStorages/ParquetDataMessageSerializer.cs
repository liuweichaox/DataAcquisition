using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DataAcquisition.Domain.Models;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
///     DataMessage 的 Parquet 序列化/反序列化器。
/// </summary>
internal static class ParquetDataMessageSerializer
{
    private static readonly ParquetSchema Schema = new(
        new DateTimeDataField("timestamp", DateTimeFormat.DateAndTime),
        new DataField<string>("measurement"),
        new DataField<string>("plc_code"),
        new DataField<string>("channel_code"),
        new DataField<string>("cycle_id"),
        new DataField<string>("event_type"),
        new DataField<string>("diagnostic_type"),
        new DataField<string>("data_json"));

    public static async Task WriteAsync(Stream stream, IReadOnlyList<DataMessage> dataMessages)
    {
        if (dataMessages.Count == 0)
            return;

        await using var writer = await ParquetWriter.CreateAsync(Schema, stream, append: false).ConfigureAwait(false);
        writer.CompressionMethod = CompressionMethod.Snappy;
        using var rowGroup = writer.CreateRowGroup();
        await WriteColumnsAsync(rowGroup, dataMessages).ConfigureAwait(false);
    }

    public static async Task<List<DataMessage>> ReadAsync(Stream stream)
    {
        var messages = new List<DataMessage>();

        using var reader = await ParquetReader.CreateAsync(stream).ConfigureAwait(false);
        if (reader.RowGroupCount == 0)
            return messages;

        for (var i = 0; i < reader.RowGroupCount; i++)
        {
            using var rowGroupReader = reader.OpenRowGroupReader(i);
            var columns = await ReadColumnsAsync(rowGroupReader).ConfigureAwait(false);
            messages.AddRange(BuildMessages(columns));
        }

        return messages;
    }

    private static async Task WriteColumnsAsync(dynamic rowGroupWriter, IReadOnlyList<DataMessage> dataMessages)
    {
        var timestamps = dataMessages.Select(x => x.Timestamp.UtcDateTime).ToArray();
        var measurements = dataMessages.Select(x => x.Measurement).ToArray();
        var plcCodes = dataMessages.Select(x => x.PlcCode).ToArray();
        var channelCodes = dataMessages.Select(x => x.ChannelCode).ToArray();
        var cycleIds = dataMessages.Select(x => x.CycleId).ToArray();
        var eventTypes = dataMessages.Select(x => x.EventType?.ToString() ?? string.Empty).ToArray();
        var diagnosticTypes = dataMessages.Select(x => x.DiagnosticType?.ToString() ?? string.Empty).ToArray();
        var dataJsons = dataMessages.Select(x =>
            JsonSerializer.Serialize((IDictionary<string, object?>)x.DataValues)).ToArray();

        await rowGroupWriter.WriteColumnAsync(new DataColumn((DateTimeDataField)Schema.DataFields[0], timestamps))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)Schema.DataFields[1], measurements))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)Schema.DataFields[2], plcCodes))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)Schema.DataFields[3], channelCodes))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)Schema.DataFields[4], cycleIds))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)Schema.DataFields[5], eventTypes))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)Schema.DataFields[6], diagnosticTypes))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)Schema.DataFields[7], dataJsons))
            .ConfigureAwait(false);
    }

    private static async Task<ParquetDataColumns> ReadColumnsAsync(dynamic rowGroupReader)
    {
        var timestampColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[0]).ConfigureAwait(false);
        var measurementColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[1]).ConfigureAwait(false);
        var plcCodeColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[2]).ConfigureAwait(false);
        var channelCodeColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[3]).ConfigureAwait(false);
        var cycleIdColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[4]).ConfigureAwait(false);
        var eventTypeColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[5]).ConfigureAwait(false);
        var diagnosticTypeColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[6]).ConfigureAwait(false);
        var dataJsonColumn = (DataColumn)await rowGroupReader.ReadColumnAsync(Schema.DataFields[7]).ConfigureAwait(false);

        return new ParquetDataColumns(
            ReadColumnData<DateTime>(timestampColumn),
            ReadColumnData<string>(measurementColumn),
            ReadColumnData<string>(plcCodeColumn),
            ReadColumnData<string>(channelCodeColumn),
            ReadColumnData<string>(cycleIdColumn),
            ReadColumnData<string>(eventTypeColumn),
            ReadColumnData<string>(diagnosticTypeColumn),
            ReadColumnData<string>(dataJsonColumn));
    }

    private static T[] ReadColumnData<T>(DataColumn column)
    {
        return column.Data switch
        {
            T[] typed => typed,
            Array array => array.Cast<object?>().Select(value => value is null ? default! : (T)value).ToArray(),
            _ => throw new InvalidOperationException($"无法读取 Parquet 列数据: {column.Field.Name}")
        };
    }

    private static IEnumerable<DataMessage> BuildMessages(ParquetDataColumns columns)
    {
        for (var row = 0; row < columns.Timestamps.Length; row++)
        {
            var timestamp = new DateTimeOffset(DateTime.SpecifyKind(columns.Timestamps[row], DateTimeKind.Utc));
            var hasDiagnostic = !string.IsNullOrWhiteSpace(columns.DiagnosticTypes[row]);
            var message = hasDiagnostic
                ? DataMessage.CreateDiagnostic(
                    columns.CycleIds[row],
                    columns.Measurements[row],
                    columns.PlcCodes[row],
                    columns.ChannelCodes[row],
                    Enum.Parse<DiagnosticEventType>(columns.DiagnosticTypes[row]),
                    timestamp)
                : DataMessage.Create(
                    columns.CycleIds[row],
                    columns.Measurements[row],
                    columns.PlcCodes[row],
                    columns.ChannelCodes[row],
                    Enum.Parse<EventType>(columns.EventTypes[row]),
                    timestamp);

            var dataValues = JsonSerializer.Deserialize<Dictionary<string, object?>>(columns.DataJsons[row]);
            if (dataValues != null)
            {
                foreach (var kv in dataValues)
                    message.AddDataValue(kv.Key, kv.Value);
            }

            yield return message;
        }
    }

    private sealed record ParquetDataColumns(
        DateTime[] Timestamps,
        string[] Measurements,
        string[] PlcCodes,
        string[] ChannelCodes,
        string[] CycleIds,
        string[] EventTypes,
        string[] DiagnosticTypes,
        string[] DataJsons);
}
