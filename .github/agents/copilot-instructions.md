# GitHub Copilot Agent Instructions

**Project**: Outreach Agent  
**Feature**: 001-deterministic-agent-mvp  
**Auto-Generated**: 2026-01-06  
**Purpose**: Context for GitHub Copilot to understand project technologies and architecture

---

## UI Migration Strategy

### Existing React Prototype
**Repository**: https://github.com/andriyshevchenko/outreachgenie-app  
**Stack**: React + TypeScript + shadcn/ui + Tailwind CSS

### Reuse Approach
1. **HTML/CSS/Tailwind**: Copy directly to `OutreachAgent.Desktop/wwwroot/` - **zero changes needed**
2. **React Components**: Convert JSX → Blazor Razor syntax (HTML structure stays identical)
3. **Event Handlers**: `onClick` → `@onclick`, `onChange` → `@onchange`
4. **State Management**: `useState` → C# properties + `StateHasChanged()`
5. **Icons**: Lucide React → Blazor.Lucide NuGet package (same icon names)

### Example Conversion

**React (existing)**:
```tsx
// outreachgenie-app/src/pages/ChatPage.tsx
import { Button } from '@/components/ui/button';

export function ChatPage() {
  const [messages, setMessages] = useState<Message[]>([]);
  
  const handleSend = async () => {
    const response = await fetch('/api/chat', { method: 'POST' });
    setMessages([...messages, response]);
  };
  
  return (
    <div className="flex flex-col h-screen">
      <Button onClick={handleSend}>Send</Button>
    </div>
  );
}
```

**Blazor (converted)**:
```razor
@* OutreachAgent.Desktop/Components/Pages/ChatPage.razor *@
@inject IChatService ChatService

<div class="flex flex-col h-screen">
    <Button OnClick="HandleSend">Send</Button>
</div>

@code {
    private List<Message> messages = new();
    
    private async Task HandleSend()
    {
        var response = await ChatService.SendAsync();
        messages.Add(response);
        StateHasChanged();
    }
}
```

**Key Insight**: Tailwind classes (`flex flex-col h-screen`) stay **100% identical**. Only C# syntax changes.

---

## Technology Stack

### Language & Runtime
- **C# 14** with **.NET 10 LTS** (support until November 2028)
- Target Framework: `net10.0-maui` for desktop-first cross-platform

### Primary Dependencies
- **.NET MAUI**: Desktop application framework (Windows 11+, macOS 12+)
- **Blazor Hybrid**: HTML/CSS UI components (**reuses existing React prototype from https://github.com/andriyshevchenko/outreachgenie-app**)
- **Supabase .NET SDK**: PostgreSQL persistence via PostgREST API
- **OpenAI .NET SDK**: LLM provider (GPT-4 default, generic interface for alternatives)
- **Playwright for .NET**: LinkedIn browser automation (anti-detection, cross-platform)
- **RestrictedPython**: Sandboxed Python script execution (invoked via System.Diagnostics.Process)

### Testing Frameworks
- **xUnit**: Unit and integration tests
- **Playwright**: Browser automation E2E tests

---

## Database Schema (Supabase PostgreSQL)

8 core entities with UUID primary keys:

1. **Campaign**: Primary namespace (id, name, status, created_at, updated_at)
   - Status: initializing | active | paused | completed | error

2. **Task**: Work units (id, campaign_id, description, status, preconditions, metadata)
   - Status: pending | in-progress | done | blocked

3. **Lead**: LinkedIn prospects (id, campaign_id, full_name, profile_url, job_title, company, weight_score, status)
   - Status: pending | contacted | responded | rejected
   - weight_score: 0-100 prioritization

4. **Artifact**: Persisted knowledge (id, campaign_id, artifact_type, artifact_key, source, content, version)
   - Types: job_posting | scoring_algorithm | python_script | analysis_result

5. **AuditLog**: Tool invocation audit trail (id, campaign_id, timestamp, action_type, payload, success)

6. **EventLog**: State mutation log (id, campaign_id, timestamp, entity_type, entity_id, change_payload)

7. **Configuration**: Encrypted settings (id, user_id, key_name, encrypted_value, config_type)

8. **ExecutionState**: Current position (campaign_id, current_task_id, last_action_timestamp, cycle_count)

---

## Architecture Principles (Constitution)

### Controller Authority (NON-NEGOTIABLE)
- Controller owns all state mutations and side effects
- LLM Agent proposes actions only, never mutates state directly
- UI cannot bypass Controller for state changes

### Deterministic State-First Design
- Agent receives state snapshot per cycle (no conversation history dependency)
- System resumable from persisted state after application restart
- State reloaded every execution cycle from database

### 8-Step Execution Loop (FR-005a through FR-005h)
```
1. Load State: Fresh state from database
2. Select Task: Eligible tasks based on status and preconditions
3. Invoke Agent: LLM with state snapshot (no conversation history)
4. Validate Proposal: Check against 8 allowed action types
5. Execute Action: Controller-mediated tool execution
6. Persist Logs: Mandatory audit log entry
7. Update State: Task status change after verification
8. Repeat: Return to step 1
```

### 8 Finite Action Types (FR-005i through FR-005p)
- `create_task`: Propose new task
- `select_next_task`: Choose next task to work on
- `execute_tool`: Browser automation, filesystem, CLI
- `generate_message`: Outreach content generation
- `analyze_leads`: Lead prioritization/scoring
- `request_user_input`: Request user decision
- `persist_artifact`: Save data as artifact
- `no_op`: Explicit no-action (waiting state)

---

## Project Structure

```
OutreachAgent.sln
Directory.Build.props            # Centralized build configuration
Directory.Packages.props          # Centralized NuGet versions
.editorconfig                     # Code style enforcement

src/
├── OutreachAgent.Desktop/        # MAUI Blazor Hybrid UI
│   ├── MauiProgram.cs
│   ├── Components/Pages/         # Campaign list, settings, chat UI
│   └── wwwroot/                  # shadcn + Tailwind CSS

├── OutreachAgent.Core/           # Controller + domain logic
│   ├── Controller/
│   │   ├── CampaignController.cs    # Execution loop orchestrator
│   │   ├── StateManager.cs          # State load/persist
│   │   └── ProposalValidator.cs     # Action vocabulary validation
│   ├── Models/Entities/             # 8 database entities
│   ├── Models/Proposals/            # 8 agent proposal DTOs
│   └── Services/
│       ├── AgentService.cs          # LLM invocation
│       ├── ToolExecutor.cs          # Mediated tool execution
│       └── AuditLogger.cs           # Mandatory audit logging

└── OutreachAgent.Infrastructure/    # External integrations
    ├── Database/SupabaseRepository.cs
    ├── LLM/
    │   ├── ILLMProvider.cs           # Generic interface
    │   └── OpenAIProvider.cs         # GPT-4 implementation
    └── Automation/
        ├── PlaywrightService.cs      # LinkedIn automation
        └── PythonSandbox.cs          # RestrictedPython wrapper

tests/
├── OutreachAgent.Core.Tests/        # Unit tests
├── OutreachAgent.Integration.Tests/ # Integration tests
└── OutreachAgent.E2E.Tests/         # Playwright E2E tests
```

---

## Code Quality Requirements

### Static Analysis (Constitution Principle III)
- `TreatWarningsAsErrors` MUST be enabled
- SonarAnalyzer.CSharp MUST be installed
- StyleCop.Analyzers MUST be installed
- Build fails on analyzer violations

### Centralized Configuration (Constitution Principle IV)
- `Directory.Build.props`: Shared build properties
- `Directory.Packages.props`: NuGet package versions
- Projects reference packages WITHOUT version numbers

### Observability (Constitution Principle VI)
- ILogger<T> for structured logging
- All Controller actions logged with context (campaign ID, lead ID, action type)
- Correlation IDs for operation tracking

### Test-First (Constitution Principle VII)
- Write tests before implementation (Red-Green-Refactor)
- Contract tests for Controller ↔ Agent interface
- Integration tests for database operations
- E2E tests for browser automation

---

## Key Contracts

### State Snapshot (Controller → Agent)
```csharp
public class StateSnapshot
{
    public Campaign Campaign { get; set; }
    public Task? CurrentTask { get; set; }
    public List<Task> PendingTasks { get; set; } // Max 10
    public LeadsSummary LeadsSummary { get; set; }
    public List<AuditLogEntry> RecentAuditLog { get; set; } // Max 5
    public List<ArtifactReference> AvailableArtifacts { get; set; }
}
```

### Agent Proposal (Agent → Controller)
```csharp
public abstract class AgentProposal
{
    public string ActionType { get; set; } // One of 8 finite types
    public object Parameters { get; set; }
    public string? Justification { get; set; }
}
```

---

## Performance Targets

- UI responsiveness: <100ms for user actions
- State persistence: <500ms for database writes
- Campaign resume: <5 seconds from UI click to execution
- Database queries: <2 seconds for responsive UI
- Python scripts: <60 seconds max execution time

---

## Success Criteria

- User can stop agent mid-campaign and resume within 5 seconds (SC-001)
- Agent processes 100 leads without conversation history bloat (SC-002)
- Application restart results in zero data loss (SC-003)
- Deleting chat history does not affect execution (SC-012)
- Controller enforces 8-step loop with zero skips (SC-011)
- Invalid agent proposals rejected with 100% accuracy (SC-013)

---

## Common Tasks

### Create New Entity
1. Define model in `OutreachAgent.Core/Models/Entities/`
2. Add to SupabaseRepository generic CRUD
3. Update data-model.md with schema
4. Create migration in Supabase SQL Editor

### Add New Tool
1. Define interface in `OutreachAgent.Core/Services/IToolExecutor.cs`
2. Implement in `OutreachAgent.Infrastructure/Automation/`
3. Add to `execute_tool` proposal schema in agent-contract.md
4. Update ProposalValidator to recognize new tool_name

### Create Test
1. Write test in appropriate test project
2. Use xUnit `[Fact]` or `[Theory]` attributes
3. Follow Arrange-Act-Assert pattern
4. Run: `dotnet test --filter "FullyQualifiedName~YourTest"`

---

## References

- **Constitution**: `.specify/memory/constitution.md`
- **Feature Spec**: `specs/001-deterministic-agent-mvp/spec.md`
- **Implementation Plan**: `specs/001-deterministic-agent-mvp/plan.md`
- **Data Model**: `specs/001-deterministic-agent-mvp/data-model.md`
- **Agent Contract**: `specs/001-deterministic-agent-mvp/contracts/agent-contract.md`
- **Quickstart**: `specs/001-deterministic-agent-mvp/quickstart.md`

---

**Last Updated**: 2026-01-06 (from plan.md Technical Context)
