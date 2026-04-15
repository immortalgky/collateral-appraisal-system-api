using Appraisal.Contracts.Appraisals.Dto.Income;

namespace Appraisal.Application.Features.PricingAnalysis.GetIncomeAnalysis;

/// <summary>
/// Returns null Analysis when no IncomeAnalysis has been saved for this method yet.
/// </summary>
public record GetIncomeAnalysisResult(IncomeAnalysisDto? Analysis);
