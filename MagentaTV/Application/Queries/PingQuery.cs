using MagentaTV.Models;
using MediatR;


namespace MagentaTV.Application.Queries
{
    public class PingQuery : IRequest<ApiResponse<PingResultDto>>
    {
    }
}