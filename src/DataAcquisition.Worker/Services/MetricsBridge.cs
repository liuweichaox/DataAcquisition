using System.Diagnostics.Metrics;
using Prometheus;

namespace DataAcquisition.Worker.Services;

/// <summary>
///     将 System.Diagnostics.Metrics 的指标桥接到 Prometheus
/// </summary>
public class MetricsBridge : IDisposable
{
    private readonly Dictionary<string, ICollector<Counter.Child>> _counters = new();
    private readonly Dictionary<string, ICollector<Histogram.Child>> _histograms = new();
    private readonly MeterListener _listener;
    private readonly object _lock = new();

    public MetricsBridge()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "DataAcquisition") listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        _listener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);

        _listener.Start();
    }

    public void Dispose()
    {
        _listener?.Dispose();
    }

    private void OnMeasurementRecorded<T>(Instrument instrument, T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        where T : struct
    {
        lock (_lock)
        {
            var metricName = SanitizeMetricName(instrument.Name);
            var labelNames = ExtractLabelNames(tags);
            var labelValues = ExtractLabelValues(tags);

            if (instrument is Histogram<double> || instrument is Histogram<int> || instrument is Histogram<long>)
            {
                var histogram = GetOrCreateHistogram(metricName, instrument.Description ?? "", labelNames);
                if (measurement is double d)
                    histogram.WithLabels(labelValues).Observe(d);
                else if (measurement is int i)
                    histogram.WithLabels(labelValues).Observe(i);
                else if (measurement is long l)
                    histogram.WithLabels(labelValues).Observe(l);
            }
            else if (instrument is Counter<double> || instrument is Counter<int> || instrument is Counter<long>)
            {
                var counter = GetOrCreateCounter(metricName, instrument.Description ?? "", labelNames);
                if (measurement is double d2)
                    counter.WithLabels(labelValues).Inc(d2);
                else if (measurement is int i2)
                    counter.WithLabels(labelValues).Inc(i2);
                else if (measurement is long l2)
                    counter.WithLabels(labelValues).Inc(l2);
            }
        }
    }

    private Histogram GetOrCreateHistogram(string name, string help, string[] labelNames)
    {
        var key = $"{name}_{string.Join("_", labelNames)}";
        if (!_histograms.TryGetValue(key, out var collector))
        {
            var histogram = Metrics.CreateHistogram(name, help, new HistogramConfiguration
            {
                LabelNames = labelNames
            });
            _histograms[key] = histogram;
            return histogram;
        }

        return (Histogram)collector;
    }

    private Counter GetOrCreateCounter(string name, string help, string[] labelNames)
    {
        var key = $"{name}_{string.Join("_", labelNames)}";
        if (!_counters.TryGetValue(key, out var collector))
        {
            var counter = Metrics.CreateCounter(name, help, new CounterConfiguration
            {
                LabelNames = labelNames
            });
            _counters[key] = counter;
            return counter;
        }

        return (Counter)collector;
    }

    private string SanitizeMetricName(string name)
    {
        // Prometheus 指标名称只能包含字母、数字、下划线和冒号
        return name.Replace(".", "_").Replace("-", "_");
    }

    private string[] ExtractLabelNames(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var names = new List<string>();
        foreach (var tag in tags) names.Add(SanitizeMetricName(tag.Key));
        return names.ToArray();
    }

    private string[] ExtractLabelValues(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var values = new List<string>();
        foreach (var tag in tags) values.Add(tag.Value?.ToString() ?? "");
        return values.ToArray();
    }
}