using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetAuthStatusQuery : IRequest<ApiResponse<AuthStatusDto>>
    {
    }
}