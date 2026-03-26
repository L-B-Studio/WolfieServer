using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMessengerServer.Application.DTO.Auth;
using ProjectMessengerServer.Application.DTO.Password;
using ProjectMessengerServer.Application.Services;

namespace ProjectMessengerServer.Controllers
{
    [ApiController]
    [Route("auth/")]
    public class AuthController : ControllerBase
    {
        private readonly TokenService _tokenService;
        private readonly AuthService _authService;
        private readonly DeviceService _deviceService;
        private readonly PasswordResetService _passwordResetService;

        public AuthController(TokenService tokenService, AuthService authService, DeviceService deviceService, PasswordResetService passwordResetService)
        {
            _tokenService = tokenService;
            _authService = authService;
            _deviceService = deviceService;
            _passwordResetService = passwordResetService;
        }

        [AllowAnonymous]
        [HttpPost("registration")]
        public async Task<IActionResult> Register(RegistrationRequest req)
        {
            //data.TryGetValue("username", out string? name);
            //data.TryGetValue("email", out string? email);
            //data.TryGetValue("password", out string? password);
            //data.TryGetValue("birthday", out string? birthdayString);
            //data.TryGetValue("device_info", out string? deviceInfo);
            string name = req.Username;
            string email = req.Email;
            string password = req.Password;
            string birthdayString = req.Birthday;
            string? deviceId = req.Device_id;
            string? deviceType = req.Device_type;
            string? placeAuthorization = req.Place_authorization;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(birthdayString))
            {
                Console.WriteLine("Registration failed: Missing required fields.");
                return BadRequest();
            }

            if (!DateTime.TryParse(birthdayString, out DateTime birthday))
            {
                Console.WriteLine("Registration failed: Invalid birthday format.");
                return BadRequest();
            }

            var user = await _authService.CreateUserAsync(req);

            if (user == null)
            {
                Console.WriteLine("Registration failed: User creation failed.");
                return BadRequest();
            }

            var hashAccessToken = await _tokenService.CreateAccessTokenAsync(user);
            var hashRefreshToken = await _tokenService.CreateRefreshTokenAsync(user);

            return Ok(new RegistrationResponse(
                Token_refresh: hashRefreshToken,
                Token_access: hashAccessToken
            ));
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest req)
        {

            //data.TryGetValue("email", out string? email);
            //data.TryGetValue("password", out string? password);
            //data.TryGetValue("device_info", out string? deviceInfo);

            string email = req.Email;
            string password = req.Password;
            string? deviceId = req.Device_id;
            string? deviceType = req.Device_type;
            string? placeAuthorization = req.Place_authorization;
            if (email == null || password == null)
            {
                return BadRequest();
            }

            var user = await _authService.AuthenticateUserAsync(req);

            if (user == null)
            {
                return Unauthorized();
            }

            var checkDeviceId = await _deviceService.GetUserDeviceAsync(user.Id, deviceId);

            if (checkDeviceId == null)
            {
                await _deviceService.AddUserDeviceAsync(user, deviceId, deviceType, placeAuthorization);
            }

            var hashAccessToken = await _tokenService.CreateAccessTokenAsync(user);
            var hashRefreshToken = await _tokenService.CreateRefreshTokenAsync(user);


            return Ok(new LoginResponse(
                Token_access: hashAccessToken,
                Token_refresh: hashRefreshToken
            ));
        }

        [AllowAnonymous]
        [HttpPost("get_access_token")]
        public async Task<IActionResult> GetAccessToken(GetAccessTokenRequest req)
        {

            //data.TryGetValue("email", out string? email);
            //data.TryGetValue("password", out string? password);
            //data.TryGetValue("device_info", out string? deviceInfo);

            //data.TryGetValue("token_refresh", out string refreshTokenSession);
            //data.TryGetValue("device_info", out string? deviceInfo);
            string refreshTokenSession = req.Token_refresh;
            string? deviceId = req.Device_id;
            string? deviceType = req.Device_type;
            string? placeAuthorization = req.Place_authorization;

            if (string.IsNullOrWhiteSpace(refreshTokenSession))
            {
                return BadRequest();
            }

            var rotationResult = await _tokenService.RotationTokenAsync(refreshTokenSession);

            if (rotationResult == null)
            {
                return Unauthorized();
            }

            return Ok(new GetAccessTokenResponse(
                Token_access: rotationResult.HashAccessToken,
                Token_refresh: rotationResult.HashRefreshToken
            ));
        }

        [AllowAnonymous]
        [HttpPost("password/forgot")]
        public async Task<IActionResult> ResetPassword(ForgotPassRequest req)
        {

            //data.TryGetValue("email", out string? email);
            //data.TryGetValue("password", out string? password);
            //data.TryGetValue("device_info", out string? deviceInfo);

            //data.TryGetValue("token_refresh", out string refreshTokenSession);
            //data.TryGetValue("device_info", out string? deviceInfo);

            //data.TryGetValue("email", out string email);
            string email = req.Email;

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest();
            }

            var result = await _passwordResetService.ResetPasswordAsync(req);

            if (!result.IsSuccess)
            {
                return BadRequest();
            }

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("password/verify")]
        public async Task<IActionResult> VerifyResetPassword(ForgotPassVerifyRequest req)
        {
            //data.TryGetValue("email", out string email);
            //data.TryGetValue("code", out string code);
            //data.TryGetValue("device_info", out string deviceInfo);
            string email = req.Email;
            string code = req.Code;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(code))
            {
                return BadRequest();
            }

            var hashTokenReset = await _passwordResetService.VerifyResetPasswordAsync(req);

            if (hashTokenReset == null)
            {
                return BadRequest();
            }

            return Ok(new ForgotPassVerifyResponse(
                Token_reset: hashTokenReset
            ));
        }

        [AllowAnonymous]
        [HttpPost("password/change")]
        public async Task<IActionResult> ChangePassword(ChangedPassRequest req)
        {

            //data.TryGetValue("token_reset", out string token);
            //data.TryGetValue("password", out string newPassword);

            string token = req.Token_reset;
            string newPassword = req.Password;
            string? deviceId = req.Device_id;
            string? deviceType = req.Device_type;
            string? placeAuthorization = req.Place_authorization;

            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return BadRequest();
            }

            var result = await _passwordResetService.ChangePasswordAsync(req);

            if (!result.IsSuccess)
            {
                return BadRequest();
            }

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(LogoutRequest req)
        {
            string refreshTokenSession = req.Token_refresh;

            var result = await _tokenService.RevokeRefreshTokenAsync(refreshTokenSession);

            if (!result.IsSuccess)
            {
                return BadRequest();
            }

            return NoContent();
        }
    }
}
