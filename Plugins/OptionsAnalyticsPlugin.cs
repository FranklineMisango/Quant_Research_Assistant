using Microsoft.SemanticKernel;
using System.ComponentModel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Options Analytics Plugin - Exposes options pricing, Greeks calculation, and derivatives risk analytics
/// </summary>
public class OptionsAnalyticsPlugin
{
    private readonly OptionsAnalyticsService _optionsService;

    public OptionsAnalyticsPlugin(OptionsAnalyticsService optionsService)
    {
        _optionsService = optionsService;
    }

    [KernelFunction("calculate_black_scholes")]
    [Description("Calculate Black-Scholes option price and Greeks for a given option")]
    public async Task<string> CalculateBlackScholesAsync(
        [Description("Current price of the underlying asset")] double underlyingPrice,
        [Description("Strike price of the option")] double strike,
        [Description("Time to expiration in years")] double timeToExpiration,
        [Description("Implied volatility (as decimal, e.g., 0.25 for 25%)")] double volatility,
        [Description("Option type: 'call' or 'put'")] string optionType,
        [Description("Dividend yield (optional, default 0)")] double dividendYield = 0.0)
    {
        try
        {
            var type = optionType.ToLower() == "call" ? OptionType.Call : OptionType.Put;
            var result = await _optionsService.CalculateBlackScholesAsync(
                underlyingPrice, strike, timeToExpiration, volatility, type, dividendYield);

            return $"Black-Scholes Analysis:\n" +
                   $"Option Price: ${result.OptionPrice:F4}\n" +
                   $"Greeks:\n" +
                   $"  Delta: {result.Greeks.Delta:F4}\n" +
                   $"  Gamma: {result.Greeks.Gamma:F4}\n" +
                   $"  Theta: {result.Greeks.Theta:F4}\n" +
                   $"  Vega: {result.Greeks.Vega:F4}\n" +
                   $"  Rho: {result.Greeks.Rho:F4}\n" +
                   $"Risk-Free Rate: {result.RiskFreeRate:P2}\n" +
                   $"Time to Expiration: {result.TimeToExpiration:F4} years";
        }
        catch (Exception ex)
        {
            return $"Error calculating Black-Scholes: {ex.Message}";
        }
    }

    [KernelFunction("calculate_implied_volatility")]
    [Description("Calculate implied volatility from market option price")]
    public async Task<string> CalculateImpliedVolatilityAsync(
        [Description("Current market price of the option")] double marketPrice,
        [Description("Current price of the underlying asset")] double underlyingPrice,
        [Description("Strike price of the option")] double strike,
        [Description("Time to expiration in years")] double timeToExpiration,
        [Description("Option type: 'call' or 'put'")] string optionType,
        [Description("Dividend yield (optional, default 0)")] double dividendYield = 0.0)
    {
        try
        {
            var type = optionType.ToLower() == "call" ? OptionType.Call : OptionType.Put;
            var impliedVol = await _optionsService.CalculateImpliedVolatilityAsync(
                marketPrice, underlyingPrice, strike, timeToExpiration, type, dividendYield);

            return $"Implied Volatility Analysis:\n" +
                   $"Market Price: ${marketPrice:F4}\n" +
                   $"Implied Volatility: {impliedVol:P2}\n" +
                   $"Annualized Volatility: {impliedVol * 100:F2}%";
        }
        catch (Exception ex)
        {
            return $"Error calculating implied volatility: {ex.Message}";
        }
    }

    [KernelFunction("analyze_options_chain")]
    [Description("Analyze complete options chain for a given underlying symbol")]
    public async Task<string> AnalyzeOptionsChainAsync(
        [Description("Underlying symbol (e.g., AAPL, SPY)")] string underlyingSymbol,
        [Description("Expiration date (optional, format: yyyy-MM-dd)")] string? expiration = null)
    {
        try
        {
            DateTime? expirationDate = null;
            if (!string.IsNullOrEmpty(expiration))
            {
                expirationDate = DateTime.Parse(expiration);
            }

            var optionsChain = await _optionsService.AnalyzeOptionsChainAsync(underlyingSymbol, expirationDate);

            if (!optionsChain.Any())
            {
                return $"No options found for {underlyingSymbol}";
            }

            var callOptions = optionsChain.Where(o => o.Type == OptionType.Call).Take(5);
            var putOptions = optionsChain.Where(o => o.Type == OptionType.Put).Take(5);

            var result = $"Options Chain Analysis for {underlyingSymbol}:\n\n";
            
            result += "Top 5 Call Options:\n";
            result += "Strike\tPremium\tDelta\tGamma\tTheta\tVega\n";
            foreach (var call in callOptions)
            {
                result += $"{call.Strike:F2}\t${call.Premium:F4}\t{call.Greeks.Delta:F3}\t{call.Greeks.Gamma:F3}\t{call.Greeks.Theta:F3}\t{call.Greeks.Vega:F3}\n";
            }

            result += "\nTop 5 Put Options:\n";
            result += "Strike\tPremium\tDelta\tGamma\tTheta\tVega\n";
            foreach (var put in putOptions)
            {
                result += $"{put.Strike:F2}\t${put.Premium:F4}\t{put.Greeks.Delta:F3}\t{put.Greeks.Gamma:F3}\t{put.Greeks.Theta:F3}\t{put.Greeks.Vega:F3}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing options chain: {ex.Message}";
        }
    }

    [KernelFunction("calculate_portfolio_greeks")]
    [Description("Calculate portfolio-level Greeks for risk management")]
    public async Task<string> CalculatePortfolioGreeksAsync(
        [Description("JSON array of options positions with symbols and quantities")] string optionsPositionsJson)
    {
        try
        {
            // For demo purposes, we'll use synthetic data
            // In production, this would parse the JSON and use actual positions
            var syntheticPositions = new List<OptionsContract>
            {
                new OptionsContract 
                { 
                    Symbol = "AAPL_CALL_150", 
                    Greeks = new OptionsGreeks { Delta = 0.6, Gamma = 0.05, Theta = -0.02, Vega = 0.15 } 
                },
                new OptionsContract 
                { 
                    Symbol = "SPY_PUT_400", 
                    Greeks = new OptionsGreeks { Delta = -0.4, Gamma = 0.03, Theta = -0.01, Vega = 0.12 } 
                }
            };

            var portfolioGreeks = await _optionsService.CalculatePortfolioGreeksAsync(syntheticPositions);

            return $"Portfolio Greeks Summary:\n" +
                   $"Net Delta: {portfolioGreeks.Delta:F4} (Directional Risk)\n" +
                   $"Net Gamma: {portfolioGreeks.Gamma:F4} (Delta Sensitivity)\n" +
                   $"Net Theta: {portfolioGreeks.Theta:F4} (Time Decay per Day)\n" +
                   $"Net Vega: {portfolioGreeks.Vega:F4} (Volatility Sensitivity)\n" +
                   $"Net Rho: {portfolioGreeks.Rho:F4} (Interest Rate Sensitivity)\n\n" +
                   $"Risk Assessment:\n" +
                   $"- Delta exposure: {(Math.Abs(portfolioGreeks.Delta) > 0.5 ? "High" : "Moderate")} directional risk\n" +
                   $"- Gamma exposure: {(Math.Abs(portfolioGreeks.Gamma) > 0.1 ? "High" : "Low")} acceleration risk\n" +
                   $"- Theta decay: ${portfolioGreeks.Theta * 100:F2} per day time decay\n" +
                   $"- Vega exposure: {(Math.Abs(portfolioGreeks.Vega) > 0.2 ? "High" : "Low")} volatility sensitivity";
        }
        catch (Exception ex)
        {
            return $"Error calculating portfolio Greeks: {ex.Message}";
        }
    }
}