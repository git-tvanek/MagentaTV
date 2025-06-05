using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class QueueBackgroundWorkCommand : IRequest<ApiResponse<string>>
    {
        public string WorkType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
