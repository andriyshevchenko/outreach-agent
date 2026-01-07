# LLM Agent Contract (OpenAI/Azure OpenAI)

**Version**: 1.0.0  
**Service**: Azure OpenAI / OpenAI API  
**Model**: GPT-4 Turbo

---

## Service: `agent_propose_action`

### Purpose
Receives current campaign/task context from Controller, proposes a single actionable step with JSON-structured output. Agent is stateless and MUST NOT rely on conversation history.

### Request Schema

```json
{
  "model": "gpt-4-turbo",
  "messages": [
    {
      "role": "system",
      "content": "You are a LinkedIn outreach agent. Output valid JSON only. Propose ONE action per response."
    },
    {
      "role": "user",
      "content": "CONTEXT:\n- Campaign: CTO Outreach Q1\n- Task ID: task-abc-123\n- Task Type: ScrapeProfile\n- Target Lead: John Doe (https://www.linkedin.com/in/johndoe/)\n- Leads scraped today: 25/50\n- Daily quota remaining: 25\n\nPREVIOUS EXECUTION (last 3 tasks):\n1. ScrapeProfile for Jane Smith - SUCCESS (12s)\n2. ScrapeProfile for Bob Johnson - SUCCESS (15s)\n3. SendLinkedInMessage to Jane Smith - SUCCESS (8s)\n\nAvailable tools: linkedin_scrape_profile, linkedin_send_message, send_email, wait_cooldown\n\nPropose ONE action (JSON only)."
    }
  ],
  "temperature": 0.3,
  "max_tokens": 500,
  "response_format": { "type": "json_object" }
}
```

**Key Parameters**:
- `temperature`: 0.3 (low variance for deterministic output)
- `max_tokens`: 500 (sufficient for JSON response)
- `response_format`: `{ "type": "json_object" }` (enforces JSON output in GPT-4 Turbo)

### Response Schema (Agent Output)

```json
{
  "action": "linkedin_scrape_profile",
  "args": {
    "profileUrl": "https://www.linkedin.com/in/johndoe/",
    "fields": ["name", "title", "company", "email"]
  },
  "reason": "Task requires profile scraping; daily quota has 25 remaining, proceeding with next lead."
}
```

**Response Fields**:
- `action` (required): Tool name from whitelist (`linkedin_scrape_profile`, `linkedin_send_message`, `send_email`, `wait_cooldown`)
- `args` (required): JSON object with tool-specific arguments (must match tool contract)
- `reason` (required): 1-2 sentence rationale for proposed action

### Error Response Schema

```json
{
  "error": {
    "code": "QUOTA_EXCEEDED",
    "message": "Daily LinkedIn quota (50) exceeded, cannot propose scraping action",
    "suggestedAction": "wait_cooldown",
    "suggestedArgs": {
      "durationHours": 24
    }
  }
}
```

**Error Scenarios**:
- `QUOTA_EXCEEDED` - Daily/weekly rate limits reached
- `INVALID_CONTEXT` - Missing required context fields (campaign, task, lead)
- `NO_VALID_ACTION` - No actionable step available (e.g., all leads processed)

### Validation Rules (Controller Responsibility)

**Input Validation** (before LLM call):
1. Context must include: `CampaignName`, `TaskId`, `TaskType`, `LeadLinkedInUrl`
2. Daily quota must be > 0 for scraping/messaging actions
3. Task `Status` must be `Pending` or `InProgress`

**Output Validation** (FR-011):
1. JSON structure: Must have `action`, `args`, `reason` fields
2. Tool whitelist: `action` must be in `["linkedin_scrape_profile", "linkedin_send_message", "send_email", "wait_cooldown"]`
3. Argument validation: `args` must match tool contract (e.g., `profileUrl` for scraping)
4. Reason length: 10-200 characters (prevents empty or excessive reasoning)

### State Reloading (FR-010)

**Critical Rule**: Controller MUST reload all state from database before each LLM invocation. NO reliance on previous agent responses.

**Context Construction**:
```csharp
public async Task<AgentContext> LoadAgentContextAsync(Guid taskId)
{
    var task = await _db.GetTaskAsync(taskId);
    var campaign = await _db.GetCampaignAsync(task.CampaignId);
    var lead = await _db.GetLeadAsync(task.LeadId);

    // FR-010: Reload recent execution results from database (NOT conversation history)
    var recentTasks = await _db.GetRecentCompletedTasksAsync(campaign.Id, limit: 3);
    var recentResults = recentTasks.Select(t => new TaskResult
    {
        TaskType = t.Type,
        Status = t.Status,
        ExecutionTimeMs = (t.CompletedAt.Value - t.CreatedAt).TotalMilliseconds,
        Output = t.Output
    }).ToList();

    // FR-043: Check daily quota
    var todayScrapedCount = await _db.CountTasksByTypeAsync(campaign.Id, TaskType.ScrapeProfile, DateTime.UtcNow.Date);
    var todayMessageCount = await _db.CountTasksByTypeAsync(campaign.Id, TaskType.SendLinkedInMessage, DateTime.UtcNow.Date);

    return new AgentContext
    {
        CampaignName = campaign.Name,
        TaskId = task.Id,
        TaskType = task.Type,
        LeadName = lead.Name,
        LeadLinkedInUrl = lead.LinkedInUrl,
        RecentTaskResults = recentResults,
        LeadsScrapedCount = todayScrapedCount,
        MessagesSentCount = todayMessageCount,
        DailyQuotaRemaining = 50 - todayScrapedCount // FR-039: 50/day limit
    };
}
```

### Single-Action Enforcement (FR-006)

**Rule**: Agent proposes ONE action per cycle. Controller validates and executes, then reloads state for next cycle.

**Workflow**:
```
1. Controller loads context from database
2. Controller calls LLM with context
3. LLM proposes ONE action (JSON output)
4. Controller validates action
5. Controller executes action via MCP
6. Controller updates database
7. REPEAT (goto step 1 for next task)
```

**Anti-Pattern (FORBIDDEN)**:
```json
// INVALID: Multiple actions in one response
{
  "actions": [
    { "action": "linkedin_scrape_profile", "args": {...} },
    { "action": "linkedin_send_message", "args": {...} }
  ]
}
```

### Example Prompt Construction

**System Prompt** (always included):
```
You are a LinkedIn outreach automation agent. Your role is to propose ONE atomic action based on the current task context provided by the Controller.

CRITICAL RULES:
1. Output ONLY valid JSON with fields: action, args, reason
2. Propose EXACTLY ONE action per response (not a sequence)
3. DO NOT assume conversation history exists - all context is in the current message
4. Check daily quota before proposing scraping/messaging actions
5. If quota exceeded, propose "wait_cooldown" action

Available tools:
- linkedin_scrape_profile: Scrape LinkedIn profile data
- linkedin_send_message: Send LinkedIn message or connection request
- send_email: Send email to lead
- wait_cooldown: Pause execution for X hours

Output format:
{
  "action": "tool_name",
  "args": { /* tool-specific arguments */ },
  "reason": "1-2 sentence rationale"
}
```

**User Prompt Template** (constructed per cycle):
```
CURRENT TASK:
- Campaign: {{campaign_name}}
- Task ID: {{task_id}}
- Task Type: {{task_type}}
- Target Lead: {{lead_name}} ({{lead_linkedin_url}})

DATABASE STATE (reloaded every cycle):
- Leads scraped today: {{leads_scraped_count}}/50
- Messages sent today: {{messages_sent_count}}/50
- Daily quota remaining: {{daily_quota_remaining}}

PREVIOUS EXECUTION RESULTS (last 3 completed tasks):
{{#each recent_task_results}}
- {{task_type}} for {{lead_name}} - {{status}} ({{execution_time_ms}}ms)
{{/each}}

Available tools: linkedin_scrape_profile, linkedin_send_message, send_email, wait_cooldown

CONSTRAINTS:
- Propose ONLY ONE action
- Check quota before proposing scraping/messaging
- If quota = 0, propose "wait_cooldown" with durationHours = 24

Propose action (JSON only, no markdown):
```

### Controller Integration

**C# Implementation**:
```csharp
// OutreachAgent.Agent/LlmAgentService.cs
public class LlmAgentService
{
    private readonly HttpClient _httpClient; // Azure OpenAI client
    private readonly string _apiKey;
    private readonly string _endpoint;

    public async Task<AgentAction> ProposeActionAsync(AgentContext context)
    {
        // Construct prompt from context
        var systemPrompt = GetSystemPrompt();
        var userPrompt = ConstructUserPrompt(context);

        var requestBody = new
        {
            model = "gpt-4-turbo",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3,
            max_tokens = 500,
            response_format = new { type = "json_object" } // GPT-4 Turbo JSON mode
        };

        var response = await _httpClient.PostAsJsonAsync(_endpoint, requestBody);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
        var agentOutput = result.Choices[0].Message.Content;

        // Parse and return AgentAction
        return JsonSerializer.Deserialize<AgentAction>(agentOutput);
    }

    private string ConstructUserPrompt(AgentContext context)
    {
        var recentTasksSummary = string.Join("\n", context.RecentTaskResults.Select(r =>
            $"- {r.TaskType} for {r.LeadName} - {r.Status} ({r.ExecutionTimeMs}ms)"));

        return $@"
CURRENT TASK:
- Campaign: {context.CampaignName}
- Task ID: {context.TaskId}
- Task Type: {context.TaskType}
- Target Lead: {context.LeadName} ({context.LeadLinkedInUrl})

DATABASE STATE:
- Leads scraped today: {context.LeadsScrapedCount}/50
- Messages sent today: {context.MessagesSentCount}/50
- Daily quota remaining: {context.DailyQuotaRemaining}

PREVIOUS EXECUTION RESULTS (last 3 completed tasks):
{recentTasksSummary}

Available tools: linkedin_scrape_profile, linkedin_send_message, send_email, wait_cooldown

Propose ONE action (JSON only):
";
    }
}

// Controller validation
public async Task<ExecutionResult> ExecuteCycleAsync(Guid taskId)
{
    // FR-010: Reload state every cycle
    var context = await LoadAgentContextAsync(taskId);

    // Call LLM
    var proposedAction = await _agent.ProposeActionAsync(context);

    // FR-011: Validate agent output
    if (!ValidateAgentAction(proposedAction))
    {
        return ExecutionResult.Fail("Agent output validation failed");
    }

    // FR-007: Audit log BEFORE execution
    await _db.InsertAuditLogAsync(new AuditLog
    {
        TaskId = taskId,
        Action = proposedAction.Action,
        Args = JsonSerializer.SerializeToDocument(proposedAction.Args),
        Status = LogStatus.Pending,
        Timestamp = DateTime.UtcNow
    });

    // Execute via MCP
    var result = await _mcpManager.CallToolAsync("playwright", proposedAction.Action, proposedAction.Args);

    // FR-008: Verify execution
    var verified = await VerifyExecutionAsync(taskId, result);
    if (!verified)
    {
        return ExecutionResult.Fail("Verification failed, task NOT marked complete");
    }

    await _db.UpdateTaskStatusAsync(taskId, TaskStatus.Completed);
    return ExecutionResult.Success(result);
}
```

---

## Fallback Actions

### Quota Exceeded
**Agent Output**:
```json
{
  "action": "wait_cooldown",
  "args": {
    "durationHours": 24,
    "reason": "Daily LinkedIn quota (50) exceeded"
  },
  "reason": "Daily quota reached, pausing campaign until midnight UTC reset."
}
```

### Session Expired
**Agent Output**:
```json
{
  "action": "refresh_session",
  "args": {
    "keyVaultSecretName": "linkedin-session-cookie"
  },
  "reason": "LinkedIn session expired, fetching fresh credentials from Key Vault."
}
```

### No More Leads
**Agent Output**:
```json
{
  "action": "complete_campaign",
  "args": {
    "campaignId": "campaign-abc"
  },
  "reason": "All leads processed, marking campaign as completed."
}
```

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task ProposeAction_ValidContext_ReturnsValidJson()
{
    // Arrange
    var context = new AgentContext
    {
        CampaignName = "Test Campaign",
        TaskType = TaskType.ScrapeProfile,
        LeadLinkedInUrl = "https://www.linkedin.com/in/test/",
        DailyQuotaRemaining = 30
    };

    // Act
    var action = await _agentService.ProposeActionAsync(context);

    // Assert
    Assert.Equal("linkedin_scrape_profile", action.Action);
    Assert.NotNull(action.Args);
    Assert.NotEmpty(action.Reason);
}

[Fact]
public async Task ProposeAction_QuotaZero_ReturnsWaitCooldown()
{
    // Arrange
    var context = new AgentContext
    {
        DailyQuotaRemaining = 0
    };

    // Act
    var action = await _agentService.ProposeActionAsync(context);

    // Assert
    Assert.Equal("wait_cooldown", action.Action);
    Assert.Equal(24, action.Args.GetProperty("durationHours").GetInt32());
}
```

### Integration Tests
- Test with Azure OpenAI sandbox (real API calls)
- Verify JSON output parsing
- Test error handling (API failures, invalid JSON)

### E2E Tests
- Run full campaign with 5 tasks
- Verify each LLM response triggers correct MCP tool
- Test quota enforcement (stops at 50th action)
