using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetStreamUrlQuery : IRequest<ApiResponse<StreamUrlDto>>
    {
        public int ChannelId { get; set; }
        public string Quality { get; set; } = "p5";
    }
}
