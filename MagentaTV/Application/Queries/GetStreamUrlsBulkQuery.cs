using System.Collections.Generic;
using System.Linq;
using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Queries;

public class GetStreamUrlsBulkQuery : IRequest<ApiResponse<Dictionary<int, string?>>>
{
    public IEnumerable<int> ChannelIds { get; set; } = Enumerable.Empty<int>();
}
