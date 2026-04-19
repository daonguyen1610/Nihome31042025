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
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.AccessToken, opt => opt.Ignore())
            .ForMember(d => d.RefreshToken, opt => opt.Ignore())
            .ForMember(d => d.ExpiresAt, opt => opt.Ignore())
            .ForMember(d => d.OtpRequired, opt => opt.Ignore());
    }
}
