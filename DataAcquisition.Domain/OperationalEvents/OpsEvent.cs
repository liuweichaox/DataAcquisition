using System;

namespace DataAcquisition.Domain.OperationalEvents;

/// <summary>
/// 系统运行事件记录。
/// </summary>
public record OpsEvent(DateTimeOffset Timestamp, string Level, string Message, object? Data);
