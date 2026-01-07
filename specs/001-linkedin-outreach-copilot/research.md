# Research Report: LinkedIn Outreach Copilot Agent
**Feature**: LinkedIn Outreach Copilot Agent  
**Date**: 2026-01-07  
**Status**: Phase 0 Research Complete

## Executive Summary

This research document consolidates technical decisions and best practices for implementing the LinkedIn Outreach Copilot Agent using .NET 10 MAUI Blazor Hybrid architecture. All NEEDS CLARIFICATION items from the Technical Context have been resolved through official documentation and current best practices (2025-2026).

---

## 1. .NET MAUI Blazor Hybrid Patterns

### Decision: Use BlazorWebView with Native Host Pattern

**Rationale**:
- **BlazorWebView Control**: .NET MAUI's `BlazorWebView` hosts Blazor components natively without internet requirements, rendering through embedded WebView2 (Windows), WKWebView (iOS/macOS), or Chromium WebView (Android)
- **Desktop-First Optimization**: Blazor components run in .NET process with full device API access, avoiding WebAssembly overhead
- **Performance**: Components load and execute code quickly with no browser-based limitations
- **Background Task Support**: Native threading model allows background operations (LinkedIn automation, database polling) independent of UI lifecycle

**Key Implementation Details**:
```csharp
// MauiProgram.cs - Configure BlazorWebView
builder.Services.AddMauiBlazorWebView();
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("Microsoft.AspNetCore.Components.WebView", LogLevel.Trace);
    logging.AddDebug(); // Debug logging for diagnostics
});
```

**Architecture Considerations**:
- **Separation of Concerns**: UI (Razor components) lives in `OutreachAgent.UI/`, business logic (Controller) in `OutreachAgent.Core/`
- **State Management**: Blazor components subscribe to Controller state changes via C# events, not conversation history
- **Lifecycle Management**: BlazorWebView `HostPage` property points to `wwwroot/index.html`, `RootComponents` specify initial render targets

**Reference**: [ASP.NET Core Blazor Hybrid](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/?view=aspnetcore-10.0), [Host a Blazor web app in a .NET MAUI app using BlazorWebView](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/blazorwebview)

**Alternatives Considered**:
- Blazor Server/WebAssembly: Rejected due to network dependency and startup latency
- WPF/WinForms BlazorWebView: Limited to Windows only, breaks constitution's desktop-first cross-platform requirement

---

## 2. Supabase PostgreSQL Integration (.NET)

### Decision: Use Official Supabase .NET Client SDK with Connection Pooling

**Rationale**:
- **Official SDK**: `Supabase` library (formerly `supabase-csharp`, renamed in 2024) provides type-safe access to Supabase REST API, Auth, and Realtime features
- **Connection Pooling**: Supabase Pooler (PgBouncer) provides transaction-mode connection pooling at `<project>.pooler.supabase.com:6543` for runtime, direct connection at port `5432` for migrations
- **Two Connection Strings**: 
  - `DATABASE_URL` (pooled): `postgres://postgres.PROJECT:[PASSWORD]@aws-0-REGION.pooler.supabase.com:6543/postgres?pgbouncer=true`
  - `DIRECT_URL` (direct): `postgres://postgres.PROJECT:[PASSWORD]@aws-0-REGION.pooler.supabase.com:5432/postgres` (for migrations/admin operations)

**Key Implementation Details**:
```csharp
// OutreachAgent.Core/Services/SupabaseService.cs
using Supabase;
using Supabase.Realtime;

public class SupabaseService
{
    private readonly Client _client;

    public SupabaseService(IConfiguration config)
    {
        var url = config["Supabase:Url"];
        var key = config["Supabase:AnonKey"];
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = true, // Enable real-time subscriptions
        };
        _client = new Client(url, key, options);
        await _client.InitializeAsync();
    }

    // Entity access with typed models
    public Task<List<Campaign>> GetActiveCampaignsAsync()
    {
        return _client.From<Campaign>()
            .Select("*")
            .Where(c => c.Status == CampaignStatus.Active)
            .Get();
    }
}
```

**Database Schema Management**:
- Use direct connection (`DIRECT_URL`) for EF Core migrations or Supabase CLI migrations
- Use pooled connection (`DATABASE_URL`) for runtime queries with `?pgbouncer=true` flag
- Connection limit: Default 50 connections/database (configure via Supabase dashboard)

**Reference**: [Supabase C# Client SDK](https://github.com/supabase-community/supabase-csharp) (renamed to "Supabase" package in 2024), [Supabase Connection Pooling](https://supabase.com/docs/guides/database/connecting-to-postgres), [Prisma with Supabase](https://www.prisma.io/docs/orm/overview/databases/supabase)

**Alternatives Considered**:
- Npgsql raw PostgreSQL client: More verbose, requires manual connection pooling logic
- Entity Framework Core with Npgsql: Adds ORM overhead; Supabase SDK preferred for REST API parity

---

## 3. Azure Key Vault + Application Insights Integration

### Decision: Use Azure.Identity with DefaultAzureCredential for Passwordless Auth

**Rationale**:
- **DefaultAzureCredential**: Automatically handles authentication chain (managed identity → Visual Studio → Azure CLI → environment variables) without hardcoded credentials
- **Managed Identity Support**: Production deployments use system-assigned or user-assigned managed identities (no secrets in code/config)
- **Unified SDK**: `Azure.Security.KeyVault.Secrets` + `Azure.Identity` provide consistent authentication across Azure services

**Key Implementation Details**:
```csharp
// OutreachAgent.Core/Services/SecretManager.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;

public class SecretManager
{
    private readonly SecretClient _client;

    public SecretManager(IConfiguration config)
    {
        var vaultUri = config["Azure:KeyVault:VaultUri"];
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            // For user-assigned managed identity (if applicable)
            ManagedIdentityClientId = config["Azure:KeyVault:ManagedIdentityClientId"]
        });

        var options = new SecretClientOptions
        {
            Retry = new RetryOptions
            {
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(16),
                MaxRetries = 5,
                Mode = RetryMode.Exponential
            }
        };

        _client = new SecretClient(new Uri(vaultUri), credential, options);
    }

    public async Task<string> GetLinkedInCredentialAsync()
    {
        KeyVaultSecret secret = await _client.GetSecretAsync("linkedin-session-cookie");
        return secret.Value;
    }
}
```

**Application Insights Integration**:
```csharp
// MauiProgram.cs - OpenTelemetry + Azure Monitor
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

builder.Services.AddOpenTelemetry()
    .UseAzureMonitor(options =>
    {
        options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource("OutreachAgent.Core"); // Custom ActivitySource
        tracing.AddHttpClientInstrumentation(); // Track HTTP calls
        tracing.AddAspNetCoreInstrumentation(); // Track ASP.NET requests
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("OutreachAgent.Metrics"); // Custom metrics
        metrics.AddHttpClientInstrumentation();
    });
```

**Distributed Tracing for MCP Tools**:
- Use `System.Diagnostics.Activity` and `ActivitySource` to create spans for each MCP tool call
- Application Insights automatically correlates traces across HTTP boundaries
- `Activity.Current.SetTag("mcp.tool", "linkedin_scrape")` adds custom properties

**Reference**: [Azure Key Vault with Managed Identity](https://learn.microsoft.com/en-us/azure/key-vault/general/tutorial-net-create-vault-azure-web-app), [OpenTelemetry with Azure Monitor](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-applicationinsights), [Application Insights Distributed Tracing](https://learn.microsoft.com/en-us/azure/azure-monitor/app/distributed-tracing)

**Alternatives Considered**:
- appsettings.json secrets: Insecure for production, no audit trail
- Windows Credential Manager: Platform-specific, requires manual key management

---

## 4. Model Context Protocol (MCP) Server Integration

### Decision: Embedded MCP Servers with Lifecycle Management

**Rationale**:
- **MCP Lifecycle Stages**: Initialize (validate executables) → Register (add to service collection) → Connect (spawn process + stdio transport) → Monitor (health checks) → Shutdown (graceful termination)
- **Security**: Sandboxed tool execution with pre/post validation, cryptographic verification of server executables (SHA256 hash check)
- **Embedded Deployment**: Bundle MCP server executables (Playwright.js, DesktopCommander.exe) in app package, auto-start on demand
- **Transport**: JSON-RPC 2.0 over stdio (stdin/stdout) for IPC, avoiding network complexity

**Key Implementation Details**:
```csharp
// OutreachAgent.MCP/McpServerManager.cs
public class McpServerManager
{
    private readonly Dictionary<string, Process> _serverProcesses = new();
    private readonly ILogger<McpServerManager> _logger;

    public async Task<string> StartServerAsync(string serverName, string executablePath, string[] args)
    {
        // Security: Verify executable hash
        if (!await VerifyExecutableHashAsync(executablePath))
            throw new SecurityException($"MCP server executable hash mismatch: {serverName}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = string.Join(" ", args),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        _serverProcesses[serverName] = process;

        // Initialize JSON-RPC client over stdio
        var client = new McpClient(process.StandardInput, process.StandardOutput);
        await client.InitializeAsync(new { clientInfo = new { name = "OutreachAgent", version = "1.0.0" } });

        _logger.LogInformation("MCP server '{ServerName}' started (PID: {Pid})", serverName, process.Id);
        return serverName;
    }

    public async Task<JsonElement> CallToolAsync(string serverName, string toolName, JsonElement args)
    {
        // Pre-validation: Check tool whitelist
        if (!IsToolAllowed(serverName, toolName))
            throw new InvalidOperationException($"Tool {toolName} not allowed for server {serverName}");

        // Invoke tool via JSON-RPC
        var client = GetClient(serverName);
        var result = await client.SendRequestAsync("tools/call", new { name = toolName, arguments = args });

        // Post-validation: Log and verify result
        await AuditLogAsync(serverName, toolName, args, result);
        return result;
    }

    private async Task<bool> VerifyExecutableHashAsync(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "");
        return hash == _expectedHashes[Path.GetFileName(path)];
    }
}
```

**MCP Server Registry (appsettings.json)**:
```json
{
  "MCP": {
    "Servers": {
      "playwright": {
        "Executable": "mcp-servers/playwright/playwright-mcp.exe",
        "Args": [],
        "ExpectedHash": "A1B2C3D4E5F6...",
        "AllowedTools": ["linkedin_login", "linkedin_search", "linkedin_scrape_profile"]
      },
      "desktop-commander": {
        "Executable": "mcp-servers/desktop-commander/desktop-commander.exe",
        "Args": ["--sandbox"],
        "ExpectedHash": "F6E5D4C3B2A1...",
        "AllowedTools": ["read_file", "write_file", "list_directory"]
      }
    }
  }
}
```

**Reference**: [MCP Lifecycle Specification](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle), [MCP Security Best Practices](https://www.networkintelligence.ai/blogs/model-context-protocol-mcp-security-checklist/), [MCP for Technical Professionals](https://securityboulevard.com/2025/11/mcp-for-technical-professionals-a-comprehensive-guide-to-understanding-and-implementing-the-model-context-protocol/)

**Alternatives Considered**:
- HTTP-based MCP servers: Added network attack surface, requires TLS certificates
- Third-party MCP server hosting: External dependency, data privacy concerns

---

## 5. Playwright.NET for LinkedIn Automation

### Decision: Adaptive Rate Limiting with Stealth Patterns

**Rationale**:
- **Anti-Detection**: Use `playwright-stealth` patterns (navigator.webdriver override, WebGL fingerprint randomization, timing jitter) to avoid LinkedIn bot detection
- **Rate Limiting Strategy**:
  - **Variable Delays**: 30-90 seconds between actions (randomized via `Random.Next()`)
  - **Daily Quota**: 50 LinkedIn connections/day enforced at Controller level
  - **Session Limits**: 2-hour maximum session duration with forced cooldown
  - **Human Simulation**: Mouse movements, scroll events, typing delays (100-300ms per keystroke)

**Key Implementation Details**:
```csharp
// OutreachAgent.MCP/PlaywrightService.cs
using Microsoft.Playwright;

public class PlaywrightService
{
    private IBrowser _browser;
    private IPage _page;
    private readonly Random _random = new();

    public async Task InitializeAsync(bool headless = true)
    {
        var playwright = await Playwright.CreateAsync();
        _browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            Args = new[] { "--disable-blink-features=AutomationControlled" } // Anti-detection
        });

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            Viewport = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "en-US",
            TimezoneId = "America/New_York"
        });

        // Inject stealth script
        await context.AddInitScriptAsync(@"
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
            window.chrome = { runtime: {} };
        ");

        _page = await context.NewPageAsync();
    }

    public async Task LinkedInSearchAsync(string keywords)
    {
        await _page.GotoAsync("https://www.linkedin.com/search/results/people/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Simulate human typing with delays
        await _page.Locator("input[aria-label='Search']").TypeAsync(keywords, new LocatorTypeOptions
        {
            Delay = _random.Next(100, 300) // 100-300ms per keystroke
        });

        await _page.Keyboard.PressAsync("Enter");

        // Adaptive delay: 30-90 seconds
        await Task.Delay(TimeSpan.FromSeconds(_random.Next(30, 90)));
    }

    public async Task SimulateHumanBehaviorAsync()
    {
        // Random mouse movements
        await _page.Mouse.MoveAsync(_random.Next(100, 800), _random.Next(100, 600));
        
        // Random scroll
        await _page.Mouse.WheelAsync(0, _random.Next(100, 500));
        
        await Task.Delay(_random.Next(1000, 3000));
    }
}
```

**Rate Limiting Enforcement (Controller)**:
```csharp
// OutreachAgent.Core/Controllers/CampaignController.cs
public class CampaignController
{
    private const int MAX_DAILY_CONNECTIONS = 50;
    private const int MAX_SESSION_DURATION_HOURS = 2;

    public async Task<TaskResult> ExecuteTaskAsync(Guid taskId)
    {
        var task = await _db.GetTaskAsync(taskId);
        
        // Check daily quota
        var todayConnectionCount = await _db.CountConnectionsAsync(DateTime.UtcNow.Date);
        if (todayConnectionCount >= MAX_DAILY_CONNECTIONS)
            return TaskResult.QuotaExceeded("Daily LinkedIn connection limit reached");

        // Check session duration
        var sessionStart = await _db.GetSessionStartTimeAsync(task.CampaignId);
        if (DateTime.UtcNow - sessionStart > TimeSpan.FromHours(MAX_SESSION_DURATION_HOURS))
        {
            await _playwright.ShutdownAsync();
            return TaskResult.SessionExpired("2-hour session limit reached, forcing cooldown");
        }

        // Execute with adaptive delay
        await _playwright.ExecuteWithDelayAsync(task);
        return TaskResult.Success();
    }
}
```

**Reference**: [Playwright Stealth Mode](https://www.scrapeless.com/en/blog/playwright-stealth), [Avoid Bot Detection with Playwright](https://brightdata.com/blog/how-tos/avoid-bot-detection-with-playwright-stealth), [Playwright .NET Documentation](https://playwright.dev/dotnet/)

**Alternatives Considered**:
- Selenium WebDriver: Less stealth features, slower execution
- Manual HTTP requests (LinkedIn API): Violates LinkedIn TOS, requires OAuth (not suitable for automation)

---

## 6. LLM Agent Pattern (Controller-Agent Architecture)

### Decision: Stateless Agent with Single-Action Responses

**Rationale**:
- **Agent as Stateless Function**: LLM receives fresh context from Controller at every cycle, proposes ONE action, Controller validates and executes
- **NO Conversation History Dependence**: Agent cannot rely on previous responses; all state is reloaded from database
- **JSON-Structured Output**: Agent must output valid JSON with `action`, `tool`, `args`, and `reason` fields
- **Validation Layer**: Controller parses JSON, checks invariants (FR-007 to FR-011), then executes via MCP

**Key Implementation Details**:
```csharp
// OutreachAgent.Agent/LlmAgentService.cs
using System.Text.Json;

public class LlmAgentService
{
    private readonly HttpClient _httpClient; // OpenAI/Azure OpenAI client

    public async Task<AgentAction> ProposeActionAsync(AgentContext context)
    {
        var prompt = $@"
You are a LinkedIn outreach agent. Based on the current task context, propose ONE action.

CURRENT TASK:
- Campaign: {context.CampaignName}
- Task ID: {context.TaskId}
- Task Type: {context.TaskType}
- Target Lead: {context.LeadName} ({context.LeadLinkedInUrl})

PREVIOUS EXECUTION RESULTS (last 3 completed tasks):
{JsonSerializer.Serialize(context.RecentTaskResults)}

DATABASE STATE:
- Leads scraped: {context.LeadsScrapedCount}
- Messages sent: {context.MessagesSentCount}
- Daily quota remaining: {context.DailyQuotaRemaining}

Available tools: linkedin_search, linkedin_scrape_profile, linkedin_send_message, send_email

OUTPUT FORMAT (JSON only, no markdown):
{{
  ""action"": ""string (tool name)"",
  ""args"": {{ /* tool-specific arguments */ }},
  ""reason"": ""string (1-2 sentence rationale)""
}}

CONSTRAINTS:
- Propose ONLY ONE action
- Check daily quota before connection/message actions
- If quota exceeded, propose ""wait_cooldown"" action
";

        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "gpt-4",
            messages = new[]
            {
                new { role = "system", content = "You are a LinkedIn automation agent. Output valid JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 500
        });

        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
        var actionJson = result.Choices[0].Message.Content;

        // Parse and validate JSON
        return JsonSerializer.Deserialize<AgentAction>(actionJson);
    }
}

public record AgentAction(string Action, JsonElement Args, string Reason);
public record AgentContext(
    Guid TaskId,
    string TaskType,
    string CampaignName,
    string LeadName,
    string LeadLinkedInUrl,
    List<TaskResult> RecentTaskResults,
    int LeadsScrapedCount,
    int MessagesSentCount,
    int DailyQuotaRemaining
);
```

**Controller Validation**:
```csharp
// OutreachAgent.Core/Controllers/AgentController.cs
public class AgentController
{
    public async Task<ExecutionResult> ExecuteCycleAsync(Guid taskId)
    {
        // FR-010: Reload state from database every cycle
        var context = await LoadAgentContextAsync(taskId);

        // Propose action via LLM
        var proposedAction = await _agent.ProposeActionAsync(context);

        // FR-011: Validate agent output (JSON structure, tool whitelist)
        if (!ValidateAgentAction(proposedAction))
            return ExecutionResult.Fail("Agent output validation failed");

        // FR-007: Log BEFORE execution
        await _db.InsertAuditLogAsync(new AuditLog
        {
            TaskId = taskId,
            Action = proposedAction.Action,
            Timestamp = DateTime.UtcNow,
            Status = "pending"
        });

        // Execute via MCP
        var result = await _mcpManager.CallToolAsync("playwright", proposedAction.Action, proposedAction.Args);

        // FR-008: Verify execution, update task status ONLY if verified
        var verified = await VerifyExecutionAsync(taskId, result);
        if (!verified)
            return ExecutionResult.Fail("Execution verification failed, task NOT marked complete");

        await _db.UpdateTaskStatusAsync(taskId, TaskStatus.Completed);
        return ExecutionResult.Success(result);
    }

    private bool ValidateAgentAction(AgentAction action)
    {
        // Check tool whitelist
        var allowedTools = new[] { "linkedin_search", "linkedin_scrape_profile", "linkedin_send_message", "send_email" };
        if (!allowedTools.Contains(action.Action))
            return false;

        // Validate JSON structure
        if (string.IsNullOrEmpty(action.Reason) || action.Args.ValueKind == JsonValueKind.Null)
            return false;

        return true;
    }

    private async Task<bool> VerifyExecutionAsync(Guid taskId, JsonElement result)
    {
        // FR-008: Task verification - check database state changed
        var task = await _db.GetTaskAsync(taskId);
        
        if (task.Type == TaskType.ScrapeProfile)
        {
            // Verify Lead record exists in database
            var lead = await _db.GetLeadAsync(task.LeadId);
            return lead != null && !string.IsNullOrEmpty(lead.ProfileData);
        }

        if (task.Type == TaskType.SendMessage)
        {
            // Verify OutreachMessage record exists
            var message = await _db.GetMessageAsync(task.MessageId);
            return message != null && message.Status == MessageStatus.Sent;
        }

        return false;
    }
}
```

**Reference**: [.NET Distributed Tracing](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing), [MCP Tool Execution Best Practices](https://www.emergentmind.com/topics/mcp-server-lifecycle)

**Alternatives Considered**:
- Multi-step agent planning: Violates FR-006 (single action per cycle), increases drift risk
- Agent with internal state: Violates FR-004 (stateless agent), leads to "GitHub Copilot issues"

---

## Summary of Technology Choices

| Component | Technology | Key Benefit | Reference |
|---|---|---|---|
| Desktop UI | .NET MAUI Blazor Hybrid (BlazorWebView) | Native performance, no browser overhead | [Microsoft Docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/) |
| Database | Supabase PostgreSQL (Supabase SDK, formerly supabase-csharp) | Managed PostgreSQL with REST API, connection pooling | [Supabase C# SDK](https://github.com/supabase-community/supabase-csharp) |
| Secrets | Azure Key Vault + DefaultAzureCredential | Passwordless auth, managed identities | [Azure Key Vault .NET](https://learn.microsoft.com/en-us/azure/key-vault/secrets/quick-create-net) |
| Telemetry | Azure Application Insights + OpenTelemetry | Distributed tracing, custom metrics | [OpenTelemetry with Azure Monitor](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-applicationinsights) |
| MCP Integration | Embedded MCP servers (stdio transport) | Secure IPC, no network exposure | [MCP Lifecycle](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle) |
| LinkedIn Automation | Playwright.NET + Stealth | Anti-detection, human simulation | [Playwright Stealth](https://www.scrapeless.com/en/blog/playwright-stealth) |
| Agent Pattern | Stateless LLM + Controller validation | Deterministic execution, database as source of truth | [MCP Security Checklist](https://www.networkintelligence.ai/blogs/model-context-protocol-mcp-security-checklist/) |

---

## Next Steps

1. **Generate Data Model (Phase 1)**: Define EF Core entities for Campaign, Task, Lead, OutreachMessage, AuditLog, EnvironmentSecret
2. **Create API Contracts (Phase 1)**: OpenAPI specifications for Controller → MCP tool invocations
3. **Write Quickstart Guide (Phase 1)**: Developer onboarding with setup instructions (Azure CLI, Supabase CLI, .NET 9 SDK)
4. **Update Agent Context**: Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot` to add MCP, Playwright, Supabase SDKs to GitHub Copilot instructions

**All Phase 0 research objectives complete.**
