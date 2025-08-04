using Microsoft.SemanticKernel;
using System.ComponentModel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Advanced Risk Metrics Plugin - Exposes VaR, CVaR, stress testing, and comprehensive risk analytics
/// </summary>
public class AdvancedRiskMetricsPlugin
{
    private readonly AdvancedRiskMetricsService _riskService;

    public AdvancedRiskMetricsPlugin(AdvancedRiskMetricsService riskService)
    {
        _riskService = riskService;
    }

    [KernelFunction("calculate_var_cvar")]
    [Description("Calculate Value at Risk (VaR) and Conditional VaR for a security or portfolio")]
    public async Task<string> CalculateVarCvarAsync(
        [Description("Security symbol (e.g., AAPL, SPY) or 'PORTFOLIO' for portfolio-level analysis")] string symbol,
        [Description("Lookback period in days (default: 252 for 1 year)")] int lookbackDays = 252,
        [Description("Portfolio value in USD (default: 100000)")] double portfolioValue = 100000)
    {
        try
        {
            var lookbackPeriod = TimeSpan.FromDays(lookbackDays);
            var riskMetrics = symbol.ToUpper() == "PORTFOLIO" 
                ? await _riskService.CalculatePortfolioRiskMetricsAsync()
                : await _riskService.CalculateAdvancedRiskMetricsAsync(symbol, lookbackPeriod, portfolioValue);

            var result = $"Advanced Risk Metrics for {symbol}:\n\n";
            
            result += $"Value at Risk (VaR):\n";
            result += $"95% VaR: ${riskMetrics.ValueAtRisk95:N0} (5% chance of losing more)\n";
            result += $"99% VaR: ${riskMetrics.ValueAtRisk99:N0} (1% chance of losing more)\n\n";

            result += $"Conditional Value at Risk (CVaR/Expected Shortfall):\n";
            result += $"95% CVaR: ${riskMetrics.ConditionalVaR95:N0} (average loss if in worst 5%)\n";
            result += $"99% CVaR: ${riskMetrics.ConditionalVaR99:N0} (average loss if in worst 1%)\n\n";

            result += $"Distribution Characteristics:\n";
            result += $"Skewness: {riskMetrics.Skewness:F3} ({GetSkewnessInterpretation(riskMetrics.Skewness)})\n";
            result += $"Kurtosis: {riskMetrics.Kurtosis:F3} ({GetKurtosisInterpretation(riskMetrics.Kurtosis)})\n";
            result += $"Maximum Drawdown: {riskMetrics.MaximumDrawdown:P2}\n";
            result += $"Tail Ratio: {riskMetrics.TailRatio:F2}\n\n";

            result += $"Risk Assessment:\n";
            result += $"- VaR as % of portfolio: {(riskMetrics.ValueAtRisk95 / portfolioValue):P2}\n";
            result += $"- CVaR/VaR ratio: {(riskMetrics.ConditionalVaR95 / Math.Max(riskMetrics.ValueAtRisk95, 1)):F2}\n";
            result += $"- Tail risk level: {GetTailRiskLevel(riskMetrics.TailRatio, riskMetrics.Kurtosis)}\n";
            result += $"- Distribution: {GetDistributionCharacteristics(riskMetrics.Skewness, riskMetrics.Kurtosis)}\n";

            result += $"\nCalculated at: {riskMetrics.CalculatedAt:yyyy-MM-dd HH:mm:ss}";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error calculating VaR/CVaR: {ex.Message}";
        }
    }

    [KernelFunction("perform_stress_tests")]
    [Description("Perform comprehensive stress testing scenarios")]
    public async Task<string> PerformStressTestsAsync(
        [Description("Security symbol (e.g., AAPL, SPY) or 'PORTFOLIO' for portfolio-level analysis")] string symbol,
        [Description("Lookback period in days (default: 252 for 1 year)")] int lookbackDays = 252,
        [Description("Portfolio value in USD (default: 100000)")] double portfolioValue = 100000)
    {
        try
        {
            var lookbackPeriod = TimeSpan.FromDays(lookbackDays);
            var riskMetrics = symbol.ToUpper() == "PORTFOLIO" 
                ? await _riskService.CalculatePortfolioRiskMetricsAsync()
                : await _riskService.CalculateAdvancedRiskMetricsAsync(symbol, lookbackPeriod, portfolioValue);

            var result = $"Stress Test Results for {symbol}:\n\n";
            
            if (riskMetrics.StressTests.Any())
            {
                result += "Scenario Analysis:\n";
                result += "Scenario\t\t\tReturn Impact\tPortfolio Impact\n";
                result += new string('-', 60) + "\n";

                foreach (var stress in riskMetrics.StressTests.OrderBy(s => s.PortfolioImpact))
                {
                    result += $"{stress.ScenarioName,-20}\t{stress.ReturnImpact:P1}\t\t${stress.PortfolioImpact:N0}\n";
                }

                result += "\nWorst Case Scenarios:\n";
                var worstScenario = riskMetrics.StressTests.OrderBy(s => s.PortfolioImpact).First();
                result += $"Most severe scenario: {worstScenario.ScenarioName}\n";
                result += $"Potential loss: ${Math.Abs(worstScenario.PortfolioImpact):N0} ({Math.Abs(worstScenario.PortfolioImpact) / portfolioValue:P2} of portfolio)\n";
                result += $"Description: {worstScenario.Description}\n\n";

                result += "Risk Management Recommendations:\n";
                var totalWorstCase = Math.Abs(riskMetrics.StressTests.Min(s => s.PortfolioImpact));
                if (totalWorstCase > portfolioValue * 0.2)
                {
                    result += "- HIGH RISK: Consider position sizing or hedging\n";
                    result += "- Recommended max position size: Reduce by 30-50%\n";
                }
                else if (totalWorstCase > portfolioValue * 0.1)
                {
                    result += "- MODERATE RISK: Monitor closely and consider partial hedging\n";
                }
                else
                {
                    result += "- LOW RISK: Stress test losses within acceptable ranges\n";
                }
            }
            else
            {
                result += "No stress test data available.";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error performing stress tests: {ex.Message}";
        }
    }

    [KernelFunction("run_monte_carlo_simulation")]
    [Description("Run Monte Carlo simulation for risk estimation")]
    public async Task<string> RunMonteCarloSimulationAsync(
        [Description("Security symbol (e.g., AAPL, SPY)")] string symbol,
        [Description("Number of simulations (default: 10000)")] int simulations = 10000,
        [Description("Time horizon in days (default: 22 for 1 month)")] int days = 22,
        [Description("Portfolio value in USD (default: 100000)")] double portfolioValue = 100000)
    {
        try
        {
            var result = await _riskService.RunMonteCarloRiskSimulationAsync(symbol, simulations, days, portfolioValue);

            var output = $"Monte Carlo Risk Simulation for {symbol}:\n";
            output += $"Simulations: {result.Simulations:N0} over {result.Days} days\n\n";

            output += $"Risk Metrics:\n";
            output += $"95% VaR: ${result.VaR95:N0}\n";
            output += $"99% VaR: ${result.VaR99:N0}\n";
            output += $"95% CVaR: ${result.CVaR95:N0}\n";
            output += $"99% CVaR: ${result.CVaR99:N0}\n\n";

            output += $"Return Projections:\n";
            output += $"Expected Return: ${result.ExpectedReturn:N0}\n";
            output += $"Expected Volatility: ${result.Volatility:N0}\n";
            output += $"Best Case Scenario: ${result.BestCase:N0}\n";
            output += $"Worst Case Scenario: ${result.WorstCase:N0}\n\n";

            output += $"Performance Statistics:\n";
            var returnRate = result.ExpectedReturn / portfolioValue;
            var riskRate = result.VaR95 / portfolioValue;
            output += $"Expected Return Rate: {returnRate:P2} over {days} days\n";
            output += $"Risk Rate (95% VaR): {riskRate:P2}\n";
            output += $"Risk-Adjusted Return: {(Math.Abs(riskRate) > 0.001 ? returnRate / riskRate : 0):F2}\n\n";

            output += $"Investment Insights:\n";
            if (returnRate > 0.02) // 2% expected return
            {
                output += "- Positive expected returns with moderate risk\n";
            }
            else if (returnRate > 0)
            {
                output += "- Modest positive returns expected\n";
            }
            else
            {
                output += "- Negative expected returns - consider alternatives\n";
            }

            if (riskRate > 0.15) // 15% VaR
            {
                output += "- HIGH RISK: Significant downside potential\n";
            }
            else if (riskRate > 0.05)
            {
                output += "- MODERATE RISK: Acceptable for diversified portfolios\n";
            }
            else
            {
                output += "- LOW RISK: Conservative investment profile\n";
            }

            output += $"\nSimulated on: {result.SimulatedAt:yyyy-MM-dd HH:mm:ss}";

            return output;
        }
        catch (Exception ex)
        {
            return $"Error running Monte Carlo simulation: {ex.Message}";
        }
    }

    [KernelFunction("calculate_component_var")]
    [Description("Calculate component VaR for portfolio risk attribution")]
    public async Task<string> CalculateComponentVarAsync(
        [Description("Confidence level (default: 0.95 for 95%)")] double confidenceLevel = 0.95)
    {
        try
        {
            var componentVaR = await _riskService.CalculateComponentVaRAsync(confidenceLevel);

            if (!componentVaR.Any())
            {
                return "No portfolio positions found for component VaR calculation.";
            }

            var result = $"Component VaR Analysis at {confidenceLevel:P0} confidence level:\n\n";
            
            result += "Position Risk Contribution:\n";
            result += "Symbol\t\tComponent VaR\t% of Total Risk\n";
            result += new string('-', 50) + "\n";

            var totalComponentVaR = componentVaR.Values.Sum();
            foreach (var component in componentVaR.OrderByDescending(kvp => kvp.Value))
            {
                var percentage = totalComponentVaR > 0 ? component.Value / totalComponentVaR : 0;
                result += $"{component.Key,-10}\t${component.Value:N0}\t\t{percentage:P1}\n";
            }

            result += $"\nTotal Portfolio VaR: ${totalComponentVaR:N0}\n\n";

            result += "Risk Concentration Analysis:\n";
            var topRiskContributor = componentVaR.OrderByDescending(kvp => kvp.Value).First();
            var topPercentage = totalComponentVaR > 0 ? topRiskContributor.Value / totalComponentVaR : 0;

            if (topPercentage > 0.4)
            {
                result += $"- HIGH CONCENTRATION: {topRiskContributor.Key} contributes {topPercentage:P1} of portfolio risk\n";
                result += "- Recommendation: Consider diversification or position reduction\n";
            }
            else if (topPercentage > 0.25)
            {
                result += $"- MODERATE CONCENTRATION: {topRiskContributor.Key} is largest risk contributor ({topPercentage:P1})\n";
                result += "- Recommendation: Monitor position size and correlation\n";
            }
            else
            {
                result += "- WELL DIVERSIFIED: Risk is well distributed across positions\n";
            }

            var riskBudgetUtilization = componentVaR.Count(kvp => kvp.Value > totalComponentVaR * 0.1);
            result += $"- Active risk positions: {riskBudgetUtilization} positions contribute >10% each\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error calculating component VaR: {ex.Message}";
        }
    }

    private string GetSkewnessInterpretation(double skewness)
    {
        return skewness switch
        {
            > 0.5 => "Positive skew - upside bias",
            > 0.1 => "Slight positive skew",
            > -0.1 => "Approximately symmetric",
            > -0.5 => "Slight negative skew",
            _ => "Negative skew - downside bias"
        };
    }

    private string GetKurtosisInterpretation(double kurtosis)
    {
        return kurtosis switch
        {
            > 5 => "High kurtosis - fat tails",
            > 3.5 => "Elevated kurtosis",
            > 2.5 => "Normal kurtosis",
            _ => "Low kurtosis - thin tails"
        };
    }

    private string GetTailRiskLevel(double tailRatio, double kurtosis)
    {
        if (tailRatio < 0.5 && kurtosis > 4)
            return "HIGH - Significant tail risk";
        else if (tailRatio < 0.8 || kurtosis > 3.5)
            return "MODERATE - Some tail risk";
        else
            return "LOW - Limited tail risk";
    }

    private string GetDistributionCharacteristics(double skewness, double kurtosis)
    {
        var skewDesc = Math.Abs(skewness) > 0.5 ? "asymmetric" : "symmetric";
        var kurtDesc = kurtosis > 4 ? "fat-tailed" : kurtosis < 2.5 ? "thin-tailed" : "normal-tailed";
        return $"{skewDesc} and {kurtDesc}";
    }
}