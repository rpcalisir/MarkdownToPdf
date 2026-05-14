using MarkdownToPdf.Web.Features.PdfGeneration.Jobs;
using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Shared.Core;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Generate;

public sealed class GeneratePdfHandler(
    IPdfGenerationQueue queue,
    IDistributedCache cache)
    : IRequestHandler<GeneratePdfCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(GeneratePdfCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var jobId = Guid.NewGuid();

            // Initialize the job state so the polling endpoint immediately recognizes it
            var initialState = new PdfJobState(jobId, JobStatus.Pending);
            var stateJson = JsonSerializer.Serialize(initialState);

            // ARCHITECTURAL FIX: Serializing to JSON and writing to IDistributedCache ensures 
            // the state is available to all nodes behind the load balancer.
            await cache.SetStringAsync(
                jobId.ToString(),
                stateJson,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            var job = new PdfGenerationJob(jobId, request.MarkdownText!);

            // Instantly offload the heavy work to the Background Channel
            await queue.QueueJobAsync(job, cancellationToken);

            return Result<Guid>.Success(jobId);
        }
        catch (Exception)
        {
            return Result<Guid>.Failure(DomainErrors.PdfGeneration.FailedToGenerate);
        }
    }
}