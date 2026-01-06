<!--
  SYNC IMPACT REPORT
  Version: 1.0.0 → 1.0.2
  Date: 2026-01-06
  
  Changes (PATCH):
  - Locked to ONLY latest versions (no alternatives)
  - C# 14 only (with .NET 10 LTS)
  - .NET 10 LTS only (support until November 2028)
  - MAUI target: net10.0-maui only
  - Testing: xUnit and Playwright (latest stable, no alternatives listed)
  - Removed version optionality to enforce consistency
  
  Templates Requiring Updates:
  ✅ Updated: plan-template.md (Constitution Check section references current principles)
  ✅ Updated: spec-template.md (Requirements align with deterministic state & testing principles)
  ✅ Updated: tasks-template.md (Task categorization reflects quality gates & testing discipline)
  
  Rationale for Version 1.0.2 (PATCH):
  - Enforced latest-only versions (removed alternatives)
  - Single source of truth for technology choices
  - Simplified decision-making for developers
  
  Previous Changes (v1.0.1 - 2026-01-06):
  - Updated .NET Technology Standards section with latest versions
  - C# 12+ → C# 13/14 (with .NET 9/10)
  - .NET 8/9 → .NET 9 (STS) or .NET 10 (LTS recommended)
  - Added specific MAUI target framework monikers
  - Updated Blazor Hybrid requirements (Android 7.0+/iOS 14+ for WebView)
  - Clarified testing frameworks: xUnit recommended, Playwright over Selenium
  - Added specific MAUI platform requirements (Windows 11+, macOS 12+)
  
  Previous Changes (v1.0.0 - 2026-01-06):
  - Initial constitution ratification
  - Established 7 core principles: Controller Authority, Deterministic State, Code Quality, 
    Centralized Configuration, Containerization, Observability, Test-First
  - Added .NET Technology Standards section
  - Added Development Workflow section
-->

# Outreach Agent Constitution

## Core Principles

### I. Controller Authority (NON-NEGOTIABLE)

The Controller is the single source of truth for all application state and side effects.

**MUST Requirements**:
- All state mutations MUST go through the Controller (.NET layer)
- LLM Agent MUST NOT mutate state directly - it proposes, Controller executes
- UI MUST NOT bypass Controller for any state changes or tool execution
- Database writes MUST originate only from Controller
- Logging, invariant enforcement, and task completion decisions MUST be Controller-owned

**Rationale**: Eliminates context loss as a correctness risk. The system remains correct even if conversation history is truncated, LLM forgets prior turns, or application restarts mid-campaign.

### II. Deterministic State-First Design

The system MUST remain correct without relying on LLM memory or conversation history.

**MUST Requirements**:
- Agent receives state snapshot per cycle - never assumes memory of prior cycles
- All execution must be resumable from persisted state
- Campaign progress MUST be recoverable after application restart
- State snapshots MUST be complete and self-contained
- No implicit dependencies on conversation context

**Rationale**: LLM is stateless and replaceable. System correctness cannot depend on LLM reliability.

### III. Code Quality Through Static Analysis (NON-NEGOTIABLE)

All code MUST pass static code analysis checks before merge.

**MUST Requirements**:
- `TreatWarningsAsErrors` MUST be enabled in all .NET projects
- SonarAnalyzer.CSharp MUST be installed across all projects (via Directory.Build.props)
- StyleCop.Analyzers MUST be installed for consistent C# code style
- EditorConfig MUST define and enforce project-wide code style rules
- Build MUST fail if any analyzer rule is violated (unless explicitly suppressed with justification)
- CI/CD pipelines MUST enforce code quality gates

**Rationale**: Ensures consistent, maintainable, high-quality codebase across team. Shortens feedback loop by catching issues early in development cycle.

### IV. Centralized Configuration Management

Build configuration and package versions MUST be centralized for consistency.

**MUST Requirements**:
- `Directory.Build.props` MUST define shared build properties (target framework, nullable reference types, implicit usings)
- `Directory.Packages.props` MUST manage NuGet package versions centrally
- Individual projects MUST reference packages without specifying versions
- Cross-cutting packages (analyzers, logging, etc.) MUST be defined once in Directory.Build.props
- All projects MUST inherit from centralized configuration

**Rationale**: Simplifies dependency management, ensures version consistency across solution, reduces configuration drift.

### V. Containerization & Deployment Support

Application MUST support containerized deployment for consistency across environments.

**MUST Requirements**:
- Dockerfile MUST be maintained for the MAUI application when deployment requires it
- Docker Compose MUST be available for orchestrating app + external services (database, etc.)
- Multi-stage builds MUST be used to optimize image size
- Container health checks MUST be implemented for production readiness
- Environment-specific configuration MUST be externalized (not hardcoded in images)

**Rationale**: Ensures consistent deployment across development, staging, and production. Enables orchestration of app with dependencies.

### VI. Observability & Structured Logging

All critical operations MUST be observable and auditable.

**MUST Requirements**:
- Structured logging MUST be used (ILogger with semantic logging)
- All Controller actions MUST be logged with context (campaign ID, lead ID, action type)
- Tool executions (browser automation, API calls) MUST be logged with input/output
- Agent proposals and Controller decisions MUST be logged separately
- Log levels MUST be used appropriately (Debug, Information, Warning, Error, Critical)
- Logs MUST support correlation across operations (use correlation IDs)

**Rationale**: Enables debugging, audit trails, and system health monitoring. Critical for deterministic system verification.

### VII. Test-First for Critical Paths

Critical functionality MUST have tests written before implementation.

**MUST Requirements**:
- Test creation → Test approval → Test failure → Implementation (Red-Green-Refactor)
- Contract tests MUST exist for Controller ↔ Agent interface
- Integration tests MUST exist for Controller ↔ Database operations
- Unit tests MUST exist for state validation and invariant enforcement
- Browser automation scenarios MUST have integration tests
- Tests MUST be executable independently and in parallel where possible

**Rationale**: Validates deterministic behavior. Ensures Controller logic correctness. Prevents regressions in state management.

## .NET Technology Standards

**Language**: C# 14  
**Runtime**: .NET 10 (LTS, support until November 2028)  
**Framework**: .NET MAUI (net10.0-maui) for desktop-first cross-platform (Windows 11+, macOS 12+)  
**UI Layer**: Blazor Hybrid (HTML/CSS-based components, requires Android 7.0+/iOS 14+ for web view)  
**Database**: Supabase (PostgreSQL via PostgREST API)  
**Testing Frameworks**: 
- Unit tests: xUnit (latest stable)
- Browser automation: Playwright for .NET (latest stable)
- Integration: xUnit with TestHost
**Dependency Injection**: Microsoft.Extensions.DependencyInjection (built-in MAUI DI)  
**Logging**: Microsoft.Extensions.Logging (ILogger<T>)  
**Configuration**: Microsoft.Extensions.Configuration (appsettings.json, environment variables)

**Performance Targets**:
- UI responsiveness: < 100ms for user actions
- State persistence: < 500ms for database writes
- Campaign step execution: < 30s per lead action (depending on LLM latency)

**Constraints**:
- Desktop-first design (mobile is secondary)
- Offline capability NOT required (requires internet for LLM and Supabase)
- Single-user desktop application (no multi-tenancy in MVP)

## Development Workflow

### Code Quality Gates

1. **Pre-commit**: EditorConfig auto-formatting enforced in IDE
2. **Build**: Static analyzers MUST pass (SonarAnalyzer, StyleCop)
3. **Pre-merge**: All tests MUST pass (unit + integration)
4. **CI Pipeline**: GitHub Actions MUST run full build + test + publish steps

### Constitution Compliance Review

- All feature specs (spec.md) MUST reference applicable principles in Requirements section
- All implementation plans (plan.md) MUST include Constitution Check section
- Task lists (tasks.md) MUST categorize tasks by principle-driven requirements (e.g., testing, logging, state management)
- Code reviews MUST verify adherence to Controller Authority and Code Quality principles

### Amendment Process

- Amendments MUST be proposed via pull request to constitution.md
- Amendments MUST include version bump reasoning (MAJOR/MINOR/PATCH per semantic versioning)
- Amendments MUST include Sync Impact Report listing affected templates/docs
- Amendments MUST be approved before merging

**Version Bump Rules**:
- **MAJOR**: Backward-incompatible changes (removing principles, redefining core requirements)
- **MINOR**: New principles added, materially expanded guidance
- **PATCH**: Clarifications, wording fixes, non-semantic refinements

## Governance

This constitution supersedes all other practices. When conflicts arise between this document and other guidance, the constitution takes precedence.

**Enforcement**:
- Directory.Build.props MUST enforce code quality settings
- CI/CD pipelines MUST enforce build and test gates
- Code reviews MUST verify compliance with principles
- Violations MUST be justified or remediated before merge

**Complexity Justification**:
- Any deviation from principles MUST be documented in plan.md "Complexity Tracking" section
- Justifications MUST include rationale, alternatives considered, and mitigation plan
- Technical debt introduced MUST be tracked and addressed in future iterations

**Runtime Development Guidance**:
- For detailed implementation patterns, refer to product vision documents in `product vision/`
- For agent-specific behavior, refer to `product vision/agent_specification_deterministic_desktop_outreach_agent.md`
- For end-to-end workflows, refer to `product vision/agent_e2e.cs`

**Version**: 1.0.2 | **Ratified**: 2026-01-06 | **Last Amended**: 2026-01-06
