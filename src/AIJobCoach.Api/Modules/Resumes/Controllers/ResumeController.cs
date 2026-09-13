using AIJobCoach.Api.Common.Http;
using AIJobCoach.Api.Modules.Resumes.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIJobCoach.Api.Modules.Resumes.Controllers;

[ApiController]
[Authorize]
[Route("api/resumes")]
public sealed class ResumeController : ControllerBase
{
    private readonly ResumeService _resumeService;

    public ResumeController(ResumeService resumeService)
    {
        _resumeService = resumeService;
    }

    /// <summary>
    /// Gets all active resumes that belong to the current authenticated user.
    /// </summary>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>A list of resume summaries.</returns>
    [HttpGet]
    public async Task<IActionResult> GetResumes(CancellationToken ct)
    {
        if (!this.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var resumes = await _resumeService.ListByUserAsync(userId, ct);

        return Ok(resumes);
    }

    /// <summary>
    /// Gets a single active resume by id for the current authenticated user.
    /// </summary>
    /// <param name="id">The resume id.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The resume summary if found; otherwise 404.</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetResume(Guid id, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var resume = await _resumeService.GetByIdAsync(id, userId, ct);

        if (resume is null)
        {
            return NotFound(new
            {
                code = "RESUME_NOT_FOUND",
                message = "Resume was not found."
            });
        }

        return Ok(resume);
    }

    /// <summary>
    /// Uploads a resume file for the current authenticated user.
    /// </summary>
    /// <param name="file">The resume file to upload.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The created resume summary.</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadResume([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return UnprocessableEntity(new
            {
                code = "FILE_REQUIRED",
                message = "A resume file is required."
            });
        }

        if (!this.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await using var stream = file.OpenReadStream();

        var result = await _resumeService.UploadAsync(
            userId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            ct);

        if (!result.IsSuccess)
        {
            return this.ToErrorActionResult(result.Error);
        }

        return CreatedAtAction(
            nameof(GetResume),
            new { id = result.Value!.Id },
            result.Value);
    }

    /// <summary>
    /// Soft-deletes a resume owned by the current authenticated user.
    /// </summary>
    /// <param name="id">The resume id.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>204 No Content on success; 404 if the resume was not found.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteResume(Guid id, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _resumeService.SoftDeleteAsync(id, userId, ct);

        if (!result.IsSuccess)
        {
            return this.ToErrorActionResult(result.Error);
        }

        return NoContent();
    }
}
