using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using QuantResearchAgent.Core;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Services;

/// <summary>
/// Factor Analysis Service - Provides multi-factor models, attribution analysis, and systematic risk modeling
/// Essential for systematic alpha generation and risk attribution
/// </summary>
public class FactorAnalysisService
{
    private readonly ILogger<FactorAnalysisService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MarketDataService _marketDataService;
    private readonly ConcurrentDictionary<string, FactorModel> _factorModelCache = new();

    // Standard factor data (in production, these would come from external data providers)
    private readonly Dictionary<string, double[]> _factorReturns = new();

    public FactorAnalysisService(
        ILogger<FactorAnalysisService> logger,
        IConfiguration configuration,
        MarketDataService marketDataService)
    {
        _logger = logger;
        _configuration = configuration;
        _marketDataService = marketDataService;
        InitializeFactorData();
    }

    /// <summary>
    /// Build a multi-factor model for a given security
    /// </summary>
    public async Task<FactorModel> BuildFactorModelAsync(
        string symbol, 
        List<string> factorNames,
        TimeSpan lookbackPeriod)
    {
        try
        {
            _logger.LogInformation("Building factor model for {Symbol} with factors: {Factors}", 
                symbol, string.Join(", ", factorNames));

            var cacheKey = $"{symbol}_{string.Join("_", factorNames)}_{lookbackPeriod.TotalDays}";
            if (_factorModelCache.TryGetValue(cacheKey, out var cached) && 
                DateTime.UtcNow - cached.EndDate < TimeSpan.FromDays(1))
            {
                _logger.LogInformation("Returning cached factor model for {Symbol}", symbol);
                return cached;
            }

            // Get historical returns for the security
            var securityReturns = await GetHistoricalReturnsAsync(symbol, lookbackPeriod);
            if (securityReturns.Length < 30)
            {
                throw new InvalidOperationException($"Insufficient data for factor model. Need at least 30 observations, got {securityReturns.Length}");
            }

            // Prepare factor data
            var factorData = PrepareFactorData(factorNames, securityReturns.Length);
            
            // Run multiple regression
            var regression = PerformMultipleRegression(securityReturns, factorData);

            var factorModel = new FactorModel
            {
                Name = $"{symbol}_FactorModel",
                Factors = BuildFactorList(factorNames, regression),
                RSquared = regression.RSquared,
                AdjustedRSquared = regression.AdjustedRSquared,
                TrackingError = regression.TrackingError,
                InformationRatio = CalculateInformationRatio(regression),
                StartDate = DateTime.UtcNow.Subtract(lookbackPeriod),
                EndDate = DateTime.UtcNow
            };

            _factorModelCache[cacheKey] = factorModel;

            _logger.LogInformation("Factor model built for {Symbol}. R²: {RSquared:F4}, Tracking Error: {TE:F4}", 
                symbol, factorModel.RSquared, factorModel.TrackingError);

            return factorModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build factor model for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Perform factor attribution analysis
    /// </summary>
    public async Task<FactorAttribution> PerformFactorAttributionAsync(
        string symbol,
        FactorModel factorModel,
        TimeSpan analysisWindow)
    {
        try
        {
            _logger.LogInformation("Performing factor attribution for {Symbol}", symbol);

            var securityReturns = await GetHistoricalReturnsAsync(symbol, analysisWindow);
            var totalReturn = securityReturns.Sum();

            var attribution = new FactorAttribution
            {
                Symbol = symbol,
                TotalReturn = totalReturn,
                StartDate = DateTime.UtcNow.Subtract(analysisWindow),
                EndDate = DateTime.UtcNow
            };

            // Calculate factor contributions
            var factorContributions = new List<FactorContribution>();
            double totalFactorReturn = 0;

            foreach (var factor in factorModel.Factors)
            {
                var factorReturns = GetFactorReturns(factor.Name, securityReturns.Length);
                var factorReturn = factorReturns.Sum();
                var contribution = factor.Beta * factorReturn;

                factorContributions.Add(new FactorContribution
                {
                    FactorName = factor.Name,
                    Exposure = factor.Beta,
                    FactorReturn = factorReturn,
                    Contribution = contribution
                });

                totalFactorReturn += contribution;
            }

            attribution.FactorReturns = factorContributions;
            attribution.AlphaReturn = totalReturn - totalFactorReturn;
            attribution.SpecificReturn = attribution.AlphaReturn;

            _logger.LogInformation("Factor attribution completed. Total Return: {Total:P2}, Alpha: {Alpha:P2}", 
                attribution.TotalReturn, attribution.AlphaReturn);

            return attribution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform factor attribution for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Build Fama-French 3-factor model
    /// </summary>
    public async Task<FactorModel> BuildFamaFrenchModelAsync(string symbol, TimeSpan lookbackPeriod)
    {
        var factorNames = new List<string> { "Market", "SMB", "HML" };
        return await BuildFactorModelAsync(symbol, factorNames, lookbackPeriod);
    }

    /// <summary>
    /// Build Carhart 4-factor model (Fama-French + Momentum)
    /// </summary>
    public async Task<FactorModel> BuildCarhartModelAsync(string symbol, TimeSpan lookbackPeriod)
    {
        var factorNames = new List<string> { "Market", "SMB", "HML", "UMD" };
        return await BuildFactorModelAsync(symbol, factorNames, lookbackPeriod);
    }

    /// <summary>
    /// Calculate factor exposures for multiple securities
    /// </summary>
    public async Task<Dictionary<string, FactorModel>> CalculateFactorExposuresAsync(
        List<string> symbols,
        List<string> factorNames,
        TimeSpan lookbackPeriod)
    {
        try
        {
            _logger.LogInformation("Calculating factor exposures for {Count} symbols", symbols.Count);

            var exposures = new Dictionary<string, FactorModel>();
            var tasks = symbols.Select(symbol => BuildFactorModelAsync(symbol, factorNames, lookbackPeriod));
            var results = await Task.WhenAll(tasks);

            for (int i = 0; i < symbols.Count; i++)
            {
                exposures[symbols[i]] = results[i];
            }

            _logger.LogInformation("Factor exposures calculated for {Count} symbols", symbols.Count);
            return exposures;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate factor exposures");
            throw;
        }
    }

    /// <summary>
    /// Analyze factor stability over time
    /// </summary>
    public async Task<Dictionary<string, double>> AnalyzeFactorStabilityAsync(
        string symbol, 
        List<string> factorNames,
        TimeSpan analysisWindow,
        int rollingWindowDays = 60)
    {
        try
        {
            _logger.LogInformation("Analyzing factor stability for {Symbol}", symbol);

            var stability = new Dictionary<string, double>();
            var securityReturns = await GetHistoricalReturnsAsync(symbol, analysisWindow);
            
            // Calculate rolling factor betas
            var rollingBetas = new Dictionary<string, List<double>>();
            foreach (var factorName in factorNames)
            {
                rollingBetas[factorName] = new List<double>();
            }

            for (int i = rollingWindowDays; i < securityReturns.Length; i++)
            {
                var windowReturns = securityReturns[(i - rollingWindowDays)..i];
                var factorData = PrepareFactorData(factorNames, windowReturns.Length, i - rollingWindowDays);
                var regression = PerformMultipleRegression(windowReturns, factorData);

                for (int j = 0; j < factorNames.Count; j++)
                {
                    rollingBetas[factorNames[j]].Add(regression.Betas[j]);
                }
            }

            // Calculate stability as 1 - coefficient of variation
            foreach (var factorName in factorNames)
            {
                var betas = rollingBetas[factorName];
                var mean = betas.Mean();
                var stdDev = betas.StandardDeviation();
                var coefficientOfVariation = Math.Abs(mean) > 1e-10 ? stdDev / Math.Abs(mean) : double.MaxValue;
                stability[factorName] = Math.Max(0, 1 - coefficientOfVariation);
            }

            _logger.LogInformation("Factor stability analysis completed for {Symbol}", symbol);
            return stability;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze factor stability for {Symbol}", symbol);
            throw;
        }
    }

    private void InitializeFactorData()
    {
        // Initialize synthetic factor data (in production, this would come from data providers)
        var random = new Random(42); // Fixed seed for reproducibility
        var dataPoints = 252 * 3; // 3 years of daily data

        _factorReturns["Market"] = GenerateFactorReturns(random, dataPoints, 0.0008, 0.015); // Market excess return
        _factorReturns["SMB"] = GenerateFactorReturns(random, dataPoints, 0.0003, 0.008);   // Small minus Big
        _factorReturns["HML"] = GenerateFactorReturns(random, dataPoints, 0.0002, 0.009);   // High minus Low
        _factorReturns["UMD"] = GenerateFactorReturns(random, dataPoints, 0.0001, 0.012);   // Up minus Down (Momentum)
        _factorReturns["Quality"] = GenerateFactorReturns(random, dataPoints, 0.0002, 0.007);
        _factorReturns["LowVol"] = GenerateFactorReturns(random, dataPoints, -0.0001, 0.006);
    }

    private double[] GenerateFactorReturns(Random random, int count, double meanReturn, double volatility)
    {
        var normal = new Normal(meanReturn, volatility, random);
        return normal.Samples().Take(count).ToArray();
    }

    private async Task<double[]> GetHistoricalReturnsAsync(string symbol, TimeSpan period)
    {
        // In production, this would fetch real historical data
        // For now, generate synthetic returns correlated with factors
        var random = new Random(symbol.GetHashCode());
        var days = (int)period.TotalDays;
        var returns = new double[Math.Min(days, 252 * 3)]; // Limit to available data

        var normal = new Normal(0.0005, 0.02, random); // Slightly positive expected return with 2% daily vol
        for (int i = 0; i < returns.Length; i++)
        {
            returns[i] = normal.Sample();
        }

        return returns;
    }

    private Matrix<double> PrepareFactorData(List<string> factorNames, int observations, int startIndex = 0)
    {
        var factorMatrix = Matrix<double>.Build.Dense(observations, factorNames.Count + 1); // +1 for intercept

        // Add intercept column
        for (int i = 0; i < observations; i++)
        {
            factorMatrix[i, 0] = 1.0;
        }

        // Add factor columns
        for (int j = 0; j < factorNames.Count; j++)
        {
            var factorReturns = GetFactorReturns(factorNames[j], observations, startIndex);
            for (int i = 0; i < observations; i++)
            {
                factorMatrix[i, j + 1] = factorReturns[i];
            }
        }

        return factorMatrix;
    }

    private double[] GetFactorReturns(string factorName, int count, int startIndex = 0)
    {
        if (!_factorReturns.ContainsKey(factorName))
        {
            throw new ArgumentException($"Unknown factor: {factorName}");
        }

        var allReturns = _factorReturns[factorName];
        var endIndex = Math.Min(startIndex + count, allReturns.Length);
        var actualStart = Math.Max(0, endIndex - count);
        
        return allReturns[actualStart..endIndex];
    }

    private RegressionResult PerformMultipleRegression(double[] dependentVariable, Matrix<double> independentVariables)
    {
        var y = Vector<double>.Build.DenseOfArray(dependentVariable);
        var X = independentVariables;

        // Calculate betas using normal equation: β = (X'X)^(-1)X'y
        var XtX = X.TransposeThisAndMultiply(X);
        var Xty = X.TransposeThisAndMultiply(y);
        var betas = XtX.Solve(Xty);

        // Calculate fitted values and residuals
        var yHat = X.Multiply(betas);
        var residuals = y.Subtract(yHat);

        // Calculate R-squared
        var yMean = y.Average();
        var totalSumSquares = y.Sum(yi => Math.Pow(yi - yMean, 2));
        var residualSumSquares = residuals.Sum(r => r * r);
        var rSquared = 1 - (residualSumSquares / totalSumSquares);

        // Calculate adjusted R-squared
        var n = y.Count;
        var k = X.ColumnCount - 1; // Exclude intercept
        var adjustedRSquared = 1 - ((1 - rSquared) * (n - 1) / (n - k - 1));

        // Calculate standard errors and t-statistics
        var mse = residualSumSquares / (n - k - 1);
        var covarianceMatrix = XtX.Inverse().Multiply(mse);
        var standardErrors = Vector<double>.Build.Dense(betas.Count, i => Math.Sqrt(covarianceMatrix[i, i]));
        var tStatistics = betas.PointwiseDivide(standardErrors);

        // Calculate tracking error (annualized)
        var trackingError = residuals.StandardDeviation() * Math.Sqrt(252);

        return new RegressionResult
        {
            Betas = betas.ToArray(),
            StandardErrors = standardErrors.ToArray(),
            TStatistics = tStatistics.ToArray(),
            RSquared = rSquared,
            AdjustedRSquared = adjustedRSquared,
            TrackingError = trackingError,
            Residuals = residuals.ToArray()
        };
    }

    private List<Factor> BuildFactorList(List<string> factorNames, RegressionResult regression)
    {
        var factors = new List<Factor>();

        for (int i = 0; i < factorNames.Count; i++)
        {
            var beta = regression.Betas[i + 1]; // Skip intercept
            var tStat = regression.TStatistics[i + 1];
            var pValue = CalculatePValue(tStat, regression.Betas.Length);

            factors.Add(new Factor
            {
                Name = factorNames[i],
                Beta = beta,
                TStatistic = tStat,
                PValue = pValue,
                Exposure = beta
            });
        }

        return factors;
    }

    private double CalculatePValue(double tStatistic, int degreesOfFreedom)
    {
        var tDist = new StudentT(0, 1, degreesOfFreedom);
        return 2 * (1 - tDist.CumulativeDistribution(Math.Abs(tStatistic)));
    }

    private double CalculateInformationRatio(RegressionResult regression)
    {
        var alpha = regression.Betas[0]; // Intercept is alpha
        var alphaStdError = regression.StandardErrors[0];
        return Math.Abs(alphaStdError) > 1e-10 ? alpha / alphaStdError : 0;
    }

    private class RegressionResult
    {
        public double[] Betas { get; set; } = Array.Empty<double>();
        public double[] StandardErrors { get; set; } = Array.Empty<double>();
        public double[] TStatistics { get; set; } = Array.Empty<double>();
        public double RSquared { get; set; }
        public double AdjustedRSquared { get; set; }
        public double TrackingError { get; set; }
        public double[] Residuals { get; set; } = Array.Empty<double>();
    }
}