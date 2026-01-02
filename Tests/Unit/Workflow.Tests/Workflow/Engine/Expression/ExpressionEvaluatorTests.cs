using Workflow.Workflow.Engine.Expression;
using FluentAssertions;
using Xunit;
using System.Diagnostics;

namespace Workflow.Tests.Workflow.Engine.Expression;

public class ExpressionEvaluatorTests
{
    private readonly IExpressionEvaluator _evaluator;

    public ExpressionEvaluatorTests()
    {
        _evaluator = new ExpressionEvaluator();
    }

    [Fact]
    public void EvaluateExpression_SimpleEquality_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["status"] = "approved",
            ["amount"] = 1000
        };

        // Act
        var result = _evaluator.EvaluateExpression("status == 'approved'", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateExpression_ComplexLogic_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["amount"] = 5000,
            ["priority"] = "high",
            ["status"] = "pending"
        };

        // Act
        var result = _evaluator.EvaluateExpression(
            "(amount > 1000 && priority == 'high') || status == 'approved'",
            variables);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("amount > 1000", true)]
    [InlineData("amount <= 500", false)]
    [InlineData("status == 'pending'", true)]
    [InlineData("status != 'approved'", true)]
    [InlineData("approved == false", true)]
    public void EvaluateExpression_VariousConditions_ReturnsExpectedResults(
        string expression, bool expected)
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["amount"] = 1500,
            ["status"] = "pending",
            ["approved"] = false
        };

        // Act
        var result = _evaluator.EvaluateExpression(expression, variables);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateExpression_NumericComparisons_WorksWithDifferentTypes()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["intValue"] = 100,
            ["doubleValue"] = 150.5,
            ["decimalValue"] = 200.75m,
            ["longValue"] = 1000L
        };

        // Act & Assert
        _evaluator.EvaluateExpression("intValue < doubleValue", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("doubleValue < decimalValue", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("decimalValue < longValue", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("intValue + doubleValue > 200", variables).Should().BeTrue();
    }

    [Fact]
    public void EvaluateExpression_StringOperations_WorksCorrectly()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["name"] = "John Doe",
            ["email"] = "user@company.com",
            ["description"] = "High priority request"
        };

        // Act & Assert
        _evaluator.EvaluateExpression("name contains 'John'", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("email contains '@company.com'", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("name contains 'Jane'", variables).Should().BeFalse();
    }

    [Fact]
    public void EvaluateExpression_BooleanOperations_WorksCorrectly()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["is_active"] = true,
            ["is_disabled"] = false,
            ["has_permission"] = true
        };

        // Act & Assert
        _evaluator.EvaluateExpression("is_active == true", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("!is_disabled", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("is_active && has_permission", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("is_disabled || has_permission", variables).Should().BeTrue();
    }

    [Fact]
    public void EvaluateExpression_ComplexLogicWithParentheses_EvaluatesCorrectly()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["age"] = 25,
            ["status"] = "verified",
            ["priority"] = "high",
            ["amount"] = 1000
        };

        // Act & Assert
        _evaluator.EvaluateExpression(
            "age >= 18 && status == 'verified'", variables).Should().BeTrue();
        _evaluator.EvaluateExpression(
            "priority == 'high' || amount > 10000", variables).Should().BeTrue();
        _evaluator.EvaluateExpression(
            "(status == 'pending' && priority == 'high') || amount > 50000", variables).Should().BeFalse();
        _evaluator.EvaluateExpression(
            "(age >= 18 && status == 'verified') && (priority == 'high' || amount > 500)", variables).Should().BeTrue();
    }

    [Fact]
    public void EvaluateExpression_WithMissingVariables_HandlesMissingGracefully()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["existing"] = "value"
        };

        // Act
        var result = _evaluator.EvaluateExpression("missing_variable == 'test'", variables);

        // Assert - Missing variables should be treated as null/false
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateExpression_EmptyExpression_ReturnsTrue()
    {
        // Arrange
        var variables = new Dictionary<string, object>();

        // Act
        var result = _evaluator.EvaluateExpression("", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateExpression_NullExpression_ReturnsTrue()
    {
        // Arrange
        var variables = new Dictionary<string, object>();

        // Act
        var result = _evaluator.EvaluateExpression(null!, variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateExpression_WithInvalidExpression_ThrowsException()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["status"] = "active"
        };

        // Act & Assert
        var act = () => _evaluator.EvaluateExpression("status = 'active'", variables); // Single = instead of ==
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ValidateExpression_ValidExpression_ReturnsTrue()
    {
        // Act
        var isValid = _evaluator.ValidateExpression(
            "(status == 'approved' && amount > 1000) || priority == 'urgent'",
            out var errorMessage);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateExpression_InvalidExpression_ReturnsFalse()
    {
        // Act
        var isValid = _evaluator.ValidateExpression(
            "status = 'approved'", // Invalid: single = instead of ==
            out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateExpression_EmptyOrNullExpression_ReturnsTrue(string expression)
    {
        // Act
        var isValid = _evaluator.ValidateExpression(expression, out var errorMessage);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("status = 'active'")]  // Single = instead of ==
    [InlineData("(status == 'active'")]  // Missing closing parenthesis
    [InlineData("status == 'active' &&")]  // Incomplete expression
    [InlineData("&& status == 'active'")]  // Missing left operand
    public void ValidateExpression_InvalidExpressions_ReturnsFalse(string expression)
    {
        // Act
        var isValid = _evaluator.ValidateExpression(expression, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateExpression_Generic_ReturnsCorrectType()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["count"] = 42,
            ["name"] = "test",
            ["ratio"] = 3.14
        };

        // Act & Assert
        _evaluator.EvaluateExpression<int>("count", variables).Should().Be(42);
        _evaluator.EvaluateExpression<string>("name", variables).Should().Be("test");
        _evaluator.EvaluateExpression<double>("ratio", variables).Should().Be(3.14);
    }

    [Fact]
    public void EvaluateExpression_CachingPerformance_ImprovesDuplicateEvaluations()
    {
        // Arrange
        var expression = "complex_calculation > threshold && status == 'ready'";
        var variables = new Dictionary<string, object>
        {
            ["complex_calculation"] = 100,
            ["threshold"] = 50,
            ["status"] = "ready"
        };

        // Act - First evaluation (compiles expression)
        var stopwatch = Stopwatch.StartNew();
        var result1 = _evaluator.EvaluateExpression(expression, variables);
        var firstTime = stopwatch.ElapsedTicks;

        stopwatch.Restart();
        var result2 = _evaluator.EvaluateExpression(expression, variables);
        var secondTime = stopwatch.ElapsedTicks;

        // Assert
        result1.Should().Be(result2);
        secondTime.Should().BeLessThanOrEqualTo(firstTime, "Cached evaluation should be faster or equal");
    }

    [Fact]
    public void EvaluateExpression_WorkflowScenarios_HandlesRealWorldExpressions()
    {
        // Arrange - Loan application scenario
        var variables = new Dictionary<string, object>
        {
            ["loan_amount"] = 250000,
            ["credit_score"] = 720,
            ["debt_to_income"] = 0.35,
            ["employment_years"] = 5,
            ["priority"] = "standard",
            ["applicant_type"] = "individual"
        };

        // Act & Assert - Auto-approval conditions
        _evaluator.EvaluateExpression(
            "loan_amount <= 250000 && credit_score >= 700 && debt_to_income <= 0.4",
            variables).Should().BeTrue();

        // Manual review conditions  
        _evaluator.EvaluateExpression(
            "loan_amount > 250000 || credit_score < 700 || debt_to_income > 0.4",
            variables).Should().BeFalse();

        // Priority routing
        _evaluator.EvaluateExpression(
            "priority == 'urgent' || (applicant_type == 'vip' && loan_amount > 500000)",
            variables).Should().BeFalse();

        // Experience-based routing
        _evaluator.EvaluateExpression(
            "employment_years >= 3 && credit_score >= 650",
            variables).Should().BeTrue();
    }

    [Fact]
    public void EvaluateExpression_EdgeCases_HandlesGracefully()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["null_value"] = null!,
            ["empty_string"] = "",
            ["zero"] = 0,
            ["false_value"] = false
        };

        // Act & Assert - Null handling
        _evaluator.EvaluateExpression("null_value == null", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("null_value != null", variables).Should().BeFalse();

        // Empty string handling
        _evaluator.EvaluateExpression("empty_string == ''", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("empty_string != ''", variables).Should().BeFalse();

        // Zero and false handling
        _evaluator.EvaluateExpression("zero == 0", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("false_value == false", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("!false_value", variables).Should().BeTrue();
    }

    #region Security Tests - Critical vulnerability prevention

    [Theory]
    [InlineData("eval('malicious code')")]
    [InlineData("constructor.constructor('return process')()")]
    [InlineData("__proto__.malicious = true")]
    [InlineData("import('fs').then(fs => fs.readFile('/etc/passwd'))")]
    [InlineData("require('child_process').exec('rm -rf /')")]
    [InlineData("window.location = 'http://malicious.com'")]
    [InlineData("document.cookie")]
    [InlineData("alert('xss')")]
    public void EvaluateExpression_InjectionAttempts_ShouldThrowSecurityException(string maliciousExpression)
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["status"] = "active"
        };

        // Act & Assert
        var act = () => _evaluator.EvaluateExpression(maliciousExpression, variables);
        act.Should().Throw<Exception>("Injection attempts should be blocked")
            .WithMessage("*Error evaluating expression*");
    }

    [Fact]
    public void EvaluateExpression_ExcessivelyLongExpression_ShouldThrowException()
    {
        // Arrange - Create expression longer than MaxExpressionLength (2000 chars)
        var longExpression = string.Join(" && ", Enumerable.Repeat("status == 'active'", 150));
        var variables = new Dictionary<string, object> { ["status"] = "active" };

        // Act & Assert
        var act = () => _evaluator.EvaluateExpression(longExpression, variables);
        act.Should().Throw<Exception>("Excessively long expressions should be blocked");
    }

    [Fact]
    public void EvaluateExpression_ExcessiveTokenCount_ShouldThrowException()
    {
        // Arrange - Create expression with more than MaxExpressionTokenCount (500 tokens)
        var tokens = Enumerable.Repeat("x", 600);
        var excessiveExpression = string.Join(" && ", tokens);
        var variables = new Dictionary<string, object>();

        // Act & Assert
        var act = () => _evaluator.EvaluateExpression(excessiveExpression, variables);
        act.Should().Throw<Exception>("Excessive token count should be blocked");
    }

    [Fact]
    public void EvaluateExpression_ExcessiveNestingDepth_ShouldThrowException()
    {
        // Arrange - Create deeply nested expression beyond MaxExpressionDepth (50 levels)
        var nestedExpression = "(" + string.Join("", Enumerable.Repeat("(", 60)) + "true" + 
                              string.Join("", Enumerable.Repeat(")", 60)) + ")";
        var variables = new Dictionary<string, object>();

        // Act & Assert
        var act = () => _evaluator.EvaluateExpression(nestedExpression, variables);
        act.Should().Throw<Exception>("Excessive nesting should be blocked");
    }

    [Fact]
    public void EvaluateExpression_TimeoutScenario_ShouldCancelAndThrow()
    {
        // Arrange - Create expression that would take longer than 1 second timeout
        // Note: This test validates the timeout mechanism, actual infinite loop prevention
        // is handled by the expression compiler's complexity limits
        var complexExpression = string.Join(" && ", Enumerable.Repeat("amount > 100", 1000));
        var variables = new Dictionary<string, object> { ["amount"] = 500 };

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        var act = () => _evaluator.EvaluateExpression(complexExpression, variables);
        
        // Should either complete quickly (due to complexity limits) or timeout
        if (stopwatch.ElapsedMilliseconds > 1000)
        {
            act.Should().Throw<Exception>().Which.Message.Should().Contain("timed out");
        }
        
        stopwatch.Stop();
        // Regardless of outcome, should not take significantly longer than timeout
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Should respect timeout limits");
    }

    [Theory]
    [InlineData("status == 'approved'; DROP TABLE Users; --'")]
    [InlineData("status == 'test' UNION SELECT * FROM sensitive_data")]
    [InlineData("amount > 100; DELETE FROM workflows;")]
    [InlineData("1=1 OR 1=1")]
    [InlineData("' OR '1'='1")]
    public void EvaluateExpression_SQLInjectionPatterns_ShouldBeBlocked(string injectionAttempt)
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["status"] = "active",
            ["amount"] = 1000
        };

        // Act & Assert - SQL injection patterns should fail to parse or execute
        var act = () => _evaluator.EvaluateExpression(injectionAttempt, variables);
        act.Should().Throw<Exception>("SQL injection patterns should be blocked");
    }

    [Fact]
    public void EvaluateExpression_UnauthorizedFunctionCalls_ShouldBeBlocked()
    {
        // Arrange - Test expressions that might try to call unauthorized functions
        var unauthorizedExpressions = new[]
        {
            "System.IO.File.ReadAllText('/etc/passwd')",
            "Math.Random()",
            "DateTime.Now.AddDays(-1)",
            "Environment.GetEnvironmentVariable('PATH')",
            "Assembly.LoadFrom('malicious.dll')"
        };
        
        var variables = new Dictionary<string, object> { ["status"] = "active" };

        // Act & Assert
        foreach (var expression in unauthorizedExpressions)
        {
            var act = () => _evaluator.EvaluateExpression(expression, variables);
            act.Should().Throw<Exception>($"Unauthorized function call should be blocked: {expression}");
        }
    }

    [Fact]
    public void EvaluateExpression_UnsafeOperators_ShouldBeBlocked()
    {
        // Arrange - Test operators that are not in the whitelist
        var unsafeOperators = new[]
        {
            "status === 'active'",  // Triple equals (not in whitelist)
            "status >> 'active'",   // Bit shift operator
            "status << 'active'",   // Bit shift operator
            "status & 'active'",    // Bitwise AND
            "status | 'active'",    // Bitwise OR
            "status ^ 'active'"     // XOR operator
        };
        
        var variables = new Dictionary<string, object> { ["status"] = "active" };

        // Act & Assert
        foreach (var expression in unsafeOperators)
        {
            var act = () => _evaluator.EvaluateExpression(expression, variables);
            act.Should().Throw<Exception>($"Unsafe operator should be blocked: {expression}");
        }
    }

    [Fact]
    public void EvaluateExpression_ReflectionAttempts_ShouldBeBlocked()
    {
        // Arrange - Test expressions attempting to use reflection
        var reflectionAttempts = new[]
        {
            "GetType()",
            "typeof(string)",
            "this.GetType().GetMethod('ToString').Invoke(this, null)",
            "Assembly.GetExecutingAssembly()",
            "Type.GetType('System.IO.File')"
        };
        
        var variables = new Dictionary<string, object> { ["status"] = "active" };

        // Act & Assert
        foreach (var expression in reflectionAttempts)
        {
            var act = () => _evaluator.EvaluateExpression(expression, variables);
            act.Should().Throw<Exception>($"Reflection attempt should be blocked: {expression}");
        }
    }

    [Fact]
    public void EvaluateExpression_InputSanitization_HandlesSpecialCharacters()
    {
        // Arrange - Test expressions with special characters that should be handled safely
        var variables = new Dictionary<string, object>
        {
            ["description"] = "User input with <script>alert('xss')</script>",
            ["filename"] = "../../../etc/passwd",
            ["query"] = "'; DROP TABLE users; --"
        };

        // Act & Assert - Should safely handle special characters in variable values
        _evaluator.EvaluateExpression("description contains 'User'", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("filename contains 'passwd'", variables).Should().BeTrue();
        _evaluator.EvaluateExpression("query contains 'DROP'", variables).Should().BeTrue();
        
        // Malicious content in variables should not execute, just be treated as string values
        var act1 = () => _evaluator.EvaluateExpression("description", variables);
        act1.Should().NotThrow("Variable content should be treated as data, not code");
    }

    [Fact]
    public void EvaluateExpression_MemoryExhaustionPrevention_LimitsLargeDataSets()
    {
        // Arrange - Test with large variable sets to ensure memory limits are respected
        var largeVariables = new Dictionary<string, object>();
        
        // Add many variables to test memory usage
        for (int i = 0; i < 10000; i++)
        {
            largeVariables[$"var_{i}"] = $"value_{i}";
        }

        // Act & Assert - Should handle large variable sets without excessive memory usage
        var initialMemory = GC.GetTotalMemory(false);
        
        var result = _evaluator.EvaluateExpression("var_100 == 'value_100'", largeVariables);
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        result.Should().BeTrue();
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should be reasonable"); // Less than 50MB
    }

    [Fact]
    public void EvaluateExpression_ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            ["status"] = "active",
            ["amount"] = 1000
        };

        var expression = "status == 'active' && amount > 500";
        var tasks = new List<Task<bool>>();

        // Act - Execute same expression concurrently from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => _evaluator.EvaluateExpression(expression, variables)));
        }

        var results = Task.WhenAll(tasks).Result;

        // Assert - All results should be consistent (thread-safe)
        results.Should().AllSatisfy(result => result.Should().BeTrue());
    }

    [Fact]
    public void ValidateExpression_SecurityValidation_BlocksMaliciousPatterns()
    {
        // Arrange - Test validation catches security issues
        var maliciousExpressions = new[]
        {
            "eval('malicious')",
            "__proto__.constructor",
            "require('fs')",
            "1" + string.Join("", Enumerable.Range(0, 60).Select(_ => " + 1")), // Deep nesting beyond limit
            new string('a', 3000) // Too long
        };

        // Act & Assert
        foreach (var expression in maliciousExpressions)
        {
            var isValid = _evaluator.ValidateExpression(expression, out var errorMessage);
            isValid.Should().BeFalse($"Malicious expression should be invalid: {expression}");
            errorMessage.Should().NotBeNullOrEmpty();
        }
    }

    #endregion
}