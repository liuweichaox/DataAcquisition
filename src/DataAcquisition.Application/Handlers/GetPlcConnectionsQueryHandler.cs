using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Application.Queries;
using MediatR;

namespace DataAcquisition.Application.Handlers;

public sealed class GetPlcConnectionsQueryHandler(IDataAcquisitionService service)
    : IRequestHandler<GetPlcConnectionsQuery, IReadOnlyCollection<PlcConnectionStatus>>
{
    public Task<IReadOnlyCollection<PlcConnectionStatus>> Handle(GetPlcConnectionsQuery request, CancellationToken cancellationToken)
        => Task.FromResult(service.GetPlcConnections());
}

