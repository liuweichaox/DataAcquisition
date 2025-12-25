using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Application.Commands;
using DataAcquisition.Domain.Clients;
using MediatR;

namespace DataAcquisition.Application.Handlers;

public sealed class WritePlcRegisterCommandHandler(IDataAcquisitionService service)
    : IRequestHandler<WritePlcRegisterCommand, IReadOnlyList<PLCWriteResult>>
{
    public async Task<IReadOnlyList<PLCWriteResult>> Handle(WritePlcRegisterCommand request, CancellationToken cancellationToken)
    {
        var results = new List<PLCWriteResult>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var result = await service.WritePLCAsync(
                request.PlcCode,
                item.Address,
                item.Value,
                item.DataType,
                cancellationToken);

            results.Add(result);
        }

        return results;
    }
}

