# Research: Deterministic Desktop Outreach Agent

**Date**: 2026-01-06  
**Purpose**: Resolve technical unknowns identified during plan setup

## Research Tasks

Based on Technical Context analysis, the following areas require investigation:

1. **Supabase .NET SDK patterns** - Best practices for PostgREST API integration in .NET desktop apps
2. **OpenAI structured outputs** - Ensuring JSON schema adherence for 8 finite action types
3. **Playwright for .NET** - Anti-detection configuration for LinkedIn automation
4. **RestrictedPython integration** - Process invocation patterns from .NET with security isolation
5. **MAUI Blazor Hybrid** - Adapting shadcn + Tailwind CSS to Blazor component model
6. **State snapshot optimization** - Efficient serialization/deserialization for agent input (bounded context)

---

## 1. Supabase .NET SDK Patterns

### Decision
Use **supabase-csharp** community SDK with PostgREST client for database operations.

### Rationale
- Official Supabase .NET SDK is community-maintained but actively developed
- PostgREST API provides automatic REST endpoints for PostgreSQL tables
- Authentication via API keys (encrypted in database per FR-023)
- Supports all CRUD operations required for 8 entities

### Implementation Pattern
```csharp
// SupabaseRepository.cs
public class SupabaseRepository<T> where T : class
{
    private readonly Supabase.Client _client;
    
    public async Task<T> GetByIdAsync(Guid id)
    {
        var response = await _client.From<T>()
            .Select("*")
            .Where(x => x.Id == id)
            .Single();
        return response;
    }
    
    public async Task<List<T>> GetByCampaignIdAsync(Guid campaignId)
    {
        var response = await _client.From<T>()
            .Select("*")
            .Where(x => x.CampaignId == campaignId)
            .Get();
        return response.Models;
    }
    
    public async Task UpsertAsync(T entity)
    {
        await _client.From<T>().Upsert(entity);
    }
}
```

### Alternatives Considered
- **Npgsql direct** - Lower-level, requires manual SQL, no auto-generated API
- **Entity Framework Core** - Heavier, not optimized for PostgREST patterns

### References
- [supabase-csharp GitHub](https://github.com/supabase-community/supabase-csharp)
- [PostgREST API patterns](https://postgrest.org/en/stable/)

---

## 2. OpenAI Structured Outputs

### Decision
Use **OpenAI Function Calling with JSON Schema** to enforce 8 finite action types.

### Rationale
- GPT-4 supports strict JSON schema mode (no hallucinated fields)
- Define schema for each of 8 action types (FR-005i through FR-005p)
- Invalid schemas rejected by OpenAI API before reaching Controller
- Aligns with FR-005d (Validate Proposal step)

### Implementation Pattern
```csharp
// AgentService.cs
public async Task<AgentProposal> InvokeAgentAsync(CampaignState state)
{
    var tools = new List<Tool>
    {
        new Tool
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = "create_task",
                Description = "Propose new task with description and preconditions",
                Parameters = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["description"] = new JsonObject { ["type"] = "string" },
                        ["preconditions"] = new JsonObject { ["type"] = "array" }
                    },
                    ["required"] = new JsonArray { "description" }
                }
            }
        },
        // ... 7 more action types
    };
    
    var request = new ChatCompletionRequest
    {
        Model = "gpt-4",
        Messages = BuildStateSnapshot(state),
        Tools = tools,
        ToolChoice = "required" // Force function call
    };
    
    var response = await _openAIClient.CreateChatCompletionAsync(request);
    return ParseProposal(response.Choices[0].Message.ToolCalls[0]);
}
```

### Alternatives Considered
- **Free-form text parsing** - Unreliable, violates FR-010 (Agent output validated)
- **Few-shot prompting only** - No schema guarantee, can produce invalid JSON

### References
- [OpenAI Function Calling docs](https://platform.openai.com/docs/guides/function-calling)
- [JSON Schema specification](https://json-schema.org/)

---

## 3. Playwright for .NET Anti-Detection

### Decision
Use **Playwright for .NET with stealth configurations** to minimize LinkedIn bot detection.

### Rationale
- Playwright .NET SDK officially maintained by Microsoft
- Supports headless and headed modes (headed for debugging)
- Built-in anti-detection: realistic user-agent, viewport, navigation timing
- Can intercept network requests to avoid loading unnecessary resources

### Implementation Pattern
```csharp
// PlaywrightService.cs
public class PlaywrightService
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    
    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = false, // Set true for production
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled", // Hide automation flag
                "--disable-dev-shm-usage",
                "--no-sandbox"
            }
        });
    }
    
    public async Task<IPage> NavigateToLinkedInAsync()
    {
        var context = await _browser.NewContextAsync(new()
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "en-US",
            TimezoneId = "America/New_York"
        });
        
        var page = await context.NewPageAsync();
        await page.GotoAsync("https://www.linkedin.com");
        return page;
    }
}
```

### Best Practices
- **Rate limiting**: Wait 2-5 seconds between actions (FR-constraint: LinkedIn ToS compliance)
- **Session cookies**: Persist authentication cookies to avoid repeated logins
- **Human-like interactions**: Random delays, mouse movements (not MVP, future enhancement)

### Alternatives Considered
- **Selenium** - Older, easier to detect, less modern API
- **PuppeteerSharp** - Chrome-only, no cross-browser support

### References
- [Playwright for .NET docs](https://playwright.dev/dotnet/)
- [LinkedIn automation best practices](https://www.linkedin.com/help/linkedin/answer/56347)

---

## 4. RestrictedPython Integration from .NET

### Decision
Use **Process invocation to Python interpreter with RestrictedPython**-validated scripts.

### Rationale
- RestrictedPython runs within standard Python runtime (no separate container needed)
- .NET invokes `python -c "script"` via System.Diagnostics.Process
- RestrictedPython compile() validates script before execution
- Restricted globals prevent filesystem/network access (FR-039)

### Implementation Pattern
```csharp
// PythonSandbox.cs (C# side)
public class PythonSandbox
{
    public async Task<string> ExecuteScriptAsync(string script, Dictionary<string, object> context)
    {
        var wrappedScript = $@"
from RestrictedPython import compile_restricted, safe_globals
import json

code = compile_restricted('''{script}''', '<inline>', 'exec')
restricted_globals = safe_globals.copy()
restricted_globals['__builtins__']['__import__'] = None  # Block imports
restricted_globals['context'] = {JsonSerializer.Serialize(context)}

exec(code, restricted_globals)
print(json.dumps(restricted_globals.get('result', None)))
";

        var processInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"-c \"{wrappedScript.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(processInfo);
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        if (!string.IsNullOrEmpty(error))
            throw new PythonExecutionException(error);
        
        return output;
    }
}
```

```python
# Example restricted script (Python side, passed from LLM)
# User request: "Analyze response rates by industry"

import pandas as pd
df = pd.DataFrame(context['leads'])  # context injected by C#
result = df.groupby('industry')['responded'].mean().to_dict()
# result variable captured by C# wrapper
```

### Security Considerations
- **No filesystem access**: RestrictedPython blocks `open()`, `os`, `sys` modules
- **No network access**: Blocks `socket`, `urllib`, `requests`
- **Database read-only**: Context pre-fetched by C#, passed as JSON (FR-040)
- **Timeout**: Process.WaitForExit(60000) enforces 60-second limit (FR-041)

### Alternatives Considered
- **IronPython** - Outdated, Python 2.x only
- **Docker containers** - Over-engineered for MVP, requires Docker runtime

### References
- [RestrictedPython GitHub](https://github.com/zopefoundation/RestrictedPython)
- [.NET Process class](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process)

---

## 5. MAUI Blazor Hybrid with Existing React Prototype

### Decision
**Reuse HTML/CSS directly from existing React prototype** (https://github.com/andriyshevchenko/outreachgenie-app) in Blazor Hybrid WebView.

### Rationale
- Existing prototype uses React + shadcn + Tailwind CSS
- Blazor Hybrid MAUI WebView renders HTML/CSS identically to browser
- **HTML/CSS/Tailwind can be copied directly** to `wwwroot/` - no conversion needed
- Only React JSX logic needs conversion to C# Blazor syntax (event handlers, data binding)
- Significant time savings: UI already designed, tested, and styled

### Implementation Strategy

#### Phase 1: Copy Static Assets (No Changes)
```powershell
# Copy from andriyshevchenko/outreachgenie-app to OutreachAgent.Desktop/wwwroot/
cp -r outreachgenie-app/src/index.css wwwroot/css/
cp -r outreachgenie-app/tailwind.config.ts wwwroot/
cp -r outreachgenie-app/postcss.config.js wwwroot/
# Tailwind CSS works identically in Blazor WebView
```

#### Phase 2: Convert React Components to Blazor (Minimal Changes)

**React Component (existing)**:
```tsx
// src/components/ui/card.tsx
export const Card = ({ className, ...props }) => (
  <div className={cn("rounded-lg border bg-card", className)} {...props} />
);
```

**Blazor Component (converted)**:
```razor
<!-- Components/UI/Card.razor -->
<div class="@GetClassName()">
    @ChildContent
</div>

@code {
    [Parameter] public string? ClassName { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    
    private string GetClassName() => 
        string.Join(" ", "rounded-lg border bg-card", ClassName ?? "");
}
```

**Key Insight**: HTML structure and Tailwind classes stay **100% identical**. Only event handlers and data flow change.

#### Phase 3: Integrate C# Backend

**React (API calls)**:
```tsx
const [messages, setMessages] = useState([]);
const response = await fetch('/api/chat', { method: 'POST', body: JSON.stringify(input) });
setMessages([...messages, response]);
```

**Blazor (Direct C# calls)**:
```razor
@inject IChatService ChatService

<button @onclick="SendMessage">Send</button>

@code {
    private List<Message> messages = new();
    
    private async Task SendMessage()
    {
        var response = await ChatService.SendAsync(input); // Direct C# call, no HTTP
        messages.Add(response);
    }
}
```

### Existing Prototype Analysis

From https://github.com/andriyshevchenko/outreachgenie-app:

**Reusable (Copy Directly)**:
- ✅ `src/components/ui/` - All shadcn components (alert, button, card, etc.)
- ✅ `src/index.css` - Tailwind base styles with CSS variables
- ✅ `tailwind.config.ts` - Custom theme (LinkedIn blue, sidebar colors)
- ✅ All Tailwind utility classes
- ✅ Lucide React icons → Blazor.Lucide NuGet package (same icons)

**Requires Conversion** (React JSX → Blazor Razor):
- 🔄 `src/pages/ChatPage.tsx` → `Components/Pages/ChatPage.razor`
- 🔄 `src/pages/SettingsPage.tsx` → `Components/Pages/SettingsPage.razor`
- 🔄 `src/pages/AnalyticsPage.tsx` → `Components/Pages/AnalyticsPage.razor`
- 🔄 `src/components/layout/Sidebar.tsx` → `Components/Shared/Sidebar.razor`
- 🔄 Event handlers: `onClick` → `@onclick`, `onChange` → `@onchange`
- 🔄 State management: `useState` → C# properties with `StateHasChanged()`

**Not Needed** (Blazor provides built-in):
- ❌ `src/App.tsx` - Blazor has routing via `@page` directive
- ❌ `react-router-dom` - Blazor NavigationManager
- ❌ `@tanstack/react-query` - Direct C# service injection
- ❌ Vite build config - MAUI handles bundling

### Migration Effort Estimate

| Component | LOC | Conversion Time | Complexity |
|-----------|-----|----------------|------------|
| UI components (shadcn) | ~2000 | 1 day | Low (mostly HTML) |
| Pages (Chat, Settings, Analytics) | ~500 | 2 days | Medium (state + events) |
| Sidebar navigation | ~100 | 2 hours | Low |
| Tailwind config + CSS | ~300 | 0 hours | **Zero** (copy as-is) |
| **Total** | **~2900** | **3-4 days** | **Low-Medium** |

### Advantages of This Approach
1. **Proven UI/UX**: Design already validated, no guesswork
2. **Tailwind reuse**: Zero CSS rewrite, all utilities work identically
3. **Icon compatibility**: Lucide icons available in Blazor
4. **Faster delivery**: Skip UI design phase entirely
5. **No "port" risk**: HTML/CSS is HTML/CSS, works everywhere

### Alternatives Considered
- **Full Blazor rewrite**: Longer timeline, risk of design drift from prototype
- **Keep React + Electron**: Not desktop-native, heavier runtime
- **Flutter**: Different language, no C# integration

---

## 6. State Snapshot Optimization

### Decision
Use **bounded context serialization** with explicit field selection to avoid LLM token bloat.

### Rationale
- LLM context window limited (GPT-4: 128k tokens, but cost increases)
- SC-002 requires processing 100 leads without conversation history bloat
- Agent needs only current task + lead summary, not full campaign history
- FR-050 specifies bounded recent audit log

### Implementation Pattern
```csharp
// PromptBuilder.cs
public string BuildStateSnapshot(CampaignState state)
{
    var snapshot = new
    {
        Campaign = new
        {
            state.Campaign.Id,
            state.Campaign.Name,
            state.Campaign.Status
        },
        CurrentTask = state.Tasks.FirstOrDefault(t => t.Status == TaskStatus.InProgress),
        PendingTasks = state.Tasks
            .Where(t => t.Status == TaskStatus.Pending)
            .Select(t => new { t.Id, t.Description })
            .Take(10), // Limit to next 10 tasks
        LeadsSummary = new
        {
            Total = state.Leads.Count,
            Pending = state.Leads.Count(l => l.Status == LeadStatus.Pending),
            Contacted = state.Leads.Count(l => l.Status == LeadStatus.Contacted),
            CurrentLead = state.Leads.FirstOrDefault(l => l.Status == LeadStatus.Pending)
        },
        RecentAuditLog = state.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(5) // Last 5 actions only
            .Select(a => new { a.ActionType, a.Timestamp, a.Payload })
    };
    
    return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
}
```

### Token Budget Estimation
- Campaign metadata: ~100 tokens
- Current task: ~50 tokens
- Pending tasks (10): ~500 tokens
- Leads summary: ~200 tokens
- Recent audit log (5): ~250 tokens
- **Total per cycle: ~1,100 tokens** (well within GPT-4 limits, cost-effective)

### Alternatives Considered
- **Full state serialization** - Violates SC-002, bloats context unnecessarily
- **Database query on-demand** - Adds latency, agent can't reason about state

### References
- [OpenAI token counting](https://platform.openai.com/tokenizer)
- [Best practices for LLM context](https://platform.openai.com/docs/guides/prompt-engineering)

---

## Research Summary

| Area | Decision | Confidence | Blocker? |
|------|----------|------------|----------|
| Supabase patterns | supabase-csharp SDK | High | No |
| OpenAI structured output | Function calling with JSON schema | High | No |
| Playwright anti-detection | Stealth args + realistic user-agent | Medium | No (iterative tuning) |
| RestrictedPython integration | Process invocation with wrapped script | High | No |
| MAUI Blazor + Tailwind | shadcn-blazor port | Medium | No (prototype exists) |
| State snapshot optimization | Bounded context (1.1k tokens/cycle) | High | No |

**Outcome**: All technical unknowns resolved. No blockers for Phase 1 design. Medium confidence items (Playwright, shadcn-blazor) have fallback options and iterative refinement paths.
