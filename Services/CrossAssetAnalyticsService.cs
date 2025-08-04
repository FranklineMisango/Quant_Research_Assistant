using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using QuantResearchAgent.Core;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Distributions;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Services;

/// <summary>
/// Cross-Asset Analytics Service - Provides multi-asset analysis, currency hedging, and arbitrage detection
/// Essential for multi-asset portfolio management and cross-market trading strategies
/// </summary>
public class CrossAssetAnalyticsService
{
    private readonly ILogger<CrossAssetAnalyticsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MarketDataService _marketDataService;
    private readonly PortfolioService _portfolioService;
    private readonly ConcurrentDictionary<string, CrossAssetAnalytics> _analyticsCache = new();

    // Currency pairs for hedging analysis
    private readonly List<string> _majorCurrencyPairs = new()
    {
        "EURUSD", "GBPUSD", "USDJPY", "USDCHF", "USDCAD", "AUDUSD", "NZDUSD"
    };

    // Asset class mappings
    private readonly Dictionary<string, AssetClass> _assetClassMap = new();

    public CrossAssetAnalyticsService(
        ILogger<CrossAssetAnalyticsService> logger,
        IConfiguration configuration,
        MarketDataService marketDataService,
        PortfolioService portfolioService)
    {
        _logger = logger;
        _configuration = configuration;
        _marketDataService = marketDataService;
        _portfolioService = portfolioService;
        InitializeAssetClassMappings();
    }

    /// <summary>
    /// Perform comprehensive cross-asset analysis
    /// </summary>
    public async Task<CrossAssetAnalytics> PerformCrossAssetAnalysisAsync(
        List<string> symbols,
        TimeSpan analysisWindow)
    {
        try
        {
            _logger.LogInformation("Performing cross-asset analysis for {Count} symbols over {Window}", 
                symbols.Count, analysisWindow);

            var cacheKey = $"{string.Join("_", symbols)}_{analysisWindow.TotalDays}";
            if (_analyticsCache.TryGetValue(cacheKey, out var cached) && 
                DateTime.UtcNow - cached.AnalysisDate < TimeSpan.FromHours(6))
            {
                _logger.LogInformation("Returning cached cross-asset analytics");
                return cached;
            }

            var analytics = new CrossAssetAnalytics
            {
                AnalysisDate = DateTime.UtcNow
            };

            // Calculate correlations between all asset pairs
            analytics.Correlations = await CalculateAssetCorrelationsAsync(symbols, analysisWindow);

            // Analyze currency exposure
            analytics.CurrencyExposure = await AnalyzeCurrencyExposureAsync(symbols);

            // Detect arbitrage opportunities
            analytics.ArbitrageOpportunities = await DetectArbitrageOpportunitiesAsync(symbols, analysisWindow);

            // Generate hedge recommendations
            analytics.HedgeRecommendations = await GenerateHedgeRecommendationsAsync(symbols, analytics.Correlations);

            _analyticsCache[cacheKey] = analytics;

            _logger.LogInformation("Cross-asset analysis completed. Found {Correlations} correlations, {Arb} arbitrage opportunities", 
                analytics.Correlations.Count, analytics.ArbitrageOpportunities.Count);

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform cross-asset analysis");
            throw;
        }
    }

    /// <summary>
    /// Calculate rolling correlations and correlation stability
    /// </summary>
    public async Task<List<AssetCorrelation>> CalculateAssetCorrelationsAsync(
        List<string> symbols,
        TimeSpan analysisWindow,
        int rollingWindow = 30)
    {
        try
        {
            _logger.LogInformation("Calculating correlations for {Count} symbols", symbols.Count);

            var correlations = new List<AssetCorrelation>();
            var returnsData = new Dictionary<string, double[]>();

            // Get historical returns for all symbols
            foreach (var symbol in symbols)
            {
                returnsData[symbol] = await GetHistoricalReturnsAsync(symbol, analysisWindow);
            }

            // Calculate pairwise correlations
            for (int i = 0; i < symbols.Count; i++)
            {
                for (int j = i + 1; j < symbols.Count; j++)
                {
                    var symbol1 = symbols[i];
                    var symbol2 = symbols[j];
                    var returns1 = returnsData[symbol1];
                    var returns2 = returnsData[symbol2];

                    if (returns1.Length != returns2.Length)
                    {
                        var minLength = Math.Min(returns1.Length, returns2.Length);
                        returns1 = returns1.Take(minLength).ToArray();
                        returns2 = returns2.Take(minLength).ToArray();
                    }

                    var correlation = CalculateCorrelation(returns1, returns2);
                    var rollingCorrelation = CalculateRollingCorrelation(returns1, returns2, rollingWindow);
                    var correlationVolatility = rollingCorrelation.StandardDeviation();

                    correlations.Add(new AssetCorrelation
                    {
                        Asset1 = symbol1,
                        Asset2 = symbol2,
                        Correlation = correlation,
                        RollingCorrelation30D = rollingCorrelation.LastOrDefault(),
                        CorrelationVolatility = correlationVolatility,
                        IsStable = correlationVolatility < 0.2 // Threshold for stability
                    });
                }
            }

            _logger.LogInformation("Calculated {Count} pairwise correlations", correlations.Count);
            return correlations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate asset correlations");
            throw;
        }
    }

    /// <summary>
    /// Analyze currency exposure across portfolio
    /// </summary>
    public async Task<CurrencyExposure> AnalyzeCurrencyExposureAsync(List<string> symbols)
    {
        try
        {
            _logger.LogInformation("Analyzing currency exposure for {Count} symbols", symbols.Count);

            var positions = await _portfolioService.GetPositionsAsync();
            var currencyExposure = new CurrencyExposure();

            foreach (var position in positions.Where(p => symbols.Contains(p.Symbol)))
            {
                var currency = GetBaseCurrency(position.Symbol);
                var exposureAmount = position.Quantity * position.CurrentPrice;

                if (currencyExposure.Exposures.ContainsKey(currency))
                {
                    currencyExposure.Exposures[currency] += exposureAmount;
                }
                else
                {
                    currencyExposure.Exposures[currency] = exposureAmount;
                }
            }

            currencyExposure.TotalExposure = currencyExposure.Exposures.Values.Sum();

            // Generate hedge recommendations for significant exposures
            foreach (var exposure in currencyExposure.Exposures.Where(e => Math.Abs(e.Value) > currencyExposure.TotalExposure * 0.1))
            {
                if (exposure.Key != "USD") // Assume USD is base currency
                {
                    var hedgeRatio = CalculateOptimalHedgeRatio(exposure.Key, exposure.Value);
                    currencyExposure.RecommendedHedges.Add(new CurrencyHedge
                    {
                        Currency = exposure.Key,
                        ExposureAmount = exposure.Value,
                        HedgeRatio = hedgeRatio,
                        HedgeInstrument = $"{exposure.Key}USD_FWD", // Forward contract
                        Cost = CalculateHedgingCost(exposure.Key, exposure.Value, hedgeRatio)
                    });
                }
            }

            _logger.LogInformation("Currency exposure analysis completed. Total exposure: {Total:C} across {Count} currencies", 
                currencyExposure.TotalExposure, currencyExposure.Exposures.Count);

            return currencyExposure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze currency exposure");
            throw;
        }
    }

    /// <summary>
    /// Detect statistical arbitrage opportunities
    /// </summary>
    public async Task<List<ArbitragePair>> DetectArbitrageOpportunitiesAsync(
        List<string> symbols,
        TimeSpan analysisWindow,
        double zScoreThreshold = 2.0)
    {
        try
        {
            _logger.LogInformation("Detecting arbitrage opportunities with Z-score threshold {Threshold}", zScoreThreshold);

            var opportunities = new List<ArbitragePair>();
            var priceData = new Dictionary<string, double[]>();

            // Get historical price data
            foreach (var symbol in symbols)
            {
                priceData[symbol] = await GetHistoricalPricesAsync(symbol, analysisWindow);
            }

            // Look for pairs with stable spread relationships
            for (int i = 0; i < symbols.Count; i++)
            {
                for (int j = i + 1; j < symbols.Count; j++)
                {
                    var symbol1 = symbols[i];
                    var symbol2 = symbols[j];

                    // Skip if different asset classes (unless specific cross-asset strategy)
                    if (!ShouldConsiderForArbitrage(symbol1, symbol2))
                        continue;

                    var prices1 = priceData[symbol1];
                    var prices2 = priceData[symbol2];

                    if (prices1.Length != prices2.Length)
                    {
                        var minLength = Math.Min(prices1.Length, prices2.Length);
                        prices1 = prices1.Take(minLength).ToArray();
                        prices2 = prices2.Take(minLength).ToArray();
                    }

                    // Calculate price spreads and check for mean reversion
                    var spreads = CalculatePriceSpreads(prices1, prices2);
                    var meanSpread = spreads.Mean();
                    var spreadStdDev = spreads.StandardDeviation();
                    var currentSpread = spreads.Last();
                    var zScore = Math.Abs(currentSpread - meanSpread) / spreadStdDev;

                    if (zScore > zScoreThreshold && spreadStdDev > 0)
                    {
                        var confidence = CalculateArbitrageConfidence(spreads);
                        var strategy = DetermineArbitrageStrategy(currentSpread, meanSpread, symbol1, symbol2);

                        opportunities.Add(new ArbitragePair
                        {
                            Asset1 = symbol1,
                            Asset2 = symbol2,
                            PriceSpread = currentSpread,
                            HistoricalMeanSpread = meanSpread,
                            ZScore = zScore,
                            Confidence = confidence,
                            Strategy = strategy
                        });
                    }
                }
            }

            // Sort by confidence and Z-score
            opportunities = opportunities.OrderByDescending(o => o.Confidence * o.ZScore).ToList();

            _logger.LogInformation("Detected {Count} arbitrage opportunities", opportunities.Count);
            return opportunities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect arbitrage opportunities");
            throw;
        }
    }

    /// <summary>
    /// Generate hedge recommendations based on portfolio analysis
    /// </summary>
    public async Task<List<HedgeRecommendation>> GenerateHedgeRecommendationsAsync(
        List<string> symbols,
        List<AssetCorrelation> correlations)
    {
        try
        {
            _logger.LogInformation("Generating hedge recommendations for {Count} symbols", symbols.Count);

            var recommendations = new List<HedgeRecommendation>();
            var positions = await _portfolioService.GetPositionsAsync();

            foreach (var position in positions.Where(p => symbols.Contains(p.Symbol)))
            {
                var hedges = await FindOptimalHedgeInstrumentsAsync(position.Symbol, correlations);
                recommendations.AddRange(hedges.Take(3)); // Top 3 hedge recommendations per asset
            }

            // Sort by effectiveness score
            recommendations = recommendations.OrderByDescending(r => r.EffectivenessScore).ToList();

            _logger.LogInformation("Generated {Count} hedge recommendations", recommendations.Count);
            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate hedge recommendations");
            throw;
        }
    }

    /// <summary>
    /// Calculate optimal portfolio allocation across asset classes
    /// </summary>
    public async Task<Dictionary<AssetClass, double>> CalculateOptimalAllocationAsync(
        List<string> symbols,
        double targetReturn,
        double riskTolerance)
    {
        try
        {
            _logger.LogInformation("Calculating optimal allocation for target return {Return:P2} with risk tolerance {Risk:P2}", 
                targetReturn, riskTolerance);

            var assetClassReturns = new Dictionary<AssetClass, List<double>>();
            var assetClassRisks = new Dictionary<AssetClass, double>();

            // Group symbols by asset class and calculate metrics
            foreach (var symbol in symbols)
            {
                var assetClass = GetAssetClass(symbol);
                var returns = await GetHistoricalReturnsAsync(symbol, TimeSpan.FromDays(252));

                if (!assetClassReturns.ContainsKey(assetClass))
                {
                    assetClassReturns[assetClass] = new List<double>();
                }
                assetClassReturns[assetClass].AddRange(returns);
            }

            // Calculate mean returns and risks for each asset class
            foreach (var assetClass in assetClassReturns.Keys)
            {
                var returns = assetClassReturns[assetClass];
                assetClassRisks[assetClass] = returns.StandardDeviation();
            }

            // Simple optimization: weight asset classes based on Sharpe ratio and risk tolerance
            var allocation = new Dictionary<AssetClass, double>();
            var totalWeight = 0.0;

            foreach (var assetClass in assetClassReturns.Keys)
            {
                var returns = assetClassReturns[assetClass];
                var meanReturn = returns.Mean() * 252; // Annualize
                var risk = assetClassRisks[assetClass] * Math.Sqrt(252); // Annualize
                var sharpeRatio = risk > 0 ? meanReturn / risk : 0;
                
                // Weight by Sharpe ratio, adjusted for risk tolerance
                var weight = Math.Max(0, sharpeRatio * (1 - risk * (1 - riskTolerance)));
                allocation[assetClass] = weight;
                totalWeight += weight;
            }

            // Normalize weights
            if (totalWeight > 0)
            {
                var normalizedAllocation = new Dictionary<AssetClass, double>();
                foreach (var kvp in allocation)
                {
                    normalizedAllocation[kvp.Key] = kvp.Value / totalWeight;
                }
                allocation = normalizedAllocation;
            }

            _logger.LogInformation("Optimal allocation calculated across {Count} asset classes", allocation.Count);
            return allocation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate optimal allocation");
            throw;
        }
    }

    #region Private Helper Methods

    private double CalculateCorrelation(double[] returns1, double[] returns2)
    {
        if (returns1.Length != returns2.Length || returns1.Length < 2)
            return 0;

        return Correlation.Pearson(returns1, returns2);
    }

    private double[] CalculateRollingCorrelation(double[] returns1, double[] returns2, int window)
    {
        var rollingCorr = new List<double>();

        for (int i = window; i <= returns1.Length; i++)
        {
            var subset1 = returns1[(i - window)..i];
            var subset2 = returns2[(i - window)..i];
            rollingCorr.Add(CalculateCorrelation(subset1, subset2));
        }

        return rollingCorr.ToArray();
    }

    private double[] CalculatePriceSpreads(double[] prices1, double[] prices2)
    {
        var spreads = new double[prices1.Length];
        for (int i = 0; i < prices1.Length; i++)
        {
            spreads[i] = prices1[i] - prices2[i];
        }
        return spreads;
    }

    private double CalculateArbitrageConfidence(double[] spreads)
    {
        // Calculate mean reversion strength using Hurst exponent approximation
        var returns = new double[spreads.Length - 1];
        for (int i = 1; i < spreads.Length; i++)
        {
            returns[i - 1] = spreads[i] - spreads[i - 1];
        }

        var variance = returns.Variance();
        var halfLife = EstimateHalfLife(spreads);
        
        // Higher confidence for stronger mean reversion and shorter half-life
        return Math.Max(0, Math.Min(1, (1 / Math.Max(halfLife, 1)) * (1 / Math.Max(variance, 0.01))));
    }

    private double EstimateHalfLife(double[] spreads)
    {
        // Simple estimate: time for spread to mean-revert 50%
        var mean = spreads.Mean();
        var maxDeviation = spreads.Max(s => Math.Abs(s - mean));
        var halfTarget = maxDeviation * 0.5;

        for (int i = 1; i < spreads.Length; i++)
        {
            if (Math.Abs(spreads[i] - mean) <= halfTarget)
            {
                return i;
            }
        }

        return spreads.Length / 2.0; // Default to half the period
    }

    private string DetermineArbitrageStrategy(double currentSpread, double meanSpread, string asset1, string asset2)
    {
        if (currentSpread > meanSpread)
        {
            return $"Short {asset1}, Long {asset2} (spread too wide)";
        }
        else
        {
            return $"Long {asset1}, Short {asset2} (spread too narrow)";
        }
    }

    private bool ShouldConsiderForArbitrage(string symbol1, string symbol2)
    {
        var class1 = GetAssetClass(symbol1);
        var class2 = GetAssetClass(symbol2);
        
        // Consider same asset class or specific cross-asset strategies
        return class1 == class2 || (class1 == AssetClass.Equity && class2 == AssetClass.ETF);
    }

    private async Task<List<HedgeRecommendation>> FindOptimalHedgeInstrumentsAsync(
        string assetToHedge,
        List<AssetCorrelation> correlations)
    {
        var recommendations = new List<HedgeRecommendation>();

        // Find negatively correlated assets
        var negativeCorrelations = correlations
            .Where(c => (c.Asset1 == assetToHedge || c.Asset2 == assetToHedge) && c.Correlation < -0.3)
            .OrderBy(c => c.Correlation)
            .Take(5);

        foreach (var corr in negativeCorrelations)
        {
            var hedgeInstrument = corr.Asset1 == assetToHedge ? corr.Asset2 : corr.Asset1;
            var hedgeRatio = Math.Abs(corr.Correlation);
            var effectiveness = Math.Abs(corr.Correlation) * (corr.IsStable ? 1.2 : 1.0);

            recommendations.Add(new HedgeRecommendation
            {
                AssetToHedge = assetToHedge,
                HedgeInstrument = hedgeInstrument,
                HedgeRatio = hedgeRatio,
                EffectivenessScore = effectiveness,
                Rationale = $"Strong negative correlation ({corr.Correlation:F2}) with {(corr.IsStable ? "stable" : "variable")} relationship"
            });
        }

        return recommendations;
    }

    private string GetBaseCurrency(string symbol)
    {
        // Simple mapping - in production, this would use proper symbol metadata
        if (symbol.Contains("USD")) return "USD";
        if (symbol.Contains("EUR")) return "EUR";
        if (symbol.Contains("GBP")) return "GBP";
        if (symbol.Contains("JPY")) return "JPY";
        if (symbol.EndsWith(".L")) return "GBP"; // London Stock Exchange
        if (symbol.EndsWith(".T")) return "JPY"; // Tokyo Stock Exchange
        return "USD"; // Default
    }

    private double CalculateOptimalHedgeRatio(string currency, double exposureAmount)
    {
        // Simplified hedge ratio calculation
        // In practice, this would use regression-based hedge ratios
        return Math.Min(1.0, Math.Abs(exposureAmount) / 1000000); // Scale based on exposure size
    }

    private double CalculateHedgingCost(string currency, double exposureAmount, double hedgeRatio)
    {
        // Simplified cost calculation (bid-ask spread + forward points)
        var notional = Math.Abs(exposureAmount * hedgeRatio);
        var bidAskSpread = 0.0002; // 2 pips
        var forwardPoints = 0.0001; // 1 pip
        return notional * (bidAskSpread + forwardPoints);
    }

    private AssetClass GetAssetClass(string symbol)
    {
        if (_assetClassMap.ContainsKey(symbol))
        {
            return _assetClassMap[symbol];
        }

        // Simple classification based on symbol patterns
        if (symbol.Contains("BTC") || symbol.Contains("ETH") || symbol.Contains("USDT"))
            return AssetClass.Cryptocurrency;
        if (symbol.Contains("USD") || symbol.Contains("EUR") || symbol.Contains("GBP"))
            return AssetClass.Currency;
        if (symbol.Contains("SPY") || symbol.Contains("VTI") || symbol.Contains("ETF"))
            return AssetClass.ETF;
        if (symbol.Contains("GLD") || symbol.Contains("SLV") || symbol.Contains("OIL"))
            return AssetClass.Commodity;

        return AssetClass.Equity; // Default
    }

    private async Task<double[]> GetHistoricalReturnsAsync(string symbol, TimeSpan period)
    {
        // Generate synthetic returns for demo
        var random = new Random(symbol.GetHashCode());
        var days = (int)period.TotalDays;
        var returns = new double[Math.Min(days, 252 * 3)];

        var assetClass = GetAssetClass(symbol);
        var (meanReturn, volatility) = GetAssetClassParameters(assetClass);

        var normal = new Normal(meanReturn, volatility, random);
        for (int i = 0; i < returns.Length; i++)
        {
            returns[i] = normal.Sample();
        }

        return returns;
    }

    private async Task<double[]> GetHistoricalPricesAsync(string symbol, TimeSpan period)
    {
        var returns = await GetHistoricalReturnsAsync(symbol, period);
        var prices = new double[returns.Length];
        prices[0] = 100; // Starting price

        for (int i = 1; i < prices.Length; i++)
        {
            prices[i] = prices[i - 1] * (1 + returns[i]);
        }

        return prices;
    }

    private (double meanReturn, double volatility) GetAssetClassParameters(AssetClass assetClass)
    {
        return assetClass switch
        {
            AssetClass.Equity => (0.0008, 0.02),
            AssetClass.ETF => (0.0006, 0.015),
            AssetClass.Currency => (0.0001, 0.008),
            AssetClass.Commodity => (0.0003, 0.025),
            AssetClass.Cryptocurrency => (0.002, 0.05),
            AssetClass.Bond => (0.0002, 0.005),
            _ => (0.0005, 0.02)
        };
    }

    private void InitializeAssetClassMappings()
    {
        // Initialize with common symbols - in production, this would be loaded from configuration
        _assetClassMap["AAPL"] = AssetClass.Equity;
        _assetClassMap["GOOGL"] = AssetClass.Equity;
        _assetClassMap["MSFT"] = AssetClass.Equity;
        _assetClassMap["SPY"] = AssetClass.ETF;
        _assetClassMap["VTI"] = AssetClass.ETF;
        _assetClassMap["BTCUSDT"] = AssetClass.Cryptocurrency;
        _assetClassMap["ETHUSDT"] = AssetClass.Cryptocurrency;
        _assetClassMap["EURUSD"] = AssetClass.Currency;
        _assetClassMap["GBPUSD"] = AssetClass.Currency;
    }

    #endregion
}

public enum AssetClass
{
    Equity,
    ETF,
    Bond,
    Currency,
    Commodity,
    Cryptocurrency,
    RealEstate,
    Alternative
}