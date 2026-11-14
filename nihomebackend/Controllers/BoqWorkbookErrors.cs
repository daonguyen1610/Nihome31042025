using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

// The 400 body for a rejected BOQ workbook: the cause plus up to 200 cell-level issues.
internal static class BoqWorkbookErrors
{
    public static object Body(BoqWorkbookException error) => new
    {
        error.Code,
        messageKey = $"boqWorkbook.error.{error.Code}",
        error.Limit,
        issueCount = error.Result?.IssueCount ?? 0,
        issues = error.Result?.Issues.Select(issue => new
        {
            issue.Code,
            messageKey = $"boqWorkbook.error.{issue.Code}",
            issue.Sheet,
            issue.Cell,
            issue.Limit,
        }),
    };
}
