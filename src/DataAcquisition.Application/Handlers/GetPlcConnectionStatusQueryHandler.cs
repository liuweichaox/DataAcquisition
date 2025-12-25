using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Application.Queries;
using MediatR;

namespace DataAcquisition.Application.Handlers;

public sealed class GetPLCConnectionStatusQueryHandler(IDataAcquisitionService service)
    : IRequestHandler<GetPLCConnectionStatusQuery, SortedDictionary<string, bool>>
{
    public Task<SortedDictionary<string, bool>> Handle(GetPLCConnectionStatusQuery request, CancellationToken cancellationToken)
        => Task.FromResult(service.GetPlcConnectionStatus());
}

