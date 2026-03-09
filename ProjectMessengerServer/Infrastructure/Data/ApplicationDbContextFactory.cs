using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace ProjectMessengerServer.Infrastructure.Data
{
    // Этот класс реализует IDesignTimeDbContextFactory
    // Он нужен, чтобы инструменты EF (для миграций)
    // "знали", как создать ваш DbContext
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // 1. Настраиваем ConfigurationBuilder, чтобы найти appsettings.json
            // Он будет искать файл в папке, где запускается команда (т.е. в корне проекта)
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            // 2. Создаем DbContextOptionsBuilder вручную
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // 3. Получаем строку подключения из appsettings.json
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // 4. Говорим, что нужно использовать SQL Server
            optionsBuilder.UseNpgsql(connectionString);

            // 5. Возвращаем новый экземпляр DbContext
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}