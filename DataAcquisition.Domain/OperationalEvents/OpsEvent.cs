using System;

namespace DataAcquisition.Domain.OperationalEvents;

public record OpsEvent(DateTimeOffset Timestamp, string DeviceCode, string Level, string Message, object? Data);
