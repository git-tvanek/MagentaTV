using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetCatchupStreamQuery : IRequest<ApiResponse<StreamUrlDto>>
    {
        public long ScheduleId { get; set; }
        public string Quality { get; set; } = "p5";
    }
}
