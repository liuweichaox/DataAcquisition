using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Application.Queries;
using MediatR;

namespace DataAcquisition.Application.Handlers;

public sealed class GetPlcConnectionStatusQueryHandler(IDataAcquisitionService service)
    : IRequestHandler<GetPlcConnectionStatusQuery, SortedDictionary<string, bool>>
{
    public Task<SortedDictionary<string, bool>> Handle(GetPlcConnectionStatusQuery request, CancellationToken cancellationToken)
        => Task.FromResult(service.GetPlcConnectionStatus());
}

