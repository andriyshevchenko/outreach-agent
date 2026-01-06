# Quickstart: Deterministic Desktop Outreach Agent

**Goal**: Set up local development environment and run first campaign cycle

**Time**: 30 minutes  
**Prerequisites**: Windows 11+ or macOS 12+, .NET 10 SDK, Python 3.11+, Supabase account

---

## 1. Environment Setup (10 min)

### Clone Repositories

```powershell
# Main repository
git clone https://github.com/your-org/outreach-agent.git
cd outreach-agent
git checkout 001-deterministic-agent-mvp

# Existing React prototype (for UI reference/copy)
cd ..
git clone https://github.com/andriyshevchenko/outreachgenie-app.git
```

**Note**: The React prototype UI will be reused directly in Blazor Hybrid. All HTML/CSS/Tailwind classes work identically - only JSX → Razor conversion needed.

### Install Dependencies

```powershell
# .NET 10 SDK (if not installed)
winget install Microsoft.DotNet.SDK.10

# Python 3.11+ (for RestrictedPython sandbox)
winget install Python.Python.3.11

# Playwright browsers (after project restore)
# Will be done in step 3
```

### Copy UI Assets from Prototype (Optional, for UI development)

```powershell
# Copy Tailwind config and CSS from React prototype
cp ../outreachgenie-app/src/index.css src/OutreachAgent.Desktop/wwwroot/css/
cp ../outreachgenie-app/tailwind.config.ts src/OutreachAgent.Desktop/
cp ../outreachgenie-app/postcss.config.js src/OutreachAgent.Desktop/

# Note: React components in outreachgenie-app/src/components/ and outreachgenie-app/src/pages/
# are reference examples for Blazor conversion. HTML structure and Tailwind classes stay identical.
```

1. Create Supabase project at https://supabase.com
2. Run schema migration:
   ```powershell
   # Copy SQL from specs/001-deterministic-agent-mvp/data-model.md
   # Execute in Supabase SQL Editor
   ```
3. Note your Supabase URL and anon key

---

## 2. Configuration (5 min)

### Create appsettings.Development.json

```powershell
cd src/OutreachAgent.Desktop
```

Create `appsettings.Development.json`:
```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "AnonKey": "your-anon-key"
  },
  "LLM": {
    "Provider": "OpenAI",
    "ApiKey": "sk-your-openai-key",
    "Model": "gpt-4"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "OutreachAgent": "Debug"
    }
  }
}
```

**Note**: These secrets will be encrypted and moved to database in production (FR-023).

---

## 3. Build & Run (10 min)

### Restore Dependencies

```powershell
cd ..\..\  # Back to repo root
dotnet restore
```

### Install Playwright Browsers

```powershell
cd tests/OutreachAgent.E2E.Tests
pwsh bin/Debug/net10.0/playwright.ps1 install
cd ..\..
```

### Build Solution

```powershell
dotnet build
```

### Run Tests (Optional, verify setup)

```powershell
dotnet test --filter "FullyQualifiedName~OutreachAgent.Core.Tests"
```

### Run Desktop App

```powershell
cd src/OutreachAgent.Desktop
dotnet run
```

Application launches with Campaign List view (empty initially).

---

## 4. Create First Campaign (5 min)

### Import Sample Leads

1. Click "New Campaign" button
2. Enter name: "Test Campaign Q1 2026"
3. Import leads from CSV:
   ```csv
   full_name,profile_url,job_title,company
   Jane Doe,https://linkedin.com/in/janedoe,Senior Engineer,TechCorp
   John Smith,https://linkedin.com/in/johnsmith,Principal Developer,CloudCo
   ```
4. Campaign status: `initializing` → `active` after validation

### Define Campaign Goal (Triggers Scoring Algorithm)

1. In chat interface, type:
   ```
   "Generate scoring algorithm for this campaign. Goal: Target engineers at 50-500 person companies."
   ```
2. Agent proposes `persist_artifact` with `scoring_algorithm`
3. Approve artifact save
4. Agent proposes `analyze_leads` with `prioritize`
5. Agent scores leads (Jane: 87.5, John: 82.3)

### Execute First Cycle

1. Agent proposes `select_next_task` (highest priority lead)
2. Agent proposes `execute_tool` with `browser_navigate` to Jane's profile
3. Playwright opens browser, navigates to LinkedIn
4. Agent proposes `generate_message` for connection request
5. Review message, click "Approve"
6. Agent proposes `execute_tool` with `send_message`
7. Controller logs to AuditLog, updates task status to `done`
8. Cycle complete (took ~30 seconds including LLM latency)

---

## 5. Verify Stop/Continue (5 min)

### Stop Mid-Campaign

1. Click "Stop" button during next cycle
2. Agent completes current atomic action (FR-005h: Update State)
3. Campaign status: `active` → `paused`
4. ExecutionState.current_task_id saved

### Close and Reopen App

```powershell
# Close app window
# Restart
cd src/OutreachAgent.Desktop
dotnet run
```

### Resume Campaign

1. Campaign List shows "Test Campaign Q1 2026" with status `paused`
2. Click "Resume" button (FR-046: resume without chat context)
3. New chat session starts (no conversation history)
4. Type: "continue"
5. Agent loads state from database (FR-009: fresh state reload)
6. Agent proposes next action (task #2: follow up on Jane)
7. Execution continues from exact point (SC-001: <5 second resume)

**Verification**: Check AuditLog in Supabase - no duplicate actions, continuous sequence.

---

## 6. Verify Context Loss Prevention (5 min)

### Delete Chat History

1. Stop campaign
2. In database, clear any conversation history tables (if implemented)
3. Resume campaign with "continue" command
4. Agent still knows campaign state, pending tasks, lead status

**Verification**: SC-012 pass - agent does NOT say "I don't remember" or "summarizing conversation"

---

## Troubleshooting

### "Supabase connection failed"
- Verify Supabase URL and anon key in appsettings.Development.json
- Check Supabase project is not paused
- Verify schema migration ran successfully

### "OpenAI API error"
- Verify API key is valid
- Check OpenAI account has credits
- Try switching to `gpt-3.5-turbo` if GPT-4 unavailable

### "Playwright browser launch failed"
- Run `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`
- Check system has display (headless mode requires Xvfb on Linux)

### "Python script execution failed"
- Verify Python 3.11+ installed: `python --version`
- Install RestrictedPython: `pip install RestrictedPython`
- Check PATH includes Python executable

---

## Next Steps

1. **Read Implementation Plan**: `specs/001-deterministic-agent-mvp/plan.md`
2. **Review Data Model**: `specs/001-deterministic-agent-mvp/data-model.md`
3. **Study Agent Contract**: `specs/001-deterministic-agent-mvp/contracts/agent-contract.md`
4. **Run Full Test Suite**: `dotnet test` (includes integration and E2E tests)
5. **Explore Constitution**: `.specify/memory/constitution.md` (7 core principles)

---

## Development Workflow

### Red-Green-Refactor (Constitution Principle VII)

1. **Write Test First**:
   ```csharp
   [Fact]
   public async Task CampaignController_StopButton_CompletesCurrentAction()
   {
       // Arrange: Campaign with in-progress task
       // Act: Call StopAsync()
       // Assert: Task marked done, state persisted, campaign paused
   }
   ```

2. **Run Test (Red)**: `dotnet test --filter "FullyQualifiedName~CampaignControllerTests.StopButton"`

3. **Implement Feature (Green)**:
   ```csharp
   public async Task StopAsync()
   {
       await _currentAction.WaitForCompletionAsync();
       await _stateManager.PersistStateAsync(_campaignId);
       _campaign.Status = CampaignStatus.Paused;
   }
   ```

4. **Refactor**: Extract methods, add logging, improve readability

5. **Verify Static Analysis**: `dotnet build` (warnings treated as errors per Constitution III)

### Git Workflow

```powershell
git checkout -b feature/implement-stop-button
# Make changes
git add .
git commit -m "feat: Implement stop button with atomic action completion (FR-048)"
git push origin feature/implement-stop-button
# Create PR, ensure CI passes (static analysis + tests)
```

---

## Key Files Reference

| Path | Purpose |
|------|---------|
| `src/OutreachAgent.Core/Controller/CampaignController.cs` | 8-step execution loop (FR-005a-h) |
| `src/OutreachAgent.Core/Controller/ProposalValidator.cs` | Schema validation for 8 action types |
| `src/OutreachAgent.Infrastructure/Database/SupabaseRepository.cs` | CRUD for 8 entities |
| `src/OutreachAgent.Infrastructure/LLM/OpenAIProvider.cs` | GPT-4 function calling with JSON schema |
| `src/OutreachAgent.Desktop/Components/Pages/CampaignList.razor` | UI for FR-045 (campaign list view) |
| `tests/OutreachAgent.Integration.Tests/EndToEnd/CampaignResumeTests.cs` | SC-001, SC-003 verification |

---

## Success Metrics

After completing this quickstart, you should be able to:

- ✅ Launch desktop app with campaign list UI
- ✅ Create campaign with lead import and scoring algorithm generation
- ✅ Execute 3+ cycles (navigate profile, send message, update state)
- ✅ Stop campaign mid-execution and resume from exact point (<5s)
- ✅ Verify no context loss when deleting chat history
- ✅ See audit logs in Supabase for every tool execution
- ✅ Run unit tests with >90% pass rate

**Next**: Proceed to implementation phase with `/speckit.tasks` command.
