using Microsoft.EntityFrameworkCore;
using ProjectMessengerServer.Domain.Entities;
using ProjectMessengerServer.Infrastructure.Data;

namespace ProjectMessengerServer.Application.Services
{
    public class DeviceService
    {
        private readonly AppDbContext dbContext;

        public DeviceService(AppDbContext _dbContext)
        {
            dbContext = _dbContext;
        }

        public async Task<UserDevice> AddUserDeviceAsync(User user, string? deviceId, string? deviceType, string? placeAuthorization)
        {
            var userDevice = new UserDevice
            {
                User = user,
                DeviceId = deviceId ?? null,
                DeviceType = deviceType ?? null,
                PlaceAuthorization = placeAuthorization ?? null,
                LastActive = DateTime.UtcNow
            };
;
            dbContext.UserDevices.Add(userDevice);

            return userDevice;
        }

        public async Task<UserDevice> GetUserDeviceAsync(int userId, string? deviceId)
        {
            var userDevice = await dbContext.UserDevices
                .FirstOrDefaultAsync(ud => ud.UserId == userId && ud.DeviceId == deviceId);

            return userDevice!;
        }
    }
}
