namespace QuantResearchAgent.Core;

/// <summary>
/// Represents a podcast episode with analysis results
/// </summary>
public class PodcastEpisode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Transcript { get; set; }
    public DateTime PublishedDate { get; set; }
    public string PodcastUrl { get; set; } = string.Empty;
    public List<string> TechnicalInsights { get; set; } = new();
    public List<string> TradingSignals { get; set; } = new();
    public double SentimentScore { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

/// <summary>
/// Represents a trading signal with metadata
/// </summary>
public class TradingSignal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Symbol { get; set; } = string.Empty;
    public SignalType Type { get; set; }
    public double Strength { get; set; } // 0.0 to 1.0
    public double Price { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Source { get; set; } = string.Empty; // podcast, technical_analysis, etc.
    public string Reasoning { get; set; } = string.Empty;
    public double? StopLoss { get; set; }
    public double? TakeProfit { get; set; }
    public TimeSpan? Duration { get; set; }
    public SignalStatus Status { get; set; } = SignalStatus.Active;
}

public enum SignalType
{
    Buy,
    Sell,
    Hold,
    StrongBuy,
    StrongSell
}

public enum SignalStatus
{
    Active,
    Executed,
    Expired,
    Cancelled
}

/// <summary>
/// Represents market data for a symbol
/// </summary>
public class MarketData
{
    public string Symbol { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // e.g. "local", "yahoo", "binance", etc.
    public double Price { get; set; }
    public double Volume { get; set; }
    public double Change24h { get; set; }
    public double ChangePercent24h { get; set; }
    public double High24h { get; set; }
    public double Low24h { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> TechnicalIndicators { get; set; } = new();
}

/// <summary>
/// Represents a portfolio position
/// </summary>
public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double AveragePrice { get; set; }
    public double CurrentPrice { get; set; }
    public double UnrealizedPnL => (CurrentPrice - AveragePrice) * Quantity;
    public double UnrealizedPnLPercent => ((CurrentPrice - AveragePrice) / AveragePrice) * 100;
    public DateTime OpenedAt { get; set; }
    public string? StopLoss { get; set; }
    public string? TakeProfit { get; set; }
}

/// <summary>
/// Represents portfolio performance metrics
/// </summary>
public class PortfolioMetrics
{
    public double TotalValue { get; set; }
    public double TotalPnL { get; set; }
    public double TotalPnLPercent { get; set; }
    public double DailyReturn { get; set; }
    public double Volatility { get; set; }
    public double SharpeRatio { get; set; }
    public double MaxDrawdown { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double WinRate => WinningTrades + LosingTrades > 0 ? (double)WinningTrades / (WinningTrades + LosingTrades) : 0;
    public DateTime CalculatedAt { get; set; }
}

/// <summary>
/// Represents an agent job/task
/// </summary>
public class AgentJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty; // podcast_analysis, signal_generation, etc.
    public Dictionary<string, object> Parameters { get; set; } = new();
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int Priority { get; set; } = 1; // 1 = highest, 10 = lowest
}

public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Configuration for risk management
/// </summary>
public class RiskManagementConfig
{
    public double MaxDrawdown { get; set; } = 0.15;
    public double VolatilityTarget { get; set; } = 0.12;
    public int MaxPositions { get; set; } = 10;
    public double PositionSizePercent { get; set; } = 0.1;
    public double StopLossPercent { get; set; } = 0.05;
    public double TakeProfitPercent { get; set; } = 0.10;
}

// ========================
// ADVANCED FINANCE ANALYTICS MODELS
// ========================

/// <summary>
/// Options contract data and analytics
/// </summary>
public class OptionsContract
{
    public string Symbol { get; set; } = string.Empty;
    public string UnderlyingSymbol { get; set; } = string.Empty;
    public OptionType Type { get; set; }
    public double Strike { get; set; }
    public DateTime Expiration { get; set; }
    public double Premium { get; set; }
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Volume { get; set; }
    public double OpenInterest { get; set; }
    public double ImpliedVolatility { get; set; }
    public OptionsGreeks Greeks { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public enum OptionType
{
    Call,
    Put
}

/// <summary>
/// Options Greeks for risk management
/// </summary>
public class OptionsGreeks
{
    public double Delta { get; set; }      // Price sensitivity
    public double Gamma { get; set; }      // Delta sensitivity
    public double Theta { get; set; }      // Time decay
    public double Vega { get; set; }       // Volatility sensitivity
    public double Rho { get; set; }        // Interest rate sensitivity
    public double Lambda { get; set; }     // Leverage/elasticity
}

/// <summary>
/// Black-Scholes pricing model result
/// </summary>
public class BlackScholesResult
{
    public double OptionPrice { get; set; }
    public OptionsGreeks Greeks { get; set; } = new();
    public double ImpliedVolatility { get; set; }
    public double TimeToExpiration { get; set; }
    public double RiskFreeRate { get; set; }
    public DateTime CalculatedAt { get; set; }
}

/// <summary>
/// Factor analysis model for multi-factor returns
/// </summary>
public class FactorModel
{
    public string Name { get; set; } = string.Empty;
    public List<Factor> Factors { get; set; } = new();
    public double RSquared { get; set; }
    public double AdjustedRSquared { get; set; }
    public double TrackingError { get; set; }
    public double InformationRatio { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class Factor
{
    public string Name { get; set; } = string.Empty;
    public double Beta { get; set; }
    public double TStatistic { get; set; }
    public double PValue { get; set; }
    public double Exposure { get; set; }
    public bool IsSignificant => PValue < 0.05;
}

/// <summary>
/// Factor attribution analysis
/// </summary>
public class FactorAttribution
{
    public string Symbol { get; set; } = string.Empty;
    public double TotalReturn { get; set; }
    public double AlphaReturn { get; set; }
    public List<FactorContribution> FactorReturns { get; set; } = new();
    public double SpecificReturn { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class FactorContribution
{
    public string FactorName { get; set; } = string.Empty;
    public double Exposure { get; set; }
    public double FactorReturn { get; set; }
    public double Contribution { get; set; }
}

/// <summary>
/// Advanced risk metrics (VaR, CVaR, etc.)
/// </summary>
public class AdvancedRiskMetrics
{
    public string Symbol { get; set; } = string.Empty;
    public double ValueAtRisk95 { get; set; }
    public double ValueAtRisk99 { get; set; }
    public double ConditionalVaR95 { get; set; }
    public double ConditionalVaR99 { get; set; }
    public double ExpectedShortfall { get; set; }
    public double MaximumDrawdown { get; set; }
    public double Skewness { get; set; }
    public double Kurtosis { get; set; }
    public double TailRatio { get; set; }
    public List<StressTestResult> StressTests { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
}

public class StressTestResult
{
    public string ScenarioName { get; set; } = string.Empty;
    public double ReturnImpact { get; set; }
    public double VolatilityImpact { get; set; }
    public double PortfolioImpact { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Cross-asset analytics for multi-asset portfolios
/// </summary>
public class CrossAssetAnalytics
{
    public List<AssetCorrelation> Correlations { get; set; } = new();
    public CurrencyExposure CurrencyExposure { get; set; } = new();
    public List<ArbitragePair> ArbitrageOpportunities { get; set; } = new();
    public List<HedgeRecommendation> HedgeRecommendations { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
}

public class AssetCorrelation
{
    public string Asset1 { get; set; } = string.Empty;
    public string Asset2 { get; set; } = string.Empty;
    public double Correlation { get; set; }
    public double RollingCorrelation30D { get; set; }
    public double CorrelationVolatility { get; set; }
    public bool IsStable { get; set; }
}

public class CurrencyExposure
{
    public Dictionary<string, double> Exposures { get; set; } = new();
    public double TotalExposure { get; set; }
    public List<CurrencyHedge> RecommendedHedges { get; set; } = new();
}

public class CurrencyHedge
{
    public string Currency { get; set; } = string.Empty;
    public double ExposureAmount { get; set; }
    public double HedgeRatio { get; set; }
    public string HedgeInstrument { get; set; } = string.Empty;
    public double Cost { get; set; }
}

public class ArbitragePair
{
    public string Asset1 { get; set; } = string.Empty;
    public string Asset2 { get; set; } = string.Empty;
    public double PriceSpread { get; set; }
    public double HistoricalMeanSpread { get; set; }
    public double ZScore { get; set; }
    public double Confidence { get; set; }
    public string Strategy { get; set; } = string.Empty;
}

public class HedgeRecommendation
{
    public string AssetToHedge { get; set; } = string.Empty;
    public string HedgeInstrument { get; set; } = string.Empty;
    public double HedgeRatio { get; set; }
    public double EffectivenessScore { get; set; }
    public string Rationale { get; set; } = string.Empty;
}
