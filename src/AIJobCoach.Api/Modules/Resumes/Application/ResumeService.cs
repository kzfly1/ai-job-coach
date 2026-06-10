using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.Resumes.Domain;
using AIJobCoach.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace AIJobCoach.Api.Modules.Resumes.Application;

public sealed class ResumeService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;

    public ResumeService(AppDbContext dbContext, IFileStorageService fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public async Task<Result<ResumeDto>> UploadAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (fileStream is null)
        {
            throw new ArgumentNullException(nameof(fileStream));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            return Result<ResumeDto>.Failure(
                new Error("UNSUPPORTED_FILE_TYPE", "Only PDF and DOCX files are supported."));
        }
        
        if (fileSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileSizeBytes),
                "File size cannot be negative.");
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return Result<ResumeDto>.Failure(
                new Error("FILE_TOO_LARGE", "File size must not exceed 5 MB."));
        }

        var storagePath = await _fileStorage.SaveAsync(fileStream, fileName, contentType, userId, ct);

        var resume = new Resume(userId, fileName, storagePath, contentText: null);

        _dbContext.Resumes.Add(resume);
        await _dbContext.SaveChangesAsync(ct);

        return Result<ResumeDto>.Success(ToDto(resume));
    }

    public async Task<ResumeDto?> GetByIdAsync(
        Guid resumeId,
        Guid userId,
        CancellationToken ct = default)
    {
        var resume = await _dbContext.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == resumeId && r.UserId == userId && r.IsActive,
                ct);

        return resume is null ? null : ToDto(resume);
    }

    public async Task<IReadOnlyList<ResumeDto>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _dbContext.Resumes
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => new ResumeDto(
                r.Id,
                r.UserId,
                r.FileName,
                r.ContentText == null ? 0 : r.ContentText.Length,
                r.IsActive,
                r.UploadedAt))
            .ToListAsync(ct);
    }

    private static ResumeDto ToDto(Resume resume)
    {
        return new ResumeDto(
            resume.Id,
            resume.UserId,
            resume.FileName,
            resume.ContentText?.Length ?? 0,
            resume.IsActive,
            resume.UploadedAt);
    }
}
