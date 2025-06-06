using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Commands;

public class ConvertVideoCommand : IRequest<ApiResponse<string>>
{
    public string InputUrl { get; set; } = string.Empty;
    public string OutputFile { get; set; } = string.Empty;
}
