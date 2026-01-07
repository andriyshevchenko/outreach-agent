<!--
Sync Impact Report - Constitution v1.0.0
========================================
Version Change: INITIAL → 1.0.0
Created: 2026-01-07
Type: Initial constitution for .NET MAUI Blazor Hybrid desktop-first application

New Sections:
- Core Principles (7 principles covering architecture, quality, and performance)
- Technology Stack & Constraints
- Quality Gates & Standards
- Governance

Templates Status:
✅ spec-template.md - Aligns with user story prioritization and testability principles
✅ plan-template.md - Supports constitution check workflow and architecture validation
✅ tasks-template.md - Enforces test-first approach and user story organization
⚠ All templates validated for consistency with constitution principles

Follow-up Actions:
- Review constitution with team for any technology-specific adjustments
- Ensure development environment follows EditorConfig and analyzer configuration
- Configure CI/CD pipeline with quality gates matching these standards
-->

# Outreach Agent Constitution
*Desktop-First .NET MAUI Blazor Hybrid Application*

## Core Principles

### I. Desktop-First, Mobile-Aware Architecture

All features MUST be designed and optimized for Windows desktop as the primary platform, with mobile (iOS/Android) and macOS as secondary targets.

**Rules**:
- UI/UX design begins with desktop workflows and screen sizes (1920x1080+ minimum)
- Desktop-specific features (keyboard shortcuts, multi-window support, file system access) are first-class
- Mobile adaptations are created AFTER desktop implementation is validated
- BlazorWebView performance optimizations prioritize desktop (Windows WebView2)
- Native platform features used only when Blazor components cannot meet desktop UX requirements

**Rationale**: Desktop users demand high productivity and efficiency. Mobile-first compromises often sacrifice desktop usability. By prioritizing desktop, we ensure the core user experience is optimal for the primary use case while enabling graceful mobile adaptation.

### II. Hybrid Component Strategy (NON-NEGOTIABLE)

Razor components are the default UI technology. Native MAUI controls MUST only be used when Blazor cannot meet specific requirements.

**Rules**:
- ALL UI starts as Razor components (.razor files) in shared RCL projects
- Native MAUI controls (XAML) permitted ONLY for: device-specific APIs, performance-critical rendering, or platform-specific UI patterns
- Every native control usage MUST be documented with justification in component header
- Prefer C# interop to JavaScript libraries for consistency across web and native
- Component reusability across platforms is mandatory - no platform-specific duplicates without explicit approval

**Rationale**: Blazor enables maximum code sharing across platforms and web. Native controls fragment the codebase and create maintenance overhead. This principle ensures we only use native when absolutely necessary.

### III. Static Code Analysis & Quality Enforcement (NON-NEGOTIABLE)

Code quality tools are mandatory and ALL warnings MUST be treated as errors. No code bypasses these checks.

**Rules**:
- **Required Analyzers**: StyleCop.Analyzers + SonarAnalyzer.CSharp configured via Directory.Build.props
- **TreatWarningsAsErrors**: true (no exceptions)
- **EditorConfig**: Solution-level .editorconfig enforces consistent code style
- Severity customization allowed ONLY when documented in constitution or team-approved
- Build MUST fail on analyzer violations - no suppressions without architectural review
- CI/CD pipeline MUST enforce same analyzer rules as local builds

**Rationale**: As demonstrated in the attached transcripts, static code analysis catches bugs early, enforces consistency, and improves team productivity. Treating warnings as errors ensures code quality cannot be ignored or deferred.

### IV. Centralized Build & Package Management (NON-NEGOTIABLE)

All build configuration and NuGet package versions MUST be managed centrally to ensure consistency.

**Rules**:
- **Directory.Build.props**: Required at solution root - defines TargetFramework, EnableImplicitUsings, Nullable, analyzer packages
- **Directory.Packages.props**: Required at solution root - manages all package versions centrally (ManagePackageVersionsCentrally=true)
- Project files (.csproj) contain ONLY PackageReference without version attributes
- Package version updates happen in ONE place (Directory.Packages.props)
- Custom MSBuild properties and shared build logic live in Directory.Build.props
- NO project-specific build overrides without architectural justification

**Rationale**: Following the best practices from the attached transcripts, centralized management prevents version conflicts, simplifies updates, and reduces maintenance overhead across multiple projects.

### V. Performance-First Development

All features MUST meet desktop application performance standards from day one.

**Rules**:
- **Startup time**: < 3 seconds from launch to interactive UI on standard desktop hardware
- **UI responsiveness**: All interactions < 100ms response time, heavy operations use background tasks
- **Memory usage**: < 500MB baseline, < 1GB under typical workload
- **Asset optimization**: Lazy loading for components, code splitting for large modules, compressed images/fonts
- **WebView optimization**: Minimize Blazor render cycles, use virtualization for lists/grids, implement efficient caching
- Performance profiling required for any feature adding > 50ms to startup or > 100MB to memory

**Rationale**: Desktop users expect instant responsiveness. Poor performance directly impacts productivity and user satisfaction. These standards ensure a native-like experience.

### VI. Test-First Development (TDD Required)

Tests MUST be written before implementation code. No feature is complete without passing tests.

**Rules**:
- **Workflow**: Write test → User/stakeholder approval → Verify test fails (Red) → Implement (Green) → Refactor
- **Coverage Requirements**: Unit tests for all business logic, integration tests for platform interactions, UI tests for critical user flows
- **Test Organization**: Tests grouped by user story to enable independent validation
- **Continuous Testing**: Tests run on every commit via CI/CD pipeline
- Tests MUST be independently executable - no shared state or test order dependencies
- Manual testing allowed only for visual/UX validation after automated tests pass

**Rationale**: TDD ensures features meet requirements before implementation begins, catches regressions early, and enables confident refactoring. Required for maintaining quality in a hybrid codebase.

### VII. Incremental Delivery via User Story Prioritization

All features MUST be decomposed into prioritized, independently deliverable user stories.

**Rules**:
- Every feature specification includes user stories with explicit priorities (P1, P2, P3...)
- Each user story MUST be independently testable and deliverable as an MVP increment
- P1 stories represent core value - must be fully functional without P2/P3
- Implementation proceeds story-by-story, with each story reaching "done" before starting next
- "Done" definition: tests pass, code reviewed, functionality demonstrated, documentation updated
- No parallel work on multiple stories for same developer unless stories are truly independent

**Rationale**: This enables early validation, faster feedback cycles, and ability to pivot without wasted work. Users get value sooner with partial delivery rather than waiting for complete feature.

## Technology Stack & Constraints

### Required Technologies

- **.NET Version**: .NET 9.0 or later (leveraging latest performance improvements)
- **Framework**: .NET MAUI with Blazor Hybrid (BlazorWebView)
- **UI Layer**: Blazor components (Razor syntax, C#) - prefer over XAML
- **Styling**: Modern CSS with CSS isolation, FluentUI Blazor or MudBlazor component library
- **State Management**: Cascading parameters, dependency injection, local storage for persistence
- **Data Access**: Entity Framework Core for structured data, SQLite for local storage
- **HTTP Client**: HttpClient with Polly for resilience and retry policies
- **Testing**: xUnit or NUnit for unit tests, Playwright or Selenium for UI automation

### Platform Targets

- **Primary**: Windows 10/11 (x64, ARM64)
- **Secondary**: iOS 15+, Android 10+, macOS 11+
- **Web** (optional): Blazor WebAssembly or Server for companion web experience

### Performance Constraints

- **Startup**: < 3 seconds cold start
- **Memory**: < 500MB baseline, < 1GB working set
- **UI Frame Rate**: 60 FPS for animations and transitions
- **API Response**: < 200ms for local operations, < 2s for network requests (with loading indicators)
- **Package Size**: < 150MB installer for Windows desktop

### Security Requirements

- Data encryption at rest (Windows Data Protection API for desktop)
- Secure credential storage (Windows Credential Manager, platform keychains)
- HTTPS-only for all network communication
- Regular dependency updates for security patches

## Quality Gates & Standards

### Code Quality Gates

All code MUST pass these checks before merge:

1. **Build**: Solution builds successfully with TreatWarningsAsErrors=true
2. **Analyzers**: StyleCop + SonarAnalyzer pass with zero violations (customized severity via .editorconfig)
3. **Tests**: All automated tests pass (unit, integration, UI)
4. **Code Review**: Approved by at least one team member with constitution compliance verification
5. **Performance**: No regression in startup time, memory usage, or critical path performance

### Constitution Compliance Checklist

Every plan.md MUST include "Constitution Check" section validating:

- [ ] Desktop-first design validated
- [ ] Blazor components used by default (native MAUI controls justified if used)
- [ ] Static code analyzers configured (Directory.Build.props present)
- [ ] Centralized package management (Directory.Packages.props present)
- [ ] Performance targets specified and measurable
- [ ] Test-first workflow planned (tests written before implementation)
- [ ] User stories prioritized and independently testable

### Branching & Development Workflow

- Feature branches: `###-feature-name` format
- All features documented in `/specs/[###-feature-name]/` with spec.md, plan.md, tasks.md
- No direct commits to main branch
- CI/CD pipeline enforces all quality gates

## Governance

This constitution supersedes all other coding practices, style guides, or workflow documents within the Outreach Agent project.

### Amendment Process

1. Proposed changes require written rationale documenting problem, solution, and impact analysis
2. Team review and approval required (majority vote for minor amendments, unanimous for NON-NEGOTIABLE principles)
3. Version bump following semantic versioning:
   - **MAJOR**: Removing or fundamentally changing NON-NEGOTIABLE principles (requires unanimous approval)
   - **MINOR**: Adding new principles, expanding guidance, new technology adoption
   - **PATCH**: Clarifications, wording improvements, fixing contradictions
4. Updated constitution published to all team members
5. Affected templates and documentation updated to maintain consistency

### Compliance & Review

- All PRs/commits MUST be reviewed for constitution compliance
- Violations require justification and architect approval
- Regular constitution reviews (quarterly) to ensure relevance and effectiveness
- Complexity that violates principles MUST be justified in plan.md "Complexity Tracking" section

### Continuous Improvement

Team members are encouraged to propose constitution improvements based on:
- New technology capabilities (e.g., .NET version upgrades)
- Lessons learned from completed features
- Performance insights from production monitoring
- User feedback on development velocity and quality

**Version**: 1.0.0 | **Ratified**: 2026-01-07 | **Last Amended**: 2026-01-07
