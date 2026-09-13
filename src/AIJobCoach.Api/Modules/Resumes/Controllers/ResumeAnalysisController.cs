using AIJobCoach.Api.Common.Http;
using AIJobCoach.Api.Modules.Resumes.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIJobCoach.Api.Modules.Resumes.Controllers;

[ApiController]
[Authorize]
[Route("api/resumes")]
public sealed class ResumeAnalysisController : ControllerBase
{
    private readonly ResumeAnalysisService _resumeAnalysisService;

    public ResumeAnalysisController(ResumeAnalysisService resumeAnalysisService)
    {
        _resumeAnalysisService = resumeAnalysisService;
    }

    /// <summary>
    /// Returns the Developer Profile for a resume, analysing it if it has not been
    /// analysed yet. An existing profile is returned as-is unless re-analysis is forced.
    /// </summary>
    /// <param name="id">The resume id.</param>
    /// <param name="force">True to discard the stored profile and analyse again.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    [HttpPost("{id:guid}/analyse")]
    public async Task<IActionResult> AnalyseResume(
        Guid id,
        [FromQuery] bool force,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _resumeAnalysisService.AnalyseAsync(id, userId, force, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.ToErrorActionResult(result.Error);
    }

    /// <summary>
    /// Gets the stored Developer Profile for a resume.
    /// </summary>
    /// <param name="id">The resume id.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    [HttpGet("{id:guid}/analysis")]
    public async Task<IActionResult> GetAnalysis(Guid id, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _resumeAnalysisService.GetAnalysisAsync(id, userId, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.ToErrorActionResult(result.Error);
    }
}
