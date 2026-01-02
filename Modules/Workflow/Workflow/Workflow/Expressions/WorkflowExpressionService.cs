using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Workflow.Workflow.Expressions;

/// <summary>
/// Production-ready workflow expression service using Microsoft.CodeAnalysis.CSharp.Scripting
/// </summary>
public class WorkflowExpressionService : IWorkflowExpressionService
{
    private readonly ILogger<WorkflowExpressionService> _logger;
    private readonly IMemoryCache _cache;
    
    // Cache for compiled scripts
    private readonly ConcurrentDictionary<string, Script> _scriptCache = new();
    
    // Script options with common assemblies and namespaces
    private readonly ScriptOptions _scriptOptions;

    public WorkflowExpressionService(
        ILogger<WorkflowExpressionService> logger,
        IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;

        // Configure script options with common assemblies and imports
        _scriptOptions = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(System.Linq.Enumerable).Assembly,
                typeof(System.Collections.Generic.List<>).Assembly,
                typeof(DateTime).Assembly,
                typeof(Guid).Assembly,
                typeof(System.Text.RegularExpressions.Regex).Assembly,
                typeof(System.Math).Assembly
            )
            .WithImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "System.Text",
                "System.Text.RegularExpressions",
                "System.Math"
            );
    }

    public async Task<ExpressionResult<T>> EvaluateAsync<T>(string expression, ExpressionContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Evaluating expression: {Expression}", expression);

            // Create script globals from context
            var globals = CreateScriptGlobals(context);
            
            // Get or create cached script
            var script = await GetOrCreateScriptAsync<T>(expression, cancellationToken);
            
            // Execute the script
            var scriptState = await script.RunAsync(globals, cancellationToken);
            var result = (T?)scriptState.ReturnValue;

            stopwatch.Stop();

            _logger.LogDebug("Expression evaluation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return ExpressionResult<T>.Success(result!, stopwatch.Elapsed);
        }
        catch (CompilationErrorException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Expression compilation failed: {Expression}", expression);
            return ExpressionResult<T>.Failure($"Compilation error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Expression evaluation failed: {Expression}", expression);
            return ExpressionResult<T>.Failure($"Evaluation error: {ex.Message}", ex);
        }
    }

    public async Task<bool> EvaluateBooleanAsync(string expression, ExpressionContext context, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync<bool>(expression, context, cancellationToken);
        
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Boolean expression evaluation failed: {Expression}, Error: {Error}", expression, result.ErrorMessage);
            return false;
        }

        return result.Value;
    }

    public async Task<bool> ValidateExpressionAsync(string expression, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating expression syntax: {Expression}", expression);

            // Create a dummy script to validate syntax
            var script = CSharpScript.Create(expression, _scriptOptions);
            var compilation = script.GetCompilation();
            var diagnostics = compilation.GetDiagnostics();

            var errors = diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
            
            if (errors.Any())
            {
                _logger.LogDebug("Expression validation failed with {ErrorCount} errors", errors.Count);
                return false;
            }

            _logger.LogDebug("Expression validation successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expression validation error: {Expression}", expression);
            return false;
        }
    }

    public async Task<ExpressionMetadata> ParseExpressionAsync(string expression, CancellationToken cancellationToken = default)
    {
        var metadata = new ExpressionMetadata
        {
            ExpressionText = expression,
            IsValid = await ValidateExpressionAsync(expression, cancellationToken)
        };

        try
        {
            // Basic analysis - in production, you'd use Roslyn syntax analysis
            metadata.ReferencedVariables = ExtractVariableReferences(expression);
            metadata.ReferencedFunctions = ExtractFunctionReferences(expression);
            metadata.ComplexityScore = CalculateComplexityScore(expression);
            metadata.IsDeterministic = IsDeterministicExpression(expression);
            metadata.HasSideEffects = HasSideEffects(expression);
            metadata.SecurityLevel = AssessSecurityLevel(expression);
            metadata.PerformanceCategory = EstimatePerformance(metadata.ComplexityScore);

            _logger.LogDebug("Expression metadata parsed for: {Expression}", expression);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse expression metadata: {Expression}", expression);
        }

        return metadata;
    }

    public async Task<WorkflowExpression<T>> CompileAsync<T>(string expression, CancellationToken cancellationToken = default)
    {
        var workflowExpression = new WorkflowExpression<T>
        {
            ExpressionText = expression,
            CompiledAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogDebug("Compiling expression: {Expression}", expression);

            // Parse metadata first
            var metadata = await ParseExpressionAsync(expression, cancellationToken);
            workflowExpression.RequiredVariables = metadata.ReferencedVariables;
            workflowExpression.UsedFunctions = metadata.ReferencedFunctions;
            workflowExpression.ComplexityScore = metadata.ComplexityScore;

            // Compile the script
            var script = await GetOrCreateScriptAsync<T>(expression, cancellationToken);
            
            // Create a delegate wrapper
            workflowExpression.CompiledDelegate = (context) =>
            {
                var globals = CreateScriptGlobals(context);
                var result = script.RunAsync(globals).GetAwaiter().GetResult();
                return (T?)result.ReturnValue ?? default!;
            };

            workflowExpression.IsCompiled = true;
            
            _logger.LogDebug("Expression compiled successfully: {Expression}", expression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expression compilation failed: {Expression}", expression);
            workflowExpression.CompilationError = ex.Message;
            workflowExpression.IsCompiled = false;
        }

        return workflowExpression;
    }

    public async Task<ExpressionResult<T>> EvaluateCompiledAsync<T>(WorkflowExpression<T> compiledExpression, ExpressionContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!compiledExpression.IsCompiled || compiledExpression.CompiledDelegate == null)
            {
                return ExpressionResult<T>.Failure("Expression is not compiled");
            }

            _logger.LogDebug("Evaluating compiled expression: {Expression}", compiledExpression.ExpressionText);

            var result = compiledExpression.CompiledDelegate(context);
            
            stopwatch.Stop();

            return ExpressionResult<T>.Success(result, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Compiled expression evaluation failed: {Expression}", compiledExpression.ExpressionText);
            return ExpressionResult<T>.Failure($"Evaluation error: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<string>> GetAvailableFunctionsAsync(CancellationToken cancellationToken = default)
    {
        // Return list of available functions
        return new[]
        {
            "Now()", "Today()", "NewGuid()", "Random()",
            "Math.Abs()", "Math.Max()", "Math.Min()", "Math.Round()",
            "String.IsNullOrEmpty()", "String.Join()", "String.Split()",
            "Regex.IsMatch()", "Regex.Replace()"
        };
    }

    #region Private Helper Methods

    private async Task<Script<T>> GetOrCreateScriptAsync<T>(string expression, CancellationToken cancellationToken)
    {
        var cacheKey = $"script_{typeof(T).Name}_{expression.GetHashCode()}";
        
        if (_scriptCache.TryGetValue(cacheKey, out var cachedScript))
        {
            return (Script<T>)cachedScript;
        }

        var script = CSharpScript.Create<T>(expression, _scriptOptions, typeof(ScriptGlobals));
        
        // Cache the script (with eviction policy)
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            Priority = CacheItemPriority.Normal
        };
        
        _scriptCache.TryAdd(cacheKey, script);
        _cache.Set(cacheKey, script, cacheOptions);

        return script;
    }

    private static ScriptGlobals CreateScriptGlobals(ExpressionContext context)
    {
        return new ScriptGlobals
        {
            Variables = context.Variables,
            WorkflowInstanceId = context.WorkflowInstanceId,
            CurrentActivityId = context.CurrentActivityId,
            CurrentUser = context.CurrentUser,
            ExecutionTime = context.ExecutionTime,
            Metadata = context.Metadata,
            Now = DateTime.UtcNow,
            Today = DateTime.UtcNow.Date,
            NewGuid = Guid.NewGuid(),
            Random = new Random().NextDouble()
        };
    }

    private static List<string> ExtractVariableReferences(string expression)
    {
        // Simplified variable extraction - in production, use Roslyn syntax tree analysis
        var variables = new HashSet<string>();
        
        // Basic pattern matching for variable references
        var words = expression.Split(' ', '.', '(', ')', '+', '-', '*', '/', '=', '!', '<', '>', '&', '|', ',', ';')
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Where(w => char.IsLetter(w[0]) || w[0] == '_')
            .ToList();

        foreach (var word in words)
        {
            if (IsVariableReference(word))
            {
                variables.Add(word);
            }
        }

        return variables.ToList();
    }

    private static List<string> ExtractFunctionReferences(string expression)
    {
        // Simplified function extraction
        var functions = new HashSet<string>();
        var words = expression.Split(' ', '+', '-', '*', '/', '=', '!', '<', '>', '&', '|', ',', ';');
        
        foreach (var word in words)
        {
            if (word.Contains('(') && !word.StartsWith('('))
            {
                var functionName = word.Split('(')[0].Trim();
                if (!string.IsNullOrEmpty(functionName))
                {
                    functions.Add(functionName);
                }
            }
        }

        return functions.ToList();
    }

    private static bool IsVariableReference(string word)
    {
        // Check if word is likely a variable (not a keyword or literal)
        var keywords = new HashSet<string> { "true", "false", "null", "new", "var", "if", "else", "return" };
        return !keywords.Contains(word.ToLower()) && 
               !int.TryParse(word, out _) && 
               !double.TryParse(word, out _);
    }

    private static int CalculateComplexityScore(string expression)
    {
        var score = 0;
        score += expression.Count(c => c == '(') * 2; // Function calls
        score += expression.Count(c => c == '.') * 1; // Property access
        score += expression.Split("&&").Length - 1; // Logical AND
        score += expression.Split("||").Length - 1; // Logical OR
        score += expression.Length / 50; // Length complexity
        
        return Math.Max(1, score);
    }

    private static bool IsDeterministicExpression(string expression)
    {
        var nonDeterministicPatterns = new[] { "Random", "Now", "Guid", "DateTime" };
        return !nonDeterministicPatterns.Any(pattern => expression.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSideEffects(string expression)
    {
        var sideEffectPatterns = new[] { "=", "++", "--", "Add(", "Remove(", "Clear(", "Insert(" };
        return sideEffectPatterns.Any(pattern => expression.Contains(pattern));
    }

    private static ExpressionSecurityLevel AssessSecurityLevel(string expression)
    {
        if (expression.Contains("System.IO") || expression.Contains("File") || expression.Contains("Directory"))
            return ExpressionSecurityLevel.High;
        
        if (expression.Contains("Process") || expression.Contains("Assembly") || expression.Contains("Reflection"))
            return ExpressionSecurityLevel.Restricted;
            
        if (expression.Contains("HttpClient") || expression.Contains("WebRequest"))
            return ExpressionSecurityLevel.Medium;
            
        return ExpressionSecurityLevel.Safe;
    }

    private static ExpressionPerformanceCategory EstimatePerformance(int complexityScore)
    {
        return complexityScore switch
        {
            <= 5 => ExpressionPerformanceCategory.Fast,
            <= 15 => ExpressionPerformanceCategory.Medium,
            <= 30 => ExpressionPerformanceCategory.Slow,
            _ => ExpressionPerformanceCategory.VerySlow
        };
    }

    #endregion
}

/// <summary>
/// Global variables and functions available in script context
/// </summary>
public class ScriptGlobals
{
    public Dictionary<string, object> Variables { get; set; } = new();
    public Guid WorkflowInstanceId { get; set; }
    public string? CurrentActivityId { get; set; }
    public string? CurrentUser { get; set; }
    public DateTime ExecutionTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // Helper properties
    public DateTime Now { get; set; }
    public DateTime Today { get; set; }
    public Guid NewGuid { get; set; }
    public double Random { get; set; }
}