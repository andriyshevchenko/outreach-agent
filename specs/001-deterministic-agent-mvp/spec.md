# Feature Specification: Deterministic Desktop Outreach Agent

**Feature Branch**: `001-deterministic-agent-mvp`  
**Created**: 2026-01-06  
**Status**: Draft  
**Input**: User description: "Build deterministic desktop outreach agent that eliminates context loss through state-first architecture, with artifact persistence, lead prioritization, secure Python execution, and stop/continue capability"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Execute Outreach Campaign Without Context Loss (Priority: P1)

User launches a LinkedIn outreach campaign, and the agent processes leads sequentially. If the user stops the agent mid-campaign or the application restarts, the agent resumes from the exact point where it stopped without losing progress or todo list state.

**Why this priority**: This is the core value proposition - solving the critical context loss problem that makes current agent implementations unreliable. Without this, the entire solution fails to meet its primary objective.

**Independent Test**: Can be fully tested by starting a campaign with 5 leads, stopping after 2 leads are processed, restarting the application, and verifying the agent continues from lead #3 with full context of previous actions.

**Acceptance Scenarios**:

1. **Given** agent is processing lead #3 of 10, **When** user clicks Stop button, **Then** agent completes current action and persists state showing lead #3 in-progress
2. **Given** agent was stopped mid-campaign, **When** user sends new message "continue", **Then** agent loads persisted state and resumes from next pending action
3. **Given** application crashes during campaign execution, **When** user restarts application and opens campaign, **Then** agent displays current progress and can resume without re-processing completed leads
4. **Given** agent is awaiting user input for a decision, **When** user provides input, **Then** agent processes input and continues campaign without conversation history
5. **Given** campaign todo list has 15 items, **When** agent completes item #7, **Then** database reflects item #7 as complete and agent proceeds to item #8

---

### User Story 2 - Persist and Retrieve Artifacts (Priority: P1)

User asks agent to save job posting details, conversation context, or research findings as "artifacts". Later, user starts a new chat session and can reference those artifacts without providing the full conversation history.

**Why this priority**: Enables meaningful multi-session workflows without depending on message history. Critical for the deterministic architecture where conversation history is not the source of truth.

**Independent Test**: Can be tested by saving a job posting artifact in one session, closing the application, opening a new session, and successfully querying/using that artifact without any conversation context.

**Acceptance Scenarios**:

1. **Given** user says "save this job posting as artifact", **When** agent saves to database with schema metadata, **Then** artifact is stored with unique ID and retrievable schema
2. **Given** artifact exists in database, **When** user in new session says "show me the job posting from yesterday", **Then** agent queries artifacts by date/type and displays correct data
3. **Given** multiple artifacts exist, **When** user asks "list my saved artifacts", **Then** agent displays artifact names, types, and creation dates
4. **Given** user provides unstructured data, **When** agent creates artifact, **Then** schema is inferred and stored alongside data for future retrieval

---

### User Story 3 - Prioritize Leads Automatically (Priority: P2)

User imports a list of 100 LinkedIn leads. Agent analyzes each lead's profile data and prioritizes them based on relevance score (job title match, company size, recent activity, etc.). User reviews prioritized list and starts campaign with top-ranked leads first.

**Why this priority**: Improves campaign effectiveness by focusing on highest-value prospects first. Can be implemented independently once artifact storage (P1) is working.

**Independent Test**: Can be tested by importing 20 leads with varying attributes, running prioritization algorithm, and verifying leads are scored and ranked according to defined criteria (testable without actual outreach execution).

**Acceptance Scenarios**:

1. **Given** user imports CSV with 50 leads, **When** agent runs prioritization, **Then** each lead receives score 0-100 based on weighted criteria
2. **Given** prioritization completes, **When** user views lead list, **Then** leads display in descending score order with score explanation
3. **Given** user disagrees with ranking, **When** user manually adjusts lead priority, **Then** manual override persists and agent respects new order
4. **Given** new lead data becomes available, **When** agent re-runs prioritization, **Then** scores update and ranking adjusts accordingly

---

### User Story 4 - Execute Python Scripts Securely (Priority: P3)

User provides requirements for data analysis (e.g., "analyze lead response rates by industry"). Agent generates Python script to query Supabase and analyze data. Script runs in sandboxed environment with restricted permissions. Results are displayed to user and optionally saved as artifact.

**Why this priority**: Enables powerful data analysis capabilities but is not required for basic outreach functionality. Can be added after core agent and prioritization work.

**Independent Test**: Can be tested by requesting a simple data analysis, verifying script generation, confirming sandbox execution with no file system access, and validating results display.

**Acceptance Scenarios**:

1. **Given** user requests data analysis, **When** agent generates Python script, **Then** script is displayed to user for approval before execution
2. **Given** user approves script, **When** agent executes in sandbox, **Then** script has database read access only, no file system or network access
3. **Given** script attempts unauthorized operation, **When** sandbox detects violation, **Then** execution stops and user is notified of security violation
4. **Given** script completes successfully, **When** results are ready, **Then** data is formatted for display and user can save as artifact

---

### User Story 5 - Manage Environment Configuration Securely (Priority: P2)

User configures API keys, LLM provider settings, database credentials through UI. Configuration is encrypted and stored in Supabase. Agent loads configuration on startup without accessing local files.

**Why this priority**: Required for multi-device usage and security best practices. More important than Python execution but less critical than core agent functionality.

**Independent Test**: Can be tested by configuring credentials in UI, verifying encrypted storage in database, and confirming agent can authenticate to services using stored credentials.

**Acceptance Scenarios**:

1. **Given** user opens settings UI, **When** user enters API keys and saves, **Then** credentials are encrypted and stored in database with user-specific encryption key
2. **Given** credentials are stored, **When** agent needs to call LLM, **Then** agent retrieves and decrypts credentials without exposing them in logs
3. **Given** user switches devices, **When** user logs in on new device, **Then** agent loads configuration from database and operates identically
4. **Given** user updates LLM provider, **When** configuration changes, **Then** new provider is used for subsequent agent calls without application restart

---

### Edge Cases

- What happens when user provides malformed Python script request? Agent must validate syntax before attempting execution
- What happens when artifact query is ambiguous (multiple matches)? Agent must present disambiguation options to user
- What happens when LLM returns malformed action JSON? Agent must detect schema violations and request re-generation
- What happens when user stops agent while it's mid-action (e.g., sending LinkedIn message)? Agent must complete atomic action before stopping or mark for retry
- What happens when lead prioritization ties (identical scores)? Secondary sort by most recent profile update date
- What happens when sandbox environment exceeds resource limits? Script terminates gracefully with resource exceeded error
- What happens when user starts new chat session for existing campaign? Agent must load campaign state from database and continue without any conversation history dependency (core requirement, not edge case)

## Requirements *(mandatory)*

### Functional Requirements

#### Core Agent Architecture

- **FR-001**: System MUST implement Controller as single source of truth for all state mutations (Constitution Principle I)
- **FR-002**: System MUST persist all campaign state to database after each action completion
- **FR-003**: System MUST provide state snapshots to LLM agent without relying on conversation history
- **FR-004**: Agent MUST NOT mutate state directly - all state changes flow through Controller
- **FR-005**: System MUST support stop/continue workflow where user can interrupt agent and resume later

#### Deterministic Execution Loop (NON-NEGOTIABLE)

The Controller MUST implement a hard execution loop where each cycle proceeds in this exact order:

- **FR-005a**: **Step 1 - Load State**: Load authoritative state from database (campaign, tasks, leads, artifacts)
- **FR-005b**: **Step 2 - Select Task**: Select eligible tasks based on status and preconditions
- **FR-005c**: **Step 3 - Invoke Agent**: Invoke LLM agent with current state snapshot (no conversation history)
- **FR-005d**: **Step 4 - Validate Proposal**: Validate agent proposal against allowed action vocabulary and schema
- **FR-005e**: **Step 5 - Execute Action**: Execute validated action through Controller-mediated tools
- **FR-005f**: **Step 6 - Persist Logs**: Write audit log and event log entries (mandatory, cannot be skipped)
- **FR-005g**: **Step 7 - Update State**: Update task state only after verifying side effects completed
- **FR-005h**: **Step 8 - Repeat**: Return to Step 1 for next cycle

**Loop Invariants**: No step may be skipped. If any step fails, cycle is rejected and error state entered.

#### Agent Proposal Vocabulary (Finite, Controlled)

The Agent may propose ONLY the following action types (any other proposal MUST be rejected):

- **FR-005i**: `create_task` - Propose new task with description and preconditions
- **FR-005j**: `select_next_task` - Indicate which pending task to work on next
- **FR-005k**: `execute_tool` - Execute browser automation, filesystem, or CLI tool
- **FR-005l**: `generate_message` - Generate outreach message content for review
- **FR-005m**: `analyze_leads` - Run lead prioritization or analysis
- **FR-005n**: `request_user_input` - Indicate agent requires user decision or clarification
- **FR-005o**: `persist_artifact` - Request saving data as artifact (requires user approval)
- **FR-005p**: `no_op` - Explicitly signal no action needed (waiting state)

Each proposal MUST reference exactly one task ID or justify task creation.

#### Controller Invariants (Non-Negotiable)

The Controller MUST enforce these invariants and reject execution if any fails:

- **FR-006**: **No side effect without log entry** - Every tool invocation must produce audit log entry
- **FR-007**: **No task status change without verification** - Task cannot be marked done unless side effects verified
- **FR-008**: **No tool execution without mediation** - All tools (browser, filesystem, network) mediated by Controller
- **FR-009**: **State reloaded every cycle** - Fresh state load at start of every execution cycle
- **FR-010**: **Agent output validated** - Invalid outputs trigger retry or abort, never partial execution

#### Context Loss Prevention

- **FR-011**: System MUST persist todo list to database with each item's status (pending/in-progress/complete)
- **FR-012**: System MUST log all agent actions with timestamps and outcomes to database
- **FR-013**: System MUST resume campaigns from persisted state without re-processing completed work
- **FR-014**: System MUST avoid "summarizing conversation history" by not depending on message array for correctness
- **FR-015**: System MUST track current execution position (campaign ID, lead ID, action ID) in database

#### Artifact Management

- **FR-016**: System MUST allow users to save arbitrary structured/unstructured data as named artifacts
- **FR-017**: System MUST infer and store schema metadata for each artifact
- **FR-018**: System MUST enable artifact retrieval without conversation context (query by name, date, type)
- **FR-019**: System MUST version artifacts when updates occur
- **FR-020**: Artifacts MUST be user-scoped (no cross-user access)
- **FR-021**: Artifacts MUST be addressable by (campaign_id, artifact_type, artifact_key)
- **FR-022**: Artifacts MUST attribute source (user | agent) for audit purposes

#### Configuration Management

- **FR-023**: System MUST store environment configuration (API keys, DB credentials, LLM settings) encrypted in database
- **FR-024**: System MUST NOT store sensitive configuration in local files
- **FR-025**: System MUST support multiple LLM provider configurations (generic provider interface)
- **FR-026**: System MUST load configuration on application startup from database
- **FR-027**: System MUST allow configuration updates through UI without application restart
- **FR-028**: Agent MUST reference secrets by symbolic name only, never see raw values

#### Lead Prioritization

- **FR-029**: System MUST generate lead scoring algorithm dynamically based on user's campaign goals, with LLM proposing weighted criteria (0-100 score)
- **FR-030**: System MUST persist scoring algorithm blueprint as campaign artifact (artifact_type = scoring_algorithm) for reproducibility
- **FR-031**: Scoring algorithm MUST support criteria: job title relevance, company size, recent activity, profile completeness (weights determined by campaign goals)
- **FR-031**: Scoring algorithm MUST support criteria: job title relevance, company size, recent activity, profile completeness (weights determined by campaign goals)
- **FR-032**: System MUST explain prioritization score for each lead based on persisted algorithm blueprint
- **FR-033**: System MUST allow manual priority overrides that persist
- **FR-034**: System MUST re-prioritize on demand when new lead data available or algorithm updated
- **FR-035**: Lead scoring MUST be reproducible using saved algorithm blueprint (deterministic function)

#### Python Script Execution

- **FR-036**: System MUST generate Python scripts based on user natural language requests
- **FR-037**: System MUST display generated scripts to user for approval before execution
- **FR-038**: Approved scripts MUST be stored as immutable artifacts (artifact_type = python_script)
- **FR-039**: System MUST execute approved scripts in sandboxed environment with restricted permissions
- **FR-040**: Sandbox MUST allow database read access only (no write, no file system, no network)
- **FR-041**: System MUST terminate scripts exceeding resource limits (CPU time, memory)
- **FR-042**: System MUST format script results for display in UI
- **FR-043**: System MUST allow script results to be saved as artifacts
- **FR-044**: Agent MUST NOT modify scripts after user approval

#### Agent Communication Protocol

- **FR-045**: System MUST provide UI view listing all campaigns with status (initializing/active/paused/completed/error)
- **FR-046**: System MUST allow user to resume any campaign from UI without requiring chat conversation history
- **FR-047**: System MUST detect when agent requires user input (via status field in agent response)
- **FR-047**: System MUST detect when agent requires user input (via status field in agent response)
- **FR-048**: System MUST display agent output incrementally as it's generated (not batched)
- **FR-049**: System MUST handle agent responses without full conversation array (single-turn state snapshots)
- **FR-050**: System MUST support stop button that gracefully halts agent after current atomic action
- **FR-051**: Agent output MUST be persisted to database for audit trail
- **FR-052**: Agent input MUST include: current state, task list, leads summary, recent audit log (bounded)
- **FR-053**: Conversation history MUST be optional and non-authoritative in agent input

#### Campaign Lifecycle State Machine

Campaign state transitions MUST follow strict state machine rules:

- **FR-054**: Campaign states MUST be one of: initializing | active | paused | completed | error
- **FR-055**: State transition initializing → active requires environment + DB validation
- **FR-056**: State transition active → paused requires explicit user action
- **FR-057**: State transition active → completed requires all tasks marked done
- **FR-058**: State transition any → error requires invariant violation or unrecoverable failure
- **FR-059**: State transition paused → active requires explicit user action
- **FR-060**: State transitions MUST be Controller-only (UI cannot directly change campaign state)

### Key Entities

*(These map directly to Supabase tables for cross-session state recovery)*

- **Campaign**: Primary namespace key with id (PK), name, status (initializing/active/paused/completed/error), created_at. Campaign ID used to rehydrate all state.
- **Task**: Work unit with id (PK), campaign_id (FK), description, status (pending/in-progress/done/blocked), preconditions (optional), metadata (jsonb). Always loaded by campaign_id at startup.
- **Lead**: Prospect with id (PK), campaign_id (FK), full_name, profile_url, job_title, company, weight_score, status (pending/contacted/responded/rejected).
- **Artifact**: Persisted knowledge with id (PK), campaign_id (FK), artifact_type (controlled vocabulary: job_posting | scoring_algorithm | python_script | analysis_result), artifact_key (stable identifier), source (user | agent), content (jsonb/text), created_at. Addressable by (campaign_id, artifact_type, artifact_key).
- **AuditLog**: Tool invocation record with id (PK), campaign_id (FK), timestamp, action_type, payload. Records every tool execution.
- **EventLog**: State mutation record with id (PK), campaign_id (FK), timestamp, entity_type, entity_id, change_payload. Append-only log for replay.
- **Configuration**: Encrypted settings with id (PK), user_id, key_name, encrypted_value, config_type (api_key/llm_provider/database).
- **ExecutionState**: Current position with campaign_id (FK), current_task_id, last_action_timestamp. Enables resume from exact position.

## Clarifications

### Session 2026-01-06

- Q: Browser Automation Technology Selection - Which library should be used for LinkedIn interaction? → A: Playwright for .NET (modern, anti-detection resistant, officially maintained)
- Q: Python Sandbox Implementation Approach - What isolation mechanism should be used for sandboxed Python execution? → A: RestrictedPython library (pure Python sandboxing, simpler deployment)
- Q: LLM Provider Default Configuration - Which LLM provider should be the default/recommended choice for MVP? → A: OpenAI (GPT-4) (best structured outputs, mature ecosystem, balanced cost)
- Q: Lead Scoring Algorithm Weights - What default weight distribution should be used for lead prioritization? → A: LLM should dynamically generate scoring algorithm based on user's campaign goals input; algorithm blueprint persisted as campaign artifact
- Q: Campaign Resumability Scope - Should campaigns be resumable from UI without chat context? → A: Yes, core requirement (already specified in FR-002, FR-013, SC-012); UI must show campaign list and support resume via new chat session with zero conversation history dependency

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: User can stop agent mid-campaign and resume within 5 seconds without losing progress or context
- **SC-002**: Agent processes 100 leads without conversation history exceeding 10 messages (state snapshots only)
- **SC-003**: Application restart during active campaign results in zero data loss (all completed actions persisted)
- **SC-004**: User can retrieve artifacts from previous sessions with 100% accuracy (no context loss)
- **SC-005**: Lead prioritization scores correlate with user's manual rankings at >80% agreement
- **SC-006**: Python sandbox prevents 100% of unauthorized file system/network access attempts
- **SC-007**: 90% of users can successfully configure agent without technical support
- **SC-008**: Agent resumes execution within 3 seconds of user clicking continue button
- **SC-009**: Task state matches database state with 100% consistency after each execution cycle
- **SC-010**: Zero instances of "summarizing conversation history" or similar context-dependent errors
- **SC-011**: Controller enforces all 8 loop steps in sequence with zero skips across 1000 execution cycles
- **SC-012**: Deleting all chat history does not affect execution (can reconstruct from database alone)
- **SC-013**: Invalid agent proposals rejected with 100% accuracy (no partial execution)

## Assumptions

1. User has Supabase account and database already provisioned
2. User will configure LLM provider (default: OpenAI GPT-4 for best structured output quality) with valid API keys
3. LinkedIn automation will use Playwright for .NET (provides anti-detection and cross-platform reliability)
4. Initial MVP supports single user (multi-user auth added in future iteration)
5. Agent responses use JSON schema for structured output (not free-form text parsing)
6. Python sandbox implementation uses RestrictedPython library for permission control (no Docker required)
7. Existing shadcn + Tailwind UI prototype can be adapted for Blazor Hybrid

## Dependencies

- Supabase PostgreSQL database for state persistence
- Generic LLM provider interface (default: OpenAI GPT-4; supports Anthropic/local alternatives)
- Playwright for .NET for LinkedIn browser automation (anti-detection, cross-platform)
- Python runtime with RestrictedPython library for sandboxed script execution
- Blazor Hybrid MAUI runtime for desktop application

## Constraints

- Must run as desktop application (Windows 11+, macOS 12+)
- Must work offline for reading persisted state (sync required for new actions)
- Must not store sensitive data unencrypted
- Must comply with LinkedIn terms of service for automation (rate limiting, user consent)
- Must handle network interruptions gracefully
- Python scripts must execute within 60 seconds max
- Database queries must return within 2 seconds for responsive UI

## Out of Scope (Not in MVP)

- Mobile application support
- Multi-user collaboration on campaigns  
- Built-in CRM features beyond lead tracking
- Email outreach (LinkedIn only for MVP)
- Advanced analytics dashboards
- Browser extension for LinkedIn
- AI-powered message personalization (uses templates for MVP)
- Webhook integrations
- Export/import from other CRM systems

## Explicit Non-Goals (Architectural)

The following capabilities are intentionally excluded to maintain deterministic correctness:

- **Autonomous long-term planning**: Agent does not make multi-day strategic decisions
- **Self-modifying workflows**: Agent cannot change execution loop or invariants
- **Implicit memory**: No reliance on LLM's internal memory or context window
- **Multi-agent coordination**: Single agent only (no agent-to-agent communication)
- **Self-healing agents**: Failures require explicit recovery, not automatic retry

These may be layered in future iterations without violating the deterministic architecture.
