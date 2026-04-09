using MatchingService.Application.Commands;
using MatchingService.Application.Persistence;
using MatchingService.Domain.Entities;

namespace MatchingService.Application.Handlers;

public class ConfirmMatchHandler : MediatR.IRequestHandler<ConfirmMatchCommand, bool>
{
    private readonly ProductMatchRepository _repository;

    public ConfirmMatchHandler(ProductMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ConfirmMatchCommand request, CancellationToken ct)
    {
        var match = await _repository.GetByIdAsync(request.MatchId, ct);
        if (match is null) return false;

        match.Confirm(request.UserId, request.Notes);
        await _repository.UpdateAsync(match, ct);
        return true;
    }
}

public class RejectMatchHandler : MediatR.IRequestHandler<RejectMatchCommand, bool>
{
    private readonly ProductMatchRepository _repository;

    public RejectMatchHandler(ProductMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(RejectMatchCommand request, CancellationToken ct)
    {
        var match = await _repository.GetByIdAsync(request.MatchId, ct);
        if (match is null) return false;

        match.Reject(request.UserId, request.Notes);
        await _repository.UpdateAsync(match, ct);
        return true;
    }
}

public class BatchReviewHandler : MediatR.IRequestHandler<BatchReviewCommand, int>
{
    private readonly ProductMatchRepository _repository;

    public BatchReviewHandler(ProductMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(BatchReviewCommand request, CancellationToken ct)
    {
        var processed = 0;
        foreach (var item in request.Items)
        {
            var match = await _repository.GetByIdAsync(item.MatchId, ct);
            if (match is null || match.Status != Common.Domain.Enums.MatchStatus.Pending)
                continue;

            if (item.Action.Equals("Confirm", StringComparison.OrdinalIgnoreCase))
                match.Confirm(request.UserId);
            else if (item.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
                match.Reject(request.UserId);
            else
                continue;

            await _repository.UpdateAsync(match, ct);
            processed++;
        }
        return processed;
    }
}
