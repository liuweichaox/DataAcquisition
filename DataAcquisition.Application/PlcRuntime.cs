using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application;

public sealed record PlcRuntime(CancellationTokenSource Cts, Task Running);
