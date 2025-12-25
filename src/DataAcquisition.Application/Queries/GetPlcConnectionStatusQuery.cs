using System.Collections.Generic;
using MediatR;

namespace DataAcquisition.Application.Queries;

public sealed record GetPlcConnectionStatusQuery : IRequest<SortedDictionary<string, bool>>;

