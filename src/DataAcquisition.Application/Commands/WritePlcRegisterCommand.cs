using System.Collections.Generic;
using DataAcquisition.Domain.Models;
using MediatR;

namespace DataAcquisition.Application.Commands;

public sealed record WritePlcRegisterCommand(
    string PlcCode,
    IReadOnlyList<WritePlcRegisterItem> Items
) : IRequest<IReadOnlyList<PlcWriteResult>>;

public sealed record WritePlcRegisterItem(
    string Address,
    string DataType,
    object Value
);

