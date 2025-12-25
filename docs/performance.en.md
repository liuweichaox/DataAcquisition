# 🎯 Performance Optimization Recommendations

This document provides performance optimization suggestions and best practices for the DataAcquisition system.

## Acquisition Parameter Tuning

| Parameter            | Recommended Value | Description                         |
| -------------------- | ----------------- | ----------------------------------- |
| BatchSize            | 10-50             | Balance latency and throughput      |
| AcquisitionInterval  | 100-500ms         | Adjust based on PLC performance     |
| HeartbeatInterval    | 5000ms            | Connection monitoring frequency     |

## Storage Optimization

- **Parquet Compression**: Use Snappy compression to reduce disk usage
- **Retry Strategy**: RetryWorker defaults to 5 seconds, adjust based on network conditions

For detailed optimization recommendations, see: [Chinese Performance Guide](performance.md)
