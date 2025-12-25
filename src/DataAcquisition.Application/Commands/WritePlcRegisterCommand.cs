using System.Collections.Generic;
using DataAcquisition.Domain.Clients;
using MediatR;

namespace DataAcquisition.Application.Commands;

public sealed record WritePLCRegisterCommand(
    string PLCCode,
    IReadOnlyList<WritePLCRegisterItem> Items
) : IRequest<IReadOnlyList<PLCWriteResult>>;

public sealed record WritePLCRegisterItem(
    string Address,
    string DataType,
    object Value
);

