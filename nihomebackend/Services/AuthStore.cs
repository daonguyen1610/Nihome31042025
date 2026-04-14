using System.Collections.Concurrent;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class AuthStore
{
    private readonly ConcurrentDictionary<int, ApplicationUser> _users = new();
    private readonly ConcurrentDictionary<string, RegistrationOtp> _otpsByPhone = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RefreshToken> _refreshTokens = new(StringComparer.Ordinal);
    private int _userIdSequence;
    private int _refreshTokenSequence;
    private int _otpSequence;

    public ApplicationUser? FindUserByPhone(string phoneNumber) =>
        _users.Values.FirstOrDefault(u => u.PhoneNumber.Equals(phoneNumber, StringComparison.OrdinalIgnoreCase));

    public ApplicationUser? FindUserById(int userId) =>
        _users.TryGetValue(userId, out var user) ? user : null;

    public ApplicationUser AddUser(ApplicationUser user)
    {
        user.Id = Interlocked.Increment(ref _userIdSequence);
        _users[user.Id] = user;
        return user;
    }

    public void SaveOtp(RegistrationOtp otp)
    {
        otp.Id = Interlocked.Increment(ref _otpSequence);
        _otpsByPhone[otp.PhoneNumber] = otp;
    }

    public RegistrationOtp? GetLatestOtp(string phoneNumber) =>
        _otpsByPhone.TryGetValue(phoneNumber, out var otp) ? otp : null;

    public void SaveRefreshToken(RefreshToken refreshToken)
    {
        refreshToken.Id = Interlocked.Increment(ref _refreshTokenSequence);
        _refreshTokens[refreshToken.Token] = refreshToken;
    }

    public RefreshToken? GetRefreshToken(string token) =>
        _refreshTokens.TryGetValue(token, out var refreshToken) ? refreshToken : null;

    public void UpdateUser(ApplicationUser user) => _users[user.Id] = user;
}
