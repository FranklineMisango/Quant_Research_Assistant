using Microsoft.SemanticKernel;
using System.ComponentModel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Cross-Asset Analytics Plugin - Exposes multi-asset analysis, currency hedging, and arbitrage detection
/// </summary>
public class CrossAssetAnalyticsPlugin
{
    private readonly CrossAssetAnalyticsService _crossAssetService;

    public CrossAssetAnalyticsPlugin(CrossAssetAnalyticsService crossAssetService)
    {
        _crossAssetService = crossAssetService;
    }

    [KernelFunction("analyze_cross_asset_correlations")]
    [Description("Analyze correlations and relationships across multiple asset classes")]
    public async Task<string> AnalyzeCrossAssetCorrelationsAsync(
        [Description("Comma-separated list of symbols (e.g., AAPL,SPY,BTCUSDT,EURUSD)")] string symbols,
        [Description("Analysis window in days (default: 252 for 1 year)")] int windowDays = 252)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim()).ToList();
            var analysisWindow = TimeSpan.FromDays(windowDays);

            var analytics = await _crossAssetService.PerformCrossAssetAnalysisAsync(symbolList, analysisWindow);

            var result = $"Cross-Asset Correlation Analysis:\n";
            result += $"Symbols: {string.Join(", ", symbolList)}\n";
            result += $"Analysis Window: {windowDays} days\n";
            result += $"Analysis Date: {analytics.AnalysisDate:yyyy-MM-dd}\n\n";

            if (analytics.Correlations.Any())
            {
                result += "Asset Pair Correlations:\n";
                result += "Asset 1\t\tAsset 2\t\tCorrelation\t30D Rolling\tStability\n";
                result += new string('-', 75) + "\n";

                foreach (var corr in analytics.Correlations.OrderByDescending(c => Math.Abs(c.Correlation)))
                {
                    var stability = corr.IsStable ? "Stable" : "Variable";
                    result += $"{corr.Asset1,-10}\t{corr.Asset2,-10}\t{corr.Correlation:F3}\t\t{corr.RollingCorrelation30D:F3}\t\t{stability}\n";
                }

                result += "\nKey Correlation Insights:\n";
                var strongPositive = analytics.Correlations.Where(c => c.Correlation > 0.7).ToList();
                var strongNegative = analytics.Correlations.Where(c => c.Correlation < -0.5).ToList();
                var unstable = analytics.Correlations.Where(c => !c.IsStable).ToList();

                if (strongPositive.Any())
                {
                    result += $"- Strong positive correlations ({strongPositive.Count}): Risk concentration concerns\n";
                    var strongest = strongPositive.OrderByDescending(c => c.Correlation).First();
                    result += $"  Strongest: {strongest.Asset1}-{strongest.Asset2} ({strongest.Correlation:F3})\n";
                }

                if (strongNegative.Any())
                {
                    result += $"- Strong negative correlations ({strongNegative.Count}): Natural hedging opportunities\n";
                    var strongest = strongNegative.OrderBy(c => c.Correlation).First();
                    result += $"  Strongest: {strongest.Asset1}-{strongest.Asset2} ({strongest.Correlation:F3})\n";
                }

                if (unstable.Any())
                {
                    result += $"- Unstable correlations ({unstable.Count}): Regime changes or structural breaks\n";
                }

                result += $"- Stable relationships: {analytics.Correlations.Count(c => c.IsStable)} out of {analytics.Correlations.Count}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing cross-asset correlations: {ex.Message}";
        }
    }

    [KernelFunction("analyze_currency_exposure")]
    [Description("Analyze currency exposure and generate hedging recommendations")]
    public async Task<string> AnalyzeCurrencyExposureAsync(
        [Description("Comma-separated list of symbols to analyze (e.g., AAPL,ASML.AS,7203.T)")] string symbols)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim()).ToList();
            var analytics = await _crossAssetService.PerformCrossAssetAnalysisAsync(symbolList, TimeSpan.FromDays(252));

            var result = $"Currency Exposure Analysis:\n";
            result += $"Analyzed Symbols: {string.Join(", ", symbolList)}\n\n";

            if (analytics.CurrencyExposure.Exposures.Any())
            {
                result += "Currency Exposures:\n";
                result += "Currency\tExposure Amount\t% of Total\n";
                result += new string('-', 40) + "\n";

                foreach (var exposure in analytics.CurrencyExposure.Exposures.OrderByDescending(e => Math.Abs(e.Value)))
                {
                    var percentage = analytics.CurrencyExposure.TotalExposure > 0 ? 
                        Math.Abs(exposure.Value) / analytics.CurrencyExposure.TotalExposure : 0;
                    result += $"{exposure.Key}\t\t${exposure.Value:N0}\t\t{percentage:P1}\n";
                }

                result += $"\nTotal Exposure: ${analytics.CurrencyExposure.TotalExposure:N0}\n\n";

                if (analytics.CurrencyExposure.RecommendedHedges.Any())
                {
                    result += "Hedging Recommendations:\n";
                    result += "Currency\tExposure\tHedge Ratio\tInstrument\t\tCost\n";
                    result += new string('-', 65) + "\n";

                    foreach (var hedge in analytics.CurrencyExposure.RecommendedHedges)
                    {
                        result += $"{hedge.Currency}\t\t${hedge.ExposureAmount:N0}\t{hedge.HedgeRatio:P0}\t\t{hedge.HedgeInstrument}\t${hedge.Cost:N0}\n";
                    }

                    result += "\nHedging Strategy Recommendations:\n";
                    var totalHedgingCost = analytics.CurrencyExposure.RecommendedHedges.Sum(h => h.Cost);
                    var costRatio = totalHedgingCost / Math.Max(analytics.CurrencyExposure.TotalExposure, 1);

                    result += $"- Total hedging cost: ${totalHedgingCost:N0} ({costRatio:P2} of exposure)\n";
                    
                    if (costRatio > 0.02)
                        result += "- HIGH COST: Consider selective hedging or netting exposures\n";
                    else if (costRatio > 0.005)
                        result += "- MODERATE COST: Cost-effective hedging available\n";
                    else
                        result += "- LOW COST: Attractive hedging opportunity\n";

                    var largestExposure = analytics.CurrencyExposure.Exposures
                        .OrderByDescending(e => Math.Abs(e.Value)).First();
                    if (Math.Abs(largestExposure.Value) / analytics.CurrencyExposure.TotalExposure > 0.3)
                    {
                        result += $"- CONCENTRATION RISK: {largestExposure.Key} exposure is significant\n";
                        result += "- Consider portfolio rebalancing or targeted hedging\n";
                    }
                }
                else
                {
                    result += "No significant currency exposures requiring hedging.\n";
                }
            }
            else
            {
                result += "No currency exposures detected or all positions are USD-denominated.\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing currency exposure: {ex.Message}";
        }
    }

    [KernelFunction("detect_arbitrage_opportunities")]
    [Description("Detect statistical arbitrage opportunities across assets")]
    public async Task<string> DetectArbitrageOpportunitiesAsync(
        [Description("Comma-separated list of symbols (e.g., AAPL,MSFT,GOOGL)")] string symbols,
        [Description("Analysis window in days (default: 252 for 1 year)")] int windowDays = 252,
        [Description("Z-score threshold for signal (default: 2.0)")] double zScoreThreshold = 2.0)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim()).ToList();
            var analysisWindow = TimeSpan.FromDays(windowDays);

            var analytics = await _crossAssetService.PerformCrossAssetAnalysisAsync(symbolList, analysisWindow);

            var result = $"Statistical Arbitrage Opportunities:\n";
            result += $"Symbols: {string.Join(", ", symbolList)}\n";
            result += $"Analysis Window: {windowDays} days\n";
            result += $"Z-Score Threshold: {zScoreThreshold:F1}\n";
            result += $"Analysis Date: {analytics.AnalysisDate:yyyy-MM-dd}\n\n";

            if (analytics.ArbitrageOpportunities.Any())
            {
                result += "Detected Arbitrage Opportunities:\n";
                result += "Asset 1\t\tAsset 2\t\tZ-Score\tConfidence\tStrategy\n";
                result += new string('-', 80) + "\n";

                foreach (var opportunity in analytics.ArbitrageOpportunities.Take(10)) // Top 10
                {
                    result += $"{opportunity.Asset1,-10}\t{opportunity.Asset2,-10}\t{opportunity.ZScore:F2}\t{opportunity.Confidence:P0}\t\t{opportunity.Strategy}\n";
                }

                result += "\nOpportunity Analysis:\n";
                var highConfidence = analytics.ArbitrageOpportunities.Where(o => o.Confidence > 0.7).ToList();
                var extremeZScore = analytics.ArbitrageOpportunities.Where(o => o.ZScore > 3.0).ToList();

                result += $"- Total opportunities: {analytics.ArbitrageOpportunities.Count}\n";
                result += $"- High confidence (>70%): {highConfidence.Count}\n";
                result += $"- Extreme Z-scores (>3.0): {extremeZScore.Count}\n";

                if (highConfidence.Any())
                {
                    var topOpportunity = highConfidence.OrderByDescending(o => o.Confidence * o.ZScore).First();
                    result += $"\nTop Opportunity:\n";
                    result += $"- Pair: {topOpportunity.Asset1} / {topOpportunity.Asset2}\n";
                    result += $"- Current spread: {topOpportunity.PriceSpread:F4}\n";
                    result += $"- Historical mean: {topOpportunity.HistoricalMeanSpread:F4}\n";
                    result += $"- Z-Score: {topOpportunity.ZScore:F2}\n";
                    result += $"- Confidence: {topOpportunity.Confidence:P1}\n";
                    result += $"- Strategy: {topOpportunity.Strategy}\n";

                    result += $"\nRisk Considerations:\n";
                    if (topOpportunity.ZScore > 4.0)
                        result += "- EXTREME SIGNAL: High potential but verify fundamentals\n";
                    else if (topOpportunity.ZScore > 2.5)
                        result += "- STRONG SIGNAL: Good risk/reward ratio\n";
                    else
                        result += "- MODERATE SIGNAL: Conservative opportunity\n";

                    if (topOpportunity.Confidence > 0.8)
                        result += "- HIGH CONFIDENCE: Strong historical mean reversion\n";
                    else
                        result += "- MODERATE CONFIDENCE: Monitor for regime changes\n";
                }

                result += $"\nImplementation Notes:\n";
                result += "- Verify fundamental reasons for spread divergence\n";
                result += "- Use appropriate position sizing based on volatility\n";
                result += "- Set stop-losses for structural breaks\n";
                result += "- Monitor correlation stability during trade\n";
            }
            else
            {
                result += "No statistical arbitrage opportunities detected above the threshold.\n";
                result += "\nSuggestions:\n";
                result += "- Lower Z-score threshold for more opportunities\n";
                result += "- Expand universe to include more correlated assets\n";
                result += "- Check for seasonal or regime-dependent patterns\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error detecting arbitrage opportunities: {ex.Message}";
        }
    }

    [KernelFunction("generate_hedge_recommendations")]
    [Description("Generate hedge recommendations for portfolio protection")]
    public async Task<string> GenerateHedgeRecommendationsAsync(
        [Description("Comma-separated list of symbols to hedge (e.g., AAPL,GOOGL,MSFT)")] string symbols,
        [Description("Analysis window in days (default: 252 for 1 year)")] int windowDays = 252)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim()).ToList();
            var analysisWindow = TimeSpan.FromDays(windowDays);

            var analytics = await _crossAssetService.PerformCrossAssetAnalysisAsync(symbolList, analysisWindow);

            var result = $"Portfolio Hedge Recommendations:\n";
            result += $"Assets to Hedge: {string.Join(", ", symbolList)}\n";
            result += $"Analysis Window: {windowDays} days\n";
            result += $"Analysis Date: {analytics.AnalysisDate:yyyy-MM-dd}\n\n";

            if (analytics.HedgeRecommendations.Any())
            {
                result += "Recommended Hedges:\n";
                result += "Asset to Hedge\tHedge Instrument\tHedge Ratio\tEffectiveness\tRationale\n";
                result += new string('-', 90) + "\n";

                foreach (var hedge in analytics.HedgeRecommendations.Take(10)) // Top 10
                {
                    result += $"{hedge.AssetToHedge,-12}\t{hedge.HedgeInstrument,-15}\t{hedge.HedgeRatio:P0}\t\t{hedge.EffectivenessScore:F2}\t\t{hedge.Rationale}\n";
                }

                result += "\nHedge Portfolio Analysis:\n";
                var highEffectiveness = analytics.HedgeRecommendations.Where(h => h.EffectivenessScore > 0.7).ToList();
                var averageEffectiveness = analytics.HedgeRecommendations.Average(h => h.EffectivenessScore);

                result += $"- High effectiveness hedges (>0.7): {highEffectiveness.Count}\n";
                result += $"- Average hedge effectiveness: {averageEffectiveness:F2}\n";

                if (highEffectiveness.Any())
                {
                    var bestHedge = highEffectiveness.OrderByDescending(h => h.EffectivenessScore).First();
                    result += $"\nBest Hedge Opportunity:\n";
                    result += $"- Asset: {bestHedge.AssetToHedge}\n";
                    result += $"- Hedge: {bestHedge.HedgeInstrument}\n";
                    result += $"- Ratio: {bestHedge.HedgeRatio:P0}\n";
                    result += $"- Effectiveness: {bestHedge.EffectivenessScore:F2}\n";
                    result += $"- Rationale: {bestHedge.Rationale}\n";
                }

                result += $"\nHedging Strategy Guidelines:\n";
                if (averageEffectiveness > 0.6)
                {
                    result += "- GOOD HEDGING ENVIRONMENT: Effective hedges available\n";
                    result += "- Implement systematic hedging for risk management\n";
                }
                else if (averageEffectiveness > 0.3)
                {
                    result += "- MODERATE HEDGING ENVIRONMENT: Selective hedging recommended\n";
                    result += "- Focus on highest effectiveness hedges only\n";
                }
                else
                {
                    result += "- POOR HEDGING ENVIRONMENT: Limited effective hedges\n";
                    result += "- Consider position sizing or diversification instead\n";
                }

                var assetsCovered = analytics.HedgeRecommendations.Select(h => h.AssetToHedge).Distinct().Count();
                var hedgeCoverage = (double)assetsCovered / symbolList.Count;
                result += $"- Hedge coverage: {hedgeCoverage:P0} of portfolio assets\n";

                if (hedgeCoverage < 0.5)
                    result += "- LOW COVERAGE: Consider broader hedge instruments or ETFs\n";
            }
            else
            {
                result += "No effective hedge instruments found for the specified assets.\n";
                result += "\nAlternative Risk Management:\n";
                result += "- Consider broader market hedges (VIX, SPY puts)\n";
                result += "- Implement position sizing based on correlation\n";
                result += "- Use sector rotation or geographic diversification\n";
                result += "- Monitor portfolio concentration risk\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error generating hedge recommendations: {ex.Message}";
        }
    }

    [KernelFunction("calculate_optimal_allocation")]
    [Description("Calculate optimal asset allocation across asset classes")]
    public async Task<string> CalculateOptimalAllocationAsync(
        [Description("Comma-separated list of symbols representing different asset classes")] string symbols,
        [Description("Target annual return (as decimal, e.g., 0.08 for 8%)")] double targetReturn = 0.08,
        [Description("Risk tolerance (0-1, where 1 is highest risk tolerance)")] double riskTolerance = 0.5)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim()).ToList();
            var allocation = await _crossAssetService.CalculateOptimalAllocationAsync(symbolList, targetReturn, riskTolerance);

            var result = $"Optimal Asset Allocation:\n";
            result += $"Target Return: {targetReturn:P2} annually\n";
            result += $"Risk Tolerance: {riskTolerance:P0}\n";
            result += $"Assets Analyzed: {string.Join(", ", symbolList)}\n\n";

            if (allocation.Any())
            {
                result += "Recommended Allocation:\n";
                result += "Asset Class\t\tAllocation\tRisk Contribution\n";
                result += new string('-', 50) + "\n";

                foreach (var alloc in allocation.OrderByDescending(a => a.Value))
                {
                    var riskContrib = alloc.Value * riskTolerance; // Simplified risk contribution
                    result += $"{alloc.Key,-15}\t{alloc.Value:P1}\t\t{riskContrib:P1}\n";
                }

                result += "\nAllocation Analysis:\n";
                var diversification = allocation.Count(a => a.Value > 0.05); // Assets with >5% allocation
                var maxAllocation = allocation.Values.Max();
                var concentrationRisk = maxAllocation > 0.4;

                result += $"- Diversification: {diversification} asset classes with meaningful allocation\n";
                result += $"- Maximum allocation: {maxAllocation:P1}\n";
                result += $"- Concentration risk: {(concentrationRisk ? "HIGH" : "MODERATE")}\n";

                if (riskTolerance > 0.7)
                {
                    result += "- High risk tolerance: Growth-oriented allocation\n";
                    result += "- Consider higher equity/alternatives allocation\n";
                }
                else if (riskTolerance < 0.3)
                {
                    result += "- Low risk tolerance: Conservative allocation\n";
                    result += "- Emphasize bonds and defensive assets\n";
                }
                else
                {
                    result += "- Moderate risk tolerance: Balanced allocation\n";
                    result += "- Mix of growth and defensive assets\n";
                }

                result += "\nImplementation Recommendations:\n";
                if (diversification < 3)
                    result += "- LOW DIVERSIFICATION: Consider adding more asset classes\n";
                
                if (concentrationRisk)
                    result += "- CONCENTRATION RISK: Largest position may be too large\n";
                
                result += "- Rebalance periodically to maintain target allocation\n";
                result += "- Monitor correlation changes that may affect diversification\n";
                result += "- Adjust for market regime changes and economic conditions\n";
            }
            else
            {
                result += "Unable to calculate optimal allocation with current parameters.\n";
                result += "Consider adjusting target return or risk tolerance levels.\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error calculating optimal allocation: {ex.Message}";
        }
    }
}