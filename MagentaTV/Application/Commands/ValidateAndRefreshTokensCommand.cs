using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.Commands
{
    internal class ValidateAndRefreshTokensCommand : IRequest
    {
        public SessionData Session { get; set; } = null!;
    }
}