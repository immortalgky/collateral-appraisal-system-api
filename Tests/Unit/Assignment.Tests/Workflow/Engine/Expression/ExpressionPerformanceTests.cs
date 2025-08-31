using Assignment.Workflow.Engine.Expression;
using FluentAssertions;
using System.Diagnostics;
using Xunit;

namespace Assignment.Tests.Workflow.Engine.Expression;

public class ExpressionPerformanceTests
{
    private readonly IExpressionEvaluator _evaluator;

    public ExpressionPerformanceTests()
    {
        _evaluator = new ExpressionEvaluator();
    }

    [Fact]
    public void ExpressionEvaluation_Performance_MeetsThresholds()
    {
        // Arrange
        var expressions = new[]
        {
            "status == 'active'",
            "(amount > 1000 && priority == 'high') || urgent == true",
            "!((credit_score < 600 && amount > 50000) || (employment == 'unemployed' && amount > 10000))",
            "(role == 'manager' || role == 'admin') && (department == 'IT' || department == 'Finance') && experience_years >= 5"
        };

        var variables = new Dictionary<string, object>
        {
            ["status"] = "active",
            ["amount"] = 15000,
            ["priority"] = "high",
            ["urgent"] = false,
            ["credit_score"] = 720,
            ["employment"] = "employed",
            ["role"] = "manager",
            ["department"] = "IT",
            ["experience_years"] = 8
        };

        const int iterations = 10000;

        foreach (var expression in expressions)
        {
            // Warm up - compile the expression
            _evaluator.EvaluateExpression(expression, variables);

            // Measure performance
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _evaluator.EvaluateExpression(expression, variables);
            }
            stopwatch.Stop();

            var avgMicroseconds = (stopwatch.ElapsedTicks * 1000000.0) / Stopwatch.Frequency / iterations;

            // Assert - Should complete in under 100 microseconds on average
            avgMicroseconds.Should().BeLessThan(100,
                $"Expression '{expression}' took {avgMicroseconds:F2} μs on average, exceeding 100 μs threshold");
        }
    }

    [Fact]
    public void ExpressionCaching_MemoryUsage_StaysWithinLimits()
    {
        // Arrange
        const int expressionCount = 1000;
        var baseMemory = GC.GetTotalMemory(true);

        // Act - Create many different expressions to test cache behavior
        for (int i = 0; i < expressionCount; i++)
        {
            var expression = $"value_{i} > {i} && status == 'test_{i}'";
            var variables = new Dictionary<string, object>
            {
                [$"value_{i}"] = i + 1,
                ["status"] = $"test_{i}"
            };

            _evaluator.EvaluateExpression(expression, variables);
        }

        var afterMemory = GC.GetTotalMemory(true);
        var memoryIncrease = afterMemory - baseMemory;

        // Assert - Memory increase should be reasonable (less than 10MB for 1000 expressions)
        memoryIncrease.Should().BeLessThan(10 * 1024 * 1024,
            $"Memory increased by {memoryIncrease / 1024.0 / 1024.0:F2} MB, exceeding 10 MB limit");
    }

    [Fact]
    public void ExpressionEvaluation_ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        var expression = "amount > threshold && status == 'approved'";
        var variables = new Dictionary<string, object>
        {
            ["amount"] = 1500,
            ["threshold"] = 1000,
            ["status"] = "approved"
        };

        const int threadCount = 10;
        const int iterationsPerThread = 1000;
        var results = new bool[threadCount];
        var exceptions = new List<Exception>();

        // Act - Run multiple threads concurrently
        var tasks = Enumerable.Range(0, threadCount)
            .Select(threadIndex => Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        var result = _evaluator.EvaluateExpression(expression, variables);
                        if (i == 0) results[threadIndex] = result; // Store first result for verification
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent access");
        results.Should().OnlyContain(r => r == true, "All threads should produce the same result");
    }

    [Fact]
    public void ExpressionEvaluation_ComplexExpressions_MaintainPerformance()
    {
        // Arrange - Highly complex real-world appraisal expressions
        var complexExpressions = new[]
        {
            // Multi-level approval routing
            "(property_value > 1000000 && (property_type == 'commercial' || property_type == 'industrial')) || " +
            "(loan_to_value > 0.8 && credit_score < 700) || " +
            "(complexity_score > 8 && market_volatility == 'high')",

            // Geographic and temporal conditions
            "(market_area contains 'metro' && property_age < 10 && recent_sales_count > 50) && " +
            "(seasonal_factor > 1.2 || market_trend == 'rising') && " +
            "!economic_downturn",

            // Client and priority based routing
            "((client_tier == 'VIP' || client_tier == 'Enterprise') && relationship_years > 5) && " +
            "(priority == 'rush' || (property_value > 500000 && completion_days <= 3)) || " +
            "(referral_source == 'internal' && previous_appraisals_count > 10)"
        };

        var variables = new Dictionary<string, object>
        {
            ["property_value"] = 1200000,
            ["property_type"] = "commercial",
            ["loan_to_value"] = 0.75,
            ["credit_score"] = 720,
            ["complexity_score"] = 7.5,
            ["market_volatility"] = "medium",
            ["market_area"] = "downtown metro",
            ["property_age"] = 8,
            ["recent_sales_count"] = 75,
            ["seasonal_factor"] = 1.1,
            ["market_trend"] = "stable",
            ["economic_downturn"] = false,
            ["client_tier"] = "VIP",
            ["relationship_years"] = 7,
            ["priority"] = "standard",
            ["completion_days"] = 5,
            ["referral_source"] = "external",
            ["previous_appraisals_count"] = 15
        };

        const int iterations = 1000;

        foreach (var expression in complexExpressions)
        {
            // Warm up
            _evaluator.EvaluateExpression(expression, variables);

            // Measure
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _evaluator.EvaluateExpression(expression, variables);
            }
            stopwatch.Stop();

            var avgMicroseconds = (stopwatch.ElapsedTicks * 1000000.0) / Stopwatch.Frequency / iterations;

            // Assert - Complex expressions should still be fast (under 500 microseconds)
            avgMicroseconds.Should().BeLessThan(500,
                $"Complex expression took {avgMicroseconds:F2} μs on average, exceeding 500 μs threshold");
        }
    }

    [Fact]
    public void ExpressionValidation_Performance_IsFast()
    {
        // Arrange
        var expressions = new[]
        {
            "status == 'active'",
            "invalid expression syntax ===",
            "(property_value > 250000 && credit_score >= 700)",
            "malformed (( expression",
            "complex && valid && expression || with && many && operators"
        };

        const int iterations = 1000;

        foreach (var expression in expressions)
        {
            // Measure validation performance
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _evaluator.ValidateExpression(expression, out _);
            }
            stopwatch.Stop();

            var avgMicroseconds = (stopwatch.ElapsedTicks * 1000000.0) / Stopwatch.Frequency / iterations;

            // Assert - Validation should be very fast (under 50 microseconds)
            avgMicroseconds.Should().BeLessThan(50,
                $"Expression validation took {avgMicroseconds:F2} μs on average, exceeding 50 μs threshold");
        }
    }

    [Fact]
    public void ExpressionEvaluation_LargeVariableSet_MaintainPerformance()
    {
        // Arrange - Simulate large variable context like comprehensive loan application
        var largeVariableSet = new Dictionary<string, object>();

        // Create 100 variables
        for (int i = 0; i < 100; i++)
        {
            largeVariableSet[$"var_{i}"] = i % 10 == 0 ? $"string_value_{i}" : i * 1.5;
        }

        // Add some specific variables for our expression
        largeVariableSet["target_amount"] = 50000;
        largeVariableSet["approval_threshold"] = 45000;
        largeVariableSet["risk_category"] = "medium";

        var expression = "target_amount > approval_threshold && risk_category == 'medium'";
        const int iterations = 1000;

        // Warm up
        _evaluator.EvaluateExpression(expression, largeVariableSet);

        // Measure
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _evaluator.EvaluateExpression(expression, largeVariableSet);
        }
        stopwatch.Stop();

        var avgMicroseconds = (stopwatch.ElapsedTicks * 1000000.0) / Stopwatch.Frequency / iterations;

        // Assert - Large variable sets should not significantly impact performance
        avgMicroseconds.Should().BeLessThan(200,
            $"Expression with large variable set took {avgMicroseconds:F2} μs on average, exceeding 200 μs threshold");
    }

    [Fact]
    public void ExpressionCaching_HitRate_IsEffective()
    {
        // Arrange
        var expressions = new[]
        {
            "status == 'active'",
            "amount > 1000",
            "priority == 'high'"
        };

        var variables = new Dictionary<string, object>
        {
            ["status"] = "active",
            ["amount"] = 1500,
            ["priority"] = "high"
        };

        const int iterationsPerExpression = 100;

        // Act - Evaluate each expression multiple times to test caching
        var totalStopwatch = Stopwatch.StartNew();
        var firstRunTimes = new List<long>();
        var cachedRunTimes = new List<long>();

        foreach (var expression in expressions)
        {
            // First run (compilation)
            var firstRunStopwatch = Stopwatch.StartNew();
            _evaluator.EvaluateExpression(expression, variables);
            firstRunStopwatch.Stop();
            firstRunTimes.Add(firstRunStopwatch.ElapsedTicks);

            // Subsequent runs (should use cache)
            var cachedRunStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterationsPerExpression; i++)
            {
                _evaluator.EvaluateExpression(expression, variables);
            }
            cachedRunStopwatch.Stop();
            cachedRunTimes.Add(cachedRunStopwatch.ElapsedTicks / iterationsPerExpression);
        }

        totalStopwatch.Stop();

        // Assert - Cached runs should generally be faster than or equal to first runs
        for (int i = 0; i < expressions.Length; i++)
        {
            var firstTime = firstRunTimes[i];
            var avgCachedTime = cachedRunTimes[i];

            // Allow some variance, but cached should generally be faster or similar
            avgCachedTime.Should().BeLessThanOrEqualTo(firstTime * 2,
                $"Cached evaluation for '{expressions[i]}' was significantly slower than first run");
        }
    }
}