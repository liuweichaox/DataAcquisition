using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Application.Commands;
using DataAcquisition.Domain.Models;
using MediatR;

namespace DataAcquisition.Application.Handlers;

public sealed class WritePlcRegisterCommandHandler(IDataAcquisitionService service)
    : IRequestHandler<WritePlcRegisterCommand, IReadOnlyList<PlcWriteResult>>
{
    public async Task<IReadOnlyList<PlcWriteResult>> Handle(WritePlcRegisterCommand request, CancellationToken cancellationToken)
    {
        var results = new List<PlcWriteResult>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var result = await service.WritePlcAsync(
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

