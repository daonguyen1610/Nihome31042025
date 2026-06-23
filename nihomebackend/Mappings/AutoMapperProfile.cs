using AutoMapper;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Mappings;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<ApplicationUser, AuthResponse>()
            .ForMember(d => d.UserId, opt => opt.MapFrom(s => s.Id))
            // Prefer the canonical RBAC code when the user is linked to a role
            // row; fall back to the legacy enum string for unlinked accounts.
            // Callers that need an accurate value for users with custom roles
            // must either Include(u => u.RoleEntity) or override Role after
            // mapping.
            .ForMember(d => d.Role, opt => opt.MapFrom(s =>
                s.RoleEntity != null ? s.RoleEntity.Code : s.Role.ToString()))
            .ForMember(d => d.RoleId, opt => opt.MapFrom(s => s.RoleEntityId))
            .ForMember(d => d.AccessToken, opt => opt.Ignore())
            .ForMember(d => d.RefreshToken, opt => opt.Ignore())
            .ForMember(d => d.ExpiresAt, opt => opt.Ignore())
            .ForMember(d => d.OtpRequired, opt => opt.Ignore());
    }
}
