namespace ProjectMessengerServer.Model
{
    public class Log
    {
        public int Id { get; set; }                    // PK
        public DateTime Timestamp { get; set; }        // Время события
        public string Level { get; set; } = null!;     // Уровень логирования (Info, Warning, Error)
        public string Message { get; set; } = null!;   // Сообщение лога
    }
}
