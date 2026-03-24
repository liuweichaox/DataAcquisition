using System.Collections.Generic;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Queues;

internal sealed record PendingBatch(string BatchKey, string Measurement, List<DataMessage> Messages);
