# Implementation Plan: Deterministic Desktop Outreach Agent

**Branch**: `001-deterministic-agent-mvp` | **Date**: 2026-01-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-deterministic-agent-mvp/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build deterministic desktop outreach agent that eliminates context loss through state-first architecture. The Controller owns all state mutations and tool execution; the LLM Agent proposes actions only. Campaign progress persists to Supabase PostgreSQL database after each action, enabling stop/continue workflows and application restart without data loss. Execution follows mandatory 8-step loop (Load State → Select Task → Invoke Agent → Validate Proposal → Execute Action → Persist Logs → Update State → Repeat). UI displays campaign list for resumption via new chat sessions without conversation history dependency.

**Core Value Proposition**: User can stop agent mid-campaign, close application, start new chat session, and resume from exact position with zero context loss (SC-001, SC-003, SC-012).

## Technical Context

**Language/Version**: C# 14 with .NET 10 LTS (support until November 2028)  
**Primary Dependencies**: 
- .NET MAUI (net10.0-maui) for desktop-first cross-platform (Windows 11+, macOS 12+)
- Blazor Hybrid for UI (HTML/CSS components, **reuses existing React prototype UI from https://github.com/andriyshevchenko/outreachgenie-app**)
- Supabase .NET SDK for PostgreSQL persistence (PostgREST API)
- OpenAI .NET SDK for LLM provider (GPT-4 default, generic interface for alternatives)
- Playwright for .NET for LinkedIn browser automation (anti-detection, cross-platform)
- RestrictedPython for sandboxed Python script execution (called via Process)

**Storage**: Supabase PostgreSQL (8 entities: Campaign, Task, Lead, Artifact, AuditLog, EventLog, Configuration, ExecutionState)  
**Testing**: xUnit (unit/integration), Playwright (browser automation E2E)  
**Target Platform**: Desktop application (Windows 11+, macOS 12+; mobile out of scope for MVP)  
**Project Type**: Desktop application (MAUI Blazor Hybrid)

**Performance Goals**:
- UI responsiveness: <100ms for user actions
- State persistence: <500ms for database writes after each Controller action
- Campaign resume: <5 seconds from UI click to agent execution (SC-001)
- Database queries: <2 seconds for responsive UI
- Python scripts: <60 seconds max execution time

**Constraints**:
- Must work offline for reading persisted state (sync required for new actions)
- Must not store sensitive data unencrypted (FR-023: encrypted in database)
- Must comply with LinkedIn terms of service (rate limiting, user consent)
- Must handle network interruptions gracefully
- Desktop-first (no multi-user, no mobile for MVP)

**Scale/Scope**:
- Single user per installation (multi-user auth out of scope)
- 100+ leads per campaign (SC-002: process 100 without conversation history bloat)
- Campaign list UI with status indicators (FR-045)
- 8-step deterministic execution loop (FR-005a through FR-005h)
- 8 finite action types (FR-005i through FR-005p)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Phase 0 Check** (2026-01-06 10:00 UTC):
- [x] **Controller Authority**: ✅ COMPLIANT - FR-001, FR-004, FR-060 enforce Controller as single source of truth. UI cannot change campaign state directly. Agent proposes, Controller executes.
- [x] **Deterministic State**: ✅ COMPLIANT - FR-002, FR-009, FR-013 require state reload every cycle, resume from persisted state, no LLM memory dependency (SC-012: delete chat history does not affect execution).
- [x] **Code Quality**: ✅ COMPLIANT - Plan Phase 1 will configure Directory.Build.props with TreatWarningsAsErrors, SonarAnalyzer.CSharp, StyleCop.Analyzers per Constitution III.
- [x] **Centralized Config**: ✅ COMPLIANT - Plan Phase 1 will create Directory.Build.props and Directory.Packages.props for centralized NuGet management per Constitution IV.
- [x] **Containerization**: ⚠️ DEFERRED - Docker not required for desktop MAUI app. May add in future for Supabase local dev environment. Not blocking for MVP.
- [x] **Observability**: ✅ COMPLIANT - FR-006, FR-048, FR-051 require audit logging for every tool execution. Controller actions logged with ILogger<T>. AuditLog and EventLog entities defined.
- [x] **Test-First**: ✅ COMPLIANT - User Story acceptance scenarios map to integration tests. FR-006 through FR-010 (Controller invariants) require contract tests. Plan Phase 1 will identify test scaffolding.

**Phase 1 Check** (2026-01-06 - POST-DESIGN):
- [x] **Controller Authority**: ✅ VERIFIED - CampaignController.cs orchestrates 8-step loop. StateManager.cs owns state load/persist. ToolExecutor.cs mediates all side effects.
- [x] **Deterministic State**: ✅ VERIFIED - StateSnapshot contract (agent-contract.md) bounded to 1.1k tokens/cycle. ExecutionState entity tracks current position. EventLog enables replay.
- [x] **Code Quality**: ✅ VERIFIED - Directory.Build.props + Directory.Packages.props patterns defined in project structure. GitHub Copilot instructions created with analyzer requirements.
- [x] **Centralized Config**: ✅ VERIFIED - Solution structure shows centralized config files at root. All projects inherit settings.
- [x] **Containerization**: ⚠️ DEFERRED - Unchanged from Phase 0. Not blocking MVP.
- [x] **Observability**: ✅ VERIFIED - AuditLogger.cs service defined. AuditLog entity logs every tool execution (FR-006). ILogger<T> structured logging in all services.
- [x] **Test-First**: ✅ VERIFIED - Test structure defined (Unit/Integration/E2E). Contract tests specified in agent-contract.md (ProposalValidator, AgentService). Quickstart includes test-first workflow.

**Violations**: None. All principles compliant or appropriately deferred.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
OutreachAgent.sln                        # Solution file
Directory.Build.props                    # Centralized build configuration
Directory.Packages.props                 # Centralized NuGet package versions
.editorconfig                            # Code style enforcement

src/
├── OutreachAgent.Desktop/               # MAUI Blazor Hybrid desktop app
│   ├── MauiProgram.cs                   # Application entry point, DI setup
│   ├── Components/                      # Blazor UI components
│   │   ├── Pages/                       # Campaign list, settings, chat UI
│   │   ├── Shared/                      # Layout, navigation
│   │   └── Controls/                    # Reusable UI widgets
│   ├── wwwroot/                         # Static assets (CSS, JS from shadcn/Tailwind)
│   ├── Services/                        # Service registration, platform APIs
│   └── OutreachAgent.Desktop.csproj
│
├── OutreachAgent.Core/                  # Controller + domain logic
│   ├── Controller/
│   │   ├── CampaignController.cs        # Execution loop orchestrator
│   │   ├── StateManager.cs              # State load/persist (FR-009)
│   │   ├── ProposalValidator.cs         # Action vocabulary validation (FR-005d)
│   │   └── InvariantEnforcer.cs         # FR-006 through FR-010 enforcement
│   ├── Models/
│   │   ├── Entities/                    # Campaign, Task, Lead, Artifact, etc.
│   │   ├── Proposals/                   # Agent proposal DTOs (8 action types)
│   │   └── State/                       # ExecutionState, CampaignState DTOs
│   ├── Services/
│   │   ├── AgentService.cs              # LLM invocation with state snapshots
│   │   ├── ToolExecutor.cs              # Mediated tool execution (FR-008)
│   │   └── AuditLogger.cs               # FR-006: mandatory audit logging
│   └── OutreachAgent.Core.csproj
│
├── OutreachAgent.Infrastructure/        # External integrations
│   ├── Database/
│   │   ├── SupabaseRepository.cs        # Generic CRUD for 8 entities
│   │   ├── Migrations/                  # Schema versioning (if needed)
│   │   └── EncryptionService.cs         # FR-023: encrypted configuration
│   ├── LLM/
│   │   ├── ILLMProvider.cs              # Generic interface (FR-025)
│   │   ├── OpenAIProvider.cs            # Default: GPT-4 implementation
│   │   └── PromptBuilder.cs             # State snapshot formatting for agent
│   ├── Automation/
│   │   ├── PlaywrightService.cs         # LinkedIn browser automation
│   │   └── PythonSandbox.cs             # RestrictedPython wrapper (FR-038)
│   └── OutreachAgent.Infrastructure.csproj
│
tests/
├── OutreachAgent.Core.Tests/            # Unit tests
│   ├── Controller/
│   │   ├── CampaignControllerTests.cs   # 8-step loop validation
│   │   ├── ProposalValidatorTests.cs    # FR-005i through FR-005p schemas
│   │   └── InvariantEnforcerTests.cs    # FR-006 through FR-010 contracts
│   └── OutreachAgent.Core.Tests.csproj
│
├── OutreachAgent.Integration.Tests/     # Integration tests
│   ├── Database/
│   │   └── SupabaseIntegrationTests.cs  # CRUD, state persistence (FR-002)
│   ├── EndToEnd/
│   │   ├── CampaignResumeTests.cs       # SC-001, SC-003: stop/continue
│   │   └── ContextLossPrevention.cs     # SC-012: delete chat history test
│   └── OutreachAgent.Integration.Tests.csproj
│
└── OutreachAgent.E2E.Tests/             # Playwright E2E tests
    ├── Scenarios/
    │   ├── LinkedInAutomationTests.cs   # Browser interaction validation
    │   └── UserStory1Tests.cs           # US1 acceptance scenarios
    └── OutreachAgent.E2E.Tests.csproj

.specify/                                # Specification framework (existing)
specs/001-deterministic-agent-mvp/       # This feature's docs
product vision/                          # Original specs (existing)
```

**Structure Decision**: 3-project solution follows .NET best practices:
1. **OutreachAgent.Desktop** (MAUI): UI layer only, minimal logic
2. **OutreachAgent.Core**: Controller + domain models (Constitution Principle I)
3. **OutreachAgent.Infrastructure**: Database, LLM, tools (dependency inversion)

Rationale: Separation enables unit testing Core without UI/infrastructure dependencies. MAUI project references Core + Infrastructure. Test projects reference Core directly.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

**No violations to justify.** All constitution principles compliant or appropriately deferred (containerization not required for desktop MVP).

---

## Phase 0: Research (COMPLETE)

**Date**: 2026-01-06  
**Artifact**: [research.md](research.md)

**Resolved**:
- ✅ Supabase .NET SDK patterns (supabase-csharp community SDK)
- ✅ OpenAI structured outputs (Function calling with JSON schema)
- ✅ Playwright anti-detection (Stealth args + realistic user-agent)
- ✅ RestrictedPython integration (Process invocation with wrapped script)
- ✅ MAUI Blazor + Tailwind (shadcn-blazor community port)
- ✅ State snapshot optimization (Bounded context: 1.1k tokens/cycle)

**Outcome**: All technical unknowns resolved. No blockers for Phase 1 design.

---

## Phase 1: Design & Contracts (COMPLETE)

**Date**: 2026-01-06  
**Artifacts**: 
- [data-model.md](data-model.md) - 8 entities with schemas, indexes, validation rules
- [contracts/agent-contract.md](contracts/agent-contract.md) - State snapshot + 8 proposal types
- [quickstart.md](quickstart.md) - 30-minute local setup guide
- [.github/agents/copilot-instructions.md](../../.github/agents/copilot-instructions.md) - GitHub Copilot context

**Designed**:
- ✅ **Database schema**: 8 entities (Campaign, Task, Lead, Artifact, AuditLog, EventLog, Configuration, ExecutionState) with 20 indexes
- ✅ **Controller ↔ Agent contract**: StateSnapshot request + 8 proposal response types (JSON schemas)
- ✅ **Project structure**: 3-project solution (Desktop, Core, Infrastructure) + 3 test projects
- ✅ **Agent context**: GitHub Copilot instructions with constitution principles, architecture, common tasks

**Constitution Re-Check**: ✅ All principles verified post-design (see Constitution Check section above)

---

## Planning Summary

### Documentation Generated
| Document | Purpose | Status |
|----------|---------|--------|
| plan.md | This file - technical context, structure, constitution check | ✅ Complete |
| research.md | Phase 0 technical decisions with alternatives | ✅ Complete |
| data-model.md | Database schema for 8 entities | ✅ Complete |
| contracts/agent-contract.md | Controller ↔ Agent JSON schemas | ✅ Complete |
| quickstart.md | 30-minute local development setup | ✅ Complete |
| .github/agents/copilot-instructions.md | GitHub Copilot agent context | ✅ Complete |

### Next Phase
**Command**: `/speckit.tasks` to generate dependency-ordered tasks.md for implementation

**Ready for Implementation**: ✅ Yes
- All technical unknowns resolved
- Database schema designed with migrations
- API contracts defined with validation rules
- Project structure established
- Test strategy specified
- Constitution compliance verified
