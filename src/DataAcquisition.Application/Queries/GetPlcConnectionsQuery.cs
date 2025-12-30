using System.Collections.Generic;
using DataAcquisition.Domain.Models;
using MediatR;

namespace DataAcquisition.Application.Queries;

public sealed record GetPlcConnectionsQuery : IRequest<IReadOnlyCollection<PlcConnectionStatus>>;

