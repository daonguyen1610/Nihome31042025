using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class RecruitmentMetadataService(TranslationService translationService)
{
    private static readonly (string Value, string Key)[] EmploymentTypes =
    [
        ("full-time", "recruit.meta.employment.fullTime"),
        ("part-time", "recruit.meta.employment.partTime"),
        ("intern", "recruit.meta.employment.intern"),
    ];

    private static readonly (string Value, string Key)[] ExperienceLevels =
    [
        ("student", "recruit.meta.experience.student"),
        ("junior", "recruit.meta.experience.junior"),
        ("mid", "recruit.meta.experience.mid"),
        ("senior", "recruit.meta.experience.senior"),
    ];

    private static readonly (string Value, string Key)[] ApplicationStatuses =
    [
        ("new", "recruit.meta.status.new"),
        ("interview", "recruit.meta.status.interview"),
        ("hired", "recruit.meta.status.hired"),
        ("rejected", "recruit.meta.status.rejected"),
    ];

    public async Task<RecruitmentMetadataResponse> GetAsync(string lang = "vi")
    {
        var translations = await translationService.GetTranslationMapAsync(lang);

        return new RecruitmentMetadataResponse
        {
            EmploymentTypes = MapOptions(EmploymentTypes, translations),
            ExperienceLevels = MapOptions(ExperienceLevels, translations),
            ApplicationStatuses = MapOptions(ApplicationStatuses, translations),
        };
    }

    private static List<RecruitmentOptionResponse> MapOptions(
        IEnumerable<(string Value, string Key)> definitions,
        IReadOnlyDictionary<string, string> translations)
    {
        return definitions
            .Select(item => new RecruitmentOptionResponse
            {
                Value = item.Value,
                Label = translations.TryGetValue(item.Key, out var label) ? label : item.Value,
            })
            .ToList();
    }
}
