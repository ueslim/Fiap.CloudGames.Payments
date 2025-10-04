using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FIAP.CloudGames.WebAPI.Core.User
{
    public class AspNetUser : IAspNetUser
    {
        private readonly IHttpContextAccessor _accessor;

        public AspNetUser(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public string Name => _accessor?.HttpContext?.User?.Identity?.Name ?? string.Empty;

        public Guid GetUserId()
        {
            var userId = _accessor?.HttpContext?.User?.GetUserId();
            return IsAuthenticated() && Guid.TryParse(userId, out var guid) ? guid : Guid.Empty;
        }

        public string GetUserEmail()
        {
            return IsAuthenticated() ? (_accessor?.HttpContext?.User?.GetUserEmail() ?? string.Empty) : string.Empty;
        }

        public string GetUserToken()
        {
            return IsAuthenticated() ? (_accessor?.HttpContext?.User?.GetUserToken() ?? string.Empty) : string.Empty;
        }

        public bool IsAuthenticated()
        {
            return _accessor?.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        }

        public bool HasRole(string role)
        {
            return _accessor?.HttpContext?.User?.IsInRole(role) ?? false;
        }

        public IEnumerable<Claim> GetClaims()
        {
            return _accessor?.HttpContext?.User?.Claims ?? Enumerable.Empty<Claim>();
        }

        public HttpContext? GetHttpContext()
        {
            return _accessor?.HttpContext;
        }
    }
}