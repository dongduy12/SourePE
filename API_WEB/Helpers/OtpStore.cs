namespace API_WEB.Helpers
{
    public static class OtpStore
    {
        private static readonly Dictionary<string, (string Otp, DateTime Expiration)> _otps = new();

        public static void SetOtp(string email, string otp, int minutes = 5)
        {
            _otps[email] = (otp, DateTime.UtcNow.AddMinutes(minutes));
        }

        public static bool ValidateOtp(string email, string otp)
        {
            if (_otps.TryGetValue(email, out var data))
            {
                if (data.Expiration > DateTime.UtcNow && data.Otp == otp)
                {
                    return true;
                }
            }
            return false;
        }

        public static void RemoveOtp(string email)
        {
            if (_otps.ContainsKey(email))
            {
                _otps.Remove(email);
            }
        }
    }
}
