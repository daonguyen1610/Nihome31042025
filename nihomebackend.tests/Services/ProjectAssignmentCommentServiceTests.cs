using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectAssignmentCommentServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly ProjectAssignmentCommentService _sut;
    private readonly int _projectId;
    private readonly int _assignmentId;
    private readonly int _authorId;
    private readonly int _otherUserId;

    public ProjectAssignmentCommentServiceTests()
    {
        var author = AddUser("0902000001", "Comment Author");
        var other = AddUser("0902000002", "Other User");
        _authorId = author.Id;
        _otherUserId = other.Id;

        var customer = new Customer { Name = "Comment customer", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new OperationalProject
        {
            Code = "OP-COMMENT",
            Name = "Comment project",
            CustomerId = customer.Id,
        };
        _db.OperationalProjects.Add(project);
        _db.SaveChanges();
        _projectId = project.Id;

        var member = new OperationalProjectMember
        {
            OperationalProjectId = project.Id,
            UserId = author.Id,
            Position = "Architect",
            StartedAt = DateTime.UtcNow,
        };
        _db.OperationalProjectMembers.Add(member);
        _db.SaveChanges();

        var assignment = new OperationalProjectAssignment
        {
            OperationalProjectId = project.Id,
            WorkKey = "DES-COMMENT",
            Title = "Comment assignment",
            Module = "Design",
            AssigneeMemberId = member.Id,
            Status = ProjectAssignmentStatus.Planned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.OperationalProjectAssignments.Add(assignment);
        _db.SaveChanges();
        _assignmentId = assignment.Id;

        _sut = new ProjectAssignmentCommentService(_db);
    }

    [Fact]
    public async Task AddAndList_ReturnsCommentsInChronologicalOrderWithAuthorName()
    {
        var first = await _sut.AddAsync(
            _projectId, _assignmentId, null, "  First note  ", _authorId, CancellationToken.None);
        var second = await _sut.AddAsync(
            _projectId, _assignmentId, null, "Second note", _otherUserId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal("First note", first.Body);
        Assert.Equal("Comment Author", first.CreatedByName);

        var list = await _sut.ListAsync(
            _projectId, _assignmentId, null, CancellationToken.None);

        Assert.NotNull(list);
        Assert.Equal(new[] { first.Id, second!.Id }, list.Select(comment => comment.Id));
        Assert.Equal(new[] { "First note", "Second note" }, list.Select(comment => comment.Body));
    }

    [Fact]
    public async Task AddAsync_RejectsBlankComment()
    {
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _sut.AddAsync(_projectId, _assignmentId, null, "   ", _authorId, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_ReturnsNullForUnknownAssignment()
    {
        var result = await _sut.AddAsync(
            _projectId, 999_999, null, "Note", _authorId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_AllowsAuthorOnlyUnlessManager()
    {
        var comment = await _sut.AddAsync(
            _projectId, _assignmentId, null, "Own comment", _authorId, CancellationToken.None);

        var byOther = await _sut.DeleteAsync(
            _projectId, _assignmentId, null, comment!.Id, _otherUserId, canManage: false,
            CancellationToken.None);
        Assert.False(byOther);

        var byManager = await _sut.DeleteAsync(
            _projectId, _assignmentId, null, comment.Id, _otherUserId, canManage: true,
            CancellationToken.None);
        Assert.True(byManager);

        var list = await _sut.ListAsync(
            _projectId, _assignmentId, null, CancellationToken.None);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task ListAsync_ReturnsNullWhenDesignScopeDoesNotMatch()
    {
        var result = await _sut.ListAsync(
            _projectId, _assignmentId, designProjectId: 12345, CancellationToken.None);

        Assert.Null(result);
    }

    private ApplicationUser AddUser(string phone, string name)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
            Email = $"{phone}@nihome.test",
            PasswordHash = "test",
            IsActive = true,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    public void Dispose() => _db.Dispose();
}
