using System.Collections.Concurrent;
using Assignment.Workflow.Config;

namespace Assignment.Workflow.Engine.Expression;

public class ExpressionEvaluator : IExpressionEvaluator
{
    // Use LRU cache with size limit to prevent memory leaks
    private readonly LRUCache<string, ExpressionNode> _compiledExpressions = new(maxSize: WorkflowEngineConstants.ExpressionCacheSize);
    
    // Whitelist of allowed operators for security
    private static readonly HashSet<string> AllowedOperators = new()
    {
        "==", "!=", ">", ">=", "<", "<=", "&&", "||", "!", "contains", "+", "-", "*", "/", "%"
    };
    
    // Maximum expression complexity to prevent DoS - values moved to WorkflowEngineConstants
    private static readonly int MaxExpressionLength = WorkflowEngineConstants.MaxExpressionLength;
    private static readonly int MaxTokenCount = WorkflowEngineConstants.MaxExpressionTokenCount;
    private static readonly int MaxDepth = WorkflowEngineConstants.MaxExpressionDepth;

    public bool EvaluateExpression(string expression, Dictionary<string, object> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        try
        {
            var expressionNode = GetOrCompileExpression(expression);
            
            // Use cancellation token to prevent long-running expressions
            using var cts = new CancellationTokenSource(WorkflowEngineConstants.ExpressionEvaluationTimeout);
            var result = EvaluateWithTimeout(expressionNode, variables, cts.Token);
            
            return IsTruthy(result);
        }
        catch (OperationCanceledException)
        {
            throw new ExpressionEvaluationException($"Expression evaluation timed out: '{expression}'");
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException($"Error evaluating expression '{expression}': {ex.Message}", ex);
        }
    }

    public T EvaluateExpression<T>(string expression, Dictionary<string, object> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return default!;

        try
        {
            var expressionNode = GetOrCompileExpression(expression);
            var result = expressionNode.Evaluate(variables);
            return ConvertResult<T>(result);
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException($"Error evaluating expression '{expression}': {ex.Message}", ex);
        }
    }

    public bool ValidateExpression(string expression, out string? errorMessage)
    {
        errorMessage = null;
        
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        try
        {
            CompileExpression(expression);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public void ClearCache()
    {
        _compiledExpressions.Clear();
    }

    private ExpressionNode GetOrCompileExpression(string expression)
    {
        if (_compiledExpressions.TryGetValue(expression, out var cachedExpression))
        {
            return cachedExpression;
        }

        var compiledExpression = CompileExpression(expression);
        _compiledExpressions[expression] = compiledExpression;
        return compiledExpression;
    }

    private ExpressionNode CompileExpression(string expression)
    {
        // Security validation
        ValidateExpressionSecurity(expression);
        
        var lexer = new TokenLexer(expression);
        var tokens = lexer.Tokenize();
        
        if (tokens.Count == 0)
        {
            throw new ExpressionParseException("Empty expression");
        }

        // Validate token count to prevent DoS
        if (tokens.Count > MaxTokenCount)
        {
            throw new ExpressionParseException($"Expression exceeds maximum token limit of {MaxTokenCount}");
        }

        // Validate operators are whitelisted
        ValidateTokenSecurity(tokens);

        var parser = new ExpressionParser(tokens);
        var expressionNode = parser.Parse();
        
        // Validate expression depth
        ValidateExpressionDepth(expressionNode, 0);
        
        return expressionNode;
    }

    private static void ValidateExpressionSecurity(string expression)
    {
        // Validate expression length
        if (expression.Length > MaxExpressionLength)
        {
            throw new ExpressionParseException($"Expression exceeds maximum length of {MaxExpressionLength} characters");
        }

        // Check for suspicious patterns that could indicate injection attempts
        var suspiciousPatterns = new[]
        {
            "eval", "function", "constructor", "prototype", "__proto__", "import", "require",
            "process", "global", "this", "window", "document", "script", "alert"
        };

        var lowerExpression = expression.ToLowerInvariant();
        foreach (var pattern in suspiciousPatterns)
        {
            if (lowerExpression.Contains(pattern))
            {
                throw new ExpressionParseException($"Expression contains prohibited keyword: {pattern}");
            }
        }

        // Validate parentheses are balanced
        int openParens = 0;
        foreach (char c in expression)
        {
            if (c == '(') openParens++;
            else if (c == ')') openParens--;
            
            if (openParens < 0)
            {
                throw new ExpressionParseException("Unbalanced parentheses in expression");
            }
        }
        
        if (openParens != 0)
        {
            throw new ExpressionParseException("Unbalanced parentheses in expression");
        }
    }

    private static void ValidateTokenSecurity(List<Token> tokens)
    {
        foreach (var token in tokens)
        {
            // Check if token is an operator type and validate against whitelist
            if (IsOperatorToken(token.Type) && !AllowedOperators.Contains(token.Value))
            {
                throw new ExpressionParseException($"Operator '{token.Value}' is not allowed");
            }
        }
    }

    private static bool IsOperatorToken(TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Equal => true,
            TokenType.NotEqual => true,
            TokenType.GreaterThan => true,
            TokenType.GreaterThanOrEqual => true,
            TokenType.LessThan => true,
            TokenType.LessThanOrEqual => true,
            TokenType.Contains => true,
            TokenType.And => true,
            TokenType.Or => true,
            TokenType.Not => true,
            _ => false
        };
    }

    private static void ValidateExpressionDepth(ExpressionNode node, int currentDepth)
    {
        if (currentDepth > MaxDepth)
        {
            throw new ExpressionParseException($"Expression exceeds maximum depth of {MaxDepth}");
        }

        // Recursively validate child nodes
        if (node is BinaryOperatorNode binaryNode)
        {
            ValidateExpressionDepth(binaryNode.Left, currentDepth + 1);
            ValidateExpressionDepth(binaryNode.Right, currentDepth + 1);
        }
        else if (node is UnaryOperatorNode unaryNode)
        {
            ValidateExpressionDepth(unaryNode.Operand, currentDepth + 1);
        }
    }

    private bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool boolValue) return boolValue;
        if (IsNumeric(value)) return Convert.ToDecimal(value) != 0;
        if (value is string strValue) return !string.IsNullOrWhiteSpace(strValue);
        return true;
    }

    private T ConvertResult<T>(object? result)
    {
        if (result == null) return default!;
        if (result is T directResult) return directResult;

        try
        {
            return (T)Convert.ChangeType(result, typeof(T));
        }
        catch
        {
            return default!;
        }
    }

    private bool IsNumeric(object value)
    {
        return value is byte || value is sbyte || value is short || value is ushort ||
               value is int || value is uint || value is long || value is ulong ||
               value is float || value is double || value is decimal;
    }

    private static object? EvaluateWithTimeout(ExpressionNode expressionNode, Dictionary<string, object> variables, CancellationToken cancellationToken)
    {
        // Simple timeout implementation - in production, you might want more sophisticated monitoring
        cancellationToken.ThrowIfCancellationRequested();
        
        // For more complex expressions, you'd need to pass the cancellation token through the evaluation tree
        return expressionNode.Evaluate(variables);
    }
}

public class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string message) : base(message) { }
    public ExpressionEvaluationException(string message, Exception innerException) : base(message, innerException) { }
}