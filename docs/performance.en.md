# 🎯 Performance Optimization Recommendations

This document provides performance optimization suggestions and best practices for the DataAcquisition system.

## Related Documents

- [Configuration Guide](configuration.en.md) - Detailed configuration options
- [Data Processing Flow](data-flow.en.md) - Understand data flow process

## Acquisition Parameter Tuning

| Parameter            | Recommended Value | Description                         |
| -------------------- | ----------------- | ----------------------------------- |
| BatchSize            | 10-50             | Balance latency and throughput      |
| AcquisitionInterval  | 100-500ms         | Adjust based on PLC performance     |
| HeartbeatInterval    | 5000ms            | Connection monitoring frequency     |

### BatchSize Tuning Recommendations

- **Small Batch (10-20)**: Suitable for latency-sensitive scenarios, data can be written quickly
- **Medium Batch (20-50)**: Balance latency and throughput, recommended default value
- **Large Batch (50+)**: Suitable for high throughput scenarios, but will increase latency

### AcquisitionInterval Tuning Recommendations

- **High Frequency (100-200ms)**: Suitable for rapidly changing signals
- **Medium Frequency (200-500ms)**: Suitable for most industrial scenarios, recommended default value
- **Low Frequency (500ms+)**: Suitable for slowly changing sensor data

## Storage Optimization

### Parquet Compression

- **Snappy Compression**: Use Snappy compression algorithm to reduce disk usage
- **Compression Level**: System uses compression by default, can be adjusted in configuration

### Retry Strategy

- **Retry Interval**: RetryWorker defaults to 5 seconds, can be adjusted based on network conditions
- **Maximum Retry Count**: Default 3 times, errors are logged after exceeding
- **Retry Queue**: Failed data is retained in WAL files, retried periodically

### InfluxDB Optimization

- **Batch Writes**: Use batch write API to improve write efficiency
- **Write Batch Size**: Recommended batch size between 1000-5000
- **Connection Pool**: Configure appropriate connection pool size

## System Resource Optimization

### Memory Management

- **Queue Size**: Adjust queue size based on available memory
- **Cache Strategy**: Reasonable use of cache to avoid memory overflow
- **Object Pool**: Reuse objects to reduce GC pressure

### CPU Optimization

- **Concurrent Acquisition**: Concurrent acquisition from multiple PLC devices to fully utilize CPU
- **Asynchronous Processing**: Use asynchronous APIs to improve CPU utilization
- **Batch Processing**: Batch process data to reduce CPU overhead

### Network Optimization

- **Batch Reads**: Use batch reads to reduce network round trips
- **Connection Reuse**: Reuse PLC connections to reduce connection overhead
- **Timeout Settings**: Reasonably set network timeouts to avoid long waits

## Monitoring Metrics

Regularly monitor the following key metrics:

- **Collection Latency**: `data_acquisition_collection_latency_ms`
- **Queue Depth**: `data_acquisition_queue_depth`
- **Write Latency**: `data_acquisition_write_latency_ms`
- **Error Rate**: `data_acquisition_errors_total`

Adjust configuration parameters based on monitoring data to optimize system performance.

## Deployment Recommendations

### Single Machine Deployment

- Suitable for small-scale deployment, single Edge Agent acquiring data from multiple PLCs
- Recommended configuration: 4-core CPU, 8GB RAM, SSD storage

### Distributed Deployment

- Suitable for large-scale deployment, multiple Edge Agents for distributed acquisition
- Each Edge Agent is responsible for a portion of PLCs, reducing single-machine load
- Unified management and monitoring through Central API

## Next Steps

After performance optimization, you can:

- Read [Core Module Documentation](modules.en.md) to understand system core modules in depth
- Read [FAQ](faq.en.md) for more help
- Read [Design Philosophy](design.en.md) to understand system design philosophy
