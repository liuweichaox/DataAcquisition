using System;

namespace DataAcquisition.Core.OperationalEvents;

public record OpsEvent(DateTimeOffset Timestamp, string DeviceCode, string Level, string Message, object? Data);
