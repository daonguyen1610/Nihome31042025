using System.Net;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class TranslationsControllerTests : IntegrationTestBase
{
    public TranslationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetByLang_IsPublic_ReturnsOk()
    {
        (await Client.GetAsync("/api/translations/en")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpsertPair_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync("/api/translations/pair", new
        {
            key = "test.key",
            vietnameseValue = "Xin chào",
            translations = new Dictionary<string, string> { ["en"] = "Hello" },
            category = "test",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpsertPair_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var key = $"test.key.{Guid.NewGuid():N}".Substring(0, 24);
        var res = await Client.PostAsJsonAsync("/api/translations/pair", new
        {
            key,
            vietnameseValue = "Xin chào",
            translations = new Dictionary<string, string> { ["en"] = "Hello", ["zh"] = "你好", ["ja"] = "こんにちは" },
            category = "test",
        });
        res.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        (await Client.DeleteAsync($"/api/translations/key/{key}")).StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
    }

    [Fact]
    public async Task EntityTypes_ReturnCompleteMetadata()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var response = await Client.GetAsync("/api/translations/entity/types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await ReadJsonAsync(response);
        types.GetArrayLength().Should().Be(11);
        var slideshow = types.EnumerateArray().Single(item =>
            item.GetProperty("type").GetString() == "Slideshow");
        slideshow.GetProperty("displayKey").GetString().Should().Be("translations.entityType.slideshow");
        slideshow.GetProperty("fields").EnumerateArray().Select(field => field.GetString())
            .Should().BeEquivalentTo("Title", "Subtitle", "LinkText");
        var project = types.EnumerateArray().Single(item =>
            item.GetProperty("type").GetString() == "Project");
        project.GetProperty("fieldFormats").GetProperty("Highlights").GetString().Should().Be("json");
    }

    [Fact]
    public async Task EntityTranslations_ViewOnlyRole_CannotSaveOrReset()
    {
        var entityId = await WithDbAsync(async db =>
        {
            var slideshow = new SlideshowItem
            {
                Slug = UniqueSlug("view-only-translation"),
                ImageUrl = "/images/view-only-translation.jpg",
                Title = "Nội dung gốc",
                IsActive = true,
            };
            db.SlideshowItems.Add(slideshow);
            await db.SaveChangesAsync();
            db.EntityTranslations.Add(new EntityTranslation
            {
                EntityType = EntityTypes.Slideshow,
                EntityId = slideshow.Id,
                FieldName = "Title",
                LanguageCode = "en",
                Value = "Existing translation",
            });
            await db.SaveChangesAsync();
            return slideshow.Id;
        });

        try
        {
            await AuthTestHelper.AuthenticateAsync(
                Client,
                client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));

            (await Client.GetAsync($"/api/translations/entity/Slideshow/{entityId}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            var saveResponse = await Client.PostAsJsonAsync(
                $"/api/translations/entity/Slideshow/{entityId}",
                new
                {
                    languageCode = "en",
                    translations = new Dictionary<string, string> { ["Title"] = "Unauthorized change" },
                });
            saveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await Client.DeleteAsync($"/api/translations/entity/Slideshow/{entityId}"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            await WithDbAsync(async db =>
            {
                var translations = await db.EntityTranslations
                    .Where(translation => translation.EntityType == EntityTypes.Slideshow
                        && translation.EntityId == entityId)
                    .ToListAsync();
                translations.Should().ContainSingle();
                translations[0].Value.Should().Be("Existing translation");
            });
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                db.EntityTranslations.RemoveRange(db.EntityTranslations.Where(translation =>
                    translation.EntityType == EntityTypes.Slideshow && translation.EntityId == entityId));
                db.SlideshowItems.RemoveRange(db.SlideshowItems.Where(item => item.Id == entityId));
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task EntityTranslations_DesignLead_CanSaveAndReset()
    {
        var entityId = await WithDbAsync(async db =>
        {
            var slideshow = new SlideshowItem
            {
                Slug = UniqueSlug("design-lead-translation"),
                ImageUrl = "/images/design-lead-translation.jpg",
                Title = "Nội dung gốc",
                IsActive = true,
            };
            db.SlideshowItems.Add(slideshow);
            await db.SaveChangesAsync();
            return slideshow.Id;
        });

        try
        {
            await AuthTestHelper.AuthenticateAsync(
                Client,
                client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN_LEAD"));

            var saveResponse = await Client.PostAsJsonAsync(
                $"/api/translations/entity/Slideshow/{entityId}",
                new
                {
                    languageCode = "en",
                    translations = new Dictionary<string, string> { ["Title"] = "Design lead translation" },
                });
            saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            await WithDbAsync(async db =>
            {
                var translation = await db.EntityTranslations.SingleAsync(item =>
                    item.EntityType == EntityTypes.Slideshow && item.EntityId == entityId);
                translation.Value.Should().Be("Design lead translation");
            });

            (await Client.DeleteAsync($"/api/translations/entity/Slideshow/{entityId}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            await WithDbAsync(async db =>
            {
                (await db.EntityTranslations.AnyAsync(item =>
                    item.EntityType == EntityTypes.Slideshow && item.EntityId == entityId)).Should().BeFalse();
            });
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                db.EntityTranslations.RemoveRange(db.EntityTranslations.Where(translation =>
                    translation.EntityType == EntityTypes.Slideshow && translation.EntityId == entityId));
                db.SlideshowItems.RemoveRange(db.SlideshowItems.Where(item => item.Id == entityId));
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task Slideshow_TranslationRoundTrip_UpdatesStatusAndPublicContent()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var slug = UniqueSlug("translated-slide");
        var createResponse = await Client.PostAsJsonAsync("/api/slideshow", new
        {
            slug,
            imageUrl = "/images/translated-slide.jpg",
            title = "Tiêu đề gốc",
            subtitle = "Phụ đề gốc",
            linkUrl = "/about",
            linkText = "Xem thêm",
            isActive = true,
            sortOrder = 90,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(createResponse)).GetProperty("id").GetInt32();

        try
        {
            foreach (var (language, title) in new[]
            {
                ("en", "English title"),
                ("zh", "中文标题"),
                ("ja", "日本語タイトル"),
            })
            {
                var saveResponse = await Client.PostAsJsonAsync(
                    $"/api/translations/entity/Slideshow/{id}",
                    new
                    {
                        languageCode = language,
                        translations = new Dictionary<string, string> { ["Title"] = title },
                    });
                saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            var statusResponse = await Client.GetAsync("/api/translations/entity/Slideshow");
            statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var status = await ReadJsonAsync(statusResponse);
            var item = status.GetProperty("items").EnumerateArray()
                .Single(candidate => candidate.GetProperty("id").GetInt32() == id);
            item.GetProperty("translationCount").GetInt32().Should().Be(3);
            item.GetProperty("expectedFields").GetInt32().Should().Be(9);

            var publicResponse = await Client.GetAsync($"/api/slideshow/{slug}?lang=zh");
            publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ReadJsonAsync(publicResponse)).GetProperty("title").GetString().Should().Be("中文标题");

            (await Client.DeleteAsync($"/api/translations/entity/Slideshow/{id}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            var resetResponse = await Client.GetAsync($"/api/slideshow/{slug}?lang=en");
            (await ReadJsonAsync(resetResponse)).GetProperty("title").GetString().Should().Be("Tiêu đề gốc");
        }
        finally
        {
            await Client.DeleteAsync($"/api/slideshow/{id}");
        }
    }

    [Fact]
    public async Task Project_HighlightsTranslation_IsReturnedByPublicApi()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var slug = UniqueSlug("translated-project");
        var projectId = await WithDbAsync(async db =>
        {
            var project = new NihomeBackend.Models.Project
            {
                Slug = slug,
                ImageUrl = "/images/project.jpg",
                Name = "Dự án dịch",
                Client = "Khách hàng",
                Location = "Hà Nội",
                Scale = "Nhỏ",
                Scope = "Xây dựng",
                HighlightsJson = "[{\"label\":\"Diện tích\",\"value\":\"100 m2\"}]",
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });

        try
        {
            var translatedHighlights = "[{\"label\":\"Area\",\"value\":\"100 m2\"}]";
            var saveResponse = await Client.PostAsJsonAsync(
                $"/api/translations/entity/Project/{projectId}",
                new
                {
                    languageCode = "en",
                    translations = new Dictionary<string, string> { ["Highlights"] = translatedHighlights },
                });
            saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var publicResponse = await Client.GetAsync($"/api/projects/{slug}?lang=en");
            publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var project = await ReadJsonAsync(publicResponse);
            project.GetProperty("highlights")[0].GetProperty("label").GetString().Should().Be("Area");
        }
        finally
        {
            await Client.DeleteAsync($"/api/projects/{projectId}");
        }
    }

    [Fact]
    public async Task GenericEntityTranslation_RejectsInvalidContractValues()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var ids = await WithDbAsync(async db =>
        {
            var slide = new NihomeBackend.Models.SlideshowItem
            {
                Slug = UniqueSlug("validation-slide"),
                ImageUrl = "/images/validation.jpg",
                Title = "Validation slide",
            };
            var project = new NihomeBackend.Models.Project
            {
                Slug = UniqueSlug("validation-project"),
                ImageUrl = "/images/project.jpg",
                Name = "Validation project",
                Client = "Client",
                Location = "Location",
                Scale = "Scale",
                Scope = "Scope",
                HighlightsJson = "[{\"label\":\"Diện tích\",\"value\":\"100 m2\"}]",
            };
            db.AddRange(slide, project);
            await db.SaveChangesAsync();
            return (SlideId: slide.Id, ProjectId: project.Id);
        });

        try
        {
            (await Client.GetAsync("/api/translations/entity/Unsupported"))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Client.GetAsync("/api/translations/entity/Slideshow/999999"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);

            var invalidLanguage = await Client.PostAsJsonAsync($"/api/translations/entity/Slideshow/{ids.SlideId}", new
            {
                languageCode = "vi",
                translations = new Dictionary<string, string> { ["Title"] = "Không hợp lệ" },
            });
            invalidLanguage.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var invalidField = await Client.PostAsJsonAsync($"/api/translations/entity/Slideshow/{ids.SlideId}", new
            {
                languageCode = "en",
                translations = new Dictionary<string, string> { ["ImageUrl"] = "/unsafe.jpg" },
            });
            invalidField.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var invalidJson = await Client.PostAsJsonAsync($"/api/translations/entity/Project/{ids.ProjectId}", new
            {
                languageCode = "en",
                translations = new Dictionary<string, string> { ["Highlights"] = "not-json" },
            });
            invalidJson.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var invalidStructure = await Client.PostAsJsonAsync($"/api/translations/entity/Project/{ids.ProjectId}", new
            {
                languageCode = "en",
                translations = new Dictionary<string, string> { ["Highlights"] = "{\"label\":\"Area\",\"value\":\"100 m2\"}" },
            });
            invalidStructure.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var missingSourceStructure = await Client.PostAsJsonAsync($"/api/translations/entity/Project/{ids.ProjectId}", new
            {
                languageCode = "en",
                translations = new Dictionary<string, string> { ["Challenges"] = "[\"Challenge\"]" },
            });
            missingSourceStructure.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                db.SlideshowItems.Remove((await db.SlideshowItems.FindAsync(ids.SlideId))!);
                db.Projects.Remove((await db.Projects.FindAsync(ids.ProjectId))!);
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task AsBuiltCategory_TranslationRoundTrip_UsesContentTranslations()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var typesRes = await Client.GetAsync("/api/translations/entity/types");
        typesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await ReadJsonAsync(typesRes);
        types.EnumerateArray().Should().Contain(item =>
            item.GetProperty("type").GetString() == "AsBuiltDocumentCategory");

        var code = $"Translated{Guid.NewGuid():N}"[..24];
        var createRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code,
            name = "Ảnh thi công",
            isRequired = false,
            isActive = true,
            sortOrder = 90,
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createRes);
        var id = created.GetProperty("id").GetInt32();
        created.GetProperty("nameEn").GetString().Should().Be("Ảnh thi công");

        var saveRes = await Client.PostAsJsonAsync(
            $"/api/translations/entity/AsBuiltDocumentCategory/{id}",
            new
            {
                languageCode = "en",
                translations = new Dictionary<string, string> { ["Name"] = "Construction photos" },
            });
        saveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusRes = await Client.GetAsync("/api/translations/entity/AsBuiltDocumentCategory");
        var status = await ReadJsonAsync(statusRes);
        var translatedItem = status.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == id);
        translatedItem.GetProperty("translationCount").GetInt32().Should().Be(1);
        translatedItem.GetProperty("expectedFields").GetInt32().Should().Be(3);

        var detailRes = await Client.GetAsync($"/api/translations/entity/AsBuiltDocumentCategory/{id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadJsonAsync(detailRes);
        detail.GetProperty("original").GetProperty("Name").GetString().Should().Be("Ảnh thi công");
        detail.GetProperty("translations").GetProperty("en").GetProperty("Name")
            .GetString().Should().Be("Construction photos");
        detail.GetProperty("translations").GetProperty("zh").GetProperty("Name")
            .GetString().Should().BeEmpty();

        var updateRes = await Client.PutAsJsonAsync($"/api/asbuilt-categories/{id}", new
        {
            code,
            name = "Hình ảnh thi công",
            isRequired = false,
            isActive = true,
            sortOrder = 90,
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadJsonAsync(updateRes);
        updated.GetProperty("nameEn").GetString().Should().Be("Construction photos");
        updated.GetProperty("nameZh").GetString().Should().Be("Hình ảnh thi công");
        updated.GetProperty("nameJa").GetString().Should().Be("Hình ảnh thi công");

        var resetRes = await Client.DeleteAsync($"/api/translations/entity/AsBuiltDocumentCategory/{id}");
        resetRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var resetCategoryRes = await Client.GetAsync($"/api/asbuilt-categories/{id}");
        var resetCategory = await ReadJsonAsync(resetCategoryRes);
        resetCategory.GetProperty("nameEn").GetString().Should().Be("Hình ảnh thi công");

        var missingRes = await Client.GetAsync("/api/translations/entity/AsBuiltDocumentCategory/999999");
        missingRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Client.DeleteAsync($"/api/asbuilt-categories/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("vi", "Tên tiếng Việt không hợp lệ")]
    [InlineData("en", "   ")]
    public async Task AsBuiltCategory_InvalidTranslation_ReturnsBadRequest(string languageCode, string value)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var categoriesRes = await Client.GetAsync("/api/asbuilt-categories");
        var categories = await ReadJsonAsync(categoriesRes);
        var id = categories[0].GetProperty("id").GetInt32();

        var res = await Client.PostAsJsonAsync(
            $"/api/translations/entity/AsBuiltDocumentCategory/{id}",
            new
            {
                languageCode,
                translations = new Dictionary<string, string> { ["Name"] = value },
            });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
