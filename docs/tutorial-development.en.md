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

1. Prefer inheriting `PlcClientServiceBase` if you need a new communication backend
2. Implement `IPlcDriverProvider`
3. Register the provider in DI
4. Use the driver via its full `Driver` name in config

Tips:
- Leverage `HslCommunication` where possible
- Follow the existing connection lifecycle and heartbeat strategy
- Implement only the smaller capabilities your driver truly needs: `IPlcConnectionClient`, `IPlcDataAccessClient`, and `IPlcTypedWriteClient`
- Keep `Host` / `Port` and `ProtocolOptions` contracts explicit and honest
- Prefer implementing `IPlcDriverProvider` instead of adding hard-coded branches in the factory

---

## 3. Extend Storage Backend

The project separates primary storage from WAL storage on purpose:

- `IDataStorageService`
  - owns primary-store writes
  - current core method: `SaveBatchAsync(List<DataMessage>)`
- `IWalStorageService`
  - owns the local WAL lifecycle
  - must implement `WriteAsync` / `ReadAsync` / `DeleteAsync` / `MoveToRetryAsync` / `GetRetryFilesAsync` / `QuarantineInvalidAsync`

Recommendations:

1. Replace the primary backend by implementing `IDataStorageService`
2. Replace the WAL backend by implementing `IWalStorageService`
3. Do not bypass `QueueService`, otherwise the WAL-first contract is broken
4. Keep lifecycle semantics such as `pending/retry/invalid` so realtime writes and background replay do not fight each other

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

Next:

- [Contributing Guide](../CONTRIBUTING.en.md)
- [Modules](modules.en.md)
- [Design](design.en.md)
