using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using QuantResearchAgent.Core;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Services;

/// <summary>
/// Advanced Risk Metrics Service - Provides VaR, CVaR, stress testing, and comprehensive risk analytics
/// Essential for modern risk management and regulatory compliance
/// </summary>
public class AdvancedRiskMetricsService
{
    private readonly ILogger<AdvancedRiskMetricsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MarketDataService _marketDataService;
    private readonly PortfolioService _portfolioService;
    private readonly ConcurrentDictionary<string, AdvancedRiskMetrics> _riskCache = new();

    // Standard stress test scenarios
    private readonly Dictionary<string, StressScenario> _stressScenarios = new();

    public AdvancedRiskMetricsService(
        ILogger<AdvancedRiskMetricsService> logger,
        IConfiguration configuration,
        MarketDataService marketDataService,
        PortfolioService portfolioService)
    {
        _logger = logger;
        _configuration = configuration;
        _marketDataService = marketDataService;
        _portfolioService = portfolioService;
        InitializeStressScenarios();
    }

    /// <summary>
    /// Calculate comprehensive risk metrics including VaR, CVaR, and higher moments
    /// </summary>
    public async Task<AdvancedRiskMetrics> CalculateAdvancedRiskMetricsAsync(
        string symbol, 
        TimeSpan lookbackPeriod,
        double portfolioValue = 100000)
    {
        try
        {
            _logger.LogInformation("Calculating advanced risk metrics for {Symbol} with lookback {Period}", 
                symbol, lookbackPeriod);

            var cacheKey = $"{symbol}_{lookbackPeriod.TotalDays}_{portfolioValue}";
            if (_riskCache.TryGetValue(cacheKey, out var cached) && 
                DateTime.UtcNow - cached.CalculatedAt < TimeSpan.FromHours(6))
            {
                _logger.LogInformation("Returning cached risk metrics for {Symbol}", symbol);
                return cached;
            }

            // Get historical returns
            var returns = await GetHistoricalReturnsAsync(symbol, lookbackPeriod);
            if (returns.Length < 30)
            {
                throw new InvalidOperationException($"Insufficient data for risk calculation. Need at least 30 observations, got {returns.Length}");
            }

            var riskMetrics = new AdvancedRiskMetrics
            {
                Symbol = symbol,
                CalculatedAt = DateTime.UtcNow
            };

            // Calculate Value at Risk (VaR)
            riskMetrics.ValueAtRisk95 = CalculateVaR(returns, 0.95, portfolioValue);
            riskMetrics.ValueAtRisk99 = CalculateVaR(returns, 0.99, portfolioValue);

            // Calculate Conditional Value at Risk (CVaR/Expected Shortfall)
            riskMetrics.ConditionalVaR95 = CalculateCVaR(returns, 0.95, portfolioValue);
            riskMetrics.ConditionalVaR99 = CalculateCVaR(returns, 0.99, portfolioValue);
            riskMetrics.ExpectedShortfall = riskMetrics.ConditionalVaR95;

            // Calculate higher moments
            riskMetrics.Skewness = returns.Skewness();
            riskMetrics.Kurtosis = returns.Kurtosis();

            // Calculate maximum drawdown
            riskMetrics.MaximumDrawdown = CalculateMaxDrawdown(returns);

            // Calculate tail ratio
            riskMetrics.TailRatio = CalculateTailRatio(returns);

            // Perform stress tests
            riskMetrics.StressTests = await PerformStressTestsAsync(symbol, returns, portfolioValue);

            _riskCache[cacheKey] = riskMetrics;

            _logger.LogInformation("Advanced risk metrics calculated for {Symbol}. VaR 95%: {VaR:C}, CVaR 95%: {CVaR:C}", 
                symbol, riskMetrics.ValueAtRisk95, riskMetrics.ConditionalVaR95);

            return riskMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate advanced risk metrics for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Calculate portfolio-level risk metrics
    /// </summary>
    public async Task<AdvancedRiskMetrics> CalculatePortfolioRiskMetricsAsync()
    {
        try
        {
            _logger.LogInformation("Calculating portfolio-level risk metrics");

            var positions = await _portfolioService.GetPositionsAsync();
            var portfolioMetrics = await _portfolioService.CalculateMetricsAsync();

            if (!positions.Any())
            {
                throw new InvalidOperationException("No positions found in portfolio");
            }

            // Get historical returns for all positions
            var lookbackPeriod = TimeSpan.FromDays(252); // 1 year
            var portfolioReturns = await CalculatePortfolioReturnsAsync(positions, lookbackPeriod);

            var riskMetrics = new AdvancedRiskMetrics
            {
                Symbol = "PORTFOLIO",
                CalculatedAt = DateTime.UtcNow
            };

            // Portfolio VaR and CVaR
            riskMetrics.ValueAtRisk95 = CalculateVaR(portfolioReturns, 0.95, portfolioMetrics.TotalValue);
            riskMetrics.ValueAtRisk99 = CalculateVaR(portfolioReturns, 0.99, portfolioMetrics.TotalValue);
            riskMetrics.ConditionalVaR95 = CalculateCVaR(portfolioReturns, 0.95, portfolioMetrics.TotalValue);
            riskMetrics.ConditionalVaR99 = CalculateCVaR(portfolioReturns, 0.99, portfolioMetrics.TotalValue);

            // Portfolio moments
            riskMetrics.Skewness = portfolioReturns.Skewness();
            riskMetrics.Kurtosis = portfolioReturns.Kurtosis();
            riskMetrics.MaximumDrawdown = CalculateMaxDrawdown(portfolioReturns);
            riskMetrics.TailRatio = CalculateTailRatio(portfolioReturns);

            // Portfolio stress tests
            riskMetrics.StressTests = await PerformPortfolioStressTestsAsync(positions, portfolioMetrics.TotalValue);

            _logger.LogInformation("Portfolio risk metrics calculated. Portfolio VaR 95%: {VaR:C}", 
                riskMetrics.ValueAtRisk95);

            return riskMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate portfolio risk metrics");
            throw;
        }
    }

    /// <summary>
    /// Calculate component VaR for portfolio positions
    /// </summary>
    public async Task<Dictionary<string, double>> CalculateComponentVaRAsync(double confidenceLevel = 0.95)
    {
        try
        {
            _logger.LogInformation("Calculating component VaR at {Confidence:P0} confidence level", confidenceLevel);

            var positions = await _portfolioService.GetPositionsAsync();
            var portfolioMetrics = await _portfolioService.CalculateMetricsAsync();

            if (!positions.Any())
            {
                return new Dictionary<string, double>();
            }

            var componentVaR = new Dictionary<string, double>();
            var lookbackPeriod = TimeSpan.FromDays(252);

            // Calculate marginal VaR for each position
            foreach (var position in positions)
            {
                var returns = await GetHistoricalReturnsAsync(position.Symbol, lookbackPeriod);
                var positionValue = position.Quantity * position.CurrentPrice;
                var weight = positionValue / portfolioMetrics.TotalValue;

                // Simplified component VaR calculation (marginal VaR * position size)
                var marginalVaR = CalculateVaR(returns, confidenceLevel, positionValue);
                componentVaR[position.Symbol] = marginalVaR * weight;
            }

            _logger.LogInformation("Component VaR calculated for {Count} positions", componentVaR.Count);
            return componentVaR;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate component VaR");
            throw;
        }
    }

    /// <summary>
    /// Perform Monte Carlo simulation for risk estimation
    /// </summary>
    public async Task<MonteCarloRiskResult> RunMonteCarloRiskSimulationAsync(
        string symbol, 
        int simulations = 10000, 
        int days = 22,
        double portfolioValue = 100000)
    {
        try
        {
            _logger.LogInformation("Running Monte Carlo simulation for {Symbol} with {Simulations} simulations over {Days} days", 
                symbol, simulations, days);

            var returns = await GetHistoricalReturnsAsync(symbol, TimeSpan.FromDays(252));
            var meanReturn = returns.Mean();
            var volatility = returns.StandardDeviation();

            var simulatedReturns = new List<double>();
            var random = new Random();
            var normal = new Normal(meanReturn, volatility, random);

            // Run Monte Carlo simulations
            for (int i = 0; i < simulations; i++)
            {
                double cumulativeReturn = 0;
                for (int day = 0; day < days; day++)
                {
                    cumulativeReturn += normal.Sample();
                }
                simulatedReturns.Add(cumulativeReturn);
            }

            // Calculate risk metrics from simulation
            simulatedReturns.Sort();
            var var95Index = (int)(simulations * 0.05);
            var var99Index = (int)(simulations * 0.01);

            var result = new MonteCarloRiskResult
            {
                Symbol = symbol,
                Simulations = simulations,
                Days = days,
                VaR95 = Math.Abs(simulatedReturns[var95Index] * portfolioValue),
                VaR99 = Math.Abs(simulatedReturns[var99Index] * portfolioValue),
                CVaR95 = Math.Abs(simulatedReturns.Take(var95Index).Average() * portfolioValue),
                CVaR99 = Math.Abs(simulatedReturns.Take(var99Index).Average() * portfolioValue),
                ExpectedReturn = simulatedReturns.Average() * portfolioValue,
                Volatility = simulatedReturns.StandardDeviation() * portfolioValue,
                WorstCase = Math.Abs(simulatedReturns.First() * portfolioValue),
                BestCase = simulatedReturns.Last() * portfolioValue,
                SimulatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Monte Carlo simulation completed. VaR 95%: {VaR:C}, Expected Return: {ExpReturn:C}", 
                result.VaR95, result.ExpectedReturn);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run Monte Carlo risk simulation for {Symbol}", symbol);
            throw;
        }
    }

    private double CalculateVaR(double[] returns, double confidenceLevel, double portfolioValue)
    {
        var sortedReturns = returns.OrderBy(r => r).ToArray();
        var index = (int)((1 - confidenceLevel) * returns.Length);
        var varReturn = sortedReturns[Math.Max(0, index - 1)];
        return Math.Abs(varReturn * portfolioValue);
    }

    private double CalculateCVaR(double[] returns, double confidenceLevel, double portfolioValue)
    {
        var sortedReturns = returns.OrderBy(r => r).ToArray();
        var index = (int)((1 - confidenceLevel) * returns.Length);
        var tailReturns = sortedReturns.Take(Math.Max(1, index)).ToArray();
        var cvarReturn = tailReturns.Average();
        return Math.Abs(cvarReturn * portfolioValue);
    }

    private double CalculateMaxDrawdown(double[] returns)
    {
        var cumulativeReturns = new double[returns.Length + 1];
        cumulativeReturns[0] = 0;

        for (int i = 0; i < returns.Length; i++)
        {
            cumulativeReturns[i + 1] = cumulativeReturns[i] + returns[i];
        }

        double maxDrawdown = 0;
        double peak = cumulativeReturns[0];

        foreach (var value in cumulativeReturns)
        {
            if (value > peak)
            {
                peak = value;
            }
            var drawdown = peak - value;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown;
    }

    private double CalculateTailRatio(double[] returns)
    {
        var sortedReturns = returns.OrderBy(r => r).ToArray();
        var upperTail = sortedReturns.Skip((int)(returns.Length * 0.95)).Average();
        var lowerTail = Math.Abs(sortedReturns.Take((int)(returns.Length * 0.05)).Average());
        
        return lowerTail > 0 ? upperTail / lowerTail : 0;
    }

    private async Task<List<StressTestResult>> PerformStressTestsAsync(string symbol, double[] historicalReturns, double portfolioValue)
    {
        var results = new List<StressTestResult>();

        foreach (var scenario in _stressScenarios.Values)
        {
            var stressedReturn = scenario.ReturnShock;
            var stressedVolatility = historicalReturns.StandardDeviation() * scenario.VolatilityMultiplier;
            var portfolioImpact = stressedReturn * portfolioValue;

            results.Add(new StressTestResult
            {
                ScenarioName = scenario.Name,
                ReturnImpact = stressedReturn,
                VolatilityImpact = stressedVolatility,
                PortfolioImpact = portfolioImpact,
                Description = scenario.Description
            });
        }

        return results;
    }

    private async Task<List<StressTestResult>> PerformPortfolioStressTestsAsync(List<Position> positions, double portfolioValue)
    {
        var results = new List<StressTestResult>();

        foreach (var scenario in _stressScenarios.Values)
        {
            double totalImpact = 0;

            foreach (var position in positions)
            {
                var positionValue = position.Quantity * position.CurrentPrice;
                var positionImpact = positionValue * scenario.ReturnShock;
                totalImpact += positionImpact;
            }

            results.Add(new StressTestResult
            {
                ScenarioName = scenario.Name,
                ReturnImpact = scenario.ReturnShock,
                VolatilityImpact = scenario.VolatilityMultiplier,
                PortfolioImpact = totalImpact,
                Description = scenario.Description
            });
        }

        return results;
    }

    private async Task<double[]> GetHistoricalReturnsAsync(string symbol, TimeSpan period)
    {
        // In production, this would fetch real historical data
        // For now, generate synthetic returns
        var random = new Random(symbol.GetHashCode());
        var days = (int)period.TotalDays;
        var returns = new double[Math.Min(days, 252 * 3)]; // Limit to 3 years

        var normal = new Normal(0.0005, 0.02, random); // Slightly positive expected return with 2% daily vol
        for (int i = 0; i < returns.Length; i++)
        {
            returns[i] = normal.Sample();
        }

        return returns;
    }

    private async Task<double[]> CalculatePortfolioReturnsAsync(List<Position> positions, TimeSpan period)
    {
        if (!positions.Any())
        {
            return Array.Empty<double>();
        }

        var portfolioValue = positions.Sum(p => p.Quantity * p.CurrentPrice);
        var days = (int)period.TotalDays;
        var portfolioReturns = new double[Math.Min(days, 252 * 3)];

        // Calculate weighted portfolio returns
        for (int i = 0; i < portfolioReturns.Length; i++)
        {
            double dailyReturn = 0;
            foreach (var position in positions)
            {
                var weight = (position.Quantity * position.CurrentPrice) / portfolioValue;
                var assetReturns = await GetHistoricalReturnsAsync(position.Symbol, period);
                if (i < assetReturns.Length)
                {
                    dailyReturn += weight * assetReturns[i];
                }
            }
            portfolioReturns[i] = dailyReturn;
        }

        return portfolioReturns;
    }

    private void InitializeStressScenarios()
    {
        _stressScenarios["Market Crash"] = new StressScenario
        {
            Name = "Market Crash",
            Description = "Severe market downturn (2008-style crisis)",
            ReturnShock = -0.30,
            VolatilityMultiplier = 2.0
        };

        _stressScenarios["Flash Crash"] = new StressScenario
        {
            Name = "Flash Crash",
            Description = "Sudden intraday market crash",
            ReturnShock = -0.15,
            VolatilityMultiplier = 3.0
        };

        _stressScenarios["Interest Rate Spike"] = new StressScenario
        {
            Name = "Interest Rate Spike",
            Description = "Rapid increase in interest rates",
            ReturnShock = -0.10,
            VolatilityMultiplier = 1.5
        };

        _stressScenarios["Inflation Shock"] = new StressScenario
        {
            Name = "Inflation Shock",
            Description = "Unexpected inflation surge",
            ReturnShock = -0.12,
            VolatilityMultiplier = 1.8
        };

        _stressScenarios["Currency Crisis"] = new StressScenario
        {
            Name = "Currency Crisis",
            Description = "Major currency devaluation",
            ReturnShock = -0.20,
            VolatilityMultiplier = 2.5
        };
    }

    private class StressScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double ReturnShock { get; set; }
        public double VolatilityMultiplier { get; set; }
    }

    public class MonteCarloRiskResult
    {
        public string Symbol { get; set; } = string.Empty;
        public int Simulations { get; set; }
        public int Days { get; set; }
        public double VaR95 { get; set; }
        public double VaR99 { get; set; }
        public double CVaR95 { get; set; }
        public double CVaR99 { get; set; }
        public double ExpectedReturn { get; set; }
        public double Volatility { get; set; }
        public double WorstCase { get; set; }
        public double BestCase { get; set; }
        public DateTime SimulatedAt { get; set; }
    }
}