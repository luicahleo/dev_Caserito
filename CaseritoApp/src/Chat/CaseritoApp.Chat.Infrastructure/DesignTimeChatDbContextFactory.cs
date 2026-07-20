using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Chat.Infrastructure;

public sealed class DesignTimeChatDbContextFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlServer(
                "Server=localhost,1433;Database=CaseritoDb;User Id=sa;Password=noop;TrustServerCertificate=True")
            .Options;
        return new ChatDbContext(opciones);
    }
}
