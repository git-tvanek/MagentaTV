using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GenerateEpgXmlQuery : IRequest<string>
    {
        public int ChannelId { get; set; }
    }
}