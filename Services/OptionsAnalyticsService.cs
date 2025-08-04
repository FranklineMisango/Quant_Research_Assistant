using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using QuantResearchAgent.Core;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Services;

/// <summary>
/// Options Analytics Service - Provides comprehensive options pricing, Greeks calculation, and risk analytics
/// Essential for derivatives trading and advanced risk management
/// </summary>
public class OptionsAnalyticsService
{
    private readonly ILogger<OptionsAnalyticsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MarketDataService _marketDataService;
    private readonly ConcurrentDictionary<string, OptionsContract> _optionsCache = new();
    private readonly double _riskFreeRate;

    public OptionsAnalyticsService(
        ILogger<OptionsAnalyticsService> logger,
        IConfiguration configuration,
        MarketDataService marketDataService)
    {
        _logger = logger;
        _configuration = configuration;
        _marketDataService = marketDataService;
        _riskFreeRate = configuration.GetValue<double>("OptionsAnalytics:RiskFreeRate", 0.05);
    }

    /// <summary>
    /// Calculate Black-Scholes option price and Greeks
    /// </summary>
    public async Task<BlackScholesResult> CalculateBlackScholesAsync(
        double underlyingPrice,
        double strike,
        double timeToExpiration,
        double volatility,
        OptionType optionType,
        double dividendYield = 0.0)
    {
        try
        {
            _logger.LogInformation("Calculating Black-Scholes for {OptionType} option: S={S}, K={K}, T={T}, σ={Vol}", 
                optionType, underlyingPrice, strike, timeToExpiration, volatility);

            var result = new BlackScholesResult
            {
                TimeToExpiration = timeToExpiration,
                RiskFreeRate = _riskFreeRate,
                CalculatedAt = DateTime.UtcNow
            };

            // Black-Scholes calculation
            var d1 = (Math.Log(underlyingPrice / strike) + (_riskFreeRate - dividendYield + 0.5 * volatility * volatility) * timeToExpiration) 
                     / (volatility * Math.Sqrt(timeToExpiration));
            var d2 = d1 - volatility * Math.Sqrt(timeToExpiration);

            var nd1 = Normal.CDF(0, 1, d1);
            var nd2 = Normal.CDF(0, 1, d2);
            var nMinusD1 = Normal.CDF(0, 1, -d1);
            var nMinusD2 = Normal.CDF(0, 1, -d2);

            var discountFactor = Math.Exp(-_riskFreeRate * timeToExpiration);
            var dividendDiscountFactor = Math.Exp(-dividendYield * timeToExpiration);

            // Calculate option price
            if (optionType == OptionType.Call)
            {
                result.OptionPrice = underlyingPrice * dividendDiscountFactor * nd1 - strike * discountFactor * nd2;
            }
            else
            {
                result.OptionPrice = strike * discountFactor * nMinusD2 - underlyingPrice * dividendDiscountFactor * nMinusD1;
            }

            // Calculate Greeks
            result.Greeks = CalculateGreeks(underlyingPrice, strike, timeToExpiration, volatility, optionType, 
                dividendYield, d1, d2, nd1, nd2, nMinusD1, nMinusD2, discountFactor, dividendDiscountFactor);

            _logger.LogInformation("Black-Scholes calculation completed. Price: {Price:F4}, Delta: {Delta:F4}", 
                result.OptionPrice, result.Greeks.Delta);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate Black-Scholes price");
            throw;
        }
    }

    /// <summary>
    /// Calculate implied volatility using Newton-Raphson method
    /// </summary>
    public async Task<double> CalculateImpliedVolatilityAsync(
        double marketPrice,
        double underlyingPrice,
        double strike,
        double timeToExpiration,
        OptionType optionType,
        double dividendYield = 0.0)
    {
        try
        {
            _logger.LogInformation("Calculating implied volatility for market price: {MarketPrice}", marketPrice);

            const double tolerance = 1e-6;
            const int maxIterations = 100;
            double volatility = 0.2; // Initial guess

            for (int i = 0; i < maxIterations; i++)
            {
                var bs = await CalculateBlackScholesAsync(underlyingPrice, strike, timeToExpiration, volatility, optionType, dividendYield);
                var priceDiff = bs.OptionPrice - marketPrice;
                
                if (Math.Abs(priceDiff) < tolerance)
                {
                    _logger.LogInformation("Implied volatility converged in {Iterations} iterations: {IV:P2}", i + 1, volatility);
                    return volatility;
                }

                // Newton-Raphson iteration using Vega
                var vega = bs.Greeks.Vega;
                if (Math.Abs(vega) < 1e-10)
                {
                    _logger.LogWarning("Vega too small, breaking iteration");
                    break;
                }

                volatility = volatility - priceDiff / vega;
                
                // Ensure volatility stays positive
                volatility = Math.Max(0.001, Math.Min(volatility, 5.0));
            }

            _logger.LogWarning("Implied volatility did not converge, returning last estimate: {IV:P2}", volatility);
            return volatility;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate implied volatility");
            throw;
        }
    }

    /// <summary>
    /// Analyze options chain for a given underlying symbol
    /// </summary>
    public async Task<List<OptionsContract>> AnalyzeOptionsChainAsync(string underlyingSymbol, DateTime? expiration = null)
    {
        try
        {
            _logger.LogInformation("Analyzing options chain for {Symbol}", underlyingSymbol);

            // Get current market data for underlying
            var marketData = await _marketDataService.GetMarketDataAsync(underlyingSymbol);
            if (marketData == null)
            {
                throw new InvalidOperationException($"Could not retrieve market data for {underlyingSymbol}");
            }

            // For demo purposes, generate synthetic options chain
            // In production, this would fetch real options data from broker APIs
            var optionsChain = GenerateSyntheticOptionsChain(marketData, expiration);

            // Calculate theoretical prices and Greeks for each option
            foreach (var option in optionsChain)
            {
                var timeToExpiration = (option.Expiration - DateTime.UtcNow).TotalDays / 365.0;
                if (timeToExpiration > 0)
                {
                    var bs = await CalculateBlackScholesAsync(
                        marketData.Price, 
                        option.Strike, 
                        timeToExpiration, 
                        0.25, // Assume 25% volatility for demo
                        option.Type);

                    option.Premium = bs.OptionPrice;
                    option.Greeks = bs.Greeks;
                    option.ImpliedVolatility = 0.25;
                }
            }

            _logger.LogInformation("Analyzed {Count} options contracts for {Symbol}", optionsChain.Count, underlyingSymbol);
            return optionsChain;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze options chain for {Symbol}", underlyingSymbol);
            throw;
        }
    }

    /// <summary>
    /// Calculate portfolio Greeks for risk management
    /// </summary>
    public async Task<OptionsGreeks> CalculatePortfolioGreeksAsync(List<OptionsContract> optionsPositions)
    {
        try
        {
            _logger.LogInformation("Calculating portfolio Greeks for {Count} positions", optionsPositions.Count);

            var portfolioGreeks = new OptionsGreeks();

            foreach (var option in optionsPositions)
            {
                portfolioGreeks.Delta += option.Greeks.Delta;
                portfolioGreeks.Gamma += option.Greeks.Gamma;
                portfolioGreeks.Theta += option.Greeks.Theta;
                portfolioGreeks.Vega += option.Greeks.Vega;
                portfolioGreeks.Rho += option.Greeks.Rho;
            }

            _logger.LogInformation("Portfolio Greeks - Delta: {Delta:F4}, Gamma: {Gamma:F4}, Theta: {Theta:F4}", 
                portfolioGreeks.Delta, portfolioGreeks.Gamma, portfolioGreeks.Theta);

            return portfolioGreeks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate portfolio Greeks");
            throw;
        }
    }

    private OptionsGreeks CalculateGreeks(
        double underlyingPrice, double strike, double timeToExpiration, double volatility, 
        OptionType optionType, double dividendYield, double d1, double d2, 
        double nd1, double nd2, double nMinusD1, double nMinusD2, 
        double discountFactor, double dividendDiscountFactor)
    {
        var greeks = new OptionsGreeks();

        // Delta - price sensitivity
        if (optionType == OptionType.Call)
        {
            greeks.Delta = dividendDiscountFactor * nd1;
        }
        else
        {
            greeks.Delta = -dividendDiscountFactor * nMinusD1;
        }

        // Gamma - delta sensitivity (same for calls and puts)
        var phi_d1 = Math.Exp(-0.5 * d1 * d1) / Math.Sqrt(2 * Math.PI);
        greeks.Gamma = dividendDiscountFactor * phi_d1 / (underlyingPrice * volatility * Math.Sqrt(timeToExpiration));

        // Theta - time decay
        var term1 = -dividendDiscountFactor * underlyingPrice * phi_d1 * volatility / (2 * Math.Sqrt(timeToExpiration));
        var term2 = _riskFreeRate * strike * discountFactor;
        var term3 = dividendYield * underlyingPrice * dividendDiscountFactor;

        if (optionType == OptionType.Call)
        {
            greeks.Theta = term1 - term2 * nd2 + term3 * nd1;
        }
        else
        {
            greeks.Theta = term1 + term2 * nMinusD2 - term3 * nMinusD1;
        }

        greeks.Theta /= 365; // Convert to daily theta

        // Vega - volatility sensitivity (same for calls and puts)
        greeks.Vega = underlyingPrice * dividendDiscountFactor * phi_d1 * Math.Sqrt(timeToExpiration) / 100;

        // Rho - interest rate sensitivity
        if (optionType == OptionType.Call)
        {
            greeks.Rho = strike * timeToExpiration * discountFactor * nd2 / 100;
        }
        else
        {
            greeks.Rho = -strike * timeToExpiration * discountFactor * nMinusD2 / 100;
        }

        // Lambda (leverage) - percentage change in option price per percentage change in underlying
        if (Math.Abs(greeks.Delta) > 1e-10)
        {
            greeks.Lambda = greeks.Delta * underlyingPrice;
        }

        return greeks;
    }

    private List<OptionsContract> GenerateSyntheticOptionsChain(MarketData marketData, DateTime? expiration = null)
    {
        var optionsChain = new List<OptionsContract>();
        var baseExpiration = expiration ?? DateTime.UtcNow.AddDays(30);
        var currentPrice = marketData.Price;

        // Generate strikes around current price (±20%)
        var strikeRange = new List<double>();
        for (double multiplier = 0.8; multiplier <= 1.2; multiplier += 0.05)
        {
            strikeRange.Add(Math.Round(currentPrice * multiplier, 2));
        }

        // Generate options for multiple expirations
        var expirations = new List<DateTime>
        {
            baseExpiration,
            baseExpiration.AddDays(30),
            baseExpiration.AddDays(60),
            baseExpiration.AddDays(90)
        };

        foreach (var exp in expirations)
        {
            foreach (var strike in strikeRange)
            {
                // Call option
                optionsChain.Add(new OptionsContract
                {
                    Symbol = $"{marketData.Symbol}_{exp:yyyyMMdd}_C_{strike}",
                    UnderlyingSymbol = marketData.Symbol,
                    Type = OptionType.Call,
                    Strike = strike,
                    Expiration = exp,
                    Volume = Random.Shared.Next(10, 1000),
                    OpenInterest = Random.Shared.Next(100, 5000),
                    LastUpdated = DateTime.UtcNow
                });

                // Put option
                optionsChain.Add(new OptionsContract
                {
                    Symbol = $"{marketData.Symbol}_{exp:yyyyMMdd}_P_{strike}",
                    UnderlyingSymbol = marketData.Symbol,
                    Type = OptionType.Put,
                    Strike = strike,
                    Expiration = exp,
                    Volume = Random.Shared.Next(10, 1000),
                    OpenInterest = Random.Shared.Next(100, 5000),
                    LastUpdated = DateTime.UtcNow
                });
            }
        }

        return optionsChain;
    }
}