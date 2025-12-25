# ❓ Frequently Asked Questions (FAQ)

This document collects common questions and answers about the DataAcquisition system.

## Q: What if data is lost?

**A**: The system uses a WAL-first architecture. All data is first written to Parquet files, then to InfluxDB. WAL files are only deleted when both writes succeed, ensuring zero data loss.

## Q: How to add a new PLC protocol?

**A**: Implement the `IPLCClientService` interface and register the new protocol support in `PLCClientFactory`.

## Q: Do I need to restart after configuration changes?

**A**: No. The system uses FileSystemWatcher to monitor configuration file changes, supporting hot updates.

## Q: Where to view monitoring metrics?

**A**: Visit http://localhost:8000/metrics for Prometheus format metrics, or http://localhost:8000/api/metrics-data for JSON format (recommended).

For more FAQs, see: [Chinese FAQ Guide](faq.md)
