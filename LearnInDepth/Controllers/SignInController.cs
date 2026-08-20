using LearnInDepth.Handlers;
using Microsoft.AspNetCore.Mvc;
using NewHorizonLib.Attributes;

namespace LearnInDepth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SignInController : ControllerBase
    {
        private readonly ISignInHandler signInHandler;

        public SignInController(ISignInHandler signInHandler)
        {
            this.signInHandler = signInHandler;
        }

        [HttpPost("send-otp")]
        [RateLimit(3, 10)]
        public async Task<IActionResult> SendOtp()
        {
            string email = HttpContext.Request.Headers["x-uid"].ToString();
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new { success = false, message = "Email (x-uid header) is required" });
            }

            await signInHandler.SendOtpEmail(email);
            return Ok(new { success = true, message = "OTP sent" });
        }

        [HttpPost("verify-otp")]
        [RateLimit(6, 1)]
        public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request)
        {
            string email = HttpContext.Request.Headers["x-uid"].ToString();
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new { success = false, message = "Email (x-uid header) is required" });
            }

            string authToken = await signInHandler.VerifyOtpAndReturnAuthToken(email, request.Otp);
            if (string.IsNullOrEmpty(authToken))
            {
                return BadRequest(new { success = false, message = "Invalid OTP" });
            }

            return Ok(new SignInResponse
            {
                AuthToken = authToken,
                Email = email,
                Issuer = GlobalConstant.Issuer,
                Audience = GlobalConstant.Audience
            });
        }

        public class VerifyOtpRequest
        {
            public string Otp { get; set; } = string.Empty;
        }

        public class SignInResponse
        {
            public string AuthToken { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Issuer { get; set; } = string.Empty;
            public string Audience { get; set; } = string.Empty;
        }
    }
}
