using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using FaultResponseSystem.Models;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace FaultResponseSystem.Agents;

public abstract class BaseAgent<TResult> : IAgent where TResult : AgentResultBase, new()
{
    public abstract string Name { get; }
    protected readonly AzureOpenAIClient _client;
    protected readonly ChatClient _chatClient;
    protected readonly string _deploymentName;

    // Allows derived agents to register their tools
    protected List<ChatTool> _tools = new();

    /// <summary>
    /// Global semaphore — limits concurrent Azure OpenAI calls to 1 to avoid rate-limiting
    /// on the GitHub Models free tier (15 RPM). All agents share this lock.
    /// </summary>
    private static readonly SemaphoreSlim _apiSemaphore = new(1, 1);

    protected BaseAgent(IConfiguration config)
    {
        var endpoint = config["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is missing");
        var apiKey = config["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is missing");
        _deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini";

        // In production, Azure.Identity DefaultAzureCredential is preferred. 
        // For portfolio/demo, using ApiKey for ease of setup.
        _client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = _client.GetChatClient(_deploymentName);
    }

    public async Task<AgentResultBase> ExecuteAsync(Dictionary<string, object> context, CancellationToken cancellationToken = default)
    {
        var result = new TResult 
        { 
            AgentName = Name, 
            StartedAt = DateTime.UtcNow 
        };

        try
        {
            var systemPrompt = GetSystemPrompt();
            var userPrompt = GetUserPrompt(context);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.2f // Low temp for more deterministic reasoning
            };

            if (_tools.Any())
            {
                foreach(var tool in _tools)
                {
                    options.Tools.Add(tool);
                }
            }

            bool requiresAction = true;
            ChatCompletion? completion = null;
            int MaxToolLoop = 5;
            int toolLoopCount = 0;

            while (requiresAction && toolLoopCount < MaxToolLoop)
            {
                toolLoopCount++;

                // Acquire the global rate-limit semaphore, then run with a 60s per-call timeout
                await _apiSemaphore.WaitAsync(cancellationToken);
                using var callCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                callCts.CancelAfter(TimeSpan.FromSeconds(60));
                try
                {
                    completion = await _chatClient.CompleteChatAsync(messages, options, callCts.Token);
                }
                finally
                {
                    _apiSemaphore.Release();
                }
                
                // Track tokens
                if (completion.Usage != null)
                {
                    result.TokensUsed += completion.Usage.TotalTokenCount;
                }

                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    messages.Add(new AssistantChatMessage(completion));
                    
                    foreach (var toolCall in completion.ToolCalls)
                    {
                        var toolResult = await ExecuteToolAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    }
                }
                else
                {
                    requiresAction = false;
                }
            }

            result.RawLlmOutput = completion?.Content[0].Text ?? "";
            
            // Extract JSON from output (assuming agent outputs markdown json block or raw json)
            var json = ExtractJson(result.RawLlmOutput);
            
            // Populate strongly typed result
            var optionsSer = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsedResult = JsonSerializer.Deserialize<TResult>(json, optionsSer);
            
            if (parsedResult != null)
            {
                // Only copy domain-specific properties — skip AgentResultBase metadata fields
                // (StartedAt, CompletedAt, etc.) which the LLM JSON doesn't populate and would
                // overwrite our correctly-set DateTime values with DateTime.MinValue.
                var baseProps = typeof(AgentResultBase).GetProperties().Select(p => p.Name).ToHashSet();
                foreach(var prop in typeof(TResult).GetProperties()
                    .Where(p => p.CanWrite && !baseProps.Contains(p.Name)))
                {
                    var val = prop.GetValue(parsedResult);
                    if (val != null) prop.SetValue(result, val);
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    protected abstract string GetSystemPrompt();
    protected abstract string GetUserPrompt(Dictionary<string, object> context);
    
    // Optional hook for derived agents to implement tool execution
    protected virtual Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        return Task.FromResult("{}");
    }

    private string ExtractJson(string response)
    {
        var start = response.IndexOf("```json");
        if (start >= 0)
        {
            start += 7;
            var end = response.IndexOf("```", start);
            if (end > start)
            {
                return response.Substring(start, end - start).Trim();
            }
        }
        return response.Trim();
    }
}
