namespace ProjectMessengerServer.Application.DTO.Ws
{
    public record WsEnvelope(string Op, Dictionary<string, string> Data, int? Seq);
}
