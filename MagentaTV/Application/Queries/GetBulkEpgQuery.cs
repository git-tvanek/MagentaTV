using System;
using System.Collections.Generic;
using System.Linq;
using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Queries;

public class GetBulkEpgQuery : IRequest<ApiResponse<Dictionary<int, List<EpgItemDto>>>>
{
    public IEnumerable<int> ChannelIds { get; set; } = Enumerable.Empty<int>();
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
