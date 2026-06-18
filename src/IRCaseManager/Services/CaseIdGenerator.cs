using IRCaseManager.Data;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Services;

public class CaseIdGenerator(AppDbContext db)
{
    public async Task<string> GenerateAsync(DateTimeOffset openedAt)
    {
        var year = openedAt.Year;
        var prefix = $"Case-{year}-";
        var lastCaseId = await db.Cases
            .Where(irCase => irCase.CaseId.StartsWith(prefix))
            .OrderByDescending(irCase => irCase.CaseId)
            .Select(irCase => irCase.CaseId)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastCaseId is not null && int.TryParse(lastCaseId[^5..], out var previousNumber))
        {
            nextNumber = previousNumber + 1;
        }

        return $"{prefix}{nextNumber:00000}";
    }
}
