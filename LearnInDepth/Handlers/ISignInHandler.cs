namespace LearnInDepth.Handlers
{
    public interface ISignInHandler
    {
        Task SendOtpEmail(string email);
        Task<string> VerifyOtpAndReturnAuthToken(string email, string otp);
    }
}
