using ProjectMessengerServer.Model;
using ProjectMessengerServer.Data;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMessengerServer.Helpers
{
    public class LogsHelper
    {
        public static async Task AddLog(string level, string message, AppDbContext dbContext)
        {
            var log = new Log
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message
            };
            dbContext.Logs.Add(log);
            dbContext.SaveChanges();
        }

        public static IEnumerable<Log> GetLogs(AppDbContext dbContext, int limit, int? offset = 0)
        {
            var query = dbContext.Logs
                .OrderByDescending(l => l.Id)
                .Skip(limit);
            if (offset.HasValue)
            {
                query = query.Take(offset.Value);
            }
            return query.Reverse().ToList();
        }
    }
}
