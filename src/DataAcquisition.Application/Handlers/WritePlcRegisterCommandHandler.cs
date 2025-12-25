using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Application.Commands;
using DataAcquisition.Domain.Clients;
using MediatR;

namespace DataAcquisition.Application.Handlers;

public sealed class WritePLCRegisterCommandHandler(IDataAcquisitionService service)
    : IRequestHandler<WritePLCRegisterCommand, IReadOnlyList<PLCWriteResult>>
{
    public async Task<IReadOnlyList<PLCWriteResult>> Handle(WritePLCRegisterCommand request, CancellationToken cancellationToken)
    {
        var results = new List<PLCWriteResult>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var result = await service.WritePLCAsync(
                request.PLCCode,
                item.Address,
                item.Value,
                item.DataType,
                cancellationToken);

            results.Add(result);
        }

        return results;
    }
}

