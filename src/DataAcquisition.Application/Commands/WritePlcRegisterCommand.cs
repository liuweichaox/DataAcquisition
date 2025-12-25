using System.Collections.Generic;
using DataAcquisition.Domain.Clients;
using MediatR;

namespace DataAcquisition.Application.Commands;

public sealed record WritePlcRegisterCommand(
    string PlcCode,
    IReadOnlyList<WritePlcRegisterItem> Items
) : IRequest<IReadOnlyList<PLCWriteResult>>;

public sealed record WritePlcRegisterItem(
    string Address,
    string DataType,
    object Value
);

