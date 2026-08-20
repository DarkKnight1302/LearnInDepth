using LearnInDepth.Models;
using LearnInDepth.Repositories;
using NewHorizonLib.Services.Interfaces;
using System.Security.Claims;

namespace LearnInDepth.Handlers
{
    public class SignInHandler : ISignInHandler
    {
        private readonly IEmailService emailService;
        private readonly IOtpService otpService;
        private readonly ITokenService tokenService;
        private readonly IUserRepository userRepository;
        private readonly string senderEmail;
        private readonly string senderName;

        public SignInHandler(
            IEmailService emailService,
            IOtpService otpService,
            ITokenService tokenService,
            IUserRepository userRepository,
            IConfiguration configuration)
        {
            this.emailService = emailService;
            this.otpService = otpService;
            this.tokenService = tokenService;
            this.userRepository = userRepository;
            // Default sender is the verified Brevo sender already used by other apps on this account;
            // override via config once a learnindepth sender/domain is verified.
            this.senderEmail = configuration["Email:SenderAddress"] ?? "noreply@hyderabadt20championship.online";
            this.senderName = configuration["Email:SenderName"] ?? "Learn-In-Depth";
        }

        public async Task SendOtpEmail(string email)
        {
            string otp = otpService.GenerateOtp(email);
            string body = OtpEmailTemplate(otp);
            await emailService.SendMail(email, body, "Your Learn-In-Depth Verification Code", senderName, senderEmail, true).ConfigureAwait(false);
        }

        public async Task<string> VerifyOtpAndReturnAuthToken(string email, string otp)
        {
            bool isValid = otpService.ValidateOtp(email, otp);
            if (!isValid)
            {
                return string.Empty;
            }

            User user = await userRepository.GetByIdAsync(email).ConfigureAwait(false);
            if (user == null)
            {
                user = new User
                {
                    id = email,
                    Name = email,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastLoginAtUtc = DateTime.UtcNow
                };
            }
            else
            {
                user.LastLoginAtUtc = DateTime.UtcNow;
            }
            await userRepository.UpsertAsync(user).ConfigureAwait(false);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, user.Name)
            };
            return tokenService.GenerateToken(claims, GlobalConstant.Issuer, GlobalConstant.Audience, GlobalConstant.TokenExpiryDays);
        }

        private static string OtpEmailTemplate(string otp)
        {
            return $$"""
                <!DOCTYPE html>
                <html lang="en" style="margin:0;padding:0;">
                <body style="margin:0;padding:24px;background:#0b1020;font-family:Arial,Helvetica,sans-serif;color:#e6edf3;">
                  <div style="max-width:520px;margin:0 auto;background:#111827;border:1px solid #2b3645;border-radius:12px;padding:32px;text-align:center;">
                    <div style="color:#7aa2f7;letter-spacing:2px;font-size:12px;font-weight:700;">LEARN-IN-DEPTH</div>
                    <h1 style="font-size:20px;margin:16px 0 8px;">Your verification code</h1>
                    <p style="color:#b8c2cf;font-size:14px;line-height:1.6;">Use this code to sign in. It expires in 5 minutes.</p>
                    <div style="background:#0f172a;border:1px solid #334155;border-radius:10px;padding:20px;margin:20px 0;">
                      <span style="font-family:Consolas,'Courier New',monospace;font-size:32px;font-weight:900;letter-spacing:8px;color:#f8fafc;">{{otp}}</span>
                    </div>
                    <p style="color:#8ea2b7;font-size:12px;">If you didn't request this, you can ignore this email.</p>
                  </div>
                </body>
                </html>
                """;
        }
    }
}
