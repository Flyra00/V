using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Auth.Dtos;
using Restoran.Models;
using Restoran.Shared.Abstractions;

namespace Restoran.Features.Auth.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public AuthService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<AuthResult> LoginStaffAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, cancellationToken);

            return await BuildLoginResultAsync(user, password, "Username atau password salah", cancellationToken);
        }

        public async Task<AuthResult> LoginMemberOrStaffAsync(string identifier, string password, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .Include(u => u.Member)
                .FirstOrDefaultAsync(
                    u => (u.Email == identifier || u.Username == identifier) && u.IsActive,
                    cancellationToken);

            return await BuildLoginResultAsync(user, password, "Email/Username atau password salah", cancellationToken);
        }

        public async Task<AuthResult> RegisterMemberAsync(MemberRegisterRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.FullName))
            {
                return AuthResult.Failure("Data registrasi belum lengkap");
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
            {
                return AuthResult.Failure("Email sudah terdaftar");
            }

            if (await _context.Users.AnyAsync(u => u.Username == request.Username, cancellationToken))
            {
                return AuthResult.Failure("Username sudah digunakan");
            }

            var now = _dateTimeProvider.Now;
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = UserRole.Member,
                IsActive = true,
                CreatedAt = now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            var member = new Member
            {
                UserId = user.Id,
                FullName = request.FullName,
                Phone = request.Phone,
                MemberType = MemberType.Regular,
                Points = 0,
                JoinDate = now
            };

            _context.Members.Add(member);
            await _context.SaveChangesAsync(cancellationToken);

            var session = CreateSession(user, member.MemberType.ToString());
            return AuthResult.Success(session, "/Customer/Index", "Member", "Registrasi berhasil");
        }

        public AuthenticatedSession CreateGuestSession()
            => new()
            {
                UserId = 0,
                Username = "Guest",
                Role = "Guest",
                IsMember = false
            };

        private async Task<AuthResult> BuildLoginResultAsync(
            User? user,
            string password,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return AuthResult.Failure(errorMessage);
            }

            user.LastLogin = _dateTimeProvider.Now;
            await _context.SaveChangesAsync(cancellationToken);

            var session = CreateSession(user, user.Member?.MemberType.ToString());
            return AuthResult.Success(session, ResolveRedirectUrl(user.Role), user.Role.ToString());
        }

        private static AuthenticatedSession CreateSession(User user, string? memberType)
            => new()
            {
                UserId = user.Id,
                Username = user.Username,
                Role = user.Role.ToString(),
                IsMember = user.Role == UserRole.Member,
                MemberType = user.Role == UserRole.Member ? memberType ?? MemberType.Regular.ToString() : null
            };

        private static string ResolveRedirectUrl(UserRole role)
            => role switch
            {
                UserRole.Owner => "/Dashboard",
                UserRole.Admin => "/Admin",
                UserRole.Supervisor => "/Supervisor/Assets",
                UserRole.Kasir => "/Kasir",
                UserRole.BagianMasak => "/Kitchen/Display",
                _ => "/Customer/Index"
            };
    }
}
