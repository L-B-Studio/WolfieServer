using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Domain.Entities;

namespace ProjectMessengerServer.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
        public DbSet<PasswordResetTokenReset> PasswordResetTokenResets { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<Log> Logs { get; set; } = null!;
        public DbSet<UserDevice> UserDevices { get; set; } = null!;
        public DbSet<UserSettings> UserSettings { get; set; } = null!;
        public DbSet<UserPrivacy> UserPrivacies { get; set; } = null!;
        public DbSet<UserProfile> UserProfiles { get; set; } = null!;
        public DbSet<ForgotPassDevice> ForgotPassDevices { get; set; } = null!;
        public DbSet<ChatMember> ChatMembers { get; set; } = null!;
        public DbSet<Chat> Chats { get; set; } = null!;
        public DbSet<UserSequence> UserSequences { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => t.TokenHash)
                .IsUnique();

            modelBuilder.Entity<PasswordResetTokenReset>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetTokenReset>()
                .HasIndex(t => t.TokenResetHash)
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(t => t.RefreshTokenHash)
                .IsUnique();

            modelBuilder.Entity<UserDevice>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSettings>()
                .HasKey(t => t.UserId);

            modelBuilder.Entity<UserSettings>()
                .HasOne(t => t.User)
                .WithOne()
                .HasForeignKey<UserSettings>(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPrivacy>()
                .HasKey(t => t.UserId);

            modelBuilder.Entity<UserPrivacy>()
                .HasOne(t => t.User)
                .WithOne()
                .HasForeignKey<UserPrivacy>(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserProfile>()
                .HasKey(t => t.UserId);

            modelBuilder.Entity<UserProfile>()
                .HasOne(t => t.User)
                .WithOne()
                .HasForeignKey<UserProfile>(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ForgotPassDevice>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMember>()
                .HasOne(cm => cm.User)
                .WithMany(u => u.ChatMembers)
                .HasForeignKey(cm => cm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMember>()
                .HasOne(cm => cm.Chat)
                .WithMany(c => c.Members)
                .HasForeignKey(cm => cm.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMember>()
                .HasKey(cm => new { cm.ChatId, cm.UserId });

            modelBuilder.Entity<ChatMember>()
                .HasOne(cm => cm.LastReadMessage)
                .WithMany()
                .HasForeignKey(cm => cm.LastReadMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ChatMember>()
                .HasIndex(cm => cm.UserId);

            modelBuilder.Entity<Chat>()
                .HasMany(c => c.Members)
                .WithOne(cm => cm.Chat)
                .HasForeignKey(cm => cm.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Chat>()
                .HasIndex(c => c.Uid)
                .IsUnique();

            modelBuilder.Entity<UserSequence>()
                .HasKey(us => us.UserId);

            modelBuilder.Entity<UserSequence>()
                .HasOne(cm => cm.User)
                .WithOne()
                .HasForeignKey<UserSequence>(cm => cm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatId);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Chat>()
                .HasOne(c => c.LastMessage)
                .WithMany()
                .HasForeignKey(c => c.LastMessageId)
                .OnDelete(DeleteBehavior.SetNull);

        }
    }
}