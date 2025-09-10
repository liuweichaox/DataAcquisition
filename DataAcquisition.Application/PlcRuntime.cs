using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application;

/// <summary>
/// 表示 PLC 的运行时上下文，包含取消令牌和运行任务。
/// </summary>
public sealed record PlcRuntime(CancellationTokenSource Cts, Task Running);
