using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Core.DataAcquisitions;

public sealed record PlcRuntime(CancellationTokenSource Cts, Task Running);