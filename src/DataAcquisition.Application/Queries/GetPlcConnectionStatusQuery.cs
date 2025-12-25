using System.Collections.Generic;
using MediatR;

namespace DataAcquisition.Application.Queries;

public sealed record GetPLCConnectionStatusQuery : IRequest<SortedDictionary<string, bool>>;

