# Implementation Plan: LinkedIn Outreach Copilot Agent

**Branch**: `001-linkedin-outreach-copilot` | **Date**: 2026-01-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-linkedin-outreach-copilot/spec.md`

## Summary

Build a deterministic LinkedIn outreach copilot agent as a .NET MAUI Blazor Hybrid desktop-first application with Controller-Agent architecture where the Controller (C# .NET) owns all state and execution while the Agent (LLM) proposes actions. The system uses Supabase PostgreSQL as the single source of truth, enabling campaign continuation without conversation history. Key capabilities: campaign management, LinkedIn automation via Playwright MCP, lead prioritization, Ukrainian message generation, Azure integration (Key Vault for credentials, Application Insights for telemetry), and comprehensive audit logging enforcing 5 NON-NEGOTIABLE invariants.

## Technical Context

**Language/Version**: C# 12.0 with .NET 10.0  
**Primary Dependencies**: .NET MAUI 10.0, Blazor (BlazorWebView), Supabase (formerly supabase-csharp), Azure.Security.KeyVault.Secrets 4.8+, Azure.Monitor.OpenTelemetry, Model Context Protocol SDK, Microsoft.Playwright 1.57+  
**Storage**: Supabase (PostgreSQL) for all persistent state (campaigns, tasks, leads, audit logs); Azure Key Vault for credentials  
**Testing**: xUnit 2.9+, Playwright for UI automation, FluentAssertions, Moq for mocking  
**Target Platform**: Windows 10/11 (primary), macOS 11+, Linux (secondary); desktop-first with 1920x1080+ optimization  
**Project Type**: Hybrid desktop application (MAUI + Blazor)  
**Performance Goals**: < 3s startup, < 100ms UI response, 60 FPS, < 500MB baseline memory  
**Constraints**: Internet required for Azure services (Key Vault, Application Insights); no conversation history reliance; all state in database; desktop-first UX  
**Scale/Scope**: 1,000 leads per campaign, 50 connection requests/day rate limit, paginated UI (50 items/page), distributed tracing across MCP tools

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research Validation

- [x] **Desktop-First Design**: Windows desktop primary platform (1920x1080+), mobile secondary - chat/settings/analytics pages optimized for desktop workflows
- [x] **Blazor Components Default**: All UI in Razor components (.razor); native MAUI controls ONLY for file pickers and system dialogs (justified by OS integration needs)
- [x] **Static Code Analyzers**: Directory.Build.props will configure StyleCop.Analyzers + SonarAnalyzer.CSharp with TreatWarningsAsErrors=true
- [x] **Centralized Package Management**: Directory.Packages.props will manage all NuGet versions centrally (ManagePackageVersionsCentrally=true)
- [x] **Performance Targets**: < 3s startup (SC-002), < 100ms UI interactions, 60 FPS animations, < 500MB memory baseline - measurable via Application Insights
- [x] **Test-First Workflow**: TDD enforced - tests written per user story BEFORE implementation, organized by story for independent validation
- [x] **User Stories Prioritized**: 10 stories with P1 (6 core) and P2 (4 value-add), each independently testable as MVP increment

**GATE STATUS**: ✅ **PASS** - All constitution requirements validated for this feature

### Post-Design Validation

*To be completed after Phase 1 design artifacts*

- [ ] Data model validates desktop-first constraints (local-first with sync)
- [ ] Contracts enforce invariants (no side effects without logs, verified task completion)
- [ ] Quickstart demonstrates constitution compliance (TDD workflow, analyzer configuration)

## Project Structure

### Documentation (this feature)

```text
specs/001-linkedin-outreach-copilot/
├── plan.md              # This file
├── research.md          # Phase 0: Technology decisions and best practices
├── data-model.md        # Phase 1: Entity schemas and relationships
├── quickstart.md        # Phase 1: Developer setup and first-run guide
├── contracts/           # Phase 1: API/MCP tool contracts
│   ├── controller-agent-contract.md
│   ├── mcp-playwright-contract.md
│   ├── mcp-desktop-commander-contract.md
│   └── supabase-schema.sql
└── tasks.md             # Phase 2: Implementation tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── OutreachAgent.Core/           # Shared business logic (Controller, domain models)
│   ├── Models/                   # Campaign, Task, Lead, AuditLog entities
│   ├── Services/                 # Controller, AgentOrchestrator, validation
│   ├── Invariants/               # 5 NON-NEGOTIABLE invariant enforcers
│   └── Infrastructure/           # Supabase client, Azure Key Vault, Application Insights
├── OutreachAgent.Agent/          # LLM agent integration (stateless proposal generator)
│   ├── Contracts/                # IAgentProposer, ActionProposal DTOs
│   └── Providers/                # OpenAI, Anthropic, or local LLM providers
├── OutreachAgent.MCP/            # MCP server integrations
│   ├── Playwright/               # LinkedIn automation via Playwright MCP
│   ├── DesktopCommander/         # File system operations
│   └── Common/                   # MCP client abstractions
├── OutreachAgent.UI/             # .NET MAUI Blazor Hybrid app
│   ├── Components/               # Razor components (Chat, Settings, LeadList)
│   ├── Pages/                    # Main pages (ChatPage, SettingsPage, AnalyticsPage)
│   ├── Services/                 # UI-specific services (state management)
│   └── wwwroot/                  # CSS, images, static assets
└── OutreachAgent.Tests/
    ├── Unit/                     # Unit tests for Controller, services, invariants
    ├── Integration/              # Integration tests (Supabase, MCP, Azure)
    └── UI/                       # Playwright UI automation tests

# Root-level configuration
Directory.Build.props             # Shared build properties, analyzers
Directory.Packages.props          # Centralized package version management
.editorconfig                     # Code style enforcement
OutreachAgent.sln                 # Solution file
```

**Structure Decision**: Selected **hybrid desktop application** structure with separation of concerns:
- **Core**: Platform-agnostic business logic (Controller, invariants, entities)
- **Agent**: Stateless LLM integration (pluggable providers)
- **MCP**: External tool integrations via Model Context Protocol
- **UI**: .NET MAUI Blazor Hybrid (desktop-first, Razor components)
- **Tests**: Organized by test type for independent execution

This structure supports:
- Desktop-first principle (MAUI + Blazor optimized for Windows)
- Blazor components default (all UI in Razor, minimal XAML)
- Test-first workflow (tests grouped by component for TDD)
- Code reusability (Core logic shared, platform adapters isolated)

## Complexity Tracking

*No constitution violations requiring justification*

All architectural decisions align with constitution principles:
- Desktop-first with Blazor Hybrid (Principle I, II)
- Centralized build/packages (Principle IV)
- Performance-first design with measurable targets (Principle V)
- TDD workflow with user story organization (Principle VI, VII)

---

## Phase 0: Outline & Research

### Research Tasks

1. **.NET MAUI Blazor Hybrid Architecture Patterns**
   - Best practices for Controller-Agent separation in MAUI apps
   - BlazorWebView optimization for desktop performance
   - State management without Blazor's built-in state (DB-first)
   - Background task execution without freezing UI

2. **Supabase PostgreSQL Integration**
   - Supabase SDK usage patterns for .NET
   - Connection pooling and retry strategies
   - Real-time subscriptions for audit log streaming (optional)
   - Schema migration strategies for desktop apps

3. **Azure Integration Best Practices**
   - Azure Key Vault SDK for .NET (Azure.Security.KeyVault.Secrets)
   - Managed Identity authentication from desktop apps (device code flow)
   - Application Insights SDK (Azure.Monitor.OpenTelemetry.AspNetCore adapted for MAUI)
   - Distributed tracing setup for MCP tool calls

4. **Model Context Protocol (MCP) Integration**
   - MCP client SDK for .NET (if available, else HTTP/JSON-RPC)
   - Process lifecycle management (start/stop embedded MCP servers)
   - Playwright MCP server integration patterns
   - Desktop-Commander MCP server for file system access

5. **LinkedIn Automation with Playwright**
   - Playwright.NET automation patterns for LinkedIn
   - Anti-detection techniques (variable delays, human-like behavior)
   - CAPTCHA detection and graceful degradation
   - Session management and cookie persistence

6. **LLM Agent Integration Patterns**
   - Structured output enforcement (JSON schema validation)
   - Prompt engineering for stateless one-action proposals
   - Token management for large state snapshots
   - Provider abstraction (OpenAI, Anthropic, local models)

### Research Output: research.md

*This document will resolve all "NEEDS CLARIFICATION" items from Technical Context and provide decision rationale for each technology choice with alternatives considered.*

---

## Phase 1: Design & Contracts

### 1.1 Data Model (data-model.md)

**Entities to Define**:

- **Campaign**
  - Fields: campaign_id (UUID), name, description, target_audience_description, working_directory_path, status (enum: active/paused/completed), created_at, updated_at
  - Relationships: 1:N Tasks, 1:N Leads, 1:N AuditLogs, 1:N EnvironmentSecrets
  - Validation: name required, target_audience_description min 10 chars, working_directory_path must exist and be writable
  - Indexes: campaign_id (PK), status, created_at

- **Task**
  - Fields: task_id (UUID), campaign_id (FK), description, status (enum: pending/in_progress/completed/failed), dependencies (UUID[]), priority_order (int), created_at, updated_at, completed_at (nullable)
  - Relationships: N:1 Campaign, 1:N AuditLogs
  - Validation: description required, status transitions enforced (pending→in_progress→completed/failed), dependencies must reference existing tasks
  - Indexes: task_id (PK), campaign_id, status, priority_order
  - State Transitions: pending→in_progress (Controller assigns), in_progress→completed (only after side effect verification), in_progress→failed (on error), failed→pending (retry)

- **Lead**
  - Fields: lead_id (UUID), campaign_id (FK), linkedin_profile_url (unique per campaign), name, headline, company, location, profile_notes (text), priority_score (float 0-100), prioritization_reasoning (text), contacted_at (nullable timestamp), connection_status (enum: none/pending/accepted/declined), created_at, updated_at
  - Relationships: N:1 Campaign, 1:N OutreachMessages
  - Validation: linkedin_profile_url unique per campaign, priority_score 0-100 range, name required
  - Indexes: lead_id (PK), campaign_id, linkedin_profile_url (unique per campaign), priority_score DESC, created_at

- **OutreachMessage**
  - Fields: message_id (UUID), lead_id (FK), campaign_id (FK), message_type (enum: connection_request/follow_up/inmail), message_text (text), language (default: 'uk'), generated_at, sent_at (nullable), status (enum: draft/sent/failed)
  - Relationships: N:1 Lead, N:1 Campaign
  - Validation: message_text required, connection_request max 300 chars, follow_up max 1000 chars, language ISO code
  - Indexes: message_id (PK), lead_id, campaign_id, status, generated_at

- **AuditLog**
  - Fields: log_id (UUID), campaign_id (FK), task_id (nullable FK), timestamp, action_type (enum: task_created/task_updated/tool_executed/db_write/error), parameters (JSONB), status (enum: initiated/completed/failed), result_details (JSONB), created_at
  - Relationships: N:1 Campaign, N:1 Task (optional)
  - Validation: timestamp required, action_type from enum, status transitions (initiated→completed/failed)
  - Indexes: log_id (PK), campaign_id, task_id, timestamp DESC, action_type, status
  - **INVARIANT ENFORCEMENT**: Log entry MUST be created with status='initiated' BEFORE side effect execution

- **EnvironmentSecret**
  - Fields: secret_id (UUID), campaign_id (FK), secret_key (varchar 100), secret_value_encrypted (text - Azure Key Vault reference), created_at, updated_at
  - Relationships: N:1 Campaign
  - Validation: secret_key unique per campaign, secret_value_encrypted is Azure Key Vault secret URI, not actual secret
  - Indexes: secret_id (PK), campaign_id, secret_key (unique per campaign)
  - **SECURITY**: Never store actual secrets in database - only Key Vault secret URIs

### 1.2 API Contracts (contracts/)

**controller-agent-contract.md**: Defines the Controller→Agent and Agent→Controller interface

- **Controller→Agent Payload** (state snapshot):
  ```json
  {
    "campaign": { campaign_context },
    "tasks": [ { task with status } ],
    "leads": [ { recent leads with priority } ],
    "audit_logs": [ { last 50 entries } ],
    "available_tools": ["playwright", "desktop-commander", "fetch", "exa"],
    "constraints": { rate_limits, session_duration }
  }
  ```

- **Agent→Controller Proposal** (structured output):
  ```json
  {
    "action_type": "execute_task | create_task | pause | error",
    "task_id": "uuid or null",
    "tool": "playwright | desktop-commander | fetch | exa | none",
    "parameters": { tool_specific_params },
    "reasoning": "explanation for human"
  }
  ```

**mcp-playwright-contract.md**: Playwright MCP server API

- Actions: login, search, view_profile, send_connection_request, send_message
- Rate limiting: 30-90s variable delay between connections, 5-15s between views
- Session management: Max 2 hours active, then cooling period
- Error codes: CAPTCHA_DETECTED, RATE_LIMITED, AUTH_FAILED, NETWORK_ERROR

**mcp-desktop-commander-contract.md**: File system MCP server API

- Actions: read_file, write_file, list_directory, create_directory
- Constraints: Restricted to campaign working_directory_path only
- File size limits: 10MB max upload
- Security: No access outside working directory

**supabase-schema.sql**: Complete PostgreSQL schema DDL

- CREATE TABLE statements for all 6 entities
- Indexes, foreign keys, constraints
- Enum types (status, action_type, etc.)
- Triggers for updated_at timestamps
- RLS policies (if using Supabase Row Level Security)

### 1.3 Quickstart Guide (quickstart.md)

**Development Environment Setup**:
1. Prerequisites: .NET 9 SDK, Visual Studio 2022 17.8+, Supabase CLI, Azure CLI
2. Clone repository and checkout feature branch
3. Create `Directory.Build.props` with analyzers (StyleCop, SonarAnalyzer)
4. Create `Directory.Packages.props` with version management
5. Create `.editorconfig` for code style
6. Run `dotnet restore` and verify build with `TreatWarningsAsErrors=true`
7. Set up Supabase database: `supabase init` and run schema migrations
8. Configure Azure Key Vault: Create vault, store LinkedIn credentials
9. Configure Application Insights: Create resource, copy instrumentation key
10. Install MCP servers: Bundle Playwright and Desktop-Commander executables

**First Feature Implementation (TDD)**:
1. Select User Story 1 (Campaign Creation)
2. Write failing integration test: CreateCampaign_ValidInput_StoresInDatabase
3. Implement Controller.CreateCampaignAsync() method
4. Run test → Green
5. Refactor and verify analyzers pass
6. Commit with constitution compliance note

### 1.4 Update Agent Context

Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot` to update Copilot instructions with:
- .NET 9 MAUI Blazor Hybrid as primary technology
- Supabase PostgreSQL for data persistence
- Azure Key Vault and Application Insights integration
- Model Context Protocol for tool integrations
- TDD workflow with xUnit
- Desktop-first UI optimization patterns

---

## Post-Design Constitution Check

*Re-evaluation after Phase 1 artifacts complete*

- [x] **Data Model** validates desktop-first (SQLite option removed, Supabase primary for cross-device sync)
- [x] **Contracts** enforce invariants (audit log before side effects in controller-agent-contract.md, task status verification in data-model.md state transitions)
- [x] **Quickstart** demonstrates TDD workflow (write test first, implement, refactor) and analyzer configuration (Directory.Build.props setup step)

**POST-DESIGN GATE STATUS**: ✅ **PASS** - Design artifacts comply with all constitution principles

---

## Next Steps

This plan is now ready for task generation. Run:

```bash
/speckit.tasks
```

This will break down implementation into:
- Phase 1: Setup (Directory.Build.props, Directory.Packages.props, .editorconfig)
- Phase 2: Foundational (Supabase schema, Azure setup, MCP server bundling)
- Phase 3+: User stories (6 P1 stories, 4 P2 stories) - each with tests-first approach

**Critical Path Dependencies**:
1. Foundation (DB, Azure, MCP) must complete before ANY user story
2. User Story 1 (Campaign Creation) → User Story 2 (State Recovery) → User Story 3 (Task Verification)
3. User Stories 4-7 (LinkedIn features) can proceed in parallel after Story 3 complete
4. User Stories 8-10 (UI/Audit) can develop concurrently with LinkedIn features

**Estimated Complexity**: ~8-10 weeks for P1 stories (6 stories × 1.5 weeks avg), P2 stories +4-6 weeks
