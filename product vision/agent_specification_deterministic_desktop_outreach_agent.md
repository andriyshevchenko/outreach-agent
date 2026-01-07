# Deterministic Agent Specification (MVP)

## 1. Purpose and Scope

This document specifies a **deterministic, state-driven desktop agent system** for running LinkedIn outreach campaigns.

The target implementation environment is:
- **Desktop runtime:** .NET MAUI
- **UI layer:** Hybrid Blazor (HTML/CSS-based)
- **Database:** Supabase (PostgreSQL)
- **Agent runtime:** Local controller process
- **LLM provider:** Abstracted (pluggable)

The primary goal is to eliminate **context loss as a correctness risk** by design. The system must remain correct even if:
- The LLM forgets prior turns
- Conversation history is truncated or summarized
- The application is restarted mid-campaign

This specification is **implementation-agnostic at the code level** but **prescriptive at the architectural and behavioral level**.

---

## 2. Core Design Principle

> The agent is not an actor. It is a proposer.

All authoritative state, side effects, and guarantees are owned by the **Controller**, not the LLM.

The LLM may:
- Propose actions
- Provide reasoning
- Generate content

The LLM may **not**:
- Mutate state
- Decide task completion
- Decide what has been logged
- Decide what is "done"

---

## 3. System Roles and Responsibilities

### 3.1 Controller (Authoritative, .NET)

The Controller is a deterministic, single-authority process running locally inside the MAUI application.

It:
- Owns all campaign state
- Owns the execution loop
- Owns database writes
- Owns logging
- Owns invariant enforcement
- Mediates all tool execution (browser, filesystem, CLI, network)

The Controller **must not rely on conversation history** for correctness.

---

### 3.2 Agent (LLM, Non-authoritative)

The Agent is an external or embedded LLM accessed via an abstract interface.

It:
- Receives a snapshot of authoritative state
- Proposes exactly one next action per cycle
- Generates content (messages, heuristics, search queries)

It must be treated as:
- Stateless
- Fallible
- Replaceable

The Agent **cannot mutate state** and **cannot assert task completion**.

---

### 3.3 UI (Hybrid Blazor, Non-authoritative)

The UI:
- Renders campaign state
- Displays tasks, logs, and leads
- Accepts user input and confirmations

The UI does not enforce correctness and does not bypass the Controller.

---

### 3.2 Agent (LLM)

The Agent:

- Receives a snapshot of authoritative state
- Proposes exactly one next action per turn
- Explains intent (optional)

The Agent is **stateless and replaceable**.

---

### 3.3 UI (Non-authoritative)

The UI:

- Displays state
- Accepts user input
- Initiates controller cycles

The UI does not enforce correctness.

---

## 4. Authoritative State Model

All state is externalized and reloadable.

### 4.1 Campaign State

- Campaign ID
- Campaign name
- Status (initializing | active | paused | completed | error)

---

### 4.2 Task State

Tasks are first-class, authoritative data records.

Each task MUST include:
- Task ID
- Description
- Status: pending | in_progress | done | blocked
- Preconditions (optional)
- Required side effects (optional, machine-verifiable)

Tasks:
- Are created and updated only by the Controller
- May be proposed by the Agent but never directly mutated by it

Any markdown or UI representation (e.g., `tasks.md`) is a **derived projection** and must be safely deletable.

---

### 4.3 Persistent Logs

#### Event Log (Authoritative)

- Every DB mutation
- Append-only
- Used for replay and recovery

#### Audit Log (Authoritative)

- Every tool invocation
- Every file read/write
- Every browser interaction

Logging is automatic and **cannot be skipped by the agent**.

---

## 5. Invariants (Non-negotiable)

The Controller MUST enforce the following invariants:

1. **No side effect without a log entry**
2. **No task status change without verifying side effects**
3. **No tool execution without controller mediation**
4. **State is reloaded at the start of every cycle**
5. **Agent output is validated before execution**

If any invariant fails, the step is rejected.

---

## 6. Agent Interaction Contract

### 6.1 Input to Agent

Each agent invocation receives:
- Current campaign state
- Full authoritative task list
- Relevant leads summary
- Recent audit log summary (bounded)
- Immutable system rules

Conversation history is optional and non-authoritative.

---

### 6.2 Output from Agent

The agent MUST return a structured proposal containing:
- Proposed action type
- Target task ID (or rationale for new task proposal)
- Required parameters

The Controller validates all outputs.

Invalid or incomplete proposals are rejected and retried.

---

### 6.2 Output from Agent

The agent MUST return a structured proposal:

- Proposed action type
- Target task ID
- Required parameters

Free-form narration is ignored.

If output is invalid or incomplete, it is rejected.

---

## 7. Execution Loop (Deterministic)

Each cycle proceeds as follows:

1. Load authoritative state
2. Select eligible tasks
3. Invoke agent with state snapshot
4. Validate agent proposal
5. Execute action (controller-owned)
6. Persist logs
7. Update task state (if verified)
8. Repeat

No step may be skipped.

---

## 8. Context Management Policy

### 8.1 Source of Truth

The only sources of truth are:
- Local persistent state
- Supabase-backed PostgreSQL tables

Chat history, summaries, and LLM memory are explicitly non-authoritative and may be lost at any time.

---

### 8.2 Cross-Session Rehydration

On application start or chat reset, the Controller MUST:

1. Require or select a campaign_id
2. Load campaign record
3. Load all tasks by campaign_id
4. Load all artifacts by campaign_id
5. Resume execution from persisted state

This process MUST NOT depend on prior conversation context.

---

### 8.3 Artifact Access by the Agent

Artifacts are injected into the agent prompt **only when relevant**, based on:
- artifact_type
- artifact_key
- active task requirements

The agent never assumes artifacts exist unless explicitly provided.

---

### 8.2 Summarization

If summaries are used:
- They are generated by the Controller
- They are bounded and replaceable
- They are never required for correctness

The system must remain correct if all summaries and chat history are deleted.

---

## 9. Failure and Recovery

The system MUST tolerate:

- Agent forgetting prior steps
- Agent repeating proposals
- Agent hallucinating completion
- Agent crashes or restarts

Recovery strategy:

- Reload state
- Resume execution loop

No manual intervention required.

---

## 10. Compliance Constraints (LinkedIn)

- All automation occurs within a user-controlled browser session
- Credentials are never exfiltrated
- No background automation without explicit user presence

Compliance constraints are enforced by the Controller, not the Agent.

---

## 11. MVP Acceptance Criteria

The MVP is considered valid if:

- Tasks complete correctly even if the agent forgets prior turns
- Logs are complete and replayable
- Restarting the app does not lose progress
- Removing chat history does not affect correctness

---

## 12. Explicit Non-Goals (MVP)

- Multi-agent coordination
- Autonomous long-term planning
- Self-modifying workflows
- Implicit memory

These may be layered later without violating this spec.

---

## 13. Design Philosophy (Summary)

- Determinism over autonomy
- Explicit state over conversational memory
- Rejection over silent failure
- Boring correctness over impressive fluency

This specification is intentionally conservative to guarantee reliability.

---

## 14. Controller Invariant Checklist (Mandatory)

The Controller MUST reject execution if any invariant fails.

### State & Execution
- State is loaded fresh at the start of every cycle
- Only one agent proposal is processed per cycle
- No execution occurs without a validated proposal

### Tasks
- No task may transition to `done` unless all required side effects are verified
- No task may be skipped unless explicitly marked `blocked`
- Agent may not directly mutate task state

### Persistence & Artifacts
- No data may be persisted unless explicitly requested by the user or confirmed by UI
- Agent proposals to persist data require explicit approval context
- Persisted artifacts must be typed and attributed (source: user | agent)

### Logging
- Every tool invocation produces an audit log entry
- Every DB mutation produces an event log entry
- Logs are written even if the agent output is invalid

### Agent Safety
- Agent output must match schema exactly
- Invalid outputs trigger retry or abort, never partial execution
- Agent memory is never trusted

---

## 15. Campaign Lifecycle State Machine

Campaigns MUST follow a strict lifecycle:

### States
- `initializing`
- `active`
- `paused`
- `completed`
- `error`

### Transitions
- initializing → active (after environment + DB validation)
- active → paused (user action)
- active → completed (all tasks done)
- any → error (invariant violation or unrecoverable failure)
- paused → active (user action)

State transitions are Controller-only.

---

## 16. Agent Proposal Vocabulary (Finite)

The Agent may propose only the following action categories:

- `create_task`
- `select_next_task`
- `execute_tool`
- `generate_message`
- `analyze_leads`
- `request_user_input`
- `persist_artifact`
- `no_op`

Any other proposal MUST be rejected.

Each proposal MUST reference exactly one task or justify task creation.

---

## 17. Supabase Data Model (Specification-Level)

Supabase (PostgreSQL) is used strictly as persistent storage under Controller authority. It is also the **sole cross-session memory mechanism**.

All data required to resume work after chat history loss MUST be recoverable from this schema alone.

### Required Tables

#### campaigns
- id (PK)
- name
- status
- created_at

The campaign ID is the **primary namespace key** used to rehydrate all state.

---

#### tasks
- id (PK)
- campaign_id (FK → campaigns.id)
- description
- status
- metadata (jsonb)

Tasks are always loaded by campaign_id at startup.

---

#### leads
- id (PK)
- campaign_id (FK → campaigns.id)
- full_name
- profile_url
- weight_score
- status

---

#### artifacts

Artifacts are the **only persisted form of arbitrary agent- or user-provided knowledge**.

- id (PK)
- campaign_id (FK → campaigns.id)
- artifact_type (string, controlled vocabulary)
- artifact_key (string, optional, stable identifier)
- source (enum: user | agent)
- content (jsonb or text)
- created_at

Artifacts are:
- Addressable by (campaign_id, artifact_type, artifact_key)
- Independently queryable across sessions
- Safe to reload without chat history

---

#### audit_log
- id (PK)
- campaign_id (FK → campaigns.id)
- timestamp
- action_type
- payload

---

#### event_log
- id (PK)
- campaign_id (FK → campaigns.id)
- timestamp
- entity_type
- entity_id
- change_payload

---

### Schema-Level Guarantees

- campaign_id is mandatory on all domain tables
- No row depends on chat history for interpretation
- No data is stored only in conversational form

### Explicit Constraint

If chat history is deleted, the Controller MUST be able to reconstruct:
- Campaign state
- Task state
- Persisted artifacts

From the database alone.

---

## 18. MVP Validation Criteria (Operational)

The system is considered correct if:

- Deleting all chat history does not affect execution
- Restarting the application resumes correctly
- Agent forgetting prior steps does not break task execution
- Logs can fully reconstruct campaign progress
- Persisted artifacts (context, environment, scripts, heuristics) remain accessible across sessions

---

## 20. Context, Environment, Heuristics, and Script Execution (Resolved)

This section specifies how previously ambiguous items are handled safely.

---

### 20.1 `context.md`

**Role:** Optional human-readable projection of campaign context.

**Authoritative storage:**
- Stored as an `artifact` with:
  - `artifact_type = context`
  - `artifact_key = campaign_context`

**Rules:**
- `context.md` is never authoritative
- It may be regenerated at any time from the artifact
- Loss of the file must not affect execution

**Injection:**
- Context artifacts may be injected into the agent prompt only when explicitly relevant

---

### 20.2 `environment.md` (Secrets and Configuration)

**Role:** Secure storage of environment configuration required to resume a campaign.

**Authoritative storage:**
- Secrets are NOT stored in plain artifacts
- Stored via one of:
  - OS secure storage (Keychain / Credential Manager)
  - Supabase encrypted secrets (if available)

**Rules:**
- Agent never sees raw secrets
- Agent may reference symbolic names only (e.g., `DB_CONNECTION_STRING`)
- `environment.md` is a redacted projection (non-sensitive)

---

### 20.3 Leads Prioritization Algorithm

**Role:** Deterministic scoring of leads.

**Authoritative storage:**
- Stored as:
  - Versioned configuration artifact (`artifact_type = lead_scoring_config`)
  - Optional deterministic function implemented in controller code

**Rules:**
- Agent may propose heuristic changes
- Controller validates and applies changes
- Lead scoring must be reproducible and replayable

---

### 20.4 Executing Python Scripts (Security Model)

**Role:** Allow controlled execution of generated or user-approved Python scripts.

**Security model:**

1. **Proposal phase**
   - Agent proposes script execution via `execute_tool`
   - Script content is treated as data

2. **Approval phase**
   - Script execution requires explicit user approval
   - Script is stored as an artifact:
     - `artifact_type = python_script`
     - Immutable once approved

3. **Execution phase**
   - Executed in a sandboxed environment:
     - No network access (unless explicitly granted)
     - Read-only filesystem (except temp directory)
     - No access to secrets

4. **Postconditions**
   - Outputs captured and logged
   - Side effects verified

**Rules:**
- The agent cannot modify scripts after approval
- Scripts cannot execute silently
- Failure aborts the task safely

---

## 19. Explicit Non-Goals (Reaffirmed)

- Autonomous long-term planning
- Self-healing agents
- Implicit memory
- Multi-agent coordination

These may be layered later without violating this specification.

