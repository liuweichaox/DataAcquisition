# Development Tutorial: Extending Protocols and Storage

This guide is for developers who want to extend PLC protocols or storage backends.

---

## 1. Project Structure

- Application: interfaces and abstractions
- Domain: core models
- Infrastructure: implementations (PLC clients, storage, queue)
- Edge Agent: acquisition service
- Central API/Web: centralized management

---

## 2. Add a New PLC Protocol

High-level steps:

1. Implement `IPlcClientService`
2. Register it in the factory
3. Add a new `PlcType` enum value
4. Implement connect/read/write

Tips:
- Leverage `HslCommunication` where possible
- Follow the existing connection lifecycle and heartbeat strategy

---

## 3. Extend Storage Backend

Implement `IDataStorageService`:

- `WriteAsync`: batch writes
- `InitializeAsync`: setup
- `Dispose`: cleanup

Possible targets: TimescaleDB, Kafka, S3, etc.

---

## 4. Custom Data Processing

- Use `EvalExpression` for unit conversion
- Add validation or filtering before persistence

---

## 5. Testing Recommendations

- Unit tests for edge cases
- Integration tests with Simulator
- Performance tests for throughput/latency

---

## 6. Contribution Workflow

1. Fork the repo
2. Create a branch
3. Submit PR

---

Next: [Modules](modules.en.md) and [Design](design.en.md)
