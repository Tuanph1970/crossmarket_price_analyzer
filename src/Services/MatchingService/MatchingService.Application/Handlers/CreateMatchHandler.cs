using Common.Domain.Enums;
using MatchingService.Application.Commands;
using MatchingService.Application.DTOs;
using MatchingService.Application.Persistence;
using MatchingService.Application.Services;
using MatchingService.Domain.Entities;

namespace MatchingService.Application.Handlers;

public class CreateMatchHandler : MediatR.IRequestHandler<CreateMatchCommand, ProductMatchDto>
{
    private readonly ProductMatchRepository _repository;
    private readonly FuzzyMatchingService _fuzzyService;

    public CreateMatchHandler(ProductMatchRepository repository, FuzzyMatchingService fuzzyService)
    {
        _repository = repository;
        _fuzzyService = fuzzyService;
    }

    public async Task<ProductMatchDto> Handle(CreateMatchCommand request, CancellationToken ct)
    {
        var score = _fuzzyService.ComputeMatchScore(
            request.UsProductName,
            request.VnProductName,
            request.UsBrand,
            request.VnBrand);

        var match = ProductMatch.Create(
            request.UsProductId,
            request.VnProductId,
            score);

        await _repository.AddAsync(match, ct);

        return ToDto(match);
    }

    private static ProductMatchDto ToDto(ProductMatch m) =>
        new(
            m.Id,
            m.UsProductId,
            m.VnProductId,
            m.ConfidenceScore,
            m.GetConfidenceLevel(),
            m.Status,
            m.ConfirmedBy,
            m.ConfirmedAt,
            m.CreatedAt,
            null
        );
}
