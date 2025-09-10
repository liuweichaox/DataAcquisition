using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

public sealed record PlcRuntime(CancellationTokenSource Cts, Task Running);