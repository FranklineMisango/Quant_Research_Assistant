using Microsoft.SemanticKernel;
using System.ComponentModel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Factor Analysis Plugin - Exposes multi-factor models, attribution analysis, and systematic risk modeling
/// </summary>
public class FactorAnalysisPlugin
{
    private readonly FactorAnalysisService _factorService;

    public FactorAnalysisPlugin(FactorAnalysisService factorService)
    {
        _factorService = factorService;
    }

    [KernelFunction("build_fama_french_model")]
    [Description("Build Fama-French 3-factor model for a security")]
    public async Task<string> BuildFamaFrenchModelAsync(
        [Description("Security symbol (e.g., AAPL, GOOGL)")] string symbol,
        [Description("Lookback period in days (default: 252 for 1 year)")] int lookbackDays = 252)
    {
        try
        {
            var lookbackPeriod = TimeSpan.FromDays(lookbackDays);
            var factorModel = await _factorService.BuildFamaFrenchModelAsync(symbol, lookbackPeriod);

            var result = $"Fama-French 3-Factor Model for {symbol}:\n\n";
            result += $"Model Statistics:\n";
            result += $"R-Squared: {factorModel.RSquared:P2}\n";
            result += $"Adjusted R-Squared: {factorModel.AdjustedRSquared:P2}\n";
            result += $"Tracking Error: {factorModel.TrackingError:P2}\n";
            result += $"Information Ratio: {factorModel.InformationRatio:F3}\n";
            result += $"Analysis Period: {factorModel.StartDate:yyyy-MM-dd} to {factorModel.EndDate:yyyy-MM-dd}\n\n";

            result += "Factor Exposures:\n";
            foreach (var factor in factorModel.Factors)
            {
                var significance = factor.IsSignificant ? "***" : "";
                result += $"{factor.Name}:\n";
                result += $"  Beta: {factor.Beta:F4}{significance}\n";
                result += $"  T-Statistic: {factor.TStatistic:F2}\n";
                result += $"  P-Value: {factor.PValue:F4}\n\n";
            }

            result += "Model Interpretation:\n";
            var marketExposure = factorModel.Factors.FirstOrDefault(f => f.Name == "Market")?.Beta ?? 0;
            var sizeExposure = factorModel.Factors.FirstOrDefault(f => f.Name == "SMB")?.Beta ?? 0;
            var valueExposure = factorModel.Factors.FirstOrDefault(f => f.Name == "HML")?.Beta ?? 0;

            result += $"- Market Beta: {marketExposure:F2} ({GetBetaInterpretation(marketExposure)})\n";
            result += $"- Size Factor: {sizeExposure:F2} ({GetSizeInterpretation(sizeExposure)})\n";
            result += $"- Value Factor: {valueExposure:F2} ({GetValueInterpretation(valueExposure)})\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error building Fama-French model: {ex.Message}";
        }
    }

    [KernelFunction("build_carhart_model")]
    [Description("Build Carhart 4-factor model (Fama-French + Momentum) for a security")]
    public async Task<string> BuildCarhartModelAsync(
        [Description("Security symbol (e.g., AAPL, GOOGL)")] string symbol,
        [Description("Lookback period in days (default: 252 for 1 year)")] int lookbackDays = 252)
    {
        try
        {
            var lookbackPeriod = TimeSpan.FromDays(lookbackDays);
            var factorModel = await _factorService.BuildCarhartModelAsync(symbol, lookbackPeriod);

            var result = $"Carhart 4-Factor Model for {symbol}:\n\n";
            result += $"Model Statistics:\n";
            result += $"R-Squared: {factorModel.RSquared:P2}\n";
            result += $"Adjusted R-Squared: {factorModel.AdjustedRSquared:P2}\n";
            result += $"Tracking Error: {factorModel.TrackingError:P2}\n";
            result += $"Information Ratio: {factorModel.InformationRatio:F3}\n\n";

            result += "Factor Exposures:\n";
            foreach (var factor in factorModel.Factors)
            {
                var significance = factor.IsSignificant ? "***" : "";
                result += $"{factor.Name}: {factor.Beta:F4}{significance} (t={factor.TStatistic:F2})\n";
            }

            result += "\nAlpha Analysis:\n";
            var hasAlpha = factorModel.InformationRatio > 1.0;
            result += $"Information Ratio: {factorModel.InformationRatio:F3}\n";
            result += $"Alpha Assessment: {(hasAlpha ? "Significant alpha detected" : "No significant alpha")}\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error building Carhart model: {ex.Message}";
        }
    }

    [KernelFunction("perform_factor_attribution")]
    [Description("Perform factor attribution analysis to decompose returns")]
    public async Task<string> PerformFactorAttributionAsync(
        [Description("Security symbol (e.g., AAPL, GOOGL)")] string symbol,
        [Description("Analysis window in days (default: 63 for 1 quarter)")] int windowDays = 63)
    {
        try
        {
            var analysisWindow = TimeSpan.FromDays(windowDays);
            var lookbackPeriod = TimeSpan.FromDays(252);
            
            // First build the factor model
            var factorModel = await _factorService.BuildFamaFrenchModelAsync(symbol, lookbackPeriod);
            
            // Then perform attribution
            var attribution = await _factorService.PerformFactorAttributionAsync(symbol, factorModel, analysisWindow);

            var result = $"Factor Attribution Analysis for {symbol}:\n";
            result += $"Period: {attribution.StartDate:yyyy-MM-dd} to {attribution.EndDate:yyyy-MM-dd}\n\n";
            
            result += $"Return Decomposition:\n";
            result += $"Total Return: {attribution.TotalReturn:P2}\n";
            result += $"Alpha (Stock Selection): {attribution.AlphaReturn:P2}\n\n";

            result += "Factor Contributions:\n";
            foreach (var factorReturn in attribution.FactorReturns)
            {
                result += $"{factorReturn.FactorName}:\n";
                result += $"  Exposure: {factorReturn.Exposure:F3}\n";
                result += $"  Factor Return: {factorReturn.FactorReturn:P2}\n";
                result += $"  Contribution: {factorReturn.Contribution:P2}\n\n";
            }

            result += $"Specific Return: {attribution.SpecificReturn:P2}\n\n";

            // Analysis insights
            var largestContributor = attribution.FactorReturns
                .OrderByDescending(f => Math.Abs(f.Contribution))
                .FirstOrDefault();

            if (largestContributor != null)
            {
                result += $"Key Insights:\n";
                result += $"- Largest factor contributor: {largestContributor.FactorName} ({largestContributor.Contribution:P2})\n";
                result += $"- Alpha contribution: {attribution.AlphaReturn:P2}\n";
                result += $"- Stock-specific contribution: {attribution.SpecificReturn:P2}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error performing factor attribution: {ex.Message}";
        }
    }

    [KernelFunction("analyze_factor_stability")]
    [Description("Analyze factor exposure stability over time")]
    public async Task<string> AnalyzeFactorStabilityAsync(
        [Description("Security symbol (e.g., AAPL, GOOGL)")] string symbol,
        [Description("Analysis window in days (default: 252 for 1 year)")] int windowDays = 252,
        [Description("Rolling window size in days (default: 60)")] int rollingWindowDays = 60)
    {
        try
        {
            var analysisWindow = TimeSpan.FromDays(windowDays);
            var factorNames = new List<string> { "Market", "SMB", "HML" };
            
            var stability = await _factorService.AnalyzeFactorStabilityAsync(
                symbol, factorNames, analysisWindow, rollingWindowDays);

            var result = $"Factor Stability Analysis for {symbol}:\n";
            result += $"Analysis Period: {windowDays} days with {rollingWindowDays}-day rolling windows\n\n";

            result += "Factor Stability Scores (0-1, higher is more stable):\n";
            foreach (var factorStability in stability.OrderByDescending(kvp => kvp.Value))
            {
                var score = factorStability.Value;
                var assessment = score switch
                {
                    > 0.8 => "Very Stable",
                    > 0.6 => "Stable", 
                    > 0.4 => "Moderately Stable",
                    > 0.2 => "Unstable",
                    _ => "Very Unstable"
                };

                result += $"{factorStability.Key}: {score:F3} ({assessment})\n";
            }

            result += "\nInterpretation:\n";
            result += "- High stability indicates consistent factor exposures over time\n";
            result += "- Low stability suggests changing business fundamentals or market conditions\n";
            result += "- Use stable exposures for risk management and hedging decisions\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing factor stability: {ex.Message}";
        }
    }

    [KernelFunction("calculate_factor_exposures")]
    [Description("Calculate factor exposures for multiple securities")]
    public async Task<string> CalculateFactorExposuresAsync(
        [Description("Comma-separated list of symbols (e.g., AAPL,GOOGL,MSFT)")] string symbols,
        [Description("Lookback period in days (default: 252 for 1 year)")] int lookbackDays = 252)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim()).ToList();
            var lookbackPeriod = TimeSpan.FromDays(lookbackDays);
            var factorNames = new List<string> { "Market", "SMB", "HML" };

            var exposures = await _factorService.CalculateFactorExposuresAsync(symbolList, factorNames, lookbackPeriod);

            var result = $"Factor Exposures Analysis:\n";
            result += $"Securities: {string.Join(", ", symbolList)}\n";
            result += $"Lookback Period: {lookbackDays} days\n\n";

            result += "Factor Exposures Matrix:\n";
            result += "Symbol\t\tMarket\tSMB\tHML\tR²\n";
            result += new string('-', 50) + "\n";

            foreach (var exposure in exposures)
            {
                var symbol = exposure.Key;
                var model = exposure.Value;
                
                var marketBeta = model.Factors.FirstOrDefault(f => f.Name == "Market")?.Beta ?? 0;
                var smbBeta = model.Factors.FirstOrDefault(f => f.Name == "SMB")?.Beta ?? 0;
                var hmlBeta = model.Factors.FirstOrDefault(f => f.Name == "HML")?.Beta ?? 0;

                result += $"{symbol,-10}\t{marketBeta:F3}\t{smbBeta:F3}\t{hmlBeta:F3}\t{model.RSquared:F3}\n";
            }

            result += "\nPortfolio-Level Insights:\n";
            var avgMarketBeta = exposures.Values.Average(m => m.Factors.FirstOrDefault(f => f.Name == "Market")?.Beta ?? 0);
            var avgRSquared = exposures.Values.Average(m => m.RSquared);

            result += $"- Average Market Beta: {avgMarketBeta:F3}\n";
            result += $"- Average R-Squared: {avgRSquared:P2}\n";
            result += $"- Portfolio Factor Exposure Assessment: {GetPortfolioAssessment(avgMarketBeta, avgRSquared)}\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error calculating factor exposures: {ex.Message}";
        }
    }

    private string GetBetaInterpretation(double beta)
    {
        return beta switch
        {
            > 1.2 => "High Beta - Amplifies market moves",
            > 0.8 => "Market-like Beta",
            > 0.5 => "Defensive - Less volatile than market",
            _ => "Low Beta - Very defensive"
        };
    }

    private string GetSizeInterpretation(double smb)
    {
        return smb switch
        {
            > 0.3 => "Strong small-cap tilt",
            > 0.1 => "Moderate small-cap tilt",
            > -0.1 => "Size-neutral",
            > -0.3 => "Moderate large-cap tilt",
            _ => "Strong large-cap tilt"
        };
    }

    private string GetValueInterpretation(double hml)
    {
        return hml switch
        {
            > 0.3 => "Strong value tilt",
            > 0.1 => "Moderate value tilt",
            > -0.1 => "Style-neutral",
            > -0.3 => "Moderate growth tilt",
            _ => "Strong growth tilt"
        };
    }

    private string GetPortfolioAssessment(double avgBeta, double avgRSquared)
    {
        var betaAssessment = avgBeta > 1.1 ? "Aggressive" : avgBeta < 0.9 ? "Defensive" : "Balanced";
        var explanatoryPower = avgRSquared > 0.7 ? "High" : avgRSquared > 0.4 ? "Moderate" : "Low";
        
        return $"{betaAssessment} portfolio with {explanatoryPower} factor explanatory power";
    }
}